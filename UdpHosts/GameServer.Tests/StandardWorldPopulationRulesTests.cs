using GameServer;
using GameServer.Systems.Spawning.Population;
using Shared.Common;
using Xunit;

namespace GameServer.Tests;

public class StandardWorldPopulationRulesTests
{
    [Fact]
    public void Defaults_LeaveCombatAndScopeInHeadroom()
    {
        var rules = new StandardWorldPopulationRules();

        Assert.False(rules.SpawnFullZone);
        Assert.Equal(150, rules.MaxLiveNpcs);
        Assert.Equal(150f, rules.ActivationRadius);
        Assert.Equal(225f, rules.DeactivationRadius);
        Assert.Equal(4, rules.SpawnBudget);
        Assert.Equal(100, rules.SpawnBudgetWindowMs);
        Assert.Equal(80f, rules.OutpostSettlementRadius);
        Assert.Equal(200, rules.PlacementAttemptsPerUpdate);
    }

    [Fact]
    public void FromSettings_FallsBackToConservativeDefaultsForUnsafeValues()
    {
        var rules = StandardWorldPopulationRules.FromSettings(new GameServerSettings
        {
            WorldPopulationMaxLiveNpcs = 0,
            WorldPopulationActivationRadius = float.NaN,
            WorldPopulationDeactivationRadius = float.PositiveInfinity,
            WorldPopulationCellSize = 0f,
            WorldPopulationMaxNpcsPerCell = 0,
            WorldPopulationMaxDifficultyPerCell = -1,
            WorldPopulationUnbudgetedDifficultyCost = -1,
            WorldPopulationMaxPlannedSlots = 0,
            WorldPopulationSpawnBudget = 0,
            WorldPopulationSpawnBudgetWindowMs = 0,
            WorldPopulationTickIntervalMs = 0,
            WorldPopulationPlanWorkPerTick = 0,
            WorldPopulationMinSeparation = -1f,
            WorldPopulationMinPlayerDistance = -1f,
            WorldPopulationMaxPlacementAttempts = 0,
            WorldPopulationPlacementAttemptsPerUpdate = 0,
            WorldPopulationPlacementRetryDelayMs = -1,
            WorldPopulationMaxPlacementFailures = 0,
            WorldPopulationRespawnDelayMs = -1,
            WorldPopulationMinimumWalkableNormalZ = 1.1f,
            WorldPopulationDefaultBodyRadius = 0f,
            WorldPopulationDefaultBodyHeight = 0f,
            WorldPopulationDeployableInfluenceRadius = -1f,
            WorldPopulationOutpostSettlementRadius = -1f,
            WorldPopulationMeldingInfluenceRadius = -1f,
        });

        Assert.Equal(150, rules.MaxLiveNpcs);
        Assert.Equal(150f, rules.ActivationRadius);
        Assert.Equal(225f, rules.DeactivationRadius);
        Assert.Equal(32f, rules.CellSize);
        Assert.Equal(4, rules.MaxNpcsPerCell);
        Assert.Equal(400, rules.MaxDifficultyPerCell);
        Assert.Equal(25, rules.UnbudgetedDifficultyCost);
        Assert.Equal(20_000, rules.MaxPlannedSlots);
        Assert.Equal(4, rules.SpawnBudget);
        Assert.Equal(100, rules.SpawnBudgetWindowMs);
        Assert.Equal(250, rules.TickIntervalMs);
        Assert.Equal(20_000, rules.PlanWorkPerTick);
        Assert.Equal(0.5f, rules.MinSeparation);
        Assert.Equal(25f, rules.MinPlayerDistance);
        Assert.Equal(6, rules.MaxPlacementAttempts);
        Assert.Equal(200, rules.PlacementAttemptsPerUpdate);
        Assert.Equal(1_000, rules.PlacementRetryDelayMs);
        Assert.Equal(8, rules.MaxPlacementFailures);
        Assert.Equal(30_000, rules.RespawnDelayMs);
        Assert.Equal(0.35f, rules.MinimumWalkableNormalZ);
        Assert.Equal(0.7f, rules.DefaultBodyRadius);
        Assert.Equal(1.8f, rules.DefaultBodyHeight);
        Assert.Equal(25f, rules.DeployableInfluenceRadius);
        Assert.Equal(80f, rules.OutpostSettlementRadius);
        Assert.Equal(120f, rules.MeldingInfluenceRadius);
    }

