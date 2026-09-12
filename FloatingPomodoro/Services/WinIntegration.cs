using System;
using Microsoft.Win32;

namespace FloatingPomodoro.Services;

public static class WinIntegration
{
    private const string RunKey = @"Software\Microsoft\Windows\CurrentVersion\Run";
    private const string Name = "FloatingPomodoro";

    /// Whether the Run key currently has us. This, not settings.json, is the truth: the
    /// installer can write it too, and a stale `false` in settings would silently delete it.
    public static bool LaunchesAtStartup()
    {
        using var k = Registry.CurrentUser.OpenSubKey(RunKey);
        return k?.GetValue(Name) != null;
    }

    /// "Launch with Windows" via the HKCU Run key.
    public static void SetLaunchAtStartup(bool enabled)
    {
        using var k = Registry.CurrentUser.CreateSubKey(RunKey);
        if (enabled) k.SetValue(Name, $"\"{Environment.ProcessPath}\"");
        else k.DeleteValue(Name, throwOnMissingValue: false);
    }

    /// Windows "Apps use light theme" setting.
    public static bool SystemIsLight()
    {
        using var k = Registry.CurrentUser.OpenSubKey(@"Software\Microsoft\Windows\CurrentVersion\Themes\Personalize");
        return k?.GetValue("AppsUseLightTheme") is int v && v == 1;
    }
}
