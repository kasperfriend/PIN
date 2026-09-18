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
            var currentValue = character.Character_CombatController.SuperChargeProp.Value;

            var percent = AbilitySystem.RegistryOp(context.Register, Params.Percent, (Operand)Params.PercentRegop);

            // The row asks for a fraction of the 0..100 gauge; the old math compared
            // against a fraction of the CURRENT charge, which was vacuously true for
            // any Percent <= 100.
            // NOTE: the factory still routes this command to the fail-open
            // placeholder on purpose - nothing generates supercharge yet (see
            // ConsumeSuperChargeCommand), so gating on the gauge would brick HKMs
            // after their first cast. This class is the ready-to-enable gate.
            return currentValue >= percent / 100f * 100f;
        }

        Logger.Warning("{Command} {CommandId} fails because target is not a Character. If this is happening, we should investigate why.", nameof(RequireSuperChargeCommand), Params.Id);

        return false;
    }
}