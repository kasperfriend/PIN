using System.Numerics;

namespace Shared.Collision.Navigation;

/// <summary>
///     A static, triangle-based navigation mesh built from the same zone collision surfaces that
///     are loaded into physics. It is intentionally small and data-driven: walkable faces,
///     adjacency, excluded areas and material costs are all derived from the original zone assets.
/// </summary>
public sealed class NavigationMesh
{
    private const float SpatialCellSize = 16f;
    private const float VertexQuantization = 0.01f;

    private readonly NavFace[] _faces;
    private readonly Dictionary<SpatialKey, List<int>> _spatial = [];

    public NavigationMesh(
        IEnumerable<NavigationTriangle> triangles,
        Func<uint, float> materialCost,
        Func<Vector3, bool>? excludedAt = null,
        float minimumWalkableNormalZ = 0.35f)
    {
        if (triangles == null)
        {
            throw new ArgumentNullException(nameof(triangles));
        }

        materialCost ??= _ => 1f;
        var source = triangles.ToArray();
        var faces = new List<NavFace>(source.Length);

        foreach (var triangle in source)
        {
            var normal = triangle.Normal;
            if (normal.Z < minimumWalkableNormalZ ||
                !float.IsFinite(normal.Z) ||
                TriangleArea(triangle) < 0.0001f)
            {
                continue;
            }

            var centroid = triangle.Centroid;
            if (excludedAt?.Invoke(centroid) == true)
            {
                continue;
            }

            float cost = materialCost(triangle.PhysicsMaterialId);
            if (!float.IsFinite(cost) || cost < 0f)
            {
                cost = 1f;
            }

            faces.Add(new NavFace(triangle, centroid, MathF.Max(cost, 0.001f)));
        }

        _faces = [.. faces];
        BuildAdjacency();
        BuildSpatialIndex();
    }

    public int FaceCount => _faces.Length;

    /// <summary>
    ///     Finds a corridor through the baked collision surfaces. The returned points do not include
    ///     the start point. A null/empty result means that one endpoint is outside the walkable mesh,
    ///     the faces are disconnected, or a supplied runtime clearance probe rejected the corridor.
    /// </summary>
    public IReadOnlyList<Vector3> FindPath(
        Vector3 start,
        Vector3 goal,
        Func<Vector3, Vector3, bool>? blocked,
        float maxStepHeight,
        float maxSearchDistance = 128f,
        int maxExpandedFaces = 16_384)
    {
        if (_faces.Length == 0 ||
            HorizontalDistance(start, goal) > maxSearchDistance)
        {
            return Array.Empty<Vector3>();
        }

        int startFace = FindFace(start);
        int goalFace = FindFace(goal);
        if (startFace < 0 || goalFace < 0)
        {
            return Array.Empty<Vector3>();
        }

        var startPoint = ProjectToFace(start, _faces[startFace].Triangle);
        var goalPoint = ProjectToFace(goal, _faces[goalFace].Triangle);
        if (startFace == goalFace)
        {
            return CanTraverse(startPoint, goalPoint, blocked, maxStepHeight)
                ? [goalPoint]
                : Array.Empty<Vector3>();
        }

        var open = new PriorityQueue<int, float>();
        var cameFrom = new Dictionary<int, int>();
        var costSoFar = new Dictionary<int, float> { [startFace] = 0f };
        var closed = new HashSet<int>();
        open.Enqueue(startFace, 0f);

        int expanded = 0;
        bool reached = false;
        while (open.Count > 0 && expanded++ < maxExpandedFaces)
        {
            int current = open.Dequeue();
            if (!closed.Add(current))
            {
                continue;
            }

            if (current == goalFace)
            {
                reached = true;
                break;
            }

            foreach (int neighbor in _faces[current].Neighbors)
            {
                if (closed.Contains(neighbor))
                {
                    continue;
                }

                var from = _faces[current].Centroid;
                var to = _faces[neighbor].Centroid;
                if (!CanTraverse(from, to, blocked, maxStepHeight))
                {
                    continue;
                }

                float distance = Vector3.Distance(from, to);
                float edgeCost = distance * ((_faces[current].Cost + _faces[neighbor].Cost) * 0.5f);
                float nextCost = costSoFar[current] + edgeCost;
                if (costSoFar.TryGetValue(neighbor, out var oldCost) && nextCost >= oldCost)
                {
                    continue;
                }

                cameFrom[neighbor] = current;
                costSoFar[neighbor] = nextCost;
                // Use Dijkstra for material-weighted routes. This avoids a geometric heuristic
                // selecting a short but expensive surface and mirrors AIPathingCost semantics.
                open.Enqueue(neighbor, nextCost);
            }
        }

        if (!reached)
        {
            return Array.Empty<Vector3>();
        }

        var facePath = new List<int>();
        for (int cursor = goalFace; ; cursor = cameFrom[cursor])
        {
            facePath.Add(cursor);
            if (cursor == startFace)
            {
                break;
            }

            if (!cameFrom.ContainsKey(cursor))
            {
                return Array.Empty<Vector3>();
            }
        }

        facePath.Reverse();
        var raw = new List<Vector3>(facePath.Count + 1);
        for (int i = 1; i < facePath.Count; i++)
        {
            int previous = facePath[i - 1];
            int current = facePath[i];
            raw.Add(_faces[previous].Portals.GetValueOrDefault(current, _faces[current].Centroid));
        }

        if (raw.Count == 0 || HorizontalDistance(raw[^1], goalPoint) > 0.05f)
        {
            raw.Add(goalPoint);
        }
        else
        {
            raw[^1] = goalPoint;
        }

        return Simplify(startPoint, raw, blocked, maxStepHeight);
    }

