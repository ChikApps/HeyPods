// Copyright (C) 2026 ChikApps
// SPDX-License-Identifier: GPL-3.0-only
//
// Every resource reference must lead to a resource of the type the property needs (0.9.1: the wizard window stayed
// a 160 px strip). "Card" was both the card brush (ThemeManager) and a Border Style (Styles.xaml). Merged
// dictionaries are searched last-first, Styles.xaml is merged after the theme brushes, so
// SetResourceReference(Background, "Card") got the Style: "'System.Windows.Style' is not a valid value for property
// 'Background'", thrown inside Window.Show(). Nothing in the build sees this: WPF only finds out at run time.
//
// The checks, all without WPF (it does not run on Linux):
//  - one catalog of the application's resources: key -> kind (Brush, Style, Geometry, ...), from ThemeManager.cs
//    and Styles.xaml, with no key defined twice across the dictionaries that are merged together;
//  - every reference, in XAML ({StaticResource}/{DynamicResource} on an attribute or a Setter) and in code
//    (SetResourceReference, Res<T>, FindResource, TryFindResource, and the helpers that forward a key), has the
//    kind its property or cast needs. A key passed through a variable must go through a helper this test knows.

using System.Text.RegularExpressions;
using System.Xml.Linq;
using Xunit;

namespace Earbuds.Presentation.Tests;

internal static class Src
{
    public static readonly XNamespace X = "http://schemas.microsoft.com/winfx/2006/xaml";

    public static string Root
    {
        get
        {
            var dir = new DirectoryInfo(AppContext.BaseDirectory);
            while (dir is not null && !File.Exists(Path.Combine(dir.FullName, "Earbuds.sln")))
            {
                dir = dir.Parent;
            }

            Assert.NotNull(dir);
            return dir!.FullName;
        }
    }

    public static string App => Path.Combine(Root, "src", "Earbuds.App");

    public static IEnumerable<string> CodeFiles() => Directory.GetFiles(App, "*.cs", SearchOption.AllDirectories)
        .Where(f => !f.Contains(Path.DirectorySeparatorChar + "obj" + Path.DirectorySeparatorChar)
                    && !f.Contains(Path.DirectorySeparatorChar + "bin" + Path.DirectorySeparatorChar));

    public static IEnumerable<string> XamlFiles() => Directory.GetFiles(App, "*.xaml", SearchOption.AllDirectories)
        .Where(f => !f.Contains(Path.DirectorySeparatorChar + "obj" + Path.DirectorySeparatorChar)
                    && !f.Contains(Path.DirectorySeparatorChar + "bin" + Path.DirectorySeparatorChar));

    public static string Rel(string f) => Path.GetRelativePath(App, f);

    /// <summary>Code without // comments (keeps strings; good enough for our files).</summary>
    public static string Code(string file) => Regex.Replace(File.ReadAllText(file), @"(?m)^\s*//.*$", string.Empty);

    /// <summary>The argument list of each call that starts at a match of <paramref name="callStart"/> (which must
    /// end right after the opening parenthesis), split at top-level commas.</summary>
    public static IEnumerable<(int Line, List<string> Args)> Calls(string code, string callStart) =>
        CallMatches(code, callStart).Select(c => (c.Line, c.Args));

    public static IEnumerable<(int Line, List<string> Args, Match M)> CallMatches(string code, string callStart)
    {
        foreach (Match m in Regex.Matches(code, callStart))
        {
            var i = m.Index + m.Length;
            var depth = 1;
            var args = new List<string>();
            var cur = new System.Text.StringBuilder();
            var inString = false;
            for (; i < code.Length && depth > 0; i++)
            {
                var c = code[i];
                if (inString)
                {
                    cur.Append(c);
                    if (c == '\\')
                    {
                        cur.Append(code[++i]);
                    }
                    else if (c == '"')
                    {
                        inString = false;
                    }

                    continue;
                }

                if (c == '"')
                {
                    inString = true;
                }
                else if (c is '(' or '[' or '{')
                {
                    depth++;
                }
                else if (c is ')' or ']' or '}')
                {
                    depth--;
                    if (depth == 0)
                    {
                        break;
                    }
                }
                else if (c == ',' && depth == 1)
                {
                    args.Add(cur.ToString().Trim());
                    cur.Clear();
                    continue;
                }

                cur.Append(c);
            }

            if (cur.ToString().Trim().Length > 0)
            {
                args.Add(cur.ToString().Trim());
            }

            yield return (code[..m.Index].Count(ch => ch == '\n') + 1, args, m);
        }
    }
}

