using System;
using System.Collections.Generic;
using GameServer.StaticDB.Records.dbitems;

namespace GameServer.Systems.Salvage;

/// <summary>
///     One thing a salvage handed back: the item (as a <c>dbitems::RootItem</c> id), how many,
///     and which loot table the roll landed on.
/// </summary>
public sealed record SalvageAward(uint SdbId, uint Quantity, uint LootTableId);

/// <summary>
///     Rolls a <c>dbitems::LootTable</c> the way salvaging needs it. Two roll semantics live in
///     the data: mode 0 tables pick a single row weighted by <c>probability</c> (what the token
///     vending machines use, see <see cref="Vendor.VendorTokenRoll" />), while the salvage trees
///     (modes 2-4, e.g. the "NON-GEAR GENERIC SALVAGING RESULTS" and gear salvage tables) roll
///     every row independently at <c>probability</c> percent - every full 100 guarantees rolls and
///     the remainder is the chance of one more, which is also why the same sub-table appears six
///     times in the Tier-1 weapon salvage table at exactly 100.
/// </summary>
/// <remarks>
///     The reading is anchored in <c>dbitems::SalvageDisplayItems</c>: the rows a table hands out
///     at probability 100 are exactly the ones the client labels <c>guaranteed</c> in the salvage
///     preview (a Small Health Pack always yields crystite and raw crystite), and the combined
///     guaranteed-plus-chance reading reproduces the authored "1-2 common parts" style rows that
///     list the same part twice at 100 and at 2.
/// </remarks>
public static class SalvageRoller
{
    /// <summary>
    ///     How deep a roll may follow nested sub-tables. The guard is for a cycle in the tables,
    ///     not for depth.
    /// </summary>
    private const int MaxTableDepth = 8;

    /// <summary>Rolls a salvage loot table from the loaded database, once.</summary>
    /// <param name="tableId">The <c>dbitems::LootTable</c> id.</param>
    /// <returns>The awards; empty when the table does not exist or no row it has fires.</returns>
    public static IReadOnlyList<SalvageAward> Roll(uint tableId)
    {
        return Roll(
            tableId,
            Random.Shared,
            StaticDB.SDBInterface.GetLootTable,
            StaticDB.SDBInterface.GetLootTableItemDists,
            StaticDB.SDBInterface.GetLootTableSubTableDists);
    }

    /// <summary>Test seam: the same roll with the database lookups and the randomness injected.</summary>
    internal static IReadOnlyList<SalvageAward> Roll(
        uint tableId,
        Random random,
        Func<uint, LootTable> tableLookup,
        Func<uint, IReadOnlyList<LootTableItemDist>> itemRowLookup,
        Func<uint, IReadOnlyList<LootTableSubTableDist>> subTableRowLookup)
    {
        var awards = new List<SalvageAward>();
        if (random == null || tableLookup == null || itemRowLookup == null || subTableRowLookup == null)
        {
            return awards;
        }

        RollTable(tableId, 0, new Stack<uint>(), awards, random, tableLookup, itemRowLookup, subTableRowLookup);
        return awards;
    }

    private static void RollTable(
        uint tableId,
        int depth,
        Stack<uint> path,
        List<SalvageAward> awards,
        Random random,
        Func<uint, LootTable> tableLookup,
        Func<uint, IReadOnlyList<LootTableItemDist>> itemRowLookup,
        Func<uint, IReadOnlyList<LootTableSubTableDist>> subTableRowLookup)
    {
        // A dangling id, a table nested too deep, or a cycle in the making: nothing to award.
        // The path (not a flat visited set) guards recursion, because a salvage table lists the
        // same sub-table in several rows on purpose - each of those rows must roll.
        var table = tableLookup(tableId);
        if (table == null || depth >= MaxTableDepth || path.Contains(tableId))
        {
            return;
        }

        path.Push(tableId);
        try
        {
            IReadOnlyList<LootTableItemDist> itemRows = itemRowLookup(tableId) ?? [];
            IReadOnlyList<LootTableSubTableDist> subTableRows = subTableRowLookup(tableId) ?? [];

            if (table.RollMode == 0)
            {
                RollWeightedPick(tableId, itemRows, subTableRows, depth, path, awards, random, tableLookup, itemRowLookup, subTableRowLookup);
                return;
            }

            // Roll-every-row: each row fires `probability / 100` times for sure, plus once more
            // with `probability % 100` percent chance.
            foreach (var row in itemRows)
            {
                var times = IndependentRolls(row.Probability, random);
                for (var i = 0; i < times; i++)
                {
                    awards.Add(new SalvageAward(row.ItemdropId, RollQuantity(row.MinQuantity, row.MaxQuantity, random), tableId));
                }
            }

            foreach (var row in subTableRows)
            {
                var times = IndependentRolls(row.Probability, random);
                for (var i = 0; i < times; i++)
                {
                    RollTable(row.SubtableId, depth + 1, path, awards, random, tableLookup, itemRowLookup, subTableRowLookup);
                }
            }
        }
        finally
        {
            path.Pop();
        }
    }

    /// <summary>Mode 0: one row wins, picked by probability out of the rows' summed weights.</summary>
    private static void RollWeightedPick(
        uint tableId,
        IReadOnlyList<LootTableItemDist> itemRows,
        IReadOnlyList<LootTableSubTableDist> subTableRows,
        int depth,
        Stack<uint> path,
        List<SalvageAward> awards,
        Random random,
        Func<uint, LootTable> tableLookup,
        Func<uint, IReadOnlyList<LootTableItemDist>> itemRowLookup,
        Func<uint, IReadOnlyList<LootTableSubTableDist>> subTableRowLookup)
    {
        int totalWeight = 0;
        foreach (var row in itemRows)
        {
            totalWeight += row.Probability;
        }

        foreach (var row in subTableRows)
        {
            totalWeight += row.Probability;
        }

        if (totalWeight <= 0)
        {
            return;
        }

        int pick = random.Next(totalWeight);

        foreach (var row in itemRows)
        {
            if (pick < row.Probability)
            {
                awards.Add(new SalvageAward(row.ItemdropId, RollQuantity(row.MinQuantity, row.MaxQuantity, random), tableId));
                return;
            }

            pick -= row.Probability;
        }

        foreach (var row in subTableRows)
        {
            if (pick < row.Probability)
            {
                RollTable(row.SubtableId, depth + 1, path, awards, random, tableLookup, itemRowLookup, subTableRowLookup);
                return;
            }

            pick -= row.Probability;
        }
    }

    private static int IndependentRolls(ushort probability, Random random)
    {
        var rolls = probability / 100;
        if (probability % 100 != 0 && random.Next(100) < probability % 100)
        {
            rolls++;
        }

        return rolls;
    }

    private static uint RollQuantity(ushort minQuantity, ushort maxQuantity, Random random)
    {
        uint low = minQuantity;
        uint high = maxQuantity < low ? low : maxQuantity;
        uint count = low == high ? low : low + (uint)random.Next((int)(high - low + 1));

        // Rows authored 0..0 (unlock-style rows such as the rare salvage materials) award one.
        return count == 0 ? 1 : count;
    }
}
