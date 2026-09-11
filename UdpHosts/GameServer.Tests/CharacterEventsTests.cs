using System;
using Shared.Common.Characters;
using Xunit;

namespace GameServer.Tests;

/// <summary>
///     Tests for the in-process <see cref="CharacterEvents"/> notification —
///     the bridge from the New You terminal's save (REST ClientApi host) to
///     the GRPC <c>CharacterVisualsUpdated</c> broadcast (GameServerApi host)
///     that makes a zoned-in character re-skin in-game — and for the guid
///     form the GameServer recognizes players by
///     (<see cref="CharacterResolver.GameServerEventGuid"/>).
/// </summary>
public class CharacterEventsTests
{
    private static CharacterRecord TestCharacter(ulong guid) => new()
    {
        CharacterGuid = guid,
        Name = "EventTest",
        Gender = 1,
        Race = 0
    };

    [Fact]
    public void NotifyVisualsUpdated_RaisesTheEventWithTheRecord()
    {
        CharacterRecord received = null;
        Action<CharacterRecord> handler = c => received = c;
        CharacterEvents.VisualsUpdated += handler;
        try
        {
            var character = TestCharacter(0x99AABBCCDDEE0100);
            CharacterEvents.NotifyVisualsUpdated(character);

            Assert.Same(character, received);
        }
        finally
        {
            CharacterEvents.VisualsUpdated -= handler;
        }
    }

    [Fact]
    public void NotifyVisualsUpdated_WithoutSubscribers_DoesNotThrow()
    {
        // No subscriber is the normal state until the GameServerApi host
        // (same process) subscribes; the save must not care.
        CharacterEvents.NotifyVisualsUpdated(TestCharacter(0x99AABBCCDDEE0100));
    }

    [Fact]
    public void NotifyVisualsUpdated_AThrowingSubscriber_DoesNotPropagate()
    {
        // The store is already persisted when this fires, so a broken
        // subscriber (a dead GameServer stream is the realistic case) may
        // drop the live update but must not break the save request.
        Action<CharacterRecord> handler = _ => throw new InvalidOperationException("dead stream");
        CharacterEvents.VisualsUpdated += handler;
        try
        {
            CharacterEvents.NotifyVisualsUpdated(TestCharacter(0x99AABBCCDDEE0100));
        }
        finally
        {
            CharacterEvents.VisualsUpdated -= handler;
        }
    }

    [Fact]
    public void GameServerEventGuid_ReplacesTheClobberedLowByteWithFe()
    {
        // The admin account's New Eden slot, the zone-picker seed form.
        Assert.Equal(
            0x99AABBCCDDEE01FE,
            CharacterResolver.GameServerEventGuid(0x99AABBCCDDEE01C0));

        // A low byte that is already non-zero is replaced, not added to.
        Assert.Equal(
            0x99AABBCCDDEE01FE,
            CharacterResolver.GameServerEventGuid(0x99AABBCCDDEE0100));
        Assert.Equal(
            0x99AABBCCDDEE01FE,
            CharacterResolver.GameServerEventGuid(0x99AABBCCDDEE01FE));

        // Account-generated guids (slot 1 of account 26294423 in New Eden)
        // keep their slot and account bits.
        Assert.Equal(
            0xAA000100000001FE,
            CharacterResolver.GameServerEventGuid(0xAA000100000001C0));
    }

    [Fact]
    public void GameServerEventGuid_MatchesTheGameServersLiveMatchRule()
    {
        // The GameServer keeps the player's character as the client-sent guid
        // with the low byte masked off (NetworkPlayer.LoginCore) and matches
        // events with `CharacterId + 0xFE == event.CharacterGuid`: for every
        // low byte the client might have clobbered, the broadcast guid must
        // be exactly the masked guid plus 0xFE.
        var storedGuid = 0xAA000100000001C0;
        for (var clobbered = 0; clobbered < 0x100; clobbered++)
        {
            var clientSentGuid = (storedGuid & 0xffffffffffffff00) | (uint)clobbered;
            var characterId = clientSentGuid & 0xffffffffffffff00; // what LoginCore stores

            Assert.Equal(characterId + 0xFE, CharacterResolver.GameServerEventGuid(storedGuid));
        }
    }
}
