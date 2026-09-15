using System;
using System.Collections.Generic;
using System.Linq;
using AeroMessages.GSS.Character.Command;
using AeroMessages.GSS.Character.Controller;
using GameServer.Data;
using GameServer.Entities.Character;
using GameServer.Systems.Vendor;
using GameServer.Tests.Fakes;
using Xunit;

namespace GameServer.Tests;

/// <summary>
///     The NPC shop pipeline: the product window lists what the catalog builds (with the
///     deterministic guids the purchase must echo back), and a purchase charges the currency and
///     grants the goods - while declines answer with a code instead of leaving the window hanging.
/// </summary>
public class NpcVendorServiceTests : IDisposable
{
    private const uint VendorId = 310;
    private const uint Crystite = VendorCatalog.CrystiteSdbId;

    private static readonly IReadOnlyList<VendorCatalogEntry> Shelf =
    [
        new VendorCatalogEntry { Index = 0, SdbId = 30287, Quantity = 1, CurrencySdbId = Crystite, Cost = 100, Name = "Health Pack, Small" },
        new VendorCatalogEntry { Index = 1, SdbId = 30298, Quantity = 1, CurrencySdbId = Crystite, Cost = 250, Name = "Ammo Pack" },
    ];

    public NpcVendorServiceTests()
    {
        NpcVendorService.BuildCatalog = _ => Shelf;
    }

    public void Dispose()
    {
        NpcVendorService.BuildCatalog = VendorCatalog.Build;
    }

    [Fact]
    public void BuildProductsResponse_WithoutAuthorizedTerminal_IsIgnored()
    {
        var (_, player, _) = CreateVendorSession(authorizeTerminal: false);

        Assert.Null(NpcVendorService.BuildProductsResponse(player, VendorId));
    }

    [Fact]
    public void BuildProductsResponse_ListsTheCatalogWithDeterministicGuids()
    {
        var (_, player, _) = CreateVendorSession(authorizeTerminal: true);

        var response = NpcVendorService.BuildProductsResponse(player, VendorId);

        Assert.NotNull(response);
        Assert.Equal(VendorId, response.VendorId);
        Assert.Equal(2, response.Products.Length);
        Assert.Equal(30287u, response.Products[0].SdbId);
        Assert.Equal(VendorCatalog.ProductGuid(VendorId, 0), response.Products[0].GUID);
        Assert.Equal(VendorCatalog.PriceGuid(VendorId, 0), response.Products[0].Prices[0].GUID);
        Assert.Equal(Crystite, response.Products[0].Prices[0].CurrencyRemoteId);
        Assert.Equal(100u, response.Products[0].Prices[0].Amount);
    }

    /// <summary>
    ///     The regression that made a live-looking window unbuyable: the client carries the ids it is
    ///     given through scripted UI code, where every number is a 53-bit double, and hands them back
    ///     when Buy is pressed. Anything larger comes back rounded and no longer matches the stock
    ///     list, so the purchase never reaches the wire. Live sent ids below 2^21.
    /// </summary>
    [Fact]
    public void BuildProductsResponse_SendsIdsThatSurviveTheClientUiRoundTrip()
    {
        var (_, player, _) = CreateVendorSession(authorizeTerminal: true);

        var response = NpcVendorService.BuildProductsResponse(player, VendorId);

        Assert.NotNull(response);

        // The store id is what the client echoes back as the last field of a purchase (truncated to
        // 32 bits), and never the vendor NPC's 64-bit entity id.
        Assert.Equal((ulong)VendorCatalog.StoreId(VendorId), response.Id);
        Assert.NotEqual(player.CharacterEntity.AuthorizedTerminal.TerminalEntityId, response.Id);
        Assert.True(response.Id <= uint.MaxValue, $"store id {response.Id} does not fit the uint32 the client echoes back");

        foreach (var product in response.Products)
        {
            AssertIdRoundTrips(product.GUID);
            foreach (var price in product.Prices)
            {
                AssertIdRoundTrips(price.GUID);
            }
        }
    }

    /// <summary>
    ///     A purchase is validated by decoding the guids the client echoes back, so every guid the
    ///     window lists has to decode to this vendor and to its own stock index.
    /// </summary>
    [Fact]
    public void BuildProductsResponse_ListedGuidsDecodeBackToTheirOwnEntry()
    {
        var (_, player, _) = CreateVendorSession(authorizeTerminal: true);

        var response = NpcVendorService.BuildProductsResponse(player, VendorId);

        Assert.NotNull(response);
        for (var i = 0; i < response.Products.Length; i++)
        {
            var product = response.Products[i];

            Assert.True(VendorCatalog.TryDecodeGuid(product.GUID, out var vendorId, out var index, out var isPrice));
            Assert.Equal(VendorId, vendorId);
            Assert.Equal(i, index);
            Assert.False(isPrice);

            Assert.True(VendorCatalog.TryDecodeGuid(product.Prices[0].GUID, out vendorId, out index, out isPrice));
            Assert.Equal(VendorId, vendorId);
            Assert.Equal(i, index);
            Assert.True(isPrice);
        }
    }

