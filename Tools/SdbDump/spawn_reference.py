#!/usr/bin/env python3
"""Generate `Docs/SpawnReference` - the full catalog of everything PIN can spawn.

`sdb_dump.py spawnables` answers "which rows exist"; this script turns the same
data into the checked-in reference documents: one Markdown "spreadsheet" and one
CSV per spawnable kind (mobs, deployables, vehicles, carryables, turrets), plus
an index. Every row carries the exact in-game command that spawns it, and the
foreign keys a player actually cares about (faction, chassis, weapons, loot
table, deployable category/function, vehicle class, granted ability) are
resolved to readable names.

The kind -> table mapping mirrors `UdpHosts/GameServer/StaticDB/SDBCatalog.cs`
and the commands mirror `Systems/Spawning/SDBSpawner.cs`, so the documents and
the running server agree.

Decoding is done by `sdb_dump.py` (imported as a module). Only the localized
strings that are actually referenced are decrypted, which keeps a full run in
the order of a minute instead of the ~15 min a complete
`dblocalization::LocalizedText` scan costs.

Usage:
    python3 spawn_reference.py /path/to/clientdb.sd2
    python3 spawn_reference.py /path/to/clientdb.sd2 --out-dir /tmp/ref
    python3 spawn_reference.py /path/to/clientdb.sd2 --kinds monster,turret
    python3 spawn_reference.py /path/to/clientdb.sd2 --no-csv
"""

import argparse
import csv
import os
import re
import sys

sys.path.insert(0, os.path.dirname(os.path.abspath(__file__)))

import sdb_dump  # noqa: E402  (path set up above)

DEFAULT_OUT_DIR = os.path.normpath(
    os.path.join(os.path.dirname(os.path.abspath(__file__)), "..", "..", "Docs", "SpawnReference")
)
DEFAULT_GAME_DIR = os.path.normpath(
    os.path.join(os.path.dirname(os.path.abspath(__file__)), "..", "..", "UdpHosts", "GameServer")
)

# dbcharacter::Monster `race` byte -> census meaning (see Docs/MOBS_AND_NPCS.md 4.1).
RACE_NAMES = {
    0: "Human",
    2: "Chosen",
    6: "Misc",
    7: "Companion",
    8: "Melded",
    9: "Wildlife",
    10: "Outlaw",
    11: "Large wildlife",
}

# vcs::VehicleClass `name` column is authoritative; this is only the fallback
# legend for databases where that table is missing.
VEHICLE_CLASS_FALLBACK = {
    0: "HGV", 1: "LGV", 2: "Cargo", 3: "Dropship", 4: "Train", 5: "MGV", 6: "Battlecruiser",
}

KIND_ORDER = ["monster", "deployable", "vehicle", "carryable", "turret"]

# Columns of the spawnable tables that hold `dblocalization::LocalizedText` ids.
LOCALIZED_COLUMNS = (
    "localized_name_id", "localized_description_id", "interact_string",
    "name_id", "description_id", "abbreviated_name_id",
)


# --------------------------------------------------------------------------- #
# low level helpers
# --------------------------------------------------------------------------- #

def open_db(sdb_path, game_dir, names_path=None):
    """Load an .sd2 and label tables/columns from PIN's source (as `main` does)."""
    db = sdb_dump.StaticDB(sdb_path)
    candidates = list(sdb_dump.load_names(names_path))
    if game_dir and os.path.isdir(game_dir):
        table_names, col_by_hash = sdb_dump.harvest_pin_names(game_dir)
        candidates += [n for n in table_names if n not in candidates]
        by_id = {sdb_dump.ffnv32(n): n for n in candidates}
        for table in db.tables:
            if table["id"] in by_id:
                table["name"] = by_id[table["id"]]
        sdb_dump.apply_column_names(db, col_by_hash)
        print(f"harvested {len(table_names)} table names from PIN source", file=sys.stderr)
    else:
        db.resolve_names(candidates)
    return db


def table_columns(table):
    """column name -> field descriptor, using the labels resolved by open_db."""
    return {
        table["column_names"].get(field["id"], f"col_{field['id']:08x}"): field
        for field in table["fields"]
    }


def selected_fields(table, only=None):
    """[(field index, column name, field)] for the labelled columns wanted.

    Unlabelled columns (nobody guessed their name) are skipped; the field index
    is kept because nullable bitfields are addressed by it.
    """
    selected = []
    for index, field in enumerate(table["fields"]):
        name = table["column_names"].get(field["id"], f"col_{field['id']:08x}")
        if name.startswith("col_") or (only is not None and name not in only):
            continue
        selected.append((index, name, field))
    return selected


def read_row(db, table, row_index, selected):
    """Decode one row's selected columns, honouring the nullable bitfields."""
    nulls = db.row_nulls(table, row_index) if table["nullable_bitfields"] else ()
    return {
        name: None if field_index in nulls else db.row_field(table, field, row_index)
        for field_index, name, field in selected
    }


def iter_rows(db, table, only=None):
    """Yield one dict per row, decoding only the requested columns.

    Decoding is the expensive part of this format (strings are decrypted with a
    freshly seeded Mersenne twister), so callers ask for exactly what they need.
    """
    selected = selected_fields(table, only)
    for index in range(table["row_count"]):
        yield read_row(db, table, index, selected)


def build_index(db, table_name, key="id", only=()):
    """key value -> {column: value} for a lookup table."""
    table = db.find_table(table_name)
    if table is None:
        return {}
    columns = table_columns(table)
    if key not in columns:
        return {}
    selected = selected_fields(table, set(only) | {key})
    out = {}
    for index in range(table["row_count"]):
        row = read_row(db, table, index, selected)
        out[row.get(key)] = {name: value for name, value in row.items() if name != key}
    return out


class LocalizedText:
    """`dblocalization::LocalizedText` with on-demand decryption.

    The table has ~175k rows and every string costs a Mersenne-twister seeding,
    so decoding all of it takes minutes. Instead callers register the ids they
    care about with `want()` and `resolve()` makes a single pass that decrypts
    only those.
    """

    def __init__(self, db):
        self.db = db
        self.table = db.find_table("dblocalization::LocalizedText")
        self.columns = table_columns(self.table) if self.table is not None else {}
        self.wanted = set()
        self.text = {}

    def want(self, value):
        if isinstance(value, int) and value > 0:
            self.wanted.add(value)

    def want_all(self, values):
        for value in values:
            self.want(value)

    def resolve(self):
        if self.table is None or not self.wanted:
            return
        id_field = self.columns.get("id")
        english_field = self.columns.get("english")
        if id_field is None or english_field is None:
            return
        db, table = self.db, self.table
        wanted = self.wanted
        for index in range(table["row_count"]):
            text_id = db.row_field(table, id_field, index)
            if text_id in wanted:
                value = db.row_field(table, english_field, index)
                if value and value.strip():
                    self.text[text_id] = value.strip()
                wanted.discard(text_id)
                if not wanted:
                    break
        print(f"resolved {len(self.text)} localized strings", file=sys.stderr)

    def get(self, value):
        return self.text.get(value) if isinstance(value, int) else None


