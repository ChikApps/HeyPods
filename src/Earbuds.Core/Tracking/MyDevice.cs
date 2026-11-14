// Copyright (C) 2026 ChikApps
// SPDX-License-Identifier: GPL-3.0-only
//
// "My AirPods" vs. someone else's (spec §5). Our own logic.
// RSSI starting values sit between OpenPods (-60 dBm, PodsStatusScanCallback.java:30) and
// AirPodsDesktop (default -80, Source/Core/Settings.h:78), as the spec asks. To be calibrated on real hardware.

using Earbuds.Core.Ble;
using Earbuds.Core.Models;

namespace Earbuds.Core.Tracking;

/// <summary>All signal-strength constants in one place (spec: "fixed in one file in the code").</summary>
public static class Thresholds
{
    /// <summary>Show data only from broadcasts at least this strong (dBm).</summary>
    public const short DisplayRssi = -70;

    /// <summary>Open the popup only for a case opened at least this close (dBm). Stronger than <see cref="DisplayRssi"/>.</summary>
    public const short PopupRssi = -60;

    /// <summary>Wizard: list candidates down to this strength (dBm). Looser, so a user's own case is not missed.</summary>
    public const short WizardRssi = -80;

    /// <summary>
    /// A matching sender this close to the strongest one still counts as mine (dB). My two pods broadcast
    /// from two addresses with slightly different strength; both must pass. A same-model, same-color pair
    /// further away than this is rejected while mine is around.
    /// </summary>
    public const int SameDeviceToleranceDb = 8;

    /// <summary>When several matching senders are around, compare them over this window.</summary>
    public static readonly TimeSpan CompareWindow = TimeSpan.FromSeconds(5);

    /// <summary>Wizard: forget candidates not seen for this long.</summary>
    public static readonly TimeSpan CandidateTtl = TimeSpan.FromSeconds(15);

    /// <summary>No matching broadcast for this long = "not around" (spec §3).</summary>
    public static readonly TimeSpan NotSeenAfter = TimeSpan.FromSeconds(60);

    /// <summary>Popup closes by itself after this long (spec §6).</summary>
    public static readonly TimeSpan PopupDuration = TimeSpan.FromSeconds(8);

    /// <summary>Low battery mark on the tray icon: a pod at or below this percent (spec §2).</summary>
    public const int LowBatteryPercent = 20;
}

/// <summary>
/// What we save after the wizard (spec §5 layer 1, A4): model code, color byte and, if there was one,
/// the name of the matching connected Windows device. No addresses, nothing about other people's devices.
/// </summary>
public sealed record MyDeviceProfile(ushort ModelCode, byte ColorCode, string? ConnectedDeviceName = null)
{
    public bool Matches(PodsAdvertisement advertisement) =>
        advertisement.ModelCode == ModelCode && advertisement.ColorCode == ColorCode;
}

/// <summary>
/// Picks "my" broadcasts: same model and color, strong enough, and from the strongest matching sender
/// seen in the last few seconds (spec §5 layer 2). Addresses are held in memory only, for
/// <see cref="Thresholds.CompareWindow"/>, to tell senders apart.
/// </summary>
public sealed class MyDeviceTracker
{
    private readonly TimeProvider _time;
    private readonly Dictionary<ulong, Sender> _senders = new();
    private MyDeviceProfile? _profile;

    public MyDeviceTracker(TimeProvider? time = null) => _time = time ?? TimeProvider.System;

    /// <summary>The chosen AirPods, or null before the wizard.</summary>
    public MyDeviceProfile? Profile
    {
        get => _profile;
        set
        {
            _profile = value;
            _senders.Clear();
        }
    }

    /// <summary>Returns true when this broadcast should be treated as coming from my AirPods.</summary>
    public bool Accept(PodsAdvertisement advertisement, short rssi, ulong transientAddress)
    {
        if (_profile is null || !_profile.Matches(advertisement) || rssi < Thresholds.DisplayRssi)
        {
            return false;
        }

        var now = _time.GetUtcNow();
        Prune(now);

        // Smooth RSSI per sender so one noisy reading does not flip the choice.
        var smoothed = _senders.TryGetValue(transientAddress, out var known)
            ? (known.SmoothedRssi * 2 + rssi) / 3.0
            : rssi;
        _senders[transientAddress] = new Sender(smoothed, now);

        foreach (var (address, sender) in _senders)
        {
            if (address != transientAddress && sender.SmoothedRssi > smoothed + Thresholds.SameDeviceToleranceDb)
            {
                return false; // a clearly stronger matching sender is around: that one is mine
            }
        }

        return true;
    }

    private void Prune(DateTimeOffset now)
    {
        List<ulong>? stale = null;
        foreach (var (address, sender) in _senders)
        {
            if (now - sender.LastSeen > Thresholds.CompareWindow)
            {
                (stale ??= new()).Add(address);
            }
        }

        stale?.ForEach(a => _senders.Remove(a));
    }

    private readonly record struct Sender(double SmoothedRssi, DateTimeOffset LastSeen);
}

/// <summary>One choice in the wizard ("are these yours?").</summary>
public sealed record Candidate(ushort ModelCode, byte ColorCode, ModelInfo Model, PodsAdvertisement Latest, short BestRssi, DateTimeOffset LastSeen)
{
    public MyDeviceProfile ToProfile(string? connectedDeviceName = null) => new(ModelCode, ColorCode, connectedDeviceName);
}

/// <summary>Collects wizard candidates, grouped by model and color, nearest first (spec §2 wizard).</summary>
public sealed class CandidateCollector
{
    private readonly TimeProvider _time;
    private readonly Dictionary<(ushort, byte), Candidate> _byKey = new();

    public CandidateCollector(TimeProvider? time = null) => _time = time ?? TimeProvider.System;

    public void Offer(PodsAdvertisement advertisement, short rssi)
    {
        if (rssi < Thresholds.WizardRssi)
        {
            return;
        }

        var now = _time.GetUtcNow();
        var key = (advertisement.ModelCode, advertisement.ColorCode);
        var best = _byKey.TryGetValue(key, out var existing) && now - existing.LastSeen <= Thresholds.CandidateTtl
            ? Math.Max(existing.BestRssi, rssi)
            : rssi;
        _byKey[key] = new Candidate(advertisement.ModelCode, advertisement.ColorCode, advertisement.Model, advertisement, (short)best, now);
    }

    /// <summary>Candidates seen recently, strongest (nearest) first.</summary>
    public IReadOnlyList<Candidate> GetCandidates()
    {
        var now = _time.GetUtcNow();
        return _byKey.Values
            .Where(c => now - c.LastSeen <= Thresholds.CandidateTtl)
            .OrderByDescending(c => c.BestRssi)
            .ThenBy(c => c.ModelCode)
            .ToList();
    }

    public void Clear() => _byKey.Clear();
}
