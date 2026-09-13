using System;
using System.Collections.Generic;
using System.Numerics;
using System.Reflection;
using AeroMessages.GSS.Turret.View;
using GameServer.Entities.Turret;
using GameServer.StaticDB.Records.dbcharacter;
using GameServer.StaticDB.Records.dbitems;
using GameServer.Systems.Ai;
using GameServer.Systems.Combat;
using GameServer.Systems.WeaponSim;
using GameServer.Tests.Fakes;
using Xunit;

namespace GameServer.Tests;

public class TurretWeaponFireTests
{
    private const uint TurretTypeId = 21;
    private const uint WeaponId = 30_025;
    private const uint SecondWeaponId = 30_026;
    private const ushort AmmoId = 922;
    private const uint Time = 60_000;

    private static readonly Vector3 Aim = Vector3.Normalize(new Vector3(0f, 1f, 0f));
    private static readonly Vector3 TurretPosition = new(10f, 20f, 30f);
    private static readonly Vector3 GunnerPosition = new(100f, 50f, 8f);

    private static WeaponTemplateResult RangedTemplate() => new()
    {
        DebugName = "Turret rifle",
        FireType = 1,
        Range = 30f,
        DamagePerRound = 40,
        MsPerBurst = 100,
        RoundsPerBurst = 4,
        AmmoId = AmmoId,
        BaseClipSize = 20,
        AmmoPerBurst = 1,
        ReloadTime = 700,
        SlotIndex = 2,
    };

    private static WeaponTemplateResult MeleeTemplate() => new()
    {
        DebugName = "Turret melee",
        Range = 2.6f,
        DamagePerRound = 225,
        MsPerBurst = 1600,
        RoundsPerBurst = 1,
        AmmoId = 0,
    };

    private static FakeNpcAttackDataSource DataWith(WeaponTemplateResult template, uint weaponId = WeaponId)
    {
        var data = new FakeNpcAttackDataSource();
        data.WithWeapon(weaponId, template);
        data.Ammos[AmmoId] = new Ammo
        {
            Id = AmmoId,
            ProjectileSpeed = 40f,
            ImpactRadius = 0.5f,
            MaxRadius = 1.5f,
            Flags = 1,
        };
        return data;
    }

    private static TurretWeapon Row(uint weaponId, uint id, float originX = 0f, float originY = 0f, float originZ = 0f)
    {
        var row = new TurretWeapon
        {
            TurretTypeId = TurretTypeId,
            WeaponId = weaponId,
            Id = id,
        };

        if (originX != 0f || originY != 0f || originZ != 0f)
        {
            SetPhysicalOrigin(row, originX, originY, originZ);
        }

        return row;
    }

    /// <summary>
    ///     Writes a FauFau vector onto <see cref="TurretWeapon.PhysicalOrigin" /> without taking a
    ///     compile-time dependency on the FauFau assembly (the tests project references GameServer,
    ///     not FauFau itself).
    /// </summary>
    private static void SetPhysicalOrigin(TurretWeapon row, float x, float y, float z)
    {
        var property = typeof(TurretWeapon).GetProperty(nameof(TurretWeapon.PhysicalOrigin));
        object vector = Activator.CreateInstance(property.PropertyType);
        SetNumeric(vector, "x", x);
        SetNumeric(vector, "y", y);
        SetNumeric(vector, "z", z);
        property.SetValue(row, vector);
    }

    private static void SetNumeric(object target, string name, float value)
    {
        var type = target.GetType();
        var field = type.GetField(name, BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic);
        if (field != null)
        {
            field.SetValue(target, value);
            return;
        }

        type.GetProperty(name, BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic)
            ?.SetValue(target, value);
    }

