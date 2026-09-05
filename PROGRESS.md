# Progress

## Resume

**Phase 3 — Core model, probing, search engine** of
[plan 00001](plans/00001-side-by-side-diff-control.md) is complete on `main`: `DiffView.Core`
builds a source-indexed `SideBySideDocument` (per-side lines, aligned rows, change blocks with
per-side line ranges, derived padding) from two `PaneSource`s through probe → similarity gate →
DiffPlex line diff → rows → blocks, with diagnostics and warnings; `WordDiffCache` computes
word-level pieces lazily into an LRU keyed by document version; `DiffSearch` finds a query in
either or both sides in row order. Every invariant, failure path and search case of plan
§Phase 3 has a passing test and the `Perf` numbers are recorded below. Next is **Phase 4 —
Pane presenter, padding, gutters** (plan §Phase 4), which lifts the Phase 1 spike's mechanism
into the library and owes the caret-column normalisation after `Home` twice. Both ClaudeForge
contributions (PR #37 and PR #38) are merged and the ClaudeForge pin follows the merge (see
*Upstreamed to ClaudeForge*).

The theme audit regenerates after a pin bump, in this order:

```bash
dotnet run --project src/ThemeAudit -- compat
```

```bash
dotnet run --project src/ThemeAudit -- report
```

## Phases

