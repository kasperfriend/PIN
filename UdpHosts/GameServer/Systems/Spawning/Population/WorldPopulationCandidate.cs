using System.Numerics;

namespace GameServer.Systems.Spawning.Population;

/// <summary>
///     One <c>dbcharacter::Monster</c> row that the classifier admitted as world population, with
///     the handful of columns the plan needs already read off it. Built once per plan by
///     <see cref="IWorldPopulationDataSource.GetCandidates"/>.
/// </summary>
public sealed class WorldPopulationCandidate
{
    /// <summary>The <c>dbcharacter::Monster</c> id, i.e. what <c>EntityManager.SpawnCharacter</c> takes.</summary>
    public uint MonsterId { get; init; }

    /// <summary>The kinds of ground this row may be placed in.</summary>
    public WorldPopulationHabitat Habitat { get; init; }

    /// <summary>
    ///     The row's <c>body_radius</c> in metres. Non-positive means the row carries the <c>-1</c>
    ///     "inherit" sentinel (3,103 of the 3,109 rows do) — resolve it with
    ///     <see cref="ResolvedBodyRadius"/>.
    /// </summary>
    public float BodyRadius { get; init; }

    /// <summary>The row's <c>body_height</c> in metres, with the same sentinel meaning as <see cref="BodyRadius"/>.</summary>
    public float BodyHeight { get; init; }

    /// <summary>
    ///     The row's <c>difficulty_cost</c> — the database's own encounter budget figure, 0 for
    ///     rows that were never part of a tuned encounter. The planner charges
    ///     <see cref="IWorldPopulationRules.UnbudgetedDifficultyCost"/> for those.
    /// </summary>
    public uint DifficultyCost { get; init; }

    /// <summary>
    ///     The row's <c>ai_spawn_delay_ms</c>: how long the original game let a spawn take before
    ///     the NPC became active (2,000 ms for 2,822 of the 3,109 rows). Used as the delay between
    ///     a cell being activated and its NPCs appearing, which both honours the column and spreads
    ///     a cell's spawns over time instead of letting them all land in one tick.
    /// </summary>
    public int SpawnDelayMs { get; init; }

    /// <summary>
    ///     How often this row is picked when a cell is filled for density, relative to the other
    ///     rows of its habitat. See <see cref="DensityWeight"/>.
    /// </summary>
    public int Weight { get; init; } = 1;

    /// <summary>
    ///     The body radius to plan and collide with: the row's own when it carries one, otherwise
    ///     the rules' default. The same fallback the AI uses for its navigation agent radius, so a
    ///     mob is given as much room standing as it is walking.
    /// </summary>
    public float ResolvedBodyRadius(IWorldPopulationRules rules) =>
        BodyRadius > 0f ? BodyRadius : rules.DefaultBodyRadius;

    /// <summary>The body height to plan and collide with, resolved like <see cref="ResolvedBodyRadius"/>.</summary>
    public float ResolvedBodyHeight(IWorldPopulationRules rules) =>
        BodyHeight > 0f ? BodyHeight : rules.DefaultBodyHeight;

    /// <summary>
    ///     Relative frequency of a row in the ambient population, derived from its
    ///     <c>difficulty_cost</c>: the rows the game priced at nothing are its common ambient
    ///     content (civilians, small wildlife — 2,203 of 3,109 rows), and the ones it priced at
    ///     200-1000 are the rare minibosses that a patch of ground holds at most one of. The cost
    ///     is a balance figure rather than a spawn rate, so this is a reading of it, not a column
    ///     that says so; the shape it reproduces (many cheap, few expensive) is the one the
    ///     original game's encounters had.
    /// </summary>
    public static int DensityWeight(uint difficultyCost) => difficultyCost switch
    {
        0 => 8,
        <= 25 => 6,
        <= 60 => 4,
        <= 120 => 2,
        _ => 1,
    };
}

/// <summary>
///     A place in the loaded zone that gives the ground around it a habitat and a level: an outpost
///     (with its authored capture <c>radius</c> and <c>level_band_id</c>), one of the zone's
///     deployables, or one of the Melding's control points. Read from the same custom data
///     <see cref="Systems.EntityManager.EntityManager.SpawnZoneEntities"/> spawns the zone's own
///     entities from, so the plan's idea of "where the settlement is" is the server's idea of it.
///     An outpost's authored radius is the capture/control circle; settlement habitat uses
///     <see cref="IWorldPopulationRules.OutpostSettlementRadius"/> instead.
/// </summary>
/// <param name="Position">World position of the anchor.</param>
/// <param name="Radius">
///     Metres around <paramref name="Position"/> that the data authored for this anchor, or 0 when
///     it carries none (deployables and Melding control points do not) and the planner should use
///     <see cref="IWorldPopulationRules.DeployableInfluenceRadius"/> or
///     <see cref="IWorldPopulationRules.MeldingInfluenceRadius"/>. For an outpost this is the
///     capture/control circle, not the inhabited camp; the planner caps it with
///     <see cref="IWorldPopulationRules.OutpostSettlementRadius"/> when classifying habitat.
/// </param>
/// <param name="Habitat">The habitat the anchor gives the ground around it.</param>
/// <param name="LevelBandId">
///     <c>dbitems::LevelBand</c> id the area carries, or 0 when it has none and the zone's own band
///     applies.
/// </param>
public readonly record struct WorldPopulationAnchor(
    Vector3 Position,
    float Radius,
    WorldPopulationHabitat Habitat,
    uint LevelBandId);
