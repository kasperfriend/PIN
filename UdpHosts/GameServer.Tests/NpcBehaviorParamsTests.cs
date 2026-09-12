using GameServer.Systems.Ai;
using Xunit;

namespace GameServer.Tests;

public class NpcBehaviorParamsTests
{
    [Fact]
    public void Parse_ReadsTheNameAndItsParameters()
    {
        var parsed = NpcBehaviorParams.Parse("Arch_MedRangedHumanoid_Attack(triggerPullTime=1500,fireRestDuration=2000,reviveOn=1)");

        Assert.Equal("Arch_MedRangedHumanoid_Attack", parsed.Name);
        Assert.Equal(1500, parsed.TriggerPullTimeMs);
        Assert.Equal(2000, parsed.FireRestDurationMs);
        Assert.True(parsed.HasAttackTiming);
        Assert.Equal("1", parsed.Values["reviveOn"]);
    }

    [Fact]
    public void Parse_NameWithoutParameters_KeepsTheName()
    {
        var parsed = NpcBehaviorParams.Parse("AggressiveWanderer");

        Assert.Equal("AggressiveWanderer", parsed.Name);
        Assert.Empty(parsed.Values);
        Assert.False(parsed.HasAttackTiming);
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("   ")]
    public void Parse_EmptyInput_IsEmpty(string behavior)
    {
        var parsed = NpcBehaviorParams.Parse(behavior);

        Assert.Equal(string.Empty, parsed.Name);
        Assert.Empty(parsed.Values);
        Assert.Same(NpcBehaviorParams.Empty, parsed);
    }

    [Fact]
    public void Parse_ReadsFloatingPointAndDistanceParameters()
    {
        var parsed = NpcBehaviorParams.Parse("EliteWanderer(combatDist=20.5,preferredMinCombatDist=3.5)");

        Assert.Equal(20.5f, parsed.CombatDistance);
        Assert.Equal(3.5f, parsed.PreferredMinimumCombatDistance);
    }

    [Fact]
    public void Parse_KeysAreCaseInsensitive_AndTheLastValueWins()
    {
        var parsed = NpcBehaviorParams.Parse("SwarmWanderer(TriggerPullTime=900,triggerPullTime=1100)");

        Assert.Equal(1100, parsed.TriggerPullTimeMs);
    }

    [Fact]
    public void Parse_ToleratesMalformedFragmentsAndUnknownKeys()
    {
        // The build really does contain misspelled keys (am1Coodown) and empty fragments.
        var parsed = NpcBehaviorParams.Parse("Arch_MedRangedHumanoid_Attack(,am1Coodown=3000,broken,am1Chance=0.7)");

        Assert.Equal("Arch_MedRangedHumanoid_Attack", parsed.Name);
        Assert.Equal("3000", parsed.Values["am1Coodown"]);
        Assert.Equal("0.7", parsed.Values["am1Chance"]);
        Assert.Equal(0, parsed.TriggerPullTimeMs);
    }

    [Fact]
    public void TryGetInt_ReturnsFalseForNonNumbers()
    {
        var parsed = NpcBehaviorParams.Parse("Wanderer(speed=fast,count=12)");

        Assert.False(parsed.TryGetInt("speed", out _));
        Assert.True(parsed.TryGetInt("count", out int count));
        Assert.Equal(12, count);
        Assert.False(parsed.TryGetInt("missing", out _));
    }
}
