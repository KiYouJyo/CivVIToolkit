namespace CivVIToolkit.Core.Game;

public interface IGameProcessMonitor
{
    GameSession? FindRunningSession(IReadOnlyList<GameInstallation> installations);
}
