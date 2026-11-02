// Language: C#
// Filename: HealthManager.cs
// Runs on: Windows 10 / Windows 11 (.NET 9.0-windows)
// Targets: Health, Hydration, Eye Care (20-20-20), Box Breathing & Ergonomics

using System;
using System.ComponentModel;
using System.Runtime.CompilerServices;
using System.Windows.Threading;

namespace DynamicIsland
{
    public class HealthManager : INotifyPropertyChanged
    {
        private static HealthManager? _instance;
        public static HealthManager Instance => _instance ??= new HealthManager();

        private readonly DispatcherTimer _timer;

        private int _waterMinutesLeft = 45;
        private int _eyeMinutesLeft = 20;
        private bool _isWaterEnabled = true;
        private bool _isEyeEnabled = true;

        private string _waterStatus = "💧 Minum air: 45m lagi";
        private string _eyeStatus = "👀 Istirahat mata: 20m lagi";

        // Hydration tracker (daily intake)
        private int _dailyWaterMl = 1250;
        private int _targetWaterMl = 2000;

        // Sedentary & Sitting tracker
        private int _sittingMinutes = 48;

        // Eye break 20-20-20 interactive countdown
        private bool _isEyeBreakActive = false;
        private int _eyeBreakCountdown = 20;
        private readonly DispatcherTimer _eyeBreakTimer;

        // Breathing session state (Box Breathing)
        private bool _isBreathingActive = false;
        private string _breathPrompt = "Tarik napas...";
        private int _breathCount = 4;
        private int _breathPhase = 0; // 0: inhale, 1: hold, 2: exhale
        private readonly DispatcherTimer _breathTimer;

        public event PropertyChangedEventHandler? PropertyChanged;

        public string WaterStatus
        {
            get => _waterStatus;
            private set { _waterStatus = value; OnPropertyChanged(); }
        }

        public string EyeStatus
        {
            get => _eyeStatus;
            private set { _eyeStatus = value; OnPropertyChanged(); }
        }

        public int DailyWaterMl
        {
            get => _dailyWaterMl;
            private set
            {
                _dailyWaterMl = value;
                OnPropertyChanged();
                OnPropertyChanged(nameof(WaterProgressText));
                OnPropertyChanged(nameof(WaterProgressPercent));
            }
        }

        public int TargetWaterMl => _targetWaterMl;
        public string WaterProgressText => $"{DailyWaterMl} / {TargetWaterMl} ml";
        public double WaterProgressPercent
        {
            get => Math.Min(100.0, Math.Round((double)DailyWaterMl / TargetWaterMl * 100.0, 1));
            set { }
        }

        public int SittingMinutes
        {
            get => _sittingMinutes;
            private set
            {
                _sittingMinutes = value;
                OnPropertyChanged();
                OnPropertyChanged(nameof(SittingStatusText));
            }
        }

        public string SittingStatusText
        {
            get
            {
                int hours = _sittingMinutes / 60;
                int mins = _sittingMinutes % 60;
                return hours > 0 ? $"🪑 Duduk: {hours}j {mins}m" : $"🪑 Duduk: {mins}m";
            }
        }

        public bool IsWaterEnabled
        {
            get => _isWaterEnabled;
            set { _isWaterEnabled = value; OnPropertyChanged(); }
        }

        public bool IsEyeEnabled
        {
            get => _isEyeEnabled;
            set { _isEyeEnabled = value; OnPropertyChanged(); }
        }

        public bool IsBreathingActive
        {
            get => _isBreathingActive;
            private set { _isBreathingActive = value; OnPropertyChanged(); }
        }

        public string BreathPrompt
        {
            get => _breathPrompt;
            private set { _breathPrompt = value; OnPropertyChanged(); }
        }

        public bool IsEyeBreakActive
        {
            get => _isEyeBreakActive;
            private set { _isEyeBreakActive = value; OnPropertyChanged(); }
        }

        public int EyeBreakCountdown
        {
            get => _eyeBreakCountdown;
            private set { _eyeBreakCountdown = value; OnPropertyChanged(); }
        }

        public HealthManager()
        {
            _timer = new DispatcherTimer { Interval = TimeSpan.FromMinutes(1) };
            _timer.Tick += (s, e) => TickMinute();
            _timer.Start();

            _breathTimer = new DispatcherTimer { Interval = TimeSpan.FromSeconds(1) };
            _breathTimer.Tick += (s, e) => TickBreath();

            _eyeBreakTimer = new DispatcherTimer { Interval = TimeSpan.FromSeconds(1) };
            _eyeBreakTimer.Tick += (s, e) => TickEyeBreak();
        }

