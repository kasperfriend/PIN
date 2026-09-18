using System;
using System.Collections.Generic;
using System.Numerics;
using Serilog;

namespace GameServer.Systems.Ai;

/// <summary>Collision-backed NPC navigation. No original patrol geometry is synthesized here.</summary>
public sealed class PhysicsNpcNavigation : INpcNavigation
{
    /// <summary>
    ///     How long one NPC's "there is no walkable ground under me" line is held back after it is
    ///     written. The condition is per NPC and lasts until the ground changes, so the line is about
    ///     the first sighting, not about a rate.
    /// </summary>
    private const ulong MissingGroundWarningIntervalMs = 10_000;

    /// <summary>Entries the missing-ground warning table keeps before it starts over.</summary>
    private const int MissingGroundWarningCapacity = 1_024;

    private static readonly NpcPathfinder.Options Options = new(
        CellSize: 2f, MaxStepHeight: NpcGroundMovement.MaximumStepHeight,
        MaxSearchDistance: 64f, MaxExpandedNodes: 4096, WaypointTolerance: 0.35f);

    private static readonly ILogger _logger = Log.ForContext<PhysicsNpcNavigation>();

    /// <summary>
    ///     When the next missing-ground warning may be written, per entity. Cleared above
    ///     <see cref="MissingGroundWarningCapacity" /> entries, a count only reached by ids of NPCs
    ///     that have since left the world: the warning itself is what the operator needs, and a stale
    ///     id is worth less than the memory.
    /// </summary>
    private readonly Dictionary<ulong, ulong> _missingGroundWarnedAt = [];

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

        // The wall probe may have been skipped for this step (the caller probes every other
        // step and spans the gap): the probe then measures from the last probed position, so
        // the movement of the skipped ticks is what gets checked, not this segment alone.
        bool Blocked(Vector3 sampleFrom, Vector3 sampleTo)
        {
            if (!agent.ProbeWalls)
            {
                return false;
            }

            Vector3 origin = agent.HasWallProbeOrigin ? agent.WallProbeOrigin : sampleFrom;
            return IsBlocked(origin, sampleTo, agent);
        }

        if (_shard.Physics?.HasZoneCollision != true)
        {
            position = desired;
            return !Blocked(from, desired);
        }

        // A step fails for exactly two reasons - nothing to stand on along it, or something in the
        // way - and the first of them is invisible in every other signal the server has: the NPC
        // simply never moves while it keeps shooting, which reads as anything but a ground probe.
        // Remember where the ground was missing so a refused step can say so (see WarnIfNoGround).
        Vector3? missingGround = null;
        NpcGroundSurface? TrackGround(Vector3 point)
        {
            var surface = GroundAt(point);
            if (surface == null && missingGround == null)
            {
                missingGround = point;
            }

            return surface;
        }

        bool result = NpcGroundMovement.TryStep(from, desired, TrackGround,
            Blocked,
            _shard.Physics.IsNavigationExcluded, out var grounded);
        if (result)
        {
            position = _rules.SnapToGround ? grounded : desired;
        }
        else if (missingGround.HasValue)
        {
            WarnIfNoGround(agent, from, desired, missingGround.Value);
        }

        return result;
    }

    /// <summary>
    ///     Says once, per NPC and per <see cref="MissingGroundWarningIntervalMs" />, that a step was
    ///     refused because there was no walkable surface under the path it asked for. On a zone whose
    ///     collision has a face winding the probe cannot see through, this line is the whole symptom -
    ///     a mob that stands still and shoots - and the numbers it carries (the feet, the requested
    ///     step and the point with nothing under it) are what tells that apart from a wall.
    /// </summary>
    private void WarnIfNoGround(NpcNavigationAgent agent, Vector3 from, Vector3 desired, Vector3 missingGround)
    {
        ulong now = _shard.CurrentTime;
        if (_missingGroundWarnedAt.TryGetValue(agent.EntityId, out ulong warnedAt) &&
            now < warnedAt + MissingGroundWarningIntervalMs)
        {
            return;
        }

        if (_missingGroundWarnedAt.Count >= MissingGroundWarningCapacity)
        {
            _missingGroundWarnedAt.Clear();
        }

        _missingGroundWarnedAt[agent.EntityId] = now;
        _logger.Debug(
            "NPC {EntityId}: no walkable ground under {Ground} - the step from {From} to {To} was refused " +
            "(agent radius {Radius}, height {Height}, zone {ZoneId})",
            agent.EntityId, missingGround, from, desired, agent.Radius, agent.Height, _shard.ZoneId);
    }

    /// <summary>
    ///     The walkable surface under <paramref name="point" />, or null when there is none within a
    ///     step. The normal is tested by magnitude: the zone's collision carries both face windings,
    ///     and a surface wound away from the sky - one <see cref="Physics.PhysicsEngine.TryGetGroundSurface" />
    ///     sees only from below - describes the same walkable ground as any other.
    /// </summary>
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
            MathF.Abs(normal.Z) < NpcGroundMovement.MinimumNormalZ)
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
                var segFrom = from + offset + up;
                var segTo = to + offset + up;

                // Forward probe. Static-only; using kinematic NPC bodies as route obstacles
                // deadlocks crowds, and activity reservations prevent two NPCs from occupying
                // the same workstation.
                var hit = physics.SegmentRayCast(segFrom, segTo, agent.EntityId, staticOnly: true);
                float closestT = hit.Hit ? hit.T : float.MaxValue;

                if (!hit.Hit)
                {
                    // Reverse probe catches single-sided wall faces that the baker wound
                    // away from the agent - same mesh-side issue that broke the cave
                    // ceiling probe. Hit distance is measured from segTo in the reverse
                    // cast; convert back to a forward distance.
                    var backHit = physics.SegmentRayCast(segTo, segFrom, agent.EntityId, staticOnly: true);
                    if (backHit.Hit)
                    {
                        closestT = length - backHit.T;
                    }
                }

                if (closestT < length - 0.05f)
                {
                    return true;
                }
            }
        }

        return false;
    }
}
