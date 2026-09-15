using System.Linq;
using GameServer;
using GameServer.Entities;
using GameServer.Entities.Character;
using GameServer.StaticDB.Records.aptfs;
using GameServer.StaticDB.Records.customdata;
using GameServer.Systems.Aptitude;
using GameServer.Systems.Aptitude.Commands.Impact;
using GameServer.Systems.Aptitude.Commands.Interaction;
using GameServer.Systems.Aptitude.Commands.Requirement;
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

    [Fact]
    public void VendorChannel_RunsThroughTheFullAbilityGraph_UntilTheCompletionTime()
    {
        // The prod-1962 vendor interaction graph the shard runs (MOBS_AND_NPCS.md 5.2), server
        // side: the root "interacting" effect 269 and the per-type (vendor) effect 279 both live
        // on the player, and both carry the interact target in their context target lists
        // (ImpactApplyEffect PassTargets=1). 279's duration chain holds the channel open while
        // the recorded completion time has not passed and the owner still carries 269; 279's
        // remove chain clears 269 (the CallCommand -> ability 181 step), and 269's remove chain
        // ends the interaction. The duration requirement must ask about the effect's OWNER -
        // the NPC target never carries effect 269, so a target-based check expires the vendor
        // effect on its first duration tick and the interaction dies "cancelled - no content".
        var shard = new FakeShard { CurrentTimeLong = StartTime };
        var factory = new FakeAptitudeFactory(shard);
        shard.Abilities = new AbilitySystem(shard, factory);
        var (player, networkPlayer) = CreatePlayer(shard);
        var npc = CreateVendorNpc(shard, vendorId: 310, durationMs: 500);

        factory.Effects[269] = MakeEffect(269, remove: Commands(new EndInteractionCommand(1154325)));
        factory.Effects[279] = MakeEffect(279,
            duration: Commands(
                new InteractionInProgressCommand(new InteractionInProgressCommandDef { Id = 1135846 }),
                new RequireHasEffectCommand(new RequireHasEffectCommandDef { Id = 1154324, EffectId = 269 })),
            remove: Commands(new ImpactRemoveEffectCommand(new ImpactRemoveEffectCommandDef { EffectId = 269, RemoveFromSelf = true })));

        var activation = new Context(shard, player) { Targets = new AptitudeTargets(npc) };
        Assert.True(shard.Abilities.DoApplyEffect(269, player, activation));
        Assert.True(shard.Abilities.DoApplyEffect(279, player, activation));
        new InteractionCompletionTimeCommand(new InteractionCompletionTimeCommandDef { Id = 1135843 }).Execute(activation);

        // One millisecond before the channel completes: the vendor effect must still be holding.
        Tick(shard, player, StartTime + 499);
        Assert.NotNull(GetActive(player, 279));
        Assert.NotNull(player.ActiveInteraction);
        Assert.Equal(0, player.AuthorizedTerminal.TerminalType);

        // At the recorded completion time the vendor effect expires, its remove chain clears the
        // root effect, and the end command fires the completed content: the vendor terminal.
        Tick(shard, player, StartTime + 500);
        Assert.Null(GetActive(player, 279));
        Assert.Null(GetActive(player, 269));
        Assert.Null(player.ActiveInteraction);
        Assert.Equal(7, player.AuthorizedTerminal.TerminalType);
        Assert.Equal(310u, player.AuthorizedTerminal.TerminalId);
        networkPlayer.FlushAttachedChannels();
    }

    private static EffectState GetActive(CharacterEntity character, uint effectId)
    {
        return character.GetActiveEffects().FirstOrDefault(state => state?.Effect.Id == effectId);
    }

    private static void Tick(FakeShard shard, CharacterEntity character, ulong time)
    {
        shard.CurrentTimeLong = time;
        shard.Abilities.ProcessTarget(character, time);
    }

    private static Chain Commands(params ICommand[] commands) => new() { Commands = [.. commands] };

    private static Effect MakeEffect(uint id, Chain duration = null, Chain remove = null) => new()
    {
        Data = new StaticDB.Records.apt.StatusEffectData { Id = id, MaxStackCount = 1, UpdateFrequency = 20 },
        DurationChain = duration,
        RemoveChain = remove,
    };

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
