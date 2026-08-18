using Microsoft.UI.Xaml;

namespace CivVIToolkit.App.Localization;

/// <summary>
/// Stores an MRT resource prefix on visual elements so the current window can
/// reapply localized values immediately after a language change.
/// </summary>
public static class LiveLocalization
{
    public static readonly DependencyProperty KeyProperty = DependencyProperty.RegisterAttached(
        "Key",
        typeof(string),
        typeof(LiveLocalization),
        new PropertyMetadata(null));

    public static void SetKey(DependencyObject element, string value) =>
        element.SetValue(KeyProperty, value);

    public static string? GetKey(DependencyObject element) =>
        element.GetValue(KeyProperty) as string;
}