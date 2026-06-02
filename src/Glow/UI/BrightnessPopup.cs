using System.Drawing;
using System.Drawing.Drawing2D;
using Glow.Localization;
using Glow.Monitors;

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
        Top = area.Bottom - Height - S(12);

        Show();
        Activate();
    }

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

        y += S(LPadBottom);
        ResumeLayout();
        return y;
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
        Hide(); // dismiss when the user clicks elsewhere
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
}
