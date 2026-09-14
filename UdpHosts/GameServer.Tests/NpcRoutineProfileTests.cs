using System;
using GameServer.Systems.Ai;
using Xunit;

namespace GameServer.Tests;

public class NpcRoutineProfileTests
{
    [Theory]
    [InlineData("Wander")]
    [InlineData("AggressiveWanderer")]
    [InlineData("GuardCityWanderer")]
    [InlineData("PeacetimeCityWanderer")]
    [InlineData("PeacetimeCityWandererWithHealing")]
    [InlineData("SwarmWanderer")]
    [InlineData("BasicCivilian")]
    public void OnlyDeclaredWanderersReceiveCompatibilityRoaming(string text)
    {
        var profile = Resolve(text);
        Assert.Equal(NpcRoutineKind.Wander, profile.Kind);
        Assert.True(profile.HasRoutine);
        Assert.True(profile.Walk);
        Assert.Equal(10f, profile.WanderDistance);
    }

    [Theory]
    [InlineData(null, NpcRoutineKind.Unspecified)]
    [InlineData("", NpcRoutineKind.Unspecified)]
    [InlineData("SomeFutureWanderer", NpcRoutineKind.Unspecified)]
    [InlineData("Arch_MedRangedHumanoid_Base", NpcRoutineKind.Unspecified)]
    [InlineData("AlertAndInteractive(emote=\"typing01\")", NpcRoutineKind.Unspecified)]
    [InlineData("StationaryCivilianDialog", NpcRoutineKind.Stationary)]
    [InlineData("BasicCivilian_Stationary", NpcRoutineKind.Stationary)]
    [InlineData("Stand", NpcRoutineKind.Stationary)]
    [InlineData("StayAtSpawn", NpcRoutineKind.Stationary)]
    [InlineData("Null", NpcRoutineKind.Stationary)]
    [InlineData("Wander(stationary=true)", NpcRoutineKind.Stationary)]
    public void NoGuessedPatrolForUnspecifiedOrStationaryTrees(string text, NpcRoutineKind kind)
    {
        var profile = Resolve(text);
        Assert.Equal(kind, profile.Kind);
        Assert.False(profile.HasRoutine);
    }

    [Theory]
    [InlineData("StockShootAndFollowRoute")]
    [InlineData("OneOff_FollowRoute")]
    [InlineData("NavigateToLocation")]
    [InlineData("Arch_Follower")]
    [InlineData("PeacetimeCityWanderer(city_prefix=\"WanderPoint\",flee_prefix=\"Flee City\")")]
    [InlineData("UseWorkDeployables(function=\"Rummage\",groundOffset=1.6,inSpawnVolume=true,climber=true)")]
    public void ExternalRouteAndLocomotionRequirementsAreNotInvented(string text)
    {
        var profile = Resolve(text);
        Assert.Equal(NpcRoutineKind.ExternalRoute, profile.Kind);
        Assert.NotEmpty(profile.MissingData);
        Assert.False(profile.HasRoutine);
    }

    [Fact]
    public void ActualSwarmRowCarriesItsDistanceHomeBoundAndIdleWait()
    {
        var profile = Resolve("SwarmWanderer(ability=\"Leap\", chanceAbilityAttack=0.00, maxDistFromSpawn=30, maxDistJitter=5, swarmRadiusMax=10, swarmRadiusMin=8, maxMeleeCombatBursts=3, attackLimit=3, idleEmoteMinTime=7000, idleEmoteMaxTime=12000, wanderDistance=10)");
        Assert.Equal(30f, profile.HomeRadius);
        Assert.Equal(5f, profile.MaxDistJitter);
        Assert.Equal(8f, profile.SwarmRadiusMin);
        Assert.Equal(10f, profile.SwarmRadiusMax);
        Assert.Equal(10f, profile.WanderDistance);
        Assert.Equal(7000, profile.RestMinMs);
        Assert.Equal(12000, profile.RestMaxMs);
    }

    [Fact]
    public void MaxDistJitterIsStoredSeparatelyFromHomeRadius()
    {
        var noJitter = Resolve("Wander(maxDistFromSpawn=30, wanderDistance=10)");
        Assert.Equal(30f, noJitter.HomeRadius);
        Assert.Equal(0f, noJitter.MaxDistJitter);
        var withJitter = Resolve("Wander(maxDistFromSpawn=30, maxDistJitter=5, wanderDistance=10)");
        Assert.Equal(30f, withJitter.HomeRadius);
        Assert.Equal(5f, withJitter.MaxDistJitter);
    }

