// Copyright (C) 2026 ChikApps
// SPDX-License-Identifier: GPL-3.0-only
//
// The whole engine with fake Windows parts (spec §3 states).

using Earbuds.Core.Platform;
using Earbuds.Core.Settings;
using Earbuds.Core.Tracking;
using Microsoft.Extensions.Time.Testing;
using Xunit;

namespace Earbuds.Core.Tests;

public class MonitorTests : IDisposable
{
    // AirPods Pro 2, white
    private const string BothInEar = "07 19 01 14 20 2B 9A 8F 01 00 04 0F 26 1A C4 2B FA 2F B9 B6 08 CD 60 CB DF 75 AB";
    private const string InCaseOpen = "07 19 01 14 20 55 88 F9 51 00 04 20 50 03 CA D5 C9 AC 0F FA 84 78 94 5A 4D DF F5";

    private readonly FakeBleSource _ble = new();
    private readonly FakeRadio _radio = new();
    private readonly FakeAudioDevices _audio = new();
    private readonly FakeMediaSessions _media = new();
    private readonly FakeTimeProvider _time = new();
    private EarbudsMonitor _m = null!;

    private static readonly ConnectedAudioDevice MyPro2 = new("AirPods Pro", 0x004C, 0x2014);

    private EarbudsMonitor Create(AppSettings? settings = null)
    {
        _m = new EarbudsMonitor(_ble, _radio, _audio, _media,
            settings ?? new AppSettings { MyDevice = new SavedDevice(0x1420, 0x00, null) }, _time);
        return _m;
    }

    private static byte[] InEarPacket(byte status) => ParserTests.Packet(0x1420, status, 0x99, 0x8F, lid: 0x01);

    public void Dispose() => _m?.Dispose();

    [Fact]
    public async Task FirstRun_NoDeviceChosen()
    {
        await Create(new AppSettings()).StartAsync();
        Assert.Equal(ViewState.NoDeviceChosen, _m.GetSnapshot().State);
        _ble.Emit(BothInEar, -40);
        Assert.Null(_m.GetSnapshot().Latest); // without a choice nothing is shown
    }

    [Fact]
    public async Task Chosen_NotSeen_ThenLive_ThenStaleAfter60s()
    {
        await Create().StartAsync();
        Assert.Equal(1, _ble.StartCalls);
        Assert.Equal(ViewState.NotFound, _m.GetSnapshot().State);

        _ble.Emit(BothInEar, -50);
        var s = _m.GetSnapshot();
        Assert.Equal(ViewState.Live, s.State);
        Assert.Equal("AirPods Pro 2", s.Model!.Name);
        Assert.Equal(100, s.Latest!.LeftBattery);

        _time.Advance(TimeSpan.FromSeconds(61));
        _m.Tick();
        Assert.Equal(ViewState.Stale, _m.GetSnapshot().State);
        Assert.Equal(100, _m.GetSnapshot().Latest!.LeftBattery); // last data kept for the gray view
    }

    [Fact]
    public async Task BluetoothOff_Then_On_RestartsScanner()
    {
        await Create().StartAsync();
        _radio.Set(RadioState.Off);
        Assert.Equal(ViewState.BluetoothOff, _m.GetSnapshot().State);
        _ble.Status = ScannerStatus.Failed;
        _radio.Set(RadioState.On);
        Assert.Equal(2, _ble.StartCalls);
        Assert.Equal(ViewState.NotFound, _m.GetSnapshot().State);
    }

    [Fact]
    public async Task NoBluetoothLe_NotSupported_NoScan()
    {
        _radio.Set(RadioState.NotSupported);
        await Create().StartAsync();
        Assert.Equal(ViewState.NotSupported, _m.GetSnapshot().State);
        Assert.Equal(0, _ble.StartCalls);
    }

    [Fact] // spec state 14: after sleep the watcher is stopped; Tick restarts it
    public async Task Tick_RestartsStoppedScanner()
    {
        await Create().StartAsync();
        _ble.Status = ScannerStatus.Failed;
        _m.Tick();
        Assert.Equal(2, _ble.StartCalls);
    }

