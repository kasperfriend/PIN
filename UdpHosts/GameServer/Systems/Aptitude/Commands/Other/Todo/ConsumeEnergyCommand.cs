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
            Logger.Warning(
                "{Command} {CommandId} resolved an energy cost of {Amount} (register {Register}); no energy was consumed",
                nameof(ConsumeEnergyCommand), Params.Id, amount, context.Register);
            return true;
        }

        bool allowOvercharge = Params.AllowOvercharge == 1;
        if (Params.OnTargets == 1)
        {
            // Preflight every target first. Without this pass a drain over a
            // group could spend from early targets and then fail on a later
            // target, leaving a partially applied command.
            var targets = context.Targets.ToArray();
            foreach (IAptitudeTarget target in targets)
            {
                if (!context.Abilities.CanSpendEnergy(target, amount, allowOvercharge, context.Shard.CurrentTime))
                {
                    Logger.Debug(
                        "{Command} {CommandId} rejected: target {Target} has insufficient energy for {Amount}",
                        nameof(ConsumeEnergyCommand),
                        Params.Id,
                        target,
                        amount);
                    return false;
                }
            }

            foreach (IAptitudeTarget target in targets)
            {
                if (!context.Abilities.TrySpendEnergy(context, target, amount, allowOvercharge, out var remaining))
                {
                    return false;
                }

                Logger.Information(
                    "{Command} {CommandId} consumed {Amount} energy from target {Target}, {Remaining} remaining",
                    nameof(ConsumeEnergyCommand),
                    Params.Id,
                    amount,
                    target,
                    remaining);
            }
        }
        else
        {
            if (!context.Abilities.CanSpendEnergy(context.Self, amount, allowOvercharge, context.Shard.CurrentTime))
            {
                var state = context.Abilities.GetOrAddState(context.Self);
                Logger.Debug(
                    "{Command} {CommandId} rejected: {Self} has {Energy} energy, needs {Amount}",
                    nameof(ConsumeEnergyCommand),
                    Params.Id,
                    context.Self,
                    state.Energy,
                    amount);
                return false;
            }

            if (!context.Abilities.TrySpendEnergy(context, context.Self, amount, allowOvercharge, out var remaining))
            {
                return false;
            }

            Logger.Information(
                "{Command} {CommandId} consumed {Amount} energy from {Self}, {Remaining} remaining",
                nameof(ConsumeEnergyCommand),
                Params.Id,
                amount,
                context.Self,
                remaining);
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
