using System;
using System.Collections.Generic;
using System.Numerics;

namespace GameServer.Systems.Ai;

/// <summary>
///     Deterministic navigation over the walkable surfaces supplied by the caller. This models the
///     original CAIS navigation contract: a ground sample says whether a cell exists, a collision
///     probe supplies the agent clearance, and the optional pathing-cost/exclusion queries carry
///     the zone's navigation metadata.
/// </summary>
/// <remarks>
///     <para>
///     The original game's runtime navmesh is not present in this repository. It must not be
///     described as reproduced by a generic straight-line fallback. This class therefore keeps the
///     original inputs explicit: excluded cells are never considered and a positive pathing cost is
///     part of A*'s edge cost. The game-server adapter can supply those values when its zone loader
///     has them; tests and collision-only deployments use the two-argument overload, which is the
///     unweighted compatibility path.
///     </para>
///     <para>
///     This class has no server or physics dependency. A missing ground sample means that the cell is
///     not walkable. The game-server adapter supplies the current position as a fallback when maps
///     collision is disabled, making pathfinding a no-op in the same configuration in which the old
///     straight-line movement was a no-op.
///     </para>
/// </remarks>
public static class NpcPathfinder
{
    /// <summary>Options for one local navigation search.</summary>
    public readonly record struct Options(
        float CellSize = 2f,
        float MaxStepHeight = 1.25f,
        float MaxSearchDistance = 64f,
        int MaxExpandedNodes = 4096,
        float WaypointTolerance = 0.35f)
    {
        /// <summary>The shipped navigation values.</summary>
        public static Options Default => new();
    }

    /// <summary>
    ///     Finds an unweighted path from <paramref name="start"/> to <paramref name="goal"/>. This
    ///     overload is retained for collision-only callers; use the metadata overload when loading
    ///     original zone pathing data.
    /// </summary>
    public static IReadOnlyList<Vector3> FindPath(
        Vector3 start,
        Vector3 goal,
        Func<Vector3, Vector3?> groundAt,
        Func<Vector3, Vector3, bool> blocked,
        Options options = default)
    {
        return FindPath(start, goal, groundAt, blocked, options, null, null);
    }

