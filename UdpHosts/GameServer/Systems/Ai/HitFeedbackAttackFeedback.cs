using GameServer.Entities.Character;

namespace GameServer.Systems.Ai;

/// <summary>
///     Routes NPC attacks through the existing hit feedback sender on <c>CombatSim</c> so
///     the victim sees the same TookHit message a weapon hit produces.
/// </summary>
public class HitFeedbackAttackFeedback : IAiAttackFeedback
{
    private readonly IShard _shard;

    public HitFeedbackAttackFeedback(IShard shard)
    {
        _shard = shard;
    }

    public void OnAttack(CharacterEntity source, CharacterEntity target, int damage)
    {
        // Combat is null on the in-memory test shard, in which case there is nobody to
        // tell about the hit and the damage has already been applied by the caller.
        _shard?.Combat?.HitFeedback?.TookDebugHit(target, source, damage, false, false);
    }
}
