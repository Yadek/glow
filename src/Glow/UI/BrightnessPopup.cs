using System.Drawing;
using System.Drawing.Drawing2D;
using Glow.Localization;
using Glow.Monitors;
using Glow.NightShift;

namespace Glow.UI;

// Frameless popup shown on tray click: one card with a slider per monitor.
// Layout is explicit and DPI-scaled. Hidden (not closed) when it loses focus.
public sealed class BrightnessPopup : Form
{
    private readonly MonitorManager _monitors;

    // Logical sizes (@96 DPI); multiplied by the per-monitor scale at build time.
    private const int LWidth = 296;
    private const int LPadX = 14;
    private const int LPadTop = 12;
    private const int LPadBottom = 14;
    private const int LTitleH = 22;
    private const int LCardH = 70;
    private const int LCardGap = 8;
    private const int LCardRadius = 12;
    private const int LCardInsetX = 14;
    private const int LSliderH = 20;

    // Softer, lower-contrast palette: cards sit just barely above the background.
    private static readonly Color FormBg = Color.FromArgb(32, 32, 36);
    private static readonly Color CardColor = Color.FromArgb(43, 43, 49);
    private static readonly Color TextColor = Color.FromArgb(228, 228, 232);
    private static readonly Color SubtleColor = Color.FromArgb(138, 138, 148);

    private float _scale = 1f;

    // Fade + slide animation state.
    private const int SlideOffset = 14;       // logical px the popup rises while fading in
    private const double AnimDurationMs = 140;
    private readonly System.Windows.Forms.Timer _anim = new() { Interval = 10 };
    private double _animProgress;             // 0 (hidden) .. 1 (shown)
    private int _animDir;                     // +1 fading in, -1 fading out
    private int _finalTop;
    private DateTime _lastHide = DateTime.MinValue;

    public BrightnessPopup(MonitorManager monitors)
    {
        _monitors = monitors;

        FormBorderStyle = FormBorderStyle.None;
        ShowInTaskbar = false;
        StartPosition = FormStartPosition.Manual;
        AutoScaleMode = AutoScaleMode.None; // we scale manually & predictably
        BackColor = FormBg;
        TopMost = true;
        DoubleBuffered = true;
        Font = new Font("Segoe UI", 9f);

        _anim.Tick += OnAnimTick;
    }

    // Toggle from a tray click: open if closed, close if open. The short guard
    // swallows the click that arrives right after the popup auto-hid on focus
    // loss, so clicking the icon while it's open closes it (and keeps it closed).
    public void ToggleFromTray()
    {
        if (Visible)
        {
            HideAnimated();
            return;
        }
        if ((DateTime.UtcNow - _lastHide).TotalMilliseconds < 300) return;
        ShowNearTray();
    }

    private int S(int logical) => (int)Math.Round(logical * _scale);

    private Font MakeFont(float px, FontStyle style = FontStyle.Regular)
        => new("Segoe UI", px * _scale, style, GraphicsUnit.Pixel);

    // Re-enumerate monitors, rebuild, position bottom-right and show.
    public void ShowNearTray()
    {
        // Ensure a handle so DeviceDpi is valid, then scale to this monitor's DPI.
        _ = Handle;
        _scale = DeviceDpi / 96f;

        _monitors.Refresh();
        int height = BuildContent();

        Width = S(LWidth);
        Height = height;

        var area = Screen.PrimaryScreen?.WorkingArea ?? Screen.GetWorkingArea(this);
        Left = area.Right - Width - S(12);
        _finalTop = area.Bottom - Height - S(12);

        // Start transparent and slightly lower, then fade + slide up.
        Opacity = 0;
        Top = _finalTop + S(SlideOffset);
        _animProgress = 0;
        _animDir = 1;

        Show();
        Activate();
        _anim.Start();
    }

    // Fade out, then actually hide once fully transparent.
    public void HideAnimated()
    {
        if (!Visible) return;
        if (_animDir < 0 && _anim.Enabled) return; // already fading out
        if (_animProgress <= 0) _animProgress = 1;  // shown instantly -> start from full
        _animDir = -1;
        _anim.Start();
    }