    private static (TurretWeaponFire Fire, RecordingAiProjectileLauncher Launcher, TurretEntity Turret, GameServer.Entities.Character.CharacterEntity Gunner)
        Create(FakeNpcAttackDataSource data, IReadOnlyList<TurretWeapon> weapons)
    {
        var shard = new FakeShard();
        var gunner = FakeCharacterFactory.Create(shard);
        gunner.SetPosition(GunnerPosition);

        var turret = new TurretEntity(shard, shard.GetNextGuid(), TurretTypeId, gunner, 0, 2, 0, Vector3.Zero);
        turret.Position = TurretPosition;

        var launcher = new RecordingAiProjectileLauncher();
        var fire = new TurretWeaponFire(
            _ => weapons,
            new NpcAttackResolver(data),
            launcher);

        return (fire, launcher, turret, gunner);
    }

    [Fact]
    public void Fire_RangedWeapon_FiresOneRoundFromTheGunner()
    {
        // A minigun's template carries RoundsPerBurst > 1; the client already sends one packet per
        // projectile, so this path fires exactly one round. The gunner is the source: the turret
        // has no CharacterEntity, and hostility / the watching-client announcement both key off it.
        var data = DataWith(RangedTemplate()).WithAttribute(WeaponId, 954, 50f);
        var (fire, launcher, turret, gunner) = Create(data, [Row(WeaponId, id: 1)]);

        fire.Fire(turret, gunner, Time, Aim);

        RecordingAiProjectileLauncher.RecordedShot shot = Assert.Single(launcher.Shots);
        Assert.Equal(gunner.EntityId, shot.SourceId);
        Assert.Equal(AmmoId, shot.AmmoId);
        Assert.Equal(50, shot.Damage);
        Assert.Equal(30f, shot.Range);
        Assert.Equal(40f, shot.ProjectileSpeed);
        Assert.Equal(0.5f, shot.ImpactRadius);
        Assert.Equal(1.5f, shot.MaxRadius);
        Assert.Equal(Aim, shot.Direction, new Vector3Comparer());
    }

    [Fact]
    public void Fire_PhysicalOrigin_IsTurretPositionPlusTheRowOffset()
    {
        // Identity pose leaves PhysicalOrigin on the world axes (MuzzleHardpoint is unused:
        // dbvisualrecords::Hardpoints.Transform is not loaded). The gunner standing somewhere
        // else must not move the muzzle.
        var data = DataWith(RangedTemplate());
        var (fire, launcher, turret, gunner) = Create(data, [Row(WeaponId, id: 1, originX: 0.2f, originY: 0f, originZ: 1.3f)]);

        fire.Fire(turret, gunner, Time, Aim);

        Assert.Equal(TurretPosition + new Vector3(0.2f, 0f, 1.3f), Assert.Single(launcher.Shots).Origin);
    }

    [Fact]
    public void Fire_PoseRotation_RotatesPhysicalOrigin()
    {
        // The same Inverse(pose) convention CharacterEntity uses for a muzzle offset. Identity
        // is the case above; a yaw of 90° must move the offset off the world axes.
        var data = DataWith(RangedTemplate());
        var (fire, launcher, turret, gunner) = Create(data, [Row(WeaponId, id: 1, originX: 0.2f, originY: 0f, originZ: 1.3f)]);
        turret.Turret_ObserverView.CurrentPoseProp = new CurrentPoseStruct
        {
            Rotation = Quaternion.CreateFromAxisAngle(Vector3.UnitZ, MathF.PI / 2f),
            ShortTime = 0,
        };

        fire.Fire(turret, gunner, Time, Aim);

        Vector3 expected = TurretPosition + TurretWeaponFire.RotateByPose(new Vector3(0.2f, 0f, 1.3f), turret);
        Assert.Equal(expected, Assert.Single(launcher.Shots).Origin);
        Assert.NotEqual(TurretPosition + new Vector3(0.2f, 0f, 1.3f), launcher.Shots[0].Origin);
    }

