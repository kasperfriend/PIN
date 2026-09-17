using System;
using System.Collections.Generic;
using System.Numerics;
using GameServer.Systems.Spawning.Population;

namespace GameServer.Tests.Fakes;

/// <summary>
///     Hands out a fixed roster, a fixed set of anchors and fixed chunk/level answers, so the plan
///     can be asserted on without a loaded <c>clientdb.sd2</c>.
/// </summary>
public sealed class FakeWorldPopulationDataSource : IWorldPopulationDataSource
{
    /// <summary>The monster rows the plan may use. Add them in the order the real source would.</summary>
    public List<WorldPopulationCandidate> Candidates { get; } = [];

    /// <summary>The places that give the ground around them a habitat and a level.</summary>
    public List<WorldPopulationAnchor> Anchors { get; } = [];

    /// <summary>Chunk ids the plan must refuse (the zone's client-only chunks).</summary>
    public HashSet<uint> UnspawnableChunks { get; } = [];

    /// <summary>Level a level band resolves to.</summary>
    public Dictionary<uint, byte> LevelsByBand { get; } = [];

    /// <summary>Level a place with no band gets.</summary>
    public byte DefaultLevel { get; set; } = 7;

    /// <summary>How often the roster was read.</summary>
    public int CandidateCalls { get; private set; }

    /// <summary>How often the anchors were read.</summary>
    public int AnchorCalls { get; private set; }

    /// <summary>Adds a monster row with the fields a test usually cares about.</summary>
    public WorldPopulationCandidate AddMonster(
        uint monsterId,
        WorldPopulationHabitat habitat = WorldPopulationHabitat.Wilderness,
        float bodyRadius = 0f,
        float bodyHeight = 0f,
        uint difficultyCost = 0,
        int spawnDelayMs = 0,
        int weight = 1)
    {
        var candidate = new WorldPopulationCandidate
        {
            MonsterId = monsterId,
            Habitat = habitat,
            BodyRadius = bodyRadius,
            BodyHeight = bodyHeight,
            DifficultyCost = difficultyCost,
            SpawnDelayMs = spawnDelayMs,
            Weight = weight,
        };

        Candidates.Add(candidate);
        return candidate;
    }

    public IReadOnlyList<WorldPopulationCandidate> GetCandidates()
    {
        CandidateCalls++;
        return Candidates;
    }

    public IReadOnlyList<WorldPopulationAnchor> GetAnchors(uint zoneId)
    {
        AnchorCalls++;
        return Anchors;
    }

    public byte ResolveLevel(uint zoneId, uint levelBandId) =>
        levelBandId != 0 && LevelsByBand.TryGetValue(levelBandId, out byte level) ? level : DefaultLevel;

    public bool IsChunkSpawnable(uint zoneId, uint chunkRecordId) => !UnspawnableChunks.Contains(chunkRecordId);
}

/// <summary>
///     A flat plane of walkable spots with a ground height of the test's choosing, and switches for
///     refusing a placement or reporting a chunk a spot belongs to.
/// </summary>
public sealed class FakeWorldPopulationTerrain : IWorldPopulationTerrain
{
    /// <summary>The walkable spots, in mesh order.</summary>
    public List<Vector3> Surfaces { get; } = [];

    /// <summary>Whether placements are accepted at all. Off means the ground refuses everybody.</summary>
    public bool AcceptPlacements { get; set; } = true;

    /// <summary>The height every accepted placement is snapped to.</summary>
    public float GroundHeight { get; set; }

    /// <summary>Refuses individual placements: return false for a spot a body may not stand on.</summary>
    public Func<Vector3, float, float, bool> RefusePlacement { get; set; }

    /// <summary>
    ///     Thrown from <see cref="TryResolveStandingSpot"/> when set: a zone whose data makes the
    ///     ground query blow up, which the service has to survive.
    /// </summary>
    public Exception ThrowOnPlacement { get; set; }

    /// <summary>Answers which chunk a spot belongs to; 0 when unset.</summary>
    public Func<Vector3, uint> ChunkOf { get; set; }

    /// <summary>
    ///     Answers whether a spot has a clear vertical line to the sky; null means every spot does.
    ///     A spot the answer denies is refused by <see cref="TryResolveStandingSpot" /> too, the way
    ///     the real terrain refuses covered ground.
    /// </summary>
    public Func<Vector3, bool> ExposedToSky { get; set; }

    /// <inheritdoc />
    public bool CoverRefusalsEnabled { get; set; } = true;

