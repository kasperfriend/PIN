# Deployables — full spawn reference

Every one of the **3,902** rows of `dbcharacter::Deployable` that PIN can spawn, with the exact command for each. 2,088 of them have an English name and can be spawned by name or id; the 1,814 unnamed rows are real and spawnable, but can only be referenced by id.

> **Generated file** - do not edit by hand. Regenerate with `python3 Tools/SdbDump/spawn_reference.py <clientdb.sd2>` (see [README.md](README.md#5-regenerating-this-folder)).

Decoded from Firefall build **prod-1962**. Index, faction table and CSV notes: [README.md](README.md). How the commands are implemented: [../STATIC_DATABASE.md](../STATIC_DATABASE.md#4-spawning-from-the-database-in-game).

---

## 1. Spawning one of these

```
\spawn deployable <id|name> [<x> <y> <z>]  # chat command (note the backslash)
spawn deployable <id|name> [<x> <y> <z>]   # Admin channel / server console
\sdb deployable <filter> [limit]           # search this table in-game
\sdbinfo deployable <id|name>              # every field of one row
```

- Kind aliases accepted in place of `deployable`: `deployable`, `deployables`, `dep`.
- Spawn path: `EntityManager.SpawnDeployable(typeId, position, orientation)`.
- Older typed command: `deployable <id> [<x> <y> <z>]` (admin channel only).
- Omit `<x> <y> <z>` and the entity spawns at your character's position with your orientation; from the server console a position is required.
- Names are matched case-insensitively (exact beats prefix beats substring) and do not need quoting, so multi-word names work: `\spawn deployable Battleframe Station`.

Examples, built from the first rows of this table:

```
\spawn deployable 1                    # Battleframe Station - by id, at your feet
\spawn deployable Battleframe Station  # the same row, by name
\spawn deployable 4 -25.5 118 492      # Forcefield I at an explicit position
\sdbinfo deployable 1                  # every field of that row
\sdb deployable battleframe 20         # search this table in-game
```

## 2. Column reference

| column | meaning |
|---|---|
| `id` | `dbcharacter::Deployable.id` - the deployable type id. |
| `name` | `localized_name_id` -> `dblocalization::LocalizedText`; `-` means the row has no English text and can only be spawned by id. |
| `category` | `deployable_category` -> `dbcharacter::DeployableCategory.name` (Turret, Shield, Repair Station, Spawner, ...). |
| `function` | `function` -> `dbcharacter::DeployableFunction.name`: what the deployable is for in AI terms (Fixed Weapon, Ammo, Cover, Driver Seat, ...). |
| `faction` | `default_faction` -> `dbcharacter::Faction.internal_name`. `spawn` creates deployables unowned with this faction. |
| `health` | `standard_health`. `start hp` is `start_hitpoints`, the value the entity actually starts with. |
| `scale` | `scale`, the model scale multiplier. |
| `build ms` | `build_time_ms`, how long the build/calldown takes. |
| `spawn command` | The exact chat command. Drop the leading `\` for the Admin channel or the server console, and append `<x> <y> <z>` for an explicit position. |

The table in §5 is the readable subset. **Every** column of the SDB row - all 53 of them, plus the resolved names - is in [csv/deployables.csv](csv/deployables.csv).

## 3. Notes

- Spawned unowned, with the row's `default_faction`; pass an owner through `EntityManager.SpawnDeployable` in code if you need one.
- Rows with `turret_type != 0` also spawn that `dbcharacter::Turret` as a child (see [TURRETS.md](TURRETS.md)).
- This is the largest spawnable table and it was never catalogued before: it contains every player-built structure, thumper, watchtower, seat, terminal, prop and test object in the game.

## 4. Breakdown

### By category

Rows per `dbcharacter::DeployableCategory`.

| category | rows | named |
|---|---|---|
| None | 3,238 | 1,693 |
| Turret | 137 | 62 |
| Mine | 80 | 52 |
| Spawner | 58 | 38 |
| Shield | 49 | 33 |
| Glider pad | 38 | 31 |
| Datapads | 37 | 22 |
| Mannable Turret | 29 | 13 |
| Forge | 22 | 16 |
| Repair Station | 21 | 13 |
| Consumer Fireworks | 20 | 17 |
| Surface Deposit | 20 | 9 |
| Anti-Personnel Turret | 19 | 12 |
| Generic Terminal | 17 | 9 |
| Tech SIN | 17 | 13 |
| SIN Tower | 16 | 8 |
| Tech Turret | 12 | 5 |
| Loadout Station | 10 | 5 |
| Manufacturing Terminal | 10 | 10 |
| Arcporter Pylon | 8 | 5 |
| Charged Pulse | 7 | 1 |
| Power Cell Dispenser | 7 | 3 |
| Adventurer's Glider Pad | 4 | 1 |
| Arcporter | 4 | 2 |
| Vending Machine | 4 | 3 |
| Multi-Turret | 3 | 3 |
| Sentinel Pod | 3 | 2 |
| FinishLine | 2 | 2 |
| Medium Thumper | 2 | 1 |
| PVP Terminal | 2 | 0 |
| Power Cell Receptacle | 2 | 0 |
| Army Terminal | 1 | 1 |
| Jump pad | 1 | 1 |
| New You | 1 | 1 |
| Rechargable Jump Pad | 1 | 1 |

### By function

Rows per `dbcharacter::DeployableFunction`.

| function | rows | named |
|---|---|---|
| Undefined | 2,942 | 1,587 |
| Fixed Weapon | 210 | 89 |
| Deployed Target | 180 | 94 |
| Target (primary) | 148 | 84 |
| Interactable Objective | 127 | 86 |
| Target (secondary) | 61 | 45 |
| Target (tertiary) | 44 | 24 |
| Rest | 41 | 13 |
| Play | 39 | 23 |
| Healing | 38 | 30 |
| Work | 17 | 4 |
| Rummage | 10 | 3 |
| Repair Work | 9 | 1 |
| Market | 7 | 1 |
| Bar | 5 | 0 |
| Recreation | 5 | 1 |
| Guard Stance | 4 | 1 |
| Line Rest 1 | 3 | 0 |
| Medical Aid | 3 | 0 |
| Cover | 2 | 1 |
| Passenger Seat | 2 | 0 |
| Deployed Shield | 1 | 1 |
| Driver Seat | 1 | 0 |
| Line Rest 2 | 1 | 0 |
| Loot | 1 | 0 |
| Weapon Rack | 1 | 0 |

### By faction

Rows per default faction.

| faction | rows | named |
|---|---|---|
| accord | 2,638 | 1,381 |
| - | 474 | 288 |
| chosen | 274 | 148 |
| neutral | 160 | 97 |
| friendly | 112 | 61 |
| gaea | 92 | 34 |
| melding | 54 | 32 |
| bandit | 39 | 18 |
| Rebels | 34 | 18 |
| Reapers | 11 | 7 |
| monster | 7 | 2 |
| Black Hills Bandits | 3 | 1 |
| Ophanim | 3 | 1 |
| Tanken | 1 | 0 |

## 5. All 3,902 rows

Sorted by id. `spawn command` is ready to copy into the chat window.

| id | name | category | function | faction | health | start hp | scale | build ms | spawn command |
|---|---|---|---|---|---|---|---|---|---|
| 1 | Battleframe Station | Loadout Station | Undefined | accord | 0 | 0 | 1 | 0 | \spawn deployable 1 |
| 2 | TEST Vending Machine | Vending Machine | Undefined | accord | 0 | 1 | 2 | 0 | \spawn deployable 2 |
| 4 | Forcefield I | Shield | Deployed Target | accord | 5 | 200 | 1 | 2000 | \spawn deployable 4 |
| 5 | Explosive Barrel | None | Undefined | accord | 1 | 300 | 1 | 0 | \spawn deployable 5 |
| 8 | - | None | Undefined | accord | 0 | 1 | 1 | 0 | \spawn deployable 8 |
| 9 | - | None | Undefined | accord | 3.75 | 1 | 1 | 0 | \spawn deployable 9 |
| 10 | - | None | Undefined | accord | 0 | 1 | 1 | 0 | \spawn deployable 10 |
| 11 | Repair Station I | Repair Station | Healing | accord | 1.75 | 200 | 1 | 3000 | \spawn deployable 11 |
| 12 | - | None | Undefined | accord | 0 | 1 | 1 | 0 | \spawn deployable 12 |
| 13 | - | None | Undefined | accord | 0 | 1 | 1 | 0 | \spawn deployable 13 |
| 14 | - | None | Undefined | accord | 0 | 1 | 1 | 0 | \spawn deployable 14 |
| 15 | - | None | Undefined | accord | 0 | 1 | 1 | 0 | \spawn deployable 15 |
| 16 | - | None | Undefined | accord | 7.5 | 1 | 1 | 5000 | \spawn deployable 16 |
| 17 | - | None | Undefined | accord | 0.125 | 1 | 1 | 0 | \spawn deployable 17 |
| 18 | - | None | Undefined | accord | 2.5 | 1 | 1 | 0 | \spawn deployable 18 |
| 19 | - | None | Undefined | accord | 37.5 | 1 | 1 | 13000 | \spawn deployable 19 |
| 20 | - | None | Undefined | accord | 7.5 | 1 | 1 | 0 | \spawn deployable 20 |
| 21 | - | None | Undefined | accord | 7.5 | 1 | 1 | 0 | \spawn deployable 21 |
| 22 | - | None | Undefined | accord | 0.75 | 1 | 1 | 0 | \spawn deployable 22 |
| 23 | SIN Deployable | None | Undefined | accord | 3.75 | 1 | 1 | 0 | \spawn deployable 23 |
| 24 | - | None | Undefined | accord | 7.5 | 1 | 1 | 0 | \spawn deployable 24 |
| 25 | - | None | Undefined | accord | 2.5 | 1 | 1 | 0 | \spawn deployable 25 |
| 26 | Forcefield II | Shield | Deployed Target | accord | 6.25 | 2500 | 1 | 0 | \spawn deployable 26 |
| 27 | Repair Station II | Repair Station | Healing | accord | 2.625 | 700 | 1 | 5000 | \spawn deployable 27 |
| 28 | - | None | Undefined | accord | 7.5 | 1 | 1 | 0 | \spawn deployable 28 |
| 29 | - | None | Undefined | accord | 2.5 | 1 | 1 | 0 | \spawn deployable 29 |
| 30 | - | None | Undefined | accord | 0.75 | 1 | 1 | 0 | \spawn deployable 30 |
| 31 | EMP Mine | None | Undefined | accord | 0.75 | 1 | 1 | 0 | \spawn deployable 31 |
| 32 | - | None | Undefined | accord | 3.75 | 1 | 1 | 0 | \spawn deployable 32 |
| 33 | - | None | Undefined | accord | 0.75 | 1 | 1 | 0 | \spawn deployable 33 |
| 34 | - | None | Undefined | accord | 0.75 | 1 | 1 | 0 | \spawn deployable 34 |
| 35 | - | None | Undefined | accord | 0.75 | 1 | 1 | 0 | \spawn deployable 35 |
| 36 | - | None | Undefined | accord | 3.75 | 1 | 1 | 0 | \spawn deployable 36 |
| 37 | Anti-Vehicular Mine | None | Undefined | accord | 1.5 | 1 | 1 | 0 | \spawn deployable 37 |
| 39 | - | None | Undefined | accord | 0 | 1 | 1 | 0 | \spawn deployable 39 |
| 40 | - | None | Undefined | accord | 0 | 1 | 1 | 0 | \spawn deployable 40 |
| 41 | - | None | Undefined | accord | 7.5 | 1 | 1 | 0 | \spawn deployable 41 |
| 42 | - | None | Undefined | accord | 3.75 | 1 | 1 | 0 | \spawn deployable 42 |
| 43 | - | None | Undefined | accord | 3.75 | 1 | 1 | 0 | \spawn deployable 43 |
| 44 | Battleframe Garage | Forge | Undefined | accord | 0 | 1 | 1 | 0 | \spawn deployable 44 |
| 45 | - | None | Undefined | accord | 0 | 1 | 1 | 0 | \spawn deployable 45 |
| 47 | - | None | Undefined | accord | 1.25 | 1 | 1 | 0 | \spawn deployable 47 |
| 48 | - | None | Undefined | accord | 0 | 1 | 1 | 0 | \spawn deployable 48 |
| 50 | - | None | Undefined | accord | 75 | 1 | 1 | 0 | \spawn deployable 50 |
| 51 | - | None | Undefined | accord | 15 | 1 | 1 | 0 | \spawn deployable 51 |
| 52 | - | None | Undefined | accord | 7.5 | 1 | 1 | 0 | \spawn deployable 52 |
| 54 | - | None | Undefined | accord | 7.5 | 1 | 1 | 0 | \spawn deployable 54 |
| 55 | - | None | Undefined | accord | 7.5 | 1 | 1 | 0 | \spawn deployable 55 |
| 56 | - | None | Undefined | accord | 7.5 | 1 | 1 | 0 | \spawn deployable 56 |
| 57 | - | None | Undefined | accord | 0 | 1 | 1 | 0 | \spawn deployable 57 |
| 58 | - | None | Undefined | accord | 0 | 1 | 1 | 0 | \spawn deployable 58 |
| 59 | - | None | Undefined | accord | 0.125 | 1 | 1 | 0 | \spawn deployable 59 |
| 60 | - | None | Undefined | accord | 0 | 1 | 1 | 0 | \spawn deployable 60 |
| 61 | - | None | Healing | accord | 2.5 | 1 | 1 | 0 | \spawn deployable 61 |
| 62 | - | None | Undefined | accord | 0 | 1 | 1 | 0 | \spawn deployable 62 |
| 63 | - | None | Undefined | accord | 1.25 | 1 | 1 | 0 | \spawn deployable 63 |
| 64 | - | None | Undefined | accord | 3.75 | 1 | 1 | 0 | \spawn deployable 64 |
| 65 | - | None | Undefined | accord | 0 | 1 | 1 | 0 | \spawn deployable 65 |
| 66 | - | None | Undefined | accord | 0 | 1 | 1 | 0 | \spawn deployable 66 |
| 67 | - | None | Undefined | accord | 12.5 | 1 | 1 | 0 | \spawn deployable 67 |
| 68 | - | None | Undefined | accord | 0 | 1 | 1 | 0 | \spawn deployable 68 |
| 69 | - | None | Undefined | accord | 7.5 | 1 | 1 | 0 | \spawn deployable 69 |
| 77 | - | None | Undefined | accord | 0 | 1 | 1 | 0 | \spawn deployable 77 |
| 78 | Directional Mine | None | Undefined | accord | 0.75 | 1 | 1 | 0 | \spawn deployable 78 |
| 79 | - | None | Undefined | accord | 0 | 1 | 1 | 0 | \spawn deployable 79 |
| 80 | - | None | Undefined | accord | 2.5 | 1 | 1 | 0 | \spawn deployable 80 |
| 81 | - | None | Undefined | accord | 0.75 | 1 | 1 | 0 | \spawn deployable 81 |
| 82 | - | None | Undefined | accord | 0.75 | 1 | 1 | 0 | \spawn deployable 82 |
| 83 | - | None | Undefined | accord | 0.75 | 1 | 1 | 0 | \spawn deployable 83 |
| 85 | - | None | Undefined | accord | 0 | 1 | 1 | 0 | \spawn deployable 85 |
| 86 | - | None | Undefined | accord | 2.5 | 1 | 1 | 0 | \spawn deployable 86 |
| 87 | - | None | Undefined | accord | 0 | 1 | 1 | 0 | \spawn deployable 87 |
| 88 | - | None | Undefined | accord | 2.5 | 1 | 1 | 0 | \spawn deployable 88 |
| 89 | - | None | Undefined | accord | 2.5 | 1000 | 1 | 0 | \spawn deployable 89 |
| 90 | - | None | Undefined | accord | 1.25 | 200 | 1 | 2000 | \spawn deployable 90 |
| 91 | SuppressorBot deployment | None | Undefined | accord | 1.875 | 200 | 1 | 5000 | \spawn deployable 91 |
| 92 | - | None | Undefined | accord | 0 | 1 | 1 | 0 | \spawn deployable 92 |
| 93 | Small Healthpack | None | Undefined | accord | 1.25 | 1 | 0.5 | 300 | \spawn deployable 93 |
| 94 | - | None | Undefined | accord | 0 | 1 | 1 | 0 | \spawn deployable 94 |
| 95 | - | None | Undefined | accord | 1.125 | 450 | 1 | 0 | \spawn deployable 95 |
| 96 | - | None | Undefined | accord | 0 | 1 | 1 | 0 | \spawn deployable 96 |
| 97 | - | None | Undefined | accord | 1.25 | 1 | 1 | 300 | \spawn deployable 97 |
| 98 | - | None | Undefined | accord | 0.75 | 1 | 1 | 0 | \spawn deployable 98 |
| 99 | - | None | Undefined | accord | 2.5 | 200 | 1 | 5000 | \spawn deployable 99 |
| 100 | - | None | Undefined | accord | 0 | 1 | 1 | 0 | \spawn deployable 100 |
| 101 | - | None | Undefined | accord | 2500000 | 1 | 1 | 0 | \spawn deployable 101 |
| 103 | - | None | Undefined | accord | 0 | 1 | 1 | 0 | \spawn deployable 103 |
| 104 | - | None | Undefined | accord | 0 | 1 | 1 | 0 | \spawn deployable 104 |
| 106 | - | None | Undefined | accord | 1.25 | 1 | 1 | 0 | \spawn deployable 106 |
| 107 | - | None | Undefined | accord | 0 | 1 | 1 | 0 | \spawn deployable 107 |
| 108 | Supply Crate | None | Undefined | accord | 0 | 1 | 1 | 0 | \spawn deployable 108 |
| 109 | Loot Crate | None | Undefined | accord | 0.125 | 50 | 1 | 0 | \spawn deployable 109 |
| 110 | - | None | Play | accord | 0 | 1 | 1 | 0 | \spawn deployable 110 |
| 111 | - | None | Undefined | accord | 2.5 | 1 | 1 | 0 | \spawn deployable 111 |
| 112 | - | None | Undefined | accord | 0.125 | 50 | 1 | 0 | \spawn deployable 112 |
| 113 | - | None | Undefined | accord | 0.125 | 50 | 1 | 0 | \spawn deployable 113 |
| 114 | - | None | Undefined | accord | 0.125 | 50 | 1 | 0 | \spawn deployable 114 |
| 115 | - | None | Rest | accord | 0 | 1 | 1 | 0 | \spawn deployable 115 |
| 116 | Work Deployable (Invisible for p | None | Work | accord | 0 | 1 | 1 | 0 | \spawn deployable 116 |
| 117 | - | None | Undefined | accord | 1.25 | 500 | 1 | 0 | \spawn deployable 117 |
| 118 | Brown Chair | None | Rest | accord | 0 | 0 | 1 | 0 | \spawn deployable 118 |
| 119 | - | None | Repair Work | accord | 0 | 1 | 1 | 0 | \spawn deployable 119 |
| 120 | - | None | Undefined | accord | 0 | 0 | 1 | 0 | \spawn deployable 120 |
| 121 | - | None | Weapon Rack | accord | 0 | 0 | 1 | 0 | \spawn deployable 121 |
| 122 | - | None | Work | accord | 0 | 1 | 1 | 0 | \spawn deployable 122 |
| 123 | - | None | Undefined | accord | 0 | 1 | 1 | 0 | \spawn deployable 123 |
| 124 | - | None | Undefined | accord | 0 | 0 | 1 | 0 | \spawn deployable 124 |
| 126 | - | Mannable Turret | Fixed Weapon | accord | 0 | 0 | 1 | 0 | \spawn deployable 126 |
| 127 | - | None | Undefined | accord | 0 | 1 | 1 | 0 | \spawn deployable 127 |
| 128 | - | None | Undefined | accord | 37.5 | 1 | 1 | 13000 | \spawn deployable 128 |
| 134 | - | None | Undefined | accord | 250 | 1 | 1 | 0 | \spawn deployable 134 |
| 135 | - | None | Undefined | accord | 7.5 | 1 | 1 | 0 | \spawn deployable 135 |
| 136 | - | None | Deployed Target | accord | 0 | 1 | 1 | 0 | \spawn deployable 136 |
| 137 | - | None | Undefined | accord | 0 | 1 | 1 | 0 | \spawn deployable 137 |
| 138 | - | None | Undefined | accord | 3.75 | 200 | 1 | 4000 | \spawn deployable 138 |
| 139 | - | None | Undefined | accord | 0 | 1 | 1 | 0 | \spawn deployable 139 |
| 140 | _Invisible Object | None | Undefined | accord | 0 | 1 | 1 | 0 | \spawn deployable 140 |
| 141 | - | None | Undefined | accord | 0 | 1 | 1 | 0 | \spawn deployable 141 |
| 142 | - | None | Undefined | accord | 0 | 1 | 1 | 0 | \spawn deployable 142 |
| 143 | - | None | Undefined | accord | 0.75 | 1 | 1 | 0 | \spawn deployable 143 |
| 144 | - | None | Undefined | accord | 0 | 1 | 1 | 0 | \spawn deployable 144 |
| 145 | - | Mine | Undefined | accord | 1 | 1 | 1 | 2000 | \spawn deployable 145 |
| 146 | - | None | Undefined | accord | 0 | 1 | 1 | 0 | \spawn deployable 146 |
| 148 | - | None | Undefined | accord | 0 | 1 | 1 | 0 | \spawn deployable 148 |
| 149 | Detonator | None | Undefined | accord | 0 | 1 | 1 | 0 | \spawn deployable 149 |
| 150 | - | None | Undefined | accord | 0 | 1 | 1 | 0 | \spawn deployable 150 |
| 151 | - | Charged Pulse | Deployed Target | accord | 1 | 1 | 1 | 1000 | \spawn deployable 151 |
| 152 | - | None | Undefined | accord | 0 | 1 | 1 | 0 | \spawn deployable 152 |
| 153 | - | None | Undefined | accord | 0 | 1 | 1 | 0 | \spawn deployable 153 |
| 154 | - | None | Undefined | accord | 0 | 1 | 1 | 0 | \spawn deployable 154 |
| 155 | - | None | Undefined | accord | 0 | 1 | 1 | 0 | \spawn deployable 155 |
| 156 | _Health Powerup (Medium) | None | Undefined | accord | 0 | 1 | 1.5 | 0 | \spawn deployable 156 |
| 157 | _Ammo Powerup | None | Undefined | accord | 0 | 1 | 1.5 | 0 | \spawn deployable 157 |
| 158 | _Double Damage Powerup | None | Undefined | accord | 0 | 1 | 1 | 0 | \spawn deployable 158 |
| 159 | - | None | Undefined | accord | 0 | 1 | 1 | 0 | \spawn deployable 159 |
| 160 | - | None | Undefined | accord | 0 | 1 | 1 | 0 | \spawn deployable 160 |
| 161 | - | None | Undefined | accord | 0.25 | 1 | 1 | 0 | \spawn deployable 161 |
| 162 | - | None | Undefined | accord | 0 | 1 | 1 | 0 | \spawn deployable 162 |
| 163 | Makeshift Blender | None | Undefined | accord | 0 | 1 | 1 | 0 | \spawn deployable 163 |
| 164 | - | None | Undefined | accord | 100 | 40000 | 1 | 13000 | \spawn deployable 164 |
| 166 | _Gravlift Pad (15m) | None | Undefined | accord | 0.75 | 1 | 1 | 0 | \spawn deployable 166 |
| 167 | Battleframe Station | Loadout Station | Healing | accord | 0 | 0 | 1 | 0 | \spawn deployable 167 |
| 168 | - | None | Undefined | accord | 0 | 1 | 1 | 0 | \spawn deployable 168 |
| 170 | - | None | Undefined | accord | 150 | 1 | 1 | 0 | \spawn deployable 170 |
| 171 | - | None | Undefined | accord | 0 | 1 | 1 | 0 | \spawn deployable 171 |
| 172 | - | None | Undefined | accord | 0 | 1 | 1 | 0 | \spawn deployable 172 |
| 173 | - | None | Undefined | accord | 3.75 | 1500 | 1 | 0 | \spawn deployable 173 |
| 174 | - | None | Undefined | accord | 0 | 1 | 1 | 250 | \spawn deployable 174 |
| 175 | - | None | Undefined | accord | 0 | 1 | 1 | 250 | \spawn deployable 175 |
| 176 | - | None | Undefined | accord | 0 | 1 | 1 | 0 | \spawn deployable 176 |
| 177 | - | None | Undefined | accord | 22.5 | 9000 | 1 | 0 | \spawn deployable 177 |
| 178 | _Ability Refresher Powerup | None | Undefined | accord | 0 | 1 | 1.5 | 0 | \spawn deployable 178 |
| 179 | - | None | Undefined | accord | 0 | 1 | 1 | 0 | \spawn deployable 179 |
| 180 | - | None | Undefined | accord | 0 | 1 | 1 | 0 | \spawn deployable 180 |
| 181 | - | None | Undefined | accord | 7.5 | 200 | 1 | 10000 | \spawn deployable 181 |
| 182 | - | None | Undefined | accord | 2.5 | 1 | 1 | 30 | \spawn deployable 182 |
| 183 | - | None | Undefined | accord | 0 | 1 | 1 | 0 | \spawn deployable 183 |
| 184 | - | None | Undefined | accord | 2.5 | 1 | 1 | 10000 | \spawn deployable 184 |
| 185 | - | None | Undefined | accord | 7.5 | 200 | 1 | 10000 | \spawn deployable 185 |
| 186 | AI Fear | None | Undefined | accord | 0 | 0 | 1 | 0 | \spawn deployable 186 |
| 187 | - | None | Undefined | accord | 7.5 | 400 | 1 | 5000 | \spawn deployable 187 |
| 188 | Powered forcefield location | None | Undefined | accord | 0 | 1 | 1 | 0 | \spawn deployable 188 |
| 190 | - | None | Undefined | accord | 2.5 | 1 | 1 | 0 | \spawn deployable 190 |
| 191 | Generator Forcefield 8x5 | None | Undefined | accord | 5 | 1 | 1 | 0 | \spawn deployable 191 |
| 193 | - | None | Undefined | accord | 5 | 1 | 1 | 10000 | \spawn deployable 193 |
| 194 | SIN Sensor | Tech SIN | Deployed Target | accord | 1 | 3000 | 1 | 5000 | \spawn deployable 194 |
| 195 | SIN Sensor Tech Point | Tech SIN | Undefined | accord | 0 | 1 | 1 | 10000 | \spawn deployable 195 |
| 196 | - | None | Undefined | accord | 7.5 | 1 | 1 | 5000 | \spawn deployable 196 |
| 197 | - | Tech SIN | Undefined | accord | 0 | 1 | 1 | 10000 | \spawn deployable 197 |
| 198 | - | None | Undefined | accord | 0 | 1 | 1 | 0 | \spawn deployable 198 |
| 202 | - | None | Undefined | accord | 0 | 1 | 1 | 0 | \spawn deployable 202 |
| 203 | - | None | Undefined | accord | 0 | 1 | 1 | 0 | \spawn deployable 203 |
| 204 | - | None | Undefined | accord | 0 | 1 | 1 | 0 | \spawn deployable 204 |
| 206 | _Health Powerup (Small) | None | Undefined | accord | 0 | 1 | 1 | 0 | \spawn deployable 206 |
| 207 | - | None | Undefined | accord | 0 | 1 | 1 | 0 | \spawn deployable 207 |
| 208 | - | None | Undefined | accord | 0 | 1 | 1 | 10000 | \spawn deployable 208 |
| 209 | _Thunderdome | None | Deployed Target | accord | 5 | 1 | 1 | 0 | \spawn deployable 209 |
| 210 | Reconstruction Beacon | None | Undefined | accord | 0 | 1 | 1 | 0 | \spawn deployable 210 |
| 211 | Chosen Arcport Beacon | Spawner | Deployed Target | chosen | 30 | 6000 | 1 | 250 | \spawn deployable 211 |
| 213 | - | None | Undefined | accord | 0 | 0 | 1 | 0 | \spawn deployable 213 |
| 215 | - | None | Undefined | accord | 0.5 | 200 | 1 | 0 | \spawn deployable 215 |
| 216 | - | None | Undefined | accord | 10 | 0 | 1 | 0 | \spawn deployable 216 |
| 217 | Arcporter Exit | None | Undefined | accord | 6.25 | 100 | 1 | 15000 | \spawn deployable 217 |
| 218 | Destructible Objective | None | Undefined | accord | 0.25 | 1 | 1 | 0 | \spawn deployable 218 |
| 219 | - | None | Undefined | accord | 0 | 1 | 1 | 10000 | \spawn deployable 219 |
| 220 | - | None | Undefined | accord | 0 | 1 | 1 | 0 | \spawn deployable 220 |
| 222 | Generator Forcefield 4x4 | None | Undefined | accord | 5 | 1 | 1 | 0 | \spawn deployable 222 |
| 223 | - | None | Undefined | accord | 0 | 1 | 1 | 0 | \spawn deployable 223 |
| 225 | - | None | Undefined | accord | 0 | 1 | 1 | 0 | \spawn deployable 225 |
| 226 | - | None | Undefined | accord | 0 | 1 | 1 | 0 | \spawn deployable 226 |
| 227 | - | None | Undefined | accord | 5 | 1 | 1 | 5000 | \spawn deployable 227 |
| 228 | - | None | Undefined | accord | 0 | 1 | 1 | 10000 | \spawn deployable 228 |
| 236 | _Health Powerup (Large) | None | Undefined | accord | 0 | 1 | 1 | 0 | \spawn deployable 236 |
| 242 | _Generic Pad | None | Undefined | accord | 0 | 1 | 1 | 0 | \spawn deployable 242 |
| 245 | - | None | Undefined | accord | 0 | 1 | 1 | 0 | \spawn deployable 245 |
| 246 | - | None | Undefined | accord | 0.5 | 200 | 1 | 0 | \spawn deployable 246 |
| 247 | - | None | Undefined | accord | 0 | 1 | 1 | 0 | \spawn deployable 247 |
| 249 | - | None | Undefined | accord | 7.5 | 400 | 1 | 10000 | \spawn deployable 249 |
| 250 | - | None | Undefined | accord | 0 | 1 | 1 | 0 | \spawn deployable 250 |
| 251 | - | None | Undefined | accord | 0 | 1 | 1 | 0 | \spawn deployable 251 |
| 252 | - | None | Undefined | accord | 0 | 1 | 1 | 0 | \spawn deployable 252 |
| 253 | - | Charged Pulse | Deployed Target | accord | 2.625 | 700 | 1 | 4000 | \spawn deployable 253 |
| 254 | Forcefield III | Shield | Deployed Target | accord | 7.5 | 1 | 1 | 0 | \spawn deployable 254 |
| 255 | - | None | Rest | accord | 0 | 0 | 1 | 0 | \spawn deployable 255 |
| 256 | - | None | Undefined | accord | 0 | 1 | 1 | 0 | \spawn deployable 256 |
| 258 | - | None | Rest | accord | 0 | 0 | 1 | 0 | \spawn deployable 258 |
| 259 | - | None | Rest | accord | 0 | 0 | 1 | 0 | \spawn deployable 259 |
| 260 | - | None | Rest | accord | 0 | 0 | 1 | 0 | \spawn deployable 260 |
| 261 | - | None | Rest | accord | 0 | 0 | 1 | 0 | \spawn deployable 261 |
| 262 | - | None | Rest | accord | 0 | 0 | 1 | 0 | \spawn deployable 262 |
| 263 | Forcefield I | Shield | Undefined | accord | 5 | 2000 | 1 | 0 | \spawn deployable 263 |
| 264 | Forcefield II | Shield | Undefined | accord | 6.25 | 2500 | 1 | 0 | \spawn deployable 264 |
| 265 | - | Shield | Undefined | accord | 8.125 | 1 | 1 | 0 | \spawn deployable 265 |
| 268 | - | None | Undefined | accord | 1.25 | 500 | 1.2 | 0 | \spawn deployable 268 |
| 269 | TownStand1 invisible spot. | None | Rest | accord | 0 | 0 | 1 | 0 | \spawn deployable 269 |
| 270 | TownStand2 invisible spot. | None | Rest | accord | 0 | 0 | 1 | 0 | \spawn deployable 270 |
| 271 | TownStand3 invisible spot. | None | Rest | accord | 0 | 0 | 1 | 0 | \spawn deployable 271 |
| 272 | TownStand4 invisible spot. | None | Rest | accord | 0 | 0 | 1 | 0 | \spawn deployable 272 |
| 273 | TownStand5 invisible spot. | None | Rest | accord | 0 | 0 | 1 | 0 | \spawn deployable 273 |
| 274 | TownStand6 invisible spot. | None | Rest | accord | 0 | 0 | 1 | 0 | \spawn deployable 274 |
| 275 | - | None | Rest | accord | 0 | 0 | 1 | 0 | \spawn deployable 275 |
| 278 | - | None | Rest | accord | 0 | 0 | 1 | 0 | \spawn deployable 278 |
| 279 | - | None | Rest | accord | 0 | 0 | 1 | 0 | \spawn deployable 279 |
| 282 | - | None | Rest | accord | 0 | 0 | 1 | 0 | \spawn deployable 282 |
| 283 | - | None | Undefined | accord | 37.5 | 1 | 1 | 13000 | \spawn deployable 283 |
| 284 | - | None | Undefined | accord | 7.5 | 3000 | 1 | 1 | \spawn deployable 284 |
| 285 | SIN Shield Generator | None | Target (primary) | accord | 30 | 3000 | 1 | 0 | \spawn deployable 285 |
| 286 | - | None | Undefined | accord | 0 | 1 | 1 | 0 | \spawn deployable 286 |
| 287 | - | None | Rest | accord | 0 | 0 | 1 | 0 | \spawn deployable 287 |
| 288 | - | None | Undefined | accord | 0 | 1 | 1 | 0 | \spawn deployable 288 |
| 289 | Glider Pad | Glider pad | Undefined | accord | 0.75 | 1 | 1 | 0 | \spawn deployable 289 |
| 291 | Copacabana SIN Uplink | SIN Tower | Target (secondary) | accord | 7.5 | 3000 | 1 | 40666 | \spawn deployable 291 |
| 292 | - | None | Undefined | accord | 37.5 | 1 | 1 | 13000 | \spawn deployable 292 |
| 293 | - | None | Undefined | accord | 1.25 | 1 | 1 | 300 | \spawn deployable 293 |
| 294 | - | None | Work | accord | 0 | 1 | 1 | 0 | \spawn deployable 294 |
| 295 | - | None | Undefined | accord | 0 | 1 | 1 | 0 | \spawn deployable 295 |
| 303 | - | Tech Turret | Undefined | accord | 0 | 1 | 1 | 0 | \spawn deployable 303 |
| 304 | Water Valve | None | Undefined | accord | 0 | 1 | 1 | 0 | \spawn deployable 304 |
| 305 | - | None | Undefined | accord | 1 | 1 | 1 | 0 | \spawn deployable 305 |
| 306 | AA Turret | Mannable Turret | Fixed Weapon | accord | 15 | 6000 | 1 | 0 | \spawn deployable 306 |
| 307 | - | None | Deployed Target | accord | 2.5 | 1000 | 1 | 0 | \spawn deployable 307 |
| 308 | - | None | Undefined | accord | 0 | 1 | 1 | 0 | \spawn deployable 308 |
| 310 | - | None | Undefined | friendly | 250000 | 100000000 | 1 | 0 | \spawn deployable 310 |
| 312 | - | None | Undefined | accord | 0 | 1 | 1 | 0 | \spawn deployable 312 |
| 313 | - | None | Deployed Target | accord | 7.4 | 1200 | 1 | 75000 | \spawn deployable 313 |
| 314 | - | None | Undefined | accord | 0.0625 | 1 | 1 | 0 | \spawn deployable 314 |
| 315 | - | None | Undefined | accord | 0 | 1 | 1 | 0 | \spawn deployable 315 |
| 316 | - | None | Undefined | accord | 0 | 1 | 1 | 0 | \spawn deployable 316 |
| 317 | - | None | Undefined | accord | 0 | 1 | 1 | 0 | \spawn deployable 317 |
| 318 | - | None | Deployed Target | accord | 2.5 | 100 | 1 | 0 | \spawn deployable 318 |
| 320 | - | None | Undefined | friendly | 0 | 1 | 1 | 0 | \spawn deployable 320 |
| 321 | Forcefield II | Shield | Undefined | accord | 6.25 | 2000 | 1 | 5000 | \spawn deployable 321 |
| 322 | - | None | Undefined | accord | 15 | 6000 | 1 | 250 | \spawn deployable 322 |
| 323 | - | None | Undefined | accord | 37.5 | 15000 | 1 | 250 | \spawn deployable 323 |
| 325 | Dying Forcefield | None | Deployed Target | accord | 2.5 | 1 | 1 | 0 | \spawn deployable 325 |
| 326 | - | None | Rest | accord | 0 | 0 | 1 | 0 | \spawn deployable 326 |
| 327 | - | None | Undefined | accord | 0 | 1 | 1 | 0 | \spawn deployable 327 |
| 328 | - | None | Undefined | accord | 1.75 | 200 | 1 | 0 | \spawn deployable 328 |
| 332 | - | SIN Tower | Undefined | accord | 0 | 1 | 1 | 0 | \spawn deployable 332 |
| 333 | Forcefield III | Shield | Undefined | accord | 5 | 1500 | 1 | 5000 | \spawn deployable 333 |
| 334 | _TownSit1 invisible spot. | None | Rest | accord | 0 | 0 | 1 | 0 | \spawn deployable 334 |
| 335 | - | None | Undefined | accord | 0 | 1 | 1 | 0 | \spawn deployable 335 |
| 336 | - | None | Fixed Weapon | chosen | 125 | 50000 | 1 | 0 | \spawn deployable 336 |
| 339 | - | None | Undefined | accord | 0.75 | 1 | 1 | 0 | \spawn deployable 339 |
| 340 | Chosen Drop Pod | Spawner | Deployed Target | chosen | 62.5 | 25000 | 1 | 250 | \spawn deployable 340 |
| 341 | - | None | Undefined | accord | 0 | 1 | 1 | 0 | \spawn deployable 341 |
| 342 | Forcefield I | Shield | Deployed Target | accord | 5 | 200 | 1 | 2000 | \spawn deployable 342 |
| 343 | - | None | Undefined | accord | 0 | 1 | 1 | 0 | \spawn deployable 343 |
| 344 | - | None | Undefined | accord | 0.1 | 40 | 1 | 0 | \spawn deployable 344 |
| 345 | - | None | Undefined | accord | 0 | 1 | 1 | 0 | \spawn deployable 345 |
| 346 | - | None | Undefined | accord | 0 | 1 | 1 | 0 | \spawn deployable 346 |
| 347 | - | None | Undefined | friendly | 0 | 1 | 1 | 0 | \spawn deployable 347 |
| 348 | - | Turret | Fixed Weapon | accord | 3.5 | 1400 | 1.3 | 5000 | \spawn deployable 348 |
| 351 | - | None | Undefined | accord | 42.5 | 17000 | 1 | 0 | \spawn deployable 351 |
| 353 | - | None | Undefined | chosen | 0 | 0 | 1 | 0 | \spawn deployable 353 |
| 355 | Engineer Turret II | Turret | Fixed Weapon | accord | 4 | 1600 | 1.3 | 4000 | \spawn deployable 355 |
| 356 | - | None | Fixed Weapon | chosen | 25 | 10000 | 0.4 | 0 | \spawn deployable 356 |
| 357 | - | None | Undefined | accord | 0 | 1 | 1 | 0 | \spawn deployable 357 |
| 358 | - | None | Target (primary) | accord | 30 | 12000 | 1 | 0 | \spawn deployable 358 |
| 359 | - | None | Undefined | accord | 0 | 1 | 1 | 0 | \spawn deployable 359 |
| 360 | - | None | Interactable Objective | accord | 0 | 0 | 1 | 0 | \spawn deployable 360 |
| 361 | - | None | Undefined | accord | 0 | 0 | 1 | 0 | \spawn deployable 361 |
| 363 | - | None | Undefined | accord | 0 | 1 | 1 | 0 | \spawn deployable 363 |
| 365 | _Invisible Object | None | Undefined | accord | 0 | 1 | 1 | 0 | \spawn deployable 365 |
| 366 | - | None | Undefined | accord | 0 | 1 | 1 | 0 | \spawn deployable 366 |
| 367 | Fire | None | Undefined | accord | 0 | 1 | 1 | 0 | \spawn deployable 367 |
| 368 | SIN Uplink Tower | SIN Tower | Target (secondary) | accord | 62.5 | 25000 | 1 | 1 | \spawn deployable 368 |
| 369 | _Supply Crate Interacted | None | Undefined | accord | 0 | 1 | 1 | 0 | \spawn deployable 369 |
| 371 | - | Turret | Fixed Weapon | accord | 7.5 | 3000 | 1.5 | 5000 | \spawn deployable 371 |
| 372 | Glider Pad | Glider pad | Undefined | accord | 0 | 0 | 1 | 0 | \spawn deployable 372 |
| 374 | - | None | Undefined | accord | 7.5 | 1 | 1 | 0 | \spawn deployable 374 |
| 375 | - | None | Undefined | accord | 7.5 | 1 | 1 | 0 | \spawn deployable 375 |
| 376 | - | None | Undefined | accord | 7.5 | 1 | 1 | 0 | \spawn deployable 376 |
| 377 | - | None | Undefined | accord | 7.5 | 1 | 1 | 0 | \spawn deployable 377 |
| 379 | - | None | Undefined | accord | 0 | 1 | 1 | 0 | \spawn deployable 379 |
| 380 | - | None | Undefined | accord | 0 | 1 | 1 | 0 | \spawn deployable 380 |
| 381 | - | None | Undefined | accord | 0 | 1 | 1 | 0 | \spawn deployable 381 |
| 382 | - | None | Undefined | accord | 0 | 1 | 1 | 0 | \spawn deployable 382 |
| 383 | - | None | Undefined | accord | 0 | 1 | 1 | 0 | \spawn deployable 383 |
| 385 | - | None | Undefined | accord | 0 | 1 | 1 | 0 | \spawn deployable 385 |
| 387 | - | Spawner | Deployed Target | chosen | 60 | 24000 | 1 | 250 | \spawn deployable 387 |
| 388 | - | None | Undefined | accord | 0 | 1 | 1 | 0 | \spawn deployable 388 |
| 394 | Jump Pad | Jump pad | Undefined | accord | 0.75 | 1 | 1 | 0 | \spawn deployable 394 |
| 395 | Battleframe Station | Loadout Station | Undefined | accord | 0 | 0 | 1 | 0 | \spawn deployable 395 |
| 396 | Battleframe Station | None | Undefined | accord | 0 | 0 | 1 | 0 | \spawn deployable 396 |
| 398 | - | None | Undefined | accord | 0 | 1 | 1 | 11000 | \spawn deployable 398 |
| 399 | - | None | Target (primary) | accord | 50 | 20000 | 1.5 | 11000 | \spawn deployable 399 |
| 400 | - | None | Undefined | accord | 0 | 1 | 1 | 0 | \spawn deployable 400 |
| 401 | Anti-Personnel Auto-Turret | Tech Turret | Fixed Weapon | accord | 5 | 3000 | 1.5 | 5000 | \spawn deployable 401 |
| 402 | Auto-Turret | None | Undefined | accord | 0 | 1 | 1 | 0 | \spawn deployable 402 |
| 404 | Chosen Drop Pod | Spawner | Deployed Target | chosen | 6.25 | 3500 | 1 | 10500 | \spawn deployable 404 |
| 405 | Trans Hub SIN Uplink | None | Target (secondary) | accord | 7.5 | 3000 | 1 | 40666 | \spawn deployable 405 |
| 406 | Hydrocore SIN Uplink | None | Target (secondary) | accord | 125 | 50000 | 1 | 40666 | \spawn deployable 406 |
| 407 | Nutretic Processing SIN Uplink | None | Target (secondary) | accord | 125 | 50000 | 1 | 40666 | \spawn deployable 407 |
| 408 | Broken Shores SIN Uplink | None | Target (secondary) | accord | 125 | 50000 | 1 | 40666 | \spawn deployable 408 |
| 409 | Biosphere SIN Uplink | None | Target (secondary) | accord | 125 | 50000 | 1 | 40666 | \spawn deployable 409 |
| 410 | Sigu's Sanctuary SIN Uplink | None | Target (secondary) | accord | 125 | 50000 | 1 | 40666 | \spawn deployable 410 |
| 411 | Thump Dump SIN Uplink | None | Target (primary) | accord | 125 | 50000 | 1 | 40666 | \spawn deployable 411 |
| 412 | Sunken Harbor SIN Uplink | None | Target (secondary) | accord | 3000 | 3000 | 1 | 40666 | \spawn deployable 412 |
| 413 | The Shiv SIN Uplink | None | Target (secondary) | accord | 125 | 50000 | 1 | 40666 | \spawn deployable 413 |
| 414 | - | None | Target (secondary) | accord | 1 | 3000 | 1 | 40666 | \spawn deployable 414 |
| 415 | Plasma Reactor | None | Target (tertiary) | accord | 30 | 3000 | 1 | 0 | \spawn deployable 415 |
| 416 | - | None | Deployed Target | accord | 0.75 | 1 | 1 | 0 | \spawn deployable 416 |
| 417 | - | None | Undefined | accord | 0.025 | 10 | 1 | 0 | \spawn deployable 417 |
| 418 | - | None | Undefined | accord | 0.025 | 10 | 1 | 0 | \spawn deployable 418 |
| 419 | - | None | Undefined | accord | 0 | 1 | 1 | 0 | \spawn deployable 419 |
| 420 | - | None | Undefined | accord | 0 | 1 | 1 | 0 | \spawn deployable 420 |
| 422 | - | None | Target (tertiary) | accord | 25 | 10000 | 1 | 0 | \spawn deployable 422 |
| 423 | - | None | Undefined | accord | 0 | 1 | 0.8 | 0 | \spawn deployable 423 |
| 424 | - | None | Undefined | accord | 0 | 1 | 1 | 0 | \spawn deployable 424 |
| 426 | - | None | Target (primary) | accord | 2500 | 999999 | 1 | 0 | \spawn deployable 426 |
| 427 | - | None | Undefined | accord | 0 | 1 | 1 | 0 | \spawn deployable 427 |
| 428 | - | None | Undefined | accord | 0 | 1 | 1.3 | 0 | \spawn deployable 428 |
| 429 | - | None | Undefined | accord | 6.25 | 2438 | 1 | 10000 | \spawn deployable 429 |
| 430 | - | None | Undefined | accord | 0 | 1 | 1 | 0 | \spawn deployable 430 |
| 431 | Rare Supply Crate | None | Undefined | accord | 0 | 1 | 3 | 0 | \spawn deployable 431 |
| 436 | - | None | Undefined | accord | 0 | 1 | 1 | 0 | \spawn deployable 436 |
| 438 | - | None | Undefined | accord | 0 | 1 | 1 | 0 | \spawn deployable 438 |
| 439 | _Harvester Drill | None | Undefined | accord | 0 | 1 | 1 | 0 | \spawn deployable 439 |
| 442 | Army Terminal | Army Terminal | Undefined | accord | 0 | 0 | 1 | 0 | \spawn deployable 442 |
| 443 | - | None | Undefined | accord | 0 | 1 | 0.8 | 0 | \spawn deployable 443 |
| 445 | - | None | Undefined | accord | 0.125 | 50 | 1 | 0 | \spawn deployable 445 |
| 446 | Molecular Printer | Manufacturing Terminal | Undefined | accord | 0 | 0 | 1 | 0 | \spawn deployable 446 |
| 448 | _Molecular Printer (Powered Off) | Manufacturing Terminal | Undefined | accord | 0 | 0 | 1 | 0 | \spawn deployable 448 |
| 449 | - | Power Cell Receptacle | Undefined | accord | 0 | 1 | 1 | 1 | \spawn deployable 449 |
| 455 | SIN Uplink Tower | SIN Tower | Target (primary) | accord | 62.5 | 25000 | 1 | 1 | \spawn deployable 455 |
| 459 | Warbringer Generator | Spawner | Undefined | chosen | 3 | 1200 | 1 | 0 | \spawn deployable 459 |
| 460 | - | Spawner | Deployed Target | accord | 30 | 6000 | 1 | 250 | \spawn deployable 460 |
| 461 | Molecular Printer | Manufacturing Terminal | Undefined | accord | 0 | 0 | 1 | 0 | \spawn deployable 461 |
| 462 | Molecular Printer | Manufacturing Terminal | Undefined | accord | 0 | 0 | 1 | 0 | \spawn deployable 462 |
| 464 | - | Spawner | Undefined | chosen | 50 | 20000 | 1 | 500 | \spawn deployable 464 |
| 465 | Chosen Drop Pod | None | Deployed Target | chosen | 6.25 | 2500 | 1 | 250 | \spawn deployable 465 |
| 466 | Army Terminal (Powered Off) | None | Undefined | accord | 0 | 0 | 1 | 0 | \spawn deployable 466 |
| 467 | Battleframe Garage | None | Undefined | accord | 0 | 1 | 1 | 0 | \spawn deployable 467 |
| 469 | - | None | Undefined | gaea | 25 | 10000 | 0.75 | 0 | \spawn deployable 469 |
| 471 | - | None | Undefined | gaea | 25 | 10000 | 1 | 0 | \spawn deployable 471 |
| 472 | - | None | Undefined | accord | 37.5 | 15000 | 1 | 250 | \spawn deployable 472 |
| 473 | Warbringer | None | Undefined | chosen | 12.5 | 5000 | 1 | 15000 | \spawn deployable 473 |
| 477 | - | None | Undefined | accord | 37.5 | 1 | 1 | 12000 | \spawn deployable 477 |
| 478 | - | None | Undefined | accord | 2.5 | 1000 | 1 | 250 | \spawn deployable 478 |
| 479 | - | None | Undefined | accord | 7.5 | 3000 | 1 | 250 | \spawn deployable 479 |
| 480 | - | None | Undefined | accord | 7.5 | 3000 | 1 | 250 | \spawn deployable 480 |
| 481 | - | None | Undefined | accord | 3.75 | 1500 | 1 | 250 | \spawn deployable 481 |
| 482 | - | None | Undefined | accord | 5 | 2000 | 1 | 250 | \spawn deployable 482 |
| 483 | - | None | Undefined | accord | 20 | 8000 | 1 | 0 | \spawn deployable 483 |
| 484 | - | None | Undefined | accord | 0 | 1 | 1 | 13000 | \spawn deployable 484 |
| 485 | - | Loadout Station | Undefined | accord | 0 | 0 | 1 | 0 | \spawn deployable 485 |
| 486 | - | Loadout Station | Undefined | accord | 0 | 0 | 1 | 0 | \spawn deployable 486 |
| 487 | - | None | Undefined | chosen | 0 | 1 | 1 | 0 | \spawn deployable 487 |
| 488 | - | Repair Station | Deployed Target | accord | 12.5 | 5000 | 0.5 | 0 | \spawn deployable 488 |
| 489 | - | Repair Station | Undefined | accord | 0 | 0 | 0.5 | 0 | \spawn deployable 489 |
| 490 | - | None | Deployed Target | accord | 7.5 | 1 | 1 | 0 | \spawn deployable 490 |
| 491 | - | Mine | Undefined | accord | 1 | 1 | 1 | 2000 | \spawn deployable 491 |
| 492 | - | None | Undefined | accord | 15 | 6000 | 1 | 250 | \spawn deployable 492 |
| 493 | Repair Station III | Repair Station | Healing | accord | 2.625 | 700 | 1 | 5000 | \spawn deployable 493 |
| 496 | - | None | Undefined | friendly | 0 | 1 | 1 | 0 | \spawn deployable 496 |
| 498 | - | None | Undefined | melding | 25 | 10000 | 1 | 0 | \spawn deployable 498 |
| 500 | - | None | Undefined | accord | 0 | 1 | 1 | 0 | \spawn deployable 500 |
| 501 | - | Loadout Station | Undefined | accord | 0 | 0 | 1 | 0 | \spawn deployable 501 |
| 502 | - | None | Deployed Target | accord | 1.75 | 700 | 1 | 250 | \spawn deployable 502 |
| 503 | - | None | Deployed Target | accord | 1.75 | 700 | 1 | 250 | \spawn deployable 503 |
| 504 | - | None | Deployed Target | accord | 1.75 | 700 | 1 | 250 | \spawn deployable 504 |
| 505 | - | None | Deployed Target | accord | 1.75 | 700 | 1 | 250 | \spawn deployable 505 |
| 506 | - | None | Deployed Target | accord | 1.75 | 700 | 1 | 250 | \spawn deployable 506 |
| 507 | - | None | Deployed Target | accord | 1.75 | 700 | 1 | 250 | \spawn deployable 507 |
| 508 | - | None | Undefined | accord | 0 | 1 | 1 | 0 | \spawn deployable 508 |
| 509 | - | None | Undefined | accord | 0 | 1 | 1 | 0 | \spawn deployable 509 |
| 510 | - | None | Undefined | accord | 0 | 1 | 1 | 0 | \spawn deployable 510 |
| 514 | - | Turret | Fixed Weapon | accord | 3.5 | 700 | 0.7 | 0 | \spawn deployable 514 |
| 515 | - | None | Undefined | gaea | 25 | 10000 | 1 | 0 | \spawn deployable 515 |
| 517 | Chosen Bifold Cannon | Mannable Turret | Fixed Weapon | chosen | 1 | 100 | 1 | 1000 | \spawn deployable 517 |
| 519 | - | None | Deployed Target | accord | 20 | 8000 | 1 | 0 | \spawn deployable 519 |
| 520 | - | Spawner | Deployed Target | accord | 30 | 12000 | 1 | 250 | \spawn deployable 520 |
| 521 | - | None | Undefined | gaea | 7.5 | 3000 | 1 | 0 | \spawn deployable 521 |
| 522 | - | None | Undefined | accord | 0 | 1 | 4 | 0 | \spawn deployable 522 |
| 524 | Sonic Detonator | None | Undefined | accord | 0 | 1 | 1 | 0 | \spawn deployable 524 |
| 526 | - | None | Undefined | accord | 0 | 1 | 1 | 0 | \spawn deployable 526 |
| 527 | - | PVP Terminal | Undefined | accord | 0 | 1 | 1 | 0 | \spawn deployable 527 |
| 528 | - | PVP Terminal | Undefined | accord | 0 | 1 | 1 | 0 | \spawn deployable 528 |
| 530 | - | Forge | Undefined | accord | 0 | 1 | 1 | 0 | \spawn deployable 530 |
| 533 | - | Turret | Fixed Weapon | accord | 7.5 | 3000 | 3 | 5000 | \spawn deployable 533 |
| 534 | Rare Supply Crate | None | Undefined | accord | 0 | 1 | 3 | 0 | \spawn deployable 534 |
| 535 | Cerrado Plains SIN Uplink | None | Target (secondary) | accord | 125 | 50000 | 1 | 40666 | \spawn deployable 535 |
| 536 | Stonewall SIN Uplink | None | Target (secondary) | accord | 125 | 50000 | 1 | 40666 | \spawn deployable 536 |
| 540 | - | None | Undefined | friendly | 0 | 1 | 1 | 0 | \spawn deployable 540 |
| 542 | - | None | Undefined | friendly | 0 | 1 | 1 | 0 | \spawn deployable 542 |
| 543 | - | None | Undefined | friendly | 0 | 1 | 1 | 0 | \spawn deployable 543 |
| 544 | - | None | Undefined | accord | 0 | 1 | 1 | 0 | \spawn deployable 544 |
| 546 | - | Spawner | Deployed Target | chosen | 18.75 | 7500 | 1 | 0 | \spawn deployable 546 |
| 547 | Marauder Drop Pod | None | Undefined | chosen | 12.5 | 5000 | 1 | 250 | \spawn deployable 547 |
| 548 | - | None | Undefined | accord | 0 | 1 | 1 | 0 | \spawn deployable 548 |
| 549 | - | None | Undefined | accord | 0 | 1 | 1 | 0 | \spawn deployable 549 |
| 552 | - | None | Undefined | accord | 0.75 | 1 | 1 | 0 | \spawn deployable 552 |
| 553 | - | Mannable Turret | Fixed Weapon | chosen | 7.5 | 3000 | 1 | 0 | \spawn deployable 553 |
| 554 | _Aranhas Worker Lair + Spawns | None | Undefined | gaea | 25 | 10000 | 1.25 | 3000 | \spawn deployable 554 |
| 555 | - | None | Undefined | gaea | 25 | 10000 | 2 | 0 | \spawn deployable 555 |
| 556 | Hisser Lair | None | Undefined | gaea | 7.5 | 3000 | 1 | 0 | \spawn deployable 556 |
| 557 | Mobile Loadout Terminal | Forge | Undefined | accord | 0 | 1 | 0.6 | 0 | \spawn deployable 557 |
| 558 | Mobile Battleframe Station | Loadout Station | Undefined | accord | 0 | 0 | 0.7 | 0 | \spawn deployable 558 |
| 559 | _Invisible Object No visuals | None | Undefined | accord | 0 | 1 | 3 | 0 | \spawn deployable 559 |
| 560 | SIN Shield Generator | None | Target (primary) | accord | 35 | 3000 | 1 | 5400 | \spawn deployable 560 |
| 561 | - | None | Target (primary) | accord | 35 | 3000 | 1 | 5400 | \spawn deployable 561 |
| 562 | - | None | Target (primary) | accord | 35 | 3000 | 1 | 5400 | \spawn deployable 562 |
| 564 | - | None | Undefined | accord | 0 | 1 | 1 | 0 | \spawn deployable 564 |
| 567 | - | None | Undefined | accord | 0 | 1 | 1 | 0 | \spawn deployable 567 |
| 568 | SIN Beacon | Mine | Undefined | accord | 5 | 1 | 1 | 500 | \spawn deployable 568 |
| 570 | Accord Dropship | None | Undefined | accord | 0.0025 | 1 | 1 | 0 | \spawn deployable 570 |
| 571 | SIN Uplink Tower Shield | SIN Tower | Target (secondary) | accord | 7.5 | 3000 | 1 | 1 | \spawn deployable 571 |
| 572 | - | None | Undefined | accord | 0 | 1 | 1 | 0 | \spawn deployable 572 |
| 573 | - | None | Undefined | accord | 0 | 1 | 3 | 0 | \spawn deployable 573 |
| 574 | - | None | Undefined | accord | 0 | 1 | 1 | 0 | \spawn deployable 574 |
| 575 | - | None | Undefined | accord | 37.5 | 1 | 0.1 | 13000 | \spawn deployable 575 |
| 577 | - | None | Undefined | accord | 0 | 1 | 1 | 500 | \spawn deployable 577 |
| 578 | - | None | Undefined | gaea | 25 | 10000 | 1.25 | 3000 | \spawn deployable 578 |
| 579 | - | None | Undefined | melding | 25 | 10000 | 1 | 0 | \spawn deployable 579 |
| 584 | - | None | Undefined | monster | 0 | 0 | 1 | 0 | \spawn deployable 584 |
| 585 | _Toxic Aranhas Worker Lair + Spawns | None | Undefined | gaea | 25 | 10000 | 1.25 | 3000 | \spawn deployable 585 |
| 586 | - | None | Undefined | accord | 1.25 | 1 | 0.75 | 300 | \spawn deployable 586 |
| 588 | - | Loadout Station | Undefined | accord | 0 | 0 | 1 | 0 | \spawn deployable 588 |
| 589 | - | None | Deployed Target | accord | 12.5 | 2500 | 0.35 | 45000 | \spawn deployable 589 |
| 590 | - | Charged Pulse | Deployed Target | accord | 0 | 1 | 1 | 1000 | \spawn deployable 590 |
| 591 | - | None | Undefined | accord | 0 | 0 | 1 | 0 | \spawn deployable 591 |
| 592 | - | None | Undefined | accord | 0 | 1 | 3 | 0 | \spawn deployable 592 |
| 593 | - | None | Undefined | accord | 0 | 1 | 1.55 | 0 | \spawn deployable 593 |
| 595 | - | None | Undefined | friendly | 0 | 1 | 1 | 0 | \spawn deployable 595 |
| 597 | Flare | None | Undefined | accord | 0 | 1 | 1 | 0 | \spawn deployable 597 |
| 599 | - | None | Undefined | bandit | 0 | 1 | 1 | 0 | \spawn deployable 599 |
| 600 | - | Tech Turret | Fixed Weapon | bandit | 3.75 | 1500 | 1.5 | 5000 | \spawn deployable 600 |
| 601 | - | None | Undefined | accord | 0 | 1 | 1 | 0 | \spawn deployable 601 |
| 602 | Trap Explosive | Mine | Undefined | accord | 0.25 | 1 | 0.7 | 500 | \spawn deployable 602 |
| 603 | - | Mine | Fixed Weapon | accord | 1 | 100 | 1 | 250 | \spawn deployable 603 |
| 604 | - | Mine | Fixed Weapon | accord | 0.25 | 100 | 1 | 250 | \spawn deployable 604 |
| 605 | - | Mine | Fixed Weapon | accord | 0.25 | 100 | 1 | 250 | \spawn deployable 605 |
| 606 | Multi Turret | Multi-Turret | Fixed Weapon | accord | 1.125 | 1 | 0.9 | 1500 | \spawn deployable 606 |
| 607 | - | Turret | Fixed Weapon | accord | 2 | 800 | 1 | 1000 | \spawn deployable 607 |
| 608 | - | None | Rest | accord | 0 | 0 | 1 | 0 | \spawn deployable 608 |
| 609 | - | None | Rest | accord | 0 | 0 | 1 | 0 | \spawn deployable 609 |
| 610 | Large Scottish Sword | None | Undefined | accord | 1.25 | 1 | 3 | 0 | \spawn deployable 610 |
| 611 | - | None | Undefined | accord | 0.5 | 1 | 1 | 0 | \spawn deployable 611 |
| 612 | Energy Shield | Shield | Deployed Target | accord | 5 | 1 | 1 | 0 | \spawn deployable 612 |
| 613 | - | None | Undefined | accord | 37.5 | 1 | 1 | 0 | \spawn deployable 613 |
| 614 | _DuelMarker | None | Undefined | accord | 0 | 1 | 1 | 0 | \spawn deployable 614 |
| 615 | - | None | Undefined | chosen | 0 | 1 | 1 | 0 | \spawn deployable 615 |
| 616 | - | None | Undefined | friendly | 0 | 1 | 1 | 0 | \spawn deployable 616 |
| 617 | - | Spawner | Deployed Target | accord | 15 | 6000 | 0.2 | 45000 | \spawn deployable 617 |
| 618 | - | Turret | Fixed Weapon | accord | 10 | 4000 | 1.5 | 5000 | \spawn deployable 618 |
| 619 | APS Barrier Emitter | None | Deployed Target | accord | 7.5 | 3000 | 1 | 3000 | \spawn deployable 619 |
| 620 | - | Turret | Fixed Weapon | accord | 12.5 | 5000 | 1.5 | 5000 | \spawn deployable 620 |
| 621 | Barrier Emitter Tech Point | None | Deployed Target | accord | 0 | 1 | 1 | 0 | \spawn deployable 621 |
| 622 | _Multi-Schema Outpost Auto-Turret | None | Undefined | accord | 0 | 1 | 1 | 0 | \spawn deployable 622 |
| 623 | - | None | Undefined | accord | 0 | 1 | 1 | 0 | \spawn deployable 623 |
| 624 | - | None | Undefined | accord | 0 | 1 | 1 | 0 | \spawn deployable 624 |
| 625 | - | Turret | Fixed Weapon | accord | 1 | 400 | 0.5 | 300 | \spawn deployable 625 |
| 626 | - | None | Undefined | accord | 0 | 1 | 1 | 1000 | \spawn deployable 626 |
| 627 | - | None | Undefined | accord | 0 | 1 | 1 | 0 | \spawn deployable 627 |
| 628 | - | None | Undefined | accord | 0 | 1 | 1 | 0 | \spawn deployable 628 |
| 629 | - | None | Undefined | accord | 0 | 1 | 1 | 0 | \spawn deployable 629 |
| 630 | - | None | Undefined | accord | 0 | 1 | 1 | 0 | \spawn deployable 630 |
| 631 | - | None | Undefined | accord | 0 | 1 | 1 | 0 | \spawn deployable 631 |
| 632 | - | None | Undefined | accord | 0 | 1 | 1 | 0 | \spawn deployable 632 |
| 633 | Decoy Projector | None | Deployed Target | accord | 2500 | 999999 | 1 | 0 | \spawn deployable 633 |
| 635 | Metallics Surface Deposit | Surface Deposit | Undefined | friendly | 0.375 | 150 | 1 | 0 | \spawn deployable 635 |
| 636 | Tungsten Surface Deposit | Surface Deposit | Undefined | friendly | 0.375 | 150 | 1 | 0 | \spawn deployable 636 |
| 637 | Titanium Surface Deposit | Surface Deposit | Undefined | friendly | 0.375 | 150 | 1 | 0 | \spawn deployable 637 |
| 638 | Uranium Surface Deposit | Surface Deposit | Undefined | friendly | 0.375 | 150 | 1 | 0 | \spawn deployable 638 |
| 643 | AA Mannable Turret Tech Point | None | Undefined | accord | 0 | 1 | 2.1 | 0 | \spawn deployable 643 |
| 644 | - | None | Undefined | accord | 21.25 | 8500 | 1 | 0 | \spawn deployable 644 |
| 645 | - | None | Undefined | accord | 0 | 1 | 1 | 0 | \spawn deployable 645 |
| 646 | - | Mine | Fixed Weapon | accord | 0.25 | 100 | 1 | 250 | \spawn deployable 646 |
| 647 | - | None | Undefined | accord | 0 | 1 | 1 | 0 | \spawn deployable 647 |
| 648 | Barricade | None | Undefined | accord | 21.25 | 8500 | 1 | 0 | \spawn deployable 648 |
| 649 | - | None | Undefined | chosen | 0 | 1 | 2.5 | 0 | \spawn deployable 649 |
| 650 | - | None | Undefined | gaea | 1.875 | 750 | 0.5 | 0 | \spawn deployable 650 |
| 651 | - | None | Undefined | accord | 0 | 0 | 1 | 0 | \spawn deployable 651 |
| 652 | - | None | Undefined | gaea | 25 | 10000 | 1 | 0 | \spawn deployable 652 |
| 653 | Low Grade Sonic Detonator | None | Undefined | accord | 0 | 1 | 1 | 0 | \spawn deployable 653 |
| 654 | - | Surface Deposit | Undefined | friendly | 0.125 | 50 | 0.4 | 0 | \spawn deployable 654 |
| 655 | - | Repair Station | Healing | accord | 2.625 | 700 | 1 | 5000 | \spawn deployable 655 |
| 656 | Gravity Field Grenade - Gravity Sphere | None | Undefined | accord | 0 | 1 | 1 | 0 | \spawn deployable 656 |
| 657 | Remote Explosive | Mine | Undefined | accord | 1 | 1 | 1 | 0 | \spawn deployable 657 |
| 658 | - | None | Healing | accord | 0 | 1 | 1 | 0 | \spawn deployable 658 |
| 659 | Riot Gun Auto-Turret | Turret | Fixed Weapon | accord | 7.5 | 3000 | 1.5 | 5000 | \spawn deployable 659 |
| 661 | Chosen Barricade | None | Undefined | chosen | 0 | 1 | 1 | 0 | \spawn deployable 661 |
| 662 | - | None | Undefined | accord | 1.25 | 500 | 1 | 0 | \spawn deployable 662 |
| 663 | Melding Tornado Core | Spawner | Deployed Target | melding | 250 | 100000 | 1 | 0 | \spawn deployable 663 |
| 664 | - | Spawner | Deployed Target | chosen | 0 | 1 | 1 | 0 | \spawn deployable 664 |
| 672 | Tornado Portal | None | Undefined | accord | 0 | 0 | 1 | 0 | \spawn deployable 672 |
| 674 | - | Shield | Deployed Target | accord | 5 | 2000 | 1 | 0 | \spawn deployable 674 |
| 675 | - | None | Undefined | melding | 0 | 0 | 1 | 0 | \spawn deployable 675 |
| 676 | New You | New You | Undefined | accord | 0 | 0 | 1 | 0 | \spawn deployable 676 |
| 677 | - | None | Undefined | melding | 0.25 | 100 | 1 | 0 | \spawn deployable 677 |
| 679 | Tier 1 Melding Bubble Deposit (Level 1) | None | Undefined | melding | 1 | 400 | 1 | 0 | \spawn deployable 679 |
| 680 | - | None | Undefined | accord | 0 | 1 | 1 | 0 | \spawn deployable 680 |
| 681 | Tier 1 Melding Bubble Deposit (Level 2) | None | Undefined | melding | 1 | 400 | 1 | 0 | \spawn deployable 681 |
| 682 | Tier 1 Melding Bubble Deposit (Level 3) | None | Undefined | melding | 1 | 400 | 1 | 0 | \spawn deployable 682 |
| 683 | Supply Station | None | Deployed Target | accord | 0.625 | 1 | 1 | 0 | \spawn deployable 683 |
| 684 | Warbringer Pillar | None | Undefined | chosen | 0 | 1 | 1 | 3000 | \spawn deployable 684 |
| 685 | Warbringer Pillar | None | Undefined | accord | 0 | 1 | 1 | 3000 | \spawn deployable 685 |
| 686 | _Chosen-Style Health Powerup (Medium) | None | Undefined | accord | 0 | 1 | 2 | 0 | \spawn deployable 686 |
| 687 | _Chosen-Style Ammo Powerup | None | Undefined | accord | 0 | 1 | 2 | 0 | \spawn deployable 687 |
| 688 | - | None | Undefined | accord | 0 | 1 | 1.79 | 0 | \spawn deployable 688 |
| 689 | Melding Resources | Spawner | Undefined | melding | 1.25 | 500 | 0.25 | 0 | \spawn deployable 689 |
| 690 | Melding Core Resources | Spawner | Undefined | melding | 1.25 | 500 | 0.25 | 0 | \spawn deployable 690 |
| 691 | Blue Hero Tribute Pyrotechnic Launcher | Consumer Fireworks | Undefined | accord | 2.5 | 0 | 0.1 | 0 | \spawn deployable 691 |
| 692 | - | Mine | Undefined | accord | 5 | 1 | 1 | 2000 | \spawn deployable 692 |
| 693 | Tiki Torch | None | Undefined | accord | 12.5 | 5000 | 1 | 300 | \spawn deployable 693 |
| 694 | - | None | Undefined | accord | 0 | 1 | 1 | 0 | \spawn deployable 694 |
| 696 | Fireworks Launcher | Consumer Fireworks | Undefined | accord | 0 | 1 | 1 | 0 | \spawn deployable 696 |
| 698 | Tri-color Obrigado Pyrotechnic Launcher | Consumer Fireworks | Undefined | accord | 2.5 | 0 | 0.1 | 0 | \spawn deployable 698 |
| 699 | El Cid's Mascletá Pyrotechnic Launcher | Consumer Fireworks | Undefined | accord | 2.5 | 0 | 0.1 | 0 | \spawn deployable 699 |
| 700 | Luau Larry Terminal | None | Undefined | accord | 0 | 0 | 1 | 0 | \spawn deployable 700 |
| 701 | Luau Larry Terminal (Powered Off) | None | Undefined | accord | 0 | 0 | 1 | 0 | \spawn deployable 701 |
| 702 | _Heavy Lift | None | Undefined | accord | 0 | 0 | 1 | 0 | \spawn deployable 702 |
| 703 | - | None | Undefined | accord | 0 | 1 | 1 | 0 | \spawn deployable 703 |
| 708 | - | None | Rest | gaea | 0 | 0 | 1 | 0 | \spawn deployable 708 |
| 710 | - | None | Undefined | accord | 0 | 0 | 1 | 0 | \spawn deployable 710 |
| 714 | _Repulsor Amplifier | None | Undefined | accord | 0 | 0 | 1 | 1 | \spawn deployable 714 |
| 716 | Stock Bait Kit | None | Target (primary) | accord | 0.025 | 10 | 1 | 0 | \spawn deployable 716 |
| 717 | - | None | Undefined | accord | 5 | 1 | 1 | 0 | \spawn deployable 717 |
| 718 | - | Repair Station | Healing | accord | 0 | 500 | 0.5 | 0 | \spawn deployable 718 |
| 719 | - | None | Undefined | gaea | 25 | 10000 | 1 | 3000 | \spawn deployable 719 |
| 720 | - | None | Undefined | friendly | 0 | 1 | 1 | 0 | \spawn deployable 720 |
| 721 | Arcporting Pylon | None | Undefined | accord | 0 | 1 | 0.15 | 0 | \spawn deployable 721 |
| 722 | Holmgang Tech | None | Undefined | accord | 0 | 1 | 1 | 5000 | \spawn deployable 722 |
| 723 | - | None | Deployed Target | accord | 0 | 1 | 1 | 0 | \spawn deployable 723 |
| 724 | - | None | Deployed Target | accord | 0 | 1 | 1 | 0 | \spawn deployable 724 |
| 725 | - | None | Undefined | accord | 0 | 0 | 1 | 0 | \spawn deployable 725 |
| 726 | Accord Supply Cache | None | Undefined | accord | 0 | 1 | 1 | 0 | \spawn deployable 726 |
| 727 | Accord Supplies - Interacted With | None | Undefined | accord | 0 | 1 | 1 | 0 | \spawn deployable 727 |
| 728 | - | None | Undefined | accord | 0.0625 | 1 | 1 | 0 | \spawn deployable 728 |
| 730 | - | None | Undefined | gaea | 3.75 | 1500 | 1 | 0 | \spawn deployable 730 |
| 731 | Pirate Spawn Beacon | None | Undefined | accord | 0 | 1 | 3 | 0 | \spawn deployable 731 |
| 732 | - | None | Undefined | gaea | 7.5 | 3000 | 1.5 | 0 | \spawn deployable 732 |
| 733 | - | None | Undefined | accord | 0 | 1 | 0.5 | 0 | \spawn deployable 733 |
| 734 | - | None | Undefined | accord | 0 | 1 | 1 | 0 | \spawn deployable 734 |
| 735 | SIN Imprint: Copacabana | Datapads | Undefined | accord | 0 | 0 | 1 | 0 | \spawn deployable 735 |
| 738 | Crashed LGV | Datapads | Undefined | accord | 0 | 1 | 1 | 0 | \spawn deployable 738 |
| 739 | Crashed LGV | Datapads | Undefined | accord | 0 | 1 | 1 | 0 | \spawn deployable 739 |
| 740 | - | None | Undefined | accord | 0 | 1 | 0.5 | 0 | \spawn deployable 740 |
| 741 | Bounty Terminal | Forge | Undefined | accord | 0 | 1 | 1 | 0 | \spawn deployable 741 |
| 742 | - | None | Undefined | accord | 0.0625 | 1 | 1 | 0 | \spawn deployable 742 |
| 743 | Data-Worm | Datapads | Target (primary) | accord | 0 | 1 | 1 | 3500 | \spawn deployable 743 |
| 744 | Data-Worm | Datapads | Target (primary) | accord | 25 | 4000 | 1 | 0 | \spawn deployable 744 |
| 745 | - | None | Deployed Target | accord | 7.5 | 1 | 1 | 0 | \spawn deployable 745 |
| 746 | - | None | Undefined | friendly | 1.25 | 500 | 1 | 0 | \spawn deployable 746 |
| 747 | - | SIN Tower | Undefined | gaea | 2.25 | 1 | 1 | 0 | \spawn deployable 747 |
| 748 | Warbringer Generator | Spawner | Undefined | chosen | 0 | 1 | 1 | 0 | \spawn deployable 748 |
| 749 | Patrol Bestower | None | Undefined | accord | 0 | 1 | 1 | 1000 | \spawn deployable 749 |
| 750 | SIN Imprint: Trans-Hub | Datapads | Undefined | accord | 0 | 0 | 1 | 0 | \spawn deployable 750 |
| 751 | - | Datapads | Undefined | accord | 0 | 0 | 1 | 0 | \spawn deployable 751 |
| 752 | SIN Imprint: Cerrado Plains | Datapads | Undefined | accord | 0 | 0 | 1 | 0 | \spawn deployable 752 |
| 753 | - | Datapads | Undefined | accord | 0 | 0 | 1 | 0 | \spawn deployable 753 |
| 754 | SIN Imprint: Hydro-Core | Datapads | Undefined | accord | 0 | 0 | 1 | 0 | \spawn deployable 754 |
| 755 | - | Datapads | Undefined | accord | 0 | 0 | 1 | 0 | \spawn deployable 755 |
| 756 | SIN Imprint: The Shiv | Datapads | Undefined | accord | 0 | 0 | 1 | 0 | \spawn deployable 756 |
| 757 | Tombstone | Consumer Fireworks | Undefined | accord | 0 | 1 | 1 | 0 | \spawn deployable 757 |
| 758 | - | Mine | Undefined | accord | 1.5 | 1 | 1 | 2000 | \spawn deployable 758 |
| 759 | Holmgang Tech | None | Undefined | accord | 0 | 1 | 1 | 5000 | \spawn deployable 759 |
| 760 | Holmgang Tech | None | Undefined | accord | 0 | 1 | 1 | 5000 | \spawn deployable 760 |
| 761 | Holmgang Tech | None | Undefined | accord | 0 | 1 | 1 | 5000 | \spawn deployable 761 |
| 762 | _Invisible Object No visuals | None | Undefined | accord | 0 | 1 | 1 | 0 | \spawn deployable 762 |
| 764 | - | Mine | Undefined | accord | 1.5 | 1 | 1 | 2000 | \spawn deployable 764 |
| 765 | - | None | Undefined | accord | 0 | 1 | 1 | 2000 | \spawn deployable 765 |
| 766 | - | None | Undefined | accord | 1.5 | 1 | 1 | 0 | \spawn deployable 766 |
| 767 | Gravestone - Fog Machine | Consumer Fireworks | Undefined | accord | 0 | 1 | 1 | 0 | \spawn deployable 767 |
| 768 | Jack-o-Lantern | None | Undefined | accord | 0.0025 | 1 | 1 | 1 | \spawn deployable 768 |
| 769 | Brontodon Gene Transsample Shipment | None | Undefined | accord | 0 | 0 | 0.25 | 0 | \spawn deployable 769 |
| 770 | Brontodon Gene Epicycle Sample Shipment | None | Undefined | accord | 0 | 0 | 1 | 0 | \spawn deployable 770 |
| 771 | Brontodon Gene Epicycle Collection Shipment | None | Undefined | accord | 0 | 0 | 2 | 0 | \spawn deployable 771 |
| 772 | Brontodon Transgenome Shipment | None | Undefined | accord | 0 | 0 | 3 | 0 | \spawn deployable 772 |
| 773 | - | None | Undefined | accord | 3.75 | 1 | 0.1 | 0 | \spawn deployable 773 |
| 774 | Audrey III | Glider pad | Undefined | accord | 2.25 | 900 | 1 | 0 | \spawn deployable 774 |
| 775 | SIN Uplink Beacon | None | Undefined | accord | 0 | 1 | 1.5 | 11000 | \spawn deployable 775 |
| 776 | SIN Uplink Beacon | None | Target (primary) | accord | 50 | 20000 | 1.5 | 11000 | \spawn deployable 776 |
| 777 | - | None | Undefined | gaea | 1.125 | 450 | 1 | 0 | \spawn deployable 777 |
| 778 | Dead Drop Bucket | Datapads | Undefined | accord | 0 | 1 | 1 | 3500 | \spawn deployable 778 |
| 779 | - | None | Undefined | accord | 0 | 1 | 1 | 0 | \spawn deployable 779 |
| 780 | - | None | Undefined | gaea | 2.25 | 1 | 1.25 | 0 | \spawn deployable 780 |
| 781 | - | None | Undefined | gaea | 2.25 | 1 | 1.5 | 0 | \spawn deployable 781 |
| 782 | - | None | Undefined | accord | 0 | 1 | 1 | 1000 | \spawn deployable 782 |
| 783 | - | None | Undefined | accord | 0 | 1 | 1 | 1000 | \spawn deployable 783 |
| 784 | - | None | Undefined | accord | 0 | 1 | 1 | 0 | \spawn deployable 784 |
| 785 | SIN Data Collector | Datapads | Undefined | accord | 0 | 1 | 1 | 3500 | \spawn deployable 785 |
| 786 | - | None | Undefined | accord | 0 | 1 | 1 | 0 | \spawn deployable 786 |
| 787 | - | None | Undefined | accord | 0 | 1 | 1 | 0 | \spawn deployable 787 |
| 788 | _Large Permadome Deployable | None | Undefined | accord | 0 | 1 | 3.8 | 0 | \spawn deployable 788 |
| 789 | Accord Cargo Crate | None | Undefined | accord | 0 | 1 | 1 | 0 | \spawn deployable 789 |
| 790 | Breakable Cargo Crate | None | Undefined | accord | 0.25 | 100 | 1 | 0 | \spawn deployable 790 |
| 791 | Crashed Thumper | None | Undefined | accord | 0 | 0 | 1 | 18600 | \spawn deployable 791 |
| 793 | - | None | Undefined | accord | 0 | 10000 | 1.53 | 0 | \spawn deployable 793 |
| 794 | - | None | Undefined | accord | 0 | 1 | 1 | 0 | \spawn deployable 794 |
| 795 | - | None | Undefined | accord | 0 | 1 | 1 | 0 | \spawn deployable 795 |
| 796 | Trollstone | Consumer Fireworks | Undefined | accord | 0 | 1 | 1 | 0 | \spawn deployable 796 |
| 797 | Melding Anomaly | None | Undefined | friendly | 1250 | 500000 | 1 | 0 | \spawn deployable 797 |
| 798 | - | None | Undefined | accord | 0 | 1 | 5 | 0 | \spawn deployable 798 |
| 799 | Watchtower Terminal | None | Interactable Objective | accord | 0 | 0 | 1 | 0 | \spawn deployable 799 |
| 804 | Signal Scanning Antenna | None | Undefined | accord | 0 | 1 | 1 | 1000 | \spawn deployable 804 |
| 805 | - | None | Undefined | accord | 0 | 1 | 1 | 0 | \spawn deployable 805 |
| 806 | - | None | Undefined | accord | 0 | 1 | 1 | 0 | \spawn deployable 806 |
| 807 | - | Surface Deposit | Undefined | friendly | 0.125 | 50 | 1 | 0 | \spawn deployable 807 |
| 809 | Baneclaw Dome - No Shoot In | None | Undefined | melding | 0 | 0 | 1 | 0 | \spawn deployable 809 |
| 811 | - | None | Undefined | accord | 0 | 0 | 1 | 0 | \spawn deployable 811 |
| 812 | Quicksilver Glider Pad | Adventurer's Glider Pad | Undefined | accord | 0.75 | 1 | 1 | 0 | \spawn deployable 812 |
| 813 | - | None | Undefined | accord | 0 | 0 | 1 | 0 | \spawn deployable 813 |
| 814 | Anel Vermelho da Morte Pyrotechnic Launcher | Consumer Fireworks | Undefined | accord | 2.5 | 0 | 0.1 | 0 | \spawn deployable 814 |
| 815 | Anel Verde da Vida Pyrotechnics Launcher | Consumer Fireworks | Undefined | accord | 2.5 | 0 | 0.1 | 0 | \spawn deployable 815 |
| 816 | - | None | Target (primary) | accord | 50 | 1 | 1 | 12000 | \spawn deployable 816 |
| 817 | - | None | Undefined | accord | 0.0025 | 1 | 1.45 | 0 | \spawn deployable 817 |
| 818 | - | None | Undefined | accord | 0.0025 | 1 | 1.45 | 0 | \spawn deployable 818 |
| 819 | Baneclaw Dome - One-Way collision | None | Undefined | melding | 0 | 0 | 1 | 0 | \spawn deployable 819 |
| 820 | Race Terminal | None | Undefined | accord | 0 | 1 | 1 | 0 | \spawn deployable 820 |
| 821 | Crashed Thumper | None | Undefined | accord | 50 | 1 | 1 | 12000 | \spawn deployable 821 |
| 822 | Crashed Thumper | None | Undefined | accord | 50 | 1 | 1 | 12000 | \spawn deployable 822 |
| 823 | Crashed Thumper | None | Undefined | accord | 50 | 1 | 1 | 12000 | \spawn deployable 823 |
| 824 | - | None | Deployed Target | melding | 25 | 10000 | 1.25 | 3000 | \spawn deployable 824 |
| 825 | - | None | Undefined | accord | 0.0025 | 1 | 0.89 | 0 | \spawn deployable 825 |
| 826 | - | None | Undefined | accord | 0.0025 | 1 | 0.89 | 0 | \spawn deployable 826 |
| 828 | Starter Packs | None | Undefined | accord | 0 | 0 | 1.23 | 0 | \spawn deployable 828 |
| 829 | - | None | Undefined | accord | 0.0025 | 1 | 1 | 0 | \spawn deployable 829 |
| 830 | - | None | Undefined | accord | 0.0025 | 1 | 1 | 0 | \spawn deployable 830 |
| 831 | - | None | Undefined | accord | 0.0025 | 1 | 1 | 0 | \spawn deployable 831 |
| 832 | - | None | Undefined | accord | 0.0025 | 1 | 1 | 0 | \spawn deployable 832 |
| 833 | - | None | Undefined | accord | 0.0025 | 1 | 1 | 0 | \spawn deployable 833 |
| 834 | Crashed Thumper | None | Undefined | accord | 50 | 1 | 1 | 0 | \spawn deployable 834 |
| 835 | - | None | Rest | accord | 0 | 0 | 1 | 0 | \spawn deployable 835 |
| 836 | Portable Antenna Terminal | None | Undefined | accord | 0 | 1 | 1 | 0 | \spawn deployable 836 |
| 837 | - | None | Undefined | accord | 0.0125 | 1 | 0.1 | 0 | \spawn deployable 837 |
| 838 | - | None | Undefined | accord | 50 | 1 | 1 | 0 | \spawn deployable 838 |
| 839 | - | None | Undefined | accord | 0 | 1 | 5 | 0 | \spawn deployable 839 |
| 840 | - | Glider pad | Undefined | accord | 0.75 | 1 | 1 | 0 | \spawn deployable 840 |
| 841 | Game of Chance | None | Undefined | accord | 0 | 0 | 1 | 0 | \spawn deployable 841 |
| 842 | - | Glider pad | Undefined | accord | 0.75 | 1 | 1 | 0 | \spawn deployable 842 |
| 843 | - | None | Undefined | accord | 0 | 1 | 1 | 0 | \spawn deployable 843 |
| 844 | - | None | Undefined | melding | 0 | 0 | 1 | 0 | \spawn deployable 844 |
| 845 | - | None | Undefined | accord | 1.25 | 1 | 1 | 500 | \spawn deployable 845 |
| 847 | Happy New Years Fireworks | Consumer Fireworks | Undefined | accord | 0 | 1 | 1 | 0 | \spawn deployable 847 |
| 849 | _Aranhas Worker Lair + Spawns | None | Undefined | gaea | 25 | 10000 | 0.8 | 3000 | \spawn deployable 849 |
| 850 | - | None | Undefined | accord | 0.0025 | 1 | 1 | 1 | \spawn deployable 850 |
| 851 | - | None | Undefined | accord | 0.0025 | 1 | 1 | 1 | \spawn deployable 851 |
| 852 | - | None | Undefined | accord | 0.0025 | 1 | 1 | 1 | \spawn deployable 852 |
| 853 | - | Shield | Undefined | bandit | 8.75 | 3500 | 1 | 0 | \spawn deployable 853 |
| 854 | - | Mine | Undefined | chosen | 1.5 | 1 | 1 | 2000 | \spawn deployable 854 |
| 855 | Seasons Greetings Fireworks | Consumer Fireworks | Undefined | accord | 0 | 1 | 1 | 0 | \spawn deployable 855 |
| 856 | - | Consumer Fireworks | Undefined | accord | 0 | 1 | 1 | 0 | \spawn deployable 856 |
| 857 | - | Consumer Fireworks | Undefined | accord | 0 | 1 | 1 | 0 | \spawn deployable 857 |
| 858 | - | None | Undefined | chosen | 3.75 | 1 | 1 | 0 | \spawn deployable 858 |
| 859 | - | None | Undefined | accord | 3.75 | 1 | 1 | 0 | \spawn deployable 859 |
| 860 | - | None | Undefined | accord | 0 | 0 | 1 | 0 | \spawn deployable 860 |
| 861 | - | None | Undefined | chosen | 0.025 | 10 | 1 | 0 | \spawn deployable 861 |
| 862 | Marauder Arcport Beacon | None | Deployed Target | chosen | 17.5 | 7000 | 1 | 10500 | \spawn deployable 862 |
| 863 | - | None | Undefined | chosen | 0 | 0 | 1 | 0 | \spawn deployable 863 |
| 864 | - | None | Undefined | chosen | 0 | 1 | 1 | 0 | \spawn deployable 864 |
| 866 | - | None | Undefined | accord | 0.0025 | 1 | 0.1 | 0 | \spawn deployable 866 |
| 867 | - | None | Deployed Target | accord | 6 | 1 | 0.7 | 0 | \spawn deployable 867 |
| 868 | - | Mine | Undefined | accord | 1 | 1 | 1 | 0 | \spawn deployable 868 |
| 869 | - | Mine | Undefined | accord | 1 | 1 | 1 | 0 | \spawn deployable 869 |
| 870 | - | None | Deployed Target | accord | 8 | 1 | 0.9 | 0 | \spawn deployable 870 |
| 871 | - | None | Deployed Target | accord | 10 | 1 | 1.1 | 0 | \spawn deployable 871 |
| 875 | - | None | Undefined | accord | 0.0025 | 1 | 1 | 0 | \spawn deployable 875 |
| 876 | Just a terminal for testing with. | None | Undefined | accord | 0 | 1 | 1 | 0 | \spawn deployable 876 |
| 877 | - | None | Undefined | accord | 5.5 | 1 | 1 | 0 | \spawn deployable 877 |
| 879 | - | None | Undefined | accord | 0.0025 | 1 | 1 | 0 | \spawn deployable 879 |
| 880 | - | Shield | Undefined | accord | 5 | 1 | 1 | 0 | \spawn deployable 880 |
| 881 | - | None | Undefined | accord | 0 | 0 | 0.51 | 0 | \spawn deployable 881 |
| 883 | - | None | Undefined | accord | 0 | 0 | 0.28 | 0 | \spawn deployable 883 |
| 884 | - | None | Undefined | accord | 0 | 0 | 0.55 | 0 | \spawn deployable 884 |
| 885 | - | None | Undefined | accord | 0 | 0 | 0.28 | 0 | \spawn deployable 885 |
| 886 | - | None | Undefined | accord | 0.625 | 250 | 1 | 0 | \spawn deployable 886 |
| 887 | - | None | Undefined | accord | 0 | 0 | 1 | 0 | \spawn deployable 887 |
| 890 | - | None | Undefined | gaea | 25 | 10000 | 1.25 | 3000 | \spawn deployable 890 |
| 891 | - | Mine | Undefined | accord | 5 | 1 | 1 | 2000 | \spawn deployable 891 |
| 892 | Race Terminal | None | Undefined | accord | 0 | 1 | 1 | 0 | \spawn deployable 892 |
| 893 | Finish line | FinishLine | Undefined | accord | 0 | 1 | 1 | 0 | \spawn deployable 893 |
| 894 | Overclocking Station | None | Undefined | accord | 1.25 | 500 | 1 | 0 | \spawn deployable 894 |
| 895 | - | None | Undefined | accord | 0 | 1 | 0.37 | 0 | \spawn deployable 895 |
| 896 | - | None | Undefined | accord | 0 | 0 | 1 | 0 | \spawn deployable 896 |
| 897 | - | Turret | Fixed Weapon | accord | 3.5 | 700 | 1.3 | 5000 | \spawn deployable 897 |
| 898 | - | None | Undefined | accord | 0.75 | 1 | 1 | 0 | \spawn deployable 898 |
| 899 | Sentinel Pod | Sentinel Pod | Deployed Target | accord | 1 | 400 | 1 | 3000 | \spawn deployable 899 |
| 905 | _Battlelab Platform | None | Rest | accord | 0.0025 | 1 | 1 | 0 | \spawn deployable 905 |
| 906 | BattleLab - Jumping Puzzle | None | Undefined | accord | 0 | 1 | 1 | 0 | \spawn deployable 906 |
| 909 | - | Turret | Undefined | neutral | 0.75 | 1 | 1 | 0 | \spawn deployable 909 |
| 910 | - | Shield | Undefined | accord | 5 | 1 | 1 | 0 | \spawn deployable 910 |
| 911 | Accord Assault Rifle - Press E to Pick up | None | Interactable Objective | accord | 0 | 1 | 2 | 0 | \spawn deployable 911 |
| 918 | Chosen Artillery Cannon | None | Fixed Weapon | chosen | 0 | 1 | 1 | 3000 | \spawn deployable 918 |
| 921 | - | Turret | Undefined | accord | 0.0025 | 1 | 1 | 0 | \spawn deployable 921 |
| 922 | Artillery Terminal | None | Interactable Objective | accord | 0 | 0 | 1 | 0 | \spawn deployable 922 |
| 923 | _Tutorial - Crouch Turret | Turret | Fixed Weapon | friendly | 0 | 0 | 1.3 | 5000 | \spawn deployable 923 |
| 924 | - | Turret | Play | accord | 0 | 1 | 1 | 0 | \spawn deployable 924 |
| 929 | - | None | Undefined | accord | 0.0025 | 1 | 1 | 0 | \spawn deployable 929 |
| 930 | Chosen Shield Deployable | None | Undefined | chosen | 10 | 1 | 1 | 0 | \spawn deployable 930 |
| 931 | - | None | Undefined | accord | 0 | 1 | 1 | 0 | \spawn deployable 931 |
| 932 | _Tutorial Door 10x5 | None | Undefined | accord | 0 | 1 | 1 | 0 | \spawn deployable 932 |
| 933 | Warbringer | Spawner | Undefined | chosen | 55 | 22000 | 1 | 15000 | \spawn deployable 933 |
| 934 | - | None | Fixed Weapon | accord | 1 | 400 | 0.5 | 300 | \spawn deployable 934 |
| 936 | - | Datapads | Target (primary) | neutral | 0.75 | 300 | 1 | 0 | \spawn deployable 936 |
| 938 | - | None | Interactable Objective | accord | 0.0025 | 1 | 1 | 0 | \spawn deployable 938 |
| 939 | Sabotage Stolen Weapons Cache | Turret | Play | accord | 0 | 1 | 2 | 0 | \spawn deployable 939 |
| 940 | Energy Wall | Shield | Deployed Target | accord | 1 | 1000 | 1.5 | 0 | \spawn deployable 940 |
| 941 | - | None | Cover | accord | 0 | 0 | 1 | 0 | \spawn deployable 941 |
| 942 | - | None | Fixed Weapon | chosen | 0 | 1 | 1 | 0 | \spawn deployable 942 |
| 943 | - | None | Undefined | accord | 0 | 1 | 3 | 0 | \spawn deployable 943 |
| 944 | - | None | Undefined | accord | 0 | 1 | 3 | 0 | \spawn deployable 944 |
| 945 | Jumpjet Terminal | None | Undefined | accord | 0 | 1 | 0.25 | 0 | \spawn deployable 945 |
| 946 | Heavy Turret - Rank I | Turret | Fixed Weapon | accord | 2.5 | 1 | 1.3 | 1000 | \spawn deployable 946 |
| 947 | Heavy Turret - Rank II | Turret | Fixed Weapon | accord | 4 | 1600 | 1.3 | 1000 | \spawn deployable 947 |
| 948 | Chosen Strifebringer | Turret | Play | accord | 0 | 1 | 1 | 0 | \spawn deployable 948 |
| 952 | Melding Fragment | None | Undefined | accord | 0.0025 | 1 | 1 | 0 | \spawn deployable 952 |
| 953 | Accord Shield Disruptor | None | Target (primary) | accord | 22.5 | 9000 | 3 | 0 | \spawn deployable 953 |
| 955 | Arcporter Pylon | None | Undefined | accord | 0 | 1 | 1 | 0 | \spawn deployable 955 |
| 956 | - | Mannable Turret | Fixed Weapon | accord | 15 | 6000 | 1 | 0 | \spawn deployable 956 |
| 957 | Accord Shield Disruptor | None | Target (primary) | accord | 22.5 | 9000 | 3 | 0 | \spawn deployable 957 |
| 958 | - | None | Rest | accord | 0 | 0 | 1 | 0 | \spawn deployable 958 |
| 959 | Guard | None | Rest | accord | 0 | 0 | 1 | 0 | \spawn deployable 959 |
| 960 | Artillery Spawn Pad | None | Undefined | chosen | 0 | 1 | 1 | 0 | \spawn deployable 960 |
| 961 | Armored Aranha Nest | Turret | Play | gaea | 0 | 1 | 2 | 0 | \spawn deployable 961 |
| 963 | - | None | Undefined | accord | 0.0025 | 1 | 2 | 0 | \spawn deployable 963 |
| 964 | Hijacked Terminal | None | Undefined | chosen | 0 | 0 | 1 | 0 | \spawn deployable 964 |
| 965 | - | None | Undefined | accord | 2.5 | 1 | 1 | 0 | \spawn deployable 965 |
| 966 | Accord Shield Disruptor | None | Target (primary) | accord | 46.25 | 18500 | 3 | 0 | \spawn deployable 966 |
| 967 | Accord Shield Disruptor | None | Target (primary) | accord | 46.25 | 18500 | 3 | 0 | \spawn deployable 967 |
| 969 | - | Turret | Fixed Weapon | friendly | 0 | 0 | 1.3 | 5000 | \spawn deployable 969 |
| 970 | - | None | Undefined | accord | 0.25 | 100 | 1 | 0 | \spawn deployable 970 |
| 971 | - | None | Undefined | accord | 0 | 0 | 1 | 0 | \spawn deployable 971 |
| 974 | - | Mine | Undefined | accord | 1 | 1 | 1 | 0 | \spawn deployable 974 |
| 976 | Artillery Terminal | None | Undefined | accord | 0 | 0 | 1 | 0 | \spawn deployable 976 |
| 979 | Chosen Shield Deployable | None | Undefined | chosen | 250 | 1 | 1 | 0 | \spawn deployable 979 |
| 980 | _Tutorial - Crouch Turret | Turret | Fixed Weapon | monster | 0 | 0 | 1.3 | 5000 | \spawn deployable 980 |
| 981 | Tether Field - Area | None | Undefined | monster | 1 | 1 | 1 | 0 | \spawn deployable 981 |
| 982 | Melding Portal | None | Deployed Target | chosen | 3.5 | 1 | 0.3 | 0 | \spawn deployable 982 |
| 983 | Engineer Anti-Personnel Turret | Anti-Personnel Turret | Fixed Weapon | accord | 2000 | 2000 | 2 | 2500 | \spawn deployable 983 |
| 984 | - | Datapads | Undefined | melding | 0 | 0 | 1 | 0 | \spawn deployable 984 |
| 985 | - | None | Undefined | accord | 3.5 | 1 | 1 | 0 | \spawn deployable 985 |
| 986 | - | None | Undefined | accord | 3.5 | 1 | 1 | 0 | \spawn deployable 986 |
| 987 | - | None | Undefined | accord | 3.5 | 1 | 1 | 0 | \spawn deployable 987 |
| 988 | Accord Assault Terminal | None | Undefined | accord | 0 | 1 | 1 | 0 | \spawn deployable 988 |
| 989 | Accord Engineer Battleframe | None | Undefined | accord | 0 | 1 | 1 | 0 | \spawn deployable 989 |
| 990 | Accord Biotech Battleframe | None | Undefined | accord | 0 | 1 | 1 | 0 | \spawn deployable 990 |
| 991 | Accord Dreadnaught Battleframe | None | Undefined | accord | 0 | 1 | 1 | 0 | \spawn deployable 991 |
| 992 | Accord Recon Battleframe | None | Undefined | accord | 0 | 1 | 1 | 0 | \spawn deployable 992 |
| 993 | _Ultimate Ability Powerup (Max) | None | Undefined | accord | 0 | 1 | 1.5 | 0 | \spawn deployable 993 |
| 994 | - | None | Undefined | accord | 0 | 1 | 1.5 | 0 | \spawn deployable 994 |
| 995 | - | None | Undefined | accord | 0 | 1 | 1 | 0 | \spawn deployable 995 |
| 996 | - | None | Undefined | accord | 0 | 1 | 1 | 0 | \spawn deployable 996 |
| 997 | - | None | Undefined | accord | 0 | 1 | 1 | 0 | \spawn deployable 997 |
| 998 | Spawns a beacon that indicated the artillery strike target area. | Mine | Undefined | accord | 5 | 1 | 1 | 500 | \spawn deployable 998 |
| 999 | _Tutorial Door 8x5 | None | Undefined | accord | 0 | 1 | 1 | 0 | \spawn deployable 999 |
| 1000 | Chosen Bomb | Turret | Play | accord | 0 | 1 | 1 | 0 | \spawn deployable 1000 |
| 1001 | - | None | Undefined | accord | 0 | 0 | 1 | 0 | \spawn deployable 1001 |
| 1002 | Melding Cave Deposit (Level 1) | None | Undefined | melding | 3.125 | 1 | 1 | 0 | \spawn deployable 1002 |
| 1003 | Melding Cave Deposit (Level 2) | None | Undefined | melding | 4.375 | 1 | 1 | 0 | \spawn deployable 1003 |
| 1004 | Melding Cave Deposit (Level 3) | None | Undefined | melding | 6.25 | 1 | 1 | 0 | \spawn deployable 1004 |
| 1005 | Raider Distress Beacon | None | Interactable Objective | accord | 0 | 1 | 1 | 0 | \spawn deployable 1005 |
| 1006 | - | Forge | Undefined | accord | 0 | 1 | 0.6 | 0 | \spawn deployable 1006 |
| 1007 | Speed Boost Station | None | Undefined | accord | 1.25 | 500 | 1 | 0 | \spawn deployable 1007 |
| 1008 | - | None | Undefined | accord | 0 | 1 | 1 | 0 | \spawn deployable 1008 |
| 1009 | - | None | Rest | accord | 0 | 0 | 1 | 0 | \spawn deployable 1009 |
| 1010 | - | None | Line Rest 1 | accord | 0 | 0 | 1 | 0 | \spawn deployable 1010 |
| 1011 | - | None | Line Rest 2 | accord | 0 | 0 | 1 | 0 | \spawn deployable 1011 |
| 1012 | - | None | Guard Stance | accord | 0 | 0 | 1 | 0 | \spawn deployable 1012 |
| 1013 | - | None | Rest | accord | 0 | 0 | 1 | 0 | \spawn deployable 1013 |
| 1014 | Claymore | None | Undefined | accord | 0.5 | 1 | 1 | 0 | \spawn deployable 1014 |
| 1015 | Arcporter Pylon | None | Undefined | accord | 0 | 1 | 1 | 0 | \spawn deployable 1015 |
| 1016 | - | None | Undefined | accord | 0 | 1 | 5 | 0 | \spawn deployable 1016 |
| 1017 | - | None | Undefined | accord | 0 | 1 | 5 | 0 | \spawn deployable 1017 |
| 1018 | Healing Generator | Repair Station | Deployed Target | accord | 0.25 | 100 | 1 | 0 | \spawn deployable 1018 |
| 1019 | - | None | Undefined | accord | 0 | 1 | 1 | 0 | \spawn deployable 1019 |
| 1020 | Outpost Arcporter Pylon | None | Undefined | accord | 0 | 1 | 1 | 0 | \spawn deployable 1020 |
| 1021 | Unpowered Outpost Arcporter Pylon | None | Undefined | accord | 0 | 1 | 1 | 0 | \spawn deployable 1021 |
| 1023 | Accord Terminal | None | Undefined | accord | 0 | 1 | 1 | 0 | \spawn deployable 1023 |
| 1024 | Bandit Terminal | None | Undefined | accord | 0 | 1 | 1 | 0 | \spawn deployable 1024 |
| 1025 | Blackwater Repulsor Amplifier | None | Target (primary) | accord | 42.5 | 17000 | 1 | 1 | \spawn deployable 1025 |
| 1026 | AngelWings Glider Pad | Glider pad | Undefined | accord | 0.75 | 1 | 1 | 0 | \spawn deployable 1026 |
| 1027 | - | Forge | Undefined | accord | 0 | 1 | 1 | 0 | \spawn deployable 1027 |
| 1028 | - | Tech Turret | Undefined | accord | 0 | 1 | 1 | 0 | \spawn deployable 1028 |
| 1030 | - | Spawner | Undefined | melding | 1.25 | 500 | 0.25 | 0 | \spawn deployable 1030 |
| 1033 | - | Tech Turret | Target (tertiary) | accord | 10 | 4000 | 1.5 | 0 | \spawn deployable 1033 |
| 1034 | - | None | Undefined | accord | 0 | 1 | 0.5 | 0 | \spawn deployable 1034 |
| 1035 | Chosen SIN Detector | Tech SIN | Deployed Target | accord | 0 | 1 | 1 | 5000 | \spawn deployable 1035 |
| 1036 | Chosen SIN Detector Tech Point | Tech SIN | Undefined | accord | 0 | 1 | 1 | 10000 | \spawn deployable 1036 |
| 1037 | Siphon Chain Core | None | Target (primary) | chosen | 1.5 | 500 | 0.75 | 0 | \spawn deployable 1037 |
| 1038 | - | None | Interactable Objective | chosen | 0 | 0 | 1 | 0 | \spawn deployable 1038 |
| 1039 | Accord Terminal | None | Undefined | accord | 0 | 1 | 1 | 0 | \spawn deployable 1039 |
| 1040 | Bandit Terminal | None | Undefined | accord | 0 | 1 | 1 | 0 | \spawn deployable 1040 |
| 1041 | Reactor Control Panel | None | Undefined | neutral | 0 | 0 | 1 | 0 | \spawn deployable 1041 |
| 1042 | Oilspill's Dropship | None | Undefined | accord | 0 | 0 | 1 | 0 | \spawn deployable 1042 |
| 1043 | Blackwater Repulsor Terminal | None | Interactable Objective | accord | 0 | 0 | 1 | 0 | \spawn deployable 1043 |
| 1045 | Weapon Rack | None | Undefined | accord | 0 | 0 | 1 | 0 | \spawn deployable 1045 |
| 1046 | - | None | Undefined | accord | 0 | 1 | 1.2 | 0 | \spawn deployable 1046 |
| 1047 | Unpowered Warfront Arcfolder Pylon | None | Undefined | accord | 0 | 1 | 1 | 0 | \spawn deployable 1047 |
| 1048 | Warfront Arcfolder | None | Undefined | accord | 0 | 1 | 1 | 0 | \spawn deployable 1048 |
| 1049 | SIN Power Link | None | Undefined | accord | 0 | 1 | 1 | 0 | \spawn deployable 1049 |
| 1050 | Weapon Rack | None | Guard Stance | accord | 0 | 0 | 1 | 0 | \spawn deployable 1050 |
| 1051 | - | None | Guard Stance | accord | 0 | 0 | 1 | 0 | \spawn deployable 1051 |
| 1052 | Intact Drill | None | Undefined | accord | 0 | 0 | 1 | 0 | \spawn deployable 1052 |
| 1053 | - | None | Rest | accord | 0 | 0 | 1 | 0 | \spawn deployable 1053 |
| 1054 | - | None | Rest | accord | 0 | 0 | 1 | 0 | \spawn deployable 1054 |
| 1055 | - | None | Rest | accord | 0 | 0 | 1 | 0 | \spawn deployable 1055 |
| 1056 | - | None | Bar | accord | 0 | 0 | 1 | 0 | \spawn deployable 1056 |
| 1057 | - | None | Bar | accord | 0 | 0 | 1 | 0 | \spawn deployable 1057 |
| 1058 | - | None | Bar | accord | 0 | 0 | 1 | 0 | \spawn deployable 1058 |
| 1059 | - | None | Bar | accord | 0 | 0 | 1 | 0 | \spawn deployable 1059 |
| 1060 | - | None | Bar | accord | 0 | 0 | 1 | 0 | \spawn deployable 1060 |
| 1061 | - | None | Rest | accord | 0 | 0 | 1 | 0 | \spawn deployable 1061 |
| 1062 | - | None | Rest | accord | 0 | 0 | 1 | 0 | \spawn deployable 1062 |
| 1063 | - | None | Recreation | accord | 0 | 0 | 1 | 0 | \spawn deployable 1063 |
| 1064 | - | None | Recreation | accord | 0 | 0 | 1 | 0 | \spawn deployable 1064 |
| 1065 | - | None | Recreation | accord | 0 | 0 | 1 | 0 | \spawn deployable 1065 |
| 1066 | - | None | Recreation | accord | 0 | 0 | 1 | 0 | \spawn deployable 1066 |
| 1067 | - | None | Medical Aid | accord | 0 | 0 | 1 | 0 | \spawn deployable 1067 |
| 1068 | - | None | Medical Aid | accord | 0 | 0 | 1 | 0 | \spawn deployable 1068 |
| 1069 | - | None | Rummage | accord | 0 | 0 | 1 | 0 | \spawn deployable 1069 |
| 1070 | - | None | Work | accord | 0 | 0 | 1 | 0 | \spawn deployable 1070 |
| 1071 | - | None | Work | accord | 0 | 0 | 1 | 0 | \spawn deployable 1071 |
| 1072 | - | None | Work | accord | 0 | 0 | 1 | 0 | \spawn deployable 1072 |
| 1073 | - | None | Work | accord | 0 | 0 | 1 | 0 | \spawn deployable 1073 |
| 1074 | - | None | Work | accord | 0 | 0 | 1 | 0 | \spawn deployable 1074 |
| 1075 | - | None | Repair Work | accord | 0 | 0 | 1 | 0 | \spawn deployable 1075 |
| 1076 | - | None | Work | accord | 0 | 0 | 1 | 0 | \spawn deployable 1076 |
| 1077 | - | None | Work | accord | 0 | 0 | 1 | 0 | \spawn deployable 1077 |
| 1078 | - | None | Repair Work | accord | 0 | 0 | 1 | 0 | \spawn deployable 1078 |
| 1079 | DiamondWing Glider Pad | Glider pad | Undefined | accord | 0.75 | 1 | 1 | 0 | \spawn deployable 1079 |
| 1080 | _Health Powerup (Medium, 5 min respawn) | None | Undefined | accord | 0 | 1 | 1.5 | 0 | \spawn deployable 1080 |
| 1081 | _Ammo Powerup (1 min respawn) | None | Undefined | accord | 0 | 1 | 1 | 0 | \spawn deployable 1081 |
| 1082 | - | None | Rummage | accord | 0 | 0 | 1 | 0 | \spawn deployable 1082 |
| 1083 | - | None | Medical Aid | accord | 0 | 0 | 1 | 0 | \spawn deployable 1083 |
| 1084 | - | None | Market | accord | 0 | 0 | 1 | 0 | \spawn deployable 1084 |
| 1085 | - | None | Market | accord | 0 | 0 | 1 | 0 | \spawn deployable 1085 |
| 1086 | - | None | Market | accord | 0 | 0 | 1 | 0 | \spawn deployable 1086 |
| 1087 | Crystalwing Glider Pad | Glider pad | Undefined | accord | 0.75 | 1 | 1 | 0 | \spawn deployable 1087 |
| 1088 | - | None | Rest | accord | 0 | 0 | 1 | 0 | \spawn deployable 1088 |
| 1089 | Siphon Beam Emitter | None | Undefined | chosen | 2.5 | 500 | 1 | 0 | \spawn deployable 1089 |
| 1090 | - | None | Undefined | chosen | 0 | 0 | 1 | 0 | \spawn deployable 1090 |
| 1091 | - | None | Undefined | chosen | 0 | 0 | 1 | 0 | \spawn deployable 1091 |
| 1092 | - | None | Undefined | chosen | 0 | 0 | 1 | 0 | \spawn deployable 1092 |
| 1093 | Siphon Focus Invisible Object | None | Undefined | chosen | 0 | 1 | 1 | 0 | \spawn deployable 1093 |
| 1094 | - | None | Undefined | accord | 0 | 1 | 1.25 | 0 | \spawn deployable 1094 |
| 1095 | - | None | Undefined | friendly | 0 | 1 | 1 | 0 | \spawn deployable 1095 |
| 1096 | - | None | Interactable Objective | chosen | 0 | 0 | 1 | 0 | \spawn deployable 1096 |
| 1097 | - | None | Repair Work | accord | 0 | 0 | 1 | 0 | \spawn deployable 1097 |
| 1098 | - | None | Repair Work | accord | 0 | 0 | 1 | 0 | \spawn deployable 1098 |
| 1099 | NPC_Emote_ProgrammingTech_01 | None | Work | accord | 0 | 0 | 1 | 0 | \spawn deployable 1099 |
| 1100 | NPC_Emote_DataEntry_01 | None | Repair Work | accord | 0 | 0 | 1 | 0 | \spawn deployable 1100 |
| 1103 | AI-Possessed Terminal | None | Undefined | chosen | 0 | 0 | 1 | 0 | \spawn deployable 1103 |
| 1104 | - | None | Work | accord | 0 | 0 | 1 | 0 | \spawn deployable 1104 |
| 1105 | - | None | Rummage | accord | 0 | 0 | 1 | 0 | \spawn deployable 1105 |
| 1106 | - | None | Rummage | accord | 0 | 0 | 1 | 0 | \spawn deployable 1106 |
| 1107 | - | None | Rummage | accord | 0 | 0 | 1 | 0 | \spawn deployable 1107 |
| 1108 | - | None | Rummage | accord | 0 | 0 | 1 | 0 | \spawn deployable 1108 |
| 1109 | Anti-Personnel Auto-Turret | Tech Turret | Fixed Weapon | accord | 7.5 | 3000 | 1.5 | 5000 | \spawn deployable 1109 |
| 1110 | Anti-Personnel Auto-Turret | Tech Turret | Fixed Weapon | chosen | 7.5 | 3000 | 1.5 | 5000 | \spawn deployable 1110 |
| 1111 | Chosen Siphon | None | Undefined | neutral | 0 | 0 | 1 | 0 | \spawn deployable 1111 |
| 1112 | Liberated AI Terminal | None | Undefined | accord | 0 | 0 | 1 | 0 | \spawn deployable 1112 |
| 1114 | - | None | Undefined | accord | 0.0025 | 1 | 1 | 0 | \spawn deployable 1114 |
| 1115 | Chosen Chain Platform | None | Undefined | neutral | 0 | 0 | 1 | 0 | \spawn deployable 1115 |
| 1116 | Blackwater Repulsor Terminal | None | Undefined | accord | 0 | 0 | 1 | 0 | \spawn deployable 1116 |
| 1117 | Chosen Siphon | None | Undefined | neutral | 0 | 1 | 1 | 0 | \spawn deployable 1117 |
| 1118 | Terminal | None | Undefined | chosen | 0 | 0 | 1 | 0 | \spawn deployable 1118 |
| 1119 | Heart Terminal | None | Undefined | chosen | 0 | 0 | 1 | 0 | \spawn deployable 1119 |
| 1120 | Siphon Focus | None | Undefined | neutral | 0 | 0 | 1 | 0 | \spawn deployable 1120 |
| 1121 | Tainted Crystite Storage | None | Undefined | accord | 0 | 0 | 1 | 0 | \spawn deployable 1121 |
| 1122 | Crystite Core Storage | None | Undefined | accord | 0 | 0 | 1 | 0 | \spawn deployable 1122 |
| 1123 | Medical Supply Storage | None | Undefined | accord | 0 | 0 | 1 | 0 | \spawn deployable 1123 |
| 1124 | - | None | Undefined | accord | 0 | 0 | 1 | 0 | \spawn deployable 1124 |
| 1125 | _Locked Watchtower Terminal | None | Interactable Objective | accord | 0 | 0 | 1 | 0 | \spawn deployable 1125 |
| 1126 | - | Mannable Turret | Fixed Weapon | chosen | 6.25 | 2500 | 1 | 1000 | \spawn deployable 1126 |
| 1127 | Necropolis Spike | None | Undefined | accord | 0 | 0 | 0.7 | 0 | \spawn deployable 1127 |
| 1128 | Chosen Barricade | None | Undefined | neutral | 0 | 1 | 1 | 0 | \spawn deployable 1128 |
| 1129 | - | None | Undefined | neutral | 0 | 1 | 1 | 250 | \spawn deployable 1129 |
| 1133 | - | None | Undefined | neutral | 0 | 1 | 1 | 0 | \spawn deployable 1133 |
| 1134 | Chosen Harvester Chain B | None | Undefined | neutral | 0 | 1 | 1 | 0 | \spawn deployable 1134 |
| 1135 | - | None | Undefined | neutral | 0 | 1 | 1 | 0 | \spawn deployable 1135 |
| 1136 | - | None | Undefined | accord | 0 | 1 | 3 | 0 | \spawn deployable 1136 |
| 1137 | Intro Dropship Activator | None | Undefined | accord | 0 | 1 | 1 | 0 | \spawn deployable 1137 |
| 1138 | - | None | Driver Seat | accord | 0 | 0 | 1 | 0 | \spawn deployable 1138 |
| 1139 | Chemical Analysis Matrix | Generic Terminal | Play | accord | 0 | 1 | 1 | 0 | \spawn deployable 1139 |
| 1140 | Chemistry Set | Manufacturing Terminal | Play | accord | 0 | 1 | 0.9 | 0 | \spawn deployable 1140 |
| 1141 | - | None | Undefined | chosen | 0 | 0 | 15 | 0 | \spawn deployable 1141 |
| 1142 | Melding Repulsor Terminal | None | Interactable Objective | accord | 0 | 0 | 1 | 0 | \spawn deployable 1142 |
| 1143 | _Melding Repulsor Terminal | None | Undefined | accord | 0 | 0 | 1 | 0 | \spawn deployable 1143 |
| 1144 | Treasure Vault | Turret | Play | accord | 0 | 1 | 2.5 | 0 | \spawn deployable 1144 |
| 1145 | - | None | Undefined | accord | 0 | 1 | 1 | 0 | \spawn deployable 1145 |
| 1146 | Melding Repulsor Terminal | None | Undefined | accord | 0 | 0 | 1 | 0 | \spawn deployable 1146 |
| 1147 | SIN Imprint: Blackwater Anomaly 01 | Datapads | Interactable Objective | accord | 0 | 0 | 1 | 0 | \spawn deployable 1147 |
| 1148 | SIN Imprint: Blackwater Anomaly 02 | Datapads | Interactable Objective | accord | 0 | 0 | 1 | 0 | \spawn deployable 1148 |
| 1149 | SIN Imprint: Blackwater Anomaly 03 | Datapads | Interactable Objective | accord | 0 | 0 | 1 | 0 | \spawn deployable 1149 |
| 1150 | SIN Imprint: Blackwater Anomaly 04 | Datapads | Interactable Objective | accord | 0 | 0 | 1 | 0 | \spawn deployable 1150 |
| 1151 | SIN Imprint: Blackwater Anomaly 05 | Datapads | Interactable Objective | accord | 0 | 0 | 1 | 0 | \spawn deployable 1151 |
| 1152 | Melding Repulsor Terminal | None | Undefined | accord | 0 | 0 | 1 | 0 | \spawn deployable 1152 |
| 1153 | Chosen Energy Bomb | None | Undefined | accord | 0 | 1 | 1 | 0 | \spawn deployable 1153 |
| 1154 | - | None | Interactable Objective | neutral | 0 | 0 | 1 | 0 | \spawn deployable 1154 |
| 1155 | - | None | Undefined | gaea | 7.5 | 3000 | 1 | 0 | \spawn deployable 1155 |
| 1156 | - | None | Undefined | gaea | 25 | 10000 | 2 | 0 | \spawn deployable 1156 |
| 1157 | - | None | Undefined | gaea | 25 | 10000 | 1.25 | 3000 | \spawn deployable 1157 |
| 1158 | Challenge Terminal | None | Undefined | accord | 0 | 1 | 1 | 0 | \spawn deployable 1158 |
| 1159 | - | None | Undefined | accord | 0 | 1 | 2 | 0 | \spawn deployable 1159 |
| 1160 | - | None | Undefined | accord | 0 | 1 | 2 | 0 | \spawn deployable 1160 |
| 1161 | - | None | Target (primary) | accord | 52.5 | 18900 | 2 | 0 | \spawn deployable 1161 |
| 1162 | Tutorial Turret | Turret | Fixed Weapon | gaea | 1.25 | 500 | 1.3 | 5000 | \spawn deployable 1162 |
| 1163 | Tectonic Agitator Control Tower | None | Undefined | accord | 0 | 1 | 1 | 0 | \spawn deployable 1163 |
| 1164 | Agitator Power Generator | None | Target (primary) | accord | 20 | 8000 | 2 | 0 | \spawn deployable 1164 |
| 1165 | - | None | Undefined | accord | 0 | 1 | 0.15 | 0 | \spawn deployable 1165 |
| 1166 | - | None | Target (primary) | accord | 65 | 23400 | 0.6 | 0 | \spawn deployable 1166 |
| 1167 | - | None | Undefined | accord | 0 | 1 | 2 | 0 | \spawn deployable 1167 |
| 1168 | Tectonic Agitator Control Tower | None | Undefined | accord | 0 | 1 | 1 | 0 | \spawn deployable 1168 |
| 1169 | - | None | Undefined | accord | 0 | 1 | 0.15 | 0 | \spawn deployable 1169 |
| 1170 | Wrecked Power Generator | None | Undefined | accord | 0 | 1 | 2 | 0 | \spawn deployable 1170 |
| 1171 | Copacabana Arcporter | Arcporter Pylon | Deployed Target | accord | 0 | 1 | 0.15 | 0 | \spawn deployable 1171 |
| 1172 | Cache of Chosen Tech | None | Undefined | accord | 0 | 1 | 1 | 500 | \spawn deployable 1172 |
| 1173 | Melding Fragment Arcfolder | None | Undefined | accord | 0 | 1 | 1 | 0 | \spawn deployable 1173 |
| 1174 | - | None | Interactable Objective | accord | 16.25 | 6500 | 0.6 | 0 | \spawn deployable 1174 |
| 1175 | Chosen Darkslip Theatre Prop | None | Undefined | chosen | 0 | 1 | 2 | 0 | \spawn deployable 1175 |
| 1179 | Jetball Goal | None | Undefined | accord | 0 | 1 | 1 | 0 | \spawn deployable 1179 |
| 1180 | Watchtower Terminal | None | Interactable Objective | accord | 0 | 0 | 1 | 0 | \spawn deployable 1180 |
| 1181 | - | None | Target (primary) | accord | 0 | 1 | 1 | 12000 | \spawn deployable 1181 |
| 1182 | Batsheba's Cache of Tech | None | Undefined | accord | 0 | 1 | 5 | 500 | \spawn deployable 1182 |
| 1183 | Daily Reward Crate | None | Undefined | accord | 0 | 1 | 0.7 | 3000 | \spawn deployable 1183 |
| 1185 | _Agitator Thunderdome | None | Undefined | accord | 0 | 2000 | 1.3 | 0 | \spawn deployable 1185 |
| 1186 | Agitator Power Generator | None | Undefined | accord | 0 | 1 | 2 | 0 | \spawn deployable 1186 |
| 1187 | Chosen Siphon | None | Undefined | neutral | 0 | 1 | 1 | 0 | \spawn deployable 1187 |
| 1188 | Agitator Power Generator | None | Target (primary) | accord | 20 | 8000 | 2 | 0 | \spawn deployable 1188 |
| 1189 | - | None | Target (primary) | accord | 0 | 9000 | 1 | 0 | \spawn deployable 1189 |
| 1190 | - | None | Undefined | melding | 4.375 | 1 | 1 | 0 | \spawn deployable 1190 |
| 1191 | Melding Deposit | None | Undefined | melding | 4.375 | 1 | 1 | 0 | \spawn deployable 1191 |
| 1192 | - | None | Undefined | melding | 4.375 | 1 | 1 | 0 | \spawn deployable 1192 |
| 1194 | - | None | Undefined | accord | 0 | 0 | 1 | 0 | \spawn deployable 1194 |
| 1195 | - | None | Undefined | accord | 0 | 0 | 1 | 0 | \spawn deployable 1195 |
| 1196 | - | None | Undefined | accord | 0 | 0 | 1 | 0 | \spawn deployable 1196 |
| 1197 | Supply Crate | None | Undefined | accord | 0 | 1 | 1 | 0 | \spawn deployable 1197 |
| 1198 | Rare Supply Crate | None | Undefined | accord | 0 | 1 | 3 | 0 | \spawn deployable 1198 |
| 1199 | - | None | Interactable Objective | chosen | 1000 | 2147483647 | 1.5 | 0 | \spawn deployable 1199 |
| 1200 | - | None | Interactable Objective | accord | 0 | 1200 | 1 | 0 | \spawn deployable 1200 |
| 1201 | - | Surface Deposit | Interactable Objective | friendly | 0.375 | 150 | 1 | 0 | \spawn deployable 1201 |
| 1202 | Cancel Thumper | None | Undefined | neutral | 0 | 0 | 1 | 0 | \spawn deployable 1202 |
| 1203 | Jetball Toss Platform | None | Rest | neutral | 0 | 0 | 1 | 0 | \spawn deployable 1203 |
| 1204 | Jetball Toss Goal | None | Undefined | accord | 0 | 1 | 1 | 0 | \spawn deployable 1204 |
| 1205 | HAWK Turret | Turret | Fixed Weapon | chosen | 0 | 0 | 1.5 | 5000 | \spawn deployable 1205 |
| 1206 | - | None | Undefined | accord | 0 | 1 | 1 | 0 | \spawn deployable 1206 |
| 1207 | - | None | Undefined | accord | 0.25 | 1 | 1 | 0 | \spawn deployable 1207 |
| 1208 | Hammer Target | None | Undefined | accord | 0.25 | 1 | 0.4 | 0 | \spawn deployable 1208 |
| 1209 | - | Sentinel Pod | Deployed Target | accord | 1 | 400 | 1 | 3000 | \spawn deployable 1209 |
| 1210 | - | None | Undefined | accord | 0.25 | 1 | 0.1 | 0 | \spawn deployable 1210 |
| 1211 | Overclocking Station - Deployable | None | Undefined | accord | 1.25 | 0 | 1 | 0 | \spawn deployable 1211 |
| 1212 | - | None | Undefined | accord | 0 | 0 | 0.75 | 0 | \spawn deployable 1212 |
| 1213 | - | None | Deployed Target | accord | 12 | 4800 | 3 | 0 | \spawn deployable 1213 |
| 1216 | Resource Crate | None | Undefined | accord | 0 | 1 | 1 | 2500 | \spawn deployable 1216 |
| 1217 | Daily Reward Crate | None | Undefined | accord | 0 | 1 | 1 | 2500 | \spawn deployable 1217 |
| 1218 | - | None | Undefined | accord | 1.25 | 1 | 0.75 | 300 | \spawn deployable 1218 |
| 1219 | - | None | Undefined | accord | 0 | 1 | 1 | 0 | \spawn deployable 1219 |
| 1223 | - | None | Undefined | accord | 0 | 1 | 1 | 0 | \spawn deployable 1223 |
| 1225 | - | None | Rest | accord | 0.0025 | 1 | 0.7 | 0 | \spawn deployable 1225 |
| 1226 | Tutorial Video Drone | None | Undefined | accord | 0 | 1 | 1 | 0 | \spawn deployable 1226 |
| 1227 | Chosen Strifebringer | Turret | Play | accord | 0 | 1 | 1 | 0 | \spawn deployable 1227 |
| 1228 | Chosen Turret | None | Fixed Weapon | chosen | 6 | 2400 | 0.25 | 0 | \spawn deployable 1228 |
| 1229 | Chosen Turret | None | Fixed Weapon | chosen | 6 | 2400 | 1.35 | 0 | \spawn deployable 1229 |
| 1233 | - | None | Interactable Objective | accord | 0 | 1 | 1 | 0 | \spawn deployable 1233 |
| 1234 | Hoverpad | Glider pad | Undefined | accord | 0.75 | 1 | 1 | 0 | \spawn deployable 1234 |
| 1235 | Chosen Generator | None | Target (primary) | chosen | 20 | 8000 | 3 | 0 | \spawn deployable 1235 |
| 1236 | - | None | Fixed Weapon | chosen | 37 | 14800 | 1 | 0 | \spawn deployable 1236 |
| 1237 | - | None | Undefined | accord | 0 | 0 | 1 | 0 | \spawn deployable 1237 |
| 1238 | - | None | Undefined | friendly | 0 | 0 | 1 | 1000 | \spawn deployable 1238 |
| 1239 | Chosen Security Override | None | Undefined | chosen | 3 | 1200 | 1 | 0 | \spawn deployable 1239 |
| 1240 | Chosen Bomb | Turret | Play | accord | 0 | 1 | 1 | 0 | \spawn deployable 1240 |
| 1241 | _Blast Door | None | Undefined | accord | 0 | 0 | 0.85 | 0 | \spawn deployable 1241 |
| 1242 | _Blast Door | None | Undefined | accord | 0 | 0 | 1 | 0 | \spawn deployable 1242 |
| 1243 | _Blast Door | None | Undefined | accord | 0 | 0 | 1 | 0 | \spawn deployable 1243 |
| 1244 | - | None | Undefined | accord | 0 | 1 | 1 | 0 | \spawn deployable 1244 |
| 1245 | - | None | Undefined | neutral | 0.25 | 10 | 1 | 0 | \spawn deployable 1245 |
| 1246 | Crashed Thumper | Medium Thumper | Target (primary) | accord | 20 | 8000 | 1 | 0 | \spawn deployable 1246 |
| 1247 | Vending Machine Terminal | Vending Machine | Undefined | accord | 0 | 1 | 1 | 0 | \spawn deployable 1247 |
| 1248 | Vending Machine Terminal (powered off) | Vending Machine | Undefined | accord | 0 | 1 | 1 | 0 | \spawn deployable 1248 |
| 1249 | Marketplace Terminal | Generic Terminal | Undefined | accord | 0 | 1 | 1 | 0 | \spawn deployable 1249 |
| 1250 | Marketplace Terminal (powered down) | Generic Terminal | Undefined | accord | 0 | 1 | 1 | 0 | \spawn deployable 1250 |
| 1253 | - | None | Undefined | accord | 1 | 1 | 1 | 0 | \spawn deployable 1253 |
| 1254 | - | SIN Tower | Target (secondary) | friendly | 0 | 3000 | 1 | 0 | \spawn deployable 1254 |
| 1255 | Omnicon Banner Dispenser | None | Undefined | accord | 0 | 0 | 1 | 0 | \spawn deployable 1255 |
| 1256 | Target | None | Undefined | neutral | 1 | 400 | 4 | 0 | \spawn deployable 1256 |
| 1257 | - | None | Undefined | accord | 1 | 1 | 1 | 0 | \spawn deployable 1257 |
| 1258 | - | None | Undefined | accord | 0 | 1 | 1 | 0 | \spawn deployable 1258 |
| 1259 | Missile Test Platform | None | Undefined | accord | 0 | 0 | 1 | 0 | \spawn deployable 1259 |
| 1260 | Thumper Cart | None | Undefined | accord | 0 | 1 | 1 | 0 | \spawn deployable 1260 |
| 1261 | - | None | Undefined | accord | 0 | 0 | 0.1 | 0 | \spawn deployable 1261 |
| 1262 | Gate Control | None | Undefined | accord | 0 | 1 | 1 | 0 | \spawn deployable 1262 |
| 1263 | - | None | Undefined | friendly | 0 | 1 | 1 | 0 | \spawn deployable 1263 |
| 1264 | gate | None | Undefined | friendly | 0 | 1 | 1 | 0 | \spawn deployable 1264 |
| 1265 | gate | None | Undefined | friendly | 0 | 1 | 1 | 0 | \spawn deployable 1265 |
| 1266 | - | Datapads | Interactable Objective | accord | 0 | 1 | 1 | 3500 | \spawn deployable 1266 |
| 1267 | - | Turret | Fixed Weapon | Black Hills Bandits | 4 | 1200 | 1.5 | 5000 | \spawn deployable 1267 |
| 1268 | Jump Pad | Rechargable Jump Pad | Undefined | accord | 0.75 | 1 | 1 | 0 | \spawn deployable 1268 |
| 1269 | - | Power Cell Dispenser | Undefined | friendly | 0 | 1 | 1 | 0 | \spawn deployable 1269 |
| 1270 | Melding Repulsor | None | Undefined | friendly | 0 | 0 | 1 | 1 | \spawn deployable 1270 |
| 1271 | SIN Uplink Tower | SIN Tower | Target (primary) | - | 64 | 600 | 1 | 0 | \spawn deployable 1271 |
| 1272 | - | None | Fixed Weapon | chosen | 25 | 10000 | 1 | 0 | \spawn deployable 1272 |
| 1273 | - | Spawner | Undefined | chosen | 0 | 1 | 3 | 0 | \spawn deployable 1273 |
| 1274 | Resource Crate | None | Undefined | accord | 0 | 1 | 1 | 2500 | \spawn deployable 1274 |
| 1275 | - | None | Undefined | accord | 1.25 | 1 | 0.75 | 300 | \spawn deployable 1275 |
| 1276 | - | None | Undefined | accord | 1.25 | 1 | 0.75 | 300 | \spawn deployable 1276 |
| 1277 | All Purpose Invisible Point | None | Undefined | neutral | 0 | 0 | 1 | 0 | \spawn deployable 1277 |
| 1278 | Research Facility SafeRoomDoor | None | Undefined | accord | 0 | 1 | 1 | 0 | \spawn deployable 1278 |
| 1279 | Research Facility General Door | None | Undefined | accord | 0 | 1 | 1 | 0 | \spawn deployable 1279 |
| 1280 | Omnidyne Booth | None | Undefined | accord | 0 | 1 | 1 | 0 | \spawn deployable 1280 |
| 1281 | Omnidyne Booth | None | Undefined | accord | 0 | 1 | 1 | 0 | \spawn deployable 1281 |
| 1282 | - | Arcporter Pylon | Deployed Target | accord | 0 | 1 | 0.15 | 0 | \spawn deployable 1282 |
| 1283 | _Invisible/No Collision Melding Bubble Deposit | None | Undefined | melding | 1 | 400 | 1 | 0 | \spawn deployable 1283 |
| 1284 | - | None | Undefined | friendly | 0 | 1 | 2 | 0 | \spawn deployable 1284 |
| 1285 | - | None | Undefined | friendly | 0 | 1 | 2 | 0 | \spawn deployable 1285 |
| 1286 | - | None | Undefined | friendly | 0 | 1 | 1 | 0 | \spawn deployable 1286 |
| 1287 | - | None | Undefined | friendly | 0 | 1 | 1 | 0 | \spawn deployable 1287 |
| 1288 | Copy of _Ammo Powerup | None | Undefined | accord | 0 | 1 | 1 | 0 | \spawn deployable 1288 |
| 1289 | - | None | Undefined | accord | 0 | 0 | 1 | 0 | \spawn deployable 1289 |
| 1290 | Army Standard | None | Undefined | accord | 12.5 | 5000 | 1 | 300 | \spawn deployable 1290 |
| 1291 | Sunset Glider Pad | Glider pad | Undefined | accord | 0.75 | 1 | 1 | 0 | \spawn deployable 1291 |
| 1292 | - | None | Target (primary) | chosen | 20 | 8000 | 3 | 0 | \spawn deployable 1292 |
| 1293 | - | None | Fixed Weapon | chosen | 0 | 4000 | 1 | 0 | \spawn deployable 1293 |
| 1294 | - | None | Undefined | accord | 0 | 1 | 1 | 0 | \spawn deployable 1294 |
| 1295 | - | None | Undefined | neutral | 0.25 | 10 | 1 | 0 | \spawn deployable 1295 |
| 1296 | - | None | Undefined | neutral | 0.25 | 10 | 1 | 0 | \spawn deployable 1296 |
| 1297 | - | None | Undefined | neutral | 0.25 | 10 | 1 | 0 | \spawn deployable 1297 |
| 1298 | - | None | Undefined | accord | 1 | 1 | 1 | 0 | \spawn deployable 1298 |
| 1300 | - | None | Undefined | accord | 0 | 0 | 1 | 0 | \spawn deployable 1300 |
| 1301 | - | None | Undefined | accord | 0.75 | 1 | 1 | 0 | \spawn deployable 1301 |
| 1302 | - | Arcporter Pylon | Fixed Weapon | accord | 0 | 0 | 0.8 | 0 | \spawn deployable 1302 |
| 1303 | - | None | Fixed Weapon | accord | 20 | 8000 | 1 | 0 | \spawn deployable 1303 |
| 1304 | New Eden Security Transport | None | Fixed Weapon | accord | 20 | 8000 | 1 | 0 | \spawn deployable 1304 |
| 1305 | - | None | Fixed Weapon | accord | 0 | 4000 | 1 | 0 | \spawn deployable 1305 |
| 1307 | - | None | Undefined | accord | 0 | 0 | 4 | 0 | \spawn deployable 1307 |
| 1308 | - | None | Undefined | friendly | 0 | 1 | 1 | 2500 | \spawn deployable 1308 |
| 1309 | - | None | Undefined | gaea | 4 | 1500 | 1 | 0 | \spawn deployable 1309 |
| 1310 | Sonic Ultra Detonator | None | Undefined | accord | 0 | 1 | 1 | 0 | \spawn deployable 1310 |
| 1311 | - | None | Undefined | accord | 0 | 1 | 1 | 0 | \spawn deployable 1311 |
| 1312 | - | Repair Station | Healing | accord | 0 | 0 | 1 | 0 | \spawn deployable 1312 |
| 1313 | - | None | Interactable Objective | accord | 0 | 0 | 1 | 0 | \spawn deployable 1313 |
| 1315 | - | None | Undefined | accord | 0 | 0 | 1 | 250 | \spawn deployable 1315 |
| 1318 | - | None | Undefined | chosen | 0 | 1 | 1 | 0 | \spawn deployable 1318 |
| 1319 | Heavy Turret (Rocket)- Rank II | Turret | Fixed Weapon | accord | 4 | 1600 | 1.3 | 4000 | \spawn deployable 1319 |
| 1320 | Chosen Commander Drop Pod | Spawner | Deployed Target | chosen | 12 | 4800 | 1.5 | 10500 | \spawn deployable 1320 |
| 1321 | Arcfolder | Forge | Undefined | accord | 0 | 1 | 1 | 0 | \spawn deployable 1321 |
| 1322 | - | None | Target (primary) | accord | 50 | 1 | 1 | 12000 | \spawn deployable 1322 |
| 1323 | - | None | Target (primary) | accord | 50 | 1 | 1 | 12000 | \spawn deployable 1323 |
| 1324 | - | None | Target (primary) | accord | 50 | 1 | 1 | 12000 | \spawn deployable 1324 |
| 1325 | - | Turret | Fixed Weapon | bandit | 5 | 2000 | 1.5 | 5000 | \spawn deployable 1325 |
| 1326 | - | None | Undefined | accord | 0 | 0 | 0.086 | 0 | \spawn deployable 1326 |
| 1327 | - | None | Undefined | accord | 0 | 1 | 1.5 | 0 | \spawn deployable 1327 |
| 1328 | - | None | Undefined | accord | 0 | 1 | 1 | 0 | \spawn deployable 1328 |
| 1329 | Chosen Strifebringer | Turret | Play | chosen | 4 | 1 | 1 | 0 | \spawn deployable 1329 |
| 1330 | Craterdome Energy Shield | None | Deployed Target | chosen | 25 | 1 | 1 | 0 | \spawn deployable 1330 |
| 1331 | - | None | Undefined | chosen | 0 | 1 | 0.6 | 0 | \spawn deployable 1331 |
| 1332 | - | SIN Tower | Target (primary) | accord | 64 | 600 | 1 | 0 | \spawn deployable 1332 |
| 1333 | Chosen Harbinger | Spawner | Undefined | chosen | 55 | 22000 | 3 | 15000 | \spawn deployable 1333 |
| 1334 | SIN Generator | SIN Tower | Target (primary) | accord | 0 | 0 | 1.8 | 0 | \spawn deployable 1334 |
| 1335 | - | None | Undefined | neutral | 0 | 0 | 0.5 | 0 | \spawn deployable 1335 |
| 1337 | Thumper Disruptor | None | Undefined | accord | 0 | 0 | 1.2 | 0 | \spawn deployable 1337 |
| 1338 | - | SIN Tower | Target (secondary) | accord | 7.5 | 3000 | 1 | 40666 | \spawn deployable 1338 |
| 1339 | Harbinger Sheild | None | Undefined | chosen | 0 | 1 | 0.75 | 0 | \spawn deployable 1339 |
| 1340 | Kaimuki Main Terminal | None | Interactable Objective | accord | 0 | 0 | 1 | 0 | \spawn deployable 1340 |
| 1341 | - | None | Interactable Objective | accord | 0 | 0 | 0.25 | 0 | \spawn deployable 1341 |
| 1342 | Accord Thumper | None | Target (tertiary) | accord | 60 | 24000 | 1 | 11666 | \spawn deployable 1342 |
| 1343 | Ares Melding Repulsor Project | None | Interactable Objective | friendly | 0 | 0 | 1 | 1 | \spawn deployable 1343 |
| 1344 | FOB Harpoon SIN Uplink | None | Interactable Objective | accord | 0 | 0 | 1 | 40666 | \spawn deployable 1344 |
| 1345 | - | None | Undefined | accord | 0 | 1 | 1 | 0 | \spawn deployable 1345 |
| 1346 | - | None | Deployed Target | chosen | 5 | 2000 | 1 | 250 | \spawn deployable 1346 |
| 1347 | - | None | Undefined | accord | 0 | 1 | 1 | 0 | \spawn deployable 1347 |
| 1348 | - | None | Undefined | accord | 1 | 1 | 1 | 0 | \spawn deployable 1348 |
| 1349 | Devil's Tusk Sensor Array SIN Uplink | None | Target (secondary) | accord | 60 | 24000 | 1 | 40666 | \spawn deployable 1349 |
| 1350 | War Effort Terminal | None | Undefined | accord | 0 | 1 | 1 | 0 | \spawn deployable 1350 |
| 1351 | Melded Power Cell Donation Terminal | None | Undefined | accord | 0 | 1 | 1 | 0 | \spawn deployable 1351 |
| 1352 | - | Mannable Turret | Fixed Weapon | accord | 15 | 6000 | 1 | 0 | \spawn deployable 1352 |
| 1353 | - | None | Target (tertiary) | accord | 10 | 4000 | 1 | 0 | \spawn deployable 1353 |
| 1354 | Sunrise Glider Pad | Glider pad | Undefined | accord | 0.75 | 1 | 1 | 0 | \spawn deployable 1354 |
| 1355 | Tutorial Battery Housing | None | Interactable Objective | accord | 0 | 0 | 0.5 | 0 | \spawn deployable 1355 |
| 1356 | - | None | Target (tertiary) | accord | 10 | 4000 | 0.5 | 0 | \spawn deployable 1356 |
| 1357 | Mobile Battleframe Garage | Forge | Undefined | accord | 0 | 1 | 0.6 | 0 | \spawn deployable 1357 |
| 1358 | Mobile Crafting Terminal | Manufacturing Terminal | Undefined | accord | 0 | 0 | 0.65 | 0 | \spawn deployable 1358 |
| 1361 | - | Tech Turret | Undefined | accord | 1 | 1 | 2 | 0 | \spawn deployable 1361 |
| 1362 | - | None | Target (primary) | accord | 25 | 10000 | 1 | 0 | \spawn deployable 1362 |
| 1363 | - | None | Undefined | accord | 0 | 0 | 1 | 0 | \spawn deployable 1363 |
| 1364 | Sin Tower Shield Generator Terminal | Tech SIN | Interactable Objective | accord | 0 | 1 | 1 | 0 | \spawn deployable 1364 |
| 1365 | SinTowerShield | SIN Tower | Target (secondary) | accord | 0 | 0 | 1 | 0 | \spawn deployable 1365 |
| 1366 | - | None | Interactable Objective | accord | 0 | 0 | 1 | 40666 | \spawn deployable 1366 |
| 1367 | - | None | Interactable Objective | accord | 0 | 0 | 1 | 40666 | \spawn deployable 1367 |
| 1368 | New Eden Security Arcporter Pylon | Arcporter Pylon | Undefined | accord | 0 | 1 | 1 | 0 | \spawn deployable 1368 |
| 1369 | Glider Pad | Glider pad | Undefined | accord | 0 | 0 | 1 | 0 | \spawn deployable 1369 |
| 1370 | Outpost Heavy Turret | Turret | Fixed Weapon | accord | 0 | 0 | 2 | 5000 | \spawn deployable 1370 |
| 1371 | Melded Power Cell Recepticle | None | Undefined | accord | 0 | 1 | 1 | 0 | \spawn deployable 1371 |
| 1372 | - | Mine | Undefined | accord | 1 | 1 | 1 | 0 | \spawn deployable 1372 |
| 1373 | - | Charged Pulse | Deployed Target | accord | 8 | 1 | 1.5 | 1000 | \spawn deployable 1373 |
| 1374 | Leaderboard Terminal | None | Undefined | accord | 0 | 0 | 1 | 0 | \spawn deployable 1374 |
| 1375 | Statue Base | None | Undefined | accord | 0 | 0 | 0.5 | 0 | \spawn deployable 1375 |
| 1376 | - | None | Undefined | accord | 1.25 | 1 | 0.75 | 300 | \spawn deployable 1376 |
| 1377 | - | None | Undefined | neutral | 0 | 0 | 1 | 0 | \spawn deployable 1377 |
| 1378 | - | None | Undefined | accord | 0 | 1 | 1 | 0 | \spawn deployable 1378 |
| 1379 | - | None | Deployed Target | accord | 12 | 1 | 1.4 | 0 | \spawn deployable 1379 |
| 1380 | - | None | Undefined | accord | 0 | 0 | 1 | 0 | \spawn deployable 1380 |
| 1381 | - | Mine | Undefined | neutral | 0.5 | 1 | 3 | 2000 | \spawn deployable 1381 |
| 1382 | - | None | Undefined | accord | 0 | 0 | 1 | 0 | \spawn deployable 1382 |
| 1383 | - | None | Undefined | neutral | 0 | 0 | 1 | 0 | \spawn deployable 1383 |
| 1384 | - | None | Undefined | accord | 0 | 0 | 1 | 0 | \spawn deployable 1384 |
| 1385 | - | None | Interactable Objective | chosen | 0 | 0 | 1 | 0 | \spawn deployable 1385 |
| 1386 | - | None | Undefined | neutral | 0 | 0 | 1 | 0 | \spawn deployable 1386 |
| 1387 | - | None | Undefined | gaea | 0 | 0 | 1 | 0 | \spawn deployable 1387 |
| 1388 | - | None | Undefined | accord | 0 | 1 | 1 | 0 | \spawn deployable 1388 |
| 1389 | - | None | Undefined | accord | 0 | 0 | 1 | 0 | \spawn deployable 1389 |
| 1390 | - | None | Undefined | neutral | 0 | 0 | 1 | 0 | \spawn deployable 1390 |
| 1391 | - | None | Undefined | chosen | 0 | 1 | 1 | 250 | \spawn deployable 1391 |
| 1392 | - | None | Undefined | neutral | 0 | 0 | 1 | 0 | \spawn deployable 1392 |
| 1393 | - | None | Undefined | accord | 1 | 1 | 1 | 0 | \spawn deployable 1393 |
| 1394 | - | None | Undefined | accord | 1 | 1 | 1 | 0 | \spawn deployable 1394 |
| 1395 | - | None | Undefined | accord | 1 | 1 | 1 | 0 | \spawn deployable 1395 |
| 1396 | - | None | Undefined | accord | 1 | 1 | 1 | 0 | \spawn deployable 1396 |
| 1397 | - | Turret | Play | accord | 0 | 1 | 1 | 0 | \spawn deployable 1397 |
| 1398 | - | Turret | Play | accord | 0 | 1 | 1 | 0 | \spawn deployable 1398 |
| 1399 | - | None | Undefined | accord | 0 | 1 | 1 | 0 | \spawn deployable 1399 |
| 1400 | Melding Fragment Arcfolder | None | Undefined | accord | 0 | 1 | 1 | 0 | \spawn deployable 1400 |
| 1402 | Healing Generator | None | Undefined | chosen | 0.75 | 800 | 0.4 | 0 | \spawn deployable 1402 |
| 1403 | - | None | Undefined | gaea | 0 | 0 | 1 | 0 | \spawn deployable 1403 |
| 1404 | - | None | Undefined | chosen | 0 | 1 | 1 | 0 | \spawn deployable 1404 |
| 1405 | - | Generic Terminal | Undefined | accord | 0 | 1 | 0.01 | 0 | \spawn deployable 1405 |
| 1406 | - | None | Undefined | accord | 0 | 1 | 1 | 0 | \spawn deployable 1406 |
| 1407 | - | None | Undefined | neutral | 0 | 0 | 1 | 0 | \spawn deployable 1407 |
| 1408 | - | None | Undefined | neutral | 0 | 0 | 1 | 0 | \spawn deployable 1408 |
| 1409 | - | None | Undefined | gaea | 0 | 0 | 1 | 0 | \spawn deployable 1409 |
| 1410 | - | None | Undefined | gaea | 0 | 0 | 1 | 0 | \spawn deployable 1410 |
| 1411 | - | None | Undefined | gaea | 0 | 0 | 1 | 0 | \spawn deployable 1411 |
| 1412 | - | None | Undefined | gaea | 0 | 0 | 1 | 0 | \spawn deployable 1412 |
| 1413 | - | None | Undefined | gaea | 0 | 0 | 1 | 0 | \spawn deployable 1413 |
| 1414 | - | None | Undefined | gaea | 0 | 0 | 1 | 0 | \spawn deployable 1414 |
| 1415 | - | None | Undefined | gaea | 0 | 0 | 1 | 0 | \spawn deployable 1415 |
| 1416 | - | None | Undefined | gaea | 0 | 0 | 1 | 0 | \spawn deployable 1416 |
| 1417 | - | None | Undefined | accord | 0 | 0 | 1 | 0 | \spawn deployable 1417 |
| 1519 | - | None | Undefined | accord | 0 | 1 | 1 | 0 | \spawn deployable 1519 |
| 1520 | Automated Callbox | None | Interactable Objective | neutral | 0 | 0 | 1 | 0 | \spawn deployable 1520 |
| 1521 | Bandit Shelter | None | Undefined | accord | 0 | 0 | 1 | 0 | \spawn deployable 1521 |
| 1522 | Decoy Projector - NPC Deployed | None | Deployed Target | accord | 2500 | 999999 | 1 | 0 | \spawn deployable 1522 |
| 1523 | Bandit Cache Torch | None | Undefined | accord | 0 | 0 | 1 | 300 | \spawn deployable 1523 |
| 1526 | LGV Dismount Mine | Mine | Undefined | neutral | 1.5 | 1 | 3 | 2000 | \spawn deployable 1526 |
| 1528 | Bandit Cache campfire | None | Undefined | accord | 0 | 1 | 1 | 0 | \spawn deployable 1528 |
| 1529 | Bandit Cache campfire 2 | None | Undefined | accord | 0 | 1 | 0.75 | 0 | \spawn deployable 1529 |
| 1530 | Bandit Cache campfire 3 | None | Undefined | accord | 0 | 1 | 1.25 | 0 | \spawn deployable 1530 |
| 1531 | Bandit Cache | None | Undefined | accord | 0 | 1 | 1 | 0 | \spawn deployable 1531 |
| 1532 | Anti-LGV Mine | Mine | Undefined | neutral | 0 | 1 | 1.5 | 2000 | \spawn deployable 1532 |
| 1533 | Tanken Datapad | None | Interactable Objective | neutral | 0 | 0 | 1 | 0 | \spawn deployable 1533 |
| 1539 | - | None | Undefined | accord | 1 | 1 | 1 | 0 | \spawn deployable 1539 |
| 1540 | Rare Supply Crate | Datapads | Undefined | neutral | 0 | 1 | 1.5 | 0 | \spawn deployable 1540 |
| 1541 | Rare Supply Crate | Datapads | Undefined | neutral | 0 | 1 | 1.5 | 0 | \spawn deployable 1541 |
| 1542 | Storm Kestrel Nest (looted) | None | Undefined | neutral | 0 | 1 | 1 | 0 | \spawn deployable 1542 |
| 1543 | Storm Kestrel Nest | None | Undefined | accord | 15 | 6000 | 1 | 0 | \spawn deployable 1543 |
| 1546 | Trip Mine | None | Undefined | neutral | 0 | 9999999 | 1 | 500 | \spawn deployable 1546 |
| 1547 | Oilspill's Dropship | None | Target (primary) | accord | 50 | 20000 | 1 | 0 | \spawn deployable 1547 |
| 1548 | Accord Agent Data Scanner | Mine | Undefined | accord | 0 | 9999999 | 1 | 500 | \spawn deployable 1548 |
| 1549 | Arclight Container | None | Undefined | accord | 0 | 9999999 | 0.5 | 500 | \spawn deployable 1549 |
| 1550 | Fallen Accord Patrol Officer | Mine | Undefined | accord | 0 | 9999999 | 1 | 500 | \spawn deployable 1550 |
| 1551 | Meteor Bombardment - shoots the Crystite Meteor | None | Undefined | neutral | 0 | 1 | 1 | 0 | \spawn deployable 1551 |
| 1552 | Dusk Glider Pad | Glider pad | Undefined | accord | 0.75 | 1 | 1 | 0 | \spawn deployable 1552 |
| 1553 | - | None | Undefined | chosen | 0 | 1 | 1.5 | 0 | \spawn deployable 1553 |
| 1554 | - | None | Undefined | accord | 0 | 0 | 1 | 0 | \spawn deployable 1554 |
| 1555 | - | None | Target (primary) | accord | 50 | 20000 | 1 | 0 | \spawn deployable 1555 |
| 1556 | - | Datapads | Undefined | accord | 0 | 1 | 1 | 0 | \spawn deployable 1556 |
| 1558 | - | None | Undefined | accord | 0.0025 | 1 | 1 | 0 | \spawn deployable 1558 |
| 1561 | - | None | Undefined | accord | 0.5 | 1 | 1 | 0 | \spawn deployable 1561 |
| 1562 | - | None | Undefined | accord | 0.5 | 1 | 1 | 0 | \spawn deployable 1562 |
| 1563 | - | None | Undefined | accord | 0 | 1 | 1 | 0 | \spawn deployable 1563 |
| 1564 | - | None | Undefined | accord | 0 | 1 | 1 | 0 | \spawn deployable 1564 |
| 1565 | - | None | Undefined | accord | 0 | 1 | 1 | 0 | \spawn deployable 1565 |
| 1566 | - | None | Undefined | accord | 0 | 1 | 1 | 0 | \spawn deployable 1566 |
| 1567 | Chosen Commander Drop Pod | None | Deployed Target | chosen | 12 | 4800 | 1.5 | 250 | \spawn deployable 1567 |
| 1568 | Accord Vault | Turret | Play | accord | 0 | 1 | 2.5 | 0 | \spawn deployable 1568 |
| 1569 | Homing Beacon | None | Undefined | accord | 0.0025 | 1 | 0.5 | 0 | \spawn deployable 1569 |
| 1570 | - | None | Undefined | accord | 0.5 | 1 | 1 | 0 | \spawn deployable 1570 |
| 1571 | - | None | Undefined | accord | 0.5 | 1 | 1 | 0 | \spawn deployable 1571 |
| 1572 | Fallen Accord Patrol Officer | Mine | Undefined | accord | 0 | 9999999 | 1 | 500 | \spawn deployable 1572 |
| 1573 | Fallen Accord Patrol Officer | None | Undefined | accord | 0 | 9999999 | 1 | 500 | \spawn deployable 1573 |
| 1574 | Accord Scientist Datapad | Mine | Undefined | accord | 0 | 9999999 | 1 | 500 | \spawn deployable 1574 |
| 1575 | Wandering Encounter - Distress Call - Fragment2 | Glider pad | Undefined | accord | 0 | 9999999 | 0.375 | 0 | \spawn deployable 1575 |
| 1576 | Civilian Cage | None | Undefined | accord | 0 | 0 | 1 | 0 | \spawn deployable 1576 |
| 1577 | - | None | Undefined | accord | 0 | 1 | 1 | 1000 | \spawn deployable 1577 |
| 1578 | Meteor Bombardment collision check | None | Undefined | neutral | 0 | 1 | 0.25 | 0 | \spawn deployable 1578 |
| 1579 | Communications Tower | None | Deployed Target | chosen | 50 | 1 | 1 | 1000 | \spawn deployable 1579 |
| 1580 | Crystite Meteor Resource Deposit | Surface Deposit | Target (primary) | neutral | 20 | 1000 | 1 | 0 | \spawn deployable 1580 |
| 1581 | - | None | Undefined | accord | 0 | 99999999 | 0.5 | 0 | \spawn deployable 1581 |
| 1582 | _Crate 8 | None | Undefined | accord | 0 | 9999999 | 4 | 500 | \spawn deployable 1582 |
| 1583 | Accord Distress Beacon | Generic Terminal | Undefined | accord | 0 | 9999999 | 0.25 | 500 | \spawn deployable 1583 |
| 1584 | Disarmed Anti-LGV Mine | Mine | Undefined | neutral | 0 | 1 | 1.5 | 2000 | \spawn deployable 1584 |
| 1585 | Use | None | Undefined | friendly | 0 | 1 | 1 | 0 | \spawn deployable 1585 |
| 1586 | _Explosive | None | Undefined | accord | 0 | 0 | 1 | 0 | \spawn deployable 1586 |
| 1587 | Heavy Turret | None | Fixed Weapon | chosen | 15 | 6000 | 1 | 2000 | \spawn deployable 1587 |
| 1588 | - | None | Deployed Target | bandit | 0 | 14000 | 1 | 0 | \spawn deployable 1588 |
| 1589 | - | None | Undefined | accord | 0 | 1 | 1 | 1000 | \spawn deployable 1589 |
| 1590 | - | None | Undefined | bandit | 0 | 0 | 1 | 1000 | \spawn deployable 1590 |
| 1591 | - | None | Passenger Seat | accord | 0 | 0 | 1 | 0 | \spawn deployable 1591 |
| 1592 | Chosen Sin Tower | None | Target (secondary) | chosen | 20 | 6000 | 1 | 0 | \spawn deployable 1592 |
| 1593 | _Harvester Part | None | Undefined | accord | 0 | 0 | 1.5 | 0 | \spawn deployable 1593 |
| 1594 | Chosen Generator | None | Target (primary) | chosen | 10 | 3000 | 2 | 0 | \spawn deployable 1594 |
| 1595 | Chosen Shield Generator | None | Undefined | chosen | 5 | 1200 | 1.6 | 0 | \spawn deployable 1595 |
| 1596 | Rare Supply Crate | Datapads | Undefined | neutral | 0 | 1 | 1.5 | 0 | \spawn deployable 1596 |
| 1597 | Meteor Bombardment - Shooting Star | None | Undefined | neutral | 0 | 1 | 1 | 0 | \spawn deployable 1597 |
| 1598 | Meteor Bombardment - shoots the secondary falling Crystite Meteor | None | Undefined | neutral | 0 | 1 | 1 | 0 | \spawn deployable 1598 |
| 1599 | Meteor Bombardment - shoots the 3rd falling Crystite Meteor | None | Undefined | neutral | 0 | 1 | 1 | 0 | \spawn deployable 1599 |
| 1600 | - | None | Undefined | accord | 0 | 1 | 1 | 0 | \spawn deployable 1600 |
| 1601 | _Glider Boost | None | Undefined | accord | 0 | 1 | 1 | 0 | \spawn deployable 1601 |
| 1602 | Accord Arcporter | None | Undefined | friendly | 0 | 0 | 1 | 0 | \spawn deployable 1602 |
| 1603 | ED-2000 | None | Undefined | friendly | 0 | 1 | 2 | 0 | \spawn deployable 1603 |
| 1604 | _Wander Encounters - Giant Aranhas Spawner | None | Undefined | gaea | 25 | 10000 | 1.25 | 3000 | \spawn deployable 1604 |
| 1605 | _Culex Lair + Spawns | None | Undefined | gaea | 25 | 10000 | 2 | 0 | \spawn deployable 1605 |
| 1612 | - | Charged Pulse | Deployed Target | accord | 1 | 1 | 1 | 1000 | \spawn deployable 1612 |
| 1613 | Fuller's LGV | None | Undefined | accord | 0 | 1 | 1 | 0 | \spawn deployable 1613 |
| 1614 | _Rock | None | Undefined | accord | 0 | 0 | 0.85 | 0 | \spawn deployable 1614 |
| 1615 | Melding Repulsor Generator | None | Target (primary) | accord | 10 | 3000 | 1 | 0 | \spawn deployable 1615 |
| 1616 | - | None | Undefined | accord | 0 | 1 | 1 | 1000 | \spawn deployable 1616 |
| 1617 | _Dome | None | Undefined | melding | 0 | 0 | 0.56 | 0 | \spawn deployable 1617 |
| 1619 | Crystite Explosive | None | Undefined | accord | 0 | 1 | 1 | 0 | \spawn deployable 1619 |
| 1620 | - | None | Undefined | accord | 0 | 1 | 1 | 0 | \spawn deployable 1620 |
| 1621 | - | None | Target (primary) | chosen | 20 | 8000 | 3 | 0 | \spawn deployable 1621 |
| 1622 | Accord Distress Beacon | Mine | Undefined | accord | 0 | 1 | 1 | 0 | \spawn deployable 1622 |
| 1623 | Accord Crate | None | Undefined | neutral | 0 | 1 | 1 | 0 | \spawn deployable 1623 |
| 1624 | - | None | Undefined | accord | 0 | 1 | 5 | 0 | \spawn deployable 1624 |
| 1625 | Topaz Glider Pad | Glider pad | Undefined | accord | 0.75 | 1 | 1 | 0 | \spawn deployable 1625 |
| 1626 | VIP Boosted Glider Pad | Glider pad | Undefined | accord | 0.75 | 1 | 1 | 0 | \spawn deployable 1626 |
| 1627 | Emerald Glider Pad | Glider pad | Undefined | accord | 0.75 | 1 | 1 | 0 | \spawn deployable 1627 |
| 1628 | Royal Glider Pad | Glider pad | Undefined | accord | 0.75 | 1 | 1 | 0 | \spawn deployable 1628 |
| 1629 | - | None | Undefined | accord | 0 | 1 | 1 | 0 | \spawn deployable 1629 |
| 1630 | Crystite Meteor Debris Fragment | Surface Deposit | Target (primary) | accord | 1 | 100 | 2 | 0 | \spawn deployable 1630 |
| 1631 | Rusted Thumper Panel | None | Undefined | accord | 0 | 1 | 1 | 0 | \spawn deployable 1631 |
| 1632 | Rusted Thumper Panel | None | Undefined | accord | 0 | 1 | 1 | 0 | \spawn deployable 1632 |
| 1633 | Damaged Thumper Panel | None | Undefined | accord | 0 | 1 | 1 | 0 | \spawn deployable 1633 |
| 1634 | Lid No Fall | None | Undefined | accord | 0 | 1 | 1 | 0 | \spawn deployable 1634 |
| 1635 | Accord Case Officer Datacache | None | Undefined | accord | 0 | 1 | 1 | 0 | \spawn deployable 1635 |
| 1636 | - | None | Undefined | accord | 0 | 1 | 1 | 0 | \spawn deployable 1636 |
| 1637 | Wrecked LGV | None | Undefined | accord | 0 | 1 | 1 | 0 | \spawn deployable 1637 |
| 1638 | Loading Cart | None | Undefined | accord | 0 | 1 | 1 | 0 | \spawn deployable 1638 |
| 1639 | - | None | Undefined | accord | 0 | 9999999 | 1 | 0 | \spawn deployable 1639 |
| 1640 | - | None | Undefined | accord | 0 | 1 | 0.25 | 0 | \spawn deployable 1640 |
| 1641 | APC Debris | None | Undefined | accord | 0 | 1 | 0.875 | 0 | \spawn deployable 1641 |
| 1642 | - | None | Undefined | chosen | 0 | 1 | 1 | 0 | \spawn deployable 1642 |
| 1643 | _SIN Hover Flare | None | Undefined | accord | 0 | 1 | 1 | 0 | \spawn deployable 1643 |
| 1644 | ED-2000 | None | Undefined | accord | 0 | 1 | 1 | 0 | \spawn deployable 1644 |
| 1645 | _Disruption Hover Flare | None | Undefined | accord | 0 | 1 | 1 | 0 | \spawn deployable 1645 |
| 1646 | _ED-2000 | None | Undefined | friendly | 0 | 1 | 2 | 0 | \spawn deployable 1646 |
| 1647 | Accord Patrol Pack | None | Undefined | accord | 0 | 9999999 | 1 | 500 | \spawn deployable 1647 |
| 1648 | - | Shield | Undefined | accord | 5 | 1 | 1 | 0 | \spawn deployable 1648 |
| 1649 | - | None | Undefined | accord | 1 | 1 | 1 | 0 | \spawn deployable 1649 |
| 1650 | Happy New Years Fireworks | Consumer Fireworks | Undefined | accord | 0 | 1 | 1 | 0 | \spawn deployable 1650 |
| 1651 | Fireworks Launcher | Consumer Fireworks | Undefined | accord | 0 | 1 | 1 | 0 | \spawn deployable 1651 |
| 1652 | Accord Sin Uplink | None | Target (secondary) | accord | 55 | 55000 | 1 | 40666 | \spawn deployable 1652 |
| 1653 | - | None | Undefined | accord | 1 | 150 | 1 | 0 | \spawn deployable 1653 |
| 1654 | Anti-Air Turret | Mannable Turret | Fixed Weapon | accord | 0 | 6000 | 3 | 0 | \spawn deployable 1654 |
| 1655 | - | Glider pad | Undefined | accord | 0 | 9999999 | 1 | 0 | \spawn deployable 1655 |
| 1656 | - | Glider pad | Undefined | accord | 0 | 9999999 | 1 | 0 | \spawn deployable 1656 |
| 1657 | - | None | Undefined | accord | 0 | 48000 | 2 | 0 | \spawn deployable 1657 |
| 1658 | Breachable Door | None | Undefined | neutral | 0 | 1 | 1.25 | 0 | \spawn deployable 1658 |
| 1659 | - | None | Undefined | accord | 0 | 1 | 1 | 0 | \spawn deployable 1659 |
| 1660 | - | None | Undefined | chosen | 50 | 20000 | 2 | 0 | \spawn deployable 1660 |
| 1661 | Chosen Pylon | None | Undefined | chosen | 210 | 90000 | 2 | 0 | \spawn deployable 1661 |
| 1662 | Accord Patrol Truck | None | Undefined | chosen | 0 | 1 | 1 | 0 | \spawn deployable 1662 |
| 1663 | - | None | Undefined | chosen | 0 | 1 | 1 | 0 | \spawn deployable 1663 |
| 1664 | Accord Patrol Cart | None | Undefined | chosen | 0 | 1 | 1.5 | 0 | \spawn deployable 1664 |
| 1665 | Fallen Accord Patrol Officer | None | Undefined | accord | 0 | 9999999 | 1 | 500 | \spawn deployable 1665 |
| 1666 | Fallen Accord Patrol Officer | Mine | Undefined | accord | 0 | 9999999 | 1 | 500 | \spawn deployable 1666 |
| 1667 | _Visualized Transponder Location | None | Undefined | accord | 0 | 1 | 5 | 0 | \spawn deployable 1667 |
| 1668 | - | None | Undefined | accord | 0 | 1 | 2 | 0 | \spawn deployable 1668 |
| 1669 | Accord Strike Marker | None | Undefined | accord | 0 | 1 | 1 | 0 | \spawn deployable 1669 |
| 1670 | - | None | Undefined | accord | 0 | 1 | 1.3 | 0 | \spawn deployable 1670 |
| 1671 | Stealth Generator | None | Undefined | chosen | 3 | 1200 | 0.25 | 0 | \spawn deployable 1671 |
| 1672 | Caldera Creche | None | Undefined | gaea | 20 | 10000 | 1 | 0 | \spawn deployable 1672 |
| 1673 | Auxiliary Engine | None | Undefined | accord | 0 | 9999999 | 1 | 0 | \spawn deployable 1673 |
| 1674 | Chosen Thumper | None | Target (secondary) | chosen | 100 | 30000 | 1 | 13000 | \spawn deployable 1674 |
| 1675 | _Dog House | None | Undefined | neutral | 0 | 0 | 1 | 0 | \spawn deployable 1675 |
| 1676 | _Ol' Betsy's Dog House | None | Undefined | neutral | 0 | 0 | 1.25 | 0 | \spawn deployable 1676 |
| 1677 | - | Mannable Turret | Fixed Weapon | chosen | 15.25 | 12500 | 1 | 1000 | \spawn deployable 1677 |
| 1678 | Cage Doors | None | Undefined | accord | 0 | 0 | 1.1 | 0 | \spawn deployable 1678 |
| 1679 | Rotten Brontodon Meat | None | Deployed Target | accord | 12 | 4800 | 1 | 0 | \spawn deployable 1679 |
| 1680 | - | Turret | Fixed Weapon | monster | 7.5 | 3000 | 1.5 | 0 | \spawn deployable 1680 |
| 1681 | - | None | Undefined | chosen | 1 | 1 | 2 | 0 | \spawn deployable 1681 |
| 1682 | - | None | Undefined | chosen | 1 | 1 | 2 | 0 | \spawn deployable 1682 |
| 1683 | - | None | Undefined | chosen | 1 | 1 | 2 | 0 | \spawn deployable 1683 |
| 1684 | Stolen Supplies | None | Undefined | accord | 0 | 50 | 0.5 | 0 | \spawn deployable 1684 |
| 1685 | Accord Supplies | Generic Terminal | Undefined | accord | 0 | 1 | 0.125 | 500 | \spawn deployable 1685 |
| 1686 | - | None | Undefined | accord | 0 | 0 | 1 | 0 | \spawn deployable 1686 |
| 1687 | - | None | Play | gaea | 0 | 1 | 2 | 0 | \spawn deployable 1687 |
| 1688 | Rotten Brontodon Meat | None | Target (primary) | accord | 2 | 1000 | 1 | 0 | \spawn deployable 1688 |
| 1689 | Mechanical Terminal | None | Target (primary) | accord | 500 | 195000 | 1 | 0 | \spawn deployable 1689 |
| 1690 | Sample Analyzer | None | Undefined | accord | 0 | 1 | 1 | 0 | \spawn deployable 1690 |
| 1691 | - | None | Undefined | accord | 0.5 | 1 | 1 | 0 | \spawn deployable 1691 |
| 1692 | - | Shield | Undefined | accord | 10 | 1 | 1 | 0 | \spawn deployable 1692 |
| 1693 | - | None | Undefined | accord | 5 | 1 | 1 | 0 | \spawn deployable 1693 |
| 1694 | - | None | Fixed Weapon | chosen | 15 | 6000 | 1.5 | 0 | \spawn deployable 1694 |
| 1695 | Oilspill's Fridge | None | Undefined | friendly | 0 | 1 | 0.4 | 0 | \spawn deployable 1695 |
| 1696 | Gauss Couplers | None | Undefined | friendly | 0 | 1 | 1 | 0 | \spawn deployable 1696 |
| 1697 | Torrent Capacitor | None | Undefined | friendly | 0 | 1 | 0.25 | 0 | \spawn deployable 1697 |
| 1698 | Armored Dropship | None | Undefined | accord | 0 | 1 | 0.7 | 0 | \spawn deployable 1698 |
| 1699 | _Debris | None | Undefined | melding | 0 | 1 | 1 | 0 | \spawn deployable 1699 |
| 1700 | Debris | None | Undefined | melding | 2.5 | 1000 | 1 | 0 | \spawn deployable 1700 |
| 1701 | - | Turret | Fixed Weapon | bandit | 2.5 | 1000 | 1.5 | 5000 | \spawn deployable 1701 |
| 1702 | - | Power Cell Dispenser | Undefined | friendly | 0 | 1 | 1 | 0 | \spawn deployable 1702 |
| 1703 | - | None | Undefined | chosen | 12.5 | 4500 | 1.1 | 0 | \spawn deployable 1703 |
| 1704 | - | None | Undefined | chosen | 12.5 | 4500 | 0.65 | 0 | \spawn deployable 1704 |
| 1706 | - | None | Undefined | accord | 0 | 1 | 1 | 0 | \spawn deployable 1706 |
| 1707 | Copy of Fallen Accord Patrol Officer | Mine | Undefined | accord | 0 | 9999999 | 1 | 500 | \spawn deployable 1707 |
| 1708 | - | None | Undefined | accord | 0 | 1 | 2 | 0 | \spawn deployable 1708 |
| 1709 | Chosen Invisible Point | None | Undefined | chosen | 0 | 1 | 1 | 0 | \spawn deployable 1709 |
| 1710 | - | None | Undefined | accord | 0 | 1 | 0.325 | 0 | \spawn deployable 1710 |
| 1711 | - | None | Undefined | accord | 0 | 1 | 1 | 0 | \spawn deployable 1711 |
| 1712 | - | None | Undefined | accord | 0 | 1 | 1 | 0 | \spawn deployable 1712 |
| 1713 | Fragment | None | Undefined | accord | 0 | 1 | 0.35 | 0 | \spawn deployable 1713 |
| 1714 | - | None | Undefined | accord | 0 | 1 | 1 | 0 | \spawn deployable 1714 |
| 1715 | - | None | Undefined | accord | 0 | 0 | 1 | 0 | \spawn deployable 1715 |
| 1716 | - | Datapads | Loot | accord | 1 | 1 | 1 | 0 | \spawn deployable 1716 |
| 1717 | - | Datapads | Undefined | accord | 0 | 1 | 1 | 0 | \spawn deployable 1717 |
| 1718 | - | None | Undefined | friendly | 0 | 1 | 1 | 0 | \spawn deployable 1718 |
| 1719 | - | None | Undefined | chosen | 4 | 1000 | 1 | 0 | \spawn deployable 1719 |
| 1720 | - | None | Undefined | accord | 0 | 1 | 0.5 | 0 | \spawn deployable 1720 |
| 1722 | - | None | Undefined | accord | 0 | 1 | 2 | 0 | \spawn deployable 1722 |
| 1723 | Fallen Accord Patrol Officer | Mine | Undefined | accord | 0 | 9999999 | 1 | 500 | \spawn deployable 1723 |
| 1724 | Security Terminal | None | Undefined | accord | 0 | 0 | 1 | 0 | \spawn deployable 1724 |
| 1725 | Generator Control Terminal | None | Undefined | accord | 0 | 0 | 1.5 | 0 | \spawn deployable 1725 |
| 1726 | Key Terminal | None | Undefined | chosen | 0 | 1 | 1 | 0 | \spawn deployable 1726 |
| 1727 | Security Terminal | None | Undefined | accord | 0 | 0 | 1 | 0 | \spawn deployable 1727 |
| 1728 | Mainframe Terminal | None | Interactable Objective | chosen | 0 | 0 | 1 | 0 | \spawn deployable 1728 |
| 1729 | Auto-Turret Bay | None | Undefined | accord | 0 | 1 | 1 | 0 | \spawn deployable 1729 |
| 1730 | Warbringer Generator | None | Undefined | accord | 0 | 1 | 1 | 0 | \spawn deployable 1730 |
| 1731 | - | None | Undefined | friendly | 0 | 9999999 | 3 | 0 | \spawn deployable 1731 |
| 1732 | Rust Bucket - Sonic Detonator | None | Undefined | accord | 0 | 1 | 1 | 0 | \spawn deployable 1732 |
| 1733 | - | None | Undefined | accord | 0 | 1 | 1 | 0 | \spawn deployable 1733 |
| 1734 | Signal Flare | None | Undefined | accord | 0 | 1 | 1 | 0 | \spawn deployable 1734 |
| 1735 | Warfront Command Center | None | Undefined | accord | 0 | 1 | 1 | 0 | \spawn deployable 1735 |
| 1736 | Accord Facility Sliding Door | None | Undefined | accord | 0 | 1 | 1.7 | 0 | \spawn deployable 1736 |
| 1737 | - | None | Undefined | accord | 0 | 0 | 1 | 0 | \spawn deployable 1737 |
| 1738 | Emergency Relay Terminal | None | Interactable Objective | chosen | 0 | 0 | 1 | 0 | \spawn deployable 1738 |
| 1739 | - | None | Undefined | accord | 1 | 1 | 1 | 0 | \spawn deployable 1739 |
| 1740 | - | None | Undefined | friendly | 0 | 1 | 1 | 0 | \spawn deployable 1740 |
| 1741 | _Brontodon Carcass | None | Target (primary) | accord | 100000000 | 100000000 | 1 | 0 | \spawn deployable 1741 |
| 1742 | DeathBringer | Spawner | Target (tertiary) | chosen | 30 | 10800 | 1 | 15000 | \spawn deployable 1742 |
| 1744 | Brontodon Carcass | None | Undefined | accord | 0 | 1 | 1 | 0 | \spawn deployable 1744 |
| 1745 | - | Turret | Play | chosen | 50 | 18000 | 1 | 0 | \spawn deployable 1745 |
| 1746 | - | None | Undefined | accord | 0 | 1 | 1 | 0 | \spawn deployable 1746 |
| 1747 | NPC_Emote_Trash_01 | None | Rummage | accord | 0 | 0 | 1 | 0 | \spawn deployable 1747 |
| 1748 | _Obstacle | None | Undefined | accord | 0 | 1 | 1 | 0 | \spawn deployable 1748 |
| 1749 | _Icicle | None | Undefined | chosen | 0 | 1 | 3 | 0 | \spawn deployable 1749 |
| 1750 | _InvisibleMe | None | Undefined | accord | 1 | 1 | 1 | 0 | \spawn deployable 1750 |
| 1751 | - | None | Undefined | - | 0 | 1 | 1 | 5000 | \spawn deployable 1751 |
| 1752 | - | None | Undefined | accord | 0 | 1 | 1 | 0 | \spawn deployable 1752 |
| 1753 | - | None | Target (secondary) | accord | 7 | 2520 | 1 | 5000 | \spawn deployable 1753 |
| 1754 | - | None | Fixed Weapon | accord | 125 | 50000 | 1 | 0 | \spawn deployable 1754 |
| 1755 | - | None | Fixed Weapon | accord | 25 | 10000 | 0.4 | 0 | \spawn deployable 1755 |
| 1756 | - | None | Undefined | gaea | 25 | 10000 | 1.25 | 3000 | \spawn deployable 1756 |
| 1757 | - | None | Undefined | friendly | 0 | 1 | 1 | 0 | \spawn deployable 1757 |
| 1758 | - | Turret | Play | gaea | 0 | 1 | 3 | 0 | \spawn deployable 1758 |
| 1759 | Chosen Strifebringer | Turret | Play | chosen | 0 | 1 | 1 | 2000 | \spawn deployable 1759 |
| 1760 | Generator Base | None | Undefined | accord | 0 | 0 | 2 | 1 | \spawn deployable 1760 |
| 1761 | Generator | None | Target (primary) | accord | 20 | 8000 | 2 | 10000 | \spawn deployable 1761 |
| 1762 | Destroyed Generator | None | Undefined | accord | 0 | 0 | 2 | 0 | \spawn deployable 1762 |
| 1763 | Invisible Bomb Point | None | Undefined | accord | 0 | 0 | 1 | 0 | \spawn deployable 1763 |
| 1764 | Diamondhead Chosen Mining Camp Generator Shield | None | Undefined | accord | 0 | 2000 | 10 | 0 | \spawn deployable 1764 |
| 1765 | Explosives Crate | Power Cell Dispenser | Undefined | friendly | 0 | 1 | 4 | 0 | \spawn deployable 1765 |
| 1766 | Accord Explosive | None | Undefined | accord | 0 | 1 | 1 | 0 | \spawn deployable 1766 |
| 1767 | Chosen Wall Gate | None | Undefined | neutral | 0 | 1 | 1 | 0 | \spawn deployable 1767 |
| 1768 | Fireworks Grenade | None | Undefined | accord | 0 | 1 | 1 | 0 | \spawn deployable 1768 |
| 1769 | Warfront Artillery Cannon | None | Fixed Weapon | chosen | 25 | 10000 | 1 | 0 | \spawn deployable 1769 |
| 1770 | - | None | Fixed Weapon | neutral | 0 | 0 | 1.75 | 0 | \spawn deployable 1770 |
| 1771 | Mining Camp SIN Uplink | None | Target (secondary) | accord | 60 | 24000 | 1 | 40666 | \spawn deployable 1771 |
| 1772 | Kaimuki Research Station SIN Uplink | None | Target (secondary) | accord | 60 | 24000 | 1 | 40666 | \spawn deployable 1772 |
| 1773 | Chosen Bulwark  SIN Uplink | None | Target (secondary) | accord | 60 | 24000 | 1 | 40666 | \spawn deployable 1773 |
| 1774 | Crossroads Station SIN Uplink | None | Target (secondary) | accord | 60 | 24000 | 1 | 40666 | \spawn deployable 1774 |
| 1775 | Chosen Artillery SIN Uplink | None | Target (secondary) | accord | 60 | 24000 | 1 | 40666 | \spawn deployable 1775 |
| 1776 | Kanaloa Research Station SIN Uplink | None | Target (secondary) | accord | 60 | 24000 | 1 | 40666 | \spawn deployable 1776 |
| 1777 | Camp Jasper SIN Uplink | None | Target (secondary) | accord | 60 | 24000 | 1 | 40666 | \spawn deployable 1777 |
| 1778 | Chosen Prison Watch SIN Uplink | None | Target (secondary) | accord | 60 | 24000 | 1 | 40666 | \spawn deployable 1778 |
| 1779 | Chosen Omnidex SIN Uplink | None | Target (secondary) | accord | 60 | 24000 | 1 | 40666 | \spawn deployable 1779 |
| 1780 | Chosen Station SIN Uplink | None | Target (secondary) | accord | 60 | 24000 | 1 | 40666 | \spawn deployable 1780 |
| 1781 | Stronghold SIN Uplink | None | Target (secondary) | accord | 60 | 24000 | 1 | 40666 | \spawn deployable 1781 |
| 1782 | Research Station Dummy Explosion | None | Undefined | neutral | 0 | 0 | 1 | 100 | \spawn deployable 1782 |
| 1783 | Chinese New Year FireworksLauncher | Consumer Fireworks | Undefined | accord | 0 | 1 | 1 | 0 | \spawn deployable 1783 |
| 1784 | - | Generic Terminal | Deployed Target | accord | 0 | 600 | 1 | 0 | \spawn deployable 1784 |
| 1785 | - | None | Undefined | friendly | 0 | 9999999 | 3 | 0 | \spawn deployable 1785 |
| 1786 | - | Generic Terminal | Deployed Target | accord | 9 | 1 | 1 | 0 | \spawn deployable 1786 |
| 1787 | - | Mine | Undefined | accord | 0 | 9999999 | 1 | 500 | \spawn deployable 1787 |
| 1788 | - | Mine | Undefined | accord | 0 | 9999999 | 0.675 | 500 | \spawn deployable 1788 |
| 1789 | - | Vending Machine | Undefined | accord | 0 | 1 | 1 | 0 | \spawn deployable 1789 |
| 1790 | Cobalt Phoenix Glider Pad | Glider pad | Undefined | accord | 0.75 | 1 | 1 | 0 | \spawn deployable 1790 |
| 1791 | Repair Terminal | None | Undefined | accord | 0 | 1 | 1 | 0 | \spawn deployable 1791 |
| 1793 | - | None | Undefined | accord | 0 | 1 | 1 | 0 | \spawn deployable 1793 |
| 1794 | - | Mine | Undefined | neutral | 0 | 9999999 | 0.45 | 500 | \spawn deployable 1794 |
| 1795 | - | Shield | Undefined | bandit | 8.75 | 3500 | 1 | 0 | \spawn deployable 1795 |
| 1797 | Ammo Powerup | None | Undefined | accord | 0 | 1 | 1 | 0 | \spawn deployable 1797 |
| 1798 | - | None | Deployed Target | accord | 7.5 | 3000 | 1 | 3000 | \spawn deployable 1798 |
| 1799 | Health Powerup | None | Undefined | accord | 0 | 1 | 1.5 | 0 | \spawn deployable 1799 |
| 1800 | - | Tech Turret | Fixed Weapon | accord | 6 | 3000 | 1.5 | 5000 | \spawn deployable 1800 |
| 1801 | - | None | Undefined | bandit | 8.75 | 1750 | 1 | 0 | \spawn deployable 1801 |
| 1802 | _Dome | None | Undefined | melding | 0 | 0 | 0.4872 | 0 | \spawn deployable 1802 |
| 1803 | _Dome | None | Undefined | melding | 0 | 0 | 0.4676 | 0 | \spawn deployable 1803 |
| 1804 | Cherub Glider Pad | Glider pad | Undefined | accord | 0.75 | 1 | 1 | 0 | \spawn deployable 1804 |
| 1805 | Chosen Beacon | None | Undefined | chosen | 40 | 14400 | 1.5 | 8000 | \spawn deployable 1805 |
| 1806 | - | None | Undefined | chosen | 0 | 1 | 1 | 250 | \spawn deployable 1806 |
| 1807 | Present Box | None | Undefined | neutral | 0 | 1 | 1 | 0 | \spawn deployable 1807 |
| 1808 | Warbringer Pillar | None | Undefined | friendly | 0 | 1 | 1 | 250 | \spawn deployable 1808 |
| 1809 | Shield Generator | None | Target (primary) | accord | 25 | 9000 | 1 | 250 | \spawn deployable 1809 |
| 1810 | _Interaction Point | None | Undefined | friendly | 0 | 1 | 1 | 0 | \spawn deployable 1810 |
| 1811 | _Interaction Point | None | Undefined | friendly | 0 | 1 | 1 | 0 | \spawn deployable 1811 |
| 1812 | - | None | Target (primary) | chosen | 0 | 2000 | 0.75 | 0 | \spawn deployable 1812 |
| 1813 | - | None | Undefined | accord | 2.5 | 1 | 0.75 | 0 | \spawn deployable 1813 |
| 1814 | AA Materials | None | Interactable Objective | neutral | 0 | 1 | 1 | 0 | \spawn deployable 1814 |
| 1815 | Bridge | None | Undefined | accord | 0 | 1 | 1.3 | 0 | \spawn deployable 1815 |
| 1816 | - | None | Undefined | accord | 0 | 1 | 1 | 0 | \spawn deployable 1816 |
| 1817 | - | None | Healing | accord | 0 | 1 | 1 | 0 | \spawn deployable 1817 |
| 1818 | Chosen Drop Pod | Spawner | Deployed Target | chosen | 25 | 3500 | 1 | 10500 | \spawn deployable 1818 |
| 1819 | - | Glider pad | Undefined | accord | 0.75 | 1 | 1 | 0 | \spawn deployable 1819 |
| 1820 | AA Turret | None | Rest | accord | 0 | 0 | 1 | 1000 | \spawn deployable 1820 |
| 1821 | - | None | Fixed Weapon | chosen | 25 | 10000 | 0.4 | 0 | \spawn deployable 1821 |
| 1822 | - | None | Fixed Weapon | chosen | 125 | 50000 | 1 | 0 | \spawn deployable 1822 |
| 1823 | - | Tech Turret | Fixed Weapon | accord | 0 | 3000 | 1.5 | 5000 | \spawn deployable 1823 |
| 1824 | - | None | Undefined | accord | 0 | 1 | 1 | 0 | \spawn deployable 1824 |
| 1825 | - | None | Deployed Target | accord | 7.5 | 3000 | 1 | 3000 | \spawn deployable 1825 |
| 1826 | Set Charge | None | Undefined | accord | 0 | 1 | 1 | 0 | \spawn deployable 1826 |
| 1827 | _Dropship Vulnerable Spot | None | Target (primary) | accord | 11 | 1 | 1 | 0 | \spawn deployable 1827 |
| 1829 | - | None | Undefined | accord | 0 | 1 | 2 | 0 | \spawn deployable 1829 |
| 1830 | - | None | Rummage | accord | 0 | 0 | 1 | 0 | \spawn deployable 1830 |
| 1831 | Auxiliary Engine | None | Undefined | accord | 0 | 9999999 | 1 | 0 | \spawn deployable 1831 |
| 1832 | Bombardment Point for Dropship Cinematics | None | Undefined | friendly | 0 | 0 | 1 | 0 | \spawn deployable 1832 |
| 1833 | Chosen Technology | None | Undefined | accord | 0 | 0 | 1 | 0 | \spawn deployable 1833 |
| 1834 | King Cake | None | Recreation | accord | 0.75 | 1 | 1.5 | 0 | \spawn deployable 1834 |
| 1835 | - | None | Undefined | accord | 0 | 1 | 1 | 0 | \spawn deployable 1835 |
| 1836 | Stolen Supply Crate | Datapads | Undefined | neutral | 0 | 1 | 1.5 | 0 | \spawn deployable 1836 |
| 1837 | - | None | Undefined | accord | 0 | 1 | 1.3 | 0 | \spawn deployable 1837 |
| 1838 | - | None | Undefined | accord | 0 | 0 | 1 | 0 | \spawn deployable 1838 |
| 1839 | - | Shield | Undefined | accord | 0 | 0 | 1 | 0 | \spawn deployable 1839 |
| 1840 | - | Turret | Fixed Weapon | accord | 0 | 0 | 1.3 | 4000 | \spawn deployable 1840 |
| 1841 | - | None | Undefined | accord | 0 | 0 | 1 | 0 | \spawn deployable 1841 |
| 1842 | Shield Generator | None | Undefined | accord | 0 | 1 | 1.75 | 0 | \spawn deployable 1842 |
| 1843 | Meteor Bombardment - shoots the Invisible Crystite Meteor | None | Undefined | neutral | 0 | 1 | 1 | 0 | \spawn deployable 1843 |
| 1844 | Copy of Meteor Bombardment collision check | None | Undefined | neutral | 0 | 1 | 0.25 | 0 | \spawn deployable 1844 |
| 1845 | _CoverObject1 | None | Undefined | accord | 0 | 1 | 1 | 0 | \spawn deployable 1845 |
| 1846 | _CoverObject2 | None | Undefined | accord | 0 | 1 | 1 | 0 | \spawn deployable 1846 |
| 1847 | _CoverObject3 | None | Undefined | accord | 0 | 0 | 1 | 0 | \spawn deployable 1847 |
| 1848 | Accord Strike Marker | None | Undefined | accord | 0 | 1 | 1 | 0 | \spawn deployable 1848 |
| 1849 | - | Generic Terminal | Undefined | accord | 0 | 1 | 1 | 0 | \spawn deployable 1849 |
| 1850 | - | None | Undefined | melding | 0 | 0 | 1 | 0 | \spawn deployable 1850 |
| 1851 | - | Turret | Undefined | bandit | 1 | 7000 | 0.75 | 0 | \spawn deployable 1851 |
| 1852 | - | Generic Terminal | Undefined | accord | 0 | 1 | 1 | 0 | \spawn deployable 1852 |
| 1853 | - | None | Undefined | chosen | 1 | 200 | 1 | 0 | \spawn deployable 1853 |
| 1854 | _Hybrid Surface Deposit | Surface Deposit | Undefined | neutral | 0 | 14000 | 0.5 | 0 | \spawn deployable 1854 |
| 1855 | - | Turret | Play | accord | 0 | 1 | 1 | 0 | \spawn deployable 1855 |
| 1856 | - | None | Undefined | accord | 1 | 1 | 1 | 0 | \spawn deployable 1856 |
| 1857 | Horde - Chosen Drop Pod | Spawner | Deployed Target | chosen | 62.5 | 25000 | 1 | 250 | \spawn deployable 1857 |
| 1858 | - | Generic Terminal | Interactable Objective | accord | 0 | 1 | 1 | 0 | \spawn deployable 1858 |
| 1859 | Chosen Drop Pod | None | Deployed Target | chosen | 6.25 | 2500 | 1 | 250 | \spawn deployable 1859 |
| 1860 | - | None | Deployed Target | accord | 200 | 500 | 1 | 250 | \spawn deployable 1860 |
| 1862 | - | None | Deployed Target | accord | 200 | 500 | 1 | 250 | \spawn deployable 1862 |
| 1863 | - | None | Deployed Target | accord | 200 | 500 | 1 | 250 | \spawn deployable 1863 |
| 1864 | - | None | Deployed Target | accord | 200 | 500 | 1 | 250 | \spawn deployable 1864 |
| 1865 | _GlacierRock | None | Undefined | accord | 0 | 1 | 0.2 | 0 | \spawn deployable 1865 |
| 1866 | _GlacierBridge | None | Undefined | accord | 0 | 1 | 0.2 | 0 | \spawn deployable 1866 |
| 1867 | _GlacierCliff | None | Undefined | accord | 0 | 1 | 0.2 | 0 | \spawn deployable 1867 |
| 1868 | _CoverObject4 | None | Undefined | accord | 0 | 0 | 1.22 | 0 | \spawn deployable 1868 |
| 1869 | _GlacierIceBarrier | None | Undefined | accord | 0 | 0 | 1 | 0 | \spawn deployable 1869 |
| 1870 | - | None | Undefined | chosen | 0 | 1 | 2 | 0 | \spawn deployable 1870 |
| 1871 | - | None | Fixed Weapon | accord | 25 | 10000 | 0.5 | 0 | \spawn deployable 1871 |
| 1872 | - | None | Fixed Weapon | accord | 125 | 50000 | 1 | 0 | \spawn deployable 1872 |
| 1873 | - | None | Undefined | melding | 0 | 0 | 0.75 | 0 | \spawn deployable 1873 |
| 1874 | - | Power Cell Dispenser | Undefined | friendly | 0 | 1 | 1 | 0 | \spawn deployable 1874 |
| 1875 | - | None | Undefined | accord | 0 | 1 | 1 | 0 | \spawn deployable 1875 |
| 1876 | AA Turret | Mannable Turret | Fixed Weapon | accord | 0 | 0 | 1 | 0 | \spawn deployable 1876 |
| 1877 | Razorwind Bomb | None | Undefined | accord | 0 | 1 | 4 | 0 | \spawn deployable 1877 |
| 1878 | - | None | Undefined | accord | 0 | 1 | 1 | 0 | \spawn deployable 1878 |
| 1879 | - | None | Target (primary) | bandit | 5 | 1 | 1 | 11666 | \spawn deployable 1879 |
| 1880 | - | Mannable Turret | Fixed Weapon | chosen | 6.25 | 2500 | 1 | 1000 | \spawn deployable 1880 |
| 1881 | - | None | Target (primary) | bandit | 5 | 1 | 1 | 11666 | \spawn deployable 1881 |
| 1882 | - | None | Deployed Target | accord | 0.625 | 1 | 1 | 0 | \spawn deployable 1882 |
| 1883 | - | Turret | Undefined | bandit | 1 | 7000 | 0.75 | 0 | \spawn deployable 1883 |
| 1884 | - | None | Undefined | accord | 0 | 1 | 1 | 0 | \spawn deployable 1884 |
| 1885 | - | None | Undefined | accord | 0 | 1 | 1 | 0 | \spawn deployable 1885 |
| 1886 | - | None | Undefined | accord | 0 | 1 | 1 | 0 | \spawn deployable 1886 |
| 1887 | - | None | Undefined | accord | 0 | 1 | 1 | 0 | \spawn deployable 1887 |
| 1888 | - | None | Undefined | accord | 0 | 1 | 1 | 0 | \spawn deployable 1888 |
| 1889 | - | None | Undefined | accord | 0 | 1 | 1 | 0 | \spawn deployable 1889 |
| 1890 | - | None | Undefined | accord | 0 | 1 | 1 | 0 | \spawn deployable 1890 |
| 1891 | Community Fireworks: Mascleta | None | Undefined | accord | 0 | 1 | 1 | 0 | \spawn deployable 1891 |
| 1892 | Community Fireworks: Streamers | None | Undefined | accord | 0 | 1 | 1 | 0 | \spawn deployable 1892 |
| 1893 | Community Fireworks: Burst | None | Undefined | accord | 0 | 1 | 1 | 0 | \spawn deployable 1893 |
| 1894 | Fireworks - Epic Hero | None | Undefined | accord | 0 | 1 | 1 | 0 | \spawn deployable 1894 |
| 1895 | Fireworks, Airburst - Blue Sphere | None | Undefined | accord | 0 | 1 | 1 | 0 | \spawn deployable 1895 |
| 1896 | Fireworks, Airburst - Ring Sphere | None | Undefined | accord | 0 | 1 | 1 | 0 | \spawn deployable 1896 |
| 1897 | Fireworks, Airburst - Red Sphere | None | Undefined | accord | 0 | 1 | 1 | 0 | \spawn deployable 1897 |
| 1898 | - | None | Undefined | chosen | 25 | 1 | 1 | 0 | \spawn deployable 1898 |
| 1899 | Chosen Shield | None | Undefined | chosen | 25 | 1 | 1 | 0 | \spawn deployable 1899 |
| 1900 | Fireworks - Carnaval | None | Undefined | accord | 0 | 1 | 1 | 0 | \spawn deployable 1900 |
| 1901 | Community Fireworks: Carnaval | None | Undefined | accord | 0 | 1 | 1 | 0 | \spawn deployable 1901 |
| 1902 | Bandit Supplies | None | Undefined | accord | 0 | 0 | 1 | 0 | \spawn deployable 1902 |
| 1903 | Bandit Barricade | None | Undefined | accord | 0 | 0 | 1.5 | 0 | \spawn deployable 1903 |
| 1904 | Chosen Drop Pod | Spawner | Deployed Target | chosen | 15 | 7000 | 1 | 10500 | \spawn deployable 1904 |
| 1921 | Chosen Sin Tower | None | Target (secondary) | chosen | 0 | 0 | 1 | 18000 | \spawn deployable 1921 |
| 1922 | - | None | Undefined | accord | 0 | 1 | 1 | 0 | \spawn deployable 1922 |
| 1923 | Chosen ForceShields | Shield | Undefined | chosen | 20 | 7200 | 2 | 0 | \spawn deployable 1923 |
| 1924 | - | None | Undefined | chosen | 0 | 0 | 0 | 0 | \spawn deployable 1924 |
| 1925 | Hijacked Terminal | None | Interactable Objective | chosen | 0 | 0 | 1 | 0 | \spawn deployable 1925 |
| 1926 | Locked Hackable Terminal | None | Undefined | chosen | 0 | 0 | 1 | 0 | \spawn deployable 1926 |
| 1927 | Copy of Chosen Barricade | None | Undefined | chosen | 0 | 1 | 1 | 0 | \spawn deployable 1927 |
| 1928 | Copy of Weapons Crate | None | Undefined | accord | 0 | 1 | 1 | 5000 | \spawn deployable 1928 |
| 1929 | - | None | Undefined | neutral | 0 | 1 | 1 | 0 | \spawn deployable 1929 |
| 1930 | - | None | Target (secondary) | accord | 0 | 360 | 1 | 3000 | \spawn deployable 1930 |
| 1931 | - | None | Undefined | accord | 0 | 1 | 1 | 0 | \spawn deployable 1931 |
| 1932 | - | None | Undefined | neutral | 0 | 1 | 1 | 0 | \spawn deployable 1932 |
| 1933 | - | None | Undefined | gaea | 25 | 10000 | 1.25 | 3000 | \spawn deployable 1933 |
| 1934 | - | Spawner | Undefined | chosen | 0 | 1 | 1 | 250 | \spawn deployable 1934 |
| 1935 | - | None | Interactable Objective | accord | 0 | 1 | 1 | 0 | \spawn deployable 1935 |
| 1936 | Horde Mine | Mine | Undefined | neutral | 0.5 | 1 | 3 | 2000 | \spawn deployable 1936 |
| 1937 | - | None | Deployed Target | neutral | 0 | 0 | 1 | 250 | \spawn deployable 1937 |
| 1938 | - | None | Undefined | accord | 0 | 1 | 1 | 0 | \spawn deployable 1938 |
| 1940 | Plant Explosives | None | Undefined | neutral | 0 | 1 | 3 | 0 | \spawn deployable 1940 |
| 1941 | Suspicious Pile of Trash | None | Undefined | accord | 0 | 1 | 1 | 0 | \spawn deployable 1941 |
| 1942 | Human Corpse | Mine | Undefined | accord | 0 | 1 | 1 | 500 | \spawn deployable 1942 |
| 1943 | - | None | Undefined | accord | 0 | 1 | 1 | 3000 | \spawn deployable 1943 |
| 1944 | Courier LGV | None | Undefined | accord | 0 | 1 | 1 | 3000 | \spawn deployable 1944 |
| 1945 | - | None | Undefined | accord | 0 | 1 | 1 | 3000 | \spawn deployable 1945 |
| 1946 | Garbage Pile | None | Undefined | accord | 0 | 1 | 1 | 3000 | \spawn deployable 1946 |
| 1947 | - | None | Undefined | neutral | 0 | 1 | 1 | 0 | \spawn deployable 1947 |
| 1948 | Chosen Tech | None | Undefined | accord | 0 | 1 | 1 | 3000 | \spawn deployable 1948 |
| 1949 | - | None | Undefined | neutral | 0 | 1 | 1.5 | 0 | \spawn deployable 1949 |
| 1950 | - | None | Undefined | neutral | 0 | 1 | 1.25 | 0 | \spawn deployable 1950 |
| 1951 | - | None | Undefined | gaea | 0 | 10000 | 2.5 | 3000 | \spawn deployable 1951 |
| 1952 | - | Generic Terminal | Undefined | accord | 0 | 0 | 1 | 0 | \spawn deployable 1952 |
| 1953 | Arc Content - Aranhas Worker Lair + Spawns | None | Undefined | gaea | 25 | 10000 | 1.25 | 3000 | \spawn deployable 1953 |
| 1954 | - | None | Undefined | gaea | 25 | 10000 | 0.8 | 3000 | \spawn deployable 1954 |
| 1956 | - | None | Undefined | chosen | 0 | 1 | 1 | 0 | \spawn deployable 1956 |
| 1957 | - | None | Undefined | accord | 0 | 1 | 2 | 0 | \spawn deployable 1957 |
| 1958 | - | None | Undefined | accord | 0 | 0 | 1 | 0 | \spawn deployable 1958 |
| 1959 | - | None | Undefined | accord | 0 | 0 | 1 | 0 | \spawn deployable 1959 |
| 1960 | - | None | Undefined | neutral | 0 | 0 | 1 | 0 | \spawn deployable 1960 |
| 1961 | - | None | Undefined | friendly | 0 | 0 | 1 | 1000 | \spawn deployable 1961 |
| 1962 | Stolen Goods | None | Undefined | accord | 0 | 0 | 2 | 3000 | \spawn deployable 1962 |
| 1963 | - | None | Undefined | chosen | 0 | 1 | 1 | 0 | \spawn deployable 1963 |
| 1964 | - | None | Undefined | accord | 1 | 720 | 0.5 | 0 | \spawn deployable 1964 |
| 1965 | L - 99 - Sonic Detonator | None | Undefined | accord | 0 | 1 | 1 | 0 | \spawn deployable 1965 |
| 1966 | Power Supply | None | Undefined | accord | 0 | 1 | 2 | 0 | \spawn deployable 1966 |
| 1967 | - | Turret | Play | chosen | 0 | 1 | 1 | 2000 | \spawn deployable 1967 |
| 1968 | - | None | Undefined | accord | 0 | 1 | 0.869 | 0 | \spawn deployable 1968 |
| 1970 | - | None | Undefined | accord | 0 | 0 | 0.75 | 0 | \spawn deployable 1970 |
| 1971 | - | None | Undefined | accord | 0 | 0 | 1 | 0 | \spawn deployable 1971 |
| 1972 | - | None | Undefined | accord | 0 | 1 | 1 | 0 | \spawn deployable 1972 |
| 1973 | - | Turret | Fixed Weapon | neutral | 2 | 800 | 1 | 1000 | \spawn deployable 1973 |
| 1974 | Activate Generator | None | Interactable Objective | bandit | 0 | 0 | 1 | 0 | \spawn deployable 1974 |
| 1975 | - | None | Undefined | accord | 0 | 1 | 1 | 0 | \spawn deployable 1975 |
| 1976 | - | None | Undefined | accord | 0 | 1 | 1 | 0 | \spawn deployable 1976 |
| 1977 | - | None | Undefined | accord | 0 | 1 | 1 | 0 | \spawn deployable 1977 |
| 1978 | Chosen Ambient | None | Undefined | accord | 0 | 1 | 1 | 0 | \spawn deployable 1978 |
| 1979 | - | None | Undefined | neutral | 0 | 1 | 1 | 0 | \spawn deployable 1979 |
| 1980 | Aranha Pod | None | Undefined | accord | 0 | 1 | 1 | 3000 | \spawn deployable 1980 |
| 1981 | Dropship Pilot | None | Undefined | accord | 0 | 1 | 0.5 | 0 | \spawn deployable 1981 |
| 1982 | Crashed Dropship | None | Undefined | accord | 0 | 1 | 1 | 0 | \spawn deployable 1982 |
| 1983 | Weapons Crate | Power Cell Dispenser | Undefined | friendly | 0 | 1 | 4 | 0 | \spawn deployable 1983 |
| 1984 | Storage Crate | None | Undefined | accord | 0 | 1 | 1 | 0 | \spawn deployable 1984 |
| 1985 | Agent Corpse | None | Undefined | accord | 0 | 1 | 1 | 3000 | \spawn deployable 1985 |
| 1986 | Convoy | None | Target (primary) | accord | 150 | 21000 | 1 | 0 | \spawn deployable 1986 |
| 1987 | - | None | Undefined | accord | 0 | 0 | 1.5 | 0 | \spawn deployable 1987 |
| 1988 | - | None | Target (primary) | accord | 10 | 3600 | 1.5 | 0 | \spawn deployable 1988 |
| 1989 | Copy of _Melding Hazard | None | Undefined | chosen | 0 | 1 | 3 | 0 | \spawn deployable 1989 |
| 1990 | - | None | Undefined | accord | 0 | 1 | 3 | 0 | \spawn deployable 1990 |
| 1991 | - | None | Undefined | accord | 0 | 1 | 3 | 0 | \spawn deployable 1991 |
| 1992 | Orbital Comm Tower Antenna | None | Target (tertiary) | accord | 125 | 50000 | 0.5 | 0 | \spawn deployable 1992 |
| 1993 | Arcfolder | Turret | Target (secondary) | accord | 20 | 7200 | 1 | 0 | \spawn deployable 1993 |
| 1994 | - | None | Undefined | accord | 0 | 1 | 3 | 0 | \spawn deployable 1994 |
| 1995 | Arc Datapad | None | Interactable Objective | neutral | 0 | 0 | 1 | 0 | \spawn deployable 1995 |
| 1996 | - | Turret | Play | accord | 0 | 0 | 1 | 0 | \spawn deployable 1996 |
| 1997 | - | None | Undefined | accord | 0 | 1 | 1 | 0 | \spawn deployable 1997 |
| 1998 | - | None | Undefined | chosen | 12.5 | 4500 | 0.4 | 0 | \spawn deployable 1998 |
| 1999 | - | None | Undefined | accord | 0 | 1 | 1 | 0 | \spawn deployable 1999 |
| 2000 | - | None | Undefined | accord | 0 | 1 | 1 | 0 | \spawn deployable 2000 |
| 2001 | - | None | Undefined | accord | 0 | 1 | 1 | 0 | \spawn deployable 2001 |
| 2002 | - | None | Undefined | accord | 0 | 0 | 1 | 0 | \spawn deployable 2002 |
| 2003 | - | None | Undefined | chosen | 0 | 0 | 1.89 | 0 | \spawn deployable 2003 |
| 2004 | - | None | Undefined | gaea | 25 | 10000 | 0.8 | 3000 | \spawn deployable 2004 |
| 2005 | Satelite Bay | None | Undefined | accord | 0 | 1 | 3 | 0 | \spawn deployable 2005 |
| 2006 | Auxiliary Targeting Satelite Dish | Tech SIN | Target (primary) | accord | 30 | 35000 | 2 | 5000 | \spawn deployable 2006 |
| 2007 | - | None | Undefined | accord | 5 | 300 | 1 | 7000 | \spawn deployable 2007 |
| 2008 | Satelite Uplink Terminal | None | Undefined | accord | 0 | 0 | 1 | 0 | \spawn deployable 2008 |
| 2009 | - | None | Undefined | accord | 0 | 1 | 1 | 0 | \spawn deployable 2009 |
| 2010 | - | None | Undefined | accord | 0 | 1 | 1 | 0 | \spawn deployable 2010 |
| 2011 | - | None | Undefined | accord | 0 | 0 | 0.99 | 0 | \spawn deployable 2011 |
| 2012 | - | None | Undefined | accord | 0 | 1 | 1 | 0 | \spawn deployable 2012 |
| 2013 | Drone Cache | None | Undefined | accord | 0 | 1 | 1 | 0 | \spawn deployable 2013 |
| 2014 | Suspicious Terminal | None | Undefined | accord | 0 | 1 | 1 | 0 | \spawn deployable 2014 |
| 2015 | OCT Primary Targeting Satelite Dish | Tech SIN | Target (primary) | accord | 140 | 10000 | 1.5 | 5000 | \spawn deployable 2015 |
| 2016 | Instance Door | None | Undefined | accord | 0 | 0 | 1 | 0 | \spawn deployable 2016 |
| 2017 | - | None | Undefined | accord | 0 | 1 | 1 | 0 | \spawn deployable 2017 |
| 2029 | - | None | Undefined | accord | 0 | 1 | 1 | 0 | \spawn deployable 2029 |
| 2030 | - | None | Undefined | accord | 0 | 1 | 1 | 0 | \spawn deployable 2030 |
| 2031 | - | None | Undefined | accord | 0 | 1 | 1 | 0 | \spawn deployable 2031 |
| 2032 | - | None | Undefined | accord | 0 | 1 | 1 | 0 | \spawn deployable 2032 |
| 2033 | - | None | Undefined | accord | 0 | 1 | 1 | 0 | \spawn deployable 2033 |
| 2034 | - | None | Undefined | accord | 0 | 1 | 1 | 0 | \spawn deployable 2034 |
| 2035 | - | None | Undefined | accord | 0 | 1 | 1 | 0 | \spawn deployable 2035 |
| 2036 | - | None | Undefined | accord | 0 | 1 | 1 | 0 | \spawn deployable 2036 |
| 2037 | Medical Supplies | None | Undefined | accord | 0 | 1 | 1 | 0 | \spawn deployable 2037 |
| 2038 | Accord Storage Crate | None | Target (primary) | accord | 100 | 30000 | 0.5 | 0 | \spawn deployable 2038 |
| 2039 | - | None | Undefined | accord | 0 | 1 | 1 | 2500 | \spawn deployable 2039 |
| 2040 | - | Tech SIN | Undefined | accord | 0 | 1 | 1 | 10000 | \spawn deployable 2040 |
| 2041 | - | None | Deployed Target | accord | 150 | 500 | 3 | 250 | \spawn deployable 2041 |
| 2042 | Discarded Weapon | None | Deployed Target | accord | 0 | 1 | 1 | 0 | \spawn deployable 2042 |
| 2043 | - | None | Interactable Objective | accord | 0 | 0 | 3 | 250 | \spawn deployable 2043 |
| 2044 | Coral Formation | None | Undefined | accord | 0 | 1 | 0.7 | 3000 | \spawn deployable 2044 |
| 2045 | Red Flower | None | Undefined | accord | 0 | 1 | 1 | 0 | \spawn deployable 2045 |
| 2046 | Culex Spawning Nest | None | Undefined | accord | 0 | 1 | 2 | 3000 | \spawn deployable 2046 |
| 2047 | Culex Lair | None | Undefined | gaea | 25 | 10000 | 2 | 0 | \spawn deployable 2047 |
| 2048 | - | None | Undefined | accord | 0 | 0 | 1 | 0 | \spawn deployable 2048 |
| 2049 | Animal Trap | None | Undefined | accord | 1 | 0 | 1 | 0 | \spawn deployable 2049 |
| 2050 | - | None | Undefined | accord | 1 | 1 | 1 | 0 | \spawn deployable 2050 |
| 2051 | Animal Trap | None | Undefined | accord | 1 | 0 | 1 | 0 | \spawn deployable 2051 |
| 2052 | - | Turret | Fixed Weapon | accord | 3.5 | 1400 | 1.3 | 5000 | \spawn deployable 2052 |
| 2053 | Initialize Arcporter | None | Undefined | friendly | 0 | 1 | 5 | 0 | \spawn deployable 2053 |
| 2054 | - | None | Undefined | accord | 0 | 1 | 0.25 | 0 | \spawn deployable 2054 |
| 2055 | - | None | Undefined | accord | 1 | 1 | 1 | 0 | \spawn deployable 2055 |
| 2056 | Datapad | None | Undefined | accord | 0 | 0 | 2 | 0 | \spawn deployable 2056 |
| 2057 | Refurbished Crystite Reactor | None | Undefined | accord | 25 | 9000 | 0.75 | 0 | \spawn deployable 2057 |
| 2058 | - | None | Undefined | accord | 20 | 7200 | 1 | 0 | \spawn deployable 2058 |
| 2059 | Mechanical Debris | None | Undefined | accord | 22.5 | 8100 | 1 | 0 | \spawn deployable 2059 |
| 2060 | Mechanical Debris | None | Undefined | accord | 22.5 | 8100 | 1 | 0 | \spawn deployable 2060 |
| 2061 | - | None | Undefined | gaea | 24 | 8640 | 2 | 0 | \spawn deployable 2061 |
| 2062 | - | None | Undefined | accord | 0 | 1 | 1 | 0 | \spawn deployable 2062 |
| 2063 | Satelite Uplink Terminal | None | Undefined | accord | 0 | 0 | 1 | 0 | \spawn deployable 2063 |
| 2064 | - | None | Undefined | accord | 20 | 7200 | 1 | 0 | \spawn deployable 2064 |
| 2065 | Accord Container | None | Undefined | accord | 0 | 0 | 1 | 0 | \spawn deployable 2065 |
| 2066 | Accord Vehicle | None | Undefined | accord | 0 | 0 | 1 | 0 | \spawn deployable 2066 |
| 2067 | - | None | Undefined | accord | 0 | 0 | 1 | 0 | \spawn deployable 2067 |
| 2068 | - | None | Undefined | accord | 0 | 0 | 0.75 | 0 | \spawn deployable 2068 |
| 2069 | Bandit Barrel | None | Undefined | accord | 0 | 0 | 1 | 0 | \spawn deployable 2069 |
| 2070 | Torch | None | Undefined | accord | 0 | 0 | 1 | 300 | \spawn deployable 2070 |
| 2071 | Bandit Barricade | None | Undefined | accord | 0 | 0 | 1.5 | 0 | \spawn deployable 2071 |
| 2072 | Human Corpse | Mine | Undefined | accord | 0 | 1 | 1 | 500 | \spawn deployable 2072 |
| 2073 | Aranha  Remains | None | Undefined | accord | 20 | 7200 | 1.125 | 0 | \spawn deployable 2073 |
| 2074 | - | None | Undefined | accord | 0 | 0 | 0.75 | 0 | \spawn deployable 2074 |
| 2075 | - | None | Undefined | accord | 0 | 0 | 0.75 | 0 | \spawn deployable 2075 |
| 2076 | Explosives | Mine | Undefined | accord | 0 | 1 | 1 | 500 | \spawn deployable 2076 |
| 2077 | Feeder System | None | Undefined | accord | 25 | 9000 | 1.25 | 0 | \spawn deployable 2077 |
| 2078 | - | None | Undefined | accord | 0 | 1 | 1 | 0 | \spawn deployable 2078 |
| 2079 | SIN Imprint | None | Undefined | accord | 0 | 1 | 1 | 0 | \spawn deployable 2079 |
| 2080 | - | Spawner | Deployed Target | chosen | 12 | 4800 | 1.5 | 3000 | \spawn deployable 2080 |
| 2081 | - | None | Undefined | accord | 0 | 1 | 1 | 0 | \spawn deployable 2081 |
| 2082 | - | None | Undefined | accord | 0 | 1 | 1 | 0 | \spawn deployable 2082 |
| 2083 | Storm Kestrel Nest | None | Interactable Objective | accord | 15 | 6000 | 1 | 0 | \spawn deployable 2083 |
| 2084 | Hisser Hive | None | Undefined | accord | 7.5 | 3000 | 1 | 0 | \spawn deployable 2084 |
| 2085 | _Power Antenna | None | Undefined | accord | 0 | 0 | 1 | 5000 | \spawn deployable 2085 |
| 2087 | Rigged Sonic Detonator | None | Undefined | accord | 0 | 1 | 1 | 0 | \spawn deployable 2087 |
| 2088 | Carlo Fonseca | Mine | Undefined | accord | 0 | 1 | 1 | 500 | \spawn deployable 2088 |
| 2089 | Rebel Supplies | None | Undefined | accord | 0 | 0 | 2 | 0 | \spawn deployable 2089 |
| 2090 | - | None | Undefined | accord | 1 | 1 | 1 | 0 | \spawn deployable 2090 |
| 2092 | Tier 2 Melding Bubble Deposit (Level 1) | None | Undefined | melding | 1 | 400 | 1 | 0 | \spawn deployable 2092 |
| 2093 | Tier 2 Melding Bubble Deposit (Level 2) | None | Undefined | melding | 1 | 400 | 1 | 0 | \spawn deployable 2093 |
| 2094 | Tier 2 Melding Bubble Deposit (Level 3) | None | Undefined | melding | 1 | 400 | 1 | 0 | \spawn deployable 2094 |
| 2095 | Tier 3 Melding Bubble Deposit (Level 1) | None | Undefined | melding | 1 | 400 | 1 | 0 | \spawn deployable 2095 |
| 2096 | Tier 3 Melding Bubble Deposit (Level 2) | None | Undefined | melding | 1 | 400 | 1 | 0 | \spawn deployable 2096 |
| 2097 | Tier 3 Melding Bubble Deposit (Level 3) | None | Undefined | melding | 1 | 400 | 1 | 0 | \spawn deployable 2097 |
| 2098 | - | None | Undefined | melding | 1 | 400 | 1 | 0 | \spawn deployable 2098 |
| 2099 | - | None | Undefined | melding | 1 | 400 | 1 | 0 | \spawn deployable 2099 |
| 2100 | - | None | Undefined | melding | 1 | 400 | 1 | 0 | \spawn deployable 2100 |
| 2101 | - | None | Undefined | accord | 1 | 1 | 1 | 0 | \spawn deployable 2101 |
| 2102 | Chosen Strifebringer | None | Play | accord | 0 | 1 | 1 | 0 | \spawn deployable 2102 |
| 2103 | - | None | Undefined | accord | 1 | 1 | 3 | 0 | \spawn deployable 2103 |
| 2104 | Ravaged Food Crate | None | Undefined | accord | 0 | 1 | 0.5 | 0 | \spawn deployable 2104 |
| 2105 | Nutretic Shipment - To Copacabana | None | Undefined | accord | 0 | 1 | 2 | 0 | \spawn deployable 2105 |
| 2106 | Nutretic Shipment - To Trans Hub | None | Undefined | accord | 0 | 1 | 0.25 | 0 | \spawn deployable 2106 |
| 2107 | Nutretic Shipment - To Thump Dump | None | Undefined | accord | 0 | 1 | 1 | 0 | \spawn deployable 2107 |
| 2108 | Thumpers | None | Undefined | accord | 0 | 1 | 1 | 0 | \spawn deployable 2108 |
| 2109 | - | None | Undefined | accord | 0 | 1 | 1 | 0 | \spawn deployable 2109 |
| 2110 | SIN Jamming Array | Mine | Undefined | accord | 0 | 1 | 1 | 500 | \spawn deployable 2110 |
| 2111 | Hisser Carapace | Repair Station | Healing | accord | 0 | 0 | 1 | 3000 | \spawn deployable 2111 |
| 2112 | Large Cover | None | Undefined | accord | 0 | 0 | 1 | 0 | \spawn deployable 2112 |
| 2113 | - | None | Undefined | accord | 0 | 1 | 1 | 0 | \spawn deployable 2113 |
| 2114 | Large Cover | None | Undefined | accord | 0 | 0 | 1 | 0 | \spawn deployable 2114 |
| 2115 | Large Cover | None | Undefined | accord | 0 | 0 | 1 | 0 | \spawn deployable 2115 |
| 2116 | Bandit Torch | None | Undefined | accord | 0 | 1 | 1 | 0 | \spawn deployable 2116 |
| 2117 | - | None | Undefined | accord | 0 | 1 | 1 | 0 | \spawn deployable 2117 |
| 2118 | - | None | Undefined | accord | 0 | 1 | 1 | 0 | \spawn deployable 2118 |
| 2119 | Hisser Lair | None | Undefined | accord | 7.5 | 3000 | 1 | 0 | \spawn deployable 2119 |
| 2120 | - | None | Target (secondary) | accord | 0 | 3000 | 0.9 | 0 | \spawn deployable 2120 |
| 2121 | - | None | Undefined | accord | 1 | 1 | 1 | 0 | \spawn deployable 2121 |
| 2122 | Glider Pad | Glider pad | Undefined | accord | 0 | 0 | 1 | 0 | \spawn deployable 2122 |
| 2123 | SIN Sensor Tech Point | Tech SIN | Undefined | accord | 0 | 1 | 1 | 10000 | \spawn deployable 2123 |
| 2124 | Bandit Cave Door | None | Undefined | accord | 0 | 1 | 0.85 | 0 | \spawn deployable 2124 |
| 2125 | Melded Tree | None | Undefined | melding | 0 | 7200 | 0.25 | 0 | \spawn deployable 2125 |
| 2126 | Melded Tree | None | Undefined | melding | 0 | 7200 | 0.3 | 0 | \spawn deployable 2126 |
| 2127 | - | None | Undefined | accord | 0 | 1 | 1 | 0 | \spawn deployable 2127 |
| 2129 | Stolen Goods | None | Healing | accord | 0 | 0 | 2 | 3000 | \spawn deployable 2129 |
| 2130 | Stolen Goods | None | Healing | accord | 0 | 0 | 2 | 3000 | \spawn deployable 2130 |
| 2131 | Sensor | None | Undefined | accord | 0 | 0 | 1 | 0 | \spawn deployable 2131 |
| 2132 | - | None | Undefined | accord | 0 | 0 | 5 | 0 | \spawn deployable 2132 |
| 2133 | - | None | Target (primary) | accord | 7.5 | 4000 | 1 | 5000 | \spawn deployable 2133 |
| 2134 | Vector Host | Tech SIN | Undefined | accord | 0 | 1 | 1 | 10000 | \spawn deployable 2134 |
| 2135 | Thumper | None | Undefined | accord | 0 | 1 | 1 | 0 | \spawn deployable 2135 |
| 2136 | - | None | Undefined | neutral | 0 | 0 | 1 | 0 | \spawn deployable 2136 |
| 2137 | Job Board - Dredge | None | Undefined | accord | 0 | 1 | 1 | 0 | \spawn deployable 2137 |
| 2138 | - | None | Undefined | accord | 0 | 1 | 1 | 0 | \spawn deployable 2138 |
| 2139 | Job Board - The Nest | None | Undefined | accord | 0 | 1 | 1 | 0 | \spawn deployable 2139 |
| 2140 | - | None | Undefined | accord | 0 | 1 | 1 | 0 | \spawn deployable 2140 |
| 2141 | Job Board - Omnidyne Facility | None | Undefined | accord | 0 | 1 | 1 | 0 | \spawn deployable 2141 |
| 2142 | - | None | Undefined | accord | 0 | 1 | 1 | 0 | \spawn deployable 2142 |
| 2143 | Crate of SIN Hacks | None | Undefined | accord | 0 | 0 | 2 | 0 | \spawn deployable 2143 |
| 2144 | Battleframe Garage | Forge | Undefined | accord | 0 | 1 | 1 | 0 | \spawn deployable 2144 |
| 2145 | SIN Tower Interface | None | Undefined | accord | 0 | 50 | 1 | 0 | \spawn deployable 2145 |
| 2146 | Pylon 37 | None | Target (secondary) | accord | 100 | 16000 | 1 | 0 | \spawn deployable 2146 |
| 2147 | Culex Brains | None | Undefined | accord | 20 | 7200 | 1 | 0 | \spawn deployable 2147 |
| 2148 | Hisser Lair | None | Undefined | gaea | 7.5 | 3000 | 1 | 0 | \spawn deployable 2148 |
| 2149 | Rock | None | Undefined | accord | 0.25 | 1 | 1 | 0 | \spawn deployable 2149 |
| 2150 | Rifle Case | None | Undefined | accord | 0 | 1 | 1 | 3000 | \spawn deployable 2150 |
| 2151 | Wiley's Cache | None | Undefined | accord | 0 | 1 | 1 | 0 | \spawn deployable 2151 |
| 2152 | Bottle | None | Undefined | accord | 0 | 1 | 1 | 0 | \spawn deployable 2152 |
| 2153 | - | Turret | Fixed Weapon | accord | 10 | 4500 | 1.3 | 4000 | \spawn deployable 2153 |
| 2154 | Wiley's Cache | None | Undefined | accord | 0 | 0 | 3 | 0 | \spawn deployable 2154 |
| 2155 | Wiley's LGV | None | Target (primary) | accord | 200 | 78000 | 1 | 0 | \spawn deployable 2155 |
| 2156 | - | None | Undefined | accord | 0 | 1 | 1 | 0 | \spawn deployable 2156 |
| 2158 | Bandit Terminal | None | Undefined | accord | 0 | 1 | 1 | 0 | \spawn deployable 2158 |
| 2159 | Shell Fragment | None | Undefined | accord | 20 | 7200 | 1.125 | 0 | \spawn deployable 2159 |
| 2160 | Power Generator | None | Undefined | accord | 7.5 | 4000 | 0.25 | 5000 | \spawn deployable 2160 |
| 2161 | - | None | Undefined | accord | 0 | 1 | 1 | 0 | \spawn deployable 2161 |
| 2162 | Data Drive | None | Undefined | accord | 0.0025 | 1 | 2 | 0 | \spawn deployable 2162 |
| 2163 | Shipment | None | Undefined | accord | 0 | 1 | 1 | 0 | \spawn deployable 2163 |
| 2165 | Place Bait Here | None | Undefined | neutral | 0 | 0 | 1 | 0 | \spawn deployable 2165 |
| 2166 | Chosen Arm Piece | None | Undefined | accord | 1 | 1 | 1 | 0 | \spawn deployable 2166 |
| 2167 | Chosen Head Piece | None | Undefined | accord | 1 | 1 | 1 | 0 | \spawn deployable 2167 |
| 2169 | Chosen Scarecrow | None | Target (primary) | accord | 20 | 7800 | 1 | 5000 | \spawn deployable 2169 |
| 2170 | - | None | Deployed Target | accord | 150 | 500 | 3 | 250 | \spawn deployable 2170 |
| 2171 | - | None | Deployed Target | accord | 150 | 500 | 3 | 250 | \spawn deployable 2171 |
| 2172 | Dredge SIN Uplink | None | Target (secondary) | accord | 125 | 50000 | 1 | 40666 | \spawn deployable 2172 |
| 2173 | Andreev Station SIN Uplink | None | Target (secondary) | accord | 125 | 50000 | 1 | 40666 | \spawn deployable 2173 |
| 2174 | The Nest SIN Uplink | None | Target (secondary) | accord | 125 | 50000 | 1 | 40666 | \spawn deployable 2174 |
| 2175 | Buzzard's Stash | None | Undefined | accord | 0 | 1 | 1 | 0 | \spawn deployable 2175 |
| 2176 | Chemical Amplifier | None | Undefined | accord | 0 | 0 | 1 | 3000 | \spawn deployable 2176 |
| 2177 | Wargrim Meat | Repair Station | Healing | accord | 0 | 0 | 1 | 3000 | \spawn deployable 2177 |
| 2178 | Lab 16 SIN Uplink | None | Target (secondary) | accord | 125 | 50000 | 1 | 40666 | \spawn deployable 2178 |
| 2179 | - | None | Undefined | accord | 1 | 3000 | 1 | 5000 | \spawn deployable 2179 |
| 2180 | Wargrim Meat | Repair Station | Healing | accord | 0 | 0 | 1 | 3000 | \spawn deployable 2180 |
| 2181 | Deep Mineral Thumper | None | Target (primary) | accord | 50 | 15000 | 1 | 13000 | \spawn deployable 2181 |
| 2182 | - | None | Rest | accord | 0 | 0 | 1 | 0 | \spawn deployable 2182 |
| 2183 | Dwarf Harvester | None | Undefined | accord | 0 | 1 | 0.5 | 0 | \spawn deployable 2183 |
| 2184 | Toolbox | None | Undefined | accord | 20 | 7200 | 1 | 0 | \spawn deployable 2184 |
| 2185 | Crystite | None | Undefined | accord | 20 | 7200 | 1 | 0 | \spawn deployable 2185 |
| 2186 | Drill Parts | None | Undefined | accord | 20 | 7200 | 1 | 0 | \spawn deployable 2186 |
| 2187 | Cognac's Drill | None | Undefined | accord | 0 | 0 | 2 | 0 | \spawn deployable 2187 |
| 2188 | Giant Drill | None | Undefined | accord | 0 | 1 | 1 | 0 | \spawn deployable 2188 |
| 2189 | Mining Machine | None | Target (primary) | accord | 0 | 1 | 1 | 0 | \spawn deployable 2189 |
| 2190 | Repair Crane | None | Undefined | accord | 0 | 1 | 0.5 | 0 | \spawn deployable 2190 |
| 2191 | Drill Parts | None | Undefined | accord | 0 | 1 | 2 | 0 | \spawn deployable 2191 |
| 2192 | Mining Crates | None | Undefined | accord | 0 | 1 | 1 | 0 | \spawn deployable 2192 |
| 2193 | Light Post | None | Undefined | accord | 0 | 1 | 1 | 0 | \spawn deployable 2193 |
| 2194 | - | None | Undefined | accord | 0 | 1 | 1 | 0 | \spawn deployable 2194 |
| 2195 | - | None | Undefined | accord | 0 | 1 | 1 | 0 | \spawn deployable 2195 |
| 2196 | Tanken Badge | None | Undefined | accord | 1 | 1 | 0.05 | 0 | \spawn deployable 2196 |
| 2197 | Toxic Dispencer | None | Undefined | accord | 25 | 9000 | 1 | 0 | \spawn deployable 2197 |
| 2198 | Arc Content - Safe Anchor Point | None | Undefined | accord | 0.75 | 300 | 1 | 0 | \spawn deployable 2198 |
| 2199 | Magneto Mine | None | Undefined | bandit | 0 | 1 | 1 | 0 | \spawn deployable 2199 |
| 2200 | Proximity Sensor | Tech SIN | Undefined | accord | 0 | 1 | 1 | 10000 | \spawn deployable 2200 |
| 2201 | _Community Event Flag | Generic Terminal | Play | accord | 0 | 1 | 0.25 | 0 | \spawn deployable 2201 |
| 2202 | - | Generic Terminal | Undefined | accord | 0 | 1 | 1 | 0 | \spawn deployable 2202 |
| 2203 | Broadcast Antenna | None | Undefined | accord | 0 | 1 | 0.25 | 0 | \spawn deployable 2203 |
| 2204 | Flag Base | None | Undefined | accord | 0 | 1 | 0.5 | 0 | \spawn deployable 2204 |
| 2205 | Torch | None | Undefined | accord | 0 | 0 | 1 | 300 | \spawn deployable 2205 |
| 2209 | Weapons Cache | Power Cell Dispenser | Undefined | friendly | 0 | 1 | 1 | 0 | \spawn deployable 2209 |
| 2210 | Signal Buoy Anchor Point | None | Undefined | neutral | 0 | 0 | 1 | 0 | \spawn deployable 2210 |
| 2211 | Signal Buoy | None | Undefined | accord | 0 | 1 | 1 | 0 | \spawn deployable 2211 |
| 2212 | Chosen Technology | None | Undefined | accord | 0 | 1 | 0.2 | 3000 | \spawn deployable 2212 |
| 2213 | Chosen Technology | None | Undefined | accord | 0 | 1 | 0.2 | 3000 | \spawn deployable 2213 |
| 2214 | Food | None | Undefined | accord | 20 | 7200 | 1 | 0 | \spawn deployable 2214 |
| 2215 | Propaganda | None | Undefined | accord | 0 | 0 | 0.8 | 0 | \spawn deployable 2215 |
| 2216 | Accord Convoy | None | Undefined | accord | 0 | 1 | 0.2 | 3000 | \spawn deployable 2216 |
| 2217 | Datapad | None | Undefined | accord | 0 | 0 | 2 | 0 | \spawn deployable 2217 |
| 2218 | - | None | Repair Work | accord | 0 | 64000 | 0.7 | 0 | \spawn deployable 2218 |
| 2219 | Food Crate | None | Undefined | accord | 0 | 1 | 2 | 0 | \spawn deployable 2219 |
| 2220 | Passcode | None | Undefined | accord | 0 | 9999999 | 1 | 500 | \spawn deployable 2220 |
| 2221 | Accord Storage Crate | None | Undefined | accord | 0 | 1 | 0.5 | 0 | \spawn deployable 2221 |
| 2222 | Aranha Pod | None | Undefined | accord | 0 | 1 | 1 | 3000 | \spawn deployable 2222 |
| 2223 | - | None | Undefined | accord | 0 | 1 | 1 | 0 | \spawn deployable 2223 |
| 2224 | LGV Station | None | Undefined | accord | 0 | 1 | 1 | 0 | \spawn deployable 2224 |
| 2225 | Illegal Ammo | None | Undefined | accord | 1.25 | 1 | 1 | 300 | \spawn deployable 2225 |
| 2226 | Turret Calldown Point | None | Undefined | accord | 0 | 1 | 5 | 0 | \spawn deployable 2226 |
| 2227 | - | None | Undefined | accord | 0 | 1 | 1 | 1000 | \spawn deployable 2227 |
| 2228 | Tissue Sample | None | Undefined | accord | 20 | 7200 | 1 | 0 | \spawn deployable 2228 |
| 2229 | Astrek Tech | None | Undefined | accord | 0 | 1 | 1 | 1000 | \spawn deployable 2229 |
| 2230 | Omnidyne-M Turret | Turret | Fixed Weapon | accord | 125 | 3000 | 2 | 3000 | \spawn deployable 2230 |
| 2231 | Luau Larry Terminal | None | Undefined | accord | 0 | 0 | 1 | 0 | \spawn deployable 2231 |
| 2232 | Irradiated Tech Fragment | None | Undefined | accord | 0 | 1 | 0.125 | 5000 | \spawn deployable 2232 |
| 2233 | - | None | Undefined | accord | 90 | 32400 | 1 | 13000 | \spawn deployable 2233 |
| 2234 | ARES Pilot 1138 | Mine | Undefined | accord | 0 | 1 | 1 | 500 | \spawn deployable 2234 |
| 2235 | Chosen Drop Pod | Turret | Undefined | accord | 40 | 12500 | 1 | 250 | \spawn deployable 2235 |
| 2236 | - | None | Undefined | accord | 0 | 1 | 1 | 0 | \spawn deployable 2236 |
| 2237 | - | None | Undefined | accord | 0 | 1 | 1 | 0 | \spawn deployable 2237 |
| 2238 | - | None | Undefined | accord | 0 | 1 | 1 | 0 | \spawn deployable 2238 |
| 2239 | - | None | Undefined | accord | 0 | 1 | 1 | 0 | \spawn deployable 2239 |
| 2240 | - | None | Undefined | accord | 0 | 1 | 1 | 0 | \spawn deployable 2240 |
| 2241 | - | None | Undefined | accord | 0 | 1 | 1 | 0 | \spawn deployable 2241 |
| 2242 | - | None | Undefined | accord | 0 | 1 | 1 | 0 | \spawn deployable 2242 |
| 2243 | Job Board Terminal  - Sunken Harbor | None | Undefined | accord | 0 | 1 | 1 | 0 | \spawn deployable 2243 |
| 2244 | - | None | Undefined | accord | 0 | 1 | 1 | 0 | \spawn deployable 2244 |
| 2245 | Job Board Terminal  - Dredge | None | Undefined | accord | 0 | 1 | 1 | 0 | \spawn deployable 2245 |
| 2246 | - | None | Undefined | accord | 0 | 1 | 1 | 0 | \spawn deployable 2246 |
| 2247 | Job Board Terminal  - The Nest | None | Undefined | accord | 0 | 1 | 1 | 0 | \spawn deployable 2247 |
| 2248 | - | None | Undefined | accord | 0 | 1 | 1 | 0 | \spawn deployable 2248 |
| 2249 | Job Board Terminal  - Omnidyne Facility | None | Undefined | accord | 0 | 1 | 1 | 0 | \spawn deployable 2249 |
| 2250 | - | None | Undefined | accord | 0 | 1 | 1 | 0 | \spawn deployable 2250 |
| 2251 | - | None | Undefined | accord | 20 | 7200 | 1 | 0 | \spawn deployable 2251 |
| 2252 | Incinerator | None | Undefined | accord | 0 | 1 | 1 | 0 | \spawn deployable 2252 |
| 2253 | Irradiated Tech Fragment | None | Undefined | accord | 0 | 1 | 0.125 | 5000 | \spawn deployable 2253 |
| 2254 | SIN Terminal | None | Undefined | accord | 0 | 1 | 1 | 0 | \spawn deployable 2254 |
| 2255 | Mecham's LGV | None | Undefined | accord | 0 | 1 | 1 | 0 | \spawn deployable 2255 |
| 2256 | Waffle Mix | None | Target (primary) | accord | 0.025 | 10 | 1 | 0 | \spawn deployable 2256 |
| 2257 | Copy of Signal Scanning Antenna | None | Undefined | accord | 0 | 1 | 0.5 | 1000 | \spawn deployable 2257 |
| 2258 | Chosen Mortar | None | Fixed Weapon | chosen | 0 | 1 | 0.125 | 3000 | \spawn deployable 2258 |
| 2259 | - | None | Target (primary) | accord | 0.025 | 10 | 1 | 0 | \spawn deployable 2259 |
| 2260 | Ruby Maser | None | Undefined | accord | 0 | 1 | 4 | 5000 | \spawn deployable 2260 |
| 2261 | Omnidyne-M Storage Crate | None | Undefined | accord | 0 | 1 | 1.5 | 0 | \spawn deployable 2261 |
| 2262 | Scan Location | None | Undefined | accord | 0 | 0 | 1 | 0 | \spawn deployable 2262 |
| 2263 | - | None | Repair Work | accord | 0 | 64000 | 0.9 | 0 | \spawn deployable 2263 |
| 2264 | - | None | Repair Work | accord | 0 | 64000 | 1.1 | 0 | \spawn deployable 2264 |
| 2265 | - | None | Undefined | accord | 0 | 1 | 1 | 0 | \spawn deployable 2265 |
| 2266 | Recon Drone Deploy | None | Undefined | accord | 0 | 1 | 5 | 0 | \spawn deployable 2266 |
| 2267 | Recon Drone | None | Target (primary) | accord | 100 | 30000 | 0.5 | 5000 | \spawn deployable 2267 |
| 2268 | Fireworks Control Panel | None | Undefined | accord | 0 | 1 | 1 | 0 | \spawn deployable 2268 |
| 2269 | Signal Relay | None | Target (primary) | accord | 250 | 97500 | 0.25 | 0 | \spawn deployable 2269 |
| 2270 | - | None | Undefined | gaea | 13.33 | 0 | 2 | 0 | \spawn deployable 2270 |
| 2271 | Signal Relay Point | None | Target (primary) | accord | 0 | 5000 | 1 | 0 | \spawn deployable 2271 |
| 2272 | Lung Beetle Carapace | Repair Station | Healing | accord | 0 | 0 | 1 | 3000 | \spawn deployable 2272 |
| 2273 | Portable SIN Scanner | None | Undefined | accord | 0 | 9999999 | 1 | 500 | \spawn deployable 2273 |
| 2274 | Signs of a Struggle | None | Undefined | neutral | 0 | 0 | 1 | 0 | \spawn deployable 2274 |
| 2275 | Anti-Personnel Auto-Turret | Tech Turret | Fixed Weapon | accord | 16 | 1500 | 1.5 | 5000 | \spawn deployable 2275 |
| 2276 | Infected Chosen Tissue Sample | None | Undefined | accord | 20 | 7200 | 1 | 0 | \spawn deployable 2276 |
| 2277 | Arc Content - Devil's Tusk Ambient Large | None | Undefined | accord | 0 | 1 | 0.4 | 3000 | \spawn deployable 2277 |
| 2278 | - | None | Undefined | accord | 0 | 1 | 1 | 0 | \spawn deployable 2278 |
| 2279 | - | None | Undefined | accord | 0 | 1 | 1 | 3000 | \spawn deployable 2279 |
| 2280 | - | None | Undefined | accord | 0 | 0 | 0.35 | 0 | \spawn deployable 2280 |
| 2281 | SIN Hack Cache | None | Undefined | accord | 0 | 1 | 1 | 0 | \spawn deployable 2281 |
| 2284 | Transit to Baneclaw Lair | None | Undefined | accord | 0 | 1 | 0.7 | 0 | \spawn deployable 2284 |
| 2285 | Chosen Technology | None | Undefined | accord | 0 | 1 | 0.2 | 3000 | \spawn deployable 2285 |
| 2286 | Crashed thumper | None | Undefined | accord | 50 | 1 | 1 | 0 | \spawn deployable 2286 |
| 2287 | Pylon | None | Target (secondary) | accord | 100 | 16000 | 1 | 0 | \spawn deployable 2287 |
| 2288 | Fire Gland | None | Undefined | accord | 1 | 1 | 1 | 0 | \spawn deployable 2288 |
| 2289 | Target Drone | None | Undefined | neutral | 0.75 | 1 | 1 | 0 | \spawn deployable 2289 |
| 2290 | Copy of Accord Vehicle | None | Undefined | accord | 0 | 0 | 1 | 0 | \spawn deployable 2290 |
| 2291 | Accord Banner | None | Undefined | accord | 0 | 1 | 1 | 0 | \spawn deployable 2291 |
| 2292 | - | None | Undefined | accord | 0 | 1 | 0.5 | 0 | \spawn deployable 2292 |
| 2293 | - | None | Undefined | accord | 0 | 1 | 4 | 0 | \spawn deployable 2293 |
| 2294 | - | None | Undefined | accord | 0 | 1 | 4 | 0 | \spawn deployable 2294 |
| 2295 | - | None | Undefined | accord | 0 | 1 | 1 | 0 | \spawn deployable 2295 |
| 2296 | Rifle | None | Undefined | accord | 0 | 1 | 1.3 | 0 | \spawn deployable 2296 |
| 2297 | Crashed Thumper | None | Undefined | accord | 0 | 0 | 1 | 0 | \spawn deployable 2297 |
| 2298 | - | None | Target (tertiary) | accord | 60 | 24000 | 1 | 11666 | \spawn deployable 2298 |
| 2299 | - | Arcporter | Undefined | accord | 0 | 1 | 1 | 1 | \spawn deployable 2299 |
| 2300 | Portal | None | Undefined | accord | 0 | 1 | 1 | 0 | \spawn deployable 2300 |
| 2301 | - | None | Target (primary) | bandit | 15 | 4500 | 1 | 0 | \spawn deployable 2301 |
| 2302 | Accord Dog Tag | None | Undefined | accord | 0 | 1 | 0.5 | 0 | \spawn deployable 2302 |
| 2303 | - | None | Undefined | accord | 0 | 1 | 5 | 0 | \spawn deployable 2303 |
| 2304 | Chosen Barrel | None | Undefined | accord | 3.75 | 1 | 1 | 0 | \spawn deployable 2304 |
| 2305 | - | None | Undefined | accord | 0 | 1 | 1 | 0 | \spawn deployable 2305 |
| 2306 | - | None | Undefined | accord | 0 | 1 | 1 | 0 | \spawn deployable 2306 |
| 2307 | - | None | Undefined | accord | 0 | 1 | 1 | 0 | \spawn deployable 2307 |
| 2308 | - | None | Undefined | accord | 0 | 1 | 1 | 0 | \spawn deployable 2308 |
| 2309 | - | None | Undefined | accord | 0 | 1 | 1 | 0 | \spawn deployable 2309 |
| 2310 | - | None | Undefined | accord | 0 | 1 | 1 | 0 | \spawn deployable 2310 |
| 2311 | - | None | Undefined | accord | 0 | 1 | 1 | 0 | \spawn deployable 2311 |
| 2312 | - | None | Undefined | accord | 0 | 1 | 1 | 0 | \spawn deployable 2312 |
| 2313 | - | None | Undefined | accord | 0 | 1 | 1 | 0 | \spawn deployable 2313 |
| 2314 | - | None | Undefined | accord | 0 | 1 | 1 | 0 | \spawn deployable 2314 |
| 2315 | - | None | Undefined | accord | 0 | 1 | 1 | 0 | \spawn deployable 2315 |
| 2316 | - | None | Undefined | accord | 0 | 1 | 1 | 0 | \spawn deployable 2316 |
| 2317 | Forward Operating Base Sagan SIN Uplink | None | Target (secondary) | accord | 125 | 50000 | 1 | 40666 | \spawn deployable 2317 |
| 2318 | Tecumseh Airbase SIN Uplink | None | Target (secondary) | accord | 125 | 50000 | 1 | 40666 | \spawn deployable 2318 |
| 2319 | Forest Watch SIN Uplink | None | Target (secondary) | accord | 125 | 50000 | 1 | 40666 | \spawn deployable 2319 |
| 2320 | Scorcher Bait | None | Target (secondary) | accord | 1000 | 390000 | 1 | 0 | \spawn deployable 2320 |
| 2321 | Place Bait | None | Undefined | accord | 0 | 1 | 5 | 0 | \spawn deployable 2321 |
| 2322 | Delirium Engine | None | Undefined | chosen | 15 | 4500 | 1 | 0 | \spawn deployable 2322 |
| 2323 | Dead Holmganger | Mine | Undefined | accord | 0 | 1 | 1 | 500 | \spawn deployable 2323 |
| 2324 | Case of Hooch | None | Undefined | accord | 0 | 1 | 2 | 0 | \spawn deployable 2324 |
| 2325 | Disabled Camera Bot | Mine | Undefined | accord | 0 | 1 | 1 | 500 | \spawn deployable 2325 |
| 2326 | Thudder | None | Undefined | accord | 37.5 | 1 | 1 | 13000 | \spawn deployable 2326 |
| 2327 | - | None | Deployed Target | accord | 0 | 0 | 0.7 | 0 | \spawn deployable 2327 |
| 2328 | - | None | Undefined | accord | 0 | 1 | 1 | 0 | \spawn deployable 2328 |
| 2329 | - | None | Undefined | accord | 0 | 0 | 1 | 0 | \spawn deployable 2329 |
| 2330 | Ophanim Crate | None | Undefined | accord | 0 | 1 | 1 | 0 | \spawn deployable 2330 |
| 2331 | Firejacket Meat | None | Undefined | accord | 1 | 1 | 0.8 | 0 | \spawn deployable 2331 |
| 2332 | Accord Plasma Cannon | None | Undefined | accord | 0 | 0 | 1 | 0 | \spawn deployable 2332 |
| 2333 | - | None | Undefined | accord | 0 | 1 | 4 | 0 | \spawn deployable 2333 |
| 2334 | Strifebringer | Turret | Play | chosen | 0 | 0 | 1 | 0 | \spawn deployable 2334 |
| 2335 | - | None | Undefined | accord | 0 | 5000 | 0.001 | 0 | \spawn deployable 2335 |
| 2336 | Pile of Trash | None | Undefined | accord | 0 | 1 | 1 | 0 | \spawn deployable 2336 |
| 2337 | Proximity Sensor | None | Undefined | accord | 0 | 5000 | 0.25 | 0 | \spawn deployable 2337 |
| 2338 | Omnidyne Tech | None | Undefined | accord | 0 | 1 | 1 | 0 | \spawn deployable 2338 |
| 2339 | Armored Dropship | None | Undefined | accord | 0 | 1 | 0.7 | 0 | \spawn deployable 2339 |
| 2340 | Devil's Tusk Dropship | None | Undefined | accord | 0 | 1 | 0.7 | 0 | \spawn deployable 2340 |
| 2341 | NPC interacts with this and despawns itself | None | Undefined | bandit | 0 | 0 | 1 | 0 | \spawn deployable 2341 |
| 2342 | Melding Repulsor | None | Undefined | friendly | 0 | 0 | 1 | 500 | \spawn deployable 2342 |
| 2343 | - | None | Undefined | accord | 0 | 1 | 0.75 | 0 | \spawn deployable 2343 |
| 2344 | - | Power Cell Receptacle | Undefined | accord | 0 | 1 | 1 | 1 | \spawn deployable 2344 |
| 2345 | Chosen Heavy Thumper Turret | None | Fixed Weapon | chosen | 15 | 4500 | 1.5 | 0 | \spawn deployable 2345 |
| 2346 | Flare Container | None | Undefined | accord | 0 | 1 | 1 | 0 | \spawn deployable 2346 |
| 2347 | Plant the Pheromones | None | Undefined | accord | 0 | 1 | 1 | 0 | \spawn deployable 2347 |
| 2348 | _Hybrid Surface Deposit | Surface Deposit | Undefined | friendly | 0.375 | 150 | 0.5 | 0 | \spawn deployable 2348 |
| 2349 | Place Bait | None | Undefined | accord | 0 | 1 | 5 | 0 | \spawn deployable 2349 |
| 2350 | Chosen Pylon | None | Undefined | chosen | 100 | 30000 | 2 | 0 | \spawn deployable 2350 |
| 2351 | Ophanim Supplies | None | Undefined | accord | 0 | 0 | 1 | 0 | \spawn deployable 2351 |
| 2352 | _Arcfold Transponder | Turret | Target (tertiary) | accord | 7.5 | 3900 | 5 | 0 | \spawn deployable 2352 |
| 2353 | _Broken Arcfold Transponder | None | Undefined | accord | 0 | 1 | 5 | 0 | \spawn deployable 2353 |
| 2354 | _Fully Charged Arcfold Transponder | None | Undefined | accord | 0 | 1 | 5 | 0 | \spawn deployable 2354 |
| 2355 | Methylamine Barrel | None | Undefined | accord | 0 | 1 | 1 | 0 | \spawn deployable 2355 |
| 2356 | - | None | Undefined | accord | 0 | 1 | 1 | 0 | \spawn deployable 2356 |
| 2357 | Oil Spill | None | Undefined | accord | 0.75 | 300 | 1 | 0 | \spawn deployable 2357 |
| 2358 | Defence Training Device | None | Undefined | accord | 0 | 1 | 1 | 0 | \spawn deployable 2358 |
| 2359 | Arcfold Containment Array | None | Undefined | accord | 0 | 1 | 0.25 | 0 | \spawn deployable 2359 |
| 2360 | - | Turret | Fixed Weapon | accord | 20 | 10000 | 1.3 | 1000 | \spawn deployable 2360 |
| 2361 | Dredge Arcporter | Arcporter Pylon | Deployed Target | accord | 0 | 1 | 0.15 | 0 | \spawn deployable 2361 |
| 2362 | - | None | Undefined | accord | 0 | 1 | 1 | 3000 | \spawn deployable 2362 |
| 2363 | Human Corpse | Mine | Undefined | accord | 0 | 1 | 1 | 500 | \spawn deployable 2363 |
| 2364 | _Aranha Nest | None | Undefined | gaea | 0 | 150 | 1.5 | 0 | \spawn deployable 2364 |
| 2365 | Sample Holder | None | Undefined | accord | 0 | 1 | 0.325 | 0 | \spawn deployable 2365 |
| 2366 | - | None | Undefined | accord | 1 | 1 | 1 | 0 | \spawn deployable 2366 |
| 2367 | - | None | Undefined | accord | 0 | 1 | 1 | 0 | \spawn deployable 2367 |
| 2368 | Monument Platform | None | Undefined | accord | 0 | 0 | 0.625 | 0 | \spawn deployable 2368 |
| 2369 | Monument Base Emissive | None | Undefined | neutral | 0 | 0 | 0.425 | 0 | \spawn deployable 2369 |
| 2370 | - | None | Undefined | accord | 1 | 1 | 1 | 0 | \spawn deployable 2370 |
| 2371 | Stand | None | Undefined | neutral | 0 | 0 | 1.25 | 0 | \spawn deployable 2371 |
| 2372 | Monument Base | None | Undefined | neutral | 0 | 0 | 0.625 | 0 | \spawn deployable 2372 |
| 2373 | Pedestal Collision | None | Undefined | neutral | 0 | 0 | 1 | 0 | \spawn deployable 2373 |
| 2374 | _War Monument | None | Undefined | accord | 0 | 0 | 0.375 | 0 | \spawn deployable 2374 |
| 2375 | Holo ARES Projector | None | Undefined | neutral | 0 | 0 | 0.5 | 0 | \spawn deployable 2375 |
| 2376 | Holo ARES Projector | None | Undefined | accord | 0 | 1 | 0.5 | 0 | \spawn deployable 2376 |
| 2377 | _Debris | None | Undefined | accord | 0 | 1 | 1 | 0 | \spawn deployable 2377 |
| 2378 | Decayed Surface Deposit | None | Undefined | accord | 0 | 1 | 1 | 0 | \spawn deployable 2378 |
| 2379 | Fancy Torch | None | Undefined | accord | 0 | 0 | 0.75 | 0 | \spawn deployable 2379 |
| 2380 | Tier 1 Monument Base | None | Undefined | accord | 0 | 0 | 0.75 | 0 | \spawn deployable 2380 |
| 2381 | Tier 2 Statue Base | None | Undefined | accord | 0 | 0 | 1 | 0 | \spawn deployable 2381 |
| 2382 | Holo Nostromom Projector | None | Undefined | accord | 0 | 0 | 0.5 | 0 | \spawn deployable 2382 |
| 2383 | Monument Base | None | Undefined | neutral | 0 | 0 | 1 | 0 | \spawn deployable 2383 |
| 2384 | Holocruiser | None | Undefined | accord | 0 | 0 | 1 | 0 | \spawn deployable 2384 |
| 2385 | ARES Torch | None | Undefined | accord | 0 | 0 | 1 | 300 | \spawn deployable 2385 |
| 2386 | Torch | None | Undefined | accord | 0 | 0 | 1 | 300 | \spawn deployable 2386 |
| 2387 | Chosen Turret | None | Fixed Weapon | chosen | 6 | 1800 | 1.35 | 0 | \spawn deployable 2387 |
| 2388 | Holo Earth Projector | None | Undefined | neutral | 0 | 0 | 1 | 0 | \spawn deployable 2388 |
| 2389 | Holo Pack Projector | None | Undefined | neutral | 0 | 0 | 1 | 0 | \spawn deployable 2389 |
| 2390 | Holocruiser | None | Undefined | accord | 0 | 0 | 1 | 0 | \spawn deployable 2390 |
| 2391 | Statue Base | None | Undefined | neutral | 0 | 0 | 1.25 | 0 | \spawn deployable 2391 |
| 2392 | Flight Recorder | None | Undefined | accord | 0 | 0 | 2 | 0 | \spawn deployable 2392 |
| 2393 | Holo Earth Projector | None | Undefined | neutral | 0 | 0 | 0.5 | 0 | \spawn deployable 2393 |
| 2394 | Chosen Tech | None | Undefined | accord | 0 | 1 | 1 | 3000 | \spawn deployable 2394 |
| 2395 | Ash Dragon Meat | None | Undefined | accord | 0 | 1 | 0.8 | 0 | \spawn deployable 2395 |
| 2396 | Scorcher Gland | None | Undefined | accord | 0 | 1 | 0.8 | 0 | \spawn deployable 2396 |
| 2397 | Deep Mineral Thumper | None | Target (primary) | accord | 1 | 1 | 1 | 13000 | \spawn deployable 2397 |
| 2398 | _Arcport Effect | None | Undefined | accord | 0 | 1 | 1 | 0 | \spawn deployable 2398 |
| 2399 | Chewed Up Bracelet | None | Undefined | accord | 20 | 7200 | 4 | 0 | \spawn deployable 2399 |
| 2400 | LGV | None | Undefined | accord | 0 | 1 | 1 | 0 | \spawn deployable 2400 |
| 2401 | Weapons Shipment | None | Undefined | accord | 0 | 0 | 2 | 3000 | \spawn deployable 2401 |
| 2402 | Construction Barricade | None | Undefined | accord | 0 | 1 | 1 | 3000 | \spawn deployable 2402 |
| 2403 | Construction Light | None | Undefined | accord | 0 | 1 | 1 | 3000 | \spawn deployable 2403 |
| 2404 | Construction Light Rectangular | None | Undefined | accord | 0 | 1 | 1 | 3000 | \spawn deployable 2404 |
| 2405 | Construction Crate | None | Undefined | accord | 0 | 1 | 2 | 3000 | \spawn deployable 2405 |
| 2406 | Construction Sign | None | Undefined | accord | 0 | 1 | 1 | 3000 | \spawn deployable 2406 |
| 2407 | - | None | Undefined | accord | 1 | 1 | 1 | 0 | \spawn deployable 2407 |
| 2408 | - | None | Undefined | accord | 1 | 300 | 1 | 0 | \spawn deployable 2408 |
| 2409 | - | None | Undefined | accord | 1 | 1 | 1 | 0 | \spawn deployable 2409 |
| 2411 | - | Mine | Undefined | accord | 0 | 1 | 1 | 500 | \spawn deployable 2411 |
| 2412 | Transit to Dredge | None | Undefined | accord | 0 | 1 | 0.7 | 0 | \spawn deployable 2412 |
| 2413 | - | None | Undefined | accord | 0 | 1 | 0.7 | 0 | \spawn deployable 2413 |
| 2414 | Transit to Sertao | None | Undefined | accord | 0 | 1 | 0.7 | 0 | \spawn deployable 2414 |
| 2415 | _Broken Core Clutter | None | Undefined | friendly | 0 | 1 | 1 | 0 | \spawn deployable 2415 |
| 2416 | _Broken Core | None | Undefined | friendly | 0 | 1 | 0.625 | 0 | \spawn deployable 2416 |
| 2417 | Heavy Rocket Turret | Turret | Fixed Weapon | accord | 2.5 | 1 | 1.3 | 1000 | \spawn deployable 2417 |
| 2418 | - | None | Deployed Target | accord | 25 | 1 | 1 | 0 | \spawn deployable 2418 |
| 2419 | - | None | Target (primary) | accord | 0 | 0 | 2 | 0 | \spawn deployable 2419 |
| 2420 | Armored Dropship | None | Undefined | accord | 0 | 1 | 0.7 | 0 | \spawn deployable 2420 |
| 2421 | Contained Sample | None | Undefined | accord | 0 | 1 | 2.25 | 0 | \spawn deployable 2421 |
| 2422 | _Sample Collection | Generic Terminal | Undefined | gaea | 0 | 150 | 0.25 | 0 | \spawn deployable 2422 |
| 2423 | _Sample Collection | Generic Terminal | Undefined | gaea | 0 | 150 | 0.25 | 0 | \spawn deployable 2423 |
| 2424 | _Aranha Nest | None | Undefined | gaea | 0 | 150 | 1.5 | 0 | \spawn deployable 2424 |
| 2425 | SIN Terminal | None | Target (primary) | accord | 100 | 30000 | 1 | 0 | \spawn deployable 2425 |
| 2426 | - | None | Undefined | chosen | 0 | 1 | 0.03 | 0 | \spawn deployable 2426 |
| 2427 | _Placeholder | None | Undefined | gaea | 0 | 1 | 1 | 0 | \spawn deployable 2427 |
| 2428 | Accord Recon Infiltrator | Mine | Undefined | accord | 0 | 1 | 1 | 500 | \spawn deployable 2428 |
| 2429 | _Snare Point | None | Undefined | gaea | 5 | 500 | 0.5 | 0 | \spawn deployable 2429 |
| 2430 | _Research Collection Point | None | Undefined | accord | 0 | 1 | 1 | 0 | \spawn deployable 2430 |
| 2431 | _Collection Point Base | None | Undefined | accord | 0 | 1 | 1.125 | 0 | \spawn deployable 2431 |
| 2432 | _Missile | None | Undefined | accord | 0 | 1 | 0.5 | 0 | \spawn deployable 2432 |
| 2433 | _Mobile Research Station | None | Target (tertiary) | accord | 0 | 1 | 2.5 | 0 | \spawn deployable 2433 |
| 2434 | _Bioelectric Disruptor Location | None | Undefined | accord | 0 | 150 | 1 | 0 | \spawn deployable 2434 |
| 2435 | _Bioelectric Disruptor | None | Undefined | accord | 0 | 150 | 1 | 0 | \spawn deployable 2435 |
| 2436 | Mobile Mini-Printer | None | Undefined | accord | 0 | 150 | 0.75 | 0 | \spawn deployable 2436 |
| 2437 | _Wall of the Fallen | None | Undefined | accord | 0 | 0 | 0.25 | 0 | \spawn deployable 2437 |
| 2439 | - | None | Undefined | accord | 0 | 1 | 5 | 0 | \spawn deployable 2439 |
| 2440 | Hardpoint Turret | Turret | Fixed Weapon | accord | 125 | 3000 | 2 | 3000 | \spawn deployable 2440 |
| 2442 | - | None | Undefined | accord | 0 | 1 | 1 | 0 | \spawn deployable 2442 |
| 2443 | - | Forge | Undefined | accord | 0 | 1 | 1 | 0 | \spawn deployable 2443 |
| 2444 | - | None | Undefined | accord | 0 | 1 | 1 | 0 | \spawn deployable 2444 |
| 2445 | - | None | Undefined | accord | 0 | 0 | 1 | 0 | \spawn deployable 2445 |
| 2446 | Chosen Tech | None | Undefined | accord | 3.75 | 1 | 0.5 | 0 | \spawn deployable 2446 |
| 2447 | Chosen Data Collector | None | Undefined | accord | 0 | 1 | 1 | 3000 | \spawn deployable 2447 |
| 2448 | Treasure Chest | None | Undefined | accord | 0 | 1 | 1.25 | 0 | \spawn deployable 2448 |
| 2449 | _Missile Cargo | Manufacturing Terminal | Deployed Target | accord | 0 | 0 | 1.25 | 0 | \spawn deployable 2449 |
| 2450 | Generator Control Terminal | None | Undefined | accord | 0 | 0 | 1.5 | 0 | \spawn deployable 2450 |
| 2451 | _Research Relay | None | Undefined | accord | 0 | 0 | 0.5 | 0 | \spawn deployable 2451 |
| 2452 | - | None | Passenger Seat | accord | 0 | 0 | 1 | 0 | \spawn deployable 2452 |
| 2453 | ARES Initiative Monument (AD 2233) | None | Undefined | accord | 0 | 0 | 1 | 0 | \spawn deployable 2453 |
| 2454 | Icarus Glider Pad | Glider pad | Undefined | - | 1 | 1 | 1 | 0 | \spawn deployable 2454 |
| 2455 | _Caged Aranha | None | Undefined | accord | 0 | 1 | 1 | 0 | \spawn deployable 2455 |
| 2457 | _Sample Holder | None | Undefined | accord | 0 | 1 | 0.325 | 0 | \spawn deployable 2457 |
| 2458 | _Chemistry Set | Manufacturing Terminal | Play | accord | 0 | 1 | 0.7 | 0 | \spawn deployable 2458 |
| 2459 | _Crate | None | Undefined | accord | 0 | 1 | 1 | 0 | \spawn deployable 2459 |
| 2460 | Chibi Torch | None | Undefined | accord | 12.5 | 5000 | 1 | 300 | \spawn deployable 2460 |
| 2461 | Metallics Surface Deposit | Surface Deposit | Undefined | friendly | 0.375 | 150 | 1 | 0 | \spawn deployable 2461 |
| 2462 | - | SIN Tower | Target (primary) | accord | 64 | 600 | 1 | 0 | \spawn deployable 2462 |
| 2463 | - | Forge | Undefined | accord | 0 | 1 | 1 | 0 | \spawn deployable 2463 |
| 2464 | (PROTOTYPE) Glider Challenge Target | Turret | Undefined | chosen | 1 | 1 | 1 | 0 | \spawn deployable 2464 |
| 2465 | _Arena | None | Deployed Target | chosen | 0 | 1 | 10 | 0 | \spawn deployable 2465 |
| 2466 | - | None | Undefined | - | 0 | 1 | 1.25 | 0 | \spawn deployable 2466 |
| 2467 | - | None | Undefined | monster | 0 | 1 | 1 | 0 | \spawn deployable 2467 |
| 2469 | - | None | Undefined | accord | 0 | 1 | 1 | 0 | \spawn deployable 2469 |
| 2470 | - | Turret | Fixed Weapon | bandit | 4 | 1600 | 1.3 | 4000 | \spawn deployable 2470 |
| 2471 | - | None | Undefined | accord | 0 | 0 | 1.6 | 0 | \spawn deployable 2471 |
| 2472 | - | None | Undefined | accord | 0 | 0 | 1 | 0 | \spawn deployable 2472 |
| 2473 | - | None | Undefined | accord | 0 | 1 | 0.7 | 0 | \spawn deployable 2473 |
| 2474 | - | Arcporter | Undefined | accord | 0 | 1 | 1 | 0 | \spawn deployable 2474 |
| 2475 | - | Surface Deposit | Undefined | friendly | 0.375 | 150 | 4 | 0 | \spawn deployable 2475 |
| 2476 | Explosives | Mine | Undefined | accord | 0 | 1 | 6 | 500 | \spawn deployable 2476 |
| 2477 | - | None | Undefined | accord | 0.0025 | 1 | 1 | 0 | \spawn deployable 2477 |
| 2478 | - | None | Undefined | accord | 0 | 1 | 1 | 0 | \spawn deployable 2478 |
| 2479 | _Mobile Antenna | None | Undefined | accord | 0 | 1 | 2 | 0 | \spawn deployable 2479 |
| 2480 | - | None | Undefined | accord | 0 | 0 | 1 | 0 | \spawn deployable 2480 |
| 2481 | _Missile Cargo | Manufacturing Terminal | Deployed Target | accord | 0 | 780 | 1.25 | 0 | \spawn deployable 2481 |
| 2482 | - | None | Undefined | chosen | 0.0025 | 1 | 1 | 0 | \spawn deployable 2482 |
| 2483 | _Missile Cargo LZ Marker | Manufacturing Terminal | Deployed Target | accord | 0 | 1 | 1 | 0 | \spawn deployable 2483 |
| 2484 | _Previous | None | Undefined | accord | 0 | 1 | 1 | 0 | \spawn deployable 2484 |
| 2485 | _Next | None | Undefined | accord | 0 | 1 | 1 | 0 | \spawn deployable 2485 |
| 2486 | - | None | Undefined | accord | 0 | 1 | 2 | 0 | \spawn deployable 2486 |
| 2487 | - | None | Undefined | accord | 0 | 1 | 1 | 0 | \spawn deployable 2487 |
| 2488 | Santa's Sleigh | None | Undefined | neutral | 0 | 1000 | 1 | 0 | \spawn deployable 2488 |
| 2489 | Food Crate | None | Undefined | accord | 0 | 1 | 2 | 0 | \spawn deployable 2489 |
| 2490 | - | None | Undefined | accord | 0 | 1 | 1 | 0 | \spawn deployable 2490 |
| 2491 | Rocket Sleigh | None | Undefined | neutral | 0 | 1 | 1 | 0 | \spawn deployable 2491 |
| 2492 | Sky Discovery Launch Pad | Glider pad | Undefined | accord | 0.75 | 1 | 1 | 0 | \spawn deployable 2492 |
| 2493 | - | None | Undefined | accord | 0 | 1 | 1 | 1000 | \spawn deployable 2493 |
| 2494 | - | None | Target (tertiary) | accord | 17 | 6630 | 1 | 0 | \spawn deployable 2494 |
| 2495 | - | None | Undefined | accord | 0 | 0 | 1 | 0 | \spawn deployable 2495 |
| 2496 | - | None | Interactable Objective | accord | 0 | 1 | 1 | 0 | \spawn deployable 2496 |
| 2497 | - | None | Undefined | accord | 0 | 1 | 1 | 0 | \spawn deployable 2497 |
| 2498 | - | None | Undefined | accord | 0 | 1 | 1 | 0 | \spawn deployable 2498 |
| 2499 | Kisuton Race Terminal | FinishLine | Undefined | accord | 0 | 0 | 1 | 0 | \spawn deployable 2499 |
| 2500 | Copy of All Purpose Invisible Point | None | Undefined | neutral | 0 | 0 | 1 | 0 | \spawn deployable 2500 |
| 2501 | - | None | Interactable Objective | gaea | 15 | 5850 | 1 | 0 | \spawn deployable 2501 |
| 2502 | - | None | Undefined | accord | 15 | 5850 | 1 | 0 | \spawn deployable 2502 |
| 2503 | - | None | Undefined | accord | 15 | 5850 | 1 | 0 | \spawn deployable 2503 |
| 2504 | - | None | Target (tertiary) | accord | 0 | 6630 | 1 | 0 | \spawn deployable 2504 |
| 2505 | Crashed MGV | None | Undefined | accord | 17 | 6630 | 1 | 0 | \spawn deployable 2505 |
| 2506 | - | None | Undefined | accord | 0 | 1 | 1 | 0 | \spawn deployable 2506 |
| 2507 | _Rock | None | Undefined | accord | 0 | 1 | 1 | 0 | \spawn deployable 2507 |
| 2508 | - | None | Undefined | accord | 0 | 1 | 1 | 0 | \spawn deployable 2508 |
| 2509 | - | None | Undefined | neutral | 7 | 1 | 0.7 | 0 | \spawn deployable 2509 |
| 2510 | - | None | Undefined | neutral | 10 | 1 | 0.3 | 0 | \spawn deployable 2510 |
| 2511 | - | None | Undefined | neutral | 6 | 1 | 0.5 | 0 | \spawn deployable 2511 |
| 2512 | Drilling Laser | Turret | Undefined | accord | 257 | 10000 | 4 | 4000 | \spawn deployable 2512 |
| 2513 | - | None | Undefined | melding | 5 | 1 | 1 | 0 | \spawn deployable 2513 |
| 2514 | - | None | Undefined | melding | 2.5 | 975 | 1 | 0 | \spawn deployable 2514 |
| 2515 | - | None | Undefined | melding | 1 | 1 | 0.75 | 0 | \spawn deployable 2515 |
| 2516 | Glowing Orb | None | Undefined | melding | 0 | 1 | 0.5 | 0 | \spawn deployable 2516 |
| 2517 | - | Datapads | Target (primary) | accord | 10 | 1 | 1 | 3500 | \spawn deployable 2517 |
| 2518 | - | Datapads | Target (primary) | accord | 0 | 1 | 1 | 1000 | \spawn deployable 2518 |
| 2519 | - | Datapads | Target (primary) | accord | 0 | 1 | 1 | 1000 | \spawn deployable 2519 |
| 2520 | - | None | Undefined | accord | 0 | 1 | 1 | 0 | \spawn deployable 2520 |
| 2521 | _Ghost | None | Undefined | - | 0 | 1 | 1 | 0 | \spawn deployable 2521 |
| 2522 | _Ghost | None | Undefined | accord | 0 | 1 | 1 | 0 | \spawn deployable 2522 |
| 2523 | _Mist | None | Undefined | accord | 0 | 1 | 1 | 0 | \spawn deployable 2523 |
| 2524 | _Pumpkin | None | Undefined | accord | 0 | 1 | 1 | 0 | \spawn deployable 2524 |
| 2525 | Glider Challenge Registrar (Coral Forest - Easy) | None | Undefined | accord | 0 | 1 | 1 | 0 | \spawn deployable 2525 |
| 2526 | Glider Challenge Registrar (Coral Forest - Medium) | None | Undefined | accord | 0 | 1 | 1 | 0 | \spawn deployable 2526 |
| 2527 | Glider Challenge Registrar (Coral Forest - Hard) | None | Undefined | accord | 0 | 1 | 1 | 0 | \spawn deployable 2527 |
| 2528 | Glider Challenge Registrar (Sertao - Easy) | None | Undefined | accord | 0 | 1 | 1 | 0 | \spawn deployable 2528 |
| 2529 | Glider Challenge Registrar (Sertao - Medium) | Forge | Undefined | accord | 0 | 1 | 1 | 0 | \spawn deployable 2529 |
| 2530 | Glider Challenge Registrar (Sertao - Hard) | Forge | Undefined | accord | 0 | 1 | 1 | 0 | \spawn deployable 2530 |
| 2531 | Glider Challenge Registrar (Devil's Tusk - Easy) | Forge | Undefined | accord | 0 | 1 | 1 | 0 | \spawn deployable 2531 |
| 2532 | Glider Challenge Registrar (Devil's Tusk - Medium) | Forge | Undefined | accord | 0 | 1 | 1 | 0 | \spawn deployable 2532 |
| 2533 | Glider Challenge Registrar (Devil's Tusk - Hard) | Forge | Undefined | accord | 0 | 1 | 1 | 0 | \spawn deployable 2533 |
| 2534 | Detonation Pack | None | Undefined | accord | 0 | 150 | 0.75 | 0 | \spawn deployable 2534 |
| 2535 | _Tombstone | None | Undefined | - | 0 | 1 | 1 | 0 | \spawn deployable 2535 |
| 2536 | - | None | Undefined | accord | 0 | 1 | 1 | 0 | \spawn deployable 2536 |
| 2537 | Scorcher Nest | None | Undefined | gaea | 1 | 150 | 1.75 | 0 | \spawn deployable 2537 |
| 2539 | - | None | Undefined | accord | 0 | 1 | 1 | 0 | \spawn deployable 2539 |
| 2540 | Detonator Placement | None | Undefined | accord | 0 | 150 | 1 | 0 | \spawn deployable 2540 |
| 2541 | ZAP Proximity Scanner | Tech SIN | Deployed Target | accord | 125 | 750 | 1 | 10000 | \spawn deployable 2541 |
| 2542 | Counter-Scanner | None | Undefined | accord | 0 | 90000 | 0.75 | 0 | \spawn deployable 2542 |
| 2543 | Battleframe Recorder | None | Undefined | accord | 0 | 0 | 0.5 | 0 | \spawn deployable 2543 |
| 2544 | Detonation Pack Storage Crate | None | Undefined | accord | 0 | 150 | 0.75 | 0 | \spawn deployable 2544 |
| 2545 | LGV Rental Terminal | Arcporter | Undefined | accord | 0 | 1 | 1 | 0 | \spawn deployable 2545 |
| 2546 | Neutron Reassembler | None | Undefined | accord | 0 | 0 | 1 | 0 | \spawn deployable 2546 |
| 2547 | - | None | Undefined | accord | 0 | 1 | 0.75 | 0 | \spawn deployable 2547 |
| 2548 | Operation_000 Blast Door | None | Undefined | accord | 0 | 1 | 2.5 | 0 | \spawn deployable 2548 |
| 2549 | Chosen AA Turret | Mannable Turret | Fixed Weapon | chosen | 160 | 63000 | 1 | 0 | \spawn deployable 2549 |
| 2550 | Ophanim Crate | None | Undefined | accord | 0 | 1 | 1 | 0 | \spawn deployable 2550 |
| 2551 | Chosen Targeting Pylon | None | Undefined | accord | 0 | 30000 | 0.75 | 0 | \spawn deployable 2551 |
| 2552 | Data Relay | None | Undefined | accord | 0 | 9999999 | 2 | 500 | \spawn deployable 2552 |
| 2553 | Accord Storage Crate | None | Target (primary) | accord | 100 | 30000 | 0.5 | 0 | \spawn deployable 2553 |
| 2554 | Infected Tissue Sample | None | Undefined | accord | 20 | 7200 | 1 | 0 | \spawn deployable 2554 |
| 2555 | - | None | Target (secondary) | accord | 125 | 50000 | 1 | 40666 | \spawn deployable 2555 |
| 2556 | Antigenic Tissue Sample | None | Undefined | accord | 20 | 7200 | 1 | 0 | \spawn deployable 2556 |
| 2557 | Unstable Upheaval | None | Undefined | gaea | 10 | 5000 | 0.85 | 1 | \spawn deployable 2557 |
| 2558 | Operation Turret | Mannable Turret | Fixed Weapon | accord | 0 | 6000 | 1 | 0 | \spawn deployable 2558 |
| 2559 | Datapad Terminal | None | Undefined | accord | 0 | 50 | 1 | 0 | \spawn deployable 2559 |
| 2560 | Copy of Cache of Chosen Tech | None | Undefined | accord | 0 | 1 | 1 | 500 | \spawn deployable 2560 |
| 2561 | Repulsor Cradle | None | Undefined | accord | 0 | 1 | 7 | 0 | \spawn deployable 2561 |
| 2562 | Strifebringer | Turret | Deployed Target | chosen | 16 | 3500 | 1 | 0 | \spawn deployable 2562 |
| 2563 | - | None | Undefined | chosen | 0 | 1 | 5 | 0 | \spawn deployable 2563 |
| 2564 | - | None | Undefined | accord | 0 | 1 | 1 | 0 | \spawn deployable 2564 |
| 2565 | Shield Generator | Turret | Play | accord | 0 | 0 | 1 | 0 | \spawn deployable 2565 |
| 2566 | - | Turret | Target (tertiary) | monster | 0 | 1 | 1 | 10000 | \spawn deployable 2566 |
| 2567 | - | None | Interactable Objective | accord | 0 | 0 | 1 | 0 | \spawn deployable 2567 |
| 2568 | Chosen Shield Generator | None | Target (primary) | chosen | 25 | 6000 | 1 | 0 | \spawn deployable 2568 |
| 2569 | Kestral Meat | None | Undefined | accord | 20 | 7200 | 1 | 0 | \spawn deployable 2569 |
| 2571 | _Ghost | None | Undefined | - | 0 | 1 | 1 | 0 | \spawn deployable 2571 |
| 2572 | _Ghost | None | Undefined | - | 0 | 1 | 1 | 0 | \spawn deployable 2572 |
| 2573 | _Ghost | None | Undefined | - | 0 | 1 | 1 | 0 | \spawn deployable 2573 |
| 2574 | Chosen Laser Drill | None | Undefined | chosen | 40 | 50000 | 2.5 | 0 | \spawn deployable 2574 |
| 2575 | Resonator Shield | None | Undefined | chosen | 12.5 | 4500 | 1.275 | 0 | \spawn deployable 2575 |
| 2576 | - | None | Undefined | accord | 0 | 1 | 1 | 0 | \spawn deployable 2576 |
| 2577 | Ash Dragon Egg | None | Undefined | gaea | 1 | 300 | 3 | 0 | \spawn deployable 2577 |
| 2578 | Chosen Totem | None | Undefined | chosen | 1 | 1 | 0.8 | 3000 | \spawn deployable 2578 |
| 2579 | - | Mannable Turret | Fixed Weapon | accord | 0 | 0 | 1 | 0 | \spawn deployable 2579 |
| 2580 | - | None | Undefined | accord | 0 | 150 | 1 | 0 | \spawn deployable 2580 |
| 2581 | - | None | Target (tertiary) | gaea | 5 | 1 | 0.5 | 2000 | \spawn deployable 2581 |
| 2582 | - | None | Undefined | - | 1 | 1 | 1 | 0 | \spawn deployable 2582 |
| 2583 | Copy of Fallen Accord Patrol Officer | Mine | Undefined | accord | 0 | 9999999 | 1 | 500 | \spawn deployable 2583 |
| 2584 | - | Mine | Undefined | accord | 0 | 9999999 | 1 | 500 | \spawn deployable 2584 |
| 2585 | Kisuton LGV Terminal | None | Undefined | accord | 0 | 0 | 0.5 | 0 | \spawn deployable 2585 |
| 2586 | Race Decorations | None | Undefined | accord | 0 | 1 | 1 | 0 | \spawn deployable 2586 |
| 2587 | Tech Shipment | None | Target (primary) | accord | 8 | 3120 | 1.5 | 0 | \spawn deployable 2587 |
| 2588 | _Aranhas Worker Lair + Spawns | None | Undefined | gaea | 25 | 10000 | 1.25 | 3000 | \spawn deployable 2588 |
| 2589 | Laser Drill | Anti-Personnel Turret | Fixed Weapon | accord | 40 | 4000 | 2 | 2500 | \spawn deployable 2589 |
| 2590 | Coolant Sprayer | None | Undefined | accord | 0 | 1 | 0.5 | 0 | \spawn deployable 2590 |
| 2591 | Damaged Laser Drill | Anti-Personnel Turret | Undefined | accord | 2000 | 2000 | 2 | 2500 | \spawn deployable 2591 |
| 2592 | NPE ARES 2 - Dead body search | None | Interactable Objective | neutral | 0 | 1 | 1 | 0 | \spawn deployable 2592 |
| 2593 | Kisuton LGV Terminal | None | Undefined | accord | 0 | 0 | 0.5 | 0 | \spawn deployable 2593 |
| 2594 | - | None | Undefined | neutral | 0 | 1 | 1.3 | 0 | \spawn deployable 2594 |
| 2595 | Kisuton LGV Terminal | None | Undefined | accord | 0 | 0 | 0.5 | 0 | \spawn deployable 2595 |
| 2596 | - | Turret | Target (tertiary) | monster | 0 | 1 | 1 | 0 | \spawn deployable 2596 |
| 2597 | Light Bridge Centerpiece | Shield | Undefined | friendly | 6.25 | 3500 | 1.3 | 0 | \spawn deployable 2597 |
| 2598 | - | None | Undefined | accord | 0 | 1 | 1 | 0 | \spawn deployable 2598 |
| 2599 | - | None | Undefined | accord | 0 | 1 | 1 | 0 | \spawn deployable 2599 |
| 2600 | Light Bridge End Piece | Shield | Undefined | friendly | 6.25 | 3500 | 1 | 0 | \spawn deployable 2600 |
| 2601 | Copy of Light Bridge Mid Piece | Shield | Undefined | friendly | 1 | 3500 | 1 | 0 | \spawn deployable 2601 |
| 2602 | - | None | Undefined | accord | 0 | 1 | 1 | 0 | \spawn deployable 2602 |
| 2603 | Mini Melding Repulsor | None | Target (primary) | accord | 32 | 8000 | 0.35 | 0 | \spawn deployable 2603 |
| 2604 | - | None | Undefined | accord | 0 | 0 | 1.5 | 0 | \spawn deployable 2604 |
| 2605 | Chosen Life Siphon | Repair Station | Target (secondary) | chosen | 100 | 200000 | 1 | 500 | \spawn deployable 2605 |
| 2608 | AA Turret | Mannable Turret | Fixed Weapon | accord | 15 | 6000 | 1 | 0 | \spawn deployable 2608 |
| 2610 | - | None | Undefined | accord | 0 | 1 | 1.3 | 0 | \spawn deployable 2610 |
| 2611 | Melding Spawner | None | Deployed Target | chosen | 40 | 8000 | 1 | 250 | \spawn deployable 2611 |
| 2612 | Brontodon Carcass | None | Interactable Objective | accord | 1000 | 390000 | 1 | 0 | \spawn deployable 2612 |
| 2613 | Copy of | None | Undefined | accord | 0 | 1 | 1 | 0 | \spawn deployable 2613 |
| 2614 | Tech Shipment | None | Target (primary) | accord | 35 | 7800 | 1.5 | 0 | \spawn deployable 2614 |
| 2615 | Turret Control Terminal | None | Undefined | accord | 0 | 1 | 1 | 0 | \spawn deployable 2615 |
| 2616 | Chosen Bifold Mortar | Mannable Turret | Fixed Weapon | chosen | 10 | 3500 | 1 | 1000 | \spawn deployable 2616 |
| 2617 | - | Consumer Fireworks | Undefined | accord | 0 | 1 | 1 | 0 | \spawn deployable 2617 |
| 2618 | Melding Repulsor Terminal | None | Undefined | accord | 0 | 0 | 1 | 0 | \spawn deployable 2618 |
| 2619 | Chosen Mortar | None | Fixed Weapon | chosen | 0 | 1 | 0.125 | 3000 | \spawn deployable 2619 |
| 2620 | - | Datapads | Undefined | accord | 0 | 0 | 1 | 0 | \spawn deployable 2620 |
| 2621 | Melding Repulsor | None | Target (primary) | accord | 200 | 1000000 | 1.5 | 500 | \spawn deployable 2621 |
| 2622 | - | Datapads | Undefined | accord | 0 | 0 | 1 | 0 | \spawn deployable 2622 |
| 2623 | - | Datapads | Undefined | accord | 0 | 0 | 1 | 0 | \spawn deployable 2623 |
| 2624 | Supply Depot Terminal | None | Interactable Objective | accord | 0 | 0 | 1 | 0 | \spawn deployable 2624 |
| 2625 | Supply Depot Terminal | None | Interactable Objective | accord | 0 | 0 | 1 | 0 | \spawn deployable 2625 |
| 2626 | Supply Depot Terminal | None | Interactable Objective | accord | 0 | 0 | 1 | 0 | \spawn deployable 2626 |
| 2627 | Brontodon Chunk | None | Deployed Target | accord | 12 | 4800 | 1 | 0 | \spawn deployable 2627 |
| 2628 | _Operation_000 Small Jump Pad | None | Undefined | accord | 0 | 0 | 1 | 0 | \spawn deployable 2628 |
| 2629 | King Brontodon Upheaval Warning | None | Undefined | accord | 10 | 5000 | 1 | 0 | \spawn deployable 2629 |
| 2630 | Aranha Food | None | Interactable Objective | gaea | 0 | 0 | 1 | 0 | \spawn deployable 2630 |
| 2631 | _Harvester Control Point | None | Undefined | accord | 0 | 1 | 1 | 0 | \spawn deployable 2631 |
| 2632 | Door Control Terminal | None | Undefined | accord | 0 | 50 | 1 | 0 | \spawn deployable 2632 |
| 2633 | _Operation_000 Medium Jump Pad | None | Undefined | accord | 0 | 0 | 1 | 0 | \spawn deployable 2633 |
| 2634 | Real Ground. | None | Undefined | accord | 0 | 1 | 1.3 | 0 | \spawn deployable 2634 |
| 2635 | _Operation_000 Large Jump Pad | None | Undefined | accord | 0 | 0 | 1 | 0 | \spawn deployable 2635 |
| 2636 | Blow Up to Proceed | None | Undefined | accord | 2 | 800 | 0.75 | 0 | \spawn deployable 2636 |
| 2637 | _OPERATION_000 Rare Supply Crate A | None | Undefined | accord | 0 | 1 | 3 | 0 | \spawn deployable 2637 |
| 2638 | _OPERATION_000 Rare Supply Crate B | None | Undefined | accord | 0 | 1 | 3 | 0 | \spawn deployable 2638 |
| 2639 | _OPERATION_000 Rare Supply Crate C | None | Undefined | accord | 0 | 1 | 3 | 0 | \spawn deployable 2639 |
| 2640 | Override Systems | None | Undefined | neutral | 0 | 0 | 1 | 0 | \spawn deployable 2640 |
| 2641 | LGV Terminal | None | Undefined | accord | 0 | 0 | 1 | 0 | \spawn deployable 2641 |
| 2642 | Harvester Security Core | None | Target (primary) | - | 10 | 3900 | 1.5 | 0 | \spawn deployable 2642 |
| 2643 | Harvester Security Core | None | Target (primary) | - | 20 | 7800 | 1.5 | 0 | \spawn deployable 2643 |
| 2644 | _Harvester Base | None | Undefined | - | 0 | 0 | 1 | 0 | \spawn deployable 2644 |
| 2645 | _Harvester Base | None | Undefined | - | 0 | 0 | 1 | 0 | \spawn deployable 2645 |
| 2646 | Harvester Security Terminal | None | Interactable Objective | accord | 0 | 0 | 1 | 0 | \spawn deployable 2646 |
| 2647 | Harvester Security Terminal | None | Interactable Objective | accord | 0 | 0 | 1 | 0 | \spawn deployable 2647 |
| 2648 | Harvester Security Terminal | None | Interactable Objective | accord | 0 | 0 | 1 | 0 | \spawn deployable 2648 |
| 2649 | Chosen Mortar Target | None | Undefined | - | 0 | 1 | 1 | 1000 | \spawn deployable 2649 |
| 2650 | - | None | Deployed Target | accord | 257 | 100230 | 0.7 | 0 | \spawn deployable 2650 |
| 2651 | - | None | Undefined | gaea | 0 | 0 | 1 | 500 | \spawn deployable 2651 |
| 2652 | Supply Chest | None | Undefined | - | 0 | 0 | 1.5 | 0 | \spawn deployable 2652 |
| 2653 | Magnetically Sealed Supply Vault | None | Undefined | - | 0 | 0 | 2 | 0 | \spawn deployable 2653 |
| 2654 | - | None | Undefined | chosen | 0 | 1 | 1 | 0 | \spawn deployable 2654 |
| 2655 | - | None | Undefined | accord | 0 | 0 | 1 | 5000 | \spawn deployable 2655 |
| 2656 | Melded Outbreak | None | Play | chosen | 0 | 1 | 1 | 0 | \spawn deployable 2656 |
| 2657 | Copy of  Chosen Sentinel - Accord Tent | None | Undefined | accord | 0 | 1 | 1 | 0 | \spawn deployable 2657 |
| 2658 | - | None | Undefined | accord | 0 | 1 | 0.869 | 0 | \spawn deployable 2658 |
| 2659 | - | None | Undefined | - | 1 | 1 | 1 | 0 | \spawn deployable 2659 |
| 2660 | - | None | Undefined | - | 1 | 1 | 1 | 0 | \spawn deployable 2660 |
| 2661 | Crate of Repulsor Parts | None | Target (primary) | - | 30 | 11700 | 1 | 0 | \spawn deployable 2661 |
| 2662 | Crate of Repulsor Parts | None | Target (primary) | - | 30 | 11700 | 1 | 0 | \spawn deployable 2662 |
| 2663 | Stolen Accord Crate | None | Undefined | accord | 0 | 1 | 1 | 1000 | \spawn deployable 2663 |
| 2664 | _Repulsor Base | None | Undefined | - | 0 | 1 | 1 | 0 | \spawn deployable 2664 |
| 2665 | _Repulsor Arm | None | Undefined | - | 0 | 1 | 1 | 0 | \spawn deployable 2665 |
| 2666 | Melding Repulsor | None | Undefined | - | 0 | 1 | 1 | 0 | \spawn deployable 2666 |
| 2667 | Rare Supply Crate | None | Undefined | accord | 0 | 1 | 3 | 0 | \spawn deployable 2667 |
| 2668 | Supply Crate | None | Undefined | accord | 0 | 1 | 1 | 0 | \spawn deployable 2668 |
| 2669 | Micro Energy Shield | Shield | Deployed Target | accord | 5 | 1 | 0.5 | 0 | \spawn deployable 2669 |
| 2670 | - | None | Undefined | accord | 0 | 1 | 0.869 | 0 | \spawn deployable 2670 |
| 2671 | - | None | Undefined | - | 1 | 1 | 1 | 0 | \spawn deployable 2671 |
| 2672 | - | None | Undefined | - | 1 | 1 | 1 | 0 | \spawn deployable 2672 |
| 2673 | - | None | Undefined | accord | 0 | 1 | 0.869 | 0 | \spawn deployable 2673 |
| 2674 | - | None | Undefined | accord | 0 | 1 | 0.869 | 0 | \spawn deployable 2674 |
| 2675 | Damaged Drilling Laser | Turret | Undefined | accord | 257 | 10000 | 4 | 4000 | \spawn deployable 2675 |
| 2676 | Melding Repulsor Terminal | None | Interactable Objective | accord | 0 | 0 | 1 | 0 | \spawn deployable 2676 |
| 2677 | Repulsor Tether | None | Undefined | accord | 0 | 1 | 1 | 0 | \spawn deployable 2677 |
| 2678 | - | None | Undefined | - | 1 | 1 | 1 | 0 | \spawn deployable 2678 |
| 2679 | - | None | Target (secondary) | accord | 7.5 | 3000 | 1 | 0 | \spawn deployable 2679 |
| 2680 | NPE ARES 2 - Chosen Terminal Interaction point | None | Interactable Objective | neutral | 0 | 0 | 1 | 0 | \spawn deployable 2680 |
| 2681 | NPC - Chosen Dread - TeleDome Deployable | None | Deployed Shield | chosen | 5 | 1 | 1 | 0 | \spawn deployable 2681 |
| 2682 | - | None | Undefined | chosen | 0 | 1 | 1 | 0 | \spawn deployable 2682 |
| 2683 | - | None | Undefined | - | 0 | 1 | 1.25 | 0 | \spawn deployable 2683 |
| 2684 | Manual Door Lock | None | Undefined | accord | 40 | 16000 | 1.5 | 0 | \spawn deployable 2684 |
| 2685 | - | None | Undefined | accord | 0 | 1 | 4 | 0 | \spawn deployable 2685 |
| 2686 | Tanken Barricade | None | Undefined | bandit | 20 | 2000 | 1.75 | 0 | \spawn deployable 2686 |
| 2687 | Blast Door | None | Undefined | accord | 0 | 1 | 2.5 | 0 | \spawn deployable 2687 |
| 2688 | Copy of Blast Door | None | Undefined | accord | 0 | 1 | 2.5 | 0 | \spawn deployable 2688 |
| 2689 | Energy Orb | Tech SIN | Target (primary) | accord | 35 | 10000 | 0.75 | 0 | \spawn deployable 2689 |
| 2690 | Melding Repulsor Terminal | None | Interactable Objective | accord | 0 | 0 | 1 | 0 | \spawn deployable 2690 |
| 2691 | - | None | Deployed Target | accord | 7.5 | 3000 | 1 | 3000 | \spawn deployable 2691 |
| 2692 | - | None | Undefined | - | 0 | 1 | 1 | 0 | \spawn deployable 2692 |
| 2693 | - | Arcporter Pylon | Fixed Weapon | accord | 0 | 0 | 0.8 | 0 | \spawn deployable 2693 |
| 2694 | - | None | Undefined | accord | 0 | 1 | 1 | 0 | \spawn deployable 2694 |
| 2695 | _Melding Arena | Arcporter | Undefined | melding | 0 | 0 | 1 | 0 | \spawn deployable 2695 |
| 2696 | - | Turret | Fixed Weapon | accord | 8.5 | 3315 | 1.3 | 4000 | \spawn deployable 2696 |
| 2697 | - | Turret | Fixed Weapon | accord | 8.5 | 3315 | 1.3 | 4000 | \spawn deployable 2697 |
| 2698 | - | Turret | Fixed Weapon | accord | 8.5 | 3315 | 1.3 | 4000 | \spawn deployable 2698 |
| 2699 | _Blast Door | None | Undefined | accord | 0 | 1 | 1.15 | 0 | \spawn deployable 2699 |
| 2700 | - | Anti-Personnel Turret | Fixed Weapon | accord | 2000 | 2000 | 0.75 | 1000 | \spawn deployable 2700 |
| 2701 | - | Repair Station | Undefined | chosen | 10 | 200000 | 1 | 500 | \spawn deployable 2701 |
| 2702 | - | Spawner | Target (tertiary) | chosen | 40 | 10800 | 1 | 15000 | \spawn deployable 2702 |
| 2703 | - | Mannable Turret | Fixed Weapon | accord | 15 | 6000 | 1 | 0 | \spawn deployable 2703 |
| 2704 | Copy of Operation_000 Blast Door | None | Undefined | accord | 0 | 1 | 2 | 0 | \spawn deployable 2704 |
| 2705 | - | None | Undefined | accord | 0 | 1 | 1 | 0 | \spawn deployable 2705 |
| 2713 | Remote Spawn Point | None | Undefined | - | 50 | 19500 | 1 | 0 | \spawn deployable 2713 |
| 2714 | - | None | Undefined | - | 50 | 19500 | 1 | 0 | \spawn deployable 2714 |
| 2715 | Ol' Man Bill's Stash | None | Undefined | accord | 0 | 1 | 1.25 | 0 | \spawn deployable 2715 |
| 2716 | - | None | Fixed Weapon | chosen | 15 | 4500 | 1.5 | 0 | \spawn deployable 2716 |
| 2717 | Glider Pad | Glider pad | Undefined | accord | 0.75 | 1 | 1 | 0 | \spawn deployable 2717 |
| 2718 | Hacked Chosen Turret | None | Fixed Weapon | accord | 9 | 3510 | 1 | 0 | \spawn deployable 2718 |
| 2719 | Chosen Turret | None | Fixed Weapon | chosen | 9 | 3510 | 1 | 0 | \spawn deployable 2719 |
| 2720 | - | None | Undefined | accord | 2 | 1 | 1 | 2000 | \spawn deployable 2720 |
| 2721 | _OPERATION_000 Rare Supply Crate D | None | Undefined | accord | 0 | 1 | 3 | 0 | \spawn deployable 2721 |
| 2722 | Invis Deployable Fizzler | None | Undefined | - | 0 | 1 | 1 | 1000 | \spawn deployable 2722 |
| 2723 | - | None | Undefined | accord | 0 | 1 | 1 | 0 | \spawn deployable 2723 |
| 2724 | Outgoing Cargo | None | Undefined | friendly | 0 | 1 | 2 | 0 | \spawn deployable 2724 |
| 2725 | - | None | Undefined | - | 1 | 1 | 1 | 0 | \spawn deployable 2725 |
| 2726 | Glider Challenge Map Terminal | None | Undefined | accord | 0 | 0 | 0.5 | 0 | \spawn deployable 2726 |
| 2727 | Chosen Dropship | Spawner | Deployed Target | chosen | 90 | 35100 | 1 | 250 | \spawn deployable 2727 |
| 2728 | Glider Terminal Decoration | None | Undefined | accord | 0 | 1 | 1 | 0 | \spawn deployable 2728 |
| 2729 | - | Mannable Turret | Fixed Weapon | chosen | 15 | 5850 | 1 | 2500 | \spawn deployable 2729 |
| 2730 | Invis Deployable that can be used to hold particle effects and stuff | None | Undefined | - | 0 | 1 | 1 | 1000 | \spawn deployable 2730 |
| 2731 | Chosen Bomb | Turret | Play | accord | 0 | 1 | 1 | 0 | \spawn deployable 2731 |
| 2732 | Laser Turret | Anti-Personnel Turret | Fixed Weapon | accord | 10 | 4000 | 2 | 2500 | \spawn deployable 2732 |
| 2733 | - | None | Undefined | accord | 0 | 1 | 1 | 0 | \spawn deployable 2733 |
| 2734 | Beam Turret | Anti-Personnel Turret | Fixed Weapon | accord | 10 | 4000 | 2 | 2500 | \spawn deployable 2734 |
| 2735 | Bandit Turret | Tech Turret | Fixed Weapon | bandit | 3 | 3000 | 1.5 | 5000 | \spawn deployable 2735 |
| 2736 | LGV Terminal | None | Undefined | accord | 0 | 0 | 1 | 0 | \spawn deployable 2736 |
| 2737 | [DDSWARM] Impact Dummy Object | None | Undefined | gaea | 0 | 1 | 1 | 0 | \spawn deployable 2737 |
| 2738 | Copy of NPE ARES 2 - Dead body search | None | Interactable Objective | neutral | 0 | 1 | 1 | 0 | \spawn deployable 2738 |
| 2739 | Copy of NPE ARES 2 - Dead body search | None | Interactable Objective | neutral | 0 | 1 | 1 | 0 | \spawn deployable 2739 |
| 2740 | Copy of NPE ARES 2 - Dead body search | None | Interactable Objective | neutral | 0 | 1 | 1 | 0 | \spawn deployable 2740 |
| 2741 | Copy of NPE ARES 2 - Dead body search | None | Interactable Objective | neutral | 0 | 1 | 1 | 0 | \spawn deployable 2741 |
| 2742 | Start Resynchronization | None | Undefined | neutral | 0 | 0 | 1 | 0 | \spawn deployable 2742 |
| 2743 | Smuggler Arcporter | Turret | Interactable Objective | bandit | 0 | 1 | 1 | 0 | \spawn deployable 2743 |
| 2744 | Weapons Crate | None | Interactable Objective | bandit | 0 | 1 | 1 | 5000 | \spawn deployable 2744 |
| 2745 | - | None | Interactable Objective | accord | 0 | 1 | 1 | 0 | \spawn deployable 2745 |
| 2746 | Rubble Piece 2 | None | Undefined | - | 1 | 1 | 1 | 0 | \spawn deployable 2746 |
| 2747 | _Solar Panel | None | Undefined | - | 0 | 0 | 1 | 0 | \spawn deployable 2747 |
| 2748 | Rubble Piece 3 | None | Undefined | - | 1 | 1 | 1 | 0 | \spawn deployable 2748 |
| 2749 | Rubble Piece 4 | None | Undefined | - | 1 | 1 | 1 | 0 | \spawn deployable 2749 |
| 2750 | Rubble Piece 5 | None | Undefined | - | 1 | 1 | 1 | 0 | \spawn deployable 2750 |
| 2753 | Whack an Elf Terminal | None | Undefined | accord | 0 | 1 | 1 | 0 | \spawn deployable 2753 |
| 2754 | Chosen Dropship | None | Deployed Target | chosen | 90 | 35100 | 1 | 250 | \spawn deployable 2754 |
| 2755 | Generator Control Terminal | None | Undefined | chosen | 0 | 0 | 1.5 | 0 | \spawn deployable 2755 |
| 2756 | - | None | Undefined | chosen | 0 | 0 | 1 | 0 | \spawn deployable 2756 |
| 2757 | - | None | Undefined | accord | 0 | 1 | 1 | 0 | \spawn deployable 2757 |
| 2758 | - | None | Undefined | accord | 0 | 1 | 1 | 0 | \spawn deployable 2758 |
| 2759 | - | None | Undefined | accord | 0 | 1 | 1 | 0 | \spawn deployable 2759 |
| 2760 | - | None | Undefined | accord | 0 | 1 | 1 | 0 | \spawn deployable 2760 |
| 2761 | - | None | Undefined | accord | 0 | 1 | 1 | 0 | \spawn deployable 2761 |
| 2762 | - | None | Undefined | accord | 0 | 1 | 1 | 0 | \spawn deployable 2762 |
| 2763 | - | None | Undefined | accord | 0 | 1 | 1 | 0 | \spawn deployable 2763 |
| 2764 | Disarmed Chosen Bomb | Turret | Play | accord | 0 | 1 | 1 | 0 | \spawn deployable 2764 |
| 2765 | - | Spawner | Deployed Target | chosen | 30 | 6000 | 1 | 250 | \spawn deployable 2765 |
| 2766 | - | Spawner | Deployed Target | chosen | 30 | 6000 | 1 | 250 | \spawn deployable 2766 |
| 2767 | - | None | Interactable Objective | - | 1 | 1 | 1 | 0 | \spawn deployable 2767 |
| 2768 | - | None | Undefined | accord | 0 | 1 | 1 | 0 | \spawn deployable 2768 |
| 2769 | - | None | Undefined | accord | 0 | 1 | 1 | 0 | \spawn deployable 2769 |
| 2770 | - | None | Undefined | accord | 0 | 1 | 1 | 0 | \spawn deployable 2770 |
| 2771 | - | None | Undefined | accord | 0 | 1 | 1 | 0 | \spawn deployable 2771 |
| 2772 | - | None | Undefined | accord | 0 | 1 | 1 | 0 | \spawn deployable 2772 |
| 2773 | - | None | Undefined | accord | 0 | 1 | 1 | 0 | \spawn deployable 2773 |
| 2774 | - | None | Undefined | accord | 0 | 1 | 1 | 0 | \spawn deployable 2774 |
| 2775 | - | None | Undefined | accord | 0 | 1 | 1 | 0 | \spawn deployable 2775 |
| 2776 | - | None | Undefined | accord | 0 | 1 | 1 | 0 | \spawn deployable 2776 |
| 2777 | - | None | Undefined | friendly | 0 | 0 | 0.5 | 0 | \spawn deployable 2777 |
| 2778 | Reaper Barricade | None | Undefined | bandit | 20 | 2000 | 1.75 | 0 | \spawn deployable 2778 |
| 2779 | Reaper Barricade | None | Undefined | bandit | 20 | 2000 | 1.75 | 0 | \spawn deployable 2779 |
| 2780 | Tanken Barricade | None | Undefined | bandit | 20 | 2000 | 1.75 | 0 | \spawn deployable 2780 |
| 2781 | Ophanim Barricade | None | Undefined | bandit | 20 | 2000 | 0.5 | 0 | \spawn deployable 2781 |
| 2782 | Ophanim Barricade | None | Undefined | bandit | 20 | 2000 | 0.5 | 0 | \spawn deployable 2782 |
| 2783 | - | None | Target (primary) | accord | 15 | 20000 | 0.7 | 0 | \spawn deployable 2783 |
| 2784 | - | None | Interactable Objective | neutral | 1 | 1 | 0.7 | 0 | \spawn deployable 2784 |
| 2785 | Melding Repulsor | None | Undefined | friendly | 20 | 20000 | 1 | 1000 | \spawn deployable 2785 |
| 2786 | - | None | Interactable Objective | neutral | 1 | 1 | 0.7 | 0 | \spawn deployable 2786 |
| 2787 | - | Spawner | Deployed Target | chosen | 62.5 | 25000 | 1 | 250 | \spawn deployable 2787 |
| 2788 | - | None | Undefined | gaea | 7.5 | 3000 | 1 | 0 | \spawn deployable 2788 |
| 2789 | - | None | Interactable Objective | neutral | 1 | 1 | 0.7 | 0 | \spawn deployable 2789 |
| 2790 | - | None | Interactable Objective | neutral | 1 | 1 | 1 | 0 | \spawn deployable 2790 |
| 2791 | - | None | Interactable Objective | neutral | 1 | 1 | 0.7 | 0 | \spawn deployable 2791 |
| 2792 | - | None | Interactable Objective | neutral | 1 | 1 | 1 | 0 | \spawn deployable 2792 |
| 2793 | Supply Canister | None | Undefined | chosen | 0.65 | 500 | 2 | 0 | \spawn deployable 2793 |
| 2794 | - | None | Target (primary) | - | 20 | 7800 | 3 | 0 | \spawn deployable 2794 |
| 2795 | Accord Mega Crate | None | Undefined | accord | 0 | 1 | 0.7 | 3000 | \spawn deployable 2795 |
| 2796 | Crate Control Panel | None | Undefined | accord | 0 | 1 | 1 | 0 | \spawn deployable 2796 |
| 2797 | Heavy Turret | None | Fixed Weapon | chosen | 0 | 0 | 1 | 2000 | \spawn deployable 2797 |
| 2798 | OPERATION_000 Rare Supply Crate C - Hardmode | None | Undefined | accord | 0 | 1 | 3 | 0 | \spawn deployable 2798 |
| 2799 | OPERATION_000 Rare Supply Crate D - Hardmode | None | Undefined | accord | 0 | 1 | 3 | 0 | \spawn deployable 2799 |
| 2800 | Overturned Trucks | None | Undefined | friendly | 1 | 1 | 1 | 0 | \spawn deployable 2800 |
| 2801 | Accord Dropship | Spawner | Deployed Target | accord | 30 | 6000 | 1 | 250 | \spawn deployable 2801 |
| 2802 | Chosen Energy Core | None | Interactable Objective | chosen | 0 | 0 | 3 | 0 | \spawn deployable 2802 |
| 2803 | - | None | Undefined | - | 0 | 1 | 1 | 0 | \spawn deployable 2803 |
| 2804 | - | None | Undefined | - | 0 | 1 | 1 | 0 | \spawn deployable 2804 |
| 2805 | - | None | Undefined | - | 0 | 1 | 1 | 0 | \spawn deployable 2805 |
| 2806 | - | None | Undefined | - | 0 | 1 | 1 | 0 | \spawn deployable 2806 |
| 2807 | - | None | Undefined | - | 0 | 1 | 1 | 0 | \spawn deployable 2807 |
| 2808 | Accord Dropship | Spawner | Deployed Target | accord | 30 | 6000 | 1 | 250 | \spawn deployable 2808 |
| 2809 | - | None | Play | accord | 0 | 1 | 1 | 0 | \spawn deployable 2809 |
| 2810 | _Fake Orb | None | Undefined | accord | 10 | 3900 | 5 | 0 | \spawn deployable 2810 |
| 2811 | - | None | Target (primary) | accord | 1 | 1 | 1 | 0 | \spawn deployable 2811 |
| 2812 | Chosen Generator | None | Target (primary) | chosen | 20 | 7800 | 2 | 0 | \spawn deployable 2812 |
| 2813 | Chosen Generator | None | Target (primary) | chosen | 25 | 6000 | 1 | 0 | \spawn deployable 2813 |
| 2814 | Adjudication Wall | Shield | Deployed Target | accord | 2.5 | 1000 | 0.55 | 0 | \spawn deployable 2814 |
| 2815 | - | None | Deployed Target | neutral | 0 | 0 | 1 | 250 | \spawn deployable 2815 |
| 2816 | Blast Door | None | Undefined | accord | 0 | 1 | 5.5 | 0 | \spawn deployable 2816 |
| 2817 | Blast Door | None | Undefined | accord | 0 | 1 | 0.677 | 0 | \spawn deployable 2817 |
| 2818 | - | None | Line Rest 1 | accord | 0 | 0 | 1 | 0 | \spawn deployable 2818 |
| 2819 | - | None | Line Rest 1 | accord | 0 | 0 | 1 | 0 | \spawn deployable 2819 |
| 2820 | Encounter Reset Switch | None | Interactable Objective | accord | 0 | 50 | 1 | 0 | \spawn deployable 2820 |
| 2821 | Chosen Barricade | None | Undefined | neutral | 0 | 1 | 1.5 | 0 | \spawn deployable 2821 |
| 2822 | Chosen Supply Canister | None | Undefined | chosen | 1 | 500 | 1 | 0 | \spawn deployable 2822 |
| 2823 | - | None | Undefined | - | 1 | 1 | 1 | 0 | \spawn deployable 2823 |
| 2824 | - | None | Undefined | accord | 0.25 | 1 | 0.4 | 0 | \spawn deployable 2824 |
| 2825 | - | None | Undefined | melding | 0 | 7200 | 1 | 0 | \spawn deployable 2825 |
| 2826 | - | None | Target (primary) | accord | 100 | 30000 | 1 | 0 | \spawn deployable 2826 |
| 2827 | - | None | Undefined | accord | 0 | 1 | 1 | 500 | \spawn deployable 2827 |
| 2828 | Chosen Turret | None | Fixed Weapon | chosen | 115 | 44850 | 1 | 0 | \spawn deployable 2828 |
| 2829 | - | None | Interactable Objective | - | 1 | 1 | 1 | 0 | \spawn deployable 2829 |
| 2830 | Accord Dropship Wreckage | None | Interactable Objective | - | 1 | 1 | 1 | 0 | \spawn deployable 2830 |
| 2831 | - | None | Interactable Objective | - | 1 | 1 | 1 | 0 | \spawn deployable 2831 |
| 2832 | - | None | Interactable Objective | - | 1 | 1 | 1 | 0 | \spawn deployable 2832 |
| 2833 | - | None | Interactable Objective | - | 1 | 1 | 1 | 0 | \spawn deployable 2833 |
| 2834 | _Chosen Container | None | Undefined | accord | 0 | 1 | 1 | 500 | \spawn deployable 2834 |
| 2835 | Auto-Turret Hardpoint Stage 2 | None | Undefined | accord | 0 | 1 | 1 | 0 | \spawn deployable 2835 |
| 2836 | - | Turret | Target (secondary) | accord | 20 | 7200 | 1 | 0 | \spawn deployable 2836 |
| 2837 | - | None | Undefined | accord | 0 | 1 | 1 | 0 | \spawn deployable 2837 |
| 2838 | - | None | Deployed Target | chosen | 3 | 1 | 1 | 0 | \spawn deployable 2838 |
| 2839 | - | None | Interactable Objective | - | 1 | 1 | 1 | 0 | \spawn deployable 2839 |
| 2840 | - | Spawner | Deployed Target | chosen | 25 | 3500 | 1 | 10500 | \spawn deployable 2840 |
| 2841 | Command Center Terminal | None | Undefined | accord | 0 | 1 | 1.5 | 0 | \spawn deployable 2841 |
| 2842 | Auto-Turret Hardpoint Stage 1 | None | Undefined | accord | 0 | 1 | 1 | 0 | \spawn deployable 2842 |
| 2843 | Auto-Turret Hardpoint Stage 3 | None | Undefined | accord | 0 | 1 | 1 | 0 | \spawn deployable 2843 |
| 2844 | Multi-Schema Auto-Turret Stage 4 | None | Undefined | accord | 0 | 1 | 1 | 0 | \spawn deployable 2844 |
| 2845 | - | None | Undefined | accord | 0 | 1 | 1 | 0 | \spawn deployable 2845 |
| 2846 | Flamethrower Turret | None | Fixed Weapon | accord | 11 | 4000 | 1.5 | 5000 | \spawn deployable 2846 |
| 2847 | Accord Munitions | None | Undefined | accord | 0 | 1 | 2 | 0 | \spawn deployable 2847 |
| 2848 | Accord Munitions | None | Undefined | accord | 0 | 1 | 2 | 0 | \spawn deployable 2848 |
| 2849 | - | Turret | Fixed Weapon | accord | 12.5 | 5000 | 1.5 | 5000 | \spawn deployable 2849 |
| 2850 | Sniper Turret | None | Fixed Weapon | accord | 10 | 4000 | 1.5 | 5000 | \spawn deployable 2850 |
| 2851 | Accord Flight Recorder | None | Undefined | accord | 0 | 1 | 1 | 0 | \spawn deployable 2851 |
| 2852 | - | None | Undefined | accord | 0 | 1 | 1 | 0 | \spawn deployable 2852 |
| 2853 | Cryo Grenade Turret | None | Fixed Weapon | accord | 10 | 3000 | 1.5 | 5000 | \spawn deployable 2853 |
| 2854 | - | Turret | Fixed Weapon | accord | 10 | 4500 | 1.3 | 4000 | \spawn deployable 2854 |
| 2855 | - | Turret | Fixed Weapon | accord | 10 | 4000 | 1.5 | 5000 | \spawn deployable 2855 |
| 2856 | - | None | Interactable Objective | - | 50 | 1 | 1 | 0 | \spawn deployable 2856 |
| 2857 | _Registration Point | None | Undefined | accord | 0 | 1 | 1 | 0 | \spawn deployable 2857 |
| 2859 | _Underground Hatch | None | Undefined | - | 1 | 1 | 1 | 0 | \spawn deployable 2859 |
| 2860 | Mysterious Golden Plate | None | Undefined | accord | 0 | 1 | 1 | 0 | \spawn deployable 2860 |
| 2861 | - | None | Target (primary) | - | 25 | 9750 | 1 | 0 | \spawn deployable 2861 |
| 2862 | - | None | Undefined | - | 0 | 0 | 2 | 0 | \spawn deployable 2862 |
| 2863 | - | None | Undefined | accord | 0 | 1 | 1 | 2000 | \spawn deployable 2863 |
| 2864 | - | None | Undefined | - | 0 | 0 | 1 | 0 | \spawn deployable 2864 |
| 2865 | - | None | Undefined | - | 0 | 0 | 1 | 0 | \spawn deployable 2865 |
| 2866 | - | None | Undefined | accord | 0 | 1 | 1 | 500 | \spawn deployable 2866 |
| 2867 | Leroy | None | Undefined | accord | 0 | 1 | 1 | 0 | \spawn deployable 2867 |
| 2868 | - | Surface Deposit | Undefined | friendly | 0.375 | 150 | 4 | 0 | \spawn deployable 2868 |
| 2869 | - | Surface Deposit | Undefined | friendly | 0.375 | 150 | 4 | 0 | \spawn deployable 2869 |
| 2870 | _Marks an area | None | Undefined | friendly | 0 | 100 | 1 | 0 | \spawn deployable 2870 |
| 2871 | _Community Snowball Fight Arena | None | Undefined | accord | 0 | 1 | 1 | 0 | \spawn deployable 2871 |
| 2872 | _Snowball Pickup | None | Undefined | accord | 0 | 1 | 1 | 0 | \spawn deployable 2872 |
| 2873 | _Wintertide Tree w/auto-spawned presents | None | Undefined | friendly | 0 | 7200 | 1 | 0 | \spawn deployable 2873 |
| 2874 | _Present | None | Undefined | friendly | 0 | 30000 | 1 | 0 | \spawn deployable 2874 |
| 2875 | - | None | Undefined | - | 0 | 0 | 1 | 0 | \spawn deployable 2875 |
| 2876 | _Present | None | Undefined | friendly | 0 | 30000 | 2 | 0 | \spawn deployable 2876 |
| 2877 | _Present | None | Undefined | friendly | 0 | 30000 | 3 | 0 | \spawn deployable 2877 |
| 2878 | Snowdrift | None | Undefined | - | 0 | 1 | 0.8 | 0 | \spawn deployable 2878 |
| 2879 | Snowdrift | None | Undefined | - | 0 | 1 | 0.8 | 0 | \spawn deployable 2879 |
| 2880 | Snowman | None | Undefined | - | 0 | 1 | 0.8 | 0 | \spawn deployable 2880 |
| 2881 | Snowy Tree | None | Undefined | - | 0 | 1 | 0.7 | 0 | \spawn deployable 2881 |
| 2882 | Snowy Tree | None | Undefined | - | 0 | 1 | 0.7 | 0 | \spawn deployable 2882 |
| 2883 | - | Turret | Fixed Weapon | chosen | 50 | 19500 | 3.5 | 0 | \spawn deployable 2883 |
| 2884 | - | None | Undefined | accord | 0 | 1 | 0.05 | 0 | \spawn deployable 2884 |
| 2885 | Skiver Lair | None | Undefined | gaea | 20 | 8000 | 3 | 3000 | \spawn deployable 2885 |
| 2886 | _Registration Point | None | Undefined | accord | 0 | 1 | 1 | 0 | \spawn deployable 2886 |
| 2887 | _Inflatable Snowman | None | Undefined | accord | 0 | 1 | 1 | 0 | \spawn deployable 2887 |
| 2888 | - | None | Deployed Target | accord | 5 | 1 | 1 | 0 | \spawn deployable 2888 |
| 2890 | - | None | Undefined | - | 0 | 0 | 1 | 0 | \spawn deployable 2890 |
| 2891 | - | None | Undefined | - | 0 | 0 | 1 | 0 | \spawn deployable 2891 |
| 2892 | - | None | Undefined | - | 0 | 0 | 1 | 0 | \spawn deployable 2892 |
| 2893 | - | None | Undefined | - | 0 | 0 | 1 | 0 | \spawn deployable 2893 |
| 2894 | - | None | Undefined | - | 0 | 0 | 1 | 0 | \spawn deployable 2894 |
| 2895 | - | None | Undefined | - | 0 | 0 | 0.5 | 0 | \spawn deployable 2895 |
| 2896 | - | None | Undefined | chosen | 0 | 1 | 1 | 0 | \spawn deployable 2896 |
| 2897 | - | None | Undefined | - | 0 | 0 | 1 | 0 | \spawn deployable 2897 |
| 2898 | - | None | Target (primary) | accord | 20 | 7800 | 1 | 3500 | \spawn deployable 2898 |
| 2899 | - | None | Target (primary) | accord | 20 | 4000 | 1 | 0 | \spawn deployable 2899 |
| 2900 | Melding Core | None | Undefined | accord | 10 | 5000 | 1 | 0 | \spawn deployable 2900 |
| 2901 | Snow Globe for Vendor | None | Undefined | accord | 0 | 1 | 0.25 | 0 | \spawn deployable 2901 |
| 2902 | Accord Cargo Container | None | Undefined | accord | 0 | 0 | 3 | 0 | \spawn deployable 2902 |
| 2903 | - | None | Undefined | chosen | 0 | 1 | 1 | 0 | \spawn deployable 2903 |
| 2904 | Wintertide Tree Firework | Consumer Fireworks | Undefined | accord | 0 | 1 | 1 | 0 | \spawn deployable 2904 |
| 2905 | Wintertide Snowman Firework | Consumer Fireworks | Undefined | accord | 0 | 1 | 1 | 0 | \spawn deployable 2905 |
| 2906 | _Inflatable Snowman | None | Undefined | accord | 0 | 1 | 1 | 0 | \spawn deployable 2906 |
| 2907 | Inflatable Snowman | None | Undefined | accord | 0 | 1 | 1 | 0 | \spawn deployable 2907 |
| 2908 | _Warcom cover prop | None | Undefined | neutral | 0 | 1 | 0.25 | 0 | \spawn deployable 2908 |
| 2909 | Present | None | Undefined | friendly | 0.1 | 150 | 2 | 0 | \spawn deployable 2909 |
| 2910 | Wintertide Tree | None | Undefined | accord | 0 | 1 | 1 | 0 | \spawn deployable 2910 |
| 2911 | _Wintertide Log | None | Undefined | accord | 0 | 1 | 0.125 | 0 | \spawn deployable 2911 |
| 2912 | _Warcom cover prop | None | Undefined | neutral | 0 | 1 | 0.25 | 0 | \spawn deployable 2912 |
| 2913 | - | None | Undefined | friendly | 0 | 30000 | 3 | 0 | \spawn deployable 2913 |
| 2914 | A Small Wintertide Tree | None | Undefined | friendly | 0 | 7200 | 0.25 | 0 | \spawn deployable 2914 |
| 2915 | Small Inflatable Snowman | None | Undefined | accord | 0 | 1 | 0.5 | 0 | \spawn deployable 2915 |
| 2916 | Arcfold Beacon - Charging | None | Undefined | accord | 0 | 1 | 0.6 | 0 | \spawn deployable 2916 |
| 2917 | Father Wintertide's Icy Cream Social | None | Undefined | - | 1 | 1 | 0.5 | 0 | \spawn deployable 2917 |
| 2918 | - | None | Undefined | neutral | 0 | 0 | 1 | 0 | \spawn deployable 2918 |
| 2919 | - | Turret | Fixed Weapon | accord | 18 | 6900 | 1.3 | 4000 | \spawn deployable 2919 |
| 2920 | - | Turret | Fixed Weapon | accord | 18 | 3315 | 1.3 | 4000 | \spawn deployable 2920 |
| 2921 | - | Turret | Fixed Weapon | accord | 18 | 3315 | 1.3 | 4000 | \spawn deployable 2921 |
| 2922 | - | None | Deployed Target | accord | 15 | 3000 | 1 | 3000 | \spawn deployable 2922 |
| 2923 | - | None | Undefined | - | 1 | 1 | 1 | 0 | \spawn deployable 2923 |
| 2924 | - | None | Undefined | - | 1 | 1 | 1 | 0 | \spawn deployable 2924 |
| 2925 | - | None | Undefined | - | 1 | 1 | 1 | 0 | \spawn deployable 2925 |
| 2926 | - | None | Undefined | - | 1 | 1 | 1 | 0 | \spawn deployable 2926 |
| 2927 | _Warcom cover prop | None | Undefined | accord | 0 | 1 | 0.125 | 0 | \spawn deployable 2927 |
| 2928 | - | None | Undefined | accord | 0 | 1 | 1 | 0 | \spawn deployable 2928 |
| 2929 | _Fireworks - Abu Dhabi | None | Undefined | accord | 0 | 1 | 1 | 0 | \spawn deployable 2929 |
| 2930 | _Fireworks - Auckland | None | Undefined | accord | 0 | 1 | 1 | 0 | \spawn deployable 2930 |
| 2931 | _Fireworks - Bangkok | None | Undefined | accord | 0 | 1 | 1 | 0 | \spawn deployable 2931 |
| 2932 | _Fireworks - Beijing | None | Undefined | accord | 0 | 1 | 1 | 0 | \spawn deployable 2932 |
| 2933 | _Fireworks - Berlin | None | Undefined | accord | 0 | 1 | 1 | 0 | \spawn deployable 2933 |
| 2934 | _Fireworks - Cape Verde Islands | None | Undefined | accord | 0 | 1 | 1 | 0 | \spawn deployable 2934 |
| 2935 | _Fireworks - Caracas | None | Undefined | accord | 0 | 1 | 1 | 0 | \spawn deployable 2935 |
| 2936 | _Fireworks - Chicago | None | Undefined | accord | 0 | 1 | 1 | 0 | \spawn deployable 2936 |
| 2937 | _Fireworks - Dhaka | None | Undefined | accord | 0 | 1 | 1 | 0 | \spawn deployable 2937 |
| 2938 | _Fireworks - Dublin | None | Undefined | accord | 0 | 1 | 1 | 0 | \spawn deployable 2938 |
| 2939 | _Fireworks - Fortaleza | None | Undefined | accord | 0 | 1 | 1 | 0 | \spawn deployable 2939 |
| 2940 | _Fireworks - Glasgow | None | Undefined | accord | 0 | 1 | 1 | 0 | \spawn deployable 2940 |
| 2941 | _Fireworks - Honolulu | None | Undefined | accord | 0 | 1 | 1 | 0 | \spawn deployable 2941 |
| 2942 | _Fireworks - Islamabad | None | Undefined | accord | 0 | 1 | 1 | 0 | \spawn deployable 2942 |
| 2943 | _Fireworks - Jerusalem | None | Undefined | accord | 0 | 1 | 1 | 0 | \spawn deployable 2943 |
| 2944 | _Fireworks - Juneau | None | Undefined | accord | 0 | 1 | 1 | 0 | \spawn deployable 2944 |
| 2945 | _Fireworks - Krakow | None | Undefined | accord | 0 | 1 | 1 | 0 | \spawn deployable 2945 |
| 2946 | _Fireworks - London | None | Undefined | accord | 0 | 1 | 1 | 0 | \spawn deployable 2946 |
| 2947 | _Fireworks - Los Angeles | None | Undefined | accord | 0 | 1 | 1 | 0 | \spawn deployable 2947 |
| 2948 | _Fireworks - Mexico City | None | Undefined | accord | 0 | 1 | 1 | 0 | \spawn deployable 2948 |
| 2949 | _Fireworks - Moscow | None | Undefined | accord | 0 | 1 | 1 | 0 | \spawn deployable 2949 |
| 2950 | _Fireworks - New Delhi | None | Undefined | accord | 0 | 1 | 1 | 0 | \spawn deployable 2950 |
| 2951 | _Fireworks - New York | None | Undefined | accord | 0 | 1 | 1 | 0 | \spawn deployable 2951 |
| 2952 | _Fireworks - Paris | None | Undefined | accord | 0 | 1 | 1 | 0 | \spawn deployable 2952 |
| 2953 | - | None | Undefined | accord | 0 | 1 | 1 | 0 | \spawn deployable 2953 |
| 2954 | - | None | Undefined | accord | 0 | 1 | 1 | 0 | \spawn deployable 2954 |
| 2955 | - | None | Undefined | accord | 0 | 1 | 1 | 0 | \spawn deployable 2955 |
| 2956 | - | None | Undefined | accord | 0 | 1 | 1 | 0 | \spawn deployable 2956 |
| 2957 | - | None | Undefined | accord | 0 | 1 | 1 | 0 | \spawn deployable 2957 |
| 2958 | _Happy New Years Prague | None | Undefined | accord | 0 | 1 | 1 | 0 | \spawn deployable 2958 |
| 2959 | _Happy New Years Red 5 Studios | None | Undefined | accord | 0 | 1 | 1 | 0 | \spawn deployable 2959 |
| 2960 | _Happy New Years Rio de Janeiro | None | Undefined | accord | 0 | 1 | 1 | 0 | \spawn deployable 2960 |
| 2961 | _Happy New Years Samoa | None | Undefined | accord | 0 | 1 | 1 | 0 | \spawn deployable 2961 |
| 2962 | _Happy New Years Seoul | None | Undefined | accord | 0 | 1 | 1 | 0 | \spawn deployable 2962 |
| 2963 | _Happy New Years Shanghai | None | Undefined | accord | 0 | 1 | 1 | 0 | \spawn deployable 2963 |
| 2964 | _Happy New Years Solomon Islands | None | Undefined | accord | 0 | 1 | 1 | 0 | \spawn deployable 2964 |
| 2965 | _Happy New Years Sydney | None | Undefined | accord | 0 | 1 | 1 | 0 | \spawn deployable 2965 |
| 2966 | _Happy New Years Tokyo | None | Undefined | accord | 0 | 1 | 1 | 0 | \spawn deployable 2966 |
| 2967 | _Happy New Years Washington DC | None | Undefined | accord | 0 | 1 | 1 | 0 | \spawn deployable 2967 |
| 2968 | _Happy New Years Wellington | None | Undefined | accord | 0 | 1 | 1 | 0 | \spawn deployable 2968 |
| 2969 | - | None | Undefined | - | 150 | 19500 | 1 | 0 | \spawn deployable 2969 |
| 2970 | Poacher Sonic Detonator | None | Undefined | accord | 0 | 1 | 1 | 0 | \spawn deployable 2970 |
| 2971 | - | None | Undefined | neutral | 0 | 1 | 1 | 0 | \spawn deployable 2971 |
| 2972 | Father Wintertide's Merry Munition Drop | None | Undefined | - | 1 | 1 | 1 | 0 | \spawn deployable 2972 |
| 2973 | - | Turret | Fixed Weapon | accord | 0 | 0 | 1.3 | 0 | \spawn deployable 2973 |
| 2974 | - | Turret | Fixed Weapon | accord | 0 | 0 | 1.3 | 0 | \spawn deployable 2974 |
| 2975 | - | Turret | Fixed Weapon | accord | 0 | 0 | 1.3 | 0 | \spawn deployable 2975 |
| 2976 | - | None | Undefined | - | 0 | 1 | 0.72 | 0 | \spawn deployable 2976 |
| 2977 | Accord Dropship | Spawner | Undefined | accord | 30 | 6000 | 1 | 250 | \spawn deployable 2977 |
| 2978 | Dropship Beacon | None | Undefined | accord | 0 | 1 | 1 | 0 | \spawn deployable 2978 |
| 2979 | - | None | Deployed Target | chosen | 3 | 1 | 1 | 0 | \spawn deployable 2979 |
| 2980 | - | None | Undefined | gaea | 0 | 1 | 1 | 0 | \spawn deployable 2980 |
| 2981 | - | None | Undefined | gaea | 5 | 1950 | 1 | 0 | \spawn deployable 2981 |
| 2982 | Dropship Beacon | None | Undefined | accord | 0 | 1 | 1 | 0 | \spawn deployable 2982 |
| 2983 | - | None | Undefined | accord | 0 | 1 | 1 | 0 | \spawn deployable 2983 |
| 2984 | - | None | Undefined | accord | 0 | 1 | 1 | 0 | \spawn deployable 2984 |
| 2985 | - | None | Undefined | accord | 0 | 1 | 1 | 0 | \spawn deployable 2985 |
| 2986 | - | None | Undefined | accord | 25 | 1 | 1 | 0 | \spawn deployable 2986 |
| 2987 | - | Turret | Fixed Weapon | accord | 7.5 | 3000 | 1.5 | 5000 | \spawn deployable 2987 |
| 2988 | - | None | Undefined | accord | 5 | 300 | 1 | 5000 | \spawn deployable 2988 |
| 2989 | Chosen Chest Piece | None | Undefined | accord | 1 | 1 | 1 | 0 | \spawn deployable 2989 |
| 2990 | Reaper Anti-Personnel Turret | Anti-Personnel Turret | Fixed Weapon | Reapers | 55 | 20000 | 2 | 2500 | \spawn deployable 2990 |
| 2991 | - | None | Undefined | - | 0 | 1 | 1 | 0 | \spawn deployable 2991 |
| 2992 | - | None | Undefined | - | 0 | 1 | 1 | 0 | \spawn deployable 2992 |
| 2993 | Breaching Charge Visualizer | None | Undefined | accord | 0 | 150 | 1 | 0 | \spawn deployable 2993 |
| 2994 | Breaching Charge | None | Undefined | accord | 0 | 150 | 1 | 0 | \spawn deployable 2994 |
| 2995 | - | Adventurer's Glider Pad | Undefined | accord | 0.75 | 1 | 1 | 0 | \spawn deployable 2995 |
| 2996 | - | None | Undefined | accord | 0 | 1 | 1 | 0 | \spawn deployable 2996 |
| 2997 | Accord Cargo Ship | Spawner | Target (secondary) | accord | 30 | 6000 | 1 | 0 | \spawn deployable 2997 |
| 2998 | Accord Cargo Container | Spawner | Undefined | accord | 0 | 0 | 1 | 5000 | \spawn deployable 2998 |
| 2999 | Glider Pad | Glider pad | Undefined | accord | 0 | 0 | 1 | 0 | \spawn deployable 2999 |
| 3000 | - | None | Undefined | chosen | 0 | 1 | 5 | 0 | \spawn deployable 3000 |
| 3001 | - | None | Undefined | accord | 0 | 0 | 1 | 0 | \spawn deployable 3001 |
| 3002 | _ | Turret | Fixed Weapon | accord | 2 | 800 | 1 | 1000 | \spawn deployable 3002 |
| 3003 | - | None | Undefined | chosen | 0 | 1 | 6.97383 | 0 | \spawn deployable 3003 |
| 3004 | - | None | Undefined | chosen | 0 | 1 | 4.17619 | 0 | \spawn deployable 3004 |
| 3005 | Call In Accord Thumper | None | Undefined | accord | 60 | 24000 | 1 | 100 | \spawn deployable 3005 |
| 3006 | - | None | Target (tertiary) | chosen | 45 | 17550 | 1 | 4000 | \spawn deployable 3006 |
| 3007 | Chosen Strifebringer | None | Play | accord | 0 | 1 | 1 | 0 | \spawn deployable 3007 |
| 3008 | Crimson Storm Dropship | Spawner | Undefined | accord | 30 | 6000 | 1 | 250 | \spawn deployable 3008 |
| 3009 | Dropship Beacon | None | Undefined | accord | 0 | 1 | 1 | 0 | \spawn deployable 3009 |
| 3010 | Dropship Beacon | None | Undefined | accord | 0 | 1 | 1 | 0 | \spawn deployable 3010 |
| 3011 | - | Turret | Fixed Weapon | chosen | 6 | 2400 | 1.35 | 0 | \spawn deployable 3011 |
| 3012 | _Molecular Printer | None | Undefined | accord | 0 | 0 | 0.4 | 0 | \spawn deployable 3012 |
| 3013 | _Molecular Printer | None | Undefined | accord | 0 | 0 | 0.4 | 0 | \spawn deployable 3013 |
| 3014 | Shutter Door | None | Undefined | - | 0 | 1 | 3.5 | 0 | \spawn deployable 3014 |
| 3015 | - | None | Fixed Weapon | chosen | 15 | 4500 | 1.5 | 0 | \spawn deployable 3015 |
| 3016 | - | None | Undefined | accord | 0 | 1 | 1 | 0 | \spawn deployable 3016 |
| 3017 | Danger Sign | None | Undefined | accord | 0 | 1 | 1 | 1000 | \spawn deployable 3017 |
| 3018 | - | Turret | Fixed Weapon | accord | 6 | 2400 | 1.35 | 10000 | \spawn deployable 3018 |
| 3019 | Bathsheba placeholder | None | Market | accord | 0 | 0 | 1 | 0 | \spawn deployable 3019 |
| 3020 | - | None | Undefined | - | 0 | 1 | 0.2 | 0 | \spawn deployable 3020 |
| 3021 | - | None | Undefined | - | 0 | 1 | 1 | 0 | \spawn deployable 3021 |
| 3022 | - | None | Undefined | - | 0 | 1 | 1 | 0 | \spawn deployable 3022 |
| 3023 | - | None | Undefined | - | 0 | 1 | 1 | 0 | \spawn deployable 3023 |
| 3024 | - | None | Undefined | - | 0 | 1 | 0.7 | 0 | \spawn deployable 3024 |
| 3025 | - | None | Market | accord | 0 | 0 | 1 | 0 | \spawn deployable 3025 |
| 3026 | - | None | Undefined | - | 0 | 1 | 1.5 | 0 | \spawn deployable 3026 |
| 3027 | - | None | Undefined | - | 0 | 1 | 0.4 | 0 | \spawn deployable 3027 |
| 3028 | Experimental Battleframe Station | Loadout Station | Undefined | accord | 0 | 0 | 1 | 0 | \spawn deployable 3028 |
| 3029 | - | None | Undefined | accord | 0 | 1 | 1 | 0 | \spawn deployable 3029 |
| 3030 | _ | None | Target (primary) | accord | 0 | 1 | 1.45 | 0 | \spawn deployable 3030 |
| 3031 | - | None | Undefined | accord | 0 | 0 | 1 | 0 | \spawn deployable 3031 |
| 3032 | Electrical Generator - Powered | None | Undefined | neutral | 5 | 2000 | 1 | 2500 | \spawn deployable 3032 |
| 3033 | _Barricade | None | Undefined | bandit | 0 | 1 | 0.5 | 0 | \spawn deployable 3033 |
| 3034 | _Barricade | None | Undefined | bandit | 0 | 1 | 1 | 0 | \spawn deployable 3034 |
| 3035 | Electrical Generator - Unpowered | None | Undefined | accord | 0 | 2000 | 1 | 2500 | \spawn deployable 3035 |
| 3036 | Electrical Generator - Powered | None | Undefined | Reapers | 5 | 4000 | 1 | 2500 | \spawn deployable 3036 |
| 3037 | Ophanim Totem | None | Target (tertiary) | bandit | 4 | 50 | 1 | 500 | \spawn deployable 3037 |
| 3038 | - | Tech SIN | Target (primary) | accord | 3000 | 1170000 | 2 | 5000 | \spawn deployable 3038 |
| 3039 | Accord Dropship | Spawner | Deployed Target | chosen | 30 | 6000 | 1 | 250 | \spawn deployable 3039 |
| 3040 | Accord Dropship | Spawner | Deployed Target | accord | 30 | 6000 | 1 | 250 | \spawn deployable 3040 |
| 3041 | - | Mannable Turret | Fixed Weapon | chosen | 10 | 3500 | 1 | 1000 | \spawn deployable 3041 |
| 3042 | - | None | Undefined | accord | 0 | 1 | 1 | 0 | \spawn deployable 3042 |
| 3043 | Rescue Kara | None | Undefined | accord | 0 | 1 | 1 | 0 | \spawn deployable 3043 |
| 3044 | Copy of | None | Undefined | accord | 0 | 1 | 1.3 | 0 | \spawn deployable 3044 |
| 3045 | Transit to the Amazon | None | Undefined | accord | 0 | 1 | 0.7 | 0 | \spawn deployable 3045 |
| 3046 | - | None | Undefined | accord | 0 | 1 | 1 | 0 | \spawn deployable 3046 |
| 3047 | - | None | Deployed Target | chosen | 2500 | 999999 | 1 | 0 | \spawn deployable 3047 |
| 3048 | - | None | Undefined | accord | 0 | 1 | 1 | 2500 | \spawn deployable 3048 |
| 3049 | - | None | Interactable Objective | - | 50 | 1 | 1 | 0 | \spawn deployable 3049 |
| 3050 | - | None | Undefined | accord | 0 | 1 | 2.35 | 0 | \spawn deployable 3050 |
| 3051 | - | None | Undefined | accord | 0 | 1 | 2.35 | 0 | \spawn deployable 3051 |
| 3052 | - | None | Undefined | accord | 0 | 1 | 1 | 0 | \spawn deployable 3052 |
| 3053 | - | None | Undefined | accord | 0 | 1 | 2.35 | 0 | \spawn deployable 3053 |
| 3054 | _Razorwind Assassin Twin 01 - Proximity Mine | Mine | Undefined | chosen | 1 | 1 | 2 | 1000 | \spawn deployable 3054 |
| 3055 | Equipment Control Switch | None | Undefined | accord | 0 | 50 | 1 | 0 | \spawn deployable 3055 |
| 3056 | Hull Breach Containment Switch | None | Undefined | accord | 0 | 50 | 1 | 0 | \spawn deployable 3056 |
| 3057 | Water Pump Terminal | None | Undefined | accord | 0 | 1 | 1 | 0 | \spawn deployable 3057 |
| 3058 | - | None | Undefined | accord | 0 | 1 | 1 | 0 | \spawn deployable 3058 |
| 3059 | - | None | Undefined | accord | 0 | 1 | 1 | 2000 | \spawn deployable 3059 |
| 3060 | - | None | Undefined | accord | 60 | 24000 | 1 | 0 | \spawn deployable 3060 |
| 3061 | Explosive Barrel | None | Undefined | accord | 1 | 300 | 1 | 0 | \spawn deployable 3061 |
| 3062 | - | None | Undefined | accord | 0 | 150 | 0.75 | 0 | \spawn deployable 3062 |
| 3063 | - | None | Target (primary) | accord | 60 | 24000 | 1 | 0 | \spawn deployable 3063 |
| 3064 | ChosenRockFormations | None | Undefined | - | 0 | 0 | 1 | 0 | \spawn deployable 3064 |
| 3065 | ChosenTechA | None | Undefined | - | 0 | 0 | 1 | 0 | \spawn deployable 3065 |
| 3066 | ChosenTechB | None | Undefined | - | 0 | 0 | 1 | 0 | \spawn deployable 3066 |
| 3067 | ChosenTechInterior | None | Undefined | - | 0 | 0 | 1 | 0 | \spawn deployable 3067 |
| 3068 | DeadGuards | None | Undefined | - | 0 | 0 | 1 | 0 | \spawn deployable 3068 |
| 3069 | - | None | Undefined | - | 0 | 0 | 1 | 0 | \spawn deployable 3069 |
| 3070 | - | Spawner | Deployed Target | accord | 0 | 0 | 1 | 250 | \spawn deployable 3070 |
| 3071 | ChosenPrisonPodIntact | None | Undefined | - | 0 | 0 | 1.2 | 0 | \spawn deployable 3071 |
| 3072 | ChosenPrisonPodBroken | None | Undefined | - | 0 | 0 | 1 | 0 | \spawn deployable 3072 |
| 3073 | Plant Explosive | None | Undefined | - | 0 | 1 | 1 | 0 | \spawn deployable 3073 |
| 3074 | Razorwind Assasin Twin 02 - Artillery Strike - Beacon | Mine | Undefined | chosen | 5 | 1 | 1 | 500 | \spawn deployable 3074 |
| 3075 | - | Mine | Undefined | accord | 0 | 1 | 1 | 500 | \spawn deployable 3075 |
| 3076 | - | Mine | Undefined | accord | 0 | 9999999 | 1 | 500 | \spawn deployable 3076 |
| 3077 | - | Mine | Undefined | accord | 0 | 9999999 | 1 | 500 | \spawn deployable 3077 |
| 3078 | Jump Pad | None | Undefined | accord | 0 | 0 | 1 | 0 | \spawn deployable 3078 |
| 3079 | - | None | Undefined | accord | 0 | 1 | 1 | 2500 | \spawn deployable 3079 |
| 3080 | Blast Door | None | Undefined | accord | 0 | 1 | 5.5 | 0 | \spawn deployable 3080 |
| 3081 | Skiver Egg | None | Undefined | gaea | 2 | 780 | 1.5 | 1000 | \spawn deployable 3081 |
| 3082 | Core Mission 3 Core mission 3 Area waypoint | None | Undefined | - | 1 | 1 | 1 | 0 | \spawn deployable 3082 |
| 3083 | - | None | Undefined | - | 0 | 0 | 1 | 0 | \spawn deployable 3083 |
| 3084 | Accord Drop Ship | Spawner | Target (primary) | accord | 10 | 100 | 1 | 250 | \spawn deployable 3084 |
| 3085 | Copy of Just a terminal for testing with. | None | Undefined | accord | 0 | 1 | 1 | 0 | \spawn deployable 3085 |
| 3086 | - | None | Market | accord | 0 | 0 | 1 | 0 | \spawn deployable 3086 |
| 3087 | Mission03CrashShip | None | Undefined | - | 0 | 1 | 1 | 0 | \spawn deployable 3087 |
| 3088 | Transit to Devil's Tusk | None | Undefined | accord | 0 | 1 | 0.7 | 0 | \spawn deployable 3088 |
| 3089 | - | None | Target (secondary) | accord | 125 | 50000 | 1 | 40666 | \spawn deployable 3089 |
| 3090 | - | None | Target (secondary) | accord | 125 | 50000 | 1 | 40666 | \spawn deployable 3090 |
| 3091 | - | None | Target (secondary) | accord | 125 | 50000 | 1 | 40666 | \spawn deployable 3091 |
| 3092 | - | None | Target (secondary) | accord | 125 | 50000 | 1 | 40666 | \spawn deployable 3092 |
| 3093 | - | None | Target (secondary) | accord | 125 | 50000 | 1 | 40666 | \spawn deployable 3093 |
| 3094 | - | None | Target (secondary) | accord | 125 | 50000 | 1 | 40666 | \spawn deployable 3094 |
| 3095 | - | Adventurer's Glider Pad | Undefined | accord | 0.75 | 1 | 1 | 0 | \spawn deployable 3095 |
| 3096 | Mission4.0_CrashSiteLandingShip | None | Undefined | - | 0 | 0 | 1 | 0 | \spawn deployable 3096 |
| 3097 | - | None | Undefined | - | 0 | 0 | 1 | 0 | \spawn deployable 3097 |
| 3098 | - | None | Undefined | accord | 0 | 1 | 2 | 0 | \spawn deployable 3098 |
| 3099 | Mission03Debris | None | Undefined | - | 0 | 0 | 1 | 0 | \spawn deployable 3099 |
| 3100 | Power up Facility | None | Undefined | accord | 0 | 1 | 1 | 0 | \spawn deployable 3100 |
| 3101 | Mission03Rubble | None | Undefined | - | 0 | 0 | 1 | 0 | \spawn deployable 3101 |
| 3102 | Accord Drop Ship | Spawner | Undefined | accord | 0 | 0 | 1 | 250 | \spawn deployable 3102 |
| 3103 | Mission4.0_CrashSite | None | Undefined | - | 0 | 10000 | 1 | 0 | \spawn deployable 3103 |
| 3105 | - | None | Undefined | accord | 0 | 1 | 1 | 0 | \spawn deployable 3105 |
| 3106 | Disable Security Terminal | None | Undefined | accord | 0 | 1 | 1 | 0 | \spawn deployable 3106 |
| 3107 | - | Anti-Personnel Turret | Fixed Weapon | bandit | 3 | 3000 | 1.5 | 5000 | \spawn deployable 3107 |
| 3108 | Supply Crate | None | Interactable Objective | accord | 0 | 1 | 1 | 5000 | \spawn deployable 3108 |
| 3109 | - | None | Deployed Target | gaea | 7 | 1 | 0.25 | 0 | \spawn deployable 3109 |
| 3110 | - | None | Undefined | gaea | 10 | 0 | 1 | 1000 | \spawn deployable 3110 |
| 3111 | - | None | Undefined | gaea | 10 | 3000 | 1 | 0 | \spawn deployable 3111 |
| 3112 | Scorcher Lair | None | Undefined | gaea | 10 | 3000 | 1 | 0 | \spawn deployable 3112 |
| 3113 | - | None | Undefined | gaea | 10 | 3000 | 1 | 0 | \spawn deployable 3113 |
| 3114 | Crab Spider Nest | None | Undefined | gaea | 10 | 3000 | 1 | 0 | \spawn deployable 3114 |
| 3115 | - | None | Undefined | gaea | 10 | 3000 | 1 | 0 | \spawn deployable 3115 |
| 3116 | Crystite Turret | None | Fixed Weapon | gaea | 4 | 4500 | 0.5 | 300 | \spawn deployable 3116 |
| 3117 | Heavy Turret - Rank I | Turret | Fixed Weapon | accord | 2.5 | 1 | 1.3 | 1500 | \spawn deployable 3117 |
| 3118 | - | None | Undefined | - | 0 | 1 | 0.72 | 0 | \spawn deployable 3118 |
| 3119 | - | None | Undefined | - | 0 | 1 | 0.72 | 0 | \spawn deployable 3119 |
| 3120 | - | None | Undefined | gaea | 10 | 150 | 1 | 0 | \spawn deployable 3120 |
| 3121 | - | None | Undefined | gaea | 10 | 0 | 1 | 0 | \spawn deployable 3121 |
| 3122 | - | None | Undefined | gaea | 25 | 10000 | 1 | 0 | \spawn deployable 3122 |
| 3123 | - | None | Undefined | friendly | 20 | 20000 | 1 | 1000 | \spawn deployable 3123 |
| 3124 | - | None | Undefined | gaea | 7.5 | 3000 | 1 | 0 | \spawn deployable 3124 |
| 3125 | - | None | Undefined | - | 0 | 1 | 1.25 | 0 | \spawn deployable 3125 |
| 3126 | - | None | Undefined | accord | 0 | 1 | 1 | 0 | \spawn deployable 3126 |
| 3127 | Dead Accord Soldier | Mine | Undefined | accord | 0 | 9999999 | 1 | 500 | \spawn deployable 3127 |
| 3128 | - | None | Interactable Objective | neutral | 0 | 0 | 1 | 0 | \spawn deployable 3128 |
| 3129 | - | None | Undefined | - | 1 | 1 | 1 | 0 | \spawn deployable 3129 |
| 3130 | - | None | Undefined | - | 1 | 1 | 1 | 0 | \spawn deployable 3130 |
| 3131 | - | None | Target (primary) | accord | 0 | 0 | 2 | 0 | \spawn deployable 3131 |
| 3132 | - | None | Undefined | chosen | 0 | 1 | 1 | 0 | \spawn deployable 3132 |
| 3133 | - | Turret | Fixed Weapon | bandit | 4 | 1200 | 1.5 | 5000 | \spawn deployable 3133 |
| 3134 | - | None | Deployed Target | accord | 7.5 | 3000 | 1 | 3000 | \spawn deployable 3134 |
| 3138 | - | None | Undefined | - | 0 | 1 | 0.7 | 0 | \spawn deployable 3138 |
| 3139 | - | Power Cell Dispenser | Undefined | friendly | 0 | 1 | 4 | 0 | \spawn deployable 3139 |
| 3140 | - | Mine | Undefined | neutral | 0 | 1 | 4 | 2000 | \spawn deployable 3140 |
| 3141 | - | None | Undefined | - | 1 | 1 | 1 | 0 | \spawn deployable 3141 |
| 3142 | - | None | Undefined | - | 1 | 1 | 1 | 0 | \spawn deployable 3142 |
| 3143 | - | None | Undefined | - | 1 | 1 | 1 | 0 | \spawn deployable 3143 |
| 3144 | - | None | Undefined | - | 1 | 1 | 1 | 0 | \spawn deployable 3144 |
| 3145 | - | None | Undefined | - | 1 | 1 | 1 | 0 | \spawn deployable 3145 |
| 3146 | - | None | Undefined | accord | 0 | 1 | 1 | 0 | \spawn deployable 3146 |
| 3147 | - | None | Undefined | - | 1 | 1 | 2 | 0 | \spawn deployable 3147 |
| 3148 | - | None | Undefined | melding | 2.5 | 1000 | 3 | 0 | \spawn deployable 3148 |
| 3149 | - | None | Undefined | melding | 2.5 | 1000 | 1 | 0 | \spawn deployable 3149 |
| 3150 | _DamagedDoor | None | Undefined | accord | 0 | 1 | 2 | 0 | \spawn deployable 3150 |
| 3151 | - | None | Undefined | accord | 0 | 1 | 1.3 | 0 | \spawn deployable 3151 |
| 3152 | - | None | Undefined | accord | 0 | 0 | 1.5 | 0 | \spawn deployable 3152 |
| 3153 | - | None | Target (primary) | chosen | 25 | 6000 | 1 | 0 | \spawn deployable 3153 |
| 3154 | - | None | Undefined | - | 0 | 1 | 0.72 | 0 | \spawn deployable 3154 |
| 3155 | Pantheon Boss Event Beacon | Repair Station | Target (secondary) | chosen | 100 | 200000 | 3 | 500 | \spawn deployable 3155 |
| 3156 | Energy Shield | Shield | Deployed Target | accord | 5 | 1 | 1 | 0 | \spawn deployable 3156 |
| 3157 | - | None | Undefined | - | 1 | 1 | 1 | 0 | \spawn deployable 3157 |
| 3158 | - | None | Undefined | - | 0 | 0 | 1 | 0 | \spawn deployable 3158 |
| 3159 | Cover_Half_Short | None | Undefined | - | 0 | 0 | 1 | 0 | \spawn deployable 3159 |
| 3160 | Cover_Crouch_Short | None | Undefined | - | 0 | 0 | 1 | 0 | \spawn deployable 3160 |
| 3161 | Cover_Crouch_Long | None | Undefined | - | 0 | 0 | 1 | 0 | \spawn deployable 3161 |
| 3162 | - | None | Undefined | - | 0 | 0 | 1 | 0 | \spawn deployable 3162 |
| 3163 | - | None | Undefined | - | 0 | 0 | 1 | 0 | \spawn deployable 3163 |
| 3164 | Cover_Stand_Short | None | Undefined | - | 0 | 0 | 1 | 0 | \spawn deployable 3164 |
| 3165 | - | None | Undefined | - | 0 | 0 | 1 | 0 | \spawn deployable 3165 |
| 3166 | - | None | Undefined | - | 0 | 0 | 1 | 0 | \spawn deployable 3166 |
| 3167 | Cover_Half_Corner | None | Undefined | - | 0 | 0 | 1 | 0 | \spawn deployable 3167 |
| 3168 | Cover_Half_Crouch_T | None | Undefined | - | 0 | 0 | 1 | 0 | \spawn deployable 3168 |
| 3169 | Cover_Stand_Half_Stand | None | Undefined | - | 0 | 0 | 1 | 0 | \spawn deployable 3169 |
| 3170 | - | None | Undefined | - | 0 | 0 | 1 | 0 | \spawn deployable 3170 |
| 3171 | - | None | Undefined | - | 0 | 0 | 1 | 0 | \spawn deployable 3171 |
| 3172 | - | None | Undefined | - | 0 | 0 | 1 | 0 | \spawn deployable 3172 |
| 3173 | - | None | Undefined | - | 0 | 0 | 1 | 0 | \spawn deployable 3173 |
| 3174 | Cover_Stand_Half_Cross | None | Undefined | - | 0 | 0 | 1 | 0 | \spawn deployable 3174 |
| 3175 | Cover_Half_Stand_Half | None | Undefined | - | 0 | 0 | 1 | 0 | \spawn deployable 3175 |
| 3176 | Copy of Cover_Half_Stand_Half | None | Undefined | - | 0 | 0 | 1 | 0 | \spawn deployable 3176 |
| 3177 | Cover_Speedball_Stand | None | Undefined | - | 0 | 0 | 1 | 0 | \spawn deployable 3177 |
| 3178 | Cover_Half_Stand | None | Undefined | - | 0 | 0 | 1 | 0 | \spawn deployable 3178 |
| 3179 | - | None | Undefined | - | 0 | 0 | 1 | 0 | \spawn deployable 3179 |
| 3180 | - | None | Undefined | - | 0 | 0 | 1 | 0 | \spawn deployable 3180 |
| 3181 | - | None | Undefined | - | 0 | 0 | 1 | 0 | \spawn deployable 3181 |
| 3182 | - | None | Undefined | - | 0 | 0 | 1 | 0 | \spawn deployable 3182 |
| 3183 | Cover_Crouch_Stand | None | Undefined | - | 0 | 0 | 1 | 0 | \spawn deployable 3183 |
| 3184 | - | None | Undefined | - | 0 | 0 | 1 | 0 | \spawn deployable 3184 |
| 3185 | - | None | Undefined | - | 0 | 0 | 1 | 0 | \spawn deployable 3185 |
| 3186 | - | None | Undefined | - | 0 | 0 | 1 | 0 | \spawn deployable 3186 |
| 3187 | Mission_3.0_PuzzleLargeRing | None | Undefined | - | 0 | 0 | 1 | 0 | \spawn deployable 3187 |
| 3188 | Mission_3.0_PuzzleSmallRing | None | Undefined | - | 0 | 0 | 1 | 0 | \spawn deployable 3188 |
| 3189 | - | None | Undefined | accord | 0 | 1 | 0.5 | 0 | \spawn deployable 3189 |
| 3190 | Fire Suppression Console | None | Undefined | accord | 3 | 0 | 1 | 0 | \spawn deployable 3190 |
| 3191 | Arclight Data Console | None | Undefined | accord | 2.5 | 1 | 0.45 | 0 | \spawn deployable 3191 |
| 3192 | _Lockbox 5 | None | Undefined | accord | 0 | 1 | 3 | 0 | \spawn deployable 3192 |
| 3193 | _Door | None | Undefined | accord | 0 | 1 | 1 | 0 | \spawn deployable 3193 |
| 3194 | _Keyhole | None | Undefined | - | 0 | 1 | 1 | 0 | \spawn deployable 3194 |
| 3195 | Heavy Turret - Rank II | Turret | Fixed Weapon | accord | 4 | 1600 | 1.3 | 1000 | \spawn deployable 3195 |
| 3196 | - | Mannable Turret | Fixed Weapon | accord | 0 | 6000 | 1 | 0 | \spawn deployable 3196 |
| 3197 | Supply Station | None | Deployed Target | accord | 0.625 | 1 | 1 | 0 | \spawn deployable 3197 |
| 3198 | Engineer Anti-Personnel Turret | Anti-Personnel Turret | Fixed Weapon | accord | 2000 | 2000 | 2 | 2500 | \spawn deployable 3198 |
| 3199 | - | None | Undefined | accord | 0 | 0 | 2 | 500 | \spawn deployable 3199 |
| 3200 | SIN Server Core Terminal | None | Undefined | accord | 0 | 1 | 1 | 0 | \spawn deployable 3200 |
| 3201 | SIN Server Core Status Screen | None | Undefined | accord | 0 | 1 | 5.629 | 0 | \spawn deployable 3201 |
| 3202 | - | Spawner | Undefined | accord | 0 | 0 | 1 | 5000 | \spawn deployable 3202 |
| 3204 | Captive Chosen - Turret Powered Down | None | Fixed Weapon | accord | 6 | 2400 | 1.35 | 100 | \spawn deployable 3204 |
| 3205 | Chosen Turret | Turret | Fixed Weapon | chosen | 12 | 2400 | 1 | 0 | \spawn deployable 3205 |
| 3206 | Accord Soldier | None | Interactable Objective | neutral | 0 | 1 | 1 | 0 | \spawn deployable 3206 |
| 3207 | _Cover - Covered Crate Red | None | Undefined | - | 0 | 1 | 0.7 | 0 | \spawn deployable 3207 |
| 3208 | _Cover - Covered Crate Blue | None | Undefined | - | 0 | 1 | 0.7 | 0 | \spawn deployable 3208 |
| 3209 | _Cover - Crate | None | Undefined | - | 0 | 1 | 1 | 0 | \spawn deployable 3209 |
| 3210 | _Cover - Crashed MGV | None | Undefined | - | 0 | 1 | 1 | 0 | \spawn deployable 3210 |
| 3211 | _Cover - Crashed Convoy | None | Undefined | - | 0 | 1 | 1 | 0 | \spawn deployable 3211 |
| 3212 | _Cover - Thumper Cart | None | Undefined | - | 0 | 1 | 1 | 0 | \spawn deployable 3212 |
| 3213 | Cover_Stand | None | Undefined | - | 0 | 0 | 1 | 0 | \spawn deployable 3213 |
| 3214 | - | None | Target (primary) | accord | 0.025 | 10 | 1 | 0 | \spawn deployable 3214 |
| 3215 | Enter Mission | None | Undefined | accord | 0 | 1 | 1 | 0 | \spawn deployable 3215 |
| 3216 | - | Shield | Deployed Target | accord | 5 | 1 | 0.5 | 0 | \spawn deployable 3216 |
| 3217 | Mission_2.0_CoverA | None | Undefined | - | 0 | 0 | 1 | 0 | \spawn deployable 3217 |
| 3218 | Mission_2.0_CoverB | None | Undefined | - | 0 | 0 | 1 | 0 | \spawn deployable 3218 |
| 3219 | Mission_2.0_CoverC | None | Undefined | - | 0 | 0 | 1 | 0 | \spawn deployable 3219 |
| 3220 | Mission_2.0_CoverD | None | Undefined | - | 0 | 0 | 1 | 0 | \spawn deployable 3220 |
| 3221 | Kara Novan - Proximity Mine | Mine | Undefined | accord | 1 | 1 | 2 | 1000 | \spawn deployable 3221 |
| 3222 | - | None | Undefined | - | 5 | 500 | 1.2 | 0 | \spawn deployable 3222 |
| 3223 | - | None | Undefined | neutral | 0 | 0 | 1 | 0 | \spawn deployable 3223 |
| 3224 | Authorization Terminal | None | Undefined | accord | 0 | 1 | 1 | 0 | \spawn deployable 3224 |
| 3225 | Enter WARCOM | None | Undefined | accord | 0 | 1 | 1 | 0 | \spawn deployable 3225 |
| 3226 | Bandit Cover | None | Undefined | - | 0 | 0 | 1 | 0 | \spawn deployable 3226 |
| 3227 | Accord Cover | None | Undefined | - | 0 | 0 | 1 | 0 | \spawn deployable 3227 |
| 3228 | Accord Cover | None | Undefined | - | 0 | 0 | 1 | 0 | \spawn deployable 3228 |
| 3229 | Accord Cover | None | Undefined | - | 0 | 0 | 1 | 0 | \spawn deployable 3229 |
| 3230 | Accord Cover | None | Undefined | - | 0 | 0 | 1 | 0 | \spawn deployable 3230 |
| 3231 | Chosen Cover | None | Undefined | - | 0 | 0 | 1 | 0 | \spawn deployable 3231 |
| 3232 | Accord Cover | None | Undefined | - | 0 | 0 | 1 | 0 | \spawn deployable 3232 |
| 3233 | Bandit Cover | None | Undefined | - | 0 | 0 | 1 | 0 | \spawn deployable 3233 |
| 3234 | Bandit Cover | None | Undefined | - | 0 | 0 | 1 | 0 | \spawn deployable 3234 |
| 3235 | Bandit Cover | None | Undefined | - | 0 | 0 | 1 | 0 | \spawn deployable 3235 |
| 3236 | Bandit Cover | None | Undefined | - | 0 | 0 | 1 | 0 | \spawn deployable 3236 |
| 3237 | Bandit Cover | None | Undefined | - | 0 | 0 | 1 | 0 | \spawn deployable 3237 |
| 3238 | Bandit Cover | None | Undefined | - | 0 | 0 | 1 | 0 | \spawn deployable 3238 |
| 3239 | Bandit Cover | None | Undefined | - | 0 | 0 | 1 | 0 | \spawn deployable 3239 |
| 3240 | Bandit Cover | None | Undefined | - | 0 | 0 | 1 | 0 | \spawn deployable 3240 |
| 3241 | Bandit Cover | None | Undefined | - | 0 | 0 | 1 | 0 | \spawn deployable 3241 |
| 3242 | Bandit Cover | None | Undefined | - | 0 | 0 | 1 | 0 | \spawn deployable 3242 |
| 3243 | Bandit Cover | None | Undefined | - | 0 | 0 | 1 | 0 | \spawn deployable 3243 |
| 3244 | Bandit Cover | None | Undefined | - | 0 | 0 | 1 | 0 | \spawn deployable 3244 |
| 3245 | Bandit Cover | None | Undefined | - | 0 | 0 | 1 | 0 | \spawn deployable 3245 |
| 3246 | Bandit Cover | None | Undefined | - | 0 | 0 | 1 | 0 | \spawn deployable 3246 |
| 3247 | Bandit Cover | None | Undefined | - | 0 | 0 | 1 | 0 | \spawn deployable 3247 |
| 3248 | Bandit Cover | None | Undefined | - | 0 | 0 | 1 | 0 | \spawn deployable 3248 |
| 3249 | Bandit Cover | None | Undefined | - | 0 | 0 | 1 | 0 | \spawn deployable 3249 |
| 3250 | Accord Cover | None | Undefined | - | 0 | 0 | 1 | 0 | \spawn deployable 3250 |
| 3251 | Bandit Cover | None | Undefined | - | 0 | 0 | 1 | 0 | \spawn deployable 3251 |
| 3252 | Bandit Cover | None | Undefined | - | 0 | 0 | 1 | 0 | \spawn deployable 3252 |
| 3253 | Bandit Cover | None | Undefined | - | 0 | 0 | 1 | 0 | \spawn deployable 3253 |
| 3254 | Accord Cover | None | Undefined | - | 0 | 0 | 1 | 0 | \spawn deployable 3254 |
| 3255 | Accord Cover | None | Undefined | - | 0 | 0 | 1 | 0 | \spawn deployable 3255 |
| 3256 | Accord Cover | None | Undefined | - | 0 | 0 | 1 | 0 | \spawn deployable 3256 |
| 3257 | Accord Cover | None | Undefined | - | 0 | 0 | 1 | 0 | \spawn deployable 3257 |
| 3258 | Accord Cover | None | Undefined | - | 0 | 0 | 1 | 0 | \spawn deployable 3258 |
| 3259 | Accord Cover | None | Undefined | - | 0 | 0 | 1 | 0 | \spawn deployable 3259 |
| 3260 | Accord Cover | None | Undefined | - | 0 | 0 | 1 | 0 | \spawn deployable 3260 |
| 3261 | Accord Cover | None | Undefined | - | 0 | 0 | 1 | 0 | \spawn deployable 3261 |
| 3262 | Accord Cover | None | Undefined | - | 0 | 0 | 1 | 0 | \spawn deployable 3262 |
| 3263 | Accord Cover | None | Undefined | - | 0 | 0 | 1 | 0 | \spawn deployable 3263 |
| 3264 | Accord Cover | None | Undefined | - | 0 | 0 | 1 | 0 | \spawn deployable 3264 |
| 3265 | Accord Cover | None | Undefined | - | 0 | 0 | 1 | 0 | \spawn deployable 3265 |
| 3266 | Accord Cover | None | Undefined | - | 0 | 0 | 1 | 0 | \spawn deployable 3266 |
| 3267 | Accord Cover | None | Undefined | - | 0 | 0 | 1 | 0 | \spawn deployable 3267 |
| 3268 | Accord Cover | None | Undefined | - | 0 | 0 | 1 | 0 | \spawn deployable 3268 |
| 3269 | Accord Cover | None | Undefined | - | 0 | 0 | 1 | 0 | \spawn deployable 3269 |
| 3270 | Accord Cover | None | Undefined | - | 0 | 0 | 1 | 0 | \spawn deployable 3270 |
| 3271 | Accord Cover | None | Undefined | - | 0 | 0 | 1 | 0 | \spawn deployable 3271 |
| 3272 | Accord Cover | None | Undefined | - | 0 | 0 | 1 | 0 | \spawn deployable 3272 |
| 3273 | Accord Cover | None | Undefined | - | 0 | 0 | 1 | 0 | \spawn deployable 3273 |
| 3274 | Accord Cover | None | Undefined | - | 0 | 0 | 1 | 0 | \spawn deployable 3274 |
| 3275 | Accord Cover | None | Undefined | - | 0 | 0 | 1 | 0 | \spawn deployable 3275 |
| 3276 | Accord Cover | None | Undefined | - | 0 | 0 | 1 | 0 | \spawn deployable 3276 |
| 3277 | Accord Cover | None | Undefined | - | 0 | 0 | 1 | 0 | \spawn deployable 3277 |
| 3278 | Accord Cover | None | Undefined | - | 0 | 0 | 1 | 0 | \spawn deployable 3278 |
| 3279 | Accord Cover | None | Undefined | - | 0 | 0 | 1 | 0 | \spawn deployable 3279 |
| 3280 | Accord Cover | None | Undefined | - | 0 | 0 | 1 | 0 | \spawn deployable 3280 |
| 3281 | Accord Cover | None | Undefined | - | 0 | 0 | 1 | 0 | \spawn deployable 3281 |
| 3282 | Chosen Cover | None | Undefined | - | 0 | 0 | 1 | 0 | \spawn deployable 3282 |
| 3283 | Chosen Cover | None | Undefined | - | 0 | 0 | 1 | 0 | \spawn deployable 3283 |
| 3284 | Chosen Cover | None | Undefined | - | 0 | 0 | 1 | 0 | \spawn deployable 3284 |
| 3285 | Chosen Cover | None | Undefined | - | 0 | 0 | 0.721 | 0 | \spawn deployable 3285 |
| 3286 | Chosen Cover | None | Undefined | - | 0 | 0 | 0.721 | 0 | \spawn deployable 3286 |
| 3287 | Chosen Cover | None | Undefined | - | 0 | 0 | 0.326 | 0 | \spawn deployable 3287 |
| 3288 | Chosen Cover | None | Undefined | - | 0 | 0 | 0.291 | 0 | \spawn deployable 3288 |
| 3289 | Chosen Cover | None | Undefined | - | 0 | 0 | 0.714 | 0 | \spawn deployable 3289 |
| 3290 | Chosen Cover | None | Undefined | - | 0 | 0 | 1.464 | 0 | \spawn deployable 3290 |
| 3291 | Accord Cover | None | Undefined | - | 0 | 0 | 0.851 | 0 | \spawn deployable 3291 |
| 3292 | Accord Cover | None | Undefined | - | 0 | 0 | 1 | 0 | \spawn deployable 3292 |
| 3293 | Accord Cover | None | Undefined | - | 0 | 0 | 1 | 0 | \spawn deployable 3293 |
| 3294 | Chosen Cover | None | Undefined | - | 0 | 0 | 1 | 0 | \spawn deployable 3294 |
| 3295 | Chosen Cover | None | Undefined | - | 0 | 0 | 1 | 0 | \spawn deployable 3295 |
| 3296 | Accord Cover | None | Undefined | - | 0 | 0 | 1 | 0 | \spawn deployable 3296 |
| 3297 | Chosen Cover | None | Undefined | - | 0 | 0 | 1 | 0 | \spawn deployable 3297 |
| 3298 | Chosen Cover | None | Undefined | - | 0 | 0 | 0.761 | 0 | \spawn deployable 3298 |
| 3299 | Accord Cover | None | Undefined | - | 0 | 0 | 1.144 | 0 | \spawn deployable 3299 |
| 3300 | Chosen Cover | None | Undefined | - | 0 | 0 | 0.54 | 0 | \spawn deployable 3300 |
| 3301 | Bandit Cover | None | Undefined | - | 0 | 0 | 2.2 | 0 | \spawn deployable 3301 |
| 3302 | Bandit Cover | None | Undefined | - | 0 | 0 | 0.788 | 0 | \spawn deployable 3302 |
| 3303 | Accord Cover | None | Undefined | - | 0 | 0 | 1.94 | 0 | \spawn deployable 3303 |
| 3304 | Accord Cover | None | Undefined | - | 0 | 0 | 0.779 | 0 | \spawn deployable 3304 |
| 3305 | Accord Cover | None | Undefined | - | 0 | 0 | 1.629 | 0 | \spawn deployable 3305 |
| 3306 | Bandit Cover | None | Undefined | - | 0 | 0 | 1 | 0 | \spawn deployable 3306 |
| 3307 | Accord Cover | None | Undefined | - | 0 | 0 | 1.399 | 0 | \spawn deployable 3307 |
| 3308 | Bandit Cover | None | Undefined | - | 0 | 0 | 0.371 | 0 | \spawn deployable 3308 |
| 3309 | Bandit Cover | None | Undefined | - | 0 | 0 | 0.76 | 0 | \spawn deployable 3309 |
| 3310 | Bandit Cover | None | Undefined | - | 0 | 0 | 1 | 0 | \spawn deployable 3310 |
| 3311 | Bandit Cover | None | Undefined | - | 0 | 0 | 1 | 0 | \spawn deployable 3311 |
| 3312 | Open Door | None | Undefined | accord | 0 | 1 | 1.1 | 0 | \spawn deployable 3312 |
| 3313 | Bandit Cover | None | Undefined | - | 0 | 0 | 1 | 0 | \spawn deployable 3313 |
| 3314 | _ | None | Undefined | accord | 0 | 1 | 1 | 0 | \spawn deployable 3314 |
| 3315 | Exit WARCOM | None | Undefined | accord | 0 | 1 | 1 | 0 | \spawn deployable 3315 |
| 3316 | _WARCOM Satellite | None | Undefined | neutral | 0 | 1 | 1 | 0 | \spawn deployable 3316 |
| 3317 | _Cage | None | Undefined | accord | 0 | 0 | 3 | 0 | \spawn deployable 3317 |
| 3318 | _Door | None | Undefined | accord | 0 | 0 | 3.3 | 0 | \spawn deployable 3318 |
| 3319 | - | None | Target (primary) | accord | 35 | 15000 | 1.5 | 0 | \spawn deployable 3319 |
| 3320 | Blast Door Scaled for Core Mission #5 | None | Undefined | - | 5 | 500 | 1.2 | 0 | \spawn deployable 3320 |
| 3321 | - | None | Target (primary) | accord | 35 | 15000 | 1.5 | 0 | \spawn deployable 3321 |
| 3322 | - | None | Target (primary) | accord | 35 | 15000 | 1.5 | 0 | \spawn deployable 3322 |
| 3323 | Melded Squid Oil | None | Target (primary) | accord | 35 | 7800 | 1.5 | 0 | \spawn deployable 3323 |
| 3324 | _Generic Impactor | None | Undefined | neutral | 0 | 1 | 1 | 0 | \spawn deployable 3324 |
| 3325 | Stun Grenade Crate | None | Undefined | - | 0 | 1 | 1 | 0 | \spawn deployable 3325 |
| 3326 | Explosives Crate | None | Undefined | - | 0 | 1 | 1 | 0 | \spawn deployable 3326 |
| 3327 | _UnPassable | None | Undefined | - | 0 | 1 | 1.5 | 0 | \spawn deployable 3327 |
| 3328 | Blast Door for Core Mission #10 | None | Undefined | accord | 0 | 1 | 1.25 | 0 | \spawn deployable 3328 |
| 3329 | Access Terminal | None | Undefined | accord | 0 | 1 | 1 | 0 | \spawn deployable 3329 |
| 3330 | EMP Forcefield Generator | None | Undefined | Reapers | 5 | 4000 | 1 | 2500 | \spawn deployable 3330 |
| 3331 | Chosen Generator | None | Interactable Objective | - | 0 | 0 | 1 | 0 | \spawn deployable 3331 |
| 3332 | EMP Field Control Terminal | None | Undefined | accord | 0 | 50 | 1 | 0 | \spawn deployable 3332 |
| 3333 | - | None | Target (primary) | accord | 35 | 15000 | 0.5 | 0 | \spawn deployable 3333 |
| 3334 | - | None | Target (primary) | accord | 35 | 15000 | 0.5 | 0 | \spawn deployable 3334 |
| 3335 | - | None | Target (primary) | accord | 35 | 15000 | 0.5 | 0 | \spawn deployable 3335 |
| 3336 | Typing NPC Emote for Chosen | None | Work | chosen | 0 | 0 | 1 | 0 | \spawn deployable 3336 |
| 3337 | - | None | Undefined | Reapers | 20 | 7800 | 1.25 | 2500 | \spawn deployable 3337 |
| 3339 | Accord Drop Ship | Spawner | Undefined | accord | 0 | 0 | 1 | 250 | \spawn deployable 3339 |
| 3340 | Mysterious Cargo | None | Undefined | accord | 0 | 1 | 1 | 3000 | \spawn deployable 3340 |
| 3341 | - | None | Fixed Weapon | accord | 0 | 400 | 1 | 300 | \spawn deployable 3341 |
| 3342 | Camera Bot | Mine | Undefined | accord | 0 | 1 | 1 | 500 | \spawn deployable 3342 |
| 3343 | GiGi Console | None | Undefined | accord | 0 | 0 | 1 | 0 | \spawn deployable 3343 |
| 3344 | - | None | Undefined | - | 0 | 1 | 1 | 0 | \spawn deployable 3344 |
| 3345 | Garbage Crusher Plate | None | Interactable Objective | neutral | 0 | 1 | 1 | 0 | \spawn deployable 3345 |
| 3346 | Turret Powered Down | Turret | Fixed Weapon | accord | 6 | 2400 | 1.35 | 100 | \spawn deployable 3346 |
| 3347 | Turret Powered On | Turret | Fixed Weapon | accord | 6 | 2400 | 1.35 | 100 | \spawn deployable 3347 |
| 3348 | Vorgth Flag Ship | None | Deployed Target | chosen | 90 | 10000 | 1 | 250 | \spawn deployable 3348 |
| 3349 | - | None | Undefined | Rebels | 15 | 0 | 1 | 0 | \spawn deployable 3349 |
| 3350 | Cargo Rail Control Terminal | None | Undefined | accord | 0 | 50 | 1 | 0 | \spawn deployable 3350 |
| 3351 | _Metal Gibs 8 | None | Undefined | neutral | 0 | 1 | 1 | 0 | \spawn deployable 3351 |
| 3352 | Copy of Blast Door | None | Undefined | accord | 0 | 1 | 17 | 0 | \spawn deployable 3352 |
| 3353 | - | None | Undefined | accord | 0 | 1 | 17 | 0 | \spawn deployable 3353 |
| 3354 | Shutter Door | None | Undefined | accord | 0 | 1 | 3 | 0 | \spawn deployable 3354 |
| 3355 | - | Turret | Fixed Weapon | Ophanim | 7.5 | 3000 | 1.5 | 2500 | \spawn deployable 3355 |
| 3356 | - | None | Undefined | accord | 0 | 1 | 1.5 | 0 | \spawn deployable 3356 |
| 3357 | - | Turret | Fixed Weapon | Ophanim | 3 | 1 | 1.3 | 2000 | \spawn deployable 3357 |
| 3358 | - | Mannable Turret | Fixed Weapon | chosen | 5 | 3500 | 0.8 | 1000 | \spawn deployable 3358 |
| 3359 | Chosen Heavy Turret | Turret | Fixed Weapon | chosen | 5 | 2400 | 0.7 | 100 | \spawn deployable 3359 |
| 3360 | _Marks a sub area | None | Undefined | friendly | 0 | 100 | 1 | 0 | \spawn deployable 3360 |
| 3361 | - | Surface Deposit | Undefined | chosen | 1 | 500 | 1 | 0 | \spawn deployable 3361 |
| 3362 | Plant Explosive | None | Undefined | friendly | 0 | 1 | 1 | 0 | \spawn deployable 3362 |
| 3363 | _Metal Gibs | None | Undefined | neutral | 0 | 1 | 1 | 0 | \spawn deployable 3363 |
| 3364 | _Generic impactor spawner | None | Undefined | neutral | 0 | 1 | 1 | 0 | \spawn deployable 3364 |
| 3365 | Explosives Crate | None | Undefined | - | 0 | 1 | 1 | 0 | \spawn deployable 3365 |
| 3366 | - | Turret | Fixed Weapon | Black Hills Bandits | 3 | 1 | 1.3 | 2000 | \spawn deployable 3366 |
| 3367 | Heavy Turret | Turret | Fixed Weapon | bandit | 3 | 1 | 1.3 | 2000 | \spawn deployable 3367 |
| 3368 | Heavy Turret | Turret | Fixed Weapon | Reapers | 3 | 1 | 1.3 | 2000 | \spawn deployable 3368 |
| 3369 | - | Turret | Fixed Weapon | Rebels | 3 | 1 | 1.3 | 2000 | \spawn deployable 3369 |
| 3370 | - | Turret | Fixed Weapon | Tanken | 3 | 1 | 1.3 | 2000 | \spawn deployable 3370 |
| 3371 | Heavy Turret | Turret | Fixed Weapon | accord | 3 | 1 | 1.3 | 2000 | \spawn deployable 3371 |
| 3372 | - | Turret | Fixed Weapon | bandit | 3 | 1 | 1.3 | 2000 | \spawn deployable 3372 |
| 3373 | - | Turret | Fixed Weapon | bandit | 3 | 1 | 1.3 | 2000 | \spawn deployable 3373 |
| 3374 | Open Door | None | Undefined | accord | 0 | 1 | 1.4 | 0 | \spawn deployable 3374 |
| 3375 | - | Surface Deposit | Undefined | friendly | 0.375 | 150 | 1 | 0 | \spawn deployable 3375 |
| 3376 | Chrysalis | None | Target (primary) | gaea | 8 | 0 | 3 | 0 | \spawn deployable 3376 |
| 3377 | Blast Door Lock | None | Undefined | accord | 0 | 1 | 0.4 | 0 | \spawn deployable 3377 |
| 3378 | - | None | Undefined | - | 0 | 1 | 1 | 0 | \spawn deployable 3378 |
| 3379 | Shutter Door | None | Undefined | - | 0 | 1 | 9 | 0 | \spawn deployable 3379 |
| 3380 | Hanger Door A | None | Undefined | - | 0 | 1 | 3.7 | 0 | \spawn deployable 3380 |
| 3381 | Glider Pad Control Terminal | None | Undefined | accord | 0 | 50 | 1 | 0 | \spawn deployable 3381 |
| 3382 | Hanger Door B | None | Undefined | - | 0 | 1 | 3.7 | 0 | \spawn deployable 3382 |
| 3383 | - | None | Undefined | accord | 0 | 1 | 2 | 0 | \spawn deployable 3383 |
| 3384 | - | None | Target (primary) | accord | 1 | 1 | 1 | 0 | \spawn deployable 3384 |
| 3385 | - | None | Undefined | accord | 0 | 1 | 5 | 0 | \spawn deployable 3385 |
| 3386 | - | None | Undefined | - | 0 | 0 | 1 | 0 | \spawn deployable 3386 |
| 3387 | - | None | Undefined | - | 0 | 0 | 1.2 | 0 | \spawn deployable 3387 |
| 3388 | - | Turret | Fixed Weapon | accord | 2.5 | 1 | 1.3 | 1000 | \spawn deployable 3388 |
| 3389 | - | Shield | Deployed Target | accord | 5 | 1 | 1 | 0 | \spawn deployable 3389 |
| 3390 | - | Anti-Personnel Turret | Fixed Weapon | accord | 2000 | 2000 | 2 | 2500 | \spawn deployable 3390 |
| 3391 | - | None | Deployed Target | accord | 0.625 | 1 | 1 | 0 | \spawn deployable 3391 |
| 3392 | U.A.S. Bellicose Cargo | None | Undefined | friendly | 0 | 1 | 2 | 0 | \spawn deployable 3392 |
| 3393 | EMP Turret | None | Undefined | accord | 0 | 1 | 1 | 0 | \spawn deployable 3393 |
| 3394 | EMP Activation Terminal | None | Undefined | accord | 0 | 1 | 1 | 0 | \spawn deployable 3394 |
| 3395 | _ | None | Undefined | accord | 0 | 100 | 0.4 | 0 | \spawn deployable 3395 |
| 3396 | Authorization Terminal | None | Undefined | accord | 0 | 1 | 1 | 0 | \spawn deployable 3396 |
| 3397 | Authorization Terminal | None | Undefined | accord | 0 | 1 | 1 | 0 | \spawn deployable 3397 |
| 3398 | EMP Firing Console | None | Undefined | accord | 0 | 0 | 1 | 0 | \spawn deployable 3398 |
| 3399 | - | None | Market | accord | 0 | 0 | 1 | 0 | \spawn deployable 3399 |
| 3400 | _Marks an area | None | Undefined | friendly | 0 | 100 | 1 | 0 | \spawn deployable 3400 |
| 3401 | Reaper Dropship | Spawner | Deployed Target | Reapers | 30 | 6000 | 1 | 250 | \spawn deployable 3401 |
| 3402 | Dropship | Spawner | Deployed Target | Reapers | 30 | 6000 | 1 | 250 | \spawn deployable 3402 |
| 3403 | Platform | None | Undefined | - | 1 | 1 | 1 | 0 | \spawn deployable 3403 |
| 3404 | - | None | Fixed Weapon | Reapers | 125 | 50000 | 0.4 | 0 | \spawn deployable 3404 |
| 3405 | PvP Jump Pad | None | Undefined | accord | 0 | 0 | 1.25 | 0 | \spawn deployable 3405 |
| 3406 | - | None | Undefined | friendly | 0 | 1 | 1 | 0 | \spawn deployable 3406 |
| 3407 | - | Turret | Fixed Weapon | bandit | 3 | 1 | 1.3 | 2000 | \spawn deployable 3407 |
| 3408 | _Piece of the EMP Generator for core mission 10 | None | Undefined | accord | 0 | 1 | 1 | 0 | \spawn deployable 3408 |
| 3409 | _Piece of the EMP Generator for core mission 10 | None | Undefined | accord | 0 | 1 | 1 | 0 | \spawn deployable 3409 |
| 3410 | - | Turret | Fixed Weapon | bandit | 3 | 1 | 1.3 | 2000 | \spawn deployable 3410 |
| 3411 | PvP Speed Boost Station | None | Undefined | accord | 1.25 | 500 | 1 | 0 | \spawn deployable 3411 |
| 3412 | Dropship Extraction Winch | None | Undefined | accord | 0 | 50 | 0.5 | 0 | \spawn deployable 3412 |
| 3413 | Heavy Turret | None | Fixed Weapon | accord | 13 | 1 | 1.3 | 2000 | \spawn deployable 3413 |
| 3414 | - | None | Undefined | accord | 0 | 0 | 1 | 0 | \spawn deployable 3414 |
| 3416 | Accord Dropship | Spawner | Deployed Target | accord | 0 | 0 | 1 | 250 | \spawn deployable 3416 |
| 3417 | - | None | Guard Stance | accord | 0 | 0 | 1 | 0 | \spawn deployable 3417 |
| 3418 | Bandit Gear | None | Interactable Objective | accord | 0 | 1 | 1 | 5000 | \spawn deployable 3418 |
| 3419 | _Piece of the EMP Generator for core mission 10 | None | Undefined | accord | 0 | 1 | 1 | 0 | \spawn deployable 3419 |
| 3420 | _ | None | Rest | accord | 0 | 0 | 1 | 0 | \spawn deployable 3420 |
| 3421 | Energy Wall | Shield | Deployed Target | accord | 0 | 0 | 1.5 | 0 | \spawn deployable 3421 |
| 3422 | - | None | Undefined | - | 0 | 1 | 1 | 0 | \spawn deployable 3422 |
| 3423 | Shelter | None | Undefined | accord | 0 | 1 | 2 | 0 | \spawn deployable 3423 |
| 3424 | Distress Signal | None | Undefined | accord | 0 | 0 | 2 | 0 | \spawn deployable 3424 |
| 3425 | Accord Soldier | None | Interactable Objective | neutral | 0 | 1 | 1 | 0 | \spawn deployable 3425 |
| 3426 | - | None | Undefined | accord | 0 | 0 | 1.1 | 0 | \spawn deployable 3426 |
| 3427 | - | Charged Pulse | Deployed Target | accord | 5 | 1 | 1 | 1000 | \spawn deployable 3427 |
| 3428 | Hisser Lair | None | Undefined | friendly | 0 | 1 | 1 | 0 | \spawn deployable 3428 |
| 3429 | - | Turret | Fixed Weapon | accord | 2.5 | 1 | 1.3 | 2000 | \spawn deployable 3429 |
| 3430 | _Invisible Sin Tap Install Marker | None | Interactable Objective | accord | 0 | 0 | 1 | 0 | \spawn deployable 3430 |
| 3431 | _Invisible Thump Dump SIN Tower Hack Marker | None | Interactable Objective | accord | 0 | 0 | 1 | 0 | \spawn deployable 3431 |
| 3432 | Melding Pocket Transport | Spawner | Undefined | accord | 0 | 1 | 1 | 0 | \spawn deployable 3432 |
| 3433 | - | None | Fixed Weapon | Reapers | 25 | 10000 | 0.4 | 0 | \spawn deployable 3433 |
| 3434 | Flamethrower Auto-Turret | Turret | Fixed Weapon | gaea | 10 | 40000 | 1 | 1000 | \spawn deployable 3434 |
| 3435 | Cargo Rail Lock | None | Undefined | accord | 40 | 16000 | 1.5 | 0 | \spawn deployable 3435 |
| 3436 | _Thumper Component - Cover Object Spawner | None | Undefined | neutral | 0 | 1 | 1 | 0 | \spawn deployable 3436 |
| 3437 | - | None | Interactable Objective | neutral | 0 | 1 | 1 | 0 | \spawn deployable 3437 |
| 3438 | - | None | Undefined | - | 0 | 1 | 1 | 0 | \spawn deployable 3438 |
| 3439 | Accord Soldier | None | Interactable Objective | neutral | 0 | 1 | 1 | 0 | \spawn deployable 3439 |
| 3440 | Corpse | Mine | Undefined | accord | 0 | 9999999 | 1 | 500 | \spawn deployable 3440 |
| 3441 | Corpse | None | Interactable Objective | neutral | 0 | 1 | 1 | 0 | \spawn deployable 3441 |
| 3442 | Oilspill's Teddy Bear | None | Undefined | accord | 0.025 | 10 | 10 | 0 | \spawn deployable 3442 |
| 3443 | Headset | None | Undefined | - | 0 | 1 | 1 | 0 | \spawn deployable 3443 |
| 3444 | - | None | Undefined | accord | 0 | 1 | 1 | 0 | \spawn deployable 3444 |
| 3445 | Obtain Project Epsilon | None | Undefined | accord | 0 | 1 | 1 | 0 | \spawn deployable 3445 |
| 3446 | _Smugglers Trucks | None | Interactable Objective | friendly | 0 | 0 | 1 | 0 | \spawn deployable 3446 |
| 3447 | - | None | Undefined | accord | 0.025 | 10 | 1 | 0 | \spawn deployable 3447 |
| 3448 | Weapons Cache | None | Target (primary) | accord | 0 | 0 | 1 | 0 | \spawn deployable 3448 |
| 3449 | - | None | Undefined | accord | 0.025 | 10 | 1 | 0 | \spawn deployable 3449 |
| 3450 | Wooden Box | None | Undefined | accord | 2.5 | 2000 | 3 | 0 | \spawn deployable 3450 |
| 3451 | _Kang's Stuff | None | Interactable Objective | accord | 0 | 1 | 1 | 5000 | \spawn deployable 3451 |
| 3452 | - | None | Undefined | accord | 0.025 | 10 | 1 | 0 | \spawn deployable 3452 |
| 3453 | - | None | Undefined | accord | 0 | 10 | 1 | 0 | \spawn deployable 3453 |
| 3454 | - | None | Undefined | accord | 0 | 0 | 1 | 0 | \spawn deployable 3454 |
| 3455 | - | None | Undefined | accord | 0.025 | 10 | 1 | 0 | \spawn deployable 3455 |
| 3456 | - | None | Undefined | accord | 0.025 | 10 | 1 | 0 | \spawn deployable 3456 |
| 3457 | - | None | Undefined | accord | 0.025 | 10 | 1 | 0 | \spawn deployable 3457 |
| 3458 | - | None | Undefined | accord | 0.025 | 10 | 1 | 0 | \spawn deployable 3458 |
| 3459 | _Hallway before destruction | None | Undefined | accord | 0 | 1 | 1 | 0 | \spawn deployable 3459 |
| 3460 | _Destroyed bits of boss room | None | Undefined | accord | 0 | 1 | 1 | 0 | \spawn deployable 3460 |
| 3461 | _Copy of Destroyed bits of boss room | None | Undefined | accord | 0 | 1 | 1 | 0 | \spawn deployable 3461 |
| 3462 | _Copy of Copy of Destroyed bits of boss room | None | Undefined | accord | 0 | 1 | 1 | 0 | \spawn deployable 3462 |
| 3463 | _Destroyed bits of boss room | None | Undefined | accord | 0 | 1 | 1 | 0 | \spawn deployable 3463 |
| 3464 | _Destroyed bits that serve as cover | None | Undefined | accord | 0 | 1 | 1 | 0 | \spawn deployable 3464 |
| 3465 | _Chosen Spike | None | Undefined | - | 0 | 1 | 1 | 0 | \spawn deployable 3465 |
| 3466 | _Chosen Container | None | Undefined | - | 0 | 1 | 0.7 | 0 | \spawn deployable 3466 |
| 3467 | _Chosen Generator | None | Undefined | - | 0 | 1 | 1.5 | 0 | \spawn deployable 3467 |
| 3468 | Black Hills Hideout | None | Undefined | - | 0 | 0 | 1 | 0 | \spawn deployable 3468 |
| 3469 | _Rubble | None | Undefined | accord | 0 | 1 | 0.159 | 0 | \spawn deployable 3469 |
| 3470 | Cage Door | None | Undefined | accord | 0 | 0 | 1.1 | 0 | \spawn deployable 3470 |
| 3471 | Signs of a struggle | None | Interactable Objective | accord | 0 | 0 | 1 | 0 | \spawn deployable 3471 |
| 3472 | - | None | Undefined | accord | 0 | 0 | 1 | 300 | \spawn deployable 3472 |
| 3473 | New Eden Security Transport Vehicle | None | Undefined | accord | 0 | 0 | 1 | 0 | \spawn deployable 3473 |
| 3474 | - | None | Undefined | accord | 0 | 0 | 1 | 0 | \spawn deployable 3474 |
| 3475 | _Kang's Secret Stuff | None | Interactable Objective | accord | 0 | 1 | 1 | 5000 | \spawn deployable 3475 |
| 3476 | Electrical Generator Terminal | None | Undefined | accord | 0 | 50 | 1 | 0 | \spawn deployable 3476 |
| 3477 | Power Terminal | None | Undefined | accord | 0 | 1 | 1.1 | 0 | \spawn deployable 3477 |
| 3478 | Power Terminal | None | Undefined | accord | 0 | 1 | 1.1 | 0 | \spawn deployable 3478 |
| 3479 | Safe House Access | None | Undefined | - | 0 | 0 | 1 | 0 | \spawn deployable 3479 |
| 3480 | Blast Door | None | Target (primary) | accord | 8 | 3000 | 1.45 | 0 | \spawn deployable 3480 |
| 3481 | - | None | Interactable Objective | friendly | 0 | 0 | 1 | 0 | \spawn deployable 3481 |
| 3482 | Accord Truck | None | Undefined | chosen | 0 | 1 | 1 | 0 | \spawn deployable 3482 |
| 3483 | Medical Cache | None | Undefined | accord | 0 | 1 | 1.5 | 0 | \spawn deployable 3483 |
| 3484 | Bandit Explosive | None | Undefined | Black Hills Bandits | 0 | 1 | 1 | 0 | \spawn deployable 3484 |
| 3485 | Black Hills Beacon | None | Undefined | accord | 0 | 1 | 0.75 | 0 | \spawn deployable 3485 |
| 3486 | Hisser Nest | None | Undefined | accord | 0 | 1 | 0.25 | 0 | \spawn deployable 3486 |
| 3487 | - | None | Undefined | accord | 0 | 1 | 1 | 0 | \spawn deployable 3487 |
| 3488 | - | None | Undefined | accord | 0 | 1 | 1 | 0 | \spawn deployable 3488 |
| 3489 | Rebel Beacon | None | Undefined | accord | 0 | 1 | 0.25 | 0 | \spawn deployable 3489 |
| 3490 | Poacher Beacon | None | Undefined | accord | 0 | 1 | 0.5 | 0 | \spawn deployable 3490 |
| 3491 | - | None | Undefined | accord | 0 | 1 | 2 | 0 | \spawn deployable 3491 |
| 3492 | Authorization Terminal | None | Undefined | accord | 0 | 1 | 1 | 0 | \spawn deployable 3492 |
| 3493 | - | None | Undefined | accord | 0 | 1 | 0.4 | 0 | \spawn deployable 3493 |
| 3494 | Authorization Terminal | None | Undefined | accord | 0 | 1 | 1 | 0 | \spawn deployable 3494 |
| 3495 | - | None | Undefined | accord | 0 | 1 | 1.5 | 0 | \spawn deployable 3495 |
| 3496 | - | Turret | Fixed Weapon | accord | 0 | 0 | 1.3 | 0 | \spawn deployable 3496 |
| 3497 | CM5 - All Purpose Invisible Point | None | Undefined | neutral | 0 | 0 | 1 | 0 | \spawn deployable 3497 |
| 3498 | - | None | Undefined | friendly | 0 | 1 | 1 | 0 | \spawn deployable 3498 |
| 3499 | - | None | Undefined | accord | 0 | 1 | 1 | 0 | \spawn deployable 3499 |
| 3500 | Anti Personnel Turret | Turret | Fixed Weapon | accord | 0 | 0 | 1.3 | 0 | \spawn deployable 3500 |
| 3501 | - | None | Undefined | accord | 0 | 1 | 15 | 0 | \spawn deployable 3501 |
| 3502 | - | None | Undefined | accord | 0 | 0 | 1 | 0 | \spawn deployable 3502 |
| 3503 | _PvP Hoverpad | Glider pad | Undefined | accord | 0 | 1 | 1 | 0 | \spawn deployable 3503 |
| 3504 | - | SIN Tower | Target (primary) | - | 64 | 600 | 1 | 0 | \spawn deployable 3504 |
| 3505 | Scaled Copy of _Tutorial Door 8x5 | None | Undefined | accord | 0 | 1 | 1.6 | 0 | \spawn deployable 3505 |
| 3506 | _Alarm | None | Undefined | - | 1 | 1 | 1 | 0 | \spawn deployable 3506 |
| 3508 | Deployable Wall | None | Undefined | accord | 0 | 1 | 1.25 | 0 | \spawn deployable 3508 |
| 3509 | - | None | Undefined | - | 0 | 1 | 5 | 0 | \spawn deployable 3509 |
| 3510 | - | None | Undefined | - | 0 | 0 | 1 | 0 | \spawn deployable 3510 |
| 3511 | _ | Spawner | Deployed Target | chosen | 0 | 0 | 1 | 250 | \spawn deployable 3511 |
| 3512 | _ | None | Undefined | accord | 0 | 1 | 1 | 0 | \spawn deployable 3512 |
| 3513 | _ | None | Undefined | accord | 0 | 1 | 1 | 0 | \spawn deployable 3513 |
| 3514 | - | None | Undefined | accord | 0 | 1 | 1.741 | 0 | \spawn deployable 3514 |
| 3515 | - | Anti-Personnel Turret | Fixed Weapon | accord | 10 | 4000 | 2 | 2500 | \spawn deployable 3515 |
| 3516 | Pulse Generator | Charged Pulse | Undefined | accord | 1 | 1 | 1 | 1000 | \spawn deployable 3516 |
| 3517 | - | None | Deployed Target | chosen | 5 | 2000 | 1 | 250 | \spawn deployable 3517 |
| 3518 | Aranha Brood Mother | None | Target (primary) | gaea | 20 | 0 | 1 | 1500 | \spawn deployable 3518 |
| 3519 | AlarmParticle | None | Undefined | friendly | 0 | 0 | 1 | 0 | \spawn deployable 3519 |
| 3520 | - | None | Undefined | chosen | 450 | 100000 | 3 | 0 | \spawn deployable 3520 |
| 3521 | - | None | Undefined | accord | 0 | 10 | 1 | 0 | \spawn deployable 3521 |
| 3522 | - | None | Undefined | accord | 0 | 1 | 1 | 0 | \spawn deployable 3522 |
| 3523 | - | None | Undefined | accord | 0 | 1 | 1 | 0 | \spawn deployable 3523 |
| 3524 | - | None | Undefined | accord | 0 | 1 | 1 | 0 | \spawn deployable 3524 |
| 3525 | - | None | Undefined | accord | 0 | 1 | 1 | 0 | \spawn deployable 3525 |
| 3526 | - | None | Undefined | - | 1 | 1 | 1 | 0 | \spawn deployable 3526 |
| 3527 | BlockoutKana_Pillar | None | Undefined | accord | 450 | 100000 | 1 | 0 | \spawn deployable 3527 |
| 3528 | - | None | Undefined | accord | 225 | 100000 | 1 | 0 | \spawn deployable 3528 |
| 3529 | - | None | Undefined | accord | 225 | 100000 | 1 | 0 | \spawn deployable 3529 |
| 3530 | - | None | Undefined | accord | 225 | 100000 | 1 | 0 | \spawn deployable 3530 |
| 3531 | - | None | Undefined | accord | 450 | 100000 | 1 | 0 | \spawn deployable 3531 |
| 3532 | - | None | Undefined | accord | 450 | 100000 | 1 | 0 | \spawn deployable 3532 |
| 3533 | - | None | Undefined | accord | 450 | 100000 | 1 | 0 | \spawn deployable 3533 |
| 3534 | - | None | Undefined | accord | 450 | 100000 | 1 | 0 | \spawn deployable 3534 |
| 3535 | - | None | Undefined | accord | 450 | 100000 | 1 | 0 | \spawn deployable 3535 |
| 3536 | Door Protecting Scientists | Turret | Target (primary) | accord | 40 | 2147483647 | 1 | 0 | \spawn deployable 3536 |
| 3537 | Small Jetball Goal | None | Undefined | accord | 0 | 1 | 0.25 | 0 | \spawn deployable 3537 |
| 3538 | - | None | Undefined | accord | 0 | 10 | 1 | 0 | \spawn deployable 3538 |
| 3540 | - | None | Undefined | accord | 0 | 1 | 4.2 | 0 | \spawn deployable 3540 |
| 3541 | - | None | Undefined | accord | 0 | 1 | 1 | 0 | \spawn deployable 3541 |
| 3542 | _ | Mannable Turret | Fixed Weapon | accord | 0 | 6000 | 1 | 0 | \spawn deployable 3542 |
| 3543 | Startled Birds Trigger | None | Undefined | friendly | 0 | 0 | 1 | 0 | \spawn deployable 3543 |
| 3544 | Spotlight | None | Undefined | Rebels | 3 | 500 | 2 | 0 | \spawn deployable 3544 |
| 3545 | - | None | Undefined | accord | 200 | 100000 | 1 | 0 | \spawn deployable 3545 |
| 3546 | - | None | Undefined | accord | 400 | 100000 | 1 | 0 | \spawn deployable 3546 |
| 3547 | [CM5] Elevator Door | None | Undefined | - | 5 | 500 | 1.1 | 0 | \spawn deployable 3547 |
| 3548 | - | None | Undefined | accord | 0 | 1 | 0.2 | 0 | \spawn deployable 3548 |
| 3549 | - | None | Undefined | accord | 0 | 1 | 1 | 0 | \spawn deployable 3549 |
| 3550 | - | None | Undefined | accord | 0 | 1 | 1 | 0 | \spawn deployable 3550 |
| 3551 | Bait the Trap | None | Undefined | accord | 0 | 1 | 1 | 0 | \spawn deployable 3551 |
| 3552 | Turret Operation Switch | None | Undefined | accord | 0 | 50 | 1 | 0 | \spawn deployable 3552 |
| 3553 | Barrel Chute Switch | None | Undefined | accord | 0 | 50 | 1 | 0 | \spawn deployable 3553 |
| 3554 | Crusher Plate Switch | None | Undefined | accord | 0 | 50 | 1 | 0 | \spawn deployable 3554 |
| 3555 | Time Lock Door Terminal | None | Undefined | accord | 0 | 50 | 1 | 0 | \spawn deployable 3555 |
| 3556 | _Proximity | None | Undefined | chosen | 5 | 1950 | 1 | 0 | \spawn deployable 3556 |
| 3557 | - | None | Target (tertiary) | accord | 20000 | 1000000 | 1 | 0 | \spawn deployable 3557 |
| 3558 | Thumper Repair Unit Crate | None | Undefined | accord | 0 | 150 | 1 | 0 | \spawn deployable 3558 |
| 3559 | Button | None | Undefined | accord | 0 | 1 | 0.2 | 0 | \spawn deployable 3559 |
| 3560 | Chosen Dropship | None | Target (primary) | chosen | 50 | 0 | 0.5 | 3500 | \spawn deployable 3560 |
| 3561 | - | None | Undefined | accord | 0 | 1 | 1 | 0 | \spawn deployable 3561 |
| 3562 | Cargo | None | Undefined | accord | 0 | 1 | 1 | 3000 | \spawn deployable 3562 |
| 3563 | Leaves fall from tree | None | Undefined | friendly | 0 | 0 | 1 | 0 | \spawn deployable 3563 |
| 3564 | Mining Facility Gate | None | Undefined | friendly | 0 | 1 | 7 | 0 | \spawn deployable 3564 |
| 3565 | Chosen Energy Core | None | Target (secondary) | chosen | 4 | 8000 | 3 | 0 | \spawn deployable 3565 |
| 3566 | - | None | Undefined | accord | 0 | 1 | 0.55 | 0 | \spawn deployable 3566 |
| 3567 | Egg Sac | None | Undefined | gaea | 2 | 780 | 1.5 | 1000 | \spawn deployable 3567 |
| 3568 | PvP Long Range Jump Pad | None | Undefined | accord | 0 | 0 | 1.25 | 0 | \spawn deployable 3568 |
| 3569 | Anchor | None | Undefined | Reapers | 5 | 780 | 0.5 | 500 | \spawn deployable 3569 |
| 3570 | - | None | Target (primary) | accord | 0 | 1 | 1 | 12000 | \spawn deployable 3570 |
| 3571 | - | None | Target (primary) | accord | 0 | 1 | 1 | 12000 | \spawn deployable 3571 |
| 3572 | - | None | Target (primary) | accord | 0 | 1 | 1 | 12000 | \spawn deployable 3572 |
| 3573 | - | None | Undefined | accord | 0 | 1 | 0.3 | 0 | \spawn deployable 3573 |
| 3574 | Kara Novan - Proximity Mine | Mine | Undefined | accord | 1 | 1 | 2 | 1000 | \spawn deployable 3574 |
| 3575 | - | None | Undefined | - | 1 | 1 | 1 | 0 | \spawn deployable 3575 |
| 3576 | Glider Pad | Glider pad | Undefined | accord | 1 | 1 | 1 | 0 | \spawn deployable 3576 |
| 3577 | Door | None | Undefined | - | 0 | 500 | 1 | 0 | \spawn deployable 3577 |
| 3578 | Door Control | None | Interactable Objective | accord | 0 | 50 | 1 | 0 | \spawn deployable 3578 |
| 3579 | - | None | Interactable Objective | chosen | 0 | 0 | 3 | 0 | \spawn deployable 3579 |
| 3580 | Cryo Pod | None | Undefined | neutral | 0 | 300 | 1 | 0 | \spawn deployable 3580 |
| 3581 | Cryo Pod | None | Undefined | chosen | 0.45 | 300 | 1 | 100 | \spawn deployable 3581 |
| 3582 | Jump Pad | None | Undefined | accord | 0 | 0 | 1.25 | 0 | \spawn deployable 3582 |
| 3583 | Detonation Pack | None | Undefined | accord | 0 | 150 | 0.75 | 0 | \spawn deployable 3583 |
| 3584 | - | None | Undefined | - | 1 | 4800 | 0.6 | 5000 | \spawn deployable 3584 |
| 3585 | Place Mine | None | Undefined | accord | 0 | 1 | 1 | 0 | \spawn deployable 3585 |
| 3586 | - | None | Undefined | - | 1 | 1 | 1 | 0 | \spawn deployable 3586 |
| 3587 | - | None | Undefined | - | 0 | 1 | 1 | 0 | \spawn deployable 3587 |
| 3588 | PvP Heavy Turret - Rank II | Turret | Fixed Weapon | accord | 4 | 1600 | 1.3 | 1000 | \spawn deployable 3588 |
| 3589 | - | None | Undefined | - | 0 | 1 | 1 | 0 | \spawn deployable 3589 |
| 3590 | - | None | Undefined | - | 1 | 1 | 1 | 0 | \spawn deployable 3590 |
| 3591 | - | None | Undefined | accord | 0 | 1 | 1.5 | 0 | \spawn deployable 3591 |
| 3592 | _Invisible Object | None | Undefined | accord | 0 | 1 | 1 | 0 | \spawn deployable 3592 |
| 3593 | Fire Suppression Console | None | Target (tertiary) | accord | 0 | 0 | 1 | 0 | \spawn deployable 3593 |
| 3594 | Non-Lethal Shock Rifle - Interact to Equip | None | Interactable Objective | accord | 0 | 1 | 1 | 0 | \spawn deployable 3594 |
| 3595 | Non-Lethal Concussive Rifle -  Interact to Equip | None | Interactable Objective | accord | 0 | 1 | 1 | 0 | \spawn deployable 3595 |
| 3596 | Chosen Shield Generator | None | Undefined | chosen | 1 | 400 | 3 | 0 | \spawn deployable 3596 |
| 3597 | Roof Hatch Controls | None | Interactable Objective | accord | 0 | 1 | 1 | 0 | \spawn deployable 3597 |
| 3598 | Roof Hatch Controls | None | Interactable Objective | accord | 0 | 1 | 1 | 0 | \spawn deployable 3598 |
| 3599 | Roof Hatch Controls | None | Interactable Objective | accord | 0 | 1 | 1 | 0 | \spawn deployable 3599 |
| 3600 | Roof Hatch Controls | None | Interactable Objective | accord | 0 | 1 | 1 | 0 | \spawn deployable 3600 |
| 3601 | Forceshield Generator | None | Target (primary) | accord | 8 | 3000 | 1.45 | 0 | \spawn deployable 3601 |
| 3602 | UpDraft | None | Undefined | accord | 0 | 1 | 1 | 0 | \spawn deployable 3602 |
| 3603 | - | None | Undefined | - | 0 | 1 | 1 | 0 | \spawn deployable 3603 |
| 3604 | - | None | Undefined | - | 0 | 1 | 1 | 0 | \spawn deployable 3604 |
| 3605 | - | None | Undefined | neutral | 0 | 0 | 1 | 1000 | \spawn deployable 3605 |
| 3606 | - | None | Undefined | - | 0 | 1 | 1 | 0 | \spawn deployable 3606 |
| 3607 | - | None | Undefined | - | 0 | 1 | 1 | 0 | \spawn deployable 3607 |
| 3608 | - | None | Undefined | - | 0 | 1 | 1 | 0 | \spawn deployable 3608 |
| 3609 | - | None | Undefined | - | 0 | 1 | 1 | 0 | \spawn deployable 3609 |
| 3610 | - | None | Undefined | - | 0 | 1 | 1 | 0 | \spawn deployable 3610 |
| 3611 | - | None | Undefined | - | 0 | 1 | 1 | 0 | \spawn deployable 3611 |
| 3612 | Camera Bot | Mine | Undefined | Rebels | 5 | 2000 | 1 | 500 | \spawn deployable 3612 |
| 3613 | Cog | None | Undefined | chosen | 0 | 0 | 0.75 | 0 | \spawn deployable 3613 |
| 3614 | - | None | Deployed Target | accord | 5 | 1 | 0.9 | 0 | \spawn deployable 3614 |
| 3615 | - | None | Undefined | chosen | 15 | 4500 | 1.5 | 0 | \spawn deployable 3615 |
| 3616 | Super Updraft | None | Undefined | accord | 0 | 1 | 1 | 0 | \spawn deployable 3616 |
| 3617 | Electric Field Controls | None | Undefined | accord | 0 | 0 | 1 | 0 | \spawn deployable 3617 |
| 3618 | Roof Controls | None | Undefined | accord | 0 | 1 | 1 | 0 | \spawn deployable 3618 |
| 3619 | Melding Repulsor | None | Undefined | friendly | 20 | 20000 | 1 | 1000 | \spawn deployable 3619 |
| 3620 | NPC_Emote_Utility_01 | None | Rummage | accord | 0 | 0 | 1 | 0 | \spawn deployable 3620 |
| 3621 | NPC_Emote_SitChair_01 | None | Rummage | accord | 0 | 0 | 1 | 0 | \spawn deployable 3621 |
| 3622 | Rhino | None | Undefined | - | 1 | 1 | 1 | 0 | \spawn deployable 3622 |
| 3623 | Omnidyne Bastion Terminal | None | Undefined | accord | 0 | 1 | 1 | 0 | \spawn deployable 3623 |
| 3624 | Astrek Electron Terminal | None | Undefined | accord | 0 | 1 | 1 | 0 | \spawn deployable 3624 |
| 3625 | Astrek Recluse Terminal | None | Undefined | accord | 0 | 1 | 1 | 0 | \spawn deployable 3625 |
| 3626 | Omnidyne Dragonfly Terminal | None | Undefined | accord | 0 | 1 | 1 | 0 | \spawn deployable 3626 |
| 3627 | Astrek Raptor Terminal | None | Undefined | accord | 0 | 1 | 1 | 0 | \spawn deployable 3627 |
| 3628 | Omnidyne Nighthawk Terminal | None | Undefined | accord | 0 | 1 | 1 | 0 | \spawn deployable 3628 |
| 3629 | Astrek Rhino Terminal | None | Undefined | accord | 0 | 1 | 1 | 0 | \spawn deployable 3629 |
| 3630 | Omnidyne Mammoth Terminal | None | Undefined | accord | 0 | 1 | 1 | 0 | \spawn deployable 3630 |
| 3631 | Astrek Firecat Terminal | None | Undefined | accord | 0 | 1 | 1 | 0 | \spawn deployable 3631 |
| 3632 | Omnidyne Tigerclaw Terminal | None | Undefined | accord | 0 | 1 | 1 | 0 | \spawn deployable 3632 |
| 3633 | - | None | Undefined | accord | 0.75 | 1 | 1 | 0 | \spawn deployable 3633 |
| 3634 | - | None | Undefined | accord | 0.75 | 1 | 1 | 0 | \spawn deployable 3634 |
| 3635 | Knockout Gas Dispenser | None | Undefined | accord | 0 | 1 | 1 | 0 | \spawn deployable 3635 |
| 3636 | Breaching Charge Visualizer | None | Undefined | accord | 0 | 150 | 0.5 | 0 | \spawn deployable 3636 |
| 3637 | Breaching Charge | None | Undefined | accord | 0 | 150 | 0.5 | 0 | \spawn deployable 3637 |
| 3638 | Rhino Reactor | None | Undefined | - | 1 | 1 | 1 | 0 | \spawn deployable 3638 |
| 3639 | Cell Bars | None | Undefined | accord | 0 | 0 | 1 | 0 | \spawn deployable 3639 |
| 3640 | Proximity Mines | None | Undefined | - | 0 | 1 | 1 | 0 | \spawn deployable 3640 |
| 3641 | Mammoth Reactor | None | Undefined | - | 1 | 1 | 1 | 0 | \spawn deployable 3641 |
| 3642 | Mammoth | None | Undefined | - | 1 | 1 | 1 | 0 | \spawn deployable 3642 |
| 3643 | Loose Ceiling Hatch | None | Undefined | accord | 0 | 0 | 1 | 0 | \spawn deployable 3643 |
| 3644 | - | None | Undefined | - | 0 | 1 | 1 | 0 | \spawn deployable 3644 |
| 3645 | Security Console | None | Target (tertiary) | accord | 0 | 1 | 1 | 0 | \spawn deployable 3645 |
| 3646 | Glider Pad | Glider pad | Undefined | accord | 0 | 0 | 1 | 0 | \spawn deployable 3646 |
| 3647 | Firecat | None | Undefined | - | 1 | 1 | 1 | 0 | \spawn deployable 3647 |
| 3648 | Tigerclaw | None | Undefined | - | 1 | 1 | 1 | 0 | \spawn deployable 3648 |
| 3649 | - | None | Undefined | accord | 0 | 1 | 1 | 0 | \spawn deployable 3649 |
| 3650 | Bastion | None | Undefined | - | 1 | 1 | 1 | 0 | \spawn deployable 3650 |
| 3651 | Electron | None | Undefined | - | 1 | 1 | 1 | 0 | \spawn deployable 3651 |
| 3652 | Raptor | None | Undefined | - | 1 | 1 | 1 | 0 | \spawn deployable 3652 |
| 3653 | Nighthawk | None | Undefined | - | 1 | 1 | 1 | 0 | \spawn deployable 3653 |
| 3654 | Dragonfly | None | Undefined | - | 1 | 1 | 1 | 0 | \spawn deployable 3654 |
| 3655 | Recluse | None | Undefined | - | 1 | 1 | 1 | 0 | \spawn deployable 3655 |
| 3656 | Firecat Reactor | None | Undefined | - | 1 | 1 | 1 | 0 | \spawn deployable 3656 |
| 3657 | Tigerclaw Reactor | None | Undefined | - | 1 | 1 | 1 | 0 | \spawn deployable 3657 |
| 3658 | - | None | Undefined | accord | 0 | 1 | 1 | 0 | \spawn deployable 3658 |
| 3659 | Firecat | None | Undefined | - | 1 | 1 | 1 | 0 | \spawn deployable 3659 |
| 3660 | Tigerclaw | None | Undefined | - | 1 | 1 | 1 | 0 | \spawn deployable 3660 |
| 3661 | Bastion Reactor | None | Undefined | - | 1 | 1 | 1 | 0 | \spawn deployable 3661 |
| 3662 | Electron Reactor | None | Undefined | - | 1 | 1 | 1 | 0 | \spawn deployable 3662 |
| 3663 | Nighthawk Reactor | None | Undefined | - | 1 | 1 | 1 | 0 | \spawn deployable 3663 |
| 3664 | Raptor Reactor | None | Undefined | - | 1 | 1 | 1 | 0 | \spawn deployable 3664 |
| 3665 | Recluse Reactor | None | Undefined | - | 1 | 1 | 1 | 0 | \spawn deployable 3665 |
| 3666 | Dragonfly Reactor | None | Undefined | - | 1 | 1 | 1 | 0 | \spawn deployable 3666 |
| 3667 | Console | Generic Terminal | Interactable Objective | accord | 0 | 0 | 1 | 0 | \spawn deployable 3667 |
| 3668 | - | None | Undefined | - | 1 | 1 | 1 | 0 | \spawn deployable 3668 |
| 3669 | Dropship | Spawner | Target (primary) | accord | 30 | 6000 | 1 | 5000 | \spawn deployable 3669 |
| 3670 | - | None | Undefined | accord | 1.25 | 500 | 1 | 0 | \spawn deployable 3670 |
| 3671 | Accord Dropship | Spawner | Target (primary) | accord | 30 | 6000 | 1 | 5000 | \spawn deployable 3671 |
| 3672 | Busted Toilet | None | Undefined | accord | 0 | 0 | 1 | 0 | \spawn deployable 3672 |
| 3673 | Toilet | None | Undefined | Rebels | 5 | 0 | 1 | 0 | \spawn deployable 3673 |
| 3674 | Glass Window | None | Undefined | Rebels | 2 | 0 | 2 | 0 | \spawn deployable 3674 |
| 3675 | Copy of Chosen Drop Pod | None | Deployed Target | chosen | 1 | 4800 | 0.8 | 0 | \spawn deployable 3675 |
| 3676 | - | None | Undefined | accord | 0 | 0 | 2.65 | 0 | \spawn deployable 3676 |
| 3677 | Glider Pad | Glider pad | Undefined | accord | 1 | 1 | 1 | 0 | \spawn deployable 3677 |
| 3678 | Reset Target Drones | None | Undefined | accord | 0 | 0 | 1 | 0 | \spawn deployable 3678 |
| 3682 | _ShutterDoor | None | Undefined | - | 0 | 1 | 1 | 0 | \spawn deployable 3682 |
| 3683 | Evacuate Launcher | None | Undefined | accord | 0 | 1 | 1 | 0 | \spawn deployable 3683 |
| 3684 | Arcfield Stabilization Matrix | None | Undefined | accord | 0 | 1 | 0.8 | 0 | \spawn deployable 3684 |
| 3685 | - | None | Undefined | accord | 0 | 1 | 0.7 | 3000 | \spawn deployable 3685 |
| 3686 | _Land Mine | None | Undefined | accord | 0 | 10 | 1 | 0 | \spawn deployable 3686 |
| 3687 | Guard's Key | None | Undefined | accord | 0 | 0 | 1 | 0 | \spawn deployable 3687 |
| 3689 | Teleportal | Arcporter Pylon | Deployed Target | accord | 1 | 1 | 1 | 0 | \spawn deployable 3689 |
| 3690 | SIN Sensor Tech Point | Tech SIN | Undefined | accord | 0 | 1 | 1 | 10000 | \spawn deployable 3690 |
| 3692 | Smokescreen | Mine | Undefined | accord | 1 | 1 | 1 | 0 | \spawn deployable 3692 |
| 3693 | PowerField | Mine | Undefined | accord | 1 | 1 | 1 | 0 | \spawn deployable 3693 |
| 3694 | PowerField with Conduit | Mine | Undefined | accord | 1 | 1 | 1 | 0 | \spawn deployable 3694 |
| 3695 | Melding Repulsor | None | Target (tertiary) | accord | 5000 | 5000 | 1 | 1000 | \spawn deployable 3695 |
| 3696 | - | None | Undefined | melding | 5 | 1 | 1 | 500 | \spawn deployable 3696 |
| 3697 | Storm Kestrel Nest | None | Interactable Objective | accord | 15 | 6000 | 1 | 0 | \spawn deployable 3697 |
| 3698 | Ventilation Ducts | None | Undefined | accord | 0 | 1 | 0.2 | 0 | \spawn deployable 3698 |
| 3699 | Hisser Hive | None | Undefined | accord | 7.5 | 3000 | 1 | 0 | \spawn deployable 3699 |
| 3700 | Chosen Drop Pod | None | Undefined | - | 1 | 4800 | 0.6 | 8000 | \spawn deployable 3700 |
| 3701 | - | None | Undefined | - | 1 | 4800 | 0.6 | 10000 | \spawn deployable 3701 |
| 3702 | Datapad | None | Undefined | accord | 0 | 0 | 2 | 0 | \spawn deployable 3702 |
| 3703 | _Facility Entrance | None | Interactable Objective | - | 1 | 1 | 2.1 | 0 | \spawn deployable 3703 |
| 3704 | Enter Facility | None | Undefined | accord | 0 | 1 | 1 | 0 | \spawn deployable 3704 |
| 3705 | Mining Turret | Turret | Target (primary) | accord | 100 | 10000 | 4 | 4000 | \spawn deployable 3705 |
| 3706 | Drill Unit Crate | None | Undefined | accord | 0 | 150 | 1 | 0 | \spawn deployable 3706 |
| 3707 | - | None | Undefined | accord | 0 | 1 | 0.4 | 3000 | \spawn deployable 3707 |
| 3708 | Battleframe Garage | Forge | Undefined | accord | 0 | 1 | 1 | 0 | \spawn deployable 3708 |
| 3709 | Chosen Air Mine | None | Undefined | chosen | 1 | 400 | 5 | 0 | \spawn deployable 3709 |
| 3710 | Skiver Lair | None | Undefined | gaea | 0 | 0 | 3 | 3000 | \spawn deployable 3710 |
| 3711 | Dead Holmganger | Mine | Undefined | accord | 0 | 1 | 1 | 500 | \spawn deployable 3711 |
| 3712 | Disabled Camera Bot | Mine | Undefined | accord | 0 | 1 | 1 | 500 | \spawn deployable 3712 |
| 3713 | Stolen Goods | None | Healing | accord | 0 | 0 | 2 | 3000 | \spawn deployable 3713 |
| 3714 | Stolen Goods | None | Healing | accord | 0 | 0 | 2 | 3000 | \spawn deployable 3714 |
| 3715 | Human Corpse | Mine | Undefined | accord | 0 | 1 | 1 | 500 | \spawn deployable 3715 |
| 3716 | Weapons Crate | None | Undefined | - | 0 | 1 | 1 | 5000 | \spawn deployable 3716 |
| 3717 | Battleframe Garage | Forge | Undefined | accord | 0 | 1 | 1 | 0 | \spawn deployable 3717 |
| 3718 | - | None | Undefined | accord | 0 | 1 | 1 | 0 | \spawn deployable 3718 |
| 3719 | - | None | Undefined | Rebels | 0 | 1 | 1 | 0 | \spawn deployable 3719 |
| 3720 | _Invis | None | Undefined | - | 0 | 1 | 1 | 0 | \spawn deployable 3720 |
| 3721 | - | None | Deployed Target | accord | 5 | 100 | 0.3 | 0 | \spawn deployable 3721 |
| 3722 | _Roof Piece | None | Undefined | - | 1 | 1 | 1 | 0 | \spawn deployable 3722 |
| 3723 | Progress | None | Undefined | - | 20 | 1000 | 1 | 0 | \spawn deployable 3723 |
| 3724 | _Door | None | Undefined | - | 1 | 1 | 1 | 0 | \spawn deployable 3724 |
| 3725 | _BrokenDoor | None | Undefined | - | 1 | 1 | 1 | 0 | \spawn deployable 3725 |
| 3726 | _Land Mine | None | Undefined | chosen | 10 | 10 | 1 | 0 | \spawn deployable 3726 |
| 3727 | Unearthed - Falling Debris Warning | None | Undefined | accord | 10 | 5000 | 1 | 0 | \spawn deployable 3727 |
| 3728 | - | Tech SIN | Target (primary) | accord | 25000 | 25000 | 2 | 5000 | \spawn deployable 3728 |
| 3729 | Homing Beacon | None | Undefined | accord | 0.0025 | 1 | 0.5 | 0 | \spawn deployable 3729 |
| 3730 | - | None | Undefined | Rebels | 0 | 1 | 1 | 0 | \spawn deployable 3730 |
| 3731 | Triangulator Terminal | None | Work | accord | 0 | 1 | 1 | 0 | \spawn deployable 3731 |
| 3732 | Listening Device | None | Deployed Target | chosen | 0 | 0 | 1 | 1000 | \spawn deployable 3732 |
| 3733 | Mason's Emergency Shield | None | Deployed Target | accord | 40 | 10000 | 1 | 0 | \spawn deployable 3733 |
| 3734 | Immolation Oven | None | Deployed Target | accord | 5 | 1 | 1 | 0 | \spawn deployable 3734 |
| 3735 | Thermal Shield | Shield | Deployed Target | accord | 5 | 1 | 1 | 0 | \spawn deployable 3735 |
| 3736 | Cryogenic Shield | Shield | Deployed Target | accord | 5 | 1 | 1 | 0 | \spawn deployable 3736 |
| 3737 | Chemical Shield | Shield | Deployed Target | accord | 5 | 1 | 1 | 0 | \spawn deployable 3737 |
| 3738 | Melding Shield | Shield | Deployed Target | accord | 5 | 1 | 1 | 0 | \spawn deployable 3738 |
| 3739 | Mine Crate | None | Undefined | accord | 0 | 150 | 0.75 | 0 | \spawn deployable 3739 |
| 3740 | Detonator Placement | None | Undefined | accord | 0 | 150 | 1 | 0 | \spawn deployable 3740 |
| 3741 | _Land Mine | None | Undefined | accord | 0 | 10 | 1 | 0 | \spawn deployable 3741 |
| 3742 | Thumper Part | None | Healing | accord | 0 | 0 | 1 | 3000 | \spawn deployable 3742 |
| 3743 | Thumper Part | None | Healing | accord | 0 | 0 | 1 | 3000 | \spawn deployable 3743 |
| 3744 | Thumper Part | None | Healing | accord | 0 | 0 | 1 | 3000 | \spawn deployable 3744 |
| 3745 | Thumper Part | None | Healing | accord | 0 | 0 | 1 | 3000 | \spawn deployable 3745 |
| 3746 | Suspicious Pile of Trash | None | Healing | accord | 0 | 0 | 1 | 3000 | \spawn deployable 3746 |
| 3747 | Crate of Parts | None | Healing | accord | 0 | 0 | 1 | 3000 | \spawn deployable 3747 |
| 3748 | - | SIN Tower | Target (primary) | accord | 20000 | 20000 | 1 | 1000 | \spawn deployable 3748 |
| 3749 | Turret Emplacement | None | Healing | accord | 0 | 0 | 1 | 3000 | \spawn deployable 3749 |
| 3750 | Weapons Crate | None | Healing | accord | 0 | 0 | 2 | 3000 | \spawn deployable 3750 |
| 3751 | Melding Canister | None | Healing | accord | 0 | 0 | 2 | 3000 | \spawn deployable 3751 |
| 3752 | _Thunderdome | None | Deployed Target | accord | 5 | 1 | 1 | 0 | \spawn deployable 3752 |
| 3753 | _Thunderdome | None | Deployed Target | accord | 5 | 1 | 1 | 0 | \spawn deployable 3753 |
| 3754 | _Thunderdome | None | Deployed Target | accord | 5 | 1 | 1 | 0 | \spawn deployable 3754 |
| 3755 | _Thunderdome | None | Deployed Target | accord | 5 | 1 | 1 | 0 | \spawn deployable 3755 |
| 3756 | Crate of SIN Hacks | None | Undefined | accord | 0 | 0 | 2 | 0 | \spawn deployable 3756 |
| 3757 | SIN Console | None | Target (tertiary) | accord | 0 | 1 | 1 | 0 | \spawn deployable 3757 |
| 3758 | Bandit Truck | None | Interactable Objective | friendly | 0 | 0 | 1 | 0 | \spawn deployable 3758 |
| 3759 | Thermal Wall | Shield | Deployed Target | accord | 1 | 1000 | 1.5 | 0 | \spawn deployable 3759 |
| 3760 | Cryo Wall | Shield | Deployed Target | accord | 1 | 1000 | 1.5 | 0 | \spawn deployable 3760 |
| 3761 | Chemical Wall | Shield | Deployed Target | accord | 1 | 1000 | 1.5 | 0 | \spawn deployable 3761 |
| 3762 | Melding Wall | Shield | Deployed Target | accord | 1 | 1000 | 1.5 | 0 | \spawn deployable 3762 |
| 3763 | - | Spawner | Undefined | accord | 30 | 6000 | 1 | 5000 | \spawn deployable 3763 |
| 3764 | - | None | Undefined | accord | 0 | 1 | 0.7 | 3000 | \spawn deployable 3764 |
| 3765 | Overturned Truck | None | Undefined | friendly | 1 | 1 | 1 | 0 | \spawn deployable 3765 |
| 3766 | Mechanical Door Lock | None | Undefined | accord | 40 | 16000 | 1.5 | 0 | \spawn deployable 3766 |
| 3767 | Chemical Amplifier | None | Undefined | accord | 0 | 0 | 1 | 3000 | \spawn deployable 3767 |
| 3768 | Suspicious Rock | None | Undefined | accord | 0.25 | 1 | 1 | 0 | \spawn deployable 3768 |
| 3769 | Shell Fragment | None | Undefined | accord | 0 | 0 | 1.125 | 0 | \spawn deployable 3769 |
| 3770 | Chewed Up Bracelet | None | Undefined | accord | 20 | 7200 | 4 | 0 | \spawn deployable 3770 |
| 3771 | Wiley's Cache | None | Undefined | accord | 0 | 1 | 1 | 0 | \spawn deployable 3771 |
| 3772 | SIN Imprint | None | Undefined | accord | 0 | 1 | 1 | 0 | \spawn deployable 3772 |
| 3773 | Food Crate | None | Healing | accord | 0 | 0 | 2 | 3000 | \spawn deployable 3773 |
| 3774 | Stealth Device | None | Undefined | - | 1 | 1 | 0.3 | 0 | \spawn deployable 3774 |
| 3775 | Craterdome Thermal Shield | None | Deployed Target | chosen | 25 | 1 | 1 | 0 | \spawn deployable 3775 |
| 3776 | Craterdome Cryo Shield | None | Deployed Target | chosen | 25 | 1 | 1 | 0 | \spawn deployable 3776 |
| 3777 | Craterdome Chemical Shield | None | Deployed Target | chosen | 25 | 1 | 1 | 0 | \spawn deployable 3777 |
| 3778 | Craterdome Melding Shield | None | Deployed Target | chosen | 25 | 1 | 1 | 0 | \spawn deployable 3778 |
| 3779 | _Chosen Dropship | Spawner | Deployed Target | chosen | 0 | 0 | 1 | 250 | \spawn deployable 3779 |
| 3780 | Aranha Pod | None | Undefined | accord | 0 | 1 | 1 | 3000 | \spawn deployable 3780 |
| 3781 | Storage Crate | None | Undefined | accord | 0 | 1 | 1 | 0 | \spawn deployable 3781 |
| 3782 | - | None | Undefined | accord | 0 | 1 | 1 | 0 | \spawn deployable 3782 |
| 3783 | - | Medium Thumper | Target (primary) | accord | 20 | 8000 | 1 | 0 | \spawn deployable 3783 |
| 3784 | Deployable_Tech_Console | None | Undefined | - | 0 | 0 | 1 | 0 | \spawn deployable 3784 |
| 3785 | Mining Turret | Turret | Undefined | accord | 100 | 10000 | 4 | 4000 | \spawn deployable 3785 |
| 3786 | Hydroelectric Generator | None | Undefined | accord | 0 | 0 | 2 | 5000 | \spawn deployable 3786 |
| 3787 | Generator Terminal | None | Undefined | accord | 0 | 0 | 1 | 5000 | \spawn deployable 3787 |
| 3788 | - | None | Undefined | - | 1 | 1 | 1 | 0 | \spawn deployable 3788 |
| 3789 | - | None | Deployed Target | accord | 2.5 | 1 | 1 | 0 | \spawn deployable 3789 |
| 3790 | Micro Melding Shield | Shield | Deployed Target | accord | 5 | 1 | 0.5 | 0 | \spawn deployable 3790 |
| 3791 | Micro Chemical Shield | Shield | Deployed Target | accord | 5 | 1 | 0.5 | 0 | \spawn deployable 3791 |
| 3792 | Micro Thermal Shield | Shield | Deployed Target | accord | 5 | 1 | 0.5 | 0 | \spawn deployable 3792 |
| 3793 | Micro Cryo Shield | Shield | Deployed Target | accord | 5 | 1 | 0.5 | 0 | \spawn deployable 3793 |
| 3794 | - | None | Undefined | - | 0 | 1 | 1 | 0 | \spawn deployable 3794 |
| 3795 | Mike's Trick or Treat Bag | None | Undefined | friendly | 0.1 | 150 | 2 | 0 | \spawn deployable 3795 |
| 3796 | Fireworks - Bunny | None | Undefined | accord | 0 | 1 | 1 | 0 | \spawn deployable 3796 |
| 3797 | - | None | Undefined | - | 1 | 1 | 1 | 0 | \spawn deployable 3797 |
| 3798 | - | None | Undefined | - | 1 | 1 | 1 | 0 | \spawn deployable 3798 |
| 3799 | - | None | Undefined | - | 1 | 1 | 1 | 0 | \spawn deployable 3799 |
| 3800 | - | None | Undefined | - | 1 | 1 | 1 | 0 | \spawn deployable 3800 |
| 3801 | - | None | Undefined | - | 1 | 1 | 1 | 0 | \spawn deployable 3801 |
| 3802 | - | None | Undefined | - | 1 | 1 | 1 | 0 | \spawn deployable 3802 |
| 3803 | - | None | Undefined | - | 1 | 1 | 1 | 0 | \spawn deployable 3803 |
| 3804 | - | None | Undefined | - | 1 | 1 | 1 | 0 | \spawn deployable 3804 |
| 3805 | - | Loadout Station | Undefined | accord | 0 | 0 | 0.7 | 0 | \spawn deployable 3805 |
| 3806 | Facility EMP Generator | None | Target (primary) | Rebels | 5 | 250 | 2 | 0 | \spawn deployable 3806 |
| 3807 | Accord Cargo Container | None | Undefined | accord | 0 | 0 | 1.5 | 0 | \spawn deployable 3807 |
| 3808 | Anti-Air Turret | None | Fixed Weapon | chosen | 0 | 0 | 1 | 2000 | \spawn deployable 3808 |
| 3809 | - | None | Undefined | Rebels | 0 | 1 | 1 | 0 | \spawn deployable 3809 |
| 3810 | _PvP Immolation Oven | None | Deployed Target | accord | 5 | 1 | 1 | 0 | \spawn deployable 3810 |
| 3811 | - | None | Undefined | - | 1 | 4800 | 0.6 | 5000 | \spawn deployable 3811 |
| 3812 | _Mi837_CM5 - Attach Point | None | Undefined | accord | 0 | 1 | 1 | 0 | \spawn deployable 3812 |
| 3813 | Communication Array Generator Terminal | None | Undefined | neutral | 0 | 0 | 1.5 | 0 | \spawn deployable 3813 |
| 3814 | - | None | Undefined | - | 0 | 0 | 1 | 0 | \spawn deployable 3814 |
| 3815 | Chosen Channeling Pillar | None | Undefined | friendly | 0 | 20000 | 1 | 0 | \spawn deployable 3815 |
| 3816 | High Explosive Charge | None | Undefined | accord | 0 | 0 | 1 | 0 | \spawn deployable 3816 |
| 3817 | High Explosive Charge | None | Undefined | accord | 0 | 0 | 1 | 0 | \spawn deployable 3817 |
| 3819 | BWA - Boss Anim Mortar Attack | Mine | Undefined | chosen | 5 | 1 | 1 | 500 | \spawn deployable 3819 |
| 3820 | - | None | Deployed Target | accord | 100 | 1 | 1 | 0 | \spawn deployable 3820 |
| 3821 | Jump Pad | None | Undefined | accord | 0 | 0 | 1.25 | 0 | \spawn deployable 3821 |
| 3822 | Breaker | None | Target (tertiary) | neutral | 0 | 0 | 1 | 0 | \spawn deployable 3822 |
| 3823 | Anti-Armor Turret | Mannable Turret | Target (tertiary) | accord | 500 | 6000 | 0.7 | 0 | \spawn deployable 3823 |
| 3824 | Anti-Mortar Turret | Mannable Turret | Target (tertiary) | accord | 500 | 6000 | 0.7 | 0 | \spawn deployable 3824 |
| 3825 | Power Generator | None | Target (primary) | accord | 8 | 3000 | 1.3 | 0 | \spawn deployable 3825 |
| 3826 | Power Generator | None | Undefined | accord | 8 | 0 | 1.3 | 0 | \spawn deployable 3826 |
| 3834 | - | None | Undefined | - | 1 | 1 | 1 | 0 | \spawn deployable 3834 |
| 3835 | _Medium Jump Pad | None | Undefined | accord | 0 | 0 | 1 | 0 | \spawn deployable 3835 |
| 3836 | Rebreather Crate | None | Target (tertiary) | neutral | 0 | 0 | 1.5 | 0 | \spawn deployable 3836 |
| 3837 | - | None | Undefined | - | 1 | 1 | 0.8 | 0 | \spawn deployable 3837 |
| 3838 | Copy of copy of accelerate door | None | Undefined | - | 1 | 1 | 1.5 | 0 | \spawn deployable 3838 |
| 3839 | Exit Battle Lab | None | Undefined | accord | 0 | 1 | 1 | 0 | \spawn deployable 3839 |
| 3840 | - | Mine | Undefined | accord | 1 | 1 | 1 | 0 | \spawn deployable 3840 |
| 3841 | - | None | Undefined | accord | 0 | 1 | 1 | 0 | \spawn deployable 3841 |
| 3842 | Shutter Door - Scale 3.5 | None | Undefined | - | 0 | 1 | 3.5 | 0 | \spawn deployable 3842 |
| 3843 | - | None | Undefined | accord | 0 | 1 | 1 | 0 | \spawn deployable 3843 |
| 3844 | - | None | Fixed Weapon | Rebels | 0 | 1 | 5 | 0 | \spawn deployable 3844 |
| 3845 | Mega Laser | Anti-Personnel Turret | Fixed Weapon | Rebels | 10 | 4000 | 2 | 2500 | \spawn deployable 3845 |
| 3846 | - | None | Undefined | accord | 0 | 1 | 1 | 0 | \spawn deployable 3846 |
| 3847 | - | None | Undefined | accord | 0 | 1 | 1 | 0 | \spawn deployable 3847 |
| 3848 | - | None | Undefined | accord | 0 | 1 | 1 | 0 | \spawn deployable 3848 |
| 3849 | - | None | Undefined | accord | 0 | 1 | 1 | 0 | \spawn deployable 3849 |
| 3850 | - | None | Undefined | accord | 0 | 1 | 1 | 0 | \spawn deployable 3850 |
| 3851 | - | Shield | Deployed Target | accord | 2 | 1 | 0.5 | 0 | \spawn deployable 3851 |
| 3852 | Copy of copy of accelerate door | None | Undefined | - | 1 | 1 | 1.5 | 0 | \spawn deployable 3852 |
| 3853 | Accord Thumper | None | Target (tertiary) | accord | 60 | 24000 | 1 | 11666 | \spawn deployable 3853 |
| 3854 | _Trooper Transform | None | Play | accord | 0 | 1 | 0.85 | 0 | \spawn deployable 3854 |
| 3855 | - | None | Undefined | neutral | 0 | 0 | 1 | 0 | \spawn deployable 3855 |
| 3856 | Accord Thumper Drop Site | None | Undefined | accord | 60 | 24000 | 1 | 100 | \spawn deployable 3856 |
| 3857 | Mean Beam Machine | Anti-Personnel Turret | Fixed Weapon | Rebels | 10 | 4000 | 2 | 2500 | \spawn deployable 3857 |
| 3858 | Codex | Datapads | Target (primary) | accord | 0 | 1 | 1 | 1000 | \spawn deployable 3858 |
| 3859 | Elevator Button | None | Undefined | accord | 0 | 1 | 0.2 | 0 | \spawn deployable 3859 |
| 3860 | Power Generator | None | Undefined | - | 25 | 10 | 2.45 | 0 | \spawn deployable 3860 |
| 3861 | - | None | Undefined | - | 25 | 10 | 1 | 0 | \spawn deployable 3861 |
| 3862 | Chosen Energy Core | None | Target (primary) | chosen | 4 | 8000 | 3 | 0 | \spawn deployable 3862 |
| 3863 | Rebel AA Turret | Turret | Fixed Weapon | Rebels | 0 | 63000 | 0.7 | 0 | \spawn deployable 3863 |
| 3864 | Energy Field Generator | None | Undefined | Rebels | 1 | 40000 | 0.8 | 500 | \spawn deployable 3864 |
| 3865 | - | None | Undefined | Rebels | 5 | 40000 | 1 | 500 | \spawn deployable 3865 |
| 3866 | - | None | Undefined | Rebels | 5 | 40000 | 1 | 500 | \spawn deployable 3866 |
| 3867 | Power Regulator | None | Target (primary) | Rebels | 10 | 40000 | 2.5 | 500 | \spawn deployable 3867 |
| 3868 | - | None | Undefined | accord | 0 | 40000 | 4 | 500 | \spawn deployable 3868 |
| 3869 | Energy Shield | Shield | Deployed Target | accord | 1 | 1000 | 1 | 0 | \spawn deployable 3869 |
| 3870 | ARES Soldier | None | Undefined | - | 0 | 0 | 1 | 0 | \spawn deployable 3870 |
| 3871 | ARES Soldier | None | Undefined | - | 0 | 0 | 1 | 0 | \spawn deployable 3871 |
| 3872 | ARES Soldier | None | Undefined | - | 0 | 0 | 1 | 0 | \spawn deployable 3872 |
| 3873 | Prison Monitor System | None | Undefined | accord | 0 | 1 | 1 | 0 | \spawn deployable 3873 |
| 3874 | Crystite Chamber | None | Undefined | - | 1 | 1 | 1 | 0 | \spawn deployable 3874 |
| 3875 | - | None | Undefined | accord | 0 | 1 | 3 | 0 | \spawn deployable 3875 |
| 3876 | Defense Vendor Terminal | Glider pad | Undefined | - | 0 | 1 | 1 | 0 | \spawn deployable 3876 |
| 3878 | - | None | Undefined | accord | 1 | 1000 | 1 | 0 | \spawn deployable 3878 |
| 3879 | Ventilation Fan | None | Undefined | - | 0 | 1 | 0.17 | 0 | \spawn deployable 3879 |
| 3880 | Fuse Box | None | Undefined | accord | 0 | 1 | 0.6 | 0 | \spawn deployable 3880 |
| 3881 | Elevator Terminal | None | Undefined | accord | 0 | 1 | 1 | 0 | \spawn deployable 3881 |
| 3882 | - | Turret | Fixed Weapon | chosen | 10 | 4000 | 1.5 | 5000 | \spawn deployable 3882 |
| 3883 | Chosen Power Generator | None | Cover | chosen | 0 | 0 | 3 | 0 | \spawn deployable 3883 |
| 3884 | - | None | Target (primary) | accord | 7 | 2520 | 1 | 5000 | \spawn deployable 3884 |
| 3885 | - | None | Undefined | accord | 0 | 1 | 5 | 3000 | \spawn deployable 3885 |
| 3886 | - | None | Undefined | accord | 0 | 1 | 4 | 3000 | \spawn deployable 3886 |
| 3887 | - | None | Undefined | accord | 0 | 1 | 1 | 0 | \spawn deployable 3887 |
| 3888 | - | None | Undefined | accord | 0 | 1 | 4 | 0 | \spawn deployable 3888 |
| 3889 | Tesla Blaster | Anti-Personnel Turret | Fixed Weapon | Rebels | 10 | 4000 | 2 | 2500 | \spawn deployable 3889 |
| 3890 | - | None | Fixed Weapon | accord | 10 | 3000 | 1.5 | 5000 | \spawn deployable 3890 |
| 3891 | _Mi837_CM5 - Attach Point- MOVING(NEVER SKIPS UPDATES) | None | Undefined | - | 0 | 1 | 1 | 0 | \spawn deployable 3891 |
| 3892 | - | None | Undefined | accord | 0 | 1 | 0.2 | 0 | \spawn deployable 3892 |
| 3893 | Interface Probe | None | Undefined | accord | 0 | 1 | 1 | 0 | \spawn deployable 3893 |
| 3894 | Power Cell | None | Undefined | accord | 0 | 1 | 1 | 0 | \spawn deployable 3894 |
| 3895 | Code Breaker | None | Undefined | accord | 0 | 1 | 1 | 0 | \spawn deployable 3895 |
| 3896 | Detection Inhibitor | None | Undefined | accord | 0 | 1 | 1 | 0 | \spawn deployable 3896 |
| 3897 | Locked Door Terminal | None | Undefined | accord | 0 | 0 | 1 | 0 | \spawn deployable 3897 |
| 3898 | - | None | Undefined | - | 0 | 1 | 1 | 0 | \spawn deployable 3898 |
| 3899 | - | None | Undefined | - | 0 | 1 | 1 | 0 | \spawn deployable 3899 |
| 3900 | Mobile AA | None | Undefined | accord | 0 | 1 | 1 | 0 | \spawn deployable 3900 |
| 3901 | Turret Emplacement | None | Healing | accord | 0 | 0 | 1 | 3000 | \spawn deployable 3901 |
| 3902 | Power Control Console | None | Undefined | - | 0 | 0 | 1 | 0 | \spawn deployable 3902 |
| 3903 | Desecrated Fungus | None | Undefined | accord | 12 | 1 | 1 | 500 | \spawn deployable 3903 |
| 3904 | - | None | Undefined | - | 0 | 0 | 1 | 0 | \spawn deployable 3904 |
| 3905 | Shutter Door Controls | None | Undefined | accord | 0 | 1 | 1 | 0 | \spawn deployable 3905 |
| 3906 | - | None | Undefined | - | 0 | 0 | 1 | 0 | \spawn deployable 3906 |
| 3907 | - | None | Undefined | - | 1 | 1 | 1 | 0 | \spawn deployable 3907 |
| 3908 | Mine | None | Undefined | Rebels | 0.75 | 10 | 1 | 0 | \spawn deployable 3908 |
| 3911 | Elevator | None | Undefined | accord | 0 | 1 | 1 | 0 | \spawn deployable 3911 |
| 3912 | - | None | Undefined | Rebels | 0 | 0 | 2 | 0 | \spawn deployable 3912 |
| 3913 | - | None | Undefined | Rebels | 0 | 0 | 1 | 0 | \spawn deployable 3913 |
| 3914 | - | None | Undefined | accord | 0 | 0 | 1 | 300 | \spawn deployable 3914 |
| 3915 | - | Repair Station | Healing | accord | 0.25 | 100 | 0.7 | 0 | \spawn deployable 3915 |
| 3916 | NPC Healing Generator | Repair Station | Healing | accord | 3.5 | 100 | 1 | 100 | \spawn deployable 3916 |
| 3917 | - | None | Undefined | - | 0 | 1 | 3.7 | 0 | \spawn deployable 3917 |
| 3918 | PvP Evacuate Launcher | None | Undefined | accord | 0 | 1 | 1 | 0 | \spawn deployable 3918 |
| 3919 | Destroyed Mega Laser | None | Fixed Weapon | Rebels | 0 | 1 | 1 | 0 | \spawn deployable 3919 |
| 3920 | Crystite Generator | None | Undefined | - | 1 | 1 | 1 | 0 | \spawn deployable 3920 |
| 3921 | - | None | Undefined | - | 0 | 1 | 0.4 | 0 | \spawn deployable 3921 |
| 3922 | - | None | Undefined | - | 0 | 1 | 1 | 0 | \spawn deployable 3922 |
| 3923 | Control Button | None | Undefined | accord | 0 | 1 | 0.2 | 0 | \spawn deployable 3923 |
| 3924 | Power Generator | None | Interactable Objective | accord | 0 | 0 | 0.5 | 0 | \spawn deployable 3924 |
| 3925 | Crystite Generator | None | Undefined | - | 1 | 1 | 1.5 | 0 | \spawn deployable 3925 |
| 3926 | - | None | Undefined | - | 1 | 1 | 2 | 0 | \spawn deployable 3926 |
| 3927 | - | Anti-Personnel Turret | Fixed Weapon | Rebels | 0 | 4000 | 1 | 2500 | \spawn deployable 3927 |
| 3928 | - | Surface Deposit | Work | friendly | 0 | 150 | 0.5 | 0 | \spawn deployable 3928 |
| 3929 | Gate Crasher - Projectile Firing Invisible Point | None | Undefined | neutral | 0 | 0 | 1 | 0 | \spawn deployable 3929 |
| 3930 | - | None | Undefined | friendly | 0 | 150 | 1 | 0 | \spawn deployable 3930 |
| 3931 | - | Surface Deposit | Work | friendly | 0 | 150 | 0.25 | 0 | \spawn deployable 3931 |
| 3932 | - | Surface Deposit | Work | friendly | 0 | 150 | 0.5 | 0 | \spawn deployable 3932 |
| 3933 | _Lockbox 2 | None | Undefined | accord | 0 | 1 | 3 | 0 | \spawn deployable 3933 |
| 3934 | _Key | None | Undefined | - | 0 | 1 | 1 | 0 | \spawn deployable 3934 |
| 3935 | Accord Cover (Scaled 1.7) Core Mission 4 - Mission 05 - No Exit | None | Undefined | - | 0 | 0 | 1.7 | 0 | \spawn deployable 3935 |
| 3936 | Fireworks - Mooncake | Consumer Fireworks | Undefined | accord | 0 | 1 | 1 | 0 | \spawn deployable 3936 |
| 3938 | - | None | Undefined | accord | 0 | 1 | 1 | 0 | \spawn deployable 3938 |
| 3939 | PvP Gravity Field Grenade - Gravity Sphere | None | Undefined | accord | 0 | 1 | 1 | 0 | \spawn deployable 3939 |
| 3940 | - | None | Undefined | Rebels | 0 | 1 | 3 | 0 | \spawn deployable 3940 |
| 3941 | - | None | Undefined | Rebels | 0 | 0 | 1 | 0 | \spawn deployable 3941 |
| 3942 | - | None | Undefined | Rebels | 0 | 0 | 1 | 0 | \spawn deployable 3942 |
| 3943 | EMP Power Regulation Terminal | None | Undefined | accord | 0 | 50 | 1 | 0 | \spawn deployable 3943 |
| 3944 | - | None | Undefined | accord | 0 | 0 | 1 | 0 | \spawn deployable 3944 |
| 3945 | Biogoo Grenade | None | Undefined | - | 1 | 1 | 1 | 0 | \spawn deployable 3945 |
| 3946 | - | None | Undefined | accord | 0 | 0 | 1 | 0 | \spawn deployable 3946 |
| 3947 | - | None | Target (tertiary) | accord | 0 | 1 | 1.35 | 0 | \spawn deployable 3947 |
| 3948 | - | None | Undefined | - | 0 | 1 | 1 | 0 | \spawn deployable 3948 |
| 3949 | - | None | Undefined | - | 0 | 1 | 1 | 0 | \spawn deployable 3949 |
| 3950 | _Medium Jump Pad | None | Undefined | accord | 0 | 0 | 1 | 0 | \spawn deployable 3950 |
| 3951 | - | Adventurer's Glider Pad | Undefined | accord | 0 | 0 | 1 | 0 | \spawn deployable 3951 |
| 3952 | _Mi751_CM4_M05 - Attach Point | None | Undefined | - | 0 | 1 | 1 | 0 | \spawn deployable 3952 |
| 3953 | Detonation Pack Storage Crate | None | Undefined | accord | 0 | 150 | 0.75 | 0 | \spawn deployable 3953 |
| 3954 | - | None | Undefined | accord | 0 | 1 | 1 | 0 | \spawn deployable 3954 |
| 3955 | - | None | Undefined | accord | 0 | 1 | 1.5 | 0 | \spawn deployable 3955 |
| 3956 | - | None | Undefined | - | 1 | 4800 | 1.2 | 5000 | \spawn deployable 3956 |
| 3957 | - | None | Undefined | - | 1 | 4800 | 1.2 | 8000 | \spawn deployable 3957 |
| 3958 | - | None | Undefined | - | 1 | 4800 | 1.2 | 10000 | \spawn deployable 3958 |
| 3959 | Power Generator | None | Undefined | - | 25 | 10 | 2.45 | 0 | \spawn deployable 3959 |
| 3960 | Chosen Energy Core | None | Interactable Objective | chosen | 0 | 0 | 3 | 0 | \spawn deployable 3960 |
| 3961 | Mine | None | Target (tertiary) | bandit | 0.5 | 10 | 1 | 1000 | \spawn deployable 3961 |
| 3962 | Weapons Crate | None | Undefined | accord | 1 | 1 | 1 | 0 | \spawn deployable 3962 |
| 3963 | - | None | Undefined | chosen | 1 | 500 | 1 | 0 | \spawn deployable 3963 |
| 3964 | - | None | Undefined | accord | 2 | 0 | 1 | 0 | \spawn deployable 3964 |
| 3965 | - | None | Undefined | accord | 0 | 1 | 2 | 0 | \spawn deployable 3965 |
| 3966 | - | None | Undefined | accord | 0 | 1 | 1 | 0 | \spawn deployable 3966 |
| 3967 | - | Spawner | Target (primary) | accord | 30 | 6000 | 1 | 5000 | \spawn deployable 3967 |
| 3968 | - | None | Undefined | accord | 0 | 1 | 0.75 | 0 | \spawn deployable 3968 |
| 3969 | - | None | Undefined | neutral | 2 | 100 | 1 | 0 | \spawn deployable 3969 |
| 3970 | - | None | Undefined | accord | 100 | 40000 | 1 | 13000 | \spawn deployable 3970 |
| 3971 | _Omnidyne-M Fleet Parent Object | None | Undefined | accord | 0 | 100 | 1 | 13000 | \spawn deployable 3971 |
| 3972 | Crate | None | Undefined | accord | 0 | 0 | 2 | 0 | \spawn deployable 3972 |
| 3973 | - | Glider pad | Undefined | accord | 0.75 | 1 | 1 | 0 | \spawn deployable 3973 |
| 3974 | Armored Dropship | None | Undefined | accord | 0 | 1 | 0.7 | 0 | \spawn deployable 3974 |
| 3975 | Omnidyne Canister | None | Undefined | accord | 0 | 1 | 1 | 0 | \spawn deployable 3975 |
| 3976 | NPC Overclocking Station | None | Undefined | accord | 1.25 | 0 | 1 | 0 | \spawn deployable 3976 |
| 3977 | Big Kahuna Glider Pad | Glider pad | Undefined | accord | 0.75 | 1 | 1 | 0 | \spawn deployable 3977 |
| 3978 | Head Honcho Glider Pad | Glider pad | Undefined | accord | 0.75 | 1 | 1 | 0 | \spawn deployable 3978 |
| 3979 | AlarmParticle | None | Undefined | friendly | 0 | 0 | 1 | 0 | \spawn deployable 3979 |
| 3980 | Incinerator | None | Undefined | - | 0 | 1 | 1.25 | 0 | \spawn deployable 3980 |
| 3981 | Arcfolder | Forge | Undefined | accord | 0 | 1 | 1 | 0 | \spawn deployable 3981 |
| 3982 | SIN Anchor | Mine | Undefined | accord | 0 | 0 | 1 | 2000 | \spawn deployable 3982 |
| 3983 | Exit SIN | Forge | Undefined | accord | 0 | 1 | 1 | 0 | \spawn deployable 3983 |
| 3984 | - | None | Undefined | accord | 0 | 1 | 1 | 0 | \spawn deployable 3984 |
| 3985 | Buzzard wreckage | None | Interactable Objective | accord | 0 | 1 | 1 | 0 | \spawn deployable 3985 |
| 3986 | - | None | Deployed Target | accord | 17.5 | 7000 | 1 | 10500 | \spawn deployable 3986 |
| 3987 | Vandilization Location | None | Interactable Objective | - | 0 | 1 | 1 | 0 | \spawn deployable 3987 |
| 3988 | Astrek Prototype Sniper Rifle | None | Interactable Objective | accord | 0 | 1 | 1 | 0 | \spawn deployable 3988 |
| 3989 | Signal Beacon | None | Undefined | accord | 0 | 1 | 1 | 0 | \spawn deployable 3989 |
| 3990 | Crate | None | Undefined | accord | 0 | 0 | 1 | 0 | \spawn deployable 3990 |
| 3991 | Turret Control Panel | None | Undefined | accord | 0 | 1 | 1 | 0 | \spawn deployable 3991 |
| 3992 | Turret Emplacement | None | Healing | accord | 0 | 0 | 1 | 3000 | \spawn deployable 3992 |
| 3993 | Incinerator | None | Undefined | - | 0 | 1 | 1.25 | 0 | \spawn deployable 3993 |
| 3994 | - | None | Undefined | accord | 0.0025 | 1 | 1 | 0 | \spawn deployable 3994 |
| 3995 | Access Terminal | None | Target (tertiary) | accord | 0 | 1 | 1 | 0 | \spawn deployable 3995 |
| 3996 | Sand Computer Access | None | Undefined | accord | 0 | 1 | 1 | 0 | \spawn deployable 3996 |
| 3997 | Corpse | Mine | Undefined | accord | 0 | 1 | 1 | 500 | \spawn deployable 3997 |
| 3998 | Computer Terminal | None | Target (tertiary) | accord | 0 | 1 | 1 | 0 | \spawn deployable 3998 |
| 3999 | Explosive Charge | None | Undefined | accord | 0 | 1 | 1 | 0 | \spawn deployable 3999 |
| 4000 | Omnidyne-M Supply Dropship | Spawner | Target (primary) | accord | 30 | 6000 | 1 | 5000 | \spawn deployable 4000 |
| 4001 | Reactor Controls | None | Target (tertiary) | accord | 0 | 1 | 1 | 0 | \spawn deployable 4001 |
| 4002 | Omnidyne-M Supply Container | None | Undefined | chosen | 1 | 500 | 1 | 2500 | \spawn deployable 4002 |
| 4003 | Crashed MGV | None | Undefined | - | 0 | 1 | 1 | 0 | \spawn deployable 4003 |
| 4004 | Dropship Debris | None | Undefined | - | 0 | 1 | 1 | 0 | \spawn deployable 4004 |
| 4005 | Flight Recorder | None | Undefined | accord | 0 | 1 | 1 | 0 | \spawn deployable 4005 |
| 4006 | Van Laar's Personal Server | None | Target (tertiary) | accord | 0 | 1 | 1 | 0 | \spawn deployable 4006 |
| 4007 | PvP Smokescreen | Mine | Undefined | accord | 1 | 1 | 1 | 0 | \spawn deployable 4007 |
| 4008 | Turret | Turret | Fixed Weapon | bandit | 2 | 1 | 1 | 3000 | \spawn deployable 4008 |
| 4009 | PvP Remote Flashbang | Mine | Undefined | accord | 1 | 1 | 1 | 0 | \spawn deployable 4009 |
| 4010 | Command Console | None | Undefined | accord | 0 | 1 | 1 | 0 | \spawn deployable 4010 |
| 4011 | Proximity Sensor | None | Undefined | accord | 0 | 0 | 1 | 0 | \spawn deployable 4011 |
| 4012 | ARES Console | None | Undefined | accord | 0 | 1 | 1 | 0 | \spawn deployable 4012 |
| 4013 | - | None | Undefined | accord | 0 | 1 | 1 | 2500 | \spawn deployable 4013 |
| 4014 | Tainted Water | None | Interactable Objective | accord | 0 | 0 | 1 | 0 | \spawn deployable 4014 |
| 4015 | Waste Ejection System | None | Undefined | accord | 0 | 50 | 1 | 0 | \spawn deployable 4015 |
| 4016 | Dead Body | Mine | Undefined | accord | 0 | 1 | 1 | 500 | \spawn deployable 4016 |
| 4017 | Critical Care Unit | None | Undefined | accord | 0 | 0 | 0.5 | 0 | \spawn deployable 4017 |
| 4018 | Explosives | None | Undefined | chosen | 1 | 500 | 1 | 0 | \spawn deployable 4018 |
| 4019 | Arcfolder | None | Undefined | - | 0 | 1 | 2.624 | 0 | \spawn deployable 4019 |
| 4020 | Observation Drone | Mine | Undefined | accord | 0 | 1 | 1 | 500 | \spawn deployable 4020 |
| 4021 | Arcfolder | None | Undefined | - | 0 | 1 | 2.624 | 0 | \spawn deployable 4021 |
| 4022 | - | None | Undefined | accord | 0 | 1 | 1 | 0 | \spawn deployable 4022 |
| 4023 | - | Turret | Target (primary) | Rebels | 6000 | 30000 | 1 | 0 | \spawn deployable 4023 |
| 4024 | - | None | Undefined | Rebels | 0 | 40000 | 2.5 | 500 | \spawn deployable 4024 |
| 4025 | Mega Laser | Anti-Personnel Turret | Fixed Weapon | Rebels | 10 | 4000 | 1 | 2500 | \spawn deployable 4025 |
| 4026 | Heavy Turret | None | Fixed Weapon | accord | 13 | 1 | 1.3 | 2500 | \spawn deployable 4026 |
| 4027 | Thermal Scanner | None | Undefined | accord | 0 | 1 | 0.5 | 0 | \spawn deployable 4027 |
| 4028 | Rasper Spawning Ground | None | Undefined | accord | 0 | 1 | 0.5 | 0 | \spawn deployable 4028 |
| 4029 | Emergency Shutter Button | None | Undefined | accord | 0 | 1 | 0.2 | 0 | \spawn deployable 4029 |
| 4030 | PvP Thunderdome | None | Deployed Target | accord | 5 | 1 | 1 | 0 | \spawn deployable 4030 |
| 4031 | _Terminal | None | Undefined | accord | 0 | 1 | 1 | 0 | \spawn deployable 4031 |
| 4032 | Probe Launch Terminal | None | Undefined | accord | 0 | 50 | 1 | 0 | \spawn deployable 4032 |
| 4033 | - | None | Undefined | accord | 0 | 50 | 2 | 0 | \spawn deployable 4033 |
| 4034 | Melding Probe Debris | None | Undefined | accord | 0 | 50 | 2 | 0 | \spawn deployable 4034 |
| 4035 | - | None | Undefined | - | 0 | 1 | 2.846 | 0 | \spawn deployable 4035 |
| 4036 | - | None | Undefined | accord | 0 | 1 | 4 | 0 | \spawn deployable 4036 |
| 4037 | - | None | Undefined | accord | 0 | 1 | 2.4 | 0 | \spawn deployable 4037 |
| 4038 | - | None | Undefined | - | 0 | 1 | 1 | 0 | \spawn deployable 4038 |
| 4039 | Glass Window | None | Undefined | Rebels | 10 | 0 | 4 | 0 | \spawn deployable 4039 |
| 4040 | - | None | Undefined | accord | 0 | 1 | 2 | 0 | \spawn deployable 4040 |
| 4041 | Heavy Turret | Turret | Fixed Weapon | Rebels | 3 | 1 | 1 | 2000 | \spawn deployable 4041 |
| 4042 | Chosen Bomb | None | Undefined | accord | 0 | 50 | 1 | 0 | \spawn deployable 4042 |
| 4043 | Datakey Analyzer | None | Target (tertiary) | accord | 0 | 1 | 1 | 0 | \spawn deployable 4043 |
| 4044 | - | None | Fixed Weapon | neutral | 11 | 4000 | 1.5 | 5000 | \spawn deployable 4044 |
| 4045 | - | None | Undefined | accord | 0 | 1 | 3 | 0 | \spawn deployable 4045 |
| 4046 | - | None | Undefined | accord | 0 | 1 | 3 | 0 | \spawn deployable 4046 |
| 4047 | Copy of  Operation - Base Shutter Door | None | Undefined | accord | 0 | 1 | 3.1 | 0 | \spawn deployable 4047 |
| 4048 | Control Desk | None | Undefined | Rebels | 0 | 0 | 1 | 0 | \spawn deployable 4048 |
| 4049 | Copy of Chosen Barricade (half) - one that doesn't fall to the ground | None | Undefined | neutral | 0 | 1 | 1 | 0 | \spawn deployable 4049 |
| 4050 | copy of Chosen Barricade - full size | None | Undefined | chosen | 0 | 1 | 1 | 0 | \spawn deployable 4050 |
| 4051 | Power Generator | None | Undefined | - | 25 | 10 | 2.245 | 0 | \spawn deployable 4051 |
| 4052 | Power Generator | None | Undefined | - | 25 | 10 | 2.245 | 0 | \spawn deployable 4052 |
| 4053 | Invisible Point | None | Undefined | neutral | 0 | 0 | 1 | 0 | \spawn deployable 4053 |
| 4054 | Proximity Mine | Mine | Undefined | accord | 1 | 1 | 2 | 1000 | \spawn deployable 4054 |
| 4055 | - | None | Undefined | - | 0 | 1 | 2.846 | 0 | \spawn deployable 4055 |
| 4056 | Spotlight Turret | Turret | Fixed Weapon | Rebels | 5 | 1 | 2 | 2000 | \spawn deployable 4056 |
| 4057 | Mini Melding Repulsor | None | Undefined | accord | 0 | 1 | 0.35 | 1000 | \spawn deployable 4057 |
| 4058 | Motion Scanner | None | Undefined | accord | 0 | 1 | 0.5 | 0 | \spawn deployable 4058 |
| 4059 | Chosen Terminal | None | Undefined | accord | 0 | 1 | 1 | 0 | \spawn deployable 4059 |
| 4060 | - | None | Undefined | accord | 0 | 1 | 1 | 0 | \spawn deployable 4060 |
| 4061 | Holding Pen Terminal | None | Undefined | accord | 0 | 1 | 1 | 0 | \spawn deployable 4061 |
| 4062 | _Terminal | None | Undefined | accord | 0 | 1 | 1 | 0 | \spawn deployable 4062 |
| 4063 | - | None | Undefined | accord | 0 | 1 | 1 | 2500 | \spawn deployable 4063 |
| 4064 | Jump Pad Control Terminal | None | Undefined | accord | 0 | 1 | 1 | 0 | \spawn deployable 4064 |
| 4065 | _Terminal | None | Undefined | accord | 0 | 1 | 1 | 0 | \spawn deployable 4065 |
| 4066 | - | None | Undefined | neutral | 0 | 0 | 1 | 0 | \spawn deployable 4066 |
| 4067 | Relic Cache | None | Undefined | accord | 0 | 1 | 1 | 0 | \spawn deployable 4067 |
| 4068 | Large Relic Cache | None | Undefined | accord | 0 | 1 | 2 | 0 | \spawn deployable 4068 |
| 4069 | Relic Hunt Glider Pad | Glider pad | Undefined | accord | 0.75 | 1 | 1 | 0 | \spawn deployable 4069 |
| 4070 | Jump Pad | None | Undefined | accord | 0 | 0 | 1.25 | 0 | \spawn deployable 4070 |
| 4071 | Armored Dropship | None | Undefined | accord | 0 | 0 | 1 | 0 | \spawn deployable 4071 |
| 4074 | _Nian Arena One-Way Dome | None | Undefined | chosen | 0 | 0 | 0.8 | 0 | \spawn deployable 4074 |
| 4075 | Nian Fight Arena | None | Undefined | accord | 0 | 1 | 1 | 0 | \spawn deployable 4075 |
| 4076 | _Nian Dome - No Shoot In | None | Undefined | melding | 0 | 0 | 0.8 | 0 | \spawn deployable 4076 |
| 4077 | Door Control | None | Undefined | accord | 0 | 1 | 2 | 0 | \spawn deployable 4077 |
| 4078 | PvP Teleportal | Arcporter Pylon | Deployed Target | accord | 1 | 1 | 1 | 0 | \spawn deployable 4078 |
| 4079 | Armored Dropship | Spawner | Undefined | accord | 0 | 0 | 1 | 5000 | \spawn deployable 4079 |
| 4080 | Radio Equipment | None | Interactable Objective | accord | 0 | 0 | 1 | 0 | \spawn deployable 4080 |
| 4081 | _[LIVE][NIAN] Bombardment Portal | None | Undefined | melding | 0 | 1 | 1 | 0 | \spawn deployable 4081 |
| 4082 | Vehicle Part | None | Interactable Objective | accord | 0 | 0 | 0.4 | 3000 | \spawn deployable 4082 |
| 4083 | - | Glider pad | Undefined | accord | 0.75 | 1 | 1 | 0 | \spawn deployable 4083 |
| 4084 | Acid Fumes | None | Interactable Objective | accord | 0 | 0 | 1 | 0 | \spawn deployable 4084 |
| 4085 | Place Explosives | None | Interactable Objective | accord | 0 | 0 | 1 | 0 | \spawn deployable 4085 |
| 4086 | _Invisible Point | None | Undefined | neutral | 0 | 0 | 1 | 0 | \spawn deployable 4086 |
| 4087 | Melding Autocannon Crystal Shield | Shield | Undefined | accord | 5 | 1 | 0.3 | 0 | \spawn deployable 4087 |
| 4088 | _Invisible Point | None | Undefined | neutral | 0 | 0 | 1 | 0 | \spawn deployable 4088 |
| 4089 | PvP Healing Generator | Repair Station | Healing | accord | 0.25 | 100 | 1 | 0 | \spawn deployable 4089 |
| 4090 | - | Shield | Target (tertiary) | accord | 5 | 1 | 0.75 | 0 | \spawn deployable 4090 |
| 4091 | _Cryo Barrel | None | Undefined | chosen | 3 | 500 | 1 | 0 | \spawn deployable 4091 |
| 4092 | Security Console | None | Target (tertiary) | accord | 0 | 1 | 1 | 0 | \spawn deployable 4092 |
| 4093 | _Pyro Barrel | None | Undefined | chosen | 3 | 500 | 1 | 0 | \spawn deployable 4093 |
| 4094 | _Photo booth | None | Undefined | accord | 0 | 500 | 1 | 0 | \spawn deployable 4094 |
| 4095 | - | None | Undefined | chosen | 0.65 | 500 | 2 | 0 | \spawn deployable 4095 |
| 4096 | - | None | Undefined | accord | 0 | 0 | 1.439 | 0 | \spawn deployable 4096 |
| 4097 | - | None | Undefined | accord | 0 | 0 | 0.9 | 0 | \spawn deployable 4097 |
| 4098 | - | None | Undefined | accord | 0 | 0 | 0.626 | 0 | \spawn deployable 4098 |
| 4099 | SIN Bridge | None | Interactable Objective | accord | 0 | 0 | 1 | 0 | \spawn deployable 4099 |
| 4100 | - | None | Undefined | accord | 0 | 1 | 1 | 0 | \spawn deployable 4100 |
| 4101 | - | None | Undefined | - | 1 | 1 | 1 | 0 | \spawn deployable 4101 |
| 4102 | _ceiling hatch | None | Undefined | - | 1 | 1 | 4 | 0 | \spawn deployable 4102 |
| 4103 | - | None | Undefined | accord | 0 | 0 | 1.25 | 0 | \spawn deployable 4103 |
| 4104 | ChosenPrisonPodBroken | None | Undefined | - | 0 | 0 | 1 | 0 | \spawn deployable 4104 |
| 4105 | - | None | Undefined | - | 0 | 1 | 1 | 0 | \spawn deployable 4105 |
| 4106 | - | None | Undefined | accord | 0 | 0 | 1.1 | 0 | \spawn deployable 4106 |
| 4107 | - | None | Undefined | - | 0 | 1 | 1.5 | 0 | \spawn deployable 4107 |
| 4108 | - | None | Undefined | accord | 0 | 0 | 1 | 0 | \spawn deployable 4108 |
| 4109 | Shield Generator | None | Undefined | - | 1 | 1 | 0.75 | 0 | \spawn deployable 4109 |
| 4110 | - | None | Undefined | accord | 0 | 1 | 1 | 0 | \spawn deployable 4110 |
| 4111 | _Invisible Point | None | Undefined | neutral | 0 | 0 | 1 | 0 | \spawn deployable 4111 |
| 4112 | Shooty | Turret | Fixed Weapon | chosen | 2 | 1 | 1 | 3000 | \spawn deployable 4112 |
| 4113 | - | None | Undefined | - | 1 | 1 | 5 | 0 | \spawn deployable 4113 |
| 4114 | Reactor Core Temperature Stabilizer | None | Undefined | - | 1 | 1 | 1 | 0 | \spawn deployable 4114 |
| 4115 | Reactor Core Temperature Stabilizer | None | Undefined | - | 1 | 1 | 1 | 0 | \spawn deployable 4115 |
| 4116 | Scorcher Cover | None | Undefined | - | 0 | 0 | 0.5 | 0 | \spawn deployable 4116 |
| 4117 | Scorcher Cover | None | Undefined | - | 0 | 0 | 1 | 0 | \spawn deployable 4117 |
| 4118 | Scorcher Cover | None | Undefined | - | 0 | 0 | 0.075 | 0 | \spawn deployable 4118 |
| 4119 | Scorcher Cover | None | Undefined | - | 0 | 0 | 1 | 0 | \spawn deployable 4119 |
| 4120 | Scorcher Cover | None | Undefined | - | 0 | 0 | 1 | 0 | \spawn deployable 4120 |
| 4121 | - | None | Undefined | - | 0 | 0 | 0.4 | 0 | \spawn deployable 4121 |
| 4122 | - | None | Undefined | - | 0 | 0 | 1 | 0 | \spawn deployable 4122 |
| 4123 | Scorcher Cover | None | Undefined | - | 0 | 0 | 0.5 | 0 | \spawn deployable 4123 |
| 4124 | Scorcher Cover | None | Undefined | - | 0 | 0 | 0.5 | 0 | \spawn deployable 4124 |
| 4125 | Scorcher Cover | None | Undefined | - | 0 | 0 | 1 | 0 | \spawn deployable 4125 |
| 4126 | - | None | Undefined | - | 0 | 0 | 0.4 | 0 | \spawn deployable 4126 |
| 4127 | Laser Drill | None | Undefined | - | 1 | 1 | 0.5 | 0 | \spawn deployable 4127 |
| 4128 | Turret Emplacement | None | Healing | accord | 0 | 0 | 1 | 3000 | \spawn deployable 4128 |
| 4129 | - | None | Undefined | - | 0 | 0 | 0.25 | 0 | \spawn deployable 4129 |
| 4130 | Flare Launcher | None | Undefined | accord | 0 | 1 | 1 | 0 | \spawn deployable 4130 |
| 4131 | Healing Dome Station | Repair Station | Healing | accord | 0 | 500 | 0.5 | 0 | \spawn deployable 4131 |
| 4132 | - | Mannable Turret | Fixed Weapon | accord | 15 | 60000 | 1 | 0 | \spawn deployable 4132 |
| 4133 | Generator | None | Undefined | - | 0 | 0 | 1 | 0 | \spawn deployable 4133 |
| 4134 | Equipment | None | Undefined | - | 0 | 0 | 1 | 0 | \spawn deployable 4134 |
| 4135 | Terminal Screen | None | Undefined | - | 0 | 0 | 0.5 | 0 | \spawn deployable 4135 |
| 4136 | Access Terminal | None | Undefined | - | 0 | 0 | 1 | 0 | \spawn deployable 4136 |
| 4137 | Canopy | None | Undefined | - | 0 | 0 | 1.4 | 0 | \spawn deployable 4137 |
| 4138 | - | None | Undefined | - | 1 | 1 | 1 | 0 | \spawn deployable 4138 |
| 4139 | - | None | Undefined | accord | 0 | 1 | 0.25 | 0 | \spawn deployable 4139 |
| 4140 | Melding Nano Missile Turret | Turret | Fixed Weapon | accord | 2 | 1 | 0.3 | 100 | \spawn deployable 4140 |
| 4141 | - | None | Undefined | accord | 0 | 1 | 0.9 | 0 | \spawn deployable 4141 |
| 4142 | - | Forge | Undefined | accord | 0 | 1 | 1 | 0 | \spawn deployable 4142 |
| 4143 | - | None | Undefined | neutral | 0 | 0 | 1 | 0 | \spawn deployable 4143 |
| 4144 | - | None | Undefined | neutral | 0 | 0 | 1 | 0 | \spawn deployable 4144 |
| 4145 | - | None | Undefined | neutral | 0 | 0 | 1 | 0 | \spawn deployable 4145 |
| 4146 | - | None | Undefined | - | 0 | 0 | 0.3 | 0 | \spawn deployable 4146 |
| 4147 | - | None | Undefined | - | 0 | 0 | 0.3 | 0 | \spawn deployable 4147 |
| 4148 | - | None | Undefined | - | 0 | 0 | 0.3 | 0 | \spawn deployable 4148 |
| 4149 | - | Mannable Turret | Fixed Weapon | accord | 15 | 60000 | 1 | 0 | \spawn deployable 4149 |
| 4150 | Ophanim Chest Cover 1 | None | Undefined | - | 0 | 0 | 1 | 0 | \spawn deployable 4150 |
| 4151 | Ophanim Full Cover 2 | None | Undefined | - | 0 | 0 | 0.75 | 0 | \spawn deployable 4151 |
| 4152 | Ophanim Full Cover 1 | None | Undefined | - | 0 | 0 | 0.75 | 0 | \spawn deployable 4152 |
| 4153 | - | None | Undefined | chosen | 1 | 500 | 1 | 0 | \spawn deployable 4153 |
| 4154 | - | None | Undefined | chosen | 1 | 500 | 1 | 0 | \spawn deployable 4154 |
| 4155 | _flamethrower | Turret | Undefined | neutral | 11 | 4000 | 1.5 | 5000 | \spawn deployable 4155 |
| 4156 | - | None | Undefined | chosen | 0 | 1 | 2.5 | 0 | \spawn deployable 4156 |
| 4157 | - | Mine | Undefined | accord | 6 | 1 | 1 | 500 | \spawn deployable 4157 |
| 4158 | - | None | Undefined | accord | 0 | 1 | 1 | 0 | \spawn deployable 4158 |
| 4159 | - | None | Undefined | accord | 0 | 1 | 1.5 | 0 | \spawn deployable 4159 |
| 4160 | - | None | Undefined | - | 0 | 1 | 1.5 | 0 | \spawn deployable 4160 |
| 4161 | - | None | Undefined | neutral | 0 | 0 | 1 | 0 | \spawn deployable 4161 |
| 4162 | - | None | Undefined | - | 0 | 1 | 2.5 | 0 | \spawn deployable 4162 |
| 4163 | - | None | Undefined | - | 1 | 1 | 0.2 | 0 | \spawn deployable 4163 |
| 4164 | - | None | Undefined | gaea | 150 | 13500 | 2.5 | 0 | \spawn deployable 4164 |
| 4165 | - | None | Undefined | gaea | 1000 | 90000 | 1 | 0 | \spawn deployable 4165 |
| 4166 | - | None | Undefined | gaea | 0 | 10000 | 0.45 | 3000 | \spawn deployable 4166 |
| 4167 | _M17_SOS - Battlelab - Shutter Door (scale 2.0) - Mod_WindowShutt | None | Undefined | accord | 0 | 1 | 2 | 0 | \spawn deployable 4167 |
| 4168 | _M17_SOS - Battlelab - Shutter Door (scale 3) - Mod_WindowShu | None | Undefined | accord | 0 | 1 | 3 | 0 | \spawn deployable 4168 |
| 4169 | _flamethrower controls | None | Undefined | accord | 0 | 1 | 1 | 0 | \spawn deployable 4169 |
| 4170 | - | None | Fixed Weapon | chosen | 0 | 5000 | 0.125 | 3000 | \spawn deployable 4170 |
| 4172 | Ophanim Chest Cover 2 | None | Undefined | - | 0 | 0 | 1 | 0 | \spawn deployable 4172 |
| 4173 | Ophanim Chest Cover 3 | None | Undefined | - | 0 | 0 | 1 | 0 | \spawn deployable 4173 |
| 4174 | Ophanim Full Cover 4 | None | Undefined | - | 0 | 0 | 1 | 0 | \spawn deployable 4174 |
| 4175 | Ophanim Full Cover 4a | None | Undefined | - | 0 | 0 | 0.75 | 0 | \spawn deployable 4175 |
| 4176 | Baneclaw - Gravity Well | None | Undefined | chosen | 0 | 1 | 1 | 0 | \spawn deployable 4176 |
| 4177 | Creature Carcass | None | Undefined | accord | 0 | 1 | 1 | 0 | \spawn deployable 4177 |
| 4178 | _core | None | Undefined | - | 1 | 1 | 0.3 | 0 | \spawn deployable 4178 |
| 4179 | Hidden Button | None | Undefined | accord | 0 | 1 | 0.2 | 0 | \spawn deployable 4179 |
| 4180 | - | None | Undefined | accord | 0 | 1 | 3 | 0 | \spawn deployable 4180 |
| 4181 | _M22_Homecoming - A_DropDoor03 (scale: x) | None | Undefined | - | 5 | 500 | 1.5 | 0 | \spawn deployable 4181 |
| 4182 | - | None | Undefined | accord | 0 | 0 | 0.626 | 0 | \spawn deployable 4182 |
| 4183 | - | None | Undefined | accord | 0 | 0 | 0.9 | 0 | \spawn deployable 4183 |
| 4184 | - | None | Undefined | accord | 0 | 0 | 1.439 | 0 | \spawn deployable 4184 |
| 4185 | Heavy Flak Turret | Mannable Turret | Fixed Weapon | accord | 15 | 8000 | 1 | 0 | \spawn deployable 4185 |
| 4186 | Bomb | None | Undefined | Ophanim | 0 | 10 | 0.45 | 200 | \spawn deployable 4186 |
| 4187 | Battlecruiser Model | None | Undefined | - | 1 | 1 | 1 | 0 | \spawn deployable 4187 |
| 4188 | PvP Bastion - Grounding Pod | Sentinel Pod | Deployed Target | accord | 1 | 400 | 1 | 3000 | \spawn deployable 4188 |
| 4189 | _laser | None | Undefined | accord | 11 | 4000 | 1.5 | 5000 | \spawn deployable 4189 |
| 4190 | Play the Front Nine | None | Undefined | accord | 0 | 1 | 1 | 0 | \spawn deployable 4190 |
| 4191 | - | None | Undefined | accord | 0 | 1 | 1 | 0 | \spawn deployable 4191 |
| 4192 | - | Repair Station | Healing | accord | 1 | 100 | 1 | 0 | \spawn deployable 4192 |
| 4193 | - | None | Undefined | accord | 0 | 1 | 1 | 0 | \spawn deployable 4193 |
| 4194 | _Door | None | Undefined | accord | 0 | 1 | 1.5 | 0 | \spawn deployable 4194 |
| 4195 | - | None | Undefined | - | 1 | 1 | 1 | 0 | \spawn deployable 4195 |
| 4196 | _M22_Homecoming - Scanner Ring | None | Undefined | - | 1 | 1 | 1 | 0 | \spawn deployable 4196 |
| 4197 | Multi Turret PVP Bastion | Multi-Turret | Fixed Weapon | accord | 1 | 1 | 0.9 | 1500 | \spawn deployable 4197 |
| 4198 | _Flame Audio | None | Undefined | - | 1 | 1 | 1 | 0 | \spawn deployable 4198 |
| 4199 | - | None | Undefined | - | 1 | 1 | 2 | 0 | \spawn deployable 4199 |
| 4200 | - | None | Undefined | - | 0 | 1 | 1 | 0 | \spawn deployable 4200 |
| 4201 | Monitoring - U.A.S. Vanguard | None | Undefined | accord | 0 | 1 | 1 | 0 | \spawn deployable 4201 |
| 4202 | - | None | Undefined | accord | 0 | 50 | 1 | 0 | \spawn deployable 4202 |
| 4203 | - | None | Undefined | neutral | 100 | 1 | 0.3 | 0 | \spawn deployable 4203 |
| 4204 | Chosen Containment Pod | None | Undefined | chosen | 0 | 0 | 1.25 | 0 | \spawn deployable 4204 |
| 4205 | Broken Chosen Containment Pod | None | Undefined | accord | 0 | 300 | 1.25 | 0 | \spawn deployable 4205 |
| 4206 | PvP Bastion - Overdrive Turret - Rank II - Engineer | Turret | Fixed Weapon | accord | 4 | 1600 | 1.3 | 1000 | \spawn deployable 4206 |
| 4207 | - | None | Undefined | - | 1 | 1 | 1 | 0 | \spawn deployable 4207 |
| 4208 | - | None | Undefined | - | 0 | 1 | 0.4 | 0 | \spawn deployable 4208 |
| 4209 | - | None | Undefined | accord | 0 | 1 | 1 | 0 | \spawn deployable 4209 |
| 4210 | - | None | Undefined | accord | 0 | 1 | 1 | 0 | \spawn deployable 4210 |
| 4211 | - | None | Undefined | accord | 0 | 1 | 1 | 0 | \spawn deployable 4211 |
| 4212 | - | None | Undefined | accord | 0 | 100 | 1 | 13000 | \spawn deployable 4212 |
| 4213 | - | None | Undefined | accord | 0 | 1 | 1 | 0 | \spawn deployable 4213 |
| 4214 | - | None | Undefined | accord | 0 | 1 | 1 | 0 | \spawn deployable 4214 |
| 4215 | _Door | None | Undefined | - | 1 | 1 | 0.4 | 0 | \spawn deployable 4215 |
| 4216 | - | None | Undefined | - | 1 | 1 | 6.2 | 0 | \spawn deployable 4216 |
| 4217 | _Door | None | Undefined | - | 1 | 1 | 0.6 | 0 | \spawn deployable 4217 |
| 4218 | Accord Dropship | Spawner | Target (primary) | accord | 300 | 6000 | 1 | 5000 | \spawn deployable 4218 |
| 4219 | - | None | Deployed Target | accord | 5 | 1 | 1 | 1 | \spawn deployable 4219 |
| 4220 | - | None | Undefined | accord | 0 | 1 | 1 | 0 | \spawn deployable 4220 |
| 4221 | - | None | Undefined | - | 1 | 1 | 1 | 0 | \spawn deployable 4221 |
| 4222 | Chosen Dropship | None | Target (primary) | chosen | 45 | 0 | 0.2 | 3500 | \spawn deployable 4222 |
| 4223 | - | None | Undefined | - | 0 | 1 | 1 | 0 | \spawn deployable 4223 |
| 4224 | - | None | Undefined | accord | 0 | 1 | 1 | 0 | \spawn deployable 4224 |
| 4225 | - | None | Undefined | - | 0 | 1 | 1 | 0 | \spawn deployable 4225 |
| 4226 | - | None | Undefined | accord | 0 | 1 | 5 | 0 | \spawn deployable 4226 |
| 4227 | Heavy Turret | None | Undefined | accord | 10 | 900 | 3 | 2500 | \spawn deployable 4227 |
| 4228 | - | None | Undefined | neutral | 0 | 1 | 1.5 | 0 | \spawn deployable 4228 |
| 4229 | - | Shield | Target (primary) | accord | 2.5 | 50000 | 1.5 | 0 | \spawn deployable 4229 |
| 4230 | Dual Input Control Terminal | None | Undefined | accord | 0 | 50 | 1 | 0 | \spawn deployable 4230 |
| 4231 | - | None | Undefined | accord | 0 | 1 | 1.5 | 0 | \spawn deployable 4231 |
| 4232 | _[LIVE][NIAN] Timed Portal | None | Undefined | melding | 0 | 1 | 1 | 0 | \spawn deployable 4232 |
| 4233 | Chosen Generator | None | Target (primary) | accord | 100 | 9000 | 2 | 0 | \spawn deployable 4233 |
| 4234 | Broken Chosen Generator | None | Target (primary) | accord | 0 | 9000 | 2 | 0 | \spawn deployable 4234 |
| 4235 | - | None | Undefined | accord | 12 | 1 | 1 | 500 | \spawn deployable 4235 |
| 4236 | - | None | Undefined | chosen | 100 | 9000 | 2.5 | 0 | \spawn deployable 4236 |
| 4237 | - | None | Undefined | accord | 0 | 1 | 1 | 0 | \spawn deployable 4237 |
| 4238 | PvP Supply Station | None | Deployed Target | accord | 0.625 | 1 | 1 | 0 | \spawn deployable 4238 |
| 4239 | - | None | Undefined | accord | 0 | 1 | 1 | 0 | \spawn deployable 4239 |
| 4240 | - | None | Target (primary) | chosen | 80 | 100000 | 1 | 0 | \spawn deployable 4240 |
| 4241 | PvP Engineer Anti-Personnel Turret | Anti-Personnel Turret | Fixed Weapon | accord | 2000 | 2000 | 2 | 1000 | \spawn deployable 4241 |
| 4242 | - | None | Undefined | - | 0 | 0 | 1 | 0 | \spawn deployable 4242 |
| 4243 | - | None | Undefined | - | 1 | 1 | 1 | 0 | \spawn deployable 4243 |
| 4244 | - | None | Undefined | accord | 0 | 1 | 1 | 0 | \spawn deployable 4244 |
| 4245 | - | None | Undefined | neutral | 0 | 1 | 0.4 | 0 | \spawn deployable 4245 |
| 4246 | - | None | Undefined | neutral | 0 | 1 | 1.5 | 0 | \spawn deployable 4246 |
| 4247 | _Invisible Object | None | Undefined | accord | 0 | 1 | 1 | 0 | \spawn deployable 4247 |
| 4248 | - | None | Undefined | - | 1 | 1 | 2 | 0 | \spawn deployable 4248 |
| 4249 | - | None | Fixed Weapon | chosen | 25 | 10000 | 1 | 0 | \spawn deployable 4249 |
| 4250 | - | None | Undefined | accord | 200 | 200 | 1 | 0 | \spawn deployable 4250 |
| 4251 | Blast Door | None | Undefined | chosen | 200 | 20000 | 1.5 | 0 | \spawn deployable 4251 |
| 4252 | - | None | Undefined | accord | 0 | 1 | 1 | 0 | \spawn deployable 4252 |
| 4253 | - | None | Fixed Weapon | chosen | 25 | 10000 | 1 | 0 | \spawn deployable 4253 |
| 4254 | _Nian Shield - No Shoot In | None | Undefined | melding | 0 | 0 | 0.15 | 0 | \spawn deployable 4254 |
| 4255 | _door | None | Undefined | - | 1 | 1 | 1 | 0 | \spawn deployable 4255 |
| 4256 | _Door | None | Undefined | - | 1 | 1 | 0.371 | 0 | \spawn deployable 4256 |
| 4257 | - | None | Undefined | - | 1 | 1 | 1.3 | 0 | \spawn deployable 4257 |
| 4258 | Invis Point Deployable | None | Undefined | accord | 0 | 1 | 1 | 0 | \spawn deployable 4258 |
| 4259 | - | Shield | Target (primary) | accord | 20 | 50000 | 1.5 | 0 | \spawn deployable 4259 |
| 4260 | - | None | Target (tertiary) | accord | 0 | 1 | 1 | 0 | \spawn deployable 4260 |
| 4261 | _Wall | None | Undefined | accord | 0 | 1 | 1.25 | 0 | \spawn deployable 4261 |
| 4262 | _Cover_Crouch_Short | None | Undefined | - | 0 | 0 | 1 | 0 | \spawn deployable 4262 |
| 4263 | Supply Canister | None | Undefined | chosen | 0.65 | 500 | 2 | 0 | \spawn deployable 4263 |
| 4264 | _trap door | None | Undefined | - | 1 | 1 | 0.314 | 0 | \spawn deployable 4264 |
| 4265 | Landing Beacon | None | Undefined | accord | 0 | 0 | 1 | 0 | \spawn deployable 4265 |
| 4266 | _M17_SOS - Battlelab - Shutter Door (scale 4) | None | Undefined | accord | 0 | 1 | 4 | 0 | \spawn deployable 4266 |
| 4267 | - | Anti-Personnel Turret | Fixed Weapon | accord | 2000 | 2000 | 2 | 1000 | \spawn deployable 4267 |
| 4268 | Repulsion Unit Power Supply | None | Undefined | accord | 20 | 20000 | 1 | 1000 | \spawn deployable 4268 |
| 4269 | The Melding | None | Undefined | neutral | 0 | 0 | 1 | 0 | \spawn deployable 4269 |
| 4271 | _M22_Homecoming - Temp CoverFull - Crate09a2 - Large Freight Shi | None | Undefined | - | 1 | 1 | 1.2 | 0 | \spawn deployable 4271 |
| 4272 | Energy Discharge | None | Undefined | neutral | 0 | 0 | 1 | 0 | \spawn deployable 4272 |
| 4273 | Small version of Leaves fall from tree | None | Undefined | friendly | 0 | 0 | 1 | 0 | \spawn deployable 4273 |
| 4275 | - | None | Undefined | - | 1 | 1 | 1 | 0 | \spawn deployable 4275 |
| 4276 | - | None | Undefined | accord | 0 | 1 | 2 | 0 | \spawn deployable 4276 |
| 4277 | - | None | Undefined | accord | 0 | 1 | 1 | 0 | \spawn deployable 4277 |
| 4278 | - | None | Undefined | accord | 0 | 1 | 1 | 0 | \spawn deployable 4278 |
| 4279 | - | None | Undefined | accord | 0 | 1 | 1 | 0 | \spawn deployable 4279 |
| 4280 | Melding Core Power Regulator | SIN Tower | Undefined | accord | 0 | 0 | 1.8 | 0 | \spawn deployable 4280 |
| 4281 | - | None | Undefined | accord | 0 | 1 | 1 | 0 | \spawn deployable 4281 |
| 4282 | - | None | Undefined | accord | 0 | 1 | 1 | 0 | \spawn deployable 4282 |
| 4283 | - | None | Undefined | accord | 0 | 1 | 1 | 0 | \spawn deployable 4283 |
| 4284 | - | None | Undefined | accord | 0 | 1 | 1 | 0 | \spawn deployable 4284 |
| 4285 | - | None | Deployed Target | accord | 5 | 1 | 0.9 | 0 | \spawn deployable 4285 |
| 4286 | - | Mannable Turret | Fixed Weapon | accord | 15 | 6000 | 1 | 0 | \spawn deployable 4286 |
| 4287 | - | None | Undefined | friendly | 0.1 | 150 | 2 | 0 | \spawn deployable 4287 |
| 4288 | PvP Decoy Projector | None | Deployed Target | accord | 2500 | 50000 | 1 | 0 | \spawn deployable 4288 |
| 4289 | PvP SIN Beacon | Mine | Undefined | accord | 5 | 1 | 1 | 500 | \spawn deployable 4289 |
| 4290 | - | None | Fixed Weapon | chosen | 25 | 10000 | 1 | 0 | \spawn deployable 4290 |
| 4291 | - | None | Fixed Weapon | chosen | 25 | 10000 | 1 | 0 | \spawn deployable 4291 |
| 4292 | - | Turret | Target (primary) | chosen | 13 | 1 | 3 | 2500 | \spawn deployable 4292 |
| 4293 | - | None | Fixed Weapon | chosen | 125 | 50000 | 3 | 0 | \spawn deployable 4293 |
| 4294 | PvP Artillery Strike - Beacon | Mine | Undefined | accord | 5 | 1 | 1 | 500 | \spawn deployable 4294 |
| 4295 | - | None | Undefined | accord | 0 | 1 | 1 | 0 | \spawn deployable 4295 |
| 4296 | - | Mine | Undefined | accord | 5 | 1 | 1 | 500 | \spawn deployable 4296 |
| 4297 | - | None | Undefined | chosen | 12.5 | 5000 | 0.33 | 15000 | \spawn deployable 4297 |
| 4298 | Heavy Flak Turret | Mannable Turret | Fixed Weapon | accord | 15 | 8000 | 1 | 0 | \spawn deployable 4298 |
| 4299 | - | None | Undefined | accord | 0 | 1 | 1 | 0 | \spawn deployable 4299 |
| 4300 | Warbringer | None | Undefined | chosen | 10 | 5000 | 0.33 | 15000 | \spawn deployable 4300 |
| 4301 | - | None | Target (tertiary) | accord | 5 | 1 | 1 | 0 | \spawn deployable 4301 |
| 4302 | - | None | Target (tertiary) | accord | 5 | 1 | 1 | 0 | \spawn deployable 4302 |
| 4303 | - | None | Target (tertiary) | accord | 5 | 1 | 1 | 0 | \spawn deployable 4303 |
| 4304 | - | None | Target (tertiary) | accord | 5 | 1 | 1 | 0 | \spawn deployable 4304 |
| 4305 | Invisible Point | None | Undefined | chosen | 0 | 0 | 1 | 0 | \spawn deployable 4305 |
| 4306 | Warbringer | None | Healing | chosen | 12.5 | 5000 | 0.33 | 3000 | \spawn deployable 4306 |
| 4307 | Warbringer | None | Healing | chosen | 0 | 0 | 0.33 | 0 | \spawn deployable 4307 |
| 4308 | Targeting Device | None | Undefined | accord | 0 | 50 | 0.5 | 0 | \spawn deployable 4308 |
| 4309 | Inactive Warbringer | None | Undefined | accord | 0 | 50 | 0.33 | 0 | \spawn deployable 4309 |
| 4310 | PvP Longer Range Jump Pad | None | Undefined | accord | 0 | 0 | 1.25 | 0 | \spawn deployable 4310 |
| 4311 | PvP Medium Jump Pad | None | Undefined | accord | 0 | 0 | 1.25 | 0 | \spawn deployable 4311 |
| 4312 | Antenna | None | Undefined | accord | 0 | 50 | 1 | 0 | \spawn deployable 4312 |
| 4313 | Relay Node | None | Undefined | accord | 0 | 1 | 2 | 0 | \spawn deployable 4313 |
| 4314 | Seismograph | None | Undefined | accord | 0 | 1 | 1 | 0 | \spawn deployable 4314 |
| 4315 | Aranah Cover Full 01 | None | Undefined | - | 0 | 0 | 1 | 0 | \spawn deployable 4315 |
| 4316 | Canopy | None | Undefined | - | 0 | 0 | 1.4 | 0 | \spawn deployable 4316 |
| 4317 | Chosen Thumper | None | Undefined | - | 0 | 0 | 1 | 0 | \spawn deployable 4317 |
| 4318 | - | Shield | Deployed Target | accord | 2.5 | 1000 | 1.5 | 0 | \spawn deployable 4318 |
| 4319 | _M22 - DropCrate01 Breakable(floats) | None | Undefined | chosen | 1 | 500 | 2 | 0 | \spawn deployable 4319 |
| 4320 | - | None | Undefined | accord | 0 | 50 | 5 | 0 | \spawn deployable 4320 |
| 4321 | - | None | Undefined | accord | 0 | 50 | 2 | 0 | \spawn deployable 4321 |
| 4322 | - | None | Undefined | - | 1 | 1 | 1 | 0 | \spawn deployable 4322 |
| 4323 | - | None | Undefined | - | 0 | 1 | 1.1 | 0 | \spawn deployable 4323 |
| 4324 | Fireball | None | Interactable Objective | accord | 0 | 0 | 0.2 | 0 | \spawn deployable 4324 |
| 4325 | - | None | Fixed Weapon | accord | 15 | 8000 | 1 | 0 | \spawn deployable 4325 |
| 4326 | - | Turret | Fixed Weapon | accord | 2 | 1 | 2 | 100 | \spawn deployable 4326 |
| 4327 | - | None | Undefined | accord | 0 | 1 | 1 | 0 | \spawn deployable 4327 |
| 4328 | - | None | Fixed Weapon | Reapers | 0 | 10000 | 0.5 | 0 | \spawn deployable 4328 |
| 4329 | - | None | Undefined | friendly | 0.1 | 150 | 2 | 0 | \spawn deployable 4329 |
| 4330 | - | None | Fixed Weapon | chosen | 25 | 10000 | 1 | 0 | \spawn deployable 4330 |
| 4331 | - | None | Undefined | chosen | 0 | 0 | 1 | 0 | \spawn deployable 4331 |
| 4332 | - | None | Undefined | accord | 0 | 1 | 3 | 0 | \spawn deployable 4332 |
| 4333 | - | None | Undefined | accord | 20 | 1800 | 1.5 | 0 | \spawn deployable 4333 |
| 4334 | Fuel Canister | None | Undefined | chosen | 0 | 0 | 1 | 0 | \spawn deployable 4334 |
| 4335 | Terminal | None | Undefined | accord | 0 | 50 | 1 | 0 | \spawn deployable 4335 |
| 4336 | Data Decrypter | None | Target (tertiary) | accord | 0 | 1 | 1 | 0 | \spawn deployable 4336 |
| 4337 | Array Control Panel | None | Undefined | accord | 0 | 1 | 1 | 0 | \spawn deployable 4337 |
| 4338 | Signal Relay | None | Undefined | accord | 0 | 1 | 1 | 0 | \spawn deployable 4338 |
| 4339 | - | Turret | Fixed Weapon | accord | 2 | 1 | 1 | 100 | \spawn deployable 4339 |
| 4340 | - | None | Undefined | chosen | 200 | 13500 | 2.5 | 0 | \spawn deployable 4340 |
| 4341 | - | Turret | Play | accord | 0 | 1 | 1 | 0 | \spawn deployable 4341 |
| 4342 | Codex | Datapads | Target (primary) | accord | 0 | 1 | 1 | 1000 | \spawn deployable 4342 |
| 4343 | - | None | Undefined | chosen | 0 | 1 | 1 | 0 | \spawn deployable 4343 |
| 4344 | - | Turret | Play | accord | 0 | 1 | 1 | 0 | \spawn deployable 4344 |
| 4345 | - | Turret | Play | accord | 0 | 1 | 1 | 0 | \spawn deployable 4345 |
| 4346 | - | Turret | Play | accord | 0 | 1 | 1 | 0 | \spawn deployable 4346 |
| 4347 | - | Turret | Play | accord | 0 | 1 | 1 | 0 | \spawn deployable 4347 |
| 4348 | - | None | Undefined | - | 1 | 4800 | 1.2 | 5000 | \spawn deployable 4348 |
| 4349 | - | None | Undefined | chosen | 3 | 500 | 1 | 250 | \spawn deployable 4349 |
| 4350 | _Invisible | None | Undefined | friendly | 3 | 500 | 1 | 0 | \spawn deployable 4350 |
| 4351 | Chosen Terminal | None | Undefined | accord | 0 | 1 | 1 | 0 | \spawn deployable 4351 |
| 4352 | - | None | Fixed Weapon | chosen | 25 | 10000 | 1 | 0 | \spawn deployable 4352 |
| 4353 | _M09_Taken - Copy of _Heavy Lift (702) | None | Undefined | accord | 0 | 0 | 1 | 0 | \spawn deployable 4353 |
| 4354 | M09_Taken - Invisible LookAt Point | None | Undefined | accord | 0 | 1 | 1 | 0 | \spawn deployable 4354 |
| 4355 | - | None | Undefined | chosen | 10000000000 | 2147483647 | 2 | 0 | \spawn deployable 4355 |
| 4356 | Engine Part | None | Target (primary) | accord | 0 | 0 | 0.7 | 0 | \spawn deployable 4356 |
| 4357 | _M22_Homecoming - MedHeavy Jump Pad | None | Undefined | accord | 0 | 0 | 1 | 0 | \spawn deployable 4357 |
| 4358 | - | Turret | Fixed Weapon | accord | 2.5 | 1 | 1.3 | 1500 | \spawn deployable 4358 |
| 4359 | Multi Turret | Multi-Turret | Fixed Weapon | accord | 1 | 1 | 0.9 | 1500 | \spawn deployable 4359 |
| 4360 | PVE Heavy Turret - Rank II | Turret | Fixed Weapon | accord | 4 | 1600 | 1.3 | 1000 | \spawn deployable 4360 |
| 4361 | - | None | Undefined | accord | 0 | 0 | 1 | 0 | \spawn deployable 4361 |
| 4362 | - | Anti-Personnel Turret | Fixed Weapon | accord | 2000 | 2000 | 2 | 2500 | \spawn deployable 4362 |
| 4363 | Titanium | None | Target (primary) | accord | 0 | 0 | 2 | 0 | \spawn deployable 4363 |
| 4364 | - | None | Undefined | - | 0 | 1 | 1 | 0 | \spawn deployable 4364 |
| 4365 | Torque Ring Install Point | None | Interactable Objective | accord | 0 | 0 | 1 | 0 | \spawn deployable 4365 |
| 4366 | PVP Heavy Turret - Rank II | Turret | Fixed Weapon | accord | 4 | 1600 | 1.3 | 1000 | \spawn deployable 4366 |
| 4367 | Heavy Turret - Rank I | Turret | Fixed Weapon | accord | 2.5 | 1 | 1.3 | 1500 | \spawn deployable 4367 |
| 4368 | Copy of _Chosen Spike | None | Undefined | - | 0 | 1 | 0.5 | 0 | \spawn deployable 4368 |
| 4369 | Chosen Cache | None | Undefined | accord | 0 | 1 | 0.5 | 500 | \spawn deployable 4369 |
| 4370 | Dropship to Copacabana | None | Undefined | accord | 0 | 1 | 1 | 0 | \spawn deployable 4370 |
| 4371 | Loose Vent Cover | None | Undefined | accord | 3 | 0 | 0.8 | 0 | \spawn deployable 4371 |
| 4372 | - | None | Deployed Target | - | 0 | 1 | 1 | 0 | \spawn deployable 4372 |
| 4373 | - | None | Undefined | friendly | 0 | 1 | 0.7 | 3000 | \spawn deployable 4373 |
| 4374 | Energy Shield | Shield | Deployed Target | accord | 5 | 1 | 1 | 0 | \spawn deployable 4374 |
| 4375 | - | None | Undefined | neutral | 0 | 1 | 0.2 | 0 | \spawn deployable 4375 |
| 4376 | - | None | Undefined | neutral | 0 | 1 | 0.4 | 0 | \spawn deployable 4376 |
| 4377 | - | None | Undefined | - | 0 | 0 | 1 | 0 | \spawn deployable 4377 |
| 4378 | - | None | Undefined | - | 0 | 0 | 0.851 | 0 | \spawn deployable 4378 |
| 4379 | - | None | Undefined | - | 0 | 0 | 1 | 0 | \spawn deployable 4379 |
| 4380 | - | None | Undefined | - | 0 | 0 | 1 | 0 | \spawn deployable 4380 |
| 4381 | - | None | Undefined | accord | 0 | 1 | 1 | 0 | \spawn deployable 4381 |
| 4382 | - | None | Undefined | accord | 0 | 1 | 1 | 0 | \spawn deployable 4382 |
| 4383 | Strifebringer Firework Generator | None | Play | accord | 0 | 1 | 0.25 | 0 | \spawn deployable 4383 |
| 4384 | - | None | Undefined | accord | 0 | 1 | 1.3 | 0 | \spawn deployable 4384 |
| 4385 | - | None | Undefined | - | 0 | 1 | 2 | 0 | \spawn deployable 4385 |
| 4386 | Portable Universal Crafting Knowledge (P.U.C.K.) | Forge | Undefined | accord | 0 | 1 | 1 | 0 | \spawn deployable 4386 |
| 4387 | - | None | Undefined | accord | 5 | 1 | 1 | 0 | \spawn deployable 4387 |
| 4388 | - | None | Undefined | accord | 0 | 1 | 1 | 0 | \spawn deployable 4388 |
| 4389 | - | None | Undefined | accord | 0 | 0 | 30 | 12000 | \spawn deployable 4389 |
| 4391 | - | None | Target (secondary) | chosen | 0 | 0 | 20 | 13000 | \spawn deployable 4391 |
| 4392 | - | None | Fixed Weapon | chosen | 100000 | 0 | 20 | 1000 | \spawn deployable 4392 |

---

Regenerate: `python3 Tools/SdbDump/spawn_reference.py <clientdb.sd2>` - see [README.md](README.md). Related: [../MOBS_AND_NPCS.md](../MOBS_AND_NPCS.md) (mobs grouped by faction, with the anatomy of a monster row), [../SPAWNING_AND_COMBAT.md](../SPAWNING_AND_COMBAT.md) (what happens after the spawn), [../STATIC_DATABASE.md](../STATIC_DATABASE.md) (the file format and the commands).
