using System;
using System.Collections.Generic;
using System.Globalization;
using System.Numerics;
using System.Text;
using System.Threading;
using GameServer.Entities;
using GameServer.Entities.Character;
using GameServer.StaticDB;
using GameServer.Systems.Ai;
using Serilog;

namespace GameServer.Systems.Spawning.Population;

/// <summary>
///     Streams the world population plan around the players: builds the plan once the zone has
///     players in it, activates the cells near them, spawns what fits within a hard budget, refills
///     what died, and takes it all away again when nobody is near or the feature is turned off.
/// </summary>
/// <remarks>
///     <para>
///         <b>Only where players are.</b> A shard with no player in the zone has no population: the
///         plan is not even built until somebody is there, and everything alive is removed when the
///         last player leaves. Cells are activated inside
///         <see cref="IWorldPopulationRules.ActivationRadius"/> and deactivated outside
///         <see cref="IWorldPopulationRules.DeactivationRadius"/>, so the world exists around the
///         players rather than all at once.
///     </para>
///     <para>
///         <b>Bounded cost.</b> Four independent brakes: the live NPC cap, the spawn budget per time
///         window, the update interval (the shard ticks every 5 ms, this runs a few times a second),
///         and the plan's own work budget while it is being built. A player sprinting into an empty
///         corner of the zone therefore fills it over a couple of seconds rather than in one tick.
///     </para>
///     <para>
///         <b>Collisions.</b> Two kinds, both checked before anything is spawned: the physical one
///         (<see cref="IWorldPopulationTerrain.TryResolveStandingSpot"/> snaps the spot onto the
///         ground, refuses a slope no NPC could stand on, and refuses a volume the world or another
///         body already fills) and the server side one (<see cref="SpawnOccupancyGrid"/> refuses a
///         spot another planned or spawned body already holds, which no ray can see because that body
///         may not exist yet). A refusal is retried at jittered positions, then later, then - if the
///         ground itself is the problem - reported and given up on rather than spun on.
///     </para>
/// </remarks>
public sealed class WorldPopulationService
{
    /// <summary>Salt keeping the retry jitter's hashes independent of the plan's.</summary>
    private const int RetryAngleSalt = 0x3D71;

    /// <summary>Salt for the retry jitter's distance draw.</summary>
    private const int RetryDistanceSalt = 0x5F35;

    /// <summary>
    ///     How far a retry may move a slot from its planned position, as a fraction of the cell size.
    ///     Half a cell can put the NPC in the neighbouring cell, which is intended: better a mob
    ///     standing 20 m from where it was planned than a cell that never fills.
    /// </summary>
    private const float RetryJitterFraction = 0.5f;

    /// <summary>
    ///     Consecutive failed updates after which the service turns itself off. One failure is a
    ///     zone's data disagreeing with an assumption; three in a row is a feature that would
    ///     otherwise throw every update for the life of the process.
    /// </summary>
    private const int MaxConsecutiveFailures = 3;

    /// <summary>
    ///     Updates between two "the plan is still building" lines. The reason is worth repeating
    ///     because it is the only one that resolves on its own, and a zone that stays empty for a
    ///     minute should say so rather than fall silent.
    /// </summary>
    private const int PlanningAnnouncementTicks = 40;

    private readonly IShard _shard;
    private readonly ILogger _logger;
    private readonly IWorldPopulationRules _rules;
    private readonly IWorldPopulationTerrain _terrain;
    private readonly IWorldPopulationSpawner _spawner;
    private readonly SpawnOccupancyGrid _occupancy;
    private readonly WorldPopulationPlanner _planner;

    private readonly Dictionary<long, WorldPopulationCell> _activeCells = [];
    private readonly HashSet<long> _wantedActivate = [];
    private readonly HashSet<long> _wantedKeep = [];
    private readonly Queue<WorldPopulationSlot> _pending = new();
    private readonly List<WorldPopulationSlot> _liveSlots = [];
    private readonly List<Vector3> _players = [];
    private readonly List<WorldPopulationCell> _deactivating = [];

