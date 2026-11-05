// Language: C#
// Filename: ScreenTimeManager.cs
// Runs on: Windows 10 / Windows 11 (.NET 9.0-windows10.0.19041.0)
// Targets: Dynamic Island Screen Time & App Usage Tracker (Poni Reference)

using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Runtime.CompilerServices;
using System.Text.Json;
using System.Windows.Media;
using System.Windows.Threading;

namespace DynamicIsland
{
    public class AppUsageItem : INotifyPropertyChanged
    {
        private string _appName = string.Empty;
        private ImageSource? _appIcon;
        private double _progressPercent;
        private string _durationText = string.Empty;
        private int _secondsUsed;

        public string AppName
        {
            get => _appName;
            set { _appName = value; OnPropertyChanged(); }
        }

        public ImageSource? AppIcon
        {
            get => _appIcon;
            set { _appIcon = value; OnPropertyChanged(); }
        }

        public double ProgressPercent
        {
            get => _progressPercent;
            set { _progressPercent = value; OnPropertyChanged(); }
        }

        public string DurationText
        {
            get => _durationText;
            set { _durationText = value; OnPropertyChanged(); }
        }

        public int SecondsUsed
        {
            get => _secondsUsed;
            set { _secondsUsed = value; OnPropertyChanged(); }
        }

