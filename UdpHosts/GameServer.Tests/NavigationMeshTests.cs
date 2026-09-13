using System.Numerics;
using Shared.Collision.Navigation;
using Xunit;

namespace GameServer.Tests;

public class NavigationMeshTests
{
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
}
