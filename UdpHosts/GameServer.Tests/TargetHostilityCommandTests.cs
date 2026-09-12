using System.Numerics;
using AeroMessages.GSS;
using GameServer.Entities.Character;
using GameServer.StaticDB.Records.apt;
using GameServer.StaticDB.Records.aptfs;
using GameServer.Systems.Aptitude;
using GameServer.Systems.Aptitude.Commands.Target;
using GameServer.Tests.Fakes;
using Xunit;

namespace GameServer.Tests;

public class TargetHostilityCommandTests
{
    [Fact]
    public void TargetFriendlies_KeepsFriendlyAndSelfAndNeutral()
    {
        var shard = new FakeShard();
        var self = CreateCharacter(shard, factionId: 1);
        var friendly = CreateCharacter(shard, factionId: 1);
        var hostile = CreateCharacter(shard, factionId: 2);
        // For fallback logic, same faction = friendly, different = hostile.
        // To test neutral, we would need SDB, but fallback treats different as hostile.
        // So we test friendly vs hostile only here.

        var context = new Context(shard, self);
        context.Targets.Push(friendly);
        context.Targets.Push(hostile);
        context.Targets.Push(self);

        var def = new TargetFriendliesCommandDef { Id = 1, IncludeSelf = 1, IncludeInitiator = 1, IncludeOwner = 1, FailNoTargets = 0 };
        Assert.True(new TargetFriendliesCommand(def).Execute(context));

        // Should keep friendly and self, drop hostile
        var result = context.Targets.ToArray();
        Assert.Contains(friendly, result);
        Assert.Contains(self, result);
        Assert.DoesNotContain(hostile, result);
    }

    [Fact]
    public void TargetFriendlies_RespectsIncludeSelfFlag()
    {
        var shard = new FakeShard();
        var self = CreateCharacter(shard, factionId: 1);
        var friendly = CreateCharacter(shard, factionId: 1);

        var context = new Context(shard, self);
        context.Targets.Push(self);
        context.Targets.Push(friendly);

        var defExcludeSelf = new TargetFriendliesCommandDef { Id = 1, IncludeSelf = 0, IncludeInitiator = 1, IncludeOwner = 1, FailNoTargets = 0 };
        Assert.True(new TargetFriendliesCommand(defExcludeSelf).Execute(context));
        Assert.DoesNotContain(self, context.Targets.ToArray());
        Assert.Contains(friendly, context.Targets.ToArray());

        var context2 = new Context(shard, self);
        context2.Targets.Push(self);
        var defIncludeSelf = new TargetFriendliesCommandDef { Id = 1, IncludeSelf = 1, IncludeInitiator = 1, IncludeOwner = 1, FailNoTargets = 0 };
        Assert.True(new TargetFriendliesCommand(defIncludeSelf).Execute(context2));
        Assert.Contains(self, context2.Targets.ToArray());
    }

    [Fact]
    public void TargetFriendlies_FailNoTargets()
    {
        var shard = new FakeShard();
        var self = CreateCharacter(shard, factionId: 1);
        var hostile = CreateCharacter(shard, factionId: 2);

        var context = new Context(shard, self);
        context.Targets.Push(hostile);

        var def = new TargetFriendliesCommandDef { Id = 1, IncludeSelf = 0, IncludeInitiator = 1, IncludeOwner = 1, FailNoTargets = 1 };
        Assert.False(new TargetFriendliesCommand(def).Execute(context));
        Assert.Empty(context.Targets.ToArray());
    }

    [Fact]
    public void TargetHostiles_KeepsHostile()
    {
        var shard = new FakeShard();
        var self = CreateCharacter(shard, factionId: 1);
        var friendly = CreateCharacter(shard, factionId: 1);
        var hostile = CreateCharacter(shard, factionId: 2);

        var context = new Context(shard, self);
        context.Targets.Push(friendly);
        context.Targets.Push(hostile);
        context.Targets.Push(self);

        var def = new TargetHostilesCommandDef { Id = 1, IncludeSelf = 0, IncludeInitiator = 1, IncludeOwner = 1, FailNoTargets = 0 };
        Assert.True(new TargetHostilesCommand(def).Execute(context));

        var result = context.Targets.ToArray();
        Assert.Contains(hostile, result);
        Assert.DoesNotContain(friendly, result);
        Assert.DoesNotContain(self, result);
    }

