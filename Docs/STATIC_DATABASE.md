# The Static Database (`clientdb.sd2`)

Nearly everything Firefall knows about itself — every mob, deployable,
vehicle, weapon, ability, status effect and string — lives in a single
encrypted, compressed file: the **static database** (`.sd2`), shipped with the
client at `Firefall/system/db/clientdb.sd2`. PIN loads it at startup and never
writes to it.

This document explains what is inside that file, how much of it PIN currently
reads, and how to **spawn things straight out of it** with the in-game
`spawn` / `sdb` / `sdbinfo` commands.

Related reading:

- [MOBS_AND_NPCS.md](MOBS_AND_NPCS.md) — the full mob/NPC catalog (names, ids, factions)
- [SPAWNING_AND_COMBAT.md](SPAWNING_AND_COMBAT.md) — how a spawned entity is replicated and fights
- [CHARACTERS_AND_BATTLEFRAMES.md](CHARACTERS_AND_BATTLEFRAMES.md) — player-side `characters.json`

> All numbers below come from Firefall build **prod-1962**, decoded with
> `Tools/SdbDump` (see §6). A split copy of that file lives in
> `Tools/clientdb.zip.001` / `.002`.

---

## 1. File format in one page

| Part | What it is |
|------|------------|
| Header (128 bytes) | Magic, file version (`12`), memory version (`1002`), flags, patch string (`prod-1962`), table count, pool offset. |
| Payload obfuscation | The payload is XOR'd with a Mersenne-Twister stream seeded from the patch string. |
| Compression | The de-obfuscated payload is raw deflate. |
| Memory image | A verbatim dump of the client's in-memory table layout: table descriptors, field descriptors, fixed-stride row data, nullable bitfields. |
| String/blob pool | MT-encrypted pool at the end; strings and variable-length blobs are pool offsets in the row data. |

Two consequences matter in practice:

