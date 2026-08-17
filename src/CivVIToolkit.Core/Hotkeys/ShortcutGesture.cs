namespace CivVIToolkit.Core.Hotkeys;

[Flags]
public enum ShortcutModifiers
{
    None = 0,
    Alt = 1,
    Control = 2,
    Shift = 4,
    Windows = 8,
}

public sealed record ShortcutGesture(ShortcutModifiers Modifiers, string Key)
{
    public static ShortcutGesture Parse(string text)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(text);
        var parts = text.Split('+', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);
        if (parts.Length == 1)
        {
            return new ShortcutGesture(ShortcutModifiers.None, parts[0]);
        }

        var modifiers = ShortcutModifiers.None;
        for (var index = 0; index < parts.Length - 1; index++)
        {
            modifiers |= parts[index].ToLowerInvariant() switch
            {
                "alt" => ShortcutModifiers.Alt,
                "ctrl" or "control" => ShortcutModifiers.Control,
                "shift" => ShortcutModifiers.Shift,
                "win" or "windows" => ShortcutModifiers.Windows,
                _ => throw new FormatException($"Unknown shortcut modifier '{parts[index]}'."),
            };
        }

        return new ShortcutGesture(modifiers, parts[^1]);
    }
}

public interface IHotkeyRegistrationService
{
    IDisposable Register(ShortcutGesture gesture, Action callback);
}
