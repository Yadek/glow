using System.Drawing;
using System.Drawing.Drawing2D;
using System.Drawing.Text;
using System.Runtime.InteropServices;

namespace Glow.UI;

// Draws the tray icon: white "glow" on a rounded square. The background is either
// solid black (Create) or a slowly shifting gradient (CreateGradient) for the
// optional animated icon.
public static class GlowIcon
{
    private static readonly Color StaticBg = Color.FromArgb(18, 18, 20);

    public static Icon Create(int size = 32)
        => Build(size, rect => new SolidBrush(StaticBg));

    // phase advances over time (radians-ish); hues rotate to create a smooth shimmer.
    public static Icon CreateGradient(int size, double phase)
    {
        return Build(size, rect =>
        {
            double hue = (phase * 40.0) % 360.0;
            Color c1 = FromHsl(hue, 0.65, 0.50);
            Color c2 = FromHsl((hue + 60.0) % 360.0, 0.65, 0.58);
            float angle = (float)((phase * 30.0) % 360.0);
            return new LinearGradientBrush(rect, c1, c2, angle);
        });
    }

    private static Icon Build(int size, Func<RectangleF, Brush> backgroundBrush)
    {
        using var bmp = new Bitmap(size, size);
        using (var g = Graphics.FromImage(bmp))
        {
            g.SmoothingMode = SmoothingMode.AntiAlias;
            g.TextRenderingHint = TextRenderingHint.AntiAliasGridFit;
            g.Clear(Color.Transparent);

            var rect = new RectangleF(0, 0, size - 1, size - 1);
            using (var path = RoundedRect(rect, size * 0.22f))
            using (var brush = backgroundBrush(rect))
            {
                g.FillPath(brush, path);
            }

            DrawGlowText(g, size);
        }

        return ToIcon(bmp);
    }

    private static void DrawGlowText(Graphics g, int size)
    {
        string text = size >= 24 ? "glow" : "g";
        using var fg = new SolidBrush(Color.White);
        using var fmt = new StringFormat(StringFormat.GenericTypographic)
        {
            Alignment = StringAlignment.Center,
            LineAlignment = StringAlignment.Center,
            FormatFlags = StringFormatFlags.NoWrap,
        };

        float target = size * 0.82f;
        float fontSize = size * (size >= 24 ? 0.42f : 0.6f);
        var font = new Font("Segoe UI", fontSize, FontStyle.Bold, GraphicsUnit.Pixel);
        float measured = g.MeasureString(text, font, PointF.Empty, fmt).Width;
        if (measured > target)
        {
            fontSize *= target / measured;
            font.Dispose();
            font = new Font("Segoe UI", fontSize, FontStyle.Bold, GraphicsUnit.Pixel);
        }
        g.DrawString(text, font, fg, new RectangleF(0, 0, size, size), fmt);
        font.Dispose();
    }

    private static GraphicsPath RoundedRect(RectangleF r, float radius)
    {
        float d = radius * 2;
        var path = new GraphicsPath();
        path.AddArc(r.X, r.Y, d, d, 180, 90);
        path.AddArc(r.Right - d, r.Y, d, d, 270, 90);
        path.AddArc(r.Right - d, r.Bottom - d, d, d, 0, 90);
        path.AddArc(r.X, r.Bottom - d, d, d, 90, 90);
        path.CloseFigure();
        return path;
    }

    private static Icon ToIcon(Bitmap bmp)
    {
        IntPtr hIcon = bmp.GetHicon();
        try
        {
            using var temp = Icon.FromHandle(hIcon);
            return (Icon)temp.Clone();
        }
        finally
        {
            DestroyIcon(hIcon);
        }
    }

    // HSL (h in degrees, s/l in 0..1) to RGB.
    private static Color FromHsl(double h, double s, double l)
    {
        h = (h % 360.0) / 360.0;
        double r, g, b;
        if (s == 0)
        {
            r = g = b = l;
        }
        else
        {
            double q = l < 0.5 ? l * (1 + s) : l + s - l * s;
            double p = 2 * l - q;
            r = HueToRgb(p, q, h + 1.0 / 3.0);
            g = HueToRgb(p, q, h);
            b = HueToRgb(p, q, h - 1.0 / 3.0);
        }
        return Color.FromArgb(
            (int)Math.Round(r * 255),
            (int)Math.Round(g * 255),
            (int)Math.Round(b * 255));
    }

    private static double HueToRgb(double p, double q, double t)
    {
        if (t < 0) t += 1;
        if (t > 1) t -= 1;
        if (t < 1.0 / 6.0) return p + (q - p) * 6 * t;
        if (t < 1.0 / 2.0) return q;
        if (t < 2.0 / 3.0) return p + (q - p) * (2.0 / 3.0 - t) * 6;
        return p;
    }

    [DllImport("user32.dll", EntryPoint = "DestroyIcon")]
    private static extern bool DestroyIcon(IntPtr handle);
}
