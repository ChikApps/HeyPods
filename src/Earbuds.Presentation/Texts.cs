// Copyright (C) 2026 ChikApps
// SPDX-License-Identifier: GPL-3.0-only

using System.Globalization;
using Earbuds.Core.Ble;
using Earbuds.Core.Models;
using Earbuds.Core.Notifications;
using Earbuds.Core.Tracking;
using Earbuds.Core.Updates;

namespace Earbuds.Presentation;

/// <summary>Composed texts for the wizard, the notification and the update card.</summary>
public static class Texts
{
    private static string F(string format, params object[] args) => string.Format(CultureInfo.InvariantCulture, format, args);

    /// <summary>wiz.2.details: "AirPods 3 · לבן · סוללה: שמאל 80%, ימין 70%, קופסה 50%". Unknown color is left out.</summary>
    public static string WizardDetails(Candidate c)
    {
        var a = c.Latest;
        var name = PanelBuilder.ModelName(c.Model);
        var color = S.ColorName(DeviceColors.TryGet(c.ColorCode));
        if (a.Layout == PodsLayout.Single)
        {
            var b = Format.PercentInText(a.HeadsetBattery);
            return color is null ? F(S.Wiz2DetailsMaxNoColor, name, b) : F(S.Wiz2DetailsMax, name, color, b);
        }

        var l = Format.PercentInText(a.LeftBattery);
        var r = Format.PercentInText(a.RightBattery);
        var k = Format.PercentInText(a.CaseBattery);
        return color is null ? F(S.Wiz2DetailsNoColor, name, l, r, k) : F(S.Wiz2Details, name, color, l, r, k);
    }

    /// <summary>Wizard list row, first line: "AirPods Pro 2 · לבן".</summary>
    public static string CandidateName(Candidate c)
    {
        var color = S.ColorName(DeviceColors.TryGet(c.ColorCode));
        var name = PanelBuilder.ModelName(c.Model);
        return color is null ? name : name + " · " + color;
    }

    /// <summary>Wizard list row, second line: "שמאל 80% · ימין 70% · קופסה 50%".</summary>
    public static string CandidateBattery(Candidate c)
    {
        var a = c.Latest;
        return a.Layout == PodsLayout.Single
            ? F(S.ListBatteryMax, Format.PercentInText(a.HeadsetBattery))
            : F(S.ListBatteryDual, Format.PercentInText(a.LeftBattery), Format.PercentInText(a.RightBattery), Format.PercentInText(a.CaseBattery));
    }

    /// <summary>Low battery notification body. Shows both pods as they are now (design), the alert only decides when.</summary>
    public static string ToastBody(LowBatteryAlert alert, PodsAdvertisement? latest)
    {
        if (alert.Items.Any(i => i.Part == BatteryPart.Headset))
        {
            var b = latest?.HeadsetBattery ?? alert.Items.First(i => i.Part == BatteryPart.Headset).Percent;
            return Format.Rlm + F(S.ToastBodyMax, Format.PercentInText(b));
        }

        int? left = latest?.LeftBattery ?? alert.Items.FirstOrDefault(i => i.Part == BatteryPart.Left)?.Percent;
        int? right = latest?.RightBattery ?? alert.Items.FirstOrDefault(i => i.Part == BatteryPart.Right)?.Percent;
        return Format.Rlm + F(S.ToastBodyDual, Format.PercentInText(left), Format.PercentInText(right));
    }
}

/// <summary>States of the "check for update now" card.</summary>
public enum UpdateCardState
{
    Idle,
    Checking,
    Available,
    UpToDate,
    Failed,
    NotConfigured,
    NoRelease,
}

/// <summary>What the update card shows.</summary>
public sealed record UpdateCard(UpdateCardState State, string Title, string Hint, bool ButtonEnabled, bool ButtonIsDownload, Uri? DownloadPage)
{
    public string ButtonText => ButtonIsDownload ? S.UpdDownload : S.UpdButton;

    public static UpdateCard Idle { get; } = new(UpdateCardState.Idle, S.UpdTitle, S.UpdIdleHint, true, false, null);

    public static UpdateCard Checking { get; } = new(UpdateCardState.Checking, S.UpdTitle, S.UpdChecking, false, false, null);

    public static UpdateCard From(UpdateResult result, string currentVersion)
    {
        var cur = Format.Lrm + currentVersion + Format.Lrm;
        return result.Status switch
        {
            UpdateStatus.UpdateAvailable when result.DownloadPage is not null => new(UpdateCardState.Available,
                string.Format(CultureInfo.InvariantCulture, S.UpdAvailable, Format.Lrm + result.Latest?.ToString() + Format.Lrm),
                string.Format(CultureInfo.InvariantCulture, S.UpdAvailableHint, cur), true, true, result.DownloadPage),
            UpdateStatus.UpToDate => new(UpdateCardState.UpToDate, S.UpdUpToDate, S.UpdUpToDateHint, true, false, null),
            UpdateStatus.NotConfigured => new(UpdateCardState.NotConfigured, S.UpdNotConfigured, S.UpdNotConfiguredHint, true, false, null),
            UpdateStatus.NoRelease => new(UpdateCardState.NoRelease, S.UpdNoRelease,
                string.Format(CultureInfo.InvariantCulture, S.UpdNoReleaseHint, cur), true, false, null),
            _ => new(UpdateCardState.Failed, S.UpdFailed, S.UpdFailedHint, true, false, null),
        };
    }
}
