#!/usr/bin/env python3
"""Verify friendly NPC movement inputs and repository placements in bundled prod-1962.

Friendly means PIN's directional FactionHostility result toward player faction 1,
not an assertion of original-game allegiance. No patrol coordinates are inferred.
"""
import argparse
import collections
import io
import json
from pathlib import Path
import tempfile
import zipfile

from npc_movement_audit import ROOT, parse_behavior
from sdb_dump import (StaticDB, KNOWN_TABLES, harvest_pin_names,
                      harvest_record_table_names, apply_column_names)


def faction_stances(factions, relations):
    """Mirror FactionHostility.Load: wildcard expansion, overrides and directional defaults."""
    by_id = {row["id"]: row for row in factions}
    result = {}
    for relation in relations:
        a, b = relation["faction_a"], relation["faction_b"]
        primary = by_id if a == 0 else (a,)
        secondary = by_id if b == 0 else (b,)
        for source in primary:
            for target in secondary:
                if source not in by_id or target not in by_id:
                    continue
                stance = ("Friendly" if relation["hostility_stance"] >= 1 else
                          "Hostile" if by_id[source]["default_stance"] <= -1 else "Neutral")
                result[source, target] = stance
                if relation["hostility_bidirectional"] == 1 and not (a == 0 and b == 0):
                    result[target, source] = stance
    return result


