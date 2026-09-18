# 00023 — The harness the prose describes

> Status: **approved 2026-09-17**. Supersedes nothing.

`AGENTS.md` §9 is about 75 lines of hard-won fact about driving the running demo: that `xdotool` and
`wmctrl` are installed and the app really can be driven (`:422`); that `xdotool key F7` and
`ctrl+Down` into a focused pane work; that menu accelerators (`alt+v`) do **not** work through
`xdotool` while clicking does (`:441`); that `xdotool search --class DiffView | head -1` can return
an **unmapped** window (`:483`); that `xdotool mousemove` raises no tooltip, because synthetic motion
does not produce the dwell Avalonia waits on (`:496`).

**Every tool that applies those facts lives in `~/c/cl/scratch/DiffView/` and has never been
committed.** The knowledge is in the repository and the instruments are not, so each session that
needs a by-hand pass rebuilds the driver from prose — and rediscovers the traps the prose warns
about only after tripping them.

This plan commits the instruments, and shapes them so the **Windows and macOS runs owed since plan
00001 phase 10** have something to run.

## What exists today, and what each is worth

| In scratch | Lines | What it encodes | Verdict |
|---|---|---|---|
| `catch-crash.sh` | 30 | Re-runs the suite until the intermittent SIGABRT, keeping the log of the run that crashed. **The test host dies mid-run and the summary reads `Failed!` with `failed: 0`** and a total short of the full count — grepping hides it, which is how it went unnoticed once | **Commit.** No UI, no platform dependency, and it guards a false green on every machine |
| `capture-menu.sh` | 46 | Right-click a pane and capture the context popup. Four traps: an Avalonia popup is its own **unnamed override-redirect X window**, so it is found by **diffing** the root's children (counting picks up a 10×10 stub); the popup opens on one layout pass and lays out on the next, so a frame between them is empty; an unmapped window makes `x11grab` fail with `BadMatch` | **Commit as a verb** |
| `capture-topmenu.sh` | 76 | The same for a menu-bar item, by window-relative offset, reporting the popup height — the number plan 00022's menu fix was judged on. Also picks the demo's real window: viewable, and the widest candidate, because an unmapped stub and the F12 log window both answer to the class | **Commit as a verb** |
| `capture-tooltip.sh` | 37 | Hover and capture a tooltip — **and it is the experiment that proved tooltips cannot be driven this way** (`DECISIONS.md:2200`, `AGENTS.md:494`) | **Commit as a `hover` verb, known-failing on X11** — see below |
| `launch-demo.sh` | 24 | Detached launch, because **the agent harness reaps a background child at the turn boundary (exit 144)** unless it is launched from a script file this way | **Fold into `scripts/run-demo`** as a `--detach` flag; it otherwise duplicates a script already committed |
| `measure-menu-width.py` | 64 | East-Asian display width of each locale's menu entries, because a character count is the wrong metric for `ja-JP`, `ko-KR` and `zh-CN` | **Commit, but beside `gen-locale-review`** — it is a locale tool, not a window driver |
| `mutate-*.py` | 11 files | Per-phase mutation harnesses, proving a new test can fail before it is committed | **Stays scratch.** Each is written against one phase's code and its correctness decays the moment that code lands |
| `seams.py`, `entangle.py`, `locate-the-34.py`, `splice.py`, `classify-de-DE.py`, `agents-s6-rows.py` | — | One-shot investigations and converters | **Stays scratch** |
| `SurfaceDump/`, `SiblingProbe/`, `DocSeamProbe/`, `spike/`, `drift-check.sh` | — | Plan 00021's measurements, cited by that plan | **Stays scratch** until 00021 lands, which commits `SurfaceDump` as a test of its own |

## What is wrong with them as they stand

None of the five shell scripts can be committed unchanged.

| Defect | Where | Why it matters |
|---|---|---|
| **`xwininfo` output is parsed in English** — `awk '/Map State/'`, `/Absolute upper-left X/`, `/^ Width:/` | all three capture scripts | This repository runs its own suite under `DIFFVIEW_TEST_UI_CULTURE=de-DE` and ships eight locales. A harness that breaks under a non-English desktop is a harness that fails exactly when someone is checking a translation — which is what plans 00014 and 00016 used it for |
| **`XAUTHORITY` hardcoded** to `/run/user/1001/.mutter-Xwaylandauth.*` | all three | A uid and a compositor, both assumed. Breaks for any other user or session |
| **Repo path hardcoded** to `/home/janus/c/DiffView` | `launch-demo.sh`, `catch-crash.sh`, `measure-menu-width.py` | `scripts/run-demo.sh` already shows the fix: resolve from `$0` |
| **`:0.0` display hardcoded** | all three | Assumes one display |
| **The root-child diff is copied three times** | all three | One helper, three copies, already drifting in its settle times |

## The seam

