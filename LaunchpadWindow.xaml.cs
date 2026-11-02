// Language: C#
// Filename: LaunchpadWindow.xaml.cs
// Runs on: Windows 10 / Windows 11 (.NET 9)
// Targets: DynamicIsland Launchpad — app grid, smart search (apps + files + web)

using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Runtime.InteropServices;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Threading;
using Microsoft.Win32;

namespace DynamicIsland
{
    public partial class LaunchpadWindow : Window
    {
        private bool _deactivateEnabled = false;
        private DispatcherTimer? _deactivateArm;
        private DispatcherTimer? _searchDebounce;
        private string _lastQuery = "";

        // ── Cached Start Menu app list ──
        private List<AppEntry> _allApps = new();
        private bool _appsCached = false;

        public LaunchpadWindow()
        {
            InitializeComponent();

            // 400ms deactivate arm — prevents instant hide when focus bounces from dock button
                        Deactivated += (s, e) => { if (_deactivateEnabled) { Hide(); MainWindow.TrimAppMemory(); } };
            Activated += (s, e) =>
            {
                _deactivateEnabled = false;
                _deactivateArm?.Stop();
                _deactivateArm = new DispatcherTimer { Interval = TimeSpan.FromMilliseconds(400) };
                _deactivateArm.Tick += (ts, te) => { _deactivateEnabled = true; _deactivateArm?.Stop(); };
                _deactivateArm.Start();
            };

            KeyDown += LaunchpadWindow_KeyDown;
            Loaded += LaunchpadWindow_Loaded;
        }

        // ─────────────────────────────────────────────────────────────────
        #region Lifecycle
        private void LaunchpadWindow_Loaded(object sender, RoutedEventArgs e)
        {
            UpdateDateDisplay();
            TxtUserProfileName.Text = Environment.UserName;

            AttachLaunchpadAppMenus();

            // Pre-cache apps in background so first search is instant
            Task.Run(CacheApps);
        }

        private void AttachLaunchpadAppMenus()
        {
            try
            {
                foreach (UIElement child in PanelApps.Children)
                {
                    if (child is Button btn && btn.Tag is string appName)
                    {
                        var cm = new ContextMenu();
                        var header = new MenuItem { Header = $"📌 {appName}", IsEnabled = false, FontWeight = FontWeights.Bold };
                        cm.Items.Add(header);
                        cm.Items.Add(new Separator());

                        var pinItem = new MenuItem { Header = "➕  Tambah ke Dock Bawah" };
                        string capturedName = appName;
                        pinItem.Click += (s, e) =>
                        {
                            var app = _allApps.FirstOrDefault(a => a.Name.Contains(capturedName, StringComparison.OrdinalIgnoreCase))
                                   ?? GetPinnedApps().FirstOrDefault(a => a.Name.Contains(capturedName, StringComparison.OrdinalIgnoreCase));
                            if (app != null)
                            {
                                DockManager.Instance.AddAppToDock(app.Name, app.ExePath);
                            }
                        };
                        cm.Items.Add(pinItem);
                        btn.ContextMenu = cm;
                    }
                }
            }
            catch { }
        }

        public void UpdatePosition(FloatingDockWindow? dock = null)
        {
            double sw = SystemParameters.PrimaryScreenWidth;
            double sh = SystemParameters.PrimaryScreenHeight;
            Width = 480; Height = 560;
            Left = (sw - Width) / 2.0;
            Top = dock != null && dock.IsVisible
                ? dock.Top - Height - 8
                : sh - Height - 90;
        }

        public void UpdateDateDisplay()
        {
            var now = DateTime.Now;
            TxtLaunchCalMonth.Text = now.ToString("MMM", new System.Globalization.CultureInfo("id-ID")).ToUpper();
            TxtLaunchCalDay.Text = now.Day.ToString();
        }

        // Show the window: reset search state then show
        public new void Show()
        {
            ClearSearch();
            base.Show();
            Dispatcher.BeginInvoke(() => TxtSearchBox.Focus(), DispatcherPriority.Input);
        }
        #endregion