public sealed class ResourceTypeTests
{
    // property name -> kind of resource it takes
    private static readonly Dictionary<string, string> PropertyKind = new()
    {
        ["Background"] = "Brush",
        ["Foreground"] = "Brush",
        ["BorderBrush"] = "Brush",
        ["Fill"] = "Brush",
        ["Stroke"] = "Brush",
        ["Brush"] = "Brush",
        ["OpacityMask"] = "Brush",
        ["CaretBrush"] = "Brush",
        ["SelectionBrush"] = "Brush",
        ["Style"] = "Style",
        ["BasedOn"] = "Style",
        ["FontFamily"] = "FontFamily",
        ["Data"] = "Geometry",
        ["Geometry"] = "Geometry",
        ["ClipGeometry"] = "Geometry",
        ["Clip"] = "Geometry",
        ["CornerRadius"] = "CornerRadius",
        ["Color"] = "Color",
        ["Template"] = "ControlTemplate",
        ["Source"] = "ImageSource",
        ["ImageSource"] = "ImageSource",
        ["Drawing"] = "Drawing",
    };

    // C# type in a cast / Res<T> -> kind
    private static readonly Dictionary<string, string> TypeKind = new()
    {
        ["Brush"] = "Brush",
        ["SolidColorBrush"] = "Brush",
        ["Style"] = "Style",
        ["Geometry"] = "Geometry",
        ["FontFamily"] = "FontFamily",
        ["CornerRadius"] = "CornerRadius",
        ["Color"] = "Color",
        ["ControlTemplate"] = "ControlTemplate",
        ["ImageSource"] = "ImageSource",
        ["DrawingImage"] = "ImageSource",
    };

    /// <summary>Variables that carry a resource key into SetResourceReference / Res / FindResource, per file. Each
    /// helper's callers are checked below (<see cref="HelperCalls"/>). A new variable must be added here AND
    /// have its callers checked, or this test fails.</summary>
    private static readonly HashSet<string> KnownKeyVariables = new()
    {
        "Controls/Ui.cs:style", "Controls/Ui.cs:brush", "Controls/Ui.cs:geometryKey", "Controls/Ui.cs:key",
        "Views/AppWindow.cs:style",
        "Views/AboutWindow.cs:fill", "Views/AboutWindow.cs:stroke", "Views/AboutWindow.cs:icon",
        "Controls/BatteryRing.cs:key",
    };

    // ---------- the catalog ----------

    private static string KindOfElement(string localName) => localName switch
    {
        "Style" => "Style",
        "ControlTemplate" => "ControlTemplate",
        "DataTemplate" => "DataTemplate",
        "Geometry" or "PathGeometry" or "StreamGeometry" or "RectangleGeometry" or "EllipseGeometry" or "GeometryGroup" or "CombinedGeometry" => "Geometry",
        "FontFamily" => "FontFamily",
        "Color" => "Color",
        "CornerRadius" => "CornerRadius",
        "Thickness" => "Thickness",
        "Double" => "Double",
        "DrawingImage" or "BitmapImage" => "ImageSource",
        "DrawingGroup" or "GeometryDrawing" => "Drawing",
        _ when localName.EndsWith("Brush", StringComparison.Ordinal) => "Brush",
        _ => "?" + localName,
    };

