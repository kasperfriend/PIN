using AeroMessages.GSS.Character;
using GameServer.Entities.Character;
using GameServer.Entities.Deployable;
using GameServer.StaticDB.Records.aptfs;
using GameServer.Systems.Aptitude;
using GameServer.Systems.Aptitude.Commands.Other;
using GameServer.Tests.Fakes;
using Xunit;

namespace GameServer.Tests;

/// <summary>
///     <c>SwitchWeapon</c> (aptitude command 97) puts the character's replicated weapon selection on the
///     row's slot for the lifetime of the effect that carries it. 343 rows: slots 1-3, 202 with
///     <c>restore_on_rollback</c>, 204 with <c>play_animation</c>. The command was a placeholder that
///     swapped nothing; these tests pin the swap, the rollback and the two row flags.
/// </summary>
public class SwitchWeaponCommandTests
{
    private static readonly uint Now = 61_000;

    [Fact]
    public void Execute_SwitchesTheReplicatedWeaponIndexToTheRow()
    {
        var shard = new FakeShard { CurrentTimeLong = Now };
        var character = CreateCharacter(shard);

        var command = new SwitchWeaponCommand(new SwitchWeaponCommandDef { Id = 172246, TargetWeaponSlot = 2, RestoreOnRollback = 1 });
        var context = Execute(command, character);

        Assert.Equal(2u, character.WeaponIndex.Index);
        Assert.Equal(Now, character.WeaponIndex.Time);
        Assert.Equal(2u, character.Character_CombatView.WeaponIndexProp.Index);
        Assert.Equal(2u, character.Character_CombatController.WeaponIndexProp.Index);
        Assert.True(context.Actives.ContainsKey(command));
    }

    [Fact]
    public void Execute_WithPlayAnimation_StampsTheEquipmentLoadTime()
    {
        var shard = new FakeShard();
        var character = CreateCharacter(shard);
        // Spawn stamped the load time with the shard's time; move the clock on so the swap's stamp is
        // distinguishable from it.
        shard.CurrentTimeLong = Now;

        var command = new SwitchWeaponCommand(new SwitchWeaponCommandDef
        {
            Id = 160038,
            TargetWeaponSlot = 2,
            PlayAnimation = 1,
        });
        Execute(command, character);

        Assert.Equal(Now, character.Character_CombatView.EquipmentLoadTimeProp);
    }

    [Fact]
    public void Execute_WithoutPlayAnimation_LeavesTheEquipmentLoadTimeAlone()
    {
        var shard = new FakeShard();
        var character = CreateCharacter(shard);
        shard.CurrentTimeLong = Now;

        var command = new SwitchWeaponCommand(new SwitchWeaponCommandDef
        {
            Id = 172246,
            TargetWeaponSlot = 2,
            PlayAnimation = 0,
        });
        Execute(command, character);

        // Still the spawn stamp: the swap happened at a later time and wrote nothing.
        Assert.Equal(60_000u, character.Character_CombatView.EquipmentLoadTimeProp);
    }

    [Fact]
    public void Remove_WithRestoreOnRollback_WritesThePreviousSelectionBack()
    {
        var shard = new FakeShard { CurrentTimeLong = Now };
        var character = CreateCharacter(shard);
        character.SetWeaponIndex(new WeaponIndexData { Index = 1, Unk1 = 1, Unk2 = 0, Time = 59_000 });

        var command = new SwitchWeaponCommand(new SwitchWeaponCommandDef
        {
            Id = 172246,
            TargetWeaponSlot = 2,
            RestoreOnRollback = 1,
        });
        var context = Execute(command, character);

        shard.CurrentTimeLong = Now + 500;
        RemoveEffect(command, context);

        Assert.Equal(1u, character.WeaponIndex.Index);
        Assert.Equal(Now + 500, character.WeaponIndex.Time);
        Assert.Equal(1u, character.Character_CombatView.WeaponIndexProp.Index);
    }

    [Fact]
    public void Remove_WithRestoreOnRollback_LeavesAWeaponThePlayerChoseMeanwhile()
    {
        var shard = new FakeShard { CurrentTimeLong = Now };
        var character = CreateCharacter(shard);
        character.SetWeaponIndex(new WeaponIndexData { Index = 1, Unk1 = 1, Unk2 = 0, Time = 59_000 });

        var command = new SwitchWeaponCommand(new SwitchWeaponCommandDef
        {
            Id = 172246,
            TargetWeaponSlot = 2,
            RestoreOnRollback = 1,
        });
        var context = Execute(command, character);

        // The player pressed a weapon key while the effect ran: their client reported slot 1 through
        // SelectWeapon, which writes the index exactly like this.
        character.SetWeaponIndex(new WeaponIndexData { Index = 1, Unk1 = 3, Unk2 = 0, Time = Now + 100 });

        RemoveEffect(command, context);

        Assert.Equal(1u, character.WeaponIndex.Index);
    }

