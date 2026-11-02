using System;
using System.Diagnostics;
using System.Runtime.InteropServices;
using System.Windows;
using System.Windows.Threading;

namespace DynamicIsland
{
    public class DockManager
    {
        private static DockManager? _instance;
        public static DockManager Instance => _instance ??= new DockManager();

        private FloatingDockWindow? _dockWindow;
        private LaunchpadWindow? _launchpadWindow;
        private bool _isDockEnabled = false;

        public bool IsDockEnabled => _isDockEnabled;

        public FloatingDockWindow? DockWindow => _dockWindow;
        public LaunchpadWindow? LaunchpadWindow => _launchpadWindow;

        public DockManager()
        {
            AppDomain.CurrentDomain.ProcessExit += (s, e) => RestoreWindowsTaskbar();
            if (Application.Current != null)
            {
                Application.Current.Exit += (s, e) => RestoreWindowsTaskbar();
            }
        }

        public void Initialize(bool enabled)
        {
            SetDockEnabled(enabled);
        }

        public void SetDockEnabled(bool enabled)
        {
            _isDockEnabled = enabled;

            Application.Current?.Dispatcher.Invoke(() =>
            {
                if (enabled)
                {
                    HideWindowsTaskbar();
                    if (_dockWindow == null)
                    {
                        _dockWindow = new FloatingDockWindow();
                    }
                    _dockWindow.Show();
                    _dockWindow.UpdatePosition();
                }
                else
                {
                    if (_launchpadWindow != null && _launchpadWindow.IsVisible)
                    {
                        _launchpadWindow.Hide();
                    }
                    if (_dockWindow != null)
                    {
                        _dockWindow.Hide();
                    }
                    RestoreWindowsTaskbar();
                }
            });
        }

        public void ToggleLaunchpad()
        {
            Application.Current?.Dispatcher.Invoke(() =>
            {
                if (_launchpadWindow == null)
                {
                    _launchpadWindow = new LaunchpadWindow();
                }

                if (_launchpadWindow.IsVisible)
                {
                    _launchpadWindow.Hide();
                }
                else
                {
                    _launchpadWindow.Show();
                    _launchpadWindow.UpdatePosition(_dockWindow);
                    _launchpadWindow.Activate();
                }
            });
        }

        public void HideWindowsTaskbar()
        {
            try
            {
                IntPtr primary = NativeMethods.FindWindow("Shell_TrayWnd", null);
                if (primary != IntPtr.Zero)
                {
                    NativeMethods.ShowWindow(primary, NativeMethods.SW_HIDE);
                }

                IntPtr secondary = NativeMethods.FindWindow("Shell_SecondaryTrayWnd", null);
                if (secondary != IntPtr.Zero)
                {
                    NativeMethods.ShowWindow(secondary, NativeMethods.SW_HIDE);
                }
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"[DockManager] Error hiding taskbar: {ex.Message}");
            }
        }

        public void RestoreWindowsTaskbar()
        {
            try
            {
                IntPtr primary = NativeMethods.FindWindow("Shell_TrayWnd", null);
                if (primary != IntPtr.Zero)
                {
                    NativeMethods.ShowWindow(primary, NativeMethods.SW_SHOW);
                }

                IntPtr secondary = NativeMethods.FindWindow("Shell_SecondaryTrayWnd", null);
                if (secondary != IntPtr.Zero)
                {
                    NativeMethods.ShowWindow(secondary, NativeMethods.SW_SHOW);
                }
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"[DockManager] Error restoring taskbar: {ex.Message}");
            }
        }

        public void AddAppToDock(string name, string path, string? iconAsset = null)
        {
            Application.Current?.Dispatcher.Invoke(() =>
            {
                var config = DockConfig.Load();
                if (!config.Items.Exists(i => i.Name.Equals(name, StringComparison.OrdinalIgnoreCase)))
                {
                    config.Items.Add(new DockConfigItem
                    {
                        Name = name,
                        Path = path,
                        IconAsset = iconAsset
                    });
                    config.Save();
                    _dockWindow?.ReloadConfig();
                }
            });
        }

        public void ReloadDock()
        {
            Application.Current?.Dispatcher.Invoke(() =>
            {
                _dockWindow?.ReloadConfig();
            });
        }

        public static void LaunchApp(string target, string? fallbackUrl = null)
        {
            try
            {
                if (string.IsNullOrWhiteSpace(target)) return;

                if (target.StartsWith("shell:AppsFolder\\", StringComparison.OrdinalIgnoreCase))
                {
                    Process.Start(new ProcessStartInfo("explorer.exe", target) { UseShellExecute = true });
                    return;
                }

                Process.Start(new ProcessStartInfo
                {
                    FileName = target,
                    UseShellExecute = true
                });
            }
            catch
            {
                if (!string.IsNullOrEmpty(fallbackUrl))
                {
                    try
                    {
                        Process.Start(new ProcessStartInfo
                        {
                            FileName = fallbackUrl,
                            UseShellExecute = true
                        });
                    }
                    catch { }
                }
            }
        }
    }
}
