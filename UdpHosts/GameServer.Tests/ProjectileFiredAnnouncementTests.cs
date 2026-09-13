using System;
using System.Numerics;
using Aero.Gen;
using AeroMessages.GSS.Character;
using AeroMessages.GSS.Character.Event;
using GameServer.Entities.Character;
using GameServer.StaticDB.Records.dbitems;
using GameServer.Systems.Ai;
using GameServer.Systems.Combat;
using GameServer.Tests.Fakes;
using Xunit;

namespace GameServer.Tests;

/// <summary>
///     NPC ranged attacks fire through <c>ProjectileSim</c>, which is server-only. Watching clients
///     only draw the tracer if the server sends <c>WeaponProjectileFired</c> - the same event a
///     player's fire path echoes to the shooter and now also announces to other watchers. These
///     tests pin that announcement: a scoped watcher receives it, a player's own client is skipped
///     when they already have the echo, and a character nobody is watching does not throw.
/// </summary>
public class ProjectileFiredAnnouncementTests
{
    [Fact]
    public void SendToWatchers_TellsAScopedClientTheShotLeft()
    {
        var (shard, npc, player) = CreateScopedNpc();
        player.FlushAttachedChannels();
        player.SentPackets.Clear();

        var aim = Vector3.Normalize(new Vector3(1f, 0f, 0f));
        ProjectileFiredAnnouncement.SendToWatchers(shard, npc, aim);
        player.FlushAttachedChannels();

        Assert.True(
            AnyPacketContains(player.SentPackets, SerializeFired(aim, Vector3.Zero, haveVelocity: false, shard.CurrentShortTime)),
            "a watching client must receive WeaponProjectileFired aimed along the shot");
    }

    [Fact]
    public void SendToWatchers_CarriesTheShootersVelocityWhenItIsMoving()
    {
        var (shard, npc, player) = CreateScopedNpc();
        npc.Velocity = new Vector3(3f, 4f, 0f);
        player.FlushAttachedChannels();
        player.SentPackets.Clear();

        var aim = Vector3.Normalize(new Vector3(0f, 1f, 0f));
        ProjectileFiredAnnouncement.SendToWatchers(shard, npc, aim);
        player.FlushAttachedChannels();

        Assert.True(
            AnyPacketContains(player.SentPackets, SerializeFired(aim, npc.Velocity, haveVelocity: true, shard.CurrentShortTime)),
            "a moving shooter must put its velocity on WeaponProjectileFired");
    }

    [Fact]
    public void SendToWatchers_ExceptOwner_TellsAScopedObserverAndSkipsTheShooter()
    {
        // A player's fire path already echoes WeaponProjectileFired on ReliableGss. The
        // announcement to other watchers must not send a second copy to the shooter, or
        // they draw the tracer twice. A seated turret gunner has no echo, so the default
        // (exceptOwner false) still includes the owner's client - pinned below.
        var (shard, shooter, shooterPlayer, observer) = CreateScopedPlayerPair();
        shooterPlayer.FlushAttachedChannels();
        observer.FlushAttachedChannels();
        shooterPlayer.SentPackets.Clear();
        observer.SentPackets.Clear();

        var aim = Vector3.Normalize(new Vector3(1f, 0f, 0f));
        var message = new WeaponProjectileFired
        {
            ShortTime = 1234,
            Aim = aim,
            HaveShooterVelocity = 0,
            ShooterVelocity = default,
        };

        ProjectileFiredAnnouncement.SendToWatchers(shard, shooter, message, exceptOwner: true);
        shooterPlayer.FlushAttachedChannels();
        observer.FlushAttachedChannels();

        var packed = SerializeFired(aim, Vector3.Zero, haveVelocity: false, shortTime: 1234);
        Assert.True(
            AnyPacketContains(observer.SentPackets, packed),
            "another player watching the shooter must receive WeaponProjectileFired");
        Assert.False(
            AnyPacketContains(shooterPlayer.SentPackets, packed),
            "the shooter already has the ReliableGss echo and must not get a second copy");
    }

    [Fact]
    public void SendToWatchers_IncludesTheShootersOwnClientWhenTheyHaveNoEcho()
    {
        var (shard, shooter, shooterPlayer, observer) = CreateScopedPlayerPair();
        shooterPlayer.FlushAttachedChannels();
        observer.FlushAttachedChannels();
        shooterPlayer.SentPackets.Clear();
        observer.SentPackets.Clear();

        var aim = Vector3.Normalize(new Vector3(0f, 1f, 0f));
        ProjectileFiredAnnouncement.SendToWatchers(shard, shooter, aim);
        shooterPlayer.FlushAttachedChannels();
        observer.FlushAttachedChannels();

        var packed = SerializeFired(aim, Vector3.Zero, haveVelocity: false, shortTime: shard.CurrentShortTime);
        Assert.True(
            AnyPacketContains(shooterPlayer.SentPackets, packed),
            "a seated turret gunner (no CombatController echo) must still receive the announcement");
        Assert.True(
            AnyPacketContains(observer.SentPackets, packed),
            "an observer of that shot must receive it too");
    }

