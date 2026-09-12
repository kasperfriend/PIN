namespace GameServer.Enums;

public enum ItemAttributeId : ushort
{
    JetEnergyRecharge = 5,
    Health = 6,
    HealthRegen = 7,
    RunSpeed = 12,
    RateOfFire = 17,
    JetEnergy = 35,
    JumpHeight = 37,
    WeaponDamage = 954,
    WeaponMagazineSize = 956,
    WeaponRange = 957,
    WeaponSpread = 958,
    JetSprintCost = 1121,
    CreatureHPModifier = 1143,

    /// <summary>Creature Damage Modifier: a monster row's <c>MonsterAttributeRange</c> multiplier on every attack it makes.</summary>
    CreatureDamageModifier = 1144,

    /// <summary>Creature Weapon Damage Modifier: a creature weapon item's own damage modifier (attribute 954 is the player-side equivalent).</summary>
    CreatureWeaponDamageModifier = 1145,
    PowerRating = 1451,
    SprintSpeed = 1377
}