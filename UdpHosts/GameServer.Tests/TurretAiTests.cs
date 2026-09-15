using System;
using System.Linq;
using System.Numerics;
using System.Threading;
using AeroMessages.GSS.Character;
using GameServer.Data;
using GameServer.Entities.Character;
using GameServer.Entities.Deployable;
using GameServer.Entities.Turret;
using GameServer.StaticDB;
using GameServer.StaticDB.Records.dbcharacter;
using GameServer.StaticDB.Records.dbitems;
using GameServer.Systems.Ai;
using GameServer.Systems.Combat;
using GameServer.Tests.Fakes;
using Xunit;

namespace GameServer.Tests;

public class TurretAiTests
{
    private const uint TurretTypeId = 21;
    private const uint WeaponId = 30_025;
    private const ushort AmmoId = 922;
    private const ulong FirstTick = 60_000;
    private const ulong Step = 50;

    private static readonly CharacterStateData.CharacterStatus Living = CharacterStateData.CharacterStatus.Living;

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

    private static FakeNpcAttackDataSource Data(bool parabolic = false)
    {
        var data = new FakeNpcAttackDataSource();
        data.WithWeapon(WeaponId, RangedTemplate());
        data.Ammos[AmmoId] = new Ammo
        {
            Id = AmmoId,
            ProjectileSpeed = 40f,
            ImpactRadius = 0.5f,
            MaxRadius = 1.5f,
            // 1 = SimulationMode.Linear, 2 = SimulationMode.Parabolic.
            Flags = parabolic ? 2u : 1u,
            Gravity = parabolic ? 9.81f : 0f,
        };
        data.WithAttribute(WeaponId, 954, 50f);
        return data;
    }

    private static CharacterEntity CreateLiving(FakeShard shard, Vector3 position)
    {
        var character = FakeCharacterFactory.Create(shard);
        character.SetCharacterState(Living, 0);
        character.SetMaxHealth(100_000, resetCurrent: true);
        character.SetPosition(position);
        return character;
    }

    private static (FakeShard Shard, TurretAi Ai, RecordingAiProjectileLauncher Shots, TurretEntity Turret, CharacterEntity Owner, CharacterEntity Target)
        Create(Vector3 targetPosition, IAiHostility hostility = null, Vector3? muzzleOffset = null, bool parabolic = false)
    {
        var shard = new FakeShard();
        var owner = CreateLiving(shard, Vector3.Zero);
        shard.Entities[owner.EntityId] = owner;

        var turret = new TurretEntity(shard, shard.GetNextGuid(), TurretTypeId, owner, 0, 2, 0, Vector3.Zero);
        turret.Position = Vector3.Zero;
        shard.Entities[turret.EntityId] = turret;

        var target = CreateLiving(shard, targetPosition);
        shard.Entities[target.EntityId] = target;
        var client = new FakeNetworkPlayer(shard) { CharacterEntity = target, SocketId = 1 };
        shard.Clients[client.SocketId] = client;

        // The barrel's muzzle hardpoint: with an offset the shot leaves from turret base + the
        // offset, like a real turret whose barrel sits metres above its base.
        var weapon = new TurretWeapon
        {
            TurretTypeId = TurretTypeId,
            WeaponId = WeaponId,
            Id = 1,
            MuzzleHardpoint = muzzleOffset.HasValue ? "Muzzle" : null,
        };

        var shots = new RecordingAiProjectileLauncher();
        var fire = new TurretWeaponFire(
            _ => [weapon],
            new NpcAttackResolver(Data(parabolic)),
            shots,
            hardpointOffset: muzzleOffset.HasValue ? _ => muzzleOffset.Value : null);
        var ai = new TurretAi(shard, hostility ?? new AlwaysHostileAiHostility(), fire);
        Assert.True(ai.Register(turret));
        return (shard, ai, shots, turret, owner, target);
    }

    [Fact]
    public void UnmannedTurret_FiresAtAHostileInRange()
    {
        var (_, ai, shots, turret, owner, target) = Create(new Vector3(20f, 0f, 0f));

        ai.Tick(FirstTick);

        RecordingAiProjectileLauncher.RecordedShot shot = Assert.Single(shots.Shots);
        Assert.Equal(owner.EntityId, shot.SourceId);
        Assert.Equal(AmmoId, shot.AmmoId);
        Assert.Equal(50, shot.Damage);
        Assert.True(shot.Direction.X > 0.9f, "the turret should aim at the target, not at the floor");
        Assert.Equal(FirstTick, turret.Turret_ObserverView.WeaponBurstFiredProp);
        Assert.True(
            turret.Turret_ObserverView.CurrentPoseProp.Rotation != Quaternion.Identity,
            "the turret should yaw toward the target");
        Assert.Equal(100_000, target.CurrentHealth);
    }

