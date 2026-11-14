// Copyright (C) 2026 ChikApps
// SPDX-License-Identifier: GPL-3.0-only
//
// Wires the engine (Earbuds.Core via Earbuds.Windows) to the tray icon and the windows.

using System.Windows;
using System.Windows.Threading;
using Earbuds.App.Platform;
using Earbuds.App.Theme;
using Earbuds.App.Views;
using Earbuds.Core;
using Earbuds.Core.Notifications;
using Earbuds.Core.Settings;
using Earbuds.Core.Updates;
using Earbuds.Presentation;
using Earbuds.Windows;

namespace Earbuds.App;

public sealed class AppController : ISettingsHost, IDisposable
{
    private readonly Application _app;
    private readonly bool _startedByWindows;
    private readonly string _settingsPath = WinPlatform.SettingsPath(AppInfo.DataFolderName);
    private readonly DispatcherTimer _tick = new() { Interval = TimeSpan.FromSeconds(1) };
    private readonly Toasts _toasts = new();
    private EarbudsMonitor _monitor = null!;
    private TrayIcon _tray = null!;
    private HotkeyHost? _hotkey;
    private MainFlyout? _main;
    private PopupWindow? _popup;
    private WizardWindow? _wizard;
    private SettingsWindow? _settingsWindow;
    private AboutWindow? _about;
    private LicensesWindow? _licenses;

    public AppController(Application app, string[] args)
    {
        _app = app;
        _startedByWindows = StartupCommand.IsStartupLaunch(args);
    }

    public event EventHandler? SettingsChanged;

    public AppSettings Settings => _monitor.Settings;

    public bool HotkeyBusy { get; private set; }

    public bool SaveFailed { get; private set; }

    private static DateTimeOffset Now => DateTimeOffset.UtcNow;

    public async Task StartAsync()
    {
        ThemeManager.Current.Initialize(_app);
        var settings = SettingsSerializer.Load(_settingsPath);
        _monitor = WinPlatform.CreateMonitor(settings);
        _monitor.SnapshotChanged += (_, _) => _app.Dispatcher.BeginInvoke(Refresh);
        _monitor.PopupRequested += (_, _) => _app.Dispatcher.BeginInvoke(ShowPopup);
        _monitor.PopupCloseRequested += (_, _) => _app.Dispatcher.BeginInvoke(() => _popup?.Close());
        _monitor.LowBattery += (_, alert) => _app.Dispatcher.BeginInvoke(() => ShowLowBattery(alert));
        _monitor.SettingsChanged += (_, s) => _app.Dispatcher.BeginInvoke(() => Save(s));

        _tray = new TrayIcon(ShowMain, ShowSettings, ShowAbout, Exit);
        _tray.LeftClick += (_, _) => ToggleMain();
        _toasts.Register();
        _toasts.Clicked += (_, _) => _app.Dispatcher.BeginInvoke(ShowMain);
        StartupRegistration.Apply(settings.StartWithWindows);
        try
        {
            _hotkey = new HotkeyHost();
            _hotkey.Pressed += (_, _) => ToggleMain();
            if (settings.HotkeyEnabled)
            {
                HotkeyBusy = !_hotkey.Register(settings.Hotkey);
            }
        }
        catch (Exception e)
        {
            ErrorLog.Write("hotkey", e);
        }

        Refresh();
        await _monitor.StartAsync();
        _tick.Tick += (_, _) =>
        {
            _monitor.Tick();
            Refresh();
        };
        _tick.Start();

        // First run: the wizard opens by itself (spec §3). Not when Windows starts us at sign-in.
        if (settings.MyDevice is null && !_startedByWindows)
        {
            ShowWizard();
        }
    }

    // ---------- refresh ----------

    private void Refresh()
    {
        var snapshot = _monitor.GetSnapshot();
        var now = Now;
        _tray.Update(TrayBuilder.Build(snapshot, now));
        if (_main?.IsVisible == true)
        {
            _main.Update(PanelBuilder.Build(snapshot, now, popup: false, pauseEnabled: _monitor.Settings.PauseOnRemove));
        }

        if (_popup?.IsVisible == true)
        {
            _popup.Update(PanelBuilder.Build(snapshot, now, popup: true));
        }
    }

    // ---------- panels near the clock ----------

    public void ToggleMain()
    {
        if (_main?.IsVisible == true)
        {
            _main.HidePanel();
            return;
        }

        // the click on the icon also deactivated (and hid) the window a moment ago: that click meant "close"
        if (_main is not null && DateTime.UtcNow - _main.HiddenAt < TimeSpan.FromMilliseconds(400))
        {
            return;
        }

        ShowMain();
    }

    public void ShowMain()
    {
        _popup?.Close();
        _main ??= new MainFlyout(ShowSettings, ShowWizard);
        var model = PanelBuilder.Build(_monitor.GetSnapshot(), Now, popup: false, pauseEnabled: _monitor.Settings.PauseOnRemove);
        if (_main.IsVisible)
        {
            _main.Update(model);
            _main.Activate();
            return;
        }

        _main.Open(model);
    }

