using System.Numerics;

namespace GameServer.Systems.Spawning.Population;

/// <summary>
///     The ground world population plans on and validates against: where the loaded zone says an
///     NPC can stand, which chunk a spot belongs to, and whether a body actually fits at a spot.
///     Split out from the planner and the service so both can be tested against fixed ground
///     instead of a loaded zone; the production implementation is
///     <see cref="PhysicsWorldPopulationTerrain"/>, which reads the zone through
///     <see cref="Physics.PhysicsEngine"/>.
/// </summary>
public interface IWorldPopulationTerrain
{
    /// <summary>
    ///     Whether the zone supplies walkable surface points at all. When it does not (no maps or
    ///     collision configured) the planner falls back to the authored anchor positions, which are
    ///     the only spots left whose height the data vouches for.
    /// </summary>
    bool HasSurfaces { get; }

    /// <summary>How many walkable surface points the zone has.</summary>
    int SurfaceCount { get; }

    /// <summary>
    ///     One walkable surface point by index, in mesh order. A point here is a spot the zone's own
    ///     collision considers standable: walkable slope, and not excluded from pathing by the chunk
    ///     metadata the navigation mesh was baked with.
    /// </summary>
    bool TryGetSurface(int index, out Vector3 position);

    /// <summary>
    ///     The <c>dbzonemetadata::ChunkRecord</c> id of the chunk <paramref name="position"/> falls
    ///     in, or 0 when the zone's chunks are unknown. The plan asks
    ///     <see cref="IWorldPopulationDataSource.IsChunkSpawnable"/> about the answer.
    /// </summary>
    uint GetChunkRecordId(Vector3 position);

    /// <summary>
    ///     Physically validates one candidate standing spot for a body of the given size: snaps it
    ///     onto the surface it belongs to, refuses it when that surface is too steep to stand on or
    ///     the body would end up inside the world or inside another entity, and returns the spot the
    ///     body should actually be placed at.
    /// </summary>
    /// <param name="candidate">The planned spot.</param>
    /// <param name="bodyRadius">Body radius in metres.</param>
    /// <param name="bodyHeight">Body height in metres.</param>
    /// <param name="position">The validated spot; <paramref name="candidate"/> when nothing moved it.</param>
    /// <returns>Whether a body of that size can stand there.</returns>
    bool TryResolveStandingSpot(Vector3 candidate, float bodyRadius, float bodyHeight, out Vector3 position);

    /// <summary>Zone bounds from ZoneBoundsLayer (0x21000) if present, from actual client map file.</summary>
    Vector3? ZoneBoundsMin { get; }
    Vector3? ZoneBoundsMax { get; }

    /// <summary>Whether position is inside zone bounds, or true when no bounds are known.</summary>
    bool IsInsideZoneBounds(Vector3 position);
}
