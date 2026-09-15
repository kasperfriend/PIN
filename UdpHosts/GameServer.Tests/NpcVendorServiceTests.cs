using System;
using System.Collections.Generic;
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
        Assert.Equal(NpcVendorService.PurchaseSuccessfulCode, response.Code);
        Assert.Equal(400u, player.Inventory.GetResourceQuantity(Crystite));
        Assert.Equal(1, player.Inventory.CountItemsBySdbId(30287));
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
                TerminalEntityId = 0,
            });
        }

        // Partial updates stay off, so the mutations never touch a (nonexistent) network channel.
        player.Inventory = new CharacterInventory(shard, null, character);
        return (shard, player, character);
    }
}
