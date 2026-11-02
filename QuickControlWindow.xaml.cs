using System;
using System.Diagnostics;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Shapes;
using System.Windows.Threading;

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
            Height = 420;
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
                BdWifiTile.Background = new SolidColorBrush(Color.FromRgb(255, 255, 255));
                TxtWifiHeader.Foreground = new SolidColorBrush(Color.FromRgb(17, 17, 17));
                TxtWifiStatus.Foreground = new SolidColorBrush(Color.FromRgb(70, 70, 70));
                IconWifi.Fill = new SolidColorBrush(Color.FromRgb(17, 17, 17));
                TxtWifiArrow.Foreground = new SolidColorBrush(Color.FromRgb(85, 85, 85));
            }
            else
            {
                TxtWifiStatus.Text = "Tidak terhubung";
                BdWifiTile.Background = new SolidColorBrush(Color.FromArgb(0x40, 0xFF, 0xFF, 0xFF));
                TxtWifiHeader.Foreground = new SolidColorBrush(Color.FromRgb(255, 255, 255));
                TxtWifiStatus.Foreground = new SolidColorBrush(Color.FromRgb(180, 190, 205));
                IconWifi.Fill = new SolidColorBrush(Color.FromRgb(255, 255, 255));
                TxtWifiArrow.Foreground = new SolidColorBrush(Color.FromRgb(180, 190, 205));
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

        private void UpdateBluetoothVisual()
        {
            if (BdBtTile == null) return;
            if (_isBluetoothOn)
            {
                BdBtTile.Background = new SolidColorBrush(Color.FromRgb(255, 255, 255));
                if (TxtBtHeader != null) TxtBtHeader.Foreground = new SolidColorBrush(Color.FromRgb(17, 17, 17));
                if (TxtBtStatus != null) TxtBtStatus.Foreground = new SolidColorBrush(Color.FromRgb(70, 70, 70));
                if (IconBt != null) IconBt.Fill = new SolidColorBrush(Color.FromRgb(17, 17, 17));
                if (TxtBtArrow != null) TxtBtArrow.Foreground = new SolidColorBrush(Color.FromRgb(85, 85, 85));
                if (TxtBtStatus != null) TxtBtStatus.Text = "Nyala";
            }
            else
            {
                BdBtTile.Background = new SolidColorBrush(Color.FromArgb(0x40, 0xFF, 0xFF, 0xFF));
                if (TxtBtHeader != null) TxtBtHeader.Foreground = new SolidColorBrush(Color.FromRgb(255, 255, 255));
                if (TxtBtStatus != null) TxtBtStatus.Foreground = new SolidColorBrush(Color.FromRgb(180, 190, 205));
                if (IconBt != null) IconBt.Fill = new SolidColorBrush(Color.FromRgb(255, 255, 255));
                if (TxtBtArrow != null) TxtBtArrow.Foreground = new SolidColorBrush(Color.FromRgb(180, 190, 205));
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
                        ? new SolidColorBrush(Color.FromArgb(0x60, 0x34, 0xC7, 0x59))
                        : new SolidColorBrush(Color.FromArgb(0x25, 0xFF, 0xFF, 0xFF)),
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
                        Foreground = new SolidColorBrush(Color.FromRgb(52, 199, 89)),
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
            _isDndActive = !_isDndActive;
            if (_isDndActive)
            {
                BdDndTile.Background = new SolidColorBrush(Color.FromArgb(0x80, 0xAF, 0x52, 0xDE)); // purple glow
                TxtDndStatus.Text = "Aktif";
            }
            else
            {
                BdDndTile.Background = new SolidColorBrush(Color.FromArgb(0x28, 0xFF, 0xFF, 0xFF));
                TxtDndStatus.Text = "Mati";
            }
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

        private void SliderBrightness_ValueChanged(object sender, RoutedPropertyChangedEventArgs<double> e)
        {
            int val = (int)Math.Round(e.NewValue);
            TxtBrightnessVal.Text = val.ToString();
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
