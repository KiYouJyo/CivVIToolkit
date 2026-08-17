namespace CivVIToolkit.Core.Game;

public sealed record GameSession(
    int ProcessId,
    string ExecutablePath,
    string ProcessName,
    GameStore Store,
    GraphicsBackend GraphicsBackend,
    string? InstallDirectory,
    Version? FileVersion,
    bool MatchedKnownInstallation,
    string? FileVersionText = null,
    string? GameCoreModulePath = null)
{
    public bool IsAttached => ProcessId > 0;

    public bool IsGatheringStormCoreLoaded => !string.IsNullOrWhiteSpace(GameCoreModulePath);
}
