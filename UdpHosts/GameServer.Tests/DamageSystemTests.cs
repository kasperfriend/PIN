using GameServer.Entities.Character;
using GameServer.Entities.Deployable;
using GameServer.Systems.SystemEvents;
using GameServer.Tests.Fakes;
using Xunit;

namespace GameServer.Tests;

public class DamageSystemTests
{
    private static (FakeShard Shard, CharacterEntity Character) CreateCharacter(int maxHealth = 1000, int maxShields = 0)
    {
        var shard = new FakeShard();
        var character = new CharacterEntity(shard, shard.GetNextGuid(0));
        character.SetControllingPlayer(new FakeNetworkPlayer(shard) { CharacterEntity = character });
        character.SetCharacterState(AeroMessages.GSS.Character.CharacterStateData.CharacterStatus.Living, 0);
        character.SetMaxHealth(maxHealth, resetCurrent: true);
        character.SetMaxShields(maxShields, resetCurrent: true);

        return (shard, character);
    }

    [Fact]
    public void ApplyDamage_ReducesHealth()
    {
        var (shard, character) = CreateCharacter();

        shard.Damage.ApplyDamage(character, 300);

        Assert.Equal(700, character.CurrentHealth);
    }

    [Fact]
    public void ApplyDamage_ClampsAtZero()
    {
        var (shard, character) = CreateCharacter();

        shard.Damage.ApplyDamage(character, 5000);

        Assert.Equal(0, character.CurrentHealth);
    }

    [Fact]
    public void ApplyDamage_ShieldsAbsorbDamageFirst()
    {
        var (shard, character) = CreateCharacter(maxShields: 500);

        shard.Damage.ApplyDamage(character, 700);

        Assert.Equal(0, character.CurrentShields);
        Assert.Equal(800, character.CurrentHealth);
    }

    [Fact]
    public void ApplyDamage_DamageFullyAbsorbedByShields_KeepsHealth()
    {
        var (shard, character) = CreateCharacter(maxShields: 500);

        shard.Damage.ApplyDamage(character, 300);

        Assert.Equal(200, character.CurrentShields);
        Assert.Equal(1000, character.CurrentHealth);
    }

    [Fact]
    public void ApplyDamage_DeadCharacter_IsIgnored()
    {
        var (shard, character) = CreateCharacter();
        character.SetCharacterState(AeroMessages.GSS.Character.CharacterStateData.CharacterStatus.Dead, 0);

        shard.Damage.ApplyDamage(character, 300);

        Assert.Equal(1000, character.CurrentHealth);
    }

    [Fact]
    public void ApplyDamage_BleedingOutCharacter_IsIgnored()
    {
        var (shard, character) = CreateCharacter();
        character.SetCharacterState(AeroMessages.GSS.Character.CharacterStateData.CharacterStatus.Incapacitated, 0);

        shard.Damage.ApplyDamage(character, 300);

        Assert.Equal(1000, character.CurrentHealth);
    }

    [Fact]
    public void ApplyDamage_PublishesEntityDamagedEvent()
    {
        var (shard, character) = CreateCharacter();
        EntityDamagedEvent received = default;
        var receivedAny = false;
        shard.EventBus.Subscribe<EntityDamagedEvent>(evt =>
        {
            received = evt;
            receivedAny = true;
        });

        shard.Damage.ApplyDamage(character, 300);

        Assert.True(receivedAny);
        Assert.Equal(character, received.Target);
        Assert.Equal(300, received.DamageAmount);
    }

    [Fact]
    public void ApplyDamage_DeadCharacter_PublishesNoEvent()
    {
        var (shard, character) = CreateCharacter();
        character.SetCharacterState(AeroMessages.GSS.Character.CharacterStateData.CharacterStatus.Dead, 0);
        var eventCount = 0;
        shard.EventBus.Subscribe<EntityDamagedEvent>(_ => ++eventCount);

        shard.Damage.ApplyDamage(character, 300);

        Assert.Equal(0, eventCount);
    }

    [Fact]
    public void ApplyHeal_RestoresHealth()
    {
        var (shard, character) = CreateCharacter();
        shard.Damage.ApplyDamage(character, 800);

        shard.Damage.ApplyHeal(character, 300);

        Assert.Equal(500, character.CurrentHealth);
    }

    [Fact]
    public void ApplyHeal_ClampsAtMaxHealth()
    {
        var (shard, character) = CreateCharacter();
        shard.Damage.ApplyDamage(character, 100);

        shard.Damage.ApplyHeal(character, 5000);

        Assert.Equal(1000, character.CurrentHealth);
    }

    [Fact]
    public void ApplyHeal_DeadCharacter_IsIgnored()
    {
        var (shard, character) = CreateCharacter();
        character.SetCharacterState(AeroMessages.GSS.Character.CharacterStateData.CharacterStatus.Dead, 0);

        shard.Damage.ApplyHeal(character, 300);

        Assert.Equal(1000, character.CurrentHealth);
    }

    [Fact]
    public void ApplyHeal_PublishesEntityHealedEvent()
    {
        var (shard, character) = CreateCharacter();
        EntityHealedEvent received = default;
        var receivedAny = false;
        shard.EventBus.Subscribe<EntityHealedEvent>(evt =>
        {
            received = evt;
            receivedAny = true;
        });

        shard.Damage.ApplyHeal(character, 300);

        Assert.True(receivedAny);
        Assert.Equal(300, received.HealAmount);
    }

    [Fact]
    public void ApplyDamage_DeployableAtZeroHealth_DiesAndGetsLifetime()
    {
        var shard = new FakeShard();
        var deployable = new DeployableEntity(shard, shard.GetNextGuid(0), type: 395, abilitySrcId: 0);
        deployable.SetMaxHealth(100);
        deployable.SetCurrentHealth(100);

        shard.Damage.ApplyDamage(deployable, 150);

        Assert.True(deployable.IsDead);
        Assert.Equal(0, deployable.CurrentHealth);
        Assert.True(shard.EntityMan.HasRemainingLifetime(deployable));
    }

    [Fact]
    public void ApplyDamage_DeadDeployable_IsIgnored()
    {
        var shard = new FakeShard();
        var deployable = new DeployableEntity(shard, shard.GetNextGuid(0), type: 395, abilitySrcId: 0);
        deployable.SetMaxHealth(100);
        deployable.SetCurrentHealth(100);
        deployable.MarkDead();
        var eventCount = 0;
        shard.EventBus.Subscribe<EntityDamagedEvent>(_ => ++eventCount);

        shard.Damage.ApplyDamage(deployable, 150);

        Assert.Equal(0, eventCount);
    }
}
