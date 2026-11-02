// Language: C#
// Filename: StartbarManager.cs
// Runs on: Windows 10 / Windows 11 (.NET 9.0-windows10.0.19041.0)
// Targets: Dynamic Island Startbar & Windows Task Manager

using System;
using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Diagnostics;
using System.IO;
using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;
using System.Text;
using System.Windows;
using System.Windows.Interop;
using System.Windows.Media;
using System.Windows.Media.Imaging;

namespace DynamicIsland
{
    public class WindowTaskItem : INotifyPropertyChanged
    {
        private string _title = string.Empty;
        private ImageSource? _icon;
        private bool _isCurrentActive;

        public IntPtr Hwnd { get; set; }
        public int ProcessId { get; set; }
        public string ProcessName { get; set; } = string.Empty;

        public string Title
        {
            get => _title;
            set { _title = value; OnPropertyChanged(); }
        }

        public ImageSource? Icon
        {
            get => _icon;
            set { _icon = value; OnPropertyChanged(); }
        }

        public bool IsCurrentActive
        {
            get => _isCurrentActive;
            set { _isCurrentActive = value; OnPropertyChanged(); }
        }

        public event PropertyChangedEventHandler? PropertyChanged;
        protected void OnPropertyChanged([CallerMemberName] string? propName = null)
        {
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propName));
        }
    }

    public class StartbarManager : INotifyPropertyChanged
    {
        private static StartbarManager? _instance;
        public static StartbarManager Instance => _instance ??= new StartbarManager();

        public ObservableCollection<WindowTaskItem> ActiveWindows { get; } = new();

        private string _searchFilter = string.Empty;
        public string SearchFilter
        {
            get => _searchFilter;
            set
            {
                if (_searchFilter != value)
                {
                    _searchFilter = value;
                    OnPropertyChanged();
                    RefreshWindows();
                }
            }
        }

        public event PropertyChangedEventHandler? PropertyChanged;
        protected void OnPropertyChanged([CallerMemberName] string? propName = null)
        {
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propName));
        }

        #region Win32 P/Invoke
        private delegate bool EnumWindowsProc(IntPtr hWnd, IntPtr lParam);

        [DllImport("user32.dll")]
        [return: MarshalAs(UnmanagedType.Bool)]
        private static extern bool EnumWindows(EnumWindowsProc lpEnumFunc, IntPtr lParam);

        [DllImport("user32.dll")]
        [return: MarshalAs(UnmanagedType.Bool)]
        private static extern bool IsWindowVisible(IntPtr hWnd);

        [DllImport("user32.dll", CharSet = CharSet.Auto, SetLastError = true)]
        private static extern int GetWindowText(IntPtr hWnd, StringBuilder lpString, int nMaxCount);

        [DllImport("user32.dll", SetLastError = true, CharSet = CharSet.Auto)]
        private static extern int GetWindowTextLength(IntPtr hWnd);

        [DllImport("user32.dll")]
        private static extern uint GetWindowThreadProcessId(IntPtr hWnd, out uint lpdwProcessId);

        [DllImport("user32.dll", EntryPoint = "GetWindowLong")]
        private static extern IntPtr GetWindowLongPtr32(IntPtr hWnd, int nIndex);

        [DllImport("user32.dll", EntryPoint = "GetWindowLongPtr")]
        private static extern IntPtr GetWindowLongPtr64(IntPtr hWnd, int nIndex);

        private static IntPtr GetWindowLongPtr(IntPtr hWnd, int nIndex)
        {
            return IntPtr.Size == 8 ? GetWindowLongPtr64(hWnd, nIndex) : GetWindowLongPtr32(hWnd, nIndex);
        }

        [DllImport("user32.dll")]
        private static extern IntPtr GetForegroundWindow();

        [DllImport("user32.dll")]
        private static extern IntPtr SendMessage(IntPtr hWnd, uint Msg, IntPtr wParam, IntPtr lParam);

        [DllImport("user32.dll")]
        [return: MarshalAs(UnmanagedType.Bool)]
        private static extern bool ShowWindowAsync(IntPtr hWnd, int nCmdShow);

        [DllImport("user32.dll")]
        [return: MarshalAs(UnmanagedType.Bool)]
        private static extern bool SetForegroundWindow(IntPtr hWnd);

        [DllImport("user32.dll", SetLastError = true)]
        private static extern bool PostMessage(IntPtr hWnd, uint Msg, IntPtr wParam, IntPtr lParam);

        [DllImport("user32.dll", SetLastError = true)]
        private static extern bool DestroyIcon(IntPtr hIcon);

        [StructLayout(LayoutKind.Sequential, CharSet = CharSet.Auto)]
        private struct SHFILEINFO
        {
            public IntPtr hIcon;
            public int iIcon;
            public uint dwAttributes;
            [MarshalAs(UnmanagedType.ByValTStr, SizeConst = 260)]
            public string szDisplayName;
            [MarshalAs(UnmanagedType.ByValTStr, SizeConst = 80)]
            public string szTypeName;
        }

        [DllImport("shell32.dll", CharSet = CharSet.Auto)]
        private static extern IntPtr SHGetFileInfo(string pszPath, uint dwFileAttributes, ref SHFILEINFO psfi, uint cbFileInfo, uint uFlags);

        private const uint SHGFI_ICON = 0x000000100;
        private const uint SHGFI_SMALLICON = 0x000000001;

        private const int GWL_STYLE = -16;
        private const int GWL_EXSTYLE = -20;
        private const long WS_VISIBLE = 0x10000000L;
        private const long WS_EX_TOOLWINDOW = 0x00000080L;
        private const long WS_EX_APPWINDOW = 0x00040000L;

        private const uint WM_GETICON = 0x007F;
        private const uint WM_CLOSE = 0x0010;
        private const IntPtr ICON_SMALL = 0;
        private const IntPtr ICON_BIG = 1;
        private const IntPtr ICON_SMALL2 = 2;

        private const int SW_RESTORE = 9;
        private const int SW_MINIMIZE = 6;
        private const int SW_SHOW = 5;
        #endregion

        public void RefreshWindows()
        {
            try
            {
                var fgHwnd = GetForegroundWindow();
                var currentPid = Process.GetCurrentProcess().Id;
                var list = new System.Collections.Generic.List<WindowTaskItem>();

                EnumWindows((hWnd, lParam) =>
                {
                    if (!IsWindowVisible(hWnd)) return true;

                    int len = GetWindowTextLength(hWnd);
                    if (len <= 0) return true;

                    var sb = new StringBuilder(len + 1);
                    GetWindowText(hWnd, sb, sb.Capacity);
                    string title = sb.ToString().Trim();

                    if (string.IsNullOrEmpty(title)) return true;

                    // Filter out desktop and system shells
                    if (title == "Program Manager" || title == "Windows Input Experience" || title == "Dynamic Island Pro") return true;

                    long exStyle = (long)GetWindowLongPtr(hWnd, GWL_EXSTYLE);
                    if ((exStyle & WS_EX_TOOLWINDOW) != 0 && (exStyle & WS_EX_APPWINDOW) == 0)
                    {
                        return true;
                    }

                    GetWindowThreadProcessId(hWnd, out uint pid);
                    if (pid == currentPid || pid == 0) return true;

                    string procName = "App";
                    string? procPath = null;
                    try
                    {
                        var proc = Process.GetProcessById((int)pid);
                        procName = proc.ProcessName;
                        try { procPath = proc.MainModule?.FileName; } catch { }
                    }
                    catch { }

                    // Apply search filter if any
                    if (!string.IsNullOrWhiteSpace(_searchFilter))
                    {
                        if (!title.Contains(_searchFilter, StringComparison.OrdinalIgnoreCase) &&
                            !procName.Contains(_searchFilter, StringComparison.OrdinalIgnoreCase))
                        {
                            return true;
                        }
                    }

                    var icon = ExtractWindowIcon(hWnd, procPath);

                    list.Add(new WindowTaskItem
                    {
                        Hwnd = hWnd,
                        ProcessId = (int)pid,
                        ProcessName = procName,
                        Title = title,
                        Icon = icon,
                        IsCurrentActive = (hWnd == fgHwnd)
                    });

                    return true;
                }, IntPtr.Zero);

                System.Windows.Application.Current?.Dispatcher.Invoke(() =>
                {
                    ActiveWindows.Clear();
                    foreach (var item in list)
                    {
                        ActiveWindows.Add(item);
                    }
                });
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"[StartbarManager] Refresh error: {ex.Message}");
            }
        }

        private ImageSource? ExtractWindowIcon(IntPtr hWnd, string? procPath)
        {
            IntPtr hIcon = SendMessage(hWnd, WM_GETICON, ICON_SMALL2, IntPtr.Zero);
            if (hIcon == IntPtr.Zero)
                hIcon = SendMessage(hWnd, WM_GETICON, ICON_SMALL, IntPtr.Zero);
            if (hIcon == IntPtr.Zero)
                hIcon = SendMessage(hWnd, WM_GETICON, ICON_BIG, IntPtr.Zero);

            if (hIcon != IntPtr.Zero)
            {
                try
                {
                    var img = Imaging.CreateBitmapSourceFromHIcon(hIcon, Int32Rect.Empty, BitmapSizeOptions.FromEmptyOptions());
                    img.Freeze();
                    return img;
                }
                catch { }
            }

            if (!string.IsNullOrEmpty(procPath) && File.Exists(procPath))
            {
                return GetIconFromPath(procPath);
            }

            return null;
        }

        public static ImageSource? GetIconFromPath(string? procPath)
        {
            if (string.IsNullOrEmpty(procPath) || !File.Exists(procPath)) return null;
            try
            {
                var shinfo = new SHFILEINFO();
                IntPtr res = SHGetFileInfo(procPath, 0, ref shinfo, (uint)Marshal.SizeOf(shinfo), SHGFI_ICON | SHGFI_SMALLICON);
                if (res != IntPtr.Zero && shinfo.hIcon != IntPtr.Zero)
                {
                    var img = Imaging.CreateBitmapSourceFromHIcon(shinfo.hIcon, Int32Rect.Empty, BitmapSizeOptions.FromEmptyOptions());
                    img.Freeze();
                    DestroyIcon(shinfo.hIcon);
                    return img;
                }
            }
            catch { }
            return null;
        }

        public void ActivateWindow(IntPtr hWnd)
        {
            if (hWnd == IntPtr.Zero) return;
            try
            {
                ShowWindowAsync(hWnd, SW_RESTORE);
                SetForegroundWindow(hWnd);
                RefreshWindows();
            }
            catch { }
        }

        public void MinimizeWindow(IntPtr hWnd)
        {
            if (hWnd == IntPtr.Zero) return;
            try
            {
                ShowWindowAsync(hWnd, SW_MINIMIZE);
                RefreshWindows();
            }
            catch { }
        }

        public void CloseWindow(IntPtr hWnd)
        {
            if (hWnd == IntPtr.Zero) return;
            try
            {
                PostMessage(hWnd, WM_CLOSE, IntPtr.Zero, IntPtr.Zero);
                System.Threading.Tasks.Task.Delay(150).ContinueWith(_ => RefreshWindows());
            }
            catch { }
        }

        public static void Launch(string command, string? args = null)
        {
            try
            {
                Process.Start(new ProcessStartInfo
                {
                    FileName = command,
                    Arguments = args ?? string.Empty,
                    UseShellExecute = true
                });
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"[StartbarManager] Launch failed: {ex.Message}");
            }
        }
    }
}