def report(path):
    db = StaticDB(path)
    game = ROOT / "UdpHosts" / "GameServer"
    loaded, columns = harvest_pin_names(str(game))
    db.resolve_names(KNOWN_TABLES + loaded + harvest_record_table_names(game))
    apply_column_names(db, columns)

    def rows(name):
        table = db.find_table(name)
        if table is None:
            raise ValueError(f"Missing table: {name}")
        return list(db.rows(table))

    stances = faction_stances(rows("dbcharacter::Faction"), rows("dbcharacter::FactionRelations"))
    monsters = rows("dbcharacter::Monster")
    # CharacterEntity.LoadMonster replicates faction as a byte.
    friendly = [r for r in monsters if stances.get((r["faction_id"] & 255, 1)) == "Friendly"]
    placements = json.loads((game / "StaticDB/CustomData/character_spawn.json").read_text(encoding="utf-8"))
    placed = collections.Counter(p["type"] for p in placements)
    groups = collections.defaultdict(list)
    for row in friendly:
        groups[parse_behavior(row["behavior"])[0]].append(row)
    lines = ["# Friendly NPC movement verification", "",
             "Generated with `python3 Tools/SdbDump/friendly_npc_audit.py`; use `--check` to verify.", "",
             f"- Build: **{db.patch}**; **{len(friendly):,}** of **{len(monsters):,}** monster templates resolve friendly toward player faction 1 under **PIN's current faction policy**.",
             "- This is not original allegiance recovery: friendly/neutral depend on directional wildcard/default handling in `FactionHostility`. Factionless templates are excluded, not assumed hostile or friendly by their behavior name.",
             f"- **{sum(placed[r['id']] for r in friendly)} friendly placements** in `character_spawn.json`; template availability does not mean world placement.", "",
             "## Civilian/guard movement inputs", "",
             "| Base behavior | Friendly templates | Current placements | Runtime interpretation |", "|---|---:|---:|---|"]
    meanings = {
        "PeacetimeCityWanderer": "Bounded walking; two named-point requests (459/551) stay MissingRoute",
        "PeacetimeCityWandererCore": "Bounded walking when present",
        "PeacetimeCityWandererWithHealing": "Bounded walking; healing tree not recovered",
        "GuardCityWanderer": "Bounded walking; not an authored ordered patrol",
        "BasicCivilian": "Bounded walking; greetings not recovered",
        "BasicCivilian_Stationary": "Explicitly fixed",
        "StationaryCivilianDialog": "Explicitly fixed",
        "AlertAndInteractive": "No inferred patrol; base interactions/emotes are separate",
        "AlertAndLookAtPlayer": "No inferred patrol; look-at tree not recovered",
        "": "PIN bounded-roaming fallback, NOT authored route evidence",
    }
    for name, meaning in meanings.items():
        rr = groups[name]
        lines.append(f"| `{name or '(empty)'}` | {len(rr)} | {sum(placed[r['id']] for r in rr)} | {meaning} |")
    lines += ["", "## Static JSON friendly placements (not total runtime population)", "",
              "| Spawn id | Zone id | Monster id | Base invocation | Result |", "|---|---:|---:|---|---|"]
    friendly_ids = {r["id"]: r for r in friendly}
    for p in placements:
        r = friendly_ids.get(p["type"])
        if r:
            name, _ = parse_behavior(r["behavior"])
            interpretation = ("Fallback roam, requires loaded reachable ground" if not name else
                              "No ambient routine for this archetype; no route assignment")
            text = r['behavior'].replace('|', '&#124;')
            lines.append(f"| {p['id']} | {p['zone_id']} | {r['id']} | `{text or '(empty)'}` | {interpretation} |")
    lines += ["", "## Friendly explicit route/follow/unsupported locomotion requests", "",
              "Base invocations only; offensive/defensive references remain in the full movement census.", "",
              "| Monster id | Base invocation |", "|---|---|"]
    external = {"StockShootAndFollowRoute", "OneOff_FollowRoute", "NavigateToLocation",
                "Arch_Follower", "TestFollowPlayer", "ProtectVehicle"}
    for r in friendly:
        name, params = parse_behavior(r["behavior"])
        if (name in external or params.get("city_prefix") or
            params.get("climber", "").lower() in ("true", "1") or
            params.get("grounded", "").lower() in ("false", "0")):
            lines.append(f"| {r['id']} | `{r['behavior'].replace('|', '&#124;')}` |")
    lines += ["", "## Verified boundary", "",
              "- The four placements above cover **only `character_spawn.json`**. `SdbWorldPopulationDataSource` and `MonsterHabitatClassifier` also admit eligible represented settlement templates (vendors and explicit route/prop/unsupported-locomotion requirements are filtered from automatic population); `WorldPopulationPlanner` generates cells/slots around settlement anchors, and `EntityManagerWorldPopulationSpawner` registers them through the same spawn path. Population is enabled by default but constrained by terrain, streaming and caps. Hard-coded/debug/ability spawns are also outside this count. No total live-zone NPC count is asserted.",
              "- `EntityManager.SpawnCharacter` registers NPCs irrespective of faction. `AiEngine` runs ambient routines while Idle; friendly player proximity does not require a combat target to make them walk.",
              "- `NpcRoutineProfile` honors explicit stationary/zero-distance settings; named route, climbing, spawn-volume and non-ground requirements do not acquire invented ground patrols.",
              "- `NpcRoutine` requests destinations, pauses on arrival, chooses subsequent legs and can visit a matching placed work deployable. `PhysicsNpcNavigation.SupportsRoutines` requires loaded collision; rejected paths/steps do not move the body.",
              "- **No authored ordered NPC patrol assignments were identified** in the complete client schema or repository spawn records. Mission waypoints are not NPC routes. Map path layers have no recovered ground-NPC assignment; do not repurpose them as patrol loops.",
              "- **Zero placements** of the 109 nonempty-behavior work deployable definitions are in `deployable.json` (see the full movement census). Work visits require a live, matching, reachable station; templates alone do not place one.",
              "- Placed monster 2407 has an empty invocation **and nonzero behavior_instance_id=362**. PIN does not resolve that CAIS instance; its roaming is a fallback, not evidence that the original NPC had no behavior.",
              "- Factionless monster 2939 requests `PeacetimeCityWanderer(restFunction=\"Work\")`; it is not in the friendly count. Its interaction-like name does not establish friendly faction semantics.",
              "- Exact tree defaults, route assignments, greeting/healing/flee/escort logic, original locomotion speeds and live-zone reachability remain unverified. Bounded roaming is PIN compatibility policy, not proof of original patrol parity.", "",
              "See [NPC routines](NPC_ROUTINES.md), [complete movement census](NpcMovement/README.md), and [AI audit](NPC_AI_AUDIT_2026-10-08.md).", ""]
    return "\n".join(lines)


def main():
    parser = argparse.ArgumentParser(description=__doc__)
    parser.add_argument("--check", action="store_true")
    args = parser.parse_args()
    with tempfile.TemporaryDirectory(prefix="pin-friendly-") as temporary:
        archive = b"".join((ROOT / "Tools" / f"clientdb.zip.{p:03d}").read_bytes() for p in (1, 2))
        path = Path(temporary) / "clientdb.sd2"
        with zipfile.ZipFile(io.BytesIO(archive)) as zipped:
            path.write_bytes(zipped.read("clientdb.sd2"))
        content = report(path)
    target = ROOT / "Docs/FRIENDLY_NPC_MOVEMENT_VERIFICATION.md"
    if args.check:
        if not target.exists() or target.read_text(encoding="utf-8") != content:
            raise SystemExit(f"Stale friendly NPC report: {target}")
    else:
        target.write_text(content, encoding="utf-8")
    print(f"{'Verified' if args.check else 'Generated'} {target}")


if __name__ == "__main__":
    main()
