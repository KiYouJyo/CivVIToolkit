using CivVIToolkit.Core.Game;
using CivVIToolkit.Core.Trainer;
using CivVIToolkit.Platform.Windows.Trainer;

namespace CivVIToolkit.App;

// Keep MainWindow's existing construction site stable while the exact-build
// acceptance engine is being iterated. Types in the current namespace take
// precedence over the imported platform namespace.
internal sealed class SteamDx12Build1023995TrainerEngine : ITrainerEngine
{
    private readonly SteamDx12Build1023995TrainerEngineV2 _inner;

    public SteamDx12Build1023995TrainerEngine(SteamDx12Build1023995Probe probe) =>
        _inner = new SteamDx12Build1023995TrainerEngineV2(probe);

    public GameSession? Session => _inner.Session;
    public IReadOnlyList<TrainerFeatureState> Features => _inner.Features;
    public Task AttachAsync(GameSession session, CancellationToken cancellationToken = default) => _inner.AttachAsync(session, cancellationToken);
    public Task DetachAsync(CancellationToken cancellationToken = default) => _inner.DetachAsync(cancellationToken);
    public Task SetEnabledAsync(string featureId, bool enabled, CancellationToken cancellationToken = default) => _inner.SetEnabledAsync(featureId, enabled, cancellationToken);
    public Task ExecuteAsync(string featureId, long? value = null, CancellationToken cancellationToken = default) => _inner.ExecuteAsync(featureId, value, cancellationToken);
    public void Dispose() => _inner.Dispose();
}
