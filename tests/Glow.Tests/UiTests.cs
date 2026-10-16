using System.Drawing;
using System.Windows.Forms;
using Glow.UI;
using Glow.Wallpapers;

internal static class UiTests
{
    internal static void Run()
    {
        Program.Check(!WindowBackdrop.ShouldUseGlass(true, true, false, 19045), "Backdrop_Windows10Fallback");
        Program.Check(!WindowBackdrop.ShouldUseGlass(true, true, true, 26100), "Backdrop_HighContrastFallback");
        Program.Check(!WindowBackdrop.ShouldUseGlass(true, false, false, 26100), "Backdrop_RespectsTransparency");
        Program.Check(!WindowBackdrop.ShouldUseGlass(false, true, false, 26100), "Backdrop_RespectsAppSetting");
        Program.Check(WindowBackdrop.ShouldUseGlass(true, true, false, 22621), "Backdrop_SupportedWindows");
        Program.Check(Theme.PillForeground(true, true, Color.Black, Color.Blue) == Color.Black,
            "Pill_UsesHighContrastSelectionText");
        Program.Check(Theme.PillForeground(false, true, Color.Black, Color.Blue) == Color.Blue,
            "Pill_UsesHighContrastControlText");
        using var slider = new BrightnessSlider { Size = new(200, 24) };
        Program.Check(slider.TabStop, "Slider_KeyboardFocusable");
        Program.Check(BrightnessSlider.ValueAfterKey(99, Keys.Right) == 100, "Slider_KeyboardClamps");
        Program.Check(BrightnessSlider.ValueAfterKey(42, Keys.Home) == 0, "Slider_Home");
        Program.Check(BrightnessSlider.ValueAfterKey(42, Keys.End) == 100, "Slider_End");
        Program.Check(BrightnessSlider.ValueAfterKey(42, Keys.PageDown) == 32, "Slider_PageDown");
        using var control = new MonitorPreview { Size = new(160, 90) };
        var old = new WallpaperPreview(new Bitmap(8, 8), WallpaperSource.Windows);
        control.SetPreview(old);
        control.SetPreview(null);
        bool disposed = false;
        try { _ = old.Image.GetPixel(0, 0); } catch (ArgumentException) { disposed = true; }
        Program.Check(disposed, "Preview_ControlDisposesOldImage");
    }
}