    [Fact]
    public void SendToWatchers_IsANoOpWhenNobodyIsWatching()
    {
        var shard = new FakeShard { CurrentTimeLong = 60_000 };
        var npc = FakeCharacterFactory.Create(shard);
        npc.SetCharacterState(CharacterStateData.CharacterStatus.Living, 60_000);

        ProjectileFiredAnnouncement.SendToWatchers(shard, npc, Vector3.UnitX);
        ProjectileFiredAnnouncement.SendToWatchers(null, npc, Vector3.UnitX);
        ProjectileFiredAnnouncement.SendToWatchers(shard, null, Vector3.UnitX);
        ProjectileFiredAnnouncement.SendToWatchers(shard, npc, Vector3.Zero);
    }

    [Fact]
    public void ShardAiProjectileLauncher_AnnouncesEachRoundToWatchers()
    {
        // The production launcher is what a live NPC fires through. RecordingAiProjectileLauncher
        // (the engine tests' fake) does not announce, because those tests have no watching client.
        var (shard, npc, player) = CreateScopedNpc();
        player.FlushAttachedChannels();
        player.SentPackets.Clear();

        var aim = Vector3.Normalize(new Vector3(1f, 0f, 0.1f));
        var ammo = new Ammo { Id = 922, ProjectileSpeed = 40f, ImpactRadius = 0.5f, MaxRadius = 1.5f };
        new ShardAiProjectileLauncher(shard).FireRangedAttack(
            npc,
            trace: 1,
            origin: npc.Position + new Vector3(0f, 0f, 1.62f),
            direction: aim,
            ammo,
            range: 25f,
            projectileSpeed: 40f,
            impactRadius: 0.5f,
            maxRadius: 1.5f,
            damage: 100);

        player.FlushAttachedChannels();

        Assert.True(
            AnyPacketContains(player.SentPackets, SerializeFired(aim, Vector3.Zero, haveVelocity: false, shard.CurrentShortTime)),
            "ShardAiProjectileLauncher must announce the round, not only simulate it");
    }

    private static (FakeShard Shard, CharacterEntity Shooter, FakeNetworkPlayer ShooterPlayer, FakeNetworkPlayer Observer) CreateScopedPlayerPair()
    {
        var shard = new FakeShard { CurrentTimeLong = 60_000 };
        var shooter = FakeCharacterFactory.Create(shard);
        shooter.SetCharacterState(CharacterStateData.CharacterStatus.Living, 60_000);

        var shooterPlayer = new FakeNetworkPlayer(shard) { CharacterEntity = shooter, SocketId = 1 };
        shooterPlayer.AttachRealChannels();
        shard.Clients[shooterPlayer.SocketId] = shooterPlayer;
        shard.EntityMan.Add(shooter.EntityId, shooter);
        // Player is set after Add so ScopeIn skips the controller-keyframe path (no loadout
        // on a fake character). exceptOwner still needs the back-reference.
        shooter.Player = shooterPlayer;

        var observerCharacter = FakeCharacterFactory.Create(shard);
        observerCharacter.SetCharacterState(CharacterStateData.CharacterStatus.Living, 60_000);
        var observer = new FakeNetworkPlayer(shard) { CharacterEntity = observerCharacter, SocketId = 2 };
        observer.AttachRealChannels();
        shard.Clients[observer.SocketId] = observer;
        shard.EntityMan.ScopeIn(observer, shooter);

        return (shard, shooter, shooterPlayer, observer);
    }

    private static (FakeShard Shard, CharacterEntity Npc, FakeNetworkPlayer Player) CreateScopedNpc()
    {
        var shard = new FakeShard { CurrentTimeLong = 60_000 };
        var playerCharacter = FakeCharacterFactory.Create(shard);
        playerCharacter.SetCharacterState(CharacterStateData.CharacterStatus.Living, 60_000);

        var player = new FakeNetworkPlayer(shard) { CharacterEntity = playerCharacter };
        player.AttachRealChannels();
        shard.Clients[player.SocketId] = player;

        var npc = FakeCharacterFactory.Create(shard);
        npc.SetCharacterState(CharacterStateData.CharacterStatus.Living, 60_000);
        shard.EntityMan.Add(npc.EntityId, npc);

        return (shard, npc, player);
    }

    private static byte[] SerializeFired(Vector3 aim, Vector3 velocity, bool haveVelocity, ushort shortTime)
    {
        var message = new WeaponProjectileFired
        {
            ShortTime = shortTime,
            Aim = aim,
            HaveShooterVelocity = haveVelocity ? (byte)1 : (byte)0,
            ShooterVelocity = haveVelocity ? velocity : default,
        };
        var packed = new byte[message.GetPackedSize()];
        message.Pack(packed);
        return packed;
    }

    private static bool AnyPacketContains(System.Collections.Generic.IEnumerable<System.Memory<byte>> packets, byte[] needle)
    {
        foreach (var packet in packets)
        {
            if (Contains(packet.Span, needle))
            {
                return true;
            }
        }

        return false;
    }

    private static bool Contains(System.ReadOnlySpan<byte> haystack, System.ReadOnlySpan<byte> needle)
    {
        for (var start = 0; start + needle.Length <= haystack.Length; start++)
        {
            if (haystack.Slice(start, needle.Length).SequenceEqual(needle))
            {
                return true;
            }
        }

        return false;
    }
}
