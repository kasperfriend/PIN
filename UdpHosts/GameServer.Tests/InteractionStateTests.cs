using GameServer.Entities.Character;
using Xunit;

namespace GameServer.Tests;

public class InteractionStateTests
{
    [Fact]
    public void IsCompleted_FlipsAtTheCompletionTime()
    {
        var state = new InteractionState { StartTimeMs = 1000, CompletionTimeMs = 1500 };

        Assert.False(state.IsCompleted(1499));
        Assert.True(state.IsCompleted(1500));
        Assert.True(state.IsCompleted(2000));
    }

    [Fact]
    public void PercentAt_ClampsAndScales()
    {
        var state = new InteractionState { StartTimeMs = 1000, CompletionTimeMs = 2000 };

        Assert.Equal((byte)0, state.PercentAt(1000));
        Assert.Equal((byte)50, state.PercentAt(1500));
        Assert.Equal((byte)100, state.PercentAt(2000));
        Assert.Equal((byte)100, state.PercentAt(5000));
        Assert.Equal((byte)0, state.PercentAt(500));
    }

    [Fact]
    public void PercentAt_ZeroLengthChannel_NeverOverflows()
    {
        var state = new InteractionState { StartTimeMs = 1000, CompletionTimeMs = 1000 };

        Assert.True(state.IsCompleted(1000));
        Assert.Equal((byte)100, state.PercentAt(1000));
    }
}
