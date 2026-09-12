using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;
using FloatingPomodoro.Models;
using FloatingPomodoro.Services;

namespace FloatingPomodoro.ViewModels;

public class TimerViewModel : Observable
{
    private readonly TimerService _timer = new();
    private readonly AudioService _audio = new();
    private TimerMode _mode;
    private bool _finished;
    private int _cycle;
    private TaskItem? _currentTask;
    private DateTime _startedAt;

    public AppSettings Settings { get; }
    public ObservableCollection<TaskItem> Tasks { get; }

    /// Loaded once and appended in memory; the file is the persisted copy, not the source of truth.
    public List<PomodoroSession> History { get; }

    /// (title, message) for a desktop notification. Only raised when the user wants them.
    public event Action<string, string>? Notify;
    /// Fired after ApplySettings so the shell can re-theme.
    public event Action? SettingsApplied;

    public TimerViewModel()
    {
        Settings = Storage.LoadSettings();
        Settings.Sanitize();
        Tasks = new ObservableCollection<TaskItem>(Storage.LoadTasks());
        History = Storage.LoadHistory();

        // The service owns Remaining and IsRunning; the VM just re-publishes what they derive.
        _timer.Tick += _ => RaiseAll(nameof(Remaining), nameof(TimerText), nameof(Progress),
                                     nameof(IsRunning), nameof(PlayGlyph), nameof(StartPauseText));
        _timer.Completed += OnCompleted;
        _timer.Reset(Total);
    }

    // ---- state ----
    private TimeSpan Total => TimeSpan.FromMinutes(Settings.MinutesFor(Mode));

    public TimerMode Mode
    {
        get => _mode;
        private set { if (Set(ref _mode, value)) RaiseAll(nameof(ModeText), nameof(ModeTitle)); }
    }

    public TimeSpan Remaining => _timer.Remaining;
    public bool IsRunning => _timer.IsRunning;

    /// True while flashing after a session ended (until the user interacts).
    public bool Finished { get => _finished; private set => Set(ref _finished, value); }

    public TaskItem? CurrentTask
    {
        get => _currentTask;
        set { if (Set(ref _currentTask, value)) Raise(nameof(CurrentTaskTitle)); }
    }

    // ---- derived for the UI ----
    public string TimerText => $"{(int)Remaining.TotalMinutes:00}:{Remaining.Seconds:00}";
    public string ModeTitle => Mode switch { TimerMode.ShortBreak => "Short Break", TimerMode.LongBreak => "Long Break", _ => "Focus" };
    public string ModeText => ModeTitle.ToUpperInvariant();
    public string StatusText => $"{TimerText} — {ModeTitle}";
    public double Progress => Total.TotalSeconds <= 0 ? 0 : 1 - Remaining.TotalSeconds / Total.TotalSeconds;
    public string PlayGlyph => IsRunning ? "⏸" : "▶";
    public string StartPauseText => IsRunning ? "Pause" : "Start";
    public string CurrentTaskTitle => CurrentTask?.Title ?? "No task selected";
    public string CycleDots => string.Join(" ", Enumerable.Range(0, Settings.LongBreakInterval).Select(i => i < _cycle ? "●" : "○"));
    public int TodayCount => Stats.Today(History).Pomodoros;
    public string TodayText => $"#{TodayCount}";

    /// "3 Pomodoros remaining · Est. finish 12:20 PM" for the task panel, deliberately not on the widget.
    public string PendingEstimate
    {
        get
        {
            int left = Tasks.Where(t => !t.Done).Sum(t => Math.Max(0, t.Estimated - t.Completed));
            if (left == 0) return "Nothing pending";
            int minutes = left * Settings.FocusMinutes + (left - 1) * Settings.ShortBreakMinutes;
            return $"{Stats.Plural(left, "Pomodoro")} remaining · Est. finish {DateTime.Now.AddMinutes(minutes):t}";
        }
    }

    // ---- settings the widget edits directly; everything else binds through Settings.* ----
    public bool AlwaysOnTop
    {
        get => Settings.AlwaysOnTop;
        set { Settings.AlwaysOnTop = value; Raise(); Storage.SaveSettings(Settings); }
    }

    public bool MiniMode
    {
        get => Settings.MiniMode;
        set { Settings.MiniMode = value; Raise(); Storage.SaveSettings(Settings); }
    }

