using GameServer.StaticDB.Records.dbitems;
using GameServer.Systems.Vendor;
using Xunit;

namespace GameServer.Tests;

public class VendorCatalogTests
{
    [Theory]
    [InlineData(1u, 0)]
    [InlineData(310u, 7)]
    [InlineData(10001u, 16)]
    [InlineData(65535u, 255)]
    public void Guids_RoundTripThroughDecode(uint vendorId, int index)
    {
        Assert.True(VendorCatalog.TryDecodeGuid(VendorCatalog.ProductGuid(vendorId, index), out uint decodedVendor, out int decodedIndex, out bool isPrice));
        Assert.Equal(vendorId, decodedVendor);
        Assert.Equal(index, decodedIndex);
        Assert.False(isPrice);

        Assert.True(VendorCatalog.TryDecodeGuid(VendorCatalog.PriceGuid(vendorId, index), out decodedVendor, out decodedIndex, out isPrice));
        Assert.Equal(vendorId, decodedVendor);
        Assert.Equal(index, decodedIndex);
        Assert.True(isPrice);
    }

    [Fact]
    public void TryDecodeGuid_RejectsForeignGuids()
    {
        Assert.False(VendorCatalog.TryDecodeGuid(0, out _, out _, out _));
        Assert.False(VendorCatalog.TryDecodeGuid(0x1234_0000_0000_0001ul, out _, out _, out _));

        // A real inventory guid (random 64-bit space) must never decode as a product.
        Assert.False(VendorCatalog.TryDecodeGuid(0xdeadbeef_cafebabeul, out _, out _, out _));
    }

    /// <summary>
    ///     A vendor window's ids have to survive the client's scripted UI, where every number is an
    ///     IEEE-754 double with a 53-bit mantissa, and come back unchanged in the purchase request.
    ///     Live minted plain database ids below 2^21; anything larger is why Buy used to do nothing.
    /// </summary>
    [Theory]
    [InlineData(1u, 0)]
    [InlineData(310u, 7)]
    [InlineData(10001u, 16)]
    [InlineData(65535u, 255)]
    public void Guids_StaySmallEnoughForTheClientUi(uint vendorId, int index)
    {
        var guids = new[] { VendorCatalog.ProductGuid(vendorId, index), VendorCatalog.PriceGuid(vendorId, index) };

        foreach (var guid in guids)
        {
            Assert.True(guid > 0, "a zero guid reads as \"nothing selected\"");
            Assert.True(guid <= int.MaxValue, $"guid {guid} does not fit the 32 bits a scripted UI holds exactly");
            Assert.Equal(guid, (ulong)(double)guid);
        }

        // A product and its price must not collide.
        Assert.NotEqual(guids[0], guids[1]);
    }

    [Fact]
    public void TryDecodeGuid_RejectsTheOversizedGuidsItUsedToMint()
    {
        // Vendor 100's first entry, before the ids were made small enough to round-trip.
        Assert.False(VendorCatalog.TryDecodeGuid(0x56454E44_00640000ul, out _, out _, out _));
        Assert.False(VendorCatalog.TryDecodeGuid(0x56454E44_80640000ul, out _, out _, out _));
    }

    /// <summary>
    ///     The window id (<c>VendorProductsResponse.Id</c>) the client truncates to 32 bits and echoes
    ///     back as the last field of a purchase. Live sent a small store-table id here, never the
    ///     vendor NPC's 64-bit entity id.
    /// </summary>
    [Fact]
    public void StoreId_IsSmallAndStablePerVendor()
    {
        Assert.Equal(310u, VendorCatalog.StoreId(310));
        Assert.Equal(VendorCatalog.StoreId(310), VendorCatalog.StoreId(310));
        Assert.NotEqual(VendorCatalog.StoreId(310), VendorCatalog.StoreId(60));
        Assert.NotEqual(0u, VendorCatalog.StoreId(310));
    }

    [Fact]
    public void Build_Quartermaster_UsesCrystitePricesAndSkipsUnknownItems()
    {
        // A database that only knows two of the curated rows: the rest of the shelf is skipped.
        RootItem lookup(uint id) => id switch
        {
            30287 => new RootItem { SdbId = 30287, Type = 7 },
            30298 => new RootItem { SdbId = 30298, Type = 7 },
            _ => null,
        };

        var stock = VendorCatalog.Build(310, lookup, _ => null, [], []);

        Assert.Equal(2, stock.Count);
        Assert.All(stock, entry => Assert.Equal(VendorCatalog.CrystiteSdbId, entry.CurrencySdbId));
        Assert.Equal(30287u, stock[0].SdbId);
        Assert.Equal(100u, stock[0].Cost);
        Assert.Equal(30298u, stock[1].SdbId);
        Assert.Equal(0, stock[0].Index);
        Assert.Equal(1, stock[1].Index);
    }

    [Fact]
    public void Build_TokenMachine_ListsItsDisplayItemsForOneKeyToken()
    {
        RootItem lookup(uint id) => new() { SdbId = id, Type = 0 };

        var machine = new VendorTokenMachine { Id = 5 };
        VendorTokenDisplayItems[] displayItems =
        [
            new VendorTokenDisplayItems { MachineId = 5, KeyItemId = 85771, ItemId = 10 },
            new VendorTokenDisplayItems { MachineId = 5, KeyItemId = 85771, ItemId = 80404 },
            new VendorTokenDisplayItems { MachineId = 105, KeyItemId = 85771, ItemId = 99999 },
        ];
        var keyItems = new[] { new VendorTokenKeyItems { MachineId = 5, KeyItemId = 85771 } };

        var stock = VendorCatalog.Build(5, lookup, id => id == 5 ? machine : null, displayItems, keyItems);

        Assert.Equal(2, stock.Count);
        Assert.Equal(10u, stock[0].SdbId);
        Assert.Equal(85771u, stock[0].CurrencySdbId);
        Assert.Equal(1u, stock[0].Cost);
        Assert.Equal(80404u, stock[1].SdbId);
    }
}
