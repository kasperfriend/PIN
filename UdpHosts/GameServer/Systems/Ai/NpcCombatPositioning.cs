using System;
using System.Collections.Generic;
using System.Numerics;

namespace GameServer.Systems.Ai;

/// <summary>
///     Bounded PIN combat policy, not a reproduction of an unavailable CAIS cover tree.
///     Only reachable, supported positions may become tactical goals. Cover is a short
///     respite followed by a return to firing/chasing, never a permanent hiding state.
/// </summary>
public sealed class NpcCombatPositioning
{
    private Vector3? _goal;
    private ulong _until;
    private ulong _nextSearch;

    public void Reset()
    {
        _goal = null;
        _until = 0;
        _nextSearch = 0;
    }

    public Vector3? Goal(ulong now)
    {
        if (now >= _until)
        {
            _goal = null;
        }

        return _goal;
    }

    public bool CanSearch(ulong now) => !Goal(now).HasValue && now >= _nextSearch;

    /// <summary>At most eight local path queries, and at most one search per four seconds.</summary>
    public void Search(
        ulong now,
        ulong entityId,
        Vector3 position,
        Vector3 target,
        Vector3 home,
        float leashRadius,
        float standoff,
        float attackRange,
        bool seekCover,
        Func<Vector3, IReadOnlyList<Vector3>> pathTo,
        Func<Vector3, bool> occluded)
    {
        _nextSearch = now + 4000;
        var away = position - target;
        away.Z = 0f;
        away = away.LengthSquared() > 0.001f ? Vector3.Normalize(away) : Vector3.UnitX;
        var side = new Vector3(-away.Y, away.X, 0f);
        float distance = AiVectors.HorizontalDistance(position, target);
        bool retreat = distance < MathF.Min(standoff * 0.65f, 8f);
        float bestScore = float.NegativeInfinity;
        for (int i = 0; i < 8; i++)
        {
            // Alternating left/right preferences avoid a whole squad choosing one side.
            float angle = (i + (entityId % 2 == 0 ? 0 : 4)) * (MathF.PI / 4f);
            var candidate = position + (away * MathF.Cos(angle) + side * MathF.Sin(angle)) * 6f;
            if (AiVectors.HorizontalDistance(candidate, home) > leashRadius)
            {
                continue;
            }

            var path = pathTo(candidate);
            if (path == null || path.Count == 0)
            {
                continue;
            }

            var previous = position;
            float pathLength = 0f;
            foreach (var waypoint in path)
            {
                pathLength += AiVectors.Distance(previous, waypoint);
                previous = waypoint;
            }

            if (!float.IsFinite(pathLength) || pathLength > 12f)
            {
                continue;
            }

            var point = path[^1];
            float travel = AiVectors.HorizontalDistance(position, point);
            float range = AiVectors.Distance(point, target);
            if (!NpcGroundMovement.Finite(point) || travel < 2f || travel > 9f ||
                AiVectors.HorizontalDistance(point, home) > leashRadius || range > attackRange * 0.95f)
            {
                continue;
            }

            bool hidden = occluded(point);
            if ((!seekCover && hidden) || (retreat && range <= distance + 1f))
            {
                continue;
            }

            // Under fire/reloading, accept only actual cover. In open terrain ordinary
            // strafing/retreat is still available on the next search rather than fake cover.
            if (seekCover && !hidden)
            {
                continue;
            }

            float score = -MathF.Abs(range - standoff) - travel * 0.1f;
            if (score > bestScore)
            {
                bestScore = score;
                _goal = point;
            }
        }

        if (_goal.HasValue)
        {
            // Enough time to travel six metres and briefly reload, then expose/chase again.
            _until = now + 2500;
        }
    }
}
