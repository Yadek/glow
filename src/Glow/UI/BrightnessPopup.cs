using System.Drawing;
using System.Runtime.InteropServices;
using Glow.Localization;
using Glow.Monitors;
using Glow.Native;
using Glow.NightShift;

namespace Glow.UI;

// Frameless popup shown on tray click.
//
// Layout: an optional "all monitors" master card (only with more than one
// display), then one card per display. Each card has a sun row (hardware
// brightness over DDC/CI) and a moon row (night mode over gamma). Displays
// without DDC/CI still get a card, because night mode works on them.
//
// Everything is laid out explicitly and scaled by the DPI of the screen the
// popup is about to appear on. Hidden (not closed) when it loses focus.
public sealed class BrightnessPopup : Form
{
    private readonly DisplayManager _displays;

    // Logical sizes (@96 DPI); multiplied by the target screen's scale at build time.
    private const int LWidth = 320;
    private const int LPadX = 14;
    private const int LPadTop = 12;
    private const int LPadBottom = 14;
    private const int LTitleH = 22;
    private const int LCardGap = 8;
    private const int LCardRadius = 12;
    private const int LCardInsetX = 14;
    private const int LCardPadTop = 10;
    private const int LHeaderH = 18;
    private const int LRowGap = 8;
    private const int LRowH = 22;
    private const int LCardPadBottom = 12;
    private const int LGlyphW = 18;
    private const int LGlyphGap = 8;
    private const int LPillW = 52;
    private const int LPillH = 22;
    private const int LPercentW = 46;
    private const int LMargin = 12;

    // Night rows use a warm amber instead of the accent colour, so the two rows
    // read as different things at a glance.
    private static readonly Color NightFill = Color.FromArgb(232, 152, 74);

    private float _scale = 1f;
    private readonly Dictionary<(float Size, FontStyle Style), Font> _fonts = new();

    // Live references to the built controls, so master and per-display controls
    // can keep each other in sync without a full rebuild.
    private sealed class DisplayRow
    {
        public required DisplayTarget Target;
        public BrightnessSlider? Brightness;
        public Label? PercentLabel;
        public required BrightnessSlider Night;
        public required RoundedPanel NightPill;
    }

    private readonly List<DisplayRow> _rows = new();
    private BrightnessSlider? _masterBrightness;
    private Label? _masterPercent;
    private BrightnessSlider? _masterNight;
    private RoundedPanel? _masterPill;

    // Fade + slide animation state.
    private const int SlideOffset = 14;       // logical px the popup rises while fading in
    private const double AnimDurationMs = 140;
    private readonly System.Windows.Forms.Timer _anim = new() { Interval = 10 };
    private double _animProgress;             // 0 (hidden) .. 1 (shown)
    private int _animDir;                     // +1 fading in, -1 fading out
    private int _finalTop;
    private DateTime _lastHide = DateTime.MinValue;

