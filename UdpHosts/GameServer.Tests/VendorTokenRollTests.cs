using System;
using System.Collections.Generic;
using System.Linq;
using GameServer.StaticDB.Records.dbitems;
using GameServer.Systems.Vendor;
using Xunit;

namespace GameServer.Tests;

/// <summary>
///     The token vending machine's real prize roll: the tables
///     <c>dbitems::VendorTokenLootTables</c> authors for a (machine, key item) pair, rolled through
///     <c>dbitems::LootTable</c>, <c>dbitems::LootTableItemDist</c> and
///     <c>dbitems::LootTableSubTableDist</c>. The fixtures here are the rows build prod-1962 carries
///     for the Accord Reward Quartermaster (machine 5) and its 1.3x sibling (machine 105), so the
///     tests read against the real prize pool: slot 1 crystite, slot 2 the rare-item check.
/// </summary>
public class VendorTokenRollTests
{
    /// <summary>The token machines 5 and 105 take: <c>dbitems::VendorTokenKeyItems</c> key item 85771.</summary>
    private const uint AccordToken = 85771;

    private const uint Slot1CrystiteTable = 5853;
    private const uint Slot2RareItemTable = 5857;

    /// <summary>
    ///     The machine rows of prod-1962: two slots each for machines 5 and 105, the second machine
    ///     scaling slot 1's crystite by 1.3.
    /// </summary>
    private static readonly VendorTokenLootTables[] Machines =
    [
        new VendorTokenLootTables { MachineId = 5, KeyItemId = AccordToken, LootTableId = Slot1CrystiteTable, LootTableScale = 1.0f },
        new VendorTokenLootTables { MachineId = 5, KeyItemId = AccordToken, LootTableId = Slot2RareItemTable, LootTableScale = 1.0f },
        new VendorTokenLootTables { MachineId = 105, KeyItemId = AccordToken, LootTableId = Slot1CrystiteTable, LootTableScale = 1.3f },
        new VendorTokenLootTables { MachineId = 105, KeyItemId = AccordToken, LootTableId = Slot2RareItemTable, LootTableScale = 1.0f },
    ];

    /// <summary>
    ///     Slot 1, "Accord Reward Quartermaster Vending - Slot 1 (Crystite)": five crystite rows whose
    ///     weights sum to exactly 1000, the last of them the 2000..3000 jackpot at 2 in 1000.
    /// </summary>
    private static readonly LootTableItemDist[] Slot1Items =
    [
        new LootTableItemDist { LootTableId = (ushort)Slot1CrystiteTable, ItemdropId = 10, MinQuantity = 20, MaxQuantity = 40, Probability = 598, AllowScaling = 1 },
        new LootTableItemDist { LootTableId = (ushort)Slot1CrystiteTable, ItemdropId = 10, MinQuantity = 41, MaxQuantity = 80, Probability = 250, AllowScaling = 1 },
        new LootTableItemDist { LootTableId = (ushort)Slot1CrystiteTable, ItemdropId = 10, MinQuantity = 81, MaxQuantity = 120, Probability = 100, AllowScaling = 1 },
        new LootTableItemDist { LootTableId = (ushort)Slot1CrystiteTable, ItemdropId = 10, MinQuantity = 121, MaxQuantity = 160, Probability = 50, AllowScaling = 1 },
        new LootTableItemDist { LootTableId = (ushort)Slot1CrystiteTable, ItemdropId = 10, MinQuantity = 2000, MaxQuantity = 3000, Probability = 2, AllowScaling = 1 },
    ];

    /// <summary>Slot 2's branches, in database order; their weights sum to 850.</summary>
    private static readonly LootTableSubTableDist[] Slot2Branches =
    [
        new LootTableSubTableDist { LootTableId = (ushort)Slot2RareItemTable, SubtableId = 5856, Probability = 448 },  // resources and consumables
        new LootTableSubTableDist { LootTableId = (ushort)Slot2RareItemTable, SubtableId = 5855, Probability = 150 },  // boosts
        new LootTableSubTableDist { LootTableId = (ushort)Slot2RareItemTable, SubtableId = 5854, Probability = 200 },  // aesthetics
        new LootTableSubTableDist { LootTableId = (ushort)Slot2RareItemTable, SubtableId = 5858, Probability = 2 },    // legendary
        new LootTableSubTableDist { LootTableId = (ushort)Slot2RareItemTable, SubtableId = 5983, Probability = 50 },   // VIP membership
    ];

