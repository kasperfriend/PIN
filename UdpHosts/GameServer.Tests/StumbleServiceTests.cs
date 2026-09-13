using System.Numerics;
using AeroMessages.GSS.Character;
using GameServer.Entities.Character;
using GameServer.StaticDB.Records.dbcharacter;
using GameServer.Systems.Combat;
using GameServer.Systems.Emotes;
using GameServer.Tests.Fakes;
using Xunit;

namespace GameServer.Tests;

/// <summary>
///     Stumble is applied by an explicit <c>dbcharacter::Stumble</c> id. Damage does not pick
///     one: no weapon, ammo or damage-type row names a stumble in prod-1962.
/// </summary>
public class StumbleServiceTests
{
    private const uint CombatStumbleId = 1;
    private const uint StatusFx = 9452;

    [Fact]
    public void TryStumble_AppliesTheStatusEffectAndRemembersTheTime()
    {
        var (victim, sourcePos, effects, service) = Create();

        var applied = service.TryStumble(victim, sourcePos, CombatStumbleId, 4_000);

        Assert.True(applied);
        Assert.Equal(4_000u, victim.LastStumbleTime);
        Assert.True(victim.HasStumbled(CombatStumbleId));
        var effect = Assert.Single(effects.Applied);
        Assert.Equal(StatusFx, effect.EffectId);
        Assert.Equal(4_000u, effect.Time);
    }

    [Fact]
    public void TryStumble_UnknownId_IsIgnored()
    {
        var (victim, sourcePos, effects, service) = Create();

        Assert.False(service.TryStumble(victim, sourcePos, 99, 4_000));
        Assert.Equal(0u, victim.LastStumbleTime);
        Assert.Empty(effects.Applied);
    }

    [Fact]
    public void TryStumble_RestrictStumble_IsIgnored()
    {
        var (victim, sourcePos, effects, service) = Create();
        victim.SetCombatFlags(new CombatFlagsData
        {
            Value = CombatFlagsData.CharacterCombatFlags.restrict_stumble,
            Time = 0,
        });

        Assert.False(service.TryStumble(victim, sourcePos, CombatStumbleId, 4_000));
        Assert.Empty(effects.Applied);
    }

    [Fact]
    public void TryStumble_Cooldown_BlocksARepeat()
    {
        var (victim, sourcePos, effects, service) = Create();

        Assert.True(service.TryStumble(victim, sourcePos, CombatStumbleId, 1_000));
        Assert.False(service.TryStumble(victim, sourcePos, CombatStumbleId, 1_500));
        Assert.True(service.TryStumble(victim, sourcePos, CombatStumbleId, 3_100));
        Assert.Equal(2, effects.Applied.Count);
    }

    [Fact]
    public void TryStumble_OnlyOnceRow_FiresOnce()
    {
        var shard = new FakeShard();
        var victim = LivingCharacter(shard);
        var data = new FakeStumbleDataSource();
        data.Stumbles[2] = new Stumble { Id = 2, StatusfxId = StatusFx, CooldownMs = 0, OnlyOnce = 1 };
        var effects = new RecordingEmoteEffectApplier();
        var service = new StumbleService(data, effects);
        var sourcePos = victim.Position + new Vector3(0f, 2f, 0f);

        Assert.True(service.TryStumble(victim, sourcePos, 2, 1_000));
        Assert.False(service.TryStumble(victim, sourcePos, 2, 2_000));
        Assert.Single(effects.Applied);
    }

    [Fact]
    public void TryStumble_DeadVictim_IsIgnored()
    {
        var (victim, sourcePos, effects, service) = Create();
        victim.SetCharacterState(CharacterStateData.CharacterStatus.Dead, 1);

        Assert.False(service.TryStumble(victim, sourcePos, CombatStumbleId, 4_000));
        Assert.Empty(effects.Applied);
    }

    private static (CharacterEntity Victim, Vector3 SourcePos, RecordingEmoteEffectApplier Effects, StumbleService Service) Create()
    {
        var shard = new FakeShard();
        var victim = LivingCharacter(shard);
        victim.AimDirection = new Vector3(0f, 1f, 0f);
        var data = CombatTable();
        var effects = new RecordingEmoteEffectApplier();
        var service = new StumbleService(data, effects);
        return (victim, victim.Position + new Vector3(0f, 2f, 0f), effects, service);
    }

    private static FakeStumbleDataSource CombatTable()
    {
        var data = new FakeStumbleDataSource();
        data.Stumbles[CombatStumbleId] = new Stumble
        {
            Id = CombatStumbleId,
            AnimIndex = 12,
            StatusfxId = StatusFx,
            Duration = 2_000,
            CooldownMs = 2_000,
        };
        data.Directions[CombatStumbleId] =
        [
            new StumbleDirection { StumbleId = CombatStumbleId, AnimSubstate = 0, Duration = 2_000 },
            new StumbleDirection { StumbleId = CombatStumbleId, AnimSubstate = 1, Duration = 2_000 },
            new StumbleDirection { StumbleId = CombatStumbleId, AnimSubstate = 2, Duration = 2_000 },
            new StumbleDirection { StumbleId = CombatStumbleId, AnimSubstate = 3, Duration = 2_000 },
        ];
        return data;
    }

    private static CharacterEntity LivingCharacter(FakeShard shard)
    {
        var character = new CharacterEntity(shard, shard.GetNextGuid(0));
        character.SetCharacterState(CharacterStateData.CharacterStatus.Living, 0);
        shard.Entities.Add(character.EntityId, character);
        return character;
    }

    private sealed class RecordingEmoteEffectApplier : IEmoteEffectApplier
    {
        public System.Collections.Generic.List<(ulong EntityId, uint EffectId, uint Time)> Applied { get; } = [];

        public bool Apply(CharacterEntity target, uint effectId, uint time)
        {
            Applied.Add((target.EntityId, effectId, time));
            return true;
        }
    }
}
