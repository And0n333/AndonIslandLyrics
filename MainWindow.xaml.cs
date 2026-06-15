using System.ComponentModel;
using System.Diagnostics;
using System.Globalization;
using System.IO;
using System.Net;
using System.Net.Http;
using System.Net.Http.Headers;
using System.Numerics;
using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using System.Text.Json.Serialization;
using System.Text.RegularExpressions;
using System.Xml.Linq;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Interop;
using System.Windows.Markup;
using System.Windows.Media;
using System.Windows.Media.Animation;
using System.Windows.Media.Imaging;
using System.Windows.Threading;
using Windows.Media.Control;
using Windows.Storage.Streams;
using DynamicIslandLyrics.Models;

[assembly: InternalsVisibleTo("DynamicIslandLyrics")]

namespace DynamicIslandLyrics;

public partial class MainWindow : Window
{
    private const int WM_WINDOWPOSCHANGING = 0x0046;

    private readonly LyricsIslandViewModel _viewModel;
    private Hardcodet.Wpf.TaskbarNotification.TaskbarIcon? _taskbarIcon;
    private HwndSource? _hwndSource;
    private string _previousLyric = string.Empty;

    public MainWindow()
    {
        InitializeComponent();
        _viewModel = new LyricsIslandViewModel();
        DataContext = _viewModel;

        // 监听歌词变化触发动画
        _viewModel.PropertyChanged += OnViewModelPropertyChanged;

        // 监听窗口位置变化以更新上边圆角
        DependencyPropertyDescriptor.FromProperty(TopProperty, typeof(MainWindow))
            .AddValueChanged(this, (_, _) => UpdateTopEdgeCorner());

        // 窗口大小变化时也需要更新
        DependencyPropertyDescriptor.FromProperty(WidthProperty, typeof(MainWindow))
            .AddValueChanged(this, (_, _) => UpdateTopEdgeCorner());
    }

    private async void Window_Loaded(object sender, RoutedEventArgs e)
    {
        // 设置 Win32 消息钩子，拦截窗口位置变化防止触发 Snap
        _hwndSource = PresentationSource.FromVisual(this) as HwndSource;
        if (_hwndSource is { Handle: not 0 })
        {
            _hwndSource.AddHook(WndProc);
        }

        InitializeTaskbarIcon();
        await _viewModel.InitializeAsync();
    }

    private void Window_Closing(object? sender, CancelEventArgs e)
    {
        _viewModel.Dispose();
        _taskbarIcon?.Dispose();
    }

    private void Island_MouseLeftButtonDown(object sender, MouseButtonEventArgs e)
    {
        if (e.ClickCount == 2)
        {
            _viewModel.IsAlwaysOnTop = !_viewModel.IsAlwaysOnTop;
            return;
        }

        DragMove();
    }

    private void InitializeTaskbarIcon()
    {
        _taskbarIcon = new Hardcodet.Wpf.TaskbarNotification.TaskbarIcon
        {
            Icon = CreateAppIcon(),
            ToolTipText = "Dynamic Island Lyrics",
            Visibility = Visibility.Visible
        };

        // 双击托盘图标显示/隐藏窗口
        _taskbarIcon.TrayMouseDoubleClick += (_, _) =>
        {
            Show();
            Activate();
        };

        // 右键上下文菜单
        var contextMenu = new ContextMenu();

        // — 动画模式标题 —
        contextMenu.Items.Add(new MenuItem
        {
            Header = "歌词动画模式",
            IsEnabled = false,
            FontWeight = FontWeights.Bold
        });

        // 淡入淡出
        var fadeItem = new MenuItem
        {
            Header = "淡入淡出",
            IsCheckable = true,
            IsChecked = _viewModel.AnimationMode == LyricAnimationMode.Fade,
            Tag = LyricAnimationMode.Fade
        };
        fadeItem.Click += (_, _) => SetAnimationMode(LyricAnimationMode.Fade, fadeItem);
        contextMenu.Items.Add(fadeItem);

        // 缩放
        var scaleItem = new MenuItem
        {
            Header = "缩放进入",
            IsCheckable = true,
            IsChecked = _viewModel.AnimationMode == LyricAnimationMode.Scale,
            Tag = LyricAnimationMode.Scale
        };
        scaleItem.Click += (_, _) => SetAnimationMode(LyricAnimationMode.Scale, scaleItem);
        contextMenu.Items.Add(scaleItem);

        contextMenu.Items.Add(new Separator());

        // — 置顶 —
        var pinItem = new MenuItem
        {
            Header = "始终置顶",
            IsCheckable = true,
            IsChecked = _viewModel.IsAlwaysOnTop
        };
        pinItem.Click += (_, _) => _viewModel.IsAlwaysOnTop = !_viewModel.IsAlwaysOnTop;
        // 同步置顶状态
        _viewModel.PropertyChanged += (_, args) =>
        {
            if (args.PropertyName == nameof(LyricsIslandViewModel.IsAlwaysOnTop))
            {
                pinItem.IsChecked = _viewModel.IsAlwaysOnTop;
            }
        };
        contextMenu.Items.Add(pinItem);

        contextMenu.Items.Add(new Separator());

        // — 退出 —
        var exitItem = new MenuItem { Header = "退出" };
        exitItem.Click += (_, _) => Application.Current.Shutdown();
        contextMenu.Items.Add(exitItem);

        _taskbarIcon.ContextMenu = contextMenu;
    }

    private void SetAnimationMode(LyricAnimationMode mode, MenuItem? currentItem = null)
    {
        _viewModel.AnimationMode = mode;

        // 更新菜单项的勾选状态
        if (_taskbarIcon?.ContextMenu is ContextMenu menu)
        {
            foreach (var item in menu.Items.OfType<MenuItem>())
            {
                if (item.Tag is LyricAnimationMode)
                {
                    item.IsChecked = item.Tag is LyricAnimationMode m && m == mode;
                }
            }
        }
    }

    private static System.Drawing.Icon CreateAppIcon()
    {
        // 用程序集图标或生成一个简单的图标
        using var bitmap = new System.Drawing.Bitmap(16, 16);
        using var g = System.Drawing.Graphics.FromImage(bitmap);
        g.Clear(System.Drawing.Color.Transparent);
        g.SmoothingMode = System.Drawing.Drawing2D.SmoothingMode.AntiAlias;

        // 绘制一个圆形音符图标
        using var brush = new System.Drawing.SolidBrush(System.Drawing.Color.FromArgb(0x2F, 0x7C, 0xE6, 0xC8));
        g.FillEllipse(brush, 0, 0, 15, 15);

        // 绘制 "♪" 字符
        using var font = new System.Drawing.Font("Segoe UI Symbol", 9, System.Drawing.FontStyle.Bold);
        using var textBrush = new System.Drawing.SolidBrush(System.Drawing.Color.White);
        g.DrawString("♪", font, textBrush, 2, 1);

        return System.Drawing.Icon.FromHandle(bitmap.GetHicon());
    }


    #region 歌词动画

    private void OnViewModelPropertyChanged(object? sender, PropertyChangedEventArgs e)
    {
        if (e.PropertyName == nameof(LyricsIslandViewModel.CurrentLyric))
        {
            var newLyric = _viewModel.CurrentLyric;
            if (newLyric != _previousLyric)
            {
                _previousLyric = newLyric;
                AnimateLyric();
            }
        }
    }

    private void AnimateLyric()
    {
        if (LyricTextBlock == null) return;

        // 重置 Transform
        var transform = LyricTextBlock.RenderTransform as TransformGroup;
        if (transform != null)
        {
            if (transform.Children[0] is ScaleTransform scale)
            {
                scale.ScaleX = 1;
                scale.ScaleY = 1;
            }
            if (transform.Children[1] is TranslateTransform translate)
            {
                translate.Y = 0;
            }
        }

        LyricTextBlock.Opacity = 1;

        // 根据当前动画模式选择 Storyboard
        Storyboard? storyboard = null;

        switch (_viewModel.AnimationMode)
        {
            case LyricAnimationMode.Fade:
                LyricTextBlock.Opacity = 0;
                storyboard = TryFindResource("FadeInAnimation") as Storyboard;
                if (storyboard != null)
                {
                    Storyboard.SetTarget(storyboard, LyricTextBlock);
                }
                break;

            case LyricAnimationMode.Scale:
                LyricTextBlock.Opacity = 0;
                if (transform?.Children[0] is ScaleTransform scaleTransform)
                {
                    scaleTransform.ScaleX = 0.85;
                    scaleTransform.ScaleY = 0.85;
                }
                storyboard = TryFindResource("ScaleInAnimation") as Storyboard;
                if (storyboard != null)
                {
                    Storyboard.SetTarget(storyboard, LyricTextBlock);
                }
                break;
        }

        if (storyboard != null)
        {
            storyboard.Begin();
        }
    }

