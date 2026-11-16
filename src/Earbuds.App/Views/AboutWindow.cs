// Copyright (C) 2026 ChikApps
// SPDX-License-Identifier: GPL-3.0-only
//
// About (spec §2, the texts file §7, design 23): name, version, free/GPL, "check for update now" (only on click,
// the app's only network access), source code (hidden until the address exists), privacy, honest note,
// credits, full licenses, contact, trademarks, no warranty.

using System.Windows;
using System.Windows.Controls;
using Earbuds.App.Art;
using Earbuds.App.Controls;
using Earbuds.App.Platform;
using Earbuds.Core.Updates;
using Earbuds.Presentation;

namespace Earbuds.App.Views;

public sealed class AboutWindow : AppWindow
{
    private readonly Func<CancellationToken, Task<UpdateResult>> _check;
    private readonly Action _licenses;
    private readonly ContentControl _update = new() { Focusable = false };
    private CancellationTokenSource? _cts;

    public AboutWindow(Func<CancellationToken, Task<UpdateResult>> checkUpdate, Action openLicenses)
        : base(S.AboutTitle)
    {
        _check = checkUpdate;
        _licenses = openLicenses;
        Closed += (_, _) => _cts?.Cancel();
        Build();
        ShowUpdate(UpdateCard.Idle);
    }

    private void Build()
    {
        var body = new StackPanel();

        var top = new DockPanel { Margin = new Thickness(0, 6, 0, 14) };
        var icon = new Image { Source = ArtLibrary.AppIcon, Width = 64, Height = 64, Margin = new Thickness(0, 0, 16, 0), FlowDirection = FlowDirection.LeftToRight };
        DockPanel.SetDock(icon, Dock.Left);
        top.Children.Add(icon);
        var names = new StackPanel { VerticalAlignment = VerticalAlignment.Center };
        var nameRow = new WrapPanel();
        var name = Ui.Text(AppInfo.Name, size: 24, semibold: true);
        name.LineHeight = 30;
        nameRow.Children.Add(Ui.Latin(name));
        var sub = Ui.Text(AppInfo.Subtitle, size: 14, brush: "Sec");
        sub.FlowDirection = FlowDirection.LeftToRight;
        sub.VerticalAlignment = VerticalAlignment.Bottom;
        sub.Margin = new Thickness(8, 0, 0, 4);
        nameRow.Children.Add(sub);
        names.Children.Add(nameRow);
        names.Children.Add(Ui.Text(S.AboutTagline, brush: "Sec"));
        names.Children.Add(Ui.Text(string.Format(System.Globalization.CultureInfo.InvariantCulture, S.AboutVersion, Format.Lrm + AppInfo.Version + Format.Lrm) + " · " + S.AboutBy, "Hint"));
        top.Children.Add(names);
        body.Children.Add(top);

        body.Children.Add(Para(Ui.Text(S.AboutFree)));
        _update.Margin = new Thickness(0, 6, 0, 10);
        body.Children.Add(_update);

        if (!string.IsNullOrEmpty(AppInfo.SourceCodeUrl))
        {
            var row = new DockPanel();
            var ext = Ui.Icon("IcoExt", 12, 12, "Sec", 1.1);
            DockPanel.SetDock(ext, Dock.Right);
            row.Children.Add(ext);
            row.Children.Add(Ui.Text(S.AboutCode));
            var code = new Button { Style = Ui.Res<Style>("CardBtn"), Content = row, Margin = new Thickness(0, 0, 0, 14) };
            code.Click += (_, _) => Shell.Open(AppInfo.SourceCodeUrl);
            body.Children.Add(code);
        }

        body.Children.Add(Para(Ui.Text(S.AboutPrivacy)));

        var honest = new TextBlock { TextWrapping = TextWrapping.Wrap, FontSize = 13, LineHeight = 19 };
        honest.Inlines.Add(new System.Windows.Documents.Bold(new System.Windows.Documents.Run(S.AboutHonestLead + " ")));
        honest.Inlines.Add(new System.Windows.Documents.Run(S.AboutHonest));
        var callout = new Border { Style = Ui.Res<Style>("CardBox"), MinHeight = 0, Margin = new Thickness(0, 10, 0, 10), Child = honest };
        body.Children.Add(callout);

        body.Children.Add(Ui.Text(S.AboutCreditsHead, "SubHeading"));
        body.Children.Add(Ui.Text(S.AboutCreditsBody, size: 13));
        var chips = new WrapPanel { Margin = new Thickness(0, 8, 0, 0) };
        foreach (var credit in S.AboutCreditsList)
        {
            var chip = new Border { CornerRadius = new CornerRadius(999), Padding = new Thickness(10, 2, 10, 2), Margin = new Thickness(0, 0, 6, 6), Child = new TextBlock { Text = credit, FontSize = 12, FlowDirection = FlowDirection.LeftToRight } };
            chip.SetResourceReference(Border.BackgroundProperty, "Hov");
            chips.Children.Add(chip);
        }

        body.Children.Add(chips);
        var licenses = Ui.Button(S.AboutLicenses, "Btn", (_, _) => _licenses());
        licenses.HorizontalAlignment = HorizontalAlignment.Left; // start edge (right) in RTL
        licenses.Margin = new Thickness(0, 12, 0, 4);
        body.Children.Add(licenses);

        var contact = new WrapPanel { Margin = new Thickness(0, 8, 0, 0) };
        contact.Children.Add(Ui.Text(S.AboutContact, "Hint"));
        var mail = Ui.Link(AppInfo.ContactEmail, (_, _) => Shell.Open("mailto:" + AppInfo.ContactEmail));
        mail.FontSize = 12;
        mail.FlowDirection = FlowDirection.LeftToRight;
        contact.Children.Add(mail);
        body.Children.Add(contact);
        body.Children.Add(Fine(S.AboutTm));
        body.Children.Add(Fine(S.AboutWarranty));
        Body = body;
    }

