using System;
using System.Numerics;

namespace GameServer.Systems.Ai;

public readonly record struct NpcGroundSurface(Vector3 Position, Vector3 Normal);

/// <summary>
///     Terrain support for a ground agent. Unlike a single endpoint ray (or the old 100 m downward
///     snap), this checks every half metre of a step, rejecting holes, floors above/below, steep
///     surfaces and excluded regions. Pure callbacks keep the safety rules executable in tests.
/// </summary>
/// <remarks>
///     A surface's normal is tested by magnitude, not by sign: the zone's baked collision carries
///     both face windings, so the same walkable ground presents an up-facing normal where a lone
///     downward ray can see it and a down-facing one where only an upward ray can
///     (<see cref="Physics.PhysicsEngine.TryGetGroundSurface" />). What makes ground unwalkable is
///     being steep, and both signs of a steep normal are equally steep.
/// </remarks>
public static class NpcGroundMovement
{
    public const float MaximumStepHeight = 1.25f;
    public const float MinimumNormalZ = 0.35f;
    private const float SampleSpacing = 0.5f;

    public static bool TryStep(
        Vector3 from,
        Vector3 desired,
        Func<Vector3, NpcGroundSurface?> groundAt,
        Func<Vector3, Vector3, bool> blocked,
        Func<Vector3, bool> excluded,
        out Vector3 position)
    {
        position = from;
        if (!Finite(from) || !Finite(desired) || groundAt == null || blocked == null)
        {
            return false;
        }

        float distance = AiVectors.HorizontalDistance(from, desired);
        // Simulation steps are capped by AiEngine; this guard also makes the public helper bounded
        // for malformed callers. Long journeys consist of many ordinary movement steps.
        if (!float.IsFinite(distance) || distance > 32f)
        {
            return false;
        }

        int samples = Math.Max(1, (int)MathF.Ceiling(distance / SampleSpacing));
        var previous = from;
        for (int i = 1; i <= samples; i++)
        {
            var probe = Vector3.Lerp(from, desired, i / (float)samples);
            probe.Z = previous.Z;
            var surface = groundAt(probe);
            if (!surface.HasValue || !Finite(surface.Value.Position) || !Finite(surface.Value.Normal) ||
                MathF.Abs(surface.Value.Normal.Z) < MinimumNormalZ ||
                MathF.Abs(surface.Value.Position.Z - previous.Z) > MaximumStepHeight ||
                excluded?.Invoke(surface.Value.Position) == true ||
                blocked(previous, surface.Value.Position))
            {
                return false;
            }

            previous = surface.Value.Position;
        }

        position = previous;
        return true;
    }

    public static bool Finite(Vector3 value)
        => float.IsFinite(value.X) && float.IsFinite(value.Y) && float.IsFinite(value.Z);
}