    #endregion

    #region 窗口边缘圆角
    /// <summary>
    /// 当窗口贴合桌面上边时（Top == 0），移除上边两个圆角 + 缩小上边距消除空隙。
    /// 窗口移离后恢复全部圆角和边距。
    /// </summary>
    private void UpdateTopEdgeCorner()
    {
        if (IslandBorder == null) return;

        const double topEdgeThreshold = 8.0;

        if (Top <= topEdgeThreshold)
        {
            // 上边平直，底部保留圆角；上边距归零消除空隙
            IslandBorder.CornerRadius = new CornerRadius(0, 0, 42, 42);
            IslandBorder.Margin = new Thickness(12, 0, 12, 12);
        }
        else
        {
            // 恢复全部圆角和完整边距
            IslandBorder.CornerRadius = new CornerRadius(42);
            IslandBorder.Margin = new Thickness(12);
        }
    }

    #endregion

    #region Win32 消息拦截

    /// <summary>
    /// 拦截 WM_WINDOWPOSCHANGING：
    /// 当系统试图将窗口放到负 Y 位置（Snap Layout 触发前的瞬态位置）时，
    /// 将 Y 钳制到 0，阻止 Snap 覆盖层弹出。
    /// </summary>
    private IntPtr WndProc(IntPtr hwnd, int msg, IntPtr wParam, IntPtr lParam, ref bool handled)
    {
        if (msg == WM_WINDOWPOSCHANGING)
        {
            var pos = Marshal.PtrToStructure<WINDOWPOS>(lParam);

            // Snap Layout 在正式放置前会把窗口放到 Y≈-3000~-1 的位置
            // 检测到这种情况就强制钳制到 Top=0
            if (pos.y < -5)
            {
                pos.y = 0;
                Marshal.StructureToPtr(pos, lParam, false);
                handled = true;
            }
        }

        return IntPtr.Zero;
    }

    [StructLayout(LayoutKind.Sequential)]
    private struct WINDOWPOS
    {
        public IntPtr hwnd;
        public IntPtr hwndInsertAfter;
        public int x;
        public int y;
        public int cx;
        public int cy;
        public int flags;
    }

    #endregion
}

/// <summary>
/// 使用 SmtcManager 管理多会话，MediaSessionModel 处理歌词匹配，
/// ClassicLyricDocument/ILyricDocument 处理歌词时间查询。
/// </summary>
public sealed partial class LyricsIslandViewModel : INotifyPropertyChanged, IDisposable
{
    private readonly DispatcherTimer _syncTimer;
    private readonly OnlineLyricProvider _onlineLyricProvider = new();
    private SmtcManager? _smtcManager;
    private MediaSessionModel? _currentMedia;
    private bool _isAlwaysOnTop = true;
    private string _currentLyric = "等待 SMTC 媒体会话";
    private ImageSource _albumCover;
    private LyricAnimationMode _animationMode = LyricAnimationMode.Fade;

    public LyricsIslandViewModel()
    {
        _albumCover = CreateFallbackCover();
        _syncTimer = new DispatcherTimer
        {
            Interval = TimeSpan.FromMilliseconds(200)
        };
        _syncTimer.Tick += (_, _) => SyncLyricWithTimeline();
    }

    public event PropertyChangedEventHandler? PropertyChanged;

    public string CurrentLyric
    {
        get => _currentLyric;
        private set => SetField(ref _currentLyric, value);
    }

    public ImageSource AlbumCover
    {
        get => _albumCover;
        private set => SetField(ref _albumCover, value);
    }

    public bool IsAlwaysOnTop
    {
        get => _isAlwaysOnTop;
        set => SetField(ref _isAlwaysOnTop, value);
    }

    /// <summary>当前歌词动画模式</summary>
    public LyricAnimationMode AnimationMode
    {
        get => _animationMode;
        set
        {
            if (SetField(ref _animationMode, value))
            {
                AnimationModeChanged?.Invoke(this, value);
            }
        }
    }

    /// <summary>动画模式变更事件（供 View 层触发动画）</summary>
    public event EventHandler<LyricAnimationMode>? AnimationModeChanged;

    /// <summary>
    /// 初始化 SMTC 管理器，开始监听媒体会话。
    /// </summary>
    public async Task InitializeAsync()
    {
        try
        {
            _smtcManager = await SmtcManager.CreateAsync();
            _smtcManager.SessionAdded += OnSessionAdded;
            _smtcManager.SessionRemoved += OnSessionRemoved;

            // 绑定已有会话
            var firstSession = _smtcManager.Sessions.FirstOrDefault();
            if (firstSession != null)
            {
                AttachToSession(firstSession);
            }
            else
            {
                CurrentLyric = "未检测到正在播放";
            }
        }
        catch (Exception ex)
        {
            CurrentLyric = $"SMTC 初始化失败：{ex.Message}";
            DiagnosticLog.Write($"SMTC init error: {ex}");
        }
    }

    private void OnSessionAdded(object? sender, IMediaSession session)
    {
        Application.Current.Dispatcher.Invoke(() => AttachToSession(session));
    }

    private void OnSessionRemoved(object? sender, IMediaSession session)
    {
        Application.Current.Dispatcher.Invoke(() =>
        {
            if (_currentMedia?.Session == session)
            {
                DetachSession();
                TryAttachNextSession();
            }
        });
    }

    private void TryAttachNextSession()
    {
        var next = _smtcManager?.Sessions.FirstOrDefault();
        if (next != null)
        {
            AttachToSession(next);
        }
        else
        {
            AlbumCover = CreateFallbackCover();
            CurrentLyric = "未检测到正在播放";
        }
    }

    private void AttachToSession(IMediaSession session)
    {
        // 先清理旧的
        if (_currentMedia?.Session == session) return;

        DetachSession();

        _currentMedia = new MediaSessionModel(session, _onlineLyricProvider);
        _currentMedia.Session.TimelinePropertiesChanged += OnTimelineChanged;
        _currentMedia.Session.PlaybackInfoChanged += OnPlaybackInfoChanged;

        // 更新封面和歌词
        _ = UpdateMediaDisplayAsync();
        _syncTimer.Start();
    }

    private void DetachSession()
    {
        _syncTimer.Stop();

        if (_currentMedia != null)
        {
            _currentMedia.Session.TimelinePropertiesChanged -= OnTimelineChanged;
            _currentMedia.Session.PlaybackInfoChanged -= OnPlaybackInfoChanged;
            _currentMedia.Dispose();
            _currentMedia = null;
        }
    }

    private void OnTimelineChanged(object? sender, EventArgs e)
    {
        SyncLyricWithTimeline();
    }

    private void OnPlaybackInfoChanged(object? sender, EventArgs e)
    {
        SyncLyricWithTimeline();
    }

    private async Task UpdateMediaDisplayAsync()
    {
        if (_currentMedia == null) return;

        var session = _currentMedia.Session;

        // 封面
        if (session.AlbumCover != null)
        {
            AlbumCover = session.AlbumCover;
        }

        // 首次歌词显示
        var position = session.PlaybackStatus == MediaSessionPlaybackStatus.Playing
            ? session.Position
            : TimeSpan.Zero;
        CurrentLyric = _currentMedia.GetCurrentLyricText(position);

        // 异步加载歌词
        await _currentMedia.RefreshLyricAsync();
        SyncLyricWithTimeline();
    }

    /// <summary>
    /// 定时器触发 — 根据当前播放位置更新歌词显示。
    /// 使用 HotLyric 风格的 ILyricDocument.GetCurrentLine() 查询。
    /// </summary>
    private void SyncLyricWithTimeline()
    {
        var media = _currentMedia;
        if (media == null) return;

        var session = media.Session;
        var position = session.PlaybackStatus == MediaSessionPlaybackStatus.Playing
            ? session.Position
            : TimeSpan.Zero;

        // 会话无标题
        if (string.IsNullOrWhiteSpace(session.Title))
        {
            CurrentLyric = "正在读取媒体信息";
            return;
        }

        // 通过 MediaSessionModel 获取当前歌词文本
        var text = media.GetCurrentLyricText(position);
        CurrentLyric = text;

        // 同步封面
        if (session.AlbumCover != null)
        {
            AlbumCover = session.AlbumCover;
        }
    }

