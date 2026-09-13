using BepuPhysics;
using BepuUtilities;
using BepuUtilities.Memory;
using Serilog;
using Shared.Collision.Cache;
using Shared.Collision.Chunk;
using Shared.Collision.Layers.Collision;
using Shared.Collision.Navigation;
using Shared.Collision.Tagfile;

namespace Shared.Collision.ZoneLoading;

public static class ChunkProcessor
{
    private static readonly ILogger _logger = Log.ForContext(typeof(ChunkProcessor));

    public static StaticDescription[] ProcessChunk(
        string chunkPath,
        string cachePath,
        Simulation simulation,
        BufferPool pool,
        ThreadDispatcher dispatcher,
        bool forceReload = false,
        Action<IReadOnlyList<NavigationTriangle>>? navigationSink = null)
    {
        var chunkName = Path.GetFileNameWithoutExtension(chunkPath);
        var cacheFile = ChunkCache.GetCachePath(cachePath, chunkName);
        var navigationCacheFile = NavigationCache.GetCachePath(cachePath, chunkName);

        if (!forceReload && ChunkCache.TryLoad(simulation, pool, dispatcher, cacheFile, out var cached))
        {
            if (navigationSink == null)
            {
                return cached;
            }

            if (NavigationCache.TryLoad(navigationCacheFile, out var cachedNavigation))
            {
                navigationSink(cachedNavigation);
                return cached;
            }
        }

        var chunk = ChunkFileReader.Read(chunkPath);

        var lod3Layers = FindAllLod3CollisionLayers(chunk);

        if (lod3Layers.Length == 0)
        {
            _logger.Warning("Chunk {Name} has no LOD3 collision layers, what?", chunk.Name);
            return [];
        }

        var loader = new TagfileLoader(simulation, pool, dispatcher);

        List<StaticDescription> allStatics = [];
        List<NavigationTriangle> navigationTriangles = [];

        void CaptureNavigation(IReadOnlyList<NavigationTriangle> triangles)
        {
            navigationTriangles.AddRange(triangles);
            navigationSink?.Invoke(triangles);
        }

        Action<IReadOnlyList<NavigationTriangle>>? captureNavigation =
            navigationSink == null ? null : CaptureNavigation;

        foreach (var collisionLayer in lod3Layers)
        {
            var hkxBytes = collisionLayer.Enwf.HavokBinaryTagfile;

            if (hkxBytes.Length == 0)
            {
                continue;
            }

            var vertBlocks = EnwfToBepuConverter.ConvertVertBlocks(collisionLayer.Enwf.VertBlocks);
            var indiceBlocks = EnwfToBepuConverter.ConvertIndiceBlocks(collisionLayer.Enwf.IndiceBlocks);
            var statics = loader.ProcessTagfileBytes(
                hkxBytes,
                vertBlocks,
                indiceBlocks,
                collisionLayer.Enwf.PhysicsMatIds,
                captureNavigation);

            if (statics.Length > 0)
            {
                allStatics.AddRange(statics);
            }
        }

        var result = allStatics.ToArray();

        ChunkCache.Save(simulation, pool, result, cacheFile);
        if (navigationSink != null)
        {
            NavigationCache.Save(navigationTriangles, navigationCacheFile);
        }

        return result;
    }

    private static ChunkStaticGeometryCollisionLayer[] FindAllLod3CollisionLayers(ChunkFile chunk)
    {
        List<ChunkStaticGeometryCollisionLayer> result = [];

        foreach (var lod in chunk.Lod)
        {
            if (lod.Level != 3)
            {
                continue;
            }

            foreach (var layer in lod.SharedLayers)
            {
                if (layer is ChunkStaticGeometryCollisionLayer collision)
                {
                    result.Add(collision);
                }
            }

            foreach (var subChunk in lod.SubChunks)
            {
                foreach (var layer in subChunk.Layers)
                {
                    if (layer is ChunkStaticGeometryCollisionLayer collision)
                    {
                        result.Add(collision);
                    }
                }
            }
        }

        return [.. result];
    }
}
