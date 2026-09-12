using System;
using System.Collections.Generic;
using System.Runtime.InteropServices;
using System.Windows;
using System.Windows.Interop;

namespace FloatingPomodoro.Services;

/// Global Ctrl+Alt hotkeys via RegisterHotKey, hooked on an existing WPF window.
public sealed class Hotkeys : IDisposable
{
    private const int WM_HOTKEY = 0x0312;
    private const uint MOD_ALT = 1, MOD_CONTROL = 2, MOD_SHIFT = 4, MOD_NOREPEAT = 0x4000;

    [DllImport("user32.dll")] private static extern bool RegisterHotKey(IntPtr hWnd, int id, uint mods, uint vk);
    [DllImport("user32.dll")] private static extern bool UnregisterHotKey(IntPtr hWnd, int id);

    private readonly IntPtr _hwnd;
    private readonly HwndSource _src;
    private readonly Dictionary<int, Action> _actions = new();

    public Hotkeys(Window window)
    {
        _hwnd = new WindowInteropHelper(window).EnsureHandle();
        _src = HwndSource.FromHwnd(_hwnd);
        _src.AddHook(Hook);
    }

    /// Tries Ctrl+Alt+key, then Ctrl+Alt+Shift+key if another app owns the first.
    /// Returns the human-readable combo that registered, or null if both are taken.
    public string? Add(uint vk, string keyName, Action action)
    {
        foreach (var (mods, label) in new[] { (MOD_CONTROL | MOD_ALT, "Ctrl + Alt"), (MOD_CONTROL | MOD_ALT | MOD_SHIFT, "Ctrl + Alt + Shift") })
        {
            int id = _actions.Count + 1;
            if (!RegisterHotKey(_hwnd, id, mods | MOD_NOREPEAT, vk)) continue;
            _actions[id] = action;
            return $"{label} + {keyName}";
        }
        return null;
    }

    private IntPtr Hook(IntPtr hwnd, int msg, IntPtr wParam, IntPtr lParam, ref bool handled)
    {
        if (msg == WM_HOTKEY && _actions.TryGetValue(wParam.ToInt32(), out var a)) { a(); handled = true; }
        return IntPtr.Zero;
    }

    public void Dispose()
    {
        foreach (var id in _actions.Keys) UnregisterHotKey(_hwnd, id);
        _src.RemoveHook(Hook);
    }
}
