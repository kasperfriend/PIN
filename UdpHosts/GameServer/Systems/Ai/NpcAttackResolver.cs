using System;
using System.Collections.Generic;
using System.Numerics;
using GameServer.Enums;
using GameServer.StaticDB;
using GameServer.StaticDB.Records.dbcharacter;
using GameServer.StaticDB.Records.dbitems;

namespace GameServer.Systems.Ai;

/// <summary>
///     Builds an <see cref="NpcAttackProfile" /> for a monster out of the static database: which
///     weapon it fights with, whether that weapon is melee or a projectile weapon, how hard and how
///     often it hits, and where the shot comes out.
/// </summary>
/// <remarks>
///     The lookup chain is the database's own: <c>dbcharacter::Monster.weapon1_id</c> (falling back
///     to <c>weapon2_id</c> when the primary slot is empty) -&gt; <c>dbitems::Weapons</c> -&gt;
///     <c>dbitems::WeaponTemplates</c> with the item's <c>WeaponTemplateModifiers</c> and weapon-slot
///     ability modules applied by <see cref="SDBUtils.GetDetailedWeaponTemplateInfo" />, plus the
///     item's own <c>AttributeRange</c> rows (954/957/1145 and the ammo stat attributes) and the
///     monster's <c>MonsterAttributeRange</c> creature modifiers. Nothing is approximated by weapon
///     class: two monsters with the same template but different item rows resolve to different
///     numbers because their rows differ.
/// </remarks>
public sealed class NpcAttackResolver
{
    /// <summary>Slack factor on a ranged weapon's exit range, so the state machine does not flap at the range edge.</summary>
    private const float RangedExitRangeFactor = 1.15f;

    /// <summary>How much further than a melee weapon's own range the tuned melee reach may sit.</summary>
    private const float MeleeExitRangeSlack = 1.5f;

    private readonly INpcAttackDataSource _data;
    private readonly IAiRules _rules;

    public NpcAttackResolver(INpcAttackDataSource data, IAiRules rules = null)
    {
        _data = data ?? throw new ArgumentNullException(nameof(data));
        _rules = rules ?? new StandardAiRules();
    }

    /// <summary>Resolves the attack profile of a <c>dbcharacter::Monster</c> row at a level.</summary>
    public NpcAttackProfile Resolve(uint characterTypeId, byte level)
    {
        var monster = _data.GetMonster(characterTypeId);
        if (monster == null)
        {
            return NpcAttackProfile.Unarmed;
        }

        // The database's own primary/secondary order: a monster fights with weapon1 when it has one.
        uint weaponId = monster.Weapon1Id != 0 ? monster.Weapon1Id : monster.Weapon2Id;

        // The row stores its muzzle offset in the static database's own vector type (a reference type
        // whose fields are lower case); rows that carry none leave it null, and the engine works in
        // System.Numerics, so convert on the way in.
        Vector3 muzzleOffset = monster.ProjectileOffset != null
            ? SDBUtils.Vector3FromFauFau(monster.ProjectileOffset)
            : default;

        return ResolveWeapon(monster.Id, weaponId, level, SelectBehavior(monster), muzzleOffset);
    }

