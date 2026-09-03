using System;
using GameServer.StaticDB.Records.aptfs;

namespace GameServer.Systems.Aptitude.Commands.Damage;

public class EnergyToDamageCommand : Command, ICommand
{
    private EnergyToDamageCommandDef Params;

    public EnergyToDamageCommand(EnergyToDamageCommandDef par)
    : base(par)
    {
        Params = par;
    }

    public bool Execute(Context context)
    {
        uint time = context.Shard.CurrentTime;
        var state = context.Abilities.GetOrAddState(context.Self);
        state.UpdateEnergy(time);

        // Convert the current server-tracked pool (not the cap) to damage, so
        // an ability that scales off energy does not assume a full pool.
        if (state.Energy < Params.EnergyRequired)
        {
            Logger.Debug(
                "{Command} {CommandId} fails: {Self} has {Energy} energy, needs {Required}",
                nameof(EnergyToDamageCommand),
                Params.Id,
                context.Self,
                state.Energy,
                Params.EnergyRequired);
            return false;
        }

        float usable = Math.Min(state.Energy, Params.MaxEnergyAllowed);
        float damage = Params.EnergyPerPoint > 0 ? usable / Params.EnergyPerPoint : usable;
        Logger.Debug(
            "{Command} {CommandId} converts {Energy} energy to {Damage} damage",
            nameof(EnergyToDamageCommand),
            Params.Id,
            usable,
            damage);

        context.FormerRegister = context.Register;
        context.Register = MathF.Round(damage);
        return true;
    }
}
