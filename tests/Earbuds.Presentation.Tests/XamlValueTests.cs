// Copyright (C) 2026 ChikApps
// SPDX-License-Identifier: GPL-3.0-only
//
// WPF cannot run on Linux, so these tests read every .xaml file of the app as XML and check, with ports of WPF's own
// converters, the values that WPF turns into objects while loading (paths, colors, rectangles, points, numbers,
// enums, resource references). A value WPF cannot convert does not fail the build: it fails on the user's PC at
// the first moment the resource is used, with the unreadable message "Provide value on
// 'System.Windows.Baml2006.TypeConverterMarkupExtension' threw an exception". (0.9.0: PathGeometry
// Figures="F1 M ..." - the Figures parser does not accept the fill-rule prefix.)

using System.Globalization;
using System.Text.RegularExpressions;
using System.Xml.Linq;
using Xunit;

namespace Earbuds.Presentation.Tests;

public sealed class XamlValueTests
{
    private static readonly XNamespace X = "http://schemas.microsoft.com/winfx/2006/xaml";

    private static readonly Regex Hex = new("^#([0-9A-Fa-f]{3,4}|[0-9A-Fa-f]{6}|[0-9A-Fa-f]{8})$", RegexOptions.Compiled);

    private static readonly HashSet<string> ColorNames = new(
        ("Transparent AliceBlue AntiqueWhite Aqua Aquamarine Azure Beige Bisque Black BlanchedAlmond Blue BlueViolet Brown " +
         "BurlyWood CadetBlue Chartreuse Chocolate Coral CornflowerBlue Cornsilk Crimson Cyan DarkBlue DarkCyan DarkGoldenrod " +
         "DarkGray DarkGreen DarkKhaki DarkMagenta DarkOliveGreen DarkOrange DarkOrchid DarkRed DarkSalmon DarkSeaGreen " +
         "DarkSlateBlue DarkSlateGray DarkTurquoise DarkViolet DeepPink DeepSkyBlue DimGray DodgerBlue Firebrick FloralWhite " +
         "ForestGreen Fuchsia Gainsboro GhostWhite Gold Goldenrod Gray Green GreenYellow Honeydew HotPink IndianRed Indigo Ivory " +
         "Khaki Lavender LavenderBlush LawnGreen LemonChiffon LightBlue LightCoral LightCyan LightGoldenrodYellow LightGray " +
         "LightGreen LightPink LightSalmon LightSeaGreen LightSkyBlue LightSlateGray LightSteelBlue LightYellow Lime LimeGreen " +
         "Linen Magenta Maroon MediumAquamarine MediumBlue MediumOrchid MediumPurple MediumSeaGreen MediumSlateBlue " +
         "MediumSpringGreen MediumTurquoise MediumVioletRed MidnightBlue MintCream MistyRose Moccasin NavajoWhite Navy OldLace " +
         "Olive OliveDrab Orange OrangeRed Orchid PaleGoldenrod PaleGreen PaleTurquoise PaleVioletRed PapayaWhip PeachPuff Peru " +
         "Pink Plum PowderBlue Purple Red RosyBrown RoyalBlue SaddleBrown Salmon SandyBrown SeaGreen SeaShell Sienna Silver " +
         "SkyBlue SlateBlue SlateGray Snow SpringGreen SteelBlue Tan Teal Thistle Tomato Turquoise Violet Wheat White WhiteSmoke " +
         "Yellow YellowGreen").Split(' '),
        StringComparer.OrdinalIgnoreCase);

    private static readonly Dictionary<string, string[]> Enums = new()
    {
        ["StrokeStartLineCap"] = new[] { "Flat", "Square", "Round", "Triangle" },
        ["StrokeEndLineCap"] = new[] { "Flat", "Square", "Round", "Triangle" },
        ["StartLineCap"] = new[] { "Flat", "Square", "Round", "Triangle" },
        ["EndLineCap"] = new[] { "Flat", "Square", "Round", "Triangle" },
        ["StrokeLineJoin"] = new[] { "Miter", "Bevel", "Round" },
        ["LineJoin"] = new[] { "Miter", "Bevel", "Round" },
        ["HorizontalAlignment"] = new[] { "Left", "Center", "Right", "Stretch" },
        ["VerticalAlignment"] = new[] { "Top", "Center", "Bottom", "Stretch" },
        ["FlowDirection"] = new[] { "LeftToRight", "RightToLeft" },
        ["Visibility"] = new[] { "Visible", "Hidden", "Collapsed" },
        ["Orientation"] = new[] { "Horizontal", "Vertical" },
        ["TextWrapping"] = new[] { "NoWrap", "Wrap", "WrapWithOverflow" },
        ["FillRule"] = new[] { "EvenOdd", "Nonzero" },
        ["ShutdownMode"] = new[] { "OnLastWindowClose", "OnMainWindowClose", "OnExplicitShutdown" },
    };

