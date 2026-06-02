using System.Drawing;
using System.Drawing.Drawing2D;

namespace Glow.UI;

// Owner-drawn 0–100 slider used instead of the native TrackBar (which ignores
// the dark theme). Draws relative to its own size, so any DPI works.
public sealed class BrightnessSlider : Control
{
    private int _value;
    private bool _dragging;

    public event EventHandler? ValueChanged;

    private static readonly Color TrackColor = Color.FromArgb(72, 72, 80);
    private readonly Color _fillColor = Theme.AccentColor();   // matches the Windows accent
    private static readonly Color ThumbColor = Color.White;
    private static readonly Color ThumbBorder = Color.FromArgb(45, 0, 0, 0);

    public BrightnessSlider()
    {
        SetStyle(ControlStyles.AllPaintingInWmPaint
               | ControlStyles.OptimizedDoubleBuffer
               | ControlStyles.ResizeRedraw
               | ControlStyles.UserPaint
               | ControlStyles.SupportsTransparentBackColor, true);
        BackColor = Color.Transparent;
        TabStop = false;
    }

    public int Value
    {
        get => _value;
        set
        {
            int v = Math.Clamp(value, 0, 100);
            if (v == _value) return;
            _value = v;
            Invalidate();
            ValueChanged?.Invoke(this, EventArgs.Empty);
        }
    }

    private int ThumbDiameter => Math.Min(Height, Width);
    private int Usable => Math.Max(1, Width - ThumbDiameter);

    private int ValueToX(int value) => (ThumbDiameter / 2) + (int)Math.Round(Usable * (value / 100.0));

    private int XToValue(int x)
    {
        int radius = ThumbDiameter / 2;
        double ratio = (x - radius) / (double)Usable;
        return Math.Clamp((int)Math.Round(ratio * 100), 0, 100);
    }

    protected override void OnPaint(PaintEventArgs e)
    {
        var g = e.Graphics;
        g.SmoothingMode = SmoothingMode.AntiAlias;
        g.PixelOffsetMode = PixelOffsetMode.Half;

        int d = ThumbDiameter;
        int trackThickness = Math.Max(4, Height / 5);
        int trackY = (Height - trackThickness) / 2;
        int trackLeft = d / 2;
        int trackRight = Width - d / 2;
        int thumbX = ValueToX(_value);

        // Background track (full width, rounded caps)
        using (var bg = new SolidBrush(TrackColor))
        {
            FillCapsule(g, bg, trackLeft - trackThickness / 2, trackY,
                trackRight + trackThickness / 2 - (trackLeft - trackThickness / 2), trackThickness);
        }

        // Filled portion up to the thumb
        using (var fill = new SolidBrush(_fillColor))
        {
            int fillRight = thumbX;
            FillCapsule(g, fill, trackLeft - trackThickness / 2, trackY,
                fillRight - (trackLeft - trackThickness / 2), trackThickness);
        }

        // Thumb (a touch smaller than the full height for a softer look)
        int thumbSize = (int)Math.Round(d * 0.82);
        var thumbRect = new Rectangle(thumbX - thumbSize / 2, (Height - thumbSize) / 2, thumbSize, thumbSize);
        using (var shadow = new SolidBrush(ThumbBorder))
        {
            g.FillEllipse(shadow, thumbRect.X, thumbRect.Y + 1, thumbRect.Width, thumbRect.Height);
        }
        using (var thumb = new SolidBrush(ThumbColor))
        {
            g.FillEllipse(thumb, thumbRect);
        }
    }

    private static void FillCapsule(Graphics g, Brush brush, int x, int y, int width, int height)
    {
        if (width <= 0) return;
        if (width <= height)
        {
            g.FillEllipse(brush, x, y, width, height);
            return;
        }
        using var path = new GraphicsPath();
        path.AddArc(x, y, height, height, 90, 180);
        path.AddArc(x + width - height, y, height, height, 270, 180);
        path.CloseFigure();
        g.FillPath(brush, path);
    }

    protected override void OnMouseDown(MouseEventArgs e)
    {
        base.OnMouseDown(e);
        if (e.Button == MouseButtons.Left)
        {
            _dragging = true;
            Capture = true;
            Value = XToValue(e.X);
        }
    }

    protected override void OnMouseMove(MouseEventArgs e)
    {
        base.OnMouseMove(e);
        if (_dragging)
        {
            Value = XToValue(e.X);
        }
    }

    protected override void OnMouseUp(MouseEventArgs e)
    {
        base.OnMouseUp(e);
        _dragging = false;
        Capture = false;
    }

    protected override void OnMouseWheel(MouseEventArgs e)
    {
        base.OnMouseWheel(e);
        Value += Math.Sign(e.Delta) * 5;
    }
}
