using GameServer.Entities;
using GameServer.StaticDB.Records.aptfs;
using GameServer.Systems.Combat;

namespace GameServer.Systems.Aptitude.Commands.Target;

/// <summary>
///     Generic hostility filter used in ~389 ability chains. Filters existing targets by hostility stance
///     relative to Self or Initiator (CompareFromInitiator), with FilterType mapping and ExcludeMode inversion.
///     <para>
///     FilterType mapping (inferred from SDB usage and heal/damage gates, documented as assumption):
///     0 = Friendly, 1 = Hostile, 2 = Neutral, 3 = Self, 4 = Friendly or Self, 5 = Any (all).
///     IncludeNormalOnly (when 1) restricts to Neutral only, matching observed rows where filter_type=5 + include_normal_only=1.
///     ExcludeMode (when 1) inverts the match.
///     </para>
/// </summary>
public class TargetByHostilityCommand : Command, ICommand
{
    private static readonly HostilityResolver _hostility = new();

    private TargetByHostilityCommandDef Params;

    public TargetByHostilityCommand(TargetByHostilityCommandDef par)
        : base(par)
    {
        Params = par;
    }

    public bool Execute(Context context)
    {
        var previousTargets = context.Targets;
        var newTargets = new AptitudeTargets();

        // Reference for hostility comparison
        BaseEntity reference = null;
        if (Params.CompareFromInitiator == 1)
        {
            reference = context.Initiator as BaseEntity;
        }

        reference ??= context.Self as BaseEntity;

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

            bool matches;
            if (Params.IncludeNormalOnly == 1)
            {
                // Only neutral passes when this flag is set
                if (reference == null)
                {
                    matches = false;
                }
                else
                {
                    var stance = GetStanceSafe(reference, targetEntity);
                    matches = stance == HostilityStance.Neutral;
                }
            }
            else
            {
                matches = MatchesFilterType(reference, targetEntity, Params.FilterType);
            }

            // ExcludeMode inverts
            if (Params.ExcludeMode == 1)
            {
                matches = !matches;
            }

            if (!matches)
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

    private bool MatchesFilterType(BaseEntity reference, BaseEntity targetEntity, byte filterType)
    {
        if (reference == null)
        {
            // No reference, treat as matching any for permissive behavior
            return true;
        }

        var stance = GetStanceSafe(reference, targetEntity);

        return filterType switch
        {
            0 => stance == HostilityStance.Friendly, // Friendly only
            1 => stance == HostilityStance.Hostile, // Hostile only
            2 => stance == HostilityStance.Neutral, // Neutral only
            3 => stance == HostilityStance.Self, // Self only
            4 => stance == HostilityStance.Friendly || stance == HostilityStance.Self, // Friendly or Self
            5 => true, // Any (all stances) - used with IncludeNormalOnly to restrict to Neutral
            _ => true // Unknown filter types default to permissive to avoid breaking chains
        };
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