using System;
using System.Collections.Concurrent;
using System.Numerics;
using System.Threading;
using GameServer.Entities.Character;
using GameServer.Enums;
using GameServer.Extensions;
using GameServer.Physics;
using GameServer.StaticDB.Records.dbitems;
using GameServer.Systems.Aptitude;
using GameServer.Systems.WeaponSim;

namespace GameServer.Systems.ProjectileSim;

public class ProjectileSim
{
    /// <summary>
    /// Fallback damage used when a weapon has no usable database value at all — the
    /// legacy flat 1337 placeholder. Never the damage of a healthy weapon row.
    /// </summary>
    public const int LegacyPlaceholderDamage = 1337;

    /// <summary>
    /// How often in-flight projectiles are stepped. The shard thread runs unbounded (it
    /// spins between the subsystems' own intervals), and without a gate this Tick ran on
    /// every spin iteration: the per-projectile physics raycast was issued thousands of
    /// times per second and the homing integration below advanced a missile by a full
    /// 50 ms of flight on *every* iteration, i.e. at CPU speed instead of real time.
    /// 20 ms matches the ability system's cadence and keeps impact detection snappy.
    /// </summary>
    private const ulong UpdateIntervalMs = 20;

    /// <summary>
    /// A global upper bound on in-flight rounds. Each one issues a physics ray every 20 ms; without
    /// this backpressure a crowded NPC encounter can retain tens of thousands of slow rounds and
    /// turn every shard update into a ray-cast storm. New rounds are dropped while full — existing
    /// rounds still complete, so the simulation recovers by itself rather than growing a queue.
    /// </summary>
    private const int MaxActiveProjectiles = 256;

    /// <summary>
    /// A delayed shard must not replay an unbounded number of period abilities for one projectile in
    /// a single update. Four preserves normal catch-up while a long pause resynchronises instead of
    /// making the recovery tick slower than the pause that caused it.
    /// </summary>
    private const int MaxPeriodAbilityActivationsPerUpdate = 4;

    private readonly Shard _shard;
    private readonly Serilog.ILogger _logger;
    private readonly DebugProjectileHitCallbacks? _debugCallbacks;
    private readonly ConcurrentDictionary<(ulong EntityId, uint TraceId), ActiveProjectile> _activeProjectiles;
    private ulong _lastUpdate;

    public ProjectileSim(Shard shard, DebugProjectileHitCallbacks? debugCallbacks = null)
    {
        _shard = shard;
        _debugCallbacks = debugCallbacks;
        _logger = shard.Logger.ForContext<ProjectileSim>();
        _activeProjectiles = new ConcurrentDictionary<(ulong EntityId, uint TraceId), ActiveProjectile>();
    }

    public void FireProjectile(CharacterEntity entity, uint trace, Vector3 origin, Vector3 direction, Ammo ammo, float range, float projectileSpeed, float impactRadius, float maxRadius)
    {
        FireProjectile(entity, trace, origin, direction, ammo, range, projectileSpeed, impactRadius, maxRadius, LegacyPlaceholderDamage);
    }

