// Copyright (C) 2026 ChikApps
// SPDX-License-Identifier: GPL-3.0-only
//
// Base for the regular windows (wizard, settings, about, licenses): Windows 11 style, 480 wide,
// our own 32 px title bar with the window buttons on the LEFT (RTL), content padding 24,
// optional button bar at the bottom on the card color (design §3), optional dimmed dialog on top.

using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using System.Windows.Shell;
using Earbuds.App.Art;
using Earbuds.App.Controls;
using Earbuds.App.Theme;

namespace Earbuds.App.Views;

public class AppWindow : Window
{
    private readonly DockPanel _dock = new();
    private readonly ScrollViewer _scroll;
    private readonly Border _footer;
    private readonly Grid _overlay = new() { Visibility = Visibility.Collapsed };

    /// <summary>Smallest height of a regular window: title bar + some content + the button bar.</summary>
    public const double DefaultMinHeight = 260;

    /// <param name="height">A fixed height (the wizard: its steps differ in height, the window must not jump or
    /// shrink). Null = grow with the content, never below <see cref="DefaultMinHeight"/>.</param>
    protected AppWindow(string title, bool minimize = true, double width = 480, double? height = null)
    {
        Title = title;

        // The size never depends on the content alone: in 0.9.1 the content failed while the window was being
        // shown, and with SizeToContent and no minimum the window stayed a 160 px strip with only its title.
        Width = width;
        MinWidth = width;
        MaxHeight = Math.Max(400, SystemParameters.WorkArea.Height * 0.94);
        if (height is { } h)
        {
            SizeToContent = SizeToContent.Manual;
            Height = Math.Min(h, MaxHeight);
            MinHeight = Height;
        }
        else
        {
            SizeToContent = SizeToContent.Height;
            MinHeight = DefaultMinHeight;
        }

        ResizeMode = minimize ? ResizeMode.CanMinimize : ResizeMode.NoResize;
        WindowStartupLocation = WindowStartupLocation.CenterScreen;
        FlowDirection = FlowDirection.RightToLeft;
        UseLayoutRounding = true;
        SetResourceReference(BackgroundProperty, "Win");
        SetResourceReference(FontFamilyProperty, "UiFont");
        SetResourceReference(ForegroundProperty, "Text");
        FontSize = 14;
        TextOptions.SetTextFormattingMode(this, TextFormattingMode.Display);
        try
        {
            Icon = BitmapFrame.Create(new Uri("pack://application:,,,/Assets/app.ico"));
        }
        catch (Exception)
        {
            // the window still works without an icon
        }

        WindowChrome.SetWindowChrome(this, new WindowChrome
        {
            CaptionHeight = 32,
            ResizeBorderThickness = new Thickness(0),
            GlassFrameThickness = new Thickness(0, 0, 0, 1),
            CornerRadius = new CornerRadius(0),
            UseAeroCaptionButtons = false,
        });

        // title bar
        var bar = new DockPanel { Height = 32 };
        var close = CaptionButton("IcoX", 12, "CloseCaptionBtn", Close);
        DockPanel.SetDock(close, Dock.Right);
        bar.Children.Add(close);
        if (minimize)
        {
            var min = CaptionButton("IcoMin", 10, "CaptionBtn", () => WindowState = WindowState.Minimized);
            DockPanel.SetDock(min, Dock.Right);
            bar.Children.Add(min);
        }

        var icon = new Image { Source = ArtLibrary.AppIcon, Width = 16, Height = 16, Margin = new Thickness(12, 0, 8, 0), FlowDirection = FlowDirection.LeftToRight };
        DockPanel.SetDock(icon, Dock.Left);
        bar.Children.Add(icon);
        bar.Children.Add(new TextBlock { Text = title, FontSize = 12, VerticalAlignment = VerticalAlignment.Center, TextTrimming = TextTrimming.CharacterEllipsis });
        DockPanel.SetDock(bar, Dock.Top);
        _dock.Children.Add(bar);

        _footer = new Border { Padding = new Thickness(24, 20, 24, 20), BorderThickness = new Thickness(0, 1, 0, 0), Visibility = Visibility.Collapsed };
        _footer.SetResourceReference(Border.BackgroundProperty, "Card");
        _footer.SetResourceReference(Border.BorderBrushProperty, "Sep");
        DockPanel.SetDock(_footer, Dock.Bottom);
        _dock.Children.Add(_footer);

        _scroll = new ScrollViewer { VerticalScrollBarVisibility = ScrollBarVisibility.Auto, Padding = new Thickness(24, 8, 24, 20), Focusable = false };
        _dock.Children.Add(_scroll);

        var root = new Grid();
        root.Children.Add(_dock);
        _overlay.SetResourceReference(Panel.BackgroundProperty, "Scrim");
        root.Children.Add(_overlay);
        Content = root;
        PreviewKeyDown += (_, e) =>
        {
            if (e.Key == System.Windows.Input.Key.Escape)
            {
                if (_overlay.Visibility == Visibility.Visible)
                {
                    HideDialog();
                }
                else
                {
                    Close();
                }

                e.Handled = true;
            }
        };
    }

