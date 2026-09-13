using AeroMessages.GSS.Character;
using GameServer.Entities.Character;

namespace GameServer.Systems.Emotes;

/// <summary>
///     Performs emotes out of <c>dbcharacter::EmoteRecord</c>.
/// </summary>
/// <remarks>
///     <para>
///         The client drives an emote from the replicated <see cref="EmoteData" /> on the performing
///         character's views, and the id it sends has to be one of the database's 382 emotes: an id the
///         table does not hold is ignored rather than replicated, so a malformed command cannot make every
///         client in range resolve an animation that does not exist. Emote id 0 is the database's
///         "not emoting" state, so it is always accepted and clears the emote.
///     </para>
///     <para>
///         Four emote rows name a <c>statuseffect</c> (<c>shocking</c> 1460, <c>firedance</c> 1462,
///         <c>heartbooth_left</c> 1478, <c>heartbooth_right</c> 1479). Those effects carry the client-side
///         commands that draw the emote (<c>tfPerformEmote</c> for 1460, <c>tfPlayAnimation</c> for the
///         heartbooth pair, the particle update chain for 1462) and the server-side duration commands that
///         end it (<c>RequireMoving</c>/<c>AirborneDuration</c>/<c>TimeDuration</c>), so the emote only
///         runs for every client while the server keeps the effect applied. The other 378 emotes are
///         purely client-side and need no effect.
///     </para>
/// </remarks>
public sealed class EmoteService
{
    /// <summary>The emote id that means "not emoting" (<see cref="EmoteData.Id" />).</summary>
    public const ushort NoEmote = 0;

    private readonly IEmoteDataSource _data;
    private readonly IEmoteEffectApplier _effects;

    public EmoteService(IEmoteDataSource data, IEmoteEffectApplier effects = null)
    {
        _data = data;
        _effects = effects;
    }

    /// <summary>
    ///     The emote id a name maps to, for the callers whose data names the emote instead of numbering it:
    ///     the monster behaviour strings do (<c>AlertAndInteractive(emote="calm")</c>). The comparison is
    ///     the table's own, case-insensitively, and a name no row carries resolves to
    ///     <see cref="NoEmote" /> rather than to a guess.
    /// </summary>
    /// <param name="name">An <c>dbcharacter::EmoteRecord.name</c> value, or null/empty.</param>
    /// <returns>The emote id, or <see cref="NoEmote" /> when the table has no such emote.</returns>
    public ushort ResolveEmoteName(string name)
    {
        if (_data == null || string.IsNullOrWhiteSpace(name))
        {
            return NoEmote;
        }

        return _data.GetEmoteByName(name.Trim())?.Id ?? NoEmote;
    }

    /// <summary>
    ///     Performs an emote: replicates it on the character's views and applies the status effect the
    ///     emote's row names, if any. Emote id 0 stops the emote.
    /// </summary>
    /// <param name="character">The character performing (or stopping) the emote.</param>
    /// <param name="emoteId">The emote id, or <see cref="NoEmote" /> to stop the emote.</param>
    /// <param name="time">The client's timestamp for the emote, replicated to the observers as it arrives.</param>
    /// <returns>Whether the emote is one the database holds (false: the request was ignored).</returns>
    public bool Perform(CharacterEntity character, ushort emoteId, uint time)
    {
        if (character == null)
        {
            return false;
        }

        if (emoteId == NoEmote)
        {
            character.SetEmote(new EmoteData { Id = NoEmote, Time = time });
            return true;
        }

        var emote = _data?.GetEmote(emoteId);
        if (emote == null)
        {
            return false;
        }

        character.SetEmote(new EmoteData { Id = emoteId, Time = time });

        if (emote.Value.HasStatusEffect)
        {
            _effects?.Apply(character, emote.Value.StatusEffectId, time);
        }

        return true;
    }
}
