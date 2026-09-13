using System.Diagnostics;
using System.Numerics;
using BepuPhysics;
using BepuUtilities;
using BepuUtilities.Memory;
using Serilog;
using Shared.Collision.Layers;
using Shared.Collision.Navigation;
using Shared.Collision.Zone;

namespace Shared.Collision.ZoneLoading;

public class ZoneLoader
{
    private static readonly ILogger _logger = Log.Logger.ForContext<ZoneLoader>();

    private readonly Simulation _simulation;
    private readonly BufferPool _pool;
    private readonly ThreadDispatcher _dispatcher;
    private readonly string _mapsPath;
    private readonly string _cachePath;
    private readonly Func<uint, ulong?>? _chunkPathingFlags;
    private readonly List<ZoneNavigationRegion> _excludedRegions = [];
    private readonly List<NavigationTriangle> _navigationTriangles = [];

    public ZoneLoader(
        Simulation simulation,
        BufferPool pool,
        ThreadDispatcher dispatcher,
        string mapsPath,
        string cachePath,
        Func<uint, ulong?>? chunkPathingFlags = null)
    {
        _simulation = simulation;
        _pool = pool;
        _dispatcher = dispatcher;
        _mapsPath = mapsPath;
        _cachePath = cachePath;
        _chunkPathingFlags = chunkPathingFlags;
    }

    /// <summary>Whether the loaded zone supplied at least one excluded pathing chunk.</summary>
    public bool HasNavigationExclusions => _excludedRegions.Count > 0;

    /// <summary>Collision surfaces retained for building the zone navigation mesh.</summary>
    public IReadOnlyList<NavigationTriangle> NavigationTriangles => _navigationTriangles;

    /// <summary>Returns true when the original zone metadata excludes this point from AI pathing.</summary>
    public bool IsNavigationExcluded(Vector3 point)
    {
        foreach (var region in _excludedRegions)
        {
            if (region.Contains(point))
            {
                return true;
            }
        }

        return false;
    }

    public long? LoadZone(uint zoneId, bool forceReload = false)
    {
        _excludedRegions.Clear();
        _navigationTriangles.Clear();
        var stopwatch = Stopwatch.StartNew();

        var zoneFilePath = Path.Combine(_mapsPath, $"{zoneId}.zone");

        if (!File.Exists(zoneFilePath))
        {
            _logger.Error("Zone file not found: {Path}", zoneFilePath);
            return null;
        }

        var zone = ZoneFileReader.Read(zoneFilePath);

        if (zone.Root is not ZoneRootLayer rootLayer)
        {
            _logger.Error("Invalid zone root layer for zone {ZoneId}", zoneId);
            return null;
        }

        var chunkRefs = ChunkOriginCalculator.ExtractChunks(rootLayer, zoneId);
        if (_chunkPathingFlags != null)
        {
            foreach (var chunkRef in chunkRefs)
            {
                if (chunkRef.ChunkRecordId == 0 || _chunkPathingFlags(chunkRef.ChunkRecordId) is not ulong flags || flags == 0)
                {
                    continue;
                }

                var min = chunkRef.Origin;
                var max = min + new Vector3(ChunkOriginCalculator.ChunkSize, ChunkOriginCalculator.ChunkSize, 0f);
                _excludedRegions.Add(new ZoneNavigationRegion(min, max, flags));
            }
        }

        _logger.Information($"Zone {{ZoneId}} ({{ZoneName}}): References {{Count}} {(chunkRefs.Length == 1 ? "chunk" : "chunks")}", zoneId, zone.Name, chunkRefs.Length);

        foreach (var chunkRef in chunkRefs)
        {
            _logger.Information("Loading chunk ({CurrentCount}/{TotalCount}) {ChunkName}", chunkRefs.IndexOf(chunkRef) + 1, chunkRefs.Length, chunkRef.Name);
            var chunkPath = Path.Combine(_mapsPath, "chunks", $"{chunkRef.Name}.gtchunk");

            var statics = ChunkProcessor.ProcessChunk(
                chunkPath,
                _cachePath,
                _simulation,
                _pool,
                _dispatcher,
                forceReload,
                triangles =>
                {
                    foreach (var triangle in triangles)
                    {
                        _navigationTriangles.Add(new NavigationTriangle(
                            triangle.A + chunkRef.Origin,
                            triangle.B + chunkRef.Origin,
                            triangle.C + chunkRef.Origin,
                            triangle.PhysicsMaterialId));
                    }
                });

            if (statics.Length == 0)
            {
                continue;
            }

            foreach (var staticsItem in statics)
            {
                var adjusted = staticsItem;
                adjusted.Pose.Position += chunkRef.Origin;
                _simulation.Statics.Add(adjusted);
            }
        }

        stopwatch.Stop();
        _logger.Information("Zone {ZoneId}: Loaded successfully in {Duration}. Total statics: {Count}", zoneId, stopwatch.Elapsed, _simulation.Statics.Count);

        return zone.Timestamp;
    }
}
