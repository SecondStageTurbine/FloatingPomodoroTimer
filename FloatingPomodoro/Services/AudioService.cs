using System;
using System.IO;
using System.Linq;
using System.Windows.Media;

namespace FloatingPomodoro.Services;

public class AudioService
{
    public static readonly string SoundDir = Path.Combine(Storage.Dir, "sounds");

    /// The alarms are embedded in the exe so it ships as one downloadable file.
    /// Unpack them once beside the settings, where the user can also drop their own .wav files.
    static AudioService()
    {
        try
        {
            var asm = typeof(AudioService).Assembly;
            const string prefix = "FloatingPomodoro.Resources.";
            Directory.CreateDirectory(SoundDir);
            foreach (var res in asm.GetManifestResourceNames())
            {
                if (!res.StartsWith(prefix, StringComparison.Ordinal) || !res.EndsWith(".wav", StringComparison.OrdinalIgnoreCase)) continue;
                var file = Path.Combine(SoundDir, res[prefix.Length..]);
                if (File.Exists(file)) continue;
                using var src = asm.GetManifestResourceStream(res)!;
                using var dst = File.Create(file);
                src.CopyTo(dst);
            }
        }
        catch { /* a silent alarm beats no app */ }
    }

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