    private ulong _lastUpdate;
    private ulong _lastSpawnWindow;
    private int _spawnedThisWindow;
    private int _consecutiveFailures;
    private bool _occupancySeeded;
    private IdleReason _announcedIdle = IdleReason.None;

    /// <summary>
    ///     Starts at zero so a plan that builds in a second or two never reports itself: "still
    ///     building" is only worth saying once the wait is long enough to notice.
    /// </summary>
    private int _ticksSincePlanningAnnouncement;

    /// <summary>Why the most recent update spawned nothing, so it is announced once per change.</summary>
    private enum IdleReason
    {
        None,
        Disabled,
        NoPlayers,
        Planning,
    }

    public WorldPopulationService(
        IShard shard,
        IWorldPopulationRules rules,
        IWorldPopulationDataSource data,
        IWorldPopulationTerrain terrain,
        IWorldPopulationSpawner spawner)
    {
        _shard = shard;
        _logger = shard.Logger.ForContext<WorldPopulationService>();
        _rules = rules;
        _terrain = terrain;
        _spawner = spawner;

        // The placement grid hashes at a finer resolution than the plan's cells: its queries are a
        // couple of body radii wide, not 32 m.
        _occupancy = new SpawnOccupancyGrid(Math.Max(4f, rules.CellSize / 4f));
        _planner = new WorldPopulationPlanner(
            shard.ZoneId,
            rules,
            data,
            terrain,
            shard.Logger.ForContext<WorldPopulationPlanner>());

        Enabled = rules.Enabled;
    }

    /// <summary>
    ///     Whether the service populates the world. Turning it off removes everything it spawned
    ///     (and nothing else) at the next update; turning it back on streams the same plan in again.
    ///     Initialised from <see cref="IWorldPopulationRules.Enabled"/>, i.e. from the server's
    ///     <c>SpawnWorldPopulation</c> setting.
    /// </summary>
    public bool Enabled { get; set; }

    /// <summary>How many population NPCs are in the world right now.</summary>
    public int LiveCount => _liveSlots.Count;

    /// <summary>How many cells are activated right now.</summary>
    public int ActiveCellCount => _activeCells.Count;

    /// <summary>How many slots are waiting to be filled.</summary>
    public int PendingSlotCount => _pending.Count;

    /// <summary>How many slots gave up on their ground.</summary>
    public int ParkedSlotCount { get; private set; }

    /// <summary>Total NPCs spawned since the shard started.</summary>
    public int SpawnedTotal { get; private set; }

    /// <summary>Total NPCs removed because their cell was deactivated or the feature was turned off.</summary>
    public int DespawnedTotal { get; private set; }

    /// <summary>Total population NPCs that left the world by other means (killed, despawned by a mission).</summary>
    public int LostTotal { get; private set; }

    /// <summary>Total placement refusals, for telling a zone whose plan does not fit its ground.</summary>
    public int RefusedPlacements { get; private set; }

    /// <summary>How many bodies the placement grid is holding.</summary>
    public int OccupancyCount => _occupancy.Count;

    /// <summary>How many players the last update measured the world against.</summary>
    public int PlayerCount => _players.Count;

    /// <summary>The plan, for its statistics. Never null; incomplete until its work is done.</summary>
    public WorldPopulationPlanner Plan => _planner;