    private void ShowPopup()
    {
        if (_main?.IsVisible == true || _wizard is not null)
        {
            return; // the main window already shows the battery
        }

        var model = PanelBuilder.Build(_monitor.GetSnapshot(), Now, popup: true);
        if (model.Body != PanelBody.Gauges)
        {
            return;
        }

        if (_popup is { IsVisible: true })
        {
            _popup.Update(model);
            return;
        }

        _popup = new PopupWindow(ShowWizard);
        _popup.Closed += (s, _) =>
        {
            if (ReferenceEquals(s, _popup))
            {
                _popup = null;
            }
        };
        _popup.Show(model);
    }

    private void ShowLowBattery(LowBatteryAlert alert)
    {
        var snapshot = _monitor.GetSnapshot();
        var family = PanelBuilder.FamilyOf(snapshot.Latest?.Model ?? snapshot.Model);
        _toasts.ShowLowBattery(S.ToastTitle, Texts.ToastBody(alert, snapshot.Latest), family, ThemeManager.Current.AppsDark);
    }

    // ---------- regular windows ----------

    public void ShowWizard()
    {
        _popup?.Close();
        _main?.HidePanel();
        if (_wizard is not null)
        {
            _wizard.Bring();
            return;
        }

        _wizard = new WizardWindow(_monitor);
        _wizard.Closed += (_, _) =>
        {
            _wizard = null;
            Refresh();
        };
        _wizard.Show();
        _wizard.Activate();
    }

    public void ShowSettings()
    {
        _main?.HidePanel();
        if (_settingsWindow is not null)
        {
            _settingsWindow.Bring();
            return;
        }

        _settingsWindow = new SettingsWindow(this);
        _settingsWindow.Closed += (_, _) => _settingsWindow = null;
        _settingsWindow.Show();
        _settingsWindow.Activate();
    }

    public void ShowAbout()
    {
        _main?.HidePanel();
        if (_about is not null)
        {
            _about.Bring();
            return;
        }

        _about = new AboutWindow(CheckUpdateAsync, ShowLicenses);
        _about.Closed += (_, _) => _about = null;
        _about.Show();
        _about.Activate();
    }

    private void ShowLicenses()
    {
        if (_licenses is not null)
        {
            _licenses.Bring();
            return;
        }

        _licenses = new LicensesWindow();
        _licenses.Closed += (_, _) => _licenses = null;
        _licenses.Show();
        _licenses.Activate();
    }

    /// <summary>The app's only network access, and only when the user clicks (2026-11-14).</summary>
    private static Task<UpdateResult> CheckUpdateAsync(CancellationToken token)
    {
        AppVersion.TryParse(AppInfo.Version, out var current);
        return new UpdateChecker(new GitHubReleaseSource()).CheckAsync(current!, token);
    }

    // ---------- settings ----------

    private void Save(AppSettings settings)
    {
        try
        {
            SettingsSerializer.Save(_settingsPath, settings);
            SaveFailed = false;
        }
        catch (Exception e)
        {
            ErrorLog.Write("save", e);
            SaveFailed = true;
        }

        SettingsChanged?.Invoke(this, EventArgs.Empty);
        Refresh();
    }

    public void SetStartWithWindows(bool on)
    {
        StartupRegistration.Apply(on);
        _monitor.UpdateSettings(s => s with { StartWithWindows = on });
    }

    public void SetPauseOnRemove(bool on) => _monitor.UpdateSettings(s => s with { PauseOnRemove = on });

    public void SetPopupOnCaseOpen(bool on) => _monitor.UpdateSettings(s => s with { PopupOnCaseOpen = on });

    public void SetLowBatteryAlert(bool on) => _monitor.UpdateSettings(s => s with { LowBatteryAlert = on });

    public bool SetHotkey(bool on)
    {
        var ok = true;
        if (_hotkey is not null)
        {
            if (on)
            {
                ok = _hotkey.Register(_monitor.Settings.Hotkey);
            }
            else
            {
                _hotkey.Unregister();
            }
        }

        HotkeyBusy = on && !ok;
        // Keep "on" in the file even when taken right now: it is tried again at the next start.
        _monitor.UpdateSettings(s => s with { HotkeyEnabled = on });
        return ok;
    }

    public void OpenWizard() => ShowWizard();

    public void ForgetDevice() => _monitor.ForgetDevice();

    // ---------- exit ----------

    public void Exit()
    {
        _tick.Stop();
        _app.Shutdown();
    }

    public void Dispose()
    {
        _tick.Stop();
        _popup?.Close();
        _hotkey?.Dispose();
        _tray?.Dispose();
        _monitor?.Dispose();
        ThemeManager.Current.Dispose();
    }
}
