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
    private readonly List<ZoneChunkRef> _chunkRefs = [];
    private Vector3? _zoneBoundsMin;
    private Vector3? _zoneBoundsMax;
    private readonly List<ZonePathLayer> _zonePaths = [];
    private readonly List<MeldingPerimeterLayer> _meldingPerimeters = [];
    private int _subZoneRegionCount;
    private int _encounterNameCount;

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

    /// <summary>
    ///     The chunks the loaded zone references, each with its world origin (the chunk's minimum
    ///     corner) and its <c>dbzonemetadata::ChunkRecord</c> id. Empty when no zone was loaded.
    /// </summary>
    public IReadOnlyList<ZoneChunkRef> ChunkRefs => _chunkRefs;

    /// <summary>Zone bounds from ZoneBoundsLayer (0x21000) if present.</summary>
    public Vector3? ZoneBoundsMin => _zoneBoundsMin;
    public Vector3? ZoneBoundsMax => _zoneBoundsMax;

    /// <summary>Authored path layers (0x20800) - vehicle/dropship routes, NOT NPC patrols (see MAP_FILES_FINDINGS.md).</summary>
    public IReadOnlyList<ZonePathLayer> ZonePaths => _zonePaths;

    /// <summary>Melding perimeter layers (type 5) from the zone file.</summary>
    public IReadOnlyList<MeldingPerimeterLayer> MeldingPerimeters => _meldingPerimeters;

    public int SubZoneRegionCount => _subZoneRegionCount;
    public int EncounterNameCount => _encounterNameCount;

    /// <summary>Whether position is inside zone bounds, or true when no bounds are known.</summary>
    public bool IsInsideZoneBounds(Vector3 pos)
    {
        if (_zoneBoundsMin == null || _zoneBoundsMax == null) return true;
        var min = _zoneBoundsMin.Value;
        var max = _zoneBoundsMax.Value;
        return pos.X >= min.X && pos.X <= max.X && pos.Y >= min.Y && pos.Y <= max.Y && pos.Z >= min.Z && pos.Z <= max.Z;
    }

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
        _chunkRefs.Clear();
        _zoneBoundsMin = null;
        _zoneBoundsMax = null;
        _zonePaths.Clear();
        _meldingPerimeters.Clear();
        _subZoneRegionCount = 0;
        _encounterNameCount = 0;
        var stopwatch = Stopwatch.StartNew();

        var zoneFilePath = Path.Combine(_mapsPath, $"{zoneId}.zone");

        if (!File.Exists(zoneFilePath))
        {
            _logger.Error("Zone file not found for zone {ZoneId}: {Path} (maps path: {MapsPath})", zoneId, zoneFilePath, _mapsPath);
            return null;
        }

        var zone = ZoneFileReader.Read(zoneFilePath);

        if (zone.Root is not ZoneRootLayer rootLayer)
        {
            _logger.Error("Invalid zone root layer for zone {ZoneId}", zoneId);
            return null;
        }

        // Extract zone metadata from root children - based on MAP_FILES_FINDINGS.md
        // ZoneBoundsLayer (0x21000) gives AABB, ZonePathLayer (0x20800) are vehicle routes (not NPC patrols),
        // MeldingPerimeterLayer (type 5) gives Melding wall, SubZoneRegion (0x21700) and EncounterName (0x21200) for stats
        foreach (var child in rootLayer.Children)
        {
            if (child is ZoneBoundsLayer bounds)
            {
                _zoneBoundsMin = new Vector3(bounds.Min.X, bounds.Min.Y, bounds.Min.Z);
                _zoneBoundsMax = new Vector3(bounds.Max.X, bounds.Max.Y, bounds.Max.Z);
            }
            else if (child is ZonePathLayer path)
            {
                _zonePaths.Add(path);
            }
            else if (child is ZoneMeldingLayer meldingLayer)
            {
                foreach (var sub in meldingLayer.Children)
                {
                    if (sub is MeldingPerimeterLayer perim)
                    {
                        _meldingPerimeters.Add(perim);
                    }
                }
            }
            else if (child is ZoneSubZoneRegionLayer)
            {
                _subZoneRegionCount++;
            }
            else if (child is ZonePropEncounterNameRegistryLayer enc)
            {
                _encounterNameCount += enc.Names.Length;
            }
        }

        if (_zoneBoundsMin.HasValue && _zoneBoundsMax.HasValue)
        {
            _logger.Information(\"Zone {ZoneId}: Bounds Min {Min} Max {Max} Paths {PathCount} MeldingPerims {MeldingCount} SubZoneRegions {SubZoneCount} EncounterNames {EncCount}\",
                zoneId, _zoneBoundsMin.Value, _zoneBoundsMax.Value, _zonePaths.Count, _meldingPerimeters.Count, _subZoneRegionCount, _encounterNameCount);
        }
        else
        {
            _logger.Information(\"Zone {ZoneId}: No bounds layer, Paths {PathCount} MeldingPerims {MeldingCount} SubZoneRegions {SubZoneCount} EncounterNames {EncCount}\",
                zoneId, _zonePaths.Count, _meldingPerimeters.Count, _subZoneRegionCount, _encounterNameCount);
        }

        var chunkRefs = ChunkOriginCalculator.ExtractChunks(rootLayer, zoneId);
        _chunkRefs.AddRange(chunkRefs);
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
