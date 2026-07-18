# Faktum Screen Marker

[![CI](https://github.com/FoxRav/faktum-screenwriter/actions/workflows/ci.yml/badge.svg)](https://github.com/FoxRav/faktum-screenwriter/actions/workflows/ci.yml)

**Temporary transparent screen annotations for Windows 11.** Press **Ctrl+§** to draw over your desktop, then press again to close — everything is cleared. Nothing is saved to disk.

Faktum Screen Marker is a lightweight tray-resident desktop app built with **.NET 10**, **WPF**, and narrowly scoped Win32 interop. It runs as a single instance per Windows user session and supports dual-monitor setups with one overlay per display.

## Features

- **Global hotkey activation** — **Ctrl+§** (physical key left of top-row `1`, resolved via scan code `0x29` across keyboard layouts) with **Ctrl+Shift+F12** fallback
- **Per-monitor transparent overlays** — one borderless topmost window per active monitor; Per-Monitor V2 DPI aware
- **In-overlay toolbar** — compact `ToolbarControl` rendered in the same HWND as the overlay visual tree (no separate toolbar window)
- **In-overlay text editor** — non-modal `TextEditorControl` placed beside the click point on each monitor
- **Drawing tools** — pen, line, arrow, rectangle, ellipse, text (one-shot), eraser, undo, redo, clear
- **Session keyboard shortcuts** — Q/W/E/A/S/Z/X for tools; 1–3 for stroke width; 4–9 for color
- **Text font size presets** — 16, 24, 32, 48, 64, 96 DIP (default 24)
- **No drawing persistence** — annotations exist only for the active session; reopening always starts with an empty canvas
- **Tray resident** — single instance per user session via session-scoped mutex
- **Automated smoke tests** — `--smoke-test` and `--platform-smoke-test` for CI and local verification

## Quick start

### Run the published executable

After building (see below), run:

```
artifacts/publish/win-x64/FaktumScreenMarker.exe
```

The app appears in the system tray. Press **Ctrl+§** to activate drawing mode.

### Build from source

```powershell
git clone https://github.com/FoxRav/faktum-screenwriter.git
cd faktum-screenwriter
./scripts/dev.ps1
```

This restores dependencies, builds Release, runs tests, publishes win-x64, and creates a review ZIP.

Skip ZIP creation when needed:

```powershell
./scripts/dev.ps1 -SkipZip
```

## Requirements

- **Windows 11** x64
- **.NET SDK 10.0.302** (pinned in `global.json`)

## Activation hotkeys

| Hotkey | Role |
|--------|------|
| **Ctrl+§** | Primary toggle — Idle ↔ Active |
| **Ctrl+Shift+F12** | Fallback toggle (fixed; not configurable) |

Both hotkeys use `RegisterHotKey` with `MOD_NOREPEAT`. The primary hotkey enumerates all loaded keyboard layouts, maps scan code `0x29` to virtual keys, and registers **Ctrl+VK** for each unique layout. When the input language changes, primary registrations are refreshed automatically.

Toggle semantics: one press activates (transparent overlays + toolbar on the pointer monitor); the same hotkey deactivates (overlays close, drawings destroyed, foreground restored best-effort). The tray menu provides explicit Activate/Deactivate actions that are idempotent.

## Drawing session keyboard shortcuts

While drawing mode is **Active** and you are **not** typing in the text editor, these shortcuts work on every monitor overlay:

| Key | Action |
|-----|--------|
| Q | Pen |
| W | Line |
| E | Arrow |
| A | Rectangle |
| S | Ellipse |
| Z | Text (one-shot) |
| X | Eraser |
| 1 | Stroke width 2 DIP (thin) |
| 2 | Stroke width 4 DIP (medium) |
| 3 | Stroke width 8 DIP (thick) |
| 4 | Red |
| 5 | Green |
| 6 | Blue |
| 7 | Yellow |
| 8 | White |
| 9 | Black |

While the text editor has focus, only **Ctrl+Enter** (commit) and **Escape** (cancel) are handled; letter and digit shortcuts are suppressed so you can type normally.

## Text font size

Toolbar **Text.FontSize** selector presets: **16, 24, 32, 48, 64, 96 DIP** (default **24**).

Each committed text annotation stores its own size. Changing the selector updates the open editor live and applies to the next placement only.

## Settings location

```
%LOCALAPPDATA%\FaktumAI\ScreenMarker\settings.json
```

Persisted preferences include toolbar placement, preferred color, stroke width, text font size, language, and startup options. **Drawing content never persists.** Activation hotkeys are fixed and read-only in the settings UI.

## Build and test

Individual steps:

```powershell
$env:PATH = "$env:USERPROFILE\.dotnet;$env:PATH"
dotnet restore --locked-mode
dotnet build Faktum.ScreenMarker.slnx -c Release --no-restore
dotnet test Faktum.ScreenMarker.slnx -c Release --no-build
./scripts/verify-current-architecture.ps1
./scripts/publish-win-x64.ps1
./artifacts/publish/win-x64/FaktumScreenMarker.exe --smoke-test
./artifacts/publish/win-x64/FaktumScreenMarker.exe --platform-smoke-test
./scripts/create-review-zip.ps1
```

Review ZIPs are written to `artifacts/review-zips/` (gitignored), named `faktum-screen-marker-review-YYYYMMDD-HHmmss.zip`.

## Project structure

```
src/
  Faktum.ScreenMarker.Core/          Pure domain: drawing models, geometry, history, state machine
  Faktum.ScreenMarker.Platform.Windows/ Win32: hotkeys, monitors, settings, mutex
  Faktum.ScreenMarker.App/           WPF: overlays, in-overlay toolbar, tray, settings UI
tests/
  Faktum.ScreenMarker.Core.Tests/
  Faktum.ScreenMarker.Platform.Windows.Tests/
scripts/                             build, test, publish, verify, review ZIP
docs/                                architecture, ADRs, manual test plan, privacy
.github/workflows/ci.yml             Windows CI pipeline
```

See `docs/architecture.md` for design details.

## Privacy

- **No drawing persistence** — annotations, pixels, and text coordinates are never written to disk
- **Settings only** — benign UI preferences in `%LOCALAPPDATA%`
- **No network** — local desktop application; no accounts, cloud, analytics, or screen capture
- **No keyboard hook** — global activation uses `RegisterHotKey` only; no keystroke logging

See `docs/privacy-and-security.md` for full details.

## Manual testing

Hardware verification checklist: `docs/manual-test-plan.md` (M1–M19 manual; M20–M21 automated smoke).

## Contributing

See [CONTRIBUTING.md](CONTRIBUTING.md).

## Security

Report security concerns per [SECURITY.md](SECURITY.md). Do not include drawing content or settings files in reports.

## License

See [NOTICE.md](NOTICE.md).

## Specification

Implementation baseline and product contract: `COMPOSER_MASTER_IMPLEMENTATION_BRIEF.md`.
