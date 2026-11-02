// Language: C#
// Filename: SettingsWindow.xaml.cs
// Runs on: Windows 10 / Windows 11 (.NET 9.0-windows)
// Targets: Dynamic Island Pro Settings Window

using System;
using System.Runtime.InteropServices;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Interop;
using System.Windows.Media;
using System.IO;
using System.Windows.Input;
using System.Windows.Media.Imaging;

namespace DynamicIsland
{
    public partial class SettingsWindow : Window
    {
        [DllImport("dwmapi.dll")]
        private static extern int DwmSetWindowAttribute(IntPtr hwnd, int attr, ref int attrValue, int attrSize);
        private const int DWMWA_USE_IMMERSIVE_DARK_MODE = 20;

        private readonly MainWindow _mainWindow;
        private readonly SettingsManager _settings;
        private bool _isInitializing = false;
        private Button? _activeNavButton;

        public SettingsWindow(MainWindow mainWindow, SettingsManager settings)
        {
            _mainWindow = mainWindow;
            _settings = settings;
            InitializeComponent();

            SourceInitialized += SettingsWindow_SourceInitialized;
                        Loaded += SettingsWindow_Loaded;
            Closed += (s, e) => MainWindow.TrimAppMemory();
        }

        private void SettingsWindow_SourceInitialized(object? sender, EventArgs e)
        {
            try
            {
                var handle = new WindowInteropHelper(this).Handle;
                int darkMode = 1;
                DwmSetWindowAttribute(handle, DWMWA_USE_IMMERSIVE_DARK_MODE, ref darkMode, sizeof(int));
            }
            catch { }
        }

