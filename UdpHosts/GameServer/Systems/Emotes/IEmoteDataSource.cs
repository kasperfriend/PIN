using System.Collections.Generic;
using GameServer.StaticDB;

namespace GameServer.Systems.Emotes;

/// <summary>
///     What the database says an emote is: one <c>dbcharacter::EmoteRecord</c> row. The row's animation
///     fields (<c>anim_override_id</c>, <c>head_anim_override_id</c>, <c>animation_name</c>) are read by
///     the client, which knows the animation networks of the visuals it draws; the field the server acts
///     on is <see cref="StatusEffectId" />.
/// </summary>
/// <param name="Id">The emote id the client performs (<c>GssCharacterCommand.PerformEmote.EmoteId</c>).</param>
/// <param name="Name">The row's name, for diagnostics (<c>dance</c>, <c>npc_alert</c>, <c>guardidle</c>, ...).</param>
/// <param name="StatusEffectId">
///     The status effect the emote runs while it lasts (<c>statuseffect</c>), or 0 for the 378 of the 382
///     rows that carry none.
/// </param>
public readonly record struct EmoteDefinition(ushort Id, string Name, uint StatusEffectId)
{
    /// <summary>Whether performing the emote applies a status effect.</summary>
    public bool HasStatusEffect => StatusEffectId != 0;
}

/// <summary>
///     The <c>dbcharacter::EmoteRecord</c> table as the <see cref="EmoteService" /> reads it. Exists so the
///     service is unit tested against a fake database: the production implementation is a thin wrapper
///     around <see cref="SDBInterface" />.
/// </summary>
public interface IEmoteDataSource
{
    /// <summary>The emote with that id.</summary>
    /// <param name="emoteId">The emote id a client asked for.</param>
    /// <returns>The row, or null when the id is not one of the database's emotes.</returns>
    EmoteDefinition? GetEmote(ushort emoteId);

    /// <summary>
    ///     The emote with that name, matched case-insensitively. The database names emotes in the monster
    ///     behaviour strings (<c>AlertAndInteractive(emote="calm")</c>) and never by id, so this is how an
    ///     NPC's own emote is resolved.
    /// </summary>
    /// <param name="name">An <c>dbcharacter::EmoteRecord.name</c> value, as a behaviour string wrote it.</param>
    /// <returns>The row, or null when no emote carries that name.</returns>
    EmoteDefinition? GetEmoteByName(string name);
}

/// <summary>The production <see cref="IEmoteDataSource" />: reads the loaded static database.</summary>
public sealed class SdbEmoteDataSource : IEmoteDataSource
{
    public EmoteDefinition? GetEmote(ushort emoteId)
    {
        var record = SDBInterface.GetEmoteRecord(emoteId);
        if (record == null)
        {
            return null;
        }

        return new EmoteDefinition(record.Id, record.Name, record.Statuseffect);
    }

    public EmoteDefinition? GetEmoteByName(string name)
    {
        var record = SDBInterface.GetEmoteRecord(name);
        if (record == null)
        {
            return null;
        }

        return new EmoteDefinition(record.Id, record.Name, record.Statuseffect);
    }
}
