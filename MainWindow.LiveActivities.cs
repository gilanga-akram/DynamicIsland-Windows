// Live Activities: volume/brightness HUD, charging & low-battery HUD,
// Bluetooth device connect banner and "Now Playing" banner when the track changes.
// Each one follows its toggle in Settings > Live Activities.

using System;
using System.ComponentModel;
using System.Windows.Media;

namespace DynamicIsland
{
    public partial class MainWindow
    {
        private bool _liveInit;
        private bool _lastCharging;
        private int _lastBatteryPct = -1;
        private bool _headsetWasConnected;
        private bool _mouseWasConnected;
        private string _lastTrackKey = "";

        private static readonly Brush HudGreen = new SolidColorBrush(Color.FromRgb(0x30, 0xD1, 0x58));
        private static readonly Brush HudBlue = new SolidColorBrush(Color.FromRgb(0x0A, 0x84, 0xFF));
        private static readonly Brush HudRed = new SolidColorBrush(Color.FromRgb(0xFF, 0x45, 0x3A));

        private void InitLiveActivities()
        {
            if (_liveInit) return;
            _liveInit = true;

            // Volume HUD (keyboard keys, Windows flyout, other apps)
            Audio.MasterChangedExternally += (vol, muted) =>
            {
                if (Settings?.ShowVolumeHUD == false) return;
                string glyph = muted || vol == 0 ? "" : vol < 34 ? "" : vol < 67 ? "" : "";
                ShowHud(glyph, muted ? 0 : vol, HudBlue);
            };

            // Charging / low battery
            _lastCharging = Metrics.IsCharging;
            _lastBatteryPct = Metrics.BatteryPercentage;
            Metrics.PropertyChanged += Metrics_LiveActivity;

            // Bluetooth accessories
            _headsetWasConnected = Battery.Headset.IsConnected;
            _mouseWasConnected = Battery.Mouse.IsConnected;
            Battery.Headset.PropertyChanged += (s, e) => DeviceConnectionChanged(Battery.Headset, e, ref _headsetWasConnected);
            Battery.Mouse.PropertyChanged += (s, e) => DeviceConnectionChanged(Battery.Mouse, e, ref _mouseWasConnected);

            // Now Playing banner
            _lastTrackKey = $"{Media.SongTitle}|{Media.ArtistName}";
            Media.PropertyChanged += Media_LiveActivity;
        }

        private void Metrics_LiveActivity(object? sender, PropertyChangedEventArgs e)
        {
            if (!Metrics.HasBattery || Settings?.ShowBatteryHUD == false) return;

            if (e.PropertyName == nameof(SystemMetrics.IsCharging) && Metrics.IsCharging != _lastCharging)
            {
                _lastCharging = Metrics.IsCharging;
                Dispatcher.InvokeAsync(() =>
                {
                    ShowHud(_lastCharging ? "" : "", Metrics.BatteryPercentage, _lastCharging ? HudGreen : Brushes.White);
                    SoundManager.Play(AtollSound.Feedback);
                });
            }
            else if (e.PropertyName == nameof(SystemMetrics.BatteryPercentage))
            {
                int pct = Metrics.BatteryPercentage;
                int prev = _lastBatteryPct;
                _lastBatteryPct = pct;
                if (prev < 0 || Metrics.IsCharging) return;
                foreach (int threshold in new[] { 20, 10, 5 })
                {
                    if (prev > threshold && pct <= threshold)
                    {
                        Dispatcher.InvokeAsync(() =>
                        {
                            ShowHud("", pct, HudRed);
                            Notifications?.ShowNotification("Baterai Lemah", $"Sisa baterai {pct}%. Sambungkan pengisi daya.", "Baterai", "#FF453A");
                        });
                        break;
                    }
                }
            }
        }

        private void DeviceConnectionChanged(DeviceBatteryInfo dev, PropertyChangedEventArgs e, ref bool wasConnected)
        {
            if (e.PropertyName != nameof(DeviceBatteryInfo.IsConnected)) return;
            bool now = dev.IsConnected;
            if (now == wasConnected) return;
            wasConnected = now;
            if (Settings?.ShowBluetoothHUD == false) return;

            string name = dev.Name;
            int pct = dev.BatteryPercent;
            Dispatcher.InvokeAsync(() =>
            {
                if (now)
                {
                    ShowHud("", pct, HudBlue);
                    Notifications?.ShowNotification(name, pct > 0 ? $"Terhubung · baterai {pct}%" : "Terhubung", "Bluetooth", "#0A84FF");
                }
                else
                {
                    Notifications?.ShowNotification(name, "Terputus", "Bluetooth", "#8E8E93");
                }
            });
        }

        private void Media_LiveActivity(object? sender, PropertyChangedEventArgs e)
        {
            if (e.PropertyName != nameof(MediaManager.SongTitle) && e.PropertyName != nameof(MediaManager.ArtistName)) return;
            string title = Media.SongTitle ?? "";
            string artist = Media.ArtistName ?? "";
            string key = $"{title}|{artist}";
            if (key == _lastTrackKey || string.IsNullOrWhiteSpace(title)) return;
            _lastTrackKey = key;

            if (Settings?.ShowMediaHUD == false || _isExpanded || !Media.IsPlaying) return;
            Dispatcher.InvokeAsync(() =>
                Notifications?.TriggerNotification(NotificationAppType.General, "Sedang Diputar", title,
                    string.IsNullOrWhiteSpace(artist) ? "Lagu baru" : artist, "#FF2D55", "", "Now Playing"));
        }
    }
}
