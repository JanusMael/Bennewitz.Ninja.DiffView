# 00033 — A pass whose steps can fail

> Status: **approved 2026-10-09**. Supersedes nothing.

Plan 00023 built the instrument; this makes its steps able to fail.

`scripts/drive-demo-interactive.ps1` drives the demo through twelve steps and judges each by **whether
the driver verb threw**. That is a test of the driver, not of the application. Measured over three runs
on 2026-10-08/09, three defects sat inside a reported `ok`:

| What reported `ok` | What had happened |
|---|---|
| The splitter drag | The press landed on a connector polygon, so it selected a block. The panes never moved. Found by diffing two frames: **48 pixels**, which were the caret blinking |
| `hover` on the change-marker margin | The margin's centre row is unchanged, so there is no tooltip there. The *next* step then captured a **stale popup window** |
| Ticking the colour-blind palette | Refused for three different reasons across three runs, the last of which would have **light-dismissed the menu and clicked the pane underneath** had the guard not caught it |

Exactly **one** step asserts anything today, added while fixing the first row: the splitter reads the
left pane's width back through UI Automation, 531 → 681. The rest cannot fail in any way that matters.

⚠ **This plan does not make the pass a test, and it is worth being exact about what it does catch.**
All three defects above are **harness defects — where the driver aimed, not what the application did.**
Not one was a DiffView bug. So the claim here is narrow and it is not "end-to-end coverage": it is that
**a frame is what its name says it is.** Run 1 produced `08-palette-colour-blind.png` showing the demo
with a menu open over it, and that is the class of failure this closes — the look staying trustworthy,
which is the only thing the pass was ever for. `AGENTS.md` §9's rule stands: *a captured window is a
look, not a test.*

⚠ **What automated coverage already exists, so this is not mistaken for filling that gap**: 695 headless
tests, including `AccessibilityCoverageTests` walking the automation peer tree and `PanePeerFocusTests`
asking a pane's peer about focus. They use real controls and real peers with no window and no injected
input. Whether a *separate* automated end-to-end suite should exist is a question this plan does not
answer and does not foreclose — see the decisions at the end.

## What can be asserted, and what cannot

⛔ **Every peer this library ships advertises no pattern — deliberately, per `AGENTS.md` §1.** There is
no `TogglePattern`, no `ValuePattern`, no `SelectionPattern` in `src/DiffView.Avalonia`;
`DiffPanePresenterAutomationPeer` overrides four things and no more: control type,
`HasKeyboardFocusCore`, `IsKeyboardFocusableCore`, `SetFocusCore`.

⚠ **That is a fact about OUR peers and it is not a fact about the tree a harness reads** — the first
draft of this plan conflated the two and built its central constraint on the error. The stock Avalonia
controls inside our templates advertise patterns of their own. **Surveyed against the running demo**:
exactly 14 elements advertise any pattern, and one of them matters.

| Readable through UIA | Not readable through UIA |
|---|---|
| `AutomationId`, `ControlType`, `BoundingRectangle` | A checkable menu item's state — a `MenuItem` advertises `ExpandCollapse` and **no `Toggle`** |
| `HasKeyboardFocus`, `IsKeyboardFocusable` | The current change index |
| **Presence or absence in the tree** — §1 hides an off part through `IsVisible`, so it is genuinely gone | A pane's selection, caret or find query — **no `Value` pattern exists anywhere** |
| ⭐ **The scroll offset** — `PART_VerticalScrollBar` is a `ScrollBar` advertising **`RangeValue`** | Any rendered text — **no `Text` pattern exists anywhere** |
| `Name` — but it is translated, so §1 forbids searching by it | |

⛔ **`PART_VerticalScrollBar` is AvaloniaEdit's template part, not ours.** It is not in
`fixtures/automation-ids.txt` and nothing pins it: an Avalonia upgrade may rename it, and the assertion
that depends on it fails as *part not found* rather than as a wrong answer, which is the safe direction.
Phase 4 names it in one place so a rename is one edit.

**The second column is covered by the demo's own log, which plan 00029 built for exactly this.** It
already records a checkable menu item with the state it left (`Menu: {Path} → on|off`), each command by
name and where it came from, each copy a pane asked for, and — raised from the library at the demo's
level — where each click landed, each selection a pointer made and each fold that opened.

So the rule this plan adopts:

> **Assert through UI Automation where the tree answers; through the demo's log where it does not; and
> through a frame only where neither can.**

## Decisions

