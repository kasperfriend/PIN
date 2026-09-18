using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Globalization;
using System.Numerics;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using GameServer.Data;
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
///         players rather than all at once. Only players in the shard's own zone count: one shard
///         runs one zone, and a player in any other zone stands on ground this plan knows nothing
///         about.
///     </para>
///     <para>
///         <b>Bounded cost.</b> Five independent brakes: the live NPC cap, the spawn budget per time
///         window, the update interval (the shard ticks every 5 ms, this runs a few times a second),
///         the plan's own work budget while it is being built, and the placement work budget, which
///         is what bounds an update whose ground refuses the slots it is trying: refusals do not
///         spend spawn budget, so without a work budget the loop walked the entire queue, about
///         fifteen physics queries per attempt. A player sprinting or gliding into an empty corner
///         of the zone therefore fills it over a couple of seconds rather than in one tick, and the
///         shard keeps answering its clients while it does.
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

    /// <summary>
    ///     How many distinct other zones are named in the "players are elsewhere" announcement and
    ///     in <see cref="DescribeStatus"/>. The count is exact however many zones there are; only the
    ///     listing is capped, so a crowd spread over the whole zone picker does not produce a
    ///     paragraph-long log line.
    /// </summary>
    private const int MaxElsewhereZonesListed = 4;

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
    private readonly List<string> _elsewhereZones = [];
    private readonly List<WorldPopulationCell> _deactivating = [];

    /// <summary>
    ///     How much planner work one step of the plan worker asks for. The phases are sequential and
    ///     each step finishes a phase, so the number is only a ceiling: the point of the worker is
    ///     that the budget that used to pace the plan (20,000 a tick) no longer bounds it.
    /// </summary>
    private const int PlanWorkerStepBudget = 1_000_000;

    private ulong _lastUpdate;
    private ulong _lastSpawnWindow;
    private int _spawnedThisWindow;
    private int _consecutiveFailures;
    private bool _occupancySeeded;
    private IdleReason _announcedIdle = IdleReason.None;

    /// <summary>
    ///     How many threads the plan build uses: 0 keeps the build on the shard's tick, one budget
    ///     slice per update, which is what a test shard and the <c>WorldPopulationPlanOnWorkers=false</c>
    ///     setting ask for; a positive number builds it on a background worker using that many
    ///     threads.
    /// </summary>
    private readonly int _planThreads;

    /// <summary>Guards the worker's start/stop handshake, which the tick and the worker both touch.</summary>
    private readonly object _planWorkerLock = new();

    /// <summary>The running plan worker, or null; replaced when one finished and the plan is not.</summary>
    private Task? _planWorker;

    /// <summary>
    ///     A failure the worker caught, waiting for the shard's tick to report it - the failure
    ///     handling below (counting failures, turning the feature off, clearing the NPCs) touches
    ///     entities, which only the tick thread may do.
    /// </summary>
    private Exception? _planWorkerFailure;

    /// <summary>How long the zone has been waiting for its plan, started with the first build attempt.</summary>
    private readonly Stopwatch _planWait = new();

    /// <summary>Whether the "the plan is ready" line has been said, so it is said once.</summary>
    private bool _planReadyAnnounced;

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
        PlayersElsewhere,
        Planning,
    }

    /// <param name="planWorkerThreads">
    ///     Threads the plan build may use, and with them whether it happens at all inside the shard's
    ///     tick: 0 (the default, and what the tests use) builds the plan on the tick exactly one
    ///     <see cref="IWorldPopulationRules.PlanWorkPerTick" /> slice at a time, the way it always
    ///     did; a positive number builds it on a background worker that uses up to that many threads
    ///     and hands the finished plan to the tick. A live shard passes the resolved
    ///     <c>WorldPopulationPlanThreads</c> (half the box by default), so a zone with a player in it is planned in about the time its
    ///     CPU work takes instead of in tens of seconds of budgeted updates - and the shard's tick,
    ///     which used to pay for every one of those updates, does not pay at all. The plan itself is
    ///     the same plan either way.
    /// </param>
    public WorldPopulationService(
        IShard shard,
        IWorldPopulationRules rules,
        IWorldPopulationDataSource data,
        IWorldPopulationTerrain terrain,
        IWorldPopulationSpawner spawner,
        int planWorkerThreads = 0)
    {
        _shard = shard;
        _logger = shard.Logger.ForContext<WorldPopulationService>();
        _rules = rules;
        _terrain = terrain;
        _spawner = spawner;
        _planThreads = Math.Max(0, planWorkerThreads);

        // The placement grid hashes at a finer resolution than the plan's cells: its queries are a
        // couple of body radii wide, not 32 m.
        _occupancy = new SpawnOccupancyGrid(Math.Max(4f, rules.CellSize / 4f));
        _planner = new WorldPopulationPlanner(
            shard.ZoneId,
            rules,
            data,
            terrain,
            shard.Logger.ForContext<WorldPopulationPlanner>(),

            // 1 rather than 0 when the plan is built inside the tick: that path makes no promises
            // about wall clock (it is the "somebody measured their machine, use less of it" setting),
            // and a parallel phase inside the tick is exactly the burst of threads it exists to avoid.
            maxDegreeOfParallelism: Math.Max(1, _planThreads));

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

    /// <summary>
    ///     Total slots handed back to the queue because an update ran out of
    ///     <see cref="IWorldPopulationRules.PlacementAttemptsPerUpdate"/> before trying them. A
    ///     healthy shard sees these whenever a player streams in more ground than one update can
    ///     validate; a number that climbs without the live count following it is the budget set
    ///     lower than the zone needs.
    /// </summary>
    public int DeferredPlacements { get; private set; }

    /// <summary>How many bodies the placement grid is holding.</summary>
    public int OccupancyCount => _occupancy.Count;

    /// <summary>How many players the last update measured the world against.</summary>
    public int PlayerCount => _players.Count;

    /// <summary>
    ///     How many connected players the last update found in zones other than the shard's own.
    ///     They neither activate cells nor count as present: one shard runs one zone, and this
    ///     plan's ground is the shard's.
    /// </summary>
    public int PlayersElsewhereCount { get; private set; }

    /// <summary>
    ///     The other zones those players are in, as display strings, up to
    ///     <c>MaxElsewhereZonesListed</c>. Empty when nobody is elsewhere.
    /// </summary>
    public IReadOnlyList<string> PlayersElsewhereZones => _elsewhereZones;

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
            Update(currentTime, ct);
            _consecutiveFailures = 0;
        }
        catch (Exception ex)
        {
            ReportFailure(ex);
        }
    }

    /// <summary>
    ///     Counts one failed population update and, when it is the third in a row, turns the feature
    ///     off and takes its NPCs with it. Both the tick's own failures and the plan worker's go
    ///     through here, because the two halves of the system fail for the same kind of reason
    ///     (the zone's data does not suit the code) and an operator should not have to read two
    ///     different counters to see it.
    /// </summary>
    /// <param name="ex">What went wrong.</param>
    private void ReportFailure(Exception ex)
    {
        _consecutiveFailures++;
        _logger.Error(
            ex,
            "World population update failed ({Count} in a row); the feature turns itself off after {Max}",
            _consecutiveFailures,
            MaxConsecutiveFailures);

        if (_consecutiveFailures < MaxConsecutiveFailures)
        {
            return;
        }

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

    private void Update(ulong currentTime, CancellationToken ct)
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

        if (!_rules.SpawnFullZone && _players.Count == 0)
        {
            if (PlayersElsewhereCount > 0)
            {
                // The shard has players, but none of them are in its zone. Same treatment as
                // nobody at all: the plan stays (it is only memory), the world does not - and
                // the plan is not built either, since there is nobody here to stream it to.
                if (LiveCount > 0 || _activeCells.Count > 0)
                {
                    Clear("no players in this shard's zone");
                }

                AnnouncePlayersElsewhere();
                return;
            }

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
            // A worker's failure is reported here, on the thread that may touch entities.
            var planFailure = Interlocked.Exchange(ref _planWorkerFailure, null);
            if (planFailure != null)
            {
                ReportFailure(planFailure);
                if (!Enabled)
                {
                    return;
                }
            }

            _planWait.Start();

            if (_planThreads > 0)
            {
                // The plan is somebody else's job now: this tick only starts the worker when there
                // is not one running and watches for the plan it publishes. The tick's own cost is
                // the work the plan build cannot do on another thread - activating cells, placing
                // NPCs, refilling what died - which is where it was always needed.
                StartPlanWorker(ct);
            }
            else
            {
                _ = _planner.Work(Math.Max(1, _rules.PlanWorkPerTick));
            }

            if (!_planner.IsComplete)
            {
                AnnouncePlanning();
                return;
            }
        }

        if (!_planReadyAnnounced && _planner.IsComplete)
        {
            _planReadyAnnounced = true;
            LogPlanReady();
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
    ///     Says an update found players only in other zones, once until that changes. This is the
    ///     announcement that answers "the zone is empty" when the operator is looking at the wrong
    ///     zone: the character selection screen is a zone picker, but the shard only runs the zone
    ///     it was started with (<c>ZoneId</c>), so a player anywhere else stands on ground this plan
    ///     knows nothing about.
    /// </summary>
    private void AnnouncePlayersElsewhere()
    {
        if (_announcedIdle == IdleReason.PlayersElsewhere)
        {
            return;
        }

        _announcedIdle = IdleReason.PlayersElsewhere;

        _logger.Information(
            "World population: spawning nothing in zone {ZoneId} - {ElsewhereCount} connected player(s) are in other zones " +
            "({ElsewhereZones:l}), and one shard runs one zone: its collision, entities and population all belong to zone {ZoneId}. " +
            "To play in one of those zones instead, set ZoneId to its id in the server config and restart",
            _shard.ZoneId,
            PlayersElsewhereCount,
            string.Join(", ", _elsewhereZones));
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

        // Said with where the work is happening: "20,000 per update" described a plan that was being
        // drip-fed to the tick, and an operator who reads it while the plan is being built on worker
        // threads would be looking for progress in the wrong place.
        string pace = _planThreads > 0
            ? $"built on {_planThreads} worker thread(s), off the shard's tick"
            : $"{_rules.PlanWorkPerTick} per update";

        _logger.Information(
            "World population: still building the plan for zone {ZoneId} ({Surfaces} walkable surfaces scanned, {Cells} cells so far, {Pace}); nothing spawns until it finishes",
            _shard.ZoneId,
            _planner.ScannedSurfaces,
            _planner.CellCount,
            pace);
    }

    /// <summary>
    ///     Starts the worker that builds the plan when one is not already building it. Called from
    ///     the shard's tick; the worker itself only ever touches the planner (its own data source and
    ///     the loaded zone's navigation mesh), never the shard's clients, entities or channels.
    /// </summary>
    /// <param name="ct">The shard's cancellation token, so a stopping server stops the build too.</param>
    private void StartPlanWorker(CancellationToken ct)
    {
        lock (_planWorkerLock)
        {
            if (_planWorker is { IsCompleted: false })
            {
                return;
            }

            _planWorker = Task.Factory.StartNew(
                () => BuildPlan(ct),
                ct,
                TaskCreationOptions.LongRunning | TaskCreationOptions.DenyChildAttach,
                TaskScheduler.Default);
        }
    }

    /// <summary>
    ///     Builds the plan to completion on a worker, one phase step at a time, and reports how long
    ///     it took. Never lets an exception escape: a caught failure is handed to the tick through
    ///     <see cref="_planWorkerFailure" />, because turning the feature off means despawning its
    ///     NPCs and only the tick's thread may do that.
    /// </summary>
    /// <param name="ct">The shard's cancellation token.</param>
    private void BuildPlan(CancellationToken ct)
    {
        var started = Stopwatch.StartNew();

        try
        {
            while (!_planner.IsComplete && !ct.IsCancellationRequested)
            {
                _ = _planner.Work(PlanWorkerStepBudget);
            }
        }
        catch (Exception ex)
        {
            _planWorkerFailure = ex;
            return;
        }

        if (!_planner.IsComplete)
        {
            // The shard is stopping; the plan is simply unfinished, and nobody is waiting for it.
            return;
        }

        started.Stop();
        _logger.Information(
            "World population plan for zone {ZoneId}: built on {Threads} worker thread(s) in {Elapsed} (off the shard's tick), {Cells} cells, {Slots} slots",
            _shard.ZoneId,
            _planThreads,
            started.Elapsed,
            _planner.CellCount,
            _planner.SlotCount);
    }

    /// <summary>
    ///     Says the plan has just become usable, once: how big it is and how long the zone waited for
    ///     it. The wait is the number that used to be tens of seconds.
    /// </summary>
    private void LogPlanReady()
    {
        _logger.Information(
            "World population: plan for zone {ZoneId} is ready after {Elapsed} ({Cells} cells, {Slots} slots, {Placed} of {Roster} monster rows placed) - cells around the players activate from here",
            _shard.ZoneId,
            _planWait.Elapsed,
            _planner.CellCount,
            _planner.SlotCount,
            _planner.PlacedRosterCount,
            _planner.RosterCount);
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
        int maxLive = _rules.SpawnFullZone ? _planner.SlotCount : _rules.MaxLiveNpcs;
        _ = text.AppendLine(Enabled
            ? $"World population: on (live {LiveCount}/{maxLive} NPCs of {_planner.RosterCount} monster rows, {CountDistinctLiveTypes()} kinds in the world)"
            : "World population: off");
        _ = text.AppendLine(_planner.IsComplete
            ? $"Plan: {_planner.CellCount} cells, {_planner.SlotCount} slots, {_planner.PlacedRosterCount} rows placed" +
              (_planner.UnplacedRosterCount > 0 ? $", {_planner.UnplacedRosterCount} rows have no ground of their kind in this zone" : string.Empty) +
              (_planner.RefusedChunkCells > 0 ? $", {_planner.RefusedChunkCells} cells refused by chunk rules" : string.Empty) +
              (_planner.UsedAnchorFallback ? ", built from authored anchors (no walkable surfaces)" : string.Empty) +
              (_planThreads > 0 ? $", built on {_planThreads} worker thread(s) off the shard's tick" : string.Empty)
            : $"Plan: building ({_planner.ScannedSurfaces} surfaces scanned, {_planner.CellCount} cells so far" +
              (_planThreads > 0
                  ? $", on {_planThreads} worker thread(s) off the shard's tick)"
                  : $", {_rules.PlanWorkPerTick} per update)"));
        string players = PlayerCount.ToString(CultureInfo.InvariantCulture) + " players";
        if (PlayersElsewhereCount > 0)
        {
            players += $" ({PlayersElsewhereCount} in other zones: {string.Join(", ", _elsewhereZones)})";
        }

        _ = text.AppendLine(string.Format(
            CultureInfo.InvariantCulture,
            "Streaming: {0} active cells, {1} slots queued, {2}, {3}",
            ActiveCellCount,
            PendingSlotCount,
            players,
            _rules.SpawnFullZone
                ? "full zone (persistent)"
                : string.Format(CultureInfo.InvariantCulture, "activate {0:0} m / deactivate {1:0} m", _rules.ActivationRadius, _rules.DeactivationRadius)));
        _ = text.AppendLine(string.Format(
            CultureInfo.InvariantCulture,
            "Lifetime: {0} spawned, {1} despawned, {2} lost, {3} placements refused, {4} slots parked, {5} placement deferrals, {6} bodies in the placement grid",
            SpawnedTotal,
            DespawnedTotal,
            LostTotal,
            RefusedPlacements,
            ParkedSlotCount,
            DeferredPlacements,
            OccupancyCount));

        // Only when the terrain has an overhead-cover rule and it has seen something: a terrain
        // without one (the test terrains, a shard with no collision) prints nothing.
        if (_terrain.CoverRefusals > 0 || _terrain.CoverRuleSuspended)
        {
            _ = text.AppendLine(string.Format(
                CultureInfo.InvariantCulture,
                "Cover: {0} spots refused as covered{1}",
                _terrain.CoverRefusals,
                _terrain.CoverRuleSuspended
                    ? " - the rule is suspended for this zone, because it refused almost every spot it judged; spots are placed on the plan's ground again"
                    : string.Empty));
        }

        if (_terrain.ZoneBoundsMin.HasValue && _terrain.ZoneBoundsMax.HasValue)
        {
            _ = text.AppendLine(string.Format(
                CultureInfo.InvariantCulture,
                "Zone: bounds {0} to {1}, {2} path layers (vehicle, not NPC), {3} melding perims, {4} subzone regions, {5} encounter names (from map file)",
                _terrain.ZoneBoundsMin.Value,
                _terrain.ZoneBoundsMax.Value,
                _shard.Physics?.ZonePaths?.Count ?? 0,
                _shard.Physics?.MeldingPerimeters?.Count ?? 0,
                _shard.Physics?.SubZoneRegionCount ?? 0,
                _shard.Physics?.EncounterNameCount ?? 0));
        }

        return text.ToString().TrimEnd();
    }

    private void CollectPlayers()
    {
        _players.Clear();
        PlayersElsewhereCount = 0;
        _elsewhereZones.Clear();

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

            // One shard runs one zone: its collision, its authored entities and this plan all
            // belong to the shard's ZoneId. A player in any other zone (the character selection
            // screen is a zone picker) stands on ground this plan knows nothing about, so they
            // neither activate cells nor count as present - populating around their position
            // would plan New Eden's NPCs onto Sertao's coordinates.
            if (!ShardZone.IsPlayerInZone(_shard, client))
            {
                PlayersElsewhereCount++;

                if (_elsewhereZones.Count < MaxElsewhereZonesListed)
                {
                    // Non-null here: a null zone counts as in-zone, so the gate above passed it.
                    string label = DescribeZone(client.CurrentZone);
                    if (!_elsewhereZones.Contains(label))
                    {
                        _elsewhereZones.Add(label);
                    }
                }

                continue;
            }

            // From actual map file: ZoneBoundsLayer (0x21000) gives AABB. If player is outside bounds
            // (e.g. void, far outside), don't activate cells - treat as not present for population.
            // This is data-backed from client maps, not invented.
            if (!_terrain.IsInsideZoneBounds(character.Position))
            {
                // Player outside zone bounds - don't count for population, but don't count as elsewhere either
                // They are in correct zone but in void. Log at debug.
                _logger.Debug("World population: player {Player} outside zone {ZoneId} bounds {Min} {Max} at {Pos} - not activating cells",
                    client.CharacterEntity?.EntityId ?? 0, _shard.ZoneId, _terrain.ZoneBoundsMin, _terrain.ZoneBoundsMax, character.Position);
                continue;
            }

            _players.Add(character.Position);
        }
    }

    private static string DescribeZone(Zone zone) =>
        string.IsNullOrEmpty(zone.Name) ? $"zone {zone.ID}" : $"{zone.Name} ({zone.ID})";

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
        if (_rules.SpawnFullZone)
        {
            foreach (var cell in _planner.Cells.Values)
            {
                if (_activeCells.ContainsKey(cell.Key) || cell.Slots.Count == 0)
                {
                    continue;
                }

                ActivateCell(cell, currentTime);
            }

            return;
        }

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
        // Skip cells whose center is outside zone bounds (from actual map file ZoneBoundsLayer)
        // This prevents activating cells in void where navigation mesh may have no faces but anchor fallback created cells
        if (!_terrain.IsInsideZoneBounds(cell.Center))
        {
            return;
        }

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
            slot.ResetPlacementRound();
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
            slot.ResetPlacementRound();

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

        int budget = _rules.SpawnFullZone
            ? _rules.SpawnBudget - _spawnedThisWindow
            : Math.Min(_rules.SpawnBudget - _spawnedThisWindow, _rules.MaxLiveNpcs - LiveCount);
        if (budget <= 0)
        {
            return;
        }

        // A slot that is not due yet goes back on the queue, so the loop has to stop after one pass
        // through what was queued rather than chase its own tail.
        int guard = _pending.Count;

        // Work, not spawns, is what this loop spends. Every attempt costs about fifteen physics
        // queries (ground probe, standing-volume probes, overhead-cover probe), so a queue of
        // refused slots used to burn the whole shard tick before it produced anything: the shard
        // stopped answering its clients for as long as that took, which is the ping spike a player
        // sees while gliding across a zone (each new cell refills the queue) and the connect timeout
        // of a client trying to get back into the world. The budget bounds one update; what it does
        // not get to is deferred - not failed - and is tried on the next update.
        int work = Math.Max(1, _rules.PlacementAttemptsPerUpdate);

        while (budget > 0 && work > 0 && guard-- > 0 && _pending.TryDequeue(out var slot))
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

            switch (TryPlace(slot, work, out int spent))
            {
                case PlacementOutcome.Placed:
                    work -= spent;
                    budget--;
                    break;
                case PlacementOutcome.RefusedByGround:
                    work -= spent;
                    RetryLater(slot, currentTime, countsAsFailure: true);
                    break;
                case PlacementOutcome.Deferred:
                    // Out of work for this update. Back on the queue, where it keeps both its place
                    // and the attempt its round had reached: no failure is counted, so a slot the
                    // ground refuses is still parked only after MaxPlacementFailures full rounds of
                    // trying, not because the update ran out of work.
                    work -= spent;
                    _pending.Enqueue(slot);
                    DeferredPlacements++;
                    break;
                default:
                    work -= spent;
                    RetryLater(slot, currentTime, countsAsFailure: false);
                    break;
            }
        }
    }

    /// <summary>
    ///     Tries to place one slot, spending at most <paramref name="workBudget"/> attempts and
    ///     reporting what it spent in <paramref name="attemptsSpent"/>. Running out of budget is not
    ///     a refusal: the attempt the round had reached is recorded on the slot and the caller gets
    ///     <see cref="PlacementOutcome.Deferred"/> back, so the slot keeps its progress and its place
    ///     in the queue instead of being charged a failure it did not have.
    /// </summary>
    /// <remarks>
    ///     A round - the slot's anchor and the jittered positions around it, up to
    ///     <see cref="IWorldPopulationRules.MaxPlacementAttempts"/> of them - may therefore span
    ///     several updates. What makes a round a round is unchanged: it is one pass over the slot's
    ///     positions, and its result is what counts a failure towards parking the slot.
    /// </remarks>
    private PlacementOutcome TryPlace(WorldPopulationSlot slot, int workBudget, out int attemptsSpent)
    {
        var candidate = slot.Candidate;
        float radius = candidate.ResolvedBodyRadius(_rules);
        float height = candidate.ResolvedBodyHeight(_rules);
        int attempts = Math.Max(1, _rules.MaxPlacementAttempts);

        attemptsSpent = 0;

        for (int attempt = slot.AttemptsThisRound; attempt < attempts; attempt++)
        {
            if (attemptsSpent >= workBudget)
            {
                // Out of placement work for this update. The round continues where it stopped the
                // next time the slot is dequeued; the slot is due immediately, because a deferral
                // sets no NotBefore.
                slot.AttemptsThisRound = attempt;
                return PlacementOutcome.Deferred;
            }

            attemptsSpent++;

            var position = attempt == 0 ? slot.Anchor : slot.Anchor + RetryJitter(slot, attempt);

            if (IsTooCloseToAPlayer(position) ||
                !_occupancy.IsAreaFree(position, radius, _rules.MinSeparation))
            {
                slot.RoundRefusedForRoom = true;
                continue;
            }

            if (!_terrain.TryResolveStandingSpot(position, radius, height, out var resolved))
            {
                slot.RoundRefusedByGround = true;
                continue;
            }

            // The snap keeps X and Y, but re-check anyway: the resolved spot is the one that is
            // actually going to be occupied.
            if (IsTooCloseToAPlayer(resolved) ||
                !_occupancy.IsAreaFree(resolved, radius, _rules.MinSeparation))
            {
                slot.RoundRefusedForRoom = true;
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
                slot.ResetPlacementRound();
                return PlacementOutcome.RefusedByGround;
            }

            slot.EntityId = entityId;
            slot.NotBefore = 0;
            slot.Failures = 0;
            slot.ResetPlacementRound();
            _occupancy.Add(entityId, resolved, radius);
            _liveSlots.Add(slot);
            SpawnedTotal++;
            _spawnedThisWindow++;

            return PlacementOutcome.Placed;
        }

        RefusedPlacements++;

        // A slot only ever refused by the ground is a slot whose ground does not fit its body; a
        // slot that was also refused for room is worth another look later, when whatever was
        // standing there has moved. The round is over either way, so the next one starts at the
        // anchor again.
        bool refusedByGroundOnly = slot.RoundRefusedByGround && !slot.RoundRefusedForRoom;
        slot.ResetPlacementRound();

        return refusedByGroundOnly
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
                LogParkedSlot(slot);
                return;
            }
        }

        _pending.Enqueue(slot);
    }

    /// <summary>
    ///     Says the first slot that parks, and then every fiftieth, naming the row and the patch of
    ///     ground it was trying to stand on. A zone that parks its plan used to do it in complete
    ///     silence - the status counted the parks and nothing in the log explained them - which is
    ///     exactly the shape of an empty world nobody can diagnose from the log alone. (#114 had
    ///     started saying this; #115 removed the line with the check it belonged to.)
    /// </summary>
    private void LogParkedSlot(WorldPopulationSlot slot)
    {
        if (ParkedSlotCount != 1 && ParkedSlotCount % 50 != 0)
        {
            return;
        }

        _logger.Information(
            "World population: slot parked ({Parked} so far) - dbcharacter::Monster row {MonsterId} found no ground it fits around {Anchor} in zone {ZoneId} after {Failures} rounds of jittered attempts, so that cell's slot is out of the plan",
            ParkedSlotCount,
            slot.Candidate.MonsterId,
            slot.Anchor,
            _shard.ZoneId,
            slot.Failures);
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

        /// <summary>
        ///     The update ran out of placement work before this slot had spent its attempts. Not a
        ///     refusal: the slot goes back on the queue and is tried on the next update, with no
        ///     failure charged.
        /// </summary>
        Deferred,
    }
}
