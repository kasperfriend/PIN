using System;
using System.IO;
using System.Linq;
using Microsoft.Extensions.FileProviders;
using Shared.Web.Assets;
using Xunit;

namespace GameServer.Tests;

/// <summary>
///     The rules the web asset host serves the client's high-resolution texture chunks by: what a chunk
///     request looks like, which of the configured roots answers it, and what the host says when none does.
///     These are the pieces a blurry-texture report turns on, and none of them needs a running server.
///     The scratch folders are created per test and deleted by <see cref="Dispose"/>, which xunit requires of
///     a disposable fixture whether or not a test asserted on it.
/// </summary>
public class WebAssetTests : IDisposable
{
    private readonly string _root;

    public WebAssetTests()
    {
        _root = Directory.CreateTempSubdirectory("pin-assets-").FullName;
    }

    public void Dispose()
    {
        try
        {
            Directory.Delete(_root, true);
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
        {
            // A test scratch folder that outlives the test is a temp folder, not a failure.
        }
    }

    // --- the shape of a chunk request -------------------------------------------------------------------

    [Theory]
    [InlineData("vtex/prod-1962/static.vtex0", true)]
    [InlineData("vtex/prod-1962/static.vtex1", true)]
    [InlineData("vtex/beta-1858/static.vtex2", true)]
    [InlineData(@"vtex\prod-1962\static.vtex2", true)]
    [InlineData("VTEX/PROD-1962/static.vtex0", true)]
    [InlineData("vtex/prod-1962/static.vtex", false)]
    [InlineData("AssetStream/prod-1962/textures.vtex0", false)]
    [InlineData("static.vtex0", false)]
    [InlineData("", false)]
    public void IsChunkPath_OnlyForAChunkUnderABuildFolder(string path, bool expected)
    {
        Assert.Equal(expected, VirtualTextureChunks.IsChunkPath(path));
    }

    [Fact]
    public void FindBuildFolder_IsTheDirectoryUnderVtex()
    {
        Assert.Equal("prod-1962", VirtualTextureChunks.FindBuildFolder("vtex/prod-1962/static.vtex0"));
        Assert.Null(VirtualTextureChunks.FindBuildFolder("vtex/static.vtex0"));
        Assert.Null(VirtualTextureChunks.FindBuildFolder("AssetStream/prod-1962/"));
    }

    [Fact]
    public void GetChunkName_StripsTheFolderTheClientAskedUnder()
    {
        Assert.Equal("static.vtex1", VirtualTextureChunks.GetChunkName("vtex/prod-1962/static.vtex1"));
    }

    [Fact]
    public void Extensions_AreTheChunksTheClientProbesForInOrder()
    {
        Assert.Equal(new[] { ".vtex0", ".vtex1", ".vtex2" }, VirtualTextureChunks.Extensions);
    }

    [Fact]
    public void Candidates_AnswerAChunkAnywhereInTheRootBeforeAnywhereElse()
    {
        var candidates = VirtualTextureChunks.Candidates("vtex/prod-1962/static.vtex0");

        Assert.Equal(
            new[] { "vtex/prod-1962/static.vtex0", "static.vtex0", "vtex/static.vtex0" },
            candidates);
    }

    [Fact]
    public void Candidates_AreTheLiteralPathForAnythingThatIsNotAChunk()
    {
        Assert.Equal(
            new[] { "AssetStream/prod-1962/icon.png" },
            VirtualTextureChunks.Candidates("AssetStream/prod-1962/icon.png"));
    }

    [Fact]
    public void ChunkNames_AreTheThreeFilesASetConsistsOf()
    {
        Assert.Equal(new[] { "static.vtex0", "static.vtex1", "static.vtex2" }, VirtualTextureChunks.ChunkNames());
    }

    // --- which root answers a request -------------------------------------------------------------------

    [Fact]
    public void GetFileInfo_FirstRootWithTheFileWins()
    {
        var first = Write("Assets", "vtex/prod-1962/static.vtex0", "first");
        Write("Other", "vtex/prod-1962/static.vtex0", "second");

        var info = Provider(first, Path.Combine(_root, "Other")).GetFileInfo("vtex/prod-1962/static.vtex0");

        Assert.True(info.Exists);
        using var stream = info.CreateReadStream();
        Assert.Equal("first", new StreamReader(stream).ReadToEnd());
    }

    [Fact]
    public void GetFileInfo_FallsBackToTheNextRoot()
    {
        var empty = Prepare("Empty");
        var full = Write("Full", "vtex/prod-1962/static.vtex2", "chunk");

        var info = Provider(empty, full).GetFileInfo("vtex/prod-1962/static.vtex2");

        Assert.True(info.Exists);
    }

    [Fact]
    public void GetFileInfo_FindsALooseChunkByNameWhenTheBuildFolderMisses()
    {
        // The client's system\vt folder keeps the chunks at its top level, which is where a pointer in
        // Firefall:Assets:Paths lands them - no vtex/<build>/ tree to match the client's request against.
        var root = Prepare("Install");
        File.WriteAllText(Path.Combine(root, "static.vtex0"), "loose chunk");

        var info = Provider(root).GetFileInfo("vtex/prod-1962/static.vtex0");

        Assert.True(info.Exists);
        Assert.Equal(Path.Combine(root, "static.vtex0"), info.PhysicalPath);
    }

    [Fact]
    public void GetFileInfo_FindsAChunkDroppedInsideTheVtexFolder()
    {
        // "put them in the vtex folder" read literally, without the build directory: still the right file.
        var root = Prepare("Install");
        WriteAt(root, "vtex/static.vtex1", "loose in vtex");

        var info = Provider(root).GetFileInfo("vtex/prod-1962/static.vtex1");

        Assert.True(info.Exists);
        Assert.Equal(Path.Combine(root, "vtex", "static.vtex1"), info.PhysicalPath);
    }

    [Fact]
    public void GetFileInfo_PrefersTheFileAtTheBuildPathOverALooseCopy()
    {
        var root = Prepare("Install");
        File.WriteAllText(Path.Combine(root, "static.vtex0"), "stale leftover");
        WriteAt(root, "vtex/prod-1962/static.vtex0", "the real answer");

        var info = Provider(root).GetFileInfo("vtex/prod-1962/static.vtex0");

        Assert.True(info.Exists);
        Assert.Equal(Path.Combine(root, "vtex", "prod-1962", "static.vtex0"), info.PhysicalPath);
    }

    [Fact]
    public void GetFileInfo_LooseFallbackIsOnlyForChunks()
    {
        var root = Prepare("Install");
        File.WriteAllText(Path.Combine(root, "misc.dat"), "not a chunk");

        Assert.False(Provider(root).GetFileInfo("vtex/prod-1962/misc.dat").Exists);
    }

    [Fact]
    public void GetFileInfo_MissingChunkStaysMissing()
    {
        var root = Prepare("Install");

        var info = Provider(root).GetFileInfo("vtex/prod-1962/static.vtex0");

        Assert.False(info.Exists);
        Assert.Equal("static.vtex0", System.IO.Path.GetFileName(info.Name));
    }

    [Fact]
    public void GetFileInfo_AnEmptyProviderServesNothing()
    {
        Assert.False(new WebAssetFileProvider(Array.Empty<IFileProvider>()).GetFileInfo("vtex/prod-1962/static.vtex0").Exists);
    }

    [Fact]
    public void Watch_NeverFiresSoNoRootIsWatched()
    {
        // One FileSystemWatcher per root over a dozen gigabytes of rarely-touched files buys nothing: a
        // static response already carries Last-Modified and an ETag for the client to re-check.
        var token = Provider(Prepare("Install")).Watch("*");

        Assert.False(token.ActiveChangeCallbacks);
        Assert.False(token.HasChanged);
    }

    // --- what the startup report says ---------------------------------------------------------------------

    [Fact]
    public void Inspect_AMissingRootIsReportedAndNotThrown()
    {
        var status = AssetRootProbe.Inspect(Path.Combine(_root, "does-not-exist"));

        Assert.False(status.Exists);
        Assert.False(status.HasChunks);
    }

    [Fact]
    public void Inspect_KeysChunksByTheBuildTheyAnswerFor()
    {
        Prepare("Root");
        Write("Root", "vtex/prod-1962/static.vtex0", "a");
        Write("Root", "vtex/prod-1962/static.vtex1", "b");
        File.WriteAllText(Path.Combine(_root, "Root", "static.vtex2"), "loose");

        var status = AssetRootProbe.Inspect(Path.Combine(_root, "Root"));

        Assert.True(status.Exists);
        Assert.Equal(new[] { "prod-1962", "vtex" }, status.Chunks.Keys.OrderBy(k => k, StringComparer.OrdinalIgnoreCase).ToArray());
        Assert.Equal(2, status.Chunks["prod-1962"].Count);
        Assert.Single(status.Chunks["vtex"]);
    }

    [Fact]
    public void Inspect_ALooseCopyOfAServedChunkIsNotReportedTwice()
    {
        Write("Root", "vtex/prod-1962/static.vtex0", "served");
        File.WriteAllText(Path.Combine(_root, "Root", "static.vtex0"), "leftover");

        var status = AssetRootProbe.Inspect(Path.Combine(_root, "Root"));

        Assert.Equal(new[] { "prod-1962" }, status.Chunks.Keys);
    }

    [Fact]
    public void Inspect_CountsAChunkInsideTheVtexFolderAsLoose()
    {
        var root = Prepare("Root");
        Directory.CreateDirectory(Path.Combine(root, "vtex"));
        File.WriteAllText(Path.Combine(root, "vtex", "static.vtex0"), "x");

        var status = AssetRootProbe.Inspect(root);

        Assert.True(status.HasChunks);
        Assert.Equal(new[] { "vtex" }, status.Chunks.Keys);
        Assert.True(AssetRootProbe.ServesChunks(new[] { status }));
        Assert.Empty(AssetRootProbe.ServedBuilds(new[] { status }));
    }

    [Fact]
    public void Inspect_KeysAChunkByItsBuildAndNotAsLoose()
    {
        var root = Write("Root", "vtex/prod-1962/static.vtex0", "x");

        var status = AssetRootProbe.Inspect(root);

        Assert.Equal(new[] { "prod-1962" }, status.Chunks.Keys);
    }

    [Fact]
    public void ServedBuilds_NamesTheBuildsAnyRootCovers()
    {
        Write("A", "vtex/prod-1962/static.vtex0", "x");
        Write("B", "vtex/beta-1858/static.vtex0", "y");

        var builds = AssetRootProbe.ServedBuilds(
            AssetRootProbe.Inspect(new[] { Path.Combine(_root, "A"), Path.Combine(_root, "B") }));

        Assert.Equal(new[] { "beta-1858", "prod-1962" }, builds);
    }

    [Fact]
    public void Describe_AnEmptyRootSaysWhatTheClientWants()
    {
        var lines = AssetRootProbe.Describe(AssetRootProbe.Inspect(new[] { Prepare("Empty") }));

        var line = Assert.Single(lines);
        Assert.Contains("holds no texture chunks", line);
        Assert.Contains("vtex/<env>-<build>/", line);
        Assert.Contains("or loose in this folder", line);
    }

    [Fact]
    public void Describe_ARequestNobodyAnswersNamesTheFolderToFillIn()
    {
        var lines = AssetRootProbe.Describe(
            AssetRootProbe.Inspect(new[] { Prepare("Empty") }),
            new[] { "vtex/prod-1962/static.vtex0" });

        Assert.Contains(lines, l => l.Contains("nothing serves static.vtex0"));
        Assert.Contains(
            lines,
            l => l.Contains(Path.Combine("vtex", "prod-1962")) && l.Contains("Firefall:Assets:Paths"));
    }

    [Fact]
    public void MissingChunks_NamesTheFilesASetDoesNotHold()
    {
        var root = Prepare("Root");
        File.WriteAllText(Path.Combine(root, "static.vtex0"), "x");
        File.WriteAllText(Path.Combine(root, "static.vtex2"), "y");

        var missing = AssetRootProbe.MissingChunks(AssetRootProbe.Inspect(root));

        Assert.Equal(new[] { "static.vtex1" }, missing);
        Assert.Contains("static.vtex1", string.Join("; ", AssetRootProbe.Describe(new[] { AssetRootProbe.Inspect(root) })));
    }

    [Fact]
    public void MissingChunks_IsEmptyForACompleteSet()
    {
        var root = Prepare("Root");
        foreach (var name in VirtualTextureChunks.ChunkNames())
        {
            File.WriteAllText(Path.Combine(root, name), "x");
        }

        Assert.Empty(AssetRootProbe.MissingChunks(AssetRootProbe.Inspect(root)));
        Assert.Single(AssetRootProbe.Describe(new[] { AssetRootProbe.Inspect(root) }));
    }

    [Fact]
    public void ServesChunks_IsFalseForAHostWithNothingToAnswer()
    {
        Assert.False(AssetRootProbe.ServesChunks(AssetRootProbe.Inspect(new[] { Prepare("Empty") })));
        Assert.False(AssetRootProbe.ServesChunks(Array.Empty<AssetRootStatus>()));
    }

    [Fact]
    public void Describe_AChunkTheRootAnswersForIsNotMissing()
    {
        Write("Root", "vtex/beta-1858/static.vtex0", "x");

        // The file is there, under another build's name: the literal request misses, so the report must not
        // claim the server holds nothing - the host still answers it from the loose fallback or the next root.
        var lines = AssetRootProbe.Describe(
            AssetRootProbe.Inspect(new[] { Path.Combine(_root, "Root") }),
            new[] { "vtex/beta-1858/static.vtex0" });

        Assert.DoesNotContain(lines, l => l.Contains("nothing serves"));
        Assert.Contains(lines, l => l.Contains("beta-1858"));
    }

    [Fact]
    public void Describe_AMissingRootIsNamedSoAConfigLineCanBeFixed()
    {
        var missing = Path.Combine(_root, "gone");

        var lines = AssetRootProbe.Describe(AssetRootProbe.Inspect(new[] { missing }));

        Assert.Contains(missing, lines[0]);
        Assert.Contains("does not exist", lines[0]);
    }

    private WebAssetFileProvider Provider(params string[] roots)
    {
        return new WebAssetFileProvider(roots.Where(Directory.Exists).Select(r => (IFileProvider)new PhysicalFileProvider(r)));
    }

    /// <summary>Creates an empty asset root under the test's scratch directory.</summary>
    private string Prepare(string name)
    {
        var directory = Path.Combine(_root, name);
        Directory.CreateDirectory(directory);

        return directory;
    }

    /// <summary>Writes one file under an asset root, creating the folders the path names.</summary>
    private static void WriteAt(string root, string relative, string content)
    {
        var target = Path.Combine(root, relative);
        Directory.CreateDirectory(Path.GetDirectoryName(target));
        File.WriteAllText(target, content);
    }

    /// <summary>An asset root holding one file, i.e. <see cref="Prepare"/> and <see cref="WriteAt"/> together.</summary>
    private string Write(string root, string relative, string content)
    {
        var directory = Prepare(root);
        WriteAt(directory, relative, content);

        return directory;
    }
}
