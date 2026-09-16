using System;
using System.Collections.Generic;
using System.Linq;
using System.Numerics;
using GameServer.Systems.Ai;
using Serilog;

namespace GameServer.Systems.Spawning.Population;

/// <summary>
///     Turns a loaded zone and the monster table into a plan: cells of walkable ground, each with the
///     habitat and level the database gave it, each holding the slots of the NPCs that belong there.
///     The plan is built once and then streamed by <see cref="WorldPopulationService"/>.
/// </summary>
/// <remarks>
///     <para>
///         <b>Where the positions come from.</b> The client database has no per-zone spawn table -
///         that lived in the live server's spawn groups, which never shipped in <c>clientdb.sd2</c> -
///         so the plan puts NPCs on the ground the zone itself says they can stand on: the centroids
///         of the navigation mesh's walkable faces, which are baked from the zone's own collision with
///         the slope cutoff and the chunk metadata's pathing exclusions already applied. Which kinds
///         of ground a cell is, and what level it carries, come from the authored data the server
///         already spawns the zone from (outpost camps sized by
///         <see cref="IWorldPopulationRules.OutpostSettlementRadius"/> rather than the capture
///         circle, deployables, Melding perimeters; level still follows the nearest outpost's
///         band). A zone without collision falls back to those authored positions, which is the
///         only ground left whose height the data vouches for. Walkable faces alone do not say
///         where an NPC may actually stand - a cave floor is flat and walkable, and nothing in the
///         mesh knows the sky - so a cell is refused when the zone's collision covers its ground
///         from above (caves, tunnels, ground under roofs and rock overhangs), and a planned spot
///         is checked the same way again when the placement probe validates it.
///     </para>
///     <para>
///         <b>Which monsters go where.</b> Two passes. The first gives every admitted monster row one
///         slot somewhere it fits, so that a zone's plan carries the whole roster the zone can host
///         rather than a sample of it; a row is refused only when the zone has no ground of its kind
///         at all (a settlement NPC in a zone with no outpost), which the status reports rather than
///         hides. The second fills the remaining room for density, picking rows by the frequency
///         <see cref="WorldPopulationCandidate.DensityWeight"/> reads out of their
///         <c>difficulty_cost</c> and stopping at the cell's difficulty budget. The result is the
///         shape the original game's encounters had: common ambient rows in groups, expensive ones
///         alone.
///     </para>
///     <para>
///         Everything is deterministic - roster order, cell order, jitter, facing, density picks - so
///         two servers with the same database and the same zone plan the same world.
///     </para>
/// </remarks>
public sealed class WorldPopulationPlanner
{
    /// <summary>
    ///     Side length of the anchor buckets in metres. Larger than the biggest habitat influence
    ///     the planner actually applies (Melding 120 m, outpost camp 80 m, deployable 25 m; an
    ///     outpost's authored capture radius reaches 550 m but is not used as habitat), so scanning
    ///     a bucket and its eight neighbours always finds every anchor that can reach the cell.
    ///     Level still reads the nearest banded anchor in the same neighbourhood.
    /// </summary>
    private const float AnchorBucketSize = 1024f;

    /// <summary>
    ///     How many cells one monster row probes for its coverage slot before it is reported as
    ///     unplaceable. The cursor makes the probes land on cells the previous rows did not use, so
    ///     this is a guard against a pathological plan (a row whose habitat has nothing but full
    ///     cells), not the normal path.
    /// </summary>
    private const int CoverageProbeLimit = 64;

    /// <summary>
    ///     The plan work one cell costs while the terrain can cover ground. A plain cell is a
    ///     neighbourhood scan of the anchors around it; the cell's sky check is a pair of ray casts
    ///     over the zone's static geometry, so it is charged against the same budget at a multiple
    ///     of that, which is what keeps the casts inside the per update work the plan is allowed.
    ///     A budget smaller than the cost still builds one cell a call (see <see cref="BuildCells"/>),
    ///     so the plan cannot stall however small <c>PlanWorkPerTick</c> is set.
    /// </summary>
    private const int CellWithSkyCheckWork = 64;