    private void OnAnimTick(object? sender, EventArgs e)
    {
        _animProgress += _animDir * (_anim.Interval / AnimDurationMs);

        if (_animDir < 0 && _animProgress <= 0)
        {
            _animProgress = 0;
            _anim.Stop();
            Opacity = 0;
            _lastHide = DateTime.UtcNow;
            base.Hide();
            return;
        }
        if (_animDir > 0 && _animProgress >= 1)
        {
            _animProgress = 1;
            _anim.Stop();
        }

        double eased = EaseOutCubic(Math.Clamp(_animProgress, 0, 1));
        Opacity = eased;
        Top = _finalTop + (int)Math.Round((1 - eased) * S(SlideOffset));
    }

    private static double EaseOutCubic(double t) => 1 - Math.Pow(1 - t, 3);

    // Builds the controls and returns the total form height in device px.
    private int BuildContent()
    {
        SuspendLayout();
        DisposeChildren();
        Controls.Clear();

        int contentWidth = S(LWidth) - S(LPadX) * 2;
        int y = S(LPadTop);

        Controls.Add(new Label
        {
            Text = Strings.Title,
            ForeColor = TextColor,
            BackColor = FormBg,
            Font = MakeFont(14f, FontStyle.Bold),
            AutoSize = false,
            TextAlign = ContentAlignment.MiddleLeft,
            Bounds = new Rectangle(S(LPadX) + S(4), y, contentWidth, S(LTitleH)),
        });
        y += S(LTitleH) + S(6);

        var list = _monitors.Monitors;
        if (list.Count == 0)
        {
            Controls.Add(new Label
            {
                Text = Strings.NoMonitors,
                ForeColor = SubtleColor,
                BackColor = FormBg,
                Font = MakeFont(11.5f),
                AutoSize = false,
                TextAlign = ContentAlignment.MiddleLeft,
                Bounds = new Rectangle(S(LPadX) + S(4), y, contentWidth, S(40)),
            });
            y += S(40);
        }
        else
        {
            for (int i = 0; i < list.Count; i++)
            {
                var card = BuildMonitorCard(list[i], i + 1, new Rectangle(S(LPadX), y, contentWidth, S(LCardH)));
                Controls.Add(card);
                y += S(LCardH) + S(LCardGap);
            }
            y -= S(LCardGap); // no gap after the last card
        }

        y += S(LCardGap);
        Controls.Add(BuildNightLightCard(new Rectangle(S(LPadX), y, contentWidth, S(LCardH))));
        y += S(LCardH);

        y += S(LPadBottom);
        ResumeLayout();
        return y;
    }

    private RoundedPanel BuildNightLightCard(Rectangle bounds)
    {
        var card = new RoundedPanel
        {
            Bounds = bounds,
            BackColor = FormBg,
            FillColor = CardColor,
            CornerRadius = S(LCardRadius),
        };

        int innerX = S(LCardInsetX);
        int innerW = card.Width - innerX * 2;
        int pillW = S(52), pillH = S(22);

        var title = new Label
        {
            Text = Strings.NightLight,
            ForeColor = TextColor,
            BackColor = CardColor,
            Font = MakeFont(12f),
            AutoSize = false,
            TextAlign = ContentAlignment.MiddleLeft,
            Bounds = new Rectangle(innerX, S(9), innerW - pillW - S(6), S(20)),
        };

        var pill = new RoundedPanel
        {
            BackColor = CardColor,
            CornerRadius = pillH / 2,
            Font = MakeFont(10.5f, FontStyle.Bold),
            Cursor = Cursors.Hand,
            Bounds = new Rectangle(innerX + innerW - pillW, S(8), pillW, pillH),
        };

        var slider = new BrightnessSlider
        {
            Value = Math.Clamp(NightLight.GetIntensity(), 0, 100),
            BackColor = CardColor,
            Bounds = new Rectangle(innerX, S(38), innerW, S(LSliderH)),
        };

        void RefreshPill(bool on)
        {
            pill.Text = on ? Strings.On : Strings.Off;
            pill.FillColor = on ? Theme.AccentColor() : Color.FromArgb(72, 72, 80);
            pill.ForeColor = on ? Color.White : SubtleColor;
            pill.Invalidate();
        }
        RefreshPill(NightLight.IsEnabled());

        pill.Click += (_, _) =>
        {
            NightLight.SetEnabled(!NightLight.IsEnabled());
            RefreshPill(NightLight.IsEnabled());
        };

        slider.ValueChanged += (_, _) => NightLight.SetIntensity(slider.Value); // live

        card.Controls.Add(title);
        card.Controls.Add(pill);
        card.Controls.Add(slider);
        return card;
    }

