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
}