    /// <summary>The consumables branch, whose six rows sum to 9 - not to a thousand.</summary>
    private static readonly LootTableItemDist[] ConsumableItems =
    [
        new LootTableItemDist { LootTableId = 5856, ItemdropId = 82644, MinQuantity = 0, MaxQuantity = 0, Probability = 4 },  // Resource Crate
        new LootTableItemDist { LootTableId = 5856, ItemdropId = 77261, MinQuantity = 3, MaxQuantity = 5, Probability = 1 },  // Horn of Plenty
        new LootTableItemDist { LootTableId = 5856, ItemdropId = 30287, MinQuantity = 3, MaxQuantity = 5, Probability = 1 },  // Health Pack, Small
        new LootTableItemDist { LootTableId = 5856, ItemdropId = 75096, MinQuantity = 2, MaxQuantity = 4, Probability = 1 },  // Health Pack, Medium
        new LootTableItemDist { LootTableId = 5856, ItemdropId = 85193, MinQuantity = 2, MaxQuantity = 2, Probability = 1 },  // Health Pack, Large
        new LootTableItemDist { LootTableId = 5856, ItemdropId = 30298, MinQuantity = 3, MaxQuantity = 5, Probability = 1 },  // Ammo Pack
    ];

    /// <summary>The VIP branch, whose weights do sum to a thousand.</summary>
    private static readonly LootTableItemDist[] VipItems =
    [
        new LootTableItemDist { LootTableId = 5983, ItemdropId = 86364, Probability = 644 },  // VIP Membership: 1 hour
        new LootTableItemDist { LootTableId = 5983, ItemdropId = 86365, Probability = 300 },  // VIP Membership: 8 hours
        new LootTableItemDist { LootTableId = 5983, ItemdropId = 86366, Probability = 50 },   // VIP Membership: 1 day
        new LootTableItemDist { LootTableId = 5983, ItemdropId = 86367, Probability = 5 },    // VIP Membership: 7 days
        new LootTableItemDist { LootTableId = 5983, ItemdropId = 86368, Probability = 1 },    // VIP Membership: 30 days
    ];

    /// <summary>
    ///     The boosts branch, in database order: the two 20% boosts are the rare rows, 3 of its 48
    ///     against the two 10% ones at 21. These are the goods PIN's quartermaster shelf used to sell.
    /// </summary>
    private static readonly LootTableItemDist[] BoostItems =
    [
        new LootTableItemDist { LootTableId = 5855, ItemdropId = 77066, Probability = 3 },   // XP Boost - 20% for 1 hour
        new LootTableItemDist { LootTableId = 5855, ItemdropId = 81361, Probability = 3 },   // Reputation Boost - 20% for 1 hour
        new LootTableItemDist { LootTableId = 5855, ItemdropId = 85781, Probability = 21 },  // XP Boost - 10% for 1 hour
        new LootTableItemDist { LootTableId = 5855, ItemdropId = 85782, Probability = 21 },  // Reputation Boost - 10% for 1 hour
    ];

    private static readonly LootTable[] Tables =
    [
        new LootTable { Id = Slot1CrystiteTable, Name = "Accord Reward Quartermaster Vending - Slot 1 (Crystite)" },
        new LootTable { Id = Slot2RareItemTable, Name = "Accord T1 Reward Quartermaster Vending - Slot 2 - \"Rare Item\" % check" },
        new LootTable { Id = 5856, Name = "Accord Reward Quartermaster - Slot 2 Resources and Consumables (uncommon) subtable" },
        new LootTable { Id = 5855, Name = "Accord Reward Quartermaster - Slot 2 BOOSTS (rare) subtable" },
        new LootTable { Id = 5983, Name = "Accord Reward Quartermaster - Slot 2 VIP Membership subtable" },
    ];

    /// <summary>
    ///     One token pays out once per slot the machine has: crystite from slot 1, and whatever the
    ///     rare-item check lands on from slot 2.
    /// </summary>
    [Fact]
    public void Roll_PaysOutOneAwardPerSlotTheMachineHas()
    {
        var awards = Roll(5, new ScriptedRandom(0));

        Assert.Equal(2, awards.Count);
        Assert.Equal(10u, awards[0].SdbId);                 // crystite
        Assert.Equal(Slot1CrystiteTable, awards[0].LootTableId);
        Assert.Equal(20u, awards[0].Quantity);              // the low end of the 20..40 band
        Assert.Equal(82644u, awards[1].SdbId);              // Resource Crate, the consumables branch
        Assert.Equal(5856u, awards[1].LootTableId);
        Assert.Equal(1u, awards[1].Quantity);               // authored 0..0, and still one item
    }

    /// <summary>
    ///     <c>loot_table_scale</c> is what tells machine 105 from machine 5: the same tables, 1.3 times
    ///     the crystite. Only rows authored <c>allow_scaling</c> are scaled - in the vendor tables that
    ///     is every crystite row and no item row, so a scale changes the payout and never the prizes.
    /// </summary>
    [Fact]
    public void Roll_ScalesOnlyTheRowsTheDatabaseScales()
    {
        var plain = Roll(5, new ScriptedRandom(0));
        var scaled = Roll(105, new ScriptedRandom(0));

        Assert.Equal(20u, plain[0].Quantity);
        Assert.Equal(26u, scaled[0].Quantity);   // 20 x 1.3, rounded
        Assert.Equal(plain[1].Quantity, scaled[1].Quantity);
        Assert.Equal(plain[1].SdbId, scaled[1].SdbId);
    }

