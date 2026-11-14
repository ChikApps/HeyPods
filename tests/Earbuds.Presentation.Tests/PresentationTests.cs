// Copyright (C) 2026 ChikApps
// SPDX-License-Identifier: GPL-3.0-only

using System.Text.RegularExpressions;
using Earbuds.Core;
using Earbuds.Core.Ble;
using Earbuds.Core.Notifications;
using Earbuds.Core.Platform;
using Earbuds.Core.Tracking;
using Earbuds.Core.Updates;
using Xunit;

namespace Earbuds.Presentation.Tests;

public class RingTests
{
    [Fact] // the worked example in מפרט-לדני.md §4א: popup, 70% -> (3.37, 38.65), large arc
    public void SpecExample_70Percent()
    {
        var r = RingGeometry.Compute(70, 60, 4);
        Assert.False(r.Empty);
        Assert.False(r.Full);
        Assert.Equal(30, r.CenterX);
        Assert.Equal(28, r.Radius);
        Assert.Equal((30, 2), (r.StartX, r.StartY)); // 12 o'clock
        Assert.Equal(3.37, Math.Round(r.EndX, 2));
        Assert.Equal(38.65, Math.Round(r.EndY, 2));
        Assert.True(r.IsLargeArc);
    }

    [Theory]
    [InlineData(25, 58, 30)] // 3 o'clock: clockwise means to the RIGHT of the top (not mirrored)
    [InlineData(50, 30, 58)] // 6 o'clock
    [InlineData(75, 2, 30)]  // 9 o'clock
    public void FillsClockwiseFromTop(double p, double x, double y)
    {
        var r = RingGeometry.Compute(p, 60, 4);
        Assert.Equal(x, Math.Round(r.EndX, 6));
        Assert.Equal(y, Math.Round(r.EndY, 6));
        Assert.Equal(p > 50, r.IsLargeArc);
    }

    [Fact]
    public void Full_And_Empty()
    {
        Assert.True(RingGeometry.Compute(100, 56, 4).Full);
        Assert.True(RingGeometry.Compute(120, 56, 4).Full);
        Assert.True(RingGeometry.Compute(0, 56, 4).Empty);   // no dot at 0%
        Assert.True(RingGeometry.Compute(null, 56, 4).Empty); // "--"
        Assert.True(RingGeometry.Compute(double.NaN, 56, 4).Empty);
        Assert.Equal(26, RingGeometry.Compute(100, 56, 4).Radius); // r = (D - S) / 2, the ring never leaves D
    }

    [Fact]
    public void HundredPercent_SmallerText()
    {
        Assert.Equal(12, RingSize.Popup.FontSizeFor(100), 6);
        Assert.Equal(11.2, RingSize.Main.FontSizeFor(100), 6);
        Assert.Equal(15, RingSize.Popup.FontSizeFor(90));
        Assert.Equal(8, RingSize.Popup.BoltWidthFor(100));
        Assert.Equal(9, RingSize.Popup.BoltWidthFor(null));
    }

    [Fact]
    public void Ease_IsMonotonic_FromZeroToOne()
    {
        Assert.Equal(0, RingGeometry.Ease(0));
        Assert.Equal(1, RingGeometry.Ease(1));
        var last = 0.0;
        for (var t = 0.01; t < 1; t += 0.01)
        {
            var v = RingGeometry.Ease(t);
            Assert.InRange(v, last, 1.0000001);
            last = v;
        }

        Assert.InRange(RingGeometry.Ease(0.5), 0.85, 0.99); // fast start, slow end
    }
}

public class FormatTests
{
    [Fact]
    public void Percent_AlwaysNumberThenSign()
    {
        Assert.Equal("80%", Format.Percent(80));
        Assert.Equal("--", Format.Percent(null));
        Assert.Equal("\u200E100%\u200E", Format.PercentInText(100));
    }

