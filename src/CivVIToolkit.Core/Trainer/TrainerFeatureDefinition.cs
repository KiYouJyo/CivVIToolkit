namespace CivVIToolkit.Core.Trainer;

public enum TrainerFeatureKind
{
    Toggle,
    Action,
    ValueAction,
}

public sealed record TrainerFeatureDefinition(
    string Id,
    string Group,
    string DisplayName,
    string Shortcut,
    TrainerFeatureKind Kind,
    long? DefaultValue = null,
    string? Notes = null);
