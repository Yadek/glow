using System.Drawing;
using Microsoft.Win32;

namespace Glow.UI;

// Colours for the popup. Follows the Windows app theme (light/dark) and the
// user's accent colour, both re-read on demand because either can change while
// the app is running.
public static class Theme
{
    private static readonly Color AccentFallback = Color.FromArgb(0, 120, 215);

    private static bool _isDark = ReadIsDark();
    private static Color _accent = ReadAccent();

    public static bool IsDark => _isDark;
    public static Color Accent => HighContrast ? SystemColors.Highlight : _accent;
    public static bool HighContrast => SystemInformation.HighContrast;
    public static Color Edge => HighContrast ? SystemColors.WindowText
        : _isDark ? Color.FromArgb(77, 79, 91) : Color.FromArgb(218, 221, 232);

    internal static Color PillForeground(bool active, bool highContrast, Color selectionText, Color controlText)
        => highContrast ? (active ? selectionText : controlText) : active ? Color.White : Subtle;

    /// <summary>Re-reads the theme and accent colour (call on WM_SETTINGCHANGE / UserPreferenceChanged).</summary>
    public static void Refresh()
    {
        _isDark = ReadIsDark();
        _accent = ReadAccent();
    }

    public static Color FormBg => HighContrast ? SystemColors.Window : _isDark ? Color.FromArgb(24, 27, 35) : Color.FromArgb(235, 239, 248);
    public static Color CardBg => HighContrast ? SystemColors.Window : _isDark ? Color.FromArgb(39, 43, 54) : Color.FromArgb(250, 252, 255);
    public static Color Text => HighContrast ? SystemColors.WindowText : _isDark ? Color.FromArgb(238, 241, 249) : Color.FromArgb(26, 29, 40);
    public static Color Subtle => HighContrast ? SystemColors.WindowText : _isDark ? Color.FromArgb(165, 174, 193) : Color.FromArgb(89, 101, 126);
    public static Color Track => HighContrast ? SystemColors.GrayText : _isDark ? Color.FromArgb(68, 75, 93) : Color.FromArgb(213, 220, 235);
    public static Color PillOff => HighContrast ? SystemColors.Control : _isDark ? Color.FromArgb(72, 72, 80) : Color.FromArgb(214, 214, 222);
    public static Color Thumb => HighContrast ? SystemColors.WindowText : Color.White;
    public static Color ThumbShadow => Color.FromArgb(_isDark ? 45 : 70, 0, 0, 0);

    /// <summary>Border drawn around the popup, as 0x00BBGGRR for DWMWA_BORDER_COLOR.</summary>
    public static int BorderColorRef => _isDark ? 0x00524F4A : 0x00D2D2D2;

    // HKCU\...\Themes\Personalize\AppsUseLightTheme: 0 = dark apps, 1 = light.
    private static bool ReadIsDark()
    {
        try
        {
            using var key = Registry.CurrentUser.OpenSubKey(
                @"Software\Microsoft\Windows\CurrentVersion\Themes\Personalize");
            return key?.GetValue("AppsUseLightTheme") is int v && v == 0;
        }
        catch
        {
            return true; // dark is the safer default for a tray flyout
        }
    }

    // HKCU\...\DWM\AccentColor is 0xAABBGGRR.
    private static Color ReadAccent()
    {
        try
        {
            using var key = Registry.CurrentUser.OpenSubKey(@"Software\Microsoft\Windows\DWM");
            if (key?.GetValue("AccentColor") is int abgr)
            {
                byte r = (byte)(abgr & 0xFF);
                byte g = (byte)((abgr >> 8) & 0xFF);
                byte b = (byte)((abgr >> 16) & 0xFF);
                return Color.FromArgb(r, g, b);
            }
        }
        catch
        {
            // ignore and fall back
        }
        return AccentFallback;
    }
}
