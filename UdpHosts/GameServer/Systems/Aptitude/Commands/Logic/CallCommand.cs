using GameServer.StaticDB.Records.apt;

namespace GameServer.Systems.Aptitude.Commands.Logic;

public class CallCommand : Command, ICommand
{
    private CallCommandDef Params;

    public CallCommand(CallCommandDef par)
: base(par)
    {
        Params = par;
    }

    public bool Execute(Context context)
    {
        // Keep the called ability inside the root activation transaction. A
        // failed called chain must fail the root chain so its energy and
        // deferred cooldowns are rolled back together.
        return context.Abilities.HandleCalledAbility(context, Params.AbilityId);
    }
}