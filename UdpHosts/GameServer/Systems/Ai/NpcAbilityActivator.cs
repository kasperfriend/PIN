using GameServer.Entities.Character;
using GameServer.Systems.Aptitude;

namespace GameServer.Systems.Ai;

/// <summary>
///     The NPC half of the aptitude runtime: running one of a weapon's own abilities on the mob that holds it.
///     Exists as an interface so the AI engine can be tested without a shard, and so a server with no ability
///     system (a test shard, a stripped deployment) simply never activates anything.
/// </summary>
public interface INpcAbilityActivator
{
    /// <summary>
    ///     Activates an ability with the NPC as its initiator. <paramref name="register" /> carries the
    ///     value the chain's commands read, which for a weapon ability is the weapon's charge time (see
    ///     <see cref="NpcAttackProfile.ChargeUpMs" />); pass <c>float.NaN</c> for "no value".
    /// </summary>
    bool Activate(CharacterEntity npc, uint abilityId, uint time, float register);
}

/// <summary>The production <see cref="INpcAbilityActivator" />: the shard's aptitude system.</summary>
public sealed class ShardAbilityActivator : INpcAbilityActivator
{
    private readonly IShard _shard;

    public ShardAbilityActivator(IShard shard)
    {
        _shard = shard;
    }

    public bool Activate(CharacterEntity npc, uint abilityId, uint time, float register)
    {
        var abilities = _shard.Abilities;
        if (abilities == null || abilityId == 0)
        {
            return false;
        }

        return abilities.HandleActivateAbility(
            _shard,
            npc,
            abilityId,
            time,
            new AptitudeTargets(),
            register: register);
    }
}