    private static ImageSource CreateFallbackCover()
    {
        const int size = 256;
        var drawing = new DrawingVisual();

        using (var context = drawing.RenderOpen())
        {
            var bounds = new Rect(0, 0, size, size);
            var background = new LinearGradientBrush(
                Color.FromRgb(27, 32, 43),
                Color.FromRgb(123, 230, 200),
                new Point(0, 0),
                new Point(1, 1));

            context.DrawRoundedRectangle(background, null, bounds, 40, 40);
            context.DrawEllipse(new SolidColorBrush(Color.FromArgb(58, 255, 255, 255)), null, new Point(74, 72), 34, 34);
            context.DrawEllipse(new SolidColorBrush(Color.FromArgb(76, 0, 0, 0)), null, new Point(132, 132), 76, 76);
            context.DrawEllipse(new SolidColorBrush(Color.FromArgb(165, 255, 255, 255)), null, new Point(132, 132), 22, 22);
            context.DrawRectangle(new SolidColorBrush(Color.FromArgb(52, 255, 255, 255)), null, new Rect(166, 48, 24, 96));
            context.DrawEllipse(new SolidColorBrush(Color.FromArgb(92, 255, 255, 255)), null, new Point(152, 146), 28, 20);
        }

        var bitmap = new RenderTargetBitmap(size, size, 96, 96, PixelFormats.Pbgra32);
        bitmap.Render(drawing);
        bitmap.Freeze();
        return bitmap;
    }

    private bool SetField<T>(ref T field, T value, [CallerMemberName] string? propertyName = null)
    {
        if (EqualityComparer<T>.Default.Equals(field, value))
        {
            return false;
        }

        field = value;
        OnPropertyChanged(propertyName);
        return true;
    }

    private void OnPropertyChanged([CallerMemberName] string? propertyName = null)
    {
        PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
    }

    public void Dispose()
    {
        _syncTimer.Stop();
        DetachSession();

        if (_smtcManager != null)
        {
            _smtcManager.SessionAdded -= OnSessionAdded;
            _smtcManager.SessionRemoved -= OnSessionRemoved;
            _smtcManager.Dispose();
            _smtcManager = null;
        }

        _onlineLyricProvider.Dispose();
    }
}

public static partial class LocalLyricProvider
{
    private static readonly Regex TimestampRegex = CreateTimestampRegex();

    public static IReadOnlyList<TimedLyricLine> TryLoad(string title, string artist)
    {
        var lyricsDirectory = Path.Combine(AppContext.BaseDirectory, "Lyrics");

        if (!Directory.Exists(lyricsDirectory))
        {
            return Array.Empty<TimedLyricLine>();
        }

        var candidates = BuildCandidateFileNames(title, artist);

        foreach (var candidate in candidates)
        {
            var path = Path.Combine(lyricsDirectory, candidate);

            if (File.Exists(path))
            {
                return ParseLrcLines(File.ReadAllLines(path));
            }
        }

        return Array.Empty<TimedLyricLine>();
    }

    public static IReadOnlyList<TimedLyricLine> ParseLrcText(string lrcText)
    {
        if (string.IsNullOrWhiteSpace(lrcText))
        {
            return Array.Empty<TimedLyricLine>();
        }

        return ParseLrcLines(lrcText.Split(["\r\n", "\n"], StringSplitOptions.None));
    }

    private static IEnumerable<string> BuildCandidateFileNames(string title, string artist)
    {
        var safeTitle = SanitizeFileName(title);
        var safeArtist = SanitizeFileName(artist);

        if (!string.IsNullOrWhiteSpace(safeTitle) && !string.IsNullOrWhiteSpace(safeArtist))
        {
            yield return $"{safeArtist} - {safeTitle}.lrc";
            yield return $"{safeTitle} - {safeArtist}.lrc";
        }

        if (!string.IsNullOrWhiteSpace(safeTitle))
        {
            yield return $"{safeTitle}.lrc";
        }
    }

    private static string SanitizeFileName(string value)
    {
        var invalidChars = Path.GetInvalidFileNameChars();
        return new string(value.Select(character => invalidChars.Contains(character) ? '_' : character).ToArray()).Trim();
    }

    private static IReadOnlyList<TimedLyricLine> ParseLrcLines(IEnumerable<string> lines)
    {
        var timedLines = new List<TimedLyricLine>();

        foreach (var line in lines)
        {
            var matches = TimestampRegex.Matches(line);

            if (matches.Count == 0)
            {
                continue;
            }

            var text = TimestampRegex.Replace(line, string.Empty).Trim();

            if (string.IsNullOrWhiteSpace(text))
            {
                continue;
            }

            foreach (Match match in matches)
            {
                if (TryParseTimestamp(match, out var timestamp))
                {
                    timedLines.Add(new TimedLyricLine(timestamp, timestamp, text));
                }
            }
        }

        return TimedLyricLine.Normalize(timedLines);
    }

    private static bool TryParseTimestamp(Match match, out TimeSpan timestamp)
    {
        timestamp = TimeSpan.Zero;

        if (!int.TryParse(match.Groups["minutes"].Value, NumberStyles.Integer, CultureInfo.InvariantCulture, out var minutes) ||
            !int.TryParse(match.Groups["seconds"].Value, NumberStyles.Integer, CultureInfo.InvariantCulture, out var seconds))
        {
            return false;
        }

        var fractionText = match.Groups["fraction"].Value;
        var milliseconds = 0;

        if (!string.IsNullOrWhiteSpace(fractionText))
        {
            var normalized = fractionText.Length switch
            {
                1 => fractionText + "00",
                2 => fractionText + "0",
                _ => fractionText[..3]
            };

            if (!int.TryParse(normalized, NumberStyles.Integer, CultureInfo.InvariantCulture, out milliseconds))
            {
                return false;
            }
        }

        timestamp = new TimeSpan(0, 0, minutes, seconds, milliseconds);
        return true;
    }

    [GeneratedRegex(@"\[(?<minutes>\d{1,3}):(?<seconds>\d{2})(?:\.(?<fraction>\d{1,3}))?\]")]
    private static partial Regex CreateTimestampRegex();
}

public sealed class SmtcTimelineTracker
{
    private static readonly TimeSpan ResumeGuardDuration = TimeSpan.FromMilliseconds(1200);
    private static readonly TimeSpan SeekDetectionThreshold = TimeSpan.FromSeconds(2);
    private static readonly TimeSpan SuspiciousBackwardsJump = TimeSpan.FromSeconds(5);
    private static readonly TimeSpan PostResumeZeroIgnoreWindow = TimeSpan.FromSeconds(5);
    private readonly Stopwatch _stopwatch = new();
    private TimeSpan _basePosition = TimeSpan.Zero;
    private DateTimeOffset _lastUpdatedTime = DateTimeOffset.MinValue;
    private TimeSpan _resumeGuardPosition = TimeSpan.Zero;
    private long _resumeGuardUntilTicks;
    private long _lastResumeTicks;
    private bool _isPlaying;

    public TimeSpan Duration { get; private set; } = TimeSpan.Zero;

    public void Reset()
    {
        _stopwatch.Reset();
        _basePosition = TimeSpan.Zero;
        _lastUpdatedTime = DateTimeOffset.MinValue;
        _resumeGuardPosition = TimeSpan.Zero;
        _resumeGuardUntilTicks = 0;
        _lastResumeTicks = 0;
        _isPlaying = false;
        Duration = TimeSpan.Zero;
    }

    public void CalibrateFromTimeline(GlobalSystemMediaTransportControlsSession session)
    {
        var timeline = session.GetTimelineProperties();
        var playbackInfo = session.GetPlaybackInfo();
        var playbackStatus = playbackInfo.PlaybackStatus;
        var rawPosition = ClampPosition(timeline.Position);
        var estimated = GetPosition();

        Duration = timeline.EndTime > timeline.StartTime
            ? timeline.EndTime - timeline.StartTime
            : TimeSpan.Zero;

        if (ShouldIgnoreResumeBackwardsJump(rawPosition, estimated))
        {
            _lastUpdatedTime = timeline.LastUpdatedTime;
            _isPlaying = playbackStatus == GlobalSystemMediaTransportControlsSessionPlaybackStatus.Playing;
            if (_isPlaying && !_stopwatch.IsRunning)
            {
                _stopwatch.Start();
            }
            DiagnosticLog.Write($"SMTC ignored resume-back jump: guardedPosition={_resumeGuardPosition}, estimated={estimated}, rawPosition={timeline.Position}, lastUpdated={timeline.LastUpdatedTime}");
            return;
        }

        _basePosition = rawPosition;
        _lastUpdatedTime = timeline.LastUpdatedTime;
        _isPlaying = playbackStatus == GlobalSystemMediaTransportControlsSessionPlaybackStatus.Playing;

        if (_isPlaying)
        {
            _stopwatch.Restart();
        }
        else
        {
            _stopwatch.Reset();
        }

        DiagnosticLog.Write($"SMTC timeline calibrated: position={_basePosition}, rawPosition={timeline.Position}, playing={_isPlaying}, lastUpdated={timeline.LastUpdatedTime}");
    }