        // ─────────────────────────────────────────────────────────────────
        #region Keyboard
        private void LaunchpadWindow_KeyDown(object sender, KeyEventArgs e)
        {
            if (e.Key == Key.Escape)
            {
                if (!string.IsNullOrEmpty(TxtSearchBox.Text))
                    ClearSearch();
                else
                    Hide();
                e.Handled = true;
            }
        }

        private void TxtSearchBox_KeyDown(object sender, KeyEventArgs e)
        {
            if (e.Key == Key.Enter && !string.IsNullOrWhiteSpace(TxtSearchBox.Text))
            {
                // Enter → top result OR web search
                SearchWeb(TxtSearchBox.Text.Trim());
                e.Handled = true;
            }
        }
        #endregion

        // ─────────────────────────────────────────────────────────────────
        #region Search

        private void TxtSearchBox_TextChanged(object sender, TextChangedEventArgs e)
        {
            string q = TxtSearchBox.Text;
            TxtSearchPlaceholder.Visibility = string.IsNullOrEmpty(q) ? Visibility.Visible : Visibility.Collapsed;
            BtnClearSearch.Visibility = string.IsNullOrEmpty(q) ? Visibility.Collapsed : Visibility.Visible;

            if (string.IsNullOrWhiteSpace(q))
            {
                ShowAppGrid();
                return;
            }

            // Debounce 200ms
            _searchDebounce?.Stop();
            _searchDebounce = new DispatcherTimer { Interval = TimeSpan.FromMilliseconds(200) };
            _searchDebounce.Tick += (ts, te) =>
            {
                _searchDebounce?.Stop();
                RunSearch(q.Trim());
            };
            _searchDebounce.Start();
        }

        private void BtnClearSearch_Click(object sender, RoutedEventArgs e) => ClearSearch();

        private void ClearSearch()
        {
            TxtSearchBox.Text = "";
            ShowAppGrid();
        }

        private void ShowAppGrid()
        {
            ScrollApps.Visibility = Visibility.Visible;
            PanelSearchView.Visibility = Visibility.Collapsed;
        }

        private void ShowSearchPanel()
        {
            ScrollApps.Visibility = Visibility.Collapsed;
            PanelSearchView.Visibility = Visibility.Visible;
        }

        private async void RunSearch(string q)
        {
            if (q == _lastQuery) return;
            _lastQuery = q;

            TxtWebSearchLabel.Text = $"Cari di Google: \"{q}\"";
            ShowSearchPanel();
            PanelSearchResults.Children.Clear();

            // Section: Apps (instant, from pinned + cached list)
            var appMatches = MatchApps(q);
            if (appMatches.Count > 0)
            {
                AddSectionHeader("Aplikasi");
                foreach (var app in appMatches.Take(6))
                    AddResultRow(app.Icon, app.Name, app.Description, () => { Hide(); LaunchEntry(app); }, app);
            }

            // Section: Files (async, search index via Everything-like approach)
            AddSectionHeader("File");
            var filePlaceholder = AddLoadingRow("Mencari file...");

            var files = await Task.Run(() => SearchFiles(q));
            Dispatcher.Invoke(() =>
            {
                PanelSearchResults.Children.Remove(filePlaceholder);
                if (files.Count > 0)
                {
                    foreach (var f in files.Take(5))
                    {
                        string path = f;
                        string name = Path.GetFileName(f);
                        string dir  = Path.GetDirectoryName(f) ?? "";
                        string ext  = Path.GetExtension(f).ToLowerInvariant();
                        string icon = ext switch
                        {
                            ".pdf"  => "📕",
                            ".docx" or ".doc" => "📝",
                            ".xlsx" or ".xls" => "📊",
                            ".pptx" or ".ppt" => "📊",
                            ".png" or ".jpg" or ".jpeg" or ".gif" or ".webp" => "🖼️",
                            ".mp4" or ".mkv" or ".mov" or ".avi" => "🎬",
                            ".mp3" or ".flac" or ".wav" or ".m4a" => "🎵",
                            ".zip" or ".rar" or ".7z" => "📦",
                            _ => "📄"
                        };
                        AddResultRow(icon, name, dir, () => { Hide(); OpenFile(path); });
                    }
                }
                else
                {
                    var tb = new TextBlock
                    {
                        Text = "Tidak ada file ditemukan",
                        Foreground = new SolidColorBrush(Color.FromArgb(0x80, 0xFF, 0xFF, 0xFF)),
                        FontSize = 11,
                        Margin = new Thickness(8, 0, 0, 4)
                    };
                    PanelSearchResults.Children.Add(tb);
                }
            });
        }

