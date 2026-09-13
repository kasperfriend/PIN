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
