// Copyright (C) 2026 ChikApps
// SPDX-License-Identifier: GPL-3.0-only

using Earbuds.Core;
using Earbuds.Core.Settings;

namespace Earbuds.Windows;

/// <summary>Wires the Windows adapters to the core engine. The WPF app (next step) calls this once.</summary>
public static class WinPlatform
{
    /// <summary>Settings file: %AppData%\{appFolderName}\settings.json (per user, no admin rights).</summary>
    public static string SettingsPath(string appFolderName) =>
        Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData), appFolderName, "settings.json");

    /// <summary>Creates the engine with real Windows adapters. Call <see cref="EarbudsMonitor.StartAsync"/> afterwards.</summary>
    public static EarbudsMonitor CreateMonitor(AppSettings settings)
    {
        var audio = new WinConnectedAudioDevices();
        audio.StartWatching();
        return new EarbudsMonitor(
            new WinBleAdvertisementSource(),
            new WinBluetoothRadio(),
            audio,
            new WinMediaSessions(),
            settings,
            time: null,
            ownsAdapters: true);
    }
}