    [Fact]
    public void Fire_ZeroPhysicalOrigin_FallsBackToTheGunnersProjectileOrigin()
    {
        var data = DataWith(RangedTemplate());
        var (fire, launcher, turret, gunner) = Create(data, [Row(WeaponId, id: 1)]);

        fire.Fire(turret, gunner, Time, Aim);

        Assert.Equal(gunner.GetProjectileOrigin(Time, Aim), Assert.Single(launcher.Shots).Origin);
    }

    [Fact]
    public void Fire_DualWeaponTurret_FiresEachRangedRow()
    {
        // 21 turret types in prod-1962 carry two weapon rows. The packet does not name a
        // hardpoint, so every ranged row of the type fires once, each from its own
        // PhysicalOrigin. Different ammo on the second row is how the test proves both left.
        const ushort secondAmmoId = 923;
        var secondTemplate = RangedTemplate();
        secondTemplate.AmmoId = secondAmmoId;
        var data = DataWith(RangedTemplate()).WithWeapon(SecondWeaponId, secondTemplate);
        data.Ammos[secondAmmoId] = new Ammo { Id = secondAmmoId, ProjectileSpeed = 50f, ImpactRadius = 1f, MaxRadius = 2f };
        var weapons = new List<TurretWeapon>
        {
            Row(WeaponId, id: 1, originX: 0.2f, originY: 0f, originZ: 1.3f),
            Row(SecondWeaponId, id: 2, originX: -0.2f, originY: 0f, originZ: 1.3f),
        };
        var (fire, launcher, turret, gunner) = Create(data, weapons);

        fire.Fire(turret, gunner, Time, Aim);

        Assert.Equal(2, launcher.Shots.Count);
        Assert.Equal(AmmoId, launcher.Shots[0].AmmoId);
        Assert.Equal(secondAmmoId, launcher.Shots[1].AmmoId);
        Assert.Equal(TurretPosition + new Vector3(0.2f, 0f, 1.3f), launcher.Shots[0].Origin);
        Assert.Equal(TurretPosition + new Vector3(-0.2f, 0f, 1.3f), launcher.Shots[1].Origin);
    }

    [Fact]
    public void Fire_MeleeThenRanged_FiresOnlyTheRangedRow()
    {
        const ushort secondAmmoId = 923;
        var secondTemplate = RangedTemplate();
        secondTemplate.AmmoId = secondAmmoId;
        var data = DataWith(MeleeTemplate()).WithWeapon(SecondWeaponId, secondTemplate);
        data.Ammos[secondAmmoId] = new Ammo { Id = secondAmmoId, ProjectileSpeed = 50f, ImpactRadius = 1f, MaxRadius = 2f };
        var weapons = new List<TurretWeapon>
        {
            Row(WeaponId, id: 1),
            Row(SecondWeaponId, id: 2),
        };
        var (fire, launcher, turret, gunner) = Create(data, weapons);

        fire.Fire(turret, gunner, Time, Aim);

        Assert.Equal(secondAmmoId, Assert.Single(launcher.Shots).AmmoId);
    }

    [Fact]
    public void Fire_GunnerLevel_ScalesDamagePerRound()
    {
        // The resolver stays at level 1 / monsterId 0; the gunner's battleframe progression
        // grows the per-round number through the same curve a handheld shot uses.
        var data = DataWith(RangedTemplate()).WithAttribute(WeaponId, 954, 50f);
        var (fire, launcher, turret, gunner) = Create(data, [Row(WeaponId, id: 1)]);
        gunner.FrameProgressionLevel = 10;

        fire.Fire(turret, gunner, Time, Aim);

        int expected = WeaponDamageMath.RoundDamage(50f * WeaponDamageMath.DamageLevelScale(10));
        Assert.Equal(expected, Assert.Single(launcher.Shots).Damage);
        Assert.NotEqual(50, expected);
    }

