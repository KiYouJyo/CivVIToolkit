using System.Text.Json;
using System.Text.Json.Serialization;
using CivVIToolkit.App.Localization;
using CivVIToolkit.App.Settings;
using CivVIToolkit.Core.Game;
using CivVIToolkit.Core.Localization;
using CivVIToolkit.Core.Trainer;
using CivVIToolkit.Platform.Windows.Diagnostics;
using CivVIToolkit.Platform.Windows.Discovery;
using CivVIToolkit.Platform.Windows.Processes;
using CivVIToolkit.Platform.Windows.Trainer;
using Microsoft.UI.Dispatching;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Media;
using Windows.ApplicationModel.DataTransfer;

namespace CivVIToolkit.App;

public sealed partial class MainWindow : Window
{
    private static readonly JsonSerializerOptions DiagnosticsJsonOptions = new()
    {
        WriteIndented = true,
        Converters = { new JsonStringEnumConverter() },
    };

    private readonly IGameDiscoveryService _discovery = new WindowsGameDiscoveryService();
    private readonly IGameProcessMonitor _processMonitor = new Civ6ProcessMonitor();
    private readonly IGameRuntimeDiagnosticsService _diagnostics = new GameRuntimeDiagnosticsService();
    private readonly ITrainerEngine _trainer = new PendingSignatureTrainerEngine();
    private readonly ILocalizationService _localization = LocalizationService.Default;
    private readonly AppSettingsService _settingsService = AppSettingsService.Default;
    private readonly DispatcherQueueTimer _processTimer;

    private IReadOnlyList<GameInstallation> _installations = [];
    private GameSession? _session;
    private AppSettings _settings;
    private bool _refreshInProgress;
    private bool _languageInitializing;

    public MainWindow()
    {
        InitializeComponent();
        Title = _localization.GetString("AppDisplayName");
        SystemBackdrop = new MicaBackdrop();

        _settings = _settingsService.Load();
        TrainerList.ItemsSource = TrainerCatalog.All.Select(feature => LocalizedTrainerFeature.From(feature, _localization)).ToList();
        InitializeLanguageOptions();
        RootNavigation.SelectedItem = RootNavigation.MenuItems[0];

        _processTimer = DispatcherQueue.CreateTimer();
        _processTimer.Interval = TimeSpan.FromSeconds(2);
        _processTimer.IsRepeating = true;
        _processTimer.Tick += ProcessTimer_Tick;
        _processTimer.Start();

        Closed += MainWindow_Closed;
        _ = RefreshDiscoveryAsync();
    }

    private void InitializeLanguageOptions()
    {
        _languageInitializing = true;
        try
        {
            var options = new[]
            {
                new LanguageOption(LanguagePreference.SystemValue, _localization.GetString("Language_System")),
                new LanguageOption("zh-CN", _localization.GetString("Language_Chinese")),
                new LanguageOption("ja-JP", _localization.GetString("Language_Japanese")),
                new LanguageOption("en-US", _localization.GetString("Language_English")),
            };
            LanguageComboBox.ItemsSource = options;
            var selected = LanguagePreference.Normalize(_settings.Language);
            LanguageComboBox.SelectedItem = options.First(option => option.Value == selected);
        }
        finally
        {
            _languageInitializing = false;
        }
    }

    private async void ProcessTimer_Tick(DispatcherQueueTimer sender, object args)
    {
        await RefreshProcessStateAsync();
    }

    private async Task RefreshDiscoveryAsync()
    {
        if (_refreshInProgress)
        {
            return;
        }

        _refreshInProgress = true;
        RefreshButton.IsEnabled = false;
        try
        {
            _installations = await _discovery.DiscoverInstallationsAsync();
            await RefreshProcessStateAsync();
        }
        catch (Exception exception)
        {
            StatusTitleText.Text = _localization.GetString("Status_DetectionFailed");
            StatusBadgeText.Text = exception.GetType().Name;
            DetectionDetailsText.Text = exception.Message;
        }
        finally
        {
            RefreshButton.IsEnabled = true;
            _refreshInProgress = false;
        }
    }

    private async Task RefreshProcessStateAsync()
    {
        var next = _processMonitor.FindRunningSession(_installations);
        var changed = next?.ProcessId != _session?.ProcessId;
        _session = next;

        if (changed)
        {
            DiagnosticsStatusText.Text = string.Empty;
            if (_session is null)
            {
                await _trainer.DetachAsync();
            }
            else
            {
                await _trainer.AttachAsync(_session);
            }
        }

        RenderDetectionState();
    }

    private void RenderDetectionState()
    {
        DiagnosticsButton.IsEnabled = _session is not null;

        if (_session is not null)
        {
            StatusTitleText.Text = _localization.GetString("Status_RunningTitle");
            StatusBadgeText.Text = $"{StoreLabel(_session.Store)} · {BackendLabel(_session.GraphicsBackend)}";
            DetectionDetailsText.Text = _localization.GetFormattedString(
                "Status_RunningDetailsFormat",
                _session.ProcessId,
                _session.FileVersion?.ToString() ?? _localization.GetString("Common_UnknownVersion"),
                _session.ExecutablePath);
            TrainerStatusText.Text = _localization.GetFormattedString(
                "Trainer_AttachedFormat",
                StoreLabel(_session.Store),
                BackendLabel(_session.GraphicsBackend));
        }
        else if (_installations.Count > 0)
        {
            var stores = string.Join(" + ", _installations.Select(installation => StoreLabel(installation.Store)).Distinct());
            StatusTitleText.Text = _localization.GetString("Status_InstallationDetectedTitle");
            StatusBadgeText.Text = _localization.GetFormattedString("Status_InstallationDetectedBadgeFormat", stores);
            DetectionDetailsText.Text = _localization.GetString("Status_LaunchHint");
            TrainerStatusText.Text = _localization.GetString("Trainer_InstallationFound");
        }
        else
        {
            StatusTitleText.Text = _localization.GetString("Status_NotDetectedTitle");
            StatusBadgeText.Text = _localization.GetString("Status_ScanCompleted");
            DetectionDetailsText.Text = _localization.GetString("Status_NotDetectedHint");
            TrainerStatusText.Text = _localization.GetString("Trainer_WaitingSupported");
        }

        InstallationsText.Text = _installations.Count == 0
            ? _localization.GetString("Status_NoMetadata")
            : string.Join("\n", _installations.Select(FormatInstallation));
    }

