# 00015 — A test that asserts English says so

Left open by plan 00014 phase 3 and recorded in `DECISIONS.md` §"The suite is culture-dependent in
94 places, and only 32 of them are defects". The suite pins `en-US` in `HeadlessTestApp`, so every
assertion of library text passes on any machine; flip the pin with `DIFFVIEW_TEST_UI_CULTURE=de-DE`
and 94 of 606 fail. That leg is the audit plan 00014 asked for and deliberately did not write,
because as a whole-suite gate it is permanently red for a reason that is not a defect.

**The recorded split was made by class name, and measuring it by failure mode moves it.** Re-run on
2026-09-13: **60 are snapshot failures** — a `VerifyException` over a `.png` — and **34 are
behavioural assertions of English text**, not 62 and 32. The class that moves is
`EditingSnapshotTests`, whose two failures are `Assert.Contains("Left", …DirtyText)` standing in
front of a `Verify` call that therefore never runs. Its name says snapshot; its failure is a defect.

**That is not an arithmetic correction, it is the shape of the work.** An assertion that precedes a
capture hides the capture's own verdict behind it, so **neither number is final until the
behavioural half is fixed**. The 60 is a lower bound, and the list of frames to pin cannot be
written before the 34 are.

The decision's conclusion survives intact: the two halves land together or neither does.

## Goal

`dotnet test` under `DIFFVIEW_TEST_UI_CULTURE=de-DE` is green, and a CI leg holds it that way, so
that **a test failing under that leg means exactly one thing**: it asserted English text without
saying it wanted English. Today that signal is buried under 60 frames doing the right thing.

## Non-goals

| Excluded | Note |
|---|---|
| **German snapshot baselines** | A committed frame is a picture of English chrome. Rendering `de-DE` and committing that doubles 60 baselines, and the second copy asserts nothing the first does not — it only has to be regenerated twice whenever a pixel moves |
| **A comparer that tolerates text differences** | It would tolerate exactly the differences it exists to catch. `AGENTS.md` §5 already records the tolerance as this repository's recurring trap: a change smaller than `MaxDifferingFraction` passes every snapshot while the frames depict the old drawing |
| **Fixing the 34 by asserting through `DiffViewStrings.Get`** | The obvious fix and the wrong default — see *A defect on both sides of a comparison cancels*. Available per test where the test's subject really is the wiring rather than the words, and named as a deviation when used |
| **The de-DE leg on all three OSes** | The axis under test is the culture, not the platform. One `ubuntu-latest` job; the existing matrix stays English |
| **The other seven locales as legs** | `de-DE` is the canary. Eight legs cost eight times as much to catch one class of defect, and the parity gate already holds the other seven structurally |
| **`CurrentCulture` — numbers, dates, formats** | A separate axis, left on the machine's deliberately (plan 00014, *Two cultures, deliberately*). `DIFFVIEW_TEST_UI_CULTURE` moves `CurrentUICulture` only, and this plan does not widen it |
| **Native review of the eight locales** | Still gates the first release, still not a test. Unrelated to whether the suite says which language it expects |

## Architecture

### The pin is scoped, never ambient — measured, not assumed

The tempting fix is one assignment in `HeadlessTestApp.BuildAvaloniaApp`: pin
`DiffViewStrings.Localization` to `en-US` once and let every frame render English regardless of the
thread's UI culture. **It was tried on 2026-09-13 and it recovers 16 of the 94.**

`LocalizationTests` is the reason, and it is doing nothing wrong: it assigns
`DiffViewStrings.Localization` directly and calls `DiffViewStrings.ResetForTesting()` in a `finally`,
both of which are the seam working as designed. Either one destroys an ambient pin, and because the
Avalonia suite is serial by design (`xunit.runner.json`), **every test after it in the run order
reverts to German**. The result is 78 failures instead of 94, distributed by run order rather than by
cause — which is worse than 94, because it looks like snapshot drift.

So the pin has a scope and a lifetime:

```csharp
using (DiffViewStrings.Override(new DiffViewLocalization { Culture = English }))
{
    …
}
```

`Override` already returns an `IDisposable` that restores the previous value, which is exactly the
lifetime wanted, and a scope re-established per test cannot be destroyed by what a previous test did.

### Where the scope is written, and the attribute question

Wrapping 31 test bodies in a `using` is 31 edits and 31 chances to forget one. xunit's
`BeforeAfterTestAttribute` puts it in one place per class:

```csharp
[EnglishChrome]
public sealed class MenuSnapshotTests
```

**Whether that attribute fires is an open question and phase 1's whole job.** These are not `[Fact]`
tests: the suite uses `[AvaloniaFact]` and `[AvaloniaTheory]` from `Avalonia.Headless.XUnit`, which
dispatch the test body onto the Avalonia thread through their own test case. If
`BeforeAfterTestAttribute` is not honoured on that path, or fires on the wrong thread, the attribute
is worse than nothing — it would read as a pin while pinning nothing, and the suite would still be
green on an English runner. **Phase 1 proves it fires by watching it fail**, before anything is
applied to 31 classes.

### The pin goes on tests, not on classes

`EditingSnapshotTests` is why. One class-level attribute there pins English for
`The_marks_an_edit_leaves_are_painted_and_named`, whose failure is a **real** culture defect, and the
class turns green while the defect stays. A class is the right granularity only where every test in
it is a frame, which holds for the 14 classes contributing the 60 and not for that one.
`PresenterSnapshotTests` is the other end of the same point: it contributes nothing to the 94,
because a presenter frame carries no chrome text, and so it is not pinned at all.

