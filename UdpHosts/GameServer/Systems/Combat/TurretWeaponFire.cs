using System;
using System.Collections.Generic;
using System.Numerics;
using GameServer.Entities.Character;
using GameServer.Entities.Turret;
using GameServer.StaticDB;
using GameServer.StaticDB.Records.dbcharacter;
using GameServer.Systems.Ai;
using AiPrng = GameServer.Systems.PRNG.PRNG;

namespace GameServer.Systems.Combat;

/// <summary>
///     Fires one round from a seated turret: the first <c>dbcharacter::TurretWeapon</c> row of
///     the turret type, resolved through <see cref="NpcAttackResolver.ResolveWeapon" /> and handed
///     to <see cref="IAiProjectileLauncher" /> the same way an NPC rifle is. The gunner is the
///     projectile source (hostility, combat log, the watching-client announcement), the turret
///     is only where the shot leaves from.
/// </summary>
/// <remarks>
///     The client already plays the turret's burst markers from <c>FireBurst</c> / <c>FireEnd</c>
///     and predicts its own tracer from the fire input. This class is the missing server half:
///     a real <c>ProjectileSim</c> round so the shot can hit, using the weapon the turret's own
///     table names rather than the gunner's equipped one.
///     <para>
///     One packet is one round. The client already sends <c>FireWeaponProjectile</c> once per
///     projectile (a minigun streams many), so spending <c>RoundsPerBurst</c> here would fire a
///     burst per packet. Dual-weapon turrets (two rows of the same type) fire the first row by
///     <c>Id</c>: the packet does not say which barrel, and inventing a second shot is the
///     over-fire the burst rule is there to avoid.
///     </para>
///     <para>
///     The watching-client <c>WeaponProjectileFired</c> comes from
///     <see cref="ShardAiProjectileLauncher" /> alone. An extra CombatController-style ReliableGss
///     echo would double-send to the gunner: <c>EntityManager.SendToScoped</c> already includes
///     the character's own client.
///     </para>
/// </remarks>
public sealed class TurretWeaponFire
{
    /// <summary>
    ///     Level <c>NpcAttackResolver</c> reads the weapon at. Turrets replicate
    ///     <c>ScalingLevelProp = 1</c> and have no monster row, so there is no other level to use;
    ///     a turret item that carries attribute 954 (Damage Per Round) is used as-is, and
    ///     <c>monsterId</c> 0 makes the creature damage modifier 1.
    /// </summary>
    public const byte TurretWeaponLevel = 1;

    /// <summary>
    ///     Production path: the loaded <c>dbcharacter::TurretWeapon</c> table and the static
    ///     database's weapon/ammo/attribute chain. The launcher is created per shot from the
    ///     turret's shard so a test can inject its own without touching this instance.
    /// </summary>
    public static TurretWeaponFire Production { get; } = new(
        SDBInterface.GetTurretWeapons,
        new NpcAttackResolver(new SdbNpcAttackDataSource()));

    private readonly Func<uint, IReadOnlyList<TurretWeapon>> _weapons;
    private readonly NpcAttackResolver _resolver;
    private readonly IAiProjectileLauncher _launcher;

    public TurretWeaponFire(
        Func<uint, IReadOnlyList<TurretWeapon>> weapons,
        NpcAttackResolver resolver,
        IAiProjectileLauncher launcher = null)
    {
        _weapons = weapons ?? throw new ArgumentNullException(nameof(weapons));
        _resolver = resolver ?? throw new ArgumentNullException(nameof(resolver));
        _launcher = launcher;
    }

    /// <summary>
    ///     Fires one round of the turret's first weapon along <paramref name="aimDirection" />,
    ///     attributed to <paramref name="gunner" />. No-op when the turret has no ranged weapon,
    ///     the aim is degenerate, or either entity is missing.
    /// </summary>
    public void Fire(
        TurretEntity turret,
        CharacterEntity gunner,
        uint time,
        Vector3 aimDirection,
        Vector3? shooterVelocity = null)
    {
        if (turret == null || gunner == null)
        {
            return;
        }

        if (aimDirection.LengthSquared() <= 0.0001f)
        {
            return;
        }

        var weapons = _weapons(turret.Type);
        if (weapons == null || weapons.Count == 0)
        {
            return;
        }

        // First row by Id (the loader already orders them). A second barrel on the same type is
        // not a second shot of this packet: the client does not name a hardpoint, and firing both
        // would invent the extra round.
        var weapon = weapons[0];
        if (weapon == null || weapon.WeaponId == 0)
        {
            return;
        }

        // monsterId 0: a turret is not a monster, so there is no Creature Damage Modifier row to
        // apply (the lookup returns null and the modifier stays 1). The muzzle offset the resolver
        // stores is unused here - the shot leaves from TurretWeapon.PhysicalOrigin, not from a
        // monster projectile_offset.
        var profile = _resolver.ResolveWeapon(
            monsterId: 0,
            weapon.WeaponId,
            TurretWeaponLevel,
            NpcBehaviorParams.Empty,
            Vector3.Zero);

        if (!profile.IsRanged || profile.Ammo == null)
        {
            // A melee or unresolved row has nothing to fire through ProjectileSim. Hitscan from a
            // turret is not in this build's combat path and is not invented here.
            return;
        }

        Vector3 aim = Vector3.Normalize(aimDirection);
        Vector3 origin = ResolveOrigin(turret, gunner, weapon, time, aim, shooterVelocity);
        Vector3 direction = NpcAttackSpreadMath.Apply(
            aim,
            profile.SpreadPct,
            time,
            profile.SlotIndex,
            round: 0,
            lastSpreadDirection: Vector3.Zero,
            lastSpreadTime: time);

        IAiProjectileLauncher launcher = _launcher ?? new ShardAiProjectileLauncher(turret.Shard);
        launcher.FireRangedAttack(
            gunner,
            AiPrng.Trace(time, 0),
            origin,
            direction,
            profile.Ammo,
            profile.Range,
            profile.ProjectileSpeed,
            profile.ImpactRadius,
            profile.MaxRadius,
            profile.DamagePerRound);
    }

    /// <summary>
    ///     World-space muzzle: the turret's position plus <c>TurretWeapon.PhysicalOrigin</c> as the
    ///     row stores it (already world-axis, not rotated by the turret pose - the column is a
    ///     vector3, not a hardpoint, and <c>MuzzleHardpoint</c> is unused because nothing in this
    ///     build maps the name onto a bone). A missing or zero origin falls back to the gunner's
    ///     own projectile origin, the same interpolation a handheld shot uses.
    /// </summary>
    internal static Vector3 ResolveOrigin(
        TurretEntity turret,
        CharacterEntity gunner,
        TurretWeapon weapon,
        uint time,
        Vector3 aim,
        Vector3? shooterVelocity)
    {
        Vector3 offset = weapon?.PhysicalOrigin != null
            ? SDBUtils.Vector3FromFauFau(weapon.PhysicalOrigin)
            : Vector3.Zero;

        if (offset.LengthSquared() > 0.0001f)
        {
            return turret.Position + offset;
        }

        return gunner.GetProjectileOrigin(time, aim, shooterVelocity);
    }
}
