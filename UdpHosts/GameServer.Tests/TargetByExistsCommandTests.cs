using GameServer.Entities.Character;
using GameServer.StaticDB.Records.customdata;
using GameServer.Systems.Aptitude;
using GameServer.Systems.Aptitude.Commands.Target;
using GameServer.Tests.Fakes;
using Xunit;

namespace GameServer.Tests;

public class TargetByExistsCommandTests
{
    private static readonly TargetByExistsCommandDef Def = new() { Id = 114105 };

    [Fact]
    public void Execute_KeepsTargetsThatAreStillInTheShard()
    {
        var shard = new FakeShard();
        var initiator = CreateRegisteredCharacter(shard);
        var target = CreateRegisteredCharacter(shard);

        var context = new Context(shard, initiator);
        context.Targets.Push(target);

        Assert.True(new TargetByExistsCommand(Def).Execute(context));

        Assert.Equal(new IAptitudeTarget[] { target }, context.Targets.ToArray());
    }

    [Fact]
    public void Execute_DropsTargetsThatNoLongerExist()
    {
        var shard = new FakeShard();
        var initiator = CreateRegisteredCharacter(shard);
        var alive = CreateRegisteredCharacter(shard);
        var despawned = CreateRegisteredCharacter(shard);

        var context = new Context(shard, initiator);
        context.Targets.Push(alive);
        context.Targets.Push(despawned);

        shard.Entities.Remove(despawned.EntityId);

        Assert.True(new TargetByExistsCommand(Def).Execute(context));

        Assert.Equal(new IAptitudeTarget[] { alive }, context.Targets.ToArray());
        Assert.Equal(new IAptitudeTarget[] { alive, despawned }, context.FormerTargets.ToArray());
    }

    [Fact]
    public void Execute_WithoutAnySurvivingTarget_StillSucceeds()
    {
        var shard = new FakeShard();
        var initiator = CreateRegisteredCharacter(shard);
        var despawned = CreateRegisteredCharacter(shard);

        var context = new Context(shard, initiator);
        context.Targets.Push(despawned);

        shard.Entities.Remove(despawned.EntityId);

        // The def has no fail-on-empty flag, so an empty target list must not
        // break the chain.
        Assert.True(new TargetByExistsCommand(Def).Execute(context));

        Assert.Empty(context.Targets.ToArray());
    }

    [Fact]
    public void Execute_WithoutTargets_IsNoOp()
    {
        var shard = new FakeShard();
        var initiator = CreateRegisteredCharacter(shard);

        var context = new Context(shard, initiator);

        Assert.True(new TargetByExistsCommand(Def).Execute(context));

        Assert.Empty(context.Targets.ToArray());
    }

    [Fact]
    public void Execute_KeepsTargetOrder()
    {
        var shard = new FakeShard();
        var initiator = CreateRegisteredCharacter(shard);
        var first = CreateRegisteredCharacter(shard);
        var gone = CreateRegisteredCharacter(shard);
        var last = CreateRegisteredCharacter(shard);

        var context = new Context(shard, initiator);
        context.Targets.Push(first);
        context.Targets.Push(gone);
        context.Targets.Push(last);

        shard.Entities.Remove(gone.EntityId);

        Assert.True(new TargetByExistsCommand(Def).Execute(context));

        Assert.Equal(new IAptitudeTarget[] { first, last }, context.Targets.ToArray());
        Assert.Equal(last, context.Targets.Peek());
    }

    private static CharacterEntity CreateRegisteredCharacter(FakeShard shard)
    {
        var character = new CharacterEntity(shard, shard.GetNextGuid(0));
        shard.Entities.Add(character.EntityId, character);

        return character;
    }
}
