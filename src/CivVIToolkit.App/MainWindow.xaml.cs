using System.Diagnostics;
using System.Globalization;
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
using Microsoft.UI.Xaml.Input;
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

    private static readonly IReadOnlyDictionary<string, HashSet<string>> TrainerCategories =
        new Dictionary<string, HashSet<string>>(StringComparer.Ordinal)
        {
            ["resources"] =
            [
                "player.unlimited-gold", "player.unlimited-faith", "player.add-influence",
                "player.unlimited-resources", "player.add-gold", "ai.zero-gold",
                "ai.zero-faith", "ai.zero-influence", "ai.zero-resources",
            ],
            ["science"] =
            [
                "player.instant-research", "player.instant-civic", "ai.block-research", "ai.block-civic",
            ],
            ["units"] =
            [
                "unit.unlimited-movement", "unit.unlimited-health", "unit.always-upgrade",
                "ai.block-movement", "combat.one-hit-kill",
            ],
            ["cities"] =
            [
                "city.instant-production", "city.max-population", "unit.unlimited-builder-charges", "ai.block-production",
            ],
        };

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
    private readonly Dictionary<string, long> _trainerActionValues = TrainerCatalog.All
        .Where(feature => feature.Kind == TrainerFeatureKind.ValueAction)
        .ToDictionary(feature => feature.Id, feature => feature.DefaultValue ?? 1L, StringComparer.Ordinal);

    private IReadOnlyList<GameInstallation> _installations = [];
    private List<LocalizedTrainerFeature> _trainerFeatures = [];
    private GameSession? _session;
    private AppSettings _settings;
    private Win32HotkeyRegistrationService? _hotkeys;
    private CompactTrainerWindow? _compactTrainerWindow;
    private string _trainerCategory = "resources";
    private string? _lastTrainerUiSignature;
    private bool _refreshInProgress;
    private bool _languageInitializing;
    private bool _trainerActionInProgress;

    public MainWindow()
    {
        InitializeComponent();
        TrainerList.ItemTemplate = TrainerTemplateFactory.CreateMainTemplate();
        Title = _localization.GetString("AppDisplayName");
        ExtendsContentIntoTitleBar = true;
        SetTitleBar(AppTitleBar);

        _buildProbe = new SteamDx12Build1023995Probe();
        _trainer = new SteamDx12Build1023995StableTrainerEngine(_buildProbe);
        _settings = _settingsService.Load();

        InitializeLanguageOptions();
        InitializeVersionText();
        RefreshTrainerList(force: true);
        RootNavigation.SelectedItem = HomeNavItem;
        ShowPage("overview");
        InitializeHotkeys();

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
            ? "v0.3.1"
            : $"v{version.Major}.{version.Minor}.{version.Build}";
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

    private async void ProcessTimer_Tick(DispatcherQueueTimer sender, object args) => await RefreshProcessStateAsync();

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
            LaunchStatusText.Text = exception.Message;
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
            TrainerOperationStatusText.Text = UiText(
                "连接支持的单机对局后可使用已验证功能。",
                "対応するシングルプレイのゲームに接続すると、検証済み機能を利用できます。",
                "Connect to a supported single-player match to use verified features.");

            if (_session is null)
            {
                await _trainer.DetachAsync();
            }
            else
            {
                await _trainer.AttachAsync(_session);
            }
            _lastTrainerUiSignature = null;
        }

        RenderDetectionState();
    }

    private void RenderDetectionState()
    {
        var hasSession = _session is not null;
        DiagnosticsPageCopyButton.IsEnabled = hasSession;
        DiagnosticsButton.IsEnabled = true;
        OverviewStatusDot.Opacity = hasSession ? 1 : 0.35;

        if (_session is not null)
        {
            var displayVersion = _session.FileVersionText
                ?? _session.FileVersion?.ToString()
                ?? _localization.GetString("Common_UnknownVersion");

            StatusTitleText.Text = _localization.GetString("Status_RunningTitle");
            StatusBadgeText.Text = $"{StoreLabel(_session.Store)} · {BackendLabel(_session.GraphicsBackend)}";
            DetectionDetailsText.Text = _localization.GetFormattedString(
                "Status_RunningDetailsFormat",
                _session.ProcessId,
                displayVersion,
                _session.ExecutablePath);

            OverviewStoreText.Text = StoreLabel(_session.Store);
            OverviewRendererText.Text = BackendLabel(_session.GraphicsBackend);
            OverviewVersionText.Text = displayVersion;
            OverviewProcessText.Text = _session.ProcessId.ToString(CultureInfo.InvariantCulture);
            DiagnosticsPageStateText.Text = UiText("已连接 Civilization VI", "Civilization VI に接続済み", "Civilization VI connected");
            DiagnosticsPageDetailsText.Text = BuildDiagnosticsSummary(_session, displayVersion);
        }
        else if (_installations.Count > 0)
        {
            var stores = string.Join(" + ", _installations.Select(installation => StoreLabel(installation.Store)).Distinct());
            var renderers = string.Join(" + ", _installations.SelectMany(installation => installation.Executables).Select(executable => BackendLabel(executable.GraphicsBackend)).Distinct());
            StatusTitleText.Text = _localization.GetString("Status_InstallationDetectedTitle");
            StatusBadgeText.Text = _localization.GetFormattedString("Status_InstallationDetectedBadgeFormat", stores);
            DetectionDetailsText.Text = _localization.GetString("Status_LaunchHint");
            OverviewStoreText.Text = stores;
            OverviewRendererText.Text = string.IsNullOrWhiteSpace(renderers) ? "—" : renderers;
            OverviewVersionText.Text = UiText("未运行", "未実行", "Not running");
            OverviewProcessText.Text = "—";
            DiagnosticsPageStateText.Text = UiText("已检测游戏安装，当前未运行", "ゲームを検出しました。現在は未実行です", "Game installation detected; not running");
            DiagnosticsPageDetailsText.Text = string.Join("\n", _installations.Select(FormatInstallation));
        }
        else
        {
            StatusTitleText.Text = _localization.GetString("Status_NotDetectedTitle");
            StatusBadgeText.Text = _localization.GetString("Status_ScanCompleted");
            DetectionDetailsText.Text = _localization.GetString("Status_NotDetectedHint");
            OverviewStoreText.Text = "—";
            OverviewRendererText.Text = "—";
            OverviewVersionText.Text = "—";
            OverviewProcessText.Text = "—";
            DiagnosticsPageStateText.Text = UiText("当前没有运行中的 Civilization VI", "Civilization VI は実行されていません", "Civilization VI is not running");
            DiagnosticsPageDetailsText.Text = UiText("尚未检测到 Steam 或 Epic Games 安装。", "Steam / Epic Games のインストールを検出できませんでした。", "No Steam or Epic Games installation detected.");
        }

        InstallationsText.Text = _installations.Count == 0
            ? _localization.GetString("Status_NoMetadata")
            : string.Join("\n", _installations.Select(FormatInstallation));

        RenderLauncherState();
        RefreshTrainerList();
    }

    private void RefreshTrainerList(bool force = false)
    {
        var states = _trainer.Features.ToDictionary(state => state.Definition.Id, StringComparer.Ordinal);
        var firstBinding = _trainerFeatures.Count == 0;

        if (firstBinding)
        {
            foreach (var feature in TrainerCatalog.All)
            {
                states.TryGetValue(feature.Id, out var state);
                _trainerActionValues.TryGetValue(feature.Id, out var configuredValue);
                var item = LocalizedTrainerFeature.From(
                    feature,
                    _localization,
                    state,
                    FormatTrainerState(feature, state),
                    feature.Kind == TrainerFeatureKind.ValueAction ? configuredValue : null);
                item.ConfigureActions(ExecuteTrainerFeatureAsync, SetTrainerActionValue);
                _trainerFeatures.Add(item);
            }

            ApplyTrainerFilter();
        }
        else
        {
            var byId = _trainerFeatures.ToDictionary(item => item.Id, StringComparer.Ordinal);
            foreach (var feature in TrainerCatalog.All)
            {
                if (!byId.TryGetValue(feature.Id, out var item))
                {
                    continue;
                }

                states.TryGetValue(feature.Id, out var state);
                _trainerActionValues.TryGetValue(feature.Id, out var configuredValue);
                item.UpdateFrom(
                    feature,
                    _localization,
                    state,
                    FormatTrainerState(feature, state),
                    feature.Kind == TrainerFeatureKind.ValueAction ? configuredValue : null);
            }
        }

        UpdateTrainerSummary(_trainerFeatures);
        _compactTrainerWindow?.RefreshStatus();
    }

    private void UpdateTrainerSummary(IReadOnlyList<LocalizedTrainerFeature> items)
    {
        var enabled = items.Count(item => item.IsEnabled);
        var available = items.Count(item => item.Availability == TrainerAvailability.Available);
        HomeTrainerCountText.Text = UiText($"{enabled} 项启用", $"{enabled} 件有効", $"{enabled} enabled");
        TrainerEnabledCountText.Text = UiText($"默认 · {enabled} / 22 已启用", $"既定 · {enabled} / 22 有効", $"Default · {enabled} / 22 enabled");
        TrainerDisableAllButton.IsEnabled = enabled > 0 && !_trainerActionInProgress;

        if (available == TrainerCatalog.All.Count)
        {
            TrainerStatusText.Text = UiText("已验证 Steam / DX12 / Gathering Storm 1023995 精确 Profile。", "Steam / DX12 / Gathering Storm 1023995 の検証済みプロファイルです。", "Verified Steam / DX12 / Gathering Storm 1023995 profile.");
            TrainerSafetyText.Text = UiText("单机模式 · Profile 已验证", "シングルプレイ · プロファイル検証済み", "Single-player · profile verified");
            HomeTrainerSafetyText.Text = UiText("Profile 已验证", "プロファイル検証済み", "Profile verified");
            DiagnosticsWriteStateText.Text = UiText("当前构建已通过精确 Profile 校验", "現在のビルドは精密プロファイル検証済み", "Current build passed exact-profile validation");
        }
        else
        {
            TrainerSafetyText.Text = UiText("单机模式 · 不匹配时拒绝写入", "シングルプレイ · 不一致時は書き込み拒否", "Single-player · fail-closed on mismatch");
            HomeTrainerSafetyText.Text = hasCurrentSessionText();
            DiagnosticsWriteStateText.Text = hasCurrentSessionText();
        }

        string hasCurrentSessionText() => _session is null
            ? UiText("等待游戏", "ゲーム待機中", "Waiting for game")
            : UiText("当前构建尚未通过 Trainer Profile", "現在のビルドは Trainer Profile 未検証", "Current build is not trainer-profile verified");
    }

    private string FormatTrainerState(TrainerFeatureDefinition feature, TrainerFeatureState? state)
    {
        if (state is null)
        {
            return UiText("等待签名", "署名待ち", "Signature pending");
        }

        if (state.Availability == TrainerAvailability.Available)
        {
            if (state.IsEnabled)
            {
                return UiText("已启用", "有効", "Enabled");
            }
            if (ExperimentalAcceptanceFeatures.Contains(feature.Id))
            {
                return UiText("实验性", "実験的", "Experimental");
            }
            return feature.Kind == TrainerFeatureKind.ValueAction
                ? UiText("可执行", "実行可能", "Ready")
                : UiText("可启用", "有効化可能", "Ready");
        }

        return state.Availability switch
        {
            TrainerAvailability.NotAttached => UiText("未连接", "未接続", "Not attached"),
            TrainerAvailability.UnsupportedGameVersion => UiText("版本不支持", "未対応バージョン", "Unsupported"),
            TrainerAvailability.Error => UiText("运行错误", "実行エラー", "Runtime error"),
            _ => UiText("等待签名", "署名待ち", "Signature pending"),
        };
    }

    private void ApplyTrainerFilter()
    {
        if (!TrainerCategories.TryGetValue(_trainerCategory, out var ids))
        {
            ids = TrainerCategories["resources"];
        }

        var query = TrainerSearchBox?.Text?.Trim() ?? string.Empty;
        var filtered = _trainerFeatures
            .Where(item => ids.Contains(item.Id))
            .Where(item => string.IsNullOrEmpty(query)
                || item.DisplayName.Contains(query, StringComparison.CurrentCultureIgnoreCase)
                || item.Shortcut.Contains(query, StringComparison.OrdinalIgnoreCase))
            .ToList();

        TrainerList.ItemsSource = filtered;
        TrainerCategoryCountText.Text = UiText($"{filtered.Count} 项", $"{filtered.Count} 件", $"{filtered.Count} items");
        (TrainerCategoryTitleText.Text, TrainerCategoryDescriptionText.Text) = _trainerCategory switch
        {
            "science" => (UiText("科技与文化", "科学・文化", "Science & Culture"), UiText("研究、市政以及对应的 AI 进度控制。", "研究・社会制度と AI の進捗制御。", "Research, civics, and matching AI progress controls.")),
            "units" => (UiText("单位与战斗", "ユニット・戦闘", "Units & Combat"), UiText("移动、生命、升级与战斗相关修改。", "移動・体力・アップグレード・戦闘関連。", "Movement, health, upgrade, and combat features.")),
            "cities" => (UiText("城市与建造", "都市・生産", "Cities & Production"), UiText("人口、生产和建造者使用次数。", "人口・生産・労働者の使用回数。", "Population, production, and builder charges.")),
            _ => (UiText("资源与经济", "資源・経済", "Resources & Economy"), UiText("玩家资源、即时增量，以及对应的 AI 资源控制。", "プレイヤー資源、即時加算、AI 資源制御。", "Player resources, instant additions, and AI resource controls.")),
        };

        UpdateCategoryButtonState();
    }

    private void UpdateCategoryButtonState()
    {
        ResourcesCategoryButton.Opacity = _trainerCategory == "resources" ? 1 : 0.7;
        ScienceCategoryButton.Opacity = _trainerCategory == "science" ? 1 : 0.7;
        UnitsCategoryButton.Opacity = _trainerCategory == "units" ? 1 : 0.7;
        CitiesCategoryButton.Opacity = _trainerCategory == "cities" ? 1 : 0.7;
    }

    private async void TrainerList_ItemClick(object sender, ItemClickEventArgs e)
    {
        if (e.ClickedItem is LocalizedTrainerFeature feature)
        {
            await ExecuteTrainerFeatureAsync(feature.Id);
        }
    }

    private void TrainerValueInput_ValueChanged(object sender, NumberBoxValueChangedEventArgs e)
    {
        if (sender is not NumberBox numberBox || numberBox.Tag is not string featureId || double.IsNaN(numberBox.Value))
        {
            return;
        }
        _trainerActionValues[featureId] = (long)Math.Clamp(Math.Round(numberBox.Value), 1d, 8_000_000d);
    }

    private void TrainerValueInput_Tapped(object sender, TappedRoutedEventArgs e) => e.Handled = true;

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
            TrainerOperationStatusText.Text = state?.StatusMessage ?? UiText("当前功能不可用。", "現在この機能は利用できません。", "This feature is currently unavailable.");
            return;
        }

        _trainerActionInProgress = true;
        TrainerDisableAllButton.IsEnabled = false;
        try
        {
            TrainerBuildProbeSnapshot? before = null;
            if (definition.Kind == TrainerFeatureKind.ValueAction)
            {
                before = await _buildProbe.ProbeAsync(_session);
                var amount = _trainerActionValues.TryGetValue(featureId, out var configuredValue)
                    ? configuredValue
                    : definition.DefaultValue;
                await _trainer.ExecuteAsync(featureId, amount);
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
                    _ => $"{localized} · {UiText("完成", "完了", "Completed")}",
                };
            }
            else
            {
                TrainerOperationStatusText.Text = $"{localized} · {(updatedState.IsEnabled ? UiText("已启用", "有効", "Enabled") : UiText("已关闭", "無効", "Disabled"))}";
            }
        }
        catch (Exception exception)
        {
            TrainerOperationStatusText.Text = exception.Message;
        }
        finally
        {
            _trainerActionInProgress = false;
            _lastTrainerUiSignature = null;
            RefreshTrainerList(force: true);
        }
    }

    private async void TrainerDisableAllButton_Click(object sender, RoutedEventArgs e)
    {
        if (_trainerActionInProgress || _session is null)
        {
            return;
        }

        _trainerActionInProgress = true;
        try
        {
            var enabledIds = _trainer.Features
                .Where(state => state.IsEnabled && state.Definition.Kind == TrainerFeatureKind.Toggle)
                .Select(state => state.Definition.Id)
                .ToArray();
            foreach (var featureId in enabledIds)
            {
                await _trainer.SetEnabledAsync(featureId, false);
            }
            TrainerOperationStatusText.Text = UiText("已关闭全部持续功能。", "すべての継続機能を無効にしました。", "All persistent features disabled.");
        }
        catch (Exception exception)
        {
            TrainerOperationStatusText.Text = exception.Message;
        }
        finally
        {
            _trainerActionInProgress = false;
            _lastTrainerUiSignature = null;
            RefreshTrainerList(force: true);
        }
    }

    private void TrainerCategoryButton_Click(object sender, RoutedEventArgs e)
    {
        if (sender is Button { Tag: string category } && TrainerCategories.ContainsKey(category))
        {
            _trainerCategory = category;
            ApplyTrainerFilter();
        }
    }

    private void TrainerSearchBox_TextChanged(object sender, TextChangedEventArgs e) => ApplyTrainerFilter();

    private void CompactTrainerButton_Click(object sender, RoutedEventArgs e)
    {
        if (_compactTrainerWindow is not null)
        {
            _compactTrainerWindow.Activate();
            return;
        }

        _compactTrainerWindow = new CompactTrainerWindow(
            _trainerFeatures,
            ExecuteTrainerFeatureAsync,
            SetTrainerActionValue);
        _compactTrainerWindow.Closed += (_, _) => _compactTrainerWindow = null;
        _compactTrainerWindow.Activate();
    }

    private void SetTrainerActionValue(string featureId, long value)
    {
        if (_trainerActionValues.ContainsKey(featureId))
        {
            _trainerActionValues[featureId] = Math.Clamp(value, 1, 8_000_000);
        }
    }

    private string BuildDiagnosticsSummary(GameSession session, string displayVersion)
    {
        var core = session.IsGatheringStormCoreLoaded ? session.GameCoreModulePath : "Gathering Storm GameCore not loaded";
        return $"PID: {session.ProcessId}\nStore: {StoreLabel(session.Store)}\nRenderer: {BackendLabel(session.GraphicsBackend)}\nVersion: {displayVersion}\nExecutable: {session.ExecutablePath}\nGameCore: {core}";
    }

    private void RenderLauncherState()
    {
        var firstInstallation = _installations.FirstOrDefault();
        if (firstInstallation is null)
        {
            LaunchStoreText.Text = UiText("尚未检测到游戏安装", "ゲームのインストールが見つかりません", "Game installation not detected");
            LaunchPathText.Text = UiText("点击“重新扫描”再次检测 Steam / Epic Games。", "再スキャンして Steam / Epic Games を検出します。", "Rescan to detect Steam / Epic Games.");
            LaunchDx11Button.IsEnabled = false;
            LaunchDx12Button.IsEnabled = false;
            LaunchStatusText.Text = UiText("等待检测", "検出待ち", "Waiting for detection");
            return;
        }

        LaunchStoreText.Text = $"{StoreLabel(firstInstallation.Store)} · Civilization VI";
        LaunchPathText.Text = firstInstallation.InstallDirectory;
        LaunchDx11Button.IsEnabled = _session is null && HasExecutable(GraphicsBackend.DirectX11);
        LaunchDx12Button.IsEnabled = _session is null && HasExecutable(GraphicsBackend.DirectX12);
        LaunchStatusText.Text = _session is not null
            ? UiText("游戏已在运行，快捷启动暂时禁用。", "ゲームは実行中です。クイック起動は無効です。", "Game is running; quick launch is disabled.")
            : UiText("已自动解析可用启动目标。", "利用可能な起動対象を検出しました。", "Available launch targets detected.");
    }

    private bool HasExecutable(GraphicsBackend backend) =>
        _installations.Any(installation => installation.Executables.Any(executable => executable.GraphicsBackend == backend));

    private void LaunchDx11Button_Click(object sender, RoutedEventArgs e) => LaunchGame(GraphicsBackend.DirectX11);
    private void LaunchDx12Button_Click(object sender, RoutedEventArgs e) => LaunchGame(GraphicsBackend.DirectX12);

    private void LaunchGame(GraphicsBackend backend)
    {
        if (_session is not null)
        {
            LaunchStatusText.Text = UiText("Civilization VI 已在运行。", "Civilization VI は実行中です。", "Civilization VI is already running.");
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
                LaunchStatusText.Text = UiText($"已请求启动 {BackendLabel(backend)}。", $"{BackendLabel(backend)} の起動を要求しました。", $"Requested {BackendLabel(backend)} launch.");
            }
            catch (Exception exception)
            {
                LaunchStatusText.Text = exception.Message;
            }
            return;
        }
    }

    private async Task CopyDiagnosticsAsync()
    {
        if (_session is null)
        {
            DiagnosticsPageResultText.Text = UiText("请先启动 Civilization VI。", "Civilization VI を起動してください。", "Start Civilization VI first.");
            return;
        }

        DiagnosticsPageCopyButton.IsEnabled = false;
        DiagnosticsPageResultText.Text = UiText("正在收集诊断信息…", "診断情報を収集中…", "Collecting diagnostics…");
        try
        {
            var snapshot = await _diagnostics.CaptureAsync(_session);
            if (_session.IsGatheringStormCoreLoaded)
            {
                try
                {
                    snapshot = snapshot with { TrainerProbe = await _buildProbe.ProbeAsync(_session) };
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
            DiagnosticsPageResultText.Text = _localization.GetFormattedString("Diagnostics_CopiedFormat", snapshot.ExecutableSha256[..12], snapshot.ModuleImageSize);
        }
        catch (Exception exception)
        {
            DiagnosticsPageResultText.Text = _localization.GetFormattedString("Diagnostics_FailedFormat", exception.Message);
        }
        finally
        {
            DiagnosticsPageCopyButton.IsEnabled = _session is not null;
        }
    }

    private async void DiagnosticsPageCopyButton_Click(object sender, RoutedEventArgs e) => await CopyDiagnosticsAsync();

    private void DiagnosticsButton_Click(object sender, RoutedEventArgs e) => SelectNavigationTag("diagnostics");

    private async void RefreshButton_Click(object sender, RoutedEventArgs e) => await RefreshDiscoveryAsync();

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
        if (args.SelectedItem is NavigationViewItem { Tag: string tag })
        {
            ShowPage(tag);
        }
    }

    private void HomeNavigationButton_Click(object sender, RoutedEventArgs e)
    {
        if (sender is Button { Tag: string tag })
        {
            SelectNavigationTag(tag);
        }
    }

    private void SelectNavigationTag(string tag)
    {
        foreach (var item in RootNavigation.MenuItems.Concat(RootNavigation.FooterMenuItems).OfType<NavigationViewItem>())
        {
            if (string.Equals(item.Tag as string, tag, StringComparison.Ordinal))
            {
                RootNavigation.SelectedItem = item;
                ShowPage(tag);
                return;
            }
        }
    }

    private void ShowPage(string tag)
    {
        OverviewPanel.Visibility = tag == "overview" ? Visibility.Visible : Visibility.Collapsed;
        TrainerPanel.Visibility = tag == "trainer" ? Visibility.Visible : Visibility.Collapsed;
        DiagnosticsPanel.Visibility = tag == "diagnostics" ? Visibility.Visible : Visibility.Collapsed;
        SettingsPanel.Visibility = tag == "settings" ? Visibility.Visible : Visibility.Collapsed;
        PlaceholderPanel.Visibility = tag is "saves" or "mods" or "encyclopedia" ? Visibility.Visible : Visibility.Collapsed;

        if (PlaceholderPanel.Visibility == Visibility.Visible)
        {
            (PlaceholderTitleText.Text, PlaceholderDescriptionText.Text) = tag switch
            {
                "saves" => (UiText("存档管理", "セーブ管理", "Save Manager"), UiText("保留存档浏览、备份与恢复入口；当前版本暂不实现。", "セーブ閲覧・バックアップ・復元の入口を予約しています。", "Save browsing, backup, and restore are reserved for a later milestone.")),
                "mods" => (UiText("Mod 管理", "Mod 管理", "Mod Manager"), UiText("保留 Mod 浏览、启停与配置入口；当前版本暂不实现。", "Mod の閲覧・有効化・設定入口を予約しています。", "Mod browsing, toggling, and configuration are reserved for later.")),
                _ => (UiText("百科", "百科事典", "Encyclopedia"), UiText("保留文明、领袖、单位、建筑与机制百科入口；当前版本暂不实现。", "文明・指導者・ユニット・建造物・ルールの入口を予約しています。", "Civilization, leader, unit, building, and rules entries are reserved for later.")),
            };
        }
    }

    private void PageHost_SizeChanged(object sender, SizeChangedEventArgs e)
    {
        var width = e.NewSize.Width;
        var medium = width < 1050;
        var narrow = width < 760;

        HomeSearchBox.Visibility = width < 900 ? Visibility.Collapsed : Visibility.Visible;
        RuntimeBadges.Visibility = width < 820 ? Visibility.Collapsed : Visibility.Visible;
        GameArtworkPlaceholder.Visibility = width < 720 ? Visibility.Collapsed : Visibility.Visible;

        if (medium)
        {
            SessionSecondaryColumn.Width = new GridLength(0);
            Grid.SetColumn(ContinueGameCard, 0);
            Grid.SetColumnSpan(ContinueGameCard, 2);
            Grid.SetRow(ContinueGameCard, 0);
            Grid.SetColumn(RunControlCard, 0);
            Grid.SetColumnSpan(RunControlCard, 2);
            Grid.SetRow(RunControlCard, 1);
        }
        else
        {
            SessionPrimaryColumn.Width = new GridLength(2, GridUnitType.Star);
            SessionSecondaryColumn.Width = new GridLength(1, GridUnitType.Star);
            Grid.SetColumn(ContinueGameCard, 0);
            Grid.SetColumnSpan(ContinueGameCard, 1);
            Grid.SetRow(ContinueGameCard, 0);
            Grid.SetColumn(RunControlCard, 1);
            Grid.SetColumnSpan(RunControlCard, 1);
            Grid.SetRow(RunControlCard, 0);
        }

        if (narrow)
        {
            ModuleColumn1.Width = new GridLength(1, GridUnitType.Star);
            ModuleColumn2.Width = new GridLength(0);
            ModuleColumn3.Width = new GridLength(0);
            PlaceModule(SaveModuleCard, 0, 0, 3);
            PlaceModule(ModModuleCard, 0, 1, 3);
            PlaceModule(EncyclopediaModuleCard, 0, 2, 3);
        }
        else if (medium)
        {
            ModuleColumn1.Width = new GridLength(1, GridUnitType.Star);
            ModuleColumn2.Width = new GridLength(1, GridUnitType.Star);
            ModuleColumn3.Width = new GridLength(0);
            PlaceModule(SaveModuleCard, 0, 0, 1);
            PlaceModule(ModModuleCard, 1, 0, 1);
            PlaceModule(EncyclopediaModuleCard, 0, 1, 2);
        }
        else
        {
            ModuleColumn1.Width = new GridLength(1, GridUnitType.Star);
            ModuleColumn2.Width = new GridLength(1, GridUnitType.Star);
            ModuleColumn3.Width = new GridLength(1, GridUnitType.Star);
            PlaceModule(SaveModuleCard, 0, 0, 1);
            PlaceModule(ModModuleCard, 1, 0, 1);
            PlaceModule(EncyclopediaModuleCard, 2, 0, 1);
        }

        if (width < 900)
        {
            DiagnosticsColumn2.Width = new GridLength(0);
            Grid.SetColumn(WriteProtectionCard, 0);
            Grid.SetColumnSpan(WriteProtectionCard, 2);
            Grid.SetRow(WriteProtectionCard, 0);
            Grid.SetColumn(DiagnosticsPurposeCard, 0);
            Grid.SetColumnSpan(DiagnosticsPurposeCard, 2);
            Grid.SetRow(DiagnosticsPurposeCard, 1);
        }
        else
        {
            DiagnosticsColumn1.Width = new GridLength(1, GridUnitType.Star);
            DiagnosticsColumn2.Width = new GridLength(1, GridUnitType.Star);
            Grid.SetColumn(WriteProtectionCard, 0);
            Grid.SetColumnSpan(WriteProtectionCard, 1);
            Grid.SetRow(WriteProtectionCard, 0);
            Grid.SetColumn(DiagnosticsPurposeCard, 1);
            Grid.SetColumnSpan(DiagnosticsPurposeCard, 1);
            Grid.SetRow(DiagnosticsPurposeCard, 0);
        }

        if (width < 700)
        {
            SettingsLanguageControlColumn.Width = new GridLength(0);
            Grid.SetColumn(LanguageComboBox, 0);
            Grid.SetRow(LanguageComboBox, 1);
        }
        else
        {
            SettingsLanguageControlColumn.Width = new GridLength(300);
            Grid.SetColumn(LanguageComboBox, 1);
            Grid.SetRow(LanguageComboBox, 0);
        }

        TrainerCategoryColumn.Width = width < 760 ? new GridLength(150) : new GridLength(190);
    }

    private static void PlaceModule(FrameworkElement element, int column, int row, int columnSpan)
    {
        Grid.SetColumn(element, column);
        Grid.SetRow(element, row);
        Grid.SetColumnSpan(element, columnSpan);
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

    private static string UiText(string zh, string ja, string en)
    {
        var language = CultureInfo.CurrentUICulture.Name;
        if (language.StartsWith("ja", StringComparison.OrdinalIgnoreCase)) return ja;
        if (language.StartsWith("en", StringComparison.OrdinalIgnoreCase)) return en;
        return zh;
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
        _compactTrainerWindow?.Close();
        _trainer.Dispose();
    }

    private sealed record LanguageOption(string Value, string DisplayName);
}