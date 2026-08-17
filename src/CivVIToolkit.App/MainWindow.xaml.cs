using System.Text.Json;
using System.Text.Json.Serialization;
using CivVIToolkit.App.Localization;
using CivVIToolkit.App.Settings;
using CivVIToolkit.Core.Game;
using CivVIToolkit.Core.Hotkeys;
using CivVIToolkit.Core.Localization;
using CivVIToolkit.Core.Trainer;
using CivVIToolkit.Platform.Windows.Diagnostics;
using CivVIToolkit.Platform.Windows.Discovery;
using CivVIToolkit.Platform.Windows.Hotkeys;
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
    private static readonly HashSet<string> ExperimentalAcceptanceFeatures =
    [
        "unit.always-upgrade",
        "combat.one-hit-kill",
        "ai.block-production",
    ];

    private static readonly JsonSerializerOptions DiagnosticsJsonOptions = new()
    {
        WriteIndented = true,
        Converters = { new JsonStringEnumConverter() },
    };

    private readonly IGameDiscoveryService _discovery = new WindowsGameDiscoveryService();
    private readonly IGameProcessMonitor _processMonitor = new Civ6ProcessMonitor();
    private readonly IGameRuntimeDiagnosticsService _diagnostics = new GameRuntimeDiagnosticsService();
    private readonly SteamDx12Build1023995Probe _buildProbe;
    private readonly ITrainerEngine _trainer;
    private readonly ILocalizationService _localization = LocalizationService.Default;
    private readonly AppSettingsService _settingsService = AppSettingsService.Default;
    private readonly DispatcherQueueTimer _processTimer;
    private readonly List<IDisposable> _trainerHotkeyRegistrations = [];

    private IReadOnlyList<GameInstallation> _installations = [];
    private GameSession? _session;
    private AppSettings _settings;
    private Win32HotkeyRegistrationService? _hotkeys;
    private bool _refreshInProgress;
    private bool _languageInitializing;
    private bool _trainerActionInProgress;

    public MainWindow()
    {
        InitializeComponent();
        Title = _localization.GetString("AppDisplayName");
        SystemBackdrop = new MicaBackdrop();

        _buildProbe = new SteamDx12Build1023995Probe();
        _trainer = new SteamDx12Build1023995TrainerEngine(_buildProbe);
        _settings = _settingsService.Load();
        RefreshTrainerList();
        InitializeLanguageOptions();
        RootNavigation.SelectedItem = RootNavigation.MenuItems[0];
        InitializeHotkeys();

        _processTimer = DispatcherQueue.CreateTimer();
        _processTimer.Interval = TimeSpan.FromSeconds(2);
        _processTimer.IsRepeating = true;
        _processTimer.Tick += ProcessTimer_Tick;
        _processTimer.Start();

        Closed += MainWindow_Closed;
        _ = RefreshDiscoveryAsync();
    }

    private void InitializeHotkeys()
    {
        var failures = new List<string>();
        try
        {
            var windowHandle = WinRT.Interop.WindowNative.GetWindowHandle(this);
            _hotkeys = new Win32HotkeyRegistrationService(windowHandle);
            foreach (var feature in TrainerCatalog.All)
            {
                var featureId = feature.Id;
                try
                {
                    _trainerHotkeyRegistrations.Add(_hotkeys.Register(
                        ShortcutGesture.Parse(feature.Shortcut),
                        () => DispatcherQueue.TryEnqueue(() => _ = ExecuteTrainerFeatureAsync(featureId))));
                }
                catch (Exception exception)
                {
                    failures.Add($"{feature.Shortcut}: {exception.Message}");
                }
            }
        }
        catch (Exception exception)
        {
            failures.Add(exception.Message);
        }

        if (failures.Count > 0)
        {
            TrainerOperationStatusText.Text = string.Join(" · ", failures.Take(3));
        }
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
        var changed = next?.ProcessId != _session?.ProcessId
            || !string.Equals(next?.GameCoreModulePath, _session?.GameCoreModulePath, StringComparison.OrdinalIgnoreCase);
        _session = next;

        if (changed)
        {
            DiagnosticsStatusText.Text = string.Empty;
            TrainerOperationStatusText.Text = _localization.GetString("Trainer_OperationHint.Text");
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
            var displayVersion = _session.FileVersionText
                ?? _session.FileVersion?.ToString()
                ?? _localization.GetString("Common_UnknownVersion");
            DetectionDetailsText.Text = _localization.GetFormattedString(
                "Status_RunningDetailsFormat",
                _session.ProcessId,
                displayVersion,
                _session.ExecutablePath);

            var availableCount = _trainer.Features.Count(state => state.Availability == TrainerAvailability.Available);
            TrainerStatusText.Text = availableCount == TrainerCatalog.All.Count
                ? _localization.GetString("Trainer_ProfileVerified")
                : _localization.GetFormattedString(
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

        RefreshTrainerList();
    }

    private void RefreshTrainerList()
    {
        var states = _trainer.Features.ToDictionary(state => state.Definition.Id, StringComparer.Ordinal);
        TrainerList.ItemsSource = TrainerCatalog.All.Select(feature =>
        {
            states.TryGetValue(feature.Id, out var state);
            return LocalizedTrainerFeature.From(feature, _localization, state, FormatTrainerState(feature, state));
        }).ToList();
    }

    private string FormatTrainerState(TrainerFeatureDefinition feature, TrainerFeatureState? state)
    {
        if (state is null)
        {
            return _localization.GetString("Trainer_SignaturePending.Text");
        }

        if (state.Availability == TrainerAvailability.Available)
        {
            if (state.IsEnabled)
            {
                return _localization.GetString("Trainer_StateEnabled");
            }

            if (ExperimentalAcceptanceFeatures.Contains(feature.Id))
            {
                return _localization.GetString("Trainer_StateExperimental");
            }

            return feature.Kind == TrainerFeatureKind.ValueAction
                ? _localization.GetString("Trainer_StateActionReady")
                : _localization.GetString("Trainer_StateReady");
        }

        return state.Availability switch
        {
            TrainerAvailability.NotAttached => _localization.GetString("Trainer_StateNotAttached"),
            TrainerAvailability.UnsupportedGameVersion => _localization.GetString("Trainer_StateUnsupported"),
            TrainerAvailability.Error => _localization.GetString("Trainer_StateError"),
            _ => _localization.GetString("Trainer_SignaturePending.Text"),
        };
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

    private async void TrainerList_ItemClick(object sender, ItemClickEventArgs e)
    {
        if (e.ClickedItem is LocalizedTrainerFeature feature)
        {
            await ExecuteTrainerFeatureAsync(feature.Id);
        }
    }

    private async Task ExecuteTrainerFeatureAsync(string featureId)
    {
        if (_trainerActionInProgress || _session is null)
        {
            return;
        }

        var definition = TrainerCatalog.All.FirstOrDefault(feature => feature.Id == featureId);
        var state = _trainer.Features.FirstOrDefault(candidate => candidate.Definition.Id == featureId);
        if (definition is null || state?.Availability != TrainerAvailability.Available)
        {
            TrainerOperationStatusText.Text = state?.StatusMessage ?? _localization.GetString("Trainer_StateUnavailable");
            return;
        }

        _trainerActionInProgress = true;
        try
        {
            TrainerBuildProbeSnapshot? before = null;
            if (definition.Kind == TrainerFeatureKind.ValueAction)
            {
                before = await _buildProbe.ProbeAsync(_session);
                await _trainer.ExecuteAsync(featureId, definition.DefaultValue);
            }
            else
            {
                await _trainer.SetEnabledAsync(featureId, !state.IsEnabled);
            }

            var localized = LocalizedTrainerFeature.From(definition, _localization).DisplayName;
            var updatedState = _trainer.Features.First(candidate => candidate.Definition.Id == featureId);
            if (definition.Kind == TrainerFeatureKind.ValueAction && before is not null)
            {
                var after = await _buildProbe.ProbeAsync(_session);
                TrainerOperationStatusText.Text = featureId switch
                {
                    "player.add-gold" => $"{localized}: {before.Gold:N1} → {after.Gold:N1}",
                    "player.add-influence" => $"{localized}: {before.InfluencePoints:N1} → {after.InfluencePoints:N1}",
                    _ => $"{localized} · {_localization.GetString("Trainer_StateCompleted")}",
                };
            }
            else
            {
                TrainerOperationStatusText.Text = $"{localized} · {_localization.GetString(updatedState.IsEnabled ? "Trainer_StateEnabled" : "Trainer_StateDisabled")}";
            }
        }
        catch (Exception exception)
        {
            TrainerOperationStatusText.Text = exception.Message;
        }
        finally
        {
            _trainerActionInProgress = false;
            RefreshTrainerList();
        }
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
            if (_session.IsGatheringStormCoreLoaded)
            {
                try
                {
                    var trainerProbe = await _buildProbe.ProbeAsync(_session);
                    snapshot = snapshot with { TrainerProbe = trainerProbe };
                }
                catch (Exception exception)
                {
                    snapshot = snapshot with { TrainerProbeError = exception.Message };
                }
            }

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
        foreach (var registration in _trainerHotkeyRegistrations)
        {
            registration.Dispose();
        }
        _trainerHotkeyRegistrations.Clear();
        _hotkeys?.Dispose();
        _trainer.Dispose();
    }

    private sealed record LanguageOption(string Value, string DisplayName);
}