        public event PropertyChangedEventHandler? PropertyChanged;
        protected void OnPropertyChanged([CallerMemberName] string? name = null) =>
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(name));
    }

    /// <summary>
    /// Tracks real foreground-app usage per day (only while the user is active),
    /// keeps 14 days of history on disk and warns once when the daily limit is reached.
    /// </summary>
    public class ScreenTimeManager : INotifyPropertyChanged
    {
        private static ScreenTimeManager? _instance;
        public static ScreenTimeManager Instance => _instance ??= new ScreenTimeManager();

        private static readonly string HistoryFile = Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData), "DynamicIslandPro", "screentime.json");

        private static readonly HashSet<string> SystemProcs = new(StringComparer.OrdinalIgnoreCase)
        {
            "explorer", "Taskmgr", "SystemSettings", "ShellExperienceHost", "StartMenuExperienceHost",
            "SearchHost", "searchapp", "LockApp", "ApplicationFrameHost", "mmc", "control"
        };

        private sealed class DayData
        {
            public Dictionary<string, int> Apps { get; set; } = new(StringComparer.OrdinalIgnoreCase);
            public int[] Hours { get; set; } = new int[24];
            public int Switches { get; set; }
        }

        private readonly DispatcherTimer _timer;
        private Dictionary<string, DayData> _history = new();
        private string _today = DateTime.Today.ToString("yyyy-MM-dd");
        private string _lastActiveProcess = "";
        private bool _is7DaysFilter;
        private bool _limitWarned;
        private int _tick;
        private readonly Dictionary<string, ImageSource?> _iconCache = new(StringComparer.OrdinalIgnoreCase);

        public ObservableCollection<AppUsageItem> MostUsedApps { get; } = new();

        private DayData Today
        {
            get
            {
                string key = DateTime.Today.ToString("yyyy-MM-dd");
                if (key != _today)
                {
                    _today = key;
                    _limitWarned = false;
                }
                if (!_history.TryGetValue(_today, out var d))
                {
                    d = new DayData();
                    _history[_today] = d;
                }
                return d;
            }
        }

        private IEnumerable<DayData> SelectedDays =>
            _is7DaysFilter
                ? Enumerable.Range(0, 7).Select(i => DateTime.Today.AddDays(-i).ToString("yyyy-MM-dd"))
                      .Where(_history.ContainsKey).Select(k => _history[k])
                : new[] { Today };

        private int SumSeconds(Func<string, bool> filter) =>
            SelectedDays.SelectMany(d => d.Apps).Where(kv => filter(kv.Key)).Sum(kv => kv.Value);

        public string TotalTimeFormatted => Format(SumSeconds(_ => true));
        public string OtherTimeFormatted => Format(SumSeconds(p => !SystemProcs.Contains(p)));
        public string SystemTimeFormatted => Format(SumSeconds(p => SystemProcs.Contains(p)));
        public string PeriodLabel => _is7DaysFilter ? "7 hari" : "Hari ini";

        public string SwitchesSummaryText
        {
            get
            {
                int sw = SelectedDays.Sum(d => d.Switches);
                string top = SelectedDays.SelectMany(d => d.Apps)
                    .GroupBy(kv => kv.Key, StringComparer.OrdinalIgnoreCase)
                    .OrderByDescending(g => g.Sum(kv => kv.Value)).Select(g => g.Key).FirstOrDefault() ?? "-";
                return $"{sw} pindah aplikasi · terbanyak: {top}";
            }
        }

        // Last 5 hours of today's activity, as bar heights (max 22 px)
        public double Bar0 => BarHeight(4);
        public double Bar1 => BarHeight(3);
        public double Bar2 => BarHeight(2);
        public double Bar3 => BarHeight(1);
        public double Bar4 => BarHeight(0);

        private double BarHeight(int hoursAgo)
        {
            int h = DateTime.Now.Hour - hoursAgo;
            if (h < 0) return 3;
            return Math.Max(3, Today.Hours[h] / 3600.0 * 22.0);
        }

        public bool Is7DaysFilter
        {
            get => _is7DaysFilter;
            set
            {
                if (_is7DaysFilter != value)
                {
                    _is7DaysFilter = value;
                    OnPropertyChanged();
                    RaiseTotals();
                    RefreshAppList();
                }
            }
        }

        public ScreenTimeManager()
        {
            Load();
            RefreshAppList();

            _timer = new DispatcherTimer { Interval = TimeSpan.FromSeconds(1) };
            _timer.Tick += Timer_Tick;
            _timer.Start();
        }

        private void Timer_Tick(object? sender, EventArgs e)
        {
            try
            {
                if (SettingsManager.Instance?.TrackScreenTime == false) return;
                if (HealthManager.IdleSeconds() >= 120) return; // away from the PC: not screen time

                var hwnd = NativeMethods.GetForegroundWindow();
                if (hwnd == IntPtr.Zero) return;
                NativeMethods.GetWindowThreadProcessId(hwnd, out uint pid);
                if (pid == 0) return;

                using var proc = Process.GetProcessById((int)pid);
                var pName = proc.ProcessName;
                if (string.Equals(pName, "Idle", StringComparison.OrdinalIgnoreCase)) return;
                if (string.Equals(pName, "DynamicIsland", StringComparison.OrdinalIgnoreCase)) pName = "Dynamic Island";

                var day = Today;
                if (!string.Equals(_lastActiveProcess, pName, StringComparison.OrdinalIgnoreCase))
                {
                    if (_lastActiveProcess.Length > 0) day.Switches++;
                    _lastActiveProcess = pName;
                }

                day.Apps[pName] = day.Apps.TryGetValue(pName, out int s) ? s + 1 : 1;
                day.Hours[DateTime.Now.Hour]++;

                _tick++;
                if (_tick % 5 == 0)
                {
                    RaiseTotals();
                    RefreshAppList();
                    CheckLimit();
                }
                if (_tick % 60 == 0) Save();
            }
            catch { }
        }

        private void CheckLimit()
        {
            int limitH = SettingsManager.Instance?.DailyScreenLimitHours ?? 0;
            if (limitH <= 0 || _limitWarned) return;
            int todaySec = Today.Apps.Values.Sum();
            if (todaySec >= limitH * 3600)
            {
                _limitWarned = true;
                NotificationManager.Instance?.ShowNotification(
                    "Batas Waktu Layar Tercapai",
                    $"Kamu sudah {Format(todaySec)} di depan layar hari ini (batas {limitH} jam). Saatnya istirahat.",
                    "Screen Time", "#5E5CE6");
            }
        }

        public void SetFilter(bool is7Days) => Is7DaysFilter = is7Days;

        private void RaiseTotals()
        {
            OnPropertyChanged(nameof(TotalTimeFormatted));
            OnPropertyChanged(nameof(OtherTimeFormatted));
            OnPropertyChanged(nameof(SystemTimeFormatted));
            OnPropertyChanged(nameof(SwitchesSummaryText));
            OnPropertyChanged(nameof(PeriodLabel));
            OnPropertyChanged(nameof(Bar0)); OnPropertyChanged(nameof(Bar1)); OnPropertyChanged(nameof(Bar2));
            OnPropertyChanged(nameof(Bar3)); OnPropertyChanged(nameof(Bar4));
        }

        private void RefreshAppList()
        {
            var sorted = SelectedDays.SelectMany(d => d.Apps)
                .GroupBy(kv => kv.Key, StringComparer.OrdinalIgnoreCase)
                .Select(g => new { Name = g.Key, Sec = g.Sum(kv => kv.Value) })
                .OrderByDescending(x => x.Sec).Take(5).ToList();

            MostUsedApps.Clear();
            if (sorted.Count == 0) return;
            int maxSec = Math.Max(sorted[0].Sec, 1);

            foreach (var x in sorted)
            {
                if (!_iconCache.TryGetValue(x.Name, out var icon))
                {
                    icon = TryGetProcessIcon(x.Name);
                    if (icon != null) _iconCache[x.Name] = icon;
                }
                MostUsedApps.Add(new AppUsageItem
                {
                    AppName = x.Name,
                    AppIcon = icon,
                    SecondsUsed = x.Sec,
                    ProgressPercent = Math.Min(100.0, (double)x.Sec / maxSec * 100.0),
                    DurationText = Format(x.Sec)
                });
            }
        }

        private static string Format(int seconds)
        {
            if (seconds >= 3600) return $"{seconds / 3600}j {seconds % 3600 / 60}m";
            if (seconds >= 60) return $"{seconds / 60}m";
            return $"{seconds}d";
        }

        private static ImageSource? TryGetProcessIcon(string processName)
        {
            try
            {
                var procs = Process.GetProcessesByName(processName);
                if (procs.Length > 0 && procs[0].MainModule?.FileName is string exePath && File.Exists(exePath))
                {
                    return StartbarManager.GetIconFromPath(exePath);
                }
            }
            catch { }
            return null;
        }

        // ---------- persistence ----------
        private void Load()
        {
            try
            {
                if (!File.Exists(HistoryFile)) return;
                var data = JsonSerializer.Deserialize<Dictionary<string, DayData>>(File.ReadAllText(HistoryFile));
                if (data == null) return;
                foreach (var d in data.Values)
                {
                    d.Apps = new Dictionary<string, int>(d.Apps ?? new(), StringComparer.OrdinalIgnoreCase);
                    if (d.Hours == null || d.Hours.Length != 24) d.Hours = new int[24];
                }
                _history = data;
            }
            catch { }
        }

        public void Save()
        {
            try
            {
                // keep two weeks
                string cutoff = DateTime.Today.AddDays(-14).ToString("yyyy-MM-dd");
                foreach (var k in _history.Keys.Where(k => string.CompareOrdinal(k, cutoff) < 0).ToList())
                    _history.Remove(k);

                Directory.CreateDirectory(Path.GetDirectoryName(HistoryFile)!);
                File.WriteAllText(HistoryFile, JsonSerializer.Serialize(_history));
            }
            catch { }
        }

        public event PropertyChangedEventHandler? PropertyChanged;
        protected void OnPropertyChanged([CallerMemberName] string? name = null) =>
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(name));
    }
}
