using GameServer.StaticDB.Records.customdata;

namespace GameServer.Systems.Aptitude.Commands.Target;

public class TargetByExistsCommand : Command, ICommand
{
    private TargetByExistsCommandDef Params;

    public TargetByExistsCommand(TargetByExistsCommandDef par)
    : base(par)
    {
        Params = par;
    }

    // CommandType 134 ("Target - Filter Existing Objects") carries no
    // parameters: it keeps the targets that are still part of the shard and
    // drops the ones that are gone. Chains that hold on to a target over time
    // (effect update/duration chains, deployables, called down vehicles, NPC
    // chains) run this before doing anything with the target so they do not
    // act on entities that despawned or were destroyed in the meantime.
    //
    // "Exists" is the same test RequireInitiatorExistsCommand does: the entity
    // is still registered in the shard's entity table, which is where
    // EntityManager.Add/Remove keeps every aptitude-capable entity.
    //
    // The def has no fail-on-empty flag (unlike the other target filters), so
    // an empty result does not break the chain - the command keeps the
    // always-succeed behaviour of the placeholder it replaces and only the
    // target list changes.
    public bool Execute(Context context)
    {
        var previousTargets = context.Targets;
        var newTargets = new AptitudeTargets();
        int dropped = 0;

        foreach (IAptitudeTarget target in previousTargets)
        {
            if (target != null && context.Shard.Entities.ContainsKey(target.EntityId))
            {
                newTargets.Push(target);
            }
            else
            {
                dropped++;
                Logger.Debug(
                    "{Command} {CommandId} dropped {Target} because it no longer exists",
                    nameof(TargetByExistsCommand),
                    Params.Id,
                    target);
            }
        }

        context.FormerTargets = previousTargets;
        context.Targets = newTargets;

        return true;
    }
}
