using CivVIToolkit.Core.Localization;

namespace CivVIToolkit.App.Settings;

public sealed class AppSettings
{
    public string Language { get; set; } = LanguagePreference.SystemValue;
}
