// Copyright (C) 2026 ChikApps
// SPDX-License-Identifier: GPL-3.0-only

using Earbuds.Core.Settings;
using Xunit;

namespace Earbuds.Core.Tests;

public class SettingsTests
{
    [Fact]
    public void Defaults_MatchSpec()
    {
        var s = new AppSettings();
        Assert.True(s.StartWithWindows);
        Assert.True(s.PauseOnRemove);
        Assert.True(s.PopupOnCaseOpen);
        Assert.Null(s.MyDevice);
    }

    [Fact]
    public void RoundTrip()
    {
        var s = new AppSettings { PauseOnRemove = false, MyDevice = new SavedDevice(0x1420, 0x03, "My AirPods") };
        Assert.Equal(s, SettingsSerializer.Deserialize(SettingsSerializer.Serialize(s)));
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("{")]
    [InlineData("not json at all")]
    [InlineData("[1,2,3]")]
    [InlineData("{\"myDevice\": {\"modelCode\": 99999999}}")]
    [InlineData("{\"pauseOnRemove\": \"yes\"}")]
    [InlineData("{\"a\":{\"b\":{\"c\":{\"d\":{\"e\":{\"f\":{\"g\":{\"h\":{\"i\":1}}}}}}}}}")]
    public void Broken_GivesDefaults(string? json) =>
        Assert.Equal(new AppSettings(), SettingsSerializer.Deserialize(json));

    [Fact]
    public void Huge_GivesDefaults() =>
        Assert.Equal(new AppSettings(), SettingsSerializer.Deserialize("{\"x\":\"" + new string('a', 70_000) + "\"}"));

    [Fact]
    public void UnknownFieldsIgnored_KnownKept()
    {
        var s = SettingsSerializer.Deserialize("{\"pauseOnRemove\": false, \"address\": \"AA:BB\"}");
        Assert.False(s.PauseOnRemove);
    }

    [Fact] // spec A4: only these fields are written
    public void FileHasNoAddresses()
    {
        var json = SettingsSerializer.Serialize(new AppSettings { MyDevice = new SavedDevice(0x1420, 0, "Mine") });
        Assert.DoesNotContain("address", json, StringComparison.OrdinalIgnoreCase);
        var keys = System.Text.Json.JsonDocument.Parse(json).RootElement.EnumerateObject().Select(p => p.Name).ToArray();
        Assert.Equal(new[] { "version", "startWithWindows", "pauseOnRemove", "popupOnCaseOpen", "lowBatteryAlert", "hotkeyEnabled", "hotkey", "myDevice" }, keys);
    }

    [Fact]
    public void DeviceName_ControlCharsRemoved_Truncated()
    {
        var s = SettingsSerializer.Deserialize(
            SettingsSerializer.Serialize(new AppSettings { MyDevice = new SavedDevice(1, 0, "A\u0000B\nC" + new string('x', 400)) }));
        Assert.StartsWith("ABC", s.MyDevice!.ConnectedDeviceName);
        Assert.Equal(SettingsSerializer.MaxNameLength, s.MyDevice.ConnectedDeviceName!.Length);
    }

    [Fact]
    public void SaveAndLoad_File()
    {
        var dir = Path.Combine(Path.GetTempPath(), "earbuds-test-" + Guid.NewGuid().ToString("N"));
        try
        {
            var path = Path.Combine(dir, "settings.json");
            Assert.Equal(new AppSettings(), SettingsSerializer.Load(path)); // missing file
            var s = new AppSettings { StartWithWindows = false };
            SettingsSerializer.Save(path, s);
            Assert.Equal(s, SettingsSerializer.Load(path));
            Assert.False(File.Exists(path + ".tmp"));
        }
        finally
        {
            if (Directory.Exists(dir))
            {
                Directory.Delete(dir, recursive: true);
            }
        }
    }
}
