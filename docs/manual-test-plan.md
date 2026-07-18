# Manual test plan

Execute on Windows 11 x64 hardware with .NET 10 and the published `FaktumScreenMarker.exe`. Record **Observed** for each item. Do **not** mark rows as passed unless executed on target hardware.

## Current status (2026-07-18, in-overlay toolbar architecture)

| Range | Status | Notes |
|-------|--------|-------|
| Automated (216 tests) | **PASSED** | Core 54 + Platform 162; font size, keyboard shortcuts, WPF hit-test matrix (21 controls) |
| M20 `--smoke-test` | **PASSED** | Exit 0 (agent) |
| M21 `--platform-smoke-test` | **PASSED** | Exit 0; WPF hit test all 21 controls + routed `Tool.Line` + Q shortcut smoke |
| UI1–UI6 | **NOT VERIFIED** | Manual in-overlay toolbar gates on Kone1 — do not claim until executed on hardware |
| BT1–BT7 | **NOT VERIFIED** | Manual Kone1 hardware gates — blocked until UI1–UI6 recorded |
| CR1–CR4 | **NOT PASSED** | Manual crash-regression on published exe |
| TS1–TS4 | **NOT VERIFIED** | Retest on Kone1 after UI1–UI6 and BT1–BT7 |
| TS-text-size | **NOT VERIFIED** | Manual text font size gates — do not mark passed until hardware run |
| KS1–KS3 | **NOT VERIFIED** | Manual keyboard shortcut gates — do not mark passed until hardware run |
| M2a-BenQ | **NOT VERIFIED** | Prior FAIL on freehand mouse-up |
| M2b | **NOT VERIFIED** | Prior FAIL (toolbar stuck after text); in-overlay editor removes tree before callback |
| M2a-Laptop | **BLOCKED** | Blocked by UI1–UI6, BT1–BT7, CR1–CR4 |
| M3–M19 | **BLOCKED** | Do not execute until UI1–UI6, BT1–BT7, and CR1–CR4 pass |

## In-overlay toolbar verification gates (UI1–UI6)

Run on published `FaktumScreenMarker.exe` on Kone1 dual-monitor setup (BenQ + Lenovo). **Do not mark passed unless observed on hardware.**

| ID | Scenario | Expected | Observed |
|----|----------|----------|----------|
| UI1 | Tool selection before first drawing | Activate with Ctrl+§; without drawing, click Line then Rectangle then Pen on in-overlay toolbar; each tool selects immediately; toolbar remains clickable | |
| UI2 | Text commit toolbar clickability | Select Text, place annotation, Ctrl+Enter commit; toolbar Pen (or prior tool) selected and all 21 controls clickable immediately after commit | |
| UI3 | Same-HWND Z-order | Toolbar draws above overlay input on pointer monitor; no separate toolbar window; toolbar clicks never start strokes behind toolbar | |
| UI4 | Dual-monitor pointer placement | Toolbar appears on monitor under cursor at activation; drawing works on BenQ and Lenovo before any toolbar interaction on either monitor | |
| UI5 | Text editor per monitor | Text editor opens beside click point on each monitor; Escape removes editor and restores tool; no topmost text window | |
| UI6 | Eraser + text cross | Text editing → switch to Eraser → empty click → drag erase → Close during eraser; no stuck mouse capture; toolbar hit-testable after each step | |

## Toolbar build verification gates (BT1–BT7)

Run on published `FaktumScreenMarker.exe` on Kone1 dual-monitor setup (BenQ + Lenovo). **Do not mark passed unless observed on hardware.**

| ID | Scenario | Expected | Observed |
|----|----------|----------|----------|
| BT1 | Pen/Line/Arrow/Rect/Ellipse uninterrupted | All five drag tools draw repeatedly in one session without deactivate | |
| BT2 | Text one-shot | Text opens non-modal editor; Ctrl+Enter commits; Escape cancels; toolbar stays clickable; returns to prior tool (Pen default) | |
| BT3 | Eraser drag session | Eraser removes topmost on drag; empty click no jam; capture released on mouse-up | |
| BT4 | All 21 toolbar controls | 7 tools + text font size + 6 colors + 3 widths + Undo/Redo/Clear/Close each work repeatedly in one session | |
| BT5 | Color/width selection | Visible selected state; affects next object only; persists restart | |
| BT6 | Undo/Redo/Clear | Undo restores z-order; clear double-confirm; redo branch cleared on new draw | |
| BT7 | Close + re-activate | Close cancels interactions, releases capture, clears drawings; Ctrl+§ fresh session | |

## Crash-regression gates (CR1–CR4)

Run on published `FaktumScreenMarker.exe` after automated verification succeeds. **All four must pass before M2a–M2d resume.**