        private void SettingsWindow_Loaded(object sender, RoutedEventArgs e)
        {
            try
            {
                _isInitializing = true;
                // ==========================================
                // 1. GENERAL
                // ==========================================
                if (ToggleLaunchAtLogin != null) ToggleLaunchAtLogin.IsChecked = _settings.StartWithWindows;
                if (ToggleLockPosition != null) ToggleLockPosition.IsChecked = _settings.LockPosition;
                if (ToggleHideFullscreen != null) ToggleHideFullscreen.IsChecked = _settings.AutoGameMode;
                if (ToggleAutoHide != null) ToggleAutoHide.IsChecked = _settings.AutoHide;
                if (ToggleHideButton != null) ToggleHideButton.IsChecked = _settings.ShowHideButton;
                if (ToggleTouchscreen != null) ToggleTouchscreen.IsChecked = _settings.TouchscreenSupport;
                if (ToggleKeepWindowsBelow != null) ToggleKeepWindowsBelow.IsChecked = _settings.KeepWindowsBelow;

                if (CmbLanguage != null)
                {
                    string curLang = _settings.AppLanguage ?? "id";
                    foreach (ComboBoxItem item in CmbLanguage.Items)
                    {
                        if (item.Tag?.ToString() == curLang)
                        {
                            CmbLanguage.SelectedItem = item;
                            break;
                        }
                    }
                    if (CmbLanguage.SelectedItem == null && CmbLanguage.Items.Count > 0)
                    {
                        CmbLanguage.SelectedIndex = 0;
                    }
                }
                ApplyLocalization();

                if (SliderPosition != null)
                {
                    SliderPosition.Value = _settings.PositionPercent > 0 ? _settings.PositionPercent : 50;
                }
                if (TxtPositionPercent != null)
                {
                    TxtPositionPercent.Text = $"{(int)(SliderPosition?.Value ?? 50)}%";
                }

                // ==========================================
                // 2. MODULES
                // ==========================================
                if (ToggleEnableDynamicIsland != null) ToggleEnableDynamicIsland.IsChecked = _settings.EnableDynamicIsland;
                if (ToggleSoundEffects != null) ToggleSoundEffects.IsChecked = _settings.EnableSoundEffects;
                                if (ToggleClipboard != null) ToggleClipboard.IsChecked = _settings.EnableClipboardManager;
                                if (TogglePomodoroModule != null) TogglePomodoroModule.IsChecked = _settings.ShowPomodoroWidget;
                if (ToggleFloatingDock != null) ToggleFloatingDock.IsChecked = _settings.EnableFloatingDock;

                // ==========================================
                // 3. LIVE ACTIVITIES
                // ==========================================
                if (ToggleMediaHUD != null) ToggleMediaHUD.IsChecked = _settings.ShowMediaHUD;
                if (ToggleBatteryHUD != null) ToggleBatteryHUD.IsChecked = _settings.ShowBatteryHUD;
                if (ToggleBluetoothHUD != null) ToggleBluetoothHUD.IsChecked = _settings.ShowBluetoothHUD;
                if (ToggleVolumeHUD != null) ToggleVolumeHUD.IsChecked = _settings.ShowVolumeHUD;

                // ==========================================
                // 4. CAT BUDDY & CHARACTERS
                // ==========================================
                HighlightActiveCharacter(_settings.CharacterAnimation);

                // ==========================================
                // 5. CURSOR
                // ==========================================
                                if (ToggleDodgeBrowserTabs != null) ToggleDodgeBrowserTabs.IsChecked = _settings.DodgeBrowserTabs;
                if (ToggleCursorMagnet != null) ToggleCursorMagnet.IsChecked = _settings.CursorMagnetSnap;
                if (SliderHoverDelay != null)
                {
                    SliderHoverDelay.Value = _settings.HoverDwellDelayMs > 0 ? _settings.HoverDwellDelayMs : 180;
                    if (TxtHoverDelay != null) TxtHoverDelay.Text = $"{(int)SliderHoverDelay.Value}ms";
                }

                // ==========================================
                // 6. SIZING & NOTCH HARDWARE (UKURAN ISLAND)
                // ==========================================
                if (_mainWindow != null && _mainWindow.Settings != null)
                {
                    if (SliderCompactW != null)
                    {
                        SliderCompactW.Value = _mainWindow.Settings.CompactWidth;
                        if (TxtCompactW != null) TxtCompactW.Text = $"{(int)SliderCompactW.Value} px";
                    }
                    if (SliderCompactH != null)
                    {
                        SliderCompactH.Value = _mainWindow.Settings.CompactHeight;
                        if (TxtCompactH != null) TxtCompactH.Text = $"{(int)SliderCompactH.Value} px";
                    }
                    if (SliderExpandedW != null)
                    {
                        SliderExpandedW.Value = _mainWindow.Settings.ExpandedWidth;
                        if (TxtExpandedW != null) TxtExpandedW.Text = $"{(int)SliderExpandedW.Value} px";
                    }
                    if (SliderExpandedH != null)
                    {
                        SliderExpandedH.Value = _mainWindow.Settings.ExpandedHeight;
                        if (TxtExpandedH != null) TxtExpandedH.Text = $"{(int)SliderExpandedH.Value} px";
                    }
                }

                // ==========================================
                // 7. MEDIA
                // ==========================================
                if (ToggleAutoMusic != null) ToggleAutoMusic.IsChecked = _settings.AutoSwitchMusic;

                // ==========================================
                // 9. WEATHER
                // ==========================================
                if (CmbWeatherCity != null && !string.IsNullOrEmpty(_settings.WeatherCity))
                {
                    foreach (ComboBoxItem item in CmbWeatherCity.Items)
                    {
                        if (item.Content?.ToString() == _settings.WeatherCity)
                        {
                            item.IsSelected = true;
                            break;
                        }
                    }
                }
                if (CmbWeatherUnit != null && !string.IsNullOrEmpty(_settings.WeatherUnit))
                {
                    foreach (ComboBoxItem item in CmbWeatherUnit.Items)
                    {
                        if (item.Content?.ToString()?.Contains(_settings.WeatherUnit) == true)
                        {
                            item.IsSelected = true;
                            break;
                        }
                    }
                }

                // ==========================================
                // 10. PRAYER TIMES
                // ==========================================
                if (CmbPrayerCity != null && !string.IsNullOrEmpty(_settings.PrayerCity))
                {
                    foreach (ComboBoxItem item in CmbPrayerCity.Items)
                    {
                        if (item.Content?.ToString()?.Contains(_settings.PrayerCity) == true)
                        {
                            item.IsSelected = true;
                            break;
                        }
                    }
                }
                if (TogglePrayerNotif != null) TogglePrayerNotif.IsChecked = _settings.PrayerNotification;

                // ==========================================
                // 11. POMODORO
                // ==========================================
                if (SliderPomodoroWork != null)
                {
                    SliderPomodoroWork.Value = _settings.PomodoroWorkMin > 0 ? _settings.PomodoroWorkMin : 25;
                    if (TxtPomodoroWork != null) TxtPomodoroWork.Text = $"{(int)SliderPomodoroWork.Value} Menit";
                }
                if (SliderPomodoroBreak != null)
                {
                    SliderPomodoroBreak.Value = _settings.PomodoroBreakMin > 0 ? _settings.PomodoroBreakMin : 5;
                    if (TxtPomodoroBreak != null) TxtPomodoroBreak.Text = $"{(int)SliderPomodoroBreak.Value} Menit";
                }

                // ==========================================
                // 12. HEALTH
                // ==========================================
                if (SliderWaterReminder != null)
                {
                    SliderWaterReminder.Value = _settings.WaterReminderIntervalMin > 0 ? _settings.WaterReminderIntervalMin : 45;
                    if (TxtWaterReminder != null) TxtWaterReminder.Text = $"{(int)SliderWaterReminder.Value} Menit";
                }
                if (TogglePostureReminder != null) TogglePostureReminder.IsChecked = _settings.PostureReminder;
                if (ToggleEyeBreak != null) ToggleEyeBreak.IsChecked = _settings.EyeBreakReminder;

                // ==========================================
                // 13. SCREEN TIME
                // ==========================================
                if (ToggleTrackScreenTime != null) ToggleTrackScreenTime.IsChecked = _settings.TrackScreenTime;
                if (SliderScreenLimit != null)
                {
                    SliderScreenLimit.Value = _settings.DailyScreenLimitHours > 0 ? _settings.DailyScreenLimitHours : 6;
                    if (TxtScreenLimit != null) TxtScreenLimit.Text = $"{(int)SliderScreenLimit.Value} Jam";
                }
                _activeNavButton = NavGeneral;
            }
            catch (Exception ex)
            {
                Console.WriteLine($"[ERROR] SettingsWindow_Loaded: {ex}");
                try
                {
                    var logPath = System.IO.Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData), "DynamicIslandPro", "app.log");
                    System.IO.File.AppendAllText(logPath, $"[{DateTime.Now}] SettingsWindow_Loaded Exception: {ex}\n");
                }
                catch { }
            }
            finally
            {
                _isInitializing = false;
            }
        }

        // ==========================================
        // SIDEBAR NAVIGATION
        // ==========================================
        private void NavTab_Click(object sender, RoutedEventArgs e)
        {
            if (sender is not Button clickedBtn) return;
            _activeNavButton = clickedBtn;

            // Reset all nav button active tags
            if (NavWelcome != null) NavWelcome.Tag = null;
            if (NavGeneral != null) NavGeneral.Tag = null;
            if (NavModules != null) NavModules.Tag = null;
            if (NavLiveActivities != null) NavLiveActivities.Tag = null;
            if (NavCatBuddy != null) NavCatBuddy.Tag = null;
            if (NavCursor != null) NavCursor.Tag = null;
            if (NavDashboard != null) NavDashboard.Tag = null;
            if (NavMedia != null) NavMedia.Tag = null;
            if (NavCalendar != null) NavCalendar.Tag = null;
            if (NavWeather != null) NavWeather.Tag = null;
            if (NavPrayerTimes != null) NavPrayerTimes.Tag = null;
            if (NavPomodoro != null) NavPomodoro.Tag = null;
            if (NavHealth != null) NavHealth.Tag = null;
            if (NavScreenTime != null) NavScreenTime.Tag = null;
            if (NavDayProgress != null) NavDayProgress.Tag = null;
            if (NavBluetoothWifi != null) NavBluetoothWifi.Tag = null;
            if (NavSupportCreator != null) NavSupportCreator.Tag = null;
            if (NavDockApps != null) NavDockApps.Tag = null;

            clickedBtn.Tag = "Active";

            // Hide all pages
            if (PageGeneral != null) PageGeneral.Visibility = Visibility.Collapsed;
            if (PageModules != null) PageModules.Visibility = Visibility.Collapsed;
            if (PageLiveActivities != null) PageLiveActivities.Visibility = Visibility.Collapsed;
            if (PageCatBuddy != null) PageCatBuddy.Visibility = Visibility.Collapsed;
            if (PageCursor != null) PageCursor.Visibility = Visibility.Collapsed;
            if (PageDashboard != null) PageDashboard.Visibility = Visibility.Collapsed;
            if (PageMedia != null) PageMedia.Visibility = Visibility.Collapsed;
            if (PageCalendar != null) PageCalendar.Visibility = Visibility.Collapsed;
            if (PageWeather != null) PageWeather.Visibility = Visibility.Collapsed;
            if (PagePrayerTimes != null) PagePrayerTimes.Visibility = Visibility.Collapsed;
            if (PagePomodoro != null) PagePomodoro.Visibility = Visibility.Collapsed;
            if (PageHealth != null) PageHealth.Visibility = Visibility.Collapsed;
            if (PageScreenTime != null) PageScreenTime.Visibility = Visibility.Collapsed;
            if (PageDayProgress != null) PageDayProgress.Visibility = Visibility.Collapsed;
            if (PageBluetoothWifi != null) PageBluetoothWifi.Visibility = Visibility.Collapsed;
            if (PageWelcome != null) PageWelcome.Visibility = Visibility.Collapsed;
            if (PageSupportCreator != null) PageSupportCreator.Visibility = Visibility.Collapsed;
            if (PageDockApps != null) PageDockApps.Visibility = Visibility.Collapsed;

            // Activate target page
            if (clickedBtn == NavGeneral)
            {
                if (PageGeneral != null) PageGeneral.Visibility = Visibility.Visible;
            }
            else if (clickedBtn == NavModules)
            {
                if (PageModules != null) PageModules.Visibility = Visibility.Visible;
            }
            else if (clickedBtn == NavLiveActivities)
            {
                if (PageLiveActivities != null) PageLiveActivities.Visibility = Visibility.Visible;
            }
            else if (clickedBtn == NavCatBuddy)
            {
                if (PageCatBuddy != null) PageCatBuddy.Visibility = Visibility.Visible;
            }
            else if (clickedBtn == NavCursor)
            {
                if (PageCursor != null) PageCursor.Visibility = Visibility.Visible;
            }
            else if (clickedBtn == NavDashboard)
            {
                if (PageDashboard != null) PageDashboard.Visibility = Visibility.Visible;
            }
            else if (clickedBtn == NavMedia)
            {
                if (PageMedia != null) PageMedia.Visibility = Visibility.Visible;
            }
            else if (clickedBtn == NavCalendar)
            {
                if (PageCalendar != null) PageCalendar.Visibility = Visibility.Visible;
            }
            else if (clickedBtn == NavWeather)
            {
                if (PageWeather != null) PageWeather.Visibility = Visibility.Visible;
            }
            else if (clickedBtn == NavPrayerTimes)
            {
                if (PagePrayerTimes != null) PagePrayerTimes.Visibility = Visibility.Visible;
            }
            else if (clickedBtn == NavPomodoro)
            {
                if (PagePomodoro != null) PagePomodoro.Visibility = Visibility.Visible;
            }
            else if (clickedBtn == NavHealth)
            {
                if (PageHealth != null) PageHealth.Visibility = Visibility.Visible;
            }
            else if (clickedBtn == NavScreenTime)
            {
                if (PageScreenTime != null) PageScreenTime.Visibility = Visibility.Visible;
            }
            else if (clickedBtn == NavDayProgress)
            {
                if (PageDayProgress != null) PageDayProgress.Visibility = Visibility.Visible;
            }
            else if (clickedBtn == NavBluetoothWifi)
            {
                if (PageBluetoothWifi != null) PageBluetoothWifi.Visibility = Visibility.Visible;
            }
            else if (clickedBtn == NavWelcome)
            {
                if (PageWelcome != null) PageWelcome.Visibility = Visibility.Visible;
            }
            else if (clickedBtn == NavSupportCreator)
            {
                if (PageSupportCreator != null) PageSupportCreator.Visibility = Visibility.Visible;
            }
            else if (clickedBtn == NavDockApps)
            {
                if (PageDockApps != null) PageDockApps.Visibility = Visibility.Visible;
                LoadDockAppsList();

            }
            UpdateActiveHeader();
        }

        private void UpdateActiveHeader()
        {
            var loc = LocalizationManager.Instance;
            var btn = _activeNavButton ?? NavGeneral;

            if (btn == NavGeneral) SetHeader("\u2699\uFE0F", loc.Get("HeaderGeneral", "General"));
            else if (btn == NavModules) SetHeader("\uD83E\uDDE9", loc.Get("HeaderModules", "Modules & Features"));
            else if (btn == NavLiveActivities) SetHeader("\uD83D\uDD14", loc.Get("HeaderLiveActivities", "Live Activities"));
            else if (btn == NavCatBuddy) SetHeader("\uD83D\uDC3E", loc.Get("HeaderCatBuddy", "Cat Buddy & Characters"));
            else if (btn == NavCursor) SetHeader("\uD83D\uDC46", loc.Get("HeaderCursor", "Cursor Interaction"));
            else if (btn == NavDashboard) SetHeader("\uD83D\uDCD0", loc.Get("HeaderDashboard", "Ukuran Dynamic"));
            else if (btn == NavMedia) SetHeader("\uD83C\uDFB5", loc.Get("HeaderMedia", "Media Player"));
            else if (btn == NavCalendar) SetHeader("\uD83D\uDCC5", loc.Get("HeaderCalendar", "Calendar"));
            else if (btn == NavWeather) SetHeader("\u26C5", loc.Get("HeaderWeather", "Weather"));
            else if (btn == NavPrayerTimes) SetHeader("\uD83D\uDD4C", loc.Get("HeaderPrayerTimes", "Prayer Times"));
            else if (btn == NavPomodoro) SetHeader("\uD83C\uDF45", loc.Get("HeaderPomodoro", "Pomodoro Timer"));
            else if (btn == NavHealth) SetHeader("\uD83D\uDCA7", loc.Get("HeaderHealth", "Health & Wellness"));
            else if (btn == NavScreenTime) SetHeader("\u23F1", loc.Get("HeaderScreenTime", "Screen Time"));
            else if (btn == NavDayProgress) SetHeader("\uD83D\uDCCA", loc.Get("HeaderDayProgress", "Day Progress"));
            else if (btn == NavBluetoothWifi) SetHeader("\uD83D\uDCF6", loc.Get("HeaderBluetoothWifi", "Bluetooth & Wi-Fi"));
            else if (btn == NavWelcome) SetHeader("\uD83D\uDC4B", loc.Get("HeaderWelcome", "Welcome"));
            else if (btn == NavSupportCreator) SetHeader("\uD83D\uDC96", loc.Get("HeaderSupportCreator", "Support Creator"));
            else if (btn == NavDockApps) SetHeader("\u2693", "Aplikasi Dock");
        }

        private void SetHeader(string icon, string title)
        {
            if (HeaderIconText != null) HeaderIconText.Text = icon;
            if (HeaderTitleText != null) HeaderTitleText.Text = title;
        }
        // 1. GENERAL CONTROLS
        // ==========================================
        private void ToggleLaunchAtLogin_Click(object sender, RoutedEventArgs e)
        {
            if (_settings != null) _settings.StartWithWindows = ToggleLaunchAtLogin.IsChecked == true;
        }

        private void CmbDisplay_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            if (_settings != null && CmbDisplay?.SelectedItem is ComboBoxItem item)
            {
                _settings.SelectedDisplay = item.Content?.ToString() ?? "Primary display";
            }
        }

        private void SliderPosition_ValueChanged(object sender, RoutedPropertyChangedEventArgs<double> e)
        {
            if (TxtPositionPercent != null)
            {
                TxtPositionPercent.Text = $"{(int)e.NewValue}%";
            }
            if (_settings != null)
            {
                _settings.PositionPercent = e.NewValue;
            }
            if (_mainWindow != null)
            {
                _mainWindow.UpdateHorizontalPosition(e.NewValue / 100.0);
            }
        }

        private void BtnAlignLeft_Click(object sender, RoutedEventArgs e)
        {
            if (SliderPosition != null) SliderPosition.Value = 0;
        }

        private void BtnAlignCenter_Click(object sender, RoutedEventArgs e)
        {
            if (SliderPosition != null) SliderPosition.Value = 50;
        }

        private void BtnAlignRight_Click(object sender, RoutedEventArgs e)
        {
            if (SliderPosition != null) SliderPosition.Value = 100;
        }

        private void ToggleLockPosition_Click(object sender, RoutedEventArgs e)
        {
            if (_settings != null) _settings.LockPosition = ToggleLockPosition.IsChecked == true;
        }

        private void ToggleHideFullscreen_Click(object sender, RoutedEventArgs e)
        {
            if (_settings != null) _settings.AutoGameMode = ToggleHideFullscreen.IsChecked == true;
        }

        private void ToggleAutoHide_Click(object sender, RoutedEventArgs e)
        {
            if (_settings != null)
            {
                _settings.AutoHide = ToggleAutoHide.IsChecked == true;
                _mainWindow?.SetAutoHide(_settings.AutoHide);
            }
        }

        private void ToggleHideButton_Click(object sender, RoutedEventArgs e)
        {
            if (_settings != null)
            {
                _settings.ShowHideButton = ToggleHideButton.IsChecked == true;
                _mainWindow?.SetHideButtonVisible(_settings.ShowHideButton);
            }
        }

        private void ToggleTouchscreen_Click(object sender, RoutedEventArgs e)
        {
            if (_settings != null) _settings.TouchscreenSupport = ToggleTouchscreen.IsChecked == true;
        }

        private void ToggleKeepWindowsBelow_Click(object sender, RoutedEventArgs e)
        {
            if (_settings != null) _settings.KeepWindowsBelow = ToggleKeepWindowsBelow.IsChecked == true;
        }

        // ==========================================
        // 2. MODULES CONTROLS
        // ==========================================
        private void ToggleSoundEffects_Click(object sender, RoutedEventArgs e)
        {
            if (_settings != null)
            {
                _settings.EnableSoundEffects = ToggleSoundEffects.IsChecked == true;
                SoundManager.Enabled = _settings.EnableSoundEffects;
            }
        }

        private void ToggleClipboard_Click(object sender, RoutedEventArgs e)
        {
            if (_settings != null) _settings.EnableClipboardManager = ToggleClipboard.IsChecked == true;
        }

        private void ToggleQuickTools_Click(object sender, RoutedEventArgs e)
        {
            // Toggles Quick Tools display in ribbon
        }

        private void ToggleTaskbarModule_Click(object sender, RoutedEventArgs e)
        {
            // Toggles Taskbar Preview in ribbon
        }

        private void TogglePomodoroModule_Click(object sender, RoutedEventArgs e)
        {
            if (_settings != null)
            {
                _settings.ShowPomodoroWidget = TogglePomodoroModule.IsChecked == true;
                _mainWindow?.UpdatePomodoroVisibility(_settings.ShowPomodoroWidget);
            }
        }

        private void ToggleEnableDynamicIsland_Click(object sender, RoutedEventArgs e)
        {
            if (_settings != null)
            {
                _settings.EnableDynamicIsland = ToggleEnableDynamicIsland.IsChecked == true;
                _mainWindow?.UpdateIslandVisibility(_settings.EnableDynamicIsland);
            }
        }

        private void ToggleFloatingDock_Click(object sender, RoutedEventArgs e)
        {
            if (_settings != null)
            {
                _settings.EnableFloatingDock = ToggleFloatingDock.IsChecked == true;
                DockManager.Instance.SetDockEnabled(_settings.EnableFloatingDock);
            }
        }

        private void BtnRestoreTaskbarDirect_Click(object sender, RoutedEventArgs e)
        {
            if (_settings != null)
            {
                _settings.EnableFloatingDock = false;
                if (ToggleFloatingDock != null) ToggleFloatingDock.IsChecked = false;
                DockManager.Instance.SetDockEnabled(false);
            }
        }

        // ==========================================
        // 3. LIVE ACTIVITIES CONTROLS
        // ==========================================
        private void ToggleMediaHUD_Click(object sender, RoutedEventArgs e)
        {
            if (_settings != null) _settings.ShowMediaHUD = ToggleMediaHUD.IsChecked == true;
        }

        private void ToggleBatteryHUD_Click(object sender, RoutedEventArgs e)
        {
            if (_settings != null) _settings.ShowBatteryHUD = ToggleBatteryHUD.IsChecked == true;
        }

        private void ToggleBluetoothHUD_Click(object sender, RoutedEventArgs e)
        {
            if (_settings != null) _settings.ShowBluetoothHUD = ToggleBluetoothHUD.IsChecked == true;
        }

        private void ToggleVolumeHUD_Click(object sender, RoutedEventArgs e)
        {
            if (_settings != null) _settings.ShowVolumeHUD = ToggleVolumeHUD.IsChecked == true;
        }

        // ==========================================
        // 4. CAT BUDDY & CHARACTERS
        // ==========================================
        private void BtnSelectChar_Click(object sender, RoutedEventArgs e)
        {
            if (sender is Button btn && btn.Tag is string charName)
            {
                if (_settings != null) _settings.CharacterAnimation = charName;
                _mainWindow?.SetCharacterAnimation(charName);
                HighlightActiveCharacter(charName);
            }
        }

        private void HighlightActiveCharacter(string? charName)
        {
            var activeBorder = new SolidColorBrush(Color.FromRgb(59, 130, 246));
            var clearBorder = Brushes.Transparent;

            if (BtnCharNailong != null) BtnCharNailong.BorderBrush = (charName == "Nailong" || string.IsNullOrEmpty(charName)) ? activeBorder : clearBorder;
            if (BtnCharSpongebob != null) BtnCharSpongebob.BorderBrush = (charName == "Spongebob") ? activeBorder : clearBorder;
            if (BtnCharMinion != null) BtnCharMinion.BorderBrush = (charName == "Minion") ? activeBorder : clearBorder;
        }

        private void BtnIdleMode_Click(object sender, RoutedEventArgs e)
        {
            if (sender is Button btn && btn.Tag is string modeStr)
            {
                _mainWindow?.SelectIdleModeByName(modeStr);
            }
        }

        // ==========================================
        // 5. CURSOR CONTROLS
        // ==========================================
                private void ToggleDodgeBrowserTabs_Click(object sender, RoutedEventArgs e)
        {
            if (_settings != null) _settings.DodgeBrowserTabs = ToggleDodgeBrowserTabs.IsChecked == true;
        }

        private void ToggleCursorMagnet_Click(object sender, RoutedEventArgs e)
        {
            if (_settings != null) _settings.CursorMagnetSnap = ToggleCursorMagnet.IsChecked == true;
        }

        private void SliderHoverDelay_ValueChanged(object sender, RoutedPropertyChangedEventArgs<double> e)
        {
            if (TxtHoverDelay != null)
            {
                TxtHoverDelay.Text = $"{(int)e.NewValue}ms";
            }
            if (_settings != null)
            {
                _settings.HoverDwellDelayMs = (int)e.NewValue;
            }
        }

        // ==========================================
        // 6. DASHBOARD (NOTCH SIZING & PRESETS)
        // ==========================================
        private void PresetCompact_Click(object sender, RoutedEventArgs e)
        {
            if (_mainWindow != null && _mainWindow.Settings != null)
            {
                _mainWindow.Settings.ApplyPreset("kecil");
                _mainWindow.ApplyCurrentSizeSettings();
                SyncSizingSliders();
            }
        }

        private void PresetDefault_Click(object sender, RoutedEventArgs e)
        {
            if (_mainWindow != null && _mainWindow.Settings != null)
            {
                _mainWindow.Settings.ApplyPreset("standar");
                _mainWindow.ApplyCurrentSizeSettings();
                SyncSizingSliders();
            }
        }

        private void PresetLarge_Click(object sender, RoutedEventArgs e)
        {
            if (_mainWindow != null && _mainWindow.Settings != null)
            {
                _mainWindow.Settings.ApplyPreset("besar");
                _mainWindow.ApplyCurrentSizeSettings();
                SyncSizingSliders();
            }
        }

        private void SyncSizingSliders()
        {
            if (_mainWindow != null && _mainWindow.Settings != null)
            {
                if (SliderCompactW != null) SliderCompactW.Value = _mainWindow.Settings.CompactWidth;
                if (SliderCompactH != null) SliderCompactH.Value = _mainWindow.Settings.CompactHeight;
                if (SliderExpandedW != null) SliderExpandedW.Value = _mainWindow.Settings.ExpandedWidth;
                if (SliderExpandedH != null) SliderExpandedH.Value = _mainWindow.Settings.ExpandedHeight;
            }
        }

        private void SliderCompactW_ValueChanged(object sender, RoutedPropertyChangedEventArgs<double> e)
        {
            if (TxtCompactW != null) TxtCompactW.Text = $"{(int)e.NewValue} px";
            if (_mainWindow != null && _mainWindow.Settings != null)
            {
                _mainWindow.Settings.CompactWidth = e.NewValue;
                _mainWindow.ApplyCurrentSizeSettings();
            }
        }

        private void SliderCompactH_ValueChanged(object sender, RoutedPropertyChangedEventArgs<double> e)
        {
            if (TxtCompactH != null) TxtCompactH.Text = $"{(int)e.NewValue} px";
            if (_mainWindow != null && _mainWindow.Settings != null)
            {
                _mainWindow.Settings.CompactHeight = e.NewValue;
                _mainWindow.ApplyCurrentSizeSettings();
            }
        }

        private void SliderExpandedW_ValueChanged(object sender, RoutedPropertyChangedEventArgs<double> e)
        {
            if (TxtExpandedW != null) TxtExpandedW.Text = $"{(int)e.NewValue} px";
            if (_mainWindow != null && _mainWindow.Settings != null)
            {
                _mainWindow.Settings.ExpandedWidth = e.NewValue;
                _mainWindow.ApplyCurrentSizeSettings();
            }
        }

        private void SliderExpandedH_ValueChanged(object sender, RoutedPropertyChangedEventArgs<double> e)
        {
            if (TxtExpandedH != null) TxtExpandedH.Text = $"{(int)e.NewValue} px";
            if (_mainWindow != null && _mainWindow.Settings != null)
            {
                _mainWindow.Settings.ExpandedHeight = e.NewValue;
                _mainWindow.ApplyCurrentSizeSettings();
            }
        }

        private void BtnSetIdleBattery_Click(object sender, RoutedEventArgs e)
        {
            _mainWindow?.SelectIdleModeByName("Battery");
        }

        private void BtnSetIdleMusic_Click(object sender, RoutedEventArgs e)
        {
            _mainWindow?.SelectIdleModeByName("Music");
        }

        private void BtnSetIdleLyrics_Click(object sender, RoutedEventArgs e)
        {
            _mainWindow?.SelectIdleModeByName("Lyrics");
        }

        private void BtnSetIdlePerf_Click(object sender, RoutedEventArgs e)
        {
            _mainWindow?.SelectIdleModeByName("Performance");
        }

        private void BtnSetIdleCat_Click(object sender, RoutedEventArgs e)
        {
            _mainWindow?.SelectIdleModeByName("Cat");
        }

        private void BtnSetIdleClock_Click(object sender, RoutedEventArgs e)
        {
            _mainWindow?.SelectIdleModeByName("Clock");
        }

        // ==========================================
        // 7. MEDIA CONTROLS
        // ==========================================
        private void ToggleAutoMusic_Click(object sender, RoutedEventArgs e)
        {
            if (_settings != null) _settings.AutoSwitchMusic = ToggleAutoMusic.IsChecked == true;
        }

        private void ToggleWaveEqualizer_Click(object sender, RoutedEventArgs e)
        {
            // Toggles wave equalizer animation
        }

        // ==========================================
        // 9. WEATHER CONTROLS
        // ==========================================
        private void CmbWeatherCity_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            if (_settings != null && CmbWeatherCity?.SelectedItem is ComboBoxItem item)
            {
                _settings.WeatherCity = item.Content?.ToString() ?? "Jakarta";
            }
        }

        private void CmbWeatherUnit_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            if (_settings != null && CmbWeatherUnit?.SelectedItem is ComboBoxItem item)
            {
                var content = item.Content?.ToString() ?? "Celsius";
                _settings.WeatherUnit = content.Contains("Fahrenheit") ? "Fahrenheit" : "Celsius";
            }
        }

        // ==========================================
        // 10. PRAYER TIMES CONTROLS
        // ==========================================
        private void CmbPrayerCity_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            if (_settings != null && CmbPrayerCity?.SelectedItem is ComboBoxItem item)
            {
                _settings.PrayerCity = item.Content?.ToString() ?? "Jakarta (WIB)";
                PrayerManager.Instance.CalculateDailyPrayers();
            }
        }

        private void TogglePrayerNotif_Click(object sender, RoutedEventArgs e)
        {
            if (_settings != null)
            {
                _settings.PrayerNotification = TogglePrayerNotif.IsChecked == true;
                PrayerManager.Instance.CalculateDailyPrayers();
            }
        }

        // ==========================================
        // 11. POMODORO CONTROLS
        // ==========================================
        private void SliderPomodoroWork_ValueChanged(object sender, RoutedPropertyChangedEventArgs<double> e)
        {
            if (TxtPomodoroWork != null)
            {
                TxtPomodoroWork.Text = $"{(int)e.NewValue} Menit";
            }
            if (_settings != null)
            {
                _settings.PomodoroWorkMin = (int)e.NewValue;
            }
        }

        private void SliderPomodoroBreak_ValueChanged(object sender, RoutedPropertyChangedEventArgs<double> e)
        {
            if (TxtPomodoroBreak != null)
            {
                TxtPomodoroBreak.Text = $"{(int)e.NewValue} Menit";
            }
            if (_settings != null)
            {
                _settings.PomodoroBreakMin = (int)e.NewValue;
            }
        }

        // ==========================================
        // 12. HEALTH CONTROLS
        // ==========================================
        private void SliderWaterReminder_ValueChanged(object sender, RoutedPropertyChangedEventArgs<double> e)
        {
            if (TxtWaterReminder != null)
            {
                TxtWaterReminder.Text = $"{(int)e.NewValue} Menit";
            }
            if (_settings != null)
            {
                _settings.WaterReminderIntervalMin = (int)e.NewValue;
            }
        }

        private void TogglePostureReminder_Click(object sender, RoutedEventArgs e)
        {
            if (_settings != null) _settings.PostureReminder = TogglePostureReminder.IsChecked == true;
        }

        private void ToggleEyeBreak_Click(object sender, RoutedEventArgs e)
        {
            if (_settings != null) _settings.EyeBreakReminder = ToggleEyeBreak.IsChecked == true;
        }

        // ==========================================
        // 13. SCREEN TIME CONTROLS
        // ==========================================
        private void ToggleTrackScreenTime_Click(object sender, RoutedEventArgs e)
        {
            if (_settings != null) _settings.TrackScreenTime = ToggleTrackScreenTime.IsChecked == true;
        }

        private void SliderScreenLimit_ValueChanged(object sender, RoutedPropertyChangedEventArgs<double> e)
        {
            if (TxtScreenLimit != null)
            {
                TxtScreenLimit.Text = $"{(int)e.NewValue} Jam";
            }
            if (_settings != null)
            {
                _settings.DailyScreenLimitHours = (int)e.NewValue;
            }
        }

        // ==========================================
        // 14. DAY PROGRESS CONTROLS
        // ==========================================
        private void CmbWorkHours_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            if (_settings != null && CmbWorkStart?.SelectedItem is ComboBoxItem startItem)
            {
                var text = startItem.Content?.ToString() ?? "09:00";
                if (int.TryParse(text.Split(':')[0], out int startH)) _settings.WorkdayStartHour = startH;
            }
            if (_settings != null && CmbWorkEnd?.SelectedItem is ComboBoxItem endItem)
            {
                var text = endItem.Content?.ToString() ?? "18:00";
                if (int.TryParse(text.Split(':')[0], out int endH)) _settings.WorkdayEndHour = endH;
            }
        }

        // ==========================================
        // 17. SUPPORT CREATOR (SAWERIA QRIS)
        // ==========================================
        private void BtnQuickSupport_Click(object sender, RoutedEventArgs e)
        {
            if (NavSupportCreator != null)
            {
                NavTab_Click(NavSupportCreator, new RoutedEventArgs());
            }
        }

        private void BtnOpenSaweriaUrl_Click(object sender, RoutedEventArgs e)
        {
            try
            {
                System.Diagnostics.Process.Start(new System.Diagnostics.ProcessStartInfo("https://saweria.co/widgets/qr?streamKey=d8d9614364808abcaf1b64b22a515ad1")
                {
                    UseShellExecute = true
                });
            }
            catch { }
        }

        private void BtnCopySaweriaUrl_Click(object sender, RoutedEventArgs e)
        {
            try
            {
                Clipboard.SetText("https://saweria.co/widgets/qr?streamKey=d8d9614364808abcaf1b64b22a515ad1");
                MessageBox.Show(LocalizationManager.Instance.Get("SaweriaCopied", "Tautan Saweria berhasil disalin ke Clipboard!"), "Support Creator", MessageBoxButton.OK, MessageBoxImage.Information);
            }
            catch { }
        }

        private void BtnOpenPaypalUrl_Click(object sender, RoutedEventArgs e)
        {
            try
            {
                System.Diagnostics.Process.Start(new System.Diagnostics.ProcessStartInfo("https://www.paypal.com/qrcodes/p2pqrc/MS58PPP2S5H5E")
                {
                    UseShellExecute = true
                });
            }
            catch { }
        }

        private void BtnCopyPaypalUrl_Click(object sender, RoutedEventArgs e)
        {
            try
            {
                Clipboard.SetText("https://www.paypal.com/qrcodes/p2pqrc/MS58PPP2S5H5E");
                MessageBox.Show(LocalizationManager.Instance.Get("PaypalCopied", "Tautan PayPal berhasil disalin ke Clipboard!"), "Support Creator", MessageBoxButton.OK, MessageBoxImage.Information);
            }
            catch { }
        }

        private void CmbLanguage_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            if (_isInitializing) return;
            if (CmbLanguage?.SelectedItem is ComboBoxItem item && item.Tag != null)
            {
                string langCode = item.Tag.ToString() ?? "id";
                _settings.AppLanguage = langCode;
                LocalizationManager.Instance.SetLanguage(langCode);
                ApplyLocalization();
            }
        }

        private void ApplyLocalization()
        {
            var loc = LocalizationManager.Instance;

            // Header
            UpdateActiveHeader();

            // Sidebar
            if (TxtSectionDynamic != null) TxtSectionDynamic.Text = loc.Get("SectionDynamic", "DYNAMIC");
            if (TxtSectionModules != null) TxtSectionModules.Text = loc.Get("SectionModules", "MODUL & FITUR");

            if (TxtNavWelcome != null) TxtNavWelcome.Text = loc.Get("NavWelcome");
            if (TxtNavGeneral != null) TxtNavGeneral.Text = loc.Get("NavGeneral");
            if (TxtNavModules != null) TxtNavModules.Text = loc.Get("NavModules");
            if (TxtNavLiveActivities != null) TxtNavLiveActivities.Text = loc.Get("NavLiveActivities");
            if (TxtNavCatBuddy != null) TxtNavCatBuddy.Text = loc.Get("NavCatBuddy");
            if (TxtNavCursor != null) TxtNavCursor.Text = loc.Get("NavCursor");
            if (TxtNavDashboard != null) TxtNavDashboard.Text = loc.Get("NavDashboard");
            if (TxtNavMedia != null) TxtNavMedia.Text = loc.Get("NavMedia");
            if (TxtNavCalendar != null) TxtNavCalendar.Text = loc.Get("NavCalendar");
            if (TxtNavWeather != null) TxtNavWeather.Text = loc.Get("NavWeather");
            if (TxtNavPrayerTimes != null) TxtNavPrayerTimes.Text = loc.Get("NavPrayerTimes");
            if (TxtNavPomodoro != null) TxtNavPomodoro.Text = loc.Get("NavPomodoro");
            if (TxtNavHealth != null) TxtNavHealth.Text = loc.Get("NavHealth");
            if (TxtNavScreenTime != null) TxtNavScreenTime.Text = loc.Get("NavScreenTime");
            if (TxtNavDayProgress != null) TxtNavDayProgress.Text = loc.Get("NavDayProgress");
            if (TxtNavBluetoothWifi != null) TxtNavBluetoothWifi.Text = loc.Get("NavBluetoothWifi");
            if (TxtNavSupportCreator != null) TxtNavSupportCreator.Text = loc.Get("NavSupportCreator");
            if (BtnQuickSupport != null) BtnQuickSupport.Content = "\uD83D\uDC96 " + loc.Get("NavSupportCreator");
            if (BtnExitApp != null) BtnExitApp.Content = loc.Get("BtnExitApp");

            // Page 1: General
            if (TxtBrandingSub != null) TxtBrandingSub.Text = loc.Get("BrandingSub");
            if (TxtLaunchAtLoginTitle != null) TxtLaunchAtLoginTitle.Text = loc.Get("LaunchAtLoginTitle");
            if (TxtShowDynamicOn != null) TxtShowDynamicOn.Text = loc.Get("ShowDynamicOn");
            if (TxtGeneralLanguageTitle != null) TxtGeneralLanguageTitle.Text = loc.Get("LanguageTitle");
            if (TxtGeneralLanguageDesc != null) TxtGeneralLanguageDesc.Text = loc.Get("LanguageDesc");
            if (TxtPositionTitle != null) TxtPositionTitle.Text = loc.Get("PositionTitle");
            if (TxtPositionDesc != null) TxtPositionDesc.Text = loc.Get("PositionDesc");
            if (TxtLockPositionTitle != null) TxtLockPositionTitle.Text = loc.Get("LockPositionTitle");
            if (TxtLockPositionDesc != null) TxtLockPositionDesc.Text = loc.Get("LockPositionDesc");
            if (TxtHideFullscreenTitle != null) TxtHideFullscreenTitle.Text = loc.Get("HideFullscreenTitle");
            if (TxtHideFullscreenDesc != null) TxtHideFullscreenDesc.Text = loc.Get("HideFullscreenDesc");
            if (TxtAutoHideTitle != null) TxtAutoHideTitle.Text = loc.Get("AutoHideTitle");
            if (TxtAutoHideDesc != null) TxtAutoHideDesc.Text = loc.Get("AutoHideDesc");
            if (TxtHideButtonTitle != null) TxtHideButtonTitle.Text = loc.Get("HideButtonTitle");
            if (TxtHideButtonDesc != null) TxtHideButtonDesc.Text = loc.Get("HideButtonDesc");
            if (TxtTouchscreenTitle != null) TxtTouchscreenTitle.Text = loc.Get("TouchscreenTitle");
            if (TxtTouchscreenDesc != null) TxtTouchscreenDesc.Text = loc.Get("TouchscreenDesc");
            if (TxtKeepWindowsBelowTitle != null) TxtKeepWindowsBelowTitle.Text = loc.Get("KeepWindowsBelowTitle");
            if (TxtKeepWindowsBelowDesc != null) TxtKeepWindowsBelowDesc.Text = loc.Get("KeepWindowsBelowDesc");

            // Page 2: Modules
            if (TxtModulesHeaderTitle != null) TxtModulesHeaderTitle.Text = loc.Get("ModulesHeaderTitle");
            if (TxtSoundEffectsTitle != null) TxtSoundEffectsTitle.Text = loc.Get("SoundEffectsTitle");
            if (TxtSoundEffectsDesc != null) TxtSoundEffectsDesc.Text = loc.Get("SoundEffectsDesc");
            if (TxtClipboardTitle != null) TxtClipboardTitle.Text = loc.Get("ClipboardTitle");
            if (TxtClipboardDesc != null) TxtClipboardDesc.Text = loc.Get("ClipboardDesc");
            if (TxtQuickToolsTitle != null) TxtQuickToolsTitle.Text = loc.Get("QuickToolsTitle");
            if (TxtQuickToolsDesc != null) TxtQuickToolsDesc.Text = loc.Get("QuickToolsDesc");
            if (TxtTaskbarTitle != null) TxtTaskbarTitle.Text = loc.Get("TaskbarTitle");
            if (TxtTaskbarDesc != null) TxtTaskbarDesc.Text = loc.Get("TaskbarDesc");

            // Page 3: Live Activities
            if (TxtLiveActivitiesHeaderTitle != null) TxtLiveActivitiesHeaderTitle.Text = loc.Get("LiveActivitiesHeaderTitle");
            if (TxtMediaHUDTitle != null) TxtMediaHUDTitle.Text = loc.Get("MediaHUDTitle");
            if (TxtMediaHUDDesc != null) TxtMediaHUDDesc.Text = loc.Get("MediaHUDDesc");
            if (TxtBatteryHUDTitle != null) TxtBatteryHUDTitle.Text = loc.Get("BatteryHUDTitle");
            if (TxtBatteryHUDDesc != null) TxtBatteryHUDDesc.Text = loc.Get("BatteryHUDDesc");
            if (TxtBluetoothHUDTitle != null) TxtBluetoothHUDTitle.Text = loc.Get("BluetoothHUDTitle");
            if (TxtBluetoothHUDDesc != null) TxtBluetoothHUDDesc.Text = loc.Get("BluetoothHUDDesc");
            if (TxtVolumeHUDTitle != null) TxtVolumeHUDTitle.Text = loc.Get("VolumeHUDTitle");
            if (TxtVolumeHUDDesc != null) TxtVolumeHUDDesc.Text = loc.Get("VolumeHUDDesc");

            // Page 4: Cat Buddy
            if (TxtCatBuddyHeaderTitle != null) TxtCatBuddyHeaderTitle.Text = loc.Get("CatBuddyHeaderTitle");
            if (TxtNailongDesc != null) TxtNailongDesc.Text = loc.Get("NailongDesc");
            if (TxtSpongebobDesc != null) TxtSpongebobDesc.Text = loc.Get("SpongebobDesc");
            if (TxtMinionDesc != null) TxtMinionDesc.Text = loc.Get("MinionDesc");
            if (TxtIdleAltTitle != null) TxtIdleAltTitle.Text = loc.Get("IdleAltTitle");
                        if (TxtIdleCatLabel != null) TxtIdleCatLabel.Text = loc.Get("BtnIdleCat");
            if (TxtIdleBatteryLabel != null) TxtIdleBatteryLabel.Text = loc.Get("BtnIdleBattery");
            if (TxtIdleMusicLabel != null) TxtIdleMusicLabel.Text = loc.Get("BtnIdleMusic");
            if (TxtIdleLyricsLabel != null) TxtIdleLyricsLabel.Text = loc.Get("BtnIdleLyrics");
            if (TxtIdlePerfLabel != null) TxtIdlePerfLabel.Text = loc.Get("BtnIdlePerf");
            if (TxtIdleClockLabel != null) TxtIdleClockLabel.Text = loc.Get("BtnIdleClock");

            // Page 5: Cursor
            if (TxtCursorHeaderTitle != null) TxtCursorHeaderTitle.Text = loc.Get("CursorHeaderTitle");
            if (TxtCursorMagnetTitle != null) TxtCursorMagnetTitle.Text = loc.Get("CursorMagnetTitle");
            if (TxtCursorMagnetDesc != null) TxtCursorMagnetDesc.Text = loc.Get("CursorMagnetDesc");
            if (TxtHoverDelayTitle != null) TxtHoverDelayTitle.Text = loc.Get("HoverDelayTitle");
            if (TxtHoverDelayDesc != null) TxtHoverDelayDesc.Text = loc.Get("HoverDelayDesc");

            // Page 6: Dynamic Size
            if (TxtDynamicSizeHeader != null) TxtDynamicSizeHeader.Text = loc.Get("DynamicSizeHeader");
            if (TxtDynamicSizeDesc != null) TxtDynamicSizeDesc.Text = loc.Get("DynamicSizeDesc");
            if (BtnPresetCompact != null) BtnPresetCompact.Content = loc.Get("PresetKecil");
            if (BtnPresetDefault != null) BtnPresetDefault.Content = loc.Get("PresetStandar");
            if (BtnPresetLarge != null) BtnPresetLarge.Content = loc.Get("PresetBesar");
            if (TxtCompactGroup != null) TxtCompactGroup.Text = loc.Get("CompactGroup");
            if (TxtCompactWidth != null) TxtCompactWidth.Text = loc.Get("CompactWidth");
            if (TxtCompactHeight != null) TxtCompactHeight.Text = loc.Get("CompactHeight");
            if (TxtExpandedGroup != null) TxtExpandedGroup.Text = loc.Get("ExpandedGroup");
            if (TxtExpandedWidth != null) TxtExpandedWidth.Text = loc.Get("ExpandedWidth");
            if (TxtExpandedHeight != null) TxtExpandedHeight.Text = loc.Get("ExpandedHeight");
            if (TxtIdleDefaultTitle != null) TxtIdleDefaultTitle.Text = loc.Get("IdleDefaultTitle");

            // Page 7: Media
            if (TxtMediaHeaderTitle != null) TxtMediaHeaderTitle.Text = loc.Get("MediaHeaderTitle");
            if (TxtAutoMusicTitle != null) TxtAutoMusicTitle.Text = loc.Get("AutoMusicTitle");
            if (TxtAutoMusicDesc != null) TxtAutoMusicDesc.Text = loc.Get("AutoMusicDesc");

            // Page 8: Calendar
            if (TxtCalendarHeaderTitle != null) TxtCalendarHeaderTitle.Text = loc.Get("CalendarHeaderTitle");
            if (TxtCalendarSyncTitle != null) TxtCalendarSyncTitle.Text = loc.Get("CalendarSyncTitle");
            if (TxtCalendarSyncDesc != null) TxtCalendarSyncDesc.Text = loc.Get("CalendarSyncDesc");

            // Page 9: Weather
            if (TxtWeatherHeaderTitle != null) TxtWeatherHeaderTitle.Text = loc.Get("WeatherHeaderTitle");
            if (TxtWeatherCityTitle != null) TxtWeatherCityTitle.Text = loc.Get("WeatherCityTitle");
            if (TxtWeatherUnitTitle != null) TxtWeatherUnitTitle.Text = loc.Get("WeatherUnitTitle");

            // Page 10: Prayer Times
            if (TxtPrayerHeaderTitle != null) TxtPrayerHeaderTitle.Text = loc.Get("PrayerHeaderTitle");
            if (TxtPrayerCityTitle != null) TxtPrayerCityTitle.Text = loc.Get("PrayerCityTitle");
            if (TxtPrayerNotifTitle != null) TxtPrayerNotifTitle.Text = loc.Get("PrayerNotifTitle");
            if (TxtPrayerNotifDesc != null) TxtPrayerNotifDesc.Text = loc.Get("PrayerNotifDesc");

            // Page 11: Pomodoro
            if (TxtPomodoroHeaderTitle != null) TxtPomodoroHeaderTitle.Text = loc.Get("PomodoroHeaderTitle");
            if (TxtPomodoroWorkTitle != null) TxtPomodoroWorkTitle.Text = loc.Get("PomodoroWorkTitle");
            if (TxtPomodoroBreakTitle != null) TxtPomodoroBreakTitle.Text = loc.Get("PomodoroBreakTitle");

            // Page 12: Health
            if (TxtHealthHeaderTitle != null) TxtHealthHeaderTitle.Text = loc.Get("HealthHeaderTitle");
            if (TxtWaterReminderTitle != null) TxtWaterReminderTitle.Text = loc.Get("WaterReminderTitle");
            if (TxtWaterReminderDesc != null) TxtWaterReminderDesc.Text = loc.Get("WaterReminderDesc");
            if (TxtPostureReminderTitle != null) TxtPostureReminderTitle.Text = loc.Get("PostureReminderTitle");
            if (TxtPostureReminderDesc != null) TxtPostureReminderDesc.Text = loc.Get("PostureReminderDesc");
            if (TxtEyeBreakTitle != null) TxtEyeBreakTitle.Text = loc.Get("EyeBreakTitle");
            if (TxtEyeBreakDesc != null) TxtEyeBreakDesc.Text = loc.Get("EyeBreakDesc");

            // Page 13: Screen Time
            if (TxtScreenTimeHeaderTitle != null) TxtScreenTimeHeaderTitle.Text = loc.Get("ScreenTimeHeaderTitle");
            if (TxtTrackScreenTimeTitle != null) TxtTrackScreenTimeTitle.Text = loc.Get("TrackScreenTimeTitle");
            if (TxtTrackScreenTimeDesc != null) TxtTrackScreenTimeDesc.Text = loc.Get("TrackScreenTimeDesc");
            if (TxtScreenLimitTitle != null) TxtScreenLimitTitle.Text = loc.Get("ScreenLimitTitle");
            if (TxtScreenLimitDesc != null) TxtScreenLimitDesc.Text = loc.Get("ScreenLimitDesc");

            // Page 14: Day Progress
            if (TxtDayProgressHeaderTitle != null) TxtDayProgressHeaderTitle.Text = loc.Get("DayProgressHeaderTitle");

            // Page 15: Bluetooth & WiFi
            if (TxtBluetoothWifiHeaderTitle != null) TxtBluetoothWifiHeaderTitle.Text = loc.Get("BluetoothWifiHeaderTitle");
            if (TxtBluetoothDeviceTitle != null) TxtBluetoothDeviceTitle.Text = loc.Get("BluetoothDeviceTitle");
            if (TxtBluetoothDeviceDesc != null) TxtBluetoothDeviceDesc.Text = loc.Get("BluetoothDeviceDesc");
            if (TxtWifiSSIDTitle != null) TxtWifiSSIDTitle.Text = loc.Get("WifiSSIDTitle");
            if (TxtWifiSSIDDesc != null) TxtWifiSSIDDesc.Text = loc.Get("WifiSSIDDesc");

            // Page 16: Welcome
            if (TxtWelcomeTitle != null) TxtWelcomeTitle.Text = loc.Get("WelcomeTitle");
            if (TxtWelcomeDesc != null) TxtWelcomeDesc.Text = loc.Get("WelcomeDesc");
            if (TxtShortcutHeader != null) TxtShortcutHeader.Text = loc.Get("ShortcutHeader");
            if (TxtShortcut1 != null) TxtShortcut1.Text = "\u2022 " + loc.Get("Shortcut1");
            if (TxtShortcut2 != null) TxtShortcut2.Text = "\u2022 " + loc.Get("Shortcut2");
            if (TxtShortcut3 != null) TxtShortcut3.Text = "\u2022 " + loc.Get("Shortcut3");
            if (TxtShortcut4 != null) TxtShortcut4.Text = "\u2022 " + loc.Get("Shortcut4");

            // Page 17: Support Creator
            if (TxtSupportHeaderTitle != null) TxtSupportHeaderTitle.Text = loc.Get("SupportTitle");
            if (TxtSupportHeaderDesc != null) TxtSupportHeaderDesc.Text = loc.Get("SupportDesc");
            if (TxtSaweriaTitle != null) TxtSaweriaTitle.Text = loc.Get("SaweriaTitle");
            if (TxtSaweriaDesc != null) TxtSaweriaDesc.Text = loc.Get("SaweriaDesc");
            if (BtnOpenSaweriaUrl != null) BtnOpenSaweriaUrl.Content = "\uD83C\uDF10 " + loc.Get("BtnOpenSaweria");
            if (BtnCopySaweriaUrl != null) BtnCopySaweriaUrl.Content = "\uD83D\uDCCB " + loc.Get("BtnCopySaweria");

            if (TxtPaypalTitle != null) TxtPaypalTitle.Text = loc.Get("PaypalTitle");
            if (TxtPaypalDesc != null) TxtPaypalDesc.Text = loc.Get("PaypalDesc");
            if (BtnOpenPaypalUrl != null) BtnOpenPaypalUrl.Content = "\uD83C\uDF10 " + loc.Get("BtnOpenPaypal");
            if (BtnCopyPaypalUrl != null) BtnCopyPaypalUrl.Content = "\uD83D\uDCCB " + loc.Get("BtnCopyPaypal");
            if (TxtSupportFooter != null) TxtSupportFooter.Text = loc.Get("SupportFooter");
        }

        // ==========================================
        // EXIT / CLOSE
        // ==========================================
        private void BtnManageDockApps_Click(object sender, RoutedEventArgs e)
        {
            if (NavDockApps != null)
                NavTab_Click(NavDockApps, new RoutedEventArgs());
        }

        private void BtnResetDockSettings_Click(object sender, RoutedEventArgs e)
        {
            var cfg = DockConfig.GetDefault();
            cfg.Save();
            DockManager.Instance.ReloadDock();
            LoadDockAppsList();
        }

        private void BtnAddAppToDockSettings_Click(object sender, RoutedEventArgs e)
        {
            var dlg = new Microsoft.Win32.OpenFileDialog
            {
                Title = "Pilih Aplikasi untuk ditambah ke Dock",
                Filter = "Executable & Shortcuts|*.exe;*.lnk|Semua File|*.*",
                Multiselect = false
            };

            if (dlg.ShowDialog() != true) return;

            string exePath = dlg.FileName;
            string name = System.IO.Path.GetFileNameWithoutExtension(exePath);

            string? autoIcon = null;
            string n = name.ToLowerInvariant();
            if (n.Contains("telegram")) autoIcon = "Assets/icon_Telegram.png";
            else if (n.Contains("fivem")) autoIcon = "Assets/icon_FiveM.png";
            else if (n.Contains("chrome")) autoIcon = "Assets/icon_Chrome.png";
            else if (n.Contains("edge")) autoIcon = "Assets/icon_Edge.png";
            else if (n.Contains("discord")) autoIcon = "Assets/icon_Discord.png";
            else if (n.Contains("whatsapp")) autoIcon = "Assets/icon_WhatsApp.png";
            else if (n.Contains("spotify")) autoIcon = "Assets/icon_Spotify.png";
            else if (n.Contains("calc")) autoIcon = "Assets/icon_Calculator.png";
            else if (n.Contains("capcut")) autoIcon = "Assets/icon_CapCut.png";
            else if (n.Contains("notepad") || n.Contains("catatan")) autoIcon = "Assets/icon_Notepad.png";
            else if (n.Contains("outlook") || n.Contains("mail") || n.Contains("email")) autoIcon = "Assets/icon_Email.png";
            else if (n.Contains("antigravity")) autoIcon = "Assets/icon_Antigravity.png";

            var config = DockConfig.Load();
            config.Items.Add(new DockConfigItem
            {
                Name = name,
                Path = exePath,
                IconAsset = autoIcon
            });
            config.Save();
            DockManager.Instance.ReloadDock();
            LoadDockAppsList();
        }

        private void LoadDockAppsList()
        {
            if (PanelDockSettingsList == null) return;
            PanelDockSettingsList.Children.Clear();

            var config = DockConfig.Load();
            if (TxtDockAppsCount != null)
                TxtDockAppsCount.Text = $"Total: {config.Items.Count} Aplikasi";

            for (int i = 0; i < config.Items.Count; i++)
            {
                int index = i;
                var item = config.Items[i];

                var card = new Border
                {
                    Background = new SolidColorBrush((Color)ColorConverter.ConvertFromString("#15161C")),
                    BorderBrush = new SolidColorBrush((Color)ColorConverter.ConvertFromString("#23252E")),
                    BorderThickness = new Thickness(1),
                    CornerRadius = new CornerRadius(10),
                    Padding = new Thickness(14, 10, 14, 10),
                    Margin = new Thickness(0, 3, 0, 3)
                };

                var grid = new Grid();
                grid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(42) });
                grid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
                grid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(180) });

                // Column 0: Icon squircle
                var iconBorder = new Border
                {
                    Width = 34, Height = 34,
                    CornerRadius = new CornerRadius(9),
                    Background = new SolidColorBrush((Color)ColorConverter.ConvertFromString("#20232B")),
                    HorizontalAlignment = HorizontalAlignment.Left
                };

                UIElement? iconEl = null;
                string asset = item.IconAsset ?? "";
                if (string.IsNullOrEmpty(asset))
                {
                    string n = item.Name.ToLowerInvariant();
                    if (n.Contains("telegram")) asset = "Assets/icon_Telegram.png";
                    else if (n.Contains("fivem")) asset = "Assets/icon_FiveM.png";
                    else if (n.Contains("chrome")) asset = "Assets/icon_Chrome.png";
                    else if (n.Contains("edge")) asset = "Assets/icon_Edge.png";
                    else if (n.Contains("discord")) asset = "Assets/icon_Discord.png";
                    else if (n.Contains("whatsapp")) asset = "Assets/icon_WhatsApp.png";
                    else if (n.Contains("spotify")) asset = "Assets/icon_Spotify.png";
                    else if (n.Contains("notepad") || n.Contains("catatan")) asset = "Assets/icon_Notepad.png";
                    else if (n.Contains("outlook") || n.Contains("email")) asset = "Assets/icon_Email.png";
                    else if (n.Contains("antigravity")) asset = "Assets/icon_Antigravity.png";
                }

                if (!string.IsNullOrEmpty(asset))
                {
                    try
                    {
                        var img = new Image
                        {
                            Source = new BitmapImage(new Uri(asset, UriKind.Relative)),
                            Width = 32, Height = 32, Stretch = Stretch.Uniform
                        };
                        RenderOptions.SetBitmapScalingMode(img, BitmapScalingMode.HighQuality);
                        iconEl = img;
                    }
                    catch { }
                }

                if (iconEl == null && (item.Name.Equals("Musik", StringComparison.OrdinalIgnoreCase) || item.Name.Contains("Music", StringComparison.OrdinalIgnoreCase)))
                {
                    iconBorder.Background = new SolidColorBrush((Color)ColorConverter.ConvertFromString("#FA2D48"));
                    iconEl = new TextBlock { Text = "\u266A", FontSize = 16, Foreground = Brushes.White, HorizontalAlignment = HorizontalAlignment.Center, VerticalAlignment = VerticalAlignment.Center };
                }
                else if (iconEl == null && (item.Name.Equals("Kalender", StringComparison.OrdinalIgnoreCase) || item.Name.Contains("Calendar", StringComparison.OrdinalIgnoreCase)))
                {
                    iconBorder.Background = Brushes.White;
                    iconEl = new TextBlock { Text = DateTime.Now.Day.ToString(), FontSize = 14, FontWeight = FontWeights.Bold, Foreground = Brushes.Black, HorizontalAlignment = HorizontalAlignment.Center, VerticalAlignment = VerticalAlignment.Center };
                }
                else if (iconEl == null && (item.Name.Equals("Pengaturan", StringComparison.OrdinalIgnoreCase) || item.Name.Contains("Settings", StringComparison.OrdinalIgnoreCase)))
                {
                    iconBorder.Background = new SolidColorBrush((Color)ColorConverter.ConvertFromString("#4A5263"));
                    iconEl = new TextBlock { Text = "\u2699", FontSize = 16, Foreground = Brushes.White, HorizontalAlignment = HorizontalAlignment.Center, VerticalAlignment = VerticalAlignment.Center };
                }
                else if (iconEl == null && !string.IsNullOrEmpty(item.Path) && File.Exists(item.Path))
                {
                    try
                    {
                        var shinfo = new NativeMethods.SHFILEINFO();
                        NativeMethods.SHGetFileInfo(item.Path, 0, ref shinfo, (uint)Marshal.SizeOf(shinfo),
                            NativeMethods.SHGFI_ICON | NativeMethods.SHGFI_LARGEICON);
                        if (shinfo.hIcon != IntPtr.Zero)
                        {
                            var bmp = System.Windows.Interop.Imaging.CreateBitmapSourceFromHIcon(
                                shinfo.hIcon, Int32Rect.Empty, BitmapSizeOptions.FromEmptyOptions());
                            iconEl = new Image { Source = bmp, Width = 30, Height = 30, Stretch = Stretch.Uniform };
                            RenderOptions.SetBitmapScalingMode(iconEl, BitmapScalingMode.HighQuality);
                            NativeMethods.DestroyIcon(shinfo.hIcon);
                        }
                    }
                    catch { }
                }

                if (iconEl == null)
                {
                    iconEl = new TextBlock
                    {
                        Text = item.Name.Length > 0 ? item.Name[0].ToString().ToUpperInvariant() : "?",
                        Foreground = Brushes.White,
                        FontWeight = FontWeights.Bold,
                        FontSize = 14,
                        HorizontalAlignment = HorizontalAlignment.Center,
                        VerticalAlignment = VerticalAlignment.Center
                    };
                }

                iconBorder.Child = iconEl;
                Grid.SetColumn(iconBorder, 0);
                grid.Children.Add(iconBorder);

                // Column 1: Info (Name & Path)
                var spInfo = new StackPanel { VerticalAlignment = VerticalAlignment.Center, Margin = new Thickness(6, 0, 10, 0) };
                spInfo.Children.Add(new TextBlock
                {
                    Text = item.Name,
                    Foreground = Brushes.White,
                    FontWeight = FontWeights.SemiBold,
                    FontSize = 13
                });
                spInfo.Children.Add(new TextBlock
                {
                    Text = string.IsNullOrEmpty(item.Path) ? "(Target internal bawaan)" : item.Path,
                    Foreground = new SolidColorBrush((Color)ColorConverter.ConvertFromString("#8E95A5")),
                    FontSize = 10.5,
                    TextTrimming = TextTrimming.CharacterEllipsis,
                    Margin = new Thickness(0, 2, 0, 0)
                });
                Grid.SetColumn(spInfo, 1);
                grid.Children.Add(spInfo);

                // Column 2: Action buttons (Up, Down, Delete)
                var spActions = new StackPanel { Orientation = Orientation.Horizontal, HorizontalAlignment = HorizontalAlignment.Right, VerticalAlignment = VerticalAlignment.Center };

                // Up Button
                var btnUp = new Button
                {
                    Content = "\u25B2",
                    Width = 28, Height = 28,
                    Margin = new Thickness(0, 0, 4, 0),
                    Background = new SolidColorBrush((Color)ColorConverter.ConvertFromString("#232630")),
                    Foreground = Brushes.White,
                    BorderThickness = new Thickness(0),
                    Cursor = Cursors.Hand,
                    IsEnabled = (index > 0),
                    ToolTip = "Pindah ke kiri / awal dock"
                };
                int moveUpIdx = index;
                btnUp.Click += (s, e) =>
                {
                    if (moveUpIdx > 0)
                    {
                        var cfg = DockConfig.Load();
                        var temp = cfg.Items[moveUpIdx];
                        cfg.Items[moveUpIdx] = cfg.Items[moveUpIdx - 1];
                        cfg.Items[moveUpIdx - 1] = temp;
                        cfg.Save();
                        DockManager.Instance.ReloadDock();
                        LoadDockAppsList();
                    }
                };
                spActions.Children.Add(btnUp);

                // Down Button
                var btnDown = new Button
                {
                    Content = "\u25BC",
                    Width = 28, Height = 28,
                    Margin = new Thickness(0, 0, 8, 0),
                    Background = new SolidColorBrush((Color)ColorConverter.ConvertFromString("#232630")),
                    Foreground = Brushes.White,
                    BorderThickness = new Thickness(0),
                    Cursor = Cursors.Hand,
                    IsEnabled = (index < config.Items.Count - 1),
                    ToolTip = "Pindah ke kanan / akhir dock"
                };
                int moveDownIdx = index;
                btnDown.Click += (s, e) =>
                {
                    var cfg = DockConfig.Load();
                    if (moveDownIdx < cfg.Items.Count - 1)
                    {
                        var temp = cfg.Items[moveDownIdx];
                        cfg.Items[moveDownIdx] = cfg.Items[moveDownIdx + 1];
                        cfg.Items[moveDownIdx + 1] = temp;
                        cfg.Save();
                        DockManager.Instance.ReloadDock();
                        LoadDockAppsList();
                    }
                };
                spActions.Children.Add(btnDown);

                // Delete Button
                var btnDelete = new Button
                {
                    Content = "\u2715 Hapus",
                    Height = 28,
                    Padding = new Thickness(10, 0, 10, 0),
                    Background = new SolidColorBrush((Color)ColorConverter.ConvertFromString("#331818")),
                    Foreground = new SolidColorBrush((Color)ColorConverter.ConvertFromString("#F87171")),
                    BorderBrush = new SolidColorBrush((Color)ColorConverter.ConvertFromString("#5C2020")),
                    BorderThickness = new Thickness(1),
                    Cursor = Cursors.Hand,
                    FontSize = 11,
                    FontWeight = FontWeights.SemiBold
                };
                int delIdx = index;
                btnDelete.Click += (s, e) =>
                {
                    var cfg = DockConfig.Load();
                    if (delIdx >= 0 && delIdx < cfg.Items.Count)
                    {
                        cfg.Items.RemoveAt(delIdx);
                        cfg.Save();
                        DockManager.Instance.ReloadDock();
                        LoadDockAppsList();
                    }
                };
                spActions.Children.Add(btnDelete);

                Grid.SetColumn(spActions, 2);
                grid.Children.Add(spActions);

                card.Child = grid;
                PanelDockSettingsList.Children.Add(card);
            }
        }
        private void BtnExitApp_Click(object sender, RoutedEventArgs e)
        {
            Application.Current.Shutdown();
        }
    }
}
