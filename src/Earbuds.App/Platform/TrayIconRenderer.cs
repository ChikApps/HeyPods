// Copyright (C) 2026 ChikApps
// SPDX-License-Identifier: GPL-3.0-only
//
// Tray icon, drawn as vectors at the exact pixel size Windows asks for (design: art.js tray(), svg/tray-*.svg):
// two small pods filled to the lower battery in the accent color; gray = no data; red dot = low; slash = Bluetooth off.
// Colors follow the TASKBAR theme (it can be dark while apps are light).

using System.IO;
using System.Windows;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using Earbuds.Presentation;

namespace Earbuds.App.Platform;

internal static class TrayIconRenderer
{
    private const string Pod = "M40 94A7 7 0 0 1 33 87L33 49C26 50 18 47 13 41A8 8 0 0 1 12 27C14 13 22 6 31 6C44 6 54 15 54 27C54 36 51 42 47 46L47 87A7 7 0 0 1 40 94Z";
    private static readonly Geometry PodGeometry = Freeze(Geometry.Parse("F1 " + Pod));

    public static DrawingGroup Draw(TrayLook look, int fillPercent, bool taskbarDark, Color accent)
    {
        var d = taskbarDark;
        var grey = look is TrayLook.Off or TrayLook.BluetoothOff;
        var baseColor = Hex(d ? "#F2F2F2" : "#1B1B1F");
        var on = grey ? Hex(d ? "#7A7D85" : "#9A9DA4") : accent;
        var track = Hex(d ? "#3A3D45" : "#D5D9E0");
        var fillTop = grey ? 0 : 100 - Math.Clamp(fillPercent, 0, 100);
        var bg = Hex(d ? "#1C1E24" : "#EEF0F4");

        var root = new DrawingGroup();
        root.Children.Add(new GeometryDrawing(Brushes.Transparent, null, new RectangleGeometry(new Rect(0, 0, 20, 20))));
        root.Children.Add(PodDrawing(10.4, false, grey, track, on, fillTop, grey ? on : baseColor));
        root.Children.Add(PodDrawing(0.4, true, grey, track, on, fillTop, grey ? on : baseColor));
        if (look == TrayLook.Low)
        {
            root.Children.Add(new GeometryDrawing(new SolidColorBrush(Hex("#E81123")), new Pen(new SolidColorBrush(bg), 1.2), new EllipseGeometry(new Point(16, 16), 3.6, 3.6)));
        }

        if (look == TrayLook.BluetoothOff)
        {
            var line = new LineGeometry(new Point(2, 18.5), new Point(18.5, 2));
            root.Children.Add(new GeometryDrawing(null, new Pen(new SolidColorBrush(bg), 3.2) { StartLineCap = PenLineCap.Round, EndLineCap = PenLineCap.Round }, line));
            root.Children.Add(new GeometryDrawing(null, new Pen(new SolidColorBrush(Hex(d ? "#C8CBD1" : "#5D6068")), 1.3) { StartLineCap = PenLineCap.Round, EndLineCap = PenLineCap.Round }, line));
        }

        root.ClipGeometry = new RectangleGeometry(new Rect(0, 0, 20, 20));
        root.Freeze();
        return root;
    }

    private static Drawing PodDrawing(double x, bool mirror, bool grey, Color track, Color on, double fillTop, Color stroke)
    {
        var g = new DrawingGroup();
        var t = new TransformGroup();
        if (mirror)
        {
            t.Children.Add(new MatrixTransform(-1, 0, 0, 1, 60, 0));
        }

        t.Children.Add(new ScaleTransform(0.16, 0.16));
        t.Children.Add(new TranslateTransform(x, 2));
        g.Transform = t;
        var inner = new DrawingGroup { ClipGeometry = PodGeometry };
        if (!grey)
        {
            inner.Children.Add(new GeometryDrawing(new SolidColorBrush(track), null, new RectangleGeometry(new Rect(0, 0, 60, 100))));
        }

        inner.Children.Add(new GeometryDrawing(new SolidColorBrush(on), null, new RectangleGeometry(new Rect(0, fillTop, 60, 100))));
        g.Children.Add(inner);
        g.Children.Add(new GeometryDrawing(null, new Pen(new SolidColorBrush(stroke), grey ? 7 : 5), PodGeometry));
        return g;
    }

    /// <summary>Renders to a 32-bit icon (classic DIB format, works on every Windows 10/11).</summary>
    public static System.Drawing.Icon ToIcon(Drawing drawing, int size)
    {
        var visual = new DrawingVisual();
        using (var dc = visual.RenderOpen())
        {
            dc.PushTransform(new ScaleTransform(size / 20.0, size / 20.0));
            dc.DrawDrawing(drawing);
        }

        var bmp = new RenderTargetBitmap(size, size, 96, 96, PixelFormats.Pbgra32);
        bmp.Render(visual);
        var stride = size * 4;
        var pixels = new byte[stride * size];
        bmp.CopyPixels(pixels, stride, 0);

        using var ms = new MemoryStream();
        using (var w = new BinaryWriter(ms, System.Text.Encoding.ASCII, leaveOpen: true))
        {
            var maskStride = ((size + 31) / 32) * 4;
            var imageBytes = 40 + pixels.Length + maskStride * size;
            // ICONDIR + one ICONDIRENTRY
            w.Write((short)0); w.Write((short)1); w.Write((short)1);
            w.Write((byte)(size >= 256 ? 0 : size)); w.Write((byte)(size >= 256 ? 0 : size)); w.Write((byte)0); w.Write((byte)0);
            w.Write((short)1); w.Write((short)32); w.Write(imageBytes); w.Write(22);
            // BITMAPINFOHEADER (height doubled: color + mask)
            w.Write(40); w.Write(size); w.Write(size * 2); w.Write((short)1); w.Write((short)32); w.Write(0);
            w.Write(pixels.Length + maskStride * size); w.Write(0); w.Write(0); w.Write(0); w.Write(0);
            for (var y = size - 1; y >= 0; y--) // bottom-up, straight (not premultiplied) alpha
            {
                for (var x = 0; x < size; x++)
                {
                    var i = y * stride + x * 4;
                    var a = pixels[i + 3];
                    byte Un(byte c) => a == 0 ? (byte)0 : (byte)Math.Min(255, c * 255 / a);
                    w.Write(Un(pixels[i])); w.Write(Un(pixels[i + 1])); w.Write(Un(pixels[i + 2])); w.Write(a);
                }
            }

            w.Write(new byte[maskStride * size]); // AND mask: all 0, alpha decides
        }

        ms.Position = 0;
        return new System.Drawing.Icon(ms, size, size);
    }

    private static Color Hex(string hex) => (Color)ColorConverter.ConvertFromString(hex);

    private static T Freeze<T>(T f)
        where T : Freezable
    {
        f.Freeze();
        return f;
    }
}
