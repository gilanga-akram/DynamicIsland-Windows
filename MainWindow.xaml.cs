// Language: C#
// Filename: MainWindow.xaml.cs
// Runs on: Windows 10 / Windows 11 (.NET 9.0-windows10.0.19041.0)
// Targets: Dynamic Island Pro with ALL 7 menus preserved and zero box shadow artifacts

using System;
using System.Diagnostics;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Animation;
using System.Windows.Media.Imaging;
using System.Windows.Threading;

namespace DynamicIsland
{
    public enum IslandDock
    {
        Top,
        Left,
        Right
    }

    public enum CompactMode
    {
        Battery,
        Music,
        Lyrics,
        Performance,
        Cat,
        Clock
    }

    public enum CatState
    {
        Walking,
        Sitting,
        Sleeping,
        Petted
    }

    public partial class MainWindow : Window
    {
        // Brushes for Apple Hardware Notch (Collapsed / Idle: Pure Black) vs macOS Crystal Frosted Glass (Expanded: Matches Dock)
        private static readonly Brush _notchFillCollapsed = new SolidColorBrush(Color.FromRgb(0, 0, 0)); // Pure Hardware Notch Black
        private static readonly Brush _notchBorderCollapsed = new SolidColorBrush(Color.FromArgb(255, 37, 37, 44)); // #25252C

        private static readonly Brush _glassFillExpanded = new LinearGradientBrush(
            new GradientStopCollection
            {
                new GradientStop((Color)ColorConverter.ConvertFromString("#48384B62"), 0.0),
                new GradientStop((Color)ColorConverter.ConvertFromString("#2E1A2638"), 1.0)
            },
            new Point(0, 0),
            new Point(0, 1)
        );

        private static readonly Brush _glassBorderExpanded = new LinearGradientBrush(
            new GradientStopCollection
            {
                new GradientStop((Color)ColorConverter.ConvertFromString("#85FFFFFF"), 0.0),
                new GradientStop((Color)ColorConverter.ConvertFromString("#30FFFFFF"), 1.0)
            },
            new Point(0, 0),
            new Point(0, 1)
        );

        private static readonly System.Windows.Media.Effects.DropShadowEffect _glassShadowExpanded = new System.Windows.Media.Effects.DropShadowEffect
        {
            BlurRadius = 24,
            ShadowDepth = 6,
            Direction = 270,
            Color = Colors.Black,
            Opacity = 0.42
        };
        public SettingsManager Settings { get; }
        public SystemMetrics Metrics { get; }
        public MediaManager Media { get; }
        public DeviceBatteryManager Battery { get; }
        public NotificationManager Notifications { get; }
        public ShelfManager Shelf { get; } = new();
        public AudioManager Audio { get; }
        public ClipboardManager Clipboard { get; } = new();
        public PrayerManager Prayer => PrayerManager.Instance;
        public TodoNotesManager TodoNotes => TodoNotesManager.Instance;
        public HealthManager Health => HealthManager.Instance;
        public GameModeManager GameMode => GameModeManager.Instance;
        public ScreenTimeManager ScreenTime => ScreenTimeManager.Instance;
        public StartbarManager Startbar => StartbarManager.Instance;

        private bool _isExpanded = false;
        private bool _isPinned = false;
        private bool _autoExpandOnHover = true;

        // Clock Ticker Timer
        private readonly DispatcherTimer _clockTimer;

        // Notification Banner State
        private readonly DispatcherTimer _notifDwellTimer;
        private bool _isNotificationShowing = false;
        private NotificationItem? _activeNotification = null;

        // Idle compact display mode & auto-switching
        private CompactMode _currentCompactMode = CompactMode.Battery;
        private bool _autoSwitchMusic = true;
        public IslandDock CurrentDock { get; private set; } = IslandDock.Top;

        // Mouse interaction
        private bool _isCompactInteracting = false;

        // Pomodoro / Stopwatch timer
        private readonly DispatcherTimer _pomodoroTimer;
        private int _timerSeconds = 25 * 60;
        private int _stopwatchSeconds = 0;
        private bool _isTimerRunning = false;
        private bool _isStopwatchMode = false;

        // Equalizer animation timer
        private readonly DispatcherTimer _eqTimer;
        private readonly Random _rand = new();

        // Gaming Mode & Anti-Disruption Timers
        private readonly DispatcherTimer _gameCheckTimer;
        private readonly DispatcherTimer _memoryTrimTimer;
        private bool _isBrowserDodged = false;
        private DispatcherTimer? _browserDodgeTimer;
        private TranslateTransform? _islandDodgeTransform;
        private readonly DispatcherTimer _hoverDwellTimer;
        private bool _isInGameOrFullscreen = false;

        // Island Pet (Animated Cat / Dancing Nailong Dinosaur) Controller
        private readonly DispatcherTimer _catTimer;
        private CatState _catState = CatState.Walking;
        private double _catX = 20;
        private double _catSpeed = 1.6;
        private int _catDirection = 1; // 1 = right, -1 = left
        private int _catStep = 0;
        private int _catStateTicks = 0;
        private int _catMaxStateTicks = 60;
        private int _catEmoteTicks = 0;
        private readonly BitmapSource[] _nailongDanceFrames = new BitmapSource[18];
        private int _nailongDanceIndex = 0;
        private readonly BitmapSource[] _spongebobDanceFrames = new BitmapSource[17];
        private int _spongebobDanceIndex = 0;
        private readonly BitmapSource[] _minionDanceFrames = new BitmapSource[210];
        private int _minionDanceIndex = 0;

        // Realtime Audio Reactive Visualizer (moving rainbow orb & bottom line)
        private readonly DispatcherTimer _audioVisualizerTimer;
        private double _audioAnimationPhase = 0.0;
        private double _currentOrbScale = 1.0;
        private float _smoothedPeak = 0.0f;

        // Dynamic Island dimensions (Configurable via Settings)
        public double CompactW => Settings?.CompactWidth ?? 220;
        public double CompactH => Settings?.CompactHeight ?? 36;
        public double ExpandedW => Math.Max(680, Settings?.ExpandedWidth ?? 680);
        public double ExpandedH => Math.Max(165, Settings?.ExpandedHeight ?? 165);

        private MenuItem? MenuCharNailong;
        private MenuItem? MenuCharSpongebob;
        private MenuItem? MenuCharMinion;
        private MenuItem? MenuAutoMusic = new MenuItem();
        private MenuItem? MenuGameMode = new MenuItem();
        private SettingsWindow? _settingsWindow;
        private double _horizontalPositionPercent = 0.5;

        private (Grid view, Button btn)[] _tabs = Array.Empty<(Grid, Button)>();
        private int _currentTabIndex = 0;

        public MainWindow()
        {
            Console.WriteLine("[DIAG] MainWindow.ctor start");
            InitializeComponent();
            Console.WriteLine("[DIAG] MainWindow.ctor: InitializeComponent complete");

            try
            {
                var iconUri = new Uri("pack://application:,,,/DynamicIsland;component/Assets/logo.png", UriKind.Absolute);
                Icon = System.Windows.Media.Imaging.BitmapFrame.Create(iconUri);
            }
            catch (Exception ex)
            {
                Console.WriteLine($"[DIAG] Icon load catch: {ex.Message}");
            }

            Console.WriteLine("[DIAG] Initializing SettingsManager...");
            Settings = new SettingsManager();
            Console.WriteLine("[DIAG] Initializing SystemMetrics...");
            Metrics = new SystemMetrics();
            Console.WriteLine("[DIAG] Initializing MediaManager...");
            Media = new MediaManager(Dispatcher);
            Console.WriteLine("[DIAG] Initializing BatteryManager...");
            Battery = new DeviceBatteryManager(Dispatcher);
            Console.WriteLine("[DIAG] Initializing AudioManager...");
            Audio = new AudioManager(Dispatcher);
            Console.WriteLine("[DIAG] Initializing NotificationManager...");
            Notifications = new NotificationManager();
            Notifications.NotificationReceived += Notifications_NotificationReceived;
            DataContext = this;
            Console.WriteLine("[DIAG] All managers initialized.");

            _clockTimer = new DispatcherTimer
            {
                Interval = TimeSpan.FromSeconds(1)
            };
            _clockTimer.Tick += (s, e) =>
            {
                string timeStr = DateTime.Now.ToString("HH:mm");
                if (TxtClockTime != null) TxtClockTime.Text = timeStr;
                if (TxtSideClockTime != null) TxtSideClockTime.Text = timeStr;
            };
            _clockTimer.Start();

            _autoExpandOnHover = Settings.AutoExpandOnHover;
            _autoSwitchMusic = Settings.AutoSwitchMusic;
            Topmost = Settings.AlwaysOnTop;
            IslandBorder.Opacity = Settings.NotchOpacity / 100.0;
            IslandBorder.Width = CompactW;
            IslandBorder.Height = CompactH;

            Media.PropertyChanged += Media_PropertyChanged;
            Media.Lyrics.ActiveLineChanged += Lyrics_ActiveLineChanged;
            Settings.PropertyChanged += (s, ev) =>
            {
                if (ev?.PropertyName == nameof(Settings.EnableSoundEffects))
                {
                    SoundManager.Enabled = Settings.EnableSoundEffects;
                }
                else if (ev?.PropertyName == nameof(Settings.EnableDynamicIsland))
                {
                    Dispatcher.Invoke(() => UpdateIslandVisibility(Settings.EnableDynamicIsland));
                }
            };

            _notifDwellTimer = new DispatcherTimer
            {
                Interval = TimeSpan.FromSeconds(4.5)
            };
            _notifDwellTimer.Tick += (s, e) => DismissNotification();

            _pomodoroTimer = new DispatcherTimer
            {
                Interval = TimeSpan.FromSeconds(1)
            };
            _pomodoroTimer.Tick += PomodoroTimer_Tick;

            _eqTimer = new DispatcherTimer
            {
                Interval = TimeSpan.FromMilliseconds(120)
            };
            _eqTimer.Tick += EqTimer_Tick;
            _eqTimer.Start();

            // Hover Dwell Timer (avoids accidental triggers when cursor quickly swipes across top)
            _hoverDwellTimer = new DispatcherTimer();
            _hoverDwellTimer.Tick += HoverDwellTimer_Tick;

            // Background Fullscreen / Game Detection Timer (fast 250ms check)
            _gameCheckTimer = new DispatcherTimer
            {
                Interval = TimeSpan.FromMilliseconds(250)
            };
            _gameCheckTimer.Tick += GameCheckTimer_Tick;
            _gameCheckTimer.Start();

            // Island Pet Animation Timer (fluid & authentic ~15.4 FPS matching Giphy dance GIF)
            _catTimer = new DispatcherTimer
            {
                Interval = TimeSpan.FromMilliseconds(65)
            };
            _catTimer.Tick += CatTimer_Tick;

            EnsureActiveCharacterFramesLoaded();

            // Periodic WorkingSet Memory Trimmer (keeps RAM at ~30-45MB)
            _memoryTrimTimer = new DispatcherTimer { Interval = TimeSpan.FromSeconds(50) };
            _memoryTrimTimer.Tick += (s, ev) => TrimAppMemory();
            _memoryTrimTimer.Start();

            var startupTrim = new DispatcherTimer { Interval = TimeSpan.FromSeconds(4) };
            startupTrim.Tick += (s, ev) =>
            {
                startupTrim.Stop();
                TrimAppMemory();
            };
            startupTrim.Start();



            // Realtime Audio Reactive Visualizer (~40 FPS)
            _audioVisualizerTimer = new DispatcherTimer(DispatcherPriority.Render)
            {
                Interval = TimeSpan.FromMilliseconds(25)
            };
            _audioVisualizerTimer.Tick += AudioVisualizerTimer_Tick;
            _audioVisualizerTimer.Start();

            Console.WriteLine("[DIAG] MainWindow.ctor finished successfully.");
        }

        private void Window_Loaded(object sender, RoutedEventArgs e)
        {
            Console.WriteLine("[DIAG] Window_Loaded event FIRED!");
            SoundManager.Enabled = Settings?.EnableSoundEffects ?? true;
            SoundManager.Play(AtollSound.Unlock);
            Clipboard.Initialize(this);

            _tabs = new (Grid, Button)[]
            {
                (ViewHome, BtnHome),
                (ViewLyrics, BtnLyrics),
                (ViewNook, BtnNook),
                (ViewShelf, BtnShelf),
                (ViewTaskbar, BtnTaskbar),
                (ViewTodo, BtnTodo),
                (ViewMixer, BtnMixerTab),
                (ViewWeather, BtnWeather),
                (ViewHealth, BtnHealth),
                (ViewScreenTime, BtnScreenTime),
                (ViewClipboard, BtnClipboard),
                (ViewPerf, BtnPerf),
                (ViewGameMode, BtnGameMode),
                (ViewTools, BtnTools),
                (ViewTimer, BtnTimer),
                (ViewSettings, BtnSettingsTab),
                (ViewBattery, BtnBattery),
                (ViewMusic, BtnMusic),
                (ViewAgenda, BtnAgenda)
            };

            try
            {
                if (ActiveWindowsItemsControl != null)
                {
                    ActiveWindowsItemsControl.ItemsSource = StartbarManager.Instance.ActiveWindows;
                    StartbarManager.Instance.ActiveWindows.CollectionChanged += (s, ev) =>
                    {
                        if (TxtActiveWindowsCount != null)
                            TxtActiveWindowsCount.Text = $"{StartbarManager.Instance.ActiveWindows.Count} Jendela";
                    };
                }
            }
            catch { }

            string initialTime = DateTime.Now.ToString("HH:mm");
            if (TxtClockTime != null) TxtClockTime.Text = initialTime;
            if (TxtSideClockTime != null) TxtSideClockTime.Text = initialTime;

            Shelf.NotificationRequested += msg =>
            {
                Notifications.TriggerNotification(NotificationAppType.General, "Shelf & AirDrop", "Shelf", msg, "#0A84FF", "", "Dynamic Shelf");
            };

            if (Settings != null)
            {
                Settings.PositionOffsetX = 0;
                Settings.DockPosition = "Top";
                Settings.DockOffsetY = 0;
            }
            SetDockPosition(IslandDock.Top, animate: false);
            LocationChanged += MainWindow_LocationChanged;

            Console.WriteLine($"[DIAG] Window position: Left={Left}, Top={Top}, Width={Width}, Height={Height}, Vis={Visibility}");
            SetActiveView(ViewHome, BtnHome);
            UpdatePerfCardVisuals();
            UpdateGameModeVisuals();

            // Load saved Default Idle Mode
            if (!string.IsNullOrEmpty(Settings?.DefaultIdleMode))
            {
                if (Enum.TryParse<CompactMode>(Settings.DefaultIdleMode, true, out var initialMode))
                {
                    SetCompactMode(initialMode, animate: false);
                }
            }
            else
            {
                SetCompactMode(CompactMode.Clock, animate: false);
            }

            // Auto-switch to Music or Lyrics on startup if already playing
            if (_autoSwitchMusic && Media.IsPlaying)
            {
                if (Settings?.DefaultIdleMode == "Lyrics")
                    SetCompactMode(CompactMode.Lyrics, animate: false);
                else
                    SetCompactMode(CompactMode.Music, animate: false);
            }
            UpdateCharacterVisuals();

            // Poni Signature: Global Alt+N Hotkey
            try
            {
                var helper = new System.Windows.Interop.WindowInteropHelper(this);
                _hwndSource = System.Windows.Interop.HwndSource.FromHwnd(helper.Handle);
                _hwndSource?.AddHook(WndProc);
                NativeMethods.RegisterHotKey(helper.Handle, HOTKEY_ALT_N, 0x0001, 0x4E); // Alt + N
                Closed += (s, ev) =>
                {
                    try
                    {
                        NativeMethods.UnregisterHotKey(helper.Handle, HOTKEY_ALT_N);
                        _hwndSource?.RemoveHook(WndProc);
                    }
                    catch { }
                };
            }
            catch { }

            UpdateIslandVisibility(Settings?.EnableDynamicIsland ?? true);
            UpdateIslandVisualGlass(false);
                        UpdatePomodoroVisibility(Settings?.ShowPomodoroWidget ?? true);
            UpdatePomodoroDate();
            DockManager.Instance.Initialize(Settings?.EnableFloatingDock ?? true);
            PrayerManager.Instance.CalculateDailyPrayers();
            Console.WriteLine("[DIAG] Window_Loaded initialization finished.");
        }

        private void Media_PropertyChanged(object? sender, System.ComponentModel.PropertyChangedEventArgs e)
        {
            if (e.PropertyName == nameof(Media.IsPlaying))
            {
                Dispatcher.Invoke(() =>
                {
                    if (_autoSwitchMusic)
                    {
                        if (Media.IsPlaying)
                        {
                            if (_currentCompactMode != CompactMode.Music && _currentCompactMode != CompactMode.Lyrics)
                            {
                                if (Settings?.DefaultIdleMode == "Lyrics")
                                    SetCompactMode(CompactMode.Lyrics);
                                else
                                    SetCompactMode(CompactMode.Music);
                            }
                        }
                        else
                        {
                            // If we were displaying music/lyrics because it was playing, revert to user default
                            if (_currentCompactMode == CompactMode.Music || _currentCompactMode == CompactMode.Lyrics)
                            {
                                CompactMode defaultMode = CompactMode.Battery;
                                if (Enum.TryParse<CompactMode>(Settings?.DefaultIdleMode, true, out var parsed))
                                {
                                    defaultMode = parsed;
                                }
                                SetCompactMode(defaultMode);
                            }
                        }
                    }
                });
            }
        }

        public void SetDockPosition(IslandDock dock = IslandDock.Top, bool animate = true)
        {
            CurrentDock = IslandDock.Top;
            if (Settings != null)
            {
                Settings.DockPosition = "Top";
                Settings.PositionOffsetX = 0;
                Settings.DockOffsetY = 0;
            }

            ApplyDockLayout();
        }

        public void ApplyDockLayout()
        {
            double screenW = SystemParameters.PrimaryScreenWidth;

            CurrentDock = IslandDock.Top;
            if (Settings != null)
            {
                Settings.DockPosition = "Top";
                Settings.PositionOffsetX = 0;
                Settings.DockOffsetY = 0;
            }

            if (IslandRootGrid != null)
            {
                IslandRootGrid.HorizontalAlignment = HorizontalAlignment.Center;
                IslandRootGrid.VerticalAlignment = VerticalAlignment.Top;
            }
            Left = (screenW - Width) / 2.0;
            Top = 0;

            if (!_isExpanded)
            {
                IslandBorder.Width = CompactW;
                IslandBorder.Height = CompactH;
                if (CompactHorizontalContainer != null) CompactHorizontalContainer.Visibility = Visibility.Visible;
                if (CompactSideContainer != null) CompactSideContainer.Visibility = Visibility.Collapsed;
            }

            UpdateNotchGeometry(IslandBorder.Width, IslandBorder.Height);
        }

        private void MainWindow_LocationChanged(object? sender, EventArgs e)
        {
            if (Settings?.LockPosition == true)
            {
                double screenW = SystemParameters.PrimaryScreenWidth;
                double expectedLeft = (screenW - Width) * _horizontalPositionPercent;
                double expectedTop = 0;

                if (Math.Abs(Left - expectedLeft) > 0.5 || Math.Abs(Top - expectedTop) > 0.5)
                {
                    Left = expectedLeft;
                    Top = expectedTop;
                }
            }
        }

        public void CenterToTop()
        {
            SetDockPosition(IslandDock.Top);
        }

        private void IslandBorder_SizeChanged(object sender, SizeChangedEventArgs e)
        {
            if (e.NewSize.Width > 0 && e.NewSize.Height > 0)
            {
                UpdateNotchGeometry(e.NewSize.Width, e.NewSize.Height);

                // Keep character within bounds if compact width was resized
                var activeCharName = Settings?.CharacterAnimation ?? "Nailong";
                double charW = (activeCharName == "Spongebob") ? 62.0 : ((activeCharName == "Minion") ? 44.0 : 26.0);
                if (e.NewSize.Width > 80 && _catX > e.NewSize.Width - charW - 48)
                {
                    _catX = Math.Max(48, e.NewSize.Width - charW - 48);
                    if (CatAvatar != null) Canvas.SetLeft(CatAvatar, _catX);
                }
            }
        }

