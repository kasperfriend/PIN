using GameServer.Enums;
using GameServer.StaticDB;
using GameServer.StaticDB.Records.apt;

namespace GameServer.Systems.Aptitude.Commands.Register;

/// <summary>
/// Loads the power rating of the ability module that activated the chain into
/// the register. Power rating is the item-level scalar Firefall uses to scale
/// ability base amounts (damage, cost, healing), so chain commands with a
/// multiplicative regop use it instead of a hardcoded 1.0.
/// </summary>
public class LoadRegisterFromModulePowerCommand : Command, ICommand
{
    private LoadRegisterFromModulePowerCommandDef Params;

    public LoadRegisterFromModulePowerCommand(LoadRegisterFromModulePowerCommandDef par)
: base(par)
    {
        Params = par;
    }

    public bool Execute(Context context)
    {
        float power = 1.0f;
        if (context.AbilityModuleId != 0)
        {
            var module = SDBInterface.GetAbilityModule(context.AbilityModuleId);
            if (module != null && module.PowerLevel > 0)
            {
                power = module.PowerLevel;
            }
            else
            {
                Logger.Debug(
                    "{Command} {CommandId} could not resolve module power for module {ModuleId}, using 1.0",
                    nameof(LoadRegisterFromModulePowerCommand),
                    Params.Id,
                    context.AbilityModuleId);
            }
        }
        else
        {
            Logger.Debug(
                "{Command} {CommandId} has no ability module in context, using 1.0",
                nameof(LoadRegisterFromModulePowerCommand),
                Params.Id);
        }

        // ModulePowerType describes which power source the chain wants; the
        // activated ability module is the only one available server-side today.
        if (Params.ModulePowerType != 0)
        {
            Logger.Debug(
                "{Command} {CommandId} uses the activated ability module power for ModulePowerType {Type}",
                nameof(LoadRegisterFromModulePowerCommand),
                Params.Id,
                Params.ModulePowerType);
        }

        context.Register = AbilitySystem.RegistryOp(context.Register, power, (Operand)Params.Regop);
        return true;
    }
}
