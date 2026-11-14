// Copyright (C) 2026 ChikApps
// SPDX-License-Identifier: GPL-3.0-only
//
// Keyboard shortcut that opens the main window (2026-11-14): default Ctrl+Alt+A, can be turned off.

namespace Earbuds.Core.Hotkeys;

/// <summary>Modifier keys. Values match the Win32 RegisterHotKey MOD_* flags.</summary>
[Flags]
public enum HotkeyModifiers
{
    None = 0,
    Alt = 0x1,
    Ctrl = 0x2,
    Shift = 0x4,
    Win = 0x8,
}

/// <summary>A shortcut such as "Ctrl+Alt+A". The key is stored as a Win32 virtual-key code.</summary>
public sealed record HotkeyGesture(HotkeyModifiers Modifiers, int VirtualKey)
{
    /// <summary>The default shortcut.</summary>
    public static readonly HotkeyGesture Default = new(HotkeyModifiers.Ctrl | HotkeyModifiers.Alt, 'A');

    /// <summary>Default text, as stored in the settings file.</summary>
    public const string DefaultText = "Ctrl+Alt+A";

    /// <summary>
    /// Parses "Ctrl+Alt+A", "ctrl + shift + F5", "Alt+Win+1". Rules: at least one of Ctrl/Alt/Win
    /// (Shift alone would steal normal typing), exactly one key: A-Z, 0-9 or F1-F12.
    /// </summary>
    public static bool TryParse(string? text, out HotkeyGesture? gesture)
    {
        gesture = null;
        if (string.IsNullOrWhiteSpace(text) || text.Length > 40)
        {
            return false;
        }

        var modifiers = HotkeyModifiers.None;
        int? key = null;
        foreach (var raw in text.Split('+'))
        {
            var part = raw.Trim().ToUpperInvariant();
            HotkeyModifiers mod = part switch
            {
                "CTRL" or "CONTROL" => HotkeyModifiers.Ctrl,
                "ALT" => HotkeyModifiers.Alt,
                "SHIFT" => HotkeyModifiers.Shift,
                "WIN" or "WINDOWS" => HotkeyModifiers.Win,
                _ => HotkeyModifiers.None,
            };

            if (mod != HotkeyModifiers.None)
            {
                if ((modifiers & mod) != 0)
                {
                    return false; // "Ctrl+Ctrl+A"
                }

                modifiers |= mod;
                continue;
            }

            if (key is not null || !TryKey(part, out var vk))
            {
                return false;
            }

            key = vk;
        }

        if (key is null || (modifiers & (HotkeyModifiers.Ctrl | HotkeyModifiers.Alt | HotkeyModifiers.Win)) == 0)
        {
            return false;
        }

        gesture = new HotkeyGesture(modifiers, key.Value);
        return true;
    }

    /// <summary>Parses, or returns <see cref="Default"/> for anything invalid.</summary>
    public static HotkeyGesture ParseOrDefault(string? text) => TryParse(text, out var g) ? g! : Default;

    public override string ToString()
    {
        var parts = new List<string>();
        if (Modifiers.HasFlag(HotkeyModifiers.Ctrl)) parts.Add("Ctrl");
        if (Modifiers.HasFlag(HotkeyModifiers.Alt)) parts.Add("Alt");
        if (Modifiers.HasFlag(HotkeyModifiers.Shift)) parts.Add("Shift");
        if (Modifiers.HasFlag(HotkeyModifiers.Win)) parts.Add("Win");
        parts.Add(VirtualKey is >= 0x70 and <= 0x7B ? "F" + (VirtualKey - 0x6F) : ((char)VirtualKey).ToString());
        return string.Join("+", parts);
    }

    private static bool TryKey(string part, out int vk)
    {
        vk = 0;
        if (part.Length == 1 && (part[0] is >= 'A' and <= 'Z' || part[0] is >= '0' and <= '9'))
        {
            vk = part[0]; // VK_A..VK_Z = 'A'..'Z', VK_0..VK_9 = '0'..'9'
            return true;
        }

        if (part.Length is 2 or 3 && part[0] == 'F' && int.TryParse(part.AsSpan(1), out var n) && n is >= 1 and <= 12 &&
            part[1] != '0')
        {
            vk = 0x6F + n; // VK_F1 = 0x70
            return true;
        }

        return false;
    }
}

/// <summary>A system-wide shortcut (Windows: RegisterHotKey).</summary>
public interface IGlobalHotkey : IDisposable
{
    /// <summary>The shortcut was pressed.</summary>
    event EventHandler? Pressed;

    /// <summary>Registers the shortcut (replacing a previous one). False when another app already uses it.</summary>
    bool Register(HotkeyGesture gesture);

    void Unregister();
}
