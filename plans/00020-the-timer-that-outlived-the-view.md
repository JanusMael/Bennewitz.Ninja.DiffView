# 00020 — The timer that outlived the view

> Status: **approved 2026-09-15**. Supersedes nothing.

Plan 00019 found an intermittent test-host abort, repaired the test that exposed it, and left the
defect underneath open. A step-back review of the **first** draft found that the defect it named was
the wrong one, and that a second, real one had been sitting beside it unnoticed. A step-back review
of the **second** draft then applied that draft's own fix and measured it: it is necessary, it is not
sufficient, and none of the four tests the draft proposed could tell the difference.

This draft is the third. Its shape is set by three rounds of measurement, below.

## What the probes settled

**Round one — plan 00019, the dispatch question.** The first draft was about
`StatusController.DispatchToUiThread` invoking inline when `Dispatcher.UIThread.CheckAccess()` says
yes, outside the method's own `try`/`catch`. It rested on *"very likely test-host-only, but that is
carrying the weight"*. That sentence was replaced by a measurement:

| Probe | Result |
|---|---|
| `Thread.CurrentThread.IsThreadPoolThread` on the headless UI thread | **True** |
| `Thread.CurrentThread.Name` | **`.NET TP Worker`** |
| `Dispatcher.UIThread.CheckAccess()` from a fresh pool thread | False |

The headless host runs Avalonia's UI loop on a thread-pool thread, which is handed back when the test
ends; a later timer callback can land on it and `CheckAccess()` — which compares thread identity —
answers true for a thread whose dispatcher state has moved on. A desktop application runs a
dedicated, non-pool UI thread, so a timer callback is never on it and the call always posts. **The
abort cannot occur in production**, at this site or the other four. The dispatch change is dropped.

**Round two — is `OnDetachedFromVisualTree` the right hook?** The release only reaches the scenario
this plan headlines if closing a window detaches its content. Measured, not assumed:

| Probe | Result |
|---|---|
| `Window.Close()` → content's `OnDetachedFromVisualTree` | **fires** (`detached=1`) |
| `Window.Content = null` → same | **fires** (`detached=1`) |
| Re-attaching the same control → `OnAttachedToVisualTree` | fires (`attached=2`) |
| Re-attaching the same control → `OnApplyTemplate` | **does not re-run** (`templated=1`) |
| `CompositeHost.Dispose()` (which is `Window.Close()`) with a message on screen, **today** | `ActiveTimers=1`, text still set |

So the hook is right, closing is covered, and the rebuild after a re-attach must come from the lazy
getter rather than from a re-applied template — which is what the *null* in the release buys.

**Round three — does the release hold?** The second draft's fix
(`_status?.Dispose(); _status = null;` in `OnDetachedFromVisualTree`) was applied verbatim to
`SideBySideDiffView` and measured:

| Probe, with the fix applied | Result |
|---|---|
| Post a success, detach | `ActiveTimers=0` — **the draft's own test, and it passes** |
| …then flip `IgnoreCase` and post another message | **`ActiveTimers=2`** |
| Detach first, then let a build land | `0` → **`ActiveTimers=1`**, text `Compared 33 rows in 0 ms` |

**The release is not durable, and the cause is one line.** `UpdateStrip()` ends with
`StatusController status = Status;` — the **lazy getter** — at `SideBySideDiffView.cs:2956` and
`InlineDiffView.cs:1665`. There are **19 and 15 `UpdateStrip()` call sites** on the two views, several
of them on async completion paths. So any strip refresh after a detach builds a fresh controller and
re-subscribes `Changed`, and the next message arms a fresh timer on a view that will never detach
again.

## The actual defect

**Nothing ever releases `StatusController`.** Both views create it lazily and neither disposes it:

- `SideBySideDiffView.cs:931` and `InlineDiffView.cs:689` — `_status = new StatusController(TimeProvider)`.
- There is no `_status?.Dispose()` anywhere in `src/`, and neither view overrides any attach or
  detach method at all.

A success message arms a 6-second auto-clear; a warning, 10 seconds. Until that timer fires it holds
its callback, which holds the controller, which holds a `Changed` subscription to the view — so a
**closed or detached view stays reachable from the timer queue for up to ten seconds**, and the
callback then mutates a status strip belonging to a view nobody is looking at.

**The retention is bounded at ten seconds and always was** — the callback fires, the timer unroots
itself, and the view becomes collectable. That bound is worth stating plainly, because it is the
honest measure of what this plan is worth: it does not stop an unbounded leak, it stops a control
from touching a view that has left the tree. The second draft claimed to leave *no* timer behind;
round three shows that claim is not reachable without changing `StatusController`, which is out of
scope. This draft claims what it can prove instead.

## Goal

**A view that leaves the tree cancels what it has armed, and no strip refresh re-arms it.** A view
that comes back still works. What in-flight work can still arm after a detach is named, bounded and
recorded rather than engineered away.

