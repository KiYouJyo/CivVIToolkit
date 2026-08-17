using CivVIToolkit.Core.Game;

namespace CivVIToolkit.Platform.Windows.Discovery;

public sealed class WindowsGameDiscoveryService : IGameDiscoveryService
{
    public Task<IReadOnlyList<GameInstallation>> DiscoverInstallationsAsync(
        CancellationToken cancellationToken = default) =>
        Task.Run<IReadOnlyList<GameInstallation>>(() =>
        {
            cancellationToken.ThrowIfCancellationRequested();

            var installations = new Dictionary<string, GameInstallation>(StringComparer.OrdinalIgnoreCase);
            foreach (var installation in new SteamCiv6Locator().Discover())
            {
                installations[installation.InstallDirectory] = installation;
            }

            cancellationToken.ThrowIfCancellationRequested();
            foreach (var installation in new EpicCiv6Locator().Discover())
            {
                installations[installation.InstallDirectory] = installation;
            }

            return installations.Values
                .OrderBy(installation => installation.Store)
                .ThenBy(installation => installation.InstallDirectory, StringComparer.OrdinalIgnoreCase)
                .ToArray();
        }, cancellationToken);
}
