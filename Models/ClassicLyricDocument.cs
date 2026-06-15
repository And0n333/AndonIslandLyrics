using System.Globalization;
using System.Text.RegularExpressions;

namespace AndonIslandLyrics.Models;

/// <summary>
/// 经典 LRC 格式歌词文档 — 源自 HotLyric 的 ClassicLyricDocument。
/// 将 LRC 文本解析为 ILyricDocument 层级结构，支持按时间查询。
/// </summary>
public partial class ClassicLyricDocument : ILyricDocument
{
    private ClassicLyricDocument(IReadOnlyList<ClassicLyricLine> lines)
    {
        AllLines = lines;
    }

    public IReadOnlyList<ILyricLine> AllLines { get; }

    public ILyricLine? GetCurrentLine(TimeSpan time, bool skipEmpty = true)
    {
        ILyricLine? result = null;

        foreach (var line in AllLines)
        {
            if (skipEmpty && string.IsNullOrWhiteSpace(line.Text))
                continue;

            if (line.StartTime <= time)
            {
                result = line;
            }

            if (line.StartTime > time)
            {
                break;
            }
        }

        return result;
    }

    public ILyricLine? GetNextLine(TimeSpan time, bool skipEmpty = true)
    {
        foreach (var line in AllLines)
        {
            if (skipEmpty && string.IsNullOrWhiteSpace(line.Text))
                continue;

            if (line.StartTime > time)
            {
                return line;
            }
        }

        return null;
    }

    public ILyricLine? GetCurrentOrNextLine(TimeSpan time, bool skipEmpty = true)
    {
        return GetCurrentLine(time, skipEmpty) ?? GetNextLine(time, skipEmpty);
    }

    /// <summary>
    /// 从 LRC 格式的文本创建 ClassicLyricDocument。
    /// </summary>
    public static ClassicLyricDocument? Create(string lrcText)
    {
        if (string.IsNullOrWhiteSpace(lrcText))
            return null;

        var lines = new List<ClassicLyricLine>();
        var textLines = lrcText.Split(["\r\n", "\n", "\r"], StringSplitOptions.None);

        foreach (var line in textLines)
        {
            var matches = TimestampRegex().Matches(line);

            if (matches.Count == 0)
                continue;

            var text = TimestampRegex().Replace(line, "").Trim();

            foreach (Match match in matches)
            {
                if (TryParseTimestamp(match, out var timestamp))
                {
                    lines.Add(new ClassicLyricLine(timestamp, timestamp, text));
                }
            }
        }

        if (lines.Count == 0)
            return null;

        // 归一化 EndTime：如果没有明确结束时间，用下一行开始时间
        var normalized = new ClassicLyricLine[lines.Count];
        for (int i = 0; i < lines.Count; i++)
        {
            var start = lines[i].StartTime;
            var end = lines[i].EndTime > start ? lines[i].EndTime : start + TimeSpan.FromSeconds(4);

            if (i + 1 < lines.Count && lines[i + 1].StartTime > start)
            {
                end = lines[i + 1].StartTime;
            }

            normalized[i] = new ClassicLyricLine(start, end, lines[i].Text);
        }

        return new ClassicLyricDocument(normalized);
    }

    [GeneratedRegex(@"\[(?<minutes>\d{1,3}):(?<seconds>\d{2})(?:\.(?<fraction>\d{1,3}))?\]")]
    private static partial Regex TimestampRegex();

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

    public class ClassicLyricLine : ILyricLine
    {
        public ClassicLyricLine(TimeSpan startTime, TimeSpan endTime, string text)
        {
            StartTime = startTime;
            EndTime = endTime;
            Text = text;
            AllSpans = [new ClassicLyricLineSpan(startTime, endTime, text, 0, text.Length, true)];
        }

        public TimeSpan StartTime { get; }
        public TimeSpan EndTime { get; }
        public bool IsEndLine => false;
        public string Text { get; }
        public IReadOnlyList<ILyricLineSpan> AllSpans { get; }

        public ILyricLineSpan? GetCurrentSpan(TimeSpan time)
        {
            return AllSpans.FirstOrDefault(s => s.StartTime <= time && s.EndTime > time);
        }

        public ILyricLineSpan? GetNextSpan(TimeSpan time)
        {
            return AllSpans.FirstOrDefault(s => s.StartTime > time);
        }
    }

    public record ClassicLyricLineSpan : ILyricLineSpan
    {
        public ClassicLyricLineSpan(TimeSpan startTime, TimeSpan endTime, string text, int charIndex, int charLength, bool isEndSpan)
        {
            StartTime = startTime;
            EndTime = endTime;
            Text = text;
            CharacterIndex = charIndex;
            CharacterLength = charLength;
            IsEndSpan = isEndSpan;
        }

        public TimeSpan StartTime { get; }
        public TimeSpan EndTime { get; }
        public string Text { get; }
        public bool IsEndSpan { get; }
        public int CharacterIndex { get; }
        public int CharacterLength { get; }
    }
}
