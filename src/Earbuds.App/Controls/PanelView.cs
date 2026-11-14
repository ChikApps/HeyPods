// Copyright (C) 2026 ChikApps
// SPDX-License-Identifier: GPL-3.0-only
//
// Body shared by the popup, the main window and the wizard: the drawing + one ring per part, or a state message
// (not found, Bluetooth off, no Bluetooth LE, not chosen). Design: ui.js row() / gauge() / mainWin().

using System.Windows;
using System.Windows.Controls;
using System.Windows.Controls.Primitives;
using System.Windows.Media;
using Earbuds.App.Art;
using Earbuds.App.Theme;
using Earbuds.Presentation;

namespace Earbuds.App.Controls;

public sealed class PanelView : ContentControl
{
    private readonly List<BatteryRing> _rings = new();
    private PanelModel? _shown;

    public PanelView()
    {
        Focusable = false;
        Loaded += (_, _) => ThemeManager.Current.Changed += OnTheme;
        Unloaded += (_, _) => ThemeManager.Current.Changed -= OnTheme;
    }

    public RingSize RingSize { get; init; } = RingSize.Popup;

    /// <summary>Drawing height for the case drawings (k × 73) and for AirPods Max (k × 92).</summary>
    public double ArtHeight { get; init; } = 150;

    public double MaxArtHeight { get; init; } = 133;

    /// <summary>Wizard step 3: drawing only, no rings.</summary>
    public bool ArtOnly { get; init; }

    public event EventHandler? ChooseRequested;

    public event EventHandler? BluetoothSettingsRequested;

    public void Show(PanelModel model)
    {
        // The main window refreshes every second ("updated 5 seconds ago"); rebuild only when the body changed,
        // so the ring animation is not cut and nothing flickers.
        if (model.SameBodyAs(_shown))
        {
            return;
        }

        _shown = model;
        Rebuild(model);
    }

    /// <summary>Rings fill from 0 when a window opens (design §6): parts left to right, the case 0.12 s later.</summary>
    public void AnimateIn()
    {
        for (var i = 0; i < _rings.Count; i++)
        {
            var isCase = _rings.Count == 3 && i == 1;
            _rings[i].AnimateIn(isCase ? TimeSpan.FromSeconds(0.12) : TimeSpan.Zero);
        }
    }

    private void OnTheme(object? sender, EventArgs e)
    {
        if (_shown is not null)
        {
            Rebuild(_shown);
        }
    }

    private static bool Dark => ThemeManager.Current.AppsDark;

    private void Rebuild(PanelModel m)
    {
        _rings.Clear();
        Content = m.Body switch
        {
            PanelBody.Gauges => Gauges(m),
            PanelBody.NotFound => Message(Drawing(m.Art), S.StNotFound, null, null),
            PanelBody.BluetoothOff => Message(BtOffIcon(), S.StBtOff, S.StBtOffBtn, () => BluetoothSettingsRequested?.Invoke(this, EventArgs.Empty), primary: false),
            PanelBody.NotSupported => Message(BtOffIcon(), S.StNoBle, null, null),
            _ => Message(Drawing(ArtModel.ForCase(ArtFamily.Generic)), S.StNoChoice, S.StNoChoiceBtn, () => ChooseRequested?.Invoke(this, EventArgs.Empty), primary: true),
        };
    }

    private Image Drawing(ArtModel art, double? height = null)
    {
        var h = height ?? (art.Family == ArtFamily.Max ? MaxArtHeight : ArtHeight);
        var image = new Image
        {
            Source = ArtLibrary.Scene(art, Dark),
            Height = h,
            Stretch = Stretch.Uniform,
            HorizontalAlignment = HorizontalAlignment.Center,
            FlowDirection = FlowDirection.LeftToRight, // left pod on the left, always
            SnapsToDevicePixels = false,
        };
        RenderOptions.SetBitmapScalingMode(image, BitmapScalingMode.HighQuality);
        System.Windows.Automation.AutomationProperties.SetName(image, string.Empty);
        return image;
    }

