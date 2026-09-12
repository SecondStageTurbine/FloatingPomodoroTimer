using System;
using System.Diagnostics;
using System.Windows;
using System.Windows.Input;
using FloatingPomodoro.Services;
using FloatingPomodoro.ViewModels;

namespace FloatingPomodoro.Views;

public partial class SettingsWindow : Window
{
    public static string[] Themes { get; } = { "System", "Light", "Dark" };
    public static int[] Repeats { get; } = { 1, 2, 3, 5, 10 };
    public static string[] Sounds => AudioService.Sounds;

    private readonly TimerViewModel _vm;

    public SettingsWindow(TimerViewModel vm)
    {
        InitializeComponent();
        _vm = vm;
        DataContext = vm;
        DataPath.Text = Storage.Dir;
        VersionText.Text = $"Version {typeof(App).Assembly.GetName().Version?.ToString(3)}";
        Shortcuts.ItemsSource = App.Current.HotkeyLines;
    }

    private void Test_Click(object sender, RoutedEventArgs e) => _vm.TestAlarm();

    private void DataPath_Click(object sender, MouseButtonEventArgs e)
    {
        try
        {
            System.IO.Directory.CreateDirectory(Storage.Dir);
            Process.Start(new ProcessStartInfo("explorer.exe", Storage.Dir) { UseShellExecute = true });
        }
        catch { }
    }

    private void Window_Closed(object sender, EventArgs e)
    {
        _vm.StopAlarm();
        _vm.ApplySettings();
    }
}
