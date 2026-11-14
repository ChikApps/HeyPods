// Copyright (C) 2026 ChikApps
// SPDX-License-Identifier: GPL-3.0-only

using System.Diagnostics;

namespace Earbuds.App.Platform;

/// <summary>Opens links in the user's browser / Windows settings. Only addresses written in our code.</summary>
internal static class Shell
{
    public static void Open(string target)
    {
        try
        {
            Process.Start(new ProcessStartInfo(target) { UseShellExecute = true })?.Dispose();
        }
        catch (Exception e)
        {
            ErrorLog.Write("open", e);
        }
    }

    /// <summary>Windows Bluetooth settings page.</summary>
    public static void BluetoothSettings() => Open("ms-settings:bluetooth");
}
