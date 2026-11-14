// Copyright (C) 2026 ChikApps
// SPDX-License-Identifier: GPL-3.0-only
//
// Icon near the clock (H.NotifyIcon, MIT): 4 looks, tooltip, click = open/close the main window,
// right click = menu (open · settings · about · exit).

using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using Earbuds.App.Controls;
using Earbuds.App.Theme;
using Earbuds.Presentation;
using H.NotifyIcon;

namespace Earbuds.App.Platform;

internal sealed class TrayIcon : IDisposable
{
    private readonly TaskbarIcon _icon;
    private TrayModel? _model;
    private string? _iconKey;
    private System.Drawing.Icon? _current;

    public TrayIcon(Action open, Action settings, Action about, Action exit)
    {
        var menu = new ContextMenu { Style = Ui.Res<Style>("TrayMenu") };
        menu.Items.Add(Item(S.MenuOpen, "IcoOpen", open, bold: true));
        menu.Items.Add(Item(S.MenuSettings, "IcoGear", settings));
        menu.Items.Add(Item(S.MenuAbout, "IcoInfo2", about));
        var sep = new Separator { Margin = new Thickness(4, 4, 4, 4), Height = 1 };
        sep.Template = new ControlTemplate(typeof(Separator)) { VisualTree = SeparatorLine() };
        menu.Items.Add(sep);
        menu.Items.Add(Item(S.MenuExit, "IcoExit", exit));

        _icon = new TaskbarIcon
        {
            ToolTipText = AppInfo.Name,
            ContextMenu = menu,
            NoLeftClickDelay = true,
            MenuActivation = H.NotifyIcon.Core.PopupActivationMode.RightClick,
        };
        _icon.TrayLeftMouseUp += (_, _) => LeftClick?.Invoke(this, EventArgs.Empty);
        _icon.ForceCreate(false);
        ThemeManager.Current.Changed += OnTheme;
    }

    public event EventHandler? LeftClick;

    public void Update(TrayModel model)
    {
        if (model == _model)
        {
            return;
        }

        _model = model;
        if (_icon.ToolTipText != model.Tooltip)
        {
            _icon.ToolTipText = model.Tooltip;
        }

        Redraw(force: false);
    }

    private void OnTheme(object? sender, EventArgs e) => Redraw(force: true);

    private void Redraw(bool force)
    {
        if (_model is null)
        {
            return;
        }

        var theme = ThemeManager.Current;
        var size = (int)Math.Round(16 * Math.Max(96u, SafeDpi()) / 96.0);
        var key = $"{_model.Look}.{_model.FillPercent}.{theme.TaskbarDark}.{theme.TrayAccent}.{size}";
        if (!force && key == _iconKey)
        {
            return;
        }

        try
        {
            var icon = TrayIconRenderer.ToIcon(TrayIconRenderer.Draw(_model.Look, _model.FillPercent, theme.TaskbarDark, theme.TrayAccent), size);
            _icon.UpdateIcon(icon);
            _current?.Dispose();
            _current = icon;
            _iconKey = key;
        }
        catch (Exception e)
        {
            ErrorLog.Write("tray", e);
        }
    }

    private static uint SafeDpi()
    {
        try
        {
            return Native.GetDpiForSystem();
        }
        catch (Exception)
        {
            return 96;
        }
    }

    private static MenuItem Item(string text, string icon, Action click, bool bold = false)
    {
        var item = new MenuItem
        {
            Header = text,
            Style = Ui.Res<Style>("TrayMenuItem"),
            Icon = Ui.Icon(icon, 16, 16),
            FontWeight = bold ? FontWeights.SemiBold : FontWeights.Normal,
        };
        item.Click += (_, _) => click();
        return item;
    }

    private static FrameworkElementFactory SeparatorLine()
    {
        var f = new FrameworkElementFactory(typeof(Border));
        f.SetValue(FrameworkElement.HeightProperty, 1.0);
        f.SetResourceReference(Border.BackgroundProperty, "Sep");
        return f;
    }

    public void Dispose()
    {
        ThemeManager.Current.Changed -= OnTheme;
        _icon.Dispose();
        _current?.Dispose();
    }
}
