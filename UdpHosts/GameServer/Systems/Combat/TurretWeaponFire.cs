using System;
using System.Collections.Generic;
using System.Numerics;
using BepuUtilities;
using GameServer.Entities.Character;
using GameServer.Entities.Turret;
using GameServer.StaticDB;
using GameServer.StaticDB.Records.dbcharacter;
using GameServer.Systems.Ai;
using GameServer.Systems.Aptitude;
using GameServer.Systems.WeaponSim;
using AiPrng = GameServer.Systems.PRNG.PRNG;

namespace GameServer.Systems.Combat;

/// <summary>
///     Fires one round per <c>dbcharacter::TurretWeapon</c> row of a turret type, resolved through
///     <see cref="NpcAttackResolver.ResolveWeapon" /> and handed to <see cref="IAiProjectileLauncher" />
///     the same way an NPC rifle is. The gunner (or unmanned owner) is the projectile source
///     (hostility, combat log, the watching-client announcement); the turret is only where the shot
///     leaves from.
/// </summary>
/// <remarks>
///     The client already plays the turret's burst markers from <c>FireBurst</c> / <c>FireEnd</c>
///     and a seated gunner predicts its own tracer from the fire input. This class is the missing
///     server half: a real <c>ProjectileSim</c> round so the shot can hit, using the weapon the
///     turret's own table names rather than the gunner's equipped one.
///     <para>
///     One packet is one round per barrel. The client already sends <c>FireWeaponProjectile</c>
///     once per projectile (a minigun streams many), so spending <c>RoundsPerBurst</c> here would
///     fire a burst per packet. Dual-weapon turrets (21 types in prod-1962) have two rows of the
///     same type, each with its own <c>PhysicalOrigin</c>; the packet does not name a hardpoint, so
///     every ranged row of the type fires once. A melee or unresolved row is skipped.
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
    ///     <c>ScalingLevelProp = 1</c> and have no monster row, so there is no other level to use
    ///     for the item/template lookup; a turret item that carries attribute 954 (Damage Per Round)
    ///     is used as-is, and <c>monsterId</c> 0 makes the creature damage modifier 1. The gunner's
    ///     battleframe progression then grows that number through
    ///     <see cref="WeaponDamageMath.DamageLevelScale" />, the same curve a handheld shot uses.
    /// </summary>
    public const byte TurretWeaponLevel = 1;

    /// <summary>
    ///     Production path: the loaded <c>dbcharacter::TurretWeapon</c> table and the static
    ///     database's weapon/ammo/attribute chain. The launcher is created per shot from the
    ///     turret's shard so a test can inject its own without touching this instance.
    /// </summary>
    public static TurretWeaponFire Production { get; } = new(
        SDBInterface.GetTurretWeapons,
        new NpcAttackResolver(new SdbNpcAttackDataSource()),
        hardpointOffset: SDBInterface.GetHardpointOffset);

    private readonly Func<uint, IReadOnlyList<TurretWeapon>> _weapons;
    private readonly NpcAttackResolver _resolver;
    private readonly IAiProjectileLauncher _launcher;
    private readonly Func<string, Vector3> _hardpointOffset;

    public TurretWeaponFire(
        Func<uint, IReadOnlyList<TurretWeapon>> weapons,
        NpcAttackResolver resolver,
        IAiProjectileLauncher launcher = null,
        Func<string, Vector3> hardpointOffset = null)
    {
        _weapons = weapons ?? throw new ArgumentNullException(nameof(weapons));
        _resolver = resolver ?? throw new ArgumentNullException(nameof(resolver));
        _launcher = launcher;
        _hardpointOffset = hardpointOffset;
    }

    /// <summary>
    ///     Fires one round of every ranged weapon row of the turret along
    ///     <paramref name="aimDirection" />, attributed to <paramref name="gunner" />. No-op when
    ///     the turret has no ranged weapon, the aim is degenerate, or either entity is missing.
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

        Vector3 aim = Vector3.Normalize(aimDirection);
        IAiProjectileLauncher launcher = _launcher ?? new ShardAiProjectileLauncher(turret.Shard);
        float levelScale = WeaponDamageMath.DamageLevelScale(gunner.FrameProgressionLevel);
        var remaining = turret.AmmoRemaining;
        bool replicateAmmo = false;

        if (remaining == null || remaining.Length != weapons.Count)
        {
            remaining = new ushort[weapons.Count];
            for (int i = 0; i < weapons.Count; i++)
            {
                remaining[i] = ResolveMagazineSize(weapons[i]);
            }

            replicateAmmo = true;
        }

        byte round = 0;
        for (int i = 0; i < weapons.Count; i++)
        {
            var weapon = weapons[i];
            if (weapon == null || weapon.WeaponId == 0)
            {
                continue;
            }

            // monsterId 0: a turret is not a monster, so there is no Creature Damage Modifier row
            // to apply (the lookup returns null and the modifier stays 1). The muzzle offset the
            // resolver stores is unused here - the shot leaves from TurretWeapon.PhysicalOrigin
            // plus MuzzleHardpoint, not from a monster projectile_offset.
            var profile = _resolver.ResolveWeapon(
                monsterId: 0,
                weapon.WeaponId,
                TurretWeaponLevel,
                NpcBehaviorParams.Empty,
                Vector3.Zero);

            if (!profile.IsRanged || profile.Ammo == null)
            {
                // A melee or unresolved row has nothing to fire through ProjectileSim. Hitscan
                // from a turret is not in this build's combat path and is not invented here.
                continue;
            }

            if (remaining[i] == 0 && profile.MagazineSize > 0)
            {
                // The turret protocol has no ReloadWeapon command, so an empty clip refills
                // before this round rather than blocking the packet the client already sent.
                remaining[i] = profile.MagazineSize;
            }

            if (NpcWeaponOvercharge.ShouldActivate(profile.OverchargeAbilityId, profile.MsOverchargeDelay, profile.ChargeUpMs)
                && profile.OverchargeClientFeedback)
            {
                gunner.Shard?.Abilities?.HandleActivateAbility(
                    gunner.Shard,
                    gunner,
                    profile.OverchargeAbilityId,
                    time,
                    new AptitudeTargets());
            }

            Vector3 origin = ResolveOrigin(turret, gunner, weapon, time, aim, shooterVelocity, _hardpointOffset);
            Vector3 direction = NpcAttackSpreadMath.Apply(
                aim,
                profile.SpreadPct,
                time,
                profile.SlotIndex,
                round,
                lastSpreadDirection: Vector3.Zero,
                lastSpreadTime: time);

            int damage = WeaponDamageMath.RoundDamage(profile.DamagePerRound * levelScale);
            launcher.FireRangedAttack(
                gunner,
                AiPrng.Trace(time, round),
                origin,
                direction,
                profile.Ammo,
                profile.Range,
                profile.ProjectileSpeed,
                profile.ImpactRadius,
                profile.MaxRadius,
                damage);

            if (remaining[i] > 0)
            {
                remaining[i]--;
                replicateAmmo = true;
            }

            round++;
        }

        if (replicateAmmo)
        {
            turret.SetAmmo(remaining);
        }
    }

    /// <summary>
    ///     The first ranged profile of a turret type, or <see cref="NpcAttackProfile.Unarmed" /> when
    ///     the type has none. Unmanned AI uses this for range and cadence; seated fire still walks
    ///     every row.
    /// </summary>
    public NpcAttackProfile ResolveLeadProfile(uint turretType)
    {
        var weapons = _weapons(turretType);
        if (weapons == null)
        {
            return NpcAttackProfile.Unarmed;
        }

        foreach (var weapon in weapons)
        {
            if (weapon == null || weapon.WeaponId == 0)
            {
                continue;
            }

            var profile = _resolver.ResolveWeapon(
                monsterId: 0,
                weapon.WeaponId,
                TurretWeaponLevel,
                NpcBehaviorParams.Empty,
                Vector3.Zero);

            if (profile.IsRanged && profile.Ammo != null)
            {
                return profile;
            }
        }

        return NpcAttackProfile.Unarmed;
    }

    /// <summary>
    ///     World-space muzzle: the turret's position plus <c>TurretWeapon.PhysicalOrigin</c> and
    ///     the translation of <c>MuzzleHardpoint</c> (from <c>dbvisualrecords::Hardpoints.Transform</c>),
    ///     rotated by the turret's current pose — the same local-to-world the character muzzle uses
    ///     (<c>QuaternionEx.Transform(offset, Inverse(rotation))</c>). A missing or zero origin falls
    ///     back to the gunner's own projectile origin, the same interpolation a handheld shot uses.
    /// </summary>
    internal static Vector3 ResolveOrigin(
        TurretEntity turret,
        CharacterEntity gunner,
        TurretWeapon weapon,
        uint time,
        Vector3 aim,
        Vector3? shooterVelocity,
        Func<string, Vector3> hardpointOffset = null)
    {
        Vector3 offset = weapon?.PhysicalOrigin != null
            ? SDBUtils.Vector3FromFauFau(weapon.PhysicalOrigin)
            : Vector3.Zero;

        if (hardpointOffset != null && !string.IsNullOrWhiteSpace(weapon?.MuzzleHardpoint))
        {
            offset += hardpointOffset(weapon.MuzzleHardpoint);
        }

        if (offset.LengthSquared() > 0.0001f)
        {
            return turret.Position + RotateByPose(offset, turret);
        }

        return gunner.GetProjectileOrigin(time, aim, shooterVelocity);
    }

    /// <summary>
    ///     Rotates a local-space muzzle offset into world space by the turret's replicated pose.
    ///     Identity (and a degenerate quaternion) leave the offset on the world axes, which is
    ///     what a turret that has never been aimed reports.
    /// </summary>
    internal static Vector3 RotateByPose(Vector3 localOffset, TurretEntity turret)
    {
        Quaternion rotation = turret?.Turret_ObserverView != null
            ? turret.Turret_ObserverView.CurrentPoseProp.Rotation
            : Quaternion.Identity;

        if (rotation.LengthSquared() < 0.0001f)
        {
            return localOffset;
        }

        return QuaternionEx.Transform(localOffset, QuaternionEx.Inverse(rotation));
    }

    private ushort ResolveMagazineSize(TurretWeapon weapon)
    {
        if (weapon == null || weapon.WeaponId == 0)
        {
            return 0;
        }

        var profile = _resolver.ResolveWeapon(
            monsterId: 0,
            weapon.WeaponId,
            TurretWeaponLevel,
            NpcBehaviorParams.Empty,
            Vector3.Zero);

        return profile.MagazineSize;
    }
}