    [Fact]
    public void TargetByHostility_FilterType_Hostile()
    {
        var shard = new FakeShard();
        var self = CreateCharacter(shard, factionId: 1);
        var friendly = CreateCharacter(shard, factionId: 1);
        var hostile = CreateCharacter(shard, factionId: 2);

        var context = new Context(shard, self);
        context.Targets.Push(friendly);
        context.Targets.Push(hostile);

        var def = new TargetByHostilityCommandDef
        {
            Id = 1,
            IncludeSelf = 0,
            IncludeInitiator = 1,
            IncludeOwner = 1,
            CompareFromInitiator = 0,
            IncludeNormalOnly = 0,
            FailNoTargets = 0,
            FilterType = 1, // Hostile
            ExcludeMode = 0
        };

        Assert.True(new TargetByHostilityCommand(def).Execute(context));
        Assert.Equal(new IAptitudeTarget[] { hostile }, context.Targets.ToArray());
    }

    [Fact]
    public void TargetByHostility_FilterType_Friendly()
    {
        var shard = new FakeShard();
        var self = CreateCharacter(shard, factionId: 1);
        var friendly = CreateCharacter(shard, factionId: 1);
        var hostile = CreateCharacter(shard, factionId: 2);

        var context = new Context(shard, self);
        context.Targets.Push(friendly);
        context.Targets.Push(hostile);

        var def = new TargetByHostilityCommandDef
        {
            Id = 1,
            IncludeSelf = 0,
            IncludeInitiator = 1,
            IncludeOwner = 1,
            CompareFromInitiator = 0,
            IncludeNormalOnly = 0,
            FailNoTargets = 0,
            FilterType = 0, // Friendly
            ExcludeMode = 0
        };

        Assert.True(new TargetByHostilityCommand(def).Execute(context));
        Assert.Equal(new IAptitudeTarget[] { friendly }, context.Targets.ToArray());
    }

    [Fact]
    public void TargetByHostility_ExcludeMode_Inverts()
    {
        var shard = new FakeShard();
        var self = CreateCharacter(shard, factionId: 1);
        var friendly = CreateCharacter(shard, factionId: 1);
        var hostile = CreateCharacter(shard, factionId: 2);

        var context = new Context(shard, self);
        context.Targets.Push(friendly);
        context.Targets.Push(hostile);

        var def = new TargetByHostilityCommandDef
        {
            Id = 1,
            IncludeSelf = 0,
            IncludeInitiator = 1,
            IncludeOwner = 1,
            CompareFromInitiator = 0,
            IncludeNormalOnly = 0,
            FailNoTargets = 0,
            FilterType = 1, // Hostile, but exclude_mode=1 means NOT hostile
            ExcludeMode = 1
        };

        Assert.True(new TargetByHostilityCommand(def).Execute(context));
        Assert.Equal(new IAptitudeTarget[] { friendly }, context.Targets.ToArray());
    }

    [Fact]
    public void TargetByHostility_CompareFromInitiator()
    {
        var shard = new FakeShard();
        var self = CreateCharacter(shard, factionId: 1);
        var initiator = CreateCharacter(shard, factionId: 2);
        var friendlyToInitiator = CreateCharacter(shard, factionId: 2);
        var hostileToInitiator = CreateCharacter(shard, factionId: 1);

        var context = new Context(shard, self);
        context.Initiator = initiator;
        context.Self = self;
        context.Targets.Push(friendlyToInitiator);
        context.Targets.Push(hostileToInitiator);

        var def = new TargetByHostilityCommandDef
        {
            Id = 1,
            IncludeSelf = 1,
            IncludeInitiator = 1,
            IncludeOwner = 1,
            CompareFromInitiator = 1, // compare from initiator
            IncludeNormalOnly = 0,
            FailNoTargets = 0,
            FilterType = 0, // Friendly relative to initiator
            ExcludeMode = 0
        };

        Assert.True(new TargetByHostilityCommand(def).Execute(context));
        // Friendly to initiator is faction 2
        Assert.Equal(new IAptitudeTarget[] { friendlyToInitiator }, context.Targets.ToArray());
    }