## Decisions

| Decision | Why, and what was dismissed |
|---|---|
| **Release on detach with `_status?.Dispose(); _status = null;`** *(locked 2026-09-15)* | The null is what makes it correct. `Dispose()` latches `_disposed`, and `Set` does `ObjectDisposedException.ThrowIf(_disposed, this)` — so disposing *alone* breaks every view that comes back: a tab switch, a virtualized list, a reparent. Nulling hands the next `Status` access to the existing lazy getter, which builds a fresh controller and re-subscribes `Changed`. Re-attach is safe for free. **Round three leaves this decision intact and shows it is not sufficient on its own** |
| **The statement order is load-bearing, and carries a comment saying so** | `Dispose()` raises `Changed` synchronously → `OnStatusChanged` → `UpdateStrip`. The release avoids resurrecting the controller it is disposing only because `_status` is still non-null at that instant. Reversed, the two lines create a controller instead of releasing one |
| **`UpdateStrip` reads the field, never the getter** | The substantive addition from round three. `StatusController? status = _status;` with `?.` on the three reads. When `_status` is null the getter would have built a controller whose `Text` is null, `Kind` is `None` and `IsDismissible` is false — exactly the values the null-coalescing form writes, so **nothing visible changes** and 34 resurrection paths close at once |
| **A post-detach completion may arm one more timer. Accepted, bounded, recorded** | Once `Set` has raised `Changed` it arms its timer *afterwards*, so no hook driven by `Changed` can cancel what that call is about to create. Reaching zero means gating all 24 `Set…` call sites or changing `StatusController`. The residue is one timer of at most ten seconds, only when work started before the detach lands after it — strictly smaller than today's, and the same bound. It goes to `DECISIONS.md` with the numbers |
| **A failure does not survive a detach** | `Dispose()` calls `Apply(null, StatusKind.None)` unconditionally, so it clears `Failure` — documented as *"sticks until dismissed or replaced"* — and `State` along with the auto-clearing kinds. The second draft justified the loss as *"correct for a message that was going to clear itself in six seconds"*, which is true of `Success` and `Warning` and false of the one kind that exists to persist. Tabbing away from a failed diff and back now shows empty panes and no error. Accepted because a detach is also what a re-`Show` recovers from, and the alternative — preserving a message across a disposal — reintroduces the ownership the release exists to remove. **Named here rather than discovered later** |
| **`Status`'s lifetime is documented on the property** | `Status` is public on both views and returns a public class with settable `SuccessAutoClearDelay` / `WarningAutoClearDelay` and a public `Changed` event. Today it returns one instance for the life of the view; after this change, one per attach. A host that stashed it gets `ObjectDisposedException`; a host that configured a delay or subscribed `Changed` loses both **silently**. The guide never mentions `Status` and the demo never stashes it, so nothing breaks today — but the package id is permanent once published, so the summary says it in the same commit |
| **The dispatch inline path is recorded, not fixed** | Measured as headless-only (round one). Changing shipped code to harden a test host is the wrong trade, and the same argument retires it at the other four `CheckAccess()` sites |
| **Plan 00019's test fix stays** | A test that leaves a real-clock timer armed is worse than one that does not, independently of this |

### Dismissed

| Alternative | Why not |
|---|---|
| Release inside `DetachParts()` | The established teardown site, and it already ends in `_sync?.Dispose(); _sync = null;` (`SideBySideDiffView.cs:3130`) — the very idiom this plan proposes. It is wrong here: `DetachParts()` runs from `OnApplyTemplate` (`:1172`) only, and round two measured that the template is **not** re-applied on re-attach, so it would fire once and never again |
| Cancel in-flight work on detach | Would close the residue by stopping the build whose completion arms the timer. It also cancels a build across an ordinary tab switch, so the user returns to a view that has stopped working — a worse regression than ten seconds of retention |
| A hook on `Changed` that re-releases while detached | Looks like four lines and does not work: `Set` raises `Changed` **before** it arms, so the release runs and the timer is created after it. Written down because it is the obvious next idea |
| `Dismiss()` on detach | Cancels the timer and blanks the message, and is smaller — but the controller and its `Changed` subscription survive, so it releases by side effect rather than by design, and it has the identical post-detach residue |
| Make the view `IDisposable` and let the host do it | Honest about ownership and worse in practice: every consumer must remember, and the hosting guide would have to teach it. A control that leaks unless disposed is a worse default than one that tidies up after itself |
| Fix `DispatchToUiThread` as well | See round one — measured as unreachable in production |

## Scope

**In.** The detach override and the release on both views; the `UpdateStrip` field read on both
views; the `Status` summary; the tests that pin all of it; `DECISIONS.md`, `PROGRESS.md`,
`CHANGELOG.md`.

**Out.**