    /// <summary>
    ///     One world population update. Called from the shard's tick, after the entity manager's, so
    ///     the zone's own entities exist before the plan is built and the occupancy grid is seeded
    ///     from them. Does its work at most every
    ///     <see cref="IWorldPopulationRules.TickIntervalMs"/> and returns immediately otherwise.
    /// </summary>
    /// <remarks>
    ///     Never throws into the shard's tick. This system reads every monster row in the database
    ///     and every walkable surface in the zone, so the honest failure mode is "something in this
    ///     zone's data does not suit it" - and that must cost the feature, not the server. An update
    ///     that throws is logged, counted, and after <see cref="MaxConsecutiveFailures"/> in a row the
    ///     service turns itself off (taking its NPCs with it) instead of throwing four times a second
    ///     for the rest of the process.
    /// </remarks>
    public void Tick(double deltaTime, ulong currentTime, CancellationToken ct)
    {
        if (ct.IsCancellationRequested)
        {
            return;
        }

        if (currentTime < _lastUpdate + (ulong)Math.Max(1, _rules.TickIntervalMs))
        {
            return;
        }

        _lastUpdate = currentTime;

        try
        {
            Update(currentTime);
            _consecutiveFailures = 0;
        }
        catch (Exception ex)
        {
            _consecutiveFailures++;
            _logger.Error(
                ex,
                "World population update failed ({Count} in a row); the feature turns itself off after {Max}",
                _consecutiveFailures,
                MaxConsecutiveFailures);

            if (_consecutiveFailures >= MaxConsecutiveFailures)
            {
                Enabled = false;

                try
                {
                    Clear("its update kept failing");
                }
                catch (Exception clearEx)
                {
                    _logger.Error(clearEx, "World population could not clear its NPCs while turning itself off");
                }
            }
        }
    }

    private void Update(ulong currentTime)
    {
        if (!Enabled || !_rules.Enabled)
        {
            if (LiveCount > 0 || _activeCells.Count > 0)
            {
                Clear("turned off");
            }

            AnnounceDisabled();
            return;
        }

        CollectPlayers();

        if (_players.Count == 0)
        {
            // Nothing to populate for. The plan stays (it is only memory), the world does not.
            if (LiveCount > 0 || _activeCells.Count > 0)
            {
                Clear("no players in the zone");
            }

            AnnounceNoPlayers();
            return;
        }

        if (!_planner.IsComplete)
        {
            _ = _planner.Work(Math.Max(1, _rules.PlanWorkPerTick));
            if (!_planner.IsComplete)
            {
                AnnouncePlanning();
                return;
            }
        }

        _announcedIdle = IdleReason.None;
        _ticksSincePlanningAnnouncement = 0;

        // Every update rather than only when the plan finished: a Clear (nobody in the zone, or the
        // feature turned off) forgets the grid, and the world it is seeded from has moved since.
        SeedOccupancy();

        UpdateActivation(currentTime);
        ProcessSpawns(currentTime);
        Reconcile(currentTime);
    }

    /// <summary>Says the feature is off, and which of the two switches is off.</summary>
    private void AnnounceDisabled()
    {
        if (_announcedIdle == IdleReason.Disabled)
        {
            return;
        }

        _announcedIdle = IdleReason.Disabled;

        _logger.Information(
            "World population: spawning nothing in zone {ZoneId} - {Reason}",
            _shard.ZoneId,
            _rules.Enabled
                ? "the service turned itself off after its update kept failing (see the error above)"
                : "SpawnWorldPopulation is false in the server settings");
    }

    /// <summary>Says an update found nobody to populate around, once until that changes.</summary>
    private void AnnounceNoPlayers()
    {
        if (_announcedIdle == IdleReason.NoPlayers)
        {
            return;
        }

        _announcedIdle = IdleReason.NoPlayers;

        _logger.Information(
            "World population: spawning nothing in zone {ZoneId} - no player counts as present yet, and a client only does " +
            "once it can receive entity state and has a character in the world ({Clients} clients on the shard)",
            _shard.ZoneId,
            _shard.Clients.Count);
    }

    /// <summary>
    ///     Says the plan is still being built, with its progress, every
    ///     <see cref="PlanningAnnouncementTicks"/> updates rather than once.
    /// </summary>
    /// <remarks>
    ///     Every path out of <see cref="Update"/> that spawns nothing used to return in silence, so a
    ///     zone that stayed empty was indistinguishable in the log from one where the feature was
    ///     never switched on - and "the zone is empty" had nothing to point at. This is the one
    ///     reason that resolves by itself, so it repeats: a plan that is not advancing says so.
    /// </remarks>
    private void AnnouncePlanning()
    {
        _announcedIdle = IdleReason.Planning;

        if (++_ticksSincePlanningAnnouncement < PlanningAnnouncementTicks)
        {
            return;
        }

        _ticksSincePlanningAnnouncement = 0;

        _logger.Information(
            "World population: still building the plan for zone {ZoneId} ({Surfaces} walkable surfaces scanned, {Cells} cells so far, " +
            "{Budget} per update); nothing spawns until it finishes",
            _shard.ZoneId,
            _planner.ScannedSurfaces,
            _planner.CellCount,
            _rules.PlanWorkPerTick);
    }

