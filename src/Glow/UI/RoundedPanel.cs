using System.Drawing;
using System.Drawing.Drawing2D;

namespace Glow.UI;

// A panel with smooth, anti-aliased rounded corners. It stays opaque and paints
// its corners with the parent colour (BackColor), so the rounded FillColor edge
// blends cleanly — unlike a Region clip, which gives jagged pixel edges.
public sealed class RoundedPanel : Panel
{
    public Color FillColor { get; set; } = Color.Black;
    public int CornerRadius { get; set; } = 10;

    public RoundedPanel()
    {
        DoubleBuffered = true;
        ResizeRedraw = true;
    }

    protected override void OnPaint(PaintEventArgs e)
    {
        var g = e.Graphics;
        g.SmoothingMode = SmoothingMode.AntiAlias;
        g.PixelOffsetMode = PixelOffsetMode.Half;

        var rect = new Rectangle(0, 0, Width - 1, Height - 1);
        using (var path = Build(rect, CornerRadius))
        using (var brush = new SolidBrush(FillColor))
        {
            g.FillPath(brush, path);
        }

        if (!string.IsNullOrEmpty(Text))
        {
            TextRenderer.DrawText(g, Text, Font, new Rectangle(0, 0, Width, Height), ForeColor,
                TextFormatFlags.HorizontalCenter | TextFormatFlags.VerticalCenter | TextFormatFlags.NoPrefix);
        }
    }

    private static GraphicsPath Build(Rectangle r, int radius)
    {
        int d = Math.Max(1, Math.Min(radius * 2, Math.Min(r.Width, r.Height)));
        var p = new GraphicsPath();
        p.AddArc(r.X, r.Y, d, d, 180, 90);
        p.AddArc(r.Right - d, r.Y, d, d, 270, 90);
        p.AddArc(r.Right - d, r.Bottom - d, d, d, 0, 90);
        p.AddArc(r.X, r.Bottom - d, d, d, 90, 90);
        p.CloseFigure();
        return p;
    }
}
