using Glow.Localization;
using Glow.Settings;

namespace Glow.UI;

// The header and tray share one menu and the existing actions. Appearance
// options update the current popup immediately and persist with other settings.
internal static class PopupSettingsMenu
{
    internal static void AddTo(ContextMenuStrip menu, Action refresh)
    {
        var glass = new ToolStripMenuItem(Strings.Glass) { CheckOnClick = true };
        var previews = new ToolStripMenuItem(Strings.WallpaperPreviews) { CheckOnClick = true };
        glass.Click += (_, _) => { AppSettings.GlassEnabled = glass.Checked; refresh(); };
        previews.Click += (_, _) => { AppSettings.WallpaperPreviewsEnabled = previews.Checked; refresh(); };
        menu.Opening += (_, _) =>
        {
            glass.Checked = AppSettings.GlassEnabled;
            previews.Checked = AppSettings.WallpaperPreviewsEnabled;
        };
        menu.Items.Add(glass);
        menu.Items.Add(previews);
        menu.Items.Add(new ToolStripSeparator());
    }
}