    private static readonly string[] BrushProps = { "Brush", "Fill", "Stroke", "Background", "BorderBrush", "Foreground" };
    private static readonly string[] PointProps = { "Center", "StartPoint", "EndPoint", "GradientOrigin", "RenderTransformOrigin" };
    private static readonly string[] ThicknessProps = { "Margin", "Padding", "BorderThickness" };
    private static readonly string[] DoubleProps =
    {
        "Width", "Height", "MinWidth", "MinHeight", "MaxWidth", "MaxHeight", "RadiusX", "RadiusY", "Offset", "Thickness",
        "MiterLimit", "Opacity", "StrokeThickness", "FontSize", "LineHeight",
    };

    // ---------- locating the files ----------

    private static string AppDir
    {
        get
        {
            var dir = new DirectoryInfo(AppContext.BaseDirectory);
            while (dir is not null && !File.Exists(Path.Combine(dir.FullName, "Earbuds.sln")))
            {
                dir = dir.Parent;
            }

            Assert.NotNull(dir);
            return Path.Combine(dir!.FullName, "src", "Earbuds.App");
        }
    }

    private static string[] XamlFiles() =>
        Directory.GetFiles(AppDir, "*.xaml", SearchOption.AllDirectories)
            .Where(f => !f.Contains(Path.DirectorySeparatorChar + "obj" + Path.DirectorySeparatorChar) &&
                        !f.Contains(Path.DirectorySeparatorChar + "bin" + Path.DirectorySeparatorChar))
            .OrderBy(f => f, StringComparer.Ordinal)
            .ToArray();

    private static string Rel(string file) => Path.GetRelativePath(AppDir, file);

    // ---------- the parser port itself must keep rejecting what WPF rejects ----------

    [Theory]
    [InlineData("F1 M 0 0 L 1 1")] // the 0.9.0 crash: fill-rule prefix in Figures
    [InlineData("L 1 1")] // must start with M
    [InlineData("M 0 0 L 1")] // missing coordinate
    [InlineData("M 0 0 A 1 1 0 2 0 5 5")] // arc flag must be 0 or 1
    [InlineData("M 0,,0")] // two commas
    [InlineData("M 0 0 Z 5 5")] // number after Z
    [InlineData("M 0 0 X 1 1")] // unknown command
    public void Parser_RejectsWhatWpfRejects(string path) => Assert.NotNull(WpfPathParser.ValidateFigures(path));

    [Theory]
    [InlineData("M 0 0 L 10 10 Z")]
    [InlineData("M0,0 L1,1 5,5 M2,2 H4 V9 Z")]
    [InlineData("M 54 -81 A 28 28 0 0 1 82 -53 V -29.8")]
    [InlineData("M 0 0 C 1 1 2 2 3 3 S 4 4 5 5 Q 1 1 2 2 T 3 3")]
    [InlineData("")] // an empty path is allowed
    public void Parser_AcceptsWhatWpfAccepts(string path) => Assert.Null(WpfPathParser.ValidateFigures(path));

    [Fact]
    public void GeometryParse_AcceptsFillRulePrefix_FiguresDoesNot()
    {
        Assert.Null(WpfPathParser.ValidateGeometry("F1 M 0 0 L 1 1"));
        Assert.Null(WpfPathParser.ValidateGeometry("F0M0,0L1,1"));
        Assert.NotNull(WpfPathParser.ValidateGeometry("F2 M 0 0"));
        Assert.NotNull(WpfPathParser.ValidateGeometry("f1 M 0 0")); // lowercase f is not a prefix
        Assert.NotNull(WpfPathParser.ValidateFigures("F1 M 0 0"));
    }

    // ---------- every value in every XAML file ----------

    [Fact]
    public void XamlFiles_Exist()
    {
        var files = XamlFiles().Select(Rel).ToList();
        Assert.Contains("App.xaml", files);
        Assert.Contains(Path.Combine("Theme", "Styles.xaml"), files);
        Assert.True(files.Count(f => f.StartsWith("Art" + Path.DirectorySeparatorChar, StringComparison.Ordinal)) >= 12);
    }

