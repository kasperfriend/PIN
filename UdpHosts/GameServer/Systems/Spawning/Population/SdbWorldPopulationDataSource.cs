using System;
using System.Numerics;
using System.Collections.Generic;
using System.Linq;
using GameServer.StaticDB;
using Serilog;

namespace GameServer.Systems.Spawning.Population;

/// <summary>
///     Reads world population's inputs from the shipped database: the monster rows through
///     <see cref="SDBInterface"/>, the zone's habitats and levels through
///     <see cref="CustomDBInterface"/> (the same custom data
///     <see cref="Systems.EntityManager.EntityManager.SpawnZoneEntities"/> spawns the zone's own
///     entities from) and the chunk rules through <c>dbzonemetadata</c>.
/// </summary>
/// <remarks>
///     Everything here is read once per plan build, on the shard's tick thread; the only state kept
///     between calls is the per zone chunk lookup. Nothing is written.
/// </remarks>
public sealed class SdbWorldPopulationDataSource : IWorldPopulationDataSource
{
    private static readonly ILogger _logger = Log.ForContext<SdbWorldPopulationDataSource>();

    /// <summary><c>clientonly</c> by chunk id, per zone, so a chunk lookup is a dictionary hit.</summary>
    private readonly Dictionary<uint, Dictionary<uint, byte>> _clientOnlyByZone = [];

    public IReadOnlyList<WorldPopulationCandidate> GetCandidates()
    {
        var monsters = SDBInterface.GetMonsters();
        if (monsters == null || monsters.Count == 0)
        {
            _logger.Warning("World population: no dbcharacter::Monster rows are loaded, so the zone will stay empty");
            return Array.Empty<WorldPopulationCandidate>();
        }

        var candidates = new List<WorldPopulationCandidate>(monsters.Count);
        var excluded = new Dictionary<string, int>(StringComparer.Ordinal);

        // Ascending id order: the same database always produces the same roster, so the same zone
        // plans the same way on every server and every run.
        foreach (var monster in monsters.Values.OrderBy(row => row.Id))
        {
            bool admitted = MonsterHabitatClassifier.TryClassify(
                monster.Behavior,
                monster.VendorId,
                FactionName(monster.FactionId),
                monster.ChassisId != 0 || monster.PosetypeId != 0,
                out var habitat,
                out string exclusion);

            if (!admitted)
            {
                excluded[exclusion] = excluded.GetValueOrDefault(exclusion) + 1;
                continue;
            }

            candidates.Add(new WorldPopulationCandidate
            {
                MonsterId = monster.Id,
                Habitat = habitat,
                BodyRadius = monster.BodyRadius,
                BodyHeight = monster.BodyHeight,
                DifficultyCost = monster.DifficultyCost,
                SpawnDelayMs = monster.AiSpawnDelayMs > int.MaxValue ? int.MaxValue : (int)monster.AiSpawnDelayMs,
                Weight = WorldPopulationCandidate.DensityWeight(monster.DifficultyCost),
            });
        }

        _logger.Information(
            "World population roster: {Admitted} of {Total} dbcharacter::Monster rows are world population ({Excluded} refused)",
            candidates.Count,
            monsters.Count,
            string.Join(", ", excluded.OrderByDescending(pair => pair.Value).Select(pair => $"{pair.Value} {pair.Key}")));

        return candidates;
    }