    [Fact]
    public void DespawnFlagsAreReadWithoutInventingBehavior()
    {
        var profile = Resolve("Wander(despawnWhenStuck=1, despawnDist=45)");
        Assert.True(profile.DespawnWhenStuck);
        Assert.Equal(45f, profile.DespawnDistance);
        Assert.False(Resolve("Wander").DespawnWhenStuck);
        Assert.Equal(0f, Resolve("Wander").DespawnDistance);
    }

    [Fact]
    public void LeashToSpawnFlagIsPreserved()
    {
        Assert.True(Resolve("Wander(leashToSpawn=1)").LeashToSpawn);
        Assert.False(Resolve("Wander").LeashToSpawn);
    }

    [Fact]
    public void FastWanderCoreHonorsZeroRestAndNumericWalkFlag()
    {
        var profile = Resolve("FastWanderCore(restDurationMin=0,restDurationMax=200,nearSpawn=1,walk=0)");
        Assert.False(profile.Walk);
        Assert.True(profile.NearSpawn);
        Assert.Equal(0, profile.RestMinMs);
        Assert.Equal(200, profile.RestMaxMs);
        Assert.Equal(profile.WanderDistance, profile.HomeRadius);
    }

    [Fact]
    public void VocalizedWanderKeepsItsOwnRadiusAndRestRatherThanCombatStandoff()
    {
        var profile = Resolve("WanderWithEmoteVocalized(restDurationMin = 5000, restDurationMax = 9000, distance = 25, walk = false, walkInRoute = false, emote = \"fear\", frequency = 10000, abstracted = true, nearSpawn = true)");
        Assert.False(profile.Walk);
        Assert.Equal(25f, profile.HomeRadius);
        Assert.Equal(25f, profile.WanderDistance);
        Assert.Equal(5000, profile.RestMinMs);
        Assert.Equal(9000, profile.RestMaxMs);
    }

    [Fact]
    public void ExplicitZeroDisablesWanderingAndNoNestedChildParameterLeaks()
    {
        Assert.Equal(0f, Resolve("Wander(wanderDistance=0)").WanderDistance);
        Assert.Equal(0f, Resolve("Wander(calmWanderChance=0)").WanderChance);
        var profile = Resolve("Wander(child=Other(distance=999, walk=false), distance=12, walk=true)");
        Assert.Equal(12f, profile.WanderDistance);
        Assert.True(profile.Walk);
    }

    [Fact]
    public void OffensiveCombatWalkOverridesBaseButDoesNotChangeCalmWalking()
    {
        var profile = NpcRoutineProfile.Resolve(
            NpcBehaviorParams.Parse("Wander(calmWalk=true,combatWalk=false,leashWalk=true)"),
            NpcBehaviorParams.Parse("Attack(combatWalk=true)"));
        Assert.True(profile.Walk);
        Assert.Equal(true, profile.CombatWalk);
        Assert.Equal(true, profile.LeashWalk);
    }

    [Fact]
    public void ExplicitDistanceIsNotReducedByTheCompatibilityHomeDefault()
    {
        var profile = Resolve("FastWander(distance=35.0)");
        Assert.Equal(35f, profile.WanderDistance);
        Assert.Equal(35f, profile.HomeRadius);
        profile = Resolve("EliteStationary(wanderDistance=10,leashDistance=40)");
        Assert.Equal(NpcRoutineKind.Wander, profile.Kind);
        Assert.Equal(10f, profile.WanderDistance);
        Assert.Equal(40f, profile.LeashDistance);
    }

    [Fact]
    public void WorkFunctionUsesTheDatabaseNameNotTheVisualName()
    {
        Assert.Equal("Repair Work", Resolve("PeacetimeCityWanderer(restFunction=\"Repair Work\")").WorkFunction);
        var worker = Resolve("UseWorkDeployables(function=\"Rummage\")");
        Assert.Equal(NpcRoutineKind.Work, worker.Kind);
        Assert.Equal("Rummage", worker.WorkFunction);
    }

    [Fact]
    public void AllNumericInputsAreFiniteBoundedAndOrdered()
    {
        var profile = Resolve("Wander(distance=NaN,maxDistFromSpawn=Infinity,restDurationMin=9000,restDurationMax=1,calmWanderChance=Infinity)");
        Assert.Equal(10f, profile.WanderDistance);
        Assert.Equal(30f, profile.HomeRadius);
        Assert.Equal(9000, profile.RestMaxMs);
        Assert.Equal(1f, profile.WanderChance);

        profile = Resolve("Wander(distance=1e30,maxDistFromSpawn=1e35,leashDist=40)");
        Assert.Equal(40f, profile.HomeRadius);
        Assert.Equal(40f, profile.WanderDistance);
        Assert.Equal(40f, profile.LeashDistance);
    }

    private static NpcRoutineProfile Resolve(string text) => NpcRoutineProfile.Resolve(NpcBehaviorParams.Parse(text));
}
