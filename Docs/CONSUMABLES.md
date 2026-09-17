# Consumables, kits and boosts

How a usable item is activated, what its aptitude chain does, and whether the copy leaves the
inventory. This is the result of an audit of every `dbitems::RootItem` of type 7 (Consumable) in
the prod-1962 `clientdb.sd2` against the server's aptitude runtime, plus the Powerup (9), kit and
boost items that share the machinery. Numbers are from that database; the scripts that produced
them walk `dbitems::AbilityModule` → `apt::AbilityData` → the chain, recursing through
`ConditionalBranch` / `LogicAndChain` / `LogicOrChain` / `Call`.

## 1. The activation path

1. The client sends `ActivateConsumable { ItemSdbId, Time }` (`CombatController.ActivateConsumable`).
2. The item's `dbitems::AbilityModule` row (keyed by the item's own sdb id) names the
   `apt::AbilityData`; the controller refuses the activation up front when the player carries none
   (`CharacterInventory.HasItemOrResource`), otherwise runs the chain with
   `Context.AbilityModuleId = ItemSdbId`.
3. The chain's `ConsumeItem` node (aptitude command 244, server-only) takes one copy from the
   activating player - from the resource pool consumables live in (`ItemStacking`), or a guid copy
   when an older inventory still holds those.
4. The root activation commits its cooldowns only when the whole chain succeeded
   (`AbilitySystem.CommitActivationCooldowns`); since this audit it also **rolls back** what a failed
   chain already changed (`Context.ActivationRollbacks`): a spent consumable goes back.
5. The client is told through `InventoryUpdate` (resource quantity, a full resend for a removed guid
   item) and `BagInventoryUpdate` (the bag layout, when a slot appeared or disappeared).

Kits and boosts are ordinary consumables - the same message, the same chain shapes.

## 2. Where the chain spends the item (1,569 consumables with an ability module)

| Shape | Items | Examples | Spent? |
|---|---:|---|---|
| `ConsumeItem` in the root chain | 459 | Health/Ammo Packs, grenades, 1-Use Glider/Jump Pads, Arcfold Beacons, Sonic Detonators, Beta Crystite Crate | yes |
| `ConsumeItem` inside a branch | 64 | SIN Bridge, Pumpkin Candies, Wintertide Present, Horn of Plenty, Red Pocket | yes, when that branch runs |
| no `ConsumeItem` - reusable or a different mechanic | 424 | Diamondwing / Crystalwing Glider Pads, Recharging Ammo Pack, Gene Sampler, Scan Hammer, LGVs, Release Drone | never (by data) |
| account unlocks (`Unlock*`, `AddAccountGroup`) | 424 | New You Unlocks, Titles, warpaints, pilot licences, rental contracts | never (by data; and the unlock itself is a placeholder, see §5) |
| calldowns that `DeployableCalldown` without consuming | 92 | thumpers, Tiki Torch, Reusable Battleframe Station | never (by data) |
| `UnpackItem` packages | 44 | Packaged Quicksilver Glider Pad, Packaged Tiki Pets, Packaged Recharging Fireworks | should be replaced by the content - placeholder |
| loot crates (`GrantOwnerItem` / `SpawnLoot`) | 33 | Brontodon Transgenome, Key/Structural/Catalyst Fragments | should be replaced by the roll - placeholder |
| no `apt::AbilityData` | 29 | Red Bean, Pilot Licences 86642+ | not activatable |

### Gate order

The data does not always check before it spends:

- **224** root chains run `InstantActivation` (the cooldown gate that also *starts* the cooldown)
  *after* `ConsumeItem`: Health Pack, Small is `ActiveInitiation, TimeCooldown, StatRequirement,
  ConsumeItem, ImpactApplyEffect, InstantActivation`. In 176 of them the leading `TimeCooldown` row
  (`duration 0`, `check_local/category/global`) covers the same cooldown, so a click inside the
  window is refused before the pack is touched. In **48** it does not - there is no `TimeCooldown`
  before `ConsumeItem` at all (calldown 30166, Ammo Pack 30192, Arcfold Beacon 34132, the Precision
  Pyrotechnics Fuses 76980-76982, 34142) - and a click inside the window used to fail at
  `InstantActivation` with the item already gone.
- **8** rental contracts (141555-141562) test `RequireHasItem(141541, negate)` after consuming.

This is why `ConsumeItem` now registers a rollback: a chain that spends the item and is then
refused hands it back, and no cooldown starts.

## 3. What was broken and what changed

