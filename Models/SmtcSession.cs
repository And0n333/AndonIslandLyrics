using System.Diagnostics;
using System.IO;
using System.Windows;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using Windows.Media.Control;
using Windows.Storage.Streams;

namespace DynamicIslandLyrics.Models;

/// <summary>
/// SMTC 会话封装 — 源自 HotLyric 的 SMTCSession。
/// 包装 GlobalSystemMediaTransportControlsSession，提供强类型事件和属性。
/// </summary>
public sealed class SmtcSession : IMediaSession
{
    private readonly GlobalSystemMediaTransportControlsSession _session;
    private bool _disposed;
    private string _title = string.Empty;
    private string _artist = string.Empty;
    private string _appName = string.Empty;
    private TimeSpan _position;
    private TimeSpan _duration;
    private MediaSessionPlaybackStatus _playbackStatus;
    private ImageSource? _albumCover;

    public SmtcSession(GlobalSystemMediaTransportControlsSession session)
    {
        _session = session ?? throw new ArgumentNullException(nameof(session));
        Id = Guid.NewGuid().ToString("N");

        // 获取 AppName
        try
        {
            _appName = session.SourceAppUserModelId ?? "未知应用";
        }
        catch
        {
            _appName = "未知应用";
        }

        // 订阅事件
        _session.MediaPropertiesChanged += OnMediaPropertiesChanged;
        _session.TimelinePropertiesChanged += OnTimelinePropertiesChanged;
        _session.PlaybackInfoChanged += OnPlaybackInfoChanged;
    }

    public string Id { get; }
    public string AppName => _appName;
    public string Title => _title;
    public string Artist => _artist;
    public TimeSpan Position => _position;
    public TimeSpan Duration => _duration;
    public MediaSessionPlaybackStatus PlaybackStatus => _playbackStatus;
    public ImageSource? AlbumCover => _albumCover;
    public bool IsDisposed => _disposed;

    public event EventHandler? MediaPropertiesChanged;
    public event EventHandler? PlaybackInfoChanged;
    public event EventHandler? TimelinePropertiesChanged;
    public event EventHandler? SessionDisconnected;

    public async Task RefreshMediaPropertiesAsync()
    {
        if (_disposed) return;

        try
        {
            var properties = await _session.TryGetMediaPropertiesAsync();
            if (properties == null) return;

            var title = properties.Title?.Trim() ?? string.Empty;
            var artist = properties.Artist?.Trim() ?? string.Empty;

            var changed = !string.Equals(_title, title, StringComparison.Ordinal) ||
                          !string.Equals(_artist, artist, StringComparison.Ordinal);

            _title = title;
            _artist = artist;

            // 加载专辑封面
            _albumCover = await LoadThumbnailAsync(properties.Thumbnail);

            if (changed)
            {
                UpdateTimeline();
                MediaPropertiesChanged?.Invoke(this, EventArgs.Empty);
            }
        }
        catch (Exception ex)
        {
            Debug.WriteLine($"SMTC refresh failed: {ex.Message}");
        }
    }

    private void UpdateTimeline()
    {
        if (_disposed) return;

        try
        {
            var timeline = _session.GetTimelineProperties();
            _position = timeline.Position;
            _duration = timeline.EndTime > timeline.StartTime
                ? timeline.EndTime - timeline.StartTime
                : TimeSpan.Zero;
        }
        catch
        {
            // session 可能已断开
        }
    }

    private void UpdatePlaybackInfo()
    {
        if (_disposed) return;

        try
        {
            var info = _session.GetPlaybackInfo();
            _playbackStatus = info.PlaybackStatus switch
            {
                GlobalSystemMediaTransportControlsSessionPlaybackStatus.Playing => MediaSessionPlaybackStatus.Playing,
                GlobalSystemMediaTransportControlsSessionPlaybackStatus.Paused => MediaSessionPlaybackStatus.Paused,
                GlobalSystemMediaTransportControlsSessionPlaybackStatus.Stopped => MediaSessionPlaybackStatus.Stopped,
                _ => MediaSessionPlaybackStatus.Unknown
            };
        }
        catch
        {
            _playbackStatus = MediaSessionPlaybackStatus.Stopped;
        }
    }

    private void OnMediaPropertiesChanged(GlobalSystemMediaTransportControlsSession sender, MediaPropertiesChangedEventArgs args)
    {
        _ = Application.Current.Dispatcher.InvokeAsync(async () =>
        {
            await RefreshMediaPropertiesAsync();
        });
    }

    private void OnTimelinePropertiesChanged(GlobalSystemMediaTransportControlsSession sender, TimelinePropertiesChangedEventArgs args)
    {
        Application.Current.Dispatcher.Invoke(() =>
        {
            if (_disposed) return;
            UpdateTimeline();
            TimelinePropertiesChanged?.Invoke(this, EventArgs.Empty);
        });
    }

    private void OnPlaybackInfoChanged(GlobalSystemMediaTransportControlsSession sender, PlaybackInfoChangedEventArgs args)
    {
        Application.Current.Dispatcher.Invoke(() =>
        {
            if (_disposed) return;
            UpdatePlaybackInfo();
            PlaybackInfoChanged?.Invoke(this, EventArgs.Empty);
        });
    }

    private static async Task<ImageSource?> LoadThumbnailAsync(IRandomAccessStreamReference? thumbnail)
    {
        if (thumbnail is null) return null;

        try
        {
            using var stream = await thumbnail.OpenReadAsync();
            using var memoryStream = new MemoryStream();
            stream.AsStreamForRead().CopyTo(memoryStream);
            memoryStream.Position = 0;

            var bitmap = new BitmapImage();
            bitmap.BeginInit();
            bitmap.CacheOption = BitmapCacheOption.OnLoad;
            bitmap.StreamSource = memoryStream;
            bitmap.EndInit();
            bitmap.Freeze();
            return bitmap;
        }
        catch
        {
            return null;
        }
    }

    public void Dispose()
    {
        if (_disposed) return;
        _disposed = true;

        try
        {
            _session.MediaPropertiesChanged -= OnMediaPropertiesChanged;
            _session.TimelinePropertiesChanged -= OnTimelinePropertiesChanged;
            _session.PlaybackInfoChanged -= OnPlaybackInfoChanged;
        }
        catch
        {
            // ignore
        }

        SessionDisconnected?.Invoke(this, EventArgs.Empty);
        SessionDisconnected = null;
        MediaPropertiesChanged = null;
        PlaybackInfoChanged = null;
        TimelinePropertiesChanged = null;
    }
}
