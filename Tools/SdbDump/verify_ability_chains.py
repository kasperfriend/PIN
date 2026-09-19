#!/usr/bin/env python3
"""Battleframe ability verification for the PIN server pipeline.

Walks the same data path the GameServer walks at ability activation:

    stock loadout slot module (dbitems::AbilityModule.ability_chain_id)
      -> apt::AbilityData.chain
      -> apt::BaseCommandDef (id ->subtype-> Next linked list)
      -> apt::CommandType (subtype -> name, environment)

and classifies every chain node's command type against the Factory's
switch (enabled case / commented-out case / no case -> placeholder).

The per-frame module inventory comes from
Lib/Shared.Common/Characters/ChassisStockLoadouts.cs (the committed
server-side stock loadout table, itself generated from this same db).

Usage:
    python3 Tools/SdbDump/verify_ability_chains.py [--sdb PATH] [--check] [--names] [--out PATH]

Without --sdb the bundled Tools/clientdb.zip.001/.002 is used.
--check exits non-zero when a chain placeholder appears that is not in the
documented allowlist below (a regression tripwire for the factory routing).
"""

import argparse
import json
import os
import re
import sys
import tempfile
import zipfile
from collections import defaultdict
from pathlib import Path

sys.path.insert(0, os.path.dirname(os.path.abspath(__file__)))
import sdb_dump

REPO = str(Path(__file__).resolve().parents[2])

# Documented remaining gaps, keyed by command type to the ability ids whose
# chains are allowed to hold a placeholder node of it (snapshot of the 2026-09
# audit). Fixing one of these makes the entry redundant - drop it then.
# A NEW pair here means the factory lost a route or an ability started using a
# command type the server does not implement. The server-side story:
#
#   ActivateAbilityTrigger / RegisterAbilityTrigger
#       The ability trigger subsystem. Both defs are id-only (no loadable
#       parameters) and no client<->server trigger message flow exists in the
#       AeroMessages tree, so registration/firing cannot be implemented from
#       the available evidence; ~29 player-facing abilities reference them
#       (several HKM/accel/melee rows and most frame passives).
#   TinyObjectCreate
#       Id-only def, and the tiny-object entity family is not modeled
#       ('OLD Emergency Response' is also a superseded ability row).
#   RequireItemDurability
#       Only a static char-level CurrentDurabilityPctProp exists; per-slot
#       item wear is not modeled (PvP-frame rows only).
ALLOWED_PLACEHOLDERS = {
    # Trigger subsystem: id-only defs, no client<->server trigger message flow
    # exists in AeroMessages, so registration/firing is not implementable from
    # the available evidence.
    "ActivateAbilityTrigger": {35448, 35455, 35535, 35540, 39243, 39257, 39360, 39405, 39423,
                               39426, 39434, 39844, 39845, 39846, 40487, 41022, 41758, 41881},
    "RegisterAbilityTrigger": {35348, 35465, 35501, 35509, 35582, 35613, 35621, 35630, 35879,
                               36268, 41850},
    # Id-only def; the tiny-object entity family is not modeled.
    "TinyObjectCreate": {35445},
    # Only a static char-level durability prop exists; per-slot wear is not modeled.
    "RequireItemDurability": {39847},
}


def open_db(sdb_path):
    if sdb_path is None:
        archive = b"".join((Path(REPO) / "Tools" / f"clientdb.zip.{part:03d}").read_bytes() for part in (1, 2))
        tmp = tempfile.NamedTemporaryFile(suffix=".zip", delete=False)
        tmp.write(archive)
        tmp.close()
        with zipfile.ZipFile(tmp.name) as zipped:
            sdb_path = str(Path(tempfile.mkdtemp()) / "clientdb.sd2")
            Path(sdb_path).write_bytes(zipped.read("clientdb.sd2"))
        os.unlink(tmp.name)
    db = sdb_dump.StaticDB(sdb_path)
    db.resolve_names(sdb_dump.harvest_record_table_names(f"{REPO}/UdpHosts/GameServer"))
    _, col_by_hash = sdb_dump.harvest_pin_names(f"{REPO}/UdpHosts/GameServer")
    sdb_dump.apply_column_names(db, col_by_hash)
    return db


def table_map(db, name, key="id"):
    table = db.find_table(name)
    if table is None:
        raise SystemExit(f"error: table {name!r} not in db")
    out = {}
    for row in db.rows(table):
        if key in row and row[key] is not None:
            out[row[key]] = row
    return out


