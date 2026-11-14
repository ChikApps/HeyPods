// Copyright (C) 2026 ChikApps
// SPDX-License-Identifier: GPL-3.0-only
//
// Decisions of 2026-11-14: low battery notification, keyboard shortcut, "check for update now".

using Earbuds.Core.Ble;
using Earbuds.Core.Hotkeys;
using Earbuds.Core.Notifications;
using Earbuds.Core.Updates;
using Xunit;

namespace Earbuds.Core.Tests;

public class LowBatteryAlertTests
{
    private static PodsAdvertisement Pods(byte pods, byte flagsCase = 0x8F) =>
        ProximityPairingParser.Parse(ParserTests.Packet(0x1320, 0x2B, pods, flagsCase))!; // AirPods 3, left = lower nibble

    private readonly LowBatteryAlerts _a = new();

    [Fact]
    public void OncePerDrop()
    {
        Assert.Null(_a.Evaluate(Pods(0x55), true));                  // 50/50
        Assert.Null(_a.Evaluate(Pods(0x53), true));                  // left 30
        var alert = _a.Evaluate(Pods(0x52), true);                  // left 20 -> alert
        Assert.Equal(new LowBatteryItem(BatteryPart.Left, 20), Assert.Single(alert!.Items));
        Assert.Null(_a.Evaluate(Pods(0x52), true));                  // still 20: no repeat
        Assert.Null(_a.Evaluate(Pods(0x51), true));                  // 10: same drop, no repeat
        Assert.Null(_a.Evaluate(Pods(0x5F), true));                  // unknown: nothing changes
        Assert.Null(_a.Evaluate(Pods(0x51), true));
        Assert.Null(_a.Evaluate(Pods(0x58), true));                  // charged to 80: re-armed
        Assert.NotNull(_a.Evaluate(Pods(0x52), true));               // next drop alerts again
    }

    [Fact]
    public void BothPodsAtOnce_OneAlertTwoItems()
    {
        var alert = _a.Evaluate(Pods(0x21), true);
        Assert.Equal(2, alert!.Items.Count);
    }

    [Fact]
    public void Charging_NoAlert()
    {
        Assert.Null(_a.Evaluate(Pods(0x52, flagsCase: 0x1F), true)); // left charging (bit 0)
    }

    [Fact]
    public void SettingOff_NoAlert_AndNoLateAlertWhenTurnedOn()
    {
        Assert.Null(_a.Evaluate(Pods(0x52), false));
        Assert.Null(_a.Evaluate(Pods(0x52), true));
    }

    [Fact]
    public void Exactly20_Alerts_30_DoesNot()
    {
        Assert.Null(_a.Evaluate(Pods(0x33), true));
        Assert.NotNull(_a.Evaluate(Pods(0x32), true));
    }

    [Fact]
    public void Max_Headset()
    {
        var max = ProximityPairingParser.Parse(ParserTests.Packet(0x0A20, 0x20, 0x01, 0x80))!;
        Assert.Equal(BatteryPart.Headset, Assert.Single(_a.Evaluate(max, true)!.Items).Part);
    }
}

public class HotkeyTests
{
    [Theory]
    [InlineData("Ctrl+Alt+A", HotkeyModifiers.Ctrl | HotkeyModifiers.Alt, 'A', "Ctrl+Alt+A")]
    [InlineData(" alt + ctrl + a ", HotkeyModifiers.Ctrl | HotkeyModifiers.Alt, 'A', "Ctrl+Alt+A")]
    [InlineData("Ctrl+Shift+F5", HotkeyModifiers.Ctrl | HotkeyModifiers.Shift, 0x74, "Ctrl+Shift+F5")]
    [InlineData("Win+Alt+1", HotkeyModifiers.Win | HotkeyModifiers.Alt, '1', "Alt+Win+1")]
    [InlineData("Control+F12", HotkeyModifiers.Ctrl, 0x7B, "Ctrl+F12")]
    public void Valid(string text, HotkeyModifiers mods, int vk, string normalized)
    {
        Assert.True(HotkeyGesture.TryParse(text, out var g));
        Assert.Equal(new HotkeyGesture(mods, vk), g);
        Assert.Equal(normalized, g!.ToString());
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("A")]               // no modifier
    [InlineData("Shift+A")]         // Shift alone would steal typing
    [InlineData("Ctrl+Alt")]        // no key
    [InlineData("Ctrl+A+B")]        // two keys
    [InlineData("Ctrl+Ctrl+A")]
    [InlineData("Ctrl+F13")]
    [InlineData("Ctrl+F0")]
    [InlineData("Ctrl+F01")]
    [InlineData("Ctrl+Space")]
    [InlineData("Ctrl+א")]
    public void Invalid(string? text) => Assert.False(HotkeyGesture.TryParse(text, out _));

    [Fact]
    public void Default_IsCtrlAltA() =>
        Assert.Equal(HotkeyGesture.Default, HotkeyGesture.ParseOrDefault(HotkeyGesture.DefaultText));

    [Fact]
    public void Settings_InvalidHotkey_FallsBackToDefault()
    {
        var s = Settings.SettingsSerializer.Deserialize("{\"hotkey\": \"Shift+Q\", \"hotkeyEnabled\": false}");
        Assert.Equal("Ctrl+Alt+A", s.Hotkey);
        Assert.False(s.HotkeyEnabled);
    }
}

public class UpdateCheckTests
{
    private const string Repo = "ChikApps/test-app";

    private sealed class FakeSource : IReleaseSource
    {
        public Uri? Location { get; init; }
        public Exception? Throw { get; init; }
        public Uri? Asked { get; private set; }
        public int Calls { get; private set; }