    public IReadOnlyList<WorldPopulationAnchor> GetAnchors(uint zoneId)
    {
        var anchors = new List<WorldPopulationAnchor>();
        int outposts = 0;
        int deployables = 0;
        int meldingPoints = 0;

        // Outposts carry both: their own authored radius (150-550 m in Coral Forest) and the level
        // band of the area around them (1-5 at the starter outpost up to 29-30 in the far corners
        // of the same zone), which is the level gradient the game gave the zone.
        foreach (var outpost in CustomDBInterface.GetZoneOutposts(zoneId).Values)
        {
            anchors.Add(new WorldPopulationAnchor(
                outpost.Position,
                outpost.Radius,
                WorldPopulationHabitat.Settlement,
                outpost.LevelBandId));
            outposts++;
        }

        // Deployables (469 of them in Coral Forest: watchtowers, thumper pads, terminals) mark the
        // smaller settled places. The data has no radius for them, so the planner sizes them.
        foreach (var deployable in CustomDBInterface.GetZoneDeployables(zoneId).Values)
        {
            anchors.Add(new WorldPopulationAnchor(
                deployable.Position,
                0f,
                WorldPopulationHabitat.Settlement,
                0u));
            deployables++;
        }

        // The Melding's perimeters are splines of control points (4-23 per Melding, 16 Meldings in
        // Coral Forest); every control point anchors the ground around it as Melding. The shipped
        // points are the spline knots - treating them as isolated 120m circles leaves gaps along
        // the wall between knots. Interpolating the edges with 60m steps makes the habitat
        // classification follow the perimeter line instead of a dotted line, still using only
        // the shipped control points and the configured MeldingInfluenceRadius.
        foreach (var melding in CustomDBInterface.GetZoneMeldings(zoneId).Values)
        {
            var points = melding.ControlPoints;
            foreach (var controlPoint in points)
            {
                anchors.Add(new WorldPopulationAnchor(
                    controlPoint,
                    0f,
                    WorldPopulationHabitat.Melding,
                    0u));
                meldingPoints++;
            }

            if (points.Count > 1)
            {
                for (int i = 0; i < points.Count; i++)
                {
                    var a = points[i];
                    var b = points[(i + 1) % points.Count];
                    float edge = GameServer.Systems.Ai.AiVectors.HorizontalDistance(a, b);
                    if (!float.IsFinite(edge) || edge <= 60f)
                        continue;
                    int steps = (int)(edge / 60f);
                    for (int s = 1; s < steps; s++)
                    {
                        float t = s / (float)steps;
                        var mid = new System.Numerics.Vector3(
                            a.X + ((b.X - a.X) * t),
                            a.Y + ((b.Y - a.Y) * t),
                            a.Z + ((b.Z - a.Z) * t));
                        anchors.Add(new WorldPopulationAnchor(mid, 0f, WorldPopulationHabitat.Melding, 0u));
                        meldingPoints++;
                    }
                }
            }
        }

        _logger.Information(
            "World population anchors for zone {ZoneId}: {Outposts} outposts, {Deployables} deployables, {MeldingPoints} Melding control points",
            zoneId,
            outposts,
            deployables,
            meldingPoints);

        return anchors;
    }

    public byte ResolveLevel(uint zoneId, uint levelBandId)
    {
        if (levelBandId != 0)
        {
            var level = SDBUtils.ResolveNpcLevel(SDBInterface.GetLevelBand(levelBandId));
            if (level != 0)
            {
                return level;
            }
        }

        // No usable band at this spot: fall back to the zone's own band, which
        // SDBUtils.ResolveNpcLevel turns into SDBUtils.DefaultNpcLevel when the zone has none.
        return SDBUtils.ResolveNpcLevel(zoneId);
    }

    public bool IsChunkSpawnable(uint zoneId, uint chunkRecordId)
    {
        if (chunkRecordId == 0)
        {
            // The position did not fall inside a chunk the zone loader knows (a zone loaded from a
            // cache, or a point just outside the chunk grid). Nothing says it is client only.
            return true;
        }

        if (ClientOnlyByChunk(zoneId).TryGetValue(chunkRecordId, out byte clientOnly) && clientOnly != 0)
        {
            return false;
        }

        var chunk = SDBInterface.GetChunkRecord(chunkRecordId);
        return chunk == null || chunk.RemoveInProduction == 0;
    }

    private Dictionary<uint, byte> ClientOnlyByChunk(uint zoneId)
    {
        if (_clientOnlyByZone.TryGetValue(zoneId, out var cached))
        {
            return cached;
        }

        var links = new Dictionary<uint, byte>();
        foreach (var link in SDBInterface.GetZoneChunks(zoneId))
        {
            links[link.Chunkid] = link.Clientonly;
        }

        _clientOnlyByZone[zoneId] = links;
        return links;
    }

    private static string FactionName(uint factionId) =>
        factionId == 0 ? null : SDBInterface.GetFaction(factionId)?.InternalName;
}
