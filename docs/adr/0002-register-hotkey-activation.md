# ADR 0002: RegisterHotKey activation

## Status

Accepted

## Superseded decision

Earlier drafts used `WH_KEYBOARD_LL` with a three-key scan-code chord (§+1+2), suppression, and `SendInput` replay. That approach is **not** implemented. See historic notes in git history for `0002-low-level-keyboard-hook.md` if needed.

## Context

Drawing mode must toggle globally without stealing focus or requiring the tray menu.

## Decision

Use `RegisterHotKey` on a hidden WPF message window:

- Primary: **Ctrl+§** (`MOD_CONTROL | MOD_NOREPEAT` + VK resolved from physical scan code `0x29` via `MapVirtualKeyEx` and active keyboard layout)
- Fallback: **Ctrl+Shift+F12** (fixed MVP shortcut; not persisted in settings)
- Re-register primary on `WM_INPUTLANGCHANGE`
- One `WM_HOTKEY` message → one hotkey toggle request
- Single WndProc hook on the hidden HWND (attached exactly once)

## Consequences

- No key suppression or replay; Ctrl+§ consumes the hotkey at the OS level when registered successfully
- Hotkey conflicts surface via one tray notification per failure; fallback remains registered when possible
- Simpler shutdown (unregister hotkeys; no hook thread)
- Layout changes require re-resolution of the § key VK
