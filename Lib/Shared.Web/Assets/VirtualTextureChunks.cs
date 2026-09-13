using System;
using System.Collections.Generic;
using System.IO;

namespace Shared.Web.Assets;

/// <summary>
///     The path shape of the streamed high-resolution virtual-texture chunks.
/// </summary>
/// <remarks>
///     <c>VTRemotePath</c> in <c>firefall.ini</c> names a build -
///     <c>…/vtex/%ENVMNEMONIC%-%BUILDNUM%/static.vtex</c> - and the client probes that base for the chunks
///     holding its high resolution mip levels
///     (<c>HEAD …/vtex/prod-1962/static.vtex0</c>, then <c>…1</c> and <c>…2</c>). With no answer it keeps the
///     low-resolution mips baked into its own archives, which is what a blurry Firefall looks like. The build
///     the client names comes from *its* install, so chunks copied out of another one - or simply dropped in
///     the <c>Assets</c> folder somebody said to fill in, instead of the build directory inside it - miss on
///     the literal path. <see cref="Candidates"/> is the small generosity that turns that "nothing changed"
///     into sharp textures: the same file answers, wherever in the root it was put, while every other asset
///     stays on its literal path.
/// </remarks>
public static class VirtualTextureChunks
{
    /// <summary>Root folder of the streamed assets, as the client's URLs name it.</summary>
    public const string BuildFolder = "vtex";

    /// <summary>File stem the chunks share, from the <c>static.vtex</c> the ini names.</summary>
    public const string ChunkStem = "static";

    /// <summary>Chunk suffixes a client probes for, biggest (and sharpest) first.</summary>
    public static readonly IReadOnlyList<string> Extensions = new[]
                                                               {
                                                                   ".vtex0",
                                                                   ".vtex1",
                                                                   ".vtex2",
                                                               };

    /// <summary>
    ///     Whether a request path is one of the virtual-texture chunks.
    /// </summary>
    /// <param name="subpath">The requested path, relative to the root that would serve it.</param>
    /// <returns><c>true</c> for <c>vtex/&lt;build&gt;/static.vtex1</c>, whatever the build is named.</returns>
    public static bool IsChunkPath(string subpath)
    {
        // The client asks for <root>/vtex/<build>/<chunk>; a path that is shallower than that is not a chunk
        // request (and, for instance, an asset stream path that happens to end in .vtex0 must stay a miss).
        return FindBuildFolder(subpath) != null &&
               HasChunkExtension(GetChunkName(subpath));
    }

    /// <summary>
    ///     The name a chunk request is made for, without the folder it was asked under.
    /// </summary>
    /// <param name="subpath">A chunk path, e.g. <c>vtex/prod-1962/static.vtex0</c>.</param>
    /// <returns><c>static.vtex0</c> - the name a chunk file carries wherever it happens to be kept.</returns>
    public static string GetChunkName(string subpath)
    {
        var parts = Split(subpath);

        return parts.Count == 0 ? string.Empty : parts[parts.Count - 1];
    }

    /// <summary>
    ///     The chunk names a complete set consists of, i.e. the files a client probes for.
    /// </summary>
    /// <param name="stem">File stem; <see cref="ChunkStem"/> is what <c>VTRemotePath</c> names.</param>
    /// <returns><c>static.vtex0</c>, <c>.vtex1</c>, <c>.vtex2</c> - in the order a client asks for them.</returns>
    public static IReadOnlyList<string> ChunkNames(string stem = ChunkStem)
    {
        var names = new List<string>(Extensions.Count);

        foreach (var extension in Extensions)
        {
            names.Add(stem + extension);
        }

        return names;
    }

    /// <summary>
    ///     The paths a request is looked up under, in order.
    /// </summary>
    /// <param name="subpath">The requested path, e.g. <c>vtex/prod-1962/static.vtex0</c>.</param>
    /// <returns>
    ///     The literal path first, then - for a chunk only - its bare name at the top of a root and its name
    ///     directly under <c>vtex</c>. Anything that is not a chunk gets exactly one candidate, so the
    ///     generosity cannot answer a path the request never named.
    /// </returns>
    public static IReadOnlyList<string> Candidates(string subpath)
    {
        var candidates = new List<string>
                         {
                             subpath
                         };

        if (!IsChunkPath(subpath))
        {
            return candidates;
        }

        var name = GetChunkName(subpath);

        AddDistinct(candidates, name);
        AddDistinct(candidates, $"{BuildFolder}/{name}");

        return candidates;
    }

    /// <summary>
    ///     Whether a file name is one of the chunk files a client asks for.
    /// </summary>
    /// <param name="fileName">A file name, without a path.</param>
    /// <returns><c>true</c> for <c>static.vtex0</c>, <c>static.vtex1</c> and <c>static.vtex2</c>.</returns>
    public static bool IsChunkName(string fileName) => HasChunkExtension(fileName);

    /// <summary>
    ///     The folder under <c>vtex</c> a path names, i.e. the build it was made for.
    /// </summary>
    /// <param name="subpath">A path under an asset root.</param>
    /// <returns>
    ///     <c>prod-1962</c> for <c>vtex/prod-1962/static.vtex0</c>, and <c>null</c> otherwise - including for
    ///     <c>vtex/static.vtex0</c>, where the folder is missing and the last segment is the file itself. A
    ///     build is by definition a directory <em>holding</em> files, so a path has to be at least three segments
    ///     deep for its middle one to name a build; without that rule a chunk left loose in <c>vtex\</c>
    ///     would be read as a build called <c>static.vtex0</c>.
    /// </returns>
    public static string FindBuildFolder(string subpath)
    {
        if (string.IsNullOrEmpty(subpath))
        {
            return null;
        }

        var parts = Split(subpath);

        // i + 1 has to leave at least one segment after the build folder: the file.
        for (var i = 0; i < parts.Count - 2; i++)
        {
            if (string.Equals(parts[i], BuildFolder, StringComparison.OrdinalIgnoreCase))
            {
                return parts[i + 1];
            }
        }

        return null;
    }

    /// <summary>
    ///     The segments of a path under an asset root, either separator accepted.
    /// </summary>
    /// <param name="subpath">A path, as a request carries it or as the file system returns it.</param>
    /// <returns>Its non-empty parts.</returns>
    internal static IReadOnlyList<string> Split(string subpath)
    {
        if (string.IsNullOrEmpty(subpath))
        {
            return Array.Empty<string>();
        }

        var separators = new[]
                         {
                             '/',
                             Path.DirectorySeparatorChar,
                             Path.AltDirectorySeparatorChar,
                         };

        return subpath.Split(separators, StringSplitOptions.RemoveEmptyEntries);
    }

    private static bool HasChunkExtension(string fileName)
    {
        if (string.IsNullOrEmpty(fileName))
        {
            return false;
        }

        foreach (var extension in Extensions)
        {
            if (fileName.EndsWith(extension, StringComparison.OrdinalIgnoreCase))
            {
                return true;
            }
        }

        return false;
    }

    private static void AddDistinct(ICollection<string> candidates, string candidate)
    {
        foreach (var existing in candidates)
        {
            if (string.Equals(existing, candidate, StringComparison.OrdinalIgnoreCase))
            {
                return;
            }
        }

        candidates.Add(candidate);
    }
}
