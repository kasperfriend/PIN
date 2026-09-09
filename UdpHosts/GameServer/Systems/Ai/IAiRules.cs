namespace GameServer.Systems.Ai;

/// <summary>
///     Tunable behaviour parameters for NPC AI. Split out from the engine so the
///     decision logic can be exercised in tests and so server operators can dial
///     the difficulty without touching code.
/// </summary>
public interface IAiRules
{
    /// <summary>Master switch. When false no brain ticks at all.</summary>
    bool Enabled { get; }

    /// <summary>Distance in metres at which an idle NPC notices a hostile player.</summary>
    float AggroRadius { get; }

    /// <summary>
    ///     Vertical bound of that notice range in metres: a player this far above (or below) an NPC
    ///     is not acquired at all, however close they are to it horizontally. 0 turns the bound off
    ///     and makes the aggro volume a cylinder instead of a squashed sphere.
    /// </summary>
    /// <remarks>
    ///     Without it a mob two storeys below a player walks up to the wall under them and stays
    ///     engaged forever (there is no pathfinding to solve the height difference yet), which reads
    ///     as "the mob found me through the floor".
    /// </remarks>
    float MaxAcquisitionHeightDelta { get; }

    /// <summary>
    ///     Distance in metres at which a chasing NPC switches into the attack state, measured
    ///     straight-line (including the height difference, see <see cref="AiVectors.Distance" />).
    ///     This is the monster's reach: as long as PIN has no NPC projectiles it is a melee reach,
    ///     so it is deliberately close to the monster's own melee weapon row
    ///     (<c>dbitems::WeaponTemplates.range</c>, e.g. 2.6 m for "NPC Melee Medium (Spyder)").
    /// </summary>
    float AttackRange { get; }

    /// <summary>
    ///     Distance in metres at which an attacking NPC falls back to chasing. Must be
    ///     larger than <see cref="AttackRange" /> so a target walking around the edge of
    ///     the range does not flip the state machine every tick.
    /// </summary>
    float AttackRangeExit { get; }

    /// <summary>
    ///     Largest height difference in metres a single attack may span. A melee swing reaches a
    ///     player standing on a crate or in the middle of a jump, but not one standing on the
    ///     balcony the mob is circling underneath.
    /// </summary>
    float MaxAttackHeightDelta { get; }

    /// <summary>Distance in metres the NPC tries to keep from its target once engaged.</summary>
    float StandoffRange { get; }

    /// <summary>How far in metres from its spawn point an NPC will follow a target before giving up.</summary>
    float LeashRadius { get; }

    /// <summary>Distance in metres from the spawn point that counts as "arrived home".</summary>
    float HomeArrivalRadius { get; }

    /// <summary>Minimum delay in milliseconds between two attacks by the same NPC.</summary>
    int AttackCooldownMs { get; }

    /// <summary>
    ///     The share of the monster's per-level damage rating that one attack commits. The rating
    ///     is a balance figure (see <see cref="IAiMonsterStats.GetAttackDamage" />), not a per-swing
    ///     amount, and using it whole makes a monster kill a same-level player in one or two hits.
    /// </summary>
    float AttackDamageFraction { get; }

    /// <summary>
    ///     Fallback damage applied to the target by one attack when the monster has no
    ///     <c>dbcharacter::MonsterScaling</c> damage row for its level. NPCs whose level resolves
    ///     normally attack for <see cref="AttackDamageFraction"/> of the database rating instead,
    ///     so this value is already a per-swing number and is not scaled any further.
    /// </summary>
    int AttackDamage { get; }

    /// <summary>Milliseconds an NPC keeps hunting a target it can no longer see before giving up.</summary>
    int TargetLostTimeoutMs { get; }

    /// <summary>Milliseconds between target acquisition / line of sight passes.</summary>
    int PerceptionIntervalMs { get; }

    /// <summary>Milliseconds between movement + pose broadcast passes.</summary>
    int MovementIntervalMs { get; }

    /// <summary>
    ///     Whether a downward ray cast should pull a moving NPC onto the ground surface.
    ///     On by default. A character origin sits at the feet (the muzzle offset in
    ///     <c>CharacterEntity.CalculateProjectileOrigin</c> is (0.2, 0, 1.62), i.e. chest
    ///     height above the origin), so <see cref="GroundOffset" /> is 0. The probe is a
    ///     no-op when no zone collision data is loaded.
    /// </summary>
    bool SnapToGround { get; }

    /// <summary>
    ///     Height in metres to add to the ground surface when <see cref="SnapToGround" /> is
    ///     on. 0 for the feet origin convention PIN uses.
    /// </summary>
    float GroundOffset { get; }

    /// <summary>Move speed in metres per second used when the monster row has no usable speed.</summary>
    float DefaultMoveSpeed { get; }

    /// <summary>Chase speed in metres per second used when the monster row has no usable speed.</summary>
    float DefaultChaseSpeed { get; }

    /// <summary>
    ///     Lower bound for a monster row speed to be trusted, in metres per second. Values
    ///     outside this window are treated as unset or as a different unit and ignored.
    /// </summary>
    float MinTrustedSpeed { get; }

    /// <summary>Upper bound for a monster row speed to be trusted, in metres per second.</summary>
    float MaxTrustedSpeed { get; }
}