| Finding | Effect on the player | Fix |
|---|---|---|
| `Return` (command 105) was a `return true` stub. 43 consumables use it as the end of the "cannot use it now" branch: every glider pad / calldown is `ConditionalBranch(if AirborneDuration; else notification, InstantActivation, Return)` followed by `ConsumeItem, DeployableSpawn, ...`. | A grounded glider pad told the player it could not launch, **then spent the pad and spawned it anyway**. | `ReturnCommand` sets `Context.ReturnRequested`; `Chain.Execute` unwinds every frame on that context and reports the last command's result; `ConditionalBranch` / `WhileLoop` stop on it. A called ability or applied effect runs on a copied context, so a return there ends only that chain. |
| `StatRequirement` (98) was a stub. 762 rows; 660 compare Health against a percentage of MaxHealth. | A **Health Pack at full health was accepted and spent**; the heal-over-time effect's duration chain (`TimeDuration, StatRequirement(health < 100%)`) never ended early. | Implemented: `stat1 (lt/gt/eq) value` or `value % of stat2`, vitals read from the live pools (`AptitudeStatReader`). |
| `LoadRegisterFromStat` read every stat as a *modifier* (1.0 unmodified). 343 of 359 rows read MaxHealth or Health: the pack effect is `SetRegister 0.2, LoadRegisterFromStat(MaxHealth) x, HealDamage(1 x register)`. | A Health Pack **healed 1 hit point**. Brontodon stomp damage (`0.005 x MaxHealth`) was 1 as well. | Vitals (6/7/8/9/11/12) come from the live pools; everything else stays a modifier. |
| Removing a guid item (`ConsumeItemBySdbId`, `RemoveItem`) sent nothing to the client. Stack consumption sent the quantity but the bag layout kept the empty slot. | A consumed or salvaged guid item **stayed in the bag until relog**; clicking it produced activations the server refused. An emptied stack kept its slot. | `SendItemsRemoved` (full `InventoryUpdate` + `BagInventoryUpdate`; the protocol has no per-item removal we know of), `SendBagUpdate` when a pool appears or empties. |
| `ConsumeItem` had no rollback; cooldowns did (deferred commit). | Second click inside the cooldown **ate the item for nothing** (48 unguarded chains, see §2); any later rejection - a nested chain, a called ability - did the same. | `Context.ActivationRollbacks`, run newest-first by the root activation on failure; stack → `AddResource`, guid copies → `RestoreItem` with the old guid. |
| `RequireHasItem` counted guid items only. 83 of its 183 rows name a consumable (glider pad tiers in ability 37924, rental contracts), which live in stacks. | Pad tier selection never matched; rental "already have it" checks passed. | Uses `HasItemOrResource`. |

Tests: `ConsumableActivationTests` (Health Pack spends one and heals 20 % of max; refused and kept
at full health; refused and kept inside the cooldown; stack and guid rollback; rollback through a
called ability; the grounded glider pad returns before `ConsumeItem`, the airborne one spends the
pad; a `Return` two logic levels down ends the ability but not the next activation; a `Return` in a
called ability ends only that chain; `StatRequirement` percent/absolute cases;
`LoadRegisterFromStat` reads the live pools).

### Notes on `Return` semantics

`apt::ReturnCommandDef` carries `return_success`, `return_halt`, `return_yield`, `return_status`.
In the data:

- all-zero as the last node of an ability chain: 239 abilities (NPC melee/apply chains like
  `ImpactApplyEffect, Return`), 28 else-chains, 6 then-chains, 9 effect apply chains;
- `halt=1`: 36 effect update chains (`RequireHasEffect, EncounterSignal, Return`), 12 else-chains;
- `success=1`: 36 effect duration chains consisting of the single `Return` (an effect that lives
  until removed), 7 abilities;
- 5 NPC abilities carry nodes after a mid-chain all-zero `Return` (35815, 35925, 37474, 37853,
  41258) - those trailing nodes are unreachable, as written.

The server treats all four flags as the plain return; the unwound frames report the returning
command's result (success), so the branch that ran to its `Return` counts as having run and the
`InstantActivation` cooldown it queued starts. `return_halt`/`return_yield` presumably control the
update-chain loop and `return_status` the client's failure notification; neither has a server
meaning yet.

## 4. Kits

102 items carry "Kit" in their name:

- **Battleframe Visual Unlock Kits** (85452-85463, 85792-85793) and the other unlock items: `InstantActivation,
  UnlockTitles / UnlockWarpaints / UnlockDecals / ..., ImpactApplyEffect(400)`. Their chains carry no
  `ConsumeItem`; the live server spent the kit inside the unlock command, and so does this one
  (`PlayerRewards.ConsumeActivatingItem`, once per activation, only for type 7/9 items, undone with
  the activation). Using one records the unlock in `CharacterUnlocks` (§7), sends `UnlocksUpdate`,
  and the kit leaves the bag.
- **Level 5 / 10 / 15 Upgrade Kits** (86412+, one per frame): `TimeCooldown, TargetSelf,
  ConsumeItem, ImpactApplyEffect(400), SpawnLoot, InstantActivation`. `SpawnLoot` now rolls a
  `dbitems::LootTable` (`LootTableRoller`) into the bag. The client database never shipped the
  server-side table ids for these rows, so `CustomData/aptgss_SpawnLootCommandDef.json` was
  authored from the loot tables that *are* in the database: the kits use the "Level N Uncommon
  <Slot>" gear tables (7129-7454), the booster packs their own tables (5474-5510), the Secure
  Lockers / Caches their masters (6826-7564), PTS crates 6555-6568. Rows still without a table log
  once and grant nothing (the kit is still spent by its own `ConsumeItem`, as on live).

