// Copyright (C) 2026 ChikApps
// SPDX-License-Identifier: GPL-3.0-only
//
// "Full licenses": THIRD_PARTY_NOTICES.md and the GPL-3.0 text, shipped inside the exe (English, left-to-right).

using System.IO;
using System.Reflection;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using Earbuds.Presentation;

namespace Earbuds.App.Views;

public sealed class LicensesWindow : AppWindow
{
    public LicensesWindow()
        : base(S.LicensesTitle, width: 640)
    {
        Height = Math.Min(720, SystemParameters.WorkArea.Height * 0.9);
        SizeToContent = SizeToContent.Manual;
        var text = Read("THIRD_PARTY_NOTICES.md") + Environment.NewLine + Environment.NewLine + new string('=', 72) + Environment.NewLine + Environment.NewLine + Read("LICENSE.txt");
        var box = new TextBox
        {
            Text = text,
            IsReadOnly = true,
            TextWrapping = TextWrapping.Wrap,
            FlowDirection = FlowDirection.LeftToRight,
            FontFamily = new FontFamily("Consolas, Cascadia Mono, Courier New"),
            FontSize = 12,
            BorderThickness = new Thickness(0),
            Background = Brushes.Transparent,
            VerticalScrollBarVisibility = ScrollBarVisibility.Disabled,
        };
        box.SetResourceReference(ForegroundProperty, "Text");
        Body = box;
    }

    private static string Read(string name)
    {
        using var stream = Assembly.GetExecutingAssembly().GetManifestResourceStream(name);
        if (stream is null)
        {
            return string.Empty;
        }

        using var reader = new StreamReader(stream);
        return reader.ReadToEnd();
    }
}
