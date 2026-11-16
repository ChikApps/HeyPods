// Copyright (C) 2026 ChikApps
// SPDX-License-Identifier: GPL-3.0-only
//
// A faithful port (validation only) of WPF's own path mini-language parser, so a bad path string in a XAML file
// fails in the Linux test run instead of on a user's Windows PC at start-up.
// Source: dotnet/wpf, src/Microsoft.DotNet.Wpf/src/PresentationCore/System/Windows/Media/ParsersCommon.cs
// (class AbbreviatedGeometryParser, MIT). Keep the behaviour identical, including the strictness:
//  * PathGeometry.Figures="..."  -> PathFigureCollection.Parse -> AbbreviatedGeometryParser from index 0.
//    It does NOT know the fill-rule prefix: "F1 M ..." throws FormatException (that was the 0.9.0 start-up crash,
//    "Provide value on 'System.Windows.Baml2006.TypeConverterMarkupExtension' threw an exception").
//  * Geometry.Parse / Path.Data="..." / <Geometry>..</Geometry> -> an optional "F0"/"F1" first (capital F only).

using System.Globalization;

namespace Earbuds.Presentation.Tests;

internal static class WpfPathParser
{
    /// <summary>PathFigureCollection.Parse: no fill-rule prefix. Returns null when WPF would accept it.</summary>
    public static string? ValidateFigures(string path) => Run(path, 0);

    /// <summary>Geometry.Parse: optional leading "F0" or "F1" (capital F), then the same grammar.</summary>
    public static string? ValidateGeometry(string path)
    {
        var i = 0;
        while (i < path.Length && char.IsWhiteSpace(path, i))
        {
            i++;
        }

        if (i < path.Length && path[i] == 'F')
        {
            i++;
            while (i < path.Length && char.IsWhiteSpace(path, i))
            {
                i++;
            }

            if (i == path.Length || (path[i] != '0' && path[i] != '1'))
            {
                return "fill rule F must be followed by 0 or 1";
            }

            i++;
        }

        return Run(path, i);
    }

    private static string? Run(string path, int start)
    {
        try
        {
            new Parser(path, start).Parse();
            return null;
        }
        catch (FormatException e)
        {
            return e.Message;
        }
    }

    private sealed class Parser
    {
        private readonly string _s;
        private int _i;
        private char _token;

        public Parser(string s, int start)
        {
            _s = s;
            _i = start;
        }

        private bool More() => _i < _s.Length;

        private void Bad() => throw new FormatException($"unexpected token near index {_i - 1}: '{Around()}'");

        private string Around() => _s.Substring(Math.Max(0, _i - 1), Math.Min(14, _s.Length - Math.Max(0, _i - 1)));

        private bool SkipWhiteSpace(bool allowComma)
        {
            var commaMet = false;
            while (More())
            {
                var ch = _s[_i];
                switch (ch)
                {
                    case ' ':
                    case '\n':
                    case '\r':
                    case '\t':
                        break;
                    case ',':
                        if (allowComma)
                        {
                            commaMet = true;
                            allowComma = false;
                        }
                        else
                        {
                            Bad();
                        }

                        break;
                    default:
                        if ((ch > ' ' && ch <= 'z') || !char.IsWhiteSpace(ch))
                        {
                            return commaMet;
                        }

                        break;
                }

                _i++;
            }

            return commaMet;
        }

        private bool ReadToken()
        {
            SkipWhiteSpace(false);
            if (More())
            {
                _token = _s[_i++];
                return true;
            }

            return false;
        }

        private bool IsNumber(bool allowComma)
        {
            var commaMet = SkipWhiteSpace(allowComma);
            if (More())
            {
                _token = _s[_i];
                if (_token is '.' or '-' or '+' or (>= '0' and <= '9') or 'I' or 'N')
                {
                    return true;
                }
            }

            if (commaMet)
            {
                Bad();
            }

            return false;
        }

