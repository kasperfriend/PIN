using System;

namespace GameServer.Systems.Ai;

/// <summary>
///     The pure half of an NPC's attack numbers: turning the static database's weapon rows into the
///     damage one round deals and the time between two attacks. Split out of
///     <see cref="NpcAttackResolver" /> so the rules can be asserted on without a static database.
/// </summary>
/// <remarks>
///     <para>
///     <b>Damage.</b> The database carries two mutually exclusive damage statements for the weapons
///     NPCs use, and this resolver spends whichever one the row has:
///     </para>
///     <list type="number">
///     <item>
///     <description>
///     <c>dbitems::AttributeRange</c> attribute 954 (Damage Per Round) on the weapon item - the
///     literal per-round damage of a level-matched item. Monsters that fight with the shared PvE
///     weapon families (e.g. the level-1/26/45/51/55 "PvE - Assault Rifle (Secondary)" items) carry
///     it, and it already encodes the item's level, so it is used as-is. It is the same attribute
///     player weapons use (see <c>WeaponDamageMath</c>), which is why an NPC with a PvE weapon hits
///     like the item says it does.
///     </description>
///     </item>
///     <item>
///     <description>
///     Attribute 1145 (Creature Weapon Damage Modifier) on the weapon item. Creature weapon items
///     are level-1 rows, so their damage is the creature's per-level damage rating from
///     <c>dbcharacter::MonsterScaling</c> scaled by the weapon's own modifier - the value that makes
///     one monster's copy of "NPC Guard Rifle" hit harder than another's (the same template carries
///     1145 = 0.0217 on the common guards and 0.15 on the elite ones). This is how a level-45 NPC
///     fights at level-45 damage while the weapon row itself stays level 1: the rating is the level
///     term, the modifier is the weapon term. The current NPC rule (a flat tenth of the rating,
///     <c>AiAttackDamage.Resolve</c>) is exactly this formula with the modal 1145 of the build - the
///     value is 0.1 on more weapons than any other - so rows that carry the modifier now hit for
///     what their own row says instead of the average.
///     </description>
///     </item>
///     <item>
///     <description>
///     Rows with neither (the 103 monster weapon items that are pure melee/ability vehicles, plus
///     every monster the database gives no weapon at all) keep the rating share the engine has
///     always used: <c>rating x unmodifiedWeaponDamageFraction</c>, i.e. a tenth of the monster's
///     per-level damage rating, with the rules' flat fallback for a monster or level the database
///     has no rating for.
///     </description>
///     </item>
///     </list>
///     <para>
///     Every branch is then multiplied by the monster's own attribute 1144 (Creature Damage
///     Modifier, 0.95-1.5 on the 81 named monsters that carry one, absent - i.e. 1 - on everything
///     else).
///     </para>
///     <para>
///     <b>Cadence.</b> The AI attack cycle comes from the behaviour string when the row has one
///     (see <see cref="NpcBehaviorParams" />), because the weapon template's burst cadence is the
///     client's fire animation timing: <c>NPC Guard Rifle</c> carries <c>ms_per_burst</c> = 100 with
///     four rounds, which is a per-frame volley, while the behaviour rows say a guard pulls the
///     trigger after 1,500 ms and rests 1,000 ms. Only rows without behaviour timing fall back to
///     the weapon cadence, and only a weapon row that says nothing falls back to the rules'
///     cooldown.
///     </para>
/// </remarks>
public static class NpcAttackDamageMath
{
    /// <summary>
    ///     <c>dbitems::AttributeRange</c> attribute id of "Creature Weapon Damage Modifier", the
    ///     per-item damage modifier a creature weapon carries instead of attribute 954.
    /// </summary>
    public const ushort CreatureWeaponDamageModifierAttribute = 1145;

    /// <summary>
    ///     <c>dbcharacter::MonsterAttributeRange</c> attribute id of "Creature Damage Modifier", the
    ///     monster-wide multiplier applied to every branch of the damage rule.
    /// </summary>
    public const ushort CreatureDamageModifierAttribute = 1144;

    /// <summary>
    ///     <c>dbcharacter::MonsterAttributeRange</c> attribute id of "Creature HP Modifier", kept
    ///     next to the damage one because the two are read from the same rows.
    /// </summary>
    public const ushort CreatureHealthModifierAttribute = 1143;

    /// <summary>
    ///     Longest reach in metres that still counts as melee. The database separates the two cleanly:
    ///     its melee rows (<c>NPC Melee Medium (Spyder)</c>, <c>NPC Brontodon Stomp</c>, the various
    ///     melee ability vehicles) carry 2-3 m, its ranged rows start in the tens (the shortest NPC
    ///     ranged rows are 20 m) and the ones with no range at all carry no ammo row either.
    /// </summary>
    public const float MeleeReachMax = 4f;

