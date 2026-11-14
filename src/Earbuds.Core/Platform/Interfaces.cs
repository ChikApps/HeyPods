// Copyright (C) 2026 ChikApps
// SPDX-License-Identifier: GPL-3.0-only
//
// Everything the core needs from the operating system, as interfaces.
// Windows implementations: src/Earbuds.Windows. Test fakes: tests/Earbuds.Core.Tests/Fakes.cs.

namespace Earbuds.Core.Platform;

/// <summary>One received BLE broadcast with Apple manufacturer data.</summary>
/// <param name="TransientAddress">
/// BLE address of the sender. It rotates every few minutes. Kept in memory for a few seconds only
/// (to tell two senders apart); never saved to disk (spec A4).
/// </param>
/// <param name="Rssi">Signal strength in dBm.</param>
/// <param name="AppleData">Manufacturer data after the 0x004C company ID.</param>
/// <param name="Timestamp">When it was received.</param>
public sealed record BleAdvertisement(ulong TransientAddress, short Rssi, ReadOnlyMemory<byte> AppleData, DateTimeOffset Timestamp);

/// <summary>Scanner state.</summary>
public enum ScannerStatus
{
    Stopped,
    Started,
    /// <summary>Stopped because of an error (radio off, adapter removed, blocked by policy...).</summary>
    Failed,
}

/// <summary>Source of BLE broadcasts (Windows: BluetoothLEAdvertisementWatcher).</summary>
public interface IBleAdvertisementSource : IDisposable
{
    event EventHandler<BleAdvertisement>? Received;
    event EventHandler<ScannerStatus>? StatusChanged;
    ScannerStatus Status { get; }
    /// <summary>Starts scanning. Must not throw; failures are reported through <see cref="StatusChanged"/>.</summary>
    void Start();
    void Stop();
}

/// <summary>Bluetooth radio availability.</summary>
public enum RadioState
{
    Unknown,
    On,
    Off,
    /// <summary>No Bluetooth adapter, or the adapter has no Bluetooth LE.</summary>
    NotSupported,
}

/// <summary>Bluetooth radio monitor (Windows: BluetoothAdapter + Radio).</summary>
public interface IBluetoothRadio : IDisposable
{
    event EventHandler<RadioState>? StateChanged;
    RadioState State { get; }
    Task InitializeAsync();
}

/// <summary>A classic Bluetooth device that Windows reports as connected.</summary>
/// <param name="Name">Device name as shown in Windows (e.g. "My AirPods Pro").</param>
/// <param name="VendorId">Bluetooth vendor ID (Apple = 0x004C), 0 when unknown.</param>
/// <param name="ProductId">Bluetooth product ID, 0 when unknown (byte-swapped model code, see ModelCatalog.ToWindowsProductId).</param>
public sealed record ConnectedAudioDevice(string Name, ushort VendorId, ushort ProductId);

/// <summary>Which Bluetooth audio devices are connected to this PC.</summary>
public interface IConnectedAudioDevices : IDisposable
{
    event EventHandler? Changed;
    Task<IReadOnlyList<ConnectedAudioDevice>> GetConnectedAsync();
}

/// <summary>Playback status of one media session.</summary>
public enum MediaPlaybackStatus
{
    Unknown,
    Playing,
    Paused,
    Stopped,
    /// <summary>The session no longer exists.</summary>
    Gone,
}

/// <summary>A media session (Windows: GlobalSystemMediaTransportControlsSession).</summary>
/// <param name="Id">Stable id of the app (SourceAppUserModelId).</param>
public sealed record MediaSession(string Id, MediaPlaybackStatus Status);

/// <summary>System media sessions (Windows: GlobalSystemMediaTransportControlsSessionManager).</summary>
/// <remarks>No "toggle" media key anywhere: a toggle could start playback by mistake (spec, state "no active player").</remarks>
public interface IMediaSessions
{
    /// <summary>Raised when a session's playback status changes (best effort).</summary>
    event EventHandler<MediaSession>? PlaybackChanged;
    /// <summary>The session that is playing right now, or null.</summary>
    Task<MediaSession?> GetPlayingSessionAsync();
    Task<MediaPlaybackStatus> GetStatusAsync(string sessionId);
    Task<bool> TryPauseAsync(string sessionId);
    Task<bool> TryPlayAsync(string sessionId);
}