        public Task<Uri?> GetLatestReleaseLocationAsync(Uri latestUrl, CancellationToken cancellationToken)
        {
            Calls++;
            Asked = latestUrl;
            return Throw is null ? Task.FromResult(Location) : Task.FromException<Uri?>(Throw);
        }
    }

    private static AppVersion V(string s) => AppVersion.TryParse(s, out var v) ? v! : throw new ArgumentException(s);

    [Theory]
    [InlineData("1.0.1", "1.0.0", 1)]
    [InlineData("v1.2", "1.2.0", 0)]
    [InlineData("1.10.0", "1.9.9", 1)]
    [InlineData("2.0.0-beta.1", "2.0.0", -1)]
    [InlineData("2.0.0-beta.2", "2.0.0-beta.1", 1)]
    [InlineData("1.0.0.1", "1.0.0", 1)]
    public void Compare(string a, string b, int sign) => Assert.Equal(sign, Math.Sign(V(a).CompareTo(V(b))));

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("v")]
    [InlineData("1..2")]
    [InlineData("1.2.3.4.5")]
    [InlineData("latest")]
    [InlineData("1.0-")]
    [InlineData("1.0-<script>")]
    [InlineData("9999999999.0")]
    [InlineData("1.0 ")]
    public void InvalidVersions(string? text)
    {
        if (text == "1.0 ")
        {
            Assert.True(AppVersion.TryParse(text, out _)); // surrounding spaces are trimmed
            return;
        }

        Assert.False(AppVersion.TryParse(text, out _));
    }

    [Fact]
    public async Task NotConfigured_NoRequestAtAll()
    {
        var source = new FakeSource();
        var r = await new UpdateChecker(source, "OWNER/REPO").CheckAsync(V("1.0.0")); // placeholder repository = not configured
        Assert.Equal(UpdateStatus.NotConfigured, r.Status);
        Assert.Equal(0, source.Calls);
        Assert.False(UpdateConfig.IsConfigured("OWNER/REPO"));
        Assert.True(UpdateConfig.IsConfigured());
        Assert.Equal("ChikApps/HeyPods", UpdateConfig.Repository);
    }

    [Fact]
    public async Task NewerRelease_Available_WithCleanPage()
    {
        var source = new FakeSource { Location = new Uri($"https://github.com/{Repo}/releases/tag/v1.2.0") };
        var r = await new UpdateChecker(source, Repo).CheckAsync(V("1.1.0"));
        Assert.Equal(UpdateStatus.UpdateAvailable, r.Status);
        Assert.Equal("1.2.0", r.Latest!.ToString());
        Assert.Equal($"https://github.com/{Repo}/releases/tag/v1.2.0", r.DownloadPage!.AbsoluteUri);
        Assert.Equal($"https://github.com/{Repo}/releases/latest", source.Asked!.AbsoluteUri);
    }

    [Theory]
    [InlineData("1.2.0")]
    [InlineData("1.3.0")]
    public async Task SameOrOlder_UpToDate(string current)
    {
        var source = new FakeSource { Location = new Uri($"https://github.com/{Repo}/releases/tag/1.2.0") };
        var r = await new UpdateChecker(source, Repo).CheckAsync(V(current));
        Assert.Equal(UpdateStatus.UpToDate, r.Status);
        Assert.Null(r.DownloadPage);
    }

    [Fact]
    public async Task NoRelease()
    {
        var r = await new UpdateChecker(new FakeSource(), Repo).CheckAsync(V("1.0.0"));
        Assert.Equal(UpdateStatus.NoRelease, r.Status);
    }

    [Fact]
    public async Task NetworkError_Failed_NoCrash()
    {
        var r = await new UpdateChecker(new FakeSource { Throw = new HttpRequestException("blocked") }, Repo).CheckAsync(V("1.0.0"));
        Assert.Equal(UpdateStatus.Failed, r.Status);
    }

    [Theory] // never send the browser to an address we did not expect
    [InlineData("http://github.com/ChikApps/test-app/releases/tag/v9.0.0")]
    [InlineData("https://evil.example/ChikApps/test-app/releases/tag/v9.0.0")]
    [InlineData("https://github.com.evil.example/ChikApps/test-app/releases/tag/v9.0.0")]
    [InlineData("https://github.com/Other/test-app/releases/tag/v9.0.0")]
    [InlineData("https://github.com/ChikApps/test-app/releases/tag/v9.0.0/../../x")]
    [InlineData("https://github.com/ChikApps/test-app/releases/tag/not-a-version")]
    [InlineData("https://user@github.com/ChikApps/test-app/releases/tag/v9.0.0")]
    [InlineData("https://github.com:8443/ChikApps/test-app/releases/tag/v9.0.0")]
    [InlineData("https://github.com/ChikApps/test-app/releases")]
    public async Task UnexpectedRedirect_Failed(string location)
    {
        var r = await new UpdateChecker(new FakeSource { Location = new Uri(location) }, Repo).CheckAsync(V("1.0.0"));
        Assert.Equal(UpdateStatus.Failed, r.Status);
    }

    [Theory]
    [InlineData("ChikApps/app", true)]
    [InlineData("OWNER/REPO", false)]
    [InlineData("ChikApps", false)]
    [InlineData("a/b/c", false)]
    [InlineData("../x", false)]
    [InlineData("Chik Apps/x", false)]
    public void RepositoryValidation(string repo, bool ok) => Assert.Equal(ok, UpdateConfig.IsConfigured(repo));
}
