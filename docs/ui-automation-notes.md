# Driving a desktop UI: what this repository has measured

> **Most of this is a pointer now.** `AGENTS.md` §8 says Avalonia and drivable-UI lessons have one
> living copy, in `JanusMael/Bennewitz.Ninja.XamlQuality` — `docs/avalonia-gotchas.md` for framework
> foot-guns and `docs/ai-drivable-ui.md` for driving and verifying by agent. The Windows batches of
> 2026-09-30, 10-08, 10-09 and 10-10 **landed there on 2026-10-13**, in pull requests
> [#59](https://github.com/JanusMael/Bennewitz.Ninja.XamlQuality/pull/59),
> [#60](https://github.com/JanusMael/Bennewitz.Ninja.XamlQuality/pull/60) and
> [#67](https://github.com/JanusMael/Bennewitz.Ninja.XamlQuality/pull/67), several of them changed by
> XamlQuality's own measurement. What is left here is what did **not** land: the X11 batch, the
> entries nobody has re-measured, one disagreement, and whatever is specific to DiffView or to one of
> these machines.
>
> ⚠ **The reduction is conditional on landing, never on sending.** Each row below was checked against
> the merged documents rather than taken from the report of them.

## The instrument

`scripts/drive-demo.cs` is one .NET 10 file-based app with a platform back end, plus thin `.sh` and
`.ps1` wrappers — plan 00023. Its verbs are `launch`, `window`, `key`, `click`, `drag`, `mark`,
`popup`, `geometry`, `capture`, `hover` and `probe`, chained with `then`. The X11 back end drives
through `xdotool`, `xwininfo` and `ffmpeg`; the Windows back end uses UI Automation, `SendInput` and
`PrintWindow` directly, with `scripts/drive-demo-uia.ps1` for the one part it cannot reach.

## What landed upstream, and what XamlQuality changed on the way

Measured by XamlQuality in two Windows Sandbox sessions on Avalonia 12.1.3 with Fluent, one with the
host's GPU shared and one without, from a per-monitor DPI-aware driver, with headless probes beside
them and causes read in Avalonia's source at the tag.

| What this repository measured | Outcome | Lives in |
|---|---|---|
| A hit-test answers from the last frame drawn | landed, **changed**: it holds on a real window too, until the next frame is composed — stale at once and after a `RequestAnimationFrame` callback, right 50 ms after it, 10 of 10. Headless, `RunJobs` alone or a render tick alone leaves it stale; `RunJobs` then a tick, or `CaptureRenderedFrame()`, settles it | `avalonia-gotchas.md` |
| `PrintWindow` is solid black, and reports success, without `PW_RENDERFULLCONTENT` | landed, at 12.1.3 with the default `Win32PlatformOptions`, GPU shared and not. **The cause is not traced** | `avalonia-gotchas.md` |
| Synthetic pointer motion raises a tooltip on Windows | landed, from `SetCursorPos` alone | `ai-drivable-ui.md` §5 |
| A click must be guarded, because it goes to whatever is topmost at that pixel | landed, **changed** — see *The guard this repository should adopt* below | `ai-drivable-ui.md` |
| `SetForegroundWindow` is unreliable, so asking is not evidence of getting | landed, **changed**: it was refused to a process that had sent **no input**, and granted to one that just had. Not, as recorded here, refused for any process that does not already hold the foreground | `ai-drivable-ui.md` |
| Holding the foreground is not having something focused inside | landed, **narrowed**: with nothing focused, keys reach the **window's own key bindings** and not a view's; one click on the view fixes it. Not "nothing at all" | `ai-drivable-ui.md` |
| A part hidden with `IsVisible=false` is genuinely absent from the tree | landed, and **absent from the raw view too** | `ai-drivable-ui.md` §8 |
| A gesture's success is not its effect (the `drag` verb) | landed as written | `ai-drivable-ui.md` |
| An Avalonia popup is its own top-level window | landed, **changed** — see *Where a menu entry lives* below | `ai-drivable-ui.md` |
| A run that stops early is not a pass | landed as method | `ai-drivable-ui.md` §5, step 6 |
| Avalonia's `MenuItem` offers no way to open or run it | landed, **changed**: no `ExpandCollapse` at 12.1.2 **or** 12.1.3, the peer being the same file in both — the one `MenuItem` with it in their survey was the window's **System** menu item, which is Windows'. A checkable `MenuItem`'s missing `Toggle` is a defect in the Windows bridge, which asks for the provider off the UI thread; a peer caching `ToggleType` on the UI thread works around it | `avalonia-gotchas.md` |
| A by-hand driver with a person watching is a position of its own | landed as a **fifth layer**: attended real input on the person's own desktop, the window checked unobstructed and focused before every event, refusing otherwise, and never in an unattended sweep | `ai-drivable-ui.md` §4 |
| A DPI-unaware process is handed virtualised coordinates | landed | `ai-drivable-ui/SharedDesktopHarnessJournal.md` |

### Where a menu entry lives, and why this repository's reader still works

XamlQuality measured the tree, and it is the opposite way round from what a reader would guess: a
menu's **items** — in XAML, built in code, or in a context menu alike — sit under the **main
window's** element in *both* views, while each submenu and context menu is **drawn** in a popup
window of its own, which is the items' nearest window in the **raw** view. The XAML/run-time
asymmetry this repository once recorded did not reproduce.

`scripts/drive-demo-uia.ps1` never walks upward, so it is not exposed to the view question: it
enumerates the process's top-level windows and walks each id path **downward** with
`FindFirst(TreeScope.Descendants, …)`, reporting the window the path resolved under. A menu entry is
a tree descendant of the main window, so that reader can report the **main** window for an entry
whose pixels are in the popup — which is exactly the false refusal plan 00033 found on the
colour-blind palette entry, and `drive-demo.cs`'s `Drawn` is what repairs it, by trusting the
pixel's own top-level window when it belongs to the same process.

### The guard that asked the pixel what the pixel was — adopted

⛔ **The defect was circularity, not a missing check.** `Drawn` resolved `WindowFromPoint` then
`GetAncestor(GA_ROOT)` for the pixel it was about to click and accepted that window whenever it
belonged to the demo's process; `Beneath` then checked the same pixel against the answer. The expected
window was therefore derived from the pixel being tested, so for any window of this process it agreed
with itself and **`Beneath` could not fail**. It caught other applications — the case it was written
for — which is exactly why the hole went unseen. What it let through was the demo's **own F12
live-log window** lying over the target: the click went into the log window and the driver reported
success.

**Adopted 2026-10-13.** The expected window now comes from the **tree**:
`scripts/drive-demo-uia.ps1` walks `TreeWalker.RawViewWalker` from the element to its nearest ancestor
carrying a window handle and reports it as a tenth field, and `Drawn` reads that field. The raw view is
load-bearing — a control-view walk passes the popup and would reintroduce plan 00033's false refusal.
`0` means the reader could not name the window, and it is **refused rather than fallen back on**.

⚠ **The second half of XamlQuality's method is declined, with a reason**: also checking that UI
Automation's element at the point is the element itself catches "right window, wrong element", and this
demo does not reach that state — `OverlayPopups` stays unset, so every menu, tooltip and context menu
is a top-level window rather than an in-window overlay. It would cost a second reader round-trip per
click, the coordinates not existing until after the first read. Revisit if `OverlayPopups` is ever set.

✅ **Verified on a real window, JANUSWAX2, 2026-10-13.** The field's *reading* is headless-tested both
ways; the raw-view **walk** needed a desktop, and got one in three readings: the guard refuses an
obstruction **proven topmost at the click pixel** and names it; it allows the same click once the
obstruction is gone; and a pane-menu entry reads as `in 1048780 drawn in 1049142` — found under the
main window's element, drawn in a popup of its own — with the click on it allowed. The third is the
only case the field changes an answer in, and it is XamlQuality's inverted finding confirmed here.

⛔ **The harness that took the first of those readings reported FAIL, and the guard was not what was
wrong.** It was DPI-unaware on a console at 200% — `1113x756` read for a `2200x1440` window, the trap
§9 already records — and it never checked that the obstruction it built was at the pixel it clicked,
so "allowed" was a conclusion from an unproven premise. The repaired harness measures the pixel and
reports **UNREAD** when the premise fails. ⚠ An unexplained difference stays on the record: the
repaired one printed `per-monitor DPI aware: False` and still read physical coordinates where the
first, launched the same way, read virtualised ones. The verdict does not rest on it — the driver named
the obstructing window itself.

## Linux and X11 — still owed upstream

⛔ **The 2026-09-28 batch has NOT landed** (confirmed by XamlQuality, 2026-10-13), so none of this is
a pointer yet and the copy here is the only one. `AGENTS.md` §9 is the long-form record. The parts
that are not specific to this machine, and therefore belong upstream:

- An Avalonia popup — a menu or a tooltip — is its own unnamed, **override-redirect** top-level
  window. Find it by diffing the root's children against a mark taken before the action, never by
  counting or by name.
- A popup opens on one layout pass and lays out on the next, so a frame taken between them is an
  empty box. Wait until its geometry holds still.
- A window manager may keep override windows of its own that are not menus — mutter keeps a
  screen-sized guard window and a 1×1 off screen — so a finder that takes "the newest override
  window" or "the tallest" picks one of those. Filter by size and refuse a click outside the popup's
  own bounds.
- `xwininfo`'s per-window field **labels** are fixed strings and do not translate, but the root's
  child list contains free text, so read that by its two ends — the id first, the geometry last.
  A harness that parses English output fails exactly when someone is checking a translation.
- A reparenting window manager's frame is a window of its own: `xdotool getwindowgeometry` counts its
  offset twice, so take positions from `xwininfo`'s absolute upper-left, and grab the **client**
  window, not the frame.
- A grab that succeeds and comes back solid black means the session is not presenting — a closed
  remote-desktop connection leaves one. Check the pixels; never read the app's state from them.

### The tooltip re-measurement, which XamlQuality asked for

Their `xdotool` entry records the **opposite** — that a tooltip cannot be driven under X11 — and it
stands unchanged until X11 is measured on their side. This repository's two readings, sent 2026-10-13:

| | |
|---|---|
| **2026-09-13** | `xdotool mousemove` onto a line number raised **no** tooltip. What made it fail was never established |
| **2026-09-28** | The tooltip appeared on **ten trials of ten**, Avalonia 12.1.2 — the same version as the first date, the library unchanged between them |
| Environment | xdotool 3.20160805.1, Xwayland 24.1.6, GNOME Shell 48.7; `XDG_SESSION_TYPE=wayland` with XWayland at `DISPLAY=:0`, the demo an XWayland client of Avalonia's X11 backend |
| Method | `xdotool mousemove` to the margin, then read the popup as an override-redirect window by the root-child diff. Approached from **outside** the window, from its **far corner**, from **another line of the same margin**, and from the screen point **(5, 5)** where the older scratch script parks |
| What appeared | The change marker's *Change 3 of 5 · +0 −0 ~1* and the line number's *Line 9 · right line 11*; in `pt-BR`, *Linha 9 · linha 11 à direita* |
| Cross-check | The old scratch script's capture and the driver's are identical **pixel for pixel**, so the driver is not the variable |

## One disagreement, and it is narrower than it looks

**Raising a window before clicking an entry of a menu it opened.** This repository recorded that the
raise light-dismisses the menu, so the click lands on the pane underneath — a plausible-looking wrong
result rather than a failure. XamlQuality **did not reproduce it** and left the entry out: four ways
of raising the window left the menu open, and a click on an item afterwards ran it.

⚠ **The two readings are compatible, and the qualifier is the whole of it.** Theirs was measured
*with the window already active*, where a raise changes no foreground; and their own note that
**another application taking the foreground closed the menu** is this repository's case, because the
only reason to raise is that the window is *not* in front. So:

- **Raising an already-active window is harmless** — theirs, and new here.
- **Taking the foreground closes the menu** — both, and it is the case the driver's rule is about.

The rule `AGENTS.md` §9 and `DECISIONS.md` state therefore **stands as written**, and the gap worth
telling XamlQuality is that leaving the entry out drops the inactive-window case their own
measurement confirms.

## Specific to DiffView or to these machines, so it stays here

- **The demo's log is a live oracle on Windows.** A click at 10:21:02.284 was readable from outside
  the process within a second, by both `tail` and a `FileStream` opened `FileShare.ReadWrite`, the
  directory entry's reported length matching the stream's exactly. The sink is ClaudeForge's
  `BucketedRollingFileSink`, whose inner `Serilog.Sinks.File` is configured `buffered: false` with
  `flushToDiskInterval: 2s`.
- ⛔ **An empty log after a click means the click did not arrive — not that the log is slow.** This
  cost an hour and a wrong diagnosis here: clicks into an obscured demo produced no lines, which was
  read as buffering when the input had gone into another application entirely. **The log not moving
  is the signal, and it is the correct one.**
- ⛔ **A running demo locks its own assemblies**, so the solution cannot rebuild while it is up:
  `dotnet test` fails with MSB3027/MSB3021 naming the demo's process. No Linux equivalent. Close it
  by the pid the launch recorded, never by name — a pattern matching the demo's name also matches the
  agent harness's own shell (`AGENTS.md` §9).
- **Where DiffView sits on patterns.** Its peers advertise **no pattern at all**, by decision
  (`AGENTS.md` §1): the views are `Group`, a pane `Edit`, the margins `Custom`. Pattern-driving is
  therefore not available here, and nothing should be added merely so a driver can poke it — which is
  the TailBlazer port's own rule. The one reading that does come through a pattern is the scroll
  offset, off the stock scroll bars inside AvaloniaEdit's template, which are not ours.
- **Find the app by process id and the window by title, not one or the other.** The F12 log window
  shares the process, so a pid alone is ambiguous; two demos on one desktop share a title, so a title
  alone is ambiguous.
- ⛔ **Never name a PowerShell variable `$pid`** — that is the harness's own process id. XamlQuality's
  rule; the parameter here is `-DemoProcessId`.

## Not measured by either repository, and left out upstream

- **`PW_CLIENTONLY | PW_RENDERFULLCONTENT` gives exactly the client area** — 1100×720 for a window
  whose frame is 1116×759. Taking the client area is what makes `click left 88 16` mean the same
  thing on both platforms, because under X11 the window manager's frame is a separate window.
- **`PrintWindow` draws a window that is behind other windows**, which is what lets the capture half
  run on a desktop someone is working at. TailBlazer reports it also works on a **locked** session
  (their measurement, not reproduced here).
- **A PNG can be written by hand in about 60 lines** — `ZLibStream` for the deflate, a hand-rolled
  CRC-32, one unfiltered scanline per row, colour type 2 — so a driver needs no image dependency.
  Verified by decoding with GDI+.
- **The arrows and the navigation block must carry `KEYEVENTF_EXTENDEDKEY`.** They share their
  virtual-key codes with the numeric keypad, so without the flag a `Down` can arrive as the keypad's
  `2`.
- **UI Automation's managed client is reachable from PowerShell 7** — `Add-Type -AssemblyName
  UIAutomationClient` and `UIAutomationTypes` — and **not** from a portable `net10.0` file-based app,
  because `System.Windows.Automation` lives in the Windows Desktop framework. Hence a `.ps1` beside
  the `.cs`, the only place this repository's script convention bends.
- **Interop trades.** `[LibraryImport]` requires `AllowUnsafeBlocks`, which a file-based app does not
  have; `[DllImport]` needs nothing, and for a dozen flat Win32 calls it is the right trade. The
  alternative to the PowerShell helper was hand-declaring UI Automation's COM interfaces, where
  **vtable order is the whole contract**: the Windows SDK's own
  `Include/<version>/um/UIAutomationClient.idl` is the authority, and the documentation lists members
  alphabetically, which is a trap. The virtual-key codes, the `SendInput` flags and
  `PW_RENDERFULLCONTENT` were read from `Include/<version>/um/WinUser.h` rather than recalled.

## A defect that was not one, recorded so it is not "found" again

This one is `Bennewitz.Ninja.AppServices`' and was never XamlQuality's.

`BucketedRollingFileSink` builds its inner `Serilog.Sinks.File` logger with
`.MinimumLevel.Information()` hard-coded, which reads like a filter that would drop a host's `Debug`
events from the file while the Trace sink and the F12 window still showed them. **It is not.** The
sink formats each event itself and re-emits the finished line at Information
(`_currentLogger?.Information("{RawLine}", …)` in `Emit`), so the inner minimum never filters
anything, and the real level is already inside the text. The outer pipeline honours
`options.MinimumLevel` normally. Checked in `Bennewitz.Ninja.AppServices`, the live home of this
code; ClaudeForge's copy, which DiffView still consumes as a hand-packed `1.0.1`, is identical.

The lesson is about reading, not logging: **one line of a class is not the class.** The conclusion was
drawn from the `LoggerConfiguration` alone, without reading the `Emit` twenty lines above it.

## Still owed

- **The X11 batch of 2026-09-28**, above, which has not landed. The tooltip environment and method
  went to XamlQuality on 2026-10-13 at their request; their entry stands until they measure X11.
- **The macOS back end** is specified in plan 00023 and not written; its permission grants are
  interactive and cannot be scripted. Open item 9.
- **The interactive pass's exit code**, open item 18: it has no `exit` path, so it returns 0 with a
  failure list, and item 17's own harness read that 0 as a pass over four failed assertions.
- **A by-hand pass at a scaling other than 100%** exercises less than it looks like it does. On a
  console at 200% the demo is `2200x1440`, the View menu's palette entry falls outside its scrolling
  popup and is absent from the tree, and the splitter and map drags press coordinates that belong to a
  `1100x720` client. The driver refuses rather than misfiring, so the frames are still a look at that
  scaling — but the gestures are not read.
- Whether `PostMessage(WM_LBUTTONDOWN/UP)` or `AttachThreadInput` can deliver a click to an unraised
  Avalonia window is **unmeasured** — by this repository, by TailBlazer and by XamlQuality, none of
  whom would guess. The caveats to test are that posted messages bypass hit-testing, do not move the
  real cursor, and would change what hover and anything reading the pointer's position see.