    private int FindFace(Vector3 point)
    {
        var key = ToSpatialKey(point);
        int best = -1;
        float bestHeight = float.PositiveInfinity;

        // Check the local cell and its neighbors. A point can be slightly outside a triangle due
        // to floating-point conversion between the zone collision and the entity pose.
        for (int x = key.X - 1; x <= key.X + 1; x++)
        {
            for (int y = key.Y - 1; y <= key.Y + 1; y++)
            {
                if (!_spatial.TryGetValue(new SpatialKey(x, y), out var candidates))
                {
                    continue;
                }

                foreach (int index in candidates)
                {
                    var triangle = _faces[index].Triangle;
                    if (!ContainsHorizontal(point, triangle))
                    {
                        continue;
                    }

                    float height = MathF.Abs(ProjectToFace(point, triangle).Z - point.Z);
                    if (height < bestHeight)
                    {
                        best = index;
                        bestHeight = height;
                    }
                }
            }
        }

        return best;
    }

    private void BuildAdjacency()
    {
        var edges = new Dictionary<EdgeKey, List<int>>();
        var edgePoints = new Dictionary<EdgeKey, (Vector3 A, Vector3 B)>();
        for (int index = 0; index < _faces.Length; index++)
        {
            var triangle = _faces[index].Triangle;
            AddEdge(edges, edgePoints, EdgeKey.Create(triangle.A, triangle.B), index, triangle.A, triangle.B);
            AddEdge(edges, edgePoints, EdgeKey.Create(triangle.B, triangle.C), index, triangle.B, triangle.C);
            AddEdge(edges, edgePoints, EdgeKey.Create(triangle.C, triangle.A), index, triangle.C, triangle.A);
        }

        foreach (var face in _faces)
        {
            face.Neighbors.Clear();
            face.Portals.Clear();
        }

        foreach (var pair in edges)
        {
            var adjacent = pair.Value;
            if (adjacent.Count < 2)
            {
                continue;
            }

            var edge = edgePoints[pair.Key];
            var portal = (edge.A + edge.B) * 0.5f;
            for (int i = 0; i < adjacent.Count; i++)
            {
                for (int j = i + 1; j < adjacent.Count; j++)
                {
                    int first = adjacent[i];
                    int second = adjacent[j];
                    _faces[first].Neighbors.Add(second);
                    _faces[second].Neighbors.Add(first);
                    _faces[first].Portals[second] = portal;
                    _faces[second].Portals[first] = portal;
                }
            }
        }
    }

    private void BuildSpatialIndex()
    {
        for (int index = 0; index < _faces.Length; index++)
        {
            var triangle = _faces[index].Triangle;
            int minX = FloorCell(MathF.Min(triangle.A.X, MathF.Min(triangle.B.X, triangle.C.X)));
            int maxX = FloorCell(MathF.Max(triangle.A.X, MathF.Max(triangle.B.X, triangle.C.X)));
            int minY = FloorCell(MathF.Min(triangle.A.Y, MathF.Min(triangle.B.Y, triangle.C.Y)));
            int maxY = FloorCell(MathF.Max(triangle.A.Y, MathF.Max(triangle.B.Y, triangle.C.Y)));

            for (int x = minX; x <= maxX; x++)
            {
                for (int y = minY; y <= maxY; y++)
                {
                    var key = new SpatialKey(x, y);
                    if (!_spatial.TryGetValue(key, out var faces))
                    {
                        faces = [];
                        _spatial[key] = faces;
                    }

                    faces.Add(index);
                }
            }
        }
    }

    private static void AddEdge(
        Dictionary<EdgeKey, List<int>> edges,
        Dictionary<EdgeKey, (Vector3 A, Vector3 B)> edgePoints,
        EdgeKey edge,
        int face,
        Vector3 a,
        Vector3 b)
    {
        if (!edges.TryGetValue(edge, out var faces))
        {
            faces = [];
            edges[edge] = faces;
            edgePoints[edge] = (a, b);
        }

        faces.Add(face);
    }

