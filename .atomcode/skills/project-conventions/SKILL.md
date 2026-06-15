---
name: project-conventions
description: DynamicIslandLyrics 项目约定和架构知识
user_invocable: false
---

# DynamicIslandLyrics 项目约定

## 技术栈
- **语言**: C# 12 (.NET 8.0)
- **UI 框架**: WPF (Windows Presentation Foundation)
- **平台**: Windows 10.0.19041+ (仅限 Windows)
- **IDE**: Visual Studio 2022 / Rider / dotnet CLI

## 架构模式
- **MVVM**: ViewModel (`LyricsIslandViewModel`) 实现 `INotifyPropertyChanged`
- **代码分离**: XAML 布局 + C# code-behind (MainWindow.xaml / MainWindow.xaml.cs)
- **依赖注入**: 无 DI 容器，手动通过构造函数传递 `HttpClient`

## 命名规范
- **类**: PascalCase — `MainWindow`, `LyricsIslandViewModel`, `OnlineLyricProvider`
- **方法**: PascalCase — `InitializeSmtcAsync()`, `RefreshLyricsAsync()`
- **私有字段**: `_camelCase` — `_viewModel`, `_smtcSession`
- **本地变量**: `camelCase` — `title`, `songId`
- **文件名**: 与类名一致 — `MainWindow.xaml` / `MainWindow.xaml.cs`

## 核心功能

### SMTC 集成
- 使用 `GlobalSystemMediaTransportControlsSessionManager` 监听系统媒体会话
- 读取 `MediaProperties`（标题、歌手、专辑封面）、`TimelineProperties`（播放位置、时长）
- `SmtcTimelineTracker` 负责位置跟踪和校准

### 歌词获取优先级
1. AMLL TTML 歌词站 (https://amlldb.bikonoo.com)
2. QQ 音乐 API (`QQMusicLyricClient`)
3. 网易云音乐 API (`NetEaseLyricClient`)
4. LRCLIB 在线接口 (https://lrclib.net)
5. 本地 `Lyrics/` 目录 LRC 文件

### 歌词格式
- **TTML**: `<p begin="..." end="...">歌词文本</p>`，忽略 `ttm:role="x-translation"`
- **LRC**: `[mm:ss.xx]歌词文本`
- **内部模型**: `TimedLyricLine` — `{Time: TimeSpan, Text: string, EndTime: TimeSpan?}`

## 关键约定
- 所有 lyric provider 实现 `IDisposable`
- 异步方法统一 `Async` 后缀
- 使用 `System.Text.Json` 而非 Newtonsoft.Json
- 歌曲时长用于过滤候选结果，减少错配
- 本地时钟推进 + SMTC 事件校准的混合定位策略
