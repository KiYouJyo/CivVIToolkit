using CivVIToolkit.Core.Game;

namespace CivVIToolkit.Core.Trainer;

public sealed record TrainerSignatureProfile(
    string Id,
    GameStore Store,
    GraphicsBackend GraphicsBackend,
    string FileVersion,
    string? ExecutableSha256,
    IReadOnlyDictionary<string, TrainerSignatureDefinition> Features);

public sealed record TrainerSignatureDefinition(
    string Pattern,
    int MatchOffset = 0,
    string? ExpectedBytes = null,
    string? Notes = null);

public interface ITrainerSignatureProvider
{
    TrainerSignatureProfile? FindProfile(GameSession session);
}
