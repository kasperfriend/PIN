namespace GameServer.Systems.Ai;

/// <summary>What the brain wants its body to do this tick, as far as locomotion goes.</summary>
public enum AiMovementIntent
{
    /// <summary>Hold position.</summary>
    None = 0,

    /// <summary>Walk towards the tracked target.</summary>
    TowardTarget = 1,

    /// <summary>Walk back to the spawn point.</summary>
    TowardHome = 2,
}

/// <summary>
///     The result of one brain decision. Pure data - applying it to an entity is
///     <see cref="AiEngine" />'s job.
/// </summary>
/// <param name="State">State the brain ended up in after this decision.</param>
/// <param name="Movement">Requested locomotion.</param>
/// <param name="FaceTarget">Whether the body should be turned towards the target.</param>
/// <param name="Attack">True when an attack should be resolved this tick.</param>
public readonly record struct AiDecision(
    AiBrainState State,
    AiMovementIntent Movement,
    bool FaceTarget,
    bool Attack);
