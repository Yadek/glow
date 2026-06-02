using System.Drawing;
using System.Drawing.Drawing2D;
using System.Drawing.Text;

namespace Glow.UI;

// Draws the tray icon at runtime: white "glow" on a black rounded square.
public static class GlowIcon
{
    public static Icon Create(int size = 32)
    {
        using var bmp = new Bitmap(size, size);
        using (var g = Graphics.FromImage(bmp))
        {
            g.SmoothingMode = SmoothingMode.AntiAlias;
            g.TextRenderingHint = TextRenderingHint.AntiAliasGridFit;
            g.Clear(Color.Transparent);

            float radius = size * 0.22f;
            using (var path = RoundedRect(new RectangleF(0, 0, size - 1, size - 1), radius))
            using (var bg = new SolidBrush(Color.FromArgb(18, 18, 20)))
            {
                g.FillPath(bg, path);
            }

            // "glow" scaled to fit; for tiny tray sizes a single "g" reads better.
            string text = size >= 24 ? "glow" : "g";
            using var fg = new SolidBrush(Color.White);
            using var fmt = new StringFormat(StringFormat.GenericTypographic)
            {
                Alignment = StringAlignment.Center,
                LineAlignment = StringAlignment.Center,
                FormatFlags = StringFormatFlags.NoWrap,
            };

            // Shrink the font until "glow" fits on one line in the square.
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

        IntPtr hIcon = bmp.GetHicon();
        try
        {
            // Clone so the Icon owns managed memory and the GDI handle can be freed.
            using var temp = Icon.FromHandle(hIcon);
            return (Icon)temp.Clone();
        }
        finally
        {
            NativeDestroyIcon(hIcon);
        }
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

    [System.Runtime.InteropServices.DllImport("user32.dll", EntryPoint = "DestroyIcon")]
    private static extern bool NativeDestroyIcon(IntPtr handle);
}
