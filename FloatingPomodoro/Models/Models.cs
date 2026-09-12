using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Runtime.CompilerServices;

namespace FloatingPomodoro.Models;

public enum TimerMode { Focus, ShortBreak, LongBreak }

public class Observable : INotifyPropertyChanged
{
    public event PropertyChangedEventHandler? PropertyChanged;

    protected bool Set<T>(ref T field, T value, [CallerMemberName] string? name = null)
    {
        if (EqualityComparer<T>.Default.Equals(field, value)) return false;
        field = value;
        Raise(name);
        return true;
    }

    protected void Raise([CallerMemberName] string? name = null) =>
        PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(name));

    protected void RaiseAll(params string[] names)
    {
        foreach (var n in names) Raise(n);
    }
}

public class AppSettings
{
    public int FocusMinutes { get; set; } = 25;
    public int ShortBreakMinutes { get; set; } = 5;
    public int LongBreakMinutes { get; set; } = 15;
    public int LongBreakInterval { get; set; } = 4;
    public bool AutoStartBreaks { get; set; }
    public bool AutoStartFocus { get; set; }

    public string Theme { get; set; } = "System"; // System | Light | Dark
    public bool ShowTaskName { get; set; } = true;
    public bool ShowProgressBar { get; set; } = true;
    public bool AlwaysOnTop { get; set; } = true;
    public double Opacity { get; set; } = 1.0;   // 0.7 - 1.0
    public bool MiniMode { get; set; }

    public string Alarm { get; set; } = "soft-bell.wav";
    public double AlarmVolume { get; set; } = 0.7;
    public int AlarmRepeat { get; set; } = 3;
    public bool DesktopNotifications { get; set; } = true;
    public bool FlashOnFinish { get; set; } = true;

    public bool LaunchAtStartup { get; set; }
    public bool MinimizeToTray { get; set; } = true;

    public double? WindowLeft { get; set; }
    public double? WindowTop { get; set; }

    public int MinutesFor(TimerMode mode) => mode switch
    {
        TimerMode.ShortBreak => ShortBreakMinutes,
        TimerMode.LongBreak => LongBreakMinutes,
        _ => FocusMinutes,
    };

    /// Clamp anything a hand-edited settings.json or a typo could break.
    public void Sanitize()
    {
        FocusMinutes = Math.Clamp(FocusMinutes, 1, 180);
        ShortBreakMinutes = Math.Clamp(ShortBreakMinutes, 1, 60);
        LongBreakMinutes = Math.Clamp(LongBreakMinutes, 1, 120);
        LongBreakInterval = Math.Clamp(LongBreakInterval, 1, 12);
        Opacity = Math.Clamp(Opacity, 0.7, 1.0);
        AlarmVolume = Math.Clamp(AlarmVolume, 0, 1);
        AlarmRepeat = Math.Clamp(AlarmRepeat, 1, 10);
        if (Theme is not ("System" or "Light" or "Dark")) Theme = "System";
    }
}

public class TaskItem : Observable
{
    private string _title = "";
    private int _estimated = 1, _completed;
    private bool _done;

    public string Id { get; set; } = Guid.NewGuid().ToString("N");
    public string Title { get => _title; set => Set(ref _title, value); }
    public int Estimated { get => _estimated; set => Set(ref _estimated, value); }
    public int Completed { get => _completed; set => Set(ref _completed, value); }
    public bool Done { get => _done; set => Set(ref _done, value); }
    public DateTime CreatedAt { get; set; } = DateTime.Now;
}

public class PomodoroSession
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public DateTime StartedAt { get; set; }
    public DateTime CompletedAt { get; set; }
    public int DurationMinutes { get; set; }
    public TimerMode Mode { get; set; }
    public string? TaskId { get; set; }
}
