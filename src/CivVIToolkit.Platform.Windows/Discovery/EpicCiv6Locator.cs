using System.Text.Json;
using CivVIToolkit.Core.Game;

namespace CivVIToolkit.Platform.Windows.Discovery;

internal sealed class EpicCiv6Locator
{
    public IReadOnlyList<GameInstallation> Discover()
    {
        var manifestsDirectory = Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.CommonApplicationData),
            "Epic",
            "EpicGamesLauncher",
            "Data",
            "Manifests");

        if (!Directory.Exists(manifestsDirectory))
        {
            return [];
        }

        var results = new Dictionary<string, GameInstallation>(StringComparer.OrdinalIgnoreCase);
        foreach (var manifestPath in Directory.EnumerateFiles(manifestsDirectory, "*.item", SearchOption.TopDirectoryOnly))
        {
            TryReadManifest(manifestPath, results);
        }

        return results.Values.ToArray();
    }

    private static void TryReadManifest(string manifestPath, IDictionary<string, GameInstallation> results)
    {
        try
        {
            using var document = JsonDocument.Parse(File.ReadAllText(manifestPath));
            var root = document.RootElement;
            var displayName = GetString(root, "DisplayName");
            var launchExecutable = GetString(root, "LaunchExecutable");

            var looksLikeCiv6 = displayName?.Contains("Civilization VI", StringComparison.OrdinalIgnoreCase) == true
                || launchExecutable?.Contains("CivilizationVI", StringComparison.OrdinalIgnoreCase) == true;
            if (!looksLikeCiv6)
            {
                return;
            }

            var installLocation = GetString(root, "InstallLocation");
            if (string.IsNullOrWhiteSpace(installLocation) || !Directory.Exists(installLocation))
            {
                return;
            }

            var fullPath = Path.GetFullPath(installLocation);
            var appName = GetString(root, "AppName") ?? GetString(root, "CatalogItemId");
            results[fullPath] = new GameInstallation(
                GameStore.EpicGames,
                fullPath,
                Civ6ExecutableDiscovery.Find(fullPath),
                appName,
                displayName ?? "Sid Meier's Civilization VI");
        }
        catch (Exception exception) when (exception is IOException or JsonException or UnauthorizedAccessException)
        {
            // Epic keeps several manifests here. A single malformed/stale entry should not block discovery.
        }
    }

    private static string? GetString(JsonElement root, string propertyName) =>
        root.TryGetProperty(propertyName, out var property) && property.ValueKind == JsonValueKind.String
            ? property.GetString()
            : null;
}