    /// <summary>
    ///     Resolves one weapon of a character row. Public because deployables and turrets carry their
    ///     weapon ids in other tables but share the whole weapon/attribute/modifier chain below it.
    /// </summary>
    public NpcAttackProfile ResolveWeapon(uint monsterId, uint weaponId, byte level, NpcBehaviorParams behavior, Vector3 muzzleOffset)
    {
        if (weaponId == 0)
        {
            return NpcAttackProfile.Unarmed;
        }

        // GetDetailedWeaponInfo returns the weapon's main firing mode (and its underbarrel as Alt);
        // an NPC attacks with the main mode, so that is the template the profile reads.
        var template = _data.GetWeaponInfo(weaponId)?.Main;
        if (template == null)
        {
            return NpcAttackProfile.Unarmed;
        }

        var behaviorParams = behavior ?? NpcBehaviorParams.Empty;
        var attributes = _data.GetItemAttributeRange(weaponId) ?? new Dictionary<ushort, AttributeRange>();

        // Damage rows. 954 and 1145 never co-occur on a monster weapon item in build prod-1962 (0 of
        // the 596 items carry both), so at most one of these is non-zero.
        float itemDamage = ReadAttribute(attributes, (ushort)ItemAttributeId.WeaponDamage);
        float weaponModifier = ReadAttribute(attributes, NpcAttackDamageMath.CreatureWeaponDamageModifierAttribute);

        var creatureModifierRow = _data.GetMonsterAttribute(monsterId, NpcAttackDamageMath.CreatureDamageModifierAttribute);
        float creatureDamageModifier = creatureModifierRow != null
            ? NpcAttackDamageMath.ResolveCreatureModifier(creatureModifierRow.Base, creatureModifierRow.PerLevel, level)
            : 1f;

        var scaling = _data.GetMonsterScaling(level);
        int levelDamageRating = scaling != null ? (int)scaling.Damage : 0;

        int damage = NpcAttackDamageMath.ResolvePerRound(
            itemDamage,
            weaponModifier,
            levelDamageRating,
            creatureDamageModifier,
            _rules.AttackDamageFraction,
            _rules.AttackDamage);

        // The animation window is the weapon's own burst timing, not the AI cadence: ms_burst_duration
        // when the burst is fired over time, otherwise ms_per_burst (the fire cycle). See
        // NpcAttackAnimation for how it is spent.
        uint burstDuration = template.MsBurstDuration > 0 ? template.MsBurstDuration : template.MsPerBurst;

        float rangeAttribute = ReadAttribute(attributes, (ushort)ItemAttributeId.WeaponRange);
        float range = rangeAttribute > 0f ? rangeAttribute : template.Range;
        byte rounds = template.RoundsPerBurst > 0 ? template.RoundsPerBurst : (byte)1;

        uint interval = NpcAttackDamageMath.ResolveAttackIntervalMs(
            behaviorParams.TriggerPullTimeMs,
            behaviorParams.FireRestDurationMs,
            template.MsBurstDuration,
            template.MsPerBurst,
            (uint)Math.Max(0, _rules.AttackCooldownMs));

        // Melee vs ranged is a property of the row, not of the monster class: a weapon with an ammo
        // row and a range past arm's length shoots, everything else (the melee ability vehicles with
        // 2-3 m ranges, and rows whose ammo id does not resolve) swings.
        bool ranged = template.AmmoId != 0 && range > NpcAttackDamageMath.MeleeReachMax;
        Ammo ammo = ranged ? _data.GetAmmo(template.AmmoId) : null;
        if (ammo == null)
        {
            ranged = false;
        }

        float projectileSpeed = ammo != null ? ammo.ProjectileSpeed : 0f;
        float impactRadius = ammo != null ? ammo.ImpactRadius : 0f;
        float maxRadius = ammo != null ? ammo.MaxRadius : 0f;

        // The ammo's stat columns point back at weapon attributes (e.g. projectile speed 1644); an NPC
        // weapon that carries the attribute uses it, exactly like the player path does.
        if (ammo != null)
        {
            if (ammo.ProjectileSpeedStat != 0 && attributes.TryGetValue((ushort)ammo.ProjectileSpeedStat, out var speedRow) && speedRow.Base > 0f)
            {
                projectileSpeed = speedRow.Base;
            }

            if (ammo.ImpactRadiusStat != 0 && attributes.TryGetValue((ushort)ammo.ImpactRadiusStat, out var impactRow) && impactRow.Base > 0f)
            {
                impactRadius = impactRow.Base;
            }

            if (ammo.MaxRadiusStat != 0 && attributes.TryGetValue((ushort)ammo.MaxRadiusStat, out var maxRadiusRow) && maxRadiusRow.Base > 0f)
            {
                maxRadius = maxRadiusRow.Base;
            }
        }

        // A melee row swings at the tuned reach, not at its own template "range" (which for a melee
        // weapon is an ability radius): the rules reach already sits just above the 2.6-3 m of the
        // build's melee rows, and a ranged row whose ammo id does not resolve must not fall back to a
        // 30 m "range" and become an unavoidable hitscan sniper.
        float attackRange = ranged ? range : _rules.AttackRange;
        float attackRangeExit = ranged
            ? range * RangedExitRangeFactor
            : MathF.Max(_rules.AttackRangeExit, attackRange + MeleeExitRangeSlack);

        float standoff = ranged ? ResolveRangedStandoff(behaviorParams) : _rules.StandoffRange;

        return new NpcAttackProfile
        {
            Mode = ranged ? NpcAttackMode.Ranged : NpcAttackMode.Melee,
            MonsterId = monsterId,
            WeaponId = weaponId,
            WeaponTypeId = _data.GetWeaponTemplateId(weaponId),
            WeaponName = template.DebugName ?? string.Empty,
            DamagePerRound = damage,
            RoundsPerBurst = rounds,
            AttackIntervalMs = interval,
            Range = range,
            AttackRange = attackRange,
            AttackRangeExit = attackRangeExit,
            StandoffRange = standoff,
            AmmoId = ranged ? template.AmmoId : (ushort)0,
            Ammo = ranged ? ammo : null,
            ProjectileSpeed = projectileSpeed,
            ImpactRadius = impactRadius,
            MaxRadius = maxRadius,
            FireType = template.FireType,
            BurstDurationMs = burstDuration,
            ArmedAnimationId = template.AnimArmedId,
            ArmedAnimationPriority = template.AnimArmedPriority,
            FireAnimationType = template.AnimFireType,
            ReloadAnimationType = template.AnimReloadType,
            ChargeAnimationType = template.AnimChargeType,
            AttackAbilityId = template.AttackAbility,
            MeleeAbilityId = template.MeleeAbility,
            MuzzleOffset = muzzleOffset,
            CreatureDamageModifier = creatureDamageModifier,
            CreatureWeaponDamageModifier = weaponModifier,
            Behavior = behaviorParams,
        };
    }

