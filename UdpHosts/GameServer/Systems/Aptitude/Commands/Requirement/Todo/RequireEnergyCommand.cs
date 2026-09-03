using GameServer.Enums;
using GameServer.StaticDB.Records.aptfs;

namespace GameServer.Systems.Aptitude.Commands.Requirement;

public class RequireEnergyCommand : Command, ICommand
{
    private RequireEnergyCommandDef Params;

    public RequireEnergyCommand(RequireEnergyCommandDef par)
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

        float required = AbilitySystem.RegistryOp(context.Register, Params.Amount, (Operand)Params.AmountRegop);
        if (Params.AmountRegop != 0 && context.Register == 0f)
        {
            Logger.Debug(
                "{Command} {CommandId} amount regop {Regop} evaluated against register 0; the chain likely needs a LoadRegister command before this node",
                nameof(RequireEnergyCommand),
                Params.Id,
                (Operand)Params.AmountRegop);
        }

        bool hasEnergy = state.Energy >= required;

        bool result = Params.Negate == 1 ? !hasEnergy : hasEnergy;
        if (!result)
        {
            Logger.Debug(
                "{Command} {CommandId} fails: {Self} has {Energy} energy of {MaxEnergy}, needs {Required}",
                nameof(RequireEnergyCommand),
                Params.Id,
                context.Self,
                state.Energy,
                state.MaxEnergy,
                required);
        }

        return result;
    }
}