    public void ForceSeekFromTimeline(GlobalSystemMediaTransportControlsSession session)
    {
        var timeline = session.GetTimelineProperties();
        var playbackInfo = session.GetPlaybackInfo();
        var rawPosition = ClampPosition(timeline.Position);

        Duration = timeline.EndTime > timeline.StartTime
            ? timeline.EndTime - timeline.StartTime
            : Duration;
        _basePosition = rawPosition;
        _lastUpdatedTime = timeline.LastUpdatedTime;
        _resumeGuardUntilTicks = 0;
        _resumeGuardPosition = TimeSpan.Zero;
        _isPlaying = playbackInfo.PlaybackStatus == GlobalSystemMediaTransportControlsSessionPlaybackStatus.Playing;

        if (_isPlaying)
        {
            _stopwatch.Restart();
        }
        else
        {
            _stopwatch.Reset();
        }

        DiagnosticLog.Write($"SMTC forced seek: position={_basePosition}, rawPosition={timeline.Position}, playing={_isPlaying}, lastUpdated={timeline.LastUpdatedTime}");
    }

    public void ApplyTimelineChange(GlobalSystemMediaTransportControlsSession session)
    {
        var timeline = session.GetTimelineProperties();
        var rawPosition = ClampPosition(timeline.Position);
        var estimated = GetPosition();

        if (ShouldIgnoreResumeBackwardsJump(rawPosition, estimated))
        {
            DiagnosticLog.Write($"SMTC ignored timeline resume-back jump: guardedPosition={_resumeGuardPosition}, estimated={estimated}, rawPosition={timeline.Position}, lastUpdated={timeline.LastUpdatedTime}");
            return;
        }

        Duration = timeline.EndTime > timeline.StartTime
            ? timeline.EndTime - timeline.StartTime
            : Duration;

        // Ignore transient position=0 events shortly after resume.
        // Some players briefly report position=0 on resume, even with valid duration.
        if (rawPosition <= TimeSpan.FromSeconds(1) &&
            estimated > SeekDetectionThreshold &&
            _lastResumeTicks > 0 &&
            Stopwatch.GetTimestamp() - _lastResumeTicks < (long)(PostResumeZeroIgnoreWindow.TotalSeconds * Stopwatch.Frequency))
        {
            _lastUpdatedTime = timeline.LastUpdatedTime;
            DiagnosticLog.Write($"SMTC ignored transient zero after resume: estimated={estimated}, rawPosition={timeline.Position}, lastUpdated={timeline.LastUpdatedTime}");
            return;
        }

        // Small position drift during normal playback — adjust base without a full seek.
        if (Abs(rawPosition - estimated) <= SeekDetectionThreshold)
        {
            _basePosition = rawPosition;
            _lastUpdatedTime = timeline.LastUpdatedTime;

            DiagnosticLog.Write($"SMTC timeline adjusted: position={_basePosition}, rawPosition={timeline.Position}, estimated={estimated}, playing={_isPlaying}, lastUpdated={timeline.LastUpdatedTime}");
            return;
        }

        ForceSeekFromTimeline(session);
    }

    public void ApplyPlaybackState(GlobalSystemMediaTransportControlsSession session)
    {
        var timeline = session.GetTimelineProperties();
        var playbackInfo = session.GetPlaybackInfo();
        var sessionPlaying = playbackInfo.PlaybackStatus == GlobalSystemMediaTransportControlsSessionPlaybackStatus.Playing;

        Duration = timeline.EndTime > timeline.StartTime
            ? timeline.EndTime - timeline.StartTime
            : Duration;

        var rawPosition = ClampPosition(timeline.Position);
        var estimated = GetPosition();

        // When resuming from pause, some players briefly report position=0.
        // Ignore this transient zero and resume from the estimated position.
        if (sessionPlaying && !_isPlaying &&
            rawPosition <= TimeSpan.FromSeconds(1) &&
            estimated > SeekDetectionThreshold)
        {
            _isPlaying = true;
            _resumeGuardPosition = _basePosition;
            _resumeGuardUntilTicks = Stopwatch.GetTimestamp() + (long)(ResumeGuardDuration.TotalSeconds * Stopwatch.Frequency);
            _lastResumeTicks = Stopwatch.GetTimestamp();
            _lastUpdatedTime = timeline.LastUpdatedTime;
            _stopwatch.Restart();
            DiagnosticLog.Write($"SMTC resumed (ignored transient zero): basePosition={_basePosition}, rawPosition={timeline.Position}, lastUpdated={timeline.LastUpdatedTime}");
            return;
        }

        if (Abs(rawPosition - estimated) > SeekDetectionThreshold && !ShouldIgnoreResumeBackwardsJump(rawPosition, estimated))
        {
            ForceSeekFromTimeline(session);
            return;
        }

        if (_isPlaying == sessionPlaying)
        {
            return;
        }

        if (!sessionPlaying)
        {
            _basePosition = GetPosition();
            _isPlaying = false;
            _stopwatch.Reset();
            DiagnosticLog.Write($"SMTC paused: frozenPosition={_basePosition}, rawPosition={timeline.Position}, lastUpdated={timeline.LastUpdatedTime}");
            return;
        }

        _isPlaying = true;
        _resumeGuardPosition = _basePosition;
        _resumeGuardUntilTicks = Stopwatch.GetTimestamp() + (long)(ResumeGuardDuration.TotalSeconds * Stopwatch.Frequency);
        _lastResumeTicks = Stopwatch.GetTimestamp();
        _lastUpdatedTime = timeline.LastUpdatedTime;
        _stopwatch.Restart();
        DiagnosticLog.Write($"SMTC resumed: basePosition={_basePosition}, rawPosition={timeline.Position}, lastUpdated={timeline.LastUpdatedTime}");
    }

    public void RefreshFromSessionIfNeeded(GlobalSystemMediaTransportControlsSession session)
    {
        var timeline = session.GetTimelineProperties();
        var playbackInfo = session.GetPlaybackInfo();
        var sessionPlaying = playbackInfo.PlaybackStatus == GlobalSystemMediaTransportControlsSessionPlaybackStatus.Playing;

        if (sessionPlaying != _isPlaying)
        {
            ApplyPlaybackState(session);
            return;
        }

        if (timeline.LastUpdatedTime != _lastUpdatedTime)
        {
            ApplyTimelineChange(session);
            return;
        }

        if (!_isPlaying)
        {
            return;
        }
    }

    public TimeSpan GetPosition()
    {
        var position = _isPlaying
            ? _basePosition + _stopwatch.Elapsed
            : _basePosition;

        return ClampPosition(position);
    }

    private TimeSpan ClampPosition(TimeSpan position)
    {
        if (position < TimeSpan.Zero)
        {
            return TimeSpan.Zero;
        }

        return Duration > TimeSpan.Zero && position > Duration ? Duration : position;
    }

    private bool ShouldIgnoreResumeBackwardsJump(TimeSpan rawPosition, TimeSpan estimatedPosition)
    {
        if (_resumeGuardUntilTicks == 0 || Stopwatch.GetTimestamp() > _resumeGuardUntilTicks)
        {
            return false;
        }

        if (_resumeGuardPosition <= SuspiciousBackwardsJump)
        {
            return false;
        }

        var returnsNearBeginning = rawPosition <= TimeSpan.FromSeconds(3);
        var estimatedStillNearResumePoint = Abs(estimatedPosition - _resumeGuardPosition) < ResumeGuardDuration + TimeSpan.FromSeconds(1);
        return returnsNearBeginning && estimatedStillNearResumePoint;
    }

    private static TimeSpan Abs(TimeSpan value)
    {
        return value < TimeSpan.Zero ? -value : value;
    }
}

public static class DiagnosticLog
{
    private static readonly object LockObject = new();
    private static readonly string LogPath = Path.Combine(AppContext.BaseDirectory, "smtc-sync.log");

    public static void Write(string message)
    {
        try
        {
            lock (LockObject)
            {
                File.AppendAllText(
                    LogPath,
                    $"{DateTimeOffset.Now:O} {message}{Environment.NewLine}",
                    Encoding.UTF8);
            }
        }
        catch
        {
        }
    }
}

public sealed class OnlineLyricProvider : IDisposable
{
    private readonly HttpClient _lrclibHttpClient = new()
    {
        BaseAddress = new Uri("https://lrclib.net")
    };
    private readonly AmllTtmlLyricProvider _amllTtmlLyricProvider = new();
    private readonly ChineseLyricProvider _chineseLyricProvider = new();

    public OnlineLyricProvider()
    {
        _lrclibHttpClient.DefaultRequestHeaders.UserAgent.Add(
            new ProductInfoHeaderValue("DynamicIslandLyrics", "1.0"));
    }

