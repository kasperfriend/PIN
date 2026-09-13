using System.Numerics;
using AeroMessages.GSS.Character.Event;
using GameServer.Entities.Character;

namespace GameServer.Systems.Combat;

/// <summary>
///     Tells watching clients that a projectile left a character. A player's own client predicts
///     that from the fire input and the server echoes <c>WeaponProjectileFired</c> back to the
///     shooter on ReliableGss; everyone else - observers of another player, and every client
///     watching an NPC - only sees the shot if the server sends the same event. NPC ranged
///     attacks used to fire through <c>ProjectileSim</c> alone, so the damage arrived with no
///     tracer and no muzzle. A seated turret gunner has no CombatController echo, so that path
///     still includes the gunner's own client.
/// </summary>
public static class ProjectileFiredAnnouncement
{
    /// <summary>
    ///     Sends <c>WeaponProjectileFired</c> to every client the character is scoped into, aimed
    ///     along <paramref name="direction" />. No-op when there is nobody to tell (a test shard
    ///     with no entity manager, a character that has not been scoped in).
    /// </summary>
    public static void SendToWatchers(IShard shard, CharacterEntity source, Vector3 direction)
    {
        if (shard?.EntityMan == null || source == null)
        {
            return;
        }

        if (direction.LengthSquared() <= 0.0001f)
        {
            return;
        }

        var velocity = source.Velocity;
        bool haveVelocity = velocity.LengthSquared() > 0.0001f;
        var message = new WeaponProjectileFired
        {
            ShortTime = shard.CurrentShortTime,
            Aim = Vector3.Normalize(direction),
            HaveShooterVelocity = haveVelocity ? (byte)1 : (byte)0,
            ShooterVelocity = haveVelocity ? velocity : default,
        };

        SendToWatchers(shard, source, message);
    }

    /// <summary>
    ///     Same as the direction overload, but uses a message already built (the player's fire
    ///     packet's time, aim, and velocity) so observers draw the same shot the shooter echoed.
    ///     <paramref name="exceptOwner" /> skips the shooter's own client: a player's fire path
    ///     already echoed the event on ReliableGss, and a second UnreliableGss copy would double
    ///     the tracer. Leave it false for NPC and turret fire - those have no echo, and a seated
    ///     gunner only sees the shot through this announcement.
    /// </summary>
    public static void SendToWatchers(IShard shard, CharacterEntity source, WeaponProjectileFired message, bool exceptOwner = false)
    {
        if (shard?.EntityMan == null || source == null || message == null)
        {
            return;
        }

        shard.EntityMan.SendToScoped(source, message, exceptOwner ? source.Player : null);
    }
}
