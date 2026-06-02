using Microsoft.Win32;

namespace Glow.Settings;

// Per-user app settings stored under HKCU\Software\Glow.
// (Removed on uninstall by the installer, so nothing is left behind.)
public static class AppSettings
{
    private const string KeyPath = @"Software\Glow";

    public static bool AnimateTrayIcon
    {
        get => GetBool("AnimateTrayIcon");
        set => SetInt("AnimateTrayIcon", value ? 1 : 0);
    }

    public static bool NightEnabled
    {
        get => GetBool("NightEnabled");
        set => SetInt("NightEnabled", value ? 1 : 0);
    }

    public static int NightIntensity
    {
        get => GetInt("NightIntensity", defaultValue: 50); // 50% on a fresh install
        set => SetInt("NightIntensity", value);
    }

    private static bool GetBool(string name) => GetInt(name) != 0;

    private static int GetInt(string name, int defaultValue = 0)
    {
        using var key = Registry.CurrentUser.OpenSubKey(KeyPath);
        return key?.GetValue(name) is int v ? v : defaultValue;
    }

    private static void SetInt(string name, int value)
    {
        using var key = Registry.CurrentUser.CreateSubKey(KeyPath);
        key?.SetValue(name, value, RegistryValueKind.DWord);
    }
}