    private RoundedPanel BuildMonitorCard(BrightnessMonitor monitor, int index, Rectangle bounds)
    {
        var card = new RoundedPanel
        {
            Bounds = bounds,
            BackColor = FormBg,     // corners blend into the form
            FillColor = CardColor,
            CornerRadius = S(LCardRadius),
        };

        int innerX = S(LCardInsetX);
        int innerW = card.Width - innerX * 2;

        var name = new Label
        {
            Text = string.IsNullOrWhiteSpace(monitor.Name) ? Strings.Display(index) : monitor.Name,
            ForeColor = TextColor,
            BackColor = CardColor,
            Font = MakeFont(12f),
            AutoSize = false,
            AutoEllipsis = true,
            TextAlign = ContentAlignment.MiddleLeft,
            Bounds = new Rectangle(innerX, S(10), innerW - S(46), S(18)),
        };

        var percent = new Label
        {
            Text = monitor.Percent + "%",
            ForeColor = SubtleColor,
            BackColor = CardColor,
            Font = MakeFont(12f),
            AutoSize = false,
            TextAlign = ContentAlignment.MiddleRight,
            Bounds = new Rectangle(innerX + innerW - S(46), S(10), S(46), S(18)),
        };

        var slider = new BrightnessSlider
        {
            Value = Math.Clamp(monitor.Percent, 0, 100),
            BackColor = CardColor,
            Bounds = new Rectangle(innerX, S(38), innerW, S(LSliderH)),
        };
        slider.ValueChanged += (_, _) =>
        {
            percent.Text = slider.Value + "%";   // instant, on the UI thread
            monitor.RequestPercent(slider.Value); // throttled hardware write off-thread
        };

        card.Controls.Add(name);
        card.Controls.Add(percent);
        card.Controls.Add(slider);
        return card;
    }

    private void DisposeChildren()
    {
        for (int i = Controls.Count - 1; i >= 0; i--)
        {
            Controls[i].Dispose();
        }
    }

    protected override void OnDeactivate(EventArgs e)
    {
        base.OnDeactivate(e);
        _lastHide = DateTime.UtcNow; // mark now so a tray click doesn't reopen
        HideAnimated();              // fade out when the user clicks elsewhere
    }

    protected override CreateParams CreateParams
    {
        get
        {
            const int CS_DROPSHADOW = 0x00020000;
            var cp = base.CreateParams;
            cp.ClassStyle |= CS_DROPSHADOW;
            return cp;
        }
    }

    protected override void OnHandleCreated(EventArgs e)
    {
        base.OnHandleCreated(e);
        // Smooth, OS-native rounded corners on Windows 11 (ignored on Windows 10).
        const int DWMWA_WINDOW_CORNER_PREFERENCE = 33;
        const int DWMWCP_ROUND = 2;
        int pref = DWMWCP_ROUND;
        try { DwmSetWindowAttribute(Handle, DWMWA_WINDOW_CORNER_PREFERENCE, ref pref, sizeof(int)); }
        catch { /* dwmapi unavailable */ }
    }

    [System.Runtime.InteropServices.DllImport("dwmapi.dll")]
    private static extern int DwmSetWindowAttribute(IntPtr hwnd, int attr, ref int value, int size);

    protected override void Dispose(bool disposing)
    {
        if (disposing)
        {
            _anim.Dispose();
        }
        base.Dispose(disposing);
    }
}
