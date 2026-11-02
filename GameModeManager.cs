// Language: C#
// Filename: GameModeManager.cs
// Runs on: Windows 10 / Windows 11 (.NET 9.0-windows)
// Targets: Gaming Hub, Live Game Detection, Game Boost, WinLock, and Low-Latency Telemetry

using System;
using System.ComponentModel;
using System.Diagnostics;
using System.IO;
using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;
using System.Text;
using System.Windows.Threading;

namespace DynamicIsland
{
    public class GameModeManager : INotifyPropertyChanged
    {
        private static GameModeManager? _instance;
        public static GameModeManager Instance => _instance ??= new GameModeManager();

        private readonly DispatcherTimer _pollTimer;

        private string _activeGameTitle = "Mode Gaming: Standby";
        private bool _isGameRunning = false;
        private bool _isBoostActive = false;
        private bool _isWinLockActive = false;
        private bool _isDndActive = false;
        private string _boostStatusText = "🚀 Game Boost Siap";
        private string _cleanRamStatus = "🧹 Bersihkan RAM";

        // Keyboard Hook for WinKey Lock
        private IntPtr _hookId = IntPtr.Zero;
        private LowLevelKeyboardProc? _proc;

        public event PropertyChangedEventHandler? PropertyChanged;

        public string ActiveGameTitle
        {
            get => _activeGameTitle;
            private set { _activeGameTitle = value; OnPropertyChanged(); }
        }

        public bool IsGameRunning
        {
            get => _isGameRunning;
            private set { _isGameRunning = value; OnPropertyChanged(); }
        }

        public bool IsBoostActive
        {
            get => _isBoostActive;
            set { _isBoostActive = value; OnPropertyChanged(); OnPropertyChanged(nameof(BoostButtonText)); }
        }

        public bool IsWinLockActive
        {
            get => _isWinLockActive;
            set { _isWinLockActive = value; OnPropertyChanged(); OnPropertyChanged(nameof(WinLockButtonText)); }
        }

        public bool IsDndActive
        {
            get => _isDndActive;
            set { _isDndActive = value; OnPropertyChanged(); }
        }

        public string BoostButtonText => _isBoostActive ? "🚀 Boost: AKTIF" : "🚀 Game Boost";
        public string WinLockButtonText => _isWinLockActive ? "🔒 WinLock: AKTIF" : "🔓 WinLock";

        public string BoostStatusText
        {
            get => _boostStatusText;
            private set { _boostStatusText = value; OnPropertyChanged(); }
        }

        public string CleanRamStatus
        {
            get => _cleanRamStatus;
            private set { _cleanRamStatus = value; OnPropertyChanged(); }
        }

        public int CurrentFps => SystemMetrics.Instance?.CurrentFps ?? 60;
        public double FrameTimeMs => CurrentFps > 0 ? Math.Round(1000.0 / CurrentFps, 1) : 4.6;

        public GameModeManager()
        {
            _pollTimer = new DispatcherTimer { Interval = TimeSpan.FromMilliseconds(250) };
            _pollTimer.Tick += (s, e) => PollActiveGame();
            _pollTimer.Start();

            // Always install hook so Win key → toggles Launchpad instead of Start Menu
            InstallKeyboardHook();
        }

        private void PollActiveGame()
        {
            try
            {
                IntPtr hwnd = NativeMethods.GetForegroundWindow();
                if (hwnd == IntPtr.Zero) return;

                NativeMethods.GetWindowThreadProcessId(hwnd, out uint pid);
                if (pid == 0 || pid == (uint)Environment.ProcessId) return;

                using var proc = Process.GetProcessById((int)pid);
                string procName = proc.ProcessName.ToLowerInvariant();
                string winTitle = proc.MainWindowTitle;

                // Known game binaries and engines
                bool isGame = IsKnownGame(procName, winTitle);

                // Or fullscreen DirectX / exclusive state
                if (!isGame && NativeMethods.SHQueryUserNotificationState(out var state) == 0)
                {
                    if (state == NativeMethods.QUERY_USER_NOTIFICATION_STATE.QUNS_RUNNING_D_3D_FULL_SCREEN_EXCLUSIVE)
                    {
                        isGame = true;
                    }
                }

                if (isGame)
                {
                    IsGameRunning = true;
                    string friendlyName = GetFriendlyGameName(procName, winTitle);
                    ActiveGameTitle = $"🎮 {friendlyName}";

                    if (_isBoostActive && proc.PriorityClass != ProcessPriorityClass.High)
                    {
                        try { proc.PriorityClass = ProcessPriorityClass.High; } catch { }
                    }
                }
                else
                {
                    IsGameRunning = false;
                    ActiveGameTitle = "🎮 Mode Gaming: Siap / Desktop";
                }

                OnPropertyChanged(nameof(CurrentFps));
                OnPropertyChanged(nameof(FrameTimeMs));
            }
            catch
            {
                // Process may have exited
            }
        }

