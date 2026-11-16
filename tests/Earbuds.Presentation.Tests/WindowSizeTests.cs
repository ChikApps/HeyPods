// Copyright (C) 2026 ChikApps
// SPDX-License-Identifier: GPL-3.0-only
//
// Every window has a real size even when its content fails (0.9.1: the wizard stayed a 160 px strip with only its
// title, because the size came from the content alone), and every wizard step really has content.
// Read from the source: WPF does not run on Linux.

using System.Globalization;
using System.Text.RegularExpressions;
using Xunit;

namespace Earbuds.Presentation.Tests;

public sealed class WindowSizeTests
{
    private static string Views => Path.Combine(Src.App, "Views");

    private static double Const(string code, string name)
    {
        var m = Regex.Match(code, "const double " + name + "\\s*=\\s*(?<v>[\\d.]+)\\s*;");
        Assert.True(m.Success, "constant " + name + " not found");
        return double.Parse(m.Groups["v"].Value, CultureInfo.InvariantCulture);
    }

    /// <summary>Every class that derives from Window (directly or through our bases).</summary>
    private static List<(string File, string Name, string Base, string Code)> Windows()
    {
        var all = Src.CodeFiles().Select(f => (File: f, Code: Src.Code(f))).ToList();
        var bases = new HashSet<string> { "Window" };
        var found = new List<(string, string, string, string)>();
        for (var changed = true; changed;)
        {
            changed = false;
            foreach (var (file, code) in all)
            {
                foreach (Match m in Regex.Matches(code, "class (?<n>\\w+)\\s*:\\s*(?<b>[\\w.]+)"))
                {
                    var b = m.Groups["b"].Value;
                    b = b[(b.LastIndexOf('.') + 1)..];
                    if (bases.Contains(b) && bases.Add(m.Groups["n"].Value))
                    {
                        found.Add((file, m.Groups["n"].Value, b, code));
                        changed = true;
                    }
                }
            }
        }

        return found;
    }

    [Fact]
    public void AllWindowsFound()
    {
        var names = Windows().Select(w => w.Name).ToHashSet();
        foreach (var n in new[] { "AppWindow", "FlyoutWindow", "WizardWindow", "SettingsWindow", "AboutWindow", "LicensesWindow", "MainFlyout", "PopupWindow" })
        {
            Assert.Contains(n, names);
        }
    }

    [Fact] // a window that sizes itself to its content must also have a minimum, or failed content = a strip
    public void EveryWindow_HasWidthAndHeightThatDoNotDependOnContentAlone()
    {
        var problems = new List<string>();
        foreach (var (file, name, b, code) in Windows())
        {
            if (b != "Window")
            {
                continue; // checked through its base below
            }

            var width = Regex.IsMatch(code, "(?<![\\w.])MinWidth\\s*=") || (Regex.IsMatch(code, "(?<![\\w.])Width\\s*=") && !code.Contains("SizeToContent.Width", StringComparison.Ordinal));
            var height = Regex.IsMatch(code, "(?<![\\w.])MinHeight\\s*=");
            if (!width)
            {
                problems.Add($"{name}: no MinWidth (or fixed Width without SizeToContent.Width)");
            }

            if (!height)
            {
                problems.Add($"{name}: no MinHeight");
            }
        }

        Assert.True(problems.Count == 0, string.Join("\n", problems));
    }

    [Fact]
    public void AppWindow_MinimumIsTheWidth_AndAReasonableHeight()
    {
        var code = Src.Code(Path.Combine(Views, "AppWindow.cs"));
        Assert.Matches("MinWidth\\s*=\\s*width;", code);
        Assert.Matches("Width\\s*=\\s*width;", code);
        Assert.Matches("MinHeight\\s*=\\s*DefaultMinHeight;", code);
        Assert.Matches("MinHeight\\s*=\\s*Height;", code);
        Assert.InRange(Const(code, "DefaultMinHeight"), 200, 400);

        // every regular window: 320..800 wide (spec: 480; licenses 640)
        foreach (var (file, name, b, c) in Windows().Where(w => w.Base == "AppWindow"))
        {
            var call = Regex.Match(c, ":\\s*base\\((?<a>[^;{]*)\\)\\s*\\{");
            Assert.True(call.Success, name + ": base(...) call not found");
            var w = Regex.Match(call.Groups["a"].Value, "width:\\s*(?<v>[\\w.]+)");
            var value = !w.Success ? 480 : double.TryParse(w.Groups["v"].Value, NumberStyles.Float, CultureInfo.InvariantCulture, out var d) ? d : Const(c, w.Groups["v"].Value);
            Assert.InRange(value, 320, 800);
        }
    }

