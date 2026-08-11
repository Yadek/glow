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
    public static Color Accent => _accent;

    /// <summary>Re-reads the theme and accent colour (call on WM_SETTINGCHANGE / UserPreferenceChanged).</summary>
    public static void Refresh()
    {
        _isDark = ReadIsDark();
        _accent = ReadAccent();
    }

    public static Color FormBg => _isDark ? Color.FromArgb(32, 32, 36) : Color.FromArgb(243, 243, 246);
    public static Color CardBg => _isDark ? Color.FromArgb(43, 43, 49) : Color.FromArgb(255, 255, 255);
    public static Color Text => _isDark ? Color.FromArgb(228, 228, 232) : Color.FromArgb(26, 26, 30);
    public static Color Subtle => _isDark ? Color.FromArgb(138, 138, 148) : Color.FromArgb(104, 104, 114);
    public static Color Track => _isDark ? Color.FromArgb(72, 72, 80) : Color.FromArgb(206, 206, 214);
    public static Color PillOff => _isDark ? Color.FromArgb(72, 72, 80) : Color.FromArgb(214, 214, 222);
    public static Color Thumb => Color.White;
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
