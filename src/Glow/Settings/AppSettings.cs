using Microsoft.Win32;

namespace Glow.Settings;

// Per-user app settings stored under HKCU\Software\Glow.
// (Removed on uninstall by the installer, so nothing is left behind.)
public static class AppSettings
{
    private const string KeyPath = @"Software\Glow";

    public static bool AnimateTrayIcon
    {
        get
        {
            using var key = Registry.CurrentUser.OpenSubKey(KeyPath);
            return key?.GetValue("AnimateTrayIcon") is int v && v != 0;
        }
        set
        {
            using var key = Registry.CurrentUser.CreateSubKey(KeyPath);
            key?.SetValue("AnimateTrayIcon", value ? 1 : 0, RegistryValueKind.DWord);
        }
    }
}
