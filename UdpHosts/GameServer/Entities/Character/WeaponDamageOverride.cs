namespace GameServer.Entities.Character;

/// <summary>
///     A weapon-damage substitution installed by <c>aptfs::SetWeaponDamageCommandDef</c> (79 rows): the
///     row resolves a damage value and this says how the weapon simulation should apply it to the
///     weapon's own resolved round damage. The def carries no weapon-slot column, so the override is
///     for whichever weapon is active when it is installed and when it is read.
/// </summary>
public class WeaponDamageOverride
{
    /// <summary>The value the row resolved to, after its lerp and damage range.</summary>
    public float Damage;

    /// <summary>
    ///     True when the row's <c>multiply</c> is set: scale the weapon's damage. False when
    ///     <c>set</c> is set: combine through <see cref="Regop" /> instead.
    /// </summary>
    public bool Multiply;

    /// <summary>The row's <c>damage_regop</c>: 0 ASSIGN in 52 of 79 rows, 2 MULTIPLY in 21, 1 ADD in 6.</summary>
    public byte Regop;
}
