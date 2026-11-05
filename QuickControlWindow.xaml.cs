using System;
using System.Diagnostics;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Shapes;
using System.Windows.Threading;
using System.Threading.Tasks;

namespace DynamicIsland
{
    public partial class QuickControlWindow : Window
    {
        private DispatcherTimer _pollTimer;
        private bool _isUpdatingSliders = false;
        private bool _isDndActive = false;

        public QuickControlWindow()
        {
            InitializeComponent();
            Deactivated += (s, e) => Hide();

            _pollTimer = new DispatcherTimer
            {
                Interval = TimeSpan.FromSeconds(3)
            };
            _pollTimer.Tick += (s, e) => RefreshState();
            _pollTimer.Start();

            Loaded += (s, e) => RefreshState();
        }

        public void ShowAt(double rightX, double bottomY)
        {
            RefreshState();
            PanelMainView.Visibility = Visibility.Visible;
            PanelWifiView.Visibility = Visibility.Collapsed;

            Width = 360;
            Height = 440;
            Left = rightX - Width;
            Top = bottomY - Height - 10;

            Show();
            Activate();
        }

        private void RefreshState()
        {
            if (!IsVisible) return;

            // 1. Wifi status
            var (isConnected, connectedSsid) = WifiService.GetCurrentConnection();
            if (isConnected && !string.IsNullOrEmpty(connectedSsid))
            {
                TxtWifiStatus.Text = connectedSsid;
                BdWifiTile.Background = CcOnBrush;
            }
            else
            {
                TxtWifiStatus.Text = "Tidak terhubung";
                BdWifiTile.Background = CcOffBrush;
            }

            // 2. Battery status
            if (Application.Current.MainWindow is MainWindow mw && mw.Metrics != null)
            {
                int pct = mw.Metrics.BatteryPercentage;
                TxtBatteryPercent.Text = $"{pct}%";
                TxtBatteryState.Text = mw.Metrics.IsCharging ? "  mengisi daya" : "  tersisa";
                BdPowerBar.Width = Math.Max(2, (18.0 * pct) / 100.0);
            }

            // 3. Volume
            if (!_isUpdatingSliders)
            {
                _isUpdatingSliders = true;
                if (Application.Current.MainWindow is MainWindow mwMain && mwMain.Audio != null)
                {
                    int currentVol = mwMain.Audio.MasterOutputVolume;
                    SliderVolume.Value = currentVol;
                    TxtVolumeVal.Text = currentVol.ToString();
                }
                _isUpdatingSliders = false;
            }

            // 4. Bluetooth Real State
            _ = UpdateBluetoothUIAsync();
        }

        private async Task UpdateBluetoothUIAsync()
        {
            try
            {
                _isBluetoothOn = await BluetoothService.IsBluetoothOnAsync();
                UpdateBluetoothVisual();
            }
            catch { }
        }

        // macOS Control Center icon circles: blue when on, gray when off
        private static readonly SolidColorBrush CcOnBrush = new SolidColorBrush(Color.FromRgb(0x0A, 0x84, 0xFF));
        private static readonly SolidColorBrush CcOffBrush = new SolidColorBrush(Color.FromRgb(0x48, 0x48, 0x4A));

        private void UpdateBluetoothVisual()
        {
            if (BdBtTile == null) return;
            if (_isBluetoothOn)
            {
                BdBtTile.Background = CcOnBrush;
                if (TxtBtStatus != null) TxtBtStatus.Text = "Nyala";
            }
            else
            {
                BdBtTile.Background = CcOffBrush;
                if (TxtBtStatus != null) TxtBtStatus.Text = "Mati";
            }
        }

        private void BtnWifiTile_Click(object sender, RoutedEventArgs e)
        {
            PanelMainView.Visibility = Visibility.Collapsed;
            PanelWifiView.Visibility = Visibility.Visible;
            PopulateWifiList();
        }

