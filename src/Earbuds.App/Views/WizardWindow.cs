// Copyright (C) 2026 ChikApps
// SPDX-License-Identifier: GPL-3.0-only
//
// Wizard "are these your AirPods?" (spec §2, §5 layer 1; design 15-20): 1 searching (waves; after 20 s a hint),
// 2 one candidate (yes / no -> next) or a list (nearest first), or "no more", 3 done.

using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using System.Windows.Media.Animation;
using System.Windows.Shapes;
using System.Windows.Threading;
using Earbuds.App.Art;
using Earbuds.App.Controls;
using Earbuds.App.Platform;
using Earbuds.App.Theme;
using Earbuds.Core;
using Earbuds.Core.Tracking;
using Earbuds.Presentation;

namespace Earbuds.App.Views;

public sealed class WizardWindow : AppWindow
{
    /// <summary>After the first candidate appears, wait this long for more (several AirPods nearby).</summary>
    private static readonly TimeSpan CollectFor = TimeSpan.FromSeconds(2.5);
    private static readonly TimeSpan SlowAfter = TimeSpan.FromSeconds(20);

    private readonly EarbudsMonitor _monitor;
    private readonly DispatcherTimer _timer = new() { Interval = TimeSpan.FromMilliseconds(500) };
    private DateTime _searchStarted;
    private DateTime? _firstSeen;
    private bool _slowShown;
    private List<Candidate> _frozen = new();
    private int _index;
    private int _selected;
    private bool _done;
    private PanelView? _livePanel;
    private TextBlock? _liveDetails;
    private string _step = string.Empty;

    public WizardWindow(EarbudsMonitor monitor)
        : base(S.WizTitle, minimize: false)
    {
        _monitor = monitor;
        _timer.Tick += (_, _) => Tick();
        Closed += (_, _) =>
        {
            _timer.Stop();
            if (!_done)
            {
                _monitor.CancelWizard();
            }
        };
        Search();
    }

    /// <summary>True after the user picked AirPods.</summary>
    public bool Completed => _done;

    private void Search()
    {
        _monitor.BeginWizard();
        _searchStarted = DateTime.UtcNow;
        _firstSeen = null;
        _slowShown = false;
        ShowSearching(slow: false);
        _timer.Start();
    }

    private void Tick()
    {
        var candidates = _monitor.GetCandidates();
        if (_step == "searching")
        {
            if (candidates.Count > 0)
            {
                _firstSeen ??= DateTime.UtcNow;
                if (DateTime.UtcNow - _firstSeen >= CollectFor)
                {
                    _frozen = candidates.ToList();
                    _index = 0;
                    _selected = 0;
                    if (_frozen.Count == 1)
                    {
                        ShowOne();
                    }
                    else
                    {
                        ShowList();
                    }
                }
            }
            else if (!_slowShown && DateTime.UtcNow - _searchStarted >= SlowAfter)
            {
                _slowShown = true;
                ShowSearching(slow: true);
            }
        }
        else if (_step == "one" && _index < _frozen.Count)
        {
            // keep the battery of the shown candidate live
            var current = _frozen[_index];
            var fresh = candidates.FirstOrDefault(c => c.ModelCode == current.ModelCode && c.ColorCode == current.ColorCode);
            if (fresh is not null)
            {
                _frozen[_index] = fresh;
                _livePanel?.Show(PanelBuilder.ForCandidate(fresh, DateTimeOffset.UtcNow));
                if (_liveDetails is not null)
                {
                    _liveDetails.Text = Texts.WizardDetails(fresh);
                }
            }
        }
    }

    // ---------- views ----------

    private static UIElement Steps(int n)
    {
        var grid = new System.Windows.Controls.Primitives.UniformGrid { Rows = 1, Columns = 3, Margin = new Thickness(0, 8, 0, 14) };
        for (var i = 1; i <= 3; i++)
        {
            var bar = new Border { Height = 3, CornerRadius = new CornerRadius(2), Margin = new Thickness(i == 1 ? 0 : 3, 0, i == 3 ? 0 : 3, 0) };
            bar.SetResourceReference(Border.BackgroundProperty, i <= n ? "Acc" : "Sep");
            grid.Children.Add(bar);
        }

        System.Windows.Automation.AutomationProperties.SetName(grid, string.Format(System.Globalization.CultureInfo.InvariantCulture, S.WizStep, n));
        return grid;
    }

