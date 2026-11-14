// Copyright (C) 2026 ChikApps
// SPDX-License-Identifier: GPL-3.0-only
//
// Bluetooth on/off/missing (spec §3 states 7-8). Our own code. NOT RUN (no Windows here).

using Earbuds.Core.Platform;
using Windows.Devices.Bluetooth;
using Windows.Devices.Radios;
using CoreRadioState = Earbuds.Core.Platform.RadioState;
using WinRadioState = Windows.Devices.Radios.RadioState;

namespace Earbuds.Windows;

/// <summary>Windows implementation of <see cref="IBluetoothRadio"/>.</summary>
public sealed class WinBluetoothRadio : IBluetoothRadio
{
    private Radio? _radio;
    private CoreRadioState _state = CoreRadioState.Unknown;

    public event EventHandler<CoreRadioState>? StateChanged;

    public CoreRadioState State => _state;

    public async Task InitializeAsync()
    {
        if (_radio is not null)
        {
            return;
        }

        try
        {
            var adapter = await BluetoothAdapter.GetDefaultAsync();
            if (adapter is null || !adapter.IsLowEnergySupported)
            {
                Set(CoreRadioState.NotSupported);
                return;
            }

            var radio = await adapter.GetRadioAsync();
            if (radio is null)
            {
                Set(CoreRadioState.NotSupported);
                return;
            }

            _radio = radio;
            radio.StateChanged += OnStateChanged;
            Set(Map(radio.State));
        }
        catch (Exception)
        {
            Set(CoreRadioState.Unknown);
        }
    }

    public void Dispose()
    {
        if (_radio is not null)
        {
            _radio.StateChanged -= OnStateChanged;
            _radio = null;
        }
    }

    private void OnStateChanged(Radio sender, object args) => Set(Map(sender.State));

    private static CoreRadioState Map(WinRadioState state) => state switch
    {
        WinRadioState.On => CoreRadioState.On,
        WinRadioState.Off or WinRadioState.Disabled => CoreRadioState.Off,
        _ => CoreRadioState.Unknown,
    };

    private void Set(CoreRadioState state)
    {
        if (_state == state)
        {
            return;
        }

        _state = state;
        StateChanged?.Invoke(this, state);
    }
}
