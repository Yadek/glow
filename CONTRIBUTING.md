# Contributing to Glow

Bug reports, monitor compatibility notes and pull requests are all welcome.

The most useful thing you can send is a **compatibility report**: your monitor model, how it is connected, and whether the brightness slider showed up. DDC/CI support varies wildly between panels, and there is no way to know without real hardware.

Open an [issue](https://github.com/Yadek/glow/issues/new) for anything — questions included.

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

## Local checks

```powershell
dotnet run --project tests/Glow.Tests -c Release
python -m unittest discover -s tools -p test_github_stats.py -v
dotnet build Glow.sln -c Release
```

The C# checks use a self-contained Windows runner and no external test framework. They use local fixture images and do not change monitor brightness or gamma. Hardware support, resume/hot-plug and the actual DWM material still require a Windows smoke test. Wallpaper Engine's multi-monitor mapping is deliberately disabled until verified against a running installation; a numeric CLI index is not a Windows display identity.

## Repository statistics

`stats.yml` runs at 03:17 UTC daily and on manual dispatch from the default branch. It saves observations and `overview.svg` in an orphan `stats` branch; that branch is excluded from app builds. GitHub can delay scheduled jobs. History remains in that branch even when an API date leaves the 14-day traffic window; a scheduling gap longer than the window cannot be reconstructed.

The normal Actions token collects public totals and writes the history branch. To collect optional traffic, create a repository secret named **GH_TRAFFIC_TOKEN** through **Settings → Secrets and variables → Actions**. Use a GitHub App installation token or a fine-grained token scoped only to this repository with **Administration: read**. Configure the token's own expiry/rotation as needed. Never paste it into an issue, chat or source file. Connecting GitHub to a coding app does not create this Actions secret.

No traffic secret or a 403/404 leaves traffic unavailable while public metrics continue. Transient traffic errors preserve old samples and mark the source as an error. Public API failures or invalid history abort the run without a commit. Repeated runs replace overlapping UTC days; they never add the same day's traffic twice. Asset downloads are paginated across stable releases and prereleases, excluding drafts; deleted assets can reduce the current observable total.

For local collection, set `GH_TOKEN` and optionally `GH_TRAFFIC_TOKEN` in the process environment using your secure credential tooling, then run:

```powershell
python tools/github_stats.py --repository Yadek/glow --data-dir artifacts/stats
```

Do not commit test fixtures as real history. Commits on `stats` use the standard GitHub Actions bot identity; product commits keep the human author's configured identity with no AI trailers.

## Releasing

Push a version tag and CI builds the exe, packages the installer and attaches both to a new GitHub Release:

```bash
git tag v1.0.0
git push origin v1.0.0
```

Tags containing `-beta` are published as prereleases and are never offered to stable installs by the updater.

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

## Documentation

The README exists in two languages — [`README.md`](README.md) (English) and [`README.ru.md`](README.ru.md) (Russian). They are kept in sync section by section; if you change one, please mirror the change in the other. UI strings live in `src/Glow/Localization/Strings.cs`.

## License

By contributing you agree that your contributions are licensed under the [MIT License](LICENSE).