    [Fact] // design 15-19: 480 wide, tallest step 498 -> fixed height, no SizeToContent
    public void Wizard_HasAFixedSizeFromTheDesign()
    {
        var code = Src.Code(Path.Combine(Views, "WizardWindow.cs"));
        Assert.Matches("base\\(S\\.WizTitle, minimize: false, width: WizardWidth, height: WizardHeight\\)", code);
        Assert.Equal(480, Const(code, "WizardWidth"));
        Assert.InRange(Const(code, "WizardHeight"), 500, 600);
        Assert.DoesNotContain("SizeToContent", code, StringComparison.Ordinal);

        var app = Src.Code(Path.Combine(Views, "AppWindow.cs"));
        Assert.Matches("if \\(height is \\{\\s*\\} h\\)\\s*\\{\\s*SizeToContent = SizeToContent\\.Manual;", app);
    }

    [Fact]
    public void Flyouts_HaveAMinimumAroundTheCard()
    {
        var code = Src.Code(Path.Combine(Views, "FlyoutWindow.cs"));
        Assert.Equal(340, Const(code, "CardWidth"));
        Assert.InRange(Const(code, "CardMinHeight"), 100, 300);
        Assert.Contains("MinWidth = CardWidth + 2 * ShadowMargin;", code, StringComparison.Ordinal);
        Assert.Contains("MinHeight = CardMinHeight + 2 * ShadowMargin;", code, StringComparison.Ordinal);
    }

    [Fact] // every step of the wizard puts content in the window and a button bar under it
    public void Wizard_EveryStepSetsContentAndButtons()
    {
        var code = Src.Code(Path.Combine(Views, "WizardWindow.cs"));
        var steps = new[] { "ShowSearching", "ShowOne", "ShowList", "ShowNone", "ShowDone" };
        foreach (var step in steps)
        {
            var m = Regex.Match(code, "private void " + step + "\\([^)]*\\)\\s*\\{(?<body>.*?)\\n    \\}", RegexOptions.Singleline);
            Assert.True(m.Success, step + " not found");
            var body = m.Groups["body"].Value;
            Assert.Matches("\\bBody\\s*=\\s*body;", body);
            Assert.Matches("_step\\s*=\\s*\"\\w+\";", body);
            Assert.Contains("Steps(", body, StringComparison.Ordinal);
            Assert.Contains("Ui.Text(S.Wiz", body, StringComparison.Ordinal);
            Assert.Contains("SetFooter(", body, StringComparison.Ordinal);
        }

        // the first step is shown from the constructor (through Search), before the window is shown
        var ctor = Regex.Match(code, "public WizardWindow\\([^)]*\\)(?<body>.*?)\\n    \\}", RegexOptions.Singleline);
        Assert.True(ctor.Success, "constructor not found");
        Assert.Matches("\\n        Search\\(\\);", ctor.Groups["body"].Value);
        Assert.Matches("private void Search\\(\\)\\s*\\{[^}]*ShowSearching\\(slow: false\\);", code);
    }

    [Fact] // a runtime error in any window: logged, and the user is told once (not only at start-up)
    public void RuntimeErrors_AreLoggedAndShownOnce()
    {
        var app = Src.Code(Path.Combine(Src.App, "App.xaml.cs"));
        Assert.Contains("FailureReport.Runtime(\"ui\", e.Exception);", app, StringComparison.Ordinal);
        Assert.Contains("FailureReport.Runtime(\"task\"", app, StringComparison.Ordinal);
        Assert.Contains("FailureReport.Runtime(\"domain\", e);", app, StringComparison.Ordinal);
        Assert.Contains("e.Handled = true;", app, StringComparison.Ordinal);

        var report = Src.Code(Path.Combine(Src.App, "Platform", "FailureReport.cs"));
        Assert.Contains("ErrorLog.Write(where, e);", report, StringComparison.Ordinal);
        Assert.Contains("ErrorLog.OpenInExplorer();", report, StringComparison.Ordinal);
        Assert.Contains("Show(S.RuntimeFailed);", report, StringComparison.Ordinal);
        Assert.Equal(2, Regex.Matches(report, "Interlocked\\.Exchange\\(ref _shown, 1\\) != 0").Count);
        Assert.Equal("משהו לא עבד כמו שצריך. אפשר לשלוח את הקובץ שנפתח לצוות.", S.RuntimeFailed);
    }
}
