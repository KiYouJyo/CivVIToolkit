using System.Globalization;
using Microsoft.Windows.ApplicationModel.Resources;

namespace CivVIToolkit.App.Localization;

/// <summary>
/// Central MRT Core backed localization access for dynamic C# text.
/// </summary>
public sealed class LocalizationService : ILocalizationService
{
    private readonly ResourceLoader _resourceLoader;

    public static LocalizationService Default { get; } = new();

    public LocalizationService()
        : this(new ResourceLoader())
    {
    }

    internal LocalizationService(ResourceLoader resourceLoader)
    {
        _resourceLoader = resourceLoader ?? throw new ArgumentNullException(nameof(resourceLoader));
    }

    public string GetString(string resourceKey)
    {
        if (string.IsNullOrWhiteSpace(resourceKey))
        {
            return string.Empty;
        }

        try
        {
            var value = _resourceLoader.GetString(resourceKey);
            return string.IsNullOrEmpty(value) ? CreatePlaceholder(resourceKey) : value;
        }
        catch
        {
            return CreatePlaceholder(resourceKey);
        }
    }

    public string GetFormattedString(string resourceKey, params object[] arguments)
    {
        var template = GetString(resourceKey);
        try
        {
            return string.Format(CultureInfo.CurrentCulture, template, arguments);
        }
        catch (FormatException)
        {
            return template;
        }
    }

    public static string CreatePlaceholder(string resourceKey) => $"!{resourceKey}!";
}
