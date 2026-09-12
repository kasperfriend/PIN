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
        var service = new EmoteService(new FakeEmoteDataSource());

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
        return new EmoteService(new FakeEmoteDataSource(), effects);
    }

    private static CharacterEntity CreateCharacter()
    {
        var shard = new FakeShard();
        var character = new CharacterEntity(shard, shard.GetNextGuid(0));
        character.SetCharacterState(CharacterStateData.CharacterStatus.Living, 0);
        shard.Entities.Add(character.EntityId, character);

        return character;
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
