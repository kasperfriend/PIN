using System.Numerics;
using GameServer.StaticDB.Records.dbitems;

namespace GameServer.Systems.Ai;

/// <summary>How a spawned NPC or mob attacks, as the static database describes it.</summary>
public enum NpcAttackMode : byte
{
    /// <summary>No usable <c>dbcharacter::Monster</c> weapon row: the NPC keeps the legacy rating based attack.</summary>
    None = 0,

    /// <summary>Direct hit on a target inside the weapon's reach (no projectile).</summary>
    Melee = 1,

    /// <summary>Fires the weapon's <c>dbitems::Ammo</c> projectile at the target.</summary>
    Ranged = 2,
}

/// <summary>
///     Everything one NPC attack needs, resolved from <c>clientdb.sd2</c>:
///     <c>dbcharacter::Monster.weapon1_id/weapon2_id</c> -&gt; <c>dbitems::Weapons</c> -&gt;
///     <c>dbitems::WeaponTemplates</c> (with the item's <c>dbitems::WeaponTemplateModifiers</c> and
///     weapon-slot module overrides applied by <c>SDBUtils.GetDetailedWeaponTemplateInfo</c>),
///     the weapon item's <c>dbitems::AttributeRange</c> rows, the monster's
///     <c>dbcharacter::MonsterAttributeRange</c> creature modifiers and the behaviour string's
///     AI timing parameters.
/// </summary>
/// <remarks>
///     A record, so it is immutable and can be swapped in tests. <see cref="Unarmed" /> is the
///     value for a monster the database gives no weapon to; the engine then keeps the legacy
///     <see cref="AiAttackDamage" /> rating based swing so such rows still deal damage.
/// </remarks>
public sealed record NpcAttackProfile
{
    /// <summary>The profile of a monster with no resolvable weapon.</summary>
    public static readonly NpcAttackProfile Unarmed = new();

    public NpcAttackMode Mode { get; init; } = NpcAttackMode.None;

    /// <summary><c>dbcharacter::Monster.id</c> the profile was resolved for (0 for a synthetic profile).</summary>
    public uint MonsterId { get; init; }

    /// <summary>The weapon item id (<c>dbitems::Weapons.id</c>) the monster fights with, or 0.</summary>
    public uint WeaponId { get; init; }

    /// <summary>The weapon template id (<c>dbitems::WeaponTemplates.id</c>) the item points at, or 0.</summary>
    public uint WeaponTypeId { get; init; }

    /// <summary>The template's debug name (e.g. <c>NPC Guard Rifle</c>), for logs and diagnostics.</summary>
    public string WeaponName { get; init; } = string.Empty;

    /// <summary>Damage one round of the attack deals, after the creature modifiers have been applied.</summary>
    public int DamagePerRound { get; init; }

    /// <summary>Rounds the weapon fires per attack (template <c>rounds_per_burst</c>, at least 1).</summary>
    public byte RoundsPerBurst { get; init; } = 1;

    /// <summary>
    ///     Milliseconds between two attacks: the behaviour's <c>triggerPullTime + fireRestDuration</c>
    ///     when the row carries them, otherwise the weapon template's burst cadence (see
    ///     <see cref="NpcAttackDamageMath.ResolveAttackIntervalMs" />).
    /// </summary>
    public uint AttackIntervalMs { get; init; }

    /// <summary>
    ///     The weapon's effective range in metres: attribute 957 (<c>Range</c>) of the weapon item
    ///     when it has one, otherwise the template's <c>range</c>.
    /// </summary>
    public float Range { get; init; }

    /// <summary>Distance in metres at which the NPC switches into its attack state.</summary>
    public float AttackRange { get; init; }

    /// <summary>Distance in metres at which an attacking NPC falls back to chasing.</summary>
    public float AttackRangeExit { get; init; }

    /// <summary>Distance in metres the NPC tries to keep once engaged (behaviour <c>combatDist</c> for ranged rows).</summary>
    public float StandoffRange { get; init; }

    /// <summary>The <c>dbitems::Ammo</c> row fired, or 0/absent for melee.</summary>
    public ushort AmmoId { get; init; }

    /// <summary>The resolved ammo row itself, so the projectile can be launched without another lookup.</summary>
    public Ammo Ammo { get; init; }

    /// <summary>Muzzle speed in m/s (<c>ammo.projectile_speed</c>, overridden by its stat attribute when set).</summary>
    public float ProjectileSpeed { get; init; }

    /// <summary>Impact radius in metres (<c>ammo.impact_radius</c>, stat override aware).</summary>
    public float ImpactRadius { get; init; }

    /// <summary>Maximum radius in metres (<c>ammo.max_radius</c>, stat override aware).</summary>
    public float MaxRadius { get; init; }

    /// <summary>Template fire type byte, kept for diagnostics.</summary>
    public byte FireType { get; init; }

    /// <summary>Template attack ability id (an aptitude chain in the original game), or 0.</summary>
    public uint AttackAbilityId { get; init; }

    /// <summary>Template melee ability id, or 0.</summary>
    public uint MeleeAbilityId { get; init; }

    /// <summary>
    ///     Local-space muzzle offset (<c>dbcharacter::Monster.projectile_offset</c>). When it is
    ///     zero the engine uses a chest-height offset instead.
    /// </summary>
    public Vector3 MuzzleOffset { get; init; }

    /// <summary>
    ///     <c>dbcharacter::MonsterAttributeRange</c> attribute 1144 (Creature Damage Modifier) at the
    ///     NPC's level; 1 when the monster has no row for it.
    /// </summary>
    public float CreatureDamageModifier { get; init; } = 1f;

    /// <summary>
    ///     Attribute 1145 (Creature Weapon Damage Modifier) of the weapon item, or 0 when the item has
    ///     no row for it. See <see cref="NpcAttackDamageMath.ResolvePerRound" /> for how it is spent.
    /// </summary>
    public float CreatureWeaponDamageModifier { get; init; }

    /// <summary>The parsed behaviour string the attack timing came from.</summary>
    public NpcBehaviorParams Behavior { get; init; } = NpcBehaviorParams.Empty;

    /// <summary>Whether the database gave this NPC a usable weapon.</summary>
    public bool HasWeapon => Mode != NpcAttackMode.None;

    /// <summary>Whether the attack is a projectile attack.</summary>
    public bool IsRanged => Mode == NpcAttackMode.Ranged;
}