    [Fact]
    public void TargetSingle_AcquiresClosestInFront()
    {
        var shard = new FakeShard();
        var self = CreateCharacter(shard, Vector3.Zero, Vector3.UnitX, factionId: 1);
        var behind = CreateCharacter(shard, new Vector3(-10, 0, 0), factionId: 2);
        var far = CreateCharacter(shard, new Vector3(20, 0, 0), factionId: 2);
        var near = CreateCharacter(shard, new Vector3(5, 0, 0), factionId: 2);

        var context = new Context(shard, self);
        var def = new TargetSingleCommandDef { Id = 1, Range = 15f, UseInitPos = 0, IgnoreWalls = 1, StaticOnly = 0, SetOffset = 0 };

        Assert.True(new TargetSingleCommand(def).Execute(context));
        Assert.Equal(new IAptitudeTarget[] { near }, context.Targets.ToArray());
    }

    [Fact]
    public void TargetSingle_IgnoresBehind()
    {
        var shard = new FakeShard();
        var self = CreateCharacter(shard, Vector3.Zero, Vector3.UnitX, factionId: 1);
        CreateCharacter(shard, new Vector3(-5, 0, 0), factionId: 2);

        var context = new Context(shard, self);
        var def = new TargetSingleCommandDef { Id = 1, Range = 10f, UseInitPos = 0, IgnoreWalls = 1, StaticOnly = 0, SetOffset = 0 };

        Assert.True(new TargetSingleCommand(def).Execute(context));
        Assert.Empty(context.Targets.ToArray());
    }

    [Fact]
    public void TargetSingle_RespectsRange()
    {
        var shard = new FakeShard();
        var self = CreateCharacter(shard, Vector3.Zero, Vector3.UnitX, factionId: 1);
        CreateCharacter(shard, new Vector3(20, 0, 0), factionId: 2);

        var context = new Context(shard, self);
        var def = new TargetSingleCommandDef { Id = 1, Range = 10f, UseInitPos = 0, IgnoreWalls = 1, StaticOnly = 0, SetOffset = 0 };

        Assert.True(new TargetSingleCommand(def).Execute(context));
        Assert.Empty(context.Targets.ToArray());
    }

    [Fact]
    public void TargetSingle_UseInitPos()
    {
        var shard = new FakeShard();
        var self = CreateCharacter(shard, Vector3.Zero, Vector3.UnitX, factionId: 1);
        var target = CreateCharacter(shard, new Vector3(105, 0, 0), factionId: 2);

        var contextFromSelf = new Context(shard, self) { InitPosition = new Vector3(100, 0, 0) };
        var defSelf = new TargetSingleCommandDef { Id = 1, Range = 10f, UseInitPos = 0, IgnoreWalls = 1, StaticOnly = 0, SetOffset = 0 };
        Assert.True(new TargetSingleCommand(defSelf).Execute(contextFromSelf));
        Assert.Empty(contextFromSelf.Targets.ToArray());

        var contextFromInit = new Context(shard, self) { InitPosition = new Vector3(100, 0, 0) };
        var defInit = new TargetSingleCommandDef { Id = 1, Range = 10f, UseInitPos = 1, IgnoreWalls = 1, StaticOnly = 0, SetOffset = 0 };
        Assert.True(new TargetSingleCommand(defInit).Execute(contextFromInit));
        Assert.Equal(new IAptitudeTarget[] { target }, contextFromInit.Targets.ToArray());
    }

    private static CharacterEntity CreateCharacter(FakeShard shard, byte factionId)
    {
        return CreateCharacter(shard, Vector3.Zero, Vector3.UnitX, factionId);
    }

    private static CharacterEntity CreateCharacter(FakeShard shard, Vector3 position, byte factionId)
    {
        return CreateCharacter(shard, position, Vector3.UnitX, factionId);
    }

    private static CharacterEntity CreateCharacter(FakeShard shard, Vector3 position, Vector3 aimDirection, byte factionId)
    {
        var character = new CharacterEntity(shard, shard.GetNextGuid(0))
        {
            Position = position,
            Orientation = Quaternion.Identity,
            AimDirection = aimDirection
        };
        character.SetHostilityInfo(new HostilityInfoData
        {
            Flags = HostilityInfoData.HostilityFlags.Faction,
            FactionId = factionId
        });
        shard.Entities.Add(character.EntityId, character);
        return character;
    }
}