    [Theory]
    [InlineData(0, "עודכן עכשיו")]
    [InlineData(4, "עודכן עכשיו")]
    [InlineData(5, "עודכן לפני 5 שניות")]
    [InlineData(59, "עודכן לפני 59 שניות")]
    [InlineData(60, "עודכן לפני דקה")]
    [InlineData(119, "עודכן לפני דקה")]
    [InlineData(120, "עודכן לפני 2 דקות")]
    [InlineData(3600, "עודכן לפני שעה")]
    [InlineData(7200, "עודכן לפני שעתיים")]
    [InlineData(5 * 3600, "עודכן לפני 5 שעות")]
    [InlineData(-10, "עודכן עכשיו")]
    public void Updated(int seconds, string expected) => Assert.Equal(expected, Format.Updated(TimeSpan.FromSeconds(seconds)));

    [Theory]
    [InlineData(61, "נראו לאחרונה לפני דקה")]
    [InlineData(12 * 60, "נראו לאחרונה לפני 12 דקות")]
    [InlineData(3600, "נראו לאחרונה לפני שעה")]
    [InlineData(7200, "נראו לאחרונה לפני שעתיים")]
    [InlineData(3 * 3600 + 5, "נראו לאחרונה לפני 3 שעות")]
    public void LastSeen(int seconds, string expected) => Assert.Equal(expected, Format.LastSeen(TimeSpan.FromSeconds(seconds)));
}

public class PanelTests
{
    [Fact] // design 03: AirPods 3, left in case charging, right in ear
    public void Live_MainWindow()
    {
        var p = PanelBuilder.Build(H.Snap(ViewState.Live, H.Pods(lc: true)), H.Now);
        Assert.Equal("AirPods 3", p.Title);
        Assert.True(p.TitleIsLatin);
        Assert.Equal(PanelBody.Gauges, p.Body);
        Assert.Equal(new[] { "שמאל", "קופסה", "ימין" }, p.Gauges.Select(g => g.Label)); // physical order
        Assert.Equal(new int?[] { 80, 50, 70 }, p.Gauges.Select(g => g.Level));
        Assert.True(p.Gauges[0].Charging);
        Assert.Equal(PodSpot.InCase, p.Gauges[0].Spot);
        Assert.Equal(PodSpot.InEar, p.Gauges[2].Spot);
        Assert.Equal("באוזן", p.Gauges[2].SpotText);
        Assert.Equal(new ArtModel(ArtFamily.Short, false, true, LedState.Green), p.Art);
        Assert.Equal("עודכן עכשיו", p.BarText);
        Assert.False(p.ShowNotConnected);
        Assert.False(p.ShowProHint);
    }

    [Fact]
    public void Popup_NoPills_NoBar()
    {
        var p = PanelBuilder.Build(H.Snap(ViewState.Live, H.Pods()), H.Now, popup: true, pauseEnabled: true);
        Assert.All(p.Gauges, g => Assert.Equal(PodSpot.None, g.Spot));
        Assert.Equal(string.Empty, p.BarText);
        Assert.False(p.ShowNotConnected);
    }

    [Fact] // design 05: 20% and below = red, case amber light
    public void Low()
    {
        var p = PanelBuilder.Build(H.Snap(ViewState.Live, H.Pods(left: 10, right: 20, kase: 20)), H.Now);
        Assert.Equal(GaugeTone.Low, p.Gauges[0].Tone);
        Assert.Equal(GaugeTone.Low, p.Gauges[2].Tone);
        Assert.Equal(LedState.Amber, p.Art.Led);
        Assert.Equal(GaugeTone.Normal, PanelBuilder.Build(H.Snap(ViewState.Live, H.Pods(left: 30)), H.Now).Gauges[0].Tone);
    }

