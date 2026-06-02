using System.Runtime.InteropServices;
using Glow.Native;
using Glow.Settings;

namespace Glow.NightShift;

// Night mode implemented with display gamma ramps (SetDeviceGammaRamp) — the same
// proven approach f.lux/redshift use. Warms every attached monitor with a smooth
// 0–100% intensity. Reliable across Windows 10/11 and applied instantly.
public static class NightLight
{
    [DllImport("gdi32.dll")]
    private static extern bool SetDeviceGammaRamp(IntPtr hdc, ushort[] ramp);

    [DllImport("gdi32.dll", CharSet = CharSet.Unicode)]
    private static extern IntPtr CreateDC(string? driver, string device, string? port, IntPtr data);

    [DllImport("gdi32.dll")]
    private static extern bool DeleteDC(IntPtr hdc);

    [DllImport("user32.dll")]
    private static extern IntPtr GetDC(IntPtr hwnd);

    [DllImport("user32.dll")]
    private static extern int ReleaseDC(IntPtr hwnd, IntPtr hdc);

    private const int DISPLAY_DEVICE_ATTACHED_TO_DESKTOP = 0x00000001;

    // 0% = neutral (6500K), 100% = warm.
    private const int NeutralKelvin = 6500;
    private const int WarmestKelvin = 2700;
    private const int DefaultIntensity = 50;

    private static bool _enabled;
    private static int _intensity = DefaultIntensity;

    public static bool IsEnabled() => _enabled;
    public static int GetIntensity() => _intensity;

    // Restore persisted state at startup and re-apply (gamma resets each session).
    public static void Initialize()
    {
        _enabled = AppSettings.NightEnabled;
        _intensity = Math.Clamp(AppSettings.NightIntensity, 0, 100);
        if (_enabled) Apply(_intensity);
    }

    public static void SetEnabled(bool on)
    {
        _enabled = on;
        AppSettings.NightEnabled = on;
        Apply(on ? _intensity : 0); // enabling applies exactly the current level
    }

    public static void SetIntensity(int percent)
    {
        _intensity = Math.Clamp(percent, 0, 100);
        AppSettings.NightIntensity = _intensity;
        if (_enabled) Apply(_intensity);
    }

    // Reset displays to neutral without changing the saved state (used on exit).
    public static void RestoreNeutral() => Apply(0);

    private static void Apply(int percent)
    {
        ushort[] ramp = BuildRamp(percent);
        bool applied = false;

        foreach (string device in ActiveDisplays())
        {
            IntPtr hdc = CreateDC(null, device, null, IntPtr.Zero);
            if (hdc != IntPtr.Zero)
            {
                if (SetDeviceGammaRamp(hdc, ramp)) applied = true;
                DeleteDC(hdc);
            }
        }

        if (!applied)
        {
            IntPtr dc = GetDC(IntPtr.Zero);
            if (dc != IntPtr.Zero)
            {
                SetDeviceGammaRamp(dc, ramp);
                ReleaseDC(IntPtr.Zero, dc);
            }
        }
    }

    private static IEnumerable<string> ActiveDisplays()
    {
        for (uint i = 0; ; i++)
        {
            var dd = new NativeMethods.DISPLAY_DEVICE { cb = Marshal.SizeOf<NativeMethods.DISPLAY_DEVICE>() };
            if (!NativeMethods.EnumDisplayDevices(null, i, ref dd, 0)) break;
            if ((dd.StateFlags & DISPLAY_DEVICE_ATTACHED_TO_DESKTOP) != 0)
            {
                yield return dd.DeviceName;
            }
        }
    }

    private static ushort[] BuildRamp(int percent)
    {
        percent = Math.Clamp(percent, 0, 100);
        int kelvin = NeutralKelvin - percent * (NeutralKelvin - WarmestKelvin) / 100;

        // Normalise against 6500K so 0% is a perfectly neutral (identity) ramp.
        var (nr, ng, nb) = KelvinToRgb(NeutralKelvin);
        var (r, g, b) = KelvinToRgb(kelvin);
        double fr = r / nr, fg = g / ng, fb = b / nb;

        var ramp = new ushort[768];
        for (int i = 0; i < 256; i++)
        {
            int baseVal = i * 257; // 0..65535
            ramp[i] = Clamp(baseVal * fr);
            ramp[256 + i] = Clamp(baseVal * fg);
            ramp[512 + i] = Clamp(baseVal * fb);
        }
        return ramp;
    }

    private static ushort Clamp(double v) => (ushort)Math.Clamp(v, 0, 65535);

    // Tanner Helland's blackbody approximation (returns 0..255 per channel).
    private static (double r, double g, double b) KelvinToRgb(int kelvin)
    {
        double t = kelvin / 100.0;
        double r, g, b;

        r = t <= 66 ? 255 : 329.698727446 * Math.Pow(t - 60, -0.1332047592);
        g = t <= 66
            ? 99.4708025861 * Math.Log(t) - 161.1195681661
            : 288.1221695283 * Math.Pow(t - 60, -0.0755148492);
        if (t >= 66) b = 255;
        else if (t <= 19) b = 0;
        else b = 138.5177312231 * Math.Log(t - 10) - 305.0447927307;

        return (Math.Clamp(r, 1, 255), Math.Clamp(g, 1, 255), Math.Clamp(b, 1, 255));
    }
}
