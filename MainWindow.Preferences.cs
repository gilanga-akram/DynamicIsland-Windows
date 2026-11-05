// Applies Settings that previously were only saved: saved position, tab visibility,
// "keep windows below the island" (AppBar reservation), touchscreen mode and cursor magnet.

using System;
using System.Runtime.InteropServices;
using System.Windows;
using System.Windows.Interop;
using System.Windows.Media;
using System.Windows.Threading;

namespace DynamicIsland
{
    public partial class MainWindow
    {
        private AppBarReserve? _appBar;
        private DispatcherTimer? _magnetTimer;

        // ---------- fullscreen detection helpers ----------
        [StructLayout(LayoutKind.Sequential)]
        private struct MonRect { public int Left, Top, Right, Bottom; }
        [StructLayout(LayoutKind.Sequential)]
        private struct MonInfo { public int cbSize; public MonRect rcMonitor; public MonRect rcWork; public uint dwFlags; }
        [DllImport("user32.dll")] private static extern bool IsZoomed(IntPtr hWnd);
        [DllImport("user32.dll")] private static extern IntPtr MonitorFromWindow(IntPtr hwnd, uint flags);
        [DllImport("user32.dll")] private static extern bool GetMonitorInfo(IntPtr hMonitor, ref MonInfo info);

        private static bool TryGetMonitorBounds(IntPtr hwnd, out int left, out int top, out int right, out int bottom)
        {
            left = top = right = bottom = 0;
            try
            {
                IntPtr mon = MonitorFromWindow(hwnd, 2 /* MONITOR_DEFAULTTONEAREST */);
                var mi = new MonInfo { cbSize = Marshal.SizeOf<MonInfo>() };
                if (mon == IntPtr.Zero || !GetMonitorInfo(mon, ref mi)) return false;
                (left, top, right, bottom) = (mi.rcMonitor.Left, mi.rcMonitor.Top, mi.rcMonitor.Right, mi.rcMonitor.Bottom);
                return right > left && bottom > top;
            }
            catch { return false; }
        }

        private void ApplyStartupPreferences()
        {
            // Saved horizontal position (Settings > General > Position along the top)
            if (Settings != null)
            {
                _horizontalPositionPercent = Math.Clamp(Settings.PositionPercent / 100.0, 0.0, 1.0);
                ApplyDockLayout();
            }

            ApplyTabVisibility();
            ApplyKeepWindowsBelow();
            if (!_isTimerRunning && !_isStopwatchMode) SetCountdownPreset(PomodoroFocusMin * 60, $"FOKUS ({PomodoroFocusMin}M)");

            // Touchscreen: there is no "mouse leave" on touch, so close the island when focus moves away
            Deactivated += (s, e) =>
            {
                // A pinned island stays open on purpose: losing focus must neither unpin nor close it
                if (Settings?.TouchscreenSupport == true && _isExpanded && !_isPinned)
                {
                    CollapseIsland();
                }
            };
            System.Windows.Input.Stylus.SetIsPressAndHoldEnabled(IslandBorder, false);

            // Cursor magnet: a slightly larger hover zone around the compact island
            _magnetTimer = new DispatcherTimer { Interval = TimeSpan.FromMilliseconds(80) };
            _magnetTimer.Tick += MagnetTimer_Tick;
            _magnetTimer.Start();

            Closed += (s, e) => { _appBar?.Dispose(); ScreenTimeManager.Instance.Save(); };
            if (Application.Current != null) Application.Current.Exit += (s, e) => { _appBar?.Dispose(); ScreenTimeManager.Instance.Save(); };
        }

        private void ScreenTimeDonut_Click(object sender, System.Windows.Input.MouseButtonEventArgs e)
        {
            e.Handled = true;
            ScreenTimeManager.Instance.Is7DaysFilter = !ScreenTimeManager.Instance.Is7DaysFilter;
        }

