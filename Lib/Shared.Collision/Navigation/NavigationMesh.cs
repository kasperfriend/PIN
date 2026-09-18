using System.Numerics;
using Serilog;

namespace Shared.Collision.Navigation;

/// <summary>
///     A static, triangle-based navigation mesh built from the same zone collision surfaces that
///     are loaded into physics. It is intentionally small and data-driven: walkable faces,
///     adjacency, excluded areas and material costs are all derived from the original zone assets.
///     Overlapping copies of the same surface (chunk skirts, a zone file listing the same tile
///     twice) and small disconnected islands stacked over or under a larger surface (tree
///     canopies, cavities under rocks) are dropped so they cannot become spawn points, and
///     triangles whose physics material is never ground (the beds under the zone's water line)
///     are dropped for the same reason.
/// </summary>
/// <remarks>
///     This constructor is handed every walkable triangle of the zone - a prod zone's collision
///     bakes millions of them - and it runs on the thread that has not yet let a single client in
///     (the GameServer marks itself ready only once the shard, and with it the physics and this
///     mesh, exist). So every pass has to be linear in the face count with a small constant, and no
///     pass may compare a face against everything it merely *overlaps* in space: faces are matched
///     through buckets of their own centroid, which is where a duplicate of a surface is found, and
///     the island pass only looks at patches small enough to be a canopy at all, for a bounded total
///     number of comparisons. A zone that spends that budget keeps the faces it had not reached and
///     says so in the log; an unfiltered patch of ground is a worse outcome than a server that
///     never finishes starting, which is what an unbounded version of these passes looked like.
/// </remarks>
public sealed class NavigationMesh
{
    private static readonly ILogger _logger = Log.ForContext<NavigationMesh>();

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
    ///     Side of the bucket faces are met in when the mesh is looking for the same surface
    ///     recorded twice. Two copies of a triangle - a tile listed under both chunk ref layers, a
    ///     neighbour's skirt reaching over this one - have the same centroid to within the float
    ///     noise of the two files, so a bucket this small is enough to meet them and big enough
    ///     that nothing else does.
    /// </summary>
    private const float DuplicateBucketSize = 0.5f;

    /// <summary>
    ///     A disconnected walkable patch bigger than this - 64 m on a side, an eighth of a chunk tile
    ///     - is a level of its own: a bridge deck, a balcony, a plateau. The stacked-island test does
    ///     not look at it at all, whatever it happens to sit over. Canopies and the cavity under a
    ///     rock are far below it, and the cap is also what keeps that test from walking the ground
    ///     under every single face of a zone whose foliage is a hundred thousand separate little
    ///     patches.
    /// </summary>
    private const float IslandCandidateMaxArea = 4096f;

    /// <summary>
    ///     How many face pairs the stacked-island pass may examine over a whole zone before it stops
    ///     looking and keeps every patch it has not judged. A bound, not a tuning knob: a zone that
    ///     reaches it is a zone whose bake would otherwise run for as long as the machine is left
    ///     alone, and a canopy that stayed in the mesh costs a few refused spawn cells (the placement
    ///     probe rejects a spot with no ground under it anyway).
    /// </summary>
    private const int IslandSupportCandidateBudget = 32_000_000;

    /// <summary>
    ///     How many cells of the runtime spatial index one face may be entered into along an axis. A
    ///     collision triangle never legitimately covers more than a chunk tile (32 cells); the cap
    ///     is there so that one absurd or non-finite vertex cannot turn the loop that fills the index
    ///     into a walk over billions of cells - a cast of a non-finite float to int lands on
    ///     <see cref="int.MinValue"/>, and a loop from there to the face's other corner never ends.
    /// </summary>
    private const int MaxIndexSpanCells = 64;

    /// <summary>
    ///     How far <see cref="FindPath"/> will pull a start or goal that is not on a triangle onto
    ///     the nearest walkable face. Ambient wander picks a random XY that often lands just off
    ///     the mesh; without this snap that destination is an empty path.
    /// </summary>
    private const float PathSnapRadius = 8f;

    private NavFace[] _faces;
    private readonly Dictionary<SpatialKey, List<int>> _spatial = [];