| ID | Scenario | Expected | Observed |
|----|----------|----------|----------|
| CR1 | BenQ monitor — first freehand stroke | Stroke visible; mouse-up completes without crash | |
| CR2 | BenQ monitor — ten sequential freehand strokes | Each stroke commits; no crash; overlay remains usable | |
| CR3 | Laptop + BenQ — alternating monitors | Strokes isolated per monitor; no crash switching monitors | |
| CR4 | Long complex freehand (slow drag, many points) | Simplifies and commits; no crash or hang | |

## Toolbar and same-session tool switching gates (TS1–TS4)

Run on published `FaktumScreenMarker.exe` on Kone1 dual-monitor setup (BenQ + Lenovo). **All four must pass before M3–M19 continue.**

| ID | Scenario | Expected | Observed |
|----|----------|----------|----------|
| TS1 | Initial tool selection | Pen selected by default; first stroke draws pen; toolbar buttons respond | |
| TS2 | Same-session multi-tool switching | After first drawing, select Line → Arrow → Rect → Ellipse → Pen repeatedly; each tool draws correct shape; prior drawings remain; no deactivate/reactivate required | |
| TS3 | Toolbar clickability after drawing | After each drawing interaction, toolbar buttons remain clickable; Line/Arrow/etc. change `ActiveTool`; pressed/highlighted state follows selection | |
| TS4 | Dual-monitor shared session | Toolbar clickable on pointer monitor; draw on BenQ and Lenovo in one session; tool selection shared; pointer move does not reset tool; display rebuild does not reset tool unexpectedly | |

## Text font size gates (TS-text-size)

Run on published `FaktumScreenMarker.exe` on Kone1 dual-monitor setup. **Do not mark passed unless observed on hardware.**

| ID | Scenario | Expected | Observed |
|----|----------|----------|----------|
| TS-text-size-1 | Default and selector | Toolbar shows **Text.FontSize** combo default 24; presets 16/24/32/48/64/96 available | |
| TS-text-size-2 | Live editor preview | Select Text, place editor, change font size in toolbar; open TextBox font size updates immediately | |
| TS-text-size-3 | Per-annotation size | Commit two text labels at different sizes; each keeps its size; changing selector does not alter committed text | |
| TS-text-size-4 | Persistence | Preferred size survives deactivate/restart; invalid saved value snaps to nearest preset | |

## Active-session keyboard shortcut gates (KS1–KS3)

Run on published `FaktumScreenMarker.exe` on Kone1 dual-monitor setup. **Do not mark passed unless observed on hardware.**

| ID | Scenario | Expected | Observed |
|----|----------|----------|----------|
| KS1 | Tool and style shortcuts | Q/W/E/A/S/Z/X select tools (Z one-shot); 1–3 set width; 4–9 set color; toolbar visual state matches | |
| KS2 | Dual-monitor routing | Shortcuts work from either monitor overlay in one session; shared tool/color/width state | |
| KS3 | Text editor isolation | While TextBox focused, Q/1/4 do not fire; Ctrl+Enter commits; Escape cancels; Ctrl+§ toggle still works from tray/hotkey path | |

## Startup and tray

| ID | Scenario | Expected | Observed |
|----|----------|----------|----------|
| M1 | Launch app from published exe | Tray icon visible; no overlay windows | |
| M16 | Second instance | Second launch in same user session exits quietly; first keeps tray; other Windows users not blocked | |

## Ctrl+§ activation and deactivation

| ID | Scenario | Expected | Observed |
|----|----------|----------|----------|
| M2 | Ctrl+§ activates drawing (Finnish layout) | Overlays and toolbar appear on all monitors; **left-click drag draws on overlay; underlying app receives no pointer input while active** | |
| M2a | Overlay blocks left-click/drag | Drag pen stroke on empty overlay area; Notepad behind overlay does not select text or receive clicks | |
| M2b | Toolbar remains independently clickable | Toolbar buttons respond; click on toolbar does not draw stroke behind toolbar | BLOCKED |
| M2c | Right/middle mouse blocked on overlay | Right-click and middle-click on overlay empty area do not reach underlying app | BLOCKED |
| M2d | Cross cursor on overlay | Crosshair cursor visible over overlay drawing area | BLOCKED |
| M2e | Ctrl+§ activates drawing (English layout) | Same physical key toggles using English VK mapping | BLOCKED |
| M2f | Switch Finnish → English → Finnish while idle | All layout VK mappings remain registered; Ctrl+§ still toggles without focus | BLOCKED |
| M4 | Second Ctrl+§ deactivates | Overlays close; drawings destroyed; prior foreground restored best-effort | BLOCKED |
| M3 | Holding Ctrl+§ does not repeat-toggle | Single transition despite held keys (`MOD_NOREPEAT`) | BLOCKED |
| M4a | § without Ctrl | Normal typing in Notepad; no toggle | BLOCKED |
| M4b | Ctrl alone | No toggle | BLOCKED |
| M4c | Successful activation | § does not type into Notepad | BLOCKED |
| M10 | Rapid double Ctrl+§ while activating | Ends in Idle (no stuck Activating; no duplicate activation) | BLOCKED |
| M11 | Rapid triple Ctrl+§ while activating | Ends in Active | BLOCKED |
| M11a | Rapid toggle 1–4 from Idle | 1 Active, 2 Idle, 3 Active, 4 Idle | BLOCKED |
| M18 | Primary hotkey conflict (if reproducible) | Tray notification; fallback still works | BLOCKED |
| M19 | Fallback registration failure (if reproducible) | Tray notification; Ctrl+§ or tray menu still documented | BLOCKED |
| M19a | Layout change while running | Primary hotkeys re-register once; no duplicate toggles per press | BLOCKED |

