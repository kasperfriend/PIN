#!/usr/bin/env python3
"""Complete prod-1962 placement input audit, not recovered original spawn locations.

The CSV inventories every monster admitted/excluded by the current classifier and
flags missing assignment evidence. Counts follow code policy, not inferred lore.
"""
import argparse
import collections
import io
import json
import math
from pathlib import Path
import re
import tempfile
import zipfile

from npc_movement_audit import ROOT, parse_behavior, csv_text
from sdb_dump import (StaticDB, KNOWN_TABLES, harvest_pin_names,
                      harvest_record_table_names, apply_column_names)


def classification_sets(source, names=('_neverWorldSpawned', '_settlementBehaviors')):
    """Read the two explicitly named sets, so report checks track classifier edits."""
    result = []
    for name in names:
        match = re.search(r'\b' + name + r'\s*=\s*new\([^;]+?\{(.*?)\};', source, re.S)
        if not match:
            raise ValueError(f'Missing classifier set {name}')
        result.append({v.lower() for v in re.findall(r'"([^"\n]+)"', match.group(1))})
    return result


def classify(row, faction, excluded, settlement, routes, poses):
    name, params = parse_behavior(row['behavior'])
    if not (row['chassis_id'] or row['posetype_id']):
        return 'Excluded', 'no chassis and no posetype'
    if name.lower() in excluded:
        return 'Excluded', f'behaviour {name}'
    if name.lower() in routes or params.get('city_prefix', '').strip():
        return 'Excluded', 'requires route, named points or follow target'
    try:
        offset = float(params.get('groundoffset', '0'))
    except ValueError:
        offset = 0
    if (params.get('climber', '').lower() in ('true', '1') or
        params.get('grounded', '').lower() in ('false', '0') or
        params.get('inspawnvolume', '').lower() in ('true', '1') or
        (math.isfinite(offset) and offset > 0)):
        return 'Excluded', 'requires unsupported locomotion or spawn volume'
    if name.lower() == 'performemotenophysics' or params.get('emote', '').strip().lower() in poses:
        return 'Excluded', 'requires assigned prop or pose placement'
    if row['vendor_id']:
        return 'Excluded', 'requires vendor placement assignment'
    habitats = []
    if name.lower() in settlement:
        habitats.append('Settlement')
    if (faction or '').lower() == 'melding' or name.lower().startswith('melding'):
        habitats.append('Melding')
    if (faction or '').lower() == 'chosen':
        habitats.extend(('Melding', 'Wilderness'))
    return '|'.join(dict.fromkeys(habitats)) or 'Wilderness', ''


def risks(row, habitat):
    if habitat == 'Excluded':
        return []
    name, params = parse_behavior(row['behavior'])
    result = []
    if row['vendor_id']:
        result.append('vendor location/uniqueness not assigned')
    if name in {'StockShootAndFollowRoute', 'OneOff_FollowRoute', 'NavigateToLocation',
                'Arch_Follower', 'ProtectVehicle', 'TestFollowPlayer'} or 'city_prefix' in params:
        result.append('route/follow/named-point assignment missing')
    if (params.get('climber', '').lower() in ('1', 'true') or
        params.get('grounded', '').lower() in ('0', 'false') or
        params.get('inspawnvolume', '').lower() in ('1', 'true')):
        result.append('unsupported locomotion/spawn volume')
    if name == 'PerformEmoteNoPhysics' or any(word in params.get('emote', '').lower()
                                            for word in ('seat', 'chair', 'lean', 'typing')):
        result.append('pose may require an assigned prop')
    if not name and row['behavior_instance_id']:
        result.append('empty invocation has unresolved CAIS instance')
    if 'restfunction' in params:
        result.append('work destination requires placed station')
    return result


