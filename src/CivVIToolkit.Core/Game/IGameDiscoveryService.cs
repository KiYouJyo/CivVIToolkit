namespace CivVIToolkit.Core.Game;

public interface IGameDiscoveryService
{
    Task<IReadOnlyList<GameInstallation>> DiscoverInstallationsAsync(
        CancellationToken cancellationToken = default);
}
