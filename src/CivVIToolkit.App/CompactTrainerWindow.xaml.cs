using CivVIToolkit.App.Localization;
using Microsoft.UI.Windowing;
using Microsoft.UI.Xaml;
using Windows.Graphics;

namespace CivVIToolkit.App;

public sealed partial class CompactTrainerWindow : Window
{
    public CompactTrainerWindow(IReadOnlyList<LocalizedTrainerFeature> features)
    {
        InitializeComponent();
        Title = "CivVIToolkit · Trainer";
        ExtendsContentIntoTitleBar = true;
        SetTitleBar(CompactTitleBar);
        CompactTrainerList.ItemsSource = features;

        AppWindow.Resize(new SizeInt32(520, 720));
        if (AppWindow.Presenter is OverlappedPresenter presenter)
        {
            presenter.IsAlwaysOnTop = true;
            presenter.IsMaximizable = false;
            presenter.IsMinimizable = false;
        }
    }
}