    /// <summary>key -> kind for the brushes ThemeManager puts in its dictionary.</summary>
    private static Dictionary<string, string> ThemeCatalog()
    {
        var text = File.ReadAllText(Path.Combine(Src.App, "Theme", "ThemeManager.cs"));
        var kinds = new Dictionary<string, string>();
        var problems = new List<string>();
        foreach (Match m in Regex.Matches(text, "Set\\(\"(?<k>\\w+)\""))
        {
            kinds[m.Groups["k"].Value] = "Brush"; // Set() = Freeze(new SolidColorBrush(...))
        }

        Assert.Contains("_brushes[key] = Freeze(new SolidColorBrush(", text);
        foreach (Match m in Regex.Matches(text, "_brushes\\[\"(?<k>\\w+)\"\\]\\s*=\\s*(?<v>[^;]+);"))
        {
            var key = m.Groups["k"].Value;
            var v = m.Groups["v"].Value.Trim();
            string kind;
            if (v.Contains("SolidColorBrush", StringComparison.Ordinal))
            {
                kind = "Brush";
            }
            else if (v.StartsWith("new CornerRadius", StringComparison.Ordinal))
            {
                kind = "CornerRadius";
            }
            else if (v == "Accent" || Regex.IsMatch(v, "^(d \\? )?Color\\.From"))
            {
                kind = "Color";
            }
            else if (Regex.Match(v, "^_brushes\\[\"(?<o>\\w+)\"\\]$") is { Success: true } alias && kinds.TryGetValue(alias.Groups["o"].Value, out var ok))
            {
                kind = ok;
            }
            else
            {
                problems.Add($"ThemeManager.cs: cannot tell the type of '{key}' = {v} (teach ResourceTypeTests.ThemeCatalog)");
                continue;
            }

            if (kinds.TryGetValue(key, out var before) && before != kind)
            {
                problems.Add($"ThemeManager.cs: '{key}' set as {before} and as {kind}");
            }

            kinds[key] = kind;
        }

        Assert.True(problems.Count == 0, string.Join("\n", problems));
        Assert.True(kinds.Count > 30, "theme keys not found in ThemeManager.cs");
        return kinds;
    }

    private static Dictionary<string, string> DictionaryCatalog(string file, List<string> problems)
    {
        var kinds = new Dictionary<string, string>();
        foreach (var e in XDocument.Load(file).Root!.Elements())
        {
            var key = e.Attribute(Src.X + "Key")?.Value;
            if (key is null)
            {
                continue;
            }

            if (kinds.ContainsKey(key))
            {
                problems.Add($"{Src.Rel(file)}: x:Key '{key}' defined twice");
            }

            kinds[key] = KindOfElement(e.Name.LocalName);
        }

        return kinds;
    }

    /// <summary>What the application resolves a key to: the merged dictionaries are the theme brushes (inserted
    /// first by ThemeManager.Initialize) and Styles.xaml (App.xaml).</summary>
    private static Dictionary<string, string> AppCatalog(List<string> problems)
    {
        var app = XDocument.Load(Path.Combine(Src.App, "App.xaml")).ToString();
        Assert.Contains("/Theme/Styles.xaml", app);
        var theme = ThemeCatalog();
        var styles = DictionaryCatalog(Path.Combine(Src.App, "Theme", "Styles.xaml"), problems);
        foreach (var key in theme.Keys.Intersect(styles.Keys))
        {
            problems.Add($"key '{key}' is a {theme[key]} in ThemeManager.cs AND a {styles[key]} in Styles.xaml: WPF returns the one in " +
                         "the dictionary merged last (Styles.xaml) to everyone, so the other is never seen (0.9.1 wizard bug with 'Card')");
        }

        var all = new Dictionary<string, string>(theme);
        foreach (var (k, v) in styles)
        {
            all[k] = v; // Styles.xaml wins, as in WPF
        }

        return all;
    }

    [Fact]
    public void Catalog_NoKeyDefinedTwiceAcrossMergedDictionaries()
    {
        var problems = new List<string>();
        AppCatalog(problems);
        foreach (var f in Src.XamlFiles().Where(f => f.Contains(Path.DirectorySeparatorChar + "Art" + Path.DirectorySeparatorChar)))
        {
            DictionaryCatalog(f, problems);
        }

        Assert.True(problems.Count == 0, string.Join("\n", problems));
    }

    [Fact] // the 0.9.1 bug, pinned
    public void CardBrush_IsABrush_AndTheBorderStyleHasItsOwnName()
    {
        var problems = new List<string>();
        var cat = AppCatalog(problems);
        Assert.Equal("Brush", cat["Card"]);
        Assert.Equal("Style", cat["CardBox"]);
    }

