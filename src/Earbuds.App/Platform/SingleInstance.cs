// Copyright (C) 2026 ChikApps
// SPDX-License-Identifier: GPL-3.0-only
//
// One copy only. Starting the exe again just opens the main window of the running copy.

using Earbuds.Presentation;

namespace Earbuds.App.Platform;

internal sealed class SingleInstance : IDisposable
{
    private readonly Mutex _mutex;
    private readonly EventWaitHandle _show;
    private readonly bool _owned;
    private RegisteredWaitHandle? _wait;

    public SingleInstance()
    {
        _mutex = new Mutex(true, @"Local\" + AppInfo.InstanceId + ".Instance", out _owned);
        _show = new EventWaitHandle(false, EventResetMode.AutoReset, @"Local\" + AppInfo.InstanceId + ".Show");
    }

    public bool IsFirst => _owned;

    /// <summary>Second copy: asks the first one to show itself.</summary>
    public void SignalFirst() => _show.Set();

    /// <summary>First copy: called (on a pool thread) when another copy was started.</summary>
    public void Listen(Action onShow) =>
        _wait = ThreadPool.RegisterWaitForSingleObject(_show, (_, _) => onShow(), null, Timeout.Infinite, executeOnlyOnce: false);

    public void Dispose()
    {
        _wait?.Unregister(null);
        if (_owned)
        {
            _mutex.ReleaseMutex();
        }

        _mutex.Dispose();
        _show.Dispose();
    }
}