## 5. Boosts and the former placeholders

42 boost items (XP Boost x18, Reputation Boost x12, Crystite Boost x5, ...) are `TimeCooldown,
ApplyPermanentEffect, ImpactApplyEffect(3509 | 4351 feedback), ConsumeItem`. `ApplyPermanentEffect`
now records a typed, timed boost (`xp` / `crystite` / `reputation`, percent, duration or permanent)
on the character, pushes it to the client through the BaseController modifier props
(`XpBoostModifierProp`, `PermanentStatusEffectsProp`, ...), and sweeps expiry every minute and at
login. There is no XP system on the server yet, so the boost is *held* (visible, persisted,
expiring) rather than multiplying anything; `CharacterUnlocks.BoostFraction(type, now)` is the hook
for whoever awards XP/crystite/reputation. `RemovePermanentEffect` (Cleanse 143945, the polymorph
removers) drops boosts by effect id / type.

Commands that used to return `true` doing nothing, and what they do now (rows = `CustomData`
rows, data = rows with a payload authored; the rest log once):

| Command | Rows / with data | Now |
|---|---:|---|
| `GrantOwnerItem` | 302 / 102 | grants `item_sdb_id` x `quantity` (or rolls `loot_table_id`); `cost_sdb_id` x `cost_quantity` pays first (50 fragments -> 1 component; the activating stack is the cost, not spent twice) |
| `SpawnLoot` | 399 / 95 | rolls one or several loot tables into the bag, honouring `roll_mode` (0/1 weighted pick, 2 every row, 3/4 per-row chance) and `stack_duplicate_results` |
| `UnpackItem` | 45 / 34 | takes the package, grants its content list |
| `ShowRewardScreen` | 284 / 71 | `DisplayRewards` with every item the activation granted (`Context.AwardedItems`) |
| `AddAccountGroup` | 179 / 127 | timed membership (LGV rentals 25/60 min, VIP flags); a second contract extends the remaining time |
| `ApplyPermanentEffect` / `RemovePermanentEffect` | 112 / 62, 4 / 0 | boosts, above |
| `UnlockTitles` | 101 / 89 | unlock + sets the title when the character has none |
| `UnlockWarpaints` / `UnlockDecals` / `UnlockPatterns` / `UnlockOrnaments` / `UnlockHeadAccessories` / `UnlockVisualOverrides` | 92/64, 35/19, 26/17, 142/115, 25/24, 2/0 | cosmetic unlock groups |
| `UnlockBattleframes` | 34 / 27 | frame chassis unlock (Holmgang Pilot Licenses 142187-142202) |
| `UnlockCerts` | 310 / 189 | certificate unlock (recipes, campaign tokens) |
| `UnlockContent` / `ApplyUnlock` | 35 / 0, 20 / 0 | wired; no ids identified in the database yet |
| `ModifyOwnerResources` / `AddFactionReputation` / `AwardRedBeans` | 145/1, 4/3, 2/2 | resource pool delta, faction reputation ledger, red beans |
| `RequireHasUnlock` / `RequireHasCertificate` / `RequireAppliedUnlock` | client `aptfs` tables | gate on the ledger (`negate` honoured); the 94 `negate=1` Campaign Token rows now block a double redeem |

The ids were matched by localized name (`Tools/SdbDump/author_item_command_defs.py`, re-runnable);
a row whose name matched nothing keeps `0` and its `comment`, so the activation still succeeds
(the client's own feedback plays) and the log says which row needs authoring. The remaining gaps are
mostly `SpawnLoot` rows on comment-less delivery items and `UnlockCerts` rows whose certificate has
no name in the client.

`AddLootTable` and `RequireLootStore` (NPC drop plumbing, reached by no item) stay placeholders.

## 7. The unlock ledger

`CharacterUnlocks` (per `CharacterEntity`) holds unlock groups (titles, warpaints, decals,
czi_patterns, ornaments, head_accessories, battleframes, certificate, visual_overrides, applied),
account groups with expiry, boosts with expiry, and faction reputation. It is loaded with the
character (`CharacterAndBattleframeVisuals.Unlocks`), sent as one `UnlocksUpdate` after the
inventory at login, saved through `SaveCharacterUnlocks` (GameServerAPI) after every successful
activation that touched it (`Context.DirtyUnlocks`) and when the player leaves the shard. Records
are `(kind, group, id, value, expires_at)`; expired entries are dropped on load.

## 6. Powerups and non-consumable items with `ConsumeItem`

18 Powerup (type 9) items (Health Powerup 30286 and friends) and two odd rows (54028, an ability
module; 122897 "Ultra XP & test passive", a frame module) carry `ConsumeItem`. The node spends
whatever `Context.AbilityModuleId` names, so they behave like consumables whenever an activation
carries their sdb id and are no-ops otherwise (an activation without a module id never spends).
