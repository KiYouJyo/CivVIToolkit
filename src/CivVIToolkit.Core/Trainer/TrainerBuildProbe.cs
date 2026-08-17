using CivVIToolkit.Core.Game;

namespace CivVIToolkit.Core.Trainer;

public sealed record TrainerBuildProbeSnapshot(
    string ProfileId,
    string GameCoreSha256,
    int LocalPlayerId,
    double Gold,
    double Faith,
    double InfluencePoints,
    IReadOnlyDictionary<string, string> VerifiedSignatures,
    int GoldRaw,
    int FaithRaw,
    int InfluencePointsRaw,
    string PlayerManagerAddress,
    string PlayerAddress,
    string ComponentsAddress,
    string TreasuryAddress,
    string ReligionAddress,
    string InfluenceAddress);

public interface ITrainerBuildProbe
{
    Task<TrainerBuildProbeSnapshot> ProbeAsync(
        GameSession session,
        CancellationToken cancellationToken = default);
}