1. **Names are hashes.** Table and column names are stored only as `FFnv32`
   hashes. Both PIN and `Tools/SdbDump` recover readable names by hashing
   *candidate* names (PIN's `LoadStaticDB<T>("dbcharacter::Monster")` calls and
   the C# record property names) and matching hashes. A table whose name nobody
   guessed shows up as `0xDEADBEEF`.
2. **Rows have no text.** A monster row has `localized_name_id`, not a name.
   Display names come from `dblocalization::LocalizedText`.

PIN's runtime path is `StaticDBLoader` -> FauFau's `StaticDB` parser ->
`SDBInterface` dictionaries. `Tools/SdbDump/sdb_dump.py` is a dependency-free
Python port of exactly that logic, so offline dumps and the running server
agree.

## 2. What PIN reads today

Generated with `python3 Tools/SdbDump/sdb_dump.py coverage clientdb.sd2`:

```
tables in file : 575
identified     : 255      (names recovered by hashing candidates)
loaded by PIN  : 240
unidentified   : 320      (name unknown; content still decodable by hash)
rows           : 814,339 of 1,523,277 (53.5%) in PIN-loaded tables
```

| Schema | Tables loaded / identified | Rows |
|--------|---------------------------|------|
| `apt` (aptitude/abilities) | 59 / 59 | 237,835 |
| `aptfs` (aptitude command defs) | 121 / 121 | 31,027 |
| `dbcharacter` | 17 / 31 | 32,247 |
| `dbencounterdata` | 2 / 2 | 1,002 |
| `dbitems` | 23 / 23 | 341,569 |
| `dblocalization` | 1 / 2 | 182,958 |
| `dbphysicsmaterials` | 1 / 1 | 49 |
| `dbvisualrecords` | 2 / 2 | 12,509 |
| `dbzonemetadata` | 1 / 1 | 39 |
| `vcs` (vehicles) | 13 / 13 | 3,447 |

**Identified but still unused by PIN** — the obvious next implementation
targets, all mob-related:

| Table | Rows | What it would give us |
|-------|------|-----------------------|
| `dbcharacter::MonsterVisualOption` | 15,230 | Random visual variants per monster (the `visual_options_id` PIN currently TODOs in `CharacterEntity.LoadMonster`). |
| `dbcharacter::MonsterVisualOptions` | 153 | The variant set headers. |
| `dbcharacter::MonsterMood` | 2,268 | Ambient mood/animation sets for idle NPCs. |
| `dbcharacter::MonsterMoodName` | 6 | Mood name lookup. |
| `dbcharacter::MonsterItemTags` | 1,032 | Item tags used by loot rolls. |
| `dbcharacter::MonsterTitle` | 419 | Titles shown above an NPC's name. |
| `dbcharacter::MonsterAttributeRange` | 167 | Per-level attribute curves for NPCs. |
| `dbcharacter::TurretWeapon` | 149 | The guns a `dbcharacter::Turret` fires. |
| `dbcharacter::VoiceSet` | 463 | NPC voice sets (`voice_set` column). |
| `dbcharacter::EmoteRecord` | 382 | Emote definitions. |
| `dbcharacter::FactionGroup(Members)` | 97 / 146 | Faction groupings above the flat faction list. |
| `dbcharacter::XPRewardType` | 99 | Kill reward types (`xpreward_type`). |
| `dbcharacter::Head` | 67 | Head visual records. |
| `dblocalization::UITextMap` | 7,665 | UI string keys. |

The remaining 320 tables are decodable but nobody has guessed their names yet;
`sdb_dump.py dump clientdb.sd2 0xC79FA24C` still works on them.

### 2.1 New in this change

Two tables were previously listed in the docs but not actually loaded; PIN now
reads both:

| Table | Rows | Exposed as |
|-------|------|-----------|
| `dblocalization::LocalizedText` | 175,293 | `SDBInterface.GetLocalizedText(id)` / `GetLocalizedString(id)` |
| `dbcharacter::MonsterScaling` | 80 | `SDBInterface.GetMonsterScaling(level)` |

Localization is what makes the new commands able to say *"Aranha Queen"*
instead of *"2435"*.

## 3. The spawnable catalog

Five SDB tables map directly onto an `EntityManager.Spawn*` method. PIN wraps
them in `StaticDB/SDBCatalog.cs`:

| Kind | Table | Rows | Named | Spawn path |
|------|-------|------|-------|-----------|
| `monster` | `dbcharacter::Monster` | 3,109 | 1,772 | `EntityManager.SpawnCharacter` |
| `deployable` | `dbcharacter::Deployable` | 3,902 | 2,088 | `EntityManager.SpawnDeployable` |
| `vehicle` | `vcs::VehicleInfo` | 173 | 173 | `EntityManager.SpawnVehicle` |
| `carryable` | `dbitems::CarryableObject` | 105 | 71 | `EntityManager.SpawnCarryable` |
| `turret` | `dbcharacter::Turret` | 107 | 107 | `EntityManager.SpawnTurret` (child entity) |

"Named" = the row's `localized_name_id` resolves to non-empty English text.
Unnamed rows are real and spawnable, they are just placeholders, cut content or
internal variants; they can only be referenced by id.

Every `Monster` id in that table is also documented, faction by faction, in
[MOBS_AND_NPCS.md](MOBS_AND_NPCS.md).

### 3.1 Aliases accepted for `<kind>`

```
monster    | monsters | npc | npcs | char | character | characters | mob | mobs
deployable | deployables | dep
vehicle    | vehicles | veh
carryable  | carryables | carry
turret     | turrets
```

## 4. Spawning from the database in-game

All three commands are registered **twice** — as a chat command (typed in the
game's chat window, prefixed the way your client is configured, e.g. `\spawn`)
and as an admin/server command (the server console / admin channel). They share
one implementation in `Systems/Spawning/SDBSpawner.cs`, so behaviour is
identical.

### 4.1 `spawn` — create anything

```
spawn <kind> <id|name> [<x> <y> <z>]
```

Aliases: `spawn`, `sdbspawn`, `spawn_sdb`.

- `<id|name>` is either a numeric row id **or** a name. Names are matched
  case-insensitively; exact match beats prefix match beats substring match.
  Multi-word names work without quoting (`spawn monster Aranha Queen`).
- The optional `x y z` is parsed with invariant culture. Omit it and the entity
  spawns at your character's position; the orientation always follows your
  character (identity from the console).
- Ambiguous names are rejected with the list of candidates instead of guessing.

Examples:

```
spawn monster 2435                  -> Aranha Queen at your feet
spawn monster Aranha Queen          -> same, by name
spawn npc 290 -25.5 118 492         -> Accord Assault at an explicit position
spawn deployable Battleframe Station
spawn vehicle 116                   -> Cobra XLR
spawn vehicle Cobra XLR 0 0 0
spawn carryable 26                  -> Accord Datapad
spawn turret Minigun Turret         -> attaches a turret to your character
```

Feedback looks like `Spawned monster 2435 (Aranha Queen) at (12.5, 40, 491.9)`.

Notes per kind:

- **monster** — full `CharacterEntity.LoadMonster` path: chassis, warpaints,
  weapons, faction hostility, physics body, AI lifecycle.
- **deployable** — spawned unowned with the row's default faction.
- **vehicle** — owned by your character when you have one, never auto-mounted.
- **carryable** — position only; carryables have no orientation.
- **turret** — turrets are *child* entities and need a parent, so this attaches
  the turret to the calling player's character. It is refused from the console.

### 4.2 `sdb` — browse and search

```
sdb                                  # overview: row counts per kind
sdb <kind>                           # first 20 rows of that kind
sdb <kind> <filter>                  # rows whose name matches (or the exact id)
sdb <kind> <filter> <limit>          # up to <limit> rows (max 200)
```

Aliases: `sdb`, `sdblist`, `sdbsearch`, `sdbfind`.

Results are printed to the **client console** (`SendDebugLog`), not the chat
line, because listings are multi-line; chat just confirms that the output was
printed. From the server console they go to the log.

```
sdb monster aranha 10
dbcharacter::Monster matching "aranha" (10 shown):
   2342  Aranha  [gaea]
   2344  Aranha Soldier  [gaea]
   2345  Aranha Worker  [gaea]
   2435  Aranha Queen  [gaea]
   2569  Aranha Hatchling  [gaea]
   ...

(`-1` in a monster row means "inherit from the chassis"; a blank `behavior`
means the row has no CAIS behavior set and the NPC just stands there.)
```

### 4.3 `sdbinfo` — inspect one row

```
sdbinfo <kind> <id|name>
```

Aliases: `sdbinfo`, `sdbshow`, `sdbrow`.

```
sdbinfo monster 2435
dbcharacter::Monster #2435: Aranha Queen
  faction        : 7 (gaea)
  chassis        : 122993   backpack: 0
  weapons        : 143828 / 0
  behavior       : - (off: -, def: -)
  scaling table  : 0   health regen: 5
  speed          : normal -1, fast -1
  body           : radius -1, mass -1
  loot tables    : 5706 / 5395
  ai spawn delay : 2000 ms
  spawn with     : npc 2435 [<x> <y> <z>]
```

Each kind prints the fields that matter for it (deployables show health,
category and build time; vehicles show class and race; carryables show pickup
radii; turrets show posture and pitch/yaw limits).

### 4.4 Relationship to the older commands

The pre-existing typed commands still work and are unchanged:

| Old | New equivalent |
|-----|----------------|
| `npc <id> [x y z]` | `spawn monster <id\|name> [x y z]` |
| `deployable <id> [x y z]` | `spawn deployable <id\|name> [x y z]` |
| `vehicle <id> [x y z]` | `spawn vehicle <id\|name> [x y z]` |
| `carryable <id> [x y z]` | `spawn carryable <id\|name> [x y z]` |
| *(none)* | `spawn turret <id\|name>` |

The new commands add name resolution, discovery (`sdb`), inspection
(`sdbinfo`), turrets, and one consistent syntax across kinds.

## 5. Code map

| File | Role |
|------|------|
| `StaticDB/Loaders/StaticDBLoader.cs` | `LoadStaticDB<T>("schema::Table")` reflection loader; now also `LoadLocalizedText` and `LoadMonsterScaling`. |
| `StaticDB/SDBInterface.cs` | Static dictionaries + accessors; now also `GetLocalizedString`, `GetMonsterScaling`, and `Get*s()` enumerators for the spawnable tables. |
| `StaticDB/SDBCatalog.cs` | **New.** Kind enum, row -> `SDBCatalogEntry` projection, ranked search, id/name resolution, per-kind `Describe`. |
| `Systems/Spawning/SDBSpawner.cs` | **New.** Argument parsing and dispatch shared by the chat and admin commands. |
| `Systems/Chat/Commands/SpawnChatCommand.cs`, `SdbBrowseChatCommand.cs`, `SdbInfoChatCommand.cs` | Chat-side registrations. |
| `Systems/Admin/Commands/SpawnFromSDBServerCommand.cs`, `SdbBrowseServerCommand.cs`, `SdbInfoServerCommand.cs` | Admin/console registrations. |
| `Systems/EntityManager/EntityManager.cs` | The actual `Spawn*` methods (unchanged). |

`SDBCatalog` is the only place that knows how a kind maps to a table, an
accessor and a spawn method — adding a sixth spawnable kind means adding one
enum value and one case per `switch` there plus one in `SDBSpawner`.

## 6. Regenerating everything in this document

```sh
# The reference file (split zip, extracted outside Git)
cat Tools/clientdb.zip.001 Tools/clientdb.zip.002 > /tmp/clientdb.zip
unzip -o /tmp/clientdb.zip -d /tmp

# Coverage table of §2
python3 Tools/SdbDump/sdb_dump.py coverage /tmp/clientdb.sd2

# Spawnable catalog of §3 (all kinds, or one)
python3 Tools/SdbDump/sdb_dump.py spawnables /tmp/clientdb.sd2 -o spawnables.json
python3 Tools/SdbDump/sdb_dump.py spawnables /tmp/clientdb.sd2 vehicle

# Overview / single tables / the mobs report
python3 Tools/SdbDump/sdb_dump.py info     /tmp/clientdb.sd2
python3 Tools/SdbDump/sdb_dump.py dump     /tmp/clientdb.sd2 dbcharacter::Turret -o turrets.json
python3 Tools/SdbDump/sdb_dump.py monsters /tmp/clientdb.sd2 -o monsters.json

# Verify the decoder itself against a synthetic .sd2
python3 Tools/SdbDump/selftest.py
```

Note that `dblocalization::LocalizedText` has 175k rows, so any command that
resolves names takes ~10 s and a few hundred MB of RAM.
