#nullable enable
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Numerics;
using System.Runtime.CompilerServices;
using System.Threading;
using BepuPhysics;
using BepuPhysics.Collidables;
using BepuPhysics.Trees;
using BepuUtilities;
using BepuUtilities.Memory;
using DebugPipeProto;
using GameServer.Entities;
using GameServer.Entities.Character;
using GameServer.StaticDB;
using GameServer.Systems.Combat;
using GameServer.Systems.ProjectileSim;
using GameServer.Systems.SystemEvents;
using Serilog;
using Shared.Collision;
using Shared.Collision.Layers;
using Shared.Collision.Navigation;
using Shared.Collision.ZoneLoading;

namespace GameServer.Physics;

/// <summary>
///    Runs physics simulations (primarily hit detection)
/// </summary>
public partial class PhysicsEngine
{
    public const float TargetTimestepDuration = 50; // (1/20f)
    public const float TargetDebugTickDuration = 200;

    /// <summary>
    ///     How many fixed timesteps one <see cref="Tick" /> may run at most. The accumulator grows
    ///     freely while the shard thread is busy (zone loading, a long GC pause, a slow tick), and
    ///     with no cap the tick after such a stall tried to run every missed 50 ms step back to
    ///     back — hundreds of timesteps when a load blocks for seconds — stretching the tick
    ///     further and feeding the next backlog: a self-sustaining spiral of death on exactly the
    ///     machines (server and game client sharing cores) where the stalls happen. Dropping time
    ///     beyond the cap slows the simulation instead, which is what every other gate-driven
    ///     subsystem of the shard already does under load.
    /// </summary>
    private const int MaxCatchUpStepsPerTick = 8;

    /// <summary>
    ///     Metres of slack the <see cref="IsStandingVolumeClear" /> probes leave around a body, so a
    ///     mob is not placed with its surface exactly touching a wall it then has to path out of.
    /// </summary>
    private const float ClearanceMargin = 0.1f;

    /// <summary>
    ///     Metres of headroom <see cref="IsStandingVolumeClear" /> requires above a body's height.
    /// </summary>
    private const float HeadroomMargin = 0.2f;

    /// <summary>
    ///     How far above a body's head <see cref="HasOverheadCover" /> reaches. Every cave,
    ///     tunnel, underpass and roofed space a mob could be buried in hangs lower than this,
    ///     while a zone's tree lines and the tops of its natural arches do not.
    /// </summary>
    private const float DefaultSkyProbeHeight = 12f;

    /// <summary>
    ///     How far below a query <see cref="TryGetGroundSurface" />'s winding fallback reaches when
    ///     the downward probe sees nothing. The fallback answers "the surface the feet rest on" for
    ///     a triangle wound away from the sky, and this is the one piece of that query the caller's
    ///     own window cannot bound: a spawn snap with a ten kilometre window would otherwise settle
    ///     on whatever floor sits at the bottom of it. Five metres is deeper than any step, spawn
    ///     jitter or body height the callers probe for.
    /// </summary>
    private const float GroundBackfaceProbeReach = 5f;

    /// <summary>
    ///     How many stacked surfaces <see cref="TryGetGroundSurface" />'s winding fallback walks
    ///     upward before it settles on the highest one found. One is the usual answer (the surface
    ///     under the query); the rest cover a floor stacked under another floor inside the reach.
    /// </summary>
    private const int GroundBackfaceProbes = 4;

    /// <summary>
    ///     Gap left between the winding fallback's re-casts so the surface it just hit is not hit
    ///     again by the next probe up.
    /// </summary>
    private const float GroundBackfaceProbeEpsilon = 0.001f;

    /// <summary>
    ///     How far past the query's own height the winding fallback's segment ends. A segment ray
    ///     cast reports a hit only strictly inside the segment (<c>t &lt; distance</c>), and the
    ///     surface a standing body rests on is exactly at the query's height - which is the case this
    ///     fallback exists for. Ending dead on the query would walk past the very surface it is for;
    ///     a millimetre of overshoot is not a reach, and it cannot pick up anything above the feet.
    /// </summary>
    private const float GroundBackfaceProbeOvershoot = 0.001f;

    /// <summary>
    ///     How many of a zone's walkable navigation faces the load-time winding report samples. A
    ///     few hundred is enough to tell a zone whose ground the downward ray can see from one whose
    ///     ground it cannot, and it costs that many ray casts once, at load.
    /// </summary>
    private const int GroundVisibilitySamples = 256;

    /// <summary>
    ///     How far above and below a sampled face's own height the load-time report's probes reach.
    ///     Wide enough to span the jitter of a face centroid and the mesh's own spacing.
    /// </summary>
    private const float GroundVisibilityProbeReach = 2f;

    /// <summary>
    ///     How close to the sampled face's own height a probe's hit has to land to count as that
    ///     face's ground, rather than as some other surface (a roof, a deck, a floor beneath it)
    ///     that happens to sit inside the probe's window.
    /// </summary>
    private const float GroundVisibilityTolerance = 1f;

    /// <summary>
    ///     Fractions of the body height the clearance probes are fired at: ankle, waist, shoulder.
    ///     Three heights catch both a low crate the body would stand inside and a barrier it would
    ///     poke its head through.
    /// </summary>
    private static readonly float[] ClearanceProbeHeightFractions = [0.15f, 0.5f, 0.85f];

    /// <summary>The horizontal directions the clearance probes are fired in.</summary>
    private static readonly Vector3[] ClearanceProbeDirections =
        [Vector3.UnitX, -Vector3.UnitX, Vector3.UnitY, -Vector3.UnitY];

    private readonly ILogger _logger;
    private readonly EventBus _eventBus;
    private readonly ZoneLoader _zoneLoader;
    private readonly RigidBodyLoader _rigidBodyLoader;
    private readonly Dictionary<BodyHandle, ulong> _bodyToEntityId = [];
    private readonly Dictionary<ulong, BodyHandle> _entityIdToBody = [];
    private readonly Dictionary<ulong, AssetCompoundKey> _entityIdToAssetKey = [];

