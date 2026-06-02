using System.Runtime.InteropServices;
using System.Text;
using Glow.Native;
using Microsoft.Win32;

namespace Glow.Monitors;

// Resolves the real model name of a display (e.g. "DELL U2419H") instead of
// "Generic PnP Monitor". HMONITOR -> device interface path -> EDID blob in the
// registry -> monitor-name descriptor (0xFC).
public static class MonitorNameResolver
{
    public static string? Resolve(IntPtr hMonitor)
    {
        var info = new NativeMethods.MONITORINFOEX { cbSize = Marshal.SizeOf<NativeMethods.MONITORINFOEX>() };
        if (!NativeMethods.GetMonitorInfo(hMonitor, ref info))
        {
            return null;
        }

        var dd = new NativeMethods.DISPLAY_DEVICE { cb = Marshal.SizeOf<NativeMethods.DISPLAY_DEVICE>() };
        if (!NativeMethods.EnumDisplayDevices(info.szDevice, 0, ref dd, NativeMethods.EDD_GET_DEVICE_INTERFACE_NAME))
        {
            return null;
        }

        return ParseFromInterface(dd.DeviceID);
    }

    // dd.DeviceID looks like:  \\?\DISPLAY#GSM5B09#5&2d6c&0&UID256#{e6f07b5f-...}
    private static string? ParseFromInterface(string? deviceId)
    {
        if (string.IsNullOrEmpty(deviceId)) return null;

        var parts = deviceId.Split('#');
        if (parts.Length < 3) return null;

        string vendorProduct = parts[1]; // GSM5B09
        string instance = parts[2];       // 5&2d6c&0&UID256

        return ReadEdidName(vendorProduct, instance) ?? PrettyVendorProduct(vendorProduct);
    }

    private static string? ReadEdidName(string vendorProduct, string instance)
    {
        string path = $@"SYSTEM\CurrentControlSet\Enum\DISPLAY\{vendorProduct}\{instance}\Device Parameters";
        using var key = Registry.LocalMachine.OpenSubKey(path);
        if (key?.GetValue("EDID") is not byte[] edid || edid.Length < 128)
        {
            return null;
        }
        return DecodeEdidMonitorName(edid);
    }

    // EDID block 0 has four 18-byte descriptors at offsets 54/72/90/108.
    // A descriptor starting with 00 00 00 FC is the ASCII monitor name.
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
                // EDID names are padded/terminated oddly; trim stray trailing dashes/spaces.
                string name = sb.ToString().Trim().TrimEnd('-', ' ').Trim();
                if (name.Length > 0) return name;
            }
        }
        return null;
    }

    // Fallback: "GSM5B09" → "GSM 5B09" (3-letter PnP vendor id + product code).
    private static string PrettyVendorProduct(string vendorProduct)
    {
        if (vendorProduct.Length > 3)
        {
            return $"{vendorProduct[..3]} {vendorProduct[3..]}";
        }
        return vendorProduct;
    }
}
