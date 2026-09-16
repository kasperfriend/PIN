using System;
using System.Collections.Generic;
using GameServer.Data;
using GameServer.Entities.Character;
using GameServer.Enums;
using GameServer.StaticDB;
using GameServer.StaticDB.Records.dbitems;
using GameServer.Systems.Salvage;
using GameServer.Tests.Fakes;
using Xunit;
using RequestEntry = AeroMessages.GSS.Character.Command.ItemSalvageRequest;

namespace GameServer.Tests;

/// <summary>
///     The salvage pipeline: a request entry breaks down only what the database marks salvageable,
///     guid-carrying equipment is removed by guid and stackables by quantity out of their pool, and
///     every removed piece rolls the item's salvage loot table - the response echoes exactly what
///     was actually broken down so the client window stays in sync.
/// </summary>
public class SalvageServiceTests : IDisposable
{
    private const uint Weapon = 143670;
    private const uint HealthPack = 82604;
    private const uint NotSalvageable = 77066;
    private const uint Crystite = 10;
    private const uint FrameModule = 76958;

    public SalvageServiceTests()
    {
        SalvageService.LookupItem = ItemShelf;
        SalvageService.LookupSalvageRewards = RewardsShelf;
        SalvageService.RollLootTable = _ => [new SalvageAward(Crystite, 5, 0)];
    }

    public void Dispose()
    {
        SalvageService.LookupItem = SDBInterface.GetRootItem;
        SalvageService.LookupSalvageRewards = SDBInterface.GetSalvageRewards;
        SalvageService.RollLootTable = SalvageRoller.Roll;
    }

    [Fact]
    public void Run_GuidItem_RemovesItRollsOnceAndEchoesIt()
    {
        var (_, player, _) = CreateSession();
        var guid = player.Inventory.CreateItem(Weapon);

        var response = SalvageService.Run(player, [new RequestEntry { GUID = guid, SdbId = Weapon, Quantity = 1 }]);

        Assert.Single(response.SalvageResponses);
        Assert.Equal(guid, response.SalvageResponses[0].GUID);
        Assert.Equal(Weapon, response.SalvageResponses[0].SdbId);
        Assert.Equal(1u, response.SalvageResponses[0].Quantity);
        Assert.Equal(0, player.Inventory.CountItemsBySdbId(Weapon));
        Assert.Equal(5u, player.Inventory.GetResourceQuantity(Crystite));
    }

    [Fact]
    public void Run_GuidNotCarried_IsRefusedAndGrantsNothing()
    {
        var (_, player, _) = CreateSession();
        var guid = player.Inventory.CreateItem(Weapon);

        var response = SalvageService.Run(player, [new RequestEntry { GUID = guid + 0x100, SdbId = Weapon, Quantity = 1 }]);

        Assert.Empty(response.SalvageResponses);
        Assert.Equal(1, player.Inventory.CountItemsBySdbId(Weapon));
        Assert.Equal(0u, player.Inventory.GetResourceQuantity(Crystite));
    }

    [Fact]
    public void Run_Stack_ConsumesTheQuantityRollsPerPieceAndEchoesTheQuantity()
    {
        var (_, player, _) = CreateSession();
        player.Inventory.AddResource(HealthPack, 3);

        var rolls = 0;
        SalvageService.RollLootTable = _ =>
        {
            rolls++;
            return [new SalvageAward(Crystite, 2, 0)];
        };

        var response = SalvageService.Run(player, [new RequestEntry { GUID = 0, SdbId = HealthPack, Quantity = 3 }]);

        Assert.Single(response.SalvageResponses);
        Assert.Equal(0ul, response.SalvageResponses[0].GUID);
        Assert.Equal(3u, response.SalvageResponses[0].Quantity);
        Assert.Equal(3, rolls);
        Assert.Equal(0u, player.Inventory.GetResourceQuantity(HealthPack));
        Assert.Equal(6u, player.Inventory.GetResourceQuantity(Crystite));
    }

    /// <summary>
    ///     A stack salvage is all-or-nothing: asking for more than the pool holds must not nibble
    ///     away part of it while the client window believes the whole stack went.
    /// </summary>
    [Fact]
    public void Run_StackShortOfTheQuantity_LeavesThePoolWhole()
    {
        var (_, player, _) = CreateSession();
        player.Inventory.AddResource(HealthPack, 2);

        var response = SalvageService.Run(player, [new RequestEntry { GUID = 0, SdbId = HealthPack, Quantity = 3 }]);

        Assert.Empty(response.SalvageResponses);
        Assert.Equal(2u, player.Inventory.GetResourceQuantity(HealthPack));
        Assert.Equal(0u, player.Inventory.GetResourceQuantity(Crystite));
    }