        private void TickMinute()
        {
            SittingMinutes++;

            if (SittingMinutes % 60 == 0)
            {
                NotificationManager.Instance?.ShowNotification(
                    "🧘 Waktunya Peregangan!",
                    "Kamu sudah duduk selama 1 jam. Berdiri dan regangkan punggungmu sebentar.",
                    "Dynamic Island Health");
                SoundManager.Play(AtollSound.Feedback);
            }

            if (_isWaterEnabled)
            {
                _waterMinutesLeft--;
                if (_waterMinutesLeft <= 0)
                {
                    _waterMinutesLeft = 45;
                    NotificationManager.Instance?.ShowNotification(
                        "💧 Waktunya Minum Air!",
                        "Jaga hidrasi tubuh kamu agar tetap fokus dan segar.",
                        "Dynamic Island Health");
                    SoundManager.Play(AtollSound.Feedback);
                }
                WaterStatus = $"💧 Minum air: {_waterMinutesLeft}m lagi";
            }

            if (_isEyeEnabled)
            {
                _eyeMinutesLeft--;
                if (_eyeMinutesLeft <= 0)
                {
                    _eyeMinutesLeft = 20;
                    NotificationManager.Instance?.ShowNotification(
                        "👀 Aturan 20-20-20",
                        "Pandang objek sejauh 6 meter (20 kaki) selama 20 detik untuk relaksasi mata.",
                        "Dynamic Island Health");
                    SoundManager.Play(AtollSound.Feedback);
                }
                EyeStatus = $"👀 Istirahat mata: {_eyeMinutesLeft}m lagi";
            }
        }

        public void DrinkWaterNow()
        {
            DailyWaterMl += 250;
            _waterMinutesLeft = 45;
            WaterStatus = $"💧 Air diminum! ({_waterMinutesLeft}m lagi)";
            SoundManager.Play(AtollSound.Feedback);
        }

        public void ResetSittingTime()
        {
            SittingMinutes = 0;
            SoundManager.Play(AtollSound.Feedback);
        }

        public void StartEyeBreak()
        {
            if (IsEyeBreakActive)
            {
                IsEyeBreakActive = false;
                _eyeBreakTimer.Stop();
                EyeStatus = "👀 Istirahat mata selesai!";
                return;
            }

            IsEyeBreakActive = true;
            EyeBreakCountdown = 20;
            EyeStatus = $"👀 Tatap 6 meter... ({EyeBreakCountdown}s)";
            _eyeBreakTimer.Start();
            SoundManager.Play(AtollSound.Feedback);
        }

        private void TickEyeBreak()
        {
            EyeBreakCountdown--;
            if (EyeBreakCountdown <= 0)
            {
                _eyeBreakTimer.Stop();
                IsEyeBreakActive = false;
                _eyeMinutesLeft = 20;
                EyeStatus = "👀 Mata rileks! (20m lagi)";
                SoundManager.Play(AtollSound.Dynamic);
            }
            else
            {
                EyeStatus = $"👀 Tatap 6 meter... ({EyeBreakCountdown}s)";
            }
        }

        public void StartBreathing()
        {
            _isBreathingActive = true;
            _breathCount = 4;
            _breathPhase = 0;
            BreathPrompt = "Tarik napas... (4s)";
            OnPropertyChanged(nameof(IsBreathingActive));
            _breathTimer.Start();
            SoundManager.Play(AtollSound.Feedback);
        }

        public void StopBreathing()
        {
            _isBreathingActive = false;
            _breathTimer.Stop();
            BreathPrompt = "Selesai! Tubuh lebih rileks.";
            OnPropertyChanged(nameof(IsBreathingActive));
            SoundManager.Play(AtollSound.Feedback);
        }

        public void ToggleBreathing()
        {
            if (IsBreathingActive) StopBreathing();
            else StartBreathing();
        }

        private void TickBreath()
        {
            _breathCount--;
            if (_breathCount <= 0)
            {
                _breathPhase = (_breathPhase + 1) % 3;
                _breathCount = 4;
                BreathPrompt = _breathPhase switch
                {
                    0 => "Tarik napas... (4s)",
                    1 => "Tahan napas... (4s)",
                    2 => "Hembuskan perlahan... (4s)",
                    _ => "Bernapas rileks"
                };
            }
            else
            {
                string action = _breathPhase switch
                {
                    0 => "Tarik napas",
                    1 => "Tahan napas",
                    2 => "Hembuskan perlahan",
                    _ => "Napas"
                };
                BreathPrompt = $"{action}... ({_breathCount}s)";
            }
        }

        private void OnPropertyChanged([CallerMemberName] string? name = null)
        {
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(name));
        }
    }
}
