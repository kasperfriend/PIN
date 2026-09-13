using System.Collections.Generic;

namespace GameServer.Systems.Spawning.Population;

/// <summary>
///     The database side of world population: which monster rows are population, where the zone's
///     habitats and levels are, and which chunks the zone's own metadata refuses. Split out from
///     <see cref="WorldPopulationPlanner"/> so the plan can be built and tested against fixed data
///     instead of a loaded <c>clientdb.sd2</c>; the production implementation is
///     <see cref="SdbWorldPopulationDataSource"/>.
/// </summary>
public interface IWorldPopulationDataSource
{
    /// <summary>
    ///     Every monster row that may be spawned as ambient world population, in ascending
    ///     <see cref="WorldPopulationCandidate.MonsterId"/> order so a plan built from the same
    ///     database is always the same plan.
    /// </summary>
    IReadOnlyList<WorldPopulationCandidate> GetCandidates();

    /// <summary>The places in <paramref name="zoneId"/> that give the ground around them a habitat and a level.</summary>
    IReadOnlyList<WorldPopulationAnchor> GetAnchors(uint zoneId);

    /// <summary>
    ///     The level an NPC gets at a place carrying <paramref name="levelBandId"/>, falling back to
    ///     the zone's own band (and to <see cref="StaticDB.SDBUtils.DefaultNpcLevel"/>) when the band
    ///     is absent or unusable. Never returns 0.
    /// </summary>
    byte ResolveLevel(uint zoneId, uint levelBandId);

    /// <summary>
    ///     Whether the plan may place NPCs in <paramref name="chunkRecordId"/> of
    ///     <paramref name="zoneId"/>. Answers from the zone's own chunk metadata:
    ///     <c>dbzonemetadata::ZoneChunkLinker.clientonly</c> (chunks the server never simulated) and
    ///     <c>dbzonemetadata::ChunkRecord.remove_in_production</c> (chunks stripped from the shipped
    ///     build). An unknown chunk is allowed: a zone whose collision came from a cache without
    ///     chunk records should still populate.
    /// </summary>
    bool IsChunkSpawnable(uint zoneId, uint chunkRecordId);
}
