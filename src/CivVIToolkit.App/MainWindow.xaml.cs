using System.Diagnostics;
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

    private static readonly IReadOnlyDictionary<string, string[]> TrainerCategoryIds =
        new Dictionary<string, string[]>(StringComparer.Ordinal)
        {
            ["resources"] =
            [
                "player.unlimited-gold",
                "player.unlimited-faith",
                "player.add-influence",
                "player.unlimited-resources",
                "player.add-gold",
                "ai.zero-gold",
                "ai.zero-faith",
                "ai.zero-influence",
                "ai.zero-resources",
            ],
            ["science"] =
            [
                "player.instant-research",
                "player.instant-civic",
                "ai.block-research",
                "ai.block-civic",
            ],
            ["units"] =
            [
                "unit.unlimited-movement",
                "unit.unlimited-health",
                "unit.always-upgrade",
                "ai.block-movement",
                "combat.one-hit-kill",
            ],
            ["cities"] =
            [
                "city.instant-production",
                "city.max-population",
                "unit.unlimited-builder-charges",
                "ai.block-production",
            ],
        };

    private readonly IGameDiscoveryService _discovery = new WindowsGameDiscoveryService();
    private readonly IGameProcessMonitor _processMonitor = new Civ6ProcessMonitor();
    private readonly IGameRuntimeDiagnosticsService _diagnostics = new GameRuntimeDiagnosticsService();
    private readonly ITrainerEngine _trainer = new PendingSignatureTrainerEngine();
    private readonly ILocalizationService _localization = LocalizationService.Default;
    private readonly AppSettingsService _settingsService = AppSettingsService.Default;
    private readonly DispatcherQueueTimer _processTimer;

    private IReadOnlyList<GameInstallation> _installations = [];
    private IReadOnlyList<LocalizedTrainerFeature> _trainerFeatures = [];
    private GameSession? _session;
    private AppSettings _settings;
    private CompactTrainerWindow? _compactTrainerWindow;
    private bool _refreshInProgress;
    private bool _languageInitializing;
    private bool _sidebarCollapsed;
    private string _activePage = "overview";
    private string _trainerCategory = "resources";

    public MainWindow()
    {
        InitializeComponent();
        Title = _localization.GetString("AppDisplayName");
        SystemBackdrop = new MicaBackdrop();
        ExtendsContentIntoTitleBar = true;
        SetTitleBar(TitleBarDragRegion);

        _settings = _settingsService.Load();
        _trainerFeatures = TrainerCatalog.All
            .Select(feature => LocalizedTrainerFeature.From(feature, _localization))
            .ToList();
        InitializeLanguageOptions();
        InitializeVersionText();
        ApplyTrainerFilter();
        ShowPage("overview");

        _processTimer = DispatcherQueue.CreateTimer();
        _processTimer.Interval = TimeSpan.FromSeconds(2);
        _processTimer.IsRepeating = true;
        _processTimer.Tick += ProcessTimer_Tick;
        _processTimer.Start();

        Closed += MainWindow_Closed;
        _ = RefreshDiscoveryAsync();
    }

    private void InitializeVersionText()
    {
        var version = typeof(MainWindow).Assembly.GetName().Version;
        AppVersionText.Text = version is null
            ? "v0.3.0"
            : $"v{version.Major}.{version.Minor}.{version.Build}";
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
            LaunchStatusText.Text = $"检测失败：{exception.Message}";
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
            DiagnosticsPageResultText.Text = string.Empty;
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
        var hasSession = _session is not null;
        DiagnosticsButton.IsEnabled = hasSession;
        DiagnosticsPageCopyButton.IsEnabled = hasSession;
        OverviewStatusDot.Opacity = hasSession ? 1 : 0.35;
        TitleStatusDot.Opacity = hasSession ? 1 : 0.35;

        if (_session is not null)
        {
            var displayVersion = _session.FileVersionText
                ?? _session.FileVersion?.ToString()
                ?? _localization.GetString("Common_UnknownVersion");

            StatusTitleText.Text = _localization.GetString("Status_RunningTitle");
            StatusBadgeText.Text = $"{Path.GetFileName(_session.ExecutablePath)} · PID {_session.ProcessId} · {StoreLabel(_session.Store)}";
            DetectionDetailsText.Text = _session.ExecutablePath;
            TrainerStatusText.Text = "按功能分组管理第一版 22 项修改。当前进程已连接，但写入签名仍需完成运行时验收。";

            OverviewStoreText.Text = StoreLabel(_session.Store);
            OverviewRendererText.Text = BackendLabel(_session.GraphicsBackend);
            OverviewVersionText.Text = displayVersion;
            OverviewProcessText.Text = $"PID {_session.ProcessId}";
            TitleStatusText.Text = "已连接";
            DiagnosticsPageStateText.Text = "已连接 Civilization VI";
            DiagnosticsPageDetailsText.Text = BuildDiagnosticsSummary(_session, displayVersion);
        }
        else if (_installations.Count > 0)
        {
            var stores = string.Join(" + ", _installations.Select(installation => StoreLabel(installation.Store)).Distinct());
            var renderers = string.Join(" + ", _installations
                .SelectMany(installation => installation.Executables)
                .Select(executable => BackendLabel(executable.GraphicsBackend))
                .Distinct());

            StatusTitleText.Text = _localization.GetString("Status_InstallationDetectedTitle");
            StatusBadgeText.Text = _localization.GetFormattedString("Status_InstallationDetectedBadgeFormat", stores);
            DetectionDetailsText.Text = _localization.GetString("Status_LaunchHint");
            TrainerStatusText.Text = "按功能分组管理第一版 22 项修改。启动 Civilization VI 后会自动连接只读 Trainer Core。";

            OverviewStoreText.Text = stores;
            OverviewRendererText.Text = string.IsNullOrWhiteSpace(renderers) ? "—" : renderers;
            OverviewVersionText.Text = "未运行";
            OverviewProcessText.Text = "—";
            TitleStatusText.Text = "已检测安装";
            DiagnosticsPageStateText.Text = "已检测游戏安装，当前未运行";
            DiagnosticsPageDetailsText.Text = string.Join("\n", _installations.Select(FormatInstallation));
        }
        else
        {
            StatusTitleText.Text = _localization.GetString("Status_NotDetectedTitle");
            StatusBadgeText.Text = _localization.GetString("Status_ScanCompleted");
            DetectionDetailsText.Text = _localization.GetString("Status_NotDetectedHint");
            TrainerStatusText.Text = "按功能分组管理第一版 22 项修改。当前正在等待受支持的 Civilization VI 安装与进程。";

            OverviewStoreText.Text = "—";
            OverviewRendererText.Text = "—";
            OverviewVersionText.Text = "—";
            OverviewProcessText.Text = "—";
            TitleStatusText.Text = "未连接";
            DiagnosticsPageStateText.Text = "当前没有运行中的 Civilization VI";
            DiagnosticsPageDetailsText.Text = "尚未检测到 Steam 或 Epic Games 安装。";
        }

        InstallationsText.Text = _installations.Count == 0
            ? _localization.GetString("Status_NoMetadata")
            : string.Join("\n", _installations.Select(FormatInstallation));

        RenderLauncherState();
    }

    private string BuildDiagnosticsSummary(GameSession session, string displayVersion)
    {
        var core = session.IsGatheringStormCoreLoaded
            ? session.GameCoreModulePath
            : "Gathering Storm GameCore 尚未载入";
        return $"PID: {session.ProcessId}\nStore: {StoreLabel(session.Store)}\nRenderer: {BackendLabel(session.GraphicsBackend)}\nVersion: {displayVersion}\nExecutable: {session.ExecutablePath}\nGameCore: {core}";
    }

    private void RenderLauncherState()
    {
        var firstInstallation = _installations.FirstOrDefault();
        if (firstInstallation is null)
        {
            LaunchStoreText.Text = "尚未检测到游戏安装";
            LaunchPathText.Text = "点击右上角“重新扫描”以检测 Steam / Epic Games。";
            LaunchDx11Button.IsEnabled = false;
            LaunchDx12Button.IsEnabled = false;
            LaunchStatusText.Text = "未检测到可启动目标";
            return;
        }

        LaunchStoreText.Text = $"{StoreLabel(firstInstallation.Store)} · Civilization VI";
        LaunchPathText.Text = firstInstallation.InstallDirectory;
        LaunchDx11Button.IsEnabled = _session is null && HasExecutable(GraphicsBackend.DirectX11);
        LaunchDx12Button.IsEnabled = _session is null && HasExecutable(GraphicsBackend.DirectX12);

        if (_session is not null)
        {
            LaunchStatusText.Text = "游戏正在运行 · 快捷启动已暂时锁定以避免重复进程";
        }
        else if (!LaunchDx11Button.IsEnabled && !LaunchDx12Button.IsEnabled)
        {
            LaunchStatusText.Text = "已找到安装目录，但尚未解析到 DX11 / DX12 可执行文件";
        }
        else
        {
            LaunchStatusText.Text = "✓ 启动目标已就绪";
        }
    }

    private bool HasExecutable(GraphicsBackend backend) =>
        _installations.Any(installation => installation.Executables.Any(executable => executable.GraphicsBackend == backend));

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
            ShowPage("diagnostics");
            return;
        }

        await CopyDiagnosticsAsync();
    }

    private async void DiagnosticsPageCopyButton_Click(object sender, RoutedEventArgs e)
    {
        await CopyDiagnosticsAsync();
    }

    private async Task CopyDiagnosticsAsync()
    {
        if (_session is null)
        {
            return;
        }

        DiagnosticsButton.IsEnabled = false;
        DiagnosticsPageCopyButton.IsEnabled = false;
        DiagnosticsStatusText.Text = _localization.GetString("Diagnostics_Collecting");
        DiagnosticsPageResultText.Text = "正在收集只读诊断信息…";
        try
        {
            var snapshot = await _diagnostics.CaptureAsync(_session);
            var json = JsonSerializer.Serialize(snapshot, DiagnosticsJsonOptions);
            var package = new DataPackage();
            package.SetText(json);
            Clipboard.SetContent(package);
            Clipboard.Flush();

            var message = _localization.GetFormattedString(
                "Diagnostics_CopiedFormat",
                snapshot.ExecutableSha256[..12],
                snapshot.ModuleImageSize);
            DiagnosticsStatusText.Text = message;
            DiagnosticsPageResultText.Text = message;
        }
        catch (Exception exception)
        {
            var message = _localization.GetFormattedString("Diagnostics_FailedFormat", exception.Message);
            DiagnosticsStatusText.Text = message;
            DiagnosticsPageResultText.Text = message;
        }
        finally
        {
            DiagnosticsButton.IsEnabled = _session is not null;
            DiagnosticsPageCopyButton.IsEnabled = _session is not null;
        }
    }

    private void LaunchDx11Button_Click(object sender, RoutedEventArgs e)
    {
        LaunchGame(GraphicsBackend.DirectX11);
    }

    private void LaunchDx12Button_Click(object sender, RoutedEventArgs e)
    {
        LaunchGame(GraphicsBackend.DirectX12);
    }

    private void LaunchGame(GraphicsBackend backend)
    {
        if (_session is not null)
        {
            LaunchStatusText.Text = "Civilization VI 已在运行。";
            return;
        }

        foreach (var installation in _installations)
        {
            var executable = installation.Executables.FirstOrDefault(candidate => candidate.GraphicsBackend == backend);
            if (executable is null)
            {
                continue;
            }

            try
            {
                var workingDirectory = Path.GetDirectoryName(executable.Path);
                Process.Start(new ProcessStartInfo
                {
                    FileName = executable.Path,
                    WorkingDirectory = string.IsNullOrWhiteSpace(workingDirectory) ? installation.InstallDirectory : workingDirectory,
                    UseShellExecute = true,
                });
                LaunchStatusText.Text = $"已请求启动 {StoreLabel(installation.Store)} · {BackendLabel(backend)}。";
            }
            catch (Exception exception)
            {
                LaunchStatusText.Text = $"启动失败：{exception.Message}";
            }
            return;
        }

        LaunchStatusText.Text = $"未找到 {BackendLabel(backend)} 可执行文件。";
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

    private void SidebarToggleButton_Click(object sender, RoutedEventArgs e)
    {
        _sidebarCollapsed = !_sidebarCollapsed;
        SidebarColumn.Width = new GridLength(_sidebarCollapsed ? 64 : 220);

        var labelVisibility = _sidebarCollapsed ? Visibility.Collapsed : Visibility.Visible;
        HomeNavLabel.Visibility = labelVisibility;
        TrainerNavLabel.Visibility = labelVisibility;
        SavesNavLabel.Visibility = labelVisibility;
        ModsNavLabel.Visibility = labelVisibility;
        EncyclopediaNavLabel.Visibility = labelVisibility;
        DiagnosticsNavLabel.Visibility = labelVisibility;
        SettingsNavLabel.Visibility = labelVisibility;
    }

    private void NavigationButton_Click(object sender, RoutedEventArgs e)
    {
        if (sender is Button { Tag: string tag })
        {
            ShowPage(tag);
        }
    }

    private void ShowPage(string tag)
    {
        _activePage = tag;
        OverviewPanel.Visibility = tag == "overview" ? Visibility.Visible : Visibility.Collapsed;
        TrainerPanel.Visibility = tag == "trainer" ? Visibility.Visible : Visibility.Collapsed;
        DiagnosticsPanel.Visibility = tag == "diagnostics" ? Visibility.Visible : Visibility.Collapsed;
        SettingsPanel.Visibility = tag == "settings" ? Visibility.Visible : Visibility.Collapsed;
        PlaceholderPanel.Visibility = tag is "saves" or "mods" or "encyclopedia" ? Visibility.Visible : Visibility.Collapsed;

        if (PlaceholderPanel.Visibility == Visibility.Visible)
        {
            (PlaceholderTitleText.Text, PlaceholderDescriptionText.Text) = tag switch
            {
                "saves" => ("存档管理", "集中查看、备份、复制与恢复 Civilization VI 存档。当前版本仅保留入口。"),
                "mods" => ("Mod 管理", "浏览、启停与组织 Civilization VI Mod。当前版本仅保留入口。"),
                _ => ("百科", "文明、领袖、单位、建筑与规则资料库。当前版本仅保留入口。"),
            };
        }

        UpdateNavigationVisuals();
    }

    private void UpdateNavigationVisuals()
    {
        var selectedBrush = (Brush)RootShell.Resources["ShellAccentSubtleBrush"];
        var selectedTextBrush = (Brush)RootShell.Resources["ShellAccentBrush"];
        var normalTextBrush = (Brush)RootShell.Resources["ShellSecondaryTextBrush"];

        foreach (var button in NavigationButtons())
        {
            var selected = string.Equals(button.Tag as string, _activePage, StringComparison.Ordinal);
            button.Background = selected ? selectedBrush : null;
            button.Foreground = selected ? selectedTextBrush : normalTextBrush;
            button.Opacity = 1;
        }
    }

    private IEnumerable<Button> NavigationButtons()
    {
        yield return HomeNavButton;
        yield return TrainerNavButton;
        yield return SavesNavButton;
        yield return ModsNavButton;
        yield return EncyclopediaNavButton;
        yield return DiagnosticsNavButton;
        yield return SettingsNavButton;
    }

    private void TrainerCategoryButton_Click(object sender, RoutedEventArgs e)
    {
        if (sender is Button { Tag: string category } && TrainerCategoryIds.ContainsKey(category))
        {
            _trainerCategory = category;
            ApplyTrainerFilter();
        }
    }

    private void TrainerSearchBox_TextChanged(object sender, TextChangedEventArgs e)
    {
        ApplyTrainerFilter();
    }

    private void ApplyTrainerFilter()
    {
        if (!TrainerCategoryIds.TryGetValue(_trainerCategory, out var ids))
        {
            ids = TrainerCategoryIds["resources"];
            _trainerCategory = "resources";
        }

        var allowed = ids.ToHashSet(StringComparer.Ordinal);
        var query = TrainerSearchBox?.Text?.Trim() ?? string.Empty;
        var filtered = _trainerFeatures
            .Where(feature => allowed.Contains(feature.Id))
            .Where(feature => string.IsNullOrWhiteSpace(query)
                || feature.DisplayName.Contains(query, StringComparison.OrdinalIgnoreCase)
                || feature.Shortcut.Contains(query, StringComparison.OrdinalIgnoreCase)
                || feature.Id.Contains(query, StringComparison.OrdinalIgnoreCase)
                || feature.Scope.Contains(query, StringComparison.OrdinalIgnoreCase))
            .ToList();

        TrainerList.ItemsSource = filtered;
        var (title, description) = _trainerCategory switch
        {
            "resources" => ("资源与经济", "玩家资源、即时增量，以及对应的 AI 资源控制。"),
            "science" => ("科技与文化", "研究、市政，以及对应的 AI 科技与文化进度控制。"),
            "units" => ("单位与战斗", "单位移动、生命、升级、AI 移动与战斗相关能力。"),
            _ => ("城市与建造", "城市生产、人口、建造者次数与 AI 生产控制。"),
        };
        TrainerCategoryTitleText.Text = title;
        TrainerCategoryDescriptionText.Text = description;
        TrainerCategoryCountText.Text = $"{filtered.Count} 项";
        UpdateTrainerCategoryVisuals();
    }

    private void UpdateTrainerCategoryVisuals()
    {
        var selectedBrush = (Brush)RootShell.Resources["ShellAccentSubtleBrush"];
        var selectedTextBrush = (Brush)RootShell.Resources["ShellAccentBrush"];
        var normalTextBrush = (Brush)RootShell.Resources["ShellSecondaryTextBrush"];

        foreach (var button in TrainerCategoryButtons())
        {
            var selected = string.Equals(button.Tag as string, _trainerCategory, StringComparison.Ordinal);
            button.Background = selected ? selectedBrush : null;
            button.Foreground = selected ? selectedTextBrush : normalTextBrush;
        }
    }

    private IEnumerable<Button> TrainerCategoryButtons()
    {
        yield return ResourcesCategoryButton;
        yield return ScienceCategoryButton;
        yield return UnitsCategoryButton;
        yield return CitiesCategoryButton;
    }

    private void CompactTrainerButton_Click(object sender, RoutedEventArgs e)
    {
        if (_compactTrainerWindow is null)
        {
            _compactTrainerWindow = new CompactTrainerWindow(_trainerFeatures);
            _compactTrainerWindow.Closed += (_, _) => _compactTrainerWindow = null;
        }

        _compactTrainerWindow.Activate();
    }

    private void MainWindow_Closed(object sender, WindowEventArgs args)
    {
        _processTimer.Stop();
        _trainer.Dispose();
        _compactTrainerWindow?.Close();
        _compactTrainerWindow = null;
    }

    private sealed record LanguageOption(string Value, string DisplayName);
}
