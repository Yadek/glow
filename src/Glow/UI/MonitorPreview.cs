using System.Drawing.Drawing2D;
using Glow.Localization;
using Glow.Wallpapers;

namespace Glow.UI;

internal sealed class MonitorPreview : Control
{
    private WallpaperPreview? _preview;

    internal MonitorPreview()
    {
        DoubleBuffered = true;
        ResizeRedraw = true;
        TabStop = false;
        AccessibleRole = AccessibleRole.Graphic;
        AccessibleName = Strings.WallpaperUnavailable;
    }

    internal void SetPreview(WallpaperPreview? preview)
    {
        var old = _preview;
        _preview = preview;
        old?.Dispose();
        AccessibleName = preview?.Source == WallpaperSource.WallpaperEngine ? Strings.EnginePreview
            : preview is null ? Strings.WallpaperUnavailable : Strings.DesktopWallpaper;
        Invalidate();
    }

    protected override void OnPaint(PaintEventArgs e)
    {
        if (Width < 2 || Height < 2) return;
        var g = e.Graphics;
        g.SmoothingMode = SmoothingMode.AntiAlias;
        var screen = new Rectangle(1, 1, Width - 3, Height - 7);
        using var path = RoundedPanel.Build(screen, Math.Max(4, Height / 10));
        var state = g.Save();
        g.SetClip(path);
        if (_preview is not null)
        {
            g.InterpolationMode = InterpolationMode.HighQualityBicubic;
            // Letterbox already-composed wallpaper rather than stretching it.
            float scale = Math.Min(screen.Width / (float)_preview.Image.Width, screen.Height / (float)_preview.Image.Height);
            float w = _preview.Image.Width * scale, h = _preview.Image.Height * scale;
            g.Clear(Color.Black);
            g.DrawImage(_preview.Image, screen.Left + (screen.Width - w) / 2, screen.Top + (screen.Height - h) / 2, w, h);
        }
        else
        {
            using var gradient = new LinearGradientBrush(screen, Theme.CardBg, Theme.Track, 25);
            g.FillRectangle(gradient, screen);
            TextRenderer.DrawText(g, Strings.WallpaperUnavailable, Font, screen, Theme.Subtle,
                TextFormatFlags.HorizontalCenter | TextFormatFlags.VerticalCenter | TextFormatFlags.EndEllipsis);
        }
        if (_preview?.Source == WallpaperSource.WallpaperEngine)
        {
            var label = new Rectangle(4, screen.Bottom - Font.Height - 4, screen.Width - 6, Font.Height + 2);
            using var shade = new SolidBrush(Color.FromArgb(175, 0, 0, 0));
            g.FillRectangle(shade, label);
            TextRenderer.DrawText(g, Strings.EnginePreview, Font, label, Color.White,
                TextFormatFlags.HorizontalCenter | TextFormatFlags.EndEllipsis);
        }
        g.Restore(state);
        using var border = new Pen(Theme.Edge);
        g.DrawPath(border, path);
        using var stand = new Pen(Theme.Subtle, Math.Max(1, Height / 40f));
        g.DrawLine(stand, Width / 2, screen.Bottom + 1, Width / 2, Height - 2);
        g.DrawLine(stand, Width / 2 - 10, Height - 2, Width / 2 + 10, Height - 2);
    }

    protected override void Dispose(bool disposing)
    {
        if (disposing) { _preview?.Dispose(); _preview = null; }
        base.Dispose(disposing);
    }
}
