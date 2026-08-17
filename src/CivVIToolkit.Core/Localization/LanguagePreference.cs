namespace CivVIToolkit.Core.Localization;

/// <summary>
/// Maps the persisted language setting to supported BCP-47 languages.
/// The stored value "system" means follow the Windows user language.
/// </summary>
public static class LanguagePreference
{
    public const string SystemValue = "system";

    public static IReadOnlyList<string> SupportedBcp47Languages { get; } = ["zh-CN", "ja-JP", "en-US"];

    public static string Normalize(string? storedValue)
    {
        if (string.IsNullOrWhiteSpace(storedValue))
        {
            return SystemValue;
        }

        var trimmed = storedValue.Trim();
        if (string.Equals(trimmed, SystemValue, StringComparison.OrdinalIgnoreCase))
        {
            return SystemValue;
        }

        return SupportedBcp47Languages.FirstOrDefault(
                   supported => string.Equals(supported, trimmed, StringComparison.OrdinalIgnoreCase))
               ?? SystemValue;
    }

    public static string? ResolveOverride(string? storedValue)
    {
        var normalized = Normalize(storedValue);
        return string.Equals(normalized, SystemValue, StringComparison.Ordinal) ? null : normalized;
    }

    public static string ResolveSystemLanguage(IReadOnlyList<string>? systemLanguages)
    {
        var tag = systemLanguages?.FirstOrDefault(language => !string.IsNullOrWhiteSpace(language))?.Trim();
        if (string.IsNullOrEmpty(tag))
        {
            return "zh-CN";
        }

        var exact = SupportedBcp47Languages.FirstOrDefault(language =>
            string.Equals(language, tag, StringComparison.OrdinalIgnoreCase) ||
            tag.StartsWith(language + "-", StringComparison.OrdinalIgnoreCase));
        if (exact is not null)
        {
            return exact;
        }

        if (tag.StartsWith("zh", StringComparison.OrdinalIgnoreCase)) return "zh-CN";
        if (tag.StartsWith("ja", StringComparison.OrdinalIgnoreCase)) return "ja-JP";
        if (tag.StartsWith("en", StringComparison.OrdinalIgnoreCase)) return "en-US";
        return "zh-CN";
    }

    public static string ResolveEffectiveLanguage(string? storedValue, IReadOnlyList<string>? systemLanguages) =>
        ResolveOverride(storedValue) ?? ResolveSystemLanguage(systemLanguages);
}
