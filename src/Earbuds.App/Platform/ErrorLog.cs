// Copyright (C) 2026 ChikApps
// SPDX-License-Identifier: GPL-3.0-only
//
// Local error log (%LocalAppData%\<name>\errors.log, max 256 KB). Never sent anywhere (spec A1).
// Every entry is written in full: the whole chain of inner exceptions (a WPF "XamlParseException" hides the real
// cause in InnerException), each with its own stack, plus the line number WPF gives for XAML errors.

using System.Diagnostics;
using System.IO;
using System.Reflection;
using System.Runtime.InteropServices;
using System.Text;
using Earbuds.Presentation;

namespace Earbuds.App.Platform;

internal static class ErrorLog
{
    private const long MaxBytes = 256 * 1024;
    private const int MaxDepth = 12;
    private static readonly object Lock = new();

    public static string Folder => Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), AppInfo.DataFolderName);

    public static string FilePath => Path.Combine(Folder, "errors.log");

    public static void Write(string where, Exception e)
    {
        try
        {
            var text = Describe(where, e);
            lock (Lock)
            {
                Directory.CreateDirectory(Folder);
                var info = new FileInfo(FilePath);
                if (info.Exists && info.Length > MaxBytes)
                {
                    File.Move(FilePath, FilePath + ".old", overwrite: true);
                }

                File.AppendAllText(FilePath, text, new UTF8Encoding(false));
            }
        }
        catch (Exception)
        {
            // logging must never crash the app
        }
    }

    /// <summary>Shows the log in Explorer (the file selected). For the "something went wrong at start-up" message.</summary>
    public static void OpenInExplorer()
    {
        try
        {
            Directory.CreateDirectory(Folder);
            var args = File.Exists(FilePath) ? $"/select,\"{FilePath}\"" : $"\"{Folder}\"";
            Process.Start(new ProcessStartInfo("explorer.exe", args) { UseShellExecute = false })?.Dispose();
        }
        catch (Exception)
        {
            // no Explorer: the message still tells where the file is
        }
    }

    /// <summary>The full text of one entry (also used by tests of the format).</summary>
    internal static string Describe(string where, Exception e)
    {
        var sb = new StringBuilder();
        sb.AppendLine($"==== {DateTimeOffset.Now:yyyy-MM-dd HH:mm:ss} [{where}] {AppInfo.Name} {AppInfo.Version} ====");
        sb.AppendLine($"{RuntimeInformation.OSDescription} | {RuntimeInformation.FrameworkDescription} | {RuntimeInformation.ProcessArchitecture} | culture {System.Globalization.CultureInfo.CurrentCulture.Name} | ui {System.Globalization.CultureInfo.CurrentUICulture.Name}");
        AppendException(sb, e, "exception", 0);
        sb.AppendLine();
        return sb.ToString();
    }

    private static void AppendException(StringBuilder sb, Exception e, string label, int depth)
    {
        sb.AppendLine($"--- {label}: {e.GetType().FullName}: {e.Message}");
        var extra = XamlPosition(e);
        if (extra.Length > 0)
        {
            sb.AppendLine("    " + extra);
        }

        sb.AppendLine("    HResult 0x" + e.HResult.ToString("X8"));
        sb.AppendLine(string.IsNullOrWhiteSpace(e.StackTrace) ? "    (no stack)" : e.StackTrace);
        if (depth >= MaxDepth)
        {
            sb.AppendLine("    (more inner exceptions not shown)");
            return;
        }

        if (e is AggregateException agg)
        {
            var n = 0;
            foreach (var inner in agg.InnerExceptions)
            {
                AppendException(sb, inner, $"inner {depth + 1}.{++n} (aggregate)", depth + 1);
            }
        }
        else if (e.InnerException is not null)
        {
            AppendException(sb, e.InnerException, $"inner {depth + 1}", depth + 1);
        }
    }

    /// <summary>XamlParseException / XmlException: where in the XAML it happened (read by name, no WPF reference needed).</summary>
    private static string XamlPosition(Exception e)
    {
        try
        {
            var parts = new List<string>();
            foreach (var name in new[] { "LineNumber", "LinePosition", "BaseUri", "Key", "Name" })
            {
                var p = e.GetType().GetProperty(name, BindingFlags.Public | BindingFlags.Instance);
                if (p is null || p.GetIndexParameters().Length > 0)
                {
                    continue;
                }

                var v = p.GetValue(e);
                if (v is not null && !(v is int i && i == 0) && v.ToString() is { Length: > 0 } s)
                {
                    parts.Add($"{name}={s}");
                }
            }

            return parts.Count == 0 ? string.Empty : "where in the XAML: " + string.Join(", ", parts);
        }
        catch (Exception)
        {
            return string.Empty;
        }
    }
}
