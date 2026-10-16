using System.Drawing;
using Glow.Wallpapers;

internal static class WallpaperTests
{
    internal static void Run()
    {
        var a = new WallpaperDisplay("A", "device-a", new Rectangle(-1920, 0, 1920, 1080));
        var b = new WallpaperDisplay("B", "device-b", new Rectangle(0, 0, 1920, 1080));
        Program.Check(DesktopWallpaperProvider.MatchMonitor(a, new[] { b, a }) == a,
            "MatchMonitor_IgnoresEnumerationOrder");
        Program.Check(DesktopWallpaperProvider.MatchMonitor(a with { InterfacePath = "DEVICE-A" }, new[] { b, a }) == a,
            "MatchMonitor_IgnoresPathCase");
        Program.Check(DesktopWallpaperProvider.MatchMonitor(a with { InterfacePath = null }, new[] { a, a with { Key = "C" } }) is null,
            "MatchMonitor_RejectsDuplicateGeometry");
        Program.Check(DesktopWallpaperProvider.MatchMonitor(a, new[] { a, a }) is null,
            "MatchMonitor_RejectsDuplicateIdentity");
        Program.Check(DesktopWallpaperProvider.MatchMonitor(a, new[] { b }) is null,
            "MatchMonitor_MissingReturnsNull");
        Program.Check(DesktopWallpaperProvider.SpanCrop(a.Bounds, new Rectangle(-1920, 0, 3840, 1080), new Size(3840, 1080))
            == new Rectangle(0, 0, 1920, 1080), "SpanCrop_NegativeCoordinates");
        Program.Check(DesktopWallpaperProvider.SpanCrop(b.Bounds, new Rectangle(-1920, 0, 3840, 1080), new Size(1920, 540))
            == new Rectangle(960, 0, 960, 540), "SpanCrop_ScalesRightMonitor");
    }
}