    [Fact]
    public void Xaml_EveryValueConverts()
    {
        var problems = new List<string>();
        var checkedPaths = 0;
        var checkedValues = 0;
        foreach (var file in XamlFiles())
        {
            var doc = XDocument.Load(file, LoadOptions.SetLineInfo);
            foreach (var e in doc.Descendants())
            {
                var tag = e.Name.LocalName;
                string Where(string what) => $"{Rel(file)}:{((System.Xml.IXmlLineInfo)e).LineNumber} <{tag}> {what}";

                // path text inside <Geometry>..</Geometry> (Geometry.Parse: a fill-rule prefix is fine here)
                if (tag == "Geometry" && e.Name.Namespace != X && !e.HasElements)
                {
                    checkedPaths++;
                    var err = WpfPathParser.ValidateGeometry(e.Value);
                    if (err is not null)
                    {
                        problems.Add(Where("path: " + err + " | " + Cut(e.Value)));
                    }
                }

                foreach (var a in e.Attributes())
                {
                    if (a.IsNamespaceDeclaration || a.Name.Namespace != XNamespace.None)
                    {
                        continue; // x:Key, xmlns, po:Freeze, mc:Ignorable
                    }

                    var name = a.Name.LocalName;
                    var value = a.Value;
                    if (value.StartsWith('{'))
                    {
                        continue; // markup extension: see Xaml_ResourceReferencesResolve
                    }

                    checkedValues++;
                    if (name == "Figures")
                    {
                        checkedPaths++;
                        var err = WpfPathParser.ValidateFigures(value);
                        if (err is not null)
                        {
                            problems.Add(Where("Figures: " + err + " | " + Cut(value)));
                        }

                        continue;
                    }

                    if (name == "Data")
                    {
                        checkedPaths++;
                        var err = WpfPathParser.ValidateGeometry(value);
                        if (err is not null)
                        {
                            problems.Add(Where("Data: " + err + " | " + Cut(value)));
                        }

                        continue;
                    }

                    // Setter: the Value is converted to the type of the property named in Property=
                    var prop = name;
                    if (tag == "Setter" && name == "Value")
                    {
                        prop = e.Attribute("Property")?.Value ?? string.Empty;
                    }
                    else if (tag == "Setter" || tag == "Trigger")
                    {
                        continue; // Property=, TargetName=, Value= of a Trigger (a state like True)
                    }

                    var bad = CheckValue(tag, prop, value);
                    if (bad is not null)
                    {
                        problems.Add(Where($"{name}=\"{Cut(value)}\": {bad}"));
                    }
                }
            }
        }

        Assert.True(checkedPaths > 1000, "expected the drawings to be checked, got " + checkedPaths);
        Assert.True(checkedValues > 10000, "expected many values, got " + checkedValues);
        Assert.True(problems.Count == 0, "XAML values WPF cannot convert:\n" + string.Join("\n", problems.Take(30)));
    }

    [Fact] // the 0.9.0 crash, pinned: no Figures string may start with the fill-rule prefix
    public void Xaml_NoFillRulePrefixInFigures()
    {
        foreach (var file in XamlFiles())
        {
            foreach (var e in XDocument.Load(file).Descendants().Where(d => d.Name.LocalName == "PathGeometry"))
            {
                var figures = e.Attribute("Figures")?.Value;
                if (figures is null)
                {
                    continue;
                }

                Assert.False(figures.TrimStart().StartsWith('F'), Rel(file) + ": PathGeometry Figures must not start with F0/F1; use the FillRule attribute");
                Assert.True(e.Attribute("FillRule") is not null, Rel(file) + ": PathGeometry needs FillRule (WPF default is EvenOdd, the SVG drawing is Nonzero)");
            }
        }
    }

