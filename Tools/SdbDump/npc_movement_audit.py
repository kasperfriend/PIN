#!/usr/bin/env python3
"""Reproducible NPC movement census of the COMPLETE clientdb.sd2 schema.

No .NET, network, installed client or third-party Python dependencies required.
By default reads the repository's split prod-1962 archive without extracting it
into Git. --check verifies the checked-in census; a positional .sd2 audits another
build. This is evidence/inventory, not a reconstruction of missing CAIS trees.
"""
import argparse
import collections
import csv
import hashlib
import io
import json
from pathlib import Path
import tempfile
import zipfile

from sdb_dump import (
    StaticDB, KNOWN_TABLES, TYPE_NAMES, DBT_BLOB, DBT_VECTOR2, DBT_VECTOR3,
    DBT_VECTOR2_ARRAY, DBT_VECTOR3_ARRAY, DBT_VECTOR4_ARRAY, DBT_MATRIX4X4,
    DBT_HALF_MATRIX4X3, DBT_BOX3, harvest_pin_names, harvest_record_table_names,
    apply_column_names,
)

ROOT = Path(__file__).resolve().parents[2]
BEHAVIORS = ("behavior", "behavior_offensive", "behavior_defensive")
SPATIAL = {DBT_VECTOR2, DBT_VECTOR3, DBT_VECTOR2_ARRAY, DBT_VECTOR3_ARRAY,
           DBT_VECTOR4_ARRAY, DBT_MATRIX4X4, DBT_HALF_MATRIX4X3, DBT_BOX3}
ROUTE_NAMES = {"StockShootAndFollowRoute", "OneOff_FollowRoute", "NavigateToLocation"}


def parse_behavior(text):
    """Top-level CAIS arguments; commas in child invocations/quoted strings stay inside."""
    text = (text or "").strip()
    if "(" not in text:
        return text, {}
    name, args = text.split("(", 1)
    if ")" in args:
        args = args[:args.rfind(")")]
    fragments = []
    start = depth = 0
    quoted = escaped = False
    for i, ch in enumerate(args):
        if escaped:
            escaped = False
            continue
        if quoted and ch == "\\":
            escaped = True
            continue
        if ch == '"':
            quoted = not quoted
        elif not quoted:
            if ch == "(":
                depth += 1
            elif ch == ")" and depth:
                depth -= 1
            elif ch == "," and not depth:
                fragments.append(args[start:i])
                start = i + 1
    fragments.append(args[start:])
    values = {}
    for fragment in fragments:
        if "=" in fragment:
            key, value = fragment.split("=", 1)
            key = key.strip().lower()
            if key:
                values[key] = value.strip().strip('"')
    return name.strip(), values


def csv_text(columns, rows):
    stream = io.StringIO(newline="")
    writer = csv.DictWriter(stream, fieldnames=columns, lineterminator="\n")
    writer.writeheader()
    writer.writerows(rows)
    return stream.getvalue()