        /// <summary>Shows or hides the Tools and Startbar tabs (Settings > Modules).</summary>
        public void ApplyTabVisibility()
        {
            if (Settings == null) return;
            if (BtnTools != null) BtnTools.Visibility = Settings.ShowToolsTab ? Visibility.Visible : Visibility.Collapsed;
            if (BtnTaskbar != null) BtnTaskbar.Visibility = Settings.ShowTaskbarTab ? Visibility.Visible : Visibility.Collapsed;
            if (BtnClipboard != null) BtnClipboard.Visibility = Settings.EnableClipboardManager ? Visibility.Visible : Visibility.Collapsed;

            bool toolsHidden = !Settings.ShowToolsTab && ViewTools?.Visibility == Visibility.Visible;
            bool taskbarHidden = !Settings.ShowTaskbarTab && ViewTaskbar?.Visibility == Visibility.Visible;
            bool clipHidden = !Settings.EnableClipboardManager && ViewClipboard?.Visibility == Visibility.Visible;
            if (toolsHidden || taskbarHidden || clipHidden) SetActiveView(ViewHome, BtnHome);
        }

        /// <summary>Reserves a strip at the top of the screen so maximized windows sit below the island.</summary>
        public void ApplyKeepWindowsBelow()
        {
            try
            {
                if (Settings?.KeepWindowsBelow == true && (Settings?.EnableDynamicIsland ?? true))
                {
                    var dpi = VisualTreeHelper.GetDpi(this);
                    int widthPx = (int)Math.Round(SystemParameters.PrimaryScreenWidth * dpi.DpiScaleX);
                    int heightPx = (int)Math.Round((CompactH + 4) * dpi.DpiScaleY);
                    _appBar ??= new AppBarReserve();
                    _appBar.Reserve(widthPx, heightPx);
                }
                else
                {
                    _appBar?.Release();
                }
            }
            catch { }
        }

        private bool _magnetArmed;

        private void MagnetTimer_Tick(object? sender, EventArgs e)
        {
            if (Settings?.CursorMagnetSnap != true || !_autoExpandOnHover || _isExpanded || !IsVisible) { _magnetArmed = false; return; }
            if (IslandBorder.IsMouseOver) { _magnetArmed = false; return; }

            if (IsCursorInMagnetZone())
            {
                if (!_magnetArmed && !IsGameBlockingActive())
                {
                    _magnetArmed = true;
                    int delay = Math.Max(60, Settings?.HoverDwellDelayMs ?? 180);
                    _hoverDwellTimer.Interval = TimeSpan.FromMilliseconds(delay);
                    _hoverDwellTimer.Start();
                }
            }
            else
            {
                _magnetArmed = false;
            }
        }

        /// <summary>True when the cursor is just below or beside the compact island.</summary>
        private bool IsCursorInMagnetZone()
        {
            try
            {
                if (Settings?.CursorMagnetSnap != true || !IslandBorder.IsVisible) return false;
                if (!NativeCursor.GetCursorPos(out var pt)) return false;
                var topLeft = IslandBorder.PointToScreen(new Point(0, 0));
                var dpi = VisualTreeHelper.GetDpi(this);
                double w = IslandBorder.ActualWidth * dpi.DpiScaleX;
                double h = IslandBorder.ActualHeight * dpi.DpiScaleY;
                double padX = 18 * dpi.DpiScaleX, padY = 22 * dpi.DpiScaleY;
                return pt.X >= topLeft.X - padX && pt.X <= topLeft.X + w + padX &&
                       pt.Y >= topLeft.Y && pt.Y <= topLeft.Y + h + padY;
            }
            catch { return false; }
        }

        // ---------- display selection (Settings > General > Show Dynamic on) ----------
        [StructLayout(LayoutKind.Sequential)] private struct MRECT { public int Left, Top, Right, Bottom; }
        [StructLayout(LayoutKind.Sequential)]
        private struct MONITORINFO { public int cbSize; public MRECT rcMonitor; public MRECT rcWork; public uint dwFlags; }
        private delegate bool MonitorEnumProc(IntPtr hMonitor, IntPtr hdc, IntPtr rect, IntPtr data);
        [DllImport("user32.dll")] private static extern bool EnumDisplayMonitors(IntPtr hdc, IntPtr clip, MonitorEnumProc proc, IntPtr data);
        [DllImport("user32.dll")] private static extern bool GetMonitorInfo(IntPtr hMonitor, ref MONITORINFO mi);