    [Fact]
    public async Task MalformedAndForeign_Ignored()
    {
        await Create().StartAsync();
        _ble.Emit(new byte[] { 0x07, 0x19, 0x01 }, -40);
        _ble.Emit(new byte[] { 0x10, 0x05, 1, 2, 3, 4, 5 }, -40);
        _ble.Emit(ParserTests.Packet(0x0E20, 0x2B, 0x99, 0x8F), -40); // someone else's AirPods Pro
        Assert.Equal(2, _m.IgnoredCount);
        Assert.Equal(ViewState.NotFound, _m.GetSnapshot().State);
    }

    [Fact]
    public async Task CaseBattery_KeptWhenCaseStopsReporting()
    {
        await Create().StartAsync();
        _ble.Emit(InCaseOpen, -40);
        Assert.Equal(90, _m.GetSnapshot().LastKnownCaseBattery!.Value);
        _time.Advance(TimeSpan.FromMinutes(3));
        _ble.Emit(BothInEar, -40); // case nibble F
        var s = _m.GetSnapshot();
        Assert.Null(s.Latest!.CaseBattery);
        Assert.Equal(90, s.LastKnownCaseBattery!.Value);
        Assert.Equal(TimeSpan.FromMinutes(3), _time.GetUtcNow() - s.LastKnownCaseBattery.UpdatedAt);
    }

    [Fact]
    public async Task Popup_OnOpenNearby_OnceAndClosesWithLid()
    {
        await Create().StartAsync();
        var opened = 0;
        var closed = 0;
        _m.PopupRequested += (_, _) => opened++;
        _m.PopupCloseRequested += (_, _) => closed++;

        _ble.Emit(InCaseOpen, -55);
        _ble.Emit(InCaseOpen, -55);
        Assert.Equal(1, opened);

        _ble.Emit(ParserTests.Packet(0x1420, 0x55, 0x88, 0xF9, lid: 0x59), -55); // lid bit 3 set: closed
        Assert.Equal(1, closed);

        _ble.Emit(InCaseOpen, -55);
        Assert.Equal(2, opened);
    }

    [Fact]
    public async Task Popup_NotForCaseOpenedAtNeighbouringDesk()
    {
        await Create().StartAsync();
        var opened = 0;
        _m.PopupRequested += (_, _) => opened++;
        _ble.Emit(InCaseOpen, -65); // shown (>= -70) but too weak for the popup (< -60)
        Assert.Equal(0, opened);
        Assert.Equal(ViewState.Live, _m.GetSnapshot().State);
    }

    [Fact]
    public async Task Popup_RespectsSetting()
    {
        await Create(new AppSettings { PopupOnCaseOpen = false, MyDevice = new SavedDevice(0x1420, 0, null) }).StartAsync();
        var opened = 0;
        _m.PopupRequested += (_, _) => opened++;
        _ble.Emit(InCaseOpen, -40);
        Assert.Equal(0, opened);
    }

    [Fact]
    public async Task AutoPause_EndToEnd_OnlyWhenConnected()
    {
        _media.Sessions["chrome"] = MediaPlaybackStatus.Playing;
        _audio.Devices.Add(MyPro2);
        await Create().StartAsync();
        Assert.True(_m.GetSnapshot().ConnectedAsAudio);

        _ble.Emit(InEarPacket(0x2B), -45); // both in ear
        _ble.Emit(InEarPacket(0x29), -45); // bit 1 cleared: left out
        await _m.WhenPlaybackIdleAsync();
        Assert.Equal(MediaPlaybackStatus.Paused, _media.Sessions["chrome"]);

        _ble.Emit(InEarPacket(0x2B), -45);
        await _m.WhenPlaybackIdleAsync();
        Assert.Equal(MediaPlaybackStatus.Playing, _media.Sessions["chrome"]);
    }

    [Fact] // spec §5 layer 3: my AirPods only broadcasting, not connected -> no pause
    public async Task AutoPause_NotConnected_NoPause()
    {
        _media.Sessions["chrome"] = MediaPlaybackStatus.Playing;
        await Create().StartAsync();
        Assert.False(_m.GetSnapshot().ConnectedAsAudio);
        _ble.Emit(InEarPacket(0x2B), -45);
        _ble.Emit(InEarPacket(0x29), -45);
        await _m.WhenPlaybackIdleAsync();
        Assert.Equal(MediaPlaybackStatus.Playing, _media.Sessions["chrome"]);
    }