def main():
    parser = argparse.ArgumentParser(description=__doc__)
    parser.add_argument("sdb", nargs="?", default=None,
                        help="path to clientdb.sd2 (default: bundled Tools/clientdb.zip.001/.002)")
    parser.add_argument("--check", action="store_true",
                        help="exit 1 when a placeholder outside the documented allowlist appears")
    parser.add_argument("--names", action="store_true",
                        help="also resolve localized ability names (slower: decrypts only the needed dblocalization strings)")
    parser.add_argument("--out", metavar="PATH", default=None,
                        help="write the per-frame report JSON here")
    args = parser.parse_args()

    db = open_db(args.sdb)
    modules = table_map(db, "dbitems::AbilityModule")
    abilities = table_map(db, "apt::AbilityData")
    basedefs = table_map(db, "apt::BaseCommandDef")
    cmdtypes = table_map(db, "apt::CommandType")

    # --- Factory routing census -------------------------------------------------
    factory = open(f"{REPO}/UdpHosts/GameServer/Systems/Aptitude/Factory.cs").read()
    enum_src = open(f"{REPO}/UdpHosts/GameServer/Systems/Aptitude/CommandType.cs").read()
    id_to_name = {int(i): n for n, i in re.findall(r"^\s{4}(\w+)\s*=\s*(\d+),", enum_src, re.M)}
    enabled = set(re.findall(r"^\s{12}case CommandType\.(\w+):", factory, re.M))
    disabled = set(re.findall(r"^\s*//\s*case CommandType\.(\w+):", factory, re.M))
    enabled -= disabled

    # Deliberately a chain-side no-op: the combat controller that owns the activation
    # sends the AbilityActivated ack itself; re-running the row would double-send.
    BY_DESIGN_NOOP = {"ActiveInitiation"}
    print(f"Factory: {len(enabled)} enabled command types, {len(disabled)} commented, enum {len(id_to_name)} members")

    # --- Chain walker -----------------------------------------------------------
    ENV_LABEL = {}

    def env_of(type_id):
        rec = cmdtypes.get(type_id)
        if rec is None:
            return "?"
        v = rec.get("environment")
        return v if isinstance(v, str) else {0: "both", 1: "server", 2: "client"}.get(v, str(v))

    def walk_chain(chain_id):
        """Yield (node_id, command-type name, env) along the linked list."""
        seen = set()
        cur = chain_id
        hops = 0
        while cur and cur not in seen:
            seen.add(cur)
            hops += 1
            if hops > 500:
                yield (cur, "<CYCLE/OVERRUN>", "!")
                return
            node = basedefs.get(cur)
            if node is None:
                yield (cur, f"<MISSING Node={cur}>", "!")
                return
            subtype = node["subtype"]
            rec = cmdtypes.get(subtype)
            enum = id_to_name.get(subtype)
            if rec is None:
                name = f"<UNKNOWN TYPE {subtype}>"
            elif enum is None:
                name = f"{rec['tblname']}<{subtype}:no-enum>"
            else:
                name = enum
            yield (cur, name, env_of(subtype))
            cur = node["next"]

    def chain_health(chain_id):
        """(total, noop_client, [(node,type,env) placeholders]) for the linked list.

        A node is a REAL gap when its command type runs in server environment
        ('server' or 'both') and the factory does not route it; client-only
        nodes are server-side no-ops by design (CustomNOOPCommand).
        """
        total, noop_client, placeholder = 0, 0, []
        for node_id, name, env in walk_chain(chain_id):
            if name.startswith("<"):
                placeholder.append((node_id, name, env))
                continue
            total += 1
            if env == "client":
                noop_client += 1
                continue
            short = name.split("<", 1)[0]
            if short in BY_DESIGN_NOOP:
                noop_client += 1
                continue
            if short not in enabled:
                placeholder.append((node_id, name, env))
        return total, noop_client, placeholder

    # --- Per-frame inventory ----------------------------------------------------
    src = open(f"{REPO}/Lib/Shared.Common/Characters/ChassisStockLoadouts.cs").read()
    frames = []
    blocks = re.split(r"\[\d+u\] = new ChassisStockLoadout", src)
    keys = re.findall(r"\[(\d+)u\] = new ChassisStockLoadout", src)
    for key, body in zip(keys, blocks[1:]):
        display = re.search(r"DisplayName = \"([^\"]+)\"", body)
        name = re.search(r"\bName = \"([^\"]+)\"", body)
        slots = [(int(a), int(b)) for a, b in re.findall(r"SlotType = (\d+), PveModule = (\d+)u", body)]
        frames.append((display.group(1) if display else (name.group(1) if name else f"chassis {key}"), key, name.group(1) if name else "", slots))

    ALL_CMD_IN_USE = defaultdict(set)  # cmd short name -> frames using it (placeholder paths)

    report = []
    for display, chassis, lo_name, slots in frames:
        frame_entry = {"frame": display, "chassis": chassis, "loadout": lo_name, "slots": []}
        for slot_type, item in slots:
            mod = modules.get(item)
            if mod is None:
                # Not an ability module (gear, weapons, gear items) -> not this audit's subject.
                continue
            ability_id = mod["ability_chain_id"]
            entry = {
                "slot": slot_type,
                "module": item,
                "ability_id": ability_id,
                "flags": f"pve={mod['activatable_in_pve']} pvp={mod['activatable_in_pvp']} adventure={mod['activatable_in_adventure']}",
            }
            if ability_id == 0:
                entry["status"] = "NO-ABILITY"
            else:
                ab = abilities.get(ability_id)
                if ab is None:
                    entry["status"] = "MISSING-ABILITY-ROW"
                elif ab["chain"] == 0:
                    entry["status"] = "NO-CHAIN"
                else:
                    total, noop_client, placeholder = chain_health(ab["chain"])
                    entry["chain_id"] = ab["chain"]
                    entry["commands"] = total
                    entry["client_noop"] = noop_client
                    if placeholder:
                        entry["status"] = "PLACEHOLDERS"
                        entry["placeholders"] = [
                            {"node": n, "type": t, "env": e} for n, t, e in placeholder
                        ]
                        for _, t, _ in placeholder:
                            ALL_CMD_IN_USE[t].add(display)
                    else:
                        entry["status"] = "OK"
            frame_entry["slots"].append(entry)
        report.append(frame_entry)

    # --- Aggregate: abilities trans-(frame) inventory ---------------------------
    all_used_modules = {item for _, _, _, slots in frames for _, item in slots}
    mm = [m for i, m in modules.items() if i in all_used_modules]

    if args.names:
        want = {abilities[s["ability_id"]]["localized_name_id"]
                for f in report for s in f["slots"] if s["ability_id"] in abilities}
        name_map = sdb_dump._localized(db, only_ids=want)
        for f in report:
            for s in f["slots"]:
                ref = abilities.get(s["ability_id"])
                if ref is not None:
                    s["name"] = name_map.get(ref["localized_name_id"], "?")

    if args.out:
        with open(args.out, "w") as fh:
            json.dump(report, fh, indent=1)

    # --- Console summary ----------------------------------------------------------
    ok = placeholders = nochain = miss = 0
    for f in report:
        print(f"\n=== {f['frame']} (chassis {f['chassis']})")
        for s in f["slots"]:
            tag = s["status"]
            icons = {"OK": "ok ", "PLACEHOLDERS": "PH ", "NO-ABILITY": "n/a", "NO-CHAIN": "no1", "MISSING-ABILITY-ROW": "no2"}
            label = f" '{s['name']}'" if s.get("name") and s["name"] != "?" else ""
            line = f"  [{icons.get(tag, '?? ')}] slot {s['slot']:>2} module {s['module']:>6} ability {s['ability_id']:>6}{label} ({s['flags']})"
            if "chain_id" in s:
                line += f" chain {s['chain_id']} nodes {s['commands']}"
            print(line)
            for ph in s.get("placeholders", []):
                print(f"       placeholder node {ph['node']} type {ph['type']} env={ph['env']}")
            if tag == "OK":
                ok += 1
            elif tag == "PLACEHOLDERS":
                placeholders += 1
            elif tag == "NO-CHAIN":
                nochain += 1
            else:
                miss += 1
    print(f"\n=== summary: {ok} OK, {placeholders} with placeholder nodes, {nochain} without chain, {miss} missing rows")
    print("\nPlaceholder command types in use (frames affected):")
    for t, fr in sorted(ALL_CMD_IN_USE.items()):
        print(f"  {t}: {', '.join(sorted(fr))}")

    if args.check:
        violations, redundant = [], []
        seen_pairs = set()
        for f in report:
            for s in f["slots"]:
                for ph in s.get("placeholders", []):
                    short = ph["type"].split("<", 1)[0]
                    allowed = ALLOWED_PLACEHOLDERS.get(short, set())
                    pair = (short, s["ability_id"])
                    seen_pairs.add(pair)
                    if s["ability_id"] not in allowed:
                        violations.append((f["frame"], s["slot"], s["ability_id"], short))
        for short, aids in ALLOWED_PLACEHOLDERS.items():
            missing = {aid for aid in aids if (short, aid) not in seen_pairs}
            for aid in sorted(missing):
                redundant.append((short, aid))

        if redundant:
            print("\n--check notice: allowlist entries whose placeholder went away (update the allowlist):")
            for short, aid in redundant:
                print(f"  {short} ability {aid}")
        if violations:
            print("\n--check FAILED: placeholders outside the documented allowlist:")
            for frame, slot, aid, short in violations:
                print(f"  frame '{frame}' slot {slot}: ability {aid} placeholder type {short}")
            return 1
        print(f"\n--check OK ({len(seen_pairs)} placeholder pairs, all documented)")
    return 0


if __name__ == "__main__":
    sys.exit(main())
