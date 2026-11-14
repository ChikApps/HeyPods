// Copyright (C) 2026 ChikApps
// SPDX-License-Identifier: GPL-3.0-only
//
// Decoder for Apple's "Proximity Pairing" BLE broadcast (Continuity type 0x07).
// Written by us from the layout documented in these GPL-3.0 projects (no code copied verbatim):
//   AirPodsDesktop  Source/Core/AppleCP.h:100-160, AppleCP.cpp:23-40, 103-157    (bit layout, length check)
//   CAPod           .../ble/devices/DualApplePods.kt:17-104, 158, 165-208       (flip, in-ear XOR, microphone, lid)
//                   .../ble/protocol/ProximityPairing.kt:24, 64, 67             (prefix 0x01, length 25)
//                   .../ble/devices/SingleApplePods.kt:18, AirPodsMax*.kt       (over-ear battery and wear)
//   LibrePods       linux/ble/blemanager.cpp:127-208                           (same layout, prefix check)
//   OpenPods        .../pods/PodsStatus.java:20-35, 45-70                       (nibbles, flip, value 15)
// The two sources disagree on in-ear bits when the broadcasting pod is in the case:
// we follow CAPod and LibrePods (XOR with status bit 6), which CAPod tests with real broadcasts
// (AirPodsPro2UsbcTest.kt "via #164 - in case").

using Earbuds.Core.Models;

namespace Earbuds.Core.Ble;

/// <summary>Decodes Apple manufacturer data (company 0x004C) into a <see cref="PodsAdvertisement"/>.</summary>
public static class ProximityPairingParser
{
    /// <summary>Apple's Bluetooth company identifier.</summary>
    public const ushort AppleCompanyId = 0x004C;

    /// <summary>Continuity message type "Proximity Pairing".</summary>
    public const byte ProximityPairingType = 0x07;

    /// <summary>Payload length of a genuine Proximity Pairing message (CAPod ProximityPairing.kt:64; AirPodsDesktop: 27-byte struct).</summary>
    public const byte PayloadLength = 25;

    /// <summary>First payload byte of a plaintext status broadcast (CAPod ProximityPairing.kt:67; LibrePods skips 0x00, blemanager.cpp:139).</summary>
    public const byte StatusPrefix = 0x01;

    /// <summary>Upper bound for input we even look at. BLE legacy adverts are 31 bytes, extended ones up to 254.</summary>
    public const int MaxInputLength = 255;

    /// <summary>
    /// Tries to decode Apple manufacturer data (the bytes after the 0x004C company ID).
    /// Never throws: anything short, long, truncated or unexpected returns false.
    /// </summary>
    public static bool TryParse(ReadOnlySpan<byte> appleData, out PodsAdvertisement? advertisement)
    {
        advertisement = null;
        if (appleData.Length < 2 || appleData.Length > MaxInputLength)
        {
            return false;
        }

        // Apple manufacturer data is a list of Continuity messages: [type][length][payload]...
        // Walk it with bounds checks and pick the 0x07 message.
        var offset = 0;
        while (offset + 2 <= appleData.Length)
        {
            var type = appleData[offset];
            var length = appleData[offset + 1];
            var start = offset + 2;
            if (start + length > appleData.Length)
            {
                return false; // length byte points past the end: malformed
            }

            if (type == ProximityPairingType)
            {
                if (length != PayloadLength)
                {
                    return false; // clones or other formats: ignore (spec: unknown structure -> ignore quietly)
                }

                return TryParsePayload(appleData.Slice(start, length), out advertisement);
            }

            offset = start + length;
        }

        return false;
    }

    /// <summary>Convenience overload.</summary>
    public static PodsAdvertisement? Parse(ReadOnlySpan<byte> appleData) =>
        TryParse(appleData, out var result) ? result : null;

