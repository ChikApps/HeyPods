// Copyright (C) 2026 ChikApps
// SPDX-License-Identifier: GPL-3.0-only
//
// Something went wrong: full details to the log (stack + inner exceptions), the log opened in Explorer, and ONE
// message per run in plain Hebrew (the user does not read English or stack traces).
// Startup: "could not start" (the app then closes). Runtime (any window, any time later): "something did not work",
// the app keeps running (the tray icon must stay).

using System.Windows;
using System.Windows.Threading;
using Earbuds.Presentation;

namespace Earbuds.App.Platform;

internal static class FailureReport
{
    /// <summary>The same error (where + type + message) is written at most this many times per run: an error in
    /// layout repeats on every layout pass and would push everything else out of the 256 KB log.</summary>
    private const int MaxSameEntries = 3;

    private static readonly Dictionary<string, int> Seen = new();
    private static int _shown;

    /// <summary>Error before the app finished starting. Blocks until the user closes the message.</summary>
    public static void Startup(string where, Exception e)
    {
        Log(where, e);
        if (Interlocked.Exchange(ref _shown, 1) != 0)
        {
            return;
        }

        ErrorLog.OpenInExplorer();
        Show(S.StartupFailed);
    }

    /// <summary>Error while running (a window, a timer, a task). Logged; the message is shown once, later, by the
    /// dispatcher, so it never runs inside the layout pass that failed.</summary>
    public static void Runtime(string where, Exception e)
    {
        Log(where, e);
        if (Interlocked.Exchange(ref _shown, 1) != 0)
        {
            return;
        }

        void Tell()
        {
            ErrorLog.OpenInExplorer();
            Show(S.RuntimeFailed);
        }

        var dispatcher = Application.Current?.Dispatcher;
        if (dispatcher is null || dispatcher.HasShutdownStarted)
        {
            Tell();
            return;
        }

        try
        {
            dispatcher.BeginInvoke(DispatcherPriority.ApplicationIdle, Tell);
        }
        catch (Exception)
        {
            Tell();
        }
    }

    private static void Log(string where, Exception e)
    {
        var id = where + "|" + e.GetType().FullName + "|" + e.Message;
        lock (Seen)
        {
            Seen.TryGetValue(id, out var n);
            Seen[id] = n + 1;
            if (n >= MaxSameEntries)
            {
                return;
            }
        }

        ErrorLog.Write(where, e);
    }

    private static void Show(string text)
    {
        try
        {
            MessageBox.Show(text, AppInfo.Name, MessageBoxButton.OK, MessageBoxImage.Error, MessageBoxResult.OK, MessageBoxOptions.RtlReading | MessageBoxOptions.RightAlign);
        }
        catch (Exception)
        {
            // nothing more to try: the folder with the log is already open
        }
    }
}
