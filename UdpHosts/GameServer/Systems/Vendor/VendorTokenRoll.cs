using System;
using System.Collections.Generic;
using GameServer.StaticDB;
using GameServer.StaticDB.Records.dbitems;

namespace GameServer.Systems.Vendor;

/// <summary>
///     One thing a token vending machine dispensed: the item, how many, and which loot table the
///     roll landed on (for the log line a player can be shown).
/// </summary>
/// <param name="SdbId">The <c>dbitems::RootItem</c> awarded.</param>
/// <param name="Quantity">How many, after the machine's scale.</param>
/// <param name="LootTableId">The <c>dbitems::LootTable</c> the award came out of.</param>
public sealed record TokenRollAward(uint SdbId, uint Quantity, uint LootTableId);

/// <summary>
///     The real prize roll of a token vending machine, straight out of the client database:
///     <c>dbitems::VendorTokenLootTables</c> names the loot tables a (machine, key item) pair rolls
///     and how their quantities are scaled, <c>dbitems::LootTableItemDist</c> and
///     <c>dbitems::LootTableSubTableDist</c> hold the odds, and <c>dbitems::LootTable</c> the tables
///     themselves.
/// </summary>
/// <remarks>
///     <para>
///     <b>The machine's slots.</b> Every <c>VendorTokenLootTables</c> row of a machine is one slot it
///     dispenses, so one token can pay out more than once. Build prod-1962 authors the Accord Reward
///     Quartermaster (machine 5, key item 85771) with two: loot table 5853, named "Accord Reward
///     Quartermaster Vending - Slot 1 (Crystite)", and 5857, "Accord T1 Reward Quartermaster Vending
///     - Slot 2 - \"Rare Item\" % check". Machines 105, 106 and 107 roll the same two tables with
///     <c>loot_table_scale</c> 1.3, 1.6 and 2.0 on slot 1 - the same prize pool, better crystite.
///     </para>
///     <para>
///     <b>How a table is rolled.</b> One row wins, picked by <c>probability</c> out of the sum of the
///     table's own rows, items and sub-tables together; a sub-table row recurses. Normalizing by the
///     table's own sum is what the data supports: slot 1's five crystite rows sum to exactly 1000
///     (598/250/100/50/2 - the last being the 2000..3000 crystite jackpot), but the tables nested
///     under slot 2 sum to 9, 22, 32 and 48, and reading those against a fixed 1000 would mean a
///     branch that was already won then hands out nothing 95..99% of the time. Under this reading
///     slot 2's own 850 is also relative, so a token always dispenses something from each slot.
///     </para>
///     <para>
///     <b>Quantities.</b> Uniform across <c>min_quantity</c>..<c>max_quantity</c>, times
///     <c>loot_table_scale</c> when the row's <c>allow_scaling</c> is set - which in the vendor tables
///     only the crystite rows carry, so a scale changes the payout and never the number of cosmetics.
///     A row authored 0..0 (every unlock, cosmetic, boost and VIP membership) awards one.
///     </para>
///     <para>
///     <b>Not modelled, because the vendor tables author none of it:</b> quality and level rolls
///     (<c>roll_with_quality</c>, <c>level_range</c>, <c>LootTable.min_quality</c>/<c>max_quality</c>
///     are zero in all twelve tables a machine can reach), <c>LootTableDistRequirements</c> (no row
///     references any of their dists), and <c>roll_mode</c> - slot tables are 0, and the one table
///     with mode 2 belongs to machine 2, a web-store machine no NPC carries.
///     </para>
/// </remarks>
public static class VendorTokenRoll
{
    /// <summary>
    ///     How deep a roll may follow nested sub-tables. The vendor trees are three deep at most
    ///     (5857 -&gt; 5854 -&gt; 5984); the guard is for a cycle in the tables, not for depth.
    /// </summary>
    private const int MaxTableDepth = 8;

