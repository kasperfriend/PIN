# Turrets — full spawn reference

Every one of the **107** rows of `dbcharacter::Turret` that PIN can spawn, with the exact command for each. All 107 of them are named, so every row can be spawned by name or id.

> **Generated file** - do not edit by hand. Regenerate with `python3 Tools/SdbDump/spawn_reference.py <clientdb.sd2>` (see [README.md](README.md#5-regenerating-this-folder)).

Decoded from Firefall build **prod-1962**. Index, faction table and CSV notes: [README.md](README.md). How the commands are implemented: [../STATIC_DATABASE.md](../STATIC_DATABASE.md#4-spawning-from-the-database-in-game).

---

## 1. Spawning one of these

```
\spawn turret <id|name>       # chat command (note the backslash)
spawn turret <id|name>        # Admin channel (needs a player character)
\sdb turret <filter> [limit]  # search this table in-game
\sdbinfo turret <id|name>     # every field of one row
```

- Kind aliases accepted in place of `turret`: `turret`, `turrets`.
- Spawn path: `EntityManager.SpawnTurret(typeId, parent)` - child entity, needs a parent.
- Older typed command: none - `spawn turret` is the only way to create one from a command.
- Turrets take no position: they are child entities and always attach to the calling player's character, which is also why the server console cannot spawn one.
- Names are matched case-insensitively (exact beats prefix beats substring) and do not need quoting, so multi-word names work: `\spawn turret Minigun Turret`.

Examples, built from the first rows of this table:

```
\spawn turret 1                  # Minigun Turret - by id
\spawn turret Minigun Turret     # the same row, by name
\spawn turret Mounted Turret 01  # Mounted Turret 01 - by name, attaches to you
\sdbinfo turret 1                # every field of that row
\sdb turret minigun 20           # search this table in-game
```

## 2. Column reference

| column | meaning |
|---|---|
| `id` | `dbcharacter::Turret.id` - the turret type id. |
| `name` | Plain text `name` column (turrets are the one spawnable table that is not localized). |
| `posture` | `posture` byte sent to the client as the gunner posture (2 = standing for most rows). |
| `attack type` | `attack_type` byte (1 for nearly every row). |
| `behavior` | CAIS behavior set name; some rows store a numeric id here. |
| `pitch` | `min_pitch` .. `max_pitch` in **radians** (`1.5708` = 90 deg); `-1` is the 'no limit set' marker in this table. |
| `yaw` | `min_yaw` .. `max_yaw` in **radians** (`6.2832` = full 360 deg traversal). |
| `weapons` | `dbcharacter::TurretWeapon.weapon_id` rows for this turret; `weapon_names` in the CSV. |
| `spawn command` | The exact chat command. Turrets attach to the calling player's character, so this one is refused from the server console. |

The table in §5 is the readable subset. **Every** column of the SDB row - all 17 of them, plus the resolved names - is in [csv/turrets.csv](csv/turrets.csv).

## 3. Notes

- Turrets are **child** entities: `spawn turret` attaches the turret to the calling player's character and is refused when there is no character (server console).
- Deployables and vehicles reference turrets through `turret_type`; spawning those creates the turret automatically.

## 4. Breakdown

### By posture

Rows per `posture` byte.

| posture | rows | named |
|---|---|---|
| 2 | 93 | 93 |
| 0 | 6 | 6 |
| 5 | 4 | 4 |
| 3 | 3 | 3 |
| 1 | 1 | 1 |

### By attack type

Rows per `attack_type` byte.

| attack type | rows | named |
|---|---|---|
| 1 | 101 | 101 |
| 0 | 5 | 5 |
| 3 | 1 | 1 |

## 5. All 107 rows

Sorted by id. `spawn command` is ready to copy into the chat window.

| id | name | posture | attack type | behavior | pitch | yaw | weapons | spawn command |
|---|---|---|---|---|---|---|---|---|
| 1 | Minigun Turret | 2 | 1 | 1 | 0 .. -1 | 0 .. -1 | 30905 | \spawn turret 1 |
| 2 | Mounted Turret 01 | 2 | 1 | 1 | -0.3927 .. 1.57 | 0 .. -1 | 30045 | \spawn turret 2 |
| 3 | Quad Cannon | 2 | 1 | - | 0 .. -0.0174533 | 0 .. -0.0174533 | 85954 | \spawn turret 3 |
| 4 | Tank Turret | 2 | 1 | 1 | -0.3927 .. 1.1781 | -0.7854 .. 0.7854 | 30045 | \spawn turret 4 |
| 5 | Mounted Turret 02 | 2 | 1 | 1 | 0.195 .. 1.57 | 0 .. -1 | 33782 | \spawn turret 5 |
| 6 | Dropship Turret | 2 | 1 | 1 | -1.57 .. 0 | 0 .. -1 | 32848 | \spawn turret 6 |
| 7 | Engineer Turret - I | 2 | 1 | - | 0 .. -1 | 0 .. -1 | 20038 | \spawn turret 7 |
| 8 | Engineer Turret - II | 2 | 1 | - | 0 .. -1 | 0 .. -1 | 30767 | \spawn turret 8 |
| 9 | Dropship MiniTurret | 2 | 1 | 1 | -1.57 .. 0 | 0 .. -1 | 33979 | \spawn turret 9 |
| 10 | Tech Rocket Turret | 2 | 1 | - | 0 .. -1 | 0 .. -1 | 33961 | \spawn turret 10 |
| 11 | Chosen Guard Tower | 2 | 1 | 1 | -0.17 .. 1.57 | 0 .. -1 | 34475 | \spawn turret 11 |
| 12 | Multi Turret - I | 2 | 1 | - | 0 .. -1 | 0 .. -1 | 75622 | \spawn turret 12 |
| 13 | Tech Flamethrower Turret | 2 | 1 | - | 0 .. 6.28319 | 0 .. 6.28319 | 75682 | \spawn turret 13 |
| 14 | Tech AA Rocket Turret | 2 | 1 | - | 0 .. -1 | 0 .. -1 | 75686 | \spawn turret 14 |
| 15 | Tech Riot Gun Turret | 2 | 1 | - | 0 .. 6.28319 | 0 .. 6.28319 | 75892 | \spawn turret 15 |
| 16 | Hostile NPC Turret | 2 | 1 | - | 0 .. -1 | 0 .. -1 | 75453 | \spawn turret 16 |
| 17 | Chosen Pillar Turret | 2 | 1 | - | -0.17 .. 1.57 | 0 .. -1.00007 | 77214 | \spawn turret 17 |
| 18 | Chosen Artillery Turret | 0 | 1 | - | 0.610865 .. 0.959931 | 0 .. 0 | 77723 | \spawn turret 18 |
| 19 | Heavy Turret I (Engi Ability) | 2 | 1 | - | 0 .. -1 | 0 .. -1 | 77778 | \spawn turret 19 |
| 20 | Heavy Turret II | 2 | 1 | - | 0 .. -1 | 0 .. -1 | 77779 | \spawn turret 20 |
| 21 | UNUSED | 2 | 1 | - | 0 .. -1 | 0 .. -1 | 116561, 77779 | \spawn turret 21 |
| 22 | Engineer Anti-Personnel Turret | 2 | 1 | - | -0.610865 .. 0.959934 | -6.28319 .. 6.28319 | 78011 | \spawn turret 22 |
| 23 | Chosen Watchtower Turret | 2 | 1 | - | -0.17 .. 1.57 | 0 .. -1 | 77214 | \spawn turret 23 |
| 24 | Tanken Turret I | 2 | 1 | - | 0 .. -1 | 0 .. -1 | 85421 | \spawn turret 24 |
| 25 | H.A.W.K. Turret | 2 | 1 | - | -0.3927 .. 1.57 | 0 .. -1 | 85429 | \spawn turret 25 |
| 26 | Heavy Turret II (Rockets) | 2 | 1 | - | 0 .. -1 | 0 .. -1 | 85640, 77779 | \spawn turret 26 |
| 27 | DEBUG chen's test turret | 2 | 1 | - | 0 .. -1 | 0 .. -1 | 85643 | \spawn turret 27 |
| 28 | Outpost Heavy Turret I | 2 | 1 | - | 0 .. -1 | 0 .. -1 | 85731 | \spawn turret 28 |
| 128 | Chosen Heavy Turret | 2 | 1 | - | 0.195477 .. 1.5708 | 0 .. -1 | 92796 | \spawn turret 128 |
| 129 | Chosen Heavy Thumper Turret | 2 | 1 | - | -0.17 .. 1.57 | 0 .. -1 | 86341 | \spawn turret 129 |
| 130 | DiamondHead Warfront Darkslip Mi | 2 | 1 | - | -1.57 .. 0 | 0 .. -1 | 86491 | \spawn turret 130 |
| 131 | DiamondHead Warfront Darkslip Ma | 2 | 1 | - | -1.57 .. 0 | 0 .. -1 | 86492 | \spawn turret 131 |
| 132 | MGV Miniguns | 5 | 1 | - | -0.20944 .. 0.523599 | 0 .. -0.0174533 | 30905 | \spawn turret 132 |
| 133 | NPE Precursor Turret | 2 | 1 | - | 0 .. -1 | 0 .. -1 | 86508 | \spawn turret 133 |
| 134 | LGV 1x Minigun | 2 | 1 | - | -0.698132 .. 0.349066 | -0.523599 .. 0.523599 | 95145 | \spawn turret 134 |
| 135 | LGV missiles | 2 | 1 | - | 0 .. 0 | 0 .. 0 | 96473 | \spawn turret 135 |
| 136 | Accord BattleCruiser MiniTurret | 2 | 1 | - | -0.872665 .. 0.872665 | 0 .. -1 | 86534 | \spawn turret 136 |
| 137 | Accord Battlecruiser LargeTurret | 2 | 1 | - | 0 .. 1.5708 | 0 .. -1 | 86533 | \spawn turret 137 |
| 138 | LGV Machine Gun | 0 | 1 | - | -0.122173 .. 0.122173 | -0.785398 .. 0.785398 | 95145 | \spawn turret 138 |
| 139 | Deprecated - Please Reuse This I | 0 | 1 | - | 0.0174533 .. 0.174533 | -0.785398 .. 0.785398 | - | \spawn turret 139 |
| 140 | Omnidyn-m Heavy Turret | 2 | 1 | - | 0 .. -1 | 0 .. -1 | 96716 | \spawn turret 140 |
| 141 | Chosen Heavy Turret 2 - Thumper | 2 | 1 | - | 0.195477 .. 1.5708 | 0 .. -1 | 114621 | \spawn turret 141 |
| 142 | Heavy Turret I (Rocket) | 2 | 1 | - | 0 .. -1 | 0 .. -1 | 117720 | \spawn turret 142 |
| 143 | Bloodkings Turret | 2 | 1 | - | 0 .. -1 | 0 .. -1 | 118138 | \spawn turret 143 |
| 144 | Operation Mounted Turret 01 | 2 | 1 | - | -0.610865 .. 1.5708 | 0 .. -1 | 118602 | \spawn turret 144 |
| 145 | Operation Mining Turret | 2 | 1 | - | -0.698132 .. 0.959934 | -2.18166 .. 2.18166 | 118688 | \spawn turret 145 |
| 146 | Chosen Mortar | 0 | 1 | - | 0.610865 .. 0.959931 | 0 .. 0 | 118768 | \spawn turret 146 |
| 147 | [OWPVP] Anti Personnel Turret | 2 | 1 | - | 0 .. 6.28319 | -6.28319 .. 6.28319 | 77779 | \spawn turret 147 |
| 148 | [OWPVP] Anti Air EMP Turret | 2 | 1 | - | 0 .. -1 | 0 .. -1 | 118952 | \spawn turret 148 |
| 149 | [OWPVP] Anti Vehicle Turret | 2 | 1 | - | 0 .. -1 | 0 .. -1 | 118947 | \spawn turret 149 |
| 150 | TEST Chosen Transport Gun | 0 | 1 | - | 0.0872665 .. 0.174533 | 0 .. 0 | 114621 | \spawn turret 150 |
| 151 | Chosen Warzone Mounted Turret | 2 | 1 | - | -0.698132 .. 1.57 | 0 .. -1 | 119908 | \spawn turret 151 |
| 152 | NotBeingUsed | 2 | 1 | - | 0.195 .. 1.57 | 0 .. -1 | - | \spawn turret 152 |
| 153 | Operation Beam Turret | 2 | 1 | - | -0.698132 .. 0.959934 | -2.18166 .. 2.18166 | 120507 | \spawn turret 153 |
| 154 | Chosen Warzone Chosen Turret | 2 | 1 | - | -0.698132 .. 1.57 | 0 .. -1 | 120565 | \spawn turret 154 |
| 155 | Chosen Turret Core Mission 3 | 2 | 1 | - | -0.392699 .. 1.5708 | 0 .. -1.00007 | 120692 | \spawn turret 155 |
| 156 | Tower Defense Tech Flamethrower | 2 | 1 | - | 0 .. 6.28319 | 0 .. 6.28319 | 120971 | \spawn turret 156 |
| 157 | Tower Defense Tech AA Rocket Tur | 2 | 1 | - | 0 .. -1.309 | 0 .. -1.00007 | 120975 | \spawn turret 157 |
| 158 | Tower Defense Tech Sniper Turret | 2 | 1 | - | 0 .. 6.28319 | 0 .. 6.28319 | 123206 | \spawn turret 158 |
| 159 | Tower Defense Chemical Sprayer T | 2 | 1 | - | 0 .. 6.28319 | 0 .. 6.28319 | 120994 | \spawn turret 159 |
| 160 | [OWPVP] Anti Vehicle Turret Upgr | 2 | 1 | - | 0 .. -1 | 0 .. -1 | - | \spawn turret 160 |
| 161 | [OWPVP] Anti Personnel Turret U | 2 | 0 | - | 0 .. -1 | 0 .. -1 | - | \spawn turret 161 |
| 162 | Heavy Chosen Pillar Turret | 2 | 1 | - | -0.17 .. 1.57 | 0 .. -1.00007 | 122560 | \spawn turret 162 |
| 163 | Heavy Chosen Watchtower Turret | 2 | 1 | - | -0.17 .. 1.57 | 0 .. -1 | 122560 | \spawn turret 163 |
| 164 | Reaper Manable Turret | 2 | 1 | - | -0.610865 .. 0.959934 | -2.18166 .. 2.18166 | 78011 | \spawn turret 164 |
| 165 | Crystite Aranha Turret | 2 | 1 | - | -1.5708 .. 1.5708 | 0 .. 0 | 122891 | \spawn turret 165 |
| 166 | Sniper Mounted Turret 01 | 2 | 1 | - | -0.610865 .. 1.5708 | 0 .. -1.00007 | 114057 | \spawn turret 166 |
| 167 | Tower Defense Tech Riot Cryo Tur | 2 | 1 | - | 0 .. 6.28319 | 0 .. 6.28319 | 122962 | \spawn turret 167 |
| 168 | NPE - Mushroom Rock Turret | 2 | 1 | - | -0.17 .. 1.57 | 0 .. -1 | 123415 | \spawn turret 168 |
| 169 | [UNUSED] | 2 | 1 | - | 0 .. -1 | 0 .. -1 | 123443 | \spawn turret 169 |
| 170 | Heavy Turret | 2 | 1 | - | 0 .. -1 | 0 .. -1 | 123443 | \spawn turret 170 |
| 171 | Chosen Heavy Turret | 2 | 1 | - | 0 .. -1.00007 | 0 .. -1.00007 | 123445 | \spawn turret 171 |
| 172 | CM5 Engineer Turret - I | 2 | 1 | - | 0 .. -1 | 0 .. -1.00007 | 123446 | \spawn turret 172 |
| 173 | Chosen Light Turret | 2 | 1 | - | -0.17 .. 1.57 | 0 .. -1 | 123448 | \spawn turret 173 |
| 174 | Tower Defense Heavy Turret | 2 | 1 | - | 0 .. -1 | 0 .. -1 | 124322 | \spawn turret 174 |
| 175 | Sniper Turret (Engi Ability) | 2 | 1 | - | 0 .. -1 | 0 .. -1 | 124494 | \spawn turret 175 |
| 176 | Operation 002 - Reaper Gunship M | 2 | 1 | - | -1.57 .. 0 | 0 .. -1 | 124318 | \spawn turret 176 |
| 177 | Core Mission 05 Flamethrow [CM5] | 2 | 1 | - | 0 .. 6.28319 | 0 .. 6.28319 | 124320 | \spawn turret 177 |
| 178 | Chosen LGV Weapon Core Mission 3 | 2 | 1 | - | 0 .. 0 | 0 .. 0 | 113708 | \spawn turret 178 |
| 179 | Missile Command Turret | 2 | 1 | - | -0.698132 .. 0.959934 | -2.18166 .. 2.18166 | 120628 | \spawn turret 179 |
| 180 | Defense of Dredge AA Turret | 2 | 0 | - | 0.195 .. 1.57 | 0 .. -1 | 139388 | \spawn turret 180 |
| 181 | Defense of Dredge Defense Turret | 2 | 1 | - | 0.195 .. 1.57 | 0 .. -1 | 139389 | \spawn turret 181 |
| 182 | Gate Crasher - Anti-Air Turret | 2 | 1 | - | -0.392699 .. 1.5708 | 0 .. -1.00007 | 139393 | \spawn turret 182 |
| 183 | Operation Boss Beam Turret | 2 | 1 | - | -0.0174533 .. 0.0174533 | -0.0174533 .. 0.0174533 | 33827 | \spawn turret 183 |
| 184 | [DoD] Defense of Dredge Defense | 2 | 1 | - | 0.195 .. 1.57 | 0 .. -1 | 139389 | \spawn turret 184 |
| 185 | TEST Fog Finger | 2 | 1 | - | 0 .. 6.28319 | 0 .. 6.28319 | 139328 | \spawn turret 185 |
| 186 | Operation Tesla Beam Turret - El | 2 | 1 | - | -0.698132 .. 0.959934 | -2.18166 .. 2.18166 | 139839 | \spawn turret 186 |
| 187 | NPC Multi Turret | 2 | 1 | - | 0 .. -1 | 0 .. -1 | 140774 | \spawn turret 187 |
| 188 | Operation 003 - Final Boss Beam | 2 | 1 | - | -1.5708 .. 1.5708 | -3.14159 .. 3.14159 | 33827 | \spawn turret 188 |
| 189 | MGV missiles | 2 | 1 | - | 0 .. 0 | 0 .. 0 | 141848 | \spawn turret 189 |
| 190 | [TEST] jwoe - Ship Turret Type | 5 | 3 | - | 0 .. -0.785398 | 0 .. -0.785398 | 141900 | \spawn turret 190 |
| 191 | Melding Nano Missile Turret | 1 | 1 | - | 0 .. 6.28319 | 0 .. 6.28319 | 141974 | \spawn turret 191 |
| 192 | [DTZE] Anti-Air Flak | 5 | 1 | - | 0.226893 .. 1.5708 | 0 .. -0.0174533 | 141966 | \spawn turret 192 |
| 193 | [ZONE EVENT] DT Darkslip Shield | 2 | 0 | - | 0 .. -0.174533 | 0 .. -0.174533 | 141967 | \spawn turret 193 |
| 194 | Devils' Tusk Chosen Mortar | 0 | 1 | - | 0.610865 .. 0.959931 | 0 .. 0 | 142001 | \spawn turret 194 |
| 195 | [ZONE EVENT] DT Accord Gunship R | 3 | 1 | - | -1.5708 .. 0 | -0.349066 .. 2.96706 | 142270 | \spawn turret 195 |
| 196 | [DT Zone Event] Test rocket turr | 2 | 0 | - | -0.349066 .. 0.349066 | -0.0872665 .. 0.0872665 | 142148 | \spawn turret 196 |
| 197 | [ZONE EVENT] DT Accord Gunship L | 3 | 0 | - | -1.5708 .. -0.174533 | -2.96706 .. 0.349066 | 142270 | \spawn turret 197 |
| 198 | [ZONE EVENT] DT Accord Gunship M | 3 | 1 | - | -1.5708 .. 0 | 0 .. -0.0174533 | 142050 | \spawn turret 198 |
| 199 | Prototype Gunship Turret (Bottom | 2 | 1 | - | -1.5708 .. 0.174533 | 3.05433 .. 0.349066 | 142372 | \spawn turret 199 |
| 200 | Prototype Gunship Turret (Top) | 2 | 1 | - | -0.174533 .. 1.5708 | 3.05433 .. 0.349066 | 142372 | \spawn turret 200 |
| 201 | Leviathan Swivel Turret | 2 | 1 | - | -0.392699 .. 1.5708 | 0 .. -1.00007 | 142642 | \spawn turret 201 |
| 202 | Prototype Leviathan Turret | 2 | 1 | - | 0 .. 1.5708 | 0 .. -1.00007 | 142643 | \spawn turret 202 |
| 203 | [DTZE] Anti-Air Flak #2 - More P | 5 | 1 | - | 0.139626 .. 1.5708 | 0 .. -0.0174533 | 141966 | \spawn turret 203 |
| 204 | Accord Fighter Turret | 2 | 1 | - | -1.5708 .. 0 | 0 .. -1.00007 | 142372 | \spawn turret 204 |
| 205 | obsolete | 2 | 1 | - | -0.610865 .. 0.959934 | -6.28319 .. 6.28319 | 143180 | \spawn turret 205 |
| 206 | Chosen Medium Thumper Turret | 2 | 1 | - | 0.195477 .. 1.5708 | 0 .. -1 | 144086 | \spawn turret 206 |

---

Regenerate: `python3 Tools/SdbDump/spawn_reference.py <clientdb.sd2>` - see [README.md](README.md). Related: [../MOBS_AND_NPCS.md](../MOBS_AND_NPCS.md) (mobs grouped by faction, with the anatomy of a monster row), [../SPAWNING_AND_COMBAT.md](../SPAWNING_AND_COMBAT.md) (what happens after the spawn), [../STATIC_DATABASE.md](../STATIC_DATABASE.md) (the file format and the commands).
