// Language: C#
// Filename: MediaManager.cs
// Runs on: Windows 10 / Windows 11 (.NET 9.0-windows10.0.19041.0)
// Targets: Real-time GSMTC Audio/Video Playback Tracking, Timeline Seeking, Album Art & Media Controls

using System;
using System.ComponentModel;
using System.Diagnostics;
using System.IO;
using System.Runtime.CompilerServices;
using System.Threading.Tasks;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using System.Windows.Threading;
using Windows.Media.Control;

namespace DynamicIsland
{
    public class MediaManager : INotifyPropertyChanged
    {
        private GlobalSystemMediaTransportControlsSessionManager? _sessionManager;
        private GlobalSystemMediaTransportControlsSession? _currentSession;

        private string _songTitle = "Apple Music";
        private string _artistName = "Siap memutar musik";
        private string _sourceApp = "Apple Music";
        private bool _isPlaying = false;
        private bool _isAppleMusic = true;
        private ImageSource? _albumArt;
        private bool _hasAlbumArt = false;

        // Realtime Timeline Properties
        private string _currentPosition = "0:00";
        private string _totalDuration = "-0:00";
        private double _timelineProgress = 0.0;
        private TimeSpan _timelinePosition = TimeSpan.Zero;
        private TimeSpan _timelineEndTime = TimeSpan.Zero;
        private TimeSpan _timelineStartTime = TimeSpan.Zero;
        private DateTimeOffset _timelineLastUpdated = DateTimeOffset.MinValue;
        private readonly System.Diagnostics.Stopwatch _playStopwatch = new();
        private TimeSpan _baselinePosition = TimeSpan.Zero;
        private TimeSpan _lastRawPosition = TimeSpan.MinValue;
        private DateTimeOffset _lastRawUpdatedTime = DateTimeOffset.MinValue;

        private readonly Dispatcher _dispatcher;
        private readonly DispatcherTimer _pollTimer;
        private readonly DispatcherTimer _liveTimelineTimer;
        private bool _isSeeking = false;

        public string SongTitle
        {
            get => _songTitle;
            set { if (_songTitle != value) { _songTitle = value; OnPropertyChanged(); } }
        }

        public string ArtistName
        {
            get => _artistName;
            set { if (_artistName != value) { _artistName = value; OnPropertyChanged(); } }
        }

        public string SourceApp
        {
            get => _sourceApp;
            set { if (_sourceApp != value) { _sourceApp = value; OnPropertyChanged(); } }
        }

        public bool IsPlaying
        {
            get => _isPlaying;
            set
            {
                if (_isPlaying != value)
                {
                    _isPlaying = value;
                    if (_isPlaying)
                    {
                        if (!_playStopwatch.IsRunning) _playStopwatch.Start();
                    }
                    else
                    {
                        if (_playStopwatch.IsRunning)
                        {
                            _baselinePosition += _playStopwatch.Elapsed;
                            _playStopwatch.Reset();
                        }
                    }
                    OnPropertyChanged();
                }
            }
        }

        public bool IsAppleMusic
        {
            get => _isAppleMusic;
            set { if (_isAppleMusic != value) { _isAppleMusic = value; OnPropertyChanged(); } }
        }

        public ImageSource? AlbumArt
        {
            get => _albumArt;
            set { if (_albumArt != value) { _albumArt = value; OnPropertyChanged(); } }
        }

        public bool HasAlbumArt
        {
            get => _hasAlbumArt;
            set { if (_hasAlbumArt != value) { _hasAlbumArt = value; OnPropertyChanged(); } }
        }

        private static readonly Brush _defaultAccent = CreateFrozenBrush(Color.FromRgb(0xF2, 0xF2, 0xF7));
        private Brush _accentBrush = _defaultAccent;