    [Fact]
    public void FromSettings_PreservesEveryOperatorsExplicitTuning()
    {
        var rules = StandardWorldPopulationRules.FromSettings(new GameServerSettings
        {
            SpawnWorldPopulation = false,
            WorldPopulationSpawnFullZone = true,
            WorldPopulationMaxLiveNpcs = 75,
            WorldPopulationActivationRadius = 80f,
            WorldPopulationDeactivationRadius = 135f,
            WorldPopulationCellSize = 16f,
            WorldPopulationMaxNpcsPerCell = 3,
            WorldPopulationMaxDifficultyPerCell = 300,
            WorldPopulationUnbudgetedDifficultyCost = 5,
            WorldPopulationMaxPlannedSlots = 1_234,
            WorldPopulationSpawnBudget = 2,
            WorldPopulationSpawnBudgetWindowMs = 50,
            WorldPopulationTickIntervalMs = 125,
            WorldPopulationPlanWorkPerTick = 500,
            WorldPopulationMinSeparation = 0.25f,
            WorldPopulationMinPlayerDistance = 0f,
            WorldPopulationMaxPlacementAttempts = 3,
            WorldPopulationPlacementAttemptsPerUpdate = 7,
            WorldPopulationPlacementRetryDelayMs = 0,
            WorldPopulationMaxPlacementFailures = 2,
            WorldPopulationRespawnDelayMs = 0,
            WorldPopulationMinimumWalkableNormalZ = 0.5f,
            WorldPopulationDefaultBodyRadius = 0.8f,
            WorldPopulationDefaultBodyHeight = 2f,
            WorldPopulationDeployableInfluenceRadius = 0f,
            WorldPopulationOutpostSettlementRadius = 0f,
            WorldPopulationMeldingInfluenceRadius = 55f,
        });

        Assert.False(rules.Enabled);
        Assert.True(rules.SpawnFullZone);
        Assert.Equal(75, rules.MaxLiveNpcs);
        Assert.Equal(80f, rules.ActivationRadius);
        Assert.Equal(135f, rules.DeactivationRadius);
        Assert.Equal(16f, rules.CellSize);
        Assert.Equal(3, rules.MaxNpcsPerCell);
        Assert.Equal(300, rules.MaxDifficultyPerCell);
        Assert.Equal(5, rules.UnbudgetedDifficultyCost);
        Assert.Equal(1_234, rules.MaxPlannedSlots);
        Assert.Equal(2, rules.SpawnBudget);
        Assert.Equal(50, rules.SpawnBudgetWindowMs);
        Assert.Equal(125, rules.TickIntervalMs);
        Assert.Equal(500, rules.PlanWorkPerTick);
        Assert.Equal(0.25f, rules.MinSeparation);
        Assert.Equal(0f, rules.MinPlayerDistance);
        Assert.Equal(3, rules.MaxPlacementAttempts);
        Assert.Equal(7, rules.PlacementAttemptsPerUpdate);
        Assert.Equal(0, rules.PlacementRetryDelayMs);
        Assert.Equal(2, rules.MaxPlacementFailures);
        Assert.Equal(0, rules.RespawnDelayMs);
        Assert.Equal(0.5f, rules.MinimumWalkableNormalZ);
        Assert.Equal(0.8f, rules.DefaultBodyRadius);
        Assert.Equal(2f, rules.DefaultBodyHeight);
        Assert.Equal(0f, rules.DeployableInfluenceRadius);
        Assert.Equal(0f, rules.OutpostSettlementRadius);
        Assert.Equal(55f, rules.MeldingInfluenceRadius);
    }

