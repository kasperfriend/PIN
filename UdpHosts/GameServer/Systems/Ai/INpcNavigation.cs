using System.Collections.Generic;
using System.Numerics;

namespace GameServer.Systems.Ai;

public readonly record struct NpcNavigationAgent(ulong EntityId, float Radius, float Height);

/// <summary>Shared by combat and ambient travel. An unsuccessful query never authorizes a direct move.</summary>
public interface INpcNavigation
{
    /// <summary>Whether there is real ground on which to generate ambient destinations.</summary>
    bool SupportsRoutines { get; }

    IReadOnlyList<Vector3> FindPath(Vector3 start, Vector3 goal, NpcNavigationAgent agent);

    /// <summary>Checks the whole step, including terrain support, before any entity state is written.</summary>
    bool TryStep(Vector3 from, Vector3 desired, NpcNavigationAgent agent, out Vector3 position);
}
