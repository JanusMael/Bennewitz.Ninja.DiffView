# Driving a desktop UI: what this repository has measured

> ⚠ **This is a staging document, not a home.** `AGENTS.md` §8 says Avalonia and drivable-UI lessons
> have one living copy, in `JanusMael/Bennewitz.Ninja.XamlQuality` — `docs/avalonia-gotchas.md` for
> framework foot-guns and `docs/ai-drivable-ui.md` for driving and verifying by agent. Everything here
> is owed to those two documents. It is written down first because a lesson measured and not recorded
> is a lesson paid for twice, and because plan 00023 is not finished: what is upstreamed goes when the
> approach is fully worked out, and what stays here afterwards is a pointer plus whatever is specific
> to DiffView or to one of these machines.
>
> Every claim below says where it was measured or who measured it. Anything taken from another session
> on trust is marked as such and is **not** this repository's evidence.

## The instrument

`scripts/drive-demo.cs` is one .NET 10 file-based app with a platform back end, plus thin `.sh` and
`.ps1` wrappers — plan 00023. Its verbs are `launch`, `window`, `key`, `click`, `mark`, `popup`,
`geometry`, `capture` and `hover`, chained with `then`. The X11 back end drives through `xdotool`,
`xwininfo` and `ffmpeg`; the Windows back end uses UI Automation, `SendInput` and `PrintWindow`
directly, with a PowerShell helper for the one part it cannot reach.

## Windows

Measured on 2026-10-08, Windows 11 Pro 26200, .NET 10.0.401, Avalonia 12.0.0, PowerShell 7.6.6,
against the DiffView demo, unless a line says otherwise.

### Capture

- ⛔ **`PrintWindow` without `PW_RENDERFULLCONTENT` returns an entirely black frame for an Avalonia
  window, and reports success.** Mean grey 0, brightest pixel 0, over the whole window; the call
  returns true and `GetLastError` is clean. With the flag, real content — mean grey 46.8, brightest
  250, on the same window one second apart. This is a **false green with no error anywhere**, and it
  is the same shape as the X11 black frame (`AGENTS.md` §9, a session that is not presenting) arrived
  at by a completely unrelated route. A capture is therefore checked for being all black whatever
  flags were passed.
- **`PW_CLIENTONLY | PW_RENDERFULLCONTENT` gives exactly the client area** — 1100×720 for a window
  whose frame is 1116×759. That matters beyond tidiness: under X11 the window manager's frame is a
  separate window and `demo` is the client, so taking the client area here is what makes
  `click left 88 16` mean the same thing on both platforms.
- **`PrintWindow` draws a window that is behind other windows**, which is what makes a capture usable
  on a desktop someone is working at. TailBlazor reports it also works on a **locked** session
  (their measurement, not reproduced here).
- **A PNG can be written by hand in about 60 lines** — `ZLibStream` for the deflate, a hand-rolled
  CRC-32, one unfiltered scanline per row, colour type 2 — so a driver needs no image dependency.
  Verified by decoding with GDI+: 24-bit PNG, right dimensions, right way up, right channel order.

### Finding parts

- **UI Automation's managed client is reachable from PowerShell 7** on Windows:
  `Add-Type -AssemblyName UIAutomationClient` and `UIAutomationTypes`. It is **not** reachable from a
  portable `net10.0` file-based app, because `System.Windows.Automation` lives in the Windows Desktop
  framework and referencing it would make the whole driver Windows-only. Hence a `.ps1` beside the
  `.cs`, which is the only place this repository's script convention bends.
- ⛔ **Never search `RootElement` with `TreeScope.Descendants`.** It walks every application on the
  desktop. TailBlazor measured one search at 17 s, and one harness at 94 s on a quiet desktop against
  406 s on a busy one with identical code; XamlQuality's `ai-drivable-ui.md` records an unscoped
  search that found and invoked **another application's** button. Find the windows as
  `RootElement.FindAll(TreeScope.Children, ProcessIdProperty == pid)`, then search descendants within
  one of those.
- ⛔ **Never name a PowerShell variable `$pid`** — that is the harness's own process id. XamlQuality's
  rule; the parameter here is `-DemoProcessId`.
- **Find the app by process id and the window by title, not one or the other.** The demo's F12 log
  window shares its process, so a pid alone is ambiguous; two demos on one desktop share a title, so
  a title alone is ambiguous. DiffView's driver narrows by pid when a launch recorded one and picks
  the main window by title within that.
