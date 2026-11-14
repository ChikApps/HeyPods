// Copyright (C) 2026 ChikApps
// SPDX-License-Identifier: GPL-3.0-only

using Earbuds.Core;
using Earbuds.Core.Ble;
using Earbuds.Core.Models;
using Earbuds.Core.Platform;
using Earbuds.Core.Tracking;

namespace Earbuds.Presentation.Tests;

internal static class H
{
    public static readonly DateTimeOffset Now = new(2026, 11, 14, 12, 0, 0, TimeSpan.Zero);

    public static PodsAdvertisement Pods(
        ushort model = 0x1320, int? left = 80, int? right = 70, int? kase = 50,
        bool lc = false, bool rc = false, bool cc = false,
        EarState le = EarState.InCase, EarState re = EarState.InEar, byte color = 0x00,
        int? headset = null, bool headsetCharging = false) => new()
    {
        ModelCode = model,
        Model = ModelCatalog.Find(model),
        ColorCode = color,
        Status = 0,
        LidByte = 0,
        ConnectionStateByte = 0,
        ValuesFlipped = false,
        PrimarySide = PodSide.Left,
        LeftBattery = left,
        RightBattery = right,
        CaseBattery = kase,
        LeftCharging = lc,
        RightCharging = rc,
        CaseCharging = cc,
        LeftEar = le,
        RightEar = re,
        ThisPodInCase = false,
        OnePodInCase = false,
        BothPodsInCase = false,
        Lid = LidState.Open,
        HeadsetBattery = headset,
        HeadsetCharging = headsetCharging,
        HeadsetWorn = headset is null ? null : true,
    };

    public static MonitorSnapshot Snap(
        ViewState state, PodsAdvertisement? latest = null, ushort model = 0x1320, bool chosen = true,
        TimeSpan? seenAgo = null, TimedValue<int>? kase = null, bool connected = true, RadioState radio = RadioState.On) =>
        new(state,
            chosen ? new MyDeviceProfile(model, 0) : null,
            latest,
            latest is null ? null : Now - (seenAgo ?? TimeSpan.FromSeconds(3)),
            kase ?? (latest?.CaseBattery is { } c ? new TimedValue<int>(c, Now - (seenAgo ?? TimeSpan.FromSeconds(3))) : null),
            connected,
            radio);
}