    /// <summary>
    /// Fires a server-simulated projectile. <paramref name="damage"/> is the damage the
    /// projectile deals at the muzzle; when the fired ammo row defines damage falloff
    /// (<c>damage_decay</c> != 0) the actual impact damage is reduced by the distance the
    /// projectile has travelled when it hits (see <c>WeaponDamageMath.ApplyDamageFalloff</c>).
    /// </summary>
    public void FireProjectile(CharacterEntity entity, uint trace, Vector3 origin, Vector3 direction, Ammo ammo, float range, float projectileSpeed, float impactRadius, float maxRadius, int damage)
    {
        float directionLengthSquared = direction.LengthSquared();
        if (entity == null || ammo == null || !IsFinite(origin) || !IsFinite(direction) ||
            !float.IsFinite(directionLengthSquared) || directionLengthSquared < 0.0001f ||
            !float.IsFinite(projectileSpeed) || projectileSpeed <= 0f || !float.IsFinite(range) || range <= 0f ||
            !float.IsFinite(impactRadius) || impactRadius < 0f || !float.IsFinite(maxRadius) || maxRadius < 0f)
        {
            if (OnceLog.ShouldLog((nameof(ProjectileSim), "invalid fire input", ammo?.Id ?? 0u)))
            {
                _logger.Warning(
                    "Discarding projectile with invalid input (source {Source}, ammo {AmmoId}, speed {Speed}, range {Range}, impact radius {ImpactRadius}, max radius {MaxRadius})",
                    entity?.EntityId ?? 0,
                    ammo?.Id ?? 0,
                    projectileSpeed,
                    range,
                    impactRadius,
                    maxRadius);
            }

            return;
        }

        if (_activeProjectiles.Count >= MaxActiveProjectiles)
        {
            if (OnceLog.ShouldLog((nameof(ProjectileSim), "active projectile cap", _shard.InstanceId)))
            {
                _logger.Warning(
                    "Projectile simulation reached its {MaxActiveProjectiles} active-round cap; dropping new rounds until it recovers",
                    MaxActiveProjectiles);
            }

            return;
        }

        direction = Vector3.Normalize(direction);
        var ammoFlags = new AmmoFlags(ammo.Flags);
        bool isDrunk = DrunkMissile.IsActive(ammo);

        var velocity = projectileSpeed * direction;
        float actualSpeed = velocity.Length();
        var lifetimeMs = ammo.ConstLifetime > 0
            ? ammo.ConstLifetime
            : ComputeDefaultLifetimeMs(range, actualSpeed);
        var endPosition = origin + (velocity * (lifetimeMs / 1000f));
        if (!IsFinite(velocity) || !float.IsFinite(actualSpeed) || !IsFinite(endPosition))
        {
            if (OnceLog.ShouldLog((nameof(ProjectileSim), "unrepresentable trajectory", ammo.Id)))
            {
                _logger.Warning(
                    "Discarding projectile with an unrepresentable trajectory (source {Source}, ammo {AmmoId}, speed {Speed}, lifetime {Lifetime})",
                    entity.EntityId,
                    ammo.Id,
                    projectileSpeed,
                    lifetimeMs);
            }

            return;
        }

        var projectile = new ActiveProjectile
        {
            EntityId = entity.EntityId,
            TraceId = trace,
            Type = ammoFlags.Simulation,
            Ammo = ammo,
            Origin = origin,
            Direction = direction,
            Velocity = velocity,
            InitialVelocity = velocity,
            StartPosition = origin,
            EndPosition = endPosition,
            CurrentPosition = origin,
            PreviousPosition = origin,
            DamageAmount = damage,
            StartTime = _shard.CurrentTime,
            LifetimeMs = lifetimeMs,
            Range = range,
            BouncesRemaining = ammo.MaxBounces,
            HitsRemaining = (byte)ammo.MaxHits,
            IsAlive = true,
            HasHit = false,
            TargetEntityId = 0,
            HitEntityId = 0,
            HitPosition = Vector3.Zero,
            HitNormal = Vector3.Zero,
            AccumulatedDt = 0f,
            ImpactRadius = impactRadius,
            MaxRadius = maxRadius,
            IsDrunk = isDrunk
        };

        if (!_activeProjectiles.TryAdd((entity.EntityId, trace), projectile))
        {
            // Trace ids originate with the firing protocol. A duplicate must not replace a round
            // that is already in flight: doing so makes its impact/lifetime depend on packet order.
            if (OnceLog.ShouldLog((nameof(ProjectileSim), "duplicate trace", entity.EntityId, trace)))
            {
                _logger.Warning(
                    "Discarding duplicate in-flight projectile trace {Trace} from entity {EntityId}",
                    trace,
                    entity.EntityId);
            }

            return;
        }

        _logger.Debug("Spawned {Type} projectile trace={Trace}, speed={Speed}, range={Range}, lifetime={Lifetime}ms, impactRadius={ImpactRadius}, maxRadius={MaxRadius}", ammoFlags.Simulation, trace, projectileSpeed, range, lifetimeMs, impactRadius, maxRadius);
        SendDebugSpawn(entity, trace, origin, direction, projectileSpeed);
    }