    private static IReadOnlyList<Vector3> Simplify(
        Vector3 start,
        IReadOnlyList<Vector3> raw,
        Func<Vector3, Vector3, bool>? blocked,
        float maxStepHeight)
    {
        if (raw.Count < 2)
        {
            return raw;
        }

        var result = new List<Vector3>();
        Vector3 anchorPoint = start;
        int anchor = 0;
        while (anchor < raw.Count)
        {
            int furthest = anchor;
            for (int candidate = anchor + 1; candidate < raw.Count; candidate++)
            {
                if (!CanTraverse(anchorPoint, raw[candidate], blocked, maxStepHeight))
                {
                    break;
                }

                furthest = candidate;
            }

            result.Add(raw[furthest]);
            anchorPoint = raw[furthest];
            anchor = furthest + 1;
        }

        return result;
    }

    private static bool CanTraverse(
        Vector3 from,
        Vector3 to,
        Func<Vector3, Vector3, bool>? blocked,
        float maxStepHeight)
    {
        return MathF.Abs(to.Z - from.Z) <= maxStepHeight &&
               (blocked == null || !blocked(from, to));
    }

    private static bool ContainsHorizontal(Vector3 point, NavigationTriangle triangle)
    {
        Vector2 p = new(point.X, point.Y);
        Vector2 a = new(triangle.A.X, triangle.A.Y);
        Vector2 b = new(triangle.B.X, triangle.B.Y);
        Vector2 c = new(triangle.C.X, triangle.C.Y);
        float denominator = ((b.Y - c.Y) * (a.X - c.X)) + ((c.X - b.X) * (a.Y - c.Y));
        if (MathF.Abs(denominator) < 0.000001f)
        {
            return false;
        }

        float u = (((b.Y - c.Y) * (p.X - c.X)) + ((c.X - b.X) * (p.Y - c.Y))) / denominator;
        float v = (((c.Y - a.Y) * (p.X - c.X)) + ((a.X - c.X) * (p.Y - c.Y))) / denominator;
        float w = 1f - u - v;
        return u >= -0.01f && v >= -0.01f && w >= -0.01f;
    }

    private static Vector3 ProjectToFace(Vector3 point, NavigationTriangle triangle)
    {
        Vector2 p = new(point.X, point.Y);
        Vector2 a = new(triangle.A.X, triangle.A.Y);
        Vector2 b = new(triangle.B.X, triangle.B.Y);
        Vector2 c = new(triangle.C.X, triangle.C.Y);
        float denominator = ((b.Y - c.Y) * (a.X - c.X)) + ((c.X - b.X) * (a.Y - c.Y));
        if (MathF.Abs(denominator) < 0.000001f)
        {
            return triangle.Centroid;
        }

        float u = (((b.Y - c.Y) * (p.X - c.X)) + ((c.X - b.X) * (p.Y - c.Y))) / denominator;
        float v = (((c.Y - a.Y) * (p.X - c.X)) + ((a.X - c.X) * (p.Y - c.Y))) / denominator;
        float w = 1f - u - v;
        return (triangle.A * u) + (triangle.B * v) + (triangle.C * w);
    }

    private static float TriangleArea(NavigationTriangle triangle)
    {
        return Vector3.Cross(triangle.B - triangle.A, triangle.C - triangle.A).Length() * 0.5f;
    }

    private static float HorizontalDistance(Vector3 a, Vector3 b)
    {
        float x = a.X - b.X;
        float y = a.Y - b.Y;
        return MathF.Sqrt((x * x) + (y * y));
    }

    private static int FloorCell(float value) => (int)MathF.Floor(value / SpatialCellSize);

    private static SpatialKey ToSpatialKey(Vector3 point) => new(FloorCell(point.X), FloorCell(point.Y));

    private readonly class NavFace
    {
        public NavFace(NavigationTriangle triangle, Vector3 centroid, float cost)
        {
            Triangle = triangle;
            Centroid = centroid;
            Cost = cost;
        }

        public NavigationTriangle Triangle { get; }
        public Vector3 Centroid { get; }
        public float Cost { get; }
        public List<int> Neighbors { get; } = [];
        public Dictionary<int, Vector3> Portals { get; } = [];
    }

    private readonly record struct SpatialKey(int X, int Y);

    private readonly record struct EdgeKey(VertexKey A, VertexKey B)
    {
        public static EdgeKey Create(Vector3 a, Vector3 b)
        {
            var first = VertexKey.Create(a);
            var second = VertexKey.Create(b);
            return first.CompareTo(second) <= 0 ? new EdgeKey(first, second) : new EdgeKey(second, first);
        }
    }

    private readonly record struct VertexKey(int X, int Y, int Z) : IComparable<VertexKey>
    {
        public static VertexKey Create(Vector3 value)
        {
            return new VertexKey(
                (int)MathF.Round(value.X / VertexQuantization),
                (int)MathF.Round(value.Y / VertexQuantization),
                (int)MathF.Round(value.Z / VertexQuantization));
        }

        public int CompareTo(VertexKey other)
        {
            int x = X.CompareTo(other.X);
            if (x != 0) return x;
            int y = Y.CompareTo(other.Y);
            return y != 0 ? y : Z.CompareTo(other.Z);
        }
    }
}
