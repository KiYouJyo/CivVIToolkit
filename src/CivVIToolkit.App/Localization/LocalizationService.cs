using System.Globalization;
using CivVIToolkit.Core.Localization;
using Microsoft.Windows.ApplicationModel.Resources;
using Microsoft.Windows.Globalization;
using Windows.System.UserProfile;

namespace CivVIToolkit.App.Localization;

/// <summary>
/// Central MRT Core backed localization access for dynamic C# text.
/// Supports changing the effective UI language while the app is running.
/// </summary>
public sealed class LocalizationService : ILocalizationService
{
    private readonly object _gate = new();
    private ResourceLoader _resourceLoader;
    private string _currentLanguage;
    private int _switchInProgress;

    public static LocalizationService Default { get; } = new();

    public LocalizationService()
    {
        _resourceLoader = new ResourceLoader();
        _currentLanguage = ResolveCurrentLanguage();
    }

    public string CurrentLanguage
    {
        get
        {
            lock (_gate)
            {
                return _currentLanguage;
            }
        }
    }

    public string GetString(string resourceKey)
    {
        if (string.IsNullOrWhiteSpace(resourceKey))
        {
            return string.Empty;
        }

        try
        {
            ResourceLoader loader;
            lock (_gate)
            {
                loader = _resourceLoader;
            }

            var value = loader.GetString(resourceKey);
            return string.IsNullOrEmpty(value) ? CreatePlaceholder(resourceKey) : value;
        }
        catch
        {
            return CreatePlaceholder(resourceKey);
        }
    }

    public string GetFormattedString(string resourceKey, params object[] arguments)
    {
        var template = GetString(resourceKey);
        try
        {
            return string.Format(CultureInfo.CurrentCulture, template, arguments);
        }
        catch (FormatException)
        {
            return template;
        }
    }

    public async Task<bool> SwitchLanguageAsync(string language, CancellationToken cancellationToken = default)
    {
        if (Interlocked.Exchange(ref _switchInProgress, 1) != 0)
        {
            return false;
        }

        var previousOverride = ApplicationLanguages.PrimaryLanguageOverride;
        var previousCulture = CultureInfo.CurrentCulture;
        var previousUiCulture = CultureInfo.CurrentUICulture;
        var previousLanguage = CurrentLanguage;

        try
        {
            cancellationToken.ThrowIfCancellationRequested();
            var normalized = LanguagePreference.Normalize(language);
            var effective = LanguagePreference.ResolveEffectiveLanguage(
                normalized,
                GlobalizationPreferences.Languages);

            ApplicationLanguages.PrimaryLanguageOverride = effective;
            var culture = CultureInfo.GetCultureInfo(effective);
            CultureInfo.CurrentCulture = culture;
            CultureInfo.CurrentUICulture = culture;

            var replacementLoader = new ResourceLoader();
            lock (_gate)
            {
                _resourceLoader = replacementLoader;
                _currentLanguage = effective;
            }

            await Task.CompletedTask;
            return true;
        }
        catch
        {
            ApplicationLanguages.PrimaryLanguageOverride = previousOverride;
            CultureInfo.CurrentCulture = previousCulture;
            CultureInfo.CurrentUICulture = previousUiCulture;
            lock (_gate)
            {
                _resourceLoader = new ResourceLoader();
                _currentLanguage = previousLanguage;
            }
            return false;
        }
        finally
        {
            Volatile.Write(ref _switchInProgress, 0);
        }
    }

    private static string ResolveCurrentLanguage()
    {
        var overrideLanguage = ApplicationLanguages.PrimaryLanguageOverride;
        return LanguagePreference.ResolveEffectiveLanguage(
            string.IsNullOrWhiteSpace(overrideLanguage) ? LanguagePreference.SystemValue : overrideLanguage,
            GlobalizationPreferences.Languages);
    }

    public static string CreatePlaceholder(string resourceKey) => $"!{resourceKey}!";
}