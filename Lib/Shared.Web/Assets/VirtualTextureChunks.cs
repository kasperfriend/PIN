using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;

namespace Shared.Web.Assets;

/// <summary>
///     The path shape of the streamed high-resolution virtual-texture chunks.
/// </summary>
/// <remarks>
///     <c>VTRemotePath</c> in <c>firefall.ini</c> names a base, not a file -
///     <c>…/vtex/%ENVMNEMONIC%-%BUILDNUM%/static.vtex</c> - and the client appends a suffix to it for every
///     part of the page table it wants: <c>static.vtex_idx</c>, the index that says which tile lives where,
///     and <c>static.vtex0</c>, <c>static.vtex1</c> and so on - the levels themselves, <c>0</c> being the
///     sharpest mip and every number after it a coarser one. With no answer it keeps the levels baked into
///     its own archives, which is what a blurry Firefall looks like.
///     <para>
///     </para>
///     The build the client names comes from *its* install, so chunks copied out of another one - or simply
///     dropped in the <c>Assets</c> folder somebody said to fill in, instead of the build directory inside
///     it - miss on the literal path. <see cref="Candidates"/> is the small generosity that turns that
///     "nothing changed" into sharp textures: the same file answers, wherever in the root it was put, while
///     every other asset stays on its literal path.
///     <para>
///     </para>
///     Which brings up the part that is easy to get wrong, and worth more than the rest of this class: a
///     stock install of the game <em>does</em> carry a page table. It ships the index and the coarse levels
///     - <c>static.vtex_idx</c> and <c>static.vtex3</c> up to <c>static.vtex6</c> - which is why a client
///     with an empty <c>Assets</c> folder and a client with the wrong half of the set in it look exactly
///     alike. Only the first <see cref="HighResolutionLevelCount"/> levels (roughly twelve gigabytes the
///     install never carried, and the client once downloaded from Red5's CDN) are what a player means by
///     "high-resolution textures". So the host both serves every name a client can ask for and keeps those
///     apart from the rest in what it <see cref="HighResolutionChunkNames">says</see>: a root holding the
///     index and levels 3 to 6 is not a root that holds textures, and the log has to say so in as many
///     words, because from the client's side the two states are the same screenshot.
/// </remarks>
public static class VirtualTextureChunks
{
    /// <summary>Root folder of the streamed assets, as the client's URLs name it.</summary>
    public const string BuildFolder = "vtex";

    /// <summary>File stem the chunks share, from the <c>static.vtex</c> the ini names.</summary>
    public const string ChunkStem = "static";

    /// <summary>The body every chunk name carries between its stem and its suffix: <c>static.vtex0</c>.</summary>
    public const string FileBase = "vtex";

    /// <summary>Suffix of the page-table index: <c>static.vtex_idx</c>.</summary>
    public const string IndexSuffix = "_idx";

    /// <summary>
    ///     The coarsest level a client's page table carries. Level <c>0</c> is the sharpest mip and every
    ///     level after it is a quarter of the one before, so this is the last name that can exist rather
    ///     than the last one anybody has seen: recognition (<see cref="LevelOf"/>) is not bounded by it,
    ///     and a file carrying a higher number is still served under whatever name the client asked for.
    /// </summary>
    public const int MaxLevel = 6;

    /// <summary>
    ///     How many levels, counted from the sharpest, an install of the game does not carry: <c>0</c>,
    ///     <c>1</c> and <c>2</c> are the ones the client has to be sent, and the only ones that change how
    ///     the world looks. Everything else a root may hold is a level the client already owns.
    /// </summary>
    public const int HighResolutionLevelCount = 3;

    /// <summary>
    ///     The name of the page-table index, the chunk that says which tile lives where.
    /// </summary>
    /// <param name="stem">File stem; <see cref="ChunkStem"/> is what <c>VTRemotePath</c> names.</param>
    /// <returns><c>static.vtex_idx</c>.</returns>
    public static string IndexName(string stem = ChunkStem) => $"{stem}.{FileBase}{IndexSuffix}";

    /// <summary>
    ///     The name of one level of the page table.
    /// </summary>
    /// <param name="level">The level, <c>0</c> being the sharpest mip.</param>
    /// <param name="stem">File stem; <see cref="ChunkStem"/> is what <c>VTRemotePath</c> names.</param>
    /// <returns><c>static.vtex0</c> for level <c>0</c>.</returns>
    public static string LevelName(int level, string stem = ChunkStem) =>
        $"{stem}.{FileBase}{level.ToString(CultureInfo.InvariantCulture)}";

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
               IsChunkName(GetChunkName(subpath));
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
    ///     Every chunk name a client can ask for: the index first, then the levels from the sharpest down.
    /// </summary>
    /// <param name="stem">File stem; <see cref="ChunkStem"/> is what <c>VTRemotePath</c> names.</param>
    /// <returns>
    ///     <c>static.vtex_idx</c>, then <c>static.vtex0</c> through <c>static.vtex6</c>. This is the set a
    ///     root is <em>recognised</em> by, and it is wider than the set that decides anything - see
    ///     <see cref="HighResolutionChunkNames"/>.
    /// </returns>
    public static IReadOnlyList<string> ChunkNames(string stem = ChunkStem)
    {
        var names = new List<string>(MaxLevel + 2)
                    {
                        IndexName(stem),
                    };

        for (var level = 0; level <= MaxLevel; level++)
        {
            names.Add(LevelName(level, stem));
        }

        return names;
    }

