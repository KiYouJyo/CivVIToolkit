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
        MainWindow = new MainWindow();
        MainWindow.Activate();
    }

    private static void ApplyLanguagePreference()
    {
        var settings = AppSettingsService.Default.Load();
        ApplicationLanguages.PrimaryLanguageOverride = LanguagePreference.ResolveEffectiveLanguage(
            settings.Language,
            GlobalizationPreferences.Languages);
    }
}
