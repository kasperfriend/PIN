#!/usr/bin/env python3
"""Author the server-side aptitude command rows that make consumable items work.

The client database ships the *ability chains* of every item (which commands run, in which
order) but not the payload of the server-only commands: an ``UnlockWarpaints`` node says
"unlock a warpaint" without naming which one, ``SpawnLoot`` does not carry its loot table,
``GrantOwnerItem`` does not carry the item, and so on. Those payloads lived in the server
database that never shipped. This script reconstructs them from the data that *did* ship:
the item that owns the chain, its localized name, and the tables the unlock ids live in
(``dbitems::RootItem`` type 15 for warpaints, ``dbvisualrecords::TattooDecal`` for decals,
``dbvisualrecords::CziPattern`` for patterns, ``dbvisualrecords::OrnamentsMapGroups`` for
ornaments, ``dbcharacter::HeadAccessory`` for hair, ``dbcharacter::MonsterTitle`` for titles,
``dbitems::Certificate`` for certs, ``dbitems::LootTable`` for loot). Where a match is
found the row gets a payload; where it is not, the row keeps its ``comment`` (naming the
item) so the server can log exactly which chain node is still empty.

Usage::

    # dump the tables once (see sdb_dump.py)
    python3 sdb_dump.py dump clientdb.sd2 <table> -o /tmp/sdb/<table with :: -> __>.json
    # then
    python3 author_item_command_defs.py /tmp/sdb ../../UdpHosts/GameServer/StaticDB/CustomData

Existing hand-authored rows (``title_id`` on UnlockTitles, ``sdb_id`` on UnlockBattleframes,
``package_sdb_id``/``item_sdb_id`` on UnpackItem, ...) are always kept as they are.
"""

import collections
import json
import os
import re
import sys

DUMP_DIR = sys.argv[1] if len(sys.argv) > 1 else "/tmp/sdb"
OUT_DIR = sys.argv[2] if len(sys.argv) > 2 else os.path.join(
    os.path.dirname(__file__), "..", "..", "UdpHosts", "GameServer", "StaticDB", "CustomData")


def rows(table):
    with open(os.path.join(DUMP_DIR, table + ".json"), encoding="utf-8") as handle:
        return json.load(handle)["rows"]


# ----------------------------------------------------------------------------- client data
root = {r["sdb_id"]: r for r in rows("dbitems__RootItem")}
mods = {r["id"]: r for r in rows("dbitems__AbilityModule")}
abil = {r["id"]: r for r in rows("apt__AbilityData")}
ctype = {r["id"]: r["sdb_fullname"].split("::")[-1].replace("CommandDef", "") for r in rows("apt__CommandType")}
bcd = {r["id"]: r for r in rows("apt__BaseCommandDef")}
cond = {r["id"]: r for r in rows("apt__ConditionalBranchCommandDef")}
call = {r["id"]: r for r in rows("apt__CallCommandDef")}
lor = {r["id"]: r for r in rows("apt__LogicOrChainCommandDef")}
lac = {r["id"]: r for r in rows("apt__LogicAndChainCommandDef")}
iae = {r["id"]: r for r in rows("apt__ImpactApplyEffectCommandDef")}
sed = {r["id"]: r for r in rows("apt__StatusEffectData")}
loc = {r["id"]: r["english"] for r in rows("dblocalization__LocalizedText")}
loot_tables = {r["id"]: r for r in rows("dbitems__LootTable")}
rhi = {r["id"]: r for r in rows("aptfs__RequireHasItemCommandDef")}
rhc = {r["id"]: r for r in rows("aptfs__RequireHasCertificateCommandDef")}
rhu = {r["id"]: r for r in rows("aptfs__RequireHasUnlockCommandDef")}


def name(sdb_id):
    item = root.get(sdb_id)
    return loc.get(item["name_id"], "") if item else ""


