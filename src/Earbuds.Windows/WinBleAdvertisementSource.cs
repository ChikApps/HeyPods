// Copyright (C) 2026 ChikApps
// SPDX-License-Identifier: GPL-3.0-only
//
// BLE scanner on BluetoothLEAdvertisementWatcher. Same approach as JamesCJ60/WinPods (GPL-3.0)
// WinPods/Services/AirPodsAdvertisementScanner.cs (active scanning, Apple company ID 0x004C);
// written by us, with stop/error handling added. NOT RUN: there is no Windows or Bluetooth in the build environment.

using Earbuds.Core.Ble;
using Earbuds.Core.Platform;
using Windows.Devices.Bluetooth;
using Windows.Devices.Bluetooth.Advertisement;
using Windows.Storage.Streams;

namespace Earbuds.Windows;

/// <summary>Windows implementation of <see cref="IBleAdvertisementSource"/>.</summary>
public sealed class WinBleAdvertisementSource : IBleAdvertisementSource
{
    private readonly object _lock = new();
    private BluetoothLEAdvertisementWatcher? _watcher;
    private ScannerStatus _status = ScannerStatus.Stopped;

    public event EventHandler<BleAdvertisement>? Received;
    public event EventHandler<ScannerStatus>? StatusChanged;

    public ScannerStatus Status
    {
        get { lock (_lock) { return _status; } }
    }

    public void Start()
    {
        lock (_lock)
        {
            if (_watcher?.Status == BluetoothLEAdvertisementWatcherStatus.Started)
            {
                return;
            }

            DetachWatcher();
            try
            {
                // A fresh watcher each time: a watcher that stopped with an error (radio off, sleep) is not reused.
                var watcher = new BluetoothLEAdvertisementWatcher { ScanningMode = BluetoothLEScanningMode.Active };
                watcher.Received += OnReceived;
                watcher.Stopped += OnStopped;
                _watcher = watcher;
                watcher.Start();
                SetStatus(watcher.Status == BluetoothLEAdvertisementWatcherStatus.Aborted ? ScannerStatus.Failed : ScannerStatus.Started);
            }
            catch (Exception)
            {
                // No adapter, radio off or access denied: report, never throw (spec: no crash).
                DetachWatcher();
                SetStatus(ScannerStatus.Failed);
            }
        }
    }

    public void Stop()
    {
        lock (_lock)
        {
            DetachWatcher();
            SetStatus(ScannerStatus.Stopped);
        }
    }

    public void Dispose() => Stop();

    private void OnReceived(BluetoothLEAdvertisementWatcher sender, BluetoothLEAdvertisementReceivedEventArgs args)
    {
        try
        {
            foreach (var section in args.Advertisement.GetManufacturerDataByCompanyId(ProximityPairingParser.AppleCompanyId))
            {
                var bytes = ReadBuffer(section.Data);
                if (bytes.Length == 0)
                {
                    continue;
                }

                Received?.Invoke(this, new BleAdvertisement(args.BluetoothAddress, args.RawSignalStrengthInDBm, bytes, args.Timestamp));
            }
        }
        catch (Exception)
        {
            // A strange advertisement must never take the scanner down.
        }
    }

    private void OnStopped(BluetoothLEAdvertisementWatcher sender, BluetoothLEAdvertisementWatcherStoppedEventArgs args)
    {
        lock (_lock)
        {
            if (!ReferenceEquals(sender, _watcher))
            {
                return;
            }

            SetStatus(args.Error == BluetoothError.Success ? ScannerStatus.Stopped : ScannerStatus.Failed);
        }
    }

    private static byte[] ReadBuffer(IBuffer buffer)
    {
        // Oversized data is dropped whole (never truncated into something that might look valid).
        if (buffer.Length == 0 || buffer.Length > ProximityPairingParser.MaxInputLength)
        {
            return Array.Empty<byte>();
        }

        var length = (int)buffer.Length;
        var bytes = new byte[length];
        using var reader = DataReader.FromBuffer(buffer);
        reader.ReadBytes(bytes);
        return bytes;
    }

    private void DetachWatcher()
    {
        if (_watcher is null)
        {
            return;
        }

        var watcher = _watcher;
        _watcher = null;
        watcher.Received -= OnReceived;
        watcher.Stopped -= OnStopped;
        try
        {
            if (watcher.Status == BluetoothLEAdvertisementWatcherStatus.Started)
            {
                watcher.Stop();
            }
        }
        catch (Exception)
        {
            // already stopped by the system
        }
    }

    private void SetStatus(ScannerStatus status)
    {
        if (_status == status)
        {
            return;
        }

        _status = status;
        var handler = StatusChanged;
        if (handler is not null)
        {
            // Raise outside the caller's lock on the thread pool, so handlers may call Start() again.
            ThreadPool.QueueUserWorkItem(_ => handler(this, status));
        }
    }
}
