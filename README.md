<div align="center">

<img src="assets/glow.png" width="120" alt="Glow logo"/>

# Glow

**Lightweight tray utility to control your monitors' hardware brightness via DDC/CI.**

[![Build & Release](https://github.com/Yadek/glow/actions/workflows/build.yml/badge.svg)](https://github.com/Yadek/glow/actions/workflows/build.yml)
[![Release](https://img.shields.io/github/v/release/Yadek/glow?display_name=tag)](https://github.com/Yadek/glow/releases/latest)
[![Platform](https://img.shields.io/badge/Windows-10%20%7C%2011-0078D6?logo=windows)](https://www.microsoft.com/windows)
[![.NET](https://img.shields.io/badge/.NET-8.0-512BD4?logo=dotnet)](https://dotnet.microsoft.com/)
[![License: MIT](https://img.shields.io/badge/License-MIT-green.svg)](LICENSE)

</div>

---

## Features

- **True hardware brightness** — drives external monitors over **DDC/CI** (`Dxva2.dll`), not a software overlay.
- **Multi-monitor** — auto-detects every connected display and shows a separate slider for each, labelled with the real model name (read from EDID).
- **Near-zero footprint** — no background timers or polling; the app sleeps in the message loop and only wakes on a click. Idle CPU ≈ 0%.
- **Single self-contained `.exe`** — no .NET runtime to install.
- **Auto localization** — UI follows the Windows display language (English / Русский), English fallback.
- **Matches your theme** — the slider uses the current Windows accent color.
- **Silent autostart** — optional launch with Windows via `HKCU\…\Run`.
- **Clean uninstall** — removes the app, the autostart key and all config; leaves no trace.

## How it works

Click the **Glow** icon in the system tray (next to the volume icon) → a small popup appears with one brightness slider per monitor. Drag, and the change is written straight to the display hardware. Right-click the icon for **Run at startup** and **Exit**.

> DDC/CI must be supported and enabled by the monitor. Most external desktop monitors support it; many laptop internal panels do not (and are simply skipped).

## Installation

1. Download `Glow-Setup-x.y.z.exe` from the [latest release](https://github.com/Yadek/glow/releases/latest).
2. Run it and tick **Start Glow automatically when Windows starts**.
3. Glow appears in your tray. That's it.

Prefer no installer? Grab `Glow-x.y.z-portable.exe` from the same release and run it directly — it's fully self-contained.

## Tech stack

| Area           | Choice                                              |
| -------------- | --------------------------------------------------- |
| Language       | C# / .NET 8                                          |
| UI             | WinForms — frameless, hand-drawn dark popup          |
| Brightness API | Win32 DDC/CI P/Invoke (`Dxva2.dll`)                 |
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
│  ├─ Native/           # Win32 / DDC-CI P/Invoke
│  ├─ Monitors/         # monitor discovery, names, brightness
│  ├─ Localization/     # EN/RU strings
│  ├─ Startup/          # HKCU autostart toggle
│  ├─ UI/               # tray icon, popup, slider, theme
│  └─ glow.ico
├─ installer/glow.iss   # Inno Setup script
├─ tools/Make-Icon.ps1  # icon generator
├─ assets/glow.svg      # source logo
└─ .github/workflows/   # CI/CD
```

## License

[MIT](LICENSE)
