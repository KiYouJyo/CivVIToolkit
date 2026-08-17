namespace CivVIToolkit.App.Localization;

public interface ILocalizationService
{
    string GetString(string resourceKey);

    string GetFormattedString(string resourceKey, params object[] arguments);
}
