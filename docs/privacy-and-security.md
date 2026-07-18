# Privacy and security

- No drawing persistence; settings file excludes drawing content
- Global hotkeys registered via `RegisterHotKey`; no low-level keyboard hook
- No keystroke history, logging of scan codes, or key capture UI
- Diagnostic logs contain error categories only (no PII, no key data)
- Single-instance mutex: `Local\FaktumAI.ScreenMarker.SingleInstance.{username}` (one instance per user session)
