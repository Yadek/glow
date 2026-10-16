namespace Glow.Wallpapers;

internal sealed record WallpaperDisplay(string Key, string? InterfacePath, Rectangle Bounds, bool IsOnlyDisplay = false);
internal enum WallpaperSource { Windows, WallpaperEngine }
internal enum WallpaperPosition { Center, Tile, Stretch, Fit, Fill, Span }
internal sealed record WallpaperCandidate(string Path, WallpaperSource Source,
    WallpaperPosition Position = WallpaperPosition.Fill, Rectangle? DesktopBounds = null,
    Rectangle? MonitorBounds = null, Color? Background = null);

// Each result owns its bitmap. UI controls take ownership; the cache keeps copies.
internal sealed class WallpaperPreview(Bitmap image, WallpaperSource source) : IDisposable
{
    public Bitmap Image { get; } = image;
    public WallpaperSource Source { get; } = source;
    public void Dispose() => Image.Dispose();
}
