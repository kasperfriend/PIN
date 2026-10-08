# Battleframe ability verification

How the player-facing battleframe kit fares against the server's aptitude
pipeline, verified directly against `clientdb.sd2` (prod-1962).

## What "verified" means here

For every module seated in a stock battleframe's loadout (the same
`Lib/Shared.Common/Characters/ChassisStockLoadouts.cs` table the server and
web hosts resolve, generated from `dbcharacter::CharCreateLoadout*`):

1. `dbitems::AbilityModule.ability_chain_id` must name a real
   `apt::AbilityData` row with a nonzero chain;
2. every node of that chain (`apt::BaseCommandDef`, walked `Id -> Next`) must
   classify as *runnable server-side*: environment `client` rows are no-ops by
   design, and every `server`/`both` command type must have a factory route to
   a real implementation (the chain runs exactly the commands the data
   declares);
3. `ActiveInitiation` is excluded on purpose: the combat controller that owns
   the activation already sends the acknowledgement, so the chain row is a
   documented no-op (running it would double-send).

Beyond that, this audit does **not** assert per-command parameter semantics
(direction math, effect values) — those rest on the audit fixes in the PR and
the unit-test suite; and true end-to-end requires a live client, which the
sandbox does not have.

Reproduce / keep it green:

```
python3 Tools/SdbDump/verify_ability_chains.py [--names]
python3 Tools/SdbDump/verify_ability_chains.py --check   # CI: census step
```

The `--check` mode pins the documented allowlist of remaining gaps: any new
placeholder (a lost factory route, or data starting to use an unimplemented
command type) fails CI; fixing a documented one only asks for an allowlist
cleanup.

## Scoreboard (34 chassis table, ability seats only)

| state | seats |
|---|---|
| chain fully implemented server-side | 73 |
| chain has a remaining documented gap | 51 |
| ability row with no chain (non-kit gear slots) | 18 |
| module with no ability link (gear/upgrade modules) | 80 |
| missing ability row | 0 |

All ok rows went through the full chain req/target/effect pipeline:
cooldowns (`TimeCooldown`, `InflictCooldown`, `InstantActivation`), loadout
item-stat registers (`LoadRegisterFromItemStat`), requirements
(`RequireHasEffect`, `RequireCState`, `RequireJumped`, `RequireSuperCharge`,
…), `ImpactApplyEffect`/status-effect chains, deployable/turret spawns,
`ForcePush`/`ApplyImpulse`, melee auxiliaries, med systems, SIN spawns.

## Remaining gaps (named abilities)

### Ability trigger subsystem — biggest single gap (29 abilities)
`ActivateAbilityTrigger` / `RegisterAbilityTrigger`: both command defs are
id-only, and the AeroMessages tree carries no client<->server ability-trigger
message flow, so what a "trigger" means at runtime is not derivable from the
shipped evidence. A protocol-capture of a charged activation (e.g. pressing
and releasing Overcharge on a live client) would unblock this.

Affected: **Shockwave** (Assault HKM), **Overcharge**, **Meteor Strike**,
**Afterburner**, **Absorption Bomb** (Dread HKM), **Heavy Armor**,
**Turret Mode**, **Auxiliary Weapon - Melee** (Rifle Bash / Hammer / Sword),
**Heavy Turret**, **Anti-Personnel Turret**, **Supply Station**, **Cryo
Bolt**, **Bulwark** (Electron), PvP Ground Stomp / Energy Sword; and the
frame passives using `RegisterAbilityTrigger` (**Ambush** (Nighthawk),
**Conduit** (Raptor), **Rally** (Dragonfly), **E-Tank** (Tigerclaw),
**Incinerator** (Firecat), **Necrotic Poison** (Recluse), **Personal
Shield** (Mammoth), **Radiation Leak**, **Arsenal EM Pulse**, **Equip
Assault Nanites**).

### TinyObjectCreate — 1 ability
`OLD Emergency Response` (Dragonfly). Id-only def; the tiny-object entity
family is not modeled. The row is also a superseded "OLD" ability.

