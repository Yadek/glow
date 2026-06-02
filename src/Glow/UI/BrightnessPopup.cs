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

    // Debounce night-light intensity writes while dragging the slider.
    private readonly System.Windows.Forms.Timer _nlApply = new() { Interval = 120 };
    private int _nlPending = -1;

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
        _nlApply.Tick += (_, _) =>
        {
            _nlApply.Stop();
            if (_nlPending >= 0) NightLight.SetIntensity(_nlPending);
        };
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
        ApplyRoundedRegion();

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
            BackColor = Color.Transparent,
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
                BackColor = Color.Transparent,
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

        if (NightLight.IsSupported)
        {
            y += S(LCardGap);
            Controls.Add(BuildNightLightCard(new Rectangle(S(LPadX), y, contentWidth, S(LCardH))));
            y += S(LCardH);
        }

        y += S(LPadBottom);
        ResumeLayout();
        return y;
    }

    private Panel BuildNightLightCard(Rectangle bounds)
    {
        var card = new Panel { Bounds = bounds, BackColor = CardColor };
        card.Paint += (_, e) =>
        {
            e.Graphics.SmoothingMode = SmoothingMode.AntiAlias;
            using var brush = new SolidBrush(CardColor);
            using var path = RoundedPath(new Rectangle(0, 0, card.Width, card.Height), S(LCardRadius));
            e.Graphics.FillPath(brush, path);
        };
        card.Region = new Region(RoundedPath(new Rectangle(0, 0, card.Width, card.Height), S(LCardRadius)));

        int innerX = S(LCardInsetX);
        int innerW = card.Width - innerX * 2;
        int pillW = S(52), pillH = S(20);

        var title = new Label
        {
            Text = Strings.NightLight,
            ForeColor = TextColor,
            BackColor = Color.Transparent,
            Font = MakeFont(12f),
            AutoSize = false,
            TextAlign = ContentAlignment.MiddleLeft,
            Bounds = new Rectangle(innerX, S(10), innerW - pillW - S(6), S(18)),
        };

        var pill = new Label
        {
            AutoSize = false,
            TextAlign = ContentAlignment.MiddleCenter,
            Font = MakeFont(10.5f, FontStyle.Bold),
            Cursor = Cursors.Hand,
            Bounds = new Rectangle(innerX + innerW - pillW, S(9), pillW, pillH),
        };
        pill.Region = new Region(RoundedPath(new Rectangle(0, 0, pillW, pillH), pillH / 2));

        var slider = new BrightnessSlider
        {
            Value = Math.Clamp(NightLight.GetIntensity(), 0, 100),
            Bounds = new Rectangle(innerX, S(38), innerW, S(LSliderH)),
        };

        void RefreshPill(bool on)
        {
            pill.Text = on ? Strings.On : Strings.Off;
            pill.BackColor = on ? Theme.AccentColor() : Color.FromArgb(70, 70, 78);
            pill.ForeColor = on ? Color.White : SubtleColor;
        }
        RefreshPill(NightLight.IsEnabled());

        pill.Click += (_, _) =>
        {
            NightLight.SetEnabled(!NightLight.IsEnabled());
            RefreshPill(NightLight.IsEnabled());
        };

        slider.ValueChanged += (_, _) =>
        {
            _nlPending = slider.Value;
            _nlApply.Stop();
            _nlApply.Start(); // apply shortly after the user stops dragging
        };

        card.Controls.Add(title);
        card.Controls.Add(pill);
        card.Controls.Add(slider);
        return card;
    }

    private Panel BuildMonitorCard(BrightnessMonitor monitor, int index, Rectangle bounds)
    {
        var card = new Panel
        {
            Bounds = bounds,
            BackColor = CardColor,
        };
        card.Paint += (_, e) =>
        {
            e.Graphics.SmoothingMode = SmoothingMode.AntiAlias;
            using var brush = new SolidBrush(CardColor);
            using var path = RoundedPath(new Rectangle(0, 0, card.Width, card.Height), S(LCardRadius));
            e.Graphics.FillPath(brush, path);
        };
        card.Region = new Region(RoundedPath(new Rectangle(0, 0, card.Width, card.Height), S(LCardRadius)));

        int innerX = S(LCardInsetX);
        int innerW = card.Width - innerX * 2;

        var name = new Label
        {
            Text = string.IsNullOrWhiteSpace(monitor.Name) ? Strings.Display(index) : monitor.Name,
            ForeColor = TextColor,
            BackColor = Color.Transparent,
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
            BackColor = Color.Transparent,
            Font = MakeFont(12f),
            AutoSize = false,
            TextAlign = ContentAlignment.MiddleRight,
            Bounds = new Rectangle(innerX + innerW - S(46), S(10), S(46), S(18)),
        };

        var slider = new BrightnessSlider
        {
            Value = Math.Clamp(monitor.Percent, 0, 100),
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

    private void ApplyRoundedRegion()
    {
        using var path = RoundedPath(new Rectangle(0, 0, Width, Height), S(14));
        Region = new Region(path);
    }

    private static GraphicsPath RoundedPath(Rectangle rect, int radius)
    {
        int d = Math.Max(1, radius * 2);
        var path = new GraphicsPath();
        path.AddArc(rect.X, rect.Y, d, d, 180, 90);
        path.AddArc(rect.Right - d, rect.Y, d, d, 270, 90);
        path.AddArc(rect.Right - d, rect.Bottom - d, d, d, 0, 90);
        path.AddArc(rect.X, rect.Bottom - d, d, d, 90, 90);
        path.CloseFigure();
        return path;
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

    protected override void Dispose(bool disposing)
    {
        if (disposing)
        {
            _anim.Dispose();
            _nlApply.Dispose();
        }
        base.Dispose(disposing);
    }
}