# --------------------------------------------------------------------------- #
# formatting
# --------------------------------------------------------------------------- #

def fmt_number(value):
    if isinstance(value, float):
        if value == int(value) and abs(value) < 1e15:
            return str(int(value))
        return f"{value:.6g}"
    return str(value)


def fmt(value, empty=""):
    """Render a raw SDB value for Markdown/CSV output."""
    if value is None:
        return empty
    if isinstance(value, bool):
        return "true" if value else "false"
    if isinstance(value, (int, float)):
        return fmt_number(value)
    if isinstance(value, (tuple, list)):
        return "(" + ", ".join(fmt_number(v) if isinstance(v, (int, float)) else fmt(v) for v in value) + ")"
    if isinstance(value, bytes):
        value = value.decode("utf-8", "replace")
    return str(value).strip()


def cell(value, empty="-"):
    """Markdown table cell: escaped, never empty."""
    text = fmt(value, empty="")
    if not text:
        return empty
    return text.replace("|", "\\|").replace("\n", " ")


def md_table(headers, rows):
    lines = ["| " + " | ".join(headers) + " |", "|" + "|".join("---" for _ in headers) + "|"]
    lines.extend("| " + " | ".join(row) + " |" for row in rows)
    return "\n".join(lines)


def md_table_from_dicts(headers, keys, rows):
    return md_table(headers, [[cell(row.get(key)) for key in keys] for row in rows])


def count(value):
    return f"{value:,}"


def id_list(values):
    return ", ".join(str(v) for v in values)


# --------------------------------------------------------------------------- #
# context: everything shared between the kind builders
# --------------------------------------------------------------------------- #

class Context:
    def __init__(self, db):
        self.db = db
        self.patch = db.patch
        self.text = LocalizedText(db)
        self.raw = {}          # kind -> [raw row dicts]
        self.rows = {}         # kind -> [resolved row dicts]
        self.factions = {}     # id -> {"internal_name", "name", "abbrev", "default_stance", ...}
        self.categories = {}   # deployable category id -> name
        self.functions = {}    # deployable function id -> name
        self.vehicle_classes = {}
        self.loot_tables = {}  # id -> name
        self.abilities = {}    # apt::AbilityData id -> localized name id
        self.titles = {}       # dbcharacter::MonsterTitle id -> localized name id
        self.item_names = {}   # root item id -> name
        self.turret_weapons = {}  # turret id -> [weapon ids]
        self.item_name_ids = {}   # root item id -> localized name id
        self.scaling = {}      # level -> {health, damage}

    # -- name helpers ------------------------------------------------------ #

    def name(self, text_id):
        return self.text.get(text_id)

    def faction_label(self, faction_id):
        faction = self.factions.get(faction_id)
        if faction is None:
            return None
        return faction["internal_name"] or faction["name"]

    def item_name(self, item_id):
        return self.item_names.get(item_id)

    def command(self, kind, row_id):
        return f"\\spawn {kind} {row_id}"


def load_lookups(ctx):
    """Read the small lookup tables the spawnable rows point at."""
    db = ctx.db

    faction_rows = db.find_table("dbcharacter::Faction")
    if faction_rows is not None:
        for row in iter_rows(db, faction_rows):
            ctx.factions[row["id"]] = {
                "id": row["id"],
                "internal_name": (row.get("internal_name") or "").strip(),
                "name_id": row.get("localized_name_id"),
                "abbrev_id": row.get("abbreviated_name_id"),
                "default_stance": row.get("default_stance"),
                "default_stance_priority": row.get("default_stance_priority"),
                "min_reputation": row.get("min_reputation"),
                "max_reputation": row.get("max_reputation"),
                "starting_reputation": row.get("starting_reputation"),
                "name": None,
                "abbrev": None,
            }
            ctx.text.want(row.get("localized_name_id"))
            ctx.text.want(row.get("abbreviated_name_id"))

    for table_name, target in (("dbcharacter::DeployableCategory", ctx.categories),
                               ("dbcharacter::DeployableFunction", ctx.functions),
                               ("vcs::VehicleClass", ctx.vehicle_classes)):
        for key, value in build_index(db, table_name, only=("name", "description")).items():
            target[key] = value

    ctx.loot_tables = build_index(db, "dbitems::LootTable", only=("name",))
    ctx.abilities = build_index(db, "apt::AbilityData", only=("localized_name_id",))
    ctx.titles = build_index(db, "dbcharacter::MonsterTitle", only=("localized_name_id",))
    ctx.scaling = build_index(db, "dbcharacter::MonsterScaling", key="level", only=("health", "damage"))

    turret_weapons = db.find_table("dbcharacter::TurretWeapon")
    if turret_weapons is not None:
        for row in iter_rows(db, turret_weapons):
            ctx.turret_weapons.setdefault(row.get("turret_type_id"), []).append(row.get("weapon_id"))

    for ability in ctx.abilities.values():
        ctx.text.want(ability.get("localized_name_id"))
    for title in ctx.titles.values():
        ctx.text.want(title.get("localized_name_id"))

    print(f"lookups: {len(ctx.factions)} factions, {len(ctx.categories)} deployable categories, "
          f"{len(ctx.functions)} functions, {len(ctx.vehicle_classes)} vehicle classes, "
          f"{len(ctx.loot_tables)} loot tables, {len(ctx.abilities)} abilities, "
          f"{len(ctx.titles)} titles, {len(ctx.turret_weapons)} turrets with weapons", file=sys.stderr)


def load_kind_rows(ctx, kinds):
    """Pass 1: read the spawnable tables themselves (every column, for the CSVs)."""
    for kind in kinds:
        table_name = SPEC[kind]["table"]
        table = ctx.db.find_table(table_name)
        if table is None:
            print(f"warning: {table_name} not in this database - skipping {kind}", file=sys.stderr)
            ctx.raw[kind] = []
            continue
        rows = [row for row in iter_rows(ctx.db, table) if row.get("id") is not None]
        rows.sort(key=lambda row: row["id"])
        ctx.raw[kind] = rows
        named = sum(1 for row in rows if ctx.text.get(row.get("localized_name_id")))
        print(f"{kind:<11} {len(rows):>5} rows from {table_name}", file=sys.stderr)

        for row in rows:
            ctx.text.want_all(row.get(column) for column in LOCALIZED_COLUMNS)


