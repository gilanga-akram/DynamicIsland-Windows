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

        /// <summary>Machine translation of <see cref="Text"/> (kept even while translations are hidden).</summary>
        internal string TranslatedText { get; set; } = string.Empty;

        private string _translation = string.Empty;
        /// <summary>Translation shown under the line; empty when hidden or not needed.</summary>
        public string Translation
        {
            get => _translation;
            set { if (_translation != value) { _translation = value; OnPropertyChanged(); } }
        }

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

    /// <summary>
    /// Karaoke fill: turns a 0–1 line progress into a left-to-right opacity mask with a soft edge,
    /// so the glowing copy of the active lyric line lights up word by word.
    /// </summary>
    public class KaraokeMaskConverter : System.Windows.Data.IValueConverter
    {
        public object Convert(object value, Type targetType, object parameter, System.Globalization.CultureInfo culture)
        {
            double p = value is double d ? Math.Clamp(d, 0.0, 1.0) : 0.0;
            const double edge = 0.08;
            double head = p * (1 + edge); // reaches fully lit at 1.0
            var brush = new System.Windows.Media.LinearGradientBrush { StartPoint = new System.Windows.Point(0, 0), EndPoint = new System.Windows.Point(1, 0) };
            brush.GradientStops.Add(new System.Windows.Media.GradientStop(System.Windows.Media.Colors.Black, Math.Max(0, head - edge)));
            brush.GradientStops.Add(new System.Windows.Media.GradientStop(System.Windows.Media.Colors.Transparent, head));
            brush.Freeze();
            return brush;
        }

        public object ConvertBack(object value, Type targetType, object parameter, System.Globalization.CultureInfo culture)
            => throw new NotSupportedException();
    }

    public class LyricsManager : INotifyPropertyChanged
    {
        private static readonly HttpClient _httpClient = new()
        {
            // LRCLIB regularly needs 10s+ to answer; 5s meant almost every lookup timed out
            Timeout = TimeSpan.FromSeconds(20)
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
            _cacheDirectory = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData), "DynamicIslandPro", "LyricsCache2"); // v2: old cache held mismatched lyrics from unvalidated searches
            try { Directory.CreateDirectory(_cacheDirectory); } catch { }
        }

        public async Task LoadLyricsAsync(string rawTitle, string rawArtist, double durationSec, bool forceReload = false, bool isAppleMusic = false)
        {
            if (string.IsNullOrWhiteSpace(rawTitle) || rawTitle == "Apple Music" || rawTitle == "Memutar Audio")
            {
                ClearLyrics("Tidak ada lagu yang sedang diputar");
                return;
            }

            // MediaManager already split "Artist - Title"; here we only strip noise (never re-split, that swaps titles like "Song - Remix")
            var (cleanTitle, cleanArtist) = CleanTrackMetadata(rawTitle, rawArtist, structuredSource: true);
            string coreTitle = GetCoreTitle(cleanTitle);

            string trackKey = $"{cleanTitle}||{cleanArtist}";

            if (!forceReload && trackKey == _currentTrackKey && (HasLyrics || IsLoading))
            {
                return; // Already current track
            }

            bool isNewTrack = trackKey != _currentTrackKey;
            _currentTrackKey = trackKey;
            _fetchCts?.Cancel();
            _fetchCts = new CancellationTokenSource();
            var ct = _fetchCts.Token;

            if (isNewTrack)
            {
                // A manual sync offset belongs to the song it was set on
                _retryCount = 0;
                _dispatcher.Invoke(ResetOffset);
            }

            // Check in-memory cache first (unless force reload requested)
            if (!forceReload && _memoryCache.TryGetValue(trackKey, out var cached))
            {
                ApplyLyricsResult(trackKey, cached.HasLyrics, cached.Lines, cached.PlainText);
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
                        ApplyLyricsResult(trackKey, true, diskLines, diskLrc);
                        return;
                    }
                }
                catch { }
            }

            // Don't leave the previous song's lyrics on screen while searching
            _dispatcher.Invoke(() =>
            {
                Lines = new ObservableCollection<LyricLine>();
                HasLyrics = false;
                IsInstrumental = false;
                _activeIndex = -1;
                CurrentLyricText = "";
                NextLyricText = "";
                LyricsSource = "";
                StatusMessage = "Mencari lirik realtime...";
                IsLoading = true;
            });

            try
            {
                // Debounce to avoid spamming APIs during rapid skipping
                await Task.Delay(250, ct);

                var found = await FindLyricsAsync(cleanTitle, coreTitle, cleanArtist, durationSec, isAppleMusic, ct);
                if (ct.IsCancellationRequested || trackKey != _currentTrackKey) return;

                if (found.Candidate != null)
                {
                    var c = found.Candidate;
                    _memoryCache[trackKey] = (true, c.Lines, c.Plain);
                    LyricsSource = c.Source;
                    if (c.IsSynced) SaveToDiskCache(cacheFile, c.CacheText);
                    ApplyLyricsResult(trackKey, true, c.Lines, c.Plain, c.IsInstrumental);
                    return;
                }

                if (found.NetworkFailed)
                {
                    // Servers too slow / unreachable: not a real "not found", so don't cache it and try again shortly
                    bool willRetry = _retryCount < MaxAutoRetries;
                    ApplyLyricsResult(trackKey, false, new List<LyricLine>(), "",
                        status: willRetry ? "Server lirik lambat, mencoba lagi..." : "Server lirik tidak merespons. Tekan coba lagi.");
                    if (willRetry) ScheduleRetry(rawTitle, rawArtist, durationSec, isAppleMusic, trackKey, ct);
                    return;
                }

                _memoryCache[trackKey] = (false, new List<LyricLine>(), "");
                ApplyLyricsResult(trackKey, false, new List<LyricLine>(), "");
            }
            catch (OperationCanceledException)
            {
                // Normal track skip cancellation
            }
            catch
            {
                ApplyLyricsResult(trackKey, false, new List<LyricLine>(), "");
            }
        }

        private const int MaxAutoRetries = 2;
        private int _retryCount = 0;

        private void ScheduleRetry(string rawTitle, string rawArtist, double durationSec, bool isAppleMusic, string trackKey, CancellationToken ct)
        {
            _retryCount++;
            _ = Task.Delay(TimeSpan.FromSeconds(10), ct).ContinueWith(t =>
            {
                if (t.IsCanceled || trackKey != _currentTrackKey) return;
                _dispatcher.InvokeAsync(() => LoadLyricsAsync(rawTitle, rawArtist, durationSec, forceReload: true, isAppleMusic: isAppleMusic));
            }, TaskScheduler.Default);
        }

        private sealed class LyricsCandidate
        {
            public List<LyricLine> Lines { get; init; } = new();
            public string Plain { get; init; } = "";
            public string CacheText { get; init; } = "";
            public string Source { get; init; } = "";
            public bool IsSynced { get; init; }
            public bool IsInstrumental { get; init; }
        }

        private sealed class FetchStats
        {
            public int LrclibAnswers;
            public int LrclibFailures;
        }

        /// <summary>
        /// Runs the lyric sources in parallel (LRCLIB can take 10s+ per request, so a sequential chain never finishes)
        /// and returns the first validated synced result, falling back to plain lyrics.
        /// </summary>
        private async Task<(LyricsCandidate? Candidate, bool NetworkFailed)> FindLyricsAsync(
            string title, string coreTitle, string artist, double durationSec, bool isAppleMusic, CancellationToken ct)
        {
            var stats = new FetchStats();
            bool hasArtist = !string.IsNullOrWhiteSpace(artist) && !IsChannelOrGenericArtist(artist);

            // Phase 1: precise lookups (exact LRCLIB get + structured search, Apple Music for Apple sources)
            var phase1 = new List<Func<CancellationToken, Task<LyricsCandidate?>>>();
            if (hasArtist)
            {
                phase1.Add(c => LrclibGetCandidateAsync(coreTitle, artist, durationSec, stats, c));
                phase1.Add(c => LrclibSearchCandidateAsync(coreTitle, artist, null, coreTitle, artist, hasArtist, durationSec, stats, c));
            }
            // Apple Music lyrics usually answer in ~1s while LRCLIB can take 20s, so race them for every source
            if (isAppleMusic || hasArtist) phase1.Add(c => AppleCandidateAsync(title, coreTitle, artist, c));

            // Phase 2: fuzzy keyword search (YouTube titles). Started right away so the slow LRCLIB requests overlap,
            // but its results are only used when phase 1 has no synced lyrics (phase 1 is more precise).
            var phase2 = new List<Func<CancellationToken, Task<LyricsCandidate?>>>
            {
                c => LrclibSearchCandidateAsync(null, null, hasArtist ? $"{artist} {coreTitle}" : coreTitle, coreTitle, artist, hasArtist, durationSec, stats, c)
            };
            if (hasArtist) phase2.Add(c => LrclibSearchCandidateAsync(coreTitle, null, null, coreTitle, artist, hasArtist, durationSec, stats, c));

            using var lookupCts = CancellationTokenSource.CreateLinkedTokenSource(ct);
            var phase1Tasks = StartAll(phase1, lookupCts.Token);
            var phase2Tasks = StartAll(phase2, lookupCts.Token);

            try
            {
                var best1 = await FirstSyncedOrBestAsync(phase1Tasks, ct);
                if (best1 != null && best1.IsSynced) return (best1, false);

                var best2 = await FirstSyncedOrBestAsync(phase2Tasks, ct);
                if (best2 != null && best2.IsSynced) return (best2, false);

                var plainBest = best1 ?? best2;
                if (plainBest != null) return (plainBest, false);
            }
            finally
            {
                lookupCts.Cancel(); // Stop whatever is still running once we have an answer
            }

            // Phase 3: plain lyrics from lyrics.ovh
            if (hasArtist && !string.IsNullOrWhiteSpace(coreTitle))
            {
                string? ovh = await FetchFromLyricsOvhAsync(coreTitle, artist, ct);
                if (!string.IsNullOrWhiteSpace(ovh))
                {
                    return (new LyricsCandidate
                    {
                        Lines = ConvertPlainLyricsToTimed(ovh, durationSec),
                        Plain = ovh,
                        Source = "Web"
                    }, false);
                }
            }

            // LRCLIB never answered at all => connectivity problem, not a missing song
            return (null, stats.LrclibAnswers == 0 && stats.LrclibFailures > 0);
        }

        private static List<Task<LyricsCandidate?>> StartAll(List<Func<CancellationToken, Task<LyricsCandidate?>>> sources, CancellationToken token)
        {
            return sources.Select(s => SafeRun(s, token)).ToList();

            static async Task<LyricsCandidate?> SafeRun(Func<CancellationToken, Task<LyricsCandidate?>> f, CancellationToken token)
            {
                try { return await f(token); } catch { return null; }
            }
        }

        private static async Task<LyricsCandidate?> FirstSyncedOrBestAsync(List<Task<LyricsCandidate?>> tasks, CancellationToken ct)
        {
            var pending = tasks.ToList();
            LyricsCandidate? fallback = null;
            while (pending.Count > 0)
            {
                var done = await Task.WhenAny(pending);
                pending.Remove(done);
                var cand = await done;
                if (cand == null) continue;
                if (cand.IsSynced) return cand;
                fallback ??= cand;
            }
            ct.ThrowIfCancellationRequested();
            return fallback;
        }

        private async Task<LyricsCandidate?> AppleCandidateAsync(string title, string coreTitle, string artist, CancellationToken ct)
        {
            var r = await TryFetchAppleMusicMultiAsync(title, coreTitle, artist, ct);
            if (r == null || r.Value.lines.Count == 0) return null;
            return new LyricsCandidate { Lines = r.Value.lines, CacheText = r.Value.lrc, Source = "Apple Music", IsSynced = true };
        }

        private async Task<LyricsCandidate?> LrclibGetCandidateAsync(string title, string artist, double durationSec, FetchStats stats, CancellationToken ct)
        {
            var r = await FetchFromLrclibGetAsync(title, artist, durationSec, stats, ct);
            if (r == null || !IsPlausibleMatch(r, title, artist, true, durationSec, out _)) return null;
            return ToCandidate(r, durationSec);
        }

        private async Task<LyricsCandidate?> LrclibSearchCandidateAsync(string? trackName, string? artistName, string? query,
            string coreTitle, string artist, bool hasArtist, double durationSec, FetchStats stats, CancellationToken ct)
        {
            var items = await FetchFromLrclibSearchAsync(trackName, artistName, query, stats, ct);
            var r = PickBestLrclibItem(items, coreTitle, artist, hasArtist, durationSec);
            return r == null ? null : ToCandidate(r, durationSec);
        }

        private LyricsCandidate? ToCandidate(LrclibResponse r, double durationSec)
        {
            string synced = r.SyncedLyrics ?? "";
            string plain = r.PlainLyrics ?? "";

            if (!string.IsNullOrWhiteSpace(synced))
            {
                var parsed = ParseLrcString(synced);
                if (parsed.Count > 0)
                    return new LyricsCandidate { Lines = parsed, Plain = plain, CacheText = synced, Source = "LRCLIB", IsSynced = true, IsInstrumental = r.Instrumental };
            }

            if (!string.IsNullOrWhiteSpace(plain))
                return new LyricsCandidate { Lines = ConvertPlainLyricsToTimed(plain, durationSec), Plain = plain, Source = "LRCLIB", IsInstrumental = r.Instrumental };

            if (r.Instrumental)
            {
                // Instrumental is a definitive answer, treat it like a synced hit
                return new LyricsCandidate
                {
                    Lines = new List<LyricLine> { new() { Timestamp = TimeSpan.Zero, Text = "🎵 Musik Instrumental" } },
                    Source = "LRCLIB",
                    IsSynced = true,
                    IsInstrumental = true
                };
            }
            return null;
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

            // (Title-only lookups were removed: they returned lyrics of unrelated songs with the same name)
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

        private static readonly JsonSerializerOptions _jsonOptions = new() { PropertyNameCaseInsensitive = true };

        private async Task<LrclibResponse?> FetchFromLrclibGetAsync(string title, string artist, double durationSec, FetchStats stats, CancellationToken ct)
        {
            try
            {
                string url = $"https://lrclib.net/api/get?track_name={Uri.EscapeDataString(title)}&artist_name={Uri.EscapeDataString(artist)}";
                if (durationSec > 20) url += $"&duration={(int)Math.Round(durationSec)}";

                using var response = await _httpClient.GetAsync(url, ct);
                Interlocked.Increment(ref stats.LrclibAnswers);
                if (response.IsSuccessStatusCode)
                {
                    var json = await response.Content.ReadAsStringAsync(ct);
                    return JsonSerializer.Deserialize<LrclibResponse>(json, _jsonOptions);
                }
            }
            catch (Exception) when (!ct.IsCancellationRequested)
            {
                Interlocked.Increment(ref stats.LrclibFailures);
            }
            catch { }
            return null;
        }

        private async Task<List<LrclibResponse>?> FetchFromLrclibSearchAsync(string? trackName, string? artistName, string? query, FetchStats stats, CancellationToken ct)
        {
            string url;
            if (!string.IsNullOrWhiteSpace(query))
            {
                url = $"https://lrclib.net/api/search?q={Uri.EscapeDataString(query)}";
            }
            else if (!string.IsNullOrWhiteSpace(trackName))
            {
                url = $"https://lrclib.net/api/search?track_name={Uri.EscapeDataString(trackName)}";
                if (!string.IsNullOrWhiteSpace(artistName)) url += $"&artist_name={Uri.EscapeDataString(artistName)}";
            }
            else return null;

            for (int retry = 0; retry < 2; retry++)
            {
                try
                {
                    using var response = await _httpClient.GetAsync(url, ct);
                    if ((int)response.StatusCode == 503 || (int)response.StatusCode == 429)
                    {
                        await Task.Delay(500, ct);
                        continue;
                    }
                    Interlocked.Increment(ref stats.LrclibAnswers);
                    if (response.IsSuccessStatusCode)
                    {
                        var json = await response.Content.ReadAsStringAsync(ct);
                        return JsonSerializer.Deserialize<List<LrclibResponse>>(json, _jsonOptions);
                    }
                    return null;
                }
                catch (Exception) when (!ct.IsCancellationRequested)
                {
                    Interlocked.Increment(ref stats.LrclibFailures);
                    return null; // A timeout already cost 20s; don't double it
                }
            }
            return null;
        }

        private static LrclibResponse? PickBestLrclibItem(List<LrclibResponse>? items, string coreTitle, string artist, bool hasArtist, double durationSec)
        {
            if (items == null || items.Count == 0) return null;

            var scored = new List<(LrclibResponse Item, double Score)>();
            foreach (var item in items)
            {
                if (!IsPlausibleMatch(item, coreTitle, artist, hasArtist, durationSec, out bool artistOk)) continue;

                double score = 0;
                if (!string.IsNullOrWhiteSpace(item.SyncedLyrics)) score += 100;
                if (artistOk) score += 50;
                if (NormalizeForMatch(GetCoreTitle(item.TrackName ?? "")) == NormalizeForMatch(coreTitle)) score += 20;
                if (durationSec > 20 && item.Duration > 0) score -= Math.Min(30, Math.Abs(item.Duration - durationSec));
                scored.Add((item, score));
            }

            return scored.Count == 0 ? null : scored.OrderByDescending(s => s.Score).First().Item;
        }

        /// <summary>
        /// Rejects search hits that are clearly another song (the old code accepted the first result of any search,
        /// which is how a TikTok clip ended up with someone else's lyrics).
        /// </summary>
        private static bool IsPlausibleMatch(LrclibResponse r, string coreTitle, string artist, bool hasArtist, double durationSec, out bool artistOk)
        {
            artistOk = false;
            string rt = NormalizeForMatch(GetCoreTitle(r.TrackName ?? ""));
            string t = NormalizeForMatch(coreTitle);
            if (rt.Length == 0 || t.Length == 0) return false;

            bool titleExact = rt == t;
            bool titleOk = titleExact
                           || (t.Length >= 4 && rt.Contains(t))
                           || (rt.Length >= 4 && t.Contains(rt))
                           || TokenOverlap(rt, t) >= 0.75;
            if (!titleOk) return false;

            if (hasArtist)
            {
                string ra = NormalizeForMatch(r.ArtistName ?? "");
                string a = NormalizeForMatch(artist);
                artistOk = ra.Length > 0 && a.Length > 0 &&
                           (ra.Contains(a) || a.Contains(ra) || TokenOverlap(ra, a) >= 0.5);
                if (artistOk) return true;
            }

            if (durationSec > 20 && r.Duration > 0)
            {
                // Music videos often have intros/outros, so allow more slack when the title is an exact match
                double diff = Math.Abs(r.Duration - durationSec);
                return titleExact ? diff <= 30 : diff <= 8;
            }

            return titleExact;
        }

        private static string NormalizeForMatch(string s)
        {
            if (string.IsNullOrWhiteSpace(s)) return "";
            var sb = new StringBuilder(s.Length);
            foreach (char ch in s.Normalize(NormalizationForm.FormD))
            {
                var cat = System.Globalization.CharUnicodeInfo.GetUnicodeCategory(ch);
                if (cat == System.Globalization.UnicodeCategory.NonSpacingMark) continue;
                if (char.IsLetterOrDigit(ch)) sb.Append(char.ToLowerInvariant(ch));
                else if (ch == '&') sb.Append(" and ");
                else sb.Append(' ');
            }
            return Regex.Replace(sb.ToString(), @"\s+", " ").Trim();
        }

        private static double TokenOverlap(string a, string b)
        {
            var ta = a.Split(' ', StringSplitOptions.RemoveEmptyEntries).ToHashSet();
            var tb = b.Split(' ', StringSplitOptions.RemoveEmptyEntries).ToHashSet();
            if (ta.Count == 0 || tb.Count == 0) return 0;
            int common = ta.Count(x => tb.Contains(x));
            return (double)common / Math.Min(ta.Count, tb.Count);
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

        private void ApplyLyricsResult(string trackKey, bool hasLyrics, List<LyricLine> lines, string plain, bool instrumental = false, string? status = null)
        {
            _dispatcher.Invoke(() =>
            {
                // A slower lookup for a previous song must not overwrite the current one
                if (trackKey != _currentTrackKey) return;

                Lines = new ObservableCollection<LyricLine>(lines);
                HasLyrics = hasLyrics && lines.Count > 0;
                IsInstrumental = instrumental;
                _activeIndex = -1;
                CurrentLyricText = "";
                NextLyricText = HasLyrics ? Lines[0].Text : "";
                CurrentTranslation = "";
                StatusMessage = status ?? (HasLyrics ? "" : "Lirik tidak ditemukan untuk lagu ini");
                IsLoading = false;
                foreach (var l in Lines) l.Translation = _showTranslation ? l.TranslatedText : "";
            });

            if (hasLyrics && !instrumental && lines.Count > 0)
                _ = TranslateLinesAsync(trackKey, lines);
        }

        // ───────────────────────── Lyric translation ─────────────────────────

        private bool _showTranslation = SettingsManager.Instance.LyricsTranslate;
        /// <summary>Show a translated line under every lyric line (persisted in the settings).</summary>
        public bool ShowTranslation
        {
            get => _showTranslation;
            set
            {
                if (_showTranslation == value) return;
                _showTranslation = value;
                SettingsManager.Instance.LyricsTranslate = value;
                OnPropertyChanged();
                foreach (var l in Lines) l.Translation = value ? l.TranslatedText : "";
                UpdateCurrentTranslation();
                if (value && HasLyrics && !IsInstrumental && !Lines.Any(l => l.TranslatedText.Length > 0))
                    _ = TranslateLinesAsync(_currentTrackKey, Lines.ToList());
            }
        }

        private bool _isTranslating;
        public bool IsTranslating
        {
            get => _isTranslating;
            private set { if (_isTranslating != value) { _isTranslating = value; OnPropertyChanged(); } }
        }

        private string _currentTranslation = "";
        /// <summary>Translation of the line being sung (for the compact views).</summary>
        public string CurrentTranslation
        {
            get => _currentTranslation;
            private set { if (_currentTranslation != value) { _currentTranslation = value; OnPropertyChanged(); } }
        }

        private void UpdateCurrentTranslation()
        {
            CurrentTranslation = _showTranslation && _activeIndex >= 0 && _activeIndex < Lines.Count ? Lines[_activeIndex].TranslatedText : "";
        }

        /// <summary>Language the lyrics are translated into: the app language (Indonesian by default).</summary>
        private static string TranslationTarget
        {
            get
            {
                string lang = LocalizationManager.Instance.CurrentLanguage;
                return string.IsNullOrWhiteSpace(lang) ? "id" : lang.ToLowerInvariant();
            }
        }

        private readonly Dictionary<string, Dictionary<string, string>> _translationCache = new(StringComparer.OrdinalIgnoreCase);
        private readonly HashSet<string> _translationsRunning = new(StringComparer.OrdinalIgnoreCase);

        private async Task TranslateLinesAsync(string trackKey, List<LyricLine> lines)
        {
            if (!_showTranslation || string.IsNullOrEmpty(trackKey)) return;
            string target = TranslationTarget;
            string key = trackKey + "||" + target;
            if (!_translationsRunning.Add(key)) return;

            try
            {
                var texts = lines.Select(l => l.Text.Trim())
                                 .Where(t => t.Length > 1 && t.Any(char.IsLetter))
                                 .Distinct(StringComparer.Ordinal)
                                 .ToList();
                if (texts.Count == 0) return;

                if (!_translationCache.TryGetValue(key, out var map))
                {
                    string file = GetTranslationCacheFilePath(trackKey, target);
                    map = await Task.Run(() => LoadTranslationFile(file));
                    if (map == null)
                    {
                        _dispatcher.Invoke(() => { if (trackKey == _currentTrackKey) IsTranslating = true; });
                        map = await FetchTranslationsAsync(texts, target);
                        if (map == null) return; // offline: try again next time this song is loaded
                        SaveTranslationFile(file, map);
                    }
                    _translationCache[key] = map;
                }

                _dispatcher.Invoke(() =>
                {
                    if (trackKey != _currentTrackKey) return;
                    foreach (var l in lines)
                    {
                        l.TranslatedText = map.TryGetValue(l.Text.Trim(), out var tr) ? tr : "";
                        l.Translation = _showTranslation ? l.TranslatedText : "";
                    }
                    UpdateCurrentTranslation();
                });
            }
            catch { }
            finally
            {
                _translationsRunning.Remove(key);
                _dispatcher.Invoke(() => { if (trackKey == _currentTrackKey) IsTranslating = false; });
            }
        }

        /// <summary>
        /// Translates the lines in a few batched requests (one line per row). Returns text→translation,
        /// leaving out lines that are already in the target language. Null when the service can't be reached.
        /// </summary>
        private static async Task<Dictionary<string, string>?> FetchTranslationsAsync(List<string> texts, string target)
        {
            var result = new Dictionary<string, string>(StringComparer.Ordinal);
            bool anyAnswer = false;

            // Batches of ~1500 characters keep the request small and the line split reliable
            var batches = new List<List<string>>();
            var cur = new List<string>(); int len = 0;
            foreach (var t in texts)
            {
                if (cur.Count > 0 && len + t.Length > 1500) { batches.Add(cur); cur = new List<string>(); len = 0; }
                cur.Add(t); len += t.Length + 1;
            }
            if (cur.Count > 0) batches.Add(cur);

            foreach (var batch in batches)
            {
                var (rows, srcLang) = await GoogleTranslateAsync(string.Join("\n", batch), target);
                if (rows == null) continue;
                anyAnswer = true;
                if (string.Equals(srcLang, target, StringComparison.OrdinalIgnoreCase)) continue; // already in the app language

                if (rows.Count == batch.Count)
                {
                    for (int i = 0; i < batch.Count; i++) AddTranslation(result, batch[i], rows[i]);
                }
                else
                {
                    // The service merged or split rows: translate this batch line by line instead
                    using var gate = new SemaphoreSlim(4);
                    var tasks = batch.Select(async line =>
                    {
                        await gate.WaitAsync();
                        try
                        {
                            var (r, _) = await GoogleTranslateAsync(line, target);
                            return (line, tr: r == null ? "" : string.Join(" ", r));
                        }
                        finally { gate.Release(); }
                    }).ToList();
                    foreach (var (line, tr) in await Task.WhenAll(tasks))
                        lock (result) AddTranslation(result, line, tr);
                }
            }
            return anyAnswer ? result : null;

            static void AddTranslation(Dictionary<string, string> map, string original, string translated)
            {
                translated = (translated ?? "").Trim();
                if (translated.Length == 0) return;
                if (NormalizeForMatch(translated) == NormalizeForMatch(original)) return; // same line (name, "oh oh", already translated)
                map[original] = translated;
            }
        }

        private static async Task<(List<string>? Rows, string SourceLang)> GoogleTranslateAsync(string text, string target)
        {
            try
            {
                string url = $"https://translate.googleapis.com/translate_a/single?client=gtx&sl=auto&tl={Uri.EscapeDataString(target)}&dt=t";
                using var content = new FormUrlEncodedContent(new[] { new KeyValuePair<string, string>("q", text) });
                using var cts = new CancellationTokenSource(TimeSpan.FromSeconds(12));
                using var res = await _httpClient.PostAsync(url, content, cts.Token);
                if (!res.IsSuccessStatusCode) return (null, "");

                using var doc = JsonDocument.Parse(await res.Content.ReadAsStringAsync(cts.Token));
                var root = doc.RootElement;
                var sb = new StringBuilder();
                if (root.ValueKind == JsonValueKind.Array && root.GetArrayLength() > 0 && root[0].ValueKind == JsonValueKind.Array)
                {
                    foreach (var seg in root[0].EnumerateArray())
                    {
                        if (seg.ValueKind == JsonValueKind.Array && seg.GetArrayLength() > 0 && seg[0].ValueKind == JsonValueKind.String)
                            sb.Append(seg[0].GetString());
                    }
                }
                string src = root.ValueKind == JsonValueKind.Array && root.GetArrayLength() > 2 && root[2].ValueKind == JsonValueKind.String
                    ? root[2].GetString() ?? "" : "";
                var rows = sb.ToString().Replace("\r", "").Split('\n').Select(r => r.Trim()).ToList();
                while (rows.Count > 0 && rows[^1].Length == 0) rows.RemoveAt(rows.Count - 1);
                return (rows, src);
            }
            catch
            {
                return (null, "");
            }
        }

        private string GetTranslationCacheFilePath(string trackKey, string target)
        {
            string safeName = trackKey.Replace("||", "_");
            foreach (char c in Path.GetInvalidFileNameChars()) safeName = safeName.Replace(c, '_');
            if (safeName.Length > 80) safeName = safeName.Substring(0, 80);
            return Path.Combine(_cacheDirectory, $"{safeName}.{target}.tr.json");
        }

        private static Dictionary<string, string>? LoadTranslationFile(string path)
        {
            try
            {
                if (!File.Exists(path)) return null;
                return JsonSerializer.Deserialize<Dictionary<string, string>>(File.ReadAllText(path, Encoding.UTF8));
            }
            catch { return null; }
        }

        private static void SaveTranslationFile(string path, Dictionary<string, string> map)
        {
            _ = Task.Run(() =>
            {
                try { File.WriteAllText(path, JsonSerializer.Serialize(map), Encoding.UTF8); } catch { }
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
                CurrentTranslation = "";
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

            if (newIndex == -1)
            {
                // Intro before the first lyric line: show a music note instead of highlighting line 1 early
                if (_activeIndex != -1 || CurrentLyricText != "♪")
                {
                    _activeIndex = -1;
                    foreach (var l in Lines) { l.IsActive = false; l.IsPast = false; l.Progress = 0.0; }
                    CurrentLyricText = "♪";
                    NextLyricText = Lines[0].Text;
                    CurrentTranslation = "";
                    CurrentLineProgress = 0.0;
                    ActiveLineChanged?.Invoke(-1);
                }
                return;
            }

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
                UpdateCurrentTranslation();
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
            => CleanTrackMetadata(rawTitle, rawArtist, structuredSource: false);

        // Bracket groups like "(Official HD Video)", "(Explicit Video)", "[4K Upgrade]" that are never part of the song name
        private static readonly Regex _noiseBracketContent = new(
            @"\b(?:official|video|audio|lyrics?|lirik|mv|m/v|visuali[sz]er|explicit|clean|hd|hq|4k|8k|1080p|720p|remaster(?:ed)?|upgrade|subs?|subtitle|terjemahan|karaoke|teaser|trailer|performance|clip|klip)\b",
            RegexOptions.IgnoreCase | RegexOptions.Compiled);

        // "Song - Remix" style suffixes: the part after the dash is a version, not an artist/title split
        private static readonly Regex _versionSuffix = new(
            @"\b(?:remix|mix|edit|version|versi|live|acoustic|akustik|remaster(?:ed)?|instrumental|cover|karaoke|slowed|sped\s*up|reverb|from)\b",
            RegexOptions.IgnoreCase | RegexOptions.Compiled);

        /// <param name="structuredSource">
        /// True for Apple Music / Spotify (and for already-cleaned values): the title is the song name,
        /// so it is never split on " - " (which used to turn "Song - Live" into title "Live", artist "Song").
        /// </param>
        public static (string Title, string Artist) CleanTrackMetadata(string rawTitle, string rawArtist, bool structuredSource)
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
            title = Regex.Replace(title, @"\s*[\(\[【]([^\)\]】]*)[\)\]】]",
                m => _noiseBracketContent.IsMatch(m.Groups[1].Value) ? "" : m.Value).Trim();
            // Unbracketed trailing tags: "Song. Official Video", "Song | Official Music Video", "Song - Video Klip Resmi"
            title = Regex.Replace(title,
                @"[\s\.\-|,:]+(?:Official\s+(?:Music\s+|Lyrics?\s+|HD\s+|4K\s+)?(?:Video|Audio|MV|Visuali[sz]er)|Lyrics?\s+Video|Music\s+Video|Video\s+(?:Klip|Lirik)(?:\s+Resmi)?)\b.*$",
                "", RegexOptions.IgnoreCase).Trim();

            if (structuredSource)
            {
                return (CleanSingleString(title), CleanArtistName(artist));
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
            string[] separators = new[] { " - ", " – ", " — ", " ♦ ", " • ", " ・ ", " ｜ ", " : ", " | ", " ~ ", " // ", " / ", " by " };
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

                    if (IsChannelOrGenericArtist(cleanA))
                    {
                        // On YouTube, "Artist - Title" is the standard format
                        return (part1, part0);
                    }

                    string nA = NormalizeForMatch(cleanA);
                    string n0 = NormalizeForMatch(part0);
                    string n1 = NormalizeForMatch(part1);

                    // "TULUS - Hati-Hati di Jalan" uploaded by channel "Tulus"
                    if (n0.Length > 0 && (n0.Contains(nA) || nA.Contains(n0)))
                    {
                        return (part1, part0);
                    }
                    // "Hati-Hati di Jalan - Tulus"
                    if (n1.Length > 0 && (n1.Contains(nA) || nA.Contains(n1)))
                    {
                        return (part0, cleanA);
                    }
                    // Real artist + "Song - Remix" (YouTube Music): keep the whole title
                    if (_versionSuffix.IsMatch(part1))
                    {
                        return (CleanSingleString(title), cleanA);
                    }
                    // Default YouTube format: part0 is Artist, part1 is Title
                    return (part1, part0);
                }
            }

            // "Artist Title" without a separator, e.g. "Morfem (Indonesia) Rayakan Pemenang" on channel "MORFEMoriginal"
            if (!IsChannelOrGenericArtist(cleanA) && cleanA.Length >= 2)
            {
                var m = Regex.Match(title, "^" + Regex.Escape(cleanA) + @"\b\s*(?:\([^)]*\)|\[[^\]]*\])?\s*[:\-–—]?\s*(.+)$", RegexOptions.IgnoreCase);
                if (m.Success && m.Groups[1].Value.Trim().Length >= 2)
                {
                    return (CleanSingleString(m.Groups[1].Value), cleanA);
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
            // Soundtrack / remaster suffixes: "Song - From \"Movie\"", "Song (From Movie)", "Song - 2011 Remaster"
            core = Regex.Replace(core, @"\s*[\(\[]\s*From\s+[^)\]]+[\)\]]", "", RegexOptions.IgnoreCase);
            core = Regex.Replace(core, @"\s+[-–—]\s+(?:From\s+.+|(?:\d{4}\s+)?Remaster(?:ed)?(?:\s+\d{4})?(?:\s+Version)?)$", "", RegexOptions.IgnoreCase);
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
                // Whole words only, so real artists like "The Edge" or "Hedgehog" aren't treated as channels
                if (Regex.IsMatch(a, $@"(?:^|\W){Regex.Escape(token)}(?:$|\W)")) return true;
            }
            return a.EndsWith("vevo");
        }

        private static string CleanArtistName(string artist)
        {
            if (string.IsNullOrWhiteSpace(artist)) return "";
            string a = artist.Trim();

            // Apple Music for Windows reports "Artist — Album" (em dash); YouTube channels use "Artist - Topic"
            foreach (var sep in new[] { " — ", " – ", " - " })
            {
                int idx = a.IndexOf(sep, StringComparison.Ordinal);
                if (idx > 0)
                {
                    a = a.Substring(0, idx).Trim();
                    break;
                }
            }

            a = Regex.Replace(a, @"VEVO$", "", RegexOptions.IgnoreCase).Trim();
            a = Regex.Replace(a, @"\s*-\s*Topic$", "", RegexOptions.IgnoreCase).Trim();
            a = Regex.Replace(a, @"\s+Official(?:\s+Channel)?$", "", RegexOptions.IgnoreCase).Trim();
            a = Regex.Replace(a, @"\s+(?:Records|Music|Entertainment|Channel|Original|TV)$", "", RegexOptions.IgnoreCase).Trim();
            // Glued channel suffixes: "MORFEMoriginal", "TulusOfficial", "NoahMusic"
            a = Regex.Replace(a, @"(?<=[A-Z0-9])(?:official|original|music|tv)$", "").Trim();
            a = Regex.Replace(a, @"(?<=[a-z0-9])(?:Official|Original|Music|TV)$", "").Trim();
            return CleanSingleString(a);
        }

        private static string CleanSingleString(string s)
        {
            if (string.IsNullOrWhiteSpace(s)) return "";
            string res = s.Trim();
            res = res.Trim(' ', '♦', '•', '・', '|', '｜', '~', '-', '–', '—');
            // Only unwrap quotes that enclose the whole string ('"Song"'), never strip a lone closing quote
            if (res.Length > 1 && "\"'`“‘".Contains(res[0]) && "\"'`”’".Contains(res[^1]))
            {
                res = res.Substring(1, res.Length - 2);
            }
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
