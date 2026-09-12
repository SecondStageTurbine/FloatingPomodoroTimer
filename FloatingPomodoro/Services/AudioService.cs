using System;
using System.IO;
using System.Linq;
using System.Windows.Media;

namespace FloatingPomodoro.Services;

public class AudioService
{
    public static readonly string SoundDir = Path.Combine(AppContext.BaseDirectory, "Resources");

    public static string[] Sounds => Directory.Exists(SoundDir)
        ? Directory.GetFiles(SoundDir, "*.wav").Select(f => Path.GetFileName(f)).OrderBy(f => f).ToArray()
        : Array.Empty<string>();

    private readonly MediaPlayer _player = new();
    private int _repeatsLeft;

    public AudioService() => _player.MediaEnded += (_, _) =>
    {
        if (--_repeatsLeft > 0) { _player.Position = TimeSpan.Zero; _player.Play(); }
    };

    public void Play(string file, double volume, int repeat = 1)
    {
        var path = Path.Combine(SoundDir, file);
        if (!File.Exists(path)) return;
        _repeatsLeft = Math.Max(1, repeat);
        _player.Open(new Uri(path));
        _player.Volume = Math.Clamp(volume, 0, 1);
        _player.Play();
    }

    public void Stop()
    {
        _repeatsLeft = 0;
        _player.Stop();
    }
}
