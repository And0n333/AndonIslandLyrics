using System.Diagnostics;
using System.Windows;
using Windows.Media.Control;

namespace AndonIslandLyrics.Models;

/// <summary>
/// SMTC 会话管理器 — 源自 HotLyric 的 SMTCManager。
/// 管理所有活跃的媒体会话，提供会话变更通知。
/// </summary>
public sealed class SmtcManager : IDisposable
{
    private GlobalSystemMediaTransportControlsSessionManager? _manager;
    private readonly Dictionary<string, SmtcSession> _sessions = new();
    private bool _disposed;

    public event EventHandler<IMediaSession>? SessionAdded;
    public event EventHandler<IMediaSession>? SessionRemoved;

    public IReadOnlyCollection<SmtcSession> Sessions => _sessions.Values;

    public bool IsInitialized => _manager != null;

    private SmtcManager()
    {
    }

    public static async Task<SmtcManager> CreateAsync()
    {
        var instance = new SmtcManager();
        await instance.InitializeAsync();
        return instance;
    }

    private async Task InitializeAsync()
    {
        try
        {
            _manager = await GlobalSystemMediaTransportControlsSessionManager.RequestAsync();
            _manager.SessionsChanged += OnSessionsChanged;

            // 枚举已有会话
            var existingSessions = _manager.GetSessions();
            foreach (var session in existingSessions)
            {
                AddSession(session);
            }
        }
        catch (Exception ex)
        {
            Debug.WriteLine($"SMTC Manager init failed: {ex.Message}");
        }
    }

    private void OnSessionsChanged(GlobalSystemMediaTransportControlsSessionManager sender, SessionsChangedEventArgs args)
    {
        Application.Current.Dispatcher.Invoke(() =>
        {
            if (_disposed || _manager == null) return;

            var currentSessions = _manager.GetSessions();
            var currentIds = new HashSet<string>(currentSessions.Select(s => GetSessionKey(s)));

            // 移除已断开的会话
            var removedKeys = _sessions.Keys.Where(k => !currentIds.Contains(k)).ToList();
            foreach (var key in removedKeys)
            {
                if (_sessions.Remove(key, out var removed))
                {
                    removed.Dispose();
                    SessionRemoved?.Invoke(this, removed);
                }
            }

            // 添加新会话
            foreach (var session in currentSessions)
            {
                var key = GetSessionKey(session);
                if (!_sessions.ContainsKey(key))
                {
                    AddSession(session);
                }
            }
        });
    }

    private void AddSession(GlobalSystemMediaTransportControlsSession nativeSession)
    {
        var key = GetSessionKey(nativeSession);
        if (_sessions.ContainsKey(key)) return;

        var smtcSession = new SmtcSession(nativeSession);
        _sessions[key] = smtcSession;
        _ = smtcSession.RefreshMediaPropertiesAsync();
        SessionAdded?.Invoke(this, smtcSession);
    }

    private static string GetSessionKey(GlobalSystemMediaTransportControlsSession session)
    {
        return session.SourceAppUserModelId ?? session.GetHashCode().ToString();
    }

    public void Dispose()
    {
        if (_disposed) return;
        _disposed = true;

        if (_manager != null)
        {
            _manager.SessionsChanged -= OnSessionsChanged;
        }

        foreach (var session in _sessions.Values)
        {
            session.Dispose();
        }
        _sessions.Clear();

        SessionAdded = null;
        SessionRemoved = null;
    }
}