    private static FrameworkElement BtOffIcon()
    {
        var icon = Ui.Icon("IcoBtOff", 40, 40, "Sec", 2.2);
        icon.HorizontalAlignment = HorizontalAlignment.Center;
        return icon;
    }

    private FrameworkElement Gauges(PanelModel m)
    {
        var root = new StackPanel { Margin = new Thickness(0, 10, 0, 12) };
        root.Children.Add(Drawing(m.Art));
        if (ArtOnly)
        {
            return root;
        }

        // physical order (left on the left), so the row is left-to-right; text inside each part stays Hebrew
        var row = new UniformGrid { Rows = 1, Columns = m.Gauges.Count, FlowDirection = FlowDirection.LeftToRight, Margin = new Thickness(0, 8, 0, 0) };
        foreach (var g in m.Gauges)
        {
            row.Children.Add(Gauge(g));
        }

        root.Children.Add(row);
        return root;
    }

    private FrameworkElement Gauge(GaugeModel g)
    {
        var stack = new StackPanel { HorizontalAlignment = HorizontalAlignment.Center, FlowDirection = FlowDirection.RightToLeft, Margin = new Thickness(3, 0, 3, 0) };
        System.Windows.Automation.AutomationProperties.SetName(stack, g.Accessible);
        var ring = new BatteryRing { Size = RingSize, Level = g.Level, Tone = g.Tone, Charging = g.Charging, Margin = new Thickness(0, 2, 0, 6), HorizontalAlignment = HorizontalAlignment.Center };
        _rings.Add(ring);
        stack.Children.Add(ring);
        stack.Children.Add(Ui.Text(g.Label, "Hint", align: TextAlignment.Center));
        if (g.Spot != PodSpot.None)
        {
            var pill = new Border
            {
                CornerRadius = new CornerRadius(999),
                Padding = new Thickness(9, 1, 9, 1),
                Margin = new Thickness(0, 6, 0, 0),
                HorizontalAlignment = HorizontalAlignment.Center,
                Child = Ui.Text(g.SpotText, "Hint", brush: g.Spot == PodSpot.Out ? "Sec" : "Text"),
            };
            if (g.Spot == PodSpot.Out)
            {
                pill.BorderThickness = new Thickness(1);
                pill.SetResourceReference(Border.BorderBrushProperty, "Sep");
            }
            else
            {
                pill.SetResourceReference(Border.BackgroundProperty, "Hov");
            }

            stack.Children.Add(pill);
        }

        if (g.Note is { } note)
        {
            var t = Ui.Text(note, "Hint", size: 11, align: TextAlignment.Center);
            t.MaxWidth = 110;
            t.Margin = new Thickness(0, 4, 0, 0);
            t.LineHeight = 14;
            stack.Children.Add(t);
        }

        return stack;
    }

    private FrameworkElement Message(FrameworkElement art, string text, string? button, Action? click, bool primary = false)
    {
        var stack = new StackPanel { Margin = new Thickness(8, 18, 8, 16), HorizontalAlignment = HorizontalAlignment.Center };
        if (art is Image img)
        {
            img.Height = 110;
            img.Opacity = 0.9;
        }

        stack.Children.Add(art);
        var t = Ui.Text(text, size: 13, align: TextAlignment.Center);
        t.MaxWidth = 270;
        t.Margin = new Thickness(0, 10, 0, 0);
        stack.Children.Add(t);
        if (button is not null && click is not null)
        {
            var b = Ui.Button(button, primary ? "BtnPrimary" : "Btn", (_, _) => click());
            b.HorizontalAlignment = HorizontalAlignment.Center;
            b.Margin = new Thickness(0, 10, 0, 0);
            stack.Children.Add(b);
        }

        return stack;
    }
}