    /// <summary>How many distinct monster rows are in the world right now.</summary>
    public int CountDistinctLiveTypes()
    {
        if (_liveSlots.Count == 0)
        {
            return 0;
        }

        HashSet<uint> types = [];
        foreach (var slot in _liveSlots)
        {
            _ = types.Add(slot.Candidate.MonsterId);
        }

        return types.Count;
    }

    /// <summary>
    ///     The population NPCs alive within <paramref name="radius"/> metres of
    ///     <paramref name="position"/>, nearest first, with the monster row and the position each one
    ///     actually ended up at (which the AI may have moved since it was placed). Capped at
    ///     <paramref name="limit"/> entries. This is what the <c>\population near</c> command shows:
    ///     the quickest way to see whether what the plan put somewhere is what belongs there.
    /// </summary>
    public IReadOnlyList<(uint MonsterId, Vector3 Position, float Distance)> ListLiveNear(
        Vector3 position,
        float radius,
        int limit = 40)
    {
        if (_liveSlots.Count == 0 || radius <= 0f || limit <= 0)
        {
            return Array.Empty<(uint, Vector3, float)>();
        }

        var found = new List<(uint MonsterId, Vector3 Position, float Distance)>();

        foreach (var slot in _liveSlots)
        {
            var at = _shard.Entities.TryGetValue(slot.EntityId, out var entity) ? entity.Position : slot.Anchor;
            float distance = AiVectors.Distance(position, at);

            if (distance <= radius)
            {
                found.Add((slot.Candidate.MonsterId, at, distance));
            }
        }

        found.Sort((a, b) => a.Distance.CompareTo(b.Distance));

        return found.Count > limit ? found.GetRange(0, limit) : found;
    }

    /// <summary>
    ///     A short report of what the service is doing, for the <c>\population</c> and
    ///     <c>population</c> commands.
    /// </summary>
    public string DescribeStatus()
    {
        var text = new StringBuilder();
        _ = text.AppendLine(Enabled
            ? $"World population: on (live {LiveCount}/{_rules.MaxLiveNpcs} NPCs of {_planner.RosterCount} monster rows, {CountDistinctLiveTypes()} kinds in the world)"
            : "World population: off");
        _ = text.AppendLine(_planner.IsComplete
            ? $"Plan: {_planner.CellCount} cells, {_planner.SlotCount} slots, {_planner.PlacedRosterCount} rows placed" +
              (_planner.UnplacedRosterCount > 0 ? $", {_planner.UnplacedRosterCount} rows have no ground of their kind in this zone" : string.Empty) +
              (_planner.RefusedChunkCells > 0 ? $", {_planner.RefusedChunkCells} cells refused by chunk rules" : string.Empty) +
              (_planner.UsedAnchorFallback ? ", built from authored anchors (no walkable surfaces)" : string.Empty)
            : $"Plan: building ({_planner.ScannedSurfaces} surfaces scanned, {_planner.CellCount} cells so far)");
        _ = text.AppendLine(string.Format(
            CultureInfo.InvariantCulture,
            "Streaming: {0} active cells, {1} slots queued, {2} players, activate {3:0} m / deactivate {4:0} m",
            ActiveCellCount,
            PendingSlotCount,
            PlayerCount,
            _rules.ActivationRadius,
            _rules.DeactivationRadius));
        _ = text.AppendLine(string.Format(
            CultureInfo.InvariantCulture,
            "Lifetime: {0} spawned, {1} despawned, {2} lost, {3} placements refused, {4} slots parked, {5} bodies in the placement grid",
            SpawnedTotal,
            DespawnedTotal,
            LostTotal,
            RefusedPlacements,
            ParkedSlotCount,
            OccupancyCount));

        return text.ToString().TrimEnd();
    }

