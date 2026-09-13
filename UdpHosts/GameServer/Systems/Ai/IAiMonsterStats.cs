using System.Collections.Generic;

namespace GameServer.Systems.Ai;

/// <summary>
///     Source of the per monster movement speeds. Abstracted because the static database
///     is only available on a real server, and the engine should not have to care.
/// </summary>
public interface IAiMonsterStats
{
    /// <summary>
    ///     The <c>normal_speed</c> and <c>fast_speed</c> of a monster row. Either value may
    ///     be zero when the row is missing or does not define it.
    /// </summary>
    (float NormalSpeed, float FastSpeed) GetSpeeds(uint characterTypeId);

    /// <summary>
    ///     The monster's original navigation body dimensions from <c>dbcharacter::Monster</c>.
    ///     These dimensions are used for corridor clearance; zero values mean the database row
    ///     did not provide a usable dimension and the navigation fallback is used.
    /// </summary>
    (float BodyRadius, float BodyHeight) GetBodyDimensions(uint characterTypeId);

    /// <summary>
    ///     The monster's behaviour strings: the base <c>behavior</c> column (the behaviour set it runs while
    ///     it has no target, where its idle emote lives) and the <c>behavior_offensive</c> one (the set it
    ///     runs while it is fighting). Either may be empty or carry no parentheses at all, and the third
    ///     column the database has, <c>behavior_defensive</c>, is not read because the engine has no
    ///     defensive state to run it in.
    /// </summary>
    /// <param name="characterTypeId">The <c>dbcharacter::Monster</c> row id.</param>
    /// <returns>The two behaviour strings, each <c>string.Empty</c> when the row or the column is missing.</returns>
    (string Base, string Offensive) GetBehaviors(uint characterTypeId);

    /// <summary>
    ///     The ability modules a behaviour string configures (<c>am1Id</c>/<c>am2Id</c> and their gates),
    ///     resolved into what the engine runs: see <see cref="NpcBehaviorAbilities" /> and
    ///     <see cref="NpcAbilityModule" />.
    /// </summary>
    /// <param name="behavior">The behaviour string the modules are read from.</param>
    /// <returns>One entry per configured module, in the order am1, am2; empty for most monsters.</returns>
    IReadOnlyList<NpcAbilityModuleScan> GetAbilityModules(NpcBehaviorParams behavior);

    /// <summary>
    ///     The monster's <c>dbcharacter::MonsterScaling</c> damage <b>rating</b> for
    ///     <paramref name="level"/>, straight from that row's <c>damage</c> column. This is the
    ///     level's damage budget (half of the level's health rating on every row of the table), not
    ///     what one swing is worth - <see cref="AiAttackDamage.Resolve"/> turns it into a per-hit
    ///     number. Returns 0 when the monster row or the scaling row for that level is missing,
    ///     which tells the caller to fall back to its configured default damage.
    /// </summary>
    int GetAttackDamage(uint characterTypeId, byte level);

    /// <summary>
    ///     The monster's weapon resolved from the static database: mode (melee/ranged), per-round
    ///     damage, rounds per burst, attack cadence, reach and, for ranged rows, the
    ///     <c>dbitems::Ammo</c> row the projectile is fired with. Returns
    ///     <see cref="NpcAttackProfile.Unarmed" /> when the monster or its weapon row does not
    ///     resolve, which tells the caller to keep the rating based attack of
    ///     <see cref="GetAttackDamage" />.
    /// </summary>
    NpcAttackProfile GetAttackProfile(uint characterTypeId, byte level);
}
