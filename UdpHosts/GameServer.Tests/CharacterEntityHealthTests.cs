using GameServer.Entities.Character;
using GameServer.Tests.Fakes;
using Xunit;
using CombatFlagsData = AeroMessages.GSS.Character.CombatFlagsData;

namespace GameServer.Tests;

public class CharacterEntityHealthTests
{
    private static (FakeShard Shard, CharacterEntity Character) CreateCharacter(int maxHealth = 1000, int maxShields = 200)
    {
        var shard = new FakeShard();
        var character = new CharacterEntity(shard, shard.GetNextGuid(0));
        character.SetControllingPlayer(new FakeNetworkPlayer(shard) { CharacterEntity = character });
        character.SetMaxHealth(maxHealth, resetCurrent: true);
        character.SetMaxShields(maxShields, resetCurrent: true);

        return (shard, character);
    }

    [Fact]
    public void SetCurrentHealth_ClampsAtZero()
    {
        var (_, character) = CreateCharacter();

        character.SetCurrentHealth(-50);

        Assert.Equal(0, character.CurrentHealth);
    }

    [Fact]
    public void SetCurrentHealth_ClampsAtMaxHealth()
    {
        var (_, character) = CreateCharacter();

        character.SetCurrentHealth(5000);

        Assert.Equal(1000, character.CurrentHealth);
    }

    [Fact]
    public void SetMaxHealth_ResetCurrent_FillsHealth()
    {
        var (_, character) = CreateCharacter();
        character.SetCurrentHealth(100);

        character.SetMaxHealth(2000, resetCurrent: true);

        Assert.Equal(2000, character.MaxHealth.Value);
        Assert.Equal(2000, character.CurrentHealth);
    }

    [Fact]
    public void SetMaxHealth_KeepCurrent_ClampsCurrentHealth()
    {
        var (_, character) = CreateCharacter();
        character.SetCurrentHealth(1000);

        character.SetMaxHealth(500, resetCurrent: false);

        Assert.Equal(500, character.MaxHealth.Value);
        Assert.Equal(500, character.CurrentHealth);
    }

    [Fact]
    public void SetCurrentHealth_UpdatesObserverHealthPercentage()
    {
        var (_, character) = CreateCharacter();

        character.SetCurrentHealth(500);

        Assert.Equal(50, character.Character_ObserverView.CurrentHealthPctProp);
    }

    [Fact]
    public void SetCurrentHealth_AfterDamage_UpdatesBaseControllerHealth()
    {
        var (_, character) = CreateCharacter();

        character.SetCurrentHealth(123);

        Assert.Equal(123, character.Character_BaseController.CurrentHealthProp);
    }

    [Fact]
    public void SetCurrentShields_ClampsBothWays()
    {
        var (_, character) = CreateCharacter();

        character.SetCurrentShields(-10);
        Assert.Equal(0, character.CurrentShields);

        character.SetCurrentShields(1000);
        Assert.Equal(200, character.CurrentShields);
    }

    [Fact]
    public void HasCombatFlag_DefaultsToFalse()
    {
        var (_, character) = CreateCharacter();

        Assert.False(character.HasCombatFlag(CombatFlagsData.CharacterCombatFlags.immune_falldamage));
    }

    [Fact]
    public void SetCombatFlags_IsObservableThroughHasCombatFlag()
    {
        var (_, character) = CreateCharacter();

        character.SetCombatFlags(new CombatFlagsData
        {
            Value = CombatFlagsData.CharacterCombatFlags.immune_falldamage,
            Time = 0
        });

        Assert.True(character.HasCombatFlag(CombatFlagsData.CharacterCombatFlags.immune_falldamage));
    }

    [Fact]
    public void NewCharacter_StartsWithDefaultHealth()
    {
        var shard = new FakeShard();
        var character = new CharacterEntity(shard, shard.GetNextGuid(0));

        Assert.Equal(19192, character.MaxHealth.Value);
        Assert.Equal(19192, character.CurrentHealth);
        Assert.True(character.IsAlive);
    }
}
