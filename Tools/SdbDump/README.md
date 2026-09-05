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

# Everything PIN's in-game `spawn <kind> <id|name>` command can create:
# monsters, deployables, vehicles, carryables and turrets, with resolved names
python3 sdb_dump.py spawnables /path/to/clientdb.sd2 -o spawnables.json
python3 sdb_dump.py spawnables /path/to/clientdb.sd2 vehicle   # one kind

# How much of the file PIN's StaticDBLoader actually reads, and which
# identified tables are still unused
python3 sdb_dump.py coverage /path/to/clientdb.sd2
```

`spawnables` mirrors `GameServer/StaticDB/SDBCatalog.cs`, so the offline dump
and the in-game `sdb` command list the same rows.

Optional flags: `--game-dir <dir>` to point at PIN's `UdpHosts/GameServer`
for name harvesting (auto-detected by default) and `--names <file>` to add
extra candidate table names.

## Generating Docs/SpawnReference

`spawn_reference.py` is the generator behind
[`Docs/SpawnReference`](../../Docs/SpawnReference/README.md): the full
spawnable catalog, one Markdown spreadsheet **and** one CSV per kind, where
every row carries the exact `\spawn <kind> <id>` command.

```sh
# rewrite every document + CSV in Docs/SpawnReference (~30 s)
python3 spawn_reference.py /path/to/clientdb.sd2

# somewhere else, or only part of it
python3 spawn_reference.py /path/to/clientdb.sd2 --out-dir /tmp/ref
python3 spawn_reference.py /path/to/clientdb.sd2 --kinds monster,turret --no-csv
```

It imports `sdb_dump.py` as its decoder, so it needs nothing but Python 3.
Beyond `spawnables` it also resolves the foreign keys a player actually cares
about — faction, chassis, weapons, loot tables, deployable category/function,
vehicle class, granted ability — by reading the small lookup tables
(`dbcharacter::Faction`, `DeployableCategory`, `DeployableFunction`,
`vcs::VehicleClass`, `dbitems::LootTable`, `dbitems::RootItem`,
`apt::AbilityData`, `dbcharacter::MonsterTitle`, `TurretWeapon`) and
decrypting **only** the `dblocalization::LocalizedText` rows those tables
reference. The CSVs additionally contain every raw column of each SDB record.

Run it after changing `SDBCatalog.cs` / the spawn commands, or when pointing
PIN at a different Firefall build, and commit the result.

## Verification

`selftest.py` builds a synthetic `.sd2` from scratch (header, obfuscation,
compression, table/field/row layout, MT-encrypted pool, nullable bitfields)
and verifies that this tool decodes every value back correctly:

```sh
python3 selftest.py
```

## Note on the real file

`clientdb.sd2` is game data taken from a Firefall installation. A decoded copy
of build `prod-1962` (split zip parts under `Tools/`) is used as the reference
input for `Docs/MOBS_AND_NPCS.md` and `Docs/SpawnReference/`; the extracted
`.sd2` itself stays out of Git. Point the tool at your own copy for anything
else.

```sh
cat Tools/clientdb.zip.001 Tools/clientdb.zip.002 > /tmp/clientdb.zip
unzip -o /tmp/clientdb.zip -d /tmp/sdb
python3 Tools/SdbDump/spawn_reference.py /tmp/sdb/clientdb.sd2
```
