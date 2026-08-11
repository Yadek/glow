using Glow.Native;

namespace Glow.Monitors;

// A display as the UI sees it: always present, with hardware brightness attached
// only when the panel actually speaks DDC/CI. Displays that don't (most laptop
// panels) still appear, because night mode works on them through gamma.
public sealed class DisplayTarget : IDisposable
{
    public DisplayInfo Info { get; }
    public BrightnessMonitor? Brightness { get; }

    public string Key => Info.Key;
    public string Name => Info.Name;
    public string DeviceName => Info.DeviceName;
    public bool SupportsBrightness => Brightness is not null;

    internal DisplayTarget(DisplayInfo info, BrightnessMonitor? brightness)
    {
        Info = info;
        Brightness = brightness;
    }

    public void Dispose() => Brightness?.Dispose();
}

// Builds the list of displays for the popup. DDC/CI handles are acquired here,
// which costs monitor I2C round-trips, so the result is cached and only rebuilt
// when the display topology actually changes (see Invalidate).
public sealed class DisplayManager : IDisposable
{
    private readonly List<DisplayTarget> _displays = new();
    private bool _stale = true;

    public IReadOnlyList<DisplayTarget> Displays => _displays;

    /// <summary>Marks the cache dirty so the next Refresh() re-enumerates. Call on hot-plug.</summary>
    public void Invalidate() => _stale = true;

    /// <summary>Rebuilds the display list if the topology changed since last time.</summary>
    public void Refresh(bool force = false)
    {
        if (!_stale && !force)
        {
            return;
        }

        DisposeDisplays();

        foreach (var info in DisplayCatalog.Enumerate())
        {
            _displays.Add(new DisplayTarget(info, TryAttachBrightness(info.HMonitor)));
        }

        _stale = false;
    }

    // Returns the first DDC/CI brightness-capable physical monitor behind this
    // HMONITOR, or null. Handles we don't keep are released immediately.
    private static BrightnessMonitor? TryAttachBrightness(IntPtr hMonitor)
    {
        if (!NativeMethods.GetNumberOfPhysicalMonitorsFromHMONITOR(hMonitor, out uint count) || count == 0)
        {
            return null;
        }

        var array = new NativeMethods.PHYSICAL_MONITOR[count];
        if (!NativeMethods.GetPhysicalMonitorsFromHMONITOR(hMonitor, count, array))
        {
            return null;
        }

        BrightnessMonitor? attached = null;
        for (int i = 0; i < array.Length; i++)
        {
            if (attached is null)
            {
                // TryCreate releases the handle itself when brightness isn't supported.
                attached = BrightnessMonitor.TryCreate(array[i]);
                if (attached is not null) continue;
            }
            else
            {
                NativeMethods.DestroyPhysicalMonitors(1, new[] { array[i] });
            }
        }

        return attached;
    }

    private void DisposeDisplays()
    {
        foreach (var d in _displays)
        {
            d.Dispose();
        }
        _displays.Clear();
    }

    public void Dispose() => DisposeDisplays();
}
