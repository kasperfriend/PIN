using System;
using System.Collections.Generic;
using System.IO;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Hosting;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.FileProviders;
using Serilog;
using Shared.Web;
using Shared.Web.Assets;
using Shared.Web.Config;

namespace WebHost.WebAsset;

/// <summary>
///     The host the client streams from: <c>AssetStreamPath</c> and <c>VTRemotePath</c> in
///     <c>firefall.ini</c> both name it, so everything here is answered out of the asset roots
///     (<c>Assets</c> next to the binary plus <c>Firefall:Assets:Paths</c>) and nothing else.
/// </summary>
public class WebServer : BaseWebServer
{
    private static readonly ILogger Log = Serilog.Log.ForContext<WebServer>();

    public WebServer(IConfiguration configuration)
        : base(configuration)
    {
    }

    protected override void ConfigureChildServices(IServiceCollection services)
    {
    }

    protected override void ConfigureChild(IApplicationBuilder app, IWebHostEnvironment env)
    {
        var roots = ResolveAssetRoots(env);
        var providers = new List<IFileProvider>();

        foreach (var root in roots)
        {
            // PhysicalFileProvider throws for a missing directory, and a folder named in the configuration that
            // has not been created yet is a line for the report, not a reason to have no asset host at all.
            if (Directory.Exists(root))
            {
                providers.Add(new PhysicalFileProvider(root));
            }
        }

        if (providers.Count == 0)
        {
            Log.Warning(
                "Web asset host has nothing to serve: none of {Roots} exists, so every client request here is a 404 and every texture stays at the resolution packed in the client's own archives",
                string.Join(", ", roots));
        }

        app.UseStaticFiles(new StaticFileOptions
                           {
                               FileProvider = new WebAssetFileProvider(providers),
                               RequestPath = string.Empty,

                               // "static.vtex0" is not an extension any mime map ever learned, and the
                               // default content-type provider answers an unknown one with a 404 before it
                               // ever looks at the disk. That single default is the difference between a
                               // folder full of textures and textures a client can load: with it off, every
                               // chunk request stays a NotFound even when the file sits exactly where the
                               // client asked for it. Ranges, ETag and Last-Modified come with the middleware
                               // itself: a client resuming a streamed tile needs the first, and it re-checks
                               // a chunk it already cached against the other two.
                               ServeUnknownFileTypes = true,
                               DefaultContentType = "application/octet-stream",
                           });

        Report(roots);
    }

    /// <summary>
    ///     The folders this host serves, in the order a request is answered from them: <c>Assets</c> next to
    ///     the binary first, then every folder <c>Firefall:Assets:Paths</c> names.
    /// </summary>
    /// <param name="env">The hosting environment, whose content root anchors both folder kinds.</param>
    /// <returns>The absolute paths, in order, without the duplicates.</returns>
    private IReadOnlyList<string> ResolveAssetRoots(IWebHostEnvironment env)
    {
        var roots = new List<string>();

        void Add(string path)
        {
            if (string.IsNullOrWhiteSpace(path))
            {
                return;
            }

            // GetFullPath on top of the join: it collapses the separators and the trailing one, so a folder
            // named with a slash at the end - or with slashes anywhere, as a pasted path tends to carry them -
            // is recognised as the root it already is instead of being served twice.
            var full = Path.IsPathRooted(path)
                           ? Path.GetFullPath(path)
                           : Path.GetFullPath(Path.Combine(env.ContentRootPath, path));

            // A root listed twice - or a configured path that is the default one - would be served twice, and
            // a report that says so reads like two folders holding the same textures.
            if (!roots.Exists(r => string.Equals(r, full, StringComparison.OrdinalIgnoreCase)))
            {
                roots.Add(full);
            }
        }

        Add(Path.Combine(env.ContentRootPath, "Assets"));

        var configured = (Configuration.GetSection("Firefall").Get<Firefall>() ?? new Firefall()).Assets;
        foreach (var path in configured?.Paths ?? new List<string>())
        {
            Add(path);
        }

        return roots;
    }

