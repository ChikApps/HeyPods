// Copyright (C) 2026 ChikApps
// SPDX-License-Identifier: GPL-3.0-only
//
// Small helpers to build the UI in code with the shared styles.

using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using System.Windows.Shapes;

namespace Earbuds.App.Controls;

public static class Ui
{
    public static T Res<T>(string key) where T : class => (T)Application.Current.FindResource(key);

    public static TextBlock Text(string text, string style = "Body", double? size = null, bool semibold = false, string? brush = null, TextAlignment align = TextAlignment.Left)
    {
        var t = new TextBlock { Text = text, TextAlignment = align };
        t.Style = Res<Style>(style);
        if (size is { } s)
        {
            t.FontSize = s;
            t.LineHeight = Math.Round(s * 1.43);
        }

        if (semibold)
        {
            t.FontWeight = FontWeights.SemiBold;
        }

        if (brush is not null)
        {
            t.SetResourceReference(TextBlock.ForegroundProperty, brush);
        }

        return t;
    }

    /// <summary>Latin text inside RTL (model names, versions): laid out left-to-right but aligned like the paragraph.</summary>
    public static TextBlock Latin(TextBlock t)
    {
        t.FlowDirection = FlowDirection.LeftToRight;
        t.HorizontalAlignment = HorizontalAlignment.Left; // in an RTL parent, Left = the visual right edge
        return t;
    }

    public static Path Icon(string geometryKey, double size, double viewBox, string brush = "Text", double stroke = 1.2, bool fill = false)
    {
        var p = new Path
        {
            Data = Res<Geometry>(geometryKey),
            Width = size,
            Height = size,
            Stretch = Stretch.None,
            Style = Res<Style>("IconPath"),
            StrokeThickness = stroke,
            LayoutTransform = new ScaleTransform(size / viewBox, size / viewBox),
        };
        p.Width = viewBox;
        p.Height = viewBox;
        if (fill)
        {
            p.SetResourceReference(Shape.FillProperty, brush);
            p.Stroke = null;
        }
        else
        {
            p.SetResourceReference(Shape.StrokeProperty, brush);
        }

        return p;
    }

    public static Button Button(string text, string style, RoutedEventHandler click)
    {
        var b = new Button { Content = text, Style = Res<Style>(style) };
        b.Click += click;
        return b;
    }

    public static Button Link(string text, RoutedEventHandler click)
    {
        var b = new Button { Content = new TextBlock { Text = text, TextWrapping = TextWrapping.Wrap }, Style = Res<Style>("LinkBtn") };
        b.Click += click;
        return b;
    }

    /// <summary>Round info / warning mark (16 px) for notice rows.</summary>
    public static FrameworkElement Mark(bool warning)
    {
        var g = new Grid { Width = 16, Height = 16, VerticalAlignment = VerticalAlignment.Top, Margin = new Thickness(0, 1, 0, 0), FlowDirection = FlowDirection.LeftToRight };
        var circle = new Ellipse { Width = 15, Height = 15 };
        circle.SetResourceReference(Shape.FillProperty, warning ? "WarnIco" : "InfoIco");
        g.Children.Add(circle);
        var mark = new Path { Data = Res<Geometry>(warning ? "IcoWarnMark" : "IcoInfoMark"), StrokeThickness = 1.5, StrokeStartLineCap = PenLineCap.Round, StrokeEndLineCap = PenLineCap.Round };
        mark.SetResourceReference(Shape.StrokeProperty, "Win");
        g.Children.Add(mark);
        return g;
    }

    /// <summary>Notice row (blue info / yellow warning), design .bar-info.</summary>
    public static Border Notice(bool warning, params UIElement[] content)
    {
        var dock = new DockPanel();
        var mark = Mark(warning);
        mark.Margin = new Thickness(0, 1, 10, 0);
        DockPanel.SetDock(mark, Dock.Left);
        dock.Children.Add(mark);
        var stack = new StackPanel();
        foreach (var c in content)
        {
            stack.Children.Add(c);
        }

        dock.Children.Add(stack);
        var b = new Border { CornerRadius = new CornerRadius(6), Padding = new Thickness(11, 9, 11, 9), BorderThickness = new Thickness(1), Child = dock, Margin = new Thickness(0, 0, 0, 12) };
        b.SetResourceReference(Border.BackgroundProperty, warning ? "Warn" : "Info");
        b.SetResourceReference(Border.BorderBrushProperty, "CardStroke");
        return b;
    }

    /// <summary>Spinning ring (16 px).</summary>
    public static FrameworkElement Spinner()
    {
        var g = new Grid { Width = 16, Height = 16, FlowDirection = FlowDirection.LeftToRight };
        var track = new Ellipse { StrokeThickness = 2 };
        track.SetResourceReference(Shape.StrokeProperty, "Sep");
        var arc = new Path { StrokeThickness = 2, Data = Geometry.Parse("M8,1 A7,7 0 0 1 15,8"), RenderTransformOrigin = new Point(0.5, 0.5) };
        arc.SetResourceReference(Shape.StrokeProperty, "Acc");
        var rotate = new RotateTransform();
        arc.RenderTransform = rotate;
        if (SystemParameters.ClientAreaAnimation)
        {
            rotate.BeginAnimation(RotateTransform.AngleProperty, new System.Windows.Media.Animation.DoubleAnimation(0, 360, TimeSpan.FromSeconds(1)) { RepeatBehavior = System.Windows.Media.Animation.RepeatBehavior.Forever });
        }

        g.Children.Add(track);
        g.Children.Add(arc);
        return g;
    }
}
