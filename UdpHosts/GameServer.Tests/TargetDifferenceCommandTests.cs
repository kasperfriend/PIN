using GameServer.Entities.Character;
using GameServer.StaticDB.Records.apt;
using GameServer.Systems.Aptitude;
using GameServer.Systems.Aptitude.Commands.Target;
using GameServer.Tests.Fakes;
using Xunit;

namespace GameServer.Tests;

public class TargetDifferenceCommandTests
{
    [Fact]
    public void Execute_RemovesTargetsThatAreAlsoInTheFormerList()
    {
        var shard = new FakeShard();
        var initiator = CreateCharacter(shard);
        var stayed = CreateCharacter(shard);
        var entered = CreateCharacter(shard);

        var context = new Context(shard, initiator);
        context.FormerTargets.Push(stayed);
        context.Targets.Push(stayed);
        context.Targets.Push(entered);

        Assert.True(Command(replaceFormer: 0, swapCurrentFormer: 0).Execute(context));

        Assert.Equal(new IAptitudeTarget[] { entered }, context.Targets.ToArray());
    }

    [Fact]
    public void Execute_WithoutReplaceFormer_LeavesTheFormerListAlone()
    {
        var shard = new FakeShard();
        var initiator = CreateCharacter(shard);
        var stayed = CreateCharacter(shard);
        var entered = CreateCharacter(shard);

        var context = new Context(shard, initiator);
        context.FormerTargets.Push(stayed);
        context.Targets.Push(stayed);
        context.Targets.Push(entered);

        Assert.True(Command(replaceFormer: 0, swapCurrentFormer: 0).Execute(context));

        Assert.Equal(new IAptitudeTarget[] { stayed }, context.FormerTargets.ToArray());
    }

    [Fact]
    public void Execute_WithReplaceFormer_KeepsTheUnfilteredListAsFormer()
    {
        var shard = new FakeShard();
        var initiator = CreateCharacter(shard);
        var stayed = CreateCharacter(shard);
        var entered = CreateCharacter(shard);

        var context = new Context(shard, initiator);
        context.FormerTargets.Push(stayed);
        context.Targets.Push(stayed);
        context.Targets.Push(entered);

        Assert.True(Command(replaceFormer: 1, swapCurrentFormer: 0).Execute(context));

        Assert.Equal(new IAptitudeTarget[] { entered }, context.Targets.ToArray());
        Assert.Equal(new IAptitudeTarget[] { stayed, entered }, context.FormerTargets.ToArray());
    }

    [Fact]
    public void Execute_WithSwapCurrentFormer_SubtractsTheOtherWayAround()
    {
        var shard = new FakeShard();
        var initiator = CreateCharacter(shard);
        var inner = CreateCharacter(shard);
        var outerOnly = CreateCharacter(shard);

        // The layered cone blasts: the big volume is swapped into the former
        // list, the small one is collected into the current list, and the
        // command has to keep the ring between them.
        var context = new Context(shard, initiator);
        context.FormerTargets.Push(inner);
        context.FormerTargets.Push(outerOnly);
        context.Targets.Push(inner);

        Assert.True(Command(replaceFormer: 1, swapCurrentFormer: 1).Execute(context));

        Assert.Equal(new IAptitudeTarget[] { outerOnly }, context.Targets.ToArray());
        Assert.Equal(new IAptitudeTarget[] { inner, outerOnly }, context.FormerTargets.ToArray());
    }

    [Fact]
    public void Execute_KeepsTargetOrder()
    {
        var shard = new FakeShard();
        var initiator = CreateCharacter(shard);
        var first = CreateCharacter(shard);
        var shared = CreateCharacter(shard);
        var last = CreateCharacter(shard);

        var context = new Context(shard, initiator);
        context.FormerTargets.Push(shared);
        context.Targets.Push(first);
        context.Targets.Push(shared);
        context.Targets.Push(last);

        Assert.True(Command(replaceFormer: 0, swapCurrentFormer: 0).Execute(context));

        Assert.Equal(new IAptitudeTarget[] { first, last }, context.Targets.ToArray());
        Assert.Equal(last, context.Targets.Peek());
    }

    [Fact]
    public void Execute_WithoutFormerTargets_KeepsEveryTarget()
    {
        var shard = new FakeShard();
        var initiator = CreateCharacter(shard);
        var target = CreateCharacter(shard);

        var context = new Context(shard, initiator);
        context.Targets.Push(target);

        Assert.True(Command(replaceFormer: 0, swapCurrentFormer: 0).Execute(context));

        Assert.Equal(new IAptitudeTarget[] { target }, context.Targets.ToArray());
    }

    [Fact]
    public void Execute_WithIdenticalLists_ClearsTheTargetsButStillSucceeds()
    {
        var shard = new FakeShard();
        var initiator = CreateCharacter(shard);
        var target = CreateCharacter(shard);

        var context = new Context(shard, initiator);
        context.FormerTargets.Push(target);
        context.Targets.Push(target);

        // No fail-on-empty flag on this def: an empty difference must not
        // break the chain.
        Assert.True(Command(replaceFormer: 0, swapCurrentFormer: 0).Execute(context));

        Assert.Empty(context.Targets.ToArray());
    }

    [Fact]
    public void Execute_WithoutAnyTargets_IsNoOp()
    {
        var shard = new FakeShard();
        var initiator = CreateCharacter(shard);

        var context = new Context(shard, initiator);

        Assert.True(Command(replaceFormer: 1, swapCurrentFormer: 1).Execute(context));

        Assert.Empty(context.Targets.ToArray());
        Assert.Empty(context.FormerTargets.ToArray());
    }

    private static TargetDifferenceCommand Command(byte replaceFormer, byte swapCurrentFormer)
    {
        return new TargetDifferenceCommand(new TargetDifferenceCommandDef
        {
            Id = 1028387,
            ReplaceFormer = replaceFormer,
            SwapCurrentFormer = swapCurrentFormer
        });
    }

    private static CharacterEntity CreateCharacter(FakeShard shard)
    {
        var character = new CharacterEntity(shard, shard.GetNextGuid(0));
        shard.Entities.Add(character.EntityId, character);

        return character;
    }
}
