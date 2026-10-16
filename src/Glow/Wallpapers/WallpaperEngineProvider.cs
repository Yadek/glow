using System.Diagnostics;
using System.Text;
using System.Text.Json;

namespace Glow.Wallpapers;

internal sealed class WallpaperEngineProvider
{
    internal async Task<WallpaperCandidate?> GetCandidateAsync(WallpaperDisplay display, CancellationToken token)
    {
        // The documented numeric index is not a Windows display identity. Until
        // a multi-display location mapping is verified, query only a single display.
        if (!display.IsOnlyDisplay) return null;
        foreach (string name in new[] { "wallpaper64", "wallpaper32" })
        {
            foreach (var running in Process.GetProcessesByName(name))
            {
                using (running)
                {
                    try
                    {
                        string? executable = running.MainModule?.FileName;
                        if (executable is null || !File.Exists(executable)) continue;
                        string? output = await QueryAsync(executable, token).ConfigureAwait(false);
                        string? path = output is null ? null : ParseWallpaperPath(output);
                        string? preview = path is null ? null : FindPreview(path);
                        if (preview is not null) return new(preview, WallpaperSource.WallpaperEngine);
                    }
                    catch (Exception e) when (e is System.ComponentModel.Win32Exception or InvalidOperationException or IOException)
                    {
                        // Unavailable/protected/exiting process: use the Windows wallpaper.
                    }
                }
            }
        }
        return null;
    }

    private static async Task<string?> QueryAsync(string executable, CancellationToken token)
    {
        using var timeout = CancellationTokenSource.CreateLinkedTokenSource(token);
        timeout.CancelAfter(TimeSpan.FromSeconds(2));
        using var process = new Process
        {
            StartInfo = new(executable)
            {
                UseShellExecute = false, CreateNoWindow = true,
                RedirectStandardOutput = true, RedirectStandardError = true,
            },
        };
        process.StartInfo.ArgumentList.Add("-control");
        process.StartInfo.ArgumentList.Add("getWallpaper");
        try
        {
            if (!process.Start()) return null;
            var stdout = ReadBoundedAsync(process.StandardOutput, timeout.Token);
            var stderr = ReadBoundedAsync(process.StandardError, timeout.Token);
            await Task.WhenAll(stdout, stderr, process.WaitForExitAsync(timeout.Token)).ConfigureAwait(false);
            return process.ExitCode == 0 ? await stdout.ConfigureAwait(false) : null;
        }
        catch (OperationCanceledException) when (!token.IsCancellationRequested) { return null; }
        catch (IOException) { return null; }
        finally
        {
            try { if (!process.HasExited) process.Kill(); }
            catch (InvalidOperationException) { }
            catch (System.ComponentModel.Win32Exception) { }
        }
    }

    private static async Task<string> ReadBoundedAsync(StreamReader reader, CancellationToken token)
    {
        var text = new StringBuilder();
        var buffer = new char[1024];
        int count;
        while ((count = await reader.ReadAsync(buffer.AsMemory(), token).ConfigureAwait(false)) > 0)
        {
            if (text.Length + count > 65536) throw new IOException("Wallpaper Engine response is too large.");
            text.Append(buffer, 0, count);
        }
        return text.ToString();
    }

    internal static string? ParseWallpaperPath(string output)
    {
        string path = output.Trim().Trim('"');
        if (path.Contains('\n') || path.Contains('\r') || path.StartsWith(@"\\")
            || !Path.IsPathFullyQualified(path) || path.Length > 32767) return null;
        return path;
    }

    internal static string? FindPreview(string wallpaperPath)
    {
        try
        {
            string extension = Path.GetExtension(wallpaperPath).ToLowerInvariant();
            if (extension is ".png" or ".jpg" or ".jpeg" or ".bmp") return File.Exists(wallpaperPath) ? wallpaperPath : null;
            string? folder = Path.GetDirectoryName(wallpaperPath);
            if (folder is null) return null;
            string project = Path.Combine(folder, "project.json");
            var info = new FileInfo(project);
            if (!info.Exists || info.Length > 1024 * 1024) return null;
            using var json = JsonDocument.Parse(File.ReadAllText(project));
            if (!json.RootElement.TryGetProperty("preview", out var property) || property.ValueKind != JsonValueKind.String) return null;
            string? relative = property.GetString();
            if (string.IsNullOrWhiteSpace(relative) || Path.IsPathRooted(relative)) return null;
            string preview = Path.GetFullPath(Path.Combine(folder, relative));
            string root = Path.GetFullPath(folder) + Path.DirectorySeparatorChar;
            if (!preview.StartsWith(root, StringComparison.OrdinalIgnoreCase)) return null;
            return File.Exists(preview) ? preview : null;
        }
        catch (Exception e) when (e is IOException or UnauthorizedAccessException or JsonException or ArgumentException or NotSupportedException)
        { return null; }
    }
}