def chain(head):
    out = []
    seen = set()
    while head and head in bcd and head not in seen:
        seen.add(head)
        row = bcd[head]
        out.append((ctype.get(row["subtype"], str(row["subtype"])), row["id"]))
        head = row["next"]
    return out


def walk(head, depth=0, seen=None, out=None):
    """Every (command type, command id) reachable from a chain head, in execution order."""
    out = [] if out is None else out
    seen = set() if seen is None else seen
    if head in seen or depth > 6:
        return out
    seen.add(head)
    for kind, cid in chain(head):
        out.append((kind, cid))
        if kind == "ConditionalBranch":
            for key in ("if_chain", "then_chain", "else_chain"):
                if cond[cid][key]:
                    walk(cond[cid][key], depth + 1, seen, out)
        elif kind == "LogicOrChain" and cid in lor:
            walk(lor[cid]["or_chain"], depth + 1, seen, out)
        elif kind == "LogicAndChain" and cid in lac:
            walk(lac[cid]["and_chain"], depth + 1, seen, out)
        elif kind == "Call" and cid in call and call[cid]["ability_id"] in abil:
            walk(abil[call[cid]["ability_id"]]["chain"], depth + 1, seen, out)
        elif kind == "ImpactApplyEffect" and cid in iae:
            effect = sed.get(iae[cid]["effect_id"])
            if effect and effect["apply_chain"]:
                walk(effect["apply_chain"], depth + 1, seen, out)
    return out


# command id -> [(item sdb id, ordinal of this command type within the item's chain)]
owners = collections.defaultdict(list)
for sdb_id, module in mods.items():
    ability = abil.get(module["ability_chain_id"])
    if sdb_id not in root or not ability:
        continue
    counters = collections.Counter()
    for kind, cid in walk(ability["chain"]):
        owners[cid].append((sdb_id, counters[kind], ability["id"]))
        counters[kind] += 1


def norm(text):
    return re.sub(r"[^a-z0-9]+", " ", (text or "").lower()).strip()


PREFIXES = (
    r"Warpaint Unlock", r"Battleframe Visual Unlock", r"Holmgang Warpaint", r"Holmgang Ornament",
    r"Holmgang Decal", r"New You Unlock", r"Battleframe Unlock", r"Decal Unlock", r"Ornament Unlock",
    r"Pattern Unlock", r"Title Unlock", r"Unlock Title", r"Title", r"Certificate Unlock", r"Cert Unlock",
    r"Head Accessory Unlock", r"Emote Unlock", r"Frame Unlock", r"CZI Pattern Unlock",
    r"Battleframe Decal Unlock", r"Battleframe Pattern Unlock", r"Holmgang Pilot License", r"Red 5 Gift",
    r"Recipe Unlock", r"Blueprint Unlock", r"Unlock",
)
SUFFIXES = (
    r"Warpaint", r"Decal", r"Battleframe Pattern", r"Bodysuit Pattern", r"Armor Pattern", r"Pattern",
    r"Ornament", r"Visual Kit", r"Kit", r"Title", r"Certificate", r"Unlock", r"Hairstyle", r"Facial Hair",
    r"Battleframe", r"Eyepiece",
)


def strip(text):
    text = re.sub(r"^\[.*?\]\s*", "", text or "")
    text = re.sub(r"^(%s)\s*[:\-]\s*" % "|".join(PREFIXES), "", text)
    text = re.sub(r"\s*[-:]?\s*(%s)$" % "|".join(SUFFIXES), "", text)
    return text.strip()


def index_by_name(pairs):
    index = collections.defaultdict(list)
    for key, value in pairs:
        if key:
            index[norm(key)].append(value)
    return index


