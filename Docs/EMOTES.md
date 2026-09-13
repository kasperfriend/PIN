# Emotes and the rest of the animation data

> Scope: the animation surface the client owns and the server only *starts*, besides the attack,
> reload, locomotion and death animations covered by `Docs/NPC_AI.md` §3. Everything below is read
> out of the build's `clientdb.sd2` (`prod-1962`); where a rule is not in a table it is called out as
> unknown rather than invented.

A Firefall character's animation is selected by the client from replicated state. Three of those
states exist besides the weapon markers:

| State | Wire | Source |
|---|---|---|
| Emote id | `ObserverView.EmoteID`, `BaseController.EmoteID` (`GSS/Character/CharacterShared.cs` `EmoteData`) | `dbcharacter::EmoteRecord` (`PerformEmote` command) |
| Status effects | the character's `StatusEffects_0..31` fields | the chains of `apt::StatusEffectData` — the client runs the client-side commands (`apttf::*`) inside them, which is how a **chain plays an animation** |
| Dialog line | `PlayDialogScriptMessage` (`GssMessage`) / `PrivateDialog` / `PublicDialog` | `dbdialogdata::DialogScript` |

## 1. What is implemented

`PerformEmote` (`GssCharacterCommand.PerformEmote`, `EmoteData { Id, Time }`) is validated against
`dbcharacter::EmoteRecord` before it is replicated (`GameServer/Systems/Emotes/EmoteService.cs`):

* `EmoteRecord` is now loaded into `SDBInterface` (`GetEmoteRecord` / `GetEmoteRecords`).
* An id the table does not hold is **ignored** — replicating it would ask every client in range to
  resolve an animation that does not exist. `EmoteServerCommand` reports the same refusal.
* Emote id 0 is the "not emoting" state: it clears `EmoteData` (no emote row has id 0).
* Four rows name a `statuseffect`, and performing them applies that effect through the shard's
  `AbilitySystem` (`DoApplyEffect`), which replicates it so every client runs its chain (below).
  The player's own client is not the only client that animates the emote: the effect is the
  authoritative state.

## 2. `dbcharacter::EmoteRecord` — 382 rows, ids 1..1482