    /// <summary>
    ///     Entities that already got their pose-shape warning at creation. The shape resolution runs
    ///     again on every movement update (and every ai tick for NPCs), and the "no collision data"
    ///     state is permanent for the entity — warning per update turned one bad database row into a
    ///     warning per packet from its victims. Lives as long as the body does; pruned in RemoveEntity.
    /// </summary>
    private readonly HashSet<ulong> _poseShapeWarningsIssued = [];
    private readonly string _mapsPath = string.Empty;
    private readonly string _cachePath = string.Empty;
    private readonly bool _forceReload;
    private readonly bool _isDebugPipeClient;

    private TypedIndex _fallbackShape;
    private NavigationMesh? _navigationMesh;
    private int _debugEntityIndex = -1;
    private double _debugTimeAccumulator;

    public PhysicsEngine(EventBus eventBus, uint zoneId, string mapsPath = "", string assetDBPath = "", bool loadMapsCollision = false, DebugProjectileHitCallbacks? debugProjectileHitCallbacks = null, bool isDebugPipeClient = false, string cachePath = "", bool forceReload = false)
    {
        _eventBus = eventBus;
        _logger = Log.Logger.ForContext<PhysicsEngine>();
        _mapsPath = mapsPath;
        _cachePath = cachePath;
        _forceReload = forceReload;
        DebugProjectileHitCallbacks = debugProjectileHitCallbacks;

        // The 20 Hz timestep of a zone shard does not have enough work to keep a large dispatcher
        // fleet busy: every Bepu dispatch thread is a spinning worker that competes with the game
        // client for a core, and the typical setup runs server and client on the same machine. This
        // simulation profile (mostly static zone geometry plus a few hundred kinematic bodies) is
        // comfortably served by a handful of threads, so cap the fleet.
        const int MaxDispatcherThreads = 4;
        var targetThreadCount = int.Clamp(
            Environment.ProcessorCount > 4 ? Environment.ProcessorCount - 2 : Environment.ProcessorCount - 1,
            1,
            MaxDispatcherThreads);

        BufferPool = new BufferPool();
        ThreadDispatcher = new ThreadDispatcher(targetThreadCount);
        Simulation = Simulation.Create(BufferPool, new NarrowPhaseCallbacks(), new PoseIntegratorCallbacks(new Vector3(0, 0, -8)), new SolveDescription(8, 1));

        _fallbackShape = Simulation.Shapes.Add(new Sphere(0.9f));

        _zoneLoader = new ZoneLoader(
            Simulation,
            BufferPool,
            ThreadDispatcher,
            mapsPath,
            cachePath,
            chunkId => SDBInterface.GetChunkRecord(chunkId)?.ExcludeFromPathing);
        _rigidBodyLoader = new RigidBodyLoader(Simulation, BufferPool, ThreadDispatcher, assetDBPath, cachePath);
        PoseLoader = new PoseLoader.PoseLoader(assetDBPath);

        _isDebugPipeClient = isDebugPipeClient;
        DebugInitialize(isDebugPipeClient, zoneId);

        if (loadMapsCollision)
        {
            LoadZone(zoneId);
        }
        else
        {
            // Said out loud rather than implied: a shard configured without collision has no ground
            // to snap spawned mobs to and no navigation mesh for world population to plan on, which
            // is exactly the state an operator chasing "the world looks empty" needs to rule in or
            // out from the first log lines.
            _logger.Information(
                "Zone {ZoneId}: collision loading is disabled (LoadMapsCollision is false), so ground snapping and collision-derived world population placement are unavailable",
                zoneId);
        }
    }

    public long? ZoneFileTimestamp { get; private set; }

    public Simulation Simulation { get; protected set; }
    public BufferPool BufferPool { get; private set; }
    public ThreadDispatcher ThreadDispatcher { get; private set; }
    public double TimeAccumulator { get; protected set; }
    public PoseLoader.PoseLoader PoseLoader { get; private set; }
    private DebugProjectileHitCallbacks? DebugProjectileHitCallbacks { get; set; }

    public void LoadZone(uint zoneId)
    {
        var ts = _zoneLoader.LoadZone(zoneId, _forceReload);
        if (ts.HasValue)
        {
            ZoneFileTimestamp = ts.Value;

            // Said before it starts, because the bake is the one step of loading a zone that is pure
            // CPU over everything the zone has - every walkable collision triangle of all its chunks,
            // which is millions of them on a prod zone - and it runs on the thread that decides when
            // the server stops refusing clients: the GameServer calls itself ready only once the shard
            // (and so this constructor) has returned. A console that stops after "Loaded successfully"
            // and says nothing else looks exactly like a server that is still loading chunks, and the
            // operator reading it is waiting for the wrong thing.
            var bakeStarted = Stopwatch.StartNew();
            _logger.Information(
                "Zone {ZoneId}: baking the navigation mesh from {TriangleCount} collision triangles",
                zoneId,
                _zoneLoader.NavigationTriangles.Count);

            _navigationMesh = _zoneLoader.NavigationTriangles.Count > 0
                ? new NavigationMesh(
                    _zoneLoader.NavigationTriangles,
                    materialId => SDBInterface.GetPhysicsMaterial(materialId)?.AIPathingCost ?? 1f,
                    _zoneLoader.IsNavigationExcluded,
                    materialExcluded: IsUnderwaterMaterial)
                : null;
            _logger.Information(
                "Zone {ZoneId}: navigation mesh has {TriangleCount} source triangles and {FaceCount} walkable faces after {Elapsed}",
                zoneId,
                _zoneLoader.NavigationTriangles.Count,
                _navigationMesh?.FaceCount ?? 0,
                bakeStarted.Elapsed);

            LogGroundVisibility(zoneId);
        }
        else
        {
            _navigationMesh = null;

            // The zone loader already logged why (a missing {zoneId}.zone file or an invalid root
            // layer), but that message does not say what it means for this shard. Spell it out here
            // once so an operator who switched ZoneId can see immediately what the server is (and is
            // not) doing: no collision, no ground snapping, and world population degrades to the
            // zone's authored positions.
            _logger.Warning(
                "Zone {ZoneId} could not be loaded (expected {ZoneFile} in the maps folder). This shard runs without zone collision: ground snapping is unavailable and world population falls back to the zone's authored positions",
                zoneId,
                Path.Combine(_mapsPath, $"{zoneId}.zone"));
        }
    }