### RequireItemDurability — 1 ability
`Medical System - Primary Shared Activation Effect` on the PvP test frames
(Bastion/Dreadnaught). Per-slot item wear is not modeled (only a static
char-level prop exists), so no honest answer server-side.

### Frame ability groups (passive kit) — documented exclusion
`dbcharacter::AbilityGroupModule` / `AbilityGroupPassive` load nowhere, so
a battleframe's passive group (sprint/jetpack/core triggers, e.g. core group
32) cannot be enrolled from data. Button abilities are unaffected (they
resolve through loadout modules).

## Fourth audit round — transitive closure, and what it turned up

The scoreboard above walks each ability's **linear** node list. That undercounts:
a chain reaches more chains through `ConditionalBranch` (`if`/`then`/`else`),
`LogicOrChain`, `LogicAndChain` and `Call`, and every `ImpactApplyEffect` pulls in
the applied effect's `apply`/`remove`/`update`/`duration` chains. Following all of
those edges, the 143 stock-kit ability seats reach **1304 chains**, and the gap
count is materially different:

| state | seats |
|---|---|
| reachable closure fully implemented | 41 |
| closure holds at least one gap | 102 |

The **Teleport Beacon** is the clearest case the linear walk missed: its recall
lives in the `else` branch of a `ConditionalBranch`, and both nodes in it were
broken — `TargetFromStatusEffect` ignored its only parameter, and `Teleport` was
a `return true` placeholder. Neither appeared in any earlier table.

### Closed this round

- **Activation replication** — `AbilityActivated` was sent only to the acting
  player's own channel. Every `apttf::` row of a chain (`tfPlayAnimation`,
  `tfParticleEffectAsset`, `tfBeamEffect`, `tfCameraShakeEffect`,
  `tfAbilityAnimation`) is `environment=client`, so the server no-ops it and the
  client runs it — *for a client that heard the activation*. A client that never
  heard it played no cast animation and drew no effect, so abilities were
  invisible to everyone but their caster, and an NPC's ability (which activates
  through `AbilitySystem` with no combat-controller echo at all) was invisible to
  everyone. It is now announced from `ExecuteAbilityActivation`, the single
  funnel every root activation passes through, on the same `SendToScoped` path
  `TookHit` and `WeaponProjectileFired` use. The actor's own client is skipped:
  it already gets the ReliableGss acknowledgement.
- **`TargetFromStatusEffect`** — 72 rows, every one with a nonzero `StatusfxId`,
  `AlsoInitiator` 0 in 67 of them. It pushed the initiator when that flag was set
  and returned, so the 67 rows asking for the effect carriers produced an empty
  target list and everything downstream ran against nothing. It now scans the
  shard for the carriers, which is what the chain shapes ask for (they clear or
  never populate the list first).