**This repository already has a cross-platform script convention, and it is the right one.**
`scripts/gen-locale-review.cs` is a .NET 10 **file-based app** carrying a `#:project` directive, and
`.sh` / `.ps1` beside it are thin wrappers that `cd` to the root and `exec dotnet run` — *"the work
is done by the portable .NET 10 file-based app beside this script."* Four tools already follow it.

So the driver is **one C# file-based app with a platform back end**, not a pile of shell.

**The verb vocabulary**, which is all any of the five scripts actually needs:

| Verb | What it does |
|---|---|
| `launch` | Start the demo detached; report the pid and the log path |
| `window` | The demo's main window: viewable, and the widest candidate |
| `key <chord>` | Send a key chord to the focused window |
| `click <button> <x> <y>` | Click, in window-relative coordinates |
| `mark` / `popup` | Snapshot the top-level windows, then name the one that appeared since |
| `geometry <window>` | Position and size |
| `capture <window> <out.png>` | The window's pixels |

**The back ends, and what each costs:**

| Platform | Mechanism | Notes |
|---|---|---|
| **Linux / X11** | `xdotool`, `wmctrl`, `xwininfo -int`, `ffmpeg -f x11grab` | Already proven. `-int` and field parsing by position rather than by English label fixes the locale defect. Under Wayland this needs XWayland, which is what is in use here |
| **Windows** | `System.Windows.Automation` for finding elements, `SendInput` for input, `PrintWindow` for capture | **No external tools at all** — it is all reachable from the C# app directly |
| **macOS** | `osascript` to activate, `CGWindowListCopyWindowInfo` to enumerate, `screencapture -l<id>` to grab | Needs **Accessibility and Screen Recording permission grants**, which are per-application and cannot be scripted. A first run will prompt, and in CI it simply will not work |

### The part that makes this worth doing properly

**On Windows and macOS the harness can address elements by their automation name, not by pixel
coordinates** — and `AccessibilityCoverageTests` already forces every interactive control in every
template to carry `AutomationProperties.Name`. `LeftPaneName`, `RightPaneName`, `GutterName`,
`MinimapName` and `StatusStripName` are public properties a host sets, and the demo sets them.

So the accessibility work already done becomes the addressing mechanism, and a Windows run can say
*"click the element named X"* where the Linux run says *"click 40 pixels from the top-left"*. That is
strictly more robust, and it means the two owed platform runs are cheaper than the Linux one was —
and the verb vocabulary takes a **name or** a coordinate — **decided 2026-09-17**.

## Decisions

| Decision | Why |
|---|---|
| **One `scripts/drive-demo.cs` with a platform back end, plus `.sh` / `.ps1` wrappers** | The convention four existing tools already follow. A pile of platform shell scripts is what this plan exists to stop |
| **Verbs take a name *or* a coordinate** | Decided 2026-09-17. Windows and macOS can address by automation name; X11 cannot, here. A vocabulary that only speaks coordinates throws away the a11y work and makes the owed runs harder than the one already done. Each back end implements what it can and errors clearly on what it cannot, and every captured result records which form produced it — the two platforms are genuinely not doing the same thing |
| **`catch-crash` is a separate tool, not a verb** | It drives no window. It belongs in `scripts/` on its own and is useful on every platform today |
| **`measure-menu-width` goes beside `gen-locale-review`, not in the driver** | It reads resource XML. It is a locale tool that happened to be written during a by-hand pass |
| **The mutation harnesses stay in scratch** | Each is written against one phase's code and is contradicted by the edit that lands it. Your own rule: *do not keep a file whose correctness decays* |
| **The tooltip experiment becomes a `hover` verb, expected to fail on X11** | A script that can never succeed is not worth committing, and the negative is *already* recorded twice in prose (`AGENTS.md:494`, `DECISIONS.md:2200`) — so "it records a finding" is not the reason. The reason is that **it may well succeed on Windows**: the X11 failure is that synthetic `xdotool` motion produces no dwell state, and `SendInput` on Windows generates real pointer input that plausibly does. So `hover` is a verb whose X11 implementation is known-failing and whose Windows implementation is a genuine test of the back end — which is a tool, not a museum piece |
| **`launch-demo` folds into `run-demo --detach`** | One launcher. The exit-144 trap becomes a flag on the script that already exists, rather than a second script that drifts from it |

### Dismissed

| Alternative | Why not |
|---|---|
| Commit the five shell scripts as they are | Three hardcode a uid, a compositor, a display and a repo path, and all three parse English `xwininfo` output in a repository that tests under `de-DE` |
| Keep everything in scratch and enrich `AGENTS.md` §9 further | The status quo. §9 is already thorough and the rebuilding still happens, because prose is not a program |
| A shell driver per platform | Three implementations, three drift paths, and `pwsh` on Windows would still need P/Invoke for capture. The C# app puts the platform choice in one file |
| Drive through AT-SPI on Linux for name-based addressing | Would unify the vocabulary across all three platforms, and is the better long-run answer. Out here: it needs Avalonia's AT-SPI backend verified first, which is its own spike, and the coordinate path already works |

