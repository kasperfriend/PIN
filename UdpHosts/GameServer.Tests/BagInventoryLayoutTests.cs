using System.Text.Json;
using GameServer.Data;
using Xunit;

namespace GameServer.Tests;

/// <summary>
///     The bag model the client enforces: nine bags in the captured 5x5 layout while the content
///     fits, grown in whole grid rows past that, with the carried items and stacks mirrored into
///     the slots the update names.
/// </summary>
public class BagInventoryLayoutTests
{
    [Fact]
    public void BagLengthFor_SmallInventories_KeepsTheAuthenticGrid()
    {
        Assert.Equal(25, BagInventoryLayout.BagLengthFor(0));
        Assert.Equal(25, BagInventoryLayout.BagLengthFor(1));
        Assert.Equal(25, BagInventoryLayout.BagLengthFor(9 * 25));
    }

    [Fact]
    public void BagLengthFor_Overflow_GrowsInWholeRowsOfFive()
    {
        Assert.Equal(30, BagInventoryLayout.BagLengthFor(9 * 25 + 1));
        Assert.Equal(30, BagInventoryLayout.BagLengthFor(9 * 30));
        Assert.Equal(35, BagInventoryLayout.BagLengthFor(9 * 30 + 1));
        Assert.Equal(115, BagInventoryLayout.BagLengthFor(9 * 115));
        Assert.Equal(120, BagInventoryLayout.BagLengthFor(9 * 115 + 1));
    }

    [Fact]
    public void CapacityFor_ComesOutAsBagsTimesLength()
    {
        Assert.Equal(9 * 25, BagInventoryLayout.CapacityFor(25));

        // Grown or not, the layout always holds the slots it was sized for.
        for (var slots = 0; slots <= 1000; slots++)
        {
            Assert.True(
                BagInventoryLayout.CapacityFor(BagInventoryLayout.BagLengthFor(slots)) >= slots,
                $"capacity for {slots} slots");
        }
    }

    [Fact]
    public void BuildUpdateJson_MirrorsTheCarriedSlotsIntoTheNineBags()
    {
        var slots = new BagInventoryLayout.BagSlot[]
        {
            new(0xAABBCCDDEEFF00AAul, 143670, 1),
            new(0ul, 82604, 20),
        };

        using var doc = JsonDocument.Parse(BagInventoryLayout.BuildUpdateJson(slots));
        var root = doc.RootElement;

        Assert.Equal(2, root.GetProperty("version").GetInt32());

        var bagTypes = root.GetProperty("bag_types");
        Assert.Equal(2, bagTypes.GetArrayLength());

        var definitions = bagTypes[0].GetProperty("definitions");
        Assert.Equal(9, definitions.GetArrayLength());
        foreach (var definition in definitions.EnumerateArray())
        {
            Assert.Equal(25, definition.GetProperty("length").GetInt32());
        }

        var second = bagTypes[1];
        Assert.Empty(second.GetProperty("definitions").EnumerateArray());
        Assert.Empty(second.GetProperty("slots").EnumerateArray());

        var parsedSlots = bagTypes[0].GetProperty("slots");
        Assert.Equal(2, parsedSlots.GetArrayLength());

        Assert.Equal(0xAABBCCDDEEFF00AAul, parsedSlots[0].GetProperty("item_guid").GetUInt64());
        Assert.Equal(143670u, parsedSlots[0].GetProperty("item_sdb_id").GetUInt32());
        Assert.Equal(1u, parsedSlots[0].GetProperty("quantity").GetUInt32());

        Assert.Equal(0ul, parsedSlots[1].GetProperty("item_guid").GetUInt64());
        Assert.Equal(82604u, parsedSlots[1].GetProperty("item_sdb_id").GetUInt32());
        Assert.Equal(20u, parsedSlots[1].GetProperty("quantity").GetUInt32());
    }
}