    [Fact] // string literals Geometry.Parse("...") in the code go through the same parser at run time
    public void Code_GeometryParseLiterals_Convert()
    {
        var count = 0;
        foreach (var cs in Directory.GetFiles(AppDir, "*.cs", SearchOption.AllDirectories)
                     .Where(f => !f.Contains(Path.DirectorySeparatorChar + "obj" + Path.DirectorySeparatorChar)))
        {
            var text = File.ReadAllText(cs);
            foreach (Match m in Regex.Matches(text, "Geometry\\.Parse\\(\\s*(\"F\\d \"\\s*\\+\\s*)?(?:\"(?<lit>[^\"]*)\"|(?<id>\\w+))\\s*\\)"))
            {
                var path = m.Groups["lit"].Success ? m.Groups["lit"].Value : null;
                if (path is null)
                {
                    var c = Regex.Match(text, "const string " + m.Groups["id"].Value + " = \"(?<v>[^\"]*)\"");
                    Assert.True(c.Success, Rel(cs) + ": cannot find constant " + m.Groups["id"].Value);
                    path = c.Groups["v"].Value;
                }

                if (m.Groups[1].Success)
                {
                    path = m.Groups[1].Value.Trim().TrimEnd('+').Trim().Trim('"') + path;
                }

                count++;
                Assert.True(WpfPathParser.ValidateGeometry(path) is null, Rel(cs) + ": " + WpfPathParser.ValidateGeometry(path) + " | " + path);
            }
        }

        Assert.True(count >= 3, "expected the Geometry.Parse calls in the code, got " + count);
    }

    // ---------- resources: every reference resolves, in an order WPF accepts ----------

    [Fact]
    public void Xaml_ResourceReferencesResolve()
    {
        var problems = new List<string>();
        var theme = ThemeKeys();
        var styles = Path.Combine(AppDir, "Theme", "Styles.xaml");
        var styleKeys = TopLevelKeys(styles);

        foreach (var file in XamlFiles())
        {
            var doc = XDocument.Load(file, LoadOptions.SetLineInfo);
            var root = doc.Root!;
            if (root.Name.LocalName != "ResourceDictionary")
            {
                continue;
            }

            var defined = new HashSet<string>();
            var allKeys = new HashSet<string>();
            foreach (var item in root.Elements().Where(c => !c.Name.LocalName.Contains('.') && c.Name.LocalName != "ResourceDictionary"))
            {
                var key = item.Attribute(X + "Key")?.Value;
                if (key is not null && !allKeys.Add(key))
                {
                    problems.Add($"{Rel(file)}: duplicate x:Key '{key}' (WPF throws when loading the dictionary)");
                }
            }

            foreach (var item in root.Elements())
            {
                foreach (var e in item.DescendantsAndSelf())
                {
                    foreach (var a in e.Attributes().Where(a => a.Value.StartsWith('{')))
                    {
                        var m = Regex.Match(a.Value, "^\\{(StaticResource|DynamicResource)\\s+(\\S+?)\\s*\\}$");
                        if (!m.Success)
                        {
                            continue;
                        }

                        var kind = m.Groups[1].Value;
                        var key = m.Groups[2].Value;
                        var where = $"{Rel(file)}:{((System.Xml.IXmlLineInfo)e).LineNumber} {kind} '{key}'";
                        if (kind == "StaticResource" && !defined.Contains(key))
                        {
                            problems.Add(where + " is not defined BEFORE its use in this dictionary");
                        }

                        if (kind == "DynamicResource" && !theme.Contains(key) && !allKeys.Contains(key))
                        {
                            problems.Add(where + " is not a theme brush (ThemeManager) nor a key of this dictionary");
                        }
                    }
                }

                var k = item.Attribute(X + "Key")?.Value;
                if (k is not null)
                {
                    defined.Add(k);
                }
            }
        }

        Assert.True(problems.Count == 0, string.Join("\n", problems.Take(30)));

        // what the C# code asks the dictionaries for
        var codeProblems = new List<string>();
        foreach (var cs in Directory.GetFiles(AppDir, "*.cs", SearchOption.AllDirectories)
                     .Where(f => !f.Contains(Path.DirectorySeparatorChar + "obj" + Path.DirectorySeparatorChar)))
        {
            var text = File.ReadAllText(cs);
            foreach (Match m in Regex.Matches(text, "(?:SetResourceReference\\([^,]+,\\s*|FindResource\\(|TryFindResource\\(|Res<\\w+>\\()\"(?<k>\\w+)\""))
            {
                var key = m.Groups["k"].Value;
                if (!theme.Contains(key) && !styleKeys.Contains(key))
                {
                    codeProblems.Add($"{Rel(cs)}: resource '{key}' does not exist");
                }
            }

            foreach (Match m in Regex.Matches(text, "\"(?<k>Ico[A-Z]\\w+)\""))
            {
                if (!styleKeys.Contains(m.Groups["k"].Value))
                {
                    codeProblems.Add($"{Rel(cs)}: icon geometry '{m.Groups["k"].Value}' does not exist in Styles.xaml");
                }
            }
        }

        Assert.True(codeProblems.Count == 0, string.Join("\n", codeProblems.Distinct().Take(30)));
        Assert.NotEmpty(styleKeys);
    }

