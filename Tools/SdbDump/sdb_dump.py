#!/usr/bin/env python3
"""Firefall StaticDB (clientdb.sd2) decoder and dumper.

Standalone, dependency-free port of the format logic from the original
toolchain (themeldingwars/FauFau `FauFau/Formats/StaticDB.cs`), which is also
the library PIN itself uses at runtime (NuGet package `FauFau`, see
`GameServer.csproj` / `StaticDBLoader`).

Container layout (128 byte header + payload):
    u32  magic       0xDA7ABA5E
    u32  version     file version (11 beta, 12 prod)
    u32  payloadSize size of the payload after the header
    u32  flags       bit0 ObfuscatedPool, bit1 BigEndian, bit2 Compressed,
                     bit3 Client, bit4 Server
    u64  timestamp   unix microseconds
    char[104] patch  e.g. "beta-1869" / "prod-1962", NUL padded

Payload:
    if ObfuscatedPool (flags&1): XOR with Red5 MersenneTwister stream seeded
    with FFnv32(patch string).
    if Compressed (flags&4): u32 inflated size, u32 padding, u16 0x7801,
    then a raw DEFLATE stream (wbits=-15).

Inflated memory image:
    u32  memoryVersion (1000 old, 1002 current)
    u16  tableCount
    tableCount * TableInfo   { u32 id, u16 numBytes, u16 numFields,
                               u16 numUsedBytes, u8 nullableBitfields }
    per table numFields * FieldInfo { u32 id, u16 start, u8 nullableIndex,
                                      u8 type }
    tableCount * RowInfo     { u32 rowOffset, u32 rowCount }
    u32  poolOffset
    rows live at absolute `rowOffset` inside the inflated blob
    pool (shared string/blob store) starts at `poolOffset`

Table ids and column ids are FFnv32 hashes of their names. PIN looks tables
up as "schema::Name" (e.g. "dbcharacter::Monster") and columns as
snake_case property names (e.g. "localized_name_id"); a resolved name list
can be supplied with --names to label the dump.

Usage:
    python3 sdb_dump.py info     clientdb.sd2 [--names names.txt]
    python3 sdb_dump.py dump     clientdb.sd2 dbcharacter::Monster [-o out.json]
    python3 sdb_dump.py tables   clientdb.sd2
    python3 sdb_dump.py monsters clientdb.sd2 [-o monsters.json]
"""

import argparse
import json
import os
import struct
import sys
import zlib

MAGIC = 0xDA7ABA5E
HEADER_SIZE = 128

FLAG_OBFUSCATED_POOL = 1 << 0
FLAG_BIG_ENDIAN = 1 << 1
FLAG_COMPRESSED = 1 << 2

# DBType ids (FauFau StaticDB.DBType)
DBT_UNKNOWN = 0
DBT_BYTE = 1
DBT_USHORT = 2
DBT_UINT = 3
DBT_ULONG = 4
DBT_SBYTE = 5
DBT_SHORT = 6
DBT_INT = 7
DBT_LONG = 8
DBT_FLOAT = 9
DBT_DOUBLE = 10
DBT_STRING = 11
DBT_VECTOR2 = 12
DBT_VECTOR3 = 13
DBT_VECTOR4 = 14
DBT_MATRIX4X4 = 15
DBT_BLOB = 16
DBT_BOX3 = 17
DBT_VECTOR2_ARRAY = 18
DBT_VECTOR3_ARRAY = 19
DBT_VECTOR4_ARRAY = 20
DBT_ASCII_CHAR = 21
DBT_BYTE_ARRAY = 22
DBT_USHORT_ARRAY = 23
DBT_UINT_ARRAY = 24
DBT_HALF_MATRIX4X3 = 25
DBT_HALF = 26

TYPE_NAMES = {
    DBT_UNKNOWN: "Unknown", DBT_BYTE: "Byte", DBT_USHORT: "UShort",
    DBT_UINT: "UInt", DBT_ULONG: "ULong", DBT_SBYTE: "SByte",
    DBT_SHORT: "Short", DBT_INT: "Int", DBT_LONG: "Long",
    DBT_FLOAT: "Float", DBT_DOUBLE: "Double", DBT_STRING: "String",
    DBT_VECTOR2: "Vector2", DBT_VECTOR3: "Vector3", DBT_VECTOR4: "Vector4",
    DBT_MATRIX4X4: "Matrix4x4", DBT_BLOB: "Blob", DBT_BOX3: "Box3",
    DBT_VECTOR2_ARRAY: "Vector2Array", DBT_VECTOR3_ARRAY: "Vector3Array",
    DBT_VECTOR4_ARRAY: "Vector4Array", DBT_ASCII_CHAR: "AsciiChar",
    DBT_BYTE_ARRAY: "ByteArray", DBT_USHORT_ARRAY: "UShortArray",
    DBT_UINT_ARRAY: "UIntArray", DBT_HALF_MATRIX4X3: "HalfMatrix4x3",
    DBT_HALF: "Half",
}