    private static bool TryParsePayload(ReadOnlySpan<byte> p, out PodsAdvertisement? advertisement)
    {
        advertisement = null;

        // p[0] = prefix, p[1..2] = model, p[3] = status, p[4] = pod batteries, p[5] = flags|case battery,
        // p[6] = lid, p[7] = color, p[8] = connection state, p[9..24] = encrypted (ignored, never stored).
        if (p.Length != PayloadLength || p[0] != StatusPrefix)
        {
            return false;
        }

        var modelCode = (ushort)((p[1] << 8) | p[2]);
        var model = ModelCatalog.Find(modelCode);
        var status = p[3];
        var pods = p[4];
        var flags = (byte)(p[5] >> 4);
        var caseNibble = p[5] & 0x0F;
        var lidByte = p[6];

        if (model.Layout == PodsLayout.Single)
        {
            advertisement = new PodsAdvertisement
            {
                ModelCode = modelCode,
                Model = model,
                ColorCode = p[7],
                Status = status,
                LidByte = lidByte,
                ConnectionStateByte = p[8],
                ValuesFlipped = false,
                PrimarySide = null,
                LeftBattery = null,
                RightBattery = null,
                CaseBattery = null,
                LeftCharging = false,
                RightCharging = false,
                CaseCharging = false,
                LeftEar = EarState.Unknown,
                RightEar = EarState.Unknown,
                ThisPodInCase = false,
                OnePodInCase = false,
                BothPodsInCase = false,
                Lid = LidState.NotInCase,
                // CAPod SingleApplePods.kt:18 and OpenPods PodsStatus.java:53 (character 13 = lower nibble).
                HeadsetBattery = DecodeBattery(pods & 0x0F),
                // CAPod AirPodsMax.kt:44, AirPodsMaxUsbc.kt:44, AirPodsMax2.kt:44.
                HeadsetCharging = (flags & 0x01) != 0,
                HeadsetWorn = model.Wear switch
                {
                    // CAPod AirPodsMax.kt:48, AirPodsMaxUsbc.kt:48; JamesCJ60 AirPodsAdvertisementParser.cs:57.
                    WearBits.Bit5 => (status & 0x20) != 0,
                    // CAPod AirPodsMax2.kt:58 (comment: bit 5 always set on Max 2); JamesCJ60 AirPodsAdvertisementParser.cs:56.
                    WearBits.Bit1OrBit3 => (status & 0x0A) != 0,
                    _ => null,
                },
            };
            return true;
        }

        // Status bit 5 set = values for the left pod are in the lower nibbles (all five sources agree).
        var flipped = (status & 0x20) == 0;
        var leftNibble = flipped ? pods >> 4 : pods & 0x0F;
        var rightNibble = flipped ? pods & 0x0F : pods >> 4;

        // Charging: flags bit 0 = pod in the lower nibble, bit 1 = the other pod, bit 2 = case.
        var leftCharging = (flags & (flipped ? 0x02 : 0x01)) != 0;
        var rightCharging = (flags & (flipped ? 0x01 : 0x02)) != 0;
        var caseCharging = (flags & 0x04) != 0;

        var thisPodInCase = (status & 0x40) != 0;  // bit 6
        var onePodInCase = (status & 0x10) != 0;   // bit 4
        var bothPodsInCase = (status & 0x04) != 0; // bit 2

        // In-ear bits 1 and 3; which pod they belong to flips again when the sender is in the case (CAPod :81-90).
        var earXor = flipped ^ thisPodInCase;
        var leftInEarBit = (status & (earXor ? 0x08 : 0x02)) != 0;
        var rightInEarBit = (status & (earXor ? 0x02 : 0x08)) != 0;

        // Microphone (primary) pod: CAPod DualApplePods.kt:97/104.
        var leftIsPrimary = ((status & 0x20) != 0) ^ thisPodInCase;

        var (leftEar, rightEar) = ResolveEarStates(
            leftInEarBit, rightInEarBit, leftCharging, rightCharging, onePodInCase, bothPodsInCase);

        advertisement = new PodsAdvertisement
        {
            ModelCode = modelCode,
            Model = model,
            ColorCode = p[7],
            Status = status,
            LidByte = lidByte,
            ConnectionStateByte = p[8],
            ValuesFlipped = flipped,
            PrimarySide = leftIsPrimary ? PodSide.Left : PodSide.Right,
            LeftBattery = DecodeBattery(leftNibble),
            RightBattery = DecodeBattery(rightNibble),
            CaseBattery = DecodeBattery(caseNibble),
            LeftCharging = leftCharging,
            RightCharging = rightCharging,
            CaseCharging = caseCharging,
            LeftEar = leftEar,
            RightEar = rightEar,
            ThisPodInCase = thisPodInCase,
            OnePodInCase = onePodInCase,
            BothPodsInCase = bothPodsInCase,
            Lid = DecodeLid(lidByte, thisPodInCase, onePodInCase, bothPodsInCase),
            HeadsetBattery = null,
            HeadsetCharging = false,
            HeadsetWorn = null,
        };
        return true;
    }

    /// <summary>
    /// Battery nibble: 0..10 = 0..100% in tens, 15 = unknown (all sources).
    /// 11..14: CAPod and JamesCJ60 clamp to 100%, AirPodsDesktop treats as unknown. We treat them as unknown,
    /// because the spec says not to guess and not to show a wrong value.
    /// </summary>
    internal static int? DecodeBattery(int nibble) => nibble is >= 0 and <= 10 ? nibble * 10 : null;

    /// <summary>
    /// Lid state per CAPod DualApplePods.kt:165-208: the lid bit is only trustworthy from a pod inside the case
    /// (bit 6) or while both pods are in the case (bit 2). Bit 3 of the lid byte: 0 = open, 1 = closed
    /// (also AirPodsDesktop AppleCP.h:150-155, LibrePods blemanager.cpp:205).
    /// </summary>
    internal static LidState DecodeLid(byte lidByte, bool thisPodInCase, bool onePodInCase, bool bothPodsInCase)
    {
        if (!thisPodInCase && !onePodInCase && !bothPodsInCase)
        {
            return LidState.NotInCase;
        }

        if (!thisPodInCase && !bothPodsInCase)
        {
            return LidState.Unknown;
        }

        return ((lidByte >> 3) & 0x01) == 0 ? LidState.Open : LidState.Closed;
    }

    /// <summary>
    /// Our own combination of the bits into one state per pod:
    /// both-in-case wins; a charging pod is in the case (AirPodsDesktop AppleCP.cpp:146-157 uses the same idea
    /// to ignore the in-ear bit while charging); then the in-ear bit; then, if exactly one pod is reported
    /// in the case and only one pod is unaccounted for, that pod is in the case.
    /// </summary>
    internal static (EarState Left, EarState Right) ResolveEarStates(
        bool leftInEarBit, bool rightInEarBit, bool leftCharging, bool rightCharging,
        bool onePodInCase, bool bothPodsInCase)
    {
        if (bothPodsInCase)
        {
            return (EarState.InCase, EarState.InCase);
        }

        var left = leftCharging ? EarState.InCase : leftInEarBit ? EarState.InEar : EarState.OutOfEar;
        var right = rightCharging ? EarState.InCase : rightInEarBit ? EarState.InEar : EarState.OutOfEar;

        if (onePodInCase && left != EarState.InCase && right != EarState.InCase)
        {
            if (left == EarState.OutOfEar && right != EarState.OutOfEar)
            {
                left = EarState.InCase;
            }
            else if (right == EarState.OutOfEar && left != EarState.OutOfEar)
            {
                right = EarState.InCase;
            }
        }

        return (left, right);
    }
}
