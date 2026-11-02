// Language: C#
// Filename: SettingsManager.cs
// Runs on: Windows 10 / Windows 11 (.NET 9.0-windows10.0.19041.0)
// Targets: Dynamic Island Customization & Persistence (Sizing, Scale, Offset, Behaviors)

using System;
using System.ComponentModel;
using System.IO;
using System.Runtime.CompilerServices;
using System.Text.Json;
using Microsoft.Win32;

namespace DynamicIsland
{
    public class SettingsManager : INotifyPropertyChanged
    {
        private static SettingsManager? _instance;
        public static SettingsManager Instance => _instance ??= new SettingsManager();

        private static readonly string SettingsDir = Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData),
            "DynamicIslandPro");
        private static readonly string SettingsFile = Path.Combine(SettingsDir, "settings.json");

        // 1. DIMENSIONS & SIZING
        private double _compactWidth = 240;
        private double _compactHeight = 36;
        private double _expandedWidth = 680;
        private double _expandedHeight = 144;
        private double _globalScale = 100; // in percent (75% - 130%)

        // 2. POSITION & SCREEN
        private double _positionOffsetX = 0; // -400 to +400 px
        private string _dockPosition = "Top"; // "Top", "Left", "Right"
        private double _dockOffsetY = 0; // -500 to +500 px for side docking
        private double _notchOpacity = 100; // in percent (60% - 100%)
        private bool _alwaysOnTop = true;

        // 3. BEHAVIOR & INTERACTION
        private bool _autoExpandOnHover = true;
        private bool _autoSwitchMusic = true;
        private string _defaultIdleMode = "Cat"; // "Battery", "Music", "Performance", "Cat" (Nailong)
        private string _characterAnimation = "Nailong"; // "Nailong", "Spongebob", or "Minion"
        private int _animationSpeedMs = 240; // 120ms to 400ms
        private int _hoverDwellDelayMs = 180; // 0ms to 400ms dwell delay to prevent accidental swipe

        // 4. GAMING & ANTI-DISRUPTION
        private bool _autoGameMode = true; // Auto-detect fullscreen games
        private string _gameAction = "IgnoreHover"; // "IgnoreHover" or "AutoHide"
        private bool _manualGameMode = false; // Manual switch

        // 5. NOTIFICATIONS (DISCORD, STEAM, SYSTEM)
        private bool _enableNotifications = true;
        private bool _enableDiscordNotif = true;
        private bool _enableSteamNotif = true;

        // 6. SYSTEM & PONI GENERAL SETTINGS
        private string _appLanguage = "id"; // "id", "en-US", "en-GB", "zh", "ru", "de", "pl", "ja", "ko"
        private bool _startWithWindows = false;
        private double _positionPercent = 50.0;
        private bool _lockPosition = false;
        private bool _autoHide = false;
        private bool _showHideButton = true;
        private bool _touchscreenSupport = false;
        private bool _keepWindowsBelow = false;
        private string _selectedDisplay = "Primary display";

        // 8. MODULES SETTINGS
        private string _weatherCity = "Jakarta";
        private string _weatherUnit = "Celsius";
        private string _prayerCity = "Jakarta";
        private bool _prayerNotification = true;
        private int _pomodoroWorkMin = 25;
        private int _pomodoroBreakMin = 5;
        private int _waterReminderIntervalMin = 45;
        private bool _postureReminder = true;
        private bool _eyeBreakReminder = true;
        private bool _trackScreenTime = true;
        private int _dailyScreenLimitHours = 6;
        private int _workdayStartHour = 9;
        private int _workdayEndHour = 18;
                private bool _cursorMagnetSnap = true;
        private bool _dodgeBrowserTabs = true;
        private bool _showMediaHUD = true;
        private bool _showBatteryHUD = true;
        private bool _showBluetoothHUD = true;
        private bool _showVolumeHUD = true;

        // 7. ATOLL SUITE (Audio Feedback, Clipboard Manager, Minimalist Music)
        private bool _enableSoundEffects = true;
        private bool _enableClipboardManager = true;
        private bool _enableMinimalisticMusic = true;
        private bool _showPomodoroWidget = true;
        private bool _enableDynamicIsland = true;
        private bool _enableFloatingDock = true;

        public bool EnableDynamicIsland
        {
            get => _enableDynamicIsland;
            set { _enableDynamicIsland = value; OnPropertyChanged(); Save(); }
        }

        public bool EnableFloatingDock
        {
            get => _enableFloatingDock;
            set { _enableFloatingDock = value; OnPropertyChanged(); Save(); }
        }

        public bool ShowPomodoroWidget
        {
            get => _showPomodoroWidget;
            set { _showPomodoroWidget = value; OnPropertyChanged(); Save(); }
        }

        public bool EnableSoundEffects
        {
            get => _enableSoundEffects;
            set { _enableSoundEffects = value; OnPropertyChanged(); Save(); }
        }

        public bool EnableClipboardManager
        {
            get => _enableClipboardManager;
            set { _enableClipboardManager = value; OnPropertyChanged(); Save(); }
        }

        public bool EnableMinimalisticMusic
        {
            get => _enableMinimalisticMusic;
            set { _enableMinimalisticMusic = value; OnPropertyChanged(); Save(); }
        }

        public double CompactWidth
        {
            get => _compactWidth;
            set { _compactWidth = Math.Round(value); OnPropertyChanged(); Save(); }
        }

        public void SetCompactWidthLive(double value)
        {
            double rounded = Math.Round(value);
            if (Math.Abs(_compactWidth - rounded) >= 1.0)
            {
                _compactWidth = rounded;
                OnPropertyChanged(nameof(CompactWidth));
            }
        }

        public double CompactHeight
        {
            get => _compactHeight;
            set { _compactHeight = Math.Round(value); OnPropertyChanged(); Save(); }
        }

        public double ExpandedWidth
        {
            get => _expandedWidth;
            set { _expandedWidth = Math.Round(value); OnPropertyChanged(); Save(); }
        }

        public double ExpandedHeight
        {
            get => _expandedHeight;
            set { _expandedHeight = Math.Round(value); OnPropertyChanged(); Save(); }
        }

        public double GlobalScale
        {
            get => _globalScale;
            set { _globalScale = Math.Round(value); OnPropertyChanged(); Save(); }
        }

        public double PositionOffsetX
        {
            get => _positionOffsetX;
            set { _positionOffsetX = Math.Round(value); OnPropertyChanged(); Save(); }
        }

        public string DockPosition
        {
            get => _dockPosition;
            set { _dockPosition = value; OnPropertyChanged(); Save(); }
        }

        public double DockOffsetY
        {
            get => _dockOffsetY;
            set { _dockOffsetY = Math.Round(value); OnPropertyChanged(); Save(); }
        }

        public double NotchOpacity
        {
            get => _notchOpacity;
            set { _notchOpacity = Math.Round(value); OnPropertyChanged(); Save(); }
        }

        public bool AlwaysOnTop
        {
            get => _alwaysOnTop;
            set { _alwaysOnTop = value; OnPropertyChanged(); Save(); }
        }

        public bool AutoExpandOnHover
        {
            get => _autoExpandOnHover;
            set { _autoExpandOnHover = value; OnPropertyChanged(); Save(); }
        }

        public bool AutoSwitchMusic
        {
            get => _autoSwitchMusic;
            set { _autoSwitchMusic = value; OnPropertyChanged(); Save(); }
        }

        public string DefaultIdleMode
        {
            get => _defaultIdleMode;
            set { _defaultIdleMode = value; OnPropertyChanged(); Save(); }
        }

        public string CharacterAnimation
        {
            get => _characterAnimation;
            set { _characterAnimation = value; OnPropertyChanged(); Save(); }
        }

        public int AnimationSpeedMs
        {
            get => _animationSpeedMs;
            set { _animationSpeedMs = value; OnPropertyChanged(); Save(); }
        }

        public int HoverDwellDelayMs
        {
            get => _hoverDwellDelayMs;
            set { _hoverDwellDelayMs = value; OnPropertyChanged(); Save(); }
        }

        public bool AutoGameMode
        {
            get => _autoGameMode;
            set { _autoGameMode = value; OnPropertyChanged(); Save(); }
        }

        public string GameAction
        {
            get => _gameAction;
            set { _gameAction = value; OnPropertyChanged(); Save(); }
        }

        public bool ManualGameMode
        {
            get => _manualGameMode;
            set { _manualGameMode = value; OnPropertyChanged(); Save(); }
        }

        public bool EnableNotifications
        {
            get => _enableNotifications;
            set { _enableNotifications = value; OnPropertyChanged(); Save(); }
        }

        public bool EnableDiscordNotif
        {
            get => _enableDiscordNotif;
            set { _enableDiscordNotif = value; OnPropertyChanged(); Save(); }
        }

        public bool EnableSteamNotif
        {
            get => _enableSteamNotif;
            set { _enableSteamNotif = value; OnPropertyChanged(); Save(); }
        }

        public bool StartWithWindows
        {
            get => _startWithWindows;
            set
            {
                _startWithWindows = value;
                OnPropertyChanged();
                SetStartupRegistry(value);
                Save();
            }
        }

        public double PositionPercent
        {
            get => _positionPercent;
            set { _positionPercent = Math.Clamp(value, 0, 100); OnPropertyChanged(); Save(); }
        }

        public bool LockPosition
        {
            get => _lockPosition;
            set { _lockPosition = value; OnPropertyChanged(); Save(); }
        }

        public bool AutoHide
        {
            get => _autoHide;
            set { _autoHide = value; OnPropertyChanged(); Save(); }
        }

        public bool ShowHideButton
        {
            get => _showHideButton;
            set { _showHideButton = value; OnPropertyChanged(); Save(); }
        }

        public bool TouchscreenSupport
        {
            get => _touchscreenSupport;
            set { _touchscreenSupport = value; OnPropertyChanged(); Save(); }
        }

        public bool KeepWindowsBelow
        {
            get => _keepWindowsBelow;
            set { _keepWindowsBelow = value; OnPropertyChanged(); Save(); }
        }

        public string SelectedDisplay
        {
            get => _selectedDisplay;
            set { _selectedDisplay = value; OnPropertyChanged(); Save(); }
        }

        public string AppLanguage
        {
            get => _appLanguage;
            set
            {
                if (_appLanguage != value)
                {
                    _appLanguage = value;
                    OnPropertyChanged();
                    Save();
                    LocalizationManager.Instance.SetLanguage(value);
                }
            }
        }

        public string WeatherCity
        {
            get => _weatherCity;
            set { _weatherCity = value; OnPropertyChanged(); Save(); }
        }

        public string WeatherUnit
        {
            get => _weatherUnit;
            set { _weatherUnit = value; OnPropertyChanged(); Save(); }
        }

        public string PrayerCity
        {
            get => _prayerCity;
            set { _prayerCity = value; OnPropertyChanged(); Save(); }
        }

        public bool PrayerNotification
        {
            get => _prayerNotification;
            set { _prayerNotification = value; OnPropertyChanged(); Save(); }
        }

        public int PomodoroWorkMin
        {
            get => _pomodoroWorkMin;
            set { _pomodoroWorkMin = Math.Clamp(value, 5, 90); OnPropertyChanged(); Save(); }
        }

        public int PomodoroBreakMin
        {
            get => _pomodoroBreakMin;
            set { _pomodoroBreakMin = Math.Clamp(value, 1, 30); OnPropertyChanged(); Save(); }
        }

        public int WaterReminderIntervalMin
        {
            get => _waterReminderIntervalMin;
            set { _waterReminderIntervalMin = Math.Clamp(value, 15, 120); OnPropertyChanged(); Save(); }
        }

        public bool PostureReminder
        {
            get => _postureReminder;
            set { _postureReminder = value; OnPropertyChanged(); Save(); }
        }

        public bool EyeBreakReminder
        {
            get => _eyeBreakReminder;
            set { _eyeBreakReminder = value; OnPropertyChanged(); Save(); }
        }

        public bool TrackScreenTime
        {
            get => _trackScreenTime;
            set { _trackScreenTime = value; OnPropertyChanged(); Save(); }
        }

        public int DailyScreenLimitHours
        {
            get => _dailyScreenLimitHours;
            set { _dailyScreenLimitHours = Math.Clamp(value, 1, 16); OnPropertyChanged(); Save(); }
        }

        public int WorkdayStartHour
        {
            get => _workdayStartHour;
            set { _workdayStartHour = Math.Clamp(value, 0, 23); OnPropertyChanged(); Save(); }
        }

        public int WorkdayEndHour
        {
            get => _workdayEndHour;
            set { _workdayEndHour = Math.Clamp(value, 0, 23); OnPropertyChanged(); Save(); }
        }

                public bool CursorMagnetSnap
        {
            get => _cursorMagnetSnap;
            set { _cursorMagnetSnap = value; OnPropertyChanged(); Save(); }
        }

        public bool DodgeBrowserTabs
        {
            get => _dodgeBrowserTabs;
            set { _dodgeBrowserTabs = value; OnPropertyChanged(); Save(); }
        }

        public bool ShowMediaHUD
        {
            get => _showMediaHUD;
            set { _showMediaHUD = value; OnPropertyChanged(); Save(); }
        }

        public bool ShowBatteryHUD
        {
            get => _showBatteryHUD;
            set { _showBatteryHUD = value; OnPropertyChanged(); Save(); }
        }

        public bool ShowBluetoothHUD
        {
            get => _showBluetoothHUD;
            set { _showBluetoothHUD = value; OnPropertyChanged(); Save(); }
        }

        public bool ShowVolumeHUD
        {
            get => _showVolumeHUD;
            set { _showVolumeHUD = value; OnPropertyChanged(); Save(); }
        }

        public event PropertyChangedEventHandler? PropertyChanged;
        private void OnPropertyChanged([CallerMemberName] string? name = null) =>
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(name));

        public SettingsManager()
        {
            _instance = this;
            Load();
            CheckStartupRegistry();
        }

        public void ApplyPreset(string preset)
        {
            switch (preset.ToLowerInvariant())
            {
                case "compact":
                case "kecil":
                    CompactWidth = 200;
                    CompactHeight = 32;
                    ExpandedWidth = 550;
                    ExpandedHeight = 84;
                    GlobalScale = 90;
                    break;
                case "large":
                case "besar":
                    CompactWidth = 280;
                    CompactHeight = 42;
                    ExpandedWidth = 720;
                    ExpandedHeight = 110;
                    GlobalScale = 115;
                    break;
                case "default":
                case "standar":
                default:
                    CompactWidth = 240;
                    CompactHeight = 36;
                    ExpandedWidth = 680;
                    ExpandedHeight = 144;
                    GlobalScale = 100;
                    PositionOffsetX = 0;
                    NotchOpacity = 100;
                    break;
            }
        }

        public void ResetToDefault()
        {
            ApplyPreset("standar");
            DockPosition = "Top";
            DockOffsetY = 0;
            AutoExpandOnHover = true;
            AutoSwitchMusic = true;
            DefaultIdleMode = "Battery";
            AnimationSpeedMs = 240;
            HoverDwellDelayMs = 180;
            AutoGameMode = true;
            GameAction = "IgnoreHover";
            ManualGameMode = false;
            AlwaysOnTop = true;
        }

        private void SetStartupRegistry(bool enable)
        {
            try
            {
                using var key = Registry.CurrentUser.OpenSubKey(@"SOFTWARE\Microsoft\Windows\CurrentVersion\Run", true);
                if (key == null) return;
                string exePath = Environment.ProcessPath ?? "";
                if (enable && !string.IsNullOrEmpty(exePath))
                {
                    key.SetValue("DynamicIslandPro", $"\"{exePath}\"");
                }
                else
                {
                    key.DeleteValue("DynamicIslandPro", false);
                }
            }
            catch { }
        }

        private void CheckStartupRegistry()
        {
            try
            {
                using var key = Registry.CurrentUser.OpenSubKey(@"SOFTWARE\Microsoft\Windows\CurrentVersion\Run", false);
                if (key != null)
                {
                    _startWithWindows = key.GetValue("DynamicIslandPro") != null;
                }
            }
            catch { }
        }

        public void Save()
        {
            try
            {
                if (!Directory.Exists(SettingsDir))
                {
                    Directory.CreateDirectory(SettingsDir);
                }

                var options = new JsonSerializerOptions { WriteIndented = true };
                var json = JsonSerializer.Serialize(this, options);
                File.WriteAllText(SettingsFile, json);
            }
            catch { }
        }

        public void Load()
        {
            try
            {
                if (File.Exists(SettingsFile))
                {
                    var json = File.ReadAllText(SettingsFile);
                    var data = JsonSerializer.Deserialize<SettingsData>(json);
                    if (data != null)
                    {
                        if (data.CompactWidth > 0) _compactWidth = data.CompactWidth;
                        if (data.CompactHeight > 0) _compactHeight = data.CompactHeight;
                        if (data.ExpandedWidth > 0) _expandedWidth = data.ExpandedWidth;
                        if (data.ExpandedHeight > 0) _expandedHeight = data.ExpandedHeight;
                        if (data.GlobalScale > 0) _globalScale = data.GlobalScale;
                        _positionOffsetX = data.PositionOffsetX;
                        if (!string.IsNullOrEmpty(data.DockPosition)) _dockPosition = data.DockPosition;
                        _dockOffsetY = data.DockOffsetY;
                        if (data.NotchOpacity > 0) _notchOpacity = data.NotchOpacity;
                        _alwaysOnTop = data.AlwaysOnTop;
                        _autoExpandOnHover = data.AutoExpandOnHover;
                        _autoSwitchMusic = data.AutoSwitchMusic;
                        if (!string.IsNullOrEmpty(data.DefaultIdleMode)) _defaultIdleMode = data.DefaultIdleMode;
                        if (!string.IsNullOrEmpty(data.CharacterAnimation)) _characterAnimation = data.CharacterAnimation;
                        if (data.AnimationSpeedMs > 0) _animationSpeedMs = data.AnimationSpeedMs;
                        if (data.HoverDwellDelayMs >= 0) _hoverDwellDelayMs = data.HoverDwellDelayMs;
                        _autoGameMode = data.AutoGameMode;
                        if (!string.IsNullOrEmpty(data.GameAction)) _gameAction = data.GameAction;
                        _manualGameMode = data.ManualGameMode;
                        _enableNotifications = data.EnableNotifications;
                        _enableDiscordNotif = data.EnableDiscordNotif;
                        _enableSteamNotif = data.EnableSteamNotif;
                        _enableSoundEffects = data.EnableSoundEffects;
                        _enableClipboardManager = data.EnableClipboardManager;
                        _enableMinimalisticMusic = data.EnableMinimalisticMusic;
                        _showPomodoroWidget = data.ShowPomodoroWidget;
                        _enableDynamicIsland = data.EnableDynamicIsland;
                        _enableFloatingDock = data.EnableFloatingDock;
                        if (data.PositionPercent >= 0) _positionPercent = data.PositionPercent;
                        _lockPosition = data.LockPosition;
                        _autoHide = data.AutoHide;
                        _showHideButton = data.ShowHideButton;
                        _touchscreenSupport = data.TouchscreenSupport;
                        _keepWindowsBelow = data.KeepWindowsBelow;
                        if (!string.IsNullOrEmpty(data.SelectedDisplay)) _selectedDisplay = data.SelectedDisplay;
                        if (!string.IsNullOrEmpty(data.AppLanguage))
                        {
                            _appLanguage = data.AppLanguage;
                            LocalizationManager.Instance.SetLanguage(_appLanguage);
                        }
                        if (!string.IsNullOrEmpty(data.WeatherCity)) _weatherCity = data.WeatherCity;
                        if (!string.IsNullOrEmpty(data.WeatherUnit)) _weatherUnit = data.WeatherUnit;
                        if (!string.IsNullOrEmpty(data.PrayerCity)) _prayerCity = data.PrayerCity;
                        _prayerNotification = data.PrayerNotification;
                        if (data.PomodoroWorkMin > 0) _pomodoroWorkMin = data.PomodoroWorkMin;
                        if (data.PomodoroBreakMin > 0) _pomodoroBreakMin = data.PomodoroBreakMin;
                        if (data.WaterReminderIntervalMin > 0) _waterReminderIntervalMin = data.WaterReminderIntervalMin;
                        _postureReminder = data.PostureReminder;
                        _eyeBreakReminder = data.EyeBreakReminder;
                        _trackScreenTime = data.TrackScreenTime;
                        if (data.DailyScreenLimitHours > 0) _dailyScreenLimitHours = data.DailyScreenLimitHours;
                        if (data.WorkdayStartHour >= 0) _workdayStartHour = data.WorkdayStartHour;
                        if (data.WorkdayEndHour > 0) _workdayEndHour = data.WorkdayEndHour;
                                                _cursorMagnetSnap = data.CursorMagnetSnap;
                        _dodgeBrowserTabs = data.DodgeBrowserTabs;
                        _showMediaHUD = data.ShowMediaHUD;
                        _showBatteryHUD = data.ShowBatteryHUD;
                        _showBluetoothHUD = data.ShowBluetoothHUD;
                        _showVolumeHUD = data.ShowVolumeHUD;
                    }
                }
            }
            catch { }
        }

        public class SettingsData
        {
            public double CompactWidth { get; set; } = 240;
            public double CompactHeight { get; set; } = 36;
            public double ExpandedWidth { get; set; } = 680;
            public double ExpandedHeight { get; set; } = 144;
            public double GlobalScale { get; set; } = 100;
            public double PositionOffsetX { get; set; } = 0;
            public string DockPosition { get; set; } = "Top";
            public double DockOffsetY { get; set; } = 0;
            public double NotchOpacity { get; set; } = 100;
            public bool AlwaysOnTop { get; set; } = true;
            public bool AutoExpandOnHover { get; set; } = true;
            public bool AutoSwitchMusic { get; set; } = true;
            public string DefaultIdleMode { get; set; } = "Cat";
            public string CharacterAnimation { get; set; } = "Nailong";
            public int AnimationSpeedMs { get; set; } = 240;
            public int HoverDwellDelayMs { get; set; } = 180;
            public bool AutoGameMode { get; set; } = true;
            public string GameAction { get; set; } = "IgnoreHover";
            public bool ManualGameMode { get; set; } = false;
            public bool EnableNotifications { get; set; } = true;
            public bool EnableDiscordNotif { get; set; } = true;
            public bool EnableSteamNotif { get; set; } = true;
            public bool EnableSoundEffects { get; set; } = true;
            public bool EnableClipboardManager { get; set; } = true;
            public bool EnableMinimalisticMusic { get; set; } = true;
            public bool ShowPomodoroWidget { get; set; } = true;
            public bool EnableDynamicIsland { get; set; } = true;
            public bool EnableFloatingDock { get; set; } = true;
            public double PositionPercent { get; set; } = 50.0;
            public bool LockPosition { get; set; } = false;
            public bool AutoHide { get; set; } = false;
            public bool ShowHideButton { get; set; } = true;
            public bool TouchscreenSupport { get; set; } = false;
            public bool KeepWindowsBelow { get; set; } = false;
            public string SelectedDisplay { get; set; } = "Primary display";
            public string AppLanguage { get; set; } = "id";
            public string WeatherCity { get; set; } = "Jakarta";
            public string WeatherUnit { get; set; } = "Celsius";
            public string PrayerCity { get; set; } = "Jakarta";
            public bool PrayerNotification { get; set; } = true;
            public int PomodoroWorkMin { get; set; } = 25;
            public int PomodoroBreakMin { get; set; } = 5;
            public int WaterReminderIntervalMin { get; set; } = 45;
            public bool PostureReminder { get; set; } = true;
            public bool EyeBreakReminder { get; set; } = true;
            public bool TrackScreenTime { get; set; } = true;
            public int DailyScreenLimitHours { get; set; } = 6;
            public int WorkdayStartHour { get; set; } = 9;
            public int WorkdayEndHour { get; set; } = 18;
            public bool CursorMagnetSnap { get; set; } = true;
            public bool DodgeBrowserTabs { get; set; } = true;
            public bool ShowMediaHUD { get; set; } = true;
            public bool ShowBatteryHUD { get; set; } = true;
            public bool ShowBluetoothHUD { get; set; } = true;
            public bool ShowVolumeHUD { get; set; } = true;
        }
    }
}
