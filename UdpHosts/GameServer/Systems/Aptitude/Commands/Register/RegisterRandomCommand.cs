using System;
using GameServer.Enums;
using GameServer.StaticDB.Records.apt;

namespace GameServer.Systems.Aptitude.Commands.Register;

public class RegisterRandomCommand : Command, ICommand
{
    private RegisterRandomCommandDef Params;

    public RegisterRandomCommand(RegisterRandomCommandDef par)
        : base(par)
    {
        Params = par;
    }

    public bool Execute(Context context)
    {
        float prevValue = context.Register;

        // Command instances are shared through the chain cache and can execute on the
        // packet thread and the shard thread at the same time: Random.Shared is the
        // thread-safe source, an instance field Random is not.
        float rand = Random.Shared.NextSingle();
        float range = Params.MaxValue - Params.MinValue;
        float randValue = Params.MinValue + (range * rand);
        context.Register = AbilitySystem.RegistryOp(prevValue, randValue, (Operand)Params.Regop);

        Logger.Debug("{Command} {CommandId}: ({prevValue}, {randValue} ({Min} - {Max}), {op}) => {register}", nameof(RegisterRandomCommand), Params.Id, prevValue, randValue, Params.MinValue, Params.MaxValue, (Operand)Params.Regop, context.Register);

        return true;
    }
}