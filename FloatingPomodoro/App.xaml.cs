using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Windows;
using System.Windows.Data;
using System.Windows.Media;
using System.Windows.Media.Animation;
using FloatingPomodoro.Models;
using FloatingPomodoro.Services;
using FloatingPomodoro.ViewModels;
using FloatingPomodoro.Views;
using Forms = System.Windows.Forms;

namespace FloatingPomodoro;

public partial class App : Application
{
    public static new App Current => (App)Application.Current;

    public TimerViewModel VM { get; private set; } = null!;
    public bool Exiting { get; private set; }

    private TimerWindow _win = null!;
    private Forms.NotifyIcon? _tray;
    private Forms.ToolStripItem? _trayStatus;
    private Hotkeys? _hotkeys;
    private SolidColorBrush? _accent;

    protected override void OnStartup(StartupEventArgs e)
    {
        base.OnStartup(e);
        if (Array.IndexOf(e.Args, "--selftest") >= 0) { Environment.Exit(SelfTest.Run()); return; }

        VM = new TimerViewModel();
        ApplyTheme();

        VM.PropertyChanged += (_, a) =>
        {
            if (a.PropertyName == nameof(VM.Mode)) AnimateAccent(VM.Mode);
            else if (a.PropertyName == nameof(VM.TimerText) && _tray != null) _tray.Text = VM.StatusText;
        };
        VM.SettingsApplied += ApplyTheme;
        VM.Notify += (title, msg) => _tray?.ShowBalloonTip(5000, title, msg, Forms.ToolTipIcon.None);

        _win = new TimerWindow(VM);
        _win.Show();
        SetupTray();
        SetupHotkeys();
    }

    protected override void OnExit(ExitEventArgs e)
    {
        _hotkeys?.Dispose();
        if (_tray != null) { _tray.Visible = false; _tray.Dispose(); }
        base.OnExit(e);
    }

    // ---- shell actions ----
    public void ToggleTimer()
    {
        if (_win.IsVisible) _win.Hide();
        else { _win.Show(); _win.Activate(); }
    }

    public void ShowTimer() { _win.Show(); _win.Activate(); }
    public void ShowSettings() => ShowWindow(() => new SettingsWindow(VM));
    public void ShowTasks() => ShowWindow(() => new TaskWindow(VM));
    public void ShowStats() => ShowWindow(() => new StatsWindow(VM));

    /// Application.Windows already tracks what is open, so there is no second registry to keep in sync.
    private void ShowWindow<T>(Func<T> make) where T : Window
    {
        var w = Windows.OfType<T>().FirstOrDefault() ?? make();
        if (!w.IsVisible) w.Show();
        w.Activate();
    }

    public void ExitApp()
    {
        Exiting = true;
        Shutdown();
    }

    // ---- theme ----
    private void ApplyTheme() => ApplyTheme(VM.Settings.Theme, VM.Mode);

    /// The only place the palettes live; App.xaml declares the keys with no colours.
    internal void ApplyTheme(string theme, TimerMode mode)
    {
        bool light = theme == "Light" || (theme == "System" && WinIntegration.SystemIsLight());
        Resources["BgBrush"] = Brush(light ? "#F4F4F6" : "#202124");
        Resources["SurfaceBrush"] = Brush(light ? "#E1E1E6" : "#2C2D31");
        Resources["FgBrush"] = Brush(light ? "#1E1F22" : "#FFFFFF");
        Resources["Fg2Brush"] = Brush(light ? "#6B6B70" : "#A8A8A8");
        // Seed once; re-theming must not replace a brush that AnimateAccent may be animating.
        if (_accent == null) Resources["AccentBrush"] = _accent = new SolidColorBrush(AccentFor(mode));
    }

    /// WPF freezes a resource brush once consumers use it, and a frozen brush cannot be animated.
    /// So each mode change installs a fresh brush that fades from the old colour; DynamicResource consumers pick it up.
    private void AnimateAccent(TimerMode mode)
    {
        var b = new SolidColorBrush(_accent?.Color ?? AccentFor(mode));
        b.BeginAnimation(SolidColorBrush.ColorProperty, new ColorAnimation(AccentFor(mode), TimeSpan.FromMilliseconds(200)));
        _accent = b;
        Resources["AccentBrush"] = b;
    }

    private static Color AccentFor(TimerMode mode) => Parse(mode switch
    {
        TimerMode.ShortBreak => "#4E9F86",
        TimerMode.LongBreak => "#4976B8",
        _ => "#E76F51",
    });

    private static Color Parse(string hex) => (Color)ColorConverter.ConvertFromString(hex);
    private static SolidColorBrush Brush(string hex) => new(Parse(hex));

    // ---- tray ----
    private void SetupTray()
    {
        var menu = new Forms.ContextMenuStrip();
        _trayStatus = menu.Items.Add("Floating Pomodoro");
        _trayStatus.Enabled = false;
        menu.Items.Add(new Forms.ToolStripSeparator());
        menu.Items.Add("Start / Pause", null, (_, _) => VM.StartPause());
        menu.Items.Add("Skip", null, (_, _) => VM.Skip());
        menu.Items.Add("Reset", null, (_, _) => VM.Reset());
        menu.Items.Add(new Forms.ToolStripSeparator());
        menu.Items.Add("Show Timer", null, (_, _) => ShowTimer());
        menu.Items.Add("Tasks", null, (_, _) => ShowTasks());
        menu.Items.Add("Statistics", null, (_, _) => ShowStats());
        menu.Items.Add("Settings", null, (_, _) => ShowSettings());
        menu.Items.Add(new Forms.ToolStripSeparator());
        menu.Items.Add("Exit", null, (_, _) => ExitApp());
        menu.Opening += (_, _) => _trayStatus.Text = VM.StatusText;

        _tray = new Forms.NotifyIcon
        {
            // Read the app's own embedded icon rather than a loose .ico, so a single-file exe still has a tray icon.
            Icon = TrayIcon(),
            Text = "Floating Pomodoro",
            Visible = true,
            ContextMenuStrip = menu,
        };
        _tray.MouseClick += (_, a) => { if (a.Button == Forms.MouseButtons.Left) ToggleTimer(); };
    }

    private static System.Drawing.Icon TrayIcon()
    {
        try { return System.Drawing.Icon.ExtractAssociatedIcon(Environment.ProcessPath!) ?? System.Drawing.SystemIcons.Application; }
        catch { return System.Drawing.SystemIcons.Application; }
    }

    // ---- global hotkeys (Ctrl+Alt+...) ----
    /// "Ctrl + Alt + Space  —  Start / Pause" lines, reflecting what actually registered. Shown in Settings.
    public List<string> HotkeyLines { get; } = new();

    private void SetupHotkeys()
    {
        _hotkeys = new Hotkeys(_win);
        Bind(0x20, "Space", "Start / Pause", VM.StartPause);
        Bind(0x27, "Right", "Skip", VM.Skip);
        Bind('R', "R", "Reset", VM.Reset);
        Bind('P', "P", "Show / hide timer", ToggleTimer);

        void Bind(uint vk, string key, string what, Action action) =>
            HotkeyLines.Add($"{_hotkeys.Add(vk, key, action) ?? "(taken by another app)"}  —  {what}");
    }
}

public class BoolToVis : IValueConverter
{
    public bool Invert { get; set; }

    public object Convert(object value, Type targetType, object parameter, CultureInfo culture) =>
        (value is true) ^ Invert ? Visibility.Visible : Visibility.Collapsed;

    public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture) =>
        throw new NotSupportedException();
}
