using System.Numerics;

namespace Shared.Collision.Navigation;

/// <summary>
///     A static, triangle-based navigation mesh built from the same zone collision surfaces that
///     are loaded into physics. It is intentionally small and data-driven: walkable faces,
///     adjacency, excluded areas and material costs are all derived from the original zone assets.
///     Overlapping copies of the same surface (chunk skirts, a zone file listing the same tile
///     twice) and small disconnected islands stacked over or under a larger surface (tree
///     canopies, cavities under rocks) are dropped so they cannot become spawn points.
/// </summary>
public sealed class NavigationMesh
{
    private const float SpatialCellSize = 16f;
    private const float VertexQuantization = 0.01f;

    /// <summary>
    ///     Faces whose centroids sit within this vertical window of each other and whose XY
    ///     projections overlap are the same surface recorded twice (chunk skirts, the zone file
    ///     listing the same tile as both 0x10101 and 0x10100). Keep the larger copy.
    /// </summary>
    private const float DuplicateHeight = 0.5f;

    /// <summary>
    ///     A disconnected walkable patch smaller than this, stacked over or under a larger one, is
    ///     a tree canopy or the cavity under a rock - not a balcony or a bridge. Dropped so NPCs
    ///     are not planned onto it.
    /// </summary>
    private const float SmallIslandArea = 24f;

    /// <summary>
    ///     Relative form of <see cref="SmallIslandArea"/>: a floating patch that covers less than
    ///     this fraction of the surface it overlaps is still a tree/cavity even when its absolute
    ///     area is a few dozen square metres.
    /// </summary>
    private const float SmallIslandRatio = 0.12f;

    /// <summary>
    ///     How far <see cref="FindPath"/> will pull a start or goal that is not on a triangle onto
    ///     the nearest walkable face. Ambient wander picks a random XY that often lands just off
    ///     the mesh; without this snap that destination is an empty path.
    /// </summary>
    private const float PathSnapRadius = 8f;

    private NavFace[] _faces;
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