        private void UpdateNotchGeometry(double w, double h)
        {
            if (w <= 0 || h <= 0) return;

            if (CurrentDock == IslandDock.Top)
            {
                // Top dock geometry: Flared concave ears at top screen bezel, convex corners at bottom
                double rTop = _isExpanded ? 16.0 : 10.0;
                double rBot = _isExpanded ? 20.0 : 16.0;

                if (rTop * 2 + rBot * 2 > w) rTop = w * 0.1;
                if (rTop + rBot > h) rBot = h - rTop;

                var fillFigure = new PathFigure
                {
                    StartPoint = new Point(0, 0),
                    IsClosed = true,
                    IsFilled = true
                };

                fillFigure.Segments.Add(new BezierSegment(
                    new Point(rTop * 0.552, 0),
                    new Point(rTop, rTop * 0.448),
                    new Point(rTop, rTop),
                    true));
                fillFigure.Segments.Add(new LineSegment(new Point(rTop, h - rBot), true));
                fillFigure.Segments.Add(new BezierSegment(
                    new Point(rTop, h - rBot * 0.448),
                    new Point(rTop + rBot * 0.448, h),
                    new Point(rTop + rBot, h),
                    true));
                fillFigure.Segments.Add(new LineSegment(new Point(w - rTop - rBot, h), true));
                fillFigure.Segments.Add(new BezierSegment(
                    new Point(w - rTop - rBot * 0.448, h),
                    new Point(w - rTop, h - rBot * 0.448),
                    new Point(w - rTop, h - rBot),
                    true));
                fillFigure.Segments.Add(new LineSegment(new Point(w - rTop, rTop), true));
                fillFigure.Segments.Add(new BezierSegment(
                    new Point(w - rTop, rTop * 0.448),
                    new Point(w - rTop * 0.552, 0),
                    new Point(w, 0),
                    true));
                fillFigure.Segments.Add(new LineSegment(new Point(0, 0), true));

                var fillGeom = new PathGeometry();
                fillGeom.Figures.Add(fillFigure);
                fillGeom.Freeze();

                if (NotchFillPath != null) NotchFillPath.Data = fillGeom;
                if (NotchContentGrid != null) NotchContentGrid.Clip = fillGeom;

                var strokeFigure = new PathFigure
                {
                    StartPoint = new Point(0, 0),
                    IsClosed = false,
                    IsFilled = false
                };

                strokeFigure.Segments.Add(new BezierSegment(
                    new Point(rTop * 0.552, 0),
                    new Point(rTop, rTop * 0.448),
                    new Point(rTop, rTop),
                    true));
                strokeFigure.Segments.Add(new LineSegment(new Point(rTop, h - rBot), true));
                strokeFigure.Segments.Add(new BezierSegment(
                    new Point(rTop, h - rBot * 0.448),
                    new Point(rTop + rBot * 0.448, h),
                    new Point(rTop + rBot, h),
                    true));
                strokeFigure.Segments.Add(new LineSegment(new Point(w - rTop - rBot, h), true));
                strokeFigure.Segments.Add(new BezierSegment(
                    new Point(w - rTop - rBot * 0.448, h),
                    new Point(w - rTop, h - rBot * 0.448),
                    new Point(w - rTop, h - rBot),
                    true));
                strokeFigure.Segments.Add(new LineSegment(new Point(w - rTop, rTop), true));
                strokeFigure.Segments.Add(new BezierSegment(
                    new Point(w - rTop, rTop * 0.448),
                    new Point(w - rTop * 0.552, 0),
                    new Point(w, 0),
                    true));

                var strokeGeom = new PathGeometry();
                strokeGeom.Figures.Add(strokeFigure);
                strokeGeom.Freeze();

                if (NotchBorderPath != null) NotchBorderPath.Data = strokeGeom;
            }
            else if (CurrentDock == IslandDock.Left)
            {
                // Left dock geometry: Attached to X=0. Concave ears at (0,0) and (0,h), convex corners on right
                double rEar = _isExpanded ? 14.0 : 8.0;
                double rCorner = _isExpanded ? 18.0 : 14.0;

                if (rEar * 2 + rCorner * 2 > h) rEar = h * 0.08;
                if (rEar + rCorner > w) rCorner = Math.Max(4, w - rEar);

                var fillFigure = new PathFigure
                {
                    StartPoint = new Point(0, 0),
                    IsClosed = true,
                    IsFilled = true
                };

                fillFigure.Segments.Add(new BezierSegment(
                    new Point(0, rEar * 0.552),
                    new Point(rEar * 0.448, rEar),
                    new Point(rEar, rEar),
                    true));
                fillFigure.Segments.Add(new LineSegment(new Point(w - rCorner, rEar), true));
                fillFigure.Segments.Add(new BezierSegment(
                    new Point(w - rCorner * 0.448, rEar),
                    new Point(w, rEar + rCorner * 0.448),
                    new Point(w, rEar + rCorner),
                    true));
                fillFigure.Segments.Add(new LineSegment(new Point(w, h - rEar - rCorner), true));
                fillFigure.Segments.Add(new BezierSegment(
                    new Point(w, h - rEar - rCorner * 0.448),
                    new Point(w - rCorner * 0.448, h - rEar),
                    new Point(w - rCorner, h - rEar),
                    true));
                fillFigure.Segments.Add(new LineSegment(new Point(rEar, h - rEar), true));
                fillFigure.Segments.Add(new BezierSegment(
                    new Point(rEar * 0.448, h - rEar),
                    new Point(0, h - rEar * 0.552),
                    new Point(0, h),
                    true));
                fillFigure.Segments.Add(new LineSegment(new Point(0, 0), true));

                var fillGeom = new PathGeometry();
                fillGeom.Figures.Add(fillFigure);
                fillGeom.Freeze();

                if (NotchFillPath != null) NotchFillPath.Data = fillGeom;
                if (NotchContentGrid != null) NotchContentGrid.Clip = fillGeom;

                var strokeFigure = new PathFigure
                {
                    StartPoint = new Point(0, 0),
                    IsClosed = false,
                    IsFilled = false
                };

                strokeFigure.Segments.Add(new BezierSegment(
                    new Point(0, rEar * 0.552),
                    new Point(rEar * 0.448, rEar),
                    new Point(rEar, rEar),
                    true));
                strokeFigure.Segments.Add(new LineSegment(new Point(w - rCorner, rEar), true));
                strokeFigure.Segments.Add(new BezierSegment(
                    new Point(w - rCorner * 0.448, rEar),
                    new Point(w, rEar + rCorner * 0.448),
                    new Point(w, rEar + rCorner),
                    true));
                strokeFigure.Segments.Add(new LineSegment(new Point(w, h - rEar - rCorner), true));
                strokeFigure.Segments.Add(new BezierSegment(
                    new Point(w, h - rEar - rCorner * 0.448),
                    new Point(w - rCorner * 0.448, h - rEar),
                    new Point(w - rCorner, h - rEar),
                    true));
                strokeFigure.Segments.Add(new LineSegment(new Point(rEar, h - rEar), true));
                strokeFigure.Segments.Add(new BezierSegment(
                    new Point(rEar * 0.448, h - rEar),
                    new Point(0, h - rEar * 0.552),
                    new Point(0, h),
                    true));

                var strokeGeom = new PathGeometry();
                strokeGeom.Figures.Add(strokeFigure);
                strokeGeom.Freeze();

                if (NotchBorderPath != null) NotchBorderPath.Data = strokeGeom;
            }
            else if (CurrentDock == IslandDock.Right)
            {
                // Right dock geometry: Attached to X=w. Concave ears at (w,0) and (w,h), convex corners on left
                double rEar = _isExpanded ? 14.0 : 8.0;
                double rCorner = _isExpanded ? 18.0 : 14.0;

                if (rEar * 2 + rCorner * 2 > h) rEar = h * 0.08;
                if (rEar + rCorner > w) rCorner = Math.Max(4, w - rEar);

                var fillFigure = new PathFigure
                {
                    StartPoint = new Point(w, 0),
                    IsClosed = true,
                    IsFilled = true
                };

                fillFigure.Segments.Add(new BezierSegment(
                    new Point(w, rEar * 0.552),
                    new Point(w - rEar * 0.448, rEar),
                    new Point(w - rEar, rEar),
                    true));
                fillFigure.Segments.Add(new LineSegment(new Point(rCorner, rEar), true));
                fillFigure.Segments.Add(new BezierSegment(
                    new Point(rCorner * 0.448, rEar),
                    new Point(0, rEar + rCorner * 0.448),
                    new Point(0, rEar + rCorner),
                    true));
                fillFigure.Segments.Add(new LineSegment(new Point(0, h - rEar - rCorner), true));
                fillFigure.Segments.Add(new BezierSegment(
                    new Point(0, h - rEar - rCorner * 0.448),
                    new Point(rCorner * 0.448, h - rEar),
                    new Point(rCorner, h - rEar),
                    true));
                fillFigure.Segments.Add(new LineSegment(new Point(w - rEar, h - rEar), true));
                fillFigure.Segments.Add(new BezierSegment(
                    new Point(w - rEar * 0.448, h - rEar),
                    new Point(w, h - rEar * 0.552),
                    new Point(w, h),
                    true));
                fillFigure.Segments.Add(new LineSegment(new Point(w, 0), true));

                var fillGeom = new PathGeometry();
                fillGeom.Figures.Add(fillFigure);
                fillGeom.Freeze();

                if (NotchFillPath != null) NotchFillPath.Data = fillGeom;
                if (NotchContentGrid != null) NotchContentGrid.Clip = fillGeom;

                var strokeFigure = new PathFigure
                {
                    StartPoint = new Point(w, 0),
                    IsClosed = false,
                    IsFilled = false
                };

                strokeFigure.Segments.Add(new BezierSegment(
                    new Point(w, rEar * 0.552),
                    new Point(w - rEar * 0.448, rEar),
                    new Point(w - rEar, rEar),
                    true));
                strokeFigure.Segments.Add(new LineSegment(new Point(rCorner, rEar), true));
                strokeFigure.Segments.Add(new BezierSegment(
                    new Point(rCorner * 0.448, rEar),
                    new Point(0, rEar + rCorner * 0.448),
                    new Point(0, rEar + rCorner),
                    true));
                strokeFigure.Segments.Add(new LineSegment(new Point(0, h - rEar - rCorner), true));
                strokeFigure.Segments.Add(new BezierSegment(
                    new Point(0, h - rEar - rCorner * 0.448),
                    new Point(rCorner * 0.448, h - rEar),
                    new Point(rCorner, h - rEar),
                    true));
                strokeFigure.Segments.Add(new LineSegment(new Point(w - rEar, h - rEar), true));
                strokeFigure.Segments.Add(new BezierSegment(
                    new Point(w - rEar * 0.448, h - rEar),
                    new Point(w, h - rEar * 0.552),
                    new Point(w, h),
                    true));

                var strokeGeom = new PathGeometry();
                strokeGeom.Figures.Add(strokeFigure);
                strokeGeom.Freeze();

                if (NotchBorderPath != null) NotchBorderPath.Data = strokeGeom;
            }
        }

        // ==============================================
        // ANIMATION: FLUID APPLE-STYLE EXPAND & COLLAPSE
        // ==============================================
        private void ExpandIsland()
        {
            if (_isExpanded) return;
            _isExpanded = true;
            UpdateIslandVisualGlass(true);
            _catTimer?.Stop();
            SoundManager.Play(AtollSound.Feedback);

            if (_isNotificationShowing)
            {
                _notifDwellTimer.Stop();
                _isNotificationShowing = false;
                NotificationBannerView.Visibility = Visibility.Collapsed;
            }

            var cubicEase = new CubicEase { EasingMode = EasingMode.EaseOut };
            int dur = Settings?.AnimationSpeedMs ?? 240;

            var (expW, expH) = GetTargetExpandedDimensions();
            var animW = new DoubleAnimation(IslandBorder.ActualWidth, expW, TimeSpan.FromMilliseconds(dur))
            {
                EasingFunction = cubicEase
            };

            var animH = new DoubleAnimation(IslandBorder.ActualHeight, expH, TimeSpan.FromMilliseconds(dur))
            {
                EasingFunction = cubicEase
            };

            var fadeOut = new DoubleAnimation(1, 0, TimeSpan.FromMilliseconds(Math.Max(50, dur / 3)));
            fadeOut.Completed += (s, e) =>
            {
                CompactView.Visibility = Visibility.Collapsed;
                ExpandedView.Visibility = Visibility.Visible;
                var fadeIn = new DoubleAnimation(0, 1, TimeSpan.FromMilliseconds(Math.Max(80, dur / 2)));
                ExpandedView.BeginAnimation(OpacityProperty, fadeIn);
            };

            IslandBorder.BeginAnimation(WidthProperty, animW);
            IslandBorder.BeginAnimation(HeightProperty, animH);
            CompactView.BeginAnimation(OpacityProperty, fadeOut);
        }

        private void CollapseIsland()
        {
            if (!_isExpanded || _isPinned) return;
            _isExpanded = false;
            UpdateIslandVisualGlass(false);
            SoundManager.Play(AtollSound.Feedback);

            var cubicEase = new CubicEase { EasingMode = EasingMode.EaseOut };
            int dur = Settings?.AnimationSpeedMs ?? 240;

            double targetW = (CurrentDock == IslandDock.Top) ? CompactW : 40;
            double targetH = (CurrentDock == IslandDock.Top) ? CompactH : 152;

            var animW = new DoubleAnimation(IslandBorder.ActualWidth, targetW, TimeSpan.FromMilliseconds(dur))
            {
                EasingFunction = cubicEase
            };

            var animH = new DoubleAnimation(IslandBorder.ActualHeight, targetH, TimeSpan.FromMilliseconds(dur))
            {
                EasingFunction = cubicEase
            };

            var fadeOut = new DoubleAnimation(1, 0, TimeSpan.FromMilliseconds(Math.Max(50, dur / 3)));
            fadeOut.Completed += (s, e) =>
            {
                ExpandedView.Visibility = Visibility.Collapsed;
                CompactView.Visibility = Visibility.Visible;

                if (CurrentDock == IslandDock.Top)
                {
                    if (CompactHorizontalContainer != null) CompactHorizontalContainer.Visibility = Visibility.Visible;
                    if (CompactSideContainer != null) CompactSideContainer.Visibility = Visibility.Collapsed;
                }
                else
                {
                    if (CompactHorizontalContainer != null) CompactHorizontalContainer.Visibility = Visibility.Collapsed;
                    if (CompactSideContainer != null) CompactSideContainer.Visibility = Visibility.Visible;
                }

                var fadeIn = new DoubleAnimation(0, 1, TimeSpan.FromMilliseconds(Math.Max(80, dur / 2)));
                CompactView.BeginAnimation(OpacityProperty, fadeIn);

                if (_currentCompactMode == CompactMode.Cat || _currentCompactMode == CompactMode.Clock)
                {
                    _catTimer?.Start();
                }
            };

            IslandBorder.BeginAnimation(WidthProperty, animW);
            IslandBorder.BeginAnimation(HeightProperty, animH);
            ExpandedView.BeginAnimation(OpacityProperty, fadeOut);
        }

        // ==============================================
        // MOUSE & DRAG INTERACTION
        // ==============================================
        private void IslandBorder_MouseEnter(object sender, MouseEventArgs e)
        {
            if (_isPinned)
            {
                IslandBorder.Cursor = Cursors.Arrow;
            }

            if (IsGameBlockingActive())
            {
                // GAMING / FULLSCREEN MODE: Do NOT open on hover
                return;
            }

            // AUTO-DODGE: If active window is browser and cursor is in tab area, ngumpet ke atas layar!
            if (!_isExpanded && (Settings?.DodgeBrowserTabs ?? true) && HasBrowserTabBehindIsland())
            {
                TriggerBrowserTabDodge();
                return;
            }

            if (_autoExpandOnHover && !_isExpanded)
            {
                int delay = Settings?.HoverDwellDelayMs ?? 180;
                if (delay > 0)
                {
                    _hoverDwellTimer.Interval = TimeSpan.FromMilliseconds(delay);
                    _hoverDwellTimer.Start();
                }
                else
                {
                    ExpandIsland();
                }
            }
        }

        private void IslandBorder_MouseLeave(object sender, MouseEventArgs e)
        {
            _hoverDwellTimer?.Stop();

            if (!_isPinned && _isExpanded)
            {
                CollapseIsland();
            }
        }

        // ==============================================
        // BROWSER TAB AUTO-DODGE ("MODE NGUMPET")
        // ==============================================
        private bool HasBrowserTabBehindIsland()
        {
            try
            {
                IntPtr fgHwnd = NativeMethods.GetForegroundWindow();
                if (fgHwnd == IntPtr.Zero) return false;

                var helper = new System.Windows.Interop.WindowInteropHelper(this);
                if (fgHwnd == helper.Handle) return false;

                NativeMethods.GetWindowThreadProcessId(fgHwnd, out uint pid);
                if (pid == 0) return false;

                using var proc = System.Diagnostics.Process.GetProcessById((int)pid);
                string name = proc.ProcessName.ToLowerInvariant();

                // Only Chromium, Gecko, and tabbed browsers with top tab bars
                if (name != "chrome" && name != "msedge" && name != "brave" && name != "firefox" &&
                    name != "opera" && name != "opera_gx" && name != "vivaldi" && name != "arc" &&
                    name != "zen" && name != "waterfox" && name != "thorium" && name != "floorp")
                {
                    return false;
                }

                if (!NativeMethods.GetWindowRect(fgHwnd, out var rect)) return false;

                // Browser window must be docked/maximized at the top edge (tabs sit at Y: 0 to 45)
                if (rect.Top > 10 || rect.Bottom < 250) return false;

                Point screenPos = IslandBorder.PointToScreen(new Point(0, 0));
                double islandLeft = screenPos.X;
                double islandRight = islandLeft + IslandBorder.ActualWidth;

                // Browser window must physically cover the island horizontally
                if (rect.Left > islandRight || rect.Right < islandLeft) return false;

                // Query the browser window specifically for TabItem elements in UI Automation
                var browserRoot = System.Windows.Automation.AutomationElement.FromHandle(fgHwnd);
                if (browserRoot == null) return false;

                var tabCondition = new System.Windows.Automation.PropertyCondition(
                    System.Windows.Automation.AutomationElement.ControlTypeProperty,
                    System.Windows.Automation.ControlType.TabItem);

                var tabs = browserRoot.FindAll(System.Windows.Automation.TreeScope.Descendants, tabCondition);
                if (tabs == null || tabs.Count == 0)
                {
                    return false;
                }

                // Check if ANY tab physically overlaps the Dynamic Island bounds
                for (int i = 0; i < tabs.Count; i++)
                {
                    try
                    {
                        Rect tabRect = tabs[i].Current.BoundingRectangle;

                        // Tab must be in the top tab strip area
                        if (tabRect.Top <= 45 && tabRect.Bottom >= 5)
                        {
                            // Does this tab physically sit behind the Dynamic Island?
                            if (tabRect.Right >= islandLeft && tabRect.Left <= islandRight)
                            {
                                return true;
                            }
                        }
                    }
                    catch { }
                }

                // Also check for the "+" (New Tab button) if it sits directly behind the island
                try
                {
                    var btnCondition = new System.Windows.Automation.PropertyCondition(
                        System.Windows.Automation.AutomationElement.ControlTypeProperty,
                        System.Windows.Automation.ControlType.Button);
                    var buttons = browserRoot.FindAll(System.Windows.Automation.TreeScope.Descendants, btnCondition);
                    for (int i = 0; i < buttons.Count; i++)
                    {
                        string btnName = (buttons[i].Current.Name ?? "").ToLowerInvariant();
                        if (btnName.Contains("tab baru") || btnName.Contains("new tab"))
                        {
                            Rect btnRect = buttons[i].Current.BoundingRectangle;
                            if (btnRect.Top <= 45 && btnRect.Right >= islandLeft && btnRect.Left <= islandRight)
                            {
                                return true;
                            }
                        }
                    }
                }
                catch { }

                return false;
            }
            catch { }
            return false;
        }
        private void TriggerBrowserTabDodge()
        {
            if (_isExpanded || _isBrowserDodged) return;

            // If user holds Ctrl or Alt, allow interacting with Dynamic Island without dodging
            if ((Keyboard.Modifiers & (ModifierKeys.Control | ModifierKeys.Alt)) != 0) return;

            _isBrowserDodged = true;
            _hoverDwellTimer?.Stop();

            // 1. Make Island hit-test transparent so all mouse clicks pass directly to the browser tabs behind it
            IslandBorder.IsHitTestVisible = false;

            // 2. Smoothly animate Island upwards into the screen bezel
            if (_islandDodgeTransform == null)
            {
                _islandDodgeTransform = new TranslateTransform();
                IslandRootGrid.RenderTransform = _islandDodgeTransform;
            }

            double dodgeDistance = -(IslandBorder.ActualHeight > 0 ? IslandBorder.ActualHeight : 36) + 3;
            if (dodgeDistance >= 0) dodgeDistance = -33;

            var animY = new System.Windows.Media.Animation.DoubleAnimation
            {
                To = dodgeDistance,
                Duration = TimeSpan.FromMilliseconds(130),
                EasingFunction = new System.Windows.Media.Animation.QuarticEase { EasingMode = System.Windows.Media.Animation.EasingMode.EaseOut }
            };
            _islandDodgeTransform.BeginAnimation(TranslateTransform.YProperty, animY);

            var animOpacity = new System.Windows.Media.Animation.DoubleAnimation
            {
                To = 0.12,
                Duration = TimeSpan.FromMilliseconds(130)
            };
            IslandBorder.BeginAnimation(UIElement.OpacityProperty, animOpacity);

            // 3. Monitor cursor position to detect when user moves away from tab strip
            if (_browserDodgeTimer == null)
            {
                _browserDodgeTimer = new DispatcherTimer { Interval = TimeSpan.FromMilliseconds(30) };
                _browserDodgeTimer.Tick += BrowserDodgeTimer_Tick;
            }
            _browserDodgeTimer.Start();
        }