## Drawing tools

| ID | Scenario | Expected | Observed |
|----|----------|----------|----------|
| M3a | Pen draw + toolbar Undo | Stroke appears then disappears immediately without mouse move | BLOCKED |
| M12 | Text tool + Escape | Text entry commits with Ctrl+Enter; Escape cancels preview | BLOCKED |
| M13 | Eraser tool | Topmost object on active monitor removed | BLOCKED |
| M14 | Clear double-confirm | First click arms; second clears all monitors | BLOCKED |

## Settings and persistence

| ID | Scenario | Expected | Observed |
|----|----------|----------|----------|
| M5 | Settings save round-trip | `settings.json` updated with color/stroke/toolbar placement/language; no drawing content | |
| M5a | Toolbar color/stroke persistence | Changes survive deactivation and restart | |
| M5b | Toolbar position persistence | Monitor-relative position restored on same monitor | |

## Fallback hotkey

| ID | Scenario | Expected | Observed |
|----|----------|----------|----------|
| M6 | Ctrl+Shift+F12 fallback | Reliably toggles drawing mode | BLOCKED |

## Multi-monitor, DPI, and toolbar rebuild

| ID | Scenario | Expected | Observed |
|----|----------|----------|----------|
| M7 | Multi-monitor draw isolation | Object drawn on monitor A not visible on monitor B | BLOCKED |
| M8 | Mixed DPI 125%/150% | Overlays cover each monitor; toolbar usable; no coordinate drift | BLOCKED |
| M8a | Secondary monitor toolbar default | Toolbar appears centered on pointer monitor including negative virtual origins | BLOCKED |
| M8b | Text editor placement | Text dialog beside clicked point on secondary/mixed-DPI monitor | BLOCKED |
| M8c | Toolbar above overlays | After drawing on overlay, toolbar remains clickable and above overlay Z-order | BLOCKED |
| M8d | Toolbar visible while drawing | Toolbar stays visible during active drawing session | BLOCKED |
| M8e | Toolbar survives attach/detach/DPI | Toolbar remains visible after monitor attach/detach or DPI change rebuild | BLOCKED |
| M15 | Display topology change while active | Session cleared once; overlays rebuilt once; toolbar detached before owner close; tray notification | BLOCKED |

## Startup integration

| ID | Scenario | Expected | Observed |
|----|----------|----------|----------|
| M17 | Start with Windows toggle | Registry Run entry added/removed; survives restart | |

## Automated smoke (CI/agent)

| ID | Scenario | Expected | Observed |
|----|----------|----------|----------|
| M20 | Smoke test `--smoke-test` | Process exits 0 without UI hang | |
| M21 | Platform smoke `--platform-smoke-test` | Hidden HWND; single WndProc; all layout primary mappings resolved and registered; fallback registered; every valid primary ID one toggle; fallback one toggle; unknown ID zero toggles; complete unregister; overlay+toolbar activation; toolbar above overlay; WindowFromPoint inside→toolbar, outside→overlay; synthetic drawing does not bury toolbar; second tool updates ActiveTool; clean exit 0 | |

## Notes

- Automated regression tests cover Ramer–Douglas–Peucker simplification (iterative, non-mutating), freehand input validation, sequential stroke controller state, overlay input surface, pointer capture, extended-style verification, transition actions, parity toggles, multi-layout hotkey registration, in-overlay toolbar rebuild ordering, **WPF hit-test validation** (all 20 controls + empty input area), **routed toolbar input** (no AutomationPeer-only paths), same-session tool selection before first drawing, interaction-tool latching, **ToolbarInteractionCoordinator** (Idle/DragDrawing/Erasing/TextEditing/Deactivating), **19-step uninterrupted session**, injectable text editor host, eraser capture invariants, idempotent shutdown, history trimming, monitor scoping, settings isolation, and platform helpers.
- M20–M21 and the 176 automated tests pass in CI/agent verification; **UI1–UI6, BT1–BT7, CR1–CR4, TS1–TS4, and M1–M19 remain outstanding** on target hardware.
- **UI1–UI6 must be recorded before BT1–BT7 continue. BT1–BT7 must pass before TS1–TS4 and M3–M19 continue. CR1–CR4 must pass before M2a–M2d continue.**
- Privacy-safe diagnostic log: `%LOCALAPPDATA%\FaktumAI\ScreenMarker\logs\diagnostics.log` (exception type, operation name, monitor id only — no coordinates, drawings, or screen content).
