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

    /// <summary>
    ///     Template <c>slot_index</c>, the PRNG seed a player shot uses for spread. Carried so an NPC
    ///     pellet of the same weapon scatters from the same slot the client would have used.
    /// </summary>
    public byte SlotIndex { get; init; }

    /// <summary>
    ///     Template <c>min_spread</c> (with item/slot modifiers already applied). The floor of the
    ///     weapon's cone; 0 on a weapon the database gives no spread.
    /// </summary>
    public float MinSpread { get; init; }

    /// <summary>Template <c>max_spread</c>, the ceiling of the weapon's cone.</summary>
    public float MaxSpread { get; init; }

    /// <summary>
    ///     Template <c>starting_spread</c>, the fraction of the (max - min) band the first shot of a
    ///     standing character opens at. See <see cref="NpcAttackSpreadMath.ResolveSpreadPct" />.
    /// </summary>
    public float StartingSpread { get; init; }

    /// <summary>
    ///     The spread percent one standing NPC attack fires at: the first-shot cone of the weapon's
    ///     own spread profile, with the item's attribute 958 applied when it carries one. 0 on a
    ///     weapon the database gives no spread, which is every melee row. See
    ///     <see cref="NpcAttackSpreadMath" />.
    /// </summary>
    public float SpreadPct { get; init; }

    /// <summary>
    ///     Length of one attack's animation in milliseconds, straight from the template's burst
    ///     columns (<c>ms_burst_duration</c> when the burst is fired over time, otherwise
    ///     <c>ms_per_burst</c>). This is the window the engine marks with
    ///     <c>CombatView.WeaponBurstFired</c> ... <c>WeaponBurstEnded</c>; it is independent of
    ///     <see cref="AttackIntervalMs" />, which is the behaviour's time between two attacks.
    /// </summary>
    public uint BurstDurationMs { get; init; }

    /// <summary>
    ///     The weapon's armed-pose animation id (<c>dbitems::WeaponTemplates.anim_armed_id</c>, with
    ///     the item's and slot's modifiers applied). The client picks the armed animation set from the
    ///     equipped template id; the value is carried here so the AI's animation decision is explicit
    ///     and testable. Absent on a template that carries none.
    /// </summary>
    public byte ArmedAnimationId { get; init; }

    /// <summary>Priority of the armed pose (<c>anim_armed_priority</c>, 100 on every weapon an NPC uses).</summary>
    public byte ArmedAnimationPriority { get; init; }

    /// <summary>Attack animation selector of the weapon (<c>anim_fire_type</c>).</summary>
    public byte FireAnimationType { get; init; }

    /// <summary>Reload animation selector of the weapon (<c>anim_reload_type</c>).</summary>
    public byte ReloadAnimationType { get; init; }

    /// <summary>Charge animation selector of the weapon (<c>anim_charge_type</c>).</summary>
    public byte ChargeAnimationType { get; init; }

    /// <summary>
    ///     Rounds one magazine holds: the weapon item's attribute 956 (Weapon Magazine Size) when it carries
    ///     one, otherwise the template's <c>base_clip_size</c>. 1 or less on the rows the database gives no
    ///     magazine (every melee row), which the engine reads as "this weapon never reloads".
    /// </summary>
    public ushort MagazineSize { get; init; }

    /// <summary>
    ///     Rounds one attack spends from the magazine: the template's <c>ammo_per_burst</c> when it carries it,
    ///     otherwise <c>rounds_per_burst</c> - a shotgun fires its 16 rounds for one shell, a rifle one round
    ///     per round. At least 1. See <see cref="NpcWeaponMagazine" />.
    /// </summary>
    public byte AmmoPerBurst { get; init; } = 1;

    /// <summary>
    ///     Milliseconds a reload takes (<c>dbitems::WeaponTemplates.reload_time</c>): the window the client
    ///     plays the weapon's reload animation (<c>anim_reload_type</c>) in, and the time the NPC cannot fire
    ///     for. 0 on a weapon the database gives no reload time.
    /// </summary>
    public uint ReloadTimeMs { get; init; }

    /// <summary>
    ///     Template attack ability id (<c>attack_ability_id</c>) - the aptitude chain the weapon runs before
    ///     a burst (a charge-up), or 0. <see cref="NpcWeaponAbilities" /> walks it, and
    ///     <see cref="AiEngine" /> runs it for the weapons whose chains carry an animation.
    /// </summary>
    public uint AttackAbilityId { get; init; }

    /// <summary>Template burst ability id (<c>burst_ability_id</c>) - the chain the weapon runs as it fires, or 0.</summary>
    public uint BurstAbilityId { get; init; }

    /// <summary>
    ///     Milliseconds the weapon charges before it fires (<c>ms_chargeup</c>). The ability chains of a
    ///     charging weapon apply a "replenishable" effect whose length lives in a server-only table this
    ///     build does not ship, so the weapon's own charge time is what an NPC's activation gives it: the
    ///     effect lasts as long as the charge the row describes (the chain's
    ///     <c>ReplenishEffectDurationCommandDef</c> hands it over). 0 on the weapons that do not charge.
    /// </summary>
    public uint ChargeUpMs { get; init; }

    /// <summary>
    ///     Whether the weapon's ability chains carry a command the client runs (see
    ///     <see cref="INpcAttackDataSource.IsClientCommand" />): an animation, an emote, a material switch,
    ///     a particle or an audio feedback. The server does not execute any of them, so the chain has to be
    ///     run for the effect it applies to replicate - that effect is what the client draws the weapon's
    ///     animation, muzzle flash and sound from.
    /// </summary>
    public bool ChainClientFeedback { get; init; }

    /// <summary>
    ///     Whether the weapon's ability chains deliver the hit themselves (<c>InflictDamageCommandDef</c> or
    ///     <c>FireProjectileCommandDef</c>). The chain is then the attack, and the AI's own direct hit -
    ///     melee damage or a projectile volley - must stand down so the target is not hit twice.
    /// </summary>
    public bool ChainDeliversDamage { get; init; }

    /// <summary>
    ///     Template melee ability id (<c>melee_ability_id</c>) - the chain a melee weapon runs as its
    ///     attack when it has no burst/attack ability, or 0. Walked with the other two attack ids (see
    ///     <see cref="NpcWeaponAbilities.Scan" />) and run as the fallback of
    ///     <see cref="AttackChainAbilityId" /> when the chain carries client feedback.
    /// </summary>
    public uint MeleeAbilityId { get; init; }

    /// <summary>
    ///     The ability the weapon fires when its magazine runs dry (<c>dbitems::WeaponTemplates.clip_empty_ability</c>),
    ///     or 0. The data says what the empty click is: template 12132 (Tesla Rifle 2.0) names 39239, which applies
    ///     effect 10480 - the dry-fire sound and its two muzzle particles, plus <c>restrict_weapon</c> for the 1.5 s
    ///     the effect lives, behind a <c>RequireWeaponArmed</c> duration gate.
    /// </summary>
    public uint ClipEmptyAbilityId { get; init; }

    /// <summary>
    ///     Whether the clip-empty ability's chains carry a command a client executes. A weapon whose empty ability
    ///     is server-side alone (35842, the <c>clip_empty_ability</c> of templates 11975 and 11971: a
    ///     <c>RegisterTimedTriggerCommandDef</c> and nothing else) is not activated - running it would change
    ///     nothing a client shows, exactly like a server-only burst chain.
    /// </summary>
    public bool ClipEmptyClientFeedback { get; init; }

    /// <summary>
    ///     The ability the weapon fires when a reload starts (<c>dbitems::WeaponTemplates.reload_ability</c>),
    ///     or 0. The sibling of <see cref="ClipEmptyAbilityId" />: the empty click is the moment the magazine
    ///     runs dry, this is the reload that follows. Gated the same way - a hook whose chains carry nothing
    ///     a client executes is left alone.
    /// </summary>
    public uint ReloadAbilityId { get; init; }

    /// <summary>
    ///     Whether the reload ability's chains carry a command a client executes. A server-only reload hook
    ///     is not activated, exactly like a server-only empty-clip or burst chain.
    /// </summary>
    public bool ReloadClientFeedback { get; init; }

    /// <summary>
    ///     The ability the weapon fires when a charge is held long enough to overcharge
    ///     (<c>dbitems::WeaponTemplates.overcharge_ability</c>), or 0. The delay is
    ///     <see cref="MsOverchargeDelay" />; an NPC charges for <see cref="ChargeUpMs" /> and then
    ///     fires, so the hook runs with the attack when that charge crosses the delay. See
    ///     <see cref="NpcWeaponOvercharge" />.
    /// </summary>
    public uint OverchargeAbilityId { get; init; }

    /// <summary>
    ///     Milliseconds a charge must be held before <see cref="OverchargeAbilityId" /> applies
    ///     (<c>ms_overcharge_delay</c>). 0 means the row does not overcharge.
    /// </summary>
    public uint MsOverchargeDelay { get; init; }

    /// <summary>
    ///     Whether the overcharge ability's chains carry a command a client executes. A server-only
    ///     overcharge hook is not activated, exactly like a server-only empty-clip or burst chain.
    /// </summary>
    public bool OverchargeClientFeedback { get; init; }

    /// <summary>
    ///     The ability id one attack actually runs: burst when the template names one, else attack, else
    ///     melee. The three columns are the database's own attack hooks; the engine picks one so a weapon
    ///     that only fills <c>melee_ability_id</c> still animates.
    /// </summary>
    public uint AttackChainAbilityId =>
        BurstAbilityId != 0 ? BurstAbilityId : AttackAbilityId != 0 ? AttackAbilityId : MeleeAbilityId;

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

    /// <summary>
    ///     Whether the weapon reloads when it runs dry: a projectile weapon whose magazine holds rounds and
    ///     that the database gives a reload time. Every melee row, and the rows the database gives neither a
    ///     magazine nor a reload time, fire without one. See <see cref="NpcWeaponMagazine" />.
    /// </summary>
    public bool Reloads => NpcWeaponMagazine.Reloads(IsRanged, MagazineSize, ReloadTimeMs);

    /// <summary>
    ///     Rounds one attack takes out of the magazine: <c>ammo_per_burst</c> when the template carries it,
    ///     else <c>rounds_per_burst</c>, never less than one round. A weapon the database gives no magazine
    ///     - every melee row - costs nothing, its magazine is <see cref="NpcWeaponMagazine.None" />.
    /// </summary>
    public int MagazineCost => NpcWeaponMagazine.ResolveCost(AmmoPerBurst, RoundsPerBurst);
}
