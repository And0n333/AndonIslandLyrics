namespace DynamicIslandLyrics.Models;

/// <summary>
/// 歌词文档接口 — 来自 HotLyric 的设计模式。
/// 支持按时间点查询当前行、下一行、上一行。
/// </summary>
public interface ILyricDocument
{
    IReadOnlyList<ILyricLine> AllLines { get; }
    ILyricLine? GetCurrentLine(TimeSpan time, bool skipEmpty);
    ILyricLine? GetNextLine(TimeSpan time, bool skipEmpty);
    ILyricLine? GetCurrentOrNextLine(TimeSpan time, bool skipEmpty);
}

/// <summary>
/// 歌词行接口 — 包含时间范围和内部的字符跨度。
/// </summary>
public interface ILyricLine
{
    TimeSpan StartTime { get; }
    TimeSpan EndTime { get; }
    bool IsEndLine { get; }
    string Text { get; }
    IReadOnlyList<ILyricLineSpan> AllSpans { get; }
    ILyricLineSpan? GetCurrentSpan(TimeSpan time);
    ILyricLineSpan? GetNextSpan(TimeSpan time);
}

/// <summary>
/// 歌词字符跨度 — 支持逐字歌词（卡拉OK效果）。
/// </summary>
public interface ILyricLineSpan
{
    TimeSpan StartTime { get; }
    TimeSpan EndTime { get; }
    string Text { get; }
    bool IsEndSpan { get; }
    int CharacterIndex { get; }
    int CharacterLength { get; }
}
