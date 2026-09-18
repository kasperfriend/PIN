#nullable enable
using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading;
using Serilog;

namespace GameServer.Physics.PoseLoader;

public class PoseLoader
{
    private readonly string _assetRoot;
    private readonly ILogger _logger;

    private readonly HashSet<string> _availableFolders;
    private readonly ConcurrentDictionary<string, string> _pathCache = new();
    private readonly ConcurrentDictionary<string, PoseData> _dataCache = new();

    /// <summary>
    ///     Assets that will not load, so they are never read again. A pose that is missing from the
    ///     archive or does not parse is not going to start parsing halfway through a session, and the
    ///     resolution that asks for it runs on the hot path: <see cref="PhysicsEngine.GetAssetShape"/>
    ///     is called for every movement update of every entity (players on their movement input, NPCs
    ///     on the AI's movement tick), and it asks the loader again for any shape it has no entry for.
    ///     Without this the failure was paid per entity per tick: a file read, an INI parse, a caught
    ///     exception and a full stack trace on the log - about twenty times a second for every NPC
    ///     whose pose does not load, which is what a freshly populated zone spends its shard tick on
    ///     and what turns a spawn storm, or a glider flying into one, into a ping spike.
    /// </summary>
    private readonly ConcurrentDictionary<string, byte> _failedAssets = new();

    private int _readCount;

    public PoseLoader(string assetRoot, ILogger? logger = null)
    {
        _assetRoot = assetRoot;
        _logger = logger ?? Log.ForContext<PoseLoader>();

        if (!Directory.Exists(_assetRoot))
        {
            _logger.Information("PoseLoader AssetDBPath not found, will not load posefiles");
            _availableFolders = [];
            return;
        }

        _availableFolders = new(AssetPathResolver.ScanAvailableFolders(_assetRoot));

        _logger.Information("PoseLoader initialized with {FolderCount} folders", _availableFolders.Count);
    }

    /// <summary>
    ///     How many times a pose file was actually read from disk and parsed. A diagnostic: after the
    ///     first request for an asset this stops moving for that asset whatever its outcome, which is
    ///     what tells a hot path that keeps asking apart from a cache that stopped answering. The
    ///     tests read it for the same reason.
    /// </summary>
    public int ReadCount => _readCount;

    /// <summary>How many assets this loader has given up on (and now answers for from memory).</summary>
    public int FailedAssetCount => _failedAssets.Count;

    public PoseData Load(string assetId)
    {
        if (!TryLoad(assetId, out var result))
        {
            throw new FileNotFoundException($"Pose file not found for ID: {assetId}");
        }

        return result;
    }

    public bool TryLoad(string assetId, out PoseData result)
    {
        result = default!;

        if (string.IsNullOrWhiteSpace(assetId) || assetId.Length != 8 || !assetId.All(char.IsDigit))
        {
            // Also remembered, keyed by whatever was asked for: a malformed id in a database row is
            // asked for exactly as often as a missing file is.
            if (_failedAssets.TryAdd(string.IsNullOrEmpty(assetId) ? "<empty>" : assetId, 0))
            {
                _logger.Warning("Invalid asset ID format: {AssetId}; the fallback shape is used for it from now on", assetId);
            }

            return false;
        }

        _logger.Verbose("Pose file {assetId} Requested", assetId);

        if (_dataCache.TryGetValue(assetId, out var cached))
        {
            result = cached;
            _logger.Verbose("Pose file {assetId} {Name} Cached", assetId, result.Name);
            return true;
        }

        if (_failedAssets.ContainsKey(assetId))
        {
            // Already tried, and known not to load. Verbose, because this is asked several times a
            // second per entity: the failure itself was reported once, when it was discovered.
            _logger.Verbose("Pose file {AssetId} is known not to load; using the fallback shape", assetId);
            return false;
        }

        var path = ResolvePath(assetId);
        if (path == null)
        {
            if (_failedAssets.TryAdd(assetId, 0))
            {
                _logger.Warning(
                    "Pose file {AssetId} not found under {AssetRoot}; the fallback shape is used for it from now on",
                    assetId,
                    _assetRoot);
            }

            return false;
        }

        try
        {
            _ = Interlocked.Increment(ref _readCount);

            var ini = IniLoader.IniLoader.LoadFromFile(path);
            result = PoseData.LoadFromIni(ini, _logger);
            _dataCache[assetId] = result;
            _logger.Debug("Pose file {AssetId} Loaded pose {Name} from {Path}", assetId, result.Name, path);
            return true;
        }
        catch (Exception ex)
        {
            // Once per asset, with the reason: the id, the file and the exception are what an
            // operator needs to fix the data, and repeating the stack trace on every movement tick
            // is the noise this cache exists to remove.
            if (_failedAssets.TryAdd(assetId, 0))
            {
                _logger.Warning(
                    ex,
                    "Pose file {AssetId} failed to load from {Path}; the fallback shape is used for it from now on and the file is not read again",
                    assetId,
                    path);
            }

            return false;
        }
    }

    private string? ResolvePath(string assetId)
    {
        return AssetPathResolver.Resolve(_assetRoot, assetId, ".pose", _availableFolders, _pathCache);
    }
}
