namespace CivVIToolkit.Core.Trainer;

public enum TrainerAvailability
{
    NotAttached,
    SignaturePending,
    Available,
    UnsupportedGameVersion,
    Error,
}

public sealed record TrainerFeatureState(
    TrainerFeatureDefinition Definition,
    TrainerAvailability Availability,
    bool IsEnabled = false,
    string? StatusMessage = null);
