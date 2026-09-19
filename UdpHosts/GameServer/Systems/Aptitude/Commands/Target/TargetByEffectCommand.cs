using System.Linq;
using GameServer.StaticDB.Records.aptfs;

namespace GameServer.Systems.Aptitude.Commands.Target;

public class TargetByEffectCommand : Command, ICommand
{
    private TargetByEffectCommandDef Params;

    public TargetByEffectCommand(TargetByEffectCommandDef par)
: base(par)
    {
        Params = par;
    }

    public bool Execute(Context context)
    {
        // Params.FilterList (1 in 1086 instances, 0 in 6) stays undecoded: every row
        // we can load behaves like a filter over the input target list, so both forms
        // take the same path until a counterexample shows up.
        var previousTargets = context.Targets;
        var newTargets = new AptitudeTargets();

        foreach (IAptitudeTarget target in previousTargets)
        {
            // match = the target carries the effect at the required stack count; with
            // SameInitiator the (coalesced) application must have been initiated by
            // this chain's initiator. Negate then flips the whole question.
            //
            // The old per-effect flip of "condition" broke both axes: a Negate row
            // kept a target that HAD the required effect as long as it carried any
            // second, unrelated effect, and the non-negated SameInitiator branch
            // kept targets whose matching effect came from a DIFFERENT initiator.
            bool match = false;
            foreach (EffectState active in target.GetActiveEffects())
            {
                if (active == null)
                {
                    continue;
                }

                if (Params.EffectId != active.Effect.Id || active.Stacks < Params.StackCount)
                {
                    continue;
                }

                if (Params.SameInitiator == 1 && !WasAppliedBy(active, context.Initiator))
                {
                    continue;
                }

                match = true;
                break;
            }

            if (Params.Negate == 1)
            {
                match = !match;
            }

            if (match)
            {
                newTargets.Push(target);
            }
        }

        context.FormerTargets = previousTargets;
        context.Targets = newTargets;

        if (Params.FailNoTargets == 1 && context.Targets.Count == 0)
        {
            return false;
        }

        return true;
    }

    private static bool WasAppliedBy(EffectState active, IAptitudeTarget initiator)
    {
        // Same rule as RequireHasEffect: the replicated status slot coalesces every
        // application of the effect id, so consult the initiating context and all
        // stacked ones - any application from this initiator counts.
        return active.Context?.Initiator == initiator
            || active.StackedContexts.Any(stacked => stacked.Initiator == initiator);
    }
}