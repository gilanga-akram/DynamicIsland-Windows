// Language: C#
// Filename: FloatingDockWindow.xaml.cs
// Runs on: Windows 11 / .NET 9
// Targets: Floating Dock — dynamic icons, customizable, glass pill

using System;
using System.Diagnostics;
using System.IO;
using System.Runtime.InteropServices;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using System.Windows.Shapes;
using System.Windows.Threading;

namespace DynamicIsland
{
    public partial class FloatingDockWindow : Window
    {
        private DispatcherTimer _clockTimer;
        private int _hydrationSeconds = 57 * 60 + 12;
        private QuickControlWindow? _quickControl;
        private DockConfig _dockConfig;
        private readonly DispatcherTimer _gameAvoidTimer;
        private bool _isBehindGame = false;

        public FloatingDockWindow()
        {
            InitializeComponent();
            _dockConfig = DockConfig.Load();

            Loaded += FloatingDockWindow_Loaded;
            ContextMenu = BuildDockContextMenu();

            _clockTimer = new DispatcherTimer { Interval = TimeSpan.FromSeconds(1) };
            _clockTimer.Tick += ClockTimer_Tick;
            _clockTimer.Start();

            _gameAvoidTimer = new DispatcherTimer { Interval = TimeSpan.FromMilliseconds(250) };
            _gameAvoidTimer.Tick += GameAvoidTimer_Tick;
            _gameAvoidTimer.Start();
        }

        private void FloatingDockWindow_Loaded(object sender, RoutedEventArgs e)
        {
            UpdatePosition();
            UpdateClockAndCalendar();
            RebuildPinnedApps();
        }

        public bool IsContextMenuOpen
        {
            get
            {
                if (ContextMenu?.IsOpen == true) return true;
                foreach (var child in PanelPinnedApps.Children)
                    if (child is Button b && b.ContextMenu?.IsOpen == true) return true;
                return _quickControl?.IsVisible == true;
            }
        }

        public void UpdatePosition()
        {
            double sw = SystemParameters.PrimaryScreenWidth;
            Height = 104; if (Width < 640) Width = 920;
            Left = (sw - Width) / 2.0;
            Top = SystemParameters.PrimaryScreenHeight - Height - 6;
        }

        public void ReloadConfig()
        {
            _dockConfig = DockConfig.Load();
            RebuildPinnedApps();
        }

        // ─────────────────────────────────────────────────────────────────
        #region Dynamic Dock

        private void RebuildPinnedApps()
        {
            PanelPinnedApps.Children.Clear();

            // Recalculate dock width based on item count
            int itemCount = _dockConfig.Items.Count;
            // Each item ~43px + fixed elements ~380px
            double sw = SystemParameters.PrimaryScreenWidth; Width = Math.Min(sw - 30, Math.Max(640, 480 + itemCount * 54));
            UpdatePosition();

            foreach (var item in _dockConfig.Items)
            {
                var btn = BuildDockButton(item);
                PanelPinnedApps.Children.Add(btn);
            }
        }

