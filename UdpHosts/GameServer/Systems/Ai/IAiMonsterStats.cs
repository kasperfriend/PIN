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
    ///     The attack damage a monster of <paramref name="level"/> deals per hit, straight from the
    ///     <c>damage</c> column of its <c>dbcharacter::MonsterScaling</c> row. 0 when the monster row
    ///     or the scaling row for that level is missing, which tells the caller to fall back to its
    ///     configured default damage.
    /// </summary>
    int GetAttackDamage(uint characterTypeId, byte level);
}
