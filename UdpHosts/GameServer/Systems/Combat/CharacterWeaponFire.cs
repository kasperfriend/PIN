using GameServer.Entities.Character;
using GameServer.Entities.Turret;

namespace GameServer.Systems.Combat;

/// <summary>
///     Whether a character's own equipped weapon is allowed to fire. A seated turret gunner
///     already fires through <c>dbcharacter::TurretWeapon</c> on the turret controller; the
///     character CombatController still receives the client's <c>FireWeaponProjectile</c> for
///     the gun in their hands, and answering that used to spawn a second round from the
///     equipped weapon.
/// </summary>
public static class CharacterWeaponFire
{
    /// <summary>
    ///     False while the character is attached to a turret: that fire belongs to
    ///     <c>TurretWeaponFire</c>, not <c>WeaponSim</c>. True for every other pose, including
    ///     a vehicle occupant (those are not turret gunners).
    /// </summary>
    public static bool ShouldFireEquippedWeapon(CharacterEntity character)
    {
        return character != null && character.AttachedToEntity is not TurretEntity;
    }
}
