using System;
using System.Collections.Concurrent;
using System.Numerics;
using AeroMessages.GSS.Turret.View;
using GameServer.Entities;
using GameServer.Entities.Character;
using GameServer.Entities.Deployable;
using GameServer.Entities.Turret;
using GameServer.Systems.Combat;

namespace GameServer.Systems.Ai;

/// <summary>
///     Unmanned turret fire. A seated gunner already sends <c>FireWeaponProjectile</c>; this is the
///     server half for a turret nobody is sitting in: pick a hostile player inside the lead weapon's
///     range and hand one packet's worth of rounds to <see cref="TurretWeaponFire" />.
/// </summary>
/// <remarks>
///     <c>dbcharacter::Turret.Behavior</c> is a numeric flag (<c>"1"</c> or <c>-</c>), not a CAIS
///     behaviour string, so there is no monster tree to run. Range and cadence come from the first
///     ranged <c>TurretWeapon</c> row of the type (the same profile seated fire resolves). The
///     projectile source is the parent character when the turret is attached to one, otherwise the
///     parent <see cref="BaseAptitudeEntity.Owner" /> (a deployable or vehicle). Without a living
///     source there is no <see cref="IAiProjectileLauncher" /> to fire through, so the turret stays
///     silent. A seated turret (<c>ControllingPlayer != null</c>) is left to the gunner's packets.
///     <para>
///     Owned by <see cref="AiEngine" /> and ticked <b>before</b> the empty-brains early return, so a
///     shard with no NPCs still fires its turrets. The engine's <c>Enabled</c> / rules switch gates
///     this path too.
///     </para>
/// </remarks>
public sealed class TurretAi
{
    /// <summary>Chest height used for aim and line of sight, matching <c>AiEngine</c>.</summary>
    public const float EyeHeight = 1.4f;

    /// <summary>
    ///     How often an unmanned turret that finds no target retries the scan. Firing is gated by each
    ///     weapon's own interval (see <see cref="NpcAttackDamageMath.MinimumAttackIntervalMs"/>), but a
    ///     turret that never fires records no last-fire time, so without this its target scan - and the
    ///     line of sight casts it makes - would run on every shard tick (~200 Hz, ten times the NPC
    ///     perception cadence).
    /// </summary>
    private const ulong ScanIntervalMs = 200;

    private readonly ConcurrentDictionary<ulong, TurretBrain> _turrets = new();
    private readonly IShard _shard;
    private readonly IAiHostility _hostility;
    private readonly TurretWeaponFire _fire;

    public TurretAi(IShard shard, IAiHostility hostility, TurretWeaponFire fire)
    {
        _shard = shard ?? throw new ArgumentNullException(nameof(shard));
        _hostility = hostility ?? throw new ArgumentNullException(nameof(hostility));
        _fire = fire ?? throw new ArgumentNullException(nameof(fire));
    }

    /// <summary>Number of turrets currently being simulated.</summary>
    public int TrackedCount => _turrets.Count;

    /// <summary>Starts simulating a turret. Safe to call twice for the same id.</summary>
    public bool Register(TurretEntity turret)
    {
        if (turret == null)
        {
            return false;
        }

        return _turrets.TryAdd(turret.EntityId, new TurretBrain { Turret = turret });
    }

    /// <summary>Stops simulating a turret. Safe to call for unknown ids.</summary>
    public bool Unregister(ulong entityId) => _turrets.TryRemove(entityId, out _);

    /// <summary>Drops every tracked turret. Used when a zone is torn down.</summary>
    public void Clear() => _turrets.Clear();

    /// <summary>
    ///     The character a turret's shots are attributed to: the parent when it is a character,
    ///     otherwise the parent aptitude entity's <see cref="BaseAptitudeEntity.Owner" />. Null
    ///     when neither exists (an unowned deployable, a turret whose parent has despawned).
    /// </summary>
    public static CharacterEntity ResolveSource(TurretEntity turret)
    {
        if (turret?.Parent is CharacterEntity character)
        {
            return character;
        }

        if (turret?.Parent is BaseAptitudeEntity aptitude)
        {
            return aptitude.Owner;
        }

        return null;
    }

    public void Tick(ulong currentTime)
    {
        if (_turrets.IsEmpty)
        {
            return;
        }

        foreach (var entry in _turrets)
        {
            Update(entry.Value, currentTime);
        }
    }

