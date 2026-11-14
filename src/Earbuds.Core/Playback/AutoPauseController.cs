// Copyright (C) 2026 ChikApps
// SPDX-License-Identifier: GPL-3.0-only
//
// Pause/resume logic. Our own code; the idea of remembering which session we paused and resuming only
// that one comes from JamesCJ60/WinPods (GPL-3.0) WinPods/Services/AirPodsWearPlaybackController.cs and
// WindowsSystemMediaTransport.cs. Differences: we never act on the first reading, never send a
// play/pause key, and drop the resume when the user touched playback in between.

using Earbuds.Core.Platform;

namespace Earbuds.Core.Playback;

/// <summary>What the controller did with a reading (for tests and diagnostics).</summary>
public enum AutoPauseAction
{
    None,
    Paused,
    Resumed,
}

/// <summary>
/// Spec (spec §1.5, §3, §6): a pod leaves the ear -> pause. It comes back -> resume, only if we paused.
/// Never resume a player the user paused. Never pause when our AirPods are not connected as audio
/// (a neighbour's AirPods must not stop your music). Off in settings -> neither pause nor resume.
/// </summary>
/// <remarks>
/// Rule, in pods-in-ear counts: pause when the count drops (2->1, 1->0), resume when it is back to the
/// count it had before we paused. So the usual case is "pause when one leaves, resume when both are back",
/// and it also works for someone listening with a single pod.
/// </remarks>
public sealed class AutoPauseController : IDisposable
{
    /// <summary>Status events this soon after our own pause are ignored (they may describe the moment before it).</summary>
    public static readonly TimeSpan OwnPauseGrace = TimeSpan.FromSeconds(1.5);

    private readonly IMediaSessions _media;
    private readonly TimeProvider _time;
    private readonly SemaphoreSlim _gate = new(1, 1);
    private long _pausedAtTicks;
    private int? _lastCount;
    private string? _pausedSessionId;
    private int _resumeAtCount;

    public AutoPauseController(IMediaSessions media, TimeProvider? time = null)
    {
        _media = media;
        _time = time ?? TimeProvider.System;
        _media.PlaybackChanged += OnPlaybackChanged;
    }

    /// <summary>The session we paused and may resume, or null.</summary>
    public string? PausedSessionId => _pausedSessionId;

    /// <summary>
    /// Feed one wear reading of "my" AirPods.
    /// </summary>
    /// <param name="inEarCount">Pods in an ear (over-ear: 1 = worn), or null when unknown.</param>
    /// <param name="enabled">Setting "pause when a pod is removed".</param>
    /// <param name="connectedAsAudio">My AirPods are connected to this PC as an audio device.</param>
    public async Task<AutoPauseAction> OnWearReadingAsync(int? inEarCount, bool enabled, bool connectedAsAudio)
    {
        await _gate.WaitAsync().ConfigureAwait(false);
        try
        {
            if (!enabled)
            {
                _pausedSessionId = null;
                _lastCount = inEarCount ?? _lastCount;
                return AutoPauseAction.None;
            }

            if (inEarCount is not { } count)
            {
                return AutoPauseAction.None;
            }

            var previous = _lastCount;
            _lastCount = count;

            if (previous is not { } before || before == count)
            {
                // First reading after start/selection: we did not see a pod leave, so do nothing.
                return AutoPauseAction.None;
            }

            if (count < before)
            {
                return await HandleRemovedAsync(before, connectedAsAudio).ConfigureAwait(false);
            }

            return await HandleInsertedAsync(count, connectedAsAudio).ConfigureAwait(false);
        }
        catch
        {
            // Media APIs can throw (player closed mid-call). Never crash the app over playback.
            return AutoPauseAction.None;
        }
        finally
        {
            _gate.Release();
        }
    }

    /// <summary>Forget everything (new AirPods chosen, or "forget my AirPods").</summary>
    public void Reset()
    {
        _gate.Wait();
        try
        {
            _lastCount = null;
            _pausedSessionId = null;
            _resumeAtCount = 0;
        }
        finally
        {
            _gate.Release();
        }
    }

    public void Dispose()
    {
        _media.PlaybackChanged -= OnPlaybackChanged;
        _gate.Dispose();
    }

    private async Task<AutoPauseAction> HandleRemovedAsync(int countBefore, bool connectedAsAudio)
    {
        if (_pausedSessionId is not null)
        {
            return AutoPauseAction.None; // already paused by us (e.g. 2 -> 1 -> 0); keep the original resume target
        }

        if (!connectedAsAudio)
        {
            return AutoPauseAction.None;
        }

        var playing = await _media.GetPlayingSessionAsync().ConfigureAwait(false);
        if (playing is null || playing.Status != MediaPlaybackStatus.Playing)
        {
            return AutoPauseAction.None; // nothing playing or user already paused: nothing to remember
        }

        if (!await _media.TryPauseAsync(playing.Id).ConfigureAwait(false))
        {
            return AutoPauseAction.None;
        }

        _pausedSessionId = playing.Id;
        _resumeAtCount = countBefore;
        Volatile.Write(ref _pausedAtTicks, _time.GetUtcNow().UtcTicks);
        return AutoPauseAction.Paused;
    }

    private async Task<AutoPauseAction> HandleInsertedAsync(int count, bool connectedAsAudio)
    {
        if (_pausedSessionId is not { } sessionId || count < _resumeAtCount)
        {
            return AutoPauseAction.None;
        }

        _pausedSessionId = null;
        if (!connectedAsAudio)
        {
            return AutoPauseAction.None;
        }

        // Resume only if the session is still paused (the user did not play/stop/close it meanwhile).
        var status = await _media.GetStatusAsync(sessionId).ConfigureAwait(false);
        if (status != MediaPlaybackStatus.Paused)
        {
            return AutoPauseAction.None;
        }

        return await _media.TryPlayAsync(sessionId).ConfigureAwait(false)
            ? AutoPauseAction.Resumed
            : AutoPauseAction.None;
    }

    private void OnPlaybackChanged(object? sender, MediaSession session)
    {
        // The user resumed the player we paused (or closed it): it is theirs again, do not resume later.
        // A stop needs no handling here: resume only happens from "Paused".
        var paused = Volatile.Read(ref _pausedSessionId);
        if (paused is null ||
            session.Status is not (MediaPlaybackStatus.Playing or MediaPlaybackStatus.Gone) ||
            !string.Equals(session.Id, paused, StringComparison.Ordinal))
        {
            return;
        }

        var sincePause = _time.GetUtcNow().UtcTicks - Volatile.Read(ref _pausedAtTicks);
        if (sincePause >= 0 && sincePause < OwnPauseGrace.Ticks)
        {
            return;
        }

        // Clear only if it is still the same pause (same string instance we read above).
        Interlocked.CompareExchange(ref _pausedSessionId, null, paused);
    }
}
