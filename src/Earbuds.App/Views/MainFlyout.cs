// Copyright (C) 2026 ChikApps
// SPDX-License-Identifier: GPL-3.0-only
//
// Main window (click on the tray icon, or Ctrl+Alt+A): model name, drawing, rings with where each pod is,
// state messages, "not connected" notice, Pro hint, "updated X ago" bar with the settings button.
// Closes on Esc or a click outside (spec §2).

using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using Earbuds.App.Controls;
using Earbuds.App.Platform;
using Earbuds.Presentation;

namespace Earbuds.App.Views;

public sealed class MainFlyout : FlyoutWindow
{
    private readonly PanelView _panel = new() { RingSize = RingSize.Main, ArtHeight = 117, MaxArtHeight = 106 };
    private readonly TextBlock _title = new();
    private readonly Border _notice;
    private readonly Border _proHint;
    private readonly TextBlock _bar = Ui.Text(string.Empty, "Hint");

    public MainFlyout(Action openSettings, Action choose)
        : base(activate: true)
    {
        Title = AppInfo.Name;
        var root = new StackPanel();
        var inner = new StackPanel { Margin = new Thickness(16, 14, 16, 0) };
        inner.Children.Add(Header(_title, null));
        _panel.ChooseRequested += (_, _) => choose();
        _panel.BluetoothSettingsRequested += (_, _) => Shell.BluetoothSettings();
        inner.Children.Add(_panel);

        var noticeText = Ui.Text(S.StNotConnected + " ", size: 12);
        noticeText.LineHeight = 17;
        var noticeLink = Ui.Link(S.StNotConnectedLink, (_, _) => Shell.BluetoothSettings());
        noticeLink.FontSize = 12;
        noticeLink.HorizontalAlignment = HorizontalAlignment.Left;
        _notice = Ui.Notice(false, noticeText, noticeLink);
        inner.Children.Add(_notice);

        var tip = new DockPanel();
        var tipIcon = Ui.Icon("IcoTip", 16, 16, "Sec");
        tipIcon.Margin = new Thickness(0, 0, 8, 0);
        tip.Children.Add(tipIcon);
        tip.Children.Add(Ui.Text(S.ProHint, size: 13, brush: "Sec"));
        _proHint = new Border { BorderThickness = new Thickness(0, 1, 0, 0), Padding = new Thickness(0, 10, 0, 11), Child = tip };
        _proHint.SetResourceReference(Border.BorderBrushProperty, "Sep");
        inner.Children.Add(_proHint);
        root.Children.Add(inner);

        var gear = Ui.Button(string.Empty, "IconBtn", (_, _) => openSettings());
        gear.Content = Ui.Icon("IcoGear", 16, 16);
        System.Windows.Automation.AutomationProperties.SetName(gear, S.SettingsA11y);
        var bar = new DockPanel { MinHeight = 40 };
        DockPanel.SetDock(gear, Dock.Right);
        bar.Children.Add(gear);
        _bar.VerticalAlignment = VerticalAlignment.Center;
        bar.Children.Add(_bar);
        var barBorder = new Border { Padding = new Thickness(16, 4, 8, 4), BorderThickness = new Thickness(0, 1, 0, 0), Child = bar };
        barBorder.SetResourceReference(Border.BackgroundProperty, "Bar");
        barBorder.SetResourceReference(Border.BorderBrushProperty, "Sep");
        root.Children.Add(barBorder);
        Body = root;

        SourceInitialized += (_, _) => FlyoutPositioner.MakeToolWindow(this);
        KeyDown += (_, e) =>
        {
            if (e.Key == Key.Escape)
            {
                HidePanel();
            }
        };
        Deactivated += (_, _) => HidePanel();
    }

    /// <summary>When the window was hidden by a click outside (so the same click on the tray icon does not reopen it).</summary>
    public DateTime HiddenAt { get; private set; }

    public void HidePanel()
    {
        if (IsVisible)
        {
            HiddenAt = DateTime.UtcNow;
            Hide();
        }
    }

    public void Update(PanelModel model)
    {
        _title.Text = model.Title;
        _title.FlowDirection = model.TitleIsLatin ? FlowDirection.LeftToRight : FlowDirection.RightToLeft;
        _title.HorizontalAlignment = model.TitleIsLatin ? HorizontalAlignment.Left : HorizontalAlignment.Stretch;
        _panel.Show(model);
        _notice.Visibility = model.ShowNotConnected ? Visibility.Visible : Visibility.Collapsed;
        _proHint.Visibility = model.ShowProHint ? Visibility.Visible : Visibility.Collapsed;
        _bar.Text = model.BarText;
    }

    public void Open(PanelModel model)
    {
        Update(model);
        ShowNearClock();
        Activate();
        Native.SetForegroundWindow(new System.Windows.Interop.WindowInteropHelper(this).Handle);
    }

    protected override void OnShown() => _panel.AnimateIn();
}
