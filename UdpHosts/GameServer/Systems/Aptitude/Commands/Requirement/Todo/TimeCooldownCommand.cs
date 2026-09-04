using GameServer.StaticDB.Records.apt;

namespace GameServer.Systems.Aptitude.Commands.Requirement;

public class TimeCooldownCommand : Command, ICommand
{
    private TimeCooldownCommandDef Params;

    public TimeCooldownCommand(TimeCooldownCommandDef par)
    : base(par)
    {
        Params = par;
    }

    public bool Execute(Context context)
    {
        uint time = context.Shard.CurrentTime;
        // Cooldown state belongs to the caster even when a nested effect has
        // temporarily changed Self to a target.
        var state = context.Abilities.GetOrAddState(context.ActivationInitiator);

        bool checkLocal = Params.CheckLocal == 1 && context.AbilityId != 0;
        bool checkCategory = Params.CheckCategory == 1 && Params.Category != 0;
        bool checkGlobal = Params.CheckGlobal == 1;

        // Requirement: fail while a matching cooldown is still running, which
        // aborts the rest of the chain (no double activation / re-cast).
        if (checkGlobal && state.IsGlobalBlocked(time))
        {
            Logger.Debug("{Command} {CommandId} fails: global cooldown active", nameof(TimeCooldownCommand), Params.Id);
            return false;
        }

        if (checkCategory && state.IsCategoryBlocked(Params.Category, time))
        {
            Logger.Debug("{Command} {CommandId} fails: cooldown active for category {Category}", nameof(TimeCooldownCommand), Params.Id, Params.Category);
            return false;
        }

        if (checkLocal && state.IsAbilityBlocked(context.AbilityId, time))
        {
            Logger.Debug("{Command} {CommandId} fails: cooldown active for ability {AbilityId}", nameof(TimeCooldownCommand), Params.Id, context.AbilityId);
            return false;
        }

        // Also act as the cooldown definition when the chain carries no
        // dedicated InflictCooldown command: opening the gate starts the timer
        // so repeated activations inside the window are blocked.
        if (Params.Duration != 0)
        {
            if (!checkLocal && !checkCategory && !checkGlobal)
            {
                checkLocal = context.AbilityId != 0;
            }

            if (checkLocal)
            {
                state.StartCooldown(AbilityCooldownKind.Local, context.AbilityId, 0, Params.Duration, time);
            }

            if (checkCategory)
            {
                state.StartCooldown(AbilityCooldownKind.Category, 0, Params.Category, Params.Duration, time);
            }

            if (checkGlobal)
            {
                state.StartCooldown(AbilityCooldownKind.Global, 0, 0, Params.Duration, time);
            }
        }

        return true;
    }
}
