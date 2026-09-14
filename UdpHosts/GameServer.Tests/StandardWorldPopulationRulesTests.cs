using GameServer;
using GameServer.Systems.Spawning.Population;
using Xunit;

namespace GameServer.Tests;

public class StandardWorldPopulationRulesTests
{
    [Fact]
    public void Defaults_LeaveCombatAndScopeInHeadroom()
    {
        var rules = new StandardWorldPopulationRules();

        Assert.Equal(150, rules.MaxLiveNpcs);
        Assert.Equal(150f, rules.ActivationRadius);
        Assert.Equal(225f, rules.DeactivationRadius);
        Assert.Equal(4, rules.SpawnBudget);
        Assert.Equal(100, rules.SpawnBudgetWindowMs);
    }

    [Fact]
    public void FromSettings_FallsBackToConservativeDefaultsForInvalidValues()
    {
        var rules = StandardWorldPopulationRules.FromSettings(new GameServerSettings
        {
            WorldPopulationMaxLiveNpcs = 0,
            WorldPopulationActivationRadius = 0f,
        });

        Assert.Equal(150, rules.MaxLiveNpcs);
        Assert.Equal(150f, rules.ActivationRadius);
        Assert.Equal(225f, rules.DeactivationRadius);
    }

    [Fact]
    public void FromSettings_PreservesAnOperatorsExplicitTuning()
    {
        var rules = StandardWorldPopulationRules.FromSettings(new GameServerSettings
        {
            WorldPopulationMaxLiveNpcs = 75,
            WorldPopulationActivationRadius = 80f,
        });

        Assert.Equal(75, rules.MaxLiveNpcs);
        Assert.Equal(80f, rules.ActivationRadius);
        Assert.Equal(120f, rules.DeactivationRadius);
    }
}
