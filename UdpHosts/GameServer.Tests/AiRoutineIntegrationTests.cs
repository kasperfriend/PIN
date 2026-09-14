using System;
using System.Collections.Generic;
using System.Numerics;
using System.Threading;
using AeroMessages.GSS.Character;
using GameServer.Entities.Character;
using GameServer.Entities.Deployable;
using GameServer.StaticDB.Records.aptfs;
using GameServer.StaticDB.Records.dbcharacter;
using GameServer.Systems.Ai;
using GameServer.Systems.Aptitude;
using GameServer.Systems.Aptitude.Commands.Movement;
using GameServer.Systems.Emotes;
using GameServer.Tests.Fakes;
using Xunit;

namespace GameServer.Tests;

public class AiRoutineIntegrationTests
{
    private const ulong Start = 60_000;
    private static readonly NpcRoutineRules Immediate = new() { RestMinMs = 0, RestMaxMs = 0 };

    [Theory]
    [InlineData("Wander", 0.1f, (short)0x5004)]
    [InlineData("FastWander", 0.5f, (short)0x2004)]
    [InlineData("Wander(walk=false)", 0.5f, (short)0x2004)]
    public void IdleNpcMovesWithItsDatabaseSpeedAndLocomotionState(string behavior, float distance, short state)
    {
        var (shard, npc, navigation) = Create(behavior);
        Tick(shard, Start);
        Assert.Equal(distance, npc.Position.Length(), 4);
        Assert.Equal(state, npc.MovementState);
        Assert.Equal(NpcRoutineState.Walking, shard.AI.GetRoutineState(npc.EntityId));
        Assert.Single(navigation.Requests);
    }

    [Fact]
    public void RoutineArrivalDoesNotUseTheWeaponsCombatStandoff()
    {
        var (shard, npc, navigation) = Create("Wander");
        Tick(shard, Start);
        var goal = navigation.Requests[0].Goal;
        for (ulong now = Start + 50; now < Start + 10_000; now += 50)
        {
            Tick(shard, now);
            if (shard.AI.GetRoutineState(npc.EntityId) == NpcRoutineState.Waiting)
            {
                break;
            }
        }

        Assert.InRange(Vector3.Distance(npc.Position, goal), 0f, NpcRoutine.ArrivalRadius);
        Assert.Equal((short)0x1000, npc.MovementState);
    }

    [Fact]
    public void SuccessfulAmbientPathIsCachedRatherThanRebuiltEveryCombatReplanInterval()
    {
        var (shard, _, navigation) = Create("Wander");
        for (ulong now = Start; now <= Start + 1000; now += 50)
        {
            Tick(shard, now);
        }

        Assert.Single(navigation.Requests);
    }

    [Fact]
    public void EmptyPathOrRejectedStepDoesNotMoveOrPretendToWalk()
    {
        var (shard, npc, navigation) = Create("Wander");
        navigation.Reachable = false;
        Tick(shard, Start);
        Assert.Equal(Vector3.Zero, npc.Position);
        Assert.Equal((short)0x1000, npc.MovementState);
        for (ulong now = Start + 50; now < Start + 2000; now += 50)
        {
            Tick(shard, now);
        }

        Assert.Single(navigation.Requests);
        navigation.Reachable = true;
        navigation.StepAllowed = false;
        Tick(shard, Start + 2000);
        Assert.Equal(Vector3.Zero, npc.Position);
        Assert.Equal((short)0x1000, npc.MovementState);
    }

    [Fact]
    public void StaticBodiesDoNotChaseAndUnknownRouteBodiesDoNotRoam()
    {
        var (shard, npc, navigation) = Create("Stand");
        var player = AddCharacter(shard, new Vector3(20f, 0f, 0f));
        shard.AI.Aggro(npc.EntityId, player.EntityId);
        Tick(shard, Start);
        Assert.Equal(Vector3.Zero, npc.Position);
        Assert.Empty(navigation.Requests);

        var (other, follower, routeNavigation) = Create("OneOff_FollowRoute");
        Tick(other, Start);
        Assert.Equal(NpcRoutineState.MissingRoute, other.AI.GetRoutineState(follower.EntityId));
        Assert.Empty(routeNavigation.Requests);
    }

