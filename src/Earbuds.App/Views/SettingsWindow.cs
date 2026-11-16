// Copyright (C) 2026 ChikApps
// SPDX-License-Identifier: GPL-3.0-only
//
// Settings (spec §6 + 2026-11-14; design 21-22): five switches saved at once (no "save" button),
// "my AirPods" with change / forget (forget asks first).

using System.Windows;
using System.Windows.Controls;
using System.Windows.Controls.Primitives;
using Earbuds.App.Art;
using Earbuds.App.Controls;
using Earbuds.App.Theme;
using Earbuds.Core.Models;
using Earbuds.Core.Settings;
using Earbuds.Presentation;

namespace Earbuds.App.Views;

/// <summary>What the settings window needs from the app.</summary>
public interface ISettingsHost
{
    AppSettings Settings { get; }

    /// <summary>The shortcut is on in the settings but another app holds it.</summary>
    bool HotkeyBusy { get; }

    /// <summary>The last save to disk failed.</summary>
    bool SaveFailed { get; }

    event EventHandler? SettingsChanged;

    void SetStartWithWindows(bool on);

    void SetPauseOnRemove(bool on);

    void SetPopupOnCaseOpen(bool on);

    void SetLowBatteryAlert(bool on);

    /// <summary>Returns false when the shortcut is taken by another app.</summary>
    bool SetHotkey(bool on);

    void OpenWizard();

    void ForgetDevice();
}

public sealed class SettingsWindow : AppWindow
{
    private readonly ISettingsHost _host;
    private bool _refreshing;

    public SettingsWindow(ISettingsHost host)
        : base(S.SetTitle)
    {
        _host = host;
        _host.SettingsChanged += OnChanged;
        Closed += (_, _) => _host.SettingsChanged -= OnChanged;
        ThemeManager.Current.Changed += OnChanged;
        Closed += (_, _) => ThemeManager.Current.Changed -= OnChanged;
        Build();
    }

    private void OnChanged(object? sender, EventArgs e) => Dispatcher.BeginInvoke(Build);

    private void Build()
    {
        _refreshing = true;
        var s = _host.Settings;
        var body = new StackPanel();
        body.Children.Add(Ui.Text(S.SetTitle, "Heading"));
        if (_host.SaveFailed)
        {
            body.Children.Add(Ui.Notice(true, Ui.Text(S.SetSaveFailed, size: 12)));
        }

        body.Children.Add(Toggle(S.SetStartup, S.SetStartupHint, s.StartWithWindows, _host.SetStartWithWindows));
        body.Children.Add(Toggle(S.SetPause, S.SetPauseHint, s.PauseOnRemove, _host.SetPauseOnRemove));
        body.Children.Add(Toggle(S.SetPopup, null, s.PopupOnCaseOpen, _host.SetPopupOnCaseOpen));
        body.Children.Add(Toggle(S.SetLowBattery, S.SetLowBatteryHint, s.LowBatteryAlert, _host.SetLowBatteryAlert));

        var hotkeyOn = s.HotkeyEnabled && !_host.HotkeyBusy;
        var keys = new StackPanel { Orientation = Orientation.Horizontal, FlowDirection = FlowDirection.LeftToRight, HorizontalAlignment = HorizontalAlignment.Right, Margin = new Thickness(0, 6, 0, 0) };
        var parts = s.Hotkey.Split('+');
        for (var i = 0; i < parts.Length; i++)
        {
            if (i > 0)
            {
                keys.Children.Add(new TextBlock { Text = "+", Margin = new Thickness(4, 0, 4, 0), FontSize = 12, VerticalAlignment = VerticalAlignment.Center, Foreground = (System.Windows.Media.Brush)FindResource("Sec") });
            }

            var kbd = new Border { MinWidth = 26, Padding = new Thickness(7, 1, 7, 1), CornerRadius = new CornerRadius(4), BorderThickness = new Thickness(1, 1, 1, 2), Child = new TextBlock { Text = parts[i], FontSize = 12, TextAlignment = TextAlignment.Center } };
            kbd.SetResourceReference(Border.BackgroundProperty, "Ctl");
            kbd.SetResourceReference(Border.BorderBrushProperty, "CtlStroke");
            keys.Children.Add(kbd);
        }

        var extra = new List<UIElement> { keys };
        if (_host.HotkeyBusy && s.HotkeyEnabled)
        {
            var busy = Ui.Text(S.SetHotkeyBusy, size: 12, brush: "Low");
            busy.Margin = new Thickness(0, 6, 0, 0);
            extra.Add(busy);
        }

        body.Children.Add(Toggle(S.SetHotkey, S.SetHotkeyHint, hotkeyOn, on =>
        {
            if (!_host.SetHotkey(on))
            {
                Dispatcher.BeginInvoke(Build); // taken: the switch goes back off with the explanation
            }
        }, extra.ToArray()));

        body.Children.Add(Ui.Text(S.SetMine, "SubHeading"));
        body.Children.Add(Mine(s));
        Body = body;
        _refreshing = false;
    }

