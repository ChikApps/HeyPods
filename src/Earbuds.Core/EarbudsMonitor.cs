// Copyright (C) 2026 ChikApps
// SPDX-License-Identifier: GPL-3.0-only

using Earbuds.Core.Ble;
using Earbuds.Core.Models;
using Earbuds.Core.Notifications;
using Earbuds.Core.Platform;
using Earbuds.Core.Playback;
using Earbuds.Core.Settings;
using Earbuds.Core.Tracking;

namespace Earbuds.Core;

/// <summary>What the UI should show (spec §3 states).</summary>
public enum ViewState
{
    /// <summary>No AirPods chosen yet (first run, or wizard closed without a choice).</summary>
    NoDeviceChosen,
    /// <summary>Bluetooth is off.</summary>
    BluetoothOff,
    /// <summary>No Bluetooth adapter or no Bluetooth LE.</summary>
    NotSupported,
    /// <summary>Chosen, but never seen since start: "open the case near the computer".</summary>
    NotFound,
    /// <summary>Seen within the last 60 seconds.</summary>
    Live,
    /// <summary>Seen before, but not in the last 60 seconds: show last data in gray with "last seen X ago".</summary>
    Stale,
}

/// <summary>A value plus when it was last updated.</summary>
public sealed record TimedValue<T>(T Value, DateTimeOffset UpdatedAt);

/// <summary>Everything the UI needs to draw one frame.</summary>
public sealed record MonitorSnapshot(
    ViewState State,
    MyDeviceProfile? Profile,
    PodsAdvertisement? Latest,
    DateTimeOffset? LastSeen,
    TimedValue<int>? LastKnownCaseBattery,
    bool ConnectedAsAudio,
    RadioState Radio)
{
    /// <summary>Model of the chosen AirPods (from the profile, so it is known even before the first broadcast).</summary>
    public ModelInfo? Model => Profile is null ? null : ModelCatalog.Find(Profile.ModelCode);

    /// <summary>Red dot on the tray icon: a pod (or the headset) at or below 20% (spec §2).</summary>
    public bool IsLowBattery => Latest is { } a && State == ViewState.Live &&
        (IsLow(a.LeftBattery) || IsLow(a.RightBattery) || IsLow(a.HeadsetBattery));

    private static bool IsLow(int? percent) => percent is { } p && p <= Thresholds.LowBatteryPercent;
}

/// <summary>
/// The engine under the UI: receives broadcasts, decodes them, keeps only "my" AirPods, tracks state,
/// asks for the popup and drives pause/resume. All OS access goes through the interfaces in Platform/.
/// </summary>
public sealed class EarbudsMonitor : IDisposable
{
    private readonly IBleAdvertisementSource _source;
    private readonly IBluetoothRadio _radio;
    private readonly IConnectedAudioDevices _audio;
    private readonly IMediaSessions _media;
    private readonly bool _ownsAdapters;
    private readonly TimeProvider _time;
    private readonly MyDeviceTracker _tracker;
    private readonly CandidateCollector _candidates;
    private readonly AutoPauseController _autoPause;
    private readonly LowBatteryAlerts _lowBattery = new();
    private readonly object _lock = new();

    private AppSettings _settings;
    private PodsAdvertisement? _latest;
    private DateTimeOffset? _lastSeen;
    private TimedValue<int>? _caseBattery;
    private LidState _lastLid = LidState.Unknown;
    private bool _popupOpen;
    private bool _connectedAsAudio;
    private bool _wizardActive;
    private Task _playbackChain = Task.CompletedTask;
    private MonitorSnapshot? _lastPublished;
    private bool _disposed;

    public EarbudsMonitor(
        IBleAdvertisementSource source,
        IBluetoothRadio radio,
        IConnectedAudioDevices audio,
        IMediaSessions media,
        AppSettings settings,
        TimeProvider? time = null,
        bool ownsAdapters = false)
    {
        _source = source;
        _radio = radio;
        _audio = audio;
        _media = media;
        _ownsAdapters = ownsAdapters;
        _time = time ?? TimeProvider.System;
        _settings = settings;
        _tracker = new MyDeviceTracker(_time) { Profile = settings.ToProfile() };
        _candidates = new CandidateCollector(_time);
        _autoPause = new AutoPauseController(media, _time);

        _source.Received += OnReceived;
        _radio.StateChanged += OnRadioStateChanged;
        _audio.Changed += OnAudioDevicesChanged;
    }

    /// <summary>The UI state changed (data, state or connection).</summary>
    public event EventHandler<MonitorSnapshot>? SnapshotChanged;

    /// <summary>My case was opened nearby: show the popup.</summary>
    public event EventHandler<MonitorSnapshot>? PopupRequested;

    /// <summary>My case was closed: close the popup if it is open.</summary>
    public event EventHandler? PopupCloseRequested;

    /// <summary>A pod (or the headset) just dropped to 20% or below: show a Windows notification.</summary>
    public event EventHandler<LowBatteryAlert>? LowBattery;