    /// <summary>Whether the loaded zone supplied original exclusions and walkable collision surfaces.</summary>
    public bool HasNavigationExclusions => _zoneLoader.HasNavigationExclusions;

    /// <summary>Tests the original zone chunk metadata's excluded-from-pathing regions.</summary>
    public bool IsNavigationExcluded(Vector3 point) => _zoneLoader.IsNavigationExcluded(point);

    /// <summary>Whether a collision-derived, material-weighted navigation mesh is available.</summary>
    public bool HasNavigationMesh => _navigationMesh != null && _navigationMesh.FaceCount > 0;

    /// <summary>
    ///     Whether the zone's static geometry is loaded, i.e. whether a ground probe can hit
    ///     anything at all. Distinct from <see cref="HasNavigationMesh"/>: a zone can have collision
    ///     without a single walkable face in it.
    /// </summary>
    public bool HasZoneCollision => Simulation.Statics.Count > 0;

    /// <summary>How many walkable faces the loaded zone's navigation mesh has; 0 when there is none.</summary>
    public int WalkableFaceCount => _navigationMesh?.FaceCount ?? 0;

    /// <summary>
    ///     The centroid of one walkable face of the loaded navigation mesh: a spot the zone's own
    ///     baked collision considers standable (walkable slope, not excluded from pathing by the
    ///     chunk metadata). Enumerating them gives the set of every place an NPC could be put,
    ///     which is what world population plans against. Returns false when the index is outside
    ///     the mesh or no mesh is loaded.
    /// </summary>
    public bool TryGetWalkableFaceCentroid(int faceIndex, out Vector3 centroid)
    {
        centroid = default;
        return _navigationMesh != null && _navigationMesh.TryGetFaceCentroid(faceIndex, out centroid);
    }

    /// <summary>
    ///     The chunks the loaded zone was built from, with each chunk's world origin and its
    ///     <c>dbzonemetadata::ChunkRecord</c> id. Empty when no zone collision is loaded. World
    ///     population uses it to ask the database whether a chunk is one the server simulates at
    ///     all before placing an NPC in it.
    /// </summary>
    public IReadOnlyList<ZoneChunkRef> ZoneChunks => _zoneLoader.ChunkRefs;

    /// <summary>Zone bounds from ZoneBoundsLayer (0x21000) if present, from actual client map file.</summary>
    public Vector3? ZoneBoundsMin => _zoneLoader.ZoneBoundsMin;
    public Vector3? ZoneBoundsMax => _zoneLoader.ZoneBoundsMax;

    /// <summary>Authored path layers (0x20800) - vehicle/dropship routes, NOT NPC patrols (see MAP_FILES_FINDINGS.md).</summary>
    public IReadOnlyList<ZonePathLayer> ZonePaths => _zoneLoader.ZonePaths;

    /// <summary>Melding perimeter layers from zone file.</summary>
    public IReadOnlyList<MeldingPerimeterLayer> MeldingPerimeters => _zoneLoader.MeldingPerimeters;

    public int SubZoneRegionCount => _zoneLoader.SubZoneRegionCount;
    public int EncounterNameCount => _zoneLoader.EncounterNameCount;

    /// <summary>Whether position is inside zone bounds, or true when no bounds are known.</summary>
    public bool IsInsideZoneBounds(Vector3 pos) => _zoneLoader.IsInsideZoneBounds(pos);

    /// <summary>Queries the loaded collision-derived navigation mesh, if one is available.</summary>
    public IReadOnlyList<Vector3>? FindNavigationPath(
        Vector3 start,
        Vector3 goal,
        Func<Vector3, Vector3, bool> blocked,
        float maxStepHeight,
        float maxSearchDistance = 128f)
    {
        return _navigationMesh?.FindPath(start, goal, blocked, maxStepHeight, maxSearchDistance);
    }

    public StaticDescription[] LoadRigidBody(string assetId)
    {
        return _rigidBodyLoader.Load(assetId);
    }

    public void Tick(double deltaTime, ulong currentTime, CancellationToken ct)
    {
        TimeAccumulator += deltaTime;

        var steps = 0;
        while (!ct.IsCancellationRequested && TimeAccumulator >= TargetTimestepDuration && steps < MaxCatchUpStepsPerTick)
        {
            DebugProcessMessages();
            Simulation.Timestep(TargetTimestepDuration, ThreadDispatcher);
            TimeAccumulator -= TargetTimestepDuration;
            steps++;
        }

        if (TimeAccumulator >= TargetTimestepDuration)
        {
            // The cap was hit while the simulation was still behind: keep a sub-step worth of
            // carry-over and retire the rest, or the backlog would drive the catch-up loop again
            // before the next tick even lands.
            _logger.Debug("Physics dropped {DroppedMs:F0} ms of accumulated simulation time after a stall", TimeAccumulator - TargetTimestepDuration);
            TimeAccumulator = TargetTimestepDuration * 0.99f;
        }

        if (!ct.IsCancellationRequested && !_isDebugPipeClient)
        {
            _debugTimeAccumulator += deltaTime;
            if (_debugTimeAccumulator >= TargetDebugTickDuration)
            {
                DebugSendTickUpdate();
                _debugTimeAccumulator = 0;
            }
        }
    }

    public BodyHandle CreateKineticEntity(CharacterEntity entity)
    {
        _logger.Debug("CreateKineticEntity Character {entityId}", entity.EntityId);
        var pose = new RigidPose { Position = entity.Position, Orientation = Quaternion.Inverse(entity.Orientation) };
        AssetCompoundKey key = GetCharacterPoseAsset(entity);
        var shape = GetAssetShape(key);
        var body = Simulation.Bodies.Add(BodyDescription.CreateKinematic(pose, shape, -1));
        _bodyToEntityId[body] = entity.EntityId;
        _entityIdToBody[entity.EntityId] = body;
        _entityIdToAssetKey[entity.EntityId] = key;

        _ = DebugPipe?.SendAsync(new PipeMessage
        {
            CreateKineticEntity = new CreateKineticEntity
            {
                EntityId = entity.EntityId,
                Pose = pose.ToProto(),
                Shape = new PipeCollisionShape
                {
                    AssetId = key.AssetId,
                    Offset = key.Offset.ToProto(),
                    Scale = key.Scale,
                },
            }
        });

        return body;
    }

