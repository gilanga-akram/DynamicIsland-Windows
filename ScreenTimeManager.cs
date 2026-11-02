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
using System.Runtime.InteropServices;
using System.Windows.Media;
using System.Windows.Media.Imaging;
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

    public class ScreenTimeManager : INotifyPropertyChanged
    {
        private static ScreenTimeManager? _instance;
        public static ScreenTimeManager Instance => _instance ??= new ScreenTimeManager();

        private readonly DispatcherTimer _timer;
        private readonly Dictionary<string, int> _appSeconds = new(StringComparer.OrdinalIgnoreCase);
        private int _totalSeconds = 360; // default 6 minutes
        private int _otherSeconds = 240; // 4 minutes
        private int _systemSeconds = 120; // 2 minutes
        private int _switchCount = 62;
        private string _lastActiveProcess = "Antigravity";
        private bool _is7DaysFilter = false;

        public ObservableCollection<AppUsageItem> MostUsedApps { get; } = new();

        public string TotalTimeFormatted => $"{_totalSeconds / 60}m";
        public string OtherTimeFormatted => $"{_otherSeconds / 60}m";
        public string SystemTimeFormatted => $"{_systemSeconds / 60}m";
        public string SwitchesSummaryText => $"{_switchCount} app switches · more: {_lastActiveProcess}";

        public bool Is7DaysFilter
        {
            get => _is7DaysFilter;
            set
            {
                if (_is7DaysFilter != value)
                {
                    _is7DaysFilter = value;
                    OnPropertyChanged();
                    RefreshAppList();
                }
            }
        }

        public ScreenTimeManager()
        {
            // Seed initial items matching Poni reference
            _appSeconds["Antigravity"] = 420; // 7m
            _appSeconds["onisshortcut"] = 120; // 2m
            _appSeconds["Poni"] = 60; // 1m
            _appSeconds["searchhost"] = 4; // 4s
            _appSeconds["Windows Explorer"] = 1; // 1s

            RefreshAppList();

            _timer = new DispatcherTimer { Interval = TimeSpan.FromSeconds(1) };
            _timer.Tick += Timer_Tick;
            _timer.Start();
        }

        private void Timer_Tick(object? sender, EventArgs e)
        {
            try
            {
                var hwnd = NativeMethods.GetForegroundWindow();
                if (hwnd != IntPtr.Zero)
                {
                    NativeMethods.GetWindowThreadProcessId(hwnd, out uint pid);
                    if (pid > 0)
                    {
                        using var proc = Process.GetProcessById((int)pid);
                        var pName = proc.ProcessName;
                        if (!string.Equals(pName, "DynamicIsland", StringComparison.OrdinalIgnoreCase) &&
                            !string.Equals(pName, "Idle", StringComparison.OrdinalIgnoreCase))
                        {
                            if (!string.Equals(_lastActiveProcess, pName, StringComparison.OrdinalIgnoreCase))
                            {
                                _switchCount++;
                                _lastActiveProcess = pName;
                                OnPropertyChanged(nameof(SwitchesSummaryText));
                            }

                            if (!_appSeconds.ContainsKey(pName))
                            {
                                _appSeconds[pName] = 0;
                            }
                            _appSeconds[pName]++;
                            _totalSeconds++;

                            if (pName.Equals("explorer", StringComparison.OrdinalIgnoreCase) ||
                                pName.Equals("Taskmgr", StringComparison.OrdinalIgnoreCase) ||
                                pName.Equals("SystemSettings", StringComparison.OrdinalIgnoreCase))
                            {
                                _systemSeconds++;
                            }
                            else
                            {
                                _otherSeconds++;
                            }

                            if (_totalSeconds % 5 == 0)
                            {
                                OnPropertyChanged(nameof(TotalTimeFormatted));
                                OnPropertyChanged(nameof(OtherTimeFormatted));
                                OnPropertyChanged(nameof(SystemTimeFormatted));
                                RefreshAppList();
                            }
                        }
                    }
                }
            }
            catch { }
        }

        public void SetFilter(bool is7Days)
        {
            Is7DaysFilter = is7Days;
        }

        private void RefreshAppList()
        {
            var sorted = _appSeconds.OrderByDescending(kv => kv.Value).Take(5).ToList();
            if (sorted.Count == 0) return;

            int maxSec = Math.Max(sorted[0].Value, 1);
            MostUsedApps.Clear();

            foreach (var kv in sorted)
            {
                double pct = Math.Min(100.0, ((double)kv.Value / maxSec) * 100.0);
                string durStr;
                if (kv.Value >= 60)
                {
                    durStr = $"{kv.Value / 60}m";
                }
                else
                {
                    durStr = $"{kv.Value}s";
                }

                ImageSource? icon = TryGetProcessIcon(kv.Key);

                MostUsedApps.Add(new AppUsageItem
                {
                    AppName = kv.Key,
                    AppIcon = icon,
                    SecondsUsed = kv.Value,
                    ProgressPercent = pct,
                    DurationText = durStr
                });
            }
        }

        private ImageSource? TryGetProcessIcon(string processName)
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

        public event PropertyChangedEventHandler? PropertyChanged;
        protected void OnPropertyChanged([CallerMemberName] string? name = null) =>
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(name));
    }
}