    private static void AssertIdRoundTrips(ulong id)
    {
        Assert.True(id > 0, "a zero id reads as \"nothing selected\"");
        Assert.True(id <= int.MaxValue, $"id {id} exceeds the 32 bits a scripted UI can hold exactly");
        Assert.Equal(id, (ulong)(double)id); // exact through the client's double arithmetic
    }

    [Fact]
    public void TryPurchase_Success_ChargesTheCurrencyAndGrantsTheItem()
    {
        var (_, player, _) = CreateVendorSession(authorizeTerminal: true);
        player.Inventory.AddResource(Crystite, 500);

        var response = NpcVendorService.TryPurchase(
            player,
            VendorId,
            VendorCatalog.ProductGuid(VendorId, 0),
            VendorCatalog.PriceGuid(VendorId, 0));

        Assert.Equal(1, response.Success);

        // Live answers a successful purchase with an empty code and lets the client show its own
        // success text; a literal token would risk being displayed instead.
        Assert.Equal(string.Empty, response.Code);
        Assert.Equal(NpcVendorService.PurchaseSuccessfulCode, response.Code);
        Assert.Equal(VendorCatalog.StoreId(VendorId), response.VendorId);
        Assert.Equal(VendorCatalog.ProductGuid(VendorId, 0), response.ProductId);
        Assert.Equal(VendorCatalog.PriceGuid(VendorId, 0), response.PriceId);
        Assert.Equal(400u, player.Inventory.GetResourceQuantity(Crystite));
        Assert.Equal(1, player.Inventory.CountItemsBySdbId(30287));
    }

    /// <summary>
    ///     Spending the last of a currency drops the pool from the inventory; the client still has to
    ///     be told it is empty instead of the purchase blowing up on the removed dictionary key.
    /// </summary>
    [Fact]
    public void TryPurchase_SpendingTheLastOfTheCurrency_StillAnswersAndReplicatesTheEmptyPool()
    {
        var (shard, player, character) = CreateVendorSession(authorizeTerminal: true);
        var networkPlayer = (FakeNetworkPlayer)player;
        networkPlayer.AttachRealChannels();

        // Partial updates on: this is the path that replicates the emptied pool.
        player.Inventory = new CharacterInventory(shard, networkPlayer, character) { EnablePartialUpdates = true };
        player.Inventory.AddResource(Crystite, 100); // exactly the price of stock entry 0

        var response = NpcVendorService.TryPurchase(
            player,
            VendorId,
            VendorCatalog.ProductGuid(VendorId, 0),
            VendorCatalog.PriceGuid(VendorId, 0));

        Assert.Equal(1, response.Success);
        Assert.Equal(0u, player.Inventory.GetResourceQuantity(Crystite));
        Assert.Equal(1, player.Inventory.CountItemsBySdbId(30287));
    }

    /// <summary>
    ///     The store id a purchase names: the client echoes the id its window was opened with, and a
    ///     live 2015 capture shows it as a plain trailing uint32 rather than as the optional fields
    ///     AeroMessages models.
    /// </summary>
    [Fact]
    public void ReadStoreId_LiveTailLayout_ReadsTheTrailingUInt32()
    {
        // The exact 36 bytes a live client sent (build 1869: store 2321, product 583521, price 1297821).
        byte[] payload =
        [
            0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, // unknown
            0x01, 0x92, 0x34, 0xab, 0x64, 0xb8, 0x3c, 0x7f, // buyer entity id
            0x61, 0xe7, 0x08, 0x00, 0x00, 0x00, 0x00, 0x00, // product guid
            0x9d, 0xcd, 0x13, 0x00, 0x00, 0x00, 0x00, 0x00, // price guid
            0x11, 0x09, 0x00, 0x00,                          // store id 2321
        ];

        // What AeroMessages' optional-field model makes of those same bytes: nothing filled in.
        var request = new VendorPurchaseRequest { HaveUnk2 = 0x11, HaveUnk3 = 0x09, HaveUnk4 = 0x00 };

        Assert.Equal(2321u, NpcVendorService.ReadStoreId(request, payload));
    }

    [Fact]
    public void ReadStoreId_ModelledFields_WinOverTheRawTail()
    {
        byte[] payload = [.. Enumerable.Repeat((byte)0, 32), 0x11, 0x09, 0x00, 0x00];

        Assert.Equal(2321u, NpcVendorService.ReadStoreId(new VendorPurchaseRequest { HaveUnk2 = 1, ScuffedVendorID = 2321 }, payload));
        Assert.Equal(2321u, NpcVendorService.ReadStoreId(new VendorPurchaseRequest { HaveUnk3 = 1, VendorRemoteID = 2321 }, payload));
        Assert.Equal(0u, NpcVendorService.ReadStoreId(null, payload));

        // A short payload must not be read past its end.
        Assert.Equal(0u, NpcVendorService.ReadStoreId(new VendorPurchaseRequest(), payload[..20]));
    }