warpaints = index_by_name((name(s), s) for s, r in root.items() if r["type"] == 15)
decals = index_by_name((loc.get(r["localized_name_id"]), r["id"]) for r in rows("dbvisualrecords__TattooDecal"))
patterns = index_by_name((loc.get(r["name_id"]), r["id"]) for r in rows("dbvisualrecords__CziPattern"))
ornaments = index_by_name((loc.get(r["col_036cbcd1"]), r["id"]) for r in rows("dbvisualrecords__OrnamentsMapGroups"))
head_accessories = index_by_name((loc.get(r["loc_name_id"]), r["ha_id"]) for r in rows("dbcharacter__HeadAccessory"))
titles = index_by_name((loc.get(r["localized_name_id"]), r["id"]) for r in rows("dbcharacter__MonsterTitle"))
certs = index_by_name((loc.get(r["localized_name_id"]), r["id"]) for r in rows("dbitems__Certificate"))
loot_by_name = index_by_name((r["name"], r["id"]) for r in loot_tables.values())
items_by_name = collections.defaultdict(list)
for s, r in root.items():
    items_by_name[norm(name(s))].append(s)

FRAME_ITEMS = {
    "assault": 76164, "dreadnaught": 75772, "dreadnought": 75772, "biotech": 75774, "engineer": 75775,
    "recon": 75773, "firecat": 76133, "tigerclaw": 76132, "electron": 76337, "bastion": 76338,
    "mammoth": 76331, "rhino": 76332, "dragonfly": 76335, "recluse": 76336, "nighthawk": 76333,
    "raptor": 76334, "graviton": 82359, "arsenal": 82360, "archangel": 82394,
}

# Some names need a nudge before they line up with the table they unlock.
ALIASES = {
    "purple haze": ["purpledaze"],
    "cupid": ["valentine s cupid"],
    "carnaval": ["carnival"],
    "the brood visual": ["brood", "brood ii"],
    "black cats visual": ["black cats"],
    "valentine s": ["heart"],
    "ares eyepiece": ["ares eyepiece", "ares"],
    "2014 goggles": ["ace goggles"],
    "reindeer antlers": ["reinder antlers"],
    "omnitech crown": ["omnidyne tech crown"],
    "phantom": ["phantom mask"],
    "the skull": ["skull"],
    "cat ears": ["feline ears"],
    "tricorn": ["tricorne hat"],
    "viking hat": ["viking helmet"],
    "rabbit ears": ["rabbit mask"],
    "aviator sunglasses": ["aviators", "aviator goggles"],
    "dragon helm": ["dragon mask"],
    "astrek skull helm": ["astrek skull mask"],
    "fiesty": ["feisty"],
    "sideswipe": ["side swipe"],
    "5 o clock": ["5 o clock"],
}


def lookup(index, raw):
    key = norm(strip(raw))
    if key in index:
        return index[key]
    for alias in ALIASES.get(key, []):
        if alias in index:
            return index[alias]
    return []


def load_existing(filename):
    for folder in ("", "Todo"):
        path = os.path.join(OUT_DIR, folder, filename)
        if os.path.exists(path):
            with open(path, encoding="utf-8-sig") as handle:
                return path, json.load(handle)
    raise FileNotFoundError(filename)


def has_payload(row):
    return any(k not in ("id", "comment") and v not in (None, 0, "", []) for k, v in row.items())


def item_comment(cid):
    seen = []
    for sdb_id, ordinal, ability in owners.get(cid, []):
        label = "%d. %s" % (ability, name(sdb_id) or "item %d" % sdb_id)
        if label not in seen:
            seen.append(label)
    return " / ".join(seen[:3])


def author(filename, out_name, fill):
    """Rewrite ``filename`` keeping hand-authored payloads and filling the rest via ``fill``."""
    path, existing = load_existing(filename)
    result = []
    filled = 0
    for row in existing:
        row = dict(row)
        if not has_payload(row):
            payload = fill(row["id"])
            if payload:
                row.update(payload)
                filled += 1
        if not row.get("comment"):
            row["comment"] = item_comment(row["id"])
        result.append(row)
    target = os.path.join(OUT_DIR, out_name)
    with open(target, "w", encoding="utf-8") as handle:
        json.dump(result, handle, indent=2, ensure_ascii=False)
        handle.write("\n")
    if os.path.abspath(target) != os.path.abspath(path):
        os.remove(path)
    total = sum(1 for r in result if has_payload(r))
    print("%-45s %4d rows, %4d with payload (+%d)" % (out_name, len(result), total, filled))


