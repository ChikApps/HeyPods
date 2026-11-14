// Copyright (C) 2026 ChikApps
// SPDX-License-Identifier: GPL-3.0-only
//
// System media sessions (GSMTC). The approach (current session first, then all sessions; remember
// the session by SourceAppUserModelId) follows JamesCJ60/WinPods (GPL-3.0)
// WinPods/Services/WindowsSystemMediaTransport.cs:105-140. Written by us. Unlike that file there is no
// media-key or audio-level fallback: a toggle key could start playback by mistake (spec §3).
// NOT RUN (no Windows here).

using Earbuds.Core.Platform;
using Windows.Media.Control;
using GsmtcStatus = Windows.Media.Control.GlobalSystemMediaTransportControlsSessionPlaybackStatus;

namespace Earbuds.Windows;

/// <summary>Windows implementation of <see cref="IMediaSessions"/>.</summary>
public sealed class WinMediaSessions : IMediaSessions, IDisposable
{
    private readonly SemaphoreSlim _managerGate = new(1, 1);
    private readonly object _hookLock = new();
    private readonly Dictionary<string, GlobalSystemMediaTransportControlsSession> _hooked = new(StringComparer.Ordinal);
    private GlobalSystemMediaTransportControlsSessionManager? _manager;

    public event EventHandler<MediaSession>? PlaybackChanged;

    public async Task<MediaSession?> GetPlayingSessionAsync()
    {
        var manager = await GetManagerAsync().ConfigureAwait(false);
        if (manager is null)
        {
            return null;
        }

        var current = manager.GetCurrentSession();
        if (current is not null && Map(current) == MediaPlaybackStatus.Playing)
        {
            return new MediaSession(current.SourceAppUserModelId, MediaPlaybackStatus.Playing);
        }

        foreach (var session in manager.GetSessions())
        {
            if (Map(session) == MediaPlaybackStatus.Playing)
            {
                return new MediaSession(session.SourceAppUserModelId, MediaPlaybackStatus.Playing);
            }
        }

        return null;
    }

    public async Task<MediaPlaybackStatus> GetStatusAsync(string sessionId)
    {
        var session = await FindAsync(sessionId).ConfigureAwait(false);
        return session is null ? MediaPlaybackStatus.Gone : Map(session);
    }

    public async Task<bool> TryPauseAsync(string sessionId)
    {
        var session = await FindAsync(sessionId).ConfigureAwait(false);
        return session is not null && await session.TryPauseAsync();
    }

    public async Task<bool> TryPlayAsync(string sessionId)
    {
        var session = await FindAsync(sessionId).ConfigureAwait(false);
        return session is not null && await session.TryPlayAsync();
    }

    public void Dispose()
    {
        lock (_hookLock)
        {
            foreach (var session in _hooked.Values)
            {
                session.PlaybackInfoChanged -= OnPlaybackInfoChanged;
            }

            _hooked.Clear();
        }

        if (_manager is not null)
        {
            _manager.SessionsChanged -= OnSessionsChanged;
            _manager = null;
        }

        _managerGate.Dispose();
    }

    private async Task<GlobalSystemMediaTransportControlsSession?> FindAsync(string sessionId)
    {
        var manager = await GetManagerAsync().ConfigureAwait(false);
        if (manager is null)
        {
            return null;
        }

        foreach (var session in manager.GetSessions())
        {
            if (string.Equals(session.SourceAppUserModelId, sessionId, StringComparison.Ordinal))
            {
                return session;
            }
        }

        return null;
    }

    private async Task<GlobalSystemMediaTransportControlsSessionManager?> GetManagerAsync()
    {
        if (_manager is not null)
        {
            return _manager;
        }

        await _managerGate.WaitAsync().ConfigureAwait(false);
        try
        {
            if (_manager is null)
            {
                var manager = await GlobalSystemMediaTransportControlsSessionManager.RequestAsync();
                manager.SessionsChanged += OnSessionsChanged;
                _manager = manager;
                HookSessions(manager);
            }

            return _manager;
        }
        catch (Exception)
        {
            return null; // e.g. Windows without media session support: "no active player" -> do nothing
        }
        finally
        {
            _managerGate.Release();
        }
    }

    private void OnSessionsChanged(GlobalSystemMediaTransportControlsSessionManager sender, SessionsChangedEventArgs args)
    {
        try
        {
            HookSessions(sender);
        }
        catch (Exception)
        {
            // ignore: best effort
        }
    }

    private void HookSessions(GlobalSystemMediaTransportControlsSessionManager manager)
    {
        var gone = new List<string>();
        lock (_hookLock)
        {
            var current = manager.GetSessions().ToDictionary(s => s.SourceAppUserModelId, StringComparer.Ordinal);
            foreach (var (id, session) in _hooked.ToList())
            {
                if (!current.ContainsKey(id))
                {
                    session.PlaybackInfoChanged -= OnPlaybackInfoChanged;
                    _hooked.Remove(id);
                    gone.Add(id);
                }
            }

            foreach (var (id, session) in current)
            {
                if (!_hooked.ContainsKey(id))
                {
                    session.PlaybackInfoChanged += OnPlaybackInfoChanged;
                    _hooked[id] = session;
                }
            }
        }

        foreach (var id in gone)
        {
            PlaybackChanged?.Invoke(this, new MediaSession(id, MediaPlaybackStatus.Gone));
        }
    }

    private void OnPlaybackInfoChanged(GlobalSystemMediaTransportControlsSession sender, PlaybackInfoChangedEventArgs args)
    {
        try
        {
            PlaybackChanged?.Invoke(this, new MediaSession(sender.SourceAppUserModelId, Map(sender)));
        }
        catch (Exception)
        {
            // session closed while reading it
        }
    }

    private static MediaPlaybackStatus Map(GlobalSystemMediaTransportControlsSession session) =>
        session.GetPlaybackInfo()?.PlaybackStatus switch
        {
            GsmtcStatus.Playing => MediaPlaybackStatus.Playing,
            GsmtcStatus.Paused => MediaPlaybackStatus.Paused,
            GsmtcStatus.Stopped or GsmtcStatus.Closed => MediaPlaybackStatus.Stopped,
            _ => MediaPlaybackStatus.Unknown,
        };
}