def load_item_names(ctx):
    """Resolve the item names behind monster chassis / weapon / turret-weapon ids."""
    referenced = set()
    for row in ctx.raw.get("monster", []):
        for column in ("chassis_id", "weapon1_id", "weapon2_id", "backpack_id"):
            if row.get(column):
                referenced.add(row[column])
    for weapons in ctx.turret_weapons.values():
        referenced.update(w for w in weapons if w)

    table = ctx.db.find_table("dbitems::RootItem")
    if table is None or not referenced:
        return
    columns = table_columns(table)
    key_field, name_field = columns.get("sdb_id"), columns.get("name_id")
    if key_field is None or name_field is None:
        return

    db = ctx.db
    selected = selected_fields(table, {"sdb_id", "name_id"})
    name_ids = {}
    for index in range(table["row_count"]):
        row = read_row(db, table, index, selected)
        item_id = row.get("sdb_id")
        if item_id in referenced and row.get("name_id"):
            name_ids[item_id] = row["name_id"]
            ctx.text.want(row["name_id"])
    ctx.item_name_ids = name_ids
    print(f"root items: {len(name_ids)} of {len(referenced)} referenced ids carry a name", file=sys.stderr)


def finalize_names(ctx):
    """Pass 2: decrypt the localized strings collected above, then fill lookups."""
    ctx.text.resolve()
    for faction in ctx.factions.values():
        faction["name"] = ctx.text.get(faction["name_id"])
        faction["abbrev"] = ctx.text.get(faction["abbrev_id"])
    for item_id, name_id in getattr(ctx, "item_name_ids", {}).items():
        ctx.item_names[item_id] = ctx.text.get(name_id)


# --------------------------------------------------------------------------- #
# per kind row builders
# --------------------------------------------------------------------------- #

def build_monsters(ctx):
    rows = []
    for raw in ctx.raw.get("monster", []):
        weapon_ids = [raw.get("weapon1_id") or 0, raw.get("weapon2_id") or 0]
        weapons = " / ".join(str(w) if w else "-" for w in weapon_ids)
        loot_ids = [raw.get("loot_table_id") or 0, raw.get("loot_table2_id") or 0]
        loot = " / ".join(str(x) if x else "-" for x in loot_ids)
        race = raw.get("race")
        row = dict(raw)
        row.update({
            "name": ctx.name(raw.get("localized_name_id")),
            "faction": ctx.faction_label(raw.get("faction_id")),
            "race_name": RACE_NAMES.get(race, fmt(race)),
            "chassis_name": ctx.item_name(raw.get("chassis_id")),
            "weapon1_name": ctx.item_name(raw.get("weapon1_id")),
            "weapon2_name": ctx.item_name(raw.get("weapon2_id")),
            "backpack_name": ctx.item_name(raw.get("backpack_id")),
            "loot_table_name": (ctx.loot_tables.get(raw.get("loot_table_id")) or {}).get("name"),
            "loot_table2_name": (ctx.loot_tables.get(raw.get("loot_table2_id")) or {}).get("name"),
            "title_name": ctx.name((ctx.titles.get(raw.get("title")) or {}).get("localized_name_id")),
            "behavior_short": (raw.get("behavior") or "").split("(")[0].strip() or None,
            "weapons": weapons if any(weapon_ids) else None,
            "loot": loot if any(loot_ids) else None,
            "spawn_command": ctx.command("monster", raw["id"]),
        })
        rows.append(row)
    return rows


def build_deployables(ctx):
    rows = []
    for raw in ctx.raw.get("deployable", []):
        category_id = raw.get("deployable_category")
        function_id = raw.get("function")
        category = ctx.categories.get(category_id) or {}
        function = ctx.functions.get(function_id) or {}
        row = dict(raw)
        row.update({
            "name": ctx.name(raw.get("localized_name_id")),
            "faction": ctx.faction_label(raw.get("default_faction")),
            "category_name": category.get("name") or (fmt(category_id) if category_id else None),
            "category_description": category.get("description"),
            "function_name": function.get("name") or (fmt(function_id) if function_id else None),
            "function_description": function.get("description"),
            "interact_text": ctx.name(raw.get("interact_string")),
            "spawn_command": ctx.command("deployable", raw["id"]),
        })
        rows.append(row)
    return rows


def build_vehicles(ctx):
    rows = []
    for raw in ctx.raw.get("vehicle", []):
        class_id = raw.get("vehicle_class")
        vehicle_class = ctx.vehicle_classes.get(class_id) or {}
        row = dict(raw)
        row.update({
            "name": ctx.name(raw.get("localized_name_id")),
            "faction": ctx.faction_label(raw.get("faction_id")),
            "class_name": vehicle_class.get("name") or VEHICLE_CLASS_FALLBACK.get(class_id, fmt(class_id)),
            "spawn_command": ctx.command("vehicle", raw["id"]),
        })
        rows.append(row)
    return rows


def build_carryables(ctx):
    rows = []
    for raw in ctx.raw.get("carryable", []):
        ability_id = raw.get("ability_granted_id")
        row = dict(raw)
        row.update({
            "name": ctx.name(raw.get("localized_name_id")),
            "description": ctx.name(raw.get("localized_description_id")),
            "interact_text": ctx.name(raw.get("interact_string")),
            "ability_granted_name": ctx.name((ctx.abilities.get(ability_id) or {}).get("localized_name_id")),
            "pickup_by": "interact" if raw.get("pickup_by_interaction") else "touch",
            "spawn_command": ctx.command("carryable", raw["id"]),
        })
        rows.append(row)
    return rows


def build_turrets(ctx):
    rows = []
    for raw in ctx.raw.get("turret", []):
        weapon_ids = list(dict.fromkeys(w for w in ctx.turret_weapons.get(raw["id"], []) if w))
        row = dict(raw)
        row.update({
            "name": (raw.get("name") or "").strip() or None,
            "weapons": ", ".join(str(w) for w in weapon_ids) or None,
            "weapon_names": ", ".join(filter(None, (ctx.item_name(w) for w in weapon_ids))) or None,
            "pitch": f"{fmt(raw.get('min_pitch'))} .. {fmt(raw.get('max_pitch'))}",
            "yaw": f"{fmt(raw.get('min_yaw'))} .. {fmt(raw.get('max_yaw'))}",
            "spawn_command": ctx.command("turret", raw["id"]),
        })
        rows.append(row)
    return rows


# --------------------------------------------------------------------------- #
# kind metadata: what goes into the documents
# --------------------------------------------------------------------------- #