- **`Teleport`** — id-only def, 42 rows. Moves the target list to
  `Context.InitPosition`, the initiation position the other movement commands
  read; every chain shape settles the list first and then teleports it. Follows
  `TeleportServerCommand`: position write, fall-damage reset, then a
  `ForcedMovement` type 1 so the client does not interpolate across the map.
  *Documented limit:* a recall to a position recorded by an *earlier* activation
  (the beacon's "back to where I threw it") needs the destination effect's stored
  context, and an id-only def has no column to name it.
- **`BattleFrameDuration`** — routed, but to a bare `return true`, so a gate
  asking "still in the qualifying frame" always said yes. **73 nodes in the
  stock kits** — the largest single silent no-op found. `Classtype` is
  `dbitems::Battleframe.Archtype` (value sets 0/5/6/11/13 vs 0/2/5/6/7/8/9/11/13);
  `Notchanged` compares the new `CharacterEntity.LastLoadoutChangeTime` with the
  effect's own start time.

### Still open, by size

Remaining placeholders in the transitive closure, by node count:
`ActivateAbilityTrigger` 56, `RegisterAbilityTrigger` 45, `SlotAmmo` 13,
`Bullrush` 8, `DetonateProjectiles` 8, `RegisterTimedTrigger` 7,
`ItemAttributeModifier` 6, `RegisterClientProximity` 5, `SetWeaponDamage` 5,
`TinyObjectCreate` 4, then `TargetByNPC`, `ModifyHostility`,
`TargetByDamageResponse`, `RequireAbilityPhysics`, `AddPhysics` (3 each) and a
tail of 1–2. One routed command is still a no-op: `RequirementServer` (10 nodes).

Note the shipped `verify_ability_chains.py --check` allowlist is keyed to the
*linear* walk, so it does not see the branch-reachable rows above; it still passes
unchanged (31 documented placeholder pairs). Extending it to the closure would
add several new pairs and needs its allowlist rewritten in the same change.

## Fifth audit round — implementing what the data can actually specify

Round four's scoreboard counts gaps; this round closes them where the definition
carries enough information to close them honestly. The dividing line used
throughout is **can the row say what it means?** A def with real columns can be
implemented against its own data; a def that is nothing but an id cannot, and
guessing at it makes abilities behave *wrongly*, which is worse than a documented
no-op.

`apt::CommandType` has 394 rows. **186** are routed to a command class in
`Factory`; another **145** appear there only as commented-out cases, and the rest
have no case at all. Of the unrouted, most are not ability machinery anyway
(arc/mission control, matchmaking, the loot store, chat bubbles).

### Implemented

| command | rows | what it does now |
|---|---|---|
| `Bullrush` | 31 | Rhino/Dreadnaught *Charge*. Maps onto the protocol's own `ForcedMovementType.Bullrush` (6) block: `Speed`/`Duration` become its `Speed` and `StartTime`/`EndTime`, `Velocity` is the horizontal facing scaled by speed. |
| `TargetByDamageResponse` | 243 | Target filter sibling of `RequireDamageResponse`; reads the target's own `DamageResponseId`, falling back to the def's column. |
| `DropCarryable` | id only | Tail of the throw-the-object chains; same transition as `DropAllCarryable` today. |
| `RegisterClientProximity` | 239 | **The proximity trigger.** `radius`/`max_targets`/`retry_interval`/`chain`/`ability_id` are all loadable, so this is implemented rather than documented away. |
| `DetonateProjectiles` | 59 | Blows the caster's in-flight rounds early through the simulation's own expiry path. |
| `SetProjectileTarget` | 52 | Redirects already-homing rounds onto the chain's target. |
| `SlotAmmo` | 115 | Weapon ammo substitution — closes the `// TODO: Handle ammo overrides` in `WeaponSim`. |
| `NamedVariableAssign` | 291 | The missing half of a matched pair (see below). |

### Two of these were half-built features, not missing commands

**`NamedVariableAssign` / `LoadRegisterFromNamedVar`.** The reader (type 240) was
already implemented but could only ever take its `undecl_value` fallback, and said
so in its own doc: nothing ever declared a variable. The two tables share one
vocabulary — `WingFX`, `damage`, `FuseLength`, `teslacount`, `heat` — which is the
evidence they address the same store, and both key on `name_id` (the string
`member_name` is empty in 119 of 291 assign rows and 156 of 265 read rows). The
store now exists, per entity, on the ability system. The glider pad keeps working
exactly as before: chain 1001671 row 1001663 reads `WingFX` with fallback 1.0 and
no chain in the pad's activation assigns it.

**`SlotAmmo` / `WeaponSim`.** The weapon simulation carried the matching hole as
an explicit `// TODO: Handle ammo overrides`. The substitution is held on the
character rather than in the weapon-details cache, because that cache is rebuilt
on every loadout change and an override stored in it would vanish mid-ability.

### Adaptations, stated at the code site

- `RegisterClientProximity` is named for the **client** detecting proximity, and
  the AeroMessages tree carries no client→server "something entered my radius"
  message. The server answers the same question by scanning the shard, which is
  why the row's retry interval is honoured instead of firing every tick.
- `Bullrush`'s `ImpactEffect` is nonzero in exactly 1 of 31 rows and needs a swept
  collision test the server has no geometry for. Not applied; documented.
- `TargetByDamageResponse`'s `UseWeaponDamageType` is 0 in all 243 rows, so it is
  left alone rather than guessed. Same for `SetProjectileTarget`'s
  `TargetingThis` (0 in 52) and `SlotAmmo`'s `CreditToAbility` (no representation
  on the projectile path — guessing would attribute kills wrongly).
