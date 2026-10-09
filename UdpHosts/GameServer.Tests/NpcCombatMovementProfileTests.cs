using System.Text.Json;
using GameServer.Systems.Ai;
using Xunit;

namespace GameServer.Tests;

public class NpcCombatMovementProfileTests
{
    [Fact]
    public void UnknownSniperTransitionsDoNotSilentlyActivateAKitingPolicy()
    {
        Assert.Equal(NpcCombatMovementProfile.Default, NpcCombatMovementProfile.Resolve(
            NpcBehaviorParams.Parse("Arch_Sniper_Base(runToSnipeDist=35,snipeToRunDist=20)"), null));
    }

    [Fact]
    public void UnsupportedOffensiveValueDoesNotResurrectBaseChance()
    {
        var profile = NpcCombatMovementProfile.Resolve(
            NpcBehaviorParams.Parse("Base(moveChance=0)"),
            NpcBehaviorParams.Parse("Attack(moveChance=10.0)"));
        Assert.Equal(1f, profile.MoveChance);
    }

    [Theory]
    [InlineData("Attack(moveChance=0.2,maxMove=10)", 0.2f, 10f)]
    [InlineData("Attack(moveChance=.75,maxMove=15)", 0.75f, 12f)]
    [InlineData("Attack(moveChance=0,maxMove=0)", 0f, 0f)]
    [InlineData("Attack(moveChance=10.0,maxMove=-1)", 1f, 12f)]
    [InlineData("Attack(moveChance=NaN,maxMove=Infinity)", 1f, 12f)]
    public void ValidValuesConstrainFallbackAndUnsupportedValuesDoNotBecomeProbabilities(
        string text, float chance, float distance)
    {
        var profile = NpcCombatMovementProfile.Resolve(NpcBehaviorParams.Parse(text), null);
        Assert.Equal(chance, profile.MoveChance);
        Assert.Equal(distance, profile.MaxMove);
    }

    [Fact]
    public void OffensiveKeysOverrideIndividuallyWithoutChangingAmbientProfile()
    {
        var behavior = NpcBehaviorParams.Parse("Base(moveChance=.2,maxMove=10,speedMultiplier=1.5)");
        var offensive = NpcBehaviorParams.Parse("Attack(moveChance=0)");
        var profile = NpcCombatMovementProfile.Resolve(behavior, offensive);
        Assert.Equal(0f, profile.MoveChance);
        Assert.Equal(10f, profile.MaxMove);
        Assert.Equal(1.5f, profile.SpeedMultiplier);
        Assert.Equal(".2", behavior.Values["moveChance"]);
    }

    [Theory]
    [InlineData("Attack(grounded=false)", false)]
    [InlineData("Attack(climber=1)", false)]
    [InlineData("Attack", true)]
    public void NonGroundTacticsAreNotInvented(string text, bool ground)
    {
        Assert.Equal(ground, NpcCombatMovementProfile.Resolve(NpcBehaviorParams.Parse(text), null).GroundTactics);
    }

    [Theory]
    [InlineData("StockMelee(speedMultiplier=1.5)", 1.5f)]
    [InlineData("GiantAranhaMiniBoss(speedMultiplier=1)", 1f)]
    [InlineData("Attack(speedMultiplier=0)", 1f)]
    [InlineData("Attack(speedMultiplier=NaN)", 1f)]
    [InlineData("Attack(speedMultiplier=100)", 1f)]
    public void CombatMultiplierRejectsUnsafeValues(string text, float expected)
    {
        Assert.Equal(expected, NpcCombatMovementProfile.Resolve(NpcBehaviorParams.Parse(text), null).SpeedMultiplier);
    }

    [Theory]
    [InlineData(1229u, 0.2f, 12f, 1f, true)]
    [InlineData(1849u, 0.75f, 10f, 1f, true)]
    [InlineData(1034u, 1f, 12f, 1f, true)]
    [InlineData(538u, 1f, 12f, 1.5f, true)]
    [InlineData(490u, 1f, 12f, 1f, false)]
    public void ActualProd1962InvocationsResolveTheirAuthoredConstraints(
        uint id, float chance, float distance, float speed, bool ground)
    {
        using var stream = typeof(NpcCombatMovementProfileTests).Assembly.GetManifestResourceStream("NpcMovementReference.json");
        Assert.NotNull(stream);
        using var document = JsonDocument.Parse(stream);
        foreach (var row in document.RootElement.GetProperty("monsters").EnumerateArray())
        {
            if (row.GetProperty("id").GetUInt32() != id)
            {
                continue;
            }

            var profile = NpcCombatMovementProfile.Resolve(
                NpcBehaviorParams.Parse(row.GetProperty("behavior").GetString()),
                NpcBehaviorParams.Parse(row.GetProperty("behavior_offensive").GetString()));
            Assert.Equal(chance, profile.MoveChance);
            Assert.Equal(distance, profile.MaxMove);
            Assert.Equal(speed, profile.SpeedMultiplier);
            Assert.Equal(ground, profile.GroundTactics);
            return;
        }

        Assert.Fail($"Missing monster {id} in shipped census");
    }
}
