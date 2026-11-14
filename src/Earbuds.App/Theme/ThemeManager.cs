// Copyright (C) 2026 ChikApps
// SPDX-License-Identifier: GPL-3.0-only
//
// Light / dark from Windows (AppsUseLightTheme for windows, SystemUsesLightTheme for the tray icon, because the
// taskbar can be dark while apps are light), and the user's accent color (design §1). Switches live, no restart.

using System.Windows;
using System.Windows.Media;
using Microsoft.Win32;
using WinUi = Windows.UI.ViewManagement;

namespace Earbuds.App.Theme;

public sealed class ThemeManager : IDisposable
{
    private const string PersonalizeKey = @"Software\Microsoft\Windows\CurrentVersion\Themes\Personalize";
    private readonly ResourceDictionary _brushes = new();
    private WinUi.UISettings? _uiSettings;

    public static ThemeManager Current { get; } = new();

    /// <summary>Windows 11 (build 22000+): rounded corners 8. Windows 10: square, opaque (design §1).</summary>
    public static bool IsWindows11 { get; } = Environment.OSVersion.Version.Build >= 22000;

    public bool AppsDark { get; private set; }

    public bool TaskbarDark { get; private set; }

    /// <summary>Accent for the current app theme (light: AccentDark1, dark: AccentLight2).</summary>
    public Color Accent { get; private set; }

    /// <summary>Accent for the tray icon (follows the taskbar theme, not the app theme).</summary>
    public Color TrayAccent { get; private set; }

    /// <summary>Raised on the UI thread after the colors changed.</summary>
    public event EventHandler? Changed;

    public void Initialize(Application app)
    {
        app.Resources.MergedDictionaries.Insert(0, _brushes);
        Apply();
        SystemEvents.UserPreferenceChanged += OnUserPreferenceChanged;
        try
        {
            _uiSettings = new WinUi.UISettings();
            _uiSettings.ColorValuesChanged += OnColorValuesChanged;
        }
        catch (Exception)
        {
            _uiSettings = null; // keep the default blue
        }
    }

    public void Dispose()
    {
        SystemEvents.UserPreferenceChanged -= OnUserPreferenceChanged;
        if (_uiSettings is not null)
        {
            _uiSettings.ColorValuesChanged -= OnColorValuesChanged;
        }
    }

    private void OnUserPreferenceChanged(object? sender, UserPreferenceChangedEventArgs e)
    {
        if (e.Category is UserPreferenceCategory.General or UserPreferenceCategory.Color or UserPreferenceCategory.VisualStyle)
        {
            Application.Current?.Dispatcher.BeginInvoke(Apply);
        }
    }

    private void OnColorValuesChanged(WinUi.UISettings sender, object args) =>
        Application.Current?.Dispatcher.BeginInvoke(Apply);

    private static bool ReadLight(string name, bool fallback)
    {
        try
        {
            using var key = Registry.CurrentUser.OpenSubKey(PersonalizeKey);
            return key?.GetValue(name) is int v ? v != 0 : fallback;
        }
        catch (Exception)
        {
            return fallback;
        }
    }

    private Color ReadAccent(bool dark)
    {
        var fallback = dark ? Hex("#60CDFF") : Hex("#005FB8");
        try
        {
            var ui = _uiSettings ?? new WinUi.UISettings();
            var c = ui.GetColorValue(dark ? WinUi.UIColorType.AccentLight2 : WinUi.UIColorType.AccentDark1);
            return c.A == 0 ? fallback : Color.FromRgb(c.R, c.G, c.B);
        }
        catch (Exception)
        {
            return fallback;
        }
    }

    public void Apply()
    {
        AppsDark = !ReadLight("AppsUseLightTheme", true);
        TaskbarDark = !IsWindows11 || !ReadLight("SystemUsesLightTheme", false); // Windows 10 taskbar: always dark icons area
        Accent = ReadAccent(AppsDark);
        TrayAccent = ReadAccent(TaskbarDark);

        var d = AppsDark;
        void Set(string key, string light, string dark) => _brushes[key] = Freeze(new SolidColorBrush(Hex(d ? dark : light)));
        _brushes["Acc"] = Freeze(new SolidColorBrush(Accent));
        _brushes["AccHov"] = Freeze(new SolidColorBrush(Mix(Accent, d ? Colors.Black : Colors.White, 0.1)));
        _brushes["AccColor"] = Accent;
        Set("OnAcc", "#FFFFFF", "#000000");
        Set("Low", "#C42B1C", "#FF6A5C");
        Set("RingStale", "#A6ABB3", "#7D828C");
        Set("NumStale", "#8A8F98", "#8C919B");
        if (IsWindows11)
        {
            Set("Fly", "#F0F9F9FA", "#F0282A30");
        }
        else
        {
            Set("Fly", "#F2F2F2", "#2B2B2B");
        }

        Set("FlyStroke", "#14000000", "#14FFFFFF");
        Set("Win", "#F3F3F3", "#202020");
        Set("Card", "#FFFFFF", "#2B2B2B");
        Set("CardStroke", "#0F000000", "#0FFFFFFF");
        Set("Text", "#1B1B1F", "#F3F3F3");
        Set("Sec", "#5D5F66", "#A9ABB2");
        Set("Ter", "#8A8C92", "#7E8087");
        Set("Bar", "#F5EEEFF2", "#F51E2025");
        Set("Hov", "#0D000000", "#12FFFFFF");
        Set("Press", "#09000000", "#0BFFFFFF");
        Set("Sep", "#14000000", "#14FFFFFF");
        Set("Trk", "#E1E3E8", "#3B3E46");
        Set("Ctl", "#FBFBFB", "#0FFFFFFF");
        Set("CtlHov", "#F6F6F6", "#15FFFFFF");
        Set("CtlStroke", "#24000000", "#17FFFFFF");
        Set("CtlBottom", "#3D000000", "#0FFFFFFF");
        _brushes["Link"] = _brushes["Acc"];
        Set("TogOff", "#99000000", "#99FFFFFF");
        Set("Info", "#E6F0FA", "#1F3447");
        _brushes["InfoIco"] = _brushes["Acc"];
        Set("Warn", "#FFF4CE", "#433519");
        Set("WarnIco", "#9D5D00", "#FCE100");
        Set("Scrim", "#4D000000", "#73000000");
        Set("Ok", "#0F7B0F", "#0F7B0F");
        Set("CloseHov", "#C42B1C", "#C42B1C");
        _brushes["FlyCorner"] = new CornerRadius(IsWindows11 ? 8 : 0);
        _brushes["CardCorner"] = new CornerRadius(6);
        _brushes["ShadowColor"] = d ? Color.FromArgb(0x80, 0, 0, 0) : Color.FromArgb(0x38, 0x14, 0x1E, 0x32);

        Changed?.Invoke(this, EventArgs.Empty);
    }

    public Brush Brush(string key) => (Brush)_brushes[key];

    public static Color Hex(string hex) => (Color)ColorConverter.ConvertFromString(hex);

    private static Color Mix(Color a, Color b, double t) => Color.FromRgb(
        (byte)(a.R + (b.R - a.R) * t), (byte)(a.G + (b.G - a.G) * t), (byte)(a.B + (b.B - a.B) * t));

    private static T Freeze<T>(T f)
        where T : Freezable
    {
        f.Freeze();
        return f;
    }
}
