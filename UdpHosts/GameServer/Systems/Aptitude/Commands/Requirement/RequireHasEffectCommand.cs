using System.Linq;
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
        foreach (EffectState active in target.GetActiveEffects())
        {
            if (active == null)
            {
                continue;
            }

            if (active.Effect.Id == Params.EffectId && active.Stacks >= Params.StackCount)
            {
                if (Params.SameInitiator == 1 && !WasAppliedBy(active, context.Initiator))
                {
                    continue;
                }

                return true;
            }
        }

        return false;
    }

    private static bool WasAppliedBy(EffectState active, IAptitudeTarget initiator)
    {
        // The shared status-effect slot coalesces every application of the effect id, so the
        // same-initiator question has to consult the initiating context and all stacked
        // ones: any application from this initiator passes the gate, regardless of who
        // applied a different stack of the same effect.
        return active.Context?.Initiator == initiator
            || active.StackedContexts.Any(stacked => stacked.Initiator == initiator);
    }
}
