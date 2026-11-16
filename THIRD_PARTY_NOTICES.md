# Third-party notices and credits

This program is licensed under the GNU General Public License v3.0 (see `LICENSE`).
Copyright (C) 2026 ChikApps.

AirPods and Apple are trademarks of Apple Inc. This project is independent and not affiliated with,
endorsed or sponsored by Apple. No Apple software, images or binaries are included.

## Open-source projects we learned from

No source file was copied. Our C# code was written from the protocol knowledge in these projects;
where a piece of logic follows one of them closely, the file says so in a header comment with the
exact file and line. Model codes and test broadcasts are factual data taken from them (cited per row
in `src/Earbuds.Core/Models/ModelCatalog.cs` and per test in `tests/`). Commits read: see `docs/models.md`.

| Project | License | Used for | Where in our code |
|---|---|---|---|
| CAPod, d4rken-org/capod (CAPod contributors) | GPL-3.0 | Broadcast layout: flip bit, in-ear XOR with "this pod in case", microphone side, lid trust rule, prefix 0x01, Max/Max 2 wear bits. Model codes. **Real broadcast bytes and expected values used in our unit tests.** | `Ble/ProximityPairingParser.cs`, `Models/ModelCatalog.cs`, `tests/ParserTests.cs`, `tests/FuzzTests.cs`, `tests/CommonModelsTests.cs`, `tests/MonitorTests.cs` |
| AirPodsDesktop, SpriteOvO/AirPodsDesktop (Copyright (C) 2021-2022 SpriteOvO) | GPL-3.0 | Broadcast struct (27 bytes, bit fields), lid bit, color table, "charging pod is not in the ear", Windows vendor/product ID properties and device lookup sequence, RSSI default -80. Model codes. | `Ble/ProximityPairingParser.cs`, `Models/*.cs`, `Tracking/MyDevice.cs`, `Earbuds.Windows/WinConnectedAudioDevices.cs` |
| LibrePods, kavishdevar/librepods (Copyright (C) 2025 LibrePods contributors) | GPL-3.0 | Same layout confirmed independently, pairing-mode prefix check, color table. Model codes. | `Ble/ProximityPairingParser.cs`, `Models/*.cs` |
| WinPods, JamesCJ60/WinPods (JamesCJ) | GPL-3.0 | Windows approach: BluetoothLEAdvertisementWatcher (active scan), GSMTC sessions remembered by app ID, "resume only the session we paused", Max wear bits, Max color values. Model codes. | `Playback/AutoPauseController.cs`, `Earbuds.Windows/WinBleAdvertisementSource.cs`, `Earbuds.Windows/WinMediaSessions.cs`, `Models/*.cs` |
| OpenPods, adolfintel/OpenPods (Copyright (C) 2019-2022 Federico Dossena) | GPL-3.0 | Original nibble/flip description, value 15 = unknown, RSSI -60. Model codes. Test broadcasts from OpenPods issue #34 (via CAPod tests). | `Ble/ProximityPairingParser.cs`, `Models/ModelCatalog.cs`, `Tracking/MyDevice.cs`, `tests/ParserTests.cs` |

Read but not used in this code: sinanovicanes/winpods (MIT), bho3538/Airpods-Windows-Control (MIT),
martinoturrina/airpods-windows (MIT), MagicPodsCore (GPL-3.0). Those relate to phase 2 (driver), which is not planned.

## Libraries shipped with the app

| Library | License | Note |
|---|---|---|
| .NET 8 runtime + WPF (Microsoft), 8.0.34 | MIT | bundled inside the exe (self-contained single file) |
| H.NotifyIcon.Wpf 2.3.2, H.NotifyIcon 2.3.2, H.GeneratedIcons.System.Drawing 2.3.2 (HavenDV, https://github.com/HavenDV/H.NotifyIcon) | MIT | tray icon near the clock (`src/Earbuds.App/Platform/TrayIcon.cs`) |
| System.Drawing.Common 9.0.1, Microsoft.Win32.SystemEvents 9.0.1 (Microsoft) | MIT | dependencies of H.NotifyIcon |
| WinRT.Runtime 2.2.0 (C#/WinRT, CsWinRT; Copyright (c) Microsoft Corporation, https://github.com/microsoft/CsWinRT) | MIT | runtime support for the Windows APIs we call (Bluetooth, media sessions, notifications). Shipped inside the exe; it comes in the same NuGet package as the projection below. MIT text checked in the CsWinRT repository `LICENSE`. |
| Windows SDK projection for .NET (Microsoft.Windows.SDK.NET.Ref 10.0.19041.56, built with CsWinRT) | Microsoft Windows SDK license: text NOT read (https://aka.ms/WinSDKLicenseURL) | Access to Windows APIs. ❓ Unresolved, to be confirmed before publishing: redistribution terms and GPL "System Libraries" status. What we checked on 2026-11-14: the package `.nuspec` only gives `licenseUrl` = that link and `requireLicenseAcceptance` = true (no SPDX id); the package has no LICENSE file inside; the link could not be opened from our build machine (blocked, HTTP 403). So the text is unknown to us. |

## Drawings and fonts

The earbud, case and tray drawings are our own (ChikApps), built from shapes and gradients,
not traced from Apple images, no logo. The tiny letters "L", "R" and "?" on the drawings are glyph outlines
from Noto Sans Hebrew (Copyright 2022 The Noto Project Authors, SIL Open Font License 1.1, https://openfontlicense.org),
converted to vector paths by `tools/art/svg2xaml.py`. The app's own text uses the Segoe UI font that ships with Windows.

## Used only for testing (not shipped)

| Library | License |
|---|---|
| xunit 2.9.2, xunit.runner.visualstudio 2.8.2 | Apache-2.0 |
| Microsoft.NET.Test.Sdk 17.11.1 | MIT |
| Microsoft.Extensions.TimeProvider.Testing 8.10.0 | MIT |
| fontTools, lxml (Python, build-time only for `tools/art`) | MIT, BSD-3-Clause |
