using CivVIToolkit.Core.Game;
using CivVIToolkit.Core.Trainer;

namespace CivVIToolkit.Platform.Windows.Trainer;

public sealed class PendingSignatureTrainerEngine : ITrainerEngine
{
    private IReadOnlyList<TrainerFeatureState> _features = BuildStates(TrainerAvailability.NotAttached, "Start Civilization VI to attach.");

    public GameSession? Session { get; private set; }
    public IReadOnlyList<TrainerFeatureState> Features => _features;

    public Task AttachAsync(GameSession session, CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();
        Session = session;
        _features = BuildStates(
            TrainerAvailability.SignaturePending,
            $"Core attached to {session.GraphicsBackend}; a matching signature profile is required before this feature can be enabled.");
        return Task.CompletedTask;
    }

    public Task DetachAsync(CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();
        Session = null;
        _features = BuildStates(TrainerAvailability.NotAttached, "Start Civilization VI to attach.");
        return Task.CompletedTask;
    }

    public Task SetEnabledAsync(string featureId, bool enabled, CancellationToken cancellationToken = default) =>
        throw new NotSupportedException($"Feature '{featureId}' has no verified signature profile yet.");

    public Task ExecuteAsync(string featureId, long? value = null, CancellationToken cancellationToken = default) =>
        throw new NotSupportedException($"Feature '{featureId}' has no verified signature profile yet.");

    public void Dispose()
    {
        Session = null;
    }

    private static IReadOnlyList<TrainerFeatureState> BuildStates(TrainerAvailability availability, string message) =>
        TrainerCatalog.All.Select(feature => new TrainerFeatureState(feature, availability, false, message)).ToArray();
}
