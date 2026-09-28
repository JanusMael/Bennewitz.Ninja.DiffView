# 00029 — The demo says what you did

> Status: **approved 2026-09-28**. Supersedes nothing.

On 2026-09-28 Brian found a bug by hand in the demo — padding lost on lines that begin with
whitespace, once *Show whitespace* is on — and reported it with a screenshot. Reproducing it took a
launch, a menu drive and three captures, because the demo's log records what the control did and
nothing of what its user did. His run's log shows exactly that gap: three builds at 07:18 whose block
counts change from 5 to 4 to 3, and no line saying which toggles were on, what was clicked, or what
was edited to cause them. It also cannot say which machine it ran on.

Brian's ask, the same day: log the selections he makes, captured on mouse up rather than repeatedly
during a drag; log anything else that makes a bug he finds reproducible; log the machine's host name
and no IP address. **Primarily in the demo**; the library carries it only at specific seams where that
is less friction, as a log adapter that is off by default.

## Which side logs what

Measured against the library's public surface, the demo can reach nearly everything itself:
`DiffPanePresenter.Side` and `SelectedLines`; `ContextAt(lineNumber, region)`, whose public
`DiffPaneContext` carries the source side, source line, row and block — the unified view's mapping
included; the views' `KeyMap`, `GestureFor` and `CommandFor`, which name the command behind a chord or
a menu item; the presenters' public `CopyOutRequested` and `CopySelectionRequested`; the documents'
own change events; and the overview map and connector gutter, which are public controls.

**Two facts are the library's alone.** Which region of a pane a click landed in — text, number margin,
change markers, padding, a fold's placeholder — is decided by the event source's identity against the
pane's own margins, and both margins are internal. And a fold opening is an internal event. So the
library logs exactly those two, and the demo logs everything else.

## What the log gains

From the library, under a new `DiffViewLogCategories.Interaction` at `Debug`, written by `DiffViewLog`:

| Seam | When | What is written | Example |
|---|---|---|---|
| A pane gesture | on pointer release in a pane — never during a drag | the side, the region, and the caret or the selection as source `line:column` ranges with a character count | `Left pane: selected 12:9–14:10 (58 chars)` · `Left pane: click in the number margin at line 12` |
| A fold opening | when a placeholder gives its run back | the rows it restored | `Fold opened: rows 40–57` |

From the demo, at `Information`:

| Event | When | What is written | Example |
|---|---|---|---|
| The machine | once, beside the `[DebugFlags]` line | host name, OS description, .NET runtime | `Host: <name> · Linux 6.18 · .NET 10.0` |
| The window | when it opens, and when a resize has been quiet for half a second | client size and display scaling | `Window: 1100×720 at scaling 1` |
| A menu choice | when an item of the demo's own menu bar is clicked | the item's path, and its checked state after the click | `Menu: View ▸ Show whitespace → on` |
| A command | when a key chord or a pane's context menu runs one | the `DiffCommand` name, resolved through `KeyMap` and `CommandFor` — and, when it acts on a selection, the selection it acted on | `Command CopyToRight on right 12–14` · `Command NextChange` |
| A copy | when a pane raises `CopyOutRequested` or `CopySelectionRequested` | the pane and what it asked to send | `Copy requested: left block 3 → right` |
| An edit | once edits have paused for half a second | the pane, the first line touched, the lines added and removed | `Right pane: edit at line 14, +1 line` |

A plain keystroke in an editable pane is typing, and is never logged in any form. A selection made
with the keyboard is logged when a command acts on it, which is the moment it matters.

## Off by default, and the one switch

The library already takes an `ILoggerFactory` from its host and logs under the categories in
`DiffViewLogCategories`. The two new lines go under `DiffView.Interaction` at `Debug`: a host that logs
at the usual `Information` never sees one, and a host that wants them enables that category — one
filter, the way every `Microsoft.Extensions.Logging` host already switches a category on. There is no
property beside it, so there is no second switch to disagree with the first. The demo enables the
category, so its log says what was done by default.

