using System;
using Serilog;

namespace Shared.Common.Characters;

/// <summary>
/// In-process notifications that a character stored in <see cref="CharacterStore"/>
/// has changed.
///
/// The web hosts all run in the WebHostManager process and share the store's
/// in-memory copy, so a static event is the lightest way for the host that
/// persists a change to reach the host that has to relay it: the REST ClientApi
/// persists the New You terminal's save, and the GRPC GameServerApi host
/// subscribes to <see cref="VisualsUpdated"/> and rebroadcasts it to every
/// connected GameServer as the <c>CharacterVisualsUpdated</c> event — the
/// mechanism that makes a zoned-in character pick up its new appearance
/// without logging out (the save alone only changes what the next login sees).
/// </summary>
public static class CharacterEvents
{
    /// <summary>
    /// Raised after a character's appearance has been persisted
    /// (the New You terminal's save request).
    /// </summary>
    public static event Action<CharacterRecord> VisualsUpdated;

    /// <summary>
    /// Notify subscribers that <paramref name="character"/>'s appearance
    /// changed. Subscribers run isolated: the store is already persisted by
    /// the time this is called, so a failing subscriber must not be able to
    /// break the request that made the change (the live update is dropped,
    /// the save is not).
    /// </summary>
    public static void NotifyVisualsUpdated(CharacterRecord character)
    {
        var handler = VisualsUpdated;
        if (handler == null)
        {
            return;
        }

        try
        {
            handler(character);
        }
        catch (Exception ex)
        {
            Log.Warning(ex,
                "A CharacterEvents.VisualsUpdated subscriber failed for character {CharacterGuid}: the change is persisted, only the live update was dropped",
                character?.CharacterGuid);
        }
    }
}
