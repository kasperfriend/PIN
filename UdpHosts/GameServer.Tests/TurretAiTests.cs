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

    private static FakeNpcAttackDataSource Data()
    {
        var data = new FakeNpcAttackDataSource();
        data.WithWeapon(WeaponId, RangedTemplate());
        data.Ammos[AmmoId] = new Ammo
        {
            Id = AmmoId,
            ProjectileSpeed = 40f,
            ImpactRadius = 0.5f,
            MaxRadius = 1.5f,
            Flags = 1,
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
        Create(Vector3 targetPosition, IAiHostility hostility = null)
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

        var shots = new RecordingAiProjectileLauncher();
        var fire = new TurretWeaponFire(
            _ => [new TurretWeapon { TurretTypeId = TurretTypeId, WeaponId = WeaponId, Id = 1 }],
            new NpcAttackResolver(Data()),
            shots);
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
    public void UnmannedTurret_DoesNotFireAtATargetInAnotherZone()
    {
        var (shard, ai, shots, _, _, _) = Create(new Vector3(20f, 0f, 0f));
        shard.Clients.Values.Single().CurrentZone = new Zone { ID = 1030, Name = "Sertao" };

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
