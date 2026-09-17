using System;
using System.Collections.Generic;
using GameServer.StaticDB;
using GameServer.StaticDB.Records.dbitems;

namespace GameServer.Systems.Loot;

/// <summary>One item a loot roll produced.</summary>
/// <param name="SdbId">The <c>dbitems::RootItem</c>.</param>
/// <param name="Quantity">How many.</param>
/// <param name="LootTableId">The table the row came out of.</param>
public sealed record LootAward(uint SdbId, uint Quantity, uint LootTableId);

/// <summary>
///     Rolls a <c>dbitems::LootTable</c> the way the crates, caches, lockers, booster packs and
///     upgrade kits expect, honouring the table's <c>roll_mode</c>:
///     <list type="bullet">
///         <item><c>0</c>/<c>1</c> - weighted: one row wins, by <c>probability</c> over the table's own sum (items and sub-tables together).</item>
///         <item><c>2</c> - every row pays out (a booster pack lists its five sub-packs at 100 each).</item>
///         <item><c>3</c>/<c>4</c> - each row is an independent percentage chance (<c>probability</c> out of 100).</item>
///         <item><c>5</c> - fabrication: weighted, like 0.</item>
///     </list>
///     Quantities are uniform over <c>min_quantity..max_quantity</c>, a 0..0 row awarding one.
///     <c>stack_duplicate_results</c> merges repeated items of one roll into a single award.
/// </summary>
public static class LootTableRoller
{
    private const int MaxTableDepth = 10;

    public static IReadOnlyList<LootAward> Roll(uint lootTableId) =>
        Roll(lootTableId, Random.Shared, SDBInterface.GetLootTable, SDBInterface.GetLootTableItemDists, SDBInterface.GetLootTableSubTableDists);

    /// <summary>Test seam: the roll with the database lookups and the randomness injected.</summary>
    public static IReadOnlyList<LootAward> Roll(
        uint lootTableId,
        Random random,
        Func<uint, LootTable> tableLookup,
        Func<uint, IReadOnlyList<LootTableItemDist>> itemRowLookup,
        Func<uint, IReadOnlyList<LootTableSubTableDist>> subTableRowLookup)
    {
        var awards = new List<LootAward>();
        if (random == null || tableLookup == null || itemRowLookup == null || subTableRowLookup == null)
        {
            return awards;
        }

        RollTable(lootTableId, 0, [], awards, random, tableLookup, itemRowLookup, subTableRowLookup);

        var root = tableLookup(lootTableId);
        return root is { StackDuplicateResults: 1 } ? Stack(awards) : awards;
    }

    private static void RollTable(
        uint tableId,
        int depth,
        HashSet<uint> path,
        List<LootAward> awards,
        Random random,
        Func<uint, LootTable> tableLookup,
        Func<uint, IReadOnlyList<LootTableItemDist>> itemRowLookup,
        Func<uint, IReadOnlyList<LootTableSubTableDist>> subTableRowLookup)
    {
        var table = tableId == 0 ? null : tableLookup(tableId);
        if (table == null || depth >= MaxTableDepth || !path.Add(tableId))
        {
            return;
        }

        try
        {
            IReadOnlyList<LootTableItemDist> itemRows = itemRowLookup(tableId) ?? [];
            IReadOnlyList<LootTableSubTableDist> subTableRows = subTableRowLookup(tableId) ?? [];

            switch (table.RollMode)
            {
                case 2:
                    foreach (var row in itemRows)
                    {
                        Award(row, tableId, awards, random);
                    }

                    foreach (var row in subTableRows)
                    {
                        RollTable(row.SubtableId, depth + 1, path, awards, random, tableLookup, itemRowLookup, subTableRowLookup);
                    }

                    return;

                case 3:
                case 4:
                    foreach (var row in itemRows)
                    {
                        if (random.Next(100) < row.Probability)
                        {
                            Award(row, tableId, awards, random);
                        }
                    }

                    foreach (var row in subTableRows)
                    {
                        if (random.Next(100) < row.Probability)
                        {
                            RollTable(row.SubtableId, depth + 1, path, awards, random, tableLookup, itemRowLookup, subTableRowLookup);
                        }
                    }

                    return;

                default:
                    RollWeighted(tableId, itemRows, subTableRows, depth, path, awards, random, tableLookup, itemRowLookup, subTableRowLookup);
                    return;
            }
        }
        finally
        {
            // Siblings may legitimately share a sub-table; only a cycle along the current path is refused.
            path.Remove(tableId);
        }
    }

    private static void RollWeighted(
        uint tableId,
        IReadOnlyList<LootTableItemDist> itemRows,
        IReadOnlyList<LootTableSubTableDist> subTableRows,
        int depth,
        HashSet<uint> path,
        List<LootAward> awards,
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
                Award(row, tableId, awards, random);
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

    private static void Award(LootTableItemDist row, uint tableId, List<LootAward> awards, Random random)
    {
        if (row.ItemdropId == 0)
        {
            return;
        }

        uint low = row.MinQuantity;
        uint high = row.MaxQuantity < low ? low : row.MaxQuantity;
        uint count = low == high ? low : low + (uint)random.Next((int)(high - low + 1));
        awards.Add(new LootAward(row.ItemdropId, count == 0 ? 1 : count, tableId));
    }

    private static List<LootAward> Stack(List<LootAward> awards)
    {
        var stacked = new List<LootAward>();
        foreach (var award in awards)
        {
            int index = stacked.FindIndex(a => a.SdbId == award.SdbId);
            if (index >= 0)
            {
                stacked[index] = stacked[index] with { Quantity = stacked[index].Quantity + award.Quantity };
            }
            else
            {
                stacked.Add(award);
            }
        }

        return stacked;
    }
}
