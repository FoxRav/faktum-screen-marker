# Architecture

Modular monolith with three projects:

- **Faktum.ScreenMarker.Core** — application coordinator, drawing models, geometry, history, simplification (no WPF/Win32)
- **Faktum.ScreenMarker.Platform.Windows** — global hotkey registration, monitors, settings, mutex, foreground window
- **Faktum.ScreenMarker.App** — WPF overlays, toolbar, tray, settings UI, composition root

One overlay window per monitor while Active. Drawing uses WPF `DrawingVisual` with immutable Core objects.

## Activation

Default toggle: **Ctrl+§** via `RegisterHotKey` on a hidden message window (`MOD_CONTROL | MOD_NOREPEAT` + layout-resolved VK for scan code `0x29`). Fallback: **Ctrl+Shift+F12**. `WM_INPUTLANGCHANGE` triggers primary hotkey re-registration.

Toggle semantics: Idle→Active, Active→Idle (drawings destroyed), with queued transitions during Activating/Deactivating. Hotkey uses `RequestHotkeyToggle()`; tray menu uses explicit idempotent activate/deactivate.

Toolbar is owned by the pointer monitor overlay and re-parented on display rebuild so it stays above overlays (`SetWindowPos` Z-order, `ShowActivated=false`).

Single-instance mutex: `Local\FaktumAI.ScreenMarker.SingleInstance.{username}` (session scope; other Windows users not blocked).