    public void Tick(double deltaTime, ulong currentTime, CancellationToken ct)
    {
        if (_activeProjectiles.IsEmpty || ct.IsCancellationRequested)
        {
            // Keep the homing step clock fresh while nothing is in flight, so the first
            // update of the next volley steps by the interval instead of the whole quiet
            // period between volleys. Only the shard thread ever calls this.
            _lastUpdate = currentTime;
            return;
        }

        if (currentTime <= _lastUpdate + UpdateIntervalMs)
        {
            return;
        }

        var elapsedSinceUpdateMs = _lastUpdate == 0 ? UpdateIntervalMs : currentTime - _lastUpdate;
        _lastUpdate = currentTime;

        // Enumerate the live dictionary instead of snapshotting Keys.ToArray():
        // ConcurrentDictionary's enumerator is safe during concurrent modification
        // (new projectiles are simply picked up on the next tick), and this loop
        // no longer allocates a key array on every update.
        foreach (var entry in _activeProjectiles)
        {
            var key = entry.Key;

            if (!_activeProjectiles.TryGetValue(key, out var projectile))
            {
                continue;
            }

            if (ct.IsCancellationRequested)
            {
                break;
            }

            uint elapsedMs = (uint)(currentTime - projectile.StartTime);

            projectile.PreviousPosition = projectile.CurrentPosition;

            Vector3 basePosition;
            switch (projectile.Type)
            {
                case AmmoFlags.SimulationMode.Basic:
                    basePosition = ComputeLinearPosition(projectile, elapsedMs);
                    break;

                case AmmoFlags.SimulationMode.Linear:
                    basePosition = ComputeLinearPosition(projectile, elapsedMs);
                    break;

                case AmmoFlags.SimulationMode.Parabolic:
                    basePosition = ComputeParabolicPosition(ref projectile, elapsedMs);
                    break;

                case AmmoFlags.SimulationMode.Homing:
                    basePosition = UpdateHoming(ref projectile, elapsedMs, projectile.CurrentPosition - projectile.DrunkOffset, elapsedSinceUpdateMs);
                    break;

                default:
                    basePosition = projectile.CurrentPosition;
                    break;
            }

            if (projectile.IsDrunk)
            {
                float elapsedSec = elapsedMs / 1000.0f;
                float progress = projectile.LifetimeMs == 0 ? 0f : elapsedMs / (float)projectile.LifetimeMs;
                projectile.DrunkOffset = DrunkMissile.ComputeOffset(projectile.InitialVelocity, projectile.Ammo, elapsedSec, progress, projectile.TraceId);
            }
            else
            {
                projectile.DrunkOffset = Vector3.Zero;
            }

            projectile.CurrentPosition = basePosition + projectile.DrunkOffset;

            var source = GetSourceEntity(projectile);
            var periodActivations = 0;
            while (projectile.IsAlive && AmmoAbilityHooks.TryPeriod(projectile.Ammo, elapsedMs, ref projectile.LastPeriodElapsedMs, out uint periodAbility))
            {
                if (periodActivations++ >= MaxPeriodAbilityActivationsPerUpdate)
                {
                    // The time was already missed. Advance to now rather than replaying hundreds
                    // of expired ticks and turning a single long frame into an exception/ability
                    // storm that prevents recovery.
                    projectile.LastPeriodElapsedMs = elapsedMs;
                    _logger.Debug(
                        "Projectile trace={Trace} skipped delayed period abilities after {MaxPeriodAbilityActivationsPerUpdate} catch-up activations",
                        projectile.TraceId,
                        MaxPeriodAbilityActivationsPerUpdate);
                    break;
                }

                AmmoAbilityHooks.Activate(_shard, source, periodAbility);
            }

            var hit = _shard.Physics.SegmentRayCast(projectile.PreviousPosition, projectile.CurrentPosition, projectile.EntityId);

            if (hit.Hit)
            {
                projectile.HasHit = true;
                projectile.HitEntityId = hit.HitEntityId;
                projectile.HitPosition = hit.HitPosition;
                projectile.HitNormal = hit.Normal;

                // Distance travelled up to the impact point (the segment past the hit does not count).
                projectile.DistanceTravelled += Vector3.Distance(projectile.PreviousPosition, hit.HitPosition);

                IAptitudeTarget hitTarget = ResolveHitTarget(hit.HitEntityId);

                if (TryBounce(ref projectile, hit))
                {
                    projectile.PreviousPosition = hit.HitPosition;
                    _logger.Debug("Projectile trace={Trace} bounced off entity={Entity} at {Pos}", projectile.TraceId, hit.HitEntityId, hit.HitPosition);
                    SendDebugBounce(projectile, hit.HitPosition, hit.Normal);
                    AmmoAbilityHooks.Activate(_shard, source, AmmoAbilityHooks.TouchAbility(projectile.Ammo), hitTarget);
                }
                else
                {
                    projectile.IsAlive = false;
                    projectile.HitsRemaining = 0;

                    // Retire before delivering gameplay callbacks. Ability chains are database
                    // supplied and can fail; if one throws before the old end-of-loop removal,
                    // this same round remains active and impacts again every tick. That was the
                    // source of the repeated damage/ability/NullReferenceException loop in the
                    // supplied server log. Removal first makes a terminal impact exactly once.
                    _activeProjectiles.TryRemove(key, out _);

                    _logger.Debug("Projectile trace={Trace} impact entity={Entity} at {Pos} after {Distance}m", projectile.TraceId, hit.HitEntityId, hit.HitPosition, projectile.DistanceTravelled);
                    if (source != null)
                    {
                        int impactDamage = WeaponDamageMath.ApplyDamageFalloff(
                            projectile.DamageAmount,
                            projectile.DistanceTravelled,
                            projectile.Range,
                            projectile.Ammo.DamageDecay,
                            projectile.Ammo.DamageDecayRangefrac,
                            projectile.Ammo.MinDamageFrac);
                        _logger.Debug("Projectile trace={Trace} impact damage {Damage} (base {Base}, {Distance}m travelled, type {DamageType})", projectile.TraceId, impactDamage, projectile.DamageAmount, projectile.DistanceTravelled, projectile.Ammo.Damagetype);
                        _shard.Physics.HandleProjectileImpact(source, projectile.TraceId, hit, impactDamage, projectile.Ammo.Damagetype);
                    }

                    AmmoAbilityHooks.Activate(_shard, source, AmmoAbilityHooks.TouchAbility(projectile.Ammo), hitTarget);
                    AmmoAbilityHooks.Activate(_shard, source, AmmoAbilityHooks.ImpactAbility(projectile.Ammo), hitTarget);

                    // The entry has already been removed. In particular, do not reach the generic
                    // removal below: a synchronous callback is allowed to fire a new round with
                    // the same protocol trace id, and that new round must not be removed here.
                    continue;
                }
            }
            else
            {
                // Full segment travelled, nothing hit on the way.
                projectile.DistanceTravelled += Vector3.Distance(projectile.PreviousPosition, projectile.CurrentPosition);
            }

            if (elapsedMs >= projectile.LifetimeMs)
            {
                // Airburst is also a terminal callback and can synchronously start another round
                // with this trace. Remove the expiring round first for the same reason as impact.
                _activeProjectiles.TryRemove(key, out _);

                if (projectile.IsAlive)
                {
                    AmmoAbilityHooks.Activate(_shard, source, AmmoAbilityHooks.AirburstAbility(projectile.Ammo));
                }

                _logger.Debug("Projectile trace={Trace} expired at {Elapsed}/{Lifetime}ms", projectile.TraceId, elapsedMs, projectile.LifetimeMs);
                SendDebugTimeout(projectile, projectile.CurrentPosition);
                continue;
            }

            if (!projectile.IsAlive)
            {
                _activeProjectiles.TryRemove(key, out _);
            }
            else
            {
                _activeProjectiles[key] = projectile;
            }
        }
    }

