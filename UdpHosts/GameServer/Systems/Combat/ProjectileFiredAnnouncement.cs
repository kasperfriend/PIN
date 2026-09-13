using System.Numerics;
using AeroMessages.GSS.Character.Event;
using GameServer.Entities.Character;

namespace GameServer.Systems.Combat;

/// <summary>
///     Tells watching clients that a projectile left a character. A player's own client predicts
///     that from the fire input and the server only echoes <c>WeaponProjectileFired</c> back to
///     the shooter; everyone else - observers of another player, and every client watching an
///     NPC - only sees the shot if the server sends the same event. NPC ranged attacks used to
///     fire through <c>ProjectileSim</c> alone, so the damage arrived with no tracer and no muzzle.
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

        shard.EntityMan.SendToScoped(source, message);
    }
}