    /// <summary>
    ///     How far into its cell a slot's planned position may sit, as a fraction of the cell size.
    ///     0.4 of 32 m keeps a slot inside its own cell (half of it is 16 m) with room for the body's
    ///     own radius.
    /// </summary>
    private const float JitterFraction = 0.4f;

    /// <summary>How far a slot may turn away from its cell's facing, in radians (~34° either way).</summary>
    private const float FacingSpread = 0.6f;

    // Salts keep the hashes that decide different things independent of each other.
    private const int DensityOrderSalt = 0x51ED;
    private const int FacingSpreadSalt = 0x7A11;
    private const int JitterSalt = 0x2F31;
    private const int FacingSalt = 0x6C07;
    private const int DensityPickSalt = 0x1B87;

    private enum Phase
    {
        Surfaces,
        Cells,
        Assign,
        Complete,
    }

    private readonly uint _zoneId;
    private readonly IWorldPopulationRules _rules;
    private readonly IWorldPopulationDataSource _data;
    private readonly IWorldPopulationTerrain _terrain;
    private readonly ILogger _logger;

    private readonly Dictionary<long, CellDraft> _drafts = [];
    private readonly Dictionary<long, WorldPopulationCell> _cells = [];
    private readonly Dictionary<(int X, int Y), List<int>> _anchorBuckets = [];
    private readonly Dictionary<WorldPopulationHabitat, int> _coverageCursors = [];

    private readonly List<long> _keysInDensityOrder = [];
    private readonly List<WorldPopulationCandidate> _roster = [];

    private readonly List<WorldPopulationCell> _wildernessCells = [];
    private readonly List<WorldPopulationCell> _settlementCells = [];
    private readonly List<WorldPopulationCell> _meldingCells = [];

    private readonly List<WorldPopulationCandidate> _wildernessPool = [];
    private readonly List<WorldPopulationCandidate> _settlementPool = [];
    private readonly List<WorldPopulationCandidate> _meldingPool = [];

    private IReadOnlyList<WorldPopulationAnchor> _anchors = Array.Empty<WorldPopulationAnchor>();

    // Tolerate a misconfigured rules object rather than planning nothing or dividing by zero.
    private readonly float _cellSize;
    private readonly int _maxNpcsPerCell;
    private readonly int _maxPlannedSlots;

    private Phase _phase = Phase.Surfaces;
    private int _nextSurface;

    /// <summary>Draft keys in ascending order, i.e. the order cells are built in.</summary>
    private readonly List<long> _draftKeys = [];

    private int _nextDraft;
    private bool _cellsPrepared;

    public WorldPopulationPlanner(
        uint zoneId,
        IWorldPopulationRules rules,
        IWorldPopulationDataSource data,
        IWorldPopulationTerrain terrain,
        ILogger logger)
    {
        _zoneId = zoneId;
        _rules = rules;
        _data = data;
        _terrain = terrain;
        _logger = logger;

        _cellSize = rules.CellSize > 0f ? rules.CellSize : 32f;
        _maxNpcsPerCell = Math.Max(1, rules.MaxNpcsPerCell);
        _maxPlannedSlots = Math.Max(1, rules.MaxPlannedSlots);
    }

    /// <summary>Whether the plan is finished.</summary>
    public bool IsComplete => _phase == Phase.Complete;

    /// <summary>How many cells the plan has.</summary>
    public int CellCount => _cells.Count;

    /// <summary>How many slots the plan has, i.e. how many NPCs it can hold over the whole zone.</summary>
    public int SlotCount { get; private set; }

    /// <summary>How many monster rows were admitted as world population.</summary>
    public int RosterCount => _roster.Count;

    /// <summary>How many admitted rows got at least one slot somewhere they fit.</summary>
    public int PlacedRosterCount { get; private set; }

    /// <summary>
    ///     How many admitted rows this zone cannot host: rows whose habitat the zone has no ground
    ///     for (settlement NPCs in a zone with no outpost, Melding creatures in a zone with no
    ///     Melding). Reported rather than silently dropped.
    /// </summary>
    public int UnplacedRosterCount { get; private set; }

