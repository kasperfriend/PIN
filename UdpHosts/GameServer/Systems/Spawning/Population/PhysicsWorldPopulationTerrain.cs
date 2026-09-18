using System;
using System.Collections.Generic;
using System.Numerics;
using GameServer.Physics;
using Serilog;
using Shared.Collision.ZoneLoading;

namespace GameServer.Systems.Spawning.Population;

/// <summary>
///     The zone's ground, as the physics engine knows it: walkable surface points come from the
///     navigation mesh baked from the zone's own collision, chunk ids from the chunk references the
///     zone file was loaded from, and standing room from ray casts plus a broad phase query against
///     the bodies that are already there.
/// </summary>
/// <remarks>
///     Tolerates a null engine (a shard that runs without physics, and the test shards): it then
///     reports no surfaces, so the planner falls back to the authored anchors and placement accepts
///     the planned height, which is what a spawn without collision data has always done.
/// </remarks>
public sealed class PhysicsWorldPopulationTerrain : IWorldPopulationTerrain
{
    /// <summary>
    ///     How far above a planned spot the ground probe starts. The same 1.5 m the AI's own ground
    ///     probe (<see cref="Systems.Ai.AiEngine"/>) uses, deliberately: a spot validated here is a
    ///     fixed point of the AI's snap, so an NPC placed here is not moved by the first AI tick.
    /// </summary>
    private const float GroundProbeUp = 1.5f;

    /// <summary>
    ///     How far below a planned spot the ground probe reaches. Short on purpose: the spot comes
    ///     from a surface the zone's collision baked (or from authored data that carries a real
    ///     height), so the surface it belongs to is at its feet. A long reach would snap a spot that
    ///     sits under a bridge or a roof onto whatever is below it and move the NPC out of the place
    ///     the plan chose.
    /// </summary>
    private const float GroundProbeDown = 3f;

    /// <summary>
    ///     How many standing spots the overhead-cover rule must have judged before it is allowed to
    ///     be called wrong about a whole zone. Six attempts per slot
    ///     (<see cref="IWorldPopulationRules.MaxPlacementAttempts"/>) means a handful of slots reach
    ///     this, so a zone the rule misreads starts spawning again within the first update that
    ///     tries to place, instead of parking its plan later on.
    /// </summary>
    private const int CoverChecksBeforeSuspicion = 200;

    /// <summary>
    ///     The refusal share - nine spots in ten - at or above which the overhead-cover rule counts
    ///     as wrong about the whole zone rather than right about individual spots. A zone with real
    ///     caves also has open ground for the rule to approve; one where it refuses almost every spot
    ///     over dozens of spots is the failure shape of the two checks #115 reverted (see
    ///     <see cref="SuspendCoverRuleIfItRefusesTheWholeZone"/>), and an empty world is the worse
    ///     outcome of the two.
    /// </summary>
    private const int CoverRefusalNumerator = 19;

    /// <summary>Denominator of <see cref="CoverRefusalNumerator"/>; integer arithmetic keeps it exact.</summary>
    private const int CoverRefusalDenominator = 20;

    private readonly PhysicsEngine _physics;
    private readonly IWorldPopulationRules _rules;

    /// <summary>
    ///     The one judgement this terrain makes about a whole zone rather than about one spot: see
    ///     <see cref="SuspendCoverRuleIfItRefusesTheWholeZone"/>. Goes to the process log like the
    ///     engine's own, because a shard that loses its world population has to say so in the log an
    ///     operator already has.
    /// </summary>
    private readonly ILogger _logger = Log.Logger.ForContext<PhysicsWorldPopulationTerrain>();

    /// <summary>How many candidate spots the overhead-cover rule has refused since this terrain was created.</summary>
    private int _coverRefusals;

    /// <summary>How many candidate spots the overhead-cover rule has judged, refusals included.</summary>
    private int _coverChecks;

    /// <summary>Set once the rule has been shown to misread the zone: cover then refuses nothing.</summary>
    private bool _coverRuleSuspended;

    /// <summary>Chunk id by index in the zone's own chunk grid, built on first use.</summary>
    private Dictionary<(int X, int Y), uint> _chunkByIndex;

    /// <summary>Guards the one-time build of <see cref="_chunkByIndex" />; the planner may be threaded.</summary>
    private readonly object _chunkIndexLock = new();

