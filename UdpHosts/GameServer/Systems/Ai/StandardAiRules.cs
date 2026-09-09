namespace GameServer.Systems.Ai;

/// <summary>
///     The out of the box AI tuning. Every value can be replaced by passing a
///     custom <see cref="IAiRules" /> to <see cref="AiEngine" />.
/// </summary>
/// <remarks>
///     The attack numbers are melee on purpose: PIN has no NPC projectiles yet (an attack is a
///     direct <c>IShard.Damage</c> call, see <c>Docs/NPC_AI.md</c> §3), so a mob may only swing
///     once it is next to you, and it may not swing through a floor.
/// </remarks>
public class StandardAiRules : IAiRules
{
    public bool Enabled { get; init; } = true;

    public float AggroRadius { get; init; } = 55f;

    public float MaxAcquisitionHeightDelta { get; init; } = 12f;

    /// <summary>
    ///     A monster's reach. The database's melee weapon rows are measured origin to origin too —
    ///     <c>dbitems::WeaponTemplates</c> id 9, the row the Melded Aranha's weapon points at, is
    ///     "NPC Melee Medium (Spyder)" with <c>range</c> 2.6 m — and this adds the slack of a body
    ///     radius on top, because chasing parks the mob at <see cref="StandoffRange"/> rather than
    ///     inside the target.
    /// </summary>
    public float AttackRange { get; init; } = 3.5f;

    public float AttackRangeExit { get; init; } = 5f;

    /// <summary>
    ///     About the height of a low crate: enough that a swing still connects with a player who
    ///     is jumping or standing on something small, low enough that a player on a balcony or a
    ///     rock the mob cannot climb is out of reach.
    /// </summary>
    public float MaxAttackHeightDelta { get; init; } = 2.5f;

    public float StandoffRange { get; init; } = 2f;

    public float LeashRadius { get; init; } = 120f;

    public float HomeArrivalRadius { get; init; } = 2f;

    public int AttackCooldownMs { get; init; } = 1200;

    /// <summary>
    ///     One attack commits a tenth of the monster's <c>dbcharacter::MonsterScaling</c> damage
    ///     rating for its level. The rating is a balance figure — it is exactly half of the level's
    ///     health rating on every one of the table's 80 rows — so read as a per-swing number it
    ///     kills a same-level player in one or two hits (the level-45 row is 13,934 against the
    ///     ~19-21k health pool that level has). A tenth of it leaves a level-matched mob needing
    ///     ~12-18 swings instead; see <c>Docs/NPC_AI.md</c> §5 for the per-level numbers.
    /// </summary>
    public float AttackDamageFraction { get; init; } = 0.1f;

    /// <summary>
    ///     Only used when the monster's <c>dbcharacter::MonsterScaling</c> row (by its level) cannot be
    ///     resolved; see <see cref="IAiMonsterStats.GetAttackDamage"/>. Real monsters attack for a
    ///     fraction of their database rating instead. This is that fraction applied to the level-1
    ///     rating (50 / 10 = 5), which is roughly what an unresolvable monster is worth: the fallback
    ///     is a weak punch, not a siege weapon — the 180 it used to carry was tuned against the
    ///     retired flat 19,192 health pool and takes a fresh frame's ~360 in two hits.
    /// </summary>
    public int AttackDamage { get; init; } = 5;

    public int TargetLostTimeoutMs { get; init; } = 6000;

    public int PerceptionIntervalMs { get; init; } = 200;

    public int MovementIntervalMs { get; init; } = 50;

    public bool SnapToGround { get; init; } = true;

    public float GroundOffset { get; init; }

    public float DefaultMoveSpeed { get; init; } = 5f;

    public float DefaultChaseSpeed { get; init; } = 8.5f;

    public float MinTrustedSpeed { get; init; } = 0.25f;

    public float MaxTrustedSpeed { get; init; } = 35f;
}