        private void BtnBackToMain_Click(object sender, RoutedEventArgs e)
        {
            PanelWifiView.Visibility = Visibility.Collapsed;
            PanelMainView.Visibility = Visibility.Visible;
            RefreshState();
        }

        private void BtnRefreshWifi_Click(object sender, RoutedEventArgs e)
        {
            PopulateWifiList();
        }

        private void PopulateWifiList()
        {
            PanelWifiList.Children.Clear();
            var networks = WifiService.GetVisibleNetworks();

            if (networks.Count == 0)
            {
                var tbEmpty = new TextBlock
                {
                    Text = "Tidak ada sinyal Wi-Fi ditemukan",
                    Foreground = new SolidColorBrush(Color.FromRgb(180, 190, 210)),
                    FontSize = 12,
                    HorizontalAlignment = HorizontalAlignment.Center,
                    Margin = new Thickness(0, 30, 0, 0)
                };
                PanelWifiList.Children.Add(tbEmpty);
                return;
            }

            foreach (var net in networks)
            {
                var btn = new Button
                {
                    Background = Brushes.Transparent,
                    BorderThickness = new Thickness(0),
                    Cursor = Cursors.Hand,
                    Margin = new Thickness(0, 3, 0, 3)
                };

                var border = new Border
                {
                    Background = net.IsConnected
                        ? new SolidColorBrush(Color.FromRgb(0x0A, 0x84, 0xFF))
                        : new SolidColorBrush(Color.FromArgb(0x26, 0xFF, 0xFF, 0xFF)),
                    CornerRadius = new CornerRadius(10),
                    Padding = new Thickness(12, 8, 12, 8)
                };

                var grid = new Grid();
                grid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(24) });
                grid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
                grid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(45) });

                // Wifi Icon
                var path = new System.Windows.Shapes.Path
                {
                    Stretch = Stretch.Uniform,
                    Width = 14,
                    Height = 12,
                    Fill = Brushes.White,
                    VerticalAlignment = VerticalAlignment.Center,
                    Data = Geometry.Parse("M12,4C7.31,4,3.07,5.9,0,8.98L12,21l12-12.02C20.93,5.9,16.69,4,12,4z")
                };
                Grid.SetColumn(path, 0);
                grid.Children.Add(path);

                // SSID
                var sp = new StackPanel { VerticalAlignment = VerticalAlignment.Center };
                var tbName = new TextBlock
                {
                    Text = net.Ssid,
                    Foreground = Brushes.White,
                    FontWeight = FontWeights.SemiBold,
                    FontSize = 12,
                    TextTrimming = TextTrimming.CharacterEllipsis
                };
                sp.Children.Add(tbName);

                if (net.IsConnected)
                {
                    var tbConn = new TextBlock
                    {
                        Text = "Terhubung",
                        Foreground = new SolidColorBrush(Color.FromArgb(0xCC, 0xFF, 0xFF, 0xFF)),
                        FontSize = 9.5
                    };
                    sp.Children.Add(tbConn);
                }
                Grid.SetColumn(sp, 1);
                grid.Children.Add(sp);

                // Signal %
                var tbSig = new TextBlock
                {
                    Text = $"{net.SignalStrength}%",
                    Foreground = new SolidColorBrush(Color.FromRgb(180, 190, 210)),
                    FontSize = 11,
                    VerticalAlignment = VerticalAlignment.Center,
                    HorizontalAlignment = HorizontalAlignment.Right
                };
                Grid.SetColumn(tbSig, 2);
                grid.Children.Add(tbSig);

                border.Child = grid;
                btn.Content = border;

                string targetSsid = net.Ssid;
                btn.Click += (s, e) =>
                {
                    WifiService.ConnectToProfile(targetSsid);
                    // Also trigger flyout in case password is required
                    WifiService.OpenWindowsWifiFlyout();
                };

                PanelWifiList.Children.Add(btn);
            }
        }

        private void BtnOpenFullWifiSettings_Click(object sender, RoutedEventArgs e)
        {
            WifiService.OpenWindowsWifiFlyout();
        }

        private bool _isBluetoothOn = false;

        private async void BtnBluetoothTile_Click(object sender, RoutedEventArgs e)
        {
            _isBluetoothOn = !_isBluetoothOn;
            UpdateBluetoothVisual();

            bool success = await BluetoothService.SetBluetoothStateAsync(_isBluetoothOn);
            if (!success)
            {
                BluetoothService.OpenWindowsBluetoothSettings();
            }

            await Task.Delay(1200);
            _ = UpdateBluetoothUIAsync();
        }

        private void BtnDndTile_Click(object sender, RoutedEventArgs e)
        {
            GameModeManager.Instance.ToggleDnd();
            ApplyDndVisual();
        }

        private void ApplyDndVisual()
        {
            _isDndActive = GameModeManager.Instance.IsDndActive;
            BdDndTile.Background = _isDndActive ? new SolidColorBrush(Color.FromRgb(0x5E, 0x5C, 0xE6)) : CcOffBrush; // macOS Focus indigo
            TxtDndStatus.Text = _isDndActive ? "Aktif" : "Mati";
        }

        private void BtnBatterySaver_Click(object sender, RoutedEventArgs e)
        {
            try
            {
                Process.Start(new ProcessStartInfo("ms-settings:batterysaver") { UseShellExecute = true });
            }
            catch { }
        }

        private void SliderVolume_ValueChanged(object sender, RoutedPropertyChangedEventArgs<double> e)
        {
            if (_isUpdatingSliders) return;
            int vol = (int)Math.Round(e.NewValue);
            TxtVolumeVal.Text = vol.ToString();
            if (Application.Current.MainWindow is MainWindow mw && mw.Audio != null)
            {
                mw.Audio.MasterOutputVolume = vol;
            }
        }

        private DispatcherTimer? _brightnessDebounce;
        private bool _brightnessSupported = true;

        private async void RefreshBrightnessAsync()
        {
            int? cur = await System.Threading.Tasks.Task.Run(BrightnessService.Get);
            _isUpdatingSliders = true;
            if (cur.HasValue)
            {
                _brightnessSupported = true;
                SliderBrightness.IsEnabled = true;
                SliderBrightness.Value = cur.Value;
                TxtBrightnessVal.Text = cur.Value.ToString();
            }
            else
            {
                _brightnessSupported = false;
                SliderBrightness.IsEnabled = false;
                TxtBrightnessVal.Text = "Tidak didukung";
            }
            _isUpdatingSliders = false;
        }

        private void SliderBrightness_ValueChanged(object sender, RoutedPropertyChangedEventArgs<double> e)
        {
            int val = (int)Math.Round(e.NewValue);
            if (TxtBrightnessVal != null && _brightnessSupported) TxtBrightnessVal.Text = val.ToString();
            if (_isUpdatingSliders || !_brightnessSupported) return;

            _brightnessDebounce ??= new DispatcherTimer { Interval = TimeSpan.FromMilliseconds(120) };
            _brightnessDebounce.Stop();
            _brightnessDebounce.Tick -= BrightnessDebounce_Tick;
            _brightnessDebounce.Tick += BrightnessDebounce_Tick;
            _brightnessDebounce.Start();
        }

        private async void BrightnessDebounce_Tick(object? sender, EventArgs e)
        {
            _brightnessDebounce?.Stop();
            int val = (int)Math.Round(SliderBrightness.Value);
            await BrightnessService.SetAsync(val);
            if (Application.Current.MainWindow is MainWindow mw && (mw.Settings?.ShowVolumeHUD ?? true))
                mw.ShowHud("\uE706", val);
        }

        private void BtnOpenSettings_Click(object sender, RoutedEventArgs e)
        {
            try
            {
                Process.Start(new ProcessStartInfo("ms-settings:") { UseShellExecute = true });
            }
            catch { }
        }
    }
}
