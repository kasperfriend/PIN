# Mobs & NPCs — full spawn reference

Every one of the **3,109** rows of `dbcharacter::Monster` that PIN can spawn, with the exact command for each. 1,772 of them have an English name and can be spawned by name or id; the 1,337 unnamed rows are real and spawnable, but can only be referenced by id.

> **Generated file** - do not edit by hand. Regenerate with `python3 Tools/SdbDump/spawn_reference.py <clientdb.sd2>` (see [README.md](README.md#5-regenerating-this-folder)).

Decoded from Firefall build **prod-1962**. Index, faction table and CSV notes: [README.md](README.md). How the commands are implemented: [../STATIC_DATABASE.md](../STATIC_DATABASE.md#4-spawning-from-the-database-in-game).

---

## 1. Spawning one of these

```
\spawn monster <id|name> [<x> <y> <z>]  # chat command (note the backslash)
spawn monster <id|name> [<x> <y> <z>]   # Admin channel / server console
\sdb monster <filter> [limit]           # search this table in-game
\sdbinfo monster <id|name>              # every field of one row
```

- Kind aliases accepted in place of `monster`: `monster`, `monsters`, `npc`, `npcs`, `char`, `character`, `characters`, `mob`, `mobs`.
- Spawn path: `EntityManager.SpawnCharacter(typeId, position, orientation)`.
- Older typed command: `\npc <id> [<x> <y> <z>]` (chat + admin).
- Omit `<x> <y> <z>` and the entity spawns at your character's position with your orientation; from the server console a position is required.
- Names are matched case-insensitively (exact beats prefix beats substring) and do not need quoting, so multi-word names work: `\spawn monster Melded Wyrm`.

Examples, built from the first rows of this table:

```
\spawn monster 1                # Melded Wyrm - by id, at your feet
\spawn monster Melded Wyrm      # the same row, by name
\spawn monster 4 -25.5 118 492  # Yellow Shirt at an explicit position
\sdbinfo monster 1              # every field of that row
\sdb monster melded 20          # search this table in-game
```

## 2. Column reference

| column | meaning |
|---|---|
| `id` | `dbcharacter::Monster.id` - the character type id used by every spawn path. |
| `name` | `localized_name_id` resolved through `dblocalization::LocalizedText`; `-` means the row has no English text (placeholder, cut content or internal variant) and can only be spawned by id. |
| `faction` | `faction_id` resolved through `dbcharacter::Faction.internal_name`; drives hostility against the player (faction 1 `accord` is friendly, so those NPCs cannot be shot). |
| `race` | Census byte: 0 Human, 2 Chosen, 6 Misc (drones, targets, Necronus), 7 Companion/critter, 8 Melded, 9 Wildlife, 10 Outlaw, 11 Large wildlife. |
| `chassis` | `chassis_id` -> `dbitems::Battleframe` / `dbitems::RootItem`: body, visuals and the jetpack/energy parameters PIN replicates. The CSV adds `chassis_name`. |
| `weapons` | `weapon1_id` / `weapon2_id` -> `dbitems::Weapons`; `-` means the slot is empty. `weapon1_name` / `weapon2_name` are in the CSV. |
| `behavior` | CAIS behavior set name - the short name only, the full parameter list (`Arch_MedRangedHumanoid_Base(triggerPullTime=1500,...)`) is in the CSV's `behavior` column. Empty means the row has no behavior and the NPC just stands there. |
| `scaling` | `scaling_table_id` - `0` (nearly every row) means no scaling set. Per-level health/damage curves live in `dbcharacter::MonsterScaling`, listed in [README.md](README.md#4-monster-scaling-dbcharactermonsterscaling). |
| `loot tables` | `loot_table_id` / `loot_table2_id` -> `dbitems::LootTable` (names in the CSV). |
| `hp regen` | `health_regen`, out-of-combat regeneration. |
| `spawn command` | The exact chat command. Drop the leading `\` for the Admin channel or the server console, and append `<x> <y> <z>` for an explicit position. |

The table in §5 is the readable subset. **Every** column of the SDB row - all 68 of them, plus the resolved names - is in [csv/mobs.csv](csv/mobs.csv).

## 3. Notes

- Full `CharacterEntity.LoadMonster` path: chassis, warpaints, weapons, faction hostility, physics body and AI lifecycle.
- Movement/physics columns (`normal_speed`, `fast_speed`, `body_radius`, `body_mass`, `body_height`) are `-1` when the row inherits them from its chassis battleframe.
- Spawned mobs are kinematic physics bodies, so they are hittable; hits still need pose shape data from `Tools/CollisionGenerator` (see `Docs/SPAWNING_AND_COMBAT.md` §4).
- Default NPC health is 19192 and `HandleProjectileImpact` deals 1337 damage, so an unbuffed mob takes ~15 hits.

## 4. Breakdown

### By faction

Rows per `dbcharacter::Faction.internal_name` (`-` = no faction row).

| faction | rows | named |
|---|---|---|
| accord | 1,731 | 1,042 |
| gaea | 431 | 189 |
| chosen | 270 | 122 |
| bandit | 249 | 164 |
| melding | 122 | 45 |
| friendly | 114 | 83 |
| neutral | 42 | 26 |
| Rebels | 35 | 24 |
| Black Hills Bandits | 28 | 22 |
| Reapers | 23 | 16 |
| - | 17 | 9 |
| monster | 17 | 5 |
| Ophanim | 16 | 13 |
| Tanken | 8 | 8 |
| Corporation - Omnidyne-M | 2 | 2 |
| Blackhats | 1 | 0 |
| Civilian | 1 | 0 |
| Corporation - Astrek Association | 1 | 1 |
| Corporation - Kisuton | 1 | 1 |

### By race

Rows per census race byte.

| race | rows | named |
|---|---|---|
| Human | 1,748 | 1,051 |
| Wildlife | 434 | 191 |
| Outlaw | 365 | 253 |
| Chosen | 268 | 118 |
| Melded | 118 | 42 |
| Companion | 84 | 59 |
| Large wildlife | 71 | 49 |
| Misc | 21 | 9 |

## 5. All 3,109 rows

Sorted by id. `spawn command` is ready to copy into the chat window.

| id | name | faction | race | chassis | weapons | behavior | scaling | loot tables | hp regen | spawn command |
|---|---|---|---|---|---|---|---|---|---|---|
| 1 | Melded Wyrm | melding | Melded | 30145 | 85189 / 85190 | - | 0 | 5726 / 6650 | 5 | \spawn monster 1 |
| 2 | NO MONSTER | accord | Human | 0 | - | - | 0 | - | 0 | \spawn monster 2 |
| 3 | - | accord | Human | 30160 | - | - | 0 | - | 0 | \spawn monster 3 |
| 4 | Yellow Shirt | accord | Human | 75683 | 30025 / 20015 | PlayerPet | 0 | - | 5 | \spawn monster 4 |
| 5 | Red Shirt | chosen | Chosen | 120582 | 92799 / 33064 | EliteWanderer | 0 | - | 0 | \spawn monster 5 |
| 6 | Small Salamander | gaea | Wildlife | 30146 | 34477 / - | AggressiveWanderer | 0 | 2 / - | 0 | \spawn monster 6 |
| 7 | Small Salamander | gaea | Wildlife | 34333 | 34477 / - | AggressiveWanderer | 0 | 2 / - | 0 | \spawn monster 7 |
| 8 | Small Salamander | gaea | Wildlife | 34334 | 34477 / - | AggressiveWanderer | 0 | 2 / - | 0 | \spawn monster 8 |
| 9 | - | gaea | Wildlife | 34335 | 34476 / - | AggressiveWanderer | 0 | 3 / - | 0 | \spawn monster 9 |
| 10 | Chosen Target Dummy | chosen | Chosen | 32740 | 32739 / 33064 | - | 0 | 3 / 5701 | 0 | \spawn monster 10 |
| 11 | - | gaea | Wildlife | 34336 | 34476 / - | AggressiveWanderer | 0 | 3 / - | 0 | \spawn monster 11 |
| 12 | - | gaea | Wildlife | 34337 | 34476 / - | AggressiveWanderer | 0 | 3 / - | 0 | \spawn monster 12 |
| 13 | - | gaea | Wildlife | 34338 | 34478 / - | AggressiveWanderer | 0 | 4 / - | 0 | \spawn monster 13 |
| 14 | - | gaea | Wildlife | 34339 | 34478 / - | AggressiveWanderer | 0 | 4 / - | 0 | \spawn monster 14 |
| 19 | Melded Varant - OLD | melding | Melded | 34340 | 76426 / 76122 | GiantMeldingSalamander | 0 | 5312 / 5461 | 0 | \spawn monster 19 |
| 20 | - | accord | Human | 34430 | - | - | 0 | - | 0 | \spawn monster 20 |
| 21 | - | accord | Human | 0 | - | - | 0 | - | 0 | \spawn monster 21 |
| 22 | Necronus | chosen | Misc | 30436 | 30647 / - | - | 0 | - | 0 | \spawn monster 22 |
| 23 | - | accord | Human | 0 | - | - | 0 | - | 0 | \spawn monster 23 |
| 24 | - | accord | Human | 0 | - | - | 0 | - | 0 | \spawn monster 24 |
| 25 | - | accord | Human | 0 | - | - | 0 | - | 0 | \spawn monster 25 |
| 26 | - | chosen | Chosen | 30047 | 20013 / 20020 | Null | 0 | 51 / - | 0 | \spawn monster 26 |
| 27 | - | accord | Human | 30439 | - | AlertAndInteractive | 0 | - | 0 | \spawn monster 27 |
| 28 | - | accord | Human | 30440 | - | Null | 0 | - | 0 | \spawn monster 28 |
| 29 | - | accord | Human | 30440 | - | Null | 0 | - | 0 | \spawn monster 29 |
| 30 | - | accord | Human | 30439 | - | Null | 0 | - | 0 | \spawn monster 30 |
| 31 | - | accord | Human | 30447 | - | Null | 0 | - | 0 | \spawn monster 31 |
| 32 | - | accord | Human | 30448 | - | Null | 0 | - | 0 | \spawn monster 32 |
| 33 | - | accord | Human | 30448 | - | Null | 0 | - | 0 | \spawn monster 33 |
| 34 | - | accord | Human | 30449 | - | Null | 0 | - | 0 | \spawn monster 34 |
| 35 | - | accord | Human | 30450 | - | Null | 0 | - | 0 | \spawn monster 35 |
| 36 | - | accord | Human | 30450 | - | Null | 0 | - | 0 | \spawn monster 36 |
| 37 | - | accord | Human | 30450 | - | Null | 0 | - | 0 | \spawn monster 37 |
| 38 | - | accord | Human | 30449 | - | Null | 0 | - | 0 | \spawn monster 38 |
| 39 | - | accord | Human | 30449 | - | Null | 0 | - | 0 | \spawn monster 39 |
| 40 | - | accord | Human | 30451 | 20039 / - | EngineerTurret | 0 | - | 0 | \spawn monster 40 |
| 41 | - | accord | Human | 10002 | - | Null | 0 | - | 0 | \spawn monster 41 |
| 42 | - | chosen | Chosen | 30053 | - | Null | 0 | - | 0 | \spawn monster 42 |
| 43 | - | accord | Human | 10001 | - | Null | 0 | - | 0 | \spawn monster 43 |
| 44 | - | chosen | Chosen | 30053 | 20009 / - | Null | 0 | 51 / - | 0 | \spawn monster 44 |
| 47 | - | accord | Human | 10004 | - | Null | 0 | - | 0 | \spawn monster 47 |
| 48 | - | accord | Human | 30452 | 20034 / - | Null | 0 | - | 0 | \spawn monster 48 |
| 49 | - | chosen | Chosen | 30452 | 20034 / - | Null | 0 | 50 / - | 0 | \spawn monster 49 |
| 52 | - | gaea | Wildlife | 30432 | - | Null | 0 | - | 0 | \spawn monster 52 |
| 53 | - | gaea | Wildlife | 30031 | - | Null | 0 | - | 0 | \spawn monster 53 |
| 54 | - | gaea | Wildlife | 30033 | 20012 / 20012 | Null | 0 | - | 0 | \spawn monster 54 |
| 55 | - | accord | Human | 10002 | - | Null | 0 | - | 0 | \spawn monster 55 |
| 57 | - | gaea | Wildlife | 30030 | 20010 / - | Null | 0 | - | 0 | \spawn monster 57 |
| 58 | - | accord | Human | 30452 | - / 20006 | EngineerTurret | 0 | - | 0 | \spawn monster 58 |
| 59 | - | chosen | Chosen | 30452 | - / 20006 | EngineerTurret | 0 | - | 0 | \spawn monster 59 |
| 61 | - | friendly | Companion | 30433 | - | Null | 0 | - | 0 | \spawn monster 61 |
| 63 | - | chosen | Chosen | 30451 | 20039 / - | EngineerTurret | 0 | - | 0 | \spawn monster 63 |
| 65 | - | accord | Human | 30451 | 20038 / - | EngineerTurret | 0 | - | 0 | \spawn monster 65 |
| 66 | - | chosen | Chosen | 30451 | 20038 / - | EngineerTurret | 0 | - | 0 | \spawn monster 66 |
| 68 | Melding Tornado | melding | Melded | 0 | - | MeldingTornado | 0 | 5272 / - | 0 | \spawn monster 68 |
| 69 | - | gaea | Wildlife | 30453 | - | AggressiveWanderer | 0 | - | 0 | \spawn monster 69 |
| 70 | - | accord | Human | 10004 | 30334 / 20044 | AggressiveWanderer | 0 | - | 0 | \spawn monster 70 |
| 72 | - | chosen | Chosen | 30028 | - / 20006 | AggressiveWanderer | 0 | 50 / - | 0 | \spawn monster 72 |
| 73 | - | accord | Human | 30028 | - / 20006 | AggressiveWanderer | 0 | 50 / - | 0 | \spawn monster 73 |
| 74 | - | melding | Melded | 85259 | 76560 / - | StealthVorrax | 0 | - / 5460 | 0 | \spawn monster 74 |
| 75 | - | accord | Human | 10003 | - / 20033 | AggressiveWanderer | 0 | - | 0 | \spawn monster 75 |
| 76 | - | gaea | Wildlife | 30454 | 20010 / - | AggressiveWanderer | 0 | - | 0 | \spawn monster 76 |
| 77 | - | accord | Human | 30097 | - | AggressiveWanderer | 0 | - | 0 | \spawn monster 77 |
| 78 | - | accord | Human | 30054 | - | AggressiveWanderer | 0 | - | 0 | \spawn monster 78 |
| 79 | - | chosen | Chosen | 30097 | - | AggressiveWanderer | 0 | - | 0 | \spawn monster 79 |
| 80 | - | chosen | Chosen | 30054 | - | AggressiveWanderer | 0 | - | 0 | \spawn monster 80 |
| 81 | - | accord | Human | 30067 | - | AggressiveWanderer | 0 | - | 0 | \spawn monster 81 |
| 82 | - | chosen | Chosen | 30067 | - | AggressiveWanderer | 0 | - | 0 | \spawn monster 82 |
| 83 | - | accord | Human | 30137 | - / 30025 | AggressiveWanderer | 0 | 53 / - | 0 | \spawn monster 83 |
| 84 | - | chosen | Chosen | 30137 | - / 20013 | AggressiveWanderer | 0 | 53 / - | 0 | \spawn monster 84 |
| 85 | - | accord | Human | 30137 | 30070 / - | AggressiveWanderer | 0 | 53 / - | 0 | \spawn monster 85 |
| 86 | - | chosen | Chosen | 30137 | 30070 / - | AggressiveWanderer | 0 | 53 / - | 0 | \spawn monster 86 |
| 87 | - | accord | Human | 30072 | 30071 / - | AggressiveWanderer | 0 | 51 / - | 0 | \spawn monster 87 |
| 88 | - | chosen | Chosen | 30072 | 30071 / - | AggressiveWanderer | 0 | 51 / - | 0 | \spawn monster 88 |
| 89 | - | accord | Human | 30032 | 20031 / - | AggressiveWanderer | 0 | 51 / - | 0 | \spawn monster 89 |
| 90 | - | accord | Human | 30030 | 20046 / - | Null | 0 | 1 / - | 0 | \spawn monster 90 |
| 91 | - | accord | Human | 10001 | 20002 / - | Null | 0 | - | 0 | \spawn monster 91 |
| 92 | - | accord | Human | 10001 | - / 20006 | Null | 0 | - | 0 | \spawn monster 92 |
| 93 | - | accord | Human | 10001 | - | Null | 0 | - | 0 | \spawn monster 93 |
| 94 | - | accord | Human | 10001 | 20009 / - | Null | 0 | - | 0 | \spawn monster 94 |
| 95 | - | accord | Human | 10001 | 20024 / - | Null | 0 | - | 0 | \spawn monster 95 |
| 96 | - | accord | Human | 10001 | 20005 / - | Null | 0 | - | 0 | \spawn monster 96 |
| 97 | - | accord | Human | 10001 | - | Null | 0 | - | 0 | \spawn monster 97 |
| 98 | - | accord | Human | 10001 | 20033 / - | Null | 0 | - | 0 | \spawn monster 98 |
| 99 | - | accord | Human | 10001 | 20041 / - | Null | 0 | - | 0 | \spawn monster 99 |
| 100 | - | accord | Human | 30094 | - | AggressiveWanderer | 0 | - | 0 | \spawn monster 100 |
| 101 | - | accord | Human | 30095 | - | AggressiveWanderer | 0 | - | 0 | \spawn monster 101 |
| 102 | - | chosen | Chosen | 30094 | - | AggressiveWanderer | 0 | - | 0 | \spawn monster 102 |
| 103 | - | chosen | Chosen | 30095 | - | AggressiveWanderer | 0 | - | 0 | \spawn monster 103 |
| 104 | - | accord | Human | 30097 | 30058 / - | AggressiveWanderer | 0 | - | 0 | \spawn monster 104 |
| 105 | - | chosen | Chosen | 30097 | 30058 / - | AggressiveWanderer | 0 | - | 0 | \spawn monster 105 |
| 106 | - | accord | Human | 30036 | 20024 / - | AggressiveWanderer | 0 | - | 0 | \spawn monster 106 |
| 107 | - | chosen | Chosen | 30036 | 20024 / - | AggressiveWanderer | 0 | - | 0 | \spawn monster 107 |
| 108 | - | accord | Human | 30111 | 20038 / - | EngineerTurret | 0 | - | 0 | \spawn monster 108 |
| 109 | - | chosen | Chosen | 30111 | 30045 / - | EngineerTurret | 0 | - | 0 | \spawn monster 109 |
| 110 | - | accord | Human | 30434 | - | TraumaDoc | 0 | 8 / - | 0 | \spawn monster 110 |
| 111 | - | accord | Human | 30160 | - / 30025 | Null | 0 | 51 / - | 0 | \spawn monster 111 |
| 112 | - | accord | Human | 10004 | - / 76108 | Arch_MedRangedHumanoid_Base | 0 | - | 0 | \spawn monster 112 |
| 113 | - | chosen | Chosen | 30434 | - | TraumaDoc | 0 | 17 / - | 0 | \spawn monster 113 |
| 114 | - | accord | Human | 10001 | - / 30025 | TraumaDoc | 0 | 18 / - | 0 | \spawn monster 114 |
| 115 | - | accord | Human | 10001 | - | TraumaDoc | 0 | 10 / - | 0 | \spawn monster 115 |
| 116 | - | accord | Human | 10004 | - | TraumaDoc | 0 | 15 / - | 0 | \spawn monster 116 |
| 117 | - | accord | Human | 10004 | - | TraumaDoc | 0 | 16 / - | 0 | \spawn monster 117 |
| 118 | - | accord | Human | 10002 | - | TraumaDoc | 0 | 11 / - | 0 | \spawn monster 118 |
| 119 | - | accord | Human | 10002 | - | TraumaDoc | 0 | 12 / - | 0 | \spawn monster 119 |
| 120 | - | accord | Human | 10003 | - | TraumaDoc | 0 | 13 / - | 0 | \spawn monster 120 |
| 121 | - | accord | Human | 10003 | - | TraumaDoc | 0 | 14 / - | 0 | \spawn monster 121 |
| 122 | - | accord | Human | 30437 | - | Null | 0 | - | 0 | \spawn monster 122 |
| 123 | - | accord | Human | 10003 | - | AggressiveWanderer | 0 | 19 / - | 0 | \spawn monster 123 |
| 124 | - | accord | Human | 30160 | - / 30025 | Null | 0 | 51 / - | 0 | \spawn monster 124 |
| 125 | - | gaea | Wildlife | 30031 | 30291 / - | AggressiveWanderer | 0 | - | 0 | \spawn monster 125 |
| 126 | - | accord | Human | 10003 | - | AggressiveWanderer | 0 | 21 / - | 0 | \spawn monster 126 |
| 127 | - | accord | Human | 10001 | - / 30025 | TraumaDoc | 0 | 20 / - | 0 | \spawn monster 127 |
| 128 | - | accord | Human | 10001 | - / 30025 | TraumaDoc | 0 | 22 / - | 0 | \spawn monster 128 |
| 129 | - | accord | Human | 30049 | - | MRU | 0 | - | 0 | \spawn monster 129 |
| 130 | - | melding | Melded | 30296 | 30322 / - | - | 0 | - | 0 | \spawn monster 130 |
| 131 | - | melding | Melded | 30032 | 20031 / - | AggressiveWanderer | 0 | - | 0 | \spawn monster 131 |
| 132 | - | melding | Melded | 30245 | - | Null | 0 | 51 / - | 0 | \spawn monster 132 |
| 133 | - | melding | Melded | 30247 | 20023 / - | - | 0 | - | 0 | \spawn monster 133 |
| 134 | - | melding | Melded | 30031 | 20023 / - | AggressiveWanderer | 0 | 51 / - | 0 | \spawn monster 134 |
| 135 | - | melding | Melded | 30030 | 20046 / - | AggressiveWanderer | 0 | 50 / - | 0 | \spawn monster 135 |
| 136 | - | gaea | Wildlife | 30296 | 30258 / - | AggressiveWanderer | 0 | 1 / - | 0 | \spawn monster 136 |
| 137 | - | accord | Human | 30111 | - | Null | 0 | - | 0 | \spawn monster 137 |
| 138 | - | melding | Melded | 30266 | 20028 / 20028 | AggressiveWanderer | 0 | 51 / - | 0 | \spawn monster 138 |
| 139 | - | melding | Melded | 30031 | 30331 / - | AggressiveWanderer | 0 | - | 0 | \spawn monster 139 |
| 140 | - | accord | Human | 30268 | 30270 / - | Null | 0 | - | 0 | \spawn monster 140 |
| 141 | - | melding | Melded | 30032 | 20031 / - | AggressiveWanderer | 0 | 51 / - | 0 | \spawn monster 141 |
| 142 | - | accord | Human | 10004 | - | TraumaDoc | 0 | 29 / - | 0 | \spawn monster 142 |
| 143 | Battlebot | accord | Human | 30271 | 30280 / - | Null | 0 | - | 0 | \spawn monster 143 |
| 144 | - | melding | Melded | 30031 | 20023 / - | AggressiveWanderer | 0 | 51 / - | 0 | \spawn monster 144 |
| 145 | - | melding | Melded | 30284 | 30291 / - | AggressiveWanderer | 0 | 51 / - | 0 | \spawn monster 145 |
| 146 | - | accord | Human | 10003 | 20033 / - | Null | 0 | - | 0 | \spawn monster 146 |
| 147 | - | melding | Melded | 30097 | - | Null | 0 | 51 / - | 0 | \spawn monster 147 |
| 148 | - | accord | Human | 30292 | 20044 / - | Null | 0 | - | 0 | \spawn monster 148 |
| 149 | - | chosen | Chosen | 30292 | 20044 / - | Null | 0 | - | 0 | \spawn monster 149 |
| 150 | Puker | chosen | Chosen | 30295 | 85175 / - | MeldingPuker | 0 | 5726 / 5460 | 0 | \spawn monster 150 |
| 151 | - | gaea | Wildlife | 30247 | 20028 / - | AggressiveWanderer | 0 | 51 / - | 0 | \spawn monster 151 |
| 152 | - | accord | Human | 30271 | 30306 / - | Null | 0 | - | 0 | \spawn monster 152 |
| 153 | - | melding | Melded | 30314 | 20023 / - | Null | 0 | 50 / - | 0 | \spawn monster 153 |
| 154 | - | melding | Melded | 30111 | 30330 / - | EngineerTurret | 0 | 50 / - | 0 | \spawn monster 154 |
| 155 | - | melding | Melded | 30329 | 30322 / - | Null | 0 | 50 / - | 0 | \spawn monster 155 |
| 156 | - | melding | Melded | 30295 | 30291 / - | - | 0 | - | 0 | \spawn monster 156 |
| 157 | - | melding | Melded | 30314 | 20023 / - | Null | 0 | 50 / - | 0 | \spawn monster 157 |
| 158 | - | accord | Human | 30271 | - | TraumaDoc | 0 | 74 / - | 0 | \spawn monster 158 |
| 159 | - | melding | Melded | 30314 | 30322 / - | AggressiveWanderer | 0 | 50 / - | 0 | \spawn monster 159 |
| 160 | - | melding | Melded | 30381 | 20046 / - | AggressiveWanderer | 0 | 56 / - | 0 | \spawn monster 160 |
| 161 | - | melding | Melded | 30382 | 20046 / - | AggressiveWanderer | 0 | 50 / - | 0 | \spawn monster 161 |
| 162 | - | melding | Melded | 30383 | 20046 / - | AggressiveWanderer | 0 | 50 / - | 0 | \spawn monster 162 |
| 163 | - | melding | Melded | 30384 | 20046 / - | AggressiveWanderer | 0 | 50 / - | 0 | \spawn monster 163 |
| 164 | - | melding | Melded | 30030 | 30322 / - | Null | 0 | - | 0 | \spawn monster 164 |
| 165 | - | melding | Melded | 30031 | 20028 / - | AggressiveWanderer | 0 | 51 / - | 0 | \spawn monster 165 |
| 166 | - | melding | Melded | 30053 | 30642 / - | Null | 0 | 3 / - | 0 | \spawn monster 166 |
| 167 | - | melding | Melded | 30032 | 20031 / 20010 | AggressiveWanderer | 0 | - | 0 | \spawn monster 167 |
| 168 | - | melding | Melded | 30643 | 30642 / - | AggressiveWanderer | 0 | - | 0 | \spawn monster 168 |
| 169 | - | melding | Melded | 30643 | 30642 / - | AggressiveWanderer | 0 | - | 0 | \spawn monster 169 |
| 170 | - | accord | Human | 10001 | - | - | 0 | - | 0 | \spawn monster 170 |
| 171 | - | melding | Melded | 30643 | 30789 / 30789 | AggressiveWanderer | 0 | - | 0 | \spawn monster 171 |
| 172 | - | accord | Human | 30481 | 20034 / - | Null | 0 | - | 0 | \spawn monster 172 |
| 173 | - | accord | Human | 30677 | - / 30025 | Null | 0 | - | 0 | \spawn monster 173 |
| 174 | - | accord | Human | 30605 | - | - | 0 | - | 0 | \spawn monster 174 |
| 175 | - | melding | Melded | 30296 | - | AggressiveWanderer | 0 | 36 / - | 0 | \spawn monster 175 |
| 176 | - | melding | Melded | 30032 | 30290 / - | AggressiveWanderer | 0 | 51 / - | 0 | \spawn monster 176 |
| 177 | - | accord | Human | 30679 | 30688 / - | Null | 0 | - | 0 | \spawn monster 177 |
| 178 | - | gaea | Wildlife | 30639 | 20046 / - | AggressiveWanderer | 0 | 1 / - | 0 | \spawn monster 178 |
| 179 | - | gaea | Wildlife | 30641 | 30808 / - | SwarmWanderer | 0 | 3 / - | 0 | \spawn monster 179 |
| 180 | - | accord | Human | 30678 | - / 30025 | Null | 0 | - | 0 | \spawn monster 180 |
| 181 | - | melding | Melded | 30649 | 20010 / - | AggressiveWanderer | 0 | 50 / - | 0 | \spawn monster 181 |
| 182 | - | melding | Melded | 30650 | 20023 / - | AggressiveWanderer | 0 | 50 / - | 0 | \spawn monster 182 |
| 183 | - | melding | Melded | 30643 | 20018 / 30671 | AggressiveWanderer | 0 | - | 0 | \spawn monster 183 |
| 184 | - | accord | Human | 10001 | - / 30025 | AggressiveWanderer | 0 | 51 / - | 0 | \spawn monster 184 |
| 185 | - | accord | Human | 10004 | - / 76108 | Arch_MedRangedHumanoid_Base | 0 | - | 0 | \spawn monster 185 |
| 186 | - | accord | Human | 10001 | - / 30025 | Null | 0 | - | 0 | \spawn monster 186 |
| 187 | - | accord | Human | 10003 | - / 30025 | AggressiveWanderer | 0 | - | 0 | \spawn monster 187 |
| 188 | - | accord | Human | 10002 | 30629 / - | Null | 0 | - | 0 | \spawn monster 188 |
| 189 | - | accord | Human | 10004 | 20005 / - | Null | 0 | - | 0 | \spawn monster 189 |
| 190 | - | neutral | Large wildlife | 30673 | 55400 / - | PassiveAttacker | 0 | - | 0 | \spawn monster 190 |
| 191 | - | accord | Human | 10002 | 20041 / - | Null | 0 | - | 0 | \spawn monster 191 |
| 192 | - | accord | Human | 10002 | 20041 / - | Null | 0 | - | 0 | \spawn monster 192 |
| 193 | - | accord | Human | 30028 | - / 30025 | AggressiveWanderer | 0 | 51 / - | 0 | \spawn monster 193 |
| 194 | - | accord | Human | 10001 | - / 30025 | Null | 0 | - | 0 | \spawn monster 194 |
| 195 | - | melding | Melded | 30097 | - | Null | 0 | 51 / - | 0 | \spawn monster 195 |
| 196 | Miniature Brontodon | friendly | Companion | 30708 | - | - | 0 | - | 0 | \spawn monster 196 |
| 197 | - | neutral | Large wildlife | 30727 | 20028 / - | AggressiveWanderer | 0 | - | 0 | \spawn monster 197 |
| 198 | - | melding | Melded | 30296 | 30322 / - | AggressiveWanderer | 0 | 1 / - | 0 | \spawn monster 198 |
| 199 | - | accord | Human | 30743 | 20028 / 20028 | AggressiveWanderer | 0 | - | 0 | \spawn monster 199 |
| 200 | Accord SIN Amplifier | accord | Human | 30744 | - | AggressiveWanderer | 0 | - | 0 | \spawn monster 200 |
| 201 | - | accord | Human | 30752 | 30751 / - | Null | 0 | - | 0 | \spawn monster 201 |
| 202 | - | accord | Human | 30247 | 30670 / - | AggressiveWanderer | 0 | - | 0 | \spawn monster 202 |
| 203 | - | melding | Melded | 30641 | 20046 / - | AggressiveWanderer | 0 | 3 / - | 0 | \spawn monster 203 |
| 204 | - | melding | Melded | 30758 | 30642 / - | AggressiveWanderer | 0 | 51 / - | 0 | \spawn monster 204 |
| 205 | - | melding | Melded | 30643 | 30642 / - | AggressiveWanderer | 0 | 3 / - | 0 | \spawn monster 205 |
| 206 | - | melding | Melded | 30643 | 30671 / 30671 | AggressiveWanderer | 0 | 3 / - | 0 | \spawn monster 206 |
| 207 | Typhon | accord | Human | 106351 | 85735 / - | AlertAndInteractive | 0 | 17 / - | 0 | \spawn monster 207 |
| 208 | Mourningstar | accord | Human | 106358 | - | AlertAndInteractive | 0 | 17 / - | 0 | \spawn monster 208 |
| 209 | - | accord | Human | 30111 | 30330 / - | Null | 0 | 50 / - | 0 | \spawn monster 209 |
| 210 | - | accord | Human | 30764 | 30767 / - | EngineerTurret | 0 | - | 0 | \spawn monster 210 |
| 211 | - | gaea | Wildlife | 30030 | 20046 / - | AggressiveWanderer | 0 | 1 / - | 0 | \spawn monster 211 |
| 212 | Grunt Cannoneer | chosen | Chosen | 34056 | 54004 / - | ShotGruntWanderer | 0 | 3 / - | 0 | \spawn monster 212 |
| 213 | - | melding | Melded | 30779 | 30592 / - | AggressiveWanderer | 0 | 3 / - | 0 | \spawn monster 213 |
| 214 | - | gaea | Wildlife | 30781 | 20046 / - | AggressiveWanderer | 0 | 3 / - | 0 | \spawn monster 214 |
| 215 | - | melding | Melded | 30425 | 30642 / - | AggressiveWanderer | 0 | 3 / - | 0 | \spawn monster 215 |
| 216 | - | melding | Melded | 30788 | 30789 / - | AggressiveWanderer | 0 | 3 / - | 0 | \spawn monster 216 |
| 217 | - | gaea | Wildlife | 30781 | 20046 / - | AggressiveWanderer | 0 | 3 / - | 0 | \spawn monster 217 |
| 218 | - | gaea | Wildlife | 30804 | 20034 / - | Null | 0 | 50 / - | 0 | \spawn monster 218 |
| 219 | - | accord | Human | 30666 | - / 30025 | Null | 0 | - | 0 | \spawn monster 219 |
| 220 | - | melding | Melded | 30820 | 30777 / - | AggressiveWanderer | 0 | 3 / - | 0 | \spawn monster 220 |
| 221 | - | accord | Human | 30666 | - / 30025 | Null | 0 | - | 0 | \spawn monster 221 |
| 222 | - | accord | Human | 30605 | - | Null | 0 | 29 / - | 0 | \spawn monster 222 |
| 223 | - | accord | Human | 30036 | 20013 / - | AggressiveWanderer | 0 | 3 / - | 0 | \spawn monster 223 |
| 224 | - | monster | Misc | 30147 | - / 30025 | AggressiveWanderer | 0 | 51 / - | 0 | \spawn monster 224 |
| 225 | - | gaea | Wildlife | 30030 | 20046 / - | AggressiveWanderer | 0 | 91 / - | 0 | \spawn monster 225 |
| 226 | - | accord | Human | 30028 | 20026 / 20008 | Null | 0 | - | 0 | \spawn monster 226 |
| 227 | - | gaea | Wildlife | 30137 | 30777 / - | Null | 0 | 2 / - | 0 | \spawn monster 227 |
| 228 | - | gaea | Wildlife | 30434 | 30777 / - | Null | 0 | 3 / - | 0 | \spawn monster 228 |
| 229 | - | gaea | Wildlife | 30448 | 30777 / - | Null | 0 | 3 / - | 0 | \spawn monster 229 |
| 230 | - | gaea | Wildlife | 30440 | 30777 / - | Null | 0 | 2 / - | 0 | \spawn monster 230 |
| 231 | - | accord | Human | 30605 | - / 30025 | Null | 0 | - | 0 | \spawn monster 231 |
| 232 | - | friendly | Companion | 30137 | 30025 / - | Null | 0 | - | 0 | \spawn monster 232 |
| 233 | - | accord | Human | 10004 | - / 76108 | Arch_MedRangedHumanoid_Base | 0 | - | 0 | \spawn monster 233 |
| 234 | - | friendly | Companion | 30440 | 30025 / - | Null | 0 | - | 0 | \spawn monster 234 |
| 235 | - | accord | Human | 30448 | 30025 / - | Null | 0 | - | 0 | \spawn monster 235 |
| 236 | - | accord | Human | 10004 | - | TraumaDoc | 0 | - | 0 | \spawn monster 236 |
| 237 | - | gaea | Wildlife | 30901 | 30903 / 20023 | SwarmWanderer | 0 | 5704 / 5404 | 0 | \spawn monster 237 |
| 238 | - | gaea | Wildlife | 31216 | 30903 / - | SwarmWanderer | 0 | 3 / - | 0 | \spawn monster 238 |
| 239 | OBSOLETE Aranha Worker | gaea | Wildlife | 31218 | 20046 / - | - | 0 | 5703 / 5395 | 5 | \spawn monster 239 |
| 240 | - | gaea | Wildlife | 31248 | - | AggressiveWanderer | 0 | - | 0 | \spawn monster 240 |
| 241 | Culex | gaea | Wildlife | 31219 | 32747 / - | - | 0 | 5704 / 5414 | 5 | \spawn monster 241 |
| 242 | Accord Recon | accord | Human | 10004 | - / 76108 | Arch_MedRangedHumanoid_Base | 0 | - | 0 | \spawn monster 242 |
| 243 | OBSOLETE Aranha Sieger | gaea | Wildlife | 31221 | 33830 / 30808 | - | 0 | 5704 / 5396 | 5 | \spawn monster 243 |
| 244 | Terrorclaw | gaea | Wildlife | 31222 | 85191 / - | - | 0 | 5705 / 5424 | 5 | \spawn monster 244 |
| 245 | - | gaea | Wildlife | 31223 | 34077 / - | BetaAranha | 0 | 1 / - | 0 | \spawn monster 245 |
| 246 | - | gaea | Wildlife | 31224 | 30903 / - | SwarmWanderer | 0 | 3 / - | 0 | \spawn monster 246 |
| 247 | - | gaea | Wildlife | 31225 | 30903 / - | SwarmWanderer | 0 | 3 / - | 0 | \spawn monster 247 |
| 248 | - | gaea | Wildlife | 31226 | 20010 / - | AggressiveWanderer | 0 | - | 0 | \spawn monster 248 |
| 249 | - | gaea | Wildlife | 31227 | 20023 / 30670 | AggressiveWanderer | 0 | 51 / - | 0 | \spawn monster 249 |
| 250 | - | gaea | Wildlife | 31228 | 20028 / 20028 | AggressiveWanderer | 0 | 51 / - | 0 | \spawn monster 250 |
| 251 | - | melding | Melded | 31229 | - | Null | 0 | 51 / - | 0 | \spawn monster 251 |
| 252 | - | gaea | Wildlife | 31230 | 30903 / - | SwarmWanderer | 0 | 91 / - | 0 | \spawn monster 252 |
| 253 | - | gaea | Wildlife | 31231 | 20046 / - | SwarmWanderer | 0 | 50 / - | 0 | \spawn monster 253 |
| 254 | - | gaea | Wildlife | 31232 | 20028 / 20028 | AggressiveWanderer | 0 | 51 / - | 0 | \spawn monster 254 |
| 255 | - | gaea | Wildlife | 31233 | 30808 / - | SwarmWanderer | 0 | 50 / - | 0 | \spawn monster 255 |
| 256 | - | gaea | Wildlife | 31234 | 20028 / 20028 | AggressiveWanderer | 0 | 51 / - | 0 | \spawn monster 256 |
| 257 | - | gaea | Wildlife | 31235 | 20023 / 30670 | AggressiveWanderer | 0 | 51 / - | 0 | \spawn monster 257 |
| 258 | - | gaea | Wildlife | 31236 | 30903 / - | SwarmWanderer | 0 | 3 / - | 0 | \spawn monster 258 |
| 259 | - | gaea | Wildlife | 31237 | 20010 / - | AggressiveWanderer | 0 | 51 / - | 0 | \spawn monster 259 |
| 260 | - | melding | Melded | 31238 | 20031 / - | AggressiveWanderer | 0 | 51 / - | 0 | \spawn monster 260 |
| 261 | - | gaea | Wildlife | 31239 | 20023 / 30670 | AggressiveWanderer | 0 | 51 / - | 0 | \spawn monster 261 |
| 262 | - | melding | Melded | 31240 | 30319 / - | Null | 0 | 51 / - | 0 | \spawn monster 262 |
| 263 | - | gaea | Wildlife | 31241 | 20010 / - | AggressiveWanderer | 0 | 51 / - | 0 | \spawn monster 263 |
| 264 | - | gaea | Wildlife | 31242 | 30903 / - | SwarmWanderer | 0 | 3 / - | 0 | \spawn monster 264 |
| 265 | - | gaea | Wildlife | 31243 | 20023 / 30670 | AggressiveWanderer | 0 | 51 / - | 0 | \spawn monster 265 |
| 266 | - | gaea | Wildlife | 31244 | 20010 / - | AggressiveWanderer | 0 | 51 / - | 0 | \spawn monster 266 |
| 267 | - | gaea | Wildlife | 31245 | 30808 / - | SwarmWanderer | 0 | 50 / - | 0 | \spawn monster 267 |
| 268 | - | melding | Melded | 31246 | 20031 / - | AggressiveWanderer | 0 | 51 / - | 0 | \spawn monster 268 |
| 269 | - | gaea | Wildlife | 31247 | 30903 / - | SwarmWanderer | 0 | 1 / - | 0 | \spawn monster 269 |
| 270 | Melded Hisser | melding | Melded | 31249 | 30903 / - | - | 0 | 5704 / 5459 | 5 | \spawn monster 270 |
| 271 | - | gaea | Wildlife | 31250 | 20023 / 30670 | AggressiveWanderer | 0 | 51 / - | 0 | \spawn monster 271 |
| 272 | - | melding | Melded | 31251 | 30319 / - | Null | 0 | 51 / - | 0 | \spawn monster 272 |
| 273 | - | melding | Melded | 31252 | 20031 / - | AggressiveWanderer | 0 | 51 / - | 0 | \spawn monster 273 |
| 274 | - | melding | Melded | 31253 | - | Null | 0 | 51 / - | 0 | \spawn monster 274 |
| 275 | - | accord | Human | 31254 | 20046 / - | DoorUpInteract | 0 | - | 0 | \spawn monster 275 |
| 276 | - | gaea | Wildlife | 30147 | 30025 / 30025 | EliteWanderer | 0 | 3 / - | 0 | \spawn monster 276 |
| 277 | - | gaea | Wildlife | 30147 | 30025 / 30025 | GruntWanderer | 0 | 3 / - | 0 | \spawn monster 277 |
| 278 | - | melding | Melded | 30147 | 30592 / 30025 | MedicWanderer | 0 | 3 / - | 0 | \spawn monster 278 |
| 279 | - | accord | Human | 10004 | - / 76108 | Arch_MedRangedHumanoid_Base | 0 | - | 0 | \spawn monster 279 |
| 280 | - | accord | Human | 10004 | - / 76108 | Arch_MedRangedHumanoid_Base | 0 | - | 0 | \spawn monster 280 |
| 281 | Chosen Sniper | chosen | Chosen | 125087 | 77049 / 32739 | - | 0 | 5697 / 5368 | 5 | \spawn monster 281 |
| 282 | - | accord | Human | 10004 | - / 76108 | Arch_MedRangedHumanoid_Base | 0 | - | 0 | \spawn monster 282 |
| 283 | Accord Recon | accord | Human | 10004 | - / 76108 | Arch_MedRangedHumanoid_Base | 0 | - | 0 | \spawn monster 283 |
| 284 | - | accord | Human | 10003 | - / 76108 | Arch_MedRangedHumanoid_Base | 0 | - | 0 | \spawn monster 284 |
| 285 | CORAL - Accord Black M Engineer - Variation 1 | accord | Human | 10003 | - / 76108 | Arch_MedRangedHumanoid_Base | 0 | - | 0 | \spawn monster 285 |
| 286 | - | accord | Human | 10003 | - / 76108 | Arch_MedRangedHumanoid_Base | 0 | - | 0 | \spawn monster 286 |
| 287 | - | gaea | Wildlife | 31279 | 30903 / - | SwarmWanderer | 0 | 2 / - | 0 | \spawn monster 287 |
| 288 | Accord Engineer | accord | Human | 10003 | - / 76108 | Arch_MedRangedHumanoid_Base | 0 | - | 0 | \spawn monster 288 |
| 289 | - | chosen | Chosen | 32744 | 33912 / - | EliteWanderer | 0 | 3 / - | 0 | \spawn monster 289 |
| 290 | Accord Assault | accord | Human | 10001 | - / 76108 | Arch_MedRangedHumanoid_Base | 0 | - | 0 | \spawn monster 290 |
| 291 | CORAL - Accord Black M Assault - Variation 1 | accord | Human | 10001 | - / 76108 | Arch_MedRangedHumanoid_Base | 0 | - | 0 | \spawn monster 291 |
| 292 | CORAL - Accord Brazilian M Assault - Variation 1 | accord | Human | 10001 | - / 76108 | Arch_MedRangedHumanoid_Base | 0 | - | 0 | \spawn monster 292 |
| 293 | Accord Assault | accord | Human | 10001 | - / 76108 | Arch_MedRangedHumanoid_Base | 0 | - | 0 | \spawn monster 293 |
| 294 | CORAL - Accord Asian F Assault - Variation 1 | accord | Human | 10001 | - / 76108 | Arch_MedRangedHumanoid_Base | 0 | - | 0 | \spawn monster 294 |
| 295 | CORAL - Accord Black F Assault - Variation 1 | accord | Human | 10001 | - / 76108 | Arch_MedRangedHumanoid_Base | 0 | - | 0 | \spawn monster 295 |
| 296 | - | accord | Human | 117993 | - / 76108 | Arch_MedRangedHumanoid_Base | 0 | - | 0 | \spawn monster 296 |
| 297 | Accord Assault | accord | Human | 10001 | - / 76108 | Arch_MedRangedHumanoid_Base | 0 | - | 0 | \spawn monster 297 |
| 298 | - | accord | Human | 52453 | - | - | 0 | - | 0 | \spawn monster 298 |
| 299 | CORAL - Resident Black M - Variation 1 | accord | Human | 52453 | - | - | 0 | - | 0 | \spawn monster 299 |
| 300 | - | accord | Human | 52453 | - | - | 0 | - | 0 | \spawn monster 300 |
| 301 | - | accord | Human | 52453 | - | - | 0 | - | 0 | \spawn monster 301 |
| 302 | CORAL - Resident Asian F - Variation 1 | accord | Human | 52453 | - | - | 0 | - | 0 | \spawn monster 302 |
| 303 | CORAL - Resident Black F - Variation 1 | accord | Human | 52453 | - | - | 0 | - | 0 | \spawn monster 303 |
| 304 | CORAL - Resident Brazilian F - Variation 1 | accord | Human | 52453 | - | - | 0 | - | 0 | \spawn monster 304 |
| 305 | CORAL - Resident White F - Variation 1 | accord | Human | 52453 | - | - | 0 | - | 0 | \spawn monster 305 |
| 306 | - | accord | Human | 52453 | - | - | 0 | - | 0 | \spawn monster 306 |
| 307 | - | accord | Human | 52453 | - | - | 0 | - | 0 | \spawn monster 307 |
| 308 | - | accord | Human | 52454 | - | - | 0 | - | 0 | \spawn monster 308 |
| 309 | - | accord | Human | 52454 | - | - | 0 | - | 0 | \spawn monster 309 |
| 310 | - | accord | Human | 52454 | - | PlayerPet | 0 | - | 0 | \spawn monster 310 |
| 311 | - | accord | Human | 52454 | - | - | 0 | - | 0 | \spawn monster 311 |
| 312 | - | accord | Human | 52454 | - | - | 0 | - | 0 | \spawn monster 312 |
| 313 | - | accord | Human | 52454 | - | - | 0 | - | 0 | \spawn monster 313 |
| 314 | Melded Surger | melding | Melded | 34002 | 85261 / - | FireHisser | 0 | 5726 / 5459 | 0 | \spawn monster 314 |
| 315 | CORAL - Accord Asian F Engineer - Variation 1 | accord | Human | 10003 | - / 76108 | Arch_MedRangedHumanoid_Base | 0 | - | 0 | \spawn monster 315 |
| 316 | Accord Engineer | accord | Human | 117992 | - / 76108 | Arch_MedRangedHumanoid_Base | 0 | - | 0 | \spawn monster 316 |
| 317 | - | accord | Human | 10003 | - / 76108 | Arch_MedRangedHumanoid_Base | 0 | - | 0 | \spawn monster 317 |
| 318 | - | accord | Human | 52455 | - | - | 0 | - | 0 | \spawn monster 318 |
| 319 | Accord Engineer | accord | Human | 10003 | - / 76108 | Arch_MedRangedHumanoid_Base | 0 | - | 0 | \spawn monster 319 |
| 320 | CORAL - Resident Black M - Variation 3 | accord | Human | 52455 | - | - | 0 | - | 0 | \spawn monster 320 |
| 321 | - | accord | Human | 0 | - | - | 0 | - | 0 | \spawn monster 321 |
| 322 | - | accord | Human | 0 | - | - | 0 | - | 0 | \spawn monster 322 |
| 323 | - | accord | Human | 31429 | 30767 / - | EngineerTurret | 0 | - | 0 | \spawn monster 323 |
| 324 | - | accord | Human | 52455 | - | - | 0 | - | 0 | \spawn monster 324 |
| 325 | - | chosen | Chosen | 32744 | 34004 / 34004 | EliteWanderer | 0 | 3 / - | 5 | \spawn monster 325 |
| 326 | Chosen Shock Trooper | chosen | Chosen | 125069 | 32739 / - | - | 0 | 5698 / 5369 | 5 | \spawn monster 326 |
| 327 | Juggernaut | chosen | Chosen | 33958 | 32741 / - | Arch_MedRangedHumanoid_Base | 0 | 5699 / 5370 | 5 | \spawn monster 327 |
| 328 | - | chosen | Chosen | 32742 | 32743 / 33064 | GruntWanderer | 0 | 3 / - | 0 | \spawn monster 328 |
| 329 | - | chosen | Chosen | 32744 | 32743 / - | EliteWanderer | 0 | 3 / - | 0 | \spawn monster 329 |
| 330 | - | accord | Human | 0 | - | - | 0 | - | 0 | \spawn monster 330 |
| 331 | - | melding | Melded | 32752 | 32750 / 32750 | GruntWanderer | 0 | - | 0 | \spawn monster 331 |
| 332 | - | chosen | Chosen | 33911 | 32753 / - | EliteBaseSieger | 0 | 3 / - | 0 | \spawn monster 332 |
| 333 | - | chosen | Chosen | 32740 | 20018 / - | EliteWanderer | 0 | 3 / - | 0 | \spawn monster 333 |
| 334 | - | chosen | Chosen | 33232 | 32848 / - | EngineerTurret | 0 | - | 0 | \spawn monster 334 |
| 335 | - | friendly | Companion | 32767 | - | Elevator | 0 | - | 0 | \spawn monster 335 |
| 336 | - | accord | Human | 0 | - | - | 0 | - | 0 | \spawn monster 336 |
| 337 | - | accord | Human | 0 | - | - | 0 | - | 0 | \spawn monster 337 |
| 338 | - | chosen | Chosen | 32740 | 33064 / - | EliteWanderer | 0 | 3 / - | 0 | \spawn monster 338 |
| 339 | - | accord | Human | 33254 | 33255 / - | EngineerTurret | 0 | - | 0 | \spawn monster 339 |
| 340 | - | chosen | Chosen | 33270 | 32739 / 33064 | EliteWanderer | 0 | 3 / - | 0 | \spawn monster 340 |
| 341 | - | gaea | Wildlife | 33772 | 33783 / 20023 | SwarmQueen | 0 | 5 / 5408 | 0 | \spawn monster 341 |
| 342 | - | chosen | Chosen | 32744 | 33859 / - | EliteWanderer | 0 | 3 / - | 0 | \spawn monster 342 |
| 343 | - | accord | Human | 0 | - | - | 0 | - | 0 | \spawn monster 343 |
| 344 | - | accord | Human | 30605 | - / 30025 | CivilianDialog | 0 | - | 0 | \spawn monster 344 |
| 345 | Turret Bot | friendly | Companion | 33814 | 20038 / - | PassiveAttacker | 0 | - | 0 | \spawn monster 345 |
| 346 | - | accord | Human | 0 | - | - | 0 | - | 0 | \spawn monster 346 |
| 347 | Whiptail Thresher | gaea | Wildlife | 33823 | 33949 / - | ThresherTailWhipper | 0 | 5704 / 5432 | 5 | \spawn monster 347 |
| 348 | Massive Culex | gaea | Wildlife | 33834 | 97061 / - | - | 0 | 5705 / 5415 | 5 | \spawn monster 348 |
| 349 | - | accord | Human | 0 | - | - | 0 | - | 0 | \spawn monster 349 |
| 350 | - | melding | Melded | 33840 | 32743 / - | EliteWanderer | 0 | - | 0 | \spawn monster 350 |
| 351 | - | accord | Human | 33874 | 20034 / - | Null | 0 | - | 0 | \spawn monster 351 |
| 352 | - | chosen | Chosen | 33885 | 33979 / 33064 | EliteWanderer | 0 | 3 / - | 0 | \spawn monster 352 |
| 353 | - | gaea | Wildlife | 30673 | 20028 / 20028 | - | 0 | - | 0 | \spawn monster 353 |
| 354 | - | accord | Human | 0 | - | - | 0 | - | 0 | \spawn monster 354 |
| 355 | - | accord | Human | 0 | - | - | 0 | - | 0 | \spawn monster 355 |
| 356 | Aero | accord | Human | 77731 | - | AlertAndInteractive | 0 | - | 0 | \spawn monster 356 |
| 357 | - | accord | Human | 52453 | - | CivilianDialog | 0 | - | 0 | \spawn monster 357 |
| 358 | Oilspill | accord | Human | 77437 | - | AlertAndInteractive | 0 | - | 0 | \spawn monster 358 |
| 359 | - | chosen | Chosen | 32744 | 32743 / - | EliteWanderer | 0 | 3 / - | 0 | \spawn monster 359 |
| 360 | - | accord | Human | 10002 | 76108 / 76108 | Arch_MedRangedHumanoid_Base | 0 | - | 0 | \spawn monster 360 |
| 361 | - | accord | Human | 10002 | - / 76108 | Arch_MedRangedHumanoid_Base | 0 | - | 0 | \spawn monster 361 |
| 362 | - | accord | Human | 10002 | - / 76108 | Arch_MedRangedHumanoid_Base | 0 | - | 0 | \spawn monster 362 |
| 363 | - | accord | Human | 10002 | - / 76108 | Arch_MedRangedHumanoid_Base | 0 | - | 0 | \spawn monster 363 |
| 364 | CORAL - Accord Asian F Medic - Variation 1 | accord | Human | 10002 | - / 76108 | Arch_MedRangedHumanoid_Base | 0 | - | 0 | \spawn monster 364 |
| 365 | - | accord | Human | 10002 | - / 76108 | Arch_MedRangedHumanoid_Base | 0 | - | 0 | \spawn monster 365 |
| 366 | - | accord | Human | 10002 | - / 76108 | Arch_MedRangedHumanoid_Base | 0 | - | 0 | \spawn monster 366 |
| 367 | - | accord | Human | 10002 | - / 76108 | Arch_MedRangedHumanoid_Base | 0 | - | 0 | \spawn monster 367 |
| 368 | - | accord | Human | 52455 | - | - | 0 | - | 0 | \spawn monster 368 |
| 369 | CORAL - Resident Asian F - Variation 3 | accord | Human | 52455 | - | - | 0 | - | 0 | \spawn monster 369 |
| 370 | - | accord | Human | 52455 | - | - | 0 | - | 0 | \spawn monster 370 |
| 371 | CORAL - Resident Brazilian F - Variation 3 | accord | Human | 52455 | - | - | 0 | - | 0 | \spawn monster 371 |
| 372 | CORAL - Resident White F - Variation 3 | accord | Human | 52455 | - | - | 0 | - | 0 | \spawn monster 372 |
| 373 | - | accord | Human | 31331 | - / 76108 | Arch_MedRangedHumanoid_Base | 0 | - | 0 | \spawn monster 373 |
| 374 | - | accord | Human | 31331 | - / 76108 | Arch_MedRangedHumanoid_Base | 0 | - | 0 | \spawn monster 374 |
| 375 | - | gaea | Wildlife | 34075 | 34076 / - | AggressiveWanderer | 0 | 5705 / - | 0 | \spawn monster 375 |
| 376 | - | accord | Human | 31332 | - | - | 0 | - | 0 | \spawn monster 376 |
| 377 | - | accord | Human | 31331 | - | - | 0 | - | 0 | \spawn monster 377 |
| 378 | - | accord | Human | 31332 | - | - | 0 | - | 0 | \spawn monster 378 |
| 379 | - | accord | Human | 31332 | - | - | 0 | - | 0 | \spawn monster 379 |
| 380 | - | accord | Human | 31332 | - / 30025 | - | 0 | - | 0 | \spawn monster 380 |
| 381 | - | accord | Human | 31331 | - | - | 0 | - | 0 | \spawn monster 381 |
| 382 | - | accord | Human | 33936 | 20034 / - | Null | 0 | - | 0 | \spawn monster 382 |
| 383 | - | accord | Human | 0 | - | - | 0 | - | 0 | \spawn monster 383 |
| 384 | - | gaea | Wildlife | 31218 | 30270 / - | BetaAranha | 0 | 2 / - | 0 | \spawn monster 384 |
| 385 | - | accord | Human | 30666 | 30025 / - | Null | 0 | - | 0 | \spawn monster 385 |
| 386 | OBSOLETE Hisser | gaea | Wildlife | 33945 | 30903 / - | - | 0 | 5704 / 5405 | 5 | \spawn monster 386 |
| 387 | OBSOLETE Shell-less Hisser | gaea | Wildlife | 33946 | 30903 / - | - | 0 | 5703 / 5403 | 0 | \spawn monster 387 |
| 388 | - | gaea | Wildlife | 34023 | - | Wander | 0 | 1 / - | 0 | \spawn monster 388 |
| 389 | - | accord | Human | 33963 | 33961 / - | EngineerTurret | 0 | - | 0 | \spawn monster 389 |
| 390 | - | gaea | Wildlife | 33964 | 33949 / - | SandThresher | 0 | 3 / - | 0 | \spawn monster 390 |
| 391 | - | chosen | Chosen | 32740 | 32739 / - | ChosenTrooper | 0 | 5698 / 5369 | 0 | \spawn monster 391 |
| 392 | Juggernaut | chosen | Chosen | 33958 | 32741 / 20016 | ChosenJuggernaut | 0 | 5699 / 5370 | 0 | \spawn monster 392 |
| 393 | Engineer Turret Gunner | accord | Human | 33976 | 75622 / - | AggressiveWanderer | 0 | - | 0 | \spawn monster 393 |
| 394 | - | friendly | Companion | 33993 | 20038 / - | PassiveAttacker | 0 | - | 0 | \spawn monster 394 |
| 395 | - | chosen | Chosen | 33997 | 32739 / 33064 | EliteWanderer | 0 | 3 / - | 0 | \spawn monster 395 |
| 396 | - | accord | Human | 10001 | - / 30025 | - | 0 | - | 0 | \spawn monster 396 |
| 397 | Battleframe OS | accord | Human | 10001 | - / 30025 | - | 0 | - | 0 | \spawn monster 397 |
| 398 | - | accord | Human | 10001 | - / 30025 | - | 0 | - | 0 | \spawn monster 398 |
| 399 | - | friendly | Companion | 33936 | 20034 / - | Null | 0 | - | 0 | \spawn monster 399 |
| 400 | - | accord | Human | 0 | - | - | 0 | - | 0 | \spawn monster 400 |
| 401 | - | friendly | Companion | 34016 | 30903 / - | SwarmWanderer | 0 | - | 0 | \spawn monster 401 |
| 402 | - | accord | Human | 0 | - | - | 0 | - | 0 | \spawn monster 402 |
| 403 | - | accord | Human | 34025 | - | PassivePet | 0 | - | 0 | \spawn monster 403 |
| 404 | - | accord | Human | 10001 | 30290 / 20012 | CivilianDialog | 0 | 10 / - | 0 | \spawn monster 404 |
| 405 | - | accord | Human | 0 | - | AggressiveWanderer | 0 | - | 0 | \spawn monster 405 |
| 406 | - | chosen | Chosen | 32740 | 32739 / 33064 | CraterTest | 0 | 3 / - | 0 | \spawn monster 406 |
| 407 | - | melding | Melded | 30145 | 30903 / - | AggressiveWanderer | 0 | 2 / - | 0 | \spawn monster 407 |
| 408 | - | melding | Melded | 31218 | 30903 / - | AggressiveWanderer | 0 | 2 / - | 0 | \spawn monster 408 |
| 409 | OBSOLETE Grunt Bruiser | chosen | Chosen | 32742 | 34143 / - | Arch_AdditiveMelee_Base | 0 | 5698 / 5369 | 5 | \spawn monster 409 |
| 410 | - | gaea | Wildlife | 31223 | - | AvoidMatt | 0 | 5702 / 5394 | 0 | \spawn monster 410 |
| 411 | - | gaea | Wildlife | 34079 | 34081 / 34083 | AlphaAranha | 0 | 5703 / - | 0 | \spawn monster 411 |
| 412 | - | gaea | Wildlife | 34080 | 34082 / 34084 | AlphaAranha | 0 | 4 / - | 0 | \spawn monster 412 |
| 413 | - | gaea | Wildlife | 34154 | 34087 / - | SwarmWanderer | 0 | 2 / 5404 | 0 | \spawn monster 413 |
| 414 | - | gaea | Wildlife | 34086 | - | Wander | 0 | 5702 / - | 0 | \spawn monster 414 |
| 415 | - | gaea | Wildlife | 33964 | 34090 / - | SandThresher | 0 | 5703 / - | 0 | \spawn monster 415 |
| 416 | - | gaea | Wildlife | 33820 | 34091 / - | SandThresher | 0 | 4 / - | 0 | \spawn monster 416 |
| 417 | - | gaea | Wildlife | 34092 | 34088 / - | Mosquito | 0 | 5703 / 5413 | 0 | \spawn monster 417 |
| 418 | - | gaea | Wildlife | 31219 | 32747 / - | Mosquito | 0 | 5 / - | 0 | \spawn monster 418 |
| 419 | - | gaea | Wildlife | 34129 | 34130 / - | SwarmWanderer | 0 | 4 / 5405 | 0 | \spawn monster 419 |
| 420 | - | accord | Human | 52453 | - | CivilianDialog | 0 | - | 0 | \spawn monster 420 |
| 421 | - | accord | Human | 52453 | - | CivilianDialog | 0 | - | 0 | \spawn monster 421 |
| 422 | - | accord | Human | 52453 | - | CivilianDialog | 0 | - | 0 | \spawn monster 422 |
| 423 | - | accord | Human | 52453 | - | CivilianDialog | 0 | - | 0 | \spawn monster 423 |
| 424 | - | accord | Human | 52453 | - | CivilianDialog | 0 | - | 0 | \spawn monster 424 |
| 425 | - | accord | Human | 52453 | - | CivilianDialog | 0 | - | 0 | \spawn monster 425 |
| 426 | - | accord | Human | 52453 | - | CivilianDialog | 0 | - | 0 | \spawn monster 426 |
| 427 | - | accord | Human | 52453 | - | CivilianDialog | 0 | - | 0 | \spawn monster 427 |
| 428 | - | accord | Human | 52453 | - | CivilianDialog | 0 | - | 0 | \spawn monster 428 |
| 429 | - | gaea | Wildlife | 34187 | 34077 / - | SuicideWanderer | 0 | 2 / - | 0 | \spawn monster 429 |
| 430 | OBSOLETE Explosive Aranha | gaea | Wildlife | 34185 | 20046 / - | - | 0 | 5703 / 5631 | 5 | \spawn monster 430 |
| 431 | - | gaea | Wildlife | 34186 | 34076 / - | SuicideWanderer | 0 | 4 / - | 0 | \spawn monster 431 |
| 432 | - | gaea | Wildlife | 34189 | 34077 / - | BetaAranha | 0 | 2 / - | 0 | \spawn monster 432 |
| 433 | OBSOLETE Icy Aranha | gaea | Wildlife | 34190 | 20046 / - | - | 0 | 5703 / 5632 | 5 | \spawn monster 433 |
| 434 | - | gaea | Wildlife | 34191 | 34076 / - | AggressiveWanderer | 0 | 4 / - | 0 | \spawn monster 434 |
| 435 | - | gaea | Wildlife | 34208 | 34077 / - | BetaAranha | 0 | 2 / - | 0 | \spawn monster 435 |
| 436 | OBSOLETE Geyser Aranha | gaea | Wildlife | 34209 | 20046 / - | - | 0 | 5703 / 5395 | 5 | \spawn monster 436 |
| 437 | - | gaea | Wildlife | 34210 | 34076 / - | BetaAranha | 0 | 4 / - | 0 | \spawn monster 437 |
| 438 | - | melding | Melded | 34217 | 34077 / - | BetaAranha | 0 | 5703 / 5457 | 0 | \spawn monster 438 |
| 439 | - | melding | Melded | 34218 | 20046 / - | BetaAranha | 0 | 5704 / 5458 | 0 | \spawn monster 439 |
| 440 | - | melding | Melded | 34219 | 34076 / - | BetaAranha | 0 | 5705 / 5459 | 0 | \spawn monster 440 |
| 441 | - | gaea | Wildlife | 34220 | 34077 / - | BetaAranha | 0 | 2 / 5394 | 0 | \spawn monster 441 |
| 442 | - | gaea | Wildlife | 34221 | 20046 / - | BetaAranha | 0 | 3 / 5396 | 0 | \spawn monster 442 |
| 443 | - | gaea | Wildlife | 34222 | 34076 / - | BetaAranha | 0 | 4 / 5396 | 0 | \spawn monster 443 |
| 444 | - | monster | Misc | 34480 | 30647 / - | EliteBaseAttacker | 0 | 5310 / - | 0 | \spawn monster 444 |
| 445 | - | chosen | Chosen | 33958 | 53339 / - | EliteStationary | 0 | 4 / - | 0 | \spawn monster 445 |
| 446 | - | gaea | Wildlife | 31218 | 20046 / - | SuicideWanderer | 0 | 2 / - | 0 | \spawn monster 446 |
| 447 | - | accord | Human | 40457 | - | - | 0 | - | 0 | \spawn monster 447 |
| 448 | OBSOLETE Aranha Stormer | gaea | Wildlife | 81281 | 40472 / - | - | 0 | 5703 / 5395 | 5 | \spawn monster 448 |
| 449 | OBSOLETE Toxic Aranha | gaea | Wildlife | 53616 | 20046 / - | - | 0 | 5703 / 5633 | 5 | \spawn monster 449 |
| 450 | - | accord | Human | 42185 | - | SeekAndPlay | 0 | - | 0 | \spawn monster 450 |
| 451 | - | accord | Human | 42621 | - | - | 0 | - | 0 | \spawn monster 451 |
| 452 | - | gaea | Wildlife | 49117 | 85221 / 49118 | Scarab | 0 | 5705 / 5777 | 0 | \spawn monster 452 |
| 453 | Grunt Raider | chosen | Chosen | 49119 | 34143 / - | MeleeGruntWanderer | 0 | 5697 / 5368 | 0 | \spawn monster 453 |
| 454 | - | accord | Human | 30481 | - | PassivePet | 0 | - | 0 | \spawn monster 454 |
| 455 | - | chosen | Chosen | 32740 | 52421 / 33064 | EliteWanderer | 0 | 3 / - | 0 | \spawn monster 455 |
| 456 | - | accord | Human | 52456 | - | - | 0 | - | 0 | \spawn monster 456 |
| 457 | - | accord | Human | 52456 | - | - | 0 | - | 0 | \spawn monster 457 |
| 458 | - | accord | Human | 52456 | - | - | 0 | - | 0 | \spawn monster 458 |
| 459 | - | accord | Human | 52456 | - | PeacetimeCityWanderer | 0 | - | 0 | \spawn monster 459 |
| 460 | - | accord | Human | 52456 | - | - | 0 | - | 0 | \spawn monster 460 |
| 461 | - | accord | Human | 52456 | - | - | 0 | - | 0 | \spawn monster 461 |
| 462 | - | accord | Human | 52456 | - | - | 0 | - | 0 | \spawn monster 462 |
| 463 | - | accord | Human | 52456 | - | - | 0 | - | 0 | \spawn monster 463 |
| 464 | - | accord | Human | 52457 | - | - | 0 | - | 0 | \spawn monster 464 |
| 465 | - | accord | Human | 52457 | - | - | 0 | - | 0 | \spawn monster 465 |
| 466 | - | accord | Human | 52457 | - | - | 0 | - | 0 | \spawn monster 466 |
| 467 | - | accord | Human | 52457 | - | - | 0 | - | 0 | \spawn monster 467 |
| 468 | - | accord | Human | 52457 | - | - | 0 | - | 0 | \spawn monster 468 |
| 469 | - | accord | Human | 52457 | - | - | 0 | - | 0 | \spawn monster 469 |
| 470 | - | accord | Human | 52457 | - | - | 0 | - | 0 | \spawn monster 470 |
| 471 | - | accord | Human | 52457 | - | - | 0 | - | 0 | \spawn monster 471 |
| 472 | - | accord | Human | 52458 | - | - | 0 | - | 0 | \spawn monster 472 |
| 473 | - | accord | Human | 52458 | - | - | 0 | - | 0 | \spawn monster 473 |
| 474 | - | accord | Human | 52458 | - | - | 0 | - | 0 | \spawn monster 474 |
| 475 | - | accord | Human | 52458 | - | - | 0 | - | 0 | \spawn monster 475 |
| 476 | - | accord | Human | 52458 | - | - | 0 | - | 0 | \spawn monster 476 |
| 477 | - | accord | Human | 52458 | - | - | 0 | - | 0 | \spawn monster 477 |
| 478 | - | accord | Human | 52458 | - | - | 0 | - | 0 | \spawn monster 478 |
| 479 | - | accord | Human | 52458 | - | - | 0 | - | 0 | \spawn monster 479 |
| 480 | - | gaea | Wildlife | 52499 | 34476 / - | AggressiveWanderer | 0 | 3 / - | 0 | \spawn monster 480 |
| 481 | - | accord | Human | 53337 | - | - | 0 | - | 0 | \spawn monster 481 |
| 482 | - | chosen | Chosen | 53340 | 32739 / - | HeavyWanderer | 0 | 4 / - | 0 | \spawn monster 482 |
| 483 | - | gaea | Wildlife | 53612 | 53606 / - | SwarmWanderer | 0 | 3 / 5405 | 0 | \spawn monster 483 |
| 484 | - | chosen | Chosen | 53610 | 53609 / - | Mosquito | 0 | - | 0 | \spawn monster 484 |
| 485 | - | chosen | Chosen | 32744 | 53623 / - | EliteWanderer | 0 | 3 / - | 0 | \spawn monster 485 |
| 486 | - | gaea | Wildlife | 31218 | 53623 / - | BetaAranha | 0 | 2 / - | 0 | \spawn monster 486 |
| 487 | Trapjaw | melding | Melded | 54006 | 85474 / - | Arch_FullbodyMelee_Base | 0 | 5312 / 5460 | 5 | \spawn monster 487 |
| 488 | - | accord | Human | 54018 | 55476 / - | Null | 0 | - | 0 | \spawn monster 488 |
| 489 | - | accord | Human | 54021 | 20034 / - | Null | 0 | - | 0 | \spawn monster 489 |
| 490 | - | friendly | Companion | 54024 | - | Mosquito | 0 | - | 0 | \spawn monster 490 |
| 491 | - | accord | Human | 54032 | 55476 / - | Null | 0 | - | 0 | \spawn monster 491 |
| 492 | - | accord | Human | 55780 | 55476 / - | Null | 0 | - | 0 | \spawn monster 492 |
| 493 | - | gaea | Wildlife | 65344 | 34077 / - | BetaAranha | 0 | - | 0 | \spawn monster 493 |
| 494 | - | accord | Human | 56723 | - / 76108 | Arch_MedRangedHumanoid_Base | 0 | - | 0 | \spawn monster 494 |
| 495 | - | accord | Human | 56723 | - / 76108 | Arch_MedRangedHumanoid_Base | 0 | - | 0 | \spawn monster 495 |
| 496 | - | accord | Human | 56723 | - / 76108 | Arch_MedRangedHumanoid_Base | 0 | - | 0 | \spawn monster 496 |
| 497 | Accord Dreadnaught | accord | Human | 56723 | - / 76108 | Arch_MedRangedHumanoid_Base | 0 | - | 0 | \spawn monster 497 |
| 498 | - | accord | Human | 56723 | - / 76108 | Arch_MedRangedHumanoid_Base | 0 | - | 0 | \spawn monster 498 |
| 499 | - | accord | Human | 56723 | - / 76108 | Arch_MedRangedHumanoid_Base | 0 | - | 0 | \spawn monster 499 |
| 500 | - | accord | Human | 56723 | - / 76108 | Arch_MedRangedHumanoid_Base | 0 | - | 0 | \spawn monster 500 |
| 501 | - | accord | Human | 56723 | - / 76108 | Arch_MedRangedHumanoid_Base | 0 | - | 0 | \spawn monster 501 |
| 502 | Ratchet | accord | Human | 81410 | - | AlertAndInteractive | 0 | - | 0 | \spawn monster 502 |
| 503 | Accord Soldier | accord | Human | 10001 | - / 30025 | - | 0 | - | 0 | \spawn monster 503 |
| 504 | - | accord | Human | 66844 | - | - | 0 | - | 0 | \spawn monster 504 |
| 505 | Wargrim | gaea | Wildlife | 66845 | 66846 / - | - | 0 | 5704 / 6212 | 5 | \spawn monster 505 |
| 506 | Spitting Thresher | gaea | Wildlife | 66852 | 66851 / 67426 | ThresherSpitter | 0 | 5704 / 5432 | 5 | \spawn monster 506 |
| 507 | Reckless Thresher | gaea | Wildlife | 66853 | 33949 / - | Arch_Charger_Base | 0 | 5704 / 5432 | 5 | \spawn monster 507 |
| 508 | - | accord | Human | 52453 | - | CivilianDialog | 0 | - | 0 | \spawn monster 508 |
| 509 | - | accord | Human | 0 | - | - | 0 | - | 0 | \spawn monster 509 |
| 510 | Lt. Shanafelt | accord | Human | 10001 | - | StationaryCivilianDialog | 0 | - | 0 | \spawn monster 510 |
| 511 | Sergeant Choi | accord | Human | 75105 | 114314 / - | - | 0 | - | 0 | \spawn monster 511 |
| 512 | Bandit Pointman | bandit | Outlaw | 75281 | - / 67423 | - | 0 | 5311 / 5386 | 5 | \spawn monster 512 |
| 513 | Bandit Grenadier | bandit | Outlaw | 75281 | 67424 / - | - | 0 | 5311 / 5387 | 5 | \spawn monster 513 |
| 514 | Bandit Gunman | bandit | Outlaw | 75281 | 67425 / - | - | 0 | 5311 / 5386 | 5 | \spawn monster 514 |
| 515 | Ikinya | accord | Human | 96566 | - | AlertAndInteractive | 0 | - | 0 | \spawn monster 515 |
| 516 | Consul Nostromo | accord | Human | 10001 | - / 30025 | - | 0 | - | 0 | \spawn monster 516 |
| 517 | - | accord | Human | 75259 | - | - | 0 | - | 0 | \spawn monster 517 |
| 518 | - | accord | Human | 75259 | - | - | 0 | - | 0 | \spawn monster 518 |
| 519 | - | accord | Human | 75259 | - | - | 0 | - | 0 | \spawn monster 519 |
| 520 | - | accord | Human | 75256 | - | - | 0 | 22 / - | 0 | \spawn monster 520 |
| 521 | - | accord | Human | 75259 | - | - | 0 | - | 0 | \spawn monster 521 |
| 522 | - | accord | Human | 75259 | - | - | 0 | - | 0 | \spawn monster 522 |
| 523 | - | accord | Human | 75259 | - | - | 0 | - | 0 | \spawn monster 523 |
| 524 | - | accord | Human | 75259 | - | - | 0 | - | 0 | \spawn monster 524 |
| 525 | - | accord | Human | 75259 | - | - | 0 | - | 0 | \spawn monster 525 |
| 526 | - | accord | Human | 31331 | - / 75108 | Arch_MedRangedHumanoid_Base | 0 | - | 0 | \spawn monster 526 |
| 527 | - | melding | Melded | 31249 | 30903 / - | SwarmWanderer | 0 | 5724 / 5458 | 0 | \spawn monster 527 |
| 528 | Melded Aranha | melding | Melded | 85124 | 20046 / - | - | 0 | 5703 / 5458 | 5 | \spawn monster 528 |
| 529 | Bandit Assault | bandit | Outlaw | 67422 | 30688 / - | - | 0 | 5311 / 5387 | 0 | \spawn monster 529 |
| 530 | - | bandit | Outlaw | 75115 | 75116 / - | EliteWanderer | 0 | - | 0 | \spawn monster 530 |
| 531 | Tanken Buster Corpse | bandit | Outlaw | 75424 | - | EliteWanderer | 0 | 5311 / 5386 | 0 | \spawn monster 531 |
| 532 | - | bandit | Outlaw | 75115 | 75116 / - | SiegebreakerWithCharge | 0 | 5311 / 5388 | 0 | \spawn monster 532 |
| 533 | Engineer | chosen | Chosen | 31267 | 66311 / 33064 | EliteWanderer | 0 | 3 / - | 0 | \spawn monster 533 |
| 534 | - | chosen | Chosen | 32742 | 34143 / - | MeleeGruntWanderer | 0 | 5697 / - | 0 | \spawn monster 534 |
| 535 | - | gaea | Wildlife | 75449 | 33996 / 20023 | Beetle | 0 | 4 / - | 0 | \spawn monster 535 |
| 536 | - | accord | Human | 75114 | 34143 / - | MeleeGruntWanderer | 0 | 2 / - | 0 | \spawn monster 536 |
| 537 | - | accord | Human | 30028 | - | CivilianDialog | 0 | - | 0 | \spawn monster 537 |
| 538 | Melded Spawnling | melding | Melded | 75551 | 85187 / - | StockMelee | 0 | 5724 / 6651 | 0 | \spawn monster 538 |
| 539 | - | accord | Human | 33976 | - / 75622 | AggressiveWanderer | 0 | - | 0 | \spawn monster 539 |
| 540 | - | accord | Human | 75626 | - | Null | 0 | - | 0 | \spawn monster 540 |
| 541 | - | chosen | Chosen | 75629 | 32747 / - | AggressiveWanderer | 0 | 5311 / - | 0 | \spawn monster 541 |
| 542 | - | melding | Melded | 75651 | - | PassiveWanderer | 0 | - | 0 | \spawn monster 542 |
| 543 | Chosen Drone | chosen | Chosen | 75661 | 75681 / - | ChosenEngineerDrone | 0 | 5697 / 5615 | 5 | \spawn monster 543 |
| 544 | - | accord | Human | 75665 | 75666 / 75667 | - | 0 | 54 / - | 0 | \spawn monster 544 |
| 545 | Grunt Roaster | chosen | Chosen | 34056 | 75680 / - | ShotGruntWanderer | 0 | 3 / - | 0 | \spawn monster 545 |
| 546 | - | accord | Human | 52453 | - | CivilianDialog | 0 | - | 0 | \spawn monster 546 |
| 547 | - | accord | Human | 31331 | - / 75108 | EliteWanderer | 0 | - | 0 | \spawn monster 547 |
| 548 | OBSOLETE Siegebreaker | chosen | Chosen | 75767 | 75769 / - | Arch_MoveThenFire_Base | 0 | 5699 / 5371 | 5 | \spawn monster 548 |
| 549 | - | gaea | Wildlife | 30146 | 34477 / - | AggressiveWanderer | 0 | 2 / - | 0 | \spawn monster 549 |
| 550 | - | gaea | Wildlife | 34333 | 34477 / - | AggressiveWanderer | 0 | 2 / - | 0 | \spawn monster 550 |
| 551 | - | accord | Human | 10002 | - | PeacetimeCityWanderer | 0 | - | 0 | \spawn monster 551 |
| 552 | Melding Tornado Funnel | melding | Melded | 30453 | - | MeldingTornadoMoveStage2 | 0 | - | 0 | \spawn monster 552 |
| 553 | Melding Shard | melding | Melded | 76119 | 20038 / - | Null | 0 | - | 0 | \spawn monster 553 |
| 554 | Melded Culex | melding | Melded | 77253 | 88172 / - | - | 0 | 5704 / 5459 | 5 | \spawn monster 554 |
| 555 | - | melding | Melded | 76115 | 76425 / - | SmallMeldingSalamander | 0 | 5267 / 5459 | 0 | \spawn monster 555 |
| 556 | Accord Command | accord | Human | 10001 | - / 30025 | - | 0 | - | 0 | \spawn monster 556 |
| 557 | CDR. Price | accord | Human | 76220 | - / 67423 | ProtectVehicle | 0 | - | 0 | \spawn monster 557 |
| 558 | - | friendly | Companion | 33993 | 20038 / - | PassiveAttacker | 0 | - | 0 | \spawn monster 558 |
| 559 | Melding Shard 2 | melding | Melded | 76221 | 20038 / - | Null | 0 | - | 0 | \spawn monster 559 |
| 560 | Melding Shard 3 | melding | Melded | 76222 | 20038 / - | Null | 0 | - | 0 | \spawn monster 560 |
| 561 | Melding Shard 4 | melding | Melded | 76223 | 20038 / - | Null | 0 | - | 0 | \spawn monster 561 |
| 562 | Explosive Melded Aranha | melding | Melded | 76523 | 20046 / - | - | 0 | 5725 / 5458 | 5 | \spawn monster 562 |
| 563 | - | accord | Human | 10001 | - / 30025 | - | 0 | - | 0 | \spawn monster 563 |
| 564 | - | chosen | Chosen | 32740 | 76560 / - | AggressiveWanderer | 0 | - | 0 | \spawn monster 564 |
| 565 | Melding Shard | melding | Melded | 76709 | 20038 / - | Null | 0 | - | 0 | \spawn monster 565 |
| 566 | Accord Soldier | accord | Human | 10001 | - | - | 0 | - | 0 | \spawn monster 566 |
| 567 | Merch | friendly | Companion | 76964 | - | PassivePet | 0 | - | 0 | \spawn monster 567 |
| 568 | Flesh Reaper | friendly | Companion | 76965 | - | PassivePet | 0 | - | 0 | \spawn monster 568 |
| 569 | Boon Boon | friendly | Companion | 76977 | - | - | 0 | - | 0 | \spawn monster 569 |
| 570 | Luau Larry | accord | Human | 76996 | - | AlertAndInteractive | 0 | - | 0 | \spawn monster 570 |
| 571 | Chosen Assault | chosen | Chosen | 75683 | 75684 / 75685 | EliteWanderer | 0 | - | 0 | \spawn monster 571 |
| 572 | Engineer | chosen | Chosen | 77732 | 77760 / - | Arch_MedRangedHumanoid_Base | 0 | 3 / 5813 | 5 | \spawn monster 572 |
| 573 | - | chosen | Chosen | 75774 | 34004 / - | EliteWanderer | 0 | 3 / - | 0 | \spawn monster 573 |
| 574 | - | monster | Misc | 75773 | 75822 / 75704 | Null | 0 | - | 0 | \spawn monster 574 |
| 575 | - | monster | Misc | 75683 | 75684 / 75685 | EliteWanderer | 0 | - | 0 | \spawn monster 575 |
| 576 | - | monster | Misc | 75775 | 75841 / 75704 | Null | 0 | - | 0 | \spawn monster 576 |
| 577 | - | accord | Human | 77000 | 75684 / 75705 | - | 0 | - | 0 | \spawn monster 577 |
| 578 | Oi | friendly | Companion | 76977 | - | - | 0 | - | 0 | \spawn monster 578 |
| 579 | Nanu | friendly | Companion | 76977 | - | - | 0 | - | 0 | \spawn monster 579 |
| 580 | - | gaea | Wildlife | 33823 | 33949 / - | ThresherTailWhipper | 0 | 3 / - | 0 | \spawn monster 580 |
| 581 | - | accord | Human | 52453 | - | PeacetimeCityWandererWithHealing | 0 | - | 0 | \spawn monster 581 |
| 582 | Brontodon | neutral | Large wildlife | 30033 | 77093 / - | - | 0 | 5705 / 5442 | 5 | \spawn monster 582 |
| 583 | - | monster | Misc | 30454 | 77093 / - | Brontodon | 0 | - | 0 | \spawn monster 583 |
| 584 | Brinewyrm | neutral | Large wildlife | 122909 | 122910 / - | - | 0 | 5704 / - | 5 | \spawn monster 584 |
| 585 | OBSOLETE Duke Skiver | gaea | Wildlife | 97039 | 97038 / - | - | 0 | 5704 / - | 0 | \spawn monster 585 |
| 586 | OBSOLETE Storm Kestrel | neutral | Large wildlife | 77105 | 77268 / 77248 | - | 0 | 5704 / - | 5 | \spawn monster 586 |
| 587 | Crab | neutral | Large wildlife | 77112 | - | Wander | 0 | 1 / - | 0 | \spawn monster 587 |
| 588 | OBSOLETE Nautilus | gaea | Wildlife | 77117 | 92800 / - | Arch_MoveThenFire_Base | 0 | 5705 / 5451 | 5 | \spawn monster 588 |
| 589 | - | gaea | Wildlife | 66845 | 20046 / - | FeralCanineSquad | 0 | 2 / - | 0 | \spawn monster 589 |
| 590 | - | gaea | Wildlife | 77121 | 77539 / 77539 | BetaAranha | 0 | 2 / - | 0 | \spawn monster 590 |
| 591 | Pirate Burster | bandit | Outlaw | 77169 | 77335 / - | EliteWanderer | 0 | 5311 / 5378 | 5 | \spawn monster 591 |
| 592 | Pirate Grenadier | bandit | Outlaw | 77169 | 96848 / - | EliteWanderer | 0 | 5311 / 5378 | 5 | \spawn monster 592 |
| 593 | Pirate Gunner | bandit | Outlaw | 77169 | 77337 / - | EliteWanderer | 0 | 5311 / 5378 | 5 | \spawn monster 593 |
| 594 | Pirate Assault | bandit | Outlaw | 77170 | 77339 / - | EliteWanderer | 0 | 5311 / 5378 | 5 | \spawn monster 594 |
| 595 | Pirate Dreadnaught | bandit | Outlaw | 77171 | 77338 / - | SiegebreakerWithCharge | 0 | 5312 / 5379 | 5 | \spawn monster 595 |
| 596 | Argonaut | gaea | Wildlife | 77129 | - | Argonaut | 0 | 5703 / 5448 | 5 | \spawn monster 596 |
| 597 | - | accord | Human | 77133 | - | AlertAndInteractive | 0 | - | 0 | \spawn monster 597 |
| 598 | Melding Acolyte | chosen | Chosen | 77172 | 77328 / - | Arch_Brute_Acolyte_Base | 0 | 5272 / - | 0 | \spawn monster 598 |
| 599 | Baneclaw | melding | Melded | 77149 | 142087 / - | - | 0 | 5314 / 5431 | 0 | \spawn monster 599 |
| 600 | Hellclaw | melding | Melded | 77154 | - | - | 0 | 5705 / - | 5 | \spawn monster 600 |
| 601 | - | gaea | Wildlife | 34340 | 34478 / - | MeldingAcolyte | 0 | 4 / - | 0 | \spawn monster 601 |
| 602 | - | melding | Melded | 77217 | 77218 / - | Mosquito | 0 | 5724 / 5815 | 0 | \spawn monster 602 |
| 603 | - | melding | Melded | 77288 | 32747 / - | Mosquito | 0 | 5311 / - | 0 | \spawn monster 603 |
| 604 | - | melding | Melded | 77289 | 34089 / - | Mosquito | 0 | 5726 / 5817 | 0 | \spawn monster 604 |
| 605 | Science OFC Barness | accord | Human | 81333 | - | AlertAndInteractive | 0 | - | 0 | \spawn monster 605 |
| 606 | Frost | accord | Human | 10001 | - | StationaryCivilianDialog | 0 | - | 0 | \spawn monster 606 |
| 607 | El Terremoto | accord | Human | 10001 | - | AlertAndInteractive | 0 | - | 0 | \spawn monster 607 |
| 608 | Dynamo | accord | Human | 81412 | - | AlertAndInteractive | 0 | - | 0 | \spawn monster 608 |
| 609 | - | neutral | Large wildlife | 30033 | 77093 / 77140 | Brontodon | 0 | - | 0 | \spawn monster 609 |
| 610 | Holmgang Security | accord | Human | 75105 | - / 30025 | - | 0 | - | 0 | \spawn monster 610 |
| 611 | - | bandit | Outlaw | 77300 | - / 67423 | EliteWanderer | 0 | 5311 / - | 0 | \spawn monster 611 |
| 612 | - | accord | Human | 10003 | - | AlertAndInteractive | 0 | - | 0 | \spawn monster 612 |
| 613 | - | gaea | Wildlife | 34217 | 34077 / - | - | 0 | 5294 / - | 0 | \spawn monster 613 |
| 614 | - | friendly | Companion | 34430 | - | AlertAndInteractive | 0 | - | 0 | \spawn monster 614 |
| 615 | - | neutral | Large wildlife | 77105 | 77268 / 77248 | Squawwk | 0 | 3 / - | 0 | \spawn monster 615 |
| 616 | - | gaea | Wildlife | 77097 | 77333 / - | FeralCanineSquad | 0 | 3 / - | 0 | \spawn monster 616 |
| 617 | - | accord | Human | 30028 | 30140 / - | AggressiveWanderer | 0 | - | 0 | \spawn monster 617 |
| 618 | Straw Hat Thresher | friendly | Companion | 85385 | - | - | 0 | - | 0 | \spawn monster 618 |
| 619 | - | accord | Human | 10001 | - / 30025 | Null | 0 | - | 0 | \spawn monster 619 |
| 620 | Marcelo | accord | Human | 10003 | - | AlertAndInteractive | 0 | - | 0 | \spawn monster 620 |
| 621 | Dick Allen | accord | Human | 10003 | - | AlertAndInteractive | 0 | - | 0 | \spawn monster 621 |
| 622 | Signal | neutral | Large wildlife | 10003 | - | AlertAndInteractive | 0 | - | 0 | \spawn monster 622 |
| 623 | Automated Teller Bot | friendly | Companion | 77374 | - | AlertAndInteractive | 0 | - | 0 | \spawn monster 623 |
| 624 | Marcia the Flame | accord | Human | 52457 | - | AlertAndInteractive | 0 | - | 0 | \spawn monster 624 |
| 625 | Bandit Explosives Vendor | bandit | Outlaw | 77300 | - / 67423 | AlertAndInteractive | 0 | 5327 / - | 0 | \spawn monster 625 |
| 626 | - | gaea | Wildlife | 77385 | 86735 / - | - | 0 | 4 / 5416 | 0 | \spawn monster 626 |
| 627 | - | melding | Melded | 34002 | 34003 / - | SwarmWanderer | 0 | 5312 / - | 0 | \spawn monster 627 |
| 628 | Bike Voice | neutral | Large wildlife | 0 | - | Null | 0 | - | 0 | \spawn monster 628 |
| 629 | Corporal Garland | accord | Human | 116868 | - / 30025 | AlertAndInteractive | 0 | - | 0 | \spawn monster 629 |
| 630 | Claudia Fonseca | accord | Human | 96814 | - | AlertAndInteractive | 0 | - | 0 | \spawn monster 630 |
| 631 | - | accord | Human | 10001 | - / 76108 | - | 0 | - | 0 | \spawn monster 631 |
| 632 | - | bandit | Outlaw | 77423 | 30688 / 77335 | EliteWanderer | 0 | 5313 / 5389 | 0 | \spawn monster 632 |
| 633 | Young Brontodon | neutral | Large wildlife | 77424 | 137142 / - | - | 0 | 5704 / 5441 | 5 | \spawn monster 633 |
| 634 | Elder Brontodon | neutral | Large wildlife | 77425 | 77093 / 77140 | - | 0 | 5705 / 5443 | 5 | \spawn monster 634 |
| 635 | Ancient Brontodon | gaea | Large wildlife | 77426 | 77140 / - | - | 0 | 5707 / 5444 | 5 | \spawn monster 635 |
| 636 | - | accord | Human | 30160 | - | - | 0 | - | 0 | \spawn monster 636 |
| 637 | - | gaea | Wildlife | 77447 | 77446 / 77445 | - | 0 | 4 / - | 0 | \spawn monster 637 |
| 638 | - | accord | Human | 10001 | 20024 / - | Wander | 0 | - | 0 | \spawn monster 638 |
| 639 | - | gaea | Wildlife | 77477 | 77479 / - | MeleeGruntWanderer | 0 | 4 / 5398 | 0 | \spawn monster 639 |
| 640 | Melding Acolyte | chosen | Chosen | 77485 | 77328 / - | MeldingAcolyte | 0 | 5272 / - | 0 | \spawn monster 640 |
| 641 | Alpha Wargrim | gaea | Wildlife | 77489 | 97244 / - | Arch_AdditiveMelee_Base | 0 | 5705 / 6213 | 5 | \spawn monster 641 |
| 642 | Herdmaster Brontodon | gaea | Large wildlife | 77491 | 77093 / 77140 | Brontodon | 0 | 5706 / 5442 | 5 | \spawn monster 642 |
| 643 | - | chosen | Chosen | 32740 | 77494 / 33064 | EliteWanderer | 0 | 5698 / - | 0 | \spawn monster 643 |
| 644 | - | gaea | Wildlife | 77502 | 33949 / - | ThresherCharger | 0 | 5704 / 5432 | 0 | \spawn monster 644 |
| 645 | Wintertide Elf | friendly | Companion | 77508 | - | TestElfPet | 0 | - | 0 | \spawn monster 645 |
| 646 | Accord Recruitment | accord | Human | 0 | - | Null | 0 | - | 0 | \spawn monster 646 |
| 647 | Copa Power Supply | accord | Human | 0 | - | Null | 0 | - | 0 | \spawn monster 647 |
| 648 | Mayor Palmeiro | accord | Human | 0 | - | - | 0 | - | 0 | \spawn monster 648 |
| 649 | ARES Team | accord | Human | 0 | - | Null | 0 | - | 0 | \spawn monster 649 |
| 650 | Arclight Rescue | accord | Human | 0 | - | Null | 0 | - | 0 | \spawn monster 650 |
| 651 | Dismantling the Arclight | accord | Human | 0 | - | Null | 0 | - | 0 | \spawn monster 651 |
| 652 | Admiral Nostromo | accord | Human | 0 | - | Null | 0 | - | 0 | \spawn monster 652 |
| 653 | Missing Shipment | accord | Human | 0 | - | Null | 0 | - | 0 | \spawn monster 653 |
| 654 | The Chosen | accord | Human | 0 | - | Null | 0 | - | 0 | \spawn monster 654 |
| 655 | SIN Hacking | accord | Human | 0 | - | Null | 0 | - | 0 | \spawn monster 655 |
| 656 | - | accord | Human | 0 | - | Null | 0 | - | 0 | \spawn monster 656 |
| 657 | - | accord | Human | 0 | - | Null | 0 | - | 0 | \spawn monster 657 |
| 658 | Ol' Man Bill | accord | Human | 0 | - | Null | 0 | - | 0 | \spawn monster 658 |
| 659 | Shady Ad | accord | Human | 0 | - | Null | 0 | - | 0 | \spawn monster 659 |
| 660 | Trolling Oilspill | accord | Human | 0 | - | Null | 0 | - | 0 | \spawn monster 660 |
| 661 | Thumper History | accord | Human | 0 | - | Null | 0 | - | 0 | \spawn monster 661 |
| 662 | Oilspill | accord | Human | 0 | - | Null | 0 | - | 0 | \spawn monster 662 |
| 663 | The Aegis | accord | Human | 0 | - | Null | 0 | - | 0 | \spawn monster 663 |
| 664 | Mustang's Memo | accord | Human | 0 | - | Null | 0 | - | 0 | \spawn monster 664 |
| 665 | Holmgang Show | accord | Human | 0 | - | Null | 0 | - | 0 | \spawn monster 665 |
| 666 | Omnidyne Commercial | accord | Human | 0 | - | Null | 0 | - | 0 | \spawn monster 666 |
| 667 | Captain's Log | accord | Human | 0 | - | Null | 0 | - | 0 | \spawn monster 667 |
| 668 | Earth First | accord | Human | 0 | - | Null | 0 | - | 0 | \spawn monster 668 |
| 669 | Scientific Progress | accord | Human | 0 | - | Null | 0 | - | 0 | \spawn monster 669 |
| 670 | Sabotage | accord | Human | 0 | - | Null | 0 | - | 0 | \spawn monster 670 |
| 671 | Global Warming | accord | Human | 0 | - | Null | 0 | - | 0 | \spawn monster 671 |
| 672 | Walking Tour | accord | Human | 0 | - | Null | 0 | - | 0 | \spawn monster 672 |
| 673 | Pickle's Diary | accord | Human | 0 | - | Null | 0 | - | 0 | \spawn monster 673 |
| 674 | Brontodon Poem | accord | Human | 0 | - | Null | 0 | - | 0 | \spawn monster 674 |
| 675 | Sloshy Stan | accord | Human | 0 | - | Null | 0 | - | 0 | \spawn monster 675 |
| 676 | Poaching | accord | Human | 0 | - | Null | 0 | - | 0 | \spawn monster 676 |
| 677 | SIN Imprint Conspiracy | accord | Human | 0 | - | Null | 0 | - | 0 | \spawn monster 677 |
| 678 | Ricardo's Concerns | accord | Human | 0 | - | Null | 0 | - | 0 | \spawn monster 678 |
| 679 | Quarantine Measures | accord | Human | 0 | - | Null | 0 | - | 0 | \spawn monster 679 |
| 680 | Monohan's Rebuttal | accord | Human | 0 | - | Null | 0 | - | 0 | \spawn monster 680 |
| 681 | Outbreak | accord | Human | 0 | - | Null | 0 | - | 0 | \spawn monster 681 |
| 682 | Security Lockdown | accord | Human | 0 | - | Null | 0 | - | 0 | \spawn monster 682 |
| 683 | SIN Excision | accord | Human | 0 | - | Null | 0 | - | 0 | \spawn monster 683 |
| 684 | Smuggling Affinites | accord | Human | 0 | - | Null | 0 | - | 0 | \spawn monster 684 |
| 685 | Identity Gift | accord | Human | 0 | - | Null | 0 | - | 0 | \spawn monster 685 |
| 686 | Rebellion | accord | Human | 0 | - | Null | 0 | - | 0 | \spawn monster 686 |
| 687 | Affinite Bounty | accord | Human | 0 | - | Null | 0 | - | 0 | \spawn monster 687 |
| 688 | Brody | accord | Human | 86145 | - / 30025 | - | 0 | - | 0 | \spawn monster 688 |
| 689 | - | gaea | Wildlife | 77532 | 77164 / 77163 | Nautilus | 0 | 5705 / 5450 | 0 | \spawn monster 689 |
| 690 | - | gaea | Wildlife | 77534 | 77164 / 77163 | - | 0 | 5706 / 5453 | 5 | \spawn monster 690 |
| 691 | Acolyte Diseased Culex | melding | Melded | 77536 | 77218 / - | Mosquito | 0 | 5704 / 5414 | 0 | \spawn monster 691 |
| 692 | Acolyte Hisser | melding | Melded | 77537 | 34003 / - | SwarmWanderer | 0 | 5312 / 5458 | 0 | \spawn monster 692 |
| 693 | OBSOLETE Skiver | gaea | Wildlife | 77121 | 77539 / - | - | 0 | 5703 / - | 5 | \spawn monster 693 |
| 694 | - | gaea | Wildlife | 34339 | 34478 / - | AggressiveWanderer | 0 | 5705 / - | 0 | \spawn monster 694 |
| 695 | - | chosen | Chosen | 77540 | 32739 / 33064 | - | 0 | - | 0 | \spawn monster 695 |
| 696 | - | gaea | Wildlife | 77573 | 77574 / - | - | 0 | 5704 / 5414 | 5 | \spawn monster 696 |
| 697 | - | chosen | Chosen | 77580 | 66311 / 33064 | EliteWanderer | 0 | 3 / - | 0 | \spawn monster 697 |
| 698 | - | gaea | Wildlife | 77616 | 77617 / - | BetaAranha | 0 | 2 / - | 0 | \spawn monster 698 |
| 699 | - | chosen | Chosen | 32740 | 66311 / 33064 | EliteWanderer | 0 | 3 / - | 0 | \spawn monster 699 |
| 700 | - | chosen | Chosen | 31267 | 77049 / 32739 | EliteWanderer | 0 | 3 / - | 0 | \spawn monster 700 |
| 701 | Heavy Turret - Gunner | accord | Human | 33976 | - / 77777 | AggressiveWanderer | 0 | - | 0 | \spawn monster 701 |
| 702 | - | gaea | Wildlife | 77666 | 30903 / - | SwarmWanderer | 0 | 3 / 5405 | 0 | \spawn monster 702 |
| 703 | - | gaea | Wildlife | 81296 | 30903 / - | SwarmWanderer | 0 | 3 / 5406 | 0 | \spawn monster 703 |
| 704 | - | accord | Human | 10001 | - / 76108 | Arch_MedRangedHumanoid_Base | 0 | - | 0 | \spawn monster 704 |
| 705 | - | accord | Human | 56723 | - / 76108 | Arch_MedRangedHumanoid_Base | 0 | - | 0 | \spawn monster 705 |
| 706 | - | accord | Human | 33976 | - / 77697 | AggressiveWanderer | 0 | - | 0 | \spawn monster 706 |
| 707 | - | chosen | Chosen | 32742 | 77337 / - | EliteWanderer | 0 | 5325 / - | 0 | \spawn monster 707 |
| 708 | Carcass | chosen | Chosen | 77699 | 85745 / - | Arch_AdditiveMelee_Base | 0 | 5697 / 5458 | 5 | \spawn monster 708 |
| 709 | Vile Carcass | chosen | Chosen | 77701 | 77879 / - | Arch_MoveThenFire_Base | 0 | 5698 / 5459 | 5 | \spawn monster 709 |
| 710 | - | chosen | Chosen | 32740 | 32739 / 33064 | EliteWanderer | 0 | 3 / - | 0 | \spawn monster 710 |
| 711 | - | accord | Human | 77733 | - | - | 0 | - | 0 | \spawn monster 711 |
| 712 | Female Tutorial Player Character | accord | Human | 77733 | - | - | 0 | - | 0 | \spawn monster 712 |
| 713 | - | accord | Human | 52454 | - | PlayerPet | 0 | - | 0 | \spawn monster 713 |
| 714 | - | chosen | Chosen | 77735 | 85756 / - | StockMoveAndShootGuy | 0 | 5698 / 5369 | 0 | \spawn monster 714 |
| 715 | Target Drone | monster | Misc | 77744 | - | Null | 0 | - | 0 | \spawn monster 715 |
| 716 | - | chosen | Chosen | 77762 | 75681 / - | ChosenEngineerDrone | 0 | 5698 / - | 0 | \spawn monster 716 |
| 717 | - | bandit | Human | 125034 | 106335 / - | - | 10004 | 3 / - | 0 | \spawn monster 717 |
| 718 | - | accord | Human | 33976 | - / 77697 | AggressiveWanderer | 0 | - | 0 | \spawn monster 718 |
| 719 | - | accord | Human | 33976 | - / 77697 | AggressiveWanderer | 0 | - | 0 | \spawn monster 719 |
| 720 | Turret Controller - Artillery Turret | chosen | Chosen | 32740 | 77723 / - | EliteWanderer | 0 | - | 0 | \spawn monster 720 |
| 721 | Control Worker | accord | Human | 77848 | - | AlertAndInteractive | 0 | - | 0 | \spawn monster 721 |
| 722 | Control Worker | accord | Human | 77848 | - | AlertAndInteractive | 0 | - | 0 | \spawn monster 722 |
| 723 | Accord Guard | accord | Human | 77858 | 78495 / - | AlertAndInteractive | 0 | - | 5 | \spawn monster 723 |
| 724 | Researcher | accord | Human | 77862 | - | AlertAndInteractive | 0 | - | 0 | \spawn monster 724 |
| 725 | Researcher | accord | Human | 77862 | - | AlertAndInteractive | 0 | - | 0 | \spawn monster 725 |
| 726 | Researcher | accord | Human | 77862 | - | AlertAndInteractive | 0 | - | 0 | \spawn monster 726 |
| 727 | Researcher | accord | Human | 77862 | - | AlertAndInteractive | 0 | - | 0 | \spawn monster 727 |
| 728 | Researcher | accord | Human | 77862 | - | AlertAndInteractive | 0 | - | 0 | \spawn monster 728 |
| 729 | Researcher Engineer | accord | Human | 77872 | - | PerformEmote | 0 | - | 0 | \spawn monster 729 |
| 730 | Dock Foreman | accord | Human | 77864 | - | AlertAndInteractive | 0 | - | 0 | \spawn monster 730 |
| 731 | Dock Foreman | accord | Human | 77864 | - | AlertAndInteractive | 0 | - | 0 | \spawn monster 731 |
| 732 | Drill Sergeant | accord | Human | 77865 | - | AlertAndInteractive | 0 | - | 0 | \spawn monster 732 |
| 733 | Nostromo Body Guard | accord | Human | 124532 | 114319 / - | AlertAndInteractive | 0 | - | 0 | \spawn monster 733 |
| 734 | Recruit | accord | Human | 124354 | - | PerformEmote | 0 | - | 0 | \spawn monster 734 |
| 735 | Battlelab Guard | accord | Human | 117035 | 98897 / - | PerformEmote | 0 | - | 0 | \spawn monster 735 |
| 736 | Soldier | accord | Human | 77867 | - | AlertAndInteractive | 0 | - | 0 | \spawn monster 736 |
| 737 | Soldier | accord | Human | 77867 | - | AlertAndInteractive | 0 | - | 0 | \spawn monster 737 |
| 738 | Senior Officer | accord | Human | 77868 | - | AlertAndInteractive | 0 | - | 0 | \spawn monster 738 |
| 739 | Officer Sr. Assistant | accord | Human | 77868 | - | AlertAndInteractive | 0 | - | 0 | \spawn monster 739 |
| 740 | - | neutral | Large wildlife | 77869 | - | PassiveWanderer | 0 | - | 0 | \spawn monster 740 |
| 741 | Lazy Engineer | accord | Human | 117022 | - | AlertAndInteractive | 0 | - | 0 | \spawn monster 741 |
| 742 | - | accord | Human | 77871 | - | AlertAndInteractive | 0 | - | 0 | \spawn monster 742 |
| 743 | Battlelab Engineer | accord | Human | 77872 | - | AlertAndLookAtPlayer | 0 | - | 0 | \spawn monster 743 |
| 744 | Battlelab Working Engineer | accord | Human | 117022 | - | AlertAndInteractive | 0 | - | 0 | \spawn monster 744 |
| 745 | Officer Jr. | accord | Human | 77868 | - | AlertAndInteractive | 0 | - | 0 | \spawn monster 745 |
| 746 | Officer Jr. | accord | Human | 77868 | - | AlertAndInteractive | 0 | - | 0 | \spawn monster 746 |
| 747 | Friendly Target Drone | accord | Human | 77876 | - | PerformEmote | 0 | - | 0 | \spawn monster 747 |
| 748 | Simulated Incapacitated Friendly | accord | Human | 77888 | - | - | 0 | - | 0 | \spawn monster 748 |
| 749 | Simulated Incapacitated Friendly | accord | Human | 77888 | - | - | 0 | - | 0 | \spawn monster 749 |
| 750 | Inventory Taker | accord | Human | 77872 | - | AlertAndInteractive | 0 | - | 0 | \spawn monster 750 |
| 751 | - | accord | Human | 77871 | - | AlertAndInteractive | 0 | - | 0 | \spawn monster 751 |
| 752 | Target Drone - Friendly | friendly | Companion | 80709 | - | InteractiveWithEmote | 0 | - | 0 | \spawn monster 752 |
| 753 | Aggressive Target Drone | gaea | Wildlife | 77744 | - | AggressiveWanderer | 0 | - | 0 | \spawn monster 753 |
| 754 | - | chosen | Chosen | 78005 | 32739 / - | ChosenTrooper | 0 | 3 / - | 0 | \spawn monster 754 |
| 755 | Technician engineer | accord | Human | 77872 | - | AlertAndInteractive | 0 | - | 0 | \spawn monster 755 |
| 756 | Mechanic | accord | Human | 30160 | - | AlertAndInteractive | 0 | - | 0 | \spawn monster 756 |
| 757 | Technician | accord | Human | 78971 | - | AlertAndInteractive | 0 | - | 0 | \spawn monster 757 |
| 758 | lazy mechanic | accord | Human | 77872 | - | AlertAndInteractive | 0 | - | 0 | \spawn monster 758 |
| 759 | - | gaea | Wildlife | 81295 | 81293 / 30808 | - | 0 | 3 / 5396 | 5 | \spawn monster 759 |
| 760 | - | bandit | Outlaw | 81297 | 32741 / - | EliteWanderer | 0 | 5298 / - | 0 | \spawn monster 760 |
| 761 | Ophanim Agent | bandit | Outlaw | 118231 | 103849 / - | - | 0 | 5311 / 5388 | 5 | \spawn monster 761 |
| 762 | Bandit Overclocked | bandit | Outlaw | 81307 | 81308 / - | - | 0 | 3 / 5386 | 5 | \spawn monster 762 |
| 763 | OBSOLETE Giant Aranha | gaea | Wildlife | 81311 | 82340 / - | GiantAranhaMiniBoss | 0 | 5704 / 5396 | 5 | \spawn monster 763 |
| 764 | - | accord | Human | 52458 | - | - | 0 | - | 0 | \spawn monster 764 |
| 765 | - | accord | Human | 81429 | - | - | 0 | - | 0 | \spawn monster 765 |
| 766 | Oilspill | accord | Human | 81323 | - | AlertAndInteractive | 0 | - | 0 | \spawn monster 766 |
| 767 | Corporal Jaso | accord | Human | 81391 | - | AlertAndInteractive | 0 | - | 0 | \spawn monster 767 |
| 768 | Amancio Rios | accord | Human | 81367 | - | PeacetimeCityWanderer | 0 | - | 0 | \spawn monster 768 |
| 769 | Axel | accord | Human | 81367 | - | PeacetimeCityWanderer | 0 | - | 0 | \spawn monster 769 |
| 770 | Dross | accord | Human | 81327 | - | AlertAndInteractive | 0 | - | 0 | \spawn monster 770 |
| 771 | - | accord | Human | 81369 | - | PeacetimeCityWanderer | 0 | - | 0 | \spawn monster 771 |
| 772 | Hawking | accord | Human | 81324 | - | AlertAndInteractive | 0 | - | 0 | \spawn monster 772 |
| 773 | Ol' Man Bill | accord | Human | 81325 | - | AlertAndInteractive | 0 | - | 0 | \spawn monster 773 |
| 774 | Jun Mori | accord | Human | 81342 | - | AlertAndInteractive | 0 | - | 0 | \spawn monster 774 |
| 775 | - | accord | Human | 81368 | - | PeacetimeCityWanderer | 0 | - | 0 | \spawn monster 775 |
| 776 | - | accord | Human | 81369 | - | PeacetimeCityWanderer | 0 | - | 0 | \spawn monster 776 |
| 777 | Snorri V. | accord | Human | 85774 | - | PeacetimeCityWanderer | 0 | - | 0 | \spawn monster 777 |
| 778 | Mule | accord | Human | 81389 | - | AlertAndInteractive | 0 | - | 10 | \spawn monster 778 |
| 779 | Hamid Nejem | accord | Human | 82400 | - | AlertAndInteractive | 0 | - | 0 | \spawn monster 779 |
| 780 | Mitch "Alligator" Freise | accord | Human | 81327 | - | AlertAndInteractive | 0 | - | 0 | \spawn monster 780 |
| 781 | Gus Walker | accord | Human | 81370 | - | PeacetimeCityWanderer | 0 | - | 0 | \spawn monster 781 |
| 782 | Sergio | accord | Human | 81370 | - | PeacetimeCityWanderer | 0 | - | 0 | \spawn monster 782 |
| 783 | Iolanda | accord | Human | 81370 | - | PeacetimeCityWanderer | 0 | - | 0 | \spawn monster 783 |
| 784 | Chartreuse | accord | Human | 81367 | - | PeacetimeCityWanderer | 0 | - | 0 | \spawn monster 784 |
| 785 | - | accord | Human | 81393 | 84915 / - | GuardCityWanderer | 0 | - | 0 | \spawn monster 785 |
| 786 | Corporal Rakes | accord | Human | 81392 | 84916 / - | GuardCityWanderer | 0 | - | 0 | \spawn monster 786 |
| 787 | Sgt. Maxine Hammer | accord | Human | 124276 | 96847 / - | AlertAndInteractive | 0 | - | 0 | \spawn monster 787 |
| 788 | Captain Fredericks | accord | Human | 81393 | 84917 / - | GuardCityWanderer | 0 | - | 0 | \spawn monster 788 |
| 789 | Trippy | accord | Human | 81471 | - | AlertAndInteractive | 0 | - | 0 | \spawn monster 789 |
| 790 | - | accord | Human | 81367 | - | PeacetimeCityWanderer | 0 | - | 0 | \spawn monster 790 |
| 791 | Dr. Beatrix Jardine | accord | Human | 81326 | - | AlertAndInteractive | 0 | - | 0 | \spawn monster 791 |
| 792 | Nurse Franco | accord | Human | 81372 | - | PeacetimeCityWanderer | 0 | - | 0 | \spawn monster 792 |
| 793 | Susan Bartle | accord | Human | 82397 | - | AlertAndInteractive | 0 | - | 0 | \spawn monster 793 |
| 794 | - | accord | Human | 81371 | - | PeacetimeCityWanderer | 0 | - | 0 | \spawn monster 794 |
| 795 | Lieutenant Dale Truman | accord | Human | 81371 | - | PeacetimeCityWanderer | 0 | - | 0 | \spawn monster 795 |
| 796 | Otter | accord | Human | 81477 | - | AlertAndInteractive | 0 | - | 0 | \spawn monster 796 |
| 797 | Chief Nigel Lewis | accord | Human | 81373 | - | PeacetimeCityWanderer | 0 | - | 0 | \spawn monster 797 |
| 798 | Vitor Martin | accord | Human | 81373 | - | PeacetimeCityWanderer | 0 | - | 0 | \spawn monster 798 |
| 799 | Lieutenant Namgung | accord | Human | 81373 | - | PeacetimeCityWanderer | 0 | - | 0 | \spawn monster 799 |
| 800 | Consul Nostromo | accord | Human | 81344 | - | Nostromo | 0 | - | 0 | \spawn monster 800 |
| 801 | Commander Samuel Burke | accord | Human | 81374 | - | AlertAndInteractive | 0 | - | 0 | \spawn monster 801 |
| 802 | Admiral Jason Archinaco | accord | Human | 81345 | - | AlertAndInteractive | 0 | - | 0 | \spawn monster 802 |
| 803 | Major Paulo Silva | accord | Human | 81374 | - | AlertAndInteractive | 0 | - | 0 | \spawn monster 803 |
| 804 | Commodore Annabel Mundy | accord | Human | 81374 | - | AlertAndInteractive | 0 | - | 0 | \spawn monster 804 |
| 805 | Staff Sergeant Thane Fisher | accord | Human | 81979 | - | AlertAndInteractive | 0 | - | 0 | \spawn monster 805 |
| 806 | - | accord | Human | 81375 | - | PeacetimeCityWanderer | 0 | - | 0 | \spawn monster 806 |
| 807 | Corporal Brice Raines | accord | Human | 81393 | 84917 / - | GuardCityWanderer | 0 | - | 0 | \spawn monster 807 |
| 808 | - | accord | Human | 81375 | - | PeacetimeCityWanderer | 0 | - | 0 | \spawn monster 808 |
| 809 | Riptide | accord | Human | 81376 | - | PeacetimeCityWanderer | 0 | - | 0 | \spawn monster 809 |
| 810 | Blanca Gomes | accord | Human | 81414 | - | AlertAndInteractive | 0 | - | 0 | \spawn monster 810 |
| 811 | Ines Belo | accord | Human | 81376 | - | PeacetimeCityWanderer | 0 | - | 0 | \spawn monster 811 |
| 812 | - | accord | Human | 81376 | - | PeacetimeCityWanderer | 0 | - | 0 | \spawn monster 812 |
| 813 | Corporal Gavin Butler | accord | Human | 81393 | 84917 / - | GuardCityWanderer | 0 | - | 0 | \spawn monster 813 |
| 814 | - | accord | Human | 81391 | 84915 / - | GuardCityWanderer | 0 | - | 0 | \spawn monster 814 |
| 815 | Lieutenant Tim Daniels | accord | Human | 81392 | 84916 / - | GuardCityWanderer | 0 | - | 0 | \spawn monster 815 |
| 816 | Sergeant Zeus McClellan | accord | Human | 81392 | 84916 / - | GuardCityWanderer | 0 | - | 0 | \spawn monster 816 |
| 817 | - | accord | Human | 81391 | 84915 / - | GuardCityWanderer | 0 | - | 0 | \spawn monster 817 |
| 818 | Corporal Fiona Boyle | accord | Human | 81392 | 84916 / - | GuardCityWanderer | 0 | - | 0 | \spawn monster 818 |
| 819 | Lieutenant Sarah Chevelle | accord | Human | 81393 | 84917 / - | GuardCityWanderer | 0 | - | 0 | \spawn monster 819 |
| 820 | - | accord | Human | 81377 | - | AlertAndInteractive | 0 | - | 0 | \spawn monster 820 |
| 821 | Fatima Belo | - | Human | 81378 | - | PeacetimeCityWanderer | 0 | - | 0 | \spawn monster 821 |
| 822 | Indra Rodrigues | accord | Human | 81377 | - | PeacetimeCityWanderer | 0 | - | 0 | \spawn monster 822 |
| 823 | Rosa Costa | - | Human | 81381 | - | PeacetimeCityWanderer | 0 | - | 0 | \spawn monster 823 |
| 824 | Corporal Leticia Delgado | accord | Human | 81980 | - | AlertAndInteractive | 0 | - | 0 | \spawn monster 824 |
| 825 | Old Felix Simoes | - | Human | 81377 | - | PeacetimeCityWanderer | 0 | - | 0 | \spawn monster 825 |
| 826 | Christobel | accord | Human | 118219 | - | AlertAndInteractive | 0 | - | 0 | \spawn monster 826 |
| 827 | Biff Meister | - | Human | 81377 | - | PeacetimeCityWanderer | 0 | - | 0 | \spawn monster 827 |
| 828 | Crotchety Earl | - | Human | 81379 | - | PeacetimeCityWanderer | 0 | - | 0 | \spawn monster 828 |
| 829 | - | accord | Human | 77848 | - | PerformEmoteNoPhysics | 0 | - | 0 | \spawn monster 829 |
| 830 | - | accord | Human | 81329 | - | AlertAndInteractive | 0 | - | 0 | \spawn monster 830 |
| 831 | Teobalde Palmeiro | accord | Human | 117016 | - | AlertAndInteractive | 0 | - | 0 | \spawn monster 831 |
| 832 | Jose Vargas | accord | Human | 81380 | - | AlertAndInteractive | 0 | - | 0 | \spawn monster 832 |
| 833 | Joana Medeiros | accord | Human | 81381 | - | AlertAndInteractive | 0 | - | 0 | \spawn monster 833 |
| 834 | - | accord | Human | 81391 | 84915 / - | GuardCityWanderer | 0 | - | 0 | \spawn monster 834 |
| 835 | Alex Sundal | accord | Human | 81378 | - | AlertAndInteractive | 0 | - | 0 | \spawn monster 835 |
| 836 | Spicy Al | accord | Human | 81330 | - | AlertAndInteractive | 0 | - | 0 | \spawn monster 836 |
| 837 | - | accord | Human | 77848 | - | PerformEmoteNoPhysics | 0 | - | 0 | \spawn monster 837 |
| 838 | - | accord | Human | 77848 | - | PerformEmoteNoPhysics | 0 | - | 0 | \spawn monster 838 |
| 839 | - | accord | Human | 77848 | - | PerformEmoteNoPhysics | 0 | - | 0 | \spawn monster 839 |
| 840 | - | accord | Human | 77848 | - | PerformEmoteNoPhysics | 0 | - | 0 | \spawn monster 840 |
| 841 | Sergeant Cleve Wolfe | accord | Human | 96984 | - | AlertAndInteractive | 0 | - | 0 | \spawn monster 841 |
| 842 | Lieutenant Jim Davies | accord | Human | 81392 | 84916 / - | AlertAndInteractive | 0 | - | 0 | \spawn monster 842 |
| 843 | - | accord | Human | 81378 | - | AlertAndInteractive | 0 | - | 0 | \spawn monster 843 |
| 844 | Olivia Ferro | accord | Human | 81476 | - | AlertAndInteractive | 0 | - | 0 | \spawn monster 844 |
| 845 | Rubin Gallagher | accord | Human | 81377 | - | AlertAndInteractive | 0 | - | 0 | \spawn monster 845 |
| 846 | Sergeant Sam White | accord | Human | 81391 | 84915 / - | GuardCityWanderer | 0 | - | 0 | \spawn monster 846 |
| 847 | Sapphire Gallagher | accord | Human | 81377 | - | AlertAndInteractive | 0 | - | 0 | \spawn monster 847 |
| 848 | Dwayne Tucker | accord | Human | 81411 | - | AlertAndInteractive | 0 | - | 0 | \spawn monster 848 |
| 849 | - | accord | Human | 81391 | 84915 / - | AlertAndInteractive | 0 | - | 0 | \spawn monster 849 |
| 850 | Norma Tilda | accord | Human | 81379 | - | AlertAndInteractive | 0 | - | 0 | \spawn monster 850 |
| 851 | Private Donovan Davis | accord | Human | 81392 | 84916 / - | GuardCityWanderer | 0 | - | 0 | \spawn monster 851 |
| 852 | Lieutenant Donald Abram | accord | Human | 81393 | 84917 / - | GuardCityWanderer | 0 | - | 0 | \spawn monster 852 |
| 853 | Dustin Baloc | accord | Human | 82397 | - | AlertAndInteractive | 0 | - | 0 | \spawn monster 853 |
| 854 | Private Cecilia Abello | accord | Human | 81391 | 84915 / - | GuardCityWanderer | 0 | - | 0 | \spawn monster 854 |
| 855 | Rafaela Silva | accord | Human | 81381 | - | AlertAndInteractive | 0 | - | 0 | \spawn monster 855 |
| 856 | - | accord | Human | 81389 | - | AlertAndInteractive | 0 | - | 0 | \spawn monster 856 |
| 857 | Turan | accord | Human | 81386 | - | PeacetimeCityWanderer | 0 | - | 0 | \spawn monster 857 |
| 858 | Mustang | accord | Human | 81332 | 85226 / - | AlertAndInteractive | 0 | - | 0 | \spawn monster 858 |
| 859 | Marco Machado | accord | Human | 81385 | - | PeacetimeCityWanderer | 0 | - | 0 | \spawn monster 859 |
| 860 | The Duque | accord | Human | 81385 | - | PeacetimeCityWanderer | 0 | - | 0 | \spawn monster 860 |
| 861 | Georgio Germaine | accord | Human | 81386 | - | PeacetimeCityWanderer | 0 | - | 0 | \spawn monster 861 |
| 862 | Cal Denman | accord | Human | 81981 | - | AlertAndInteractive | 0 | - | 0 | \spawn monster 862 |
| 863 | Veneno | accord | Human | 81387 | - | PeacetimeCityWanderer | 0 | - | 0 | \spawn monster 863 |
| 864 | - | accord | Human | 81387 | - | PeacetimeCityWanderer | 0 | - | 0 | \spawn monster 864 |
| 865 | Eleuterio Crespo | accord | Human | 81388 | - | PeacetimeCityWanderer | 0 | - | 0 | \spawn monster 865 |
| 866 | - | accord | Human | 86148 | - | PeacetimeCityWanderer | 0 | - | 0 | \spawn monster 866 |
| 867 | Captain Heliodoro Ribeiro | accord | Human | 81393 | 84917 / - | GuardCityWanderer | 0 | - | 0 | \spawn monster 867 |
| 868 | Charlie Bravo | accord | Human | 81478 | - | AlertAndInteractive | 0 | - | 0 | \spawn monster 868 |
| 869 | Nestor | accord | Human | 81389 | - | PeacetimeCityWanderer | 0 | - | 0 | \spawn monster 869 |
| 870 | Rudolfo | accord | Human | 81389 | - | PeacetimeCityWanderer | 0 | - | 0 | \spawn monster 870 |
| 871 | Mayor Luciana Serafim | accord | Human | 82515 | - | AlertAndInteractive | 0 | - | 0 | \spawn monster 871 |
| 872 | Seti | accord | Human | 81390 | - | PeacetimeCityWanderer | 0 | - | 0 | \spawn monster 872 |
| 873 | Antonia Campos | accord | Human | 81390 | - | PeacetimeCityWanderer | 0 | - | 0 | \spawn monster 873 |
| 874 | Jurgen | accord | Human | 81343 | - | AlertAndInteractive | 0 | - | 0 | \spawn monster 874 |
| 875 | - | accord | Human | 81391 | 84915 / - | GuardCityWanderer | 0 | - | 0 | \spawn monster 875 |
| 876 | Corporal Greyson Cadwaller | accord | Human | 81393 | 84917 / - | GuardCityWanderer | 0 | - | 0 | \spawn monster 876 |
| 877 | Corporal Jon Joyner | accord | Human | 81393 | 84917 / - | GuardCityWanderer | 0 | - | 0 | \spawn monster 877 |
| 878 | Lieutenant Abel Costa | accord | Human | 81392 | 84916 / - | GuardCityWanderer | 0 | - | 0 | \spawn monster 878 |
| 879 | Crispino Largo | accord | Human | 81390 | - | PeacetimeCityWanderer | 0 | - | 0 | \spawn monster 879 |
| 880 | - | accord | Human | 81391 | 84915 / - | GuardCityWanderer | 0 | - | 0 | \spawn monster 880 |
| 881 | Lieutenant Maria Garcia | accord | Human | 81392 | 84916 / - | GuardCityWanderer | 0 | - | 0 | \spawn monster 881 |
| 882 | Kia Sofia | accord | Human | 82401 | - | AlertAndInteractive | 0 | - | 0 | \spawn monster 882 |
| 883 | Atlas | accord | Human | 81333 | - | AlertAndInteractive | 0 | - | 0 | \spawn monster 883 |
| 884 | Giant Nautilus | gaea | Wildlife | 81319 | 122919 / - | - | 0 | 5706 / 5452 | 5 | \spawn monster 884 |
| 885 | Private Yu | accord | Human | 81393 | 84917 / - | GuardCityWanderer | 0 | - | 0 | \spawn monster 885 |
| 886 | - | accord | Human | 81391 | 84915 / - | GuardCityWanderer | 0 | - | 0 | \spawn monster 886 |
| 887 | Scavenger Bot | neutral | Large wildlife | 81335 | - | FastWander | 0 | 3 / - | 0 | \spawn monster 887 |
| 888 | Toxic Sandshark | gaea | Wildlife | 81337 | 81402 / - | LandSharkMiniBoss | 0 | 5706 / - | 5 | \spawn monster 888 |
| 889 | Raider Baron | bandit | Outlaw | 81340 | 81396 / 82354 | RaiderBaronMiniBoss | 0 | 5313 / 5388 | 0 | \spawn monster 889 |
| 890 | - | accord | Human | 96945 | - | AlertAndInteractive | 0 | - | 0 | \spawn monster 890 |
| 891 | - | accord | Human | 81347 | - | AlertAndInteractive | 0 | - | 0 | \spawn monster 891 |
| 892 | T.E.X. | friendly | Companion | 81353 | - | - | 0 | - | 0 | \spawn monster 892 |
| 893 | - | chosen | Chosen | 81354 | 32739 / 33064 | Done | 0 | - | 0 | \spawn monster 893 |
| 894 | Bandit Hound | bandit | Outlaw | 81357 | 66846 / - | FeralCanineSquad | 0 | 5704 / - | 0 | \spawn monster 894 |
| 895 | - | monster | Misc | 77744 | 20046 / - | AggressiveWanderer | 0 | - | 0 | \spawn monster 895 |
| 896 | - | accord | Human | 81480 | 81469 / - | Mosquito | 0 | - | 0 | \spawn monster 896 |
| 897 | OBSOLETE Crystite Aranha | gaea | Wildlife | 81938 | 81968 / - | CrystalAranhaMiniBoss | 0 | 6137 / 5398 | 5 | \spawn monster 897 |
| 898 | Crystite Aranha Crystal | gaea | Wildlife | 81939 | - | CrystalAranhaCrystal | 0 | 5704 / - | 5 | \spawn monster 898 |
| 899 | - | gaea | Wildlife | 82344 | 81396 / - | Wander | 0 | 4 / 5451 | 0 | \spawn monster 899 |
| 900 | Reactor PA System | accord | Human | 0 | - | - | 0 | - | 0 | \spawn monster 900 |
| 901 | - | accord | Human | 82356 | - | PeacetimeCityWanderer | 0 | - | 0 | \spawn monster 901 |
| 902 | - | gaea | Wildlife | 82384 | - / 34081 | - | 0 | 5703 / - | 5 | \spawn monster 902 |
| 903 | - | bandit | Outlaw | 82386 | 82387 / - | EliteWanderer | 0 | 5311 / 5387 | 0 | \spawn monster 903 |
| 904 | - | chosen | Chosen | 32740 | 85643 / 95360 | Arch_MedRanged_Base | 0 | 3 / 5369 | 0 | \spawn monster 904 |
| 905 | - | accord | Human | 81391 | 84915 / - | GuardCityWanderer | 0 | - | 0 | \spawn monster 905 |
| 906 | OBSOLETE Elite Juggernaut | chosen | Chosen | 82463 | 82484 / - | Arch_MedRangedHumanoid_Base | 0 | 5700 / 5371 | 5 | \spawn monster 906 |
| 907 | Lieutenant Chelsea Maclean | accord | Human | 81392 | 84916 / - | GuardCityWanderer | 0 | - | 0 | \spawn monster 907 |
| 908 | Private Shelby Gladwyn | accord | Human | 81393 | 84917 / - | GuardCityWanderer | 0 | - | 0 | \spawn monster 908 |
| 909 | Sergeant Dominick Atkinson | accord | Human | 0 | 84917 / - | GuardCityWanderer | 0 | - | 0 | \spawn monster 909 |
| 910 | Elite Siegebreaker | chosen | Chosen | 82464 | 82465 / - | Arch_MoveThenFire_Base | 0 | 5700 / 5371 | 5 | \spawn monster 910 |
| 911 | Abducted Civilian | accord | Human | 82512 | - | Null | 0 | - | 0 | \spawn monster 911 |
| 912 | - | chosen | Chosen | 32740 | 82561 / - | EliteWanderer | 0 | - | 0 | \spawn monster 912 |
| 913 | - | accord | Human | 82563 | - | AlertAndInteractive | 0 | - | 0 | \spawn monster 913 |
| 914 | Vacationer | accord | Human | 82563 | - | AlertAndInteractive | 0 | - | 0 | \spawn monster 914 |
| 915 | - | accord | Human | 82563 | - | AlertAndInteractive | 0 | - | 0 | \spawn monster 915 |
| 916 | Vacationer | accord | Human | 82563 | - | AlertAndInteractive | 0 | - | 0 | \spawn monster 916 |
| 917 | Vacationer | accord | Human | 82563 | - | AlertAndLookAtPlayer | 0 | - | 0 | \spawn monster 917 |
| 918 | Vacationer | accord | Human | 82563 | - | AlertAndInteractive | 0 | - | 0 | \spawn monster 918 |
| 919 | - | accord | Human | 82563 | - | AlertAndInteractive | 0 | - | 0 | \spawn monster 919 |
| 920 | - | accord | Human | 52453 | - | AlertAndLookAtPlayer | 0 | - | 0 | \spawn monster 920 |
| 921 | Vacationer | accord | Human | 82563 | - | AlertAndLookAtPlayer | 0 | - | 0 | \spawn monster 921 |
| 922 | Vacationer | accord | Human | 82563 | - | AlertAndLookAtPlayer | 0 | - | 0 | \spawn monster 922 |
| 923 | Ash Dragon | gaea | Wildlife | 82591 | - | Arch_FullbodyMelee_Base | 0 | 5705 / 5778 | 5 | \spawn monster 923 |
| 924 | - | gaea | Wildlife | 82570 | 34476 / - | AggressiveWanderer | 0 | 3 / - | 0 | \spawn monster 924 |
| 925 | - | gaea | Wildlife | 34023 | - | Wander | 0 | 1 / - | 0 | \spawn monster 925 |
| 926 | - | gaea | Wildlife | 34339 | - | Wander | 0 | 1 / - | 0 | \spawn monster 926 |
| 927 | Toxic Varant | gaea | Wildlife | 82589 | 82762 / - | PoisonSalamander | 0 | 5726 / 5812 | 5 | \spawn monster 927 |
| 928 | OBSOLETE Armored Scorcher | gaea | Wildlife | 82571 | 85657 / 85658 | Arch_ArmoredScorcher_Base | 0 | 5705 / 5779 | 5 | \spawn monster 928 |
| 929 | - | gaea | Wildlife | 82588 | 82606 / - | Arch_MoveThenFire_Base | 0 | 5725 / 5433 | 5 | \spawn monster 929 |
| 930 | - | gaea | Wildlife | 66852 | 66851 / 67426 | ThresherSpitter | 0 | 3 / 5433 | 0 | \spawn monster 930 |
| 931 | OBSOLETE Scorcher | gaea | Wildlife | 82578 | 82608 / - | Arch_AdditiveMelee_Base | 0 | 5703 / 5779 | 5 | \spawn monster 931 |
| 932 | - | neutral | Large wildlife | 82576 | 77093 / 77140 | Brontodon | 0 | 5337 / 5442 | 0 | \spawn monster 932 |
| 933 | - | neutral | Large wildlife | 82576 | 77093 / 77140 | Brontodon | 0 | 5337 / 5442 | 0 | \spawn monster 933 |
| 934 | - | gaea | Wildlife | 82594 | 20046 / - | BetaAranha | 0 | 3 / 5632 | 0 | \spawn monster 934 |
| 935 | Wooly Brontodon | gaea | Wildlife | 82593 | 82616 / 82618 | Brontodon | 0 | 5726 / 5442 | 5 | \spawn monster 935 |
| 936 | - | monster | Misc | 30454 | 77093 / - | PassiveAttacker | 0 | - | 0 | \spawn monster 936 |
| 937 | Scorpion Culex | gaea | Wildlife | 82590 | 82601 / 82602 | - | 0 | 5725 / 5414 | 5 | \spawn monster 937 |
| 938 | Firejacket | gaea | Wildlife | 82582 | 82599 / - | - | 0 | 5704 / 5414 | 5 | \spawn monster 938 |
| 939 | - | accord | Human | 82579 | - | AlertAndInteractive | 0 | - | 0 | \spawn monster 939 |
| 940 | Rimehunter | gaea | Wildlife | 82592 | 82619 / 82663 | - | 0 | 5725 / 6212 | 5 | \spawn monster 940 |
| 941 | White Wargrim | gaea | Wildlife | 82583 | 82585 / - | - | 0 | 5705 / 6213 | 5 | \spawn monster 941 |
| 942 | - | gaea | Wildlife | 82584 | 82613 / 82614 | - | 0 | 5725 / 5632 | 5 | \spawn monster 942 |
| 943 | - | accord | Human | 82563 | - | AlertAndInteractive | 0 | - | 0 | \spawn monster 943 |
| 944 | - | accord | Human | 82563 | - | AlertAndLookAtPlayer | 0 | - | 0 | \spawn monster 944 |
| 945 | Vacationer | accord | Human | 82563 | - | AlertAndLookAtPlayer | 0 | - | 0 | \spawn monster 945 |
| 946 | Vacationer | accord | Human | 82563 | - | AlertAndLookAtPlayer | 0 | - | 0 | \spawn monster 946 |
| 947 | Copacabana Injured Accord Soldier - Pathing | accord | Human | 31568 | - | Arch_MedRangedHumanoid_Base | 0 | - | 0 | \spawn monster 947 |
| 948 | - | accord | Human | 82563 | - | PeacetimeCityWanderer | 0 | - | 0 | \spawn monster 948 |
| 949 | - | gaea | Wildlife | 85158 | 20046 / - | SuicideWanderer | 0 | 5724 / 5631 | 0 | \spawn monster 949 |
| 950 | - | gaea | Wildlife | 85156 | 20046 / - | - | 0 | 5724 / 5394 | 0 | \spawn monster 950 |
| 951 | - | accord | Human | 82579 | - | AlertAndInteractive | 0 | - | 0 | \spawn monster 951 |
| 952 | - | melding | Melded | 82629 | 34057 / 34048 | Done | 0 | 5313 / 5461 | 0 | \spawn monster 952 |
| 953 | Supply Officer Tomas | accord | Human | 82630 | - | AlertAndInteractive | 0 | - | 0 | \spawn monster 953 |
| 954 | Supply Officer McFinn | accord | Human | 82631 | - | AlertAndInteractive | 0 | - | 0 | \spawn monster 954 |
| 955 | - | gaea | Wildlife | 85157 | 20046 / - | BetaAranha | 0 | 5725 / 5632 | 0 | \spawn monster 955 |
| 956 | Supply Officer Cross | accord | Human | 82633 | - | AlertAndInteractive | 0 | - | 0 | \spawn monster 956 |
| 957 | Supply Officer Hooker | accord | Human | 82637 | - | AlertAndInteractive | 0 | - | 0 | \spawn monster 957 |
| 958 | Supply Officer "Jabs" | accord | Human | 82642 | - | AlertAndInteractive | 0 | - | 0 | \spawn monster 958 |
| 959 | Deimos | friendly | Companion | 82821 | - | - | 0 | - | 0 | \spawn monster 959 |
| 960 | Blackwater Anomaly | accord | Human | 0 | - | Null | 0 | - | 0 | \spawn monster 960 |
| 961 | Sergeant Choi | accord | Human | 84452 | - | - | 0 | - | 0 | \spawn monster 961 |
| 962 | - | accord | Human | 10004 | - / 76108 | - | 0 | - | 0 | \spawn monster 962 |
| 963 | Junk Yard Dog | gaea | Wildlife | 83728 | 86130 / - | Arch_FullbodyMelee_Base | 0 | - / 6211 | 0 | \spawn monster 963 |
| 964 | - | chosen | Chosen | 0 | - | Null | 0 | - | 0 | \spawn monster 964 |
| 965 | - | chosen | Chosen | 0 | - | Null | 0 | - | 0 | \spawn monster 965 |
| 966 | InfoBot | accord | Human | 85732 | - | PerformEmote | 0 | - | 0 | \spawn monster 966 |
| 967 | - | melding | Melded | 30032 | 76560 / - | AggressiveWanderer | 0 | - / 5460 | 0 | \spawn monster 967 |
| 968 | - | accord | Human | 81389 | - | AlertAndInteractive | 0 | - | 0 | \spawn monster 968 |
| 969 | Bandit Dreadnaught | bandit | Outlaw | 75115 | 75116 / - | - | 0 | 3 / 5388 | 5 | \spawn monster 969 |
| 970 | Elite Bandit Gunner | bandit | Outlaw | 84471 | 82387 / - | Arch_MedRangedHumanoid_Base | 0 | 3 / 5386 | 5 | \spawn monster 970 |
| 971 | - | accord | Human | 120676 | - / 82418 | Wander | 0 | - | 0 | \spawn monster 971 |
| 972 | - | accord | Human | 81392 | 84916 / - | GuardCityWanderer | 0 | - | 0 | \spawn monster 972 |
| 973 | Arcfold Security | accord | Human | 81392 | 84916 / - | GuardCityWanderer | 0 | - | 0 | \spawn monster 973 |
| 974 | Arcfold Security | accord | Human | 81392 | 84916 / - | GuardCityWanderer | 0 | - | 0 | \spawn monster 974 |
| 975 | Arcfold Engineer | accord | Human | 81373 | - | PeacetimeCityWanderer | 0 | - | 0 | \spawn monster 975 |
| 976 | Arcfold Engineer | accord | Human | 81373 | - | PeacetimeCityWanderer | 0 | - | 0 | \spawn monster 976 |
| 977 | Arcfold Security | accord | Human | 81392 | 84916 / - | GuardCityWanderer | 0 | - | 0 | \spawn monster 977 |
| 978 | Arcfold Security | accord | Human | 81392 | 84916 / - | GuardCityWanderer | 0 | - | 0 | \spawn monster 978 |
| 979 | Arcfold Security | accord | Human | 81392 | 84916 / - | GuardCityWanderer | 0 | - | 0 | \spawn monster 979 |
| 980 | Arcfold Security | accord | Human | 81392 | 84916 / - | GuardCityWanderer | 0 | - | 0 | \spawn monster 980 |
| 981 | - | accord | Human | 81391 | 84915 / - | GuardCityWanderer | 0 | - | 0 | \spawn monster 981 |
| 982 | - | accord | Human | 81391 | 84915 / - | GuardCityWanderer | 0 | - | 0 | \spawn monster 982 |
| 983 | - | accord | Human | 81391 | 84915 / - | GuardCityWanderer | 0 | - | 0 | \spawn monster 983 |
| 984 | - | accord | Human | 81392 | 84916 / - | GuardCityWanderer | 0 | - | 0 | \spawn monster 984 |
| 985 | - | accord | Human | 81392 | 84916 / - | GuardCityWanderer | 0 | - | 0 | \spawn monster 985 |
| 986 | - | accord | Human | 81392 | 84916 / - | GuardCityWanderer | 0 | - | 0 | \spawn monster 986 |
| 987 | Arcfold Engineer | accord | Human | 81373 | - | PeacetimeCityWanderer | 0 | - | 0 | \spawn monster 987 |
| 988 | Arcfold Engineer | accord | Human | 81373 | - | PeacetimeCityWanderer | 0 | - | 0 | \spawn monster 988 |
| 989 | - | gaea | Wildlife | 121437 | 86130 / - | Arch_AdditiveMelee_Base | 0 | 5704 / - | 0 | \spawn monster 989 |
| 990 | - | chosen | Chosen | 85137 | - | Wander | 0 | 1 / - | 0 | \spawn monster 990 |
| 991 | Melded Varant | melding | Melded | 85188 | 85165 / - | Arch_FullbodyMelee_Base | 0 | 5705 / 5460 | 5 | \spawn monster 991 |
| 992 | - | accord | Human | 85149 | - | Wander | 0 | 1 / - | 0 | \spawn monster 992 |
| 993 | - | accord | Human | 75115 | 78453 / - | ChosenJuggernaut | 0 | - | 0 | \spawn monster 993 |
| 994 | - | accord | Human | 85777 | - | AlertAndLookAtPlayer | 0 | - | 0 | \spawn monster 994 |
| 995 | - | accord | Human | 81367 | - | AlertAndLookAtPlayer | 0 | - | 0 | \spawn monster 995 |
| 996 | - | accord | Human | 81367 | - | AlertAndLookAtPlayer | 0 | - | 0 | \spawn monster 996 |
| 997 | - | accord | Human | 81367 | - | AlertAndLookAtPlayer | 0 | - | 0 | \spawn monster 997 |
| 998 | - | accord | Human | 81367 | - | AlertAndLookAtPlayer | 0 | - | 0 | \spawn monster 998 |
| 999 | - | accord | Human | 81367 | - | AlertAndLookAtPlayer | 0 | - | 0 | \spawn monster 999 |
| 1000 | - | accord | Human | 81367 | - | AlertAndLookAtPlayer | 0 | - | 0 | \spawn monster 1000 |
| 1001 | - | accord | Human | 81367 | - | AlertAndLookAtPlayer | 0 | - | 0 | \spawn monster 1001 |
| 1002 | - | accord | Human | 81367 | - | AlertAndLookAtPlayer | 0 | - | 0 | \spawn monster 1002 |
| 1003 | - | accord | Human | 81367 | - | AlertAndLookAtPlayer | 0 | - | 0 | \spawn monster 1003 |
| 1004 | - | friendly | Companion | 82821 | - | InteractiveWithEmote | 0 | - | 0 | \spawn monster 1004 |
| 1005 | - | accord | Human | 82563 | - | PeacetimeCityWanderer | 0 | - | 0 | \spawn monster 1005 |
| 1006 | Omnidyne-M Rep | accord | Human | 10001 | - / 30025 | - | 0 | - | 0 | \spawn monster 1006 |
| 1007 | - | accord | Human | 10001 | - / 30025 | - | 0 | - | 0 | \spawn monster 1007 |
| 1008 | - | neutral | Large wildlife | 81335 | - | FastWander | 0 | 4 / - | 0 | \spawn monster 1008 |
| 1009 | Wreck Scavenger Drone | neutral | Large wildlife | 85231 | - | FastWander | 0 | 4 / - | 0 | \spawn monster 1009 |
| 1010 | T.O.P. | friendly | Companion | 81353 | - | - | 0 | - | 0 | \spawn monster 1010 |
| 1011 | Rageclaw | gaea | Wildlife | 85340 | 85221 / 49118 | Arch_Relocator_Rageclaw_Base | 0 | 5705 / 5777 | 0 | \spawn monster 1011 |
| 1012 | Vorgoth | chosen | Chosen | 118844 | 86126 / - | Arch_MoveThenFire_Base | 0 | - | 5 | \spawn monster 1012 |
| 1013 | _Shock Trooper | chosen | Chosen | 85390 | 32739 / - | ChosenTrooper | 0 | 5698 / 5369 | 0 | \spawn monster 1013 |
| 1014 | - | accord | Human | 82563 | - | AlertAndLookAtPlayer | 0 | - | 0 | \spawn monster 1014 |
| 1015 | - | accord | Human | 82563 | - | BasicCivilian_Stationary | 0 | - | 0 | \spawn monster 1015 |
| 1016 | - | bandit | Outlaw | 75281 | 85421 / - | EliteWanderer | 0 | 5311 / 5386 | 0 | \spawn monster 1016 |
| 1018 | Jerrod Langley | accord | Human | 85502 | - | AlertAndInteractive | 0 | - | 0 | \spawn monster 1018 |
| 1019 | Brandon Reeve | accord | Human | 85502 | - | AlertAndInteractive | 0 | - | 0 | \spawn monster 1019 |
| 1020 | Samuel Burrell | accord | Human | 85502 | - | AlertAndInteractive | 0 | - | 0 | \spawn monster 1020 |
| 1021 | Pearl Terry | accord | Human | 85502 | - | AlertAndInteractive | 0 | - | 0 | \spawn monster 1021 |
| 1022 | Cyndi Gaye | accord | Human | 85502 | - | AlertAndInteractive | 0 | - | 0 | \spawn monster 1022 |
| 1023 | Alicia Breckinridge | accord | Human | 85502 | - | AlertAndInteractive | 0 | - | 0 | \spawn monster 1023 |
| 1024 | Tiny Tempest | friendly | Companion | 85543 | - | - | 0 | - | 0 | \spawn monster 1024 |
| 1025 | Anna Kristel | accord | Human | 85502 | - | AlertAndInteractive | 0 | - | 0 | \spawn monster 1025 |
| 1026 | - | gaea | Wildlife | 85618 | - | Wander | 0 | 1 / - | 0 | \spawn monster 1026 |
| 1027 | - | accord | Human | 85619 | - | AlertAndInteractive | 0 | - | 0 | \spawn monster 1027 |
| 1028 | - | gaea | Wildlife | 85620 | 33949 / - | ThresherCharger | 0 | 5704 / 5432 | 0 | \spawn monster 1028 |
| 1029 | Hisser Queen | gaea | Wildlife | 85621 | 97130 / - | - | 0 | 5 / 5408 | 0 | \spawn monster 1029 |
| 1030 | Hisser Broodling | gaea | Wildlife | 85622 | 30903 / - | SwarmWanderer | 0 | 5703 / 5403 | 0 | \spawn monster 1030 |
| 1031 | Chosen Guardian | chosen | Chosen | 85644 | 85645 / - | - | 0 | 6 / 5813 | 5 | \spawn monster 1031 |
| 1032 | Decoy NPC | accord | Human | 75773 | 85975 / 85984 | EliteWanderer | 0 | - | 0 | \spawn monster 1032 |
| 1033 | - | gaea | Wildlife | 85655 | 85662 / 85663 | FireSnake | 0 | 5725 / - | 0 | \spawn monster 1033 |
| 1034 | Doomstalker | gaea | Wildlife | 85656 | 85678 / - | Arch_MoveThenFire_Base | 0 | 5704 / - | 0 | \spawn monster 1034 |
| 1035 | - | accord | Human | 10003 | - | StandStill | 0 | - | 0 | \spawn monster 1035 |
| 1036 | - | accord | Human | 81381 | - | StandStill | 0 | - | 0 | \spawn monster 1036 |
| 1037 | - | accord | Human | 85666 | - | - | 0 | - | 0 | \spawn monster 1037 |
| 1038 | - | chosen | Chosen | 85670 | 32739 / - | ChosenTrooper | 0 | 5698 / 5369 | 0 | \spawn monster 1038 |
| 1039 | - | accord | Human | 85673 | 84917 / - | GuardCityWanderer | 0 | - | 0 | \spawn monster 1039 |
| 1040 | - | accord | Human | 85673 | 84917 / - | GuardCityWanderer | 0 | - | 0 | \spawn monster 1040 |
| 1041 | - | accord | Human | 85673 | 84917 / - | GuardCityWanderer | 0 | - | 0 | \spawn monster 1041 |
| 1042 | - | accord | Human | 85673 | 84917 / - | GuardCityWanderer | 0 | - | 0 | \spawn monster 1042 |
| 1043 | - | accord | Human | 85673 | 84917 / - | GuardCityWanderer | 0 | - | 0 | \spawn monster 1043 |
| 1044 | - | accord | Human | 85673 | 84917 / - | GuardCityWanderer | 0 | - | 0 | \spawn monster 1044 |
| 1045 | - | chosen | Chosen | 77699 | 77878 / - | TorturedSoulMelee | 0 | - | 0 | \spawn monster 1045 |
| 1046 | - | chosen | Chosen | 32740 | 32739 / - | ChosenTrooper | 0 | - | 0 | \spawn monster 1046 |
| 1047 | - | chosen | Chosen | 85684 | 85386 / - | StockMoveAndShootGuy | 0 | 5698 / 5369 | 0 | \spawn monster 1047 |
| 1048 | - | gaea | Wildlife | 85656 | 85661 / 85678 | _inst | 0 | 5724 / - | 0 | \spawn monster 1048 |
| 1049 | - | accord | Human | 85673 | 84917 / - | GuardCityWanderer | 0 | - | 0 | \spawn monster 1049 |
| 1050 | - | accord | Human | 85673 | 84917 / - | GuardCityWanderer | 0 | - | 0 | \spawn monster 1050 |
| 1051 | - | accord | Human | 85673 | 84917 / - | GuardCityWanderer | 0 | - | 0 | \spawn monster 1051 |
| 1052 | Adm. Curtis Mokiao | accord | Human | 106353 | - | AlertAndInteractive | 0 | - | 0 | \spawn monster 1052 |
| 1053 | Landing Pad Engineer | accord | Human | 85688 | - | PeacetimeCityWanderer | 0 | - | 0 | \spawn monster 1053 |
| 1054 | Landing Pad Engineer | accord | Human | 85688 | - | PeacetimeCityWanderer | 0 | - | 0 | \spawn monster 1054 |
| 1055 | Landing Pad Engineer | accord | Human | 85688 | - | PeacetimeCityWanderer | 0 | - | 0 | \spawn monster 1055 |
| 1056 | - | accord | Human | 85673 | 84917 / - | GuardCityWanderer | 0 | - | 0 | \spawn monster 1056 |
| 1057 | - | accord | Human | 85673 | 84917 / - | GuardCityWanderer | 0 | - | 0 | \spawn monster 1057 |
| 1058 | - | accord | Human | 85673 | 84917 / - | GuardCityWanderer | 0 | - | 0 | \spawn monster 1058 |
| 1059 | Oilspill | accord | Human | 85714 | 85728 / - | StockShootAndFollowRoute | 0 | - | 0 | \spawn monster 1059 |
| 1060 | Chosen Dropship Turret Gunner Main Cannon | chosen | Chosen | 32740 | 32848 / - | Null | 0 | - | 0 | \spawn monster 1060 |
| 1061 | Chosen Dropship Turret Gunner Small Side Cannon | chosen | Chosen | 32740 | 33979 / - | Null | 0 | - | 0 | \spawn monster 1061 |
| 1062 | Capt. Hudson Fuller | accord | Human | 82360 | 85735 / - | GuardCityWanderer | 0 | - | 0 | \spawn monster 1062 |
| 1063 | Outpost Heavy Turret Gunner | bandit | Outlaw | 75281 | 85731 / - | EliteWanderer | 0 | 5311 / 5386 | 0 | \spawn monster 1063 |
| 1064 | Target Drone - Friendly Invincible | friendly | Companion | 85732 | - | InteractiveWithEmote | 0 | - | 0 | \spawn monster 1064 |
| 1065 | - | chosen | Chosen | 32740 | 32739 / - | ChosenTrooper | 0 | 5698 / - | 0 | \spawn monster 1065 |
| 1066 | - | chosen | Chosen | 32742 | 85743 / - | Meta | 0 | 5698 / - | 0 | \spawn monster 1066 |
| 1067 | Statue NPC | accord | Human | 10004 | - / 76108 | - | 0 | - | 0 | \spawn monster 1067 |
| 1068 | - | chosen | Chosen | 77699 | 85745 / - | TorturedSoulMelee | 0 | 5696 / 5367 | 0 | \spawn monster 1068 |
| 1069 | - | chosen | Chosen | 31267 | 77049 / 32739 | ChosenSniper | 0 | 5698 / 5370 | 0 | \spawn monster 1069 |
| 1070 | - | accord | Human | 10001 | - / 30025 | - | 0 | - | 0 | \spawn monster 1070 |
| 1071 | - | chosen | Chosen | 85759 | 75681 / - | ChosenEngineerDrone | 0 | 5697 / 5615 | 0 | \spawn monster 1071 |
| 1173 | - | accord | Human | 0 | - | AggressiveWanderer | 0 | - | 0 | \spawn monster 1173 |
| 1174 | Accord Merit Quartermaster | accord | Human | 125033 | - | - | 0 | - | 0 | \spawn monster 1174 |
| 1175 | Accord Merit Quartermaster | accord | Human | 118075 | - | AlertAndInteractive | 0 | - | 0 | \spawn monster 1175 |
| 1176 | Accord Merit Quartermaster | accord | Human | 118075 | - | AlertAndInteractive | 0 | - | 0 | \spawn monster 1176 |
| 1177 | Accord Merit Quartermaster | accord | Human | 118075 | - | AlertAndInteractive | 0 | - | 0 | \spawn monster 1177 |
| 1178 | - | chosen | Chosen | 85823 | - | - | 0 | 5698 / 5369 | 0 | \spawn monster 1178 |
| 1179 | Akreatrix the Untame | gaea | Wildlife | 85836 | 137035 / - | - | 0 | 5705 / 5415 | 5 | \spawn monster 1179 |
| 1180 | Wounded Bandit | bandit | Outlaw | 85880 | - | ChainWithPush | 0 | 5311 / 5386 | 0 | \spawn monster 1180 |
| 1181 | Big Brother | bandit | Outlaw | 85841 | 81396 / 82354 | RaiderBaronMiniBoss | 0 | 5313 / 5388 | 5 | \spawn monster 1181 |
| 1182 | Blood Kings Commander | bandit | Outlaw | 86147 | 97434 / - | - | 0 | 3 / 5388 | 5 | \spawn monster 1182 |
| 1183 | - | neutral | Large wildlife | 85896 | - | FastWanderCore | 0 | - | 0 | \spawn monster 1183 |
| 1184 | Small Crystite Aranha Worker | gaea | Wildlife | 85890 | 85898 / - | - | 0 | 5703 / 5394 | 0 | \spawn monster 1184 |
| 1185 | - | bandit | Outlaw | 77171 | 77338 / - | SiegebreakerWithCharge | 0 | 5311 / 5378 | 0 | \spawn monster 1185 |
| 1186 | Melded Wargrim | melding | Melded | 85897 | 66846 / - | FeralCanineSquad | 0 | 5459 / - | 0 | \spawn monster 1186 |
| 1187 | Kalidor the Giant | gaea | Wildlife | 81311 | 142169 / - | - | 0 | 5706 / 5396 | 5 | \spawn monster 1187 |
| 1188 | Vomica the Noxious | gaea | Wildlife | 81337 | 81402 / - | LandSharkMiniBoss | 0 | 5706 / - | 5 | \spawn monster 1188 |
| 1189 | Culex Swarmer | gaea | Wildlife | 88174 | 20046 / - | - | 0 | 5704 / 5414 | 5 | \spawn monster 1189 |
| 1190 | Vic the Crow | accord | Human | 96941 | - | AlertAndInteractive | 0 | - | 0 | \spawn monster 1190 |
| 1191 | - | neutral | Large wildlife | 85896 | - | FastWanderCore | 0 | - | 0 | \spawn monster 1191 |
| 1192 | L-98 | monster | Misc | 85896 | - | FastWanderCore | 0 | - | 0 | \spawn monster 1192 |
| 1193 | - | accord | Human | 81323 | - | AlertAndInteractive | 0 | - | 0 | \spawn monster 1193 |
| 1194 | Rico | accord | Human | 85686 | 85687 / - | GuardCityWanderer | 0 | - | 0 | \spawn monster 1194 |
| 1195 | Accord Dropship Pilot | accord | Human | 85686 | 85687 / - | GuardCityWanderer | 0 | - | 0 | \spawn monster 1195 |
| 1196 | Chosen Fiend | chosen | Chosen | 125065 | 85953 / - | - | 0 | 5697 / 5368 | 5 | \spawn monster 1196 |
| 1197 | Goliath | gaea | Wildlife | 85962 | 82663 / - | Arch_FullbodyMelee_Base | 0 | - / 6213 | 5 | \spawn monster 1197 |
| 1198 | Crystite Aranha Sieger | gaea | Wildlife | 85963 | 33830 / 30808 | - | 0 | 5704 / 5397 | 0 | \spawn monster 1198 |
| 1199 | - | accord | Human | 81378 | - | PeacetimeCityWanderer | 0 | - | 0 | \spawn monster 1199 |
| 1200 | - | chosen | Chosen | 32740 | 32739 / - | Arch_MedRangedHumanoid_Base | 0 | 5698 / 5369 | 0 | \spawn monster 1200 |
| 1201 | Massive Culex | gaea | Wildlife | 86043 | 137035 / - | - | 0 | 5705 / 5415 | 5 | \spawn monster 1201 |
| 1202 | - | melding | Melded | 30145 | 85189 / 85190 | MeldingWyrm | 0 | 5726 / 5460 | 0 | \spawn monster 1202 |
| 1203 | Campaign 5 Power Grab Turret Gunner | chosen | Chosen | 30036 | 30025 / 20015 | PlayerPet | 0 | - | 5 | \spawn monster 1203 |
| 1204 | - | chosen | Chosen | 86124 | 32739 / 33064 | - | 0 | 3 / 5698 | 0 | \spawn monster 1204 |
| 1205 | Devilhawk Paratrooper | accord | Human | 86127 | 96666 / - | Arch_MedRangedHumanoid_Base | 0 | 5311 / 5387 | 5 | \spawn monster 1205 |
| 1206 | Rico | accord | Human | 10004 | - / 76108 | - | 0 | - | 0 | \spawn monster 1206 |
| 1207 | Joe | accord | Human | 10001 | - / 76108 | - | 0 | - | 0 | \spawn monster 1207 |
| 1208 | Mara | accord | Human | 10001 | - / 76108 | - | 0 | - | 0 | \spawn monster 1208 |
| 1209 | Hobbes | accord | Human | 86129 | 30688 / - | Arch_MedRangedHumanoid_Base | 0 | 5311 / 5387 | 5 | \spawn monster 1209 |
| 1213 | - | chosen | Chosen | 77732 | 77760 / - | Arch_MedRangedAbilityUser_Base | 0 | 3 / 5813 | 0 | \spawn monster 1213 |
| 1214 | Accord Officer | accord | Human | 86152 | - | Stand | 0 | - | 0 | \spawn monster 1214 |
| 1215 | Accord Officer | accord | Human | 86152 | - | Stand | 0 | - | 0 | \spawn monster 1215 |
| 1216 | Accord Officer | accord | Human | 86152 | - | Stand | 0 | - | 0 | \spawn monster 1216 |
| 1217 | Accord Officer | accord | Human | 86152 | - | Stand | 0 | - | 0 | \spawn monster 1217 |
| 1218 | Sgt. Torres | accord | Human | 86153 | - | StockShootAndFollowRoute | 0 | - | 0 | \spawn monster 1218 |
| 1219 | - | chosen | Chosen | 77732 | 77760 / - | Arch_MedRangedAbilityUser_Base | 0 | 3 / 5813 | 0 | \spawn monster 1219 |
| 1220 | - | chosen | Chosen | 33958 | 32741 / - | Arch_MedRangedHumanoid_Base | 0 | 5699 / 5370 | 0 | \spawn monster 1220 |
| 1221 | Rivers | accord | Human | 86153 | 79066 / - | Arch_MedRangedHumanoid_Base | 0 | - | 0 | \spawn monster 1221 |
| 1222 | - | neutral | Large wildlife | 85686 | 85687 / - | GuardCityWanderer | 0 | - | 0 | \spawn monster 1222 |
| 1223 | Horus | accord | Human | 86524 | 85687 / - | Arch_MedRangedHumanoid_Base | 0 | - | 5 | \spawn monster 1223 |
| 1224 | - | bandit | Outlaw | 86173 | 86174 / - | EliteWanderer | 0 | 1 / 5385 | 0 | \spawn monster 1224 |
| 1225 | Putrid Carcass | chosen | Chosen | 86224 | 86197 / - | Arch_MoveThenFire_Base | 0 | 5698 / 5459 | 5 | \spawn monster 1225 |
| 1226 | Elite Blood King Assault | bandit | Outlaw | 86344 | 122968 / - | - | 0 | 5313 / 3 | 5 | \spawn monster 1226 |
| 1227 | Elite Blood King Dreadnaught | bandit | Outlaw | 86375 | 85386 / - | Arch_MoveThenFire_Base | 0 | 5314 / - | 0 | \spawn monster 1227 |
| 1228 | Blood King Assault | bandit | Outlaw | 86376 | 81308 / - | Arch_MedRangedHumanoid_Base | 0 | 5311 / - | 0 | \spawn monster 1228 |
| 1229 | Blood King Biotech | bandit | Outlaw | 96840 | 96839 / - | Arch_MedRangedHumanoid_Base | 0 | 5311 / - | 0 | \spawn monster 1229 |
| 1230 | Blood King Assault | bandit | Outlaw | 86376 | 122968 / - | - | 0 | 5312 / - | 0 | \spawn monster 1230 |
| 1231 | Blood King Trooper | bandit | Outlaw | 114639 | 96838 / - | - | 0 | 5312 / - | 0 | \spawn monster 1231 |
| 1232 | Blood King Soldier | bandit | Outlaw | 114425 | 96847 / - | - | 0 | 5311 / - | 0 | \spawn monster 1232 |
| 1233 | - | bandit | Outlaw | 86377 | 75116 / - | Arch_MedRangedHumanoid_Base | 0 | 5311 / - | 0 | \spawn monster 1233 |
| 1234 | - | bandit | Outlaw | 86377 | 75116 / - | Arch_MedRangedHumanoid_Base | 0 | 5311 / - | 0 | \spawn monster 1234 |
| 1235 | - | bandit | Outlaw | 86377 | 75116 / - | Arch_MedRangedHumanoid_Base | 0 | 5311 / - | 0 | \spawn monster 1235 |
| 1236 | Blood King Dreadnaught | bandit | Outlaw | 86377 | 106335 / - | - | 0 | 5313 / - | 0 | \spawn monster 1236 |
| 1237 | - | bandit | Outlaw | 86377 | 75116 / - | Arch_MedRangedHumanoid_Base | 0 | 5311 / - | 0 | \spawn monster 1237 |
| 1238 | Blood King Sniper | bandit | Outlaw | 86378 | 96871 / 96842 | - | 0 | 5310 / - | 0 | \spawn monster 1238 |
| 1239 | - | bandit | Outlaw | 86378 | 77049 / - | Arch_MedRangedHumanoid_Base | 0 | 5311 / - | 0 | \spawn monster 1239 |
| 1240 | - | bandit | Outlaw | 86378 | 77049 / - | Arch_MedRangedHumanoid_Base | 0 | 5311 / - | 0 | \spawn monster 1240 |
| 1241 | - | bandit | Outlaw | 86378 | 67425 / - | Arch_MedRangedHumanoid_Base | 0 | 5311 / - | 0 | \spawn monster 1241 |
| 1242 | - | bandit | Outlaw | 86378 | 67425 / - | Arch_MedRangedHumanoid_Base | 0 | 5311 / - | 0 | \spawn monster 1242 |
| 1243 | - | bandit | Outlaw | 86379 | - / 77668 | Arch_MedRangedHumanoid_Base | 0 | 5311 / - | 0 | \spawn monster 1243 |
| 1244 | - | bandit | Outlaw | 86379 | - / 77668 | Arch_MedRangedHumanoid_Base | 0 | 5311 / - | 0 | \spawn monster 1244 |
| 1245 | - | bandit | Outlaw | 86379 | - / 67424 | Arch_MedRangedHumanoid_Base | 0 | 5311 / - | 0 | \spawn monster 1245 |
| 1246 | - | bandit | Outlaw | 86379 | 67424 / - | Arch_MedRangedHumanoid_Base | 0 | 5311 / - | 0 | \spawn monster 1246 |
| 1247 | - | bandit | Outlaw | 86379 | 67424 / - | Arch_MedRangedHumanoid_Base | 0 | 5311 / - | 0 | \spawn monster 1247 |
| 1248 | Boss Singh | Black Hills Bandits | Outlaw | 86277 | 86280 / - | - | 0 | 2 / 5386 | 5 | \spawn monster 1248 |
| 1249 | _Oilspill's Dropship Engineer Drone | friendly | Companion | 82821 | - | UseWorkDeployables | 0 | - | 0 | \spawn monster 1249 |
| 1250 | Frank | accord | Human | 86395 | - / 77338 | Arch_MedRangedHumanoid_Base | 0 | - | 5 | \spawn monster 1250 |
| 1251 | Oilspill | accord | Human | 77437 | - | AlertAndInteractive | 0 | - | 0 | \spawn monster 1251 |
| 1252 | Pirate Santa | bandit | Outlaw | 77171 | 77338 / - | SiegebreakerWithCharge | 0 | 5312 / 5379 | 0 | \spawn monster 1252 |
| 1253 | Father Wintertide | accord | Outlaw | 121273 | 81396 / 77338 | RaiderBaronMiniBoss | 0 | 5313 / 5388 | 0 | \spawn monster 1253 |
| 1254 | - | gaea | Wildlife | 66853 | 33949 / - | ThresherCharger | 0 | 5704 / 5432 | 0 | \spawn monster 1254 |
| 1255 | - | accord | Human | 86380 | 76108 / - | GuardCityWanderer | 0 | - | 0 | \spawn monster 1255 |
| 1256 | Chiba | accord | Human | 10004 | - / 76108 | AlertAndInteractive | 0 | - | 5 | \spawn monster 1256 |
| 1257 | Fade | accord | Human | 86733 | 77049 / - | Arch_MedRangedHumanoid_Base | 0 | 2 / - | 0 | \spawn monster 1257 |
| 1258 | Alpha Trapjaw | melding | Melded | 86391 | 85474 / - | Arch_FullbodyMelee_Base | 0 | 5700 / 5460 | 5 | \spawn monster 1258 |
| 1259 | - | accord | Human | 81381 | - | PeacetimeCityWanderer | 0 | - | 0 | \spawn monster 1259 |
| 1260 | - | accord | Human | 86495 | - | Arch_AdditiveMelee_Base | 0 | - | 0 | \spawn monster 1260 |
| 1261 | - | accord | Human | 81381 | - | PeacetimeCityWanderer | 0 | - | 0 | \spawn monster 1261 |
| 1262 | - | accord | Human | 81383 | - | PeacetimeCityWanderer | 0 | - | 0 | \spawn monster 1262 |
| 1263 | - | accord | Human | 10002 | - / 76108 | Arch_MedRangedAbilityUser_Base | 0 | - | 0 | \spawn monster 1263 |
| 1264 | Depreciate - Experiment gone bad | monster | Misc | 77105 | 77268 / 77248 | Squawwk | 0 | 5704 / - | 0 | \spawn monster 1264 |
| 1265 | - | accord | Human | 86494 | - | PeacetimeCityWanderer | 0 | - | 0 | \spawn monster 1265 |
| 1266 | - | accord | Human | 81381 | - | PeacetimeCityWanderer | 0 | - | 0 | \spawn monster 1266 |
| 1267 | Mustang | accord | Human | 81332 | 85226 / - | AlertAndInteractive | 0 | - | 0 | \spawn monster 1267 |
| 1268 | Lieutenant Sharpe | accord | Human | 75773 | - / 30025 | - | 0 | - | 0 | \spawn monster 1268 |
| 1269 | - | accord | Human | 52457 | - | StationaryCivilianDialog | 0 | - | 0 | \spawn monster 1269 |
| 1270 | - | accord | Human | 78971 | - | AlertAndInteractive | 0 | - | 0 | \spawn monster 1270 |
| 1271 | - | gaea | Wildlife | 86485 | - | Config | 0 | - | 0 | \spawn monster 1271 |
| 1272 | - | accord | Human | 86490 | - | - | 0 | - | 1000 | \spawn monster 1272 |
| 1273 | - | accord | Human | 118219 | - | AlertAndInteractive | 0 | - | 0 | \spawn monster 1273 |
| 1274 | - | accord | Human | 10004 | - / 86510 | FireBurst | 0 | - | 0 | \spawn monster 1274 |
| 1275 | - | accord | Human | 10001 | - / 86520 | AlertAndInteractive | 0 | - | 0 | \spawn monster 1275 |
| 1276 | - | accord | Human | 56723 | - / 86509 | FaceAndAttack | 0 | - | 0 | \spawn monster 1276 |
| 1277 | - | accord | Human | 10002 | - / 85972 | AlertAndInteractive | 0 | - | 0 | \spawn monster 1277 |
| 1278 | - | accord | Human | 10003 | - / 76108 | AlertAndInteractive | 0 | - | 0 | \spawn monster 1278 |
| 1279 | - | accord | Human | 10003 | - / 76108 | Arch_MedRangedHumanoid_Base | 0 | - | 0 | \spawn monster 1279 |
| 1280 | - | accord | Human | 10004 | - / 76108 | Arch_MedRangedHumanoid_Base | 0 | - | 0 | \spawn monster 1280 |
| 1281 | - | friendly | Companion | 93709 | - | - | 0 | - | 0 | \spawn monster 1281 |
| 1282 | - | friendly | Companion | 93709 | - | - | 0 | - | 0 | \spawn monster 1282 |
| 1283 | - | chosen | Chosen | 85389 | 56826 / - | Arch_MoveThenFire_Base | 0 | 5700 / 5371 | 0 | \spawn monster 1283 |
| 1284 | - | accord | Human | 10001 | 84432 / 20016 | Arch_MedRangedTwoGuns_Base | 0 | 5312 / 5379 | 0 | \spawn monster 1284 |
| 1285 | - | bandit | Outlaw | 10001 | - / 76108 | Arch_MedRangedHumanoid_Base | 0 | - | 0 | \spawn monster 1285 |
| 1286 | - | accord | Human | 56723 | - / 86509 | FaceAndAttack | 0 | - | 0 | \spawn monster 1286 |
| 1287 | - | accord | Human | 10004 | - / 86510 | FireBurst | 0 | - | 0 | \spawn monster 1287 |
| 1288 | - | accord | Human | 10001 | - / 86520 | AlertAndInteractive | 0 | - | 0 | \spawn monster 1288 |
| 1289 | - | accord | Human | 56723 | - / 86509 | FaceAndAttack | 0 | - | 0 | \spawn monster 1289 |
| 1290 | - | accord | Human | 10002 | - / 85972 | AlertAndInteractive | 0 | - | 0 | \spawn monster 1290 |
| 1291 | - | accord | Human | 10003 | - / 76108 | AlertAndInteractive | 0 | - | 0 | \spawn monster 1291 |
| 1292 | Skull Burster | bandit | Outlaw | 77169 | 77335 / - | EliteWanderer | 0 | 5311 / 5378 | 0 | \spawn monster 1292 |
| 1293 | Skull Grenadier | bandit | Outlaw | 77169 | 77336 / - | EliteWanderer | 0 | 5311 / 5378 | 0 | \spawn monster 1293 |
| 1294 | Skull Gunner | bandit | Outlaw | 77169 | 77337 / - | EliteWanderer | 0 | 5311 / 5378 | 0 | \spawn monster 1294 |
| 1295 | Skull Assault | bandit | Outlaw | 77170 | 77339 / - | EliteWanderer | 0 | 5311 / 6113 | 0 | \spawn monster 1295 |
| 1296 | Skull Dreadnaught | bandit | Outlaw | 77171 | 77338 / - | SiegebreakerWithCharge | 0 | 5312 / 6114 | 0 | \spawn monster 1296 |
| 1297 | - | bandit | Outlaw | 75281 | - / 67423 | Arch_MedRangedHumanoid_Base | 0 | 5311 / 5386 | 0 | \spawn monster 1297 |
| 1298 | Rockhorn Bull | gaea | Wildlife | 86531 | 33949 / - | Arch_Charger_Base | 0 | 5704 / - | 5 | \spawn monster 1298 |
| 1299 | - | accord | Human | 10004 | - / 86510 | AlertAndInteractive | 0 | - | 0 | \spawn monster 1299 |
| 1300 | - | accord | Human | 10001 | - / 86520 | AlertAndInteractive | 0 | - | 0 | \spawn monster 1300 |
| 1301 | - | accord | Human | 56723 | - / 86509 | AlertAndInteractive | 0 | - | 0 | \spawn monster 1301 |
| 1302 | - | accord | Human | 10002 | - / 85972 | AlertAndInteractive | 0 | - | 0 | \spawn monster 1302 |
| 1303 | - | accord | Human | 10003 | - / 76108 | AlertAndInteractive | 0 | - | 0 | \spawn monster 1303 |
| 1304 | Black Hills Bandit | Black Hills Bandits | Outlaw | 125065 | 96842 / - | - | 0 | 5310 / 5386 | 0 | \spawn monster 1304 |
| 1305 | Chosen Shield Drone | chosen | Chosen | 86537 | 75681 / - | ChosenEngineerDrone | 0 | 5697 / 5615 | 5 | \spawn monster 1305 |
| 1306 | The Kodiak | chosen | Chosen | 0 | - | Null | 0 | - | 0 | \spawn monster 1306 |
| 1307 | - | accord | Human | 85686 | 85687 / - | GuardCityWanderer | 0 | - | 0 | \spawn monster 1307 |
| 1313 | Hell Slinger | gaea | Wildlife | 86539 | 86636 / - | - | 0 | 5705 / - | 5 | \spawn monster 1313 |
| 1314 | - | neutral | Large wildlife | 86540 | 77093 / 77140 | Brontodon | 0 | 5337 / 5441 | 0 | \spawn monster 1314 |
| 1315 | - | accord | Human | 10001 | - / 76108 | - | 0 | - | 0 | \spawn monster 1315 |
| 1316 | - | bandit | Outlaw | 75281 | - / 67423 | Arch_MedRangedHumanoid_Base | 0 | 5311 / 5386 | 0 | \spawn monster 1316 |
| 1317 | - | friendly | Companion | 86597 | - | PassivePet | 0 | - | 0 | \spawn monster 1317 |
| 1318 | BattleCruiser Turret Gunner Main Cannon | accord | Human | 32740 | 32848 / - | TurretTeleporterDropshipCannon | 0 | - | 0 | \spawn monster 1318 |
| 1319 | Firewhip | gaea | Wildlife | 86602 | 110770 / - | - | 0 | 5703 / - | 0 | \spawn monster 1319 |
| 1320 | - | accord | Human | 86607 | - | PeacetimeCityWanderer | 0 | - | 0 | \spawn monster 1320 |
| 1321 | Science OFC Nakamura | accord | Human | 96808 | - | AlertAndInteractive | 0 | - | 0 | \spawn monster 1321 |
| 1322 | - | bandit | Outlaw | 86173 | - | StationaryCivilianDialog | 0 | - | 0 | \spawn monster 1322 |
| 1323 | - | accord | Human | 78971 | - | AlertAndInteractive | 0 | - | 0 | \spawn monster 1323 |
| 1324 | Courier Alpha | accord | Human | 117999 | - | - | 0 | - | 0 | \spawn monster 1324 |
| 1325 | - | accord | Human | 81378 | - | - | 0 | - | 0 | \spawn monster 1325 |
| 1326 | Accord Scientist Team Leader | accord | Human | 117985 | - | AlertAndInteractive | 0 | - | 0 | \spawn monster 1326 |
| 1327 | - | neutral | Large wildlife | 86606 | - | Wander | 0 | 1 / - | 0 | \spawn monster 1327 |
| 1328 | - | neutral | Large wildlife | 88153 | - / 78301 | - | 0 | - | 0 | \spawn monster 1328 |
| 1329 | - | neutral | Large wildlife | 86129 | 30688 / - | - | 0 | 5311 / 5387 | 0 | \spawn monster 1329 |
| 1330 | Slingshot | neutral | Large wildlife | 86659 | - | - | 0 | - | 0 | \spawn monster 1330 |
| 1331 | - | monster | Misc | 86608 | - | FastWanderCore | 0 | - | 0 | \spawn monster 1331 |
| 1332 | Accord Quartermaster | accord | Human | 125033 | - | - | 0 | - | 0 | \spawn monster 1332 |
| 1333 | Accord Quartermaster | accord | Human | 125033 | - | - | 0 | - | 0 | \spawn monster 1333 |
| 1334 | Accord Quartermaster | accord | Human | 125033 | - | - | 0 | - | 0 | \spawn monster 1334 |
| 1335 | Accord Quartermaster | accord | Human | 125033 | - | - | 0 | - | 0 | \spawn monster 1335 |
| 1336 | - | accord | Human | 85673 | 84917 / - | GuardCityWanderer | 0 | - | 0 | \spawn monster 1336 |
| 1337 | - | accord | Human | 85673 | 84917 / - | GuardCityWanderer | 0 | - | 0 | \spawn monster 1337 |
| 1338 | - | accord | Human | 85673 | 84917 / - | GuardCityWanderer | 0 | - | 0 | \spawn monster 1338 |
| 1339 | - | accord | Human | 85673 | 84917 / - | GuardCityWanderer | 0 | - | 0 | \spawn monster 1339 |
| 1340 | - | accord | Human | 85673 | 84917 / - | GuardCityWanderer | 0 | - | 0 | \spawn monster 1340 |
| 1341 | - | accord | Human | 85673 | 84917 / - | GuardCityWanderer | 0 | - | 0 | \spawn monster 1341 |
| 1342 | - | accord | Human | 85673 | 84917 / - | GuardCityWanderer | 0 | - | 0 | \spawn monster 1342 |
| 1343 | - | accord | Human | 85673 | 84917 / - | GuardCityWanderer | 0 | - | 0 | \spawn monster 1343 |
| 1344 | - | accord | Human | 85673 | 84917 / - | GuardCityWanderer | 0 | - | 0 | \spawn monster 1344 |
| 1345 | - | accord | Human | 85673 | 84917 / - | GuardCityWanderer | 0 | - | 0 | \spawn monster 1345 |
| 1346 | - | accord | Human | 85673 | 84917 / - | GuardCityWanderer | 0 | - | 0 | \spawn monster 1346 |
| 1347 | - | accord | Human | 85673 | 84917 / - | GuardCityWanderer | 0 | - | 0 | \spawn monster 1347 |
| 1348 | - | accord | Human | 85673 | 84917 / - | GuardCityWanderer | 0 | - | 0 | \spawn monster 1348 |
| 1349 | - | accord | Human | 85673 | 84917 / - | GuardCityWanderer | 0 | - | 0 | \spawn monster 1349 |
| 1350 | - | accord | Human | 86153 | - | StockShootAndFollowRoute | 0 | - | 0 | \spawn monster 1350 |
| 1351 | - | accord | Human | 81329 | - | AlertAndInteractive | 0 | - | 0 | \spawn monster 1351 |
| 1352 | Dropship Pilot | accord | Human | 86619 | 78496 / - | Arch_MedRangedHumanoid_Base | 0 | - | 0 | \spawn monster 1352 |
| 1353 | Oilspill | accord | Human | 77437 | - | AlertAndInteractive | 0 | - | 0 | \spawn monster 1353 |
| 1354 | Vic the Crow | accord | Human | 96941 | - | AlertAndInteractive | 0 | - | 0 | \spawn monster 1354 |
| 1355 | The Ringer | accord | Human | 82563 | - | AlertAndInteractive | 0 | - | 0 | \spawn monster 1355 |
| 1356 | Gorou Kagame | bandit | Outlaw | 142074 | 139891 / - | - | 10005 | 5313 / 5389 | 0 | \spawn monster 1356 |
| 1357 | The Indexer | accord | Human | 106349 | - | AlertAndInteractive | 0 | - | 0 | \spawn monster 1357 |
| 1358 | Dexter Greer | accord | Human | 97262 | - | AlertAndInteractive | 0 | - | 0 | \spawn monster 1358 |
| 1359 | The Meddler | accord | Human | 52455 | - / 88303 | PeacetimeCityWanderer | 0 | - | 0 | \spawn monster 1359 |
| 1360 | - | friendly | Companion | 86629 | 20038 / - | TestFollowPlayer | 0 | - | 0 | \spawn monster 1360 |
| 1361 | Ol' Man Bill | accord | Human | 86630 | - | AlertAndInteractive | 0 | - | 0 | \spawn monster 1361 |
| 1362 | Hawking | accord | Human | 86631 | - | AlertAndInteractive | 0 | - | 0 | \spawn monster 1362 |
| 1363 | Atlas | accord | Human | 86632 | - | AlertAndInteractive | 0 | - | 0 | \spawn monster 1363 |
| 1364 | - | bandit | Outlaw | 86277 | 86280 / - | Arch_MedRangedHumanoid_Base | 0 | 2 / 5386 | 0 | \spawn monster 1364 |
| 1365 | Elite Shock Trooper | chosen | Chosen | 86644 | 86643 / - | Arch_MedRangedHumanoid_Base | 0 | 5698 / 5369 | 5 | \spawn monster 1365 |
| 1366 | - | chosen | Chosen | 86658 | 86662 / - | Arch_MoveThenFire_Base | 0 | 5698 / 5369 | 0 | \spawn monster 1366 |
| 1367 | - | chosen | Chosen | 86665 | - | Arch_Kamikaze_Base | 0 | 5698 / 5369 | 0 | \spawn monster 1367 |
| 1368 | Doctor Abrams | accord | Human | 81326 | - | AlertAndInteractive | 0 | - | 0 | \spawn monster 1368 |
| 1369 | Sarah | accord | Human | 81378 | - | PeacetimeCityWanderer | 0 | - | 0 | \spawn monster 1369 |
| 1370 | Derkas | accord | Human | 92801 | 78065 / - | GuardCityWanderer | 0 | - | 0 | \spawn monster 1370 |
| 1371 | Sergeant Wilcox | accord | Human | 96810 | - | AlertAndInteractive | 0 | - | 0 | \spawn monster 1371 |
| 1372 | Accord Coroner | accord | Human | 81372 | - | PeacetimeCityWanderer | 0 | - | 0 | \spawn monster 1372 |
| 1373 | - | accord | Human | 52458 | - | - | 0 | - | 0 | \spawn monster 1373 |
| 1374 | Sergeant Lewis | accord | Human | 81392 | - | AlertAndInteractive | 0 | - | 0 | \spawn monster 1374 |
| 1375 | Chosen Obliterator | chosen | Chosen | 125034 | 87382 / - | - | 0 | 5700 / 5371 | 5 | \spawn monster 1375 |
| 1376 | - | chosen | Chosen | 87385 | 88152 / - | Arch_AdditiveMelee_Base | 0 | 5698 / 5369 | 5 | \spawn monster 1376 |
| 1377 | Elite Chosen Chaingunner | chosen | Chosen | 88154 | 88155 / - | Arch_MoveThenFire_Base | 0 | 5700 / 5371 | 5 | \spawn monster 1377 |
| 1378 | - | chosen | Chosen | 88157 | 88152 / - | Arch_AdditiveMelee_Base | 0 | 5698 / 5369 | 5 | \spawn monster 1378 |
| 1379 | - | chosen | Chosen | 88158 | - | Arch_MedRangedAbilityUser_Base | 0 | 5700 / 5371 | 5 | \spawn monster 1379 |
| 1380 | Dronificus | friendly | Companion | 82821 | - | - | 0 | - | 0 | \spawn monster 1380 |
| 1381 | - | chosen | Chosen | 88158 | - | Arch_MedRangedAbilityUser_Base | 0 | 5700 / 5371 | 5 | \spawn monster 1381 |
| 1382 | - | chosen | Chosen | 88158 | - | Arch_MedRangedAbilityUser_Base | 0 | 5700 / 5371 | 5 | \spawn monster 1382 |
| 1383 | - | chosen | Chosen | 88158 | - | Arch_MedRangedAbilityUser_Base | 0 | 5700 / 5371 | 5 | \spawn monster 1383 |
| 1384 | - | chosen | Chosen | 88158 | - | Arch_MedRangedAbilityUser_Base | 0 | 5700 / 5371 | 5 | \spawn monster 1384 |
| 1385 | Chosen Archon | chosen | Chosen | 121314 | 121374 / - | - | 0 | 5700 / 5371 | 5 | \spawn monster 1385 |
| 1386 | - | chosen | Chosen | 88158 | - | Arch_MedRangedAbilityUser_Base | 0 | 5700 / 5371 | 5 | \spawn monster 1386 |
| 1387 | - | chosen | Chosen | 88158 | - | Arch_MedRangedAbilityUser_Base | 0 | 5700 / 5371 | 5 | \spawn monster 1387 |
| 1388 | - | chosen | Chosen | 88158 | - | Arch_MedRangedAbilityUser_Base | 0 | 5700 / 5371 | 5 | \spawn monster 1388 |
| 1389 | Capt. Patel | accord | Human | 118031 | - | AlertAndInteractive | 0 | - | 0 | \spawn monster 1389 |
| 1390 | Smuggler | bandit | Outlaw | 75281 | - | StationaryCivilianDialog | 0 | - | 0 | \spawn monster 1390 |
| 1391 | Accord Soldier | accord | Human | 10003 | - / 96847 | - | 0 | - | 0 | \spawn monster 1391 |
| 1392 | Cerrado Norte Bandit | bandit | Outlaw | 75281 | 67425 / - | Arch_MedRangedHumanoid_Base | 0 | - | 0 | \spawn monster 1392 |
| 1393 | Sergeant Kang | bandit | Outlaw | 118035 | 86280 / - | Arch_MedRangedHumanoid_Base | 0 | 2 / 5386 | 5 | \spawn monster 1393 |
| 1394 | Captain Abrams | accord | Outlaw | 114629 | - | AlertAndInteractive | 0 | - | 0 | \spawn monster 1394 |
| 1395 | Accord Guard | accord | Human | 77858 | 78495 / - | - | 0 | - | 5 | \spawn monster 1395 |
| 1396 | - | accord | Human | 10001 | - / 76108 | Arch_MedRangedHumanoid_Base | 0 | - | 0 | \spawn monster 1396 |
| 1397 | Accord Medical Officer | - | Human | 97262 | - | - | 0 | - | 0 | \spawn monster 1397 |
| 1398 | Wiley | friendly | Companion | 118000 | - | StationaryCivilianDialog | 0 | - | 0 | \spawn monster 1398 |
| 1399 | - | chosen | Chosen | 32740 | 32739 / 114337 | - | 0 | 5698 / 5369 | 5 | \spawn monster 1399 |
| 1400 | - | gaea | Wildlife | 94159 | 82619 / 82663 | - | 0 | 5725 / 6212 | 5 | \spawn monster 1400 |
| 1401 | Bark Slinger | gaea | Wildlife | 86539 | 92856 / - | - | 0 | 5705 / - | 5 | \spawn monster 1401 |
| 1402 | - | gaea | Wildlife | 92854 | 85661 / - | - | 0 | 5725 / - | 0 | \spawn monster 1402 |
| 1403 | Harpy | gaea | Wildlife | 96483 | 96576 / - | - | 0 | 5704 / - | 5 | \spawn monster 1403 |
| 1404 | Centauri Tick | gaea | Wildlife | 122824 | 122825 / - | - | 0 | 5703 / - | 5 | \spawn monster 1404 |
| 1405 | - | accord | Human | 85619 | - | - | 0 | - | 0 | \spawn monster 1405 |
| 1406 | Convoy Driver | accord | Human | 52462 | - | StationaryCivilianDialog | 0 | - | 0 | \spawn monster 1406 |
| 1407 | Arturs the Mechanic | accord | Human | 97027 | - | AlertAndInteractive | 0 | - | 0 | \spawn monster 1407 |
| 1408 | - | bandit | Outlaw | 75281 | - | StationaryCivilianDialog | 0 | - | 0 | \spawn monster 1408 |
| 1409 | - | chosen | Chosen | 121432 | 85550 / - | Arch_MedRangedHumanoid_Base | 0 | - | 5 | \spawn monster 1409 |
| 1410 | [OBSOLETE] Rasper | gaea | Wildlife | 92798 | 40472 / - | - | 0 | 5704 / - | 5 | \spawn monster 1410 |
| 1411 | Black Hills Lieutenant | Black Hills Bandits | Outlaw | 75281 | 67425 / - | StationaryCivilianDialog | 0 | - | 0 | \spawn monster 1411 |
| 1412 | Missing Daughter | accord | Human | 117997 | - | PeacetimeCityWanderer | 0 | - | 0 | \spawn monster 1412 |
| 1413 | Distraught Father | accord | Human | 107837 | - | PeacetimeCityWanderer | 0 | - | 0 | \spawn monster 1413 |
| 1414 | Dire Fox | gaea | Wildlife | 92850 | 96691 / - | - | 0 | 5703 / 6211 | 5 | \spawn monster 1414 |
| 1415 | Rockhog | gaea | Wildlife | 92851 | 92865 / - | - | 0 | 5705 / - | 5 | \spawn monster 1415 |
| 1416 | Security Chief Delgado | accord | Human | 96923 | 84916 / - | AlertAndLookAtPlayer | 0 | - | 0 | \spawn monster 1416 |
| 1417 | Centauri Spitter | gaea | Wildlife | 122826 | 122827 / - | - | 0 | 5704 / - | 5 | \spawn monster 1417 |
| 1418 | - | gaea | Wildlife | 92850 | 86130 / - | - | 0 | 5704 / - | 5 | \spawn monster 1418 |
| 1419 | Titanomoth | gaea | Wildlife | 95361 | - | - | 0 | 5704 / 5414 | 5 | \spawn monster 1419 |
| 1420 | Alpha Direfox | gaea | Wildlife | 95150 | 97414 / - | - | 0 | 5704 / 6212 | 5 | \spawn monster 1420 |
| 1421 | Gurgon | gaea | Wildlife | 95103 | 93707 / - | - | 0 | 5704 / 5396 | 5 | \spawn monster 1421 |
| 1422 | Gremlin | gaea | Wildlife | 95388 | 86130 / - | - | 0 | 5704 / - | 5 | \spawn monster 1422 |
| 1423 | Rhinadon | neutral | Large wildlife | 95450 | 96537 / - | - | 0 | 5705 / - | 5 | \spawn monster 1423 |
| 1424 | Dross | accord | Human | 30605 | - | AlertAndInteractive | 0 | - | 0 | \spawn monster 1424 |
| 1425 | Strideviper | gaea | Wildlife | 95048 | 95106 / - | - | 0 | 5705 / 6220 | 5 | \spawn monster 1425 |
| 1426 | Shield Crawler | gaea | Wildlife | 122832 | 122834 / - | - | 10005 | 5706 / - | 5 | \spawn monster 1426 |
| 1427 | Ankylot | melding | Melded | 95104 | 93706 / - | - | 0 | 5704 / - | 5 | \spawn monster 1427 |
| 1428 | Accord Paratrooper | accord | Human | 86127 | 96666 / - | Arch_MedRangedHumanoid_Base | 0 | 5311 / 5387 | 0 | \spawn monster 1428 |
| 1429 | Crank | accord | Human | 125033 | - | - | 0 | - | 0 | \spawn monster 1429 |
| 1430 | [OBSOLETE] Queen Rasper | gaea | Wildlife | 92891 | 95144 / - | - | 0 | 5706 / - | 5 | \spawn monster 1430 |
| 1431 | Omnidyne-M Rep | accord | Human | 114640 | - | AlertAndInteractive | 0 | - | 10 | \spawn monster 1431 |
| 1432 | Lorenzo | bandit | Outlaw | 75281 | - | StationaryCivilianDialog | 0 | - | 0 | \spawn monster 1432 |
| 1433 | Lorenzo's Sister | accord | Human | 96558 | - | - | 0 | - | 0 | \spawn monster 1433 |
| 1434 | Lorenzo's Mother | accord | Human | 96558 | - | - | 0 | - | 0 | \spawn monster 1434 |
| 1435 | Robby | accord | Human | 93710 | 81305 / 81305 | Arch_MoveThenFire_Base | 0 | - | 0 | \spawn monster 1435 |
| 1436 | Accord Soldier | accord | Human | 117995 | 98989 / 76108 | Arch_MedRangedHumanoid_Base | 0 | - | 0 | \spawn monster 1436 |
| 1437 | - | bandit | Outlaw | 81304 | 81305 / - | - | 0 | 5311 / 5388 | 5 | \spawn monster 1437 |
| 1438 | Cuno | friendly | Companion | 93796 | 81305 / - | Arch_MedRangedHumanoid_Base | 0 | 5311 / 5388 | 5 | \spawn monster 1438 |
| 1439 | Grizli | accord | Human | 96913 | - | AlertAndInteractive | 0 | - | 0 | \spawn monster 1439 |
| 1440 | Distraught Wife | accord | Human | 81378 | - | PeacetimeCityWanderer | 0 | - | 0 | \spawn monster 1440 |
| 1441 | Carter | accord | Human | 117998 | - | - | 0 | - | 0 | \spawn monster 1441 |
| 1442 | Young Storm Kestrel | gaea | Wildlife | 77105 | 124553 / - | - | 0 | 5704 / - | 5 | \spawn monster 1442 |
| 1443 | - | friendly | Companion | 75281 | 67424 / - | Arch_MedRangedHumanoid_Base | 0 | 5311 / 5387 | 5 | \spawn monster 1443 |
| 1444 | Bandit Dreadnaught | friendly | Companion | 94161 | 75116 / - | - | 0 | 3 / 5388 | 5 | \spawn monster 1444 |
| 1445 | Courier Gamma | accord | Human | 118015 | - | - | 0 | - | 0 | \spawn monster 1445 |
| 1446 | Lodo | bandit | Outlaw | 75281 | - | StationaryCivilianDialog | 0 | - | 0 | \spawn monster 1446 |
| 1447 | Carlo Fonseca | accord | Human | 52453 | - | Null | 0 | - | 0 | \spawn monster 1447 |
| 1448 | Conus | bandit | Outlaw | 95031 | - | AlertAndInteractive | 0 | - | 0 | \spawn monster 1448 |
| 1449 | Leon | bandit | Outlaw | 142074 | 139891 / - | - | 0 | 5313 / 5389 | 0 | \spawn monster 1449 |
| 1450 | Auer | Black Hills Bandits | Outlaw | 118033 | 67425 / - | Arch_MedRangedHumanoid_Base | 0 | - | 0 | \spawn monster 1450 |
| 1451 | Civilian | accord | Human | 81378 | - | PeacetimeCityWanderer | 0 | - | 0 | \spawn monster 1451 |
| 1452 | Civilian | accord | Human | 81378 | - | PeacetimeCityWanderer | 0 | - | 0 | \spawn monster 1452 |
| 1453 | Civilian | accord | Human | 118010 | - | PeacetimeCityWanderer | 0 | - | 0 | \spawn monster 1453 |
| 1454 | Civilian | accord | Human | 81378 | - | PeacetimeCityWanderer | 0 | - | 0 | \spawn monster 1454 |
| 1455 | Civilian | accord | Human | 52455 | - | PeacetimeCityWanderer | 0 | - | 0 | \spawn monster 1455 |
| 1456 | Civilian | accord | Human | 52454 | - | PeacetimeCityWanderer | 0 | - | 0 | \spawn monster 1456 |
| 1457 | Civilian | accord | Human | 118011 | - | PeacetimeCityWanderer | 0 | - | 0 | \spawn monster 1457 |
| 1458 | Civilian | accord | Human | 52455 | - | PeacetimeCityWanderer | 0 | - | 0 | \spawn monster 1458 |
| 1459 | Civilian | accord | Human | 118013 | - | PeacetimeCityWanderer | 0 | - | 0 | \spawn monster 1459 |
| 1460 | Civilian | accord | Human | 117010 | - | PeacetimeCityWanderer | 0 | - | 0 | \spawn monster 1460 |
| 1461 | Capt. Hudson Fuller | accord | Human | 120704 | - | - | 0 | - | 0 | \spawn monster 1461 |
| 1462 | Accord Soldier | accord | Human | 117988 | 97695 / 76108 | Arch_MedRangedHumanoid_Base | 0 | - | 0 | \spawn monster 1462 |
| 1463 | Accord Soldier | accord | Human | 117991 | 98471 / 76108 | Arch_MedRangedHumanoid_Base | 0 | - | 0 | \spawn monster 1463 |
| 1464 | Accord Soldier | accord | Human | 117994 | 97953 / 76108 | Arch_MedRangedHumanoid_Base | 0 | - | 0 | \spawn monster 1464 |
| 1465 | Injured ARES Pilot | friendly | Companion | 93709 | - | - | 0 | - | 0 | \spawn monster 1465 |
| 1466 | The Cortador | bandit | Outlaw | 116927 | - | Arch_MedRangedHumanoid_Base | 0 | - | 0 | \spawn monster 1466 |
| 1467 | Anjo Olhos Boy (Corpse) | bandit | Outlaw | 95030 | 67425 / - | - | 0 | - | 0 | \spawn monster 1467 |
| 1468 | - | bandit | Outlaw | 113501 | 67425 / - | Arch_MedRangedHumanoid_Base | 0 | - | 0 | \spawn monster 1468 |
| 1469 | - | accord | Human | 93710 | - | Arch_MedRangedHumanoid_Base | 0 | - | 0 | \spawn monster 1469 |
| 1470 | Remigio Coelho | accord | Human | 96486 | 78403 / - | GuardCityWanderer | 0 | - | 0 | \spawn monster 1470 |
| 1471 | Poacher | bandit | Outlaw | 75281 | - / 67423 | Arch_MedRangedHumanoid_Base | 0 | 5311 / 5386 | 5 | \spawn monster 1471 |
| 1472 | Eduardo Coelho | accord | Human | 96486 | 78063 / - | GuardCityWanderer | 0 | - | 0 | \spawn monster 1472 |
| 1473 | Ricardo Coelho | accord | Human | 118039 | 78063 / - | GuardCityWanderer | 0 | - | 0 | \spawn monster 1473 |
| 1474 | Bernardo Coelho | accord | Human | 96486 | 79272 / - | GuardCityWanderer | 0 | - | 0 | \spawn monster 1474 |
| 1475 | Steve Coelho | accord | Human | 118039 | 122968 / - | GuardCityWanderer | 0 | - | 0 | \spawn monster 1475 |
| 1476 | Poacher | bandit | Outlaw | 96456 | 106335 / - | - | 0 | 5313 / 5389 | 5 | \spawn monster 1476 |
| 1477 | Bandit Assassin | bandit | Outlaw | 95072 | 96488 / - | Arch_AdditiveMelee_Base | 0 | 5311 / 5387 | 5 | \spawn monster 1477 |
| 1478 | Escaped Hostage | accord | Human | 82563 | - | AlertAndLookAtPlayer | 0 | - | 0 | \spawn monster 1478 |
| 1479 | Freed Civilian | accord | Human | 82563 | - | AlertAndLookAtPlayer | 0 | - | 0 | \spawn monster 1479 |
| 1480 | Little Claw | friendly | Companion | 95074 | - | - | 0 | - | 0 | \spawn monster 1480 |
| 1481 | Accord Engineer | accord | Human | 117611 | 76108 / 76108 | Arch_MedRangedHumanoid_Base | 0 | - | 0 | \spawn monster 1481 |
| 1482 | Adrita | accord | Human | 81378 | - | PeacetimeCityWanderer | 0 | - | 0 | \spawn monster 1482 |
| 1483 | - | accord | Human | 10001 | 79189 / - | AlertAndInteractive | 0 | - | 0 | \spawn monster 1483 |
| 1484 | Hobo Nick | accord | Human | 52453 | - | AlertAndInteractive | 0 | - | 0 | \spawn monster 1484 |
| 1485 | Nutretic Receiver Worker | accord | Human | 81388 | - | AlertAndInteractive | 0 | - | 0 | \spawn monster 1485 |
| 1486 | Zellick | accord | Human | 95080 | - | AlertAndInteractive | 0 | - | 0 | \spawn monster 1486 |
| 1487 | Angry Citizen | accord | Human | 52456 | - | AlertAndInteractive | 0 | - | 0 | \spawn monster 1487 |
| 1488 | Angry Citizen | accord | Human | 97262 | - | AlertAndInteractive | 0 | - | 0 | \spawn monster 1488 |
| 1489 | Angry Citizen | accord | Human | 52453 | - | AlertAndInteractive | 0 | - | 0 | \spawn monster 1489 |
| 1490 | - | gaea | Wildlife | 95090 | 81968 / - | CrystalAranhaMiniBoss | 0 | 6137 / 5398 | 5 | \spawn monster 1490 |
| 1491 | - | gaea | Wildlife | 95091 | 95101 / - | LandSharkMiniBoss | 0 | 5706 / - | 5 | \spawn monster 1491 |
| 1492 | - | gaea | Wildlife | 95092 | 77218 / - | GiantAranhaMiniBoss | 0 | 5705 / 5415 | 5 | \spawn monster 1492 |
| 1493 | - | accord | Human | 85686 | 85735 / - | GuardCityWanderer | 0 | - | 0 | \spawn monster 1493 |
| 1494 | - | gaea | Wildlife | 81938 | 81968 / - | CrystalAranhaMiniBoss | 0 | 6137 / 5398 | 5 | \spawn monster 1494 |
| 1495 | Gang Leader | bandit | Outlaw | 95076 | 81308 / - | Arch_MedRangedHumanoid_Base | 0 | 3 / 5386 | 0 | \spawn monster 1495 |
| 1496 | Coruja | accord | Human | 96943 | - | AlertAndInteractive | 0 | - | 0 | \spawn monster 1496 |
| 1497 | Rebel | bandit | Outlaw | 75281 | - | Arch_MedRangedHumanoid_Base | 0 | - | 0 | \spawn monster 1497 |
| 1498 | Captain Simonis | accord | Human | 97017 | - | AlertAndInteractive | 0 | - | 0 | \spawn monster 1498 |
| 1499 | Luiz Belo | accord | Human | 81379 | - | AlertAndLookAtPlayer | 0 | - | 0 | \spawn monster 1499 |
| 1500 | - | accord | Human | 95117 | 86174 / - | EliteWanderer | 0 | 1 / 5385 | 0 | \spawn monster 1500 |
| 1501 | Treasure Hunter | accord | Human | 118021 | - | PeacetimeCityWanderer | 0 | - | 0 | \spawn monster 1501 |
| 1502 | Deana | accord | Human | 97026 | - | AlertAndInteractive | 0 | - | 0 | \spawn monster 1502 |
| 1503 | Gretchen | accord | Human | 96945 | - | AlertAndInteractive | 0 | - | 0 | \spawn monster 1503 |
| 1504 | Bjorn | accord | Human | 95080 | - | AlertAndInteractive | 0 | - | 0 | \spawn monster 1504 |
| 1505 | - | accord | Human | 81377 | - | AlertAndInteractive | 0 | - | 0 | \spawn monster 1505 |
| 1506 | Reaper Privateer | Reapers | Outlaw | 125065 | 96842 / - | - | 0 | 5310 / 5386 | 0 | \spawn monster 1506 |
| 1507 | Rebel Fighter | Rebels | Outlaw | 125065 | 96842 / - | - | 0 | 5310 / 5386 | 0 | \spawn monster 1507 |
| 1508 | Ophanim Soldier | Ophanim | Outlaw | 125065 | 96842 / - | - | 0 | 5310 / 5386 | 0 | \spawn monster 1508 |
| 1509 | - | bandit | Outlaw | 95148 | - / 67423 | - | 0 | 5311 / 5386 | 0 | \spawn monster 1509 |
| 1510 | Buzzard Pointman | bandit | Outlaw | 95152 | - / 67423 | - | 0 | 5311 / 5386 | 0 | \spawn monster 1510 |
| 1511 | Capt. Hudson Fuller | accord | Human | 97018 | - | AlertAndInteractive | 0 | - | 0 | \spawn monster 1511 |
| 1512 | Black Hills Grenadier | Black Hills Bandits | Outlaw | 125146 | 96848 / - | - | 0 | 5312 / 5388 | 5 | \spawn monster 1512 |
| 1513 | - | Black Hills Bandits | Outlaw | 75281 | 96890 / - | - | 0 | 5311 / 5386 | 5 | \spawn monster 1513 |
| 1514 | Reaper Raider | Reapers | Outlaw | 125069 | 96847 / - | - | 0 | 5311 / 5387 | 5 | \spawn monster 1514 |
| 1515 | Reaper Cannoneer | Reapers | Outlaw | 97338 | 96848 / - | - | 0 | 5311 / 5387 | 5 | \spawn monster 1515 |
| 1516 | Rebel Pyro | Rebels | Outlaw | 95136 | 96884 / - | - | 0 | 5311 / 5387 | 5 | \spawn monster 1516 |
| 1517 | - | bandit | Outlaw | 96886 | - | - | 0 | 5311 / 5386 | 5 | \spawn monster 1517 |
| 1518 | - | bandit | Outlaw | 95148 | 67425 / - | - | 0 | 5311 / 5386 | 5 | \spawn monster 1518 |
| 1519 | - | bandit | Outlaw | 95148 | 67424 / - | - | 0 | 5311 / 5387 | 5 | \spawn monster 1519 |
| 1520 | Ophanim Plasma Caster | Ophanim | Outlaw | 125146 | 139891 / - | - | 0 | 5312 / 5388 | 5 | \spawn monster 1520 |
| 1521 | Ophanim Engineer | Ophanim | Outlaw | 125147 | 122543 / - | - | 0 | 5311 / 5387 | 5 | \spawn monster 1521 |
| 1522 | Black Hills Dreadnaught | bandit | Outlaw | 95375 | 96889 / - | - | 0 | 5311 / 5388 | 5 | \spawn monster 1522 |
| 1523 | Reaper Chief | bandit | Outlaw | 81304 | 81305 / - | - | 0 | 5311 / 5388 | 5 | \spawn monster 1523 |
| 1524 | Mustang | accord | Human | 52455 | - | AlertAndInteractive | 0 | - | 0 | \spawn monster 1524 |
| 1525 | - | bandit | Outlaw | 81304 | 81305 / - | - | 0 | 5311 / 5388 | 5 | \spawn monster 1525 |
| 1526 | Rebel Support | bandit | Outlaw | 96888 | 96842 / - | - | 0 | 5311 / 5388 | 5 | \spawn monster 1526 |
| 1527 | Ophanim Chief | bandit | Outlaw | 95376 | 81305 / - | - | 0 | 5311 / 5388 | 5 | \spawn monster 1527 |
| 1528 | Tanken Chief | bandit | Outlaw | 95378 | 81305 / - | - | 0 | 5311 / 5388 | 5 | \spawn monster 1528 |
| 1529 | Ophanim Commander | Ophanim | Outlaw | 125034 | 103849 / - | - | 0 | 5313 / 5389 | 5 | \spawn monster 1529 |
| 1530 | - | bandit | Outlaw | 95148 | 75116 / - | - | 0 | 3 / 5388 | 5 | \spawn monster 1530 |
| 1531 | - | bandit | Outlaw | 95136 | 75116 / - | - | 0 | 3 / 5388 | 5 | \spawn monster 1531 |
| 1532 | - | bandit | Outlaw | 95134 | 75116 / - | - | 0 | 3 / 5388 | 5 | \spawn monster 1532 |
| 1533 | Black Hills Leader | Black Hills Bandits | Outlaw | 125034 | 106335 / - | - | 0 | 5313 / 5389 | 5 | \spawn monster 1533 |
| 1534 | - | bandit | Outlaw | 81307 | 81308 / - | - | 0 | 3 / 5386 | 5 | \spawn monster 1534 |
| 1535 | - | bandit | Outlaw | 95134 | 81308 / - | - | 0 | 3 / 5386 | 5 | \spawn monster 1535 |
| 1536 | - | bandit | Outlaw | 95136 | 81308 / - | - | 0 | 3 / 5386 | 5 | \spawn monster 1536 |
| 1537 | - | bandit | Outlaw | 95148 | 81308 / - | - | 0 | 3 / 5386 | 5 | \spawn monster 1537 |
| 1538 | - | bandit | Outlaw | 95146 | 81308 / - | - | 0 | 3 / 5386 | 5 | \spawn monster 1538 |
| 1539 | - | accord | Human | 10001 | 76108 / - | Arch_MedRangedHumanoid_Base | 0 | - | 0 | \spawn monster 1539 |
| 1540 | Chosen Earthbreaker | chosen | Chosen | 95382 | 95383 / 118938 | Arch_MoveThenFire_Base | 0 | - | 0 | \spawn monster 1540 |
| 1541 | Salvador | accord | Human | 52453 | - | - | 0 | - | 0 | \spawn monster 1541 |
| 1542 | Dado | accord | Human | 95384 | 67423 / - | GuardCityWanderer | 0 | - | 0 | \spawn monster 1542 |
| 1543 | Lacidar | accord | Human | 95385 | 20008 / - | GuardCityWanderer | 0 | - | 0 | \spawn monster 1543 |
| 1544 | Trevor | accord | Human | 95386 | 87825 / - | GuardCityWanderer | 0 | - | 0 | \spawn monster 1544 |
| 1545 | Bandit Contact | bandit | Outlaw | 81391 | 78495 / - | Arch_MedRangedHumanoid_Base | 0 | - | 5 | \spawn monster 1545 |
| 1546 | Bryce | accord | Human | 81330 | - | AlertAndInteractive | 0 | - | 0 | \spawn monster 1546 |
| 1547 | Shady Civilian | bandit | Outlaw | 75281 | 67425 / - | Arch_MedRangedHumanoid_Base | 0 | 5311 / 5386 | 5 | \spawn monster 1547 |
| 1548 | Hate-Bot | gaea | Wildlife | 95392 | - | - | 0 | 2 / - | 0 | \spawn monster 1548 |
| 1549 | Separatist Leader | bandit | Outlaw | 96456 | 75274 / - | Arch_MoveThenFire_Base | 0 | 5311 / 5388 | 5 | \spawn monster 1549 |
| 1550 | Horkos | Ophanim | Outlaw | 142074 | 139891 / - | - | 0 | 5313 / 5389 | 5 | \spawn monster 1550 |
| 1551 | - | chosen | Chosen | 95406 | 95408 / - | Arch_AdditiveMelee_Base | 0 | - | 5 | \spawn monster 1551 |
| 1552 | Accord Assistant | accord | Human | 117983 | - | PeacetimeCityWanderer | 0 | - | 0 | \spawn monster 1552 |
| 1553 | Holmgang Target Drone | accord | Human | 95404 | - | InteractiveWithEmote | 0 | - | 0 | \spawn monster 1553 |
| 1554 | Holmgang Announcer | accord | Human | 10001 | - / 30025 | - | 0 | - | 0 | \spawn monster 1554 |
| 1555 | Charon | bandit | Outlaw | 118038 | 139891 / - | - | 0 | 5313 / 5389 | 0 | \spawn monster 1555 |
| 1556 | Glass Mo | accord | Human | 95413 | - / 85972 | Arch_MedRangedHumanoid_Base | 0 | - | 0 | \spawn monster 1556 |
| 1557 | - | chosen | Chosen | 95409 | 95410 / - | Arch_MedRangedHumanoid_Base | 0 | - | 0 | \spawn monster 1557 |
| 1558 | Holmgang Fan | accord | Human | 81378 | - | PeacetimeCityWanderer | 0 | - | 0 | \spawn monster 1558 |
| 1559 | Cerberus | Rebels | Outlaw | 142074 | 143829 / - | - | 0 | 5313 / 5389 | 5 | \spawn monster 1559 |
| 1560 | Bald Bully | chosen | Human | 95412 | 120500 / 120497 | - | 0 | - | 0 | \spawn monster 1560 |
| 1561 | Holmgang Fan | accord | Human | 81378 | - | PeacetimeCityWanderer | 0 | - | 0 | \spawn monster 1561 |
| 1562 | Holmgang Fan | accord | Human | 81388 | - | PeacetimeCityWanderer | 0 | - | 0 | \spawn monster 1562 |
| 1563 | Holmgang Fan | accord | Human | 52453 | - | PeacetimeCityWanderer | 0 | - | 0 | \spawn monster 1563 |
| 1564 | Holmgang Fan | accord | Human | 52453 | - | PeacetimeCityWanderer | 0 | - | 0 | \spawn monster 1564 |
| 1565 | Julius Marks | accord | Human | 95446 | 96871 / 84917 | Arch_Sniper_Base | 0 | - | 0 | \spawn monster 1565 |
| 1566 | Vincent Marks | accord | Human | 95446 | 96871 / 84917 | Arch_Sniper_Base | 0 | - | 0 | \spawn monster 1566 |
| 1567 | Van Uzi | accord | Human | 95441 | - / 78083 | Arch_MedRangedAbilityUser_Base | 0 | - | 0 | \spawn monster 1567 |
| 1568 | Juan | accord | Human | 95451 | 86174 / - | EliteWanderer | 0 | 1 / 5385 | 1 | \spawn monster 1568 |
| 1569 | Conus | accord | Human | 95442 | - | StationaryCivilianDialog | 0 | - | 0 | \spawn monster 1569 |
| 1570 | Reporter | accord | Human | 117587 | - | PeacetimeCityWanderer | 0 | - | 0 | \spawn monster 1570 |
| 1571 | Holmgang Fan | accord | Human | 81379 | - | PeacetimeCityWanderer | 0 | - | 0 | \spawn monster 1571 |
| 1572 | Holmgang Fan | accord | Human | 81378 | - | PeacetimeCityWanderer | 0 | - | 0 | \spawn monster 1572 |
| 1573 | Holmgang Fan | accord | Human | 81388 | - | PeacetimeCityWanderer | 0 | - | 0 | \spawn monster 1573 |
| 1574 | Holmgang Fan | accord | Human | 52454 | - | PeacetimeCityWanderer | 0 | - | 0 | \spawn monster 1574 |
| 1575 | Holmgang Fan | accord | Human | 52453 | - | PeacetimeCityWanderer | 0 | - | 0 | \spawn monster 1575 |
| 1576 | SIN Hack Dealer | bandit | Outlaw | 86173 | - | EliteWanderer | 0 | 1 / 5385 | 0 | \spawn monster 1576 |
| 1577 | - | bandit | Outlaw | 95502 | 67425 / - | Arch_MedRangedAbilityUser_Base | 0 | 5311 / 5386 | 5 | \spawn monster 1577 |
| 1578 | Astrek Agent | accord | Human | 52461 | - | PeacetimeCityWanderer | 0 | - | 0 | \spawn monster 1578 |
| 1579 | - | bandit | Outlaw | 95461 | 95448 / - | Arch_FullbodyMelee_Base | 0 | 2 / 5386 | 5 | \spawn monster 1579 |
| 1580 | Captain Abrams | chosen | Chosen | 116547 | 85745 / - | Arch_AdditiveMelee_Base | 0 | 5696 / 5367 | 5 | \spawn monster 1580 |
| 1581 | Adrita | accord | Human | 82515 | - | AlertAndInteractive | 0 | - | 0 | \spawn monster 1581 |
| 1582 | Rebel Leader | Rebels | Outlaw | 96456 | 81305 / - | Arch_MoveThenFire_Base | 0 | 5311 / 5388 | 5 | \spawn monster 1582 |
| 1583 | - | accord | Human | 77744 | - | Arch_MedRangedHumanoid_Base | 0 | - | 0 | \spawn monster 1583 |
| 1584 | Scarlet | accord | Human | 117619 | - | PlayerPet | 0 | - | 0 | \spawn monster 1584 |
| 1585 | Gerald Greenway | Rebels | Outlaw | 116679 | 116680 / - | Arch_MoveThenFire_Base | 0 | 5311 / 5388 | 5 | \spawn monster 1585 |
| 1586 | Grieving Husband | accord | Human | 52462 | - | PeacetimeCityWanderer | 0 | - | 0 | \spawn monster 1586 |
| 1587 | El Terremoto | accord | Human | 96454 | 82484 / - | Arch_MedRangedHumanoid_Attack | 0 | - | 0 | \spawn monster 1587 |
| 1588 | Sydney | accord | Human | 116677 | - | PeacetimeCityWanderer | 0 | - | 0 | \spawn monster 1588 |
| 1589 | Echo 81 | accord | Human | 95490 | 67425 / - | Arch_MedRangedHumanoid_Base | 0 | 5311 / 5386 | 5 | \spawn monster 1589 |
| 1590 | Major Desselhoff | accord | Human | 95469 | - / 76108 | Arch_MedRangedHumanoid_Base | 0 | - | 0 | \spawn monster 1590 |
| 1592 | - | bandit | Outlaw | 95494 | 67425 / - | Arch_MedRangedAbilityUser_Base | 0 | 5311 / 5386 | 5 | \spawn monster 1592 |
| 1593 | Mr. Akiyama | accord | Human | 52453 | - | AlertAndInteractive | 0 | - | 0 | \spawn monster 1593 |
| 1594 | Artis the Mechanic | accord | Human | 96979 | - | AlertAndInteractive | 0 | - | 0 | \spawn monster 1594 |
| 1596 | The Traitor | bandit | Outlaw | 95136 | 75116 / - | Arch_MedRangedAbilityUser_Base | 0 | 3 / 5388 | 5 | \spawn monster 1596 |
| 1597 | Infected Storm Kestrel | gaea | Wildlife | 95488 | 123024 / - | - | 0 | 5704 / - | 5 | \spawn monster 1597 |
| 1599 | Van Pistol | bandit | Human | 95548 | - | Arch_AdditiveMelee_Base | 0 | - | 0 | \spawn monster 1599 |
| 1600 | Zanmato | accord | Human | 118005 | 67423 / - | Arch_MoveThenFire_Base | 0 | 5311 / 5388 | 5 | \spawn monster 1600 |
| 1601 | Alex | accord | Human | 118016 | - | PeacetimeCityWanderer | 0 | - | 0 | \spawn monster 1601 |
| 1602 | Jon | accord | Human | 118016 | - | PeacetimeCityWanderer | 0 | - | 0 | \spawn monster 1602 |
| 1603 | Chris | accord | Human | 118016 | - | AlertAndLookAtPlayer | 0 | - | 0 | \spawn monster 1603 |
| 1604 | Steph | accord | Human | 118018 | - | PeacetimeCityWanderer | 0 | - | 0 | \spawn monster 1604 |
| 1605 | Sliver | friendly | Companion | 118008 | - | Arch_MedRangedHumanoid_Base | 0 | - | 0 | \spawn monster 1605 |
| 1606 | Mitty the Gambler | accord | Human | 106342 | - | AlertAndInteractive | 0 | - | 0 | \spawn monster 1606 |
| 1607 | Sheriff Fairuza Nasseri | accord | Human | 106345 | 78065 / - | AlertAndInteractive | 0 | - | 0 | \spawn monster 1607 |
| 1608 | The Buzzard | accord | Human | 95152 | - / 122621 | - | 0 | 5311 / 5386 | 0 | \spawn monster 1608 |
| 1609 | Terrorclaw Prime | gaea | Wildlife | 96466 | 85191 / - | - | 0 | 5706 / 5425 | 5 | \spawn monster 1609 |
| 1610 | Cognac | accord | Human | 106330 | - | AlertAndInteractive | 0 | - | 0 | \spawn monster 1610 |
| 1611 | Rico | accord | Human | 118017 | 78063 / - | GuardCityWanderer | 0 | - | 0 | \spawn monster 1611 |
| 1612 | Rooster | accord | Human | 118019 | 78063 / - | GuardCityWanderer | 0 | - | 0 | \spawn monster 1612 |
| 1613 | Dr. Delacroix | accord | Human | 125038 | - | UseAbilityOnInteract | 0 | - | 0 | \spawn monster 1613 |
| 1614 | Maria Cantos | accord | Human | 106344 | - | AlertAndInteractive | 0 | - | 0 | \spawn monster 1614 |
| 1615 | - | accord | Human | 52457 | - | AlertAndLookAtPlayer | 0 | - | 0 | \spawn monster 1615 |
| 1616 | The Scout | accord | Human | 81304 | 106335 / - | Arch_MoveThenFire_Base | 0 | 5311 / 5388 | 5 | \spawn monster 1616 |
| 1617 | - | accord | Human | 95494 | - | AlertAndInteractive | 0 | 5311 / 5386 | 5 | \spawn monster 1617 |
| 1618 | Gunmetal Jack | bandit | Human | 142074 | 106335 / - | - | 0 | 5313 / 5389 | 5 | \spawn monster 1618 |
| 1619 | - | bandit | Outlaw | 96476 | 75116 / - | Arch_MoveThenFire_Base | 0 | 3 / 5388 | 5 | \spawn monster 1619 |
| 1620 | Nightmare Toxic Sandshark | gaea | Wildlife | 96477 | 116554 / - | LandSharkMiniBoss | 0 | 5706 / - | 5 | \spawn monster 1620 |
| 1621 | Anton Hall | accord | Human | 106359 | - | AlertAndInteractive | 0 | - | 0 | \spawn monster 1621 |
| 1622 | Buzzard | accord | Human | 95152 | - / 67425 | Arch_MedRangedHumanoid_Base | 0 | 5311 / 5386 | 0 | \spawn monster 1622 |
| 1623 | Jerry | accord | Human | 118007 | - | PeacetimeCityWanderer | 0 | - | 0 | \spawn monster 1623 |
| 1624 | Bob | accord | Human | 117984 | 78065 / - | PeacetimeCityWanderer | 0 | - | 0 | \spawn monster 1624 |
| 1625 | Axel | accord | Human | 92801 | 78063 / - | PeacetimeCityWanderer | 0 | - | 0 | \spawn monster 1625 |
| 1626 | - | accord | Human | 96486 | - | PeacetimeCityWanderer | 0 | - | 0 | \spawn monster 1626 |
| 1627 | Jessica | accord | Human | 96486 | - | PeacetimeCityWanderer | 0 | - | 0 | \spawn monster 1627 |
| 1628 | - | accord | Human | 96486 | - | PeacetimeCityWanderer | 0 | - | 0 | \spawn monster 1628 |
| 1629 | Jaxson | accord | Human | 96486 | - | PeacetimeCityWanderer | 0 | - | 0 | \spawn monster 1629 |
| 1630 | - | gaea | Wildlife | 96555 | 86130 / - | - | 0 | 5704 / - | 5 | \spawn monster 1630 |
| 1631 | - | accord | Human | 96559 | - | AlertAndInteractive | 0 | - | 0 | \spawn monster 1631 |
| 1632 | Albert Finch | accord | Human | 106343 | - | AlertAndInteractive | 0 | - | 0 | \spawn monster 1632 |
| 1633 | Dulles | accord | Human | 106340 | - | AlertAndInteractive | 0 | - | 0 | \spawn monster 1633 |
| 1634 | Kazuo Mori | accord | Human | 106341 | - | AlertAndInteractive | 0 | - | 0 | \spawn monster 1634 |
| 1635 | Buzzard Pointman | accord | Human | 118006 | - / 122621 | - | 0 | 5311 / 5386 | 0 | \spawn monster 1635 |
| 1636 | Yuki Lin | accord | Human | 106350 | - | AlertAndInteractive | 0 | - | 0 | \spawn monster 1636 |
| 1637 | Vibol Soun | accord | Human | 106350 | - | AlertAndInteractive | 0 | - | 0 | \spawn monster 1637 |
| 1638 | - | bandit | Outlaw | 96563 | 96488 / - | Arch_AdditiveMelee_Base | 0 | 5311 / 5387 | 5 | \spawn monster 1638 |
| 1639 | - | bandit | Outlaw | 95072 | 96488 / - | Arch_AdditiveMelee_Base | 0 | 5311 / 5387 | 5 | \spawn monster 1639 |
| 1640 | ARES Soldier | accord | Human | 77865 | - | AlertAndInteractive | 0 | - | 0 | \spawn monster 1640 |
| 1641 | ARES Commander | accord | Human | 125033 | - | - | 0 | - | 0 | \spawn monster 1641 |
| 1642 | ARES Soldier | accord | Human | 77866 | - | AlertAndInteractive | 0 | - | 0 | \spawn monster 1642 |
| 1643 | - | accord | Human | 77866 | - | AlertAndInteractive | 0 | - | 0 | \spawn monster 1643 |
| 1644 | ARES Soldier | accord | Human | 77867 | - | AlertAndInteractive | 0 | - | 0 | \spawn monster 1644 |
| 1645 | ARES Soldier | accord | Human | 77867 | - | AlertAndInteractive | 0 | - | 0 | \spawn monster 1645 |
| 1646 | Davis Royer | accord | Human | 106346 | - | AlertAndInteractive | 0 | - | 0 | \spawn monster 1646 |
| 1647 | ARES Soldier | accord | Human | 77865 | - | AlertAndInteractive | 0 | - | 0 | \spawn monster 1647 |
| 1648 | Rina Joshi | accord | Human | 106510 | - | AlertAndInteractive | 0 | - | 0 | \spawn monster 1648 |
| 1649 | Consigliere Sparanzo | accord | Human | 106327 | - | AlertAndInteractive | 0 | - | 0 | \spawn monster 1649 |
| 1650 | Lt. Hornsby | accord | Human | 96567 | 79272 / - | GuardCityWanderer | 0 | - | 0 | \spawn monster 1650 |
| 1651 | Baby Red Panda | friendly | Companion | 96577 | - | - | 0 | - | 0 | \spawn monster 1651 |
| 1652 | Major O'Brien | accord | Human | 96567 | 79272 / - | Arch_MedRangedHumanoid_Base | 0 | - | 0 | \spawn monster 1652 |
| 1653 | - | accord | Human | 95152 | - / 67423 | - | 0 | 5311 / 5386 | 0 | \spawn monster 1653 |
| 1654 | Horned Fox | friendly | Companion | 96579 | - | - | 0 | - | 5 | \spawn monster 1654 |
| 1655 | - | bandit | Outlaw | 85841 | 81396 / 82354 | RaiderBaronMiniBoss | 0 | 5313 / 5388 | 5 | \spawn monster 1655 |
| 1656 | - | bandit | Outlaw | 95548 | - | Arch_AdditiveMelee_Base | 0 | - | 0 | \spawn monster 1656 |
| 1657 | Tanken Enforcer | bandit | Outlaw | 85841 | 81396 / 82354 | RaiderBaronMiniBoss | 0 | 5313 / 5388 | 5 | \spawn monster 1657 |
| 1658 | Serkan | chosen | Chosen | 82463 | 82484 / - | Arch_MedRangedHumanoid_Base | 0 | 5700 / 5371 | 5 | \spawn monster 1658 |
| 1659 | - | accord | Human | 85619 | - | - | 0 | - | 0 | \spawn monster 1659 |
| 1660 | Alpha | accord | Human | 96638 | - / 117621 | Arch_MedRangedHumanoid_Base | 0 | 5311 / 5386 | 0 | \spawn monster 1660 |
| 1661 | Beta | accord | Human | 96638 | 96848 / - | - | 0 | 5311 / 5387 | 5 | \spawn monster 1661 |
| 1662 | Rogue Criminal | bandit | Outlaw | 85841 | 81396 / 82354 | RaiderBaronMiniBoss | 0 | 5313 / 5388 | 5 | \spawn monster 1662 |
| 1663 | Tanken Defector | accord | Human | 118023 | - | Arch_MedRangedHumanoid_Base | 0 | - | 0 | \spawn monster 1663 |
| 1664 | Chosen Commander | chosen | Chosen | 88154 | 88155 / - | Arch_MoveThenFire_Base | 0 | 5700 / 5371 | 5 | \spawn monster 1664 |
| 1665 | Tanken Agent | bandit | Outlaw | 95148 | 67425 / - | - | 0 | 5311 / 5386 | 5 | \spawn monster 1665 |
| 1666 | Super Striped Gremlin | gaea | Wildlife | 96614 | 86130 / - | - | 0 | 5704 / - | 5 | \spawn monster 1666 |
| 1667 | Nikodemus | accord | Human | 52455 | - | PeacetimeCityWanderer | 0 | - | 0 | \spawn monster 1667 |
| 1668 | Koralia | accord | Human | 107774 | 78065 / - | GuardCityWanderer | 0 | - | 0 | \spawn monster 1668 |
| 1669 | Enhanced Marauder | bandit | Outlaw | 81304 | 81305 / - | Arch_MedRangedHumanoid_Base | 0 | 5311 / 5388 | 5 | \spawn monster 1669 |
| 1670 | Nutretic Liaison | bandit | Outlaw | 75281 | 67425 / - | Arch_MedRangedHumanoid_Base | 0 | - | 0 | \spawn monster 1670 |
| 1671 | Hal Edwards | accord | Human | 117693 | - | - | 0 | - | 0 | \spawn monster 1671 |
| 1672 | Dr. Calmack | accord | Human | 96486 | - | StationaryCivilianDialog | 0 | - | 0 | \spawn monster 1672 |
| 1673 | SIN Hacker | bandit | Outlaw | 96646 | 79066 / - | Arch_MedRangedHumanoid_Base | 0 | - | 0 | \spawn monster 1673 |
| 1674 | Accord Scientist | accord | Human | 117986 | - | AlertAndInteractive | 0 | - | 0 | \spawn monster 1674 |
| 1675 | Accord Scientist | accord | Human | 117989 | - | AlertAndInteractive | 0 | - | 0 | \spawn monster 1675 |
| 1676 | Accord Scientist | accord | Human | 117990 | - | AlertAndInteractive | 0 | - | 0 | \spawn monster 1676 |
| 1677 | Accord Scientist | accord | Human | 117987 | - | AlertAndInteractive | 0 | - | 0 | \spawn monster 1677 |
| 1678 | Omnidyne-M Guard | bandit | Outlaw | 86344 | 75769 / - | Arch_MedRangedHumanoid_Base | 0 | 4 / 3 | 5 | \spawn monster 1678 |
| 1679 | Chimera Dealer | bandit | Outlaw | 95134 | 86280 / 67423 | RaiderBaronMiniBoss | 0 | 5311 / 5386 | 0 | \spawn monster 1679 |
| 1680 | - | accord | Human | 52459 | - | PeacetimeCityWanderer | 0 | - | 0 | \spawn monster 1680 |
| 1681 | Omnidyne-M Scientist | accord | Human | 52459 | - | PeacetimeCityWanderer | 0 | - | 0 | \spawn monster 1681 |
| 1682 | Bandit Recruiter | bandit | Outlaw | 75281 | - | AlertAndInteractive | 0 | - | 0 | \spawn monster 1682 |
| 1683 | Bandit Recruiter | bandit | Outlaw | 75281 | - | AlertAndInteractive | 0 | - | 0 | \spawn monster 1683 |
| 1684 | Bandit Recruiter | bandit | Outlaw | 75281 | - | AlertAndInteractive | 0 | - | 0 | \spawn monster 1684 |
| 1685 | Bandit Recruiter | bandit | Outlaw | 75281 | - | AlertAndInteractive | 0 | - | 0 | \spawn monster 1685 |
| 1686 | Black Hills Lieutenant | Black Hills Bandits | Outlaw | 85841 | 124703 / - | - | 0 | 5313 / 5388 | 5 | \spawn monster 1686 |
| 1687 | Doctor Abrams | accord | Human | 114633 | - | AlertAndInteractive | 0 | - | 0 | \spawn monster 1687 |
| 1688 | Tanken Lieutenant | accord | Human | 96638 | 75116 / - | - | 0 | 5311 / 5388 | 5 | \spawn monster 1688 |
| 1689 | Commander Kimbase | accord | Human | 117023 | - | AlertAndInteractive | 0 | - | 0 | \spawn monster 1689 |
| 1690 | Special Agent Hunter | accord | Human | 52455 | 76108 / - | AlertAndInteractive | 0 | - | 0 | \spawn monster 1690 |
| 1691 | SIN Phantom | bandit | Outlaw | 125147 | 122621 / - | - | 0 | 3 / - | 5 | \spawn monster 1691 |
| 1692 | Tanken Ally | accord | Human | 96638 | 96848 / - | - | 0 | 5311 / 5387 | 5 | \spawn monster 1692 |
| 1694 | Tanken Ally | accord | Human | 96638 | - / 67423 | - | 0 | 5311 / 5386 | 0 | \spawn monster 1694 |
| 1695 | Zed | accord | Human | 106357 | - | AlertAndInteractive | 0 | - | 0 | \spawn monster 1695 |
| 1696 | Commander Price | accord | Human | 106352 | - | AlertAndInteractive | 0 | - | 0 | \spawn monster 1696 |
| 1697 | Dr. Farraday | accord | Human | 81326 | - | AlertAndInteractive | 0 | - | 0 | \spawn monster 1697 |
| 1698 | - | bandit | Outlaw | 95134 | - / 67423 | - | 0 | 5311 / 5386 | 0 | \spawn monster 1698 |
| 1699 | Colonel Havel | accord | Human | 118012 | - | AlertAndInteractive | 0 | - | 0 | \spawn monster 1699 |
| 1700 | Private Durst | bandit | Outlaw | 142074 | 140162 / - | - | 0 | 5313 / 5389 | 0 | \spawn monster 1700 |
| 1701 | Corporal Linhold | accord | Human | 96694 | 78357 / - | Arch_MedRangedHumanoid_Base | 0 | - | 0 | \spawn monster 1701 |
| 1702 | - | chosen | Chosen | 96695 | 77049 / 32739 | Arch_Sniper_Base | 0 | 5698 / 5370 | 5 | \spawn monster 1702 |
| 1703 | Omnidyne-M Employee | accord | Human | 97262 | 77339 / - | AlertAndInteractive | 0 | - | 0 | \spawn monster 1703 |
| 1704 | - | gaea | Wildlife | 96698 | - | RaiderBaronMiniBoss | 0 | - | 0 | \spawn monster 1704 |
| 1705 | June Harper | accord | Human | 52458 | - | PeacetimeCityWanderer | 0 | - | 0 | \spawn monster 1705 |
| 1706 | Billy the Bullet | accord | Human | 52458 | - | AlertAndLookAtPlayer | 0 | - | 0 | \spawn monster 1706 |
| 1707 | - | accord | Human | 31331 | 85735 / - | OneOff_FireAtEnemy | 0 | - | 5 | \spawn monster 1707 |
| 1708 | Omnidyne-m Heavy Turret | accord | Human | 33976 | - / 96716 | AggressiveWanderer | 0 | - | 0 | \spawn monster 1708 |
| 1709 | Supply Officer Booker | accord | Human | 96715 | - | AlertAndInteractive | 0 | - | 0 | \spawn monster 1709 |
| 1710 | Supply Officer Cook | accord | Human | 96725 | - | AlertAndInteractive | 0 | - | 0 | \spawn monster 1710 |
| 1711 | - | accord | Human | 31331 | 85735 / - | OneOff_FireAtEnemy | 0 | - | 5 | \spawn monster 1711 |
| 1712 | - | accord | Human | 31331 | 85735 / - | OneOff_FireAtEnemy | 0 | - | 5 | \spawn monster 1712 |
| 1713 | - | friendly | Companion | 33814 | 20038 / - | Arch_AdditiveMelee_Base | 0 | - | 0 | \spawn monster 1713 |
| 1714 | Omnidyne-M Agent | bandit | Outlaw | 96744 | 96847 / - | - | 0 | - | 0 | \spawn monster 1714 |
| 1715 | Omnidyne-M Agent | bandit | Outlaw | 96744 | 96848 / - | Arch_MedRangedHumanoid_Base | 0 | - | 0 | \spawn monster 1715 |
| 1716 | Enyo Drone | bandit | Outlaw | 96762 | 123340 / - | - | 0 | 3 / - | 0 | \spawn monster 1716 |
| 1717 | Phobos Bot+B82 | bandit | Outlaw | 96761 | 123341 / - | - | 0 | 3 / - | 0 | \spawn monster 1717 |
| 1718 | - | bandit | Outlaw | 96768 | - / 67423 | - | 0 | 5311 / 5386 | 0 | \spawn monster 1718 |
| 1719 | OBSOLETE Bull Skiver | gaea | Wildlife | 96786 | - | - | 0 | 5704 / - | 5 | \spawn monster 1719 |
| 1720 | OBSOLETE Spitting Skiver | gaea | Wildlife | 96794 | 97020 / - | - | 0 | 2 / - | 5 | \spawn monster 1720 |
| 1721 | OBSOLETE King Skiver | gaea | Wildlife | 96795 | 118583 / - | - | 0 | 5705 / - | 5 | \spawn monster 1721 |
| 1722 | - | gaea | Wildlife | 97058 | 97059 / - | - | 0 | 5704 / 5397 | 5 | \spawn monster 1722 |
| 1723 | Rocket Pod Combat Drone | bandit | Outlaw | 96798 | - | Arch_AdditiveMelee_Base | 0 | - | 0 | \spawn monster 1723 |
| 1724 | - | gaea | Wildlife | 96815 | 97096 / - | - | 0 | 5704 / 5404 | 5 | \spawn monster 1724 |
| 1725 | Albert Smith | accord | Human | 125033 | - | - | 0 | - | 0 | \spawn monster 1725 |
| 1726 | Phil Mason | accord | Human | 125033 | - | - | 0 | - | 0 | \spawn monster 1726 |
| 1727 | Lexie Smith | accord | Human | 125033 | - | - | 0 | - | 0 | \spawn monster 1727 |
| 1728 | - | accord | Human | 125033 | - | - | 0 | - | 0 | \spawn monster 1728 |
| 1729 | - | gaea | Wildlife | 33945 | - | - | 0 | 5704 / 5404 | 5 | \spawn monster 1729 |
| 1730 | Enraged Ankylot | melding | Melded | 96831 | 96832 / - | - | 0 | 5704 / - | 5 | \spawn monster 1730 |
| 1731 | SFC Terry Sugarman | accord | Human | 105234 | 78076 / - | Arch_MedRangedHumanoid_Base | 0 | - | 0 | \spawn monster 1731 |
| 1732 | - | melding | Melded | 96833 | 96834 / - | - | 0 | 5724 / 5458 | 5 | \spawn monster 1732 |
| 1733 | Buzzard Biker | bandit | Outlaw | 125065 | 96842 / - | - | 0 | 5310 / 5386 | 0 | \spawn monster 1733 |
| 1734 | - | gaea | Wildlife | 97062 | 97063 / - | - | 0 | 5705 / 5415 | 5 | \spawn monster 1734 |
| 1735 | Buzzard Brawler | bandit | Outlaw | 113506 | 105231 / - | Arch_AdditiveMelee_Base | 0 | 5311 / 5387 | 5 | \spawn monster 1735 |
| 1736 | - | gaea | Wildlife | 97261 | - | - | 0 | - / 6213 | 5 | \spawn monster 1736 |
| 1737 | Buzzard Hellion | bandit | Outlaw | 125146 | 140284 / - | - | 0 | 5312 / 5388 | 0 | \spawn monster 1737 |
| 1738 | - | gaea | Wildlife | 92850 | - | - | 0 | 5704 / - | 5 | \spawn monster 1738 |
| 1739 | - | bandit | Outlaw | 95152 | 96844 / - | Arch_AdditiveMelee_Base | 0 | 5311 / 5387 | 5 | \spawn monster 1739 |
| 1740 | - | bandit | Outlaw | 95152 | 96845 / - | Arch_MedRangedHumanoid_Base | 0 | 5311 / 5387 | 5 | \spawn monster 1740 |
| 1741 | Bandit Punk | bandit | Outlaw | 125065 | 96842 / - | - | 0 | 5310 / 5386 | 0 | \spawn monster 1741 |
| 1742 | Bandit Enforcer | bandit | Outlaw | 125069 | 96847 / - | - | 0 | 5311 / 5387 | 0 | \spawn monster 1742 |
| 1743 | Bandit Grenadier | bandit | Outlaw | 125146 | 96848 / - | - | 0 | 5312 / 5388 | 0 | \spawn monster 1743 |
| 1744 | Chimera Addict | bandit | Outlaw | 125065 | 96842 / - | - | 0 | 5310 / 5386 | 0 | \spawn monster 1744 |
| 1745 | Chimera Smuggler | bandit | Outlaw | 125069 | 96847 / - | - | 0 | 5311 / 5387 | 0 | \spawn monster 1745 |
| 1746 | - | gaea | Wildlife | 96856 | 96691 / - | - | 0 | 5704 / - | 5 | \spawn monster 1746 |
| 1747 | Chimera Hacker | bandit | Outlaw | 141005 | 96854 / - | - | 0 | 5311 / 5386 | 0 | \spawn monster 1747 |
| 1748 | - | bandit | Outlaw | 96851 | 96857 / - | Arch_MedRangedHumanoid_Base | 0 | 5311 / 5386 | 0 | \spawn monster 1748 |
| 1749 | Guard Rasper | gaea | Wildlife | 96858 | 20046 / - | - | 0 | 5724 / 5458 | 5 | \spawn monster 1749 |
| 1750 | Hyena Scavenger | gaea | Wildlife | 97366 | 66846 / - | FeralCanineSquad | 0 | 5704 / 6211 | 5 | \spawn monster 1750 |
| 1751 | - | gaea | Wildlife | 96861 | 66846 / - | - | 0 | 5705 / 6212 | 5 | \spawn monster 1751 |
| 1752 | - | gaea | Wildlife | 96859 | 66846 / - | FeralCanineSquad | 0 | 5704 / 6211 | 5 | \spawn monster 1752 |
| 1753 | - | gaea | Wildlife | 96862 | 97368 / - | - | 0 | 5705 / 6212 | 5 | \spawn monster 1753 |
| 1754 | Centauri Tick Soldier | gaea | Wildlife | 122828 | 122829 / - | - | 0 | 5704 / - | 5 | \spawn monster 1754 |
| 1755 | Centauri Tick Spinner | gaea | Wildlife | 122830 | 122831 / - | - | 0 | 5704 / - | 5 | \spawn monster 1755 |
| 1756 | Blackhat Leader | bandit | Outlaw | 96865 | 96866 / - | Arch_MedRangedHumanoid_Base | 0 | 5311 / 5386 | 0 | \spawn monster 1756 |
| 1757 | Tanken Assassin | bandit | Outlaw | 96867 | 96868 / - | Arch_AdditiveMelee_Base | 0 | 5311 / 5387 | 5 | \spawn monster 1757 |
| 1758 | Tanken Gunman | Tanken | Outlaw | 113499 | 96842 / - | - | 0 | 5311 / 5386 | 0 | \spawn monster 1758 |
| 1759 | - | bandit | Outlaw | 96869 | 96870 / - | Arch_MedRangedHumanoid_Base | 0 | 5311 / 5386 | 0 | \spawn monster 1759 |
| 1760 | Tanken Sniper | Tanken | Outlaw | 113500 | 96871 / 96842 | - | 0 | 5310 / 5386 | 0 | \spawn monster 1760 |
| 1761 | Tanken Master | bandit | Outlaw | 96872 | 96873 / - | Arch_AdditiveMelee_Base | 0 | 5311 / 5387 | 5 | \spawn monster 1761 |
| 1762 | Ophanim Barrier Drone | Ophanim | Outlaw | 96878 | - | - | 0 | 5310 / 6450 | 5 | \spawn monster 1762 |
| 1763 | Reaper Captain | Reapers | Outlaw | 125034 | 106335 / - | - | 0 | 5313 / 5389 | 5 | \spawn monster 1763 |
| 1764 | Reaper Parrot | Reapers | Outlaw | 96882 | 124773 / - | ChosenEngineerDrone | 0 | 5702 / 5385 | 5 | \spawn monster 1764 |
| 1765 | Rebel Rioter | Rebels | Outlaw | 97298 | 96848 / - | - | 0 | 5311 / 5387 | 5 | \spawn monster 1765 |
| 1766 | Black Hills Outlaw | Black Hills Bandits | Outlaw | 125069 | 96847 / - | - | 0 | 5311 / 5387 | 5 | \spawn monster 1766 |
| 1767 | Black Hills Hound | Black Hills Bandits | Outlaw | 96891 | 66846 / - | - | 0 | 6211 / 6210 | 0 | \spawn monster 1767 |
| 1768 | Black Hills Hound Wrangler | Black Hills Bandits | Outlaw | 97057 | 96842 / - | - | 0 | 5311 / 5387 | 5 | \spawn monster 1768 |
| 1769 | - | gaea | Wildlife | 96894 | 96895 / - | Arch_FullbodyMelee_Base | 0 | 5726 / 5778 | 5 | \spawn monster 1769 |
| 1770 | - | gaea | Wildlife | 96894 | 96897 / - | Arch_MedRanged_Base | 0 | 5726 / 5778 | 5 | \spawn monster 1770 |
| 1771 | - | gaea | Wildlife | 96899 | - | Arch_FullbodyMelee_Base | 0 | 5726 / 5778 | 5 | \spawn monster 1771 |
| 1772 | - | gaea | Wildlife | 96894 | 96900 / - | Arch_FullbodyMelee_Base | 0 | 5726 / 5778 | 5 | \spawn monster 1772 |
| 1773 | - | gaea | Wildlife | 96901 | 96691 / - | - | 0 | 5704 / 5432 | 5 | \spawn monster 1773 |
| 1774 | Rockhorn Slinger | gaea | Wildlife | 96901 | 96903 / - | - | 0 | 5704 / 5432 | 5 | \spawn monster 1774 |
| 1775 | Rockhorn Guardian | gaea | Wildlife | 96904 | 96906 / - | Arch_MedRanged_Base | 0 | 5704 / 5432 | 5 | \spawn monster 1775 |
| 1776 | - | gaea | Wildlife | 96907 | 96908 / - | - | 0 | 5704 / 5414 | 5 | \spawn monster 1776 |
| 1777 | Direhornet | gaea | Wildlife | 97030 | 105232 / - | - | 0 | 5704 / 5414 | 5 | \spawn monster 1777 |
| 1778 | - | gaea | Wildlife | 96910 | - | - | 0 | 5704 / 5414 | 5 | \spawn monster 1778 |
| 1779 | - | gaea | Wildlife | 96911 | - | - | 0 | 5704 / 5414 | 5 | \spawn monster 1779 |
| 1780 | - | gaea | Wildlife | 96912 | 34089 / - | - | 0 | 5704 / 5414 | 5 | \spawn monster 1780 |
| 1781 | - | gaea | Wildlife | 96916 | 85661 / - | - | 0 | 5725 / - | 0 | \spawn monster 1781 |
| 1782 | Firecat | gaea | Wildlife | 96915 | 96917 / - | - | 0 | 5704 / - | 5 | \spawn monster 1782 |
| 1783 | - | gaea | Wildlife | 96918 | - | - | 0 | 5704 / 6211 | 5 | \spawn monster 1783 |
| 1784 | The Scorpion King | gaea | Wildlife | 96919 | 110771 / - | - | 0 | 5725 / - | 0 | \spawn monster 1784 |
| 1785 | - | gaea | Wildlife | 96922 | 85661 / - | - | 0 | 5725 / - | 0 | \spawn monster 1785 |
| 1786 | - | gaea | Wildlife | 96918 | - | - | 0 | 5704 / 6211 | 5 | \spawn monster 1786 |
| 1787 | - | gaea | Wildlife | 96924 | 97270 / - | - | 0 | 2 / - | 0 | \spawn monster 1787 |
| 1788 | - | gaea | Wildlife | 96915 | 110764 / - | - | 0 | 5704 / 6211 | 5 | \spawn monster 1788 |
| 1789 | - | gaea | Wildlife | 96928 | 96927 / - | - | 0 | 5704 / 6211 | 5 | \spawn monster 1789 |
| 1790 | - | gaea | Wildlife | 96924 | - | - | 0 | 2 / - | 0 | \spawn monster 1790 |
| 1791 | Flamethrower Firejacket | gaea | Wildlife | 82582 | 96930 / - | - | 0 | 5724 / 5414 | 5 | \spawn monster 1791 |
| 1792 | Astrek Courier | accord | Human | 117996 | 78061 / - | Arch_MedRangedHumanoid_Attack | 0 | - | 0 | \spawn monster 1792 |
| 1793 | Volatile Scorcher | gaea | Wildlife | 96931 | 82608 / - | - | 0 | 5702 / 5779 | 5 | \spawn monster 1793 |
| 1794 | OBSOLETE Hellfire Scorcher | gaea | Wildlife | 118726 | 120953 / - | - | 0 | 5703 / 5779 | 5 | \spawn monster 1794 |
| 1795 | Large Firejacket | gaea | Wildlife | 96932 | - | - | 0 | 5724 / 5414 | 5 | \spawn monster 1795 |
| 1796 | Buzzard Contact | bandit | Outlaw | 95152 | - / 67423 | - | 0 | 5311 / 5386 | 0 | \spawn monster 1796 |
| 1797 | Firebreath Scorcher | gaea | Wildlife | 96936 | 124587 / - | - | 0 | 5704 / 5779 | 5 | \spawn monster 1797 |
| 1798 | - | gaea | Wildlife | 97265 | 97269 / - | - | 0 | 5705 / 6212 | 5 | \spawn monster 1798 |
| 1799 | - | gaea | Wildlife | 96937 | 97430 / - | - | 0 | 5705 / - | 5 | \spawn monster 1799 |
| 1800 | ARES Soldier | accord | Human | 117622 | 79272 / - | - | 0 | - | 0 | \spawn monster 1800 |
| 1801 | Turner Jones | accord | Human | 96942 | 96488 / - | AlertAndInteractive | 0 | 5311 / 5387 | 5 | \spawn monster 1801 |
| 1802 | Razor | accord | Human | 52458 | - | PeacetimeCityWanderer | 0 | - | 0 | \spawn monster 1802 |
| 1803 | - | accord | Human | 96943 | - | AlertAndInteractive | 0 | - | 0 | \spawn monster 1803 |
| 1804 | - | bandit | Outlaw | 86377 | 75116 / - | Arch_MedRangedHumanoid_Base | 0 | 5311 / - | 0 | \spawn monster 1804 |
| 1805 | Sergeant James Mansfield | accord | Human | 114625 | 32739 / - | AlertAndInteractive | 0 | - | 0 | \spawn monster 1805 |
| 1806 | Lieutenant Julian Friese | accord | Human | 114626 | 32741 / - | AlertAndInteractive | 0 | - | 0 | \spawn monster 1806 |
| 1807 | Sergeant Burton | accord | Human | 114627 | 77049 / - | AlertAndInteractive | 0 | - | 0 | \spawn monster 1807 |
| 1808 | Chosen Archon | chosen | Chosen | 96953 | 87382 / - | Arch_MedRangedAbilityUser_Base | 0 | 5700 / 5371 | 5 | \spawn monster 1808 |
| 1809 | SIN Virus | bandit | Outlaw | 96948 | - / 114632 | - | 0 | 5313 / - | 0 | \spawn monster 1809 |
| 1810 | SIN Phantom | bandit | Outlaw | 125065 | 96842 / - | - | 0 | 2 / - | 0 | \spawn monster 1810 |
| 1811 | Traitorous Dreadnaught | bandit | Outlaw | 97237 | 106335 / - | - | 0 | 5313 / - | 0 | \spawn monster 1811 |
| 1812 | SIN Phantom | bandit | Outlaw | 125146 | 122968 / - | - | 0 | 4 / - | 0 | \spawn monster 1812 |
| 1813 | - | monster | Misc | 96957 | - | Arch_AdditiveMelee_Base | 0 | 1 / - | 0 | \spawn monster 1813 |
| 1814 | Sergeant Rice | accord | Human | 110766 | 78065 / - | AlertAndInteractive | 0 | - | 0 | \spawn monster 1814 |
| 1815 | - | bandit | Outlaw | 75281 | 67425 / - | Arch_MedRangedHumanoid_Base | 0 | - | 0 | \spawn monster 1815 |
| 1816 | - | bandit | Outlaw | 75281 | 67425 / - | Arch_MedRangedHumanoid_Base | 0 | - | 0 | \spawn monster 1816 |
| 1817 | - | bandit | Outlaw | 75281 | 67425 / - | Arch_MedRangedHumanoid_Base | 0 | - | 0 | \spawn monster 1817 |
| 1818 | Cerrado Norte Bandit | bandit | Outlaw | 75281 | 67425 / - | Arch_MedRangedHumanoid_Base | 0 | - | 0 | \spawn monster 1818 |
| 1819 | - | accord | Human | 96913 | - | AlertAndInteractive | 0 | - | 0 | \spawn monster 1819 |
| 1820 | Jace Jackson | accord | Human | 125033 | - | - | 0 | - | 0 | \spawn monster 1820 |
| 1821 | Supply Officer Chan | accord | Human | 97019 | - | AlertAndInteractive | 0 | - | 5 | \spawn monster 1821 |
| 1822 | Supply Officer Stocks | accord | Human | 97021 | - | AlertAndInteractive | 0 | - | 0 | \spawn monster 1822 |
| 1823 | Supply Officer "Shadow" | accord | Human | 97602 | - | AlertAndInteractive | 0 | - | 5 | \spawn monster 1823 |
| 1824 | Mickey the Wrench | bandit | Outlaw | 142074 | 117042 / - | - | 10005 | 5313 / 5389 | 0 | \spawn monster 1824 |
| 1825 | Private Richard Furtado | accord | Human | 117614 | 95462 / - | AlertAndInteractive | 0 | - | 0 | \spawn monster 1825 |
| 1826 | Private Lee Palmer | accord | Human | 10004 | 87943 / - | AlertAndInteractive | 0 | - | 0 | \spawn monster 1826 |
| 1827 | Lieutenant Kimberley Lyons | accord | Human | 117615 | 87927 / - | AlertAndInteractive | 0 | - | 0 | \spawn monster 1827 |
| 1828 | Private Janet Moore | accord | Human | 117616 | 95462 / - | Arch_MedRangedHumanoid_Base | 0 | - | 0 | \spawn monster 1828 |
| 1829 | Private Benny Blair | accord | Human | 117617 | 87943 / - | Arch_MedRangedHumanoid_Base | 0 | - | 0 | \spawn monster 1829 |
| 1830 | Lieutenant Irene Smith | accord | Human | 117618 | 87927 / - | Arch_MedRangedHumanoid_Base | 0 | - | 0 | \spawn monster 1830 |
| 1831 | Private Maria North | accord | Human | 10001 | 87927 / - | AlertAndInteractive | 0 | - | 0 | \spawn monster 1831 |
| 1832 | Private Christopher Carter | accord | Human | 10004 | 87943 / - | AlertAndInteractive | 0 | - | 0 | \spawn monster 1832 |
| 1833 | Lieutenant Neil Gooding | accord | Human | 10002 | 87923 / - | AlertAndInteractive | 0 | - | 0 | \spawn monster 1833 |
| 1834 | SIN Hijacker | accord | Human | 97060 | 96848 / - | Arch_MedRangedHumanoid_Base | 0 | - | 0 | \spawn monster 1834 |
| 1835 | Colonel Havel | accord | Human | 97065 | 78435 / - | Arch_MedRangedHumanoid_Base | 0 | - | 0 | \spawn monster 1835 |
| 1836 | Scientist Alvarez | accord | Human | 81372 | - | AlertAndInteractive | 0 | - | 0 | \spawn monster 1836 |
| 1837 | Traitorous Marine | bandit | Outlaw | 97236 | 96842 / - | - | 0 | 5310 / - | 0 | \spawn monster 1837 |
| 1838 | Traitorous Assault | bandit | Outlaw | 97238 | 122968 / - | - | 0 | 5312 / - | 0 | \spawn monster 1838 |
| 1839 | SIN Phantom | bandit | Outlaw | 125034 | 106335 / - | - | 0 | 5 / - | 0 | \spawn monster 1839 |
| 1840 | Astrek Scientist | accord | Human | 118001 | - | AlertAndInteractive | 0 | - | 0 | \spawn monster 1840 |
| 1841 | Astrek Scientist | accord | Human | 118002 | - | AlertAndInteractive | 0 | - | 0 | \spawn monster 1841 |
| 1842 | Astrek Scientist | accord | Human | 118004 | - | AlertAndInteractive | 0 | - | 0 | \spawn monster 1842 |
| 1843 | - | gaea | Wildlife | 97277 | 97270 / - | - | 0 | 2 / - | 0 | \spawn monster 1843 |
| 1844 | - | chosen | Chosen | 82463 | 82484 / - | Arch_MedRangedHumanoid_Base | 0 | 5700 / 5371 | 5 | \spawn monster 1844 |
| 1845 | - | bandit | Outlaw | 96456 | 81305 / - | AlertAndInteractive | 0 | 5311 / 5388 | 5 | \spawn monster 1845 |
| 1846 | - | accord | Human | 52461 | - | AlertAndInteractive | 0 | - | 0 | \spawn monster 1846 |
| 1847 | - | accord | Human | 52454 | - | AlertAndInteractive | 0 | - | 0 | \spawn monster 1847 |
| 1848 | - | chosen | Chosen | 33958 | 32741 / - | Arch_MedRangedHumanoid_Base | 0 | 5699 / 5370 | 5 | \spawn monster 1848 |
| 1849 | Speedy Steve | bandit | Outlaw | 97342 | 122621 / - | Arch_MedRangedHumanoid_Base | 0 | - | 0 | \spawn monster 1849 |
| 1850 | Twitchy Ted | bandit | Outlaw | 97344 | 84917 / - | Arch_MedRangedHumanoid_Base | 0 | - | 0 | \spawn monster 1850 |
| 1851 | Bouncing Betty | bandit | Outlaw | 97415 | 81308 / - | Arch_MedRangedHumanoid_Base | 0 | - | 0 | \spawn monster 1851 |
| 1852 | - | accord | Human | 97356 | 82484 / - | Arch_MedRangedHumanoid_Attack | 0 | - | 0 | \spawn monster 1852 |
| 1853 | - | chosen | Chosen | 97358 | 56826 / - | Arch_AdditiveMelee_Base | 0 | 5700 / 5371 | 0 | \spawn monster 1853 |
| 1854 | - | chosen | Chosen | 97360 | 32741 / - | Arch_MedRangedHumanoid_Base | 0 | 5699 / 5370 | 5 | \spawn monster 1854 |
| 1855 | Jacques Voclain | accord | Human | 97271 | - | AlertAndInteractive | 0 | - | 0 | \spawn monster 1855 |
| 1856 | Greasy Hank | accord | Human | 86380 | 67423 / - | PeacetimeCityWanderer | 0 | - | 0 | \spawn monster 1856 |
| 1857 | Captain Wallach | accord | Human | 106353 | - | AlertAndInteractive | 0 | - | 0 | \spawn monster 1857 |
| 1858 | Private Milani | accord | Human | 118029 | 87943 / - | AlertAndInteractive | 0 | - | 0 | \spawn monster 1858 |
| 1859 | - | accord | Human | 81391 | 84915 / - | GuardCityWanderer | 0 | - | 0 | \spawn monster 1859 |
| 1860 | - | accord | Human | 81381 | - | PeacetimeCityWanderer | 0 | - | 0 | \spawn monster 1860 |
| 1861 | - | accord | Human | 81381 | - | PeacetimeCityWanderer | 0 | - | 0 | \spawn monster 1861 |
| 1862 | - | Black Hills Bandits | Outlaw | 75281 | 96847 / - | Arch_MedRangedHumanoid_Base | 0 | 5311 / 5386 | 5 | \spawn monster 1862 |
| 1863 | Echo Tracking Drone | friendly | Companion | 118028 | - | - | 0 | - | 0 | \spawn monster 1863 |
| 1864 | Shanty Town Civilian | accord | Human | 117010 | - | BasicCivilian_Stationary | 0 | - | 0 | \spawn monster 1864 |
| 1865 | Commander Volkov | accord | Human | 106611 | - | AlertAndInteractive | 0 | - | 0 | \spawn monster 1865 |
| 1866 | - | accord | Human | 97475 | - / 76108 | AlertAndInteractive | 0 | - | 0 | \spawn monster 1866 |
| 1867 | ARES Pilot | accord | Human | 118025 | 77338 / - | PeacetimeCityWanderer | 0 | - | 0 | \spawn monster 1867 |
| 1868 | ARES Pilot | accord | Human | 118026 | 87027 / - | PeacetimeCityWanderer | 0 | - | 0 | \spawn monster 1868 |
| 1869 | Stavrevski | accord | Human | 117608 | 82345 / - | Null | 0 | - | 0 | \spawn monster 1869 |
| 1870 | - | accord | Human | 33874 | 20034 / - | PeacetimeCityWanderer | 0 | - | 0 | \spawn monster 1870 |
| 1871 | Corporal Rhodes | accord | Human | 118014 | 79272 / - | GuardCityWanderer | 0 | - | 0 | \spawn monster 1871 |
| 1872 | Private Harper | accord | Human | 118014 | 78063 / - | Arch_MedRangedHumanoid_Attack | 0 | - | 0 | \spawn monster 1872 |
| 1873 | Private Morine | accord | Human | 118014 | 78403 / - | Arch_MedRangedHumanoid_Attack | 0 | - | 0 | \spawn monster 1873 |
| 1874 | Private McNeill | accord | Human | 118014 | 79272 / - | GuardCityWanderer | 0 | - | 0 | \spawn monster 1874 |
| 1875 | - | bandit | Outlaw | 96869 | 96847 / - | Arch_MedRangedHumanoid_Base | 0 | 5311 / 5386 | 0 | \spawn monster 1875 |
| 1876 | - | bandit | Outlaw | 96869 | 96848 / - | Arch_MedRangedHumanoid_Base | 0 | 5311 / 5386 | 0 | \spawn monster 1876 |
| 1877 | Buzzard Chaingunner | bandit | Outlaw | 125034 | 106335 / - | - | 0 | 5313 / 5389 | 0 | \spawn monster 1877 |
| 1878 | - | accord | Human | 97262 | - | PeacetimeCityWanderer | 0 | - | 0 | \spawn monster 1878 |
| 1879 | Remus | bandit | Outlaw | 96869 | 96842 / - | Arch_MedRangedAbilityUser_Base | 0 | 5311 / 5386 | 0 | \spawn monster 1879 |
| 1880 | Rebel Guerilla | Rebels | Outlaw | 125069 | 96847 / - | - | 0 | 5311 / 5387 | 5 | \spawn monster 1880 |
| 1881 | Rebel Leader | Rebels | Outlaw | 125034 | 106335 / - | - | 0 | 5313 / 5389 | 5 | \spawn monster 1881 |
| 1882 | Tanken Mobster | Tanken | Outlaw | 113501 | 96847 / - | - | 0 | 5311 / 5387 | 0 | \spawn monster 1882 |
| 1883 | Tanken Shogun | bandit | Outlaw | 113502 | 106335 / - | - | 0 | 5311 / 5386 | 0 | \spawn monster 1883 |
| 1884 | Chimera Grenadier | bandit | Outlaw | 125146 | 96848 / - | - | 0 | 5312 / 5388 | 0 | \spawn monster 1884 |
| 1885 | Chimera Dreadnaught | bandit | Outlaw | 125034 | 106335 / - | - | 0 | 5313 / 5389 | 0 | \spawn monster 1885 |
| 1886 | Bandit Dreadnaught | bandit | Outlaw | 125034 | 106335 / - | - | 0 | 5313 / 5389 | 0 | \spawn monster 1886 |
| 1887 | Lieutenant Sadiq | accord | Human | 106610 | 78403 / - | AlertAndInteractive | 0 | - | 0 | \spawn monster 1887 |
| 1888 | Commander Auttenberg | accord | Human | 107714 | - | AlertAndInteractive | 0 | - | 0 | \spawn monster 1888 |
| 1889 | Captain Park | accord | Human | 106612 | - | AlertAndInteractive | 0 | - | 0 | \spawn monster 1889 |
| 1890 | Master Sgt. Rask | accord | Human | 107701 | 78403 / - | AlertAndInteractive | 0 | - | 0 | \spawn monster 1890 |
| 1891 | - | accord | Human | 30036 | 30025 / 20015 | PlayerPet | 0 | - | 5 | \spawn monster 1891 |
| 1892 | First Lieutenant Avakian | accord | Human | 97018 | - | AlertAndInteractive | 0 | - | 0 | \spawn monster 1892 |
| 1893 | Corporal Kawaguchi | accord | Human | 81393 | 79272 / - | GuardCityWanderer | 0 | - | 0 | \spawn monster 1893 |
| 1894 | Private Hans | accord | Human | 81393 | 79272 / - | GuardCityWanderer | 0 | - | 0 | \spawn monster 1894 |
| 1895 | Anomalous Collections Officer | accord | Human | 107715 | - | AlertAndInteractive | 0 | - | 0 | \spawn monster 1895 |
| 1896 | Mourningstar | accord | Human | 107772 | 78063 / - | GuardCityWanderer | 0 | 17 / - | 0 | \spawn monster 1896 |
| 1897 | Typhon | accord | Human | 107773 | 85735 / - | Arch_MoveThenFire_Base | 0 | 17 / - | 0 | \spawn monster 1897 |
| 1898 | Scarlet | accord | Human | 117619 | 98468 / - | Arch_MoveThenFire_Base | 0 | - | 0 | \spawn monster 1898 |
| 1899 | Wiley | accord | Human | 118000 | - / 78063 | Arch_MedRangedHumanoid_Base | 0 | - | 0 | \spawn monster 1899 |
| 1900 | Natalia Fedorov | accord | Human | 0 | - | Null | 0 | - | 0 | \spawn monster 1900 |
| 1901 | Jackie "Juice" Greene | accord | Human | 113498 | - | AlertAndInteractive | 0 | - | 0 | \spawn monster 1901 |
| 1902 | FOB Harpoon Quartermaster | accord | Human | 118075 | - | AlertAndInteractive | 0 | - | 0 | \spawn monster 1902 |
| 1903 | Crossroads Quartermaster | accord | Human | 118075 | - | AlertAndInteractive | 0 | - | 0 | \spawn monster 1903 |
| 1904 | Research Station Quartermaster | accord | Human | 118075 | - | AlertAndInteractive | 0 | - | 0 | \spawn monster 1904 |
| 1905 | Camp Jasper Quartermaster | accord | Human | 118075 | - | AlertAndInteractive | 0 | - | 0 | \spawn monster 1905 |
| 1906 | Stronghold Quartermaster | accord | Human | 118075 | - | AlertAndInteractive | 0 | - | 0 | \spawn monster 1906 |
| 1907 | Forest Watch Quartermaster | accord | Human | 118075 | - | AlertAndInteractive | 0 | - | 0 | \spawn monster 1907 |
| 1908 | Glitch | friendly | Companion | 110767 | - | - | 0 | - | 0 | \spawn monster 1908 |
| 1909 | Accord Soldier | accord | Human | 118020 | 97685 / 76108 | AlertAndLookAtPlayer | 0 | - | 0 | \spawn monster 1909 |
| 1910 | Accord Soldier | accord | Human | 10004 | - / 76108 | AlertAndLookAtPlayer | 0 | - | 0 | \spawn monster 1910 |
| 1911 | Accord Soldier | accord | Human | 10001 | - / 76108 | AlertAndLookAtPlayer | 0 | - | 0 | \spawn monster 1911 |
| 1912 | Accord Soldier | accord | Human | 10003 | - / 76108 | AlertAndLookAtPlayer | 0 | - | 0 | \spawn monster 1912 |
| 1913 | Accord Soldier | accord | Human | 10002 | - / 76108 | AlertAndLookAtPlayer | 0 | - | 0 | \spawn monster 1913 |
| 1914 | Accord Soldier | accord | Human | 10004 | - / 76108 | AlertAndLookAtPlayer | 0 | - | 0 | \spawn monster 1914 |
| 1915 | - | accord | Human | 113542 | - / 86520 | Arch_MedRangedHumanoid_Base | 0 | - | 0 | \spawn monster 1915 |
| 1916 | - | accord | Human | 113547 | 86509 / 86509 | FaceAndAttack | 0 | - | 0 | \spawn monster 1916 |
| 1917 | - | accord | Human | 113548 | 79189 / 79189 | Arch_MedRangedHumanoid_Base | 0 | - | 0 | \spawn monster 1917 |
| 1918 | - | accord | Human | 113549 | - / 85972 | Arch_MedRangedHumanoid_Defend | 0 | - | 0 | \spawn monster 1918 |
| 1919 | - | accord | Human | 10003 | - / 78063 | Arch_MedRangedHumanoid_Base | 0 | - | 0 | \spawn monster 1919 |
| 1920 | Nutretic Technician | accord | Human | 96979 | - | AlertAndInteractive | 0 | - | 0 | \spawn monster 1920 |
| 1921 | Fulton Kwok | accord | Human | 118128 | - | AlertAndInteractive | 0 | - | 0 | \spawn monster 1921 |
| 1922 | - | accord | Human | 113542 | - / 86520 | Arch_MedRangedHumanoid_Base | 0 | - | 0 | \spawn monster 1922 |
| 1923 | Ikinya | accord | Human | 10003 | - | AlertAndInteractive | 0 | - | 0 | \spawn monster 1923 |
| 1924 | Cognac | accord | Human | 106330 | - | AlertAndInteractive | 0 | - | 0 | \spawn monster 1924 |
| 1925 | _Accord Assault | accord | Human | 114361 | - / 79272 | HolsterWeapon | 0 | - | 0 | \spawn monster 1925 |
| 1926 | Sheriff Fairuza Nasseri | accord | Human | 114362 | 67423 / - | Arch_MedRangedHumanoid_Base | 0 | - | 0 | \spawn monster 1926 |
| 1927 | - | accord | Human | 76332 | 98213 / - | - | 0 | - | 0 | \spawn monster 1927 |
| 1928 | - | accord | Human | 76132 | - | AlertAndInteractive | 0 | - | 0 | \spawn monster 1928 |
| 1929 | - | accord | Human | 114395 | - / 83446 | PerformEmote | 0 | - | 0 | \spawn monster 1929 |
| 1930 | - | accord | Human | 114396 | - / 78357 | PerformEmote | 0 | - | 0 | \spawn monster 1930 |
| 1931 | - | accord | Human | 114397 | - / 79189 | PerformEmote | 0 | - | 0 | \spawn monster 1931 |
| 1932 | _Accord Engineer | accord | Human | 114398 | - / 79165 | HolsterWeapon | 0 | - | 0 | \spawn monster 1932 |
| 1933 | Supply Officer Jones | accord | Human | 97602 | - | AlertAndInteractive | 0 | - | 5 | \spawn monster 1933 |
| 1934 | Supply Officer Schwartz | accord | Human | 97021 | - | AlertAndInteractive | 0 | - | 0 | \spawn monster 1934 |
| 1935 | Supply Officer "Raptor" | accord | Human | 97602 | - | AlertAndInteractive | 0 | - | 5 | \spawn monster 1935 |
| 1936 | Supply Officer Bender | accord | Human | 96715 | - | AlertAndInteractive | 0 | - | 0 | \spawn monster 1936 |
| 1937 | Supply Officer White | accord | Human | 96715 | - | AlertAndInteractive | 0 | - | 0 | \spawn monster 1937 |
| 1938 | Accord Guard | accord | Human | 117035 | 84915 / - | OneOff_FireAtEnemy | 0 | - | 0 | \spawn monster 1938 |
| 1939 | Supply Officer Owens | accord | Human | 82633 | - | AlertAndInteractive | 0 | - | 0 | \spawn monster 1939 |
| 1940 | Supply Officer Wachi | accord | Human | 82637 | - | AlertAndInteractive | 0 | - | 0 | \spawn monster 1940 |
| 1941 | Supply Officer Smith | accord | Human | 117023 | - | AlertAndInteractive | 0 | - | 0 | \spawn monster 1941 |
| 1942 | Supply Officer Reed | accord | Human | 96715 | - | AlertAndInteractive | 0 | - | 0 | \spawn monster 1942 |
| 1943 | Supply Officer Bryson | accord | Human | 82637 | - | AlertAndInteractive | 0 | - | 0 | \spawn monster 1943 |
| 1944 | Supply Officer Mandel | accord | Human | 96725 | - | AlertAndInteractive | 0 | - | 0 | \spawn monster 1944 |
| 1945 | Supply Officer Wyland | accord | Human | 96715 | - | AlertAndInteractive | 0 | - | 0 | \spawn monster 1945 |
| 1946 | Dredge Bartender | accord | Human | 81330 | - | AlertAndInteractive | 0 | - | 0 | \spawn monster 1946 |
| 1947 | - | accord | Human | 81378 | - | AlertAndLookAtPlayer | 0 | - | 0 | \spawn monster 1947 |
| 1948 | - | accord | Human | 114608 | - | AlertAndLookAtPlayer | 0 | - | 0 | \spawn monster 1948 |
| 1949 | - | accord | Human | 114609 | - | AlertAndLookAtPlayer | 0 | - | 0 | \spawn monster 1949 |
| 1950 | - | accord | Human | 81370 | - | AlertAndLookAtPlayer | 0 | - | 0 | \spawn monster 1950 |
| 1951 | - | accord | Human | 81367 | - | AlertAndLookAtPlayer | 0 | - | 0 | \spawn monster 1951 |
| 1952 | - | accord | Human | 81390 | - | AlertAndLookAtPlayer | 0 | - | 0 | \spawn monster 1952 |
| 1953 | - | accord | Human | 77872 | - | AlertAndLookAtPlayer | 0 | - | 0 | \spawn monster 1953 |
| 1954 | - | accord | Human | 78971 | - | AlertAndLookAtPlayer | 0 | - | 0 | \spawn monster 1954 |
| 1955 | - | accord | Human | 81324 | - | AlertAndInteractive | 0 | - | 0 | \spawn monster 1955 |
| 1956 | - | accord | Human | 81367 | - | AlertAndLookAtPlayer | 0 | - | 0 | \spawn monster 1956 |
| 1957 | - | accord | Human | 81367 | - | AlertAndLookAtPlayer | 0 | - | 0 | \spawn monster 1957 |
| 1958 | Datascanner Operator 2 - Civilian | accord | Human | 81343 | - | AlertAndInteractive | 0 | - | 0 | \spawn monster 1958 |
| 1959 | Datascanner Operator 1- Civilian | accord | Human | 82397 | - | AlertAndInteractive | 0 | - | 0 | \spawn monster 1959 |
| 1960 | Injured ARES Pilot | accord | Human | 118027 | 86742 / - | AlertAndInteractive | 0 | - | 0 | \spawn monster 1960 |
| 1961 | _Diamond Head Male Accord Soldier Randomized | accord | Human | 85673 | 84917 / - | - | 0 | - | 0 | \spawn monster 1961 |
| 1962 | _Diamond Head Female Accord Soldier Randomized | accord | Human | 85673 | 84917 / - | - | 0 | - | 0 | \spawn monster 1962 |
| 1963 | - | accord | Human | 114622 | - | AlertAndLookAtPlayer | 0 | - | 0 | \spawn monster 1963 |
| 1964 | - | accord | Human | 81393 | 79272 / - | GuardCityWanderer | 0 | - | 0 | \spawn monster 1964 |
| 1965 | Dredge Accord Guard | accord | Human | 77858 | 78495 / - | AlertAndLookAtPlayer | 0 | - | 5 | \spawn monster 1965 |
| 1966 | - | accord | Human | 77862 | - | AlertAndLookAtPlayer | 0 | - | 0 | \spawn monster 1966 |
| 1967 | - | accord | Human | 77862 | - | AlertAndLookAtPlayer | 0 | - | 0 | \spawn monster 1967 |
| 1968 | - | accord | Human | 77862 | - | AlertAndLookAtPlayer | 0 | - | 0 | \spawn monster 1968 |
| 1969 | - | accord | Human | 77862 | - | AlertAndLookAtPlayer | 0 | - | 0 | \spawn monster 1969 |
| 1970 | - | accord | Human | 77862 | - | AlertAndLookAtPlayer | 0 | - | 0 | \spawn monster 1970 |
| 1971 | - | accord | Human | 77862 | - | AlertAndLookAtPlayer | 0 | - | 0 | \spawn monster 1971 |
| 1972 | - | accord | Human | 95470 | 96458 / 96457 | AlertAndInteractive | 0 | 5311 / 5386 | 5 | \spawn monster 1972 |
| 1973 | - | accord | Human | 81373 | - | PeacetimeCityWanderer | 0 | - | 0 | \spawn monster 1973 |
| 1974 | - | accord | Human | 81373 | - | PeacetimeCityWanderer | 0 | - | 0 | \spawn monster 1974 |
| 1975 | - | accord | Human | 81373 | - | PeacetimeCityWanderer | 0 | - | 0 | \spawn monster 1975 |
| 1976 | - | accord | Human | 114634 | - / 76108 | GuardCityWanderer | 0 | - | 0 | \spawn monster 1976 |
| 1977 | - | accord | Human | 10003 | - / 76108 | GuardCityWanderer | 0 | - | 0 | \spawn monster 1977 |
| 1978 | Adm. Curtis Mokiao | accord | Human | 85686 | 85687 / - | Arch_MedRangedHumanoid_Attack | 0 | - | 0 | \spawn monster 1978 |
| 1979 | - | accord | Human | 10004 | - / 76108 | GuardCityWanderer | 0 | - | 0 | \spawn monster 1979 |
| 1980 | - | accord | Human | 10001 | - / 76108 | GuardCityWanderer | 0 | - | 0 | \spawn monster 1980 |
| 1981 | - | accord | Human | 10002 | - / 76108 | GuardCityWanderer | 0 | - | 0 | \spawn monster 1981 |
| 1982 | Capt. Patel | accord | Human | 118031 | 96847 / - | Arch_MedRangedHumanoid_Base | 0 | - | 0 | \spawn monster 1982 |
| 1983 | Nightmare Nanu | friendly | Companion | 116544 | - | PassivePet | 0 | - | 0 | \spawn monster 1983 |
| 1984 | Howie | chosen | Chosen | 116548 | 88155 / - | Arch_MoveThenFire_Base | 0 | 5700 / 5371 | 5 | \spawn monster 1984 |
| 1985 | Scanbot | accord | Human | 118024 | - | - | 0 | - | 0 | \spawn monster 1985 |
| 1986 | Vic the Crow | accord | Human | 116555 | - | AlertAndInteractive | 0 | - | 0 | \spawn monster 1986 |
| 1987 | Heavy Turret II - Gunner | accord | Human | 33976 | - / 116561 | AggressiveWanderer | 0 | - | 0 | \spawn monster 1987 |
| 1988 | Accord VIP | accord | Human | 118022 | - | - | 0 | - | 0 | \spawn monster 1988 |
| 1989 | Camera Bot | friendly | Companion | 116669 | - | Null | 0 | - | 0 | \spawn monster 1989 |
| 1990 | Shanty Town Civilian | accord | Human | 117010 | - | BasicCivilian_Stationary | 0 | - | 0 | \spawn monster 1990 |
| 1991 | FOB Sagan Civilian - Male | accord | Human | 117010 | - | AlertAndLookAtPlayer | 0 | - | 0 | \spawn monster 1991 |
| 1992 | Accord Lieutenant | accord | Human | 117610 | 98471 / 30025 | - | 0 | - | 0 | \spawn monster 1992 |
| 1993 | The Cortador | Black Hills Bandits | Outlaw | 116927 | 122621 / - | - | 0 | 5313 / 5389 | 5 | \spawn monster 1993 |
| 1994 | Shanty Town Shepherd | accord | Human | 140989 | 84915 / - | GuardCityWanderer | 0 | - | 0 | \spawn monster 1994 |
| 1995 | Rebel Technician | Rebels | Outlaw | 81304 | 106335 / - | - | 0 | 5311 / 5388 | 5 | \spawn monster 1995 |
| 1996 | FOB Sagan Civilian - Female | accord | Human | 117010 | - | AlertAndLookAtPlayer | 0 | - | 0 | \spawn monster 1996 |
| 1997 | Black Hills Recruiter | Black Hills Bandits | Outlaw | 75281 | - | StationaryCivilianDialog | 0 | - | 0 | \spawn monster 1997 |
| 1998 | - | accord | Human | 117010 | - | AlertAndLookAtPlayer | 0 | - | 0 | \spawn monster 1998 |
| 1999 | - | accord | Human | 117010 | - | AlertAndLookAtPlayer | 0 | - | 0 | \spawn monster 1999 |
| 2000 | - | accord | Human | 117010 | - | AlertAndLookAtPlayer | 0 | - | 0 | \spawn monster 2000 |
| 2001 | - | accord | Human | 117010 | - | AlertAndLookAtPlayer | 0 | - | 0 | \spawn monster 2001 |
| 2002 | Accord Soldier | - | Human | 117024 | - | AlertAndLookAtPlayer | 0 | - | 0 | \spawn monster 2002 |
| 2003 | Accord Soldier | accord | Human | 117023 | - | AlertAndLookAtPlayer | 0 | - | 0 | \spawn monster 2003 |
| 2004 | Accord Officer | accord | Human | 117023 | - | AlertAndLookAtPlayer | 0 | - | 0 | \spawn monster 2004 |
| 2005 | Accord Officer | accord | Human | 117023 | - | AlertAndLookAtPlayer | 0 | - | 0 | \spawn monster 2005 |
| 2006 | Accord Engineer | accord | Human | 117024 | - | AlertAndLookAtPlayer | 0 | - | 0 | \spawn monster 2006 |
| 2007 | - | accord | Human | 117010 | - | BasicCivilian_Stationary | 0 | - | 0 | \spawn monster 2007 |
| 2008 | Accord Soldier | accord | Human | 117034 | 84915 / - | AlertAndLookAtPlayer | 0 | - | 0 | \spawn monster 2008 |
| 2009 | Accord Soldier | accord | Human | 117035 | 84915 / - | AlertAndLookAtPlayer | 0 | - | 0 | \spawn monster 2009 |
| 2010 | - | accord | Human | 117010 | - | AlertAndLookAtPlayer | 0 | - | 0 | \spawn monster 2010 |
| 2011 | Accord Guard | accord | Human | 117035 | 84915 / - | OneOff_FireAtEnemy | 0 | - | 0 | \spawn monster 2011 |
| 2012 | - | accord | Human | 117010 | - | AlertAndLookAtPlayer | 0 | - | 0 | \spawn monster 2012 |
| 2013 | - | accord | Human | 117010 | - | AlertAndLookAtPlayer | 0 | - | 0 | \spawn monster 2013 |
| 2014 | - | accord | Human | 117010 | - | AlertAndLookAtPlayer | 0 | - | 0 | \spawn monster 2014 |
| 2015 | - | accord | Human | 117010 | - | AlertAndLookAtPlayer | 0 | - | 0 | \spawn monster 2015 |
| 2016 | - | accord | Human | 117010 | - | AlertAndLookAtPlayer | 0 | - | 0 | \spawn monster 2016 |
| 2017 | Konaloa Research Base Civilian - Male | accord | Human | 117010 | - | AlertAndLookAtPlayer | 0 | - | 0 | \spawn monster 2017 |
| 2018 | Konaloa Research Base Civilian - Female | accord | Human | 117010 | - | AlertAndLookAtPlayer | 0 | - | 0 | \spawn monster 2018 |
| 2019 | Stronghold Civilian - Male | accord | Human | 117010 | - | AlertAndLookAtPlayer | 0 | - | 0 | \spawn monster 2019 |
| 2020 | Stronghold Civilian - Female | accord | Human | 117010 | - | AlertAndLookAtPlayer | 0 | - | 0 | \spawn monster 2020 |
| 2021 | Accord Medic | accord | Human | 117027 | - | AlertAndLookAtPlayer | 0 | - | 0 | \spawn monster 2021 |
| 2022 | - | accord | Human | 117580 | 85735 / - | GuardCityWanderer | 0 | - | 0 | \spawn monster 2022 |
| 2023 | - | accord | Human | 117585 | - | AlertAndInteractive | 0 | - | 0 | \spawn monster 2023 |
| 2024 | Chosen Scarecrow | accord | Human | 117586 | - | Stand | 0 | - | 5 | \spawn monster 2024 |
| 2025 | - | accord | Human | 117010 | - | AlertAndLookAtPlayer | 0 | - | 0 | \spawn monster 2025 |
| 2026 | - | accord | Human | 117010 | - | AlertAndLookAtPlayer | 0 | - | 0 | \spawn monster 2026 |
| 2027 | - | accord | Human | 117010 | - | BasicCivilian_Stationary | 0 | - | 0 | \spawn monster 2027 |
| 2028 | - | accord | Human | 117010 | - | BasicCivilian_Stationary | 0 | - | 0 | \spawn monster 2028 |
| 2029 | - | accord | Human | 117010 | - | BasicCivilian_Stationary | 0 | - | 0 | \spawn monster 2029 |
| 2030 | - | accord | Human | 117010 | - | AlertAndLookAtPlayer | 0 | - | 0 | \spawn monster 2030 |
| 2031 | - | accord | Human | 117010 | - | AlertAndLookAtPlayer | 0 | - | 0 | \spawn monster 2031 |
| 2032 | - | accord | Human | 117010 | - | AlertAndLookAtPlayer | 0 | - | 0 | \spawn monster 2032 |
| 2033 | - | accord | Human | 117010 | - | AlertAndLookAtPlayer | 0 | - | 0 | \spawn monster 2033 |
| 2034 | Biosphere Civilian - Male | accord | Human | 117010 | - | AlertAndLookAtPlayer | 0 | - | 0 | \spawn monster 2034 |
| 2035 | Biosphere Civilian - Female | accord | Human | 117010 | - | AlertAndLookAtPlayer | 0 | - | 0 | \spawn monster 2035 |
| 2038 | - | accord | Human | 117035 | 84915 / - | AlertAndLookAtPlayer | 0 | - | 0 | \spawn monster 2038 |
| 2040 | - | gaea | Wildlife | 117606 | 110770 / - | - | 0 | 5703 / - | 0 | \spawn monster 2040 |
| 2041 | Foston | accord | Human | 117580 | 85735 / - | - | 0 | - | 0 | \spawn monster 2041 |
| 2043 | Dr. Francesca Tellano | accord | Human | 52459 | - | AlertAndInteractive | 0 | - | 5 | \spawn monster 2043 |
| 2044 | - | chosen | Chosen | 32740 | 114621 / - | Arch_MedRangedHumanoid_Base | 0 | 5698 / 5369 | 5 | \spawn monster 2044 |
| 2045 | ARES Pilot | accord | Human | 10001 | - / 96847 | - | 0 | - | 0 | \spawn monster 2045 |
| 2046 | Tiny Brontodon | friendly | Companion | 118736 | - | - | 0 | - | 0 | \spawn monster 2046 |
| 2047 | Research Assistant | accord | Human | 52453 | - | AlertAndInteractive | 0 | - | 5 | \spawn monster 2047 |
| 2048 | - | chosen | Chosen | 77699 | 117697 / - | Arch_MoveThenFire_Base | 0 | 5697 / 5458 | 5 | \spawn monster 2048 |
| 2049 | OBSOLETE Toxic Aranha | gaea | Wildlife | 117706 | 20046 / - | Null | 0 | 5703 / 5633 | 5 | \spawn monster 2049 |
| 2050 | OBSOLETE Toxic Aranha Sieger | gaea | Wildlife | 117717 | 33830 / 30808 | - | 0 | 5704 / 5396 | 5 | \spawn monster 2050 |
| 2051 | OBSOLETE Toxic Explosive Aranha | gaea | Wildlife | 117719 | 20046 / - | - | 0 | 5703 / 5631 | 5 | \spawn monster 2051 |
| 2052 | Tame Toxic Aranha | friendly | Companion | 118003 | - | - | 0 | - | 5 | \spawn monster 2052 |
| 2053 | Wintertide Elf | bandit | Outlaw | 118040 | - | AlertAndInteractive | 0 | - | 0 | \spawn monster 2053 |
| 2054 | - | melding | Melded | 118135 | 85474 / - | - | 0 | 5700 / 5460 | 5 | \spawn monster 2054 |
| 2055 | Civilian | accord | Human | 118586 | - | WanderWithEmoteVocalized | 0 | - | 5 | \spawn monster 2055 |
| 2056 | Civilian | accord | Human | 118586 | - | WanderWithEmoteVocalized | 0 | - | 5 | \spawn monster 2056 |
| 2057 | Headless Horseman | chosen | Chosen | 118322 | 118410 / - | EventNpc_HeadlessHorseman | 0 | 5698 / 5369 | 5 | \spawn monster 2057 |
| 2058 | Ragequeen | gaea | Wildlife | 118148 | 85221 / 49118 | Arch_Relocator_Rageclaw_Base | 0 | 5705 / 5777 | 0 | \spawn monster 2058 |
| 2059 | - | chosen | Chosen | 118149 | 82484 / 81305 | OneOff_FireAtEnemy | 0 | - | 0 | \spawn monster 2059 |
| 2060 | - | bandit | Outlaw | 118154 | 118155 / - | - | 0 | 3 / 5388 | 5 | \spawn monster 2060 |
| 2061 | Accord Scientist | accord | Human | 118156 | - | AlertAndInteractive | 0 | - | 0 | \spawn monster 2061 |
| 2062 | - | accord | Human | 96715 | - | AlertAndInteractive | 0 | - | 100 | \spawn monster 2062 |
| 2063 | Grumbly Bumbly | bandit | Outlaw | 121004 | - | AlertAndInteractive | 0 | - | 0 | \spawn monster 2063 |
| 2064 | Large Brinewyrm | neutral | Large wildlife | 118174 | 77118 / 77119 | Wyrm | 0 | 5704 / 6219 | 5 | \spawn monster 2064 |
| 2065 | - | gaea | Wildlife | 118216 | - | - | 0 | - | 5 | \spawn monster 2065 |
| 2066 | Chosen Juggernaut | chosen | Chosen | 118217 | 122686 / - | - | 0 | 5701 / 5372 | 5 | \spawn monster 2066 |
| 2067 | - | gaea | Wildlife | 118221 | - | Config | 0 | - | 0 | \spawn monster 2067 |
| 2068 | - | accord | Human | 118225 | 96842 / - | - | 0 | - | 5 | \spawn monster 2068 |
| 2069 | - | accord | Human | 118226 | 96842 / - | - | 0 | - | 5 | \spawn monster 2069 |
| 2070 | - | accord | Human | 118227 | 96842 / - | - | 0 | - | 5 | \spawn monster 2070 |
| 2071 | Accord Soldier | accord | Human | 118244 | 97695 / 76108 | Arch_MedRangedHumanoid_Base | 0 | - | 0 | \spawn monster 2071 |
| 2072 | Accord Soldier | accord | Human | 118245 | 98471 / 76108 | Arch_MedRangedHumanoid_Base | 0 | - | 0 | \spawn monster 2072 |
| 2073 | Accord Soldier | accord | Human | 118246 | 97953 / 76108 | Arch_MedRangedHumanoid_Base | 0 | - | 0 | \spawn monster 2073 |
| 2074 | Scavenger Bot | neutral | Large wildlife | 118248 | - | PerformEmote | 0 | - / 5601 | 0 | \spawn monster 2074 |
| 2075 | Civilian Hostage | accord | Human | 81381 | - | PeacetimeCityWanderer | 0 | - | 0 | \spawn monster 2075 |
| 2076 | Civilian Hostage | accord | Human | 81381 | - | PeacetimeCityWanderer | 0 | - | 0 | \spawn monster 2076 |
| 2077 | Brontodon King | gaea | Large wildlife | 118265 | 118538 / 118271 | - | 0 | 5705 / 5443 | 5 | \spawn monster 2077 |
| 2078 | - | accord | Human | 118272 | - | AlertAndInteractive | 0 | - | 5 | \spawn monster 2078 |
| 2079 | - | - | Melded | 118286 | - | - | 0 | - | 5 | \spawn monster 2079 |
| 2086 | - | chosen | Chosen | 32740 | 92799 / 33064 | EliteWanderer | 0 | - | 0 | \spawn monster 2086 |
| 2087 | The Witch | accord | Human | 52453 | - | AlertAndInteractive | 0 | - | 5 | \spawn monster 2087 |
| 2088 | Doctor Lyon | accord | Human | 52457 | - | StationaryCivilianDialog | 0 | - | 0 | \spawn monster 2088 |
| 2089 | Sergeant Freeman | accord | Human | 118305 | 114028 / - | Arch_MedRangedHumanoid_Base | 0 | - | 0 | \spawn monster 2089 |
| 2090 | Private Harrison | accord | Human | 118308 | 97673 / - | Arch_MedRangedHumanoid_Base | 0 | - | 0 | \spawn monster 2090 |
| 2091 | - | accord | Human | 117010 | 84915 / - | AlertAndLookAtPlayer | 0 | - | 0 | \spawn monster 2091 |
| 2092 | - | accord | Human | 10001 | 97685 / 76108 | AlertAndLookAtPlayer | 0 | - | 0 | \spawn monster 2092 |
| 2093 | Flamestriker Squad Member | accord | Human | 10001 | 114319 / 97671 | AlertAndLookAtPlayer | 0 | - | 0 | \spawn monster 2093 |
| 2094 | Emerick | accord | Human | 118467 | - | AlertAndLookAtPlayer | 0 | - | 0 | \spawn monster 2094 |
| 2095 | Claire | accord | Human | 118408 | 118409 / - | - | 0 | - | 0 | \spawn monster 2095 |
| 2096 | Zed | accord | Human | 106357 | - | AlertAndInteractive | 0 | - | 0 | \spawn monster 2096 |
| 2097 | Chosen Lieutenant | chosen | Chosen | 88154 | 85971 / - | Arch_MoveThenFire_Base | 0 | 5700 / 5371 | 5 | \spawn monster 2097 |
| 2098 | - | monster | Chosen | 118357 | - | Null | 0 | - | 0 | \spawn monster 2098 |
| 2099 | - | accord | Human | 118411 | - | PeacetimeCityWanderer | 0 | - | 0 | \spawn monster 2099 |
| 2100 | Elite Devastator | chosen | Chosen | 118412 | 122645 / - | - | 0 | 5700 / 5371 | 5 | \spawn monster 2100 |
| 2101 | Christine | accord | Human | 118414 | - | AlertAndInteractive | 0 | - | 0 | \spawn monster 2101 |
| 2102 | Christobel | accord | Human | 118414 | - | AlertAndInteractive | 0 | - | 0 | \spawn monster 2102 |
| 2103 | - | accord | Human | 118411 | - | PeacetimeCityWanderer | 0 | - | 0 | \spawn monster 2103 |
| 2104 | Pterothan | gaea | Wildlife | 118574 | 118575 / - | - | 0 | 6645 / - | 0 | \spawn monster 2104 |
| 2105 | Prisoner | accord | Human | 118535 | - | Wander | 0 | - | 0 | \spawn monster 2105 |
| 2106 | Prisoner | accord | Human | 118535 | - | Wander | 0 | - | 0 | \spawn monster 2106 |
| 2107 | Infected Soldier | friendly | Human | 31331 | - | AlertAndLookAtPlayer | 0 | - | 0 | \spawn monster 2107 |
| 2108 | Bio Engineer | chosen | Chosen | 118536 | 118532 / - | Arch_MedRangedHumanoid_Base | 0 | 5700 / 5371 | 5 | \spawn monster 2108 |
| 2109 | - | gaea | Wildlife | 118584 | - | Arch_FullbodyMelee_Base | 0 | 5706 / 5778 | 5 | \spawn monster 2109 |
| 2110 | Hydrus | gaea | Wildlife | 118591 | 118833 / - | - | 0 | 5706 / - | 5 | \spawn monster 2110 |
| 2111 | Anubis | melding | Melded | 118592 | 120606 / - | - | 0 | 5706 / - | 5 | \spawn monster 2111 |
| 2112 | Haken | gaea | Wildlife | 118593 | 118845 / - | - | 0 | 5706 / - | 5 | \spawn monster 2112 |
| 2113 | Strix | gaea | Wildlife | 118594 | 118850 / - | - | 0 | 5706 / - | 5 | \spawn monster 2113 |
| 2114 | - | gaea | Wildlife | 118595 | 66846 / - | FeralCanineSquad | 0 | 5704 / 6211 | 5 | \spawn monster 2114 |
| 2115 | - | gaea | Wildlife | 118598 | - | Arch_FullbodyMelee_Base | 0 | 5706 / 5778 | 5 | \spawn monster 2115 |
| 2116 | - | gaea | Wildlife | 118599 | 66846 / - | FeralCanineSquad | 0 | 5706 / 6214 | 5 | \spawn monster 2116 |
| 2117 | Garm | gaea | Wildlife | 118607 | 85221 / 49118 | - | 0 | 5706 / 5425 | 5 | \spawn monster 2117 |
| 2118 | Kisuton Rep | accord | Human | 52453 | - | AlertAndInteractive | 0 | - | 5 | \spawn monster 2118 |
| 2119 | - | gaea | Wildlife | 118610 | 120575 / - | - | 0 | 5697 / 5615 | 5 | \spawn monster 2119 |
| 2120 | Huntmaster | bandit | Outlaw | 118686 | 81396 / 82354 | RaiderBaronMiniBoss | 0 | 5313 / 5388 | 5 | \spawn monster 2120 |
| 2121 | Scald | gaea | Wildlife | 118676 | - | - | 0 | 5705 / 5778 | 5 | \spawn monster 2121 |
| 2122 | - | gaea | Wildlife | 118677 | 118801 / - | - | 0 | - | 10 | \spawn monster 2122 |
| 2123 | - | gaea | Wildlife | 118678 | 118805 / - | - | 0 | - | 10 | \spawn monster 2123 |
| 2124 | - | gaea | Wildlife | 118679 | 118811 / - | - | 0 | - | 10 | \spawn monster 2124 |
| 2125 | - | gaea | Wildlife | 118681 | 118796 / - | - | 0 | - | 10 | \spawn monster 2125 |
| 2126 | - | gaea | Wildlife | 118683 | - | - | 0 | - | 10 | \spawn monster 2126 |
| 2127 | - | gaea | Wildlife | 118684 | - | - | 0 | - | 10 | \spawn monster 2127 |
| 2128 | - | chosen | Chosen | 118944 | 118690 / - | - | 0 | 5700 / 5371 | 5 | \spawn monster 2128 |
| 2129 | Chosen Annihilator | chosen | Chosen | 118699 | 118697 / - | - | 0 | 5698 / 5369 | 5 | \spawn monster 2129 |
| 2130 | - | gaea | Wildlife | 118708 | 118709 / - | - | 0 | 1 / - | 5 | \spawn monster 2130 |
| 2131 | Auto AntiVehicle NPC | accord | Human | 86127 | 96666 / - | TurretTeleporterTarget | 0 | 5311 / 5387 | 0 | \spawn monster 2131 |
| 2132 | Earthbreaker, Pet Hologram | friendly | Large wildlife | 118734 | - | - | 0 | - | 0 | \spawn monster 2132 |
| 2133 | Headless Horseman | accord | Human | 118738 | 118410 / - | - | 0 | - | 0 | \spawn monster 2133 |
| 2135 | Lil' King | friendly | Companion | 118750 | - | - | 0 | - | 0 | \spawn monster 2135 |
| 2136 | Chosen Siegebreaker | chosen | Chosen | 118752 | 118755 / - | - | 0 | 5698 / 5369 | 5 | \spawn monster 2136 |
| 2137 | - | friendly | Chosen | 118757 | 118410 / - | - | 0 | 5698 / 5369 | 5 | \spawn monster 2137 |
| 2138 | - | accord | Human | 0 | - | Null | 0 | - | 0 | \spawn monster 2138 |
| 2139 | - | accord | Human | 0 | - | Null | 0 | - | 0 | \spawn monster 2139 |
| 2140 | Thanks, Nostromo | accord | Human | 0 | - | Null | 0 | - | 0 | \spawn monster 2140 |
| 2141 | - | accord | Human | 0 | - | Null | 0 | - | 0 | \spawn monster 2141 |
| 2142 | - | accord | Human | 0 | - | Null | 0 | - | 0 | \spawn monster 2142 |
| 2143 | Danko | neutral | Large wildlife | 118772 | 77118 / 77119 | Wyrm | 0 | 5706 / 6221 | 5 | \spawn monster 2143 |
| 2144 | Nymero | neutral | Wildlife | 118773 | 77268 / 77248 | - | 0 | 5706 / - | 5 | \spawn monster 2144 |
| 2145 | Kojo | gaea | Wildlife | 118774 | 82585 / - | - | 0 | 5706 / 6214 | 5 | \spawn monster 2145 |
| 2146 | Kruger | gaea | Wildlife | 118775 | 85191 / - | - | 0 | 5706 / 5425 | 5 | \spawn monster 2146 |
| 2147 | - | chosen | Chosen | 32740 | 32739 / - | Arch_MedRangedHumanoid_Base | 0 | 5698 / 5369 | 5 | \spawn monster 2147 |
| 2148 | - | gaea | Wildlife | 118777 | 118802 / - | - | 0 | - | 10 | \spawn monster 2148 |
| 2149 | Accord Soldier | accord | Human | 120966 | 87741 / 76108 | Arch_MedRangedHumanoid_Base | 0 | - | 0 | \spawn monster 2149 |
| 2150 | Accord Soldier | accord | Human | 120965 | 87918 / 76108 | Arch_MedRangedHumanoid_Base | 0 | - | 0 | \spawn monster 2150 |
| 2151 | Accord Soldier | accord | Human | 120967 | 87028 / 76108 | Arch_MedRangedHumanoid_Base | 0 | - | 0 | \spawn monster 2151 |
| 2152 | Accord Soldier | accord | Human | 118818 | - | Arch_MedRangedHumanoid_Base | 0 | - | 0 | \spawn monster 2152 |
| 2153 | Massive Melded Varant | melding | Melded | 118821 | 85165 / - | Arch_FullbodyMelee_Base | 0 | 5705 / 5460 | 5 | \spawn monster 2153 |
| 2154 | Accord Soldier | accord | Human | 120964 | 86742 / 76108 | Arch_MedRangedHumanoid_Base | 0 | - | 0 | \spawn monster 2154 |
| 2156 | Chosen Engineer Shield Drone | chosen | Human | 118832 | 118932 / - | ChosenEngineerDrone | 0 | 5696 / 5615 | 5 | \spawn monster 2156 |
| 2157 | Chosen Engineer | chosen | Chosen | 118839 | 118851 / - | - | 0 | 5698 / 5813 | 5 | \spawn monster 2157 |
| 2158 | Tornado | gaea | Wildlife | 118846 | - | - | 0 | - | 0 | \spawn monster 2158 |
| 2159 | Rashnu | chosen | Chosen | 85881 | 138380 / - | - | 0 | 6644 / - | 5 | \spawn monster 2159 |
| 2160 | Magaera the Fury | gaea | Wildlife | 118928 | 96576 / - | - | 0 | 5706 / - | 5 | \spawn monster 2160 |
| 2161 | Scota the Breaker | gaea | Wildlife | 118929 | 95024 / - | Arch_FullbodyMelee_Base | 0 | 5706 / 5398 | 5 | \spawn monster 2161 |
| 2162 | Iwanci the Fetid | gaea | Wildlife | 118930 | 95106 / - | - | 0 | 5706 / - | 5 | \spawn monster 2162 |
| 2163 | Shisa the Prowler | gaea | Wildlife | 118931 | 82619 / 82663 | - | 0 | 5725 / 6212 | 5 | \spawn monster 2163 |
| 2164 | CharacterSelect_Accord Recon | accord | Human | 118933 | 79189 / 79189 | Stand | 0 | - | 0 | \spawn monster 2164 |
| 2165 | CharacterSelect_Accord Engineer | accord | Human | 118934 | 119434 / 119434 | Stand | 0 | - | 0 | \spawn monster 2165 |
| 2166 | CharacterSelect_Accord Assault | accord | Human | 118935 | 79272 / 79272 | Stand | 0 | - | 0 | \spawn monster 2166 |
| 2167 | CharacterSelect_Accord Biotech | accord | Human | 118936 | 30025 / - | Stand | 0 | - | 0 | \spawn monster 2167 |
| 2168 | CharacterSelect_Accord Dreadnaught | accord | Human | 118937 | 78302 / 78302 | Stand | 0 | - | 0 | \spawn monster 2168 |
| 2169 | Turret | accord | Human | 33976 | - / 118945 | AggressiveWanderer | 0 | - | 0 | \spawn monster 2169 |
| 2170 | - | chosen | Chosen | 118963 | - | - | 0 | - | 0 | \spawn monster 2170 |
| 2171 | Chosen Mosquito Drone | chosen | Chosen | 119905 | 119906 / - | - | 0 | 5704 / 5369 | 5 | \spawn monster 2171 |
| 2172 | - | accord | Human | 118408 | 118409 / - | - | 0 | - | 0 | \spawn monster 2172 |
| 2173 | Chosen Dropship | chosen | Chosen | 118963 | - | - | 0 | - | 0 | \spawn monster 2173 |
| 2174 | Omnidyne Representative | accord | Human | 120111 | - | AlertAndInteractive | 0 | - | 5 | \spawn monster 2174 |
| 2175 | Chosen Assault Drone | chosen | Chosen | 120561 | 120560 / - | - | 0 | 5699 / 5370 | 0 | \spawn monster 2175 |
| 2176 | - | accord | Human | 120578 | - | AlertAndInteractive | 0 | - | 0 | \spawn monster 2176 |
| 2177 | Supply Officer Balanon | accord | Human | 96715 | - | AlertAndInteractive | 0 | - | 0 | \spawn monster 2177 |
| 2178 | Ash Drinker | neutral | Large wildlife | 120584 | 85189 / 77119 | - | 0 | 5726 / 6220 | 5 | \spawn monster 2178 |
| 2179 | - | chosen | Chosen | 120588 | 34143 / - | Arch_AdditiveMelee_Base | 0 | 5698 / 5369 | 5 | \spawn monster 2179 |
| 2180 | Sand Drinker | neutral | Large wildlife | 120589 | 77118 / 77119 | Wyrm | 0 | 5704 / 6219 | 5 | \spawn monster 2180 |
| 2181 | - | accord | Human | 10004 | - / 76108 | Arch_MedRangedHumanoid_Base | 0 | - | 0 | \spawn monster 2181 |
| 2182 | - | accord | Human | 10002 | 85972 / - | Arch_MedRangedHumanoid_Base | 0 | - | 0 | \spawn monster 2182 |
| 2183 | Accord Assault | accord | Human | 10001 | 30688 / - | Arch_MedRangedHumanoid_Base | 0 | - | 0 | \spawn monster 2183 |
| 2184 | Accord Dreadnaught | accord | Human | 56723 | - / 76108 | Arch_MedRangedHumanoid_Base | 0 | - | 0 | \spawn monster 2184 |
| 2185 | - | accord | Human | 10003 | - / 76108 | Arch_MedRangedHumanoid_Base | 0 | - | 0 | \spawn monster 2185 |
| 2186 | Ash Larva | melding | Melded | 120591 | 85187 / - | StockMelee | 0 | 5724 / 6651 | 0 | \spawn monster 2186 |
| 2189 | - | accord | Human | 120602 | - | AlertAndInteractive | 0 | - | 0 | \spawn monster 2189 |
| 2190 | - | accord | Human | 120603 | - | - | 0 | - | 0 | \spawn monster 2190 |
| 2191 | ARES Pilot - Assault - Gun | accord | Human | 117031 | 86755 / - | AlertAndLookAtPlayer | 0 | - | 0 | \spawn monster 2191 |
| 2192 | Accord Soldier | accord | Human | 118244 | 86742 / 76108 | Arch_MedRangedHumanoid_Base | 0 | - | 0 | \spawn monster 2192 |
| 2193 | - | accord | Human | 118245 | 87918 / 76108 | Arch_MedRangedHumanoid_Base | 0 | - | 0 | \spawn monster 2193 |
| 2194 | Accord Soldier | accord | Human | 118244 | 87741 / 76108 | Arch_MedRangedHumanoid_Base | 0 | - | 0 | \spawn monster 2194 |
| 2195 | Accord Soldier | accord | Human | 118246 | 87028 / 76108 | Arch_MedRangedHumanoid_Base | 0 | - | 0 | \spawn monster 2195 |
| 2196 | - | chosen | Chosen | 120610 | 96868 / - | Arch_AdditiveMelee_Base | 0 | 5311 / 5387 | 5 | \spawn monster 2196 |
| 2197 | - | chosen | Chosen | 120616 | 20016 / - | Arch_AdditiveMelee_Base | 0 | 5311 / 5387 | 5 | \spawn monster 2197 |
| 2198 | - | chosen | Chosen | 120617 | 96868 / - | Arch_AdditiveMelee_Base | 0 | 5311 / 5387 | 5 | \spawn monster 2198 |
| 2199 | - | chosen | Chosen | 120618 | 77760 / - | Arch_AdditiveMelee_Base | 0 | 5311 / 5387 | 5 | \spawn monster 2199 |
| 2200 | - | accord | Human | 117031 | 84915 / - | GuardCityWanderer | 0 | - | 0 | \spawn monster 2200 |
| 2201 | ARES Pilot - Biotech - Gun | accord | Human | 117033 | 87033 / - | AlertAndLookAtPlayer | 0 | - | 0 | \spawn monster 2201 |
| 2202 | ARES Pilot - Recon - Gun | accord | Human | 117034 | 86981 / - | AlertAndLookAtPlayer | 0 | - | 0 | \spawn monster 2202 |
| 2203 | ARES Pilot - Engineer - Gun | accord | Human | 120619 | 87393 / - | AlertAndLookAtPlayer | 0 | - | 0 | \spawn monster 2203 |
| 2204 | ARES Pilot - Dreadnaught - Gun | accord | Human | 117032 | 87813 / - | AlertAndLookAtPlayer | 0 | - | 0 | \spawn monster 2204 |
| 2205 | ARES Pilot - Biotech - no Gun | accord | Human | 124698 | - | - | 0 | - | 0 | \spawn monster 2205 |
| 2206 | ARES Pilot - Assault - no Gun | accord | Human | 117031 | - | AlertAndLookAtPlayer | 0 | - | 0 | \spawn monster 2206 |
| 2207 | ARES Pilot - Recon - no Gun | accord | Human | 117034 | - | AlertAndLookAtPlayer | 0 | - | 0 | \spawn monster 2207 |
| 2208 | ARES Pilot - Dreadnaught - no Gun | accord | Human | 117032 | - | AlertAndLookAtPlayer | 0 | - | 0 | \spawn monster 2208 |
| 2209 | Kangbot | accord | Human | 85732 | - | AlertAndLookAtPlayer | 0 | - | 0 | \spawn monster 2209 |
| 2210 | - | accord | Human | 117034 | 87945 / - | GuardCityWanderer | 0 | - | 0 | \spawn monster 2210 |
| 2211 | - | accord | Human | 81391 | 84915 / - | AlertAndLookAtPlayer | 0 | - | 0 | \spawn monster 2211 |
| 2212 | - | accord | Human | 82563 | - | AlertAndLookAtPlayer | 0 | - | 0 | \spawn monster 2212 |
| 2213 | Chosen Chibi Earthbreaker, Pet Hologram | friendly | Large wildlife | 120635 | - | - | 0 | - | 0 | \spawn monster 2213 |
| 2214 | - | accord | Human | 82563 | - | PeacetimeCityWanderer | 0 | - | 0 | \spawn monster 2214 |
| 2215 | - | accord | Human | 81391 | 84915 / - | GuardCityWanderer | 0 | - | 0 | \spawn monster 2215 |
| 2216 | Chosen Heavy Turret Operator | chosen | Chosen | 120582 | 92799 / 33064 | EngineerTurretTeleporter | 0 | - | 0 | \spawn monster 2216 |
| 2217 | Chosen Assault, Pet Hologram | friendly | Large wildlife | 120660 | - | - | 0 | - | 0 | \spawn monster 2217 |
| 2218 | Chosen Chibi Assault, Pet Hologram | friendly | Large wildlife | 120661 | - | - | 0 | - | 0 | \spawn monster 2218 |
| 2219 | Chosen Engineer, Pet Hologram | friendly | Large wildlife | 120662 | - | - | 0 | - | 0 | \spawn monster 2219 |
| 2220 | Chosen Chibi Engineer, Pet Hologram | friendly | Large wildlife | 120663 | - | - | 0 | - | 0 | \spawn monster 2220 |
| 2221 | Chosen Executioner, Pet Hologram | friendly | Large wildlife | 120664 | - | - | 0 | - | 0 | \spawn monster 2221 |
| 2222 | Chosen Chibi Executioner, Pet Hologram | friendly | Large wildlife | 120665 | - | - | 0 | - | 0 | \spawn monster 2222 |
| 2223 | Chosen Archon, Pet Hologram | friendly | Large wildlife | 120666 | - | - | 0 | - | 0 | \spawn monster 2223 |
| 2224 | Chosen Chibi Archon, Pet Hologram | friendly | Large wildlife | 120667 | - | - | 0 | - | 0 | \spawn monster 2224 |
| 2225 | Melded Trapjaw, Pet Hologram | friendly | Large wildlife | 120668 | - | - | 0 | - | 0 | \spawn monster 2225 |
| 2226 | Vile Carcass, Pet Hologram | friendly | Large wildlife | 120669 | - | - | 0 | - | 0 | \spawn monster 2226 |
| 2227 | Tiny Typhon, Pet Hologram | friendly | Large wildlife | 120670 | - | - | 0 | - | 0 | \spawn monster 2227 |
| 2228 | Chibi Typhoon, Pet Hologram | friendly | Large wildlife | 120671 | - | - | 0 | - | 0 | \spawn monster 2228 |
| 2229 | Mourningstar, Pet Hologram | friendly | Large wildlife | 120672 | - | - | 0 | - | 0 | \spawn monster 2229 |
| 2230 | Chibi Mourningstar, Pet Hologram | friendly | Large wildlife | 120673 | - | - | 0 | - | 0 | \spawn monster 2230 |
| 2231 | Oilspill, Pet Hologram | friendly | Large wildlife | 120674 | - | - | 0 | - | 0 | \spawn monster 2231 |
| 2232 | Chibi Oilspill, Pet Hologram | friendly | Large wildlife | 120675 | - | - | 0 | - | 0 | \spawn monster 2232 |
| 2233 | Aero, Pet Hologram | friendly | Large wildlife | 120676 | - | - | 0 | - | 0 | \spawn monster 2233 |
| 2234 | Chibi Aero, Pet Hologram | friendly | Large wildlife | 120677 | - | - | 0 | - | 0 | \spawn monster 2234 |
| 2235 | Captain Fuller, Pet Hologram | friendly | Large wildlife | 120678 | - | - | 0 | - | 0 | \spawn monster 2235 |
| 2236 | Chibi Captain Fuller, Pet Hologram | friendly | Large wildlife | 120679 | - | - | 0 | - | 0 | \spawn monster 2236 |
| 2237 | - | friendly | Large wildlife | 120680 | - | - | 0 | - | 0 | \spawn monster 2237 |
| 2238 | - | friendly | Large wildlife | 120681 | - | - | 0 | - | 0 | \spawn monster 2238 |
| 2239 | Field Medic | accord | Human | 120705 | - | - | 0 | - | 0 | \spawn monster 2239 |
| 2240 | Battlelab Aero, Pet Hologram | friendly | Large wildlife | 120926 | - | - | 0 | - | 0 | \spawn monster 2240 |
| 2241 | - | chosen | Chosen | 121200 | 121243 / - | Arch_MedRanged_Base | 0 | 5700 / 5371 | 5 | \spawn monster 2241 |
| 2242 | [PH] Hometree Engineer | accord | Human | 97027 | - | AlertAndInteractive | 0 | - | 0 | \spawn monster 2242 |
| 2243 | - | accord | Human | 120944 | - | - | 0 | - | 0 | \spawn monster 2243 |
| 2244 | - | accord | Human | 120944 | - | - | 0 | - | 0 | \spawn monster 2244 |
| 2245 | - | chosen | Chosen | 120948 | 120947 / - | - | 0 | 5698 / 5369 | 5 | \spawn monster 2245 |
| 2246 | - | gaea | Wildlife | 120984 | - | - | 0 | 5724 / 5458 | 5 | \spawn monster 2246 |
| 2247 | - | gaea | Wildlife | 120985 | 40472 / - | - | 0 | 5704 / 5396 | 5 | \spawn monster 2247 |
| 2248 | - | gaea | Wildlife | 120986 | 95024 / - | Arch_FullbodyMelee_Base | 0 | 5706 / 5398 | 5 | \spawn monster 2248 |
| 2249 | - | gaea | Wildlife | 120987 | 97365 / - | Arch_FullbodyMelee_Base | 0 | 5724 / 5458 | 5 | \spawn monster 2249 |
| 2250 | - | gaea | Wildlife | 92890 | 40472 / - | - | 0 | 5704 / 5396 | 5 | \spawn monster 2250 |
| 2251 | - | gaea | Wildlife | 120988 | 40472 / - | - | 0 | 5704 / 5396 | 5 | \spawn monster 2251 |
| 2252 | - | gaea | Wildlife | 120990 | 40472 / - | - | 0 | 5704 / 5396 | 5 | \spawn monster 2252 |
| 2253 | - | accord | Human | 120111 | - | MeldingTornadoShard | 0 | - | 5 | \spawn monster 2253 |
| 2254 | - | chosen | Chosen | 121019 | 121018 / 121016 | - | 0 | - | 5 | \spawn monster 2254 |
| 2255 | Swamp Hisser | gaea | Wildlife | 121013 | 121012 / - | - | 0 | 5703 / 5404 | 5 | \spawn monster 2255 |
| 2256 | Matriarch Hisser | gaea | Wildlife | 121015 | 121049 / - | - | 10005 | 5706 / 5407 | 5 | \spawn monster 2256 |
| 2257 | Myrmidon Hisser | gaea | Wildlife | 121022 | 121050 / - | - | 0 | 5704 / 5405 | 5 | \spawn monster 2257 |
| 2258 | Hunter Hisser | gaea | Wildlife | 121020 | 121021 / - | - | 0 | 5704 / 5405 | 5 | \spawn monster 2258 |
| 2259 | Ichor Hisser | gaea | Wildlife | 121017 | - | - | 0 | 5703 / 5404 | 5 | \spawn monster 2259 |
| 2260 | Yukiko Akiyama | accord | Human | 117013 | - | AlertAndInteractive | 0 | - | 10 | \spawn monster 2260 |
| 2261 | Holmgang Recruiter | accord | Human | 10001 | - / 30025 | - | 0 | - | 0 | \spawn monster 2261 |
| 2262 | - | chosen | Chosen | 121036 | 118295 / - | - | 0 | - | 5 | \spawn monster 2262 |
| 2263 | - | chosen | Chosen | 121092 | 122470 / - | - | 0 | - | 0 | \spawn monster 2263 |
| 2264 | Astrek Associations | accord | Human | 10001 | - / 30025 | - | 0 | - | 0 | \spawn monster 2264 |
| 2265 | Scout | chosen | Chosen | 121053 | 85953 / - | - | 0 | 5698 / 5369 | 5 | \spawn monster 2265 |
| 2266 | - | chosen | Chosen | 121090 | 121136 / - | FastWander | 0 | - | 0 | \spawn monster 2266 |
| 2267 | Lt. Stephen "Saint" Muray | accord | Human | 82637 | - | AlertAndInteractive | 0 | - | 0 | \spawn monster 2267 |
| 2268 | - | friendly | Companion | 121199 | - | - | 0 | - | 0 | \spawn monster 2268 |
| 2269 | Corporal Belle | accord | Human | 76334 | - | AlertAndInteractive | 0 | - | 0 | \spawn monster 2269 |
| 2270 | Lt. Amanda Bloom | accord | Human | 76331 | - | AlertAndInteractive | 0 | - | 0 | \spawn monster 2270 |
| 2271 | Warrant Officer Liu | accord | Human | 76132 | - | AlertAndInteractive | 0 | - | 0 | \spawn monster 2271 |
| 2272 | Ogrix | chosen | Chosen | 121478 | 118295 / 122946 | - | 0 | 5701 / 5370 | 5 | \spawn monster 2272 |
| 2273 | Spirit of Flight, Pet Hologram | friendly | Large wildlife | 121239 | - | - | 0 | - | 0 | \spawn monster 2273 |
| 2274 | Wintertide Vendor | accord | Human | 52454 | - | AlertAndInteractive | 0 | - | 5 | \spawn monster 2274 |
| 2275 | Lt. Amanda Bloom | accord | Human | 76331 | 121315 / - | Arch_AdditiveMelee_Base | 0 | - | 0 | \spawn monster 2275 |
| 2276 | Agrievan | chosen | Chosen | 121313 | 121444 / - | - | 0 | - | 5 | \spawn monster 2276 |
| 2277 | - | accord | Human | 121319 | 121318 / - | Arch_MedRangedHumanoid_Base | 0 | - | 0 | \spawn monster 2277 |
| 2278 | - | accord | Human | 107773 | 85735 / - | Arch_MoveThenFire_Base | 0 | - | 0 | \spawn monster 2278 |
| 2279 | - | accord | Human | 107772 | 77336 / - | Arch_MedRangedHumanoid_Base | 0 | - | 0 | \spawn monster 2279 |
| 2280 | Mr. Green | friendly | Companion | 121325 | - | TestElfPet | 0 | - | 0 | \spawn monster 2280 |
| 2281 | Corporal Belle | accord | Human | 76334 | 121318 / - | Arch_MedRangedHumanoid_Base | 0 | - | 0 | \spawn monster 2281 |
| 2282 | Warrant Officer Liu | accord | Human | 76132 | 81308 / - | Arch_MedRangedHumanoid_Base | 0 | - | 0 | \spawn monster 2282 |
| 2283 | - | chosen | Chosen | 121430 | 121136 / - | - | 0 | - | 0 | \spawn monster 2283 |
| 2284 | - | chosen | Chosen | 121440 | - | - | 0 | 5697 / 5615 | 5 | \spawn monster 2284 |
| 2285 | Chosen Support Drone | chosen | Chosen | 121443 | - | - | 0 | 5696 / 5615 | 5 | \spawn monster 2285 |
| 2286 | Aliyan | chosen | Chosen | 121451 | 122669 / 122655 | - | 0 | - | 5 | \spawn monster 2286 |
| 2287 | Tarantus | chosen | Chosen | 121454 | 122655 / 122621 | - | 0 | - | 5 | \spawn monster 2287 |
| 2288 | - | chosen | Chosen | 121465 | 121444 / - | - | 0 | - | 5 | \spawn monster 2288 |
| 2289 | Chosen Healing Drone | chosen | Chosen | 121470 | 121471 / - | - | 0 | 5696 / 5615 | 5 | \spawn monster 2289 |
| 2290 | Rebel Rioter | Rebels | Outlaw | 121474 | 121472 / - | - | 0 | 5311 / 5387 | 5 | \spawn monster 2290 |
| 2291 | Kara | accord | Human | 121509 | - | - | 0 | - | 0 | \spawn monster 2291 |
| 2292 | Dr. Bathsheba | accord | Human | 121514 | - | - | 0 | - | 0 | \spawn monster 2292 |
| 2293 | - | chosen | Chosen | 122495 | 85645 / - | - | 0 | 6 / 5813 | 5 | \spawn monster 2293 |
| 2294 | Reaper Lookout | Reapers | Outlaw | 95134 | 96871 / 96847 | Arch_Sniper_Base | 0 | 5311 / 5386 | 0 | \spawn monster 2294 |
| 2295 | Reaper Powder Monkey | Reapers | Outlaw | 95134 | 122296 / 96847 | Arch_Sniper_Base | 0 | 5311 / 5386 | 0 | \spawn monster 2295 |
| 2296 | - | chosen | Chosen | 118217 | 118295 / - | - | 0 | 5699 / 5370 | 5 | \spawn monster 2296 |
| 2297 | Dread Nautilus | gaea | Wildlife | 122471 | - / 122476 | - | 0 | 5705 / 5451 | 5 | \spawn monster 2297 |
| 2298 | Militus | chosen | Chosen | 122510 | 122543 / - | - | 0 | - | 5 | \spawn monster 2298 |
| 2299 | Reaper Captain Cherise | Reapers | Outlaw | 122472 | 122473 / - | - | 0 | - | 5 | \spawn monster 2299 |
| 2300 | - | chosen | Chosen | 121090 | 121136 / - | - | 0 | 6762 / - | 0 | \spawn monster 2300 |
| 2301 | - | accord | Human | 117619 | - | AlertAndInteractive | 0 | - | 0 | \spawn monster 2301 |
| 2302 | Scarlet | accord | Human | 117619 | 81308 / - | Arch_MedRangedHumanoid_Base | 0 | - | 0 | \spawn monster 2302 |
| 2303 | Melding Core | melding | Melded | 122531 | - | Arch_AdditiveMelee_Base | 0 | - | 0 | \spawn monster 2303 |
| 2304 | - | Reapers | Outlaw | 122541 | - | - | 0 | 3 / 5387 | 5 | \spawn monster 2304 |
| 2305 | - | chosen | Chosen | 122546 | 75681 / - | - | 0 | 5697 / 5615 | 5 | \spawn monster 2305 |
| 2306 | Ophanim Trooper | Ophanim | Outlaw | 125069 | 96847 / - | - | 0 | 5311 / - | 0 | \spawn monster 2306 |
| 2307 | - | accord | Human | 76335 | - | AlertAndInteractive | 0 | - | 0 | \spawn monster 2307 |
| 2308 | Ophanim Sniper | Ophanim | Outlaw | 125087 | 96871 / 96842 | - | 0 | 5311 / - | 0 | \spawn monster 2308 |
| 2309 | - | accord | Human | 118000 | - | AlertAndInteractive | 0 | - | 0 | \spawn monster 2309 |
| 2310 | - | accord | Human | 76337 | - | AlertAndInteractive | 0 | - | 0 | \spawn monster 2310 |
| 2311 | Tanken Hitman | Tanken | Outlaw | 122561 | 122563 / - | - | 0 | 5312 / 5388 | 0 | \spawn monster 2311 |
| 2312 | Tanken Cyborg Samurai | Tanken | Outlaw | 122602 | 122567 / 122613 | - | 0 | 5313 / 5389 | 0 | \spawn monster 2312 |
| 2313 | Chosen Turret Bot | chosen | Chosen | 122611 | 120560 / - | - | 0 | - | 0 | \spawn monster 2313 |
| 2314 | Buzzard Shotgunner | bandit | Outlaw | 125147 | 122621 / - | - | 0 | 5311 / 5387 | 0 | \spawn monster 2314 |
| 2315 | Buzzard Assault Rifleman | bandit | Outlaw | 125069 | 96847 / - | - | 0 | 5311 / 5387 | 0 | \spawn monster 2315 |
| 2316 | - | bandit | Outlaw | 122624 | 122625 / - | - | 0 | 5311 / 5387 | 0 | \spawn monster 2316 |
| 2317 | - | chosen | Chosen | 122639 | 85386 / - | Arch_MoveThenFire_Base | 0 | 5700 / 5371 | 5 | \spawn monster 2317 |
| 2318 | - | chosen | Chosen | 122641 | 85386 / - | Arch_MoveThenFire_Base | 0 | 5700 / 5371 | 5 | \spawn monster 2318 |
| 2319 | - | chosen | Chosen | 122642 | 85386 / - | Arch_MoveThenFire_Base | 0 | 5700 / 5371 | 5 | \spawn monster 2319 |
| 2320 | Chosen Devastator | chosen | Chosen | 125146 | 122645 / - | - | 0 | 5699 / 5370 | 5 | \spawn monster 2320 |
| 2321 | - | gaea | Wildlife | 96931 | 82608 / - | - | 0 | - | 5 | \spawn monster 2321 |
| 2322 | Blood King Contractor | bandit | Outlaw | 122707 | 96842 / - | - | 0 | 5310 / - | 0 | \spawn monster 2322 |
| 2323 | Skiver | gaea | Wildlife | 122719 | 122717 / - | - | 0 | 5703 / - | 5 | \spawn monster 2323 |
| 2324 | Skiver Spitter | gaea | Wildlife | 122718 | 122720 / - | - | 0 | 5704 / - | 5 | \spawn monster 2324 |
| 2325 | King Skiver | gaea | Wildlife | 122724 | 122723 / - | - | 10005 | 5706 / - | 5 | \spawn monster 2325 |
| 2326 | Skiver Brood Matron | gaea | Wildlife | 122726 | - | - | 0 | 5704 / - | 5 | \spawn monster 2326 |
| 2327 | Skiverling | gaea | Wildlife | 122728 | 122717 / - | - | 0 | 5702 / - | 5 | \spawn monster 2327 |
| 2328 | - | chosen | Chosen | 122730 | 120585 / - | - | 0 | 3 / - | 0 | \spawn monster 2328 |
| 2329 | Skiver Drone | gaea | Wildlife | 122741 | 122742 / - | - | 0 | 5704 / - | 5 | \spawn monster 2329 |
| 2330 | - | gaea | Wildlife | 122743 | 122744 / - | - | 0 | 5704 / - | 5 | \spawn monster 2330 |
| 2331 | - | gaea | Wildlife | 122746 | 122995 / - | - | 0 | 5702 / - | 5 | \spawn monster 2331 |
| 2332 | Executioner | chosen | Chosen | 122752 | 86126 / - | - | 0 | - | 5 | \spawn monster 2332 |
| 2333 | - | gaea | Wildlife | 122748 | 122717 / - | - | 0 | 5703 / - | 5 | \spawn monster 2333 |
| 2334 | - | gaea | Wildlife | 122749 | 122750 / - | - | 0 | 5704 / - | 0 | \spawn monster 2334 |
| 2335 | Reaper Bombardier | Reapers | Outlaw | 97338 | 122753 / - | - | 0 | 5312 / 5388 | 5 | \spawn monster 2335 |
| 2336 | Scorcher | gaea | Wildlife | 122754 | 122755 / - | - | 0 | 5703 / 5779 | 5 | \spawn monster 2336 |
| 2337 | Hellfire Scorcher | gaea | Wildlife | 122756 | 122757 / - | - | 0 | 5704 / 5779 | 5 | \spawn monster 2337 |
| 2338 | Armored Scorcher | gaea | Wildlife | 122758 | 122759 / - | - | 10005 | 5705 / 5779 | 5 | \spawn monster 2338 |
| 2339 | - | gaea | Wildlife | 122761 | - | - | 0 | 5703 / 5779 | 5 | \spawn monster 2339 |
| 2340 | - | gaea | Wildlife | 122762 | 122763 / - | - | 0 | 5703 / 5779 | 5 | \spawn monster 2340 |
| 2341 | - | gaea | Wildlife | 122764 | 122765 / - | - | 0 | 5705 / 5779 | 5 | \spawn monster 2341 |
| 2342 | Aranha | gaea | Wildlife | 122767 | 122768 / - | - | 0 | 5703 / 5395 | 5 | \spawn monster 2342 |
| 2343 | Spitting Aranha | gaea | Wildlife | 122769 | 122770 / - | - | 0 | 5704 / 5396 | 5 | \spawn monster 2343 |
| 2344 | Aranha Soldier | gaea | Wildlife | 122771 | 122772 / - | - | 0 | 5704 / 5396 | 5 | \spawn monster 2344 |
| 2345 | Aranha Worker | gaea | Wildlife | 122773 | 123032 / - | - | 0 | 5702 / 5394 | 5 | \spawn monster 2345 |
| 2346 | Giant Aranha | gaea | Wildlife | 122774 | 122775 / - | - | 10005 | 5706 / 5398 | 5 | \spawn monster 2346 |
| 2347 | Salvage Bot AI | friendly | Companion | 82821 | - | - | 0 | - | 0 | \spawn monster 2347 |
| 2348 | - | friendly | Companion | 82821 | - | - | 0 | - | 0 | \spawn monster 2348 |
| 2349 | Repulsor AI | friendly | Companion | 82821 | - | - | 0 | - | 0 | \spawn monster 2349 |
| 2350 | - | friendly | Companion | 82821 | - | - | 0 | - | 0 | \spawn monster 2350 |
| 2351 | - | friendly | Companion | 82821 | - | - | 0 | - | 0 | \spawn monster 2351 |
| 2352 | - | friendly | Companion | 82821 | - | - | 0 | - | 0 | \spawn monster 2352 |
| 2353 | Accord Scientist | accord | Human | 122777 | - | Arch_MedRangedHumanoid_Base | 0 | - | 0 | \spawn monster 2353 |
| 2354 | - | gaea | Wildlife | 122930 | 82340 / - | GiantAranhaMiniBoss | 0 | 5704 / 5396 | 5 | \spawn monster 2354 |
| 2355 | - | gaea | Wildlife | 122787 | - | - | 0 | 5703 / 5631 | 5 | \spawn monster 2355 |
| 2356 | - | gaea | Wildlife | 122793 | 122796 / - | - | 0 | 5703 / 5631 | 5 | \spawn monster 2356 |
| 2357 | - | gaea | Wildlife | 122799 | 122800 / - | - | 0 | 5703 / 5395 | 5 | \spawn monster 2357 |
| 2358 | Hisser | gaea | Wildlife | 122802 | 122803 / - | - | 0 | 5703 / 5404 | 5 | \spawn monster 2358 |
| 2359 | Accord Scientist | accord | Human | 120111 | - | AlertAndInteractive | 0 | - | 5 | \spawn monster 2359 |
| 2360 | Spitting Hisser | gaea | Wildlife | 122805 | 122806 / - | - | 0 | 5704 / 5405 | 5 | \spawn monster 2360 |
| 2361 | Hisser Soldier | gaea | Wildlife | 122807 | 122808 / - | - | 0 | 5704 / 5405 | 5 | \spawn monster 2361 |
| 2362 | Metamorphic Hisser | gaea | Wildlife | 122810 | 122811 / 122813 | - | 0 | 5704 / 5405 | 5 | \spawn monster 2362 |
| 2363 | Hisser Queen | gaea | Wildlife | 122814 | 122815 / - | - | 10005 | 5706 / 5407 | 5 | \spawn monster 2363 |
| 2364 | - | gaea | Wildlife | 122816 | 122817 / - | - | 0 | 5704 / 5404 | 5 | \spawn monster 2364 |
| 2365 | - | gaea | Wildlife | 122818 | 122819 / - | - | 0 | 5704 / 5404 | 5 | \spawn monster 2365 |
| 2366 | - | gaea | Wildlife | 122820 | 122821 / - | - | 0 | 5704 / 5404 | 5 | \spawn monster 2366 |
| 2367 | - | gaea | Wildlife | 122822 | 122823 / - | - | 0 | 5704 / 5404 | 5 | \spawn monster 2367 |
| 2368 | Crab Spider Ranged | gaea | Wildlife | 122824 | 122825 / - | - | 0 | 5704 / 5396 | 5 | \spawn monster 2368 |
| 2369 | Crab Spider Blaster | gaea | Wildlife | 122826 | 122827 / - | - | 0 | 5704 / 5396 | 5 | \spawn monster 2369 |
| 2370 | Crab Spider Soldier | gaea | Wildlife | 122828 | 122829 / - | - | 0 | 5724 / 5458 | 5 | \spawn monster 2370 |
| 2371 | Crab Spider Spinner | gaea | Wildlife | 122830 | 122831 / - | - | 0 | 5704 / 5396 | 5 | \spawn monster 2371 |
| 2372 | Shield Crawler | gaea | Wildlife | 122832 | 122834 / - | - | 0 | 5706 / 5398 | 5 | \spawn monster 2372 |
| 2373 | - | gaea | Wildlife | 122836 | 122837 / - | - | 0 | 5704 / 5396 | 5 | \spawn monster 2373 |
| 2374 | - | gaea | Wildlife | 122839 | - | - | 0 | 5704 / 5396 | 5 | \spawn monster 2374 |
| 2375 | - | gaea | Wildlife | 122840 | 122841 / - | - | 0 | 5704 / 5404 | 5 | \spawn monster 2375 |
| 2376 | Rasper Spitter | gaea | Wildlife | 139935 | 122843 / - | - | 0 | 5703 / - | 5 | \spawn monster 2376 |
| 2377 | Rasper Blaster | gaea | Wildlife | 139935 | 122845 / - | - | 10004 | 5704 / - | 5 | \spawn monster 2377 |
| 2378 | Rasper Soldier | gaea | Wildlife | 139935 | 124727 / - | - | 10003 | 5704 / - | 5 | \spawn monster 2378 |
| 2379 | Rasper Heavy | gaea | Wildlife | 139935 | 122849 / - | - | 10005 | 5705 / - | 5 | \spawn monster 2379 |
| 2380 | Raspernaut | gaea | Wildlife | 139935 | 122851 / - | - | 10005 | 5706 / - | 5 | \spawn monster 2380 |
| 2381 | - | gaea | Wildlife | 122852 | 122853 / - | - | 0 | 5704 / - | 5 | \spawn monster 2381 |
| 2382 | - | gaea | Wildlife | 122854 | 122855 / - | - | 0 | 5704 / - | 5 | \spawn monster 2382 |
| 2383 | - | gaea | Wildlife | 122856 | 122857 / - | - | 0 | 5704 / - | 5 | \spawn monster 2383 |
| 2384 | Melding Carcass | melding | Melded | 122858 | 122859 / - | - | 0 | 5724 / 5458 | 5 | \spawn monster 2384 |
| 2385 | Melding Culex | melding | Melded | 122860 | 122861 / - | - | 0 | 5725 / 5459 | 5 | \spawn monster 2385 |
| 2386 | Melding Wyrm | melding | Melded | 122862 | 123059 / - | - | 0 | 5725 / 5459 | 5 | \spawn monster 2386 |
| 2387 | - | gaea | Wildlife | 122864 | 122865 / 122866 | - | 0 | 5704 / - | 5 | \spawn monster 2387 |
| 2388 | Melding Vorrax | melding | Melded | 122867 | 122868 / - | - | 10005 | 5727 / 5461 | 5 | \spawn monster 2388 |
| 2389 | Melding Vile Carcass | melding | Melded | 122870 | 122871 / - | - | 10005 | 5726 / 5460 | 5 | \spawn monster 2389 |
| 2390 | - | melding | Wildlife | 122872 | 122873 / - | - | 0 | 5724 / 5458 | 5 | \spawn monster 2390 |
| 2391 | - | melding | Wildlife | 122874 | 122875 / - | - | 0 | 5724 / 5458 | 5 | \spawn monster 2391 |
| 2392 | Chimera Sniper | bandit | Outlaw | 125087 | 96871 / 96842 | - | 0 | 5310 / 5386 | 0 | \spawn monster 2392 |
| 2393 | Buzzard Sniper | bandit | Outlaw | 125087 | 96871 / 96842 | - | 0 | 5310 / 5386 | 0 | \spawn monster 2393 |
| 2394 | Bandit Sniper | bandit | Outlaw | 125087 | 96871 / 96842 | - | 0 | 5310 / 5386 | 0 | \spawn monster 2394 |
| 2395 | Reaper Sniper | Reapers | Outlaw | 125087 | 96871 / 96842 | - | 0 | 5310 / 5386 | 0 | \spawn monster 2395 |
| 2396 | Black Hills Sniper | Black Hills Bandits | Outlaw | 125087 | 96871 / 96842 | - | 0 | 5310 / 5386 | 0 | \spawn monster 2396 |
| 2397 | Rebel Sniper | Rebels | Outlaw | 125087 | 96871 / 96842 | - | 0 | 5310 / 5386 | 0 | \spawn monster 2397 |
| 2398 | - | accord | Outlaw | 122882 | - | AlertAndInteractive | 0 | - | 5 | \spawn monster 2398 |
| 2399 | Lt. Cmdr. Kara Novan | accord | Human | 122883 | 122902 / 122989 | - | 0 | - | 10 | \spawn monster 2399 |
| 2400 | - | gaea | Wildlife | 122884 | - | - | 0 | 5704 / 5414 | 5 | \spawn monster 2400 |
| 2401 | - | chosen | Chosen | 122885 | 121444 / - | - | 0 | - | 5 | \spawn monster 2401 |
| 2402 | - | gaea | Wildlife | 122886 | 95024 / - | Arch_FullbodyMelee_Base | 0 | - | 5 | \spawn monster 2402 |
| 2403 | - | Reapers | Outlaw | 97340 | 106335 / - | - | 0 | 3 / 5388 | 5 | \spawn monster 2403 |
| 2404 | - | accord | Human | 122889 | 96842 / - | - | 0 | - | 0 | \spawn monster 2404 |
| 2405 | Crystite Aranha Turret Gunner | gaea | Wildlife | 32740 | 122891 / - | TurretTeleporterDropshipCannon | 0 | - | 0 | \spawn monster 2405 |
| 2406 | Crystite Aranha | gaea | Wildlife | 122893 | 123385 / - | - | 0 | 6137 / 5398 | 5 | \spawn monster 2406 |
| 2407 | Tanken Saboteur | Tanken | Outlaw | 122894 | 122895 / - | - | 0 | 5311 / 5387 | 5 | \spawn monster 2407 |
| 2408 | Capt. Hudson Fuller | accord | Human | 122899 | 122900 / - | - | 0 | - | 10 | \spawn monster 2408 |
| 2409 | Mason Chen | accord | Human | 122901 | 124798 / 122989 | - | 0 | - | 10 | \spawn monster 2409 |
| 2410 | Chosen Shield Drone | chosen | Chosen | 75661 | 75681 / - | - | 0 | 5696 / 5615 | 5 | \spawn monster 2410 |
| 2411 | - | gaea | Wildlife | 122909 | 122910 / - | - | 0 | 5704 / 6219 | 5 | \spawn monster 2411 |
| 2412 | Storm Kestrel | gaea | Wildlife | 122912 | 124553 / - | - | 0 | 5705 / - | 5 | \spawn monster 2412 |
| 2413 | Nautilus | gaea | Wildlife | 122917 | 122918 / - | - | 0 | 5705 / 5451 | 5 | \spawn monster 2413 |
| 2414 | - | gaea | Wildlife | 81319 | 122919 / - | - | 0 | 5705 / 5451 | 5 | \spawn monster 2414 |
| 2415 | - | gaea | Wildlife | 122921 | 122891 / - | - | 0 | 5704 / - | 5 | \spawn monster 2415 |
| 2416 | - | gaea | Wildlife | 122923 | 122925 / - | - | 0 | 5705 / 5415 | 5 | \spawn monster 2416 |
| 2417 | Chosen Defiler | chosen | Chosen | 122926 | 143829 / - | - | 0 | 5699 / 5370 | 5 | \spawn monster 2417 |
| 2418 | - | chosen | Chosen | 118699 | 118697 / - | - | 0 | 5699 / 5370 | 5 | \spawn monster 2418 |
| 2419 | Reaper Captain Cherise's Shield | Reapers | Outlaw | 122940 | - | Arch_Follower | 0 | - | 5 | \spawn monster 2419 |
| 2420 | [OBSOLETE] Elite Chosen Infantry | chosen | Chosen | 122941 | 122942 / - | - | 0 | 5697 / 5368 | 5 | \spawn monster 2420 |
| 2421 | [OBSOLETE] Elite Chosen Shock Trooper | chosen | Chosen | 122943 | 122944 / - | - | 0 | 5698 / 5369 | 5 | \spawn monster 2421 |
| 2422 | Technician | accord | Human | 122957 | - | - | 0 | - | 0 | \spawn monster 2422 |
| 2423 | Pantheon Event Boss | chosen | Chosen | 122964 | 122949 / 122955 | Arch_MoveThenFire_Base | 0 | - | 0 | \spawn monster 2423 |
| 2424 | Captive Chosen Stasis Field | chosen | Chosen | 122959 | - | Arch_Follower | 0 | - | 5 | \spawn monster 2424 |
| 2425 | Accord Marine | accord | Human | 122965 | 96842 / - | - | 0 | 5310 / - | 0 | \spawn monster 2425 |
| 2426 | Accord Ranger | accord | Human | 122966 | 96847 / - | - | 0 | 5311 / - | 0 | \spawn monster 2426 |
| 2427 | Accord Assault | accord | Human | 122967 | 122968 / - | - | 0 | 5312 / - | 0 | \spawn monster 2427 |
| 2428 | Accord Dreadnaught | accord | Human | 122969 | 106335 / - | - | 0 | 5313 / - | 0 | \spawn monster 2428 |
| 2429 | Accord Nighthawk | accord | Human | 125087 | 96871 / 96842 | - | 0 | 5310 / - | 0 | \spawn monster 2429 |
| 2430 | Dead Reaper Raider | neutral | Outlaw | 122975 | 96847 / - | - | 0 | 5311 / 5386 | 5 | \spawn monster 2430 |
| 2431 | Special Aranha | gaea | Wildlife | 122992 | 82340 / - | GiantAranhaMiniBoss | 0 | 5704 / 5396 | 5 | \spawn monster 2431 |
| 2432 | Dead Reaper Rifleman | neutral | Outlaw | 122983 | 96842 / - | - | 0 | 5311 / 5386 | 0 | \spawn monster 2432 |
| 2433 | Dead Reaper Cannoneer | neutral | Outlaw | 122984 | 96848 / - | - | 0 | 5311 / 5387 | 5 | \spawn monster 2433 |
| 2434 | Special Hisser | gaea | Wildlife | 122986 | 121050 / - | - | 0 | 5704 / 5404 | 5 | \spawn monster 2434 |
| 2435 | Aranha Queen | gaea | Wildlife | 122993 | 143828 / - | - | 0 | 5706 / 5395 | 5 | \spawn monster 2435 |
| 2436 | GiGi | friendly | Companion | 82821 | - | - | 0 | - | 0 | \spawn monster 2436 |
| 2437 | - | accord | Human | 81391 | 84915 / - | GuardCityWanderer | 0 | - | 0 | \spawn monster 2437 |
| 2438 | SIN Phantom | bandit | Outlaw | 125069 | 96847 / - | - | 0 | 3 / - | 0 | \spawn monster 2438 |
| 2439 | SIN Phantom | bandit | Outlaw | 125087 | 96871 / 96842 | - | 0 | 2 / - | 0 | \spawn monster 2439 |
| 2440 | Traitorous Ranger | bandit | Outlaw | 123002 | 96847 / - | - | 0 | 5311 / - | 0 | \spawn monster 2440 |
| 2441 | Traitorous Sniper | bandit | Outlaw | 123003 | 96871 / 96842 | - | 0 | 5310 / - | 0 | \spawn monster 2441 |
| 2442 | - | neutral | Large wildlife | 123004 | 95394 / - | Brontodon | 0 | 5703 / 5440 | 5 | \spawn monster 2442 |
| 2443 | - | gaea | Wildlife | 123009 | 123010 / - | - | 0 | 5704 / - | 5 | \spawn monster 2443 |
| 2444 | Icy Aranha | gaea | Wildlife | 123014 | 123015 / - | - | 0 | 5703 / 5632 | 5 | \spawn monster 2444 |
| 2445 | Icy Aranha Spitter | gaea | Wildlife | 123016 | 123017 / - | - | 0 | 5704 / 5632 | 5 | \spawn monster 2445 |
| 2446 | Freezing Aranha | gaea | Wildlife | 123018 | 123019 / - | - | 0 | 5704 / 5632 | 5 | \spawn monster 2446 |
| 2447 | Icy Shield Aranha | gaea | Wildlife | 123021 | - | - | 0 | 5704 / 5632 | 5 | \spawn monster 2447 |
| 2448 | Blizzard Aranha | gaea | Wildlife | 123022 | 123023 / - | - | 10005 | 5706 / 5632 | 5 | \spawn monster 2448 |
| 2449 | Infected Kestrel Stormer | gaea | Wildlife | 123025 | 123026 / - | - | 0 | 5704 / - | 5 | \spawn monster 2449 |
| 2450 | Infected Kestrel Duster | gaea | Wildlife | 123028 | 123029 / - | - | 0 | 5704 / - | 5 | \spawn monster 2450 |
| 2451 | - | gaea | Wildlife | 123030 | 123031 / - | - | 0 | 5704 / - | 5 | \spawn monster 2451 |
| 2452 | Poacher | bandit | Outlaw | 125065 | 96842 / - | - | 0 | 5310 / 5386 | 0 | \spawn monster 2452 |
| 2453 | Poacher Game Hunter | bandit | Outlaw | 125069 | 96847 / - | - | 0 | 5311 / 5387 | 0 | \spawn monster 2453 |
| 2454 | Poacher Grenadier | bandit | Outlaw | 125146 | 96848 / - | - | 0 | 5312 / 5388 | 0 | \spawn monster 2454 |
| 2455 | Poacher Heavy Gunner | bandit | Outlaw | 125034 | 106335 / - | - | 0 | 5313 / 5389 | 0 | \spawn monster 2455 |
| 2456 | Poacher Sniper | bandit | Outlaw | 125087 | 96871 / 96842 | - | 0 | 5310 / 5386 | 0 | \spawn monster 2456 |
| 2457 | Special  Brujo the Breaker | gaea | Wildlife | 123039 | - | Arch_FullbodyMelee_Base | 0 | 5705 / 5778 | 5 | \spawn monster 2457 |
| 2458 | Special Hydrus - Menace of the Mire | gaea | Wildlife | 123040 | 118833 / - | - | 0 | 5704 / - | 5 | \spawn monster 2458 |
| 2459 | Special Haken | gaea | Wildlife | 123042 | 118845 / - | - | 0 | 5706 / - | 5 | \spawn monster 2459 |
| 2460 | Special Strix | gaea | Wildlife | 123043 | 118850 / - | - | 0 | 3 / - | 5 | \spawn monster 2460 |
| 2461 | - | gaea | Wildlife | 123044 | 66846 / - | FeralCanineSquad | 0 | 5704 / 6211 | 5 | \spawn monster 2461 |
| 2462 | Special Gobler the Gorger | gaea | Wildlife | 123045 | - | Arch_FullbodyMelee_Base | 0 | 5705 / 5778 | 5 | \spawn monster 2462 |
| 2463 | Special Drorgan the Maneater | gaea | Wildlife | 123046 | 66846 / - | FeralCanineSquad | 0 | 5704 / 6211 | 5 | \spawn monster 2463 |
| 2464 | Special Danko the Stalker | neutral | Large wildlife | 123047 | 77118 / 77119 | Wyrm | 0 | 5704 / 6219 | 5 | \spawn monster 2464 |
| 2465 | Special Nymero the Widowmaker | neutral | Wildlife | 123048 | 77268 / 77248 | - | 0 | 5704 / - | 5 | \spawn monster 2465 |
| 2466 | Special Kojo the Murderer | gaea | Wildlife | 123049 | 82585 / - | Arch_AdditiveMelee_Base | 0 | 5705 / 6213 | 5 | \spawn monster 2466 |
| 2467 | Special Kruger the Rampager | gaea | Wildlife | 123050 | 85191 / - | Scarab | 0 | 5706 / 5425 | 5 | \spawn monster 2467 |
| 2468 | Special Anubis the Gravedigger | melding | Melded | 123041 | 120606 / - | - | 0 | 5704 / - | 5 | \spawn monster 2468 |
| 2469 | Accord Dropship Pilot | accord | Human | 125033 | - | - | 0 | - | 0 | \spawn monster 2469 |
| 2470 | - | chosen | Chosen | 123053 | 118851 / - | - | 0 | 5697 / 5458 | 5 | \spawn monster 2470 |
| 2471 | Lieutenant Cato | accord | Human | 117032 | 87788 / - | AlertAndInteractive | 0 | - | 0 | \spawn monster 2471 |
| 2472 | - | bandit | Misc | 123061 | - | Arch_AdditiveMelee_Base | 0 | - | 0 | \spawn monster 2472 |
| 2473 | Accord Soldier | accord | Human | 123208 | 87741 / 76108 | AlertAndLookAtPlayer | 0 | - | 0 | \spawn monster 2473 |
| 2474 | Accord Soldier | accord | Human | 123208 | 87741 / 76108 | AlertAndLookAtPlayer | 0 | - | 0 | \spawn monster 2474 |
| 2475 | - | accord | Human | 139722 | - | - | 0 | - | 0 | \spawn monster 2475 |
| 2476 | - | - | Human | 0 | - | - | 0 | - | 0 | \spawn monster 2476 |
| 2477 | Accord Soldier | accord | Human | 123212 | 97695 / 76108 | AlertAndLookAtPlayer | 0 | - | 0 | \spawn monster 2477 |
| 2478 | Lieutenant Draper | accord | Human | 117031 | 84915 / - | AlertAndInteractive | 0 | - | 0 | \spawn monster 2478 |
| 2479 | Chief Engineer Mullen | accord | Human | 120619 | 84915 / - | AlertAndInteractive | 0 | - | 0 | \spawn monster 2479 |
| 2480 | Commander Harrelson | accord | Human | 117034 | 84915 / - | AlertAndInteractive | 0 | - | 0 | \spawn monster 2480 |
| 2481 | - | accord | Human | 117022 | - | AlertAndInteractive | 0 | - | 0 | \spawn monster 2481 |
| 2482 | - | accord | Human | 123222 | 96842 / - | - | 0 | 5311 / 5386 | 0 | \spawn monster 2482 |
| 2483 | - | accord | Human | 123223 | 96842 / - | - | 0 | 5311 / 5386 | 0 | \spawn monster 2483 |
| 2484 | Spicy Sal | accord | Human | 123224 | 96842 / - | - | 0 | 5311 / 5386 | 0 | \spawn monster 2484 |
| 2485 | Toxic Aranha | gaea | Wildlife | 123226 | 123227 / - | - | 0 | 5703 / 5633 | 5 | \spawn monster 2485 |
| 2486 | Toxic Spitting Aranha | gaea | Wildlife | 123228 | 123229 / - | - | 0 | 5704 / 5633 | 5 | \spawn monster 2486 |
| 2487 | Toxic Aranha Defiler | gaea | Wildlife | 123230 | 123231 / - | - | 0 | 5704 / 5633 | 5 | \spawn monster 2487 |
| 2488 | Toxic Aranha Slinger | gaea | Wildlife | 123232 | 123233 / - | - | 10005 | 5705 / 5633 | 5 | \spawn monster 2488 |
| 2489 | Toxic Biohazard Aranha | gaea | Wildlife | 123234 | 123235 / - | - | 10005 | 5706 / 5633 | 5 | \spawn monster 2489 |
| 2490 | - | accord | Human | 117034 | - | AlertAndLookAtPlayer | 0 | - | 0 | \spawn monster 2490 |
| 2491 | - | accord | Human | 117034 | - | AlertAndLookAtPlayer | 0 | - | 0 | \spawn monster 2491 |
| 2492 | Accord Private | accord | Human | 123208 | 87741 / 76108 | Null | 0 | - | 0 | \spawn monster 2492 |
| 2493 | Sergeant Bell | accord | Human | 118244 | 97695 / 76108 | Arch_MedRangedHumanoid_Base | 0 | - | 0 | \spawn monster 2493 |
| 2494 | Shared Turret Gunner | accord | Misc | 120582 | 92799 / - | - | 0 | - | 0 | \spawn monster 2494 |
| 2495 | B.E.L.A.A. | accord | Human | 124085 | - | AlertAndInteractive | 0 | - | 0 | \spawn monster 2495 |
| 2496 | - | accord | Human | 0 | - | Null | 0 | - | 0 | \spawn monster 2496 |
| 2497 | - | accord | Human | 0 | - | Null | 0 | - | 0 | \spawn monster 2497 |
| 2498 | - | accord | Human | 0 | - | Null | 0 | - | 0 | \spawn monster 2498 |
| 2499 | Metamorphic Hisser | gaea | Wildlife | 124086 | - / 122813 | - | 0 | 5704 / 5404 | 5 | \spawn monster 2499 |
| 2500 | Sergeant Townsend | accord | Human | 77865 | - | AlertAndInteractive | 0 | - | 0 | \spawn monster 2500 |
| 2501 | Sergeant Gunner | accord | Human | 82360 | 20006 / - | AlertAndInteractive | 0 | - | 0 | \spawn monster 2501 |
| 2502 | Sergeant Skyler | accord | Human | 85694 | - | AlertAndInteractive | 0 | - | 0 | \spawn monster 2502 |
| 2503 | Mechanic | accord | Human | 97027 | - | AlertAndInteractive | 0 | - | 0 | \spawn monster 2503 |
| 2504 | Invisible_Man | friendly | Human | 123210 | - | - | 0 | - | 0 | \spawn monster 2504 |
| 2505 | [OBSOLETE] Elite Aranha | gaea | Wildlife | 124093 | 122768 / - | - | 0 | 5703 / 5395 | 5 | \spawn monster 2505 |
| 2506 | [OBSOLETE] Elite Spitting Aranha | gaea | Wildlife | 124098 | 122770 / - | - | 0 | 5704 / 5396 | 5 | \spawn monster 2506 |
| 2507 | - | gaea | Wildlife | 124099 | 122803 / - | - | 0 | 5703 / 5404 | 5 | \spawn monster 2507 |
| 2508 | - | gaea | Wildlife | 124100 | 122806 / - | - | 0 | 5704 / 5405 | 5 | \spawn monster 2508 |
| 2509 | - | gaea | Wildlife | 124101 | 122717 / - | - | 0 | 5703 / - | 5 | \spawn monster 2509 |
| 2510 | - | gaea | Wildlife | 124102 | 122720 / - | - | 0 | 5704 / - | 5 | \spawn monster 2510 |
| 2511 | - | melding | Melded | 124103 | 122859 / - | - | 0 | 5703 / 5458 | 5 | \spawn monster 2511 |
| 2512 | [OBSOLETE] Elite Melding Culex | melding | Melded | 124104 | 122861 / - | - | 0 | 5704 / 5459 | 5 | \spawn monster 2512 |
| 2513 | [OBSOLETE] Elite Tanken Gunman | Tanken | Outlaw | 124105 | 96842 / - | - | 0 | 5310 / 5386 | 0 | \spawn monster 2513 |
| 2514 | [OBSOLETE] Elite Tanken Mobster | Tanken | Outlaw | 124106 | 96847 / - | - | 0 | 5311 / 5387 | 0 | \spawn monster 2514 |
| 2515 | [OBSOLETE] Elite Bandit Punk | bandit | Outlaw | 124107 | 96842 / - | - | 0 | 5310 / 5386 | 0 | \spawn monster 2515 |
| 2516 | - | bandit | Outlaw | 124108 | 96847 / - | - | 0 | 5311 / 5387 | 0 | \spawn monster 2516 |
| 2517 | [OBSOLETE] Elite Rebel Fighter | Rebels | Outlaw | 124109 | 96842 / - | - | 0 | 5310 / 5386 | 0 | \spawn monster 2517 |
| 2518 | [OBSOLETE] Elite Rebel Guerilla | Rebels | Outlaw | 124110 | 96847 / - | - | 0 | 5311 / 5387 | 5 | \spawn monster 2518 |
| 2519 | - | Reapers | Outlaw | 124111 | 96842 / - | - | 0 | 5310 / 5386 | 0 | \spawn monster 2519 |
| 2520 | - | Reapers | Outlaw | 124112 | 96847 / - | - | 0 | 5311 / 5387 | 5 | \spawn monster 2520 |
| 2521 | - | Black Hills Bandits | Outlaw | 124113 | 96842 / - | - | 0 | 5310 / 5386 | 0 | \spawn monster 2521 |
| 2522 | - | Black Hills Bandits | Outlaw | 124114 | 96847 / - | - | 0 | 5311 / 5387 | 5 | \spawn monster 2522 |
| 2523 | [OBSOLETE] Elite Blood King Contractor | bandit | Outlaw | 124115 | 96842 / - | - | 0 | 5310 / - | 0 | \spawn monster 2523 |
| 2524 | [OBSOLETE] Elite Blood King Soldier | bandit | Outlaw | 124116 | 96847 / - | - | 0 | 5311 / - | 0 | \spawn monster 2524 |
| 2525 | Mendraxus | chosen | Chosen | 124118 | - | - | 0 | - | 5 | \spawn monster 2525 |
| 2526 | - | accord | Human | 124117 | 96842 / - | - | 0 | 5310 / - | 0 | \spawn monster 2526 |
| 2527 | [OBSOLETE] Elite Accord Ranger | accord | Human | 124119 | 96847 / - | - | 0 | 5311 / - | 0 | \spawn monster 2527 |
| 2528 | - | bandit | Outlaw | 124125 | 96842 / - | - | 0 | 5310 / 5386 | 0 | \spawn monster 2528 |
| 2529 | Master Poacher Game Hunter | bandit | Outlaw | 142074 | 106335 / - | - | 0 | 5313 / 5389 | 0 | \spawn monster 2529 |
| 2530 | - | gaea | Wildlife | 124127 | 123015 / - | - | 0 | 5395 / 5632 | 5 | \spawn monster 2530 |
| 2531 | - | gaea | Wildlife | 124128 | 123017 / - | - | 0 | 5396 / 5632 | 5 | \spawn monster 2531 |
| 2532 | - | gaea | Wildlife | 124130 | 123227 / - | - | 0 | 5395 / 5633 | 5 | \spawn monster 2532 |
| 2533 | - | gaea | Wildlife | 124131 | 123229 / - | - | 0 | 5396 / 5633 | 5 | \spawn monster 2533 |
| 2534 | - | accord | Human | 86619 | 78496 / - | Arch_MedRangedHumanoid_Base | 0 | - | 0 | \spawn monster 2534 |
| 2535 | [OBSOLETE] Elite Scorcher | gaea | Wildlife | 124164 | 122755 / - | - | 0 | 5703 / 5779 | 5 | \spawn monster 2535 |
| 2536 | [OBSOLETE] Elite Hellfire Scorcher | gaea | Wildlife | 124165 | 122757 / - | - | 0 | 5703 / 5779 | 5 | \spawn monster 2536 |
| 2537 | - | gaea | Wildlife | 124166 | 122825 / - | - | 0 | 5704 / 5396 | 5 | \spawn monster 2537 |
| 2538 | - | gaea | Wildlife | 124167 | 122827 / - | - | 0 | 5704 / 5396 | 5 | \spawn monster 2538 |
| 2539 | - | gaea | Wildlife | 124168 | 122843 / - | - | 0 | 5704 / - | 5 | \spawn monster 2539 |
| 2540 | - | gaea | Wildlife | 124169 | 122845 / - | - | 0 | 5704 / - | 5 | \spawn monster 2540 |
| 2541 | - | gaea | Wildlife | 124170 | 121012 / - | - | 0 | 5703 / 5404 | 5 | \spawn monster 2541 |
| 2542 | Elite Matriarch Hisser | gaea | Wildlife | 124171 | 121050 / - | - | 0 | 5704 / 5405 | 5 | \spawn monster 2542 |
| 2543 | - | Ophanim | Outlaw | 124172 | 96842 / - | - | 0 | 5311 / - | 0 | \spawn monster 2543 |
| 2544 | - | Ophanim | Outlaw | 124173 | 96847 / - | - | 0 | 5311 / - | 0 | \spawn monster 2544 |
| 2545 | - | bandit | Outlaw | 124174 | 96842 / - | - | 0 | 5311 / 5386 | 0 | \spawn monster 2545 |
| 2546 | - | bandit | Outlaw | 124175 | 96847 / - | - | 0 | 5311 / 5386 | 0 | \spawn monster 2546 |
| 2547 | - | bandit | Outlaw | 124176 | 96842 / - | - | 0 | 5311 / 5386 | 0 | \spawn monster 2547 |
| 2548 | - | bandit | Outlaw | 124177 | 96847 / - | - | 0 | 5311 / 5386 | 0 | \spawn monster 2548 |
| 2549 | - | gaea | Wildlife | 124178 | 123024 / - | - | 0 | 5704 / - | 5 | \spawn monster 2549 |
| 2550 | - | gaea | Wildlife | 124179 | 124553 / - | - | 0 | 5704 / - | 5 | \spawn monster 2550 |
| 2551 | [OBSOLETE] Elite Hate-Bot | gaea | Wildlife | 124180 | - | - | 0 | 2 / - | 0 | \spawn monster 2551 |
| 2552 | - | bandit | Outlaw | 124183 | 96842 / - | - | 0 | 5310 / - | 0 | \spawn monster 2552 |
| 2553 | - | bandit | Outlaw | 124184 | 96847 / - | - | 0 | 5311 / - | 0 | \spawn monster 2553 |
| 2554 | - | chosen | Chosen | 85952 | 85953 / - | - | 0 | 5698 / 5369 | 5 | \spawn monster 2554 |
| 2555 | Grunt Soldier | chosen | Chosen | 124194 | 85953 / - | Null | 0 | - | 5 | \spawn monster 2555 |
| 2556 | Shock Trooper | chosen | Chosen | 124195 | 32739 / - | Null | 0 | - | 5 | \spawn monster 2556 |
| 2557 | Sergeant Goodspeed | accord | Human | 75773 | - | AlertAndInteractive | 0 | - | 0 | \spawn monster 2557 |
| 2558 | - | bandit | Outlaw | 124243 | 96842 / - | - | 0 | 2 / - | 0 | \spawn monster 2558 |
| 2559 | - | bandit | Outlaw | 124244 | 96847 / - | - | 0 | 3 / - | 0 | \spawn monster 2559 |
| 2560 | Milo Quattrocchi | accord | Human | 117023 | - | AlertAndLookAtPlayer | 0 | - | 0 | \spawn monster 2560 |
| 2561 | - | Black Hills Bandits | Outlaw | 75281 | - | - | 0 | - | 0 | \spawn monster 2561 |
| 2562 | Radic | friendly | Companion | 93796 | 81305 / - | Arch_MedRangedHumanoid_Base | 0 | 5311 / 5388 | 5 | \spawn monster 2562 |
| 2563 | Litch | Black Hills Bandits | Outlaw | 75281 | - | StationaryCivilianDialog | 0 | - | 0 | \spawn monster 2563 |
| 2564 | Cade | Black Hills Bandits | Outlaw | 142074 | 96847 / - | - | 0 | 5313 / 5389 | 0 | \spawn monster 2564 |
| 2565 | Operations Agent | accord | Human | 96744 | - | AlertAndInteractive | 0 | - | 0 | \spawn monster 2565 |
| 2566 | - | - | Human | 121090 | - | - | 0 | - | 0 | \spawn monster 2566 |
| 2567 | Brontodon | friendly | Companion | 30033 | 77093 / 77140 | AlertAndInteractive | 0 | 5705 / 5442 | 5 | \spawn monster 2567 |
| 2568 | Elder Brontodon | friendly | Companion | 77425 | 77093 / 77140 | AlertAndInteractive | 0 | 5705 / 5443 | 5 | \spawn monster 2568 |
| 2569 | Aranha Hatchling | gaea | Wildlife | 31218 | 20046 / - | - | 0 | - | 5 | \spawn monster 2569 |
| 2570 | Lieutenant Sands | accord | Human | 97065 | - | AlertAndInteractive | 0 | - | 0 | \spawn monster 2570 |
| 2571 | Eben Faraday | accord | Human | 96486 | - | GuardCityWanderer | 0 | - | 0 | \spawn monster 2571 |
| 2572 | Operations Agent | accord | Human | 96744 | - | AlertAndInteractive | 0 | - | 0 | \spawn monster 2572 |
| 2573 | Iona Rhodes | accord | Human | 124690 | - | Alert | 0 | - | 0 | \spawn monster 2573 |
| 2574 | Corporal Rhodes | accord | Human | 124339 | 79272 / - | Alert | 0 | - | 0 | \spawn monster 2574 |
| 2575 | - | chosen | Chosen | 124344 | 124343 / - | - | 0 | 5696 / 5615 | 5 | \spawn monster 2575 |
| 2576 | - | Black Hills Bandits | Outlaw | 118033 | 67425 / - | Arch_MedRangedHumanoid_Base | 0 | - | 0 | \spawn monster 2576 |
| 2577 | Sergeant Kang | Black Hills Bandits | Outlaw | 142074 | 122753 / - | - | 0 | 5313 / 5389 | 5 | \spawn monster 2577 |
| 2578 | Sergeant Levy | accord | Human | 117031 | 87788 / - | AlertAndInteractive | 0 | - | 0 | \spawn monster 2578 |
| 2579 | Accord Soldier | accord | Human | 117994 | 97953 / 76108 | Arch_MedRangedHumanoid_Base | 0 | - | 0 | \spawn monster 2579 |
| 2580 | Accord Soldier | accord | Human | 117988 | 97695 / 76108 | Arch_MedRangedHumanoid_Base | 0 | - | 0 | \spawn monster 2580 |
| 2581 | Radic | Black Hills Bandits | Companion | 139722 | 106335 / - | - | 0 | 5313 / 5388 | 5 | \spawn monster 2581 |
| 2582 | Barone | bandit | Outlaw | 81304 | 81305 / - | - | 0 | 5311 / 5388 | 5 | \spawn monster 2582 |
| 2583 | Theo Pascal | accord | Human | 117010 | - | AlertAndInteractive | 0 | - | 10 | \spawn monster 2583 |
| 2584 | Mitch | Black Hills Bandits | Outlaw | 75281 | - | StationaryCivilianDialog | 0 | - | 0 | \spawn monster 2584 |
| 2585 | Corporal Platt | accord | Human | 123212 | - | AlertAndInteractive | 0 | - | 0 | \spawn monster 2585 |
| 2586 | Attack Drone | chosen | Chosen | 124545 | 124546 / - | - | 0 | 5696 / 5615 | 5 | \spawn monster 2586 |
| 2587 | Sal | accord | Human | 117012 | - | BasicCivilian | 0 | - | 0 | \spawn monster 2587 |
| 2588 | Gal | accord | Human | 117013 | - | BasicCivilian | 0 | - | 0 | \spawn monster 2588 |
| 2589 | Freya | accord | Human | 117017 | - | BasicCivilian | 0 | - | 0 | \spawn monster 2589 |
| 2590 | Ed | accord | Human | 117011 | - | BasicCivilian | 0 | - | 0 | \spawn monster 2590 |
| 2591 | Fran | accord | Human | 82563 | - | BasicCivilian | 0 | - | 0 | \spawn monster 2591 |
| 2592 | Bran | accord | Human | 117022 | - | BasicCivilian | 0 | - | 0 | \spawn monster 2592 |
| 2593 | Jackson | accord | Human | 52453 | - | BasicCivilian | 0 | - | 0 | \spawn monster 2593 |
| 2594 | Robertson | accord | Human | 52453 | - | BasicCivilian | 0 | - | 0 | \spawn monster 2594 |
| 2595 | Leia | accord | Human | 117014 | - | BasicCivilian | 0 | - | 0 | \spawn monster 2595 |
| 2596 | Luke | accord | Human | 52456 | - | BasicCivilian | 0 | - | 0 | \spawn monster 2596 |
| 2597 | Han | accord | Human | 82563 | - | BasicCivilian | 0 | - | 0 | \spawn monster 2597 |
| 2598 | Carrie | accord | Human | 117015 | - | BasicCivilian | 0 | - | 0 | \spawn monster 2598 |
| 2599 | Stella | accord | Human | 117011 | - | BasicCivilian | 0 | - | 0 | \spawn monster 2599 |
| 2600 | Dan | accord | Human | 52457 | - | BasicCivilian | 0 | - | 0 | \spawn monster 2600 |
| 2601 | Krueger | accord | Human | 117031 | 114323 / - | BasicCivilian | 0 | - | 0 | \spawn monster 2601 |
| 2602 | Luger | accord | Human | 117034 | 114501 / - | BasicCivilian | 0 | - | 0 | \spawn monster 2602 |
| 2603 | Gary | accord | Human | 117026 | - | BasicCivilian | 0 | - | 0 | \spawn monster 2603 |
| 2604 | Harry | accord | Human | 117029 | - | BasicCivilian | 0 | - | 0 | \spawn monster 2604 |
| 2605 | Pam | accord | Human | 124340 | - | BasicCivilian | 0 | - | 0 | \spawn monster 2605 |
| 2606 | Sam | accord | Human | 124340 | - | BasicCivilian | 0 | - | 0 | \spawn monster 2606 |
| 2607 | Citizen | accord | Human | 117012 | - | BasicCivilian | 0 | - | 0 | \spawn monster 2607 |
| 2608 | Alvaro Cordozo | accord | Human | 125033 | - | - | 0 | - | 10 | \spawn monster 2608 |
| 2609 | Copacabana Accord Soldier - PerformEmote | accord | Human | 117034 | 84915 / - | BasicCivilian_Stationary | 0 | - | 0 | \spawn monster 2609 |
| 2610 | - | accord | Human | 117010 | - | PerformEmote | 0 | - | 0 | \spawn monster 2610 |
| 2611 | Copacabana Civilian - PerformEmote | accord | Human | 117015 | - | PerformEmote | 0 | - | 0 | \spawn monster 2611 |
| 2612 | Copacabana swimsuit - PerformEmote | accord | Human | 124340 | - | PerformEmote | 0 | - | 0 | \spawn monster 2612 |
| 2613 | Copacabana Accord Soldier - Ambient - No gun - PerformEmote | accord | Human | 117024 | - | PerformEmote | 0 | - | 0 | \spawn monster 2613 |
| 2614 | Hydrocore Accord Soldier - Ambient | accord | Human | 117034 | 84915 / - | BasicCivilian_Stationary | 0 | - | 0 | \spawn monster 2614 |
| 2615 | - | accord | Human | 117034 | 84915 / - | BasicCivilian_Stationary | 0 | - | 0 | \spawn monster 2615 |
| 2616 | Hydrocore ARES Pilot - Ambient - Assault | accord | Human | 117031 | 84915 / - | BasicCivilian_Stationary | 0 | - | 0 | \spawn monster 2616 |
| 2617 | Hydrocore - Scientist | accord | Human | 124561 | - | BasicCivilian_Stationary | 0 | - | 0 | \spawn monster 2617 |
| 2618 | - | accord | Human | 122777 | - | Arch_MedRangedHumanoid_Base | 0 | - | 0 | \spawn monster 2618 |
| 2619 | Hydrocore Worker - Ambient Barker | accord | Human | 117015 | - | BasicCivilian_Stationary | 0 | - | 0 | \spawn monster 2619 |
| 2620 | Copacabana Resort Staff | accord | Human | 117014 | - | AlertAndLookAtPlayer | 0 | - | 0 | \spawn monster 2620 |
| 2621 | - | accord | Human | 124340 | - | AlertAndInteractive | 0 | - | 0 | \spawn monster 2621 |
| 2622 | Engineer Mechanic | accord | Human | 117022 | - | AlertAndInteractive | 0 | - | 0 | \spawn monster 2622 |
| 2623 | Omnidyne-M Information Officer | accord | Human | 117013 | - | AlertAndInteractive | 0 | - | 0 | \spawn monster 2623 |
| 2624 | Hydrocore ARES Pilot - Ambient - Biotech | accord | Human | 117033 | 84915 / - | BasicCivilian_Stationary | 0 | - | 0 | \spawn monster 2624 |
| 2625 | Accord Tech Operative | accord | Human | 124589 | - | AlertAndInteractive | 0 | - | 5 | \spawn monster 2625 |
| 2626 | Hisser Hatchling | accord | Companion | 124593 | 122806 / - | - | 0 | - | 5 | \spawn monster 2626 |
| 2627 | El Lute | accord | Human | 125033 | - | - | 0 | - | 0 | \spawn monster 2627 |
| 2628 | Harry Pendel | accord | Human | 125033 | - | - | 0 | - | 0 | \spawn monster 2628 |
| 2629 | - | accord | Human | 124340 | - | AlertAndLookAtPlayer | 0 | - | 0 | \spawn monster 2629 |
| 2630 | Accord Soldier - Ambient Pathing | accord | Human | 117035 | 137167 / - | Arch_MedRangedHumanoid_Base | 0 | - | 0 | \spawn monster 2630 |
| 2631 | Nutretic Technician | accord | Human | 124690 | - | BasicCivilian_Stationary | 0 | - | 0 | \spawn monster 2631 |
| 2632 | - | accord | Human | 76348 | 120505 / - | NavigateToLocation | 0 | - | 0 | \spawn monster 2632 |
| 2633 | - | accord | Human | 124680 | 85954 / 125187 | - | 0 | 5312 / - | 0 | \spawn monster 2633 |
| 2634 | - | accord | Human | 124682 | - | - | 0 | - | 0 | \spawn monster 2634 |
| 2635 | - | accord | Human | 124683 | - | - | 0 | - | 0 | \spawn monster 2635 |
| 2636 | - | accord | Human | 124684 | - | - | 0 | - | 0 | \spawn monster 2636 |
| 2637 | - | accord | Human | 124685 | - | - | 0 | - | 0 | \spawn monster 2637 |
| 2638 | - | accord | Human | 124687 | - | - | 0 | - | 0 | \spawn monster 2638 |
| 2639 | - | accord | Human | 124682 | - | - | 0 | - | 0 | \spawn monster 2639 |
| 2640 | - | accord | Human | 124758 | - | - | 0 | - | 0 | \spawn monster 2640 |
| 2641 | - | accord | Human | 124758 | - | - | 0 | - | 0 | \spawn monster 2641 |
| 2642 | - | accord | Human | 124684 | - | - | 0 | - | 0 | \spawn monster 2642 |
| 2643 | - | accord | Human | 124687 | - | - | 0 | - | 0 | \spawn monster 2643 |
| 2644 | - | accord | Human | 124685 | - | - | 0 | - | 0 | \spawn monster 2644 |
| 2645 | - | accord | Human | 124683 | - | - | 0 | - | 0 | \spawn monster 2645 |
| 2646 | Thump Dump Personnel | accord | Human | 117022 | - | BasicCivilian_Stationary | 0 | - | 0 | \spawn monster 2646 |
| 2647 | - | chosen | Chosen | 118216 | 122944 / - | - | 0 | 5698 / 5369 | 5 | \spawn monster 2647 |
| 2648 | Nutretic Technician | accord | Human | 125033 | - | BasicCivilian_Stationary | 0 | - | 0 | \spawn monster 2648 |
| 2649 | - | accord | Human | 124697 | - | - | 0 | - | 0 | \spawn monster 2649 |
| 2650 | - | accord | Human | 124698 | - | - | 0 | - | 0 | \spawn monster 2650 |
| 2651 | - | accord | Human | 124699 | - | - | 0 | - | 0 | \spawn monster 2651 |
| 2652 | - | accord | Human | 124700 | - | - | 0 | - | 0 | \spawn monster 2652 |
| 2653 | - | accord | Human | 76332 | - | AlertAndLookAtPlayer | 0 | - | 0 | \spawn monster 2653 |
| 2656 | - | accord | Human | 124701 | - | - | 0 | - | 0 | \spawn monster 2656 |
| 2657 | Black Hills Shotgunner | Black Hills Bandits | Outlaw | 125147 | 122621 / - | - | 0 | 5311 / 5387 | 0 | \spawn monster 2657 |
| 2658 | Black Hills Marauder | Black Hills Bandits | Outlaw | 124704 | 124703 / - | - | 0 | 5312 / 5388 | 0 | \spawn monster 2658 |
| 2659 | - | accord | Human | 124705 | - | - | 0 | - | 0 | \spawn monster 2659 |
| 2660 | - | bandit | Outlaw | 124702 | 122621 / - | - | 0 | 5310 / - | 0 | \spawn monster 2660 |
| 2661 | Bandit Hoodlum | bandit | Outlaw | 125147 | 122621 / - | - | 0 | 5311 / 5387 | 0 | \spawn monster 2661 |
| 2662 | Reaper Brigand | Reapers | Outlaw | 125147 | 122621 / - | - | 0 | 5311 / 5387 | 0 | \spawn monster 2662 |
| 2663 | Rebel Grenadier | Rebels | Outlaw | 125146 | 96848 / - | - | 0 | 5312 / 5388 | 5 | \spawn monster 2663 |
| 2664 | Accord Lieutenant | accord | Human | 124702 | 122621 / - | - | 0 | 5310 / - | 0 | \spawn monster 2664 |
| 2665 | Chimera Blaster | bandit | Outlaw | 125147 | 122621 / - | - | 0 | 5311 / 5387 | 0 | \spawn monster 2665 |
| 2666 | Poacher Blaster | bandit | Outlaw | 125147 | 122621 / - | - | 0 | 5311 / 5387 | 0 | \spawn monster 2666 |
| 2667 | - | bandit | Outlaw | 124702 | 122621 / - | - | 0 | 5310 / - | 0 | \spawn monster 2667 |
| 2668 | Chosen Ultratrooper | chosen | Chosen | 124704 | 124703 / - | - | 0 | 5699 / 5370 | 5 | \spawn monster 2668 |
| 2669 | Chosen Assassin | chosen | Chosen | 124709 | 124711 / - | - | 0 | 5698 / 5369 | 5 | \spawn monster 2669 |
| 2670 | - | accord | Human | 124684 | - | - | 0 | - | 0 | \spawn monster 2670 |
| 2671 | - | accord | Human | 124685 | - | - | 0 | - | 0 | \spawn monster 2671 |
| 2672 | Thump Dump Personnel with Scanner | accord | Human | 117022 | - | BasicCivilian_Stationary | 0 | - | 0 | \spawn monster 2672 |
| 2673 | - | accord | Human | 124717 | - | - | 0 | - | 0 | \spawn monster 2673 |
| 2674 | - | accord | Human | 124720 | - | - | 0 | - | 0 | \spawn monster 2674 |
| 2675 | - | accord | Human | 124721 | - | - | 0 | - | 0 | \spawn monster 2675 |
| 2676 | - | accord | Human | 124722 | - | - | 0 | - | 0 | \spawn monster 2676 |
| 2677 | Broken Shores Technician | accord | Human | 124690 | - | BasicCivilian_Stationary | 0 | - | 0 | \spawn monster 2677 |
| 2678 | - | accord | Human | 124731 | - | BasicCivilian_Stationary | 0 | - | 0 | \spawn monster 2678 |
| 2680 | - | accord | Human | 117025 | - | AlertAndLookAtPlayer | 0 | - | 0 | \spawn monster 2680 |
| 2681 | - | accord | Human | 117027 | - | AlertAndLookAtPlayer | 0 | - | 0 | \spawn monster 2681 |
| 2682 | - | accord | Human | 117028 | - | AlertAndLookAtPlayer | 0 | - | 0 | \spawn monster 2682 |
| 2683 | - | accord | Human | 117029 | - | AlertAndLookAtPlayer | 0 | - | 0 | \spawn monster 2683 |
| 2684 | - | accord | Human | 117026 | - | AlertAndLookAtPlayer | 0 | - | 0 | \spawn monster 2684 |
| 2685 | - | accord | Human | 117024 | - | AlertAndLookAtPlayer | 0 | - | 0 | \spawn monster 2685 |
| 2686 | - | accord | Human | 117025 | - | AlertAndLookAtPlayer | 0 | - | 0 | \spawn monster 2686 |
| 2687 | - | accord | Human | 117027 | - | AlertAndLookAtPlayer | 0 | - | 0 | \spawn monster 2687 |
| 2688 | - | accord | Human | 117028 | - | AlertAndLookAtPlayer | 0 | - | 0 | \spawn monster 2688 |
| 2689 | - | accord | Human | 117029 | - | AlertAndLookAtPlayer | 0 | - | 0 | \spawn monster 2689 |
| 2690 | - | accord | Human | 117026 | - | AlertAndLookAtPlayer | 0 | - | 0 | \spawn monster 2690 |
| 2691 | - | accord | Human | 124751 | - | - | 0 | - | 0 | \spawn monster 2691 |
| 2692 | Copacabana Accord Soldier - BasicCivilian_Stationary | accord | Human | 117034 | 84915 / - | BasicCivilian_Stationary | 0 | - | 0 | \spawn monster 2692 |
| 2693 | Copacabana Injured Accord Soldier - BasicCivilian_Stationary | accord | Human | 117010 | - | BasicCivilian_Stationary | 0 | - | 0 | \spawn monster 2693 |
| 2694 | Copacabana Civilian - BasicCivilian_Stationary | accord | Human | 117015 | - | BasicCivilian_Stationary | 0 | - | 0 | \spawn monster 2694 |
| 2695 | Copacabana Swimsuits - BasicCivilian_Stationary | accord | Human | 124340 | - | BasicCivilian_Stationary | 0 | - | 0 | \spawn monster 2695 |
| 2696 | Copacabana Accord Soldier - Ambient - No gun - BasicCivilian_Stationary | accord | Human | 117024 | - | BasicCivilian_Stationary | 0 | - | 0 | \spawn monster 2696 |
| 2697 | Rasper Shielder | gaea | Wildlife | 139935 | - | - | 10003 | 5704 / - | 5 | \spawn monster 2697 |
| 2698 | - | accord | Human | 124751 | - | Arch_MedRangedAmbient_Base | 0 | - | 0 | \spawn monster 2698 |
| 2699 | - | accord | Human | 124771 | 86851 / 76108 | - | 0 | - | 0 | \spawn monster 2699 |
| 2700 | Reaper Corsair | Reapers | Outlaw | 124704 | 124703 / - | - | 0 | 5312 / 5388 | 0 | \spawn monster 2700 |
| 2701 | - | accord | Human | 124771 | 86126 / 76108 | - | 0 | - | 0 | \spawn monster 2701 |
| 2702 | - | Reapers | Outlaw | 124780 | - | - | 0 | 3 / 5387 | 5 | \spawn monster 2702 |
| 2703 | Reaper Grenadier | Reapers | Outlaw | 125146 | 96848 / - | - | 0 | 5312 / 5388 | 5 | \spawn monster 2703 |
| 2704 | - | accord | Human | 75683 | 30025 / 20015 | PlayerPet | 0 | - | 5 | \spawn monster 2704 |
| 2705 | - | accord | Human | 124791 | - | - | 0 | - | 0 | \spawn monster 2705 |
| 2707 | - | accord | Human | 124792 | - | - | 0 | - | 0 | \spawn monster 2707 |
| 2708 | - | accord | Human | 124794 | - | - | 0 | - | 0 | \spawn monster 2708 |
| 2709 | - | accord | Human | 124793 | - | - | 0 | - | 0 | \spawn monster 2709 |
| 2710 | - | accord | Human | 117013 | - | AlertAndInteractive | 0 | - | 10 | \spawn monster 2710 |
| 2711 | Centauri Mite | gaea | Wildlife | 124819 | - | - | 0 | 5702 / - | 5 | \spawn monster 2711 |
| 2712 | Ranok | chosen | Chosen | 124831 | 140688 / - | - | 0 | 5701 / 5372 | 5 | \spawn monster 2712 |
| 2713 | Chimera Rocketeer | bandit | Outlaw | 124704 | 124703 / - | - | 0 | 5312 / 5388 | 0 | \spawn monster 2713 |
| 2714 | - | accord | Human | 124863 | - | - | 0 | - | 0 | \spawn monster 2714 |
| 2715 | - | accord | Human | 75683 | 124599 / 20015 | PlayerPet | 0 | - | 5 | \spawn monster 2715 |
| 2716 | - | accord | Human | 124925 | - | - | 0 | - | 0 | \spawn monster 2716 |
| 2717 | - | accord | Human | 124926 | - | - | 0 | - | 0 | \spawn monster 2717 |
| 2718 | - | accord | Human | 124927 | - | - | 0 | - | 0 | \spawn monster 2718 |
| 2719 | - | accord | Human | 124928 | - | - | 0 | - | 0 | \spawn monster 2719 |
| 2720 | - | accord | Human | 124930 | - | - | 0 | - | 0 | \spawn monster 2720 |
| 2721 | - | accord | Human | 124929 | - | - | 0 | - | 0 | \spawn monster 2721 |
| 2722 | - | accord | Human | 125038 | - | UseAbilityOnInteract_Dialog | 0 | - | 0 | \spawn monster 2722 |
| 2723 | Distraught Wife | accord | Human | 125038 | - | UseAbilityOnInteract | 0 | - | 0 | \spawn monster 2723 |
| 2724 | Injured Husband | accord | Human | 125150 | - | AlertAndInteractive | 0 | - | 0 | \spawn monster 2724 |
| 2725 | Parrot Bomber | accord | Outlaw | 125191 | - | - | 0 | 5702 / 5385 | 5 | \spawn monster 2725 |
| 2726 | - | accord | Human | 117016 | - | AlertAndInteractive | 0 | - | 5 | \spawn monster 2726 |
| 2727 | - | accord | Human | 117011 | - | AlertAndInteractive | 0 | - | 5 | \spawn monster 2727 |
| 2728 | Serkan | chosen | Chosen | 125251 | - | - | 0 | - | 5 | \spawn monster 2728 |
| 2729 | Mendraxus | chosen | Chosen | 125252 | - | - | 0 | - | 5 | \spawn monster 2729 |
| 2730 | SIN Hack Dealer | accord | Human | 125033 | - | AlertAndInteractive | 0 | - | 0 | \spawn monster 2730 |
| 2731 | - | accord | Human | 97602 | - | AlertAndInteractive | 0 | - | 5 | \spawn monster 2731 |
| 2732 | Overseer Slugg | chosen | Chosen | 136304 | 137004 / 137014 | - | 0 | 5701 / 5372 | 5 | \spawn monster 2732 |
| 2733 | Corporal Garland | accord | Human | 125038 | - / 30025 | UseAbilityOnInteract | 0 | - | 0 | \spawn monster 2733 |
| 2734 | Claudia Fonseca | accord | Human | 125038 | - | UseAbilityOnInteract | 0 | - | 0 | \spawn monster 2734 |
| 2735 | Astrek Corporate Representative | accord | Human | 118001 | - | AlertAndInteractive | 0 | - | 0 | \spawn monster 2735 |
| 2736 | Omnidyne-M Corporate Representative | accord | Human | 118001 | - | AlertAndInteractive | 0 | - | 0 | \spawn monster 2736 |
| 2737 | - | chosen | Chosen | 124831 | 124703 / - | - | 0 | 5698 / 5369 | 5 | \spawn monster 2737 |
| 2738 | Grizli | accord | Human | 125038 | - | UseAbilityOnInteract | 0 | - | 0 | \spawn monster 2738 |
| 2739 | Accord Engineer | accord | Human | 117611 | 76108 / 76108 | AlertAndLookAtPlayer | 0 | - | 0 | \spawn monster 2739 |
| 2740 | Reaper | Reapers | Outlaw | 125065 | 96842 / - | - | 0 | 5310 / 5386 | 0 | \spawn monster 2740 |
| 2741 | Civilian | accord | Human | 118010 | - | AlertAndLookAtPlayer | 0 | - | 0 | \spawn monster 2741 |
| 2742 | Omnidyne Representative | accord | Human | 106343 | - | AlertAndInteractive | 0 | - | 0 | \spawn monster 2742 |
| 2743 | - | accord | Human | 118411 | - | PeacetimeCityWanderer | 0 | - | 0 | \spawn monster 2743 |
| 2744 | - | accord | Human | 118411 | - | PeacetimeCityWanderer | 0 | - | 0 | \spawn monster 2744 |
| 2745 | Curtis the Fisherman | accord | Human | 118010 | - | AlertAndLookAtPlayer | 0 | - | 0 | \spawn monster 2745 |
| 2746 | Derkas | accord | Human | 92801 | 78065 / - | AlertAndLookAtPlayer | 0 | - | 0 | \spawn monster 2746 |
| 2747 | - | chosen | Chosen | 136391 | 122655 / 122655 | - | 0 | - | 0 | \spawn monster 2747 |
| 2748 | Ranok | chosen | Chosen | 130438 | - | - | 0 | - | 5 | \spawn monster 2748 |
| 2749 | Fulton Kwok | accord | Human | 125038 | - | UseAbilityOnInteract | 0 | - | 0 | \spawn monster 2749 |
| 2750 | Salvador | accord | Human | 125038 | - | UseAbilityOnInteract_Dialog | 0 | - | 0 | \spawn monster 2750 |
| 2751 | Duncan | accord | Human | 125038 | - | UseAbilityOnInteract | 0 | - | 0 | \spawn monster 2751 |
| 2752 | Capt. Hudson Fuller | accord | Human | 133048 | - | - | 0 | - | 10 | \spawn monster 2752 |
| 2753 | Mason Chen | accord | Human | 133049 | 124798 / 139886 | - | 0 | - | 10 | \spawn monster 2753 |
| 2754 | Soldier | accord | Human | 133050 | 97695 / 76108 | - | 0 | - | 0 | \spawn monster 2754 |
| 2755 | Accord Coroner | accord | Human | 81372 | - | AlertAndLookAtPlayer | 0 | - | 0 | \spawn monster 2755 |
| 2756 | Sergeant Wilcox | accord | Human | 125038 | - | UseAbilityOnInteract | 0 | - | 0 | \spawn monster 2756 |
| 2757 | Sarah | accord | Human | 81378 | - | AlertAndInteractive | 0 | - | 0 | \spawn monster 2757 |
| 2758 | Science OFC Nakamura | accord | Human | 125038 | - | UseAbilityOnInteract | 0 | - | 0 | \spawn monster 2758 |
| 2759 | Omnidyne-M Rep | accord | Human | 125038 | - | UseAbilityOnInteract | 0 | - | 10 | \spawn monster 2759 |
| 2760 | Captain Leah Tallon | accord | Human | 134401 | - | Pet_Earthbreaker | 0 | - | 0 | \spawn monster 2760 |
| 2761 | Petty Officer Third Class Mike P. Patton | accord | Human | 134405 | - | Pet_Earthbreaker | 0 | - | 0 | \spawn monster 2761 |
| 2762 | Reaper SIN Hack Dealer | Reapers | Outlaw | 125065 | 96842 / - | - | 0 | 5310 / 5386 | 0 | \spawn monster 2762 |
| 2763 | Rebel Lieutenant | accord | Outlaw | 125034 | 106335 / - | AlertAndInteractive | 0 | 5313 / - | 5 | \spawn monster 2763 |
| 2764 | - | Civilian | Human | 125033 | - | AlertAndInteractive | 0 | - | 0 | \spawn monster 2764 |
| 2765 | - | bandit | Outlaw | 85880 | - | ChainWithPush | 0 | 5311 / 5386 | 0 | \spawn monster 2765 |
| 2766 | - | accord | Human | 97262 | - | AlertAndInteractive | 0 | - | 0 | \spawn monster 2766 |
| 2767 | - | accord | Human | 81391 | 84915 / - | GuardCityWanderer | 0 | - | 0 | \spawn monster 2767 |
| 2768 | - | accord | Human | 117034 | 84915 / - | AlertAndInteractive | 0 | - | 0 | \spawn monster 2768 |
| 2769 | - | accord | Human | 117033 | 84915 / - | AlertAndLookAtPlayer | 0 | - | 0 | \spawn monster 2769 |
| 2770 | Rebel Lieutenant | accord | Outlaw | 125087 | 96871 / - | AlertAndInteractive | 0 | - | 5 | \spawn monster 2770 |
| 2771 | Chosen Infantry | chosen | Chosen | 85952 | 85953 / - | - | 0 | - | 5 | \spawn monster 2771 |
| 2772 | - | chosen | Chosen | 125069 | 32739 / - | - | 0 | - | 5 | \spawn monster 2772 |
| 2773 | Luiz Belo | accord | Human | 125038 | - | UseAbilityOnInteract | 0 | - | 0 | \spawn monster 2773 |
| 2774 | - | chosen | Chosen | 136941 | 32739 / - | - | 0 | 5698 / 5369 | 5 | \spawn monster 2774 |
| 2775 | Bandit Bully | bandit | Outlaw | 124704 | 124703 / - | - | 0 | 5312 / 5388 | 0 | \spawn monster 2775 |
| 2776 | Scarlet | accord | Human | 117619 | - | AlertAndInteractive | 0 | - | 0 | \spawn monster 2776 |
| 2777 | - | accord | Human | 10002 | - | - | 10003 | - | 0 | \spawn monster 2777 |
| 2778 | Rebel Resister | Rebels | Outlaw | 124704 | 124703 / - | - | 0 | 5312 / 5388 | 5 | \spawn monster 2778 |
| 2779 | Accord Heavy Marine | accord | Human | 124704 | 124703 / - | - | 0 | 5312 / - | 0 | \spawn monster 2779 |
| 2780 | Ophanim Commando | Ophanim | Outlaw | 124704 | 124703 / - | - | 0 | 3 / 5388 | 5 | \spawn monster 2780 |
| 2781 | Poacher Pro Hunter | bandit | Outlaw | 124704 | 124703 / - | - | 0 | 5312 / 5388 | 5 | \spawn monster 2781 |
| 2782 | June Harper | accord | Human | 52458 | - | AlertAndInteractive | 0 | - | 0 | \spawn monster 2782 |
| 2783 | - | gaea | Wildlife | 136997 | 136992 / - | - | 0 | 5704 / 5414 | 5 | \spawn monster 2783 |
| 2784 | Barfly | accord | Human | 118411 | - | AlertAndInteractive | 0 | - | 0 | \spawn monster 2784 |
| 2785 | Lt. Cmdr. Kara Novan | accord | Human | 137008 | - | - | 0 | - | 10 | \spawn monster 2785 |
| 2786 | Bartender | accord | Human | 33981 | - | AlertAndInteractive | 0 | - | 0 | \spawn monster 2786 |
| 2787 | Lieutenant Prince | accord | Human | 117031 | 84915 / - | AlertAndInteractive | 0 | - | 0 | \spawn monster 2787 |
| 2788 | Omnidyne-M Rep | accord | Human | 125038 | - | UseAbilityOnInteract | 0 | - | 10 | \spawn monster 2788 |
| 2789 | Progress | accord | Human | 125038 | - | AlertAndInteractive | 0 | - | 10 | \spawn monster 2789 |
| 2790 | Progress | accord | Human | 123210 | - | AlertAndInteractive | 0 | - | 0 | \spawn monster 2790 |
| 2791 | Tarantus | chosen | Chosen | 137024 | - | - | 0 | - | 5 | \spawn monster 2791 |
| 2792 | The Indexer | accord | Human | 125038 | - | UseAbilityOnInteract | 0 | - | 0 | \spawn monster 2792 |
| 2793 | Sergeant Cortese | accord | Human | 124276 | 96847 / - | AlertAndInteractive | 0 | - | 0 | \spawn monster 2793 |
| 2794 | Commander Finch | accord | Human | 117032 | 87788 / - | AlertAndInteractive | 0 | - | 0 | \spawn monster 2794 |
| 2795 | Lt. Maria Garcia | accord | Human | 125038 | 84916 / - | UseAbilityOnInteract | 0 | - | 0 | \spawn monster 2795 |
| 2796 | LeRoy | accord | Human | 138331 | - | AlertAndInteractive | 0 | - | 0 | \spawn monster 2796 |
| 2797 | - | gaea | Wildlife | 137027 | 137028 / - | - | 0 | 5704 / 5414 | 5 | \spawn monster 2797 |
| 2798 | - | gaea | Wildlife | 137029 | - | - | 0 | 5704 / 5414 | 5 | \spawn monster 2798 |
| 2799 | Scarlet | accord | Human | 117619 | - | AlertAndLookAtPlayer | 0 | - | 0 | \spawn monster 2799 |
| 2800 | Shady Civilian | bandit | Outlaw | 75281 | 67425 / - | AlertAndLookAtPlayer | 0 | 5311 / 5386 | 5 | \spawn monster 2800 |
| 2801 | - | Reapers | Outlaw | 124782 | 122753 / - | - | 0 | 5312 / 5388 | 5 | \spawn monster 2801 |
| 2802 | Copacabana Swimsuit - Ambient Pathing | accord | Human | 137047 | - | Arch_MedRangedHumanoid_Base | 0 | - | 0 | \spawn monster 2802 |
| 2803 | Copacabana Swimsuit - Ambient Pathing - Slow | accord | Human | 137048 | - | Arch_MedRangedHumanoid_Base | 0 | - | 0 | \spawn monster 2803 |
| 2804 | Astrek Agent | accord | Human | 52461 | - | AlertAndLookAtPlayer | 0 | - | 0 | \spawn monster 2804 |
| 2805 | Agrievan | chosen | Chosen | 121313 | - | - | 0 | - | 5 | \spawn monster 2805 |
| 2806 | Dr. Bathsheba | accord | Human | 121514 | - | - | 0 | - | 0 | \spawn monster 2806 |
| 2807 | Nostromo | accord | Human | 139390 | - | - | 0 | - | 0 | \spawn monster 2807 |
| 2808 | Overseer | chosen | Chosen | 136304 | - | - | 0 | - | 5 | \spawn monster 2808 |
| 2809 | Serkan | accord | Human | 137084 | - | - | 0 | - | 0 | \spawn monster 2809 |
| 2810 | Mason Chen | accord | Human | 137085 | - | Stand | 0 | - | 10 | \spawn monster 2810 |
| 2811 | Artis the Mechanic | accord | Human | 125038 | - | UseAbilityOnInteract | 0 | - | 0 | \spawn monster 2811 |
| 2812 | Arturs the Mechanic | accord | Human | 125038 | - | UseAbilityOnInteract | 0 | - | 0 | \spawn monster 2812 |
| 2813 | Mason Chen | accord | Human | 137084 | - | - | 0 | - | 0 | \spawn monster 2813 |
| 2814 | Steve Coelho | accord | Human | 118039 | 79272 / - | AlertAndLookAtPlayer | 0 | - | 0 | \spawn monster 2814 |
| 2815 | Copacabana Civilian - Ambient Pathing | accord | Human | 137117 | - | Arch_MedRangedHumanoid_Base | 0 | - | 0 | \spawn monster 2815 |
| 2816 | ARES Assault - Ambient Pathing | accord | Human | 117031 | 143971 / - | Arch_MedRangedHumanoid_Base | 0 | - | 0 | \spawn monster 2816 |
| 2817 | ARES Engineer - Ambient Pathing | accord | Human | 120619 | - | Arch_MedRangedHumanoid_Base | 0 | - | 0 | \spawn monster 2817 |
| 2818 | ARES Recon - Ambient Pathing | accord | Human | 117034 | 143971 / - | Arch_MedRangedHumanoid_Base | 0 | - | 0 | \spawn monster 2818 |
| 2819 | ARES Dreadnaught - Ambient Pathing | accord | Human | 117032 | 143971 / - | Arch_MedRangedHumanoid_Base | 0 | - | 0 | \spawn monster 2819 |
| 2820 | ARES Biotech - Ambient Pathing | accord | Human | 117033 | - | Arch_MedRangedHumanoid_Base | 0 | - | 0 | \spawn monster 2820 |
| 2821 | Civilian Mechanic - Ambient Pathing | accord | Human | 137121 | - | Arch_MedRangedHumanoid_Base | 0 | - | 0 | \spawn monster 2821 |
| 2822 | Bernardo Coelho | accord | Human | 96486 | 79272 / - | ApplyStatusEffect | 0 | - | 0 | \spawn monster 2822 |
| 2823 | Ricardo Coelho | accord | Human | 118039 | 78063 / - | ApplyStatusEffect | 0 | - | 0 | \spawn monster 2823 |
| 2824 | Eduardo Coelho | accord | Human | 96486 | 78063 / - | ApplyStatusEffect | 0 | - | 0 | \spawn monster 2824 |
| 2825 | Civilian Scientist - Ambient Pathing | accord | Human | 137123 | - | Arch_MedRangedHumanoid_Base | 0 | - | 0 | \spawn monster 2825 |
| 2826 | Remigio Coelho | accord | Human | 125038 | 78403 / - | UseAbilityOnInteract | 0 | - | 0 | \spawn monster 2826 |
| 2827 | Security Chief Delgado | accord | Human | 125038 | 84916 / - | UseAbilityOnInteract | 0 | - | 0 | \spawn monster 2827 |
| 2828 | Doctor Abrams | accord | Human | 125038 | - | UseAbilityOnInteract | 0 | - | 0 | \spawn monster 2828 |
| 2829 | Bernardo Coelho | accord | Human | 96486 | 79272 / - | AlertAndLookAtPlayer | 0 | - | 0 | \spawn monster 2829 |
| 2830 | Ricardo Coelho | accord | Human | 118039 | 78063 / - | AlertAndLookAtPlayer | 0 | - | 0 | \spawn monster 2830 |
| 2831 | Eduardo Coelho | accord | Human | 96486 | - | AlertAndLookAtPlayer | 0 | - | 0 | \spawn monster 2831 |
| 2832 | Accord Soldier - Turret Defender | accord | Human | 117034 | 137167 / - | Arch_MedRangedRifleman_Base | 0 | - | 0 | \spawn monster 2832 |
| 2833 | Sergeant Choi | accord | Human | 125038 | - | UseAbilityOnInteract | 0 | - | 0 | \spawn monster 2833 |
| 2834 | Civilian Nutretic Worker - Ambient Pathing | accord | Human | 137121 | - | Arch_MedRangedHumanoid_Base | 0 | - | 0 | \spawn monster 2834 |
| 2835 | Captain Abrams | accord | Outlaw | 125038 | - | UseAbilityOnInteract | 0 | - | 0 | \spawn monster 2835 |
| 2836 | Agrievan | chosen | Chosen | 121313 | - | - | 0 | - | 5 | \spawn monster 2836 |
| 2837 | Panther of Hope | accord | Human | 137243 | - | Pet_Earthbreaker | 0 | - | 0 | \spawn monster 2837 |
| 2838 | Monstrous King Skiver | gaea | Wildlife | 137244 | 122723 / - | - | 10005 | 5706 / - | 5 | \spawn monster 2838 |
| 2839 | Accord Soldier | accord | Human | 10003 | - / 96847 | AlertAndInteractive | 0 | - | 0 | \spawn monster 2839 |
| 2840 | Melding Ultra Culex | melding | Melded | 138296 | 138300 / - | - | 0 | 5704 / 5459 | 5 | \spawn monster 2840 |
| 2841 | Rebel Lieutenant | Rebels | Outlaw | 142074 | 122753 / - | - | 0 | 5313 / 5389 | 5 | \spawn monster 2841 |
| 2842 | Meat Shield | accord | Human | 138333 | 137062 / - | Pet_Earthbreaker | 0 | - | 0 | \spawn monster 2842 |
| 2843 | Injured Driver | friendly | Companion | 93709 | - | - | 0 | - | 0 | \spawn monster 2843 |
| 2844 | Thump Dump Civilian - Ambient Pathing | accord | Human | 137117 | - | Arch_MedRangedHumanoid_Base | 0 | - | 0 | \spawn monster 2844 |
| 2845 | Caldon | accord | Human | 125038 | - | UseAbilityOnInteract | 0 | - | 0 | \spawn monster 2845 |
| 2846 | Coruja | accord | Human | 125038 | - | UseAbilityOnInteract_Dialog | 0 | - | 0 | \spawn monster 2846 |
| 2847 | Adrita | accord | Human | 125038 | - | UseAbilityOnInteract_Dialog | 0 | - | 0 | \spawn monster 2847 |
| 2848 | Mission013 Stabilizer | accord | Human | 137098 | - | - | 0 | - | 0 | \spawn monster 2848 |
| 2849 | Grieving Husband | accord | Human | 52462 | - | AlertAndInteractive | 0 | - | 0 | \spawn monster 2849 |
| 2850 | Sydney | accord | Human | 125038 | - | UseAbilityOnInteract_Dialog | 0 | - | 0 | \spawn monster 2850 |
| 2851 | Lieutenant Song | accord | Human | 117031 | 84915 / - | AlertAndInteractive | 0 | - | 0 | \spawn monster 2851 |
| 2852 | Major Desselhoff | accord | Human | 95469 | - / 76108 | AlertAndLookAtPlayer | 0 | - | 0 | \spawn monster 2852 |
| 2853 | Wiley | accord | Human | 118000 | - / 78063 | AlertAndLookAtPlayer | 0 | - | 0 | \spawn monster 2853 |
| 2854 | IceClaw | gaea | Wildlife | 138373 | 138375 / - | - | 0 | 5706 / 5425 | 5 | \spawn monster 2854 |
| 2855 | Dr. Bathsheba Husk | accord | Human | 138427 | - | - | 0 | - | 0 | \spawn monster 2855 |
| 2856 | Ensign Julian Basurto | accord | Human | 117023 | - | AlertAndInteractive | 0 | - | 10 | \spawn monster 2856 |
| 2857 | Sullivan Wade | accord | Human | 117014 | - | AlertAndInteractive | 0 | - | 0 | \spawn monster 2857 |
| 2858 | Old Woman Andrita | accord | Human | 117017 | - | AlertAndInteractive | 0 | - | 0 | \spawn monster 2858 |
| 2859 | Mason Chen | accord | Human | 133049 | 124798 / 125238 | - | 0 | - | 10 | \spawn monster 2859 |
| 2860 | Chet Linwood | accord | Human | 117010 | - | AlertAndInteractive | 0 | - | 0 | \spawn monster 2860 |
| 2861 | Harry Kingsley | accord | Human | 117017 | - | AlertAndInteractive | 0 | - | 0 | \spawn monster 2861 |
| 2862 | Capt. Hudson Fuller | accord | Human | 138437 | - | - | 0 | - | 10 | \spawn monster 2862 |
| 2863 | - | accord | Human | 138444 | - | - | 0 | - | 0 | \spawn monster 2863 |
| 2864 | - | accord | Human | 138444 | - | - | 0 | - | 0 | \spawn monster 2864 |
| 2865 | Shanty Town Civilian - Ambient Pathing | accord | Human | 137117 | - | Arch_MedRangedHumanoid_Base | 0 | - | 0 | \spawn monster 2865 |
| 2866 | Cultist | bandit | Outlaw | 125065 | 96842 / - | - | 0 | 5310 / 5386 | 0 | \spawn monster 2866 |
| 2867 | Overseer Slugg | chosen | Chosen | 138533 | - | Done | 0 | - | 5 | \spawn monster 2867 |
| 2868 | Accord Officer - Ambient Pathing | accord | Human | 138691 | - | Arch_MedRangedHumanoid_Base | 0 | - | 0 | \spawn monster 2868 |
| 2869 | Wiley | accord | Human | 118000 | - / 78063 | AlertAndInteractive | 0 | - | 0 | \spawn monster 2869 |
| 2870 | Sunken Harbor Civilian - Ambient Pathing | accord | Human | 137117 | - | Arch_MedRangedHumanoid_Base | 0 | - | 0 | \spawn monster 2870 |
| 2871 | - | bandit | Outlaw | 125147 | 122621 / - | - | 0 | 5310 / 5386 | 0 | \spawn monster 2871 |
| 2872 | - | bandit | Outlaw | 125146 | 96848 / - | - | 0 | 5312 / 5388 | 0 | \spawn monster 2872 |
| 2873 | Skydock Supply Officer | accord | Human | 138728 | - | AlertAndInteractive | 0 | - | 0 | \spawn monster 2873 |
| 2874 | Warrant Officer Liu | accord | Human | 76132 | - | AlertAndInteractive | 0 | - | 0 | \spawn monster 2874 |
| 2875 | Lt. Amanda Bloom | accord | Human | 140092 | - | - | 0 | - | 0 | \spawn monster 2875 |
| 2876 | Corporal Belle | accord | Human | 76334 | - | AlertAndInteractive | 0 | - | 0 | \spawn monster 2876 |
| 2877 | Carlo Fonseca | accord | Human | 125038 | - | UseAbilityOnInteract | 0 | - | 0 | \spawn monster 2877 |
| 2878 | Lieutenant Sands | accord | Human | 125038 | - | UseAbilityOnInteract | 0 | - | 0 | \spawn monster 2878 |
| 2879 | Capt. Patel | accord | Human | 125038 | 96847 / - | UseAbilityOnInteract | 0 | - | 0 | \spawn monster 2879 |
| 2880 | Sergeant Lewis | accord | Human | 125038 | - | UseAbilityOnInteract | 0 | - | 0 | \spawn monster 2880 |
| 2881 | Trans Hub Officer | accord | Human | 117023 | - | BasicCivilian_Stationary | 0 | - | 0 | \spawn monster 2881 |
| 2882 | TransHub Accord Soldier - Ambient - No gun - BasicCivilian_Stationary | accord | Human | 117024 | - | BasicCivilian_Stationary | 0 | - | 0 | \spawn monster 2882 |
| 2883 | Trans Hub Worker - Ambient Barker | accord | Human | 117015 | - | BasicCivilian_Stationary | 0 | - | 0 | \spawn monster 2883 |
| 2884 | - | - | Human | 0 | - | - | 0 | - | 0 | \spawn monster 2884 |
| 2885 | - | - | Human | 0 | - | - | 0 | - | 0 | \spawn monster 2885 |
| 2886 | - | - | Human | 0 | - | - | 0 | - | 0 | \spawn monster 2886 |
| 2887 | - | - | Human | 0 | - | - | 0 | - | 0 | \spawn monster 2887 |
| 2888 | - | - | Human | 0 | - | - | 0 | - | 0 | \spawn monster 2888 |
| 2889 | Sunken Harbor Civilian - BasicCivilian_Stationary | accord | Human | 117015 | - | BasicCivilian_Stationary | 0 | - | 0 | \spawn monster 2889 |
| 2890 | Sunken Harbor Resident - BasicCivilian_Stationary | accord | Human | 117015 | - | BasicCivilian_Stationary | 0 | - | 0 | \spawn monster 2890 |
| 2891 | - | accord | Human | 113542 | - / 86520 | Arch_MedRangedHumanoid_Base | 0 | - | 0 | \spawn monster 2891 |
| 2892 | - | accord | Human | 76132 | 122968 / - | - | 0 | 5312 / - | 0 | \spawn monster 2892 |
| 2893 | - | accord | Human | 76133 | 134501 / - | - | 0 | 5312 / - | 0 | \spawn monster 2893 |
| 2894 | Serkan | chosen | Chosen | 138812 | - | - | 0 | - | 5 | \spawn monster 2894 |
| 2895 | Culex Matron | gaea | Wildlife | 138814 | 137035 / - | - | 0 | 5705 / 5415 | 5 | \spawn monster 2895 |
| 2896 | Tortured Soul | melding | Melded | 138815 | 122871 / - | - | 10005 | 5727 / 5461 | 5 | \spawn monster 2896 |
| 2899 | - | accord | Human | 76133 | - | - | 0 | 5312 / - | 0 | \spawn monster 2899 |
| 2900 | - | accord | Human | 76132 | - | - | 0 | 5312 / - | 0 | \spawn monster 2900 |
| 2901 | Robotic Bunny | friendly | Companion | 139294 | - | - | 0 | - | 0 | \spawn monster 2901 |
| 2902 | - | chosen | Chosen | 139340 | 121018 / - | - | 0 | - | 5 | \spawn monster 2902 |
| 2903 | Mason Chen | accord | Human | 139341 | - | - | 0 | - | 10 | \spawn monster 2903 |
| 2904 | Agrievan | chosen | Chosen | 121313 | - | - | 0 | - | 5 | \spawn monster 2904 |
| 2905 | - | chosen | Chosen | 120582 | 92799 / 33064 | EngineerTurretTeleporter | 0 | - | 0 | \spawn monster 2905 |
| 2906 | - | accord | Human | 122969 | 106335 / - | - | 0 | 5313 / - | 0 | \spawn monster 2906 |
| 2907 | - | accord | Human | 114634 | - / 76108 | GuardCityWanderer | 0 | - | 0 | \spawn monster 2907 |
| 2908 | - | accord | Human | 114634 | - / 76108 | GuardCityWanderer | 0 | - | 0 | \spawn monster 2908 |
| 2909 | - | accord | Human | 76331 | - / 76108 | GuardCityWanderer | 0 | - | 0 | \spawn monster 2909 |
| 2910 | - | accord | Human | 76331 | - / 76108 | GuardCityWanderer | 0 | - | 0 | \spawn monster 2910 |
| 2911 | Necronus | chosen | Misc | 30436 | 30647 / - | - | 0 | - | 0 | \spawn monster 2911 |
| 2912 | - | accord | Human | 10002 | 85972 / - | Arch_MedRangedHumanoid_Base | 0 | - | 0 | \spawn monster 2912 |
| 2913 | - | accord | Human | 136929 | 85972 / - | Arch_MedRangedHumanoid_Base | 0 | - | 0 | \spawn monster 2913 |
| 2914 | - | accord | Human | 76336 | 85972 / - | Arch_MedRangedHumanoid_Base | 0 | - | 0 | \spawn monster 2914 |
| 2915 | - | accord | Human | 136976 | 116408 / - | Arch_MedRangedHumanoid_Base | 0 | - | 0 | \spawn monster 2915 |
| 2916 | - | accord | Human | 136976 | 116408 / - | Arch_MedRangedHumanoid_Base | 0 | - | 0 | \spawn monster 2916 |
| 2917 | - | accord | Human | 10004 | - / 76108 | Arch_MedRangedHumanoid_Base | 0 | - | 0 | \spawn monster 2917 |
| 2918 | - | accord | Human | 136305 | - / 76108 | Arch_MedRangedHumanoid_Base | 0 | - | 0 | \spawn monster 2918 |
| 2919 | - | accord | Human | 136303 | - / 76108 | Arch_MedRangedHumanoid_Base | 0 | - | 0 | \spawn monster 2919 |
| 2920 | - | accord | Human | 136303 | - / 76108 | Arch_MedRangedHumanoid_Base | 0 | - | 0 | \spawn monster 2920 |
| 2921 | - | accord | Human | 136305 | - / 76108 | Arch_MedRangedHumanoid_Base | 0 | - | 0 | \spawn monster 2921 |
| 2922 | - | accord | Human | 139669 | 131845 / 76108 | Arch_MedRangedHumanoid_Base | 0 | - | 0 | \spawn monster 2922 |
| 2923 | - | accord | Human | 136323 | 131847 / 76108 | Arch_MedRangedHumanoid_Base | 0 | - | 0 | \spawn monster 2923 |
| 2924 | - | accord | Human | 136308 | 116420 / 76108 | Arch_MedRangedHumanoid_Base | 0 | - | 0 | \spawn monster 2924 |
| 2925 | - | accord | Human | 136308 | 116420 / 76108 | Arch_MedRangedHumanoid_Base | 0 | - | 0 | \spawn monster 2925 |
| 2926 | - | accord | Human | 136323 | 131847 / 76108 | Arch_MedRangedHumanoid_Base | 0 | - | 0 | \spawn monster 2926 |
| 2927 | Warden Raul Moreno | Rebels | Outlaw | 139623 | 139879 / - | - | 0 | 3 / 5388 | 5 | \spawn monster 2927 |
| 2928 | - | Rebels | Outlaw | 139624 | 76108 / - | - | 0 | 3 / 5388 | 5 | \spawn monster 2928 |
| 2929 | - | accord | Human | 76133 | - | - | 0 | 5312 / - | 0 | \spawn monster 2929 |
| 2930 | Dr. Bathsheba | accord | Human | 121514 | - | - | 0 | - | 0 | \spawn monster 2930 |
| 2931 | Double 11 Bot | friendly | Large wildlife | 139671 | - | - | 0 | - | 0 | \spawn monster 2931 |
| 2932 | Ed the Zombie | friendly | Large wildlife | 139793 | - | - | 0 | - | 0 | \spawn monster 2932 |
| 2933 | Prisoner | accord | Human | 140094 | 96847 / - | Arch_MedRangedHumanoid_Base | 0 | - | 0 | \spawn monster 2933 |
| 2934 | Prisoner | accord | Human | 140094 | 96847 / - | Arch_MedRangedRifleman_Base | 0 | - | 0 | \spawn monster 2934 |
| 2935 | Arsenal, Female | accord | Human | 139838 | - | Pet_Earthbreaker | 0 | - | 0 | \spawn monster 2935 |
| 2936 | Arsenal, Male | accord | Human | 139838 | - | Pet_Earthbreaker | 0 | - | 0 | \spawn monster 2936 |
| 2937 | Accord Dropship | accord | Human | 139812 | - | - | 0 | - | 0 | \spawn monster 2937 |
| 2938 | - | accord | Human | 139722 | 122900 / - | - | 0 | - | 10 | \spawn monster 2938 |
| 2939 | Ratman | - | Human | 139988 | - | PeacetimeCityWanderer | 0 | - | 0 | \spawn monster 2939 |
| 2940 | Pet Ankylot | accord | Companion | 139851 | 96832 / - | - | 0 | - | 5 | \spawn monster 2940 |
| 2942 | Pet Harpy | gaea | Companion | 139849 | - | - | 0 | - | 5 | \spawn monster 2942 |
| 2944 | - | gaea | Companion | 82583 | 82585 / - | - | 0 | 5313 / 6214 | 5 | \spawn monster 2944 |
| 2945 | - | gaea | Wildlife | 139829 | 96917 / - | - | 0 | 5704 / - | 5 | \spawn monster 2945 |
| 2946 | Buzzard Ultratrooper | bandit | Outlaw | 124704 | 124703 / - | - | 0 | 5312 / 5388 | 0 | \spawn monster 2946 |
| 2947 | - | accord | Human | 139831 | - | - | 0 | - | 0 | \spawn monster 2947 |
| 2948 | - | chosen | Chosen | 139831 | - | - | 0 | - | 0 | \spawn monster 2948 |
| 2949 | Battleframe Thief | Rebels | Outlaw | 125069 | 139886 / - | - | 0 | 5310 / 5386 | 0 | \spawn monster 2949 |
| 2950 | Battleframe Thief | Rebels | Outlaw | 125069 | 139888 / - | - | 10005 | 5310 / 5386 | 0 | \spawn monster 2950 |
| 2951 | Battleframe Thief | Rebels | Outlaw | 125034 | 106335 / - | - | 10005 | 5310 / 5386 | 0 | \spawn monster 2951 |
| 2952 | Battleframe Thief | Rebels | Outlaw | 125146 | 139891 / - | - | 10005 | 5310 / 5386 | 0 | \spawn monster 2952 |
| 2953 | Battleframe Thief | Rebels | Outlaw | 125147 | 122543 / - | - | 10005 | 5310 / 5386 | 0 | \spawn monster 2953 |
| 2954 | Rescue Drone | accord | Human | 139843 | - | - | 0 | 5696 / 5615 | 5 | \spawn monster 2954 |
| 2955 | - | friendly | Human | 139853 | - | - | 0 | - | 0 | \spawn monster 2955 |
| 2956 | - | friendly | Large wildlife | 139853 | - | - | 0 | - | 0 | \spawn monster 2956 |
| 2957 | Aliyan | chosen | Chosen | 140810 | - | - | 0 | - | 5 | \spawn monster 2957 |
| 2958 | Melding Tendril | monster | Melded | 139907 | 139905 / - | - | 10005 | 5697 / 5615 | 5 | \spawn monster 2958 |
| 2959 | - | accord | Human | 139908 | - | - | 0 | - | 0 | \spawn monster 2959 |
| 2960 | Lt. Cmdr. Kara Novan | accord | Human | 137008 | 122902 / 125239 | - | 0 | - | 10 | \spawn monster 2960 |
| 2961 | Capt. Hudson Fuller | accord | Human | 133048 | 139891 / - | - | 0 | - | 10 | \spawn monster 2961 |
| 2962 | Skoraith | chosen | Chosen | 142074 | 139939 / - | - | 10005 | 5700 / 5371 | 5 | \spawn monster 2962 |
| 2963 | Ikrus | chosen | Chosen | 142074 | 139946 / - | - | 10005 | 5700 / 5371 | 5 | \spawn monster 2963 |
| 2964 | Albert Sung | accord | Human | 140092 | - / 76108 | Arch_MedRangedHumanoid_Base | 0 | - | 0 | \spawn monster 2964 |
| 2965 | Vorkal | chosen | Chosen | 142074 | 139950 / - | - | 10005 | 5700 / 5371 | 5 | \spawn monster 2965 |
| 2966 | Ratman | - | Human | 139987 | - | Stand | 0 | - | 0 | \spawn monster 2966 |
| 2967 | Rebel Guerilla | Rebels | Outlaw | 139991 | 96847 / - | - | 0 | 5311 / 5387 | 5 | \spawn monster 2967 |
| 2968 | Anton Hall | accord | Human | 125038 | - | UseAbilityOnInteract | 0 | - | 0 | \spawn monster 2968 |
| 2969 | Dr. Ignacio Alvarez | accord | Human | 125038 | - | UseAbilityOnInteract | 0 | - | 0 | \spawn monster 2969 |
| 2970 | Sand Man | accord | Human | 125038 | - / 106335 | UseAbilityOnInteract | 0 | - | 0 | \spawn monster 2970 |
| 2971 | Missing Daughter | accord | Human | 140092 | - | Arch_MedRangedHumanoid_Base | 0 | - | 0 | \spawn monster 2971 |
| 2972 | Sheriff Fairuza Nasseri | accord | Human | 125038 | 78065 / - | UseAbilityOnInteract_Dialog | 0 | - | 0 | \spawn monster 2972 |
| 2973 | Kazuo Mori | accord | Human | 125038 | - | UseAbilityOnInteract_Dialog | 0 | - | 0 | \spawn monster 2973 |
| 2974 | Cognac | accord | Human | 125038 | - | UseAbilityOnInteract_Dialog | 0 | - | 0 | \spawn monster 2974 |
| 2975 | Costa | accord | Human | 125038 | - | UseAbilityOnInteract_Dialog | 0 | - | 0 | \spawn monster 2975 |
| 2976 | Samira | accord | Human | 125038 | - | UseAbilityOnInteract_Dialog | 0 | - | 0 | \spawn monster 2976 |
| 2977 | Marius | accord | Human | 125038 | - | UseAbilityOnInteract_Dialog | 0 | - | 0 | \spawn monster 2977 |
| 2978 | Sergeant O'Neil | accord | Human | 52457 | - | AlertAndLookAtPlayer | 0 | - | 0 | \spawn monster 2978 |
| 2979 | Injured Soldier | friendly | Companion | 93709 | - | - | 0 | - | 0 | \spawn monster 2979 |
| 2980 | - | accord | Human | 122969 | 106335 / - | - | 0 | - | 0 | \spawn monster 2980 |
| 2981 | Private Davies | accord | Human | 140092 | 139879 / 76108 | - | 0 | - | 0 | \spawn monster 2981 |
| 2982 | Archfold Mission019 | accord | Human | 140093 | - | - | 0 | - | 5 | \spawn monster 2982 |
| 2983 | Garett Murdock | accord | Human | 118017 | 78063 / - | - | 0 | - | 0 | \spawn monster 2983 |
| 2984 | Zero Face | bandit | Outlaw | 142074 | 124703 / - | - | 10005 | 5313 / 5389 | 5 | \spawn monster 2984 |
| 2985 | The Dragon | chosen | Chosen | 142074 | 140162 / - | - | 10005 | 5700 / 5371 | 5 | \spawn monster 2985 |
| 2986 | - | accord | Human | 140164 | - | - | 0 | - | 0 | \spawn monster 2986 |
| 2987 | Serkan | chosen | Chosen | 125251 | - | - | 0 | - | 5 | \spawn monster 2987 |
| 2988 | - | accord | Human | 140164 | - | - | 0 | - | 0 | \spawn monster 2988 |
| 2989 | - | Blackhats | Outlaw | 124704 | 122621 / - | - | 10005 | 5311 / 5386 | 5 | \spawn monster 2989 |
| 2990 | Edward Koro | accord | Human | 125033 | - | - | 0 | - | 5 | \spawn monster 2990 |
| 2991 | Roughneck Lieutenant | accord | Human | 52460 | - | - | 0 | - | 5 | \spawn monster 2991 |
| 2992 | - | chosen | Chosen | 125069 | 140168 / - | - | 0 | 5698 / 5369 | 5 | \spawn monster 2992 |
| 2993 | Dr. Glacier | bandit | Outlaw | 142074 | 141979 / - | - | 10005 | 5313 / 5389 | 5 | \spawn monster 2993 |
| 2994 | The Giggler | bandit | Outlaw | 142074 | 114632 / - | - | 10005 | 5313 / 5389 | 5 | \spawn monster 2994 |
| 2995 | Leola Rex | bandit | Outlaw | 142074 | 139891 / - | - | 0 | 5313 / 5389 | 0 | \spawn monster 2995 |
| 2996 | Von Laar | Black Hills Bandits | Outlaw | 142074 | 106335 / - | - | 0 | 5313 / 5389 | 5 | \spawn monster 2996 |
| 2997 | Maynard | bandit | Outlaw | 142074 | 96871 / - | - | 0 | 5313 / 5389 | 5 | \spawn monster 2997 |
| 2998 | Anzu | bandit | Outlaw | 142074 | 96871 / 114632 | - | 10005 | 5313 / 5389 | 5 | \spawn monster 2998 |
| 2999 | Karina Sokoloff | Corporation - Omnidyne-M | Human | 125038 | - | UseAbilityOnInteract_Dialog | 0 | - | 0 | \spawn monster 2999 |
| 3000 | Recluse Expert Macabee | accord | Human | 118001 | - | AlertAndInteractive | 0 | - | 0 | \spawn monster 3000 |
| 3001 | Buzzard Fugitive | accord | Outlaw | 140092 | 96842 / - | StayAtSpawn | 0 | - | 0 | \spawn monster 3001 |
| 3002 | Electron Expert Macabee | accord | Human | 118001 | - | AlertAndInteractive | 0 | - | 0 | \spawn monster 3002 |
| 3003 | Raptor Expert Macabee | accord | Human | 118001 | - | AlertAndInteractive | 0 | - | 0 | \spawn monster 3003 |
| 3004 | Firecat Expert Macabee | accord | Human | 118001 | - | AlertAndInteractive | 0 | - | 0 | \spawn monster 3004 |
| 3005 | Rhino Expert Macabee | accord | Human | 118001 | - | AlertAndInteractive | 0 | - | 0 | \spawn monster 3005 |
| 3006 | Capt. Venus Dunai | accord | Human | 124276 | 96847 / - | AlertAndInteractive | 0 | - | 0 | \spawn monster 3006 |
| 3007 | Bastion Expert Harris | accord | Human | 52461 | - | AlertAndInteractive | 0 | - | 0 | \spawn monster 3007 |
| 3008 | Chosen Thermal Drone | chosen | Chosen | 120561 | 140378 / - | - | 0 | 5700 / 5371 | 0 | \spawn monster 3008 |
| 3009 | Dragonfly Expert Harris | accord | Human | 52461 | - | AlertAndInteractive | 0 | - | 0 | \spawn monster 3009 |
| 3010 | Nighthawk Expert Harris | accord | Human | 52461 | - | AlertAndInteractive | 0 | - | 0 | \spawn monster 3010 |
| 3011 | Mammoth Expert Harris | accord | Human | 52461 | - | AlertAndInteractive | 0 | - | 0 | \spawn monster 3011 |
| 3012 | Tigerclaw Expert Harris | accord | Human | 52461 | - | AlertAndInteractive | 0 | - | 0 | \spawn monster 3012 |
| 3013 | Kisuton Vendor | accord | Human | 52453 | - | AlertAndInteractive | 0 | - | 5 | \spawn monster 3013 |
| 3014 | Deputy Singh | accord | Human | 117031 | 84915 / - | AlertAndInteractive | 0 | - | 0 | \spawn monster 3014 |
| 3015 | Ekrem Demir | accord | Human | 125038 | - | UseAbilityOnInteract | 0 | - | 0 | \spawn monster 3015 |
| 3016 | Sergeant Attah | accord | Human | 125038 | 84915 / - | UseAbilityOnInteract | 0 | - | 0 | \spawn monster 3016 |
| 3017 | Captain Hailwic | accord | Human | 125038 | - | UseAbilityOnInteract | 0 | - | 0 | \spawn monster 3017 |
| 3018 | Doctor Zaira | accord | Human | 125038 | - | UseAbilityOnInteract | 0 | - | 0 | \spawn monster 3018 |
| 3019 | Chosen 35 | chosen | Chosen | 142074 | 139891 / - | - | 10005 | 5700 / 5371 | 5 | \spawn monster 3019 |
| 3020 | Brick | bandit | Outlaw | 142074 | 124703 / - | - | 0 | 5313 / 5389 | 0 | \spawn monster 3020 |
| 3021 | Sinker | bandit | Outlaw | 142074 | 139891 / - | - | 0 | 5313 / 5389 | 0 | \spawn monster 3021 |
| 3022 | Cueball | bandit | Outlaw | 142074 | 106335 / - | - | 10005 | 5313 / 5389 | 0 | \spawn monster 3022 |
| 3023 | Layla | accord | Human | 76133 | - | BasicCivilian_Stationary | 0 | - | 0 | \spawn monster 3023 |
| 3024 | Davis Royer | accord | Human | 125038 | - | UseAbilityOnInteract | 0 | - | 0 | \spawn monster 3024 |
| 3025 | Rina Joshi | accord | Human | 125038 | - | UseAbilityOnInteract | 0 | - | 0 | \spawn monster 3025 |
| 3026 | The Indexer | accord | Human | 125038 | - | UseAbilityOnInteract | 0 | - | 0 | \spawn monster 3026 |
| 3027 | Scarlet | accord | Human | 125038 | - | UseAbilityOnInteract | 0 | - | 0 | \spawn monster 3027 |
| 3028 | Wiley | accord | Human | 125038 | - / 78063 | UseAbilityOnInteract | 0 | - | 0 | \spawn monster 3028 |
| 3029 | Serkan | chosen | Chosen | 140557 | - | - | 0 | - | 5 | \spawn monster 3029 |
| 3030 | Foo Dog | bandit | Outlaw | 125069 | 114632 / - | - | 0 | 5311 / 5387 | 5 | \spawn monster 3030 |
| 3031 | Infected Intern | bandit | Outlaw | 125065 | 114632 / - | - | 0 | 5311 / 5387 | 5 | \spawn monster 3031 |
| 3032 | - | bandit | Outlaw | 125065 | 114632 / - | - | 0 | 5311 / 5387 | 5 | \spawn monster 3032 |
| 3033 | Yuki Lin | accord | Human | 125038 | - | UseAbilityOnInteract_Dialog | 0 | - | 0 | \spawn monster 3033 |
| 3034 | Mourningstar | accord | Human | 125033 | - | UseAbilityOnInteract_Dialog | 0 | 17 / - | 0 | \spawn monster 3034 |
| 3035 | Dr. Enya Vane | accord | Human | 125038 | - | UseAbilityOnInteract | 0 | - | 0 | \spawn monster 3035 |
| 3036 | Lt. Gunnar Nash | accord | Human | 125038 | 96847 / - | UseAbilityOnInteract | 0 | - | 0 | \spawn monster 3036 |
| 3037 | Petty Officer Justin Lai | accord | Human | 0 | - | Null | 0 | - | 0 | \spawn monster 3037 |
| 3038 | Scarlet | accord | Human | 117619 | - | AlertAndLookAtPlayer | 0 | - | 0 | \spawn monster 3038 |
| 3039 | Wiley | accord | Human | 140092 | - / 78063 | Arch_MedRangedHumanoid_Base | 0 | - | 0 | \spawn monster 3039 |
| 3040 | Camille Booker | accord | Human | 125038 | - | - | 0 | - | 0 | \spawn monster 3040 |
| 3041 | Alex Kamara | accord | Human | 118017 | - | AlertAndLookAtPlayer | 0 | - | 0 | \spawn monster 3041 |
| 3042 | Lt. Aaron Yip | accord | Human | 10003 | - / 96847 | - | 0 | - | 0 | \spawn monster 3042 |
| 3043 | Radamel Mumford | accord | Human | 118017 | 78063 / - | - | 0 | - | 0 | \spawn monster 3043 |
| 3044 | Chosen Healing Drone | chosen | Chosen | 121470 | 121471 / - | - | 0 | 5696 / 5615 | 5 | \spawn monster 3044 |
| 3046 | Chimera Rescue Drone | bandit | Human | 139843 | 140672 / - | - | 10004 | 5696 / 5615 | 5 | \spawn monster 3046 |
| 3047 | Panda | friendly | Companion | 140668 | - | - | 0 | - | 0 | \spawn monster 3047 |
| 3048 | SIN Vendor | accord | Human | 117015 | - | AlertAndInteractive | 0 | - | 0 | \spawn monster 3048 |
| 3049 | The GOAT | bandit | Outlaw | 125147 | 122621 / - | - | 0 | - | 0 | \spawn monster 3049 |
| 3050 | Scientist Intern | accord | Human | 117011 | - | AlertAndLookAtPlayer | 0 | - | 5 | \spawn monster 3050 |
| 3051 | Kim Armitage | accord | Human | 140092 | - | UseAbilityOnInteract | 0 | - | 0 | \spawn monster 3051 |
| 3052 | Sin Infected Punk | bandit | Outlaw | 125065 | 96842 / - | - | 0 | 5310 / 5386 | 0 | \spawn monster 3052 |
| 3053 | Kevin | bandit | Outlaw | 142074 | 124703 / - | - | 0 | 5313 / 5389 | 0 | \spawn monster 3053 |
| 3054 | ARES 88 | accord | Human | 52453 | - | - | 0 | - | 0 | \spawn monster 3054 |
| 3055 | Ekrem Demir | accord | Human | 107837 | - | PeacetimeCityWanderer | 0 | - | 0 | \spawn monster 3055 |
| 3056 | Momma Metal | accord | Human | 117010 | 78065 / - | AlertAndInteractive | 0 | - | 0 | \spawn monster 3056 |
| 3057 | Lost Girl | accord | Human | 140092 | - | Arch_MedRangedHumanoid_Base | 0 | - | 0 | \spawn monster 3057 |
| 3058 | Deikrast | bandit | Chosen | 125146 | 139891 / - | - | 0 | 5311 / 5386 | 0 | \spawn monster 3058 |
| 3059 | Unknown Man | neutral | Large wildlife | 0 | - | Null | 0 | - | 0 | \spawn monster 3059 |
| 3060 | Unknown Girl | neutral | Large wildlife | 0 | - | Null | 0 | - | 0 | \spawn monster 3060 |
| 3061 | Tarantus | chosen | Chosen | 140715 | 121315 / - | - | 0 | - | 0 | \spawn monster 3061 |
| 3062 | The Quail | accord | Human | 140092 | 96871 / 114632 | - | 0 | - | 0 | \spawn monster 3062 |
| 3063 | - | accord | Human | 117997 | - | Arch_MedRangedHumanoid_Base | 0 | - | 0 | \spawn monster 3063 |
| 3064 | Aliyan | chosen | Chosen | 140719 | 121315 / - | - | 0 | - | 5 | \spawn monster 3064 |
| 3065 | - | friendly | Large wildlife | 140724 | - | - | 0 | - | 0 | \spawn monster 3065 |
| 3066 | - | friendly | Large wildlife | 140735 | - | - | 0 | - | 0 | \spawn monster 3066 |
| 3067 | Captive Civilian | accord | Human | 140092 | - | Arch_MedRangedHumanoid_Base | 0 | - | 0 | \spawn monster 3067 |
| 3068 | Captive Civilian | accord | Human | 140092 | - | Arch_MedRangedHumanoid_Base | 0 | - | 0 | \spawn monster 3068 |
| 3069 | - | accord | Human | 140092 | 96842 / - | - | 0 | - | 0 | \spawn monster 3069 |
| 3070 | Trojan | accord | Human | 140092 | 96847 / - | Arch_MedRangedHumanoid_Base | 0 | - | 0 | \spawn monster 3070 |
| 3071 | TEST Jumpjet NPC | bandit | Human | 140900 | 114632 / - | Done | 0 | 3 / - | 0 | \spawn monster 3071 |
| 3072 | Tarantus | chosen | Chosen | 137024 | - | - | 0 | - | 5 | \spawn monster 3072 |
| 3073 | Tarantus | chosen | Chosen | 137024 | - | - | 0 | - | 5 | \spawn monster 3073 |
| 3074 | Battleframe Thief | Rebels | Outlaw | 125146 | 139891 / - | - | 10005 | 5310 / 5386 | 0 | \spawn monster 3074 |
| 3075 | - | Rebels | Outlaw | 125146 | 139891 / - | - | 10005 | 5310 / 5386 | 0 | \spawn monster 3075 |
| 3076 | - | Rebels | Outlaw | 125034 | 106335 / - | - | 10005 | 5310 / 5386 | 0 | \spawn monster 3076 |
| 3077 | - | Rebels | Outlaw | 125034 | 106335 / - | - | 10005 | 5310 / 5386 | 0 | \spawn monster 3077 |
| 3078 | - | Rebels | Outlaw | 125034 | 106335 / - | - | 10005 | 5310 / 5386 | 0 | \spawn monster 3078 |
| 3079 | - | Rebels | Outlaw | 125069 | 139886 / - | - | 0 | 5310 / 5386 | 0 | \spawn monster 3079 |
| 3080 | - | Rebels | Outlaw | 125069 | 139886 / - | - | 0 | 5310 / 5386 | 0 | \spawn monster 3080 |
| 3081 | - | Rebels | Outlaw | 125069 | 139888 / - | - | 10005 | 5310 / 5386 | 0 | \spawn monster 3081 |
| 3082 | - | Rebels | Outlaw | 125069 | 139888 / - | - | 10005 | 5310 / 5386 | 0 | \spawn monster 3082 |
| 3083 | - | Rebels | Outlaw | 125147 | 122543 / - | - | 10005 | 5310 / 5386 | 0 | \spawn monster 3083 |
| 3084 | - | Rebels | Outlaw | 125147 | 122543 / - | - | 10005 | 5310 / 5386 | 0 | \spawn monster 3084 |
| 3085 | Operations Agent | accord | Human | 96744 | - | AlertAndInteractive | 0 | - | 0 | \spawn monster 3085 |
| 3086 | Corporal Rasheed | accord | Human | 118308 | 97673 / - | AlertAndInteractive | 0 | - | 0 | \spawn monster 3086 |
| 3087 | RL-18 Support Drone | friendly | Companion | 140942 | - | - | 0 | - | 0 | \spawn monster 3087 |
| 3088 | Astrek Security - Ambient Pathing | accord | Human | 117031 | 137167 / - | Arch_MedRangedHumanoid_Base | 0 | - | 0 | \spawn monster 3088 |
| 3089 | Sgt. Cobb | accord | Human | 140092 | - | - | 0 | - | 0 | \spawn monster 3089 |
| 3090 | Chimera - Ambient Pathing | accord | Human | 137117 | - | Arch_MedRangedHumanoid_Base | 0 | - | 0 | \spawn monster 3090 |
| 3091 | Chimera Foot Soldier - Ambient Pathing | accord | Human | 140989 | 84915 / - | GuardCityWanderer | 0 | - | 0 | \spawn monster 3091 |
| 3092 | ARES Pilot | accord | Human | 10002 | 85972 / - | Arch_MedRangedHumanoid_Base | 0 | - | 0 | \spawn monster 3092 |
| 3093 | - | accord | Human | 125087 | 96871 / 96842 | - | 0 | - | 0 | \spawn monster 3093 |
| 3094 | - | accord | Human | 125087 | 96871 / - | - | 0 | - | 0 | \spawn monster 3094 |
| 3095 | Melding Puppy | friendly | Companion | 140991 | - | - | 0 | - | 0 | \spawn monster 3095 |
| 3096 | Dealer Augustus | Corporation - Kisuton | Human | 52453 | - | AlertAndInteractive | 0 | - | 5 | \spawn monster 3096 |
| 3097 | Derek Waters | accord | Human | 125033 | - | - | 0 | - | 0 | \spawn monster 3097 |
| 3098 | Agent Clarise | Corporation - Astrek Association | Human | 118004 | - | AlertAndInteractive | 0 | - | 0 | \spawn monster 3098 |
| 3099 | Broker Ochoa | Corporation - Omnidyne-M | Human | 118001 | - | AlertAndInteractive | 0 | - | 0 | \spawn monster 3099 |
| 3100 | Dropship Pilot - POI Placement | accord | Human | 141032 | - | AlertAndLookAtPlayer | 0 | - | 0 | \spawn monster 3100 |
| 3101 | Accord Marine | accord | Human | 122965 | - | - | 0 | 5310 / - | 0 | \spawn monster 3101 |
| 3102 | Ikinya | accord | Human | 125038 | - | UseAbilityOnInteract | 0 | - | 0 | \spawn monster 3102 |
| 3103 | Buzzard Lieutenant | accord | Human | 125034 | 106335 / - | - | 0 | - | 5 | \spawn monster 3103 |
| 3104 | Ho'ou | friendly | Companion | 76977 | - | - | 0 | - | 0 | \spawn monster 3104 |
| 3105 | Weak Necronus | chosen | Misc | 30436 | 30647 / - | - | 0 | - | 0 | \spawn monster 3105 |
| 3106 | Nian | melding | Melded | 141135 | 96917 / - | - | 0 | 5704 / - | 5 | \spawn monster 3106 |
| 3107 | Tanken Civilian - Ambient Pathing | accord | Human | 137117 | - | Arch_MedRangedHumanoid_Base | 0 | - | 0 | \spawn monster 3107 |
| 3108 | Dredge Civilian - Ambient Pathing | accord | Human | 137117 | - | Arch_MedRangedHumanoid_Base | 0 | - | 0 | \spawn monster 3108 |
| 3109 | Buzzard Civilian - Ambient Pathing | accord | Human | 137117 | - | Arch_MedRangedHumanoid_Base | 0 | - | 0 | \spawn monster 3109 |
| 3110 | Mendraxus | chosen | Chosen | 125252 | - | - | 0 | - | 5 | \spawn monster 3110 |
| 3111 | Nostromo Second Version To use only in Cinemas | accord | Human | 139390 | - | - | 0 | - | 0 | \spawn monster 3111 |
| 3112 | Bruce Gulliver | accord | Human | 138331 | - | AlertAndInteractive | 0 | - | 0 | \spawn monster 3112 |
| 3113 | Chimera Soldier - Ambient | accord | Human | 117034 | 84915 / - | BasicCivilian_Stationary | 0 | - | 0 | \spawn monster 3113 |
| 3114 | Tanken Soldier - Ambient | accord | Human | 117034 | 84915 / - | BasicCivilian_Stationary | 0 | - | 0 | \spawn monster 3114 |
| 3115 | Mayor Yamada | accord | Human | 137121 | - | AlertAndInteractive | 0 | - | 0 | \spawn monster 3115 |
| 3116 | Joan Linkletter | accord | Human | 117016 | - | AlertAndInteractive | 0 | - | 0 | \spawn monster 3116 |
| 3117 | Requisition Officer Bowen | accord | Human | 52453 | - | AlertAndInteractive | 0 | - | 0 | \spawn monster 3117 |
| 3118 | Tina Tandy | accord | Human | 117012 | - | AlertAndInteractive | 0 | - | 0 | \spawn monster 3118 |
| 3119 | Chloe | accord | Human | 117013 | - | AlertAndInteractive | 0 | - | 0 | \spawn monster 3119 |
| 3120 | Cuttle | accord | Human | 117010 | - | AlertAndInteractive | 0 | - | 0 | \spawn monster 3120 |
| 3121 | Pvt. Conrad Freeman | accord | Human | 117024 | - | AlertAndInteractive | 0 | - | 0 | \spawn monster 3121 |
| 3122 | - | accord | Human | 86129 | 122968 / - | Arch_MedRangedHumanoid_Base | 0 | 5311 / 5387 | 5 | \spawn monster 3122 |
| 3123 | Dredge Town Civilian | accord | Human | 117010 | - | BasicCivilian_Stationary | 0 | - | 0 | \spawn monster 3123 |
| 3125 | - | bandit | Outlaw | 118038 | 77328 / - | Arch_Brute_Acolyte_Base | 0 | - | 0 | \spawn monster 3125 |
| 3126 | Mourningstar | accord | Human | 107772 | 131847 / - | Arch_MedRangedHumanoid_Base | 0 | - | 0 | \spawn monster 3126 |
| 3127 | - | accord | Large wildlife | 140092 | - | Arch_MedRangedHumanoid_Base | 0 | - | 0 | \spawn monster 3127 |
| 3128 | Accord Scientist | accord | Human | 140092 | - | Arch_MedRangedHumanoid_Base | 0 | - | 0 | \spawn monster 3128 |
| 3129 | - | accord | Human | 140092 | 106335 / 76108 | - | 0 | - | 0 | \spawn monster 3129 |
| 3130 | Bethany Cooper | accord | Human | 122969 | 106335 / - | - | 0 | 5313 / - | 0 | \spawn monster 3130 |
| 3131 | Irena | accord | Human | 125087 | 96871 / 96842 | - | 0 | 5310 / - | 0 | \spawn monster 3131 |
| 3132 | Bob Smith | accord | Human | 125038 | - | UseAbilityOnInteract | 0 | - | 0 | \spawn monster 3132 |
| 3133 | Septix | chosen | Chosen | 141843 | 141810 / - | - | 10007 | 5699 / 5370 | 5 | \spawn monster 3133 |
| 3134 | - | accord | Human | 124761 | - | - | 0 | - | 0 | \spawn monster 3134 |
| 3135 | - | chosen | Chosen | 141553 | 141839 / - | - | 0 | - | 5 | \spawn monster 3135 |
| 3136 | - | accord | Human | 10001 | - | - | 0 | - | 0 | \spawn monster 3136 |
| 3137 | - | accord | Human | 124770 | - | EliteWanderer | 0 | - | 0 | \spawn monster 3137 |
| 3138 | Turret | chosen | Chosen | 141871 | - | Null | 0 | - | 0 | \spawn monster 3138 |
| 3139 | Accord Engineer | accord | Human | 10003 | - / 76108 | Arch_MedRangedHumanoid_Base | 0 | - | 0 | \spawn monster 3139 |
| 3140 | Laser Drill Operator | accord | Human | 81373 | - | PeacetimeCityWanderer | 0 | - | 0 | \spawn monster 3140 |
| 3141 | - | accord | Human | 141899 | - | - | 0 | - | 0 | \spawn monster 3141 |
| 3142 | Arch Scorcher | gaea | Wildlife | 141962 | 141913 / - | - | 10005 | 5706 / 5779 | 5 | \spawn monster 3142 |
| 3143 | Field Researcher | accord | Human | 122777 | - | PeacetimeCityWanderer | 0 | - | 0 | \spawn monster 3143 |
| 3144 | Melding Shard | melding | Melded | 76119 | 140672 / - | Null | 0 | - | 0 | \spawn monster 3144 |
| 3145 | Pyrexion | chosen | Chosen | 142074 | 143086 / 143087 | - | 10007 | 5699 / 5370 | 5 | \spawn monster 3145 |
| 3146 | Quentin Quinn | accord | Human | 125033 | - | - | 0 | - | 0 | \spawn monster 3146 |
| 3147 | Jodene Sparks | accord | Human | 125033 | - | - | 0 | - | 0 | \spawn monster 3147 |
| 3148 | Marco Marquez | accord | Human | 125033 | - | - | 0 | - | 0 | \spawn monster 3148 |
| 3149 | Hazel Murphy | accord | Human | 125033 | - | - | 0 | - | 0 | \spawn monster 3149 |
| 3150 | Hesh Hipplewhite | accord | Human | 125033 | - | - | 0 | - | 0 | \spawn monster 3150 |
| 3151 | - | melding | Melded | 122870 | 122871 / - | - | 10005 | 5705 / 5460 | 5 | \spawn monster 3151 |
| 3152 | Chosen Devils Tusk Commander | chosen | Chosen | 142074 | 142004 / - | - | 0 | 5700 / 5371 | 5 | \spawn monster 3152 |
| 3153 | Dr. Joshua Tilden | accord | Human | 125038 | - | UseAbilityOnInteract | 0 | - | 0 | \spawn monster 3153 |
| 3154 | Lt. Wallace Abernathy | accord | Human | 140092 | - / 76108 | Arch_MedRangedHumanoid_Base | 0 | - | 0 | \spawn monster 3154 |
| 3155 | Capt. Zachary Wallach | accord | Human | 125038 | - | UseAbilityOnInteract | 0 | - | 0 | \spawn monster 3155 |
| 3156 | Nian Meldling | melding | Wildlife | 142038 | 142146 / - | - | 0 | 5704 / - | 5 | \spawn monster 3156 |
| 3157 | Master Sgt. Lina Rask | accord | Human | 125038 | 78403 / - | UseAbilityOnInteract | 0 | - | 0 | \spawn monster 3157 |
| 3158 | Kelly Edison | accord | Human | 125038 | - | UseAbilityOnInteract | 0 | - | 0 | \spawn monster 3158 |
| 3159 | Lou Tavek | accord | Human | 125038 | - | UseAbilityOnInteract | 0 | - | 0 | \spawn monster 3159 |
| 3160 | Sgt. Chanming Lum | accord | Human | 117031 | 84915 / - | AlertAndInteractive | 0 | - | 0 | \spawn monster 3160 |
| 3161 | Lt. Eileen Ripley | accord | Human | 140092 | 106335 / 76108 | - | 0 | - | 0 | \spawn monster 3161 |
| 3162 | Pvt. Alton Huxley | accord | Human | 140092 | 76108 / - | Arch_MedRangedHumanoid_Base | 0 | - | 0 | \spawn monster 3162 |
| 3163 | Pvt. Luke "Badger" Exeter | accord | Human | 140092 | 122968 / - | - | 0 | - | 0 | \spawn monster 3163 |
| 3164 | Honey Scour | accord | Human | 125038 | - / 76108 | UseAbilityOnInteract | 0 | - | 0 | \spawn monster 3164 |
| 3165 | _Copy of Melding Tornado Funnel | melding | Melded | 142045 | - | PeacetimeCityWandererCore | 0 | - | 0 | \spawn monster 3165 |
| 3166 | Capt. Celia Malkovitch | accord | Human | 125038 | 78403 / - | UseAbilityOnInteract | 0 | - | 0 | \spawn monster 3166 |
| 3167 | Lt. Elliot Falstaff | accord | Human | 140092 | - / 114059 | Arch_MedRangedHumanoid_Base | 0 | - | 0 | \spawn monster 3167 |
| 3168 | Cmdr. Franz Auttenberg | accord | Human | 125038 | - | UseAbilityOnInteract | 0 | - | 0 | \spawn monster 3168 |
| 3169 | Capt. Oliver Spinner | accord | Human | 125038 | - | UseAbilityOnInteract | 0 | - | 0 | \spawn monster 3169 |
| 3170 | Nian Worshipper Grenadier | melding | Outlaw | 125146 | 96848 / - | - | 0 | 5312 / 5388 | 0 | \spawn monster 3170 |
| 3171 | Nian Worshipper Enforcer | melding | Outlaw | 125069 | 96847 / - | - | 0 | 5311 / 5387 | 0 | \spawn monster 3171 |
| 3172 | Nian Worshipper Dreadnaught | melding | Outlaw | 125034 | 106335 / - | - | 0 | 5313 / 5389 | 0 | \spawn monster 3172 |
| 3173 | Ophanim Lasher | Ophanim | Outlaw | 125147 | 142053 / - | - | 0 | 5311 / 5387 | 5 | \spawn monster 3173 |
| 3174 | Ultra Ophanim | Ophanim | Outlaw | 142074 | 142063 / - | - | 0 | 5313 / 5389 | 5 | \spawn monster 3174 |
| 3175 | Ophanim Kamikaze Bot | Ophanim | Wildlife | 95392 | - | - | 0 | 2 / - | 0 | \spawn monster 3175 |
| 3176 | Duke Luka Zorin | Ophanim | Outlaw | 142074 | 142063 / - | - | 0 | 5313 / 5389 | 5 | \spawn monster 3176 |
| 3177 | - | Ophanim | Outlaw | 125034 | 142063 / - | - | 0 | 3 / 5388 | 5 | \spawn monster 3177 |
| 3178 | Core Stability | neutral | Chosen | 142074 | 141979 / 140162 | - | 10005 | 5699 / 5370 | 5 | \spawn monster 3178 |
| 3179 | - | gaea | Wildlife | 96915 | 142093 / - | - | 0 | 5704 / - | 5 | \spawn monster 3179 |
| 3180 | Field Researcher | accord | Human | 122777 | - | Arch_MedRangedHumanoid_Base | 0 | - | 0 | \spawn monster 3180 |
| 3181 | - | accord | Human | 81373 | - | Arch_MedRangedHumanoid_Base | 0 | - | 0 | \spawn monster 3181 |
| 3182 | - | accord | Human | 10003 | - / 76108 | Arch_MedRangedHumanoid_Base | 0 | - | 0 | \spawn monster 3182 |
| 3183 | - | accord | Human | 122965 | 96842 / - | - | 0 | 5310 / - | 0 | \spawn monster 3183 |
| 3184 | - | accord | Human | 125087 | 96871 / 96842 | - | 0 | 5310 / - | 0 | \spawn monster 3184 |
| 3185 | Accord Ranger | accord | Human | 122966 | 96847 / - | - | 0 | 5311 / - | 0 | \spawn monster 3185 |
| 3186 | - | accord | Human | 140092 | - | Arch_MedRangedHumanoid_Base | 0 | - | 0 | \spawn monster 3186 |
| 3187 | Mobile Repulsion Unit | accord | Human | 85732 | - | PerformEmote | 0 | - | 0 | \spawn monster 3187 |
| 3188 | Doctor Aurelius Fong | accord | Human | 142120 | - | Arch_MedRangedHumanoid_Base | 0 | - | 0 | \spawn monster 3188 |
| 3189 | - | accord | Human | 75773 | 85975 / 85984 | EliteWanderer | 0 | - | 0 | \spawn monster 3189 |
| 3190 | - | gaea | Wildlife | 142113 | 142128 / - | GiantAranhaMiniBoss | 0 | 5704 / 5396 | 5 | \spawn monster 3190 |
| 3191 | Accord Engineer | accord | Human | 143062 | - | Arch_MedRangedHumanoid_Base | 0 | - | 0 | \spawn monster 3191 |
| 3192 | Honey Scour | accord | Human | 125038 | - / 76108 | UseAbilityOnInteract | 0 | - | 0 | \spawn monster 3192 |
| 3193 | Widow Wary | accord | Human | 125038 | - / 76108 | UseAbilityOnInteract | 0 | - | 0 | \spawn monster 3193 |
| 3194 | ARES 91 | accord | Human | 10002 | - / 76108 | Arch_MedRangedHumanoid_Base | 0 | - | 0 | \spawn monster 3194 |
| 3195 | Little Kahuna | friendly | Companion | 142139 | - | - | 0 | - | 0 | \spawn monster 3195 |
| 3196 | Doctor Aurelius Fong | accord | Human | 142142 | - | Arch_MedRangedHumanoid_Base | 0 | - | 0 | \spawn monster 3196 |
| 3197 | - | accord | Human | 142150 | - | - | 0 | - | 0 | \spawn monster 3197 |
| 3198 | Devil's Tusk Accord Soldier - Ambient Pathing | accord | Human | 117035 | 137167 / - | Arch_MedRangedHumanoid_Base | 0 | - | 0 | \spawn monster 3198 |
| 3199 | - | gaea | Wildlife | 122767 | 122768 / - | - | 0 | 5703 / 5395 | 5 | \spawn monster 3199 |
| 3200 | Brinewyrm | friendly | Companion | 142178 | - | - | 0 | - | 0 | \spawn monster 3200 |
| 3201 | Nian Jr. | friendly | Companion | 142181 | - | - | 0 | - | 0 | \spawn monster 3201 |
| 3202 | Mission022_ReactorDoor | accord | Human | 142223 | - | - | 0 | - | 0 | \spawn monster 3202 |
| 3203 | Mission022_CoreDoor | accord | Human | 142213 | - | - | 0 | - | 0 | \spawn monster 3203 |
| 3204 | Healing Target | accord | Human | 122965 | - | OneOff_StandStill | 0 | 5310 / - | 0 | \spawn monster 3204 |
| 3205 | Target Drone | monster | Misc | 77744 | - | Null | 0 | - | 0 | \spawn monster 3205 |
| 3206 | Capt. Hudson Fuller | accord | Human | 142241 | - | - | 0 | - | 10 | \spawn monster 3206 |
| 3207 | 1st Sergeant Christina Vargas | accord | Human | 117028 | - | AlertAndInteractive | 0 | - | 10 | \spawn monster 3207 |
| 3208 | Doctor Demarius Sifton | accord | Human | 117023 | - | AlertAndInteractive | 0 | - | 10 | \spawn monster 3208 |
| 3209 | Tech Sergeant Jon Gosse | accord | Human | 117023 | - | AlertAndInteractive | 0 | - | 10 | \spawn monster 3209 |
| 3210 | ARES 22 | accord | Human | 117023 | - | AlertAndInteractive | 0 | - | 10 | \spawn monster 3210 |
| 3211 | Supply Officer Finley Spiegel | accord | Human | 117023 | - | AlertAndInteractive | 0 | - | 10 | \spawn monster 3211 |
| 3212 | - | accord | Human | 143009 | - | AlertAndInteractive | 0 | - | 0 | \spawn monster 3212 |
| 3213 | Echo Tracking Drone | accord | Misc | 140092 | - | - | 0 | - | 0 | \spawn monster 3213 |
| 3214 | Cmdr. Rolan Volkov | accord | Human | 125038 | - | UseAbilityOnInteract | 0 | - | 0 | \spawn monster 3214 |
| 3215 | Jacques Voclain | accord | Human | 125038 | - | UseAbilityOnInteract | 0 | - | 0 | \spawn monster 3215 |
| 3216 | Gunship Pilot | accord | Human | 86619 | 78496 / - | Arch_MedRangedHumanoid_Base | 0 | - | 0 | \spawn monster 3216 |
| 3217 | Jacques Voclain | accord | Human | 125038 | - | UseAbilityOnInteract | 0 | - | 0 | \spawn monster 3217 |
| 3218 | Orca Obscure | accord | Human | 125038 | - / 76108 | UseAbilityOnInteract | 0 | - | 0 | \spawn monster 3218 |
| 3219 | - | friendly | Companion | 143063 | - | - | 0 | - | 0 | \spawn monster 3219 |
| 3220 | Tiny General | friendly | Companion | 143074 | - | - | 0 | - | 0 | \spawn monster 3220 |
| 3221 | Rookie Grinder | friendly | Companion | 143076 | - | - | 0 | - | 0 | \spawn monster 3221 |
| 3222 | Fireball | chosen | Wildlife | 139907 | - | OneOff_FollowRoute | 0 | - | 5 | \spawn monster 3222 |
| 3223 | - | accord | Human | 142952 | - | - | 0 | - | 0 | \spawn monster 3223 |
| 3224 | Capt. Hudson Fuller Death Pose | accord | Human | 133048 | - | - | 0 | - | 10 | \spawn monster 3224 |
| 3225 | Doctor Aurelius Fong Death Pose for head | accord | Human | 142120 | - | Arch_MedRangedHumanoid_Base | 0 | - | 0 | \spawn monster 3225 |
| 3226 | - | friendly | Companion | 139294 | - | PassivePet | 0 | - | 0 | \spawn monster 3226 |
| 3227 | - | chosen | Chosen | 125146 | 139891 / - | - | 10005 | 5699 / 5370 | 5 | \spawn monster 3227 |
| 3228 | - | chosen | Chosen | 143088 | 139891 / - | - | 10005 | 5699 / 5370 | 0 | \spawn monster 3228 |
| 3229 | - | melding | Melded | 143089 | - | - | 0 | 5704 / 5414 | 5 | \spawn monster 3229 |
| 3230 | - | monster | Misc | 143095 | - | Rabbit | 0 | 3 / - | 0 | \spawn monster 3230 |
| 3231 | - | accord | Human | 81381 | - | - | 0 | - | 0 | \spawn monster 3231 |
| 3232 | - | accord | Human | 81381 | - | - | 0 | - | 0 | \spawn monster 3232 |
| 3233 | - | accord | Human | 81381 | - | - | 0 | - | 0 | \spawn monster 3233 |
| 3234 | - | accord | Human | 81381 | - | - | 0 | - | 0 | \spawn monster 3234 |
| 3235 | - | accord | Human | 81381 | - | - | 0 | - | 0 | \spawn monster 3235 |
| 3236 | - | accord | Human | 81381 | - | - | 0 | - | 0 | \spawn monster 3236 |
| 3237 | - | accord | Human | 81381 | - | - | 0 | - | 0 | \spawn monster 3237 |
| 3238 | - | accord | Human | 81381 | - | - | 0 | - | 0 | \spawn monster 3238 |
| 3239 | Accord Dropship Pilot | accord | Human | 140092 | - | - | 0 | - | 0 | \spawn monster 3239 |
| 3240 | - | chosen | Chosen | 142074 | 114632 / - | - | 0 | - | 5 | \spawn monster 3240 |
| 3241 | - | accord | Human | 143972 | 143973 / 140880 | - | 0 | - | 10 | \spawn monster 3241 |
| 3242 | - | chosen | Chosen | 143146 | - | PerformEmote | 0 | - / 5601 | 3.40282e+38 | \spawn monster 3242 |
| 3243 | - | accord | Human | 125038 | - | UseAbilityOnInteract | 0 | - | 0 | \spawn monster 3243 |
| 3244 | - | gaea | Wildlife | 96915 | 96917 / - | - | 0 | 5704 / - | 5 | \spawn monster 3244 |
| 3245 | SIN Lion | accord | Wildlife | 96915 | 143189 / - | - | 0 | 5704 / - | 5 | \spawn monster 3245 |
| 3246 | Zodiac Monkey | friendly | Companion | 143662 | - | - | 0 | - | 0 | \spawn monster 3246 |
| 3247 | Hunting Kestrel | Black Hills Bandits | Outlaw | 96882 | 143804 / - | - | 0 | 5702 / 5385 | 5 | \spawn monster 3247 |
| 3248 | - | Reapers | Outlaw | 96882 | 143804 / - | - | 0 | 5702 / 5385 | 5 | \spawn monster 3248 |
| 3249 | - | chosen | Chosen | 125069 | 32739 / - | - | 0 | 5698 / 5369 | 5 | \spawn monster 3249 |
| 3250 | 9 Tailed Fox | friendly | Companion | 143820 | - | - | 0 | - | 0 | \spawn monster 3250 |
| 3251 | Player Chosen Fiend | chosen | Chosen | 143851 | 143870 / - | - | 0 | - | 5 | \spawn monster 3251 |
| 3252 | Player Chosen Shock Trooper | chosen | Chosen | 143873 | 143859 / - | - | 0 | - | 5 | \spawn monster 3252 |
| 3253 | Chosen Emissary | friendly | Companion | 143862 | - | AlertAndInteractive | 0 | - | 5 | \spawn monster 3253 |
| 3254 | Chosen Devastator Polymorph | chosen | Chosen | 143896 | 143897 / - | - | 0 | 5699 / 5370 | 5 | \spawn monster 3254 |
| 3255 | - | chosen | Chosen | 143901 | 87382 / - | - | 0 | 5699 / 5370 | 5 | \spawn monster 3255 |
| 3256 | Emissary Assistant | chosen | Chosen | 124831 | - | AlertAndInteractive | 0 | 5701 / 5372 | 5 | \spawn monster 3256 |
| 3257 | Emissary Quartermaster | chosen | Chosen | 140092 | 143920 / - | AlertAndInteractive | 0 | 5701 / 5372 | 5 | \spawn monster 3257 |
| 3258 | - | accord | Human | 10002 | 85972 / - | AlertAndInteractive | 0 | - | 0 | \spawn monster 3258 |
| 3259 | ARES 88 | accord | Human | 10002 | 85972 / - | AlertAndInteractive | 0 | - | 0 | \spawn monster 3259 |
| 3261 | Accord Dropship Pilot | accord | Human | 125033 | - | - | 0 | - | 0 | \spawn monster 3261 |
| 3262 | Desmond Poe | accord | Human | 106357 | - | AlertAndInteractive | 0 | - | 0 | \spawn monster 3262 |

---

Regenerate: `python3 Tools/SdbDump/spawn_reference.py <clientdb.sd2>` - see [README.md](README.md). Related: [../MOBS_AND_NPCS.md](../MOBS_AND_NPCS.md) (mobs grouped by faction, with the anatomy of a monster row), [../SPAWNING_AND_COMBAT.md](../SPAWNING_AND_COMBAT.md) (what happens after the spawn), [../STATIC_DATABASE.md](../STATIC_DATABASE.md) (the file format and the commands).
