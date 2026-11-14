// Copyright (C) 2026 ChikApps
// SPDX-License-Identifier: GPL-3.0-only
//
// Low battery notification (2026-11-14): threshold 20%, once per drop, with a setting.
// Our own logic.

using Earbuds.Core.Ble;
using Earbuds.Core.Tracking;

namespace Earbuds.Core.Notifications;

/// <summary>Which part is low.</summary>
public enum BatteryPart
{
    Left,
    Right,
    /// <summary>Over-ear model (AirPods Max).</summary>
    Headset,
}

/// <summary>One part that just went low.</summary>
public sealed record LowBatteryItem(BatteryPart Part, int Percent);

/// <summary>One notification to show (may cover both pods at once).</summary>
public sealed record LowBatteryAlert(IReadOnlyList<LowBatteryItem> Items);

/// <summary>
/// Decides when to show a Windows notification. Rules:
/// a part at or below <see cref="Thresholds.LowBatteryPercent"/> that is not charging alerts once;
/// it alerts again only after it went back above the threshold (charged) and dropped again.
/// Unknown values ("--") change nothing. The case is not included (spec §2: "in one of the pods").
/// </summary>
public sealed class LowBatteryAlerts
{
    private readonly Dictionary<BatteryPart, bool> _alerted = new();

    /// <summary>Feed every accepted broadcast of my AirPods. Returns an alert to show, or null.</summary>
    public LowBatteryAlert? Evaluate(PodsAdvertisement advertisement, bool enabled)
    {
        var items = new List<LowBatteryItem>();
        if (advertisement.Layout == Models.PodsLayout.Single)
        {
            Check(BatteryPart.Headset, advertisement.HeadsetBattery, advertisement.HeadsetCharging, enabled, items);
        }
        else
        {
            Check(BatteryPart.Left, advertisement.LeftBattery, advertisement.LeftCharging, enabled, items);
            Check(BatteryPart.Right, advertisement.RightBattery, advertisement.RightCharging, enabled, items);
        }

        return items.Count == 0 ? null : new LowBatteryAlert(items);
    }

    /// <summary>New AirPods chosen or forgotten.</summary>
    public void Reset() => _alerted.Clear();

    private void Check(BatteryPart part, int? percent, bool charging, bool enabled, List<LowBatteryItem> items)
    {
        if (percent is not { } value)
        {
            return;
        }

        if (value > Thresholds.LowBatteryPercent)
        {
            _alerted[part] = false; // charged again: re-arm
            return;
        }

        if (charging || _alerted.GetValueOrDefault(part))
        {
            return;
        }

        // Low and not charging. With the setting off we still mark it, so turning the setting on
        // later does not fire for a drop that already happened.
        _alerted[part] = true;
        if (enabled)
        {
            items.Add(new LowBatteryItem(part, value));
        }
    }
}
