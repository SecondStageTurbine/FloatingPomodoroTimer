using System;
using System.Collections.ObjectModel;
using System.Linq;
using FloatingPomodoro.Models;
using FloatingPomodoro.Services;

namespace FloatingPomodoro.ViewModels;

public class TimerViewModel : Observable
{
    private readonly TimerService _timer = new();
    private TimerMode _mode;
    private TimeSpan _remaining;
    private bool _isRunning, _finished;
    private int _cycle, _todayCount, _todayMinutes;
    private TaskItem? _currentTask;
    private DateTime _startedAt;

    public AppSettings Settings { get; }
    public ObservableCollection<TaskItem> Tasks { get; }
    public AudioService Audio { get; } = new();

    /// (title, message) for a desktop notification.
    public event Action<string, string>? Notify;
    /// Fired after ApplySettings so the shell can re-theme / re-register.
    public event Action? SettingsApplied;

    public TimerViewModel()
    {
        Settings = Storage.LoadSettings();
        Settings.Sanitize();
        Tasks = new ObservableCollection<TaskItem>(Storage.LoadTasks());
        var today = Stats.Summary(Storage.LoadHistory(), DateTime.Today, DateTime.Today.AddDays(1));
        _todayMinutes = today.Minutes;
        _todayCount = today.Pomodoros;
        _timer.Tick += r => Remaining = r;
        _timer.Completed += OnCompleted;
        _remaining = Total;
    }

    // ---- state ----
    public TimeSpan Total => TimeSpan.FromMinutes(Settings.MinutesFor(Mode));

    public TimerMode Mode
    {
        get => _mode;
        private set { if (Set(ref _mode, value)) RaiseAll(nameof(ModeText), nameof(ModeTitle), nameof(Total)); }
    }

    public TimeSpan Remaining
    {
        get => _remaining;
        private set { if (Set(ref _remaining, value)) RaiseAll(nameof(TimerText), nameof(Progress)); }
    }

    public bool IsRunning
    {
        get => _isRunning;
        private set { if (Set(ref _isRunning, value)) RaiseAll(nameof(PlayGlyph), nameof(StartPauseText)); }
    }

    /// True while flashing after a session ended (until the user interacts).
    public bool Finished { get => _finished; private set => Set(ref _finished, value); }

    public TaskItem? CurrentTask
    {
        get => _currentTask;
        set { if (Set(ref _currentTask, value)) Raise(nameof(CurrentTaskTitle)); }
    }

    public int TodayCount { get => _todayCount; private set { if (Set(ref _todayCount, value)) Raise(nameof(TodayText)); } }
    public int TodayMinutes { get => _todayMinutes; private set => Set(ref _todayMinutes, value); }

    // ---- derived for the UI ----
    public string TimerText => $"{(int)Remaining.TotalMinutes:00}:{Remaining.Seconds:00}";
    public string ModeText => Mode switch { TimerMode.ShortBreak => "SHORT BREAK", TimerMode.LongBreak => "LONG BREAK", _ => "FOCUS" };
    public string ModeTitle => Mode switch { TimerMode.ShortBreak => "Short Break", TimerMode.LongBreak => "Long Break", _ => "Focus" };
    public double Progress => Total.TotalSeconds <= 0 ? 0 : 1 - Remaining.TotalSeconds / Total.TotalSeconds;
    public string PlayGlyph => IsRunning ? "⏸" : "▶";
    public string StartPauseText => IsRunning ? "Pause" : "Start";
    public string CurrentTaskTitle => CurrentTask?.Title ?? "No task selected";
    public string CycleDots => string.Join(" ", Enumerable.Range(0, Settings.LongBreakInterval).Select(i => i < _cycle ? "●" : "○"));
    public string TodayText => $"#{TodayCount}";

    // ---- settings-backed toggles the widget edits directly ----
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

    public double Opacity => Settings.Opacity;
    public bool ShowTaskName => Settings.ShowTaskName;
    public bool ShowProgressBar => Settings.ShowProgressBar;

    // ---- commands ----
    public void StartPause()
    {
        if (IsRunning) { _timer.Pause(); IsRunning = false; return; }
        StopAlert();
        Start();
    }

    private void Start()
    {
        if (Remaining <= TimeSpan.Zero) Remaining = Total;
        if (Remaining == Total) _startedAt = DateTime.Now;
        _timer.Start(Remaining);
        IsRunning = true;
    }

    public void Reset()
    {
        StopAlert();
        _timer.Reset(Total);
        IsRunning = false;
    }

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
        IsRunning = false;
    }

    /// Re-check the clock (e.g. after system resume).
    public void Poll() => _timer.Poll();

    private void StopAlert()
    {
        Audio.Stop();
        Finished = false;
    }

    private void OnCompleted()
    {
        IsRunning = false;
        var finished = Mode;
        if (finished == TimerMode.Focus)
        {
            Storage.AppendHistory(new PomodoroSession
            {
                StartedAt = _startedAt,
                CompletedAt = DateTime.Now,
                DurationMinutes = Settings.FocusMinutes,
                Mode = TimerMode.Focus,
                TaskId = CurrentTask?.Id,
            });
            TodayCount++;
            TodayMinutes += Settings.FocusMinutes;
            if (CurrentTask != null) { CurrentTask.Completed++; SaveTasks(); }
        }

        Advance();
        Audio.Play(Settings.Alarm, Settings.AlarmVolume, Settings.AlarmRepeat);

        bool wasFocus = finished == TimerMode.Focus;
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
        IsRunning = false;
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

    public void SaveTasks() => Storage.SaveTasks(Tasks.ToList());

    // ---- settings ----
    public void ApplySettings()
    {
        Settings.Sanitize();
        Storage.SaveSettings(Settings);
        try { WinIntegration.SetLaunchAtStartup(Settings.LaunchAtStartup); } catch { /* registry locked down: ignore */ }
        RaiseAll(nameof(Opacity), nameof(ShowTaskName), nameof(ShowProgressBar), nameof(AlwaysOnTop), nameof(MiniMode), nameof(CycleDots), nameof(Total));
        if (!IsRunning && !Finished) _timer.Reset(Total); // pick up new durations
        Raise(nameof(Progress));
        SettingsApplied?.Invoke();
    }
}