    [Fact]
    public void UnmannedTurret_AimsAtTheMiddleOfTheTargetModel()
    {
        // The barrel sits 2 m above the base. The AI must aim from that muzzle at the middle
        // of the target's model (0.9 m above its feet) - the old code aimed from the turret's
        // feet at a fixed 1.4 m eye height, which for a high barrel shot that up and over the
        // model.
        var (_, ai, shots, _, _, _) = Create(
            new Vector3(20f, 0f, 0f),
            muzzleOffset: new Vector3(0f, 0f, 2f));

        ai.Tick(FirstTick);

        var shot = Assert.Single(shots.Shots);

        // The shot leaves from the muzzle, not from the base. The barrel's pose is written
        // before the shot is handed out, so the origin the shot reports is the muzzle rotated
        // by the new pose - a few centimetres off the pre-rotation point, but metres from
        // the base the old code fired from.
        Assert.True(Vector3.Distance(shot.Origin, new Vector3(0f, 0f, 2f)) < 0.2f,
            $"the shot should leave from the 2 m muzzle, it left from {shot.Origin}");

        // The aim itself was solved from the muzzle: straight from (0, 0, 2) at (20, 0, 0.9).
        // The old aim (from the feet at (20, 0, 1.4)) ran at Z ≈ +0.070; this one runs below
        // horizontal at Z ≈ −0.055.
        Assert.Equal(0.9985f, shot.Direction.X, 4);
        Assert.Equal(-0.0549f, shot.Direction.Z, 4);
    }

    [Fact]
    public void UnmannedTurret_LeadsAMovingTarget()
    {
        // The target runs +Y at 10 m/s. At the ~0.5 s flight time it would move ~5 m, but the
        // lead is clamped at 3 m, so the aim point settles at (15, 13) instead of (15, 10).
        var (_, ai, shots, _, _, target) = Create(
            new Vector3(15f, 10f, 0f),
            muzzleOffset: new Vector3(0f, 0f, 2f));
        target.Velocity = new Vector3(0f, 10f, 0f);

        ai.Tick(FirstTick);

        var shot = Assert.Single(shots.Shots);

        // Aiming at (15, 13): the direction's Y/X ratio is 13/15 ≈ 0.867, whereas the unled
        // aim at (15, 10) is 10/15 ≈ 0.667.
        Assert.Equal(0.867f, shot.Direction.Y / shot.Direction.X, 3);
    }

    [Fact]
    public void UnmannedTurret_CompensatesParabolicDrop()
    {
        // A parabolic row (SimulationMode.Parabolic) falls 0.5·g·t² over its flight, so the AI
        // must launch on the arc that ends at the middle of the model, not along the straight
        // line to it. Over 20 m the round is in the air ~0.5 s and drops ~1.2 m, so the shot
        // has to go slightly up to land at 0.9 m from a 2 m muzzle.
        var (_, ai, shots, _, _, _) = Create(
            new Vector3(20f, 0f, 0f),
            muzzleOffset: new Vector3(0f, 0f, 2f),
            parabolic: true);

        ai.Tick(FirstTick);

        var shot = Assert.Single(shots.Shots);
        Assert.True(
            shot.Direction.Z > 0f,
            "a straight line from (0, 0, 2) to (20, 0, 0.9) points down; the drop-compensated shot must point up");

        // Re-run the shot through the same parabola ProjectileSim integrates and check where
        // it is when its XY reaches the target: it must be the middle of the model. The
        // muzzle's pose rotation offsets the launch a few centimetres sideways, so the Y
        // assertion allows for it while X and Z stay tight.
        Vector3 velocity = shot.Direction * shot.ProjectileSpeed;
        float t = 20f / velocity.X;
        Vector3 landed = shot.Origin + velocity * t + new Vector3(0f, 0f, -0.5f * 9.81f * t * t);
        Assert.Equal(20f, landed.X, 2);
        Assert.True(MathF.Abs(landed.Y) < 0.05f, $"the shot should stay on the target's line, it landed at {landed}");
        Assert.Equal(0.9f, landed.Z, 2);
    }

