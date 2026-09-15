namespace GameServer.Systems.Spawning.Population;

/// <summary>
///     The out of the box world population tuning. Every value can be replaced by passing a custom
///     <see cref="IWorldPopulationRules"/> to <see cref="WorldPopulationService"/> or configured
///     through <c>App.config</c> with <see cref="FromSettings(GameServerSettings)"/>.
/// </summary>
/// <remarks>
///     The defaults describe the "balanced" preset: a zone around one player carries a few hundred
///     mobs rather than the tens of thousands the plan could hold, they appear when the player is
///     still too far to notice the pop in, and they go away once the player has left the area for
///     good. See <c>Docs/WORLD_POPULATION.md</c> for what each number costs.
/// </remarks>
public class StandardWorldPopulationRules : IWorldPopulationRules
{
    public bool Enabled { get; init; } = true;

    /// <summary>
    ///     When true, spawns the full population across the entire zone rather than streaming
    ///     cells near players, and keeps all spawned NPCs without despawning them when players
    ///     move away or leave the zone.
    /// </summary>
    public bool SpawnFullZone { get; init; }

    /// <summary>
    ///     150 live NPCs keeps a zone visibly populated while leaving enough CPU, physics and
    ///     reliable-channel headroom for actual combat. A shard with several players shares the
    ///     same cap, which is the point: the cost is bounded by this number, not by how many
    ///     players are connected. Operators who have measured headroom can raise it in config.
    /// </summary>
    public int MaxLiveNpcs { get; init; } = 150;

    public float ActivationRadius { get; init; } = 150f;

    /// <summary>
    ///     1.5x the activation radius. The 75 m gap is wider than a player covers in the ~2 s it
    ///     takes to walk it, so leaving and coming back does not thrash a cell.
    /// </summary>
    public float DeactivationRadius { get; init; } = 225f;

    public float CellSize { get; init; } = 32f;

    public int MaxNpcsPerCell { get; init; } = 4;

    public int MaxDifficultyPerCell { get; init; } = 400;

    public int UnbudgetedDifficultyCost { get; init; } = 25;

    public int MaxPlannedSlots { get; init; } = 20_000;

    /// <summary>
    ///     Four spawns per 100 ms = 40/s worst case. Each spawn is an entity, a physics body, an AI
    ///     registration and a scope-in to the players who can see it. This deliberately leaves room
    ///     for the zone's existing traffic rather than competing with it during a streaming burst.
    /// </summary>
    public int SpawnBudget { get; init; } = 4;

    public int SpawnBudgetWindowMs { get; init; } = 100;

    public int TickIntervalMs { get; init; } = 250;

    /// <summary>
    ///     A full zone's navigation mesh has up to a few hundred thousand walkable faces; at 20,000
    ///     faces per 250 ms update a large zone is planned in a couple of seconds, spread over
    ///     ticks that each stay well under a millisecond of extra work.
    /// </summary>
    public int PlanWorkPerTick { get; init; } = 20_000;

    public float MinSeparation { get; init; } = 0.5f;

    public float MinPlayerDistance { get; init; } = 25f;

    public int MaxPlacementAttempts { get; init; } = 6;

    public int PlacementRetryDelayMs { get; init; } = 1_000;

    public int MaxPlacementFailures { get; init; } = 8;

    public int RespawnDelayMs { get; init; } = 30_000;

    public float MinimumWalkableNormalZ { get; init; } = 0.35f;

    public float DefaultBodyRadius { get; init; } = 0.7f;

    public float DefaultBodyHeight { get; init; } = 1.8f;

    public float DeployableInfluenceRadius { get; init; } = 25f;

    public float MeldingInfluenceRadius { get; init; } = 120f;

