using Glow.Native;

namespace Glow.Monitors;

// One DDC/CI-capable physical monitor. Wraps a native PHYSICAL_MONITOR handle.
// DDC writes are slow, so RequestPercent applies them on a background thread and
// only keeps the latest value, so dragging the slider never blocks the UI.
public sealed class BrightnessMonitor : IDisposable
{
    private NativeMethods.PHYSICAL_MONITOR _physical;
    private bool _disposed;

    private readonly object _gate = new();
    private int _pending = -1;
    private bool _running;

    public string Name { get; }
    public uint Minimum { get; }
    public uint Maximum { get; }
    public uint Current { get; private set; }

    private BrightnessMonitor(
        NativeMethods.PHYSICAL_MONITOR physical,
        string name, uint min, uint current, uint max)
    {
        _physical = physical;
        Name = name;
        Minimum = min;
        Current = current;
        Maximum = max;
    }

    // Returns null when the display doesn't expose DDC/CI brightness.
    internal static BrightnessMonitor? TryCreate(NativeMethods.PHYSICAL_MONITOR physical, string? friendlyName)
    {
        if (!NativeMethods.GetMonitorBrightness(
                physical.hPhysicalMonitor, out uint min, out uint cur, out uint max)
            || max <= min)
        {
            // Not supported (e.g. most laptop internal panels) — release and skip.
            NativeMethods.DestroyPhysicalMonitors(1, new[] { physical });
            return null;
        }

        string name =
            !string.IsNullOrWhiteSpace(friendlyName) ? friendlyName!.Trim() :
            !string.IsNullOrWhiteSpace(physical.szPhysicalMonitorDescription) ? physical.szPhysicalMonitorDescription.Trim() :
            "Display";

        return new BrightnessMonitor(physical, name, min, cur, max);
    }

    // Brightness as 0–100% of the monitor's supported range.
    public int Percent
    {
        get
        {
            uint range = Maximum - Minimum;
            return range == 0 ? 0 : (int)Math.Round((Current - Minimum) * 100.0 / range);
        }
    }

    // Queue a new brightness (0–100). Returns at once; the write runs in the background.
    public void RequestPercent(int percent)
    {
        lock (_gate)
        {
            if (_disposed) return;
            _pending = Math.Clamp(percent, 0, 100);
            if (_running) return;
            _running = true;
        }
        ThreadPool.QueueUserWorkItem(_ => DrainWrites());
    }

    private void DrainWrites()
    {
        while (true)
        {
            int value;
            lock (_gate)
            {
                if (_disposed || _pending < 0)
                {
                    _running = false;
                    return;
                }
                value = _pending;
                _pending = -1;
            }
            ApplyPercent(value);
        }
    }

    private void ApplyPercent(int percent)
    {
        uint value = (uint)Math.Round(Minimum + (Maximum - Minimum) * (percent / 100.0));
        value = Math.Clamp(value, Minimum, Maximum);
        if (NativeMethods.SetMonitorBrightness(_physical.hPhysicalMonitor, value))
        {
            Current = value;
        }
    }

    public void Dispose()
    {
        lock (_gate)
        {
            if (_disposed) return;
            _disposed = true;
        }
        // Let any in-flight DDC/CI write finish before we destroy the handle.
        SpinWait.SpinUntil(() => { lock (_gate) { return !_running; } }, 500);
        NativeMethods.DestroyPhysicalMonitors(1, new[] { _physical });
    }
}