    [Fact] // design 06: unknown value = "--", empty ring, no charging bolt
    public void Unknown()
    {
        var p = PanelBuilder.Build(H.Snap(ViewState.Live, H.Pods(left: null, lc: true, le: EarState.OutOfEar)), H.Now);
        Assert.Equal(GaugeTone.Unknown, p.Gauges[0].Tone);
        Assert.Equal("--", p.Gauges[0].LevelText);
        Assert.False(p.Gauges[0].Charging);
        Assert.Equal("בחוץ", p.Gauges[0].SpotText);
        Assert.True(p.Art.LeftOut);
        Assert.Contains("לא ידוע", p.Gauges[0].Accessible);
    }

    [Fact] // design 07: case closed -> last value gray + "updated X ago", light off
    public void CaseStale()
    {
        var snap = H.Snap(ViewState.Live, H.Pods(kase: null, le: EarState.InEar),
            kase: new TimedValue<int>(50, H.Now - TimeSpan.FromMinutes(12)));
        var p = PanelBuilder.Build(snap, H.Now);
        Assert.Equal(50, p.Gauges[1].Level);
        Assert.Equal(GaugeTone.Stale, p.Gauges[1].Tone);
        Assert.Equal("עודכן לפני 12 דקות", p.Gauges[1].Note);
        Assert.Equal(LedState.Off, p.Art.Led);
        Assert.Equal(GaugeTone.Normal, p.Gauges[0].Tone);
    }

    [Fact] // design 08: not around -> everything gray, bar "last seen"
    public void Stale()
    {
        var p = PanelBuilder.Build(H.Snap(ViewState.Stale, H.Pods(), seenAgo: TimeSpan.FromMinutes(12)), H.Now);
        Assert.All(p.Gauges, g => Assert.Equal(GaugeTone.Stale, g.Tone));
        Assert.All(p.Gauges, g => Assert.Null(g.Note));
        Assert.Equal("נראו לאחרונה לפני 12 דקות", p.BarText);
        Assert.Equal(LedState.Off, p.Art.Led);
    }

    [Fact] // design 10: not connected -> info row (only when auto-pause is on, and not in the popup)
    public void NotConnected()
    {
        var snap = H.Snap(ViewState.Live, H.Pods(), connected: false);
        Assert.True(PanelBuilder.Build(snap, H.Now).ShowNotConnected);
        Assert.False(PanelBuilder.Build(snap, H.Now, pauseEnabled: false).ShowNotConnected);
        Assert.False(PanelBuilder.Build(snap, H.Now, popup: true).ShowNotConnected);
    }

    [Fact]
    public void States_WithoutData()
    {
        Assert.Equal(PanelBody.NoChoice, PanelBuilder.Build(H.Snap(ViewState.NoDeviceChosen, chosen: false), H.Now).Body);
        Assert.Equal(AppInfo.Name, PanelBuilder.Build(H.Snap(ViewState.NoDeviceChosen, chosen: false), H.Now).Title);
        var nf = PanelBuilder.Build(H.Snap(ViewState.NotFound), H.Now);
        Assert.Equal(PanelBody.NotFound, nf.Body);
        Assert.Equal("AirPods 3", nf.Title);
        Assert.Equal(string.Empty, nf.BarText);
        Assert.Equal(PanelBody.BluetoothOff, PanelBuilder.Build(H.Snap(ViewState.BluetoothOff, radio: RadioState.Off), H.Now).Body);
        var noble = PanelBuilder.Build(H.Snap(ViewState.NotSupported, radio: RadioState.NotSupported), H.Now);
        Assert.Equal(PanelBody.NotSupported, noble.Body);
        Assert.Equal(AppInfo.Name, noble.Title);
    }

    [Fact] // design 14: unknown model -> Hebrew title, generic drawing, no light
    public void UnknownModel()
    {
        var p = PanelBuilder.Build(H.Snap(ViewState.Live, H.Pods(model: 0x9920), model: 0x9920), H.Now);
        Assert.Equal("אוזניות אפל (דגם לא מוכר)", p.Title);
        Assert.False(p.TitleIsLatin);
        Assert.Equal(ArtFamily.Generic, p.Art.Family);
        Assert.Equal(LedState.Off, p.Art.Led);
        Assert.Equal(3, p.Gauges.Count);
    }