        /// <summary>Vivid colour sampled from the artwork (tints the notch waveform like macOS notch apps).</summary>
        public Brush AccentBrush
        {
            get => _accentBrush;
            set { if (!ReferenceEquals(_accentBrush, value)) { _accentBrush = value; OnPropertyChanged(); } }
        }

        private static Brush CreateFrozenBrush(Color c)
        {
            var b = new SolidColorBrush(c);
            b.Freeze();
            return b;
        }

        /// <summary>
        /// Picks the most saturated mid-bright colour of a downscaled copy of the artwork,
        /// then lifts its brightness so it stays readable on the black notch.
        /// </summary>
        private static Brush ComputeAccentBrush(BitmapSource? art)
        {
            if (art == null) return _defaultAccent;
            try
            {
                double scale = 24.0 / Math.Max(art.PixelWidth, art.PixelHeight);
                var small = new FormatConvertedBitmap(new TransformedBitmap(art, new ScaleTransform(scale, scale)), PixelFormats.Bgra32, null, 0);
                int w = small.PixelWidth, h = small.PixelHeight, stride = w * 4;
                var px = new byte[h * stride];
                small.CopyPixels(px, stride, 0);

                double bestScore = -1, sumR = 0, sumG = 0, sumB = 0, weight = 0;
                for (int i = 0; i < px.Length; i += 4)
                {
                    double b = px[i] / 255.0, g = px[i + 1] / 255.0, r = px[i + 2] / 255.0;
                    double max = Math.Max(r, Math.Max(g, b)), min = Math.Min(r, Math.Min(g, b));
                    double sat = max <= 0 ? 0 : (max - min) / max;
                    // Favour colourful pixels that are neither near-black nor washed out
                    double score = sat * (max > 0.25 ? 1.0 : 0.2) * (max < 0.98 || sat > 0.3 ? 1.0 : 0.3);
                    if (score > bestScore) bestScore = score;
                    double wgt = score * score;
                    sumR += r * wgt; sumG += g * wgt; sumB += b * wgt; weight += wgt;
                }

                if (weight <= 0 || bestScore < 0.18) return _defaultAccent; // Greyscale artwork: stay neutral

                double ar = sumR / weight, ag = sumG / weight, ab = sumB / weight;
                double peak = Math.Max(ar, Math.Max(ag, ab));
                double lift = peak > 0 ? Math.Min(3.0, 0.92 / peak) : 1.0;
                byte To(double v) => (byte)Math.Clamp(v * lift * 255.0, 0, 255);
                return CreateFrozenBrush(Color.FromRgb(To(ar), To(ag), To(ab)));
            }
            catch
            {
                return _defaultAccent;
            }
        }

        public string CurrentPosition
        {
            get => _currentPosition;
            set { if (_currentPosition != value) { _currentPosition = value; OnPropertyChanged(); } }
        }

        public TimeSpan CurrentPositionTimeSpan => _baselinePosition + (_playStopwatch.IsRunning ? _playStopwatch.Elapsed : TimeSpan.Zero);

        public string TotalDuration
        {
            get => _totalDuration;
            set { if (_totalDuration != value) { _totalDuration = value; OnPropertyChanged(); } }
        }

        public double TimelineProgress
        {
            get => _timelineProgress;
            set { if (Math.Abs(_timelineProgress - value) > 0.01) { _timelineProgress = value; OnPropertyChanged(); } }
        }

        public bool IsSeeking
        {
            get => _isSeeking;
            set => _isSeeking = value;
        }

        public LyricsManager Lyrics { get; }

        public event PropertyChangedEventHandler? PropertyChanged;

