namespace GameServer.Entities.Character;

/// <summary>
///     A weapon-slot ammo substitution installed by <c>aptfs::SlotAmmoCommandDef</c>: the "load special
///     rounds into this weapon" family. Held on the character rather than on
///     <c>CharacterEntity.ActiveWeaponDetails</c> because that cache is rebuilt on every loadout change
///     (CharacterEntity.cs:2632), which would silently drop the override mid-ability.
/// </summary>
public class AmmoSlotOverride
{
    /// <summary>The <c>dbitems::Ammo</c> row to fire instead of the weapon's own.</summary>
    public uint AmmoType;

    /// <summary>Only substitute when the weapon currently loads this ammo row; 0 means always.</summary>
    public uint ReplaceAmmoType;

    /// <summary>Flat damage added on top of the weapon's resolved round damage.</summary>
    public float WeaponDamageAdd;

    /// <summary>Damage multiplier applied after <see cref="WeaponDamageAdd" />; 0 means 1.</summary>
    public float WeaponDamageMult;

    /// <summary>The ability activation that installed this, so a rollback can take it back off.</summary>
    public uint AbilityId;
}
