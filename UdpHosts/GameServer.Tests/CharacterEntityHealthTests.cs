using System;
using System.Collections.Generic;
using System.Reflection;
using GameServer.Data;
using GameServer.Entities.Character;
using GameServer.Enums;
using GameServer.StaticDB;
using GameServer.StaticDB.Records.dbitems;
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

    /// <summary>
    ///     Points <see cref="SDBInterface"/>'s level-item-attribute table (the
    ///     <c>dbitems::LevelItemAttributes</c> curve the health rule reads) at a test table,
    ///     and returns a restore action. Tests must restore in <c>finally</c>: the field is
    ///     process-wide static state and other test classes run in parallel.
    /// </summary>
    private static Action SetLevelItemAttributes(Dictionary<KeyValuePair<uint, uint>, LevelItemAttributes> table)
    {
        var field = typeof(SDBInterface).GetField("_levelItemAttributes", BindingFlags.NonPublic | BindingFlags.Static);
        var original = (Dictionary<KeyValuePair<uint, uint>, LevelItemAttributes>)field.GetValue(null);
        field.SetValue(null, table);
        return () => field.SetValue(null, original);
    }

    private static CharacterEntity CreatePlayerCharacterWithLoadout(float itemHealthSum)
    {
        var shard = new FakeShard();
        var character = new CharacterEntity(shard, shard.GetNextGuid(0));
        character.SetControllingPlayer(new FakeNetworkPlayer(shard) { CharacterEntity = character });
        character.CurrentLoadout = new CharacterLoadout();
        character.CurrentLoadout.ItemAttributes[(ushort)ItemAttributeId.Health] = itemHealthSum;
        return character;
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

    [Fact]
    public void ResetMaxHealthFromDatabase_PlayerLoadout_ReplacesConstructionDefault()
    {
        // Regression test for the spawn-path bug: NetworkPlayer.Respawn() (which also runs
        // on the first ScheduleUpdateRequest, i.e. right when a fresh character finishes
        // loading the zone) used to reset the pool to HardcodedCharacterData.MaxHealth,
        // stomping the DB-derived value ApplyLoadout had computed. It now calls
        // ResetMaxHealthFromDatabase, so the pool a player spawns with is the loadout rule:
        // at progression level 1 the curve is 0 (the table starts contributing at level 4)
        // and the pool is exactly the item Health sum (~361 for the default Accord loadout).
        var restore = SetLevelItemAttributes(new Dictionary<KeyValuePair<uint, uint>, LevelItemAttributes>());
        try
        {
            var character = CreatePlayerCharacterWithLoadout(itemHealthSum: 361f);

            // Pre-reset state: the construction-time default the login path overwrites.
            Assert.Equal(19192, character.MaxHealth.Value);

            character.ResetMaxHealthFromDatabase();

            Assert.Equal(361, character.MaxHealth.Value);
            Assert.Equal(361, character.CurrentHealth);
        }
        finally
        {
            restore();
        }
    }

    [Fact]
    public void ResetMaxHealthFromDatabase_PlayerLoadout_AppliesLevelCurveAtFrameLevel()
    {
        // The same reset at level 45 must fold in the LevelItemAttributes curve (6,360 at
        // level 45 in build prod-1962) at the documented pool scale of 3: 361 + 6,360 x 3.
        var restore = SetLevelItemAttributes(new Dictionary<KeyValuePair<uint, uint>, LevelItemAttributes>
        {
            [new KeyValuePair<uint, uint>(CharacterHealthMath.HealthAttributeId, 45)] = new() { AttributeId = CharacterHealthMath.HealthAttributeId, Level = 45, Value = 6360f },
        });
        try
        {
            var character = CreatePlayerCharacterWithLoadout(itemHealthSum: 361f);
            character.FrameProgressionLevel = 45;

            character.ResetMaxHealthFromDatabase();

            Assert.Equal(361 + (6360 * 3), character.MaxHealth.Value);
            Assert.Equal(character.MaxHealth.Value, character.CurrentHealth);
        }
        finally
        {
            restore();
        }
    }

    [Fact]
    public void ResetMaxHealthFromDatabase_WithoutPlayerLoadout_LeavesPoolUntouched()
    {
        // No database source applies (player-controlled but no loadout with item Health,
        // and no MonsterScaling level), so the reset is documented to leave the pool alone.
        // The respawn path pairs this with an explicit fill so the character still comes
        // back at full health of whatever pool it has.
        var restore = SetLevelItemAttributes(new Dictionary<KeyValuePair<uint, uint>, LevelItemAttributes>());
        try
        {
            var shard = new FakeShard();
            var character = new CharacterEntity(shard, shard.GetNextGuid(0));
            character.SetControllingPlayer(new FakeNetworkPlayer(shard) { CharacterEntity = character });
            character.SetMaxHealth(500, resetCurrent: false);
            character.SetCurrentHealth(100);

            character.ResetMaxHealthFromDatabase();

            Assert.Equal(500, character.MaxHealth.Value);
            Assert.Equal(100, character.CurrentHealth);

            // ...and the respawn fill tops it back up.
            character.SetCurrentHealth(character.MaxHealth.Value);
            Assert.Equal(500, character.CurrentHealth);
        }
        finally
        {
            restore();
        }
    }
}