        public static bool IsKnownGame(string procName, string winTitle)
        {
            string p = procName.ToLowerInvariant();
            string t = winTitle.ToLowerInvariant();

            if (p.Contains("valorant") || p.Contains("cs2") || p.Contains("csgo") ||
                p.Contains("dota2") || p.Contains("league") || p.Contains("genshin") ||
                p.Contains("honkai") || p.Contains("gta") || p.Contains("fivem") ||
                p.Contains("roblox") || p.Contains("minecraft") || p.Contains("javaw") ||
                p.Contains("fortnite") || p.Contains("apex") || p.Contains("overwatch") ||
                p.Contains("rocketleague") || p.Contains("r5apex") || p.Contains("cyberpunk") ||
                p.Contains("eldenring") || p.Contains("forzahorizon") || p.Contains("fifa") ||
                p.Contains("fc24") || p.Contains("fc25") || p.Contains("witcher") ||
                p.Contains("pubg") || p.Contains("tslgame") || p.Contains("unreal") ||
                p.Contains("unity") || p.Contains("rdr2") || p.Contains("redm") ||
                p.Contains("citizenmp") || p.Contains("rage") || p.Contains("rust") ||
                p.Contains("tarkov") || p.Contains("palworld") || p.Contains("helldivers") ||
                p.Contains("wuthering") || p.Contains("b1") || p.Contains("blackmyth") ||
                p.Contains("warzone") || p.Contains("cod") || p.Contains("osu") ||
                p.Contains("deadbydaylight") || p.Contains("gameloop") || p.Contains("bluestacks") ||
                p.Contains("ldplayer") || p.Contains("nox") || p.Contains("retroarch") ||
                p.Contains("rpcs3") || p.Contains("yuzu") || p.Contains("ryujinx") ||
                p.Contains("cemu") ||
                t.Contains("fivem") || t.Contains("roleplay") || t.Contains("grand theft auto") ||
                t.Contains("kotakita") || t.Contains("gta v") || t.Contains("gta 5"))
            {
                return true;
            }

            // Check if launched under steamapps or common game folders
            try
            {
                using var pr = Process.GetProcessesByName(procName)[0];
                string? mainMod = pr.MainModule?.FileName?.ToLowerInvariant();
                if (mainMod != null && (
                    mainMod.Contains("steamapps") ||
                    mainMod.Contains("epic games") ||
                    mainMod.Contains("riot games") ||
                    mainMod.Contains("ubisoft") ||
                    mainMod.Contains("fivem.app") ||
                    mainMod.Contains("games") ||
                    mainMod.Contains("steamlibrary") ||
                    mainMod.Contains("gog galaxy") ||
                    mainMod.Contains("electronic arts") ||
                    mainMod.Contains("xboxgames")))
                {
                    return true;
                }
            }
            catch { }

            return false;
        }

        private static string GetFriendlyGameName(string procName, string winTitle)
        {
            if (!string.IsNullOrWhiteSpace(winTitle) && winTitle.Length > 2 && !winTitle.Contains("exe"))
                return winTitle;

            string p = procName.ToLowerInvariant();
            if (p.Contains("valorant")) return "VALORANT";
            if (p.Contains("cs2") || p.Contains("csgo")) return "Counter-Strike 2";
            if (p.Contains("dota2")) return "Dota 2";
            if (p.Contains("genshin")) return "Genshin Impact";
            if (p.Contains("honkai")) return "Honkai: Star Rail";
            if (p.Contains("roblox")) return "Roblox";
            if (p.Contains("minecraft") || p.Contains("javaw")) return "Minecraft";
            if (p.Contains("cyberpunk")) return "Cyberpunk 2077";
            if (p.Contains("eldenring")) return "Elden Ring";
            if (p.Contains("apex")) return "Apex Legends";
            if (p.Contains("pubg") || p.Contains("tslgame")) return "PUBG: BATTLEGROUNDS";

            return char.ToUpper(procName[0]) + procName.Substring(1);
        }

        public void ToggleBoost()
        {
            IsBoostActive = !IsBoostActive;
            if (IsBoostActive)
            {
                BoostStatusText = "🚀 Game Boost AKTIF (High Priority)";
                CleanRamNow();
                try
                {
                    IntPtr hwnd = NativeMethods.GetForegroundWindow();
                    NativeMethods.GetWindowThreadProcessId(hwnd, out uint pid);
                    if (pid != 0)
                    {
                        using var proc = Process.GetProcessById((int)pid);
                        proc.PriorityClass = ProcessPriorityClass.High;
                    }
                }
                catch { }
                SoundManager.Play(AtollSound.Dynamic);
            }
            else
            {
                BoostStatusText = "🚀 Game Boost Nonaktif";
                SoundManager.Play(AtollSound.Feedback);
            }
        }

