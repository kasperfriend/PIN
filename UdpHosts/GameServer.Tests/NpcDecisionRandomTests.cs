using System;
using System.Collections.Generic;
using GameServer.Systems.Ai;
using Xunit;

namespace GameServer.Tests;

public class NpcDecisionRandomTests
{
    [Fact]
    public void SameDecisionReplaysButBodiesWithTheSameControllerByteDoNotShareTheirRolls()
    {
        var rolls = new HashSet<float>();
        for (ulong i = 1; i <= 128; i++)
        {
            ulong entityId = (i << 8) | 3;
            float roll = NpcDecisionRandom.Roll(entityId, 88159, 60000);
            Assert.Equal(roll, NpcDecisionRandom.Roll(entityId, 88159, 60000));
            Assert.True(float.IsFinite(roll));
            Assert.InRange(roll, 0f, MathF.BitDecrement(1f));
            rolls.Add(roll);
        }

        Assert.True(rolls.Count > 120);
    }

    [Fact]
    public void HighEntityAndClockBitsAndActionIdParticipateInDecisionNoise()
    {
        float roll = NpcDecisionRandom.Roll(0x123400, 1, 60000);
        Assert.NotEqual(roll, NpcDecisionRandom.Roll(0x100123400, 1, 60000));
        Assert.NotEqual(roll, NpcDecisionRandom.Roll(0x123400, 2, 60000));
        Assert.NotEqual(roll, NpcDecisionRandom.Roll(0x123400, 1, 0x100000000 + 60000));
    }
}
