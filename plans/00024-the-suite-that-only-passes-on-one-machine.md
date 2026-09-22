# 00024 — The suite that only passes on one machine

> Status: **approved 2026-09-22**. Supersedes nothing.
> Written 2026-09-18, the day this repository first had a remote and CI first ran; approved four
> days later, after the same two defects were observed failing identically on a second branch.

For twenty-three plans the suite was green. It was green on **one machine**. The first CI run —
2026-09-18, run `35375856161` on `main` at `65ce70d` — is the first time any of this was executed
anywhere else, and three defects were waiting.

| Job | Total | Failed | |
|---|---|---|---|
| Trim Check (Release linux-x64) | — | — | green |
| Build & Test (ubuntu-latest) | 679 | **2** | A, B |
| Culture Leg (de-DE) | 679 | **2** | A, B |
| Build & Test (windows-latest) | 679 | **68** | A, B, and 32 rendering tests |
| Build & Test (macos-latest) | 679 | **68** | the same 32 |

679 rather than 681 because `main` does not carry plan 00021 phase 1, which is on its branch.

This plan does **not** cover the NU1008 failure of the first run. That was fixed on the spot by
`reference/Directory.Packages.props` (`65ce70d`) and is recorded in `DECISIONS.md`.

## The three defects

### A — `docs/theme-audit.md` is stale on every CI machine, and nobody knows why

**Where this test lives has moved.** On `main` it is
`Bennewitz.Ninja.ThemeAudit.Tests.ReferenceAuditTests`; on
`refactor/themeaudit-moves-to-xamlquality` the in-repo tool is gone and the gate has moved to
`Bennewitz.Ninja.DiffView.Tests.ReferenceAuditTests`, reached through the published package, with
its message now naming `theme-audit compat` and `theme-audit report` rather than
`dotnet run --project src/ThemeAudit`. **It fails identically on both**, which is the strongest
evidence available that the cause is environmental rather than anything in the tool: replacing the
tool outright did not move it.

`ReferenceAuditTests.The_committed_report_equals_a_fresh_run` fails on all four test jobs with
*"a fresh run differs (written to `docs/theme-audit.received.md`)"*. It **passes here**, and the
cause is not any of the cheap explanations:

| Hypothesis | Measured |
|---|---|
| The reference checkouts have drifted from their pins | **No.** All four resolve exactly: Avalonia `12.1.2` → `d3c867a`, AvaloniaEdit `12.0.0` → `86fdebe`, Semi.Avalonia `v12.1.0.1` → `5c1fa38`, DiffPlex pinned by commit and at it |
| The committed report was simply never regenerated | **No.** The test passes locally against the committed file |
| Unsorted directory enumeration | **No.** `XamlFiles` already sorts with `.Order(StringComparer.Ordinal)` |

So the difference is environmental and **not yet identified**. CI writes the received file and
throws it away, which is the first thing to change: this plan cannot state the fix, only the step
that will reveal it. Writing a guessed fix into a plan is how a plan stops being a record.

### B — `PackagingTests` packs a configuration the suite never built

`The_readme_each_package_declares_is_inside_the_package` runs `dotnet pack … --no-build`, and
`dotnet pack` defaults to **Release**. The suite builds **Debug**. The comment at the call site
reads *"the suite has already built this configuration"*, and that is the defect in one sentence —
it has not. The error names the path, which is the proof: `bin/Release/net10.0/de-DE/DiffView.Avalonia.resources.dll`
*"was not found on disk"*, NU5026.

It has only ever passed here because `bin/Release` is populated by the release work of plans 00018
and 00019. **A green result standing on stale build output is the same false green as the aborted
test host** — the defect `catch-crash` exists for. A clean checkout has never had it.

### C — 32 rendering tests fail on Windows and macOS

| Kind | Count | What fails |
|---|---|---|
| Verify image baselines | **30 methods across 16 classes** | 10–13% of pixels differ — not antialiasing, glyph rasterization |
| Measured pixel assertions | **2** | `MarkerChipTests.The_glyph_is_centred_in_its_chip`, `MarkerGlyphTests.Every_marker_is_heavy_enough_to_scan` |