    /// <summary>
    ///     Finds a path from <paramref name="start"/> to <paramref name="goal"/>. The returned points
    ///     are ground positions, do not include the start point and always use the sampler's Z value.
    ///     An empty result means that no path was found; a one-point result is a direct path.
    /// </summary>
    /// <param name="groundAt">Returns the walkable ground below a point, or null for an unusable cell.</param>
    /// <param name="blocked">Returns true when an agent cannot traverse the segment.</param>
    /// <param name="options">The query limits and agent step constraints.</param>
    /// <param name="pathingCostAt">
    ///     Returns the original zone's AI pathing cost at a ground point. Values must be finite and
    ///     non-negative; a null callback means every walkable cell has cost 1.
    /// </param>
    /// <param name="excludedAt">Returns true for a zone region excluded from AI pathing.</param>
    public static IReadOnlyList<Vector3> FindPath(
        Vector3 start,
        Vector3 goal,
        Func<Vector3, Vector3?> groundAt,
        Func<Vector3, Vector3, bool> blocked,
        Options options,
        Func<Vector3, float>? pathingCostAt,
        Func<Vector3, bool>? excludedAt)
    {
        if (groundAt == null)
        {
            throw new ArgumentNullException(nameof(groundAt));
        }

        if (blocked == null)
        {
            throw new ArgumentNullException(nameof(blocked));
        }

        options = Sanitize(options);
        bool weighted = pathingCostAt != null || excludedAt != null;

        var startGround = groundAt(start);
        var goalGround = groundAt(goal);
        if (!startGround.HasValue || !goalGround.HasValue)
        {
            return Array.Empty<Vector3>();
        }

        var startPoint = startGround.Value;
        var goalPoint = goalGround.Value;
        if (IsExcluded(startPoint, excludedAt) || IsExcluded(goalPoint, excludedAt))
        {
            return Array.Empty<Vector3>();
        }

        if (AiVectors.HorizontalDistance(startPoint, goalPoint) <= options.WaypointTolerance &&
            AiVectors.HeightDelta(startPoint, goalPoint) <= options.MaxStepHeight)
        {
            return [goalPoint];
        }

        // A weighted query must be searched even when the direct segment is clear. Returning it
        // immediately would make AIPathingCost have no effect on route selection.
        if (!weighted && CanTraverse(startPoint, goalPoint, blocked, options))
        {
            return [goalPoint];
        }

        var goalOffset = goalPoint - startPoint;
        var goalKey = new GridKey(
            RoundToCell(goalOffset.X, options.CellSize),
            RoundToCell(goalOffset.Y, options.CellSize));
        int maxCells = (int)MathF.Ceiling(options.MaxSearchDistance / options.CellSize);
        if (Math.Abs(goalKey.X) > maxCells || Math.Abs(goalKey.Y) > maxCells)
        {
            return Array.Empty<Vector3>();
        }

        var groundCache = new Dictionary<GridKey, Vector3?>();
        var startKey = new GridKey(0, 0);
        groundCache[startKey] = startPoint;

        float CostFor(Vector3 point)
        {
            if (pathingCostAt == null)
            {
                return 1f;
            }

            float cost = pathingCostAt(point);
            return float.IsFinite(cost) && cost >= 0f ? cost : float.PositiveInfinity;
        }

        if (float.IsPositiveInfinity(CostFor(startPoint)) || float.IsPositiveInfinity(CostFor(goalPoint)))
        {
            return Array.Empty<Vector3>();
        }

        Vector3? GroundFor(GridKey key)
        {
            if (groundCache.TryGetValue(key, out var cached))
            {
                return cached;
            }

            var sample = new Vector3(
                startPoint.X + (key.X * options.CellSize),
                startPoint.Y + (key.Y * options.CellSize),
                startPoint.Z);
            if (IsExcluded(sample, excludedAt))
            {
                groundCache[key] = null;
                return null;
            }

            var result = groundAt(sample);
            if (result.HasValue && float.IsPositiveInfinity(CostFor(result.Value)))
            {
                result = null;
            }

            groundCache[key] = result;
            return result;
        }

        var open = new PriorityQueue<GridKey, float>();
        var cameFrom = new Dictionary<GridKey, GridKey>();
        var costSoFar = new Dictionary<GridKey, float> { [startKey] = 0f };
        var closed = new HashSet<GridKey>();
        open.Enqueue(startKey, weighted ? 0f : Heuristic(startKey, goalKey));

        int expanded = 0;
        bool reached = false;
        while (open.Count > 0 && expanded++ < options.MaxExpandedNodes)
        {
            var current = open.Dequeue();
            if (!closed.Add(current))
            {
                continue;
            }

            if (current == goalKey)
            {
                reached = true;
                break;
            }

            foreach (var (neighbor, diagonal) in Neighbors(current))
            {
                if (closed.Contains(neighbor) ||
                    Math.Abs(neighbor.X) > maxCells ||
                    Math.Abs(neighbor.Y) > maxCells)
                {
                    continue;
                }

                var from = GroundFor(current);
                var to = GroundFor(neighbor);
                if (!from.HasValue || !to.HasValue ||
                    !CanTraverse(from.Value, to.Value, blocked, options))
                {
                    continue;
                }

                // Do not let a diagonal cut through the corner of two walls. Both orthogonal
                // legs have to be usable as well; this is important for a character-sized agent,
                // not just a point moving through a tile corner.
                if (diagonal)
                {
                    var horizontal = new GridKey(neighbor.X, current.Y);
                    var vertical = new GridKey(current.X, neighbor.Y);
                    if (!CanTraverseKeys(current, horizontal, GroundFor, blocked, options) ||
                        !CanTraverseKeys(current, vertical, GroundFor, blocked, options))
                    {
                        continue;
                    }
                }

                float stepDistance = diagonal ? 1.4142135f : 1f;
                float fromCost = CostFor(from.Value);
                float toCost = CostFor(to.Value);
                if (float.IsPositiveInfinity(fromCost) || float.IsPositiveInfinity(toCost))
                {
                    continue;
                }

                // AIPathingCost is an edge weight, not a cosmetic preference. Dijkstra is used
                // for weighted queries rather than applying the unweighted heuristic, which may
                // otherwise prefer a short but deliberately expensive surface.
                float stepCost = stepDistance * ((fromCost + toCost) * 0.5f);
                float newCost = costSoFar[current] + stepCost;
                if (costSoFar.TryGetValue(neighbor, out var oldCost) && newCost >= oldCost)
                {
                    continue;
                }

                cameFrom[neighbor] = current;
                costSoFar[neighbor] = newCost;
                float priority = weighted ? newCost : newCost + Heuristic(neighbor, goalKey);
                open.Enqueue(neighbor, priority);
            }
        }

        if (!reached)
        {
            return Array.Empty<Vector3>();
        }

        var raw = new List<Vector3>();
        var cursor = goalKey;
        while (cursor != startKey)
        {
            var point = GroundFor(cursor);
            if (!point.HasValue)
            {
                return Array.Empty<Vector3>();
            }

            raw.Add(point.Value);
            if (!cameFrom.TryGetValue(cursor, out var parent))
            {
                return Array.Empty<Vector3>();
            }

            cursor = parent;
        }

        raw.Reverse();
        var finalGridPoint = raw.Count > 0 ? raw[^1] : startPoint;
        if (CanTraverse(finalGridPoint, goalPoint, blocked, options))
        {
            if (AiVectors.HorizontalDistance(finalGridPoint, goalPoint) > options.WaypointTolerance)
            {
                raw.Add(goalPoint);
            }
            else
            {
                raw[^1] = goalPoint;
            }
        }

        // A shortcut is safe for an unweighted collision query. In the weighted/original-data
        // mode it could cut across a high-cost surface and silently discard the route the
        // pathing-cost field selected, so preserve the searched route.
        return weighted ? raw : Simplify(raw, blocked, options);
    }

