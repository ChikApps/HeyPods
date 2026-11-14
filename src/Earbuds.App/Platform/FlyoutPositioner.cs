// Copyright (C) 2026 ChikApps
// SPDX-License-Identifier: GPL-3.0-only
//
// Puts the popup and the main window above the clock (design §3: 12 px above the taskbar, next to the clock;
// in Hebrew Windows the clock is on the left). The math is in Presentation/Placement (unit-tested).

using System.Windows;
using System.Windows.Interop;
using Earbuds.Presentation;

namespace Earbuds.App.Platform;

internal static class FlyoutPositioner
{
    /// <param name="shadowDip">Transparent margin around the visible window (for its shadow).</param>
    public static void Place(Window window, double shadowDip)
    {
        try
        {
            var hwnd = new WindowInteropHelper(window).EnsureHandle();
            var (work, edge, clockOnLeft, monitor) = Taskbar();
            double scale = 1;
            if (monitor != IntPtr.Zero && Native.GetDpiForMonitor(monitor, 0, out var dpi, out _) == 0 && dpi > 0)
            {
                scale = dpi / 96.0;
            }

            var size = window.IsLoaded ? new Size(window.ActualWidth, window.ActualHeight) : Measure(window);
            var w = (int)Math.Round(size.Width * scale);
            var h = (int)Math.Round(size.Height * scale);
            var (x, y) = Placement.Compute(work, edge, clockOnLeft, w, h, (int)Math.Round(Placement.GapDip * scale), (int)Math.Round(shadowDip * scale));
            Native.SetWindowPos(hwnd, Native.HWND_TOPMOST, x, y, 0, 0, Native.SWP_NOSIZE | Native.SWP_NOACTIVATE);
        }
        catch (Exception e)
        {
            ErrorLog.Write("place", e);
        }
    }

    private static Size Measure(Window window)
    {
        if (window.Content is FrameworkElement content)
        {
            content.Measure(new Size(double.PositiveInfinity, double.PositiveInfinity));
            return content.DesiredSize;
        }

        return new Size(window.Width, window.Height);
    }

    private static (PixelRect Work, TaskbarEdge Edge, bool ClockOnLeft, IntPtr Monitor) Taskbar()
    {
        var data = new Native.APPBARDATA { cbSize = System.Runtime.InteropServices.Marshal.SizeOf<Native.APPBARDATA>() };
        var found = Native.SHAppBarMessage(Native.ABM_GETTASKBARPOS, ref data) != UIntPtr.Zero;
        var bar = data.rc;
        var edge = !found ? TaskbarEdge.Bottom : data.uEdge switch
        {
            Native.ABE_LEFT => TaskbarEdge.Left,
            Native.ABE_TOP => TaskbarEdge.Top,
            Native.ABE_RIGHT => TaskbarEdge.Right,
            _ => TaskbarEdge.Bottom,
        };

        var monitor = Native.MonitorFromRect(ref bar, Native.MONITOR_DEFAULTTOPRIMARY);
        var info = new Native.MONITORINFO { cbSize = System.Runtime.InteropServices.Marshal.SizeOf<Native.MONITORINFO>() };
        PixelRect work;
        if (Native.GetMonitorInfo(monitor, ref info))
        {
            work = new PixelRect(info.rcWork.Left, info.rcWork.Top, info.rcWork.Right, info.rcWork.Bottom);
        }
        else
        {
            var wa = SystemParameters.WorkArea;
            work = new PixelRect((int)wa.Left, (int)wa.Top, (int)wa.Right, (int)wa.Bottom);
        }

        // Clock side: the notification area window inside the taskbar. Unknown -> left (Hebrew Windows, the spec).
        var clockOnLeft = true;
        var tray = Native.FindWindow("Shell_TrayWnd", null);
        var notify = tray == IntPtr.Zero ? IntPtr.Zero : Native.FindWindowEx(tray, IntPtr.Zero, "TrayNotifyWnd", null);
        if (found && notify != IntPtr.Zero && Native.GetWindowRect(notify, out var n) && n.Right > n.Left)
        {
            clockOnLeft = Placement.IsClockOnLeft(
                new PixelRect(bar.Left, bar.Top, bar.Right, bar.Bottom), new PixelRect(n.Left, n.Top, n.Right, n.Bottom));
        }

        return (work, edge, clockOnLeft, monitor);
    }

    /// <summary>Popup: never takes focus from the window the user works in (spec §2), no taskbar button.</summary>
    public static void MakeNoActivate(Window window)
    {
        var hwnd = new WindowInteropHelper(window).EnsureHandle();
        var style = Native.GetWindowLongPtr(hwnd, Native.GWL_EXSTYLE).ToInt64();
        Native.SetWindowLongPtr(hwnd, Native.GWL_EXSTYLE, new IntPtr(style | Native.WS_EX_NOACTIVATE | Native.WS_EX_TOOLWINDOW));
    }

    public static void MakeToolWindow(Window window)
    {
        var hwnd = new WindowInteropHelper(window).EnsureHandle();
        var style = Native.GetWindowLongPtr(hwnd, Native.GWL_EXSTYLE).ToInt64();
        Native.SetWindowLongPtr(hwnd, Native.GWL_EXSTYLE, new IntPtr(style | Native.WS_EX_TOOLWINDOW));
    }
}