    [Fact]
    public void UnmannedTurret_DoesNotFireAtATargetInAnotherZone()
    {
        var (shard, ai, shots, _, _, _) = Create(new Vector3(20f, 0f, 0f));
        ((FakeNetworkPlayer)shard.Clients.Values.Single()).CurrentZone = new Zone { ID = 1030, Name = "Sertao" };

        ai.Tick(FirstTick);

        Assert.Empty(shots.Shots);
    }

    [Fact]
    public void UnmannedTurret_DoesNotFireAtATargetPastItsWeaponRange()
    {
        // AttackRange is the weapon's 30 m, not the NPC AggroRadius of 55 m.
        var (_, ai, shots, _, _, _) = Create(new Vector3(40f, 0f, 0f));

        ai.Tick(FirstTick);

        Assert.Empty(shots.Shots);
    }

    [Fact]
    public void UnmannedTurret_DoesNotShootItsOwner()
    {
        var (shard, ai, shots, _, owner, _) = Create(new Vector3(20f, 0f, 0f));
        shard.Clients.Clear();
        var client = new FakeNetworkPlayer(shard) { CharacterEntity = owner, SocketId = 1 };
        shard.Clients[client.SocketId] = client;

        ai.Tick(FirstTick);

        Assert.Empty(shots.Shots);
    }

    [Fact]
    public void SeatedTurret_DoesNotAutoFire()
    {
        var (shard, ai, shots, turret, owner, _) = Create(new Vector3(20f, 0f, 0f));
        turret.ControllingPlayer = new FakeNetworkPlayer(shard) { CharacterEntity = owner };

        ai.Tick(FirstTick);

        Assert.Empty(shots.Shots);
    }

    [Fact]
    public void UnmannedTurret_RespectsItsWeaponCadence()
    {
        // Empty behaviour + ms_per_burst 100 resolves to MinimumAttackIntervalMs 250.
        var (_, ai, shots, _, _, _) = Create(new Vector3(20f, 0f, 0f));

        ai.Tick(FirstTick);
        Assert.Single(shots.Shots);

        ai.Tick(FirstTick + Step);
        Assert.Single(shots.Shots);

        ai.Tick(FirstTick + 250);
        Assert.Equal(2, shots.Shots.Count);
    }

    [Fact]
    public void UnmannedTurret_OnADeadDeployable_DoesNotFire()
    {
        var (shard, _, shots, _, owner, target) = Create(new Vector3(20f, 0f, 0f));
        var deployable = new DeployableEntity(shard, shard.GetNextGuid(), 1, 0, owner);
        deployable.MarkDead();
        var turret = new TurretEntity(shard, shard.GetNextGuid(), TurretTypeId, deployable, 0, 2, 0, Vector3.Zero);
        turret.Position = Vector3.Zero;
        shard.Entities[turret.EntityId] = turret;

        var fire = new TurretWeaponFire(
            _ => [new TurretWeapon { TurretTypeId = TurretTypeId, WeaponId = WeaponId, Id = 1 }],
            new NpcAttackResolver(Data()),
            shots);
        var ai = new TurretAi(shard, new AlwaysHostileAiHostility(), fire);
        ai.Register(turret);
        shots.Shots.Clear();

        ai.Tick(FirstTick);

        Assert.Empty(shots.Shots);
        Assert.Equal(100_000, target.CurrentHealth);
    }

    [Fact]
    public void UnmannedTurret_WithoutASourceCharacter_DoesNotFire()
    {
        var (shard, _, shots, _, _, target) = Create(new Vector3(20f, 0f, 0f));
        var deployable = new DeployableEntity(shard, shard.GetNextGuid(), 1, 0, owner: null);
        var turret = new TurretEntity(shard, shard.GetNextGuid(), TurretTypeId, deployable, 0, 2, 0, Vector3.Zero);
        turret.Position = Vector3.Zero;
        shard.Entities[turret.EntityId] = turret;

        var fire = new TurretWeaponFire(
            _ => [new TurretWeapon { TurretTypeId = TurretTypeId, WeaponId = WeaponId, Id = 1 }],
            new NpcAttackResolver(Data()),
            shots);
        var ai = new TurretAi(shard, new AlwaysHostileAiHostility(), fire);
        ai.Register(turret);
        shots.Shots.Clear();

        ai.Tick(FirstTick);

        Assert.Empty(shots.Shots);
        Assert.Null(TurretAi.ResolveSource(turret));
        Assert.Equal(100_000, target.CurrentHealth);
    }