- `NamedVariableAssign`'s `VarSrctype` has no enum anywhere in the codebase (1 in
  279 of 291 rows, 0 in 12). Both sides key on the executing entity, which is what
  makes the assign/read pair round-trip; modelling the column would only matter
  for sharing a variable *across* entities.

### Still genuinely blocked, and why

- **`RegisterAbilityTrigger` (45), `ActivateAbilityTrigger` (56),
  `RegisterTimedTrigger` (7)** — id-only defs, no `aptgss::` trigger table in the
  clientdb, no `Trigger` message in AeroMessages, and no table with a `trigger`
  column except `dbdialogdata::DialogScript`. There is nothing to read.
- **`TargetSquadmates` (24), `RequireSquadLeader`** — there is no squad system in
  this codebase: no squad membership on `CharacterEntity`, no squad service. The
  command has nothing to read.
- **`SetWeaponDamage` (79)** — `Dmgminvalue`/`Dmgmaxvalue` and `Multiply` are
  unambiguous, but `Lerpminvalue`/`Lerpmaxvalue` are driven by
  `Lerpfallheight`/`Lerpenergy` and which range each selects is not derivable.
  Applying the unambiguous half alone would silently produce wrong damage on the
  rows that use the lerp, so the whole command is left routed-out.
- The `ModifyDamageBy*` family, `ItemAttributeModifier`, `SetDefaultDamageBonus`,
  `AddAppendageHealthPool`, `ModifyHostility` — id-only defs.
- Line of sight (no world raycast), headshot (no hit locations),
  `RequireBulletHit`, `RequireItemDurability`, SIN acquisition.

### Verification

No .NET SDK exists in the sandbox this was developed in (no `dotnet`, `mono` or
`csc` on the filesystem), so the build and the 1421-test suite run in CI only.
Three pushes failed the build on missing `using` directives before that was
addressed with a pre-flight resolver that indexes all 4015 types declared across
`UdpHosts/` and the `Lib/` submodules and checks every type name a changed file
mentions is reachable from its namespace and imports; it is self-tested by
reintroducing the exact error CI reported. A separate check confirms all 186
command classes `Factory` constructs are reachable from its imports.

## Per-frame detail

#### Accord Assaultframe
| slot | module | ability | name | chain | state |
|---|---|---|---|---|---|
| 3 | 78528 | 30181 | Jetpack - Permissions | 888469 | ok |
| 4 | 78925 | 0 | ? | - | no ability link (gear/upgrade module) |
| 5 | 78518 | 0 | ? | - | no ability link (gear/upgrade module) |
| 6 | 78512 | 35540 | Shockwave | 1577895 | **gap:** ActivateAbilityTrigger |
| 7 | 78507 | 35448 | Overcharge  | 1634270 | **gap:** ActivateAbilityTrigger |
| 8 | 78503 | 127 | ? | 1549842 | ok |
| 9 | 82745 | 35573 | Crater | 1574694 | ok |
| 10 | 78522 | 35630 | Equip Assault Nanites | 925396 | **gap:** RegisterAbilityTrigger |

#### Dreadnaught
| slot | module | ability | name | chain | state |
|---|---|---|---|---|---|
| 6 | 125199 | 41881 | Absorption Bomb | 1634176 | **gap:** ActivateAbilityTrigger |
| 116 | 126000 | 0 | ? | - | no ability link (gear/upgrade module) |
| 121 | 90493 | 35366 | Charge | 1619015 | ok |
| 122 | 129505 | 39845 | Auxiliary Weapon - Melee, Hammer | 1410869 | **gap:** ActivateAbilityTrigger |
| 126 | 127501 | 0 | ? | - | no ability link (gear/upgrade module) |
| 127 | 128271 | 0 | ? | - | no ability link (gear/upgrade module) |
| 128 | 126731 | 0 | ? | - | no ability link (gear/upgrade module) |
| 129 | 129067 | 0 | ? | - | no ability link (gear/upgrade module) |
| 132 | 91296 | 35535 | Heavy Armor | 1634235 | **gap:** ActivateAbilityTrigger |
| 133 | 123309 | 39434 | Turret Mode  | 1605621 | **gap:** ActivateAbilityTrigger |
| 138 | 125285 | 0 | ? | - | no ability link (gear/upgrade module) |
| 139 | 91024 | 41881 | Absorption Bomb | 1634176 | **gap:** ActivateAbilityTrigger |