        private Button BuildDockButton(DockConfigItem item)
        {
            var btn = new Button
            {
                Style = (Style)Resources["DockIconBtn"],
                ToolTip = item.Name,
                Tag = item
            };

            // Icon tile
            var border = new Border
            {
                Width = 46, Height = 46,
                CornerRadius = new CornerRadius(12),
                Background = string.IsNullOrEmpty(item.IconBg)
                    ? Brushes.Transparent
                    : new SolidColorBrush((Color)ColorConverter.ConvertFromString(item.IconBg)!),
            };
            border.Effect = new System.Windows.Media.Effects.DropShadowEffect
            {
                BlurRadius = 8, ShadowDepth = 2, Direction = 270,
                Color = Colors.Black, Opacity = 0.25
            };

            // Try load icon
            UIElement? iconEl = null;

            // 0. Auto-detect known icon asset if not specified
            if (string.IsNullOrEmpty(item.IconAsset))
            {
                string n = item.Name.ToLowerInvariant();
                if (n.Contains("telegram")) item.IconAsset = "Assets/icon_Telegram.png";
                else if (n.Contains("fivem")) item.IconAsset = "Assets/icon_FiveM.png";
                else if (n.Contains("explorer")) item.IconAsset = "Assets/icon_Explorer.png";
                else if (n.Contains("chrome")) item.IconAsset = "Assets/icon_Chrome.png";
                else if (n.Contains("edge")) item.IconAsset = "Assets/icon_Edge.png";
                else if (n.Contains("discord")) item.IconAsset = "Assets/icon_Discord.png";
                else if (n.Contains("whatsapp")) item.IconAsset = "Assets/icon_WhatsApp.png";
                else if (n.Contains("spotify")) item.IconAsset = "Assets/icon_Spotify.png";
                else if (n.Contains("calc")) item.IconAsset = "Assets/icon_Calculator.png";
                else if (n.Contains("capcut")) item.IconAsset = "Assets/icon_CapCut.png";
                else if (n.Contains("notepad") || n.Contains("catatan")) item.IconAsset = "Assets/icon_Notepad.png";
                else if (n.Contains("outlook") || n.Contains("mail") || n.Contains("email")) item.IconAsset = "Assets/icon_Email.png";
                else if (n.Contains("antigravity")) item.IconAsset = "Assets/icon_Antigravity.png";
            }

            // 1. Builtin asset
            if (!string.IsNullOrEmpty(item.IconAsset))
            {
                try
                {
                    var img = new Image
                    {
                        Source = new BitmapImage(new Uri(item.IconAsset, UriKind.Relative)),
                        Width = 46, Height = 46, Stretch = Stretch.Uniform
                    };
                    RenderOptions.SetBitmapScalingMode(img, BitmapScalingMode.HighQuality);
                    iconEl = img;
                }
                catch { }
            }

            // 2. Special built-in styles
            if (iconEl == null && (item.Name.Equals("Musik", StringComparison.OrdinalIgnoreCase) || item.Name.Contains("Music", StringComparison.OrdinalIgnoreCase)))
            {
                border.Background = new SolidColorBrush((Color)ColorConverter.ConvertFromString("#FA2D48"));
                var path = new System.Windows.Shapes.Path
                {
                    Stretch = Stretch.Uniform,
                    Width = 20,
                    Height = 20,
                    Fill = Brushes.White,
                    HorizontalAlignment = HorizontalAlignment.Center,
                    VerticalAlignment = VerticalAlignment.Center,
                    Data = Geometry.Parse("M12,3v10.55c-.59-.34-1.27-.55-2-.55-2.21,0-4,1.79-4,4s1.79,4,4,4 4-1.79,4-4V7h4V3h-6z")
                };
                iconEl = path;
            }
            else if (iconEl == null && (item.Name.Equals("Kalender", StringComparison.OrdinalIgnoreCase) || item.Name.Contains("Calendar", StringComparison.OrdinalIgnoreCase)))
            {
                border.Background = Brushes.White;
                var now = DateTime.Now;
                var grid = new Grid();
                var topBar = new Border
                {
                    Height = 14,
                    VerticalAlignment = VerticalAlignment.Top,
                    Background = new SolidColorBrush((Color)ColorConverter.ConvertFromString("#FF3B30")),
                    CornerRadius = new CornerRadius(12, 12, 0, 0)
                };
                var tbMonth = new TextBlock
                {
                    Text = now.ToString("MMM", new System.Globalization.CultureInfo("id-ID")).ToUpper(),
                    Foreground = Brushes.White,
                    FontSize = 8.5,
                    FontWeight = FontWeights.Bold,
                    HorizontalAlignment = HorizontalAlignment.Center,
                    VerticalAlignment = VerticalAlignment.Center
                };
                topBar.Child = tbMonth;
                grid.Children.Add(topBar);

                var tbDay = new TextBlock
                {
                    Text = now.Day.ToString(),
                    Foreground = new SolidColorBrush(Color.FromRgb(17, 17, 17)),
                    FontSize = 18,
                    FontWeight = FontWeights.Bold,
                    FontFamily = new FontFamily("Segoe UI Variable Display, Consolas, sans-serif"),
                    HorizontalAlignment = HorizontalAlignment.Center,
                    VerticalAlignment = VerticalAlignment.Center,
                    Margin = new Thickness(0, 11, 0, 0)
                };
                grid.Children.Add(tbDay);
                iconEl = grid;
            }
            else if (iconEl == null && (item.Name.Equals("Pengaturan", StringComparison.OrdinalIgnoreCase) || item.Name.Contains("Settings", StringComparison.OrdinalIgnoreCase)))
            {
                border.Background = new SolidColorBrush((Color)ColorConverter.ConvertFromString("#8E8E93"));
                border.BorderBrush = new SolidColorBrush((Color)ColorConverter.ConvertFromString("#33FFFFFF"));
                border.BorderThickness = new Thickness(1);
                var path = new System.Windows.Shapes.Path
                {
                    Stretch = Stretch.Uniform,
                    Width = 21,
                    Height = 21,
                    Fill = Brushes.White,
                    HorizontalAlignment = HorizontalAlignment.Center,
                    VerticalAlignment = VerticalAlignment.Center,
                    Data = Geometry.Parse("M19.14,12.94c0.04-.3.06-.61.06-.94 0-.32-.02-.64-.07-.94l2.03-1.58c.18-.14.23-.41.12-.61l-1.92-3.32c-.12-.22-.37-.29-.59-.22l-2.39.96c-.5-.38-1.03-.7-1.62-.94l-.36-2.54c-.04-.24-.24-.41-.48-.41h-3.84c-.24,0-.43.17-.47.41l-.36,2.54c-.59.24-1.13.57-1.62.94l-2.39-.96c-.22-.08-.47,0-.59.22L2.74,8.87c-.12.21-.08.47.12.61l2.03,1.58c-.05.3-.09.63-.09.94s.02.64.07.94l-2.03,1.58c-.18.14-.23.41-.12.61l1.92,3.32c.12.22.37.29.59.22l2.39-.96c.5.38,1.03.7,1.62.94l.36,2.54c.05.24.24.41.48.41h3.84c.24,0,.44-.17.47-.41l.36-2.54c.59-.24,1.13-.56,1.62-.94l2.39.96c.22.08.47,0,.59-.22l1.92-3.32c.12-.22.07-.47-.12-.61l-2.01-1.58zM12,15.6c-1.98,0-3.6-1.62-3.6-3.6s1.62-3.6,3.6-3.6 3.6,1.62,3.6,3.6-1.62,3.6-3.6,3.6z")
                };
                iconEl = path;
            }

            // 3. Extract from file or shortcut via Shell SHGetFileInfo
            if (iconEl == null && !string.IsNullOrEmpty(item.Path) && File.Exists(item.Path))
            {
                try
                {
                    var shinfo = new NativeMethods.SHFILEINFO();
                    IntPtr hImg = NativeMethods.SHGetFileInfo(
                        item.Path,
                        0,
                        ref shinfo,
                        (uint)Marshal.SizeOf(shinfo),
                        NativeMethods.SHGFI_ICON | NativeMethods.SHGFI_LARGEICON);

                    if (shinfo.hIcon != IntPtr.Zero)
                    {
                        var bmp = System.Windows.Interop.Imaging.CreateBitmapSourceFromHIcon(
                            shinfo.hIcon, Int32Rect.Empty, BitmapSizeOptions.FromEmptyOptions());
                        iconEl = new Image { Source = bmp, Width = 44, Height = 44, Stretch = Stretch.Uniform };
                        RenderOptions.SetBitmapScalingMode(iconEl, BitmapScalingMode.HighQuality);
                        NativeMethods.DestroyIcon(shinfo.hIcon);
                    }
                }
                catch { }

                // If SHGetFileInfo returned zero and it's a shortcut (.lnk), resolve target and extract
                if (iconEl == null && item.Path.EndsWith(".lnk", StringComparison.OrdinalIgnoreCase))
                {
                    try
                    {
                        Type? shellType = Type.GetTypeFromProgID("WScript.Shell");
                        if (shellType != null)
                        {
                            dynamic? shell = Activator.CreateInstance(shellType);
                            dynamic? sc = shell?.CreateShortcut(item.Path);
                            string? target = sc?.TargetPath;
                            if (!string.IsNullOrEmpty(target) && File.Exists(target))
                            {
                                var shinfo = new NativeMethods.SHFILEINFO();
                                NativeMethods.SHGetFileInfo(target, 0, ref shinfo, (uint)Marshal.SizeOf(shinfo),
                                    NativeMethods.SHGFI_ICON | NativeMethods.SHGFI_LARGEICON);
                                if (shinfo.hIcon != IntPtr.Zero)
                                {
                                    var bmp = System.Windows.Interop.Imaging.CreateBitmapSourceFromHIcon(
                                        shinfo.hIcon, Int32Rect.Empty, BitmapSizeOptions.FromEmptyOptions());
                                    iconEl = new Image { Source = bmp, Width = 44, Height = 44, Stretch = Stretch.Uniform };
                                    RenderOptions.SetBitmapScalingMode(iconEl, BitmapScalingMode.HighQuality);
                                    NativeMethods.DestroyIcon(shinfo.hIcon);
                                }
                            }
                        }
                    }
                    catch { }
                }
            }

            // 4. Fallback: first letter
            if (iconEl == null)
            {
                iconEl = new TextBlock
                {
                    Text = item.Name.Length > 0 ? item.Name[0].ToString().ToUpperInvariant() : "?",
                    Foreground = Brushes.White,
                    FontSize = 16, FontWeight = FontWeights.Bold,
                    HorizontalAlignment = HorizontalAlignment.Center,
                    VerticalAlignment = VerticalAlignment.Center
                };
                border.Background = new SolidColorBrush(Color.FromArgb(0x60, 0x50, 0x70, 0xA0));
            }

            border.Child = iconEl;
            btn.Content = border;

            // Click → launch
            btn.Click += (s, e) =>
            {
                var cfg = (DockConfigItem)((Button)s).Tag!;
                if (cfg.Name.Equals("Antigravity", StringComparison.OrdinalIgnoreCase))
                {
                    string localProg = System.IO.Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "Programs", "antigravity", "Antigravity.exe");
                    if (File.Exists(localProg)) DockManager.LaunchApp(localProg);
                    else DockManager.LaunchApp("antigravity");
                }
                else if (cfg.Name.Equals("Pengaturan", StringComparison.OrdinalIgnoreCase))
                {
                    AppSettings_Click(s, e);
                }
                else
                {
                    DockManager.LaunchApp(cfg.Path, cfg.Fallback);
                }
            };