    private Border Toggle(string title, string? hint, bool value, Action<bool> changed, params UIElement[] extra)
    {
        var dock = new DockPanel();
        var label = Ui.Text(value ? S.SetOn : S.SetOff, size: 13, brush: "Sec", align: TextAlignment.Center);
        label.Width = 32;
        label.VerticalAlignment = VerticalAlignment.Center;
        label.Margin = new Thickness(14, 0, 14, 0);
        var toggle = new ToggleButton { Style = Ui.Res<Style>("Switch"), IsChecked = value, VerticalAlignment = VerticalAlignment.Center };
        System.Windows.Automation.AutomationProperties.SetName(toggle, title);
        toggle.Checked += (_, _) => Changed(true);
        toggle.Unchecked += (_, _) => Changed(false);
        void Changed(bool on)
        {
            label.Text = on ? S.SetOn : S.SetOff;
            if (!_refreshing)
            {
                changed(on);
            }
        }

        DockPanel.SetDock(toggle, Dock.Right);
        DockPanel.SetDock(label, Dock.Right);
        dock.Children.Add(toggle);
        dock.Children.Add(label);
        var info = new StackPanel { VerticalAlignment = VerticalAlignment.Center };
        info.Children.Add(Ui.Text(title));
        if (hint is not null)
        {
            var h = Ui.Text(hint, "Hint");
            h.Margin = new Thickness(0, 2, 0, 0);
            info.Children.Add(h);
        }

        foreach (var e in extra)
        {
            info.Children.Add(e);
        }

        dock.Children.Add(info);
        return new Border { Style = Ui.Res<Style>("CardBox"), Child = dock };
    }

    private Border Mine(AppSettings s)
    {
        var stack = new StackPanel();
        var buttons = new WrapPanel { Margin = new Thickness(0, 12, 0, 0) };
        if (s.MyDevice is { } device)
        {
            var model = ModelCatalog.Find(device.ModelCode);
            var row = new DockPanel();
            var art = new Image
            {
                Source = ArtLibrary.Scene(PanelBuilder.Finished(model), ThemeManager.Current.AppsDark),
                Height = PanelBuilder.FamilyOf(model) == ArtFamily.Max ? 60 : 70,
                FlowDirection = FlowDirection.LeftToRight,
                Margin = new Thickness(0, 0, 14, 0),
            };
            DockPanel.SetDock(art, Dock.Left);
            row.Children.Add(art);
            var text = new StackPanel { VerticalAlignment = VerticalAlignment.Center };
            var name = Ui.Text(PanelBuilder.ModelName(model), semibold: true);
            if (!model.IsUnknown)
            {
                Ui.Latin(name);
            }

            text.Children.Add(name);
            if (S.ColorName(DeviceColors.TryGet(device.ColorCode)) is { } color)
            {
                text.Children.Add(Ui.Text(color, "Hint"));
            }

            row.Children.Add(text);
            stack.Children.Add(row);
            var change = Ui.Button(S.SetChange, "Btn", (_, _) => _host.OpenWizard());
            change.Margin = new Thickness(0, 0, 8, 0);
            buttons.Children.Add(change);
            buttons.Children.Add(Ui.Button(S.SetForget, "BtnDanger", (_, _) =>
                ShowDialog(S.SetForgetHead, S.SetForgetBody, S.SetForgetYes, S.SetForgetNo, _host.ForgetDevice)));
        }
        else
        {
            stack.Children.Add(Ui.Text(S.StNoChoice));
            buttons.Children.Add(Ui.Button(S.StNoChoiceBtn, "BtnPrimary", (_, _) => _host.OpenWizard()));
        }

        stack.Children.Add(buttons);
        return new Border { Style = Ui.Res<Style>("CardBox"), Child = stack };
    }
}