#### Recon
| slot | module | ability | name | chain | state |
|---|---|---|---|---|---|
| 6 | 91770 | 35567 | Artillery Strike | 1582822 | ok |
| 116 | 126000 | 0 | ? | - | no ability link (gear/upgrade module) |
| 121 | 91662 | 39405 | Cryo Bolt | 1619445 | **gap:** ActivateAbilityTrigger |
| 122 | 129359 | 39846 | Auxiliary Weapon - Melee, Sword | 1315423 | **gap:** ActivateAbilityTrigger |
| 126 | 127501 | 0 | ? | - | no ability link (gear/upgrade module) |
| 127 | 128271 | 0 | ? | - | no ability link (gear/upgrade module) |
| 128 | 126731 | 0 | ? | - | no ability link (gear/upgrade module) |
| 129 | 129067 | 0 | ? | - | no ability link (gear/upgrade module) |
| 132 | 90285 | 35909 | Teleport Beacon | 1614911 | ok |
| 133 | 90020 | 34668 | Remote Explosive | 1606809 | ok |
| 138 | 125285 | 0 | ? | - | no ability link (gear/upgrade module) |
| 139 | 91793 | 35567 | Artillery Strike | 1582822 | ok |

#### Biotech
| slot | module | ability | name | chain | state |
|---|---|---|---|---|---|
| 6 | 89124 | 41867 | Heroism | 1634203 | ok |
| 116 | 126000 | 0 | ? | - | no ability link (gear/upgrade module) |
| 121 | 123271 | 34753 | Healing Generator | 1608827 | ok |
| 122 | 129213 | 39844 | Auxiliary Weapon - Melee, Rifle Bash | 1315454 | **gap:** ActivateAbilityTrigger |
| 126 | 127501 | 0 | ? | - | no ability link (gear/upgrade module) |
| 127 | 128271 | 0 | ? | - | no ability link (gear/upgrade module) |
| 128 | 126731 | 0 | ? | - | no ability link (gear/upgrade module) |
| 129 | 129067 | 0 | ? | - | no ability link (gear/upgrade module) |
| 132 | 88808 | 41866 | Adrenaline Rush | 1634211 | ok |
| 133 | 123262 | 41865 | Poison Ball | 1618829 | ok |
| 138 | 125285 | 0 | ? | - | no ability link (gear/upgrade module) |
| 139 | 89147 | 41867 | Heroism | 1634203 | ok |

#### Engineer
| slot | module | ability | name | chain | state |
|---|---|---|---|---|---|
| 6 | 91559 | 39426 | Anti-Personnel Turret  | 1591207 | **gap:** ActivateAbilityTrigger |
| 116 | 126000 | 0 | ? | - | no ability link (gear/upgrade module) |
| 121 | 91394 | 39360 | Heavy Turret | 1614550 | **gap:** ActivateAbilityTrigger |
| 122 | 129359 | 39846 | Auxiliary Weapon - Melee, Sword | 1315423 | **gap:** ActivateAbilityTrigger |
| 126 | 127501 | 0 | ? | - | no ability link (gear/upgrade module) |
| 127 | 128271 | 0 | ? | - | no ability link (gear/upgrade module) |
| 128 | 126731 | 0 | ? | - | no ability link (gear/upgrade module) |
| 129 | 129067 | 0 | ? | - | no ability link (gear/upgrade module) |
| 132 | 131211 | 41880 | Overclock | 1634332 | ok |
| 133 | 91510 | 39423 | Supply Station  | 1619632 | **gap:** ActivateAbilityTrigger |
| 138 | 125285 | 0 | ? | - | no ability link (gear/upgrade module) |
| 139 | 91582 | 39426 | Anti-Personnel Turret  | 1591207 | **gap:** ActivateAbilityTrigger |