    private void CollectPlayers()
    {
        _players.Clear();

        foreach (var client in _shard.Clients.Values)
        {
            // A client that cannot receive entity state yet has no character in the world to
            // populate around; the same rule the entity manager's scope check uses.
            if (!client.CanReceiveGSS)
            {
                continue;
            }

            var character = client.CharacterEntity;
            if (character == null)
            {
                continue;
            }

            _players.Add(character.Position);
        }
    }

    private void SeedOccupancy()
    {
        if (_occupancySeeded)
        {
            return;
        }

        _occupancySeeded = true;

        // Everything already in the world - the zone's authored NPCs, its deployables and outposts,
        // and every player - is registered so a planned position is never on top of it. Bodies that
        // appear later are caught by the physical check instead, which sees every body the
        // simulation knows.
        foreach (var entity in _shard.Entities.Values)
        {
            _occupancy.Add(entity.EntityId, entity.Position, OccupantRadius(entity));
        }

        _logger.Information(
            "World population: plan ready, {Occupants} existing entities registered with the placement grid",
            _occupancy.Count);
    }

    private float OccupantRadius(IEntity entity)
    {
        // A character carries its own body radius in its monster row; anything else gets the radius
        // the AI navigates with.
        if (entity is CharacterEntity character)
        {
            uint typeId = character.StaticInfo.CharacterTypeId;
            if (typeId != 0)
            {
                var monster = SDBInterface.GetMonster(typeId);
                if (monster != null && monster.BodyRadius > 0f)
                {
                    return monster.BodyRadius;
                }
            }
        }

        return _rules.DefaultBodyRadius;
    }

    private void UpdateActivation(ulong currentTime)
    {
        _wantedActivate.Clear();
        _wantedKeep.Clear();

        foreach (var player in _players)
        {
            CollectCellKeys(player, _rules.ActivationRadius, _wantedActivate);
            CollectCellKeys(player, _rules.DeactivationRadius, _wantedKeep);
        }

        foreach (long key in _wantedActivate)
        {
            if (_activeCells.ContainsKey(key) ||
                !_planner.Cells.TryGetValue(key, out var cell) ||
                cell.Slots.Count == 0)
            {
                continue;
            }

            ActivateCell(cell, currentTime);
        }

        _deactivating.Clear();
        foreach (var pair in _activeCells)
        {
            if (!_wantedKeep.Contains(pair.Key))
            {
                _deactivating.Add(pair.Value);
            }
        }

        foreach (var cell in _deactivating)
        {
            DeactivateCell(cell);
        }
    }

    private void CollectCellKeys(Vector3 position, float radius, HashSet<long> keys)
    {
        float cellSize = _rules.CellSize > 0f ? _rules.CellSize : 32f;

        var (minX, minY) = WorldPopulationCell.CellIndexOf(
            new Vector3(position.X - radius, position.Y - radius, position.Z),
            cellSize);
        var (maxX, maxY) = WorldPopulationCell.CellIndexOf(
            new Vector3(position.X + radius, position.Y + radius, position.Z),
            cellSize);

        for (int x = minX; x <= maxX; x++)
        {
            for (int y = minY; y <= maxY; y++)
            {
                _ = keys.Add(WorldPopulationCell.MakeKey(x, y));
            }
        }
    }

    private void ActivateCell(WorldPopulationCell cell, ulong currentTime)
    {
        cell.IsActive = true;
        _activeCells[cell.Key] = cell;

        foreach (var slot in cell.Slots)
        {
            if (slot.EntityId != 0 || slot.Parked)
            {
                continue;
            }

            // The row's own ai_spawn_delay_ms: how long the game let a spawn take before the NPC was
            // active. Honoured here, and it also staggers a cell's NPCs over a couple of seconds
            // instead of letting them all land in one update. A slot that is still waiting for a
            // respawn keeps the later of the two times.
            slot.NotBefore = Math.Max(
                slot.NotBefore,
                currentTime + (ulong)Math.Max(0, slot.Candidate.SpawnDelayMs));
            _pending.Enqueue(slot);
        }
    }

