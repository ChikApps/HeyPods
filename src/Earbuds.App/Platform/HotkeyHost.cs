// Copyright (C) 2026 ChikApps
// SPDX-License-Identifier: GPL-3.0-only
//
// Ctrl+Alt+A (2026-11-14): a hidden message-only window receives WM_HOTKEY for Earbuds.Windows.WinGlobalHotkey.

using System.Windows.Interop;
using Earbuds.Core.Hotkeys;
using Earbuds.Windows;

namespace Earbuds.App.Platform;

internal sealed class HotkeyHost : IDisposable
{
    private readonly HwndSource _source;
    private readonly WinGlobalHotkey _hotkey;

    public HotkeyHost()
    {
        _source = new HwndSource(new HwndSourceParameters("HotkeySink") { ParentWindow = new IntPtr(-3), Width = 0, Height = 0, WindowStyle = 0 });
        _hotkey = new WinGlobalHotkey(_source.Handle);
        _source.AddHook(Hook);
        _hotkey.Pressed += (_, _) => Pressed?.Invoke(this, EventArgs.Empty);
    }

    public event EventHandler? Pressed;

    public bool IsRegistered { get; private set; }

    /// <summary>False when another app already uses the shortcut.</summary>
    public bool Register(string text)
    {
        IsRegistered = _hotkey.Register(HotkeyGesture.ParseOrDefault(text));
        return IsRegistered;
    }

    public void Unregister()
    {
        _hotkey.Unregister();
        IsRegistered = false;
    }

    private IntPtr Hook(IntPtr hwnd, int msg, IntPtr wParam, IntPtr lParam, ref bool handled)
    {
        if (_hotkey.HandleMessage(msg, wParam))
        {
            handled = true;
        }

        return IntPtr.Zero;
    }

    public void Dispose()
    {
        _hotkey.Dispose();
        _source.RemoveHook(Hook);
        _source.Dispose();
    }
}
