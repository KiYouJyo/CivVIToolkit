using CivVIToolkit.App.Localization;
using CivVIToolkit.Core.Trainer;
using Microsoft.UI.Windowing;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Input;
using Microsoft.UI.Xaml.Media;
using Windows.Graphics;

namespace CivVIToolkit.App;

public sealed partial class CompactTrainerWindow : Window
{
    private readonly Func<string, Task> _executeFeatureAsync;
    private readonly Action<string, long> _setActionValue;
    private IReadOnlyList<LocalizedTrainerFeature> _features = [];
    private LocalizationService _localization = LocalizationService.Default;

    public CompactTrainerWindow(
        IReadOnlyList<LocalizedTrainerFeature> features,
        Func<string, Task> executeFeatureAsync,
        Action<string, long> setActionValue)
    {
        InitializeComponent();
        Title = "CivVIToolkit · Trainer";
        ExtendsContentIntoTitleBar = true;
        SetTitleBar(CompactTitleBar);
        _executeFeatureAsync = executeFeatureAsync ?? throw new ArgumentNullException(nameof(executeFeatureAsync));
        _setActionValue = setActionValue ?? throw new ArgumentNullException(nameof(setActionValue));

        CompactRoot.Loaded += CompactRoot_Loaded;
        UpdateFeatures(features);
        ApplyLocalization(_localization);

        if (AppWindow.Presenter is OverlappedPresenter presenter)
        {
            presenter.IsAlwaysOnTop = true;
            presenter.IsMaximizable = false;
            presenter.IsMinimizable = false;
        }
    }

    private void CompactRoot_Loaded(object sender, RoutedEventArgs e)
    {
        // AppWindow.Resize uses physical pixels while XAML layout uses effective
        // pixels. The old fixed 560x680 call made the window only ~280 DIPs wide
        // at 200% scaling, which caused NumberBox and ToggleSwitch overlap.
        var scale = CompactRoot.XamlRoot?.RasterizationScale ?? 1.0;
        const double logicalWidth = 520;
        const double logicalHeight = 640;
        AppWindow.Resize(new SizeInt32(
            Math.Max(1, (int)Math.Round(logicalWidth * scale)),
            Math.Max(1, (int)Math.Round(logicalHeight * scale))));
    }

    public void UpdateFeatures(IReadOnlyList<LocalizedTrainerFeature> features)
    {
        _features = features.ToList();
        CompactTrainerList.ItemsSource = _features;
        UpdateStatusText();
    }

    public void ApplyLocalization(LocalizationService localization)
    {
        _localization = localization ?? throw new ArgumentNullException(nameof(localization));
        ApplyLocalizedTree(CompactRoot);
        Title = _localization.GetString("Compact_TitleBar.Title");
        UpdateStatusText();
    }

    private void UpdateStatusText()
    {
        var enabled = _features.Count(feature => feature.IsEnabled);
        var available = _features.Count(feature => feature.Availability == TrainerAvailability.Available);
        CompactStatusText.Text = available == _features.Count && _features.Count > 0
            ? _localization.GetFormattedString("Compact_StatusVerifiedFormat", enabled)
            : _localization.GetFormattedString("Compact_StatusWaitingFormat", enabled);
    }

    private void ApplyLocalizedTree(DependencyObject root)
    {
        var key = LiveLocalization.GetKey(root);
        if (!string.IsNullOrWhiteSpace(key))
        {
            switch (root)
            {
                case TextBlock textBlock:
                    ApplyIfPresent($"{key}.Text", value => textBlock.Text = value);
                    break;
                case TitleBar titleBar:
                    ApplyIfPresent($"{key}.Title", value => titleBar.Title = value);
                    break;
            }
        }

        var childCount = VisualTreeHelper.GetChildrenCount(root);
        for (var index = 0; index < childCount; index++)
        {
            ApplyLocalizedTree(VisualTreeHelper.GetChild(root, index));
        }
    }

    private void ApplyIfPresent(string resourceKey, Action<string> setter)
    {
        var value = _localization.GetString(resourceKey);
        if (!(value.StartsWith('!') && value.EndsWith('!')))
        {
            setter(value);
        }
    }

    private async void CompactTrainerList_ItemClick(object sender, ItemClickEventArgs e)
    {
        if (e.ClickedItem is LocalizedTrainerFeature feature)
        {
            await _executeFeatureAsync(feature.Id);
        }
    }

    private void CompactValueInput_ValueChanged(object sender, NumberBoxValueChangedEventArgs e)
    {
        if (sender is NumberBox { Tag: string featureId } numberBox && !double.IsNaN(numberBox.Value))
        {
            _setActionValue(featureId, (long)Math.Clamp(Math.Round(numberBox.Value), 1d, 8_000_000d));
        }
    }

    private void CompactValueInput_Tapped(object sender, TappedRoutedEventArgs e) => e.Handled = true;
}