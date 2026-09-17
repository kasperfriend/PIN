using System;
using System.Collections.Generic;
using System.Numerics;
using GameServer.Physics;
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

    private readonly PhysicsEngine _physics;
    private readonly IWorldPopulationRules _rules;

    /// <summary>Chunk id by index in the zone's own chunk grid, built on first use.</summary>
    private Dictionary<(int X, int Y), uint> _chunkByIndex;

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

        // The zone's collision covers this spot from above: a cave floor, a tunnel, ground under a
        // roof or a rock overhang. The standing volume's headroom probe reaches only a body's
        // height, so a spot metres under a rock passes it; the cast to the zone's top is the one
        // that sees the cover. Refused as ground, so the slot retries at a spot that is not - and
        // parks when none is. Skipped when the plan found the zone's sky check untrustworthy (it
        // refused every cell of the zone), because then this half of the check refuses everything
        // too, and a check that refuses everything is a check that populates nothing.
        if (CoverRefusalsEnabled && !_physics.IsExposedToSky(ground))
        {
            return false;
        }

        if (!_physics.IsStandingVolumeClear(ground, bodyRadius, bodyHeight))
        {
            return false;
        }

        position = ground;
        return true;
    }

    /// <summary>
    ///     Whether the placement probe refuses covered ground. The service turns this off when the
    ///     plan found the zone's sky check untrustworthy (it refused every cell of the zone): a check
    ///     that refuses everything populates nothing, and an empty world is the worse failure.
    /// </summary>
    public bool CoverRefusalsEnabled { get; set; } = true;

    public bool IsExposedToSky(Vector3 position)
    {
        if (_physics == null)
        {
            // No engine to ask. Nothing can cover a spot.
            return true;
        }

        if (!_physics.HasZoneCollision)
        {
            // No static geometry: nothing can cover a spot.
            return true;
        }

        // The planned point sits on the ground the mesh baked, or within a step of it. Find that
        // ground with the same short window placement uses, and ask about the surface it finds: a
        // cave floor under the point is a surface the window reaches. When the window finds no
        // ground at all (a point too broken to plan on), there is nothing to judge here, and the
        // placement probe refuses such a spot one by one instead.
        if (!_physics.TryGetGroundSurface(position, out var ground, out _, GroundProbeUp, GroundProbeDown))
        {
            return true;
        }

        return _physics.IsExposedToSky(ground);
    }

    private static bool IsFinite(Vector3 value) =>
        float.IsFinite(value.X) && float.IsFinite(value.Y) && float.IsFinite(value.Z);

    private (int X, int Y) ChunkIndex(Vector3 position) => (
        (int)MathF.Floor((position.X - _chunkGridOrigin.X) / ChunkOriginCalculator.ChunkSize),
        (int)MathF.Floor((position.Y - _chunkGridOrigin.Y) / ChunkOriginCalculator.ChunkSize));

    private Dictionary<(int X, int Y), uint> ChunkByIndex(IReadOnlyList<ZoneChunkRef> chunks)
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