        public MediaManager(Dispatcher dispatcher)
        {
            _dispatcher = dispatcher;
            Lyrics = new LyricsManager(_dispatcher);

            // Initialize Windows GSMTC session manager asynchronously
            Task.Run(InitializeSessionManagerAsync);

            // Poll every 2 seconds as fallback to ensure session continuity
            _pollTimer = new DispatcherTimer
            {
                Interval = TimeSpan.FromSeconds(2)
            };
            _pollTimer.Tick += async (s, e) => await RefreshCurrentSessionAsync();
            _pollTimer.Start();

            // High-resolution real-time live playback position ticker (33ms ~ 30 FPS)
            _liveTimelineTimer = new DispatcherTimer(DispatcherPriority.Render, _dispatcher)
            {
                Interval = TimeSpan.FromMilliseconds(33)
            };
            _liveTimelineTimer.Tick += (s, e) => UpdateLivePosition();
            _liveTimelineTimer.Start();
        }

        private async Task InitializeSessionManagerAsync()
        {
            try
            {
                _sessionManager = await GlobalSystemMediaTransportControlsSessionManager.RequestAsync();
                if (_sessionManager != null)
                {
                    _sessionManager.CurrentSessionChanged += async (s, e) => await RefreshCurrentSessionAsync();
                    _sessionManager.SessionsChanged += async (s, e) => await RefreshCurrentSessionAsync();
                    await RefreshCurrentSessionAsync();
                }
            }
            catch
            {
                // Fallback: media keys still functional via NativeMethods
            }
        }