    /// No-op when the widget has not actually moved, so a plain click costs no disk write.
    public void SaveWindowPosition(double left, double top)
    {
        if (Settings.WindowLeft == left && Settings.WindowTop == top) return;
        Settings.WindowLeft = left;
        Settings.WindowTop = top;
        Storage.SaveSettings(Settings);
    }

    // ---- commands ----
    public void StartPause()
    {
        if (IsRunning) { _timer.Pause(); return; }
        StopAlert();
        Start();
    }

    private void Start()
    {
        var from = Remaining <= TimeSpan.Zero ? Total : Remaining;
        if (from == Total) _startedAt = DateTime.Now;
        _timer.Start(from);
    }

    public void Reset() => SetMode(Mode);

    public void Skip()
    {
        StopAlert();
        Advance();
    }

    public void SetMode(TimerMode mode)
    {
        StopAlert();
        Mode = mode;
        _timer.Reset(Total);
    }

    public void TestAlarm() => _audio.Play(Settings.Alarm, Settings.AlarmVolume, 1);
    public void StopAlarm() => _audio.Stop();

    private void StopAlert()
    {
        _audio.Stop();
        Finished = false;
    }

    private void OnCompleted()
    {
        var finished = Mode;
        bool wasFocus = finished == TimerMode.Focus;
        if (wasFocus)
        {
            History.Add(new PomodoroSession
            {
                StartedAt = _startedAt,
                CompletedAt = DateTime.Now,
                DurationMinutes = Settings.FocusMinutes,
                Mode = TimerMode.Focus,
                TaskId = CurrentTask?.Id,
            });
            Storage.SaveHistory(History);
            RaiseAll(nameof(TodayCount), nameof(TodayText));
            if (CurrentTask != null) { CurrentTask.Completed++; SaveTasks(); }
        }

        Advance();
        _audio.Play(Settings.Alarm, Settings.AlarmVolume, Settings.AlarmRepeat);

        if (Settings.DesktopNotifications)
            Notify?.Invoke(
                wasFocus ? "🍅 Focus session complete" : "☕ Break finished",
                wasFocus ? $"Nice work. Time for a {Settings.MinutesFor(Mode)}-minute break." : "Ready for another focus session?");

        if (wasFocus ? Settings.AutoStartBreaks : Settings.AutoStartFocus) Start();
        else if (Settings.FlashOnFinish) Finished = true;
    }

    /// Focus -> Short/Long break -> Focus. Long break every N focus sessions, then the cycle resets.
    private void Advance()
    {
        if (Mode == TimerMode.Focus)
        {
            _cycle++;
            Mode = _cycle % Settings.LongBreakInterval == 0 ? TimerMode.LongBreak : TimerMode.ShortBreak;
        }
        else
        {
            if (Mode == TimerMode.LongBreak) _cycle = 0;
            Mode = TimerMode.Focus;
        }
        _timer.Reset(Total);
        Raise(nameof(CycleDots));
    }

    // ---- tasks ----
    public void AddTask(string title, int estimated)
    {
        Tasks.Add(new TaskItem { Title = title.Trim(), Estimated = Math.Max(1, estimated) });
        SaveTasks();
    }

    public void RemoveTask(TaskItem task)
    {
        if (ReferenceEquals(CurrentTask, task)) CurrentTask = null;
        Tasks.Remove(task);
        SaveTasks();
    }

    /// Every task mutation funnels through here, so this is where the estimate is refreshed.
    public void SaveTasks()
    {
        Storage.SaveTasks(Tasks.ToList());
        Raise(nameof(PendingEstimate));
    }

    // ---- settings ----
    public void ApplySettings()
    {
        Settings.Sanitize();
        Storage.SaveSettings(Settings);
        try { WinIntegration.SetLaunchAtStartup(Settings.LaunchAtStartup); } catch { /* registry locked down: ignore */ }
        // Raising the root re-resolves every Settings.* binding, so a new display setting needs no mirror here.
        RaiseAll(nameof(Settings), nameof(AlwaysOnTop), nameof(MiniMode), nameof(CycleDots), nameof(PendingEstimate));
        if (!IsRunning && !Finished) _timer.Reset(Total); // pick up new durations
        Raise(nameof(Progress));
        SettingsApplied?.Invoke();
    }
}
