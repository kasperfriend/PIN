using System;
using GameServer.Entities.Character;
using GameServer.Entities.Deployable;
using GameServer.StaticDB.Records.customdata;

namespace GameServer.Systems.Aptitude.Commands.Other;

/// <summary>
/// CommandType 247 ("Set Glider Parameters").
///
/// Writes the <c>dbcharacter::GliderParameters</c> row the client uses as a flight model for the lifetime of an
/// effect. Glider launch stages can overlap while one effect hands flight to another, so profile writes use a
/// uniquely-owned temporary layer. Removing one stage cannot restore an older profile over a newer stage, and
/// removing the final stage hands the profile that existed before the launch back to the character.
///
/// Rows of <c>aptgss::SetGliderParametersCommandDef</c> without a value leave the profile alone.
/// </summary>
public class SetGliderParametersCommand : Command, ICommand
{
    private readonly SetGliderParametersCommandDef _params;

    public SetGliderParametersCommand(SetGliderParametersCommandDef parameters)
        : base(parameters)
    {
        _params = parameters;
    }

    public bool Execute(Context context)
    {
        // An incomplete/invalid row must not claim a layer: removing it later would otherwise
        // overwrite a valid profile granted by another active effect.
        if (_params.Value is not > 0)
        {
            return true;
        }

        var character = context.Self as CharacterEntity ?? (context.Self as DeployableEntity)?.Owner;
        if (character != null)
        {
            context.Actives.Add(this, new SetGliderParametersActiveContext());
        }
        else
        {
            Logger.Debug("[{Command} {CommandId}] does nothing because self is {SelfType}",
                nameof(SetGliderParametersCommand), _params.Id, context.Self?.GetType().Name ?? "nothing");
        }

        return true;
    }

    public void OnApply(Context context, ICommandActiveContext activeCommandContext)
    {
        if (activeCommandContext is not SetGliderParametersActiveContext active)
        {
            return;
        }

        var character = context.Self as CharacterEntity ?? (context.Self as DeployableEntity)?.Owner;
        if (character != null)
        {
            character.ApplyTemporaryGliderProfileId((uint)_params.Value, active.OverrideId);
        }
    }

    public void OnRemove(Context context, ICommandActiveContext activeCommandContext)
    {
        if (activeCommandContext is not SetGliderParametersActiveContext active)
        {
            return;
        }

        var character = context.Self as CharacterEntity ?? (context.Self as DeployableEntity)?.Owner;
        if (character != null)
        {
            character.RemoveTemporaryGliderProfileId(active.OverrideId);
        }
    }

    /// <summary>Identity of this command instance's lifetime profile layer.</summary>
    private sealed class SetGliderParametersActiveContext : ICommandActiveContext
    {
        public Guid OverrideId { get; } = Guid.NewGuid();
    }
}