    /// <summary>How many cells were refused because the zone's chunk metadata says the server does not simulate them.</summary>
    public int RefusedChunkCells { get; private set; }

    /// <summary>
    ///     How many cells were refused because the zone's collision covers their ground from above:
    ///     cave floors, tunnels, ground under roofs or rock overhangs. Reported next to the chunk
    ///     refusals, so a zone whose ground is largely covered says why its plan is smaller than its
    ///     mesh.
    /// </summary>
    public int RefusedCoveredCells { get; private set; }

    /// <summary>Whether the plan was built from authored anchor positions because the zone has no walkable surfaces.</summary>
    public bool UsedAnchorFallback { get; private set; }

    /// <summary>How many walkable surface points the zone had.</summary>
    public int ScannedSurfaces => _nextSurface;

    /// <summary>The plan's cells by grid key. Empty until the plan is complete.</summary>
    public IReadOnlyDictionary<long, WorldPopulationCell> Cells => _cells;

    /// <summary>
    ///     Advances the plan by at most <paramref name="budget"/> units of work - one navigation
    ///     mesh face scanned per unit, a cell built per unit, or <see cref="CellWithSkyCheckWork"/>
    ///     units for a cell the terrain checks against the sky, one step for the slot assignment -
    ///     and returns how many it used. Called repeatedly until <see cref="IsComplete"/>, which is
    ///     how a zone gets planned over several ticks instead of stalling one: a full zone's mesh
    ///     has up to a few hundred thousand faces and becomes tens of thousands of cells, each of
    ///     which is classified against the anchors around it and checked against the sky.
    /// </summary>
    public int Work(int budget)
    {
        return _phase switch
        {
            Phase.Surfaces => ScanSurfaces(Math.Max(1, budget)),
            Phase.Cells => BuildCells(Math.Max(1, budget)),
            Phase.Assign => Step(AssignSlots),
            _ => 0,
        };
    }

    private static int Step(Action phase)
    {
        phase();
        return 1;
    }

    private int ScanSurfaces(int budget)
    {
        int total = _terrain.SurfaceCount;
        int done = 0;

        while (_nextSurface < total && done < budget)
        {
            if (_terrain.TryGetSurface(_nextSurface, out var position))
            {
                Accumulate(position);
            }

            _nextSurface++;
            done++;
        }

        if (_nextSurface >= total)
        {
            _phase = Phase.Cells;
        }

        return done;
    }

    private void Accumulate(Vector3 position)
    {
        var (x, y) = WorldPopulationCell.CellIndexOf(position, _cellSize);
        long key = WorldPopulationCell.MakeKey(x, y);

        if (!_drafts.TryGetValue(key, out var draft))
        {
            draft = new CellDraft { X = x, Y = y };
            _drafts[key] = draft;
        }

        draft.Sum += position;
        draft.Count++;
    }

    private int BuildCells(int budget)
    {
        if (!_cellsPrepared)
        {
            _cellsPrepared = true;
            PrepareCells();
        }

        // The terrain can cover ground - and the cell's sky check is a real pair of ray casts over
        // the zone's static geometry - when it had walkable surfaces to give, i.e. when it is the
        // physics engine's. A plan built from authored anchors alone has no ground the sky check
        // could refuse, so its cells stay at plain cost.
        int cellCost = _terrain.HasSurfaces ? CellWithSkyCheckWork : 1;

        int done = 0;
        while (_nextDraft < _draftKeys.Count && done + cellCost <= budget)
        {
            BuildCell(_draftKeys[_nextDraft]);
            _nextDraft++;
            done += cellCost;
        }

        // A budget smaller than one checked cell would build nothing, and the plan would never
        // finish: build one anyway. One pair of ray casts an update is nothing, and it keeps a
        // misconfigured PlanWorkPerTick from stalling the plan.
        if (done == 0 && _nextDraft < _draftKeys.Count && cellCost > 1)
        {
            BuildCell(_draftKeys[_nextDraft]);
            _nextDraft++;
        }

        if (_nextDraft >= _draftKeys.Count)
        {
            FinishCells();
        }

        return done;
    }