    /// <param name="materialExcluded">
    ///     Decides, by physics material id, whether a triangle is collision but never navigation
    ///     ground. The zone tags the beds under its water line with the Water materials: a mob
    ///     planned onto one would stand under water, invisible to whoever walks above it yet
    ///     able to see and shoot them, so those faces join neither this mesh nor the spawn data
    ///     the mesh enumerates. Nothing is excluded when omitted.
    /// </param>
    public NavigationMesh(
        IEnumerable<NavigationTriangle> triangles,
        Func<uint, float> materialCost,
        Func<Vector3, bool>? excludedAt = null,
        float minimumWalkableNormalZ = 0.35f,
        Func<uint, bool>? materialExcluded = null)
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
            if (materialExcluded?.Invoke(triangle.PhysicsMaterialId) == true)
            {
                continue;
            }

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

        // Same surface twice first, then the patches that float over or hang under the ground, then
        // the adjacency the pathfinder walks. The edge map the adjacency is built from is collected
        // once and re-keyed through the second pass's compaction rather than rebuilt: three hashed
        // inserts per face is the most expensive thing this constructor does, and the island pass
        // only needs to know which faces are connected, which the same map answers.
        _faces = [.. faces];
        _faces = Compact(_faces, MarkDuplicateFaces(), out _);

        var edges = new Dictionary<EdgeKey, List<int>>();
        var edgePoints = new Dictionary<EdgeKey, (Vector3 A, Vector3 B)>();
        CollectSharedEdges(edges, edgePoints);

        _faces = Compact(_faces, MarkStackedIslands(edges), out var compactedFrom);
        BuildAdjacency(edges, edgePoints, compactedFrom);
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

        var startPoint = ClosestPointOnFace(start, _faces[startFace].Triangle);
        var goalPoint = ClosestPointOnFace(goal, _faces[goalFace].Triangle);
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
    ///     Marks the faces that occupy the same XY at nearly the same height - the same surface
    ///     recorded twice - and keeps the larger copy (the first of the two, on equal area).
    /// </summary>
    /// <remarks>
    ///     Faces meet through a bucket of their quantised centroid, which is where a duplicate of a
    ///     surface is: two copies of a triangle have the same centroid, to within the float noise of
    ///     the two files they came from. Matching faces by spatial overlap instead - everything
    ///     whose bounds reach the same cell - is the shape a bake must not have: dense ground shares
    ///     its cells with every small triangle around it, so the pass costs a neighbourhood scan per
    ///     face, twice over a zone's few million of them, which is a server that loads its chunks and
    ///     then never finishes. Copies whose centroids land on opposite sides of a bucket border go
    ///     unmatched, and that costs one extra face in the mesh: the zone's own chunk references are
    ///     deduplicated while it is read, so nothing here is the only thing standing between a tile
    ///     and its own copy.
    /// </remarks>
    private bool[] MarkDuplicateFaces()
    {
        var drop = new bool[_faces.Length];
        if (_faces.Length < 2)
        {
            return drop;
        }

        // Bucket -> the largest face in it that is still standing.
        var largest = new Dictionary<DuplicateKey, int>();
        for (int i = 0; i < _faces.Length; i++)
        {
            var centroid = _faces[i].Centroid;
            var key = DuplicateKey.Of(centroid);
            float area = TriangleArea(_faces[i].Triangle);

            if (largest.TryGetValue(key, out int other) && !drop[other])
            {
                var otherCentroid = _faces[other].Centroid;
                var sameSurface = MathF.Abs(otherCentroid.Z - centroid.Z) < DuplicateHeight &&
                                  (ContainsHorizontal(centroid, _faces[other].Triangle) ||
                                   ContainsHorizontal(otherCentroid, _faces[i].Triangle));
                if (sameSurface)
                {
                    if (TriangleArea(_faces[other].Triangle) >= area)
                    {
                        // The mesh already holds the better copy of this surface.
                        drop[i] = true;
                        continue;
                    }

                    drop[other] = true;
                }
            }

            // The bucket keeps its largest face as the representative, so a third copy is compared
            // against the one that would win rather than against a face that has just been dropped.
            if (!largest.TryGetValue(key, out int current) || drop[current] ||
                area >= TriangleArea(_faces[current].Triangle))
            {
                largest[key] = i;
            }
        }

        return drop;
    }

