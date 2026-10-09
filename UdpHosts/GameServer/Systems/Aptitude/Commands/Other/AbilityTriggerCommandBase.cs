using GameServer.StaticDB.Records;
using GameServer.StaticDB.Records.customdata;

namespace GameServer.Systems.Aptitude.Commands.Other;

/// <summary>
///     Shared behaviour for the <c>Register*TriggerCommand</c> family
///     (<c>RegisterAbilityTrigger</c>, <c>RegisterTimedTrigger</c>, <c>RegisterEffectTagTrigger</c>,
///     <c>RegisterHitTagTypeTrigger</c>). All four defs are id-only in both the clientdb and PIN's
///     <c>customdata</c> records — there is no <c>aptgss::</c> trigger table, no <c>Trigger</c> message
///     in AeroMessages, and no table with a <c>trigger</c> column outside
///     <c>dbdialogdata::DialogScript</c> — so a row can say only that a trigger exists.
///     <para>
///         What the trigger <em>does</em> is taken from the activation that installed it, and the
///         trigger lives as long as the effect that carried the command. See
///         <see cref="AbilityTriggerRegistration" /> for the chain evidence behind that reading.
///     </para>
/// </summary>
public abstract class AbilityTriggerCommandBase : Command, ICommand
{
    private readonly ICommandDef _params;

    protected AbilityTriggerCommandBase(ICommandDef par)
        : base(par)
    {
        _params = par;
    }

    /// <summary>True for <c>RegisterTimedTrigger</c>, which also fires when the carrying effect ends.</summary>
    protected abstract bool Timed { get; }

    public bool Execute(Context context)
    {
        if (context.Self == null)
        {
            Logger.Debug("{Command} {CommandId}: no self to install the trigger on", GetType().Name, _params.Id);
            return true;
        }

        context.Actives.Add(this, new AbilityTriggerActiveContext());

        return true;
    }

    public void OnApply(Context context, ICommandActiveContext activeCommandContext)
    {
        if (activeCommandContext is not AbilityTriggerActiveContext active || context.Self == null)
        {
            return;
        }

        active.Registration = context.Abilities?.RegisterAbilityTrigger(
            context.Self,
            context.AbilityId,
            context.AbilityModuleId,
            context.ChainId,
            Timed,
            _params.Id);
    }

    public void OnRemove(Context context, ICommandActiveContext activeCommandContext)
    {
        if (activeCommandContext is not AbilityTriggerActiveContext { Registration: not null } active)
        {
            return;
        }

        // A timed trigger is a fuse: the end of the effect that laid it is when it burns out, so the
        // ability system fires it on the way out rather than discarding it.
        context.Abilities?.UnregisterAbilityTrigger(active.Registration, context);
        active.Registration = null;
    }

    private class AbilityTriggerActiveContext : ICommandActiveContext
    {
        public AbilityTriggerRegistration Registration;
    }
}