| Phase | Status | Notes |
|---|---|---|
| 0 Bootstrap | done | 7 tests across three tiers; trim-check clean; manual dialog/F12 check passed |
| 1 Virtual-padding spike | done — go | 6 headless tests, 1 of them `Perf`; priming batched at 256 |
| 2 Theme-key audit and exhaustive dictionaries | done (ClaudeForge PR #38 merged) | `theme-audit` `report` and `compat` over a JSON configuration; inventories model Default fallback, `StyleInclude`, linked files, code providers and brush opacity; contrast scoring against each variant's own surface; reviewed Fluent→Semi and Simple→Semi mappings; `DiffView.Tokens.axaml` + colour-blind sibling; `docs/theme-audit.md` committed with drift tests; runtime resolution and rendering tests under all ten targets; tool packed as 1.1.0 |
| 3 Core model, probing, search engine | done | `PaneSource`, `TextProbe`, `LineSplitter`, `DiffOptions`, `SimilarityGate`, `DiffDocumentBuilder`, `SideBySideDocument` + `Padding`, `WordDiffCache`, `DiffSearch`; 87 unit tests (seven invariants, every failure path, cache, search) and 4 `Perf` measurements |
| 4 Pane presenter, padding, gutters | not started | lifts the spike's mechanism; normalises the caret column after `Home` |
| 5 Composite control, scroll sync, headers, status strip, theming | not started | |
| 6 Word-level highlights and options | not started | |
| 7 Navigation, minimap, connectors, tooltips | not started | |
| 8 Find | not started | |
| 9 Syntax highlighting | not started | |
| 10 Scale, visibility, accessibility | not started | |
| 11 Inline (unified) view | not started | optional |

## Phase 3 verification

| Done-when item | Result |
|---|---|
| Invariant 1 — every line of each side appears exactly once in `Rows`, in order | pass on seven pairs (small, generated 1000-line, identical, empty left, empty right, both empty, no trailing terminator); the pane's row pointers agree with the table and the line count is the editor's |
| Invariant 2 — no row has both sides `null` | pass, and each kind has exactly the sides it implies |
| Invariant 3 — a modified row's pieces concatenate to its lines | pass on every modified row of every pair, in word and character mode, and on edge lines (empty sides, separators only, trailing space) |
| Invariant 4 — blocks disjoint, ordered, covering every changed row, with exact per-side ranges | pass; an empty range sits where the side's next line would go |
| Invariant 5 — `Cr`, `CrLf`, `Lf` variants produce identical rows | pass on a 300-line pair with changes |
| Invariant 6 — `Padding.Before` summed plus `Padding.Trailing` equals the side's `null` rows | pass on every pair; padding before a line equals the run of `null` rows above it |
| Invariant 7 — below the floor on large inputs: unaligned concatenation, `Aligned` false, `TooDifferentToAlign` | pass on a 6,000-line unrelated pair over a 10,000-line threshold; `ForceAlignment` and a higher threshold align it; a small unrelated pair aligns regardless |
| Identical inputs → no blocks; empty left / empty right; binary → `BinaryInput`; mixed and CR-only endings → `MixedLineEndings`; Latin-1 fallback warned; long line → `LongLinesSkipped` and no pieces; cancellation between stages | pass — an empty side is one empty line that pairs with the other side's first line as modified (see `DECISIONS.md`) |
| `WordDiffCache` returns the same instance twice, evicts by LRU, keys by document version | pass |
| Search: scope filtering; `Both` ordering (row, left, column); whole word at line boundaries; `ChangedRowsOnly`; invalid regex → `Error`; catastrophic backtracking → timeout `Error`; `Truncated` above `MaxMatches`; cancellation | pass; a backreference pattern runs on the fallback engine; a pane text shorter than the document is tolerated |
| `Perf`: `Build` on the 10k and 200k pairs; the gate on the unrelated pair | measured, see *Measurements* |
| `dotnet build DiffView.slnx -warnaserror` | clean |
| `dotnet test --solution DiffView.slnx` | 189 passed |
| Tests proven able to fail | the search fixture, the small-pair block expectation and the default-options static initialiser each failed a run during the phase before the code or the expectation was corrected |
| Trimmed publish (`linux-x64`, self-contained) | succeeds, 0 IL warnings, 48 MB |

## Phase 2 verification

| Done-when item | Result |
|---|---|
| `theme-audit report` from a clean directory produces the report against the reference checkouts | pass: `dotnet run --project src/ThemeAudit -- report` writes `docs/theme-audit.md` (4 themes, 6 consumers); `--check` confirms it |
| `dotnet pack` puts `Bennewitz.Ninja.ThemeAudit` in the local feed | pass: `scripts/pack-theme-audit.*` packs 1.1.0 into `../nuget-local` |
| `tests/ThemeAudit.Tests` passes on fixture themes, including one that omits a key and one whose token fails its floor | pass: `Fixtures/audit` (Host omits `HostAccent` under Dark; `AppOwn` fails 4.5 in Dark) and `Fixtures/compat` |
| `docs/theme-audit.md` committed; regenerating changes nothing | pass: `ReferenceAuditTests.The_committed_report_equals_a_fresh_run` and the compat sibling; a mismatch writes a `.received` file beside the committed one |
| Resolution: with the compat dictionary every AvaloniaEdit theme key resolves under all six Semi variants; without it the test names the missing keys | pass, static and at runtime: with compat 0 missing; without, the Fluent theme file lacks 6 keys (`ContentControlThemeFontFamily`, `ControlContentThemeFontSize`, `SystemAccentColor`, `SystemBaseLowColor`, `SystemChromeMediumColor`, `ToolTipBorderThemeThickness`) and the Simple theme file 9 — the plan's "six" counted only the Fluent file |
| Contrast: every `DiffView.*` pair meets its floor under all ten targets | pass: 30 pairs × 10 targets, both palettes, 0 below the floor, 0 unmeasurable |
| Headless: a plain `TextEditor` renders under each Semi variant with the compat dictionary and no resource warning | pass: `ThemeResolutionTests.A_TextEditor_with_its_Fluent_search_panel_renders_under_Semi_with_the_compat_dictionary`, six variants, search panel open and painted, no binding warning |
| ClaudeForge pull request open and referenced here | pass: [JanusMael/ClaudeForge#38](https://github.com/JanusMael/ClaudeForge/pull/38), draft — see *Upstreamed to ClaudeForge* |
| `dotnet build DiffView.slnx -warnaserror` | clean |
| `dotnet test --solution DiffView.slnx` | 102 passed |
| New headless tests proven able to fail | removing the compat include from the `TextEditor` test failed it (the Fluent theme's static references throw at load) before the include was restored |
| Trimmed publish (`linux-x64`, self-contained) | succeeds, 0 IL warnings; the compat dictionaries and tokens compile into `DiffView.Avalonia` |

## Phase 1 verification

| Item | Test | Result |
|---|---|---|
| 1 Padding above a line and after the last line, height exactly `(k + 1) · lineHeight` | `Item1_padding_run_pads_above_a_line_and_after_the_last_line_by_whole_rows` | pass: 4 rows for 3 above, 5 rows for 4 trailing, text row centred as a plain line's; glyph pixels only in the text row |
| 2 Caret skips the zero-length element; click in padding lands on the adjacent line | `Item2_caret_skips_the_padding_element_and_a_click_in_padding_lands_on_the_adjacent_line` | pass: Right, Left, Up, Down, End, Home move one position per press; clicks in padding above, at x = 0, and in trailing padding land on the right line |
| 3 Priming equalises extents before any scrolling; survives `Redraw`; re-primes after `Document` and `FontSize` change | `Item3_height_priming_equalises_extents_before_scrolling_and_survives_redraw_document_swap_and_font_change` | pass: extents differ before priming (94 vs 90 rows), equal 99 rows after, every shared row at the same top |
| 4 Own selection and caret over transparent editor brushes paint only text bands | `Item4_selection_and_caret_drawn_from_text_extents_paint_only_text_bands` | pass: no selection or caret pixels in three padding rows; both present in the text rows |
| 5 1:1 offset sync at top, middle and bottom | `Item5_offset_sync_is_one_to_one_at_top_middle_and_bottom` | pass: equal maximum offsets, equal first visible row, every shared row aligned at all three offsets |
| 6 Priming cost for 10k gaps | `Item6_priming_cost_for_ten_thousand_gaps` (`Category=Perf`) | measured: see below |
| `dotnet build DiffView.slnx -warnaserror` | | clean |
| `dotnet test --solution DiffView.slnx` | | 12 passed (7 from Phase 0, 5 spike); the `Perf` test is excluded by default and passes with `-p:IncludePerfTests=true` |
| New tests proven able to fail | | a temporary `Assert.Fail` at the top of item 1 failed the run before removal |
| Trimmed publish (`linux-x64`, self-contained) | | succeeds, 0 IL warnings, 48 MB; no shipped code changed in this phase |

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
| Deliberate throw → dialog, F12 live log | **pass** at the window, Debug build and trimmed publish (user, 2026-09-04): the dialog shows with a working copy button and F12 opens the live log. Found: F12 pressed inside the live-log window did not close it, because the toggle lived on the main window's key handler only; fixed upstream in the diagnostics package (see *Upstreamed*) and consumed as 1.0.1 |

## Upstreamed to ClaudeForge

| Change | Reference | State |
|---|---|---|
| Live-log window ignored F12 (toggle lived on the host's main window only); and `LayeredEditors.Avalonia.Diagnostics` named a `PackageReadmeFile` it did not ship, so `dotnet pack` failed | [JanusMael/ClaudeForge#37](https://github.com/JanusMael/ClaudeForge/pull/37) | merged as `99c2963`; consumed here as diagnostics 1.0.1 — the merged source is identical to the packed branch head `f7980f2`, so the package did not change; the pin in `reference/sources.json` moved to `93065ba`, main's tip after both merges |
| Theme audit: the report for ClaudeForge's views (seven `SystemControl*` keys still referenced and undefined under Semi, one of them — `SystemAccentColorBrush` — defined by no theme at all), the generated `FluentKeys.Semi.axaml` / `SimpleKeys.Semi.axaml` merged in its `App.axaml`, the tool as a local dotnet tool, and the `docs/UI-STYLE-GUIDE.md` §2 update | [JanusMael/ClaudeForge#38](https://github.com/JanusMael/ClaudeForge/pull/38) | merged as `93065ba` after the user's click-through; `docs/theme-audit.md` regenerated against the merged checkout — ClaudeForge's own copies of the compat dictionaries now count as consumer-defined keys, leaving `SystemAccentColorBrush` as its one undefined key under Semi |

## Measurements

| What | Value | Where |
|---|---|---|
| Test run, all three projects | ~5 s | this machine, Debug, `Perf` excluded; the Reference-trait tests inventory 452 theme files |
| Trimmed self-contained publish of the demo, linux-x64 | 48 MB | `dotnet publish -c Release -r linux-x64 --self-contained true` |
| Priming 10,000 padding gaps in one pass | 10.3–10.5 s (two runs) | `Item6_priming_cost_for_ten_thousand_gaps`, this machine, Debug; quadratic in the text view's built-line list |
| Priming 10,000 padding gaps in batches of 256 with `Redraw()` between batches | 200–240 ms (two runs) | same test, including the layout pass that republishes the extent |
| Fluent 12.1.2 inventory | 1153 keys, 85 files per variant | `docs/theme-audit.md` |
| Semi 12.1.0.1 inventory | 2225–2242 keys, 285 files per variant | `docs/theme-audit.md` |
| `FluentKeys.Semi.axaml` | 42 mapped, 1912 copied, 72 skipped (36 named sub-templates × 2 variants) | `docs/theme-audit.md` §Compat dictionaries |
| `SimpleKeys.Semi.axaml` | 38 mapped, 336 copied, 4 restored, 56 skipped | same |
| `DiffDocumentBuilder.Build`, generated similar pair, 10,000 lines (400 blocks) | 8 ms | `PerfTests.Build_on_a_similar_pair`, this machine, Debug, seed 11 |
| `DiffDocumentBuilder.Build`, generated similar pair, 200,000 lines (8,000 blocks, 204,001 rows) | 459 ms | same |
| `SimilarityGate.Measure`, 2 × 200,000 unrelated lines | 16 ms (similarity 0.000) | `PerfTests.The_similarity_gate_on_the_unrelated_pair`; the gated, unaligned build of the same pair takes 109 ms |
| `DiffSearch.Find` over the 10,000-line pair | literal 3 ms (1,995 matches); regex `\b(alpha\|beta)\b` 54 ms (3,940 matches) | `PerfTests.Search_on_the_10k_pair` |
