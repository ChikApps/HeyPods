// Copyright (C) 2026 ChikApps
// SPDX-License-Identifier: GPL-3.0-only

using Earbuds.Core.Platform;

namespace Earbuds.Core.Tests;

internal static class Hex
{
    /// <summary>"07 19 01 ..." or "071901..." -> bytes.</summary>
    public static byte[] Bytes(string hex) => Convert.FromHexString(hex.Replace(" ", string.Empty));

    /// <summary>Strips the leading "07 19" so tests can also feed only the payload when needed.</summary>
    public static byte[] Payload(string hex) => Bytes(hex)[2..];
}

internal sealed class FakeBleSource : IBleAdvertisementSource
{
    public event EventHandler<BleAdvertisement>? Received;
    public event EventHandler<ScannerStatus>? StatusChanged;
    public ScannerStatus Status { get; set; } = ScannerStatus.Stopped;
    public int StartCalls { get; private set; }

    public void Start()
    {
        StartCalls++;
        Status = ScannerStatus.Started;
        StatusChanged?.Invoke(this, Status);
    }

    public void Stop() => Status = ScannerStatus.Stopped;

    public void Emit(string hex, short rssi, ulong address = 0xAABBCCDDEEFF, DateTimeOffset? at = null) =>
        Emit(Hex.Bytes(hex), rssi, address, at);

    public void Emit(byte[] data, short rssi, ulong address = 0xAABBCCDDEEFF, DateTimeOffset? at = null) =>
        Received?.Invoke(this, new BleAdvertisement(address, rssi, data, at ?? DateTimeOffset.UnixEpoch));

    public void Dispose()
    {
    }
}

internal sealed class FakeRadio : IBluetoothRadio
{
    public event EventHandler<RadioState>? StateChanged;
    public RadioState State { get; private set; } = RadioState.On;

    public Task InitializeAsync() => Task.CompletedTask;

    public void Set(RadioState state)
    {
        State = state;
        StateChanged?.Invoke(this, state);
    }

    public void Dispose()
    {
    }
}

internal sealed class FakeAudioDevices : IConnectedAudioDevices
{
    public event EventHandler? Changed;
    public List<ConnectedAudioDevice> Devices { get; } = new();

    public Task<IReadOnlyList<ConnectedAudioDevice>> GetConnectedAsync() =>
        Task.FromResult<IReadOnlyList<ConnectedAudioDevice>>(Devices.ToList());

    public void RaiseChanged() => Changed?.Invoke(this, EventArgs.Empty);

    public void Dispose()
    {
    }
}

/// <summary>A pretend media player world: sessions with a status, and a log of what we asked for.</summary>
internal sealed class FakeMediaSessions : IMediaSessions
{
    public event EventHandler<MediaSession>? PlaybackChanged;
    public Dictionary<string, MediaPlaybackStatus> Sessions { get; } = new();
    public List<string> Calls { get; } = new();
    public bool FailCalls { get; set; }

    public Task<MediaSession?> GetPlayingSessionAsync()
    {
        if (FailCalls)
        {
            throw new InvalidOperationException("player went away");
        }

        var playing = Sessions.FirstOrDefault(s => s.Value == MediaPlaybackStatus.Playing);
        return Task.FromResult(playing.Key is null ? null : new MediaSession(playing.Key, playing.Value));
    }

    public Task<MediaPlaybackStatus> GetStatusAsync(string sessionId) =>
        Task.FromResult(Sessions.TryGetValue(sessionId, out var s) ? s : MediaPlaybackStatus.Gone);

    public Task<bool> TryPauseAsync(string sessionId)
    {
        Calls.Add("pause:" + sessionId);
        if (!Sessions.ContainsKey(sessionId))
        {
            return Task.FromResult(false);
        }

        Sessions[sessionId] = MediaPlaybackStatus.Paused;
        return Task.FromResult(true);
    }

    public Task<bool> TryPlayAsync(string sessionId)
    {
        Calls.Add("play:" + sessionId);
        if (!Sessions.ContainsKey(sessionId))
        {
            return Task.FromResult(false);
        }

        Sessions[sessionId] = MediaPlaybackStatus.Playing;
        return Task.FromResult(true);
    }

    /// <summary>The user does something in the player.</summary>
    public void UserSets(string sessionId, MediaPlaybackStatus status)
    {
        Sessions[sessionId] = status;
        PlaybackChanged?.Invoke(this, new MediaSession(sessionId, status));
    }
}
