using GameServer.StaticDB.Records.aptfs;

namespace GameServer.Systems.Aptitude.Commands.Requirement;

public class RequireHasEffectCommand : Command, ICommand
{
    private RequireHasEffectCommandDef Params;

    public RequireHasEffectCommand(RequireHasEffectCommandDef par)
    : base(par)
    {
        Params = par;
    }

    public bool Execute(Context context)
    {
        // Logger.Debug("EffectID: {EffectId}", Params.EffectId);
        bool result = false;

        // In an effect's own duration or update chain the requirement is asked about the entity
        // the effect runs on - context.Self. context.Targets there is the caller's passed-through
        // target list (ImpactApplyEffect PassTargets), i.e. the other side of the interaction:
        // the per-type interaction effects (vendor 279, doctor, ...) live on the player while
        // their target list still holds the NPC, and the NPC never carries the root "interacting"
        // effect 269 - the player does. Asking that list expired the channel on the first
        // duration tick, so every E-key interaction ended "cancelled - no content" and vendor
        // menus never opened. Activation-time requirements keep the target-list semantics (the
        // interact ability's branch asks the interact target whether it lacks effect 269).
        if (context.ExecutionHint is not ExecutionHint.DurationEffect and not ExecutionHint.UpdateEffect)
        {
            if (context.Targets.Count > 0)
            {
                uint matchCounter = 0;
                foreach (IAptitudeTarget target in context.Targets)
                {
                    if (!HasRequiredEffect(target, context))
                    {
                        result = false;
                        break;
                    }

                    matchCounter++;
                }

                if (matchCounter == context.Targets.Count)
                {
                    result = true;
                }
            }
        }
        else
        {
            result = HasRequiredEffect(context.Self, context);
        }

        if (Params.Negate == 1)
        {
            result = !result;
        }

        return result;
    }

    private bool HasRequiredEffect(IAptitudeTarget target, Context context)
    {
        // TODO: Handle Params.SameInitiator
        foreach (EffectState active in target.GetActiveEffects())
        {
            if (active == null)
            {
                continue;
            }

            if (active.Effect.Id == Params.EffectId && active.Stacks >= Params.StackCount)
            {
                if (Params.SameInitiator == 1 && context.Initiator != active.Context.Initiator)
                {
                    return false;
                }

                return true;
            }
        }

        return false;
    }
}