    public async Task<IReadOnlyList<TimedLyricLine>> FindSyncedLyricsAsync(
        string title,
        string artist,
        TimeSpan duration,
        CancellationToken cancellationToken)
    {
        if (string.IsNullOrWhiteSpace(title))
        {
            return Array.Empty<TimedLyricLine>();
        }

        var amllLyrics = await _amllTtmlLyricProvider.FindSyncedLyricsAsync(title, artist, duration, cancellationToken);

        if (amllLyrics.Count > 0)
        {
            DiagnosticLog.Write($"Lyrics source: AMLL TTML, lines={amllLyrics.Count}");
            return amllLyrics;
        }

        var chineseLyrics = await _chineseLyricProvider.FindSyncedLyricsAsync(title, artist, duration, cancellationToken);

        if (chineseLyrics.Count > 0)
        {
            DiagnosticLog.Write($"Lyrics source: Chinese providers, lines={chineseLyrics.Count}");
            return chineseLyrics;
        }

        var exactLyrics = await TryGetExactLyricsAsync(title, artist, duration, cancellationToken);

        if (exactLyrics.Count > 0)
        {
            DiagnosticLog.Write($"Lyrics source: LRCLIB exact, lines={exactLyrics.Count}");
            return exactLyrics;
        }

        var url = BuildSearchUrl(title, artist);
        using var response = await _lrclibHttpClient.GetAsync(url, cancellationToken);

        if (response.StatusCode == HttpStatusCode.NotFound || !response.IsSuccessStatusCode)
        {
            return Array.Empty<TimedLyricLine>();
        }

        await using var stream = await response.Content.ReadAsStreamAsync(cancellationToken);
        var results = await JsonSerializer.DeserializeAsync<List<LrclibSearchResult>>(
            stream,
            LrclibJsonContext.Default.ListLrclibSearchResult,
            cancellationToken);

        if (results is null || results.Count == 0)
        {
            return Array.Empty<TimedLyricLine>();
        }

        foreach (var result in RankResults(results, title, artist, duration))
        {
            if (string.IsNullOrWhiteSpace(result.SyncedLyrics))
            {
                continue;
            }

            var lyrics = LocalLyricProvider.ParseLrcText(result.SyncedLyrics);

            if (lyrics.Count > 0)
            {
                DiagnosticLog.Write($"Lyrics source: LRCLIB search, providerId={result.Id}, track={result.TrackName}, artist={result.ArtistName}, duration={result.Duration}, lines={lyrics.Count}");
                return lyrics;
            }
        }

        return Array.Empty<TimedLyricLine>();
    }

    private async Task<IReadOnlyList<TimedLyricLine>> TryGetExactLyricsAsync(
        string title,
        string artist,
        TimeSpan duration,
        CancellationToken cancellationToken)
    {
        var url = BuildExactUrl(title, artist, duration);
        using var response = await _lrclibHttpClient.GetAsync(url, cancellationToken);

        if (response.StatusCode == HttpStatusCode.NotFound || !response.IsSuccessStatusCode)
        {
            return Array.Empty<TimedLyricLine>();
        }

        await using var stream = await response.Content.ReadAsStreamAsync(cancellationToken);
        var result = await JsonSerializer.DeserializeAsync(
            stream,
            LrclibJsonContext.Default.LrclibSearchResult,
            cancellationToken);

        if (string.IsNullOrWhiteSpace(result?.SyncedLyrics))
        {
            return Array.Empty<TimedLyricLine>();
        }

        return LocalLyricProvider.ParseLrcText(result.SyncedLyrics);
    }

    public void Dispose()
    {
        _lrclibHttpClient.Dispose();
        _amllTtmlLyricProvider.Dispose();
        _chineseLyricProvider.Dispose();
    }

    private static string BuildSearchUrl(string title, string artist)
    {
        var query = string.IsNullOrWhiteSpace(artist)
            ? title
            : $"{artist} {title}";

        return $"/api/search?q={Uri.EscapeDataString(query)}";
    }

    private static string BuildExactUrl(string title, string artist, TimeSpan duration)
    {
        var parts = new List<string>
        {
            $"track_name={Uri.EscapeDataString(title)}"
        };

        if (!string.IsNullOrWhiteSpace(artist))
        {
            parts.Add($"artist_name={Uri.EscapeDataString(artist)}");
        }

        if (duration > TimeSpan.Zero)
        {
            parts.Add($"duration={(int)Math.Round(duration.TotalSeconds)}");
        }

        return $"/api/get?{string.Join("&", parts)}";
    }

    private static IEnumerable<LrclibSearchResult> RankResults(
        IEnumerable<LrclibSearchResult> results,
        string title,
        string artist,
        TimeSpan duration)
    {
        var normalizedTitle = NormalizeForCompare(title);
        var normalizedArtist = NormalizeForCompare(artist);

        return results
            .Where(result => !string.IsNullOrWhiteSpace(result.SyncedLyrics))
            .OrderByDescending(result => NormalizeForCompare(result.TrackName) == normalizedTitle)
            .ThenByDescending(result => NormalizeForCompare(result.ArtistName).Contains(normalizedArtist, StringComparison.Ordinal))
            .ThenBy(result => GetDurationDistance(result.Duration, duration))
            .ThenBy(result => result.Id);
    }

    private static double GetDurationDistance(double? candidateDuration, TimeSpan duration)
    {
        return candidateDuration is null || duration <= TimeSpan.Zero
            ? double.MaxValue
            : Math.Abs(candidateDuration.Value - duration.TotalSeconds);
    }

    private static string NormalizeForCompare(string? value)
    {
        return string.IsNullOrWhiteSpace(value)
            ? string.Empty
            : value.Trim().ToUpperInvariant();
    }
}

public sealed record LrclibSearchResult(
    [property: JsonPropertyName("id")] int Id,
    [property: JsonPropertyName("trackName")] string? TrackName,
    [property: JsonPropertyName("artistName")] string? ArtistName,
    [property: JsonPropertyName("duration")] double? Duration,
    [property: JsonPropertyName("syncedLyrics")] string? SyncedLyrics);

public sealed class AmllTtmlLyricProvider : IDisposable
{
    private readonly HttpClient _httpClient = new()
    {
        BaseAddress = new Uri("https://amlldb.bikonoo.com")
    };

    public AmllTtmlLyricProvider()
    {
        _httpClient.DefaultRequestHeaders.UserAgent.ParseAdd(BaseMusicClient.UserAgent);
        _httpClient.DefaultRequestHeaders.Referrer = new Uri("https://amlldb.bikonoo.com/search.html");
    }

    public async Task<IReadOnlyList<TimedLyricLine>> FindSyncedLyricsAsync(
        string title,
        string artist,
        TimeSpan duration,
        CancellationToken cancellationToken)
    {
        var results = await SearchAsync(title, artist, cancellationToken);

        if (results.Count == 0)
        {
            return Array.Empty<TimedLyricLine>();
        }

        foreach (var result in RankResults(results, title, artist))
        {
            if (string.IsNullOrWhiteSpace(result.File))
            {
                continue;
            }

            var ttml = await DownloadTtmlAsync(result.File, cancellationToken);
            var lyrics = TtmlLyricParser.Parse(ttml);

            if (lyrics.Count == 0)
            {
                continue;
            }

            if (duration > TimeSpan.Zero && TtmlLyricParser.GetDuration(lyrics) is { } lyricDuration)
            {
                var distance = Math.Abs((lyricDuration - duration).TotalSeconds);

                if (distance > 18)
                {
                    DiagnosticLog.Write($"AMLL TTML skipped by duration: file={result.File}, lyricDuration={lyricDuration}, smtcDuration={duration}");
                    continue;
                }
            }

            DiagnosticLog.Write($"Lyrics source: AMLL TTML file={result.File}, title={result.Title}, artist={result.Artist}, lines={lyrics.Count}");
            return lyrics;
        }

        return Array.Empty<TimedLyricLine>();
    }

    public void Dispose()
    {
        _httpClient.Dispose();
    }

    private async Task<IReadOnlyList<AmllSearchResult>> SearchAsync(
        string title,
        string artist,
        CancellationToken cancellationToken)
    {
        var query = string.IsNullOrWhiteSpace(artist) ? title : $"{artist} {title}";
        var payload = JsonSerializer.Serialize(new
        {
            query,
            type = "all"
        });

        using var request = new HttpRequestMessage(HttpMethod.Post, "/api/search-lyrics")
        {
            Content = new StringContent(payload, Encoding.UTF8, "application/json")
        };
        using var response = await _httpClient.SendAsync(request, cancellationToken);

        if (!response.IsSuccessStatusCode)
        {
            return Array.Empty<AmllSearchResult>();
        }

        await using var stream = await response.Content.ReadAsStreamAsync(cancellationToken);
        var results = await JsonSerializer.DeserializeAsync(
            stream,
            LrclibJsonContext.Default.ListAmllSearchResult,
            cancellationToken);

        return results is null ? Array.Empty<AmllSearchResult>() : results;
    }

