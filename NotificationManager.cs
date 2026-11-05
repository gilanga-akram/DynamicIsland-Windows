// Language: C#
// Filename: NotificationManager.cs
// Runs on: Windows 10 / Windows 11 (.NET 9.0-windows10.0.19041.0)
// Targets: Multi-layered Realtime Desktop Notification Interceptor (Discord, Steam, Windows Toast)

using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Runtime.InteropServices;
using System.Text;
using System.Text.RegularExpressions;
using System.Threading;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Automation;
using Windows.UI.Notifications;
using Windows.UI.Notifications.Management;

namespace DynamicIsland
{
    public enum NotificationAppType
    {
        Discord,
        Steam,
        WhatsApp,
        General
    }

    /// <summary>Steam's own icon, read from the installed steam.exe (no logo file ships with the app).</summary>
    public static class SteamLogo
    {
        private static System.Windows.Media.Imaging.BitmapSource? _cached;
        private static bool _tried;

        public static System.Windows.Media.Imaging.BitmapSource? Get()
        {
            if (_tried) return _cached;
            _tried = true;
            try
            {
                string? path = null;
                var running = Process.GetProcessesByName("steam");
                try { if (running.Length > 0) path = running[0].MainModule?.FileName; } catch { }

                if (string.IsNullOrEmpty(path))
                {
                    string? reg = Microsoft.Win32.Registry.GetValue(@"HKEY_CURRENT_USER\Software\Valve\Steam", "SteamExe", null) as string;
                    if (!string.IsNullOrEmpty(reg)) path = reg.Replace('/', '\\');
                }
                if (string.IsNullOrEmpty(path) || !File.Exists(path))
                    path = @"C:\Program Files (x86)\Steam\steam.exe";
                if (!File.Exists(path)) return null;

                var shinfo = new NativeMethods.SHFILEINFO();
                NativeMethods.SHGetFileInfo(path, 0, ref shinfo, (uint)Marshal.SizeOf(shinfo),
                    NativeMethods.SHGFI_ICON | NativeMethods.SHGFI_LARGEICON);
                if (shinfo.hIcon == IntPtr.Zero) return null;

                var bmp = System.Windows.Interop.Imaging.CreateBitmapSourceFromHIcon(
                    shinfo.hIcon, Int32Rect.Empty, System.Windows.Media.Imaging.BitmapSizeOptions.FromEmptyOptions());
                bmp.Freeze();
                NativeMethods.DestroyIcon(shinfo.hIcon);
                _cached = bmp;
            }
            catch { }
            return _cached;
        }
    }

    public class NotificationItem
    {
        public string Id { get; set; } = Guid.NewGuid().ToString();
        public NotificationAppType AppType { get; set; } = NotificationAppType.General;
        public string AppName { get; set; } = "Notifikasi";
        public string Title { get; set; } = "";
        public string Message { get; set; } = "";
        public DateTime Timestamp { get; set; } = DateTime.Now;
        public string AccentColor { get; set; } = "#0A84FF";
        public string TargetProcess { get; set; } = "";
        public string Subtitle { get; set; } = "";
        /// <summary>True when the text came straight from a Windows toast (most reliable source).</summary>
        public bool FromSystemToast { get; set; }
    }

    public class NotificationManager
    {
        public static NotificationManager? Instance { get; private set; }

