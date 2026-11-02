// Language: C#
// Filename: BluetoothService.cs
// Runs on: Windows 10 / Windows 11 (.NET 9.0-windows10.0.19041.0)
// Targets: Native WinRT / Radio API Bluetooth detection & silent toggle without PowerShell popups

using System;
using System.Diagnostics;
using System.Linq;
using System.Threading.Tasks;
using Windows.Devices.Radios;

namespace DynamicIsland
{
    public static class BluetoothService
    {
        private static Radio? _bluetoothRadio;
        private static bool _initialized = false;

        public static async Task<Radio?> GetBluetoothRadioAsync()
        {
            if (_bluetoothRadio != null) return _bluetoothRadio;

            try
            {
                var radios = await Radio.GetRadiosAsync();
                _bluetoothRadio = radios.FirstOrDefault(r => r.Kind == RadioKind.Bluetooth);
                _initialized = true;
                return _bluetoothRadio;
            }
            catch
            {
                return null;
            }
        }

        public static async Task<bool> IsBluetoothOnAsync()
        {
            try
            {
                var radio = await GetBluetoothRadioAsync();
                if (radio != null)
                {
                    return radio.State == RadioState.On;
                }
            }
            catch { }

            return false;
        }

        public static async Task<bool> SetBluetoothStateAsync(bool enable)
        {
            try
            {
                var radio = await GetBluetoothRadioAsync();
                if (radio != null)
                {
                    var targetState = enable ? RadioState.On : RadioState.Off;
                    var result = await radio.SetStateAsync(targetState);
                    return result == RadioAccessStatus.Allowed;
                }
            }
            catch { }

            return false;
        }

        public static void OpenWindowsBluetoothSettings()
        {
            try
            {
                Process.Start(new ProcessStartInfo("ms-settings:bluetooth") { UseShellExecute = true });
            }
            catch { }
        }
    }
}