SPEC = {
    "monster": {
        "doc": "MOBS.md",
        "csv": "mobs.csv",
        "title": "Mobs & NPCs",
        "table": "dbcharacter::Monster",
        "name_column": "localized_name_id",
        "spawn_method": "`EntityManager.SpawnCharacter(typeId, position, orientation)`",
        "takes_position": True,
        "aliases": "`monster`, `monsters`, `npc`, `npcs`, `char`, `character`, `characters`, `mob`, `mobs`",
        "legacy": "`\\npc <id> [<x> <y> <z>]` (chat + admin)",
        "build": build_monsters,
        "md_columns": [
            ("id", "id"), ("name", "name"), ("faction", "faction"), ("race", "race_name"),
            ("chassis", "chassis_id"), ("weapons", "weapons"), ("behavior", "behavior_short"),
            ("scaling", "scaling_table_id"), ("loot tables", "loot"),
            ("hp regen", "health_regen"), ("spawn command", "spawn_command"),
        ],
        "csv_first": [
            "name", "spawn_command", "faction", "faction_id", "race_name", "chassis_name",
            "weapon1_name", "weapon2_name", "backpack_name", "loot_table_name",
            "loot_table2_name", "title_name",
        ],
        "legend": [
            ("id", "`dbcharacter::Monster.id` - the character type id used by every spawn path."),
            ("name", "`localized_name_id` resolved through `dblocalization::LocalizedText`; `-` means the row has no English text (placeholder, cut content or internal variant) and can only be spawned by id."),
            ("faction", "`faction_id` resolved through `dbcharacter::Faction.internal_name`; drives hostility against the player (faction 1 `accord` is friendly, so those NPCs cannot be shot)."),
            ("race", "Census byte: 0 Human, 2 Chosen, 6 Misc (drones, targets, Necronus), 7 Companion/critter, 8 Melded, 9 Wildlife, 10 Outlaw, 11 Large wildlife."),
            ("chassis", "`chassis_id` -> `dbitems::Battleframe` / `dbitems::RootItem`: body, visuals and the jetpack/energy parameters PIN replicates. The CSV adds `chassis_name`."),
            ("weapons", "`weapon1_id` / `weapon2_id` -> `dbitems::Weapons`; `-` means the slot is empty. `weapon1_name` / `weapon2_name` are in the CSV."),
            ("behavior", "CAIS behavior set name - the short name only, the full parameter list (`Arch_MedRangedHumanoid_Base(triggerPullTime=1500,...)`) is in the CSV's `behavior` column. Empty means the row has no behavior and the NPC just stands there."),
            ("scaling", "`scaling_table_id` - `0` (nearly every row) means no scaling set. Per-level health/damage curves live in `dbcharacter::MonsterScaling`, listed in [README.md](README.md#4-monster-scaling-dbcharactermonsterscaling)."),
            ("loot tables", "`loot_table_id` / `loot_table2_id` -> `dbitems::LootTable` (names in the CSV)."),
            ("hp regen", "`health_regen`, out-of-combat regeneration."),
            ("spawn command", "The exact chat command. Drop the leading `\\` for the Admin channel or the server console, and append `<x> <y> <z>` for an explicit position."),
        ],
        "notes": [
            "Full `CharacterEntity.LoadMonster` path: chassis, warpaints, weapons, faction hostility, physics body and AI lifecycle.",
            "Movement/physics columns (`normal_speed`, `fast_speed`, `body_radius`, `body_mass`, `body_height`) are `-1` when the row inherits them from its chassis battleframe.",
            "Spawned mobs are kinematic physics bodies, so they are hittable; hits still need pose shape data from `Tools/CollisionGenerator` (see `Docs/SPAWNING_AND_COMBAT.md` §4).",
            "Default NPC health is 19192 and `HandleProjectileImpact` deals 1337 damage, so an unbuffed mob takes ~15 hits.",
        ],
        "group_by": [
            ("Faction", "faction", "Rows per `dbcharacter::Faction.internal_name` (`-` = no faction row)."),
            ("Race", "race_name", "Rows per census race byte."),
        ],
    },
    "deployable": {
        "doc": "DEPLOYABLES.md",
        "csv": "deployables.csv",
        "title": "Deployables",
        "table": "dbcharacter::Deployable",
        "name_column": "localized_name_id",
        "spawn_method": "`EntityManager.SpawnDeployable(typeId, position, orientation)`",
        "takes_position": True,
        "aliases": "`deployable`, `deployables`, `dep`",
        "legacy": "`deployable <id> [<x> <y> <z>]` (admin channel only)",
        "build": build_deployables,
        "md_columns": [
            ("id", "id"), ("name", "name"), ("category", "category_name"), ("function", "function_name"),
            ("faction", "faction"), ("health", "standard_health"), ("start hp", "start_hitpoints"),
            ("scale", "scale"), ("build ms", "build_time_ms"), ("spawn command", "spawn_command"),
        ],
        "csv_first": [
            "name", "spawn_command", "category_name", "category_description", "function_name",
            "function_description", "faction", "default_faction", "interact_text",
        ],
        "legend": [
            ("id", "`dbcharacter::Deployable.id` - the deployable type id."),
            ("name", "`localized_name_id` -> `dblocalization::LocalizedText`; `-` means the row has no English text and can only be spawned by id."),
            ("category", "`deployable_category` -> `dbcharacter::DeployableCategory.name` (Turret, Shield, Repair Station, Spawner, ...)."),
            ("function", "`function` -> `dbcharacter::DeployableFunction.name`: what the deployable is for in AI terms (Fixed Weapon, Ammo, Cover, Driver Seat, ...)."),
            ("faction", "`default_faction` -> `dbcharacter::Faction.internal_name`. `spawn` creates deployables unowned with this faction."),
            ("health", "`standard_health`. `start hp` is `start_hitpoints`, the value the entity actually starts with."),
            ("scale", "`scale`, the model scale multiplier."),
            ("build ms", "`build_time_ms`, how long the build/calldown takes."),
            ("spawn command", "The exact chat command. Drop the leading `\\` for the Admin channel or the server console, and append `<x> <y> <z>` for an explicit position."),
        ],
        "notes": [
            "Spawned unowned, with the row's `default_faction`; pass an owner through `EntityManager.SpawnDeployable` in code if you need one.",
            "Rows with `turret_type != 0` also spawn that `dbcharacter::Turret` as a child (see [TURRETS.md](TURRETS.md)).",
            "This is the largest spawnable table and it was never catalogued before: it contains every player-built structure, thumper, watchtower, seat, terminal, prop and test object in the game.",
        ],
        "group_by": [
            ("Category", "category_name", "Rows per `dbcharacter::DeployableCategory`."),
            ("Function", "function_name", "Rows per `dbcharacter::DeployableFunction`."),
            ("Faction", "faction", "Rows per default faction."),
        ],
    },
    "vehicle": {
        "doc": "VEHICLES.md",
        "csv": "vehicles.csv",
        "title": "Vehicles",
        "table": "vcs::VehicleInfo",
        "name_column": "localized_name_id",
        "spawn_method": "`EntityManager.SpawnVehicle(typeId, position, orientation, owner, autoMount: false)`",
        "takes_position": True,
        "aliases": "`vehicle`, `vehicles`, `veh`",
        "legacy": "`vehicle <id> [<x> <y> <z>]` (admin channel only)",
        "build": build_vehicles,
        "md_columns": [
            ("id", "id"), ("name", "name"), ("class", "class_name"), ("faction", "faction"),
            ("race", "race"), ("scaling", "scaling_table_id"), ("spawn command", "spawn_command"),
        ],
        "csv_first": ["name", "spawn_command", "class_name", "faction", "faction_id"],
        "legend": [
            ("id", "`vcs::VehicleInfo.id` (a `ushort` - `SDBCatalog` casts it)."),
            ("name", "`localized_name_id` -> `dblocalization::LocalizedText`. Every vehicle row is named."),
            ("class", "`vehicle_class` -> `vcs::VehicleClass.name`: HGV, LGV, Cargo, Dropship, Train, MGV, Battlecruiser."),
            ("faction", "`faction_id` -> `dbcharacter::Faction.internal_name`; `-` means faction 0 (none)."),
            ("race", "Raw `race` byte from the VCS record (the vehicle's own race tag, not the monster census race)."),
            ("scaling", "`scaling_table_id`, the vehicle stat scaling set."),
            ("spawn command", "The exact chat command. Drop the leading `\\` for the Admin channel or the server console, and append `<x> <y> <z>` for an explicit position."),
        ],
        "notes": [
            "The vehicle is owned by the calling character when there is one, and is never auto-mounted - walk up to it and press the use key.",
            "Components (seats, turrets, weapons) come from the VCS component tables; `spawn` only creates the vehicle entity itself.",
        ],
        "group_by": [
            ("Class", "class_name", "Rows per `vcs::VehicleClass`."),
            ("Faction", "faction", "Rows per faction."),
        ],
    },
    "carryable": {
        "doc": "CARRYABLES.md",
        "csv": "carryables.csv",
        "title": "Carryables",
        "table": "dbitems::CarryableObject",
        "name_column": "localized_name_id",
        "spawn_method": "`EntityManager.SpawnCarryable(typeId, position)`",
        "takes_position": True,
        "aliases": "`carryable`, `carryables`, `carry`",
        "legacy": "`carryable <id> [<x> <y> <z>]` (admin channel only)",
        "build": build_carryables,
        "md_columns": [
            ("id", "id"), ("name", "name"), ("type", "type"), ("pickup radius", "pickup_radius"),
            ("thrown radius", "thrown_pickup_radius"), ("picked up by", "pickup_by"),
            ("cooldown ms", "pickup_cooldown"), ("visual record", "visual_record_id"),
            ("spawn command", "spawn_command"),
        ],
        "csv_first": [
            "name", "spawn_command", "description", "interact_text", "pickup_by",
            "ability_granted_name",
        ],
        "legend": [
            ("id", "`dbitems::CarryableObject.id`."),
            ("name", "`localized_name_id` -> `dblocalization::LocalizedText`; the unnamed rows are internal placeholders reachable only by id."),
            ("type", "Raw `type` value: 1 = objective/item pickup, 2 = ball/sports prop (the two values that actually occur)."),
            ("pickup radius", "`pickup_radius` in metres; `thrown radius` is `thrown_pickup_radius`."),
            ("picked up by", "`pickup_by_interaction`: `interact` (hold the use key for `interaction_time_ms`) or `touch` (walking over it is enough)."),
            ("cooldown ms", "`pickup_cooldown` before the same character can pick another one up."),
            ("visual record", "`visual_record_id` -> `dbvisualrecords::VisualRecord`, the model used."),
            ("spawn command", "The exact chat command. Drop the leading `\\` for the Admin channel or the server console. Carryables take a position but no orientation."),
        ],
        "notes": [
            "Carryables have no orientation - `SpawnCarryable` only takes a position.",
            "Pickup rules (`allow_friendly_pickup`, `allow_hostile_pickup`, `max_per_character`, `is_exclusive`, status effects) are all in the CSV.",
        ],
        "group_by": [
            ("Type", "type", "Rows per `type` value."),
        ],
    },
    "turret": {
        "doc": "TURRETS.md",
        "csv": "turrets.csv",
        "title": "Turrets",
        "table": "dbcharacter::Turret",
        "name_column": "name",
        "spawn_method": "`EntityManager.SpawnTurret(typeId, parent)` - child entity, needs a parent",
        "takes_position": False,
        "aliases": "`turret`, `turrets`",
        "legacy": "none - `spawn turret` is the only way to create one from a command",
        "build": build_turrets,
        "md_columns": [
            ("id", "id"), ("name", "name"), ("posture", "posture"), ("attack type", "attack_type"),
            ("behavior", "behavior"), ("pitch", "pitch"), ("yaw", "yaw"), ("weapons", "weapons"),
            ("spawn command", "spawn_command"),
        ],
        "csv_first": ["name", "spawn_command", "weapon_names", "weapons"],
        "legend": [
            ("id", "`dbcharacter::Turret.id` - the turret type id."),
            ("name", "Plain text `name` column (turrets are the one spawnable table that is not localized)."),
            ("posture", "`posture` byte sent to the client as the gunner posture (2 = standing for most rows)."),
            ("attack type", "`attack_type` byte (1 for nearly every row)."),
            ("behavior", "CAIS behavior set name; some rows store a numeric id here."),
            ("pitch", "`min_pitch` .. `max_pitch` in **radians** (`1.5708` = 90 deg); `-1` is the 'no limit set' marker in this table."),
            ("yaw", "`min_yaw` .. `max_yaw` in **radians** (`6.2832` = full 360 deg traversal)."),
            ("weapons", "`dbcharacter::TurretWeapon.weapon_id` rows for this turret; `weapon_names` in the CSV."),
            ("spawn command", "The exact chat command. Turrets attach to the calling player's character, so this one is refused from the server console."),
        ],
        "notes": [
            "Turrets are **child** entities: `spawn turret` attaches the turret to the calling player's character and is refused when there is no character (server console).",
            "Deployables and vehicles reference turrets through `turret_type`; spawning those creates the turret automatically.",
        ],
        "group_by": [
            ("Posture", "posture", "Rows per `posture` byte."),
            ("Attack type", "attack_type", "Rows per `attack_type` byte."),
        ],
    },
}