            // Right-click → context menu
            btn.ContextMenu = BuildItemContextMenu(item);

            return btn;
        }

        private ContextMenu BuildItemContextMenu(DockConfigItem item)
        {
            var cm = new ContextMenu();

            var header = new MenuItem
            {
                Header = $"📌 {item.Name}",
                IsEnabled = false,
                FontWeight = FontWeights.Bold
            };
            cm.Items.Add(header);
            cm.Items.Add(new Separator());

            var removeItem = new MenuItem { Header = "❌  Hapus dari Dock" };
            removeItem.Click += (s, e) =>
            {
                _dockConfig.Items.Remove(item);
                _dockConfig.Save();
                RebuildPinnedApps();
            };
            cm.Items.Add(removeItem);

            var addAbove = new MenuItem { Header = "➕  Tambah Aplikasi Baru..." };
            addAbove.Click += (s, e) => AddAppToDock();
            cm.Items.Add(addAbove);

            var resetItem = new MenuItem { Header = "🔄  Reset Dock ke Default" };
            resetItem.Click += (s, e) =>
            {
                _dockConfig = DockConfig.GetDefault();
                _dockConfig.Save();
                RebuildPinnedApps();
            };
            cm.Items.Add(resetItem);

            return cm;
        }

        private ContextMenu BuildDockContextMenu()
        {
            var cm = new ContextMenu();

            var addItem = new MenuItem { Header = "➕  Tambah Aplikasi ke Dock..." };
            addItem.Click += (s, e) => AddAppToDock();
            cm.Items.Add(addItem);

            var resetItem = new MenuItem { Header = "🔄  Reset Dock ke Default" };
            resetItem.Click += (s, e) =>
            {
                _dockConfig = DockConfig.GetDefault();
                _dockConfig.Save();
                RebuildPinnedApps();
            };
            cm.Items.Add(resetItem);

            cm.Items.Add(new Separator());

            var launchpadItem = new MenuItem { Header = "Buka Menu Aplikasi (Launchpad)" };
            launchpadItem.Click += (s, e) => DockManager.Instance.ToggleLaunchpad();
            cm.Items.Add(launchpadItem);

            var settingsItem = new MenuItem { Header = "Buka Pengaturan..." };
            settingsItem.Click += (s, e) => AppSettings_Click(s, e);
            cm.Items.Add(settingsItem);

            cm.Items.Add(new Separator());

            var restoreItem = new MenuItem { Header = "Kembali ke Taskbar Windows" };
            restoreItem.Click += (s, e) =>
            {
                SettingsManager.Instance.EnableFloatingDock = false;
                DockManager.Instance.SetDockEnabled(false);
            };
            cm.Items.Add(restoreItem);

            return cm;
        }

