namespace CivVIToolkit.Core.Game;

public sealed record GameSession(
    int ProcessId,
    string ExecutablePath,
    string ProcessName,
    GameStore Store,
    GraphicsBackend GraphicsBackend,
    string? InstallDirectory,
    Version? FileVersion,
    bool MatchedKnownInstallation)
{
    public bool IsAttached => ProcessId > 0;
}
