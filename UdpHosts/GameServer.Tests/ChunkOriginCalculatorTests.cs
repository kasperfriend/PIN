using System;
using System.IO;
using System.Numerics;
using Shared.Collision.Layers;
using Shared.Collision.ZoneLoading;
using Xunit;

namespace GameServer.Tests;

/// <summary>
///     Zone files list the same tiles as both <c>ZoneChunkRefLayer</c> (0x10101, with a
///     ChunkRecord id) and <c>ZoneChunkRef2Layer</c> (0x10100, without). Loading both copies
///     stacked collision on trees and under rocks; these tests pin the dedup that stops that.
/// </summary>
public class ChunkOriginCalculatorTests
{
    [Fact]
    public void ExtractChunks_DropsTheUnidentifiedCopyWhenBothRefLayersListTheSameTile()
    {
        var root = RootWith(
            Range(cubeFaceId: 0, minX: 100, maxX: 110, minY: 200, maxY: 210),
            ChunkRef(x: 105, y: 205, chunkRecordId: 77),
            ChunkRef2(x: 105, y: 205));

        var chunks = ChunkOriginCalculator.ExtractChunks(root, zoneId: 1);

        Assert.Single(chunks);
        Assert.Equal("0_0105_0205", chunks[0].Name);
        Assert.Equal(77u, chunks[0].ChunkRecordId);
        Assert.Equal(105u, chunks[0].X);
        Assert.Equal(205u, chunks[0].Y);
    }

    [Fact]
    public void ExtractChunks_KeepsARef2TileThatHasNoIdentifiedTwin()
    {
        var root = RootWith(
            Range(cubeFaceId: 0, minX: 100, maxX: 110, minY: 200, maxY: 210),
            ChunkRef(x: 105, y: 205, chunkRecordId: 77),
            ChunkRef2(x: 106, y: 205));

        var chunks = ChunkOriginCalculator.ExtractChunks(root, zoneId: 1);

        Assert.Equal(2, chunks.Length);
        Assert.Contains(chunks, chunk => chunk.Name == "0_0105_0205" && chunk.ChunkRecordId == 77u);
        Assert.Contains(chunks, chunk => chunk.Name == "0_0106_0205" && chunk.ChunkRecordId == 0u);
    }

    [Fact]
    public void ExtractChunks_PrefersTheCopyThatCarriesAChunkRecordId()
    {
        var root = new ZoneRootLayer();
        root.Children.Add(ChunkInfo(
            Range(cubeFaceId: 0, minX: 100, maxX: 110, minY: 200, maxY: 210),
            ChunkRef2(x: 105, y: 205)));
        root.Children.Add(ChunkInfo(
            Range(cubeFaceId: 0, minX: 100, maxX: 110, minY: 200, maxY: 210),
            ChunkRef(x: 105, y: 205, chunkRecordId: 88)));

        var chunks = ChunkOriginCalculator.ExtractChunks(root, zoneId: 1);

        Assert.Single(chunks);
        Assert.Equal(88u, chunks[0].ChunkRecordId);
    }

    [Fact]
    public void ExtractChunks_IdenticalIdentifiedRefsAreNotLoadedTwice()
    {
        var root = RootWith(
            Range(cubeFaceId: 0, minX: 100, maxX: 110, minY: 200, maxY: 210),
            ChunkRef(x: 105, y: 205, chunkRecordId: 77),
            ChunkRef(x: 105, y: 205, chunkRecordId: 77));

        var chunks = ChunkOriginCalculator.ExtractChunks(root, zoneId: 1);

        Assert.Single(chunks);
        Assert.Equal(77u, chunks[0].ChunkRecordId);
    }

    [Fact]
    public void DistinctTilesKeepDistinctOrigins()
    {
        var root = RootWith(
            Range(cubeFaceId: 0, minX: 100, maxX: 110, minY: 200, maxY: 210),
            ChunkRef(x: 105, y: 205, chunkRecordId: 1),
            ChunkRef(x: 106, y: 205, chunkRecordId: 2));

        var chunks = ChunkOriginCalculator.ExtractChunks(root, zoneId: 1);

        Assert.Equal(2, chunks.Length);
        Assert.Equal(ChunkOriginCalculator.ChunkSize, MathF.Abs(chunks[0].Origin.X - chunks[1].Origin.X));
        Assert.Equal(0f, chunks[0].Origin.Y - chunks[1].Origin.Y);
    }

    [Theory]
    [InlineData(0f, 0f, true)]
    [InlineData(511.9f, 511.9f, true)]
    [InlineData(512f, 10f, false)]
    [InlineData(-0.1f, 10f, false)]
    [InlineData(10f, 512f, false)]
    [InlineData(10f, -0.1f, false)]
    [InlineData(256f, 256f, true)]
    public void IsInsideLocalBounds_PartitionsTheChunkTileWithoutAGapOrOverlap(float x, float y, bool expected)
    {
        Assert.Equal(expected, ChunkOriginCalculator.IsInsideLocalBounds(new Vector3(x, y, 12f)));
    }

    private static ZoneRootLayer RootWith(params WorldLayer[] children)
    {
        var info = ChunkInfo(children);
        var root = new ZoneRootLayer();
        root.Children.Add(info);
        return root;
    }

    private static ZoneChunkInfoLayer ChunkInfo(params WorldLayer[] children)
    {
        var info = new ZoneChunkInfoLayer();
        foreach (var child in children)
        {
            info.Children.Add(child);
        }

        return info;
    }

    private static ZoneChunkRangeLayer Range(uint cubeFaceId, uint minX, uint maxX, uint minY, uint maxY)
    {
        var layer = new ZoneChunkRangeLayer();
        using var stream = new MemoryStream();
        using (var writer = new BinaryWriter(stream, System.Text.Encoding.UTF8, leaveOpen: true))
        {
            writer.Write(cubeFaceId);
            writer.Write(minX);
            writer.Write(maxX);
            writer.Write(minY);
            writer.Write(maxY);
        }

        stream.Position = 0;
        using var reader = new BinaryReader(stream);
        layer.ParseData(reader, 20, new WorldParseContext(0x20400));
        return layer;
    }

    private static ZoneChunkRefLayer ChunkRef(uint x, uint y, uint chunkRecordId)
    {
        var layer = new ZoneChunkRefLayer();
        using var stream = new MemoryStream();
        using (var writer = new BinaryWriter(stream, System.Text.Encoding.UTF8, leaveOpen: true))
        {
            writer.Write(x);
            writer.Write(y);
            writer.Write(chunkRecordId);
        }

        stream.Position = 0;
        using var reader = new BinaryReader(stream);
        layer.ParseData(reader, 12, new WorldParseContext(0x20400));
        return layer;
    }

    private static ZoneChunkRef2Layer ChunkRef2(uint x, uint y)
    {
        var layer = new ZoneChunkRef2Layer();
        using var stream = new MemoryStream();
        using (var writer = new BinaryWriter(stream, System.Text.Encoding.UTF8, leaveOpen: true))
        {
            writer.Write(x);
            writer.Write(y);
        }

        stream.Position = 0;
        using var reader = new BinaryReader(stream);
        layer.ParseData(reader, 8, new WorldParseContext(0x20400));
        return layer;
    }
}