    /// <summary>
    ///     The behaviour string the attack timing comes from: the row's <c>behavior</c> column when it
    ///     carries trigger parameters, otherwise its <c>behavior_offensive</c> column (the two are
    ///     separate columns and either can be the parametrised one).
    /// </summary>
    private static NpcBehaviorParams SelectBehavior(Monster monster)
    {
        var primary = NpcBehaviorParams.Parse(monster.Behavior);
        if (primary.HasAttackTiming)
        {
            return primary;
        }

        var offensive = NpcBehaviorParams.Parse(monster.BehaviorOffensive);
        return offensive.HasAttackTiming ? offensive : primary;
    }

    /// <summary>
    ///     Distance a ranged row wants to keep: its explicit preferred minimum, else the distance its
    ///     <c>combatDist</c> names, else the rules standoff.
    /// </summary>
    private float ResolveRangedStandoff(NpcBehaviorParams behavior)
    {
        if (behavior.PreferredMinimumCombatDistance > 0f)
        {
            return behavior.PreferredMinimumCombatDistance;
        }

        if (behavior.CombatDistance > 0f)
        {
            return behavior.CombatDistance;
        }

        return _rules.StandoffRange;
    }

    /// <summary>Reads one attribute's base value from an item's range rows, or 0.</summary>
    private static float ReadAttribute(IReadOnlyDictionary<ushort, AttributeRange> attributes, ushort attributeId)
    {
        if (attributeId != 0 && attributes.TryGetValue(attributeId, out var row) && row != null)
        {
            return row.Base;
        }

        return 0f;
    }
}