    private void Update(TurretBrain brain, ulong currentTime)
    {
        var turret = brain.Turret;
        if (turret == null ||
            !_shard.Entities.TryGetValue(turret.EntityId, out var registered) ||
            !ReferenceEquals(registered, turret))
        {
            _turrets.TryRemove(brain.Turret?.EntityId ?? 0, out _);
            return;
        }

        // A seated gunner already sends FireWeaponProjectile; this path is unmanned only.
        if (turret.ControllingPlayer != null)
        {
            return;
        }

        if (turret.Parent is DeployableEntity { IsDead: true })
        {
            return;
        }

        var source = ResolveSource(turret);
        if (source == null || !source.IsAlive)
        {
            return;
        }

        var profile = _fire.ResolveLeadProfile(turret.Type);
        if (!profile.IsRanged || profile.Ammo == null)
        {
            return;
        }

        uint interval = profile.AttackIntervalMs > 0
            ? profile.AttackIntervalMs
            : NpcAttackDamageMath.MinimumAttackIntervalMs;
        if (brain.LastFireAt != 0 && currentTime < brain.LastFireAt + interval)
        {
            return;
        }

        // A turret that found no target last scan does not get to rescan on the very next tick: the shard
        // ticks far faster than anything in the scene moves, and an idle turret would otherwise run this
        // scan (and its line of sight casts) on every one of them.
        if (currentTime < brain.NextScanAt)
        {
            return;
        }

        float range = profile.AttackRange > 0f ? profile.AttackRange : profile.Range;
        if (range <= 0f)
        {
            return;
        }

        var target = FindTarget(turret, source, range);
        if (target == null)
        {
            brain.NextScanAt = currentTime + ScanIntervalMs;
            return;
        }

        Vector3 aimPoint = target.Position + new Vector3(0f, 0f, EyeHeight);
        Vector3 aim = aimPoint - turret.Position;
        if (aim.LengthSquared() <= 0.0001f)
        {
            return;
        }

        if (turret.Turret_ObserverView != null)
        {
            turret.Turret_ObserverView.CurrentPoseProp = new CurrentPoseStruct
            {
                Rotation = LookRotation(aim),
                ShortTime = _shard.CurrentShortTime,
            };
        }

        uint time = unchecked((uint)currentTime);
        turret.SetFireBurst(time);
        _fire.Fire(turret, source, time, aim);
        brain.LastFireAt = currentTime;
    }

    private CharacterEntity FindTarget(TurretEntity turret, CharacterEntity source, float range)
    {
        ulong sourceId = source.EntityId;
        float rangeSq = range * range;
        CharacterEntity best = null;
        float bestDistanceSq = float.MaxValue;

        foreach (var client in _shard.Clients.Values)
        {
            var candidate = client?.CharacterEntity;
            if (candidate == null || !candidate.IsAlive || candidate.EntityId == sourceId)
            {
                continue;
            }

            // One shard simulates one zone: a player in another zone stands on ground this
            // simulation knows nothing about, so their position can only coincide with a
            // turret's range by accident. Never fire across that boundary.
            if (!ShardZone.IsPlayerInZone(_shard, client))
            {
                continue;
            }

            if (!_hostility.IsHostile(turret, candidate))
            {
                continue;
            }

            float distanceSq = Vector3.DistanceSquared(turret.Position, candidate.Position);
            if (distanceSq > rangeSq || distanceSq >= bestDistanceSq)
            {
                continue;
            }

            if (!HasLineOfSight(turret, candidate))
            {
                continue;
            }

            bestDistanceSq = distanceSq;
            best = candidate;
        }

        return best;
    }

    private bool HasLineOfSight(TurretEntity turret, CharacterEntity target)
    {
        var physics = _shard.Physics;
        if (physics == null)
        {
            return true;
        }

        var from = turret.Position + new Vector3(0f, 0f, EyeHeight);
        var to = target.Position + new Vector3(0f, 0f, EyeHeight);
        var hit = physics.SegmentRayCast(from, to, turret.EntityId);
        return !hit.Hit || hit.HitEntityId == target.EntityId;
    }

    /// <summary>
    ///     Full pitch+yaw look rotation that maps local +Y (the turret's forward) onto
    ///     <paramref name="direction" />. Character facing is yaw-only; a turret pose is a full
    ///     quaternion because the barrel pitches.
    /// </summary>
    internal static Quaternion LookRotation(Vector3 direction)
    {
        if (direction.LengthSquared() < 0.0001f)
        {
            return Quaternion.Identity;
        }

        direction = Vector3.Normalize(direction);
        float yaw = MathF.Atan2(direction.X, direction.Y);
        float horizontal = MathF.Sqrt((direction.X * direction.X) + (direction.Y * direction.Y));
        float pitch = MathF.Atan2(-direction.Z, horizontal);
        return Quaternion.CreateFromAxisAngle(Vector3.UnitZ, yaw)
               * Quaternion.CreateFromAxisAngle(Vector3.UnitX, pitch);
    }

    private sealed class TurretBrain
    {
        public TurretEntity Turret;
        public ulong LastFireAt;

        /// <summary>Earliest shard time the next idle target scan may run, see <see cref="ScanIntervalMs"/>.</summary>
        public ulong NextScanAt;
    }
}
