using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Text;

namespace Shared.Web.Assets;

/// <summary>
///     What one asset root holds, as the web asset host found it on disk.
/// </summary>
public sealed class AssetRootStatus
{
    /// <summary>
    ///     Initializes a new instance of the <see cref="AssetRootStatus"/> class.
    /// </summary>
    /// <param name="path">Absolute path of the root.</param>
    /// <param name="exists">Whether the folder exists at all.</param>
    /// <param name="chunks">The chunk files found in it, keyed by the folder they are served for.</param>
    public AssetRootStatus(string path, bool exists, IDictionary<string, List<string>> chunks)
    {
        Path = path;
        Exists = exists;
        Chunks = chunks ?? new Dictionary<string, List<string>>();
    }

    /// <summary>Absolute path of the root.</summary>
    public string Path { get; }

    /// <summary>Whether the folder exists. A missing root is a configuration line pointing at nothing.</summary>
    public bool Exists { get; }

    /// <summary>
    ///     The chunk files the root serves, keyed by the build folder they live in ("<c>vtex</c>" for chunks
    ///     kept loose - at the top of the root or directly inside <c>vtex</c>, where
    ///     <see cref="WebAssetFileProvider"/> still finds them by name). The values are full paths, so a
    ///     report can name a size next to a name.
    /// </summary>
    public IDictionary<string, List<string>> Chunks { get; }

    /// <summary>Whether the root holds a single chunk file anywhere PIN would serve from it.</summary>
    public bool HasChunks => Chunks.Count > 0;
}

/// <summary>
///     Reads the asset roots so the host can say, before anyone starts a client, what it will and will not
///     serve - and, when it will not serve the high-resolution texture chunks, exactly where to put them.
/// </summary>
/// <remarks>
///     A missing chunk file is not an error: the client probes for the chunks, and a probe answered with
///     nothing is simply the game running on the low-resolution mips its own archives carry. That is the state
///     a player calls "every texture in this game is blurry" and an operator calls "I put the files in the
///     Assets folder and nothing changed", and the reason the two disagree is a path nobody could see from the
///     log. So the host reports the roots it was given, the chunks it found in them, and what a client will ask
///     for that nobody is answering - once, at startup, next to the port it listens on.
///     <para>
///     </para>
///     Everything here is a read-only scan of at most two directory levels: a root holding twelve gigabytes in
///     three files is a folder with three entries, not a walk.
/// </remarks>
public static class AssetRootProbe
{
    /// <summary>
    ///     Reports one asset root.
    /// </summary>
    /// <param name="root">Absolute path of the folder to read. A missing folder is reported, not thrown.</param>
    /// <returns>What is in it: whether it exists, and which chunk files the host would serve from it.</returns>
    public static AssetRootStatus Inspect(string root)
    {
        var chunks = new Dictionary<string, List<string>>(StringComparer.OrdinalIgnoreCase);

        if (string.IsNullOrEmpty(root) || !Directory.Exists(root))
        {
            return new AssetRootStatus(root, false, chunks);
        }

        var vtex = Path.Combine(root, VirtualTextureChunks.BuildFolder);

        // The per-build folders the client asks for by name, i.e. vtex/<env>-<build>/static.vtex0: the
        // literal answer, and the only one that would be reported before.
        foreach (var build in SafeEnumerateDirectories(vtex))
        {
            // Always relative to the asset root: the key is the <build> inside the request path the client
            // sends, and a path measured from vtex\ itself would no longer contain that folder name.
            Collect(root, build, chunks);
        }

        // Then the two places the same files are actually found in the wild: loose at the top of the root
        // (how the client's own system\vt folder holds them, and where "copied them into Assets" leaves
        // them) and loose inside vtex\ itself. Both are served, by name, and both are reported - in the order
        // the host looks, so a report never promises a file the provider would answer with another one.
        Collect(root, root, chunks);
        Collect(root, vtex, chunks);
        DropShadowed(chunks);

        return new AssetRootStatus(root, true, chunks);
    }

    /// <summary>
    ///     Reports every asset root, in order.
    /// </summary>
    /// <param name="roots">The roots the host serves, as it resolved them.</param>
    /// <returns>One entry per root, in the same order.</returns>
    public static IReadOnlyList<AssetRootStatus> Inspect(IEnumerable<string> roots)
    {
        return (roots ?? Array.Empty<string>()).Select(Inspect).ToList();
    }

    /// <summary>
    ///     The build folders every root serves chunks for, i.e. the <c>vtex/&lt;build&gt;/</c> names a client
    ///     can be pointed at. Chunks kept loose are not a build, so they are not listed here.
    /// </summary>
    /// <param name="statuses">The roots, as <see cref="Inspect(IEnumerable{string})"/> read them.</param>
    /// <returns>Distinct build folder names, sorted, without the placeholder for loose chunks.</returns>
    public static IReadOnlyList<string> ServedBuilds(IEnumerable<AssetRootStatus> statuses)
    {
        return (statuses ?? Array.Empty<AssetRootStatus>())
               .Where(s => s != null)
               .SelectMany(s => s.Chunks.Keys)
               .Where(k => !string.Equals(k, VirtualTextureChunks.BuildFolder, StringComparison.OrdinalIgnoreCase))
               .Distinct(StringComparer.OrdinalIgnoreCase)
               .OrderBy(k => k, StringComparer.OrdinalIgnoreCase)
               .ToList();
    }

