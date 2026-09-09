using System;
using System.Numerics;
using GameServer.Systems.Ai;
using Xunit;

namespace GameServer.Tests;

public class AiVectorsTests
{
    [Fact]
    public void HorizontalDistance_IgnoresZ()
    {
        float distance = AiVectors.HorizontalDistance(new Vector3(0f, 0f, -50f), new Vector3(3f, 4f, 900f));

        Assert.Equal(5f, distance, 3);
    }

    [Fact]
    public void HorizontalDistance_OfSamePoint_IsZero()
    {
        var point = new Vector3(12.5f, -3f, 7f);

        Assert.Equal(0f, AiVectors.HorizontalDistance(point, point), 5);
    }

    [Fact]
    public void Distance_CountsTheHeightDifference()
    {
        // The 3-4-5 on the ground plus 12 m of height: the distance an attack is measured over,
        // which is what makes "the mob hit me from the floor below" impossible.
        Assert.Equal(13f, AiVectors.Distance(new Vector3(0f, 0f, 0f), new Vector3(3f, 4f, 12f)), 3);
        Assert.Equal(13f, AiVectors.Distance(new Vector3(3f, 4f, 12f), new Vector3(0f, 0f, 0f)), 3);
    }

    [Fact]
    public void Distance_OnFlatGround_IsTheHorizontalDistance()
    {
        var a = new Vector3(-2f, 5f, 400f);
        var b = new Vector3(4f, 5f, 400f);

        Assert.Equal(AiVectors.HorizontalDistance(a, b), AiVectors.Distance(a, b), 4);
    }

    [Theory]
    [InlineData(0f, 4f, 4f)]      // target above
    [InlineData(4f, 0f, 4f)]      // target below: same answer, the band is not directional
    [InlineData(-3f, -1f, 2f)]
    [InlineData(402.5f, 400f, 2.5f)] // the log's "player at Z 402, mob at Z 400" case
    public void HeightDelta_IsTheAbsoluteVerticalGap(float fromZ, float toZ, float expected)
    {
        Assert.Equal(expected, AiVectors.HeightDelta(new Vector3(1f, 2f, fromZ), new Vector3(9f, 8f, toZ)), 3);
    }

    [Theory]
    [InlineData(1f, 0f)]
    [InlineData(-1f, 0f)]
    [InlineData(0f, 1f)]
    [InlineData(0f, -1f)]
    [InlineData(0.7071068f, 0.7071068f)]
    [InlineData(-0.6f, 0.8f)]
    public void OrientationFacing_PointsLocalPlusYAlongTheRequestedDirection(float x, float y)
    {
        // CharacterEntity resolves its facing as
        // QuaternionEx.Transform(new Vector3(0, 1, 0), QuaternionEx.Inverse(Orientation))
        // (local +Y is forward, local +Z is up), so feed the produced orientation
        // back through exactly that formula.
        var forward = new Vector3(x, y, 0f);

        var orientation = AiVectors.OrientationFacing(forward);
        var resolved = Vector3.Transform(new Vector3(0f, 1f, 0f), Quaternion.Conjugate(orientation));

        Assert.Equal(Vector3.Normalize(forward).X, resolved.X, 3);
        Assert.Equal(Vector3.Normalize(forward).Y, resolved.Y, 3);
        Assert.Equal(0f, resolved.Z, 3);
    }

    [Theory]
    [InlineData(1f, 0f)]
    [InlineData(-1f, 0f)]
    [InlineData(0f, 1f)]
    [InlineData(0f, -1f)]
    [InlineData(0.7071068f, 0.7071068f)]
    [InlineData(-0.6f, 0.8f)]
    public void OrientationFacing_KeepsTheCharacterUpright(float x, float y)
    {
        // The orientation must be a yaw-only rotation about world +Z so the model's
        // up axis (local +Z) keeps pointing at world +Z (up), otherwise the mob is
        // rendered lying on its side or face up.
        var orientation = AiVectors.OrientationFacing(new Vector3(x, y, 0f));

        var up = Vector3.Transform(new Vector3(0f, 0f, 1f), Quaternion.Conjugate(orientation));

        Assert.Equal(0f, up.X, 3);
        Assert.Equal(0f, up.Y, 3);
        Assert.Equal(1f, up.Z, 3);
    }

    [Fact]
    public void OrientationFacing_IgnoresVerticalComponent()
    {
        var withPitch = AiVectors.OrientationFacing(new Vector3(0f, 1f, 5f));
        var flat = AiVectors.OrientationFacing(new Vector3(0f, 1f, 0f));

        Assert.Equal(flat.W, withPitch.W, 5);
        Assert.Equal(flat.X, withPitch.X, 5);
        Assert.Equal(flat.Y, withPitch.Y, 5);
        Assert.Equal(flat.Z, withPitch.Z, 5);
    }

    [Fact]
    public void OrientationFacing_OfDegenerateDirection_IsIdentity()
    {
        Assert.Equal(Quaternion.Identity, AiVectors.OrientationFacing(Vector3.Zero));
        Assert.Equal(Quaternion.Identity, AiVectors.OrientationFacing(new Vector3(0f, 0f, 10f)));
    }
}
