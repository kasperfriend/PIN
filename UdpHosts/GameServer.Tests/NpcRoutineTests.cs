using System;
using System.Collections.Generic;
using System.Numerics;
using GameServer.Systems.Ai;
using Xunit;

namespace GameServer.Tests;

public class NpcRoutineTests
{
    private static readonly NpcRoutineRules Immediate = new() { RestMinMs = 0, RestMaxMs = 0 };

    [Theory]
    [InlineData("Wander")]
    [InlineData("Wander(nearSpawn=true)")]
    [InlineData("FastWander(distance=35)")]
    [InlineData("SwarmWanderer(wanderDistance=10,maxDistFromSpawn=30)")]
    public void RepeatedWanderingStaysWithinItsHomeAndPerLegBounds(string behavior)
    {
        var home = new Vector3(100f, -50f, 12f);
        var routine = Create(behavior, home: home);
        var position = home;
        for (ulong now = 0; now < 20_000; now += 100)
        {
            routine.Update(now, position, true, true, true);
            if (!routine.Goal.HasValue)
            {
                continue;
            }

            var goal = routine.Goal.Value;
            Assert.InRange(AiVectors.HorizontalDistance(home, goal), 0f, routine.Profile.HomeRadius + 0.0001f);
            Assert.InRange(AiVectors.HorizontalDistance(position, goal), 0f, routine.Profile.WanderDistance + 0.0001f);
            Assert.Equal(home.Z, goal.Z);
            position = goal; // simulate successful navigation, not a scheduler-owned teleport
        }
    }

    [Fact]
    public void SameIdentityReplaysButControllerByteAloneDoesNotSynchronizeNpcs()
    {
        var first = Create(id: 0x123400);
        var replay = Create(id: 0x123400);
        var other = Create(id: 0x123500);
        foreach (var routine in new[] { first, replay, other })
        {
            routine.Update(0, Vector3.Zero, true, true, true);
        }

        Assert.Equal(first.Goal, replay.Goal);
        Assert.NotEqual(first.Goal, other.Goal);
    }

    [Fact]
    public void ArrivalStartsTheRowPauseAndDoesNotImmediatelySelectAnotherPoint()
    {
        var routine = Create("Wander(restDurationMin=5000,restDurationMax=5000)");
        routine.Update(4999, Vector3.Zero, true, true, true);
        Assert.Null(routine.Goal);
        routine.Update(5000, Vector3.Zero, true, true, true);
        var goal = routine.Goal.Value;
        routine.Update(6000, goal, true, true, true);
        Assert.Equal(NpcRoutineState.Waiting, routine.State);
        Assert.Equal(11000UL, routine.NextActionAt);
        routine.Update(10999, goal, true, true, true);
        Assert.Null(routine.Goal);
        routine.Update(11000, goal, true, true, true);
        Assert.NotNull(routine.Goal);
    }

    [Fact]
    public void CombatCancelsDestinationThenReturnsHomeBeforeResuming()
    {
        var routine = Create();
        routine.Update(0, Vector3.Zero, true, true, true);
        routine.Update(50, new Vector3(20f, 0f, 0f), false, true, true);
        Assert.Null(routine.Goal);
        Assert.Equal(NpcRoutineState.Suspended, routine.State);
        routine.Update(100, new Vector3(20f, 0f, 0f), true, true, true);
        Assert.Equal(NpcRoutineState.Returning, routine.State);
        Assert.Equal(Vector3.Zero, routine.Goal);
        routine.Update(200, Vector3.Zero, true, true, true);
        Assert.Null(routine.Goal);
        routine.Update(250, Vector3.Zero, true, true, true);
        Assert.Equal(NpcRoutineState.Walking, routine.State);
        Assert.NotEqual(Vector3.Zero, routine.Goal);
    }