        public async Task RefreshCurrentSessionAsync()
        {
            if (_sessionManager == null)
            {
                try
                {
                    _sessionManager = await GlobalSystemMediaTransportControlsSessionManager.RequestAsync();
                }
                catch { return; }
            }

            try
            {
                var session = PickBestSession();

                // If session changed, rebind event listeners
                if (!ReferenceEquals(_currentSession, session))
                {
                    UnhookSessionEvents(_currentSession);
                    _currentSession = session;
                    HookSessionEvents(_currentSession);
                }

                if (session == null)
                {
                    _dispatcher.Invoke(() =>
                    {
                        if (!_isPlaying)
                        {
                            SongTitle = "Apple Music";
                            ArtistName = "Buka & putar lagu favoritmu";
                            SourceApp = "Apple Music";
                            IsPlaying = false;
                            HasAlbumArt = false;
                            AlbumArt = null;
                            CurrentPosition = "0:00";
                            TotalDuration = "-0:00";
                            TimelineProgress = 0.0;
                            Lyrics.ClearLyrics("Buka & putar lagu favoritmu");
                        }
                    });
                    return;
                }

                // Check app source (Apple Music, Spotify, Chrome, Edge, YouTube, etc.)
                string appId = session.SourceAppUserModelId ?? "";
                bool isApple = IsAppleMusicAppId(appId);
                bool isSpotify = appId.Contains("Spotify", StringComparison.OrdinalIgnoreCase);
                string? browserName = GetBrowserName(appId);
                bool isBrowser = browserName != null;

                string appDisplayName = isApple ? "Apple Music"
                                      : isSpotify ? "Spotify"
                                      : isBrowser ? $"YouTube / {browserName}"
                                      : "Now Playing";

                var playbackInfo = session.GetPlaybackInfo();
                bool playing = playbackInfo?.PlaybackStatus == GlobalSystemMediaTransportControlsSessionPlaybackStatus.Playing;

                var mediaProperties = await session.TryGetMediaPropertiesAsync();

                string rawTitle = !string.IsNullOrWhiteSpace(mediaProperties?.Title) ? mediaProperties.Title : "Memutar Audio";
                string rawArtist = !string.IsNullOrWhiteSpace(mediaProperties?.Artist) ? mediaProperties.Artist : appDisplayName;

                string displayTitle = rawTitle;
                string displayArtist = rawArtist;

                // Browser (YouTube) titles are "Artist - Title (Official Video)" and need splitting;
                // Apple Music / Spotify already give the song name, they only need the album/noise stripped
                if (isBrowser || isApple || isSpotify)
                {
                    var (cleanT, cleanA) = LyricsManager.CleanTrackMetadata(rawTitle, rawArtist, structuredSource: !isBrowser);
                    if (!string.IsNullOrWhiteSpace(cleanT))
                    {
                        displayTitle = cleanT;
                        if (!string.IsNullOrWhiteSpace(cleanA) && !cleanA.Equals(cleanT, StringComparison.OrdinalIgnoreCase))
                        {
                            displayArtist = cleanA;
                        }
                    }
                }

                BitmapImage? bitmap = null;
                Brush accent = _defaultAccent;
                if (mediaProperties?.Thumbnail != null)
                {
                    try
                    {
                        using var stream = await mediaProperties.Thumbnail.OpenReadAsync();
                        using var netStream = stream.AsStreamForRead();
                        var memStream = new MemoryStream();
                        await netStream.CopyToAsync(memStream);
                        memStream.Position = 0;

                        bitmap = new BitmapImage();
                        bitmap.BeginInit();
                        bitmap.CacheOption = BitmapCacheOption.OnLoad;
                        bitmap.StreamSource = memStream;
                        bitmap.EndInit();
                        bitmap.Freeze();
                        accent = ComputeAccentBrush(bitmap);
                    }
                    catch { }
                }

                // Read GSMTC timeline properties
                try
                {
                    var timeline = session.GetTimelineProperties();
                    if (timeline != null)
                    {
                        _timelinePosition = timeline.Position;
                        _timelineEndTime = timeline.EndTime;
                        _timelineStartTime = timeline.StartTime;
                        _timelineLastUpdated = (timeline.LastUpdatedTime != DateTimeOffset.MinValue && timeline.LastUpdatedTime != default)
                            ? timeline.LastUpdatedTime
                            : DateTimeOffset.UtcNow;

                        bool rawChanged = timeline.Position != _lastRawPosition || timeline.LastUpdatedTime != _lastRawUpdatedTime;
                        _lastRawPosition = timeline.Position;
                        _lastRawUpdatedTime = timeline.LastUpdatedTime;

                        bool isNewTrack = SongTitle != displayTitle || ArtistName != displayArtist;
                        TimeSpan livePos = GetExtrapolatedSessionPosition(timeline, playing);
                        if (isNewTrack)
                        {
                            // Fresh track: start baseline at 0:00 (or raw position if mid-song), do NOT add stale ad-time
                            _baselinePosition = (timeline.Position > TimeSpan.FromSeconds(2)) ? livePos : TimeSpan.Zero;
                            if (playing) _playStopwatch.Restart();
                            else _playStopwatch.Reset();
                        }
                        else if (rawChanged)
                        {
                            TimeSpan cur = _baselinePosition + (_playStopwatch.IsRunning ? _playStopwatch.Elapsed : TimeSpan.Zero);
                            if (Math.Abs((livePos - cur).TotalSeconds) > 2.0)
                            {
                                // User seeked in browser
                                _baselinePosition = livePos;
                                if (playing) _playStopwatch.Restart();
                                else _playStopwatch.Reset();
                            }
                        }
                    }
                }
                catch { }

                _dispatcher.Invoke(() =>
                {
                    bool trackChanged = SongTitle != displayTitle || ArtistName != displayArtist;
                    SongTitle = displayTitle;
                    ArtistName = displayArtist;
                    SourceApp = appDisplayName;
                    IsPlaying = playing;
                    IsAppleMusic = isApple;

                    if (bitmap != null)
                    {
                        AlbumArt = bitmap;
                        HasAlbumArt = true;
                    }
                    else
                    {
                        HasAlbumArt = false;
                        AlbumArt = null;
                    }
                    AccentBrush = accent;

                    if (trackChanged)
                    {
                        _ = Lyrics.LoadLyricsAsync(displayTitle, displayArtist, _timelineEndTime.TotalSeconds, isAppleMusic: isApple);
                    }

                    UpdateLivePosition();
                });
            }
            catch
            {
                // Soft fail gracefully on transient media transition
            }
        }

        public double DurationSeconds => _timelineEndTime.TotalSeconds;