    public BrightnessPopup(DisplayManager displays)
    {
        _displays = displays;

        FormBorderStyle = FormBorderStyle.None;
        ShowInTaskbar = false;
        StartPosition = FormStartPosition.Manual;
        AutoScaleMode = AutoScaleMode.None; // we scale manually & predictably
        BackColor = Theme.FormBg;
        TopMost = true;
        DoubleBuffered = true;
        KeyPreview = true;

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
    {
        var key = (px, style);
        if (!_fonts.TryGetValue(key, out var font))
        {
            font = new Font("Segoe UI", px * _scale, style, GraphicsUnit.Pixel);
            _fonts[key] = font;
        }
        return font;
    }

    // Fonts are cached per scale; a DPI change invalidates every one of them.
    private void ResetFonts()
    {
        foreach (var font in _fonts.Values)
        {
            font.Dispose();
        }
        _fonts.Clear();
    }

    /// <summary>Re-enumerates displays, rebuilds, positions near the tray and shows.</summary>
    public void ShowNearTray()
    {
        Theme.Refresh();

        // Scale to the DPI of the screen the popup will actually appear on. Reading
        // DeviceDpi instead would give the DPI of wherever the window currently is
        // (the primary screen), which is wrong on mixed-DPI setups.
        Point cursor = Cursor.Position;
        Screen screen = Screen.FromPoint(cursor);
        float scale = ScaleForPoint(cursor);
        if (Math.Abs(scale - _scale) > 0.001f)
        {
            _scale = scale;
            ResetFonts();
        }

        BackColor = Theme.FormBg;
        ApplyWindowAttributes();

        _displays.Refresh();
        NightMode.Reapply(); // picks up hot-plugged displays and their saved settings

        int height = BuildContent();

        Rectangle work = screen.WorkingArea;
        int margin = S(LMargin);
        int maxHeight = work.Height - margin * 2;

        Width = S(LWidth);
        if (height > maxHeight)
        {
            // More displays than fit on screen — scroll rather than run off the edge.
            Height = maxHeight;
            AutoScroll = true;
        }
        else
        {
            Height = height;
            AutoScroll = false;
        }

        (Left, _finalTop) = AnchorPosition(screen, margin);

        // Start transparent and slightly lower, then fade + slide up.
        Opacity = 0;
        Top = _finalTop + S(SlideOffset);
        _animProgress = 0;
        _animDir = 1;

        Show();
        Activate();
        _anim.Start();
    }

    // Places the popup in the corner the tray actually lives in, on the screen the
    // click came from — not always the bottom-right of the primary screen.
    private (int Left, int Top) AnchorPosition(Screen screen, int margin)
    {
        Rectangle work = screen.WorkingArea;
        Rectangle bounds = screen.Bounds;

        int left = work.Right - Width - margin;
        int top = work.Bottom - Height - margin;

        if (work.Top > bounds.Top)          top = work.Top + margin;      // taskbar on top
        else if (work.Left > bounds.Left)   left = work.Left + margin;    // taskbar on the left

        left = Math.Clamp(left, work.Left + margin, Math.Max(work.Left + margin, work.Right - Width - margin));
        top = Math.Clamp(top, work.Top + margin, Math.Max(work.Top + margin, work.Bottom - Height - margin));
        return (left, top);
    }

    private static float ScaleForPoint(Point point)
    {
        try
        {
            var pt = new NativeMethods.POINT { X = point.X, Y = point.Y };
            IntPtr monitor = NativeMethods.MonitorFromPoint(pt, NativeMethods.MONITOR_DEFAULTTONEAREST);
            if (monitor != IntPtr.Zero
                && NativeMethods.GetDpiForMonitor(monitor, NativeMethods.MDT_EFFECTIVE_DPI, out uint dpiX, out _) == 0
                && dpiX > 0)
            {
                return dpiX / 96f;
            }
        }
        catch (DllNotFoundException)
        {
            // shcore.dll is Windows 8.1+; fall through to no scaling.
        }
        return 1f;
    }

    /// <summary>Fades out, then actually hides once fully transparent.</summary>
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

    // ----- content -----

    // Builds the controls and returns the total form height in device px.
    private int BuildContent()
    {
        SuspendLayout();
        DisposeChildren();
        Controls.Clear();
        _rows.Clear();
        _masterBrightness = null;
        _masterPercent = null;
        _masterNight = null;
        _masterPill = null;

        int contentWidth = S(LWidth) - S(LPadX) * 2;
        int y = S(LPadTop);

        Controls.Add(new Label
        {
            Text = Strings.Title,
            ForeColor = Theme.Text,
            BackColor = Theme.FormBg,
            Font = MakeFont(14f, FontStyle.Bold),
            AutoSize = false,
            TextAlign = ContentAlignment.MiddleLeft,
            Bounds = new Rectangle(S(LPadX) + S(4), y, contentWidth, S(LTitleH)),
        });
        y += S(LTitleH) + S(6);

        var displays = _displays.Displays;
        if (displays.Count == 0)
        {
            Controls.Add(new Label
            {
                Text = Strings.NoMonitors,
                ForeColor = Theme.Subtle,
                BackColor = Theme.FormBg,
                Font = MakeFont(11.5f),
                AutoSize = false,
                TextAlign = ContentAlignment.MiddleLeft,
                Bounds = new Rectangle(S(LPadX) + S(4), y, contentWidth, S(40)),
            });
            return y + S(40) + S(LPadBottom);
        }

        // The master card only earns its space when there's more than one display.
        if (displays.Count > 1)
        {
            bool anyBrightness = displays.Any(d => d.SupportsBrightness);
            int h = CardHeight(anyBrightness);
            Controls.Add(BuildMasterCard(new Rectangle(S(LPadX), y, contentWidth, h), anyBrightness));
            y += h + S(LCardGap);
        }

        foreach (var display in displays)
        {
            int h = CardHeight(display.SupportsBrightness);
            Controls.Add(BuildDisplayCard(display, new Rectangle(S(LPadX), y, contentWidth, h)));
            y += h + S(LCardGap);
        }
        y -= S(LCardGap); // no gap after the last card

        RefreshNightVisuals();

        ResumeLayout();
        return y + S(LPadBottom);
    }

    private int CardHeight(bool withBrightnessRow)
    {
        int rows = withBrightnessRow ? 2 : 1;
        return S(LCardPadTop) + S(LHeaderH)
             + rows * (S(LRowGap) + S(LRowH))
             + S(LCardPadBottom);
    }

    private RoundedPanel NewCard(Rectangle bounds) => new()
    {
        Bounds = bounds,
        BackColor = Theme.FormBg,   // corners blend into the form
        FillColor = Theme.CardBg,
        CornerRadius = S(LCardRadius),
    };

    private Label NewHeaderLabel(string text, int x, int y, int width) => new()
    {
        Text = text,
        ForeColor = Theme.Text,
        BackColor = Theme.CardBg,
        Font = MakeFont(12f),
        AutoSize = false,
        AutoEllipsis = true,
        TextAlign = ContentAlignment.MiddleLeft,
        Bounds = new Rectangle(x, y, width, S(LHeaderH)),
    };

    private Label NewPercentLabel(string text, int x, int y) => new()
    {
        Text = text,
        ForeColor = Theme.Subtle,
        BackColor = Theme.CardBg,
        Font = MakeFont(12f),
        AutoSize = false,
        TextAlign = ContentAlignment.MiddleRight,
        Bounds = new Rectangle(x, y, S(LPercentW), S(LHeaderH)),
    };

    private GlyphIcon NewGlyph(GlyphKind kind, int x, int y) => new()
    {
        Kind = kind,
        ForeColor = Theme.Subtle,
        BackColor = Theme.CardBg,
        Bounds = new Rectangle(x, y + (S(LRowH) - S(LGlyphW)) / 2, S(LGlyphW), S(LGlyphW)),
    };

    private RoundedPanel NewPill(int x, int y) => new()
    {
        BackColor = Theme.CardBg,
        CornerRadius = S(LPillH) / 2,
        Font = MakeFont(10.5f, FontStyle.Bold),
        Cursor = Cursors.Hand,
        Bounds = new Rectangle(x, y + (S(LRowH) - S(LPillH)) / 2, S(LPillW), S(LPillH)),
    };

    private RoundedPanel BuildMasterCard(Rectangle bounds, bool withBrightnessRow)
    {
        var card = NewCard(bounds);
        int innerX = S(LCardInsetX);
        int innerW = card.Width - innerX * 2;
        int sliderX = innerX + S(LGlyphW) + S(LGlyphGap);

        int y = S(LCardPadTop);
        card.Controls.Add(NewHeaderLabel(Strings.AllMonitors, innerX, y, innerW - S(LPercentW)));

        var supported = _displays.Displays.Where(d => d.SupportsBrightness).ToList();

        if (withBrightnessRow)
        {
            int average = supported.Count == 0
                ? 0
                : (int)Math.Round(supported.Average(d => d.Brightness!.Percent));

            _masterPercent = NewPercentLabel(average + "%", innerX + innerW - S(LPercentW), y);
            card.Controls.Add(_masterPercent);
        }

        y += S(LHeaderH);

        if (withBrightnessRow)
        {
            y += S(LRowGap);
            card.Controls.Add(NewGlyph(GlyphKind.Sun, innerX, y));

            _masterBrightness = new BrightnessSlider
            {
                BackColor = Theme.CardBg,
                Bounds = new Rectangle(sliderX, y + (S(LRowH) - S(20)) / 2, innerW - (sliderX - innerX), S(20)),
            };
            _masterBrightness.SetValueQuiet(supported.Count == 0
                ? 0
                : (int)Math.Round(supported.Average(d => d.Brightness!.Percent)));
            _masterBrightness.ValueChanged += (_, _) => OnMasterBrightnessChanged(_masterBrightness.Value);
            card.Controls.Add(_masterBrightness);
            y += S(LRowH);
        }

        // Night row: moon + intensity slider + On/Off/Mixed pill.
        y += S(LRowGap);
        card.Controls.Add(NewGlyph(GlyphKind.Moon, innerX, y));

        _masterPill = NewPill(innerX + innerW - S(LPillW), y);
        _masterPill.Click += (_, _) =>
        {
            NightMode.ToggleAll();
            RefreshNightVisuals();
        };
        card.Controls.Add(_masterPill);

        int nightWidth = innerW - (sliderX - innerX) - S(LPillW) - S(LGlyphGap);
        _masterNight = new BrightnessSlider
        {
            FillColor = NightFill,
            BackColor = Theme.CardBg,
            Bounds = new Rectangle(sliderX, y + (S(LRowH) - S(20)) / 2, nightWidth, S(20)),
        };
        _masterNight.SetValueQuiet(NightMode.RepresentativeIntensity);
        _masterNight.ValueChanged += (_, _) => OnMasterNightIntensityChanged(_masterNight.Value);
        card.Controls.Add(_masterNight);

        return card;
    }

    private RoundedPanel BuildDisplayCard(DisplayTarget display, Rectangle bounds)
    {
        var card = NewCard(bounds);
        int innerX = S(LCardInsetX);
        int innerW = card.Width - innerX * 2;
        int sliderX = innerX + S(LGlyphW) + S(LGlyphGap);

        int y = S(LCardPadTop);
        card.Controls.Add(NewHeaderLabel(display.Name, innerX, y, innerW - S(LPercentW)));

        var row = new DisplayRow
        {
            Target = display,
            Night = null!,
            NightPill = null!,
        };

        if (display.SupportsBrightness)
        {
            row.PercentLabel = NewPercentLabel(display.Brightness!.Percent + "%", innerX + innerW - S(LPercentW), y);
            card.Controls.Add(row.PercentLabel);
        }
        else
        {
            // Be explicit about why there's no brightness slider here.
            card.Controls.Add(new Label
            {
                Text = Strings.NoDdcHint,
                ForeColor = Theme.Subtle,
                BackColor = Theme.CardBg,
                Font = MakeFont(10f),
                AutoSize = false,
                AutoEllipsis = true,
                TextAlign = ContentAlignment.MiddleRight,
                Bounds = new Rectangle(innerX + innerW - S(120), y, S(120), S(LHeaderH)),
            });
        }

        y += S(LHeaderH);

        if (display.SupportsBrightness)
        {
            y += S(LRowGap);
            card.Controls.Add(NewGlyph(GlyphKind.Sun, innerX, y));

            row.Brightness = new BrightnessSlider
            {
                BackColor = Theme.CardBg,
                Bounds = new Rectangle(sliderX, y + (S(LRowH) - S(20)) / 2, innerW - (sliderX - innerX), S(20)),
            };
            row.Brightness.SetValueQuiet(Math.Clamp(display.Brightness!.Percent, 0, 100));
            row.Brightness.ValueChanged += (_, _) =>
            {
                int value = row.Brightness!.Value;
                row.PercentLabel!.Text = value + "%";      // instant, on the UI thread
                display.Brightness!.RequestPercent(value); // throttled hardware write off-thread
                SyncMasterBrightness();
            };
            card.Controls.Add(row.Brightness);
            y += S(LRowH);
        }

        y += S(LRowGap);
        card.Controls.Add(NewGlyph(GlyphKind.Moon, innerX, y));

        var pill = NewPill(innerX + innerW - S(LPillW), y);
        pill.Click += (_, _) =>
        {
            NightMode.SetEnabled(display.Key, !NightMode.Get(display.Key).Enabled);
            RefreshNightVisuals();
        };
        card.Controls.Add(pill);

        int nightWidth = innerW - (sliderX - innerX) - S(LPillW) - S(LGlyphGap);
        var night = new BrightnessSlider
        {
            FillColor = NightFill,
            BackColor = Theme.CardBg,
            Bounds = new Rectangle(sliderX, y + (S(LRowH) - S(20)) / 2, nightWidth, S(20)),
        };
        night.SetValueQuiet(NightMode.Get(display.Key).Intensity);
        night.ValueChanged += (_, _) =>
        {
            NightMode.SetIntensity(display.Key, night.Value);
            // Turning the slider is a clear intent to use night mode on this screen.
            if (!NightMode.Get(display.Key).Enabled)
            {
                NightMode.SetEnabled(display.Key, true);
            }
            RefreshNightVisuals();
        };
        card.Controls.Add(night);

        row.Night = night;
        row.NightPill = pill;
        _rows.Add(row);

        return card;
    }

    // ----- keeping master and per-display controls in sync -----

    private void OnMasterBrightnessChanged(int value)
    {
        if (_masterPercent is not null)
        {
            _masterPercent.Text = value + "%";
        }
        foreach (var row in _rows)
        {
            if (row.Brightness is null) continue;
            row.Brightness.SetValueQuiet(value);
            row.PercentLabel!.Text = value + "%";
            row.Target.Brightness!.RequestPercent(value);
        }
    }

    private void SyncMasterBrightness()
    {
        if (_masterBrightness is null || _masterPercent is null) return;

        var values = _rows.Where(r => r.Brightness is not null).Select(r => r.Brightness!.Value).ToList();
        if (values.Count == 0) return;

        int average = (int)Math.Round(values.Average());
        _masterBrightness.SetValueQuiet(average);
        _masterPercent.Text = average + "%";
    }

    private void OnMasterNightIntensityChanged(int value)
    {
        NightMode.SetAllIntensity(value);
        if (NightMode.AllEnabled != true)
        {
            NightMode.SetAllEnabled(true);
        }
        RefreshNightVisuals();
    }

    // Repaints every night pill and slider from the current NightMode state.
    private void RefreshNightVisuals()
    {
        foreach (var row in _rows)
        {
            var state = NightMode.Get(row.Target.Key);
            SetPill(row.NightPill, state.Enabled ? Strings.On : Strings.Off, state.Enabled);
            row.Night.SetValueQuiet(state.Intensity);
            row.Night.Muted = !state.Enabled;
        }

        if (_masterPill is not null)
        {
            bool? all = NightMode.AllEnabled;
            SetPill(_masterPill,
                all is null ? Strings.Mixed : all.Value ? Strings.On : Strings.Off,
                all is not false);
        }
        if (_masterNight is not null)
        {
            _masterNight.SetValueQuiet(NightMode.RepresentativeIntensity);
            _masterNight.Muted = NightMode.AllEnabled is false;
        }
    }

    private void SetPill(RoundedPanel pill, string text, bool active)
    {
        pill.Text = text;
        pill.FillColor = active ? Theme.Accent : Theme.PillOff;
        pill.ForeColor = active ? Color.White : Theme.Subtle;
        pill.Invalidate();
    }

    private void DisposeChildren()
    {
        for (int i = Controls.Count - 1; i >= 0; i--)
        {
            Controls[i].Dispose();
        }
    }

    // ----- window plumbing -----

    protected override void OnDeactivate(EventArgs e)
    {
        base.OnDeactivate(e);
        _lastHide = DateTime.UtcNow; // mark now so a tray click doesn't reopen
        HideAnimated();              // fade out when the user clicks elsewhere
    }

    protected override bool ProcessCmdKey(ref Message msg, Keys keyData)
    {
        if (keyData == Keys.Escape)
        {
            HideAnimated();
            return true;
        }
        return base.ProcessCmdKey(ref msg, keyData);
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
        ApplyWindowAttributes();
    }

    // Windows 11 chrome: rounded corners, a dark title/border when the app theme
    // is dark, and a matching hairline border. All ignored on Windows 10.
    private void ApplyWindowAttributes()
    {
        if (!IsHandleCreated) return;

        const int DWMWA_USE_IMMERSIVE_DARK_MODE = 20;
        const int DWMWA_WINDOW_CORNER_PREFERENCE = 33;
        const int DWMWA_BORDER_COLOR = 34;
        const int DWMWCP_ROUND = 2;

        try
        {
            int corner = DWMWCP_ROUND;
            DwmSetWindowAttribute(Handle, DWMWA_WINDOW_CORNER_PREFERENCE, ref corner, sizeof(int));

            int dark = Theme.IsDark ? 1 : 0;
            DwmSetWindowAttribute(Handle, DWMWA_USE_IMMERSIVE_DARK_MODE, ref dark, sizeof(int));

            int border = Theme.BorderColorRef;
            DwmSetWindowAttribute(Handle, DWMWA_BORDER_COLOR, ref border, sizeof(int));
        }
        catch (DllNotFoundException)
        {
            // dwmapi unavailable — plain square window, everything else still works.
        }
    }

    [DllImport("dwmapi.dll")]
    private static extern int DwmSetWindowAttribute(IntPtr hwnd, int attr, ref int value, int size);

    protected override void Dispose(bool disposing)
    {
        if (disposing)
        {
            _anim.Dispose();
            ResetFonts();
        }
        base.Dispose(disposing);
    }
}