    /// <summary>Settings changed (wizard done, forget). The UI saves them to disk.</summary>
    public event EventHandler<AppSettings>? SettingsChanged;

    /// <summary>Broadcasts with Apple data that did not decode (diagnostics only).</summary>
    public long IgnoredCount { get; private set; }

    public AppSettings Settings
    {
        get { lock (_lock) { return _settings; } }
    }

    /// <summary>Starts listening. Safe to call more than once.</summary>
    public async Task StartAsync()
    {
        await _radio.InitializeAsync().ConfigureAwait(false);
        await RefreshConnectedAsync().ConfigureAwait(false);
        if (_radio.State is RadioState.On or RadioState.Unknown)
        {
            _source.Start();
        }

        Publish();
    }

    /// <summary>
    /// Call about once a second from a UI timer: updates "seen X ago" states and restarts a scanner that
    /// stopped (e.g. after sleep), which covers spec state 14 without a separate power hook.
    /// </summary>
    public void Tick()
    {
        if (_radio.State is (RadioState.On or RadioState.Unknown) && _source.Status != ScannerStatus.Started)
        {
            _source.Start();
        }

        Publish();
    }

    /// <summary>Current snapshot.</summary>
    public MonitorSnapshot GetSnapshot()
    {
        lock (_lock)
        {
            return BuildSnapshot();
        }
    }

    // ---------- settings ----------

    public void UpdateSettings(Func<AppSettings, AppSettings> change)
    {
        AppSettings updated;
        lock (_lock)
        {
            var before = _settings;
            _settings = change(_settings);
            updated = _settings;
            if (before.MyDevice != updated.MyDevice)
            {
                ResetDeviceState(updated.ToProfile());
            }
        }

        SettingsChanged?.Invoke(this, updated);
        Publish();
    }

    // ---------- wizard ----------

    /// <summary>Wizard step 1: start collecting candidates.</summary>
    public void BeginWizard()
    {
        lock (_lock)
        {
            _candidates.Clear();
            _wizardActive = true;
        }
    }

    /// <summary>Wizard step 2: candidates seen recently, nearest first.</summary>
    public IReadOnlyList<Candidate> GetCandidates()
    {
        lock (_lock)
        {
            return _candidates.GetCandidates();
        }
    }

    /// <summary>Wizard "yes": save this candidate as my AirPods.</summary>
    public async Task CompleteWizardAsync(Candidate candidate)
    {
        var connected = await SafeGetConnectedAsync().ConfigureAwait(false);
        var productId = ModelCatalog.ToWindowsProductId(candidate.ModelCode);
        var sameModel = connected
            .Where(d => d.VendorId == ProximityPairingParser.AppleCompanyId && d.ProductId == productId)
            .ToList();
        var name = sameModel.Count == 1 ? sameModel[0].Name : null;

        lock (_lock)
        {
            _wizardActive = false;
        }

        UpdateSettings(s => s with { MyDevice = SavedDevice.From(candidate.ToProfile(name)) });
        await RefreshConnectedAsync().ConfigureAwait(false);
    }

    /// <summary>Wizard closed without a choice.</summary>
    public void CancelWizard()
    {
        lock (_lock)
        {
            _wizardActive = false;
            _candidates.Clear();
        }
    }

    /// <summary>"Forget my AirPods": back to first-run state.</summary>
    public void ForgetDevice() => UpdateSettings(s => s with { MyDevice = null });

    /// <summary>Waits until queued pause/resume work is done (tests, shutdown).</summary>
    public Task WhenPlaybackIdleAsync()
    {
        lock (_lock)
        {
            return _playbackChain;
        }
    }

    // ---------- connection (spec §5 layer 3) ----------

    /// <summary>
    /// True when Windows reports my AirPods as a connected audio device: Apple vendor ID, the product ID of
    /// my model, and the saved name if we have one. If Windows gives no IDs, the saved name alone decides.
    /// </summary>
    public static bool IsMyDeviceConnected(MyDeviceProfile? profile, IReadOnlyList<ConnectedAudioDevice> connected)
    {
        if (profile is null)
        {
            return false;
        }

        var productId = ModelCatalog.ToWindowsProductId(profile.ModelCode);
        foreach (var device in connected)
        {
            var nameMatches = profile.ConnectedDeviceName is null ||
                string.Equals(device.Name, profile.ConnectedDeviceName, StringComparison.OrdinalIgnoreCase);
            var idsKnown = device.VendorId != 0 || device.ProductId != 0;

            if (idsKnown)
            {
                if (device.VendorId == ProximityPairingParser.AppleCompanyId && device.ProductId == productId && nameMatches)
                {
                    return true;
                }
            }
            else if (profile.ConnectedDeviceName is not null && nameMatches)
            {
                return true;
            }
        }

        return false;
    }

    public void Dispose()
    {
        if (_disposed)
        {
            return;
        }

        _disposed = true;
        _source.Received -= OnReceived;
        _radio.StateChanged -= OnRadioStateChanged;
        _audio.Changed -= OnAudioDevicesChanged;
        _autoPause.Dispose();
        if (_ownsAdapters)
        {
            _source.Dispose();
            _radio.Dispose();
            _audio.Dispose();
            (_media as IDisposable)?.Dispose();
        }
    }