    [Fact]
    public void Run_ItemWithoutSalvageRewards_IsRefused()
    {
        var (_, player, _) = CreateSession();
        var guid = player.Inventory.CreateItem(NotSalvageable);

        var response = SalvageService.Run(player, [new RequestEntry { GUID = guid, SdbId = NotSalvageable, Quantity = 1 }]);

        Assert.Empty(response.SalvageResponses);
        Assert.Equal(1, player.Inventory.CountItemsBySdbId(NotSalvageable));
        Assert.Equal(0u, player.Inventory.GetResourceQuantity(Crystite));
    }

    [Fact]
    public void Run_UnknownItem_IsRefused()
    {
        var (_, player, _) = CreateSession();

        var response = SalvageService.Run(player, [new RequestEntry { GUID = 0, SdbId = 999999, Quantity = 1 }]);

        Assert.Empty(response.SalvageResponses);
    }

    /// <summary>
    ///     Salvaged goods that are carried as items (a frame module, say) land as guid items while
    ///     the stackables (crystite) fill their pools.
    /// </summary>
    [Fact]
    public void Run_ItemAwards_LandAsItemsBesideTheStacks()
    {
        var (_, player, _) = CreateSession();
        var guid = player.Inventory.CreateItem(Weapon);

        SalvageService.RollLootTable = _ => [new SalvageAward(Crystite, 3, 0), new SalvageAward(FrameModule, 1, 0)];

        var response = SalvageService.Run(player, [new RequestEntry { GUID = guid, SdbId = Weapon, Quantity = 1 }]);

        Assert.Single(response.SalvageResponses);
        Assert.Equal(3u, player.Inventory.GetResourceQuantity(Crystite));
        Assert.Equal(1, player.Inventory.CountItemsBySdbId(FrameModule));
    }

    [Fact]
    public void Run_MixedBatch_SalvagesWhatItCanAndRefusesTheRest()
    {
        var (_, player, _) = CreateSession();
        var good = player.Inventory.CreateItem(Weapon);
        var junk = player.Inventory.CreateItem(NotSalvageable);

        var response = SalvageService.Run(
            player,
            [
                new RequestEntry { GUID = good, SdbId = Weapon, Quantity = 1 },
                new RequestEntry { GUID = junk, SdbId = NotSalvageable, Quantity = 1 },
            ]);

        Assert.Single(response.SalvageResponses);
        Assert.Equal(good, response.SalvageResponses[0].GUID);
        Assert.Equal(0, player.Inventory.CountItemsBySdbId(Weapon));
        Assert.Equal(1, player.Inventory.CountItemsBySdbId(NotSalvageable));
    }

    /// <summary>The item shelf: the weapon and the health pack are salvageable, the thumper marker is not.</summary>
    private static RootItem ItemShelf(uint sdbId) => sdbId switch
    {
        Weapon => new RootItem { SdbId = Weapon, Type = (byte)ItemType.Weapon, SalvageRewards = 10048 },
        HealthPack => new RootItem { SdbId = HealthPack, Type = (byte)ItemType.Consumable, SalvageRewards = 10015 },
        NotSalvageable => new RootItem { SdbId = NotSalvageable, Type = (byte)ItemType.Consumable, SalvageRewards = 0 },
        Crystite => new RootItem { SdbId = Crystite, Type = (byte)ItemType.Basic },
        FrameModule => new RootItem { SdbId = FrameModule, Type = (byte)ItemType.FrameModule },
        _ => null,
    };

    private static SalvageRewards RewardsShelf(uint id) => id switch
    {
        10048 => new SalvageRewards { Id = 10048, LootTable = 6945 },
        10015 => new SalvageRewards { Id = 10015, LootTable = 6225 },
        _ => null,
    };

    private static (FakeShard Shard, IPlayer Player, CharacterEntity Character) CreateSession()
    {
        var shard = new FakeShard();
        var character = FakeCharacterFactory.Create(shard);
        shard.EntityMan.Add(character.EntityId, character);

        var player = new FakeNetworkPlayer(shard) { CharacterEntity = character };
        character.SetControllingPlayer(player);

        // Partial updates stay off, so the mutations never touch a (nonexistent) network channel.
        player.Inventory = new CharacterInventory(shard, null, character);
        return (shard, player, character);
    }
}
