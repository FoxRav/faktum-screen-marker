# Security

Report security concerns to your Faktum AI contact. Do not include drawing content or settings files in reports.

The application registers global hotkeys via `RegisterHotKey` on a hidden message window. It does **not** install a low-level keyboard hook, log keystrokes, or persist annotation text outside the active session.

Single-instance enforcement uses a per-user session mutex (`Local\FaktumAI.ScreenMarker.SingleInstance.{username}`) so other Windows users are not blocked.