def build_reports(sdb_path, root=ROOT):
    db = StaticDB(sdb_path)
    game_dir = root / "UdpHosts" / "GameServer"
    loaded, columns = harvest_pin_names(str(game_dir))
    db.resolve_names(KNOWN_TABLES + loaded + harvest_record_table_names(game_dir))
    apply_column_names(db, columns)
    tables = []
    for table in sorted(db.tables, key=lambda t: t["name"] or f"0x{t['id']:08X}"):
        def fields(types):
            return "; ".join(table["column_names"].get(f["id"], f"0x{f['id']:08X}")
                             + ":" + TYPE_NAMES[f["type"]]
                             for f in table["fields"] if f["type"] in types)
        tables.append({"hash": f"0x{table['id']:08X}", "table": table["name"] or "UNIDENTIFIED",
                       "rows": table["row_count"], "columns": table["num_fields"],
                       "runtime_loaded": table["name"] in loaded,
                       "spatial_fields": fields(SPATIAL), "blob_fields": fields({DBT_BLOB})})

    def rows(name):
        table = db.find_table(name)
        if table is None:
            raise ValueError(f"Required reference table missing: {name}")
        return list(db.rows(table))

    monsters = rows("dbcharacter::Monster")
    functions = {r["id"]: r for r in rows("dbcharacter::DeployableFunction")}
    emotes = {r["name"].lower(): r["id"] for r in rows("dbcharacter::EmoteRecord") if r["name"]}
    deployables = rows("dbcharacter::Deployable")
    placements = json.loads((game_dir / "StaticDB" / "CustomData" / "deployable.json").read_text(encoding="utf-8"))
    placed = collections.Counter(p["type"] for p in placements)
    monster_fields = ("id", "behavior", "behavior_offensive", "behavior_defensive", "behavior_instance_id",
                      "behavior_offensive_instance_id", "behavior_defensive_instance_id", "normal_speed", "fast_speed", "gravity")
    roster = [{key: r[key] for key in monster_fields} for r in sorted(monsters, key=lambda r: r["id"])]
    census = collections.defaultdict(collections.Counter)
    parameter_counts = collections.defaultdict(collections.Counter)
    routes = []
    for row in monsters:
        for field in BEHAVIORS:
            name, params = parse_behavior(row[field])
            census[name][field] += 1
            parameter_counts[field].update(params.keys())
            if name in ROUTE_NAMES or "city_prefix" in params:
                routes.append((row["id"], field, row[field]))

    activities = []
    for row in sorted(deployables, key=lambda r: r["id"]):
        if not row["behavior"]:
            continue
        name, params = parse_behavior(row["behavior"])
        activities.append({"deployable_id": row["id"], "function_id": row["function"],
                           "function": functions.get(row["function"], {}).get("name", "UNKNOWN"),
                           "behavior": row["behavior"], "emote_id": emotes.get(params.get("emote", "").lower(), ""),
                           "explicit_emote_duration": params.get("emoteduration", ""),
                           "authored_placements_in_repository": placed[row["id"]]})

    sha = hashlib.sha256(Path(sdb_path).read_bytes()).hexdigest()
    unidentified = sum(t["name"] is None for t in db.tables)
    vec3arrays = sum(f["type"] == DBT_VECTOR3_ARRAY for t in db.tables for f in t["fields"])
    populated_refs = [sum(bool(r[field + "_instance_id"]) for r in monsters) for field in BEHAVIORS]
    row_counts = {table["name"]: table["row_count"] for table in db.tables}
    loaded_tables = sum(table["runtime_loaded"] for table in tables)
    defined_activities = sum(bool(r["behavior"]) for r in deployables)
    placed_activities = sum(placed[r["id"]] for r in deployables if r["behavior"])
    lines = ["# NPC movement database census", "",
             "Generated by `python Tools/SdbDump/npc_movement_audit.py`. Do not edit the generated files by hand.",
             "Runtime behaviour and the original-game parity boundary: [NPC routines](../NPC_ROUTINES.md).", "",
             f"- Source build: **{db.patch}**, SHA-256 `{sha}`.",
             f"- **{len(db.tables)} tables**, **{sum(t['row_count'] for t in db.tables):,} rows** in the database; **{unidentified} unidentified tables**.",
             f"- **{loaded_tables} tables** are referenced by PIN's loader; the other **{len(db.tables) - loaded_tables}** named tables are also included in this census, not silently skipped.",
             f"- **{len(monsters):,} monster templates**, all three behaviour columns and all three instance references included in `monsters.json`.",
             f"- Nonzero base/offensive/defensive behaviour-instance references: **{' / '.join(map(str, populated_refs))}** rows. No CAIS instance/tree-definition table was found in this client schema.",
             f"- **{vec3arrays} Vector3Array columns** in the entire file. All spatial and blob columns, including those in unloaded tables, are listed in `tables.csv`.",
             f"- **{len(deployables):,} deployable templates**, **{len(functions)} functions**, **{defined_activities} nonempty deployable behaviours** in `activities.csv`.",
             f"- **{placed_activities} placements** of those nonempty-behaviour deployables in the repository's `CustomData/deployable.json`. The templates alone do not place a work station in a zone.", "",
             "## What the path-like tables actually contain", "",
             "The following semantic interpretations were verified for the bundled prod-1962 build. Counts above and below are computed from the input; other builds need their schema/content reviewed before applying these interpretations.", "",
             f"- `clientmissions::MissionWaypoint`: {row_counts.get('clientmissions::MissionWaypoint', 0)} chunk-local mission locations and area polygons; `chunk_id` is present. There is no NPC id, patrol ordering, next-node link or monster→waypoint association. These are not NPC routes.",
             f"- `clientmissions::GoldenPath`: {row_counts.get('clientmissions::GoldenPath', 0)} mission-progression rows (`missionchain_id`, `mission_id`, `display_lvl`, `level_req`, `order`), no movement coordinates.",
             f"- `vcs::GroundPathComponentDef`: {row_counts.get('vcs::GroundPathComponentDef', 0)} rows of vehicle motion tuning (`accel`, `max_speed`), no points.",
             f"- `vcs::FlightPathComponentDef`: {row_counts.get('vcs::FlightPathComponentDef', 0)} rows of vehicle flight/landing tuning, no route points.",
             "- `dbzonemetadata::ChunkRecord.exclude_from_pathing`: pathing exclusions, not patrol definitions.",
             "- `dbzonemetadata::GlobeViewLocation.route_mask`: globe UI routes, not NPC waypoints.",
             "- Remaining vector/matrix columns describe visual offsets, hardpoints, aim, physics, cameras, particles and UI gradients. Blob columns belong to decals, subzone grids, reverb materials and cosmetic warpaint. Opaque bytes are not fully interpreted, so this is not proof about every blob payload or external asset. No authored NPC route table was identified.", "",
             "**Absence of an authored route is not permission to turn mission markers or template offsets into patrols.** Map gameplay/CAIS/server encounter data may contain routes not present in this client database; those assets are not available in this checkout.", "",
             "## Explicit route / named-point requests", "",
             "Every occurrence in all three monster columns (not just the base column):", "",
             "| Monster id | Column | Invocation |", "|---|---|---|"]
    for monster_id, field, text in sorted(routes):
        lines.append(f"| {monster_id} | `{field}` | `{text.replace('|', '&#124;')}` |")
    lines += ["", "These invocations do not carry waypoint coordinates or the route assignment. `city_prefix` names world points; it does not define them.",
              "", "## Every behaviour name", "", "Blank means no invocation, not an implicit Wander tree.", "",
              "| Behaviour | Base rows | Offensive rows | Defensive rows |", "|---|---:|---:|---:|"]
    for name, counts in sorted(census.items()):
        lines.append(f"| `{name or '(empty)'}` | {counts['behavior']} | {counts['behavior_offensive']} | {counts['behavior_defensive']} |")
    lines += ["", "## Every top-level parameter", "",
              "Counts are occurrences, not unique NPCs. Quoted/nested behaviour arguments are not flattened into their parents.", "",
              "| Parameter | Base | Offensive | Defensive |", "|---|---:|---:|---:|"]
    keys = set().union(*(counter.keys() for counter in parameter_counts.values()))
    for key in sorted(keys):
        lines.append(f"| `{key}` | " + " | ".join(str(parameter_counts[field][key]) for field in BEHAVIORS) + " |")
    lines += ["", "## Reproduce / verify", "", "```sh", "python Tools/SdbDump/npc_movement_audit.py --check",
              "python Tools/SdbDump/npc_movement_audit.py /path/to/clientdb.sd2 --out-dir /tmp/movement-audit", "```", "",
              "`--check` fails if any census file differs. Every row of the bundled prod-1962 reference JSON is also resolved and checked by the C# suite without requiring an installed client. Alternate --out-dir outputs are not that embedded test fixture.", ""]
    return {"README.md": "\n".join(lines),
            "monsters.json": json.dumps({"patch": db.patch, "sha256": sha, "monsters": roster}, indent=2, ensure_ascii=False) + "\n",
            "tables.csv": csv_text(list(tables[0]), tables),
            "activities.csv": csv_text(list(activities[0]), activities)}


