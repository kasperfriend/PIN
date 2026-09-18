using GameServer.Entities.Character;
using GameServer.Enums;
using GameServer.StaticDB.Records.aptfs;

namespace GameServer.Systems.Aptitude.Commands.Requirement;

public class RequireSuperChargeCommand : Command, ICommand
{
    private RequireSuperChargeCommandDef Params;

    public RequireSuperChargeCommand(RequireSuperChargeCommandDef par)
        : base(par)
    {
        Params = par;
    }

    public bool Execute(Context context)
    {
        var target = context.Self;

        if (target is CharacterEntity character)
        {
            if (character.Character_CombatController?.SuperChargeProp == null)
            {
                // No controllers yet (character never became observable): no gauge, no gate.
                return false;
            }

            var percent = AbilitySystem.RegistryOp(context.Register, Params.Percent, (Operand)Params.PercentRegop);

            // The row asks for a fraction of the 0..100 gauge, so the row's Percent is
            // the absolute threshold. The old math took a fraction of the CURRENT
            // charge, which was vacuously true for any Percent <= 100. DamageSystem
            // feeds the gauge from damage events (SuperChargePerDamageDealt/Taken), so
            // gating on it does not brick HKMs.
            return character.Character_CombatController.SuperChargeProp.Value >= percent;
        }

        Logger.Warning("{Command} {CommandId} fails because target is not a Character. If this is happening, we should investigate why.", nameof(RequireSuperChargeCommand), Params.Id);

        return false;
    }
}