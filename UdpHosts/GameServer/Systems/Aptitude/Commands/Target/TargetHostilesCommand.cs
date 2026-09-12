using GameServer.Entities;
using GameServer.StaticDB.Records.aptfs;
using GameServer.Systems.Combat;

namespace GameServer.Systems.Aptitude.Commands.Target;

/// <summary>
///     Filters the current target list to only hostile targets (including neutral, matching the damage gate).
///     Used in ~1942 ability chains for damage and hostile-only effects. Previously a buggy placeholder
///     that cleared all targets.
/// </summary>
public class TargetHostilesCommand : Command, ICommand
{
    private static readonly HostilityResolver _hostility = new();

    private TargetHostilesCommandDef Params;

    public TargetHostilesCommand(TargetHostilesCommandDef par)
        : base(par)
    {
        Params = par;
    }

    public bool Execute(Context context)
    {
        var previousTargets = context.Targets;
        var newTargets = new AptitudeTargets();

        var reference = context.Self as BaseEntity;

        foreach (var target in previousTargets)
        {
            if (target == context.Self && Params.IncludeSelf == 0)
            {
                continue;
            }

            if (target == context.Initiator && Params.IncludeInitiator == 0)
            {
                continue;
            }

            if (context.Self != null && target == context.Self.Owner && Params.IncludeOwner == 0)
            {
                continue;
            }

            if (!context.Shard.Entities.TryGetValue(target.EntityId, out var entityObj) || entityObj is not BaseEntity targetEntity)
            {
                continue;
            }

            if (reference == null)
            {
                newTargets.Push(target);
                continue;
            }

            var stance = GetStanceSafe(reference, targetEntity);

            // Hostile filter: keep Hostile and Neutral (not Friendly, not Self)
            // This matches the damage gate which skips Friendly and Self.
            bool isHostile = stance == HostilityStance.Hostile || stance == HostilityStance.Neutral;

            if (!isHostile)
            {
                continue;
            }

            newTargets.Push(target);
        }

        context.FormerTargets = previousTargets;
        context.Targets = newTargets;

        if (Params.FailNoTargets == 1 && context.Targets.Count == 0)
        {
            return false;
        }

        return true;
    }

    private HostilityStance GetStanceSafe(BaseEntity reference, BaseEntity target)
    {
        try
        {
            return _hostility.GetStance(reference.HostilityInfo, target.HostilityInfo);
        }
        catch
        {
            if (reference.EntityId == target.EntityId)
            {
                return HostilityStance.Self;
            }

            if (reference.HostilityInfo.FactionId == target.HostilityInfo.FactionId)
            {
                return HostilityStance.Friendly;
            }

            return HostilityStance.Hostile;
        }
    }
}