# --------------------------------------------------------------------------- #
# document writers
# --------------------------------------------------------------------------- #

GENERATED_BANNER = (
    "> **Generated file** - do not edit by hand. Regenerate with "
    "`python3 Tools/SdbDump/spawn_reference.py <clientdb.sd2>` (see "
    "[README.md](README.md#5-regenerating-this-folder))."
)


def breakdown_tables(ctx, kind, rows):
    """`group_by` specs -> list of (heading, intro, markdown table)."""
    out = []
    for heading, key, intro in SPEC[kind]["group_by"]:
        buckets = {}
        for row in rows:
            label = fmt(row.get(key), empty="-") or "-"
            bucket = buckets.setdefault(label, [0, 0])
            bucket[0] += 1
            if row.get("name"):
                bucket[1] += 1
        ordered = sorted(buckets.items(), key=lambda item: (-item[1][0], item[0]))
        table = md_table(
            [heading.lower(), "rows", "named"],
            [[cell(label), count(total), count(named)] for label, (total, named) in ordered],
        )
        out.append((f"By {heading.lower()}", intro, table))
    return out


PLACEHOLDER_NAME = re.compile(
    r"(?i)(^|\s)(test|debug|unused|placeholder|tmp|temp|copy of|old|new)($|\s)|^_|^no monster$")