    /// <summary>
    ///     Marks the faces of disconnected walkable patches that sit over or under a larger surface:
    ///     the tree canopies and the cavities under rocks that would otherwise become spawn points.
    ///     Bridges and balconies stay, because they are either connected to the ground or a level of
    ///     their own.
    /// </summary>
    /// <remarks>
    ///     Whether a patch is connected comes from the zone's own shared-edge map, so no adjacency
    ///     has to be built just to ask that question. Only the faces of patches small enough to be an
    ///     island at all are looked at (<see cref="IslandCandidateMaxArea"/>), which is what makes
    ///     the pass affordable: a zone's ground is one component holding most of its faces, and
    ///     comparing each of those with the neighbourhood around it - to conclude, correctly, that
    ///     the ground is not floating - is the quadratic this whole bake is bounded by. The
    ///     neighbourhood a patch is tested against is read from the centroid index
    ///     (<see cref="IndexFacesByCentroid"/>), and the pass spends at most
    ///     <see cref="IslandSupportCandidateBudget"/> comparisons over the zone: whatever it did not
    ///     reach is kept, and said about in the log.
    /// </remarks>
    /// <param name="sharedEdges">The faces sharing each quantised edge of the mesh.</param>
    private bool[] MarkStackedIslands(Dictionary<EdgeKey, List<int>> sharedEdges)
    {
        var drop = new bool[_faces.Length];
        if (_faces.Length < 2)
        {
            return drop;
        }

        int[] parent = new int[_faces.Length];
        for (int i = 0; i < parent.Length; i++)
        {
            parent[i] = i;
        }

        foreach (var group in sharedEdges.Values)
        {
            // Every face on an edge is connected to every other face on it, so joining the group to
            // its first member is enough.
            for (int i = 1; i < group.Count; i++)
            {
                Union(parent, group[0], group[i]);
            }
        }

        var area = new float[_faces.Length];
        for (int i = 0; i < _faces.Length; i++)
        {
            area[Find(parent, i)] += TriangleArea(_faces[i].Triangle);
        }

        var supports = IndexFacesByCentroid();
        var dropComponent = new bool[_faces.Length];
        int budget = IslandSupportCandidateBudget;
        bool outOfBudget = false;

        for (int i = 0; i < _faces.Length && !outOfBudget; i++)
        {
            int rootI = Find(parent, i);
            if (dropComponent[rootI] || area[rootI] >= IslandCandidateMaxArea)
            {
                continue;
            }

            var centroid = _faces[i].Centroid;
            var key = ToSpatialKey(centroid);
            bool island = false;

            for (int x = key.X - 1; x <= key.X + 1 && !island && !outOfBudget; x++)
            {
                for (int y = key.Y - 1; y <= key.Y + 1 && !island && !outOfBudget; y++)
                {
                    if (!supports.TryGetValue(new SpatialKey(x, y), out var candidates))
                    {
                        continue;
                    }

                    foreach (int j in candidates)
                    {
                        if (--budget < 0)
                        {
                            outOfBudget = true;
                            break;
                        }

                        int rootJ = Find(parent, j);
                        if (rootJ == rootI || dropComponent[rootJ] || area[rootJ] <= area[rootI])
                        {
                            continue;
                        }

                        if (MathF.Abs(_faces[j].Centroid.Z - centroid.Z) < DuplicateHeight)
                        {
                            continue;
                        }

                        if (!ContainsHorizontal(centroid, _faces[j].Triangle))
                        {
                            continue;
                        }

                        if (IsSmallStackedIsland(area[rootI], area[rootJ]))
                        {
                            dropComponent[rootI] = true;
                            island = true;
                        }
                    }
                }
            }
        }

        if (outOfBudget)
        {
            _logger.Warning(
                "Navigation mesh: the stacked-island filter spent its {Budget} candidate comparisons and kept every patch it had not judged, so the rest of the zone's mesh may still hold canopies and cavities",
                IslandSupportCandidateBudget);
        }

        for (int i = 0; i < _faces.Length; i++)
        {
            if (dropComponent[Find(parent, i)])
            {
                drop[i] = true;
            }
        }

        return drop;
    }

    private static bool IsSmallStackedIsland(float islandArea, float supportArea) =>
        islandArea < supportArea &&
        (islandArea < SmallIslandArea || islandArea < supportArea * SmallIslandRatio);

