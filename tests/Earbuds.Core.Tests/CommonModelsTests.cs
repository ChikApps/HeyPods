// Copyright (C) 2026 ChikApps
// SPDX-License-Identifier: GPL-3.0-only
//
// Non-Pro models (AirPods 2, 3 and 4) are the main target, and the real test
// happens on them. Every scenario below runs for each of those models, end to end through the engine.
// Real broadcasts for these models are in ParserTests (CAPod Gen2Test:15, Gen3Test:15/49, Gen4Test:16,
// Gen4AncTest:15, plus the prefix-0x07 Gen4 frame Gen4Test:53).

using Earbuds.Core.Ble;
using Earbuds.Core.Models;
using Earbuds.Core.Platform;
using Earbuds.Core.Settings;
using Microsoft.Extensions.Time.Testing;
using Xunit;

namespace Earbuds.Core.Tests;

public class CommonModelsTests
{
    public static IEnumerable<object[]> CommonModels() => new[]
    {
        new object[] { (ushort)0x0F20, "AirPods 2" },
        new object[] { (ushort)0x1320, "AirPods 3" },
        new object[] { (ushort)0x1920, "AirPods 4" },
        new object[] { (ushort)0x1B20, "AirPods 4 (ANC)" },
    };

    // status bits: 5 = left primary (not flipped), 1/3 = in ear, 6 = sender in case, 4 = one in case, 2 = both in case
    [Theory]
    [MemberData(nameof(CommonModels))]
    public void Parser_AllEarCombinations_BothFlipDirections(ushort model, string name)
    {
        // (status, expected left, expected right)
        var cases = new (byte Status, EarState Left, EarState Right)[]
        {
            (0x2B, EarState.InEar, EarState.InEar),      // left primary, both in
            (0x0B, EarState.InEar, EarState.InEar),      // right primary, both in
            (0x22, EarState.InEar, EarState.OutOfEar),   // left primary: bit 1 = left
            (0x28, EarState.OutOfEar, EarState.InEar),
            (0x02, EarState.OutOfEar, EarState.InEar),   // right primary: bit 1 = right
            (0x08, EarState.InEar, EarState.OutOfEar),
            (0x20, EarState.OutOfEar, EarState.OutOfEar),
            (0x24, EarState.InCase, EarState.InCase),    // both in case
        };

        foreach (var (status, left, right) in cases)
        {
            var a = ProximityPairingParser.Parse(ParserTests.Packet(model, status, 0x76, 0x85, lid: 0x01))!;
            Assert.Equal(name, a.Model.Name);
            Assert.Equal(ModelFamily.Stem, a.Model.Family);
            Assert.Equal(PodsLayout.Dual, a.Layout);
            Assert.True((left, right) == (a.LeftEar, a.RightEar), $"{name} status 0x{status:X2}: got {a.LeftEar}/{a.RightEar}");
            // battery swaps with the primary side
            var leftPrimary = (status & 0x20) != 0;
            Assert.Equal(leftPrimary ? 60 : 70, a.LeftBattery);
            Assert.Equal(leftPrimary ? 70 : 60, a.RightBattery);
            Assert.Equal(50, a.CaseBattery);
        }
    }

    [Theory]
    [MemberData(nameof(CommonModels))]
    public async Task EndToEnd_Popup_Pause_Resume_LowBattery(ushort model, string name)
    {
        var ble = new FakeBleSource();
        var media = new FakeMediaSessions();
        var audio = new FakeAudioDevices();
        media.Sessions["spotify"] = MediaPlaybackStatus.Playing;
        audio.Devices.Add(new ConnectedAudioDevice(name, 0x004C, ModelCatalog.ToWindowsProductId(model)));
        using var m = new EarbudsMonitor(ble, new FakeRadio(), audio, media,
            new AppSettings { MyDevice = new SavedDevice(model, 0x00, null) }, new FakeTimeProvider());
        await m.StartAsync();
        Assert.True(m.GetSnapshot().ConnectedAsAudio, $"{name}: Windows product ID must match");

        var popups = 0;
        var lowAlerts = 0;
        m.PopupRequested += (_, _) => popups++;
        m.LowBattery += (_, _) => lowAlerts++;

        // 1. open the case near the PC (both pods in case, charging, sender in case, lid open)
        ble.Emit(ParserTests.Packet(model, 0x75, 0x88, 0x79, lid: 0x31), -50);
        Assert.Equal(1, popups);
        Assert.Equal(90, m.GetSnapshot().LastKnownCaseBattery!.Value);

        // 2. both pods in the ears, music playing
        ble.Emit(ParserTests.Packet(model, 0x2B, 0x88, 0x8F), -50);
        // 3. left pod out -> pause
        ble.Emit(ParserTests.Packet(model, 0x28, 0x88, 0x8F), -50);
        await m.WhenPlaybackIdleAsync();
        Assert.Equal(MediaPlaybackStatus.Paused, media.Sessions["spotify"]);
        // 4. back in -> resume
        ble.Emit(ParserTests.Packet(model, 0x2B, 0x88, 0x8F), -50);
        await m.WhenPlaybackIdleAsync();
        Assert.Equal(MediaPlaybackStatus.Playing, media.Sessions["spotify"]);

        // 5. user pauses by hand, takes a pod out and puts it back -> stays paused
        media.UserSets("spotify", MediaPlaybackStatus.Paused);
        ble.Emit(ParserTests.Packet(model, 0x28, 0x88, 0x8F), -50);
        ble.Emit(ParserTests.Packet(model, 0x2B, 0x88, 0x8F), -50);
        await m.WhenPlaybackIdleAsync();
        Assert.Equal(MediaPlaybackStatus.Paused, media.Sessions["spotify"]);

        // 6. battery drops to 20% -> one notification
        ble.Emit(ParserTests.Packet(model, 0x2B, 0x82, 0x8F), -50);
        ble.Emit(ParserTests.Packet(model, 0x2B, 0x82, 0x8F), -50);
        Assert.Equal(1, lowAlerts);
        Assert.True(m.GetSnapshot().IsLowBattery);

        Assert.Equal(new[] { "pause:spotify", "play:spotify" }, media.Calls);
    }

    [Theory]
    [MemberData(nameof(CommonModels))]
    public void SameModelOtherColor_IsNotMine(ushort model, string name)
    {
        var tracker = new Tracking.MyDeviceTracker(new FakeTimeProvider()) { Profile = new Tracking.MyDeviceProfile(model, 0x00) };
        Assert.False(tracker.Accept(ProximityPairingParser.Parse(ParserTests.Packet(model, 0x2B, 0x88, 0x8F, color: 0x01))!, -40, 1), name);
        Assert.False(tracker.Accept(ProximityPairingParser.Parse(ParserTests.Packet(0x1420, 0x2B, 0x88, 0x8F))!, -40, 1), name);
    }
}