    public BodyHandle CreateKineticEntity(BaseEntity entity)
    {
        _logger.Debug("CreateKineticEntity Base {entityId}", entity.EntityId);
        var offset = Vector3.Zero;

        // Spawn paths assign Collision before calling; a missing component (broken/missing
        // row) degrades to the fallback shape instead of throwing out of the spawn path.
        var scale = entity.Collision?.Scale ?? 1f;
        var assetId = entity.Collision?.HitboxCollisionId ?? 0;
        if (entity.Collision == null)
        {
            _logger.Warning("CreateKineticEntity: entity {entityId} has no collision component, using fallback shape", entity.EntityId);
        }

        var pose = new RigidPose { Position = entity.Position, Orientation = Quaternion.Inverse(entity.Orientation) };
        var key = new AssetCompoundKey(assetId, offset, scale);
        var shape = GetAssetShape(key);
        var body = Simulation.Bodies.Add(BodyDescription.CreateKinematic(pose, shape, -1));
        _bodyToEntityId[body] = entity.EntityId;
        _entityIdToBody[entity.EntityId] = body;
        _entityIdToAssetKey[entity.EntityId] = key;

        _ = DebugPipe?.SendAsync(new PipeMessage
        {
            CreateKineticEntity = new CreateKineticEntity
            {
                EntityId = entity.EntityId,
                Pose = pose.ToProto(),
                Shape = new PipeCollisionShape
                {
                    AssetId = assetId,
                    Offset = offset.ToProto(),
                    Scale = scale,
                }
            }
        });

        return body;
    }

    public void UpdateEntity(CharacterEntity entity)
    {
        if (!_entityIdToBody.TryGetValue(entity.EntityId, out var bodyHandle))
        {
            return;
        }

        var body = Simulation.Bodies[bodyHandle];
        ref var currentPose = ref body.Pose;
        var currentShape = body.Collidable.Shape;
        AssetCompoundKey key = GetCharacterPoseAsset(entity);
        var shape = GetAssetShape(key);

        var orientation = Quaternion.Inverse(entity.Orientation);
        if (currentPose.Position != entity.Position || currentPose.Orientation != orientation || currentShape != shape)
        {
            _entityIdToAssetKey[entity.EntityId] = key;
            body.Awake = true;
            body.SetShape(shape);
            currentPose.Position = entity.Position;
            currentPose.Orientation = orientation;
        }
    }

    public void UpdateEntity(BaseEntity entity)
    {
        if (!_entityIdToBody.TryGetValue(entity.EntityId, out var bodyHandle))
        {
            return;
        }

        ref var currentPose = ref Simulation.Bodies[bodyHandle].Pose;

        var orientation = Quaternion.Inverse(entity.Orientation);
        if (currentPose.Position != entity.Position || currentPose.Orientation != orientation)
        {
            var body = Simulation.Bodies[bodyHandle];
            body.Awake = true;
            currentPose.Position = entity.Position;
            currentPose.Orientation = orientation;
        }
    }

    public bool HasEntity(IEntity entity)
    {
        return _entityIdToBody.ContainsKey(entity.EntityId);
    }

    public void RemoveEntity(IEntity entity)
    {
        if (!_entityIdToBody.TryGetValue(entity.EntityId, out var bodyHandle))
        {
            _logger.Warning("RemoveEntity was called for {entity} but there is no body!", entity.ToString());
            return;
        }

        _ = _poseShapeWarningsIssued.Remove(entity.EntityId);
        _entityIdToAssetKey.Remove(entity.EntityId);
        _entityIdToBody.Remove(entity.EntityId);
        _bodyToEntityId.Remove(bodyHandle);
        Simulation.Bodies.Remove(bodyHandle);

        _ = DebugPipe?.SendAsync(new PipeMessage
        {
            RemoveEntity = new RemoveEntity
            {
                EntityId = entity.EntityId,
            }
        });
    }

    public SegmentRaycastHit SegmentRayCast(Vector3 from, Vector3 to, ulong ignoreEntityId, bool staticOnly = false)
    {
        var hitResult = default(SegmentRaycastHit);
        var delta = to - from;
        var distance = delta.Length();

        if (distance < 0.01f)
        {
            return hitResult;
        }

        var direction = delta / distance;

        var hitHandler = default(RayHitHandler);
        hitHandler.T = distance;
        hitHandler.AvoidSourceBody = ignoreEntityId != 0;
        hitHandler.SourceBody = _entityIdToBody.GetValueOrDefault(ignoreEntityId);
        hitHandler.StaticOnly = staticOnly;

        Simulation.RayCast(from, direction, distance, BufferPool, ref hitHandler);

        if (hitHandler.T < distance)
        {
            hitResult.Hit = true;
            hitResult.T = hitHandler.T;
            hitResult.HitPosition = from + (direction * hitHandler.T);
            hitResult.Normal = hitHandler.Normal;
            hitResult.ChildIndex = hitHandler.ChildIndex;
            hitResult.Collidable = hitHandler.HitCollidable;
            hitResult.HitEntityId = _bodyToEntityId.GetValueOrDefault(hitHandler.HitCollidable.BodyHandle);
        }

        return hitResult;
    }