    /// <summary>
    ///     Whether any root holds a chunk file the host would serve - by its literal path or by name, which is
    ///     the difference between "the textures are here" and the warning a server with an empty folder earns.
    /// </summary>
    /// <param name="statuses">The roots, as <see cref="Inspect(IEnumerable{string})"/> read them.</param>
    /// <returns><c>true</c> when a client streaming from this host would get high-resolution mip levels.</returns>
    public static bool ServesChunks(IEnumerable<AssetRootStatus> statuses)
    {
        return (statuses ?? Array.Empty<AssetRootStatus>()).Any(s => s != null && s.HasChunks);
    }

    /// <summary>
    ///     The chunk names a root does not hold, out of the set a client probes for.
    /// </summary>
    /// <param name="status">One scanned root.</param>
    /// <returns>Missing names, in the order the client asks for them; empty for a complete set.</returns>
    public static IReadOnlyList<string> MissingChunks(AssetRootStatus status)
    {
        var held = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

        foreach (var entry in status?.Chunks ?? new Dictionary<string, List<string>>())
        {
            foreach (var file in entry.Value)
            {
                held.Add(Path.GetFileName(file));
            }
        }

        return VirtualTextureChunks.ChunkNames()
                                  .Where(n => !held.Contains(n))
                                  .ToList();
    }

    /// <summary>
    ///     The lines the host writes about its roots: one per root, plus the answer to "is this server holding
    ///     the chunks my client is asking for" when it is not.
    /// </summary>
    /// <param name="statuses">The roots, as <see cref="Inspect(IEnumerable{string})"/> read them.</param>
    /// <param name="requestedChunks">
    ///     The chunk paths a request asked for, e.g. <c>vtex/prod-1962/static.vtex0</c>. Left empty for the
    ///     startup report, which has no request to answer and only says what the server holds.
    /// </param>
    /// <returns>The log lines, empty when there is nothing to say.</returns>
    public static IReadOnlyList<string> Describe(IReadOnlyList<AssetRootStatus> statuses, IEnumerable<string> requestedChunks = null)
    {
        var lines = new List<string>();

        foreach (var status in statuses ?? Array.Empty<AssetRootStatus>())
        {
            if (status == null)
            {
                continue;
            }

            if (!status.Exists)
            {
                lines.Add($"asset root {status.Path} does not exist");
            }
            else if (!status.HasChunks)
            {
                lines.Add(
                    $"asset root {status.Path} holds no texture chunks - a client wants {string.Join(", ", VirtualTextureChunks.ChunkNames())}, under {VirtualTextureChunks.BuildFolder}/<env>-<build>/ or loose in this folder");
            }
            else
            {
                lines.Add($"asset root {status.Path} serves {DescribeChunks(status)}");

                // All three chunks are probed, and each holds a different level of the page table: a root
                // with only static.vtex0 sharpens what it covers and leaves the rest soft, which reads like a
                // half-working fix unless the log says which half is missing.
                var incomplete = MissingChunks(status);
                if (incomplete.Count > 0)
                {
                    lines.Add($"asset root {status.Path} has no {string.Join(", ", incomplete)} - a client probes all three, so the rest of the page table stays at its cached resolution");
                }
            }
        }

        var wanted = (requestedChunks ?? Enumerable.Empty<string>()).Where(w => !string.IsNullOrEmpty(w)).ToList();
        if (wanted.Count == 0)
        {
            return lines;
        }

        var served = (statuses ?? Array.Empty<AssetRootStatus>())
                     .Where(s => s != null && s.Exists)
                     .SelectMany(s => s.Chunks.Values)
                     .Select(file => Path.GetFileName(file))
                     .ToList();

        var missing = wanted.Where(chunk => !served.Any(name => string.Equals(name, Path.GetFileName(chunk), StringComparison.OrdinalIgnoreCase)))
                            .ToList();
        if (missing.Count == 0)
        {
            return lines;
        }

        lines.Add($"nothing serves {string.Join(", ", missing.Select(f => Path.GetFileName(f)))}");

        var advice = DescribeMissingChunks(missing);
        if (advice != null)
        {
            lines.Add(advice);
        }

        return lines;
    }

    /// <summary>
    ///     Where the files a missing chunk belongs to go, in words an operator can act on.
    /// </summary>
    /// <param name="requestedChunks">The chunk paths a client asked for and nobody answered.</param>
    /// <returns>One line naming the folder to fill in, or <c>null</c> when no request named a build.</returns>
    public static string DescribeMissingChunks(IEnumerable<string> requestedChunks)
    {
        var build = (requestedChunks ?? Array.Empty<string>())
                    .Select(VirtualTextureChunks.FindBuildFolder)
                    .FirstOrDefault(b => !string.IsNullOrEmpty(b));

        if (build == null)
        {
            return null;
        }

        return "Put the chunk files under " + Path.Combine(VirtualTextureChunks.BuildFolder, build) +
               " inside an asset root, or name a folder that already holds them in Firefall:Assets:Paths (the " +
               "client's own system" + Path.DirectorySeparatorChar + "vt does). Until one of those answers, the " +
               "client renders the low-resolution mips packed in its own archives, which is what a blurry Firefall is.";
    }

