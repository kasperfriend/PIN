using System;
using System.Collections.Generic;
using System.Linq;
using AeroMessages.GSS;
using AeroMessages.GSS.Character;
using AeroMessages.GSS.Character.Command;
using AeroMessages.GSS.Character.Controller;
using GameServer.Data;
using GameServer.Entities.Character;
using GameServer.StaticDB.Records.dbcharacter;
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

    /// <summary>Supply Officer Cross, Copacabana: the one vendor a live server was recorded serving.</summary>
    private const uint CopacabanaVendorId = LiveVendorData.SupplyOfficerCrossVendorId;

    /// <summary>The Accord Reward Quartermaster's token vending machine, and the token it takes.</summary>
    private const uint MachineVendorId = 5;
    private const uint AccordToken = 85771;

    /// <summary>
    ///     A vendor NPC's entity id in the shape the live server authorizes terminals with
    ///     (id &lt;&lt; 8 | controller): the window masks the controller bits off before it looks the
    ///     NPC up.
    /// </summary>
    private const ulong VendorNpcEntityId = 0xff00000000079c00ul;

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
        NpcVendorService.DescribeStore = VendorCatalog.Describe;
        NpcVendorService.RollTokenMachine = VendorTokenRoll.Roll;
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

    /// <summary>
    ///     A captured store describes itself, not the NPC standing at it: live titled terminal 60's
    ///     window "Copacabana ARES Supplies", sent the POI's faction (20) though Supply Officer Cross
    ///     is Accord, opened the window with store id 2321, and carried the three discount rungs of
    ///     that faction's <c>dbcharacter::FactionReputations</c> - 10%, 20% and 30% off.
    /// </summary>
    [Fact]
    public void BuildProductsResponse_CapturedStore_SendsTheStoreNotTheNpc()
    {
        var (shard, player, _) = CreateVendorSession(authorizeTerminal: true, terminalId: CopacabanaVendorId);
        AddVendorNpc(shard, "Supply Officer Cross", factionId: 1);   // Accord, not the POI's faction

        var response = NpcVendorService.BuildProductsResponse(player, CopacabanaVendorId);

        Assert.NotNull(response);
        Assert.Equal((ulong)LiveVendorData.CopacabanaStoreId, response.Id);
        Assert.Equal("Copacabana ARES Supplies", response.Title);
        Assert.Equal(LiveVendorData.CopacabanaFactionId, response.FactionId);

        var ladder = LiveVendorData.CopacabanaAresSupplies.Discounts;
        Assert.Equal(ladder.Count, response.FactionDiscounts.Length);
        for (var i = 0; i < ladder.Count; i++)
        {
            Assert.Equal(ladder[i].MinReputation, response.FactionDiscounts[i].MinRep);
            Assert.Equal(ladder[i].Discount, response.FactionDiscounts[i].Discount);
        }
    }

    /// <summary>
    ///     A reputation-gated row is sent the way the capture shows it: restriction type
    ///     <c>MinReputationRestriction</c>, with the store's faction and the rung the row needs as
    ///     JSON <i>strings</i>. PIN has no reputation system, so these rows stay locked - which is
    ///     what live showed a player without the standing, and better than a price nobody can pay.
    /// </summary>
    [Fact]
    public void BuildProductsResponse_GatedRow_CarriesTheCapturedRestriction()
    {
        var (_, player, _) = CreateVendorSession(authorizeTerminal: true, terminalId: CopacabanaVendorId);
        NpcVendorService.BuildCatalog = _ =>
        [
            new VendorCatalogEntry
            {
                Index = 0,
                SdbId = 87705,
                Quantity = 1,
                CurrencySdbId = Crystite,
                Cost = 150,
                Name = "Broken LMG",
                MinReputation = 6000,
                Provenance = VendorDataProvenance.Captured,
            },
            new VendorCatalogEntry { Index = 1, SdbId = 30287, Quantity = 1, CurrencySdbId = Crystite, Cost = 100, Name = "Health Pack, Small" },
        ];

        var response = NpcVendorService.BuildProductsResponse(player, CopacabanaVendorId);

        Assert.NotNull(response);
        var gated = response.Products[0];
        Assert.Single(gated.Restrictions);
        Assert.Equal(NpcVendorService.MinReputationRestrictionType, gated.Restrictions[0].Type);
        Assert.Equal("{\"faction_id\":\"20\",\"reputation\":\"6000\"}", gated.Restrictions[0].OptionsJSON);

        // An unrestricted row carries none, which is what the capture's first ten rows did.
        Assert.Empty(response.Products[1].Restrictions);
    }

    /// <summary>
    ///     The window a quartermaster opens is titled after the NPC and discounted against the NPC's
    ///     own faction rungs - there is no capture for it, so nothing is borrowed from Copacabana
    ///     except the fractions live discounted by.
    /// </summary>
    [Fact]
    public void BuildProductsResponse_AnyOtherVendor_SendsTheNpcAndItsFactionRungs()
    {
        var (shard, player, _) = CreateVendorSession(authorizeTerminal: true);
        AddVendorNpc(shard, "Corporal Belle", factionId: 1);
        (string Title, uint FactionId)? described = null;
        NpcVendorService.DescribeStore = (vendorId, npcTitle, npcFactionId) =>
        {
            described = (npcTitle, npcFactionId);
            return new VendorStoreDecor
            {
                StoreId = vendorId,
                Title = npcTitle,
                FactionId = npcFactionId,
                Discounts = VendorCatalog.DiscountTiersFor(
                    npcFactionId,
                    _ => [new FactionReputations { FactionId = npcFactionId, MinReputation = 6000 }]),
            };
        };

        var response = NpcVendorService.BuildProductsResponse(player, VendorId);

        Assert.NotNull(response);

        // The window is described from the NPC the terminal names: its display name and its faction.
        Assert.NotNull(described);
        Assert.Equal("Corporal Belle", described.Value.Title);
        Assert.Equal(1u, described.Value.FactionId);
        Assert.Equal("Corporal Belle", response.Title);
        Assert.Equal(1u, response.FactionId);
        Assert.Equal((ulong)VendorId, response.Id);
        Assert.Single(response.FactionDiscounts);
        Assert.Equal(6000u, response.FactionDiscounts[0].MinRep);
        Assert.Equal(0.1f, response.FactionDiscounts[0].Discount);
        Assert.All(response.Products, product => Assert.Empty(product.Restrictions));
    }

    /// <summary>
    ///     An NPC the shard does not hold leaves the window titled "Vendor" with no faction - which is
    ///     what every test in this class that does not add one exercises.
    /// </summary>
    [Fact]
    public void BuildProductsResponse_WithoutTheNpcInShard_TitlesTheWindowGenerically()
    {
        var (_, player, _) = CreateVendorSession(authorizeTerminal: true);

        var response = NpcVendorService.BuildProductsResponse(player, VendorId);

        Assert.NotNull(response);
        Assert.Equal("Vendor", response.Title);
        Assert.Equal(0u, response.FactionId);
        Assert.Empty(response.FactionDiscounts);
    }

    /// <summary>
    ///     Buying a row of a token vending machine spends the token and rolls the machine's loot
    ///     tables: the prize is what the roll lands on, not the item the cabinet advertised on the
    ///     row that was clicked, and one token pays out once per slot the machine has.
    /// </summary>
    [Fact]
    public void TryPurchase_TokenMachineRow_SpendsTheTokenAndRollsTheLootTables()
    {
        (uint MachineId, uint KeyItemId)? asked = null;
        NpcVendorService.BuildCatalog = _ =>
        [
            new VendorCatalogEntry
            {
                Index = 0,
                SdbId = 85907,
                Quantity = 1,
                CurrencySdbId = AccordToken,
                Cost = 1,
                Name = "New You Unlock: Wild Child Hairstyle",
                TokenMachineRoll = true,
                Provenance = VendorDataProvenance.DatabaseDerived,
            },
        ];
        NpcVendorService.RollTokenMachine = (machineId, keyItemId) =>
        {
            asked = (machineId, keyItemId);

            // Two slots: crystite from slot 1, an unlock from slot 2's aesthetics branch.
            return [new TokenRollAward(10, 40, 5853), new TokenRollAward(85907, 2, 5984)];
        };

        var (_, player, _) = CreateVendorSession(authorizeTerminal: true, terminalId: MachineVendorId);
        player.Inventory.AddResource(AccordToken, 1);

        var response = NpcVendorService.TryPurchase(
            player,
            MachineVendorId,
            VendorCatalog.ProductGuid(MachineVendorId, 0),
            VendorCatalog.PriceGuid(MachineVendorId, 0));

        Assert.Equal(1, response.Success);
        Assert.Equal(NpcVendorService.PurchaseSuccessfulCode, response.Code);
        Assert.Equal(VendorCatalog.StoreId(MachineVendorId), response.VendorId);

        // The machine id is the vendor id, and the token spent is the key item it takes.
        Assert.NotNull(asked);
        Assert.Equal(MachineVendorId, asked.Value.MachineId);
        Assert.Equal(AccordToken, asked.Value.KeyItemId);

        Assert.Equal(0u, player.Inventory.GetResourceQuantity(AccordToken));
        Assert.Equal(2, player.Inventory.CountItemsBySdbId(85907));

        // Without the database loaded the roll's crystite lands as items rather than as a resource
        // top-up; what matters is that every award the roll returned was handed over.
        Assert.Equal(40, player.Inventory.CountItemsBySdbId(10));
    }

    /// <summary>
    ///     A machine whose tables award nothing still takes the token - that is what putting it in the
    ///     machine means - and still answers the purchase, so the client's window does not hang.
    /// </summary>
    [Fact]
    public void TryPurchase_TokenMachineWithNothingToAward_StillSpendsTheToken()
    {
        NpcVendorService.BuildCatalog = _ =>
        [
            new VendorCatalogEntry { Index = 0, SdbId = 85907, Quantity = 1, CurrencySdbId = AccordToken, Cost = 1, Name = "New You Unlock: Wild Child Hairstyle", TokenMachineRoll = true },
        ];
        NpcVendorService.RollTokenMachine = (_, _) => [];

        var (_, player, _) = CreateVendorSession(authorizeTerminal: true, terminalId: MachineVendorId);
        player.Inventory.AddResource(AccordToken, 1);

        var response = NpcVendorService.TryPurchase(
            player,
            MachineVendorId,
            VendorCatalog.ProductGuid(MachineVendorId, 0),
            VendorCatalog.PriceGuid(MachineVendorId, 0));

        Assert.Equal(1, response.Success);
        Assert.Equal(0u, player.Inventory.GetResourceQuantity(AccordToken));
        Assert.Equal(0, player.Inventory.CountItemsBySdbId(85907));
    }

    /// <summary>
    ///     A declined purchase still names the store the window was opened with: all three purchase
    ///     answers in the capture carry 2321, whether the sale went through or not.
    /// </summary>
    [Fact]
    public void TryPurchase_OnTheCapturedStore_AnswersWithTheStoreId()
    {
        var (_, player, _) = CreateVendorSession(authorizeTerminal: true, terminalId: CopacabanaVendorId);
        player.Inventory.AddResource(Crystite, 1); // the shelf's first row costs 100

        var response = NpcVendorService.TryPurchase(
            player,
            CopacabanaVendorId,
            VendorCatalog.ProductGuid(CopacabanaVendorId, 0),
            VendorCatalog.PriceGuid(CopacabanaVendorId, 0));

        Assert.Equal(0, response.Success);
        Assert.Equal(NpcVendorService.InsufficientFundsCode, response.Code);
        Assert.Equal(LiveVendorData.CopacabanaStoreId, response.VendorId);
    }

    /// <summary>
    ///     Puts a vendor NPC in the shard at the entity id the authorized terminal names, so the
    ///     window finds it and takes its name and faction from it.
    /// </summary>
    private static CharacterEntity AddVendorNpc(FakeShard shard, string displayName, byte factionId)
    {
        var npc = new CharacterEntity(shard, VendorNpcEntityId);
        npc.SetStaticInfo(new StaticInfoData { DisplayName = displayName, UniqueName = displayName });
        npc.SetHostilityInfo(new HostilityInfoData { Flags = HostilityInfoData.HostilityFlags.Faction, FactionId = factionId });
        shard.Entities[npc.EntityId] = npc;
        return npc;
    }

    private static (FakeShard Shard, IPlayer Player, CharacterEntity Character) CreateVendorSession(bool authorizeTerminal, uint terminalId = VendorId)
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
                TerminalId = terminalId,

                // A vendor NPC's real entity id, controller bits and all. The window must not hand
                // this to the client as its store id.
                TerminalEntityId = VendorNpcEntityId | 1,
            });
        }

        // Partial updates stay off, so the mutations never touch a (nonexistent) network channel.
        player.Inventory = new CharacterInventory(shard, null, character);
        return (shard, player, character);
    }
}
