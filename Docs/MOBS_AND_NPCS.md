# Mobs & NPCs — Database Catalog

This document is the reference for every mob / NPC definition available in
Firefall's static database (`clientdb.sd2`) and how PIN turns those rows into
living entities. The *mechanics* of spawning (chat commands, per-zone JSON,
combat gating) live in [SPAWNING_AND_COMBAT.md](SPAWNING_AND_COMBAT.md); this
document is about **the data itself**.

---

## 1. Where mob data lives

All NPC definitions live in the SDB file the server loads at startup
(`StaticDBPath` in `GameServer.config.json`, normally
`Firefall/system/db/clientdb.sd2`). The relevant tables are:

| Table                          | Contents                                                        |
|--------------------------------|-----------------------------------------------------------------|
| `dbcharacter::Monster`         | **The mob/NPC catalog.** One row per character type.             |
| `dbcharacter::MonsterScaling`  | Per-level health/damage scaling rows referenced by a monster.    |
| `dbcharacter::MonsterAttributeRange` | Attribute curves (base / per-level / module effect).       |
| `dbcharacter::MonsterTitle`    | Title ids a monster row can reference.                           |
| `dbcharacter::MonsterMood` / `MonsterMoodName` | Ambient mood/animation sets.                     |
| `dbcharacter::MonsterVisualOption(s)` | Random visual variants (heads, colors) per monster.      |
| `dbcharacter::Faction`         | Faction ids (`internal_name`, localized display name).           |
| `dbcharacter::FactionRelations`| Stance matrix between factions (hostile/friendly/neutral).       |
| `dbcharacter::Turret`          | Placeable turret characters (and `TurretWeapon` their guns).     |
| `dbcharacter::Deployable`      | Deployables (battleframe station, thumper beacons, ...).         |
| `dblocalization::LocalizedText`| `id -> English` strings; monster/faction names resolve here.     |
| `dbitems::Battleframe`         | Chassis records — monster rows point at one via `chassis_id`.    |
| `dbitems::WeaponTemplates`     | Weapons referenced by `weapon1_id` / `weapon2_id`.               |

A monster row itself carries no plain-text name: `localized_name_id` is a key
into `dblocalization::LocalizedText`, and `faction_id` keys `dbcharacter::Faction`
(which again points into localization for its display name).

## 2. Anatomy of a `dbcharacter::Monster` row

