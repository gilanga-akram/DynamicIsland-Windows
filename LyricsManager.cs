// Language: C#
// Filename: LyricsManager.cs
// Runs on: Windows 10 / Windows 11 (.NET 9.0-windows10.0.19041.0)
// Targets: Realtime Synced Lyrics Engine (LRCLIB API, Apple Music Proxy, Local Cache, Fuzzy YouTube Cleaner & Karaoke Tracking)

using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.ComponentModel;
using System.IO;
using System.Linq;
using System.Net.Http;
using System.Runtime.CompilerServices;
using System.Text;
using System.Text.Json;
using System.Text.RegularExpressions;
using System.Threading;
using System.Threading.Tasks;
using System.Windows.Threading;

namespace DynamicIsland
{
    public class LyricLine : INotifyPropertyChanged
    {
        public TimeSpan Timestamp { get; set; }
        public string TimestampFormatted => $"{Timestamp.Minutes}:{Timestamp.Seconds:D2}";
        public string Text { get; set; } = string.Empty;

        private bool _isActive;
        public bool IsActive
        {
            get => _isActive;
            set { if (_isActive != value) { _isActive = value; OnPropertyChanged(); } }
        }

        private bool _isPast;
        public bool IsPast
        {
            get => _isPast;
            set { if (_isPast != value) { _isPast = value; OnPropertyChanged(); } }
        }

        private double _progress = 0.0;
        public double Progress
        {
            get => _progress;
            set { if (Math.Abs(_progress - value) > 0.005) { _progress = value; OnPropertyChanged(); } }
        }

