using System.Runtime.InteropServices;
using System.Text;
using Glow.Native;
using Microsoft.Win32;

namespace Glow.Monitors;

// One attached display, independent of whether it speaks DDC/CI.
//
// Night mode works through gamma ramps on DeviceName, which every display has,
// including laptop panels. Hardware brightness needs DDC/CI and is attached
// separately by DisplayManager.
public sealed record DisplayInfo(
    string Key,          // stable across replug/reboot — used as the settings key
    string Name,         // friendly model name, e.g. "DELL U2419H"
    string DeviceName,   // \\.\DISPLAY1 — the handle gamma ramps are applied to
    IntPtr HMonitor);

// Enumerates displays. Deliberately cheap: no DDC/CI traffic, so night mode can
// run at startup and after every resume without waiting on monitor I2C.
public static class DisplayCatalog
{
    public static List<DisplayInfo> Enumerate()
    {
        var found = new List<DisplayInfo>();
        var handles = new List<IntPtr>();

        // The callback must stay alive for the duration of the call.
        NativeMethods.MonitorEnumProc callback =
            (IntPtr hMonitor, IntPtr hdc, ref NativeMethods.RECT rect, IntPtr data) =>
            {
                handles.Add(hMonitor);
                return true;
            };
        NativeMethods.EnumDisplayMonitors(IntPtr.Zero, IntPtr.Zero, callback, IntPtr.Zero);
        GC.KeepAlive(callback);

        int fallbackIndex = 0;
        foreach (IntPtr hMonitor in handles)
        {
            fallbackIndex++;

            var info = new NativeMethods.MONITORINFOEX { cbSize = Marshal.SizeOf<NativeMethods.MONITORINFOEX>() };
            if (!NativeMethods.GetMonitorInfo(hMonitor, ref info))
            {
                continue;
            }

            string device = info.szDevice; // \\.\DISPLAY1
            var (key, name) = Identify(device);

            found.Add(new DisplayInfo(
                Key: key ?? Sanitize(device),
                Name: name ?? Localization.Strings.Display(fallbackIndex),
                DeviceName: device,
                HMonitor: hMonitor));
        }

        return found;
    }

    // Resolves a display's stable key and model name from its device interface
    // path:  \\?\DISPLAY#GSM5B09#5&2d6c&0&UID256#{e6f07b5f-...}
    //                     ^vendor+product ^instance
    // Both parts survive reboots and replugs into the same port, so together they
    // make a good settings key. The model name comes from the EDID blob.
    private static (string? Key, string? Name) Identify(string deviceName)
    {
        var dd = new NativeMethods.DISPLAY_DEVICE { cb = Marshal.SizeOf<NativeMethods.DISPLAY_DEVICE>() };
        if (!NativeMethods.EnumDisplayDevices(deviceName, 0, ref dd, NativeMethods.EDD_GET_DEVICE_INTERFACE_NAME))
        {
            return (null, null);
        }

        string deviceId = dd.DeviceID;
        if (string.IsNullOrEmpty(deviceId))
        {
            return (null, null);
        }

        var parts = deviceId.Split('#');
        if (parts.Length < 3)
        {
            return (null, null);
        }

        string vendorProduct = parts[1]; // GSM5B09
        string instance = parts[2];      // 5&2d6c&0&UID256

        string key = Sanitize($"{vendorProduct}_{instance}");
        string? name = ReadEdidName(vendorProduct, instance) ?? PrettyVendorProduct(vendorProduct);
        return (key, name);
    }

    // Registry key names can't contain a backslash; keep it to a safe alphabet.
    private static string Sanitize(string raw)
    {
        var sb = new StringBuilder(raw.Length);
        foreach (char c in raw)
        {
            sb.Append(char.IsLetterOrDigit(c) ? char.ToUpperInvariant(c) : '_');
        }
        return sb.ToString().Trim('_');
    }

    private static string? ReadEdidName(string vendorProduct, string instance)
    {
        string path = $@"SYSTEM\CurrentControlSet\Enum\DISPLAY\{vendorProduct}\{instance}\Device Parameters";
        try
        {
            using var key = Registry.LocalMachine.OpenSubKey(path);
            if (key?.GetValue("EDID") is not byte[] edid || edid.Length < 128)
            {
                return null;
            }
            return DecodeEdidMonitorName(edid);
        }
        catch
        {
            return null; // registry access denied / malformed key — fall back to the PnP id
        }
    }

    // EDID block 0 has four 18-byte descriptors at offsets 54/72/90/108.
    // A descriptor starting with 00 00 00 FC holds the ASCII monitor name.
    private static string? DecodeEdidMonitorName(byte[] edid)
    {
        for (int offset = 54; offset <= 108; offset += 18)
        {
            if (edid[offset] == 0x00 && edid[offset + 1] == 0x00 &&
                edid[offset + 2] == 0x00 && edid[offset + 3] == 0xFC)
            {
                var sb = new StringBuilder(13);
                for (int i = offset + 5; i < offset + 18; i++)
                {
                    byte b = edid[i];
                    if (b == 0x0A || b == 0x00) break; // LF terminator / padding
                    sb.Append((char)b);
                }
                string name = sb.ToString().Trim().TrimEnd('-', ' ').Trim();
                if (name.Length > 0) return name;
            }
        }
        return null;
    }

    // Fallback: "GSM5B09" → "GSM 5B09" (3-letter PnP vendor id + product code).
    private static string PrettyVendorProduct(string vendorProduct)
        => vendorProduct.Length > 3 ? $"{vendorProduct[..3]} {vendorProduct[3..]}" : vendorProduct;
}