    /// <summary>Rolls what a machine dispenses for one key token, from the loaded database.</summary>
    /// <param name="machineId">The <c>dbitems::VendorTokenMachine</c> id.</param>
    /// <param name="keyItemId">
    ///     The token being spent. Zero rolls every slot the machine has, whichever token it takes.
    /// </param>
    /// <returns>The awards, one per slot that produced something; empty when the machine has no tables.</returns>
    public static IReadOnlyList<TokenRollAward> Roll(uint machineId, uint keyItemId)
    {
        return Roll(
            machineId,
            keyItemId,
            Random.Shared,
            SDBInterface.GetVendorTokenLootTables(),
            SDBInterface.GetLootTable,
            SDBInterface.GetLootTableItemDists,
            SDBInterface.GetLootTableSubTableDists);
    }

    /// <summary>Test seam: the same roll with the database lookups and the randomness injected.</summary>
    /// <param name="machineId">The machine being played.</param>
    /// <param name="keyItemId">The token spent, or zero for any.</param>
    /// <param name="random">The randomness to roll with.</param>
    /// <param name="machineTables">The <c>dbitems::VendorTokenLootTables</c> rows.</param>
    /// <param name="tableLookup"><c>dbitems::LootTable</c> by id.</param>
    /// <param name="itemRowLookup">A table's <c>dbitems::LootTableItemDist</c> rows.</param>
    /// <param name="subTableRowLookup">A table's <c>dbitems::LootTableSubTableDist</c> rows.</param>
    /// <returns>The awards, in slot order.</returns>
    internal static IReadOnlyList<TokenRollAward> Roll(
        uint machineId,
        uint keyItemId,
        Random random,
        IReadOnlyList<VendorTokenLootTables> machineTables,
        Func<uint, LootTable> tableLookup,
        Func<uint, IReadOnlyList<LootTableItemDist>> itemRowLookup,
        Func<uint, IReadOnlyList<LootTableSubTableDist>> subTableRowLookup)
    {
        var awards = new List<TokenRollAward>();
        if (random == null || machineTables == null || tableLookup == null || itemRowLookup == null || subTableRowLookup == null)
        {
            return awards;
        }

        foreach (var slot in machineTables)
        {
            if (slot.MachineId != machineId || (keyItemId != 0 && slot.KeyItemId != keyItemId))
            {
                continue;
            }

            RollTable(slot.LootTableId, slot.LootTableScale, 0, new HashSet<uint>(), awards, random, tableLookup, itemRowLookup, subTableRowLookup);
        }

        return awards;
    }

    private static void RollTable(
        uint tableId,
        float scale,
        int depth,
        HashSet<uint> visited,
        List<TokenRollAward> awards,
        Random random,
        Func<uint, LootTable> tableLookup,
        Func<uint, IReadOnlyList<LootTableItemDist>> itemRowLookup,
        Func<uint, IReadOnlyList<LootTableSubTableDist>> subTableRowLookup)
    {
        // A dangling id, a table already on this path, or a table nested too deep: nothing to award.
        if (tableId == 0 || depth >= MaxTableDepth || !visited.Add(tableId) || tableLookup(tableId) == null)
        {
            return;
        }

        IReadOnlyList<LootTableItemDist> itemRows = itemRowLookup(tableId) ?? [];
        IReadOnlyList<LootTableSubTableDist> subTableRows = subTableRowLookup(tableId) ?? [];

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
                awards.Add(new TokenRollAward(row.ItemdropId, RollQuantity(row.MinQuantity, row.MaxQuantity, row.AllowScaling, scale, random), tableId));
                return;
            }

            pick -= row.Probability;
        }

        foreach (var row in subTableRows)
        {
            if (pick < row.Probability)
            {
                RollTable(row.SubtableId, scale, depth + 1, visited, awards, random, tableLookup, itemRowLookup, subTableRowLookup);
                return;
            }

            pick -= row.Probability;
        }
    }

    private static uint RollQuantity(ushort minQuantity, ushort maxQuantity, byte? allowScaling, float scale, Random random)
    {
        uint low = minQuantity;
        uint high = maxQuantity < low ? low : maxQuantity;
        uint count = low == high ? low : low + (uint)random.Next((int)(high - low + 1));

        // Unlocks, cosmetics, boosts and memberships are authored 0..0 and still award the item.
        if (count == 0)
        {
            count = 1;
        }

        if (allowScaling == 1 && scale > 0f && Math.Abs(scale - 1f) > float.Epsilon)
        {
            count = (uint)Math.Max(1, Math.Round(count * (double)scale));
        }

        return count;
    }
}
