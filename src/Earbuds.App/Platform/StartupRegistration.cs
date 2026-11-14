// Copyright (C) 2026 ChikApps
// SPDX-License-Identifier: GPL-3.0-only
//
// "Start with Windows": a value under HKCU\Software\Microsoft\Windows\CurrentVersion\Run
// (current user only, no admin rights, spec §6).

using Earbuds.Presentation;
using Microsoft.Win32;

namespace Earbuds.App.Platform;

internal static class StartupRegistration
{
    private const string RunKey = @"Software\Microsoft\Windows\CurrentVersion\Run";

    private static string ExePath => Environment.ProcessPath ?? string.Empty;

    /// <summary>Makes the registry match the setting. Also fixes the path when the exe was moved. Returns false on failure.</summary>
    public static bool Apply(bool enabled)
    {
        try
        {
            using var key = Registry.CurrentUser.CreateSubKey(RunKey, writable: true);
            if (enabled)
            {
                if (ExePath.Length == 0)
                {
                    return false;
                }

                if (!StartupCommand.PointsTo(key.GetValue(AppInfo.StartupValueName) as string, ExePath))
                {
                    key.SetValue(AppInfo.StartupValueName, StartupCommand.Build(ExePath), RegistryValueKind.String);
                }
            }
            else if (key.GetValue(AppInfo.StartupValueName) is not null)
            {
                key.DeleteValue(AppInfo.StartupValueName, throwOnMissingValue: false);
            }

            return true;
        }
        catch (Exception e)
        {
            ErrorLog.Write("startup", e);
            return false;
        }
    }
}
