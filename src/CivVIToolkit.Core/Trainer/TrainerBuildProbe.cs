using CivVIToolkit.Core.Game;

namespace CivVIToolkit.Core.Trainer;

public sealed record TrainerBuildProbeSnapshot(
    string ProfileId,
    string GameCoreSha256,
    int LocalPlayerId,
    double Gold,
    double Faith,
    double InfluencePoints,
    IReadOnlyDictionary<string, string> VerifiedSignatures);

public interface ITrainerBuildProbe
{
    Task<TrainerBuildProbeSnapshot> ProbeAsync(
        GameSession session,
        CancellationToken cancellationToken = default);
}
