// Copyright (C) 2026 ChikApps
// SPDX-License-Identifier: GPL-3.0-only
//
// Base for the two panels near the clock (popup and main window): 340 wide, corners 8 (Windows 10: 0),
// shadow, opens 12 px above the taskbar next to the clock, rises 14 px while fading in (0.28 s, design §6).

using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using System.Windows.Media.Animation;
using System.Windows.Media.Effects;
using Earbuds.App.Controls;
using Earbuds.App.Platform;
using Earbuds.App.Theme;

namespace Earbuds.App.Views;

public class FlyoutWindow : Window
{
    /// <summary>Transparent margin that holds the shadow.</summary>
    protected const double ShadowMargin = 24;

    private readonly Border _card;
    private readonly Grid _clip;
    private readonly TranslateTransform _rise = new();
    private readonly DropShadowEffect _shadow = new() { BlurRadius = 40, ShadowDepth = 12, Direction = 270, Opacity = 1 };

    protected FlyoutWindow(bool activate)
    {
        WindowStyle = WindowStyle.None;
        AllowsTransparency = true;
        Background = Brushes.Transparent;
        ResizeMode = ResizeMode.NoResize;
        ShowInTaskbar = false;
        Topmost = true;
        ShowActivated = activate;
        SizeToContent = SizeToContent.WidthAndHeight;
        WindowStartupLocation = WindowStartupLocation.Manual;
        Left = -10000;
        Top = -10000;
        FlowDirection = FlowDirection.RightToLeft;
        UseLayoutRounding = true;
        SetResourceReference(FontFamilyProperty, "UiFont");
        FontSize = 14;
        TextOptions.SetTextFormattingMode(this, TextFormattingMode.Display);

        _clip = new Grid();
        _card = new Border
        {
            Width = 340,
            BorderThickness = new Thickness(1),
            Margin = new Thickness(ShadowMargin),
            Child = _clip,
            RenderTransform = _rise,
            Effect = _shadow,
        };
        _card.SetResourceReference(Border.BackgroundProperty, "Fly");
        _card.SetResourceReference(Border.BorderBrushProperty, "FlyStroke");
        _card.SetResourceReference(Border.CornerRadiusProperty, "FlyCorner");
        _clip.SizeChanged += (_, _) => UpdateClip();
        Content = _card;

        SizeChanged += (_, _) =>
        {
            if (IsVisible)
            {
                FlyoutPositioner.Place(this, ShadowMargin);
            }
        };
        ThemeManager.Current.Changed += OnTheme;
        Closed += (_, _) => ThemeManager.Current.Changed -= OnTheme;
        OnTheme(null, EventArgs.Empty);
    }

    /// <summary>What goes inside the panel.</summary>
    protected UIElement? Body
    {
        get => _clip.Children.Count > 0 ? _clip.Children[0] : null;
        set
        {
            _clip.Children.Clear();
            if (value is not null)
            {
                _clip.Children.Add(value);
            }
        }
    }

    private void OnTheme(object? sender, EventArgs e)
    {
        _shadow.Color = (Color)Application.Current.FindResource("ShadowColor");
        UpdateClip();
    }

    private void UpdateClip()
    {
        var r = ThemeManager.IsWindows11 ? 7 : 0;
        _clip.Clip = new RectangleGeometry(new Rect(0, 0, _clip.ActualWidth, _clip.ActualHeight), r, r);
    }

    /// <summary>Shows the panel above the clock with the opening animation.</summary>
    public void ShowNearClock()
    {
        FlyoutPositioner.Place(this, ShadowMargin);
        Show();
        Dispatcher.BeginInvoke(() => FlyoutPositioner.Place(this, ShadowMargin), System.Windows.Threading.DispatcherPriority.Loaded);
        if (SystemParameters.ClientAreaAnimation)
        {
            var duration = TimeSpan.FromSeconds(0.28);
            var ease = new CubicEase { EasingMode = EasingMode.EaseOut };
            _card.BeginAnimation(OpacityProperty, new DoubleAnimation(0, 1, duration) { EasingFunction = ease });
            _rise.BeginAnimation(TranslateTransform.YProperty, new DoubleAnimation(14, 0, duration) { EasingFunction = ease });
        }

        OnShown();
    }

    /// <summary>Called after the panel appeared (start ring animations here).</summary>
    protected virtual void OnShown()
    {
    }

    /// <summary>Panel header: title on the right, optional close button on the left (RTL).</summary>
    protected static DockPanel Header(TextBlock title, Button? close)
    {
        var dock = new DockPanel { MinHeight = 32, LastChildFill = true };
        if (close is not null)
        {
            close.Margin = new Thickness(-8, -4, 0, 0);
            close.VerticalAlignment = VerticalAlignment.Top;
            DockPanel.SetDock(close, Dock.Right);
            dock.Children.Add(close);
        }

        title.FontSize = 15;
        title.FontWeight = FontWeights.SemiBold;
        title.LineHeight = 20;
        title.VerticalAlignment = VerticalAlignment.Top;
        dock.Children.Add(title);
        return dock;
    }

    protected static Button CloseButton(Action click)
    {
        var b = Ui.Button(string.Empty, "IconBtn", (_, _) => click());
        b.Content = Ui.Icon("IcoX", 12, 12);
        System.Windows.Automation.AutomationProperties.SetName(b, Presentation.S.Close);
        return b;
    }
}
