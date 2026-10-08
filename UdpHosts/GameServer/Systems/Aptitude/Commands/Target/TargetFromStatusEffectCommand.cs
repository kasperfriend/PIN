using GameServer.StaticDB.Records.aptfs;

namespace GameServer.Systems.Aptitude.Commands.Target;

/// <summary>
///     <c>aptfs::TargetFromStatusEffectCommandDef</c>: builds the target list from the entities in
///     the shard that carry a named status effect. Unlike <c>TargetByEffect</c>, which filters the
///     list it is handed, this one starts from nothing - the chains that use it clear or never
///     populate the list first (the Teleport Beacon's recall runs
///     <c>TargetClear -> TargetFromStatusEffect -> Teleport</c>), which is why it scans the shard
///     instead of filtering.
///     <para>
///         Every one of the 72 SDB rows carries a nonzero <c>StatusfxId</c>, and
///         <c>AlsoInitiator</c> is 0 in 67 of them. The command used to read neither: it pushed the
///         initiator when <c>AlsoInitiator</c> was set and returned, so the 67 rows that ask for the
///         effect carriers got an empty target list and everything downstream of them - the
///         <c>Teleport</c>, the <c>DestroyAbilityObject</c>, the <c>ImpactRemoveEffect</c> - ran
///         against nothing.
///     </para>
/// </summary>
public class TargetFromStatusEffectCommand : Command, ICommand
{
    private TargetFromStatusEffectCommandDef Params;

    public TargetFromStatusEffectCommand(TargetFromStatusEffectCommandDef par)
        : base(par)
    {
        Params = par;
    }

    public bool Execute(Context context)
    {
        var previousTargets = context.Targets;
        var newTargets = new AptitudeTargets();

        var initiator = context.Initiator;
        if (Params.AlsoInitiator == 1 && initiator != null)
        {
            newTargets.Push(initiator);
        }

        foreach (var pair in context.Shard.Entities)
        {
            if (pair.Value is not IAptitudeTarget target)
            {
                continue;
            }

            // The initiator was already pushed above; keyed by entity id because a direct target and
            // a splash target can arrive as different entities of the same game object.
            if (initiator != null && target.EntityId == initiator.EntityId)
            {
                continue;
            }

            foreach (EffectState active in target.GetActiveEffects())
            {
                if (active != null && active.Effect.Id == Params.StatusfxId)
                {
                    newTargets.Push(target);
                    break;
                }
            }
        }

        context.FormerTargets = previousTargets;
        context.Targets = newTargets;

        return true;
    }
}
