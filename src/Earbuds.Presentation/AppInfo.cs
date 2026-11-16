// Copyright (C) 2026 ChikApps
// SPDX-License-Identifier: GPL-3.0-only

namespace Earbuds.Presentation;

/// <summary>
/// Everything that names the app, in one place: change <see cref="Name"/> and rebuild; every text, the tray
/// tooltip, the settings folder, the startup entry, the notification sender and (through publish.sh/.cmd)
/// the exe name follow it.
/// </summary>
public static class AppInfo
{
    /// <summary>App name shown to the user. Product name, 2026-11-14 (pronounced "hey-pods").</summary>
    public const string Name = "HeyPods";

    /// <summary>Must match &lt;Version&gt; in src/Earbuds.App/Earbuds.App.csproj (a test checks it).</summary>
    /// <summary>Subtitle under the name in About (2026-11-14). Nominative use; the About page says AirPods is Apple's trademark.</summary>
    public const string Subtitle = "for AirPods";

    public const string Version = "0.9.2";

    public const string Publisher = "ChikApps";

    public const string ContactEmail = "office@chiktime.com";

    /// <summary>Address of the source code (the About row is hidden when empty).</summary>
    public const string SourceCodeUrl = "https://github.com/ChikApps/HeyPods";

    /// <summary>Identity for Windows notifications (unpackaged app).</summary>
    public static string AppUserModelId => Publisher + "." + Name;

    /// <summary>Folder under %AppData% / %LocalAppData%.</summary>
    public static string DataFolderName => Name;

    /// <summary>Value name under HKCU\Software\Microsoft\Windows\CurrentVersion\Run.</summary>
    public static string StartupValueName => Name;

    /// <summary>Base name for the single-instance mutex and the "show yourself" event.</summary>
    public static string InstanceId => Publisher + "." + Name;
}