    [Fact] // 2026-11-14: Pro hint only on Pro models
    public void ProHint()
    {
        Assert.True(PanelBuilder.Build(H.Snap(ViewState.Live, H.Pods(model: 0x1420), model: 0x1420), H.Now).ShowProHint);
        Assert.False(PanelBuilder.Build(H.Snap(ViewState.Live, H.Pods(model: 0x1420), model: 0x1420), H.Now, popup: true).ShowProHint);
    }

    [Fact] // design: Max = one gauge "סוללה", no pills, Max drawing
    public void Max()
    {
        var a = H.Pods(model: 0x0A20, left: null, right: null, kase: null, headset: 60, headsetCharging: true);
        var p = PanelBuilder.Build(H.Snap(ViewState.Live, a, model: 0x0A20), H.Now);
        Assert.Single(p.Gauges);
        Assert.Equal("סוללה", p.Gauges[0].Label);
        Assert.Equal(60, p.Gauges[0].Level);
        Assert.True(p.Gauges[0].Charging);
        Assert.Equal(PodSpot.None, p.Gauges[0].Spot);
        Assert.Equal(ArtFamily.Max, p.Art.Family);
    }

    [Theory]
    [InlineData((ushort)0x0220, ArtFamily.Long)]
    [InlineData((ushort)0x0F20, ArtFamily.Long)]
    [InlineData((ushort)0x1320, ArtFamily.Short)]
    [InlineData((ushort)0x1920, ArtFamily.Short)]
    [InlineData((ushort)0x1B20, ArtFamily.Short)]
    [InlineData((ushort)0x3620, ArtFamily.Short)]
    [InlineData((ushort)0x0E20, ArtFamily.Pro)]
    [InlineData((ushort)0x2720, ArtFamily.Pro)]
    [InlineData((ushort)0x2D20, ArtFamily.Max)]
    [InlineData((ushort)0x1234, ArtFamily.Generic)]
    public void Families(ushort code, ArtFamily family) =>
        Assert.Equal(family, PanelBuilder.FamilyOf(Core.Models.ModelCatalog.Find(code)));
}

public class TrayTests
{
    [Fact]
    public void Live_Tooltip_InOrder_WithCharging()
    {
        var t = TrayBuilder.Build(H.Snap(ViewState.Live, H.Pods(kase: 50, cc: true)), H.Now);
        Assert.Equal(TrayLook.Ok, t.Look);
        Assert.Equal(70, t.FillPercent); // the lower pod
        Assert.Equal("\u200Fשמאל \u200E80%\u200E · ימין \u200E70%\u200E · קופסה \u200E50%\u200E בטעינה", t.Tooltip);
        Assert.DoesNotMatch(new Regex("%[0-9]"), t.Tooltip);
    }

    [Fact]
    public void States()
    {
        Assert.Equal(TrayLook.Low, TrayBuilder.Build(H.Snap(ViewState.Live, H.Pods(left: 20)), H.Now).Look);
        Assert.Equal(20, TrayBuilder.Build(H.Snap(ViewState.Live, H.Pods(left: 20)), H.Now).FillPercent);
        var bt = TrayBuilder.Build(H.Snap(ViewState.BluetoothOff, radio: RadioState.Off), H.Now);
        Assert.Equal(TrayLook.BluetoothOff, bt.Look);
        Assert.EndsWith(": הבלוטות' כבוי", bt.Tooltip);
        Assert.Equal(TrayLook.Off, TrayBuilder.Build(H.Snap(ViewState.NotFound), H.Now).Look);
        Assert.EndsWith("לא מצאנו את האוזניות", TrayBuilder.Build(H.Snap(ViewState.NotFound), H.Now).Tooltip);
        Assert.EndsWith("עוד לא בחרת אוזניות", TrayBuilder.Build(H.Snap(ViewState.NoDeviceChosen, chosen: false), H.Now).Tooltip);
        var stale = TrayBuilder.Build(H.Snap(ViewState.Stale, H.Pods(), seenAgo: TimeSpan.FromMinutes(5)), H.Now);
        Assert.Equal(TrayLook.Off, stale.Look);
        Assert.EndsWith("נראו לאחרונה לפני 5 דקות", stale.Tooltip);
        Assert.Equal(TrayBuilder.DefaultFill, stale.FillPercent);
    }