    [Fact]
    public void AFailedRouteWaitsBeforeTryingAgainAndNeverAdvancesAnEntity()
    {
        var routine = Create();
        routine.Update(0, Vector3.Zero, true, true, true);
        var oldGoal = routine.Goal;
        routine.Blocked(100);
        Assert.Null(routine.Goal);
        routine.Update(2099, Vector3.Zero, true, true, true);
        Assert.Null(routine.Goal);
        routine.Update(2100, Vector3.Zero, true, true, true);
        Assert.NotNull(routine.Goal);
        Assert.NotEqual(oldGoal, routine.Goal);
    }

    [Fact]
    public void NoGroundMeansNoGeneratedTravel()
    {
        var routine = Create();
        routine.Update(0, Vector3.Zero, true, true, false);
        Assert.Null(routine.Goal);
        routine.Update(10_000, Vector3.Zero, true, true, false);
        Assert.Null(routine.Goal);
    }

    [Fact]
    public void MovementRestrictionIsNotMisclassifiedAsAStuckRoute()
    {
        var routine = Create();
        routine.Update(0, Vector3.Zero, true, true, true);
        var goal = routine.Goal;
        routine.Update(9000, Vector3.Zero, true, false, true);
        routine.Update(9050, Vector3.Zero, true, true, true);
        Assert.Equal(goal, routine.Goal);
        routine.Update(17_050, Vector3.Zero, true, true, true);
        Assert.Null(routine.Goal); // truly stuck, eight seconds after the restriction ended
    }

    [Theory]
    [InlineData("Wander(distance=0)")]
    [InlineData("Wander(calmWanderChance=0)")]
    [InlineData("Stand")]
    [InlineData("OneOff_FollowRoute")]
    [InlineData("")]
    public void ExplicitlyImmobileOrMissingDataNeverProducesAGoal(string behavior)
    {
        var routine = Create(behavior);
        for (ulong now = 0; now < 30_000; now += 100)
        {
            routine.Update(now, Vector3.Zero, true, true, true);
            Assert.Null(routine.Goal);
        }
    }

    [Fact]
    public void ProjectedTerrainHeightIsUsedForArrivalButCannotMoveTheRequestedXY()
    {
        var routine = Create();
        routine.Update(0, Vector3.Zero, true, true, true);
        var goal = routine.Goal.Value;
        routine.ProjectGoal(goal + new Vector3(0f, 0f, 3f));
        Assert.Equal(3f, routine.Goal.Value.Z);
        routine.ProjectGoal(goal + new Vector3(100f, 0f, 3f));
        Assert.Equal(goal.X, routine.Goal.Value.X);
        routine.Update(500, routine.Goal.Value, true, true, true);
        Assert.Null(routine.Goal);
    }

    [Fact]
    public void WorkBeginsOnlyOnArrivalUsesMillisecondsAndReleasesAfterCompletion()
    {
        var activities = new Activities();
        var routine = Create("PeacetimeCityWanderer(restFunction=\"Work\")", activities: activities);
        routine.Update(0, Vector3.Zero, true, true, true);
        Assert.Equal(activities.Spot.Position, routine.Goal);
        Assert.False(routine.IsWorking);
        Assert.Equal((ushort)0, routine.EmoteOverride);
        routine.Update(100, activities.Spot.Position, true, true, true);
        Assert.True(routine.IsWorking);
        Assert.True(routine.Holster);
        Assert.Equal((ushort)60, routine.EmoteOverride);
        Assert.Equal(3100UL, routine.NextActionAt);
        routine.Update(3099, activities.Spot.Position, true, true, true);
        Assert.True(routine.IsWorking);
        routine.Update(3100, activities.Spot.Position, true, true, true);
        Assert.False(routine.IsWorking);
        Assert.Equal((ushort)1062, routine.EmoteOverride);
        Assert.Empty(activities.Owners);
    }