    private static Border Stage(UIElement content, double minHeight = 190, Thickness? padding = null)
    {
        var b = new Border
        {
            CornerRadius = new CornerRadius(8),
            BorderThickness = new Thickness(1),
            MinHeight = minHeight,
            Margin = new Thickness(0, 6, 0, 16),
            Padding = padding ?? new Thickness(0),
            ClipToBounds = true,
            Child = content,
        };
        b.SetResourceReference(Border.BackgroundProperty, "Card");
        b.SetResourceReference(Border.BorderBrushProperty, "CardStroke");
        return b;
    }

    private Button Later() => Ui.Button(S.WizCancel, "Btn", (_, _) => Close());

    private static Image Art(ArtModel art, double height) => new()
    {
        Source = ArtLibrary.Scene(art, ThemeManager.Current.AppsDark),
        Height = height,
        Stretch = Stretch.Uniform,
        FlowDirection = FlowDirection.LeftToRight,
        HorizontalAlignment = HorizontalAlignment.Center,
        VerticalAlignment = VerticalAlignment.Center,
    };

    private void ShowSearching(bool slow)
    {
        _step = "searching";
        var body = new StackPanel();
        body.Children.Add(Steps(1));
        body.Children.Add(Ui.Text(S.Wiz1Head, "Heading"));

        var stage = new Grid();
        var waves = new Grid { HorizontalAlignment = HorizontalAlignment.Center, VerticalAlignment = VerticalAlignment.Center };
        var animate = !slow && SystemParameters.ClientAreaAnimation;
        double[] stillScale = { 1, 1.45, 1.9 }, stillOpacity = { 0.22, 0.14, 0.07 };
        for (var i = 0; i < 3; i++)
        {
            var scale = new ScaleTransform(animate ? 0.7 : stillScale[i], animate ? 0.7 : stillScale[i]);
            var wave = new Ellipse { Width = 120, Height = 120, StrokeThickness = 2, RenderTransformOrigin = new Point(0.5, 0.5), RenderTransform = scale, Opacity = animate ? 0 : stillOpacity[i] };
            wave.SetResourceReference(Shape.StrokeProperty, "Acc");
            if (animate)
            {
                var begin = TimeSpan.FromSeconds(0.8 * i);
                var grow = new DoubleAnimation(0.7, 2.1, TimeSpan.FromSeconds(2.4)) { BeginTime = begin, RepeatBehavior = RepeatBehavior.Forever, EasingFunction = new QuadraticEase { EasingMode = EasingMode.EaseOut } };
                scale.BeginAnimation(ScaleTransform.ScaleXProperty, grow);
                scale.BeginAnimation(ScaleTransform.ScaleYProperty, grow);
                wave.BeginAnimation(OpacityProperty, new DoubleAnimation(0.5, 0, TimeSpan.FromSeconds(2.4)) { BeginTime = begin, RepeatBehavior = RepeatBehavior.Forever });
            }

            waves.Children.Add(wave);
        }

        stage.Children.Add(waves);
        stage.Children.Add(Art(ArtModel.ForCase(ArtFamily.Generic), 173));
        body.Children.Add(Stage(stage));
        body.Children.Add(Ui.Text(S.Wiz1Body));
        if (slow)
        {
            var warn = Ui.Notice(true, Ui.Text(S.Wiz1Slow, size: 12));
            warn.Margin = new Thickness(0, 4, 0, 0);
            body.Children.Add(warn);
        }
        else
        {
            var row = new StackPanel { Orientation = Orientation.Horizontal, Margin = new Thickness(0, 4, 0, 0) };
            var spin = Ui.Spinner();
            spin.Margin = new Thickness(0, 0, 10, 0);
            row.Children.Add(spin);
            row.Children.Add(Ui.Text(S.Wiz1Searching, size: 13, brush: "Sec"));
            body.Children.Add(row);
        }

        Body = body;
        SetFooter(Array.Empty<UIElement>(), new UIElement[] { Later() });
    }

