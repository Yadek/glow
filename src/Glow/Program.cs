using System.Threading;
using Glow.UI;

namespace Glow;

internal static class Program
{
    // Single global instance so we never end up with two tray icons.
    private static Mutex? _instanceMutex;

    [STAThread]
    private static void Main()
    {
        _instanceMutex = new Mutex(initiallyOwned: true, "Glow.SingleInstance.{B7A1F2C0-9E3D-4A7B-8C21-5F0E3A9D1C44}", out bool isNew);
        if (!isNew)
        {
            return; // already running
        }

        ApplicationConfiguration.Initialize();
        Application.Run(new TrayContext());

        GC.KeepAlive(_instanceMutex);
    }
}