| Column | Meaning / coverage |
|---|---|
| `name` | `dance`, `npc_calm`, `npc_alert`, `guardidle`, `townstand1..6`, `standcombat`, `taunt`, `roar`, `howl`, `burrow`, `sit_chair`, `mission0xx_*` (scripted), `pvp_executioner_headstomp`, ... |
| `anim_override_id` | 176 rows name an animation override (the client's animation network id), e.g. 1037 `sad` → 157, 1292 `handcuffs` → 113 |
| `animation_name` | 1 row: 1462 `firedance` → `tikidance` |
| `head_anim_override_id` | 1 row: 1377 `npe_executioner_reveal` → 162 |
| `statuseffect` | 4 rows: 1460 `shocking` → 13551, 1462 `firedance` → 4348, 1478 `heartbooth_left` → 14701, 1479 `heartbooth_right` → 14752 |
| `flags` | 0 (×148), 2 (×130), 35 (×38), 34 (×21), 162 (×15), 1 (×8), 32 (×7), 33 (×5), 6 (×3), 38 (×2), 96 (×2), 8 (×1) — meaning not in the build |
| `collision_offset` | non-zero on exactly 1 row |

### The four rows that carry a status effect

The effect *is* the emote on the wire, and it is co-simulated — both sides run the same chains and
execute the commands they own:

| Emote | Effect | Chains that matter |
|---|---|---|
| 1460 `shocking` | 13551 | apply: `tfPerformEmote`, `tfSwitchMaterial`; duration: `RequireInCombat`, `RequireMoving`, `AirborneDuration` |
| 1462 `firedance` | 4348 | update (200 ms): `tfParticleEffectAsset`; the animation itself is the row's `animation_name` |
| 1478/1479 `heartbooth_*` | 14701/14752 | apply: `tfSetAnimCtrlParam`, `tfPlayAnimation`; duration: `TimeDuration` |

This is the general mechanism for every animation that is not a weapon marker: **the server applies
the effect, the client plays the client-side commands inside its chains, and the effect's own
duration chain (server-side commands and/or a client-side timer) ends it.** Applying the emote's
effect is therefore all the server has to do, and it is what makes the emote visible to every client
rather than only to the player who asked for it.

## 3. The chains the server can already run: 530 `tfPerformEmote` commands

`apt::BaseCommandDef` holds 143,498 command instances; 530 of them are
`apttf::tfPerformEmoteCommandDef` (subtype 138, "Feedback - Perform Emote" in
`Systems/Aptitude/CommandType.cs`). The definition table has no parameter columns — a `PerformEmote`
command carries no emote id: **the emote is identified by the effect whose chain carries it**, which
is why `EmoteRecord.statuseffect` is the only emote → effect link in the database.

* 359 `apt::StatusEffectData` rows contain a `PerformEmote` command in one of their four chains.
* 3 `apt::AbilityData` rows contain one directly (no effect in between).
* None of the 530 is reachable from a monster *weapon*, and no monster weapon's chains hold an emote: of
  the 19 weapon
  templates the build's 3,109 monsters use (85 templates / 1,861 slots in total) 14 carry an attack or
  burst ability id (102 slots), of which 7 (75 slots) carry client feedback the engine now runs — 2
  animate (12143 the charge sniper's channel fire, 21 melee Shadowstrike), 3 deliver their own hit, and
  the other four (51, 12157, 12264, 12183) carry the weapon's muzzle flash and sound. The remaining
  client commands are reachable only from the effects of abilities the mobs do not hold, or from
  chains that hold no client command at all (documented in `Docs/NPC_AI.md` §3 and §6). `Docs/NPC_AI.md`
  §3 lists the same walk per template, including the seven templates whose attack/burst chains are
  server-side only.

One of the 530 *is* reachable from a mob, just not from a weapon: the behaviour-set ability modules
(`am1Id`/`am2Id`, walked in `Docs/NPC_AI.md` §3) are chains too, and module `86132` — the
`Arch_MoveThenFire` set, named by 12 monster references — runs ability `36817`, whose chains perform
the `roar` emote next to animation 28 and their own damage. It is the only emote among the 26 module
ids (24 of them reach an animation instead), and the engine runs those modules now, so a
`MoveThenFire` mob roars, as the data says.

Consequence for the rest: apart from that one module and the behaviour string's own `emote=` below, an
NPC has no **data path of its own** to an emote in this build — a mob emotes only if something applies
one of those effects to it (a scripted encounter, a deployable, a future `ApplyClientStatusEffect`
implementation). The general rule the walk above established is in `Docs/NPC_AI.md` §3: a chain is run
when it carries a command the client executes (`apt::CommandType.environment` = `client`, the
`apttf::` tables), which for a mob means the effect that holds the animation, the emote, the muzzle
flash or the sound — so an emote effect applied to an NPC *does* animate it. NPCs do have an emote path
of their own that is not a chain: **207 monster rows name an emote in their behaviour string** (§7),
which is where an NPC's emote now comes from. What the data gives every NPC besides that is dialog,
below.

## 4. `dbdialogdata::DialogScript` — 39,261 lines

The NPC voice/emote line table. Columns: `character_type`, `text_id`, `sound_event_id`, `emote_id`,
`voice_set`, `mood`, `delay_ms`, `next_id`, `trigger`, `display_mode`, `look_at_target`, `is_public`,
`priority`.

| Fact | Value |
|---|---|
| `character_type` | 785 distinct: 784 are `dbcharacter::Monster` ids, 0 is generic |
| Lines with an emote | 356, 13 distinct emotes: 1275 `talk` ×321, 12 `salute` ×21, 1068 `Focus` ×2, 60 `utility` ×2, 59 `cheer` ×2, then 1067 `Working\|Working_Standing`, 1069 `FocusGreeting`, 1070 `GoodbyeWorking`, 4 `taunt`, 53 `wave`, 55 `roar`, 56 `laugh` and 57 `cry` once each |
| Lines with a sound event | 33,548 |
| Lines with text | 38,707 |
| `delay_ms` | −1 = none (37,949), else 500-4,000 ms |
| `next_id` | 13,334 lines chain to a follow-up line |
| `voice_set` | 2,318 lines are voice-set specific |
| `trigger` | 0 (×38,988), 1 (×228), 2 (×45) — meaning not in the build |
| `mood` | 0 (×38,541), else 1/3/4/5/6 (see §5) |
| `display_mode` | 1 (×36,061), 0 (×3,200) |
| `priority` | 4 (×14,500), 0 (×9,002), 9 (×5,276), 7 (×4,787), 10 (×2,431), 8 (×2,135), ... resolved through `dbdialogdata::DialogPriority` (10 rows: `level` + `interrupts` + `sin_imprint`) |

`PlayDialogScriptMessage { DialogId, Unk1 }` is the wire message (the id is an `AeroSdb` reference,
so the client resolves text, sound, emote and mouth movement itself). Encounters already have
`Systems/Encounters/BaseEncounter.PlayDialog` (dialog ids in
`dbencounterdata::MapMarkerInfo.introRadioId` / `stage2..4RadioId` and `dbcharacter::Deployable.dialog_script_id`).
`DialogService` now plays a line: a public script is `PlayDialogScriptMessage` on ReliableGss to
every client the speaker is scoped into; a private script is `PrivateDialog { Time, Entity, DialogId }`
to the listener. `NotifyDialogScriptComplete { Unk1, Unk2 }` on Character BaseController walks
`next_id` (the first non-zero of the two unnamed uints is the completed line). The seven monster
rows that name `dialogScript=` in their behaviour string (10551 on 612/620/621/622, 39340 on vendors
2118/3013/3096) play that line when a player finishes interacting with them
(`EndInteractionCommand`) — they are not auto-played when the AI registers the NPC.

**Battle chatter is loaded and resolved, but not mapped onto combat events:** which of the 6
`dbdialogdata::BattleChatterDescriptions` rows applies to which event, and what `trigger` 1/2 mean,
are not in the database, so a caller has to name a description id. The tables themselves are clear:

* `BattleChatterDescriptions` (6 rows): `spread_meters` (5-50), `duration_ms` (2,000-10,000),
  `memoryless`, `default_dialog_script_id` (44,931), `dialog_script_set_id` (1417/1418/1421/1422),
  and the three probability columns `probability_ally` / `probability_targeted_player` /
  `probability_hostile` (a bark is rolled per listener by its relation to the speaker).
* `BattleChatterSetParams` (159 rows, one per line of the four sets): `voice_set_key` is a packed
  key — **high 32 bits = `dbcharacter::VoiceSet` id, low 32 bits = set id** (verified on all 159
  rows) — plus `speaker_key`, `set_tags` and the `dialog_id` to play.
* `BattleChatterScriptTags`: 1 row (set 1417, tag `NPE`); `CombatView.BattleChatterTag` (byte[2]) is
  the replicated tag it feeds.

Voice sets are the join key between an NPC and its lines: `dbcharacter::Monster.voice_set` (1,803 of
3,109 monsters carry one, 105 distinct) is already replicated as `CharacterVisuals.VoiceSet`
(`CharacterShared.cs`), and `dbcharacter::VoiceSet` (463 rows) has `group`, `sex`, `display_flags`.

## 5. Moods — client-side portraits (not implemented)

`dbcharacter::MonsterMood` (2,268 rows) maps `monster_id` + `mood` → `portrait_id`;
`dbcharacter::MonsterMoodName` names the states: 0 Neutral, 1 Excited, 3 Thinking, 4 Angry,
5 Happy, 6 Sad (mood 2 is used by 15 rows and has no name). Nothing in the character views carries a
mood — the selection is client-side — and `DialogScript.mood` is the only server-visible use of it.
Documented, not implemented.

## 6. Other animation tables found, by status

| Table | Rows | Status |
|---|---|---|
| `dbquickchatdata::QuickChatCommand` (`name_id`, `soundrecord_id`, `keybinding`, `category_id`, `order`, `radius`, `emote_id`) | 93 | The client's quick-chat/emote menu. **Not one of the 93 rows names an `emote_id`** (all zero) or a particle effect, and `PerformQuickChatCommand` / `QuickChat` carry a single unnamed uint each with no server handling, so the menu plays nothing in this build. Validation of a performed emote therefore uses `EmoteRecord` (the superset) rather than this list |
| `dbcharacter::PoseType` | 48 | Physics/collision per pose; `Monster.posetype_id` is replicated through the entity, no server rule |
| `dbcharacter::Head` (`animnet_id`) | 67 | Client-side |
| `dbitems::BattleframeVisuals` / `Battleframe` (`animnetwork_id`, `hand_animnetwork_id`, `anim_armed_id`, `posetype_id`) | 2,786 / 1,676 | Client-side (visuals are already replicated from the loadout) |
| `dbvisualrecords::VisualRecord` | 11,698 | Client-side |
| `dbcharacter::TinyObject` (`posefile_id`), `dbcharacter::Deployable` (`animnetwork`) | 379 / 3,902 | Client-side |
| `vcs::*` pose files (`driver_pose_file`, `gunner_pose_file`, ...) | 73/24/22/7 | Client-side (vehicles) |
| `dbitems::Weapons` (`first_person_animnet_id`, `third_person_animnet_id`) | 6,789 | Client-side |
| `dbcharacter::Stumble` (`anim_index`, `statusfx_id`, `duration`, `cooldown_ms`, `distance`, `only_once`, 39 rows), `dbcharacter::StumbleDirection` (`stumble_id`, `anim_substate`, 120 rows) and `dbcharacter::GibVisuals.death_anim_index` (128 rows) | 39/120/128 | Hit reactions and death animations. Stumble is applied by an explicit id (`StumbleService.TryStumble`): the victim gets BaseController `Stumble` (ushort, ushort, byte) and the row's `statusfx_id`; `restrict_stumble` / `only_once` / `cooldown_ms` are honoured. Damage does not pick a stumble id — no weapon, ammo or damage-type row names one (see `Docs/NPC_AI.md` §6). Gib visuals are still the death path |
| `aptfs::ForcePushCommandDef.do_animation`, `aptfs::SwitchWeaponCommandDef.play_animation` | 632 / 343 | Server commands with a client feedback flag; their defs are already loaded, the flags are not acted on |
| `aptfs::ApplyClientStatusEffectCommandDef` / `RemoveClientStatusEffectCommandDef` | 3 / 8 | Server → client "apply/remove this effect **on your side**" commands. `Commands/Effect/Todo/ApplyClientStatusEffectCommand.cs` exists but is not wired into `Factory`, which is the remaining path by which a server-side rule could start a client-only animation without a replicated effect |

## 7. NPC emotes: the monster behaviour string

Every emote above is something a *player* performs or an *ability* applies. The database does give an
NPC an emote of its own, in the one place that is easy to miss: the `emote` parameter of
`dbcharacter::Monster.behavior`.

| Fact | Value |
|---|---|
| Monster rows naming an emote | **207** of 3,109 - all in the base `behavior` column; three rows repeat it in `behavior_offensive` and `behavior_defensive`, and only monster 3258/3259 has it in a combat behaviour at all |
| Distinct names | 62, of which **60 are rows of `EmoteRecord`** |
| Most named | `calm` (28 rows, emote 1062), `officer` (22, 1105), `kioskviewer3` (12), `guard` (7, 1097), `controlseat` (7, 1096), `kioskviewer1/2` (7 each, 1098/1099), `townstand4` (7, 1044) |
| Named once or twice | gestures (`gesture1`-`gesture3`, `wave`-like town poses), counter work (`typing`, `typing01`, `dataentry01`, `lazyengineer`, `working`, `workingtech01`), seats (`sittingchair01/03/05`, `sittingstairs01/03`, `controlseat`), `dance` (monster 2053), `tikidance` (2063), `sleep01`, `smoking01`, `oilspill`, `trash01`, `statuepose01/02/03`, `cover` (1456), `utility` (60) |
| Names the emote table does not have | 2 rows: `waterplant01` on monster 999 (the table has `DELETEwaterplant01Delete`, 1138) and `townstand04` on monster 2481 (`townstand4` is 1044) - resolve-and-fail, never a substitution |
| Emote length | `emoteDuration=-1` on the 59 rows that state it: hold the emote until the behaviour set changes |
| Also in the behaviour | 7 rows name a `dialogScript=<id>` to say (10551 on 612/620/621/622, 39340 on the vendors 2118/3013/3096) plus `interactionType` / `lookAtTarget`; that is dialog rather than animation, and the interaction it belongs to is a client-side conversation in this build |

**Implemented.** The wire side was already there - `EmoteData` is replicated on `ObserverView` and
`BaseController`, which is exactly what `CharacterEntity.SetEmote` writes and what a client plays an
NPC's emote from - so the work was the rule for *when* an NPC holds its emote, and the database states
it in the same string: the emote belongs to the behaviour set that is running. `AiEngine` resolves the
name at registration (`EmoteService.ResolveEmoteName`, case-insensitively, into the emote table) and
then keeps the NPC's emote in step with its brain:

* `Idle` and `Return` run the base `behavior`, so the NPC holds its idle emote from its first tick
  (`AlertAndInteractive(emote="calm")` -> emote 1062).
* `Chase` and `Attack` run `behavior_offensive`, which names an emote on one monster only
  (3258/3259, the same `calm`), so an NPC that engages drops its pose - the emote of a set that has
  no emote is no emote.
* `Dead` clears it.
* `emoteDuration` is honoured: the 59 rows that state it all state `-1`, "hold it until the
  behaviour changes", and a row that states a number of seconds has the emote cleared when it is up
  without restarting while the same set still asks for it.
* A name the emote table does not have (monsters 999 and 2481) resolves to nothing: those NPCs have
  no emote rather than a substituted one.
* The 2,902 monster rows that name no emote are untouched - `SyncBehaviorEmote` finds the same emote
  id on every tick and sends nothing.

`behavior_defensive` is not read, because the emulator's brain has no defensive state to run it in,
and the `dialogScript=` parameters on seven of these rows are dialog rather than animation: they
play on interaction, not when the AI registers the NPC (see §4).

Feature coverage: `NpcBehaviorParamsTests` (the `emote`/`emoteDuration` parameters, including a
behaviour that names none) and four `AiEngineTests` cases - the pose a `calm` NPC takes, its removal
when the NPC engages, a timed emote ending and not restarting, and the typo name posing nothing.

## 8. Tests

`GameServer.Tests/EmoteServiceTests.cs`: an emote in the table is replicated with its id and time, an
emote outside it is ignored and leaves a running emote alone, id 0 stops the emote, the rows with a
`statuseffect` apply it once with the emote's timestamp, rows without one apply nothing, and the
emote still replicates when the shard has no ability system (or the effect is refused).
