// Copyright (C) 2026 ChikApps
// SPDX-License-Identifier: GPL-3.0-only
//
// Where the popup and the main window open: 12 px from the taskbar, next to the clock
// (design §3; in Hebrew Windows the clock is on the left). Pure math, in physical pixels.

namespace Earbuds.Presentation;

public enum TaskbarEdge
{
    Bottom,
    Top,
    Left,
    Right,
}

public readonly record struct PixelRect(int Left, int Top, int Right, int Bottom)
{
    public int Width => Right - Left;
    public int Height => Bottom - Top;
}

public static class Placement
{
    /// <summary>Gap between the window and the taskbar / screen edge, in DIPs (design: 12).</summary>
    public const double GapDip = 12;

    /// <summary>
    /// Top-left corner (pixels) for a window of <paramref name="width"/> × <paramref name="height"/> pixels.
    /// </summary>
    /// <param name="workArea">Monitor work area (screen minus taskbar).</param>
    /// <param name="edge">Which edge the taskbar is on.</param>
    /// <param name="clockOnLeft">True when the clock (notification area) is on the left half of the taskbar.</param>
    /// <param name="gap">Gap in pixels.</param>
    /// <param name="shadow">Invisible shadow margin around the window, in pixels (the gap is measured from the visible edge).</param>
    public static (int X, int Y) Compute(PixelRect workArea, TaskbarEdge edge, bool clockOnLeft, int width, int height, int gap, int shadow = 0)
    {
        var g = gap - shadow;
        int x, y;
        switch (edge)
        {
            case TaskbarEdge.Top:
                x = clockOnLeft ? workArea.Left + g : workArea.Right - g - width;
                y = workArea.Top + g;
                break;
            case TaskbarEdge.Left:
                x = workArea.Left + g;
                y = workArea.Bottom - g - height;
                break;
            case TaskbarEdge.Right:
                x = workArea.Right - g - width;
                y = workArea.Bottom - g - height;
                break;
            default:
                x = clockOnLeft ? workArea.Left + g : workArea.Right - g - width;
                y = workArea.Bottom - g - height;
                break;
        }

        // Never off-screen, even when the window is taller than the work area.
        x = Math.Max(workArea.Left - shadow, Math.Min(x, workArea.Right + shadow - width));
        y = Math.Max(workArea.Top - shadow, Math.Min(y, workArea.Bottom + shadow - height));
        return (x, y);
    }

    /// <summary>Which side the clock is on, from the notification-area rectangle inside the taskbar.</summary>
    public static bool IsClockOnLeft(PixelRect taskbar, PixelRect notifyArea) =>
        notifyArea.Left + notifyArea.Width / 2 < taskbar.Left + taskbar.Width / 2;
}

/// <summary>Command line of the startup entry (HKCU\...\Run).</summary>
public static class StartupCommand
{
    /// <summary>Argument added when Windows starts the app at sign-in (no wizard then).</summary>
    public const string Flag = "--startup";

    public static string Build(string exePath) => "\"" + exePath + "\" " + Flag;

    public static bool IsStartupLaunch(IEnumerable<string> args) =>
        args.Any(a => string.Equals(a, Flag, StringComparison.OrdinalIgnoreCase));

    /// <summary>True when the stored command already points at this exe (case-insensitive, quotes ignored).</summary>
    public static bool PointsTo(string? stored, string exePath) =>
        stored is not null && string.Equals(stored.Trim(), Build(exePath), StringComparison.OrdinalIgnoreCase);
}