    [Fact]
    public void CombatInterruptsTheRoutineAndUsesTheOffensiveWalkFlag()
    {
        var (shard, npc, navigation) = Create("Wander", "Attack(combatWalk=true)");
        Tick(shard, Start);
        var before = npc.Position;
        var player = AddCharacter(shard, new Vector3(20f, 0f, 0f));
        shard.AI.Aggro(npc.EntityId, player.EntityId);
        Tick(shard, Start + 50);
        Assert.Equal(NpcRoutineState.Suspended, shard.AI.GetRoutineState(npc.EntityId));
        Assert.Equal((short)0x5004, npc.MovementState);
        Assert.Equal(0.1f, Vector3.Distance(before, npc.Position), 4);
        Assert.Equal(player.Position, navigation.Requests[^1].Goal);
        shard.Entities.Remove(player.EntityId);
        Tick(shard, Start + 100);
        // Already close to home, so the routine can resume without a long return leg.
        Assert.NotEqual(NpcRoutineState.Suspended, shard.AI.GetRoutineState(npc.EntityId));
    }

    [Fact]
    public void ExplicitLeashDistanceIsUsedByTheCombatBrain()
    {
        var (shard, npc, navigation) = Create("Wander(leashDistance=10)");
        npc.SetPosition(new Vector3(20f, 0f, 0f));
        Tick(shard, Start);
        Assert.Equal(AiBrainState.Return, shard.AI.GetState(npc.EntityId));
        Assert.Equal(Vector3.Zero, navigation.Requests[0].Goal);
    }

    [Fact]
    public void NewAmbientSearchesAreBoundedPerTickWithoutStarvingThePopulation()
    {
        var (shard, _, navigation) = Create("Wander");
        for (int i = 1; i < 20; i++)
        {
            var npc = AddCharacter(shard, Vector3.Zero);
            Assert.True(shard.AI.Register(npc));
        }

        for (ulong now = Start; now < Start + 250; now += 50)
        {
            int previous = navigation.Requests.Count;
            Tick(shard, now);
            Assert.InRange(navigation.Requests.Count - previous, 0, 4);
        }

        Assert.Equal(20, navigation.Requests.Count);
    }

    [Fact]
    public void ShardDowntimeDoesNotBecomeOneHugeMovementStep()
    {
        var (shard, npc, _) = Create("FastWander");
        Tick(shard, Start);
        var before = npc.Position;
        shard.AI.Enabled = false;
        Tick(shard, Start + 30_000);
        Assert.Equal(before, npc.Position);
        shard.AI.Enabled = true;
        Tick(shard, Start + 60_000);
        Assert.InRange(Vector3.Distance(before, npc.Position), 0f, 2.501f);
    }

    [Fact]
    public void AnIdleEmoteStopsForTravelAndReturnsDuringThePause()
    {
        var (shard, npc, _) = Create("Wander(emote=\"calm\",restDurationMin=1000,restDurationMax=1000)");
        Tick(shard, Start);
        Assert.Equal((ushort)1062, npc.Emote.Id);
        Tick(shard, Start + 1000);
        Assert.Equal((ushort)0, npc.Emote.Id);
        for (ulong now = Start + 1050; now < Start + 10_000; now += 50)
        {
            Tick(shard, now);
            if (shard.AI.GetRoutineState(npc.EntityId) == NpcRoutineState.Waiting)
            {
                break;
            }
        }

        Assert.Equal((ushort)1062, npc.Emote.Id);
    }

    [Fact]
    public void MovementFlagsAndAptitudeSlideOwnTheBodyInsteadOfTheRoutine()
    {
        var (shard, npc, _) = Create("Wander");
        npc.SetCombatFlags(new CombatFlagsData { Value = CombatFlagsData.CharacterCombatFlags.restrict_movement });
        Tick(shard, Start);
        Assert.Equal(Vector3.Zero, npc.Position);
        npc.SetCombatFlags(new CombatFlagsData());
        shard.Abilities = new AbilitySystem(shard, new FakeAptitudeFactory(shard));
        var command = new MovementSlideCommand(new MovementSlideCommandDef { Id = 358560, OffsetX = 5f, MoveDuration = 667 });
        Assert.True(command.Execute(new Context(shard, npc)));
        Tick(shard, Start + 50);
        Assert.Equal(Vector3.Zero, npc.Position); // the AI never integrates the ability's slide itself
        shard.Abilities.ProcessTarget(npc, Start + 333);
        var slid = npc.Position;
        Tick(shard, Start + 350);
        Assert.Equal(slid, npc.Position);
    }

