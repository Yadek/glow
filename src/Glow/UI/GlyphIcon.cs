using System.Drawing;
using System.Drawing.Drawing2D;

namespace Glow.UI;

public enum GlyphKind
{
    Sun,
    Moon,
    ChevronDown,
    ChevronUp,
}

// The little sun / moon marker in front of each slider row. Drawn with GDI+
// rather than an icon font, because Segoe Fluent Icons only ships with Windows 11
// and a missing glyph font renders as tofu.
public sealed class GlyphIcon : Control
{
    public GlyphKind Kind { get; set; } = GlyphKind.Sun;

    public GlyphIcon()
    {
        SetStyle(ControlStyles.AllPaintingInWmPaint
               | ControlStyles.OptimizedDoubleBuffer
               | ControlStyles.ResizeRedraw
               | ControlStyles.UserPaint, true);
        TabStop = false;
        Enabled = false; // purely decorative; never takes the mouse
    }

    protected override void OnPaint(PaintEventArgs e)
    {
        var g = e.Graphics;
        g.Clear(BackColor);
        g.SmoothingMode = SmoothingMode.AntiAlias;
        g.PixelOffsetMode = PixelOffsetMode.Half;

        float size = Math.Min(Width, Height);
        float cx = Width / 2f;
        float cy = Height / 2f;

        using var brush = new SolidBrush(ForeColor);
        switch (Kind)
        {
            case GlyphKind.Sun:
                DrawSun(g, brush, cx, cy, size);
                break;
            case GlyphKind.Moon:
                DrawMoon(g, brush, cx, cy, size);
                break;
            default:
                DrawChevron(g, brush, cx, cy, size, down: Kind == GlyphKind.ChevronDown);
                break;
        }
    }

    private static void DrawChevron(Graphics g, Brush brush, float cx, float cy, float size, bool down)
    {
        float halfW = size * 0.28f;
        float halfH = size * 0.15f;
        float thickness = Math.Max(1.2f, size * 0.11f);

        using var pen = new Pen(brush, thickness)
        {
            StartCap = LineCap.Round,
            EndCap = LineCap.Round,
            LineJoin = LineJoin.Round,
        };

        float tip = down ? halfH : -halfH;
        g.DrawLines(pen, new[]
        {
            new PointF(cx - halfW, cy - tip),
            new PointF(cx,         cy + tip),
            new PointF(cx + halfW, cy - tip),
        });
    }

    private static void DrawSun(Graphics g, Brush brush, float cx, float cy, float size)
    {
        float core = size * 0.30f;
        g.FillEllipse(brush, cx - core, cy - core, core * 2, core * 2);

        float rayInner = size * 0.40f;
        float rayOuter = size * 0.50f;
        float thickness = Math.Max(1f, size * 0.09f);

        using var pen = new Pen(brush, thickness) { StartCap = LineCap.Round, EndCap = LineCap.Round };
        for (int i = 0; i < 8; i++)
        {
            double angle = i * Math.PI / 4.0;
            float dx = (float)Math.Cos(angle);
            float dy = (float)Math.Sin(angle);
            g.DrawLine(pen,
                cx + dx * rayInner, cy + dy * rayInner,
                cx + dx * rayOuter, cy + dy * rayOuter);
        }
    }

    // A crescent: a full disc with a second disc punched out of it. Painting the
    // bite in the background colour on top keeps both edges anti-aliased, which a
    // Region-based subtraction would not.
    private void DrawMoon(Graphics g, Brush brush, float cx, float cy, float size)
    {
        float r = size * 0.44f;
        g.FillEllipse(brush, cx - r, cy - r, r * 2, r * 2);

        float biteR = r * 0.86f;
        float biteCx = cx + r * 0.42f;
        float biteCy = cy - r * 0.34f;
        using var bite = new SolidBrush(BackColor);
        g.FillEllipse(bite, biteCx - biteR, biteCy - biteR, biteR * 2, biteR * 2);
    }
}
