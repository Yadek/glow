using System.Drawing.Drawing2D;

namespace Glow.Wallpapers;

internal sealed class WallpaperPreviewService : IDisposable
{
    private readonly Func<WallpaperDisplay, WallpaperCandidate?> _windows;
    private readonly Func<WallpaperDisplay, CancellationToken, Task<WallpaperCandidate?>> _engine;
    private readonly SemaphoreSlim _gate = new(1);
    private readonly Dictionary<string, Bitmap> _cache = new();
    private volatile bool _disposed;

    internal WallpaperPreviewService() : this(new DesktopWallpaperProvider().GetCandidate,
        new WallpaperEngineProvider().GetCandidateAsync) { }

    internal WallpaperPreviewService(Func<WallpaperDisplay, WallpaperCandidate?> windows,
        Func<WallpaperDisplay, CancellationToken, Task<WallpaperCandidate?>> engine)
    {
        _windows = windows;
        _engine = engine;
    }

    internal async Task<WallpaperPreview?> LoadAsync(WallpaperDisplay display, CancellationToken token)
    {
        await _gate.WaitAsync(token).ConfigureAwait(false);
        try
        {
            if (_disposed) return null;
            token.ThrowIfCancellationRequested();
            var completion = new TaskCompletionSource<WallpaperPreview?>(TaskCreationOptions.RunContinuationsAsynchronously);
            // Only one worker can run. A blocked shell COM call cannot accumulate
            // more worker threads on repeated opens; cancelled queued requests exit.
            var worker = new Thread(() =>
            {
                WallpaperPreview? result = null;
                try
                {
                    token.ThrowIfCancellationRequested();
                    var candidate = _engine(display, token).GetAwaiter().GetResult();
                    result = candidate is null ? null : Decode(candidate, display);
                    if (result is null)
                    {
                        token.ThrowIfCancellationRequested();
                        candidate = _windows(display);
                        result = candidate is null ? null : Decode(candidate, display);
                    }
                    if (token.IsCancellationRequested || _disposed)
                    {
                        result?.Dispose();
                        result = null;
                        token.ThrowIfCancellationRequested();
                    }
                    completion.SetResult(result);
                }
                catch (OperationCanceledException) { result?.Dispose(); completion.SetCanceled(token); }
                catch (Exception e) { result?.Dispose(); completion.SetException(e); }
                finally
                {
                    if (_disposed) ClearCache();
                }
            }) { IsBackground = true, Name = "Glow wallpaper preview" };
            worker.SetApartmentState(ApartmentState.STA);
            worker.Start();
            return await completion.Task.ConfigureAwait(false);
        }
        finally { _gate.Release(); }
    }

    private WallpaperPreview? Decode(WallpaperCandidate candidate, WallpaperDisplay display)
    {
        try
        {
            if (candidate.Path.StartsWith(@"\\")) return null;
            var info = new FileInfo(candidate.Path);
            if (!info.Exists || info.Length > 32 * 1024 * 1024) return null;
            string key = $"{info.FullName}|{info.LastWriteTimeUtc.Ticks}|{info.Length}|{candidate}|{display.Bounds.Size}";
            lock (_cache)
            {
                if (_cache.TryGetValue(key, out var cached)) return new((Bitmap)cached.Clone(), candidate.Source);
            }
            using var stream = new FileStream(candidate.Path, FileMode.Open, FileAccess.Read, FileShare.ReadWrite | FileShare.Delete);
            using var image = Image.FromStream(stream, false, false);
            if ((long)image.Width * image.Height > 40_000_000) return null;
            double ratio = display.Bounds.Width > 0 && display.Bounds.Height > 0
                ? display.Bounds.Width / (double)display.Bounds.Height : 16.0 / 9;
            int width = Math.Max(1, (int)Math.Round(Math.Min(640, 360 * ratio)));
            int height = Math.Max(1, (int)Math.Round(width / ratio));
            var thumbnail = new Bitmap(width, height);
            try
            {
                using var g = Graphics.FromImage(thumbnail);
                g.Clear(candidate.Background ?? Color.Black);
                g.InterpolationMode = InterpolationMode.HighQualityBicubic;
                Rectangle monitor = candidate.MonitorBounds ?? display.Bounds;
                if (monitor.Width <= 0 || monitor.Height <= 0) monitor = new(0, 0, image.Width, image.Height);
                Rectangle area = candidate.Position is WallpaperPosition.Span or WallpaperPosition.Tile
                    && candidate.DesktopBounds is { } desktop ? desktop : monitor;
                g.ScaleTransform(width / (float)monitor.Width, height / (float)monitor.Height);
                g.TranslateTransform(area.Left - monitor.Left, area.Top - monitor.Top);
                if (candidate.Position == WallpaperPosition.Tile)
                {
                    using var tile = new TextureBrush(image, WrapMode.Tile);
                    g.FillRectangle(tile, 0, 0, area.Width, area.Height);
                }
                else
                {
                    float sx = area.Width / (float)image.Width, sy = area.Height / (float)image.Height;
                    float factor = candidate.Position switch
                    {
                        WallpaperPosition.Center => 1,
                        WallpaperPosition.Fit => Math.Min(sx, sy),
                        _ => Math.Max(sx, sy),
                    };
                    float w = candidate.Position == WallpaperPosition.Stretch ? area.Width : image.Width * factor;
                    float h = candidate.Position == WallpaperPosition.Stretch ? area.Height : image.Height * factor;
                    g.DrawImage(image, (area.Width - w) / 2, (area.Height - h) / 2, w, h);
                }
                lock (_cache)
                {
                    if (_cache.Count >= 16)
                    {
                        string oldest = _cache.Keys.First();
                        _cache[oldest].Dispose();
                        _cache.Remove(oldest);
                    }
                    _cache.Add(key, (Bitmap)thumbnail.Clone());
                }
                return new(thumbnail, candidate.Source);
            }
            catch { thumbnail.Dispose(); throw; }
        }
        catch (Exception e) when (e is IOException or UnauthorizedAccessException or ArgumentException or OutOfMemoryException or System.Runtime.InteropServices.ExternalException)
        { return null; }
    }

    private void ClearCache()
    {
        lock (_cache)
        {
            foreach (var bitmap in _cache.Values) bitmap.Dispose();
            _cache.Clear();
        }
    }

    public void Dispose() { _disposed = true; ClearCache(); }
}