    /// <summary>
    ///     The corner the chunk index is measured from. A zone's chunk origins are not necessarily
    ///     multiples of the chunk size (Coral Forest's are offset by half a chunk in Y, because the
    ///     zone's centre index is 3.5), but they are all the same fractional offset from each other,
    ///     so measuring from the smallest one lands on exact multiples.
    /// </summary>
    private Vector3 _chunkGridOrigin;

    public PhysicsWorldPopulationTerrain(PhysicsEngine physics, IWorldPopulationRules rules)
    {
        _physics = physics;
        _rules = rules;
    }

    public bool HasSurfaces => _physics != null && _physics.HasNavigationMesh;

    public int SurfaceCount => _physics?.WalkableFaceCount ?? 0;

    /// <summary>
    ///     How many spots the overhead-cover rule has refused since this terrain was created. Printed
    ///     in <c>\population status</c>; see <see cref="IWorldPopulationTerrain.CoverRefusals"/>.
    /// </summary>
    public int CoverRefusals => _coverRefusals;

    /// <summary>
    ///     Whether the overhead-cover rule has switched itself off for this zone because it was
    ///     refusing almost every spot it judged. See
    ///     <see cref="IWorldPopulationTerrain.CoverRuleSuspended"/>.
    /// </summary>
    public bool CoverRuleSuspended => _coverRuleSuspended;

    public Vector3? ZoneBoundsMin => _physics?.ZoneBoundsMin;
    public Vector3? ZoneBoundsMax => _physics?.ZoneBoundsMax;

    public bool IsInsideZoneBounds(Vector3 position) => _physics == null || _physics.IsInsideZoneBounds(position);

    public bool TryGetSurface(int index, out Vector3 position)
    {
        position = default;
        return _physics != null && _physics.TryGetWalkableFaceCentroid(index, out position);
    }

    public uint GetChunkRecordId(Vector3 position)
    {
        if (_physics == null)
        {
            return 0;
        }

        var chunks = _physics.ZoneChunks;
        if (chunks.Count == 0)
        {
            return 0;
        }

        return ChunkByIndex(chunks).TryGetValue(ChunkIndex(position), out var chunkRecordId) ? chunkRecordId : 0u;
    }

    public bool TryResolveStandingSpot(Vector3 candidate, float bodyRadius, float bodyHeight, out Vector3 position)
    {
        position = candidate;

        if (_physics == null)
        {
            // No engine to ask. Nothing can be validated, and nothing can be snapped either.
            return false;
        }

        // A non-finite position or body size would be handed straight to the ray casts and the broad
        // phase, and from there into the body of whatever got spawned. Refuse it here instead.
        if (!IsFinite(candidate) || !float.IsFinite(bodyRadius) || !float.IsFinite(bodyHeight))
        {
            return false;
        }

        if (!_physics.HasZoneCollision)
        {
            // The zone has no static geometry (no maps configured), so the plan came from authored
            // anchor positions instead and there is nothing here to check them against. Accepting
            // keeps such a shard working exactly as its authored spawns already do.
            return true;
        }

        if (!_physics.TryGetGroundSurface(candidate, out var ground, out var normal, GroundProbeUp, GroundProbeDown))
        {
            return false;
        }

        // Steeper than the cutoff the navigation mesh itself was baked with: a spot the mesh would
        // never have called walkable, reached through jitter or a plan built without the mesh.
        // Absolute because the probe reports the surface normal it hit, and a floor and a ceiling
        // are equally unwalkable when they are this steep.
        if (MathF.Abs(normal.Z) < _rules.MinimumWalkableNormalZ)
        {
            return false;
        }

        if (!_physics.IsStandingVolumeClear(ground, bodyRadius, bodyHeight))
        {
            return false;
        }

        // Open sky only. The navigation mesh's island filter drops the small buried patches (a
        // tree canopy, the cavity under a rock), so the mesh legitimately holds the big covered
        // floors - caves, tunnels, the space under an overhang, the ground under the terrain
        // itself - and a spot on one is what reads as a mob spawning underground and shooting
        // whoever walks above it. A spot the world hangs over is not a spawn spot at all,
        // whatever the mesh says; the check is phrased so it can never hit the spot's own
        // ground (see HasOverheadCover). The rule is also allowed to be wrong about a zone only
        // up to a point - see SuspendCoverRuleIfItRefusesTheWholeZone.
        if (!_coverRuleSuspended)
        {
            _coverChecks++;

            if (_physics.HasOverheadCover(ground, bodyHeight))
            {
                _coverRefusals++;
                SuspendCoverRuleIfItRefusesTheWholeZone();
                return false;
            }
        }

        position = ground;
        return true;
    }