    [Fact]
    public void Remove_WithoutRestoreOnRollback_KeepsTheSwitchedWeapon()
    {
        var shard = new FakeShard { CurrentTimeLong = Now };
        var character = CreateCharacter(shard);
        character.SetWeaponIndex(new WeaponIndexData { Index = 1, Unk1 = 1, Unk2 = 0, Time = 59_000 });

        var command = new SwitchWeaponCommand(new SwitchWeaponCommandDef
        {
            Id = 160038,
            TargetWeaponSlot = 2,
            RestoreOnRollback = 0,
        });
        var context = Execute(command, character);

        Assert.Empty(context.Actives);
        RemoveEffect(command, context);

        Assert.Equal(2u, character.WeaponIndex.Index);
    }

    [Fact]
    public void Execute_OnTheWeaponThatIsAlreadyOut_SwitchesNothingAndRestoresNothing()
    {
        var shard = new FakeShard { CurrentTimeLong = Now };
        var character = CreateCharacter(shard);
        character.SetWeaponIndex(new WeaponIndexData { Index = 2, Unk1 = 1, Unk2 = 0, Time = 59_000 });

        var command = new SwitchWeaponCommand(new SwitchWeaponCommandDef
        {
            Id = 172246,
            TargetWeaponSlot = 2,
            RestoreOnRollback = 1,
        });
        var context = Execute(command, character);

        Assert.Equal(59_000u, character.WeaponIndex.Time);
        Assert.Empty(context.Actives);

        // Without the switched guard a rollback would stomp a weapon the player chose in the meantime with
        // the (identical, but stale) snapshot of this effect.
        character.SetWeaponIndex(new WeaponIndexData { Index = 1, Unk1 = 1, Unk2 = 0, Time = Now + 100 });
        RemoveEffect(command, context);

        Assert.Equal(1u, character.WeaponIndex.Index);
    }

    [Fact]
    public void NegativeSlot_StepsOver()
    {
        var shard = new FakeShard { CurrentTimeLong = Now };
        var character = CreateCharacter(shard);

        var command = new SwitchWeaponCommand(new SwitchWeaponCommandDef
        {
            Id = 1,
            TargetWeaponSlot = -1,
            RestoreOnRollback = 1,
        });
        var context = Execute(command, character);

        Assert.Equal(0u, character.WeaponIndex.Index);
        Assert.Empty(context.Actives);

        character.SetWeaponIndex(new WeaponIndexData { Index = 1, Unk1 = 1, Unk2 = 0, Time = Now });
        RemoveEffect(command, context);

        // Nothing was switched, so the rollback has nothing to hand back either.
        Assert.Equal(1u, character.WeaponIndex.Index);
    }

    [Fact]
    public void DeployableOwner_StepsOver()
    {
        var shard = new FakeShard();
        var pad = new DeployableEntity(shard, shard.GetNextGuid(), type: 395, abilitySrcId: 0);
        shard.Entities.Add(pad.EntityId, pad);

        var command = new SwitchWeaponCommand(new SwitchWeaponCommandDef { Id = 172246, TargetWeaponSlot = 2 });
        var context = new Context(shard, pad);

        Assert.True(command.Execute(context));
        Assert.Empty(context.Actives);
    }

    private static CharacterEntity CreateCharacter(FakeShard shard)
    {
        var character = FakeCharacterFactory.Create(shard);
        character.SetCharacterState(CharacterStateData.CharacterStatus.Living, shard.CurrentTime);
        shard.Entities.Add(character.EntityId, character);

        return character;
    }

    /// <summary>
    ///     Runs the command the way every chain environment does: the chain executes it directly (apply,
    ///     update, remove and ability chains alike - there is no OnApply outside an effect apply chain).
    /// </summary>
    private static Context Execute(SwitchWeaponCommand command, CharacterEntity character)
    {
        var context = new Context(character.Shard, character);

        Assert.True(command.Execute(context));

        return context;
    }

    private static void RemoveEffect(ICommand command, Context context)
    {
        foreach (var pair in context.Actives)
        {
            pair.Key.OnRemove(context, pair.Value);
        }
    }
}