    [Fact]
    public void Max_And_UnknownValues()
    {
        var max = TrayBuilder.Build(H.Snap(ViewState.Live, H.Pods(model: 0x0A20, left: null, right: null, kase: null, headset: 40), model: 0x0A20), H.Now);
        Assert.Equal("\u200Fסוללה \u200E40%\u200E", max.Tooltip);
        Assert.Equal(40, max.FillPercent);
        var unknown = TrayBuilder.Build(H.Snap(ViewState.Live, H.Pods(left: null, right: null)), H.Now);
        Assert.Equal(TrayBuilder.DefaultFill, unknown.FillPercent);
        Assert.Contains("שמאל \u200E--\u200E", unknown.Tooltip);
    }

    [Fact]
    public void Tooltip_NeverOver127() =>
        Assert.True(TrayBuilder.Build(H.Snap(ViewState.Live, H.Pods(lc: true, rc: true, cc: true)), H.Now).Tooltip.Length <= 127);
}

public class TextsTests
{
    private static Candidate Cand(PodsAdvertisement a) => new(a.ModelCode, a.ColorCode, a.Model, a, -50, H.Now);

    [Fact]
    public void WizardDetails_WithAndWithoutColor()
    {
        Assert.Equal("AirPods 3 · לבן · סוללה: שמאל \u200E80%\u200E, ימין \u200E70%\u200E, קופסה \u200E50%\u200E", Texts.WizardDetails(Cand(H.Pods())));
        Assert.Equal("AirPods 3 · סוללה: שמאל \u200E80%\u200E, ימין \u200E70%\u200E, קופסה \u200E50%\u200E", Texts.WizardDetails(Cand(H.Pods(color: 0x42))));
        Assert.Equal("AirPods Max · כחול · סוללה: \u200E60%\u200E",
            Texts.WizardDetails(Cand(H.Pods(model: 0x0A20, headset: 60, color: 0x03))));
        Assert.Equal("AirPods 3 · אפור חלל", Texts.CandidateName(Cand(H.Pods(color: 0x09))));
        Assert.Equal("שמאל \u200E80%\u200E · ימין \u200E70%\u200E · קופסה \u200E50%\u200E", Texts.CandidateBattery(Cand(H.Pods())));
    }

    [Fact]
    public void EveryColor_HasAName()
    {
        foreach (var c in Enum.GetValues<Core.Models.DeviceColor>())
        {
            Assert.False(string.IsNullOrWhiteSpace(S.ColorName(c)));
        }

        Assert.Null(S.ColorName(null));
    }

    [Fact]
    public void ToastBody()
    {
        var alert = new LowBatteryAlert(new[] { new LowBatteryItem(BatteryPart.Left, 10) });
        Assert.Equal("\u200Fשמאל \u200E10%\u200E · ימין \u200E20%\u200E. כדאי להחזיר אותן לקופסה לטעינה.",
            Texts.ToastBody(alert, H.Pods(left: 10, right: 20)));
        var max = new LowBatteryAlert(new[] { new LowBatteryItem(BatteryPart.Headset, 20) });
        Assert.StartsWith("\u200Fסוללה \u200E20%\u200E", Texts.ToastBody(max, null));
    }

