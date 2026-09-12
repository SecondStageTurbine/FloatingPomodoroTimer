using System;
using System.Windows.Threading;

namespace FloatingPomodoro.Services;

/// Wall-clock based countdown: stores EndTime, never decrements. Survives sleep and UI stalls.
public class TimerService
{
    private readonly DispatcherTimer _timer = new() { Interval = TimeSpan.FromMilliseconds(250) };

    public DateTime EndTime { get; private set; }
    public TimeSpan Remaining { get; private set; }
    public bool IsRunning { get; private set; }

    public event Action<TimeSpan>? Tick;
    public event Action? Completed;

    public TimerService() => _timer.Tick += (_, _) => Poll();

    public void Start(TimeSpan remaining)
    {
        Remaining = remaining;
        EndTime = DateTime.Now.Add(remaining);
        IsRunning = true;
        _timer.Start();
        Tick?.Invoke(Remaining);
    }

    public void Pause()
    {
        if (IsRunning) Remaining = Clamp(EndTime - DateTime.Now);
        IsRunning = false;
        _timer.Stop();
        Tick?.Invoke(Remaining);
    }

    public void Reset(TimeSpan remaining)
    {
        _timer.Stop();
        IsRunning = false;
        Remaining = remaining;
        Tick?.Invoke(Remaining);
    }

    /// Re-evaluate against the clock now. Called by the tick and safe to call after resume.
    public void Poll()
    {
        if (!IsRunning) return;
        Remaining = Clamp(EndTime - DateTime.Now);
        Tick?.Invoke(Remaining);
        if (Remaining == TimeSpan.Zero)
        {
            _timer.Stop();
            IsRunning = false;
            Completed?.Invoke();
        }
    }

    private static TimeSpan Clamp(TimeSpan t) => t < TimeSpan.Zero ? TimeSpan.Zero : t;
}
