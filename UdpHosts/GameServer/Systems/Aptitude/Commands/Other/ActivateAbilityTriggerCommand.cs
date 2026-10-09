using GameServer.StaticDB.Records.customdata;

namespace GameServer.Systems.Aptitude.Commands.Other;

/// <summary>
///     <c>customdata::ActivateAbilityTriggerCommandDef</c> (169 rows): fires the ability triggers
///     installed on an entity. Each one starts the ability activation that installed it.
///     <para>
///         It fires the triggers on the chain's current target list, falling back to the entity the
///         chain is running on when the list is empty — 44 of the 169 rows end their chain here and
///         many of the others run after a <c>TargetSelf</c>/<c>TargetInitiator</c>, so both shapes
///         occur in the data. It follows <c>TimeCooldown</c> in 42 rows, which is the cooldown guarding
///         the ability activation this command starts.
///     </para>
/// </summary>
public class ActivateAbilityTriggerCommand : Command, ICommand
{
    private ActivateAbilityTriggerCommandDef Params;

    public ActivateAbilityTriggerCommand(ActivateAbilityTriggerCommandDef par)
        : base(par)
    {
        Params = par;
    }

    public bool Execute(Context context)
    {
        int fired = 0;

        foreach (var target in context.Targets)
        {
            fired += context.Abilities?.ActivateAbilityTriggers(target, context) ?? 0;
        }

        if (fired == 0 && context.Self != null)
        {
            // No targets in the chain, or none of them carry a trigger: the entity running the chain is
            // the trigger's owner in the common case.
            fired += context.Abilities?.ActivateAbilityTriggers(context.Self, context) ?? 0;
        }

        Logger.Debug("ActivateAbilityTrigger {CommandId}: fired {Count} trigger(s)", Params.Id, fired);

        return true;
    }
}
