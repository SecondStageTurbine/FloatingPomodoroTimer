using System;
using System.Windows.Threading;

namespace FloatingPomodoro.Services;

/// Wall-clock based countdown: stores an end time, never decrements. Survives sleep and UI stalls.
public class TimerService
{
    private readonly DispatcherTimer _timer = new() { Interval = TimeSpan.FromMilliseconds(250) };
    private DateTime _endTime;

    public TimeSpan Remaining { get; private set; }
    public bool IsRunning { get; private set; }

    public event Action<TimeSpan>? Tick;
    public event Action? Completed;

    public TimerService() => _timer.Tick += (_, _) => Poll();

    public void Start(TimeSpan remaining)
    {
        Remaining = Whole(remaining);
        _endTime = DateTime.Now.Add(remaining);
        IsRunning = true;
        _timer.Start();
        Tick?.Invoke(Remaining);
    }

    public void Pause()
    {
        if (IsRunning) Remaining = Whole(_endTime - DateTime.Now);
        IsRunning = false;
        _timer.Stop();
        Tick?.Invoke(Remaining);
    }

    public void Reset(TimeSpan remaining)
    {
        _timer.Stop();
        IsRunning = false;
        Remaining = Whole(remaining);
        Tick?.Invoke(Remaining);
    }

    /// Re-evaluate against the clock. Ticks four times a second so the displayed second is never
    /// more than 250 ms stale, but only reports when the whole second actually changes — otherwise
    /// every consumer re-renders three times for nothing.
    private void Poll()
    {
        if (!IsRunning) return;
        var remaining = Whole(_endTime - DateTime.Now);
        if (remaining != Remaining)
        {
            Remaining = remaining;
            Tick?.Invoke(remaining);
        }
        if (remaining == TimeSpan.Zero)
        {
            _timer.Stop();
            IsRunning = false;
            Completed?.Invoke();
        }
    }

    /// Round up to a whole second, floored at zero: 24:59.8 left still reads 25:00, and the
    /// display only changes when the visible digits do.
    private static TimeSpan Whole(TimeSpan t) =>
        t <= TimeSpan.Zero ? TimeSpan.Zero : TimeSpan.FromSeconds(Math.Ceiling(t.TotalSeconds));
}
