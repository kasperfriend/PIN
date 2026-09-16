using GameServer.Data;
using GameServer.Entities.Character;
using GameServer.StaticDB.Records.customdata;
using GameServer.Systems.Aptitude;
using GameServer.Systems.Aptitude.Commands.Other;
using GameServer.Tests.Fakes;
using Xunit;

namespace GameServer.Tests;

/// <summary>
///     The chain node that spends a consumable: the activation carries the consumable's sdb id as
///     its module, and the command takes one copy out of the activating player's inventory.
/// </summary>
public class ConsumeItemCommandTests
{
    private const uint HealthPack = 82604;

    [Fact]
    public void Execute_StackOwned_TakesOneOffTheStack()
    {
        var (shard, _, character) = CreateSession(out var player);
        player.Inventory.AddResource(HealthPack, 2);

        var command = new ConsumeItemCommand(new ConsumeItemCommandDef { Id = 1 });
        var context = new Context(shard, character) { AbilityModuleId = HealthPack };

        Assert.True(command.Execute(context));
        Assert.Equal(1u, player.Inventory.GetResourceQuantity(HealthPack));

        Assert.True(command.Execute(context));
        Assert.Equal(0u, player.Inventory.GetResourceQuantity(HealthPack));
    }

    [Fact]
    public void Execute_StackEmpty_FailsTheActivation()
    {
        var (shard, _, character) = CreateSession(out var player);

        var command = new ConsumeItemCommand(new ConsumeItemCommandDef { Id = 1 });
        var context = new Context(shard, character) { AbilityModuleId = HealthPack };

        Assert.False(command.Execute(context));
        Assert.Equal(0u, player.Inventory.GetResourceQuantity(HealthPack));
    }

    /// <summary>Older inventories can still hold guid copies of a consumable; those are taken too.</summary>
    [Fact]
    public void Execute_GuidCopy_TakesIt()
    {
        var (shard, _, character) = CreateSession(out var player);
        var guid = player.Inventory.CreateItem(HealthPack);

        var command = new ConsumeItemCommand(new ConsumeItemCommandDef { Id = 1 });
        var context = new Context(shard, character) { AbilityModuleId = HealthPack };

        Assert.True(command.Execute(context));
        Assert.False(player.Inventory.TryGetItem(guid, out _));
    }

    /// <summary>An activation with no consumable behind it (or an NPC's) has nothing to spend.</summary>
    [Fact]
    public void Execute_NoModuleOrNoInventory_IsANoOp()
    {
        var (shard, _, character) = CreateSession(out var player);
        player.Inventory = null;

        var command = new ConsumeItemCommand(new ConsumeItemCommandDef { Id = 1 });

        Assert.True(command.Execute(new Context(shard, character) { AbilityModuleId = HealthPack }));
        Assert.True(command.Execute(new Context(shard, character) { AbilityModuleId = 0 }));
    }

    private static (FakeShard Shard, FakeNetworkPlayer Player, CharacterEntity Character) CreateSession(out FakeNetworkPlayer player)
    {
        var shard = new FakeShard();
        var character = FakeCharacterFactory.Create(shard);
        player = new FakeNetworkPlayer(shard) { CharacterEntity = character };
        character.SetControllingPlayer(player);

        // Partial updates stay off, so the mutations never touch a (nonexistent) network channel.
        player.Inventory = new CharacterInventory(shard, null, character);
        return (shard, player, character);
    }
}