68 failures from 34 methods, most parameterised by theme variant.

**The bundled font is not the fix it was taken for.** `fixtures/fonts/DejaVuSansMono.ttf` fixes
*which glyphs are drawn*; it fixes nothing about *how they are rasterized* — hinting, subpixel
positioning, the platform's font engine, and on macOS an arm64 Skia besides. Linux is green only
because every verified PNG was generated on Linux.

The two measured assertions matter more than their count. They are not baselines — they compute
over the frame — so the second defect is not "our baselines are stale" but **"two pixel
measurements are tighter than the platform spread"**, which no baseline strategy addresses.

## Decisions

| Decision | Why |
|---|---|
| **B is fixed by packing the configuration the suite built**, not by dropping `--no-build` | The comment's intent was right and its premise was wrong. Keeping `--no-build` keeps the check cheap; the fix is to name the configuration rather than inherit a different default |
| **A gets a diagnostic step before a fix**, and this plan does not name the fix | Three hypotheses were measured and all three are dead. A plan that guesses the fourth is a plan that will be wrong in writing and frozen that way |
| **Verify baselines run on Linux only; Windows and macOS run everything else** | A baseline is a record of what a human accepted. Committing three sets means committing two that nobody has ever looked at — a verification nobody performed. One reviewed set on one platform, plus the by-hand runs below, is the honest shape |
| **The owed Windows and macOS demo runs are the cross-platform rendering check** | They have been owed since plan 00001 phase 10 and this reframes them: they are not a nicety, they are the only thing that ever looks at rendering on those platforms. Plan 00023's harness is what makes them repeatable |
| **The two measured pixel assertions are re-derived against the platform spread**, not skipped | They assert something real — a glyph is centred, a marker is heavy enough to scan — and both are *properties*, not appearances. A property that only holds at one rasterization was always too tight |
| **CI keeps running all three platforms** | The 2 platform-independent failures were found precisely because Windows and macOS ran. Dropping them to get green would discard the thing that just paid for itself |

### Dismissed

| Alternative | Why not |
|---|---|
| Per-platform baselines (`UniqueForOSPlatform`) | ~180 PNGs, two thirds of them generated by a machine and reviewed by nobody, and every future visual change becomes a three-way regeneration. Triples the drift surface to buy coverage the by-hand runs give honestly |
| A tolerance on the image comparison | `AGENTS.md` §5 already records that at `ChannelTolerance = 8` and `MaxDifferingFraction = 0.005` a frame comparison passes while half a percent of pixels differ. The observed spread is 10–13%; a tolerance wide enough to pass would be wide enough to hide a real regression |
| Drop Windows and macOS from CI | They are the only reason two of these three defects are known |
| Regenerate the baselines on CI and commit whatever it produces | Same objection as per-platform baselines, without even the excuse of coverage |
| Fold this into plan 00021 | Unrelated to the viewer, and 00021 phase 1 is already on a branch. A CI defect that blocks every future run should not queue behind a feature |

## Scope

**In.** `PackagingTests`' pack configuration; the CI artifact that reveals defect A, and the fix it
points to; the platform gating of the Verify snapshot suite; the two measured pixel assertions;
`ci.yml` where it must change; `AGENTS.md` §5 on what a snapshot does and does not prove;
`DECISIONS.md`, `PROGRESS.md`.

**Out.**

| Excluded | Note |
|---|---|
| The NU1008 shield | Already fixed on `main` as `65ce70d` |
| Plan 00021 phases 2–4 | The viewer is unrelated and continues on its branch |
| The by-hand Windows and macOS demo runs | This plan reframes them and does not perform them; they need those machines, and plan 00023 phases 2–5 are what make them repeatable |
| The eight locale readers, the Trusted Publishing policy | Release gates, decided 2026-09-18 and unrelated |
| Making the snapshots platform-independent | Not achievable. Rasterization is the platform's, and pretending otherwise is what produced this |

