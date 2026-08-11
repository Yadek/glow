<div align="center">

<img src="assets/glow.png" width="120" alt="Glow logo"/>

# Glow

**Lightweight tray utility for monitor brightness over DDC/CI — with night mode on each monitor separately, or all of them at once.**

[![Build & Release](https://github.com/Yadek/glow/actions/workflows/build.yml/badge.svg)](https://github.com/Yadek/glow/actions/workflows/build.yml)
[![Release](https://img.shields.io/github/v/release/Yadek/glow?display_name=tag)](https://github.com/Yadek/glow/releases/latest)
[![Platform](https://img.shields.io/badge/Windows-10%20%7C%2011-0078D6?logo=windows)](https://www.microsoft.com/windows)
[![.NET](https://img.shields.io/badge/.NET-8.0-512BD4?logo=dotnet)](https://dotnet.microsoft.com/)
[![License: MIT](https://img.shields.io/badge/License-MIT-green.svg)](LICENSE)

**English** · [Русский](README.ru.md)

</div>

---

## Features

- **True hardware brightness** — drives external monitors over **DDC/CI** (`Dxva2.dll`), not a software overlay.
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

## How it works

Click the **Glow** icon in the system tray (next to the volume icon) → a popup appears with an **All monitors** card that drives every screen at once. Press **Each monitor separately** to unfold a card per display, and again to fold them away; the popup remembers which way you left it.

Every card has a **sun row** (hardware brightness over DDC/CI) and a **moon row** (night mode). The master sliders keep the value *you* set them to — adjusting one screen on its own never drags them along, so the master stays something you aim rather than a running average. Its night pill reads *Mixed* when your screens disagree.

Right-click the icon for **Night mode on all monitors**, **Run at startup** and **Exit**. Middle-click toggles night mode everywhere without opening the popup.

> **Brightness** needs DDC/CI to be supported and enabled by the monitor. Most external desktop monitors support it; many laptop internal panels do not — those displays still get a card, with night mode only.
>
> **Night mode** drives display gamma, so it competes with the Windows *Night light* feature: whichever wrote last wins. Turn the Windows one off if you use Glow's. It also has no effect on a display running in HDR mode, which ignores gamma ramps entirely.

## Installation

1. Download `Glow-Setup-x.y.z.exe` from the [latest release](https://github.com/Yadek/glow/releases/latest).
2. Run it and tick **Start Glow automatically when Windows starts**.
3. Glow appears in your tray. That's it.

Prefer no installer? Grab `Glow-x.y.z-portable.exe` from the same release and run it directly — it's fully self-contained.

### Beta builds

Versions tagged `-beta` are published as GitHub **prereleases** and have to be downloaded from the [releases page](https://github.com/Yadek/glow/releases) directly — the *latest release* link above always points at the newest stable build.

Beta builds **never check for updates on their own**, so they won't nag you mid-test; **Check for updates** in the tray menu still works when you ask for it. Stable installs are never offered a beta either, because the update check only looks at the latest *stable* release.

## Tech stack

| Area           | Choice                                              |
| -------------- | --------------------------------------------------- |
| Language       | C# / .NET 8                                          |
| UI             | WinForms — frameless, hand-drawn dark popup          |
| Brightness API | Win32 DDC/CI P/Invoke (`Dxva2.dll`)                 |
| Night mode     | Per-display gamma ramps (`gdi32.dll`)               |
| Monitor names  | EDID parsed from the registry (no WMI)              |
| Packaging      | Self-contained single-file exe                      |
| Installer      | Inno Setup 6                                         |
| CI/CD          | GitHub Actions → build, package, publish a Release  |

## Building from source

Requires the **.NET 8 SDK** and, for the installer, **Inno Setup 6**.

```powershell
# Publish the self-contained single-file exe
dotnet publish src/Glow/Glow.csproj -c Release -r win-x64 `
  --self-contained true -p:PublishSingleFile=true -o publish

# Build the installer
& "C:\Program Files (x86)\Inno Setup 6\ISCC.exe" installer\glow.iss `
  "/DSourceExe=$PWD\publish\Glow.exe"
# -> installer\Output\Glow-Setup-1.0.0.exe
```

The tray icon is drawn at runtime; the `.exe` icon (`src/Glow/glow.ico`) is committed and can be regenerated from the logo with `tools/Make-Icon.ps1`.

## Releasing

Push a version tag and CI builds the exe, packages the installer and attaches both to a new GitHub Release:

```bash
git tag v1.0.0
git push origin v1.0.0
```

## Repository layout

```
glow/
├─ src/Glow/            # application source
│  ├─ Native/           # Win32 / DDC-CI / gamma / DPI P/Invoke
│  ├─ Monitors/         # display discovery, EDID names, DDC brightness
│  ├─ NightShift/       # per-display night mode (gamma ramps)
│  ├─ Settings/         # HKCU settings, incl. per-display state
│  ├─ Localization/     # EN/RU strings
│  ├─ Startup/          # HKCU autostart toggle
│  ├─ Update/           # GitHub release check (manual in beta builds)
│  ├─ UI/               # tray icon, popup, slider, glyphs, theme
│  └─ glow.ico
├─ installer/glow.iss   # Inno Setup script
├─ tools/Make-Icon.ps1  # icon generator
├─ assets/glow.svg      # source logo
└─ .github/workflows/   # CI/CD
```

## License

[MIT](LICENSE)