- **A part hidden with `IsVisible=false` is genuinely absent from the tree**, confirmed on the demo:
  with the unified view, the viewer, the find bar and the banner's action switched off, a search for
  `Unified`, `Viewer`, `FindBar` and `BannerAction` returns nothing while every visible part is found.
  An error message should say so, because "not found" reads as a defect and usually is not.
- **An Avalonia context menu is its own top-level window on Windows**, as it is under X11 — so it is
  found from the desktop root together with the process id, and the `mark`/`popup` diff works
  identically on both platforms. Measured: right-clicking a pane produced a new 393×434 top-level
  window at the pointer.
- **DPI: declare `PER_MONITOR_AWARE_V2` before reading any rectangle.** A process that has not is
  answered in virtualised coordinates on any display past 100%, and every click lands scaled.
  `GetDpiForWindow` reports 96 on this machine, so nothing here would have caught it.

### Input, and the part that is genuinely hard

- ⛔ **An injected click goes to whatever window is topmost at that pixel — silently, in whatever
  application that is.** Measured the expensive way: with the demo behind the Claude desktop window,
  every `SendInput` click went into Claude, the driver reported success, and the demo's log stayed
  empty. **A driver must check before it clicks**: `WindowFromPoint`, then `GetAncestor(GA_ROOT)`, and
  refuse when the top-level window is not the one the click was aimed at. That guard caught this
  immediately and named the window in the way.
- ⛔ **Do not raise the window to fix that.** Raising the demo over the person's work means the demo
  takes *their* clicks instead — the same defect pointed the other way. `SetForegroundWindow` is also
  refused outright for a process that does not already hold the foreground, so the raise is
  unreliable as well as rude.
- **Raising before clicking a menu is worse than useless**: taking the foreground light-dismisses the
  menu, so the click then lands on whatever was underneath it. A driver that raises must raise the
  window the *target* belongs to, which for a menu entry is the menu's own window — or, better, not
  raise at all.