        private static readonly string LogFile = Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData),
            "DynamicIslandPro", "app.log");

        private UserNotificationListener? _listener;
        private bool _isListening = false;
        private bool _isAccessGranted = false;

        // Title Poller State
        private readonly System.Threading.Timer _titlePoller;
        private int _lastDiscordUnread = 0;
        private string _lastDiscordTitle = "";
        private readonly HashSet<IntPtr> _knownSteamWindows = new();

        // Deduplication & Debounce
        private string _lastNotificationKey = "";
        private DateTime _lastNotificationTime = DateTime.MinValue;

        public event Action<NotificationItem>? NotificationReceived;

        public bool IsAccessGranted => _isAccessGranted;
        public bool IsListening => _isListening;

        public NotificationManager()
        {
            Instance = this;
            Log("[NOTIF] Initializing NotificationManager multi-engine listener...");

            // 1. Try Windows Native WinRT UserNotificationListener
            _ = InitializeListenerAsync();

            // 2. Start UI Automation Global Window Creation Listener in background thread
            Task.Run(() => InitializeUIAutomation());

            // 3. Start Window Title & State Poller (every 600ms)
            _titlePoller = new System.Threading.Timer(PollWindowStates, null, 1000, 600);
        }

        private static void Log(string msg)
        {
            try
            {
                File.AppendAllText(LogFile, $"[{DateTime.Now}] {msg}\n");
            }
            catch { }
        }

        // ==============================================
        // LAYER 1: WINDOWS WINRT LISTENER (IF AVAILABLE)
        // ==============================================
        public async Task<bool> InitializeListenerAsync()
        {
            try
            {
                if (UserNotificationListener.Current == null)
                {
                    Log("[NOTIF] UserNotificationListener.Current is null.");
                    return false;
                }

                _listener = UserNotificationListener.Current;
                UserNotificationListenerAccessStatus accessStatus = await _listener.RequestAccessAsync();
                Log($"[NOTIF] Windows UserNotificationListener AccessStatus: {accessStatus}");

                if (accessStatus == UserNotificationListenerAccessStatus.Allowed)
                {
                    _isAccessGranted = true;
                    _listener.NotificationChanged += Listener_NotificationChanged;
                    _isListening = true;
                    Log("[NOTIF] UserNotificationListener registered successfully.");
                    return true;
                }
            }
            catch (Exception ex)
            {
                Log($"[NOTIF] WinRT UserNotificationListener init failed: {ex.Message}");
            }
            return false;
        }

        private async void Listener_NotificationChanged(UserNotificationListener sender, UserNotificationChangedEventArgs args)
        {
            if (args.ChangeKind != UserNotificationChangedKind.Added) return;

            try
            {
                UserNotification notif = sender.GetNotification(args.UserNotificationId);
                if (notif == null) return;

                string appName = notif.AppInfo?.DisplayInfo?.DisplayName ?? "Windows";
                string title = "";
                string message = "";

                var binding = notif.Notification?.Visual?.GetBinding(KnownNotificationBindings.ToastGeneric);
                if (binding != null)
                {
                    var textElements = binding.GetTextElements();
                    int count = 0;
                    foreach (var txt in textElements)
                    {
                        if (count == 0) title = txt.Text;
                        else if (count == 1) message = txt.Text;
                        else message += " " + txt.Text;
                        count++;
                    }
                }

                if (string.IsNullOrWhiteSpace(title) && string.IsNullOrWhiteSpace(message)) return;

                var item = CategorizeNotification(appName, title, message);
                item.FromSystemToast = true;
                await Application.Current.Dispatcher.InvokeAsync(() =>
                {
                    DispatchNotification(item);
                });
            }
            catch (Exception ex)
            {
                Log($"[NOTIF] Listener error: {ex.Message}");
            }
        }

        // ==============================================
        // LAYER 2: UI AUTOMATION WINDOW OPENED INTERCEPTOR
        // ==============================================
        private void InitializeUIAutomation()
        {
            try
            {
                Automation.AddAutomationEventHandler(
                    WindowPattern.WindowOpenedEvent,
                    AutomationElement.RootElement,
                    TreeScope.Subtree,
                    OnWindowOpened);

                Log("[NOTIF] UI Automation WindowOpenedEvent registered successfully.");
            }
            catch (Exception ex)
            {
                Log($"[NOTIF] UI Automation register error: {ex.Message}");
            }
        }

        private void OnWindowOpened(object sender, AutomationEventArgs e)
        {
            try
            {
                if (sender is not AutomationElement element) return;

                int processId = element.Current.ProcessId;
                if (processId <= 0) return;

                using var proc = Process.GetProcessById(processId);
                string procName = proc.ProcessName.ToLowerInvariant();

                if (procName.Contains("discord"))
                {
                    HandleDiscordElement(element);
                }
                else if (procName.Contains("whatsapp"))
                {
                    HandleWhatsAppElement(element);
                }
                else if (procName.Contains("steam"))
                {
                    HandleSteamElement(element);
                }
                else if (procName.Contains("shellexperiencehost") || procName.Contains("explorer"))
                {
                    HandleWindowsToastElement(element);
                }
            }
            catch { }
        }

        private void HandleDiscordElement(AutomationElement element)
        {
            try
            {
                var rect = element.Current.BoundingRectangle;
                // Popups are generally smaller floating windows
                if (rect.Width > 40 && rect.Width < 500 && rect.Height > 30 && rect.Height < 350)
                {
                    string sender = "Discord";
                    string body = element.Current.Name ?? "";

                    var texts = element.FindAll(TreeScope.Descendants, new PropertyCondition(AutomationElement.ControlTypeProperty, ControlType.Text));
                    if (texts != null && texts.Count > 0)
                    {
                        if (texts.Count >= 2)
                        {
                            sender = texts[0].Current.Name;
                            body = texts[1].Current.Name;
                        }
                        else
                        {
                            body = texts[0].Current.Name;
                        }
                    }

                    if (string.IsNullOrWhiteSpace(body)) body = "Pesan baru diterima";

                    TriggerNotification(NotificationAppType.Discord, "Discord", sender, body, "#5865F2", "Discord", "Pesan Masuk");
                }
            }
            catch { }
        }

        private void HandleWhatsAppElement(AutomationElement element)
        {
            try
            {
                var rect = element.Current.BoundingRectangle;
                // Only small floating pop-ups; the main chat window is much bigger
                if (rect.Width > 40 && rect.Width < 500 && rect.Height > 30 && rect.Height < 350)
                {
                    string sender = "WhatsApp";
                    string body = element.Current.Name ?? "";

                    var texts = element.FindAll(TreeScope.Descendants, new PropertyCondition(AutomationElement.ControlTypeProperty, ControlType.Text));
                    if (texts != null && texts.Count > 0)
                    {
                        if (texts.Count >= 2) { sender = texts[0].Current.Name; body = texts[1].Current.Name; }
                        else body = texts[0].Current.Name;
                    }

                    if (string.IsNullOrWhiteSpace(body)) body = "Pesan baru diterima";
                    TriggerNotification(NotificationAppType.WhatsApp, "WhatsApp", sender, body, "#25D366", "WhatsApp", "Pesan Masuk");
                }
            }
            catch { }
        }

        private void HandleSteamElement(AutomationElement element)
        {
            try
            {
                var rect = element.Current.BoundingRectangle;
                string className = element.Current.ClassName ?? "";

                if (className.Contains("vgui") || className.Contains("Valve") || (rect.Width > 40 && rect.Width < 500 && rect.Height > 30 && rect.Height < 350))
                {
                    string title = "Steam";
                    string body = element.Current.Name ?? "";

                    var texts = element.FindAll(TreeScope.Descendants, new PropertyCondition(AutomationElement.ControlTypeProperty, ControlType.Text));
                    if (texts != null && texts.Count > 0)
                    {
                        if (texts.Count >= 2)
                        {
                            title = texts[0].Current.Name;
                            body = texts[1].Current.Name;
                        }
                        else
                        {
                            body = texts[0].Current.Name;
                        }
                    }

                    if (string.IsNullOrWhiteSpace(body)) body = "Notifikasi game atau obrolan";

                    TriggerNotification(NotificationAppType.Steam, "Steam", title, body, "#1A9FFF", "steam", "Notifikasi Game");
                }
            }
            catch { }
        }

        private void HandleWindowsToastElement(AutomationElement element)
        {
            try
            {
                var texts = element.FindAll(TreeScope.Descendants, new PropertyCondition(AutomationElement.ControlTypeProperty, ControlType.Text));
                if (texts != null && texts.Count >= 2)
                {
                    string t0 = texts[0].Current.Name;
                    string t1 = texts[1].Current.Name;
                    string combined = $"{t0} {t1}".ToLowerInvariant();

                    if (combined.Contains("discord"))
                    {
                        TriggerNotification(NotificationAppType.Discord, "Discord", t0, t1, "#5865F2", "Discord", "Pesan Masuk");
                    }
                    else if (combined.Contains("whatsapp"))
                    {
                        TriggerNotification(NotificationAppType.WhatsApp, "WhatsApp", t0, t1, "#25D366", "WhatsApp", "Pesan Masuk");
                    }
                    else if (combined.Contains("steam"))
                    {
                        TriggerNotification(NotificationAppType.Steam, "Steam", t0, t1, "#1A9FFF", "steam", "Notifikasi Game");
                    }
                }
            }
            catch { }
        }

        // ==============================================
        // LAYER 3: REALTIME WINDOW STATE & UNREAD POLLER
        // ==============================================
        [DllImport("user32.dll", SetLastError = true)]
        private static extern uint GetWindowThreadProcessId(IntPtr hWnd, out uint lpdwProcessId);

        [DllImport("user32.dll", CharSet = CharSet.Auto)]
        private static extern int GetWindowText(IntPtr hWnd, StringBuilder lpString, int nMaxCount);

        [DllImport("user32.dll", CharSet = CharSet.Auto)]
        private static extern int GetClassName(IntPtr hWnd, StringBuilder lpClassName, int nMaxCount);

        [DllImport("user32.dll")]
        [return: MarshalAs(UnmanagedType.Bool)]
        private static extern bool IsWindowVisible(IntPtr hWnd);

        [DllImport("user32.dll")]
        private static extern bool EnumWindows(EnumWindowsProc lpEnumFunc, IntPtr lParam);

        private delegate bool EnumWindowsProc(IntPtr hWnd, IntPtr lParam);

        private void PollWindowStates(object? state)
        {
            try
            {
                // A. Check Discord Process Windows & Title
                var discordProcs = Process.GetProcessesByName("Discord");
                if (discordProcs.Length > 0)
                {
                    CheckDiscordTitle();
                }

                // A2. WhatsApp desktop / WhatsApp Web: unread counter in the window title, e.g. "(2) WhatsApp"
                CheckWhatsAppTitle();

                // B. Check Steam Process Windows & Popup Handles
                var steamProcs = Process.GetProcessesByName("steam");
                if (steamProcs.Length > 0)
                {
                    CheckSteamWindows(steamProcs);
                }
            }
            catch { }
        }

        private void CheckDiscordTitle()
        {
            try
            {
                EnumWindows((hWnd, lParam) =>
                {
                    GetWindowThreadProcessId(hWnd, out uint pid);
                    try
                    {
                        var p = Process.GetProcessById((int)pid);
                        if (p.ProcessName.Equals("Discord", StringComparison.OrdinalIgnoreCase))
                        {
                            var sb = new StringBuilder(512);
                            GetWindowText(hWnd, sb, 512);
                            string title = sb.ToString();

                            if (!string.IsNullOrWhiteSpace(title) && title != _lastDiscordTitle)
                            {
                                // Match Discord unread pattern: "(1) Discord", "(2) #general | Server", "*Discord"
                                var match = Regex.Match(title, @"^\((\d+)\)\s*(.*)");
                                if (match.Success)
                                {
                                    int count = int.TryParse(match.Groups[1].Value, out int c) ? c : 1;
                                    string channel = match.Groups[2].Value.Trim();

                                    // If unread count increased
                                    if (count > _lastDiscordUnread)
                                    {
                                        string msg = string.IsNullOrWhiteSpace(channel) || channel.Equals("Discord", StringComparison.OrdinalIgnoreCase)
                                            ? $"{count} pesan baru masuk"
                                            : $"{channel}";

                                        TriggerNotification(NotificationAppType.Discord, "Discord", "Discord", msg, "#5865F2", "Discord", "Pesan Masuk");
                                    }
                                    _lastDiscordUnread = count;
                                }
                                else if (!title.StartsWith("(") && _lastDiscordUnread > 0)
                                {
                                    // User read the messages
                                    _lastDiscordUnread = 0;
                                }
                                _lastDiscordTitle = title;
                            }
                        }
                    }
                    catch { }
                    return true;
                }, IntPtr.Zero);
            }
            catch { }
        }

        private int _lastWhatsAppUnread = 0;

        private void CheckWhatsAppTitle()
        {
            try
            {
                int best = -1;
                string bestTitle = "";
                EnumWindows((hWnd, lParam) =>
                {
                    if (!IsWindowVisible(hWnd)) return true;
                    var sb = new StringBuilder(256);
                    GetWindowText(hWnd, sb, 256);
                    string title = sb.ToString();
                    if (!title.Contains("WhatsApp", StringComparison.OrdinalIgnoreCase)) return true;

                    var m = Regex.Match(title, @"^\((\d+)\)");
                    int count = m.Success && int.TryParse(m.Groups[1].Value, out int c) ? c : 0;
                    if (count > best) { best = count; bestTitle = title; }
                    return true;
                }, IntPtr.Zero);

                if (best < 0) { _lastWhatsAppUnread = 0; return; } // no WhatsApp window open

                if (best > _lastWhatsAppUnread)
                {
                    string msg = best == 1 ? "1 pesan baru masuk" : $"{best} pesan baru masuk";
                    TriggerNotification(NotificationAppType.WhatsApp, "WhatsApp", "WhatsApp", msg, "#25D366", "WhatsApp", "Pesan Masuk");
                }
                _lastWhatsAppUnread = best;
            }
            catch { }
        }

        private void CheckSteamWindows(Process[] steamProcs)
        {
            try
            {
                EnumWindows((hWnd, lParam) =>
                {
                    GetWindowThreadProcessId(hWnd, out uint pid);
                    try
                    {
                        var p = Process.GetProcessById((int)pid);
                        if (p.ProcessName.Equals("steam", StringComparison.OrdinalIgnoreCase) || p.ProcessName.Equals("steamwebhelper", StringComparison.OrdinalIgnoreCase))
                        {
                            if (!IsWindowVisible(hWnd)) return true;

                            var sbClass = new StringBuilder(256);
                            GetClassName(hWnd, sbClass, 256);
                            string cls = sbClass.ToString();

                            var sbTitle = new StringBuilder(256);
                            GetWindowText(hWnd, sbTitle, 256);
                            string title = sbTitle.ToString();

                            // Steam popup notification window or chat notification
                            if (cls.Contains("vguiPopupWindow", StringComparison.OrdinalIgnoreCase) || title.Contains("Chat with", StringComparison.OrdinalIgnoreCase) || title.Contains("New Message", StringComparison.OrdinalIgnoreCase))
                            {
                                if (!_knownSteamWindows.Contains(hWnd))
                                {
                                    _knownSteamWindows.Add(hWnd);
                                    string sender = "Steam";
                                    string msg = title;

                                    if (title.Contains("Chat with", StringComparison.OrdinalIgnoreCase))
                                    {
                                        sender = title.Replace("Chat with", "").Trim();
                                        msg = "Pesan obrolan baru";
                                    }
                                    else if (string.IsNullOrWhiteSpace(msg))
                                    {
                                        msg = "Undangan game atau notifikasi teman";
                                    }

                                    TriggerNotification(NotificationAppType.Steam, "Steam", sender, msg, "#1A9FFF", "steam", "Notifikasi Game");
                                }
                            }
                        }
                    }
                    catch { }
                    return true;
                }, IntPtr.Zero);
            }
            catch { }
        }

        public void ShowNotification(string title, string message, string appName = "Dynamic Island", string accent = "#0A84FF")
        {
            TriggerNotification(NotificationAppType.General, appName, title, message, accent, "", "Health & Reminder");
        }

        // ==============================================
        // DISPATCHER & DEDUPLICATION
        // ==============================================
        /// <summary>Focus / Do Not Disturb: when true, banners are not shown.</summary>
        public static bool SuppressAll { get; set; }

        public void TriggerNotification(NotificationAppType appType, string appName, string title, string message, string accent, string targetProc, string subtitle)
        {
            if (SuppressAll) return;
            // Debounce same notification within 3 seconds
            string key = $"{appType}:{title}:{message}";
            if (key == _lastNotificationKey && (DateTime.Now - _lastNotificationTime).TotalSeconds < 3.0)
            {
                return;
            }

            _lastNotificationKey = key;
            _lastNotificationTime = DateTime.Now;

            var item = new NotificationItem
            {
                AppType = appType,
                AppName = appName,
                Title = string.IsNullOrWhiteSpace(title) ? appName : title,
                Message = string.IsNullOrWhiteSpace(message) ? "Notifikasi baru" : message,
                AccentColor = accent,
                TargetProcess = targetProc,
                Subtitle = subtitle,
                Timestamp = DateTime.Now
            };

            Log($"[NOTIF-DISPATCH] {appName} | {title} | {message}");

            Application.Current?.Dispatcher?.InvokeAsync(() =>
            {
                DispatchNotification(item);
            });
        }

        private readonly Dictionary<NotificationAppType, DateTime> _lastToastByApp = new();

        private void DispatchNotification(NotificationItem item)
        {
            if (SuppressAll) return;

            // The same message can arrive twice (Windows toast + window/title detection).
            // The toast has the real sender and text, so a title/window guess right after it is dropped.
            if (item.FromSystemToast)
            {
                _lastToastByApp[item.AppType] = DateTime.Now;
            }
            else if (item.AppType != NotificationAppType.General
                     && _lastToastByApp.TryGetValue(item.AppType, out var t)
                     && (DateTime.Now - t).TotalSeconds < 5)
            {
                return;
            }

            NotificationReceived?.Invoke(item);
        }

        public NotificationItem CategorizeNotification(string appName, string title, string message)
        {
            var item = new NotificationItem
            {
                AppName = appName,
                Title = title,
                Message = message,
                Timestamp = DateTime.Now
            };

            string lowerApp = appName.ToLowerInvariant();
            string lowerTitle = title.ToLowerInvariant();

            if (lowerApp.Contains("discord") || lowerTitle.Contains("discord"))
            {
                item.AppType = NotificationAppType.Discord;
                item.AppName = "Discord";
                item.AccentColor = "#5865F2";
                item.TargetProcess = "Discord";
                item.Subtitle = "Pesan Masuk";
            }
            else if (lowerApp.Contains("whatsapp") || lowerTitle.Contains("whatsapp"))
            {
                item.AppType = NotificationAppType.WhatsApp;
                item.AppName = "WhatsApp";
                item.AccentColor = "#25D366";
                item.TargetProcess = "WhatsApp";
                item.Subtitle = "Pesan Masuk";
            }
            else if (lowerApp.Contains("steam") || lowerTitle.Contains("steam"))
            {
                item.AppType = NotificationAppType.Steam;
                item.AppName = "Steam";
                item.AccentColor = "#1A9FFF";
                item.TargetProcess = "steam";
                item.Subtitle = "Notifikasi Game";
            }
            else
            {
                item.AppType = NotificationAppType.General;
                item.AccentColor = "#0A84FF";
                item.Subtitle = appName;
            }

            return item;
        }

        // ==============================================
        // TEST EMULATORS & SIMULATION
        // ==============================================
        public void SimulateDiscordNotification(string sender = "Viper", string message = "hey bro login CS2 let's play premier!")
        {
            TriggerNotification(NotificationAppType.Discord, "Discord", sender, message, "#5865F2", "Discord", "Pesan Masuk");
        }

        public void SimulateWhatsAppNotification(string sender = "Mama", string message = "Nak, jangan lupa makan ya 😊")
        {
            TriggerNotification(NotificationAppType.WhatsApp, "WhatsApp", sender, message, "#25D366", "WhatsApp", "Pesan Masuk");
        }

        public void SimulateSteamNotification(string friendName = "Gabe_Newell", string message = "mengundang Anda untuk bermain Counter-Strike 2")
        {
            TriggerNotification(NotificationAppType.Steam, "Steam", friendName, message, "#1A9FFF", "steam", "Undangan Game");
        }

        public void OpenNotificationSettings()
        {
            try
            {
                Process.Start(new ProcessStartInfo("ms-settings:privacy-notifications") { UseShellExecute = true });
            }
            catch { }
        }
    }
}
