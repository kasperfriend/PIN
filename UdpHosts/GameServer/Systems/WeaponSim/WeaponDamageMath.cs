using System;
using System.Collections.Generic;
using GameServer.Enums;

namespace GameServer.Systems.WeaponSim;

/// <summary>
///     Pure helpers that turn the static database's weapon rows into the per-hit damage a
///     projectile deals. Kept free of shard/entity state so the resolution rules are unit
///     testable.
/// </summary>
public static class WeaponDamageMath
{
    /// <summary>
    ///     Resolves the per-round damage of a fired weapon.
    /// </summary>
    /// <remarks>
    ///     <para>
    ///     The weapon <b>item</b> instance carries its own <c>dbitems::AttributeRange</c>
    ///     values; <see cref="ItemAttributeId.WeaponDamage"/> (attribute 954, whose
    ///     <c>dbitems::AttributeDefinition</c> display name is "Damage Per Round") is the
    ///     item's final per-round damage — the value a preset weapon of that level actually
    ///     deals. Preset items always carry it (e.g. the Accord Assault plasma cannon 86742:
    ///     954 = 100, its template <c>damage_per_round</c> = 100; the Recon R36 86969:
    ///     954 = 39 while its shared template row is a 1-damage stub).
    ///     </para>
    ///     <para>
    ///     <c>dbcharacter::WeaponTemplateResult.DamagePerRound</c> (template
    ///     <c>damage_per_round</c> with the item's <c>WeaponTemplateModifiers</c> and
    ///     weapon-slot module deltas applied, see
    ///     <c>SDBUtils.GetDetailedWeaponTemplateInfo</c>) is the fallback for weapons the DB
    ///     gives no 954 row to — that is also the value NPC/turret weapons fight at, since
    ///     their rows have no item attribute range.
    ///     </para>
    ///     <para>
    ///     <paramref name="fallbackDamage"/> (the legacy placeholder) is only used when the
    ///     database has neither an item damage attribute nor a usable template value, so a
    ///     broken row can never fire a 0-damage or negative-damage shot.
    ///     </para>
    /// </remarks>
    public static int ResolveRoundDamage(IReadOnlyDictionary<ushort, float> weaponAttributes, int templateDamagePerRound, int fallbackDamage)
    {
        if (weaponAttributes != null
            && weaponAttributes.TryGetValue((ushort)ItemAttributeId.WeaponDamage, out var itemDamagePerRound)
            && itemDamagePerRound > 0)
        {
            return RoundDamage(itemDamagePerRound);
        }

        return templateDamagePerRound > 0 ? templateDamagePerRound : fallbackDamage;
    }

    /// <summary>
    ///     Applies the ammo's damage falloff to <paramref name="baseDamage"/> for a hit at
    ///     <paramref name="distanceTravelled"/> metres from the muzzle.
    /// </summary>
    /// <remarks>
    ///     <para>
    ///     Falloff is defined by the fired <c>dbitems::Ammo</c> row, not by the weapon
    ///     template: <c>damage_decay</c> 0 disables it (plasma/tesla/rocket projectiles),
    ///     any other mode falls off linearly. The projectile deals full damage until
    ///     <c>damage_decay_rangefrac</c> of the weapon <c>range</c> (e.g. 0.7 = the first
    ///     70% of the range), then tapers linearly down to
    ///     <c>min_damage_frac</c> × base damage at max range (e.g. PvE assault rifle ammo:
    ///     decay starts at 70% of range, floor is 33% of the per-round damage). A fraction
    ///     of 1 (or no range) means no decay regardless of the mode.
    ///     </para>
    ///     <para>
    ///     Damage is rounded half away from zero at every stage so the applied values are
    ///     whole numbers like the rest of the damage pipeline.
    ///     </para>
    /// </remarks>
    public static int ApplyDamageFalloff(int baseDamage, float distanceTravelled, float weaponRange, byte damageDecay, float decayRangeFrac, float minDamageFrac)
    {
        if (baseDamage <= 0)
        {
            return baseDamage;
        }

        // damage_decay 0: no falloff at all.
        if (damageDecay == 0)
        {
            return baseDamage;
        }

        float range = MathF.Max(0f, weaponRange);
        float startFrac = Math.Clamp(decayRangeFrac, 0f, 1f);
        float floorFrac = Math.Clamp(minDamageFrac, 0f, 1f);

        // No usable falloff window / no damage left at the floor: keep the full value.
        if (range <= 0f || startFrac >= 1f || floorFrac >= 1f)
        {
            return baseDamage;
        }

        float distance = MathF.Max(0f, distanceTravelled);
        float startDistance = startFrac * range;

        if (distance <= startDistance)
        {
            return baseDamage;
        }

        float t = distance >= range
            ? 1f
            : (distance - startDistance) / (range - startDistance);

        float damage = baseDamage * (1f - ((1f - floorFrac) * t));
        return RoundDamage(damage);
    }

    /// <summary>Rounds a damage value to the nearest whole number, half away from zero.</summary>
    public static int RoundDamage(float damage)
    {
        return (int)MathF.Round(damage, MidpointRounding.AwayFromZero);
    }
}