    /// <summary>
    ///     Smallest attack cycle accepted from a weapon row, in milliseconds. The AI acts on a 50 ms
    ///     tick, and several creature weapons carry single-digit <c>ms_per_burst</c> values that are
    ///     fire-animation detail rather than an attack rate; behaviour driven rows are used verbatim.
    /// </summary>
    public const uint MinimumAttackIntervalMs = 250;

    /// <summary>Damage a creature weapon deals when its data resolves to something non-positive.</summary>
    public const int MinimumDamage = 1;

    /// <summary>
    ///     Resolves the damage one round of an NPC weapon deals, per the rules in the type's remarks.
    /// </summary>
    /// <param name="itemDamagePerRound">Attribute 954 of the weapon item, or 0 when it has no row.</param>
    /// <param name="creatureWeaponDamageModifier">Attribute 1145 of the weapon item, or 0 when it has no row.</param>
    /// <param name="levelDamageRating"><c>dbcharacter::MonsterScaling.damage</c> at the NPC's level, or 0.</param>
    /// <param name="creatureDamageModifier">Attribute 1144 of the monster at its level; values at or below 0 mean "no row" and count as 1.</param>
    /// <param name="unmodifiedWeaponDamageFraction">Share of the rating spent when the weapon says nothing (rules value).</param>
    /// <param name="fallbackDamage">Flat per-attack damage used when the database has no rating either.</param>
    public static int ResolvePerRound(
        float itemDamagePerRound,
        float creatureWeaponDamageModifier,
        int levelDamageRating,
        float creatureDamageModifier,
        float unmodifiedWeaponDamageFraction,
        int fallbackDamage)
    {
        float baseDamage;
        if (itemDamagePerRound > 0f)
        {
            baseDamage = itemDamagePerRound;
        }
        else if (creatureWeaponDamageModifier > 0f && levelDamageRating > 0)
        {
            baseDamage = levelDamageRating * creatureWeaponDamageModifier;
        }
        else if (levelDamageRating > 0)
        {
            baseDamage = levelDamageRating * Math.Clamp(unmodifiedWeaponDamageFraction, 0f, 1f);
        }
        else
        {
            // No weapon damage row and no rating: the rules value is already a per-attack number.
            return fallbackDamage > 0 ? fallbackDamage : 0;
        }

        float modifier = creatureDamageModifier > 0f ? creatureDamageModifier : 1f;
        int damage = (int)MathF.Round(baseDamage * modifier, MidpointRounding.AwayFromZero);
        return damage > 0 ? damage : MinimumDamage;
    }

    /// <summary>
    ///     Resolves the number of milliseconds between two attacks: the behaviour's wind-up plus rest
    ///     when the monster has them, otherwise the weapon's burst timing, otherwise the rules
    ///     cooldown.
    /// </summary>
    /// <param name="behaviorTriggerPullTimeMs">Behaviour <c>triggerPullTime</c>, or 0.</param>
    /// <param name="behaviorFireRestDurationMs">Behaviour <c>fireRestDuration</c>, or 0.</param>
    /// <param name="msBurstDuration">Weapon template <c>ms_burst_duration</c> (when the burst is fired over time).</param>
    /// <param name="msPerBurst">Weapon template <c>ms_per_burst</c> (the burst cycle).</param>
    /// <param name="fallbackIntervalMs">Rules cooldown used when the weapon row says nothing.</param>
    public static uint ResolveAttackIntervalMs(
        int behaviorTriggerPullTimeMs,
        int behaviorFireRestDurationMs,
        uint msBurstDuration,
        uint msPerBurst,
        uint fallbackIntervalMs)
    {
        long behavior = Math.Max(0, behaviorTriggerPullTimeMs) + Math.Max(0, behaviorFireRestDurationMs);
        if (behavior > 0)
        {
            return (uint)Math.Max(behavior, MinimumAttackIntervalMs);
        }

        long weapon = msBurstDuration > 0 ? msBurstDuration : msPerBurst;
        if (weapon > 0)
        {
            return (uint)Math.Max(weapon, MinimumAttackIntervalMs);
        }

        return fallbackIntervalMs > 0 ? fallbackIntervalMs : MinimumAttackIntervalMs;
    }

    /// <summary>
    ///     Reads a <c>dbcharacter::MonsterAttributeRange</c> value at a level: <c>base + per_level x level</c>.
    ///     Every creature modifier row in build prod-1962 has <c>per_level</c> 0, so this is the base
    ///     value, but the column is honoured so a future build that levels a modifier keeps working.
    /// </summary>
    public static float ResolveCreatureModifier(float baseValue, float perLevel, byte level)
    {
        return baseValue + (perLevel * level);
    }
}