    [Fact]
    public void UpdateCards()
    {
        var page = new Uri("https://github.com/o/r/releases/tag/v1.1.0");
        AppVersion.TryParse("1.1.0", out var v);
        var a = UpdateCard.From(new UpdateResult(UpdateStatus.UpdateAvailable, v, page), "1.0.0");
        Assert.Equal(UpdateCardState.Available, a.State);
        Assert.Equal("להורדה", a.ButtonText);
        Assert.Equal(page, a.DownloadPage);
        Assert.Contains("1.1.0", a.Title);
        Assert.Equal(UpdateCardState.UpToDate, UpdateCard.From(new UpdateResult(UpdateStatus.UpToDate, v), "1.1.0").State);
        Assert.Equal(UpdateCardState.Failed, UpdateCard.From(new UpdateResult(UpdateStatus.Failed), "1.0.0").State);
        Assert.Equal(UpdateCardState.NotConfigured, UpdateCard.From(new UpdateResult(UpdateStatus.NotConfigured), "1.0.0").State);
        Assert.Equal(UpdateCardState.NoRelease, UpdateCard.From(new UpdateResult(UpdateStatus.NoRelease), "1.0.0").State);
        // "available" without a page is treated as a failure (never open an address we did not check)
        Assert.Equal(UpdateCardState.Failed, UpdateCard.From(new UpdateResult(UpdateStatus.UpdateAvailable, v), "1.0.0").State);
        Assert.False(UpdateCard.Checking.ButtonEnabled);
        Assert.Equal("בדוק עדכון עכשיו", UpdateCard.Idle.ButtonText);
    }

    [Fact] // everything follows AppInfo.Name
    public void Name_InOnePlace()
    {
        Assert.Equal("HeyPods", AppInfo.Name);
        foreach (var f in typeof(S).GetFields().Where(f => f.IsLiteral))
        {
            var text = (string)f.GetRawConstantValue()!;
            Assert.DoesNotContain("[שם]", text);
            Assert.DoesNotContain("שבלול", text);
        }

        Assert.StartsWith("HeyPods", S.SetForgetBody);
        Assert.Contains("HeyPods", S.SetStartup);
        Assert.Equal("ChikApps.HeyPods", AppInfo.AppUserModelId);
    }

    [Fact] // the privacy line must match the update button (no "never connects")
    public void Privacy_MatchesUpdateButton()
    {
        Assert.Contains("חוץ מבדיקת עדכון", S.AboutPrivacy);
        Assert.DoesNotContain("לא שולח כלום לשום מקום", S.AboutPrivacy);
    }

    [Fact] // the version shown in About is the version of the exe
    public void Version_MatchesProjectFile()
    {
        var dir = AppContext.BaseDirectory;
        while (dir is not null && !File.Exists(Path.Combine(dir, "Earbuds.sln")))
        {
            dir = Path.GetDirectoryName(dir);
        }

        Assert.NotNull(dir);
        var csproj = File.ReadAllText(Path.Combine(dir!, "src", "Earbuds.App", "Earbuds.App.csproj"));
        Assert.Contains("<Version>" + AppInfo.Version + "</Version>", csproj);
    }
}

public class PlacementTests
{
    private static readonly PixelRect Work = new(0, 0, 1920, 1032); // 1080 - 48 taskbar

    [Fact] // Hebrew Windows: clock on the left -> window on the left, 12 px above the taskbar
    public void Bottom_ClockLeft()
    {
        Assert.Equal((12, 1032 - 12 - 400), Placement.Compute(Work, TaskbarEdge.Bottom, true, 340, 400, 12));
        Assert.Equal((1920 - 12 - 340, 620), Placement.Compute(Work, TaskbarEdge.Bottom, false, 340, 400, 12));
    }

    [Fact]
    public void ShadowMargin_KeepsTheVisibleGap()
    {
        var (x, y) = Placement.Compute(Work, TaskbarEdge.Bottom, true, 340 + 48, 400 + 48, 12, shadow: 24);
        Assert.Equal(12 - 24, x);
        Assert.Equal(1032 - 12 + 24 - 448, y);
    }