def example_rows(rows):
    """First named rows that are not obviously placeholders, for the doc examples."""
    named = [row for row in rows if row.get("name")]
    clean = [row for row in named if not PLACEHOLDER_NAME.search(row["name"]) and not row["name"].isupper()]
    return (clean or named)[:2]


def syntax_block(kind, spec):
    """The four commands of this kind, comment-aligned."""
    position = " [<x> <y> <z>]" if spec["takes_position"] else ""
    lines = [
        (f"\\spawn {kind} <id|name>{position}", "chat command (note the backslash)"),
        (f"spawn {kind} <id|name>{position}",
         "Admin channel / server console" if spec["takes_position"]
         else "Admin channel (needs a player character)"),
        (f"\\sdb {kind} <filter> [limit]", "search this table in-game"),
        (f"\\sdbinfo {kind} <id|name>", "every field of one row"),
    ]
    width = max(len(command) for command, _ in lines) + 2
    return [f"{command:<{width}}# {comment}" for command, comment in lines]


def examples_block(ctx, kind, rows):
    """Copy-pasteable examples built from real rows, so they cannot go stale."""
    named = example_rows(rows)
    if not named:
        return ""
    first, second = named[0], named[min(1, len(named) - 1)]
    takes_position = SPEC[kind]["takes_position"]
    third = (
        (f"\\spawn {kind} {second['id']} -25.5 118 492", f"{second['name']} at an explicit position")
        if takes_position else
        (f"\\spawn {kind} {second['name']}", f"{second['name']} - by name, attaches to you")
    )
    lines = [
        (f"\\spawn {kind} {first['id']}",
         f"{first['name']} - by id" + (", at your feet" if takes_position else "")),
        (f"\\spawn {kind} {first['name']}", "the same row, by name"),
        third,
        (f"\\sdbinfo {kind} {first['id']}", "every field of that row"),
        (f"\\sdb {kind} {first['name'].split()[0].lower()} 20", "search this table in-game"),
    ]
    width = max(len(command) for command, _ in lines) + 2
    return "```\n" + "\n".join(f"{command:<{width}}# {comment}" for command, comment in lines) + "\n```"


def write_kind_doc(ctx, kind, rows, out_dir):
    spec = SPEC[kind]
    named = sum(1 for row in rows if row.get("name"))
    unnamed = len(rows) - named
    headers = [header for header, _ in spec["md_columns"]]
    keys = [key for _, key in spec["md_columns"]]

    raw_columns = len(ctx.raw.get(kind, [{}])[0]) if ctx.raw.get(kind) else 0
    named_text = (
        f" {count(named)} of them have an English name and can be spawned by name or id; the "
        f"{count(unnamed)} unnamed rows are real and spawnable, but can only be referenced by id."
        if unnamed else f" All {count(named)} of them are named, so every row can be spawned by name or id."
    )

    multi_word = next((row["name"] for row in example_rows(rows) if " " in row["name"]), None)
    multi_word_example = f"\\spawn {kind} {multi_word}" if multi_word else f"\\spawn {kind} <name>"

    parts = [f"# {spec['title']} — full spawn reference", ""]
    parts += [
        f"Every one of the **{count(len(rows))}** rows of `{spec['table']}` that PIN can spawn, "
        f"with the exact command for each.{named_text}",
        "",
        GENERATED_BANNER,
        "",
        f"Decoded from Firefall build **{ctx.patch}**. Index, faction table and CSV notes: "
        "[README.md](README.md). How the commands are implemented: "
        "[../STATIC_DATABASE.md](../STATIC_DATABASE.md#4-spawning-from-the-database-in-game).",
        "",
        "---",
        "",
        "## 1. Spawning one of these",
        "",
        "```",
        *syntax_block(kind, spec),
        "```",
        "",
        f"- Kind aliases accepted in place of `{kind}`: {spec['aliases']}.",
        f"- Spawn path: {spec['spawn_method']}.",
        f"- Older typed command: {spec['legacy']}.",
        ("- Omit `<x> <y> <z>` and the entity spawns at your character's position with your "
         "orientation; from the server console a position is required."
         if spec["takes_position"] else
         "- Turrets take no position: they are child entities and always attach to the calling "
         "player's character, which is also why the server console cannot spawn one."),
        "- Names are matched case-insensitively (exact beats prefix beats substring) and do not "
        f"need quoting, so multi-word names work: `{multi_word_example}`.",
        "",
        "Examples, built from the first rows of this table:",
        "",
        examples_block(ctx, kind, rows),
        "",
        "## 2. Column reference",
        "",
        md_table(["column", "meaning"], [[f"`{header}`", text.replace("|", "\\|")] for header, text in spec["legend"]]),
        "",
        f"The table in §5 is the readable subset. **Every** column of the SDB row - all "
        f"{raw_columns} of them, plus the resolved names - is in "
        f"[csv/{spec['csv']}](csv/{spec['csv']}).",
        "",
        "## 3. Notes",
        "",
    ]
    parts += [f"- {note}" for note in spec["notes"]]
    parts += ["", "## 4. Breakdown", ""]
    for heading, intro, table in breakdown_tables(ctx, kind, rows):
        parts += [f"### {heading}", "", intro, "", table, ""]

    parts += [
        f"## 5. All {count(len(rows))} rows",
        "",
        f"Sorted by id. `{headers[-1]}` is ready to copy into the chat window.",
        "",
        md_table(headers, [[cell(row.get(key)) for key in keys] for row in rows]),
        "",
        "---",
        "",
        "Regenerate: `python3 Tools/SdbDump/spawn_reference.py <clientdb.sd2>` - see "
        "[README.md](README.md). Related: [../MOBS_AND_NPCS.md](../MOBS_AND_NPCS.md) (mobs grouped by "
        "faction, with the anatomy of a monster row), "
        "[../SPAWNING_AND_COMBAT.md](../SPAWNING_AND_COMBAT.md) (what happens after the spawn), "
        "[../STATIC_DATABASE.md](../STATIC_DATABASE.md) (the file format and the commands).",
        "",
    ]

    path = os.path.join(out_dir, spec["doc"])
    with open(path, "w", encoding="utf-8", newline="\n") as handle:
        handle.write("\n".join(parts))
    return path