    private static bool IsFinite(Vector3 value) =>
        float.IsFinite(value.X) && float.IsFinite(value.Y) && float.IsFinite(value.Z);

    private static uint ComputeDefaultLifetimeMs(float range, float speed)
    {
        float computedMs = ((range / Math.Max(speed, 0.001f)) * 1000f) + 0.5f;
        uint ms = (uint)Math.Max(0f, computedMs);
        return ms == 0 ? 1u : Math.Min(ms, 5000u);
    }

    private static Vector3 ComputeLinearPosition(ActiveProjectile proj, uint elapsedMs)
    {
        float t = Math.Min(1f, (float)elapsedMs / proj.LifetimeMs);
        return Vector3.Lerp(proj.StartPosition, proj.EndPosition, t);
    }

    private static Vector3 ComputeParabolicPosition(ref ActiveProjectile proj, uint elapsedMs)
    {
        uint clampedElapsedMs = Math.Min(elapsedMs, proj.LifetimeMs);
        float t = clampedElapsedMs / 1000.0f;
        var gravityAccel = new Vector3(0f, 0f, -proj.Ammo.Gravity);
        proj.Velocity = proj.InitialVelocity + (gravityAccel * t);
        return proj.StartPosition + (proj.InitialVelocity * t) + (gravityAccel * (0.5f * t * t));
    }