def first_owner(cid):
    return owners.get(cid, [(None, 0, 0)])[0]


def sibling_ids(sdb_id, kind, table):
    """Ids named by the ``kind`` requirement nodes of the item's own chain (e.g. the
    RequireHasCertificate(negate) guard in front of a Recipe Unlock names the very cert to grant)."""
    ability = abil.get(mods[sdb_id]["ability_chain_id"]) if sdb_id in mods else None
    if not ability:
        return []
    return [table[cid] for k, cid in walk(ability["chain"]) if k == kind and cid in table]


def unlock_sibling(sdb_id, unlock_type):
    return [r["unlock_id"] for r in sibling_ids(sdb_id, "RequireHasUnlock", rhu) if r["unlock_type"] == unlock_type]


def simple_unlock(index, key, allow_many=False, unlock_type=None):
    def fill(cid):
        sdb_id, _, _ = first_owner(cid)
        if sdb_id is None:
            return None
        found = unlock_sibling(sdb_id, unlock_type) if unlock_type else []
        found = found or lookup(index, name(sdb_id))
        if not found:
            return None
        if allow_many and len(found) > 1:
            return {key: sorted(set(found))}
        return {key: [found[0]] if allow_many else found[0]}
    return fill


# ------------------------------------------------------------------- per-command authoring
author("aptgss_UnlockWarpaintsCommandDef.json", "aptgss_UnlockWarpaintsCommandDef.json",
       simple_unlock(warpaints, "warpaint_id", unlock_type="warpaints"))
author("aptgss_UnlockDecalsCommandDef.json", "aptgss_UnlockDecalsCommandDef.json",
       simple_unlock(decals, "decal_id", unlock_type="decals"))
author("aptgss_UnlockPatternsCommandDef.json", "aptgss_UnlockPatternsCommandDef.json",
       simple_unlock(patterns, "pattern_ids", allow_many=True, unlock_type="czi_patterns"))
author("aptgss_UnlockOrnamentsCommandDef.json", "aptgss_UnlockOrnamentsCommandDef.json",
       simple_unlock(ornaments, "ornament_id", unlock_type="ornaments"))
author("aptgss_UnlockHeadAccessoriesCommandDef.json", "aptgss_UnlockHeadAccessoriesCommandDef.json",
       simple_unlock(head_accessories, "head_accessory_ids", allow_many=True))
author("aptgss_UnlockTitlesCommandDef.json", "aptgss_UnlockTitlesCommandDef.json",
       simple_unlock(titles, "title_id", unlock_type="titles"))


def fill_cert(cid):
    sdb_id, _, _ = first_owner(cid)
    if sdb_id is None:
        return None
    guards = [r["certificate_id"] for r in sibling_ids(sdb_id, "RequireHasCertificate", rhc) if r["negate"] == 1]
    if guards:
        return {"certificate_id": guards[0]}
    raw = name(sdb_id)
    for candidate in (raw, strip(raw), strip(raw) + " Recipe Unlocked", strip(raw) + " Unlocked",
                      re.sub(r"^Research:\s*", "", raw)):
        found = certs.get(norm(candidate))
        if found:
            return {"certificate_id": found[0]}
    return None


author("aptgss_UnlockCertsCommandDef.json", "aptgss_UnlockCertsCommandDef.json", fill_cert)


def fill_battleframe(cid):
    sdb_id, _, _ = first_owner(cid)
    if sdb_id is None:
        return None
    key = norm(strip(name(sdb_id)))
    if key in FRAME_ITEMS:
        return {"sdb_id": FRAME_ITEMS[key]}
    return None