        private void BrowserDodgeTimer_Tick(object? sender, EventArgs e)
        {
            if (!_isBrowserDodged)
            {
                _browserDodgeTimer?.Stop();
                return;
            }

            if (!NativeMethods.GetCursorPos(out var pt)) return;

            try
            {
                // Check if user holds Ctrl or Alt to force expand
                if ((Keyboard.Modifiers & (ModifierKeys.Control | ModifierKeys.Alt)) != 0)
                {
                    RestoreFromBrowserDodge();
                    return;
                }

                // If cursor stays right at the extreme top edge (Y <= 1) for a moment, user wants the island
                // Otherwise while cursor is in tab strip (Y < 48), keep dodging!
                Point screenPos = IslandBorder.PointToScreen(new Point(0, 0));
                double islandLeft = screenPos.X;
                double islandRight = islandLeft + IslandBorder.ActualWidth;

                // Restore when cursor moves down into the webpage (Y >= 48)
                // OR moves horizontally away from the island
                // OR the browser is no longer the foreground window
                if (pt.Y >= 48 || pt.X < (islandLeft - 50) || pt.X > (islandRight + 50) || !HasBrowserTabBehindIsland())
                {
                    RestoreFromBrowserDodge();
                }
            }
            catch
            {
                if (pt.Y >= 48) RestoreFromBrowserDodge();
            }
        }

        private void RestoreFromBrowserDodge()
        {
            if (!_isBrowserDodged) return;
            _isBrowserDodged = false;
            _browserDodgeTimer?.Stop();

            if (_islandDodgeTransform != null)
            {
                var animY = new System.Windows.Media.Animation.DoubleAnimation
                {
                    To = 0,
                    Duration = TimeSpan.FromMilliseconds(220),
                    EasingFunction = new System.Windows.Media.Animation.BackEase { Amplitude = 0.25, EasingMode = System.Windows.Media.Animation.EasingMode.EaseOut }
                };
                _islandDodgeTransform.BeginAnimation(TranslateTransform.YProperty, animY);
            }

            double targetOpacity = (Settings?.NotchOpacity ?? 100) / 100.0;
            var animOpacity = new System.Windows.Media.Animation.DoubleAnimation
            {
                To = targetOpacity,
                Duration = TimeSpan.FromMilliseconds(220)
            };
            IslandBorder.BeginAnimation(UIElement.OpacityProperty, animOpacity);

            var timer = new DispatcherTimer { Interval = TimeSpan.FromMilliseconds(230) };
            timer.Tick += (s, ev) =>
            {
                timer.Stop();
                if (!_isBrowserDodged)
                {
                    IslandBorder.IsHitTestVisible = true;
                }
            };
            timer.Start();
        }

        private void IslandBorder_MouseLeftButtonDown(object sender, MouseButtonEventArgs e)
        {
            _hoverDwellTimer?.Stop();

            if (_isPinned)
            {
                IslandBorder.Cursor = Cursors.Arrow;
                return;
            }

            // Compact mode: clicking expands the island
            if (!_isExpanded)
            {
                _isCompactInteracting = true;
                e.Handled = true;
                return;
            }
        }

        private void IslandBorder_MouseMove(object sender, MouseEventArgs e)
        {
            // Island is permanently anchored at the center top. No cursor dragging or repositioning allowed.
            IslandBorder.Cursor = Cursors.Arrow;

            if (!_isExpanded && !_isBrowserDodged && (Settings?.DodgeBrowserTabs ?? true) && HasBrowserTabBehindIsland())
            {
                TriggerBrowserTabDodge();
            }
        }

        private void OnDockDragDropped()
        {
            _isCompactInteracting = false;
            ApplyDockLayout();
            UpdateCharacterVisuals();
        }

        private void IslandBorder_MouseLeftButtonUp(object sender, MouseButtonEventArgs e)
        {
            if (_isCompactInteracting)
            {
                _isCompactInteracting = false;

                // User tapped/clicked: expand into the respective view and pin
                switch (_currentCompactMode)
                {
                    case CompactMode.Music:
                        SetActiveView(ViewMusic, BtnMusic);
                        break;
                    case CompactMode.Lyrics:
                        SetActiveView(ViewLyrics, BtnLyrics);
                        break;
                    case CompactMode.Performance:
                        SetActiveView(ViewPerf, BtnPerf);
                        UpdatePerfCardVisuals();
                        break;
                    case CompactMode.Clock:
                        SetActiveView(ViewMixer, BtnMixerTab);
                        break;
                    case CompactMode.Cat:
                    case CompactMode.Battery:
                    default:
                        SetActiveView(ViewNook, BtnNook);
                        break;
                }

                ExpandIsland();
                _isPinned = true;
                PinIcon.Opacity = 1.0;
                PinIcon.Foreground = (Brush)FindResource("AccentBlue");
                e.Handled = true;
            }
        }

        private void IslandBorder_PreviewMouseWheel(object sender, MouseWheelEventArgs e)
        {
            if (!_isExpanded)
            {
                // Quick shortcut: holding Ctrl or Shift while scrolling directly adjusts notch width
                if (Keyboard.IsKeyDown(Key.LeftCtrl) || Keyboard.IsKeyDown(Key.RightCtrl) ||
                    Keyboard.IsKeyDown(Key.LeftShift) || Keyboard.IsKeyDown(Key.RightShift))
                {
                    double delta = e.Delta > 0 ? 12 : -12;
                    double newW = Math.Clamp(IslandBorder.Width + delta, 170.0, 360.0);
                    IslandBorder.Width = newW;
                    Settings.CompactWidth = newW;
                    UpdateNotchGeometry(newW, CompactH);
                    CenterToTop();
                    e.Handled = true;
                    return;
                }

                // Compact mode: scroll wheel smoothly cycles between Battery, Music, and Performance
                CycleCompactMode(e.Delta < 0);
                e.Handled = true;
                return;
            }

            // In expanded mode, child handlers (TopHeaderGrid / ContentScrollViewer) handle mouse wheel.
        }

        private void TopHeaderGrid_PreviewMouseWheel(object sender, MouseWheelEventArgs e)
        {
            CycleTabs(e.Delta < 0);
            e.Handled = true;
        }

        private void TabsScrollViewer_PreviewMouseWheel(object sender, MouseWheelEventArgs e)
        {
            CycleTabs(e.Delta < 0);
            e.Handled = true;
        }

        private void ContentScrollViewer_PreviewMouseWheel(object sender, MouseWheelEventArgs e)
        {
            if (ContentScrollViewer != null && ContentScrollViewer.ScrollableHeight > 0)
            {
                // Smooth vertical scrolling inside compact dynamic island
                double delta = e.Delta > 0 ? -32 : 32;
                double newOffset = Math.Clamp(ContentScrollViewer.VerticalOffset + delta, 0, ContentScrollViewer.ScrollableHeight);
                ContentScrollViewer.ScrollToVerticalOffset(newOffset);
                e.Handled = true;
            }
            else
            {
                // Content fits fully without vertical scroll; cycle through tabs
                CycleTabs(e.Delta < 0);
                e.Handled = true;
            }
        }

        public void CycleTabs(bool forward)
        {
            if (_tabs.Length == 0) return;
            if (forward)
            {
                _currentTabIndex = (_currentTabIndex + 1) % _tabs.Length;
            }
            else
            {
                _currentTabIndex = (_currentTabIndex - 1 + _tabs.Length) % _tabs.Length;
            }

            var (view, btn) = _tabs[_currentTabIndex];
            SetActiveView(view, btn);
            if (view == ViewPerf) UpdatePerfCardVisuals();
            try { btn.BringIntoView(); } catch { }
        }

        private void PinButton_Click(object sender, RoutedEventArgs e)
        {
            _isPinned = !_isPinned;
            PinIcon.Opacity = _isPinned ? 1.0 : 0.5;
            PinIcon.Foreground = _isPinned 
                ? (Brush)FindResource("AccentBlue") 
                : (Brush)FindResource("TextSecondary");

            if (_isPinned)
            {
                IslandBorder.Cursor = Cursors.Arrow;
                _isCompactInteracting = false;
                if (IslandBorder.IsMouseCaptured)
                {
                    IslandBorder.ReleaseMouseCapture();
                }
            }
        }

        private void CollapseButton_Click(object sender, RoutedEventArgs e)
        {
            _isPinned = false;
            PinIcon.Opacity = 0.5;
            PinIcon.Foreground = (Brush)FindResource("TextSecondary");
            CollapseIsland();
        }

        // ==============================================
        // NAVIGATION (ALL 7 PREVIOUS & NEW MENUS)
        // ==============================================
        private void SetActiveView(Grid activeGrid, Button activeButton)
        {
            for (int i = 0; i < _tabs.Length; i++)
            {
                if (_tabs[i].view == activeGrid)
                {
                    _currentTabIndex = i;
                    break;
                }
            }

            try { ContentScrollViewer?.ScrollToTop(); } catch { }

            // Unconditionally collapse all views to prevent any overlapping
            if (ViewHome != null) ViewHome.Visibility = Visibility.Collapsed;
            if (ViewLyrics != null) ViewLyrics.Visibility = Visibility.Collapsed;
            if (ViewNook != null) ViewNook.Visibility = Visibility.Collapsed;
            if (ViewShelf != null) ViewShelf.Visibility = Visibility.Collapsed;
            if (ViewWeather != null) ViewWeather.Visibility = Visibility.Collapsed;
            if (ViewScreenTime != null) ViewScreenTime.Visibility = Visibility.Collapsed;
            if (ViewTaskbar != null) ViewTaskbar.Visibility = Visibility.Collapsed;
            if (ViewClipboard != null) ViewClipboard.Visibility = Visibility.Collapsed;
            if (ViewTodo != null) ViewTodo.Visibility = Visibility.Collapsed;
            if (ViewPerf != null) ViewPerf.Visibility = Visibility.Collapsed;
            if (ViewGameMode != null) ViewGameMode.Visibility = Visibility.Collapsed;
            if (ViewHealth != null) ViewHealth.Visibility = Visibility.Collapsed;
            if (ViewTimer != null) ViewTimer.Visibility = Visibility.Collapsed;
            if (ViewMixer != null) ViewMixer.Visibility = Visibility.Collapsed;
            if (ViewTools != null) ViewTools.Visibility = Visibility.Collapsed;
            if (ViewBattery != null) ViewBattery.Visibility = Visibility.Collapsed;
            if (ViewMusic != null) ViewMusic.Visibility = Visibility.Collapsed;
            if (ViewAgenda != null) ViewAgenda.Visibility = Visibility.Collapsed;
            if (ViewSettings != null) ViewSettings.Visibility = Visibility.Collapsed;

            if (_tabs != null)
            {
                foreach (var (g, _) in _tabs)
                {
                    if (g != null) g.Visibility = Visibility.Collapsed;
                }
            }

            var normalBg = Brushes.Transparent;
            var normalBorder = Brushes.Transparent;
            var normalFg = new SolidColorBrush(Color.FromRgb(240, 245, 255)); // Bright White #F0F5FF
            var activeBg = new SolidColorBrush(Color.FromArgb(120, 255, 255, 255)); // Luminous Glass Pill #78FFFFFF
            var activeBorder = new SolidColorBrush(Color.FromArgb(180, 255, 255, 255)); // Vivid Rim #B4FFFFFF
            var activeFg = Brushes.White;

            // Reset all tab button styles to crisp muted gray with transparent background
            void ResetTabBtn(Button? b)
            {
                if (b != null)
                {
                    b.Background = normalBg;
                    b.BorderBrush = normalBorder;
                    b.Foreground = normalFg;
                    b.Opacity = 1.0;
                }
            }

            ResetTabBtn(BtnHome);
            ResetTabBtn(BtnLyrics);
            ResetTabBtn(BtnNook);
            ResetTabBtn(BtnShelf);
            ResetTabBtn(BtnTaskbar);
            ResetTabBtn(BtnTodo);
            ResetTabBtn(BtnMixerTab);
            ResetTabBtn(BtnWeather);
            ResetTabBtn(BtnHealth);
            ResetTabBtn(BtnScreenTime);
            ResetTabBtn(BtnClipboard);
            ResetTabBtn(BtnPerf);
            ResetTabBtn(BtnGameMode);
            ResetTabBtn(BtnTools);
            ResetTabBtn(BtnTimer);
            ResetTabBtn(BtnSettingsTab);
            ResetTabBtn(BtnBattery);
            ResetTabBtn(BtnMusic);
            ResetTabBtn(BtnAgenda);

            activeGrid.Visibility = Visibility.Visible;
            activeButton.Background = activeBg;
            activeButton.BorderBrush = activeBorder;
            activeButton.Foreground = activeFg;
            activeButton.Opacity = 1.0;
            try { activeButton.BringIntoView(); } catch { }

            if (_isExpanded)
            {
                var (targetW, targetH) = GetViewDimensions(activeGrid);
                var cubicEase = new CubicEase { EasingMode = EasingMode.EaseOut };
                var animW = new DoubleAnimation(IslandBorder.ActualWidth, targetW, TimeSpan.FromMilliseconds(180)) { EasingFunction = cubicEase };
                var animH = new DoubleAnimation(IslandBorder.ActualHeight, targetH, TimeSpan.FromMilliseconds(180)) { EasingFunction = cubicEase };
                IslandBorder.BeginAnimation(WidthProperty, animW);
                IslandBorder.BeginAnimation(HeightProperty, animH);
            }
        }

        private (double w, double h) GetTargetExpandedDimensions()
        {
            if (_tabs != null)
            {
                foreach (var (g, _) in _tabs)
                {
                    if (g != null && g.Visibility == Visibility.Visible)
                    {
                        return GetViewDimensions(g);
                    }
                }
            }
            return (ExpandedW, ExpandedH);
        }

        private (double w, double h) GetViewDimensions(Grid activeGrid)
        {
            double w = (activeGrid == ViewHome) ? Math.Max(ExpandedW, 700) :
                       (activeGrid == ViewLyrics) ? Math.Max(ExpandedW, 700) :
                       (activeGrid == ViewNook) ? Math.Max(ExpandedW, 720) :
                       (activeGrid == ViewWeather) ? Math.Max(ExpandedW, 700) :
                       (activeGrid == ViewScreenTime) ? Math.Max(ExpandedW, 700) :
                       (activeGrid == ViewShelf) ? Math.Max(ExpandedW, 680) :
                       (activeGrid == ViewTodo) ? Math.Max(ExpandedW, 680) :
                       (activeGrid == ViewHealth) ? Math.Max(ExpandedW, 680) :
                       (activeGrid == ViewPerf) ? Math.Max(ExpandedW, 680) :
                       (activeGrid == ViewGameMode) ? Math.Max(ExpandedW, 700) :
                       (activeGrid == ViewTimer) ? Math.Max(ExpandedW, 660) :
                       (activeGrid == ViewMixer) ? 540 :
                       (activeGrid == ViewTools) ? Math.Max(ExpandedW, 640) :
                       (activeGrid == ViewClipboard) ? Math.Max(ExpandedW, 640) :
                       (activeGrid == ViewSettings) ? Math.Max(ExpandedW, 680) :
                       Math.Max(ExpandedW, 700);

            double h = (activeGrid == ViewHome) ? 145 :
                       (activeGrid == ViewLyrics) ? 195 :
                       (activeGrid == ViewNook) ? 145 :
                       (activeGrid == ViewWeather) ? 155 :
                       (activeGrid == ViewScreenTime) ? 150 :
                       (activeGrid == ViewShelf) ? 145 :
                       (activeGrid == ViewHealth) ? 145 :
                       (activeGrid == ViewTodo) ? 160 :
                       (activeGrid == ViewTimer) ? 160 :
                       (activeGrid == ViewMixer) ? 160 :
                       (activeGrid == ViewPerf) ? 165 :
                       (activeGrid == ViewGameMode) ? 160 :
                       (activeGrid == ViewClipboard) ? 155 :
                       (activeGrid == ViewSettings) ? 180 :
                       160;

            return (w, h);
        }

        private void LogoEmblem_Click(object sender, MouseButtonEventArgs e)
        {
            SetActiveView(ViewNook, BtnNook);
        }

        private void BtnNook_Click(object sender, RoutedEventArgs e) => SetActiveView(ViewNook, BtnNook);

        private void BtnTaskbar_Click(object sender, RoutedEventArgs e)
        {
            StartbarManager.Instance.RefreshWindows();
            if (TxtActiveWindowsCount != null)
                TxtActiveWindowsCount.Text = $"{StartbarManager.Instance.ActiveWindows.Count} Jendela";
            SetActiveView(ViewTaskbar, BtnTaskbar);
        }

        private void BtnRefreshTaskbar_Click(object sender, RoutedEventArgs e)
        {
            StartbarManager.Instance.RefreshWindows();
            if (TxtActiveWindowsCount != null)
                TxtActiveWindowsCount.Text = $"{StartbarManager.Instance.ActiveWindows.Count} Jendela";
        }

        private void TaskbarSearchBox_TextChanged(object sender, TextChangedEventArgs e)
        {
            if (TaskbarSearchPlaceholder != null)
            {
                TaskbarSearchPlaceholder.Visibility = string.IsNullOrEmpty(TaskbarSearchBox.Text) ? Visibility.Visible : Visibility.Collapsed;
            }
            StartbarManager.Instance.SearchFilter = TaskbarSearchBox.Text;
            if (TxtActiveWindowsCount != null)
                TxtActiveWindowsCount.Text = $"{StartbarManager.Instance.ActiveWindows.Count} Jendela";
        }

