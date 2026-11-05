// Brings a freshly launched (or already running) app window to the foreground.
// Needed because the Windows taskbar is hidden while the floating dock is active,
// so a window that opens behind others would otherwise never be seen.

using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Runtime.InteropServices;
using System.Windows;
using System.Windows.Threading;

namespace DynamicIsland
{
    internal static class WindowFocus
    {
        private delegate bool EnumProc(IntPtr h, IntPtr l);
        [DllImport("user32.dll")] private static extern bool EnumWindows(EnumProc cb, IntPtr l);
        [DllImport("user32.dll")] private static extern bool IsWindowVisible(IntPtr h);
        [DllImport("user32.dll")] private static extern bool IsIconic(IntPtr h);
        [DllImport("user32.dll")] private static extern int GetWindowTextLength(IntPtr h);
        [DllImport("user32.dll")] private static extern uint GetWindowThreadProcessId(IntPtr h, out uint pid);
        [DllImport("user32.dll", EntryPoint = "GetWindowLongW")] private static extern int GetWindowLong(IntPtr h, int i);
        [DllImport("user32.dll")] private static extern bool ShowWindow(IntPtr h, int cmd);
        [DllImport("user32.dll")] private static extern bool SetForegroundWindow(IntPtr h);
        [DllImport("user32.dll")] private static extern bool BringWindowToTop(IntPtr h);
        [DllImport("user32.dll")] private static extern IntPtr GetForegroundWindow();
        [DllImport("user32.dll")] private static extern bool AttachThreadInput(uint a, uint b, bool attach);
        [DllImport("user32.dll")] private static extern void keybd_event(byte vk, byte scan, uint flags, UIntPtr extra);
        [DllImport("kernel32.dll")] private static extern uint GetCurrentThreadId();
        [DllImport("dwmapi.dll")] private static extern int DwmGetWindowAttribute(IntPtr h, int attr, out int val, int size);

        /// <summary>All visible, titled, non-cloaked top-level windows of other processes (hwnd → is minimized).</summary>
        private static Dictionary<IntPtr, bool> Snapshot()
        {
            var map = new Dictionary<IntPtr, bool>();
            uint me = (uint)Environment.ProcessId;
            EnumWindows((h, _) =>
            {
                try
                {
                    if (!IsWindowVisible(h) || GetWindowTextLength(h) == 0) return true;
                    if ((GetWindowLong(h, -20) & 0x80) != 0) return true; // tool window
                    GetWindowThreadProcessId(h, out uint pid);
                    if (pid == me) return true;
                    if (DwmGetWindowAttribute(h, 14, out int cloaked, 4) == 0 && cloaked != 0) return true;
                    map[h] = IsIconic(h);
                }
                catch { }
                return true;
            }, IntPtr.Zero);
            return map;
        }

        /// <summary>Guesses the process name of the app behind a launch target (exe path, shortcut or URI scheme).</summary>
        public static string? HintFromTarget(string target)
        {
            try
            {
                string t = target.Trim().Trim('"');
                if (t.StartsWith("shell:", StringComparison.OrdinalIgnoreCase) || t.StartsWith("http", StringComparison.OrdinalIgnoreCase)) return null;
                int colon = t.IndexOf(':');
                if (colon > 2 && !t.Contains('\\') && !t.Contains('/'))
                {
                    string scheme = t.Substring(0, colon);
                    bool generic = scheme.StartsWith("ms-", StringComparison.OrdinalIgnoreCase) || scheme.Equals("mailto", StringComparison.OrdinalIgnoreCase);
                    return generic ? null : scheme;
                }
                string stem = Path.GetFileNameWithoutExtension(t);
                if (stem.Equals("update", StringComparison.OrdinalIgnoreCase)) return "discord";
                if (stem.Equals("explorer", StringComparison.OrdinalIgnoreCase)) return null;
                return string.IsNullOrWhiteSpace(stem) ? null : stem;
            }
            catch { return null; }
        }

        /// <summary>Call right before launching: raises the new window, or the existing one if the app was already running.</summary>
        public static void FocusAfterLaunch(string? procHint)
        {
            var app = Application.Current;
            if (app == null) return;
            app.Dispatcher.Invoke(() =>
            {
                var before = Snapshot();
                int ticks = 0;
                var timer = new DispatcherTimer { Interval = TimeSpan.FromMilliseconds(150) };
                timer.Tick += (s, e) =>
                {
                    ticks++;
                    try
                    {
                        var now = Snapshot();
                        foreach (var kv in now)
                        {
                            bool isNew = !before.TryGetValue(kv.Key, out bool wasIconic);
                            if (isNew || (wasIconic && !kv.Value)) { timer.Stop(); Bring(kv.Key); return; }
                        }
                        // Nothing new appeared: a single-instance app that was already running, so raise its existing window
                        if (ticks >= 8 && !string.IsNullOrEmpty(procHint))
                        {
                            foreach (var kv in now)
                            {
                                GetWindowThreadProcessId(kv.Key, out uint pid);
                                try
                                {
                                    using var p = Process.GetProcessById((int)pid);
                                    if (p.ProcessName.Contains(procHint, StringComparison.OrdinalIgnoreCase)) { timer.Stop(); Bring(kv.Key); return; }
                                }
                                catch { }
                            }
                        }
                    }
                    catch { }
                    if (ticks >= 30) timer.Stop();
                };
                timer.Start();
            });
        }

        public static void Bring(IntPtr h)
        {
            try
            {
                if (IsIconic(h)) ShowWindow(h, 9); // SW_RESTORE
                // A synthetic Alt tap lets SetForegroundWindow succeed from a background process
                keybd_event(0x12, 0, 0, UIntPtr.Zero);
                keybd_event(0x12, 0, 2, UIntPtr.Zero);
                uint cur = GetCurrentThreadId();
                uint fgThread = GetWindowThreadProcessId(GetForegroundWindow(), out _);
                bool attached = fgThread != 0 && fgThread != cur && AttachThreadInput(cur, fgThread, true);
                BringWindowToTop(h);
                SetForegroundWindow(h);
                if (attached) AttachThreadInput(cur, fgThread, false);
            }
            catch { }
        }
    }
}
