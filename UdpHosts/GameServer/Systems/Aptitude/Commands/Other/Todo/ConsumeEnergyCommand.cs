using System;
using GameServer.Enums;
using GameServer.StaticDB.Records.aptfs;

namespace GameServer.Systems.Aptitude.Commands.Other;

public class ConsumeEnergyCommand : Command, ICommand
{
    private ConsumeEnergyCommandDef Params;

    public ConsumeEnergyCommand(ConsumeEnergyCommandDef par)
    : base(par)
    {
        Params = par;
    }

    public bool Execute(Context context)
    {
        float amount = AbilitySystem.RegistryOp(context.Register, Params.Amount, (Operand)Params.AmountRegop);
        if (amount <= 0)
        {
            return true;
        }

        // The energy pool is server-tracked as an approximation of the client
        // simulated pool, so ConsumeEnergy keeps server-side requirements
        // (RequireEnergy/EnergyToDamage) consistent with the chain's costs.
        var state = context.Abilities.GetOrAddState(context.Self);
        state.Energy = Math.Max(0f, state.Energy - amount);

        if (Params.OnTargets == 1)
        {
            foreach (IAptitudeTarget target in context.Targets)
            {
                var targetState = context.Abilities.GetOrAddState(target);
                targetState.Energy = Math.Max(0f, targetState.Energy - amount);
            }
        }

        Logger.Debug("{Command} {CommandId} consumed {Amount} energy from {Self}", nameof(ConsumeEnergyCommand), Params.Id, amount, context.Self);
        return true;
    }
}
