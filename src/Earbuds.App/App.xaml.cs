// Copyright (C) 2026 ChikApps
// SPDX-License-Identifier: GPL-3.0-only

using System.Windows;
using System.Windows.Threading;
using Earbuds.App.Platform;

namespace Earbuds.App;

public partial class App : Application
{
    // true once the first window has been laid out and the app is idle. Before that, any error = "could not start".
    private static volatile bool _startupDone;

    private SingleInstance? _instance;
    private AppController? _controller;

    static App()
    {
        // Registered before the generated Main loads App.xaml, so even an error inside App.xaml / Styles.xaml is
        // logged in full and explained, instead of Windows' own crash dialog.
        AppDomain.CurrentDomain.UnhandledException += (_, a) =>
        {
            var e = a.ExceptionObject as Exception ?? new InvalidOperationException("unknown error object: " + a.ExceptionObject);
            if (_startupDone)
            {
                FailureReport.Runtime("domain", e);
            }
            else
            {
                FailureReport.Startup("domain", e);
            }
        };
    }

    protected override async void OnStartup(StartupEventArgs e)
    {
        base.OnStartup(e);
        DispatcherUnhandledException += OnDispatcherError;
        TaskScheduler.UnobservedTaskException += (_, a) =>
        {
            a.SetObserved();
            FailureReport.Runtime("task", a.Exception);
        };

        try
        {
            _instance = new SingleInstance();
            if (!_instance.IsFirst)
            {
                _instance.SignalFirst(); // already running: open its window and leave
                Shutdown();
                return;
            }

            _controller = new AppController(this, e.Args);
            _instance.Listen(() => Dispatcher.BeginInvoke(() => _controller?.ShowMain()));
            await _controller.StartAsync();

            // the first window is laid out later, by the dispatcher: an error there still counts as a start-up error
            _ = Dispatcher.BeginInvoke(DispatcherPriority.ApplicationIdle, () => _startupDone = true);
        }
        catch (Exception ex)
        {
            _startupDone = true;
            FailureReport.Startup("startup", ex);
            Shutdown();
        }
    }

    protected override void OnExit(ExitEventArgs e)
    {
        _controller?.Dispose();
        _instance?.Dispose();
        base.OnExit(e);
    }

    private void OnDispatcherError(object sender, DispatcherUnhandledExceptionEventArgs e)
    {
        e.Handled = true;
        if (!_startupDone)
        {
            FailureReport.Startup("ui-startup", e.Exception);
            Shutdown();
            return;
        }

        // A bug in one window must not take the tray icon away: log it locally, tell the user once, keep running.
        FailureReport.Runtime("ui", e.Exception);
    }
}
