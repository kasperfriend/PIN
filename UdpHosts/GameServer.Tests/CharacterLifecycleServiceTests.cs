using GameServer.Entities.Character;
using GameServer.Systems.CharacterLifecycle;
using GameServer.Tests.Fakes;
using Xunit;

namespace GameServer.Tests;

public class CharacterLifecycleServiceTests
{
    private static readonly AeroMessages.GSS.Character.CharacterStateData.CharacterStatus Living
        = AeroMessages.GSS.Character.CharacterStateData.CharacterStatus.Living;

    private static (FakeShard Shard, CharacterEntity Character, CharacterLifecycleService Lifecycle) CreateSut(bool canBleedout = true)
    {
        var shard = new FakeShard();
        var character = new CharacterEntity(shard, shard.GetNextGuid(0));
        character.SetControllingPlayer(new FakeNetworkPlayer(shard) { CharacterEntity = character });
        character.SetCharacterState(Living, 0);
        character.CanBleedout = canBleedout;
        character.SetMaxHealth(1000, resetCurrent: true);

        shard.CharacterLifecycle.OnCharacterCreated(character);

        return (shard, character, shard.CharacterLifecycle);
    }

    [Fact]
    public void LethalDamage_WhenBleedoutAllowed_EntersBleedout()
    {
        var (shard, character, lifecycle) = CreateSut();

        shard.Damage.ApplyDamage(character, 1000);

        Assert.Equal(CharacterLifecycleState.Bleedout, lifecycle.GetState(character));
        Assert.Equal(0, character.CurrentHealth);
        Assert.Equal(AeroMessages.GSS.Character.CharacterStateData.CharacterStatus.Incapacitated, character.CharacterState.State);
    }

    [Fact]
    public void LethalDamage_WhenBleedoutNotAllowed_GoesStraightToDead()
    {
        var (shard, character, lifecycle) = CreateSut(canBleedout: false);

        shard.Damage.ApplyDamage(character, 1000);

        Assert.Equal(CharacterLifecycleState.Dead, lifecycle.GetState(character));
        Assert.Equal(AeroMessages.GSS.Character.CharacterStateData.CharacterStatus.Dead, character.CharacterState.State);
        Assert.False(character.Alive);
    }

    [Fact]
    public void DamageDuringBleedout_DoesNotKillInstantly()
    {
        var (shard, character, lifecycle) = CreateSut();

        shard.Damage.ApplyDamage(character, 1000);
        shard.Damage.ApplyDamage(character, 1000);

        Assert.Equal(CharacterLifecycleState.Bleedout, lifecycle.GetState(character));
    }

    [Fact]
    public void BleedoutTick_BeforeExpiry_KeepsBleedingOutState()
    {
        var (shard, character, lifecycle) = CreateSut();

        shard.Damage.ApplyDamage(character, 1000);
        shard.CurrentTimeLong += 1000;
        lifecycle.Tick(1.0, shard.CurrentTimeLong, CancellationToken.None);

        // Still inside the bleedout window (15s), so the character stays down
        Assert.Equal(CharacterLifecycleState.Bleedout, lifecycle.GetState(character));
    }

    [Fact]
    public void BleedoutExpiry_TransitionsToDead()
    {
        var (shard, character, lifecycle) = CreateSut();
        var diedEventCount = 0;
        shard.EventBus.Subscribe<CharacterDiedEvent>(_ => ++diedEventCount);

        shard.Damage.ApplyDamage(character, 1000);
        shard.CurrentTimeLong += 15_000;
        lifecycle.Tick(15.0, shard.CurrentTimeLong, CancellationToken.None);

        Assert.Equal(CharacterLifecycleState.Dead, lifecycle.GetState(character));
        Assert.Equal(1, diedEventCount);
    }

    [Fact]
    public void TryRevive_RestoresLivingStateWithQuarterHealth()
    {
        var (shard, character, lifecycle) = CreateSut();

        shard.Damage.ApplyDamage(character, 1000);
        lifecycle.TryRevive(character);

        Assert.Equal(CharacterLifecycleState.Living, lifecycle.GetState(character));
        Assert.Equal(Living, character.CharacterState.State);
        Assert.Equal(250, character.CurrentHealth);
    }

    [Fact]
    public void TryRevive_WhileLiving_IsRejected()
    {
        var (shard, character, lifecycle) = CreateSut();

        lifecycle.TryRevive(character);

        Assert.Equal(1000, character.CurrentHealth);
        Assert.Equal(CharacterLifecycleState.Living, lifecycle.GetState(character));
    }

    [Fact]
    public void ForceDeath_TransitionsToDead()
    {
        var (shard, character, lifecycle) = CreateSut();
        var diedEventCount = 0;
        shard.EventBus.Subscribe<CharacterDiedEvent>(_ => ++diedEventCount);

        lifecycle.ForceDeath(character);

        Assert.Equal(CharacterLifecycleState.Dead, lifecycle.GetState(character));
        Assert.Equal(0, character.CurrentHealth);
        Assert.Equal(1, diedEventCount);
    }

    [Fact]
    public void ForceBleedout_TransitionsToBleedout()
    {
        var (shard, character, lifecycle) = CreateSut();

        lifecycle.ForceBleedout(character);

        Assert.Equal(CharacterLifecycleState.Bleedout, lifecycle.GetState(character));
        Assert.Equal(0, character.CurrentHealth);
    }

    [Fact]
    public void Reset_ReturnsToLivingState()
    {
        var (shard, character, lifecycle) = CreateSut();

        lifecycle.ForceDeath(character);
        lifecycle.Reset(character);

        Assert.Equal(CharacterLifecycleState.Living, lifecycle.GetState(character));
    }

    [Fact]
    public void NonLethalDamage_KeepsCharacterLiving()
    {
        var (shard, character, lifecycle) = CreateSut();

        shard.Damage.ApplyDamage(character, 500);

        Assert.Equal(CharacterLifecycleState.Living, lifecycle.GetState(character));
        Assert.Equal(500, character.CurrentHealth);
    }
}
