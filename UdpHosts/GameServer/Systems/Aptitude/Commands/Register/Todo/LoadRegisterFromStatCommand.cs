using GameServer.Entities.Character;
using GameServer.Enums;
using GameServer.StaticDB.Records.apt;

namespace GameServer.Systems.Aptitude.Commands.Register;

/// <summary>
/// Loads one of the character's aptitude stat modifiers into the register
/// (e.g. Energy / MaxEnergy / EnergyRechargeRate / CooldownModifier). The
/// effective modifier value is combined with the current register using the
/// record's Regop, which is how ability chain amounts (energy cost, damage,
/// cooldowns) scale with gear and active effects. The unmodified value is 1.0.
/// </summary>
public class LoadRegisterFromStatCommand : Command, ICommand
{
    private LoadRegisterFromStatCommandDef Params;

    public LoadRegisterFromStatCommand(LoadRegisterFromStatCommandDef par)
: base(par)
    {
        Params = par;
    }

    public bool Execute(Context context)
    {
        if (context.Self is not CharacterEntity character)
        {
            Logger.Warning(
                "{Command} {CommandId} fails because Self is not a Character",
                nameof(LoadRegisterFromStatCommand),
                Params.Id);
            return false;
        }

        var stat = (StatModifierIdentifier)Params.Stat;
        float statValue = character.GetCurrentStatModifierValue(stat);
        context.Register = AbilitySystem.RegistryOp(context.Register, statValue, (Operand)Params.Regop);
        Logger.Debug(
            "{Command} {CommandId}: stat {Stat} = {StatValue}, regop {Regop} => register {Register}",
            nameof(LoadRegisterFromStatCommand),
            Params.Id,
            Params.Stat,
            statValue,
            (Operand)Params.Regop,
            context.Register);
        return true;
    }
}
