using System.Drawing;
using System.Reflection;
using System.Windows.Forms;
using Glow.Monitors;
using Glow.UI;
using Glow.Wallpapers;

internal static class PreviewLifetimeTests
{
    internal static void Run()
    {
        string file = Path.GetFullPath("artifacts/delayed-preview.png");
        using (var bitmap = new Bitmap(80, 45)) bitmap.Save(file);
        using var entered = new ManualResetEventSlim();
        using var release = new ManualResetEventSlim();
        using var service = new WallpaperPreviewService(_ =>
        {
            entered.Set();
            if (!release.Wait(TimeSpan.FromSeconds(5))) throw new TimeoutException("Fixture was not released");
            return new(file, WallpaperSource.Windows);
        }, (_, _) => Task.FromResult<WallpaperCandidate?>(null));
        using var manager = new DisplayManager();
        ((List<DisplayTarget>)manager.Displays).Add(new(new("fixture", "Test", "Test", IntPtr.Zero,
            "fixture", new Rectangle(0, 0, 1920, 1080)), null));
        using var popup = new BrightnessPopup(manager);
        Field(popup, "_wallpapers").SetValue(popup, service);
        Build(popup);
        popup.ClientSize = new Size(320, 300);
        popup.Location = new Point(-32000, -32000);
        popup.TopMost = false;
        popup.Show();
        var old = popup.Controls.Cast<Control>().SelectMany(c => c.Controls.OfType<MonitorPreview>()).Single();
        int generation = (int)Field(popup, "_previewGeneration").GetValue(popup)!;
        var token = ((CancellationTokenSource)Field(popup, "_previewCancellation").GetValue(popup)!).Token;
        var task = (Task)typeof(BrightnessPopup).GetMethod("LoadPreviewAsync", BindingFlags.Instance | BindingFlags.NonPublic)!
            .Invoke(popup, new object[] { old, new WallpaperDisplay("fixture", "fixture", new(0, 0, 1920, 1080)), generation, token })!;
        Program.Check(entered.Wait(TimeSpan.FromSeconds(5)), "Preview_DelayedProviderStarted");
        popup.HideAnimated();
        Build(popup);
        var current = popup.Controls.Cast<Control>().SelectMany(c => c.Controls.OfType<MonitorPreview>()).Single();
        popup.Show();
        release.Set();
        var deadline = DateTime.UtcNow.AddSeconds(5);
        while (!task.IsCompleted && DateTime.UtcNow < deadline) { Application.DoEvents(); Thread.Sleep(5); }
        Program.Check(task.IsCompletedSuccessfully, "Preview_DelayedCloseReopenFinishes");
        Program.Check(old.IsDisposed, "Preview_OldCardDisposed");
        Program.Check(Field(old, "_preview").GetValue(old) is null, "Preview_OldGenerationDoesNotRetainBitmap");
        Program.Check(Field(current, "_preview").GetValue(current) is null, "Preview_OldGenerationCannotUpdateNewCard");
    }

    private static FieldInfo Field(object target, string name)
        => target.GetType().GetField(name, BindingFlags.Instance | BindingFlags.NonPublic)!;
    private static void Build(BrightnessPopup popup)
        => typeof(BrightnessPopup).GetMethod("BuildContent", BindingFlags.Instance | BindingFlags.NonPublic)!.Invoke(popup, null);
}
