using System;
using System.Collections.Generic;
using System.IO;
using System.Text.Json;
using System.Text.Json.Serialization;
using FloatingPomodoro.Models;

namespace FloatingPomodoro.Services;

/// JSON files in %LOCALAPPDATA%\FloatingPomodoro. No database.
public static class Storage
{
    public static readonly string Dir = Path.Combine(
        Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "FloatingPomodoro");

    private static readonly JsonSerializerOptions Json = new()
    {
        WriteIndented = true,
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
        PropertyNameCaseInsensitive = true,
        Converters = { new JsonStringEnumConverter() },
    };

    public static AppSettings LoadSettings() => Load<AppSettings>("settings.json") ?? new AppSettings();
    public static void SaveSettings(AppSettings s) => Save("settings.json", s);

    public static List<TaskItem> LoadTasks() => Load<List<TaskItem>>("tasks.json") ?? new();
    public static void SaveTasks(List<TaskItem> t) => Save("tasks.json", t);

    public static List<PomodoroSession> LoadHistory() => Load<List<PomodoroSession>>("history.json") ?? new();
    public static void AppendHistory(PomodoroSession s)
    {
        var h = LoadHistory();
        h.Add(s);
        Save("history.json", h);
    }

    private static T? Load<T>(string file)
    {
        try
        {
            var p = Path.Combine(Dir, file);
            return File.Exists(p) ? JsonSerializer.Deserialize<T>(File.ReadAllText(p), Json) : default;
        }
        catch { return default; } // ponytail: corrupt file -> defaults; add .bak rotation if anyone reports data loss
    }

    private static void Save<T>(string file, T value)
    {
        try
        {
            Directory.CreateDirectory(Dir);
            var p = Path.Combine(Dir, file);
            File.WriteAllText(p + ".tmp", JsonSerializer.Serialize(value, Json));
            File.Move(p + ".tmp", p, overwrite: true);
        }
        catch { /* disk full / locked: keep running, lose this save */ }
    }
}