    /// <summary>
    ///     Adds the chunk files of one folder to the scan result, keyed by what the host answers for.
    /// </summary>
    /// <param name="root">The asset root, the base every key is measured from.</param>
    /// <param name="directory">The folder to read.</param>
    /// <param name="chunks">The scan result, edited in place.</param>
    private static void Collect(string root, string directory, IDictionary<string, List<string>> chunks)
    {
        foreach (var file in SafeEnumerateFiles(directory))
        {
            if (!VirtualTextureChunks.IsChunkName(VirtualTextureChunks.GetChunkName(file)))
            {
                continue;
            }

            // The key is the <build> the client would have to name to be answered by this file's literal
            // path; a chunk outside any build folder is keyed under vtex itself, the loose bucket the host
            // answers by name (the chunk lying at the top of the asset root included). Measured per file and
            // not per folder, because the folders read here are the root and vtex\ themselves as much as any
            // build directory, and the root of one is the parent of the other.
            var key = VirtualTextureChunks.FindBuildFolder(Path.GetRelativePath(root, file)) ??
                      VirtualTextureChunks.BuildFolder;

            if (!chunks.TryGetValue(key, out var list))
            {
                list = new List<string>();
                chunks[key] = list;
            }

            list.Add(file);
        }
    }

    /// <summary>
    ///     Drops the chunks a looser scan only re-found: a leftover copy of a file the host already answers by
    ///     its real path is not a second answer to the client's question, and reporting it reads like one.
    /// </summary>
    /// <param name="chunks">The scan result, edited in place.</param>
    /// <summary>
    ///     Drops the chunks a looser scan only re-found: a leftover copy of a file the host already answers by
    ///     its real path is not a second answer to the client's question, and reporting it reads like one.
    /// </summary>
    /// <param name="chunks">The scan result, edited in place.</param>
    private static void DropShadowed(IDictionary<string, List<string>> chunks)
    {
        // The names a literal build path already answers for; the comparison is the one the file system on
        // Windows makes, because "Static.VTEX0" and "static.vtex0" are the same answer to the client.
        var served = new HashSet<string>(
                                         chunks.Where(e => !IsLoose(e.Key))
                                               .SelectMany(e => e.Value)
                                               .Select(f => Path.GetFileName(f)),
                                         StringComparer.OrdinalIgnoreCase);

        foreach (var entry in chunks.ToList())
        {
            var seen = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

            entry.Value.RemoveAll(
                                  f =>
                                  {
                                      var name = Path.GetFileName(f);

                                      // Same-folder duplicates - the top of the root and vtex\ itself land in
                                      // one loose bucket - keep the one the host would find first, and a loose
                                      // file is dropped only against a build folder's copy, never its own.
                                      if (seen.Contains(name) || (IsLoose(entry.Key) && served.Contains(name)))
                                      {
                                          return true;
                                      }

                                      seen.Add(name);
                                      return false;
                                  });

            if (entry.Value.Count == 0)
            {
                chunks.Remove(entry.Key);
            }
        }
    }

    private static bool IsLoose(string key) => string.Equals(key, VirtualTextureChunks.BuildFolder, StringComparison.OrdinalIgnoreCase);

    private static string DescribeChunks(AssetRootStatus status)
    {
        var builder = new StringBuilder();

        foreach (var entry in status.Chunks.OrderBy(e => e.Key, StringComparer.OrdinalIgnoreCase))
        {
            if (builder.Length > 0)
            {
                builder.Append(", ");
            }

            var folder = string.Equals(entry.Key, VirtualTextureChunks.BuildFolder, StringComparison.OrdinalIgnoreCase)
                             ? "loose"
                             : entry.Key;

            builder.Append($"{folder}: {string.Join(", ", entry.Value.Select(ReadableName))}");
        }

        return builder.ToString();
    }

    private static string ReadableName(string file)
    {
        var name = Path.GetFileName(file);

        try
        {
            if (File.Exists(file))
            {
                var length = new FileInfo(file).Length;
                return $"{name} ({(length / 1024.0 / 1024.0).ToString("0.#", CultureInfo.InvariantCulture)} MB)";
            }
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
        {
            // A size that cannot be read is a size that is not reported; the file itself was still found.
        }

        return name;
    }

    private static IReadOnlyList<string> SafeEnumerateFiles(string directory)
    {
        try
        {
            return Directory.GetFiles(directory);
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException or ArgumentException)
        {
            return Array.Empty<string>();
        }
    }

    private static IReadOnlyList<string> SafeEnumerateDirectories(string directory)
    {
        try
        {
            return Directory.GetDirectories(directory);
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException or ArgumentException)
        {
            return Array.Empty<string>();
        }
    }
}