        _faces = DropDuplicateOverlaps([.. faces]);
        BuildAdjacency();
        _faces = DropStackedIslands(_faces);
        BuildAdjacency();
        BuildSpatialIndex();
    }

    public int FaceCount => _faces.Length;

    /// <summary>
    ///     The centroid of the walkable face at <paramref name="faceIndex" />, or false when the
    ///     index is outside the mesh. Face centroids are the points this mesh's own pathfinding
    ///     walks between, so enumerating them yields every spot the baked collision considers
    ///     standable - which is what world population plans its spawn positions against.
    /// </summary>
    public bool TryGetFaceCentroid(int faceIndex, out Vector3 centroid)
    {
        if (faceIndex < 0 || faceIndex >= _faces.Length)
        {
            centroid = default;
            return false;
        }

        centroid = _faces[faceIndex].Centroid;
        return true;
    }

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

        int startFace = FindFace(start, PathSnapRadius, maxStepHeight);
        int goalFace = FindFace(goal, PathSnapRadius, maxStepHeight);
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

    private int FindFace(Vector3 point, float snapRadius, float maxStepHeight)
    {
        int containing = FindContainingFace(point);
        if (containing >= 0)
        {
            return containing;
        }

        return snapRadius > 0f ? FindNearestFace(point, snapRadius, maxStepHeight) : -1;
    }

    private int FindContainingFace(Vector3 point)
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

    /// <summary>
    ///     Nearest walkable face whose centroid is within <paramref name="snapRadius"/> horizontally
    ///     and <paramref name="maxStepHeight"/> vertically. Ambient wander picks a random XY that
    ///     often lands just off the mesh (a building footprint, a 20 cm gap); snapping that onto
    ///     the nearby ground is how a generated destination becomes a real walk, not a 2 s stall.
    ///     A roof or cave floor stays unreachable because of the height gate.
    /// </summary>
    private int FindNearestFace(Vector3 point, float snapRadius, float maxStepHeight)
    {
        var key = ToSpatialKey(point);
        int span = Math.Max(1, (int)MathF.Ceiling(snapRadius / SpatialCellSize) + 1);
        int best = -1;
        float bestDistanceSq = snapRadius * snapRadius;

        for (int x = key.X - span; x <= key.X + span; x++)
        {
            for (int y = key.Y - span; y <= key.Y + span; y++)
            {
                if (!_spatial.TryGetValue(new SpatialKey(x, y), out var candidates))
                {
                    continue;
                }

                foreach (int index in candidates)
                {
                    var centroid = _faces[index].Centroid;
                    if (MathF.Abs(centroid.Z - point.Z) > maxStepHeight)
                    {
                        continue;
                    }

                    float dx = centroid.X - point.X;
                    float dy = centroid.Y - point.Y;
                    float distanceSq = (dx * dx) + (dy * dy);
                    if (distanceSq < bestDistanceSq)
                    {
                        best = index;
                        bestDistanceSq = distanceSq;
                    }
                }
            }
        }

        return best;
    }

    /// <summary>
    ///     Drops faces that occupy the same XY at nearly the same height: overlapping zone/chunk
    ///     collision recorded twice. The larger face is the one that stays.
    /// </summary>
    private static NavFace[] DropDuplicateOverlaps(NavFace[] faces)
    {
        if (faces.Length < 2)
        {
            return faces;
        }

        var spatial = IndexFaces(faces);
        var drop = new bool[faces.Length];

        for (int i = 0; i < faces.Length; i++)
        {
            if (drop[i])
            {
                continue;
            }

            var centroid = faces[i].Centroid;
            var key = ToSpatialKey(centroid);
            float areaI = TriangleArea(faces[i].Triangle);

            for (int x = key.X - 1; x <= key.X + 1; x++)
            {
                for (int y = key.Y - 1; y <= key.Y + 1; y++)
                {
                    if (!spatial.TryGetValue(new SpatialKey(x, y), out var candidates))
                    {
                        continue;
                    }

                    foreach (int j in candidates)
                    {
                        if (j <= i || drop[j])
                        {
                            continue;
                        }

                        if (MathF.Abs(faces[j].Centroid.Z - centroid.Z) >= DuplicateHeight)
                        {
                            continue;
                        }

                        if (!ContainsHorizontal(centroid, faces[j].Triangle) &&
                            !ContainsHorizontal(faces[j].Centroid, faces[i].Triangle))
                        {
                            continue;
                        }

                        float areaJ = TriangleArea(faces[j].Triangle);
                        int loser = areaJ > areaI ? i : j;
                        drop[loser] = true;
                        if (drop[i])
                        {
                            break;
                        }
                    }

                    if (drop[i])
                    {
                        break;
                    }
                }

                if (drop[i])
                {
                    break;
                }
            }
        }

        return Compact(faces, drop);
    }

    /// <summary>
    ///     Drops disconnected walkable islands that sit over or under a larger surface. Those are
    ///     the tree canopies and under-rock cavities that would otherwise become spawn points;
    ///     bridges and balconies stay because they are either connected to the ground or large
    ///     enough to be their own layer.
    /// </summary>
    private static NavFace[] DropStackedIslands(NavFace[] faces)
    {
        if (faces.Length < 2)
        {
            return faces;
        }

        int[] parent = new int[faces.Length];
        for (int i = 0; i < parent.Length; i++)
        {
            parent[i] = i;
        }

        for (int i = 0; i < faces.Length; i++)
        {
            foreach (int neighbor in faces[i].Neighbors)
            {
                Union(parent, i, neighbor);
            }
        }

        var area = new float[faces.Length];
        for (int i = 0; i < faces.Length; i++)
        {
            area[Find(parent, i)] += TriangleArea(faces[i].Triangle);
        }

        var spatial = IndexFaces(faces);
        var dropComponent = new bool[faces.Length];

        for (int i = 0; i < faces.Length; i++)
        {
            int rootI = Find(parent, i);
            if (dropComponent[rootI])
            {
                continue;
            }

            var centroid = faces[i].Centroid;
            var key = ToSpatialKey(centroid);

            for (int x = key.X - 1; x <= key.X + 1; x++)
            {
                for (int y = key.Y - 1; y <= key.Y + 1; y++)
                {
                    if (!spatial.TryGetValue(new SpatialKey(x, y), out var candidates))
                    {
                        continue;
                    }

                    foreach (int j in candidates)
                    {
                        int rootJ = Find(parent, j);
                        if (rootJ == rootI || dropComponent[rootJ])
                        {
                            continue;
                        }

                        if (MathF.Abs(faces[j].Centroid.Z - centroid.Z) < DuplicateHeight)
                        {
                            continue;
                        }

                        if (!ContainsHorizontal(centroid, faces[j].Triangle))
                        {
                            continue;
                        }

                        if (IsSmallStackedIsland(area[rootI], area[rootJ]))
                        {
                            dropComponent[rootI] = true;
                        }
                        else if (IsSmallStackedIsland(area[rootJ], area[rootI]))
                        {
                            dropComponent[rootJ] = true;
                        }
                    }

                    if (dropComponent[rootI])
                    {
                        break;
                    }
                }

                if (dropComponent[rootI])
                {
                    break;
                }
            }
        }

        var drop = new bool[faces.Length];
        bool any = false;
        for (int i = 0; i < faces.Length; i++)
        {
            if (dropComponent[Find(parent, i)])
            {
                drop[i] = true;
                any = true;
            }
        }

        return any ? Compact(faces, drop) : faces;
    }

    private static bool IsSmallStackedIsland(float islandArea, float supportArea) =>
        islandArea < supportArea &&
        (islandArea < SmallIslandArea || islandArea < supportArea * SmallIslandRatio);

    private static Dictionary<SpatialKey, List<int>> IndexFaces(NavFace[] faces)
    {
        var spatial = new Dictionary<SpatialKey, List<int>>();
        for (int index = 0; index < faces.Length; index++)
        {
            var triangle = faces[index].Triangle;
            int minX = FloorCell(MathF.Min(triangle.A.X, MathF.Min(triangle.B.X, triangle.C.X)));
            int maxX = FloorCell(MathF.Max(triangle.A.X, MathF.Max(triangle.B.X, triangle.C.X)));
            int minY = FloorCell(MathF.Min(triangle.A.Y, MathF.Min(triangle.B.Y, triangle.C.Y)));
            int maxY = FloorCell(MathF.Max(triangle.A.Y, MathF.Max(triangle.B.Y, triangle.C.Y)));

            for (int x = minX; x <= maxX; x++)
            {
                for (int y = minY; y <= maxY; y++)
                {
                    var key = new SpatialKey(x, y);
                    if (!spatial.TryGetValue(key, out var list))
                    {
                        list = [];
                        spatial[key] = list;
                    }

                    list.Add(index);
                }
            }
        }

        return spatial;
    }

    private static NavFace[] Compact(NavFace[] faces, bool[] drop)
    {
        int kept = 0;
        for (int i = 0; i < drop.Length; i++)
        {
            if (!drop[i])
            {
                kept++;
            }
        }

        if (kept == faces.Length)
        {
            return faces;
        }

        var result = new NavFace[kept];
        int write = 0;
        for (int i = 0; i < faces.Length; i++)
        {
            if (!drop[i])
            {
                result[write++] = faces[i];
            }
        }

        return result;
    }

    private static int Find(int[] parent, int index)
    {
        while (parent[index] != index)
        {
            parent[index] = parent[parent[index]];
            index = parent[index];
        }

        return index;
    }

    private static void Union(int[] parent, int a, int b)
    {
        int rootA = Find(parent, a);
        int rootB = Find(parent, b);
        if (rootA != rootB)
        {
            parent[rootB] = rootA;
        }
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

    private sealed class NavFace
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
