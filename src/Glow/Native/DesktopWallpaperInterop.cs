using System.Runtime.InteropServices;
using Glow.Wallpapers;

namespace Glow.Native;

// shobjidl_core.h: preserve the native vtable order, including unused methods.
[ComImport, Guid("B92B56A9-8B55-4E14-9A89-0199BBB6F93B"), InterfaceType(ComInterfaceType.InterfaceIsIUnknown)]
internal interface IDesktopWallpaper
{
    void SetWallpaper([MarshalAs(UnmanagedType.LPWStr)] string monitorId, [MarshalAs(UnmanagedType.LPWStr)] string wallpaper);
    [return: MarshalAs(UnmanagedType.LPWStr)] string GetWallpaper([MarshalAs(UnmanagedType.LPWStr)] string monitorId);
    [return: MarshalAs(UnmanagedType.LPWStr)] string GetMonitorDevicePathAt(uint index);
    uint GetMonitorDevicePathCount();
    void GetMonitorRECT([MarshalAs(UnmanagedType.LPWStr)] string monitorId, out NativeMethods.RECT rect);
    void SetBackgroundColor(uint color);
    uint GetBackgroundColor();
    void SetPosition(WallpaperPosition position);
    WallpaperPosition GetPosition();
    void SetSlideshow(IntPtr items);
    IntPtr GetSlideshow();
    void SetSlideshowOptions(uint options, uint seconds);
    void GetSlideshowOptions(out uint options, out uint seconds);
    void AdvanceSlideshow([MarshalAs(UnmanagedType.LPWStr)] string monitorId, uint direction);
    uint GetStatus();
    void Enable([MarshalAs(UnmanagedType.Bool)] bool enable);
}
