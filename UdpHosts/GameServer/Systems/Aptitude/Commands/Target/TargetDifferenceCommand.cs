using System.Collections.Generic;
using GameServer.StaticDB.Records.apt;

namespace GameServer.Systems.Aptitude.Commands.Target;

/// <summary>
/// CommandType 101 ("Target - Difference").
///
/// Subtracts one of the chain's two target lists from the other: every target
/// that appears in both lists is dropped, so what is left is the set
/// difference. It carries no other parameters - the two flags only pick which
/// list is subtracted from which, and what the former list looks like
/// afterwards.
///
/// Two chain shapes in build <c>prod-1962</c> use it (21 nodes in
/// <c>apt::BaseCommandDef</c>, 22 defs):
///
/// <list type="bullet">
/// <item>
/// <description>
/// "Only the ones that just arrived" - periodic area chains that
/// <c>PopTargets</c> the set they hit on the previous tick into the former
/// list, re-run <c>TargetPBAE</c> into the current list, <c>PushTargets</c>
/// the fresh set for the next tick and then difference the two. What survives
/// are the entities that entered the area since the last tick, which is what
/// the following <c>HasTargetsDuration</c> / impulse / effect commands act on
/// (<c>ReplaceFormer</c> = 0, <c>SwapCurrentFormer</c> = 0).
/// </description>
/// </item>
/// <item>
/// <description>
/// "Only the outer ring" - layered <c>TargetConeAE</c> blasts that collect the
/// big volume, <c>TargetSwap</c> it into the former list, collect the smaller
/// inner volume into the current list and then difference the two so the inner
/// volume is excluded and each ring takes its own damage/knockback
/// (<c>ReplaceFormer</c> = 1, <c>SwapCurrentFormer</c> = 1). Here the
/// subtraction has to go the other way around, which is exactly what the swap
/// flag is for: the inner list is subtracted from the outer one.
/// </description>
/// </item>
/// </list>
///
/// Flag distribution in the SDB: 8x (0, 0), 6x (1, 0), 8x (1, 1); the
/// combination (0, 1) does not occur.
///
/// The placeholder this replaces never removed anything, so both shapes kept
/// their full target list: area chains re-applied their effect to everybody
/// still standing in the area on every tick instead of only to newcomers, and
/// ring blasts hit the inner volume once per ring.
/// </summary>
public class TargetDifferenceCommand : Command, ICommand
{
    private TargetDifferenceCommandDef Params;

    public TargetDifferenceCommand(TargetDifferenceCommandDef par)
    : base(par)
    {
        Params = par;
    }

    public bool Execute(Context context)
    {
        if (Params.SwapCurrentFormer == 1)
        {
            // Subtract the current list from the former one instead of the
            // other way around. The lists stay swapped, so the result ends up
            // in the current list like in the unswapped case.
            (context.Targets, context.FormerTargets) = (context.FormerTargets, context.Targets);
        }

        var minuend = context.Targets;
        var subtrahend = context.FormerTargets;

        var excluded = new HashSet<ulong>();

        foreach (IAptitudeTarget target in subtrahend)
        {
            if (target != null)
            {
                excluded.Add(target.EntityId);
            }
        }

        var difference = new AptitudeTargets();

        foreach (IAptitudeTarget target in minuend)
        {
            if (target == null)
            {
                continue;
            }

            if (excluded.Contains(target.EntityId))
            {
                Logger.Debug(
                    "{Command} {CommandId} dropped {Target} because it is in both target lists",
                    nameof(TargetDifferenceCommand),
                    Params.Id,
                    target);

                continue;
            }

            difference.Push(target);
        }

        if (Params.ReplaceFormer == 1)
        {
            // Keep the unfiltered list we subtracted from around as the former
            // list, the way the other target filters do.
            context.FormerTargets = new AptitudeTargets(minuend);
        }

        context.Targets = difference;

        // The def has no fail-on-empty flag, so an empty result does not break
        // the chain - the commands after it are guarded by HasTargetsDuration
        // in the chains that care.
        return true;
    }
}