    /// <summary>
    ///     The faces bucketed by the cell their own centroid falls in: one cell per face. Entering a
    ///     face in every cell its bounds cover is what a query for a point *inside* a large face needs
    ///     (<see cref="BuildSpatialIndex"/>), and what makes a scan over a cell's contents quadratic -
    ///     a big triangle shares its cells with every small one in the area. The passes above ask
    ///     "which faces are at this point", and the ones that answer it are centred there.
    /// </summary>
    private Dictionary<SpatialKey, List<int>> IndexFacesByCentroid()
    {
        var spatial = new Dictionary<SpatialKey, List<int>>();
        for (int index = 0; index < _faces.Length; index++)
        {
            var key = ToSpatialKey(_faces[index].Centroid);
            if (!spatial.TryGetValue(key, out var faces))
            {
                faces = [];
                spatial[key] = faces;
            }

            faces.Add(index);
        }

        return spatial;
    }

    /// <summary>
    ///     Compacts the faces a pass marked for dropping into a dense array: from here on the mesh is
    ///     indexed by position, both by its own pathfinding and by whatever enumerates its walkable
    ///     faces (world population plans against face indices).
    /// </summary>
    /// <param name="remap">
    ///     The pre-compaction index of every survivor, and -1 for every face that is gone; null when
    ///     nothing was dropped. This is what a structure collected over the old indices - the edge map
    ///     - is re-keyed through instead of being rebuilt from the geometry again.
    /// </param>
    private static NavFace[] Compact(NavFace[] faces, bool[] drop, out int[]? remap)
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
            remap = null;
            return faces;
        }

        remap = new int[faces.Length];
        var result = new NavFace[kept];
        int write = 0;
        for (int i = 0; i < faces.Length; i++)
        {
            if (drop[i])
            {
                remap[i] = -1;
                continue;
            }

            remap[i] = write;
            result[write++] = faces[i];
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

    /// <summary>
    ///     Groups the faces by the edge they share. Quantised vertices make the grouping exact
    ///     without having to trust float equality between the two files a duplicated surface came
    ///     from. Collected separately from <see cref="BuildAdjacency"/> because the stacked-island
    ///     pass needs the grouping to know which faces are connected, and needs nothing else: the
    ///     adjacency itself is only worth building once the faces that survive have been compacted.
    /// </summary>
    private void CollectSharedEdges(
        Dictionary<EdgeKey, List<int>> edges,
        Dictionary<EdgeKey, (Vector3 A, Vector3 B)> edgePoints)
    {
        for (int index = 0; index < _faces.Length; index++)
        {
            var triangle = _faces[index].Triangle;
            AddEdge(edges, edgePoints, EdgeKey.Create(triangle.A, triangle.B), index, triangle.A, triangle.B);
            AddEdge(edges, edgePoints, EdgeKey.Create(triangle.B, triangle.C), index, triangle.B, triangle.C);
            AddEdge(edges, edgePoints, EdgeKey.Create(triangle.C, triangle.A), index, triangle.C, triangle.A);
        }
    }

    /// <summary>
    ///     Fills in the adjacency and the portal points the pathfinder walks, from the zone's edge map.
    /// </summary>
    /// <param name="remap">
    ///     Translates the map's face indices, collected before faces were dropped, into the ones the
    ///     compacted mesh uses; null when nothing was dropped since.
    /// </param>
    private void BuildAdjacency(
        Dictionary<EdgeKey, List<int>> edges,
        Dictionary<EdgeKey, (Vector3 A, Vector3 B)> edgePoints,
        int[]? remap)
    {
        foreach (var face in _faces)
        {
            face.Neighbors.Clear();
            face.Portals.Clear();
        }

        foreach (var pair in edges)
        {
            var adjacent = pair.Value;
            if (remap != null)
            {
                // The lists are read here and never again, so they are rewritten in place rather
                // than copied: the survivors move up to the front, the dropped faces fall off the
                // end, and a face that is gone cannot be made adjacent to anything.
                int kept = 0;
                for (int i = 0; i < adjacent.Count; i++)
                {
                    int mapped = remap[adjacent[i]];
                    if (mapped >= 0)
                    {
                        adjacent[kept++] = mapped;
                    }
                }

                adjacent.RemoveRange(kept, adjacent.Count - kept);
            }

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

    /// <summary>
    ///     Enters every face in the cells its horizontal bounds cover, so a query for a point sitting
    ///     on a face finds it however large the face is. The span along an axis is capped at
    ///     <see cref="MaxIndexSpanCells"/> cells: a face outside that - one whose vertex coordinates
    ///     are absurd, or not finite at all, which is exactly what turns this loop into a walk over
    ///     billions of cells - is entered at its centroid cell alone, where it can still be found by
    ///     anything near its middle.
    /// </summary>
    private void BuildSpatialIndex()
    {
        for (int index = 0; index < _faces.Length; index++)
        {
            var triangle = _faces[index].Triangle;
            int minX = FloorCell(MathF.Min(triangle.A.X, MathF.Min(triangle.B.X, triangle.C.X)));
            int maxX = FloorCell(MathF.Max(triangle.A.X, MathF.Max(triangle.B.X, triangle.C.X)));
            int minY = FloorCell(MathF.Min(triangle.A.Y, MathF.Min(triangle.B.Y, triangle.C.Y)));
            int maxY = FloorCell(MathF.Max(triangle.A.Y, MathF.Max(triangle.B.Y, triangle.C.Y)));

            if ((long)maxX - minX > MaxIndexSpanCells || (long)maxY - minY > MaxIndexSpanCells)
            {
                AddFaceToCell(ToSpatialKey(_faces[index].Centroid), index);
                continue;
            }

            for (int x = minX; x <= maxX; x++)
            {
                for (int y = minY; y <= maxY; y++)
                {
                    AddFaceToCell(new SpatialKey(x, y), index);
                }
            }
        }
    }

    private void AddFaceToCell(SpatialKey key, int faceIndex)
    {
        if (!_spatial.TryGetValue(key, out var faces))
        {
            faces = [];
            _spatial[key] = faces;
        }

        faces.Add(faceIndex);
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

    /// <summary>
    ///     Closest point on the triangle, not the unconstrained planar projection.
    ///     <see cref="ProjectToFace"/> can land outside the face when the query is off-mesh;
    ///     a wander goal that snapped onto a nearby triangle still has to stand on it.
    /// </summary>
    private static Vector3 ClosestPointOnFace(Vector3 point, NavigationTriangle triangle)
    {
        var a = triangle.A;
        var b = triangle.B;
        var c = triangle.C;
        var ab = b - a;
        var ac = c - a;
        var ap = point - a;

        float d1 = Vector3.Dot(ab, ap);
        float d2 = Vector3.Dot(ac, ap);
        if (d1 <= 0f && d2 <= 0f)
        {
            return a;
        }

        var bp = point - b;
        float d3 = Vector3.Dot(ab, bp);
        float d4 = Vector3.Dot(ac, bp);
        if (d3 >= 0f && d4 <= d3)
        {
            return b;
        }

        float vc = (d1 * d4) - (d3 * d2);
        if (vc <= 0f && d1 >= 0f && d3 <= 0f)
        {
            float v = d1 / (d1 - d3);
            return a + (ab * v);
        }

        var cp = point - c;
        float d5 = Vector3.Dot(ab, cp);
        float d6 = Vector3.Dot(ac, cp);
        if (d6 >= 0f && d5 <= d6)
        {
            return c;
        }

        float vb = (d5 * d2) - (d1 * d6);
        if (vb <= 0f && d2 >= 0f && d6 <= 0f)
        {
            float w = d2 / (d2 - d6);
            return a + (ac * w);
        }

        float va = (d3 * d6) - (d5 * d4);
        if (va <= 0f && (d4 - d3) >= 0f && (d5 - d6) >= 0f)
        {
            float w = (d4 - d3) / ((d4 - d3) + (d5 - d6));
            return b + ((c - b) * w);
        }

        float denom = 1f / (va + vb + vc);
        return a + (ab * (vb * denom)) + (ac * (vc * denom));
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

    /// <summary>
    ///     A face centroid, quantised: <see cref="DuplicateBucketSize"/> across the ground and
    ///     <see cref="DuplicateHeight"/> up, so that faces sharing a key are faces at the same place
    ///     and at the same height, which is what a copy of a surface looks like.
    /// </summary>
    private readonly record struct DuplicateKey(int X, int Y, int Z)
    {
        public static DuplicateKey Of(Vector3 centroid) => new(
            (int)MathF.Round(centroid.X / DuplicateBucketSize),
            (int)MathF.Round(centroid.Y / DuplicateBucketSize),
            (int)MathF.Floor(centroid.Z / DuplicateHeight));
    }

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
