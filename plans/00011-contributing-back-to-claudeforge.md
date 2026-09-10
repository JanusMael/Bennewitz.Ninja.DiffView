# 00011 — Contributing back to ClaudeForge

Plan 00001 §*Contributing back to ClaudeForge* named what qualifies to go upstream. Three of those
have gone and are merged — the diagnostics F12 and pack fixes ([#37](https://github.com/JanusMael/ClaudeForge/pull/37),
`99c2963`), the theme audit and its compat dictionaries ([#38](https://github.com/JanusMael/ClaudeForge/pull/38),
`93065ba`), and the stale AvaloniaEdit-under-Semi comment ([#44](https://github.com/JanusMael/ClaudeForge/pull/44),
`168bf26`). The control itself is deferred by plan 00002's own review, and `DECISIONS.md` records
why.

Three items on that list are in neither state. They have no pull request and no decision saying
they stay here: **`StatusController` on `TimeProvider`**, **the accessibility coverage guard made
generic**, and **the Dark status foregrounds** DiffView brightened because ClaudeForge's own values
missed the floor. Each was found by using ClaudeForge's code, and each is invisible from inside
ClaudeForge, which is the only reason none of them has moved.

Nothing here is large. What makes it a plan rather than three commits is that the work lands in a
repository this one does not own, on a checkout another session may be holding, against a base that
is **not** where that checkout's `HEAD` is.

## Goal

Each of the three ends in one of two states: merged in ClaudeForge with a row in `PROGRESS.md`'s
*Upstreamed to ClaudeForge* table, or recorded in `DECISIONS.md` as deliberately kept local, with
the reason. None of them is left owed on a list with nothing written down.

## Non-goals

| Excluded | Note |
|---|---|
| The control itself | Plan 00002 was rejected on its own review and the deferral stands until `feat/agentforge-opencodeforge` lands. `src/ClaudeForge.Avalonia` already exists on `origin/main`, which suggests that split may be under way; confirming it and re-scoping the integration is a new plan, not this one |
| Backfilling ClaudeForge's accessibility names beyond what the strengthened guard requires | Phase 1 measures the debt. A number, not a guess, decides whether the guard lands with a backfill or with a baseline |
| Moving the ClaudeForge checkout's `HEAD` | Not ours. Every branch here is a worktree, every base is `origin/main`, and the checkout is left exactly where it was found |
| Changing `theme-audit` | The tool is already a local dotnet tool there and already generates a contrast matrix. This plan uses it; it does not touch it |
| Any change to DiffView's public API | The one DiffView source change in this plan is a private guard inside `StatusController` |
| ClaudeForge's own `.resx` / translation question | That is the open decision in `DECISIONS.md` about whether *DiffView* ships translations. Unrelated, and still open |

## The three contributions

### 1. `StatusController` on `TimeProvider` — a port, not a replacement

DiffView's controller is a rewrite, not a patched copy. It is a plain class with a `Changed` event
where ClaudeForge's is an `ObservableObject` with `[ObservableProperty]` fields and six per-kind
booleans (`IsActive`, `IsSuccess`, `IsWarning`, `IsFailure`, `IsState`) that its View binds to
directly. **Sending the file back would break every one of those bindings.** What goes back is the
three improvements, into the shape ClaudeForge already has.

| Improvement | What it replaces there | Why it is worth a PR |
|---|---|---|
| A `TimeProvider` taken in the constructor | `internal static Func<TimeSpan, CancellationToken, Task>? DelayOverride`, two `internal static` delay properties, and a hand-maintained `ResetForTesting()` | Three mutable statics shared by every instance and every test in the run. The method's own doc comment warns that "a missed seam silently leaks state across tests — exactly the bug this method exists to prevent". A per-instance clock deletes the class of bug instead of documenting it, and `ResetForTesting` goes with it |
| `Set` private, behind `SetActive` / `SetSuccess` / `SetWarning` / `SetFailure` / `SetState` | `public void Set(string?, StatusKind)` | The severity stops being a parameter a call site can get wrong. `AGENTS.md` §6 locks it here, naming the failure mode it prevents: "a failure renders as quiet text" |
| `Apply` compares before it assigns, so nothing is raised when nothing changed | unconditional property assignment | Idempotence at the notification boundary; cheap, and it makes a "did this change" test mean something |

And one that goes the other way, found while scoping this:

**ClaudeForge's version has a guard DiffView's rewrite dropped.** Its `AutoClearAsync` compares
`ReferenceEquals(_autoClearCts, cts)` before clearing, so a timer that has already fired cannot
clear a message that replaced it. DiffView's `StatusController.OnClearDue` passes `state: null` to
`CreateTimer` and tests only `_pendingClear is null`, which is the *current* timer, not the one that
fired. A callback that fires and queues its dispatch immediately before the UI thread calls `Set`
again therefore runs against the **new** message: it disposes the new timer and clears text that
should have stuck. A `FakeTimeProvider` fires deterministically on the calling thread, so no test in
the suite can see it.

That is fixed here first — a token object created before `CreateTimer`, passed as `state`, compared
in the callback — and the guard is then carried into the port so both versions hold the same
invariant for the same reason.

### 2. The accessibility guard's reach

**Part of this has already landed upstream.** [#39](https://github.com/JanusMael/ClaudeForge/pull/39)
(`61965c9`) named every interactive control in the diagnostics windows and added a 250-line runtime
`AccessibilityCoverageTests` walker, with menu items filtered explicitly (`afa3738`). The gap in the
standing note — that the guard scanned only XAML and so could not see code-built UI — is closed, and
nothing is owed on it.

What is still narrow is the **AXAML** guard, `AxamlAccessibilityCoverageTests`, unchanged by that
work.

| ClaudeForge's guard | DiffView's | What it misses |
|---|---|---|
| `Directory.GetFiles(viewsDir, "*.axaml")` — flat, one folder, `src/ClaudeForge/Views` | `Directory.EnumerateFiles(dir, "*.axaml", SearchOption.AllDirectories)` over whole source roots | **16 AXAML files on `origin/main` are outside `Views/` and unscanned**: 5 under `src/ClaudeForge/Controls`, 3 under `src/ClaudeForge.Avalonia/Permissions`, 2 under `src/LayeredEditors.Avalonia/Themes`, 1 under `src/LayeredEditors.Avalonia/Controls`, `App.axaml`, and 4 resource dictionaries. Two of those Permissions files carry real interactive surface |
| The interactive set omits `MenuItem` and `RepeatButton` | Has both | `MainWindow.axaml`'s menu is the app's primary interactive surface and no assertion reaches it |
| A 16-entry `Baseline`, every entry at `0` | An empty dictionary — strict zero everywhere, no entry to keep in sync | The backfill the baseline was written for in 2026-05-15 is finished. Its companion `Baseline_ConvergesToZero_FullBackfillTracker` says in its own comment to delete it and the dictionary when the total reaches zero |

The order matters: **widen, run, count, then decide the shape of the PR.** A count at or near zero
means a clean strengthening. A large count means the guard lands with a baseline for the
newly-covered files and the backfill is named as follow-up — the same ratchet ClaudeForge already
chose once. Which of those it is, is a number, and phase 1 produces it before anything is written.

### 3. The Dark status foregrounds

| Kind | Light (both, identical) | ClaudeForge Dark | DiffView Dark |
|---|---|---|---|
| Success | `#156321` | `#5BD17B` | `#6ADB88` |
| Warning | `#874400` | `#F0A03A` | `#F7B85A` |
| Failure | `#A8071A` | `#F99090` | `#FFAEAE` |
| Active | `#0050B3` | `#6BB1F2` | `#8FC8F7` |

DiffView holds its status pills to 4.5:1 on their own fill and 7:1 on the host page. ClaudeForge's
Dark foregrounds cleared their fills but sat between 5.7:1 and 6.9:1 on the two lightest dark pages
the audit knows — Semi Dusk's `#2D3236` and Simple's `#282828` — so those four moved one step here
and the Light values were taken verbatim. The four page pairs live in
`src/DiffView.Avalonia/Themes/contrast-pairs.json` at floor 7.0 and the audit holds them.

The mechanism to send it back already exists and needs nothing built: `theme-audit` is a local
dotnet tool in ClaudeForge since #38, `docs/theme-audit-report.md` there is generated from this
repository against that checkout, and the report already carries a contrast matrix. So ClaudeForge
gets a `contrast-pairs.json` of its own, in the same shape as this one, naming its four
`AppStatus*ForegroundBrush` values against its own page and fill colours; the audit is re-run; and
the numbers decide whether anything else changes.

**The floor is the question to settle before the values, and it is not automatically 7:1.** DiffView
chose 7:1 because its pills land on host pages it does not control, and it cannot know which. A
ClaudeForge pill lands on ClaudeForge's own page. If 4.5:1 on the fill is all the app owes itself,
then the pairs go in at that floor, the existing values hold, and the contribution is the assertion
rather than the colours — which is still worth having, because nothing holds them today.

## Mechanics

The traps here have each been paid for once already; `AGENTS.md` §8 carries them.

- **Never `git checkout` in `/home/janus/c/cl/ClaudeForge`.** A worktree, always:
  `git -C /home/janus/c/cl/ClaudeForge worktree add <scratch-path> -b <branch> origin/main`.
- **Branch from `origin/main`, not `main`.** That checkout's local `main` is `93065ba` and
  `origin/main` is `168bf26` — **14 commits apart**, with #39 through #44 all in the gap. A branch
  cut from local `main` silently reverts them.
- **Announce first if a ClaudeForge session is running** (`ListAgents`). None was when this was
  written; three worktrees already exist under that repository's `.claude/worktrees/`, so assume one
  can return between phases and check again before each.
- **One pull request per contribution.** They share no files, and one being declined must not hold
  up the other two.
- Where a package is affected, it is repacked into the local feed and the pin bumped here in the
  same change. None of the three touches `LayeredEditors.Avalonia.Diagnostics`, so no repack is
  expected; if that turns out to be wrong, the repack is part of that item's phase.
- `reference/sources.json` declares a ClaudeForge entry whose directory has never been materialised
  — the audit reads the sibling checkout through the entry's `local` path instead. The pin there is
  `93065ba`, which predates #44. It moves once, at the end, to whatever ClaudeForge's `main` is when
  this plan closes.

## Phases

| Phase | Size | Content |
|---|---|---|
| **1 — Measure** | S | Three numbers, no commits to ClaudeForge. The unnamed-control count from the widened AXAML guard run in a worktree over `origin/main`; the four Dark status ratios from `theme-audit` against `origin/main`, on ClaudeForge's own pages; and whether the timer race is reachable, proven by a test that fails here. Each number is a go/no-go and a shape for its item |
| **2 — The status controller** | M | The timer-identity guard here, with the failing test first; then the port to ClaudeForge — `TimeProvider` in, three statics and `ResetForTesting` out, `Set` private behind the five typed helpers, the identity check kept — as its own pull request, with its existing lifecycle tests moved onto the injected clock |
| **3 — The accessibility guard** | S–M | Recursive scan, `MenuItem` and `RepeatButton` in the set, and whatever phase 1's count says about the baseline. `Baseline_ConvergesToZero_FullBackfillTracker` retires with the dictionary if the count allows |
| **4 — The status foregrounds** | S | ClaudeForge's `contrast-pairs.json`, the floor settled against phase 1's ratios, the four values moved only if they miss it, `docs/theme-audit-report.md` regenerated in the same change |
| **5 — The record** | S | `PROGRESS.md`'s *Upstreamed* table gains a row per merge; `DECISIONS.md` gains an entry for anything declined or kept local, and for the timer race; `CHANGELOG.md`; the `reference/sources.json` pin moves once |

## Testing

Every new test is proven able to fail before it is committed.

| Test | Asserts |
|---|---|
| A fired clear cannot clear the message that replaced it | Here, in `StatusControllerTests`: a success is set, the fake clock advances so the timer fires, and a failure is set from inside the dispatch before the callback body runs. The failure survives with its text and kind intact. **This is the one that fails on today's code**, and phase 1 does not proceed on this item until it does |
| The five lifecycle rules survive the port | In ClaudeForge, over the injected clock rather than `DelayOverride`: success clears after six seconds, warning after ten, failure sticks, active and state stick, a new message cancels the pending clear. The same five cases it has now, with no static to reset between them |
| A caller cannot emit an untyped status | `Set` is not public; the five typed helpers are the only entry. A compile-level assertion, so its evidence is the call sites that had to change |
| The widened scan reaches the files the flat one could not | A count over `src/ClaudeForge.Avalonia` and `App.axaml`, both of which the flat `Views/` scan returns zero files for |
| An unnamed `MenuItem` fails the guard | A fixture element with no `AutomationProperties.Name`, counted. The set's addition is worthless without it |
| A baseline entry for a file that no longer exists fails | ClaudeForge's guard already does this; it must keep doing it after the scan widens, because the relative paths in the dictionary change shape with a recursive scan |
| The four Dark status pairs are held at their floor | Through ClaudeForge's own `contrast-pairs.json` and the audit's drift check, which already runs there |
| DiffView's suite is unchanged | 501 at the branch head, plus the new race test. The only DiffView source change in this plan is inside `StatusController` |

## Risks

| Risk | Assessment |
|---|---|
| **A branch cut from the checkout's local `main` reverts four merged pull requests** | The most expensive mistake available here, and it looks like nothing. Every worktree names `origin/main` explicitly, and phase 1 re-checks the gap before each branch rather than trusting this document's numbers |
| ClaudeForge declines a change | Then the `DECISIONS.md` entry is the deliverable, which is the outcome plan 00001 already specified. Two of the three are opinionated — a private `Set` changes call sites, and a widened guard can fail a green build — so this is a likely outcome for at least one, not a failure of the plan |
| The widened scan turns up a large backfill | Measured in phase 1, before a line is written. A guard that cannot land without naming 140 controls is a guard that lands with a baseline; ClaudeForge made exactly that call in 2026-05 and it worked |
| The port changes ClaudeForge's binding surface | It must not. `ObservableObject`, both `[ObservableProperty]` fields, `HasText`, `IsDismissible` and all six per-kind booleans stay exactly as they are. What changes is the clock, the visibility of `Set`, and three statics that disappear |
| The timer race is unreachable in practice and the fix is noise | Then phase 1's test cannot be made to fail, the item drops to a `DECISIONS.md` note, and the port carries ClaudeForge's existing guard unchanged. Either way the two versions stop disagreeing about it |
| The status floor is argued rather than measured | Phase 1 produces the ratios against ClaudeForge's own pages first. The colours are the last thing decided, not the first |
| The ClaudeForge session returns mid-flight | `ListAgents` before each phase that writes, and the worktree rule means the worst case is a stale branch, never a moved `HEAD` |
| Scope creep into the control's integration | `src/ClaudeForge.Avalonia` on `origin/main` is a temptation, not a mandate. Plan 00002's deferral stands and re-scoping it is a new number |

## Conventions

Conventional Commits, dense bodies, no AI attribution trailer — in both repositories. An approved
plan is committed before implementation and never edited; drift goes to `DECISIONS.md`. New tests
are proven able to fail before they are committed. A document naming a test names one that exists.
Anything that lands in ClaudeForge lands there first, as a branch and pull request in the same
working session, and is recorded here when it merges.
