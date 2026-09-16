using System.Linq;
using System.Numerics;
using Shared.Collision.Navigation;
using Xunit;

namespace GameServer.Tests;

public class NavigationMeshTests
{
    [Fact]
    public void AGoalJustOffTheMeshSnapsOntoTheNearestWalkableFace()
    {
        // Ambient wander picks a random XY that often lands a few tens of centimetres off a
        // triangle (a building footprint, a gap). Without a snap that destination is an empty
        // path and the NPC stands still for the retry delay.
        var mesh = new NavigationMesh(Ground(size: 4f), _ => 1f);

        var path = mesh.FindPath(
            new Vector3(1f, 1f, 0f),
            new Vector3(4.4f, 2f, 0f),
            (_, _) => false,
            1.25f);

        Assert.NotEmpty(path);
        Assert.All(path, point => Assert.InRange(point.X, -0.01f, 4.01f));
    }

    [Fact]
    public void AdjacentWalkableFacesProduceARoute()
    {
        var mesh = new NavigationMesh(
            [
                new NavigationTriangle(
                    new Vector3(0f, 0f, 0f),
                    new Vector3(2f, 0f, 0f),
                    new Vector3(0f, 2f, 0f),
                    1),
                new NavigationTriangle(
                    new Vector3(2f, 0f, 0f),
                    new Vector3(2f, 2f, 0f),
                    new Vector3(0f, 2f, 0f),
                    1),
            ],
            _ => 1f);

        var path = mesh.FindPath(
            new Vector3(0.2f, 0.2f, 0f),
            new Vector3(1.8f, 1.8f, 0f),
            (_, _) => false,
            1.25f);

        Assert.NotEmpty(path);
        Assert.Equal(2, mesh.FaceCount);
    }

    [Fact]
    public void ExcludedFacesAreNotAddedToTheMesh()
    {
        var mesh = new NavigationMesh(
            [new NavigationTriangle(
                new Vector3(0f, 0f, 0f),
                new Vector3(2f, 0f, 0f),
                new Vector3(0f, 2f, 0f),
                1)],
            _ => 1f,
            _ => true);

        Assert.Equal(0, mesh.FaceCount);
        Assert.Empty(mesh.FindPath(Vector3.Zero, Vector3.One, (_, _) => false, 1.25f));
    }

    [Fact]
    public void DuplicateOverlappingFacesAtTheSameHeightKeepOneCopy()
    {
        // Two almost-identical triangles, 5 cm apart vertically: the zone file listed the same
        // tile twice (or a chunk skirt overlapped its neighbour). One spawn point, not two.
        var mesh = new NavigationMesh(
            [
                new NavigationTriangle(
                    new Vector3(0f, 0f, 0f),
                    new Vector3(4f, 0f, 0f),
                    new Vector3(0f, 4f, 0f),
                    1),
                new NavigationTriangle(
                    new Vector3(0.1f, 0.1f, 0.05f),
                    new Vector3(3.9f, 0.1f, 0.05f),
                    new Vector3(0.1f, 3.9f, 0.05f),
                    1),
            ],
            _ => 1f);

        Assert.Equal(1, mesh.FaceCount);
        Assert.True(mesh.TryGetFaceCentroid(0, out var centroid));
        Assert.InRange(centroid.Z, -0.01f, 0.06f);
    }

    [Fact]
    public void ASmallIslandAboveTheGroundIsDropped()
    {
        // Tree canopy: a couple of square metres of walkable triangles floating over the terrain.
        var mesh = new NavigationMesh(Ground().Concat(Island(5f, 5f, 8f, size: 1f)).ToArray(), _ => 1f);

        Assert.Equal(2, mesh.FaceCount);
        Assert.All(Centroids(mesh), centroid => Assert.InRange(centroid.Z, -0.01f, 0.01f));
    }

    [Fact]
    public void ASmallIslandUnderTheGroundIsDropped()
    {
        // Cavity under a rock: walkable faces below the terrain, overlapping it in XY.
        var mesh = new NavigationMesh(Ground().Concat(Island(5f, 5f, -2f, size: 1f)).ToArray(), _ => 1f);

        Assert.Equal(2, mesh.FaceCount);
        Assert.All(Centroids(mesh), centroid => Assert.InRange(centroid.Z, -0.01f, 0.01f));
    }

    [Fact]
    public void ALargeDisconnectedLayerOverTheGroundIsKept()
    {
        // A balcony / bridge deck: large enough to be its own layer, not a tree canopy.
        var mesh = new NavigationMesh(
            Ground(size: 20f).Concat(Quad(2f, 2f, 12f, 12f, z: 5f)).ToArray(),
            _ => 1f);

        Assert.Equal(4, mesh.FaceCount);
        Assert.Contains(Centroids(mesh), centroid => centroid.Z > 4f);
        Assert.Contains(Centroids(mesh), centroid => centroid.Z < 1f);
    }

    private static NavigationTriangle[] Ground(float size = 10f) => Quad(0f, 0f, size, size, z: 0f);

    private static NavigationTriangle[] Island(float x, float y, float z, float size) =>
        Quad(x, y, x + size, y + size, z);

    private static NavigationTriangle[] Quad(float minX, float minY, float maxX, float maxY, float z) =>
    [
        new NavigationTriangle(
            new Vector3(minX, minY, z),
            new Vector3(maxX, minY, z),
            new Vector3(minX, maxY, z),
            1),
        new NavigationTriangle(
            new Vector3(maxX, minY, z),
            new Vector3(maxX, maxY, z),
            new Vector3(minX, maxY, z),
            1),
    ];

    private static Vector3[] Centroids(NavigationMesh mesh)
    {
        var result = new Vector3[mesh.FaceCount];
        for (int i = 0; i < mesh.FaceCount; i++)
        {
            Assert.True(mesh.TryGetFaceCentroid(i, out result[i]));
        }

        return result;
    }
}