    private static UIElement Para(TextBlock t)
    {
        t.Margin = new Thickness(0, 0, 0, 10);
        return t;
    }

    private static UIElement Fine(string text)
    {
        var t = Ui.Text(text, "Hint");
        t.LineHeight = 17;
        t.Margin = new Thickness(0, 8, 0, 0);
        return t;
    }

    private void ShowUpdate(UpdateCard card)
    {
        var dock = new DockPanel();
        var button = Ui.Button(card.ButtonText, card.ButtonIsDownload ? "BtnPrimary" : "Btn", async (_, _) => await OnUpdateButton(card));
        button.IsEnabled = card.ButtonEnabled;
        button.VerticalAlignment = VerticalAlignment.Center;
        button.Margin = new Thickness(14, 0, 0, 0);
        DockPanel.SetDock(button, Dock.Right);
        dock.Children.Add(button);

        UIElement? mark = card.State switch
        {
            UpdateCardState.Available => Circle("Acc", "IcoDown", "OnAcc"),
            UpdateCardState.UpToDate => Circle("Ok", "IcoCheckSmall", null),
            UpdateCardState.Failed => Ui.Mark(true),
            _ => null,
        };
        if (mark is FrameworkElement fe)
        {
            fe.Margin = new Thickness(0, 0, 14, 0);
            fe.VerticalAlignment = VerticalAlignment.Center;
            DockPanel.SetDock(fe, Dock.Left);
            dock.Children.Add(fe);
        }

        var info = new StackPanel { VerticalAlignment = VerticalAlignment.Center };
        info.Children.Add(Ui.Text(card.Title));
        if (card.State == UpdateCardState.Checking)
        {
            var row = new StackPanel { Orientation = Orientation.Horizontal, Margin = new Thickness(0, 2, 0, 0) };
            var spin = Ui.Spinner();
            spin.Margin = new Thickness(0, 0, 8, 0);
            row.Children.Add(spin);
            row.Children.Add(Ui.Text(card.Hint, "Hint"));
            info.Children.Add(row);
        }
        else
        {
            var hint = Ui.Text(card.Hint, "Hint");
            hint.Margin = new Thickness(0, 2, 0, 0);
            info.Children.Add(hint);
        }

        dock.Children.Add(info);
        _update.Content = new Border { Style = Ui.Res<Style>("CardBox"), Child = dock };
    }

    private static FrameworkElement Circle(string fill, string icon, string? stroke)
    {
        var g = new Grid { Width = 20, Height = 20, FlowDirection = FlowDirection.LeftToRight };
        var c = new System.Windows.Shapes.Ellipse();
        c.SetResourceReference(System.Windows.Shapes.Shape.FillProperty, fill);
        g.Children.Add(c);
        var p = new System.Windows.Shapes.Path { Data = Ui.Res<System.Windows.Media.Geometry>(icon), StrokeThickness = 1.6, StrokeStartLineCap = System.Windows.Media.PenLineCap.Round, StrokeEndLineCap = System.Windows.Media.PenLineCap.Round, StrokeLineJoin = System.Windows.Media.PenLineJoin.Round };
        if (stroke is null)
        {
            p.Stroke = System.Windows.Media.Brushes.White;
        }
        else
        {
            p.SetResourceReference(System.Windows.Shapes.Shape.StrokeProperty, stroke);
        }

        g.Children.Add(p);
        return g;
    }

    private async Task OnUpdateButton(UpdateCard card)
    {
        if (card.State == UpdateCardState.Available && card.DownloadPage is { } page)
        {
            Shell.Open(page.AbsoluteUri);
            return;
        }

        ShowUpdate(UpdateCard.Checking);
        _cts?.Cancel();
        _cts = new CancellationTokenSource();
        try
        {
            var result = await _check(_cts.Token);
            ShowUpdate(UpdateCard.From(result, AppInfo.Version));
        }
        catch (OperationCanceledException)
        {
            // window closed
        }
        catch (Exception e)
        {
            ErrorLog.Write("update", e);
            ShowUpdate(UpdateCard.From(new UpdateResult(UpdateStatus.Failed), AppInfo.Version));
        }
    }
}
