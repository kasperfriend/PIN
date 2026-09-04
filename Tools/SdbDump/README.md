# SdbDump

Standalone decoder/dumper for Firefall's Static Database (`clientdb.sd2` from
`Firefall/system/db`), the file PIN loads all game data from.

This is a dependency-free Python port of the same format logic PIN uses at
runtime through the [`FauFau`](https://github.com/themeldingwars/FauFau) NuGet
package (`StaticDBLoader` -> `FauFau.Formats.StaticDB`): 128-byte header,
Mersenne-Twister payload obfuscation keyed by the patch string, deflate
compression, the table/field/row memory image (memory layout `1002`), and the
MT-encrypted shared string/blob pool.

Unlike the runtime loader, this tool can look at **any** `.sd2` file without a
Firefall installation, and resolves table/column names by hashing candidate
names with Firefall's `FFnv32` (the file itself stores only hashes). Table and
column names are harvested automatically from PIN's own source
(`StaticDBLoader.cs` + `StaticDB/Records/*.cs`), so dumps come out fully
labelled.

## Usage

```sh
# Overview of every table in the file (name, rows, columns, stride)
python3 sdb_dump.py info /path/to/clientdb.sd2

# Hash list of all tables
python3 sdb_dump.py tables /path/to/clientdb.sd2

# Dump one table as JSON (column names resolved from PIN source)
python3 sdb_dump.py dump /path/to/clientdb.sd2 dbcharacter::Monster -o monster.json

# Full mobs/NPCs report: dbcharacter::Monster joined with localization names,
# factions and MonsterScaling, plus the dbcharacter::Turret table
python3 sdb_dump.py monsters /path/to/clientdb.sd2 -o monsters.json
```

Optional flags: `--game-dir <dir>` to point at PIN's `UdpHosts/GameServer`
for name harvesting (auto-detected by default) and `--names <file>` to add
extra candidate table names.

## Verification

`selftest.py` builds a synthetic `.sd2` from scratch (header, obfuscation,
compression, table/field/row layout, MT-encrypted pool, nullable bitfields)
and verifies that this tool decodes every value back correctly:

```sh
python3 selftest.py
```

## Note on the real file

`clientdb.sd2` is game data taken from a Firefall installation; it is not
committed to this repository. Point the tool at your own copy.
