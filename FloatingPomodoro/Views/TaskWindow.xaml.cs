using System;
using System.Linq;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using FloatingPomodoro.Models;
using FloatingPomodoro.ViewModels;

namespace FloatingPomodoro.Views;

public partial class TaskWindow : Window
{
    private readonly TimerViewModel _vm;
    private int _estimate = 1;

    public TaskWindow(TimerViewModel vm)
    {
        InitializeComponent();
        _vm = vm;
        DataContext = vm;
        Loaded += (_, _) => { Refresh(); NewTitle.Focus(); };
    }

    /// "3 Pomodoros remaining · Est. finish 12:20 PM" — kept out of the floating widget on purpose.
    private void Refresh()
    {
        int remaining = _vm.Tasks.Where(t => !t.Done).Sum(t => Math.Max(0, t.Estimated - t.Completed));
        if (remaining == 0) { Estimate.Text = "Nothing pending"; return; }
        int minutes = remaining * _vm.Settings.FocusMinutes + (remaining - 1) * _vm.Settings.ShortBreakMinutes;
        Estimate.Text = $"{remaining} Pomodoro{(remaining == 1 ? "" : "s")} remaining · Est. finish {DateTime.Now.AddMinutes(minutes):t}";
    }

    private void Window_Activated(object sender, EventArgs e) => Refresh();

    private void Add()
    {
        var title = NewTitle.Text.Trim();
        if (title.Length == 0) return;
        _vm.AddTask(title, _estimate);
        NewTitle.Clear();
        _estimate = 1;
        EstText.Text = "1";
        Refresh();
    }

    private void Add_Click(object sender, RoutedEventArgs e) => Add();

    private void NewTitle_KeyDown(object sender, KeyEventArgs e)
    {
        if (e.Key == Key.Enter) { Add(); e.Handled = true; }
    }

    private void EstMinus_Click(object sender, RoutedEventArgs e) => EstText.Text = (_estimate = Math.Max(1, _estimate - 1)).ToString();
    private void EstPlus_Click(object sender, RoutedEventArgs e) => EstText.Text = (_estimate = Math.Min(50, _estimate + 1)).ToString();

    private void Done_Click(object sender, RoutedEventArgs e)
    {
        _vm.SaveTasks();
        Refresh();
    }

    private void Delete_Click(object sender, RoutedEventArgs e)
    {
        if ((sender as FrameworkElement)?.DataContext is TaskItem t) _vm.RemoveTask(t);
        Refresh();
    }
}
