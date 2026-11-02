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
                var session = _sessionManager?.GetCurrentSession();

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
                bool isApple = appId.Contains("Apple", StringComparison.OrdinalIgnoreCase) ||
                               appId.Contains("iTunes", StringComparison.OrdinalIgnoreCase);

                bool isBrowser = appId.Contains("Chrome", StringComparison.OrdinalIgnoreCase) ||
                                 appId.Contains("Edge", StringComparison.OrdinalIgnoreCase) ||
                                 appId.Contains("Brave", StringComparison.OrdinalIgnoreCase) ||
                                 appId.Contains("Firefox", StringComparison.OrdinalIgnoreCase) ||
                                 appId.Contains("Opera", StringComparison.OrdinalIgnoreCase) ||
                                 appId.Contains("Vivaldi", StringComparison.OrdinalIgnoreCase);

                string appDisplayName = isApple ? "Apple Music" : "Now Playing";
                if (appId.Contains("Spotify", StringComparison.OrdinalIgnoreCase)) appDisplayName = "Spotify";
                else if (appId.Contains("Chrome", StringComparison.OrdinalIgnoreCase)) appDisplayName = "YouTube / Chrome";
                else if (appId.Contains("Edge", StringComparison.OrdinalIgnoreCase)) appDisplayName = "YouTube / Edge";
                else if (appId.Contains("Brave", StringComparison.OrdinalIgnoreCase)) appDisplayName = "YouTube / Brave";
                else if (appId.Contains("Firefox", StringComparison.OrdinalIgnoreCase)) appDisplayName = "YouTube / Firefox";
                else if (appId.Contains("Opera", StringComparison.OrdinalIgnoreCase)) appDisplayName = "YouTube / Opera";

                var playbackInfo = session.GetPlaybackInfo();
                bool playing = playbackInfo?.PlaybackStatus == GlobalSystemMediaTransportControlsSessionPlaybackStatus.Playing;

                var mediaProperties = await session.TryGetMediaPropertiesAsync();

                string rawTitle = !string.IsNullOrWhiteSpace(mediaProperties?.Title) ? mediaProperties.Title : "Memutar Audio";
                string rawArtist = !string.IsNullOrWhiteSpace(mediaProperties?.Artist) ? mediaProperties.Artist : appDisplayName;

                string displayTitle = rawTitle;
                string displayArtist = rawArtist;

                // When playing from browser (YouTube) or Apple Music, auto-clean metadata
                if (isBrowser || isApple)
                {
                    var (cleanT, cleanA) = LyricsManager.CleanTrackMetadata(rawTitle, rawArtist);
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
                        if (isNewTrack)
                        {
                            // Fresh track: start baseline at 0:00 (or raw position if mid-song), do NOT add stale ad-time
                            _baselinePosition = (timeline.Position > TimeSpan.FromSeconds(2)) ? timeline.Position : TimeSpan.Zero;
                            if (playing) _playStopwatch.Restart();
                            else _playStopwatch.Reset();
                        }
                        else if (rawChanged)
                        {
                            TimeSpan cur = _baselinePosition + (_playStopwatch.IsRunning ? _playStopwatch.Elapsed : TimeSpan.Zero);
                            if (Math.Abs((timeline.Position - cur).TotalSeconds) > 2.0)
                            {
                                // User seeked in browser
                                _baselinePosition = timeline.Position;
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
                        if (Math.Abs((timeline.Position - cur).TotalSeconds) > 2.0 || !_playStopwatch.IsRunning)
                        {
                            // Real seek detected in media session
                            _baselinePosition = timeline.Position;
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
                                if (Math.Abs((t.Position - cur).TotalSeconds) > 2.0 || !_playStopwatch.IsRunning)
                                {
                                    // Real seek detected in browser
                                    _baselinePosition = t.Position;
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
