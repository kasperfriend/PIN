using GameServer.Entities.Character;
using GameServer.Enums;
using GameServer.Extensions;
using GameServer.StaticDB.Records.apt;

namespace GameServer.Systems.Aptitude.Commands.Register;

public class LoadRegisterFromLevelCommand : Command, ICommand
{
    private readonly LoadRegisterFromLevelCommandDef _params;

    public LoadRegisterFromLevelCommand(LoadRegisterFromLevelCommandDef parameters)
        : base(parameters)
    {
        _params = parameters;
    }

    public bool Execute(Context context)
    {
        // A few client DB chains (including the projectile impact chain that previously caused the
        // repeated 37408 exceptions) reach this command before their character controller exists.
        // A level is unavailable in that state; fail this chain node instead of dereferencing the
        // controller. The caller treats a false command result as a normal failed aptitude chain.
        if (context == null)
        {
            return false;
        }

        var source = _params.FromInitiator == 1 ? context.Initiator : context.Self;
        if (source is not CharacterEntity character || character.Character_BaseController == null)
        {
            if (OnceLog.ShouldLog((nameof(LoadRegisterFromLevelCommand), "no character level", Id)))
            {
                Logger.Warning(
                    "{Command} {CommandId} could not read a level from {Source}; ending this ability chain safely",
                    nameof(LoadRegisterFromLevelCommand),
                    Id,
                    source?.GetType().Name ?? "nothing");
            }

            return false;
        }

        context.Register = AbilitySystem.RegistryOp(
            context.Register,
            character.Character_BaseController.LevelProp,
            (Operand)_params.Regop);

        return true;
    }
}
