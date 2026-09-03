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
        float amount = AbilitySystem.RegistryOp(context.Register, Params.Amount, (Operand)Params.AmountRegop);
        if (Params.AmountRegop != 0 && context.Register == 0f)
        {
            Logger.Debug(
                "{Command} {CommandId} amount regop {Regop} evaluated against register 0; the chain likely needs a LoadRegister command before this node",
                nameof(ConsumeEnergyOverTimeCommand),
                Params.Id,
                (Operand)Params.AmountRegop);
        }

        if (amount <= 0f)
        {
            return true;
        }

        // Channelled drain: the duration chain executes this command once per
        // update tick, so each execution burns one tick's worth of energy.
        if (!context.Abilities.CanSpendEnergy(context.Self, amount, allowOvercharge: false, time: context.Shard.CurrentTime))
        {
            var state = context.Abilities.GetOrAddState(context.Self);
            Logger.Debug(
                "{Command} {CommandId} stopped: {Self} has {Energy} energy, needs {Amount}",
                nameof(ConsumeEnergyOverTimeCommand),
                Params.Id,
                context.Self,
                state.Energy,
                amount);
            return false;
        }

        if (!context.Abilities.TrySpendEnergy(context, context.Self, amount, false, out var remaining))
        {
            return false;
        }

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

        // Allow the final tick to consume the pool exactly. The next duration
        // execution will fail before spending when there is not enough left.
        return true;
    }
}