        private void AddAppToDock()
        {
            var dlg = new Microsoft.Win32.OpenFileDialog
            {
                Title = "Pilih Aplikasi untuk ditambah ke Dock",
                Filter = "Executable|*.exe;*.lnk|Semua File|*.*",
                Multiselect = false
            };

            if (dlg.ShowDialog() != true) return;

            string exePath = dlg.FileName;
            string name    = System.IO.Path.GetFileNameWithoutExtension(exePath);

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
            else if (n.Contains("antigravity")) autoIcon = "Assets/icon_Antigravity.png";

            var newItem = new DockConfigItem
            {
                Name     = name,
                Path     = exePath,
                IconAsset = autoIcon
            };

            _dockConfig.Items.Add(newItem);
            _dockConfig.Save();
            RebuildPinnedApps();
        }

        #endregion

        // ─────────────────────────────────────────────────────────────────
        #region Clock / Calendar

        private void ClockTimer_Tick(object? sender, EventArgs e)
        {
            UpdateClockAndCalendar();
            if (_hydrationSeconds > 0)
            {
                _hydrationSeconds--;
                TxtDockTimer.Text = $"{_hydrationSeconds / 60:00}:{_hydrationSeconds % 60:00}";
            }
            else
            {
                _hydrationSeconds = 3600;
            }
        }

