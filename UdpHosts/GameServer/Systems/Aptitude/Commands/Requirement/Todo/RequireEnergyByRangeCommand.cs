using System.Numerics;
using GameServer.Enums;
using GameServer.StaticDB.Records.aptfs;

namespace GameServer.Systems.Aptitude.Commands.Requirement;

/// <summary>
/// "Requirement - Energy By Range": gates the chain on the current energy
/// being inside the SDB energy band [<c>MinEnergy</c>, <c>MaxEnergy</c>]
/// (unbounded when a bound is 0) and, when targets are present, on at least
/// one target being at [<c>MinRange</c>, <c>MaxRange</c>] distance from the
/// initiator. When <c>AlsoConsume</c> is set the minimum (scaled) amount is
/// spent as part of passing the requirement.
/// </summary>
public class RequireEnergyByRangeCommand : Command, ICommand
{
    private RequireEnergyByRangeCommandDef Params;

    public RequireEnergyByRangeCommand(RequireEnergyByRangeCommandDef par)
: base(par)
    {
        Params = par;
    }

    public bool Execute(Context context)
    {
        uint time = context.Shard.CurrentTime;
        var state = context.Abilities.GetOrAddState(context.Self);

        // Regenerate up to the present before checking, so the gate sees the
        // same pool the client simulates instead of the last tick snapshot.
        state.UpdateEnergy(time);

        float minEnergy = ApplyRegop(context, Params.MinEnergy);
        bool inEnergyBand = state.Energy >= minEnergy
            && (Params.MaxEnergy == 0f || state.Energy <= Params.MaxEnergy);

        bool inRange = true;
        if (Params.MinRange != 0f || Params.MaxRange != 0f)
        {
            if (context.Targets.Count > 0)
            {
                inRange = false;
                foreach (IAptitudeTarget target in context.Targets)
                {
                    float distance = Vector3.Distance(context.Initiator.Position, target.Position);
                    if ((Params.MinRange == 0f || distance >= Params.MinRange)
                        && (Params.MaxRange == 0f || distance <= Params.MaxRange))
                    {
                        inRange = true;
                        break;
                    }
                }
            }
            else
            {
                Logger.Debug(
                    "{Command} {CommandId} has range bounds but no targets; range check skipped",
                    nameof(RequireEnergyByRangeCommand),
                    Params.Id);
            }
        }

        bool result = inEnergyBand && inRange;
        if (!result)
        {
            Logger.Debug(
                "{Command} {CommandId} fails: {Self} energy {Energy} not in [{MinEnergy}, {MaxEnergy}] or no target in [{MinRange}, {MaxRange}]",
                nameof(RequireEnergyByRangeCommand),
                Params.Id,
                context.Self,
                state.Energy,
                minEnergy,
                Params.MaxEnergy,
                Params.MinRange,
                Params.MaxRange);
            return false;
        }

        if (Params.AlsoConsume == 1 && minEnergy > 0f)
        {
            if (!context.Abilities.TrySpendEnergy(context, context.Self, minEnergy, false, out var remaining))
            {
                // Keep this command an actual requirement even if the energy
                // pool changes between the range check and the spend.
                return false;
            }

            Logger.Debug(
                "{Command} {CommandId} consumed {Amount} energy from {Self}, {Remaining} remaining",
                nameof(RequireEnergyByRangeCommand),
                Params.Id,
                minEnergy,
                context.Self,
                remaining);
        }

        if (Params.AllowPrediction == 1)
        {
            Logger.Debug(
                "{Command} {CommandId} is allowed to be predicted by the client; the server-side check is authoritative",
                nameof(RequireEnergyByRangeCommand),
                Params.Id);
        }

        return true;
    }

    private float ApplyRegop(Context context, float value)
    {
        if (Params.AmountRegop == 0)
        {
            return value;
        }

        return AbilitySystem.RegistryOp(context.Register, value, (Operand)Params.AmountRegop);
    }
}
