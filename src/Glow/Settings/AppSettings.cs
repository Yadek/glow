using Microsoft.Win32;

namespace Glow.Settings;

// Per-user app settings under HKCU\Software\Glow.
//
//   Software\Glow                       global options
//   Software\Glow\Displays\<key>        per-display night mode
//
// The installer removes the whole tree on uninstall, so nothing is left behind.
public static class AppSettings
{
    private const string KeyPath = @"Software\Glow";
    private const string DisplaysPath = KeyPath + @"\Displays";

    public const int DefaultNightIntensity = 50;

    /// <summary>Sentinel for a master slider the user has never touched.</summary>
    public const int UnsetMaster = -1;

    public static bool AnimateTrayIcon
    {
        get => GetInt(KeyPath, "AnimateTrayIcon") != 0;
        set => SetInt(KeyPath, "AnimateTrayIcon", value ? 1 : 0);
    }

    /// <summary>Whether the popup shows the per-monitor cards or just the master card.</summary>
    public static bool PopupExpanded
    {
        get => GetInt(KeyPath, "PopupExpanded") != 0;
        set => SetInt(KeyPath, "PopupExpanded", value ? 1 : 0);
    }

    // The master sliders keep their own values rather than tracking the average of
    // the displays: the point of the master is to set everything at once, and a
    // control that drifts every time one display is adjusted is confusing to aim.

    public static int MasterBrightness
    {
        get => Clamp(GetInt(KeyPath, "MasterBrightness", UnsetMaster));
        set => SetInt(KeyPath, "MasterBrightness", Math.Clamp(value, 0, 100));
    }

    public static int MasterNightIntensity
    {
        get => Clamp(GetInt(KeyPath, "MasterNightIntensity", UnsetMaster));
        set => SetInt(KeyPath, "MasterNightIntensity", Math.Clamp(value, 0, 100));
    }

    // Keeps the "never set" sentinel intact while clamping any real value.
    private static int Clamp(int stored)
        => stored == UnsetMaster ? UnsetMaster : Math.Clamp(stored, 0, 100);

    // ----- per-display night mode -----

    public static bool GetNightEnabled(string displayKey)
        => GetInt(DisplayPath(displayKey), "NightEnabled") != 0;

    public static void SetNightEnabled(string displayKey, bool enabled)
        => SetInt(DisplayPath(displayKey), "NightEnabled", enabled ? 1 : 0);

    public static int GetNightIntensity(string displayKey)
        => Math.Clamp(GetInt(DisplayPath(displayKey), "NightIntensity", DefaultNightIntensity), 0, 100);

    public static void SetNightIntensity(string displayKey, int percent)
        => SetInt(DisplayPath(displayKey), "NightIntensity", Math.Clamp(percent, 0, 100));

    // ----- migration from the single global night setting (<= 1.1.2) -----

    /// <summary>
    /// Up to 1.1.2 night mode was one global on/off + intensity. Copy it onto every
    /// display the first time the new build runs, so upgrading users keep their
    /// setting instead of silently losing it. Runs once.
    /// </summary>
    public static void MigrateLegacyNightSettings(IEnumerable<string> displayKeys)
    {
        if (GetInt(KeyPath, "DisplaysMigrated") != 0)
        {
            return;
        }

        bool legacyEnabled = GetInt(KeyPath, "NightEnabled") != 0;
        int legacyIntensity = Math.Clamp(GetInt(KeyPath, "NightIntensity", DefaultNightIntensity), 0, 100);

        foreach (string key in displayKeys)
        {
            SetNightEnabled(key, legacyEnabled);
            SetNightIntensity(key, legacyIntensity);
        }

        SetInt(KeyPath, "DisplaysMigrated", 1);
    }

    private static string DisplayPath(string displayKey) => $@"{DisplaysPath}\{displayKey}";

    private static int GetInt(string path, string name, int defaultValue = 0)
    {
        try
        {
            using var key = Registry.CurrentUser.OpenSubKey(path);
            return key?.GetValue(name) is int v ? v : defaultValue;
        }
        catch
        {
            return defaultValue;
        }
    }

    private static void SetInt(string path, string name, int value)
    {
        try
        {
            using var key = Registry.CurrentUser.CreateSubKey(path);
            key?.SetValue(name, value, RegistryValueKind.DWord);
        }
        catch
        {
            // Read-only registry (locked-down machine) — settings just won't persist.
        }
    }
}
