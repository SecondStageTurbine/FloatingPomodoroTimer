using System;
using System.Collections.Generic;
using System.Linq;
using FloatingPomodoro.Models;
using FloatingPomodoro.Services;
using FloatingPomodoro.ViewModels;

namespace FloatingPomodoro;

/// `FloatingPomodoro.exe --selftest` : the one runnable check for the non-trivial logic (cycle + stats) plus a window smoke test.
/// Exit code = number of failures; details also land in %TEMP%\FloatingPomodoro.selftest.txt since a WinExe has no console.
public static class SelfTest
{
    public static int Run()
    {
        var failures = new List<string>();
        void Check(bool ok, string what) { if (!ok) failures.Add(what); }

        // Cycle: Focus -> Short x(N-1) -> Long -> Focus, dots track it.
        var vm = new TimerViewModel();
        int n = vm.Settings.LongBreakInterval;
        Check(vm.Mode == TimerMode.Focus, "starts in Focus");
        for (int i = 1; i < n; i++)
        {
            vm.Skip(); Check(vm.Mode == TimerMode.ShortBreak, $"focus #{i} -> short break");
            vm.Skip(); Check(vm.Mode == TimerMode.Focus, $"short break #{i} -> focus");
        }
        vm.Skip(); Check(vm.Mode == TimerMode.LongBreak, $"focus #{n} -> long break");
        Check(vm.CycleDots.Count(c => c == '●') == n, "all dots filled before long break");
        vm.Skip(); Check(vm.Mode == TimerMode.Focus && vm.CycleDots.Count(c => c == '●') == 0, "long break -> focus, cycle reset");
        Check(vm.TimerText == $"{vm.Settings.FocusMinutes:00}:00", "timer text after reset");
        Check(Math.Abs(vm.Progress) < 1e-9, "progress zero at start");

        // Stats: today/week bucketing and formatting.
        var h = new List<PomodoroSession>
        {
            new() { Mode = TimerMode.Focus, DurationMinutes = 25, CompletedAt = DateTime.Today.AddHours(9), TaskId = "a" },
            new() { Mode = TimerMode.Focus, DurationMinutes = 25, CompletedAt = DateTime.Today.AddHours(10), TaskId = "a" },
            new() { Mode = TimerMode.ShortBreak, DurationMinutes = 5, CompletedAt = DateTime.Today.AddHours(10) },
            new() { Mode = TimerMode.Focus, DurationMinutes = 50, CompletedAt = DateTime.Today.AddDays(-1).AddHours(9), TaskId = "b" },
            new() { Mode = TimerMode.Focus, DurationMinutes = 50, CompletedAt = DateTime.Today.AddDays(-10) },
        };
        var today = Stats.Today(h);
        Check(today == (50, 2, 1), $"today summary {today}");
        var week = Stats.Daily(h, 7);
        Check(week.Count == 7 && week[^1].Minutes == 50 && week[^2].Minutes == 50 && week.Sum(d => d.Pomodoros) == 3, "7-day buckets");
        Check(Stats.Hm(135) == "2h 15m" && Stats.Hm(45) == "45m", "h/m formatting");
        Check(Stats.Plural(1, "Pomodoro") == "1 Pomodoro" && Stats.Plural(2, "task") == "2 tasks", "pluralisation");

        // Settings sanitize.
        var s = new AppSettings { LongBreakInterval = 0, Opacity = 0.1, Theme = "Neon" };
        s.Sanitize();
        Check(s.LongBreakInterval == 1 && s.Opacity == 0.7 && s.Theme == "System", "sanitize clamps");

        // Alarms must unpack from the embedded resources, or a single-file exe has a silent alarm.
        var sounds = Services.AudioService.Sounds;
        Check(sounds.Length >= 5, $"alarms unpacked to {Services.AudioService.SoundDir} (found {sounds.Length})");
        Check(Array.IndexOf(sounds, vm.Settings.Alarm) >= 0, $"default alarm '{vm.Settings.Alarm}' is among the unpacked sounds");

        // A keyless implicit style on a panel type also matches the panels inside control templates.
        // That is how the dialogs' light text reached ComboBox's items host and painted every dropdown
        // row invisible on Windows' light popup chrome. Keep such styles keyed and applied by hand.
        var leaky = System.Windows.Application.Current.Resources.Keys.OfType<Type>()
            .Where(t => typeof(System.Windows.Controls.Panel).IsAssignableFrom(t)).Select(t => t.Name).ToList();
        Check(leaky.Count == 0, $"app-wide implicit Panel style leaks into control templates: {string.Join(", ", leaky)}");

        // Window smoke: every XAML window loads, binds and closes without throwing. Theme first, the
        // way startup does, so the brush resources the windows reference are actually populated.
        // (SettingsWindow.Closed re-saves settings and rewrites the Run key with the value it just
        // read back from that key, so this stays a no-op on both disk and registry.)
        App.Current.ApplyTheme(vm.Settings.Theme, vm.Mode);
        foreach (var make in new Func<System.Windows.Window>[] { () => new Views.TimerWindow(vm), () => new Views.SettingsWindow(vm), () => new Views.TaskWindow(vm), () => new Views.StatsWindow(vm) })
        {
            try
            {
                var w = make();
                w.Show();
                System.Windows.Threading.Dispatcher.CurrentDispatcher.Invoke(() => { }, System.Windows.Threading.DispatcherPriority.Loaded);
                w.Close();
            }
            catch (Exception ex) { var inner = ex; while (inner.InnerException != null) inner = inner.InnerException; failures.Add($"window failed: {ex.GetType().Name}: {ex.Message} | root: {inner.GetType().Name}: {inner.Message} | {(ex as System.Windows.Markup.XamlParseException)?.BaseUri} line {(ex as System.Windows.Markup.XamlParseException)?.LineNumber}"); }
        }

        Console.WriteLine(failures.Count == 0 ? "SELFTEST OK" : "SELFTEST FAILED:\n  " + string.Join("\n  ", failures));
        System.IO.File.WriteAllText(System.IO.Path.Combine(System.IO.Path.GetTempPath(), "FloatingPomodoro.selftest.txt"),
            failures.Count == 0 ? "OK" : string.Join("\n", failures));
        return failures.Count;
    }
}