The rule this plan follows: **the pin is applied to what was measured to need it**, test by test,
and a class-level attribute is a shorthand used only where every test in the class is in the list.

### A defect on both sides of a comparison cancels

The cheap fix for the 34 is to replace the English literal with the resolver:

```csharp
Assert.Contains("Left", strip.DirtyText);                              // fails under de-DE
Assert.Contains(DiffViewStrings.Get(SideLeft), strip.DirtyText);       // passes under anything
```

The second line passes in every culture **and asserts almost nothing**. If the strip is wired to the
wrong key, both sides move together and the test stays green — the failure mode plan 00014 already
met once, where `An_escaped_brace_is_not_a_placeholder` survived a mutation because English and the
locale escaped the same braces.

**The default is therefore to pin, not to soften**: keep the English literal, and say the test wants
English. Softening is correct only where the test's subject is the wiring rather than the words —
`EditingSnapshotTests` line 94 is the existing example, asserting that the header's word *is* the
resolved `HeaderDirty` — and every use of it is a deliberate choice recorded at the call site.

## Phases

| Phase | Size | Content |
|---|---|---|
| **1 — The mechanism, proven to fire** | S | `EnglishChrome` (or an explicit `using` helper if the attribute does not fire under `[AvaloniaFact]`), applied to exactly one class, and **proven by watching the de-DE leg's failures for that class go from N to 0 and back**. A pin that cannot be seen working is not a pin. Ships alone; changes nothing for the English run |
| **2 — The 34** | M | Every behavioural failure decided individually: pin English, or assert the wiring, with the reason at the call site. Re-run the leg afterwards — **this is where the hidden snapshot failures surface**, because an assertion that fails today stands in front of a `Verify` that never ran. The list for phase 3 is an output of this phase, not an input |
| **3 — The frames** | M | The pin applied to the complete snapshot list from phase 2, per test, class-level only where the class is all frames. The leg goes green. No baseline is regenerated — if a frame needs regenerating, that is a finding, not a step |
| **4 — The leg and the record** | S | The `culture-leg` CI job on `ubuntu-latest`, `DECISIONS.md` superseding its own 62/32 arithmetic, `AGENTS.md` §5 gaining the rule, `PROGRESS.md`, `CHANGELOG.md` |

Phase 1 is worth shipping alone and phases 2 and 3 are not: a half-done audit is the state
`DECISIONS.md` already rejected. Phases 2 and 3 merge together.

## Testing

Every new test is proven able to fail before it is committed.

| Test | Asserts |
|---|---|
| **The pin survives a test that resets the seam** | A test that pins English runs *after* one that calls `ResetForTesting`, and still reads English. The load-bearing test of the scope decision, and the one the ambient pin fails — mutated to an ambient pin, it must fail |
| **The pin actually reaches the rendered frame** | Under `de-DE`, a pinned snapshot test's frame matches its English baseline; with the pin removed it does not. Phase 1's evidence that the attribute fires at all |
| The pin does not leak past its test | The next test in the run reads the ambient culture, not English. `Override`'s contract, asserted where it now matters |
| **A pinned test still fails on a wrong key** | The anti-cancellation guard: with the pin in place, a mutation pointing the status strip at the wrong string key must still fail the test. What separates pinning from softening |
| The de-DE leg is green | Phase 3. The gate itself |
| The en-US run is unchanged | 606 green throughout. A pin that changes the English run has changed behaviour, not test hygiene |

## Risks

| Risk | Assessment |
|---|---|
| **`BeforeAfterTestAttribute` does not fire under `[AvaloniaFact]`** | The plan's one unknown, and the reason phase 1 exists and ships alone. If it does not fire the fallback is an explicit `using` per test — more edits, same semantics, no loss of correctness. What must not happen is an attribute that is believed to pin and does not |
| **The 60 is a lower bound** | Measured, not feared: `EditingSnapshotTests` reaches `Verify` only when its assertions pass, so its frames have never been compared under `de-DE`. Phase 2 re-runs before phase 3 fixes its list, which is the whole reason the phases are in that order |
| **A frame turns out to need regenerating** | Then a real rendering difference was hiding behind a culture failure. `AGENTS.md` §5 governs: zero `MaxDifferingFraction`, leave `ChannelTolerance` at 8, regenerate only what fails. Blanket promotion of `.received.png` is how frames a change never touched get rewritten |
| **Pinning masks a future culture defect in a pinned class** | Real and accepted: a pinned test is out of the leg's reach by construction. Mitigated by pinning per test rather than per class, so the pinned set is exactly what was measured and a newly added test is in the leg by default |
| **Some of the 34 are not one-line fixes** | `SideBySideDiffViewTests` contributes 7 and `InlineDiffViewTests` 5, several asserting whole status-strip and banner sentences. Sized M for that reason |
| **The leg doubles the suite's CI time** | About 60s on ubuntu. One job, not three, and it runs the same build |
| A pinned test reads English while the machine renders German, and nobody notices the pin is wrong | The pin is a claim that the test is about English words. Phase 4 records the claim in `AGENTS.md` §5 so it is reviewable, rather than 60 silent attributes |

## Conventions

Conventional Commits, dense bodies, no AI attribution trailer. New tests are proven able to fail
before they are committed. An approved plan is committed before implementation and never edited;
drift goes to `DECISIONS.md`. Work on `test/the-culture-audit`, branched from `main` at `eef7b37`.
