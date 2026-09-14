using System;
using System.Collections.Generic;
using System.Numerics;

namespace GameServer.Systems.Ai;

/// <summary>Collision-backed NPC navigation. No original patrol geometry is synthesized here.</summary>
public sealed class PhysicsNpcNavigation : INpcNavigation
{
    private static readonly NpcPathfinder.Options Options = new(
        CellSize: 2f, MaxStepHeight: NpcGroundMovement.MaximumStepHeight,
        MaxSearchDistance: 64f, MaxExpandedNodes: 4096, WaypointTolerance: 0.35f);

    private readonly IShard _shard;
    private readonly IAiRules _rules;

    public PhysicsNpcNavigation(IShard shard, IAiRules rules)
    {
        _shard = shard ?? throw new ArgumentNullException(nameof(shard));
        _rules = rules ?? throw new ArgumentNullException(nameof(rules));
    }

    public bool SupportsRoutines => _shard.Physics?.HasZoneCollision == true;

    public IReadOnlyList<Vector3> FindPath(Vector3 start, Vector3 goal, NpcNavigationAgent agent)
    {
        if (!NpcGroundMovement.Finite(start) || !NpcGroundMovement.Finite(goal))
        {
            return Array.Empty<Vector3>();
        }

        var physics = _shard.Physics;
        bool Blocked(Vector3 from, Vector3 to) => IsBlocked(from, to, agent);
        if (physics?.HasNavigationMesh == true)
        {
            // Disconnected mesh endpoints are a failure, not an excuse to switch to a flat grid.
            return physics.FindNavigationPath(start, goal, Blocked, Options.MaxStepHeight, Options.MaxSearchDistance)
                ?? Array.Empty<Vector3>();
        }

        Vector3? Ground(Vector3 point)
        {
            if (physics?.HasZoneCollision != true)
            {
                // Retain the existing collision-free combat development mode. Ambient routines
                // never enter it (SupportsRoutines is false without real world surfaces).
                return new Vector3(point.X, point.Y, start.Z);
            }

            return GroundAt(point)?.Position;
        }

        return NpcPathfinder.FindPath(start, goal, Ground, Blocked, Options,
            pathingCostAt: null,
            excludedAt: physics?.HasNavigationExclusions == true ? physics.IsNavigationExcluded : null);
    }

    public bool TryStep(Vector3 from, Vector3 desired, NpcNavigationAgent agent, out Vector3 position)
    {
        position = from;
        if (!NpcGroundMovement.Finite(from) || !NpcGroundMovement.Finite(desired))
        {
            return false;
        }

        if (_shard.Physics?.HasZoneCollision != true)
        {
            position = desired;
            return !IsBlocked(from, desired, agent);
        }

        bool result = NpcGroundMovement.TryStep(from, desired, GroundAt,
            (a, b) => IsBlocked(a, b, agent),
            _shard.Physics.IsNavigationExcluded, out var grounded);
        if (result)
        {
            position = _rules.SnapToGround ? grounded : desired;
        }

        return result;
    }

    private NpcGroundSurface? GroundAt(Vector3 point)
    {
        var physics = _shard.Physics;
        if (physics == null)
        {
            return null;
        }

        float offset = float.IsFinite(_rules.GroundOffset) ? _rules.GroundOffset : 0f;
        point.Z -= offset;
        if (!physics.TryGetGroundSurface(point, out var ground, out var normal,
            searchUp: NpcGroundMovement.MaximumStepHeight, searchDown: NpcGroundMovement.MaximumStepHeight) ||
            normal.Z < NpcGroundMovement.MinimumNormalZ)
        {
            return null;
        }

        ground.Z += offset;
        return new NpcGroundSurface(ground, normal);
    }

    private bool IsBlocked(Vector3 from, Vector3 to, NpcNavigationAgent agent)
    {
        var physics = _shard.Physics;
        if (physics == null)
        {
            return false;
        }

        Vector3 delta = to - from;
        float length = delta.Length();
        var flat = new Vector3(delta.X, delta.Y, 0f);
        if (flat.LengthSquared() <= 0.0001f)
        {
            return false;
        }

        var direction = Vector3.Normalize(flat);
        var side = new Vector3(-direction.Y, direction.X, 0f) * agent.Radius;
        // Static geometry only: using kinematic NPC bodies as route obstacles deadlocks crowds.
        // The activity reservation is what prevents two NPCs from occupying the same workstation.
        ReadOnlySpan<float> heights = [agent.Height * 0.45f, agent.Height * 0.8f];
        ReadOnlySpan<Vector3> offsets = [Vector3.Zero, side, -side];
        foreach (float height in heights)
        {
            foreach (var offset in offsets)
            {
                var up = new Vector3(0f, 0f, height);
                var hit = physics.SegmentRayCast(from + offset + up, to + offset + up, agent.EntityId, staticOnly: true);
                if (hit.Hit && hit.T < length - 0.05f)
                {
                    return true;
                }
            }
        }

        return false;
    }
}