    /// <summary>Reads the data the cells are classified against and freezes the order the cells are built in.</summary>
    private void PrepareCells()
    {
        _anchors = _data.GetAnchors(_zoneId) ?? Array.Empty<WorldPopulationAnchor>();
        BucketAnchors();

        if (_drafts.Count == 0)
        {
            // No walkable ground to plan on: either the zone has no collision data or none of it is
            // walkable. The authored anchors are the only positions left whose height the data
            // vouches for, so each becomes a cell of its own.
            foreach (var anchor in _anchors)
            {
                Accumulate(anchor.Position);
            }

            UsedAnchorFallback = _drafts.Count > 0;
        }

        _roster.AddRange(_data.GetCandidates() ?? Array.Empty<WorldPopulationCandidate>());

        // Ascending key order: dictionary order would depend on insertion history, and the plan is
        // meant to be the same plan everywhere.
        _draftKeys.AddRange(_drafts.Keys.OrderBy(draftKey => draftKey));
        _nextDraft = 0;
    }

    private void BuildCell(long key)
    {
        var draft = _drafts[key];
        var center = draft.Count > 0 ? draft.Sum / draft.Count : Vector3.Zero;

        var cell = new WorldPopulationCell(key, draft.X, draft.Y)
        {
            Center = center,
            SurfaceCount = draft.Count,
            ChunkRecordId = _terrain.GetChunkRecordId(center),
        };

        if (!_data.IsChunkSpawnable(_zoneId, cell.ChunkRecordId))
        {
            RefusedChunkCells++;
            return;
        }

        // The zone's collision covers this ground from above - a cave floor, a tunnel, ground
        // under a roof or a rock overhang. The navigation mesh calls all of it walkable, because a
        // cave floor is flat and nothing in the mesh knows the sky; this is the check that does. A
        // cell refused here never gets slots, so a row that belongs to the camp above - a vendor -
        // takes its coverage slot on the open ground of the camp instead of one the cave holds.
        if (!_terrain.IsExposedToSky(center))
        {
            RefusedCoveredCells++;
            return;
        }

        Classify(cell);

        _cells[key] = cell;
        _keysInDensityOrder.Add(key);

        switch (cell.Habitat)
        {
            case WorldPopulationHabitat.Settlement:
                _settlementCells.Add(cell);
                break;
            case WorldPopulationHabitat.Melding:
                _meldingCells.Add(cell);
                break;
            default:
                _wildernessCells.Add(cell);
                break;
        }
    }

    private void FinishCells()
    {
        _drafts.Clear();
        _draftKeys.Clear();

        // Density is filled in a scattered but deterministic order: sweeping the grid in key order
        // would spend the whole slot budget on one corner of the zone before reaching the next.
        _keysInDensityOrder.Sort((a, b) =>
            WorldPopulationHash.Mix(a, DensityOrderSalt).CompareTo(WorldPopulationHash.Mix(b, DensityOrderSalt)));

        BuildCandidatePools();

        _phase = Phase.Assign;

        _logger.Information(
            "World population plan for zone {ZoneId}: {Surfaces} walkable surfaces became {Cells} cells " +
            "({Wilderness} wilderness, {Settlement} settlement, {Melding} melding, {Refused} refused by chunk rules, " +
            "{Covered} under cover{Fallback})",
            _zoneId,
            ScannedSurfaces,
            _cells.Count,
            _wildernessCells.Count,
            _settlementCells.Count,
            _meldingCells.Count,
            RefusedChunkCells,
            RefusedCoveredCells,
            UsedAnchorFallback ? ", built from authored anchors" : string.Empty);
    }

    private void BucketAnchors()
    {
        _anchorBuckets.Clear();

        for (int index = 0; index < _anchors.Count; index++)
        {
            var bucket = BucketIndex(_anchors[index].Position);
            if (!_anchorBuckets.TryGetValue(bucket, out var indices))
            {
                indices = [];
                _anchorBuckets[bucket] = indices;
            }

            indices.Add(index);
        }
    }

