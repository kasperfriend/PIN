# Carryables — full spawn reference

Every one of the **105** rows of `dbitems::CarryableObject` that PIN can spawn, with the exact command for each. 71 of them have an English name and can be spawned by name or id; the 34 unnamed rows are real and spawnable, but can only be referenced by id.

> **Generated file** - do not edit by hand. Regenerate with `python3 Tools/SdbDump/spawn_reference.py <clientdb.sd2>` (see [README.md](README.md#5-regenerating-this-folder)).

Decoded from Firefall build **prod-1962**. Index, faction table and CSV notes: [README.md](README.md). How the commands are implemented: [../STATIC_DATABASE.md](../STATIC_DATABASE.md#4-spawning-from-the-database-in-game).

---

## 1. Spawning one of these

```
\spawn carryable <id|name> [<x> <y> <z>]  # chat command (note the backslash)
spawn carryable <id|name> [<x> <y> <z>]   # Admin channel / server console
\sdb carryable <filter> [limit]           # search this table in-game
\sdbinfo carryable <id|name>              # every field of one row
```

- Kind aliases accepted in place of `carryable`: `carryable`, `carryables`, `carry`.
- Spawn path: `EntityManager.SpawnCarryable(typeId, position)`.
- Older typed command: `carryable <id> [<x> <y> <z>]` (admin channel only).
- Omit `<x> <y> <z>` and the entity spawns at your character's position with your orientation; from the server console a position is required.
- Names are matched case-insensitively (exact beats prefix beats substring) and do not need quoting, so multi-word names work: `\spawn carryable Crashed Thumper Part`.

Examples, built from the first rows of this table:

```
\spawn carryable 10                    # Crashed Thumper Part - by id, at your feet
\spawn carryable Crashed Thumper Part  # the same row, by name
\spawn carryable 11 -25.5 118 492      # Ball at an explicit position
\sdbinfo carryable 10                  # every field of that row
\sdb carryable crashed 20              # search this table in-game
```

## 2. Column reference

| column | meaning |
|---|---|
| `id` | `dbitems::CarryableObject.id`. |
| `name` | `localized_name_id` -> `dblocalization::LocalizedText`; the unnamed rows are internal placeholders reachable only by id. |
| `type` | Raw `type` value: 1 = objective/item pickup, 2 = ball/sports prop (the two values that actually occur). |
| `pickup radius` | `pickup_radius` in metres; `thrown radius` is `thrown_pickup_radius`. |
| `picked up by` | `pickup_by_interaction`: `interact` (hold the use key for `interaction_time_ms`) or `touch` (walking over it is enough). |
| `cooldown ms` | `pickup_cooldown` before the same character can pick another one up. |
| `visual record` | `visual_record_id` -> `dbvisualrecords::VisualRecord`, the model used. |
| `spawn command` | The exact chat command. Drop the leading `\` for the Admin channel or the server console. Carryables take a position but no orientation. |

The table in §5 is the readable subset. **Every** column of the SDB row - all 30 of them, plus the resolved names - is in [csv/carryables.csv](csv/carryables.csv).

## 3. Notes

- Carryables have no orientation - `SpawnCarryable` only takes a position.
- Pickup rules (`allow_friendly_pickup`, `allow_hostile_pickup`, `max_per_character`, `is_exclusive`, status effects) are all in the CSV.

## 4. Breakdown

### By type

Rows per `type` value.

| type | rows | named |
|---|---|---|
| 1 | 77 | 59 |
| 2 | 28 | 12 |

## 5. All 105 rows

Sorted by id. `spawn command` is ready to copy into the chat window.

| id | name | type | pickup radius | thrown radius | picked up by | cooldown ms | visual record | spawn command |
|---|---|---|---|---|---|---|---|---|
| 2 | - | 1 | 5 | 5 | touch | 0 | 16130 | \spawn carryable 2 |
| 3 | - | 1 | 5 | 5 | touch | 0 | 16130 | \spawn carryable 3 |
| 5 | - | 1 | 5 | 5 | touch | 0 | 17327 | \spawn carryable 5 |
| 6 | - | 1 | 5 | 5 | touch | 0 | 16130 | \spawn carryable 6 |
| 8 | - | 1 | 0 | 0 | touch | 0 | 10074 | \spawn carryable 8 |
| 9 | - | 1 | 1 | 1 | touch | 500 | 11471 | \spawn carryable 9 |
| 10 | Crashed Thumper Part | 1 | 5 | 5 | interact | 0 | 17683 | \spawn carryable 10 |
| 11 | Ball | 2 | 2 | 2 | touch | 1500 | 17427 | \spawn carryable 11 |
| 21 | SIN Liquid Canister | 1 | 2 | 0 | touch | 1000 | 17596 | \spawn carryable 21 |
| 22 | Red Resonator | 2 | 2 | 2 | interact | 1500 | 17718 | \spawn carryable 22 |
| 23 | White Resonator | 2 | 2 | 2 | interact | 1500 | 17716 | \spawn carryable 23 |
| 24 | Black Resonator | 2 | 2 | 2 | interact | 1500 | 17717 | \spawn carryable 24 |
| 26 | Accord Datapad | 1 | 2 | 0 | touch | 1000 | 18792 | \spawn carryable 26 |
| 27 | Drill Parts | 1 | 2 | 0 | touch | 1000 | 17681 | \spawn carryable 27 |
| 29 | Tainted Crystite | 1 | 2 | 0 | touch | 1000 | 17666 | \spawn carryable 29 |
| 30 | Crystite Core | 1 | 2 | 0 | touch | 1000 | 17666 | \spawn carryable 30 |
| 31 | Chosen Energy Source | 1 | 2 | 0 | touch | 1000 | 17821 | \spawn carryable 31 |
| 32 | Civilian Personal Effects | 1 | 2 | 0 | touch | 1000 | 17586 | \spawn carryable 32 |
| 33 | Medical Supplies | 1 | 2 | 0 | touch | 1000 | 17589 | \spawn carryable 33 |
| 34 | - | 1 | 1 | 1 | interact | 500 | 14323 | \spawn carryable 34 |
| 35 | - | 1 | 1 | 1 | interact | 500 | 14323 | \spawn carryable 35 |
| 36 | - | 1 | 1 | 1 | interact | 500 | 14323 | \spawn carryable 36 |
| 37 | Jetball | 2 | 2 | 2 | touch | 1500 | 17427 | \spawn carryable 37 |
| 38 | Explosives | 1 | 1 | 1 | interact | 500 | 17034 | \spawn carryable 38 |
| 39 | Thumper Repair Unit | 1 | 2 | 0 | interact | 1000 | 0 | \spawn carryable 39 |
| 40 | Disposable Jetball | 2 | 2 | 2 | touch | 1500 | 17427 | \spawn carryable 40 |
| 141 | Harvester Part | 1 | 2 | 2 | interact | 1500 | 17683 | \spawn carryable 141 |
| 142 | Bandit Datapad | 1 | 2 | 0 | interact | 1000 | 16927 | \spawn carryable 142 |
| 144 | Bridge Parts | 1 | 1 | 1 | touch | 500 | 17682 | \spawn carryable 144 |
| 145 | Server Key A | 1 | 0 | 5 | interact | 0 | 18649 | \spawn carryable 145 |
| 146 | Server Key B | 1 | 0 | 5 | interact | 0 | 18653 | \spawn carryable 146 |
| 147 | Server Key C | 1 | 0 | 5 | interact | 0 | 18656 | \spawn carryable 147 |
| 151 | - | 1 | 2 | 0 | interact | 1000 | 0 | \spawn carryable 151 |
| 152 | Biotoxin Sample | 1 | 2 | 0 | interact | 1000 | 17821 | \spawn carryable 152 |
| 154 | - | 2 | 2 | 2 | interact | 1500 | 19739 | \spawn carryable 154 |
| 155 | Anti Personnel Turret | 2 | 2 | 2 | touch | 1500 | 19739 | \spawn carryable 155 |
| 156 | Accord Gate Key | 1 | 2 | 0 | interact | 1000 | 16927 | \spawn carryable 156 |
| 157 | BUD | 2 | 3 | 3 | interact | 1500 | 18943 | \spawn carryable 157 |
| 158 | Delirium Engine Core | 1 | 2 | 0 | interact | 1000 | 17821 | \spawn carryable 158 |
| 159 | The One-of-Many Ring | 1 | 2 | 2 | interact | 2000 | 19733 | \spawn carryable 159 |
| 160 | - | 2 | 2 | 2 | touch | 1500 | 17427 | \spawn carryable 160 |
| 161 | Headless Horseman's Head | 2 | 2 | 2 | touch | 1500 | 19992 | \spawn carryable 161 |
| 162 | Coolant | 1 | 2 | 0 | interact | 1000 | 17596 | \spawn carryable 162 |
| 163 | Present | 1 | 2 | 0 | interact | 2000 | 17077 | \spawn carryable 163 |
| 164 | Nutrepaste | 1 | 2 | 0 | interact | 1000 | 17596 | \spawn carryable 164 |
| 165 | Green Crystal | 1 | 2 | 0 | interact | 1000 | 17596 | \spawn carryable 165 |
| 166 | Red Crystal | 1 | 2 | 0 | interact | 1000 | 17596 | \spawn carryable 166 |
| 167 | Yellow Crystal | 1 | 2 | 0 | interact | 1000 | 17596 | \spawn carryable 167 |
| 168 | Datapad | 1 | 2 | 0 | interact | 1000 | 18792 | \spawn carryable 168 |
| 169 | Resonance Accelerator | 1 | 2 | 0 | interact | 1000 | 17683 | \spawn carryable 169 |
| 170 | Repulsor Parts | 1 | 1 | 1 | touch | 1500 | 18468 | \spawn carryable 170 |
| 171 | - | 2 | 2 | 2 | interact | 1500 | 19739 | \spawn carryable 171 |
| 172 | - | 2 | 2 | 2 | touch | 1500 | 19739 | \spawn carryable 172 |
| 173 | - | 2 | 2 | 2 | touch | 1500 | 19739 | \spawn carryable 173 |
| 174 | - | 2 | 2 | 2 | interact | 1500 | 19739 | \spawn carryable 174 |
| 175 | Cargo | 1 | 1 | 2 | interact | 1500 | 13688 | \spawn carryable 175 |
| 176 | Cargo | 1 | 1 | 2 | interact | 1500 | 18040 | \spawn carryable 176 |
| 177 | Cargo | 1 | 1 | 2 | interact | 1500 | 18468 | \spawn carryable 177 |
| 178 | Scrambler Grenade | 1 | 3 | 2 | touch | 1500 | 18614 | \spawn carryable 178 |
| 179 | - | 2 | 2 | 2 | touch | 1500 | 17718 | \spawn carryable 179 |
| 180 | Accord Weapon Supplies | 2 | 2 | 2 | interact | 1500 | 20413 | \spawn carryable 180 |
| 181 | Door Access Keycard | 1 | 2 | 0 | touch | 1000 | 18792 | \spawn carryable 181 |
| 182 | - | 1 | 2 | 0 | touch | 1000 | 13685 | \spawn carryable 182 |
| 183 | - | 1 | 5 | 5 | interact | 0 | 17683 | \spawn carryable 183 |
| 184 | - | 1 | 5 | 5 | interact | 1500 | 20525 | \spawn carryable 184 |
| 185 | Crashed Sleigh Part | 1 | 5 | 5 | interact | 3000 | 17683 | \spawn carryable 185 |
| 186 | - | 2 | 2 | 2 | touch | 1500 | 19739 | \spawn carryable 186 |
| 187 | - | 2 | 2 | 2 | touch | 1500 | 19739 | \spawn carryable 187 |
| 188 | - | 2 | 2 | 2 | touch | 1500 | 19739 | \spawn carryable 188 |
| 189 | Nautilus Bait | 1 | 2 | 0 | touch | 1000 | 19174 | \spawn carryable 189 |
| 190 | - | 1 | 2 | 0 | interact | 1000 | 20321 | \spawn carryable 190 |
| 191 | Explosive Charge | 1 | 3 | 2 | touch | 1500 | 18614 | \spawn carryable 191 |
| 192 | - | 2 | 2 | 2 | touch | 1500 | 17427 | \spawn carryable 192 |
| 193 | Jetball | 2 | 4 | 4 | touch | 250 | 17427 | \spawn carryable 193 |
| 194 | Keycard | 1 | 2 | 0 | touch | 1000 | 18792 | \spawn carryable 194 |
| 195 | - | 1 | 2 | 0 | touch | 1000 | 18614 | \spawn carryable 195 |
| 196 | Disarmed Proximity Mine | 1 | 2 | 0 | touch | 1000 | 18614 | \spawn carryable 196 |
| 197 | Disarmed Proximity Mine | 1 | 2 | 0 | interact | 1000 | 18614 | \spawn carryable 197 |
| 198 | Generator Repair Parts | 1 | 1 | 1 | interact | 500 | 17682 | \spawn carryable 198 |
| 199 | Tissue Sample | 1 | 2 | 1 | touch | 250 | 19174 | \spawn carryable 199 |
| 200 | Poison Vial | 1 | 2 | 1 | touch | 250 | 17565 | \spawn carryable 200 |
| 201 | Relic Container | 1 | 2 | 1 | touch | 250 | 14886 | \spawn carryable 201 |
| 202 | Encryption Codes | 1 | 2 | 0 | touch | 250 | 18792 | \spawn carryable 202 |
| 203 | Datacrypt | 1 | 2 | 1 | touch | 250 | 17761 | \spawn carryable 203 |
| 204 | Box of Gadgets | 1 | 2 | 1 | touch | 250 | 10392 | \spawn carryable 204 |
| 205 | Box of Supplies | 1 | 2 | 1 | touch | 250 | 13754 | \spawn carryable 205 |
| 206 | Chosen Implant | 1 | 2 | 1 | touch | 250 | 17821 | \spawn carryable 206 |
| 207 | Drone Component | 1 | 2 | 1 | touch | 250 | 20918 | \spawn carryable 207 |
| 208 | Datakey | 1 | 2 | 0 | touch | 250 | 18792 | \spawn carryable 208 |
| 209 | Chosen Transmitter | 1 | 2 | 0 | touch | 250 | 18468 | \spawn carryable 209 |
| 210 | Stolen Crystite | 1 | 2 | 1 | touch | 250 | 14886 | \spawn carryable 210 |
| 211 | Torque Ring | 1 | 2 | 1 | touch | 250 | 19208 | \spawn carryable 211 |
| 212 | Weapons Crate | 1 | 2 | 1 | touch | 250 | 13754 | \spawn carryable 212 |
| 213 | Ball | 2 | 2 | 2 | touch | 1500 | 17427 | \spawn carryable 213 |
| 214 | Chosen Tech | 1 | 2 | 1 | touch | 250 | 20525 | \spawn carryable 214 |
| 215 | SIN Implant | 1 | 2 | 1 | touch | 250 | 18792 | \spawn carryable 215 |
| 216 | Bandit Laundry | 1 | 2 | 1 | touch | 250 | 17586 | \spawn carryable 216 |
| 217 | - | 1 | 2 | 0 | touch | 1000 | 17426 | \spawn carryable 217 |
| 218 | - | 1 | 2 | 0 | touch | 1000 | 17426 | \spawn carryable 218 |
| 220 | - | 1 | 1 | 1 | touch | 500 | 17682 | \spawn carryable 220 |
| 221 | - | 2 | 2 | 2 | touch | 1500 | 17718 | \spawn carryable 221 |
| 222 | - | 2 | 2 | 2 | touch | 1500 | 17716 | \spawn carryable 222 |
| 223 | - | 2 | 2 | 2 | touch | 1500 | 17717 | \spawn carryable 223 |
| 224 | - | 2 | 2 | 2 | touch | 1500 | 17717 | \spawn carryable 224 |
| 225 | - | 2 | 2 | 2 | touch | 1500 | 17717 | \spawn carryable 225 |

---

Regenerate: `python3 Tools/SdbDump/spawn_reference.py <clientdb.sd2>` - see [README.md](README.md). Related: [../MOBS_AND_NPCS.md](../MOBS_AND_NPCS.md) (mobs grouped by faction, with the anatomy of a monster row), [../SPAWNING_AND_COMBAT.md](../SPAWNING_AND_COMBAT.md) (what happens after the spawn), [../STATIC_DATABASE.md](../STATIC_DATABASE.md) (the file format and the commands).
