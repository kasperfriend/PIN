using System;
using System.Numerics;
using GameServer.Systems.Ai;
using Xunit;

namespace GameServer.Tests;

public class NpcPathfinderTests
{
    private static Vector3? FlatGround(Vector3 point) => new(point.X, point.Y, 0f);

    [Fact]
    public void ClearGroundUsesTheDirectRoute()
    {
        var path = NpcPathfinder.FindPath(
            Vector3.Zero,
            new Vector3(10f, 0f, 0f),
            FlatGround,
            (_, _) => false);

        var point = Assert.Single(path);
        Assert.Equal(new Vector3(10f, 0f, 0f), point);
    }

    [Fact]
    public void SearchesAroundAStaticWall()
    {
        var path = NpcPathfinder.FindPath(
            Vector3.Zero,
            new Vector3(10f, 0f, 0f),
            FlatGround,
            WallBlocksCenterLine,
            new NpcPathfinder.Options(CellSize: 2f, MaxSearchDistance: 20f));

        Assert.True(path.Count > 1, "a blocked direct route should produce intermediate waypoints");
        Assert.All(path, point => Assert.True(MathF.Abs(point.Y) >= 1.9f || point.X <= 3f || point.X >= 7f));

        var previous = Vector3.Zero;
        foreach (var point in path)
        {
            Assert.False(WallBlocksCenterLine(previous, point));
            previous = point;
        }
    }

    [Fact]
    public void DoesNotInventAClimbHigherThanTheStepLimit()
    {
        Vector3? GroundWithCliff(Vector3 point)
            => new(point.X, point.Y, point.X >= 4f ? 2f : 0f);

        var path = NpcPathfinder.FindPath(
            Vector3.Zero,
            new Vector3(10f, 0f, 2f),
            GroundWithCliff,
            (_, _) => true,
            new NpcPathfinder.Options(MaxSearchDistance: 20f));

        Assert.Empty(path);
    }

    [Fact]
    public void PathingCostPrefersAnUnblockedLongerRoute()
    {
        var path = NpcPathfinder.FindPath(
            Vector3.Zero,
            new Vector3(10f, 0f, 0f),
            FlatGround,
            (_, _) => false,
            new NpcPathfinder.Options(CellSize: 2f, MaxSearchDistance: 20f),
            point => point.X is >= 3f and <= 7f && MathF.Abs(point.Y) < 0.1f ? 100f : 1f,
            null);

        Assert.NotEmpty(path);
        Assert.Contains(path, point => MathF.Abs(point.Y) > 0.1f);
    }

    [Fact]
    public void ExcludedPathingRegionIsNeverUsed()
    {
        var path = NpcPathfinder.FindPath(
            Vector3.Zero,
            new Vector3(10f, 0f, 0f),
            FlatGround,
            (_, _) => false,
            new NpcPathfinder.Options(CellSize: 2f, MaxSearchDistance: 20f),
            null,
            point => point.X is >= 3f and <= 7f && MathF.Abs(point.Y) < 0.1f);

        Assert.NotEmpty(path);
        Assert.Contains(path, point => MathF.Abs(point.Y) > 0.1f);
    }

    [Fact]
    public void DirectRouteMustNotCrossAnUnsampledHole()
    {
        Vector3? Ground(Vector3 point) => point.X is > 3f and < 7f && MathF.Abs(point.Y) < 1f
            ? null : new Vector3(point.X, point.Y, 0f);
        var path = NpcPathfinder.FindPath(Vector3.Zero, new Vector3(10f, 0f, 0f),
            Ground, (_, _) => false);

        Assert.NotEmpty(path);
        Assert.Contains(path, point => MathF.Abs(point.Y) >= 1f);
        var previous = Vector3.Zero;
        foreach (var point in path)
        {
            Assert.True(NpcGroundMovement.TryStep(previous, point,
                probe => Ground(probe) is { } ground ? new NpcGroundSurface(ground, Vector3.UnitZ) : null,
                (_, _) => false, null, out previous));
        }
    }

    [Fact]
    public void GradualSlopeMayClimbMoreThanOneStepOverTheWholeJourney()
    {
        Vector3? Ground(Vector3 point) => new(point.X, point.Y, point.X * 0.3f);
        var path = NpcPathfinder.FindPath(Vector3.Zero, new Vector3(10f, 0f, 3f),
            Ground, (_, _) => false);

        Assert.Equal(new Vector3(10f, 0f, 3f), Assert.Single(path));
    }

    [Fact]
    public void SearchCarriesHeightForwardOnABoundedGroundProbe()
    {
        Vector3? Ground(Vector3 point)
        {
            float z = point.X * 0.3f;
            return MathF.Abs(z - point.Z) <= 1.25f ? new Vector3(point.X, point.Y, z) : null;
        }

        var path = NpcPathfinder.FindPath(Vector3.Zero, new Vector3(12f, 0f, 3.6f),
            Ground, WallBlocksCenterLine);
        Assert.NotEmpty(path);
        Assert.InRange(path[^1].Z, 3.59f, 3.61f);
        Assert.Contains(path, point => MathF.Abs(point.Y) > 1f);
    }

    [Fact]
    public void ACliffWithoutAValidStepNeverBecomesADirectShortcut()
    {
        Vector3? Ground(Vector3 point) => new(point.X, point.Y, point.X >= 4f ? 3f : 0f);
        Assert.Empty(NpcPathfinder.FindPath(Vector3.Zero, new Vector3(10f, 0f, 3f),
            Ground, (_, _) => false, new NpcPathfinder.Options(MaxSearchDistance: 20f)));
    }

    [Fact]
    public void NonFiniteGroundIsRejected()
    {
        Assert.Empty(NpcPathfinder.FindPath(Vector3.Zero, Vector3.One,
            point => new Vector3(point.X, point.Y, float.NaN), (_, _) => false));
    }

    private static bool WallBlocksCenterLine(Vector3 from, Vector3 to)
    {
        // A four-metre-wide wall spanning the centre lane. The two-metre grid can route
        // around either end, while a direct segment through the lane is rejected.
        const float wallMinX = 3f;
        const float wallMaxX = 7f;
        const float wallMinY = -1f;
        const float wallMaxY = 1f;

        var delta = to - from;
        for (int i = 0; i <= 20; i++)
        {
            float t = i / 20f;
            float x = from.X + (delta.X * t);
            float y = from.Y + (delta.Y * t);
            if (x >= wallMinX && x <= wallMaxX && y >= wallMinY && y <= wallMaxY)
            {
                return true;
            }
        }

        return false;
    }
}
