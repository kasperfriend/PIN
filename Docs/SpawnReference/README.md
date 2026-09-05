# Spawnable Reference

**Every spawnable row in Firefall's static database, with the exact command that creates it.** 7,396 rows across 5 tables - 4,211 of them named - decoded from `clientdb.sd2` build **prod-1962** and written by `Tools/SdbDump/spawn_reference.py`.

> **Generated file** - do not edit by hand. Regenerate with `python3 Tools/SdbDump/spawn_reference.py <clientdb.sd2>` (see [README.md](README.md#5-regenerating-this-folder)).

One document per spawnable kind, each a flat spreadsheet of the whole table sorted by id:

| document | kind | SDB table | rows | named | unnamed | CSV |
|---|---|---|---|---|---|---|
| [Mobs & NPCs](MOBS.md) | `monster` | `dbcharacter::Monster` | 3,109 | 1,772 | 1,337 | [csv/mobs.csv](csv/mobs.csv) |
| [Deployables](DEPLOYABLES.md) | `deployable` | `dbcharacter::Deployable` | 3,902 | 2,088 | 1,814 | [csv/deployables.csv](csv/deployables.csv) |
| [Vehicles](VEHICLES.md) | `vehicle` | `vcs::VehicleInfo` | 173 | 173 | 0 | [csv/vehicles.csv](csv/vehicles.csv) |
| [Carryables](CARRYABLES.md) | `carryable` | `dbitems::CarryableObject` | 105 | 71 | 34 | [csv/carryables.csv](csv/carryables.csv) |
| [Turrets](TURRETS.md) | `turret` | `dbcharacter::Turret` | 107 | 107 | 0 | [csv/turrets.csv](csv/turrets.csv) |

The tables mirror `UdpHosts/GameServer/StaticDB/SDBCatalog.cs` and the commands mirror `Systems/Spawning/SDBSpawner.cs`, so what is listed here is exactly what the in-game `sdb` / `sdbinfo` / `spawn` commands see. Anything in these documents can be spawned straight away - no code changes, no JSON, no config.

---

## 1. The command

```
\spawn <kind> <id|name> [<x> <y> <z>]     # chat window (the backslash is the command prefix)
spawn <kind> <id|name> [<x> <y> <z>]      # Admin chat channel, or the server console
```

| part | rules |
|------|-------|
| `<kind>` | `monster` \| `deployable` \| `vehicle` \| `carryable` \| `turret` (aliases below) |
| `<id\|name>` | a numeric row id, or a name - case-insensitive, no quoting needed, multi-word names work. Exact match beats prefix match beats substring match; ambiguous names are rejected with the candidate list. |
| `[<x> <y> <z>]` | optional position, parsed with invariant culture (`-25.5 118 492`). Omit it to spawn at your character's position; required from the server console. |

`turret` is the one exception: turrets are child entities, so `spawn turret <id|name>` takes no position, always attaches to the calling player's character, and is refused from the server console. See [TURRETS.md](TURRETS.md).

Kind aliases (all case-insensitive):

| kind | accepted words |
|---|---|
| `monster` | `monster`, `monsters`, `npc`, `npcs`, `char`, `character`, `characters`, `mob`, `mobs` |
| `deployable` | `deployable`, `deployables`, `dep` |
| `vehicle` | `vehicle`, `vehicles`, `veh` |
| `carryable` | `carryable`, `carryables`, `carry` |
| `turret` | `turret`, `turrets` |

Discovery and inspection - the same kinds, no id needed:

```
\sdb                                     # row counts per kind
\sdb <kind>                              # first 20 rows
\sdb <kind> <filter> [limit]             # search by name or exact id (limit max 200)
\sdbinfo <kind> <id|name>                # every interesting field of one row
```

`sdb` / `sdbinfo` print to the **client console** (multi-line output), not the chat line. Command aliases: `spawn`/`sdbspawn`/`spawn_sdb`, `sdb`/`sdblist`/`sdbsearch`/`sdbfind`, `sdbinfo`/`sdbshow`/`sdbrow`.

Older typed commands, still available and unchanged:

| kind | typed command | where |
|---|---|---|
| `monster` | `\npc <id> [<x> <y> <z>]` | chat + admin (aliases `character`, `monster`, `spawn_npc`, `spawn_character`, `spawn_monster`) |
| `deployable` | `deployable <id> [<x> <y> <z>]` | admin only (alias `spawn_deployable`) |
| `vehicle` | `vehicle <id> [<x> <y> <z>]` | admin only (alias `spawn_vehicle`) |
| `carryable` | `carryable <id> [<x> <y> <z>]` | admin only (alias `spawn_carryable`) |
| `turret` | - none - | `spawn turret` is the only command that creates one |

The typed commands take ids only; `spawn` adds name resolution, discovery, turrets and one consistent syntax.

## 2. Reading the tables

- `-` in a cell means "empty / not set". For monster movement and body columns (`normal_speed`, `body_radius`, ...) `-1` means *inherit from the chassis battleframe*.
- Unnamed rows (`name` = `-`) are placeholders, cut content or internal variants. They are real, spawnable rows, but they can only be referenced by id and `spawn by name` will not find them.
- Ids are the game's own type ids: the same numbers `character_spawn.json`, `\npc` and the entity views use.
- Foreign keys are given as ids in the Markdown tables and as ids **plus** resolved names in the CSVs (chassis, weapons, loot tables, categories, functions, abilities).
- A value rendered as `3.40282e+38` is the C++ `FLT_MAX` sentinel the data uses for "no limit" (one cell in this build: `health_regen` of monster 3242).

## 3. Factions (`dbcharacter::Faction`)

Faction decides whether a spawned entity will fight you: the player defaults to faction `1` (`accord`), `CombatSim` drops hits on `Friendly`/`Self` stances, and unknown relations fall back to `Neutral` (which passes). `default_stance <= -1` marks a faction that is hostile unless a `dbcharacter::FactionRelations` row says otherwise.

| id | internal name | display name | abbrev | default stance | mob rows |
|---|---|---|---|---|---|
| 1 | accord | The Accord | Accord | neutral | 1,731 |
| 2 | chosen | The Chosen | Chosen | hostile by default | 270 |
| 3 | ??? | - | - | hostile by default | 0 |
| 4 | friendly | Friendly | Friendly | neutral | 114 |
| 5 | monster | Monster | Monster | hostile by default | 17 |
| 6 | melding | Melding | Melding | hostile by default | 122 |
| 7 | gaea | Gaea | Gaea | neutral | 431 |
| 8 | bandit | Bandits | Bandit | hostile by default | 249 |
| 9 | neutral | Neutral | Neutral | neutral | 42 |
| 10 | Corporation - Omnidyne-M | Omnidyne-M | Omnidyne | neutral | 2 |
| 11 | Corporation - Kisuton | Kisuton | Kisuton | neutral | 1 |
| 12 | Corporation - Astrek Association | Astrek Association | Astrek | neutral | 1 |
| 13 | accord | Destra DNA | Destra | neutral | 0 |
| 14 | Corporation - HelioSys | HelioSys | HelioSys | neutral | 0 |
| 15 | Civilian | - | - | neutral | 1 |
| 16 | Nutretic | Nutretic Processing | Nutretic | neutral | 0 |
| 17 | Tanken | Tanken | Tanken | neutral | 8 |
| 18 | accord | - | - | neutral | 0 |
| 19 | Hellhounds | Hellhounds | Hellhounds | neutral | 0 |
| 20 | copa | Copacabana | Copa | neutral | 0 |
| 21 | Northern Shores | Northern Shores | Northern Shores | neutral | 0 |
| 22 | Black Hills Bandits | Black Hills Bandits | Bandit | hostile by default | 28 |
| 23 | Thump Dump | Thump Dump | Thump Dump | neutral | 0 |
| 24 | Cerrado Plains | Cerrado Plains | Cerrado Plains | neutral | 0 |
| 25 | Trans Hub | Trans Hub | Trans Hub | neutral | 0 |
| 26 | Broken Shores | Broken Shores | Broken Shores | neutral | 0 |
| 27 | Sunken Harbor | Sunken Harbor | Sunken Harbor | neutral | 0 |
| 28 | Stonewall | Stonewall | Stonewall | neutral | 0 |
| 29 | Shanty Town | Shanty Town | Shanty Town | neutral | 0 |
| 30 | Dredge | Dredge | Dredge | neutral | 0 |
| 31 | Andreev | Astrek Andreev Station | Andreev | neutral | 0 |
| 32 | The Nest | The Nest | Nest | neutral | 0 |
| 33 | FOB Sagan | FOB Sagan | Sagan | neutral | 0 |
| 34 | Lab 16 | Lab 16 | Lab 16 | neutral | 0 |
| 35 | Tecumseh Airbase | Tecumseh Airbase | Tecumseh | neutral | 0 |
| 36 | FOB Harpoon | FOB Harpoon | Harpoon | neutral | 0 |
| 37 | Crossroads Station | Crossroads Station | Crossroads | neutral | 0 |
| 38 | Forest Watch | Forest Watch | Forest Watch | neutral | 0 |
| 39 | Kanaloa Research | Kanaloa Research Station | Kanaloa Research | neutral | 0 |
| 40 | Camp Jasper | Camp Jasper | Camp Jasper | neutral | 0 |
| 41 | Stronghold | Stronghold | Stronghold | neutral | 0 |
| 42 | Aranha | Gaea - Aranha | Aranha | hostile by default | 0 |
| 43 | Hissers | Gaea - Hissers | Hissers | hostile by default | 0 |
| 44 | Crimson Storm | Crimson Storm | Crimson Storm | neutral | 0 |
| 45 | Rebels | Rebels | Rebels | neutral | 35 |
| 46 | Reapers | Reapers | Reapers | hostile by default | 23 |
| 47 | Ophanim | Ophanim | Ophanim | hostile by default | 16 |
| 48 | Blackhats | Blackhats | Blackhats | neutral | 1 |
| 49 | Skydock Command | Skydock Command | Skydock Command | neutral | 0 |
| 50 | UAS Vanguard | UAS Vanguard | UAS Vanguard | neutral | 0 |

## 4. Monster scaling (`dbcharacter::MonsterScaling`)

80 levels; `SDBInterface.GetMonsterScaling(level)` is keyed by level.

| level | health | damage |
|---|---|---|
| 1 | 100 | 50 |
| 2 | 125 | 63 |
| 3 | 156 | 78 |
| 4 | 195 | 98 |
| 5 | 244 | 122 |
| 6 | 305 | 153 |
| 7 | 381 | 191 |
| 8 | 477 | 238 |
| 9 | 596 | 298 |
| 10 | 745 | 373 |
| 11 | 879 | 440 |
| 12 | 1037 | 519 |
| 13 | 1224 | 612 |
| 14 | 1445 | 722 |
| 15 | 1705 | 852 |
| 16 | 2011 | 1006 |
| 17 | 2373 | 1187 |
| 18 | 2801 | 1400 |
| 19 | 3305 | 1652 |
| 20 | 3900 | 1950 |
| 21 | 4289 | 2145 |
| 22 | 4718 | 2359 |
| 23 | 5190 | 2595 |
| 24 | 5709 | 2855 |
| 25 | 6280 | 3140 |
| 26 | 6908 | 3454 |
| 27 | 7599 | 3800 |
| 28 | 8359 | 4179 |
| 29 | 9195 | 4597 |
| 30 | 10114 | 5057 |
| 31 | 10923 | 5462 |
| 32 | 11797 | 5899 |
| 33 | 12741 | 6371 |
| 34 | 13760 | 6880 |
| 35 | 14861 | 7431 |
| 36 | 16050 | 8025 |
| 37 | 17334 | 8667 |
| 38 | 18721 | 9360 |
| 39 | 20219 | 10109 |
| 40 | 21836 | 10918 |
| 41 | 22928 | 11464 |
| 42 | 24074 | 12037 |
| 43 | 25278 | 12639 |
| 44 | 26542 | 13271 |
| 45 | 27869 | 13934 |
| 46 | 29262 | 14631 |
| 47 | 30726 | 15363 |
| 48 | 32262 | 16131 |
| 49 | 33875 | 16937 |
| 50 | 35569 | 17784 |
| 51 | 37347 | 18674 |
| 52 | 39214 | 19607 |
| 53 | 41175 | 20588 |
| 54 | 43234 | 21617 |
| 55 | 45396 | 22698 |
| 56 | 47665 | 23833 |
| 57 | 50049 | 25024 |
| 58 | 52551 | 26276 |
| 59 | 55179 | 27589 |
| 60 | 57938 | 28969 |
| 61 | 60834 | 30417 |
| 62 | 63876 | 31938 |
| 63 | 67070 | 33535 |
| 64 | 70424 | 35212 |
| 65 | 73945 | 36972 |
| 66 | 77642 | 38821 |
| 67 | 81524 | 40762 |
| 68 | 85600 | 42800 |
| 69 | 89880 | 44940 |
| 70 | 94374 | 47187 |
| 71 | 99093 | 49546 |
| 72 | 104048 | 52024 |
| 73 | 109250 | 54625 |
| 74 | 114713 | 57356 |
| 75 | 120448 | 60224 |
| 76 | 126471 | 63235 |
| 77 | 132794 | 66397 |
| 78 | 139434 | 69717 |
| 79 | 146405 | 73203 |
| 80 | 153726 | 76863 |

## 5. Regenerating this folder

```sh
# the reference database (split zip, extracted outside Git)
cat Tools/clientdb.zip.001 Tools/clientdb.zip.002 > /tmp/clientdb.zip
unzip -o /tmp/clientdb.zip -d /tmp/sdb

# rewrite every document + CSV in this folder
python3 Tools/SdbDump/spawn_reference.py /tmp/sdb/clientdb.sd2

# options
python3 Tools/SdbDump/spawn_reference.py /tmp/sdb/clientdb.sd2 \
    --out-dir Docs/SpawnReference --kinds monster,turret --no-csv
```

The script needs nothing but Python 3 (no Firefall installation): it imports the `sdb_dump.py` decoder, harvests table/column names from PIN's own source and decrypts only the localized strings the spawnable rows reference, so a full run takes well under a minute.

## 6. CSV files

`csv/` holds the same rows with **every** column of the SDB record plus the resolved names - the actual spreadsheet, for filtering in Excel/LibreOffice or diffing between builds:

| file | kind | columns |
|---|---|---|
| [csv/mobs.csv](csv/mobs.csv) | `monster` | 82 |
| [csv/deployables.csv](csv/deployables.csv) | `deployable` | 61 |
| [csv/vehicles.csv](csv/vehicles.csv) | `vehicle` | 11 |
| [csv/carryables.csv](csv/carryables.csv) | `carryable` | 36 |
| [csv/turrets.csv](csv/turrets.csv) | `turret` | 22 |

Column order is `id`, then the resolved names, then every raw SDB column alphabetically. Values are rendered the same way as in the Markdown tables (vectors as `(x, y, z)`, integral floats without decimals).

## 7. Related documents

- [../STATIC_DATABASE.md](../STATIC_DATABASE.md) - the `.sd2` file format, PIN's coverage of it, and how `spawn`/`sdb`/`sdbinfo` are implemented.
- [../MOBS_AND_NPCS.md](../MOBS_AND_NPCS.md) - anatomy of a monster row, mobs grouped by faction, how PIN turns a row into an entity.
- [../SPAWNING_AND_COMBAT.md](../SPAWNING_AND_COMBAT.md) - replication, combat gating, per-zone `character_spawn.json`.
- [../CHARACTERS_AND_BATTLEFRAMES.md](../CHARACTERS_AND_BATTLEFRAMES.md) - the player side (`characters.json`).
- [../HEALTH_SYSTEM.md](../HEALTH_SYSTEM.md) - health, damage, death and respawn.
- [../../Tools/SdbDump/README.md](../../Tools/SdbDump/README.md) - the decoder these documents are generated with.
