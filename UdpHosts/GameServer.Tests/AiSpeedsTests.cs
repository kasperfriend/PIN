using GameServer.Systems.Ai;
using Xunit;

namespace GameServer.Tests;

public class AiSpeedsTests
{
    private static StandardAiRules Rules => new()
    {
        MinTrustedSpeed = 0.25f,
        MaxTrustedSpeed = 35f,
    };

    [Theory]
    [InlineData(5f)]
    [InlineData(0.25f)]
    [InlineData(35f)]
    [InlineData(12.5f)]
    public void PlausibleSpeeds_AreTrusted(float speed)
    {
        Assert.True(AiSpeeds.IsTrusted(speed, Rules));
        Assert.Equal(speed, AiSpeeds.Resolve(speed, 3f, Rules));
    }

    [Theory]
    [InlineData(0f)]
    [InlineData(-1f)]
    [InlineData(0.1f)]
    [InlineData(120f)]
    [InlineData(float.NaN)]
    [InlineData(float.PositiveInfinity)]
    public void ImplausibleSpeeds_FallBackToDefault(float speed)
    {
        Assert.False(AiSpeeds.IsTrusted(speed, Rules));
        Assert.Equal(3f, AiSpeeds.Resolve(speed, 3f, Rules));
    }

    [Fact]
    public void NullRules_AreNeverTrusted()
    {
        Assert.False(AiSpeeds.IsTrusted(5f, null));
        Assert.Equal(7f, AiSpeeds.Resolve(5f, 7f, null));
    }
}