## Phases

| Phase | Size | Content |
|---|---|---|
| **1 — Two fixes and one question** | S | `PackagingTests` names its configuration, proven by a clean-tree pack. `ci.yml` uploads `docs/theme-audit.received.md` and any Verify `*.received.*` as artifacts on failure, so a red CI run stops being a dead end. **Ends with defect A diagnosed and written down, not fixed** |
| **2 — Defect A** | S–M | Whatever phase 1 revealed. Sized once it is known; if it turns out to need a different shape than this plan assumes, it supersedes rather than stretches |
| **3 — The rendering split** | M | Verify snapshots gated to Linux by a trait the runner filters, not by a silent skip — a test that quietly does not run is worse than one that fails. The two measured assertions re-derived against the observed spread, each with the number it now tolerates and why. `AGENTS.md` §5 says plainly what a snapshot proves and on which platform |
| **4 — The record** | S | `DECISIONS.md`, `PROGRESS.md`, and the resume anchor. Green on all five jobs is the done-when |

## Testing

Every new test is proven able to fail by mutation before it is committed.

| Test | Asserts |
|---|---|
| **The packaging check packs what was built** | Passes from a tree with no `bin/Release` at all — the condition CI has and this machine has not. Proven by deleting `bin/Release` locally and watching it go red before the fix |
| **A skipped snapshot is visibly skipped** | The Linux-only gate reports as filtered-out on Windows and macOS, with a count. **A silent skip is the failure mode here**: 30 tests that quietly vanish read as 30 tests that passed |
| **The two measured assertions hold at their new bound** | And fail below it. The bound is a number in the test with the platform spread beside it, not a widened constant |
| **The 679 existing tests** | Green on all five CI jobs, which is the first time that sentence will have been true |

## Risks

| Risk | Assessment |
|---|---|
| **Defect A is unidentified and phase 2 is sized blind** | Real, and the reason phase 2 is separated rather than guessed. If the cause is structural the honest move is a superseding plan, and the plan says so before the fact |
| **Gating snapshots to Linux loses real coverage** | It does. The mitigation is the by-hand runs, which are owed anyway, and the admission in `AGENTS.md` §5 that the snapshot suite proves one platform's rendering. The alternative buys coverage with baselines nobody reviewed, which is worse and harder to see |
| **The two pixel assertions may be measuring the wrong thing** | Possible. If a centred glyph cannot be asserted across platforms at any useful bound, the property needs re-deriving rather than the bound widening — recorded as a finding rather than papered over with a constant |
| **Every push now costs five jobs** | Free on a public repository, but a red `main` is the default branch of a repository that just went public. Phase 1 lands first for that reason |
| **16 branch pointers are unpushed** | Every one would fan out five red jobs today. They stay local until `main` is green |

## Conventions

Conventional Commits, dense bodies, no AI attribution trailer. An approved plan is committed before
implementation and never edited; drift goes to `DECISIONS.md`. Work on
**phase 1 directly on `main`; phases 2 and 3 on
`fix/the-suite-that-only-passes-here`, branched from `main` once phase 1 has landed.**

Decided 2026-09-22, against this repository's every-plan-gets-a-branch habit, and the reason is
specific rather than a loosening. Phase 1 is a one-line configuration fix and a CI artifact upload —
both strict improvements to a branch that is *already red* — and its whole purpose is to make CI say
something it cannot say today. CI only judges what is pushed, and `main` is the branch that is
broken, so a fix for it sitting unmerged on a branch leaves the default branch of a public
repository knowingly failing for no gain. Phase 3 is different in kind: it changes how thirty-two
tests execute, and that gets isolation and room to think.

The same two defects also fail on `refactor/themeaudit-moves-to-xamlquality`, so phase 1 unblocks
two branches at once and neither can demonstrate green until it lands.

**A fix is proven on the machine that lacked it.** Every one of these three passed here for months.
A local green is not evidence for any of them; the evidence is a CI job, and phases land in an order
that lets CI judge each one.