    [Fact]
    public void TryPurchase_InsufficientFunds_DeclinesAndKeepsTheWallet()
    {
        var (_, player, _) = CreateVendorSession(authorizeTerminal: true);
        player.Inventory.AddResource(Crystite, 50);

        var response = NpcVendorService.TryPurchase(
            player,
            VendorId,
            VendorCatalog.ProductGuid(VendorId, 0),
            VendorCatalog.PriceGuid(VendorId, 0));

        Assert.Equal(0, response.Success);
        Assert.Equal(NpcVendorService.InsufficientFundsCode, response.Code);
        Assert.Equal(50u, player.Inventory.GetResourceQuantity(Crystite));
        Assert.Equal(0, player.Inventory.CountItemsBySdbId(30287));
    }

    [Fact]
    public void TryPurchase_ForeignGuids_AreDeclined()
    {
        var (_, player, _) = CreateVendorSession(authorizeTerminal: true);
        player.Inventory.AddResource(Crystite, 5000);

        // A real inventory guid, and a product guid of a different vendor.
        var foreign = NpcVendorService.TryPurchase(player, VendorId, 0xdeadbeef_cafebabeul, 0x1234ul);
        var otherVendor = NpcVendorService.TryPurchase(
            player,
            VendorId,
            VendorCatalog.ProductGuid(999, 0),
            VendorCatalog.PriceGuid(999, 0));
        var mismatchedPrice = NpcVendorService.TryPurchase(
            player,
            VendorId,
            VendorCatalog.ProductGuid(VendorId, 0),
            VendorCatalog.PriceGuid(VendorId, 1));

        Assert.Equal(0, foreign.Success);
        Assert.Equal(NpcVendorService.InvalidProductCode, foreign.Code);
        Assert.Equal(0, otherVendor.Success);
        Assert.Equal(NpcVendorService.InvalidProductCode, otherVendor.Code);
        Assert.Equal(0, mismatchedPrice.Success);
        Assert.Equal(NpcVendorService.InvalidProductCode, mismatchedPrice.Code);
        Assert.Equal(5000u, player.Inventory.GetResourceQuantity(Crystite));
    }

    [Fact]
    public void ResolvePurchaseVendorId_WithAuthorizedTerminal_ReturnsTheOpenShop()
    {
        var (_, player, _) = CreateVendorSession(authorizeTerminal: true);

        // The client's vendor-id fields are unreliable (missing or scuffed): the open shop wins.
        Assert.Equal(VendorId, NpcVendorService.ResolvePurchaseVendorId(player, 0));
        Assert.Equal(VendorId, NpcVendorService.ResolvePurchaseVendorId(player, 999));
        Assert.Equal(VendorId, NpcVendorService.ResolvePurchaseVendorId(player, VendorId));
    }

    [Fact]
    public void ResolvePurchaseVendorId_WithoutAuthorizedTerminal_ReturnsThePacketId()
    {
        var (_, player, _) = CreateVendorSession(authorizeTerminal: false);

        Assert.Equal(VendorId, NpcVendorService.ResolvePurchaseVendorId(player, VendorId));
    }

    [Fact]
    public void TryPurchase_WithoutAuthorizedTerminal_IsDeclined()
    {
        var (_, player, _) = CreateVendorSession(authorizeTerminal: false);
        player.Inventory.AddResource(Crystite, 5000);

        var response = NpcVendorService.TryPurchase(
            player,
            VendorId,
            VendorCatalog.ProductGuid(VendorId, 0),
            VendorCatalog.PriceGuid(VendorId, 0));

        Assert.Equal(0, response.Success);
        Assert.Equal(NpcVendorService.VendorUnavailableCode, response.Code);
    }

    private static (FakeShard Shard, IPlayer Player, CharacterEntity Character) CreateVendorSession(bool authorizeTerminal)
    {
        var shard = new FakeShard();
        var character = FakeCharacterFactory.Create(shard);
        shard.EntityMan.Add(character.EntityId, character);

        var player = new FakeNetworkPlayer(shard) { CharacterEntity = character };
        character.SetControllingPlayer(player);

        if (authorizeTerminal)
        {
            character.SetAuthorizedTerminal(new AuthorizedTerminalData
            {
                TerminalType = NpcVendorService.VendorTerminalType,
                TerminalId = VendorId,

                // A vendor NPC's real entity id: the shape the live server authorizes terminals with
                // (Id << 8 | controller). The window must not hand this to the client as its store id.
                TerminalEntityId = 0xff00000000079c01ul,
            });
        }

        // Partial updates stay off, so the mutations never touch a (nonexistent) network channel.
        player.Inventory = new CharacterInventory(shard, null, character);
        return (shard, player, character);
    }
}
