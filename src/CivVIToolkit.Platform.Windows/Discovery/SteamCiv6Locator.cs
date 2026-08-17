using System.Text.RegularExpressions;
using CivVIToolkit.Core.Game;
using Microsoft.Win32;

namespace CivVIToolkit.Platform.Windows.Discovery;

internal sealed partial class SteamCiv6Locator
{
    private const string Civ6AppId = "289070";

    public IReadOnlyList<GameInstallation> Discover()
    {
        var libraries = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        foreach (var steamRoot in FindSteamRoots())
        {
            libraries.Add(steamRoot);
            AddLibrariesFromVdf(steamRoot, libraries);
        }

        var installs = new Dictionary<string, GameInstallation>(StringComparer.OrdinalIgnoreCase);
        foreach (var libraryRoot in libraries)
        {
            var manifestPath = Path.Combine(libraryRoot, "steamapps", $"appmanifest_{Civ6AppId}.acf");
            if (!File.Exists(manifestPath))
            {
                continue;
            }

            string manifest;
            try
            {
                manifest = File.ReadAllText(manifestPath);
            }
            catch (IOException)
            {
                continue;
            }

            var installDirName = InstallDirRegex().Match(manifest).Groups[1].Value;
            if (string.IsNullOrWhiteSpace(installDirName))
            {
                continue;
            }

            var installDirectory = Path.GetFullPath(Path.Combine(libraryRoot, "steamapps", "common", installDirName));
            if (!Directory.Exists(installDirectory))
            {
                continue;
            }

            installs[installDirectory] = new GameInstallation(
                GameStore.Steam,
                installDirectory,
                Civ6ExecutableDiscovery.Find(installDirectory),
                Civ6AppId,
                "Sid Meier's Civilization VI");
        }

        return installs.Values.ToArray();
    }

    private static IEnumerable<string> FindSteamRoots()
    {
        var values = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

        TryReadRegistryValue(RegistryHive.CurrentUser, RegistryView.Default, @"Software\Valve\Steam", "SteamPath", values);
        TryReadRegistryValue(RegistryHive.LocalMachine, RegistryView.Registry32, @"SOFTWARE\Valve\Steam", "InstallPath", values);
        TryReadRegistryValue(RegistryHive.LocalMachine, RegistryView.Registry64, @"SOFTWARE\WOW6432Node\Valve\Steam", "InstallPath", values);

        foreach (var value in values)
        {
            if (Directory.Exists(value))
            {
                yield return Path.GetFullPath(value);
            }
        }
    }

    private static void TryReadRegistryValue(
        RegistryHive hive,
        RegistryView view,
        string subKey,
        string valueName,
        ISet<string> results)
    {
        try
        {
            using var baseKey = RegistryKey.OpenBaseKey(hive, view);
            using var key = baseKey.OpenSubKey(subKey);
            if (key?.GetValue(valueName) is string path && !string.IsNullOrWhiteSpace(path))
            {
                results.Add(path.Replace('/', Path.DirectorySeparatorChar));
            }
        }
        catch (Exception exception) when (exception is UnauthorizedAccessException or IOException or System.Security.SecurityException)
        {
            // Registry probing is best-effort; another probe can still find Steam.
        }
    }

    private static void AddLibrariesFromVdf(string steamRoot, ISet<string> libraries)
    {
        var path = Path.Combine(steamRoot, "steamapps", "libraryfolders.vdf");
        if (!File.Exists(path))
        {
            return;
        }

        try
        {
            var text = File.ReadAllText(path);
            foreach (Match match in LibraryPathRegex().Matches(text))
            {
                var candidate = match.Groups[1].Value.Replace("\\\\", "\\");
                if (!string.IsNullOrWhiteSpace(candidate) && Directory.Exists(candidate))
                {
                    libraries.Add(Path.GetFullPath(candidate));
                }
            }
        }
        catch (IOException)
        {
            // Ignore a temporarily unavailable Steam metadata file.
        }
    }

    [GeneratedRegex("\\\"installdir\\\"\\s*\\\"([^\\\"]+)\\\"", RegexOptions.IgnoreCase)]
    private static partial Regex InstallDirRegex();

    [GeneratedRegex("\\\"path\\\"\\s*\\\"([^\\\"]+)\\\"", RegexOptions.IgnoreCase)]
    private static partial Regex LibraryPathRegex();
}