    private Vector3 UpdateHoming(ref ActiveProjectile proj, uint elapsedMs, Vector3 basePosition, ulong stepMs)
    {
        if (proj.TargetEntityId != 0 && _shard.Entities.TryGetValue(proj.TargetEntityId, out var target))
        {
            var targetPos = target.Position;
            var toTarget = targetPos - basePosition;
            if (toTarget.LengthSquared() > 0.0001f)
            {
                var toTargetDir = Vector3.Normalize(toTarget);
                float homingStrength = proj.Ammo.HomingStrength * 0.01f;
                proj.Velocity = Vector3.Lerp(proj.Velocity, homingStrength * toTargetDir, 0.1f);
            }

            // Integrate by the time actually elapsed since the previous projectile update, not by a
            // fixed 50 ms step: this Tick used to run on every unbounded shard-loop iteration, so a
            // homing missile covered 50 ms of flight per spin (hundreds of steps per real second).
            // The step is clamped so a long stall (GC pause, debugger) cannot teleport the missile.
            float clampedStepMs = Math.Min(stepMs, 100ul);
            basePosition += proj.Velocity * (clampedStepMs / 1000f);
        }
        else
        {
            float t = Math.Min(1f, (float)elapsedMs / proj.LifetimeMs);
            basePosition = Vector3.Lerp(proj.StartPosition, proj.EndPosition, t);
        }

        return basePosition;
    }

    private bool TryBounce(ref ActiveProjectile proj, SegmentRaycastHit hit)
    {
        if (proj.BouncesRemaining <= 0)
        {
            return false;
        }

        var velocityDir = Vector3.Normalize(proj.Velocity);
        var bounceAngle = Vector3.Dot(-velocityDir, hit.Normal);

        float threshold = proj.Ammo.BounceCos;
        if (hit.Normal.Z < 1f)
        {
            threshold = proj.Ammo.SlopeBounceCos;
        }

        if (bounceAngle < threshold)
        {
            return false;
        }

        var reflected = Vector3.Reflect(velocityDir, hit.Normal);

        // BounceFriction=1 means no friction (full speed preserved)
        // TODO: If bounces feel wrong, try: elasticity * friction (treats as direct multiplier)
        var speedScale = proj.Ammo.BounceElasticity * (2f - proj.Ammo.BounceFriction);
        proj.Velocity = reflected * proj.Ammo.ProjectileSpeed * Math.Max(speedScale, 0f);
        proj.CurrentPosition = hit.HitPosition;
        proj.BouncesRemaining--;

        return true;
    }

    private CharacterEntity GetSourceEntity(ActiveProjectile proj)
    {
        if (_shard.Entities.TryGetValue(proj.EntityId, out var entity))
        {
            return entity as CharacterEntity;
        }

        return null;
    }

    private IAptitudeTarget ResolveHitTarget(ulong hitEntityId)
    {
        if (hitEntityId == 0 || !_shard.Entities.TryGetValue(hitEntityId, out var entity))
        {
            return null;
        }

        return entity as IAptitudeTarget;
    }

    private void SendDebugSpawn(CharacterEntity entity, uint traceId, Vector3 origin, Vector3 direction, float speed)
    {
        _debugCallbacks?.SendDebugProjectileSpawn(entity, traceId, origin, direction, speed);
    }

    private void SendDebugBounce(ActiveProjectile proj, Vector3 position, Vector3 normal)
    {
        var source = GetSourceEntity(proj);
        if (source != null)
        {
            _debugCallbacks?.SendDebugProjectileBounce(source, proj.TraceId, position, normal);
        }
    }

    private void SendDebugTimeout(ActiveProjectile proj, Vector3 position)
    {
        var source = GetSourceEntity(proj);
        if (source != null)
        {
            var timeoutDirection = Vector3.Normalize(proj.Direction);
            _debugCallbacks?.SendDebugProjectileTimeout(source, proj.TraceId, position, timeoutDirection);
        }
    }
}