        public void ToggleWinLock()
        {
            // Hook stays installed always; flag switches behavior between
            // "open Launchpad" and "block key entirely" (game mode)
            IsWinLockActive = !_isWinLockActive;
            SoundManager.Play(_isWinLockActive ? AtollSound.Dynamic : AtollSound.Feedback);
        }

        public void ToggleDnd()
        {
            IsDndActive = !IsDndActive;
            SoundManager.Play(AtollSound.Feedback);
        }

        public void CleanRamNow()
        {
            try
            {
                long before = GC.GetTotalMemory(false);
                GC.Collect();
                GC.WaitForPendingFinalizers();

                // Empty working set for background processes
                foreach (var p in Process.GetProcesses())
                {
                    try
                    {
                        if (p.Id != Environment.ProcessId && !p.ProcessName.Equals("explorer", StringComparison.OrdinalIgnoreCase))
                        {
                            NativeMethods.EmptyWorkingSet(p.Handle);
                        }
                    }
                    catch { }
                }

                CleanRamStatus = "🧹 RAM Dibersihkan!";
                var t = new DispatcherTimer { Interval = TimeSpan.FromSeconds(3) };
                t.Tick += (s, e) =>
                {
                    CleanRamStatus = "🧹 Bersihkan RAM";
                    t.Stop();
                };
                t.Start();
                SoundManager.Play(AtollSound.Feedback);
            }
            catch
            {
                CleanRamStatus = "🧹 Bersihkan RAM";
            }
        }

        #region Low-Level Keyboard Hook for WinLock
        private delegate IntPtr LowLevelKeyboardProc(int nCode, IntPtr wParam, IntPtr lParam);

        private void InstallKeyboardHook()
        {
            if (_hookId != IntPtr.Zero) return;
            _proc = HookCallback;
            using var curProcess = Process.GetCurrentProcess();
            using var curModule = curProcess.MainModule;
            if (curModule != null)
            {
                _hookId = SetWindowsHookEx(WH_KEYBOARD_LL, _proc, GetModuleHandle(curModule.ModuleName), 0);
            }
        }

        private void UninstallKeyboardHook()
        {
            if (_hookId != IntPtr.Zero)
            {
                UnhookWindowsHookEx(_hookId);
                _hookId = IntPtr.Zero;
            }
        }

        private const int WH_KEYBOARD_LL = 13;
        private const int WM_KEYDOWN = 0x0100;
        private const int WM_KEYUP = 0x0101;
        private const int VK_LWIN = 0x5B;
        private const int VK_RWIN = 0x5C;

        private IntPtr HookCallback(int nCode, IntPtr wParam, IntPtr lParam)
        {
            if (nCode >= 0)
            {
                int vkCode = Marshal.ReadInt32(lParam);
                if (vkCode == VK_LWIN || vkCode == VK_RWIN)
                {
                    if (_isWinLockActive)
                    {
                        // Game mode: block Win key entirely
                        return (IntPtr)1;
                    }

                    // Normal mode: intercept WinKey keydown → toggle Launchpad
                    if ((int)wParam == WM_KEYDOWN)
                    {
                        System.Windows.Application.Current?.Dispatcher.BeginInvoke(() =>
                        {
                            DockManager.Instance.ToggleLaunchpad();
                        });
                        // Block native Start Menu from opening
                        return (IntPtr)1;
                    }

                    // Block WM_KEYUP too so no ghost Start Menu opens
                    return (IntPtr)1;
                }
            }
            return CallNextHookEx(_hookId, nCode, wParam, lParam);
        }

        [DllImport("user32.dll", CharSet = CharSet.Auto, SetLastError = true)]
        private static extern IntPtr SetWindowsHookEx(int idHook, LowLevelKeyboardProc lpfn, IntPtr hMod, uint dwThreadId);

        [DllImport("user32.dll", CharSet = CharSet.Auto, SetLastError = true)]
        [return: MarshalAs(UnmanagedType.Bool)]
        private static extern bool UnhookWindowsHookEx(IntPtr hhk);

        [DllImport("user32.dll", CharSet = CharSet.Auto, SetLastError = true)]
        private static extern IntPtr CallNextHookEx(IntPtr hhk, int nCode, IntPtr wParam, IntPtr lParam);

        [DllImport("kernel32.dll", CharSet = CharSet.Auto, SetLastError = true)]
        private static extern IntPtr GetModuleHandle(string lpModuleName);
        #endregion

        private void OnPropertyChanged([CallerMemberName] string? name = null)
        {
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(name));
        }
    }
}
