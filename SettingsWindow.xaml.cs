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

        private const int DWMWA_WINDOW_CORNER_PREFERENCE = 33;
        private const int DWMWA_BORDER_COLOR = 34;

        // Back / forward history of visited panes (toolbar chevrons)
        private readonly System.Collections.Generic.List<Button> _history = new();
        private int _historyIndex = -1;

        public SettingsWindow(MainWindow mainWindow, SettingsManager settings)
        {
            _mainWindow = mainWindow;
            _settings = settings;
            // Combo boxes and sliders raise their change events while the XAML is parsed
            // (default IsSelected / Value). Without this guard those defaults overwrote the
            // saved settings every time the window was opened.
            _isInitializing = true;
            InitializeComponent();

            SourceInitialized += SettingsWindow_SourceInitialized;
            Loaded += SettingsWindow_Loaded;
            StateChanged += (s, e) => RootGrid.Margin = WindowState == WindowState.Maximized ? new Thickness(7) : new Thickness(0);
            PreviewKeyDown += SettingsWindow_PreviewKeyDown;
            Closed += (s, e) => MainWindow.TrimAppMemory();
        }

        private void SettingsWindow_SourceInitialized(object? sender, EventArgs e)
        {
            try
            {
                var handle = new WindowInteropHelper(this).Handle;
                int darkMode = 1;
                DwmSetWindowAttribute(handle, DWMWA_USE_IMMERSIVE_DARK_MODE, ref darkMode, sizeof(int));
                int round = 2; // DWMWCP_ROUND: macOS-like rounded window corners
                DwmSetWindowAttribute(handle, DWMWA_WINDOW_CORNER_PREFERENCE, ref round, sizeof(int));
                int border = 0x00403E3C; // COLORREF (BGR): subtle light hairline like a macOS dark window
                DwmSetWindowAttribute(handle, DWMWA_BORDER_COLOR, ref border, sizeof(int));
            }
            catch { }
        }

        // ==========================================
        // WINDOW CHROME (traffic lights) & SHORTCUTS
        // ==========================================
        private void BtnWinClose_Click(object sender, RoutedEventArgs e) => Close();
        private void BtnWinMin_Click(object sender, RoutedEventArgs e) => WindowState = WindowState.Minimized;
        private void BtnWinMax_Click(object sender, RoutedEventArgs e) =>
            WindowState = WindowState == WindowState.Maximized ? WindowState.Normal : WindowState.Maximized;

        private void SettingsWindow_PreviewKeyDown(object sender, KeyEventArgs e)
        {
            bool ctrl = (Keyboard.Modifiers & ModifierKeys.Control) != 0;
            if (ctrl && e.Key == Key.F) { TxtSearch?.Focus(); TxtSearch?.SelectAll(); e.Handled = true; }
            else if (ctrl && e.Key == Key.W) { Close(); e.Handled = true; }
            else if ((Keyboard.Modifiers & ModifierKeys.Alt) != 0 && e.SystemKey == Key.Left) { BtnNavBack_Click(this, e); e.Handled = true; }
            else if ((Keyboard.Modifiers & ModifierKeys.Alt) != 0 && e.SystemKey == Key.Right) { BtnNavForward_Click(this, e); e.Handled = true; }
        }

        private void PageScroller_ScrollChanged(object sender, ScrollChangedEventArgs e)
        {
            // macOS shows the toolbar separator only once content scrolls underneath it
            if (ToolbarLine != null) ToolbarLine.Opacity = PageScroller.VerticalOffset > 1 ? 1 : 0;
        }

        // ==========================================
        // SIDEBAR SEARCH
        // ==========================================
        private (Button nav, FrameworkElement page)[] NavPages => new (Button, FrameworkElement)[]
        {
            (NavWelcome, PageWelcome), (NavGeneral, PageGeneral), (NavModules, PageModules),
            (NavLiveActivities, PageLiveActivities), (NavCatBuddy, PageCatBuddy), (NavCursor, PageCursor),
            (NavDashboard, PageDashboard), (NavMedia, PageMedia), (NavCalendar, PageCalendar),
            (NavWeather, PageWeather), (NavPrayerTimes, PagePrayerTimes), (NavPomodoro, PagePomodoro),
            (NavHealth, PageHealth), (NavScreenTime, PageScreenTime), (NavDayProgress, PageDayProgress),
            (NavBluetoothWifi, PageBluetoothWifi), (NavDockApps, PageDockApps), (NavSupportCreator, PageSupportCreator)
        };

        private void TxtSearch_TextChanged(object sender, TextChangedEventArgs e)
        {
            string q = TxtSearch.Text.Trim();
            bool searching = q.Length > 0;
            TxtSearchPlaceholder.Visibility = searching ? Visibility.Collapsed : Visibility.Visible;
            BtnClearSearch.Visibility = searching ? Visibility.Visible : Visibility.Collapsed;
            TxtSectionDynamic.Visibility = searching ? Visibility.Collapsed : Visibility.Visible;
            TxtSectionModules.Visibility = searching ? Visibility.Collapsed : Visibility.Visible;

            int hits = 0;
            foreach (var (nav, page) in NavPages)
            {
                bool match = !searching || ContainsText(nav, q) || ContainsText(page, q);
                nav.Visibility = match ? Visibility.Visible : Visibility.Collapsed;
                if (match) hits++;
            }
            TxtSearchEmpty.Visibility = searching && hits == 0 ? Visibility.Visible : Visibility.Collapsed;
        }

        private void TxtSearch_PreviewKeyDown(object sender, KeyEventArgs e)
        {
            if (e.Key == Key.Escape) { TxtSearch.Clear(); e.Handled = true; }
            else if (e.Key == Key.Enter)
            {
                // Enter opens the first matching pane
                foreach (var (nav, _) in NavPages)
                    if (nav.Visibility == Visibility.Visible) { ShowPage(nav, true); break; }
                e.Handled = true;
            }
        }

        private void BtnClearSearch_Click(object sender, RoutedEventArgs e)
        {
            TxtSearch.Clear();
            TxtSearch.Focus();
        }

        /// <summary>True when any text inside the element (labels, descriptions, buttons, menu items) contains the query.</summary>
        private static bool ContainsText(DependencyObject root, string q)
        {
            switch (root)
            {
                case TextBlock tb when tb.Text?.Contains(q, StringComparison.CurrentCultureIgnoreCase) == true:
                    return true;
                case ContentControl cc when cc.Content is string s && s.Contains(q, StringComparison.CurrentCultureIgnoreCase):
                    return true;
            }
            foreach (var child in LogicalTreeHelper.GetChildren(root))
                if (child is DependencyObject d && ContainsText(d, q)) return true;
            return false;
        }

        // ==========================================
        // TOOLBAR BACK / FORWARD
        // ==========================================
        private void BtnNavBack_Click(object sender, RoutedEventArgs e)
        {
            if (_historyIndex <= 0) return;
            _historyIndex--;
            ShowPage(_history[_historyIndex], false);
        }

        private void BtnNavForward_Click(object sender, RoutedEventArgs e)
        {
            if (_historyIndex >= _history.Count - 1) return;
            _historyIndex++;
            ShowPage(_history[_historyIndex], false);
        }

        private void UpdateHistoryButtons()
        {
            if (BtnNavBack != null) BtnNavBack.IsEnabled = _historyIndex > 0;
            if (BtnNavForward != null) BtnNavForward.IsEnabled = _historyIndex < _history.Count - 1;
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
                if (ToggleHideFullscreen != null) ToggleHideFullscreen.IsChecked = _settings.AutoGameMode && _settings.GameAction == "AutoHide";
                if (TxtAppVersion != null)
                {
                    var v = System.Reflection.Assembly.GetExecutingAssembly().GetName().Version;
                    TxtAppVersion.Text = v == null ? "" : $" · v{v.Major}.{v.Minor}.{Math.Max(0, v.Build)}";
                }
                if (ToggleAutoHide != null) ToggleAutoHide.IsChecked = _settings.AutoHide;
                if (ToggleHideButton != null) ToggleHideButton.IsChecked = _settings.ShowHideButton;
                if (ToggleTouchscreen != null) ToggleTouchscreen.IsChecked = _settings.TouchscreenSupport;
                if (ToggleKeepWindowsBelow != null) ToggleKeepWindowsBelow.IsChecked = _settings.KeepWindowsBelow;

                if (CmbDisplay != null)
                {
                    CmbDisplay.Items.Clear();
                    CmbDisplay.Items.Add(new ComboBoxItem { Content = "Primary display" });
                    var mons = MainWindow.GetMonitors();
                    for (int i = 0; i < mons.Count; i++)
                        CmbDisplay.Items.Add(new ComboBoxItem { Content = $"Monitor {i + 1}", ToolTip = $"{(int)mons[i].Bounds.Width}x{(int)mons[i].Bounds.Height}{(mons[i].Primary ? " (utama)" : "")}" });
                    CmbDisplay.SelectedIndex = 0;
                    foreach (ComboBoxItem it in CmbDisplay.Items)
                        if (it.Content?.ToString() == _settings.SelectedDisplay) { CmbDisplay.SelectedItem = it; break; }
                }

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
                if (ToggleQuickTools != null) ToggleQuickTools.IsChecked = _settings.ShowToolsTab;
                if (ToggleTaskbarModule != null) ToggleTaskbarModule.IsChecked = _settings.ShowTaskbarTab;
                if (ToggleFloatingDock != null) ToggleFloatingDock.IsChecked = _settings.EnableFloatingDock;

                // ==========================================
                // 3. LIVE ACTIVITIES
                // ==========================================
                if (ToggleMediaHUD != null) ToggleMediaHUD.IsChecked = _settings.ShowMediaHUD;
                if (ToggleBatteryHUD != null) ToggleBatteryHUD.IsChecked = _settings.ShowBatteryHUD;
                if (ToggleBluetoothHUD != null) ToggleBluetoothHUD.IsChecked = _settings.ShowBluetoothHUD;
                if (ToggleVolumeHUD != null) ToggleVolumeHUD.IsChecked = _settings.ShowVolumeHUD;
                if (ToggleNotifAll != null) ToggleNotifAll.IsChecked = _settings.EnableNotifications;
                if (ToggleNotifWhatsApp != null) ToggleNotifWhatsApp.IsChecked = _settings.EnableWhatsAppNotif;
                if (ToggleNotifDiscord != null) ToggleNotifDiscord.IsChecked = _settings.EnableDiscordNotif;
                if (ToggleNotifSteam != null) ToggleNotifSteam.IsChecked = _settings.EnableSteamNotif;
                if (ImgNotifSteam != null) ImgNotifSteam.Source = SteamLogo.Get();

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
                SelectComboItem(CmbWeatherCity, c => c == _settings.WeatherCity);
                SelectComboItem(CmbWeatherUnit, c => !string.IsNullOrEmpty(_settings.WeatherUnit) && c.Contains(_settings.WeatherUnit));

                // ==========================================
                // CALENDAR
                // ==========================================
                if (CmbCalendarFirstDay != null) CmbCalendarFirstDay.SelectedIndex = _settings.CalendarWeekStartsMonday ? 0 : 1;
                if (ToggleCalendarSync != null) ToggleCalendarSync.IsChecked = _settings.CalendarMarkToday;

                // ==========================================
                // BLUETOOTH & WI-FI
                // ==========================================
                if (ToggleAccBattery != null) ToggleAccBattery.IsChecked = _settings.ShowAccessoryBattery;
                if (ToggleWifiSSID != null) ToggleWifiSSID.IsChecked = _settings.ShowWifiSsid;

                // ==========================================
                // DAY PROGRESS (work hours)
                // ==========================================
                FillHourCombo(CmbWorkStart, 5, 12, _settings.WorkdayStartHour > 0 ? _settings.WorkdayStartHour : 9);
                FillHourCombo(CmbWorkEnd, 13, 23, _settings.WorkdayEndHour > 0 ? _settings.WorkdayEndHour : 18);

                // ==========================================
                // 10. PRAYER TIMES
                // ==========================================
                SelectComboItem(CmbPrayerCity, c => !string.IsNullOrEmpty(_settings.PrayerCity) && (c == _settings.PrayerCity || c.Contains(_settings.PrayerCity)));
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
                _history.Clear();
                _history.Add(NavGeneral);
                _historyIndex = 0;
                UpdateHistoryButtons();
                UpdateActiveHeader();
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
            if (sender is Button btn) ShowPage(btn, true);
        }

        private void ShowPage(Button clickedBtn, bool addToHistory)
        {
            if (addToHistory && clickedBtn != _activeNavButton)
            {
                if (_historyIndex < _history.Count - 1) _history.RemoveRange(_historyIndex + 1, _history.Count - _historyIndex - 1);
                _history.Add(clickedBtn);
                _historyIndex = _history.Count - 1;
            }
            UpdateHistoryButtons();
            _activeNavButton = clickedBtn;
            PageScroller?.ScrollToTop();

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

            if (btn == NavGeneral) SetHeader(btn, loc.Get("HeaderGeneral", "General"), loc.Get("HeroGeneral", "Atur posisi, perilaku dan tampilan Dynamic Island di layar."));
            else if (btn == NavModules) SetHeader(btn, loc.Get("HeaderModules", "Modules & Features"), loc.Get("HeroModules", "Pilih modul dan fitur yang muncul di Dynamic Island dan Dock."));
            else if (btn == NavLiveActivities) SetHeader(btn, loc.Get("HeaderLiveActivities", "Live Activities"), loc.Get("HeroLiveActivities", "Pemberitahuan langsung untuk musik, baterai, perangkat Bluetooth dan volume."));
            else if (btn == NavCatBuddy) SetHeader(btn, loc.Get("HeaderCatBuddy", "Cat Buddy & Characters"), loc.Get("HeroCatBuddy", "Pilih karakter animasi dan tampilan saat Dynamic Island sedang diam."));
            else if (btn == NavCursor) SetHeader(btn, loc.Get("HeaderCursor", "Cursor Interaction"), loc.Get("HeroCursor", "Atur cara Dynamic Island merespons kursor mouse."));
            else if (btn == NavDashboard) SetHeader(btn, loc.Get("HeaderDashboard", "Ukuran Dynamic"), loc.Get("HeroDashboard", "Ubah ukuran Dynamic Island saat tertutup maupun terbuka."));
            else if (btn == NavMedia) SetHeader(btn, loc.Get("HeaderMedia", "Media Player"), loc.Get("HeroMedia", "Pengaturan pemutar musik dan kontrol media."));
            else if (btn == NavCalendar) SetHeader(btn, loc.Get("HeaderCalendar", "Calendar"), loc.Get("HeroCalendar", "Kalender mini dan agenda mingguan di Dynamic Island."));
            else if (btn == NavWeather) SetHeader(btn, loc.Get("HeaderWeather", "Weather"), loc.Get("HeroWeather", "Lokasi dan satuan suhu untuk cuaca langsung (Open-Meteo)."));
            else if (btn == NavPrayerTimes) SetHeader(btn, loc.Get("HeaderPrayerTimes", "Prayer Times"), loc.Get("HeroPrayerTimes", "Jadwal sholat harian dan pengingat waktu sholat."));
            else if (btn == NavPomodoro) SetHeader(btn, loc.Get("HeaderPomodoro", "Pomodoro Timer"), loc.Get("HeroPomodoro", "Durasi sesi fokus dan istirahat untuk timer Pomodoro."));
            else if (btn == NavHealth) SetHeader(btn, loc.Get("HeaderHealth", "Health & Wellness"), loc.Get("HeroHealth", "Pengingat minum air, postur duduk dan istirahat mata."));
            else if (btn == NavScreenTime) SetHeader(btn, loc.Get("HeaderScreenTime", "Screen Time"), loc.Get("HeroScreenTime", "Lacak pemakaian aplikasi dan batasi waktu layar harian."));
            else if (btn == NavDayProgress) SetHeader(btn, loc.Get("HeaderDayProgress", "Day Progress"), loc.Get("HeroDayProgress", "Jam kerja yang dipakai untuk menghitung progres harimu."));
            else if (btn == NavBluetoothWifi) SetHeader(btn, loc.Get("HeaderBluetoothWifi", "Bluetooth & Wi-Fi"), loc.Get("HeroBluetoothWifi", "Info perangkat nirkabel dan jaringan Wi-Fi di Dynamic Island."));
            else if (btn == NavWelcome) SetHeader(btn, loc.Get("HeaderWelcome", "Welcome"), loc.Get("HeroWelcome", "Kenalan dengan Dynamic Island Pro dan pintasan-pintasannya."));
            else if (btn == NavSupportCreator) SetHeader(btn, loc.Get("HeaderSupportCreator", "Support Creator"), loc.Get("SupportDesc", "Dukung pengembangan Dynamic Island Pro."));
            else if (btn == NavDockApps) SetHeader(btn, "Aplikasi Dock", "Atur aplikasi, urutan dan ikon yang tampil di Dock melayang.");
        }

        /// <summary>Toolbar title + page hero. The hero reuses the colour tile and glyph of the sidebar row.</summary>
        private void SetHeader(Button nav, string title, string description)
        {
            if (HeaderTitleText != null) HeaderTitleText.Text = title;
            if (HeroTitle != null) HeroTitle.Text = title;
            if (HeroDesc != null) HeroDesc.Text = description;

            if (nav == NavWelcome)
            {
                // the Welcome pane is the profile card: show the app logo instead of a glyph tile
                if (HeroIconBox != null)
                {
                    HeroIconBox.Background = new ImageBrush(new BitmapImage(new Uri("pack://application:,,,/DynamicIsland;component/Assets/logo.png"))) { Stretch = Stretch.Uniform };
                    HeroIconBox.CornerRadius = new CornerRadius(26);
                }
                if (HeroIconGlyph != null) HeroIconGlyph.Text = "";
                return;
            }

            if (nav.Content is StackPanel sp && sp.Children.Count > 0 && sp.Children[0] is Border tile && tile.Child is TextBlock glyph)
            {
                if (HeroIconBox != null)
                {
                    HeroIconBox.Background = tile.Background;
                    HeroIconBox.CornerRadius = new CornerRadius(12);
                }
                if (HeroIconGlyph != null)
                {
                    HeroIconGlyph.Text = glyph.Text;
                    HeroIconGlyph.Foreground = glyph.Foreground;
                }
            }
        }
        // 1. GENERAL CONTROLS
        // ==========================================
        private void ToggleLaunchAtLogin_Click(object sender, RoutedEventArgs e)
        {
            if (_settings != null) _settings.StartWithWindows = ToggleLaunchAtLogin.IsChecked == true;
        }

        private void CmbDisplay_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            if (_isInitializing) return;
            if (_settings != null && CmbDisplay?.SelectedItem is ComboBoxItem item)
            {
                _settings.SelectedDisplay = item.Content?.ToString() ?? "Primary display";
                _mainWindow?.ApplyDockLayout();
                _mainWindow?.ApplyKeepWindowsBelow();
            }
        }

        private void SliderPosition_ValueChanged(object sender, RoutedPropertyChangedEventArgs<double> e)
        {
            if (_isInitializing) return;
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
            if (_settings == null) return;
            bool hide = ToggleHideFullscreen.IsChecked == true;
            if (hide) _settings.AutoGameMode = true;      // fullscreen detection must run
            _settings.GameAction = hide ? "AutoHide" : "IgnoreHover";
            _mainWindow?.RefreshGameMode();
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
            if (_settings == null) return;
            _settings.KeepWindowsBelow = ToggleKeepWindowsBelow.IsChecked == true;
            _mainWindow?.ApplyKeepWindowsBelow();
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
            _mainWindow?.ApplyTabVisibility();
        }

        private void ToggleQuickTools_Click(object sender, RoutedEventArgs e)
        {
            if (_settings == null) return;
            _settings.ShowToolsTab = ToggleQuickTools.IsChecked == true;
            _mainWindow?.ApplyTabVisibility();
        }

        private void ToggleTaskbarModule_Click(object sender, RoutedEventArgs e)
        {
            if (_settings == null) return;
            _settings.ShowTaskbarTab = ToggleTaskbarModule.IsChecked == true;
            _mainWindow?.ApplyTabVisibility();
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
                if (_settings.EnableFloatingDock) DockManager.Instance.RevealDock(); // show it once so the user sees it is on
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

        private void ToggleNotifAll_Click(object sender, RoutedEventArgs e)
        {
            if (_settings != null) _settings.EnableNotifications = ToggleNotifAll.IsChecked == true;
        }

        private void ToggleNotifWhatsApp_Click(object sender, RoutedEventArgs e)
        {
            if (_settings != null) _settings.EnableWhatsAppNotif = ToggleNotifWhatsApp.IsChecked == true;
        }

        private void ToggleNotifDiscord_Click(object sender, RoutedEventArgs e)
        {
            if (_settings != null) _settings.EnableDiscordNotif = ToggleNotifDiscord.IsChecked == true;
        }

        private void ToggleNotifSteam_Click(object sender, RoutedEventArgs e)
        {
            if (_settings != null) _settings.EnableSteamNotif = ToggleNotifSteam.IsChecked == true;
        }

        private void BtnNotifPermission_Click(object sender, RoutedEventArgs e)
        {
            NotificationManager.Instance?.OpenNotificationSettings();
        }

        private void BtnTestNotifWhatsApp_Click(object sender, RoutedEventArgs e)
        {
            NotificationManager.Instance?.SimulateWhatsAppNotification();
        }

        private void BtnTestNotifDiscord_Click(object sender, RoutedEventArgs e)
        {
            NotificationManager.Instance?.SimulateDiscordNotification();
        }

        private void BtnTestNotifSteam_Click(object sender, RoutedEventArgs e)
        {
            NotificationManager.Instance?.SimulateSteamNotification();
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
            var activeBorder = new SolidColorBrush(Color.FromRgb(0x0A, 0x84, 0xFF));
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
            if (_isInitializing) return;
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
            if (_isInitializing) return;
            if (TxtCompactW != null) TxtCompactW.Text = $"{(int)e.NewValue} px";
            if (_mainWindow != null && _mainWindow.Settings != null)
            {
                _mainWindow.Settings.CompactWidth = e.NewValue;
                _mainWindow.ApplyCurrentSizeSettings();
            }
        }

        private void SliderCompactH_ValueChanged(object sender, RoutedPropertyChangedEventArgs<double> e)
        {
            if (_isInitializing) return;
            if (TxtCompactH != null) TxtCompactH.Text = $"{(int)e.NewValue} px";
            if (_mainWindow != null && _mainWindow.Settings != null)
            {
                _mainWindow.Settings.CompactHeight = e.NewValue;
                _mainWindow.ApplyCurrentSizeSettings();
            }
        }

        private void SliderExpandedW_ValueChanged(object sender, RoutedPropertyChangedEventArgs<double> e)
        {
            if (_isInitializing) return;
            if (TxtExpandedW != null) TxtExpandedW.Text = $"{(int)e.NewValue} px";
            if (_mainWindow != null && _mainWindow.Settings != null)
            {
                _mainWindow.Settings.ExpandedWidth = e.NewValue;
                _mainWindow.ApplyCurrentSizeSettings();
            }
        }

        private void SliderExpandedH_ValueChanged(object sender, RoutedPropertyChangedEventArgs<double> e)
        {
            if (_isInitializing) return;
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

        // ==========================================
        // 9. WEATHER CONTROLS
        // ==========================================
        private void CmbWeatherCity_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            if (_isInitializing) return;
            if (_settings != null && CmbWeatherCity?.SelectedItem is ComboBoxItem item)
            {
                _settings.WeatherCity = item.Content?.ToString() ?? "Jakarta";
            }
        }

        private void CmbWeatherUnit_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            if (_isInitializing) return;
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
            if (_isInitializing) return;
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
            if (_isInitializing) return;
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
            if (_isInitializing) return;
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
            if (_isInitializing) return;
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
            if (_isInitializing) return;
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
            if (_isInitializing) return;
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
        // 8. CALENDAR CONTROLS
        // ==========================================
        private void CmbCalendarFirstDay_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            if (_isInitializing || _settings == null || CmbCalendarFirstDay == null) return;
            _settings.CalendarWeekStartsMonday = CmbCalendarFirstDay.SelectedIndex != 1;
            _mainWindow?.Metrics?.RefreshAgenda();
        }

        private void ToggleCalendarSync_Click(object sender, RoutedEventArgs e)
        {
            if (_settings == null) return;
            _settings.CalendarMarkToday = ToggleCalendarSync.IsChecked == true;
            _mainWindow?.Metrics?.RefreshAgenda();
        }

        // ==========================================
        // 15. BLUETOOTH & WI-FI CONTROLS
        // ==========================================
        private void ToggleAccBattery_Click(object sender, RoutedEventArgs e)
        {
            if (_settings != null) _settings.ShowAccessoryBattery = ToggleAccBattery.IsChecked == true;
        }

        private void ToggleWifiSSID_Click(object sender, RoutedEventArgs e)
        {
            if (_settings != null) _settings.ShowWifiSsid = ToggleWifiSSID.IsChecked == true;
        }

        // ==========================================
        // HELPERS
        // ==========================================
        /// <summary>Selects the first item whose text matches; falls back to the first item.</summary>
        private static void SelectComboItem(ComboBox? combo, Func<string, bool> match)
        {
            if (combo == null || combo.Items.Count == 0) return;
            foreach (var o in combo.Items)
                if (o is ComboBoxItem it && match(it.Content?.ToString() ?? "")) { combo.SelectedItem = it; return; }
            combo.SelectedIndex = 0;
        }

        private static void FillHourCombo(ComboBox? combo, int from, int to, int selected)
        {
            if (combo == null) return;
            combo.Items.Clear();
            selected = Math.Clamp(selected, from, to);
            for (int h = from; h <= to; h++)
            {
                var it = new ComboBoxItem { Content = $"{h:00}:00" };
                combo.Items.Add(it);
                if (h == selected) combo.SelectedItem = it;
            }
        }

        private static string TitleCase(string s) =>
            System.Globalization.CultureInfo.CurrentCulture.TextInfo.ToTitleCase((s ?? "").ToLower());

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
            if (TxtSectionDynamic != null) TxtSectionDynamic.Text = TitleCase(loc.Get("SectionDynamic", "DYNAMIC"));
            if (TxtSectionModules != null) TxtSectionModules.Text = TitleCase(loc.Get("SectionModules", "MODUL & FITUR"));

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
            if (BtnQuickSupport != null) BtnQuickSupport.Content = loc.Get("NavSupportCreator");
            if (BtnExitApp != null) BtnExitApp.Content = loc.Get("BtnExitApp");

            // Page 1: General
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
            if (TxtSaweriaTitle != null) TxtSaweriaTitle.Text = loc.Get("SaweriaTitle");
            if (TxtSaweriaDesc != null) TxtSaweriaDesc.Text = loc.Get("SaweriaDesc");
            if (BtnOpenSaweriaUrl != null) BtnOpenSaweriaUrl.Content = loc.Get("BtnOpenSaweria");
            if (BtnCopySaweriaUrl != null) BtnCopySaweriaUrl.Content = loc.Get("BtnCopySaweria");

            if (TxtPaypalTitle != null) TxtPaypalTitle.Text = loc.Get("PaypalTitle");
            if (TxtPaypalDesc != null) TxtPaypalDesc.Text = loc.Get("PaypalDesc");
            if (BtnOpenPaypalUrl != null) BtnOpenPaypalUrl.Content = loc.Get("BtnOpenPaypal");
            if (BtnCopyPaypalUrl != null) BtnCopyPaypalUrl.Content = loc.Get("BtnCopyPaypal");
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

                // macOS grouped-list row: hairline separator between rows instead of separate cards
                var card = new Border
                {
                    Background = Brushes.Transparent,
                    BorderBrush = (Brush)FindResource("SepBrush"),
                    BorderThickness = new Thickness(0, 0, 0, i < config.Items.Count - 1 ? 1 : 0),
                    Padding = new Thickness(0, 8, 0, 8)
                };
                var macButton = (Style)FindResource("RoundedBtnStyle");
                var glyphFont = (FontFamily)FindResource("Glyphs");

                var grid = new Grid();
                grid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(42) });
                grid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
                grid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(180) });

                // Column 0: Icon squircle
                var iconBorder = new Border
                {
                    Width = 32, Height = 32,
                    CornerRadius = new CornerRadius(8),
                    Background = new SolidColorBrush((Color)ColorConverter.ConvertFromString("#3A3A3C")),
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
                    Foreground = new SolidColorBrush((Color)ColorConverter.ConvertFromString("#98989D")),
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
                    Content = "\uE70E",
                    Style = macButton,
                    FontFamily = glyphFont,
                    FontSize = 10,
                    Width = 28, Height = 24, Padding = new Thickness(0),
                    Margin = new Thickness(0, 0, 4, 0),
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
                    Content = "\uE70D",
                    Style = macButton,
                    FontFamily = glyphFont,
                    FontSize = 10,
                    Width = 28, Height = 24, Padding = new Thickness(0),
                    Margin = new Thickness(0, 0, 8, 0),
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
                    Content = "Hapus",
                    Style = macButton,
                    Height = 24,
                    Padding = new Thickness(12, 0, 12, 0),
                    Foreground = new SolidColorBrush((Color)ColorConverter.ConvertFromString("#FF6961")),
                    FontSize = 12.5
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
