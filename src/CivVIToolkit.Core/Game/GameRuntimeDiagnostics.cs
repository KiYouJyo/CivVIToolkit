namespace CivVIToolkit.Core.Game;

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
    bool MatchedKnownInstallation);

public interface IGameRuntimeDiagnosticsService
{
    Task<GameRuntimeDiagnostics> CaptureAsync(
        GameSession session,
        CancellationToken cancellationToken = default);
}
