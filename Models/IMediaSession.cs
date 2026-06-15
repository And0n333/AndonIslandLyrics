using System.Windows.Media;

namespace DynamicIslandLyrics.Models;

/// <summary>
/// SMTC 媒体会话接口 — 源自 HotLyric 的 IMediaSession。
/// 抽象单个媒体会话的属性和事件。
/// </summary>
public interface IMediaSession : IDisposable
{
    string Id { get; }
    string AppName { get; }
    string Title { get; }
    string Artist { get; }
    TimeSpan Position { get; }
    TimeSpan Duration { get; }
    MediaSessionPlaybackStatus PlaybackStatus { get; }
    ImageSource? AlbumCover { get; }

    event EventHandler? MediaPropertiesChanged;
    event EventHandler? PlaybackInfoChanged;
    event EventHandler? TimelinePropertiesChanged;
    event EventHandler? SessionDisconnected;

    Task RefreshMediaPropertiesAsync();
    bool IsDisposed { get; }
}

public enum MediaSessionPlaybackStatus
{
    Unknown,
    Playing,
    Paused,
    Stopped,
    Opened,
    Closing
}
