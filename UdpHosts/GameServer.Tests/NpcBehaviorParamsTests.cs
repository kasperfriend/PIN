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
    public void Parse_ReadsTheBehaviourEmoteByName()
    {
        // The database names the emote an NPC holds in its behaviour string, never by id: monster 2053
        // (AlertAndInteractive(emote="dance")) is one of the 207 rows that do.
        var parsed = NpcBehaviorParams.Parse("AlertAndInteractive(emote=\"dance\")");

        Assert.Equal("dance", parsed.EmoteName);
        Assert.Equal("AlertAndInteractive", parsed.Name);
    }

    [Fact]
    public void Parse_ReadsTheDialogScriptId()
    {
        // Seven monster rows name a dialogScript= (10551 on 612/620/621/622, 39340 on the vendors).
        var parsed = NpcBehaviorParams.Parse("AlertAndInteractive(dialogScript=10551,interactionType=1)");

        Assert.Equal(10551u, parsed.DialogScriptId);
        Assert.Equal(0u, NpcBehaviorParams.Parse("AlertAndInteractive(emote=\"calm\")").DialogScriptId);
        Assert.Equal(0u, NpcBehaviorParams.Parse("AggressiveWanderer").DialogScriptId);
    }

    [Fact]
    public void Parse_ReadsTheEmoteDuration()
    {
        var parsed = NpcBehaviorParams.Parse("AlertAndInteractive(emote=\"crouchSupply\",emoteDuration=-1)");

        Assert.Equal("crouchSupply", parsed.EmoteName);
        Assert.True(parsed.TryGetEmoteDurationSeconds(out int seconds));
        Assert.Equal(-1, seconds);
    }

    [Fact]
    public void Parse_BehaviourWithoutAnEmote_HasNoEmote()
    {
        var wanderer = NpcBehaviorParams.Parse("AggressiveWanderer");
        var ranged = NpcBehaviorParams.Parse("Arch_MedRangedHumanoid_Attack(triggerPullTime=1500)");

        Assert.Equal(string.Empty, wanderer.EmoteName);
        Assert.Equal(string.Empty, ranged.EmoteName);
        Assert.False(ranged.TryGetEmoteDurationSeconds(out _));
    }

    [Fact]
    public void TryGetAbilityModule_ReadsTheModuleAcrossTheDatabasesSpacing()
    {
        // The shipped rows write the pairs both ways round - "am1Id=77721" and "am1Id = 33833," - and this
        // is the dodge pair's row verbatim (monster 1556): the first module turns one way, the second the
        // other, both on a 1,700 ms cooldown with a 65% chance.
        var parsed = NpcBehaviorParams.Parse(
            "Arch_MedRangedHumanoid_Base(triggerPullTime=5000,am1Id = 33833, am1Cooldown = 1700, am1Chance = 0.65, am1Timeout = 1000, am2Id = 33812, am2Cooldown = 1700, am2Chance = 0.65, am2Timeout = 1000)");

        Assert.True(parsed.TryGetAbilityModule("am1", out var first));
        Assert.Equal(33_833u, first.ModuleId);
        Assert.Equal(0.65f, first.Chance);
        Assert.Equal(1_700, first.CooldownMs);
        Assert.Equal(0f, first.MinDistance);
        Assert.Equal(float.MaxValue, first.MaxDistance);

        Assert.True(parsed.TryGetAbilityModule("am2", out var second));
        Assert.Equal(33_812u, second.ModuleId);
    }

    [Fact]
    public void TryGetAbilityModule_ReadsTheMisspelledCooldownKey()
    {
        // Nine parameter occurrences spell it am1Coodown (the docs' own example is am1Coodown=3000).
        var parsed = NpcBehaviorParams.Parse("Arch_FullbodyMelee_Base(combatDist=4,am1Id=82621,am1Facing=true,am1Coodown=3000)");

        Assert.True(parsed.TryGetAbilityModule("am1", out var module));
        Assert.Equal(8_2621u, module.ModuleId);
        Assert.Equal(3_000, module.CooldownMs);
        Assert.Equal(1f, module.Chance);
    }

    [Fact]
    public void TryGetAbilityModule_ReadsTheDistanceBand()
    {
        var parsed = NpcBehaviorParams.Parse("Arch_Melee_Base(am1Id=86132,am1MinDist=10,am1MaxDist=20)");

        Assert.True(parsed.TryGetAbilityModule("am1", out var module));
        Assert.Equal(10f, module.MinDistance);
        Assert.Equal(20f, module.MaxDistance);
        Assert.False(module.AllowsDistance(9.9f));
        Assert.True(module.AllowsDistance(10f));
        Assert.True(module.AllowsDistance(20f));
        Assert.False(module.AllowsDistance(20.1f));
    }

    [Fact]
    public void TryGetAbilityModule_ReadsOriginalNavigationFields()
    {
        var parsed = NpcBehaviorParams.Parse(
            "Arch_MoveThenFire_Base(am1Id=86132,am1NavToDist=6.5,am1NavTimeout=2400)");

        Assert.True(parsed.TryGetAbilityModule("am1", out var module));
        Assert.Equal(6.5f, module.NavToDistance);
        Assert.Equal(2400, module.NavTimeoutMs);
    }

    [Fact]
    public void TryGetAbilityModule_WithoutThatModule_IsFalse()
    {
        var wanderer = NpcBehaviorParams.Parse("AggressiveWanderer");
        var oneModule = NpcBehaviorParams.Parse("Arch_MedRangedAbilityUser_Base(am2Id = 86100)");

        Assert.False(wanderer.TryGetAbilityModule("am1", out _));
        Assert.False(wanderer.TryGetAbilityModule("am2", out _));
        Assert.False(oneModule.TryGetAbilityModule("am1", out _));
        Assert.True(oneModule.TryGetAbilityModule("am2", out var module));
        Assert.Equal(86_100u, module.ModuleId);
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