    /// <summary>
    ///     The chunk names that decide whether the world is sharp: the levels no install of the game
    ///     carries, and therefore the only files whose absence a player can see.
    /// </summary>
    /// <param name="stem">File stem; <see cref="ChunkStem"/> is what <c>VTRemotePath</c> names.</param>
    /// <returns>
    ///     <c>static.vtex0</c>, <c>static.vtex1</c>, <c>static.vtex2</c> - in the order a client asks for
    ///     them. A root missing all three renders exactly as blurry as an empty one, whatever else it holds.
    /// </returns>
    public static IReadOnlyList<string> HighResolutionChunkNames(string stem = ChunkStem)
    {
        var names = new List<string>(HighResolutionLevelCount);

        for (var level = 0; level < HighResolutionLevelCount; level++)
        {
            names.Add(LevelName(level, stem));
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
    /// <returns>
    ///     <c>true</c> for <c>static.vtex_idx</c> and for <c>static.vtex&lt;level&gt;</c> at any level.
    /// </returns>
    public static bool IsChunkName(string fileName) => IsIndexName(fileName) || LevelOf(fileName) != null;

    /// <summary>
    ///     Whether a file name is the page-table index rather than a level of it.
    /// </summary>
    /// <param name="fileName">A file name, without a path.</param>
    /// <returns><c>true</c> for <c>static.vtex_idx</c>.</returns>
    public static bool IsIndexName(string fileName) =>
        !string.IsNullOrEmpty(fileName) &&
        fileName.EndsWith($".{FileBase}{IndexSuffix}", StringComparison.OrdinalIgnoreCase);

    /// <summary>
    ///     Whether a file name is one of the levels an install of the game does not carry.
    /// </summary>
    /// <param name="fileName">A file name, without a path.</param>
    /// <returns>
    ///     <c>true</c> for <c>static.vtex0</c>, <c>static.vtex1</c> and <c>static.vtex2</c> - the files
    ///     whose presence is the difference between a sharp world and a blurry one.
    /// </returns>
    public static bool IsHighResolutionChunkName(string fileName)
    {
        var level = LevelOf(fileName);

        return level != null && level.Value < HighResolutionLevelCount;
    }

    /// <summary>
    ///     The level of the page table a chunk name carries.
    /// </summary>
    /// <param name="fileName">A file name, without a path.</param>
    /// <returns>
    ///     <c>0</c> for <c>static.vtex0</c> (the sharpest mip), <c>6</c> for <c>static.vtex6</c>, and
    ///     <c>null</c> for the index, for <c>static.vtex</c> with no level at all, and for anything else.
    /// </returns>
    public static int? LevelOf(string fileName)
    {
        if (string.IsNullOrEmpty(fileName))
        {
            return null;
        }

        // After the last dot, because a chunk name is <stem>.vtex<level> and the stem is the client's to
        // choose: the ini says static.vtex, but what identifies a chunk is the suffix, not the name in
        // front of it, and a folder holding world.vtex0 deserves to be served under that name too.
        var dot = fileName.LastIndexOf('.');
        if (dot < 0)
        {
            return null;
        }

        var suffix = fileName.Substring(dot + 1);
        if (!suffix.StartsWith(FileBase, StringComparison.OrdinalIgnoreCase))
        {
            return null;
        }

        var digits = suffix.Substring(FileBase.Length);
        if (digits.Length == 0)
        {
            return null;
        }

        foreach (var character in digits)
        {
            if (character < '0' || character > '9')
            {
                return null;
            }
        }

        // NumberStyles.None: no sign, no separator, no whitespace - a level is a bare run of digits. A name
        // too long to be one is not a level rather than a level that overflowed, which is the honest answer
        // for a file nobody's client is ever going to ask for.
        return int.TryParse(digits, NumberStyles.None, CultureInfo.InvariantCulture, out var level)
                   ? level
                   : null;
    }

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

        // Both separators are named explicitly rather than taken from Path, because on Unix the backslash is
        // a legal character in a file name and AltDirectorySeparatorChar is therefore '/' as well: asked about
        // a path written the Windows way, code that trusted Path would see one long file name instead of a
        // path. A request path always uses '/' (the middleware hands subpaths over in URL form) and a path read
        // off the disk uses whatever the platform uses - Windows both, so both are separators here everywhere.
        var separators = new[]
                         {
                             '/',
                             '\\',
                             Path.DirectorySeparatorChar,
                             Path.AltDirectorySeparatorChar,
                         };

        return subpath.Split(separators, StringSplitOptions.RemoveEmptyEntries);
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
