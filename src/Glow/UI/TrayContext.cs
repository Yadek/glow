using Glow.Localization;
using Glow.Monitors;
using Glow.Startup;
using Glow.Update;

namespace Glow.UI;

// Owns the tray icon, its menu and the popup. No main window, no timers:
// it only does work when clicked, so idle CPU is essentially zero.
public sealed class TrayContext : ApplicationContext
{
    private readonly NotifyIcon _tray;
    private readonly MonitorManager _monitors = new();
    private readonly BrightnessPopup _popup;
    private readonly ToolStripMenuItem _startupItem;
    private bool _updateChecking;

    public TrayContext()
    {
        _popup = new BrightnessPopup(_monitors);

        var menu = new ContextMenuStrip();

        _startupItem = new ToolStripMenuItem(Strings.RunAtStartup, null, OnToggleStartup)
        {
            Checked = StartupManager.IsEnabled(),
            CheckOnClick = true,
        };
        menu.Items.Add(_startupItem);
        menu.Items.Add(new ToolStripMenuItem(Strings.CheckUpdates, null, async (_, _) => await CheckForUpdatesAsync(manual: true)));
        menu.Items.Add(new ToolStripSeparator());
        menu.Items.Add(new ToolStripMenuItem(Strings.Exit, null, OnExit));

        _tray = new NotifyIcon
        {
            Icon = GlowIcon.Create(SystemInformation.SmallIconSize.Width <= 16 ? 16 : 32),
            Text = Strings.TrayTooltip,
            Visible = true,
            ContextMenuStrip = menu,
        };
        _tray.MouseClick += OnTrayClick;

        ScheduleStartupUpdateCheck();
    }

    private void OnTrayClick(object? sender, MouseEventArgs e)
    {
        if (e.Button != MouseButtons.Left) return;

        if (_popup.Visible)
        {
            _popup.Hide();
        }
        else
        {
            _popup.ShowNearTray();
        }
    }

    private void OnToggleStartup(object? sender, EventArgs e)
    {
        StartupManager.SetEnabled(_startupItem.Checked);
        _startupItem.Checked = StartupManager.IsEnabled();
    }

    // Check once a few seconds after launch, on the UI thread (message pump running).
    private void ScheduleStartupUpdateCheck()
    {
        var timer = new System.Windows.Forms.Timer { Interval = 4000 };
        timer.Tick += async (_, _) =>
        {
            timer.Stop();
            timer.Dispose();
            await CheckForUpdatesAsync(manual: false);
        };
        timer.Start();
    }

    private async Task CheckForUpdatesAsync(bool manual)
    {
        if (_updateChecking) return;
        _updateChecking = true;
        try
        {
            var info = await Updater.CheckAsync();
            if (info is null)
            {
                if (manual)
                {
                    MessageBox.Show(Strings.UpToDate, Strings.TrayTooltip,
                        MessageBoxButtons.OK, MessageBoxIcon.Information);
                }
                return;
            }

            var answer = MessageBox.Show(Strings.UpdateAvailable(info.Tag), Strings.TrayTooltip,
                MessageBoxButtons.YesNo, MessageBoxIcon.Information);
            if (answer != DialogResult.Yes) return;

            string? installer = await Updater.DownloadAsync(info);
            if (installer is null)
            {
                MessageBox.Show(Strings.UpdateDownloadFailed, Strings.TrayTooltip,
                    MessageBoxButtons.OK, MessageBoxIcon.Warning);
                return;
            }

            _tray.Visible = false;
            Updater.RunInstallerAndExit(installer);
        }
        finally
        {
            _updateChecking = false;
        }
    }

    private void OnExit(object? sender, EventArgs e)
    {
        _tray.Visible = false;
        ExitThread();
    }

    protected override void Dispose(bool disposing)
    {
        if (disposing)
        {
            _tray.Dispose();
            _popup.Dispose();
            _monitors.Dispose();
        }
        base.Dispose(disposing);
    }
}
