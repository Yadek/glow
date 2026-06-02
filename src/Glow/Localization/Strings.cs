using System.Runtime.InteropServices;

namespace Glow.Localization;

// Small code-based localization (no resx). Russian when the OS UI language is
// Russian, English otherwise.
public static class Strings
{
    private const int LANG_RUSSIAN = 0x19;

    [DllImport("kernel32.dll")]
    private static extern ushort GetUserDefaultUILanguage();

    private static readonly bool IsRussian =
        (GetUserDefaultUILanguage() & 0x3FF) == LANG_RUSSIAN;

    private static string Pick(string en, string ru) => IsRussian ? ru : en;

    public static string TrayTooltip => Pick("Glow — monitor brightness", "Glow — яркость мониторов");

    public static string Title => Pick("Brightness", "Яркость");

    public static string NoMonitors => Pick(
        "No DDC/CI-capable monitors found",
        "Мониторы с поддержкой DDC/CI не найдены");

    public static string RunAtStartup => Pick("Run at startup", "Запускать при старте Windows");

    public static string Exit => Pick("Exit", "Выход");

    public static string CheckUpdates => Pick("Check for updates", "Проверить обновления");

    public static string AnimateIcon => Pick("Animate tray icon", "Анимировать иконку в трее");

    public static string NightLight => Pick("Night light", "Ночной свет");

    public static string On => Pick("On", "Вкл");

    public static string Off => Pick("Off", "Выкл");

    public static string UpdateAvailable(string version) => Pick(
        $"Glow {version} is available. Update now?",
        $"Доступна новая версия Glow {version}. Обновить сейчас?");

    public static string UpToDate => Pick(
        "You have the latest version.",
        "Установлена последняя версия.");

    public static string UpdateDownloadFailed => Pick(
        "Could not download the update. Please try again later.",
        "Не удалось скачать обновление. Попробуйте позже.");

    // Fallback label when a monitor has no name.
    public static string Display(int index) => Pick($"Display {index}", $"Монитор {index}");
}
