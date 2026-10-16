using Glow.Monitors;
using Glow.UI;
using Glow.Wallpapers;

internal static class SmokeChecks
{
    internal static void Run()
    {
        var displays = DisplayCatalog.Enumerate(); // No DDC/CI or gamma writes.
        using var service = new WallpaperPreviewService();
        Console.WriteLine($"Windows read-only smoke: {displays.Count} logical display(s).");
        foreach (var display in displays)
        {
            using var result = service.LoadAsync(new WallpaperDisplay(display.Key, display.InterfacePath,
                display.Bounds, displays.Count == 1), default).GetAwaiter().GetResult();
            Console.WriteLine($"Wallpaper: {(result is null ? "fallback placeholder" : result.Source.ToString())}; identity: {display.InterfacePath is not null}");
        }
        using var form = new System.Windows.Forms.Form();
        Console.WriteLine($"DWM request accepted: {WindowBackdrop.Apply(form, true)} (visual blur is a separate check).");
    }
}
