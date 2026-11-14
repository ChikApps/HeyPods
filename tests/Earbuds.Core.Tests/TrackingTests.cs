// Copyright (C) 2026 ChikApps
// SPDX-License-Identifier: GPL-3.0-only

using Earbuds.Core.Ble;
using Earbuds.Core.Tracking;
using Microsoft.Extensions.Time.Testing;
using Xunit;

namespace Earbuds.Core.Tests;

public class TrackingTests
{
    private static PodsAdvertisement Pods(ushort model = 0x1420, byte color = 0x00) =>
        ProximityPairingParser.Parse(ParserTests.Packet(model, 0x2B, 0x88, 0x8F, color: color))!;

    private static readonly MyDeviceProfile Mine = new(0x1420, 0x00);

    [Fact]
    public void NoProfile_NothingAccepted()
    {
        var t = new MyDeviceTracker(new FakeTimeProvider());
        Assert.False(t.Accept(Pods(), -40, 1));
    }

    [Fact]
    public void OtherModelOrColor_Rejected()
    {
        var t = new MyDeviceTracker(new FakeTimeProvider()) { Profile = Mine };
        Assert.False(t.Accept(Pods(model: 0x2420), -40, 1));
        Assert.False(t.Accept(Pods(color: 0x01), -40, 1));
        Assert.True(t.Accept(Pods(), -40, 1));
    }

    [Theory]
    [InlineData(-70, true)]
    [InlineData(-71, false)]
    [InlineData(-95, false)]
    public void DisplayThreshold(short rssi, bool accepted)
    {
        var t = new MyDeviceTracker(new FakeTimeProvider()) { Profile = Mine };
        Assert.Equal(accepted, t.Accept(Pods(), rssi, 1));
    }

    [Fact]
    public void SameModelAndColorNearby_StrongestWins()
    {
        var time = new FakeTimeProvider();
        var t = new MyDeviceTracker(time) { Profile = Mine };
        Assert.True(t.Accept(Pods(), -45, transientAddress: 1));   // mine, close
        Assert.False(t.Accept(Pods(), -65, transientAddress: 2));  // neighbour, same model and color, further away
        Assert.True(t.Accept(Pods(), -46, transientAddress: 3));   // my other pod (different address), about as strong
        Assert.False(t.Accept(Pods(), -66, transientAddress: 2));
    }

    [Fact]
    public void WeakerSender_AcceptedAfterStrongOneIsGone()
    {
        var time = new FakeTimeProvider();
        var t = new MyDeviceTracker(time) { Profile = Mine };
        Assert.True(t.Accept(Pods(), -45, 1));
        Assert.False(t.Accept(Pods(), -65, 2));
        time.Advance(Thresholds.CompareWindow + TimeSpan.FromSeconds(1));
        Assert.True(t.Accept(Pods(), -65, 2));
    }

    [Fact]
    public void ChangingProfile_ForgetsSenders()
    {
        var t = new MyDeviceTracker(new FakeTimeProvider()) { Profile = Mine };
        Assert.True(t.Accept(Pods(), -40, 1));
        t.Profile = Mine with { };
        Assert.True(t.Accept(Pods(), -65, 2));
    }

    [Fact]
    public void Candidates_NearestFirst_WeakIgnored_Expire()
    {
        var time = new FakeTimeProvider();
        var c = new CandidateCollector(time);
        c.Offer(Pods(model: 0x0E20), -75);
        c.Offer(Pods(model: 0x1420), -50);
        c.Offer(Pods(model: 0x1420, color: 0x01), -60);
        c.Offer(Pods(model: 0x2720), -85); // below the wizard threshold
        var list = c.GetCandidates();
        Assert.Equal(new ushort[] { 0x1420, 0x1420, 0x0E20 }, list.Select(x => x.ModelCode));
        Assert.Equal((byte)0x00, list[0].ColorCode);
        Assert.Equal(new MyDeviceProfile(0x1420, 0x00, "x"), list[0].ToProfile("x"));

        time.Advance(Thresholds.CandidateTtl + TimeSpan.FromSeconds(1));
        Assert.Empty(c.GetCandidates());
    }

    [Fact]
    public void Candidates_KeepBestRssiOfAGroup()
    {
        var c = new CandidateCollector(new FakeTimeProvider());
        c.Offer(Pods(), -50);
        c.Offer(Pods(), -70);
        Assert.Equal(-50, c.GetCandidates().Single().BestRssi);
    }
}
