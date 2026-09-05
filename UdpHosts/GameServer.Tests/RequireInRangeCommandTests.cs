using System.Numerics;
using GameServer.Entities.Character;
using GameServer.StaticDB.Records.aptfs;
using GameServer.Systems.Aptitude;
using GameServer.Systems.Aptitude.Commands.Requirement;
using GameServer.Tests.Fakes;
using Xunit;

namespace GameServer.Tests;

public class RequireInRangeCommandTests
{
    [Fact]
    public void Execute_TargetWithinRange_Succeeds()
    {
        var shard = new FakeShard();
        var self = CreateCharacter(shard, Vector3.Zero);
        var target = CreateCharacter(shard, new Vector3(4, 0, 0));

        var context = new Context(shard, self);
        context.Targets.Push(target);

        Assert.True(new RequireInRangeCommand(Def(range: 5)).Execute(context));
    }

    [Fact]
    public void Execute_TargetOutOfRange_Fails()
    {
        var shard = new FakeShard();
        var self = CreateCharacter(shard, Vector3.Zero);
        var target = CreateCharacter(shard, new Vector3(0, 0, 12));

        var context = new Context(shard, self);
        context.Targets.Push(target);

        Assert.False(new RequireInRangeCommand(Def(range: 5)).Execute(context));
    }

    [Fact]
    public void Execute_AnyTargetOutOfRange_Fails()
    {
        var shard = new FakeShard();
        var self = CreateCharacter(shard, Vector3.Zero);

        var context = new Context(shard, self);
        context.Targets.Push(CreateCharacter(shard, new Vector3(1, 0, 0)));
        context.Targets.Push(CreateCharacter(shard, new Vector3(50, 0, 0)));

        Assert.False(new RequireInRangeCommand(Def(range: 10)).Execute(context));
    }

    [Fact]
    public void Execute_Negated_InvertsTheResult()
    {
        var shard = new FakeShard();
        var self = CreateCharacter(shard, Vector3.Zero);
        var target = CreateCharacter(shard, new Vector3(50, 0, 0));

        var context = new Context(shard, self);
        context.Targets.Push(target);

        Assert.True(new RequireInRangeCommand(Def(range: 10, negate: 1)).Execute(context));

        var closeContext = new Context(shard, self);
        closeContext.Targets.Push(CreateCharacter(shard, new Vector3(1, 0, 0)));

        Assert.False(new RequireInRangeCommand(Def(range: 10, negate: 1)).Execute(closeContext));
    }

    [Fact]
    public void Execute_WithoutTargets_Succeeds()
    {
        var shard = new FakeShard();
        var self = CreateCharacter(shard, Vector3.Zero);

        var context = new Context(shard, self);

        Assert.True(new RequireInRangeCommand(Def(range: 1)).Execute(context));
    }

    [Fact]
    public void Execute_DoesNotModifyTheTargetList()
    {
        var shard = new FakeShard();
        var self = CreateCharacter(shard, Vector3.Zero);
        var near = CreateCharacter(shard, new Vector3(1, 0, 0));
        var far = CreateCharacter(shard, new Vector3(99, 0, 0));

        var context = new Context(shard, self);
        context.Targets.Push(near);
        context.Targets.Push(far);

        new RequireInRangeCommand(Def(range: 10)).Execute(context);

        Assert.Equal(new IAptitudeTarget[] { near, far }, context.Targets.ToArray());
    }

    [Fact]
    public void Execute_RangeIsInclusive()
    {
        var shard = new FakeShard();
        var self = CreateCharacter(shard, Vector3.Zero);
        var target = CreateCharacter(shard, new Vector3(0, 10, 0));

        var context = new Context(shard, self);
        context.Targets.Push(target);

        Assert.True(new RequireInRangeCommand(Def(range: 10)).Execute(context));
    }

    private static RequireInRangeCommandDef Def(float range, byte negate = 0, byte useOffset = 0)
    {
        return new RequireInRangeCommandDef { Id = 1, Range = range, Negate = negate, Useoffset = useOffset };
    }

    private static CharacterEntity CreateCharacter(FakeShard shard, Vector3 position)
    {
        var character = new CharacterEntity(shard, shard.GetNextGuid(0)) { Position = position };
        shard.Entities.Add(character.EntityId, character);

        return character;
    }
}
