using System.Runtime.InteropServices;
using Glow.Native;

namespace Glow.Wallpapers;

internal sealed class DesktopWallpaperProvider
{
    // Called on the preview worker's STA. No COM object leaves that thread.
    internal WallpaperCandidate? GetCandidate(WallpaperDisplay display)
    {
        IDesktopWallpaper? desktop = null;
        try
        {
            var type = Type.GetTypeFromCLSID(new Guid("C2CF3110-460E-4FC1-B9D0-8A1C0C9CC4BD"), true)!;
            desktop = (IDesktopWallpaper)Activator.CreateInstance(type)!;
            var monitors = new List<WallpaperDisplay>();
            for (uint i = 0, count = Math.Min(desktop.GetMonitorDevicePathCount(), 128); i < count; i++)
            {
                string path = desktop.GetMonitorDevicePathAt(i);
                desktop.GetMonitorRECT(path, out var rect);
                var bounds = Rectangle.FromLTRB(rect.Left, rect.Top, rect.Right, rect.Bottom);
                if (bounds.Width > 0 && bounds.Height > 0) monitors.Add(new(path, path, bounds));
            }
            var match = MatchMonitor(display, monitors);
            if (match?.InterfacePath is null) return null;
            string file = desktop.GetWallpaper(match.InterfacePath);
            if (string.IsNullOrWhiteSpace(file)) return null; // solid colour or no wallpaper
            var position = desktop.GetPosition();
            var union = monitors.Select(m => m.Bounds).Aggregate(Rectangle.Union);
            uint color = desktop.GetBackgroundColor();
            return new(file, WallpaperSource.Windows, position, union, match.Bounds,
                Color.FromArgb((int)(color & 255), (int)((color >> 8) & 255), (int)((color >> 16) & 255)));
        }
        catch (Exception e) when (e is COMException or UnauthorizedAccessException or ArgumentException or InvalidCastException)
        {
            return null;
        }
        finally
        {
            if (desktop is not null) Marshal.FinalReleaseComObject(desktop);
        }
    }

    internal static WallpaperDisplay? MatchMonitor(WallpaperDisplay display, IReadOnlyList<WallpaperDisplay> candidates)
    {
        var identity = candidates.Where(c => !string.IsNullOrEmpty(display.InterfacePath)
            && string.Equals(display.InterfacePath, c.InterfacePath, StringComparison.OrdinalIgnoreCase)).ToArray();
        if (identity.Length > 0) return identity.Length == 1 ? identity[0] : null;
        var geometry = candidates.Where(c => c.Bounds == display.Bounds).ToArray();
        return geometry.Length == 1 ? geometry[0] : null;
    }

    internal static Rectangle SpanCrop(Rectangle monitor, Rectangle desktop, Size image)
    {
        if (desktop.Width <= 0 || desktop.Height <= 0) return Rectangle.Empty;
        double sx = image.Width / (double)desktop.Width, sy = image.Height / (double)desktop.Height;
        return Rectangle.FromLTRB((int)Math.Round((monitor.Left - desktop.Left) * sx),
            (int)Math.Round((monitor.Top - desktop.Top) * sy),
            (int)Math.Round((monitor.Right - desktop.Left) * sx),
            (int)Math.Round((monitor.Bottom - desktop.Top) * sy));
    }
}
