#nullable enable
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.RegularExpressions;
using Microsoft.Win32;
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

    private static readonly ILogger Log = Serilog.Log.ForContext(typeof(FirefallInstallLocator));

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

        // 3. Common standalone install locations.
        foreach (var candidate in EnumerateStandaloneInstallRoots())
        {
            if (TryCreate(candidate, out var standalone))
            {
                Log.Information("Detected Firefall installation at {Path}", standalone!.Root);
                return standalone;
            }
        }

        // 4. Search around the executable and the working directory.
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

        // Steam is not always installed under Program Files; the registry knows the real location.
        if (OperatingSystem.IsWindows())
        {
            AddRegistryValue(candidates, Registry.CurrentUser, @"Software\Valve\Steam", "SteamPath");
            AddRegistryValue(candidates, Registry.LocalMachine, @"SOFTWARE\WOW6432Node\Valve\Steam", "InstallPath");
            AddRegistryValue(candidates, Registry.LocalMachine, @"SOFTWARE\Valve\Steam", "InstallPath");
        }

        AddSpecialFolder(candidates, Environment.SpecialFolder.ProgramFilesX86, "Steam");
        AddSpecialFolder(candidates, Environment.SpecialFolder.ProgramFiles, "Steam");

        return candidates.Where(Directory.Exists).Distinct(StringComparer.OrdinalIgnoreCase);
    }

    private static IEnumerable<string> EnumerateStandaloneInstallRoots()
    {
        var candidates = new List<string>();

        AddSpecialFolder(candidates, Environment.SpecialFolder.ProgramFiles, FirefallFolderName);
        AddSpecialFolder(candidates, Environment.SpecialFolder.ProgramFilesX86, FirefallFolderName);

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

    private static void AddRegistryValue(ICollection<string> list, RegistryKey rootKey, string subKey, string valueName)
    {
        try
        {
            using var key = rootKey.OpenSubKey(subKey);
            if (key?.GetValue(valueName) is string value && !string.IsNullOrWhiteSpace(value))
            {
                list.Add(value);
            }
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException or ArgumentException)
        {
            // The registry key may be missing or not readable; ignore it.
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

        // Steam keeps the library list under "steamapps" and, in older versions, under "config".
        var libraryFoldersFiles = new[]
        {
            Path.Combine(steamRoot, SteamAppsFolderName, "libraryfolders.vdf"),
            Path.Combine(steamRoot, "config", "libraryfolders.vdf"),
        };

        foreach (var libraryFoldersFile in libraryFoldersFiles.Where(File.Exists).Distinct(StringComparer.OrdinalIgnoreCase))
        {
            foreach (var libraryPath in ReadLibraryFolderPaths(libraryFoldersFile))
            {
                // libraryfolders.vdf stores the library root (e.g. "D:\SteamLibrary");
                // older files and third-party tools sometimes store the "steamapps" folder itself.
                var commonFolders = new[]
                {
                    Path.Combine(libraryPath, SteamAppsFolderName, "common", FirefallFolderName),
                    Path.Combine(libraryPath, "common", FirefallFolderName),
                };

                foreach (var common in commonFolders.Distinct(StringComparer.OrdinalIgnoreCase))
                {
                    if (TryCreate(common, out result))
                    {
                        return true;
                    }
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

        // Matches the modern  "path"  "D:\SteamLibrary"  form and the legacy
        //  "1"  "D:\SteamLibrary"  form used by older libraryfolders.vdf files.
        // Values that do not look like absolute paths (labels, app ids, sizes, ...)
        // are skipped; library paths point at the library root, not at "steamapps".
        var valuePattern = new Regex(@"""(?<key>[^""]*)""\s*""(?<value>[^""]+)""", RegexOptions.Compiled);
        var paths = new List<string>();

        foreach (Match match in valuePattern.Matches(content))
        {
            var value = UnescapeVdfPath(match.Groups["value"].Value);
            if (LooksLikeAbsolutePath(value))
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

    private static bool LooksLikeAbsolutePath(string value)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            return false;
        }

        // Windows drive paths ("C:\SteamLibrary"), UNC paths ("\\server\share")
        // and Unix absolute paths ("/home/user/.steam/steam").
        return value.Contains(':') ||
               value.StartsWith(Path.DirectorySeparatorChar) ||
               value.StartsWith(Path.AltDirectorySeparatorChar);
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
