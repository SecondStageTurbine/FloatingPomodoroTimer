using System;
using System.Collections.Generic;
using System.Linq;
using FloatingPomodoro.Models;

namespace FloatingPomodoro.Services;

public record DayStat(DateTime Day, int Minutes, int Pomodoros);

public static class Stats
{
    private static IEnumerable<PomodoroSession> Focus(IEnumerable<PomodoroSession> h) =>
        h.Where(s => s.Mode == TimerMode.Focus);

    public static (int Minutes, int Pomodoros, int Tasks) Summary(IEnumerable<PomodoroSession> h, DateTime from, DateTime to)
    {
        var f = Focus(h).Where(s => s.CompletedAt >= from && s.CompletedAt < to).ToList();
        return (f.Sum(s => s.DurationMinutes), f.Count, f.Select(s => s.TaskId).Where(t => t != null).Distinct().Count());
    }

    public static (int Minutes, int Pomodoros, int Tasks) Today(IEnumerable<PomodoroSession> h) =>
        Summary(h, DateTime.Today, DateTime.Today.AddDays(1));

    /// One entry per day for the last `days` days ending today.
    public static List<DayStat> Daily(IEnumerable<PomodoroSession> h, int days)
    {
        var start = DateTime.Today.AddDays(-(days - 1));
        var byDay = Focus(h).Where(s => s.CompletedAt >= start)
            .GroupBy(s => s.CompletedAt.Date)
            .ToDictionary(g => g.Key, g => (Min: g.Sum(s => s.DurationMinutes), N: g.Count()));
        return Enumerable.Range(0, days).Select(i => start.AddDays(i))
            .Select(d => byDay.TryGetValue(d, out var v) ? new DayStat(d, v.Min, v.N) : new DayStat(d, 0, 0))
            .ToList();
    }

    public static string Hm(int minutes) => minutes >= 60 ? $"{minutes / 60}h {minutes % 60}m" : $"{minutes}m";

    public static string Plural(int n, string noun) => $"{n} {noun}{(n == 1 ? "" : "s")}";
}