    /// <summary>
    ///     Builds the rules from the server's settings. Null settings (which is what the test
    ///     shard carries) give the defaults. Invalid values fail independently: one malformed or
    ///     unsafe setting cannot disable the other explicitly configured population limits.
    /// </summary>
    public static StandardWorldPopulationRules FromSettings(GameServerSettings settings)
    {
        if (settings == null)
        {
            return new StandardWorldPopulationRules();
        }

        float activationRadius = PositiveOrDefault(settings.WorldPopulationActivationRadius, 150f);
        float requestedDeactivationRadius = settings.WorldPopulationDeactivationRadius.GetValueOrDefault();

        return new StandardWorldPopulationRules
        {
            Enabled = settings.SpawnWorldPopulation,
            SpawnFullZone = settings.WorldPopulationSpawnFullZone,
            MaxLiveNpcs = PositiveOrDefault(settings.WorldPopulationMaxLiveNpcs, 150),
            ActivationRadius = activationRadius,
            // Older App.config files did not have this key. Preserve their documented 1.5x
            // relationship instead of unexpectedly widening a custom activation radius.
            DeactivationRadius = requestedDeactivationRadius > activationRadius && float.IsFinite(requestedDeactivationRadius)
                ? requestedDeactivationRadius
                : activationRadius * 1.5f,
            CellSize = PositiveOrDefault(settings.WorldPopulationCellSize, 32f),
            MaxNpcsPerCell = PositiveOrDefault(settings.WorldPopulationMaxNpcsPerCell, 4),
            MaxDifficultyPerCell = NonNegativeOrDefault(settings.WorldPopulationMaxDifficultyPerCell, 400),
            UnbudgetedDifficultyCost = NonNegativeOrDefault(settings.WorldPopulationUnbudgetedDifficultyCost, 25),
            MaxPlannedSlots = PositiveOrDefault(settings.WorldPopulationMaxPlannedSlots, 20_000),
            SpawnBudget = PositiveOrDefault(settings.WorldPopulationSpawnBudget, 4),
            SpawnBudgetWindowMs = PositiveOrDefault(settings.WorldPopulationSpawnBudgetWindowMs, 100),
            TickIntervalMs = PositiveOrDefault(settings.WorldPopulationTickIntervalMs, 250),
            PlanWorkPerTick = PositiveOrDefault(settings.WorldPopulationPlanWorkPerTick, 20_000),
            MinSeparation = NonNegativeOrDefault(settings.WorldPopulationMinSeparation, 0.5f),
            MinPlayerDistance = NonNegativeOrDefault(settings.WorldPopulationMinPlayerDistance, 25f),
            MaxPlacementAttempts = PositiveOrDefault(settings.WorldPopulationMaxPlacementAttempts, 6),
            PlacementRetryDelayMs = NonNegativeOrDefault(settings.WorldPopulationPlacementRetryDelayMs, 1_000),
            MaxPlacementFailures = PositiveOrDefault(settings.WorldPopulationMaxPlacementFailures, 8),
            RespawnDelayMs = NonNegativeOrDefault(settings.WorldPopulationRespawnDelayMs, 30_000),
            MinimumWalkableNormalZ = WalkableNormalOrDefault(settings.WorldPopulationMinimumWalkableNormalZ, 0.35f),
            DefaultBodyRadius = PositiveOrDefault(settings.WorldPopulationDefaultBodyRadius, 0.7f),
            DefaultBodyHeight = PositiveOrDefault(settings.WorldPopulationDefaultBodyHeight, 1.8f),
            DeployableInfluenceRadius = NonNegativeOrDefault(settings.WorldPopulationDeployableInfluenceRadius, 25f),
            MeldingInfluenceRadius = NonNegativeOrDefault(settings.WorldPopulationMeldingInfluenceRadius, 120f),
        };
    }

    private static int PositiveOrDefault(int value, int defaultValue) => value > 0 ? value : defaultValue;

    private static int NonNegativeOrDefault(int value, int defaultValue) => value >= 0 ? value : defaultValue;

    private static float PositiveOrDefault(float value, float defaultValue) => value > 0f && float.IsFinite(value) ? value : defaultValue;

    private static float NonNegativeOrDefault(float value, float defaultValue) => value >= 0f && float.IsFinite(value) ? value : defaultValue;

    private static float WalkableNormalOrDefault(float value, float defaultValue) =>
        value > 0f && value <= 1f && float.IsFinite(value) ? value : defaultValue;
}
