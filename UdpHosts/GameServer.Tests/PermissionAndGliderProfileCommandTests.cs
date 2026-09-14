using System.Linq;
using AeroMessages.GSS.Character;
using AeroMessages.GSS.Character.Controller;
using GameServer.Entities.Character;
using GameServer.Entities.Deployable;
using GameServer.StaticDB.Records.customdata;
using GameServer.Systems.Aptitude;
using GameServer.Systems.Aptitude.Commands.Other;
using GameServer.Systems.Aptitude.Commands.Self;
using GameServer.Tests.Fakes;
using Xunit;

namespace GameServer.Tests;

/// <summary>
///     The commands that grant state a character has to hand back: <c>ModifyPermission</c> (the client side
///     permissions, like gliding) and <c>SetGliderParameters</c> (which glider flight profile the client uses).
///
///     Both write a single value on the character while several effects can overlap. Their temporary layers
///     therefore need to reveal the next active writer when one effect ends, and restore the value from before
///     the first writer only when the final effect ends. Otherwise one completed pad/glider stage can leave the
///     glider permission or the flight profile behind for the next jump.
/// </summary>
public class PermissionAndGliderProfileCommandTests
{
    [Fact]
    public void ModifyPermission_RestoresTheValueTheCharacterHadBefore()
    {
        var shard = new FakeShard();
        var character = CreateCharacter(shard);

        // The player's own glider ability granted the permission before the pad's launch effect ran.
        character.SetPermissionFlag(PermissionFlagsData.CharacterPermissionFlags.glider, true);

        var command = new ModifyPermissionCommand(new ModifyPermissionCommandDef { Id = 1508828, Glider = true });
        var context = RunEffect(command, character);

        Assert.True(character.CurrentPermissions[PermissionFlagsData.CharacterPermissionFlags.glider]);

        RemoveEffect(command, context);

        Assert.True(character.CurrentPermissions[PermissionFlagsData.CharacterPermissionFlags.glider]);
    }

    [Fact]
    public void ModifyPermission_OverlappingTemporaryGrantsRestoreTheOriginalValueAfterBothEnd()
    {
        var shard = new FakeShard();
        var character = CreateCharacter(shard);
        var first = new ModifyPermissionCommand(new ModifyPermissionCommandDef { Id = 1, Glider = true });
        var second = new ModifyPermissionCommand(new ModifyPermissionCommandDef { Id = 2, Glider = true });
        var firstContext = RunEffect(first, character);
        var secondContext = RunEffect(second, character);

        // The first stage may end while the successor is still responsible for the permission.
        RemoveEffect(first, firstContext);
        Assert.True(character.CurrentPermissions[PermissionFlagsData.CharacterPermissionFlags.glider]);

        // The successor must restore the value from before either stage, not the true value it
        // observed while the first stage was active. This is the stuck-glider regression.
        RemoveEffect(second, secondContext);
        Assert.False(character.CurrentPermissions[PermissionFlagsData.CharacterPermissionFlags.glider]);
    }

    [Fact]
    public void ModifyPermission_TakesTheGrantedPermissionAwayWhenItWasNotSetBefore()
    {
        var shard = new FakeShard();
        var character = CreateCharacter(shard);

        Assert.False(character.CurrentPermissions[PermissionFlagsData.CharacterPermissionFlags.glider]);

        var command = new ModifyPermissionCommand(new ModifyPermissionCommandDef { Id = 1508828, Glider = true });
        var context = RunEffect(command, character);

        Assert.True(character.CurrentPermissions[PermissionFlagsData.CharacterPermissionFlags.glider]);

        RemoveEffect(command, context);

        Assert.False(character.CurrentPermissions[PermissionFlagsData.CharacterPermissionFlags.glider]);
    }

    [Fact]
    public void ModifyPermission_LeavesPermissionsItDidNotChangeAlone()
    {
        var shard = new FakeShard();
        var character = CreateCharacter(shard);

        character.SetPermissionFlag(PermissionFlagsData.CharacterPermissionFlags.jetpack, true);

        var command = new ModifyPermissionCommand(new ModifyPermissionCommandDef { Id = 1508827, GliderHud = true });
        var context = RunEffect(command, character);

        RemoveEffect(command, context);

        Assert.True(character.CurrentPermissions[PermissionFlagsData.CharacterPermissionFlags.jetpack]);
        Assert.False(character.CurrentPermissions[PermissionFlagsData.CharacterPermissionFlags.glider_hud]);
    }

    [Fact]
    public void SetGliderParameters_HandsThePreviousProfileBackWhenTheEffectEnds()
    {
        var shard = new FakeShard();
        var character = CreateCharacter(shard);

        // The glider the player equipped for themselves.
        character.SetGliderProfileId(81423);

        // aptgss::SetGliderParametersDef row of the shared pad launch effect: the pad flies the character
        // with profile 18, the same row the game's own glider effect grants (profile 0 does not exist in
        // the client's dbcharacter::GliderParameters, so it can never be a valid flight model).
        var command = new SetGliderParametersCommand(new SetGliderParametersCommandDef { Id = 1511094, Value = 18 });
        var context = RunEffect(command, character);

        Assert.Equal(18u, character.GliderProfileId);

        RemoveEffect(command, context);

        Assert.Equal(81423u, character.GliderProfileId);
    }