        private void UpdateClockAndCalendar()
        {
            var now = DateTime.Now;
            TxtDockClock.Text = now.ToString("HH:mm");
            TxtDockDate.Text  = now.ToString("dd/MM");
            try
            {
                if (Application.Current.MainWindow is MainWindow mw && mw.Metrics != null)
                {
                    TxtDockBattery.Text = $"{mw.Metrics.BatteryPercentage}%";
                    if (!string.IsNullOrEmpty(mw.Metrics.WeatherTemp))
                        TxtDockWeather.Text = mw.Metrics.WeatherTemp;
                }
            }
            catch { }
        }

        #endregion

        // ─────────────────────────────────────────────────────────────────
        #region Fixed Buttons

        private void BtnLauncher_Click(object sender, RoutedEventArgs e)
            => DockManager.Instance.ToggleLaunchpad();

        private void BtnDockTimer_Click(object sender, RoutedEventArgs e)
        {
            _hydrationSeconds = 3600;
            TxtDockTimer.Text = "60:00";
        }

        private void TrayChevron_Click(object sender, RoutedEventArgs e)
        {
            try { Process.Start(new ProcessStartInfo("ms-actioncenter:") { UseShellExecute = true }); }
            catch { }
        }

        private void TrayWifi_Click(object sender, RoutedEventArgs e)   => ToggleQuickControl();
        private void TrayVolume_Click(object sender, RoutedEventArgs e)  => ToggleQuickControl();
        private void TrayBattery_Click(object sender, RoutedEventArgs e) => ToggleQuickControl();

