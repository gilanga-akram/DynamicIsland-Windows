// Screen brightness: laptop panels through WMI, external monitors through DDC/CI (dxva2).

using System;
using System.Collections.Generic;
using System.Management;
using System.Runtime.InteropServices;
using System.Threading.Tasks;

namespace DynamicIsland
{
    public static class BrightnessService
    {
        /// <summary>Current brightness 0-100, or null when no display supports it.</summary>
        public static int? Get()
        {
            try
            {
                int? wmi = GetWmi();
                if (wmi.HasValue) return wmi;
            }
            catch { } // WMI unavailable
            try { return GetDdc(); } catch { return null; }
        }

        /// <summary>Applies brightness on a background thread (DDC/CI calls are slow).</summary>
        public static Task<bool> SetAsync(int percent)
        {
            percent = Math.Clamp(percent, 0, 100);
            return Task.Run(() =>
            {
                bool ok = false;
                try { ok = SetWmi(percent); } catch { }
                try { ok |= SetDdc(percent); } catch { }
                return ok;
            });
        }

        // ---------- WMI (internal laptop panels) ----------
        private static int? GetWmi()
        {
            try
            {
                using var searcher = new ManagementObjectSearcher(@"root\WMI", "SELECT CurrentBrightness FROM WmiMonitorBrightness");
                foreach (ManagementObject o in searcher.Get())
                    return Convert.ToInt32(o["CurrentBrightness"]);
            }
            catch { }
            return null;
        }

        private static bool SetWmi(int percent)
        {
            try
            {
                bool any = false;
                using var searcher = new ManagementObjectSearcher(@"root\WMI", "SELECT * FROM WmiMonitorBrightnessMethods");
                foreach (ManagementObject o in searcher.Get())
                {
                    o.InvokeMethod("WmiSetBrightness", new object[] { (uint)1, (byte)percent });
                    any = true;
                }
                return any;
            }
            catch { return false; }
        }

        // ---------- DDC/CI (external monitors) ----------
        [StructLayout(LayoutKind.Sequential, CharSet = CharSet.Unicode)]
        private struct PHYSICAL_MONITOR
        {
            public IntPtr hPhysicalMonitor;
            [MarshalAs(UnmanagedType.ByValTStr, SizeConst = 128)] public string szPhysicalMonitorDescription;
        }

        private delegate bool MonitorEnumProc(IntPtr hMonitor, IntPtr hdc, IntPtr rect, IntPtr data);
        [DllImport("user32.dll")] private static extern bool EnumDisplayMonitors(IntPtr hdc, IntPtr clip, MonitorEnumProc proc, IntPtr data);
        [DllImport("dxva2.dll")] private static extern bool GetNumberOfPhysicalMonitorsFromHMONITOR(IntPtr hMonitor, out uint count);
        [DllImport("dxva2.dll")] private static extern bool GetPhysicalMonitorsFromHMONITOR(IntPtr hMonitor, uint size, [Out] PHYSICAL_MONITOR[] arr);
        [DllImport("dxva2.dll")] private static extern bool DestroyPhysicalMonitors(uint size, PHYSICAL_MONITOR[] arr);
        [DllImport("dxva2.dll")] private static extern bool GetMonitorBrightness(IntPtr h, out uint min, out uint cur, out uint max);
        [DllImport("dxva2.dll")] private static extern bool SetMonitorBrightness(IntPtr h, uint value);

        private static void ForEachPhysicalMonitor(Action<IntPtr> action)
        {
            var handles = new List<IntPtr>();
            EnumDisplayMonitors(IntPtr.Zero, IntPtr.Zero, (h, _, _, _) => { handles.Add(h); return true; }, IntPtr.Zero);
            foreach (var hMon in handles)
            {
                try
                {
                    if (!GetNumberOfPhysicalMonitorsFromHMONITOR(hMon, out uint n) || n == 0) continue;
                    var arr = new PHYSICAL_MONITOR[n];
                    if (!GetPhysicalMonitorsFromHMONITOR(hMon, n, arr)) continue;
                    try { foreach (var pm in arr) action(pm.hPhysicalMonitor); }
                    finally { DestroyPhysicalMonitors(n, arr); }
                }
                catch { }
            }
        }

        private static int? GetDdc()
        {
            int? result = null;
            try
            {
                ForEachPhysicalMonitor(h =>
                {
                    if (result == null && GetMonitorBrightness(h, out uint min, out uint cur, out uint max) && max > min)
                        result = (int)Math.Round((cur - min) * 100.0 / (max - min));
                });
            }
            catch { }
            return result;
        }

        private static bool SetDdc(int percent)
        {
            bool any = false;
            try
            {
                ForEachPhysicalMonitor(h =>
                {
                    if (GetMonitorBrightness(h, out uint min, out _, out uint max) && max > min)
                    {
                        uint v = (uint)Math.Round(min + (max - min) * percent / 100.0);
                        if (SetMonitorBrightness(h, v)) any = true;
                    }
                });
            }
            catch { }
            return any;
        }
    }
}