        public event PropertyChangedEventHandler? PropertyChanged;
        protected void OnPropertyChanged([CallerMemberName] string? name = null)
            => PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(name));
    }

    public class LyricsManager : INotifyPropertyChanged
    {
        private static readonly HttpClient _httpClient = new()
        {
            Timeout = TimeSpan.FromSeconds(5)
        };

        private readonly Dispatcher _dispatcher;
        private readonly string _cacheDirectory;

        private ObservableCollection<LyricLine> _lines = new();
        public ObservableCollection<LyricLine> Lines
        {
            get => _lines;
            private set { _lines = value; OnPropertyChanged(); }
        }

        private string _currentLyricText = "";
        public string CurrentLyricText
        {
            get => _currentLyricText;
            private set { if (_currentLyricText != value) { _currentLyricText = value; OnPropertyChanged(); OnPropertyChanged(nameof(CurrentLyricDisplay)); } }
        }

        public string CurrentLyricDisplay
        {
            get
            {
                if (!string.IsNullOrWhiteSpace(_currentLyricText)) return _currentLyricText;
                if (_isLoading) return "Mencari lirik...";
                if (_isInstrumental) return "\uD83C\uDFB5 Musik Instrumental";
                if (Lines != null && Lines.Count > 0) return Lines[0].Text;
                if (!HasLyrics) return "Lirik tidak ditemukan";
                return "...";
            }
        }

        private string _nextLyricText = "";
        public string NextLyricText
        {
            get => _nextLyricText;
            private set { if (_nextLyricText != value) { _nextLyricText = value; OnPropertyChanged(); } }
        }

        private bool _hasLyrics = false;
        public bool HasLyrics
        {
            get => _hasLyrics;
            private set { if (_hasLyrics != value) { _hasLyrics = value; OnPropertyChanged(); OnPropertyChanged(nameof(CurrentLyricDisplay)); } }
        }

        private bool _isLoading = false;
        public bool IsLoading
        {
            get => _isLoading;
            private set { if (_isLoading != value) { _isLoading = value; OnPropertyChanged(); OnPropertyChanged(nameof(CurrentLyricDisplay)); } }
        }

        private string _lyricsSource = "";
        public string LyricsSource
        {
            get => _lyricsSource;
            private set
            {
                if (_lyricsSource != value)
                {
                    _lyricsSource = value;
                    OnPropertyChanged();
                    OnPropertyChanged(nameof(IsAppleMusicSource));
                }
            }
        }

        public bool IsAppleMusicSource => _lyricsSource.Equals("Apple Music", StringComparison.OrdinalIgnoreCase);

        private bool _isInstrumental = false;
        public bool IsInstrumental
        {
            get => _isInstrumental;
            private set { if (_isInstrumental != value) { _isInstrumental = value; OnPropertyChanged(); OnPropertyChanged(nameof(CurrentLyricDisplay)); } }
        }

        private string _statusMessage = "Siap memuat lirik...";
        public string StatusMessage
        {
            get => _statusMessage;
            private set { if (_statusMessage != value) { _statusMessage = value; OnPropertyChanged(); } }
        }

        private double _currentLineProgress = 0.0;
        public double CurrentLineProgress
        {
            get => _currentLineProgress;
            set { if (Math.Abs(_currentLineProgress - value) > 0.05) { _currentLineProgress = value; OnPropertyChanged(); } }
        }

        private int _userOffsetMs = 0; // Calibration offset in milliseconds (positive = display earlier)
        public int UserOffsetMs
        {
            get => _userOffsetMs;
            set
            {
                if (_userOffsetMs != value)
                {
                    _userOffsetMs = value;
                    OnPropertyChanged();
                    OnPropertyChanged(nameof(OffsetFormatted));
                }
            }
        }

        public string OffsetFormatted => _userOffsetMs == 0 ? "0.0s (Pas)" : (_userOffsetMs < 0 ? $"{_userOffsetMs / 1000.0:F1}s" : $"+{_userOffsetMs / 1000.0:F1}s");

        public void AdjustOffset(int deltaMs)
        {
            UserOffsetMs = Math.Clamp(_userOffsetMs + deltaMs, -60000, 60000);
        }

        public void ResetOffset()
        {
            UserOffsetMs = 0;
        }

        public void SyncToLine(int lineIndex, TimeSpan currentMediaTime)
        {
            if (lineIndex < 0 || lineIndex >= Lines.Count) return;
            var targetTimestamp = Lines[lineIndex].Timestamp;
            int neededOffset = (int)(targetTimestamp - currentMediaTime).TotalMilliseconds;
            UserOffsetMs = Math.Clamp(neededOffset, -60000, 60000);
            UpdatePosition(currentMediaTime);
        }

        private int _activeIndex = -1;
        public int ActiveIndex => _activeIndex;

        public event Action<int>? ActiveLineChanged;
        public event PropertyChangedEventHandler? PropertyChanged;

        private string _currentTrackKey = "";
        private CancellationTokenSource? _fetchCts;
        private readonly Dictionary<string, (bool HasLyrics, List<LyricLine> Lines, string PlainText)> _memoryCache = new(StringComparer.OrdinalIgnoreCase);

        static LyricsManager()
        {
            _httpClient.DefaultRequestHeaders.Add("User-Agent", "Mozilla/5.0 (Windows NT 10.0; Win64; x64) DynamicIslandPro/1.3.1");
        }

        public LyricsManager(Dispatcher dispatcher)
        {
            _dispatcher = dispatcher;
            _cacheDirectory = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData), "DynamicIslandPro", "LyricsCache");
            try { Directory.CreateDirectory(_cacheDirectory); } catch { }
        }

        public async Task LoadLyricsAsync(string rawTitle, string rawArtist, double durationSec, bool forceReload = false, bool isAppleMusic = false)
        {
            if (string.IsNullOrWhiteSpace(rawTitle) || rawTitle == "Apple Music" || rawTitle == "Memutar Audio")
            {
                ClearLyrics("Tidak ada lagu yang sedang diputar");
                return;
            }

            var (cleanTitle, cleanArtist) = CleanTrackMetadata(rawTitle, rawArtist);
            string coreTitle = GetCoreTitle(cleanTitle);

            string trackKey = $"{cleanTitle}||{cleanArtist}";

            if (!forceReload && trackKey == _currentTrackKey && (HasLyrics || IsLoading))
            {
                return; // Already current track
            }

            _currentTrackKey = trackKey;
            _fetchCts?.Cancel();
            _fetchCts = new CancellationTokenSource();
            var ct = _fetchCts.Token;

            // Check in-memory cache first (unless force reload requested)
            if (!forceReload && _memoryCache.TryGetValue(trackKey, out var cached))
            {
                ApplyLyricsResult(cached.HasLyrics, cached.Lines, cached.PlainText);
                return;
            }

            // Check local file disk cache
            string cacheFile = GetCacheFilePath(cleanTitle, cleanArtist);
            if (!forceReload && File.Exists(cacheFile))
            {
                try
                {
                    string diskLrc = await File.ReadAllTextAsync(cacheFile, ct);
                    var diskLines = ParseLrcString(diskLrc);
                    if (diskLines.Count > 0)
                    {
                        _memoryCache[trackKey] = (true, diskLines, diskLrc);
                        ApplyLyricsResult(true, diskLines, diskLrc);
                        return;
                    }
                }
                catch { }
            }

            IsLoading = true;
            StatusMessage = "Mencari lirik realtime...";

            try
            {
                // Debounce 150ms to avoid spamming APIs during rapid skipping
                await Task.Delay(150, ct);

                // --- TIER 1: Apple Music Direct (Fastest, highest quality synced lyrics) ---
                if (isAppleMusic && !string.IsNullOrWhiteSpace(cleanTitle))
                {
                    var appleRes = await TryFetchAppleMusicMultiAsync(cleanTitle, coreTitle, cleanArtist, ct);
                    if (appleRes != null && appleRes.Value.lines.Count > 0)
                    {
                        _memoryCache[trackKey] = (true, appleRes.Value.lines, appleRes.Value.lrc);
                        LyricsSource = "Apple Music";
                        SaveToDiskCache(cacheFile, appleRes.Value.lrc);
                        ApplyLyricsResult(true, appleRes.Value.lines, appleRes.Value.lrc);
                        return;
                    }
                }

                LrclibResponse? result = null;

                // --- TIER 2: LRCLIB Exact Get (coreTitle + cleanArtist) ---
                if (!string.IsNullOrWhiteSpace(coreTitle) && !string.IsNullOrWhiteSpace(cleanArtist))
                {
                    result = await FetchFromLrclibGetAsync(coreTitle, cleanArtist, ct);
                }

                // --- TIER 3: LRCLIB Exact Get with full clean title ---
                if (result == null && !ct.IsCancellationRequested && !string.IsNullOrWhiteSpace(cleanTitle) && !cleanTitle.Equals(coreTitle, StringComparison.OrdinalIgnoreCase))
                {
                    result = await FetchFromLrclibGetAsync(cleanTitle, cleanArtist, ct);
                }

                // --- TIER 4: Inverted Get (in case title and artist were swapped) ---
                if (result == null && !ct.IsCancellationRequested && !string.IsNullOrWhiteSpace(cleanArtist) && !cleanArtist.Equals(cleanTitle, StringComparison.OrdinalIgnoreCase))
                {
                    result = await FetchFromLrclibGetAsync(cleanArtist, coreTitle, ct);
                }

                // --- TIER 5: LRCLIB Structured Search (coreTitle + cleanArtist) ---
                if (result == null && !ct.IsCancellationRequested && !string.IsNullOrWhiteSpace(coreTitle))
                {
                    result = await FetchFromLrclibSearchStructuredAsync(coreTitle, cleanArtist, durationSec, ct);
                }

                // --- TIER 6: LRCLIB Fuzzy Keyword Search "{cleanArtist} {coreTitle}" ---
                // (Crucial for YouTube where video titles contain extra words or differing capitalization)
                if (result == null && !ct.IsCancellationRequested)
                {
                    string broadQuery = string.IsNullOrWhiteSpace(cleanArtist) ? coreTitle : $"{cleanArtist} {coreTitle}";
                    result = await FetchFromLrclibSearchAsync(broadQuery, durationSec, ct);
                }

                // --- TIER 7: Apple Music Proxy Fallback (Enabled for YouTube & Browser tracks!) ---
                if (result == null && !ct.IsCancellationRequested && !string.IsNullOrWhiteSpace(coreTitle))
                {
                    var appleRes = await TryFetchAppleMusicMultiAsync(cleanTitle, coreTitle, cleanArtist, ct);
                    if (appleRes != null && appleRes.Value.lines.Count > 0)
                    {
                        _memoryCache[trackKey] = (true, appleRes.Value.lines, appleRes.Value.lrc);
                        LyricsSource = "Apple Music";
                        SaveToDiskCache(cacheFile, appleRes.Value.lrc);
                        ApplyLyricsResult(true, appleRes.Value.lines, appleRes.Value.lrc);
                        return;
                    }
                }

                // --- TIER 8: LRCLIB Core Title Only Search (Handles YouTube video with unknown channel) ---
                if (result == null && !ct.IsCancellationRequested && !string.IsNullOrWhiteSpace(coreTitle))
                {
                    result = await FetchFromLrclibSearchAsync(coreTitle, durationSec, ct);
                }

                // --- TIER 9: LRCLIB Full Clean Title Search ---
                if (result == null && !ct.IsCancellationRequested && !string.IsNullOrWhiteSpace(cleanTitle) && !cleanTitle.Equals(coreTitle, StringComparison.OrdinalIgnoreCase))
                {
                    result = await FetchFromLrclibSearchAsync(cleanTitle, durationSec, ct);
                }

                if (ct.IsCancellationRequested) return;

                if (result != null)
                {
                    bool instrumental = result.Instrumental;
                    string synced = result.SyncedLyrics ?? "";
                    string plain = result.PlainLyrics ?? "";

                    if (!string.IsNullOrWhiteSpace(synced))
                    {
                        var parsedLines = ParseLrcString(synced);
                        if (parsedLines.Count > 0)
                        {
                            _memoryCache[trackKey] = (true, parsedLines, plain);
                            LyricsSource = "LRCLIB";
                            SaveToDiskCache(cacheFile, synced);
                            ApplyLyricsResult(true, parsedLines, plain, instrumental);
                            return;
                        }
                    }

                    if (!string.IsNullOrWhiteSpace(plain))
                    {
                        var plainLines = ConvertPlainLyricsToTimed(plain, durationSec);
                        _memoryCache[trackKey] = (true, plainLines, plain);
                        SaveToDiskCache(cacheFile, plain);
                        ApplyLyricsResult(true, plainLines, plain, instrumental);
                        return;
                    }

                    if (instrumental)
                    {
                        var instLines = new List<LyricLine>
                        {
                            new() { Timestamp = TimeSpan.Zero, Text = "\uD83C\uDFB5 Musik Instrumental" }
                        };
                        _memoryCache[trackKey] = (true, instLines, "");
                        ApplyLyricsResult(true, instLines, "", true);
                        return;
                    }
                }

                // --- TIER 10: Fallback to lyrics.ovh for plain lyrics ---
                if (!ct.IsCancellationRequested && !string.IsNullOrWhiteSpace(coreTitle) && !string.IsNullOrWhiteSpace(cleanArtist))
                {
                    string? ovhLyrics = await FetchFromLyricsOvhAsync(coreTitle, cleanArtist, ct);
                    if (string.IsNullOrWhiteSpace(ovhLyrics))
                    {
                        ovhLyrics = await FetchFromLyricsOvhAsync(cleanArtist, coreTitle, ct);
                    }

                    if (!string.IsNullOrWhiteSpace(ovhLyrics))
                    {
                        var plainLines = ConvertPlainLyricsToTimed(ovhLyrics, durationSec);
                        _memoryCache[trackKey] = (true, plainLines, ovhLyrics);
                        LyricsSource = "Web";
                        SaveToDiskCache(cacheFile, ovhLyrics);
                        ApplyLyricsResult(true, plainLines, ovhLyrics);
                        return;
                    }
                }

                // If completely not found
                _memoryCache[trackKey] = (false, new List<LyricLine>(), "");
                ApplyLyricsResult(false, new List<LyricLine>(), "");
            }
            catch (OperationCanceledException)
            {
                // Normal track skip cancellation
            }
            catch
            {
                _memoryCache[trackKey] = (false, new List<LyricLine>(), "");
                ApplyLyricsResult(false, new List<LyricLine>(), "");
            }
        }

        private async Task<(List<LyricLine> lines, string lrc)?> TryFetchAppleMusicMultiAsync(string title, string coreTitle, string artist, CancellationToken ct)
        {
            // 1. Try Core Title + Artist
            if (!string.IsNullOrWhiteSpace(coreTitle))
            {
                var r = await FetchFromAppleMusicAsync(coreTitle, artist, ct);
                if (r != null) return r;
            }

            // 2. Try Full Title + Artist
            if (!string.IsNullOrWhiteSpace(title) && !title.Equals(coreTitle, StringComparison.OrdinalIgnoreCase))
            {
                var r = await FetchFromAppleMusicAsync(title, artist, ct);
                if (r != null) return r;
            }

            // 3. Try splitting multiple artists e.g. "Kendrick Lamar, SZA" -> "Kendrick Lamar"
            if (!string.IsNullOrWhiteSpace(artist) && (artist.Contains(",") || artist.Contains("&")))
            {
                string firstArtist = artist.Split(new[] { ',', '&' })[0].Trim();
                var r = await FetchFromAppleMusicAsync(coreTitle, firstArtist, ct);
                if (r != null) return r;
            }

            // 4. Try Core Title alone
            if (!string.IsNullOrWhiteSpace(coreTitle))
            {
                var r = await FetchFromAppleMusicAsync(coreTitle, "", ct);
                if (r != null) return r;
            }

            return null;
        }

        private List<LyricLine> ConvertPlainLyricsToTimed(string plainText, double totalSec)
        {
            var lines = new List<LyricLine>();
            if (string.IsNullOrWhiteSpace(plainText)) return lines;

            var rawLines = plainText.Split(new[] { '\r', '\n' }, StringSplitOptions.RemoveEmptyEntries)
                                    .Select(l => l.Trim())
                                    .Where(l => !string.IsNullOrWhiteSpace(l) && !l.StartsWith("[") && !l.StartsWith("Paroles de"))
                                    .ToList();

            if (rawLines.Count == 0) return lines;

            double songDuration = (totalSec > 30) ? totalSec : 200.0;
            double startOffset = 5.0; // Intro delay
            double playableDuration = Math.Max(10.0, songDuration - startOffset - 10.0);
            double lineInterval = playableDuration / rawLines.Count;

            for (int i = 0; i < rawLines.Count; i++)
            {
                double seconds = startOffset + (i * lineInterval);
                lines.Add(new LyricLine
                {
                    Timestamp = TimeSpan.FromSeconds(seconds),
                    Text = rawLines[i]
                });
            }

            return lines;
        }

        private async Task<LrclibResponse?> FetchFromLrclibGetAsync(string title, string artist, CancellationToken ct)
        {
            try
            {
                string url = $"https://lrclib.net/api/get?track_name={Uri.EscapeDataString(title)}&artist_name={Uri.EscapeDataString(artist)}";
                using var response = await _httpClient.GetAsync(url, ct);
                if (response.IsSuccessStatusCode)
                {
                    var json = await response.Content.ReadAsStringAsync(ct);
                    return JsonSerializer.Deserialize<LrclibResponse>(json, new JsonSerializerOptions { PropertyNameCaseInsensitive = true });
                }
            }
            catch { }
            return null;
        }

        private static LrclibResponse? PickBestLrclibItem(List<LrclibResponse>? items, double durationSec)
        {
            if (items == null || items.Count == 0) return null;
            var synced = items.Where(i => !string.IsNullOrWhiteSpace(i.SyncedLyrics)).ToList();
            if (synced.Count > 0)
            {
                if (durationSec > 20)
                {
                    // Find closest duration if close enough (< 60s)
                    var bestDuration = synced.OrderBy(i => Math.Abs(i.Duration - durationSec)).First();
                    if (Math.Abs(bestDuration.Duration - durationSec) < 60) return bestDuration;
                }
                // Otherwise return first synced lyric result (common for YouTube with intro/outro skits)
                return synced[0];
            }
            return items[0];
        }

        private async Task<LrclibResponse?> FetchFromLrclibSearchStructuredAsync(string title, string artist, double durationSec, CancellationToken ct)
        {
            try
            {
                string url = $"https://lrclib.net/api/search?track_name={Uri.EscapeDataString(title)}";
                if (!string.IsNullOrWhiteSpace(artist))
                    url += $"&artist_name={Uri.EscapeDataString(artist)}";

                using var response = await _httpClient.GetAsync(url, ct);
                if (response.IsSuccessStatusCode)
                {
                    var json = await response.Content.ReadAsStringAsync(ct);
                    var items = JsonSerializer.Deserialize<List<LrclibResponse>>(json, new JsonSerializerOptions { PropertyNameCaseInsensitive = true });
                    return PickBestLrclibItem(items, durationSec);
                }
            }
            catch { }
            return null;
        }

        private async Task<LrclibResponse?> FetchFromLrclibSearchAsync(string query, double durationSec, CancellationToken ct)
        {
            if (string.IsNullOrWhiteSpace(query)) return null;

            for (int retry = 0; retry < 2; retry++)
            {
                try
                {
                    string url = $"https://lrclib.net/api/search?q={Uri.EscapeDataString(query)}";
                    using var response = await _httpClient.GetAsync(url, ct);
                    if (response.IsSuccessStatusCode)
                    {
                        var json = await response.Content.ReadAsStringAsync(ct);
                        var items = JsonSerializer.Deserialize<List<LrclibResponse>>(json, new JsonSerializerOptions { PropertyNameCaseInsensitive = true });
                        return PickBestLrclibItem(items, durationSec);
                    }
                    else if ((int)response.StatusCode == 503)
                    {
                        await Task.Delay(250, ct);
                        continue;
                    }
                }
                catch { }
            }
            return null;
        }

        private async Task<string?> FetchFromLyricsOvhAsync(string title, string artist, CancellationToken ct)
        {
            try
            {
                string url = $"https://api.lyrics.ovh/v1/{Uri.EscapeDataString(artist)}/{Uri.EscapeDataString(title)}";
                using var response = await _httpClient.GetAsync(url, ct);
                if (response.IsSuccessStatusCode)
                {
                    var json = await response.Content.ReadAsStringAsync(ct);
                    var doc = JsonDocument.Parse(json);
                    if (doc.RootElement.TryGetProperty("lyrics", out var lyricsProp))
                    {
                        return lyricsProp.GetString();
                    }
                }
            }
            catch { }
            return null;
        }

        private async Task<(List<LyricLine> lines, string lrc)?> FetchFromAppleMusicAsync(string title, string artist, CancellationToken ct)
        {
            if (string.IsNullOrWhiteSpace(title)) return null;

            string cleanT = Uri.EscapeDataString(title.Trim());
            string cleanA = Uri.EscapeDataString((artist ?? "").Trim());

            string[] hosts = new[]
            {
                "https://lyricsplus.binimum.org",
                "https://lyricsplus.prjktla.workers.dev"
            };

            foreach (var host in hosts)
            {
                if (ct.IsCancellationRequested) break;
                try
                {
                    string url = $"{host}/v2/lyrics/get?title={cleanT}&artist={cleanA}&source=apple";
                    using var cts = CancellationTokenSource.CreateLinkedTokenSource(ct);
                    cts.CancelAfter(TimeSpan.FromSeconds(5.0));

                    using var req = new HttpRequestMessage(HttpMethod.Get, url);
                    req.Headers.UserAgent.ParseAdd("Mozilla/5.0 (Windows NT 10.0; Win64; x64) AppleWebKit/537.36 (KHTML, like Gecko) Chrome/120.0.0.0 Safari/537.36");
                    req.Headers.Accept.ParseAdd("application/json");

                    using var res = await _httpClient.SendAsync(req, cts.Token);
                    if (!res.IsSuccessStatusCode) continue;

                    var json = await res.Content.ReadAsStringAsync(cts.Token);
                    using var doc = JsonDocument.Parse(json);
                    var root = doc.RootElement;

                    if (!root.TryGetProperty("lyrics", out var lyricsArr) || lyricsArr.ValueKind != JsonValueKind.Array)
                        continue;

                    var lines = new List<LyricLine>();
                    var sbLrc = new StringBuilder();
                    sbLrc.AppendLine("[by:Apple Music]");

                    foreach (var item in lyricsArr.EnumerateArray())
                    {
                        if (!item.TryGetProperty("text", out var textProp)) continue;
                        string text = textProp.GetString() ?? "";

                        long timeMs = 0;
                        if (item.TryGetProperty("time", out var timeProp))
                        {
                            if (timeProp.ValueKind == JsonValueKind.Number) timeMs = timeProp.GetInt64();
                            else if (long.TryParse(timeProp.GetString(), out var parsedMs)) timeMs = parsedMs;
                        }

                        var ts = TimeSpan.FromMilliseconds(Math.Max(0, timeMs));
                        lines.Add(new LyricLine
                        {
                            Timestamp = ts,
                            Text = text
                        });

                        int min = ts.Minutes;
                        int sec = ts.Seconds;
                        int cs = ts.Milliseconds / 10;
                        sbLrc.AppendLine($"[{min:D2}:{sec:D2}.{cs:D2}]{text}");
                    }

                    if (lines.Count > 0)
                    {
                        lines.Sort((a, b) => a.Timestamp.CompareTo(b.Timestamp));
                        return (lines, sbLrc.ToString());
                    }
                }
                catch
                {
                    // Continue to next mirror
                }
            }

            return null;
        }

        private List<LyricLine> ParseLrcString(string lrc)
        {
            var list = new List<LyricLine>();
            if (string.IsNullOrWhiteSpace(lrc)) return list;

            var timeRegex = new Regex(@"\[(\d{1,2}):(\d{2})(?:[.:](\d{1,3}))?\]");
            var lines = lrc.Split(new[] { '\r', '\n' }, StringSplitOptions.RemoveEmptyEntries);

            foreach (var rawLine in lines)
            {
                string trimmed = rawLine.Trim();
                var matches = timeRegex.Matches(trimmed);
                if (matches.Count > 0)
                {
                    // Text is everything after the last timestamp
                    var lastMatch = matches[matches.Count - 1];
                    string text = trimmed.Substring(lastMatch.Index + lastMatch.Length).Trim();
                    if (string.IsNullOrWhiteSpace(text)) text = "  ";

                    foreach (Match m in matches)
                    {
                        int min = int.Parse(m.Groups[1].Value);
                        int sec = int.Parse(m.Groups[2].Value);
                        int ms = 0;
                        if (m.Groups[3].Success)
                        {
                            string msStr = m.Groups[3].Value.PadRight(3, '0');
                            if (msStr.Length > 3) msStr = msStr.Substring(0, 3);
                            int.TryParse(msStr, out ms);
                        }

                        var time = new TimeSpan(0, 0, min, sec, ms);
                        list.Add(new LyricLine
                        {
                            Timestamp = time,
                            Text = text
                        });
                    }
                }
            }

            return list.OrderBy(x => x.Timestamp).ToList();
        }

        private void ApplyLyricsResult(bool hasLyrics, List<LyricLine> lines, string plain, bool instrumental = false)
        {
            _dispatcher.Invoke(() =>
            {
                Lines = new ObservableCollection<LyricLine>(lines);
                HasLyrics = hasLyrics && lines.Count > 0;
                IsInstrumental = instrumental;
                _activeIndex = -1;
                CurrentLyricText = HasLyrics ? Lines[0].Text : "";
                NextLyricText = (HasLyrics && Lines.Count > 1) ? Lines[1].Text : "";
                StatusMessage = HasLyrics ? "" : "Lirik belum tersedia untuk lagu ini";
            });
        }

        public void ClearLyrics(string status)
        {
            _dispatcher.Invoke(() =>
            {
                _fetchCts?.Cancel();
                Lines.Clear();
                HasLyrics = false;
                IsInstrumental = false;
                IsLoading = false;
                CurrentLyricText = "";
                NextLyricText = "";
                StatusMessage = status;
                _currentTrackKey = "";
                _activeIndex = -1;
            });
        }

        public void UpdatePosition(TimeSpan currentMediaTime)
        {
            if (!HasLyrics || Lines == null || Lines.Count == 0) return;

            var effectiveTime = currentMediaTime + TimeSpan.FromMilliseconds(UserOffsetMs);
            if (effectiveTime < TimeSpan.Zero) effectiveTime = TimeSpan.Zero;

            int newIndex = -1;
            for (int i = Lines.Count - 1; i >= 0; i--)
            {
                if (effectiveTime >= Lines[i].Timestamp)
                {
                    newIndex = i;
                    break;
                }
            }

            if (newIndex == -1) newIndex = 0;

            if (newIndex != _activeIndex)
            {
                _activeIndex = newIndex;

                for (int i = 0; i < Lines.Count; i++)
                {
                    Lines[i].IsActive = (i == _activeIndex);
                    Lines[i].IsPast = (i < _activeIndex);
                    if (i != _activeIndex)
                    {
                        Lines[i].Progress = (i < _activeIndex) ? 1.0 : 0.0;
                    }
                }

                CurrentLyricText = Lines[_activeIndex].Text;
                NextLyricText = (_activeIndex + 1 < Lines.Count) ? Lines[_activeIndex + 1].Text : "";
                ActiveLineChanged?.Invoke(_activeIndex);
            }

            // Realtime Karaoke Progress Interpolation
            if (_activeIndex >= 0 && _activeIndex < Lines.Count)
            {
                var currentLineTime = Lines[_activeIndex].Timestamp;
                TimeSpan nextLineTime;

                if (_activeIndex + 1 < Lines.Count)
                {
                    nextLineTime = Lines[_activeIndex + 1].Timestamp;
                }
                else
                {
                    nextLineTime = currentLineTime + TimeSpan.FromSeconds(5.0);
                }

                double lineDuration = (nextLineTime - currentLineTime).TotalSeconds;
                if (lineDuration > 0.05)
                {
                    double elapsed = (effectiveTime - currentLineTime).TotalSeconds;
                    double ratio = Math.Clamp(elapsed / lineDuration, 0.0, 1.0);
                    CurrentLineProgress = ratio * 100.0;
                    Lines[_activeIndex].Progress = ratio;
                }
                else
                {
                    CurrentLineProgress = 100.0;
                    Lines[_activeIndex].Progress = 1.0;
                }
            }
        }

        public static (string Title, string Artist) CleanTrackMetadata(string rawTitle, string rawArtist)
        {
            if (string.IsNullOrWhiteSpace(rawTitle)) return ("", "");

            string title = rawTitle.Trim();
            string artist = (rawArtist ?? "").Trim();

            // 1. Strip browser & website suffixes
            string[] siteSuffixes = new[]
            {
                " - YouTube Music", " | YouTube Music",
                " - YouTube", " | YouTube",
                " - Google Chrome", " | Google Chrome",
                " - Microsoft Edge", " | Microsoft Edge",
                " - Brave", " | Brave",
                " - Mozilla Firefox", " | Mozilla Firefox",
                " - Opera", " | Opera",
                " - Vivaldi", " | Vivaldi"
            };
            foreach (var suffix in siteSuffixes)
            {
                if (title.EndsWith(suffix, StringComparison.OrdinalIgnoreCase))
                {
                    title = title.Substring(0, title.Length - suffix.Length).Trim();
                }
            }

            // 2. Normalize common YouTube noise tags (Indonesian & International)
            string[] noiseRegexes = new[]
            {
                @"\s*[\(\[]\s*Official\s*(?:Music\s*)?Video\s*[\)\]]",
                @"\s*[\(\[]\s*Official\s*Lyric\s*Video(?:\s*4K)?\s*[\)\]]",
                @"\s*[\(\[]\s*Lyric\s*Video\s*[\)\]]",
                @"\s*[\(\[]\s*Lyrics?\s*Video\s*[\)\]]",
                @"\s*[\(\[]\s*Lyrics?\s*[\)\]]",
                @"\s*[\(\[]\s*(?:Official\s*)?Lirik(?:\s*Video)?\s*[\)\]]",
                @"\s*[\(\[]\s*Video\s*Lirik(?:\s*Resmi)?\s*[\)\]]",
                @"\s*[\(\[]\s*Video\s*Klip(?:\s*Resmi)?\s*[\)\]]",
                @"\s*[\(\[]\s*Klip\s*Resmi\s*[\)\]]",
                @"\s*[\(\[]\s*Lirik\s*Lagu\s*[\)\]]",
                @"\s*[\(\[]\s*Color\s*Coded\s*Lyrics?\s*[\)\]]",
                @"\s*[\(\[]\s*Official\s*Audio\s*[\)\]]",
                @"\s*[\(\[]\s*Audio\s*(?:Only|Resmi)?\s*[\)\]]",
                @"\s*[\(\[]\s*Official\s*Visualizer\s*[\)\]]",
                @"\s*[\(\[]\s*Visualizer\s*[\)\]]",
                @"\s*[\(\[]\s*Official\s*MV\s*[\)\]]",
                @"\s*[\(\[]\s*MV\s*[\)\]]",
                @"\s*[\(\[]\s*M/V\s*[\)\]]",
                @"\s*[\(\[]\s*Performance\s*Video\s*[\)\]]",
                @"\s*[\(\[]\s*Dance\s*Practice(?:\s*Video)?\s*[\)\]]",
                @"\s*[\(\[]\s*Clip\s*Officiel\s*[\)\]]",
                @"\s*[\(\[]\s*Video\s*Clip\s*[\)\]]",
                @"\s*[\(\[]\s*Music\s*Video\s*[\)\]]",
                @"\s*[\(\[]\s*Official\s*[\)\]]",
                @"\s*[\(\[]\s*Remastered(?:\s*\d{4})?\s*[\)\]]",
                @"\s*[\(\[]\s*Live(?:\s+from|\s+at)?\s*[^)\]]*[\)\]]",
                @"\s*[\(\[]\s*Acoustic(?:\s*Version)?\s*[\)\]]",
                @"\s*[\(\[]\s*Versi\s*Akustik\s*[\)\]]",
                @"\s*[\(\[]\s*(?:4K|HD|HQ|1080p|720p)\s*[\)\]]",
                @"\s*[\(\[]\s*(?:Sub\s*Indo|Indo\s*Sub|Eng\s*Sub|CC)\s*[\)\]]",
                @"\s*[\(\[]\s*(?:Prod\.|Produced\s*by)\s*[^)\]]+[\)\]]",
                @"\s*[\(\[]\s*(?:Directed\s*by|Dir\.)\s*[^)\]]+[\)\]]",
                @"\s*\|\s*Official.*",
                @"\s*-\s*Official.*",
                @"\s*\|\s*Video.*",
                @"\s*\|\s*HD.*"
            };

            foreach (var pattern in noiseRegexes)
            {
                title = Regex.Replace(title, pattern, "", RegexOptions.IgnoreCase).Trim();
            }

            // 3. Handle Unicode / Asian quotation brackets: 「Title」, 『Title』, 【Title】
            var cjkMatch = Regex.Match(title, @"^(.+?)[\u300C\u300E\u3010](.+?)[\u300D\u300F\u3011]");
            if (cjkMatch.Success)
            {
                string pArtist = cjkMatch.Groups[1].Value.Trim();
                string pTitle = cjkMatch.Groups[2].Value.Trim();
                if (!string.IsNullOrWhiteSpace(pArtist) && !string.IsNullOrWhiteSpace(pTitle))
                {
                    return (CleanSingleString(pTitle), CleanSingleString(pArtist));
                }
            }

            // 4. Clean channel tags from artist
            string cleanA = CleanArtistName(artist);

            // 5. Look for Artist - Title separators in title
            string[] separators = new[] { " - ", " – ", " — ", " : ", " | ", " ~ ", " // ", " / ", " by " };
            string matchedSep = "";
            foreach (var sep in separators)
            {
                if (title.Contains(sep))
                {
                    matchedSep = sep;
                    break;
                }
            }

            if (!string.IsNullOrEmpty(matchedSep))
            {
                var parts = title.Split(new[] { matchedSep }, 2, StringSplitOptions.RemoveEmptyEntries);
                if (parts.Length == 2)
                {
                    string part0 = CleanSingleString(parts[0]);
                    string part1 = CleanSingleString(parts[1]);

                    bool artistIsChannelOrGeneric = IsChannelOrGenericArtist(cleanA);

                    if (artistIsChannelOrGeneric)
                    {
                        // On YouTube, "Artist - Title" is the standard format
                        return (part1, part0);
                    }
                    else
                    {
                        // If part1 equals the artist, then part0 is the title
                        if (part1.Equals(cleanA, StringComparison.OrdinalIgnoreCase))
                        {
                            return (part0, cleanA);
                        }
                        // Default YouTube format: part0 is Artist, part1 is Title
                        return (part1, part0);
                    }
                }
            }

            return (CleanSingleString(title), cleanA);
        }

        public static string GetCoreTitle(string title)
        {
            if (string.IsNullOrWhiteSpace(title)) return "";
            string core = title;
            // Strip feat / ft additions e.g. (feat. Artist) or ft. Artist
            core = Regex.Replace(core, @"\s*[\(\[]\s*(?:feat\.?|ft\.?|featuring)\s+[^)\]]+[\)\]]", "", RegexOptions.IgnoreCase);
            core = Regex.Replace(core, @"\s+(?:feat\.?|ft\.?|featuring)\s+.+$", "", RegexOptions.IgnoreCase);
            return CleanSingleString(core);
        }

        private static bool IsChannelOrGenericArtist(string artist)
        {
            if (string.IsNullOrWhiteSpace(artist)) return true;
            string a = artist.Trim().ToLowerInvariant();

            string[] genericTokens = new[]
            {
                "youtube", "chrome", "edge", "brave", "firefox", "opera", "browser", "now playing",
                "records", "entertainment", "vevo", "topic", "channel", "official",
                "sony music", "warner", "universal", "musica", "aquarius", "nagaswara",
                "trinity", "88rising", "atlantic", "interscope", "def jam", "republic",
                "monstercat", "spinnin", "lyrical lemonade", "indomusic", "lirik lagu"
            };

            foreach (var token in genericTokens)
            {
                if (a.Contains(token)) return true;
            }
            return false;
        }

        private static string CleanArtistName(string artist)
        {
            if (string.IsNullOrWhiteSpace(artist)) return "";
            string a = artist.Trim();

            // Apple Music Windows appends " - {Album}"
            if (a.Contains(" - "))
            {
                var parts = a.Split(new[] { " - " }, 2, StringSplitOptions.RemoveEmptyEntries);
                if (parts.Length > 0 && !string.IsNullOrWhiteSpace(parts[0])) a = parts[0].Trim();
            }
            else if (a.Contains(" – "))
            {
                var parts = a.Split(new[] { " – " }, 2, StringSplitOptions.RemoveEmptyEntries);
                if (parts.Length > 0 && !string.IsNullOrWhiteSpace(parts[0])) a = parts[0].Trim();
            }

            a = Regex.Replace(a, @"VEVO$", "", RegexOptions.IgnoreCase).Trim();
            a = Regex.Replace(a, @"\s*-\s*Topic$", "", RegexOptions.IgnoreCase).Trim();
            a = Regex.Replace(a, @"\s+Official(?:\s+Channel)?$", "", RegexOptions.IgnoreCase).Trim();
            a = Regex.Replace(a, @"\s+(?:Records|Music|Entertainment|Channel)$", "", RegexOptions.IgnoreCase).Trim();
            return CleanSingleString(a);
        }

        private static string CleanSingleString(string s)
        {
            if (string.IsNullOrWhiteSpace(s)) return "";
            string res = s.Trim();
            res = res.Trim('"', '\'', '`', '“', '”', '‘', '’');
            res = Regex.Replace(res, @"\s+", " ").Trim();
            return res;
        }

        private string GetCacheFilePath(string title, string artist)
        {
            string safeName = $"{title}_{artist}";
            foreach (char c in Path.GetInvalidFileNameChars())
            {
                safeName = safeName.Replace(c, '_');
            }
            if (safeName.Length > 80) safeName = safeName.Substring(0, 80);
            return Path.Combine(_cacheDirectory, $"{safeName}.lrc");
        }

        private void SaveToDiskCache(string path, string content)
        {
            _ = Task.Run(() =>
            {
                try
                {
                    File.WriteAllText(path, content, Encoding.UTF8);
                }
                catch { }
            });
        }

        protected void OnPropertyChanged([CallerMemberName] string? name = null)
            => PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(name));

        private class LrclibResponse
        {
            public int Id { get; set; }
            public string? TrackName { get; set; }
            public string? ArtistName { get; set; }
            public string? AlbumName { get; set; }
            public double Duration { get; set; }
            public bool Instrumental { get; set; }
            public string? PlainLyrics { get; set; }
            public string? SyncedLyrics { get; set; }
        }
    }
}