        private static bool IsAppleMusicAppId(string appId) =>
            appId.Contains("AppleMusic", StringComparison.OrdinalIgnoreCase) ||
            appId.Contains("AppleInc", StringComparison.OrdinalIgnoreCase) ||
            appId.Contains("iTunes", StringComparison.OrdinalIgnoreCase);

        private static string? GetBrowserName(string appId)
        {
            // Firefox registers with a hashed AppUserModelID instead of its name
            if (appId.Contains("308046B0AF4A39CB", StringComparison.OrdinalIgnoreCase) ||
                appId.Contains("Firefox", StringComparison.OrdinalIgnoreCase)) return "Firefox";
            if (appId.Contains("Edge", StringComparison.OrdinalIgnoreCase)) return "Edge";
            if (appId.Contains("Brave", StringComparison.OrdinalIgnoreCase)) return "Brave";
            if (appId.Contains("Opera", StringComparison.OrdinalIgnoreCase)) return "Opera";
            if (appId.Contains("Vivaldi", StringComparison.OrdinalIgnoreCase)) return "Vivaldi";
            if (appId.Contains("TheBrowserCompany", StringComparison.OrdinalIgnoreCase) ||
                appId.Equals("Arc", StringComparison.OrdinalIgnoreCase)) return "Arc";
            if (appId.Contains("Zen", StringComparison.OrdinalIgnoreCase)) return "Zen";
            if (appId.Contains("Thorium", StringComparison.OrdinalIgnoreCase)) return "Thorium";
            if (appId.Contains("Floorp", StringComparison.OrdinalIgnoreCase)) return "Floorp";
            if (appId.Contains("Waterfox", StringComparison.OrdinalIgnoreCase)) return "Waterfox";
            if (appId.Contains("Chrome", StringComparison.OrdinalIgnoreCase) ||
                appId.Contains("Chromium", StringComparison.OrdinalIgnoreCase)) return "Chrome";
            return null;
        }

        /// <summary>
        /// Windows' "current" session is simply the last one that grabbed focus, which is often a paused tab or
        /// an app that isn't playing music. Prefer whatever is actually playing, music apps first.
        /// </summary>
        private GlobalSystemMediaTransportControlsSession? PickBestSession()
        {
            var current = _sessionManager?.GetCurrentSession();
            try
            {
                bool IsPlayingSession(GlobalSystemMediaTransportControlsSession s) =>
                    s.GetPlaybackInfo()?.PlaybackStatus == GlobalSystemMediaTransportControlsSessionPlaybackStatus.Playing;

                if (current != null && IsPlayingSession(current)) return current;

                var sessions = _sessionManager?.GetSessions();
                if (sessions == null || sessions.Count == 0) return current;

                var playing = sessions.Where(IsPlayingSession).ToList();
                if (playing.Count == 0)
                {
                    // Nothing playing: keep showing the session we were already tracking (paused song)
                    if (_currentSession != null && sessions.Any(s => s.SourceAppUserModelId == _currentSession.SourceAppUserModelId))
                        return sessions.First(s => s.SourceAppUserModelId == _currentSession.SourceAppUserModelId);
                    return current;
                }

                return playing.FirstOrDefault(s => IsAppleMusicAppId(s.SourceAppUserModelId ?? "") ||
                                                   (s.SourceAppUserModelId ?? "").Contains("Spotify", StringComparison.OrdinalIgnoreCase))
                       ?? playing.FirstOrDefault(s => GetBrowserName(s.SourceAppUserModelId ?? "") != null)
                       ?? playing[0];
            }
            catch
            {
                return current;
            }
        }

        private void HookSessionEvents(GlobalSystemMediaTransportControlsSession? session)
        {
            if (session == null) return;
            try
            {
                session.PlaybackInfoChanged += Session_PlaybackInfoChanged;
                session.TimelinePropertiesChanged += Session_TimelinePropertiesChanged;
                session.MediaPropertiesChanged += Session_MediaPropertiesChanged;
            }
            catch { }
        }