# Fixed cell width per type, per memory layout version.
TYPE_SIZES_1000 = {
    DBT_BYTE: 1, DBT_USHORT: 2, DBT_UINT: 4, DBT_ULONG: 8, DBT_SBYTE: 1,
    DBT_SHORT: 2, DBT_INT: 4, DBT_LONG: 8, DBT_FLOAT: 4, DBT_DOUBLE: 8,
    DBT_STRING: 8, DBT_VECTOR2: 8, DBT_VECTOR3: 12, DBT_VECTOR4: 16,
    DBT_MATRIX4X4: 64, DBT_BLOB: 8, DBT_BOX3: 24, DBT_VECTOR2_ARRAY: 8,
    DBT_VECTOR3_ARRAY: 8, DBT_VECTOR4_ARRAY: 8, DBT_ASCII_CHAR: 1,
    DBT_BYTE_ARRAY: 8, DBT_USHORT_ARRAY: 8, DBT_UINT_ARRAY: 8,
    DBT_HALF_MATRIX4X3: 24, DBT_HALF: 2,
}
TYPE_SIZES_1002 = {
    DBT_BYTE: 1, DBT_USHORT: 2, DBT_UINT: 4, DBT_ULONG: 8, DBT_SBYTE: 1,
    DBT_SHORT: 2, DBT_INT: 4, DBT_LONG: 8, DBT_FLOAT: 4, DBT_DOUBLE: 8,
    DBT_STRING: 4, DBT_VECTOR2: 8, DBT_VECTOR3: 12, DBT_VECTOR4: 16,
    DBT_MATRIX4X4: 64, DBT_BLOB: 4, DBT_BOX3: 24, DBT_VECTOR2_ARRAY: 4,
    DBT_VECTOR3_ARRAY: 4, DBT_VECTOR4_ARRAY: 4, DBT_ASCII_CHAR: 1,
    DBT_BYTE_ARRAY: 4, DBT_USHORT_ARRAY: 4, DBT_UINT_ARRAY: 4,
    DBT_HALF_MATRIX4X3: 24, DBT_HALF: 2,
}

# Types whose cell value is a key into the shared data pool.
POOL_TYPES = {
    DBT_STRING, DBT_BLOB, DBT_VECTOR2_ARRAY, DBT_VECTOR3_ARRAY,
    DBT_VECTOR4_ARRAY, DBT_BYTE_ARRAY, DBT_USHORT_ARRAY, DBT_UINT_ARRAY,
}


def ffnv32(text):
    """Red5 "Fast FNV-32" with post-mix (FauFau Checksum.FFnv32)."""
    if isinstance(text, str):
        text = text.encode("ascii")
    h = 0x811C9DC5
    for b in text:
        h = (0x01000193 * (h ^ b)) & 0xFFFFFFFF
    a = (8193 * h) & 0xFFFFFFFF
    h = (9 * (a ^ (a >> 7))) & 0xFFFFFFFF
    return (33 * (h ^ (h >> 17))) & 0xFFFFFFFF


class MersenneTwister:
    """Red5's custom MT19937 (non-standard tempering masks, 32-bit draws)."""

    N = 624
    M = 397
    UMASK = 0x80000000
    LMASK = 0x7FFFFFFF

    def __init__(self, seed=5489):
        self.mt = [0] * self.N
        self.mti = self.N + 1
        self._init_seed(seed)

    def _init_seed(self, seed):
        self.mt[0] = seed & 0xFFFFFFFF
        for i in range(1, self.N):
            self.mt[i] = (0x6C078965 * (self.mt[i - 1] ^ (self.mt[i - 1] >> 30)) + i) & 0xFFFFFFFF
        self.mti = self.N

    def next_u32(self):
        if self.mti >= self.N:
            if self.mti == self.N + 1:
                self._init_seed(5489)
            kk = 0
            while kk < self.N - self.M:
                y = (self.mt[kk] & self.UMASK) | (self.mt[kk + 1] & self.LMASK)
                self.mt[kk] = (self.mt[kk + self.M] ^ (y >> 1) ^ (0x9908B0DF if (y & 1) else 0)) & 0xFFFFFFFF
                kk += 1
            while kk < self.N - 1:
                y = (self.mt[kk] & self.UMASK) | (self.mt[kk + 1] & self.LMASK)
                self.mt[kk] = (self.mt[kk - 227] ^ (y >> 1) ^ (0x9908B0DF if (y & 1) else 0)) & 0xFFFFFFFF
                kk += 1
            y = (self.mt[self.N - 1] & self.UMASK) | (self.mt[0] & self.LMASK)
            self.mt[self.N - 1] = (self.mt[self.M - 1] ^ (y >> 1) ^ (0x9908B0DF if (y & 1) else 0)) & 0xFFFFFFFF
            self.mti = 0

        y = self.mt[self.mti]
        self.mti += 1
        # Red5 tempering (non-standard masks)
        y ^= y >> 11
        y = (y ^ ((y & 0xFF3A58AD) << 7)) & 0xFFFFFFFF
        y = (y ^ ((y & 0xFFFFDF8C) << 15)) & 0xFFFFFFFF
        y ^= y >> 18
        return y & 0xFFFFFFFF


def mt_xor(seed, data):
    """In-place XOR of data with the MT stream (4 LE bytes per draw)."""
    mt = MersenneTwister(seed)
    n = len(data)
    words = n >> 2
    out = bytearray(n)
    for i in range(words):
        struct.pack_into("<I", out, i * 4, mt.next_u32())
    z = words * 4
    for i in range(n - z):
        out[z + i] = mt.next_u32() & 0xFF
    for i in range(n):
        data[i] ^= out[i]


