# Progress

## Resume

**Phase 1 — Virtual-padding spike** of [plan 00001](plans/00001-side-by-side-diff-control.md)
is complete on `main` with a **go**: all six items pass as headless tests
(`tests/DiffView.Avalonia.Tests/Spike`), and the facts they established, the priming cost and
the one deferred wart are in `DECISIONS.md` under *Virtual padding: go*. Phase 0's manual check
at a real window is still open: Debug → *Throw on the UI thread* must show the fatal-error dialog
with a working copy button, and F12 must open the live log, in the Debug build and in the
trimmed publish. Next is **Phase 2 — theme-key audit and exhaustive dictionaries**: the audit
tool's consumer scan, findings, contrast check and compat generator, then the exhaustive
`DiffView.*` dictionaries for every target (plan §Phase 2).

## Phases

| Phase | Status | Notes |
|---|---|---|
| 0 Bootstrap | done (manual dialog check pending) | 7 tests across three tiers; trim-check clean |
| 1 Virtual-padding spike | done — go | 6 headless tests, 1 of them `Perf`; priming batched at 256 |
| 2 Theme-key audit and exhaustive dictionaries | in progress | Detection half done, 49 unit tests: colour model + WCAG contrast; consumer reference scanner; variant-aware definition scanner; ResourceInclude graph resolver; variant-map parser; `x:Class` index; context-carrying `ThemeGraphWalker`; per-variant `ThemeInventory` with alias resolution, code-behind dictionaries, nested-`ThemeDictionaries` variant attribution, and inheritance; `ThemeAuditFindings.UndefinedKeys`. Verified against real Semi: 7 variants, 0 unresolved includes, no `System*` key, per-variant palettes resolve correctly (`SemiColorPrimary` Light `#0064FA` / Dark `#54A9FF`; ~1024 colours resolve per variant). Next (generation half): low-contrast findings (fg/bg pairing), `docs/theme-audit.md` + drift test, `FluentToSemi.json` + generated compat dictionaries, `DiffView.Tokens.axaml` + colour-blind sibling, reference-trait resolution/contrast/drift + headless tests, pack the tool, ClaudeForge contribution |
| 3 Core model, probing, search engine | not started | |
| 4 Pane presenter, padding, gutters | not started | lifts the spike's mechanism; normalises the caret column after `Home` |
| 5 Composite control, scroll sync, headers, status strip, theming | not started | |
| 6 Word-level highlights and options | not started | |
| 7 Navigation, minimap, connectors, tooltips | not started | |
| 8 Find | not started | |
| 9 Syntax highlighting | not started | |
| 10 Scale, visibility, accessibility | not started | |
| 11 Inline (unified) view | not started | optional |

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
| Live-log window ignored F12 (toggle lived on the host's main window only); and `LayeredEditors.Avalonia.Diagnostics` named a `PackageReadmeFile` it did not ship, so `dotnet pack` failed | [JanusMael/ClaudeForge#37](https://github.com/JanusMael/ClaudeForge/pull/37) | PR open; consumed here as diagnostics 1.0.1, pin at branch head `f7980f2` |

## Measurements

| What | Value | Where |
|---|---|---|
| Test run, all three projects | ~1 s | this machine, Debug, `Perf` excluded |
| Trimmed self-contained publish of the demo, linux-x64 | 48 MB | `dotnet publish -c Release -r linux-x64 --self-contained true` |
| Priming 10,000 padding gaps in one pass | 10.3–10.5 s (two runs) | `Item6_priming_cost_for_ten_thousand_gaps`, this machine, Debug; quadratic in the text view's built-line list |
| Priming 10,000 padding gaps in batches of 256 with `Redraw()` between batches | 200–240 ms (two runs) | same test, including the layout pass that republishes the extent |