    /// <summary>How many placements were asked for, successful or not.</summary>
    public int PlacementCalls { get; private set; }

    public bool HasSurfaces => Surfaces.Count > 0;

    public int SurfaceCount => Surfaces.Count;

    public Vector3? ZoneBoundsMin { get; set; }
    public Vector3? ZoneBoundsMax { get; set; }

    public bool IsInsideZoneBounds(Vector3 position)
    {
        if (ZoneBoundsMin == null || ZoneBoundsMax == null) return true;
        var min = ZoneBoundsMin.Value;
        var max = ZoneBoundsMax.Value;
        return position.X >= min.X && position.X <= max.X && position.Y >= min.Y && position.Y <= max.Y && position.Z >= min.Z && position.Z <= max.Z;
    }

    /// <summary>
    ///     Lays out a lattice of walkable spots. A spacing of half the plan's cell size gives four
    ///     spots per cell, which is what a real navigation mesh looks like at that resolution.
    /// </summary>
    public void AddPlane(Vector3 origin, int columns, int rows, float spacing)
    {
        for (int column = 0; column < columns; column++)
        {
            for (int row = 0; row < rows; row++)
            {
                Surfaces.Add(new Vector3(
                    origin.X + (column * spacing),
                    origin.Y + (row * spacing),
                    origin.Z));
            }
        }
    }

    public bool TryGetSurface(int index, out Vector3 position)
    {
        if (index < 0 || index >= Surfaces.Count)
        {
            position = default;
            return false;
        }

        position = Surfaces[index];
        return true;
    }

    public uint GetChunkRecordId(Vector3 position) => ChunkOf?.Invoke(position) ?? 0;

    public bool IsExposedToSky(Vector3 position) => ExposedToSky?.Invoke(position) ?? true;

    public bool TryResolveStandingSpot(Vector3 candidate, float bodyRadius, float bodyHeight, out Vector3 position)
    {
        PlacementCalls++;
        position = candidate;

        if (ThrowOnPlacement != null)
        {
            throw ThrowOnPlacement;
        }

        if (!AcceptPlacements || (RefusePlacement != null && !RefusePlacement(candidate, bodyRadius, bodyHeight)))
        {
            return false;
        }

        // Covered from above - a cave floor, ground under a roof - is ground the plan's cells keep
        // only because their centre is in the open, and the slots that dip into the cover have to
        // be refused here, the way the real terrain refuses them - unless the plan found the sky
        // check untrustworthy, in which case the real terrain stops refusing cover and so does this.
        if (CoverRefusalsEnabled && !IsExposedToSky(candidate))
        {
            return false;
        }

        position = new Vector3(candidate.X, candidate.Y, GroundHeight);
        return true;
    }
}

/// <summary>
///     Records what the service asked to be spawned and hands back fake entity ids, so a test can
///     count spawns, read their positions and kill or despawn them individually.
/// </summary>
public sealed class FakeWorldPopulationSpawner : IWorldPopulationSpawner
{
    /// <summary>Every spawn request, in order.</summary>
    public List<(uint MonsterId, Vector3 Position, Quaternion Orientation, byte Level)> Spawned { get; } = [];

    /// <summary>Entity ids that are still in the world.</summary>
    public HashSet<ulong> Alive { get; } = [];

    /// <summary>Entity ids the service asked to have removed, in order.</summary>
    public List<ulong> Despawned { get; } = [];

    /// <summary>Refuses individual spawns: return false and the service gets a 0 back.</summary>
    public Func<uint, Vector3, bool> RefuseSpawn { get; set; }

    private ulong _nextEntityId = 0x10000;

    /// <summary>How many NPCs are in this spawner's world.</summary>
    public int LiveCount => Alive.Count;

    /// <summary>Takes an NPC out of the world the way a death would: the service has to notice.</summary>
    public void Kill(ulong entityId) => _ = Alive.Remove(entityId);

    public ulong Spawn(uint monsterId, Vector3 position, Quaternion orientation, byte level)
    {
        if (RefuseSpawn != null && !RefuseSpawn(monsterId, position))
        {
            return 0;
        }

        Spawned.Add((monsterId, position, orientation, level));
        ulong entityId = _nextEntityId += 0x100;
        _ = Alive.Add(entityId);
        return entityId;
    }

    public bool IsAlive(ulong entityId) => Alive.Contains(entityId);

    public void Despawn(ulong entityId)
    {
        if (Alive.Remove(entityId))
        {
            Despawned.Add(entityId);
        }
    }
}
