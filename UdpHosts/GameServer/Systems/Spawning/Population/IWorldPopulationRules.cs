namespace GameServer.Systems.Spawning.Population;

/// <summary>
///     Every number the world population system is allowed to decide by itself. The defaults live
///     in <see cref="StandardWorldPopulationRules"/>; the operator facing ones (the toggle, the
///     live cap and the activation radius) are read from <c>App.config</c>, and a test or a tuning
///     pass can replace the whole set by handing a custom implementation to
///     <see cref="WorldPopulationService"/> — the same seam <see cref="Systems.Ai.IAiRules"/> gives
///     the AI.
/// </summary>
public interface IWorldPopulationRules
{
    /// <summary>Whether world population runs at all. Off means: no plan, and anything already spawned is removed.</summary>
    bool Enabled { get; }

    /// <summary>
    ///     Hard ceiling on how many population NPCs may be alive at once, whatever the plan says.
    ///     This is the number that keeps the server's cost bounded: every live NPC is an entity, a
    ///     physics body, an AI brain and a stream of keyframes.
    /// </summary>
    int MaxLiveNpcs { get; }

    /// <summary>
    ///     Metres from a player within which a planned cell is activated (its NPCs spawned).
    ///     Chosen to sit inside a character's scope range so a mob that materialises is also one
    ///     the client is told about.
    /// </summary>
    float ActivationRadius { get; }

    /// <summary>
    ///     Metres from a player beyond which an active cell is deactivated (its NPCs removed).
    ///     Must be larger than <see cref="ActivationRadius"/>: the gap is hysteresis, so a player
    ///     standing on the boundary does not make the same cell spawn and despawn every tick.
    /// </summary>
    float DeactivationRadius { get; }

    /// <summary>
    ///     Side length in metres of one planning cell. The cell is the unit of both the plan and
    ///     the streaming: cells are what get activated near a player, and the per cell caps below
    ///     are per this square. 32 m is a little over twice a monster's perception radius, so a
    ///     cell reads as one encounter-sized patch of ground.
    /// </summary>
    float CellSize { get; }

    /// <summary>Most NPCs one cell may hold, whatever their difficulty.</summary>
    int MaxNpcsPerCell { get; }

    /// <summary>
    ///     Total <c>dbcharacter::Monster.difficulty_cost</c> one cell may hold. The column is the
    ///     database's own budget figure for encounter design (its values run 0 for ambient rows
    ///     through 20-100 for standard mobs to 300-1000 for minibosses), so reading it as a per
    ///     patch budget reproduces the original game's "a few trash mobs or one big one" shape.
    ///     Enforced while filling for density; the first slot of every monster row is exempt so
    ///     that no row is priced out of the zone it belongs to.
    /// </summary>
    int MaxDifficultyPerCell { get; }

    /// <summary>
    ///     Difficulty charged to a row whose <c>difficulty_cost</c> is 0. Most ambient rows
    ///     (2,203 of 3,109) carry no cost because they were never part of a tuned encounter; giving
    ///     them a nominal one keeps a cell from filling with unlimited free NPCs.
    /// </summary>
    int UnbudgetedDifficultyCost { get; }

    /// <summary>
    ///     Ceiling on how many slots the plan may hold for the whole zone. Bounds the memory and
    ///     the build time of a plan that is mostly never activated.
    /// </summary>
    int MaxPlannedSlots { get; }

    /// <summary>Most NPCs spawned inside one <see cref="SpawnBudgetWindowMs"/> window.</summary>
    int SpawnBudget { get; }

    /// <summary>Length in milliseconds of the spawn budget window.</summary>
    int SpawnBudgetWindowMs { get; }

    /// <summary>
    ///     Milliseconds between two population updates. The shard ticks every 5 ms; population is
    ///     a streaming concern and does not need that resolution, and running it less often is what
    ///     keeps its cost off the tick budget.
    /// </summary>
    int TickIntervalMs { get; }

    /// <summary>
    ///     How many navigation mesh faces the plan builder walks per update while it is still
    ///     building. The mesh of a full zone has up to a few hundred thousand faces, so the scan is
    ///     spread over several ticks instead of stalling the first one.
    /// </summary>
    int PlanWorkPerTick { get; }

    /// <summary>
    ///     Extra metres of gap required between two bodies. Added to the radii, so two 0.7 m mobs
    ///     need 1.9 m between their centres at the default value.
    /// </summary>
    float MinSeparation { get; }

    /// <summary>
    ///     Metres of clearance required from every player before an NPC may be placed. Without it a
    ///     cell the player is standing in would materialise mobs on top of them.
    /// </summary>
    float MinPlayerDistance { get; }

    /// <summary>How many positions one slot tries before it gives up this round (its own anchor first, then jittered ones).</summary>
    int MaxPlacementAttempts { get; }

    /// <summary>Milliseconds a slot waits after a failed round before it is tried again.</summary>
    int PlacementRetryDelayMs { get; }

    /// <summary>
    ///     Failed rounds after which a slot is parked: the ground it wanted is permanently
    ///     unwalkable (too steep, blocked, always occupied) and retrying it forever would be a spin.
    /// </summary>
    int MaxPlacementFailures { get; }

    /// <summary>
    ///     Milliseconds a slot waits after its NPC died before it refills. Added to the row's own
    ///     <c>ai_spawn_delay_ms</c>, so a respawning mob is not standing there the instant the
    ///     corpse is gone.
    /// </summary>
    int RespawnDelayMs { get; }

    /// <summary>
    ///     Lowest surface normal Z a spawn position may sit on. Matches
    ///     <see cref="Shared.Collision.Navigation.NavigationMesh"/>'s own walkability cutoff, so
    ///     the population system never calls ground what the navigation mesh already refused.
    /// </summary>
    float MinimumWalkableNormalZ { get; }

    /// <summary>
    ///     Body radius used for a row whose <c>body_radius</c> is the <c>-1</c> "inherit" sentinel
    ///     (3,103 of the 3,109 rows). Same fallback the AI uses for its navigation agent radius.
    /// </summary>
    float DefaultBodyRadius { get; }

    /// <summary>Body height used for a row whose <c>body_height</c> is the <c>-1</c> "inherit" sentinel.</summary>
    float DefaultBodyHeight { get; }

    /// <summary>
    ///     Metres around one of the zone's authored deployables that count as settlement ground.
    ///     The deployables in <c>StaticDB/CustomData/deployable.json</c> carry positions but no
    ///     radius, so this is the size of the clearing a deployable implies.
    /// </summary>
    float DeployableInfluenceRadius { get; }

    /// <summary>
    ///     Metres around one Melding control point that count as Melding ground. The Melding's
    ///     perimeters in <c>StaticDB/CustomData/melding.json</c> are polylines of control points;
    ///     this is how far inland the Melding's own creatures are placed from one.
    /// </summary>
    float MeldingInfluenceRadius { get; }
}