    private async Task<string> DownloadTtmlAsync(string file, CancellationToken cancellationToken)
    {
        using var response = await _httpClient.GetAsync($"/raw-lyrics/{Uri.EscapeDataString(file)}", cancellationToken);

        if (!response.IsSuccessStatusCode)
        {
            return string.Empty;
        }

        var bytes = await response.Content.ReadAsByteArrayAsync(cancellationToken);
        return Encoding.UTF8.GetString(bytes);
    }

    private static IEnumerable<AmllSearchResult> RankResults(
        IEnumerable<AmllSearchResult> results,
        string title,
        string artist)
    {
        return results
            .Where(result => !string.IsNullOrWhiteSpace(result.File))
            .Select(result => new
            {
                Result = result,
                Score = GetMetadataScore(result, title, artist)
            })
            .Where(item => item.Score < double.MaxValue)
            .OrderBy(item => item.Score)
            .ThenByDescending(item => item.Result.Score ?? 0)
            .Select(item => item.Result);
    }

    private static double GetMetadataScore(AmllSearchResult result, string title, string artist)
    {
        var titles = result.Titles is { Length: > 0 } ? result.Titles : [result.Title ?? string.Empty];
        var artists = result.Artists is { Length: > 0 } ? result.Artists : [result.Artist ?? string.Empty];
        var best = double.MaxValue;

        foreach (var candidateTitle in titles)
        {
            foreach (var candidateArtist in artists)
            {
                var score = BaseMusicClient.GetPublicMatchScore(title, artist, candidateTitle, candidateArtist);
                best = Math.Min(best, score);
            }
        }

        return best;
    }
}

public static class TtmlLyricParser
{
    public static IReadOnlyList<TimedLyricLine> Parse(string ttml)
    {
        if (string.IsNullOrWhiteSpace(ttml))
        {
            return Array.Empty<TimedLyricLine>();
        }

        try
        {
            var document = XDocument.Parse(ttml, LoadOptions.PreserveWhitespace);
            var lines = new List<TimedLyricLine>();

            foreach (var paragraph in document.Descendants().Where(element => element.Name.LocalName == "p"))
            {
                if (!TryReadTime(paragraph.Attribute("begin")?.Value, out var begin))
                {
                    continue;
                }

                if (!TryReadTime(paragraph.Attribute("end")?.Value, out var end))
                {
                    end = begin;
                }

                var text = ExtractMainText(paragraph);

                if (!string.IsNullOrWhiteSpace(text))
                {
                    lines.Add(new TimedLyricLine(begin, end, text));
                }
            }

            return TimedLyricLine.Normalize(lines);
        }
        catch
        {
            return Array.Empty<TimedLyricLine>();
        }
    }

    public static TimeSpan? GetDuration(IReadOnlyList<TimedLyricLine> lyrics)
    {
        return lyrics.Count == 0 ? null : lyrics[^1].EndTime;
    }

    private static string ExtractMainText(XElement paragraph)
    {
        var builder = new StringBuilder();

        foreach (var node in paragraph.Nodes())
        {
            AppendNodeText(node, builder);
        }

        var text = NormalizeWhitespace(builder.ToString());

        if (!string.IsNullOrWhiteSpace(text))
        {
            return text;
        }

        return NormalizeWhitespace(paragraph.Value);
    }

    private static void AppendNodeText(XNode node, StringBuilder builder)
    {
        switch (node)
        {
            case XText text:
                builder.Append(text.Value);
                break;
            case XElement element:
                if (IsTranslationElement(element))
                {
                    return;
                }

                foreach (var child in element.Nodes())
                {
                    AppendNodeText(child, builder);
                }
                break;
        }
    }

    private static bool IsTranslationElement(XElement element)
    {
        return element.Attributes().Any(attribute =>
            attribute.Name.LocalName == "role" &&
            string.Equals(attribute.Value, "x-translation", StringComparison.OrdinalIgnoreCase));
    }

    private static string NormalizeWhitespace(string value)
    {
        return Regex.Replace(WebUtility.HtmlDecode(value), @"\s+", " ").Trim();
    }

    private static bool TryReadTime(string? value, out TimeSpan time)
    {
        time = TimeSpan.Zero;

        if (string.IsNullOrWhiteSpace(value))
        {
            return false;
        }

        value = value.Trim();

        if (value.EndsWith("ms", StringComparison.OrdinalIgnoreCase) &&
            double.TryParse(value[..^2], NumberStyles.Float, CultureInfo.InvariantCulture, out var milliseconds))
        {
            time = TimeSpan.FromMilliseconds(milliseconds);
            return true;
        }

        if (value.EndsWith("s", StringComparison.OrdinalIgnoreCase) &&
            double.TryParse(value[..^1], NumberStyles.Float, CultureInfo.InvariantCulture, out var secondsValue))
        {
            time = TimeSpan.FromSeconds(secondsValue);
            return true;
        }

        var parts = value.Split(':');

        if (parts.Length == 2 &&
            int.TryParse(parts[0], NumberStyles.Integer, CultureInfo.InvariantCulture, out var minutes) &&
            double.TryParse(parts[1], NumberStyles.Float, CultureInfo.InvariantCulture, out var seconds))
        {
            time = TimeSpan.FromMinutes(minutes) + TimeSpan.FromSeconds(seconds);
            return true;
        }

        if (parts.Length == 3 &&
            int.TryParse(parts[0], NumberStyles.Integer, CultureInfo.InvariantCulture, out var hours) &&
            int.TryParse(parts[1], NumberStyles.Integer, CultureInfo.InvariantCulture, out minutes) &&
            double.TryParse(parts[2], NumberStyles.Float, CultureInfo.InvariantCulture, out seconds))
        {
            time = TimeSpan.FromHours(hours) + TimeSpan.FromMinutes(minutes) + TimeSpan.FromSeconds(seconds);
            return true;
        }

        return false;
    }
}

public sealed record AmllSearchResult(
    [property: JsonPropertyName("title")] string? Title,
    [property: JsonPropertyName("titles")] string[]? Titles,
    [property: JsonPropertyName("artist")] string? Artist,
    [property: JsonPropertyName("artists")] string[]? Artists,
    [property: JsonPropertyName("file")] string? File,
    [property: JsonPropertyName("score")] int? Score);

public sealed class ChineseLyricProvider : IDisposable
{
    private readonly HttpClient _httpClient = new();
    private readonly NetEaseLyricClient _netEaseClient;
    private readonly QQMusicLyricClient _qqMusicClient;

    public ChineseLyricProvider()
    {
        _httpClient.DefaultRequestHeaders.UserAgent.ParseAdd(BaseMusicClient.UserAgent);
        _netEaseClient = new NetEaseLyricClient(_httpClient);
        _qqMusicClient = new QQMusicLyricClient(_httpClient);
    }

    public async Task<IReadOnlyList<TimedLyricLine>> FindSyncedLyricsAsync(
        string title,
        string artist,
        TimeSpan duration,
        CancellationToken cancellationToken)
    {
        var qqTask = _qqMusicClient.FindLyricsAsync(title, artist, duration, cancellationToken);
        var netEaseTask = _netEaseClient.FindLyricsAsync(title, artist, duration, cancellationToken);
        var tasks = new List<Task<IReadOnlyList<TimedLyricLine>>> { qqTask, netEaseTask };

        while (tasks.Count > 0)
        {
            var completed = await Task.WhenAny(tasks);
            tasks.Remove(completed);

            try
            {
                var lyrics = await completed;

                if (lyrics.Count > 0)
                {
                    return lyrics;
                }
            }
            catch (OperationCanceledException)
            {
                throw;
            }
            catch
            {
            }
        }

        return Array.Empty<TimedLyricLine>();
    }

    public void Dispose()
    {
        _httpClient.Dispose();
    }
}

public abstract class BaseMusicClient
{
    public const string UserAgent =
        "Mozilla/5.0 (Windows NT 10.0; Win64; x64) AppleWebKit/537.36 (KHTML, like Gecko) Chrome/124.0 Safari/537.36";

    protected BaseMusicClient(HttpClient httpClient)
    {
        HttpClient = httpClient;
    }

    protected HttpClient HttpClient { get; }

    protected static string BuildQueryString(IEnumerable<KeyValuePair<string, string>> values)
    {
        return string.Join("&", values.Select(pair =>
            $"{Uri.EscapeDataString(pair.Key)}={Uri.EscapeDataString(pair.Value)}"));
    }