    /// <summary>
    ///     Finds the ground surface under <paramref name="position" /> by casting straight down
    ///     from far above. Returns the surface position (keeping X and Y), or null when nothing
    ///     is hit - typically because no zone collision data is loaded.
    /// </summary>
    /// <remarks>
    ///     Used to place freshly spawned mobs on the terrain. The probe only tests static
    ///     geometry so a player or another mob standing nearby cannot be mistaken for the ground.
    ///     Either face winding is seen - see <see cref="TryGetGroundSurface" />, whose two-way
    ///     probe this is a thin wrapper over.
    /// </remarks>
    public Vector3? FindGround(Vector3 position, ulong ignoreEntityId = 0)
    {
        return TryGetGroundSurface(position, out var ground, out _, 10_000f, 10_000f, ignoreEntityId)
            ? ground
            : null;
    }

    /// <summary>
    ///     Says which way the loaded zone's ground was baked, by asking both halves of the same
    ///     question about a sample of the faces the navigation mesh kept: does the straight downward
    ///     probe see the face's own surface, and does the two-way probe.
    /// </summary>
    /// <remarks>
    ///     The two numbers should agree, and on a zone whose ground is wound towards the sky they do,
    ///     which makes this line a formality. They cannot agree on the rest. The navigation mesh
    ///     decides walkable ground with a cross product of one handedness
    ///     (<see cref="Shared.Collision.Navigation.NavigationTriangle.Normal" />) while the physics
    ///     mesh's ray test uses the other, and both read the bake's vertices in the same order - so a
    ///     face the mesh calls walkable is one a downward ray sees from below (see
    ///     <see cref="TryGetGroundSurface" />). A zone like that is a zone where every probe that
    ///     assumed ground faces up found nothing under a standing body: a mob that exists, shoots, and
    ///     cannot walk, and a spawn snap that dropped bodies onto whatever floor the down ray could
    ///     see. Printed rather than inferred because it is the one number that tells an operator which
    ///     kind of zone they have.
    /// </remarks>
    /// <param name="zoneId">The zone this report is about.</param>
    private void LogGroundVisibility(uint zoneId)
    {
        var mesh = _navigationMesh;
        if (mesh == null || mesh.FaceCount <= 0)
        {
            return;
        }

        int stride = Math.Max(1, mesh.FaceCount / GroundVisibilitySamples);
        List<Vector3> centroids = [];
        for (int face = 0; face < mesh.FaceCount && centroids.Count < GroundVisibilitySamples; face += stride)
        {
            if (mesh.TryGetFaceCentroid(face, out var centroid))
            {
                centroids.Add(centroid);
            }
        }

        var (downward, twoWay) = MeasureGroundVisibility(centroids);
        _logger.Information(
            "Zone {ZoneId}: of {Sampled} sampled walkable faces, the straight downward ground probe sees {SeenDownward} at the face's own height and the two-way probe sees {SeenTwoWay} - the difference is ground the bake wound away from the sky, which only the two-way probe finds",
            zoneId,
            centroids.Count,
            downward,
            twoWay);
    }

    /// <summary>
    ///     The measurement behind <see cref="LogGroundVisibility" />: how many of
    ///     <paramref name="points" /> the straight downward probe finds at their own height, and how
    ///     many of them the two-way probe (see <see cref="TryGetGroundSurface" />) does.
    /// </summary>
    /// <remarks>
    ///     Internal rather than private because the two numbers are exactly the finding this probe
    ///     exists for, and a test can hold a piece of ground of known winding to them without a zone
    ///     file: a sheet wound away from the sky is seen by the second probe alone.
    /// </remarks>
    /// <param name="points">Points a face of the ground could be probed at.</param>
    /// <returns>The count the downward probe found, then the count the two-way probe found.</returns>
    internal (int SeenDownward, int SeenTwoWay) MeasureGroundVisibility(IReadOnlyList<Vector3> points)
    {
        int downward = 0;
        int twoWay = 0;
        foreach (var point in points)
        {
            var from = new Vector3(point.X, point.Y, point.Z + GroundVisibilityProbeReach);
            var to = new Vector3(point.X, point.Y, point.Z - GroundVisibilityProbeReach);
            var hit = SegmentRayCast(from, to, 0, staticOnly: true);
            if (hit.Hit && MathF.Abs(hit.HitPosition.Z - point.Z) <= GroundVisibilityTolerance)
            {
                downward++;
            }

            if (TryGetGroundSurface(point, out var ground, out _, GroundVisibilityProbeReach, GroundVisibilityProbeReach) &&
                MathF.Abs(ground.Z - point.Z) <= GroundVisibilityTolerance)
            {
                twoWay++;
            }
        }

        return (downward, twoWay);
    }