        // ── App matching ──
        private List<AppEntry> MatchApps(string q)
        {
            // Always check pinned dock apps first
            var pinned = GetPinnedApps();
            var fromCache = _allApps;
            return pinned.Concat(fromCache)
                .Where(a => a.Name.Contains(q, StringComparison.OrdinalIgnoreCase))
                .DistinctBy(a => a.Name)
                .OrderBy(a => a.Name.StartsWith(q, StringComparison.OrdinalIgnoreCase) ? 0 : 1)
                .ToList();
        }

        private List<AppEntry> GetPinnedApps() => new()
        {
            new("Apple Music",    "shell:AppsFolder\\AppleInc.AppleMusicWin_nzyj5cx40ttqa!App", "🎵", "Apple Music"),
            new("File Explorer",  "explorer.exe",   "📁", "Jelajahi file"),
            new("Spotify",        "spotify:",        "🎵", "Pemutar musik"),
            new("Microsoft Edge", "msedge.exe",      "🌐", "Browser"),
            new("Google Chrome",  "chrome.exe",      "🌐", "Browser"),
            new("Discord",        "discord:",        "💬", "Chat"),
            new("WhatsApp",       "whatsapp:",       "💬", "Pesan"),
            new("Notepad",        "notepad.exe",     "📝", "Catatan"),
            new("Pengaturan",     "ms-settings:",    "⚙️",  "Windows Settings"),
            new("Antigravity",    "",                "🚀", "IDE"),
            new("Kalender",       "outlookcal:",     "📅", "Jadwal"),
        };

        // ── File search — documents & media only, NO exe/installer ──
        private static List<string> SearchFiles(string q)
        {
            var results = new List<string>();
            try
            {
                var searchRoots = new[]
                {
                    Environment.GetFolderPath(Environment.SpecialFolder.Desktop),
                    Environment.GetFolderPath(Environment.SpecialFolder.MyDocuments),
                    Environment.GetFolderPath(Environment.SpecialFolder.MyPictures),
                    Environment.GetFolderPath(Environment.SpecialFolder.MyVideos),
                    Environment.GetFolderPath(Environment.SpecialFolder.MyMusic),
                    Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.UserProfile), "Downloads"),
                };

                // Documents, media, data — intentionally NO .exe / .msi / .zip installers
                var ext = new HashSet<string>(StringComparer.OrdinalIgnoreCase)
                {
                    ".pdf", ".docx", ".doc", ".xlsx", ".xls", ".pptx", ".ppt",
                    ".txt", ".md", ".rtf", ".csv",
                    ".png", ".jpg", ".jpeg", ".gif", ".webp", ".svg",
                    ".mp4", ".mkv", ".mov", ".avi",
                    ".mp3", ".flac", ".wav", ".m4a",
                    ".zip", ".rar", ".7z",
                    ".json", ".xml", ".html",
                };

