// Copyright (C) 2026 ChikApps
// SPDX-License-Identifier: GPL-3.0-only
//
// Battery ring (design v3, מפרט-לדני.md §4א): starts at 12 o'clock and fills clockwise.
// Same formula as the design ringPath() in the original design source (ui.js).

namespace Earbuds.Presentation;

/// <summary>Sizes from the spec table (diameter D, stroke S, number font size).</summary>
public readonly record struct RingSize(double Diameter, double Stroke, double FontSize)
{
    /// <summary>Popup and wizard step 2.</summary>
    public static readonly RingSize Popup = new(60, 4, 15);

    /// <summary>Main window.</summary>
    public static readonly RingSize Main = new(56, 4, 14);

    /// <summary>100% has three digits: the number is 20% smaller so "⚡100%" stays inside the ring.</summary>
    public double FontSizeFor(int? level) => level >= 100 ? FontSize * 0.8 : FontSize;

    /// <summary>Charging bolt width (9, or 8 at 100%); height = width × 1.4.</summary>
    public double BoltWidthFor(int? level) => level >= 100 ? 8 : 9;
}

/// <summary>What to draw for one ring. Coordinates in the ring's own box (0..D), y down.</summary>
public readonly record struct RingArc(
    bool Empty, bool Full, double CenterX, double CenterY, double Radius,
    double StartX, double StartY, double EndX, double EndY, bool IsLargeArc);

public static class RingGeometry
{
    /// <summary>
    /// Arc for <paramref name="percent"/> (0..100; null = unknown). Empty for null or 0 (no dot left behind);
    /// Full for 100 (an arc cannot draw 360°, so the caller draws a circle).
    /// </summary>
    public static RingArc Compute(double? percent, double diameter, double stroke)
    {
        var c = diameter / 2;
        var r = (diameter - stroke) / 2;
        if (percent is not { } p || p <= 0 || double.IsNaN(p))
        {
            return new RingArc(true, false, c, c, r, c, c - r, c, c - r, false);
        }

        if (p >= 100)
        {
            return new RingArc(false, true, c, c, r, c, c - r, c, c - r, true);
        }

        var a = p / 100.0 * 2 * Math.PI;
        return new RingArc(false, false, c, c, r, c, c - r, c + r * Math.Sin(a), c - r * Math.Cos(a), p > 50);
    }

    /// <summary>
    /// Ease of the fill animation: cubic-bezier(.2,.7,.2,1) over 0.9 s (spec §6). Returns progress 0..1 for time 0..1.
    /// </summary>
    public static double Ease(double t)
    {
        if (t <= 0)
        {
            return 0;
        }

        if (t >= 1)
        {
            return 1;
        }

        // Solve x(u) = t for the bezier parameter u (Newton, then bisection fallback), return y(u).
        const double x1 = 0.2, y1 = 0.7, x2 = 0.2, y2 = 1.0;
        static double Bez(double u, double p1, double p2) => 3 * (1 - u) * (1 - u) * u * p1 + 3 * (1 - u) * u * u * p2 + u * u * u;
        static double Der(double u, double p1, double p2) => 3 * (1 - u) * (1 - u) * p1 + 6 * (1 - u) * u * (p2 - p1) + 3 * u * u * (1 - p2);
        var u = t;
        for (var i = 0; i < 8; i++)
        {
            var dx = Bez(u, x1, x2) - t;
            var d = Der(u, x1, x2);
            if (Math.Abs(dx) < 1e-6 || Math.Abs(d) < 1e-6)
            {
                break;
            }

            u -= dx / d;
        }

        if (u is < 0 or > 1 || Math.Abs(Bez(u, x1, x2) - t) > 1e-4)
        {
            double lo = 0, hi = 1;
            for (var i = 0; i < 40; i++)
            {
                u = (lo + hi) / 2;
                if (Bez(u, x1, x2) < t)
                {
                    lo = u;
                }
                else
                {
                    hi = u;
                }
            }
        }

        return Bez(u, y1, y2);
    }
}