    private static bool IsExcluded(Vector3 point, Func<Vector3, bool>? excludedAt)
    {
        return excludedAt?.Invoke(point) == true;
    }

    private static Options Sanitize(Options options)
    {
        float cellSize = float.IsFinite(options.CellSize) && options.CellSize >= 0.5f ? options.CellSize : 2f;
        float maxStep = float.IsFinite(options.MaxStepHeight) && options.MaxStepHeight >= 0f ? options.MaxStepHeight : 1.25f;
        float maxDistance = float.IsFinite(options.MaxSearchDistance) && options.MaxSearchDistance >= cellSize
            ? options.MaxSearchDistance
            : 64f;
        int maxNodes = options.MaxExpandedNodes > 0 ? Math.Min(options.MaxExpandedNodes, 32_768) : 4096;
        float tolerance = float.IsFinite(options.WaypointTolerance) && options.WaypointTolerance >= 0.05f
            ? options.WaypointTolerance
            : 0.35f;
        return new Options(cellSize, maxStep, maxDistance, maxNodes, tolerance);
    }

    private static int RoundToCell(float value, float cellSize)
    {
        return (int)MathF.Round(value / cellSize, MidpointRounding.AwayFromZero);
    }

    private static float Heuristic(GridKey a, GridKey b)
    {
        int dx = Math.Abs(a.X - b.X);
        int dy = Math.Abs(a.Y - b.Y);
        int diagonal = Math.Min(dx, dy);
        return (diagonal * 1.4142135f) + (Math.Max(dx, dy) - diagonal);
    }

    private static IEnumerable<(GridKey Key, bool Diagonal)> Neighbors(GridKey key)
    {
        yield return (new GridKey(key.X - 1, key.Y), false);
        yield return (new GridKey(key.X + 1, key.Y), false);
        yield return (new GridKey(key.X, key.Y - 1), false);
        yield return (new GridKey(key.X, key.Y + 1), false);
        yield return (new GridKey(key.X - 1, key.Y - 1), true);
        yield return (new GridKey(key.X - 1, key.Y + 1), true);
        yield return (new GridKey(key.X + 1, key.Y - 1), true);
        yield return (new GridKey(key.X + 1, key.Y + 1), true);
    }

    private static bool CanTraverseKeys(
        GridKey fromKey,
        GridKey toKey,
        Func<GridKey, Vector3?> groundFor,
        Func<Vector3, Vector3, bool> blocked,
        Options options)
    {
        var from = groundFor(fromKey);
        var to = groundFor(toKey);
        return from.HasValue && to.HasValue && CanTraverse(from.Value, to.Value, blocked, options);
    }

    private static bool CanTraverse(
        Vector3 from,
        Vector3 to,
        Func<Vector3, Vector3, bool> blocked,
        Options options)
    {
        if (AiVectors.HeightDelta(from, to) > options.MaxStepHeight)
        {
            return false;
        }

        return !blocked(from, to);
    }

    private static IReadOnlyList<Vector3> Simplify(
        IReadOnlyList<Vector3> raw,
        Func<Vector3, Vector3, bool> blocked,
        Options options)
    {
        if (raw.Count < 3)
        {
            return raw;
        }

        var result = new List<Vector3> { raw[0] };
        int anchor = 0;
        while (anchor < raw.Count - 1)
        {
            int furthest = anchor + 1;
            for (int candidate = furthest + 1; candidate < raw.Count; candidate++)
            {
                if (!CanTraverse(raw[anchor], raw[candidate], blocked, options))
                {
                    break;
                }

                furthest = candidate;
            }

            if (furthest == anchor)
            {
                // Defensive only: furthest starts at anchor + 1, but retaining the guard keeps
                // this loop safe if the simplifier is changed later.
                furthest = anchor + 1;
            }

            if (result[^1] != raw[furthest])
            {
                result.Add(raw[furthest]);
            }

            anchor = furthest;
        }

        return result;
    }

    private readonly record struct GridKey(int X, int Y);
}