    /// <summary>
    ///     The static ground surface under <paramref name="position" /> and the normal it faces,
    ///     found by the same straight down probe <see cref="FindGround" /> uses. Returns false when
    ///     nothing is hit - typically because no zone collision data is loaded, or because the point
    ///     is over a void.
    /// </summary>
    /// <remarks>
    ///     <para>
    ///         The normal is what tells walkable ground from a cliff face: the zone's navigation mesh is
    ///         baked with a minimum walkable normal Z of 0.35, so a surface steeper than that is one no
    ///         NPC could stand on even though the ray hit something. It may face either way - see below -
    ///         so callers that ask how walkable the surface is have to test the magnitude. Callers that
    ///         only need the height keep using <see cref="FindGround" />.
    ///     </para>
    ///     <para>
    ///         Both windings are probed. BepuPhysics mesh shapes are single-sided - a ray registers a
    ///         triangle only when it strikes the front face the winding names - and the winding is
    ///         whatever the zone file wrote: both loaders build their triangles from the bake's
    ///         vertices in the same order, with nothing normalising either. PIN's navigation mesh
    ///         reads that order with the opposite cross product (see
    ///         <see cref="Shared.Collision.Navigation.NavigationTriangle.Normal" />), so the ground it
    ///         keeps as walkable and the ground this probe's lone downward ray could see are
    ///         complementary sets - and which of them a zone's terrain lands in is the bake's
    ///         business, not the engine's. That is the same mesh-side fact
    ///         <see cref="HasStaticOcclusion" />, <see cref="HasOverheadCover" /> and the navigation wall
    ///         probes already answer for sight lines, cover and walls; the ground probe is the one
    ///         locomotion itself stands on, and it was the last of them that a lone downward ray could
    ///         still be blind to. On a surface wound away from the sky the old probe saw nothing under the
    ///         feet (so an NPC could not take a single step, while it kept firing, and routine and combat
    ///         movement failed the same way) and, when it saw something further down, the spawn snap
    ///         dropped mobs through the surface onto it.
    ///     </para>
    ///     <para>
    ///         The upward fallback starts at most <see cref="GroundBackfaceProbeReach" /> below the query
    ///         and walks up through stacked surfaces, so it never settles on a floor arbitrarily far
    ///         under a plan point. The higher of the two hits wins: the surface the query's own feet rest
    ///         on, not the floor beneath it.
    ///     </para>
    /// </remarks>
    /// <param name="position">The point to look under.</param>
    /// <param name="ground">The surface position, keeping <paramref name="position" />'s X and Y.</param>
    /// <param name="normal">The normal of the surface that was hit, facing either way.</param>
    /// <param name="searchUp">How far above <paramref name="position" /> the probe starts.</param>
    /// <param name="searchDown">How far below <paramref name="position" /> the probe reaches.</param>
    /// <param name="ignoreEntityId">Kinematic body to exclude, or 0. Static geometry is never excluded.</param>
    /// <returns>Whether a surface was hit.</returns>
    public bool TryGetGroundSurface(
        Vector3 position,
        out Vector3 ground,
        out Vector3 normal,
        float searchUp = 10_000f,
        float searchDown = 10_000f,
        ulong ignoreEntityId = 0)
    {
        ground = position;
        normal = default;

        var from = new Vector3(position.X, position.Y, position.Z + searchUp);
        var to = new Vector3(position.X, position.Y, position.Z - searchDown);
        var hit = SegmentRayCast(from, to, ignoreEntityId, staticOnly: true);
        bool found = hit.Hit;
        float foundZ = hit.HitPosition.Z;
        Vector3 foundNormal = hit.Normal;

        // The same surface, looked at from the other side. The nearest upward hit is the lowest
        // surface in the reach, so the probe keeps walking up from just above each hit and keeps the
        // highest one it finds - the surface nearest the query point from below.
        float reach = float.IsFinite(searchDown)
            ? MathF.Min(searchDown, GroundBackfaceProbeReach)
            : GroundBackfaceProbeReach;
        var upFrom = new Vector3(position.X, position.Y, position.Z - reach);
        var upTo = new Vector3(position.X, position.Y, position.Z + GroundBackfaceProbeOvershoot);
        for (int probe = 0; probe < GroundBackfaceProbes && upFrom.Z < upTo.Z; probe++)
        {
            var back = SegmentRayCast(upFrom, upTo, ignoreEntityId, staticOnly: true);
            if (!back.Hit)
            {
                break;
            }

            if (!found || back.HitPosition.Z > foundZ)
            {
                found = true;
                foundZ = back.HitPosition.Z;
                foundNormal = back.Normal;
            }

            upFrom = new Vector3(upFrom.X, upFrom.Y, back.HitPosition.Z + GroundBackfaceProbeEpsilon);
        }

        if (!found)
        {
            return false;
        }

        ground = new Vector3(position.X, position.Y, foundZ);
        normal = foundNormal;
        return true;
    }

    /// <summary>
    ///     Whether an upright body of the given size can occupy the volume whose bottom centre is
    ///     <paramref name="feet" /> without ending up inside the world or inside another entity:
    ///     horizontal static probes at ankle, waist and shoulder height along both axes, one
    ///     vertical probe for headroom, and a broad phase query for the non-static bodies (players,
    ///     mobs, vehicles, deployables) that already overlap the box.
    /// </summary>
    /// <remarks>
    ///     This is the physical half of spawn placement validation. The other half - two NPCs
    ///     planned into the same patch of ground that neither has occupied yet - is bookkeeping the
    ///     spawning system does itself, because no ray can see a body that is not there yet. Both
    ///     are needed, and neither is sufficient alone.
    /// </remarks>
    /// <param name="feet">Bottom centre of the volume, on the ground surface.</param>
    /// <param name="radius">Body radius in metres.</param>
    /// <param name="height">Body height in metres.</param>
    /// <param name="ignoreEntityId">Entity whose own body does not count as an obstruction.</param>
    /// <returns>True when the volume is free.</returns>
    public bool IsStandingVolumeClear(Vector3 feet, float radius, float height, ulong ignoreEntityId = 0)
    {
        if (radius <= 0f || height <= 0f || !float.IsFinite(radius) || !float.IsFinite(height))
        {
            return false;
        }

        if (!float.IsFinite(feet.X) || !float.IsFinite(feet.Y) || !float.IsFinite(feet.Z))
        {
            return false;
        }

        float reach = radius + ClearanceMargin;
        foreach (float heightFraction in ClearanceProbeHeightFractions)
        {
            var origin = new Vector3(feet.X, feet.Y, feet.Z + (height * heightFraction));
            foreach (var direction in ClearanceProbeDirections)
            {
                // Bidirectional probe for the same single-sided-mesh reason the sky probe
                // casts both ways: a wall whose triangles face away from a lone ray lets
                // the ray sail through and would let a mob place inside the wall.
                if (HasStaticOcclusion(origin, origin + (direction * reach), ignoreEntityId))
                {
                    return false;
                }
            }
        }

        // Headroom: a body under a low overhang or a ledge is a body stuck in the world. Started
        // just above the feet so a body that sank a hair into the surface does not report itself.
        // Bidirectional (HasStaticOcclusion) so a ledge whose triangles face up still blocks the
        // upward probe.
        var headFrom = new Vector3(feet.X, feet.Y, feet.Z + 0.05f);
        var headTo = new Vector3(feet.X, feet.Y, feet.Z + height + HeadroomMargin);
        if (HasStaticOcclusion(headFrom, headTo, ignoreEntityId))
        {
            return false;
        }

        // Everything that is not the world's own geometry: players, mobs, vehicles, deployables.
        var min = new Vector3(feet.X - radius, feet.Y - radius, feet.Z);
        var max = new Vector3(feet.X + radius, feet.Y + radius, feet.Z + height);
        var overlapEnumerator = default(BodyOverlapEnumerator);
        if (ignoreEntityId != 0 && _entityIdToBody.TryGetValue(ignoreEntityId, out var ignoreHandle))
        {
            overlapEnumerator.HasIgnore = true;
            overlapEnumerator.Ignore = ignoreHandle;
        }

        Simulation.BroadPhase.GetOverlaps(min, max, BufferPool, ref overlapEnumerator);

        return !overlapEnumerator.Found;
    }

