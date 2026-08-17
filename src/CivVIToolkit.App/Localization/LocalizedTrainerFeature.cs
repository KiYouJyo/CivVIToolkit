using CivVIToolkit.Core.Trainer;

namespace CivVIToolkit.App.Localization;

public sealed record LocalizedTrainerFeature(
    string Id,
    string Group,
    string DisplayName,
    string Shortcut,
    TrainerFeatureKind Kind,
    long? DefaultValue,
    string? Notes,
    string Status,
    bool IsEnabled,
    TrainerAvailability Availability)
{
    public static LocalizedTrainerFeature From(
        TrainerFeatureDefinition definition,
        ILocalizationService localization,
        TrainerFeatureState? state = null,
        string? status = null)
    {
        ArgumentNullException.ThrowIfNull(definition);
        ArgumentNullException.ThrowIfNull(localization);

        var groupKey = definition.Group switch
        {
            "Player" => "TrainerGroup_Player",
            "Units" => "TrainerGroup_Units",
            "Cities" => "TrainerGroup_Cities",
            "AI" => "TrainerGroup_AI",
            "Combat" => "TrainerGroup_Combat",
            _ => string.Empty,
        };

        var group = string.IsNullOrEmpty(groupKey) ? definition.Group : localization.GetString(groupKey);
        var featureKey = "TrainerFeature_" + definition.Id.Replace('.', '_').Replace('-', '_');
        var displayName = localization.GetString(featureKey);
        if (displayName.StartsWith('!') && displayName.EndsWith('!'))
        {
            displayName = definition.DisplayName;
        }

        var availability = state?.Availability ?? TrainerAvailability.SignaturePending;
        return new LocalizedTrainerFeature(
            definition.Id,
            group,
            displayName,
            definition.Shortcut,
            definition.Kind,
            definition.DefaultValue,
            definition.Notes,
            status ?? state?.StatusMessage ?? string.Empty,
            state?.IsEnabled ?? false,
            availability);
    }
}
