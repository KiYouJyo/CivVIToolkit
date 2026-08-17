using CivVIToolkit.Core.Game;

namespace CivVIToolkit.Platform.Windows.Discovery;

internal static class Civ6ExecutableDiscovery
{
    private static readonly string[] CandidateNames =
    [
        "CivilizationVI.exe",
        "CivilizationVI_DX12.exe",
    ];

    public static IReadOnlyList<GameExecutable> Find(string installDirectory)
    {
        if (string.IsNullOrWhiteSpace(installDirectory) || !Directory.Exists(installDirectory))
        {
            return [];
        }

        var results = new Dictionary<string, GameExecutable>(StringComparer.OrdinalIgnoreCase);

        foreach (var name in CandidateNames)
        {
            try
            {
                foreach (var path in Directory.EnumerateFiles(installDirectory, name, SearchOption.AllDirectories))
                {
                    var backend = name.Contains("DX12", StringComparison.OrdinalIgnoreCase)
                        ? GraphicsBackend.DirectX12
                        : GraphicsBackend.DirectX11;
                    results[path] = new GameExecutable(path, backend);
                }
            }
            catch (UnauthorizedAccessException)
            {
                // Keep any already discovered executables. A game directory should normally be fully readable.
            }
            catch (IOException)
            {
                // A transient filesystem failure must not make the entire toolkit fail to start.
            }
        }

        return results.Values
            .OrderBy(executable => executable.GraphicsBackend)
            .ToArray();
    }
}
