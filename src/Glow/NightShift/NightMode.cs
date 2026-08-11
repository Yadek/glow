using Glow.Monitors;
using Glow.Native;
using Glow.Settings;

namespace Glow.NightShift;

/// <summary>Night mode state of a single display.</summary>
public readonly record struct NightState(bool Enabled, int Intensity);

// Night mode, per display.
//
// Implemented with gamma ramps (SetDeviceGammaRamp) — the approach f.lux and
// redshift use. Gamma is applied to one display's DC at a time, so every screen
// keeps its own warmth, and it works on panels that have no DDC/CI at all
// (most laptop displays). It is also instant, unlike DDC colour writes.
//
// Gamma is volatile: Windows resets it on resume, resolution changes and session
// switches, so Reapply() must run on those events (see TrayContext).
//
// Thread-safe: SystemEvents callbacks arrive on their own thread.
public static class NightMode
{
    // 0% = neutral (6500K), 100% = warmest.
    private const int NeutralKelvin = 6500;
    private const int WarmestKelvin = 2700;

    private static readonly object Gate = new();
    private static readonly Dictionary<string, NightState> States = new(StringComparer.OrdinalIgnoreCase);

    // Highest gamma "strength" Windows accepted for a device last time — see ApplyRamp.
    private static readonly Dictionary<string, double> AcceptedBlend = new(StringComparer.OrdinalIgnoreCase);

    // Dragging the intensity slider produces ~60 changes a second. The ramp is
    // applied on every one of them (it's cheap and the user needs to see it), but
    // the registry write is coalesced so a drag costs one write, not sixty.
    private static readonly HashSet<string> DirtyIntensity = new(StringComparer.OrdinalIgnoreCase);
    private static System.Threading.Timer? _flushTimer;
    private const int FlushDelayMs = 500;

    private static List<DisplayInfo> _displays = new();

    /// <summary>Displays known at the last enumeration, in popup order.</summary>
    public static IReadOnlyList<DisplayInfo> Displays
    {
        get { lock (Gate) { return _displays.ToList(); } }
    }

    /// <summary>Loads persisted state (migrating the old global setting) and applies it.</summary>
    public static void Initialize()
    {
        List<DisplayInfo> displays = DisplayCatalog.Enumerate();
        AppSettings.MigrateLegacyNightSettings(displays.Select(d => d.Key));

        lock (Gate)
        {
            _displays = displays;
            States.Clear();
            foreach (var d in displays)
            {
                States[d.Key] = new NightState(
                    AppSettings.GetNightEnabled(d.Key),
                    AppSettings.GetNightIntensity(d.Key));
            }
            ApplyAllLocked();
        }
    }

    /// <summary>
    /// Re-enumerates displays and re-applies every ramp. Call after resume, a
    /// display change or a session switch, when Windows has reset the gamma —
    /// and when a new monitor appears, so it picks up its saved setting.
    /// </summary>
    public static void Reapply()
    {
        List<DisplayInfo> displays = DisplayCatalog.Enumerate();

        lock (Gate)
        {
            _displays = displays;
            foreach (var d in displays)
            {
                // A display we've never seen brings its persisted setting with it.
                if (!States.ContainsKey(d.Key))
                {
                    States[d.Key] = new NightState(
                        AppSettings.GetNightEnabled(d.Key),
                        AppSettings.GetNightIntensity(d.Key));
                }
            }
            ApplyAllLocked();
        }
    }

    public static NightState Get(string displayKey)
    {
        lock (Gate)
        {
            return States.TryGetValue(displayKey, out var s)
                ? s
                : new NightState(false, AppSettings.DefaultNightIntensity);
        }
    }

    public static void SetEnabled(string displayKey, bool enabled)
    {
        lock (Gate)
        {
            var state = Get(displayKey) with { Enabled = enabled };
            States[displayKey] = state;
            AppSettings.SetNightEnabled(displayKey, enabled);
            ApplyLocked(displayKey, state);
        }
    }

    public static void SetIntensity(string displayKey, int percent)
    {
        percent = Math.Clamp(percent, 0, 100);
        lock (Gate)
        {
            var state = Get(displayKey) with { Intensity = percent };
            States[displayKey] = state;
            ApplyLocked(displayKey, state);
            MarkIntensityDirtyLocked(displayKey);
        }
    }

    // ----- "all monitors at once" -----

    /// <summary>true when every display is on, false when none is, null when they differ.</summary>
    public static bool? AllEnabled
    {
        get
        {
            lock (Gate)
            {
                if (_displays.Count == 0) return false;
                bool anyOn = false, anyOff = false;
                foreach (var d in _displays)
                {
                    if (Get(d.Key).Enabled) anyOn = true; else anyOff = true;
                }
                return anyOn && anyOff ? null : anyOn;
            }
        }
    }