    // ---------- XAML references ----------

    [Fact]
    public void Xaml_EveryReferenceHasTheRightType()
    {
        var problems = new List<string>();
        var app = AppCatalog(problems);
        var checkedCount = 0;
        foreach (var file in Src.XamlFiles())
        {
            var doc = XDocument.Load(file, LoadOptions.SetLineInfo);
            var local = doc.Root!.Name.LocalName == "ResourceDictionary" ? DictionaryCatalog(file, new List<string>()) : new Dictionary<string, string>();
            foreach (var e in doc.Root.DescendantsAndSelf())
            {
                foreach (var a in e.Attributes())
                {
                    var m = Regex.Match(a.Value, "^\\{(StaticResource|DynamicResource)\\s+(?:ResourceKey=)?(\\S+?)\\s*\\}$");
                    if (!m.Success)
                    {
                        continue;
                    }

                    var prop = a.Name.LocalName;
                    if (e.Name.LocalName == "Setter" && prop == "Value")
                    {
                        prop = e.Attribute("Property")?.Value ?? "?";
                    }

                    prop = prop[(prop.LastIndexOf('.') + 1)..];
                    var key = m.Groups[2].Value;
                    var where = $"{Src.Rel(file)}:{((System.Xml.IXmlLineInfo)e).LineNumber} {prop}=\"{a.Value}\"";
                    checkedCount++;
                    Check(where, prop, key, local, app, problems);
                }
            }
        }

        Assert.True(checkedCount > 1000, "too few XAML references found: " + checkedCount);
        Assert.True(problems.Count == 0, string.Join("\n", problems.Distinct().Take(40)));
    }

    private static void Check(string where, string prop, string key, Dictionary<string, string> local, Dictionary<string, string> app, List<string> problems)
    {
        if (!PropertyKind.TryGetValue(prop, out var need))
        {
            problems.Add($"{where}: property '{prop}' is not known to ResourceTypeTests.PropertyKind (add it with its type)");
            return;
        }

        if (!local.TryGetValue(key, out var have) && !app.TryGetValue(key, out have))
        {
            problems.Add($"{where}: resource '{key}' does not exist");
            return;
        }

        if (have != need)
        {
            problems.Add($"{where}: '{key}' is a {have}, but {prop} needs a {need}");
        }
    }

    // ---------- code references ----------

    private static IEnumerable<string> LiteralKeys(string expr)
    {
        // "Key" or cond ? "A" : "B"
        var t = expr.Trim();
        if (Regex.IsMatch(t, "^\"\\w+\"$"))
        {
            yield return t.Trim('"');
            yield break;
        }

        var m = Regex.Match(t, "^.+\\?\\s*\"(?<a>\\w+)\"\\s*:\\s*\"(?<b>\\w+)\"$");
        if (m.Success)
        {
            yield return m.Groups["a"].Value;
            yield return m.Groups["b"].Value;
        }
    }

    private sealed record Ref(string File, int Line, string Key, string Kind, string Via);

