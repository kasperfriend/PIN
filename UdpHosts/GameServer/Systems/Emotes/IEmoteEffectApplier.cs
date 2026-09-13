using GameServer.Entities.Character;
using GameServer.Systems.Aptitude;

namespace GameServer.Systems.Emotes;

/// <summary>
///     Runs the status effect an emote carries. The database expects both sides of an emote effect to run
///     the same chains: the client executes the client-side commands they hold (<c>tfPerformEmote</c>,
///     <c>tfPlayAnimation</c>, <c>tfSwitchMaterial</c>, ...), and the server executes the server-side ones
///     (<c>RequireMoving</c>, <c>AirborneDuration</c>, <c>TimeDuration</c>, ...) that end the effect.
///     Applying the effect server-side is what replicates it, and therefore what makes every client play
///     the emote animation while the effect is up.
/// </summary>
public interface IEmoteEffectApplier
{
    /// <summary>Applies the emote's status effect to the character performing it.</summary>
    /// <param name="target">The character performing the emote.</param>
    /// <param name="effectId">The effect the emote's row names (<c>EmoteRecord.statuseffect</c>).</param>
    /// <param name="time">The emote's own timestamp, the event time the effect is applied at.</param>
    /// <returns>Whether the effect applied (a shard without an ability system, or an unknown effect, refuses).</returns>
    bool Apply(CharacterEntity target, uint effectId, uint time);
}

/// <summary>The production applier: the shard's ability system applies the effect by id.</summary>
public sealed class AbilitySystemEmoteEffectApplier : IEmoteEffectApplier
{
    public bool Apply(CharacterEntity target, uint effectId, uint time)
    {
        var abilities = target?.Shard?.Abilities;
        if (abilities == null)
        {
            return false;
        }

        var context = new Context(target.Shard, target)
        {
            InitTime = time,
        };

        return abilities.DoApplyEffect(effectId, target, context);
    }
}
