using GameServer;
using GameServer.Entities;
using GameServer.Entities.Character;
using GameServer.StaticDB.Records.customdata;
using GameServer.Systems.Aptitude;
using GameServer.Systems.Aptitude.Commands.Interaction;
using GameServer.Tests.Fakes;
using Xunit;

namespace GameServer.Tests;

/// <summary>
///     Pins the server-timed E-key flow: the completion-time command records the channel, the
///     in-progress gate expires it at exactly the authored duration, and the end command splits a
///     completed channel (content fires - the vendor terminal gets authorized) from a cancelled one
///     (state cleared, nothing fires).
/// </summary>
public class InteractionCommandsTests
{
    private const uint StartTime = 60_000;

    [Fact]
    public void CompletionTime_RecordsTheTargetsAuthoredDuration()
    {
        var shard = new FakeShard { CurrentTimeLong = StartTime };
        var (player, _) = CreatePlayer(shard);
        var npc = CreateVendorNpc(shard, vendorId: 310, durationMs: 500);

        var context = new Context(shard, player);
        context.Targets.Push(npc);

        var command = new InteractionCompletionTimeCommand(new InteractionCompletionTimeCommandDef { Id = 1 });
        Assert.True(command.Execute(context));

        Assert.NotNull(player.ActiveInteraction);
        Assert.Equal(npc.EntityId, player.ActiveInteraction.TargetEntityId);
        Assert.Equal(StartTime, player.ActiveInteraction.StartTimeMs);
        Assert.Equal(StartTime + 500, player.ActiveInteraction.CompletionTimeMs);
    }

    [Fact]
    public void InProgress_FailsExactlyWhenTheCompletionTimeHasPassed()
    {
        var shard = new FakeShard { CurrentTimeLong = StartTime };
        var (player, _) = CreatePlayer(shard);
        var npc = CreateVendorNpc(shard, vendorId: 310, durationMs: 500);

        var context = new Context(shard, player);
        context.Targets.Push(npc);
        new InteractionCompletionTimeCommand(new InteractionCompletionTimeCommandDef { Id = 1 }).Execute(context);

        var gate = new InteractionInProgressCommand(new InteractionInProgressCommandDef { Id = 2 });
        Assert.True(gate.Execute(context));

        shard.CurrentTimeLong = StartTime + 499;
        Assert.True(gate.Execute(context));

        shard.CurrentTimeLong = StartTime + 500;
        Assert.False(gate.Execute(context));

        // No recorded channel means nothing holds the effect open.
        player.ActiveInteraction = null;
        Assert.False(gate.Execute(context));
    }

    [Fact]
    public void EndInteraction_CancelledChannel_ClearsStateAndFiresNoContent()
    {
        var shard = new FakeShard { CurrentTimeLong = StartTime };
        var (player, networkPlayer) = CreatePlayer(shard);
        var npc = CreateVendorNpc(shard, vendorId: 310, durationMs: 500);

        var context = new Context(shard, player);
        context.Targets.Push(npc);
        new InteractionCompletionTimeCommand(new InteractionCompletionTimeCommandDef { Id = 1 }).Execute(context);

        // Halfway through the channel the player lets go of the key.
        shard.CurrentTimeLong = StartTime + 250;
        var command = new EndInteractionCommand(3);
        Assert.True(command.Execute(context));

        Assert.Null(player.ActiveInteraction);
        Assert.Equal(0, player.AuthorizedTerminal.TerminalType);
        networkPlayer.FlushAttachedChannels();
    }

    [Fact]
    public void EndInteraction_CompletedVendorChannel_AuthorizesTheVendorTerminal()
    {
        var shard = new FakeShard { CurrentTimeLong = StartTime };
        var (player, networkPlayer) = CreatePlayer(shard);
        var npc = CreateVendorNpc(shard, vendorId: 310, durationMs: 500);

        var context = new Context(shard, player);
        context.Targets.Push(npc);
        new InteractionCompletionTimeCommand(new InteractionCompletionTimeCommandDef { Id = 1 }).Execute(context);

        shard.CurrentTimeLong = StartTime + 600;
        var command = new EndInteractionCommand(3);
        Assert.True(command.Execute(context));

        Assert.Null(player.ActiveInteraction);
        Assert.Equal(7, player.AuthorizedTerminal.TerminalType);
        Assert.Equal(310u, player.AuthorizedTerminal.TerminalId);
        Assert.Equal(npc.AeroEntityId.Backing, player.AuthorizedTerminal.TerminalEntityId);
        networkPlayer.FlushAttachedChannels();
    }

    [Fact]
    public void EndInteraction_NoRecordedChannel_CompletesLikeAVehicleBoarding()
    {
        // Vehicle/doctor/transport interactions call ability 181 straight from their apply chain and
        // never record a channel; they must still count as completed content.
        var shard = new FakeShard { CurrentTimeLong = StartTime };
        var (player, networkPlayer) = CreatePlayer(shard);
        var npc = FakeCharacterFactory.Create(shard);
        npc.Interaction = new InteractionComponent { Type = InteractionType.Doctor, DurationMs = 0 };
        shard.EntityMan.Add(npc.EntityId, npc);

        var context = new Context(shard, player);
        context.Targets.Push(npc);

        var command = new EndInteractionCommand(3);
        Assert.True(command.Execute(context));

        Assert.Null(player.ActiveInteraction);
        networkPlayer.FlushAttachedChannels();
    }

    private static (CharacterEntity Player, FakeNetworkPlayer Network) CreatePlayer(FakeShard shard)
    {
        var character = FakeCharacterFactory.Create(shard);
        shard.EntityMan.Add(character.EntityId, character);
        character.SetCharacterState(AeroMessages.GSS.Character.CharacterStateData.CharacterStatus.Living, StartTime);

        var networkPlayer = new FakeNetworkPlayer(shard) { CharacterEntity = character };
        networkPlayer.AttachRealChannels();
        character.SetControllingPlayer(networkPlayer);
        return (character, networkPlayer);
    }

    private static CharacterEntity CreateVendorNpc(FakeShard shard, uint vendorId, uint durationMs)
    {
        var npc = FakeCharacterFactory.Create(shard);
        npc.Interaction = new InteractionComponent
        {
            Type = InteractionType.Vendor,
            VendorId = vendorId,
            DurationMs = durationMs,
        };
        shard.EntityMan.Add(npc.EntityId, npc);
        return npc;
    }
}
