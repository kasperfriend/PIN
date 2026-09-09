using System;
using System.Buffers.Binary;
using AeroMessages.GSS.Character;
using GameServer.Entities.Character;
using GameServer.StaticDB.Records.aptfs;
using GameServer.Systems.Aptitude;
using GameServer.Systems.Aptitude.Commands.Impact;
using GameServer.Tests.Fakes;
using Xunit;

namespace GameServer.Tests;

/// <summary>
///     A glider pad launch is delivered to the client as a <c>ForcedMovement</c> Type 5 one-frame velocity
///     impulse. The client only applies the packet while it is not stale, and it decides staleness from the
///     packet's own <c>ShortTime</c> (the current 16-bit clock) and the impulse's <c>Time1</c>/<c>Time2</c>
///     stamps. Upstream PIN and the live client send <c>Time1 = now+19</c>, <c>Time2 = now+20</c> and
///     <c>ShortTime = CurrentShortTime</c>; a launch that stamps the packet 25 ms in the past (and therefore
///     also sets <c>ShortTime</c> to an old value) is dropped by the client and leaves the player standing on
///     the pad. These tests pin the exact body the server serializes so that regression cannot quietly send a
///     stale packet again.
/// </summary>
public class ForcePushCommandTests
{
    [Fact]
    public void ForcePush_SendsAFreshType5ImpulseWithCurrentShortTime()
    {
        var shard = new FakeShard { CurrentTimeLong = 60_000 };
        var character = FakeCharacterFactory.Create(shard);
        shard.EntityMan.Add(character.EntityId, character);
        character.SetCharacterState(CharacterStateData.CharacterStatus.Living, 60_000);

        var player = new FakeNetworkPlayer(shard) { CharacterEntity = character };
        player.AttachRealChannels();
        character.SetControllingPlayer(player);

        var context = new Context(shard, character);
        context.Targets.Push(character);

        var command = new ForcePushCommand(new ForcePushCommandDef
        {
            Id = 1509142,
            Strength = 33,
            StrengthRegop = 0,
        });

        Assert.True(command.Execute(context));
        player.FlushAttachedChannels();

        // The exact ForcedMovement body the live server sends for a (0,0,33) pad launch. The message is wrapped
        // in a channel/GSS header and may share a datagram with other messages, so only search for the body.
        var expected = new byte[29];
        expected[0] = 0x05; // Type 5 = one-frame velocity impulse
        BinaryPrimitives.WriteUInt32LittleEndian(expected.AsSpan(1), 0); // Unk1
        expected[5] = 0; // HaveUnk2
        BinaryPrimitives.WriteSingleLittleEndian(expected.AsSpan(6), 0f); // Velocity.X
        BinaryPrimitives.WriteSingleLittleEndian(expected.AsSpan(10), 0f); // Velocity.Y
        BinaryPrimitives.WriteSingleLittleEndian(expected.AsSpan(14), 33f); // Velocity.Z
        BinaryPrimitives.WriteUInt32LittleEndian(expected.AsSpan(18), 60_019u); // Time1
        BinaryPrimitives.WriteUInt32LittleEndian(expected.AsSpan(22), 60_020u); // Time2
        expected[26] = 0; // Unk2
        BinaryPrimitives.WriteUInt16LittleEndian(expected.AsSpan(27), (ushort)60_000u); // ShortTime

        Assert.True(
            AnyPacketContains(player.SentPackets, expected),
            "ForcePush did not send a fresh ForcedMovement Type 5 impulse (Time1=now+19, ShortTime=current).");

        // The launch-pending window is still opened at push time (server-side), independent of the wire stamp.
        Assert.True(character.IsServerLaunchPending);

        // The marker is a waiting grace for the server's own gates, not a claim that the client is already
        // gliding: ForcePush must not overwrite the reported movement state with the glider nibble while the
        // impulse is still in flight (MovementRelay holds the grounded authoring confirm instead).
        Assert.NotEqual(Movestate.Glider, character.MovementStateContainer.Movestate);
    }

    [Theory]
    [InlineData(0u)]
    [InlineData(3u)]
    public void ForcePush_FoldsTheRegisterIntoTheImpulseStrength(uint register)
    {
        var shard = new FakeShard { CurrentTimeLong = 60_000 };
        var character = FakeCharacterFactory.Create(shard);
        shard.EntityMan.Add(character.EntityId, character);
        character.SetCharacterState(CharacterStateData.CharacterStatus.Living, 60_000);

        var player = new FakeNetworkPlayer(shard) { CharacterEntity = character };
        player.AttachRealChannels();
        character.SetControllingPlayer(player);

        var context = new Context(shard, character);
        context.Targets.Push(character);
        context.Register = register;

        var command = new ForcePushCommand(new ForcePushCommandDef
        {
            Id = 1509142,
            Strength = 30,
            StrengthRegop = 1,
        });

        Assert.True(command.Execute(context));
        player.FlushAttachedChannels();

        // The glider pad's row carries strength 30 and StrengthRegop=1; the chain register contributes the
        // module/boost bonus (0 or 3). With register unset (NaN) the register input is ignored.
        var expectedZ = 30 + register;
        var body = new byte[29];
        body[0] = 0x05;
        BinaryPrimitives.WriteUInt32LittleEndian(body.AsSpan(1), 0);
        body[5] = 0;
        BinaryPrimitives.WriteSingleLittleEndian(body.AsSpan(6), 0f);
        BinaryPrimitives.WriteSingleLittleEndian(body.AsSpan(10), 0f);
        BinaryPrimitives.WriteSingleLittleEndian(body.AsSpan(14), expectedZ);
        BinaryPrimitives.WriteUInt32LittleEndian(body.AsSpan(18), 60_019u);
        BinaryPrimitives.WriteUInt32LittleEndian(body.AsSpan(22), 60_020u);
        body[26] = 0;
        BinaryPrimitives.WriteUInt16LittleEndian(body.AsSpan(27), (ushort)60_000u);

        Assert.True(
            AnyPacketContains(player.SentPackets, body),
            $"Expected a ForcedMovement Type 5 impulse with Z={expectedZ}.");
    }

    private static bool AnyPacketContains(System.Collections.Generic.IEnumerable<Memory<byte>> packets, byte[] needle)
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

    private static bool Contains(ReadOnlySpan<byte> haystack, ReadOnlySpan<byte> needle)
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