    /// <summary>The quantity is uniform across the band the row authors, ends included.</summary>
    [Fact]
    public void Roll_QuantitySpansTheAuthoredBand()
    {
        // First the table pick, then the quantity inside the 20..40 band.
        Assert.Equal(20u, Roll(5, new ScriptedRandom(0, 0))[0].Quantity);
        Assert.Equal(40u, Roll(5, new ScriptedRandom(0, 20))[0].Quantity);
        Assert.Equal(30u, Roll(5, new ScriptedRandom(0, 10))[0].Quantity);
    }

    /// <summary>
    ///     A roll follows nested sub-tables to the item they land on, and reports the leaf table it
    ///     came out of: slot 2 -&gt; the aesthetics check -&gt; the uncommon unlocks.
    /// </summary>
    [Fact]
    public void Roll_FollowsNestedSubTablesToTheItem()
    {
        var tables = new List<LootTable>(Tables)
        {
            new LootTable { Id = 5854, Name = "Accord Merit Quartermaster - Slot 2 Aesthetics % Check subtable" },
            new LootTable { Id = 5985, Name = "Accord Reward Quartermaster - Uncommon Aesthetics subtable" },
        };

        var subTables = new List<LootTableSubTableDist>(Slot2Branches)
        {
            new LootTableSubTableDist { LootTableId = 5854, SubtableId = 5984, Probability = 900 },
            new LootTableSubTableDist { LootTableId = 5854, SubtableId = 5985, Probability = 100 },
        };

        // Two of the 32 unlocks table 5985 authors, each at probability 1 - enough to show a roll
        // reaching a leaf two sub-tables down and reporting the table it came out of.
        var items = new List<LootTableItemDist>(Slot1Items)
        {
            new LootTableItemDist { LootTableId = 5985, ItemdropId = 85410, Probability = 1 },  // New You Unlock: Purple Sunglasses
            new LootTableItemDist { LootTableId = 5985, ItemdropId = 86005, Probability = 1 },  // New You Unlock: Fedora
        };

        // Slot 1 first (pick 0, quantity 0), then slot 2's third branch (aesthetics, at 648 of 850),
        // then its second sub-table (uncommon, at 900 of 1000), then its second unlock.
        var awards = Roll(5, new ScriptedRandom(0, 0, 648, 900, 1), tables: tables, items: items, subTables: subTables);

        Assert.Equal(2, awards.Count);
        Assert.Equal(86005u, awards[1].SdbId);
        Assert.Equal(5985u, awards[1].LootTableId);
        Assert.Equal(1u, awards[1].Quantity);
    }

    /// <summary>
    ///     A table is rolled against the sum of its own rows, not against a thousand. Slot 2's five
    ///     branches sum to 850 and the consumables branch to 9; reading either against a fixed 1000
    ///     would mean a branch that was already won then hands out nothing 15% and 99% of the time.
    ///     The last row of each has to be reachable at the top of its own range.
    /// </summary>
    [Fact]
    public void Roll_WeightsAreTheTableOwnSum()
    {
        // 849 of 850: the last branch of slot 2, the VIP memberships, and its second row.
        var awards = Roll(5, new ScriptedRandom(849));
        Assert.Equal(5983u, awards[1].LootTableId);
        Assert.Equal(86365u, awards[1].SdbId);

        // 8 of the consumables branch's 9: its last row, the ammo pack, at the top of its 3..5 band.
        var consumables = Roll(5, new ScriptedRandom(0, 0, 447, 8, 2));
        Assert.Equal(30298u, consumables[1].SdbId);
        Assert.Equal(5u, consumables[1].Quantity);
    }

    /// <summary>The jackpot row is rare but reachable: 2 of slot 1's 1000.</summary>
    [Fact]
    public void Roll_TheJackpotRowIsReachable()
    {
        var awards = Roll(5, new ScriptedRandom(999, 1000));

        Assert.Equal(10u, awards[0].SdbId);
        Assert.Equal(3000u, awards[0].Quantity);   // the top of the 2000..3000 band
    }

    [Fact]
    public void Roll_AKeyItemTheMachineDoesNotTakeAwardsNothing()
    {
        Assert.Empty(Roll(5, new ScriptedRandom(0), keyItemId: 92784));
        Assert.Empty(Roll(999, new ScriptedRandom(0)));
        Assert.Empty(Roll(5, new ScriptedRandom(0), machines: []));
    }