    private void DeactivateCell(WorldPopulationCell cell)
    {
        cell.IsActive = false;
        _ = _activeCells.Remove(cell.Key);

        foreach (var slot in cell.Slots)
        {
            slot.NotBefore = 0;

            if (slot.EntityId == 0)
            {
                continue;
            }

            _spawner.Despawn(slot.EntityId);
            _ = _occupancy.Remove(slot.EntityId);
            _ = _liveSlots.Remove(slot);
            slot.EntityId = 0;
            DespawnedTotal++;
        }
    }

    private void ProcessSpawns(ulong currentTime)
    {
        if (_pending.Count == 0)
        {
            return;
        }

        if (currentTime >= _lastSpawnWindow + (ulong)Math.Max(1, _rules.SpawnBudgetWindowMs))
        {
            _lastSpawnWindow = currentTime;
            _spawnedThisWindow = 0;
        }

        int budget = Math.Min(_rules.SpawnBudget - _spawnedThisWindow, _rules.MaxLiveNpcs - LiveCount);
        if (budget <= 0)
        {
            return;
        }

        // A slot that is not due yet goes back on the queue, so the loop has to stop after one pass
        // through what was queued rather than chase its own tail.
        int guard = _pending.Count;

        while (budget > 0 && guard-- > 0 && _pending.TryDequeue(out var slot))
        {
            if (slot.EntityId != 0 || slot.Parked)
            {
                continue;
            }

            if (!slot.Cell.IsActive)
            {
                slot.NotBefore = 0;
                continue;
            }

            if (slot.NotBefore > currentTime)
            {
                _pending.Enqueue(slot);
                continue;
            }

            switch (TryPlace(slot))
            {
                case PlacementOutcome.Placed:
                    budget--;
                    break;
                case PlacementOutcome.RefusedByGround:
                    RetryLater(slot, currentTime, countsAsFailure: true);
                    break;
                default:
                    RetryLater(slot, currentTime, countsAsFailure: false);
                    break;
            }
        }
    }

    private PlacementOutcome TryPlace(WorldPopulationSlot slot)
    {
        var candidate = slot.Candidate;
        float radius = candidate.ResolvedBodyRadius(_rules);
        float height = candidate.ResolvedBodyHeight(_rules);
        int attempts = Math.Max(1, _rules.MaxPlacementAttempts);

        bool refusedByGround = false;
        bool refusedByOccupancy = false;

        for (int attempt = 0; attempt < attempts; attempt++)
        {
            var position = attempt == 0 ? slot.Anchor : slot.Anchor + RetryJitter(slot, attempt);

            if (IsTooCloseToAPlayer(position) ||
                !_occupancy.IsAreaFree(position, radius, _rules.MinSeparation))
            {
                refusedByOccupancy = true;
                continue;
            }

            if (!_terrain.TryResolveStandingSpot(position, radius, height, out var resolved))
            {
                refusedByGround = true;
                continue;
            }

            // The snap keeps X and Y, but re-check anyway: the resolved spot is the one that is
            // actually going to be occupied.
            if (IsTooCloseToAPlayer(resolved) ||
                !_occupancy.IsAreaFree(resolved, radius, _rules.MinSeparation))
            {
                refusedByOccupancy = true;
                continue;
            }

            ulong entityId = _spawner.Spawn(
                candidate.MonsterId,
                resolved,
                AiVectors.OrientationFacing(slot.Facing),
                slot.Cell.Level);

            if (entityId == 0)
            {
                // The spawner refused the row itself (no such monster, no entity manager). No
                // position is going to change that.
                return PlacementOutcome.RefusedByGround;
            }

            slot.EntityId = entityId;
            slot.NotBefore = 0;
            slot.Failures = 0;
            _occupancy.Add(entityId, resolved, radius);
            _liveSlots.Add(slot);
            SpawnedTotal++;
            _spawnedThisWindow++;

            return PlacementOutcome.Placed;
        }

        RefusedPlacements++;

        // A slot only ever refused by the ground is a slot whose ground does not fit its body; a
        // slot that was also refused for room is worth another look later, when whatever was
        // standing there has moved.
        return refusedByGround && !refusedByOccupancy
            ? PlacementOutcome.RefusedByGround
            : PlacementOutcome.RefusedForRoom;
    }

