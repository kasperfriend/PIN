using System.Numerics;
using GameServer.StaticDB.Records.aptfs;

namespace GameServer.Systems.Aptitude.Commands.Requirement;

/// <summary>
/// CommandType 81 ("Requirement - In Range").
///
/// The requirement measures the distance between the entity running the chain
/// (<see cref="Context.Self"/>) and the chain's current targets and succeeds
/// only while every one of them is within <c>Range</c> metres. Abilities use
/// it to gate effects that must stay near their caster (tethered buffs, repair
/// or heal beams, interaction chains, NPC melee follow-ups); when the target
/// walks away the chain fails on the next update and the effect stops.
///
/// Unlike <c>TargetFilterByRange</c> this command does not modify the target
/// list, it only reports whether the chain may continue.
///
/// <c>Negate</c> inverts the result ("require out of range"), the same way the
/// other requirement commands treat the flag. <c>Useoffset</c> would measure
/// from the target offset set by <c>SetTargetOffset</c> (CommandType 70) which
/// is not implemented server-side yet, so the entity positions are used.
/// </summary>
public class RequireInRangeCommand : Command, ICommand
{
    private RequireInRangeCommandDef Params;

    public RequireInRangeCommand(RequireInRangeCommandDef par)
: base(par)
    {
        Params = par;
    }

    public bool Execute(Context context)
    {
        var source = context.Self ?? context.Initiator;

        if (source == null)
        {
            Logger.Warning("{Command} {CommandId} fails because the chain has no source entity.", nameof(RequireInRangeCommand), Params.Id);

            return Params.Negate == 1;
        }

        if (Params.Useoffset == 1)
        {
            Logger.Debug("[{Command} {CommandId}] target offsets are not implemented, measuring from entity positions", nameof(RequireInRangeCommand), Params.Id);
        }

        bool result = true;
        int checkedTargets = 0;

        foreach (IAptitudeTarget target in context.Targets)
        {
            if (target == null)
            {
                continue;
            }

            checkedTargets++;

            var distance = Vector3.Distance(source.Position, target.Position);

            if (distance > Params.Range)
            {
                Logger.Debug(
                    "[{Command} {CommandId}] {Target} is {Distance} away, out of range {Range}",
                    nameof(RequireInRangeCommand),
                    Params.Id,
                    target,
                    distance,
                    Params.Range);

                result = false;

                break;
            }
        }

        if (checkedTargets == 0)
        {
            // Nothing to measure against: the source is trivially within range
            // of itself, which keeps self-only chains running like they did
            // with the always-succeed placeholder.
            result = true;
        }

        if (Params.Negate == 1)
        {
            result = !result;
        }

        return result;
    }
}
