using Glow.Localization;
using Glow.Monitors;
using Glow.NightShift;
using Glow.Settings;
using Glow.Startup;
using Glow.Update;

namespace Glow.UI;

// Owns the tray icon, its menu and the popup. No main window, no timers while idle:
// it only does work when clicked, so idle CPU is essentially zero. (The optional
// tray-icon animation adds a light timer, but only while the user enables it.)
public sealed class TrayContext : ApplicationContext
{
    private readonly NotifyIcon _tray;
    private readonly MonitorManager _monitors = new();
    private readonly BrightnessPopup _popup;
    private readonly ToolStripMenuItem _startupItem;
    private readonly ToolStripMenuItem _animateItem;
    private readonly int _iconSize;

    private System.Windows.Forms.Timer? _iconAnim;
    private double _iconPhase;
    private bool _updateChecking;

    public TrayContext()
    {
        _popup = new BrightnessPopup(_monitors);
        _iconSize = SystemInformation.SmallIconSize.Width <= 16 ? 16 : 32;

        var menu = new ContextMenuStrip();

        _startupItem = new ToolStripMenuItem(Strings.RunAtStartup, null, OnToggleStartup)
        {
            Checked = StartupManager.IsEnabled(),
            CheckOnClick = true,
        };
        _animateItem = new ToolStripMenuItem(Strings.AnimateIcon, null, OnToggleAnimation)
        {
            Checked = AppSettings.AnimateTrayIcon,
            CheckOnClick = true,
        };

        menu.Items.Add(_startupItem);
        menu.Items.Add(_animateItem);
        menu.Items.Add(new ToolStripMenuItem(Strings.CheckUpdates, null, async (_, _) => await CheckForUpdatesAsync(manual: true)));
        menu.Items.Add(new ToolStripSeparator());
        menu.Items.Add(new ToolStripMenuItem(Strings.Exit, null, OnExit));

        _tray = new NotifyIcon
        {
            Icon = GlowIcon.Create(_iconSize),
            Text = Strings.TrayTooltip,
            Visible = true,
            ContextMenuStrip = menu,
        };
        _tray.MouseClick += OnTrayClick;

        if (AppSettings.AnimateTrayIcon)
        {
            StartIconAnimation();
        }

        NightLight.Initialize(); // re-apply saved night mode (gamma resets each session)
        ScheduleStartupUpdateCheck();
    }

    private void OnTrayClick(object? sender, MouseEventArgs e)
    {
        if (e.Button == MouseButtons.Left)
        {
            _popup.ToggleFromTray();
        }
    }

    private void OnToggleStartup(object? sender, EventArgs e)
    {
        StartupManager.SetEnabled(_startupItem.Checked);
        _startupItem.Checked = StartupManager.IsEnabled();
    }

    // ----- animated tray icon -----

    private void OnToggleAnimation(object? sender, EventArgs e)
    {
        AppSettings.AnimateTrayIcon = _animateItem.Checked;
        if (_animateItem.Checked)
        {
            StartIconAnimation();
        }
        else
        {
            StopIconAnimation();
        }
    }

    private void StartIconAnimation()
    {
        _iconAnim ??= new System.Windows.Forms.Timer { Interval = 70 };
        _iconAnim.Tick -= OnIconAnimTick;
        _iconAnim.Tick += OnIconAnimTick;
        _iconAnim.Start();
    }

    private void StopIconAnimation()
    {
        _iconAnim?.Stop();
        SetTrayIcon(GlowIcon.Create(_iconSize));
    }

    private void OnIconAnimTick(object? sender, EventArgs e)
    {
        _iconPhase += 0.08;
        SetTrayIcon(GlowIcon.CreateGradient(_iconSize, _iconPhase));
    }

    // Swap the icon and dispose the previous one to free its GDI handle.
    private void SetTrayIcon(Icon icon)
    {
        var old = _tray.Icon;
        _tray.Icon = icon;
        old?.Dispose();
    }

    // ----- updates -----

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
        NightLight.RestoreNeutral(); // don't leave the screen tinted after we quit
        ExitThread();
    }

    protected override void Dispose(bool disposing)
    {
        if (disposing)
        {
            _iconAnim?.Dispose();
            _tray.Icon?.Dispose();
            _tray.Dispose();
            _popup.Dispose();
            _monitors.Dispose();
        }
        base.Dispose(disposing);
    }
}
