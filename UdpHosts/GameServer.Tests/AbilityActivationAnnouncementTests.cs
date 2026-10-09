using System.Collections.Generic;
using Aero.Gen;
using AeroMessages.GSS.Character;
using AeroMessages.GSS.Character.Event;
using GameServer.Entities.Character;
using GameServer.Entities.Deployable;
using GameServer.Systems.Combat;
using GameServer.Tests.Fakes;
using Xunit;

namespace GameServer.Tests;

/// <summary>
///     <c>AbilityActivated</c> is what makes a client run the client-environment half of an ability
///     chain (the <c>apttf::</c> animation/particle/beam/camera rows the server no-ops on purpose).
///     The character combat controller used to send it to the acting player alone, so no ability was
///     ever visible to anybody but its caster - and an NPC's ability, which has no combat controller
///     echo at all, was invisible to everyone. These tests pin the announcement: a scoped watcher
///     hears the cast, a player's own client is skipped because the combat controller already
///     acknowledged it on ReliableGss, and non-character casters stay unannounced.
/// </summary>
public class AbilityActivationAnnouncementTests
{
    [Fact]
    public void SendToWatchers_TellsAScopedClientAnNpcCast()
    {
        var (shard, npc, player) = CreateScopedNpc();
        player.FlushAttachedChannels();
        player.SentPackets.Clear();

        AbilityActivationAnnouncement.SendToWatchers(shard, npc, abilityId: 35345, activationTime: 60_000);
        player.FlushAttachedChannels();

        Assert.True(
            AnyPacketContains(player.SentPackets, Serialize(35345, 60_000)),
            "a watching client must receive AbilityActivated so it plays the cast animation and effects");
    }

    [Fact]
    public void SendToWatchers_TellsAnObserverButNotTheCaster()
    {
        var (shard, caster, casterPlayer, observer) = CreateScopedPlayerPair();
        casterPlayer.FlushAttachedChannels();
        observer.FlushAttachedChannels();
        casterPlayer.SentPackets.Clear();
        observer.SentPackets.Clear();

        AbilityActivationAnnouncement.SendToWatchers(shard, caster, abilityId: 35540, activationTime: 60_000);
        casterPlayer.FlushAttachedChannels();
        observer.FlushAttachedChannels();

        var packed = Serialize(35540, 60_000);
        Assert.True(
            AnyPacketContains(observer.SentPackets, packed),
            "an observer of the caster must receive AbilityActivated");
        Assert.False(
            AnyPacketContains(casterPlayer.SentPackets, packed),
            "the caster's own client already gets the ReliableGss ack; a second copy would double the cast");
    }

    [Fact]
    public void SendToWatchers_AnnouncesAnNpcToItsOwnerToo()
    {
        // An NPC has no ReliableGss echo, so its owner must not be excluded.
        var (shard, npc, player) = CreateScopedNpc();
        player.FlushAttachedChannels();
        player.SentPackets.Clear();

        AbilityActivationAnnouncement.SendToWatchers(shard, npc, abilityId: 34668, activationTime: 61_000);
        player.FlushAttachedChannels();

        Assert.True(
            AnyPacketContains(player.SentPackets, Serialize(34668, 61_000)),
            "an NPC has no echo of its own, so every watcher including its owner must be told");
    }