    [Fact]
    public void UnmannedTurret_OnADeployable_FiresAsTheOwner()
    {
        var (shard, _, shots, _, owner, _) = Create(new Vector3(20f, 0f, 0f));
        var deployable = new DeployableEntity(shard, shard.GetNextGuid(), 1, 0, owner);
        var turret = new TurretEntity(shard, shard.GetNextGuid(), TurretTypeId, deployable, 0, 2, 0, Vector3.Zero);
        turret.Position = Vector3.Zero;
        shard.Entities[turret.EntityId] = turret;

        var fire = new TurretWeaponFire(
            _ => [new TurretWeapon { TurretTypeId = TurretTypeId, WeaponId = WeaponId, Id = 1 }],
            new NpcAttackResolver(Data()),
            shots);
        var ai = new TurretAi(shard, new AlwaysHostileAiHostility(), fire);
        ai.Register(turret);
        shots.Shots.Clear();

        ai.Tick(FirstTick);

        Assert.Equal(owner.EntityId, Assert.Single(shots.Shots).SourceId);
        Assert.Same(owner, TurretAi.ResolveSource(turret));
    }

    [Fact]
    public void UnmannedTurret_FiresOnAShardWithNoNpcs()
    {
        // Regression: AiEngine.Tick used to return before any work when _brains was empty,
        // so a shard with turrets and no mobs never fired.
        var shard = new FakeShard();
        var owner = CreateLiving(shard, Vector3.Zero);
        shard.Entities[owner.EntityId] = owner;
        var turret = new TurretEntity(shard, shard.GetNextGuid(), TurretTypeId, owner, 0, 2, 0, Vector3.Zero);
        turret.Position = Vector3.Zero;
        shard.Entities[turret.EntityId] = turret;
        var target = CreateLiving(shard, new Vector3(20f, 0f, 0f));
        shard.Entities[target.EntityId] = target;
        shard.Clients[1] = new FakeNetworkPlayer(shard) { CharacterEntity = target, SocketId = 1 };

        var shots = new RecordingAiProjectileLauncher();
        var fire = new TurretWeaponFire(
            _ => [new TurretWeapon { TurretTypeId = TurretTypeId, WeaponId = WeaponId, Id = 1 }],
            new NpcAttackResolver(Data()),
            shots);
        shard.AI = new AiEngine(
            shard,
            shard.EventBus,
            hostility: new AlwaysHostileAiHostility(),
            projectileLauncher: shots,
            turretFire: fire);
        Assert.True(shard.AI.RegisterTurret(turret));
        Assert.Equal(0, shard.AI.TrackedCount);

        shard.AI.Tick(Step, FirstTick, CancellationToken.None);

        Assert.Single(shots.Shots);
    }

    [Fact]
    public void DisabledEngine_LeavesTurretsAlone()
    {
        var shard = new FakeShard();
        var owner = CreateLiving(shard, Vector3.Zero);
        shard.Entities[owner.EntityId] = owner;
        var turret = new TurretEntity(shard, shard.GetNextGuid(), TurretTypeId, owner, 0, 2, 0, Vector3.Zero);
        turret.Position = Vector3.Zero;
        shard.Entities[turret.EntityId] = turret;
        var target = CreateLiving(shard, new Vector3(20f, 0f, 0f));
        shard.Entities[target.EntityId] = target;
        shard.Clients[1] = new FakeNetworkPlayer(shard) { CharacterEntity = target, SocketId = 1 };

        var shots = new RecordingAiProjectileLauncher();
        var fire = new TurretWeaponFire(
            _ => [new TurretWeapon { TurretTypeId = TurretTypeId, WeaponId = WeaponId, Id = 1 }],
            new NpcAttackResolver(Data()),
            shots);
        shard.AI = new AiEngine(
            shard,
            shard.EventBus,
            hostility: new AlwaysHostileAiHostility(),
            projectileLauncher: shots,
            turretFire: fire);
        shard.AI.RegisterTurret(turret);
        shard.AI.Enabled = false;

        shard.AI.Tick(Step, FirstTick, CancellationToken.None);

        Assert.Empty(shots.Shots);
    }
}
