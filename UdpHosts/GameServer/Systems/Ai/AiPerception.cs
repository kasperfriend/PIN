namespace GameServer.Systems.Ai;

/// <summary>
///     Everything a brain needs to know about the world for one decision. Built
///     by <see cref="AiEngine" /> from entity positions and physics queries.
/// </summary>
/// <param name="TargetId">Entity id of the target the brain is asked to reason about, 0 when there is none.</param>
/// <param name="TargetAlive">Whether that target exists and is still alive.</param>
/// <param name="TargetVisible">Whether there is an unobstructed line of sight to the target right now.</param>
/// <param name="DistanceToTarget">Horizontal distance in metres to the target; what chasing and standing off are measured over.</param>
/// <param name="AttackDistance">
///     Straight-line (3D) distance in metres to the target; what an attack is measured over, so a
///     target standing above the NPC is not in melee reach just because it is over its head.
/// </param>
/// <param name="HeightDeltaToTarget">
///     Absolute height difference in metres between the NPC and the target, the second half of the
///     melee gate (<see cref="IAiRules.MaxAttackHeightDelta" />).
/// </param>
/// <param name="DistanceToHome">Horizontal distance in metres to the NPC's spawn point.</param>
/// <param name="CurrentTime">Server time in milliseconds.</param>
public readonly record struct AiPerception(
    ulong TargetId,
    bool TargetAlive,
    bool TargetVisible,
    float DistanceToTarget,
    float AttackDistance,
    float HeightDeltaToTarget,
    float DistanceToHome,
    ulong CurrentTime)
{
    /// <summary>
    ///     Flat-ground convenience: the distance an attack is measured over is the horizontal one and
    ///     there is no height difference. For targets on the same tile as the NPC (and for every test
    ///     that is not about the vertical case); <see cref="AiEngine" /> always fills in the real 3D
    ///     measurements instead.
    /// </summary>
    public AiPerception(
        ulong targetId,
        bool targetAlive,
        bool targetVisible,
        float distanceToTarget,
        float distanceToHome,
        ulong currentTime)
        : this(targetId, targetAlive, targetVisible, distanceToTarget, distanceToTarget, 0f, distanceToHome, currentTime)
    {
    }
}
