using System;
using System.Windows;
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
        Loaded += (_, _) => NewTitle.Focus();
    }

    private void Add()
    {
        var title = NewTitle.Text.Trim();
        if (title.Length == 0) return;
        _vm.AddTask(title, _estimate);
        NewTitle.Clear();
        EstText.Text = (_estimate = 1).ToString();
    }

    private void Add_Click(object sender, RoutedEventArgs e) => Add();

    private void NewTitle_KeyDown(object sender, KeyEventArgs e)
    {
        if (e.Key == Key.Enter) { Add(); e.Handled = true; }
    }

    private void EstMinus_Click(object sender, RoutedEventArgs e) => EstText.Text = (_estimate = Math.Max(1, _estimate - 1)).ToString();
    private void EstPlus_Click(object sender, RoutedEventArgs e) => EstText.Text = (_estimate = Math.Min(50, _estimate + 1)).ToString();

    private void Done_Click(object sender, RoutedEventArgs e) => _vm.SaveTasks();

    private void Delete_Click(object sender, RoutedEventArgs e)
    {
        if ((sender as FrameworkElement)?.DataContext is TaskItem t) _vm.RemoveTask(t);
    }
}
