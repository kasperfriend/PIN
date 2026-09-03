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
        uint time = context.Shard.CurrentTime;
        float amount = AbilitySystem.RegistryOp(context.Register, Params.Amount, (Operand)Params.AmountRegop);
        if (Params.AmountRegop != 0 && context.Register == 0f)
        {
            Logger.Debug(
                "{Command} {CommandId} amount regop {Regop} evaluated against register 0; the chain likely needs a LoadRegister command before this node",
                nameof(ConsumeEnergyCommand),
                Params.Id,
                (Operand)Params.AmountRegop);
        }

        if (amount <= 0f)
        {
            return true;
        }

        // The energy pool is server-tracked as an approximation of the client
        // simulated pool, so ConsumeEnergy keeps server-side requirements
        // (RequireEnergy / EnergyToDamage) consistent with the chain's costs.
        bool allowOvercharge = Params.AllowOvercharge == 1;
        if (Params.OnTargets == 1)
        {
            // The cost is charged to the targets (e.g. energy drain abilities)
            // instead of the caster.
            foreach (IAptitudeTarget target in context.Targets)
            {
                var targetState = context.Abilities.GetOrAddState(target);
                targetState.SpendEnergy(amount, time, allowOvercharge);
                Logger.Debug(
                    "{Command} {CommandId} consumed {Amount} energy from target {Target}, {Remaining} remaining",
                    nameof(ConsumeEnergyCommand),
                    Params.Id,
                    amount,
                    target,
                    targetState.Energy);
            }
        }
        else
        {
            var state = context.Abilities.GetOrAddState(context.Self);
            state.SpendEnergy(amount, time, allowOvercharge);
            Logger.Debug(
                "{Command} {CommandId} consumed {Amount} energy from {Self}, {Remaining} remaining",
                nameof(ConsumeEnergyCommand),
                Params.Id,
                amount,
                context.Self,
                state.Energy);
        }

        if (Params.AllowPrediction == 1)
        {
            Logger.Debug(
                "{Command} {CommandId} is allowed to be predicted by the client; the server-side cost is authoritative",
                nameof(ConsumeEnergyCommand),
                Params.Id);
        }

        return true;
    }
}