author("aptgss_UnlockBattleframesCommandDef.json", "aptgss_UnlockBattleframesCommandDef.json", fill_battleframe)

# Level-N upgrade kits: "Upgrade Gear Inside". No table of that name shipped, so a kit rolls one
# piece from each of the "Level N Uncommon <slot>" tables the client database does carry (the
# level-scaled armor/reactor sets those kits were sold to fill). Kits with two SpawnLoot nodes
# get the armor set on the first and the reactor/misc set on the second.
LEVEL_TABLES = {}
for table in loot_tables.values():
    match = re.match(r"Level (\d+) Uncommon (Leg Armor|Arm Armor|Head Armor|Torso Armor|Reactor|Misc Gear)$", table["name"])
    if match:
        LEVEL_TABLES.setdefault(int(match.group(1)), {})[match.group(2)] = table["id"]


def fill_spawn_loot(cid):
    sdb_id, ordinal, _ = first_owner(cid)
    if sdb_id is None:
        return None
    raw = name(sdb_id)
    kit = re.search(r"(?:Level|Tier) (\d+) Upgrade Kit", raw)
    if kit:
        level = int(kit.group(1))
        tables = LEVEL_TABLES.get(level)
        if not tables:
            return None
        armor = [tables[k] for k in ("Head Armor", "Torso Armor", "Arm Armor", "Leg Armor") if k in tables]
        rest = [tables[k] for k in ("Reactor", "Misc Gear") if k in tables]
        chosen = (armor if ordinal == 0 else rest) if ordinal_count(sdb_id, "SpawnLoot") > 1 else armor + rest
        return {"loot_table_ids": chosen, "roll_each": 1}
    stripped = re.sub(r"^PTS:\s*", "", raw).strip()
    exact = loot_by_name.get(norm(stripped))
    if exact:
        return {"loot_table_ids": [exact[0]]}
    # "Beta Crystite Crate (x11 packs)" -> "Beta Crystite Crate (11 booster packs)", multi-part PTS grants
    parts = sorted(t["id"] for t in loot_tables.values() if norm(t["name"]).startswith(norm(stripped)))
    if parts:
        booster = [t for t in parts if "booster pack" in loot_tables[t]["name"].lower()]
        if booster:
            return {"loot_table_ids": [booster[0]]}
        return {"loot_table_ids": [parts[ordinal % len(parts)]]}
    return None


def ordinal_count(sdb_id, kind):
    ability = abil.get(mods[sdb_id]["ability_chain_id"])
    return sum(1 for k, _ in walk(ability["chain"]) if k == kind) if ability else 0


author("aptgss_SpawnLootCommandDef.json", "aptgss_SpawnLootCommandDef.json", fill_spawn_loot)

LOCKER_TABLES = {
    # (locker name fragment, key item) -> loot table. 122939 is the rare drop (loot table 7057
    # rolls it at 1/…), so it opens the gold table; the common 122804 and the gifted 123027 open silver.
    ("weapon", 122804): 7561, ("weapon", 123027): 7561, ("weapon", 122939): 6826,
    ("armor", 122804): 7562, ("armor", 123027): 7562, ("armor", 122939): 7371,
    ("ability", 122804): 7563, ("ability", 123027): 7563, ("ability", 122939): 6829,
    ("module", 122804): 7564, ("module", 123027): 7564, ("module", 122939): 7056,
}


def required_items(sdb_id):
    ability = abil.get(mods[sdb_id]["ability_chain_id"])
    return [rhi[cid] for k, cid in walk(ability["chain"]) if k == "RequireHasItem" and cid in rhi] if ability else []


