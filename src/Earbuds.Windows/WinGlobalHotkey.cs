// Copyright (C) 2026 ChikApps
// SPDX-License-Identifier: GPL-3.0-only
//
// System-wide shortcut through Win32 RegisterHotKey. The WPF app gives us its window handle and forwards
// window messages (HwndSource.AddHook -> HandleMessage). Our own code. NOT RUN (no Windows here).

using System.Runtime.InteropServices;
using Earbuds.Core.Hotkeys;

namespace Earbuds.Windows;

/// <summary>Windows implementation of <see cref="IGlobalHotkey"/>.</summary>
public sealed partial class WinGlobalHotkey : IGlobalHotkey
{
    private const int WmHotkey = 0x0312;
    private const uint ModNoRepeat = 0x4000;

    private readonly IntPtr _windowHandle;
    private readonly int _id;
    private bool _registered;

    /// <param name="windowHandle">Handle of a window owned by the UI thread (e.g. the hidden main window).</param>
    /// <param name="id">Any number 0x0000-0xBFFF, unique within the app.</param>
    public WinGlobalHotkey(IntPtr windowHandle, int id = 0x0A1B)
    {
        _windowHandle = windowHandle;
        _id = id;
    }

    public event EventHandler? Pressed;

    public bool Register(HotkeyGesture gesture)
    {
        Unregister();
        _registered = RegisterHotKey(_windowHandle, _id, (uint)gesture.Modifiers | ModNoRepeat, (uint)gesture.VirtualKey);
        return _registered; // false: another app already uses this shortcut; the UI says so
    }

    public void Unregister()
    {
        if (_registered)
        {
            UnregisterHotKey(_windowHandle, _id);
            _registered = false;
        }
    }

    /// <summary>Call from the window's message hook. Returns true when the message was our shortcut.</summary>
    public bool HandleMessage(int message, IntPtr wParam)
    {
        if (message != WmHotkey || wParam.ToInt64() != _id)
        {
            return false;
        }

        Pressed?.Invoke(this, EventArgs.Empty);
        return true;
    }

    public void Dispose() => Unregister();

    [LibraryImport("user32.dll", SetLastError = true)]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static partial bool RegisterHotKey(IntPtr hWnd, int id, uint fsModifiers, uint vk);

    [LibraryImport("user32.dll", SetLastError = true)]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static partial bool UnregisterHotKey(IntPtr hWnd, int id);
}