def write_kind_csv(ctx, kind, rows, out_dir):
    spec = SPEC[kind]
    if not rows:
        return None
    raw_keys = sorted({key for row in rows for key in row if key not in spec["csv_first"] and key != "id"})
    fieldnames = ["id"] + [key for key in spec["csv_first"] if key in rows[0]] + \
                 [key for key in raw_keys if key in rows[0] or any(key in row for row in rows)]
    path = os.path.join(out_dir, "csv", spec["csv"])
    os.makedirs(os.path.dirname(path), exist_ok=True)
    with open(path, "w", encoding="utf-8", newline="") as handle:
        writer = csv.writer(handle, lineterminator="\n")
        writer.writerow(fieldnames)
        for row in rows:
            writer.writerow([fmt(row.get(key)) for key in fieldnames])
    return path


def write_index(ctx, written, out_dir):
    total_rows = sum(len(rows) for rows in ctx.rows.values())
    total_named = sum(sum(1 for row in rows if row.get("name")) for rows in ctx.rows.values())

    index_rows = []
    for kind in KIND_ORDER:
        if kind not in ctx.rows:
            continue
        spec = SPEC[kind]
        rows = ctx.rows[kind]
        named = sum(1 for row in rows if row.get("name"))
        index_rows.append([
            f"[{spec['title']}]({spec['doc']})", f"`{kind}`", f"`{spec['table']}`",
            count(len(rows)), count(named), count(len(rows) - named),
            f"[csv/{spec['csv']}](csv/{spec['csv']})" if written.get(kind, {}).get("csv") else "-",
        ])

    mobs_per_faction = {}
    for row in ctx.rows.get("monster", []):
        key = row.get("faction_id") or 0
        mobs_per_faction[key] = mobs_per_faction.get(key, 0) + 1

    faction_rows = []
    for faction in sorted(ctx.factions.values(), key=lambda item: item["id"]):
        stance = faction["default_stance"]
        stance_text = "hostile by default" if isinstance(stance, int) and stance <= -1 else "neutral"
        faction_rows.append([
            cell(faction["id"]), cell(faction["internal_name"]), cell(faction["name"]),
            cell(faction["abbrev"]), cell(stance_text),
            count(mobs_per_faction.get(faction["id"], 0)) if "monster" in ctx.rows else "-",
        ])
    faction_headers = ["id", "internal name", "display name", "abbrev", "default stance", "mob rows"]
    if "monster" not in ctx.rows:
        faction_headers = faction_headers[:-1]
        faction_rows = [row[:-1] for row in faction_rows]

    scaling_rows = [[cell(level), cell(value.get("health")), cell(value.get("damage"))]
                    for level, value in sorted(ctx.scaling.items())]

    parts = [
        "# Spawnable Reference",
        "",
        f"**Every spawnable row in Firefall's static database, with the exact command that creates "
        f"it.** {count(total_rows)} rows across {len(ctx.rows)} tables - {count(total_named)} of them "
        "named - decoded from `clientdb.sd2` build "
        f"**{ctx.patch}** and written by `Tools/SdbDump/spawn_reference.py`.",
        "",
        GENERATED_BANNER,
        "",
        "One document per spawnable kind, each a flat spreadsheet of the whole table sorted by id:",
        "",
        md_table(
            ["document", "kind", "SDB table", "rows", "named", "unnamed", "CSV"],
            index_rows,
        ),
        "",
        "The tables mirror `UdpHosts/GameServer/StaticDB/SDBCatalog.cs` and the commands mirror "
        "`Systems/Spawning/SDBSpawner.cs`, so what is listed here is exactly what the in-game "
        "`sdb` / `sdbinfo` / `spawn` commands see. Anything in these documents can be spawned "
        "straight away - no code changes, no JSON, no config.",
        "",
        "---",
        "",
        "## 1. The command",
        "",
        "```",
        "\\spawn <kind> <id|name> [<x> <y> <z>]     # chat window (the backslash is the command prefix)",
        "spawn <kind> <id|name> [<x> <y> <z>]      # Admin chat channel, or the server console",
        "```",
        "",
        "| part | rules |",
        "|------|-------|",
        "| `<kind>` | `monster` \\| `deployable` \\| `vehicle` \\| `carryable` \\| `turret` (aliases below) |",
        "| `<id\\|name>` | a numeric row id, or a name - case-insensitive, no quoting needed, multi-word names work. Exact match beats prefix match beats substring match; ambiguous names are rejected with the candidate list. |",
        "| `[<x> <y> <z>]` | optional position, parsed with invariant culture (`-25.5 118 492`). Omit it to spawn at your character's position; required from the server console. |",
        "",
        "`turret` is the one exception: turrets are child entities, so `spawn turret <id|name>` takes "
        "no position, always attaches to the calling player's character, and is refused from the "
        "server console. See [TURRETS.md](TURRETS.md).",
        "",
        "Kind aliases (all case-insensitive):",
        "",
        md_table(
            ["kind", "accepted words"],
            [[f"`{kind}`", SPEC[kind]["aliases"]] for kind in KIND_ORDER if kind in ctx.rows],
        ),
        "",
        "Discovery and inspection - the same kinds, no id needed:",
        "",
        "```",
        "\\sdb                                     # row counts per kind",
        "\\sdb <kind>                              # first 20 rows",
        "\\sdb <kind> <filter> [limit]             # search by name or exact id (limit max 200)",
        "\\sdbinfo <kind> <id|name>                # every interesting field of one row",
        "```",
        "",
        "`sdb` / `sdbinfo` print to the **client console** (multi-line output), not the chat line. "
        "Command aliases: `spawn`/`sdbspawn`/`spawn_sdb`, `sdb`/`sdblist`/`sdbsearch`/`sdbfind`, "
        "`sdbinfo`/`sdbshow`/`sdbrow`.",
        "",
        "Older typed commands, still available and unchanged:",
        "",
        md_table(
            ["kind", "typed command", "where"],
            [
                ["`monster`", "`\\npc <id> [<x> <y> <z>]`", "chat + admin (aliases `character`, `monster`, `spawn_npc`, `spawn_character`, `spawn_monster`)"],
                ["`deployable`", "`deployable <id> [<x> <y> <z>]`", "admin only (alias `spawn_deployable`)"],
                ["`vehicle`", "`vehicle <id> [<x> <y> <z>]`", "admin only (alias `spawn_vehicle`)"],
                ["`carryable`", "`carryable <id> [<x> <y> <z>]`", "admin only (alias `spawn_carryable`)"],
                ["`turret`", "- none -", "`spawn turret` is the only command that creates one"],
            ],
        ),
        "",
        "The typed commands take ids only; `spawn` adds name resolution, discovery, turrets and one "
        "consistent syntax.",
        "",
        "## 2. Reading the tables",
        "",
        "- `-` in a cell means \"empty / not set\". For monster movement and body columns "
        "(`normal_speed`, `body_radius`, ...) `-1` means *inherit from the chassis battleframe*.",
        "- Unnamed rows (`name` = `-`) are placeholders, cut content or internal variants. They are "
        "real, spawnable rows, but they can only be referenced by id and `spawn by name` will not "
        "find them.",
        "- Ids are the game's own type ids: the same numbers `character_spawn.json`, `\\npc` and the "
        "entity views use.",
        "- Foreign keys are given as ids in the Markdown tables and as ids **plus** resolved names in "
        "the CSVs (chassis, weapons, loot tables, categories, functions, abilities).",
        "- A value rendered as `3.40282e+38` is the C++ `FLT_MAX` sentinel the data uses for "
        "\"no limit\" (one cell in this build: `health_regen` of monster 3242).",
        "",
        "## 3. Factions (`dbcharacter::Faction`)",
        "",
        "Faction decides whether a spawned entity will fight you: the player defaults to faction "
        "`1` (`accord`), `CombatSim` drops hits on `Friendly`/`Self` stances, and unknown relations "
        "fall back to `Neutral` (which passes). `default_stance <= -1` marks a faction that is "
        "hostile unless a `dbcharacter::FactionRelations` row says otherwise.",
        "",
        md_table(faction_headers, faction_rows),
        "",
        "## 4. Monster scaling (`dbcharacter::MonsterScaling`)",
        "",
        f"{count(len(ctx.scaling))} levels; `SDBInterface.GetMonsterScaling(level)` is keyed by level.",
        "",
        md_table(["level", "health", "damage"], scaling_rows),
        "",
        "## 5. Regenerating this folder",
        "",
        "```sh",
        "# the reference database (split zip, extracted outside Git)",
        "cat Tools/clientdb.zip.001 Tools/clientdb.zip.002 > /tmp/clientdb.zip",
        "unzip -o /tmp/clientdb.zip -d /tmp/sdb",
        "",
        "# rewrite every document + CSV in this folder",
        "python3 Tools/SdbDump/spawn_reference.py /tmp/sdb/clientdb.sd2",
        "",
        "# options",
        "python3 Tools/SdbDump/spawn_reference.py /tmp/sdb/clientdb.sd2 \\",
        "    --out-dir Docs/SpawnReference --kinds monster,turret --no-csv",
        "```",
        "",
        "The script needs nothing but Python 3 (no Firefall installation): it imports the "
        "`sdb_dump.py` decoder, harvests table/column names from PIN's own source and decrypts only "
        "the localized strings the spawnable rows reference, so a full run takes well under a minute.",
        "",
        "## 6. CSV files",
        "",
        "`csv/` holds the same rows with **every** column of the SDB record plus the resolved names - "
        "the actual spreadsheet, for filtering in Excel/LibreOffice or diffing between builds:",
        "",
        md_table(
            ["file", "kind", "columns"],
            [[f"[csv/{SPEC[kind]['csv']}](csv/{SPEC[kind]['csv']})", f"`{kind}`",
              count(len(written[kind]["csv_columns"]))]
             for kind in KIND_ORDER if written.get(kind, {}).get("csv")],
        ),
        "",
        "Column order is `id`, then the resolved names, then every raw SDB column alphabetically. "
        "Values are rendered the same way as in the Markdown tables (vectors as `(x, y, z)`, "
        "integral floats without decimals).",
        "",
        "## 7. Related documents",
        "",
        "- [../STATIC_DATABASE.md](../STATIC_DATABASE.md) - the `.sd2` file format, PIN's coverage of it, and how `spawn`/`sdb`/`sdbinfo` are implemented.",
        "- [../MOBS_AND_NPCS.md](../MOBS_AND_NPCS.md) - anatomy of a monster row, mobs grouped by faction, how PIN turns a row into an entity.",
        "- [../SPAWNING_AND_COMBAT.md](../SPAWNING_AND_COMBAT.md) - replication, combat gating, per-zone `character_spawn.json`.",
        "- [../CHARACTERS_AND_BATTLEFRAMES.md](../CHARACTERS_AND_BATTLEFRAMES.md) - the player side (`characters.json`).",
        "- [../HEALTH_SYSTEM.md](../HEALTH_SYSTEM.md) - health, damage, death and respawn.",
        "- [../../Tools/SdbDump/README.md](../../Tools/SdbDump/README.md) - the decoder these documents are generated with.",
        "",
    ]

    path = os.path.join(out_dir, "README.md")
    with open(path, "w", encoding="utf-8", newline="\n") as handle:
        handle.write("\n".join(parts))
    return path


