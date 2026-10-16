using System.Drawing;
using Glow.Wallpapers;

internal static class PreviewTests
{
    internal static void Run()
    {
        string folder = Path.Combine(Path.Combine(Environment.CurrentDirectory, "artifacts"), "glow-preview-" + Guid.NewGuid());
        Directory.CreateDirectory(folder);
        try
        {
            string file = Path.Combine(folder, "wall.png");
            using (var image = new Bitmap(1280, 720))
            {
                using var graphics = Graphics.FromImage(image);
                graphics.Clear(Color.Coral);
                image.Save(file);
            }
            var display = new WallpaperDisplay("A", "path-a", new(0, 0, 1920, 1080));
            using var service = new WallpaperPreviewService(
                _ => new WallpaperCandidate(file, WallpaperSource.Windows),
                (_, _) => Task.FromResult<WallpaperCandidate?>(new(Path.Combine(folder, "broken.png"), WallpaperSource.WallpaperEngine)));
            File.WriteAllText(Path.Combine(folder, "broken.png"), "corrupt");
            using var preview = service.LoadAsync(display, default).GetAwaiter().GetResult();
            Program.Check(preview is not null && preview.Source == WallpaperSource.Windows, "Preview_UsesWindowsWhenEngineImageFails");
            Program.Check(preview!.Image.Size == new Size(640, 360), "Preview_ResizesLargeImage");
            Program.Check(preview.Image.GetPixel(100, 100).R > 240, "Preview_DecodesRealPixels");
            File.Move(file, file + ".moved");
            File.Move(file + ".moved", file);
            Program.Check(true, "Preview_DoesNotLockFile");
            using var second = service.LoadAsync(display, default).GetAwaiter().GetResult();
            preview.Dispose();
            Program.Check(second!.Image.GetPixel(100, 100).R > 240, "Preview_CacheReturnsIndependentOwnedBitmap");
            using var cancel = new CancellationTokenSource();
            cancel.Cancel();
            try { service.LoadAsync(display, cancel.Token).GetAwaiter().GetResult(); throw new Exception("Cancellation ignored"); }
            catch (OperationCanceledException) { Program.Check(true, "Preview_CancelledRequestRejected"); }
            using var broken = new WallpaperPreviewService(_ => new(file + ".missing", WallpaperSource.Windows),
                (_, _) => Task.FromResult<WallpaperCandidate?>(null));
            Program.Check(broken.LoadAsync(display, default).GetAwaiter().GetResult() is null, "Preview_MissingImageReturnsNull");
            string tiled = Path.Combine(folder, "tile.png");
            using (var image = new Bitmap(3840, 1080))
            {
                using var g = Graphics.FromImage(image);
                g.Clear(Color.Red);
                g.FillRectangle(Brushes.Blue, 1920, 0, 1920, 1080);
                image.Save(tiled);
            }
            var right = new WallpaperDisplay("right", "right", new(0, 0, 1920, 1080));
            using var tileService = new WallpaperPreviewService(_ => new(tiled, WallpaperSource.Windows,
                WallpaperPosition.Tile, new Rectangle(-1920, 0, 3840, 1080), right.Bounds),
                (_, _) => Task.FromResult<WallpaperCandidate?>(null));
            using var tile = tileService.LoadAsync(right, default).GetAwaiter().GetResult();
            Program.Check(tile!.Image.GetPixel(320, 180).B > 240, "Tile_RightMonitorUsesDesktopOrigin");
            using var spanService = new WallpaperPreviewService(_ => new(tiled, WallpaperSource.Windows,
                WallpaperPosition.Span, new Rectangle(-1920, 0, 3840, 1080), right.Bounds),
                (_, _) => Task.FromResult<WallpaperCandidate?>(null));
            using var span = spanService.LoadAsync(right, default).GetAwaiter().GetResult();
            Program.Check(span!.Image.GetPixel(320, 180).B > 240, "Span_RightMonitorUsesCorrectPixels");
        }
        finally { Directory.Delete(folder, true); }
    }
}
