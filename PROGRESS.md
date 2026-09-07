# Progress

## Resume

**Phase 9 — Syntax highlighting** of [plan 00001](plans/00001-side-by-side-diff-control.md) is
complete on `main`; phases 0 through 9 are done, and what remains of the plan is the
scale-and-accessibility pass and the optional inline view. `SideBySideDiffView` compares two
`PaneSource`s: two `DiffPanePresenter`s over the sources' own documents, with rendered padding
holding the rows level, row and word-level highlights, line-number and change-marker gutters,
headers, a banner for what failed or was skipped, a status strip, a connector gutter between the
panes and a minimap beside them, vertical scrolling coupled 1:1, change navigation on F7 /
Shift+F7 with F6 switching panes, and a tooltip on every gutter. Above the panes sits the find
bar: the query box, the Match case / Whole word / Regex / Changed rows only toggles, the
L / R / Both scope, the match count, previous / next / close, and an inline line for a bad
pattern or a truncated result. Matches are highlighted in both panes above the row fills and
below the selection, the current one in its own brush, with ticks down the minimap and the count
and scope in the strip; Ctrl+F opens the bar with the query pre-filled from the selection, F3 and
Enter walk the matches, Escape closes it and hands focus back. Builds run latest-wins on a worker
over text captured on the UI thread, and so do searches, over `TextDocument` snapshots with the
line table copied on the UI thread; a rebuild swaps the model and never a `TextDocument`; the
control is always in one `DiffViewState`; every user-visible string goes through `DiffViewStrings`
and every log line through `DiffViewLog`, which never carries document text — not the query
either, only its length. Each pane also colours its text from a TextMate grammar chosen by the
side's file extension, with the theme following the variant, under everything the diff draws; an
extension no grammar claims is plain text and not a failure, and a grammar that will not install
turns itself off and leaves the control `Degraded` with the language named. The demo hosts the
control with file-open, option toggles, Find, syntax highlighting and the colour-blind palette.

Next is **Phase 10 — Scale, visibility, accessibility** (plan §Phase 10): the 200k-line and
1 MB-single-line fixtures measured for build, priming, first paint and scroll latency, with the
numbers recorded here and the DiffPlex vendoring decision made if the budget is missed;
`ShowWhitespace`, `ShowLineEndings`, `TabWidth`, the mixed-line-ending notice and a font-family
fallback list that re-primes on a runtime font change; copy per pane with read-only enforced
against paste and typing; focus visuals and an automation name on every decorator. Done-when
tests in plan §Phase 10. Phase 11 is the optional inline view.

