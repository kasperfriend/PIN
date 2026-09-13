using System.Collections.Generic;
using GameServer.StaticDB.Records.dbdialogdata;
using GameServer.Systems.Dialog;
using Xunit;

namespace GameServer.Tests;

public class DialogMathTests
{
    [Fact]
    public void ProbabilityFor_ReadsTheColumnOfThatRelation()
    {
        var description = new BattleChatterDescriptions
        {
            ProbabilityAlly = 10,
            ProbabilityTargetedPlayer = 40,
            ProbabilityHostile = 80,
        };

        Assert.Equal((byte)10, DialogMath.ProbabilityFor(description, BattleChatterRelation.Ally));
        Assert.Equal((byte)40, DialogMath.ProbabilityFor(description, BattleChatterRelation.TargetedPlayer));
        Assert.Equal((byte)80, DialogMath.ProbabilityFor(description, BattleChatterRelation.Hostile));
        Assert.Equal((byte)0, DialogMath.ProbabilityFor(null, BattleChatterRelation.Ally));
    }

    [Theory]
    [InlineData(0, 0f, false)]
    [InlineData(100, 0f, true)]
    [InlineData(100, 0.99f, true)]
    [InlineData(40, 0.39f, true)]
    [InlineData(40, 0.40f, false)]
    [InlineData(40, 1f, false)]
    public void Roll_HonoursTheZeroToHundredProbability(byte probability, float roll, bool expected)
    {
        Assert.Equal(expected, DialogMath.Roll(probability, roll));
    }

    [Fact]
    public void VoiceSetKey_PacksTheVoiceSetInTheHighBits()
    {
        ulong key = ((ulong)17 << 32) | 1417;

        Assert.Equal(17u, DialogMath.VoiceSetOf(key));
        Assert.Equal(1417u, DialogMath.SetIdOf(key));
    }

    [Fact]
    public void ResolveChatterDialogId_PrefersTheVoiceSetRowOverTheDefault()
    {
        var description = new BattleChatterDescriptions
        {
            DefaultDialogScriptId = 44_931,
            DialogScriptSetId = 1417,
        };
        var rows = new List<BattleChatterSetParams>
        {
            new() { SetId = 1417, VoiceSetKey = ((ulong)5 << 32) | 1417, DialogId = 100 },
            new() { SetId = 1417, VoiceSetKey = ((ulong)9 << 32) | 1417, DialogId = 200 },
        };

        Assert.Equal(200u, DialogMath.ResolveChatterDialogId(description, voiceSet: 9, rows));
        Assert.Equal(44_931u, DialogMath.ResolveChatterDialogId(description, voiceSet: 3, rows));
        Assert.Equal(44_931u, DialogMath.ResolveChatterDialogId(description, voiceSet: 9, setParams: null));
    }
}
