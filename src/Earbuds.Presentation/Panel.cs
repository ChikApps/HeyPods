// Copyright (C) 2026 ChikApps
// SPDX-License-Identifier: GPL-3.0-only
//
// What the popup and the main window show for a given engine snapshot (spec §3, design §5).

using System.Globalization;
using Earbuds.Core;
using Earbuds.Core.Ble;
using Earbuds.Core.Models;
using Earbuds.Core.Tracking;

namespace Earbuds.Presentation;

/// <summary>Drawing family (design: short = AirPods 3/4, long = 1/2, pro, generic, max).</summary>
public enum ArtFamily
{
    Short,
    Long,
    Pro,
    Generic,
    Max,
}

/// <summary>Case light.</summary>
public enum LedState
{
    Off,
    Green,
    Amber,
}

/// <summary>Color of a ring and its number.</summary>
public enum GaugeTone
{
    /// <summary>Accent color.</summary>
    Normal,
    /// <summary>20% and below: red.</summary>
    Low,
    /// <summary>Old data: gray.</summary>
    Stale,
    /// <summary>"--": track only, number in secondary color.</summary>
    Unknown,
}

/// <summary>Location pill under a pod (main window only).</summary>
public enum PodSpot
{
    None,
    InEar,
    Out,
    InCase,
}

/// <summary>What fills the window body.</summary>
public enum PanelBody
{
    Gauges,
    NotFound,
    BluetoothOff,
    NotSupported,
    NoChoice,
}

/// <summary>Which drawing to show: family, where each pod is drawn, and the case light.</summary>
public sealed record ArtModel(ArtFamily Family, bool LeftOut, bool RightOut, LedState Led)
{
    public static ArtModel ForCase(ArtFamily family) => new(family, false, false, LedState.Off);
}

/// <summary>One ring with its label.</summary>
public sealed record GaugeModel(string Label, int? Level, bool Charging, GaugeTone Tone, PodSpot Spot, string? Note)
{
    public string LevelText => Format.Percent(Level);

    public string SpotText => Spot switch
    {
        PodSpot.InEar => S.StateInEar,
        PodSpot.Out => S.StateOut,
        PodSpot.InCase => S.StateInCase,
        _ => string.Empty,
    };

    /// <summary>Screen reader text, e.g. "שמאל 80%, בטעינה".</summary>
    public string Accessible => Label + " " + (Level is null ? S.Unknown : Format.PercentInText(Level)) + (Charging ? ", " + S.Charging : string.Empty);
}

/// <summary>Everything one popup / main window frame needs.</summary>
public sealed record PanelModel(
    string Title,
    PanelBody Body,
    ArtModel Art,
    IReadOnlyList<GaugeModel> Gauges,
    bool ShowNotConnected,
    bool ShowProHint,
    string BarText)
{
    /// <summary>Same body (everything except the bar text), comparing the gauges by value.</summary>
    public bool SameBodyAs(PanelModel? other) =>
        other is not null && Title == other.Title && Body == other.Body && Art == other.Art &&
        ShowNotConnected == other.ShowNotConnected && ShowProHint == other.ShowProHint && Gauges.SequenceEqual(other.Gauges);

    /// <summary>Latin titles ("AirPods 3") are laid out left-to-right but still aligned to the right edge.</summary>
    public bool TitleIsLatin => Title.Length > 0 && Title[0] < 0x0590;
}

public static class PanelBuilder
{
    /// <summary>Model codes of AirPods 1 and 2 (long stem drawing); every other Stem model is the short drawing.</summary>
    private static readonly HashSet<ushort> LongStem = new() { 0x0220, 0x0F20 };

    public static ArtFamily FamilyOf(ModelInfo? model)
    {
        if (model is null || model.IsUnknown)
        {
            return ArtFamily.Generic;
        }

        return model.Family switch
        {
            ModelFamily.Stem => LongStem.Contains(model.Code) ? ArtFamily.Long : ArtFamily.Short,
            ModelFamily.Pro => ArtFamily.Pro,
            ModelFamily.Max => ArtFamily.Max,
            _ => ArtFamily.Generic,
        };
    }

    /// <summary>Display name: the model name, or "אוזניות אפל (דגם לא מוכר)".</summary>
    public static string ModelName(ModelInfo? model) =>
        model is null || model.IsUnknown ? S.StUnknownModel : model.Name;