        private void ToggleQuickControl()
        {
            _quickControl ??= new QuickControlWindow();
            if (_quickControl.IsVisible)
                _quickControl.Hide();
            else
                _quickControl.ShowAt(Left + Width - 10, Top + 34); // 34 = transparent magnification band above the bar
        }

        private void AppSettings_Click(object sender, RoutedEventArgs e)
        {
            try
            {
                if (Application.Current.MainWindow is MainWindow mw) mw.OpenSettingsWindow();
                else DockManager.LaunchApp("ms-settings:");
            }
            catch { DockManager.LaunchApp("ms-settings:"); }
        }

        #endregion

        #region Game Avoidance (Stay Behind Game)

        private void GameAvoidTimer_Tick(object? sender, EventArgs e)
        {
            try
            {
                bool isGame = IsGameActive();

                if (isGame != _isBehindGame)
                {
                    _isBehindGame = isGame;
                    SetBehindGameState(isGame);
                }
            }
            catch { }
        }

        private bool IsGameActive()
        {
            try
            {
                if (GameModeManager.Instance.IsGameRunning)
                {
                    return true;
                }

                if (NativeMethods.SHQueryUserNotificationState(out var state) == 0)
                {
                    if (state == NativeMethods.QUERY_USER_NOTIFICATION_STATE.QUNS_RUNNING_D_3D_FULL_SCREEN_EXCLUSIVE ||
                        state == NativeMethods.QUERY_USER_NOTIFICATION_STATE.QUNS_PRESENTATION_MODE ||
                        state == NativeMethods.QUERY_USER_NOTIFICATION_STATE.QUNS_BUSY)
                    {
                        return true;
                    }
                }

                IntPtr fg = NativeMethods.GetForegroundWindow();
                if (fg == IntPtr.Zero) return false;

                var helper = new System.Windows.Interop.WindowInteropHelper(this);
                if (fg == helper.Handle) return false;

                var className = new System.Text.StringBuilder(256);
                NativeMethods.GetClassName(fg, className, className.Capacity);
                string cls = className.ToString();
                if (cls == "Progman" || cls == "WorkerW" || cls == "Shell_TrayWnd" || cls == "Shell_SecondaryTrayWnd")
                {
                    return false;
                }

                NativeMethods.GetWindowThreadProcessId(fg, out uint fgPid);
                if (fgPid > 0 && fgPid != (uint)Environment.ProcessId)
                {
                    using var fgProc = Process.GetProcessById((int)fgPid);
                    string pName = fgProc.ProcessName.ToLowerInvariant();
                    string pTitle = fgProc.MainWindowTitle.ToLowerInvariant();
                    if (GameModeManager.IsKnownGame(pName, pTitle))
                    {
                        return true;
                    }
                }
            }
            catch { }

            return false;
        }

        public void SetBehindGameState(bool behind)
        {
            Dispatcher.Invoke(() =>
            {
                var helper = new System.Windows.Interop.WindowInteropHelper(this);
                IntPtr hwnd = helper.Handle;

                if (behind)
                {
                    if (this.Topmost) this.Topmost = false;

                    if (hwnd != IntPtr.Zero)
                    {
                        NativeMethods.SetWindowPos(hwnd, NativeMethods.HWND_BOTTOM, 0, 0, 0, 0,
                            NativeMethods.SWP_NOMOVE | NativeMethods.SWP_NOSIZE | NativeMethods.SWP_NOACTIVATE);
                    }

                    this.IsHitTestVisible = false;
                    this.Visibility = Visibility.Collapsed;
                }
                else
                {
                    // Only come back from behind a game if the dock was summoned; otherwise it stays hidden
                    if (!DockManager.Instance.DockWanted) return;
                    this.Visibility = Visibility.Visible;
                    this.IsHitTestVisible = true;
                    this.Topmost = true;

                    if (hwnd != IntPtr.Zero)
                    {
                        NativeMethods.SetWindowPos(hwnd, NativeMethods.HWND_TOPMOST, 0, 0, 0, 0,
                            NativeMethods.SWP_NOMOVE | NativeMethods.SWP_NOSIZE | NativeMethods.SWP_NOACTIVATE);
                    }
                }
            });
        }

        #endregion
    }
}