    private string FormatInstallation(GameInstallation installation)
    {
        var renderers = installation.Executables.Count == 0
            ? _localization.GetString("Installation_ExecutablesUnresolved")
            : string.Join(", ", installation.Executables.Select(executable => BackendLabel(executable.GraphicsBackend)).Distinct());
        return $"{StoreLabel(installation.Store)} · {renderers} · {installation.InstallDirectory}";
    }

    private string StoreLabel(GameStore store) => store switch
    {
        GameStore.Steam => "Steam",
        GameStore.EpicGames => "Epic Games",
        _ => _localization.GetString("Common_UnknownStore"),
    };

    private string BackendLabel(GraphicsBackend backend) => backend switch
    {
        GraphicsBackend.DirectX11 => "DX11",
        GraphicsBackend.DirectX12 => "DX12",
        _ => _localization.GetString("Common_UnknownRenderer"),
    };

    private async void RefreshButton_Click(object sender, RoutedEventArgs e)
    {
        await RefreshDiscoveryAsync();
    }

    private async void DiagnosticsButton_Click(object sender, RoutedEventArgs e)
    {
        if (_session is null)
        {
            return;
        }

        DiagnosticsButton.IsEnabled = false;
        DiagnosticsStatusText.Text = _localization.GetString("Diagnostics_Collecting");
        try
        {
            var snapshot = await _diagnostics.CaptureAsync(_session);
            var json = JsonSerializer.Serialize(snapshot, DiagnosticsJsonOptions);
            var package = new DataPackage();
            package.SetText(json);
            Clipboard.SetContent(package);
            Clipboard.Flush();
            DiagnosticsStatusText.Text = _localization.GetFormattedString(
                "Diagnostics_CopiedFormat",
                snapshot.ExecutableSha256[..12],
                snapshot.ModuleImageSize);
        }
        catch (Exception exception)
        {
            DiagnosticsStatusText.Text = _localization.GetFormattedString("Diagnostics_FailedFormat", exception.Message);
        }
        finally
        {
            DiagnosticsButton.IsEnabled = _session is not null;
        }
    }

    private void LanguageComboBox_SelectionChanged(object sender, SelectionChangedEventArgs e)
    {
        if (_languageInitializing || LanguageComboBox.SelectedItem is not LanguageOption option)
        {
            return;
        }

        _settings.Language = option.Value;
        if (_settingsService.Save(_settings))
        {
            LanguageRestartInfoBar.Severity = InfoBarSeverity.Informational;
            LanguageRestartInfoBar.Title = _localization.GetString("Settings_RestartInfo.Title");
            LanguageRestartInfoBar.Message = _localization.GetString("Settings_RestartInfo.Message");
        }
        else
        {
            LanguageRestartInfoBar.Severity = InfoBarSeverity.Error;
            LanguageRestartInfoBar.Title = _localization.GetString("Settings_SaveFailedTitle");
            LanguageRestartInfoBar.Message = _localization.GetString("Settings_SaveFailedMessage");
        }
        LanguageRestartInfoBar.IsOpen = true;
    }

    private void RootNavigation_SelectionChanged(NavigationView sender, NavigationViewSelectionChangedEventArgs args)
    {
        if (args.SelectedItem is not NavigationViewItem item || item.Tag is not string tag)
        {
            return;
        }

        OverviewPanel.Visibility = tag == "overview" ? Visibility.Visible : Visibility.Collapsed;
        TrainerPanel.Visibility = tag == "trainer" ? Visibility.Visible : Visibility.Collapsed;
        SettingsPanel.Visibility = tag == "settings" ? Visibility.Visible : Visibility.Collapsed;
        PlaceholderPanel.Visibility = tag is "saves" or "maps" or "mods" or "launcher" ? Visibility.Visible : Visibility.Collapsed;

        if (PlaceholderPanel.Visibility == Visibility.Visible)
        {
            var (titleKey, descriptionKey) = tag switch
            {
                "saves" => ("Module_Saves_Title", "Module_Saves_Description"),
                "maps" => ("Module_Maps_Title", "Module_Maps_Description"),
                "mods" => ("Module_Mods_Title", "Module_Mods_Description"),
                _ => ("Module_Launcher_Title", "Module_Launcher_Description"),
            };
            PlaceholderTitleText.Text = _localization.GetString(titleKey);
            PlaceholderDescriptionText.Text = _localization.GetString(descriptionKey);
        }
    }

    private void MainWindow_Closed(object sender, WindowEventArgs args)
    {
        _processTimer.Stop();
        _trainer.Dispose();
    }

    private sealed record LanguageOption(string Value, string DisplayName);
}
