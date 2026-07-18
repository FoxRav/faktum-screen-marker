# Global hotkey activation

## Primary: Ctrl+§

- Modifier: `MOD_CONTROL | MOD_NOREPEAT` (left or right Ctrl)
- Physical scan code: `0x29` (key left of top-row 1)
- Virtual keys: every unique VK returned by `MapVirtualKeyEx` for scan code `0x29` across **all loaded keyboard layouts** from `GetKeyboardLayoutList`
- Each unique VK registers as `RegisterHotKey` with `MOD_CONTROL | MOD_NOREPEAT`, sequential hotkey IDs starting at `1`, all routing to the same toggle handler
- Duplicate VK values across layouts register once
- Partial registration failure is reported once via tray notification; fallback remains available
- Rebuild primary registrations on `WM_INPUTLANGCHANGE` (layout add/remove or switch)

## Fallback: Ctrl+Shift+F12

Fixed secondary hotkey with the same `RegisterHotKey` mechanism (hotkey ID `9000`).

## Toggle semantics

| Current state | Ctrl+§ action |
|---------------|---------------|
| Idle | Activate |
| Active | Deactivate + destroy drawings |
| Activating | Flip pending parity (no duplicate activation) |
| Deactivating | Flip pending parity (no duplicate deactivation) |

Rapid presses use a parity bit: from Idle, 1→Active, 2→Idle, 3→Active, 4→Idle, 5→Active. Tray Activate/Deactivate do not alter hotkey parity.

Side effects run from `ApplicationTransitionAction` only (`Activate`, `Deactivate`, or `None`) — never inferred from `NewState` alone.

## Failure handling

- Primary registration failure: tray notification; fallback and tray menu remain
- Partial primary registration: tray notification; successfully registered VKs remain active
- Fallback registration failure: tray notification; primary and tray menu remain
- No low-level keyboard hook, suppression, replay, or chord capture UI