                foreach (var root in searchRoots)
                {
                    if (!Directory.Exists(root)) continue;
                    try
                    {
                        var found = Directory.EnumerateFiles(root, $"*{q}*",
                            new EnumerationOptions
                            {
                                RecurseSubdirectories = true,
                                MaxRecursionDepth = 4,
                                IgnoreInaccessible = true
                            })
                            .Where(f => ext.Contains(Path.GetExtension(f)))
                            .Take(8)
                            .ToList();
                        results.AddRange(found);
                        if (results.Count >= 10) break;
                    }
                    catch { }
                }
            }
            catch { }
            return results.Take(5).ToList();
        }

        // ── App cache from shell:AppsFolder (Instant COM) + Registry + Start Menu ──
        private void CacheApps()
        {
            var list = new List<AppEntry>();

            // ── 1. shell:AppsFolder (ALL installed Windows apps: Store/UWP, Apple Music, Win32) ──
            try
            {
                Type? shellType = Type.GetTypeFromProgID("Shell.Application");
                if (shellType != null)
                {
                    dynamic? shell = Activator.CreateInstance(shellType);
                    dynamic? folder = shell?.NameSpace("shell:AppsFolder");
                    if (folder != null)
                    {
                        foreach (dynamic item in folder.Items())
                        {
                            try
                            {
                                string name = (string)item.Name;
                                string path = (string)item.Path;
                                if (string.IsNullOrWhiteSpace(name)) continue;

                                string launchTarget = path.Contains("!") || !path.EndsWith(".exe", StringComparison.OrdinalIgnoreCase)
                                    ? $"shell:AppsFolder\\{path}"
                                    : path;

                                string icon = "📱";
                                if (name.Contains("Music", StringComparison.OrdinalIgnoreCase) || name.Contains("Musik", StringComparison.OrdinalIgnoreCase)) icon = "🎵";
                                else if (name.Contains("Chrome", StringComparison.OrdinalIgnoreCase) || name.Contains("Edge", StringComparison.OrdinalIgnoreCase)) icon = "🌐";
                                else if (name.Contains("Store", StringComparison.OrdinalIgnoreCase)) icon = "🛍️";
                                else if (name.Contains("Setting", StringComparison.OrdinalIgnoreCase) || name.Contains("Pengaturan", StringComparison.OrdinalIgnoreCase)) icon = "⚙️";
                                else if (name.Contains("Terminal", StringComparison.OrdinalIgnoreCase) || name.Contains("PowerShell", StringComparison.OrdinalIgnoreCase)) icon = "💻";
                                else if (name.Contains("Spotify", StringComparison.OrdinalIgnoreCase)) icon = "🎧";
                                else if (name.Contains("Discord", StringComparison.OrdinalIgnoreCase) || name.Contains("WhatsApp", StringComparison.OrdinalIgnoreCase)) icon = "💬";

                                if (!list.Any(a => a.Name.Equals(name, StringComparison.OrdinalIgnoreCase)))
                                {
                                    list.Add(new AppEntry(name.Trim(), launchTarget, icon, "Aplikasi Terinstall"));
                                }
                            }
                            catch { }
                        }
                    }
                }
            }
            catch { }

            // ── 2. Registry Uninstall keys ──
            var uninstallPaths = new[]
            {
                @"SOFTWARE\Microsoft\Windows\CurrentVersion\Uninstall",
                @"SOFTWARE\WOW6432Node\Microsoft\Windows\CurrentVersion\Uninstall",
            };
            foreach (var hive in new[] { Registry.LocalMachine, Registry.CurrentUser })
            {
                foreach (var regPath in uninstallPaths)
                {
                    try
                    {
                        using var key = hive.OpenSubKey(regPath);
                        if (key == null) continue;
                        foreach (var subName in key.GetSubKeyNames())
                        {
                            try
                            {
                                using var sub = key.OpenSubKey(subName);
                                if (sub == null) continue;
                                string? name    = sub.GetValue("DisplayName") as string;
                                string? exePath = sub.GetValue("DisplayIcon") as string;
                                if (string.IsNullOrWhiteSpace(name)) continue;
                                if (name.StartsWith("Microsoft Visual C++", StringComparison.OrdinalIgnoreCase)) continue;
                                if (name.StartsWith("Microsoft .NET", StringComparison.OrdinalIgnoreCase)) continue;
                                if (!string.IsNullOrEmpty(exePath))
                                    exePath = exePath.Split(',')[0].Trim().Trim('"');

                                if (!list.Any(a => a.Name.Equals(name, StringComparison.OrdinalIgnoreCase)))
                                    list.Add(new AppEntry(name.Trim(), exePath ?? "", "🖥️", "Terinstall"));
                            }
                            catch { }
                        }
                    }
                    catch { }
                }
            }

            // ── 3. Start Menu shortcuts ──
            foreach (var dir in new[]
            {
                Environment.GetFolderPath(Environment.SpecialFolder.CommonStartMenu),
                Environment.GetFolderPath(Environment.SpecialFolder.StartMenu)
            })
            {
                if (!Directory.Exists(dir)) continue;
                try
                {
                    foreach (var lnk in Directory.EnumerateFiles(dir, "*.lnk", SearchOption.AllDirectories))
                    {
                        string name = Path.GetFileNameWithoutExtension(lnk);
                        if (!list.Any(a => a.Name.Equals(name, StringComparison.OrdinalIgnoreCase)))
                            list.Add(new AppEntry(name, lnk, "🖥️", "Aplikasi"));
                    }
                }
                catch { }
            }

            _allApps = list.DistinctBy(a => a.Name.ToLowerInvariant()).OrderBy(a => a.Name).ToList();
            _appsCached = true;
        }

        // ── Launch helpers ──
        private void LaunchEntry(AppEntry app)
        {
            if (string.IsNullOrEmpty(app.ExePath)) return;
            if (app.ExePath.EndsWith(".lnk"))
                DockManager.LaunchApp(app.ExePath);
            else
                DockManager.LaunchApp(app.ExePath);
        }

        private static void OpenFile(string path)
        {
            try
            {
                Process.Start(new ProcessStartInfo(path) { UseShellExecute = true });
            }
            catch { }
        }

        private void SearchWeb(string q)
        {
            Hide();
            DockManager.LaunchApp($"https://www.google.com/search?q={Uri.EscapeDataString(q)}");
        }

        private void BtnSearchWeb_Click(object sender, RoutedEventArgs e)
            => SearchWeb(TxtSearchBox.Text.Trim());
        #endregion

        // ─────────────────────────────────────────────────────────────────
        #region UI helpers

        private void AddSectionHeader(string title)
        {
            var tb = new TextBlock
            {
                Text = title.ToUpperInvariant(),
                Foreground = new SolidColorBrush(Color.FromArgb(0x99, 0xFF, 0xFF, 0xFF)),
                FontSize = 9.5,
                FontWeight = FontWeights.Bold,
                Margin = new Thickness(4, 8, 0, 3)
            };
            PanelSearchResults.Children.Add(tb);
        }

        private void AddResultRow(string icon, string title, string subtitle, Action onClick, AppEntry? app = null)
        {
            var btn = new Button { Style = (Style)Resources["SearchResultBtn"] };

            if (app != null)
            {
                var cm = new ContextMenu();
                var header = new MenuItem { Header = $"📌 {app.Name}", IsEnabled = false, FontWeight = FontWeights.Bold };
                cm.Items.Add(header);
                cm.Items.Add(new Separator());

                var pinItem = new MenuItem { Header = "➕  Tambah ke Dock Bawah" };
                pinItem.Click += (s, e) =>
                {
                    DockManager.Instance.AddAppToDock(app.Name, app.ExePath);
                };
                cm.Items.Add(pinItem);
                btn.ContextMenu = cm;
            }

            var grid = new Grid();
            grid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(32) });
            grid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });

            var iconTb = new TextBlock
            {
                Text = icon, FontSize = 17,
                VerticalAlignment = VerticalAlignment.Center,
                HorizontalAlignment = HorizontalAlignment.Center
            };
            Grid.SetColumn(iconTb, 0);
            grid.Children.Add(iconTb);

            var sp = new StackPanel { VerticalAlignment = VerticalAlignment.Center, Margin = new Thickness(8, 0, 0, 0) };
            sp.Children.Add(new TextBlock
            {
                Text = title, Foreground = Brushes.White,
                FontSize = 12, FontWeight = FontWeights.SemiBold,
                TextTrimming = TextTrimming.CharacterEllipsis
            });
            if (!string.IsNullOrEmpty(subtitle))
            {
                sp.Children.Add(new TextBlock
                {
                    Text = subtitle,
                    Foreground = new SolidColorBrush(Color.FromArgb(0x88, 0xFF, 0xFF, 0xFF)),
                    FontSize = 10, TextTrimming = TextTrimming.CharacterEllipsis
                });
            }
            Grid.SetColumn(sp, 1);
            grid.Children.Add(sp);

            btn.Content = grid;
            btn.Click += (s, e) => onClick();
            PanelSearchResults.Children.Add(btn);
        }

        private Border AddLoadingRow(string text)
        {
            var border = new Border { Margin = new Thickness(4, 2, 0, 2) };
            border.Child = new TextBlock
            {
                Text = text,
                Foreground = new SolidColorBrush(Color.FromArgb(0x70, 0xFF, 0xFF, 0xFF)),
                FontSize = 11, FontStyle = FontStyles.Italic
            };
            PanelSearchResults.Children.Add(border);
            return border;
        }
        #endregion

        // ─────────────────────────────────────────────────────────────────
        #region Power menu
        private void BtnPower_Click(object sender, RoutedEventArgs e)
        {
            var cm = new ContextMenu();
            AddPowerItem(cm, "Kunci Layar", () => { Hide(); NativeMethods.LockWorkStation(); });
            AddPowerItem(cm, "Mode Tidur",  () => { Hide(); NativeMethods.SetSuspendState(false, true, false); });
            AddPowerItem(cm, "Mulai Ulang", () => { Hide(); Process.Start("shutdown", "/r /t 0"); });
            AddPowerItem(cm, "Matikan",     () => { Hide(); Process.Start("shutdown", "/s /t 0"); });
            cm.PlacementTarget = BtnPower;
            cm.Placement = System.Windows.Controls.Primitives.PlacementMode.Top;
            cm.IsOpen = true;
        }

        private static void AddPowerItem(ContextMenu cm, string header, Action action)
        {
            var item = new MenuItem { Header = header };
            item.Click += (s, e) => action();
            cm.Items.Add(item);
        }
        #endregion

        // ─────────────────────────────────────────────────────────────────
        #region App launchers
        private void LaunchExplorer_Click(object sender, RoutedEventArgs e)  { Hide(); DockManager.LaunchApp("explorer.exe"); }
        private void LaunchEdge_Click(object sender, RoutedEventArgs e)       { Hide(); DockManager.LaunchApp("msedge.exe", "https://bing.com"); }
        private void LaunchChrome_Click(object sender, RoutedEventArgs e)     { Hide(); DockManager.LaunchApp("chrome.exe", "https://google.com"); }
        private void LaunchMail_Click(object sender, RoutedEventArgs e)       { Hide(); DockManager.LaunchApp("mailto:"); }
        private void LaunchWhatsApp_Click(object sender, RoutedEventArgs e)   { Hide(); DockManager.LaunchApp("whatsapp:", "https://web.whatsapp.com"); }
        private void LaunchMusic_Click(object sender, RoutedEventArgs e)      { Hide(); DockManager.LaunchApp("mswindowsmusic:", "https://music.apple.com"); }
        private void LaunchNotes_Click(object sender, RoutedEventArgs e)      { Hide(); DockManager.LaunchApp("notepad.exe"); }
        private void LaunchCalendar_Click(object sender, RoutedEventArgs e)   { Hide(); DockManager.LaunchApp("outlookcal:"); }

        private void LaunchSpotify_Click(object sender, RoutedEventArgs e)
        {
            Hide();
            string p = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData), "Spotify", "Spotify.exe");
            DockManager.LaunchApp(File.Exists(p) ? p : "spotify:", "https://open.spotify.com");
        }

        private void LaunchDiscord_Click(object sender, RoutedEventArgs e)
        {
            Hide();
            string p = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "Discord", "Update.exe");
            DockManager.LaunchApp(File.Exists(p) ? p : "discord:", "https://discord.com");
        }

        private void LaunchAntigravity_Click(object sender, RoutedEventArgs e)
        {
            Hide();
            string p = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "Programs", "antigravity", "Antigravity.exe");
            DockManager.LaunchApp(File.Exists(p) ? p : "antigravity");
        }

        private void LaunchSettings_Click(object sender, RoutedEventArgs e)
        {
            Hide();
            try { if (Application.Current.MainWindow is MainWindow mw) mw.OpenSettingsWindow(); else DockManager.LaunchApp("ms-settings:"); }
            catch { DockManager.LaunchApp("ms-settings:"); }
        }
        #endregion
    }

    // ── Data model ──
    internal record AppEntry(string Name, string ExePath, string Icon, string Description);
}