def half_to_float(h):
    """IEEE-754 binary16 to Python float."""
    exp = (h >> 10) & 0x1F
    frac = h & 0x3FF
    sign = -1.0 if (h >> 15) else 1.0
    if exp == 0:
        return sign * (frac / 1024.0) * (2.0 ** -14)
    if exp == 31:
        return sign * (float("inf") if frac == 0 else float("nan"))
    return sign * (1.0 + frac / 1024.0) * (2.0 ** (exp - 15))


class StaticDB:
    def __init__(self, path):
        with open(path, "rb") as fh:
            blob = fh.read()
        if len(blob) < HEADER_SIZE:
            raise ValueError("file smaller than the 128 byte header")

        (magic, version, payload_size, flags) = struct.unpack_from("<IIII", blob, 0)
        (timestamp_us,) = struct.unpack_from("<Q", blob, 16)
        patch = blob[24:128].split(b"\0", 1)[0].decode("ascii", "replace")
        if magic != MAGIC:
            raise ValueError(f"bad magic 0x{magic:08X} (expected 0x{MAGIC:08X}) - is this an .sd2 file?")

        self.file_version = version
        self.flags = flags
        self.timestamp_us = timestamp_us
        self.patch = patch

        payload = bytearray(blob[HEADER_SIZE:HEADER_SIZE + payload_size])
        if flags & FLAG_OBFUSCATED_POOL:
            mt_xor(ffnv32(patch), payload)

        if flags & FLAG_COMPRESSED:
            (inflated_size, _padding) = struct.unpack_from("<II", payload, 0)
            inflated = zlib.decompress(bytes(payload[10:]), -15, inflated_size)
        else:
            # Uncompressed variant: payload is the memory image itself.
            inflated = bytes(payload)

        self._parse_memory_image(inflated)

    # ------------------------------------------------------------------ #

    def _parse_memory_image(self, blob):
        self.blob = blob
        self.memory_version = struct.unpack_from("<I", blob, 0)[0]
        if self.memory_version not in (1000, 1002):
            raise ValueError(f"unsupported memory version {self.memory_version}")
        (table_count,) = struct.unpack_from("<H", blob, 4)
        off = 6

        self.tables = []
        for _ in range(table_count):
            tid, num_bytes, num_fields, used, nullable = struct.unpack_from("<IHHHB", blob, off)
            off += 11
            self.tables.append({
                "id": tid, "num_bytes": num_bytes, "num_fields": num_fields,
                "num_used": used, "nullable_bitfields": nullable,
                "fields": [], "name": None, "column_names": {},
            })

        for t in self.tables:
            for _ in range(t["num_fields"]):
                fid, start, nullable_index, ftype = struct.unpack_from("<IHBB", blob, off)
                off += 8
                t["fields"].append({
                    "id": fid, "start": start,
                    "nullable_index": nullable_index, "type": ftype,
                })

        for t in self.tables:
            row_offset, row_count = struct.unpack_from("<II", blob, off)
            off += 8
            t["row_offset"] = row_offset
            t["row_count"] = row_count

        (self.pool_offset,) = struct.unpack_from("<I", blob, off)
        self._pool_cache = {}

    # ------------------------------------------------------------------ #

    def pool_entry(self, key):
        """Decode one pool entry (already decrypted bytes) by its cell key."""
        if key in self._pool_cache:
            return self._pool_cache[key]
        pool = self.blob[self.pool_offset:]
        if self.memory_version == 1002:
            if key & 1:
                address = key >> 1
                if address + 2 > len(pool):
                    return None
                (length,) = struct.unpack_from("<H", pool, address)
                data = bytearray(pool[address + 2:address + 2 + length])
            else:
                length = key >> 24
                address = (key >> 1) & 0x7FFFFF
                data = bytearray(pool[address:address + length])
            if len(data) < length:
                return None
            if length:
                mt = MersenneTwister(key)
                mt_xor_stream = bytearray(length)
                i = 0
                while i + 4 <= length:
                    struct.pack_into("<I", mt_xor_stream, i, mt.next_u32())
                    i += 4
                while i < length:
                    mt_xor_stream[i] = mt.next_u32() & 0xFF
                    i += 1
                for i in range(length):
                    data[i] ^= mt_xor_stream[i]
            result = bytes(data)
        else:
            # v1000: u64 key, low word address, high word length,
            # stream seeded with the row index (resolved by caller).
            raise NotImplementedError("v1000 pool keys are resolved per-row; use row_field()")

        self._pool_cache[key] = result
        return result

    def row_field(self, table, field, row_index, raw=False):
        base = table["row_offset"] + table["num_bytes"] * row_index
        pos = base + field["start"]
        ftype = field["type"]
        blob = self.blob

        if self.memory_version == 1000 and ftype in POOL_TYPES:
            (key,) = struct.unpack_from("<Q", blob, pos)
            if key == 0:
                return None
            address = key & 0xFFFFFFFF
            length = key >> 32
            if length:
                data = bytearray(blob[address:address + length])
                if len(data) < length:
                    return None
                mt = MersenneTwister(row_index & 0xFFFFFFFF)
                i = 0
                while i + 4 <= length:
                    w = mt.next_u32()
                    data[i] ^= w & 0xFF
                    data[i + 1] ^= (w >> 8) & 0xFF
                    data[i + 2] ^= (w >> 16) & 0xFF
                    data[i + 3] ^= (w >> 24) & 0xFF
                    i += 4
                while i < length:
                    data[i] ^= mt.next_u32() & 0xFF
                    i += 1
            return self._decode_pool_data(ftype, bytes(data), raw)

        sizes = TYPE_SIZES_1002 if self.memory_version == 1002 else TYPE_SIZES_1000
        size = sizes.get(ftype, 0)

        if ftype in POOL_TYPES:
            if self.memory_version == 1002:
                (key,) = struct.unpack_from("<I", blob, pos)
                if key == 0:
                    return None
                data = self.pool_entry(key)
                if data is None:
                    return None
                return self._decode_pool_data(ftype, data, raw)
            return None

        if ftype == DBT_BYTE:
            return blob[pos]
        if ftype == DBT_USHORT:
            return struct.unpack_from("<H", blob, pos)[0]
        if ftype == DBT_UINT:
            return struct.unpack_from("<I", blob, pos)[0]
        if ftype == DBT_ULONG:
            return struct.unpack_from("<Q", blob, pos)[0]
        if ftype == DBT_SBYTE:
            return struct.unpack_from("<b", blob, pos)[0]
        if ftype == DBT_SHORT:
            return struct.unpack_from("<h", blob, pos)[0]
        if ftype == DBT_INT:
            return struct.unpack_from("<i", blob, pos)[0]
        if ftype == DBT_LONG:
            return struct.unpack_from("<q", blob, pos)[0]
        if ftype == DBT_FLOAT:
            return struct.unpack_from("<f", blob, pos)[0]
        if ftype == DBT_DOUBLE:
            return struct.unpack_from("<d", blob, pos)[0]
        if ftype == DBT_ASCII_CHAR:
            return chr(blob[pos])
        if ftype == DBT_HALF:
            return half_to_float(struct.unpack_from("<H", blob, pos)[0])
        if ftype == DBT_VECTOR2:
            return struct.unpack_from("<ff", blob, pos)
        if ftype == DBT_VECTOR3:
            return struct.unpack_from("<fff", blob, pos)
        if ftype == DBT_VECTOR4:
            return struct.unpack_from("<ffff", blob, pos)
        if ftype == DBT_MATRIX4X4:
            return struct.unpack_from("<16f", blob, pos)
        if ftype == DBT_BOX3:
            return struct.unpack_from("<6f", blob, pos)
        if ftype == DBT_HALF_MATRIX4X3:
            hs = struct.unpack_from("<12H", blob, pos)
            return tuple(half_to_float(h) for h in hs)
        return None

    @staticmethod
    def _decode_pool_data(ftype, data, raw):
        if raw:
            return data
        if ftype == DBT_STRING:
            return data.split(b"\0", 1)[0].decode("utf-8", "replace")
        if ftype in (DBT_BLOB, DBT_BYTE_ARRAY):
            return list(data)
        if ftype == DBT_USHORT_ARRAY:
            return list(struct.unpack(f"<{len(data) // 2}H", data[: (len(data) // 2) * 2]))
        if ftype == DBT_UINT_ARRAY:
            return list(struct.unpack(f"<{len(data) // 4}I", data[: (len(data) // 4) * 4]))
        if ftype == DBT_VECTOR2_ARRAY:
            return list(struct.unpack(f"<{len(data) // 4}f", data[: (len(data) // 4) * 4]))
        if ftype == DBT_VECTOR3_ARRAY:
            return list(struct.unpack(f"<{len(data) // 4}f", data[: (len(data) // 4) * 4]))
        if ftype == DBT_VECTOR4_ARRAY:
            return list(struct.unpack(f"<{len(data) // 4}f", data[: (len(data) // 4) * 4]))
        return data

    def row_nulls(self, table, row_index):
        """Return the set of field indices that are flagged null for a row."""
        nulls = set()
        if table["nullable_bitfields"] == 0:
            return nulls
        base = table["row_offset"] + table["num_bytes"] * row_index + table["num_used"]
        for n, field in enumerate(table["fields"]):
            if field["nullable_index"] == 255:
                continue
            byte = self.blob[base + (field["nullable_index"] // 8)]
            if (byte >> (field["nullable_index"] % 8)) & 1:
                nulls.add(n)
        return nulls

    # ------------------------------------------------------------------ #

    def resolve_names(self, candidates):
        """Label tables/columns by hashing candidate names (FFnv32)."""
        table_by_id = {t["id"]: t for t in self.tables}
        for name in candidates:
            tid = ffnv32(name)
            if tid in table_by_id:
                table_by_id[tid]["name"] = name
        return sum(1 for t in self.tables if t["name"])

    def find_table(self, name):
        tid = ffnv32(name)
        for t in self.tables:
            if t["id"] == tid:
                return t
        return None

    def rows(self, table):
        """Yield dicts of column-name -> value for every row of a table."""
        for y in range(table["row_count"]):
            nulls = self.row_nulls(table, y)
            row = {}
            for fi, field in enumerate(table["fields"]):
                cname = table["column_names"].get(field["id"], f"col_{field['id']:08x}")
                row[cname] = None if fi in nulls else self.row_field(table, field, y)
            yield row


# Default candidate table names: everything PIN's StaticDBLoader reads, plus
# a few extra known Firefall tables useful for browsing.
KNOWN_TABLES = [
    "apt::AbilityData", "apt::StatusEffectData", "apt::StatusEffectTags",
    "apt::CommandType", "apt::BaseCommandDef",
    "dbcharacter::Monster", "dbcharacter::MonsterAttributeRange",
    "dbcharacter::MonsterItemTags", "dbcharacter::MonsterMood",
    "dbcharacter::MonsterMoodName", "dbcharacter::MonsterScaling",
    "dbcharacter::MonsterTitle", "dbcharacter::MonsterVisualOption",
    "dbcharacter::MonsterVisualOptions", "dbcharacter::Faction",
    "dbcharacter::FactionRelations", "dbcharacter::FactionGroup",
    "dbcharacter::FactionGroupMembers", "dbcharacter::FactionReputations",
    "dbcharacter::CharInfo", "dbcharacter::Turret", "dbcharacter::TurretWeapon",
    "dbcharacter::Deployable", "dbcharacter::DeployableCategory",
    "dbcharacter::DeployableFunction", "dbcharacter::DamageResponse",
    "dbcharacter::DamageType", "dbcharacter::Head", "dbcharacter::VoiceSet",
    "dbcharacter::EmoteRecord", "dbcharacter::XPRewardType",
    "dblocalization::LocalizedText", "dblocalization::UITextMap",
    "dbitems::AbilityModule", "dbitems::Battleframe", "dbitems::BattleframeVisuals",
    "dbitems::RootItem", "dbitems::WeaponTemplates", "dbitems::Ammo",
    "dbitems::CarryableObject", "dbitems::ResourceNodeBeacon",
    "dbitems::ItemCharacterScalars", "dbitems::LevelBand",
    "dbzonemetadata::ZoneRecord", "dbencounterdata::MapMarkerInfo",
    "dbencounterdata::SinCardTemplate", "dbvisualrecords::VisualRecord",
    "dbvisualrecords::WarpaintPalette", "vcs::CharacterDefinition",
]



def _snake(name):
    """PascalCase -> snake_case (same convention as PIN's naming policy)."""
    out = []
    for i, ch in enumerate(name):
        if ch.isupper() and i > 0 and not name[i - 1].isupper() and name[i - 1] != "_":
            out.append("_")
        out.append(ch.lower())
    return "".join(out)


MANUAL_COLUMN_NAMES = {
    "DamageType": "damageType",
    "OrnamentsMapGroupId1": "ornaments_map_group_id_1",
    "OrnamentsMapGroupId2": "ornaments_map_group_id_2",
    "FlightFx1stPersonId": "flight_fx_1st_person_id",
    "IconId": "iconId", "IntroRadioId": "introRadioId",
    "Stage2RadioId": "stage2RadioId", "Stage3RadioId": "stage3RadioId",
    "Stage4RadioId": "stage4RadioId", "Stage5RadioId": "stage5RadioId",
    "ShowNavigation": "showNavigation", "HideCasing": "hideCasing",
    "BroadcastPriority": "broadcastPriority", "IgnoreSIN": "ignoreSIN",
    "ShowWaypoint": "showWaypoint", "ZoneType": "zoneType",
}


def harvest_pin_names(game_dir):
    """Harvest exact table names and column candidates from PIN's source.

    Tables come from `LoadStaticDB<T>("schema::Name")` calls in
    StaticDBLoader.cs; columns from the C# record properties under
    StaticDB/Records (PascalCase -> snake_case, plus the loader's manual
    conversions). Returns (table_names, column_hash_to_name).
    """
    import os
    import re
    table_names = []
    loader = os.path.join(game_dir, "StaticDB", "Loaders", "StaticDBLoader.cs")
    if os.path.isfile(loader):
        with open(loader, "r", encoding="utf-8") as fh:
            table_names = re.findall("LoadStaticDB<[^\>]+>\s*\(\"([^\"]+)\"\)", fh.read())

    col_by_hash = {}
    seen = set()
    records_root = os.path.join(game_dir, "StaticDB", "Records")
    prop_re = re.compile(r"public\s+[\w<>\[\]?.,]+\s+(\w+)\s*\{")
    if os.path.isdir(records_root):
        for root, _dirs, files in os.walk(records_root):
            for fname in files:
                if not fname.endswith(".cs"):
                    continue
                with open(os.path.join(root, fname), "r", encoding="utf-8") as fh:
                    text = fh.read()
                for prop in prop_re.findall(text):
                    if prop in seen:
                        continue
                    seen.add(prop)
                    for cand in (_snake(prop), MANUAL_COLUMN_NAMES.get(prop)):
                        if cand:
                            col_by_hash.setdefault(ffnv32(cand), cand)
    return table_names, col_by_hash


def apply_column_names(db, col_by_hash):
    count = 0
    for t in db.tables:
        for f in t["fields"]:
            name = col_by_hash.get(f["id"])
            if name:
                t["column_names"][f["id"]] = name
                count += 1
    return count


def load_names(path):
    names = list(KNOWN_TABLES)
    if path:
        with open(path, "r", encoding="utf-8") as fh:
            for line in fh:
                line = line.strip()
                if line and not line.startswith("#"):
                    names.append(line)
    return names


def cmd_info(db, args):
    db.resolve_names(load_names(args.names))
    print(f"file           : {args.sdb}")
    print(f"patch          : {db.patch}")
    print(f"file version   : {db.file_version}")
    print(f"memory version : {db.memory_version}")
    print(f"flags          : 0x{db.flags:X}")
    print(f"tables         : {len(db.tables)}")
    print(f"pool offset    : 0x{db.pool_offset:X}")
    print()
    for i, t in enumerate(db.tables):
        name = t["name"] or f"0x{t['id']:08X}"
        print(f"[{i:3}] {name:<45} rows={t['row_count']:<7} cols={t['num_fields']:<4} stride={t['num_bytes']}")
    return 0


def cmd_tables(db, args):
    db.resolve_names(load_names(args.names))
    for t in db.tables:
        print(f"0x{t['id']:08X}\t{t['name'] or '?'}\t{t['row_count']}")
    return 0


def _jsonable(value):
    if isinstance(value, bytes):
        try:
            return value.split(b"\0", 1)[0].decode("utf-8", "replace")
        except Exception:
            return list(value)
    if isinstance(value, tuple):
        return list(value)
    return value


def cmd_dump(db, args):
    db.resolve_names(load_names(args.names))
    table = db.find_table(args.table)
    if table is None:
        print(f"error: table {args.table!r} not found (hash 0x{ffnv32(args.table):08X})", file=sys.stderr)
        return 1
    # Resolve column names from snake_case candidates of common C# shapes.
    rows = []
    for row in db.rows(table):
        rows.append({k: _jsonable(v) for k, v in row.items()})
    out = {
        "table": table["name"] or f"0x{table['id']:08X}",
        "columns": [
            {
                "name": table["column_names"].get(f["id"], f"col_{f['id']:08x}"),
                "type": TYPE_NAMES.get(f["type"], str(f["type"])),
                "nullable": f["nullable_index"] != 255,
            }
            for f in table["fields"]
        ],
        "rows": rows,
    }
    _emit(out, args.output)
    return 0


def cmd_monsters(db, args):
    """Build the mobs/NPCs report: dbcharacter::Monster + names + factions."""
    names = load_names(args.names)
    # Ensure the columns can be labelled by generating snake_case candidates
    # from the PIN C# record property names is out of scope here; instead we
    # resolve per-column by trying snake_case of the well-known field list.
    monster_cols = [
        "id", "localized_name_id", "backpack_id", "fast_speed", "xp_resource_id",
        "hair_color", "head_acc2_id", "normal_speed", "eye_color",
        "bodysuit_warpaint_palette_id", "body_radius", "behavior", "lip_color",
        "fullbody_warpaint_palette_id", "ornaments_map_group_id_4",
        "ornaments_map_group_id_1", "weapon1_id", "chassis_id",
        "glow_warpaint_palette_id", "behavior_defensive_instance_id",
        "posetype_id", "skin_color", "scaling_table_id", "min_rand_scale",
        "visual_options_id", "health_regen", "loot_table2_id", "faction_id",
        "weapon2_id", "head_id", "body_mass", "xpreward_type",
        "behavior_offensive_instance_id", "visuals_group_id",
        "ai_spawn_delay_ms", "terminal_type_name", "behavior_offensive",
        "eyes_id", "behavior_defensive", "behavior_instance_id",
        "head_acc1_id", "charinfo_id", "crafting_type_id", "network_fidelity",
        "facial_hair_color", "max_rand_scale", "ornaments_map_group_id_2",
        "vendor_id", "armor_warpaint_palette_id", "difficulty_cost", "id",
        "body_height", "loot_table_id", "voice_set", "title", "respawn_flags",
        "gravity", "is_componented", "damage_response_id", "gender", "race",
        "projectile_offset",
    ]
    tables = {
        "monster": "dbcharacter::Monster",
        "text": "dblocalization::LocalizedText",
        "faction": "dbcharacter::Faction",
        "scaling": "dbcharacter::MonsterScaling",
        "turret": "dbcharacter::Turret",
    }
    resolved = {}
    for key, tname in tables.items():
        t = db.find_table(tname)
        if t is None:
            print(f"warning: {tname} not present in this database", file=sys.stderr)
            resolved[key] = None
            continue
        # column name resolution for this table
        for cand in _column_candidates(t, monster_cols if key == "monster" else None):
            t["column_names"][ffnv32(cand)] = cand
        resolved[key] = t

    t_monster = resolved["monster"]
    if t_monster is None:
        print("error: no dbcharacter::Monster table", file=sys.stderr)
        return 1

    text_by_id = {}
    if resolved["text"] is not None:
        for row in db.rows(resolved["text"]):
            rid = row.get("id")
            eng = row.get("english")
            if rid is not None and eng:
                text_by_id[rid] = eng

    faction_by_id = {}
    if resolved["faction"] is not None:
        for row in db.rows(resolved["faction"]):
            fid = row.get("id")
            if fid is None:
                continue
            faction_by_id[fid] = {
                "internal_name": row.get("internal_name"),
                "name": text_by_id.get(row.get("localized_name_id")),
            }

    scaling_by_id = {}
    if resolved["scaling"] is not None:
        for row in db.rows(resolved["scaling"]):
            sid = row.get("id")
            if sid is None:
                continue
            scaling_by_id.setdefault(sid, []).append({
                "level": row.get("level"), "health": row.get("health"),
                "damage": row.get("damage"),
            })

    monsters = []
    for row in db.rows(t_monster):
        mid = row.get("id")
        if mid is None:
            continue
        entry = {
            "id": mid,
            "name": text_by_id.get(row.get("localized_name_id")),
            "faction_id": row.get("faction_id"),
            "faction": (faction_by_id.get(row.get("faction_id")) or {}).get("internal_name"),
            "faction_name": (faction_by_id.get(row.get("faction_id")) or {}).get("name"),
            "race": row.get("race"),
            "gender": row.get("gender"),
            "chassis_id": row.get("chassis_id"),
            "weapon1_id": row.get("weapon1_id"),
            "weapon2_id": row.get("weapon2_id"),
            "behavior": row.get("behavior"),
            "behavior_offensive": row.get("behavior_offensive"),
            "behavior_defensive": row.get("behavior_defensive"),
            "health_regen": row.get("health_regen"),
            "scaling_table_id": row.get("scaling_table_id"),
            "scaling": scaling_by_id.get(row.get("scaling_table_id")),
            "loot_table_id": row.get("loot_table_id"),
            "loot_table2_id": row.get("loot_table2_id"),
            "difficulty_cost": row.get("difficulty_cost"),
            "ai_spawn_delay_ms": row.get("ai_spawn_delay_ms"),
            "normal_speed": row.get("normal_speed"),
            "fast_speed": row.get("fast_speed"),
        }
        monsters.append(entry)

    turrets = None
    if resolved["turret"] is not None:
        turrets = [
            {k: _jsonable(v) for k, v in r.items()}
            for r in db.rows(resolved["turret"])
        ]

    out = {
        "patch": db.patch,
        "count": len(monsters),
        "monsters": monsters,
        "factions": faction_by_id,
        "turrets": turrets,
    }
    _emit(out, args.output)
    print(f"monsters: {len(monsters)} (named: {sum(1 for m in monsters if m['name'])})", file=sys.stderr)
    return 0


SPAWNABLE_KINDS = {
    # kind      -> (table, name column, extra columns to include)
    "monster":   ("dbcharacter::Monster",     "localized_name_id",
                  ["faction_id", "chassis_id", "weapon1_id", "weapon2_id", "behavior",
                   "scaling_table_id", "loot_table_id", "health_regen", "ai_spawn_delay_ms"]),
    "deployable": ("dbcharacter::Deployable", "localized_name_id",
                  ["default_faction", "standard_health", "start_hitpoints",
                   "deployable_category", "function", "scale", "build_time_ms"]),
    "vehicle":   ("vcs::VehicleInfo",         "localized_name_id",
                  ["faction_id", "vehicle_class", "race", "scaling_table_id"]),
    "carryable": ("dbitems::CarryableObject", "localized_name_id",
                  ["type", "visual_record_id", "pickup_radius", "ability_granted_id"]),
    "turret":    ("dbcharacter::Turret",      "name",
                  ["posture", "attack_type", "behavior", "visualrec"]),
}


def _localized(db):
    """id -> English string map from dblocalization::LocalizedText."""
    table = db.find_table("dblocalization::LocalizedText")
    if table is None:
        return {}
    out = {}
    for row in db.rows(table):
        rid, eng = row.get("id"), row.get("english")
        if rid is not None and eng and eng.strip():
            out[rid] = eng
    return out


def cmd_spawnables(db, args):
    """Catalog of everything PIN's `spawn` command can create from the SDB.

    Mirrors GameServer/StaticDB/SDBCatalog.cs: one section per spawn kind,
    with ids, resolved names and the fields the in-game `sdbinfo` shows.
    """
    db.resolve_names(load_names(args.names))
    text = _localized(db)

    factions = {}
    ftable = db.find_table("dbcharacter::Faction")
    if ftable is not None:
        for row in db.rows(ftable):
            factions[row.get("id")] = row.get("internal_name") or text.get(row.get("localized_name_id"))

    kinds = [args.table] if args.table else list(SPAWNABLE_KINDS)
    out = {"patch": db.patch, "kinds": {}}
    for kind in kinds:
        if kind not in SPAWNABLE_KINDS:
            print(f"error: unknown kind {kind!r}; expected one of "
                  f"{', '.join(SPAWNABLE_KINDS)}", file=sys.stderr)
            return 1
        tname, name_col, extra = SPAWNABLE_KINDS[kind]
        table = db.find_table(tname)
        if table is None:
            print(f"warning: {tname} not in this database", file=sys.stderr)
            continue

        entries = []
        for row in db.rows(table):
            rid = row.get("id")
            if rid is None:
                continue
            if name_col == "name":
                name = row.get("name") or None
            else:
                name = text.get(row.get(name_col))
            entry = {"id": rid, "name": name}
            fid = row.get("faction_id", row.get("default_faction"))
            if fid:
                entry["faction"] = factions.get(fid)
            for col in extra:
                if col in row:
                    entry[col] = _jsonable(row[col])
            entries.append(entry)

        entries.sort(key=lambda e: e["id"])
        named = sum(1 for e in entries if e["name"])
        out["kinds"][kind] = {
            "table": tname, "count": len(entries), "named": named,
            "spawn_command": f"spawn {kind} <id|name> [<x> <y> <z>]",
            "entries": entries,
        }
        print(f"{kind:<10} {len(entries):>5} rows ({named} named)  {tname}", file=sys.stderr)

    _emit(out, args.output)
    return 0


def cmd_coverage(db, args):
    """Report how much of the .sd2 PIN actually reads.

    Compares the tables in the file against the `LoadStaticDB<T>("...")` calls
    in PIN's StaticDBLoader, so it is obvious which game data is still unused.
    """
    table_names, _cols = harvest_pin_names(args.game_dir)
    loaded = set(table_names)
    db.resolve_names(load_names(args.names) + list(loaded))

    present = {t["name"]: t for t in db.tables if t["name"]}
    total_rows = sum(t["row_count"] for t in db.tables)
    loaded_rows = sum(t["row_count"] for n, t in present.items() if n in loaded)

    print(f"file           : {args.sdb}")
    print(f"patch          : {db.patch}")
    print(f"tables in file : {len(db.tables)}")
    print(f"identified     : {len(present)}")
    print(f"loaded by PIN  : {sum(1 for n in present if n in loaded)}")
    print(f"unidentified   : {len(db.tables) - len(present)}")
    print(f"rows           : {loaded_rows:,} of {total_rows:,} "
          f"({100.0 * loaded_rows / max(total_rows, 1):.1f}%) in PIN-loaded tables")
    print()

    by_schema = {}
    for name, table in present.items():
        schema = name.split("::")[0]
        stats = by_schema.setdefault(schema, [0, 0, 0])
        stats[1] += 1
        stats[2] += table["row_count"]
        if name in loaded:
            stats[0] += 1
    print("schema               loaded/known   rows")
    for schema in sorted(by_schema):
        got, known, rows = by_schema[schema]
        print(f"  {schema:<20} {got:>4}/{known:<5} {rows:>10,}")

    unread = sorted(n for n in present if n not in loaded)
    if unread:
        print()
        print("identified but NOT read by PIN:")
        for name in unread:
            print(f"  {name:<45} rows={present[name]['row_count']}")

    stale = sorted(n for n in loaded if n not in present)
    if stale:
        print()
        print("read by PIN but missing from this file:")
        for name in stale:
            print(f"  {name}")
    return 0


def _column_candidates(table, extra):
    """Column names can only be guessed by candidates; return known ones."""
    candidates = set(extra or [])
    # Common columns every PIN-read table carries.
    candidates.update(["id"])
    return candidates


def _emit(obj, output):
    text = json.dumps(obj, indent=2, ensure_ascii=False, default=_jsonable)
    if output:
        with open(output, "w", encoding="utf-8") as fh:
            fh.write(text)
        print(f"wrote {output} ({len(text):,} bytes)", file=sys.stderr)
    else:
        print(text)


def main(argv=None):
    parser = argparse.ArgumentParser(description="Firefall StaticDB (.sd2) dumper")
    parser.add_argument("command", choices=["info", "tables", "dump", "monsters", "spawnables", "coverage"])
    parser.add_argument("sdb", help="path to clientdb.sd2")
    parser.add_argument("table", nargs="?", help="table name for 'dump' (schema::Name), or kind for 'spawnables'")
    parser.add_argument("-o", "--output", help="write JSON here instead of stdout")
    parser.add_argument("--names", help="extra candidate table-name list (one per line)")
    parser.add_argument("--game-dir", default=os.path.join(os.path.dirname(os.path.abspath(__file__)), "..", "..", "UdpHosts", "GameServer"),
                        help="PIN GameServer source dir for name harvesting (default: auto)")
    args = parser.parse_args(argv)

    if args.command == "dump" and not args.table:
        parser.error("the 'dump' command needs a table name")

    db = StaticDB(args.sdb)

    # Prefer exact names harvested from PIN's source when available.
    if os.path.isdir(args.game_dir):
        table_names, col_by_hash = harvest_pin_names(args.game_dir)
        known = KNOWN_TABLES + [n for n in table_names if n not in KNOWN_TABLES]
        by_id = {ffnv32(n): n for n in known}
        for t in db.tables:
            if t["id"] in by_id:
                t["name"] = by_id[t["id"]]
        applied = apply_column_names(db, col_by_hash)
        print(f"harvested {len(table_names)} table names and labelled {applied} columns from PIN source", file=sys.stderr)

    return {
        "info": cmd_info,
        "tables": cmd_tables,
        "dump": cmd_dump,
        "monsters": cmd_monsters,
        "spawnables": cmd_spawnables,
        "coverage": cmd_coverage,
    }[args.command](db, args)


if __name__ == "__main__":
    sys.exit(main())
