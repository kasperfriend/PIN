#nullable enable
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.RegularExpressions;
using Serilog;

namespace GameServer;

/// <summary>
///     Scans the machine for a Firefall client installation so that
///     <c>GameServer.config.json</c> can be populated automatically.
/// </summary>
internal static class FirefallInstallLocator
{
    private const string FirefallFolderName = "Firefall";
    private const string SteamAppsFolderName = "steamapps";
    private const string EnvFirefallPathVariable = "PIN_FIREFALL_PATH";
    private const string EnvSteamPathVariable = "PIN_STEAM_PATH";

    private static readonly ILogger Log = Serilog.Log.ForContext<FirefallInstallLocator>();

    /// <summary>
    ///     Locate a Firefall installation on the machine, or return <c>null</c> when none is found.
    /// </summary>
    public static InstalledFirefall? Locate()
    {
        // 1. An explicit environment override always wins.
        var envRoot = Environment.GetEnvironmentVariable(EnvFirefallPathVariable);
        if (!string.IsNullOrWhiteSpace(envRoot) && TryCreate(envRoot, out var fromEnv))
        {
            Log.Information("Using Firefall installation from {Variable}={Path}", EnvFirefallPathVariable, fromEnv!.Root);
            return fromEnv;
        }

        // 2. Steam library folders (parsed from libraryfolders.vdf under each Steam install).
        foreach (var steamRoot in EnumerateSteamRoots())
        {
            if (TryFindInSteamLibrary(steamRoot, out var viaSteam))
            {
                Log.Information("Detected Firefall installation at {Path} via Steam library", viaSteam!.Root);
                return viaSteam;
            }
        }

        // 3. Search around the executable and the working directory.
        foreach (var candidate in EnumerateNearbyCandidateRoots())
        {
            if (TryCreate(candidate, out var nearby))
            {
                Log.Information("Detected Firefall installation at {Path}", nearby!.Root);
                return nearby;
            }
        }

        return null;
    }

    private static IEnumerable<string> EnumerateSteamRoots()
    {
        var candidates = new List<string>();

        var envSteam = Environment.GetEnvironmentVariable(EnvSteamPathVariable);
        if (!string.IsNullOrWhiteSpace(envSteam))
        {
            candidates.Add(envSteam);
        }

        AddSpecialFolder(candidates, Environment.SpecialFolder.ProgramFilesX86, "Steam");
        AddSpecialFolder(candidates, Environment.SpecialFolder.ProgramFiles, "Steam");

        return candidates.Where(Directory.Exists).Distinct(StringComparer.OrdinalIgnoreCase);
    }

    private static void AddSpecialFolder(ICollection<string> list, Environment.SpecialFolder folder, string subPath)
    {
        try
        {
            var baseDir = Environment.GetFolderPath(folder);
            if (!string.IsNullOrWhiteSpace(baseDir))
            {
                list.Add(Path.Combine(baseDir, subPath));
            }
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException or ArgumentException)
        {
            // The special folder may not be available on this platform.
        }
    }

    private static bool TryFindInSteamLibrary(string steamRoot, out InstalledFirefall? result)
    {
        result = null;

        // Install directly under the default Steam apps folder.
        if (TryCreate(Path.Combine(steamRoot, SteamAppsFolderName, "common", FirefallFolderName), out result))
        {
            return true;
        }

        var libraryFoldersFile = Path.Combine(steamRoot, SteamAppsFolderName, "libraryfolders.vdf");
        if (!File.Exists(libraryFoldersFile))
        {
            return false;
        }

        foreach (var libraryPath in ReadLibraryFolderPaths(libraryFoldersFile))
        {
            // libraryPath normally points at the "steamapps" folder; allow for consumers that
            // store the library root instead.
            foreach (var common in new[] { Path.Combine(libraryPath, "common", FirefallFolderName), Path.Combine(libraryPath, SteamAppsFolderName, "common", FirefallFolderName) })
            {
                if (TryCreate(common, out result))
                {
                    return true;
                }
            }
        }

        return false;
    }

    private static List<string> ReadLibraryFolderPaths(string libraryFoldersFile)
    {
        string content;
        try
        {
            content = File.ReadAllText(libraryFoldersFile);
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
        {
            Log.Warning(ex, "Could not read Steam library folders file {Path}", libraryFoldersFile);
            return [];
        }

        // Matches the modern  "path"  "C:\...\steamapps"  form and the legacy
        //  "1"  "C:\...\steamapps"  form used by older libraryfolders.vdf files.
        var valuePattern = new Regex(@"""(?<key>[^""]*)""\s*""(?<value>[^""]+)""", RegexOptions.Compiled);
        var paths = new List<string>();

        foreach (Match match in valuePattern.Matches(content))
        {
            var value = UnescapeVdfPath(match.Groups["value"].Value);
            if (IsSteamAppsPath(value))
            {
                paths.Add(value);
            }
        }

        return paths;
    }

    private static string UnescapeVdfPath(string value)
    {
        // Steam VDF stores back-slashes escaped as double back-slashes.
        return value.Replace(@"\\", @"\");
    }

    private static bool IsSteamAppsPath(string value)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            return false;
        }

        var trimmed = value.TrimEnd(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar);
        if (!trimmed.EndsWith(SteamAppsFolderName, StringComparison.OrdinalIgnoreCase))
        {
            return false;
        }

        return trimmed.Contains(':') || trimmed.StartsWith(Path.DirectorySeparatorChar) || trimmed.StartsWith('/');
    }

    private static IEnumerable<string> EnumerateNearbyCandidateRoots()
    {
        var seen = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

        foreach (var start in new[] { AppContext.BaseDirectory, Directory.GetCurrentDirectory() })
        {
            if (string.IsNullOrWhiteSpace(start))
            {
                continue;
            }

            var directory = new DirectoryInfo(start);
            while (directory != null)
            {
                foreach (var candidate in new[] { directory.FullName, Path.Combine(directory.FullName, FirefallFolderName) })
                {
                    if (seen.Add(candidate))
                    {
                        yield return candidate;
                    }
                }

                directory = directory.Parent;
            }
        }
    }

    private static bool TryCreate(string root, out InstalledFirefall? result)
    {
        result = null;

        if (string.IsNullOrWhiteSpace(root) || !Directory.Exists(root))
        {
            return false;
        }

        var staticDb = Path.Combine(root, "system", "db", "clientdb.sd2");
        if (!File.Exists(staticDb))
        {
            return false;
        }

        result = new InstalledFirefall
        {
            Root = root,
            StaticDBPath = staticDb,
            MapsPath = Path.Combine(root, "system", "maps"),
            AssetDBPath = Path.Combine(root, "system", "assetdb"),
        };

        return true;
    }
}