    [Fact] // ArtLibrary asks for exactly these keys
    public void Art_DictionariesHaveTheKeysArtLibraryUses()
    {
        var art = Path.Combine(AppDir, "Art");
        foreach (var fam in new[] { "short", "long", "pro", "generic" })
        {
            foreach (var theme in new[] { "light", "dark" })
            {
                var keys = TopLevelKeys(Path.Combine(art, $"Art.{fam}.{theme}.xaml"));
                foreach (var k in new[] { "scene.cc", "scene.co", "scene.oc", "scene.oo", "bud.l", "bud.r" })
                {
                    Assert.Contains(k, keys);
                }
            }
        }

        foreach (var theme in new[] { "light", "dark" })
        {
            Assert.Contains("max", TopLevelKeys(Path.Combine(art, $"Art.max.{theme}.xaml")));
        }

        Assert.Contains("appicon", TopLevelKeys(Path.Combine(art, "Art.appicon.xaml")));
        var leds = TopLevelKeys(Path.Combine(art, "Art.leds.xaml"));
        foreach (var fam in new[] { "short", "long", "pro" })
        {
            Assert.Contains($"led.{fam}.a", leds);
            Assert.Contains($"led.{fam}.g", leds);
        }
    }

    // ---------- helpers ----------

    private static string Cut(string s) => s.Length <= 70 ? s : s[..70] + "...";

    private static HashSet<string> TopLevelKeys(string file) =>
        XDocument.Load(file).Root!.Elements().Select(e => e.Attribute(X + "Key")?.Value).Where(k => k is not null).Select(k => k!).ToHashSet();

    private static HashSet<string> ThemeKeys()
    {
        var text = File.ReadAllText(Path.Combine(AppDir, "Theme", "ThemeManager.cs"));
        var keys = new HashSet<string>();
        foreach (Match m in Regex.Matches(text, "(?:Set\\(|_brushes\\[)\"(?<k>\\w+)\""))
        {
            keys.Add(m.Groups["k"].Value);
        }

        Assert.True(keys.Count > 30, "theme keys not found in ThemeManager.cs");
        return keys;
    }

    private static string? CheckValue(string tag, string prop, string value)
    {
        value = value.Trim();
        if (Enums.TryGetValue(prop, out var names))
        {
            return names.Contains(value) ? null : "not one of " + string.Join("/", names);
        }

        if (prop == "Color" || BrushProps.Contains(prop))
        {
            if (value.StartsWith('#'))
            {
                return Hex.IsMatch(value) ? null : "bad color (need #RGB #ARGB #RRGGBB #AARRGGBB)";
            }

            return ColorNames.Contains(value) ? null : "unknown color name";
        }

        if (prop == "Rect")
        {
            if (value == "Empty")
            {
                return null;
            }

            var n = Numbers(value, 4);
            if (n is null)
            {
                return "Rect needs 4 numbers";
            }

            return n[2] < 0 || n[3] < 0 ? "Rect with negative width/height throws in WPF" : null;
        }

        if (PointProps.Contains(prop))
        {
            return Numbers(value, 2) is null ? "needs 2 numbers" : null;
        }

        if (prop == "Matrix")
        {
            return value == "Identity" || Numbers(value, 6) is not null ? null : "Matrix needs 6 numbers";
        }

        if (ThicknessProps.Contains(prop))
        {
            return Numbers(value, 1) is not null || Numbers(value, 2) is not null || Numbers(value, 4) is not null ? null : "Thickness needs 1, 2 or 4 numbers";
        }

        if (prop == "CornerRadius")
        {
            return Numbers(value, 1) is not null || Numbers(value, 4) is not null ? null : "CornerRadius needs 1 or 4 numbers";
        }

        if (DoubleProps.Contains(prop))
        {
            if ((prop is "Width" or "Height" or "MinWidth" or "MinHeight" or "MaxWidth" or "MaxHeight") && value == "Auto")
            {
                return null;
            }

            return Numbers(value, 1) is null ? "not a number" : null;
        }

        return null; // not a property this test knows
    }

    private static double[]? Numbers(string value, int count)
    {
        var parts = Regex.Split(value.Trim(), "[,\\s]+");
        if (parts.Length != count)
        {
            return null;
        }

        var result = new double[count];
        for (var i = 0; i < count; i++)
        {
            if (!double.TryParse(parts[i], NumberStyles.Float, CultureInfo.InvariantCulture, out result[i]))
            {
                return null;
            }
        }

        return result;
    }
}