def reports(path):
    db = StaticDB(path)
    game = ROOT / 'UdpHosts/GameServer'
    loaded, columns = harvest_pin_names(str(game))
    db.resolve_names(KNOWN_TABLES + loaded + harvest_record_table_names(game))
    apply_column_names(db, columns)
    def rows(name):
        table = db.find_table(name)
        if table is None:
            raise ValueError(f'Missing reference table {name}')
        return list(db.rows(table))
    monsters = rows('dbcharacter::Monster')
    factions = {r['id']: r['internal_name'] for r in rows('dbcharacter::Faction')}
    zones = {r['id']: r for r in rows('dbzonemetadata::ZoneRecord')}
    by_id = {r['id']: r for r in monsters}
    source = (game / 'Systems/Spawning/Population/MonsterHabitatClassifier.cs').read_text(encoding='utf-8')
    excluded, settlement = classification_sets(source)
    routes, poses = classification_sets(source, ('_assignedRouteBehaviors', '_propDependentEmotes'))
    roster = []
    fixture = []
    for row in sorted(monsters, key=lambda r: r['id']):
        faction = factions.get(row['faction_id'], '')
        habitat, reason = classify(row, faction, excluded, settlement, routes, poses)
        fixture.append({'monster_id': row['id'], 'behavior': row['behavior'], 'faction': faction,
                        'vendor_id': row['vendor_id'], 'has_representation': bool(row['chassis_id'] or row['posetype_id']),
                        'habitat': habitat, 'exclusion': reason})
        roster.append({'monster_id': row['id'], 'faction_id': row['faction_id'], 'faction': faction,
                       'behavior': row['behavior'], 'habitat_policy': habitat, 'exclusion': reason,
                       'vendor_id': row['vendor_id'], 'assignment_risks': '; '.join(risks(row, habitat)),
                       'original_zone_assignment': 'not recovered'})
    counts = collections.Counter(r['habitat_policy'] for r in roster)
    flags = collections.Counter(flag for r in monsters for flag in risks(r, classify(r, factions.get(r['faction_id']), excluded, settlement, routes, poses)[0]))
    spawns = json.loads((game / 'StaticDB/CustomData/character_spawn.json').read_text(encoding='utf-8'))
    lines = ['# NPC/enemy placement audit — prod-1962', '',
             'Generated by `python3 Tools/SdbDump/npc_placement_audit.py`; verify with `--check`.', '',
             '## Verdict: original placement parity is NOT verified', '',
             'Every monster template and static JSON spawn was inspected. The runtime has a **global candidate roster**, no recovered per-zone/per-encounter monster assignment and no exact original NPC location table. Habitat/collision compatibility is not evidence that a particular boss, vendor or mission NPC belongs in that zone.', '',
             f'- **{len(monsters):,} templates** inventoried in `NpcPlacement/roster.csv`; **{len(zones)} database zones**.',
             f'- **{len(spawns)} static JSON character spawns**, all in development/test zones 12 and 1003; they are faction lineup fixtures, not recovered open-world placement.',
             '- Procedural, hard-coded/debug and ability/encounter spawns are separate from that JSON inventory. The CSV is not a live entity census.', '',
             '## Current procedural roster policy', '', '| Habitat policy | Templates |', '|---|---:|']
    for key, count in sorted(counts.items()):
        lines.append(f"| {key.replace('|', '&#124;')} | {count} |")
    lines += ['', '## Procedural exclusions by first reason', '', '| Reason | Templates |', '|---|---:|']
    for reason, count in sorted(collections.Counter(r['exclusion'] for r in roster if r['exclusion']).items()):
        lines.append(f'| {reason} | {count} |')
    lines += ['', 'These categories are reproduced from `MonsterHabitatClassifier`, **not original biome/zone assignments**. The coverage phase tries to give every admitted row a slot in available compatible cells; density can repeat those rows.', '',
              '## Assignment risk flags (overlapping)', '', '| Risk | Admitted templates |', '|---|---:|']
    for key, count in sorted(flags.items()):
        lines.append(f"| {key.replace('|', '&#124;')} | {count} |")
    lines += ['', 'Flags identify missing evidence, not automatic proof a template is misplaced. For example, a vendor needs a location/role assignment; a seated pose needs a matching prop. Neither should be assigned invented original coordinates.', '',
              '## All static JSON spawns', '', '| Spawn | Zone | Monster | Faction | Invocation | Input validation |', '|---|---:|---:|---|---|---|']
    ids = set()
    for spawn in spawns:
        errors = []
        if spawn['id'] in ids:
            errors.append('duplicate spawn id')
        ids.add(spawn['id'])
        row = by_id.get(spawn['type'])
        if row is None:
            errors.append('missing monster')
        if spawn['zone_id'] not in zones:
            errors.append('zone not in client ZoneRecord (custom/test zone requires external validation)')
        pos = spawn.get('position', {})
        if not all(isinstance(pos.get(k), (int, float)) and math.isfinite(pos[k]) for k in ('X', 'Y', 'Z')):
            errors.append('invalid position')
        q = spawn.get('orientation')
        if q is not None:
            norm = sum(q.get(k, 0) ** 2 for k in ('X', 'Y', 'Z', 'W'))
            if not math.isfinite(norm) or abs(norm - 1) > .01:
                errors.append('invalid orientation')
        text = row['behavior'].replace('|', '&#124;') if row else ''
        lines.append(f"| {spawn['id']} | {spawn['zone_id']} | {spawn['type']} | {factions.get(row['faction_id'], '') if row else ''} | `{text or '(empty)'}` | {'; '.join(errors) or 'IDs and numeric pose valid; terrain not checked'} |")
    lines += ['', '## Confirmed execution guards and correction', '',
              '- Planning filters cell centers using client-only and remove-in-production chunk metadata. Runtime ground probes, slope/standing-volume checks and occupancy protect physical placement; zone-player streaming avoids using another zone\'s player coordinates.',
              '- **Corrected final-position gap:** initial jitter and retry jitter could cross a cell, zone boundary or forbidden chunk after center validation. `WorldPopulationService` now checks finite coordinates, actual zone bounds, assigned cell and chunk eligibility both before and after terrain resolution. This preserves the cell\'s existing habitat/level ownership; it does not certify habitat at subcell scale.',
              '- Unknown chunk metadata retains the existing allow policy; no-collision development mode retains anchor fallback. These are explicit unverified-placement modes, not geographic proof.',
              '- Overhead-cover rejection is an existing compatibility rule which can suspend after widespread refusals. Caves, interiors, roofs, multi-level cells and unusual surfaces still need asset-backed/client validation.', '',
              '## Remaining placement gaps', '',
              '1. **Global rather than per-zone roster:** no zone/subzone/encounter restriction on a template, so region-specific creatures and bosses can be assigned to any zone with suitable generic ground.',
              '2. **Conservative assignment gate added:** explicit route/follow/named-point requests, unsupported locomotion parameters, reviewed prop-dependent base poses and nonzero vendor roles are excluded from automatic population. This is a PIN safety policy, not recovered original placement. Unknown special trees/poses, bosses and unresolved CAIS instances still need assignment data. Explicit static/debug/ability spawns bypass this gate.',
              '3. **Settlement is a proximity heuristic:** all outposts and clustered deployables paint settlement cells without original NPC ownership/role assignments. Cell-center classification does not guarantee exact camp borders.',
              '4. **Static/hard-coded spawns use a separate path:** they do not inherit every procedural placement check. The static faction lineup is intentional debug content, not a misplaced production population to silently relocate.',
              '5. **No live map validation in this sandbox:** configured client map assets and an interactive game client are unavailable. Bounds, floor support, prop alignment and spawn composition across actual zones cannot be certified from template rows.', '',
              'Required for full verification: original zone/encounter spawn assignments plus configured zone assets, then inspect a live entity/slot export with monster id, faction, spawn source, zone/subzone, position and placement diagnostics. Do not manufacture these assignments from mission waypoints or visual offsets.', '',
              'See `NPC_AI_AUDIT_2026-10-08.md` and `WORLD_POPULATION.md` for policy boundaries and test results.', '']
    return {'NPC_PLACEMENT_AUDIT.md': '\n'.join(lines),
            'NpcPlacement/roster.csv': csv_text(list(roster[0]), roster),
            'NpcPlacement/classification.json': json.dumps({'patch': db.patch, 'monsters': fixture}, ensure_ascii=False, separators=(',', ':')) + '\n'}


def main():
    parser = argparse.ArgumentParser(description=__doc__)
    parser.add_argument('--check', action='store_true')
    args = parser.parse_args()
    with tempfile.TemporaryDirectory(prefix='pin-placement-') as temporary:
        path = Path(temporary) / 'clientdb.sd2'
        archive = b''.join((ROOT / 'Tools' / f'clientdb.zip.{p:03d}').read_bytes() for p in (1, 2))
        with zipfile.ZipFile(io.BytesIO(archive)) as zipped:
            path.write_bytes(zipped.read('clientdb.sd2'))
        output = reports(path)
    for name, content in output.items():
        target = ROOT / 'Docs' / name
        if args.check:
            if not target.exists() or target.read_text(encoding='utf-8') != content:
                raise SystemExit(f'Stale placement audit: {target}')
        else:
            target.parent.mkdir(parents=True, exist_ok=True)
            target.write_text(content, encoding='utf-8')
    print(f"{'Verified' if args.check else 'Generated'} {len(output)} placement audit files")


if __name__ == '__main__':
    main()
