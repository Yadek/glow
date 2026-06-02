using Glow.Native;

namespace Glow.Monitors;

// Finds all attached DDC/CI monitors. Call Refresh() on each popup open so
// freshly plugged displays show up.
public sealed class MonitorManager : IDisposable
{
    private readonly List<BrightnessMonitor> _monitors = new();

    public IReadOnlyList<BrightnessMonitor> Monitors => _monitors;

    public void Refresh()
    {
        DisposeMonitors();

        // EnumDisplayMonitors hands us one HMONITOR per logical display.
        NativeMethods.EnumDisplayMonitors(
            IntPtr.Zero, IntPtr.Zero, OnMonitorEnum, IntPtr.Zero);
    }

    private bool OnMonitorEnum(IntPtr hMonitor, IntPtr hdc, ref NativeMethods.RECT rect, IntPtr data)
    {
        if (!NativeMethods.GetNumberOfPhysicalMonitorsFromHMONITOR(hMonitor, out uint count)
            || count == 0)
        {
            return true; // continue enumeration
        }

        var array = new NativeMethods.PHYSICAL_MONITOR[count];
        if (!NativeMethods.GetPhysicalMonitorsFromHMONITOR(hMonitor, count, array))
        {
            return true;
        }

        // Resolve the real model name once per logical display.
        string? friendlyName = MonitorNameResolver.Resolve(hMonitor);

        for (int i = 0; i < array.Length; i++)
        {
            // If a display somehow exposes several physical monitors, keep names unique.
            string? name = (count > 1 && friendlyName is not null) ? $"{friendlyName} ({i + 1})" : friendlyName;
            var monitor = BrightnessMonitor.TryCreate(array[i], name);
            if (monitor is not null)
            {
                _monitors.Add(monitor);
            }
        }

        return true;
    }

    private void DisposeMonitors()
    {
        foreach (var m in _monitors)
        {
            m.Dispose();
        }
        _monitors.Clear();
    }

    public void Dispose() => DisposeMonitors();
}