    /// <summary>
    ///     Turns the overhead-cover rule off for the rest of this shard's life in the zone once it
    ///     has refused almost every spot it has been asked about. At that point it is not finding
    ///     caves: it is contradicting the walkable ground the zone's own collision baked, which is
    ///     exactly what the two earlier takes on this check did before #115 reverted them (a check
    ///     that reads a whole zone as covered stops the population from generating at all), and an
    ///     empty world is the worse of the two failures on offer. Everything else still applies - the
    ///     plan's ground, the slope cut, the standing-volume probes - so the spots placed from here
    ///     on are the ones the pre-check system used.
    /// </summary>
    /// <remarks>
    ///     The counters behind the decision are the ones <c>\population status</c> prints, so an
    ///     operator seeing either the warning below or a rising cover count has the same evidence the
    ///     rule itself acted on.
    /// </remarks>
    private void SuspendCoverRuleIfItRefusesTheWholeZone()
    {
        if (_coverChecks < CoverChecksBeforeSuspicion ||
            _coverRefusals * CoverRefusalDenominator < _coverChecks * CoverRefusalNumerator)
        {
            return;
        }

        _coverRuleSuspended = true;

        _logger.Warning(
            "World population: the overhead-cover rule refused {Refusals} of the {Checks} standing spots it judged in this zone, so it is suspended for the zone and spots are placed on the plan's ground again - a rule that refuses almost every spot is reading the zone's own walkable ground as covered, which is how the earlier takes on this check emptied whole worlds; a few NPCs in the wrong place beat none",
            _coverRefusals,
            _coverChecks);
    }

    private static bool IsFinite(Vector3 value) =>
        float.IsFinite(value.X) && float.IsFinite(value.Y) && float.IsFinite(value.Z);

    private (int X, int Y) ChunkIndex(Vector3 position) => (
        (int)MathF.Floor((position.X - _chunkGridOrigin.X) / ChunkOriginCalculator.ChunkSize),
        (int)MathF.Floor((position.Y - _chunkGridOrigin.Y) / ChunkOriginCalculator.ChunkSize));

    /// <summary>
    ///     The zone's chunk grid, keyed by tile: built once, on first use, from the chunk references
    ///     the zone file was loaded from.
    /// </summary>
    /// <remarks>
    ///     The build is under a lock because the planner may be running on the shard's worker threads
    ///     when it asks (see <see cref="WorldPopulationPlanner" />): two threads filling this in at
    ///     once would be two threads writing one dictionary and - worse - two candidates for
    ///     <see cref="_chunkGridOrigin" />, so a cell's chunk could depend on which of them won. Once
    ///     built it is only read.
    /// </remarks>
    /// <param name="chunks">The loaded zone's chunk references.</param>
    /// <returns>The chunk id of each tile of the zone's grid.</returns>
    private Dictionary<(int X, int Y), uint> ChunkByIndex(IReadOnlyList<ZoneChunkRef> chunks)
    {
        if (_chunkByIndex != null)
        {
            return _chunkByIndex;
        }

        lock (_chunkIndexLock)
        {
            if (_chunkByIndex != null)
            {
                return _chunkByIndex;
            }

            var minX = float.MaxValue;
            var minY = float.MaxValue;
            foreach (var chunk in chunks)
            {
                minX = MathF.Min(minX, chunk.Origin.X);
                minY = MathF.Min(minY, chunk.Origin.Y);
            }

            _chunkGridOrigin = new Vector3(minX, minY, 0f);

            var map = new Dictionary<(int X, int Y), uint>(chunks.Count);
            foreach (var chunk in chunks)
            {
                var index = ChunkIndex(chunk.Origin);
                if (map.TryGetValue(index, out var existing) && existing != 0)
                {
                    // Two zone refs landed on the same tile. Keep the copy that knows its ChunkRecord
                    // id; the overlapping one is the undeduped 0x10100/0x10101 pair.
                    continue;
                }

                map[index] = chunk.ChunkRecordId;
            }

            _chunkByIndex = map;
            return map;
        }
    }
}
