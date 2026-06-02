using System.Drawing;
using Microsoft.Win32;

namespace Glow.UI;

// Current Windows accent color, read from HKCU\...\DWM\AccentColor (0xAABBGGRR).
// Falls back to the classic Windows blue.
public static class Theme
{
    private static readonly Color Fallback = Color.FromArgb(0, 120, 215);

    public static Color AccentColor()
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
        return Fallback;
    }
}