        private void UnhookSessionEvents(GlobalSystemMediaTransportControlsSession? session)
        {
            if (session == null) return;
            try
            {
                session.PlaybackInfoChanged -= Session_PlaybackInfoChanged;
                session.TimelinePropertiesChanged -= Session_TimelinePropertiesChanged;
                session.MediaPropertiesChanged -= Session_MediaPropertiesChanged;
            }
            catch { }
        }

        private void Session_PlaybackInfoChanged(GlobalSystemMediaTransportControlsSession sender, PlaybackInfoChangedEventArgs args)
        {
            try
            {
                var info = sender.GetPlaybackInfo();
                bool playing = info?.PlaybackStatus == GlobalSystemMediaTransportControlsSessionPlaybackStatus.Playing;
                _dispatcher.Invoke(() =>
                {
                    IsPlaying = playing;
                    UpdateTimelineFromSession(sender);
                });
            }
            catch { }
        }

        private void Session_TimelinePropertiesChanged(GlobalSystemMediaTransportControlsSession sender, TimelinePropertiesChangedEventArgs args)
        {
            try
            {
                _dispatcher.Invoke(() => UpdateTimelineFromSession(sender));
            }
            catch { }
        }

        private async void Session_MediaPropertiesChanged(GlobalSystemMediaTransportControlsSession sender, MediaPropertiesChangedEventArgs args)
        {
            try
            {
                await RefreshCurrentSessionAsync();
            }
            catch { }
        }

        private TimeSpan GetExtrapolatedSessionPosition(GlobalSystemMediaTransportControlsSessionTimelineProperties? timeline, bool isPlaying)
        {
            if (timeline == null) return TimeSpan.Zero;
            TimeSpan pos = timeline.Position;
            if (pos < TimeSpan.Zero) pos = TimeSpan.Zero;

            // Only extrapolate if song is already mid-playback (> 2s) and extrapolation is recent (< 10s)
            // This prevents pre-roll ads or tab loading times from artificially pushing the track position ahead
            if (isPlaying && pos > TimeSpan.FromSeconds(2))
            {
                var lastUpdated = (timeline.LastUpdatedTime != DateTimeOffset.MinValue && timeline.LastUpdatedTime != default)
                    ? timeline.LastUpdatedTime
                    : DateTimeOffset.UtcNow;

                if (lastUpdated < DateTimeOffset.UtcNow)
                {
                    var elapsed = DateTimeOffset.UtcNow - lastUpdated;
                    if (elapsed > TimeSpan.Zero && elapsed < TimeSpan.FromSeconds(10))
                    {
                        pos += elapsed;
                    }
                }
            }
            return pos;
        }

        private void UpdateTimelineFromSession(GlobalSystemMediaTransportControlsSession session)
        {
            try
            {
                var timeline = session.GetTimelineProperties();
                if (timeline != null && timeline.EndTime > TimeSpan.Zero)
                {
                    _timelineEndTime = timeline.EndTime;
                    _timelineStartTime = timeline.StartTime;

                    bool rawChanged = timeline.Position != _lastRawPosition || timeline.LastUpdatedTime != _lastRawUpdatedTime;
                    _lastRawPosition = timeline.Position;
                    _lastRawUpdatedTime = timeline.LastUpdatedTime;

                    if (rawChanged)
                    {
                        TimeSpan cur = _baselinePosition + (_playStopwatch.IsRunning ? _playStopwatch.Elapsed : TimeSpan.Zero);
                        TimeSpan livePos = GetExtrapolatedSessionPosition(timeline, _isPlaying);
                        if (Math.Abs((livePos - cur).TotalSeconds) > 2.0 || !_playStopwatch.IsRunning)
                        {
                            // Real seek detected in media session
                            _baselinePosition = livePos;
                            if (_isPlaying) _playStopwatch.Restart();
                            else _playStopwatch.Reset();
                        }
                    }

                    UpdateLivePosition();
                }
            }
            catch { }
        }

