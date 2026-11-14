// Copyright (C) 2026 ChikApps
// SPDX-License-Identifier: GPL-3.0-only
//
// Battery ring (design v3, מפרט-לדני.md §4א). Drawn in OnRender with the math in Presentation/RingGeometry
// (unit-tested). FlowDirection is fixed LeftToRight, otherwise an RTL window mirrors it and it fills the wrong way.

using System.Globalization;
using System.Windows;
using System.Windows.Media;
using System.Windows.Media.Animation;
using Earbuds.App.Theme;
using Earbuds.Presentation;

namespace Earbuds.App.Controls;

public sealed class BatteryRing : FrameworkElement
{
    public static readonly DependencyProperty LevelProperty = DependencyProperty.Register(
        nameof(Level), typeof(int?), typeof(BatteryRing), new FrameworkPropertyMetadata(null, FrameworkPropertyMetadataOptions.AffectsRender));

    public static readonly DependencyProperty ToneProperty = DependencyProperty.Register(
        nameof(Tone), typeof(GaugeTone), typeof(BatteryRing), new FrameworkPropertyMetadata(GaugeTone.Unknown, FrameworkPropertyMetadataOptions.AffectsRender));

    public static readonly DependencyProperty ChargingProperty = DependencyProperty.Register(
        nameof(Charging), typeof(bool), typeof(BatteryRing), new FrameworkPropertyMetadata(false, FrameworkPropertyMetadataOptions.AffectsRender));

    /// <summary>0..1: part of the arc drawn (fill animation, design §6).</summary>
    public static readonly DependencyProperty ProgressProperty = DependencyProperty.Register(
        nameof(Progress), typeof(double), typeof(BatteryRing), new FrameworkPropertyMetadata(1.0, FrameworkPropertyMetadataOptions.AffectsRender));

    private static readonly Geometry Bolt = Geometry.Parse("M6,0 L0,8 H4 L2.6,14 L9.5,5.6 H5.6 L7,0 Z");
    private RingSize _size = RingSize.Popup;

    public BatteryRing()
    {
        FlowDirection = FlowDirection.LeftToRight;
        Focusable = false;
        Loaded += (_, _) => ThemeManager.Current.Changed += OnTheme;
        Unloaded += (_, _) => ThemeManager.Current.Changed -= OnTheme;
    }

    public int? Level { get => (int?)GetValue(LevelProperty); set => SetValue(LevelProperty, value); }

    public GaugeTone Tone { get => (GaugeTone)GetValue(ToneProperty); set => SetValue(ToneProperty, value); }

    public bool Charging { get => (bool)GetValue(ChargingProperty); set => SetValue(ChargingProperty, value); }

    public double Progress { get => (double)GetValue(ProgressProperty); set => SetValue(ProgressProperty, value); }

    public RingSize Size
    {
        get => _size;
        set
        {
            _size = value;
            InvalidateMeasure();
            InvalidateVisual();
        }
    }

    /// <summary>Fill from 0: 0.9 s, cubic-bezier(.2,.7,.2,1). Skipped when Windows animations are off.</summary>
    public void AnimateIn(TimeSpan delay)
    {
        if (!SystemParameters.ClientAreaAnimation)
        {
            Progress = 1;
            return;
        }

        var animation = new DoubleAnimation(0, 1, TimeSpan.FromSeconds(0.9))
        {
            BeginTime = delay,
            EasingFunction = new BezierEase(),
            FillBehavior = FillBehavior.Stop,
        };
        animation.Completed += (_, _) => Progress = 1;
        Progress = 0;
        BeginAnimation(ProgressProperty, animation);
    }

    protected override Size MeasureOverride(Size availableSize) => new(_size.Diameter, _size.Diameter);

    private void OnTheme(object? sender, EventArgs e) => InvalidateVisual();

    private Brush ToneBrush() => Tone switch
    {
        GaugeTone.Low => Res("Low"),
        GaugeTone.Stale => Res("RingStale"),
        _ => Res("Acc"),
    };

    private Brush NumberBrush() => Tone switch
    {
        GaugeTone.Low => Res("Low"),
        GaugeTone.Stale => Res("NumStale"),
        GaugeTone.Unknown => Res("Sec"),
        _ => Res("Text"),
    };

    private Brush Res(string key) => TryFindResource(key) as Brush ?? Brushes.Gray;

    protected override void OnRender(DrawingContext dc)
    {
        var d = _size.Diameter;
        var s = _size.Stroke;
        var level = Level;
        var arc = RingGeometry.Compute(level is null ? null : level.Value * Progress, d, s);

        dc.DrawEllipse(null, new Pen(Res("Trk"), s), new Point(arc.CenterX, arc.CenterY), arc.Radius, arc.Radius);
        if (!arc.Empty && Tone != GaugeTone.Unknown)
        {
            var pen = new Pen(ToneBrush(), s) { StartLineCap = PenLineCap.Round, EndLineCap = PenLineCap.Round };
            if (arc.Full)
            {
                dc.DrawEllipse(null, pen, new Point(arc.CenterX, arc.CenterY), arc.Radius, arc.Radius);
            }
            else
            {
                var g = new StreamGeometry();
                using (var ctx = g.Open())
                {
                    ctx.BeginFigure(new Point(arc.StartX, arc.StartY), false, false);
                    ctx.ArcTo(new Point(arc.EndX, arc.EndY), new Size(arc.Radius, arc.Radius), 0, arc.IsLargeArc, SweepDirection.Clockwise, true, false);
                }

                g.Freeze();
                dc.DrawGeometry(null, pen, g);
            }
        }

        // number (always "80%", left-to-right) with the bolt in front of it when charging
        var font = (FontFamily)(TryFindResource("NumFont") ?? new FontFamily("Segoe UI"));
        var text = new FormattedText(Format.Percent(level), CultureInfo.InvariantCulture, FlowDirection.LeftToRight,
            new Typeface(font, FontStyles.Normal, FontWeights.SemiBold, FontStretches.Normal),
            _size.FontSizeFor(level), NumberBrush(), VisualTreeHelper.GetDpi(this).PixelsPerDip);
        var boltW = Charging ? _size.BoltWidthFor(level) : 0;
        var gap = Charging ? 2 : 0;
        var total = boltW + gap + text.Width;
        var x = (d - total) / 2;
        if (Charging)
        {
            var scale = boltW / 10; // the bolt viewBox is 10 × 14 (ui.js)
            dc.PushTransform(new TranslateTransform(x, (d - 14 * scale) / 2));
            dc.PushTransform(new ScaleTransform(scale, scale));
            dc.DrawGeometry(ToneBrush(), null, Bolt);
            dc.Pop();
            dc.Pop();
        }

        dc.DrawText(text, new Point(x + boltW + gap, (d - text.Height) / 2));
    }

    /// <summary>cubic-bezier(.2,.7,.2,1) (design §6).</summary>
    private sealed class BezierEase : IEasingFunction
    {
        public double Ease(double normalizedTime) => RingGeometry.Ease(normalizedTime);
    }
}