## Scope

**In.** `scripts/drive-demo.cs` + wrappers; `scripts/catch-crash.cs` + wrappers;
`scripts/measure-menu-width.cs` + wrappers; a `--detach` flag on `run-demo`; `AGENTS.md` §9 rewritten
to point at the tools rather than describe them; the five scratch scripts deleted once their content
has landed.

**Out.**

| Excluded | Note |
|---|---|
| The Windows and macOS runs themselves | This builds the instrument. Using it is plan 00001's outstanding debt, and needs those machines |
| AT-SPI / name-based addressing on Linux | A spike of its own |
| CI | macOS needs interactive permission grants; none of this belongs in a headless job |
| The mutation harnesses | Scratch, by decision above |
| Plan 00021's probes | Scratch until that plan lands |

## Phases

| Phase | Size | Content |
|---|---|---|
| **1 — `catch-crash`** | S | The one tool with no UI and no platform dependency. Committed first because it is useful the moment it lands and it proves the `.cs` + wrappers shape end to end |
| **2 — The driver, X11 only** | M | `drive-demo.cs` with the seven verbs and the X11 back end; the three capture scripts reduced to calls into it; `xwininfo -int` and positional parsing, so it survives a non-English desktop; `XAUTHORITY`, display and repo path all discovered rather than assumed. **Verified by reproducing a capture each existing script already produced** — the scratch PNGs are the oracle |
| **3 — `run-demo --detach` and the locale tool** | S | The exit-144 launch folded in; `measure-menu-width` ported beside `gen-locale-review` |
| **4 — The Windows back end** | M | `System.Windows.Automation` + `SendInput` + `PrintWindow`, addressing by automation name. **Cannot be verified here** — it is written against the vocabulary and proven on the machine when the owed run happens, which the plan must say plainly rather than pretend otherwise |
| **5 — The record** | S | `AGENTS.md` §9 rewritten to name the verbs; the macOS back end specified but not written, with its permission problem recorded; `DECISIONS.md`, `PROGRESS.md`, `CHANGELOG.md`; scratch cleared |

## Testing

A harness is hard to test because its subject is a running window. What is testable:

| Test | Asserts |
|---|---|
| **The verb parser round-trips** | Every verb, its arguments, and the name-or-coordinate form, without launching anything |
| **The X11 back end reproduces a known capture** | Phase 2 against the scratch PNGs already produced by the scripts being replaced — the only real oracle available |
| **`catch-crash` reports a short total as a failure** | Fed a captured log of the real SIGABRT run, it must not report success. **The defect it exists for is a false green**, so a test that only checks the happy path would be worthless |
| **Positional `xwininfo` parsing survives a non-English locale** | Run the parser against captured `LC_ALL=de_DE` output. This is the defect that motivated the rewrite and it must be proven able to fail |
| **Nothing assumes a uid, a display or a repo path** | A grep gate over `scripts/`, in the shape of the existing documentation gates |

## Risks

| Risk | Assessment |
|---|---|
| **Phase 4 cannot be verified where it is written** | The Windows back end is written on Linux against a vocabulary. Mitigated only by keeping the verb surface small and by the phase being separable — if it is wrong, the Linux harness is unaffected |
| **macOS permission grants are interactive** | Cannot be scripted, cannot run in CI, and will prompt on first use. Specified in phase 5 and deliberately not written until someone is at that machine |
| **A harness is not the owed work** | Plan 00001 phase 10 owes *runs*, not instruments. This makes them cheaper; it does not discharge them, and it must not be allowed to read as if it had |
| **Wayland** | The X11 path needs XWayland, which is what is in use here. A pure-Wayland session has no equivalent of `x11grab` by window id, and this plan does not solve that |
| **Scope creep into a test framework** | The verbs are a driver, not an assertion library. Anything that starts comparing images belongs with `PixelProbe` and `AGENTS.md` §5, which already say a snapshot is never the guard for anything smaller than a row |

## Conventions

Conventional Commits, dense bodies, no AI attribution trailer. New tests are proven able to fail
before they are committed. An approved plan is committed before implementation and never edited;
drift goes to `DECISIONS.md`. Work on `feat/the-harness-the-prose-describes`, branched from `main` at `bf4b14f`.

**Phase 1 goes first, ahead of plan 00021** (decided 2026-09-17). `catch-crash` guards the
intermittent test-host abort that reports `Failed!` with `failed: 0` and a short total — during
00021's 2,785-line move, whose entire oracle is the 676 tests, that is a false green at the worst
possible moment. **Phases 2 to 5 follow 00021.**

**Scratch is cleared only after the content has landed** — each phase deletes the scratch scripts it
replaces, in the same change, so the two never both exist as the source of truth.