    [Fact]
    public void SendToWatchers_SkipsANonCharacterCaster()
    {
        // AbilityActivated is bound to GssCharacterView.CombatController, so a deployable's own chain
        // cannot ride it, and attributing the cast to the owner would animate the wrong entity. The
        // deployable is deliberately kept out of the entity manager: registering one serializes its
        // views, which need SDB rows a fake shard does not have. What this pins is the guard itself -
        // a non-character caster is rejected before anything is looked up or sent.
        var shard = new FakeShard { CurrentTimeLong = 60_000 };
        var ownerCharacter = FakeCharacterFactory.Create(shard);
        ownerCharacter.SetCharacterState(CharacterStateData.CharacterStatus.Living, 60_000);

        var owner = new FakeNetworkPlayer(shard) { CharacterEntity = ownerCharacter, SocketId = 1 };
        owner.AttachRealChannels();
        shard.Clients[owner.SocketId] = owner;

        var deployable = new DeployableEntity(shard, shard.GetNextGuid(), type: 1, abilitySrcId: 0, ownerCharacter);
        owner.FlushAttachedChannels();
        owner.SentPackets.Clear();

        AbilityActivationAnnouncement.SendToWatchers(shard, deployable, abilityId: 35448, activationTime: 60_000);
        owner.FlushAttachedChannels();

        Assert.Empty(owner.SentPackets);
    }

    [Fact]
    public void SendToWatchers_WithoutAnEntityManagerOrCaster_IsANoOp()
    {
        var shard = new FakeShard { CurrentTimeLong = 60_000 };
        var source = FakeCharacterFactory.Create(shard);
        source.SetCharacterState(CharacterStateData.CharacterStatus.Living, 60_000);

        AbilityActivationAnnouncement.SendToWatchers(shard, source, abilityId: 0, activationTime: 60_000);
        AbilityActivationAnnouncement.SendToWatchers(null, source, abilityId: 35345, activationTime: 60_000);
        AbilityActivationAnnouncement.SendToWatchers(shard, null, abilityId: 35345, activationTime: 60_000);
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

    private static (FakeShard Shard, CharacterEntity Caster, FakeNetworkPlayer CasterPlayer, FakeNetworkPlayer Observer) CreateScopedPlayerPair()
    {
        var shard = new FakeShard { CurrentTimeLong = 60_000 };
        var caster = FakeCharacterFactory.Create(shard);
        caster.SetCharacterState(CharacterStateData.CharacterStatus.Living, 60_000);

        var casterPlayer = new FakeNetworkPlayer(shard) { CharacterEntity = caster, SocketId = 1 };
        casterPlayer.AttachRealChannels();
        shard.Clients[casterPlayer.SocketId] = casterPlayer;
        shard.EntityMan.Add(caster.EntityId, caster);

        // Player is set after Add so ScopeIn skips the controller-keyframe path (no loadout on a
        // fake character); the back-reference is what the exceptOwner check reads.
        caster.Player = casterPlayer;

        var observerCharacter = FakeCharacterFactory.Create(shard);
        observerCharacter.SetCharacterState(CharacterStateData.CharacterStatus.Living, 60_000);
        var observer = new FakeNetworkPlayer(shard) { CharacterEntity = observerCharacter, SocketId = 2 };
        observer.AttachRealChannels();
        shard.Clients[observer.SocketId] = observer;
        shard.EntityMan.ScopeIn(observer, caster);

        return (shard, caster, casterPlayer, observer);
    }

    private static byte[] Serialize(uint abilityId, uint activationTime)
    {
        var message = new AbilityActivated
        {
            ActivatedAbilityId = abilityId,
            ActivatedTime = activationTime,
            AbilityCooldownsData = new AbilityCooldownsData
            {
                ActiveCooldowns_Group1 = [],
                ActiveCooldowns_Group2 = [],
            },
        };
        var packed = new byte[message.GetPackedSize()];
        message.Pack(packed);
        return packed;
    }

    private static bool AnyPacketContains(IEnumerable<System.Memory<byte>> packets, byte[] needle)
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

    private static bool Contains(System.ReadOnlySpan<byte> haystack, byte[] needle)
    {
        if (needle.Length == 0 || haystack.Length < needle.Length)
        {
            return false;
        }

        for (var i = 0; i <= haystack.Length - needle.Length; i++)
        {
            var matched = true;
            for (var j = 0; j < needle.Length; j++)
            {
                if (haystack[i + j] != needle[j])
                {
                    matched = false;
                    break;
                }
            }

            if (matched)
            {
                return true;
            }
        }

        return false;
    }
}
