namespace CivVIToolkit.Core.Game;

public sealed record GameModuleDiagnostics(
    string Name,
    string Path,
    string? FileVersion,
    string Sha256,
    string BaseAddress,
    int ImageSize,
    string? SymbolMapPath);

public sealed record GameRuntimeDiagnostics(
    DateTimeOffset CapturedAtUtc,
    int ProcessId,
    GameStore Store,
    GraphicsBackend GraphicsBackend,
    string ExecutablePath,
    string? FileVersion,
    string ExecutableSha256,
    string ModuleBaseAddress,
    int ModuleImageSize,
    string? InstallDirectory,
    bool MatchedKnownInstallation,
    GameModuleDiagnostics? GameCoreModule = null);

public interface IGameRuntimeDiagnosticsService
{
    Task<GameRuntimeDiagnostics> CaptureAsync(
        GameSession session,
        CancellationToken cancellationToken = default);
}
