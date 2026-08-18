using CivVIToolkit.App.Localization;
using Microsoft.UI.Windowing;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Input;
using Windows.Graphics;

namespace CivVIToolkit.App;

public sealed partial class CompactTrainerWindow : Window
{
    private readonly Func<string, Task> _executeFeatureAsync;
    private readonly Action<string, long> _setActionValue;

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
        UpdateFeatures(features);

        AppWindow.Resize(new SizeInt32(560, 680));
        if (AppWindow.Presenter is OverlappedPresenter presenter)
        {
            presenter.IsAlwaysOnTop = true;
            presenter.IsMaximizable = false;
            presenter.IsMinimizable = false;
        }
    }

    public void UpdateFeatures(IReadOnlyList<LocalizedTrainerFeature> features)
    {
        CompactTrainerList.ItemsSource = features.ToList();
        var enabled = features.Count(feature => feature.IsEnabled);
        var available = features.Count(feature => feature.Availability == CivVIToolkit.Core.Trainer.TrainerAvailability.Available);
        CompactStatusText.Text = available == features.Count
            ? $"置顶显示 · {enabled} 项启用 · Profile 已验证"
            : $"置顶显示 · {enabled} 项启用 · 等待支持的游戏 Profile";
    }

    private async void CompactTrainerList_ItemClick(object sender, ItemClickEventArgs e)
    {
        if (e.ClickedItem is not LocalizedTrainerFeature feature)
        {
            return;
        }
        await _executeFeatureAsync(feature.Id);
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
