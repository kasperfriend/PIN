namespace GameServer.Systems.Spawning.Population;

/// <summary>
///     The out of the box world population tuning. Every value can be replaced by passing a custom
///     <see cref="IWorldPopulationRules"/> to <see cref="WorldPopulationService"/>; the three the
///     operator is expected to touch come from <c>App.config</c> through
///     <see cref="FromSettings(GameServerSettings)"/>.
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
    ///     600 live NPCs is roughly what a single player's activation radius can cover at the
    ///     default density (a 200 m radius holds ~123 cells of 32 m, and a cell holds up to
    ///     <see cref="MaxNpcsPerCell"/>), so one player walking through a zone keeps the world
    ///     populated without the cap being what they run into. A shard with several players shares
    ///     the same cap, which is the point: the cost of the feature is bounded by this number, not
    ///     by how many players are connected.
    /// </summary>
    public int MaxLiveNpcs { get; init; } = 600;

    public float ActivationRadius { get; init; } = 200f;

    /// <summary>
    ///     1.5x the activation radius. The 100 m gap is wider than a player covers in the ~2 s it
    ///     takes to walk it, so leaving and coming back does not thrash a cell.
    /// </summary>
    public float DeactivationRadius { get; init; } = 300f;

    public float CellSize { get; init; } = 32f;

    public int MaxNpcsPerCell { get; init; } = 4;

    public int MaxDifficultyPerCell { get; init; } = 400;

    public int UnbudgetedDifficultyCost { get; init; } = 25;

    public int MaxPlannedSlots { get; init; } = 20_000;

    /// <summary>
    ///     12 spawns per 100 ms = 120/s worst case. Each spawn is an entity, a physics body, an AI
    ///     registration and a scope-in to the players who can see it, and
    ///     <see cref="Systems.EntityManager.EntityManager"/> drains its scope-in queue at 16 per
    ///     20 ms (800/s) — this budget stays an order of magnitude under that drain rate, so a
    ///     player walking into an empty area cannot make the scope queue grow.
    /// </summary>
    public int SpawnBudget { get; init; } = 12;

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
    ///     shard carries) give the defaults.
    /// </summary>
    public static StandardWorldPopulationRules FromSettings(GameServerSettings settings)
    {
        if (settings == null)
        {
            return new StandardWorldPopulationRules();
        }

        return new StandardWorldPopulationRules
        {
            Enabled = settings.SpawnWorldPopulation,
            MaxLiveNpcs = settings.WorldPopulationMaxLiveNpcs > 0 ? settings.WorldPopulationMaxLiveNpcs : 600,
            ActivationRadius = settings.WorldPopulationActivationRadius > 0f ? settings.WorldPopulationActivationRadius : 200f,
            DeactivationRadius = settings.WorldPopulationActivationRadius > 0f
                ? settings.WorldPopulationActivationRadius * 1.5f
                : 300f,
        };
    }
}
