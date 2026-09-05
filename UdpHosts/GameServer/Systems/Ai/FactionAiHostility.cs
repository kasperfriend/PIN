using GameServer.Entities;
using GameServer.Systems.Combat;

namespace GameServer.Systems.Ai;

/// <summary>
///     Faction table backed hostility. Uses the same policy as <see cref="CombatSim" />
///     when it gates projectile hits: only an explicit Friendly or Self stance is treated
///     as non attackable. SDB faction relation coverage is incomplete for some monster
///     factions, and treating an unresolved Neutral stance as friendly would leave whole
///     monster types unable to fight back.
/// </summary>
public class FactionAiHostility : IAiHostility
{
    private readonly HostilityResolver _resolver;

    public FactionAiHostility(HostilityResolver resolver = null)
    {
        _resolver = resolver ?? new HostilityResolver();
    }

    public bool IsHostile(IEntity attacker, IEntity target)
    {
        if (attacker == null || target == null)
        {
            return false;
        }

        if (attacker.EntityId == target.EntityId)
        {
            return false;
        }

        var stance = _resolver.GetStance(attacker.HostilityInfo, target.HostilityInfo);
        return stance != HostilityStance.Friendly && stance != HostilityStance.Self;
    }
}
