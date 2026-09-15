using System.Collections.Generic;
using System.Linq;
using GameServer.StaticDB.Records.dbcharacter;
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

        // A machine's rows are the prizes its cabinet advertises; buying one spends the token and
        // rolls the machine's loot tables instead of handing the advertised prize over.
        Assert.All(stock, entry => Assert.True(entry.TokenMachineRoll));
        Assert.All(stock, entry => Assert.Equal(VendorDataProvenance.DatabaseDerived, entry.Provenance));
    }

    /// <summary>
    ///     The capture is the only vendor data that exists, so the copy of it in the source is held
    ///     against the recording: 32 distinct rows, the six price points live used, one crystite price
    ///     per row, gates at only the two rungs the window named, and the ladder its header carried.
    /// </summary>
    [Fact]
    public void CapturedStore_MatchesTheRecording()
    {
        var store = LiveVendorData.CopacabanaAresSupplies;

        Assert.Equal(60u, store.VendorId);
        Assert.Equal(2321u, store.StoreId);
        Assert.Equal("Copacabana ARES Supplies", store.Title);
        Assert.Equal(20u, store.FactionId);
        Assert.Equal(32, store.Products.Count);
        Assert.Equal(32, store.Products.Select(product => product.SdbId).Distinct().Count());
        Assert.All(store.Products, product => Assert.Contains(product.Cost, new uint[] { 50, 100, 150, 250, 500, 2500 }));
        Assert.All(store.Products, product => Assert.Equal(1u, product.Quantity));
        Assert.All(store.Products, product => Assert.Contains(product.MinReputation, new uint[] { 0, 6000, 12000 }));
        Assert.Equal(10, store.Products.Count(product => product.MinReputation == LiveVendorData.NoReputationGate));
        Assert.Equal(21, store.Products.Count(product => product.MinReputation == 6000));
        Assert.Equal(3, store.Discounts.Count);

        Assert.Same(store, LiveVendorData.FindStore(60));
        Assert.Null(LiveVendorData.FindStore(61));
    }

    /// <summary>
    ///     The one vendor a live server was recorded serving, replayed row for row: stock, prices,
    ///     reputation gates and window order, from the 3,006 bytes of its <c>VendorProductsResponse</c>.
    /// </summary>
    [Fact]
    public void Build_CapturedVendor_ReplaysTheRecordedCatalog()
    {
        RootItem lookup(uint id) => new() { SdbId = id, Type = 7 };
        var captured = LiveVendorData.CopacabanaAresSupplies;

        var stock = VendorCatalog.Build(60, lookup, _ => null, [], []);

        Assert.Equal(captured.Products.Count, stock.Count);
        for (var i = 0; i < stock.Count; i++)
        {
            Assert.Equal(captured.Products[i].SdbId, stock[i].SdbId);
            Assert.Equal(captured.Products[i].Cost, stock[i].Cost);
            Assert.Equal(captured.Products[i].MinReputation, stock[i].MinReputation);
            Assert.Equal(captured.Products[i].Quantity, stock[i].Quantity);
            Assert.Equal(i, stock[i].Index);
            Assert.Equal(VendorCatalog.CrystiteSdbId, stock[i].CurrencySdbId);
            Assert.Equal(VendorDataProvenance.Captured, stock[i].Provenance);
        }

        // Rows the recording pins down by name.
        Assert.Equal(50u, CostOf(stock, 56811));    // Scan Hammer
        Assert.Equal(100u, CostOf(stock, 30287));   // Health Pack, Small
        Assert.Equal(500u, CostOf(stock, 75096));   // Health Pack, Medium
        Assert.Equal(2500u, CostOf(stock, 85193));  // Health Pack, Large
        Assert.Equal(150u, CostOf(stock, 82577));   // Grenade, Flash Freeze
        Assert.Equal(250u, CostOf(stock, 82597));   // Adrenaline Injector
        Assert.Equal(6000u, GateOf(stock, 87764));
        Assert.Equal(6000u, GateOf(stock, 87705));  // Broken LMG
        Assert.Equal(12000u, GateOf(stock, 32755)); // 1-Use Glider Pad Calldown
    }

    /// <summary>
    ///     A captured row the loaded database does not know is skipped rather than listed nameless -
    ///     the same rule the emulated shelf has always played by.
    /// </summary>
    [Fact]
    public void Build_CapturedVendor_SkipsRowsTheDatabaseDoesNotKnow()
    {
        RootItem lookup(uint id) => id is 30287 or 87705 ? new RootItem { SdbId = id, Type = 7 } : null;

        var stock = VendorCatalog.Build(60, lookup, _ => null, [], []);

        Assert.Equal(2, stock.Count);
        Assert.Equal(30287u, stock[0].SdbId);
        Assert.Equal(87705u, stock[1].SdbId);

        // Window positions are renumbered densely, so the guids stay contiguous.
        Assert.Equal(0, stock[0].Index);
        Assert.Equal(1, stock[1].Index);
    }

    /// <summary>
    ///     A quartermaster no capture recorded still sells at the captured prices: the field supplies
    ///     of the Copacabana window are its shelf, and a real price outranks any number PIN might
    ///     otherwise have chosen. These are the seven PIN had wrong before the capture was decoded.
    /// </summary>
    [Fact]
    public void Build_Quartermaster_PricesWhatTheCapturePrices()
    {
        RootItem lookup(uint id) => new() { SdbId = id, Type = 7 };

        // Vendor 100 is Corporal Belle (monsters 2269 and 2876) in build prod-1962.
        var stock = VendorCatalog.Build(100, lookup, _ => null, [], []);

        Assert.Equal(500u, CostOf(stock, 75096));   // Health Pack, Medium - was 250
        Assert.Equal(2500u, CostOf(stock, 85193));  // Health Pack, Large - was 500
        Assert.Equal(150u, CostOf(stock, 82577));   // Grenade, Flash Freeze - was 300
        Assert.Equal(150u, CostOf(stock, 82595));   // Grenade, Incendiary - was 300
        Assert.Equal(150u, CostOf(stock, 82596));   // Grenade, Toxic - was 300
        Assert.Equal(150u, CostOf(stock, 116563));  // Grenade, Concussion - was 350
        Assert.Equal(150u, CostOf(stock, 32755));   // 1-Use Glider Pad Calldown - was 1000
        Assert.Equal(50u, CostOf(stock, 56811));    // Scan Hammer - was not stocked at all
        Assert.Equal(250u, CostOf(stock, 82597));   // Adrenaline Injector - was not stocked at all
        Assert.Equal(100u, CostOf(stock, 30287));   // already right
        Assert.Equal(100u, CostOf(stock, 30298));   // already right

        // Every row a capture prices is marked as captured; the rest say they are PIN's own.
        Assert.All(
            stock.Where(entry => LiveVendorData.TryGetCapturedPrice(entry.SdbId, out _, out _)),
            entry => Assert.Equal(VendorDataProvenance.Captured, entry.Provenance));
        Assert.Contains(stock, entry => entry.Provenance == VendorDataProvenance.Emulated);
        Assert.All(stock, entry => Assert.Equal(VendorCatalog.CrystiteSdbId, entry.CurrencySdbId));
    }

    /// <summary>
    ///     The boosts PIN's shelf used to carry are gone from it: on live a quartermaster never sold
    ///     them. They were vending-machine prizes - loot table 5855 rolls them - and cash-shop goods.
    /// </summary>
    [Fact]
    public void Build_Quartermaster_LeavesTheMachinePrizesToTheMachines()
    {
        RootItem lookup(uint id) => new() { SdbId = id, Type = 7 };

        var stock = VendorCatalog.Build(100, lookup, _ => null, [], []);
        var ids = stock.Select(entry => entry.SdbId).ToList();

        Assert.DoesNotContain(77066u, ids);  // XP Boost - 20% for 1 hour
        Assert.DoesNotContain(81362u, ids);  // Crystite Boost - 20% for 1 hour
    }

    /// <summary>
    ///     The captured store's 21 gated rows are Copacabana's own issue - class-specific tier-8 gear
    ///     whose localized names are placeholders outside it - so they stay with the vendor they were
    ///     recorded on instead of following every quartermaster in the game.
    /// </summary>
    [Fact]
    public void Build_Quartermaster_KeepsTheCopacabanaGearRowsToTheirOwnStore()
    {
        RootItem lookup(uint id) => new() { SdbId = id, Type = 7 };

        var ids = VendorCatalog.Build(100, lookup, _ => null, [], []).Select(entry => entry.SdbId).ToList();

        Assert.DoesNotContain(87705u, ids);  // Broken LMG
        Assert.DoesNotContain(86765u, ids);
        Assert.Contains(32755u, ids);        // the calldown is a field supply, and stays
    }

    [Fact]
    public void StoreId_CapturedStore_ReplaysTheRecordedId()
    {
        Assert.Equal(2321u, VendorCatalog.StoreId(LiveVendorData.SupplyOfficerCrossVendorId));
        Assert.Equal(LiveVendorData.CopacabanaStoreId, VendorCatalog.StoreId(60));
    }

    /// <summary>
    ///     A token machine's window is the web store its <c>web_vendor_id</c> names - 30 for machines
    ///     3 and 4 in prod-1962, 2 for machine 2 - and a machine that carries none (5, 105, 106 and
    ///     107, which are the ones an NPC actually stands at) falls back to its own vendor id, because
    ///     a zero store id is a window the client cannot echo back.
    /// </summary>
    [Fact]
    public void StoreId_TokenMachine_UsesItsWebVendorId()
    {
        // The web_vendor_id column of prod-1962's seven dbitems::VendorTokenMachine rows.
        VendorTokenMachine machine(uint id) => id switch
        {
            2 => new VendorTokenMachine { Id = 2, WebVendorId = 2 },
            3 => new VendorTokenMachine { Id = 3, WebVendorId = 30 },
            4 => new VendorTokenMachine { Id = 4, WebVendorId = 30 },
            _ => new VendorTokenMachine { Id = id, WebVendorId = 0 },
        };

        Assert.Equal(2u, VendorCatalog.StoreId(2, machine));
        Assert.Equal(30u, VendorCatalog.StoreId(3, machine));
        Assert.Equal(30u, VendorCatalog.StoreId(4, machine));
        Assert.Equal(5u, VendorCatalog.StoreId(5, machine));
        Assert.Equal(310u, VendorCatalog.StoreId(310, _ => null));
    }

    /// <summary>
    ///     The discount ladder live sent is the database's own: <c>dbcharacter::FactionReputations</c>
    ///     gives faction 20 rungs at -16000, -8000, 0, 6000, 12000 and 24000, and the captured window
    ///     discounted at exactly the three positive ones, 10%/20%/30%.
    /// </summary>
    [Fact]
    public void DiscountTiersFor_AreThePositiveReputationRungsAtTheCapturedFractions()
    {
        List<FactionReputations> rungs(uint factionId) => factionId == 20
            ? new[] { -16000, -8000, 0, 6000, 12000, 24000 }.Select(rep => new FactionReputations { FactionId = 20, MinReputation = rep }).ToList()
            : null;

        var tiers = VendorCatalog.DiscountTiersFor(20, rungs);

        Assert.Equal(3, tiers.Count);
        Assert.Equal(6000u, tiers[0].MinReputation);
        Assert.Equal(0.1f, tiers[0].Discount);
        Assert.Equal(12000u, tiers[1].MinReputation);
        Assert.Equal(0.2f, tiers[1].Discount);
        Assert.Equal(24000u, tiers[2].MinReputation);
        Assert.Equal(0.3f, tiers[2].Discount);

        // Row for row the ladder the captured response carried.
        Assert.Equal(LiveVendorData.CopacabanaAresSupplies.Discounts, tiers);
    }

    /// <summary>
    ///     Faction 8 (Bandits) is the one faction in prod-1962 with a single positive rung; it gets
    ///     the first fraction of the ladder rather than none.
    /// </summary>
    [Fact]
    public void DiscountTiersFor_AFactionWithOneRungGetsTheFirstFraction()
    {
        var tiers = VendorCatalog.DiscountTiersFor(8, _ => [new FactionReputations { FactionId = 8, MinReputation = 6000 }, new FactionReputations { FactionId = 8, MinReputation = -16000 }]);

        Assert.Single(tiers);
        Assert.Equal(6000u, tiers[0].MinReputation);
        Assert.Equal(0.1f, tiers[0].Discount);
    }

    [Fact]
    public void DiscountTiersFor_NoRungsOrNoDatabaseMeansNoLadder()
    {
        Assert.Empty(VendorCatalog.DiscountTiersFor(0, _ => [new FactionReputations { MinReputation = 6000 }]));
        Assert.Empty(VendorCatalog.DiscountTiersFor(20, null));
        Assert.Empty(VendorCatalog.DiscountTiersFor(20, _ => null));
        Assert.Empty(VendorCatalog.DiscountTiersFor(20, _ => [new FactionReputations { MinReputation = -16000 }, new FactionReputations { MinReputation = 0 }]));
    }

    /// <summary>
    ///     A captured window is described by the store, not by the NPC standing at it: live titled
    ///     terminal 60 after its POI and discounted it against the POI faction, while the NPC himself
    ///     (monster 956, Supply Officer Cross) is faction 1, the Accord.
    /// </summary>
    [Fact]
    public void Describe_CapturedStore_IsTheStoreNotTheNpc()
    {
        var decor = VendorCatalog.Describe(60, "Supply Officer Cross", 1, _ => null, _ => null);

        Assert.Equal(2321u, decor.StoreId);
        Assert.Equal("Copacabana ARES Supplies", decor.Title);
        Assert.Equal(20u, decor.FactionId);
        Assert.Equal(LiveVendorData.CopacabanaAresSupplies.Discounts, decor.Discounts);
    }

    [Fact]
    public void Describe_AnyOtherVendor_IsTheNpcAndItsFactionRungs()
    {
        List<FactionReputations> rungs(uint factionId) => factionId == 1
            ? new[] { 6000, 12000, 24000 }.Select(rep => new FactionReputations { FactionId = 1, MinReputation = rep }).ToList()
            : null;

        var decor = VendorCatalog.Describe(100, "Corporal Belle", 1, _ => null, rungs);

        Assert.Equal(100u, decor.StoreId);
        Assert.Equal("Corporal Belle", decor.Title);
        Assert.Equal(1u, decor.FactionId);
        Assert.Equal(3, decor.Discounts.Count);
        Assert.Equal(6000u, decor.Discounts[0].MinReputation);
        Assert.Equal(0.1f, decor.Discounts[0].Discount);
    }

    private static uint CostOf(IReadOnlyList<VendorCatalogEntry> stock, uint sdbId)
    {
        return stock.Single(entry => entry.SdbId == sdbId).Cost;
    }

    private static uint GateOf(IReadOnlyList<VendorCatalogEntry> stock, uint sdbId)
    {
        return stock.Single(entry => entry.SdbId == sdbId).MinReputation;
    }
}