def fill_grant(cid):
    sdb_id, ordinal, _ = first_owner(cid)
    if sdb_id is None:
        return None
    raw = name(sdb_id)
    item = root[sdb_id]
    reqs = required_items(sdb_id)

    # Combine 50 fragments into a component: the fragment stack pays, the component is granted.
    frag = re.search(r"craft (?:a|an) (.+?)\.", loc.get(item["description_id"], "") or "")
    if raw.endswith("Fragment") and reqs and reqs[0]["item_id"] == sdb_id:
        target = None
        if frag:
            target = items_by_name.get(norm(frag.group(1)))
        if raw == "Key Fragment":
            target = [123027]
        if raw == "Vanity Key Fragment":
            target = [142157]
        if target:
            return {"item_sdb_id": target[0], "quantity": 1, "cost_sdb_id": sdb_id, "cost_quantity": reqs[0]["quantity"]}
        return None

    # Secure lockers: each branch requires one key variant, spends it and rolls the matching table.
    locker = re.search(r"Secure (Weapon|Armor|Ability|Module|Vanity) Locker", raw)
    if locker and reqs:
        branch_keys = [r["item_id"] for r in reqs]
        key = branch_keys[ordinal] if ordinal < len(branch_keys) else branch_keys[-1]
        table = LOCKER_TABLES.get((locker.group(1).lower(), key))
        payload = {"cost_sdb_id": key, "cost_quantity": 1}
        if table:
            payload["loot_table_id"] = table
        return payload

    turbo = re.match(r"Turbo Upgrade Kit: (.+) LGV$", raw)
    if turbo:
        upgraded = items_by_name.get(norm(turbo.group(1) + " Turbo LGV"))
        base = reqs[0]["item_id"] if reqs else None
        if upgraded:
            payload = {"item_sdb_id": upgraded[0], "quantity": 1}
            if base:
                payload.update({"cost_sdb_id": base, "cost_quantity": 1})
            return payload

    rental = re.match(r"(?:Rental Contract: |Activate Rental )(.+?)(?: LGV)? \d+ Days?$", raw)
    if rental:
        for candidate in ("Rental " + rental.group(1) + " LGV", "Rental " + rental.group(1)):
            found = items_by_name.get(norm(candidate))
            if found:
                return {"item_sdb_id": found[0], "quantity": 1}
        if reqs and reqs[0]["negate"] == 1:
            return {"item_sdb_id": reqs[0]["item_id"], "quantity": 1}

    if re.search(r"LGV Rental$|Rental .* LGV", raw) and item["type"] == 9:
        vehicle = re.sub(r"^\d+[- ]Day |\d+ Day LGV Rental$|^\d+-Day ", "", raw).replace(" Rental", "").strip()
        for candidate in (vehicle + " LGV", vehicle):
            found = [s for s in items_by_name.get(norm(candidate), []) if root[s]["type"] == 7]
            if found:
                return {"item_sdb_id": found[0], "quantity": 1}

    bundle = re.match(r"Chosen Polymorph Bundle \[(\d+) ?min\]", raw)
    if bundle:
        minutes = bundle.group(1)
        morphs = [s for s in root if re.match(r"Chosen (Fiend|ShockTrooper|Devastator) \[%s ?min\]\s*$" % minutes, name(s))]
        morphs.sort(key=lambda s: ["Fiend", "ShockTrooper", "Devastator"].index(re.search(r"(Fiend|ShockTrooper|Devastator)", name(s)).group(1)))
        if ordinal < len(morphs):
            return {"item_sdb_id": morphs[ordinal], "quantity": 1}

    count = re.search(r" x(\d+)$", raw)
    if count:
        found = [s for s in items_by_name.get(norm(raw[: count.start()]), []) if s != sdb_id]
        if found:
            return {"item_sdb_id": found[0], "quantity": int(count.group(1))}

    token = re.match(r"(\d+) (.+)$", raw)
    if token:
        found = [s for s in items_by_name.get(norm(re.sub(r"^Accord ", "", token.group(2))), []) if root[s]["type"] == 0]
        if found:
            return {"item_sdb_id": found[0], "quantity": int(token.group(1))}

    # A delivery item (type 9) that carries the name of a real item hands that item over.
    if item["type"] == 9:
        found = [s for s in items_by_name.get(norm(raw), []) if s != sdb_id and root[s]["type"] != 9]
        if found:
            found.sort(key=lambda s: (root[s]["type"] != 0, root[s]["type"] != 7, s))
            return {"item_sdb_id": found[0], "quantity": 1}
    return None


