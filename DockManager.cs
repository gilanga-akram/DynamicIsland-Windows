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

        // The dock is summoned with the Windows key and hides itself again once it is no longer in use
        private bool _dockWanted;
        private DateTime _dockShownAt = DateTime.MinValue;
        private DispatcherTimer? _autoHideTimer;

        /// <summary>True while the dock should be on screen (it was summoned and has not auto-hidden yet).</summary>
        public bool DockWanted => _dockWanted;

        /// <summary>Shows the dock (used by the Windows key). No-op when the dock feature is off.</summary>
        public void RevealDock()
        {
            Application.Current?.Dispatcher.Invoke(() =>
            {
                if (!_isDockEnabled || _dockWindow == null) return;
                _dockWanted = true;
                _dockShownAt = DateTime.UtcNow;
                _dockWindow.UpdatePosition();
                _dockWindow.Show();
                _dockWindow.SetBehindGameState(false);
                EnsureAutoHideTimer();
            });
        }

        public void HideDock()
        {
            Application.Current?.Dispatcher.Invoke(() =>
            {
                _dockWanted = false;
                if (_launchpadWindow != null && _launchpadWindow.IsVisible) _launchpadWindow.Hide();
                _dockWindow?.Hide();
            });
        }

        /// <summary>Windows-key tap: show dock + launchpad, or put both away if the dock is already out.</summary>
        public void ToggleSummon()
        {
            Application.Current?.Dispatcher.Invoke(() =>
            {
                if (!_isDockEnabled) return;
                if (_dockWanted && _dockWindow != null && _dockWindow.IsVisible)
                {
                    HideDock();
                    return;
                }
                RevealDock();
                if (_launchpadWindow == null || !_launchpadWindow.IsVisible) ToggleLaunchpad();
            });
        }

        private void EnsureAutoHideTimer()
        {
            if (_autoHideTimer != null) { _autoHideTimer.Start(); return; }
            _autoHideTimer = new DispatcherTimer { Interval = TimeSpan.FromMilliseconds(500) };
            _autoHideTimer.Tick += (s, e) =>
            {
                if (!_dockWanted || _dockWindow == null) { _autoHideTimer?.Stop(); return; }
                if ((DateTime.UtcNow - _dockShownAt).TotalMilliseconds < 1800) return;
                bool inUse = (_launchpadWindow?.IsVisible ?? false) || _dockWindow.IsMouseOver || _dockWindow.IsActive
                             || !_dockWindow.IsEnabled || _dockWindow.IsContextMenuOpen;
                if (!inUse) HideDock();
            };
            _autoHideTimer.Start();
        }

        public void Initialize(bool enabled)
        {
            // A previous run that was killed while the dock was on leaves the Windows taskbar hidden; bring it back
            if (!enabled) RestoreWindowsTaskbar();
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
                    // Create/load the dock but keep it out of sight until the Windows key summons it
                    _dockWindow.Show();
                    _dockWindow.UpdatePosition();
                    _dockWindow.Hide();
                    _dockWanted = false;
                }
                else
                {
                    _dockWanted = false;
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

                // The click on the launcher button already deactivated (and hid) the launchpad; don't reopen it
                if (!_launchpadWindow.IsVisible && (DateTime.UtcNow - _launchpadWindow.LastHiddenUtc).TotalMilliseconds < 300)
                    return;

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
                WindowFocus.FocusAfterLaunch(WindowFocus.HintFromTarget(target));

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