    [Fact]
    public async Task ConnectionChange_IsPickedUp()
    {
        await Create().StartAsync();
        _audio.Devices.Add(MyPro2);
        _audio.RaiseChanged();
        await Task.Delay(50);
        Assert.True(_m.GetSnapshot().ConnectedAsAudio);
    }

    [Theory]
    [InlineData("AirPods Pro", 0x004C, 0x2014, null, true)]
    [InlineData("AirPods Pro", 0x004C, 0x2024, null, false)]                // other model connected
    [InlineData("Headphones", 0x0001, 0x2014, null, false)]                 // not Apple
    [InlineData("Dani's AirPods", 0x004C, 0x2014, "Dani's AirPods", true)]
    [InlineData("Other AirPods", 0x004C, 0x2014, "Dani's AirPods", false)]  // same model, another pair
    [InlineData("dani's airpods", 0, 0, "Dani's AirPods", true)]            // Windows gave no IDs: name decides
    [InlineData("Some speaker", 0, 0, null, false)]
    public void IsMyDeviceConnected(string name, int vendor, int product, string? savedName, bool expected)
    {
        var profile = new MyDeviceProfile(0x1420, 0x00, savedName);
        var list = new[] { new ConnectedAudioDevice(name, (ushort)vendor, (ushort)product) };
        Assert.Equal(expected, EarbudsMonitor.IsMyDeviceConnected(profile, list));
    }

    [Fact]
    public async Task Wizard_ListsCandidates_SavesChoice_WithConnectedName()
    {
        _audio.Devices.Add(new ConnectedAudioDevice("Dani's AirPods", 0x004C, 0x2014));
        await Create(new AppSettings()).StartAsync();
        AppSettings? saved = null;
        _m.SettingsChanged += (_, s) => saved = s;
        var popups = 0;
        _m.PopupRequested += (_, _) => popups++;

        _m.BeginWizard();
        _ble.Emit(ParserTests.Packet(0x0E20, 0x55, 0x88, 0xF9), -70, address: 9);
        _ble.Emit(InCaseOpen, -45, address: 1);
        var candidates = _m.GetCandidates();
        Assert.Equal(2, candidates.Count);
        Assert.Equal(0x1420, candidates[0].ModelCode);

        await _m.CompleteWizardAsync(candidates[0]);
        Assert.Equal(new SavedDevice(0x1420, 0x00, "Dani's AirPods"), saved!.MyDevice);
        Assert.True(_m.GetSnapshot().ConnectedAsAudio);
        Assert.Equal(0, popups); // no popup while the wizard itself is open

        _ble.Emit(BothInEar, -45);
        Assert.Equal(ViewState.Live, _m.GetSnapshot().State);
    }

    [Fact]
    public async Task Forget_BackToFirstRun()
    {
        await Create().StartAsync();
        _ble.Emit(BothInEar, -45);
        _m.ForgetDevice();
        var s = _m.GetSnapshot();
        Assert.Equal(ViewState.NoDeviceChosen, s.State);
        Assert.Null(s.Latest);
        Assert.Null(_m.Settings.MyDevice);
    }

    [Fact]
    public async Task LowBattery_Flag()
    {
        await Create().StartAsync();
        _ble.Emit(ParserTests.Packet(0x1420, 0x2B, 0x92, 0x8F), -45); // left 20%
        Assert.True(_m.GetSnapshot().IsLowBattery);
        _ble.Emit(ParserTests.Packet(0x1420, 0x2B, 0x93, 0x8F), -45); // left 30%
        Assert.False(_m.GetSnapshot().IsLowBattery);
    }

    [Fact]
    public async Task Max_SingleBattery_EndToEnd()
    {
        await Create(new AppSettings { MyDevice = new SavedDevice(0x0A20, 0x0F, null) }).StartAsync();
        _ble.Emit("07 19 01 0A 20 62 04 80 01 0F 40 0D 70 50 16 F2 40 83 16 BF 10 16 34 9B 74 84 E8", -45);
        var s = _m.GetSnapshot();
        Assert.Equal(ViewState.Live, s.State);
        Assert.Equal(40, s.Latest!.HeadsetBattery);
        Assert.Null(s.LastKnownCaseBattery);
    }
}
