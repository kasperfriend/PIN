using GameServer.Entities;
using GameServer.StaticDB.Records.aptfs;
using GameServer.Systems.Combat;

namespace GameServer.Systems.Aptitude.Commands.Target;

/// <summary>
///     Filters the current target list to only friendly targets (including self and neutral, matching the heal gate).
///     Used in ~351 ability chains for heals, buffs and friendly-only effects.
/// </summary>
public class TargetFriendliesCommand : Command, ICommand
{
    private static readonly HostilityResolver _hostility = new();

    private TargetFriendliesCommandDef Params;

    public TargetFriendliesCommand(TargetFriendliesCommandDef par)
        : base(par)
    {
        Params = par;
    }

    public bool Execute(Context context)
    {
        var previousTargets = context.Targets;
        var newTargets = new AptitudeTargets();

        // Reference for hostility is Self (the entity running the chain)
        var reference = context.Self as BaseEntity;

        foreach (var target in previousTargets)
        {
            // Inclusion flags: skip self/initiator/owner when their flag is 0
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

            // Resolve target entity for hostility lookup
            if (!context.Shard.Entities.TryGetValue(target.EntityId, out var entityObj) || entityObj is not BaseEntity targetEntity)
            {
                // Target no longer exists, drop it (same as TargetByExists)
                continue;
            }

            if (reference == null)
            {
                // No reference, keep target (conservative)
                newTargets.Push(target);
                continue;
            }

            var stance = GetStanceSafe(reference, targetEntity);

            // Friendly filter: keep Friendly, Self, Neutral (i.e. not Hostile)
            // This matches the heal gate which skips only Hostile.
            // Self is explicitly Friendly for this purpose.
            bool isFriendly = stance == HostilityStance.Friendly || stance == HostilityStance.Self || stance == HostilityStance.Neutral;

            if (!isFriendly)
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
            // Fallback when SDB not loaded (unit tests): same faction = Friendly, different = Hostile, same entity = Self
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