// Copyright (C) 2026 ChikApps
// SPDX-License-Identifier: GPL-3.0-only

using System.Windows;
using System.Windows.Threading;
using Earbuds.App.Platform;

namespace Earbuds.App;

public partial class App : Application
{
    private SingleInstance? _instance;
    private AppController? _controller;

    protected override async void OnStartup(StartupEventArgs e)
    {
        base.OnStartup(e);
        DispatcherUnhandledException += OnDispatcherError;
        AppDomain.CurrentDomain.UnhandledException += (_, a) => ErrorLog.Write("domain", a.ExceptionObject as Exception ?? new Exception("unknown"));
        TaskScheduler.UnobservedTaskException += (_, a) =>
        {
            ErrorLog.Write("task", a.Exception);
            a.SetObserved();
        };

        _instance = new SingleInstance();
        if (!_instance.IsFirst)
        {
            _instance.SignalFirst(); // already running: open its window and leave
            Shutdown();
            return;
        }

        try
        {
            _controller = new AppController(this, e.Args);
            _instance.Listen(() => Dispatcher.BeginInvoke(() => _controller?.ShowMain()));
            await _controller.StartAsync();
        }
        catch (Exception ex)
        {
            ErrorLog.Write("startup", ex);
            MessageBox.Show(Presentation.AppInfo.Name + ": " + ex.Message, Presentation.AppInfo.Name, MessageBoxButton.OK, MessageBoxImage.Error, MessageBoxResult.OK, MessageBoxOptions.RtlReading | MessageBoxOptions.RightAlign);
            Shutdown();
        }
    }

    protected override void OnExit(ExitEventArgs e)
    {
        _controller?.Dispose();
        _instance?.Dispose();
        base.OnExit(e);
    }

    private static void OnDispatcherError(object sender, DispatcherUnhandledExceptionEventArgs e)
    {
        // A bug in one window must not take the tray icon away: log it locally and keep running.
        ErrorLog.Write("ui", e.Exception);
        e.Handled = true;
    }
}
