// Copyright (C) 2026 ChikApps
// SPDX-License-Identifier: GPL-3.0-only
//
// Local error log (%LocalAppData%\<name>\errors.log, max 256 KB). Never sent anywhere (spec A1).

using System.IO;
using Earbuds.Presentation;

namespace Earbuds.App.Platform;

internal static class ErrorLog
{
    private const long MaxBytes = 256 * 1024;
    private static readonly object Lock = new();

    public static string Folder => Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), AppInfo.DataFolderName);

    public static string FilePath => Path.Combine(Folder, "errors.log");

    public static void Write(string where, Exception e)
    {
        try
        {
            lock (Lock)
            {
                Directory.CreateDirectory(Folder);
                var info = new FileInfo(FilePath);
                if (info.Exists && info.Length > MaxBytes)
                {
                    File.Move(FilePath, FilePath + ".old", overwrite: true);
                }

                File.AppendAllText(FilePath, $"{DateTimeOffset.Now:yyyy-MM-dd HH:mm:ss} [{where}] {AppInfo.Version} {e.GetType().Name}: {e.Message}{Environment.NewLine}{e.StackTrace}{Environment.NewLine}");
            }
        }
        catch (Exception)
        {
            // logging must never crash the app
        }
    }
}
