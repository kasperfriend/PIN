using System.Collections.Generic;
using System.Numerics;

namespace GameServer.Systems.Ai;

public readonly record struct NpcNavigationAgent(ulong EntityId, float Radius, float Height)
{
    /// <summary>
    ///     Whether this step may spend ray casts checking for walls, or must settle for the
    ///     terrain support check alone. The caller turns it off on a step that follows a
    ///     probed one, so a whole 100 ms of movement is cleared by one set of rays instead of
    ///     one set per 50 ms tick — the bulk of what a walking NPC cost on a populated zone.
    /// </summary>
    public bool ProbeWalls { get; init; } = true;

    /// <summary>Whether <see cref="WallProbeOrigin" /> is a real position.</summary>
    public bool HasWallProbeOrigin { get; init; }

    /// <summary>
    ///     The position the agent last checked for walls. A probe measures from here to the
    ///     new position, so it covers the movement the skipped ticks made, not just this
    ///     step's own segment.
    /// </summary>
    public Vector3 WallProbeOrigin { get; init; }
}

/// <summary>Shared by combat and ambient travel. An unsuccessful query never authorizes a direct move.</summary>
public interface INpcNavigation
{
    /// <summary>Whether there is real ground on which to generate ambient destinations.</summary>
    bool SupportsRoutines { get; }

    IReadOnlyList<Vector3> FindPath(Vector3 start, Vector3 goal, NpcNavigationAgent agent);

    /// <summary>Checks the whole step, including terrain support, before any entity state is written.</summary>
    bool TryStep(Vector3 from, Vector3 desired, NpcNavigationAgent agent, out Vector3 position);
}
