using CivVIToolkit.App.Localization;
using CivVIToolkit.Core.Trainer;
using Microsoft.UI.Dispatching;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Media;

namespace CivVIToolkit.App;

public sealed partial class MainWindow
{
    private DispatcherQueueTimer? _trainerRetryTimer;
    private bool _trainerRetryInProgress;

    internal void InitializePostConstructionFixes()
    {
        LanguageComboBox.SelectionChanged -= LanguageComboBox_SelectionChanged;
        LanguageComboBox.SelectionChanged += LanguageComboBox_LiveSelectionChanged;
        PageHost.SizeChanged += UiFixes_PageHostSizeChanged;

        _trainerRetryTimer = DispatcherQueue.CreateTimer();
        _trainerRetryTimer.Interval = TimeSpan.FromSeconds(2);
        _trainerRetryTimer.IsRepeating = true;
        _trainerRetryTimer.Tick += TrainerRetryTimer_Tick;
        _trainerRetryTimer.Start();

        Closed += (_, _) => _trainerRetryTimer?.Stop();

        ApplyLiveLocalization();
        ApplyTrainerLayoutFix(PageHost.ActualWidth);
    }

    private async void TrainerRetryTimer_Tick(DispatcherQueueTimer sender, object args)
    {
        if (_trainerRetryInProgress || _trainerActionInProgress || _session is null || !_session.IsGatheringStormCoreLoaded)
        {
            return;
        }

        var states = _trainer.Features;
        if (states.Any(state => state.Availability == TrainerAvailability.Available)
            || states.Any(state => state.Availability == TrainerAvailability.UnsupportedGameVersion))
        {
            return;
        }

        if (!states.Any(state => state.Availability is TrainerAvailability.Error or TrainerAvailability.SignaturePending or TrainerAvailability.NotAttached))
        {
            return;
        }

        _trainerRetryInProgress = true;
        try
        {
            await _trainer.AttachAsync(_session);
            RenderDetectionState();
        }
        catch
        {
            // AttachAsync already projects the exact failure into Trainer state.
            // Keep retrying transient match-initialization failures without
            // weakening the SHA/AoB fail-closed boundary.
        }
        finally
        {
            _trainerRetryInProgress = false;
        }
    }

    private async void LanguageComboBox_LiveSelectionChanged(object sender, SelectionChangedEventArgs e)
    {
        if (_languageInitializing || LanguageComboBox.SelectedItem is not LanguageOption option)
        {
            return;
        }

        if (_localization is not LocalizationService liveLocalization)
        {
            return;
        }

        var previousPreference = _settings.Language;
        _settings.Language = option.Value;
        if (!_settingsService.Save(_settings))
        {
            _settings.Language = previousPreference;
            ShowLanguageFailure();
            return;
        }

        if (!await liveLocalization.SwitchLanguageAsync(option.Value))
        {
            _settings.Language = previousPreference;
            _settingsService.Save(_settings);
            ShowLanguageFailure();
            return;
        }

        InitializeLanguageOptions();
        ApplyLiveLocalization();
        RenderDetectionState();
        RefreshTrainerList(force: true);

        if (RootNavigation.SelectedItem is NavigationViewItem { Tag: string selectedTag })
        {
            ShowPage(selectedTag);
        }

        _compactTrainerWindow?.ApplyLocalization(liveLocalization);

        LanguageRestartInfoBar.Severity = InfoBarSeverity.Success;
        LanguageRestartInfoBar.Title = liveLocalization.GetString("Settings_LanguageApplied.Title");
        LanguageRestartInfoBar.Message = liveLocalization.GetString("Settings_LanguageApplied.Message");
        LanguageRestartInfoBar.IsOpen = true;
    }

    private void ShowLanguageFailure()
    {
        LanguageRestartInfoBar.Severity = InfoBarSeverity.Error;
        LanguageRestartInfoBar.Title = _localization.GetString("Settings_SaveFailedTitle");
        LanguageRestartInfoBar.Message = _localization.GetString("Settings_SaveFailedMessage");
        LanguageRestartInfoBar.IsOpen = true;
    }

    private void ApplyLiveLocalization()
    {
        ApplyLocalizedTree(RootLayout);
        Title = _localization.GetString("AppDisplayName");
        InitializeVersionText();
        // Refiltering here is user-triggered by a language change. Background
        // polling never calls this path and therefore never rebinds ItemsSource.
        ApplyTrainerFilter();
    }

    private void ApplyLocalizedTree(DependencyObject root)
    {
        ApplyLocalizedElement(root);
        var childCount = VisualTreeHelper.GetChildrenCount(root);
        for (var index = 0; index < childCount; index++)
        {
            ApplyLocalizedTree(VisualTreeHelper.GetChild(root, index));
        }
    }

    private void ApplyLocalizedElement(DependencyObject element)
    {
        var key = LiveLocalization.GetKey(element);
        if (string.IsNullOrWhiteSpace(key))
        {
            return;
        }

        switch (element)
        {
            case TextBlock textBlock:
                TryApply($"{key}.Text", value => textBlock.Text = value);
                break;
            case Button button:
                TryApply($"{key}.Content", value => button.Content = value);
                break;
            case NavigationViewItem navigationItem:
                TryApply($"{key}.Content", value => navigationItem.Content = value);
                break;
            case TextBox textBox:
                TryApply($"{key}.PlaceholderText", value => textBox.PlaceholderText = value);
                break;
            case InfoBar infoBar:
                TryApply($"{key}.Title", value => infoBar.Title = value);
                TryApply($"{key}.Message", value => infoBar.Message = value);
                break;
            case TitleBar titleBar:
                TryApply($"{key}.Title", value => titleBar.Title = value);
                break;
        }
    }

    private void TryApply(string resourceKey, Action<string> setter)
    {
        var value = _localization.GetString(resourceKey);
        if (!string.IsNullOrWhiteSpace(value)
            && !(value.StartsWith('!') && value.EndsWith('!')))
        {
            setter(value);
        }
    }

    private void UiFixes_PageHostSizeChanged(object sender, SizeChangedEventArgs e) =>
        ApplyTrainerLayoutFix(e.NewSize.Width);

    private void ApplyTrainerLayoutFix(double width)
    {
        TrainerCategoryColumn.Width = new GridLength(0);
        Grid.SetColumn(TrainerMainCard, 0);
        Grid.SetColumnSpan(TrainerMainCard, 2);

        TrainerSearchBox.Width = width < 720 ? 180 : 260;
    }
}