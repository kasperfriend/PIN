using System.Numerics;
using GameServer.StaticDB;
using Xunit;

namespace GameServer.Tests;

public class HardpointTransformTests
{
    [Fact]
    public void Translation_TwelveFloats_ReadsTheLastColumn()
    {
        Assert.Equal(new Vector3(1.5f, 2.5f, 3.5f), HardpointTransform.Translation(
            HardpointTransform.MatrixWithTranslation(1.5f, 2.5f, 3.5f)));
    }

    [Fact]
    public void Translation_XyzMembers_AreTheFallbackWhenTheCellIsNotTwelveNumbers()
    {
        Assert.Equal(new Vector3(4f, 5f, 6f), HardpointTransform.Translation(new XyzCell { X = 4f, Y = 5f, Z = 6f }));
    }

    [Fact]
    public void Translation_ThreeFloats_AreTheFallbackWhenThereIsNoNamedXyz()
    {
        Assert.Equal(new Vector3(7f, 8f, 9f), HardpointTransform.Translation(new[] { 7f, 8f, 9f }));
    }

    [Fact]
    public void Translation_Null_IsZero()
    {
        Assert.Equal(Vector3.Zero, HardpointTransform.Translation(null));
    }

    private sealed class XyzCell
    {
        public float X { get; set; }
        public float Y { get; set; }
        public float Z { get; set; }
    }
}