    public static void SetAllEnabled(bool enabled)
    {
        lock (Gate)
        {
            foreach (var d in _displays.ToList())
            {
                var state = Get(d.Key) with { Enabled = enabled };
                States[d.Key] = state;
                AppSettings.SetNightEnabled(d.Key, enabled);
                ApplyLocked(d.Key, state);
            }
        }
    }

    public static void SetAllIntensity(int percent)
    {
        percent = Math.Clamp(percent, 0, 100);
        lock (Gate)
        {
            foreach (var d in _displays.ToList())
            {
                var state = Get(d.Key) with { Intensity = percent };
                States[d.Key] = state;
                ApplyLocked(d.Key, state);
                MarkIntensityDirtyLocked(d.Key);
            }
        }
    }

    /// <summary>Toggles every display: any on → all off, otherwise all on.</summary>
    public static void ToggleAll() => SetAllEnabled(AllEnabled != true);

    // ----- coalesced persistence -----

    private static void MarkIntensityDirtyLocked(string displayKey)
    {
        DirtyIntensity.Add(displayKey);
        _flushTimer ??= new System.Threading.Timer(_ => Flush(), null, Timeout.Infinite, Timeout.Infinite);
        _flushTimer.Change(FlushDelayMs, Timeout.Infinite);
    }

    /// <summary>Writes any pending intensity changes to the registry immediately (call before exit).</summary>
    public static void Flush()
    {
        lock (Gate)
        {
            foreach (string key in DirtyIntensity)
            {
                if (States.TryGetValue(key, out var state))
                {
                    AppSettings.SetNightIntensity(key, state.Intensity);
                }
            }
            DirtyIntensity.Clear();
        }
    }

    /// <summary>Resets every display to neutral without touching the saved state (used on exit).</summary>
    public static void RestoreNeutral()
    {
        lock (Gate)
        {
            foreach (var d in _displays.ToList())
            {
                ApplyToDevice(d.DeviceName, 0);
            }
        }
    }

    // ----- applying -----

    private static void ApplyAllLocked()
    {
        foreach (var d in _displays)
        {
            ApplyLocked(d.Key, Get(d.Key));
        }
    }

    private static void ApplyLocked(string displayKey, NightState state)
    {
        foreach (var d in _displays)
        {
            if (string.Equals(d.Key, displayKey, StringComparison.OrdinalIgnoreCase))
            {
                ApplyToDevice(d.DeviceName, state.Enabled ? state.Intensity : 0);
                return;
            }
        }
    }

    private static void ApplyToDevice(string deviceName, int percent)
    {
        IntPtr hdc = NativeMethods.CreateDC(null, deviceName, null, IntPtr.Zero);
        if (hdc == IntPtr.Zero) return;
        try
        {
            ApplyRamp(hdc, deviceName, percent);
        }
        finally
        {
            NativeMethods.DeleteDC(hdc);
        }
    }

    // Windows refuses gamma ramps it considers too extreme (the exact limit depends
    // on the GdiIcmGammaRange policy), so a very warm setting can silently do
    // nothing. Try the requested warmth and fall back to progressively milder
    // versions until one is accepted; identity always is.
    //
    // Probing from scratch on every slider tick would mean up to 21 calls per
    // frame, so the strength that worked last time is cached per device and used
    // as the starting point — with a little headroom, so a milder setting can
    // climb back to full strength.
    private static void ApplyRamp(IntPtr hdc, string deviceName, int percent)
    {
        double start = AcceptedBlend.TryGetValue(deviceName, out double cached)
            ? Math.Min(1.0, cached + 0.2)
            : 1.0;

        for (double blend = start; blend > 0; blend -= 0.05)
        {
            if (NativeMethods.SetDeviceGammaRamp(hdc, BuildRamp(percent, blend)))
            {
                AcceptedBlend[deviceName] = blend;
                return;
            }
        }

        // Identity — always accepted, and leaves the screen untinted.
        NativeMethods.SetDeviceGammaRamp(hdc, BuildRamp(0, 1.0));
    }

    // blend 1.0 = the full requested warmth, 0.0 = neutral.
    private static ushort[] BuildRamp(int percent, double blend)
    {
        percent = Math.Clamp(percent, 0, 100);
        int kelvin = NeutralKelvin - percent * (NeutralKelvin - WarmestKelvin) / 100;

        // Normalise against 6500K so 0% is a perfectly neutral (identity) ramp.
        var (nr, ng, nb) = KelvinToRgb(NeutralKelvin);
        var (r, g, b) = KelvinToRgb(kelvin);
        double fr = 1 + (r / nr - 1) * blend;
        double fg = 1 + (g / ng - 1) * blend;
        double fb = 1 + (b / nb - 1) * blend;

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
