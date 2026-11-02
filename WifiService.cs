using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Text.RegularExpressions;

namespace DynamicIsland
{
    public class WifiNetworkItem
    {
        public string Ssid { get; set; } = "";
        public int SignalStrength { get; set; }
        public bool IsConnected { get; set; }
        public bool HasProfile { get; set; }
    }

    public static class WifiService
    {
        public static (bool isConnected, string connectedSsid) GetCurrentConnection()
        {
            try
            {
                var psi = new ProcessStartInfo("netsh", "wlan show interfaces")
                {
                    RedirectStandardOutput = true,
                    UseShellExecute = false,
                    CreateNoWindow = true
                };
                using var proc = Process.Start(psi);
                if (proc == null) return (false, "");
                string output = proc.StandardOutput.ReadToEnd();
                proc.WaitForExit();

                bool connected = false;
                string ssid = "";

                foreach (var line in output.Split('\n'))
                {
                    var trimmed = line.Trim();
                    if (trimmed.StartsWith("State", StringComparison.OrdinalIgnoreCase))
                    {
                        if (trimmed.Contains("connected", StringComparison.OrdinalIgnoreCase) &&
                            !trimmed.Contains("disconnected", StringComparison.OrdinalIgnoreCase))
                        {
                            connected = true;
                        }
                    }
                    else if (trimmed.StartsWith("SSID", StringComparison.OrdinalIgnoreCase) && !trimmed.StartsWith("SSID name", StringComparison.OrdinalIgnoreCase) && !trimmed.StartsWith("SSID 1", StringComparison.OrdinalIgnoreCase))
                    {
                        var parts = trimmed.Split(new[] { ':' }, 2);
                        if (parts.Length == 2)
                        {
                            ssid = parts[1].Trim();
                        }
                    }
                }

                return (connected, ssid);
            }
            catch
            {
                return (false, "");
            }
        }

        public static List<WifiNetworkItem> GetVisibleNetworks()
        {
            var list = new List<WifiNetworkItem>();
            try
            {
                var (isConnected, connectedSsid) = GetCurrentConnection();

                var psi = new ProcessStartInfo("netsh", "wlan show networks mode=bssid")
                {
                    RedirectStandardOutput = true,
                    UseShellExecute = false,
                    CreateNoWindow = true
                };
                using var proc = Process.Start(psi);
                if (proc == null) return list;
                string output = proc.StandardOutput.ReadToEnd();
                proc.WaitForExit();

                string currentSsid = "";
                int currentSignal = 0;

                foreach (var rawLine in output.Split('\n'))
                {
                    var line = rawLine.Trim();
                    if (line.StartsWith("SSID ", StringComparison.OrdinalIgnoreCase) && line.Contains(":"))
                    {
                        if (!string.IsNullOrEmpty(currentSsid))
                        {
                            AddOrUpdateNetwork(list, currentSsid, currentSignal, isConnected && currentSsid == connectedSsid);
                        }
                        var parts = line.Split(new[] { ':' }, 2);
                        currentSsid = parts.Length == 2 ? parts[1].Trim() : "";
                        currentSignal = 0;
                    }
                    else if (line.StartsWith("Signal", StringComparison.OrdinalIgnoreCase) && line.Contains(":"))
                    {
                        var match = Regex.Match(line, @"(\d+)%");
                        if (match.Success && int.TryParse(match.Groups[1].Value, out int sig))
                        {
                            if (sig > currentSignal) currentSignal = sig;
                        }
                    }
                }

                if (!string.IsNullOrEmpty(currentSsid))
                {
                    AddOrUpdateNetwork(list, currentSsid, currentSignal, isConnected && currentSsid == connectedSsid);
                }
            }
            catch { }

            return list;
        }

        private static void AddOrUpdateNetwork(List<WifiNetworkItem> list, string ssid, int signal, bool isConnected)
        {
            if (string.IsNullOrWhiteSpace(ssid)) return;
            var existing = list.Find(x => x.Ssid.Equals(ssid, StringComparison.OrdinalIgnoreCase));
            if (existing == null)
            {
                list.Add(new WifiNetworkItem
                {
                    Ssid = ssid,
                    SignalStrength = signal,
                    IsConnected = isConnected
                });
            }
            else
            {
                if (signal > existing.SignalStrength) existing.SignalStrength = signal;
                if (isConnected) existing.IsConnected = true;
            }
        }

        public static void ConnectToProfile(string ssid)
        {
            try
            {
                var psi = new ProcessStartInfo("netsh", $"wlan connect name=\"{ssid}\"")
                {
                    UseShellExecute = false,
                    CreateNoWindow = true
                };
                Process.Start(psi);
            }
            catch { }
        }

        public static void Disconnect()
        {
            try
            {
                var psi = new ProcessStartInfo("netsh", "wlan disconnect")
                {
                    UseShellExecute = false,
                    CreateNoWindow = true
                };
                Process.Start(psi);
            }
            catch { }
        }

        public static void OpenWindowsWifiFlyout()
        {
            try
            {
                Process.Start(new ProcessStartInfo("ms-availablenetworks:") { UseShellExecute = true });
            }
            catch
            {
                try
                {
                    Process.Start(new ProcessStartInfo("ms-settings:network-wifi") { UseShellExecute = true });
                }
                catch { }
            }
        }
    }
}
