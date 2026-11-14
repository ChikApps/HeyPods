// Copyright (C) 2026 ChikApps
// SPDX-License-Identifier: GPL-3.0-only
//
// Which Bluetooth audio devices are connected (spec §5 layer 3: pause only when MY AirPods are connected).
// Property names and the FindAll -> BluetoothDevice.FromIdAsync -> CreateFromIdAsync(props) sequence follow
// SpriteOvO/AirPodsDesktop (GPL-3.0) Source/Core/Bluetooth_win.h:62-64 and Bluetooth_win.cpp:150-235.
// Written by us in C#. NOT RUN (no Windows here).

using Earbuds.Core.Platform;
using Windows.Devices.Bluetooth;
using Windows.Devices.Enumeration;

namespace Earbuds.Windows;

/// <summary>Windows implementation of <see cref="IConnectedAudioDevices"/>.</summary>
public sealed class WinConnectedAudioDevices : IConnectedAudioDevices
{
    private const string VendorIdProperty = "System.DeviceInterface.Bluetooth.VendorId";
    private const string ProductIdProperty = "System.DeviceInterface.Bluetooth.ProductId";
    private const string IsConnectedProperty = "System.Devices.Aep.IsConnected";

    private DeviceWatcher? _watcher;

    public event EventHandler? Changed;

    /// <summary>Starts watching paired Bluetooth devices so <see cref="Changed"/> fires on connect/disconnect.</summary>
    public void StartWatching()
    {
        if (_watcher is not null)
        {
            return;
        }

        try
        {
            var watcher = DeviceInformation.CreateWatcher(
                BluetoothDevice.GetDeviceSelectorFromPairingState(true),
                new[] { IsConnectedProperty },
                DeviceInformationKind.AssociationEndpoint);
            watcher.Added += (_, _) => RaiseChanged();
            watcher.Updated += (_, _) => RaiseChanged();
            watcher.Removed += (_, _) => RaiseChanged();
            watcher.Start();
            _watcher = watcher;
        }
        catch (Exception)
        {
            _watcher = null; // without a watcher the monitor still refreshes at start and after the wizard
        }
    }

    public async Task<IReadOnlyList<ConnectedAudioDevice>> GetConnectedAsync()
    {
        var result = new List<ConnectedAudioDevice>();
        try
        {
            var selector = BluetoothDevice.GetDeviceSelectorFromConnectionStatus(BluetoothConnectionStatus.Connected);
            var infos = await DeviceInformation.FindAllAsync(selector);
            foreach (var info in infos)
            {
                try
                {
                    using var device = await BluetoothDevice.FromIdAsync(info.Id);
                    if (device is null || device.ConnectionStatus != BluetoothConnectionStatus.Connected)
                    {
                        continue;
                    }

                    if (device.ClassOfDevice.MajorClass != BluetoothMajorClass.AudioVideo)
                    {
                        continue; // keyboards, phones...: not an audio device
                    }

                    var details = await DeviceInformation.CreateFromIdAsync(
                        device.DeviceInformation.Id, new[] { VendorIdProperty, ProductIdProperty });
                    result.Add(new ConnectedAudioDevice(
                        device.Name ?? string.Empty,
                        ReadUInt16(details, VendorIdProperty),
                        ReadUInt16(details, ProductIdProperty)));
                }
                catch (Exception)
                {
                    // one bad device must not hide the others
                }
            }
        }
        catch (Exception)
        {
            // Bluetooth off or enumeration failed: "nothing connected"
        }

        return result;
    }

    public void Dispose()
    {
        if (_watcher is null)
        {
            return;
        }

        try
        {
            if (_watcher.Status is DeviceWatcherStatus.Started or DeviceWatcherStatus.EnumerationCompleted)
            {
                _watcher.Stop();
            }
        }
        catch (Exception)
        {
            // ignore
        }

        _watcher = null;
    }

    private static ushort ReadUInt16(DeviceInformation info, string key) =>
        info.Properties.TryGetValue(key, out var value) && value is ushort number ? number : (ushort)0;

    private void RaiseChanged() => Changed?.Invoke(this, EventArgs.Empty);
}
