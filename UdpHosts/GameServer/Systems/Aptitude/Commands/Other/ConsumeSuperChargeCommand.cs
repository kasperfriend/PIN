using System;
using AeroMessages.GSS.Character.Controller;
using GameServer.Entities.Character;
using GameServer.Enums;
using GameServer.StaticDB.Records.aptfs;

namespace GameServer.Systems.Aptitude.Commands.Other;

public class ConsumeSuperChargeCommand : Command, ICommand
{
    private ConsumeSuperChargeCommandDef Params;

    public ConsumeSuperChargeCommand(ConsumeSuperChargeCommandDef par)
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
                // No controllers yet (character never became observable): nothing to burn.
                Logger.Warning("{Command} {CommandId} fails because the character has no supercharge gauge (controllers not initialized)", nameof(ConsumeSuperChargeCommand), Params.Id);
                return false;
            }

            var currentValue = character.Character_CombatController.SuperChargeProp.Value;

            var percent = AbilitySystem.RegistryOp(context.Register, Params.Percent, (Operand)Params.PercentRegop);

            // The gauge is 0..100, so the row's Percent IS the absolute amount to
            // burn. The old math took Percent OF THE CURRENT charge, which burnt
            // less and less as the gauge drained (a 100%-cost HKM at 55 charge
            // burnt exactly what was left instead of requiring a full gauge).
            //
            // The gauge is fed from damage events by DamageSystem
            // (SuperChargePerDamageDealt/Taken), so consuming here is paired.
            var value = percent;

            character.Character_CombatController.SuperChargeProp = new SuperChargeData()
               {
                   Value = Math.Max(0f, currentValue - value),
                   Op = (byte)Operand.ASSIGN,
               };

            return true;
        }

        Logger.Warning("{Command} {CommandId} fails because target is not a Character. If this is happening, we should investigate why.", nameof(ConsumeSuperChargeCommand), Params.Id);

        return false;
    }
}