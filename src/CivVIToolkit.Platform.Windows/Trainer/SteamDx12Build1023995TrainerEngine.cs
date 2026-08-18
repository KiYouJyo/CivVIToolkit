using CivVIToolkit.Core.Game;
using CivVIToolkit.Core.Trainer;

namespace CivVIToolkit.Platform.Windows.Trainer;

/// <summary>
/// Compatibility facade retained for the app composition root. The actual
/// exact-build implementation lives in the stability-oriented scheduler.
/// </summary>
public sealed class SteamDx12Build1023995TrainerEngine : ITrainerEngine
{
    private readonly SteamDx12Build1023995StableTrainerEngine _inner;

    public SteamDx12Build1023995TrainerEngine(SteamDx12Build1023995Probe probe)
    {
        _inner = new SteamDx12Build1023995StableTrainerEngine(probe);
    }

    public GameSession? Session => _inner.Session;

    public IReadOnlyList<TrainerFeatureState> Features => _inner.Features;

    public Task AttachAsync(GameSession session, CancellationToken cancellationToken = default) =>
        _inner.AttachAsync(session, cancellationToken);

    public Task DetachAsync(CancellationToken cancellationToken = default) =>
        _inner.DetachAsync(cancellationToken);

    public Task SetEnabledAsync(string featureId, bool enabled, CancellationToken cancellationToken = default) =>
        _inner.SetEnabledAsync(featureId, enabled, cancellationToken);

    public Task ExecuteAsync(string featureId, long? value = null, CancellationToken cancellationToken = default) =>
        _inner.ExecuteAsync(featureId, value, cancellationToken);

    public void Dispose() => _inner.Dispose();
}
