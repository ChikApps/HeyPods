// Copyright (C) 2026 ChikApps
// SPDX-License-Identifier: GPL-3.0-only

using System.Text.Json;
using System.Text.Json.Serialization;
using Earbuds.Core.Tracking;

namespace Earbuds.Core.Settings;

/// <summary>
/// Settings file (%AppData%\[name]\settings.json, spec §6). Only these fields: no Bluetooth addresses,
/// no names of other people's devices (spec A4).
/// </summary>
public sealed record AppSettings
{
    public int Version { get; init; } = 1;
    public bool StartWithWindows { get; init; } = true;
    public bool PauseOnRemove { get; init; } = true;
    public bool PopupOnCaseOpen { get; init; } = true;
    /// <summary>Windows notification when a pod drops to 20% (2026-11-14).</summary>
    public bool LowBatteryAlert { get; init; } = true;
    /// <summary>Keyboard shortcut that opens the main window (2026-11-14).</summary>
    public bool HotkeyEnabled { get; init; } = true;
    /// <summary>The shortcut text; invalid text falls back to the default.</summary>
    public string Hotkey { get; init; } = Hotkeys.HotkeyGesture.DefaultText;
    public SavedDevice? MyDevice { get; init; }

    public MyDeviceProfile? ToProfile() => MyDevice?.ToProfile();
}

/// <summary>Saved "my AirPods". Model and color as numbers, plus the Windows device name if there was one.</summary>
public sealed record SavedDevice(ushort ModelCode, byte ColorCode, string? ConnectedDeviceName)
{
    public MyDeviceProfile ToProfile() => new(ModelCode, ColorCode, ConnectedDeviceName);

    public static SavedDevice From(MyDeviceProfile profile) =>
        new(profile.ModelCode, profile.ColorCode, profile.ConnectedDeviceName);
}

/// <summary>Reads and writes <see cref="AppSettings"/>. A missing or broken file gives the defaults, never an exception.</summary>
public static class SettingsSerializer
{
    /// <summary>Settings files bigger than this are treated as broken.</summary>
    public const int MaxFileBytes = 64 * 1024;

    /// <summary>Longest device name we keep (Windows allows 248).</summary>
    public const int MaxNameLength = 248;

    private static readonly JsonSerializerOptions Options = new()
    {
        WriteIndented = true,
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
        DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull,
        UnmappedMemberHandling = JsonUnmappedMemberHandling.Skip,
        MaxDepth = 8,
    };

    public static string Serialize(AppSettings settings) => JsonSerializer.Serialize(Sanitize(settings), Options);

    public static AppSettings Deserialize(string? json)
    {
        if (string.IsNullOrWhiteSpace(json) || json.Length > MaxFileBytes)
        {
            return new AppSettings();
        }

        try
        {
            return Sanitize(JsonSerializer.Deserialize<AppSettings>(json, Options) ?? new AppSettings());
        }
        catch (Exception e) when (e is JsonException or NotSupportedException or ArgumentException or InvalidOperationException)
        {
            return new AppSettings();
        }
    }

    /// <summary>Loads from a file path. Missing, unreadable, huge or corrupt -> defaults.</summary>
    public static AppSettings Load(string path)
    {
        try
        {
            var info = new FileInfo(path);
            if (!info.Exists || info.Length > MaxFileBytes)
            {
                return new AppSettings();
            }

            return Deserialize(File.ReadAllText(path));
        }
        catch (Exception e) when (e is IOException or UnauthorizedAccessException or System.Security.SecurityException)
        {
            return new AppSettings();
        }
    }

    /// <summary>Saves atomically (temp file + replace) so a crash never leaves half a file.</summary>
    public static void Save(string path, AppSettings settings)
    {
        var directory = Path.GetDirectoryName(path);
        if (!string.IsNullOrEmpty(directory))
        {
            Directory.CreateDirectory(directory);
        }

        var temp = path + ".tmp";
        File.WriteAllText(temp, Serialize(settings));
        File.Move(temp, path, overwrite: true);
    }

    private static AppSettings Sanitize(AppSettings settings)
    {
        if (!Hotkeys.HotkeyGesture.TryParse(settings.Hotkey, out var gesture))
        {
            settings = settings with { Hotkey = Hotkeys.HotkeyGesture.DefaultText };
        }
        else
        {
            settings = settings with { Hotkey = gesture!.ToString() };
        }

        if (settings.MyDevice is { ConnectedDeviceName: { } name } device)
        {
            var clean = new string(name.Where(c => !char.IsControl(c)).Take(MaxNameLength).ToArray());
            settings = settings with { MyDevice = device with { ConnectedDeviceName = clean.Length == 0 ? null : clean } };
        }

        return settings;
    }
}