author("aptgss_GrantOwnerItemCommandDef.json", "aptgss_GrantOwnerItemCommandDef.json", fill_grant)

UNIT_SECONDS = {"min": 60, "mins": 60, "minute": 60, "minutes": 60, "hour": 3600, "hours": 3600,
                "day": 86400, "days": 86400, "month": 30 * 86400, "months": 30 * 86400}
BOOST_TYPES = {"xp": "xp", "experience": "xp", "reputation": "reputation", "rep": "reputation",
               "crystite": "crystite", "cy": "crystite"}


def fill_boost(cid):
    sdb_id, ordinal, _ = first_owner(cid)
    if sdb_id is None:
        return None
    raw = name(sdb_id)
    lower = raw.lower()
    duration = 0
    permanent = 0
    span = re.search(r"(\d+) ?(min|mins|minute|minutes|hour|hours|day|days|month|months)\b", lower)
    if span:
        duration = int(span.group(1)) * UNIT_SECONDS[span.group(2)]
    if "permanent" in lower:
        permanent = 1
    percent = 0
    pct = re.search(r"(\d+)%", lower)
    mult = re.search(r"(\d+)x\b", lower)
    if pct:
        percent = int(pct.group(1))
    elif mult:
        percent = (int(mult.group(1)) - 1) * 100
    kinds = []
    for word in re.findall(r"[a-z]+", lower):
        if word in BOOST_TYPES and BOOST_TYPES[word] not in kinds:
            kinds.append(BOOST_TYPES[word])
    if not kinds or not percent or not (duration or permanent):
        return None
    kind = kinds[ordinal] if ordinal < len(kinds) else kinds[-1]
    payload = {"boost_type": kind, "percent": percent}
    if permanent:
        payload["permanent"] = 1
    else:
        payload["duration_seconds"] = duration
    return payload


author("aptgss_ApplyPermanentEffectCommandDef.json", "aptgss_ApplyPermanentEffectCommandDef.json", fill_boost)


def fill_account_group(cid):
    sdb_id, _, _ = first_owner(cid)
    if sdb_id is None:
        return None
    raw = name(sdb_id)
    if not raw:
        return None
    lower = raw.lower()
    duration = 0
    span = re.search(r"(\d+)[- ]?(min|mins|hour|hours|day|days|month|months)\b", lower)
    if span:
        duration = int(span.group(1)) * UNIT_SECONDS[span.group(2)]
    group = re.sub(r"\b\d+[- ]?(min|mins|hour|hours|day|days|month|months)\b", "", lower)
    group = re.sub(r"\b(rental contract|activate rental|rental|contract|claim|unlock|unlocks|free)\b", "", group)
    group = re.sub(r"[^a-z0-9]+", "_", group).strip("_")
    if not group:
        return None
    payload = {"group": group}
    if duration:
        payload["duration_seconds"] = duration
    return payload


author("aptgss_AddAccountGroupCommandDef.json", "aptgss_AddAccountGroupCommandDef.json", fill_account_group)


def fill_reward_screen(cid):
    sdb_id, _, _ = first_owner(cid)
    if sdb_id is None:
        return None
    lower = name(sdb_id).lower()
    screen = 1 if re.search(r"locker|crate|cache|pack|box|kit", lower) else 0
    return {"screen_type": screen}


author("aptgss_ShowRewardScreenCommandDef.json", "aptgss_ShowRewardScreenCommandDef.json", fill_reward_screen)