    /// <summary>Zero means "whichever token this machine takes", so every slot still rolls.</summary>
    [Fact]
    public void Roll_AnyKeyItemRollsEverySlot()
    {
        Assert.Equal(2, Roll(5, new ScriptedRandom(0), keyItemId: 0).Count);
    }

    /// <summary>The same seed rolls the same prizes: a payout can be reproduced from a log line.</summary>
    [Fact]
    public void Roll_IsReproducibleFromASeed()
    {
        var first = Roll(5, new Random(20150502));
        var second = Roll(5, new Random(20150502));

        Assert.Equal(first, second);
        Assert.NotEmpty(first);
    }

    /// <summary>
    ///     A machine's tables are only three deep in prod-1962, but a table that nests itself must
    ///     still terminate: the roll gives up on a table it has already visited on this path.
    /// </summary>
    [Fact]
    public void Roll_ATableThatCyclesTerminates()
    {
        LootTable[] tables =
        [
            new LootTable { Id = 1, Name = "cycle" },
            new LootTable { Id = 2, Name = "cycle back" },
        ];
        LootTableSubTableDist[] subTables =
        [
            new LootTableSubTableDist { LootTableId = 1, SubtableId = 2, Probability = 1 },
            new LootTableSubTableDist { LootTableId = 2, SubtableId = 1, Probability = 1 },
        ];
        var machines = new[] { new VendorTokenLootTables { MachineId = 7, KeyItemId = 1, LootTableId = 1, LootTableScale = 1f } };

        Assert.Empty(Roll(7, new ScriptedRandom(0), keyItemId: 1, machines: machines, tables: tables, items: [], subTables: subTables));
    }

    /// <summary>A dangling loot table id awards nothing rather than throwing.</summary>
    [Fact]
    public void Roll_AMissingTableAwardsNothing()
    {
        var machines = new[] { new VendorTokenLootTables { MachineId = 7, KeyItemId = 1, LootTableId = 4242, LootTableScale = 1f } };

        Assert.Empty(Roll(7, new ScriptedRandom(0), keyItemId: 1, machines: machines, tables: Tables, items: Slot1Items, subTables: Slot2Branches));
    }

    /// <summary>Every award is a row the tables actually author.</summary>
    [Fact]
    public void Roll_OnlyEverAwardsAuthoredItems()
    {
        var authored = Slot1Items.Concat(ConsumableItems).Concat(VipItems).Concat(BoostItems)
            .Select(row => row.ItemdropId).Distinct().ToHashSet();

        var random = new Random(7);
        for (var i = 0; i < 500; i++)
        {
            foreach (var award in Roll(5, random))
            {
                Assert.Contains(award.SdbId, authored);
                Assert.True(award.Quantity >= 1, $"award {award.SdbId} came out with quantity {award.Quantity}");
            }
        }
    }

    private static IReadOnlyList<TokenRollAward> Roll(
        uint machineId,
        Random random,
        uint keyItemId = AccordToken,
        IReadOnlyList<VendorTokenLootTables> machines = null,
        IReadOnlyList<LootTable> tables = null,
        IReadOnlyList<LootTableItemDist> items = null,
        IReadOnlyList<LootTableSubTableDist> subTables = null)
    {
        machines ??= Machines;
        tables ??= Tables;
        items ??= [.. Slot1Items, .. ConsumableItems, .. VipItems, .. BoostItems];
        subTables ??= Slot2Branches;

        var tableById = tables.ToDictionary(table => table.Id);
        var itemsByTable = items.GroupBy(row => (uint)row.LootTableId).ToDictionary(group => group.Key, group => (IReadOnlyList<LootTableItemDist>)group.ToList());
        var subsByTable = subTables.GroupBy(row => (uint)row.LootTableId).ToDictionary(group => group.Key, group => (IReadOnlyList<LootTableSubTableDist>)group.ToList());

        // A table with no rows of a kind hands back null, which the roll reads as "none".
        return VendorTokenRoll.Roll(
            machineId,
            keyItemId,
            random,
            machines,
            id => tableById.GetValueOrDefault(id),
            id => itemsByTable.GetValueOrDefault(id),
            id => subsByTable.GetValueOrDefault(id));
    }

    /// <summary>
    ///     Randomness on a script: every <see cref="Random.Next(int)" /> the roll makes takes the next
    ///     value given, clamped into range, and repeats the last one once the script runs out.
    /// </summary>
    private sealed class ScriptedRandom : Random
    {
        private readonly int[] _values;
        private int _at;

        public ScriptedRandom(params int[] values)
        {
            _values = values.Length == 0 ? new[] { 0 } : values;
        }

        public override int Next(int maxValue)
        {
            if (maxValue <= 0)
            {
                return 0;
            }

            var value = _values[Math.Min(_at, _values.Length - 1)];
            _at++;
            return value >= maxValue ? maxValue - 1 : value;
        }
    }
}