    [Fact]
    public void SetGliderParameters_OverlappingStagesRestoreTheProfileBeforeTheLaunch()
    {
        var shard = new FakeShard();
        var character = CreateCharacter(shard);
        character.SetGliderProfileId(7);
        var first = new SetGliderParametersCommand(new SetGliderParametersCommandDef { Id = 1, Value = 18 });
        var second = new SetGliderParametersCommand(new SetGliderParametersCommandDef { Id = 2, Value = 25 });
        var firstContext = RunEffect(first, character);
        var secondContext = RunEffect(second, character);

        RemoveEffect(first, firstContext);
        Assert.Equal(25u, character.GliderProfileId);

        RemoveEffect(second, secondContext);
        Assert.Equal(7u, character.GliderProfileId);
    }

    [Fact]
    public void SetGliderParameters_WithoutDataLeavesTheProfileAlone()
    {
        var shard = new FakeShard();
        var character = CreateCharacter(shard);

        character.SetGliderProfileId(18);

        var command = new SetGliderParametersCommand(new SetGliderParametersCommandDef { Id = 1508824 });
        var context = RunEffect(command, character);

        Assert.Equal(18u, character.GliderProfileId);

        RemoveEffect(command, context);

        Assert.Equal(18u, character.GliderProfileId);
    }

    [Fact]
    public void SetGliderParameters_ZeroIsNotAProfileAndLeavesTheCurrentOneAlone()
    {
        var shard = new FakeShard();
        var character = CreateCharacter(shard);

        character.SetGliderProfileId(18);

        // dbcharacter::GliderParameters has no row 0 (the client table's ids run from 4 up), so a row that
        // carries 0 must not replace the character's flight model with a nonexistent one.
        var command = new SetGliderParametersCommand(new SetGliderParametersCommandDef { Id = 1511094, Value = 0 });
        var context = RunEffect(command, character);

        Assert.Equal(18u, character.GliderProfileId);

        RemoveEffect(command, context);

        Assert.Equal(18u, character.GliderProfileId);
    }

    [Theory]
    [InlineData(null)]
    [InlineData(0u)]
    public void SetGliderParameters_WithoutAProfileDoesNotRevertLaterChanges(uint? profile)
    {
        var shard = new FakeShard();
        var character = CreateCharacter(shard);
        character.SetGliderProfileId(7);
        var command = new SetGliderParametersCommand(new SetGliderParametersCommandDef { Id = 1508824, Value = profile });
        var context = RunEffect(command, character);
        Assert.Empty(context.Actives);
        Assert.Equal(7u, character.GliderProfileId);

        // Another effect grants a real flight profile while the incomplete row is active.
        character.SetGliderProfileId(18);
        RemoveEffect(command, context);
        Assert.Equal(18u, character.GliderProfileId);
    }

    [Fact]
    public void CommandsOnADeployableOwnerDoNotFailTheChain()
    {
        var shard = new FakeShard();
        var pad = new DeployableEntity(shard, shard.GetNextGuid(), type: 395, abilitySrcId: 0);
        shard.Entities.Add(pad.EntityId, pad);

        var context = new Context(shard, pad);

        Assert.True(new ModifyPermissionCommand(new ModifyPermissionCommandDef { Id = 1508828, Glider = true }).Execute(context));
        Assert.True(new SetGliderParametersCommand(new SetGliderParametersCommandDef { Id = 1511094, Value = 0 }).Execute(context));

        Assert.Empty(context.Actives);
    }

    /// <summary>
    ///     Runs what <see cref="AbilitySystem.DoApplyEffect" /> does for one command: the chain (Execute) and then
    ///     the OnApply of everything the command registered as active.
    /// </summary>
    private static Context RunEffect(ICommand command, CharacterEntity character)
    {
        var shard = character.Shard;
        var context = new Context(shard, character);

        Assert.True(command.Execute(context));

        foreach (var pair in context.Actives.Where(pair => pair.Key == command))
        {
            pair.Key.OnApply(context, pair.Value);
        }

        return context;
    }

    private static void RemoveEffect(ICommand command, Context context)
    {
        foreach (var pair in context.Actives.Where(pair => pair.Key == command))
        {
            pair.Key.OnRemove(context, pair.Value);
        }
    }

    private static CharacterEntity CreateCharacter(FakeShard shard)
    {
        var character = new CharacterEntity(shard, shard.GetNextGuid(0));
        character.SetCharacterState(CharacterStateData.CharacterStatus.Living, 0);
        shard.Entities.Add(character.EntityId, character);

        return character;
    }
}
