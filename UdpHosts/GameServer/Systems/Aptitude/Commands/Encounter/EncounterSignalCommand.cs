using GameServer.Entities;
using GameServer.StaticDB.Records.customdata;

namespace GameServer.Systems.Aptitude.Commands.Encounter;

public class EncounterSignalCommand : Command, ICommand
{
    private EncounterSignalCommandDef Params;

    public EncounterSignalCommand(EncounterSignalCommandDef par)
: base(par)
    {
        Params = par;
    }

    public bool Execute(Context context)
    {
        // A target-only chain (e.g. one whose Self was left at the initiator's
        // player or an empty target) must not take down the chain: only entities
        // carry encounter components.
        if (context.Self is not BaseEntity self)
        {
            Logger.Debug(
                "{Command} {CommandId} has no entity Self ({SelfType}), nothing to signal",
                nameof(EncounterSignalCommand), Params.Id, context.Self?.GetType().Name ?? "null");
            return true;
        }

        if (self.Encounter != null && self.Encounter.Handles(EncounterComponent.Event.Signal))
        {
            self.Encounter.Instance.OnSignal();
        }

        return true;
    }
}