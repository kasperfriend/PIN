using System.Collections.Generic;
using AeroMessages.GSS.Character;
using GameServer.Entities.Character;
using GameServer.Systems.Emotes;
using GameServer.Tests.Fakes;
using Xunit;

namespace GameServer.Tests;

/// <summary>
///     Emotes come out of <c>dbcharacter::EmoteRecord</c> (382 rows). The client drives the animation from
///     the emote id replicated on the performer's views, so the id has to be one the table holds; the four
///     rows that name a <c>statuseffect</c> (1460 <c>shocking</c>, 1462 <c>firedance</c>, 1478/1479
///     <c>heartbooth</c>) also apply it, because those effects carry the client-side commands that draw
///     them and the server-side duration commands that end them.
/// </summary>
public class EmoteServiceTests
{
    [Fact]
    public void EmoteInTheTable_IsReplicatedOnTheCharacter()
    {
        var character = CreateCharacter();
        var effects = new RecordingEmoteEffectApplier();
        var service = CreateService(effects);

        var performed = service.Perform(character, 1, 4_000);

        Assert.True(performed);
        Assert.Equal((ushort)1, character.Emote.Id);
        Assert.Equal(4_000u, character.Emote.Time);
        Assert.Empty(effects.Applied);
    }

    [Fact]
    public void EmoteOutsideTheTable_IsIgnored()
    {
        var character = CreateCharacter();
        var effects = new RecordingEmoteEffectApplier();
        var service = CreateService(effects);

        var performed = service.Perform(character, 65_000, 4_000);

        Assert.False(performed);
        Assert.Equal(EmoteService.NoEmote, character.Emote.Id);
        Assert.Empty(effects.Applied);
    }

    [Fact]
    public void EmoteWithAStatusEffect_AppliesIt()
    {
        var character = CreateCharacter();
        var effects = new RecordingEmoteEffectApplier();
        var service = CreateService(effects);

        var performed = service.Perform(character, 1460, 4_000);

        Assert.True(performed);
        Assert.Equal((ushort)1460, character.Emote.Id);

        var applied = Assert.Single(effects.Applied);
        Assert.Equal(character.EntityId, applied.EntityId);
        Assert.Equal(13_551u, applied.EffectId);
        Assert.Equal(4_000u, applied.Time);
    }

    [Fact]
    public void EmoteWithoutAStatusEffect_AppliesNothing()
    {
        var character = CreateCharacter();
        var effects = new RecordingEmoteEffectApplier();
        var service = CreateService(effects);

        var performed = service.Perform(character, 1480, 4_000);

        Assert.True(performed);
        Assert.Equal((ushort)1480, character.Emote.Id);
        Assert.Empty(effects.Applied);
    }

    [Fact]
    public void EmoteZero_StopsTheEmote()
    {
        var character = CreateCharacter();
        var service = CreateService(new RecordingEmoteEffectApplier());

        service.Perform(character, 1, 4_000);
        var stopped = service.Perform(character, EmoteService.NoEmote, 5_000);

        Assert.True(stopped);
        Assert.Equal(EmoteService.NoEmote, character.Emote.Id);
        Assert.Equal(5_000u, character.Emote.Time);
    }

    [Fact]
    public void UnknownEmote_LeavesTheRunningEmoteAlone()
    {
        var character = CreateCharacter();
        var service = CreateService(new RecordingEmoteEffectApplier());

        service.Perform(character, 1, 4_000);
        var performed = service.Perform(character, 65_000, 5_000);

        Assert.False(performed);
        Assert.Equal((ushort)1, character.Emote.Id);
        Assert.Equal(4_000u, character.Emote.Time);
    }

    [Fact]
    public void EmoteWithAStatusEffect_StillReplicatesWithoutAnApplier()
    {
        var character = CreateCharacter();
        var service = new EmoteService(new FakeEmoteTable());

        var performed = service.Perform(character, 1460, 4_000);

        Assert.True(performed);
        Assert.Equal((ushort)1460, character.Emote.Id);
    }

    [Fact]
    public void EmoteWithAnEffectTheShardRefused_StillCountsAsPerformed()
    {
        var character = CreateCharacter();
        var effects = new RecordingEmoteEffectApplier { Result = false };
        var service = CreateService(effects);

        var performed = service.Perform(character, 1460, 4_000);

        Assert.True(performed);
        Assert.Equal((ushort)1460, character.Emote.Id);
        Assert.Single(effects.Applied);
    }

    private static EmoteService CreateService(RecordingEmoteEffectApplier effects)
    {
        return new EmoteService(new FakeEmoteTable(), effects);
    }

    private static CharacterEntity CreateCharacter()
    {
        var shard = new FakeShard();
        var character = new CharacterEntity(shard, shard.GetNextGuid(0));
        character.SetCharacterState(CharacterStateData.CharacterStatus.Living, 0);
        shard.Entities.Add(character.EntityId, character);

        return character;
    }

    /// <summary>The emote rows these tests need out of the 382 in the database.</summary>
    private sealed class FakeEmoteTable : IEmoteDataSource
    {
        private readonly Dictionary<ushort, EmoteDefinition> _emotes = new()
        {
            [1] = new EmoteDefinition(1, "dance", 0),
            [4] = new EmoteDefinition(4, "taunt", 0),
            [1460] = new EmoteDefinition(1460, "shocking", 13_551),
            [1462] = new EmoteDefinition(1462, "firedance", 4348),
            [1479] = new EmoteDefinition(1479, "heartbooth_right", 14_752),
            [1480] = new EmoteDefinition(1480, "sumostomp", 0),
        };

        public EmoteDefinition? GetEmote(ushort emoteId)
        {
            if (_emotes.TryGetValue(emoteId, out var emote))
            {
                return emote;
            }

            return null;
        }
    }

    private sealed class RecordingEmoteEffectApplier : IEmoteEffectApplier
    {
        public List<(ulong EntityId, uint EffectId, uint Time)> Applied { get; } = [];

        public bool Result { get; set; } = true;

        public bool Apply(CharacterEntity target, uint effectId, uint time)
        {
            Applied.Add((target.EntityId, effectId, time));
            return Result;
        }
    }
}