| Decision | Why |
|---|---|
| ⛔ **No assertion reads rendered text at all** | The first draft asserted on the status strip, and the strip is **unreachable**: the reader finds elements by `AutomationId` and the strip's inner text blocks have none, and its report carries no `Name` either. Reaching it would mean a descendant walk reading translated text — new capability, spent on the one thing `--culture de-DE` makes unreliable. Dropping it costs **one** row's strength and removes a whole class of locale fragility |
| **A step that cannot be asserted says so, and is listed** | A pass reporting twelve of twelve while some of them asserted nothing is the same lie in a smaller font. Every step has an assertion as the plan stands, so this is a rule for what a *run* reports — a reading that could not be taken on the day is named, never counted as a pass |
| **The reader gains `HasKeyboardFocus`, and nothing else** | `drive-demo-uia.ps1` returns geometry today. ⛔ **No peer gains a pattern to make this plan easier** — §1's no-pattern rule is a shipped contract and this is a harness. `IsEnabled` was in the first draft and is out: no row uses it, and a reader field nobody reads is the unexercised code this repository keeps deleting |
| **A `probe` verb, not an assertion verb** | The driver reports; the pass decides. A driver that knows about expectations is a test framework, and `drive-demo` is used by hand as well |
| **The pass gains no new steps** | This is about the twelve it has. New coverage is a later plan |

### Dismissed

- **Give the peers patterns so UIA can read values.** It would make the pass trivial and it is wrong: the
  no-pattern rule is a deliberate shipped decision with a test behind it (`AccessibilityCoverageTests`),
  and a harness is not a reason to change what consumers see.
- **Assert on the status strip's text.** Unreachable, and translated even if it were. See the first decision.
- **Put *this* pass in CI.** It injects input into whatever holds the foreground and is specified as
  attended; §9's refuse-rather-than-raise rule only makes sense with a person there. ⚠ **This is not a
  claim that no automated end-to-end suite could run in CI** — a Windows runner has an interactive
  desktop session, and that question is open and listed below rather than settled here.
- **Diff frames as an assertion, anywhere.** It is what found the splitter defect by hand, but it is slow,
  platform-dependent and answers *something changed* rather than *the right thing changed*. ⚠ **Not
  adopted, and no longer needed**: the map drag was the one row that would have wanted it, and
  `RangeValue` on the scroll bar answers it properly instead.

## Scope

In:

- `scripts/drive-demo-uia.ps1` — report `HasKeyboardFocus` beside the geometry, **and a `RangeValue`
  reading where the element has one**; exit distinguishably when a part is **absent** (presence is an
  assertion here, not an error). ⚠ The `RangeValue` half is a pattern *query*, a capability the first
  draft did not budget for: it belongs here, in the reader, because the C# driver only shells out to it
- `scripts/drive-demo.cs` — a `probe <path>` verb printing what the reader found; `--parse` support for it
- `scripts/drive-demo-interactive.ps1` — an assertion per step, and a summary that names any it could not read
- `src/DiffView.Demo/ActionLog.cs` — a navigation command logs the `CurrentChangeIndex` it left, the shape
  a menu choice already uses. ⚠ The **only** production file this plan touches, and it is the demo's
- `tests/DiffView.Avalonia.Tests/DriveDemoTests.cs` — the new verb's parsing and its report's reading,
  against a fixture, as the existing element report is read
- `fixtures/windows/` — a fixture for the extended report
- `AGENTS.md` §9, and `DECISIONS.md`

Out:

- **Any change to a peer, a control, or what the library exposes.** If a step cannot be asserted, it is
  listed as unasserted; it does not become a reason to widen the public surface
- The macOS back end, still (plan 00023 phase 5)
- **Any change to a workflow.** Nothing here runs in CI and nothing here is added to it. ⚠ That is a
  statement about this plan's deliverables, **not** a ruling that end-to-end automation can never run in
  CI — decision 1 below is open
- New steps in the pass

## The twelve steps, and what each would assert

| Step | Assertion | Source |
|---|---|---|
| focus left / focus right | the pane's peer reports `HasKeyboardFocus`, and the other does not | UIA |
| pane context menu | a popup exists, and its entries are found **by id within it** | UIA |
| light-dismiss | the popup is **gone from the tree** | UIA |
| line-number tooltip | a popup exists with non-empty bounds | UIA |
| View menu | a popup exists, **and `ColourBlindPalette` is found by id within it** — the entries carry ids, so stopping at the window would be weaker for free | UIA |
| change-marker tooltip | a popup exists — and ⚠ **the pointer is on a changed row**, which is why the ladder exists | UIA |
| find bar | `SideBySide/FindBar` is **present**, having been absent before; absent again after Escape — and `Command OpenFind` / `CloseFind` in the log | UIA + log |
| splitter | the left pane's width changed by the drag's distance | UIA *(done)* |
| palette | `Menu: … → on` in the log; by flag, `palette=colour-blind` in the summary line | log |
| map drag | the left pane's `PART_VerticalScrollBar` **`RangeValue` moved** — a deliberate viewport drag of 330 px, so the movement is large and the assertion is not near its own noise floor | UIA |
| F7 twice | `Command NextChange …` appears **twice** since the step began, **and the index it left is 2** — the demo logs the resulting `CurrentChangeIndex`, so a navigation that fired and went nowhere is caught | log |

**Twelve of twelve fully assertable**, and the summary still names any that could not be read on the day.

⚠ The rows are evidence rather than design in two places. The log lines were read back from the
2026-10-08 run — `Command NextChange from the keyboard` twice, two seconds apart, exactly the two F7
presses, and `OpenFind` / `CloseFind` around the find bar. The scroll row comes from surveying the
running demo's patterns, which is also what corrected this plan's premise.

