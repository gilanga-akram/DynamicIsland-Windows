// Language: C#
// Filename: ShelfManager.cs
// Runs on: Windows 10 / Windows 11 (.NET 9.0-windows10.0.19041.0)
// Targets: Dynamic Island Shelf & AirDrop (Drop-Zone, Local Web Drop, Nearby Share, Drag-out)

using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Diagnostics;
using System.IO;
using System.Net;
using System.Net.Sockets;
using System.Runtime.CompilerServices;
using System.Text;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Data;
using System.Globalization;
using System.Windows.Media;
using System.Windows.Media.Imaging;

namespace DynamicIsland
{
    public class ShelfItem : INotifyPropertyChanged
    {
        public string Id { get; set; } = Guid.NewGuid().ToString();
        public string FullPath { get; set; } = "";
        public string FileName { get; set; } = "";
        public string FileSizeText { get; set; } = "";
        public long FileSizeBytes { get; set; } = 0;
        public string Extension { get; set; } = "";
        public string Category { get; set; } = "File";
        public string CategoryColor { get; set; } = "#0A84FF";
        public string GlyphIcon { get; set; } = "📄";
        public ImageSource? Thumbnail { get; set; }
        public bool IsImage { get; set; } = false;
        public DateTime AddedTime { get; set; } = DateTime.Now;

