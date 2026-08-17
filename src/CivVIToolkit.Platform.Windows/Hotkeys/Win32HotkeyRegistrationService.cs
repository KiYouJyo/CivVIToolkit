using System.ComponentModel;
using System.Runtime.InteropServices;
using CivVIToolkit.Core.Hotkeys;

namespace CivVIToolkit.Platform.Windows.Hotkeys;

public sealed class Win32HotkeyRegistrationService : IHotkeyRegistrationService, IDisposable
{
    private const uint WmHotkey = 0x0312;
    private const uint ModAlt = 0x0001;
    private const uint ModControl = 0x0002;
    private const uint ModShift = 0x0004;
    private const uint ModWin = 0x0008;
    private const uint ModNoRepeat = 0x4000;

    private readonly nint _windowHandle;
    private readonly SubclassProc _subclassProc;
    private readonly Dictionary<int, Action> _callbacks = [];
    private readonly nuint _subclassId;
    private int _nextId = 0xC100;
    private bool _disposed;

    public Win32HotkeyRegistrationService(nint windowHandle)
    {
        if (windowHandle == 0)
        {
            throw new ArgumentException("A valid Win32 window handle is required.", nameof(windowHandle));
        }

        _windowHandle = windowHandle;
        _subclassId = (nuint)GetHashCode();
        _subclassProc = WindowSubclassProc;

        if (!SetWindowSubclass(_windowHandle, _subclassProc, _subclassId, 0))
        {
            throw new Win32Exception(Marshal.GetLastWin32Error(), "Unable to install the hotkey window subclass.");
        }
    }

    public IDisposable Register(ShortcutGesture gesture, Action callback)
    {
        ObjectDisposedException.ThrowIf(_disposed, this);
        ArgumentNullException.ThrowIfNull(gesture);
        ArgumentNullException.ThrowIfNull(callback);

        var id = Interlocked.Increment(ref _nextId);
        var modifiers = ToNativeModifiers(gesture.Modifiers) | ModNoRepeat;
        var virtualKey = VirtualKeyResolver.Resolve(gesture.Key);

        if (!RegisterHotKey(_windowHandle, id, modifiers, virtualKey))
        {
            throw new Win32Exception(Marshal.GetLastWin32Error(), $"Unable to register hotkey '{gesture}'.");
        }

        _callbacks[id] = callback;
        return new Registration(this, id);
    }

    private nint WindowSubclassProc(
        nint window,
        uint message,
        nuint wParam,
        nint lParam,
        nuint subclassId,
        nuint referenceData)
    {
        if (message == WmHotkey && _callbacks.TryGetValue(unchecked((int)wParam), out var callback))
        {
            callback();
            return 0;
        }

        return DefSubclassProc(window, message, wParam, lParam);
    }

    private void Unregister(int id)
    {
        if (_callbacks.Remove(id))
        {
            UnregisterHotKey(_windowHandle, id);
        }
    }

    public void Dispose()
    {
        if (_disposed)
        {
            return;
        }

        _disposed = true;
        foreach (var id in _callbacks.Keys.ToArray())
        {
            UnregisterHotKey(_windowHandle, id);
        }

        _callbacks.Clear();
        RemoveWindowSubclass(_windowHandle, _subclassProc, _subclassId);
    }

    private static uint ToNativeModifiers(ShortcutModifiers modifiers)
    {
        var native = 0u;
        if (modifiers.HasFlag(ShortcutModifiers.Alt)) native |= ModAlt;
        if (modifiers.HasFlag(ShortcutModifiers.Control)) native |= ModControl;
        if (modifiers.HasFlag(ShortcutModifiers.Shift)) native |= ModShift;
        if (modifiers.HasFlag(ShortcutModifiers.Windows)) native |= ModWin;
        return native;
    }

    private sealed class Registration(Win32HotkeyRegistrationService owner, int id) : IDisposable
    {
        private Win32HotkeyRegistrationService? _owner = owner;

        public void Dispose()
        {
            Interlocked.Exchange(ref _owner, null)?.Unregister(id);
        }
    }

    private delegate nint SubclassProc(
        nint hWnd,
        uint uMsg,
        nuint wParam,
        nint lParam,
        nuint uIdSubclass,
        nuint dwRefData);

    [DllImport("user32.dll", SetLastError = true)]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static extern bool RegisterHotKey(nint hWnd, int id, uint fsModifiers, uint vk);

    [DllImport("user32.dll", SetLastError = true)]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static extern bool UnregisterHotKey(nint hWnd, int id);

    [DllImport("comctl32.dll", SetLastError = true)]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static extern bool SetWindowSubclass(nint hWnd, SubclassProc pfnSubclass, nuint uIdSubclass, nuint dwRefData);

    [DllImport("comctl32.dll", SetLastError = true)]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static extern bool RemoveWindowSubclass(nint hWnd, SubclassProc pfnSubclass, nuint uIdSubclass);

    [DllImport("comctl32.dll")]
    private static extern nint DefSubclassProc(nint hWnd, uint uMsg, nuint wParam, nint lParam);
}

public static class VirtualKeyResolver
{
    public static uint Resolve(string key)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(key);
        var normalized = key.Trim();

        if (normalized.StartsWith("Num ", StringComparison.OrdinalIgnoreCase))
        {
            var suffix = normalized[4..];
            if (suffix.Length == 1 && char.IsDigit(suffix[0]))
            {
                return 0x60u + (uint)(suffix[0] - '0');
            }

            if (suffix == ".")
            {
                return 0x6E;
            }
        }

        if (normalized.Equals("PageUp", StringComparison.OrdinalIgnoreCase)) return 0x21;
        if (normalized.Equals("PageDown", StringComparison.OrdinalIgnoreCase)) return 0x22;

        if (normalized.Length >= 2
            && normalized[0] is 'F' or 'f'
            && int.TryParse(normalized[1..], out var functionKey)
            && functionKey is >= 1 and <= 24)
        {
            return 0x70u + (uint)(functionKey - 1);
        }

        if (normalized.Length == 1)
        {
            var character = char.ToUpperInvariant(normalized[0]);
            if (character is >= 'A' and <= 'Z' or >= '0' and <= '9')
            {
                return character;
            }
        }

        throw new FormatException($"Unsupported hotkey key '{key}'.");
    }
}