    // ---------- internals ----------

    private void OnReceived(object? sender, BleAdvertisement ble)
    {
        if (!ProximityPairingParser.TryParse(ble.AppleData.Span, out var advertisement) || advertisement is null)
        {
            lock (_lock)
            {
                IgnoredCount++;
            }

            return;
        }

        bool requestPopup = false, closePopup = false;
        LowBatteryAlert? lowBattery = null;
        lock (_lock)
        {
            if (_wizardActive)
            {
                _candidates.Offer(advertisement, ble.Rssi);
            }

            if (!_tracker.Accept(advertisement, ble.Rssi, ble.TransientAddress))
            {
                return;
            }

            var now = _time.GetUtcNow();
            _latest = advertisement;
            _lastSeen = now;
            if (advertisement.CaseBattery is { } caseBattery)
            {
                _caseBattery = new TimedValue<int>(caseBattery, now);
            }

            // Popup: only on a transition to "open", only when strong enough (spec §5), only if enabled.
            if (advertisement.Lid == LidState.Open && _lastLid != LidState.Open)
            {
                if (_settings.PopupOnCaseOpen && ble.Rssi >= Thresholds.PopupRssi && !_wizardActive)
                {
                    requestPopup = true;
                    _popupOpen = true;
                }
            }
            else if (advertisement.Lid is LidState.Closed or LidState.NotInCase && _popupOpen)
            {
                closePopup = true;
                _popupOpen = false;
            }

            if (advertisement.Lid != LidState.Unknown)
            {
                _lastLid = advertisement.Lid; // an untrustworthy frame does not change what we believe
            }

            lowBattery = _lowBattery.Evaluate(advertisement, _settings.LowBatteryAlert);

            var count = advertisement.InEarCount;
            var enabled = _settings.PauseOnRemove;
            var connected = _connectedAsAudio;
            _playbackChain = _playbackChain
                .ContinueWith(_ => _autoPause.OnWearReadingAsync(count, enabled, connected), TaskScheduler.Default)
                .Unwrap();
        }

        if (requestPopup)
        {
            PopupRequested?.Invoke(this, GetSnapshot());
        }

        if (closePopup)
        {
            PopupCloseRequested?.Invoke(this, EventArgs.Empty);
        }

        if (lowBattery is not null)
        {
            LowBattery?.Invoke(this, lowBattery);
        }

        Publish();
    }

    private void OnRadioStateChanged(object? sender, RadioState state)
    {
        if (state == RadioState.On)
        {
            _source.Start(); // Bluetooth turned back on: resume by itself (spec §3)
        }

        Publish();
    }

    private async void OnAudioDevicesChanged(object? sender, EventArgs e)
    {
        try
        {
            await RefreshConnectedAsync().ConfigureAwait(false);
        }
        catch
        {
            // async void event handler: never let an exception escape
        }
    }

    private async Task RefreshConnectedAsync()
    {
        var connected = await SafeGetConnectedAsync().ConfigureAwait(false);
        lock (_lock)
        {
            _connectedAsAudio = IsMyDeviceConnected(_tracker.Profile, connected);
        }

        Publish();
    }

    private async Task<IReadOnlyList<ConnectedAudioDevice>> SafeGetConnectedAsync()
    {
        try
        {
            return await _audio.GetConnectedAsync().ConfigureAwait(false);
        }
        catch
        {
            return Array.Empty<ConnectedAudioDevice>();
        }
    }

    private void ResetDeviceState(MyDeviceProfile? profile)
    {
        _tracker.Profile = profile;
        _latest = null;
        _lastSeen = null;
        _caseBattery = null;
        _lastLid = LidState.Unknown;
        _popupOpen = false;
        _connectedAsAudio = false;
        _autoPause.Reset();
        _lowBattery.Reset();
    }

    private MonitorSnapshot BuildSnapshot()
    {
        var radio = _radio.State;
        var profile = _tracker.Profile;
        ViewState state;
        if (profile is null)
        {
            state = ViewState.NoDeviceChosen;
        }
        else if (radio == RadioState.NotSupported)
        {
            state = ViewState.NotSupported;
        }
        else if (radio == RadioState.Off)
        {
            state = ViewState.BluetoothOff;
        }
        else if (_lastSeen is not { } seen)
        {
            state = ViewState.NotFound;
        }
        else
        {
            state = _time.GetUtcNow() - seen <= Thresholds.NotSeenAfter ? ViewState.Live : ViewState.Stale;
        }

        return new MonitorSnapshot(state, profile, _latest, _lastSeen, _caseBattery, _connectedAsAudio, radio);
    }

    private void Publish()
    {
        MonitorSnapshot snapshot;
        lock (_lock)
        {
            snapshot = BuildSnapshot();
            if (snapshot == _lastPublished)
            {
                return;
            }

            _lastPublished = snapshot;
        }

        SnapshotChanged?.Invoke(this, snapshot);
    }
}
