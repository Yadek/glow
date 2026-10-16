using Glow.Wallpapers;
using System.Diagnostics;
using System.Reflection;

internal static class EngineTests
{
    internal static void Run()
    {
        Program.Check(WallpaperEngineProvider.ParseWallpaperPath("debug\nC:\\wall.png") is null, "Engine_RejectsMultilineOutput");
        Program.Check(WallpaperEngineProvider.ParseWallpaperPath("\"C:\\Обои\\my project\\scene.json\"\r\n")
            == "C:\\Обои\\my project\\scene.json", "Engine_UnicodeAndSpaces");
        Program.Check(WallpaperEngineProvider.ParseWallpaperPath("https://example.com/a.jpg") is null, "Engine_RejectsUrl");
        var display = new WallpaperDisplay("a", "device-a", new(0, 0, 1920, 1080));
        Program.Check(new WallpaperEngineProvider().GetCandidateAsync(display, default).GetAwaiter().GetResult() is null,
            "Engine_RejectsUnmappedMonitor");
        string folder = Path.Combine(Path.Combine(Environment.CurrentDirectory, "artifacts"), "glow-test-" + Guid.NewGuid());
        Directory.CreateDirectory(folder);
        try
        {
            string project = Path.Combine(folder, "project.json");
            File.WriteAllText(project, "{\"preview\":\"../other.png\"}");
            Program.Check(WallpaperEngineProvider.FindPreview(project) is null, "Engine_RejectsPreviewOutsideProject");
            File.WriteAllText(project, "{\"preview\":\"missing.jpg\"}");
            Program.Check(WallpaperEngineProvider.FindPreview(project) is null, "Engine_MissingPreviewReturnsNull");
            File.WriteAllText(project, "broken json");
            Program.Check(WallpaperEngineProvider.FindPreview(project) is null, "Engine_CorruptProjectReturnsNull");
        }
        finally { Directory.Delete(folder, true); }
        foreach (string mode in new[] { "timeout", "oversize" })
        {
            Environment.SetEnvironmentVariable("GLOW_TEST_ENGINE_MODE", mode);
            try
            {
                var query = typeof(WallpaperEngineProvider).GetMethod("QueryAsync", BindingFlags.Static | BindingFlags.NonPublic)!;
                var clock = Stopwatch.StartNew();
                var task = (Task<string?>)query.Invoke(null, new object[] { Environment.ProcessPath!, CancellationToken.None })!;
                Program.Check(task.GetAwaiter().GetResult() is null, "Engine_" + mode + "ReturnsNull");
                Program.Check(clock.Elapsed < TimeSpan.FromSeconds(8), "Engine_" + mode + "IsBounded");
            }
            finally { Environment.SetEnvironmentVariable("GLOW_TEST_ENGINE_MODE", null); }
        }
    }
}