        public event PropertyChangedEventHandler? PropertyChanged;
        protected void OnPropertyChanged([CallerMemberName] string? name = null) =>
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(name));
    }

    public class ShelfManager : INotifyPropertyChanged
    {
        public ObservableCollection<ShelfItem> Items { get; } = new();

        private bool _isServerRunning = false;
        private HttpListener? _httpListener;
        private string _localIp = "127.0.0.1";
        private int _serverPort = 5055;
        private string _airDropUrl = "";
        private bool _isDropTargetActive = false;

        public bool IsServerRunning
        {
            get => _isServerRunning;
            private set { _isServerRunning = value; OnPropertyChanged(); }
        }

        public string AirDropUrl
        {
            get => _airDropUrl;
            private set { _airDropUrl = value; OnPropertyChanged(); }
        }

        public bool IsDropTargetActive
        {
            get => _isDropTargetActive;
            set { _isDropTargetActive = value; OnPropertyChanged(); }
        }

        public int ItemCount => Items.Count;

        public event Action<string>? NotificationRequested;
        public event PropertyChangedEventHandler? PropertyChanged;

        public ShelfManager()
        {
            _localIp = GetLocalIpAddress();
            AirDropUrl = $"http://{_localIp}:{_serverPort}/";
        }

        protected void OnPropertyChanged([CallerMemberName] string? name = null) =>
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(name));

        // ==============================================
        // FILE MANAGEMENT & DROP HANDLING
        // ==============================================
        public void AddFiles(IEnumerable<string> filePaths)
        {
            int addedCount = 0;
            foreach (var path in filePaths)
            {
                if (AddSingleFile(path))
                {
                    addedCount++;
                }
            }

            if (addedCount > 0)
            {
                OnPropertyChanged(nameof(ItemCount));
                NotificationRequested?.Invoke($"📥 {addedCount} berkas ditambahkan ke Shelf");
                EnsureServerStarted();
            }
        }

        public bool AddSingleFile(string path)
        {
            try
            {
                if (!File.Exists(path) && !Directory.Exists(path)) return false;

                // Avoid duplicates
                foreach (var item in Items)
                {
                    if (item.FullPath.Equals(path, StringComparison.OrdinalIgnoreCase))
                    {
                        return false;
                    }
                }

                bool isDir = Directory.Exists(path);
                var fi = isDir ? null : new FileInfo(path);
                string ext = isDir ? "Folder" : (fi?.Extension.ToUpperInvariant() ?? "");
                string name = isDir ? new DirectoryInfo(path).Name : (fi?.Name ?? "");
                long bytes = isDir ? 0 : (fi?.Length ?? 0);

                var (cat, color, glyph, isImg) = CategorizeFile(ext, isDir);
                ImageSource? thumb = null;

                if (isImg && !isDir)
                {
                    thumb = LoadThumbnail(path);
                }

                var shelfItem = new ShelfItem
                {
                    FullPath = path,
                    FileName = name,
                    Extension = ext,
                    FileSizeBytes = bytes,
                    FileSizeText = isDir ? "Folder Berkas" : FormatFileSize(bytes),
                    Category = cat,
                    CategoryColor = color,
                    GlyphIcon = glyph,
                    IsImage = isImg,
                    Thumbnail = thumb,
                    AddedTime = DateTime.Now
                };

                Items.Insert(0, shelfItem);
                return true;
            }
            catch { return false; }
        }

        public void RemoveItem(ShelfItem item)
        {
            if (Items.Remove(item))
            {
                OnPropertyChanged(nameof(ItemCount));
            }
        }

        public void ClearAll()
        {
            Items.Clear();
            OnPropertyChanged(nameof(ItemCount));
            NotificationRequested?.Invoke("🗑️ Shelf dikosongkan");
        }

        public void OpenFile(ShelfItem item)
        {
            try
            {
                Process.Start(new ProcessStartInfo(item.FullPath) { UseShellExecute = true });
            }
            catch { }
        }

        public void ShowInExplorer(ShelfItem item)
        {
            try
            {
                Process.Start(new ProcessStartInfo("explorer.exe", $"/select,\"{item.FullPath}\"") { UseShellExecute = true });
            }
            catch { }
        }

        public void CopyToClipboard(ShelfItem item)
        {
            try
            {
                var paths = new System.Collections.Specialized.StringCollection { item.FullPath };
                Clipboard.SetFileDropList(paths);
                NotificationRequested?.Invoke($"📋 Berkas '{item.FileName}' disalin ke Clipboard");
            }
            catch { }
        }

        public void ShareToDiscord(ShelfItem item)
        {
            try
            {
                CopyToClipboard(item);
                NativeMethods.FocusProcessMainWindow("Discord");
                NotificationRequested?.Invoke("💬 Siap dikirim ke Discord (Tekan Ctrl+V di chat)");
            }
            catch { }
        }

        public void ShareToSteam(ShelfItem item)
        {
            try
            {
                CopyToClipboard(item);
                NativeMethods.FocusProcessMainWindow("steam");
                NotificationRequested?.Invoke("🎮 Siap dikirim ke Steam (Tekan Ctrl+V di chat)");
            }
            catch { }
        }

        public void OpenWindowsNearbyShare()
        {
            try
            {
                Process.Start(new ProcessStartInfo("ms-settings:crossdevice") { UseShellExecute = true });
                NotificationRequested?.Invoke("📡 Membuka Pengaturan Berbagi Dekat Windows (Nearby Share)");
            }
            catch { }
        }

        // ==============================================
        // AIRDROP LOCAL WEB DROP SERVER (PHONE / LAPTOP)
        // ==============================================
        public void EnsureServerStarted()
        {
            if (_isServerRunning) return;
            try
            {
                _httpListener = new HttpListener();
                _localIp = GetLocalIpAddress();
                AirDropUrl = $"http://{_localIp}:{_serverPort}/";
                _httpListener.Prefixes.Add($"http://*:{_serverPort}/");
                _httpListener.Start();
                IsServerRunning = true;
                Task.Run(ListenLoop);
            }
            catch
            {
                try
                {
                    _httpListener = new HttpListener();
                    _httpListener.Prefixes.Add($"http://localhost:{_serverPort}/");
                    _httpListener.Start();
                    IsServerRunning = true;
                    Task.Run(ListenLoop);
                }
                catch { }
            }
        }

        private async Task ListenLoop()
        {
            while (_isServerRunning && _httpListener != null && _httpListener.IsListening)
            {
                try
                {
                    var ctx = await _httpListener.GetContextAsync();
                    _ = ProcessRequest(ctx);
                }
                catch { break; }
            }
        }

        private async Task ProcessRequest(HttpListenerContext ctx)
        {
            try
            {
                string rawUrl = ctx.Request.RawUrl ?? "/";
                if (rawUrl == "/" || rawUrl.StartsWith("/index"))
                {
                    // Render HTML Portal
                    string html = GenerateAirDropHtml();
                    byte[] buf = Encoding.UTF8.GetBytes(html);
                    ctx.Response.ContentType = "text/html; charset=utf-8";
                    ctx.Response.ContentLength64 = buf.Length;
                    await ctx.Response.OutputStream.WriteAsync(buf);
                }
                else if (rawUrl.StartsWith("/download/"))
                {
                    string id = rawUrl.Substring("/download/".Length);
                    ShelfItem? target = null;
                    foreach (var itm in Items)
                    {
                        if (itm.Id == id) { target = itm; break; }
                    }

                    if (target != null && File.Exists(target.FullPath))
                    {
                        ctx.Response.ContentType = "application/octet-stream";
                        ctx.Response.AddHeader("Content-Disposition", $"attachment; filename=\"{Uri.EscapeDataString(target.FileName)}\"");
                        using var fs = File.OpenRead(target.FullPath);
                        ctx.Response.ContentLength64 = fs.Length;
                        await fs.CopyToAsync(ctx.Response.OutputStream);
                    }
                    else
                    {
                        ctx.Response.StatusCode = 404;
                    }
                }
                else
                {
                    ctx.Response.StatusCode = 404;
                }
            }
            catch { }
            finally
            {
                try { ctx.Response.OutputStream.Close(); } catch { }
            }
        }

        private string GenerateAirDropHtml()
        {
            var sb = new StringBuilder();
            sb.Append("<!DOCTYPE html><html><head><meta charset='utf-8'><meta name='viewport' content='width=device-width, initial-scale=1.0'>");
            sb.Append("<title>Dynamic Island AirDrop</title>");
            sb.Append("<style>");
            sb.Append("body{background:#0D0D12;color:#FFF;font-family:-apple-system,BlinkMacSystemFont,Segoe UI,Roboto,sans-serif;margin:0;padding:24px;display:flex;flex-direction:column;align-items:center;}");
            sb.Append(".card{background:#161620;border:1px solid #28283A;border-radius:18px;max-width:540px;width:100%;padding:24px;box-sizing:border-box;box-shadow:0 12px 30px rgba(0,0,0,0.5);}");
            sb.Append(".header{display:flex;align-items:center;gap:12px;margin-bottom:20px;}");
            sb.Append(".logo{width:36px;height:36px;background:linear-gradient(135deg,#0A84FF,#5E5CE6);border-radius:10px;display:flex;align-items:center;justify-content:center;font-size:20px;}");
            sb.Append("h1{font-size:18px;margin:0;font-weight:700;} p{margin:2px 0 0;font-size:12px;color:#8E8E93;}");
            sb.Append(".file-list{display:flex;flex-direction:column;gap:10px;margin-top:16px;}");
            sb.Append(".file-item{background:#1F1F2C;border-radius:12px;padding:12px 16px;display:flex;align-items:center;justify-content:space-between;}");
            sb.Append(".file-info{display:flex;align-items:center;gap:12px;overflow:hidden;}");
            sb.Append(".icon{font-size:24px;}");
            sb.Append(".name{font-size:14px;font-weight:600;white-space:nowrap;overflow:hidden;text-overflow:ellipsis;max-width:260px;}");
            sb.Append(".size{font-size:11px;color:#8E8E93;}");
            sb.Append(".btn{background:#0A84FF;color:#FFF;text-decoration:none;padding:8px 16px;border-radius:8px;font-size:12px;font-weight:600;display:inline-block;}");
            sb.Append(".empty{text-align:center;padding:32px 0;color:#636366;}");
            sb.Append("</style></head><body>");
            sb.Append("<div class='card'>");
            sb.Append("<div class='header'>");
            sb.Append("<div class='logo'>📡</div>");
            sb.Append("<div><h1>AirDrop Local Shelf</h1><p>Unduh berkas dari PC Dynamic Island langsung ke HP Anda</p></div>");
            sb.Append("</div>");

            if (Items.Count == 0)
            {
                sb.Append("<div class='empty'>Belum ada berkas di Shelf Dynamic Island.<br>Tarik berkas ke Island untuk berbagi.</div>");
            }
            else
            {
                sb.Append("<div class='file-list'>");
                foreach (var item in Items)
                {
                    sb.Append("<div class='file-item'>");
                    sb.Append("<div class='file-info'>");
                    sb.Append($"<span class='icon'>{item.GlyphIcon}</span>");
                    sb.Append($"<div><div class='name'>{WebUtility.HtmlEncode(item.FileName)}</div><div class='size'>{item.FileSizeText}</div></div>");
                    sb.Append("</div>");
                    sb.Append($"<a href='/download/{item.Id}' class='btn' download>⬇️ Unduh</a>");
                    sb.Append("</div>");
                }
                sb.Append("</div>");
            }

            sb.Append("</div></body></html>");
            return sb.ToString();
        }

        private static string GetLocalIpAddress()
        {
            try
            {
                using var socket = new Socket(AddressFamily.InterNetwork, SocketType.Dgram, 0);
                socket.Connect("8.8.8.8", 65530);
                var endPoint = socket.LocalEndPoint as IPEndPoint;
                return endPoint?.Address.ToString() ?? "127.0.0.1";
            }
            catch
            {
                return "127.0.0.1";
            }
        }

        // ==============================================
        // HELPER FUNCTIONS
        // ==============================================
        private static (string cat, string color, string glyph, bool isImg) CategorizeFile(string ext, bool isDir)
        {
            if (isDir) return ("Folder", "#FF9F0A", "📁", false);

            switch (ext)
            {
                case ".PNG":
                case ".JPG":
                case ".JPEG":
                case ".GIF":
                case ".BMP":
                case ".WEBP":
                case ".SVG":
                case ".ICO":
                    return ("Foto & Gambar", "#30D158", "🖼️", true);

                case ".MP4":
                case ".MKV":
                case ".MOV":
                case ".AVI":
                case ".WEBM":
                    return ("Video", "#FA2D48", "🎬", false);

                case ".MP3":
                case ".WAV":
                case ".FLAC":
                case ".AAC":
                case ".OGG":
                case ".M4A":
                    return ("Audio", "#FA2D48", "🎵", false);

                case ".PDF":
                    return ("PDF Dokumen", "#FF453A", "📕", false);

                case ".DOC":
                case ".DOCX":
                case ".TXT":
                case ".RTF":
                case ".XLS":
                case ".XLSX":
                case ".PPT":
                case ".PPTX":
                    return ("Dokumen", "#0A84FF", "📄", false);

                case ".ZIP":
                case ".RAR":
                case ".7Z":
                case ".TAR":
                case ".GZ":
                    return ("Arsip Kompresi", "#BF5AF2", "📦", false);

                case ".CS":
                case ".JS":
                case ".TS":
                case ".PY":
                case ".CPP":
                case ".C":
                case ".HTML":
                case ".CSS":
                case ".JSON":
                case ".XML":
                    return ("Source Code", "#64D2FF", "💻", false);

                default:
                    return ("Berkas", "#8E8E93", "📄", false);
            }
        }

        private static ImageSource? LoadThumbnail(string path)
        {
            try
            {
                var bmp = new BitmapImage();
                bmp.BeginInit();
                bmp.UriSource = new Uri(path);
                bmp.DecodePixelWidth = 120; // Lightweight thumbnail
                bmp.CacheOption = BitmapCacheOption.OnLoad;
                bmp.EndInit();
                bmp.Freeze();
                return bmp;
            }
            catch { return null; }
        }

        public static string FormatFileSize(long bytes)
        {
            if (bytes < 1024) return $"{bytes} B";
            if (bytes < 1024 * 1024) return $"{bytes / 1024.0:F1} KB";
            if (bytes < 1024 * 1024 * 1024) return $"{bytes / (1024.0 * 1024):F1} MB";
            return $"{bytes / (1024.0 * 1024 * 1024):F2} GB";
        }
    }

    public class CountToVisibilityConverter : IValueConverter
    {
        public bool Invert { get; set; } = false;

        public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
        {
            int count = value is int i ? i : 0;
            bool hasItems = count > 0;
            if (Invert) hasItems = !hasItems;
            return hasItems ? Visibility.Visible : Visibility.Collapsed;
        }

        public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture) =>
            throw new NotImplementedException();
    }

    /// <summary>0–100 percentage to a 0–1 ScaleX, used for the macOS battery glyph fill.</summary>
    public class PercentToScaleConverter : IValueConverter
    {
        public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
        {
            double pct = value switch { int i => i, double d => d, _ => 0 };
            return Math.Clamp(pct / 100.0, 0.0, 1.0);
        }

        public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture) =>
            throw new NotImplementedException();
    }

    /// <summary>macOS battery colours: red at 20% or below, green otherwise.</summary>
    public class PercentToBatteryBrushConverter : IValueConverter
    {
        private static readonly System.Windows.Media.Brush Low = Freeze(System.Windows.Media.Color.FromRgb(0xFF, 0x45, 0x3A));
        private static readonly System.Windows.Media.Brush Normal = Freeze(System.Windows.Media.Color.FromRgb(0x30, 0xD1, 0x58));

        private static System.Windows.Media.Brush Freeze(System.Windows.Media.Color c)
        {
            var b = new System.Windows.Media.SolidColorBrush(c);
            b.Freeze();
            return b;
        }

        public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
        {
            int pct = value is int i ? i : 100;
            return pct <= 20 ? Low : Normal;
        }

        public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture) =>
            throw new NotImplementedException();
    }

    public class InverseBoolToVisibilityConverter : IValueConverter
    {
        public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
        {
            bool b = value is bool flag && flag;
            return b ? Visibility.Collapsed : Visibility.Visible;
        }

        public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture) =>
            throw new NotImplementedException();
    }
}
