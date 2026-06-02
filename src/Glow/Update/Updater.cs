using System.Diagnostics;
using System.Net.Http;
using System.Reflection;
using System.Text.Json;

namespace Glow.Update;

// Checks GitHub Releases for a newer version and, if the user agrees, downloads
// the installer and runs it silently. The installer closes the running app,
// updates the files and relaunches it.
public static class Updater
{
    private const string LatestReleaseApi = "https://api.github.com/repos/Yadek/glow/releases/latest";

    public sealed record UpdateInfo(Version Version, string Tag, string InstallerUrl);

    public static Version CurrentVersion =>
        Normalize(Assembly.GetExecutingAssembly().GetName().Version ?? new Version(0, 0, 0));

    // Returns the latest release if it's newer than the running build, else null.
    public static async Task<UpdateInfo?> CheckAsync()
    {
        try
        {
            using var http = CreateClient(TimeSpan.FromSeconds(15));
            string json = await http.GetStringAsync(LatestReleaseApi).ConfigureAwait(false);

            using var doc = JsonDocument.Parse(json);
            var root = doc.RootElement;

            Version? latest = ParseVersion(root.GetProperty("tag_name").GetString());
            if (latest is null || latest <= CurrentVersion) return null;

            if (!root.TryGetProperty("assets", out var assets)) return null;
            foreach (var asset in assets.EnumerateArray())
            {
                string? name = asset.GetProperty("name").GetString();
                if (name is not null
                    && name.StartsWith("Glow-Setup", StringComparison.OrdinalIgnoreCase)
                    && name.EndsWith(".exe", StringComparison.OrdinalIgnoreCase))
                {
                    string? url = asset.GetProperty("browser_download_url").GetString();
                    if (url is not null)
                    {
                        return new UpdateInfo(latest, root.GetProperty("tag_name").GetString() ?? latest.ToString(), url);
                    }
                }
            }
            return null;
        }
        catch
        {
            return null; // offline / rate-limited / unexpected response — ignore
        }
    }

    // Downloads the installer to a temp file and returns its path (or null on failure).
    public static async Task<string?> DownloadAsync(UpdateInfo info)
    {
        try
        {
            using var http = CreateClient(TimeSpan.FromMinutes(5));
            byte[] bytes = await http.GetByteArrayAsync(info.InstallerUrl).ConfigureAwait(false);
            string path = Path.Combine(Path.GetTempPath(), $"Glow-Setup-{info.Tag}.exe");
            await File.WriteAllBytesAsync(path, bytes).ConfigureAwait(false);
            return path;
        }
        catch
        {
            return null;
        }
    }

    // Launches the installer silently (UAC may prompt) and quits so files can be replaced.
    public static void RunInstallerAndExit(string installerPath)
    {
        Process.Start(new ProcessStartInfo
        {
            FileName = installerPath,
            Arguments = "/VERYSILENT /SUPPRESSMSGBOXES /NORESTART",
            UseShellExecute = true,
        });
        Application.Exit();
    }

    private static HttpClient CreateClient(TimeSpan timeout)
    {
        var http = new HttpClient { Timeout = timeout };
        http.DefaultRequestHeaders.UserAgent.ParseAdd("Glow-Updater");
        http.DefaultRequestHeaders.Accept.ParseAdd("application/vnd.github+json");
        return http;
    }

    private static Version Normalize(Version v) => new(v.Major, v.Minor, v.Build < 0 ? 0 : v.Build);

    private static Version? ParseVersion(string? tag)
    {
        if (string.IsNullOrWhiteSpace(tag)) return null;
        return Version.TryParse(tag.TrimStart('v', 'V'), out var v) ? Normalize(v) : null;
    }
}
