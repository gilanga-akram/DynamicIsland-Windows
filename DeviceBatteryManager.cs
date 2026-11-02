// Language: C#
// Filename: DeviceBatteryManager.cs
// Runs on: Windows 10 / Windows 11 (.NET 9.0-windows10.0.19041.0)
// Targets: Headset & Mouse battery and connection status monitoring

using System;
using System.ComponentModel;
using System.Linq;
using System.Runtime.CompilerServices;
using System.Threading.Tasks;
using System.Windows.Threading;
using Windows.Devices.Enumeration;

namespace DynamicIsland
{
    public class DeviceBatteryInfo : INotifyPropertyChanged
    {
        private string _name = "Device";
        private int _batteryPercent = 85;
        private bool _isConnected = true;
        private bool _isCharging = false;
        private string _deviceType = "Headset"; // "Headset" or "Mouse"

        public string Name
        {
            get => _name;
            set { _name = value; OnPropertyChanged(); }
        }

        public int BatteryPercent
        {
            get => _batteryPercent;
            set 
            { 
                _batteryPercent = Math.Clamp(value, 0, 100); 
                OnPropertyChanged();
                OnPropertyChanged(nameof(BatteryAngle));
            }
        }

        public double BatteryAngle => (_batteryPercent / 100.0) * 360.0;

        public bool IsConnected
        {
            get => _isConnected;
            set { _isConnected = value; OnPropertyChanged(); }
        }

        public bool IsCharging
        {
            get => _isCharging;
            set { _isCharging = value; OnPropertyChanged(); }
        }

        public string DeviceType
        {
            get => _deviceType;
            set { _deviceType = value; OnPropertyChanged(); }
        }

        public event PropertyChangedEventHandler? PropertyChanged;

        protected void OnPropertyChanged([CallerMemberName] string? propName = null)
        {
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propName));
        }
    }

    public class DeviceBatteryManager : INotifyPropertyChanged
    {
        private readonly DispatcherTimer _pollTimer;
        private readonly Dispatcher _dispatcher;

        public DeviceBatteryInfo Headset { get; } = new()
        {
            Name = "Razer Barracuda X",
            DeviceType = "Headset",
            BatteryPercent = 85,
            IsConnected = true
        };

        public DeviceBatteryInfo Mouse { get; } = new()
        {
            Name = "Wireless Mouse",
            DeviceType = "Mouse",
            BatteryPercent = 90,
            IsConnected = true
        };

        private DeviceBatteryInfo _activeDisplayDevice;

        public DeviceBatteryInfo ActiveDisplayDevice
        {
            get => _activeDisplayDevice;
            set { _activeDisplayDevice = value; OnPropertyChanged(); }
        }

        public event PropertyChangedEventHandler? PropertyChanged;

        public DeviceBatteryManager(Dispatcher dispatcher)
        {
            _dispatcher = dispatcher;
            _activeDisplayDevice = Headset;

            // Initial detection
            Task.Run(ScanConnectedDevicesAsync);

            // Poll every 5 seconds for battery updates
            _pollTimer = new DispatcherTimer
            {
                Interval = TimeSpan.FromSeconds(5)
            };
            _pollTimer.Tick += async (s, e) => await ScanConnectedDevicesAsync();
            _pollTimer.Start();
        }

        public void ToggleActiveDevice()
        {
            ActiveDisplayDevice = (ActiveDisplayDevice == Headset) ? Mouse : Headset;
        }

        public async Task ScanConnectedDevicesAsync()
        {
            try
            {
                // Query Windows DeviceInformation for battery properties
                string[] requestedProperties = new[]
                {
                    "System.Devices.BatteryPercent",
                    "System.ItemNameDisplay",
                    "System.Devices.Aep.IsConnected"
                };

                // Scan all devices
                var devices = await DeviceInformation.FindAllAsync(string.Empty, requestedProperties);

                bool foundHeadset = false;
                bool foundMouse = false;

                foreach (var dev in devices)
                {
                    string name = dev.Name ?? "";

                    // Headset detection (Razer Barracuda, Bluetooth headsets, AirPods)
                    if (!foundHeadset && (name.Contains("Barracuda", StringComparison.OrdinalIgnoreCase) ||
                                          name.Contains("Razer", StringComparison.OrdinalIgnoreCase) ||
                                          name.Contains("Headset", StringComparison.OrdinalIgnoreCase) ||
                                          name.Contains("Headphone", StringComparison.OrdinalIgnoreCase) ||
                                          name.Contains("AirPods", StringComparison.OrdinalIgnoreCase)))
                    {
                        foundHeadset = true;
                        int? battery = ExtractBatteryPercent(dev);

                        _dispatcher.Invoke(() =>
                        {
                            Headset.Name = name.Length > 22 ? name.Substring(0, 22) : name;
                            Headset.IsConnected = true;
                            if (battery.HasValue && battery.Value > 0)
                            {
                                Headset.BatteryPercent = battery.Value;
                            }
                        });
                    }

                    // Mouse detection (HID mouse, wireless mouse, gaming mouse)
                    if (!foundMouse && (name.Contains("Mouse", StringComparison.OrdinalIgnoreCase) ||
                                        name.Contains("G304", StringComparison.OrdinalIgnoreCase) ||
                                        name.Contains("G305", StringComparison.OrdinalIgnoreCase) ||
                                        name.Contains("Viper", StringComparison.OrdinalIgnoreCase) ||
                                        name.Contains("DeathAdder", StringComparison.OrdinalIgnoreCase)))
                    {
                        foundMouse = true;
                        int? battery = ExtractBatteryPercent(dev);

                        _dispatcher.Invoke(() =>
                        {
                            Mouse.Name = name.Length > 20 ? name.Substring(0, 20) : name;
                            Mouse.IsConnected = true;
                            if (battery.HasValue && battery.Value > 0)
                            {
                                Mouse.BatteryPercent = battery.Value;
                            }
                        });
                    }
                }
            }
            catch
            {
                // Keep active defaults if PnP scan is busy
            }
        }

        private static int? ExtractBatteryPercent(DeviceInformation dev)
        {
            if (dev.Properties.TryGetValue("System.Devices.BatteryPercent", out var val) && val != null)
            {
                if (val is byte b) return b;
                if (val is int i) return i;
                if (int.TryParse(val.ToString(), out int parsed)) return parsed;
            }
            return null;
        }

        protected void OnPropertyChanged([CallerMemberName] string? propName = null)
        {
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propName));
        }
    }
}
