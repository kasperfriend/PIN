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
        // Current energy is client simulated and not tracked server side yet,
        // so assume a full pool capped by MaxEnergyAllowed.
        float energy = Params.MaxEnergyAllowed;
        if (energy < Params.EnergyRequired)
        {
            return false;
        }

        float damage = Params.EnergyPerPoint > 0 ? energy / Params.EnergyPerPoint : energy;
        Logger.Debug("{Command} {CommandId} assumes full energy pool {Energy}, loading {Damage} damage into the register", nameof(EnergyToDamageCommand), Params.Id, energy, damage);

        context.FormerRegister = context.Register;
        context.Register = MathF.Round(damage);
        return true;
    }
}