| Excluded | Note |
|---|---|
| `StatusController` itself | Its lifecycle, token, delays and typed setters are fine and covered. This plan changes who calls `Dispose`, not what it does — which is also why the post-detach residue stays a recorded fact rather than a fix |
| The five `CheckAccess()` sites | Measured as production-unreachable. The probe result is recorded so the next person does not re-derive it |
| Any public API **signature** change | `_status` is private and the lazy getter already exists. Nothing moves; only the summary gains a sentence about lifetime |
| `ScrollSync` | **Answered, not deferred.** `SideBySideDiffView.cs:3130` already does `_sync?.Dispose(); _sync = null;` as the last act of `DetachParts()`. The second draft left this open; one grep closes it, and there is nothing for phase 2 to record |

## Phases

| Phase | Size | Content |
|---|---|---|
| **1 — The release** | M | `OnDetachedFromVisualTree` on both views: `_status?.Dispose(); _status = null;`, with the ordering comment and `/// <inheritdoc/>` — the build is `-warnaserror` and a protected override without one is `CS1591`. `UpdateStrip` reads `_status` on both views. The `Status` summary gains its lifetime sentence. Tests below, seen red first |
| **2 — The record** | S | `DECISIONS.md` takes the three probe rounds, supersedes plan 00019's open finding, and records the post-detach residue with its measured numbers; `PROGRESS.md`'s *Open* item 3 closes; `CHANGELOG.md`. Also: plan 00019's lesson that *"applying the template is a touch of `Status`"* is **no longer true** once `UpdateStrip` stops calling the getter, so the note that `TimeProvider` must be set before `Show()` is relaxed, not deleted — it is still the safe order |

## Testing

Every new test is proven able to fail before it is committed.

| Test | Asserts |
|---|---|
| **A detached view leaves no armed timer** | Through `CompositeHost` on its `FakeTimeProvider`: post a success, detach, assert `ActiveTimers` is 0. The direct statement of the defect, and it fails today |
| **A build that completes after detach arms at most one timer, and it drains** | The fifth test, and the one round three shows the other four cannot stand in for. Detach, let a build land, assert `ActiveTimers` is exactly 1 — then advance past `WarningAutoClearDelay` and assert 0. It pins the residue as *bounded*, which is what this plan claims, and it turns red the day someone makes it unbounded |
| **A strip refresh on a detached view does not rebuild the controller** | Detach, change a property that refreshes the strip and never mentions `Status` (`IgnoreCase`), assert `ActiveTimers` is still 0. Measured at **2** against the second draft's fix; this is the test that was missing |
| **A re-attached view still takes a message** | Detach, re-attach, `SetSuccess`, assert the text lands. This is the `ObjectDisposedException` trap that makes the naive one-line fix wrong, so it is pinned before the fix is trusted |
| **Detaching with a message on screen does not throw** | `Dispose` calls `Apply`, which raises `Changed` into `UpdateStrip` during teardown. Exercised on both views |
| **Both views, not one** | `InlineDiffView` has the same lazy field, the same omission and the same getter call in `UpdateStrip`; a fix to one only is the likelier mistake |
| The existing status suite is untouched | `StatusControllerTests` injects its own dispatcher and clock and must stay green with no edits |

## Risks

| Risk | Assessment |
|---|---|
| **`OnDetachedFromVisualTree` fires more often than "closed"** | It does — tab switches and virtualization both detach. That is exactly why the release nulls rather than only disposing, and why re-attach has its own test. The visible cost is that a message does not survive a tab switch, **including a failure** — decided above rather than absorbed |
| **Every composite test now disposes a controller at teardown** | `CompositeHost.Dispose()` is `Window.Close()`, and round two measured that closing detaches. So the re-entrancy path runs across the whole composite suite, not only the test that names it. Good coverage, and the reason phase 1 is M rather than S: a regression here shows up as a broad failure, not a narrow one |
| Disposing during detach re-enters the strip | `Apply` raises `Changed` → `UpdateStrip` while the view is detaching. On the UI thread, and `UpdateStrip` returns early when `_statusStrip` is null (`:2885`); covered by its own test rather than assumed |
| The residue is mistaken for the whole fix being pointless | Ten seconds, bounded before and after, narrowed to work that lands post-detach. The case for doing it is that a control should not touch a view that has left the tree — not the byte count |
| `UpdateStrip` reading the field changes what a null means | It does not: the getter built a controller reading `null` / `None` / `false`, which is what the null-coalescing form writes. Pinned by the existing strip snapshots, which must not move |

## Conventions

Conventional Commits, dense bodies, no AI attribution trailer. New tests are proven able to fail
before they are committed. An approved plan is committed before implementation and never edited;
drift goes to `DECISIONS.md`. Work on `fix/the-timer-that-outlived-the-view`, branched from `main` at
`8d05492`; a completed branch merges without asking once it is proven stable.
