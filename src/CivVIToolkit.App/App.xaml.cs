using System.Globalization;
using CivVIToolkit.App.Settings;
using CivVIToolkit.Core.Localization;
using Microsoft.UI.Xaml;
using Microsoft.Windows.Globalization;
using Windows.System.UserProfile;

namespace CivVIToolkit.App;

public partial class App : Application
{
    public static Window? MainWindow { get; private set; }

    public App()
    {
        InitializeComponent();
    }

    protected override void OnLaunched(LaunchActivatedEventArgs args)
    {
        ApplyLanguagePreference();
        var window = new MainWindow();
        window.InitializePostConstructionFixes();
        MainWindow = window;
        window.Activate();
    }

    private static void ApplyLanguagePreference()
    {
        var settings = AppSettingsService.Default.Load();
        var language = LanguagePreference.ResolveEffectiveLanguage(
            settings.Language,
            GlobalizationPreferences.Languages);
        ApplicationLanguages.PrimaryLanguageOverride = language;
        var culture = CultureInfo.GetCultureInfo(language);
        CultureInfo.CurrentUICulture = culture;
        CultureInfo.CurrentCulture = culture;
    }
}