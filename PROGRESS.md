# Progress

## Resume

**Phase 0 — Bootstrap** of [plan 00001](plans/00001-side-by-side-diff-control.md) (approved,
commit `ea23596`) is complete on this machine except for one manual check at a real window:
Debug → *Throw on the UI thread* must show the fatal-error dialog with a working copy button,
and F12 must open the live log, in the Debug build and in the trimmed publish. Everything else
in the phase's *done when* list holds. Next is **Phase 1 — the virtual-padding spike**,
time-boxed to two working days, whose outcome goes to `DECISIONS.md` before any pane code is
written.

## Phases

| Phase | Status | Notes |
|---|---|---|
| 0 Bootstrap | done (manual dialog check pending) | 7 tests across three tiers; trim-check clean |
| 1 Virtual-padding spike | not started | time-boxed to two working days |
| 2 Theme-key audit and exhaustive dictionaries | not started | `theme-audit inventory` exists and packs |
| 3 Core model, probing, search engine | not started | |
| 4 Pane presenter, padding, gutters | not started | |
| 5 Composite control, scroll sync, headers, status strip, theming | not started | |
| 6 Word-level highlights and options | not started | |
| 7 Navigation, minimap, connectors, tooltips | not started | |
| 8 Find | not started | |
| 9 Syntax highlighting | not started | |
| 10 Scale, visibility, accessibility | not started | |
| 11 Inline (unified) view | not started | optional |

## Phase 0 verification

| Check | Result |
|---|---|
| `dotnet build DiffView.slnx -warnaserror` | clean |
| `dotnet test --solution DiffView.slnx` | 7 passed: 1 Core unit, 3 ThemeAudit unit, 1 accessibility guard, 2 rendered smoke snapshots (Semi light and dark) |
| Headless host renders | `CaptureRenderedFrame()` returns an 800×500 frame; both snapshots verified |
| Demo boots under Semi and under Fluent | launched on this Wayland session with `--theme semi` and `--theme fluent`; the log shows the flags summary, `Starting`, the log directory and `XDG_SESSION_TYPE` |
| Trimmed publish (`linux-x64`, self-contained, `TrimMode=link`) | succeeds, 0 IL warnings, 48 MB output, boots and logs |
| Reference self-heal | removing `reference/DiffPlex` and building the Avalonia test project re-fetched it at its pin; `git status` shows nothing under `reference/` but the manifest and README |
| `theme-audit inventory` on Semi Light | 624 keys in 44 files; the tool packs into the local feed |
| Deliberate throw → dialog, F12 live log | **manual, pending** — needs a click at the window |

## Upstreamed to ClaudeForge

| Change | Reference | State |
|---|---|---|
| `LayeredEditors.Avalonia.Diagnostics` sets `PackageReadmeFile` but ships no README; `dotnet pack` fails without an override | — | to open |

## Measurements

| What | Value | Where |
|---|---|---|
| Test run, all three projects | ~0.9 s | this machine, Debug |
| Trimmed self-contained publish of the demo, linux-x64 | 48 MB | `dotnet publish -c Release -r linux-x64 --self-contained true` |