#### Tigerclaw
| slot | module | ability | name | chain | state |
|---|---|---|---|---|---|
| 3 | 78528 | 30181 | Jetpack - Permissions | 888469 | ok |
| 4 | 78925 | 0 | ? | - | no ability link (gear/upgrade module) |
| 5 | 79942 | 0 | ? | - | no ability link (gear/upgrade module) |
| 6 | 78850 | 35637 | Supercharge | 1634220 | ok |
| 7 | 79934 | 34665 | Missile Shot | 817166 | ok |
| 8 | 79938 | 35633 | OLD Trailblaze | 1174650 | ok |
| 9 | 79930 | 35527 | Pulsar | 1619407 | ok |
| 10 | 79946 | 35582 | E-Tank (Tigerclaw Passive) | 375041 | **gap:** RegisterAbilityTrigger |

#### Firecat
| slot | module | ability | name | chain | state |
|---|---|---|---|---|---|
| 3 | 78525 | 30181 | Jetpack - Permissions | 888469 | ok |
| 4 | 78924 | 0 | ? | - | no ability link (gear/upgrade module) |
| 5 | 78517 | 0 | ? | - | no ability link (gear/upgrade module) |
| 6 | 78554 | 35627 | Fuel Air Bomb | 1384014 | ok |
| 7 | 78542 | 35521 | Immolate | 1542950 | ok |
| 8 | 78536 | 35458 | Thermal Wave | 1634242 | ok |
| 9 | 78548 | 35518 | ? | - | ability row chains nowhere |
| 10 | 78565 | 35613 | Incinerator (Firecat Passive) | 543746 | **gap:** RegisterAbilityTrigger |

#### Assault
| slot | module | ability | name | chain | state |
|---|---|---|---|---|---|
| 6 | 88491 | 35540 | Shockwave | 1577895 | **gap:** ActivateAbilityTrigger |
| 116 | 126000 | 0 | ? | - | no ability link (gear/upgrade module) |
| 121 | 88039 | 39243 | Meteor Strike | 1613990 | **gap:** ActivateAbilityTrigger |
| 122 | 129213 | 39844 | Auxiliary Weapon - Melee, Rifle Bash | 1315454 | **gap:** ActivateAbilityTrigger |
| 126 | 127501 | 0 | ? | - | no ability link (gear/upgrade module) |
| 127 | 128271 | 0 | ? | - | no ability link (gear/upgrade module) |
| 128 | 126731 | 0 | ? | - | no ability link (gear/upgrade module) |
| 129 | 129067 | 0 | ? | - | no ability link (gear/upgrade module) |
| 132 | 88099 | 39257 | Afterburner | 1607791 | **gap:** ActivateAbilityTrigger |
| 133 | 88324 | 35448 | Overcharge  | 1634270 | **gap:** ActivateAbilityTrigger |
| 138 | 125285 | 0 | ? | - | no ability link (gear/upgrade module) |
| 139 | 88514 | 35540 | Shockwave | 1577895 | **gap:** ActivateAbilityTrigger |

#### Mammoth
| slot | module | ability | name | chain | state |
|---|---|---|---|---|---|
| 3 | 78525 | 30181 | Jetpack - Permissions | 888469 | ok |
| 4 | 78924 | 0 | ? | - | no ability link (gear/upgrade module) |
| 5 | 78748 | 0 | ? | - | no ability link (gear/upgrade module) |
| 6 | 78743 | 35367 | ? | - | ability row chains nowhere |
| 7 | 78739 | 35525 | ? | - | ability row chains nowhere |
| 8 | 78734 | 31259 | Thunderdome | 1634306 | ok |
| 9 | 78728 | 34697 | ? | - | ability row chains nowhere |
| 10 | 79965 | 35377 | Imminent Threat - Mammoth Passive | 418511 | ok |

