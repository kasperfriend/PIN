using System;
using AeroMessages.GSS.Character.Controller;
using GameServer.Entities.Character;
using GameServer.StaticDB.Records.customdata;

namespace GameServer.Systems.Aptitude.Commands.Self;

/// <summary>
/// CommandType ("Modify Permission"), the command that grants and takes away the client-side permissions of a
/// character (gliding, the glider HUD, the jetpack, ...).
///
/// The flags are one replicated value, but several effects can own the same flag at once (for example, two
/// stages of a glider handoff). A command therefore writes a uniquely-owned temporary layer on the character,
/// rather than restoring the value it happened to observe on apply. Removing an earlier effect then leaves a
/// newer grant in place; removing the last one restores the pre-effect value. This prevents a completed glide
/// from inheriting an old temporary glider/HUD grant and starting another glide on the next jump.
/// </summary>
public class ModifyPermissionCommand : Command, ICommand
{
    private readonly ModifyPermissionCommandDef _params;

    public ModifyPermissionCommand(ModifyPermissionCommandDef parameters)
        : base(parameters)
    {
        _params = parameters;
    }

    public bool Execute(Context context)
    {
        if (context.Self is CharacterEntity)
        {
            context.Actives.Add(this, new ModifyPermissionActiveContext());
        }
        else
        {
            Logger.Debug("[{Command} {CommandId}] does nothing because self is {SelfType}",
                nameof(ModifyPermissionCommand), _params.Id, context.Self?.GetType().Name ?? "nothing");
        }

        return true;
    }

    public void OnApply(Context context, ICommandActiveContext activeCommandContext)
    {
        if (context.Self is not CharacterEntity character || activeCommandContext is not ModifyPermissionActiveContext active)
        {
            return;
        }

        if (_params.Glider != null)
        {
            character.ApplyTemporaryPermissionFlag(PermissionFlagsData.CharacterPermissionFlags.glider, (bool)_params.Glider, active.OverrideId);
        }

        if (_params.GliderHud != null)
        {
            character.ApplyTemporaryPermissionFlag(PermissionFlagsData.CharacterPermissionFlags.glider_hud, (bool)_params.GliderHud, active.OverrideId);
        }

        if (_params.Jetpack != null)
        {
            character.ApplyTemporaryPermissionFlag(PermissionFlagsData.CharacterPermissionFlags.jetpack, (bool)_params.Jetpack, active.OverrideId);
        }
    }

    public void OnRemove(Context context, ICommandActiveContext activeCommandContext)
    {
        if (context.Self is not CharacterEntity character || activeCommandContext is not ModifyPermissionActiveContext active)
        {
            return;
        }

        if (_params.Glider != null)
        {
            character.RemoveTemporaryPermissionFlag(PermissionFlagsData.CharacterPermissionFlags.glider, active.OverrideId);
        }

        if (_params.GliderHud != null)
        {
            character.RemoveTemporaryPermissionFlag(PermissionFlagsData.CharacterPermissionFlags.glider_hud, active.OverrideId);
        }

        if (_params.Jetpack != null)
        {
            character.RemoveTemporaryPermissionFlag(PermissionFlagsData.CharacterPermissionFlags.jetpack, active.OverrideId);
        }
    }

    /// <summary>Identity of this command instance's lifetime layer.</summary>
    private sealed class ModifyPermissionActiveContext : ICommandActiveContext
    {
        public Guid OverrideId { get; } = Guid.NewGuid();
    }
}