    [Fact]
    public void WorkingNpcUsesStationPoseHolstersAndRestoresItsWeaponWhenInterrupted()
    {
        var (shard, npc, _) = Create("PeacetimeCityWanderer(restFunction=\"Work\")", station: true);
        npc.SetWeaponIndex(new WeaponIndexData { Index = 2, Unk1 = 1, Time = (uint)Start });
        for (ulong now = Start; now < Start + 2000; now += 50)
        {
            Tick(shard, now);
            if (shard.AI.GetRoutineState(npc.EntityId) == NpcRoutineState.Working)
            {
                break;
            }
        }

        Assert.Equal(NpcRoutineState.Working, shard.AI.GetRoutineState(npc.EntityId));
        Assert.Equal((ushort)60, npc.Emote.Id);
        Assert.Equal((byte)0, npc.WeaponIndex.Index);
        Assert.Equal(AiVectors.OrientationFacing(Vector3.UnitX), npc.Orientation);
        var target = AddCharacter(shard, new Vector3(20f, 0f, 0f));
        shard.AI.Aggro(npc.EntityId, target.EntityId);
        Tick(shard, shard.CurrentTimeLong + 50);
        Assert.Equal((byte)2, npc.WeaponIndex.Index);
        Assert.Equal((ushort)0, npc.Emote.Id);
    }

    [Theory]
    [InlineData("unregister")]
    [InlineData("death")]
    [InlineData("remove")]
    [InlineData("clear")]
    public void CleanupReleasesWorkLocationsForOtherNpcs(string cleanup)
    {
        var (shard, npc, _) = Create("UseWorkDeployables(function=\"Work\")", station: true);
        Tick(shard, Start); // reserved, on the way
        switch (cleanup)
        {
            case "unregister": shard.AI.Unregister(npc.EntityId); break;
            case "death": shard.CharacterLifecycle.ForceDeath(npc); break;
            case "remove": shard.Entities.Remove(npc.EntityId); break;
            case "clear": shard.AI.Clear(); break;
        }

        Tick(shard, Start + 50);
        var next = AddCharacter(shard, Vector3.Zero);
        Assert.True(shard.AI.Register(next));
        Tick(shard, Start + 100);
        Assert.Equal(NpcRoutineState.Walking, shard.AI.GetRoutineState(next.EntityId));
        Assert.NotEqual(Vector3.Zero, next.Position);
    }

    private static (FakeShard Shard, CharacterEntity Npc, Navigation Navigation) Create(
        string behavior, string offensive = "", bool station = false)
    {
        var shard = new FakeShard();
        var navigation = new Navigation();
        var emotes = new EmoteService(new FakeEmoteDataSource());
        var activities = new SdbNpcActivityWorld(shard, emotes,
            deployables: id => id == 116 ? new Deployable
            {
                Id = 116, Function = 1, Behavior = "DynamicEmoteHolstered(emote=\"utility\",emoteDuration=3000)",
            } : null,
            functions: id => id == 1 ? new DeployableFunction { Id = 1, Name = "Work" } : null);
        if (station)
        {
            var point = new DeployableEntity(shard, shard.GetNextGuid(), 116, 0);
            point.SetPosition(new Vector3(2f, 0f, 0f));
            point.SetOrientation(AiVectors.OrientationFacing(Vector3.UnitX));
            shard.Entities[point.EntityId] = point;
        }

        shard.AI = new AiEngine(shard, shard.EventBus,
            hostility: new NeverHostileAiHostility(), feedback: shard.AiAttackFeedback,
            monsterStats: new FakeAiMonsterStats(normalSpeed: 2f, fastSpeed: 10f) { Behavior = behavior, OffensiveBehavior = offensive },
            emotes: emotes, navigation: navigation, activities: activities, routineRules: Immediate);
        var npc = AddCharacter(shard, Vector3.Zero);
        Assert.True(shard.AI.Register(npc));
        return (shard, npc, navigation);
    }

    private static CharacterEntity AddCharacter(FakeShard shard, Vector3 position)
    {
        var entity = new CharacterEntity(shard, shard.GetNextGuid());
        entity.SetPosition(position);
        entity.SetCharacterState(CharacterStateData.CharacterStatus.Living, 0);
        entity.SetMaxHealth(100_000, resetCurrent: true);
        shard.Entities[entity.EntityId] = entity;
        return entity;
    }

    private static void Tick(FakeShard shard, ulong now)
    {
        shard.CurrentTimeLong = now;
        shard.AI.Tick(0.05, now, CancellationToken.None);
    }

    private sealed class Navigation : INpcNavigation
    {
        public bool SupportsRoutines => true;
        public bool Reachable { get; set; } = true;
        public bool StepAllowed { get; set; } = true;
        public List<(Vector3 Start, Vector3 Goal)> Requests { get; } = [];
        public IReadOnlyList<Vector3> FindPath(Vector3 start, Vector3 goal, NpcNavigationAgent agent)
        {
            Requests.Add((start, goal));
            return Reachable ? new[] { goal } : Array.Empty<Vector3>();
        }

        public bool TryStep(Vector3 from, Vector3 desired, NpcNavigationAgent agent, out Vector3 position)
        {
            position = desired;
            return StepAllowed;
        }
    }
}
