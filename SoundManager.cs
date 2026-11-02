// Language: C#
// Filename: SoundManager.cs
// Runs on: Windows 10 / Windows 11 (.NET 9.0-windows10.0.19041.0)
// Targets: Dynamic Island Atoll Audio Feedback Engine

using System;
using System.IO;
using System.Windows.Media;

namespace DynamicIsland
{
    public enum AtollSound
    {
        Feedback,
        Dynamic,
        Lock,
        Unlock,
        Timer,
        LowBattery
    }

    public static class SoundManager
    {
        private static readonly string SoundDirectory = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "Assets", "Sounds");
        private static MediaPlayer? _player;
        private static readonly object _lock = new();

        public static bool Enabled { get; set; } = true;

        public static void Play(AtollSound sound)
        {
            if (!Enabled) return;

            string filename = sound switch
            {
                AtollSound.Feedback => "audio-feedback.m4a",
                AtollSound.Dynamic => "dynamic.m4a",
                AtollSound.Lock => "lock.mp3",
                AtollSound.Unlock => "unlock.mp3",
                AtollSound.Timer => "timer.mp3",
                AtollSound.LowBattery => "lowbattery.mp3",
                _ => "audio-feedback.m4a"
            };

            string fullPath = Path.Combine(SoundDirectory, filename);
            if (!File.Exists(fullPath)) return;

            try
            {
                // Execute on UI dispatcher or background player
                System.Windows.Application.Current?.Dispatcher.BeginInvoke(new Action(() =>
                {
                    try
                    {
                        lock (_lock)
                        {
                            _player ??= new MediaPlayer();
                            _player.Open(new Uri(fullPath, UriKind.Absolute));
                            _player.Volume = 0.65;
                            _player.Play();
                        }
                    }
                    catch { }
                }));
            }
            catch { }
        }
    }
}