#### Rhino
| slot | module | ability | name | chain | state |
|---|---|---|---|---|---|
| 3 | 78528 | 30181 | Jetpack - Permissions | 888469 | ok |
| 4 | 78528 | 30181 | Jetpack - Permissions | 888469 | ok |
| 5 | 85206 | 0 | ? | - | no ability link (gear/upgrade module) |
| 6 | 78779 | 34066 | Dreadfield | 1634297 | ok |
| 7 | 78768 | 35366 | Charge | 1619015 | ok |
| 8 | 78759 | 35494 | Sundering Blast | 1609739 | ok |
| 9 | 78773 | 34659 | ? | - | ability row chains nowhere |
| 10 | 82842 | 35501 | Personal Shield (Mammoth Passive) | 1342662 | **gap:** RegisterAbilityTrigger |

#### Nighthawk
| slot | module | ability | name | chain | state |
|---|---|---|---|---|---|
| 3 | 78528 | 30181 | Jetpack - Permissions | 888469 | ok |
| 4 | 78925 | 0 | ? | - | no ability link (gear/upgrade module) |
| 5 | 79974 | 0 | ? | - | no ability link (gear/upgrade module) |
| 6 | 78909 | 35453 | Eruption | 1634324 | ok |
| 7 | 78904 | 35345 | Smoke Screen | 1614188 | ok |
| 8 | 78900 | 34668 | Remote Explosive | 1606809 | ok |
| 9 | 78895 | 34671 | Execution | 1634289 | ok |
| 10 | 79978 | 35348 | Ambush (Nighthawk Passive) | 930828 | **gap:** RegisterAbilityTrigger |

#### Raptor
| slot | module | ability | name | chain | state |
|---|---|---|---|---|---|
| 3 | 78528 | 30181 | Jetpack - Permissions | 888469 | ok |
| 4 | 78925 | 0 | ? | - | no ability link (gear/upgrade module) |
| 5 | 78858 | 0 | ? | - | no ability link (gear/upgrade module) |
| 6 | 79004 | 35618 | Overload | 1634341 | ok |
| 7 | 79000 | 35909 | Teleport Beacon | 1614911 | ok |
| 8 | 79008 | 34681 | SIN Scrambler | 1545241 | ok |
| 9 | 79012 | 35354 | ? | - | ability row chains nowhere |
| 10 | 79986 | 35465 | Conduit (Raptor Passive) | 809736 | **gap:** RegisterAbilityTrigger |

#### Dragonfly
| slot | module | ability | name | chain | state |
|---|---|---|---|---|---|
| 3 | 78528 | 30181 | Jetpack - Permissions | 888469 | ok |
| 4 | 78925 | 0 | ? | - | no ability link (gear/upgrade module) |
| 5 | 79036 | 0 | ? | - | no ability link (gear/upgrade module) |
| 6 | 78844 | 34928 | Healing Dome | 1634280 | ok |
| 7 | 79056 | 35445 | OLD Emergency Response | 1247005 | **gap:** TinyObjectCreate |
| 8 | 79046 | 34832 | ? | - | ability row chains nowhere |
| 9 | 79051 | 35040 | ? | - | ability row chains nowhere |
| 10 | 79990 | 35509 | Rally (Passive) - Dragonfly Passive | 594436 | **gap:** RegisterAbilityTrigger |

#### Recluse
| slot | module | ability | name | chain | state |
|---|---|---|---|---|---|
| 3 | 78525 | 30181 | Jetpack - Permissions | 888469 | ok |
| 4 | 78924 | 0 | ? | - | no ability link (gear/upgrade module) |
| 5 | 79035 | 0 | ? | - | no ability link (gear/upgrade module) |
| 6 | 79076 | 35620 | Necrosis | 1634227 | ok |
| 7 | 79068 | 34691 | Kinetic Shot | - | ability row chains nowhere |
| 8 | 79063 | 34734 | Creeping Death | 1607623 | ok |
| 9 | 79072 | 34997 | ? | - | ability row chains nowhere |
| 10 | 79080 | 35621 | Necrotic Poison (Recluse Passive) | 1198968 | **gap:** RegisterAbilityTrigger |