    [Fact]
    public void AppConfig_MapsTheWorkerThreadSettings()
    {
        var appSettings = AppConfigFile.ParseAppSettings(
            """
            <configuration><appSettings>
                <add key="ServerWorkerThreads" value="6"/>
                <add key="NavigationBakeThreads" value="14"/>
                <add key="WorldPopulationPlanThreads" value="9"/>
                <add key="PhysicsThreads" value="5"/>
                <add key="WorldPopulationPlanOnWorkers" value="false"/>
            </appSettings></configuration>
            """,
            "test");
        var settings = new GameServerSettings();

        GameServerModule.ApplyServerWorkerSettings(appSettings, settings);

        Assert.Equal(6, settings.ServerWorkerThreads);
        Assert.Equal(14, settings.NavigationBakeThreads);
        Assert.Equal(9, settings.WorldPopulationPlanThreads);
        Assert.Equal(5, settings.PhysicsThreads);
        Assert.False(settings.WorldPopulationPlanOnWorkers);
    }

    [Fact]
    public void Defaults_LeaveTheBackgroundWorkAutomatic()
    {
        var settings = new GameServerSettings();

        // 0 is the automatic thread count everywhere, and the plan is built off the tick: the
        // settings a server started with no config at all should have.
        Assert.Equal(0, settings.ServerWorkerThreads);
        Assert.Equal(0, settings.NavigationBakeThreads);
        Assert.Equal(0, settings.WorldPopulationPlanThreads);
        Assert.Equal(0, settings.PhysicsThreads);
        Assert.True(settings.WorldPopulationPlanOnWorkers);
    }

    [Fact]
    public void ThePerPieceThreadSettings_OverrideTheSharedOneWhenTheyAreSet()
    {
        // 0 follows the shared setting when it is set; a number is its own.
        var shared = new GameServerSettings { ServerWorkerThreads = 12 };

        Assert.Equal(12, shared.ResolvedNavigationBakeThreads);
        Assert.Equal(12, shared.ResolvedWorldPopulationPlanThreads);

        var perPiece = new GameServerSettings
        {
            ServerWorkerThreads = 4,
            NavigationBakeThreads = 14,
            WorldPopulationPlanThreads = 9,
        };

        Assert.Equal(14, perPiece.ResolvedNavigationBakeThreads);
        Assert.Equal(9, perPiece.ResolvedWorldPopulationPlanThreads);
    }

    [Fact]
    public void WithNothingConfigured_EachPieceFollowsItsOwnAutomaticRule()
    {
        // The important default: a machine nobody has configured does not get one thread count for
        // everything. 0 here means "0 to the resolver", which is what makes the bake take the whole
        // box and the plan build take half of it.
        var settings = new GameServerSettings();

        Assert.Equal(0, settings.ResolvedNavigationBakeThreads);
        Assert.Equal(0, settings.ResolvedWorldPopulationPlanThreads);

        Assert.InRange(ParallelWork.ResolveBake(settings.ResolvedNavigationBakeThreads), 1, ParallelWork.MaxBakeDegree);
        Assert.InRange(ParallelWork.ResolvePlan(settings.ResolvedWorldPopulationPlanThreads), 1, ParallelWork.MaxPlanDegree);
    }