    private void RetryLater(WorldPopulationSlot slot, ulong currentTime, bool countsAsFailure)
    {
        slot.NotBefore = currentTime + (ulong)Math.Max(0, _rules.PlacementRetryDelayMs);

        if (countsAsFailure)
        {
            slot.Failures++;

            if (slot.Failures >= Math.Max(1, _rules.MaxPlacementFailures))
            {
                // The ground it wants does not exist. Stop asking, and say so in the status.
                slot.Parked = true;
                ParkedSlotCount++;
                return;
            }
        }

        _pending.Enqueue(slot);
    }

    private Vector3 RetryJitter(WorldPopulationSlot slot, int attempt)
    {
        float angle = WorldPopulationHash.Unit(
                          WorldPopulationHash.Mix(slot.Cell.Key, (slot.Index * 17) + (attempt * 101) + RetryAngleSalt))
                      * (2f * MathF.PI);
        float distance = MathF.Sqrt(WorldPopulationHash.Unit(
                           WorldPopulationHash.Mix(slot.Cell.Key, attempt + RetryDistanceSalt)))
                         * (_rules.CellSize * RetryJitterFraction);

        return new Vector3(MathF.Cos(angle) * distance, MathF.Sin(angle) * distance, 0f);
    }

    private bool IsTooCloseToAPlayer(Vector3 position)
    {
        foreach (var player in _players)
        {
            if (AiVectors.Distance(player, position) < _rules.MinPlayerDistance)
            {
                return true;
            }
        }

        return false;
    }

    private void Reconcile(ulong currentTime)
    {
        for (int i = _liveSlots.Count - 1; i >= 0; i--)
        {
            var slot = _liveSlots[i];

            if (_spawner.IsAlive(slot.EntityId))
            {
                continue;
            }

            // Killed, or removed by something else (a mission, a despawn command, a lifetime). The
            // slot is theirs again, and refills after the respawn delay plus the row's own spawn
            // delay - a corpse is not instantly replaced by its successor.
            _ = _occupancy.Remove(slot.EntityId);
            _liveSlots.RemoveAt(i);
            slot.EntityId = 0;
            slot.NotBefore = currentTime + (ulong)Math.Max(0, _rules.RespawnDelayMs + slot.Candidate.SpawnDelayMs);
            LostTotal++;

            if (slot.Cell.IsActive && !slot.Parked)
            {
                _pending.Enqueue(slot);
            }
        }
    }

    /// <summary>
    ///     Removes everything this service spawned and forgets every activation, keeping the plan
    ///     (which is only memory) so turning the feature back on does not have to build it again.
    ///     Only ever touches entities it spawned itself.
    /// </summary>
    private void Clear(string reason)
    {
        foreach (var slot in _liveSlots)
        {
            _spawner.Despawn(slot.EntityId);
            _ = _occupancy.Remove(slot.EntityId);
            slot.EntityId = 0;
            slot.NotBefore = 0;
            slot.Failures = 0;
            DespawnedTotal++;
        }

        _liveSlots.Clear();

        foreach (var cell in _activeCells.Values)
        {
            cell.IsActive = false;
        }

        _activeCells.Clear();
        _pending.Clear();
        _occupancy.Clear();

        // The world moved while the population was away; seed the grid again when it comes back.
        _occupancySeeded = false;

        _logger.Information("World population cleared: {Reason}", reason);
    }

    private enum PlacementOutcome
    {
        Placed,

        /// <summary>The ground refused the body: no surface, too steep, inside the world, or the row cannot be spawned at all.</summary>
        RefusedByGround,

        /// <summary>Something transient was in the way: a player, or another body.</summary>
        RefusedForRoom,
    }
}
