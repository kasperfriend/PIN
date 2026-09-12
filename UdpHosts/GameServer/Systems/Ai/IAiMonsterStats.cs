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
