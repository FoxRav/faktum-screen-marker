# Faktum Screen Marker — Architecture Whitepaper

**Temporary Windows 11 screen-drawing overlay**  
**Version:** 1.2  
**Date:** 17 July 2026  
**Status:** Approved implementation baseline  
**Audience:** Windows/.NET engineers, architects, security reviewers, QA

---

## 1. Executive summary

Faktum Screen Marker is a resident Windows 11 utility that toggles a transparent multi-monitor drawing layer with **Ctrl+§** (Control plus the physical key left of top-row `1`). The same shortcut deactivates and destroys the session. No screenshots, no cloud, no drawing persistence.

Activation uses **`RegisterHotKey`** on a hidden message window — not a low-level keyboard hook, not input suppression, not `SendInput` replay, and not a three-key chord.

Fallback: **Ctrl+Shift+F12**.

Stack: C# / .NET 10 LTS / WPF / scoped Win32 interop. Modular monolith with testable Core state machines.

---

## 2. Product contract

| Requirement | Behavior |
|---|---|
| Idle | Tray icon only; zero visible overlay |
| Activate | Ctrl+§ → overlays on all monitors + toolbar on pointer monitor |
| Input ownership | Underlying apps blocked from pointer/keyboard while Active |
| Deactivate | Same hotkey or toolbar Close → destroy session, restore foreground best-effort |
| Ephemeral | Reopen always blank; no intentional disk persistence of drawings |
| Multi-monitor | One overlay per monitor; mixed DPI supported |
| Recovery | Tray menu, fallback hotkey, fault-recoverable state |

---

## 3. Logical architecture

```text
Faktum.ScreenMarker.App          WPF tray, overlays, toolbar, host
        ↓
Faktum.ScreenMarker.Core         State machine, drawing domain, geometry
        ↑
Faktum.ScreenMarker.Platform.Windows   Hotkeys, monitors, settings, mutex
```

Core has no WPF/Win32 references. Dependency flows inward.

---

## 4. Activation hotkey design

### 4.1 Why RegisterHotKey

Ctrl+§ is a standard modifier hotkey. `RegisterHotKey` delivers `WM_HOTKEY` without intercepting other applications' input — simpler, safer, and sufficient for MVP.

### 4.2 Layout-independent Ctrl+§

Physical scan code `0x29` maps to different virtual keys per keyboard layout (e.g. Finnish `§/½`, US `` ` ``).

At startup and on each `WM_INPUTLANGCHANGE`:

1. Call `GetKeyboardLayoutList`
2. For each layout, `MapVirtualKeyEx(0x29, MAPVK_VSC_TO_VK_EX, layout)`
3. Collect unique non-zero VK values
4. Register `MOD_CONTROL | MOD_NOREPEAT` + VK for each unique mapping with sequential hotkey IDs
5. Route every primary ID to the same toggle handler

This ensures Ctrl+§ works after layout switches without requiring focus on the hidden window.

### 4.3 Fallback

`Ctrl+Shift+F12` registered with a separate fixed ID. Independent failure notifications for primary (including partial registration) and fallback.

### 4.4 Input-layout ownership

`HostInitializationCoordinator` alone subscribes to `InputLanguageChanged`, re-registers primary hotkeys, and raises failure notifications. No duplicate handlers in `ApplicationHost`.

---

## 5. Application state machine

```text
Starting → Idle ⇄ Activating → Active → Deactivating → Idle
              ↘ FaultedRecoverable ↗
Any → Stopping (shutdown)
```

### Transition actions

`ApplicationTransitionResult` carries explicit `ApplicationTransitionAction`:

| Event | Action |
|---|---|
| Enter Activating | Activate |
| Enter Deactivating | Deactivate |
| Remain in state / queue parity / fault | None |
| Activation completes with pending parity | Deactivate |
| Deactivation completes with pending parity | Activate |

`DrawingLifecycleOrchestrator` executes **only** `transition.Action`, never infers side effects from `NewState`.

### Rapid toggle parity

During `Activating` or `Deactivating`, each hotkey press flips `_pendingToggleParity`. After completion: parity false → stay; parity true → opposite transition. Sequence from Idle: 1 Active, 2 Idle, 3 Active, 4 Idle, 5 Active.

Tray explicit Activate/Deactivate do not alter parity unless they initiate a new transition.

---

## 6. Overlay and toolbar lifecycle

- One `OverlayWindow` per monitor; pointer overlay owns toolbar.
- `DisplayTopologyCoordinator` debounces display changes, uses generation IDs, single lock for coordinator state.
- Rebuild sequence:
  1. Detach toolbar from owner
  2. Flush settings once
  3. Close old overlays
  4. Create new overlays
  5. Select pointer overlay
  6. Reattach or recreate toolbar
  7. Restore placement and Z-order

Toolbar never implicitly closed by old owner; closed toolbars are never reused.

---

## 7. Host shutdown

`ApplicationHost.Stop()` / `Dispose()` guarded by `_stopped` / `_disposed`. Repeated calls do not double-deactivate, double-dispose coordinators, double-unregister hotkeys, or re-enter lifecycle after `Stopping`.

---

## 8. Drawing model

Immutable Core records (`FreehandStroke`, `LineAnnotation`, etc.), monitor-local DIPs, custom WPF visual renderer, bounded undo/redo history, deterministic stroke simplification.

---

## 9. Settings and privacy

Atomic JSON settings for benign preferences only. No drawing geometry, annotation text, keystrokes, or screen pixels persisted. Operational logs sanitized.

---

## 10. Testing

| Layer | Coverage |
|---|---|
| Core | Transition actions, parity 1–10, geometry, history |
| Platform | Multi-layout VK resolution, hotkey IDs, unregistration, mutex, settings |
| App | Toolbar rebuild ordering, idempotent shutdown, smoke modes |
| Manual M1–M19 | Hardware: layouts, fallback, rapid toggle, toolbar through DPI/rebuild |

Architecture consistency enforced by `scripts/verify-current-architecture.ps1` in CI.

---

## 11. Build and release

Pinned .NET 10 SDK, warnings as errors, locked restore, self-contained single-file win-x64 publish, CI smoke tests, review ZIP artifact.

---

## 12. Known limitations

- `RegisterHotKey` may conflict with other applications using the same combination.
- No overlay on secure desktop / UAC consent / lock screen.
- Foreground restoration is best-effort.
- Multi-monitor mixed-DPI requires hardware validation (M7–M8).
- Code signing and installer out of MVP scope.

---

## 13. References

- `docs/keyboard-chord-design.md`
- `docs/adr/0002-register-hotkey-activation.md`
- `COMPOSER_MASTER_IMPLEMENTATION_BRIEF.md`