        /// <summary>Monitors in Windows enumeration order: (bounds in physical px, is primary).</summary>
        internal static System.Collections.Generic.List<(Rect Bounds, bool Primary)> GetMonitors()
        {
            var list = new System.Collections.Generic.List<(Rect, bool)>();
            EnumDisplayMonitors(IntPtr.Zero, IntPtr.Zero, (h, _, _, _) =>
            {
                var mi = new MONITORINFO { cbSize = Marshal.SizeOf<MONITORINFO>() };
                if (GetMonitorInfo(h, ref mi))
                    list.Add((new Rect(mi.rcMonitor.Left, mi.rcMonitor.Top, mi.rcMonitor.Right - mi.rcMonitor.Left, mi.rcMonitor.Bottom - mi.rcMonitor.Top), (mi.dwFlags & 1) != 0));
                return true;
            }, IntPtr.Zero);
            return list;
        }

        /// <summary>Screen the island lives on, in WPF units.</summary>
        private Rect TargetScreenDip()
        {
            var primary = new Rect(0, 0, SystemParameters.PrimaryScreenWidth, SystemParameters.PrimaryScreenHeight);
            try
            {
                string sel = Settings?.SelectedDisplay ?? "Primary display";
                if (!sel.StartsWith("Monitor ", StringComparison.OrdinalIgnoreCase)) return primary;
                if (!int.TryParse(sel.Substring(8).Trim(), out int n) || n < 1) return primary;
                var mons = GetMonitors();
                if (n > mons.Count) return primary;
                var b = mons[n - 1].Bounds;
                var dpi = VisualTreeHelper.GetDpi(this);
                return new Rect(b.X / dpi.DpiScaleX, b.Y / dpi.DpiScaleY, b.Width / dpi.DpiScaleX, b.Height / dpi.DpiScaleY);
            }
            catch { return primary; }
        }

        private static class NativeCursor
        {
            [StructLayout(LayoutKind.Sequential)] public struct POINT { public int X; public int Y; }
            [DllImport("user32.dll")] public static extern bool GetCursorPos(out POINT pt);
        }
    }

    /// <summary>Registers an invisible top-edge AppBar so Windows keeps maximized windows below it.</summary>
    internal sealed class AppBarReserve : IDisposable
    {
        [StructLayout(LayoutKind.Sequential)] private struct RECT { public int left, top, right, bottom; }
        [StructLayout(LayoutKind.Sequential)]
        private struct APPBARDATA
        {
            public int cbSize; public IntPtr hWnd; public uint uCallbackMessage; public uint uEdge; public RECT rc; public IntPtr lParam;
        }
        [DllImport("shell32.dll")] private static extern UIntPtr SHAppBarMessage(uint msg, ref APPBARDATA data);
        [DllImport("user32.dll", CharSet = CharSet.Unicode)] private static extern uint RegisterWindowMessage(string name);

        private const uint ABM_NEW = 0, ABM_REMOVE = 1, ABM_QUERYPOS = 2, ABM_SETPOS = 3, ABE_TOP = 1;
        private HwndSource? _src;
        private bool _registered;

        private APPBARDATA NewData() => new APPBARDATA
        {
            cbSize = Marshal.SizeOf<APPBARDATA>(),
            hWnd = _src!.Handle,
            uCallbackMessage = RegisterWindowMessage("DynamicIslandAppBar")
        };

        public void Reserve(int widthPx, int heightPx)
        {
            if (_src == null)
            {
                var p = new HwndSourceParameters("DynamicIslandAppBar")
                {
                    WindowStyle = unchecked((int)0x80000000),          // WS_POPUP, never shown
                    ExtendedWindowStyle = 0x00000080 | 0x08000000,       // TOOLWINDOW | NOACTIVATE
                    Width = 0, Height = 0, PositionX = -32000, PositionY = -32000
                };
                _src = new HwndSource(p);
            }

            var abd = NewData();
            if (!_registered) { SHAppBarMessage(ABM_NEW, ref abd); _registered = true; }
            abd.uEdge = ABE_TOP;
            abd.rc = new RECT { left = 0, top = 0, right = widthPx, bottom = heightPx };
            SHAppBarMessage(ABM_QUERYPOS, ref abd);
            abd.rc.bottom = abd.rc.top + heightPx;
            SHAppBarMessage(ABM_SETPOS, ref abd);
        }

        public void Release()
        {
            if (!_registered || _src == null) return;
            var abd = NewData();
            SHAppBarMessage(ABM_REMOVE, ref abd);
            _registered = false;
        }

        public void Dispose()
        {
            try { Release(); } catch { }
            _src?.Dispose();
            _src = null;
        }
    }
}