    /// <summary>
    ///     Whether straight up from <paramref name="feet" /> there is static world geometry
    ///     overhead: the test that tells open ground from a cave, a tunnel, the underside of an
    ///     overhang, and ground buried under the terrain itself. Two rays are cast — one down
    ///     from the sky and one up from just above the head — because BepuPhysics mesh shapes are
    ///     single-sided (hits register only on a triangle's front face as determined by winding
    ///     order). A cave ceiling whose triangles face away from a single probe direction would
    ///     let that ray sail right through, which is how mobs used to be placed underground and
    ///     still shoot players on the surface above them. Either ray hitting is enough to refuse
    ///     the spot; together they cover every face orientation the zone's baked collision can
    ///     present. Both stop just above the body's head, so by construction neither can hit the
    ///     ground the spot stands on.
    /// </summary>
    /// <remarks>
    ///     The reach is short on purpose (<see cref="DefaultSkyProbeHeight"/>): every cave,
    ///     tunnel and roofed space hangs lower over a body than that, while a zone's tree lines
    ///     and natural arches do not — a probe that reaches the sky reads a forest as a roof,
    ///     which is the other half of "the whole zone read as covered". A spot under a tall
    ///     natural arch is accepted, and that is the right answer for ambient spawning.
    /// </remarks>
    /// <param name="feet">Bottom centre of the body, on the ground surface.</param>
    /// <param name="bodyHeight">Body height in metres; the probe starts at the head.</param>
    /// <returns>True when static geometry hangs over the spot.</returns>
    public bool HasOverheadCover(Vector3 feet, float bodyHeight)
    {
        if (!float.IsFinite(feet.X) || !float.IsFinite(feet.Y) || !float.IsFinite(feet.Z) ||
            !float.IsFinite(bodyHeight) || bodyHeight < 0f)
        {
            // A spot this broken cannot be proven to be in the open, and an unproven spot is
            // exactly what this check exists to keep out of the world.
            return true;
        }

        var skyTop = new Vector3(feet.X, feet.Y, feet.Z + bodyHeight + DefaultSkyProbeHeight);
        const float headClearance = 0.1f;
        var justAboveHead = new Vector3(feet.X, feet.Y, feet.Z + bodyHeight + headClearance);

        // Downward ray: catches ceilings whose triangles face up (roofs, bridge decks seen from
        // below when the underside is part of the same outward-facing mesh).
        if (SegmentRayCast(skyTop, justAboveHead, 0, staticOnly: true).Hit)
        {
            return true;
        }

        // Upward ray: catches ceilings whose triangles face down (typical cave/terrain
        // undersides, where a single-sided mesh's front faces point away from a downward ray
        // and let it pass straight through — the bug that was placing underground mobs as
        // open-sky spawns). Either direction alone misses one face winding; together they
        // cover both.
        if (SegmentRayCast(justAboveHead, skyTop, 0, staticOnly: true).Hit)
        {
            return true;
        }

        return false;
    }

    /// <summary>
    ///     Whether a straight-line segment between two points is blocked by static world
    ///     geometry, accounting for single-sided collision meshes. BepuPhysics mesh shapes are
    ///     single-sided: a ray registers a hit only when it strikes a triangle's front face, so
    ///     a lone ray from below a floor to a target above sails through the floor's backside
    ///     and reports the target as visible — which is how mobs in caves were able to shoot
    ///     players walking on the ground above them. Casting both directions sees whichever
    ///     face orientation the mesh actually presents, which is what a line-of-sight query
    ///     actually wants: "is there anything solid between these two points".
    /// </summary>
    /// <param name="from">Segment start.</param>
    /// <param name="to">Segment end.</param>
    /// <param name="ignoreEntityId">Kinematic body to exclude (typically the source's own body), or 0.</param>
    /// <returns>True when any static geometry occludes the segment.</returns>
    public bool HasStaticOcclusion(Vector3 from, Vector3 to, ulong ignoreEntityId = 0)
    {
        if (SegmentRayCast(from, to, ignoreEntityId, staticOnly: true).Hit)
        {
            return true;
        }

        // Reverse cast catches single-sided backfaces — floors seen from below, ceilings seen
        // from above, and vertical walls wound the other direction by the mesh baker.
        if (SegmentRayCast(to, from, ignoreEntityId, staticOnly: true).Hit)
        {
            return true;
        }

        return false;
    }

    /// <summary>
    ///     The physics materials whose terrain is never navigation or spawn ground, matched by
    ///     the name the shipped database gives the material rather than by its id: <c>Water</c>
    ///     (10011) and <c>Water_Shallow</c> (10048) tag the shallows and the sea/lake beds below
    ///     the water line. A mob planned onto one would stand under water, invisible to whoever
    ///     walks above it yet able to see and shoot them, so the navigation mesh is baked without
    ///     these faces: no spawn point, and no path into the water either.
    /// </summary>
    private static bool IsUnderwaterMaterial(uint materialId) =>
        SDBInterface.GetPhysicsMaterial(materialId)?.Name is "Water" or "Water_Shallow";

