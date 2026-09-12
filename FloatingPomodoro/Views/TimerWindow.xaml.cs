using System.ComponentModel;
using System.Windows;
using System.Windows.Input;
using FloatingPomodoro.Models;
using FloatingPomodoro.ViewModels;

namespace FloatingPomodoro.Views;

public partial class TimerWindow : Window
{
    private readonly TimerViewModel _vm;

    public TimerWindow(TimerViewModel vm)
    {
        InitializeComponent();
        _vm = vm;
        DataContext = vm;
        Loaded += (_, _) => Place();
    }

    /// Restore the saved position if it is still on a screen, else bottom-right of the work area.
    private void Place()
    {
        var s = _vm.Settings;
        var v = new Rect(SystemParameters.VirtualScreenLeft, SystemParameters.VirtualScreenTop,
                         SystemParameters.VirtualScreenWidth, SystemParameters.VirtualScreenHeight);
        if (s.WindowLeft is double l && s.WindowTop is double t && v.Contains(new Point(l + 40, t + 20)))
        {
            Left = l; Top = t;
            return;
        }
        var wa = SystemParameters.WorkArea;
        Left = wa.Right - ActualWidth - 24;
        Top = wa.Bottom - ActualHeight - 24;
    }

    private void Window_MouseLeftButtonDown(object sender, MouseButtonEventArgs e)
    {
        if (e.ClickCount == 2) { _vm.MiniMode = !_vm.MiniMode; return; }
        DragMove();
        if (IsLoaded) _vm.SaveWindowPosition(Left, Top);
    }

    private void Window_KeyDown(object sender, KeyEventArgs e)
    {
        switch (e.Key)
        {
            case Key.Space: _vm.StartPause(); break;
            case Key.R: _vm.Reset(); break;
            case Key.S: _vm.Skip(); break;
            case Key.M: _vm.MiniMode = !_vm.MiniMode; break;
            case Key.Escape: Hide(); break;
            default: return;
        }
        e.Handled = true;
    }

    private void Window_Closing(object sender, CancelEventArgs e)
    {
        if (App.Current.Exiting) return;
        if (_vm.Settings.MinimizeToTray) { e.Cancel = true; Hide(); }
        else App.Current.ExitApp();
    }

    private void Menu_Click(object sender, RoutedEventArgs e)
    {
        ContextMenu.PlacementTarget = (UIElement)sender;
        ContextMenu.IsOpen = true;
    }

    private void StartPause_Click(object sender, RoutedEventArgs e) => _vm.StartPause();
    private void Reset_Click(object sender, RoutedEventArgs e) => _vm.Reset();
    private void Skip_Click(object sender, RoutedEventArgs e) => _vm.Skip();
    private void ModeFocus_Click(object sender, RoutedEventArgs e) => _vm.SetMode(TimerMode.Focus);
    private void ModeShort_Click(object sender, RoutedEventArgs e) => _vm.SetMode(TimerMode.ShortBreak);
    private void ModeLong_Click(object sender, RoutedEventArgs e) => _vm.SetMode(TimerMode.LongBreak);
    private void Tasks_Click(object sender, RoutedEventArgs e) => App.Current.ShowTasks();
    private void Stats_Click(object sender, RoutedEventArgs e) => App.Current.ShowStats();
    private void Settings_Click(object sender, RoutedEventArgs e) => App.Current.ShowSettings();
    private void Hide_Click(object sender, RoutedEventArgs e) => Hide();
    private void Exit_Click(object sender, RoutedEventArgs e) => App.Current.ExitApp();
}