    [Fact]
    public void OtherEdges_AndClamp()
    {
        var top = new PixelRect(0, 48, 1920, 1080);
        Assert.Equal((12, 60), Placement.Compute(top, TaskbarEdge.Top, true, 340, 400, 12));
        Assert.Equal((60, 1080 - 412), Placement.Compute(new PixelRect(48, 0, 1920, 1080), TaskbarEdge.Left, true, 340, 400, 12));
        Assert.Equal((1920 - 48 - 352, 1080 - 412), Placement.Compute(new PixelRect(0, 0, 1872, 1080), TaskbarEdge.Right, true, 340, 400, 12));
        var (_, y) = Placement.Compute(new PixelRect(0, 0, 800, 300), TaskbarEdge.Bottom, true, 340, 400, 12);
        Assert.Equal(0, y); // taller than the screen: top stays visible
    }

    [Fact]
    public void ClockSide()
    {
        var bar = new PixelRect(0, 1032, 1920, 1080);
        Assert.True(Placement.IsClockOnLeft(bar, new PixelRect(0, 1032, 300, 1080)));
        Assert.False(Placement.IsClockOnLeft(bar, new PixelRect(1600, 1032, 1920, 1080)));
    }

    [Fact]
    public void StartupCommand_RoundTrip()
    {
        var cmd = StartupCommand.Build(@"C:\Users\x\Downloads\HeyPods.exe");
        Assert.Equal("\"C:\\Users\\x\\Downloads\\HeyPods.exe\" --startup", cmd);
        Assert.True(StartupCommand.PointsTo(cmd, @"c:\users\x\downloads\heypods.exe"));
        Assert.False(StartupCommand.PointsTo(cmd, @"C:\Other\HeyPods.exe"));
        Assert.False(StartupCommand.PointsTo(null, @"C:\Other\HeyPods.exe"));
        Assert.True(StartupCommand.IsStartupLaunch(new[] { "--STARTUP" }));
        Assert.False(StartupCommand.IsStartupLaunch(Array.Empty<string>()));
    }
}

public class WizardModelTests
{
    [Fact]
    public void Candidate_ShownLikeThePopup()
    {
        var a = H.Pods(model: 0x1420, kase: null);
        var p = PanelBuilder.ForCandidate(new Candidate(a.ModelCode, 0, a.Model, a, -50, H.Now), H.Now);
        Assert.Equal("AirPods Pro 2", p.Title);
        Assert.Equal(PanelBody.Gauges, p.Body);
        Assert.All(p.Gauges, g => Assert.Equal(PodSpot.None, g.Spot));
        Assert.Equal("--", p.Gauges[1].LevelText);
        Assert.Equal(new ArtModel(ArtFamily.Pro, false, false, LedState.Green), PanelBuilder.Finished(a.Model));
        Assert.Equal(LedState.Off, PanelBuilder.Finished(Core.Models.ModelCatalog.Find(0x0A20)).Led);
    }
}

public class PanelEqualityTests
{
    [Fact] // a new frame with the same values must not rebuild the window (it would cut the ring animation)
    public void SameBody_ByValue_IgnoresBar()
    {
        var a = PanelBuilder.Build(H.Snap(ViewState.Live, H.Pods()), H.Now);
        var b = PanelBuilder.Build(H.Snap(ViewState.Live, H.Pods(), seenAgo: TimeSpan.FromSeconds(20)), H.Now);
        Assert.NotEqual(a.BarText, b.BarText);
        Assert.True(a.SameBodyAs(b));
        Assert.False(a.SameBodyAs(PanelBuilder.Build(H.Snap(ViewState.Live, H.Pods(left: 70)), H.Now)));
        Assert.False(a.SameBodyAs(null));
    }
}
