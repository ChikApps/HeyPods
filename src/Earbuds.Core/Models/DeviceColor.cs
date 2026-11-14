// Copyright (C) 2026 ChikApps
// SPDX-License-Identifier: GPL-3.0-only

namespace Earbuds.Core.Models;

/// <summary>
/// Color byte (byte 9 of the broadcast). The same 13 values appear in three sources that agree:
/// AirPodsDesktop Source/Core/AppleCP.h:47-59 (author notes "untested because I don't have a device other than white"),
/// LibrePods linux/ble/blemanager.cpp:27-59, CAPod .../ble/devices/HasAppleColor.kt (enum DeviceColor).
/// AirPods Max uses other values (JamesCJ60/WinPods AirPodsProductCatalogue.cs:113-128, e.g. 0x11 green);
/// those are not mapped here: the UI simply omits the color name. Filtering "my AirPods" uses the raw byte,
/// so unmapped colors still work.
/// </summary>
public enum DeviceColor : byte
{
    White = 0x00,
    Black = 0x01,
    Red = 0x02,
    Blue = 0x03,
    Pink = 0x04,
    Gray = 0x05,
    Silver = 0x06,
    Gold = 0x07,
    RoseGold = 0x08,
    SpaceGray = 0x09,
    DarkBlue = 0x0A,
    LightBlue = 0x0B,
    Yellow = 0x0C,
}

/// <summary>Helpers for <see cref="DeviceColor"/>.</summary>
public static class DeviceColors
{
    /// <summary>Returns the named color, or null when the byte is not one of the 13 known values.</summary>
    public static DeviceColor? TryGet(byte raw) => raw <= 0x0C ? (DeviceColor)raw : null;
}
