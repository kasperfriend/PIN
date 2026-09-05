# Vehicles — full spawn reference

Every one of the **173** rows of `vcs::VehicleInfo` that PIN can spawn, with the exact command for each. All 173 of them are named, so every row can be spawned by name or id.

> **Generated file** - do not edit by hand. Regenerate with `python3 Tools/SdbDump/spawn_reference.py <clientdb.sd2>` (see [README.md](README.md#5-regenerating-this-folder)).

Decoded from Firefall build **prod-1962**. Index, faction table and CSV notes: [README.md](README.md). How the commands are implemented: [../STATIC_DATABASE.md](../STATIC_DATABASE.md#4-spawning-from-the-database-in-game).

---

## 1. Spawning one of these

```
\spawn vehicle <id|name> [<x> <y> <z>]  # chat command (note the backslash)
spawn vehicle <id|name> [<x> <y> <z>]   # Admin channel / server console
\sdb vehicle <filter> [limit]           # search this table in-game
\sdbinfo vehicle <id|name>              # every field of one row
```

- Kind aliases accepted in place of `vehicle`: `vehicle`, `vehicles`, `veh`.
- Spawn path: `EntityManager.SpawnVehicle(typeId, position, orientation, owner, autoMount: false)`.
- Older typed command: `vehicle <id> [<x> <y> <z>]` (admin channel only).
- Omit `<x> <y> <z>` and the entity spawns at your character's position with your orientation; from the server console a position is required.
- Names are matched case-insensitively (exact beats prefix beats substring) and do not need quoting, so multi-word names work: `\spawn vehicle Accord Dropship`.

Examples, built from the first rows of this table:

```
\spawn vehicle 13                # Accord Dropship - by id, at your feet
\spawn vehicle Accord Dropship   # the same row, by name
\spawn vehicle 26 -25.5 118 492  # Chosen Darkslip at an explicit position
\sdbinfo vehicle 13              # every field of that row
\sdb vehicle accord 20           # search this table in-game
```

## 2. Column reference

| column | meaning |
|---|---|
| `id` | `vcs::VehicleInfo.id` (a `ushort` - `SDBCatalog` casts it). |
| `name` | `localized_name_id` -> `dblocalization::LocalizedText`. Every vehicle row is named. |
| `class` | `vehicle_class` -> `vcs::VehicleClass.name`: HGV, LGV, Cargo, Dropship, Train, MGV, Battlecruiser. |
| `faction` | `faction_id` -> `dbcharacter::Faction.internal_name`; `-` means faction 0 (none). |
| `race` | Raw `race` byte from the VCS record (the vehicle's own race tag, not the monster census race). |
| `scaling` | `scaling_table_id`, the vehicle stat scaling set. |
| `spawn command` | The exact chat command. Drop the leading `\` for the Admin channel or the server console, and append `<x> <y> <z>` for an explicit position. |

The table in §5 is the readable subset. **Every** column of the SDB row - all 7 of them, plus the resolved names - is in [csv/vehicles.csv](csv/vehicles.csv).

## 3. Notes

- The vehicle is owned by the calling character when there is one, and is never auto-mounted - walk up to it and press the use key.
- Components (seats, turrets, weapons) come from the VCS component tables; `spawn` only creates the vehicle entity itself.

## 4. Breakdown

### By class

Rows per `vcs::VehicleClass`.

| class | rows | named |
|---|---|---|
| LGV | 74 | 74 |
| Dropship | 49 | 49 |
| MGV | 23 | 23 |
| HGV | 14 | 14 |
| Cargo | 8 | 8 |
| Battlecruiser | 4 | 4 |
| Train | 1 | 1 |

### By faction

Rows per faction.

| faction | rows | named |
|---|---|---|
| accord | 141 | 141 |
| chosen | 18 | 18 |
| - | 4 | 4 |
| Reapers | 3 | 3 |
| friendly | 3 | 3 |
| bandit | 2 | 2 |
| neutral | 2 | 2 |

## 5. All 173 rows

Sorted by id. `spawn command` is ready to copy into the chat window.

| id | name | class | faction | race | scaling | spawn command |
|---|---|---|---|---|---|---|
| 13 | Accord Dropship | Dropship | accord | 0 | 10003 | \spawn vehicle 13 |
| 26 | Chosen Darkslip | Dropship | chosen | 2 | 10003 | \spawn vehicle 26 |
| 28 | Cobra Cycle | LGV | accord | 0 | 10003 | \spawn vehicle 28 |
| 35 | Resonator Bomb | LGV | accord | 0 | 10003 | \spawn vehicle 35 |
| 36 | Locust Chopper | LGV | accord | 0 | 10003 | \spawn vehicle 36 |
| 37 | Triton Cycle | LGV | accord | 0 | 10003 | \spawn vehicle 37 |
| 38 | Courier Cycle | LGV | accord | 0 | 10003 | \spawn vehicle 38 |
| 41 | Chosen Cycle | LGV | - | 2 | 10003 | \spawn vehicle 41 |
| 43 | Vespa Cycle | LGV | accord | 0 | 10003 | \spawn vehicle 43 |
| 47 | Cobra P-39 | LGV | accord | 0 | 10003 | \spawn vehicle 47 |
| 48 | Terromoto Cobra Cycle | LGV | accord | 0 | 10003 | \spawn vehicle 48 |
| 49 | Oilspill's Dropship | Dropship | neutral | 11 | 10003 | \spawn vehicle 49 |
| 50 | Cobra K-12 | LGV | accord | 0 | 10003 | \spawn vehicle 50 |
| 51 | Oilspill's Dropship | Dropship | neutral | 11 | 10003 | \spawn vehicle 51 |
| 52 | Accord Dropship | Dropship | accord | 0 | 10003 | \spawn vehicle 52 |
| 53 | Thumper Cart | Cargo | accord | 0 | 10003 | \spawn vehicle 53 |
| 54 | Cobra R-54 | LGV | accord | 0 | 10003 | \spawn vehicle 54 |
| 56 | ThumperCart Thumper | LGV | accord | 0 | 10003 | \spawn vehicle 56 |
| 57 | ThumperCart Cart | LGV | friendly | 7 | 10003 | \spawn vehicle 57 |
| 59 | Omnidyne-M LGV | LGV | accord | 0 | 10003 | \spawn vehicle 59 |
| 60 | Holo Whale | LGV | accord | 0 | 10003 | \spawn vehicle 60 |
| 64 | Accord Black Ops Drop Ship | Dropship | accord | 0 | 10003 | \spawn vehicle 64 |
| 66 | Convoy | HGV | accord | 0 | 10003 | \spawn vehicle 66 |
| 71 | Jaguar K-17 MGV | MGV | accord | 0 | 10003 | \spawn vehicle 71 |
| 80 | Cobra Cycle | LGV | accord | 0 | 10003 | \spawn vehicle 80 |
| 81 | _Chosen Darkslip | Dropship | chosen | 2 | 10003 | \spawn vehicle 81 |
| 82 | Chosen Darkslip | Dropship | chosen | 2 | 10003 | \spawn vehicle 82 |
| 83 | Vapor Cycle | LGV | accord | 0 | 10003 | \spawn vehicle 83 |
| 86 | Oilspill's Dropship | Dropship | accord | 0 | 10003 | \spawn vehicle 86 |
| 87 | Oilspill's Dropship | Dropship | accord | 0 | 10003 | \spawn vehicle 87 |
| 88 | Vortex Cycle | LGV | accord | 0 | 10003 | \spawn vehicle 88 |
| 89 | Accord Armored Dropship | Dropship | accord | 0 | 10003 | \spawn vehicle 89 |
| 90 | U.A.S. Vanguard | Battlecruiser | accord | 0 | 10003 | \spawn vehicle 90 |
| 91 | Accord Armored Dropship | Dropship | accord | 0 | 10003 | \spawn vehicle 91 |
| 92 | Harbinger Shields | Dropship | chosen | 2 | 10003 | \spawn vehicle 92 |
| 93 | Bloodkings Dropship | Dropship | bandit | 10 | 10003 | \spawn vehicle 93 |
| 94 | Oilspill's Dropship | Dropship | accord | 0 | 10003 | \spawn vehicle 94 |
| 95 | Zephyr Cycle | LGV | accord | 0 | 10003 | \spawn vehicle 95 |
| 97 | Convoy Mobile AA | HGV | accord | 0 | 10003 | \spawn vehicle 97 |
| 99 | Chosen General Zod's Super Darkslip | Dropship | chosen | 2 | 10003 | \spawn vehicle 99 |
| 100 | Interceptor Assault | LGV | accord | 0 | 10003 | \spawn vehicle 100 |
| 101 | Chosen Darkslip | Dropship | chosen | 2 | 10003 | \spawn vehicle 101 |
| 102 | Arclight Missile | Dropship | accord | 0 | 10003 | \spawn vehicle 102 |
| 103 | Chosen Darkslip | Dropship | chosen | 2 | 10003 | \spawn vehicle 103 |
| 104 | Accord Gunship | Dropship | accord | 0 | 10003 | \spawn vehicle 104 |
| 105 | Accord Armored Dropship | Dropship | accord | 0 | 10003 | \spawn vehicle 105 |
| 106 | Chosen Darkslip | Dropship | chosen | 2 | 10003 | \spawn vehicle 106 |
| 107 | Lancer M3 | LGV | accord | 0 | 10003 | \spawn vehicle 107 |
| 108 | DH Armored Dropship | Dropship | accord | 0 | 10003 | \spawn vehicle 108 |
| 109 | Chosen Darkslip | Dropship | chosen | 2 | 10003 | \spawn vehicle 109 |
| 110 | Cobra P-1 | LGV | accord | 0 | 10003 | \spawn vehicle 110 |
| 111 | Accord Dropship | Dropship | accord | 0 | 10003 | \spawn vehicle 111 |
| 112 | Ranger All-Terrain MGV | MGV | accord | 0 | 10003 | \spawn vehicle 112 |
| 113 | Blitz Assault LGV | LGV | accord | 0 | 10003 | \spawn vehicle 113 |
| 114 | Convoy (NonDrivable) | HGV | accord | 0 | 10003 | \spawn vehicle 114 |
| 115 | U.A.S. Vanguard | Battlecruiser | accord | 0 | 10003 | \spawn vehicle 115 |
| 116 | Cobra XLR | LGV | accord | 0 | 10003 | \spawn vehicle 116 |
| 117 | Bumblebee | MGV | accord | 0 | 10003 | \spawn vehicle 117 |
| 118 | Wasteland Armored Dropship | Dropship | accord | 0 | 10003 | \spawn vehicle 118 |
| 119 | Thumper Cart | Cargo | accord | 0 | 10003 | \spawn vehicle 119 |
| 121 | BSU Test Vehicle | MGV | accord | 0 | 10003 | \spawn vehicle 121 |
| 122 | Convoy MGV | MGV | accord | 0 | 10003 | \spawn vehicle 122 |
| 123 | TEST Convoy MGV | MGV | accord | 0 | 10003 | \spawn vehicle 123 |
| 124 | Rental Cobra R-54 | LGV | accord | 0 | 10003 | \spawn vehicle 124 |
| 125 | Red Line Accord Dropship | Dropship | accord | 0 | 10003 | \spawn vehicle 125 |
| 126 | Blue Line Accord Dropship | Dropship | accord | 0 | 10003 | \spawn vehicle 126 |
| 127 | RX1 Resource Hauler | HGV | accord | 0 | 10003 | \spawn vehicle 127 |
| 128 | Cheetah Model S | LGV | accord | 0 | 10003 | \spawn vehicle 128 |
| 129 | Cobra Turbo LGV | LGV | accord | 0 | 10003 | \spawn vehicle 129 |
| 130 | Locust Turbo Chopper | LGV | accord | 0 | 10003 | \spawn vehicle 130 |
| 131 | Cobra Turbo P-39 | LGV | accord | 0 | 10003 | \spawn vehicle 131 |
| 132 | Terromoto Turbo Cobra | LGV | accord | 0 | 10003 | \spawn vehicle 132 |
| 133 | Cobra Turbo K-12 | LGV | accord | 0 | 10003 | \spawn vehicle 133 |
| 134 | Omnidyne-M Turbo | LGV | accord | 0 | 10003 | \spawn vehicle 134 |
| 135 | Cobra Turbo P-1 | LGV | accord | 0 | 10003 | \spawn vehicle 135 |
| 136 | Cobra Turbo XLR | LGV | accord | 0 | 10003 | \spawn vehicle 136 |
| 137 | Cobra Turbo R-54 | LGV | accord | 0 | 10003 | \spawn vehicle 137 |
| 138 | Vapor Turbo Cycle | LGV | accord | 0 | 10003 | \spawn vehicle 138 |
| 139 | Vortex Turbo Cycle | LGV | accord | 0 | 10003 | \spawn vehicle 139 |
| 140 | Zephyr Turbo Cycle | LGV | accord | 0 | 10003 | \spawn vehicle 140 |
| 141 | TEST Repulsor Convoy MGV | MGV | accord | 0 | 10003 | \spawn vehicle 141 |
| 142 | TEST Convoy MGV No Cargo | MGV | accord | 0 | 10003 | \spawn vehicle 142 |
| 143 | Chosen Darkslip | Dropship | chosen | 2 | 10003 | \spawn vehicle 143 |
| 144 | Accord Cargo Ship | Cargo | accord | 0 | 10003 | \spawn vehicle 144 |
| 145 | TEST Operation Vehicle | MGV | accord | 0 | 10003 | \spawn vehicle 145 |
| 146 | OLD Chosen Cycle | LGV | - | 2 | 10003 | \spawn vehicle 146 |
| 147 | Brahma Transport | Dropship | accord | 0 | 10003 | \spawn vehicle 147 |
| 148 | Chosen Sweeper | LGV | chosen | 2 | 10003 | \spawn vehicle 148 |
| 149 | Vanilla MGV | MGV | accord | 0 | 10003 | \spawn vehicle 149 |
| 150 | TS-RC8 "Snowsquall" MGV | MGV | accord | 0 | 10003 | \spawn vehicle 150 |
| 151 | Jaguar K-27 Turbo Assault Vehicle | MGV | accord | 0 | 10003 | \spawn vehicle 151 |
| 152 | RX2 Re-enforced Resource Hauler | HGV | accord | 0 | 10003 | \spawn vehicle 152 |
| 153 | Interceptor RX Turbo Assault | LGV | accord | 0 | 10003 | \spawn vehicle 153 |
| 154 | Blitz Assault Turbo LGV | LGV | accord | 0 | 10003 | \spawn vehicle 154 |
| 155 | Chosen Darkslip | Dropship | chosen | 2 | 10003 | \spawn vehicle 155 |
| 156 | Reaper Armored Dropship | Dropship | Reapers | 10 | 10003 | \spawn vehicle 156 |
| 157 | Surf Board | LGV | accord | 0 | 10003 | \spawn vehicle 157 |
| 159 | Reaper Armored Gunship | Dropship | Reapers | 10 | 10003 | \spawn vehicle 159 |
| 160 | Reaper APC - Old Test | HGV | bandit | 10 | 10003 | \spawn vehicle 160 |
| 161 | Weekly Convoy MGV | MGV | accord | 0 | 10003 | \spawn vehicle 161 |
| 162 | Chosen Cycle | LGV | - | 2 | 10003 | \spawn vehicle 162 |
| 163 | MGV_MoneyBomb_Base | MGV | accord | 0 | 10003 | \spawn vehicle 163 |
| 164 | MGV_MoneyBomb_VIP | MGV | accord | 0 | 10003 | \spawn vehicle 164 |
| 165 | Chosen Dropship | Dropship | chosen | 0 | 10003 | \spawn vehicle 165 |
| 167 | Mine Cart | Cargo | accord | 0 | 10003 | \spawn vehicle 167 |
| 168 | Repulsor Generator | Cargo | accord | 0 | 10003 | \spawn vehicle 168 |
| 169 | Wooden Barrel | Cargo | accord | 0 | 10003 | \spawn vehicle 169 |
| 170 | Test Derby LGV | LGV | accord | 0 | 10003 | \spawn vehicle 170 |
| 171 | Test Brontodon | LGV | accord | 0 | 10003 | \spawn vehicle 171 |
| 172 | Operation Test MGV | MGV | accord | 0 | 10003 | \spawn vehicle 172 |
| 173 | Test Brontodon - Flight Path | LGV | accord | 0 | 10003 | \spawn vehicle 173 |
| 174 | APC | HGV | accord | 0 | 10003 | \spawn vehicle 174 |
| 175 | Drivable APC | HGV | accord | 0 | 10003 | \spawn vehicle 175 |
| 176 | Reaper APC | HGV | Reapers | 0 | 10003 | \spawn vehicle 176 |
| 178 | Grasshopper K-18 | LGV | accord | 0 | 10003 | \spawn vehicle 178 |
| 179 | Lancer Gold | LGV | accord | 0 | 10003 | \spawn vehicle 179 |
| 180 | Elevator | Train | accord | 0 | 10003 | \spawn vehicle 180 |
| 181 | [PVP] TDM Respawner | Dropship | accord | 0 | 10003 | \spawn vehicle 181 |
| 182 | TransHub Ship | Battlecruiser | accord | 0 | 10003 | \spawn vehicle 182 |
| 183 | AA Turret Cart | LGV | friendly | 7 | 10003 | \spawn vehicle 183 |
| 184 | Agrievan | Cargo | chosen | 2 | 10003 | \spawn vehicle 184 |
| 185 | ARES-Team Transport | HGV | accord | 0 | 10003 | \spawn vehicle 185 |
| 186 | Abe's test LGV | LGV | accord | 0 | 10003 | \spawn vehicle 186 |
| 187 | Accord Supply Dropship | Dropship | accord | 0 | 10003 | \spawn vehicle 187 |
| 189 | Accord A-10 Mamba LGV | LGV | accord | 0 | 10003 | \spawn vehicle 189 |
| 190 | Communications Array | Cargo | accord | 0 | 10003 | \spawn vehicle 190 |
| 191 | ARES-Team Transport Non-Drivable | HGV | accord | 0 | 10003 | \spawn vehicle 191 |
| 192 | A_LGVCycle04 | LGV | accord | 0 | 10003 | \spawn vehicle 192 |
| 194 | Oilspill's Dropship | Dropship | accord | 0 | 10003 | \spawn vehicle 194 |
| 195 | MoonFestival LGV | LGV | accord | 0 | 10003 | \spawn vehicle 195 |
| 197 | Blazing Hope LGV | LGV | accord | 0 | 10003 | \spawn vehicle 197 |
| 198 | Blazing Hope MGV | MGV | accord | 0 | 10003 | \spawn vehicle 198 |
| 199 | Operation Test - Doesnt Work. | HGV | accord | 0 | 10003 | \spawn vehicle 199 |
| 200 | Mobile AA | HGV | accord | 10 | 10003 | \spawn vehicle 200 |
| 201 | A1 Hauler | HGV | accord | 0 | 10003 | \spawn vehicle 201 |
| 202 | _M05_Mi751_CM4_No_Exit - Accord Dropship | Dropship | accord | 0 | 10003 | \spawn vehicle 202 |
| 203 | Bsu_TestLGV | LGV | accord | 0 | 10003 | \spawn vehicle 203 |
| 204 | Infinite LGV | LGV | accord | 0 | 10003 | \spawn vehicle 204 |
| 205 | Forester MGV | MGV | accord | 0 | 10003 | \spawn vehicle 205 |
| 206 | Fury Monster MGV | MGV | accord | 0 | 10003 | \spawn vehicle 206 |
| 207 | Tarantula LGV | LGV | accord | 0 | 10003 | \spawn vehicle 207 |
| 208 | Accord Dropship | Dropship | accord | 0 | 10003 | \spawn vehicle 208 |
| 209 | Phoenix LGV | LGV | accord | 0 | 10003 | \spawn vehicle 209 |
| 210 | Sonic Wave LGV | LGV | accord | 0 | 10003 | \spawn vehicle 210 |
| 211 | Death Rose LGV | LGV | accord | 0 | 10003 | \spawn vehicle 211 |
| 212 | Wood Pecker LGV | LGV | accord | 0 | 10003 | \spawn vehicle 212 |
| 213 | Celebrity MGV | MGV | accord | 0 | 10003 | \spawn vehicle 213 |
| 214 | Rising Star LGV | LGV | accord | 0 | 10003 | \spawn vehicle 214 |
| 215 | Top Dog LGV | LGV | accord | 0 | 10003 | \spawn vehicle 215 |
| 216 | Ace LGV | LGV | accord | 0 | 10003 | \spawn vehicle 216 |
| 218 | Omnidyne-M Flagship MGV | MGV | accord | 0 | 10003 | \spawn vehicle 218 |
| 219 | Hummingbird Racer | LGV | accord | 0 | 10003 | \spawn vehicle 219 |
| 220 | Mosquito Racer | LGV | accord | 0 | 10003 | \spawn vehicle 220 |
| 221 | Kestrel Racer | LGV | accord | 0 | 10003 | \spawn vehicle 221 |
| 222 | Black Widow Racer | LGV | accord | 0 | 10003 | \spawn vehicle 222 |
| 223 | Cherub LGV | LGV | accord | 0 | 10003 | \spawn vehicle 223 |
| 224 | Blue Kestrel (level 50 epic mgv) | MGV | accord | 0 | 10003 | \spawn vehicle 224 |
| 225 | [TEST] jwoe - DT gunship | Dropship | accord | 7 | 10003 | \spawn vehicle 225 |
| 226 | Accord Disintegrator Gunship | Dropship | accord | 7 | 10003 | \spawn vehicle 226 |
| 227 | Chosen Darkslip | Dropship | chosen | 7 | 10003 | \spawn vehicle 227 |
| 228 | Devil's Tusk Zone Event - Accord Dropship | Dropship | accord | 7 | 10003 | \spawn vehicle 228 |
| 229 | [Raid] Trolly | LGV | friendly | 7 | 10003 | \spawn vehicle 229 |
| 230 | Prototype Assault Gunship | Dropship | accord | 10 | 10003 | \spawn vehicle 230 |
| 231 | Chosen Cycle | LGV | - | 2 | 10003 | \spawn vehicle 231 |
| 232 | Chosen Raptor | LGV | chosen | 8 | 10003 | \spawn vehicle 232 |
| 233 | Accord Liberator Class Dropship | Dropship | accord | 7 | 10003 | \spawn vehicle 233 |
| 234 | U.A.S. Victory | Battlecruiser | accord | 0 | 10003 | \spawn vehicle 234 |
| 235 | Chosen Gunship (level 50 epic) | Dropship | chosen | 8 | 10003 | \spawn vehicle 235 |
| 335 | Accord Player Fighter | Dropship | accord | 0 | 10003 | \spawn vehicle 335 |
| 336 | Accord Armored Dropship | Dropship | accord | 0 | 10003 | \spawn vehicle 336 |
| 337 | Accord Medivac Dropship | Dropship | accord | 7 | 10003 | \spawn vehicle 337 |
| 338 | Chosen Raptor | LGV | chosen | 8 | 10003 | \spawn vehicle 338 |
| 339 | Copy of Jaguar K-17 MGV TESTING | MGV | accord | 0 | 10003 | \spawn vehicle 339 |

---

Regenerate: `python3 Tools/SdbDump/spawn_reference.py <clientdb.sd2>` - see [README.md](README.md). Related: [../MOBS_AND_NPCS.md](../MOBS_AND_NPCS.md) (mobs grouped by faction, with the anatomy of a monster row), [../SPAWNING_AND_COMBAT.md](../SPAWNING_AND_COMBAT.md) (what happens after the spawn), [../STATIC_DATABASE.md](../STATIC_DATABASE.md) (the file format and the commands).
