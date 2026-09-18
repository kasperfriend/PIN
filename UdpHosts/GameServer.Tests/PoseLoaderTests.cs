using System;
using System.IO;
using System.Numerics;
using GameServer.Physics.PoseLoader;
using GameServer.Tests.Fakes;
using Xunit;

namespace GameServer.Tests;

/// <summary>
///     The pose loader is asked for a shape from the hot path - <c>PhysicsEngine.GetAssetShape</c>
///     runs on every movement update of every entity - so what it does with an asset it cannot load
///     is worth as much as what it does with one it can: a file read, a parse and a logged exception
///     per tick is what turns one bad database row, or one glider pose, into a shard that cannot
///     answer its clients.
/// </summary>
public class PoseLoaderTests : IDisposable
{
    private readonly string _root;

    public PoseLoaderTests()
    {
        _root = Path.Combine(Path.GetTempPath(), "pin-pose-loader-tests", Guid.NewGuid().ToString("N"));
        _ = Directory.CreateDirectory(Path.Combine(_root, "00189000"));
    }

    public void Dispose()
    {
        try
        {
            Directory.Delete(_root, recursive: true);
        }
        catch (IOException)
        {
            // A leftover temp directory is not worth failing a test over.
        }
    }

    private void WritePose(string assetId, string body)
    {
        string path = Path.Combine(_root, AssetPathResolver.ComputeFolderName(assetId), $"{assetId}.pose");
        File.WriteAllText(path, body);
    }

    [Fact]
    public void TryLoad_ReadsAPoseWhoseNumberCarriesATrailingUnitLetter()
    {
        // The archive ships this: pose 00189610 has <2.1t 0 0> as a shape origin, and the strict
        // parser threw on it, so every body using the file fell back to a generic shape.
        WritePose("00189610", """
            [File]
            Version=1
            Type=Pose
            Name=Giant Beetle

            [Shape-body]
            Type=Sphere
            Radius=1.4
            Origin=<2.1t 0 0>
            """);

        var loader = new GameServer.Physics.PoseLoader.PoseLoader(_root, new CapturingLogger().Logger);

        Assert.True(loader.TryLoad("00189610", out var pose));
        Assert.Equal("Giant Beetle", pose.Name);
        Assert.Equal(2.1f, pose.Shapes["body"].Origin.X, 3);
        Assert.Equal(1, loader.ReadCount);
    }

    [Fact]
    public void TryLoad_RemembersAnAssetThatDoesNotParse()
    {
        // A file that is there but is one field short: the first ask reports it, and every ask
        // after that - which arrives on every movement tick of every entity using the pose - is
        // answered from memory without touching the disk or the log again.
        WritePose("00189611", """
            [File]
            Version=1
            Type=Pose
            Name=Broken

            [Shape-body]
            Type=Sphere
            """);

        var logger = new CapturingLogger();
        var loader = new GameServer.Physics.PoseLoader.PoseLoader(_root, logger.Logger);

        Assert.False(loader.TryLoad("00189611", out _));
        Assert.Equal(1, loader.ReadCount);
        Assert.Equal(1, logger.CountContaining("failed to load"));

        Assert.False(loader.TryLoad("00189611", out _));
        Assert.False(loader.TryLoad("00189611", out _));

        // Still one read and one report: the failure is the asset's, not the caller's.
        Assert.Equal(1, loader.ReadCount);
        Assert.Equal(1, logger.CountContaining("failed to load"));
        Assert.Equal(1, loader.FailedAssetCount);
    }

    [Fact]
    public void TryLoad_RemembersAnAssetThatIsNotThereAtAll()
    {
        var loader = new GameServer.Physics.PoseLoader.PoseLoader(_root, new CapturingLogger().Logger);

        Assert.False(loader.TryLoad("00299999", out _));
        Assert.False(loader.TryLoad("00299999", out _));

        Assert.Equal(0, loader.ReadCount);
        Assert.Equal(1, loader.FailedAssetCount);
    }

    [Fact]
    public void TryLoad_RemembersAnIdThatIsNotAnAssetName()
    {
        var logger = new CapturingLogger();
        var loader = new GameServer.Physics.PoseLoader.PoseLoader(_root, logger.Logger);

        Assert.False(loader.TryLoad("not-an-id", out _));
        Assert.False(loader.TryLoad("not-an-id", out _));

        // A malformed id costs exactly one warning, not one per tick.
        Assert.Equal(1, logger.CountContaining("Invalid asset ID format"));
    }

    [Fact]
    public void TryLoad_KeepsAPoseItHasLoaded()
    {
        WritePose("00189612", """
            [File]
            Version=1
            Type=Pose
            Name=Cached

            [Shape-body]
            Type=Sphere
            Radius=0.5
            """);

        var loader = new GameServer.Physics.PoseLoader.PoseLoader(_root, new CapturingLogger().Logger);

        Assert.True(loader.TryLoad("00189612", out var first));
        Assert.True(loader.TryLoad("00189612", out var second));

        Assert.Same(first, second);
        Assert.Equal(1, loader.ReadCount);
        Assert.Equal(new Vector3(0f, 0f, 0f), second.Shapes["body"].Origin);
    }
}