⚠ **F7's scroll is deliberately NOT asserted, though it would move.** `ScrollToRows` centres the
block, so the view does scroll — but the first two changes may both already be near the top, and an
assertion that passes on this fixture and fails on a pair whose first changes are adjacent is the
flakiness the risks section warns about. The command firing is the reliable half; the scroll is left as
corroboration a reader can see in the frame.

## Phases

| | Phase | Size | What lands, and how it is shown to work |
|---|---|---|---|
| 1 | The reader reports state | S | `HasKeyboardFocus`, and a `RangeValue` reading where the element has one; absence exits distinguishably from failure. Fixture + `DriveDemoTests` reading it, **proven able to fail** by a mutation of the field order and of the absent-exit |
| 2 | `probe` | S | The verb, its canonical form, `--parse` support, and tests beside the existing element-report ones |
| 3 | The demo logs where it landed | S | A navigation command records the `CurrentChangeIndex` it left. In `DemoActionLogTests`, which already reads a real Serilog pipeline: `NextChange` twice logs index 2, and a navigation that changes nothing logs the index unchanged. **Proven able to fail** |
| 4 | The pass asserts | M | One assertion per row above; the summary names anything it could not read. **Shown to work by re-breaking each of the three known defects** — point the splitter at a polygon, point the hover at an unchanged row, and run the palette against a scratch revert of `Drawn` — and confirming the step now **fails** where it reported `ok`. ⚠ That revert is scratch and is never committed; it is reverted back before this phase lands |
| 5 | The record | S | `AGENTS.md` §9 gains the assert-through-UIA-then-log rule and the verb; `DECISIONS.md` carries what the pattern survey found — that the no-pattern rule is ours and not the tree's — and why a harness still did not get to relax it; `CHANGELOG.md` |

⭐ **Phase 4's verification is the plan's whole claim.** A pass that asserts and has never been shown to
fail is the same instrument with more code in it. The three defects are reproducible and are the natural
test corpus — this plan exists because they were found by hand.

## Risks

- ⛔ **An assertion that cannot fail.** The failure mode this plan is about, re-entering through the
  front door. Phase 4's re-break is the mitigation and is not optional.
- **A flaky assertion is worse than none**, because an attended pass is abandoned the second time it
  cries wolf. Timing is the likely source: a popup is asserted *present* after a dismiss has been sent,
  so each assertion reads after the same settle the capture already waits for, and a presence check that
  can race is given the driver's existing wait rather than a sleep.
- **The log is a shared file.** Asserting "appeared since this step began" needs a mark — the file's
  length before the step, not a grep of the whole file, or a second run's lines are read as this one's.
- ⚠ **A popup existing is a weak assertion**, and **two** rows rest on it — the two tooltips. A tooltip
  has no ids inside it, so there is nothing stronger available for those; for a *menu* there is, which is
  why both menu rows reach for an entry by id rather than stopping at the window.
- ⛔ **The map drag's start must land inside the viewport box, and that depends on the scroll position** —
  a press inside the box drags, a press outside it jumps. This is the splitter defect's exact shape: a
  coordinate whose meaning depends on what is scrolled into view. It is safe only because the splitter
  step before it sends Ctrl+Home, which puts the box at the top of the map. **That is a dependency
  between two steps and phase 4 must not reorder them**; the `RangeValue` assertion turns a silent
  wrong-gesture into a failure either way, which is the point.

## Decisions this plan does not make

1. **Should a separate automated end-to-end suite exist, in CI?** This plan hardens an attended
   instrument and deliberately does not answer that. The repository has 695 headless tests and no
   window-driving automation; whether that is a gap worth closing is a scope question, not a plan-00033
   question. ⚠ Nothing here forecloses it — the `probe` verb would be the same reader either way.
2. ~~The map drag~~ — **settled by measurement, not judgement.** The first draft called it unassertable;
   a pattern survey of the running demo found `PART_VerticalScrollBar` advertising `RangeValue`, so the
   scroll offset is readable and the row is now a full assertion. Nothing for anyone to decide.
3. ~~F7's current-change index~~ — **decided 2026-10-09: the demo logs it.** `CurrentChangeIndex` is
   public API, so the demo reads it after a navigation command and records the state that command left,
   exactly as it already does for a menu choice. ⛔ **No peer advertises anything and §1 is untouched** —
   the reading moved to the host, which is where plan 00029 put this kind of knowledge already.

⚠ **One open question from the first draft is now moot**: whether to pin the pass to English so a
stronger assertion could read the status strip. No assertion reads rendered text any more, so the pass
stays locale-agnostic and there is nothing to pin.

## Conventions

The pass stays Windows-only for the reason it already is: it addresses parts by `AutomationId` and the
X11 back end refuses that. A full `scripts/mutate-gates.sh` run is not owed — no gate file is touched —
but the tests in phases 1, 2 and 3 are each proven able to fail, per `AGENTS.md` §5.