    [Fact]
    public void Fire_Ammo_ReplicatesRemainingAndIndices()
    {
        // Controller AmmoData.Ammo is remaining rounds; observer AmmoIndex is the slot
        // indices. The turret protocol has no ReloadWeapon, so an empty clip refills
        // before the round rather than blocking the packet.
        var data = DataWith(RangedTemplate());
        var (fire, launcher, turret, gunner) = Create(data, [Row(WeaponId, id: 1)]);

        fire.Fire(turret, gunner, Time, Aim);

        Assert.Equal(new ushort[] { 19 }, turret.AmmoRemaining);
        Assert.Equal(new ushort[] { 19 }, turret.Turret_BaseController.AmmoProp.Ammo);
        Assert.Equal(new ushort[] { 0 }, turret.Turret_ObserverView.AmmoProp.AmmoIndex);

        turret.SetAmmo([1]);
        fire.Fire(turret, gunner, Time, Aim);
        Assert.Equal((ushort)0, turret.AmmoRemaining[0]);

        fire.Fire(turret, gunner, Time, Aim);
        Assert.Equal((ushort)19, turret.AmmoRemaining[0]);
        Assert.Equal(3, launcher.Shots.Count);
    }

    [Fact]
    public void Fire_EmptyWeaponId_DoesNotFire()
    {
        var data = DataWith(RangedTemplate());
        var (fire, launcher, turret, gunner) = Create(data, [Row(weaponId: 0, id: 1)]);

        fire.Fire(turret, gunner, Time, Aim);

        Assert.Empty(launcher.Shots);
    }

    [Fact]
    public void Fire_MeleeWeapon_DoesNotFire()
    {
        var data = DataWith(MeleeTemplate());
        var (fire, launcher, turret, gunner) = Create(data, [Row(WeaponId, id: 1)]);

        fire.Fire(turret, gunner, Time, Aim);

        Assert.Empty(launcher.Shots);
    }

    [Fact]
    public void Fire_UnknownTurretType_DoesNotFire()
    {
        var data = DataWith(RangedTemplate());
        var (fire, launcher, turret, gunner) = Create(data, Array.Empty<TurretWeapon>());

        fire.Fire(turret, gunner, Time, Aim);

        Assert.Empty(launcher.Shots);
    }

    [Fact]
    public void Fire_DegenerateAim_DoesNotFire()
    {
        var data = DataWith(RangedTemplate());
        var (fire, launcher, turret, gunner) = Create(data, [Row(WeaponId, id: 1)]);

        fire.Fire(turret, gunner, Time, Vector3.Zero);

        Assert.Empty(launcher.Shots);
    }

    [Fact]
    public void Fire_SpreadCone_ScattersTheRoundTheSameWayAnNpcWould()
    {
        var template = RangedTemplate();
        template.MinSpread = 2f;
        template.MaxSpread = 8f;
        template.StartingSpread = 0.5f;
        var data = DataWith(template);
        var (fire, launcher, turret, gunner) = Create(data, [Row(WeaponId, id: 1)]);

        fire.Fire(turret, gunner, Time, Aim);

        Vector3 expected = NpcAttackSpreadMath.Apply(
            Aim,
            spreadPct: 5f,
            Time,
            slotIndex: 2,
            round: 0,
            lastSpreadDirection: Vector3.Zero,
            lastSpreadTime: Time);

        Assert.Equal(expected, Assert.Single(launcher.Shots).Direction, new Vector3Comparer());
        Assert.NotEqual(Aim, launcher.Shots[0].Direction, new Vector3Comparer());
    }

    [Fact]
    public void Fire_NullGunner_DoesNotFire()
    {
        var data = DataWith(RangedTemplate());
        var (fire, launcher, turret, _) = Create(data, [Row(WeaponId, id: 1)]);

        fire.Fire(turret, null, Time, Aim);

        Assert.Empty(launcher.Shots);
    }

    private sealed class Vector3Comparer : IEqualityComparer<Vector3>
    {
        public bool Equals(Vector3 a, Vector3 b) => (a - b).LengthSquared() < 0.0001f;

        public int GetHashCode(Vector3 value) => value.GetHashCode();
    }
}
