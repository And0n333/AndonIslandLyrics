namespace DynamicIslandLyrics.Models;

/// <summary>
/// 歌词数据模型 — 源自 HotLyric 的 Lyric record。
/// 包含歌词文档、翻译、元数据。
/// </summary>
public record Lyric(
    ILyricDocument? Content,
    string? LyricContent,
    ILyricDocument? Translate,
    string? TranslateContent,
    bool IsEmpty,
    string? SongName,
    string? Artists)
{
    private static readonly HashSet<string> AbsoluteMusicFlags =
    [
        "纯音乐，请欣赏",
        "此歌曲为没有填词的纯音乐，请您欣赏"
    ];

    public static Lyric CreateEmpty(string? songName, string? artists)
    {
        var content = ClassicLyricDocument.Create("[00:00.00]暂无歌词");
        return new Lyric(content, "[00:00.00]暂无歌词", null, null, true, songName, artists);
    }

    public static Lyric CreateLoading(string? songName, string? artists)
    {
        var content = ClassicLyricDocument.Create("[00:00.00]正在查找歌词...");
        return new Lyric(content, "[00:00.00]正在查找歌词...", null, null, true, songName, artists);
    }

    public static Lyric? FromLrc(string lyricContent, string? songName, string? artists)
    {
        ILyricDocument? content = null;

        try
        {
            content = ClassicLyricDocument.Create(lyricContent);
            if (content != null)
            {
                var hasMusicFlag = content.AllLines.Any(c =>
                    AbsoluteMusicFlags.Contains(c.AllSpans.FirstOrDefault()?.Text ?? ""));
                var allEmpty = content.AllLines.All(c => c.AllSpans.All(x => string.IsNullOrWhiteSpace(x.Text)));

                if (hasMusicFlag || allEmpty)
                {
                    content = null;
                }
            }
        }
        catch
        {
            return null;
        }

        if (content != null)
        {
            return new Lyric(content, lyricContent, null, null, false, songName, artists);
        }

        return null;
    }

    public static Lyric? FromTtml(IReadOnlyList<TimedLyricLine> ttmlLines, string? songName, string? artists)
    {
        if (ttmlLines.Count == 0)
            return null;

        var sb = new System.Text.StringBuilder();
        foreach (var line in ttmlLines)
        {
            sb.AppendLine($"[{line.StartTime:mm\\:ss\\.ff}]{line.Text}");
        }

        return FromLrc(sb.ToString(), songName, artists);
    }
}
