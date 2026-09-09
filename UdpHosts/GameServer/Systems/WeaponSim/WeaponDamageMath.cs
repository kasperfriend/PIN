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
    ///     The rate at which the database grows a weapon's per-round damage for every level of
    ///     the weapon item (<see cref="DamageLevelScale"/>).
    /// </summary>
    /// <remarks>
    ///     Verified against <c>clientdb.sd2</c> build <c>prod-1962</c>: a weapon family
    ///     (<c>dbitems::RootItem.autogen_group_id</c>) ships one preset item per item level and
    ///     each of them carries its own attribute 954 row. The Accord Assault rifle family
    ///     (group 10020, quality 1) reads 11, 12.1, 13.255, 14.4677 ... 44.5929 for item levels
    ///     1-20, and the plasma family (group 10003, quality 1) reads 100, 110, 120.5, 131.525
    ///     ... 405.3901. Both chains are exactly <c>levelOneValue × (2 × 1.05^(level-1) - 1)</c>:
    ///     the ratio between consecutive levels starts at 1.1 and settles onto 1.05, so it is not
    ///     a flat compound, which is why <see cref="DamageLevelScale"/> is written as the closed
    ///     form of that sum rather than as a power of 1.1.
    ///     <para>
    ///     The closed form was then checked against every multi-level weapon chain in the build —
    ///     125 (family, quality) groups covering 2,810 of those rows — by dividing each row back
    ///     to the level-1 value it implies: 2,776 of them imply the same one within 1%. The
    ///     remainder are rows of families whose level ranges overlap (a family that restarts its
    ///     chain at a higher bracket), not a different curve.
    ///     </para>
    ///     <para>
    ///     The one place where the emulation is not bit-identical to the authored data is a value
    ///     sitting exactly on a rounding midpoint: <c>100 x scale(3)</c> is 120.5 in the database
    ///     and 120.49999 in single precision, so a level-3 plasma round is 120 here and 121 if the
    ///     item row itself is used. The closed form is never more than 3 parts in 10<sup>6</sup>
    ///     away from the curve, i.e. always under one point of damage.
    ///     </para>
    ///     <para>
    ///     Only the damage attribute grows with the item level: the same items keep their
    ///     magazine (956), range (957), spread (958), rate of fire (17) and headshot (20) values
    ///     at every level of the chain, which is why this scale is applied to damage and nowhere
    ///     else.
    ///     </para>
    /// </remarks>
    public const float DamageLevelGrowthRate = 1.05f;

    /// <summary>
    ///     How many times the item's authored per-round damage a wielder of
    ///     <paramref name="progressionLevel"/> deals: <c>2 × 1.05^(level-1) - 1</c>.
    /// </summary>
    /// <remarks>
    ///     The sum of the database's growing per-level steps (see
    ///     <see cref="DamageLevelGrowthRate"/>), written closed form: 1 at level 1 (the item's own
    ///     value), 1.1 at 2, 2.103 at 10, 4.054 at 20, 16.114 at 45, 20.843 at 50. Levels at or
    ///     below 1 (and the 0 of an entity that has no progression level at all, e.g. an NPC)
    ///     return 1, so a fresh frame fires its weapon for exactly the value its item row holds.
    /// </remarks>
    public static float DamageLevelScale(int progressionLevel)
    {
        if (progressionLevel <= 1)
        {
            return 1f;
        }

        return (2f * MathF.Pow(DamageLevelGrowthRate, progressionLevel - 1)) - 1f;
    }

    /// <summary>
    ///     Resolves the per-round damage of a fired weapon.
    /// </summary>
    /// <remarks>
    ///     <para>
    ///     The weapon <b>item</b> instance carries its own <c>dbitems::AttributeRange</c>
    ///     values; <see cref="ItemAttributeId.WeaponDamage"/> (attribute 954, whose
    ///     <c>dbitems::AttributeDefinition</c> display name is "Damage Per Round") is the
    ///     item's per-round damage <b>at its own item level</b>. Preset items always carry it
    ///     (e.g. the Accord Assault plasma cannon 86742: 954 = 100, its template
    ///     <c>damage_per_round</c> = 100; the Recon R36 86969: 954 = 39 while its shared template
    ///     row is a 1-damage stub).
    ///     </para>
    ///     <para>
    ///     <paramref name="progressionLevel"/> is the wielder's battleframe progression level, and
    ///     it is what makes the same gun hurt more as the character grows: the live game hands a
    ///     leveled frame the level-matched variant of its preset (a different item id from the same
    ///     autogen family, carrying a bigger 954), and since PIN issues the character-create (level 1)
    ///     preset and has no item upgrade flow yet, the growth is applied here through
    ///     <see cref="DamageLevelScale"/> instead. It reproduces the same number the level-matched
    ///     item row would have carried.
    ///     </para>
    ///     <para>
    ///     <c>dbcharacter::WeaponTemplateResult.DamagePerRound</c> (template
    ///     <c>damage_per_round</c> with the item's <c>WeaponTemplateModifiers</c> and
    ///     weapon-slot module deltas applied, see
    ///     <c>SDBUtils.GetDetailedWeaponTemplateInfo</c>) is the fallback for weapons the DB
    ///     gives no 954 row to — that is also the value NPC/turret weapons fight at, since
    ///     their rows have no item attribute range. It grows with the level the same way.
    ///     </para>
    ///     <para>
    ///     <paramref name="fallbackDamage"/> (the legacy placeholder) is only used when the
    ///     database has neither an item damage attribute nor a usable template value, so a
    ///     broken row can never fire a 0-damage or negative-damage shot. It is deliberately
    ///     <b>not</b> level scaled: it is a stand-in for a missing row, not a database number.
    ///     </para>
    /// </remarks>
    public static int ResolveRoundDamage(IReadOnlyDictionary<ushort, float> weaponAttributes, int templateDamagePerRound, int fallbackDamage, int progressionLevel = 1)
    {
        float levelScale = DamageLevelScale(progressionLevel);

        if (weaponAttributes != null
            && weaponAttributes.TryGetValue((ushort)ItemAttributeId.WeaponDamage, out var itemDamagePerRound)
            && itemDamagePerRound > 0)
        {
            return RoundDamage(itemDamagePerRound * levelScale);
        }

        if (templateDamagePerRound > 0)
        {
            return RoundDamage(templateDamagePerRound * levelScale);
        }

        return fallbackDamage;
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