## Never logged

**Document text, in any form** — the rule `DiffViewLog` already holds the library to, held by the demo
too: no selected text, no typed character, no copied line, no find query (the library already logs
only its length). **No IP address**, and no network identity beyond the host name.

## Decisions

| Decision | Why |
|---|---|
| **The demo logs everything it can reach through public types** | Brian: primarily demo-only. Measured, that is nearly everything |
| **The library logs only the click region and fold openings** | The two facts nothing public exposes; the seams Brian allowed where they are less friction |
| **A category at `Debug` is the whole switch** (Brian, 2026-09-28) | The host's log level is already the mechanism, per category; a property beside it would be a second switch to keep in agreement |
| **Selections on pointer release only; a keyboard selection when a command acts on it** | Brian: not repeatedly during a drag. One line per gesture is the unit a report is made of |
| **Edits coalesced by a pause** | Typing would otherwise write a line per keystroke, and each line would be a step toward the text itself |
| **Commands by name, not keys** | `Command NextChange` survives a rebind and a German keyboard; a chord is also how a keystroke leaks |

Dismissed:

| Alternative | Why not |
|---|---|
| The library logs gestures, commands, copies, edits, options, folds, map and connector | Draft two of this plan, built on a survey that missed `ContextAt` and `CommandFor`: most of it is reachable from the demo |
| The demo alone, with no library change | Leaves the click region and fold openings unlogged, the two facts a margin or folding bug turns on |
| The library exposes the two facts as public API instead | Two permanent public members for a diagnostic need that logging meets |
| A `LogInteractions` property on each view | A second switch beside the log level |
| Logging pointer movement, or a drag as it happens | Brian said not to, and it would drown the lines that matter |

## Scope

**In.** `DiffViewLogCategories.Interaction`, two `DiffViewLog` methods, and the two seams in the
presenter; the demo's machine, window, menu, command, copy and edit lines, and the category switched
on in `DemoLogging`; the tests below; `AGENTS.md` §1 and §6 for the category's rules, §9 pointing a
by-hand diagnosis at the log first; the hosting guide's **Logging** paragraph; `PROGRESS.md`,
`DECISIONS.md`, `CHANGELOG.md`.

**Out.** Configuring what is logged beyond the category's level. Shipping or collecting logs. Any
new public member beyond the category's name.

## Phases

| Phase | Size | What | Verified by |
|---|---|---|---|
| 1 — The library's two seams | S | The category, the two `DiffViewLog` methods, one call at each seam | Each writes its line when the category is enabled and **nothing when it is not** — off by default, proven; the library's sentinel test extended to both: a distinctive string selected and folded over never appears in any line. Every test red first |
| 2 — The demo | S | The machine, window, menu, command, copy and edit lines; the category switched on | Each line written when the real demo window is driven headlessly, captured by a Serilog sink of the test's own — no new package — and a demo sentinel: the distinctive string selected, typed, pasted, copied and searched for never appears in any line |
| 3 — The record | S | `AGENTS.md` §1, §6 and §9, the hosting guide, `PROGRESS.md`, `DECISIONS.md`, `CHANGELOG.md` | A by-hand pass on this box — toggle *Show whitespace*, select across padding, copy a block, open a fold, press F7 — whose log reads back as that pass; the suite in `en-US` and `de-DE` under `catch-crash --expect auto`; a full `scripts/mutate-gates.sh` run, since the library assembly is a subject the gates read |

## Conventions

Branch `feat/the-demo-says-what-you-did` from `main` once plan 00021 has merged — its rewrite of
`SideBySideDiffView.cs` and the demo's window is what this changes (Brian, 2026-09-28). Conventional
Commits, dense bodies, no AI attribution trailer; committed locally, and pushed only when Brian says.
An approved plan is committed before implementation and never edited; drift goes to `DECISIONS.md`.
Plan 00023's phase 2 — the driver, drafted in scratch — resumes after this lands.