    private void ShowOne()
    {
        _step = "one";
        _timer.Start();
        var c = _frozen[_index];
        var body = new StackPanel();
        body.Children.Add(Steps(2));
        body.Children.Add(Ui.Text(S.Wiz2Head, "Heading"));
        _livePanel = new PanelView { RingSize = RingSize.Popup, ArtHeight = 124, MaxArtHeight = 113 };
        _livePanel.Show(PanelBuilder.ForCandidate(c, DateTimeOffset.UtcNow));
        body.Children.Add(Stage(_livePanel, 0, new Thickness(12, 4, 12, 4)));
        _liveDetails = Ui.Text(Texts.WizardDetails(c));
        body.Children.Add(_liveDetails);
        Body = body;
        Dispatcher.BeginInvoke(_livePanel.AnimateIn, DispatcherPriority.Loaded);

        var yes = Ui.Button(S.Wiz2Yes, "BtnPrimary", async (_, _) => await ChooseAsync(_frozen[_index]));
        var no = Ui.Button(S.Wiz2No, "Btn", (_, _) =>
        {
            _index++;
            if (_index < _frozen.Count)
            {
                ShowOne();
            }
            else
            {
                ShowNone();
            }
        });
        SetFooter(new UIElement[] { yes, no }, new UIElement[] { Later() });
        yes.Focus();
    }

    private void ShowList()
    {
        _step = "list";
        _timer.Stop();
        var body = new StackPanel();
        body.Children.Add(Steps(2));
        body.Children.Add(Ui.Text(S.Wiz2ListHead, "Heading"));
        var list = new StackPanel { Margin = new Thickness(0, 8, 0, 6) };
        System.Windows.Automation.AutomationProperties.SetName(list, S.Wiz2ListHead);
        var cards = new List<Border>();
        for (var i = 0; i < _frozen.Count; i++)
        {
            var index = i;
            var card = Candidate(_frozen[i], i == 0);
            card.MouseLeftButtonUp += (_, _) =>
            {
                _selected = index;
                for (var j = 0; j < cards.Count; j++)
                {
                    Select(cards[j], j == _selected);
                }
            };
            cards.Add(card);
            Select(card, i == _selected);
            list.Children.Add(card);
        }

        body.Children.Add(list);
        body.Children.Add(Ui.Text(S.Wiz2ListHint, size: 13, brush: "Sec"));
        Body = body;
        var yes = Ui.Button(S.Wiz2Yes, "BtnPrimary", async (_, _) => await ChooseAsync(_frozen[_selected]));
        SetFooter(new UIElement[] { yes }, new UIElement[] { Later() });
    }

    private static void Select(Border card, bool on)
    {
        card.BorderThickness = new Thickness(on ? 2 : 1);
        card.Padding = new Thickness(on ? 11 : 12, on ? 7 : 8, on ? 11 : 12, on ? 7 : 8);
        card.SetResourceReference(Border.BorderBrushProperty, on ? "Acc" : "CardStroke");
        var radio = (Grid)((DockPanel)card.Child).Children[0];
        ((Ellipse)radio.Children[0]).Visibility = on ? Visibility.Collapsed : Visibility.Visible;
        radio.Children[1].Visibility = on ? Visibility.Visible : Visibility.Collapsed;
    }

    private static Border Candidate(Candidate c, bool nearest)
    {
        var dock = new DockPanel();
        var radio = new Grid { Width = 20, Height = 20, Margin = new Thickness(0, 0, 12, 0), VerticalAlignment = VerticalAlignment.Center };
        var off = new Ellipse { StrokeThickness = 1 };
        off.SetResourceReference(Shape.StrokeProperty, "TogOff");
        var on = new Grid { Visibility = Visibility.Collapsed };
        var outer = new Ellipse();
        outer.SetResourceReference(Shape.FillProperty, "Acc");
        var dot = new Ellipse { Width = 8, Height = 8 };
        dot.SetResourceReference(Shape.FillProperty, "OnAcc");
        on.Children.Add(outer);
        on.Children.Add(dot);
        radio.Children.Add(off);
        radio.Children.Add(on);
        DockPanel.SetDock(radio, Dock.Left);
        dock.Children.Add(radio);

        var art = Art(PanelBuilder.Finished(c.Model) with { Led = LedState.Off }, PanelBuilder.FamilyOf(c.Model) == ArtFamily.Max ? 42 : 56);
        art.MinWidth = 56;
        art.Margin = new Thickness(12, 0, 0, 0);
        DockPanel.SetDock(art, Dock.Right);
        dock.Children.Add(art);
        if (nearest)
        {
            var near = Ui.Text(S.Wiz2Nearest, size: 11, brush: "Acc");
            near.VerticalAlignment = VerticalAlignment.Center;
            near.Margin = new Thickness(8, 0, 0, 0);
            DockPanel.SetDock(near, Dock.Right);
            dock.Children.Add(near);
        }

        var info = new StackPanel { VerticalAlignment = VerticalAlignment.Center };
        info.Children.Add(Ui.Text(Texts.CandidateName(c), semibold: true));
        info.Children.Add(Ui.Text(Texts.CandidateBattery(c), "Hint"));
        dock.Children.Add(info);

        var card = new Border { CornerRadius = new CornerRadius(6), Margin = new Thickness(0, 0, 0, 4), Child = dock, Cursor = System.Windows.Input.Cursors.Hand };
        card.SetResourceReference(Border.BackgroundProperty, "Card");
        System.Windows.Automation.AutomationProperties.SetName(card, Texts.CandidateName(c) + ", " + Texts.CandidateBattery(c));
        return card;
    }