    [Theory]
    [InlineData("combat")]
    [InlineData("removed")]
    [InlineData("blocked")]
    [InlineData("stop")]
    [InlineData("displaced")]
    public void EveryInterruptionReleasesItsWorkReservation(string reason)
    {
        var activities = new Activities();
        var routine = Create("UseWorkDeployables(function=\"Work\")", activities: activities);
        routine.Update(0, Vector3.Zero, true, true, true);
        routine.Update(100, activities.Spot.Position, true, true, true);
        Assert.Single(activities.Owners);
        switch (reason)
        {
            case "combat": routine.Suspend(); break;
            case "blocked": routine.Blocked(150); break;
            case "stop": routine.Stop(); break;
            case "removed":
                activities.Valid = false;
                routine.Update(150, activities.Spot.Position, true, true, true);
                break;
            case "displaced": routine.Update(150, new Vector3(30f, 0f, 0f), true, true, true); break;
        }

        Assert.Empty(activities.Owners);
        Assert.False(routine.IsWorking);
        Assert.False(routine.Holster);
    }

    [Fact]
    public void MissingWorkLocationIsNotSynthesizedFromATemplate()
    {
        var routine = Create("UseWorkDeployables(function=\"Work\")");
        routine.Update(0, Vector3.Zero, true, true, true);
        Assert.Null(routine.Goal);
        Assert.Equal(NpcRoutineState.Waiting, routine.State);
    }

    [Fact]
    public void InvalidPositionsAndClockOverflowStaySafe()
    {
        var routine = Create();
        routine.Update(0, new Vector3(float.NaN, 0f, 0f), true, true, true);
        Assert.Null(routine.Goal);
        routine.Blocked(ulong.MaxValue - 10);
        Assert.Equal(ulong.MaxValue, routine.NextActionAt);
    }

    [Fact]
    public void ArrivalCannotStartAnActivityOnTheFloorAbove()
    {
        var world = new Activities { Spot = new NpcActivitySpot(2, new Vector3(0f, 0f, 1f), Quaternion.Identity, 60, 1000, false, 0) };
        var routine = Create("UseWorkDeployables(function=Work)", activities: world);
        routine.Update(0, Vector3.Zero, true, true, true);
        routine.Update(50, Vector3.Zero, true, true, true);
        Assert.Equal(NpcRoutineState.Walking, routine.State);
        Assert.False(routine.IsWorking);
        routine.Update(100, new Vector3(0f, 0f, 1f), true, true, true);
        Assert.True(routine.IsWorking);
    }

    [Fact]
    public void StopIsTerminalUntilANewRoutineIsRegistered()
    {
        var routine = Create();
        routine.Update(0, Vector3.Zero, true, true, true);
        routine.Stop();
        routine.Update(60_000, Vector3.Zero, true, true, true);
        Assert.Null(routine.Goal);
        Assert.Equal(NpcRoutineState.Inactive, routine.State);
    }

    private static NpcRoutine Create(string behavior = "Wander", Vector3 home = default, ulong id = 0x1000, INpcActivityWorld activities = null)
        => new(id, 179, home, NpcRoutineProfile.Resolve(NpcBehaviorParams.Parse(behavior), rules: Immediate), 0, activities);

    private sealed class Activities : INpcActivityWorld
    {
        public NpcActivitySpot Spot { get; set; } = new(12, new Vector3(2f, 0f, 0f), Quaternion.Identity, 60, 3000, true, 1062);
        public HashSet<ulong> Owners { get; } = [];
        public bool Valid { get; set; } = true;
        public bool TryReserve(ulong npcId, string function, Vector3 position, Vector3 home, float radius, ulong previousSpot, out NpcActivitySpot spot)
        {
            spot = Spot;
            return Valid && Owners.Add(npcId);
        }

        public bool IsValid(ulong npcId, in NpcActivitySpot spot) => Valid && Owners.Contains(npcId);
        public void Release(ulong npcId) => Owners.Remove(npcId);
        public void Clear() => Owners.Clear();
    }
}