    private void Classify(WorldPopulationCell cell)
    {
        float settlementDistance = float.MaxValue;
        float meldingDistance = float.MaxValue;
        float bandDistance = float.MaxValue;
        Vector3 settlementFocus = cell.Center;
        Vector3 meldingFocus = cell.Center;
        uint band = 0;

        foreach (var anchor in NearbyAnchors(cell.Center))
        {
            float distance = AiVectors.HorizontalDistance(anchor.Position, cell.Center);

            if (distance <= AnchorRadius(anchor))
            {
                if (anchor.Habitat == WorldPopulationHabitat.Melding)
                {
                    if (distance < meldingDistance)
                    {
                        meldingDistance = distance;
                        meldingFocus = anchor.Position;
                    }
                }
                else if (distance < settlementDistance)
                {
                    settlementDistance = distance;
                    settlementFocus = anchor.Position;
                }
            }

            // The level of an area is the band of the outpost nearest to it, however far away that
            // outpost is: Coral Forest's own bands run 1-5 at its starter outpost and 29-30 at its
            // far corners while the zone band says 1-30, so the nearest outpost is what gives the
            // zone its level gradient.
            if (anchor.LevelBandId != 0 && distance < bandDistance)
            {
                bandDistance = distance;
                band = anchor.LevelBandId;
            }
        }

        // A settlement wins over the Melding around it: an outpost's camp inside a Melding
        // perimeter is still a place players respawn in, and its inhabitants are not Melding
        // creatures. The field around that camp is wilderness even when it sits inside the
        // outpost's capture circle.
        if (settlementDistance < float.MaxValue)
        {
            cell.Habitat = WorldPopulationHabitat.Settlement;
            cell.BaseFacing = FacingTowards(cell, settlementFocus);
        }
        else if (meldingDistance < float.MaxValue)
        {
            cell.Habitat = WorldPopulationHabitat.Melding;
            cell.BaseFacing = FacingTowards(cell, meldingFocus);
        }
        else
        {
            cell.Habitat = WorldPopulationHabitat.Wilderness;
            cell.BaseFacing = DeterministicFacing(cell.Key);
        }

        cell.Level = _data.ResolveLevel(_zoneId, band);
    }

    private Vector3 FacingTowards(WorldPopulationCell cell, Vector3 focus)
    {
        var delta = focus - cell.Center;
        delta.Z = 0f;
        return delta.LengthSquared() > 0.0001f ? Vector3.Normalize(delta) : DeterministicFacing(cell.Key);
    }

    private static Vector3 DeterministicFacing(long cellKey)
    {
        float yaw = WorldPopulationHash.Unit(WorldPopulationHash.Mix(cellKey, FacingSalt)) * (2f * MathF.PI);
        return new Vector3(MathF.Cos(yaw), MathF.Sin(yaw), 0f);
    }

    private float AnchorRadius(WorldPopulationAnchor anchor)
    {
        if (anchor.Radius <= 0f)
        {
            return anchor.Habitat == WorldPopulationHabitat.Melding
                ? _rules.MeldingInfluenceRadius
                : _rules.DeployableInfluenceRadius;
        }

        // Outposts carry their capture/control radius (150-550 m in Coral Forest). That
        // circle is the area the outpost owns on the map, not a wildlife-exclusion zone:
        // treating it as settlement habitat paints most of the zone as civilian ground
        // (~69% of Coral Forest) and leaves a player standing at any outpost with no
        // field enemies in their activation radius. The inhabited camp is the configured
        // core, or the authored radius when it is smaller; 0 disables the cap.
        if (anchor.Habitat == WorldPopulationHabitat.Settlement &&
            _rules.OutpostSettlementRadius > 0f &&
            _rules.OutpostSettlementRadius < anchor.Radius)
        {
            return _rules.OutpostSettlementRadius;
        }

        return anchor.Radius;
    }

    private IEnumerable<WorldPopulationAnchor> NearbyAnchors(Vector3 position)
    {
        var (bucketX, bucketY) = BucketIndex(position);

        for (int x = bucketX - 1; x <= bucketX + 1; x++)
        {
            for (int y = bucketY - 1; y <= bucketY + 1; y++)
            {
                if (!_anchorBuckets.TryGetValue((x, y), out var indices))
                {
                    continue;
                }

                foreach (int index in indices)
                {
                    yield return _anchors[index];
                }
            }
        }
    }

