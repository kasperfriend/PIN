namespace GameServer.Systems.Ai;

/// <summary>
///     Everything a brain needs to know about the world for one decision. Built
///     by <see cref="AiEngine" /> from entity positions and physics queries.
/// </summary>
/// <param name="TargetId">Entity id of the target the engine wants the brain to reason about, 0 when there is none.</param>
/// <param name="TargetAlive">Whether that target exists and is still alive.</param>
/// <param name="TargetVisible">Whether there is an unobstructed line of sight to the target right now.</param>
/// <param name="DistanceToTarget">Horizontal distance in metres to the target.</param>
/// <param name="DistanceToHome">Horizontal distance in metres to the NPC's spawn point.</param>
/// <param name="CurrentTime">Server time in milliseconds.</param>
public readonly record struct AiPerception(
    ulong TargetId,
    bool TargetAlive,
    bool TargetVisible,
    float DistanceToTarget,
    float DistanceToHome,
    ulong CurrentTime);