def main(argv=None):
    parser = argparse.ArgumentParser(description=__doc__)
    parser.add_argument("sdb", nargs="?", help="Default: bundled Tools/clientdb.zip.001/.002")
    parser.add_argument("--out-dir", type=Path, default=ROOT / "Docs" / "NpcMovement")
    parser.add_argument("--check", action="store_true")
    args = parser.parse_args(argv)
    with tempfile.TemporaryDirectory(prefix="pin-movement-") as temporary:
        if args.sdb:
            path = Path(args.sdb)
        else:
            archive = b"".join((ROOT / "Tools" / f"clientdb.zip.{part:03d}").read_bytes() for part in (1, 2))
            with zipfile.ZipFile(io.BytesIO(archive)) as zipped:
                # Read one expected entry; do not extract arbitrary archive paths.
                path = Path(temporary) / "clientdb.sd2"
                path.write_bytes(zipped.read("clientdb.sd2"))
        reports = build_reports(path)
    failures = []
    for name, content in reports.items():
        target = args.out_dir / name
        if args.check:
            if not target.exists() or target.read_text(encoding="utf-8") != content:
                failures.append(str(target))
        else:
            target.parent.mkdir(parents=True, exist_ok=True)
            target.write_text(content, encoding="utf-8", newline="\n")
    if failures:
        raise SystemExit("Stale movement census: " + ", ".join(failures))
    print(("Verified" if args.check else "Generated") + f" {len(reports)} movement census files in {args.out_dir}")


if __name__ == "__main__":
    main()