    private static (int X, int Y) BucketIndex(Vector3 position) => (
        (int)MathF.Floor(position.X / AnchorBucketSize),
        (int)MathF.Floor(position.Y / AnchorBucketSize));

    private void BuildCandidatePools()
    {
        foreach (var candidate in _roster)
        {
            // Density in the open field is the rows that are field content and nothing else:
            // animals, wanderers, minibosses. Chosen (Melding|Wilderness) still get their one
            // coverage slot at the Melding, and only spill into the field when that ground is
            // full; putting them in the wilderness density pool is how a player standing at an
            // outpost sees troopers instead of fauna.
            if (candidate.Habitat == WorldPopulationHabitat.Wilderness)
            {
                AddWeighted(_wildernessPool, candidate);
            }

            if (candidate.Habitat.Accepts(WorldPopulationHabitat.Settlement))
            {
                AddWeighted(_settlementPool, candidate);
            }

            if (candidate.Habitat.Accepts(WorldPopulationHabitat.Melding))
            {
                AddWeighted(_meldingPool, candidate);
            }
        }
    }

    private static void AddWeighted(List<WorldPopulationCandidate> pool, WorldPopulationCandidate candidate)
    {
        int weight = Math.Clamp(candidate.Weight, 1, 16);
        for (int i = 0; i < weight; i++)
        {
            pool.Add(candidate);
        }
    }

    private void AssignSlots()
    {
        PlaceCoverage();
        PlaceDensity();

        _phase = Phase.Complete;

        _logger.Information(
            "World population plan for zone {ZoneId}: {Slots} slots over {Cells} cells, {Placed} of {Roster} monster rows placed" +
            "{Unplaced}",
            _zoneId,
            SlotCount,
            CellCount,
            PlacedRosterCount,
            RosterCount,
            UnplacedRosterCount > 0
                ? $", {UnplacedRosterCount} rows have no ground of their kind in this zone"
                : string.Empty);
    }

    /// <summary>
    ///     Gives every admitted monster row one slot in a cell of a habitat it fits, so the zone's
    ///     plan carries the whole roster the zone can host. The budget is not enforced here - a row
    ///     whose <c>difficulty_cost</c> is larger than a whole cell's budget still gets its one slot,
    ///     and the cell simply has no room for anything else afterwards - but the per cell count is,
    ///     so a slot always has ground to stand on.
    /// </summary>
    private void PlaceCoverage()
    {
        for (int index = 0; index < _roster.Count; index++)
        {
            if (SlotCount >= _maxPlannedSlots)
            {
                // The plan has hit its ceiling, so the rest of the roster does not fit in it. Said
                // out loud rather than quietly dropped: the status reports these rows.
                UnplacedRosterCount += _roster.Count - index;
                return;
            }

            var candidate = _roster[index];

            // Most specific habitat first: the Melding's own creatures go to the Melding when the
            // zone has one, and only spill into the field when it does not (or when the Melding's
            // cells are full).
            if (TryPlaceCoverage(candidate, WorldPopulationHabitat.Melding) ||
                TryPlaceCoverage(candidate, WorldPopulationHabitat.Settlement) ||
                TryPlaceCoverage(candidate, WorldPopulationHabitat.Wilderness))
            {
                PlacedRosterCount++;
            }
            else
            {
                UnplacedRosterCount++;
            }
        }
    }

    private bool TryPlaceCoverage(WorldPopulationCandidate candidate, WorldPopulationHabitat habitat)
    {
        if (!candidate.Habitat.Accepts(habitat))
        {
            return false;
        }

        var pool = CellsFor(habitat);
        if (pool.Count == 0)
        {
            return false;
        }

        int cursor = _coverageCursors.GetValueOrDefault(habitat);
        int probes = Math.Min(pool.Count, CoverageProbeLimit);

        for (int probe = 0; probe < probes; probe++)
        {
            var cell = pool[cursor];
            cursor = cursor + 1 < pool.Count ? cursor + 1 : 0;

            if (cell.Slots.Count >= _maxNpcsPerCell)
            {
                continue;
            }

            _coverageCursors[habitat] = cursor;
            AddSlot(cell, candidate);
            return true;
        }

        _coverageCursors[habitat] = cursor;
        return false;
    }

