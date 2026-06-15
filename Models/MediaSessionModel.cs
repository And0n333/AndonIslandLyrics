using System.Windows;

namespace DynamicIslandLyrics.Models;

/// <summary>
/// 媒体会话模型 — 源自 HotLyric 的 MediaModel。
/// 将会话数据与歌词匹配逻辑结合在一起。
/// </summary>
public sealed class MediaSessionModel : IDisposable
{
    private readonly OnlineLyricProvider _onlineLyricProvider;
    private CancellationTokenSource? _lyricCts;
    private bool _disposed;

    public MediaSessionModel(IMediaSession session, OnlineLyricProvider onlineLyricProvider)
    {
        Session = session ?? throw new ArgumentNullException(nameof(session));
        _onlineLyricProvider = onlineLyricProvider ?? throw new ArgumentNullException(nameof(onlineLyricProvider));

        Lyric = Lyric.CreateEmpty(session.Title, session.Artist);

        Session.MediaPropertiesChanged += OnMediaPropertiesChanged;
        Session.SessionDisconnected += OnSessionDisconnected;
    }

    public IMediaSession Session { get; }
    public Lyric? Lyric { get; private set; }
    public bool HasLyric => Lyric is { IsEmpty: false };

    /// <summary>
    /// 获取当前时间点对应的歌词文本。
    /// </summary>
    public string GetCurrentLyricText(TimeSpan position)
    {
        var lyric = Lyric;
        if (lyric?.Content == null)
        {
            if (string.IsNullOrWhiteSpace(Session.Title))
                return "等待媒体会话";

            return string.IsNullOrWhiteSpace(Session.Artist)
                ? Session.Title
                : $"{Session.Title} - {Session.Artist}";
        }

        var currentLine = lyric.Content.GetCurrentLine(position, skipEmpty: true);
        if (currentLine != null && !string.IsNullOrWhiteSpace(currentLine.Text))
        {
            return currentLine.Text;
        }

        // 无匹配时检查是否已结束
        if (lyric.Content.AllLines.Count > 0)
        {
            var lastLine = lyric.Content.AllLines[^1];
            if (position > lastLine.EndTime)
            {
                return lyric.SongName ?? Session.Title;
            }
        }

        return string.IsNullOrWhiteSpace(Session.Artist)
            ? Session.Title
            : $"{Session.Title} - {Session.Artist}";
    }

    private async void OnMediaPropertiesChanged(object? sender, EventArgs e)
    {
        if (_disposed) return;
        await RefreshLyricAsync();
    }

    private void OnSessionDisconnected(object? sender, EventArgs e)
    {
        Dispose();
    }

    public async Task RefreshLyricAsync()
    {
        if (_disposed) return;
        if (string.IsNullOrWhiteSpace(Session.Title)) return;

        _lyricCts?.Cancel();
        _lyricCts?.Dispose();
        _lyricCts = new CancellationTokenSource();
        var token = _lyricCts.Token;

        try
        {
            Lyric = Lyric.CreateLoading(Session.Title, Session.Artist);

            var onlineLyrics = await _onlineLyricProvider.FindSyncedLyricsAsync(
                Session.Title, Session.Artist, Session.Duration, token);

            IReadOnlyList<TimedLyricLine> lyrics;
            if (onlineLyrics.Count > 0)
            {
                lyrics = onlineLyrics;
            }
            else
            {
                lyrics = LocalLyricProvider.TryLoad(Session.Title, Session.Artist);
            }

            if (token.IsCancellationRequested) return;

            if (lyrics.Count > 0)
            {
                Lyric = Lyric.FromTtml(lyrics, Session.Title, Session.Artist)
                        ?? Lyric.CreateEmpty(Session.Title, Session.Artist);
            }
            else
            {
                Lyric = Lyric.CreateEmpty(Session.Title, Session.Artist);
            }
        }
        catch (OperationCanceledException)
        {
            // 忽略取消
        }
        catch
        {
            if (token.IsCancellationRequested) return;

            var localLyrics = LocalLyricProvider.TryLoad(Session.Title, Session.Artist);
            Lyric = localLyrics.Count > 0
                ? Lyric.FromTtml(localLyrics, Session.Title, Session.Artist)
                    ?? Lyric.CreateEmpty(Session.Title, Session.Artist)
                : Lyric.CreateEmpty(Session.Title, Session.Artist);
        }
    }

    public void Dispose()
    {
        if (_disposed) return;
        _disposed = true;

        Session.MediaPropertiesChanged -= OnMediaPropertiesChanged;
        Session.SessionDisconnected -= OnSessionDisconnected;

        _lyricCts?.Cancel();
        _lyricCts?.Dispose();
        _lyricCts = null;

        Lyric = null;
    }
}
