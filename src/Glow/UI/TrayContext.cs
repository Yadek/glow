using Glow.Localization;
using Glow.Monitors;
using Glow.Startup;

namespace Glow.UI;

// Owns the tray icon, its menu and the popup. No main window, no timers:
// it only does work when clicked, so idle CPU is essentially zero.
public sealed class TrayContext : ApplicationContext
{
    private readonly NotifyIcon _tray;
    private readonly MonitorManager _monitors = new();
    private readonly BrightnessPopup _popup;
    private readonly ToolStripMenuItem _startupItem;

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
