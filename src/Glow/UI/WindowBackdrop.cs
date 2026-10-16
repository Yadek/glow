using System.Runtime.InteropServices;
using Microsoft.Win32;

namespace Glow.UI;

internal static class WindowBackdrop
{
    internal static bool ShouldUseGlass(bool requested, bool transparencyEnabled, bool highContrast, int windowsBuild)
        => requested && transparencyEnabled && !highContrast && windowsBuild >= 22621;

    internal static bool Apply(Form form, bool requested)
    {
        bool enabled = ShouldUseGlass(requested, TransparencyEnabled(), SystemInformation.HighContrast,
            Environment.OSVersion.Version.Build);
        try
        {
            // DWMSBT_TRANSIENTWINDOW is the documented Desktop Acrylic material.
            int backdrop = enabled ? 3 : 1;
            int result = DwmSetWindowAttribute(form.Handle, 38, ref backdrop, sizeof(int));
            var margins = new Margins { Left = enabled && result == 0 ? -1 : 0 };
            int frame = DwmExtendFrameIntoClientArea(form.Handle, ref margins);
            return enabled && result == 0 && frame == 0;
        }
        catch (Exception e) when (e is DllNotFoundException or EntryPointNotFoundException)
        { return false; }
    }

    private static bool TransparencyEnabled()
    {
        try
        {
            using var key = Registry.CurrentUser.OpenSubKey(@"Software\Microsoft\Windows\CurrentVersion\Themes\Personalize");
            return key?.GetValue("EnableTransparency") is not int value || value != 0;
        }
        catch { return false; }
    }

    [StructLayout(LayoutKind.Sequential)]
    private struct Margins { public int Left, Right, Top, Bottom; }
    [DllImport("dwmapi.dll")]
    private static extern int DwmSetWindowAttribute(IntPtr hwnd, int attribute, ref int value, int size);
    [DllImport("dwmapi.dll")]
    private static extern int DwmExtendFrameIntoClientArea(IntPtr hwnd, ref Margins margins);
}
