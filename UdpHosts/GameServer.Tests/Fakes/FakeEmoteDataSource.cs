using System.Collections.Generic;
using GameServer.Systems.Emotes;

namespace GameServer.Tests.Fakes;

/// <summary>
///     The emote rows the tests need out of the 382 in <c>dbcharacter::EmoteRecord</c>, by id and by name.
///     The names are the ones the database's monster behaviour strings use, with the real ids.
/// </summary>
public sealed class FakeEmoteDataSource : IEmoteDataSource
{
    private readonly Dictionary<ushort, EmoteDefinition> _byId = new()
    {
        [1] = new EmoteDefinition(1, "dance", 0),
        [4] = new EmoteDefinition(4, "taunt", 0),
        [60] = new EmoteDefinition(60, "utility", 0),
        [1062] = new EmoteDefinition(1062, "calm", 0),
        [1097] = new EmoteDefinition(1097, "guard", 0),
        [1105] = new EmoteDefinition(1105, "officer", 0),
        [1460] = new EmoteDefinition(1460, "shocking", 13_551),
        [1462] = new EmoteDefinition(1462, "firedance", 4348),
        [1479] = new EmoteDefinition(1479, "heartbooth_right", 14_752),
        [1480] = new EmoteDefinition(1480, "sumostomp", 0),
    };

    private readonly Dictionary<string, EmoteDefinition> _byName;

    public FakeEmoteDataSource()
    {
        _byName = new Dictionary<string, EmoteDefinition>(System.StringComparer.OrdinalIgnoreCase);
        foreach (var emote in _byId.Values)
        {
            _byName[emote.Name] = emote;
        }
    }

    public EmoteDefinition? GetEmote(ushort emoteId)
    {
        if (_byId.TryGetValue(emoteId, out var emote))
        {
            return emote;
        }

        return null;
    }

    public EmoteDefinition? GetEmoteByName(string name)
    {
        if (!string.IsNullOrWhiteSpace(name) && _byName.TryGetValue(name.Trim(), out var emote))
        {
            return emote;
        }

        return null;
    }
}
