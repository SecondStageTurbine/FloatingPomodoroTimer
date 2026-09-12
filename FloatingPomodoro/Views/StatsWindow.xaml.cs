using System;
using System.Collections.Generic;
using System.Linq;
using System.Windows;
using FloatingPomodoro.Services;
using FloatingPomodoro.ViewModels;

namespace FloatingPomodoro.Views;

public partial class StatsWindow : Window
{
    private record Bar(string Label, double Width, string Text, double Opacity);

    private readonly TimerViewModel _vm;

    public StatsWindow(TimerViewModel vm)
    {
        InitializeComponent();
        _vm = vm;
    }

    private void Window_Activated(object sender, EventArgs e) => Refresh();

    private void Refresh()
    {
        // The ViewModel owns history; re-reading the file here would be a second source of truth.
        var history = _vm.History;
        var today = Stats.Today(history);
        TodayTime.Text = Stats.Hm(today.Minutes);
        TodayPomodoros.Text = $"{Stats.Plural(today.Pomodoros, "Pomodoro")} completed";
        TodayTasks.Text = $"{Stats.Plural(today.Tasks, "task")} worked on";
        var all = Stats.Summary(history, DateTime.MinValue, DateTime.MaxValue);
        TotalLine.Text = $"All time: {Stats.Hm(all.Minutes)} · {Stats.Plural(all.Pomodoros, "Pomodoro")}";

        var week = Stats.Daily(history, 7);
        WeekSummary.Text = $"{Stats.Hm(week.Sum(d => d.Minutes))} · {Stats.Plural(week.Sum(d => d.Pomodoros), "Pomodoro")} this week";
        WeekBars.ItemsSource = Bars(week, d => d.Day.ToString("ddd"));

        var month = Stats.Daily(history, 30);
        MonthSummary.Text = $"{Stats.Hm(month.Sum(d => d.Minutes))} · {Stats.Plural(month.Sum(d => d.Pomodoros), "Pomodoro")} in 30 days";
        MonthBars.ItemsSource = Bars(month, d => d.Day.ToString("dd MMM"));
    }

    private static List<Bar> Bars(List<DayStat> days, Func<DayStat, string> label)
    {
        double max = Math.Max(1, days.Max(d => d.Minutes));
        return days.Select(d => new Bar(
            label(d),
            Math.Max(2, d.Minutes / max * 220),
            d.Minutes == 0 ? "" : Stats.Hm(d.Minutes),
            d.Minutes == 0 ? 0.15 : 1)).ToList();
    }
}
