// Copyright (C) 2026 ChikApps
// SPDX-License-Identifier: GPL-3.0-only

using Earbuds.Core.Models;

namespace Earbuds.Core.Ble;

/// <summary>Left or right pod.</summary>
public enum PodSide
{
    Left,
    Right,
}

/// <summary>Where a single pod is.</summary>
public enum EarState
{
    /// <summary>No information (e.g. over-ear model, or the broadcast does not say).</summary>
    Unknown,
    InEar,
    OutOfEar,
    InCase,
}

/// <summary>Case lid, as far as the broadcast can tell.</summary>
public enum LidState
{
    /// <summary>The broadcast carries no case context, or the lid bit is not trustworthy in this frame.</summary>
    Unknown,
    /// <summary>No pod is in the case, so the lid byte means nothing.</summary>
    NotInCase,
    Open,
    Closed,
}

/// <summary>
/// One decoded "Proximity Pairing" (type 0x07) broadcast. Battery values are whole tens (0..100) or null
/// when the broadcast says "unknown". The 16 encrypted bytes at the end are never kept (they may identify the owner).
/// </summary>
public sealed record PodsAdvertisement
{
    /// <summary>Model code, bytes 3-4 read big-endian (e.g. 0x1420).</summary>
    public required ushort ModelCode { get; init; }

    /// <summary>Row from <see cref="ModelCatalog"/> (or a generic "unknown" row).</summary>
    public required ModelInfo Model { get; init; }

    /// <summary>Raw color byte (byte 9).</summary>
    public required byte ColorCode { get; init; }

    /// <summary>Named color, or null when the byte is not a known color.</summary>
    public DeviceColor? Color => DeviceColors.TryGet(ColorCode);

    /// <summary>Raw status byte (byte 5), kept for diagnostics only.</summary>
    public required byte Status { get; init; }

    /// <summary>Raw lid byte (byte 8): bits 0-2 = open/close counter, bit 3 = closed.</summary>
    public required byte LidByte { get; init; }

    /// <summary>Byte 10, connection state (0x00 disconnected, 0x04 idle, 0x05 music, 0x06 call...; CAPod HasStateDetectionAirPods.kt).</summary>
    public required byte ConnectionStateByte { get; init; }

    // ---- Dual layout ----

    /// <summary>True when status bit 5 is clear: the lower nibbles belong to the right pod ("flipped").</summary>
    public required bool ValuesFlipped { get; init; }

    /// <summary>The primary pod (the one with the active microphone). Null for over-ear models.</summary>
    public required PodSide? PrimarySide { get; init; }

    public required int? LeftBattery { get; init; }
    public required int? RightBattery { get; init; }
    public required int? CaseBattery { get; init; }

    public required bool LeftCharging { get; init; }
    public required bool RightCharging { get; init; }
    public required bool CaseCharging { get; init; }

    public required EarState LeftEar { get; init; }
    public required EarState RightEar { get; init; }

    /// <summary>Status bit 6: the pod sending this broadcast is in the case.</summary>
    public required bool ThisPodInCase { get; init; }
    /// <summary>Status bit 4: one pod is in the case.</summary>
    public required bool OnePodInCase { get; init; }
    /// <summary>Status bit 2: both pods are in the case.</summary>
    public required bool BothPodsInCase { get; init; }

    public required LidState Lid { get; init; }

    // ---- Single layout (AirPods Max) ----

    /// <summary>Battery of an over-ear model. Null for Dual models.</summary>
    public required int? HeadsetBattery { get; init; }
    public required bool HeadsetCharging { get; init; }
    /// <summary>Over-ear model being worn. Null for Dual models.</summary>
    public required bool? HeadsetWorn { get; init; }

    /// <summary>Dual or Single.</summary>
    public PodsLayout Layout => Model.Layout;

    /// <summary>How many pods are in an ear right now (0-2; for over-ear models 0-1).</summary>
    public int InEarCount => Layout == PodsLayout.Single
        ? (HeadsetWorn == true ? 1 : 0)
        : (LeftEar == EarState.InEar ? 1 : 0) + (RightEar == EarState.InEar ? 1 : 0);
}
