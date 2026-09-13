using AeroMessages.GSS.Character.Event;
using GameServer.Entities.Character;

namespace GameServer.Systems.Combat;

/// <summary>
///     Tells watching clients that a character's animation changed. The ObserverView event
///     (<c>AnimationUpdated</c>) carries two unnamed fields in AeroMessages (<c>Unk1</c> ushort,
///     <c>Unk2</c> byte); this helper does not invent names for them. The AI's attack window is
///     the trigger that already has a named animation selector (<c>anim_fire_type</c>) and a
///     start/end pair (<c>WeaponBurstFired</c> / <c>WeaponBurstEnded</c>), so those values are
///     what the engine passes through: Unk1 is the weapon's fire-animation type, Unk2 is 1 at
///     the burst start and 0 at the burst end.
/// </summary>
public static class AnimationUpdatedAnnouncement
{
    /// <summary>Value of <c>Unk2</c> at the start of an attack animation window.</summary>
    public const byte BurstStarted = 1;

    /// <summary>Value of <c>Unk2</c> at the end of an attack animation window.</summary>
    public const byte BurstEnded = 0;

    /// <summary>
    ///     Sends <c>AnimationUpdated</c> to every client the character is scoped into. No-op when
    ///     there is nobody to tell (a test shard with no entity manager, a character that has not
    ///     been scoped in).
    /// </summary>
    public static void SendToWatchers(IShard shard, CharacterEntity source, ushort unk1, byte unk2)
    {
        if (shard?.EntityMan == null || source == null)
        {
            return;
        }

        shard.EntityMan.SendToScoped(source, new AnimationUpdated { Unk1 = unk1, Unk2 = unk2 });
    }
}
