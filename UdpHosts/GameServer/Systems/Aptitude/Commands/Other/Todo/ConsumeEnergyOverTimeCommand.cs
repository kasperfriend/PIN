using GameServer.Enums;
using GameServer.StaticDB.Records.aptfs;

namespace GameServer.Systems.Aptitude.Commands.Other;

public class ConsumeEnergyOverTimeCommand : Command, ICommand
{
    private ConsumeEnergyOverTimeCommandDef Params;

    public ConsumeEnergyOverTimeCommand(ConsumeEnergyOverTimeCommandDef par)
: base(par)
    {
        Params = par;
    }

    public bool Execute(Context context)
    {
        uint time = context.Shard.CurrentTime;
        float amount = AbilitySystem.RegistryOp(context.Register, Params.Amount, (Operand)Params.AmountRegop);
        if (amount <= 0f)
        {
            return true;
        }

        // Channelled drain: the duration chain executes this command once per
        // update tick, so each execution burns one tick's worth of energy.
        var state = context.Abilities.GetOrAddState(context.Self);
        float remaining = state.SpendEnergy(amount, time, allowOvercharge: false);
        Logger.Debug(
            "{Command} {CommandId} drained {Amount} energy from {Self}, {Remaining} remaining",
            nameof(ConsumeEnergyOverTimeCommand),
            Params.Id,
            amount,
            context.Self,
            remaining);

        if (Params.AllowPrediction == 1)
        {
            Logger.Debug(
                "{Command} {CommandId} is allowed to be predicted by the client; the server-side drain is authoritative",
                nameof(ConsumeEnergyOverTimeCommand),
                Params.Id);
        }

        // The pool is empty: there is nothing left to burn, so fail the
        // duration chain and let the channel/effect end like the client does.
        return remaining > 0f;
    }
}