#### Electron
| slot | module | ability | name | chain | state |
|---|---|---|---|---|---|
| 3 | 78528 | 30181 | Jetpack - Permissions | 888469 | ok |
| 4 | 78925 | 0 | ? | - | no ability link (gear/upgrade module) |
| 5 | 79954 | 0 | ? | - | no ability link (gear/upgrade module) |
| 6 | 78671 | 35583 | Electrical Storm | 1592796 | ok |
| 7 | 78666 | 35460 | Overclocking Station | 600898 | ok |
| 8 | 78660 | 35455 | Bulwark | 1634262 | **gap:** ActivateAbilityTrigger |
| 9 | 78654 | 34770 | Boomerang Shot  | 1605999 | ok |
| 10 | 79950 | 35446 | Fail-safe (Electron Passive) | 418406 | ok |

#### Bastion
| slot | module | ability | name | chain | state |
|---|---|---|---|---|---|
| 3 | 78528 | 30181 | Jetpack - Permissions | 888469 | ok |
| 4 | 78925 | 0 | ? | - | no ability link (gear/upgrade module) |
| 5 | 79962 | 0 | ? | - | no ability link (gear/upgrade module) |
| 6 | 78706 | 35596 | Fortify | 1383638 | ok |
| 7 | 78700 | 35487 | ? | - | ability row chains nowhere |
| 8 | 78693 | 35229 | Shield Wall | 1548714 | ok |
| 9 | 78687 | 34629 | Multi Deploy Turret | 1618920 | ok |
| 10 | 78723 | 35576 | Overseer | 562571 | ok |

#### KS-G01 \
| slot | module | ability | name | chain | state |
|---|---|---|---|---|---|
| 3 | 78070 | 30181 | Jetpack - Permissions | 888469 | ok |
| 4 | 78068 | 0 | ? | - | no ability link (gear/upgrade module) |
| 5 | 78418 | 0 | ? | - | no ability link (gear/upgrade module) |
| 10 | 82415 | 35879 | Radiation Leak (Graviton Passive) | 432021 | **gap:** RegisterAbilityTrigger |

#### Arsenal
| slot | module | ability | name | chain | state |
|---|---|---|---|---|---|
| 3 | 78524 | 30181 | Jetpack - Permissions | 888469 | ok |
| 4 | 78921 | 0 | ? | - | no ability link (gear/upgrade module) |
| 5 | 85405 | 0 | ? | - | no ability link (gear/upgrade module) |
| 6 | 82486 | 35887 | ? | - | ability row chains nowhere |
| 7 | 82365 | 35841 | ? | - | ability row chains nowhere |
| 8 | 82372 | 35849 | ? | - | ability row chains nowhere |
| 9 | 82379 | 34980 | ? | - | ability row chains nowhere |
| 10 | 82490 | 36268 | Arsenal Electromagnetic Pulse | 638984 | **gap:** RegisterAbilityTrigger |

#### Archangel
| slot | module | ability | name | chain | state |
|---|---|---|---|---|---|
| 6 | 82536 | 35888 | Fires from Heaven | 437089 | ok |
| 7 | 82521 | 34554 | Hover Mode | 426507 | ok |
| 8 | 82526 | 35844 | Evasive Maneuver | 1549883 | ok |
| 9 | 82531 | 35858 | Heavy Laser | 436950 | ok |
| 116 | 101763 | 0 | ? | - | no ability link (gear/upgrade module) |
| 117 | 90860 | 0 | ? | - | no ability link (gear/upgrade module) |
| 118 | 104882 | 0 | ? | - | no ability link (gear/upgrade module) |

#### A-43 \
| slot | module | ability | name | chain | state |
|---|---|---|---|---|---|
| 3 | 78070 | 30181 | Jetpack - Permissions | 888469 | ok |
| 4 | 78068 | 0 | ? | - | no ability link (gear/upgrade module) |
| 5 | 78078 | 0 | ? | - | no ability link (gear/upgrade module) |
| 6 | 77773 | 35540 | Shockwave | 1577895 | **gap:** ActivateAbilityTrigger |
| 7 | 77127 | 34966 | ? | - | ability row chains nowhere |
| 8 | 75273 | 34580 | ? | - | ability row chains nowhere |
| 9 | 77726 | 35518 | ? | - | ability row chains nowhere |
| 10 | 77902 | 35613 | Incinerator (Firecat Passive) | 543746 | **gap:** RegisterAbilityTrigger |
| 13 | 85729 | 0 | ? | - | no ability link (gear/upgrade module) |