    /// <summary>
    ///     Says what the host will serve before anyone starts a client: the roots it reads, the chunks it found
    ///     in them, and what to do about the ones it did not.
    /// </summary>
    /// <param name="roots">The folders it was built from, including the ones that do not exist.</param>
    /// <remarks>
    ///     Three answers, not two. Chunks under a <c>vtex/&lt;build&gt;/</c> folder are the literal reply to a
    ///     client's probe; chunks lying loose are the same reply found by name, so they are served, and they
    ///     deserve to be reported as served rather than as the warning an empty folder earns - the difference
    ///     between those two sentences is the whole reason this host used to leave people guessing.
    /// </remarks>
    private static void Report(IReadOnlyList<string> roots)
    {
        var statuses = AssetRootProbe.Inspect(roots);
        var lines = AssetRootProbe.Describe(statuses);
        var served = string.Join("; ", lines);

        if (lines.Count == 0)
        {
            Log.Information("Web asset host has no root to serve from");
            return;
        }

        var builds = AssetRootProbe.ServedBuilds(statuses);

        if (AssetRootProbe.ServesHighResolutionChunks(statuses) && builds.Count > 0)
        {
            Log.Information(
                "Web asset host serves {RootCount} root(s), with high-resolution texture chunks for {Builds}: {Served}",
                roots.Count,
                string.Join(", ", builds),
                served);
            return;
        }

        if (AssetRootProbe.ServesHighResolutionChunks(statuses))
        {
            Log.Information(
                "Web asset host serves {RootCount} root(s) with high-resolution texture chunks held loose, answered for any build a client names: {Served}",
                roots.Count,
                served);
            return;
        }

        // The last two cases are the same screenshot: a client that renders the mips it shipped with. They
        // are told apart here only because they are fixed differently - one is a folder to fill in and the
        // other is a folder that is already full of the wrong files.
        if (AssetRootProbe.ServesChunks(statuses))
        {
            // "I put the vtex files in Assets and nothing changed", answered: a stock install of the game
            // already carries the page-table index and the coarse levels (static.vtex_idx, static.vtex3 and
            // up), so a host answering with those is answering every probe the client makes and handing it
            // levels it already has. The three it does not have are the only ones that matter, and they are
            // the twelve gigabytes Red5's CDN used to serve - see Docs/ASSETS.md on where they went.
            Log.Warning(
                "Blurry textures ahead: every chunk file these roots hold is one the client's own install already carries - {Roots}. {Served}. {Names} are the sharp mips and the only chunk files that change how the game looks; put them straight into {Root}, or into {Build} to answer one build by the exact name the client asks for, or point Firefall:Assets:Paths at a folder that holds them. See Docs/ASSETS.md",
                string.Join(", ", roots),
                served,
                string.Join(", ", VirtualTextureChunks.HighResolutionChunkNames()),
                roots[0],
                Path.Combine(roots[0], VirtualTextureChunks.BuildFolder, "<env>-<build>"));
            return;
        }

        // The blurry-textures answer, said once here so it never has to be guessed from a 404: the client
        // probes for the chunks, gets nothing, and keeps rendering the low-resolution mips of its own archives.
        Log.Information("Web asset host serves {RootCount} root(s) and no texture chunks: {Served}", roots.Count, served);

        Log.Warning(
            "Blurry textures ahead: no asset root holds a chunk file for a client to fetch - {Roots}. Copy {Names} straight into {Root}, or into {Build} to answer one build by the exact name the client asks for, or point Firefall:Assets:Paths at a folder that already holds them (the client's own system\\vt does). See Docs/ASSETS.md",
            string.Join(", ", roots),
            string.Join(", ", VirtualTextureChunks.HighResolutionChunkNames()),
            roots[0],
            Path.Combine(roots[0], VirtualTextureChunks.BuildFolder, "<env>-<build>"));
    }
}
