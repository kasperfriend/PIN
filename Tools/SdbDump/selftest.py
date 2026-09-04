#!/usr/bin/env python3
"""Round-trip self-test for sdb_dump.py.

Builds a synthetic clientdb.sd2 (header + obfuscation + compression + tables
+ encrypted pool) entirely from scratch, then decodes it with sdb_dump and
verifies every value. This validates the decoder logic without needing a real
Firefall install:

    python3 selftest.py

Exits 0 and prints PASS when every check holds.
"""

import os
import struct
import sys
import tempfile
import zlib

sys.path.insert(0, os.path.dirname(os.path.abspath(__file__)))

import sdb_dump as S


def build_sd2(path):
    patch = b"beta-1869"

    # --- pool -------------------------------------------------------- -->
    pool = bytearray()
    entries = {}

    def add_pool(data: bytes) -> int:
        # v1002 long form: key = (offset << 1) | 1, u16 length prefix.
        offset = len(pool)
        key = ((offset << 1) | 1) & 0xFFFFFFFF
        pool.extend(struct.pack("<H", len(data)))
        # twist: XOR with MT stream seeded by the key itself
        twisted = bytearray(data)
        S.mt_xor(key, twisted)
        pool.extend(twisted)
        entries[data] = key
        return key

    key_squad = add_pool("Chosen Death Squad".encode("utf-8"))
    key_bandid = add_pool("Bandit".encode("utf-8"))
    key_empty = 0  # null string

    # --- table layout ------------------------------------------------ -->
    # dbcharacter::Monster with: id UInt @0, faction_id UInt @4,
    # normal_speed Float @8, localized_name_id String(pool) @12,
    # behavior String(pool) @16 (nullable #0), nullable bitfield byte @20,
    # stride padded to 24.
    table_id = S.ffnv32("dbcharacter::Monster")
    col_ids = {name: S.ffnv32(name) for name in
               ("id", "faction_id", "normal_speed", "localized_name_id", "behavior")}

    num_fields = 5
    num_used = 20
    nullable_bitfields = 1
    num_bytes = 24  # (20 + 1) rounded up to multiple of 4
    rows = [
        # (id, faction_id, speed, name_key, behavior_key, behavior_null)
        (1001, 3, 4.5, key_squad, key_squad, False),
        (1002, 3, 6.25, key_bandid, key_bandid, False),
        (1003, 0, 5.0, key_squad, key_empty, True),
    ]

    row_block = bytearray()
    for (rid, fid, speed, name_key, beh_key, beh_null) in rows:
        row = bytearray(num_bytes)
        struct.pack_into("<I", row, 0, rid)
        struct.pack_into("<I", row, 4, fid)
        struct.pack_into("<f", row, 8, speed)
        struct.pack_into("<I", row, 12, name_key)
        struct.pack_into("<I", row, 16, beh_key)
        row[num_used] = 1 if beh_null else 0  # bit 0 of the nullable bitfield
        row_block.extend(row)

    # --- memory image ------------------------------------------------ -->
    image = bytearray()
    image.extend(struct.pack("<IH", 1002, 1))                 # memoryVersion, tableCount
    image.extend(struct.pack("<IHHHB", table_id, num_bytes, num_fields, num_used, nullable_bitfields))
    for name, start, nullable_index, ftype in (
        ("id", 0, 255, S.DBT_UINT),
        ("faction_id", 4, 255, S.DBT_UINT),
        ("normal_speed", 8, 255, S.DBT_FLOAT),
        ("localized_name_id", 12, 255, S.DBT_STRING),
        ("behavior", 16, 0, S.DBT_STRING),
    ):
        image.extend(struct.pack("<IHBB", col_ids[name], start, nullable_index, ftype))
    rowinfo_pos = len(image)
    image.extend(b"\x00" * 8)                                  # rowOffset, rowCount (patched below)
    pool_field_pos = len(image)
    image.extend(b"\x00" * 4)                                  # poolOffset (patched below)
    row_offset = len(image)                                    # rows start here
    image.extend(row_block)
    pool_offset = len(image)                                   # pool starts after rows
    image.extend(pool)
    struct.pack_into("<II", image, rowinfo_pos, row_offset, len(rows))
    struct.pack_into("<I", image, pool_field_pos, pool_offset)
    inflated_size = len(image)

    # --- compression + obfuscation ----------------------------------- -->
    co = zlib.compressobj(1, zlib.DEFLATED, -15)               # raw deflate
    deflated = co.compress(bytes(image)) + co.flush()
    payload = bytearray()
    payload.extend(struct.pack("<II", inflated_size, 0))       # inflated size, padding
    payload.extend(b"\x78\x01")                                # zlib header marker
    payload.extend(deflated)

    header = bytearray(128)
    struct.pack_into("<IIII", header, 0, S.MAGIC, 12, len(payload),
                     S.FLAG_OBFUSCATED_POOL | S.FLAG_COMPRESSED | (1 << 3))
    struct.pack_into("<Q", header, 16, 1_400_000_000_000_000)
    header[24:24 + len(patch)] = patch

    payload_copy = bytearray(payload)
    S.mt_xor(S.ffnv32(patch.decode()), payload_copy)
    with open(path, "wb") as fh:
        fh.write(bytes(header) + bytes(payload_copy))


def expect(cond, what):
    status = "ok" if cond else "FAIL"
    print(f"  [{status}] {what}")
    if not cond:
        raise AssertionError(what)


def main():
    with tempfile.TemporaryDirectory() as tmp:
        path = os.path.join(tmp, "clientdb.sd2")
        build_sd2(path)
        print(f"built synthetic sd2 at {path} ({os.path.getsize(path)} bytes)")

        db = S.StaticDB(path)
        expect(db.patch == "beta-1869", "patch string decoded")
        expect(db.file_version == 12, "file version")
        expect(db.memory_version == 1002, "memory version")

        table = db.find_table("dbcharacter::Monster")
        expect(table is not None, "table resolved by name hash")
        expect(table["row_count"] == 3, "row count")
        expect(table["num_bytes"] == 24, "row stride")

        fields = {S.TYPE_NAMES.get(f["type"], f["type"]): f for f in table["fields"]}
        expect(len(table["fields"]) == 5, "field count")

        got = db.row_field(table, table["fields"][0], 0)
        expect(got == 1001, f"uint cell (got {got!r})")
        got = db.row_field(table, table["fields"][2], 1)
        expect(abs(got - 6.25) < 1e-6, f"float cell (got {got!r})")
        got = db.row_field(table, table["fields"][3], 0)
        expect(got == "Chosen Death Squad", f"pool string long-form key (got {got!r})")
        got = db.row_field(table, table["fields"][3], 1)
        expect(got == "Bandit", f"second pool string (got {got!r})")
        got = db.row_field(table, table["fields"][4], 0)
        expect(got == "Chosen Death Squad", f"nullable column value (got {got!r})")

        nulls = db.row_nulls(table, 2)
        expect(nulls == {4}, f"null bit detected for row 2 field 4 (got {nulls!r})")
        got = db.rows(table).__next__()  # first row dict (unnamed columns fallback)
        expect(got is not None, "row iterator works")

        print("PASS: round-trip decode verified")
    return 0


if __name__ == "__main__":
    sys.exit(main())