    /// <param name="popup">Popup: no location pills, no notices, no bar.</param>
    /// <param name="pauseEnabled">The "not connected" notice only matters when auto-pause is on.</param>
    public static PanelModel Build(MonitorSnapshot s, DateTimeOffset now, bool popup = false, bool pauseEnabled = true)
    {
        var model = s.Latest?.Model ?? s.Model;
        var family = FamilyOf(model);
        var title = s.Profile is null ? AppInfo.Name : ModelName(model);
        var empty = Array.Empty<GaugeModel>();

        switch (s.State)
        {
            case ViewState.NoDeviceChosen:
                return new PanelModel(AppInfo.Name, PanelBody.NoChoice, ArtModel.ForCase(ArtFamily.Generic), empty, false, false, string.Empty);
            case ViewState.NotSupported:
                return new PanelModel(AppInfo.Name, PanelBody.NotSupported, ArtModel.ForCase(family), empty, false, false, string.Empty);
            case ViewState.BluetoothOff:
                return new PanelModel(title, PanelBody.BluetoothOff, ArtModel.ForCase(family), empty, false, false, string.Empty);
        }

        if (s.Latest is not { } a || s.LastSeen is not { } seen || s.State == ViewState.NotFound)
        {
            return new PanelModel(title, PanelBody.NotFound, ArtModel.ForCase(family), empty, false, false, string.Empty);
        }

        var stale = s.State == ViewState.Stale;
        var bar = stale ? Format.LastSeen(now - seen) : Format.Updated(now - seen);
        var gauges = new List<GaugeModel>(3);
        ArtModel art;

        if (a.Layout == PodsLayout.Single)
        {
            gauges.Add(Gauge(S.Battery, a.HeadsetBattery, a.HeadsetCharging, stale, PodSpot.None, null));
            art = new ArtModel(ArtFamily.Max, false, false, LedState.Off);
        }
        else
        {
            // Case: the newest value from a frame that carried it. Missing in this frame (closed case, both pods
            // out) -> last known value in gray with "updated X ago" (spec state 4), never "0%" or an error.
            int? caseLevel = a.CaseBattery;
            var caseCharging = a.CaseCharging;
            var caseStale = stale;
            string? caseNote = null;
            if (caseLevel is null && s.LastKnownCaseBattery is { } known)
            {
                caseLevel = known.Value;
                caseCharging = false;
                caseStale = true;
                if (!stale)
                {
                    caseNote = Format.Updated(now - known.UpdatedAt);
                }
            }

            gauges.Add(Gauge(S.Left, a.LeftBattery, a.LeftCharging, stale, popup ? PodSpot.None : SpotOf(a.LeftEar), null));
            gauges.Add(Gauge(S.Case, caseLevel, caseCharging, caseStale, PodSpot.None, caseNote));
            gauges.Add(Gauge(S.Right, a.RightBattery, a.RightCharging, stale, popup ? PodSpot.None : SpotOf(a.RightEar), null));

            var led = caseLevel is null || caseStale || family == ArtFamily.Generic
                ? LedState.Off
                : caseLevel <= Thresholds.LowBatteryPercent ? LedState.Amber : LedState.Green;
            art = new ArtModel(family, IsOut(a.LeftEar), IsOut(a.RightEar), led);
        }

        return new PanelModel(
            title,
            PanelBody.Gauges,
            art,
            gauges,
            ShowNotConnected: !popup && pauseEnabled && !s.ConnectedAsAudio,
            ShowProHint: !popup && family == ArtFamily.Pro,
            BarText: popup ? string.Empty : bar);
    }

    /// <summary>Wizard step 2: a candidate shown like the popup (drawing + rings, no pills).</summary>
    public static PanelModel ForCandidate(Candidate c, DateTimeOffset now)
    {
        var kase = c.Latest.CaseBattery is { } k ? new TimedValue<int>(k, now) : null;
        var snap = new MonitorSnapshot(ViewState.Live, c.ToProfile(), c.Latest, now, kase, true, Core.Platform.RadioState.On);
        return Build(snap, now, popup: true);
    }

    /// <summary>Wizard step 3: the chosen AirPods, all in the case, full.</summary>
    public static ArtModel Finished(ModelInfo? model)
    {
        var family = FamilyOf(model);
        return new ArtModel(family, false, false, family is ArtFamily.Generic or ArtFamily.Max ? LedState.Off : LedState.Green);
    }

