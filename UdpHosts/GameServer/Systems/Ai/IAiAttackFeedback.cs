using GameServer.Entities.Character;

namespace GameServer.Systems.Ai;

/// <summary>
///     Client side feedback for an NPC attack. Split out so the damage path stays
///     testable - <see cref="AiEngine" /> applies the damage itself through
///     <c>IShard.Damage</c> and only uses this for the cosmetic hit messages.
/// </summary>
public interface IAiAttackFeedback
{
    void OnAttack(CharacterEntity source, CharacterEntity target, int damage);
}
