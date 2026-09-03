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
        var state = context.Abilities.GetOrAddState(context.Self);
        float required = AbilitySystem.RegistryOp(context.Register, Params.Amount, (Operand)Params.AmountRegop);
        bool hasEnergy = state.Energy >= required;

        bool result = Params.Negate == 1 ? !hasEnergy : hasEnergy;
        if (!result)
        {
            Logger.Debug("{Command} {CommandId} fails: {Self} has {Energy} energy, needs {Required}", nameof(RequireEnergyCommand), Params.Id, context.Self, state.Energy, required);
        }

        return result;
    }
}