    private static GaugeModel Gauge(string label, int? level, bool charging, bool stale, PodSpot spot, string? note)
    {
        var tone = level is null ? GaugeTone.Unknown
            : stale ? GaugeTone.Stale
            : level <= Thresholds.LowBatteryPercent ? GaugeTone.Low
            : GaugeTone.Normal;
        return new GaugeModel(label, level, charging && level is not null && !stale, tone, spot, note);
    }

    private static PodSpot SpotOf(EarState ear) => ear switch
    {
        EarState.InEar => PodSpot.InEar,
        EarState.OutOfEar => PodSpot.Out,
        EarState.InCase => PodSpot.InCase,
        _ => PodSpot.None,
    };

    private static bool IsOut(EarState ear) => ear is EarState.InEar or EarState.OutOfEar;
}

/// <summary>Tray icon look and tooltip.</summary>
public enum TrayLook
{
    /// <summary>Colored, filled to the lower pod.</summary>
    Ok,
    /// <summary>Gray: no data, not chosen, not around, no Bluetooth LE.</summary>
    Off,
    /// <summary>Colored with a red dot: a pod at 20% or less.</summary>
    Low,
    /// <summary>Gray with a slash: Bluetooth is off.</summary>
    BluetoothOff,
}

public sealed record TrayModel(TrayLook Look, int FillPercent, string Tooltip);

public static class TrayBuilder
{
    /// <summary>Default fill of the tray drawing when no level is known (design: 70).</summary>
    public const int DefaultFill = 70;

    /// <summary>Windows cuts tray tooltips at 127 characters.</summary>
    public const int MaxTooltip = 127;

    public static TrayModel Build(MonitorSnapshot s, DateTimeOffset now)
    {
        var (look, tooltip) = s.State switch
        {
            ViewState.NoDeviceChosen => (TrayLook.Off, S.TrayTooltipNoChoice),
            ViewState.BluetoothOff => (TrayLook.BluetoothOff, S.TrayTooltipBtOff),
            ViewState.NotSupported => (TrayLook.Off, S.TrayTooltipNoBle),
            ViewState.NotFound => (TrayLook.Off, S.TrayTooltipNone),
            ViewState.Stale when s.LastSeen is { } seen => (TrayLook.Off, string.Format(CultureInfo.InvariantCulture, S.TrayTooltipLastSeen, Format.LastSeen(now - seen))),
            ViewState.Live when s.Latest is { } a => (s.IsLowBattery ? TrayLook.Low : TrayLook.Ok, LiveTooltip(a, s)),
            _ => (TrayLook.Off, S.TrayTooltipNone),
        };

        var fill = DefaultFill;
        if (s.Latest is { } latest && look is TrayLook.Ok or TrayLook.Low)
        {
            int? lowest = latest.Layout == PodsLayout.Single
                ? latest.HeadsetBattery
                : Min(latest.LeftBattery, latest.RightBattery);
            fill = lowest ?? DefaultFill;
        }

        tooltip = Format.Rlm + tooltip;
        if (tooltip.Length > MaxTooltip)
        {
            tooltip = tooltip[..MaxTooltip];
        }

        return new TrayModel(look, fill, tooltip);
    }

    private static int? Min(int? a, int? b) => a is null ? b : b is null ? a : Math.Min(a.Value, b.Value);

    private static string LiveTooltip(PodsAdvertisement a, MonitorSnapshot s)
    {
        if (a.Layout == PodsLayout.Single)
        {
            return string.Format(CultureInfo.InvariantCulture, S.TrayTooltipMax, Value(a.HeadsetBattery, a.HeadsetCharging));
        }

        var caseLevel = a.CaseBattery ?? s.LastKnownCaseBattery?.Value;
        var caseCharging = a.CaseBattery is not null && a.CaseCharging;
        return string.Format(CultureInfo.InvariantCulture, S.TrayTooltip,
            Value(a.LeftBattery, a.LeftCharging), Value(a.RightBattery, a.RightCharging), Value(caseLevel, caseCharging));
    }

    private static string Value(int? level, bool charging)
    {
        var text = Format.PercentInText(level);
        return charging && level is not null ? string.Format(CultureInfo.InvariantCulture, S.TrayTooltipCharging, text) : text;
    }
}
