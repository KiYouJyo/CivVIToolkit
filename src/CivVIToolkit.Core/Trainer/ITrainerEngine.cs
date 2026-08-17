using CivVIToolkit.Core.Game;

namespace CivVIToolkit.Core.Trainer;

public interface ITrainerEngine : IDisposable
{
    GameSession? Session { get; }
    IReadOnlyList<TrainerFeatureState> Features { get; }

    Task AttachAsync(GameSession session, CancellationToken cancellationToken = default);
    Task DetachAsync(CancellationToken cancellationToken = default);
    Task SetEnabledAsync(string featureId, bool enabled, CancellationToken cancellationToken = default);
    Task ExecuteAsync(string featureId, long? value = null, CancellationToken cancellationToken = default);
}
