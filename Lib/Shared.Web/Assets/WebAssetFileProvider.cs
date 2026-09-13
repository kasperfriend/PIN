using System.Collections.Generic;
using Microsoft.Extensions.FileProviders;
using Microsoft.Extensions.Primitives;

namespace Shared.Web.Assets;

/// <summary>
///     The file provider the web asset host serves from: every configured root, in order, as one namespace.
/// </summary>
/// <remarks>
///     A request is answered by the first root that has the file, so <c>Assets</c> next to the binary keeps
///     precedence and the folders named by <c>Firefall:Assets:Paths</c> are exactly that - more places to look,
///     which is how a server avoids carrying a second copy of a dozen gigabytes of virtual-texture chunks.
///     <para>
///     </para>
///     A virtual-texture chunk is the one request it will answer by more than the literal path, because the
///     build in that path is a property of <em>the client's</em> install and not of the data: the file the
///     client wants is the same file whether it lies under <c>vtex/prod-1962/</c>, loose at the top of the
///     root (how the client's own <c>system\vt</c> folder, and any "copied them into Assets" folder, keeps
///     them) or directly inside <c>vtex</c>. <see cref="VirtualTextureChunks.Candidates"/> is that order, and
///     a non-chunk request has exactly one candidate - nothing that was not named by path gets served.
///     <para>
///     </para>
///     A root that does not exist is skipped instead of refused: <c>PhysicalFileProvider</c> throws for a
///     missing directory, and a path in the configuration that has not been created yet (or has been cleaned
///     away) is a thing to mention once at startup - see <see cref="AssetRootProbe"/> - not one that stops the
///     host that would otherwise have served everything else.
/// </remarks>
public sealed class WebAssetFileProvider : IFileProvider
{
    private readonly IReadOnlyList<IFileProvider> _roots;

    /// <summary>
    ///     Initializes a new instance of the <see cref="WebAssetFileProvider"/> class.
    /// </summary>
    /// <param name="roots">
    ///     The roots to search, first match wins. Empty is a legal answer: a host with no roots serves nothing
    ///     and says so, which is what a server with an <c>Assets</c> folder nobody filled in has always been.
    /// </param>
    public WebAssetFileProvider(IEnumerable<IFileProvider> roots)
    {
        _roots = roots == null
                     ? new List<IFileProvider>()
                     : new List<IFileProvider>(roots);
    }

    /// <inheritdoc />
    public IFileInfo GetFileInfo(string subpath)
    {
        // Candidates outside, roots inside: a chunk that is filed under the build the client named always
        // wins over a loose one, whichever root either of them lives in - the fallback is a way to find the
        // right file, not a way to prefer one folder over another.
        foreach (var candidate in VirtualTextureChunks.Candidates(subpath))
        {
            foreach (var root in _roots)
            {
                var info = root.GetFileInfo(candidate);
                if (info.Exists)
                {
                    return info;
                }
            }
        }

        return new NotFoundFileInfo(subpath);
    }

    /// <inheritdoc />
    public IDirectoryContents GetDirectoryContents(string subpath)
    {
        // Nothing asks a static host for a listing, and the middleware only uses this to serve a directory;
        // first root that answers wins, and a root without the folder simply does not answer.
        foreach (var root in _roots)
        {
            var contents = root.GetDirectoryContents(subpath);
            if (contents.Exists)
            {
                return contents;
            }
        }

        return new NotFoundDirectoryContents();
    }

    /// <inheritdoc />
    public IChangeToken Watch(string filter)
    {
        // The assets are not edited while the server runs, so the change tokens that would say otherwise -
        // one FileSystemWatcher per physical root, on a folder that may hold twelve gigabytes - are not worth
        // their cost. A static file response also carries Last-Modified and an ETag, which is what the client
        // re-checks against; the middleware never needs to be told a file changed underneath it.
        return NullChangeToken.Singleton;
    }
}
