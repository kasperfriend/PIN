namespace GameServer.Systems.Ai;

/// <summary>
///     The high level behaviour an NPC brain is currently executing.
/// </summary>
public enum AiBrainState
{
    /// <summary>No target. The NPC stands where it spawned until something aggros it.</summary>
    Idle = 0,

    /// <summary>A target has been acquired and the NPC is closing the distance.</summary>
    Chase = 1,

    /// <summary>The target is inside the attack cone/range and the NPC is shooting at it.</summary>
    Attack = 2,

    /// <summary>The NPC was dragged past its leash and is walking back to its spawn point.</summary>
    Return = 3,

    /// <summary>The NPC is dead. Terminal state, no further decisions are made.</summary>
    Dead = 4,
}