    protected static string NormalizeForCompare(string? value)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            return string.Empty;
        }

        var builder = new StringBuilder(value.Length);

        foreach (var character in value.Trim().ToUpperInvariant())
        {
            if (char.IsLetterOrDigit(character) || IsCjk(character))
            {
                builder.Append(character);
            }
        }

        return builder.ToString();
    }

    protected static bool IsLikelyMatch(string title, string artist, string? candidateTitle, string? candidateArtist)
    {
        return GetMatchScore(title, artist, TimeSpan.Zero, candidateTitle, candidateArtist, null) < double.MaxValue;
    }

    public static double GetPublicMatchScore(string title, string artist, string? candidateTitle, string? candidateArtist)
    {
        return GetMatchScore(title, artist, TimeSpan.Zero, candidateTitle, candidateArtist, null);
    }

    protected static double GetMatchScore(
        string title,
        string artist,
        TimeSpan duration,
        string? candidateTitle,
        string? candidateArtist,
        TimeSpan? candidateDuration)
    {
        var normalizedTitle = NormalizeForCompare(title);
        var normalizedArtist = NormalizeForCompare(artist);
        var normalizedCandidateTitle = NormalizeForCompare(candidateTitle);
        var normalizedCandidateArtist = NormalizeForCompare(candidateArtist);

        if (string.IsNullOrWhiteSpace(normalizedCandidateTitle))
        {
            return double.MaxValue;
        }

        var titleMatches = normalizedCandidateTitle.Contains(normalizedTitle, StringComparison.Ordinal) ||
                           normalizedTitle.Contains(normalizedCandidateTitle, StringComparison.Ordinal);

        if (!titleMatches)
        {
            return double.MaxValue;
        }

        var artistMatches = string.IsNullOrWhiteSpace(normalizedArtist) ||
                            normalizedCandidateArtist.Contains(normalizedArtist, StringComparison.Ordinal) ||
                            normalizedArtist.Contains(normalizedCandidateArtist, StringComparison.Ordinal);

        if (!artistMatches)
        {
            return double.MaxValue;
        }

        double score = normalizedCandidateTitle == normalizedTitle ? 0 : 25;

        if (!string.IsNullOrWhiteSpace(normalizedArtist) && normalizedCandidateArtist == normalizedArtist)
        {
            score -= 10;
        }

        if (duration > TimeSpan.Zero && candidateDuration is { } actualDuration && actualDuration > TimeSpan.Zero)
        {
            var durationDistance = Math.Abs((actualDuration - duration).TotalSeconds);

            if (durationDistance > 18)
            {
                return double.MaxValue;
            }

            score += durationDistance;
        }

        return score;
    }

    private static bool IsCjk(char character)
    {
        return character is >= '\u3400' and <= '\u9FFF';
    }
}

public sealed class QQMusicLyricClient : BaseMusicClient
{
    private static readonly DateTime EpochChina = new(1970, 1, 1, 8, 0, 0, DateTimeKind.Local);

    public QQMusicLyricClient(HttpClient httpClient) : base(httpClient)
    {
    }

    public async Task<IReadOnlyList<TimedLyricLine>> FindLyricsAsync(
        string title,
        string artist,
        TimeSpan duration,
        CancellationToken cancellationToken)
    {
        var songMid = await SearchSongMidAsync(title, artist, duration, cancellationToken);

        if (string.IsNullOrWhiteSpace(songMid))
        {
            return Array.Empty<TimedLyricLine>();
        }

        var lrc = await GetLyricTextAsync(songMid, cancellationToken);
        var lyrics = LocalLyricProvider.ParseLrcText(lrc);

        if (lyrics.Count > 0)
        {
            DiagnosticLog.Write($"Lyrics source: QQ Music, songMid={songMid}, lines={lyrics.Count}");
        }

        return lyrics;
    }

    private async Task<string?> SearchSongMidAsync(string title, string artist, TimeSpan duration, CancellationToken cancellationToken)
    {
        var payload = new
        {
            req_1 = new
            {
                method = "DoSearchForQQMusicDesktop",
                module = "music.search.SearchCgiService",
                param = new
                {
                    num_per_page = 10,
                    page_num = 1,
                    query = string.IsNullOrWhiteSpace(artist) ? title : $"{artist} {title}",
                    search_type = 0
                }
            }
        };

        using var request = new HttpRequestMessage(HttpMethod.Post, "https://u.y.qq.com/cgi-bin/musicu.fcg");
        request.Headers.Referrer = new Uri("https://c.y.qq.com/");
        request.Content = JsonContent(payload);

        using var response = await HttpClient.SendAsync(request, cancellationToken);

        if (!response.IsSuccessStatusCode)
        {
            return null;
        }

        await using var stream = await response.Content.ReadAsStreamAsync(cancellationToken);
        using var document = await JsonDocument.ParseAsync(stream, cancellationToken: cancellationToken);
        var list = document.RootElement
            .GetPropertyOrNull("req_1")
            ?.GetPropertyOrNull("data")
            ?.GetPropertyOrNull("body")
            ?.GetPropertyOrNull("song")
            ?.GetPropertyOrNull("list");

        if (list is null || list.Value.ValueKind != JsonValueKind.Array)
        {
            return null;
        }

        return list.Value
            .EnumerateArray()
            .Select(item => new
            {
                Mid = item.GetStringOrNull("mid") ?? item.GetStringOrNull("songmid"),
                Score = GetMatchScore(
                    title,
                    artist,
                    duration,
                    item.GetStringOrNull("title") ?? item.GetStringOrNull("name"),
                    ReadQQSingerText(item),
                    item.GetInt64OrNull("interval") is { } seconds ? TimeSpan.FromSeconds(seconds) : null)
            })
            .Where(item => !string.IsNullOrWhiteSpace(item.Mid) && item.Score < double.MaxValue)
            .OrderBy(item => item.Score)
            .Select(item => item.Mid)
            .FirstOrDefault();
    }

    private async Task<string> GetLyricTextAsync(string songMid, CancellationToken cancellationToken)
    {
        var currentMillis = (DateTime.Now.ToLocalTime().Ticks - EpochChina.Ticks) / 10000;
        const string callback = "MusicJsonCallback_lrc";
        var form = new Dictionary<string, string>
        {
            ["callback"] = callback,
            ["pcachetime"] = currentMillis.ToString(CultureInfo.InvariantCulture),
            ["songmid"] = songMid,
            ["g_tk"] = "5381",
            ["jsonpCallback"] = callback,
            ["loginUin"] = "0",
            ["hostUin"] = "0",
            ["format"] = "jsonp",
            ["inCharset"] = "utf8",
            ["outCharset"] = "utf8",
            ["notice"] = "0",
            ["platform"] = "yqq",
            ["needNewCode"] = "0"
        };

        using var request = new HttpRequestMessage(
            HttpMethod.Post,
            "https://c.y.qq.com/lyric/fcgi-bin/fcg_query_lyric_new.fcg");
        request.Headers.Referrer = new Uri("https://c.y.qq.com/");
        request.Content = new FormUrlEncodedContent(form);

        using var response = await HttpClient.SendAsync(request, cancellationToken);

        if (!response.IsSuccessStatusCode)
        {
            return string.Empty;
        }

        var jsonp = await response.Content.ReadAsStringAsync(cancellationToken);
        var json = UnwrapJsonp(callback, jsonp);

        if (string.IsNullOrWhiteSpace(json))
        {
            return string.Empty;
        }

        using var document = JsonDocument.Parse(json);
        var lyricBase64 = document.RootElement.GetStringOrNull("lyric");

        if (string.IsNullOrWhiteSpace(lyricBase64))
        {
            return string.Empty;
        }

        try
        {
            return Encoding.UTF8.GetString(Convert.FromBase64String(lyricBase64));
        }
        catch (FormatException)
        {
            return lyricBase64;
        }
    }

    private static string ReadQQSingerText(JsonElement item)
    {
        var singers = item.GetPropertyOrNull("singer");

        if (singers is null || singers.Value.ValueKind != JsonValueKind.Array)
        {
            return string.Empty;
        }

        return string.Join(" ", singers.Value.EnumerateArray().Select(singer => singer.GetStringOrNull("name")));
    }

    private static string UnwrapJsonp(string callback, string jsonp)
    {
        if (!jsonp.StartsWith(callback, StringComparison.Ordinal))
        {
            return string.Empty;
        }

        var start = jsonp.IndexOf('(');
        var end = jsonp.LastIndexOf(')');

        return start >= 0 && end > start
            ? jsonp[(start + 1)..end]
            : string.Empty;
    }

    private static StringContent JsonContent<T>(T payload)
    {
        return new StringContent(JsonSerializer.Serialize(payload), Encoding.UTF8, "application/json");
    }
}

public sealed class NetEaseLyricClient : BaseMusicClient
{
    private const string Modulus =
        "00e0b509f6259df8642dbc35662901477df22677ec152b5ff68ace615bb7b725152b3ab17a876aea8a5aa76d2e417629ec4ee341f56135fccf695280104e0312ecbda92557c93870114af6c9d05c4f7f0c3685b7a46bee255932575cce10b424d813cfe4875d3e82047b97ddef52741d546b8e289dc6935b3ece0462db0a22b8e7";
    private const string Nonce = "0CoJUm6Qyw8W8jud";
    private const string Pubkey = "010001";
    private const string Vi = "0102030405060708";
    private readonly string _encSecKey;
    private readonly string _secretKey;