# --------------------------------------------------------------------------- #

def main(argv=None):
    parser = argparse.ArgumentParser(
        description="Generate the Docs/SpawnReference catalog (Markdown + CSV) from a clientdb.sd2")
    parser.add_argument("sdb", help="path to clientdb.sd2")
    parser.add_argument("--out-dir", default=DEFAULT_OUT_DIR,
                        help="output folder (default: Docs/SpawnReference)")
    parser.add_argument("--game-dir", default=DEFAULT_GAME_DIR,
                        help="PIN GameServer source dir for table/column names (default: auto)")
    parser.add_argument("--names", help="extra candidate table-name list (one per line)")
    parser.add_argument("--kinds", default=",".join(KIND_ORDER),
                        help=f"comma separated subset of: {', '.join(KIND_ORDER)}")
    parser.add_argument("--no-csv", action="store_true", help="skip the csv/ files")
    args = parser.parse_args(argv)

    kinds = [kind.strip() for kind in args.kinds.split(",") if kind.strip()]
    unknown = [kind for kind in kinds if kind not in SPEC]
    if unknown:
        parser.error(f"unknown kind(s) {', '.join(unknown)}; expected {', '.join(KIND_ORDER)}")
    kinds = [kind for kind in KIND_ORDER if kind in kinds]

    out_dir = os.path.abspath(args.out_dir)
    os.makedirs(out_dir, exist_ok=True)

    db = open_db(args.sdb, args.game_dir, args.names)
    ctx = Context(db)

    load_lookups(ctx)
    load_kind_rows(ctx, kinds)
    load_item_names(ctx)
    finalize_names(ctx)

    written = {}
    for kind in kinds:
        rows = SPEC[kind]["build"](ctx)
        ctx.rows[kind] = rows
        if not rows:
            continue
        doc = write_kind_doc(ctx, kind, rows, out_dir)
        csv_path = None if args.no_csv else write_kind_csv(ctx, kind, rows, out_dir)
        written[kind] = {"doc": doc, "csv": csv_path,
                         "csv_columns": []}
        if csv_path:
            with open(csv_path, "r", encoding="utf-8") as handle:
                written[kind]["csv_columns"] = next(csv.reader(handle))
        named = sum(1 for row in rows if row.get("name"))
        print(f"wrote {os.path.relpath(doc, out_dir)}: {len(rows)} rows ({named} named)"
              + (f" + csv/{SPEC[kind]['csv']}" if csv_path else ""), file=sys.stderr)

    index = write_index(ctx, written, out_dir)
    print(f"wrote {os.path.relpath(index, out_dir)} ({len(ctx.rows)} kinds, "
          f"{sum(len(rows) for rows in ctx.rows.values())} rows)", file=sys.stderr)
    return 0


if __name__ == "__main__":
    sys.exit(main())