Both ClaudeForge contributions (PR #37 and PR #38) are merged and the ClaudeForge pin follows the
merge (see *Upstreamed to ClaudeForge*).

The theme audit regenerates after a pin bump or a change under `src/DiffView.Avalonia/Themes`, in
this order:

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
| 4 Pane presenter, padding, gutters | done | `DiffPanePresenter` over the source document; `PaddingRun` / `PaddingElement` / `PaddingElementGenerator` / `PaddingHeightPrimer` lifted from the spike; `PaneMetadata` bounds-checked and version-stamped; `DiffLineBackgroundRenderer`, `DiffSelectionRenderer`, `DiffCaretRenderer`, `DiffLineNumberMargin`, `ChangeMarkerMargin`, `DiffBrushes`; the fault boundary with `RenderFault`; caret column normalised after `Home` twice; control themes in `Themes/DiffPanePresenter.axaml`; `AGENTS.md`; 26 headless, pixel and snapshot test cases |
| 5 Composite control, scroll sync, headers, status strip, theming | done | `SideBySideDiffView`, `DiffPaneHeader`, `DiffStatusStrip`, the state machine and banners, the latest-wins worker, `ScrollSync`, `StatusController` on `TimeProvider`, `DiffViewLog`, `DiffViewStrings` over the new surface, compiled themes; the demo on the composite; 34 headless and snapshot test cases plus 5 status-controller unit tests |
| 6 Word-level highlights and options | done | `WordDiffLookup` over the live documents, one per build bound to its options; piece rectangles in `DiffLineBackgroundRenderer` through the visual line's columns; the marker margin's long-line tooltip; 6 headless and snapshot test cases |
| 7 Navigation, minimap, connectors, tooltips | done | `CurrentChangeIndex` + commands + F7 / Shift+F7 / F6, current-block border, "change i of n"; `DiffMinimap`; `ChangeConnectorGutter` with `SplitRatio`; tooltips on line numbers, markers, connectors and the minimap; 9 headless and snapshot test cases |
| 8 Find | done | `DiffFindBar` (query, Match case / Whole word / Regex / Changed rows only, L / R / Both scope, count, prev / next / close, inline error and truncation notice); `SearchMatchRenderer` per pane over `KnownLayer.Selection`; debounced, cancellable searches over `DocumentPaneText` snapshots, off the UI thread above 2,000 rows; minimap match ticks; the strip's find lane; Ctrl+F / F3 / Shift+F3 / Enter / Shift+Enter / Escape; 9 headless and snapshot test cases plus the sentinel log test's find leg |
| 9 Syntax highlighting | done | `SyntaxHighlighting` over `AvaloniaEdit.TextMate` per pane, the grammar from the file's extension and the theme from the variant; `UseSyntaxHighlighting` on presenter and composite; an unclaimed extension is plain text, a failed install is `Degraded` with the language named and the diff untouched; trimmed publish clean with TextMateSharp on board; 12 headless, snapshot and pixel test cases |
| 10 Scale, visibility, accessibility | not started | |
| 11 Inline (unified) view | not started | optional |

## Phase 9 verification

| Done-when item | Result |
|---|---|
| The trimmed publish of the demo succeeds and colourizes a C# fixture at runtime | pass: `dotnet publish -c Release -r linux-x64 --self-contained true` with `TrimMode=link` gives **0 IL warnings** and needs no `TRIMMING.md` wiring — TextMateSharp keeps its grammars as embedded resources and parses them with its own parser, so nothing is reflected over; `TextMateSharp.dll`, `TextMateSharp.Grammars.dll`, `Onigwrap.dll` and `libonigwrap.so` are in the output, which grew from 51 MB to 57 MB. The published binary run against `src/DiffView.Core/PaneSource.cs` and `TextProbe.cs` logs `Syntax highlighting on the "Left" pane: csharp` and the same for the right, then `State "Building" → "Ready"` with no fault. The window itself could not be screenshotted from this session (XWayland refuses the grab), so the pixels are the headless snapshots' evidence; a look at the running window is left for the user |
| Headless: unknown extension does not throw and falls back to plain text with state `Ready` | pass: `SyntaxTests.An_extension_no_grammar_claims_leaves_plain_text_and_the_state_stays_ready` — the small pair under `left.txt` / `right.txt` leaves `SyntaxLanguageId` null on both panes, one foreground on the first line, `Ready`, no fault, and the row kinds still drawn; `A_source_with_no_name_at_all_stays_plain_text` covers the source with neither path nor title, and `The_grammar_follows_the_path_when_the_source_has_one` the path route (the title route is what every other test uses) |
| Headless: a grammar install that throws puts the control in `Degraded`, names the grammar, and diff highlighting is unaffected | pass: `A_grammar_that_will_not_install_degrades_the_control_names_it_and_leaves_the_diff_highlighting` — the left pane's install throws through the `SyntaxInstallerForTesting` seam: exactly one fault, `Source` `SyntaxHighlighting` and `Subject` `csharp`, the message and `StateMessage` both naming it, that pane back to one foreground, `Degraded`, and the modified and inserted row fills still drawn on both sides while the right pane stays colourised |
| Snapshot: C# and JSON fixtures colorized under the diff backgrounds in both theme variants | pass: `SyntaxSnapshotTests.A_colourised_pair_renders_under_the_diff_backgrounds` × {Csharp, Json} × {Light, Dark} — keywords, types, strings and numbers coloured by Dark+ / Light+ over the inserted, deleted and modified fills, with the word-level pieces and the change border still on top; reviewed and approved. `fixtures/json` is the new pair |
| Pixel assertion: syntax colour is present under an inserted row's background — the two layers compose | pass: `Syntax_colour_and_the_inserted_fill_compose_on_the_same_row` — on the first inserted row of the right pane, the inserted fill composited over the pane background is on screen *and* more than one of the token colours that row's runs carry is painted inside the same band |
| Headless: the toggle | pass: `Turning_the_toggle_off_returns_the_panes_to_plain_text_and_turning_it_on_colours_them_again` — `UseSyntaxHighlighting = false` removes both installations and returns the first line to one foreground; back on, the grammar returns and the state stays `Ready` |
| Headless: the theme follows the variant | pass: `The_syntax_theme_follows_the_variant` — the first token's own colour on `using System;` changes when the application variant goes Light → Dark, and the pane is still colourised by the same grammar |
| `dotnet build DiffView.slnx -warnaserror` | clean |
| `dotnet test --solution DiffView.slnx` | 285 passed (273 before the phase) |
| New headless tests proven able to fail | six mutations: dropping `SetGrammar` failed seven of the twelve (both colourisation tests, the theme and toggle tests, all four snapshots and the pixel assertion); pinning the syntax theme to Light+ failed the variant test and the two Dark snapshots — and, before the test was sharpened to read the first token's colour rather than the line's whole colour set, it passed under that mutation because the pane's own foreground moves with the palette; dropping the fault report failed the degraded test; making `Remove` a no-op failed the toggle test; resolving every file to the C# grammar failed the unclaimed-extension test and the JSON snapshots; and treating a nameless source as `x.cs` failed the no-name test |

## Phase 8 verification

| Done-when item | Result |
|---|---|
| Headless: Ctrl+F opens the bar with focus in the query box; Esc closes it and focus returns to the pane that had it | pass: `FindTests.Ctrl_F_opens_the_bar_with_focus_in_the_query_box_and_Escape_closes_it_and_returns_focus` — with the right pane focused and one line of it selected, Ctrl+F opens the bar, pre-fills "Greeter" from the selection and puts the caret in the query box; the strip reads "find · both" before the search and "find 4 matches · both" after it, the bar "4 matches (L 2 · R 2)", both panes carry their two matches and the minimap its rows; Escape closes the bar, drops every highlight and the result, and the right pane has focus again |
| Headless: in `Both` scope with hits on both sides, F3 walks the matches in row-then-side order, and the presenter holding the current match has focus and the match selected | pass: `In_both_scope_F3_walks_the_matches_in_row_then_side_order_and_the_pane_holding_one_has_it_selected` — the four matches equal an independent walk of the alignment table (left line before right line within a row), a fresh result has no current match, and each F3 lands on the next one with its pane focused, the match selected, `CurrentSearchMatch` set on that pane and null on the other, the bar reading "match i of 4 (L 2 · R 2)", the strip "find i of 4 · both", and both panes at the same offset; the walk wraps at either end |
| Headless: switching scope L → R → Both re-runs the search and the counts and highlights change accordingly | pass: `Switching_the_scope_re_runs_the_search_and_the_counts_and_highlights_follow` — through the bar's own properties, as a click drives them: Left leaves `RightCount` 0, the right pane with no matches and no drawn rectangles, and the strip "find 2 matches · left"; Right mirrors it; Both restores four; Match case with a lower-case query gives "no matches" |
| Headless: an invalid regex shows the inline error, leaves no highlights, and the control state stays `Ready` | pass: `An_invalid_regular_expression_shows_the_error_inline_leaves_no_highlights_and_the_state_stays_ready` — `Greet(er` under Regex puts "Invalid regular expression…" in the bar, clears both panes' matches and the count, leaves `State` at `Ready` and the current match at -1, logs one `Warning` under `DiffView.Find`, and the pattern itself never reaches the log; `Greet(er)?` clears the error and the matches come back |
| Headless: a query with more than `MaxMatches` hits on the 10k-line fixture shows the truncation notice and the UI stays responsive | pass: `More_hits_than_the_cap_truncate_with_a_notice_and_the_search_runs_off_the_UI_thread` — a generated 10,000-line pair (every line holding the needle, so 20,000 hits against the default 10,000 cap): the search runs off the UI thread, `Truncated` is set, exactly `MaxMatches` matches are kept, the bar and the transient lane both read "Showing the first 10,000 matches", the frame still renders with match rectangles, and the walk works over the capped matches |
| Headless: the search worker never touches the live document — a search runs to completion while the UI thread holds the document in an update | pass: `The_search_completes_while_the_UI_thread_holds_a_document_in_an_update` — the searcher is held on a gate until the UI thread has opened `TextDocument.RunUpdate()`, and the worker then reads its `DocumentPaneText` snapshot and completes with the four matches; capturing on the worker instead makes it throw from `TextDocument.VerifyAccess` |
| Snapshot: match highlights sit above the diff backgrounds and below the selection, current match distinct, in both theme variants | pass: `FindSnapshotTests.The_find_bar_and_the_match_highlights_render` Light and Dark — the bar over the panes with the scope segmented control and "match 2 of 7 (L 4 · R 3)", every `_name` highlighted (including over word-diff pieces on modified rows), the current match on the right pane in the current-match brush under its selection, ticks in the minimap, "find 2 of 7 · both" in the strip; reviewed and approved. `FindTests.Match_highlights_sit_above_the_row_fill_and_below_the_selection` asserts the composited pixels: the match brush over the pane background on an unchanged row, and the selection over the current-match brush once it is current |
| The sentinel log test's find leg, owed since Phase 5 | pass: `SideBySideDiffViewTests.The_log_never_carries_document_text_and_each_state_transition_appears_exactly_once` now searches for the sentinel — which is document text, because Ctrl+F pre-fills the query from the selection — walks to a match, and asserts the `DiffView.Find` line reports "4 match(es)" while no record anywhere holds the sentinel |
| `dotnet build DiffView.slnx -warnaserror` | clean |
| `dotnet test --solution DiffView.slnx` | 273 passed |
| New headless tests proven able to fail | the Ctrl+F and F3 tests failed for real before the focus fix — a query box that has only just become visible has not been measured and cannot take focus, and with nothing inside the composite focused the ancestor key bindings never ran; disabling `SearchMatchRenderer.DrawCore` then failed the pixel, scope, truncation and both snapshot tests; dropping the truncation notice, the bar's error text, and the UI-thread capture failed the cap, regex and update tests respectively |
| Trimmed publish (`linux-x64`, self-contained) | succeeds, 0 IL warnings, 51 MB |
| Demo launched on this machine | boots, builds the bundled pair and logs its transitions with no fault or error; the find bar itself is exercised headlessly, and the demo's View menu now carries Find (Ctrl+F) |

## Phase 7 verification

| Done-when item | Result |
|---|---|
| Headless: `NextChange` from the top lands on `Blocks[0].FirstRow`; at the last block it stops and the status strip says so | pass: `NavigationTests.NextChange_from_the_top_lands_on_the_first_block_and_stops_at_the_last_with_the_strip_saying_so` — the first block is the current one in both panes with its border drawn in the token colour over its rows, the strip reads "change 1 of 5", every subsequent block lands in view with both panes at the same offset, the sixth press stays at 5 with "No next change" in the lane; first, previous at the first, last, and the clamping setter are covered too |
| Headless: minimap pixel → bucket → row mapping is correct at top, middle, bottom on the 200k-line fixture | pass: `OverviewTests.The_minimap_maps_pixels_to_buckets_to_rows_at_top_middle_and_bottom_on_the_200k_line_fixture` — a generated 200,000-line pair (204,001 rows) in a 400 px minimap; at buckets 0, 200 and 399 the first row, the end row, the round trip through `BucketOfRow`, the strongest kind and the click's first changed row all match an independent computation; `A_minimap_click_in_the_composite_jumps_both_panes_and_the_viewport_tracks_the_scroll` covers the click and the viewport rectangle in the composite |
| Headless: connector polygons for visible blocks have the expected left and right extents; a headless click on a polygon makes that block the current change; a headless drag on empty gutter space resizes the panes | pass: `Connector_polygons_have_the_expected_extents_a_click_selects_the_block_and_a_drag_resizes_the_panes` — one polygon per block, tops at the block's first row, the left bottom after its modified plus deleted rows and the right bottom after its modified plus inserted rows, the gutter's row tops agreeing with the pane's; a click inside the tallest polygon selects its block; a drag on the unchanged first row widens the left pane and raises `SplitRatio` |
| Headless: F6 moves focus between the panes | pass: `F7_and_Shift_F7_navigate_and_F6_switches_panes` — F7 and Shift+F7 walk the changes from a focused pane, F6 alternates the focused pane both ways, and clearing `KeyBindings` disarms them |
| Tooltips on line numbers and markers | pass: `TooltipTests.Line_numbers_name_the_counterpart_and_markers_name_the_block` — "Line 2 · right line 3", "Line 17 · no right line", "Line 2 · no left line" for the inserted using, "Change 5 of 5 · +0 −5 ~1" for the block holding the deleted and modified lines, the pointer over the line-number margin bringing the text up and leaving clearing it; the connector and minimap tooltips are asserted in `OverviewTests` |
| Snapshot: minimap, connectors and the current-block border on the small fixture in both theme variants | pass: `NavigationSnapshotTests.Minimap_connectors_and_the_current_block_render` Light and Dark with change 3 current, reviewed and approved; the ten composite and demo baselines re-approved with the gutter and minimap columns |
| Navigation with no changes, and a new model clearing the current change | pass: `Navigation_without_changes_says_so_and_a_new_model_clears_the_current_change` |
| `dotnet build DiffView.slnx -warnaserror` | clean |
| `dotnet test --solution DiffView.slnx` | 264 passed |
| New headless tests proven able to fail | the minimap's round trip failed at the middle bucket until `BucketOfRow` became the exact inverse of `FirstRowOfBucket`; the marker tooltip test expected `~0` for a block that holds a modified line above its deletions and the line-number test expected a counterpart for the inserted using — both expectations were wrong and were corrected against the model |
| Trimmed publish (`linux-x64`, self-contained) | succeeds, 0 IL warnings, 51 MB |
| Demo launched on this machine | boots with the connector gutter and the minimap beside the panes, builds the bundled pair, logs its transitions; no fault or error in the log |

## Phase 6 verification

| Done-when item | Result |
|---|---|
| Headless: the renderer's word rectangles for a `Modified` row cover exactly the `PieceRange` columns, in both panes, and the cache is populated only for rows that were rendered | pass: `WordDiffTests.Word_rectangles_cover_exactly_the_piece_columns_in_both_panes_and_the_cache_holds_only_rendered_rows` — with the modified row below a 180 px viewport the cache is empty and nothing is drawn; scrolled into view, the cache holds that one row, each drawn rectangle's left and right equal the visual line's x at the piece's start and end columns over the full row height, the pieces name exactly the changed words on each side and concatenate to the line, and the pixels inside the first rectangle carry the composited word brush while those just outside carry the plain row tint |
| Headless: the 1 MB single-line fixture renders without word-level pieces and the tooltip says so | pass: `The_one_megabyte_single_line_renders_without_pieces_and_the_marker_tooltip_says_so` — a generated 1,000,000-character line against a copy with its tail changed: `Degraded` with `LongLinesSkipped`, no pieces and no rectangles on either side, the row still drawn as modified; the marker margin's tooltip under the pointer names the limit and clears when the pointer leaves. Timing in *Measurements* |
| Snapshot: a `Modified` row shows only the changed words highlighted, in both theme variants | pass: `WordDiffSnapshotTests.A_modified_row_shows_only_the_changed_words` Light and Dark — two modified rows with two and three highlighted pieces each, reviewed and approved; the composite and demo snapshots re-approved with the highlights on their modified rows |
| Headless: toggling `IgnoreWhitespace` removes whitespace-only diffs and the status strip reflects the option | pass: `Toggling_IgnoreWhitespace_removes_whitespace_only_diffs_and_the_strip_reflects_the_option` — one modified row becomes none, the identical banner appears, the strip reads "ignore whitespace", and the option reaches the cache; off again restores the change |
| `WordDiff` and `MaxWordDiffLineLength` wired through with a rebuild on change | pass: `Word_diff_off_draws_nothing_and_character_mode_reaches_the_cache`; the limit rides in the same options record and the long-line test exercises it |
| `dotnet build DiffView.slnx -warnaserror` | clean |
| `dotnet test --solution DiffView.slnx` | 255 passed |
| New headless tests proven able to fail | the geometry test's first run under the earlier nullable-flow draft did not compile until the lookup carried `NotNullWhen`; the snapshot and composite baselines mismatched on the highlights before review, as they must |
| Trimmed publish (`linux-x64`, self-contained) | succeeds, 0 IL warnings, 51 MB |
| Demo launched on this machine | boots, builds the bundled pair, shows the highlights on its modified rows; no fault or error in the log |

## Phase 5 verification

| Done-when item | Result |
|---|---|
| Headless: with a sentinel string in both sources, the captured log after build, render and a forced `RenderFault` never contains it, and each state transition appears exactly once at `Information` | pass: `SideBySideDiffViewTests.The_log_never_carries_document_text_and_each_state_transition_appears_exactly_once` — no record's message or exception text carries the sentinel or a fixture word; `Empty → Building`, `Building → Ready`, `Ready → Degraded` each once under `DiffView.Build`; the fault once at `Error` under `DiffView.Render` with its exception. The find leg waits for Phase 8 |
| Headless: the composite renders under all ten theme targets with zero binding and resource warnings | pass: `Renders_under_every_theme_target_with_no_binding_or_resource_warnings` — Ready under each target, the header painting its own token, the bridge silent in every area |
| Unit: every `DiffView.*` pair meets its floor under all ten targets — status pills ≥ 4.5:1 on their fill and ≥ 7:1 on the page | pass: four page pairs added at floor 7.0 to `contrast-pairs.json`; the Dark status foregrounds brightened one step to clear Semi Dusk and Simple Dark (see `DECISIONS.md`); `theme-audit report` shows 0 low-contrast findings over 34 pairs × 10 targets × 2 palettes, held by `ReferenceAuditTests` |
| Headless: a `Success` message clears after its delay under a test `TimeProvider`; a `Failure` sticks until dismissed; a new message cancels the pending clear | pass: `StatusControllerTests` on `FakeTimeProvider` (five cases, including active and state sticking and disposal), and `The_status_strip_shows_the_focused_panes_caret_and_a_failure_can_be_dismissed` through the strip |
| Headless: swapping `DiffViewStrings.Resolver` before load changes the rendered strings | pass: `Swapping_the_string_resolver_before_load_changes_the_rendered_strings` — state pill, header title and change count in German |
| Headless: setting the left offset moves the right offset to the same value and back, with no feedback loop, at top, middle and bottom | pass: `Scroll_sync_is_one_to_one_at_top_middle_and_bottom_with_no_feedback_loop` — both directions at three offsets, at most four scroll events per change and none once settled, every shared row at the same top |
| Headless: `LeftSource` / `RightSource` changes rebuild the document; a change during a build supersedes it and the final state reflects the last input | pass: `Sources_build_the_document_and_the_state_moves_from_Empty_through_Building_to_Ready`, `A_change_during_a_build_supersedes_it_and_the_final_state_reflects_the_last_input` — the gated first build lands late and is discarded with a `Debug` line, one `BuildCompleted` |
| Headless: changing an option property re-runs the diff and preserves caret, selection, scroll offset and the undo stack in both panes; the `TextDocument` instances are the same objects | pass: `Changing_an_option_rebuilds_and_preserves_caret_selection_scroll_and_undo_in_both_panes` |
| Headless: a throwing builder puts the control in `Failed` with the message shown and `Retry` rebuilds | pass: `A_throwing_builder_puts_the_control_in_Failed_with_the_message_and_Retry_rebuilds` — banner, strip, status lane and `BuildFailed` all carry the message; the command re-enables and disables with the state |
| Headless: binary input → `Failed` with `BinaryInput`; the unrelated pair → `Degraded` with the banner, and Force aligns it; identical input → `Ready` with the identical banner | pass: `Binary_input_fails_the_unrelated_pair_degrades_until_forced_and_identical_input_is_Ready_with_the_banner` — the binary header badge, the too-different banner over a 12,000-line unrelated pair, Force aligning it, the identical banner and badges |
| Headless: state transitions are logged when a logger is set | pass: the sentinel test above; `LoggerFactory` replaces the plan's `Logger` (see `DECISIONS.md`) |
| Headless: dragging the gutter with headless pointer input leaves both vertical offsets equal and both panes at the same first visible row | pass: `Scrolling_the_right_pane_with_headless_pointer_input_keeps_both_panes_on_the_same_first_row` — the right scrollbar's thumb when the theme exposes one, else the wheel; offsets equal and every shared row aligned |
| Snapshot: headers, status strip, error banner and identical banner in both theme variants and both palettes | pass: `CompositeSnapshotTests` — headers and strip in Light and Dark under both palettes (four frames), the identical banner and the error banner (two), all reviewed and approved; the demo smoke snapshot re-approved with the composite in it |
| Latest-wins worker, progress after 100 ms, stale marking | pass: `A_slow_build_shows_progress_after_the_threshold_and_the_previous_result_is_marked_stale` — the previous model stays on both panes marked stale; progress appears once the fake clock passes the threshold |
| Equal horizontal scrollbar visibility; `SyncHorizontalScroll`; `LeftReadOnly` / `RightReadOnly` | pass: `Horizontal_sync_is_optional_and_both_panes_show_the_same_horizontal_bar`; the read-only flags reach the panes in the options test |
| Demo on the composite with file-open and error reporting | done: `MainWindow` hosts `SideBySideDiffView`, opens files into either side through the picker with failures in the status lane and the log, toggles the options and the palette |
| `dotnet build DiffView.slnx -warnaserror` | clean |
| `dotnet test --solution DiffView.slnx` | 249 passed |
| New headless tests proven able to fail | the caret test pointed at a blank line and reported line 4 for 3; the scroll test counted three events where it expected two and now asserts quiescence; the sentinel test tripped "visual invalidated during the render pass" before the fault event was deferred; each failed for a real reason before its fixture or the code was corrected |
| Trimmed publish (`linux-x64`, self-contained) | succeeds, 0 IL warnings, 51 MB; the palette toggle and the three control themes are compiled dictionary classes |
| Demo launched on this machine | boots, logs `Empty → Building`, the build's counts and `Building → Ready`; no fault or error in the log |

## Phase 4 verification

| Done-when item | Result |
|---|---|
| Headless: the presenter renders under all ten theme targets with zero binding and resource warnings from the logger bridge | pass: `DiffPanePresenterTests.Renders_under_every_theme_target_with_no_binding_or_resource_warnings` over the ten targets; each pane paints its own `DiffView.PaneBackgroundBrush` under every one, and the bridge saw no warning in any area |
| Headless: each presenter's document text equals its source text exactly — no padding in the document | pass: `The_document_text_equals_the_source_text_and_the_line_counts_agree`, both sides, character for character |
| Headless: on the mixed-line-ending fixture, `DiffPane.Lines` count equals the presenter's `TextDocument.LineCount` | pass: `The_model_and_the_editor_count_the_same_lines_on_mixed_line_endings` — CRLF, CR and LF in one text, five lines both ways, the `MixedLineEndings` warning raised |
| Headless: after a load both presenters report equal scroll extents before any scrolling, and the renderer receives the expected `Kind` per visual line | pass: `After_a_load_both_extents_are_equal_before_scrolling_and_the_renderer_sees_each_lines_kind` — a 200 px viewport keeps one padded line below it; both extents equal the row count times the line height, every shared row sits at the same top, the background renderer and the marker margin report each visible line's own kind and padding, and a font change to 18 px re-primes with the extents still equal. `Assigning_a_new_model_reprimes_so_lines_that_lost_their_padding_return_to_one_row` covers the union rule on a model swap |
| Headless: the line-number margin shows the document's own numbers and nothing over padding space | pass: `The_line_number_margin_shows_the_documents_own_numbers_and_nothing_over_padding_space` — numbers in visual-line order at each line's text top; the margin's pixels over the padding rows are the gutter background only, the text row carries the number |
| Headless: no `SearchPanel` is installed on the presenter | pass: `No_search_panel_is_installed` — no search input handler nested in the text area, and Ctrl+F leaves the panel closed |
| Headless: the presenter honours `IsReadOnly` — typing is rejected when true and accepted when false | pass: `IsReadOnly_is_honoured_and_defaults_to_true` — the default is true; headless text input changes nothing until the flag is cleared |
| Headless: with metadata for a shorter document, every line renders as `Unchanged` and nothing throws | pass: `Metadata_for_a_different_document_renders_unknown_lines_as_unchanged_and_never_throws` — a longer document keeps the known lines' kinds and renders the rest unchanged and unpadded; a shorter one renders every line it has; no fault either way (the reading is in `DECISIONS.md`) |
| Headless: a renderer that throws raises `RenderFault` once, disables itself, and the text is still rendered; a generator that throws does the same and the line renders without padding | pass: `A_throwing_renderer_raises_RenderFault_once_disables_itself_and_the_text_still_renders`, `A_throwing_generator_raises_RenderFault_once_and_the_lines_render_without_padding` — one fault each over two frames, glyphs still drawn, every line one row tall after the generator fault; a new model re-enables the generator |
| Pixel assertions: inserted and deleted text bands and padding space carry their theme brushes; a selection across a padded line paints only text bands; the caret on a padded line is one text line tall | pass: `PresenterPixelTests.Inserted_and_deleted_rows_and_padding_space_carry_their_theme_brushes` (row bands match the composited token colours; the padding rows carry the fill and the hatch), `A_selection_across_a_padded_line_paints_only_text_bands_and_the_caret_is_one_text_line_tall` (selection in both text bands, none in the padding rows; caret in the padded line's text band only, gone when the other pane takes focus) |
| Snapshot: the small fixture in light and dark | pass: `PresenterSnapshotTests.Small_fixture_renders` under Semi Light and Dark at 900×600, reviewed and approved; the demo smoke snapshot re-approved with the two panes in it |
| The caret column after `Home` twice, owed by Phase 1 | pass: `Home_pressed_twice_on_a_padded_line_keeps_the_caret_on_the_first_text_column` — the second `Home` lands on column 1 at visual column 1, and `Right` moves to column 2 |
| Automation names on the new surface | the two margins carry names through `DiffViewStrings` (`Margins_carry_automation_names_through_the_string_resolver`); the demo's panes are named; the accessibility guard counts `DiffPanePresenter` and `TextEditor` |
| `AGENTS.md` started with the first cross-file contracts | done: the metadata stamp, the no-worker rule, `SearchPanel.Uninstall()`, the priming triggers, the fault boundary, the padding geometry, the theming rules and the test seams |
| `dotnet build DiffView.slnx -warnaserror` | clean |
| `dotnet test --solution DiffView.slnx` | 215 passed |
| New headless tests proven able to fail | the extents test failed one padding row short before `OnLoaded` became a priming trigger — the defect it was written to catch, and one the 320 px hosts of the other tests hid; the selection test failed while it sampled an empty padded line and the bands test while its sample band still held glyphs, before their fixtures were corrected |
| Trimmed publish (`linux-x64`, self-contained) | succeeds, 0 IL warnings, 50 MB; the first draft's runtime `ResourceInclude` failed it with IL2026 and became the compiled `DiffPanePresenterTheme` (see `DECISIONS.md`) |
| Demo launched on this machine | boots, builds the bundled pair (33 rows, 5 blocks, no warnings) and shows both panes; no fault or error in the log |

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
| Test run, all three projects | ~20 s for 285 tests (~8 s at Phase 5's 200) | this machine, Debug, `Perf` excluded; the Reference-trait tests inventory 452 theme files; the presenter and composite tests render under all ten theme targets; the syntax tests wait on TextMateSharp's tokenizer thread |
| Trimmed self-contained publish of the demo, linux-x64 | 57 MB after Phase 9 — TextMateSharp's grammars and themes (51 MB after Phase 5, 50 MB after Phase 4, 48 MB through Phase 3) | `dotnet publish -c Release -r linux-x64 --self-contained true` |
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
| A 1,000,000-character single line against a copy with its tail changed: build, priming and the first frame in the composite | about 2 s for the whole test on this machine, headless, Debug (the test writes the exact figure to its output) | `WordDiffTests.The_one_megabyte_single_line_renders_without_pieces_and_the_marker_tooltip_says_so`; Phase 10 measures scrolling |