    public NetEaseLyricClient(HttpClient httpClient) : base(httpClient)
    {
        _secretKey = CreateSecretKey(16);
        _encSecKey = RsaEncode(_secretKey);
    }

    public async Task<IReadOnlyList<TimedLyricLine>> FindLyricsAsync(
        string title,
        string artist,
        TimeSpan duration,
        CancellationToken cancellationToken)
    {
        var songId = await SearchSongIdAsync(title, artist, duration, cancellationToken);

        if (string.IsNullOrWhiteSpace(songId))
        {
            return Array.Empty<TimedLyricLine>();
        }

        var lrc = await GetLyricTextAsync(songId, cancellationToken);
        var lyrics = LocalLyricProvider.ParseLrcText(lrc);

        if (lyrics.Count > 0)
        {
            DiagnosticLog.Write($"Lyrics source: NetEase Music, songId={songId}, lines={lyrics.Count}");
        }

        return lyrics;
    }

    private async Task<string?> SearchSongIdAsync(string title, string artist, TimeSpan duration, CancellationToken cancellationToken)
    {
        var payload = new Dictionary<string, string>
        {
            ["csrf_token"] = string.Empty,
            ["s"] = string.IsNullOrWhiteSpace(artist) ? title : $"{artist} {title}",
            ["type"] = "1",
            ["limit"] = "10",
            ["offset"] = "0"
        };

        using var document = await SendWeApiAsync(
            "https://music.163.com/weapi/cloudsearch/get/web",
            payload,
            cancellationToken);
        var songs = document.RootElement
            .GetPropertyOrNull("result")
            ?.GetPropertyOrNull("songs");

        if (songs is null || songs.Value.ValueKind != JsonValueKind.Array)
        {
            return null;
        }

        return songs.Value
            .EnumerateArray()
            .Select(item => new
            {
                Id = item.GetInt64OrNull("id")?.ToString(CultureInfo.InvariantCulture),
                Score = GetMatchScore(
                    title,
                    artist,
                    duration,
                    item.GetStringOrNull("name"),
                    ReadNetEaseArtistText(item),
                    item.GetInt64OrNull("dt") is { } milliseconds ? TimeSpan.FromMilliseconds(milliseconds) : null)
            })
            .Where(item => !string.IsNullOrWhiteSpace(item.Id) && item.Score < double.MaxValue)
            .OrderBy(item => item.Score)
            .Select(item => item.Id)
            .FirstOrDefault();
    }

    private async Task<string> GetLyricTextAsync(string songId, CancellationToken cancellationToken)
    {
        var payload = new Dictionary<string, string>
        {
            ["id"] = songId,
            ["os"] = "pc",
            ["lv"] = "-1",
            ["kv"] = "-1",
            ["tv"] = "-1",
            ["csrf_token"] = string.Empty
        };

        using var document = await SendWeApiAsync(
            "https://music.163.com/weapi/song/lyric?csrf_token=",
            payload,
            cancellationToken);
        return document.RootElement
            .GetPropertyOrNull("lrc")
            ?.GetStringOrNull("lyric") ?? string.Empty;
    }

    private async Task<JsonDocument> SendWeApiAsync(
        string url,
        Dictionary<string, string> payload,
        CancellationToken cancellationToken)
    {
        var prepared = Prepare(JsonSerializer.Serialize(payload));
        using var request = new HttpRequestMessage(HttpMethod.Post, url);
        request.Headers.Referrer = new Uri("https://music.163.com/");
        request.Content = new FormUrlEncodedContent(prepared);

        using var response = await HttpClient.SendAsync(request, cancellationToken);
        response.EnsureSuccessStatusCode();
        await using var stream = await response.Content.ReadAsStreamAsync(cancellationToken);
        return await JsonDocument.ParseAsync(stream, cancellationToken: cancellationToken);
    }

    private Dictionary<string, string> Prepare(string raw)
    {
        var firstPass = AesEncode(raw, Nonce);
        return new Dictionary<string, string>
        {
            ["params"] = AesEncode(firstPass, _secretKey),
            ["encSecKey"] = _encSecKey
        };
    }

    private static string ReadNetEaseArtistText(JsonElement item)
    {
        var artists = item.GetPropertyOrNull("ar") ?? item.GetPropertyOrNull("artists");

        if (artists is null || artists.Value.ValueKind != JsonValueKind.Array)
        {
            return string.Empty;
        }

        return string.Join(" ", artists.Value.EnumerateArray().Select(artist => artist.GetStringOrNull("name")));
    }

    private static string RsaEncode(string text)
    {
        var reversedText = new string(text.Reverse().ToArray());
        var value = HexToBigInteger(BitConverter.ToString(Encoding.Default.GetBytes(reversedText)).Replace("-", string.Empty));
        var exponent = HexToBigInteger(Pubkey);
        var modulus = HexToBigInteger(Modulus);
        var key = BigInteger.ModPow(value, exponent, modulus).ToString("x");
        key = key.PadLeft(256, '0');

        return key.Length > 256 ? key[^256..] : key;
    }

    private static BigInteger HexToBigInteger(string hex)
    {
        var result = new BigInteger(0);

        for (var i = 0; i < hex.Length; i++)
        {
            result += BigInteger.Multiply(
                new BigInteger(Convert.ToInt32(hex[i].ToString(), 16)),
                BigInteger.Pow(new BigInteger(16), hex.Length - i - 1));
        }

        return result;
    }

    private static string AesEncode(string secretData, string secret)
    {
        using var aes = Aes.Create();
        aes.Key = Encoding.UTF8.GetBytes(secret);
        aes.IV = Encoding.UTF8.GetBytes(Vi);
        aes.Mode = CipherMode.CBC;

        using var encryptor = aes.CreateEncryptor();
        using var stream = new MemoryStream();
        using (var cryptoStream = new CryptoStream(stream, encryptor, CryptoStreamMode.Write))
        using (var writer = new StreamWriter(cryptoStream))
        {
            writer.Write(secretData);
        }

        return Convert.ToBase64String(stream.ToArray());
    }

    private static string CreateSecretKey(int length)
    {
        const string chars = "0123456789abcdefghijklmnopqrstuvwxyzABCDEFGHIJKLMNOPQRSTUVWXYZ";
        var bytes = RandomNumberGenerator.GetBytes(length);
        var builder = new StringBuilder(length);

        foreach (var value in bytes)
        {
            builder.Append(chars[value % chars.Length]);
        }

        return builder.ToString();
    }
}

public static class JsonElementExtensions
{
    public static JsonElement? GetPropertyOrNull(this JsonElement element, string propertyName)
    {
        return element.ValueKind == JsonValueKind.Object && element.TryGetProperty(propertyName, out var property)
            ? property
            : null;
    }

    public static string? GetStringOrNull(this JsonElement element, string propertyName)
    {
        var property = element.GetPropertyOrNull(propertyName);
        return property is { ValueKind: JsonValueKind.String } ? property.Value.GetString() : null;
    }

    public static long? GetInt64OrNull(this JsonElement element, string propertyName)
    {
        var property = element.GetPropertyOrNull(propertyName);

        if (property is null)
        {
            return null;
        }

        if (property.Value.ValueKind == JsonValueKind.Number && property.Value.TryGetInt64(out var value))
        {
            return value;
        }

        if (property.Value.ValueKind == JsonValueKind.String &&
            long.TryParse(property.Value.GetString(), NumberStyles.Integer, CultureInfo.InvariantCulture, out value))
        {
            return value;
        }

        return null;
    }
}

[JsonSerializable(typeof(List<LrclibSearchResult>))]
[JsonSerializable(typeof(LrclibSearchResult))]
[JsonSerializable(typeof(List<AmllSearchResult>))]
public sealed partial class LrclibJsonContext : JsonSerializerContext;

public sealed record TimedLyricLine(TimeSpan StartTime, TimeSpan EndTime, string Text)
{
    public static IReadOnlyList<TimedLyricLine> Normalize(IEnumerable<TimedLyricLine> lines)
    {
        var ordered = lines
            .Where(line => !string.IsNullOrWhiteSpace(line.Text))
            .OrderBy(line => line.StartTime)
            .ToArray();

        if (ordered.Length == 0)
        {
            return Array.Empty<TimedLyricLine>();
        }

        var normalized = new TimedLyricLine[ordered.Length];

        for (var i = 0; i < ordered.Length; i++)
        {
            var start = ordered[i].StartTime;
            var end = ordered[i].EndTime > start ? ordered[i].EndTime : start + TimeSpan.FromSeconds(4);

            if (i + 1 < ordered.Length && ordered[i + 1].StartTime > start)
            {
                end = ordered[i + 1].StartTime;
            }

            normalized[i] = ordered[i] with { EndTime = end };
        }

        return normalized;
    }
}