- **The TailBlazer port, which runs ~31 harnesses on this estate, sends no synthetic input on a
  shared desktop at all** (their account, 2026-10-08). Their three-part answer:
  1. Drive through UI Automation **patterns** where the control honestly has the action.
  2. Where Avalonia's own peer is silent or lies, **fix the application**: give the control an
     internal peer of its own. Never advertise a pattern the peer does not honour — Avalonia's
     `ListBox` peer did exactly that (`Selection`, Avalonia #22151, fixed in 12.1.3; `Scroll`,
     #22038, unfixed) and it cost them false results.
  3. Anything that truly needs a keystroke, a drag, a wheel or a chord moves to a **headless**
     fixture, where `HeadlessWindowExtensions` raises input through Avalonia's own stack with no
     desktop involved.
  Their harnesses that do need the foreground — a `ComboBox` dropdown and a flyout light-dismiss,
  which they measured opening in only 2 of 12 background attempts — run attended on an idle desktop
  and otherwise report INCONCLUSIVE with the reason.
- **Where that leaves DiffView.** Its peers advertise **no pattern at all**, by decision
  (`AGENTS.md` §1): the views are `Group`, a pane `Edit`, the margins `Custom`. So pattern-driving is
  not available here, and nothing should be added merely so a driver can poke it. DiffView's driver
  therefore keeps real input and treats an unobstructed window as a **precondition it checks** rather
  than one it forces — which is honest for an attended by-hand pass, and is the layer XamlQuality's
  document already calls "needs an undisturbed machine".
- **Parking a window belongs in the application, not the driver** (TailBlazor, measured): an *owned*
  modal sent to `HWND_BOTTOM` broke the `ShowDialog` handshake so the result never reached the
  awaiting caller; parking needs the real HWND and frame size, so it hooks `Window.Opened` and an
  outside driver races every window the app opens later; and Windows shows a maximised window in
  front whatever you ask. DiffView has no such test mode and does not need one yet — it is noted
  here so that if one is ever added, it is added to the demo rather than bolted onto the driver.

### Reading back what happened

- **The demo's log is a live oracle on Windows, and `AGENTS.md` §9's instruction holds here.**
  Measured: a click at 10:21:02.284 was readable from outside the process within a second, by both
  `tail` and a `FileStream` opened `FileShare.ReadWrite`, with the directory entry's reported length
  matching the stream's exactly. The sink is ClaudeForge's `BucketedRollingFileSink`, whose inner
  `Serilog.Sinks.File` is configured `buffered: false` with `flushToDiskInterval: 2s`.
- ⛔ **An empty log after a click means the click did not arrive — not that the log is slow.** This
  cost an hour and a wrong diagnosis here: clicks into an obscured demo produced no log lines, which
  was read as "the log must be buffering" when the truth was that the input had gone into another
  application entirely (see the input section below). **The log not moving is the signal, and it is
  the correct one.** Suspect the click before suspecting the sink.
- ⛔ **A running demo locks its own assemblies, so the solution cannot rebuild while it is up.**
  `dotnet test` fails with MSB3027/MSB3021 naming the demo's process. There is no equivalent on
  Linux, where a running binary's file can be replaced. Close the demo before building — by the pid
  the launch recorded, never by name, because a pattern matching the demo's name also matches the
  agent harness's own shell (`AGENTS.md` §9).

### Interop, for a script that must stay portable

- `[LibraryImport]` requires `AllowUnsafeBlocks`, which a file-based app does not have; `[DllImport]`
  needs nothing. For a dozen flat Win32 calls, `[DllImport]` is the right trade.
- The alternative to the PowerShell helper was hand-declaring UI Automation's COM interfaces, where
  **vtable order is the whole contract**. The Windows SDK's own
  `Include/<version>/um/UIAutomationClient.idl` is the authority for that order, and it is on this
  machine; the documentation lists members alphabetically, which is a trap. Not taken, but recorded
  because the next person will weigh it.
- The virtual-key codes, the `SendInput` flags and `PW_RENDERFULLCONTENT` were all read from
  `Include/<version>/um/WinUser.h` rather than recalled.
- **The arrows and the navigation block must carry `KEYEVENTF_EXTENDEDKEY`.** They share their
  virtual-key codes with the numeric keypad, so without the flag a `Down` can arrive as the keypad's
  `2`.

## Linux and X11

`AGENTS.md` §9 is the long-form record and is not duplicated here. The parts that are **not**
specific to this machine, and therefore belong upstream:

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

## What is the same on both, and what is not

| | X11 | Windows |
|---|---|---|
| Addressing | coordinates only — no accessibility bridge here, and plan 00023 leaves AT-SPI out | an `AutomationId` path, scoped process → view → part → margin |
| A popup | its own override-redirect window, found by diffing the root's children | its own top-level window, found from the root in the process |
| Capture | `ffmpeg -f x11grab` by window id | `PrintWindow`, which also works behind other windows |
| Black frame means | the session is not presenting | the `PW_RENDERFULLCONTENT` flag was not passed |
| Coordinates | the client window, frame excluded | the client area, frame excluded — deliberately the same |
| The failure that costs you | a grab of an unmapped window, refused as `BadMatch` | a click into another application, reported as success |

**The one rule both back ends now share: refuse rather than guess.** Each checks that the point it is
about to act on belongs to the window it was aimed at, and says what is in the way when it does not.
The two platforms arrive at that rule from opposite directions, which is the strongest argument that
it belongs in the vocabulary rather than in either back end.

## A defect that was not one, recorded so it is not "found" again

`BucketedRollingFileSink` builds its inner `Serilog.Sinks.File` logger with
`.MinimumLevel.Information()` hard-coded, which reads like a filter that would drop a host's `Debug`
events from the file while the Trace sink and the F12 window still showed them. **It is not.** The
sink formats each event itself and re-emits the finished line at Information
(`_currentLogger?.Information("{RawLine}", …)` in `Emit`), so the inner minimum never filters
anything, and the real level is already inside the text. The outer pipeline honours
`options.MinimumLevel` normally. Checked in `Bennewitz.Ninja.AppServices`, which is the live home of
this code; ClaudeForge's copy, which DiffView still consumes as a hand-packed `1.0.1`, is identical.

The lesson is about reading, not logging: **one line of a class is not the class.** The conclusion
was drawn from the `LoggerConfiguration` alone, without reading the `Emit` twenty lines above it.

## Still owed

- Everything above, to XamlQuality, once plan 00023 phase 5 lands: the Windows entries to
  `docs/avalonia-gotchas.md` and `docs/ai-drivable-ui.md`, after which this file becomes a pointer.
- The macOS back end is specified in plan 00023 and not written; its permission grants are
  interactive and cannot be scripted.
- Whether `PostMessage(WM_LBUTTONDOWN/UP)` or `AttachThreadInput` can deliver a click to an
  unraised Avalonia window is **unmeasured** — by this repository and by TailBlazor, who declined to
  guess. The caveats to test are that posted messages bypass hit-testing, do not move the real
  cursor, and would change what hover and anything reading the pointer's position see.
