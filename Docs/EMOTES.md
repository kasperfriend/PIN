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
* None of them is a `tfPerformEmote`, and none is reachable from a monster weapon: of the 19 weapon
  templates the build's 3,109 monsters use (85 templates / 1,861 slots in total) 14 carry an attack or
  burst ability id (102 slots), of which 7 (75 slots) carry client feedback the engine now runs — 2
  animate (12143 the charge sniper's channel fire, 21 melee Shadowstrike), 3 deliver their own hit, and
  the other four (51, 12157, 12264, 12183) carry the weapon's muzzle flash and sound. The remaining
  client commands are reachable only from the effects of abilities the mobs do not hold, or from
  chains that hold no client command at all (documented in `Docs/NPC_AI.md` §3 and §6). `Docs/NPC_AI.md`
  §3 lists the same walk per template, including the seven templates whose attack/burst chains are
  server-side only.

Consequence: an NPC has no **data path of its own** to an emote in this build — a mob emotes only if
something applies one of those effects to it (a scripted encounter, a deployable, a future
`ApplyClientStatusEffect` implementation). The general rule the walk above established is in
`Docs/NPC_AI.md` §3: a chain is run when it carries a command the client executes (`apttf::`), which
for a mob means the effect that holds the animation, the emote, the muzzle flash or the sound — so an
emote effect applied to an NPC *does* animate it. What the data does give NPCs of their own is dialog,
below.

## 4. `dbdialogdata::DialogScript` — 39,261 lines (not implemented)

The NPC voice/emote line table. Columns: `character_type`, `text_id`, `sound_event_id`, `emote_id`,
`voice_set`, `mood`, `delay_ms`, `next_id`, `trigger`, `display_mode`, `look_at_target`, `is_public`,
`priority`.

| Fact | Value |
|---|---|
| `character_type` | 785 distinct: 784 are `dbcharacter::Monster` ids, 0 is generic |
| Lines with an emote | 356 (13 distinct emotes: 1275 `talk` ×321, 12 ×21, `working`/`focus`/`goodbye` 1067-1070, `cry` 12, 53-60 ...) |
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
so the client resolves text, sound, emote and mouth movement itself), and it is already sent —
`Systems/Encounters/BaseEncounter.PlayDialog` has the helper (encounters hold dialog ids in
`dbencounterdata::MapMarkerInfo.introRadioId` / `stage2..4RadioId` and `dbcharacter::Deployable.dialog_script_id`),
but nothing calls it yet, so no NPC ever speaks. `PrivateDialog { Time, Entity, DialogId }` is the
per-player variant, `PublicDialog` a parameterless broadcast, `NotifyDialogScriptComplete` the
client's "line finished" command (the natural way to walk `next_id`), and `PerformDialog` /
`SetDialogTag` the client-side equivalents.

**Not implemented because the trigger rules are not in the database:** which of the 6
`dbdialogdata::BattleChatterDescriptions` rows applies to which event, and what `trigger` 1/2 mean.
The tables themselves are clear:

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
| Player emote menu (anonymous table, `name_id`, `keybinding`, `category_id`, `order`, `emote_id`) | 93 | The client's emote list; validation uses `EmoteRecord` (the superset) so scripted/administrative emotes keep working |
| `dbcharacter::PoseType` | 48 | Physics/collision per pose; `Monster.posetype_id` is replicated through the entity, no server rule |
| `dbcharacter::Head` (`animnet_id`) | 67 | Client-side |
| `dbitems::BattleframeVisuals` / `Battleframe` (`animnetwork_id`, `hand_animnetwork_id`, `anim_armed_id`, `posetype_id`) | 2,786 / 1,676 | Client-side (visuals are already replicated from the loadout) |
| `dbvisualrecords::VisualRecord` | 11,698 | Client-side |
| `dbcharacter::TinyObject` (`posefile_id`), `dbcharacter::Deployable` (`animnetwork`) | 379 / 3,902 | Client-side |
| `vcs::*` pose files (`driver_pose_file`, `gunner_pose_file`, ...) | 73/24/22/7 | Client-side (vehicles) |
| `dbitems::Weapons` (`first_person_animnet_id`, `third_person_animnet_id`) | 6,789 | Client-side |
| `dbcharacter::Stumble` (`anim_index`, `statusfx_id`, `duration`, `cooldown_ms`, `distance`, `only_once`, 39 rows), `dbcharacter::StumbleDirection` (`stumble_id`, `anim_substate`, 120 rows) and `dbcharacter::GibVisuals.death_anim_index` (128 rows) | 39/120/128 | Hit reactions and death animations: the server reports the damage response and the gib visuals, the client picks the animation. No row links a stumble to what causes it (see `Docs/NPC_AI.md` §6) |
| `aptfs::ForcePushCommandDef.do_animation`, `aptfs::SwitchWeaponCommandDef.play_animation` | 632 / 343 | Server commands with a client feedback flag; their defs are already loaded, the flags are not acted on |
| `aptfs::ApplyClientStatusEffectCommandDef` / `RemoveClientStatusEffectCommandDef` | 3 / 8 | Server → client "apply/remove this effect **on your side**" commands. `Commands/Effect/Todo/ApplyClientStatusEffectCommand.cs` exists but is not wired into `Factory`, which is the remaining path by which a server-side rule could start a client-only animation without a replicated effect |

## 7. Tests

`GameServer.Tests/EmoteServiceTests.cs`: an emote in the table is replicated with its id and time, an
emote outside it is ignored and leaves a running emote alone, id 0 stops the emote, the rows with a
`statuseffect` apply it once with the emote's timestamp, rows without one apply nothing, and the
emote still replicates when the shard has no ability system (or the effect is refused).
