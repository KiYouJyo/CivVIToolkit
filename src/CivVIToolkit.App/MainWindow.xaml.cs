using System.Text.Json;
using System.Text.Json.Serialization;
using CivVIToolkit.Core.Game;
using CivVIToolkit.Core.Modules;
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
    private readonly DispatcherQueueTimer _processTimer;

    private IReadOnlyList<GameInstallation> _installations = [];
    private GameSession? _session;
    private bool _refreshInProgress;

    public MainWindow()
    {
        InitializeComponent();
        Title = "Civ VI Toolkit";
        SystemBackdrop = new MicaBackdrop();

        TrainerList.ItemsSource = TrainerCatalog.All;
        RootNavigation.SelectedItem = RootNavigation.MenuItems[0];

        _processTimer = DispatcherQueue.CreateTimer();
        _processTimer.Interval = TimeSpan.FromSeconds(2);
        _processTimer.IsRepeating = true;
        _processTimer.Tick += ProcessTimer_Tick;
        _processTimer.Start();

        Closed += MainWindow_Closed;
        _ = RefreshDiscoveryAsync();
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
            StatusTitleText.Text = "Detection failed";
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
            StatusTitleText.Text = "Civilization VI is running";
            StatusBadgeText.Text = $"{StoreLabel(_session.Store)} · {BackendLabel(_session.GraphicsBackend)}";
            DetectionDetailsText.Text =
                $"PID {_session.ProcessId}  ·  Version {(_session.FileVersion?.ToString() ?? "unknown")}\n{_session.ExecutablePath}";
            TrainerStatusText.Text =
                $"Attached to {StoreLabel(_session.Store)} / {BackendLabel(_session.GraphicsBackend)}. Core memory access and AoB scanning are ready; feature signatures are still pending verification.";
        }
        else if (_installations.Count > 0)
        {
            var stores = string.Join(" + ", _installations.Select(installation => StoreLabel(installation.Store)).Distinct());
            StatusTitleText.Text = "Civilization VI installation detected";
            StatusBadgeText.Text = $"{stores} · game is not running";
            DetectionDetailsText.Text = "Launch the game normally. CivVIToolkit will identify DX11 or DX12 automatically when the process appears.";
            TrainerStatusText.Text = "Installation found. Start Civilization VI to attach the trainer core.";
        }
        else
        {
            StatusTitleText.Text = "Civilization VI was not detected";
            StatusBadgeText.Text = "Steam / Epic Games scan completed";
            DetectionDetailsText.Text = "Install the game through Steam or Epic Games Launcher, then press Refresh. No manual game path is required for supported installations.";
            TrainerStatusText.Text = "Waiting for a supported Civilization VI installation and process.";
        }

        InstallationsText.Text = _installations.Count == 0
            ? "No installation metadata found."
            : string.Join("\n", _installations.Select(FormatInstallation));
    }

    private static string FormatInstallation(GameInstallation installation)
    {
        var renderers = installation.Executables.Count == 0
            ? "executables not resolved"
            : string.Join(", ", installation.Executables.Select(executable => BackendLabel(executable.GraphicsBackend)).Distinct());
        return $"{StoreLabel(installation.Store)} · {renderers} · {installation.InstallDirectory}";
    }

    private static string StoreLabel(GameStore store) => store switch
    {
        GameStore.Steam => "Steam",
        GameStore.EpicGames => "Epic Games",
        _ => "Unknown store",
    };

    private static string BackendLabel(GraphicsBackend backend) => backend switch
    {
        GraphicsBackend.DirectX11 => "DX11",
        GraphicsBackend.DirectX12 => "DX12",
        _ => "Unknown renderer",
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
        DiagnosticsStatusText.Text = "Collecting read-only runtime diagnostics…";
        try
        {
            var snapshot = await _diagnostics.CaptureAsync(_session);
            var json = JsonSerializer.Serialize(snapshot, DiagnosticsJsonOptions);
            var package = new DataPackage();
            package.SetText(json);
            Clipboard.SetContent(package);
            Clipboard.Flush();
            DiagnosticsStatusText.Text = $"Diagnostics copied · SHA-256 {snapshot.ExecutableSha256[..12]}… · image {snapshot.ModuleImageSize:N0} bytes";
        }
        catch (Exception exception)
        {
            DiagnosticsStatusText.Text = $"Diagnostics failed: {exception.Message}";
        }
        finally
        {
            DiagnosticsButton.IsEnabled = _session is not null;
        }
    }

    private void RootNavigation_SelectionChanged(NavigationView sender, NavigationViewSelectionChangedEventArgs args)
    {
        if (args.SelectedItem is not NavigationViewItem item || item.Tag is not string tag)
        {
            return;
        }

        OverviewPanel.Visibility = tag == "overview" ? Visibility.Visible : Visibility.Collapsed;
        TrainerPanel.Visibility = tag == "trainer" ? Visibility.Visible : Visibility.Collapsed;
        PlaceholderPanel.Visibility = tag is not ("overview" or "trainer") ? Visibility.Visible : Visibility.Collapsed;

        if (PlaceholderPanel.Visibility == Visibility.Visible)
        {
            var module = tag switch
            {
                "saves" => ToolkitModuleCatalog.All.First(m => m.Id == ToolkitModuleId.Saves),
                "maps" => ToolkitModuleCatalog.All.First(m => m.Id == ToolkitModuleId.MapsAndGameInfo),
                "mods" => ToolkitModuleCatalog.All.First(m => m.Id == ToolkitModuleId.Mods),
                "launcher" => ToolkitModuleCatalog.All.First(m => m.Id == ToolkitModuleId.Launcher),
                _ => ToolkitModuleCatalog.All.First(m => m.Id == ToolkitModuleId.Settings),
            };

            PlaceholderTitleText.Text = module.Title;
            PlaceholderDescriptionText.Text = module.Description;
        }
    }

    private void MainWindow_Closed(object sender, WindowEventArgs args)
    {
        _processTimer.Stop();
        _trainer.Dispose();
    }
}