    /// <summary>All (key, required kind) pairs in the code. Keys passed in variables are reported in
    /// <paramref name="variables"/>.</summary>
    private static List<Ref> CodeRefs(List<string> variables)
    {
        var refs = new List<Ref>();
        foreach (var file in Src.CodeFiles())
        {
            var rel = Src.Rel(file).Replace('\\', '/');
            var code = Src.Code(file);

            void Add(int line, string expr, string kind, string via)
            {
                var keys = LiteralKeys(expr).ToList();
                if (keys.Count == 0)
                {
                    if (expr is not ("null" or "default"))
                    {
                        variables.Add($"{rel}:{expr}");
                    }

                    return;
                }

                foreach (var k in keys)
                {
                    refs.Add(new Ref(rel, line, k, kind, via));
                }
            }

            foreach (var (line, args) in Src.Calls(code, "SetResourceReference\\("))
            {
                var prop = Regex.Match(args[0], "(\\w+)Property$").Groups[1].Value;
                Assert.True(PropertyKind.TryGetValue(prop, out var need), $"{rel}:{line}: SetResourceReference on '{args[0]}' - property not known to ResourceTypeTests.PropertyKind");
                Add(line, args[1], need!, "SetResourceReference(" + prop + ")");
            }

            foreach (Match m in Regex.Matches(code, "Res<(?<t>[\\w.]+)>\\("))
            {
                var t = m.Groups["t"].Value;
                t = t[(t.LastIndexOf('.') + 1)..];
                if (t == "T")
                {
                    continue; // the definition
                }

                Assert.True(TypeKind.ContainsKey(t), $"{rel}: Res<{t}> - type not known to ResourceTypeTests.TypeKind");
            }

            foreach (var (line, args, m) in Src.CallMatches(code, "Res<(?:[\\w.]+\\.)?(?<t>\\w+)>\\("))
            {
                var t = m.Groups["t"].Value;
                if (t != "T")
                {
                    Add(line, args[0], TypeKind[t], "Res<" + t + ">");
                }
            }

            // (T)...FindResource("k") / (T)(TryFindResource("k") ?? ...) / TryFindResource(k) as T
            foreach (Match m in Regex.Matches(code, "\\((?:[\\w]+\\.)*(?<t>\\w+)\\)\\(?[\\w.]*?(?:Try)?FindResource\\((?<k>[^)]*)\\)"))
            {
                var line = code[..m.Index].Count(ch => ch == '\n') + 1;
                if (m.Groups["t"].Value == "T" && m.Groups["k"].Value == "key")
                {
                    continue; // Ui.Res<T>: its callers are checked as Res<...>
                }

                Assert.True(TypeKind.TryGetValue(m.Groups["t"].Value, out var kind), $"{rel}:{line}: cast to {m.Groups["t"].Value} not known to TypeKind");
                Add(line, m.Groups["k"].Value, kind!, "FindResource");
            }

            foreach (Match m in Regex.Matches(code, "TryFindResource\\((?<k>[^)]*)\\)\\s+as\\s+(?<t>\\w+)"))
            {
                var line = code[..m.Index].Count(ch => ch == '\n') + 1;
                Add(line, m.Groups["k"].Value, TypeKind[m.Groups["t"].Value], "TryFindResource");
            }

            foreach (Match m in Regex.Matches(code, "(?<![\\w.])(?:Try)?FindResource\\("))
            {
                var line = code[..m.Index].Count(ch => ch == '\n') + 1;
                var before = code[Math.Max(0, m.Index - 40)..m.Index];
                var after = code[m.Index..Math.Min(code.Length, m.Index + 80)];
                Assert.True(
                    Regex.IsMatch(before, "\\([\\w.]+\\)\\(?[\\w.]*$") || Regex.IsMatch(after, "^\\w+\\([^)]*\\)\\s+as\\s+\\w+") || before.EndsWith("=> (T)Application.Current.", StringComparison.Ordinal),
                    $"{rel}:{line}: FindResource without a cast this test understands");
            }
        }

        return refs;
    }