        private void BtnLaunchExplorer_Click(object sender, RoutedEventArgs e) => StartbarManager.Launch("explorer.exe");
        private void BtnLaunchTerminal_Click(object sender, RoutedEventArgs e) => StartbarManager.Launch("powershell.exe");
        private void BtnLaunchBrowser_Click(object sender, RoutedEventArgs e) => StartbarManager.Launch("cmd.exe", "/c start https://google.com");
        private void BtnLaunchNotepad_Click(object sender, RoutedEventArgs e) => StartbarManager.Launch("notepad.exe");
        private void BtnLaunchTaskmgr_Click(object sender, RoutedEventArgs e) => StartbarManager.Launch("taskmgr.exe");
        private void BtnLaunchSettings_Click(object sender, RoutedEventArgs e) => StartbarManager.Launch("ms-settings:");
        private void BtnLaunchDownloads_Click(object sender, RoutedEventArgs e)
        {
            try
            {
                string dlPath = System.IO.Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.UserProfile), "Downloads");
                StartbarManager.Launch("explorer.exe", dlPath);
            }
            catch { }
        }
        private void BtnLaunchCalc_Click(object sender, RoutedEventArgs e) => StartbarManager.Launch("calc.exe");
        private void BtnLockPC_Click(object sender, RoutedEventArgs e) => NativeMethods.LockWorkStation();

        private void BtnHome_Click(object sender, RoutedEventArgs e) => SetActiveView(ViewHome, BtnHome);
        private void BtnLyrics_Click(object sender, RoutedEventArgs e) => SetActiveView(ViewLyrics, BtnLyrics);
        private void KaraokePreview_Click(object sender, MouseButtonEventArgs e) => SetActiveView(ViewLyrics, BtnLyrics);

        private async void LyricLine_Click(object sender, RoutedEventArgs e)
        {
            try
            {
                if (sender is FrameworkElement elem && elem.DataContext is LyricLine line && Media != null)
                {
                    await Media.SeekToTimeAsync(line.Timestamp);
                }
            }
            catch { }
        }

        private async void BtnRetryLyrics_Click(object sender, RoutedEventArgs e)
        {
            try
            {
                if (Media?.Lyrics != null)
                {
                    await Media.Lyrics.LoadLyricsAsync(Media.SongTitle, Media.ArtistName, 0, forceReload: true, isAppleMusic: Media.IsAppleMusic);
                }
            }
            catch { }
        }

        private void BtnOffsetMinusLarge_Click(object sender, RoutedEventArgs e) => Media?.Lyrics?.AdjustOffset(-2000);
        private void BtnOffsetMinusSmall_Click(object sender, RoutedEventArgs e) => Media?.Lyrics?.AdjustOffset(-500);
        private void BtnOffsetReset_Click(object sender, RoutedEventArgs e) => Media?.Lyrics?.ResetOffset();
        private void BtnOffsetPlusSmall_Click(object sender, RoutedEventArgs e) => Media?.Lyrics?.AdjustOffset(500);
        private void BtnOffsetPlusLarge_Click(object sender, RoutedEventArgs e) => Media?.Lyrics?.AdjustOffset(2000);

        private void BtnOffsetMinus_Click(object sender, RoutedEventArgs e) => Media?.Lyrics?.AdjustOffset(-200);
        private void BtnOffsetPlus_Click(object sender, RoutedEventArgs e) => Media?.Lyrics?.AdjustOffset(200);

        private void BtnSyncThisLine_Click(object sender, RoutedEventArgs e)
        {
            try
            {
                if (sender is FrameworkElement elem && elem.DataContext is LyricLine line && Media != null)
                {
                    int idx = Media.Lyrics.Lines.IndexOf(line);
                    if (idx >= 0)
                    {
                        TimeSpan cur = Media.CurrentPositionTimeSpan;
                        Media.Lyrics.SyncToLine(idx, cur);
                        SoundManager.Play(AtollSound.Dynamic);
                    }
                }
            }
            catch { }
        }

        private double _targetLyricsScroll = 0;
        private DispatcherTimer? _lyricsScrollAnimTimer;

        private void Lyrics_ActiveLineChanged(int activeIndex)
        {
            if (LyricsScrollViewer == null) return;
            Dispatcher.InvokeAsync(() =>
            {
                try
                {
                    if (activeIndex < 0)
                    {
                        _targetLyricsScroll = 0;
                    }
                    else if (LyricsItemsControl != null)
                    {
                        var container = LyricsItemsControl.ItemContainerGenerator.ContainerFromIndex(activeIndex) as FrameworkElement;
                        if (container != null)
                        {
                            var transform = container.TransformToAncestor(LyricsItemsControl);
                            var pt = transform.Transform(new Point(0, 0));
                            // Center active line in mid-viewport (~38px from top)
                            _targetLyricsScroll = Math.Max(0, pt.Y);
                        }
                        else
                        {
                            _targetLyricsScroll = Math.Max(0, activeIndex * 26.0);
                        }
                    }
                    else
                    {
                        _targetLyricsScroll = Math.Max(0, activeIndex * 26.0);
                    }

                    if (_lyricsScrollAnimTimer == null)
                    {
                        _lyricsScrollAnimTimer = new DispatcherTimer(DispatcherPriority.Render)
                        {
                            Interval = TimeSpan.FromMilliseconds(16) // ~60 FPS smooth gliding scroll
                        };
                        _lyricsScrollAnimTimer.Tick += (s, ev) =>
                        {
                            if (LyricsScrollViewer == null) return;
                            double current = LyricsScrollViewer.VerticalOffset;
                            double diff = _targetLyricsScroll - current;
                            if (Math.Abs(diff) < 0.4)
                            {
                                LyricsScrollViewer.ScrollToVerticalOffset(_targetLyricsScroll);
                                _lyricsScrollAnimTimer.Stop();
                            }
                            else
                            {
                                LyricsScrollViewer.ScrollToVerticalOffset(current + (diff * 0.28));
                            }
                        };
                    }

                    if (!_lyricsScrollAnimTimer.IsEnabled)
                    {
                        _lyricsScrollAnimTimer.Start();
                    }
                }
                catch { }
            });
        }

        private void LyricsScrollViewer_PreviewMouseWheel(object sender, MouseWheelEventArgs e)
        {
            if (LyricsScrollViewer != null)
            {
                LyricsScrollViewer.ScrollToVerticalOffset(LyricsScrollViewer.VerticalOffset - (e.Delta / 3.0));
                e.Handled = true;
            }
        }
        private void BtnWeather_Click(object sender, RoutedEventArgs e) => SetActiveView(ViewWeather, BtnWeather);
        private void BtnScreenTime_Click(object sender, RoutedEventArgs e) => SetActiveView(ViewScreenTime, BtnScreenTime);
        private void BtnShelfShare_Click(object sender, RoutedEventArgs e)
        {
            try
            {
                System.Windows.Clipboard.SetText(Shelf.AirDropUrl);
                SoundManager.Play(AtollSound.Dynamic);
            }
            catch { }
        }
        private void BtnAddEvent_Click(object sender, RoutedEventArgs e)
        {
            try { Process.Start(new ProcessStartInfo("ms-calendar:") { UseShellExecute = true }); } catch { }
        }
        private void BtnConnectCalendar_Click(object sender, RoutedEventArgs e)
        {
            try { Process.Start(new ProcessStartInfo("ms-calendar:") { UseShellExecute = true }); } catch { }
        }
        private void BtnLaunchCamera_Click(object sender, RoutedEventArgs e)
        {
            try { Process.Start(new ProcessStartInfo("microsoft.windows.camera:") { UseShellExecute = true }); } catch { }
        }
        private void BtnGameMode_Click(object sender, RoutedEventArgs e) => SetActiveView(ViewGameMode, BtnGameMode);
        private void BtnHealth_Click(object sender, RoutedEventArgs e) => SetActiveView(ViewHealth, BtnHealth);

        private void BtnGameBoost_Click(object sender, RoutedEventArgs e) => GameMode.ToggleBoost();
        private void BtnWinLock_Click(object sender, RoutedEventArgs e) => GameMode.ToggleWinLock();
        private void BtnCleanRam_Click(object sender, RoutedEventArgs e) => GameMode.CleanRamNow();
        private void BtnGameDnd_Click(object sender, RoutedEventArgs e) => GameMode.ToggleDnd();

        private void BtnHealthDrink_Click(object sender, RoutedEventArgs e) => Health.DrinkWaterNow();
        private void BtnHealthEye_Click(object sender, RoutedEventArgs e) => Health.StartEyeBreak();
        private void BtnHealthBreathe_Click(object sender, RoutedEventArgs e) => Health.ToggleBreathing();
        private void BtnHealthStretch_Click(object sender, RoutedEventArgs e) => Health.ResetSittingTime();

        private void TimerSet50_Click(object sender, RoutedEventArgs e)
        {
            _isStopwatchMode = false;
            TimerModeLabel.Text = "DEEP FOCUS (50M)";
            _timerSeconds = 50 * 60;
            TimerDisplay.Text = "50:00";
            ResetTimerState();
        }
        private void BtnToggleFocus_Click(object sender, RoutedEventArgs e)
        {
            try { Process.Start(new ProcessStartInfo("ms-settings:quiethours") { UseShellExecute = true }); } catch { }
        }

        private bool _isCaffeineActive = false;

        private void BtnToggleWifi_Click(object sender, RoutedEventArgs e)
        {
            try { Process.Start(new ProcessStartInfo("ms-availablenetworks:") { UseShellExecute = true }); } catch { }
        }

        private void BtnToggleBluetooth_Click(object sender, RoutedEventArgs e)
        {
            try { Process.Start(new ProcessStartInfo("ms-settings:bluetooth") { UseShellExecute = true }); } catch { }
        }

        private void BtnToggleCaffeine_Click(object sender, RoutedEventArgs e)
        {
            _isCaffeineActive = !_isCaffeineActive;
            if (_isCaffeineActive)
            {
                NativeMethods.SetThreadExecutionState(0x80000000 | 0x00000002 | 0x00000001); // ES_CONTINUOUS | ES_DISPLAY_REQUIRED | ES_SYSTEM_REQUIRED
                if (PillCaffeine != null) PillCaffeine.Background = new SolidColorBrush(Color.FromRgb(255, 149, 0));
            }
            else
            {
                NativeMethods.SetThreadExecutionState(0x80000000); // ES_CONTINUOUS
                if (PillCaffeine != null) PillCaffeine.Background = new SolidColorBrush(Color.FromRgb(22, 22, 34));
            }
            SoundManager.Play(AtollSound.Feedback);
        }

        private void BtnToggleDarkMode_Click(object sender, RoutedEventArgs e)
        {
            try { Process.Start(new ProcessStartInfo("ms-settings:colors") { UseShellExecute = true }); } catch { }
        }

        private void BtnQuickMicMute_Click(object sender, RoutedEventArgs e)
        {
            SetActiveView(ViewMixer, BtnMixerTab);
            ExpandIsland();
        }

        private void BtnQuickSync_Click(object sender, RoutedEventArgs e)
        {
            SoundManager.Play(AtollSound.Feedback);
            Metrics?.UpdateAllMetrics();
            StartbarManager.Instance.RefreshWindows();
        }

        // ==============================================
        // PONI SUITE: HOTKEY, HUD, PRAYER, TODO & HEALTH
        // ==============================================
        private const int WM_HOTKEY = 0x0312;
        private const int HOTKEY_ALT_N = 9001;
        private System.Windows.Interop.HwndSource? _hwndSource;
        private DispatcherTimer? _hudFadeTimer;
        private bool _isThinHandleMode = false;

        private IntPtr WndProc(IntPtr hwnd, int msg, IntPtr wParam, IntPtr lParam, ref bool handled)
        {
            if (msg == WM_HOTKEY && wParam.ToInt32() == HOTKEY_ALT_N)
            {
                Dispatcher.Invoke(() =>
                {
                    if (_isExpanded)
                    {
                        _isPinned = false;
                        CollapseIsland();
                    }
                    else
                    {
                        ExpandIsland();
                        _isPinned = true;
                        if (PinIcon != null)
                        {
                            PinIcon.Opacity = 1.0;
                            PinIcon.Foreground = (Brush)FindResource("AccentBlue");
                        }
                    }
                });
                handled = true;
            }
            return IntPtr.Zero;
        }

        public void ShowHud(string icon, int percent, Brush? barBrush = null)
        {
            if (HudBanner == null) return;
            Dispatcher.Invoke(() =>
            {
                if (TxtHudIcon != null) TxtHudIcon.Text = icon;
                if (BarHudLevel != null)
                {
                    BarHudLevel.Value = Math.Clamp(percent, 0, 100);
                    if (barBrush != null) BarHudLevel.Foreground = barBrush;
                }
                if (TxtHudPercent != null) TxtHudPercent.Text = $"{percent}%";

                HudBanner.Visibility = Visibility.Visible;
                var fadeIn = new DoubleAnimation(HudBanner.Opacity, 1.0, TimeSpan.FromMilliseconds(120));
                HudBanner.BeginAnimation(OpacityProperty, fadeIn);

                _hudFadeTimer?.Stop();
                _hudFadeTimer = new DispatcherTimer { Interval = TimeSpan.FromSeconds(1.8) };
                _hudFadeTimer.Tick += (s, e) =>
                {
                    _hudFadeTimer.Stop();
                    var fadeOut = new DoubleAnimation(1.0, 0.0, TimeSpan.FromMilliseconds(200));
                    fadeOut.Completed += (s2, e2) => HudBanner.Visibility = Visibility.Collapsed;
                    HudBanner.BeginAnimation(OpacityProperty, fadeOut);
                };
                _hudFadeTimer.Start();
            });
        }

        private void BtnPrayerDetails_Click(object sender, MouseButtonEventArgs e)
        {
            var p = Prayer;
            string schedule = $"Subuh: {p.TodayPrayers[0].TimeString} | Terbit: {p.TodayPrayers[1].TimeString} | Dzuhur: {p.TodayPrayers[2].TimeString}\nAshar: {p.TodayPrayers[3].TimeString} | Maghrib: {p.TodayPrayers[4].TimeString} | Isya: {p.TodayPrayers[5].TimeString}";
            Notifications.TriggerNotification(
                NotificationAppType.General,
                "🕌 Jadwal Sholat Kemenag RI",
                $"Berikutnya: {p.NextPrayerName} ({p.NextPrayerTime} - {p.NextPrayerCountdown} lagi)",
                schedule,
                "#BF5AF2",
                "",
                "Dynamic Island Prayer");
            SoundManager.Play(AtollSound.Feedback);
        }

        private void BtnDrinkWater_Click(object sender, RoutedEventArgs e)
        {
            Health.DrinkWaterNow();
        }

        private void BtnToggleBreathe_Click(object sender, RoutedEventArgs e)
        {
            if (Health.IsBreathingActive)
            {
                Health.StopBreathing();
            }
            else
            {
                Health.StartBreathing();
            }
        }

        private void BtnTodo_Click(object sender, RoutedEventArgs e)
        {
            SetActiveView(ViewTodo, BtnTodo);
        }

        private void BtnAddTodo_Click(object sender, RoutedEventArgs e)
        {
            if (TxtNewTodo != null && !string.IsNullOrWhiteSpace(TxtNewTodo.Text))
            {
                TodoNotes.AddTodo(TxtNewTodo.Text);
                TxtNewTodo.Text = "";
                SoundManager.Play(AtollSound.Feedback);
            }
        }

        private void TxtNewTodo_KeyDown(object sender, KeyEventArgs e)
        {
            if (e.Key == Key.Enter)
            {
                BtnAddTodo_Click(sender, e);
            }
        }

        private void CheckTodo_Click(object sender, RoutedEventArgs e)
        {
            if (sender is CheckBox cb && cb.DataContext is TodoItem item)
            {
                TodoNotes.ToggleTodo(item);
                SoundManager.Play(AtollSound.Feedback);
            }
        }

        private void BtnDeleteTodo_Click(object sender, RoutedEventArgs e)
        {
            if (sender is Button btn && btn.Tag is TodoItem item)
            {
                TodoNotes.RemoveTodo(item);
                SoundManager.Play(AtollSound.Feedback);
            }
        }

        private void BtnWindowFocus_Click(object sender, RoutedEventArgs e)
        {
            if (sender is Button btn && btn.Tag is IntPtr hwnd)
            {
                StartbarManager.Instance.ActivateWindow(hwnd);
                CollapseIsland();
            }
        }

        private void BtnWindowMinimize_Click(object sender, RoutedEventArgs e)
        {
            if (sender is Button btn && btn.Tag is IntPtr hwnd)
            {
                StartbarManager.Instance.MinimizeWindow(hwnd);
            }
        }

        private void BtnWindowClose_Click(object sender, RoutedEventArgs e)
        {
            if (sender is Button btn && btn.Tag is IntPtr hwnd)
            {
                StartbarManager.Instance.CloseWindow(hwnd);
            }
        }
        private void BtnClipboard_Click(object sender, RoutedEventArgs e) => SetActiveView(ViewClipboard, BtnClipboard);
        private void BtnMixerTab_Click(object sender, RoutedEventArgs e) => SetActiveView(ViewMixer, BtnMixerTab);
        private void BtnPerf_Click(object sender, RoutedEventArgs e)
        {
            SetActiveView(ViewPerf, BtnPerf);
            UpdatePerfCardVisuals();
        }
        private void BtnBattery_Click(object sender, RoutedEventArgs e) => SetActiveView(ViewBattery, BtnBattery);
        private void BtnMusic_Click(object sender, RoutedEventArgs e) => SetActiveView(ViewMusic, BtnMusic);
        private void BtnAgenda_Click(object sender, RoutedEventArgs e) => SetActiveView(ViewAgenda, BtnAgenda);
        private void BtnTimer_Click(object sender, RoutedEventArgs e) => SetActiveView(ViewTimer, BtnTimer);
        private void BtnTools_Click(object sender, RoutedEventArgs e) => SetActiveView(ViewTools, BtnTools);
        private void BtnShelf_Click(object sender, RoutedEventArgs e) => SetActiveView(ViewShelf, BtnShelf);
        private void BtnSettingsTab_Click(object sender, RoutedEventArgs e) => OpenSettingsWindow();

        private void CompactClock_Click(object sender, MouseButtonEventArgs e)
        {
            e.Handled = true;
            SetActiveView(ViewMixer, BtnMixerTab);
            ExpandIsland();
            _isPinned = true;
            PinIcon.Opacity = 1.0;
            PinIcon.Foreground = (Brush)FindResource("AccentBlue");
        }

        private void SiriOrb_Click(object sender, MouseButtonEventArgs e)
        {
            e.Handled = true;
            SetActiveView(ViewMixer, BtnMixerTab);
            ExpandIsland();
            _isPinned = true;
            PinIcon.Opacity = 1.0;
            PinIcon.Foreground = (Brush)FindResource("AccentBlue");
        }

        private void BtnHeaderMic_Click(object sender, MouseButtonEventArgs e)
        {
            e.Handled = true;
            SetActiveView(ViewMixer, BtnMixerTab);
            ExpandIsland();
        }

        private void BadgeFps_Click(object sender, MouseButtonEventArgs e)
        {
            try
            {
                Process.Start(new ProcessStartInfo("ms-settings:gaming-gamebar") { UseShellExecute = true });
            }
            catch { }
        }

        private void BtnOpenWindowsSound_Click(object sender, RoutedEventArgs e)
        {
            try
            {
                Process.Start(new ProcessStartInfo("ms-settings:sound") { UseShellExecute = true });
            }
            catch { }
        }

        private void SessionMute_Click(object sender, RoutedEventArgs e)
        {
            if (sender is Button btn && btn.Tag is AudioSessionItem session)
            {
                session.IsMuted = !session.IsMuted;
            }
        }

        // ==============================================
        // PERFORMANCE SUB-CARD SELECTION (CPU, MEMORY, GPU)
        // ==============================================
        private void CardCpu_Click(object sender, MouseButtonEventArgs e)
        {
            Metrics.ActivePerfTab = "CPU";
            UpdatePerfCardVisuals();
        }

        private void CardMemory_Click(object sender, MouseButtonEventArgs e)
        {
            Metrics.ActivePerfTab = "Memory";
            UpdatePerfCardVisuals();
        }

        private void CardGpu_Click(object sender, MouseButtonEventArgs e)
        {
            Metrics.ActivePerfTab = "GPU";
            UpdatePerfCardVisuals();
        }

        private void UpdatePerfCardVisuals()
        {
            var normalCardBg = new SolidColorBrush(Color.FromRgb(22, 22, 28));
            var blueCardBg = new SolidColorBrush(Color.FromRgb(28, 40, 56));
            var greenCardBg = new SolidColorBrush(Color.FromRgb(22, 42, 30));
            var cyanCardBg = new SolidColorBrush(Color.FromRgb(20, 42, 54));

            var blueAccent = (Brush)FindResource("AccentBlue");
            var greenAccent = (Brush)FindResource("BatteryGreen");
            var cyanAccent = new SolidColorBrush(Color.FromRgb(0, 210, 255));

            CardCpu.Background = Metrics.IsCpuActive ? cyanCardBg : normalCardBg;
            CardCpu.BorderBrush = Metrics.IsCpuActive ? cyanAccent : Brushes.Transparent;
            CardCpu.BorderThickness = new Thickness(Metrics.IsCpuActive ? 1 : 0);

            CardMemory.Background = Metrics.IsMemoryActive ? blueCardBg : normalCardBg;
            CardMemory.BorderBrush = Metrics.IsMemoryActive ? blueAccent : Brushes.Transparent;
            CardMemory.BorderThickness = new Thickness(Metrics.IsMemoryActive ? 1 : 0);

            CardGpu.Background = Metrics.IsGpuActive ? greenCardBg : normalCardBg;
            CardGpu.BorderBrush = Metrics.IsGpuActive ? greenAccent : Brushes.Transparent;
            CardGpu.BorderThickness = new Thickness(Metrics.IsGpuActive ? 1 : 0);

            GridCpuDetails.Visibility = Metrics.IsCpuActive ? Visibility.Visible : Visibility.Collapsed;
            GridMemoryDetails.Visibility = Metrics.IsMemoryActive ? Visibility.Visible : Visibility.Collapsed;
            GridGpuDetails.Visibility = Metrics.IsGpuActive ? Visibility.Visible : Visibility.Collapsed;

            // Dynamically tint the live wave graph to match the active telemetry source
            if (GraphLinePolyline != null && GraphGradTop != null && GraphGradBottom != null)
            {
                if (Metrics.IsCpuActive)
                {
                    GraphLinePolyline.Stroke = cyanAccent;
                    GraphGradTop.Color = Color.FromArgb(0x55, 0, 210, 255);
                    GraphGradBottom.Color = Color.FromArgb(0x05, 0, 210, 255);
                }
                else if (Metrics.IsGpuActive)
                {
                    GraphLinePolyline.Stroke = greenAccent;
                    GraphGradTop.Color = Color.FromArgb(0x55, 48, 209, 88);
                    GraphGradBottom.Color = Color.FromArgb(0x05, 48, 209, 88);
                }
                else
                {
                    GraphLinePolyline.Stroke = blueAccent;
                    GraphGradTop.Color = Color.FromArgb(0x55, 10, 132, 255);
                    GraphGradBottom.Color = Color.FromArgb(0x05, 10, 132, 255);
                }
            }
        }

        private void BtnSettings_Click(object sender, RoutedEventArgs e)
        {
            OpenSettingsWindow();
        }

        private void AvatarWidget_Click(object sender, MouseButtonEventArgs e)
        {
            // Toggle between Nook and Performance
            if (ViewNook.Visibility == Visibility.Visible)
            {
                SetActiveView(ViewPerf, BtnPerf);
            }
            else
            {
                SetActiveView(ViewNook, BtnNook);
            }
        }

        // ==============================================
        // BATTERY QUICK SWITCH
        // ==============================================
        private void ToggleDeviceIcon_Click(object sender, MouseButtonEventArgs e)
        {
            Battery.ToggleActiveDevice();
        }

        // ==============================================
        // MEDIA CONTROLS & APPLE MUSIC
        // ==============================================
        private async void MediaPlayPause_Click(object sender, RoutedEventArgs e) => await Media.TogglePlayPauseAsync();
        private async void MediaPrev_Click(object sender, RoutedEventArgs e) => await Media.PreviousTrackAsync();
        private async void MediaNext_Click(object sender, RoutedEventArgs e) => await Media.NextTrackAsync();
        private void BtnAppleBadge_Click(object sender, MouseButtonEventArgs e) => Media.OpenAppleMusic();

        private void SliderMediaSeek_PreviewMouseDown(object sender, MouseButtonEventArgs e)
        {
            Media.IsSeeking = true;
        }

        private async void SliderMediaSeek_PreviewMouseUp(object sender, MouseButtonEventArgs e)
        {
            if (sender is Slider slider)
            {
                await Media.SeekToPercentAsync(slider.Value);
            }
            Media.IsSeeking = false;
        }

        private void MediaMute_Click(object sender, RoutedEventArgs e) => NativeMethods.SendMediaKey(NativeMethods.VK_VOLUME_MUTE);
        private void MediaVolDown_Click(object sender, RoutedEventArgs e) => NativeMethods.SendMediaKey(NativeMethods.VK_VOLUME_DOWN);
        private void MediaVolUp_Click(object sender, RoutedEventArgs e) => NativeMethods.SendMediaKey(NativeMethods.VK_VOLUME_UP);

        private void EqTimer_Tick(object? sender, EventArgs e)
        {
            // 1. Expanded Music View Equalizer
            if (ViewMusic.Visibility == Visibility.Visible && Media.IsPlaying)
            {
                EqBar1.Height = _rand.Next(4, 16);
                EqBar2.Height = _rand.Next(6, 17);
                EqBar3.Height = _rand.Next(4, 15);
                EqBar4.Height = _rand.Next(7, 17);
                EqBar5.Height = _rand.Next(3, 14);
            }
            else
            {
                EqBar1.Height = 6;
                EqBar2.Height = 12;
                EqBar3.Height = 8;
                EqBar4.Height = 14;
                EqBar5.Height = 5;
            }

            // 2. Compact Music View Equalizer
            if (CompactMusicView != null && CompactMusicView.Visibility == Visibility.Visible)
            {
                if (Media.IsPlaying)
                {
                    CompactEq1.Height = _rand.Next(3, 14);
                    CompactEq2.Height = _rand.Next(6, 16);
                    CompactEq3.Height = _rand.Next(4, 13);
                    CompactEq4.Height = _rand.Next(7, 16);
                }
                else
                {
                    CompactEq1.Height = 4;
                    CompactEq2.Height = 8;
                    CompactEq3.Height = 5;
                    CompactEq4.Height = 7;
                }
            }

            // 2b. Compact Lyrics View Equalizer
            if (CompactLyricsView != null && CompactLyricsView.Visibility == Visibility.Visible)
            {
                if (Media.IsPlaying)
                {
                    CompactLyricsEq1.Height = _rand.Next(3, 14);
                    CompactLyricsEq2.Height = _rand.Next(6, 16);
                    CompactLyricsEq3.Height = _rand.Next(4, 13);
                    CompactLyricsEq4.Height = _rand.Next(7, 16);
                }
                else
                {
                    CompactLyricsEq1.Height = 4;
                    CompactLyricsEq2.Height = 8;
                    CompactLyricsEq3.Height = 5;
                    CompactLyricsEq4.Height = 7;
                }
            }

            // 3. Side Music View Equalizer
            if (SideMusicView != null && SideMusicView.Visibility == Visibility.Visible)
            {
                if (Media.IsPlaying)
                {
                    if (SideEq1 != null) SideEq1.Height = _rand.Next(3, 10);
                    if (SideEq2 != null) SideEq2.Height = _rand.Next(5, 14);
                    if (SideEq3 != null) SideEq3.Height = _rand.Next(3, 11);
                }
                else
                {
                    if (SideEq1 != null) SideEq1.Height = 3;
                    if (SideEq2 != null) SideEq2.Height = 6;
                    if (SideEq3 != null) SideEq3.Height = 4;
                }
            }
        }

        // ==============================================
        // REALTIME AUDIO VISUALIZER (ORB & RAINBOW LINE)
        // ==============================================
        private void AudioVisualizerTimer_Tick(object? sender, EventArgs e)
        {
            // 1. Fetch current realtime audio peak level from WASAPI master endpoint
            float rawPeak = Audio?.GetRealtimeAudioPeak() ?? 0.0f;

            // Also check if media player or audio sessions are actively playing
            bool isMediaPlaying = (Media != null && Media.IsPlaying);
            if (isMediaPlaying && rawPeak < 0.12f)
            {
                rawPeak = Math.Max(rawPeak, 0.22f); // Fallback audio active baseline if media session is running
            }

            // Smooth peak with snappy attack, gentle decay
            if (rawPeak > _smoothedPeak)
            {
                _smoothedPeak = Math.Min(1.0f, rawPeak * 1.8f);
            }
            else
            {
                _smoothedPeak = Math.Max(0.0f, _smoothedPeak - 0.035f);
            }

            bool hasAudioOutput = (_smoothedPeak > 0.003f);

            // CPU & RAM Throttler: when no audio is playing, sleep at 400ms instead of 25ms (40 FPS)
            if (!hasAudioOutput && !isMediaPlaying)
            {
                if (_audioVisualizerTimer.Interval.TotalMilliseconds < 350)
                {
                    _audioVisualizerTimer.Interval = TimeSpan.FromMilliseconds(400);
                }
            }
            else
            {
                if (_audioVisualizerTimer.Interval.TotalMilliseconds > 30)
                {
                    _audioVisualizerTimer.Interval = TimeSpan.FromMilliseconds(25);
                }
            }

            double speed = hasAudioOutput ? (0.06 + (_smoothedPeak * 0.16)) : 0.02;
            _audioAnimationPhase += speed;
            if (_audioAnimationPhase > 10000.0) _audioAnimationPhase = 0.0;

            // 1. Move and animate Rainbow Audio Wave (Layered cleanly at the very bottom edge, never interfering with avatar)
            double w = CompactClockMixerView != null ? CompactClockMixerView.ActualWidth : 320;
            if (w < 40) w = (IslandBorder != null ? IslandBorder.ActualWidth - 20 : 320);
            if (w < 40) w = 320;

            double baseY = 2.0;
            double amp = hasAudioOutput ? Math.Min(1.3, 0.3 + (_smoothedPeak * 1.2)) : 0.0;

            if (RainbowWavePath != null)
            {
                var geom = new StreamGeometry();
                using (var ctx = geom.Open())
                {
                    ctx.BeginFigure(new Point(2, baseY), isFilled: false, isClosed: false);
                    int segments = 40;
                    for (int i = 1; i <= segments; i++)
                    {
                        double t = (double)i / segments;
                        double x = 2 + t * (w - 4);
                        if (hasAudioOutput)
                        {
                            // Smooth envelope pinning the ends cleanly to the bottom
                            double envelope = Math.Sin(t * Math.PI);
                            // Multi-harmonic sine waves for fluid undulating liquid wave
                            double wave1 = Math.Sin(t * 3.5 * Math.PI * 2.0 - (_audioAnimationPhase * 3.2));
                            double wave2 = 0.3 * Math.Sin(t * 6.0 * Math.PI * 2.0 + (_audioAnimationPhase * 2.0));
                            double y = baseY - (amp * envelope * (wave1 + wave2));
                            ctx.LineTo(new Point(x, y), isStroked: true, isSmoothJoin: true);
                        }
                        else
                        {
                            ctx.LineTo(new Point(x, baseY), isStroked: true, isSmoothJoin: true);
                        }
                    }
                }
                geom.Freeze();
                RainbowWavePath.Data = geom;
            }

            if (RainbowLineBrush != null)
            {
                double shift = (_audioAnimationPhase * 0.40) % 1.0;
                RainbowLineBrush.StartPoint = new Point(-shift, 0);
                RainbowLineBrush.EndPoint = new Point(2.0 - shift, 0);
            }

            if (RainbowDropShadow != null)
            {
                RainbowDropShadow.BlurRadius = hasAudioOutput ? 3.5 : 2.5;
                RainbowDropShadow.Opacity = hasAudioOutput ? 0.85 : 0.70;
            }

            if (hasAudioOutput)
            {
                // 2. Move and animate Rainbow Siri Orb (bundaran pelangi berputar & membesar aktif)
                if (SiriOrbRotate != null)
                {
                    SiriOrbRotate.Angle = (_audioAnimationPhase * 260.0) % 360.0;
                }

                if (SiriInnerRotate != null)
                {
                    SiriInnerRotate.Angle = -(_audioAnimationPhase * 360.0) % 360.0;
                }

                double targetOrbScale = 1.0 + Math.Min(0.48, _smoothedPeak * 0.75);
                _currentOrbScale += (targetOrbScale - _currentOrbScale) * 0.40;

                if (SiriOrbScale != null)
                {
                    SiriOrbScale.ScaleX = _currentOrbScale;
                    SiriOrbScale.ScaleY = _currentOrbScale;
                }

                if (SiriOrbGlow != null)
                {
                    SiriOrbGlow.Opacity = 0.45 + Math.Min(0.55, _smoothedPeak * 0.90);
                }

                // 3. Keep Nailong 18-frame authentic dance completely undisturbed and natural
                if (CatWaddleRotate != null && CatWaddleRotate.Angle != 0) CatWaddleRotate.Angle = 0;
                if (CatJumpTransform != null && _catState != CatState.Petted && CatJumpTransform.Y != 0) CatJumpTransform.Y = 0;
                if (DinoSquashTransform != null && (DinoSquashTransform.ScaleX != 1.0 || DinoSquashTransform.ScaleY != 1.0))
                {
                    DinoSquashTransform.ScaleX = 1.0;
                    DinoSquashTransform.ScaleY = 1.0;
                }
                if (SideCatRotate != null && SideCatRotate.Angle != 0) SideCatRotate.Angle = 0;
            }
            else
            {
                // When silent / no audio output: gently return to resting state
                if (_currentOrbScale > 1.002)
                {
                    _currentOrbScale += (1.0 - _currentOrbScale) * 0.18;
                    if (SiriOrbScale != null)
                    {
                        SiriOrbScale.ScaleX = _currentOrbScale;
                        SiriOrbScale.ScaleY = _currentOrbScale;
                    }
                }
                else
                {
                    _currentOrbScale = 1.0;
                    if (SiriOrbScale != null)
                    {
                        SiriOrbScale.ScaleX = 1.0;
                        SiriOrbScale.ScaleY = 1.0;
                    }
                }

                if (SiriOrbGlow != null && SiriOrbGlow.Opacity > 0.46)
                {
                    SiriOrbGlow.Opacity = 0.45;
                }
            }
        }

                // ==============================================
        // FOCUS TIMER & POMODORO LOGIC (@weldraws_ THREADS STYLE)
        // ==============================================
        public void UpdateIslandVisualGlass(bool isExpanded)
        {
            if (NotchFillPath != null)
            {
                NotchFillPath.Fill = isExpanded ? _glassFillExpanded : _notchFillCollapsed;
            }

            if (NotchBorderPath != null)
            {
                NotchBorderPath.Stroke = isExpanded ? _glassBorderExpanded : _notchBorderCollapsed;
                NotchBorderPath.StrokeThickness = isExpanded ? 1.2 : 1.0;
            }

            if (IslandBorder != null)
            {
                IslandBorder.Effect = isExpanded ? _glassShadowExpanded : null;
            }
        }

        public void UpdateIslandVisibility(bool enabled)
        {
            if (IslandRootGrid != null)
            {
                IslandRootGrid.Visibility = enabled ? Visibility.Visible : Visibility.Collapsed;
            }
        }

        public void UpdatePomodoroVisibility(bool isVisible)
        {
            if (BtnTimer != null)
            {
                BtnTimer.Visibility = isVisible ? Visibility.Visible : Visibility.Collapsed;
            }
        }

        private void UpdatePomodoroDate()
        {
            if (TxtPomodoroDate != null)
            {
                TxtPomodoroDate.Text = DateTime.Now.ToString("dd MMM", new System.Globalization.CultureInfo("id-ID"));
            }
        }

        private void BtnPomoFocusMode_Click(object sender, RoutedEventArgs e)
        {
            _isStopwatchMode = false;
            _timerSeconds = 25 * 60;
            if (TimerDisplay != null) TimerDisplay.Text = " 25:00";
            if (TimerModeLabel != null) TimerModeLabel.Text = "POMODORO FOCUS (25M)";
            if (BorderPomoFocus != null)
            {
                BorderPomoFocus.Background = new SolidColorBrush((Color)ColorConverter.ConvertFromString("#1E202B"));
                BorderPomoFocus.BorderBrush = new SolidColorBrush((Color)ColorConverter.ConvertFromString("#353A4C"));
            }
            if (BorderPomoStand != null)
            {
                BorderPomoStand.Background = new SolidColorBrush((Color)ColorConverter.ConvertFromString("#14151D"));
                BorderPomoStand.BorderBrush = new SolidColorBrush((Color)ColorConverter.ConvertFromString("#202330"));
            }
            if (TxtPomoFocusLabel != null) TxtPomoFocusLabel.Foreground = Brushes.White;
            if (TxtPomoStandLabel != null) TxtPomoStandLabel.Foreground = new SolidColorBrush((Color)ColorConverter.ConvertFromString("#9CA3AF"));
            ResetTimerState();
        }

        private void BtnPomoStandMode_Click(object sender, RoutedEventArgs e)
        {
            _isStopwatchMode = false;
            _timerSeconds = 10 * 60;
            if (TimerDisplay != null) TimerDisplay.Text = " 10:00";
            if (TimerModeLabel != null) TimerModeLabel.Text = "STAND BREAK (10M)";
            if (BorderPomoStand != null)
            {
                BorderPomoStand.Background = new SolidColorBrush((Color)ColorConverter.ConvertFromString("#1E202B"));
                BorderPomoStand.BorderBrush = new SolidColorBrush((Color)ColorConverter.ConvertFromString("#34C759"));
            }
            if (BorderPomoFocus != null)
            {
                BorderPomoFocus.Background = new SolidColorBrush((Color)ColorConverter.ConvertFromString("#14151D"));
                BorderPomoFocus.BorderBrush = new SolidColorBrush((Color)ColorConverter.ConvertFromString("#202330"));
            }
            if (TxtPomoStandLabel != null) TxtPomoStandLabel.Foreground = Brushes.White;
            if (TxtPomoFocusLabel != null) TxtPomoFocusLabel.Foreground = new SolidColorBrush((Color)ColorConverter.ConvertFromString("#9CA3AF"));
            ResetTimerState();
        }

        private void PomodoroTimer_Tick(object? sender, EventArgs e)
        {
            if (_isStopwatchMode)
            {
                _stopwatchSeconds++;
                int m = _stopwatchSeconds / 60;
                int s = _stopwatchSeconds % 60;
                if (TimerDisplay != null) TimerDisplay.Text = $" {m:00}:{s:00}";
            }
            else
            {
                if (_timerSeconds > 0)
                {
                    _timerSeconds--;
                    int m = _timerSeconds / 60;
                    int s = _timerSeconds % 60;
                    if (TimerDisplay != null) TimerDisplay.Text = $" {m:00}:{s:00}";
                }
                else
                {
                    _pomodoroTimer.Stop();
                    _isTimerRunning = false;
                    if (TimerStartIcon != null) TimerStartIcon.Text = "▶";
                    if (TimerStartLabel != null) TimerStartLabel.Text = "Mulai";
                    SoundManager.Play(AtollSound.Timer);
                }
            }
        }

        private void TimerStartPause_Click(object sender, RoutedEventArgs e)
        {
            UpdatePomodoroDate();
            if (_isTimerRunning)
            {
                _pomodoroTimer.Stop();
                _isTimerRunning = false;
                if (TimerStartIcon != null) TimerStartIcon.Text = "▶";
                if (TimerStartLabel != null) TimerStartLabel.Text = "Mulai";
            }
            else
            {
                _pomodoroTimer.Start();
                _isTimerRunning = true;
                if (TimerStartIcon != null) TimerStartIcon.Text = "⏸";
                if (TimerStartLabel != null) TimerStartLabel.Text = "Jeda";
            }
        }

        private void TimerReset_Click(object sender, RoutedEventArgs e)
        {
            _pomodoroTimer.Stop();
            _isTimerRunning = false;
            if (TimerStartIcon != null) TimerStartIcon.Text = "▶";
            if (TimerStartLabel != null) TimerStartLabel.Text = "Mulai";
            if (_isStopwatchMode)
            {
                _stopwatchSeconds = 0;
                if (TimerDisplay != null) TimerDisplay.Text = " 00:00";
            }
            else
            {
                _timerSeconds = 25 * 60;
                if (TimerDisplay != null) TimerDisplay.Text = " 25:00";
            }
        }

        private void TimerSet25_Click(object sender, RoutedEventArgs e)
        {
            _isStopwatchMode = false;
            if (TimerModeLabel != null) TimerModeLabel.Text = "POMODORO FOCUS (25M)";
            _timerSeconds = 25 * 60;
            if (TimerDisplay != null) TimerDisplay.Text = " 25:00";
            ResetTimerState();
        }

        private void TimerSet15_Click(object sender, RoutedEventArgs e)
        {
            _isStopwatchMode = false;
            if (TimerModeLabel != null) TimerModeLabel.Text = "SHORT BREAK (15M)";
            _timerSeconds = 15 * 60;
            if (TimerDisplay != null) TimerDisplay.Text = " 15:00";
            ResetTimerState();
        }

        private void TimerSet5_Click(object sender, RoutedEventArgs e)
        {
            _isStopwatchMode = false;
            if (TimerModeLabel != null) TimerModeLabel.Text = "QUICK BREAK (5M)";
            _timerSeconds = 5 * 60;
            if (TimerDisplay != null) TimerDisplay.Text = " 05:00";
            ResetTimerState();
        }

        private void TimerSetStopwatch_Click(object sender, RoutedEventArgs e)
        {
            _isStopwatchMode = true;
            if (TimerModeLabel != null) TimerModeLabel.Text = "STOPWATCH MODE";
            _stopwatchSeconds = 0;
            if (TimerDisplay != null) TimerDisplay.Text = " 00:00";
            ResetTimerState();
        }

        private void ResetTimerState()
        {
            _pomodoroTimer.Stop();
            _isTimerRunning = false;
            if (TimerStartIcon != null) TimerStartIcon.Text = "▶";
            if (TimerStartLabel != null) TimerStartLabel.Text = "Mulai";
        }

        // ==============================================
        // QUICK TOOLS SHORTCUTS
        // ==============================================
        // ==============================================
        // ATOLL SYSTEM ACTION TILES & TOOLS
        // ==============================================
        private void ToolLock_Click(object sender, RoutedEventArgs e)
        {
            SoundManager.Play(AtollSound.Lock);
            NativeMethods.LockWorkStation();
        }

        private void ToolSleep_Click(object sender, RoutedEventArgs e)
        {
            SoundManager.Play(AtollSound.Lock);
            NativeMethods.SetSuspendState(false, true, true);
        }

        private void ToolMute_Click(object sender, RoutedEventArgs e)
        {
            bool isMuted = Audio.ToggleMasterMute();
            SoundManager.Play(AtollSound.Feedback);
            Notifications?.TriggerNotification(
                NotificationAppType.General,
                "Master Audio",
                isMuted ? "Audio Dibisukan" : "Audio Aktif",
                isMuted ? "Volume master dalam mode hening" : "Volume master suara aktif",
                isMuted ? "#FF453A" : "#30D158",
                "",
                "Volume Master");
        }

        private void ToolColorPicker_Click(object sender, RoutedEventArgs e)
        {
            var picked = ColorPickerTool.PickColorAtCursor();
            if (picked != null)
            {
                Notifications?.TriggerNotification(
                    NotificationAppType.General,
                    "Atoll Color Picker",
                    picked.HexString,
                    $"Warna {picked.RgbString} disalin ke Clipboard!",
                    picked.HexString,
                    "",
                    "Color Picker");
            }
        }

        private void ToolScreenshot_Click(object sender, RoutedEventArgs e)
        {
            SoundManager.Play(AtollSound.Feedback);
            try { Process.Start(new ProcessStartInfo("ms-screenclip:") { UseShellExecute = true }); } catch { }
        }

        private void ToolClipboard_Click(object sender, RoutedEventArgs e)
        {
            SetActiveView(ViewClipboard, BtnClipboard);
        }

        private void ToolTaskMgr_Click(object sender, RoutedEventArgs e)
        {
            SoundManager.Play(AtollSound.Feedback);
            try { Process.Start(new ProcessStartInfo("taskmgr.exe") { UseShellExecute = true }); } catch { }
        }

        private void ToolSettings_Click(object sender, RoutedEventArgs e)
        {
            SoundManager.Play(AtollSound.Feedback);
            try { Process.Start(new ProcessStartInfo("ms-settings:") { UseShellExecute = true }); } catch { }
        }

        // ==============================================
        // ATOLL CLIPBOARD MANAGER EVENT HANDLERS
        // ==============================================
        private void TxtClipboardSearch_TextChanged(object sender, TextChangedEventArgs e)
        {
            if (sender is TextBox tb)
            {
                Clipboard.SearchText = tb.Text;
            }
        }

        private void BtnClearClipboard_Click(object sender, RoutedEventArgs e)
        {
            Clipboard.ClearAllUnpinned();
        }

        private void BtnClipboardCopy_Click(object sender, RoutedEventArgs e)
        {
            if (sender is Button btn && btn.DataContext is ClipboardItem item)
            {
                Clipboard.CopyToClipboard(item);
                string oldContent = btn.Content?.ToString() ?? "Salin";
                btn.Content = "✓ Disalin!";
                btn.Foreground = (Brush)FindResource("BatteryGreen");

                var resetTimer = new DispatcherTimer { Interval = TimeSpan.FromMilliseconds(1200) };
                resetTimer.Tick += (s, ev) =>
                {
                    resetTimer.Stop();
                    btn.Content = oldContent;
                    btn.Foreground = (Brush)FindResource("AccentBlue");
                };
                resetTimer.Start();
            }
        }

        private void BtnClipboardPin_Click(object sender, RoutedEventArgs e)
        {
            if (sender is Button btn && btn.DataContext is ClipboardItem item)
            {
                Clipboard.TogglePin(item);
            }
        }

        private void BtnClipboardDelete_Click(object sender, RoutedEventArgs e)
        {
            if (sender is Button btn && btn.DataContext is ClipboardItem item)
            {
                Clipboard.DeleteItem(item);
            }
        }

        // ==============================================
        // CONTEXT MENU
        // ==============================================
        private void MenuShowNook_Click(object sender, RoutedEventArgs e)
        {
            if (!_isExpanded) ExpandIsland();
            SetActiveView(ViewNook, BtnNook);
        }

        private void MenuShowPerf_Click(object sender, RoutedEventArgs e)
        {
            if (!_isExpanded) ExpandIsland();
            SetActiveView(ViewPerf, BtnPerf);
        }

        private void MenuShowBattery_Click(object sender, RoutedEventArgs e)
        {
            if (!_isExpanded) ExpandIsland();
            SetActiveView(ViewBattery, BtnBattery);
        }

        private void MenuShowMusic_Click(object sender, RoutedEventArgs e)
        {
            if (!_isExpanded) ExpandIsland();
            SetActiveView(ViewMusic, BtnMusic);
        }

        private void MenuShowAgenda_Click(object sender, RoutedEventArgs e)
        {
            if (!_isExpanded) ExpandIsland();
            SetActiveView(ViewAgenda, BtnAgenda);
        }

        private void MenuShowTimer_Click(object sender, RoutedEventArgs e)
        {
            if (!_isExpanded) ExpandIsland();
            SetActiveView(ViewTimer, BtnTimer);
        }

        private void MenuShowTools_Click(object sender, RoutedEventArgs e)
        {
            if (!_isExpanded) ExpandIsland();
            SetActiveView(ViewTools, BtnTools);
        }

        private void MenuShowShelf_Click(object sender, RoutedEventArgs e)
        {
            if (!_isExpanded) ExpandIsland();
            SetActiveView(ViewShelf, BtnShelf);
        }

        // ==============================================
        // IDLE NOTCH DISPLAY MODES (BATTERY / MUSIC / PERF / CAT)
        // ==============================================
        public void SetCompactMode(CompactMode mode, bool animate = true)
        {
            bool modeChanged = (_currentCompactMode != mode);
            _currentCompactMode = mode;

            Grid? targetHorizontal = mode switch
            {
                CompactMode.Music => CompactMusicView,
                CompactMode.Lyrics => CompactLyricsView,
                CompactMode.Performance => CompactPerfView,
                CompactMode.Battery => CompactBatteryView,
                _ => CompactClockMixerView // Both Clock and Cat mode show this notch!
            };

            Grid? targetSide = mode switch
            {
                CompactMode.Music => SideMusicView,
                CompactMode.Lyrics => SideLyricsView,
                CompactMode.Performance => SidePerfView,
                CompactMode.Battery => SideBatteryView,
                _ => SideClockView
            };

            // If mode hasn't changed and targets are already fully visible, do nothing to eliminate flicker
            if (!modeChanged && targetHorizontal != null && targetHorizontal.Visibility == Visibility.Visible && targetHorizontal.Opacity >= 0.99)
            {
                return;
            }

            var horizontalViews = new[] { CompactBatteryView, CompactMusicView, CompactLyricsView, CompactPerfView, CompactClockMixerView };
            var sideViews = new[] { SideBatteryView, SideMusicView, SideLyricsView, SidePerfView, SideClockView };

            if ((mode == CompactMode.Cat || mode == CompactMode.Clock) && !_isExpanded)
            {
                _catTimer?.Start();
            }
            else
            {
                _catTimer?.Stop();
            }

            if (!animate)
            {
                foreach (var v in horizontalViews)
                {
                    if (v == null) continue;
                    v.Visibility = (v == targetHorizontal) ? Visibility.Visible : Visibility.Collapsed;
                    v.Opacity = (v == targetHorizontal) ? 1.0 : 0.0;
                }
                foreach (var v in sideViews)
                {
                    if (v == null) continue;
                    v.Visibility = (v == targetSide) ? Visibility.Visible : Visibility.Collapsed;
                    v.Opacity = (v == targetSide) ? 1.0 : 0.0;
                }
                return;
            }

            // Animate Horizontal Views
            foreach (var v in horizontalViews)
            {
                if (v == null || v == targetHorizontal) continue;
                if (v.Visibility == Visibility.Visible)
                {
                    var fadeOut = new DoubleAnimation(v.Opacity, 0, TimeSpan.FromMilliseconds(90));
                    var captured = v;
                    fadeOut.Completed += (s, e) => captured.Visibility = Visibility.Collapsed;
                    captured.BeginAnimation(OpacityProperty, fadeOut);
                }
            }

            if (targetHorizontal != null)
            {
                targetHorizontal.Visibility = Visibility.Visible;
                var fadeIn = new DoubleAnimation(0, 1, TimeSpan.FromMilliseconds(130));
                targetHorizontal.BeginAnimation(OpacityProperty, fadeIn);
            }

            // Animate Side Views
            foreach (var v in sideViews)
            {
                if (v == null || v == targetSide) continue;
                if (v.Visibility == Visibility.Visible)
                {
                    var fadeOut = new DoubleAnimation(v.Opacity, 0, TimeSpan.FromMilliseconds(90));
                    var captured = v;
                    fadeOut.Completed += (s, e) => captured.Visibility = Visibility.Collapsed;
                    captured.BeginAnimation(OpacityProperty, fadeOut);
                }
            }

            if (targetSide != null)
            {
                targetSide.Visibility = Visibility.Visible;
                var fadeIn = new DoubleAnimation(0, 1, TimeSpan.FromMilliseconds(130));
                targetSide.BeginAnimation(OpacityProperty, fadeIn);
            }
        }

        public void CycleCompactMode(bool forward = true)
        {
            int count = Enum.GetValues(typeof(CompactMode)).Length;
            int next = forward
                ? ((int)_currentCompactMode + 1) % count
                : ((int)_currentCompactMode - 1 + count) % count;
            SetCompactMode((CompactMode)next);
        }

        private void CompactCenterDot_Click(object sender, MouseButtonEventArgs e)
        {
            e.Handled = true;
            CycleCompactMode(true);
        }

        private void CompactMusicLeft_Click(object sender, MouseButtonEventArgs e)
        {
            e.Handled = true;
            SetActiveView(ViewMusic, BtnMusic);
            ExpandIsland();
            _isPinned = true;
            PinIcon.Opacity = 1.0;
            PinIcon.Foreground = (Brush)FindResource("AccentBlue");
        }

        private async void CompactMusicPlayPause_Click(object sender, MouseButtonEventArgs e)
        {
            e.Handled = true;
            await Media.TogglePlayPauseAsync();
        }

        private void CompactPerf_Click(object sender, MouseButtonEventArgs e)
        {
            e.Handled = true;
            SetActiveView(ViewPerf, BtnPerf);
            UpdatePerfCardVisuals();
            ExpandIsland();
            _isPinned = true;
            PinIcon.Opacity = 1.0;
            PinIcon.Foreground = (Brush)FindResource("AccentBlue");
        }

        public void EnsureActiveCharacterFramesLoaded()
        {
            var character = Settings?.CharacterAnimation ?? "Nailong";
            if (character == "Minion")
            {
                if (_minionDanceFrames[0] == null) LoadMinionDanceFrames();
                ClearDanceFrames(_nailongDanceFrames);
                ClearDanceFrames(_spongebobDanceFrames);
            }
            else if (character == "Spongebob")
            {
                if (_spongebobDanceFrames[0] == null) LoadSpongebobDanceFrames();
                ClearDanceFrames(_nailongDanceFrames);
                ClearDanceFrames(_minionDanceFrames);
            }
            else
            {
                if (_nailongDanceFrames[0] == null) LoadNailongDanceFrames();
                ClearDanceFrames(_spongebobDanceFrames);
                ClearDanceFrames(_minionDanceFrames);
            }
        }

        private static void ClearDanceFrames(BitmapSource[] arr)
        {
            for (int i = 0; i < arr.Length; i++)
            {
                arr[i] = null!;
            }
        }

        public static void TrimAppMemory()
        {
            try
            {
                GC.Collect(2, GCCollectionMode.Forced, false, true);
                GC.WaitForPendingFinalizers();
                GC.Collect(2, GCCollectionMode.Forced, false, true);
                NativeMethods.EmptyWorkingSet(Process.GetCurrentProcess().Handle);
            }
            catch { }
        }

        private void LoadNailongDanceFrames()
        {
            try
            {
                for (int i = 0; i < 18; i++)
                {
                    var uri = new Uri($"pack://application:,,,/DynamicIsland;component/Assets/nailong_dance/nailong_{i:D2}.png", UriKind.Absolute);
                    var bmp = new BitmapImage();
                    bmp.BeginInit();
                    bmp.UriSource = uri;
                    bmp.DecodePixelWidth = 64; // Downscale to display size: cuts memory by 90%!
                    bmp.CacheOption = BitmapCacheOption.OnLoad;
                    bmp.EndInit();
                    bmp.Freeze();
                    _nailongDanceFrames[i] = bmp;
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine($"[NAILONG] Error loading dance frames: {ex.Message}");
            }
        }

        private void LoadSpongebobDanceFrames()
        {
            try
            {
                for (int i = 0; i < 17; i++)
                {
                    var uri = new Uri($"pack://application:,,,/DynamicIsland;component/Assets/spongebob_dance/spongebob_{i:D2}.png", UriKind.Absolute);
                    var bmp = new BitmapImage();
                    bmp.BeginInit();
                    bmp.UriSource = uri;
                    bmp.DecodePixelWidth = 64; // Downscale to display size
                    bmp.CacheOption = BitmapCacheOption.OnLoad;
                    bmp.EndInit();
                    bmp.Freeze();
                    _spongebobDanceFrames[i] = bmp;
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine($"[SPONGEBOB] Error loading dance frames: {ex.Message}");
            }
        }

        private void LoadMinionDanceFrames()
        {
            try
            {
                for (int i = 0; i < 210; i++)
                {
                    var uri = new Uri($"pack://application:,,,/DynamicIsland;component/Assets/minion_dance/minion_{i:D3}.png", UriKind.Absolute);
                    var bmp = new BitmapImage();
                    bmp.BeginInit();
                    bmp.UriSource = uri;
                    bmp.DecodePixelWidth = 56; // Downscale to display size: cuts 210 frames from 35MB to ~2MB!
                    bmp.CacheOption = BitmapCacheOption.OnLoad;
                    bmp.EndInit();
                    bmp.Freeze();
                    _minionDanceFrames[i] = bmp;
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine($"[MINION] Error loading dance frames: {ex.Message}");
            }
        }
        private void CatTimer_Tick(object? sender, EventArgs e)
        {
            if (_isExpanded || (_currentCompactMode != CompactMode.Cat && _currentCompactMode != CompactMode.Clock))
            {
                _catTimer?.Stop();
                return;
            }

            // 1. Advance authentic character dance cycle (Nailong 18 frames, Spongebob 17 frames, Minion 210 frames)
            var character = Settings?.CharacterAnimation ?? "Nailong";
            if (character == "Minion" && _minionDanceFrames[0] != null)
            {
                _minionDanceIndex = (_minionDanceIndex + 1) % 210;
                var currentDanceBmp = _minionDanceFrames[_minionDanceIndex];
                if (CatAvatarImage != null) CatAvatarImage.Source = currentDanceBmp;
                if (SideCatAvatarImage != null) SideCatAvatarImage.Source = currentDanceBmp;
            }
            else if (character == "Spongebob" && _spongebobDanceFrames[0] != null)
            {
                _spongebobDanceIndex = (_spongebobDanceIndex + 1) % 17;
                var currentDanceBmp = _spongebobDanceFrames[_spongebobDanceIndex];
                if (CatAvatarImage != null) CatAvatarImage.Source = currentDanceBmp;
                if (SideCatAvatarImage != null) SideCatAvatarImage.Source = currentDanceBmp;
            }
            else if (_nailongDanceFrames[0] != null)
            {
                _nailongDanceIndex = (_nailongDanceIndex + 1) % 18;
                var currentDanceBmp = _nailongDanceFrames[_nailongDanceIndex];
                if (CatAvatarImage != null) CatAvatarImage.Source = currentDanceBmp;
                if (SideCatAvatarImage != null) SideCatAvatarImage.Source = currentDanceBmp;
            }

            if (CatAvatar == null) return;

            _catStateTicks++;

            // Fade/float emote bubble if active
            if (_catEmoteTicks > 0)
            {
                _catEmoteTicks--;
                double curTop = Canvas.GetTop(CatEmoteBubble);
                if (double.IsNaN(curTop)) curTop = 0;
                Canvas.SetTop(CatEmoteBubble, Math.Max(-6.0, curTop - 0.4));
                CatEmoteBubble.Opacity = Math.Clamp(_catEmoteTicks / 12.0, 0.0, 1.0);
                if (_catEmoteTicks == 0)
                {
                    CatEmoteBubble.Opacity = 0;
                    CatEmoteBubble.Visibility = Visibility.Collapsed;
                }
            }

            double charW = (character == "Spongebob") ? 62.0 : ((character == "Minion") ? 44.0 : 26.0);
            double minX = 48;
            double maxX = Math.Max(minX + 15, IslandBorder.ActualWidth - charW - 48);

            switch (_catState)
            {
                case CatState.Walking:
                    _catStep = (_catStep + 1) % 4;
                    UpdateCatWalkAnimation(_catStep);

                    _catX += _catSpeed * _catDirection;
                    Canvas.SetLeft(CatAvatar, _catX);

                    if (_catStateTicks % 40 == 0 || _catStateTicks % 40 == 1)
                    {
                        SetCatEyes(open: false, happy: false, sleep: true); // Natural cartoon blink
                    }
                    else
                    {
                        SetCatEyes(open: true, happy: false, sleep: false);
                    }

                    if ((_catDirection > 0 && _catX >= maxX) || (_catDirection < 0 && _catX <= minX))
                    {
                        _catX = _catDirection > 0 ? maxX : minX;
                        Canvas.SetLeft(CatAvatar, _catX);
                        UpdateCatWalkAnimation(1); // stand neutral

                        if (_rand.Next(4) == 0)
                        {
                            SwitchCatState(CatState.Sleeping, _rand.Next(50, 85));
                        }
                        else
                        {
                            SwitchCatState(CatState.Sitting, _rand.Next(25, 55));
                        }
                    }
                    break;

                case CatState.Sitting:
                    if (CatWaddleRotate != null) CatWaddleRotate.Angle = 0;
                    if (CatJumpTransform != null) CatJumpTransform.Y = 0;
                    if (DinoSquashTransform != null) { DinoSquashTransform.ScaleY = 1.0; DinoSquashTransform.ScaleX = 1.0; }

                    if (_catStateTicks >= _catMaxStateTicks)
                    {
                        _catDirection = -_catDirection;
                        if (CatScaleTransform != null) CatScaleTransform.ScaleX = _catDirection;
                        SwitchCatState(CatState.Walking);
                    }
                    break;

                case CatState.Sleeping:
                    if (CatWaddleRotate != null) CatWaddleRotate.Angle = 0;
                    if (CatJumpTransform != null) CatJumpTransform.Y = 0;
                    if (DinoSquashTransform != null) { DinoSquashTransform.ScaleY = 1.0; DinoSquashTransform.ScaleX = 1.0; }

                    if (_catStateTicks >= _catMaxStateTicks)
                    {
                        _catDirection = -_catDirection;
                        if (CatScaleTransform != null) CatScaleTransform.ScaleX = _catDirection;
                        SwitchCatState(CatState.Walking);
                    }
                    break;

                case CatState.Petted:
                    double progress = Math.Clamp((double)_catStateTicks / _catMaxStateTicks, 0, 1);
                    if (CatJumpTransform != null) CatJumpTransform.Y = -6.5 * Math.Sin(progress * Math.PI);
                    if (CatWaddleRotate != null) CatWaddleRotate.Angle = Math.Sin(_catStateTicks * 0.7) * 8.0;
                    if (DinoSquashTransform != null) { DinoSquashTransform.ScaleY = 1.05; DinoSquashTransform.ScaleX = 0.95; }

                    if (_catStateTicks >= _catMaxStateTicks)
                    {
                        SwitchCatState(CatState.Walking);
                    }
                    break;
            }
        }

        private void SetCatEyes(bool open, bool happy, bool sleep)
        {
            // Nailong avatar is full dancing cutout bitmap; eyes stay lively
        }

        private void UpdateCatWalkAnimation(int step)
        {
            if (CatWaddleRotate == null || CatJumpTransform == null) return;
            // The authentic 18-frame GIF already contains natural dance choreography.
            // Keep transforms neutral so Nailong dances fluidly without interference from the bottom wave.
            CatWaddleRotate.Angle = 0;
            if (_catState != CatState.Petted)
            {
                CatJumpTransform.Y = 0;
            }
            if (DinoSquashTransform != null)
            {
                DinoSquashTransform.ScaleX = 1.0;
                DinoSquashTransform.ScaleY = 1.0;
            }
        }

        private void SwitchCatState(CatState newState, int maxTicks = 50)
        {
            _catState = newState;
            _catStateTicks = 0;
            _catMaxStateTicks = Math.Max(10, maxTicks);
        }

        private void CatAvatar_MouseDown(object sender, MouseButtonEventArgs e)
        {
            e.Handled = true;
            PetCat();
        }

        private void PetCat()
        {
            var character = Settings?.CharacterAnimation ?? "Nailong";
            string[] emotes;
            if (character == "Minion")
            {
                emotes = new[] { "🍌 BANANA!", "🎉 Bello!", "🕺 Poopaye!", "✨ Hehe!", "🍌 Tulaliloo ti amo!", "💛 Me want banana!" };
            }
            else if (character == "Spongebob")
            {
                emotes = new[] { "🧽 I'm Ready!", "🍍 Krabby Patty!", "✨ Hehe!", "🫧 Bubble!", "🍔 Krusty Krab!", "🎶 La la la!" };
            }
            else
            {
                emotes = new[] { "💛 Nailong!", "✨ Hehe!", "🍗 Lapar!", "🥰 Gemoy!", "💛 Hehehe!" };
            }
            string selectedEmote = emotes[_rand.Next(emotes.Length)];
            TriggerCatEmote(selectedEmote, 26);
            SwitchCatState(CatState.Petted, 18);
        }

        public void SetCharacterAnimation(string name)
        {
            if (Settings != null) Settings.CharacterAnimation = name;
            EnsureActiveCharacterFramesLoaded();
            UpdateCharacterVisuals();

            // Refresh current frame immediately
            if (name == "Minion" && _minionDanceFrames[0] != null)
            {
                if (CatAvatarImage != null) CatAvatarImage.Source = _minionDanceFrames[_minionDanceIndex % 210];
                if (SideCatAvatarImage != null) SideCatAvatarImage.Source = _minionDanceFrames[_minionDanceIndex % 210];
                if (CatAvatar != null) CatAvatar.ToolTip = "🍌 Minion (Klik untuk ajak main!)";
            }
            else if (name == "Spongebob" && _spongebobDanceFrames[0] != null)
            {
                if (CatAvatarImage != null) CatAvatarImage.Source = _spongebobDanceFrames[_spongebobDanceIndex % 17];
                if (SideCatAvatarImage != null) SideCatAvatarImage.Source = _spongebobDanceFrames[_spongebobDanceIndex % 17];
                if (CatAvatar != null) CatAvatar.ToolTip = "🧽 Spongebob Squarepants (Klik untuk ajak main!)";
            }
            else if (_nailongDanceFrames[0] != null)
            {
                if (CatAvatarImage != null) CatAvatarImage.Source = _nailongDanceFrames[_nailongDanceIndex % 18];
                if (SideCatAvatarImage != null) SideCatAvatarImage.Source = _nailongDanceFrames[_nailongDanceIndex % 18];
                if (CatAvatar != null) CatAvatar.ToolTip = "🦖 Dinosaurus Nailong (Klik untuk ajak main!)";
            }
        }

        private void UpdateCharacterVisuals()
        {
            var activeChar = Settings?.CharacterAnimation ?? "Nailong";
            var activeBg = (Brush)FindResource("AccentBlue");
            var normalBg = (Brush)FindResource("CardBg");
            var activeFg = Brushes.White;
            var normalFg = (Brush)FindResource("TextSecondary");

            if (BtnCharNailong != null)
            {
                BtnCharNailong.Background = (activeChar == "Nailong") ? activeBg : normalBg;
                BtnCharNailong.Foreground = (activeChar == "Nailong") ? activeFg : normalFg;
            }
            if (BtnCharSpongebob != null)
            {
                BtnCharSpongebob.Background = (activeChar == "Spongebob") ? activeBg : normalBg;
                BtnCharSpongebob.Foreground = (activeChar == "Spongebob") ? activeFg : normalFg;
            }
            if (BtnCharMinion != null)
            {
                BtnCharMinion.Background = (activeChar == "Minion") ? activeBg : normalBg;
                BtnCharMinion.Foreground = (activeChar == "Minion") ? activeFg : normalFg;
            }

            if (MenuCharNailong != null) MenuCharNailong.IsChecked = (activeChar == "Nailong");
            if (MenuCharSpongebob != null) MenuCharSpongebob.IsChecked = (activeChar == "Spongebob");
            if (MenuCharMinion != null) MenuCharMinion.IsChecked = (activeChar == "Minion");

            if (BtnSetIdleCat != null)
            {
                BtnSetIdleCat.Content = (activeChar == "Minion") ? "🍌 Minion" : ((activeChar == "Spongebob") ? "🧽 Spongebob" : "🦖 Nailong");
            }

            if (CatAvatar != null && CatAvatarImage != null)
            {
                if (activeChar == "Minion")
                {
                    double targetH = Math.Clamp(CompactH - 6.0, 28.0, 36.0);
                    double targetW = Math.Round(targetH * (116.0 / 96.0));
                    CatAvatar.Width = targetW;
                    CatAvatar.Height = targetH;
                    CatAvatarImage.Width = targetW;
                    CatAvatarImage.Height = targetH;
                    Canvas.SetTop(CatAvatar, Math.Max(0.5, (CompactH - targetH) / 2.0));
                    _catX = Math.Clamp(_catX, 48, Math.Max(50, IslandBorder.ActualWidth - targetW - 48));
                    Canvas.SetLeft(CatAvatar, _catX);
                    CatAvatar.ToolTip = "🍌 Minion (Klik untuk ajak main!)";
                    if (_minionDanceFrames[0] != null)
                    {
                        CatAvatarImage.Source = _minionDanceFrames[_minionDanceIndex % 210];
                    }
                }
                else if (activeChar == "Spongebob")
                {
                    double targetH = Math.Clamp(CompactH - 5.0, 28.0, 38.0);
                    double targetW = Math.Round(targetH * (492.0 / 265.0));
                    CatAvatar.Width = targetW;
                    CatAvatar.Height = targetH;
                    CatAvatarImage.Width = targetW;
                    CatAvatarImage.Height = targetH;
                    Canvas.SetTop(CatAvatar, Math.Max(0.5, (CompactH - targetH) / 2.0 - 1.0));
                    _catX = Math.Clamp(_catX, 48, Math.Max(50, IslandBorder.ActualWidth - targetW - 48));
                    Canvas.SetLeft(CatAvatar, _catX);
                    CatAvatar.ToolTip = "🧽 Spongebob Squarepants (Klik untuk ajak main!)";
                    if (_spongebobDanceFrames[0] != null)
                    {
                        CatAvatarImage.Source = _spongebobDanceFrames[_spongebobDanceIndex % 17];
                    }
                }
                else
                {
                    double targetH = Math.Clamp(CompactH - 7.0, 26.0, 34.0);
                    double targetW = Math.Round(targetH * (348.0 / 407.0));
                    CatAvatar.Width = targetW;
                    CatAvatar.Height = targetH;
                    CatAvatarImage.Width = targetW;
                    CatAvatarImage.Height = targetH;
                    Canvas.SetTop(CatAvatar, Math.Max(1.0, (CompactH - targetH) / 2.0));
                    _catX = Math.Clamp(_catX, 48, Math.Max(50, IslandBorder.ActualWidth - targetW - 48));
                    Canvas.SetLeft(CatAvatar, _catX);
                    CatAvatar.ToolTip = "🦖 Dinosaurus Nailong (Klik untuk ajak main!)";
                    if (_nailongDanceFrames[0] != null)
                    {
                        CatAvatarImage.Source = _nailongDanceFrames[_nailongDanceIndex % 18];
                    }
                }
            }

            if (SideCatAvatar != null && SideCatAvatarImage != null)
            {
                if (activeChar == "Minion")
                {
                    SideCatAvatar.Width = 34;
                    SideCatAvatar.Height = 28;
                    SideCatAvatarImage.Width = 34;
                    SideCatAvatarImage.Height = 28;
                    if (_minionDanceFrames[0] != null) SideCatAvatarImage.Source = _minionDanceFrames[_minionDanceIndex % 210];
                }
                else if (activeChar == "Spongebob")
                {
                    SideCatAvatar.Width = 36;
                    SideCatAvatar.Height = 22;
                    SideCatAvatarImage.Width = 36;
                    SideCatAvatarImage.Height = 22;
                    if (_spongebobDanceFrames[0] != null) SideCatAvatarImage.Source = _spongebobDanceFrames[_spongebobDanceIndex % 17];
                }
                else
                {
                    SideCatAvatar.Width = 26;
                    SideCatAvatar.Height = 30;
                    SideCatAvatarImage.Width = 26;
                    SideCatAvatarImage.Height = 30;
                    if (_nailongDanceFrames[0] != null) SideCatAvatarImage.Source = _nailongDanceFrames[_nailongDanceIndex % 18];
                }
            }
        }

        private void BtnCharNailong_Click(object sender, RoutedEventArgs e) => SetCharacterAnimation("Nailong");
        private void BtnCharSpongebob_Click(object sender, RoutedEventArgs e) => SetCharacterAnimation("Spongebob");
        private void BtnCharMinion_Click(object sender, RoutedEventArgs e) => SetCharacterAnimation("Minion");
        private void MenuSelectNailong_Click(object sender, RoutedEventArgs e) => SetCharacterAnimation("Nailong");
        private void MenuSelectSpongebob_Click(object sender, RoutedEventArgs e) => SetCharacterAnimation("Spongebob");
        private void MenuSelectMinion_Click(object sender, RoutedEventArgs e) => SetCharacterAnimation("Minion");

        private void TriggerCatEmote(string text, int durationTicks = 20)
        {
            if (string.IsNullOrEmpty(text) || 
                text.Contains("rawr", StringComparison.OrdinalIgnoreCase) || 
                text.Contains("zzz", StringComparison.OrdinalIgnoreCase))
            {
                return;
            }
            if (TxtCatEmote == null || CatEmoteBubble == null) return;
            CatEmoteBubble.Visibility = Visibility.Visible;
            TxtCatEmote.Text = text;
            Canvas.SetLeft(CatEmoteBubble, Math.Max(5, _catX - 6));
            Canvas.SetTop(CatEmoteBubble, 0);
            CatEmoteBubble.Opacity = 1.0;
            _catEmoteTicks = durationTicks;
        }

        private void CatBadge_MouseDown(object sender, MouseButtonEventArgs e)
        {
            e.Handled = true;
            CycleCompactMode(true);
        }

        private void MenuCompactBattery_Click(object sender, RoutedEventArgs e)
        {
            if (Settings != null) Settings.DefaultIdleMode = "Battery";
            SetCompactMode(CompactMode.Battery);
        }

        private void MenuCompactMusic_Click(object sender, RoutedEventArgs e)
        {
            if (Settings != null) Settings.DefaultIdleMode = "Music";
            SetCompactMode(CompactMode.Music);
        }

        private void MenuCompactLyrics_Click(object sender, RoutedEventArgs e)
        {
            if (Settings != null) Settings.DefaultIdleMode = "Lyrics";
            SetCompactMode(CompactMode.Lyrics);
        }

        private void MenuCompactPerf_Click(object sender, RoutedEventArgs e)
        {
            if (Settings != null) Settings.DefaultIdleMode = "Performance";
            SetCompactMode(CompactMode.Performance);
        }

        private void MenuCompactCat_Click(object sender, RoutedEventArgs e)
        {
            if (Settings != null) Settings.DefaultIdleMode = "Cat";
            SetCompactMode(CompactMode.Cat);
        }

        private void MenuCompactClock_Click(object sender, RoutedEventArgs e)
        {
            if (Settings != null) Settings.DefaultIdleMode = "Clock";
            SetCompactMode(CompactMode.Clock);
        }

        private void BtnSetIdleBattery_Click(object sender, RoutedEventArgs e)
        {
            if (Settings != null) Settings.DefaultIdleMode = "Battery";
            SetCompactMode(CompactMode.Battery);
        }

        private void BtnSetIdleMusic_Click(object sender, RoutedEventArgs e)
        {
            if (Settings != null) Settings.DefaultIdleMode = "Music";
            SetCompactMode(CompactMode.Music);
        }

        private void BtnSetIdleLyrics_Click(object sender, RoutedEventArgs e)
        {
            if (Settings != null) Settings.DefaultIdleMode = "Lyrics";
            SetCompactMode(CompactMode.Lyrics);
        }

        private void CompactLyrics_Click(object sender, MouseButtonEventArgs e)
        {
            e.Handled = true;
            SetActiveView(ViewLyrics, BtnLyrics);
            ExpandIsland();
            _isPinned = true;
            PinIcon.Opacity = 1.0;
            PinIcon.Foreground = (Brush)FindResource("AccentBlue");
        }

        private void BtnSetIdlePerf_Click(object sender, RoutedEventArgs e)
        {
            if (Settings != null) Settings.DefaultIdleMode = "Performance";
            SetCompactMode(CompactMode.Performance);
        }

        private void BtnSetIdleCat_Click(object sender, RoutedEventArgs e)
        {
            if (Settings != null) Settings.DefaultIdleMode = "Cat";
            SetCompactMode(CompactMode.Cat);
        }

        private void BtnSetIdleClock_Click(object sender, RoutedEventArgs e)
        {
            if (Settings != null) Settings.DefaultIdleMode = "Clock";
            SetCompactMode(CompactMode.Clock);
        }

        private void MenuToggleAutoMusic_Click(object sender, RoutedEventArgs e)
        {
            _autoSwitchMusic = MenuAutoMusic.IsChecked;
            if (Settings != null) Settings.AutoSwitchMusic = _autoSwitchMusic;
        }

        private void SetCompactWidthPreset(double width)
        {
            if (Settings != null)
            {
                Settings.CompactWidth = width;
                if (!_isExpanded)
                {
                    IslandBorder.Width = width;
                    UpdateNotchGeometry(width, CompactH);
                    CenterToTop();
                }
            }
        }

        private void MenuSetCompact190_Click(object sender, RoutedEventArgs e) => SetCompactWidthPreset(190);
        private void MenuSetCompact240_Click(object sender, RoutedEventArgs e) => SetCompactWidthPreset(240);
        private void MenuSetCompact280_Click(object sender, RoutedEventArgs e) => SetCompactWidthPreset(280);
        private void MenuSetCompact320_Click(object sender, RoutedEventArgs e) => SetCompactWidthPreset(320);



        private void IslandBorder_MouseRightButtonUp(object sender, MouseButtonEventArgs e)
        {
            e.Handled = true;
            OpenSettingsWindow();
        }

        public void OpenSettingsWindow(string? initialTab = null)
        {
            try
            {
                if (_settingsWindow != null && _settingsWindow.IsLoaded)
                {
                    _settingsWindow.Activate();
                    if (_settingsWindow.WindowState == WindowState.Minimized)
                        _settingsWindow.WindowState = WindowState.Normal;
                    return;
                }

                _settingsWindow = new SettingsWindow(this, Settings);
                _settingsWindow.Closed += (s, ev) => _settingsWindow = null;
                _settingsWindow.Show();
                _settingsWindow.Activate();
            }
            catch (Exception ex)
            {
                Console.WriteLine($"[ERROR] OpenSettingsWindow: {ex}");
                try
                {
                    var logPath = System.IO.Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData), "DynamicIslandPro", "app.log");
                    System.IO.File.AppendAllText(logPath, $"[{DateTime.Now}] OpenSettingsWindow Exception: {ex}\n");
                }
                catch { }
            }
        }

        public void UpdateHorizontalPosition(double percent)
        {
            _horizontalPositionPercent = Math.Clamp(percent, 0.0, 1.0);
            double screenW = SystemParameters.PrimaryScreenWidth;
            Left = (screenW - Width) * _horizontalPositionPercent;
        }

        public void SetAutoHide(bool autoHide)
        {
            if (autoHide && !_isExpanded)
            {
                IslandBorder.Height = 8;
                IslandBorder.Opacity = 0.4;
            }
            else
            {
                IslandBorder.Height = CompactH;
                IslandBorder.Opacity = 1.0;
            }
        }

        public void SetHideButtonVisible(bool visible)
        {
            if (Settings != null) Settings.ShowHideButton = visible;
        }

        public void SelectIdleModeByName(string modeStr)
        {
            if (Enum.TryParse<CompactMode>(modeStr, true, out var mode))
            {
                SetCompactMode(mode);
            }
        }

        private void MenuShowSettings_Click(object sender, RoutedEventArgs e)
        {
            OpenSettingsWindow();
        }

        // ==============================================
        // DYNAMIC ISLAND SETTINGS & RESIZING LOGIC
        // ==============================================
        private void PresetCompact_Click(object sender, RoutedEventArgs e)
        {
            Settings.ApplyPreset("kecil");
            ApplyCurrentSizeSettings();
        }

        private void PresetDefault_Click(object sender, RoutedEventArgs e)
        {
            Settings.ApplyPreset("standar");
            ApplyCurrentSizeSettings();
        }

        private void PresetLarge_Click(object sender, RoutedEventArgs e)
        {
            Settings.ApplyPreset("besar");
            ApplyCurrentSizeSettings();
        }

        private void SliderDimensions_ValueChanged(object sender, RoutedPropertyChangedEventArgs<double> e)
        {
            if (IslandBorder != null && Settings != null)
            {
                ApplyCurrentSizeSettings();
            }
        }

        private void SliderPosX_ValueChanged(object sender, RoutedPropertyChangedEventArgs<double> e)
        {
            if (Settings != null) Settings.PositionOffsetX = 0;
            CenterToTop();
        }

        private void BtnResetX_Click(object sender, RoutedEventArgs e)
        {
            if (Settings != null) Settings.PositionOffsetX = 0;
            CenterToTop();
        }

        private void SliderOpacity_ValueChanged(object sender, RoutedPropertyChangedEventArgs<double> e)
        {
            if (IslandBorder != null && Settings != null)
            {
                IslandBorder.Opacity = Settings.NotchOpacity / 100.0;
            }
        }

        private void ChkAutoExpand_Click(object sender, RoutedEventArgs e)
        {
            if (Settings != null) _autoExpandOnHover = Settings.AutoExpandOnHover;
        }

        private void ChkAutoMusic_Click(object sender, RoutedEventArgs e)
        {
            if (Settings != null)
            {
                _autoSwitchMusic = Settings.AutoSwitchMusic;
                MenuAutoMusic.IsChecked = _autoSwitchMusic;
            }
        }

        private void ChkAlwaysOnTop_Click(object sender, RoutedEventArgs e)
        {
            if (Settings != null) Topmost = Settings.AlwaysOnTop;
        }

        private void SpeedFast_Click(object sender, RoutedEventArgs e)
        {
            if (Settings != null) Settings.AnimationSpeedMs = 150;
        }

        private void SpeedNormal_Click(object sender, RoutedEventArgs e)
        {
            if (Settings != null) Settings.AnimationSpeedMs = 240;
        }

        private void SpeedSlow_Click(object sender, RoutedEventArgs e)
        {
            if (Settings != null) Settings.AnimationSpeedMs = 350;
        }

        private void BtnResetAllSettings_Click(object sender, RoutedEventArgs e)
        {
            if (Settings != null)
            {
                Settings.ResetToDefault();
                _autoExpandOnHover = Settings.AutoExpandOnHover;
                _autoSwitchMusic = Settings.AutoSwitchMusic;
                Topmost = Settings.AlwaysOnTop;
                MenuAutoMusic.IsChecked = _autoSwitchMusic;
                ApplyCurrentSizeSettings();
            }
        }

        public void ApplyCurrentSizeSettings()
        {
            if (!_isExpanded)
            {
                if (CurrentDock == IslandDock.Top)
                {
                    IslandBorder.Width = CompactW;
                    IslandBorder.Height = CompactH;
                    UpdateNotchGeometry(CompactW, CompactH);
                }
                else
                {
                    IslandBorder.Width = 40;
                    IslandBorder.Height = 152;
                    UpdateNotchGeometry(40, 152);
                }
            }
            else
            {
                IslandBorder.Width = ExpandedW;
                IslandBorder.Height = ExpandedH;
                UpdateNotchGeometry(ExpandedW, ExpandedH);
            }
            ApplyDockLayout();
            UpdateCharacterVisuals();
        }

        // ==============================================
        // GAMING & FULLSCREEN ANTI-DISRUPTION SERVICE
        // ==============================================
        private void HoverDwellTimer_Tick(object? sender, EventArgs e)
        {
            _hoverDwellTimer.Stop();
            if (!_isExpanded && !_isCompactInteracting && IslandBorder.IsMouseOver && !IsGameBlockingActive())
            {
                ExpandIsland();
            }
        }

        private bool IsGameBlockingActive()
        {
            if (Settings == null) return false;
            return Settings.ManualGameMode || (Settings.AutoGameMode && _isInGameOrFullscreen);
        }

        private void GameCheckTimer_Tick(object? sender, EventArgs e)
        {
            if (Settings == null) return;

            if (!Settings.AutoGameMode)
            {
                if (_isInGameOrFullscreen)
                {
                    _isInGameOrFullscreen = false;
                    UpdateGameModeVisuals();
                }
                return;
            }

            bool isGameFullscreen = DetectFullscreenOrGame();
            if (isGameFullscreen != _isInGameOrFullscreen)
            {
                _isInGameOrFullscreen = isGameFullscreen;
                UpdateGameModeVisuals();
            }
        }

        private bool DetectFullscreenOrGame()
        {
            try
            {
                // 1. Windows Shell API: DirectX 3D Fullscreen Exclusive / Presentation / Busy state
                if (NativeMethods.SHQueryUserNotificationState(out var state) == 0)
                {
                    if (state == NativeMethods.QUERY_USER_NOTIFICATION_STATE.QUNS_RUNNING_D_3D_FULL_SCREEN_EXCLUSIVE ||
                        state == NativeMethods.QUERY_USER_NOTIFICATION_STATE.QUNS_PRESENTATION_MODE ||
                        state == NativeMethods.QUERY_USER_NOTIFICATION_STATE.QUNS_BUSY)
                    {
                        return true;
                    }
                }

                // 2. Window Bounds check (Borderless Fullscreen Games)
                IntPtr fgHwnd = NativeMethods.GetForegroundWindow();
                if (fgHwnd == IntPtr.Zero) return false;

                // Ignore if foreground window is Dynamic Island itself
                var helper = new System.Windows.Interop.WindowInteropHelper(this);
                if (fgHwnd == helper.Handle) return false;

                // Ignore Windows Shell, Desktop, and Taskbar
                var className = new System.Text.StringBuilder(256);
                NativeMethods.GetClassName(fgHwnd, className, className.Capacity);
                string cls = className.ToString();
                if (cls == "Progman" || cls == "WorkerW" || cls == "Shell_TrayWnd") return false;

                if (NativeMethods.GetWindowRect(fgHwnd, out var rect))
                {
                    double screenW = SystemParameters.PrimaryScreenWidth;
                    double screenH = SystemParameters.PrimaryScreenHeight;

                    // Covers entire monitor resolution -> Fullscreen game or app
                    if (rect.Left <= 0 && rect.Top <= 0 && rect.Right >= (int)screenW && rect.Bottom >= (int)screenH)
                    {
                        return true;
                    }
                }
            }
            catch { }

            return false;
        }

        private void UpdateGameModeVisuals()
        {
            if (Settings == null) return;

            bool isBlocking = IsGameBlockingActive();

            // 1. Update In-App Setting Button
            if (BtnToggleGameManual != null)
            {
                if (Settings.ManualGameMode)
                {
                    BtnToggleGameManual.Content = "🎮 Mode Game: ON";
                    BtnToggleGameManual.Background = new SolidColorBrush(Color.FromRgb(40, 20, 36));
                    BtnToggleGameManual.Foreground = (Brush)FindResource("AccentOrange");
                }
                else if (_isInGameOrFullscreen && Settings.AutoGameMode)
                {
                    BtnToggleGameManual.Content = "🎮 Game Terdeteksi (Aktif)";
                    BtnToggleGameManual.Background = new SolidColorBrush(Color.FromRgb(30, 20, 40));
                    BtnToggleGameManual.Foreground = (Brush)FindResource("BatteryGreen");
                }
                else
                {
                    BtnToggleGameManual.Content = "🎮 Mode Game: OFF";
                    BtnToggleGameManual.Background = new SolidColorBrush(Color.FromRgb(22, 22, 30));
                    BtnToggleGameManual.Foreground = (Brush)FindResource("TextSecondary");
                }
            }

            // 2. Update Context Menu
            if (MenuGameMode != null)
            {
                MenuGameMode.IsChecked = Settings.ManualGameMode || isBlocking;
            }

            // 3. Update Action Buttons in Settings
            if (BtnGameActionIgnore != null && BtnGameActionHide != null)
            {
                bool isHide = Settings.GameAction == "AutoHide";
                var activeBg = new SolidColorBrush(Color.FromRgb(34, 24, 50));
                var normalBg = new SolidColorBrush(Color.FromRgb(22, 22, 30));
                var activeBorder = (Brush)FindResource("AccentBlue");
                var normalBorder = (Brush)FindResource("CardBorder");

                BtnGameActionIgnore.Background = isHide ? normalBg : activeBg;
                BtnGameActionIgnore.BorderBrush = isHide ? normalBorder : activeBorder;
                BtnGameActionIgnore.Foreground = isHide ? (Brush)FindResource("TextSecondary") : Brushes.White;

                BtnGameActionHide.Background = isHide ? activeBg : normalBg;
                BtnGameActionHide.BorderBrush = isHide ? activeBorder : normalBorder;
                BtnGameActionHide.Foreground = isHide ? Brushes.White : (Brush)FindResource("TextSecondary");
            }

            // 4. Enforce Window State (Dynamic Island Stays Visible On Top During Game)
            var helper = new System.Windows.Interop.WindowInteropHelper(this);
            if (isBlocking)
            {
                // Collapse if currently open so it stays sleek and doesn't block gameplay
                if (_isExpanded && !_isPinned)
                {
                    CollapseIsland();
                }

                // Dynamic Island stays visible on top during gaming
                IslandBorder.Visibility = Visibility.Visible;
                if (Settings != null) this.Topmost = Settings.AlwaysOnTop;
                if (helper.Handle != IntPtr.Zero)
                {
                    NativeMethods.SetWindowPos(helper.Handle, NativeMethods.HWND_TOPMOST, 0, 0, 0, 0,
                        NativeMethods.SWP_NOMOVE | NativeMethods.SWP_NOSIZE | NativeMethods.SWP_NOACTIVATE);
                }
            }
            else
            {
                IslandBorder.Visibility = Visibility.Visible;
                if (Settings != null) this.Topmost = Settings.AlwaysOnTop;
                if (helper.Handle != IntPtr.Zero)
                {
                    NativeMethods.SetWindowPos(helper.Handle, NativeMethods.HWND_TOPMOST, 0, 0, 0, 0,
                        NativeMethods.SWP_NOMOVE | NativeMethods.SWP_NOSIZE | NativeMethods.SWP_NOACTIVATE);
                }
            }
        }

        private void ChkAutoGameMode_Click(object sender, RoutedEventArgs e)
        {
            UpdateGameModeVisuals();
        }

        private void BtnToggleGameManual_Click(object sender, RoutedEventArgs e)
        {
            if (Settings != null)
            {
                Settings.ManualGameMode = !Settings.ManualGameMode;
                UpdateGameModeVisuals();
            }
        }

        private void MenuGameMode_Click(object sender, RoutedEventArgs e)
        {
            if (Settings != null)
            {
                Settings.ManualGameMode = MenuGameMode.IsChecked;
                UpdateGameModeVisuals();
            }
        }

        private void BtnGameActionIgnore_Click(object sender, RoutedEventArgs e)
        {
            if (Settings != null)
            {
                Settings.GameAction = "IgnoreHover";
                UpdateGameModeVisuals();
            }
        }

        private void BtnGameActionHide_Click(object sender, RoutedEventArgs e)
        {
            if (Settings != null)
            {
                Settings.GameAction = "AutoHide";
                UpdateGameModeVisuals();
            }
        }

        private void MenuShowHeadset_Click(object sender, RoutedEventArgs e)
        {
            Battery.ActiveDisplayDevice = Battery.Headset;
        }

        private void MenuShowMouse_Click(object sender, RoutedEventArgs e)
        {
            Battery.ActiveDisplayDevice = Battery.Mouse;
        }

        private void MenuOpenAppleMusic_Click(object sender, RoutedEventArgs e)
        {
            Media.OpenAppleMusic();
        }

        private void MenuTogglePin_Click(object sender, RoutedEventArgs e)
        {
            _isPinned = !_isPinned;
            PinIcon.Opacity = _isPinned ? 1.0 : 0.5;
            PinIcon.Foreground = _isPinned 
                ? (Brush)FindResource("AccentBlue") 
                : (Brush)FindResource("TextSecondary");

            if (_isPinned)
            {
                IslandBorder.Cursor = Cursors.Arrow;
                _isCompactInteracting = false;
                if (IslandBorder.IsMouseCaptured)
                {
                    IslandBorder.ReleaseMouseCapture();
                }
                if (!_isExpanded) ExpandIsland();
            }
        }

        private void MenuResetPosition_Click(object sender, RoutedEventArgs e)
        {
            CenterToTop();
        }

        // ==============================================
        // NOTIFICATION BANNER LOGIC & TEST HANDLERS
        // ==============================================
        private void Notifications_NotificationReceived(NotificationItem item)
        {
            ShowNotification(item);
        }

        public void ShowNotification(NotificationItem item)
        {
            if (Settings != null)
            {
                if (!Settings.EnableNotifications) return;
                if (item.AppType == NotificationAppType.Discord && !Settings.EnableDiscordNotif) return;
                if (item.AppType == NotificationAppType.Steam && !Settings.EnableSteamNotif) return;
            }

            _activeNotification = item;
            _isNotificationShowing = true;

            // Fill UI
            if (TxtNotifTitle != null) TxtNotifTitle.Text = item.Title;
            if (TxtNotifSubtitle != null) TxtNotifSubtitle.Text = $" • {item.AppName}";
            if (TxtNotifBody != null) TxtNotifBody.Text = item.Message;

            if (NotifIconBadge != null && TxtNotifIcon != null)
            {
                if (item.AppType == NotificationAppType.Discord)
                {
                    NotifIconBadge.Background = new SolidColorBrush((Color)ColorConverter.ConvertFromString("#5865F2"));
                    TxtNotifIcon.Text = "💬";
                }
                else if (item.AppType == NotificationAppType.Steam)
                {
                    NotifIconBadge.Background = new SolidColorBrush((Color)ColorConverter.ConvertFromString("#1A9FFF"));
                    TxtNotifIcon.Text = "🎮";
                }
                else
                {
                    try
                    {
                        NotifIconBadge.Background = new SolidColorBrush((Color)ColorConverter.ConvertFromString(item.AccentColor ?? "#0A84FF"));
                    }
                    catch
                    {
                        NotifIconBadge.Background = new SolidColorBrush((Color)ColorConverter.ConvertFromString("#0A84FF"));
                    }
                    TxtNotifIcon.Text = "🔔";
                }
            }

            if (!_isExpanded)
            {
                if (CompactView != null) CompactView.Visibility = Visibility.Collapsed;
                if (NotificationBannerView != null)
                {
                    NotificationBannerView.Visibility = Visibility.Visible;
                    NotificationBannerView.Opacity = 1;
                }

                double targetWidth = (CurrentDock == IslandDock.Top) ? Math.Max(CompactW, 360) : 220;
                double targetHeight = (CurrentDock == IslandDock.Top) ? CompactH : 40;

                var cubicEase = new CubicEase { EasingMode = EasingMode.EaseOut };
                var animW = new DoubleAnimation(IslandBorder.ActualWidth, targetWidth, TimeSpan.FromMilliseconds(220))
                {
                    EasingFunction = cubicEase
                };
                var animH = new DoubleAnimation(IslandBorder.ActualHeight, targetHeight, TimeSpan.FromMilliseconds(220))
                {
                    EasingFunction = cubicEase
                };

                IslandBorder.BeginAnimation(WidthProperty, animW);
                IslandBorder.BeginAnimation(HeightProperty, animH);
            }

            _notifDwellTimer.Stop();
            _notifDwellTimer.Start();
        }

        public void DismissNotification()
        {
            _notifDwellTimer.Stop();
            if (!_isNotificationShowing) return;
            _isNotificationShowing = false;
            _activeNotification = null;

            if (!_isExpanded)
            {
                double targetW = (CurrentDock == IslandDock.Top) ? CompactW : 40;
                double targetH = (CurrentDock == IslandDock.Top) ? CompactH : 152;

                var cubicEase = new CubicEase { EasingMode = EasingMode.EaseOut };
                var animW = new DoubleAnimation(IslandBorder.ActualWidth, targetW, TimeSpan.FromMilliseconds(220))
                {
                    EasingFunction = cubicEase
                };
                var animH = new DoubleAnimation(IslandBorder.ActualHeight, targetH, TimeSpan.FromMilliseconds(220))
                {
                    EasingFunction = cubicEase
                };

                animW.Completed += (s, e) =>
                {
                    if (NotificationBannerView != null) NotificationBannerView.Visibility = Visibility.Collapsed;
                    if (!_isExpanded && CompactView != null)
                    {
                        CompactView.Visibility = Visibility.Visible;
                        CompactView.Opacity = 1;
                        if (CurrentDock == IslandDock.Top)
                        {
                            if (CompactHorizontalContainer != null) CompactHorizontalContainer.Visibility = Visibility.Visible;
                            if (CompactSideContainer != null) CompactSideContainer.Visibility = Visibility.Collapsed;
                        }
                        else
                        {
                            if (CompactHorizontalContainer != null) CompactHorizontalContainer.Visibility = Visibility.Collapsed;
                            if (CompactSideContainer != null) CompactSideContainer.Visibility = Visibility.Visible;
                        }
                    }
                };

                IslandBorder.BeginAnimation(WidthProperty, animW);
                IslandBorder.BeginAnimation(HeightProperty, animH);
            }
            else
            {
                if (NotificationBannerView != null) NotificationBannerView.Visibility = Visibility.Collapsed;
            }
        }

        private void NotificationBanner_Click(object sender, MouseButtonEventArgs e)
        {
            if (_activeNotification != null && !string.IsNullOrWhiteSpace(_activeNotification.TargetProcess))
            {
                NativeMethods.FocusProcessMainWindow(_activeNotification.TargetProcess);
            }
            DismissNotification();
        }

        private void BtnDismissNotif_Click(object sender, MouseButtonEventArgs e)
        {
            e.Handled = true;
            DismissNotification();
        }

        private void MenuTestDiscord_Click(object sender, RoutedEventArgs e)
        {
            Notifications.SimulateDiscordNotification();
        }

        private void MenuTestSteam_Click(object sender, RoutedEventArgs e)
        {
            Notifications.SimulateSteamNotification();
        }

        private void MenuOpenNotifSettings_Click(object sender, RoutedEventArgs e)
        {
            Notifications.OpenNotificationSettings();
        }

        // ==============================================
        // DRAG & DROP FOR SHELF & AIRDROP (DROP-ZONE)
        // ==============================================
        private void IslandBorder_DragEnter(object sender, DragEventArgs e)
        {
            if (e.Data.GetDataPresent(DataFormats.FileDrop))
            {
                e.Effects = DragDropEffects.Copy;
                if (DropZoneOverlay != null)
                {
                    DropZoneOverlay.Visibility = Visibility.Visible;
                    DropZoneOverlay.Opacity = 1;
                }
            }
            else
            {
                e.Effects = DragDropEffects.None;
            }
            e.Handled = true;
        }

        private void IslandBorder_DragOver(object sender, DragEventArgs e)
        {
            if (e.Data.GetDataPresent(DataFormats.FileDrop))
            {
                e.Effects = DragDropEffects.Copy;
                if (DropZoneOverlay != null && DropZoneOverlay.Visibility != Visibility.Visible)
                {
                    DropZoneOverlay.Visibility = Visibility.Visible;
                    DropZoneOverlay.Opacity = 1;
                }
            }
            else
            {
                e.Effects = DragDropEffects.None;
            }
            e.Handled = true;
        }

        private void IslandBorder_DragLeave(object sender, DragEventArgs e)
        {
            if (DropZoneOverlay != null)
            {
                DropZoneOverlay.Visibility = Visibility.Collapsed;
            }
            e.Handled = true;
        }

        private void IslandBorder_Drop(object sender, DragEventArgs e)
        {
            if (DropZoneOverlay != null)
            {
                DropZoneOverlay.Visibility = Visibility.Collapsed;
            }

            if (e.Data.GetDataPresent(DataFormats.FileDrop))
            {
                if (e.Data.GetData(DataFormats.FileDrop) is string[] files && files.Length > 0)
                {
                    Shelf.AddFiles(files);
                    if (!_isExpanded) ExpandIsland();
                    SetActiveView(ViewShelf, BtnShelf);
                    _isPinned = true;
                    PinIcon.Opacity = 1.0;
                    PinIcon.Foreground = (Brush)FindResource("AccentBlue");
                }
            }
            e.Handled = true;
        }

        // ==============================================
        // SHELF TOOLBAR & ACTIONS
        // ==============================================
        private void BtnAddShelfFile_Click(object sender, RoutedEventArgs e)
        {
            try
            {
                var dialog = new Microsoft.Win32.OpenFileDialog
                {
                    Title = "Pilih Berkas untuk Ditaruh di Dynamic Island Shelf",
                    Multiselect = true,
                    Filter = "Semua Berkas (*.*)|*.*|Gambar (*.png;*.jpg;*.webp)|*.png;*.jpg;*.webp;*.jpeg|Dokumen (*.pdf;*.docx;*.txt)|*.pdf;*.docx;*.txt"
                };

                if (dialog.ShowDialog() == true)
                {
                    Shelf.AddFiles(dialog.FileNames);
                }
            }
            catch { }
        }

        private void BtnClearShelf_Click(object sender, RoutedEventArgs e)
        {
            Shelf.ClearAll();
        }

        private void ShelfItemOpen_Click(object sender, RoutedEventArgs e)
        {
            if (sender is FrameworkElement fe && fe.DataContext is ShelfItem item)
            {
                Shelf.OpenFile(item);
            }
        }

        private void ShelfItemExplorer_Click(object sender, RoutedEventArgs e)
        {
            if (sender is FrameworkElement fe && fe.DataContext is ShelfItem item)
            {
                Shelf.ShowInExplorer(item);
            }
        }

        private void ShelfItemCopy_Click(object sender, RoutedEventArgs e)
        {
            if (sender is FrameworkElement fe && fe.DataContext is ShelfItem item)
            {
                Shelf.CopyToClipboard(item);
            }
        }

        private void ShelfItemRemove_Click(object sender, RoutedEventArgs e)
        {
            if (sender is FrameworkElement fe && fe.DataContext is ShelfItem item)
            {
                Shelf.RemoveItem(item);
            }
        }

        // Dragging items OUT from the shelf to Discord, Steam, Explorer, etc.
        private Point _shelfDragStart;
        private void ShelfItem_PreviewMouseLeftButtonDown(object sender, MouseButtonEventArgs e)
        {
            _shelfDragStart = e.GetPosition(null);
        }

        private void ShelfItem_MouseMove(object sender, MouseEventArgs e)
        {
            if (e.LeftButton == MouseButtonState.Pressed)
            {
                Point current = e.GetPosition(null);
                Vector diff = _shelfDragStart - current;
                if (Math.Abs(diff.X) > SystemParameters.MinimumHorizontalDragDistance ||
                    Math.Abs(diff.Y) > SystemParameters.MinimumVerticalDragDistance)
                {
                    if (sender is FrameworkElement fe && fe.DataContext is ShelfItem item && (System.IO.File.Exists(item.FullPath) || System.IO.Directory.Exists(item.FullPath)))
                    {
                        var data = new DataObject(DataFormats.FileDrop, new string[] { item.FullPath });
                        DragDrop.DoDragDrop(fe, data, DragDropEffects.Copy | DragDropEffects.Move);
                    }
                }
            }
        }

        // AirDrop sharing actions
        private void BtnCopyAirDropUrl_Click(object sender, RoutedEventArgs e)
        {
            try
            {
                System.Windows.Clipboard.SetText(Shelf.AirDropUrl);
                Notifications.TriggerNotification(NotificationAppType.General, "AirDrop Web Portal", "Tautan Disalin", $"Buka {Shelf.AirDropUrl} di browser HP untuk mengunduh berkas", "#0A84FF", "", "AirDrop Link");
            }
            catch { }
        }

        private void BtnShareDiscord_Click(object sender, RoutedEventArgs e)
        {
            if (Shelf.Items.Count > 0)
            {
                Shelf.ShareToDiscord(Shelf.Items[0]);
            }
            else
            {
                Notifications.TriggerNotification(NotificationAppType.Discord, "Discord Share", "Shelf Kosong", "Taruh file di Shelf terlebih dahulu sebelum mengirim ke Discord", "#5865F2", "Discord", "Shelf");
            }
        }

        private void BtnShareSteam_Click(object sender, RoutedEventArgs e)
        {
            if (Shelf.Items.Count > 0)
            {
                Shelf.ShareToSteam(Shelf.Items[0]);
            }
            else
            {
                Notifications.TriggerNotification(NotificationAppType.Steam, "Steam Share", "Shelf Kosong", "Taruh file di Shelf terlebih dahulu sebelum mengirim ke Steam", "#1A9FFF", "steam", "Shelf");
            }
        }

        private void BtnNearbyShare_Click(object sender, RoutedEventArgs e)
        {
            Shelf.OpenWindowsNearbyShare();
        }

        private void MenuExit_Click(object sender, RoutedEventArgs e)
        {
            Application.Current.Shutdown();
        }
    }
}
