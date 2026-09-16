using System.Numerics;
using Shared.Collision.Layers;

namespace Shared.Collision.ZoneLoading;

public static class ChunkOriginCalculator
{
    public const float ChunkSize = 512f;
    private const uint _coralForestZoneId = 448;
    private const uint _sertaoZoneId = 1030;

    /// <summary>
    ///     Whether a point in a chunk's local space belongs to that chunk's 512 m tile rather than
    ///     a neighbour's overlapping skirt. Centroids on the exclusive upper bound are owned by the
    ///     next tile, so adjacent chunks partition the plane without a gap or an overlap.
    /// </summary>
    public static bool IsInsideLocalBounds(Vector3 localPosition) =>
        localPosition.X >= 0f && localPosition.X < ChunkSize &&
        localPosition.Y >= 0f && localPosition.Y < ChunkSize;

    public static ZoneChunkRef[] ExtractChunks(ZoneRootLayer zoneRoot, uint zoneId)
    {
        var chunkRefs = new List<ZoneChunkRef>();
        var indexByName = new Dictionary<string, int>(StringComparer.Ordinal);

        // ZoneChunkRefLayer (0x10101) carries the ChunkRecord id the rest of the server keys off;
        // ZoneChunkRef2Layer (0x10100) is the same (X, Y) grid without that id. Open-world zone
        // files list both, so consuming them naively loads each chunk twice: overlapping collision,
        // overlapping walkable faces, and NPCs that snap onto the extra copy (tree canopies, the
        // underside of rocks). Prefer the identified refs, then fill any holes from the older list.
        foreach (var child in zoneRoot.Children)
        {
            if (!TryGetChunkInfo(child, zoneId, out var range, out var centerIndexX, out var centerIndexY))
            {
                continue;
            }

            foreach (var refLayer in range.Info.Children.OfType<ZoneChunkRefLayer>())
            {
                Consider(
                    chunkRefs,
                    indexByName,
                    MakeRef(range.Range, centerIndexX, centerIndexY, refLayer.X, refLayer.Y, refLayer.ChunkRecordId));
            }
        }

        foreach (var child in zoneRoot.Children)
        {
            if (!TryGetChunkInfo(child, zoneId, out var range, out var centerIndexX, out var centerIndexY))
            {
                continue;
            }

            foreach (var ref2Layer in range.Info.Children.OfType<ZoneChunkRef2Layer>())
            {
                Consider(
                    chunkRefs,
                    indexByName,
                    MakeRef(range.Range, centerIndexX, centerIndexY, ref2Layer.X, ref2Layer.Y, chunkRecordId: 0));
            }
        }

        return [.. chunkRefs];
    }

    private static bool TryGetChunkInfo(
        WorldLayer child,
        uint zoneId,
        out (ZoneChunkInfoLayer Info, ZoneChunkRangeLayer Range) range,
        out double centerIndexX,
        out double centerIndexY)
    {
        range = default;
        centerIndexX = 0;
        centerIndexY = 0;

        if (child is not ZoneChunkInfoLayer chunkInfo)
        {
            return false;
        }

        var rangeLayer = chunkInfo.Children.OfType<ZoneChunkRangeLayer>().FirstOrDefault();
        if (rangeLayer == null)
        {
            return false;
        }

        long minCoordX = rangeLayer.MinX;
        long maxCoordX = rangeLayer.MaxX;
        long minCoordY = rangeLayer.MinY;
        long maxCoordY = rangeLayer.MaxY;

        centerIndexX = (maxCoordX - minCoordX) / 2.0;
        centerIndexY = (maxCoordY - minCoordY) / 2.0;

        if (zoneId == _coralForestZoneId)
        {
            centerIndexX = 4;
            centerIndexY = 3.5;
        }
        else if (zoneId == _sertaoZoneId)
        {
            centerIndexX = 9.5;
            centerIndexY = 3;
        }

        range = (chunkInfo, rangeLayer);
        return true;
    }

    private static ZoneChunkRef MakeRef(
        ZoneChunkRangeLayer rangeLayer,
        double centerIndexX,
        double centerIndexY,
        uint x,
        uint y,
        uint chunkRecordId)
    {
        return new ZoneChunkRef
        {
            Name = $"{rangeLayer.CubeFaceId}_{x:D4}_{y:D4}",
            Origin = CalculateOrigin(rangeLayer.MaxX, rangeLayer.MaxY, centerIndexX, centerIndexY, x, y),
            X = x,
            Y = y,
            ChunkRecordId = chunkRecordId,
        };
    }

    private static void Consider(
        List<ZoneChunkRef> chunkRefs,
        Dictionary<string, int> indexByName,
        ZoneChunkRef chunkRef)
    {
        if (indexByName.TryGetValue(chunkRef.Name, out int index))
        {
            // Same tile already recorded. Keep the copy that knows its ChunkRecord id.
            if (chunkRefs[index].ChunkRecordId == 0 && chunkRef.ChunkRecordId != 0)
            {
                chunkRefs[index] = chunkRef;
            }

            return;
        }

        indexByName[chunkRef.Name] = chunkRefs.Count;
        chunkRefs.Add(chunkRef);
    }

    private static Vector3 CalculateOrigin(long maxCoordX, long maxCoordY, double centerIndexX, double centerIndexY, long x, long y)
    {
        double coordIndexX = maxCoordX - x;
        double coordIndexY = maxCoordY - y;

        double coordMultiX = centerIndexX - coordIndexX;
        double coordMultiY = centerIndexY - coordIndexY;

        int originX = (int)(coordMultiX * ChunkSize);
        int originY = (int)(coordMultiY * ChunkSize);

        return new Vector3(originX, originY, 0);
    }
}