    protected UIElement? Body
    {
        get => _scroll.Content as UIElement;
        set => _scroll.Content = value;
    }

    /// <summary>Bottom bar: <paramref name="start"/> buttons on the right (RTL start), <paramref name="end"/> on the far left.</summary>
    protected void SetFooter(UIElement[] start, UIElement[]? end = null)
    {
        if (start.Length == 0 && (end is null || end.Length == 0))
        {
            _footer.Visibility = Visibility.Collapsed;
            return;
        }

        var grid = new Grid();
        grid.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });
        grid.ColumnDefinitions.Add(new ColumnDefinition());
        grid.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });
        var s = new StackPanel { Orientation = Orientation.Horizontal };
        foreach (var b in start)
        {
            if (b is FrameworkElement fe)
            {
                fe.Margin = new Thickness(0, 0, 8, 0);
            }

            s.Children.Add(b);
        }

        grid.Children.Add(s);
        if (end is not null)
        {
            var e = new StackPanel { Orientation = Orientation.Horizontal };
            foreach (var b in end)
            {
                e.Children.Add(b);
            }

            Grid.SetColumn(e, 2);
            grid.Children.Add(e);
        }

        _footer.Child = grid;
        _footer.Visibility = Visibility.Visible;
    }

    /// <summary>Dialog (400 wide) over a dimmed window, two equal buttons (design: "forget" confirmation).</summary>
    protected void ShowDialog(string head, string body, string yes, string no, Action onYes)
    {
        var card = new Border { Width = 400, BorderThickness = new Thickness(1), VerticalAlignment = VerticalAlignment.Center, HorizontalAlignment = HorizontalAlignment.Center, Margin = new Thickness(16) };
        card.SetResourceReference(Border.BackgroundProperty, "Win");
        card.SetResourceReference(Border.BorderBrushProperty, "FlyStroke");
        card.SetResourceReference(Border.CornerRadiusProperty, "FlyCorner");
        var stack = new StackPanel();
        var text = new StackPanel { Margin = new Thickness(24, 22, 24, 18) };
        var h = Ui.Text(head, "Heading", size: 18, semibold: true);
        text.Children.Add(h);
        text.Children.Add(Ui.Text(body));
        stack.Children.Add(text);
        var foot = new Border { Padding = new Thickness(24, 20, 24, 20), BorderThickness = new Thickness(0, 1, 0, 0) };
        foot.SetResourceReference(Border.BackgroundProperty, "Card");
        foot.SetResourceReference(Border.BorderBrushProperty, "Sep");
        var buttons = new System.Windows.Controls.Primitives.UniformGrid { Rows = 1, Columns = 2 };
        var y = Ui.Button(yes, "BtnPrimary", (_, _) => { HideDialog(); onYes(); });
        y.Margin = new Thickness(0, 0, 0, 0);
        var n = Ui.Button(no, "Btn", (_, _) => HideDialog());
        n.Margin = new Thickness(8, 0, 0, 0);
        y.Margin = new Thickness(0, 0, 8, 0);
        buttons.Children.Add(y);
        buttons.Children.Add(n);
        foot.Child = buttons;
        stack.Children.Add(foot);
        card.Child = stack;
        _overlay.Children.Clear();
        _overlay.Children.Add(card);
        _overlay.Visibility = Visibility.Visible;
        n.Focus();
    }

    protected void HideDialog()
    {
        _overlay.Visibility = Visibility.Collapsed;
        _overlay.Children.Clear();
    }

    /// <summary>Brings an existing window to the front.</summary>
    public void Bring()
    {
        if (WindowState == WindowState.Minimized)
        {
            WindowState = WindowState.Normal;
        }

        Show();
        Activate();
    }

    private Button CaptionButton(string icon, double size, string style, Action click)
    {
        var glyph = Ui.Icon(icon, size, size, stroke: 1);
        glyph.SetBinding(System.Windows.Shapes.Shape.StrokeProperty, new System.Windows.Data.Binding("Foreground")
        {
            RelativeSource = new System.Windows.Data.RelativeSource(System.Windows.Data.RelativeSourceMode.FindAncestor, typeof(Button), 1),
        });
        var b = new Button { Style = Ui.Res<Style>(style), Content = glyph };
        WindowChrome.SetIsHitTestVisibleInChrome(b, true);
        b.Click += (_, _) => click();
        System.Windows.Automation.AutomationProperties.SetName(b, icon == "IcoX" ? Presentation.S.Close : Presentation.S.Minimize);
        return b;
    }
}
