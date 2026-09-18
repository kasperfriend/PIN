using System.Collections.Generic;
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
        var end = path[^1];
        Assert.InRange(end.X, -0.01f, 4.01f);
        Assert.InRange(end.Y, -0.01f, 4.01f);
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

    [Fact]
    public void ADisconnectedLayerTooBigToBeACanopyIsKeptWhateverSitsUnderIt()
    {
        // 6 400 m² of deck over 250 000 m² of ground: by the area ratio alone the deck is a small
        // floating patch, but a patch 80 m across is a level of its own - a bridge, a plateau - and
        // treating it as foliage is how a zone loses the ground on that bridge. The size the island
        // test declines to judge is also what keeps it from walking the ground under every face of a
        // zone whose canopy is a hundred thousand separate patches.
        var mesh = new NavigationMesh(
            Ground(size: 500f).Concat(Quad(20f, 20f, 100f, 100f, z: 10f)).ToArray(),
            _ => 1f);

        Assert.Equal(4, mesh.FaceCount);
        Assert.Contains(Centroids(mesh), centroid => centroid.Z > 9f);
    }

    [Fact]
    public void CopiesOfOneSurfaceFarApartInXYAreLeftAlone()
    {
        // The dedupe that drops the second copy of a surface works on faces at the same place: it is
        // a bucket lookup per face. Matching faces by "overlaps it somewhere" instead costs a scan of
        // everything in the neighbourhood per face, which on a zone's worth of ground is a bake that
        // runs for hours - and a whole tile's worth of copies is prevented where the zone is read,
        // by the chunk references being deduplicated there.
        var mesh = new NavigationMesh(
            Quad(0f, 0f, 4f, 4f, z: 0f).Concat(Quad(200f, 0f, 204f, 4f, z: 0f)).ToArray(),
            _ => 1f);

        Assert.Equal(4, mesh.FaceCount);
    }

    [Fact]
    public void FacesWithAnExcludedMaterialAreNeverBakedIntoTheMesh()
    {
        // The beds under a zone's water line are collision but never ground: a mob planned onto
        // one would stand under water, invisible to whoever walks above it yet able to see and
        // shoot them. The two patches sit far apart so that neither is a disconnected island
        // against the other - without the material filter both would be baked.
        var mesh = new NavigationMesh(
            Ground(size: 4f, material: 1)
                .Concat(Quad(200f, 0f, 204f, 4f, z: -6f, material: 7))
                .ToArray(),
            _ => 1f,
            materialExcluded: material => material == 7);

        Assert.Equal(2, mesh.FaceCount);
        Assert.All(Centroids(mesh), centroid => Assert.InRange(centroid.Z, -0.01f, 0.01f));

        var unfiltered = new NavigationMesh(
            Ground(size: 4f, material: 1)
                .Concat(Quad(200f, 0f, 204f, 4f, z: -6f, material: 7))
                .ToArray(),
            _ => 1f);

        Assert.Equal(4, unfiltered.FaceCount);
    }

    [Fact]
    public void AFaceWhoseBoundsAreAbsurdIsStillBakedInConstantTime()
    {
        // A face is entered in the runtime index in every cell its horizontal bounds cover, so that a
        // query for a point inside a large floor finds the floor however big it is. One triangle with
        // absurd coordinates (a merged mesh gone wrong; a non-finite vertex, whose cast to int lands
        // on int.MinValue and turns the loop into a walk over four billion cells per axis) used to
        // make filling that index the thing that never finished - after the chunks had all logged,
        // with nothing in the console to say what the process was doing. The span is capped now, and
        // such a face is indexed at its centroid alone. The assertion is that the mesh is built.
        Vector3[] corners =
        [
            new(-100_000f, -100_000f, 0f),
            new(100_000f, -100_000f, 0f),
            new(100_000f, 100_000f, 0f),
            new(-100_000f, 100_000f, 0f),
        ];

        var giant = new[]
        {
            new NavigationTriangle(corners[0], corners[1], corners[3], 1),
            new NavigationTriangle(corners[1], corners[2], corners[3], 1),
        };

        var mesh = new NavigationMesh(Ground(size: 10f).Concat(giant).ToArray(), _ => 1f);

        Assert.Equal(4, mesh.FaceCount);

        // And the ground next to that triangle is still walkable on its own faces.
        Assert.NotEmpty(mesh.FindPath(new Vector3(1f, 1f, 0f), new Vector3(4f, 4f, 0f), (_, _) => false, 1.25f));
    }

    [Fact]
    public void ABakeOnSeveralThreadsIsTheBakeOnOne()
    {
        // A zone's worth of ground in miniature: a grid of quads, every seventh surface recorded
        // twice, every eleventh a canopy floating over it. Each threaded pass gets several slices of
        // it, and the two bakes have to agree face for face and route for route - the mesh enumerates
        // its faces for world population and its adjacency is what an NPC's path is made of, so a
        // bake that depended on the thread count would put different NPCs on different ground on two
        // machines with the same zone.
        const int Columns = 20;
        var triangles = new List<NavigationTriangle>();

        for (int column = 0; column < Columns; column++)
        {
            for (int row = 0; row < Columns; row++)
            {
                float x = column * 4f;
                float y = row * 4f;

                triangles.AddRange(Quad(x, y, x + 4f, y + 4f, z: 0f));

                if ((column + row) % 7 == 0)
                {
                    triangles.AddRange(Quad(x, y, x + 4f, y + 4f, z: 0f));
                }

                if ((column + row) % 11 == 0)
                {
                    triangles.AddRange(Island(x + 1f, y + 1f, z: 6f, size: 1f));
                }
            }
        }

        var serial = new NavigationMesh(triangles, _ => 1f, maxDegreeOfParallelism: 1);
        var parallel = new NavigationMesh(triangles, _ => 1f, maxDegreeOfParallelism: 8);

        Assert.Equal(serial.FaceCount, parallel.FaceCount);

        for (int i = 0; i < serial.FaceCount; i++)
        {
            Assert.True(serial.TryGetFaceCentroid(i, out var expected));
            Assert.True(parallel.TryGetFaceCentroid(i, out var actual));
            Assert.Equal(expected, actual);
        }

        var start = new Vector3(1f, 1f, 0f);
        var goal = new Vector3((Columns * 4f) - 2f, (Columns * 4f) - 2f, 0f);
        var serialPath = serial.FindPath(start, goal, (_, _) => false, 1.25f);
        var parallelPath = parallel.FindPath(start, goal, (_, _) => false, 1.25f);

        Assert.NotEmpty(serialPath);
        Assert.Equal(serialPath, parallelPath);
    }

    private static NavigationTriangle[] Ground(float size = 10f, uint material = 1) =>
        Quad(0f, 0f, size, size, z: 0f, material);

    private static NavigationTriangle[] Island(float x, float y, float z, float size) =>
        Quad(x, y, x + size, y + size, z);

    private static NavigationTriangle[] Quad(float minX, float minY, float maxX, float maxY, float z, uint material = 1) =>
    [
        new NavigationTriangle(
            new Vector3(minX, minY, z),
            new Vector3(maxX, minY, z),
            new Vector3(minX, maxY, z),
            material),
        new NavigationTriangle(
            new Vector3(maxX, minY, z),
            new Vector3(maxX, maxY, z),
            new Vector3(minX, maxY, z),
            material),
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