    /// <summary>Calls of the helpers that forward a key (see <see cref="KnownKeyVariables"/>).</summary>
    private static List<Ref> HelperCalls()
    {
        var refs = new List<Ref>();
        foreach (var file in Src.CodeFiles())
        {
            var rel = Src.Rel(file).Replace('\\', '/');
            var code = Src.Code(file);

            void Add(int line, string expr, string kind, string via)
            {
                foreach (var k in LiteralKeys(expr))
                {
                    refs.Add(new Ref(rel, line, k, kind, via));
                }
            }

            static string? Named(List<string> args, string name) =>
                args.Select(a => Regex.Match(a, "^" + name + ":\\s*(.+)$")).Where(m => m.Success).Select(m => m.Groups[1].Value).FirstOrDefault();

            // Ui.Text(text, style = "Body", size, semibold, brush, align)
            foreach (var (line, args) in Src.Calls(code, "(?<![\\w.])(?:Ui\\.)?Text\\("))
            {
                if (rel == "Controls/Ui.cs" && line < 30)
                {
                    continue; // the definition
                }

                if (args.Count > 1 && !args[1].Contains(':'))
                {
                    Add(line, args[1], "Style", "Ui.Text style");
                }

                if (args.Count > 4 && !args[4].Contains(':'))
                {
                    Add(line, args[4], "Brush", "Ui.Text brush");
                }

                if (Named(args, "brush") is { } b)
                {
                    Add(line, b, "Brush", "Ui.Text brush");
                }

                if (Named(args, "style") is { } s)
                {
                    Add(line, s, "Style", "Ui.Text style");
                }
            }

            // Ui.Button(text, style, click)
            foreach (var (line, args) in Src.Calls(code, "Ui\\.Button\\("))
            {
                Add(line, args[1], "Style", "Ui.Button style");
            }

            // Ui.Icon(geometryKey, size, viewBox, brush = "Text", ...)
            foreach (var (line, args) in Src.Calls(code, "Ui\\.Icon\\("))
            {
                Add(line, args[0], "Geometry", "Ui.Icon geometry");
                if (args.Count > 3 && !args[3].Contains(':'))
                {
                    Add(line, args[3], "Brush", "Ui.Icon brush");
                }

                if (Named(args, "brush") is { } b)
                {
                    Add(line, b, "Brush", "Ui.Icon brush");
                }
            }

            // AppWindow.CaptionButton(icon, size, style, click)
            foreach (var (line, args) in Src.Calls(code, "(?<![\\w.])CaptionButton\\("))
            {
                if (args.Count == 4 && !args[0].StartsWith("string ", StringComparison.Ordinal))
                {
                    Add(line, args[0], "Geometry", "CaptionButton icon");
                    Add(line, args[2], "Style", "CaptionButton style");
                }
            }

            // AboutWindow.Circle(fill, icon, stroke)
            foreach (var (line, args) in Src.Calls(code, "(?<![\\w.])Circle\\("))
            {
                if (args.Count == 3 && !args[0].StartsWith("string ", StringComparison.Ordinal))
                {
                    Add(line, args[0], "Brush", "Circle fill");
                    Add(line, args[1], "Geometry", "Circle icon");
                    Add(line, args[2], "Brush", "Circle stroke");
                }
            }

            // BatteryRing.Res(key) -> Brush
            if (rel == "Controls/BatteryRing.cs")
            {
                foreach (var (line, args) in Src.Calls(code, "(?<![\\w.<])Res\\("))
                {
                    if (!args[0].StartsWith("string ", StringComparison.Ordinal))
                    {
                        Add(line, args[0], "Brush", "BatteryRing.Res");
                    }
                }
            }
        }

        return refs;
    }

    [Fact]
    public void Code_EveryReferenceHasTheRightType()
    {
        var problems = new List<string>();
        var app = AppCatalog(problems);
        var variables = new List<string>();
        var refs = CodeRefs(variables).Concat(HelperCalls()).ToList();

        foreach (var v in variables.Distinct())
        {
            if (!KnownKeyVariables.Contains(v))
            {
                problems.Add($"{v}: a resource key in a variable that ResourceTypeTests does not follow (add the helper's callers to HelperCalls)");
            }
        }

        foreach (var r in refs)
        {
            if (!app.TryGetValue(r.Key, out var have))
            {
                problems.Add($"{r.File}:{r.Line} {r.Via}: resource '{r.Key}' does not exist");
            }
            else if (have != r.Kind)
            {
                problems.Add($"{r.File}:{r.Line} {r.Via}: '{r.Key}' is a {have}, but a {r.Kind} is needed");
            }
        }

        Assert.True(refs.Count > 120, "too few code references found: " + refs.Count);
        Assert.Contains(refs, r => r.Via.StartsWith("Ui.Text", StringComparison.Ordinal));
        Assert.Contains(refs, r => r.Via == "Circle fill");
        Assert.Contains(refs, r => r.Via == "BatteryRing.Res");
        Assert.Contains(refs, r => r.Via == "FindResource" && r.Key == "ShadowColor" && r.Kind == "Color");
        Assert.True(problems.Count == 0, string.Join("\n", problems.Distinct().Take(40)));
    }
}