Mirrors `UdpHosts/GameServer/StaticDB/Records/dbcharacter/Monster.cs` (column
names are the snake_case forms PIN's loader looks up):

| Column (group)          | Meaning                                                              |
|-------------------------|----------------------------------------------------------------------|
| `id`                    | The character type id used everywhere (`\npc <id>`, spawn JSON).      |
| `localized_name_id`     | Key into `dblocalization::LocalizedText` for the display name.        |
| `faction_id`            | Faction row; drives the hostility stance against players.             |
| `race`, `gender`        | Census bytes (`gender` is the ASCII `'M'`/`'F'`).                     |
| `chassis_id`            | `dbitems::Battleframe` chassis: body/visuals and the jetpack energy params (max/recharge/delay) PIN replicates from it. |
| `backpack_id`           | Backpack visual slot.                                                 |
| `weapon1_id`, `weapon2_id` | `dbitems::WeaponTemplates` entries in loadout slots Primary/Secondary. |
| `head_id`, `eyes_id`, `head_acc1_id`, `head_acc2_id`, `charinfo_id` | Head/face visual build. |
| `*_color` (skin, lip, eye, hair, facial hair) | Color ids for the visuals block.                  |
| `*_warpaint_palette_id` (fullbody, armor, bodysuit, glow) | Warpaint palettes (`dbvisualrecords::WarpaintPalette`). |
| `ornaments_map_group_id_1..4` | Ornament groups (event hats, etc.).                            |
| `visual_options_id`, `visuals_group_id` | Random visual variant sets.                          |
| `behavior`, `behavior_offensive`, `behavior_defensive` (+ `*_instance_id`) | CAIS behavior set names/ids (combat AI). |
| `health_regen`          | Out-of-combat health regeneration.                                    |
| `scaling_table_id`      | `dbcharacter::MonsterScaling` set: level -> health/damage.            |
| `loot_table_id`, `loot_table2_id` | Loot rolls on death.                                        |
| `xp_resource_id`, `xpreward_type` | Reward grants on kill.                                      |
| `normal_speed`, `fast_speed`, `body_radius`, `body_mass`, `body_height` | Movement / physics shape. |
| `min_rand_scale`, `max_rand_scale` | Random model scale range.                                  |
| `ai_spawn_delay_ms`     | Delay before AI activates after spawn.                                |
| `respawn_flags`, `gravity`, `is_componented`, `damage_response_id`, `posetype_id`, `voice_set`, `title`, `vendor_id`, `terminal_type_name`, `crafting_type_id`, `network_fidelity`, `difficulty_cost`, `projectile_offset` | Misc simulation/display knobs. |

## 3. How PIN turns a row into an entity

```
CustomData/character_spawn.json | \npc 290 | admin "npc 290"
  -> EntityManager.SpawnCharacter(typeId, position)
       -> CharacterEntity.LoadMonster(typeId)        (CharacterEntity.cs)
            SDBInterface.GetMonster(typeId)          (dbcharacter::Monster row)
            SDBUtils.GetChassisWarpaint(...)         (visual palettes)
            CharacterLoadout { chassis, backpack, weapons }
            SetStaticInfo   { NameLocalizationId, Race, Gender, TargetFlags.IsNPC, ... }
            SetHostilityInfo{ FactionId }            (stance vs. players)
            ApplyLoadout    (replicates visuals + battleframe energy params)
       -> physics kinetic body, CharacterLifecycle.OnCharacterCreated
```

- The name shown client-side resolves through `NameLocalizationId`; the server
  itself never needs the string.
- The chassis lookup (`SDBInterface.GetBattleframe`) is also what feeds the
  replicated **jetpack** `EnergyParams` (max / recharge / delay) — the only
  energy pool in the game; abilities do not consume it.
- On death: `DamageSystem` -> `CharacterLifecycleService` (`CharacterDiedEvent`)
  -> `NpcDeathService` (gib visuals, 10 s corpse linger by default).
- NPC behavior strings (`behavior*`) are **not simulated yet** — spawned mobs
  stand idle until shot; there is no chase/attack AI server-side.

## 4. What the database actually contains (catalog)

> The catalog below is generated from a real `clientdb.sd2` with
> `Tools/SdbDump` (see §5). **This section is pending**: the dump has not been
> generated yet because a `clientdb.sd2` copy was not available when this was
> written. Once available, run the command in §5 and paste the summary here.

Planned layout of this section:

1. **Totals** — monster rows, named rows, factions, turrets.
2. **By faction** — every monster grouped by `Faction.internal_name`
   (Accord, Bandits, Chosen, Melding wildlife, ...), each entry as
   `id`, `name`, `race`, weapons, scaling/level range.
3. **Turrets & deployables** — `dbcharacter::Turret` / `Deployable` rows.

### Already-known entries

These ids are referenced by PIN content today
(`StaticDB/CustomData/character_spawn.json` for zones 12 and 1003, the zone 448
test spawn in `EntityManager.TempSpawnTestEntities`, and
[SPAWNING_AND_COMBAT.md](SPAWNING_AND_COMBAT.md)):

| id   | Name                | Zone(s)   | Note                          |
|------|---------------------|-----------|-------------------------------|
| 290  | Accord Assault      | 12, 1003  | Friendly (Accord) — unshootable |
| 528  | Melded Aranha       | 12        | Hostile                       |
| 1196 | Chosen Fiend        | 1003      | Hostile                       |
| 1304 | Black Hills Bandit  | 1003      | Hostile                       |
| 2342 | Aranha              | 12        | Hostile                       |
| 2407 | Tanken Saboteur     | 1003      | Hostile                       |
| 356  | Aero                | 448       | Named NPC (New Eden test spawn) |

## 5. Regenerating the catalog

`Tools/SdbDump` is a dependency-free decoder for `.sd2` files (same format
logic as the `FauFau` library PIN uses; verified by its round-trip self-test).

```sh
# Full mobs report (monsters + names + factions + scaling + turrets)
python3 Tools/SdbDump/sdb_dump.py monsters /path/to/clientdb.sd2 -o monsters.json

# Anything else, table by table
python3 Tools/SdbDump/sdb_dump.py dump /path/to/clientdb.sd2 dbcharacter::Faction
python3 Tools/SdbDump/sdb_dump.py info   /path/to/clientdb.sd2
```

The tool never needs a Firefall installation, and the `.sd2` itself is game
data that stays out of the repository.
