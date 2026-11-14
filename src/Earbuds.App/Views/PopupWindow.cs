// Copyright (C) 2026 ChikApps
// SPDX-License-Identifier: GPL-3.0-only
//
// Popup when my case opens nearby (spec §2): above the clock, never takes focus, closes after 8 seconds,
// when the case closes, or with X. "Not your AirPods?" opens the wizard.

using System.Windows;
using System.Windows.Controls;
using System.Windows.Threading;
using Earbuds.App.Controls;
using Earbuds.App.Platform;
using Earbuds.Core.Tracking;
using Earbuds.Presentation;

namespace Earbuds.App.Views;

public sealed class PopupWindow : FlyoutWindow
{
    private readonly PanelView _panel = new() { RingSize = RingSize.Popup, ArtHeight = 150, MaxArtHeight = 133 };
    private readonly TextBlock _title = new();
    private readonly DispatcherTimer _timer = new() { Interval = Thresholds.PopupDuration };

    public PopupWindow(Action notMine)
        : base(activate: false)
    {
        Title = AppInfo.Name;
        var stack = new StackPanel { Margin = new Thickness(16, 14, 16, 0) };
        stack.Children.Add(Header(_title, CloseButton(Close)));
        stack.Children.Add(_panel);
        var link = Ui.Link(S.NotMine, (_, _) =>
        {
            Close();
            notMine();
        });
        link.HorizontalAlignment = HorizontalAlignment.Center;
        link.Margin = new Thickness(0, 6, 0, 14);
        stack.Children.Add(link);
        Body = stack;

        SourceInitialized += (_, _) => FlyoutPositioner.MakeNoActivate(this);
        _timer.Tick += (_, _) => Close();
        // While the pointer is on the popup it stays (so a click on the link is not lost); 8 s again after it leaves.
        MouseEnter += (_, _) => _timer.Stop();
        MouseLeave += (_, _) => _timer.Start();
        Closed += (_, _) => _timer.Stop();
    }

    public void Show(PanelModel model)
    {
        Update(model);
        ShowNearClock();
        _timer.Start();
    }

    public void Update(PanelModel model)
    {
        _title.Text = model.Title;
        // Latin names ("AirPods 3") run left-to-right; "Left" inside an RTL parent is the visual right edge.
        _title.FlowDirection = model.TitleIsLatin ? FlowDirection.LeftToRight : FlowDirection.RightToLeft;
        _title.HorizontalAlignment = model.TitleIsLatin ? HorizontalAlignment.Left : HorizontalAlignment.Stretch;
        _panel.Show(model);
    }

    protected override void OnShown() => _panel.AnimateIn();
}
