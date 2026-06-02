using Microsoft.Win32;

namespace Glow.NightShift;

// Controls the Windows "Night light" feature by editing its (undocumented)
// CloudStore registry blobs, so toggling here flips the real Windows switch and
// reading reflects changes made in Windows. Defensive: if a blob doesn't match
// the known layout, the operation is skipped rather than risking corruption.
public static class NightLight
{
    private const string StatePath =
        @"Software\Microsoft\Windows\CurrentVersion\CloudStore\Store\DefaultAccount\Current\default$windows.data.bluelightreduction.bluelightreductionstate\windows.data.bluelightreduction.bluelightreductionstate";
    private const string SettingsPath =
        @"Software\Microsoft\Windows\CurrentVersion\CloudStore\Store\DefaultAccount\Current\default$windows.data.bluelightreduction.settings\windows.data.bluelightreduction.settings";

    // Night light colour-temperature range (Kelvin). Lower = warmer.
    private const int MinTemp = 1200; // 100% intensity
    private const int MaxTemp = 6500; // 0% intensity (neutral)

    // Available when the state blob exists and matches the known layout.
    public static bool IsSupported
    {
        get
        {
            byte[]? d = ReadData(StatePath);
            return d is not null && d.Length > 18 && d[0] == 0x43 && d[1] == 0x42
                   && (d[18] == 0x13 || d[18] == 0x15);
        }
    }

    public static bool IsEnabled()
    {
        byte[]? d = ReadData(StatePath);
        return d is not null && d.Length > 18 && d[18] == 0x15;
    }

    public static void SetEnabled(bool on)
    {
        byte[]? data = ReadData(StatePath);
        if (data is null || data.Length <= 24) return;

        var b = new List<byte>(data);
        StampTime(b);

        if (on && b[18] == 0x13)
        {
            // mark enabled and insert the "10 00" run after the inner "CB" header
            b[18] = 0x15;
            b.InsertRange(23, new byte[] { 0x10, 0x00 });
        }
        else if (!on && b[18] == 0x15 && b[23] == 0x10 && b[24] == 0x00)
        {
            b[18] = 0x13;
            b.RemoveRange(23, 2);
        }

        WriteData(StatePath, b.ToArray());
    }

    // 0..100, where 0 = neutral and 100 = warmest.
    public static int GetIntensity()
    {
        byte[]? d = ReadData(SettingsPath);
        if (d is null) return 0;
        int i = FindTempIndex(d);
        if (i < 0) return 0;
        int temp = Math.Clamp(DecodeVarint2(d, i), MinTemp, MaxTemp);
        return (int)Math.Round((MaxTemp - temp) * 100.0 / (MaxTemp - MinTemp));
    }

    public static void SetIntensity(int percent)
    {
        byte[]? data = ReadData(SettingsPath);
        if (data is null) return;
        int i = FindTempIndex(data);
        if (i < 0) return;

        percent = Math.Clamp(percent, 0, 100);
        int temp = (int)Math.Round(MaxTemp - percent / 100.0 * (MaxTemp - MinTemp));
        byte[] v = EncodeVarint(temp); // 1200..6500 -> always 2 bytes
        if (v.Length != 2) return;

        var b = new List<byte>(data);
        StampTime(b);
        b[i] = v[0];
        b[i + 1] = v[1];
        WriteData(SettingsPath, b.ToArray());
    }

    // ----- blob helpers -----

    // The night colour temperature is the 2-byte varint after the 0xCF 0x28 marker.
    private static int FindTempIndex(byte[] d)
    {
        for (int i = 0; i + 3 < d.Length; i++)
        {
            if (d[i] == 0xCF && d[i + 1] == 0x28 && (d[i + 2] & 0x80) != 0 && (d[i + 3] & 0x80) == 0)
            {
                return i + 2;
            }
        }
        return -1;
    }

    // Rewrite the 5-byte "last changed" timestamp (after 0x2A 0x06) to now, so
    // Windows accepts the change instead of treating it as stale.
    private static void StampTime(List<byte> b)
    {
        if (b.Count < 15 || b[8] != 0x2A || b[9] != 0x06) return;
        byte[] v = EncodeVarint(DateTimeOffset.UtcNow.ToUnixTimeSeconds());
        if (v.Length != 5) return; // keep byte alignment (true for current era)
        for (int k = 0; k < 5; k++) b[10 + k] = v[k];
    }

    private static int DecodeVarint2(byte[] d, int i) => (d[i] & 0x7F) | (d[i + 1] << 7);

    private static byte[] EncodeVarint(long value)
    {
        var bytes = new List<byte>();
        do
        {
            byte chunk = (byte)(value & 0x7F);
            value >>= 7;
            if (value != 0) chunk |= 0x80;
            bytes.Add(chunk);
        } while (value != 0);
        return bytes.ToArray();
    }

    private static byte[]? ReadData(string path)
    {
        try
        {
            using var key = Registry.CurrentUser.OpenSubKey(path);
            return key?.GetValue("Data") as byte[];
        }
        catch
        {
            return null;
        }
    }

    private static void WriteData(string path, byte[] data)
    {
        try
        {
            using var key = Registry.CurrentUser.OpenSubKey(path, writable: true)
                            ?? Registry.CurrentUser.CreateSubKey(path);
            key?.SetValue("Data", data, RegistryValueKind.Binary);
        }
        catch
        {
            // ignore — night light just won't change
        }
    }
}