        private void SkipDigits(bool signAllowed)
        {
            if (signAllowed && More() && (_s[_i] == '-' || _s[_i] == '+'))
            {
                _i++;
            }

            while (More() && _s[_i] >= '0' && _s[_i] <= '9')
            {
                _i++;
            }
        }

        private double ReadNumber(bool allowComma)
        {
            if (!IsNumber(allowComma))
            {
                Bad();
            }

            var simple = true;
            var start = _i;
            if (More() && (_s[_i] == '-' || _s[_i] == '+'))
            {
                _i++;
            }

            if (More() && _s[_i] == 'I')
            {
                _i = Math.Min(_i + 8, _s.Length);
                simple = false;
            }
            else if (More() && _s[_i] == 'N')
            {
                _i = Math.Min(_i + 3, _s.Length);
                simple = false;
            }
            else
            {
                SkipDigits(false);
                if (More() && _s[_i] == '.')
                {
                    simple = false;
                    _i++;
                    SkipDigits(false);
                }

                if (More() && (_s[_i] == 'E' || _s[_i] == 'e'))
                {
                    simple = false;
                    _i++;
                    SkipDigits(true);
                }
            }

            if (simple && _i <= start + 8)
            {
                var sign = 1;
                if (_s[start] == '+')
                {
                    start++;
                }
                else if (_s[start] == '-')
                {
                    start++;
                    sign = -1;
                }

                var value = 0;
                while (start < _i)
                {
                    value = (value * 10) + (_s[start] - '0');
                    start++;
                }

                return value * sign;
            }

            try
            {
                return double.Parse(_s.AsSpan(start, _i - start), provider: CultureInfo.InvariantCulture);
            }
            catch (FormatException)
            {
                throw new FormatException($"bad number at index {start}: '{_s.Substring(start, _i - start)}'");
            }
        }

        private void ReadBool()
        {
            SkipWhiteSpace(true);
            if (More())
            {
                _token = _s[_i++];
                if (_token is '0' or '1')
                {
                    return;
                }
            }

            Bad();
        }

        private void ReadPoint(bool allowComma)
        {
            ReadNumber(allowComma);
            ReadNumber(true);
        }

        public void Parse()
        {
            var first = true;
            while (ReadToken())
            {
                var cmd = _token;
                if (first)
                {
                    if (cmd != 'M' && cmd != 'm')
                    {
                        Bad();
                    }

                    first = false;
                }

                switch (cmd)
                {
                    case 'm':
                    case 'M':
                        ReadPoint(false);
                        while (IsNumber(true))
                        {
                            ReadPoint(false);
                        }

                        break;
                    case 'l':
                    case 'L':
                    case 'h':
                    case 'H':
                    case 'v':
                    case 'V':
                        do
                        {
                            if (cmd is 'h' or 'H' or 'v' or 'V')
                            {
                                ReadNumber(false);
                            }
                            else
                            {
                                ReadPoint(false);
                            }
                        }
                        while (IsNumber(true));
                        break;
                    case 'c':
                    case 'C':
                    case 's':
                    case 'S':
                        do
                        {
                            if (cmd is 's' or 'S')
                            {
                                ReadPoint(false);
                            }
                            else
                            {
                                ReadPoint(false);
                                ReadPoint(true);
                            }

                            ReadPoint(true);
                        }
                        while (IsNumber(true));
                        break;
                    case 'q':
                    case 'Q':
                    case 't':
                    case 'T':
                        do
                        {
                            if (cmd is 't' or 'T')
                            {
                                ReadPoint(false);
                            }
                            else
                            {
                                ReadPoint(false);
                                ReadPoint(true);
                            }
                        }
                        while (IsNumber(true));
                        break;
                    case 'a':
                    case 'A':
                        do
                        {
                            ReadNumber(false);
                            ReadNumber(true);
                            ReadNumber(true);
                            ReadBool();
                            ReadBool();
                            ReadPoint(true);
                        }
                        while (IsNumber(true));
                        break;
                    case 'z':
                    case 'Z':
                        break;
                    default:
                        Bad();
                        break;
                }
            }
        }
    }
}
