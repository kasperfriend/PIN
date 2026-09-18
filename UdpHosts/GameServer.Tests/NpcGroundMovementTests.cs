using System.Numerics;
using GameServer.Systems.Ai;
using GameServer.Tests.Fakes;
using Xunit;

namespace GameServer.Tests;

public class NpcGroundMovementTests
{
    [Fact]
    public void ChecksTheInteriorOfAStepNotOnlyTheDestination()
    {
        NpcGroundSurface? Ground(Vector3 p) => p.X > 0.7f && p.X < 1.3f ? null : new(p, Vector3.UnitZ);
        Assert.False(NpcGroundMovement.TryStep(Vector3.Zero, new Vector3(2f, 0f, 0f), Ground, (_, _) => false, null, out var result));
        Assert.Equal(Vector3.Zero, result);
    }

    [Theory]
    [InlineData(-100f)]
    [InlineData(-1.26f)]
    [InlineData(1.26f)]
    [InlineData(10f)]
    [InlineData(float.NaN)]
    public void NoTeleportToAFloorAboveOrBelowTheAgent(float height)
    {
        Assert.False(NpcGroundMovement.TryStep(Vector3.Zero, Vector3.UnitX,
            p => new NpcGroundSurface(new Vector3(p.X, p.Y, height), Vector3.UnitZ),
            (_, _) => false, null, out var result));
        Assert.Equal(Vector3.Zero, result);
    }

    [Fact]
    public void GentleSlopeFollowsTheGround()
    {
        Assert.True(NpcGroundMovement.TryStep(Vector3.Zero, new Vector3(2f, 0f, 0f),
            p => new NpcGroundSurface(new Vector3(p.X, p.Y, p.X * 0.2f), Vector3.Normalize(new Vector3(-0.2f, 0f, 1f))),
            (_, _) => false, null, out var result));
        Assert.Equal(new Vector3(2f, 0f, 0.4f), result);
    }

    [Fact]
    public void WalkableGroundCountsWhicheverWayItsTrianglesFace()
    {
        // The zone's baked collision carries both face windings, so the same walkable ground
        // reports an up-facing normal where a downward ray reaches it and a down-facing one where
        // only an upward ray does (see PhysicsEngine.TryGetGroundSurface).
        Assert.True(NpcGroundMovement.TryStep(Vector3.Zero, new Vector3(2f, 0f, 0f),
            p => new NpcGroundSurface(p, -Vector3.UnitZ), (_, _) => false, null, out var result));
        Assert.Equal(new Vector3(2f, 0f, 0f), result);

        // What makes a surface unwalkable is being steep - and both signs of a steep normal are
        // equally steep.
        Assert.False(NpcGroundMovement.TryStep(Vector3.Zero, Vector3.UnitX,
            p => new NpcGroundSurface(p, new Vector3(1f, 0f, -0.2f)), (_, _) => false, null, out _));
    }

    [Fact]
    public void SteepSurfacesWallsAndExcludedRegionsAllRejectMovement()
    {
        Assert.False(NpcGroundMovement.TryStep(Vector3.Zero, Vector3.UnitX,
            p => new NpcGroundSurface(p, new Vector3(1f, 0f, 0.2f)), (_, _) => false, null, out _));
        Assert.False(NpcGroundMovement.TryStep(Vector3.Zero, Vector3.UnitX,
            p => new NpcGroundSurface(p, Vector3.UnitZ), (_, _) => true, null, out _));
        Assert.False(NpcGroundMovement.TryStep(Vector3.Zero, Vector3.UnitX,
            p => new NpcGroundSurface(p, Vector3.UnitZ), (_, _) => false, p => p.X >= 0.5f, out _));
    }

    [Theory]
    [InlineData(float.NaN)]
    [InlineData(float.PositiveInfinity)]
    [InlineData(1e30f)]
    public void MalformedOrUnboundedStepsDoNoQueries(float x)
    {
        int queries = 0;
        Assert.False(NpcGroundMovement.TryStep(Vector3.Zero, new Vector3(x, 0f, 0f),
            p => { queries++; return new NpcGroundSurface(p, Vector3.UnitZ); }, (_, _) => false, null, out _));
        Assert.Equal(0, queries);
    }

    [Fact]
    public void NoMapAdapterRetainsCombatDevelopmentModeButDoesNotInventAmbientGround()
    {
        var navigation = new PhysicsNpcNavigation(new FakeShard(), new StandardAiRules());
        Assert.False(navigation.SupportsRoutines);
        var agent = new NpcNavigationAgent(1, 0.7f, 1.8f);
        Assert.NotEmpty(navigation.FindPath(Vector3.Zero, new Vector3(10f, 0f, 0f), agent));
        Assert.True(navigation.TryStep(Vector3.Zero, Vector3.UnitX, agent, out var result));
        Assert.Equal(Vector3.UnitX, result);
    }
}
