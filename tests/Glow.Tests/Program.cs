using Glow.Wallpapers;

internal static class Program
{
    private static int _count;

    [STAThread]
    private static int Main(string[] args)
    {
        try
        {
            if (args.Length == 2 && args[0] == "-control" && args[1] == "getWallpaper")
            {
                if (Environment.GetEnvironmentVariable("GLOW_TEST_ENGINE_MODE") == "oversize") Console.Write(new string('x', 70000));
                Thread.Sleep(3000);
                return 0;
            }
            if (args.Contains("--smoke")) { SmokeChecks.Run(); return 0; }
            WallpaperTests.Run();
            EngineTests.Run();
            PreviewTests.Run();
            UiTests.Run();
            PreviewLifetimeTests.Run();
            PopupRenderTests.Run(args.Length > 0 ? args[0] : Path.Combine(Environment.CurrentDirectory, "artifacts", "ui"));
            Console.WriteLine($"PASS: {_count} assertions");
            return 0;
        }
        catch (Exception error)
        {
            Console.Error.WriteLine(error);
            return 1;
        }
    }

    internal static void Check(bool condition, string name)
    {
        _count++;
        if (!condition) throw new InvalidOperationException(name);
    }
}
