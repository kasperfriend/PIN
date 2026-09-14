using System.Numerics;
using GameServer.Systems.Spawning.Population;
using Xunit;

namespace GameServer.Tests;

/// <summary>
///     The placement grid is the server-side half of spawn collision checking: it refuses a position
///     another body already holds, which no ray cast can see when that body is planned rather than
///     spawned.
/// </summary>
public class SpawnOccupancyGridTests
{
    [Fact]
    public void AnEmptyGridAcceptsAnywhere()
    {
        var grid = new SpawnOccupancyGrid(8f);

        Assert.Equal(0, grid.Count);
        Assert.True(grid.IsAreaFree(new Vector3(1234f, -567f, 10f), 0.7f, 0.5f));
    }

    [Fact]
    public void ARegisteredBodyBlocksTheGroundAroundIt()
    {
        var grid = new SpawnOccupancyGrid(8f);
        grid.Add(1, new Vector3(100f, 100f, 0f), 0.7f);

        Assert.Equal(1, grid.Count);
        Assert.False(grid.IsAreaFree(new Vector3(100.5f, 100f, 0f), 0.7f, 0.5f));
        Assert.True(grid.IsAreaFree(new Vector3(110f, 100f, 0f), 0.7f, 0.5f));
    }

    [Fact]
    public void TwoBodiesHaveToClearBothRadiiPlusTheSeparation()
    {
        var grid = new SpawnOccupancyGrid(8f);
        grid.Add(1, Vector3.Zero, 3f);

        // 3 m + 6 m + 0.5 m of separation = 9.5 m between the centres.
        Assert.False(grid.IsAreaFree(new Vector3(9f, 0f, 0f), 6f, 0.5f));
        Assert.True(grid.IsAreaFree(new Vector3(10f, 0f, 0f), 6f, 0.5f));
    }

    [Fact]
    public void ABigBodyIsFoundFromAnotherHashCell()
    {
        // The query has to look further than its own radius: the body it must not overlap can be a
        // wide one sitting in the neighbouring hash cell. A 6 m body at x=30 reaches back to x=22.8
        // for a query with a 0.7 m body and half a metre of separation.
        var grid = new SpawnOccupancyGrid(4f);
        grid.Add(1, new Vector3(30f, 0f, 0f), 6f);

        Assert.False(grid.IsAreaFree(new Vector3(25f, 0f, 0f), 0.7f, 0.5f));
        Assert.True(grid.IsAreaFree(new Vector3(20f, 0f, 0f), 0.7f, 0.5f));
    }

    [Fact]
    public void BodiesAtClearlyDifferentHeightsAreNotInTheWay()
    {
        var grid = new SpawnOccupancyGrid(8f);
        grid.Add(1, new Vector3(100f, 100f, 12f), 0.7f);

        // The same spot on the ground under a balcony: the exact three dimensional answer belongs to
        // the physics volume check, not to this grid.
        Assert.True(grid.IsAreaFree(new Vector3(100f, 100f, 0f), 0.7f, 0.5f));
        Assert.False(grid.IsAreaFree(new Vector3(100f, 100f, 12f), 0.7f, 0.5f));
    }

    [Fact]
    public void RemovingABodyFreesItsGround()
    {
        var grid = new SpawnOccupancyGrid(8f);
        grid.Add(1, Vector3.Zero, 0.7f);
        Assert.False(grid.IsAreaFree(Vector3.Zero, 0.7f, 0.5f));

        Assert.True(grid.Remove(1));
        Assert.False(grid.Remove(1));
        Assert.Equal(0, grid.Count);
        Assert.True(grid.IsAreaFree(Vector3.Zero, 0.7f, 0.5f));
    }

    [Fact]
    public void RegisteringTheSameIdAgainMovesTheBody()
    {
        var grid = new SpawnOccupancyGrid(8f);
        grid.Add(1, Vector3.Zero, 0.7f);
        grid.Add(1, new Vector3(50f, 0f, 0f), 0.7f);

        Assert.Equal(1, grid.Count);
        Assert.True(grid.IsAreaFree(Vector3.Zero, 0.7f, 0.5f));
        Assert.False(grid.IsAreaFree(new Vector3(50f, 0f, 0f), 0.7f, 0.5f));
    }

    [Fact]
    public void ClearForgetsEverything()
    {
        var grid = new SpawnOccupancyGrid(8f);
        grid.Add(1, Vector3.Zero, 0.7f);
        grid.Add(2, new Vector3(50f, 0f, 0f), 0.7f);

        grid.Clear();

        Assert.Equal(0, grid.Count);
        Assert.True(grid.IsAreaFree(Vector3.Zero, 0.7f, 0.5f));
        Assert.True(grid.IsAreaFree(new Vector3(50f, 0f, 0f), 0.7f, 0.5f));
    }

    [Fact]
    public void NegativeCoordinatesWork()
    {
        // A zone's chunk grid is centred on the zone, so plenty of its ground has negative
        // coordinates; the hash has to cope with that.
        var grid = new SpawnOccupancyGrid(8f);
        grid.Add(1, new Vector3(-1234f, -567f, 0f), 0.7f);

        Assert.False(grid.IsAreaFree(new Vector3(-1234f, -567f, 0f), 0.7f, 0.5f));
        Assert.True(grid.IsAreaFree(new Vector3(-1200f, -567f, 0f), 0.7f, 0.5f));
    }
}