    /// <summary>
    ///     Fills the room the coverage pass left, up to the plan's slot ceiling: each cell takes rows
    ///     of its own habitat with the frequency their <c>difficulty_cost</c> implies, until the cell
    ///     is full of NPCs or full of difficulty.
    /// </summary>
    private void PlaceDensity()
    {
        foreach (long key in _keysInDensityOrder)
        {
            if (SlotCount >= _maxPlannedSlots)
            {
                return;
            }

            var cell = _cells[key];
            var pool = CandidatesFor(cell.Habitat);
            if (pool.Count == 0)
            {
                continue;
            }

            while (SlotCount < _maxPlannedSlots && cell.Slots.Count < _maxNpcsPerCell)
            {
                uint pick = WorldPopulationHash.Mix(key, (cell.Slots.Count * 31) + DensityPickSalt);
                var candidate = pool[(int)(pick % (uint)pool.Count)];

                if (cell.SpentDifficulty + DifficultyOf(candidate) > _rules.MaxDifficultyPerCell)
                {
                    break;
                }

                AddSlot(cell, candidate);
            }
        }
    }

    private void AddSlot(WorldPopulationCell cell, WorldPopulationCandidate candidate)
    {
        int index = cell.Slots.Count;

        cell.Slots.Add(new WorldPopulationSlot(
            cell,
            candidate,
            cell.Center + Jitter(cell.Key, index),
            Turn(cell.BaseFacing, cell.Key, index),
            index));
        cell.SpentDifficulty += DifficultyOf(candidate);
        SlotCount++;
    }

    private int DifficultyOf(WorldPopulationCandidate candidate) => candidate.DifficultyCost > 0
        ? (candidate.DifficultyCost > int.MaxValue ? int.MaxValue : (int)candidate.DifficultyCost)
        : _rules.UnbudgetedDifficultyCost;

    private Vector3 Jitter(long cellKey, int slotIndex)
    {
        float angle = WorldPopulationHash.Unit(WorldPopulationHash.Mix(cellKey, slotIndex + JitterSalt)) * (2f * MathF.PI);

        // Square root of the second draw, so the spots spread evenly over the disc instead of
        // bunching at its centre.
        float distance = MathF.Sqrt(WorldPopulationHash.Unit(WorldPopulationHash.Mix(cellKey, ~slotIndex)))
                         * (_cellSize * JitterFraction);

        return new Vector3(MathF.Cos(angle) * distance, MathF.Sin(angle) * distance, 0f);
    }

    private static Vector3 Turn(Vector3 baseFacing, long cellKey, int slotIndex)
    {
        float angle = (WorldPopulationHash.Unit(WorldPopulationHash.Mix(cellKey, slotIndex ^ FacingSpreadSalt)) * 2f) - 1f;
        angle *= FacingSpread;

        float cos = MathF.Cos(angle);
        float sin = MathF.Sin(angle);
        var turned = new Vector3(
            (cos * baseFacing.X) - (sin * baseFacing.Y),
            (sin * baseFacing.X) + (cos * baseFacing.Y),
            0f);

        return turned.LengthSquared() > 0.0001f ? Vector3.Normalize(turned) : Vector3.UnitY;
    }

    private List<WorldPopulationCell> CellsFor(WorldPopulationHabitat habitat) => habitat switch
    {
        WorldPopulationHabitat.Settlement => _settlementCells,
        WorldPopulationHabitat.Melding => _meldingCells,
        _ => _wildernessCells,
    };

    private List<WorldPopulationCandidate> CandidatesFor(WorldPopulationHabitat habitat) => habitat switch
    {
        WorldPopulationHabitat.Settlement => _settlementPool,
        WorldPopulationHabitat.Melding => _meldingPool,
        _ => _wildernessPool,
    };

    /// <summary>Running totals of one cell while the ground is being scanned into it.</summary>
    private sealed class CellDraft
    {
        public Vector3 Sum;
        public int Count;
        public int X;
        public int Y;
    }
}
