using System;
using System.Collections.Generic;
using System.Linq;
using GameServer.StaticDB.Records.dbitems;
using GameServer.Systems.Salvage;
using Xunit;

namespace GameServer.Tests;

/// <summary>
///     The loot-table roll behind salvaging: mode 0 tables pick one weighted row (the token
///     machine tables), the salvage-tree modes roll every row independently at probability
///     percent - rows at 100 are the guaranteed materials the client's salvage preview promises,
///     and the chance remainder gives one more roll.
/// </summary>
public class SalvageRollerTests
{
    private const uint Parent = 6945;
    private const uint Part = 6229;

    [Fact]
    public void Roll_RollEveryRowMode_GuaranteedRowsAlwaysFire()
    {
        var table = new Dictionary<uint, LootTable> { [1] = new LootTable { Id = 1, RollMode = 3 } };
        var itemRows = new Dictionary<uint, IReadOnlyList<LootTableItemDist>>
        {
            [1] =
            [
                new LootTableItemDist { ItemdropId = 10, Probability = 100, MinQuantity = 1, MaxQuantity = 2 },
                new LootTableItemDist { ItemdropId = 86154, Probability = 100, MinQuantity = 2, MaxQuantity = 4 },
            ],
        };

        // No matter the roll of the dice: two rows, each at 100, both land.
        for (var seed = 0; seed < 20; seed++)
        {
            var awards = Roll(1, seed, table, itemRows, []);

            Assert.Equal(2, awards.Count);
            Assert.Equal(10u, awards[0].SdbId);
            Assert.InRange(awards[0].Quantity, 1u, 2u);
            Assert.Equal(86154u, awards[1].SdbId);
            Assert.InRange(awards[1].Quantity, 2u, 4u);
        }
    }

    [Fact]
    public void Roll_RollEveryRowMode_MinZeroRowsAwardOne()
    {
        var table = new Dictionary<uint, LootTable> { [1] = new LootTable { Id = 1, RollMode = 2 } };
        var itemRows = new Dictionary<uint, IReadOnlyList<LootTableItemDist>>
        {
            [1] = [new LootTableItemDist { ItemdropId = 86703, Probability = 100, MinQuantity = 0, MaxQuantity = 0 }],
        };

        var awards = Roll(1, 1, table, itemRows, []);

        Assert.Single(awards);
        Assert.Equal(1u, awards[0].Quantity);
    }

    [Fact]
    public void Roll_WeightedPickMode_PicksExactlyOneRow()
    {
        var table = new Dictionary<uint, LootTable> { [1] = new LootTable { Id = 1, RollMode = 0 } };
        var itemRows = new Dictionary<uint, IReadOnlyList<LootTableItemDist>>
        {
            [1] =
            [
                new LootTableItemDist { ItemdropId = 10, Probability = 700, MinQuantity = 5, MaxQuantity = 5 },
                new LootTableItemDist { ItemdropId = 86154, Probability = 300, MinQuantity = 9, MaxQuantity = 9 },
            ],
        };

        var counts = new Dictionary<uint, int>();
        for (var seed = 0; seed < 200; seed++)
        {
            var awards = Roll(1, seed, table, itemRows, []);

            Assert.Single(awards);
            Assert.Equal(awards[0].SdbId == 10 ? 5u : 9u, awards[0].Quantity);
            counts[awards[0].SdbId] = counts.GetValueOrDefault(awards[0].SdbId) + 1;
        }

        // 200 seeded rolls across a 70/30 split: both rows show up.
        Assert.True(counts.GetValueOrDefault(10u) > 0, "the 70% row never landed");
        Assert.True(counts.GetValueOrDefault(86154u) > 0, "the 30% row never landed");
    }

    /// <summary>
    ///     The Tier-1 weapon salvage table lists the same generic-parts sub-table six times at
    ///     probability 100; every one of those rows rolls - that is the six rolls of parts a
    ///     weapon breaks into - rather than collapsing into the first.
    /// </summary>
    [Fact]
    public void Roll_RepeatedSubTableRows_EachRowRolls()
    {
        var table = new Dictionary<uint, LootTable>
        {
            [Parent] = new LootTable { Id = Parent, RollMode = 4 },
            [Part] = new LootTable { Id = Part, RollMode = 3 },
        };
        var subTableRows = new Dictionary<uint, IReadOnlyList<LootTableSubTableDist>>
        {
            [Parent] =
            [
                new LootTableSubTableDist { SubtableId = (ushort)Part, Probability = 100 },
                new LootTableSubTableDist { SubtableId = (ushort)Part, Probability = 100 },
                new LootTableSubTableDist { SubtableId = (ushort)Part, Probability = 100 },
                new LootTableSubTableDist { SubtableId = (ushort)Part, Probability = 100 },
                new LootTableSubTableDist { SubtableId = (ushort)Part, Probability = 100 },
                new LootTableSubTableDist { SubtableId = (ushort)Part, Probability = 100 },
            ],
        };
        var itemRows = new Dictionary<uint, IReadOnlyList<LootTableItemDist>>
        {
            [Part] = [new LootTableItemDist { ItemdropId = 86154, Probability = 100, MinQuantity = 8, MaxQuantity = 10 }],
        };

        var awards = SalvageRoller.Roll(
            Parent,
            new Random(7),
            id => table.GetValueOrDefault(id),
            id => itemRows.GetValueOrDefault(id) ?? [],
            id => subTableRows.GetValueOrDefault(id) ?? []);

        Assert.Equal(6, awards.Count);
        Assert.All(awards, award => Assert.Equal(86154u, award.SdbId));
        Assert.InRange(awards.Sum(a => (int)a.Quantity), 48, 60);
    }

    /// <summary>A table pointing at itself must not hang the guard; it yields nothing instead.</summary>
    [Fact]
    public void Roll_CyclicTable_TerminatesWithNothing()
    {
        var table = new Dictionary<uint, LootTable> { [1] = new LootTable { Id = 1, RollMode = 3 } };
        var subTableRows = new Dictionary<uint, IReadOnlyList<LootTableSubTableDist>>
        {
            [1] = [new LootTableSubTableDist { SubtableId = 1, Probability = 100 }],
        };

        var awards = SalvageRoller.Roll(
            1,
            new Random(1),
            id => table.GetValueOrDefault(id),
            _ => [],
            id => subTableRows.GetValueOrDefault(id) ?? []);

        Assert.Empty(awards);
    }

    /// <summary>Dangling and zero table ids award nothing.</summary>
    [Fact]
    public void Roll_MissingTable_AwardsNothing()
    {
        var awards = SalvageRoller.Roll(0, new Random(1), _ => null, _ => [], _ => []);

        Assert.Empty(awards);
    }

    private static IReadOnlyList<SalvageAward> Roll(
        uint tableId,
        int seed,
        Dictionary<uint, LootTable> tables,
        Dictionary<uint, IReadOnlyList<LootTableItemDist>> itemRows,
        Dictionary<uint, IReadOnlyList<LootTableSubTableDist>> subTableRows)
    {
        return SalvageRoller.Roll(
            tableId,
            new Random(seed),
            id => tables.GetValueOrDefault(id),
            id => itemRows.GetValueOrDefault(id) ?? [],
            id => subTableRows.GetValueOrDefault(id) ?? []);
    }
}
