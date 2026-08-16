<div align="center">

<img src="https://raw.githubusercontent.com/Yadek/glow/main/assets/glow.png" width="120" alt="Glow logo"/>

# Glow — Monitor Brightness for Windows

**Change the brightness of your external monitors from the Windows tray, instead of reaching for the buttons on the monitor itself.**

[![Build & Release](https://github.com/Yadek/glow/actions/workflows/build.yml/badge.svg)](https://github.com/Yadek/glow/actions/workflows/build.yml)
[![Release](https://img.shields.io/github/v/release/Yadek/glow?display_name=tag)](https://github.com/Yadek/glow/releases/latest)
[![Platform](https://img.shields.io/badge/Windows-10%20%7C%2011-0078D6?logo=windows)](https://www.microsoft.com/windows)
[![.NET](https://img.shields.io/badge/.NET-8.0-512BD4?logo=dotnet)](https://dotnet.microsoft.com/)
[![License: MIT](https://img.shields.io/badge/License-MIT-green.svg)](LICENSE)

### [⬇ Download for Windows](https://github.com/Yadek/glow/releases/latest)

**English** · [Русский](README.ru.md)

</div>

<!-- SCREENSHOT: the tray popup with several monitor cards -> assets/screenshot-popup.png -->
<!-- SCREENSHOT: the right-click tray menu -> assets/screenshot-menu.png -->
<!-- SCREENCAST: dragging a slider while the monitor visibly dims -> assets/demo.gif -->

---

Windows has no built-in way to control the brightness of an external monitor. The slider in Settings only moves a laptop's internal panel; for everything plugged in over HDMI or DisplayPort you are left poking at the physical buttons on the bezel.

Glow puts a slider in your system tray that changes the brightness **in the monitor itself**, over the DDC/CI protocol the monitor already speaks — not a dark layer drawn on top of the picture. Every connected display gets its own slider, plus a per-screen night mode for warmer colours in the evening.

It is a single self-contained `.exe`, uses essentially no CPU while idle, and is free and open source. If you came here looking for a replacement for **ClickMonitorDDC**, which has not been updated in years, or for an alternative to **Twinkle Tray** or **Monitorian**, that is exactly the job Glow does.

## Install

1. Download **`Glow-Setup-x.y.z.exe`** from the [latest release](https://github.com/Yadek/glow/releases/latest).
2. Run it and tick **Start Glow automatically when Windows starts**.
3. Glow appears in your tray. That's it.

Prefer not to install anything? Grab **`Glow-x.y.z-portable.exe`** from the same release and run it directly — it is fully self-contained and leaves nothing behind.

**You need:** Windows 10 or 11 (64-bit) and a monitor that supports DDC/CI — most external desktop monitors do. See [Not working?](#not-working) if no brightness slider shows up.

## How to use it

Click the **Glow** icon in the system tray (next to the volume icon) → a popup appears with an **All monitors** card that drives every screen at once. Press **Each monitor separately** to unfold a card per display, and again to fold them away; the popup remembers which way you left it.

Every card has a **sun row** (hardware brightness over DDC/CI) and a **moon row** (night mode). The master sliders keep the value *you* set them to — adjusting one screen on its own never drags them along, so the master stays something you aim rather than a running average. Its night pill reads *Mixed* when your screens disagree.

Right-click the icon for **Night mode on all monitors**, **Run at startup** and **Exit**. Middle-click toggles night mode everywhere without opening the popup.

## Features

- **True hardware brightness** — drives external monitors over **DDC/CI** (`Dxva2.dll`), not a software overlay. The monitor's own backlight actually changes, so you get the power saving and the eye comfort, not a dimmed image.
- **Multi-monitor** — auto-detects every connected display and shows a separate slider for each, labelled with the real model name (read from EDID).
- **Per-monitor night mode** — each screen gets its own warmth, **or** warm them all at once from the *All monitors* card. Works even on displays with no DDC/CI, because it goes through gamma ramps rather than the monitor's own colour controls.
- **Settings that stick** — night mode is saved against each display's EDID identity, so it survives a reboot and follows the monitor to another port. Re-applied automatically after sleep, a resolution change or unlocking, where Windows would otherwise reset it silently.
- **Near-zero footprint** — no background timers or polling; the app sleeps in the message loop and only wakes on a click. Idle CPU ≈ 0%.
- **Single self-contained `.exe`** — no .NET runtime to install.
- **Auto localization** — UI follows the Windows display language (English / Русский), English fallback.
- **Matches your theme** — follows the Windows light/dark app theme and accent colour, with Windows 11 rounded corners.
- **Silent autostart** — optional launch with Windows via `HKCU\…\Run`.
- **Auto-update** — checks GitHub Releases and updates once you say yes. Betas are skipped.
- **Clean uninstall** — removes the app, the autostart key and all config; leaves no trace.

## Not working?

**A monitor has no brightness slider, only night mode.**
That display does not expose DDC/CI. Two common causes: it is a laptop's internal panel — most of them genuinely do not support it — or DDC/CI is switched off in the monitor's own on-screen menu, where it is often buried under *Settings*, *Others* or *OSD*. Turn it on there and restart Glow. Displays without DDC/CI still get a card, with night mode only.

**Night mode turns itself off, or fights with Windows.**
Night mode drives display gamma, which is the same knob the Windows *Night light* feature uses — whichever wrote last wins. If you use Glow's, switch the Windows one off in *Settings → System → Display*.

**Night mode does nothing on one screen.**
That screen is probably running in HDR mode, which ignores gamma ramps entirely. Turn HDR off for that display if you want night mode on it.

**Brightness resets after sleep or a resolution change.**
It should not — Glow re-applies your settings after sleep, unlocking and display changes. If it still happens on your hardware, please [open an issue](https://github.com/Yadek/glow/issues/new) with your monitor model.

Anything else, or something behaving oddly? [Open an issue](https://github.com/Yadek/glow/issues/new) — questions are welcome, not just bug reports.

## Beta builds

Versions tagged `-beta` are published as GitHub **prereleases** and have to be downloaded from the [releases page](https://github.com/Yadek/glow/releases) directly — the *latest release* link above always points at the newest stable build.

Beta builds **never check for updates on their own**, so they won't nag you mid-test; **Check for updates** in the tray menu still works when you ask for it. Stable installs are never offered a beta either, because the update check only looks at the latest *stable* release.

## Contributing

Bug reports, monitor compatibility notes and pull requests are all welcome — see **[CONTRIBUTING.md](CONTRIBUTING.md)** for the tech stack, how to build from source and how releases are cut.

## License

[MIT](LICENSE)
