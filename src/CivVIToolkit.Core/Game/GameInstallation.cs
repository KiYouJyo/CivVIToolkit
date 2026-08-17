namespace CivVIToolkit.Core.Game;

public sealed record GameInstallation(
    GameStore Store,
    string InstallDirectory,
    IReadOnlyList<GameExecutable> Executables,
    string? StoreAppId = null,
    string? DisplayName = null)
{
    public bool Supports(GraphicsBackend backend) =>
        Executables.Any(executable => executable.GraphicsBackend == backend);
}
