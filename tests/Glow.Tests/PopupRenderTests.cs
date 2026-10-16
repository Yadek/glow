using System.Drawing;
using System.Drawing.Drawing2D;
using System.Reflection;
using System.Windows.Forms;
using Glow.Monitors;
using Glow.UI;
using Glow.Wallpapers;
using Glow.NightShift;
using Glow.Localization;

// Renders the real popup with fixture displays, without enumerating DDC/CI,
// applying gamma, or starting TrayContext. Reflection stays in test tooling.
internal static class PopupRenderTests
{
    internal static void Run(string folder)
    {
        Directory.CreateDirectory(folder);
        foreach (float scale in new[] { 1f, 1.5f, 2f })
        {
            using var displays = new DisplayManager();
            var list = (List<DisplayTarget>)displays.Displays;
            list.Add(new(new DisplayInfo("fixture-a", "DELL U2723QE", @"\\.\DISPLAY1", IntPtr.Zero,
                "fixture-a", new Rectangle(-1920, 0, 1920, 1080)), null));
            list.Add(new(new DisplayInfo("fixture-b", "LG ULTRAGEAR 27GP850", @"\\.\DISPLAY2", IntPtr.Zero,
                "fixture-b", new Rectangle(0, 0, 2560, 1440)), null));
            using var popup = new BrightnessPopup(displays);
            Set(popup, "_scale", scale);
            Set(popup, "_screen", Screen.PrimaryScreen!);
            Set(popup, "_expanded", true);
            int height = (int)typeof(BrightnessPopup).GetMethod("BuildContent", BindingFlags.NonPublic | BindingFlags.Instance)!.Invoke(popup, null)!;
            popup.ClientSize = new Size((int)(320 * scale), height);
            popup.Location = new Point(-32000, -32000);
            popup.TopMost = false;
            popup.Show(); // WinForms skips invisible descendants in DrawToBitmap.
            CheckNightRefresh(popup, list);
            foreach (Control card in popup.Controls)
            {
                foreach (var preview in card.Controls.OfType<MonitorPreview>())
                {
                    var image = new Bitmap(640, 360);
                    using (var g = Graphics.FromImage(image))
                    using (var gradient = new LinearGradientBrush(new Rectangle(0, 0, 640, 360), Color.DarkSlateBlue, Color.Coral, 30))
                        g.FillRectangle(gradient, 0, 0, 640, 360);
                    preview.SetPreview(new WallpaperPreview(image, WallpaperSource.Windows));
                }
                if (card is RoundedPanel)
                {
                    Program.Check(card.Right <= popup.ClientSize.Width, $"Popup_CardFitsAt{scale}");
                    foreach (Control child in card.Controls)
                        Program.Check(child.Right <= card.Width && child.Bottom <= card.Height,
                            $"Popup_ChildFitsAt{scale}_{child.GetType().Name}");
                }
            }
            using var bitmap = new Bitmap(popup.Width, popup.Height);
            popup.DrawToBitmap(bitmap, popup.ClientRectangle);
            Program.Check(bitmap.GetPixel((int)(40 * scale), (int)(70 * scale)).ToArgb()
                != popup.BackColor.ToArgb(), "Popup_RenderContainsCardPixels");
            bitmap.Save(Path.Combine(folder, $"popup-{scale * 100:0}.png"));
        }
    }

    private static void Set(object target, string field, object value)
        => target.GetType().GetField(field, BindingFlags.Instance | BindingFlags.NonPublic)!.SetValue(target, value);

    private static void CheckNightRefresh(BrightnessPopup popup, List<DisplayTarget> displays)
    {
        var field = typeof(NightMode).GetField("_displays", BindingFlags.Static | BindingFlags.NonPublic)!;
        var original = field.GetValue(null);
        var states = (Dictionary<string, NightState>)typeof(NightMode).GetField("States", BindingFlags.Static | BindingFlags.NonPublic)!.GetValue(null)!;
        try
        {
            field.SetValue(null, displays.Select(d => d.Info).ToList());
            foreach (var display in displays) states[display.Key] = new NightState(true, 50);
            popup.RefreshNightVisuals();
            foreach (Control card in popup.Controls)
            {
                foreach (RoundedPanel pill in card.Controls.OfType<RoundedPanel>().Where(p => p.AccessibleRole == AccessibleRole.CheckButton))
                    Program.Check(pill.Text == Strings.On, "MenuAction_RefreshesVisibleNightPill");
                foreach (BrightnessSlider slider in card.Controls.OfType<BrightnessSlider>())
                    Program.Check(!slider.Muted, "MenuAction_RefreshesVisibleNightSlider");
            }
        }
        finally
        {
            field.SetValue(null, original);
            foreach (var display in displays) states.Remove(display.Key);
            // No native gamma/registry writes: only fixture state in this process.
        }
    }
}
