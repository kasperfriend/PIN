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
    ///     How far above a spot the sky exposure cast reaches when the zone's own bounds are unknown:
    ///     half a chunk's worth of height, more than the cover any zone's collision can put over its
    ///     own ground.
    /// </summary>
    private const float SkyReachFallback = 512f;

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
                    _zoneLoader.IsNavigationExcluded)
                : null;
            _logger.Information(
                "Zone {ZoneId}: navigation mesh has {TriangleCount} source triangles and {FaceCount} walkable faces after {Elapsed}",
                zoneId,
                _zoneLoader.NavigationTriangles.Count,
                _navigationMesh?.FaceCount ?? 0,
                bakeStarted.Elapsed);
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
    /// </remarks>
    public Vector3? FindGround(Vector3 position, ulong ignoreEntityId = 0)
    {
        const float searchUp = 10_000f;
        const float searchDown = 10_000f;

        var from = new Vector3(position.X, position.Y, position.Z + searchUp);
        var to = new Vector3(position.X, position.Y, position.Z - searchDown);
        var hit = SegmentRayCast(from, to, ignoreEntityId, staticOnly: true);
        if (!hit.Hit)
        {
            return null;
        }

        return new Vector3(position.X, position.Y, hit.HitPosition.Z);
    }

    /// <summary>
    ///     The static ground surface under <paramref name="position" /> and the normal it faces,
    ///     found by the same straight down probe <see cref="FindGround" /> uses. Returns false when
    ///     nothing is hit - typically because no zone collision data is loaded, or because the point
    ///     is over a void.
    /// </summary>
    /// <remarks>
    ///     The normal is what tells walkable ground from a cliff face: the zone's navigation mesh is
    ///     baked with a minimum walkable normal Z of 0.35, so a surface steeper than that is one no
    ///     NPC could stand on even though the ray hit something. Callers that only need the height
    ///     keep using <see cref="FindGround" />.
    /// </remarks>
    /// <param name="position">The point to look under.</param>
    /// <param name="ground">The surface position, keeping <paramref name="position" />'s X and Y.</param>
    /// <param name="normal">The normal of the surface that was hit.</param>
    /// <param name="searchUp">How far above <paramref name="position" /> the probe starts.</param>
    /// <param name="searchDown">How far below <paramref name="position" /> the probe reaches.</param>
    /// <returns>Whether a surface was hit.</returns>
    public bool TryGetGroundSurface(
        Vector3 position,
        out Vector3 ground,
        out Vector3 normal,
        float searchUp = 10_000f,
        float searchDown = 10_000f)
    {
        ground = position;
        normal = default;

        var from = new Vector3(position.X, position.Y, position.Z + searchUp);
        var to = new Vector3(position.X, position.Y, position.Z - searchDown);
        var hit = SegmentRayCast(from, to, 0, staticOnly: true);
        if (!hit.Hit)
        {
            return false;
        }

        ground = new Vector3(position.X, position.Y, hit.HitPosition.Z);
        normal = hit.Normal;
        return true;
    }

    /// <summary>
    ///     Whether a spot has an unobstructed vertical line to the sky: a straight up cast against
    ///     the zone's static geometry only, out to the top of the zone's own bounds. A hit means the
    ///     spot is covered - a cave floor, an underground tunnel, ground under a roof or a rock
    ///     overhang - and a place world population must not put an NPC on, whatever the navigation
    ///     mesh says of it: a cave floor is flat, and the mesh has no way to know the sky.
    /// </summary>
    /// <remarks>
    ///     The cast is statics only, so a player or another body standing over the spot cannot make
    ///     it look covered. It starts a hair above the spot, the same way the standing volume's
    ///     headroom probe does, so the surface the spot stands on is not its own obstruction. A zone
    ///     with no static geometry cannot cover a spot (the answer is true), and a non-finite spot
    ///     cannot be cast from (the answer is false).
    /// </remarks>
    /// <param name="position">The spot, on or just above the ground it stands on.</param>
    /// <returns>True when nothing of the zone's own geometry sits above the spot.</returns>
    public bool IsExposedToSky(Vector3 position)
    {
        if (!HasZoneCollision)
        {
            return true;
        }

        if (!float.IsFinite(position.X) || !float.IsFinite(position.Y) || !float.IsFinite(position.Z))
        {
            return false;
        }

        float top = position.Z + SkyReachFallback;
        var boundsMax = ZoneBoundsMax;
        if (boundsMax.HasValue && float.IsFinite(boundsMax.Value.Z) && boundsMax.Value.Z > position.Z + 1f)
        {
            top = boundsMax.Value.Z;
        }

        var from = new Vector3(position.X, position.Y, position.Z + 0.05f);
        var to = new Vector3(position.X, position.Y, top);
        return !SegmentRayCast(from, to, 0, staticOnly: true).Hit;
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
                if (SegmentRayCast(origin, origin + (direction * reach), ignoreEntityId, staticOnly: true).Hit)
                {
                    return false;
                }
            }
        }

        // Headroom: a body under a low overhang or a ledge is a body stuck in the world. Started
        // just above the feet so a body that sank a hair into the surface does not report itself.
        var headFrom = new Vector3(feet.X, feet.Y, feet.Z + 0.05f);
        var headTo = new Vector3(feet.X, feet.Y, feet.Z + height + HeadroomMargin);
        if (SegmentRayCast(headFrom, headTo, ignoreEntityId, staticOnly: true).Hit)
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