    private void ShowNone()
    {
        _step = "none";
        _timer.Stop();
        var body = new StackPanel();
        body.Children.Add(Steps(2));
        body.Children.Add(Ui.Text(S.Wiz2Head, "Heading"));
        body.Children.Add(Stage(Art(ArtModel.ForCase(ArtFamily.Generic), 173)));
        body.Children.Add(Ui.Text(S.Wiz2None));
        Body = body;
        var retry = Ui.Button(S.Wiz2Retry, "BtnPrimary", (_, _) => Search());
        SetFooter(new UIElement[] { retry }, new UIElement[] { Later() });
        retry.Focus();
    }

    private async Task ChooseAsync(Candidate c)
    {
        _timer.Stop();
        try
        {
            await _monitor.CompleteWizardAsync(c);
        }
        catch (Exception e)
        {
            ErrorLog.Write("wizard", e);
        }

        _done = true;
        ShowDone(c);
    }

    private void ShowDone(Candidate c)
    {
        _step = "done";
        var body = new StackPanel();
        body.Children.Add(Steps(3));
        body.Children.Add(Ui.Text(S.Wiz3Head, "Heading"));
        var stage = new StackPanel { HorizontalAlignment = HorizontalAlignment.Stretch };
        stage.Children.Add(Art(PanelBuilder.Finished(c.Model), PanelBuilder.FamilyOf(c.Model) == ArtFamily.Max ? 113 : 124));
        stage.Children.Add(MiniTaskbar());
        body.Children.Add(Stage(stage, 0, new Thickness(12, 4, 12, 4)));
        body.Children.Add(Ui.Text(S.Wiz3Body));
        Body = body;
        var close = Ui.Button(S.Wiz3Done, "BtnPrimary", (_, _) => Close());
        SetFooter(Array.Empty<UIElement>(), new UIElement[] { close });
        close.Focus();
    }

    /// <summary>A small taskbar strip showing where our icon is (design 20).</summary>
    private static UIElement MiniTaskbar()
    {
        var theme = ThemeManager.Current;
        var row = new StackPanel { Orientation = Orientation.Horizontal, HorizontalAlignment = HorizontalAlignment.Center, Margin = new Thickness(0, 10, 0, 8), FlowDirection = FlowDirection.RightToLeft };
        var clock = Ui.Text(DateTime.Now.ToString("HH:mm", System.Globalization.CultureInfo.InvariantCulture), size: 12);
        clock.FlowDirection = FlowDirection.LeftToRight;
        clock.VerticalAlignment = VerticalAlignment.Center;
        clock.Margin = new Thickness(0, 0, 10, 0);
        row.Children.Add(clock);
        var ours = new Border { CornerRadius = new CornerRadius(4), Padding = new Thickness(6), Margin = new Thickness(0, 0, 6, 0) };
        ours.SetResourceReference(Border.BackgroundProperty, "Hov");
        ours.Child = new Image
        {
            Source = new DrawingImage(TrayIconRenderer.Draw(TrayLook.Ok, TrayBuilder.DefaultFill, theme.AppsDark, theme.Accent)),
            Width = 16,
            Height = 16,
        };
        row.Children.Add(ours);
        foreach (var icon in new[] { "IcoWifi", "IcoSpeaker" })
        {
            var p = Ui.Icon(icon, 16, 16, stroke: 1.4);
            p.Margin = new Thickness(6, 0, 6, 0);
            p.VerticalAlignment = VerticalAlignment.Center;
            row.Children.Add(p);
        }

        return row;
    }
}