        private int _ticksCount = 0;

        public void UpdateLivePosition()
        {
            if (_isSeeking) return;

            try
            {
                _ticksCount++;
                // Periodically check session state every 30 ticks (~1 second at 33ms)
                if (_ticksCount % 30 == 0 && _currentSession != null)
                {
                    try
                    {
                        var t = _currentSession.GetTimelineProperties();
                        if (t != null && t.EndTime > TimeSpan.Zero)
                        {
                            _timelineEndTime = t.EndTime;
                            _timelineStartTime = t.StartTime;

                            bool rawChanged = t.Position != _lastRawPosition || t.LastUpdatedTime != _lastRawUpdatedTime;
                            if (rawChanged)
                            {
                                _lastRawPosition = t.Position;
                                _lastRawUpdatedTime = t.LastUpdatedTime;

                                TimeSpan cur = _baselinePosition + (_playStopwatch.IsRunning ? _playStopwatch.Elapsed : TimeSpan.Zero);
                                TimeSpan livePos = GetExtrapolatedSessionPosition(t, _isPlaying);
                                if (Math.Abs((livePos - cur).TotalSeconds) > 2.0 || !_playStopwatch.IsRunning)
                                {
                                    // Real seek detected in browser
                                    _baselinePosition = livePos;
                                    if (_isPlaying) _playStopwatch.Restart();
                                    else _playStopwatch.Reset();
                                }
                            }
                        }

                        var pb = _currentSession.GetPlaybackInfo();
                        if (pb != null)
                        {
                            IsPlaying = pb.PlaybackStatus == GlobalSystemMediaTransportControlsSessionPlaybackStatus.Playing;
                        }
                    }
                    catch { }
                }

                TimeSpan current;
                if (_isPlaying)
                {
                    if (!_playStopwatch.IsRunning) _playStopwatch.Start();
                    current = _baselinePosition + _playStopwatch.Elapsed;
                }
                else
                {
                    if (_playStopwatch.IsRunning)
                    {
                        _baselinePosition += _playStopwatch.Elapsed;
                        _playStopwatch.Reset();
                    }
                    current = _baselinePosition;
                }

                if (_timelineEndTime > TimeSpan.Zero)
                {
                    if (current > _timelineEndTime) current = _timelineEndTime;
                    if (current < TimeSpan.Zero) current = TimeSpan.Zero;

                    double pct = (_timelineEndTime.TotalSeconds > 0)
                        ? (current.TotalSeconds / _timelineEndTime.TotalSeconds) * 100.0
                        : 0.0;

                    TimelineProgress = Math.Clamp(pct, 0.0, 100.0);
                    CurrentPosition = FormatTime(current);

                    var remaining = _timelineEndTime - current;
                    if (remaining < TimeSpan.Zero) remaining = TimeSpan.Zero;
                    TotalDuration = "-" + FormatTime(remaining);
                }
                else if (current > TimeSpan.Zero)
                {
                    CurrentPosition = FormatTime(current);
                    TotalDuration = "--:--";
                    TimelineProgress = 0.0;
                }
                else
                {
                    CurrentPosition = "0:00";
                    TotalDuration = "-0:00";
                    TimelineProgress = 0.0;
                }

                // Real-time Lyrics Synchronization at 30 FPS
                Lyrics.UpdatePosition(current);

                if (_ticksCount % 150 == 0) // Every 5 seconds at 33ms
                {
                    try
                    {
                        var logPath = System.IO.Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData), "DynamicIslandPro", "app.log");
                        System.IO.File.AppendAllText(logPath, $"[{DateTime.Now}] [MEDIA-LIVE] Title='{SongTitle}' Pos={CurrentPosition} Rem={TotalDuration} Pct={TimelineProgress:F1}% Playing={IsPlaying}\n");
                    }
                    catch { }
                }
            }
            catch { }
        }

        public async Task SeekToPercentAsync(double percent)
        {
            try
            {
                if (_currentSession != null && _timelineEndTime > TimeSpan.Zero)
                {
                    percent = Math.Clamp(percent, 0.0, 100.0);
                    double targetSec = _timelineEndTime.TotalSeconds * (percent / 100.0);
                    var targetTime = TimeSpan.FromSeconds(targetSec);

                    await _currentSession.TryChangePlaybackPositionAsync(targetTime.Ticks);

                    _timelinePosition = targetTime;
                    _baselinePosition = targetTime;
                    if (_isPlaying) _playStopwatch.Restart(); else _playStopwatch.Reset();
                    _timelineLastUpdated = DateTimeOffset.UtcNow;
                    UpdateLivePosition();
                }
            }
            catch { }
            finally
            {
                _isSeeking = false;
            }
        }

        public async Task SeekToTimeAsync(TimeSpan time)
        {
            try
            {
                if (_currentSession != null && _timelineEndTime > TimeSpan.Zero)
                {
                    if (time > _timelineEndTime) time = _timelineEndTime;
                    if (time < TimeSpan.Zero) time = TimeSpan.Zero;

                    await _currentSession.TryChangePlaybackPositionAsync(time.Ticks);

                    _timelinePosition = time;
                    _baselinePosition = time;
                    if (_isPlaying) _playStopwatch.Restart(); else _playStopwatch.Reset();
                    _timelineLastUpdated = DateTimeOffset.UtcNow;
                    UpdateLivePosition();
                }
            }
            catch { }
            finally
            {
                _isSeeking = false;
            }
        }

        private static string FormatTime(TimeSpan t)
        {
            if (t.TotalHours >= 1)
            {
                return $"{(int)t.TotalHours}:{t.Minutes:D2}:{t.Seconds:D2}";
            }
            return $"{t.Minutes}:{t.Seconds:D2}";
        }

        public async Task TogglePlayPauseAsync()
        {
            if (_currentSession != null)
            {
                try
                {
                    await _currentSession.TryTogglePlayPauseAsync();
                    await RefreshCurrentSessionAsync();
                    return;
                }
                catch { }
            }

            // Fallback via global Windows media key
            NativeMethods.SendMediaKey(NativeMethods.VK_MEDIA_PLAY_PAUSE);
            IsPlaying = !IsPlaying;
        }

        public async Task NextTrackAsync()
        {
            if (_currentSession != null)
            {
                try
                {
                    await _currentSession.TrySkipNextAsync();
                    await RefreshCurrentSessionAsync();
                    return;
                }
                catch { }
            }

            NativeMethods.SendMediaKey(NativeMethods.VK_MEDIA_NEXT_TRACK);
        }

        public async Task PreviousTrackAsync()
        {
            if (_currentSession != null)
            {
                try
                {
                    await _currentSession.TrySkipPreviousAsync();
                    await RefreshCurrentSessionAsync();
                    return;
                }
                catch { }
            }

            NativeMethods.SendMediaKey(NativeMethods.VK_MEDIA_PREV_TRACK);
        }

        public void OpenAppleMusic()
        {
            // 1. Try launching native Apple Music Windows App via protocol
            try
            {
                Process.Start(new ProcessStartInfo("applemusic:") { UseShellExecute = true });
                return;
            }
            catch { }

            // 2. Try launching iTunes protocol
            try
            {
                Process.Start(new ProcessStartInfo("itms:") { UseShellExecute = true });
                return;
            }
            catch { }

            // 3. Fallback to Apple Music Web Player in default browser
            try
            {
                Process.Start(new ProcessStartInfo("https://music.apple.com") { UseShellExecute = true });
            }
            catch { }
        }

        protected void OnPropertyChanged([CallerMemberName] string? propertyName = null)
        {
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
        }
    }
}