    public void HandleProjectileImpact(
        CharacterEntity source,
        uint trace,
        SegmentRaycastHit hit,
        int damage = ProjectileSim.LegacyPlaceholderDamage,
        byte damageType = 0)
    {
        DebugProjectileHitCallbacks?.SendDebugProjectileImpact(source, trace, hit.HitPosition, hit.Normal);

        if (hit.Collidable.Mobility == CollidableMobility.Kinematic)
        {
            var bodyPosition = Simulation.Bodies[hit.Collidable.BodyHandle].Pose.Position;
            bodyPosition.Z -= 0.9f;
            DebugProjectileHitCallbacks?.SendDebugProjectilePoseHit(source, trace, hit.HitPosition, bodyPosition);

            var hitEntityId = _bodyToEntityId.GetValueOrDefault(hit.Collidable.BodyHandle);
            if (hitEntityId != 0 && TryGetActivePoseShapeData(hit.Collidable, hit.ChildIndex, out var poseShapeData))
            {
                var physicsMaterial = SDBInterface.GetPhysicsMaterial((uint)poseShapeData.Material);

                var headshot = poseShapeData.ShapeFlags.Headshot;
                var crit = physicsMaterial?.IsCritHit == 1;
                var damageMod = poseShapeData.DamageMod;

                _logger.Debug("ProjectileSim Impact on {ShapeName} (headshot={Headshot}, crit={Crit}, damageMod={DamageMod})", poseShapeData.Name, headshot, crit, damageMod);
                _logger.Debug("You hit {ShapeName} of {EntityId}", poseShapeData.Name, hitEntityId);
                _eventBus.Enqueue(new ProjectileHitEvent(hitEntityId, damage, source.EntityId, headshot, crit, damageMod, damageType));
                if (source.IsPlayerControlled && source.Player.Preferences.DebugWeapon != 0)
                {
                    _eventBus.Enqueue(new DebugChatDirectMessageEvent($"You hit {poseShapeData.Name} of {hitEntityId}", source.Player));
                }
            }
        }
    }

    public bool TryGetActivePoseShapeData(CollidableReference collidable, int childIndex, out ActivePoseShapeData shapeData)
    {
        shapeData = default;

        var body = Simulation.Bodies[collidable.BodyHandle];
        var shape = body.Collidable.Shape;
        if (!_poseCompoundToAssetId.TryGetValue(shape, out var poseId))
        {
            return false;
        }

        if (!_assetIdToPoseCompoundData.TryGetValue(poseId, out var poseData))
        {
            return false;
        }

        return poseData.TryGetValue(childIndex, out shapeData);
    }

    public (bool, Vector3, ulong) TargetRayCast(Vector3 origin, Vector3 direction, CharacterEntity source, float maxRange = 500f)
    {
        bool outHit = false;
        Vector3 outPos = Vector3.Zero;
        ulong outEnt = 0;

        var hitHandler = default(RayHitHandler);
        hitHandler.T = maxRange;

        // The source has no body yet in some edge cases (spawn racing, physics-less setups) —
        // just ray cast without the exclusion rather than throwing on the lookup.
        hitHandler.AvoidSourceBody = _entityIdToBody.TryGetValue(source.EntityId, out var sourceBody);
        hitHandler.SourceBody = sourceBody;

        Simulation.RayCast(origin, direction, float.MaxValue, BufferPool, ref hitHandler);
        if (hitHandler.T < maxRange)
        {
            outHit = true;
            outPos = origin + (direction * hitHandler.T);

            // The hit can be static world geometry, which has no entity — report entity 0.
            outEnt = _bodyToEntityId.GetValueOrDefault(hitHandler.HitCollidable.BodyHandle);
        }

        return (outHit, outPos, outEnt);
    }

    partial void DebugInitialize(bool isDebugPipeClient, uint zoneId);

    private BodyDescription CreateTestBall(Vector3 pos)
    {
        var bulletShape = new Sphere(3f);
        var bulletDescription = BodyDescription.CreateDynamic(new Vector3(), bulletShape.ComputeInertia(100), new(Simulation.Shapes.Add(bulletShape), 0.1f), 0.01f);
        bulletDescription.Pose.Position = pos;
        Simulation.Bodies.Add(bulletDescription);
        return bulletDescription;
    }

    /// <summary>
    ///     Broad phase enumerator that stops at the first non-static collidable overlapping the
    ///     queried box. Statics are the world's own geometry, which
    ///     <see cref="IsStandingVolumeClear" /> already probed with rays; what is left is the bodies
    ///     that occupy the volume.
    /// </summary>
    /// <remarks>
    ///     Never stored into unmanaged memory by the broad phase, so the struct can be as small as
    ///     the answer needs it to be; the same shape the collision query demo collects its overlaps
    ///     with, minus the list - here one hit is the whole answer.
    /// </remarks>
    private struct BodyOverlapEnumerator : IBreakableForEach<CollidableReference>
    {
        public bool Found;
        public bool HasIgnore;
        public BodyHandle Ignore;

        public bool LoopBody(CollidableReference reference)
        {
            if (reference.Mobility == CollidableMobility.Static)
            {
                return true;
            }

            if (HasIgnore && reference.BodyHandle.Equals(Ignore))
            {
                return true;
            }

            Found = true;

            // Breaking out is the point: the caller only asks whether anything is in the way.
            return false;
        }
    }

    private struct RayHitHandler : IRayHitHandler
    {
        public float T;
        public CollidableReference HitCollidable;
        public bool AvoidSourceBody;
        public BodyHandle SourceBody;
        public bool StaticOnly;
        public Vector3 Normal;
        public int ChildIndex;

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public readonly bool AllowTest(CollidableReference collidable)
        {
            if (StaticOnly && collidable.Mobility != CollidableMobility.Static)
            {
                return false;
            }

            if (AvoidSourceBody && collidable.Mobility != CollidableMobility.Static && collidable.BodyHandle.Equals(SourceBody))
            {
                return false;
            }

            return true;
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public readonly bool AllowTest(CollidableReference collidable, int childIndex)
        {
            if (StaticOnly && collidable.Mobility != CollidableMobility.Static)
            {
                return false;
            }

            if (AvoidSourceBody && collidable.Mobility != CollidableMobility.Static && collidable.BodyHandle.Equals(SourceBody))
            {
                return false;
            }

            return true;
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public void OnRayHit(in RayData ray, ref float maximumT, float t, Vector3 normal, CollidableReference collidable, int childIndex)
        {
            maximumT = t;
            T = t;
            HitCollidable = collidable;
            Normal = normal;
            ChildIndex = childIndex;
        }
    }
}