    [Fact]
    public void AppConfig_MapsEveryWorldPopulationKeyToSettings()
    {
        var appSettings = AppConfigFile.ParseAppSettings(
            """
            <configuration><appSettings>
                <add key="SpawnWorldPopulation" value="false"/>
                <add key="WorldPopulationSpawnFullZone" value="true"/>
                <add key="WorldPopulationMaxLiveNpcs" value="75"/>
                <add key="WorldPopulationActivationRadius" value="80"/>
                <add key="WorldPopulationDeactivationRadius" value="135"/>
                <add key="WorldPopulationCellSize" value="16"/>
                <add key="WorldPopulationMaxNpcsPerCell" value="3"/>
                <add key="WorldPopulationMaxDifficultyPerCell" value="300"/>
                <add key="WorldPopulationUnbudgetedDifficultyCost" value="5"/>
                <add key="WorldPopulationMaxPlannedSlots" value="1234"/>
                <add key="WorldPopulationSpawnBudget" value="2"/>
                <add key="WorldPopulationSpawnBudgetWindowMs" value="50"/>
                <add key="WorldPopulationTickIntervalMs" value="125"/>
                <add key="WorldPopulationPlanWorkPerTick" value="500"/>
                <add key="WorldPopulationMinSeparation" value="0.25"/>
                <add key="WorldPopulationMinPlayerDistance" value="0"/>
                <add key="WorldPopulationMaxPlacementAttempts" value="3"/>
                <add key="WorldPopulationPlacementAttemptsPerUpdate" value="7"/>
                <add key="WorldPopulationPlacementRetryDelayMs" value="0"/>
                <add key="WorldPopulationMaxPlacementFailures" value="2"/>
                <add key="WorldPopulationRespawnDelayMs" value="0"/>
                <add key="WorldPopulationMinimumWalkableNormalZ" value="0.5"/>
                <add key="WorldPopulationDefaultBodyRadius" value="0.8"/>
                <add key="WorldPopulationDefaultBodyHeight" value="2"/>
                <add key="WorldPopulationDeployableInfluenceRadius" value="0"/>
                <add key="WorldPopulationOutpostSettlementRadius" value="0"/>
                <add key="WorldPopulationMeldingInfluenceRadius" value="55"/>
            </appSettings></configuration>
            """,
            "test");
        var settings = new GameServerSettings();

        GameServerModule.ApplyWorldPopulationSettings(appSettings, settings);

        Assert.False(settings.SpawnWorldPopulation);
        Assert.True(settings.WorldPopulationSpawnFullZone);
        Assert.Equal(75, settings.WorldPopulationMaxLiveNpcs);
        Assert.Equal(80f, settings.WorldPopulationActivationRadius);
        Assert.Equal((float?)135f, settings.WorldPopulationDeactivationRadius);
        Assert.Equal(16f, settings.WorldPopulationCellSize);
        Assert.Equal(3, settings.WorldPopulationMaxNpcsPerCell);
        Assert.Equal(300, settings.WorldPopulationMaxDifficultyPerCell);
        Assert.Equal(5, settings.WorldPopulationUnbudgetedDifficultyCost);
        Assert.Equal(1_234, settings.WorldPopulationMaxPlannedSlots);
        Assert.Equal(2, settings.WorldPopulationSpawnBudget);
        Assert.Equal(50, settings.WorldPopulationSpawnBudgetWindowMs);
        Assert.Equal(125, settings.WorldPopulationTickIntervalMs);
        Assert.Equal(500, settings.WorldPopulationPlanWorkPerTick);
        Assert.Equal(0.25f, settings.WorldPopulationMinSeparation);
        Assert.Equal(0f, settings.WorldPopulationMinPlayerDistance);
        Assert.Equal(3, settings.WorldPopulationMaxPlacementAttempts);
        Assert.Equal(7, settings.WorldPopulationPlacementAttemptsPerUpdate);
        Assert.Equal(0, settings.WorldPopulationPlacementRetryDelayMs);
        Assert.Equal(2, settings.WorldPopulationMaxPlacementFailures);
        Assert.Equal(0, settings.WorldPopulationRespawnDelayMs);
        Assert.Equal(0.5f, settings.WorldPopulationMinimumWalkableNormalZ);
        Assert.Equal(0.8f, settings.WorldPopulationDefaultBodyRadius);
        Assert.Equal(2f, settings.WorldPopulationDefaultBodyHeight);
        Assert.Equal(0f, settings.WorldPopulationDeployableInfluenceRadius);
        Assert.Equal(0f, settings.WorldPopulationOutpostSettlementRadius);
        Assert.Equal(55f, settings.WorldPopulationMeldingInfluenceRadius);
    }

    [Fact]
    public void FromSettings_UsesTheLegacyDeactivationRatioWhenNoExplicitRadiusExists()
    {
        var rules = StandardWorldPopulationRules.FromSettings(new GameServerSettings
        {
            WorldPopulationActivationRadius = 80f,
        });

        Assert.Equal(80f, rules.ActivationRadius);
        Assert.Equal(120f, rules.DeactivationRadius);
    }
}
