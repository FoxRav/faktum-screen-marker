# COMPOSER MASTER IMPLEMENTATION BRIEF

## Faktum Screen Marker — Windows 11 temporary screen drawing overlay

**Document status:** Implementation baseline 1.2 (Ctrl+§ via RegisterHotKey)  
**Target environment:** Windows 11, x64  
**Primary stack:** C# 14 / .NET 10 LTS / WPF / Win32 interop  
**Root namespace:** `Faktum.ScreenMarker`  
**Execution model:** Local desktop application; no backend, account, cloud service, analytics, screen capture, or drawing persistence.

---

# 1. PRODUCT CONTRACT

Faktum Screen Marker is a lightweight resident Windows application that opens a temporary transparent drawing layer over the desktop.

## 1.1 Mandatory behavior

- Single resident user process with tray icon.
- Idle: no drawing window or toolbar visible.
- Default activation: **Ctrl+§** (Control + physical key left of top-row `1`, scan code `0x29`).
- One press activates Drawing Mode: transparent topmost overlay per monitor + compact toolbar on pointer monitor.
- Underlying desktop remains visible but does not receive pointer or ordinary keyboard input while Active.
- Same hotkey deactivates: closes overlays/toolbar, restores foreground best-effort, clears all drawings and history.
- Reopening always starts with empty canvas.
- Never intentionally persist drawings, pixels, annotation text, or coordinates.

## 1.2 Explicit non-goals

No drawing persistence, screenshots, recording, OBS, cloud, accounts, analytics, kernel driver, low-level keyboard hook, input suppression/replay, or three-key chord capture.

---

# 2. TECHNOLOGY AND ARCHITECTURE

- C# with nullable reference types, stable .NET 10 LTS, WPF overlays, narrowly scoped Win32 interop.
- Modular monolith: `Core` (pure state/geometry), `Platform.Windows` (hotkeys, monitors, settings), `App` (WPF shell).
- Custom vector drawing via WPF `DrawingVisual` / immutable Core objects.
- One overlay window per monitor; Per-Monitor V2 DPI awareness.
- Hidden message window + `RegisterHotKey` for global activation.
- Single-instance mutex per user session.
- xUnit with handwritten fakes; minimal third-party dependencies.

---

# 3. GLOBAL HOTKEY ACTIVATION

Authoritative detail: `docs/keyboard-chord-design.md`, ADR `docs/adr/0002-register-hotkey-activation.md`.

## 3.1 Primary: Ctrl+§

- Modifier: `MOD_CONTROL | MOD_NOREPEAT`
- Enumerate all loaded keyboard layouts via `GetKeyboardLayoutList`
- For each layout, map scan code `0x29` → VK via `MapVirtualKeyEx`
- Register `Ctrl+VK` for every unique non-zero VK with sequential hotkey IDs (starting at 1)
- All primary IDs route to one toggle handler
- Duplicate VK across layouts registers once
- Partial failure: tray notification once; fallback remains
- Re-register all primary IDs on `WM_INPUTLANGCHANGE` via `HostInitializationCoordinator` (single owner)

## 3.2 Fallback: Ctrl+Shift+F12

- Fixed MVP shortcut (hotkey ID 9000); not stored in settings
- Same hidden window WndProc path

## 3.3 Toggle semantics

- Hotkey toggles; tray Activate/Deactivate are idempotent and do not alter hotkey parity.
- Rapid presses during transitions flip a parity bit; after completion: false → remain, true → opposite transition.
- From Idle: 1→Active, 2→Idle, 3→Active, 4→Idle, 5→Active.
- Side effects execute from `ApplicationTransitionAction` only (`Activate`, `Deactivate`, `None`) — never inferred from `NewState`.

## 3.4 Failure handling

Each registration failure produces exactly one tray notification. No keyboard data in diagnostics.

---

# 4. APPLICATION STATE MACHINE

States: `Starting`, `Idle`, `Activating`, `Active`, `Deactivating`, `FaultedRecoverable`, `Stopping`.

- All toggle requests serialized through `ApplicationStateCoordinator`.
- `DrawingLifecycleOrchestrator.ConsumeTransition()` runs only `transition.Action`.
- Toggle during `Activating`/`Deactivating` flips parity without duplicate side effects.
- Overlay creation transactional; deactivation idempotent.
- `DrawingSession` cleared on deactivation, display rebuild failure, and shutdown.

---

# 5. OVERLAY AND TOOLBAR LIFECYCLE

- One borderless transparent topmost overlay per active monitor.
- One compact movable toolbar on pointer monitor; toolbar owned by pointer overlay.
- Display rebuild order: detach toolbar → flush settings once → close old overlays → create new overlays → reattach/recreate toolbar → restore placement and Z-order.
- Never reuse a toolbar implicitly closed by its owner; never duplicate subscriptions.

`DisplayTopologyCoordinator` uses one lock for active/initialized/rebuild/disposed/session refs, generation IDs, and injectable debounce scheduler.

---

# 6. DRAWING ENGINE (MVP)

Tools: pen, line, arrow, rectangle, ellipse, text, eraser, undo, redo, clear. Immutable Core records; monitor-local DIPs; bounded history; Ramer-Douglas-Peucker simplification with tests.

---

# 7. SETTINGS AND PERSISTENCE

Versioned JSON under `%LOCALAPPDATA%\FaktumAI\ScreenMarker\settings.json`. Benign preferences only (color, stroke, text font size, toolbar placement, language, startup). Hotkeys fixed and read-only in UI. Never persist drawings or input history.

Active-session keyboard shortcuts (Q/W/E/A/S/Z/X, 1–3 widths, 4–9 colors) route through `ToolbarInteractionCoordinator` on overlay `PreviewKeyDown`; blocked during text editing. Text font size presets: 16/24/32/48/64/96 DIP (default 24), persisted as `PreferredTextFontSize`.

---

# 8. SECURITY

Normal user process; `RegisterHotKey` only; no hook; deterministic unregistration at shutdown; no secrets in repo. See `docs/privacy-and-security.md` and `SECURITY.md`.

---

# 9. TEST STRATEGY

## Automated

- Transition actions and parity toggles (1–10 rapid presses)
- Orchestrator: no double activate/deactivate during transitions
- Multi-layout VK enumeration, duplicate elimination, multiple primary IDs, message routing, complete unregistration
- Toolbar rebuild ordering (detach before owner close)
- Idempotent host shutdown
- Display topology debounce via injected scheduler
- Settings, geometry, history, mutex, smoke modes

## Manual (M1–M19 outstanding)

Finnish/English Ctrl+§, layout switch, fallback, rapid toggle 1–4, toolbar visible during drawing and after attach/detach/DPI, no duplicate toggles after layout change. See `docs/manual-test-plan.md`.

---

# 10. BUILD, CI, AND RELEASE

CI on `windows-latest`: restore locked → build Release → test → publish win-x64 → smoke tests → architecture consistency script.

Scripts: `build.ps1`, `test.ps1`, `publish-win-x64.ps1`, `verify-current-architecture.ps1`, `create-review-zip.ps1`.

Publish: self-contained single-file win-x64, trimming off.

---

# 11. DEFINITION OF DONE

- Restore/build/test/publish/smoke/architecture-check all exit 0
- Ctrl+§ activates/deactivates via RegisterHotKey across loaded layouts
- Documentation matches implementation (no hook/chord obsolete terms in active requirements)
- Review ZIP created; M1–M19 not claimed passed unless manually executed
