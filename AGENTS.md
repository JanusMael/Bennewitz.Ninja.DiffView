# AGENTS.md — cross-file contracts for DiffView

> Audience: an agent returning to DiffView cold. Purpose: surface the contracts that are not
> visible from a single-file read, so an invariant is not broken by accident. Narrative context
> lives in [`README.md`](README.md), the plan under `plans/`, [`DECISIONS.md`](DECISIONS.md) and
> [`PROGRESS.md`](PROGRESS.md); this file is the shape ClaudeForge's `AGENTS.md` set.

This file is **fact-shaped**: every claim cites a file, a type, a member or a test name, so
drift surfaces as a missing symbol under `grep`, not as stale prose. No source-line numbers,
no dates, no counts.

## 1. Hard invariants

| Invariant | Failure signature if broken | Canonical source |
|---|---|---|
| The editor's document is the source text; padding is rendered, never inserted as lines | Offsets between the model and the editor drift; a re-diff destroys caret, selection and the undo stack | `DiffPanePresenter.Document` is assigned by the host only; `PaddingElement` has `documentLength` 0; test `DiffPanePresenterTests.The_document_text_equals_the_source_text_and_the_line_counts_agree` |
| A rebuild never replaces a `TextDocument`: assigning `DiffPanePresenter.DiffDocument` swaps `PaneMetadata`, redraws and re-primes | Caret, selection, scroll offset and undo lost on every rebuild | `DiffPanePresenter.ApplyMetadata` |
| Metadata access is bounds-checked and version-stamped: an unknown line is `Unchanged` with no padding, and `PaneMetadata.Version` is the model's `SideBySideDocument.Version` | A keystroke between two builds throws from a renderer | `PaneMetadata.KindOf`, `PaneMetadata.PaddingFor`; test `DiffPanePresenterTests.Metadata_for_a_different_document_renders_unknown_lines_as_unchanged_and_never_throws` |
| `PaneMetadata` is the only place the editor's 1-based line numbers meet the model's 0-based lines | Off-by-one kinds or padding on the wrong line | `PaneMetadata` |
| Workers never touch a `TextDocument`; AvaloniaEdit enforces owner-thread access | `InvalidOperationException` from `TextDocument.VerifyAccess` | `DiffDocumentBuilder.Build` takes `PaneSource` text; `DiffSearch.Find` reads `IPaneText`, which the composite supplies as `DocumentPaneText.Capture` — an immutable snapshot plus the line table, both taken on the UI thread |
| `SearchPanel.Uninstall()` runs in `DiffPanePresenter.OnApplyTemplate`, after the base call, because `TextEditor.OnApplyTemplate` installs one on every template application (the panel is null in the constructor) | Ctrl+F opens AvaloniaEdit's panel over the composite's find bar; a null reference in the constructor | `DiffPanePresenter.OnApplyTemplate`; test `DiffPanePresenterTests.No_search_panel_is_installed` |
| Every decorator is a fault boundary: it catches, reports once through `DiffPanePresenter.ReportFault`, and disables itself; the text layer always renders | A throwing renderer takes down every frame | `GuardedBackgroundRenderer.Draw`, `DiffMargin.Render`, `PaddingElementGenerator`, `DiffPanePresenter.PrimeNow`, `DiffPanePresenter.OnCaretPositionChanged`; tests `A_throwing_renderer_raises_RenderFault_once_disables_itself_and_the_text_still_renders`, `A_throwing_generator_raises_RenderFault_once_and_the_lines_render_without_padding` |
| A new model re-enables every decorator | A decorator stays dark after the input that broke it is gone | `DiffPanePresenter.ResetFaults`, called from `ApplyMetadata` |
| No renderer or margin reads `TextView.VisualLines` while `TextView.VisualLinesValid` is false | `VisualLinesInvalidException` during layout | `GuardedBackgroundRenderer.Draw`, `DiffMargin.Render` |
| Line splitting agrees three ways: `LineSplitter`, DiffPlex `LineChunker`, AvaloniaEdit `NewLineFinder` | Kinds and padding land on the wrong lines for CR or mixed input | `DiffPane.Lines` count equals `TextDocument.LineCount`; test `DiffPanePresenterTests.The_model_and_the_editor_count_the_same_lines_on_mixed_line_endings` |
| Document text is never logged | A secret under comparison lands in a log file | `DiffPanePresenter.ReportFault` goes through `DiffViewLog.RenderFault`, which logs decorator, side, line number and — for a grammar — its language, never text |
| Syntax highlighting is a foreground: TextMate colours the tokens and nothing else, so the row fills, the word pieces, the match highlights and the selection all compose over it | Colours fight, or a grammar hides the diff | `SyntaxHighlighting` installs `AvaloniaEdit.TextMate` and sets a grammar and a theme only; test `SyntaxSnapshotTests.Syntax_colour_and_the_inserted_fill_compose_on_the_same_row` |
| A grammar is chosen from `DiffPanePresenter.SyntaxFileName`'s extension; no extension, or one no grammar claims, is plain text and **not** a fault | A `.txt` pair puts the control in `Degraded`, or an unknown file throws | `SyntaxHighlighting.GrammarFor`; tests `SyntaxTests.An_extension_no_grammar_claims_leaves_plain_text_and_the_state_stays_ready`, `SyntaxTests.A_source_with_no_name_at_all_stays_plain_text` |

## 2. Height priming

- **Triggers.** `DiffPanePresenter.RequestPrime` runs from `ApplyMetadata` (model or side changed)
  and `OnDocumentSwapped`. It primes at once when `CanPrimeNow` — the presenter `IsLoaded` and the
  text view's measure is valid, so the styled font has reached the text view — and otherwise
  leaves the prime pending for `DiffPanePresenter.OnLoaded` or the next `LayoutUpdated`,
  whichever comes first. `Loaded` is dispatched after the first layout pass and after that
  pass's `LayoutUpdated`, which is why `OnLoaded` is a trigger: without it only the padded lines
  inside the first viewport reach the height tree, and the extents are equal by luck when every
  padded line is visible (test `After_a_load_both_extents_are_equal_before_scrolling_and_the_renderer_sees_each_lines_kind`
  keeps one below the viewport). `OnLayoutUpdated` also re-primes when
  `PaddingHeightPrimer.LineHeightChanged` reports a moved default line height (a font change
  rebases plain lines only).
- **Redraw first.** `ApplyMetadata` calls `TextView.Redraw()` before priming so built lines
  carrying the old padding are dropped and the generator runs again.
- **Union rule.** `PaddingHeightPrimer.Prime` rebuilds the previous padded set together with the
  new one for the same `TextDocument`, because a line that lost its padding keeps its stale
  height until rebuilt. A document swap calls `PaddingHeightPrimer.Forget`.
- **Batches.** `PaddingHeightPrimer.BatchSize` carries the Phase 1 measurement; a `Redraw`
  separates batches, `InvalidateMeasure` closes the prime.
- **Extent equality** also relies on `TextEditorOptions.AllowScrollBelowDocument` being false,
  set in the presenter's constructor.
- Tests: `DiffPanePresenterTests.After_a_load_both_extents_are_equal_before_scrolling_and_the_renderer_sees_each_lines_kind`,
  `DiffPanePresenterTests.Assigning_a_new_model_reprimes_so_lines_that_lost_their_padding_return_to_one_row`.

## 3. Padding geometry

- `PaddingRun.Baseline` and `PaddingRun.Size` are the spike's formulas over `PaddingMetrics`;
  `PaddingElement` sits at the line start with `HandlesLineBorders` true and supplies a caret
  stop only when alone on the line.
- `DiffLineBackgroundRenderer.PaddingOf(VisualLine)` reads the padding a visual line actually
  carries from its element, never from the metadata, so a disabled generator leaves no phantom
  fills.
- **Caret column.** `DiffPanePresenter.NormaliseCaretColumn` moves visual column 0 to 1 on a
  padded line; test `PresenterPixelTests.Home_pressed_twice_on_a_padded_line_keeps_the_caret_on_the_first_text_column`.
- **Selection and caret.** The constructor makes `TextArea.SelectionBrush` transparent,
  `TextArea.SelectionBorder` null and `Caret.CaretBrush` transparent; `DiffSelectionRenderer` and
  `DiffCaretRenderer` draw from `TextBands` (`TextTop` to `TextBottom`). Test
  `PresenterPixelTests.A_selection_across_a_padded_line_paints_only_text_bands_and_the_caret_is_one_text_line_tall`.

## 4. Theming

- The control themes live in `Themes/DiffPanePresenter.axaml`, compiled as
  `DiffPanePresenterTheme`; the presenter's constructor merges that class into its own
  `Resources` and applies `DiffPanePresenter.TextAreaThemeKey` to its text area. A runtime
  `ResourceInclude` in code is IL2026 under the trim-check; the `x:Class` dictionary is not. The `DiffView.*` tokens come from the host's `DiffViewResources.ThemeUri`
  include; `DiffBrushes.Resolve` reads them on attach, `ResourcesChanged` and
  `ActualThemeVariantChanged`, with a hard fallback per token.
- Nothing under `src/DiffView.Avalonia/Themes` references a host theme key: `theme-audit report`
  scans that directory as the "DiffView" consumer and `ReferenceAuditTests` fails on drift.
- A new colour token goes into `DiffView.Tokens.axaml`, `DiffView.Tokens.ColorBlind.axaml` and
  `contrast-pairs.json` together; `ThemeResolutionTests.Every_DiffView_token_resolves_in_both_palettes`
  asserts the two palettes define the same keys.
- The **syntax** theme follows `ActualThemeVariant` and nothing else: `ThemeName.DarkPlus` under
  Dark, `ThemeName.LightPlus` otherwise (`SyntaxHighlighting.ThemeNameFor`), re-applied from
  `DiffPanePresenter.OnThemeVariantChanged`. The colour-blind palette is a `DiffView.*` matter and
  does not reach it. The `RegistryOptions` is per pane, built on the first file with an extension,
  because TextMateSharp tokenizes on its own thread and reaches back into the registry.
- `DiffViewResources.MonospaceFontFamilyKey` is defined in `DiffView.axaml`;
  `HeadlessTestApp.Initialize` overrides it with the bundled font, which is what keeps the
  rendered snapshots machine-independent.
- `IsReadOnly` defaults to true through a local value in the presenter's constructor, because
  `TextEditor.OnIsReadOnlyChanged` applies the flag to the text area only when the property
  changes.
- `WordWrap` is forced off in `DiffPanePresenter.OnPropertyChanged`.

## 5. Tests

- Avalonia tests run serially (`xunit.runner.json`); a test that renders calls
  `TestLogSink.AssertNoWarnings` afterwards; a new headless test is proven able to fail before
  it is committed; `Perf` tests are excluded by default and `Reference` tests need the checkouts
  the `EnsureReferenceSources` target fetches.
- `PresenterHost` under `tests/DiffView.Avalonia.Tests/Presenter` is the fixture for presenter
  tests; `ThemeSwap` and `ThemeTargets` cover the ten theme targets.
- `CompositeHost` keeps syntax highlighting **off** unless a test passes `syntax: true`, for the
  reason it keeps the caret from blinking: TextMateSharp tokenizes on its own thread, so a frame
  captured without waiting is a coin toss. A test that wants colour waits on
  `CompositeHost.PumpUntilAsync` with a `SyntaxProbe` condition — the built runs' foregrounds,
  which are readable the moment the line is rebuilt — never on a sleep.
- Rendered text must be machine-independent (`SmokeSnapshotTests`, `PresenterSnapshotTests`).
  Static seams: `DebugFlags.ResetForTesting`, `DiffViewStrings.ResetForTesting`.
- `AccessibilityCoverageTests` counts `DiffPanePresenter` and `TextEditor` as interactive, so
  every pane in a view carries `AutomationProperties.Name`.

## 6. The composite

| Invariant | Failure signature if broken | Canonical source |
|---|---|---|
| Assigning `LeftSource` / `RightSource` replaces that side's `TextDocument` and clears the model until the build lands; changing an option rebuilds over the same documents and keeps the model, marked stale | The old kinds paint over new text, or caret, selection, scroll and undo are lost on an option change | `SideBySideDiffView.OnSourceChanged`, `SideBySideDiffView.RequestBuild`; tests `Changing_an_option_rebuilds_and_preserves_caret_selection_scroll_and_undo_in_both_panes`, `A_slow_build_shows_progress_after_the_threshold_and_the_previous_result_is_marked_stale` |
| Latest wins: a build's outcome is applied only while its generation is the latest, on the UI thread; the worker sees captured text, never a `TextDocument` | A stale result overwrites a newer one, or `TextDocument.VerifyAccess` throws off-thread | `SideBySideDiffView.RunBuildAsync`, `SideBySideDiffView.Complete`; test `A_change_during_a_build_supersedes_it_and_the_final_state_reflects_the_last_input` |
| The control is always in one `DiffViewState`; every transition is logged once at `Information` through `DiffViewLog.StateChanged` | A transition logged twice or not at all | `SideBySideDiffView.SetStateCore`; test `The_log_never_carries_document_text_and_each_state_transition_appears_exactly_once` |
| `DiffViewLog` is the only place a log line is formatted, and no line carries document text | A secret under comparison reaches a log file | `DiffViewLog`; the sentinel test above |
| `StatusController.Set` is private; only `SetActive`, `SetSuccess`, `SetWarning`, `SetFailure`, `SetState` emit | A failure renders as quiet text | `StatusController`; `StatusControllerTests` |
| `DiffPanePresenter.RenderFault` is raised after the render pass, via the dispatcher | "Visual was invalidated during the render pass" from a listener | `DiffPanePresenter.ReportFault` |
| `ScrollSync` compares before it sets and guards re-entrancy; both panes' horizontal bars are `Visible` or `Hidden` together | A feedback loop, or panes with unequal viewport heights | `ScrollSync.Follow`, `SideBySideDiffView.UpdateHorizontalScrollBars`; test `Scroll_sync_is_one_to_one_at_top_middle_and_bottom_with_no_feedback_loop` |
| The scroll coupling waits for both panes' templates: `DiffPanePresenter.PaneScrollViewer` is null until `OnApplyTemplate` | Sync silently absent after a template re-application | `SideBySideDiffView.TryWireScrollSync` on `TemplateApplied` |
| Every user-visible string of the composite, headers and strip goes through `DiffViewStrings` and is computed per instance, never at type initialisation | Swapping `DiffViewStrings.Resolver` before load changes nothing | `SideBySideDiffView.RefreshStrings`, `UpdateHeaders`, `UpdateStrip`; test `Swapping_the_string_resolver_before_load_changes_the_rendered_strings` |
| The composite's, header's and strip's control themes are the compiled `SideBySideDiffViewTheme` merged into each control's own resources | Invisible controls without a host include, or IL2026 under the trim-check | `SideBySideDiffView`, `DiffPaneHeader`, `DiffStatusStrip` constructors |
| A rendered frame carries no machine-specific text: `CompositeHost.ZeroTimeBuilder` zeroes the build time in the test host, and the demo loads its sides on `Opened` so the smoke test can install it first | Snapshots drift by build time | `CompositeHost`, `MainWindow.OnOpened`, `SmokeSnapshotTests` |
| Word-level pieces are read through `WordDiffLookup` over the live documents, one lookup per build result bound to that build's options; the cache is filled only by rows that rendered | Stale pieces after a rebuild, or a keystroke throwing from the renderer | `WordDiffLookup.PiecesFor` (bounds-checked), `SideBySideDiffView.ApplyModel`; test `WordDiffTests.Word_rectangles_cover_exactly_the_piece_columns_in_both_panes_and_the_cache_holds_only_rendered_rows` |
| A piece rectangle's columns come from `VisualLine.GetVisualColumn`, never from the character index directly | Highlights drift by one column on padded lines and by more over tabs | `DiffLineBackgroundRenderer.DrawWordPieces` |
| A line longer than `MaxWordDiffLineLength` gets no pieces, and the marker margin's tooltip says so | A one-megabyte line stalls the UI in the word diff, or is silently unhighlighted | `WordDiffCache.GetPieces`, `ChangeMarkerMargin.TooltipFor`; test `WordDiffTests.The_one_megabyte_single_line_renders_without_pieces_and_the_marker_tooltip_says_so` |
| Row geometry is uniform once primed: a row's top is its index times the line height, and the current-block border, the centring scroll, the connector polygons and the minimap viewport all compute from that product | Borders, connectors and jumps land on the wrong rows when priming is skipped or stale | `DiffLineBackgroundRenderer.DrawCore` (border), `SideBySideDiffView.ScrollToRows`, `ChangeConnectorGutter.TopOfRow`, `DiffMinimap`; the priming contracts in §2 |
| A connector's left extent is the block's first `ModifiedCount + DeletedCount` rows and its right extent the first `ModifiedCount + InsertedCount`, per the row builder's pairing rule | Wedges point the wrong way or bands are the wrong height | `ChangeConnectorGutter.Render`; test `OverviewTests.Connector_polygons_have_the_expected_extents_a_click_selects_the_block_and_a_drag_resizes_the_panes` |
| `CurrentChangeIndex` is set only through `SideBySideDiffView.SetCurrentChange`, which clamps, scrolls, and pushes the block to the presenters, the gutter and the minimap together; a new model resets it | One surface shows a different current change than another | `SideBySideDiffView.SetCurrentChange`, `ApplyModel`; test `NavigationTests.NextChange_from_the_top_lands_on_the_first_block_and_stops_at_the_last_with_the_strip_saying_so` |
| The default key bindings are the composite's `KeyBindings`; Avalonia tries an ancestor's bindings before raising the key event, so they fire while a pane has focus, and a host may clear them | Keys stop working after a template change, or a host cannot rebind | `SideBySideDiffView` constructor; test `NavigationTests.F7_and_Shift_F7_navigate_and_F6_switches_panes` |
| `SplitRatio` is applied to both the headers grid and the panes grid, which share their star columns | Headers drift from their panes after a gutter drag | `SideBySideDiffView.ApplySplit`, template parts `PART_Headers` and `PART_Panes` |
| A margin's tooltip is resolved per line under the pointer by `DiffMargin.OnPointerMoved` and cleared on exit; each margin supplies `TooltipFor(lineNumber)` from `PaneMetadata` | A tooltip names the wrong line, or lingers | `DiffMargin`, `DiffLineNumberMargin.TooltipFor`, `ChangeMarkerMargin.TooltipFor`; `TooltipTests` |
| Find is latest-wins by generation and debounced on `TimeProvider`; a query, an option or a new model cancels the search in flight and re-runs it | A stale result paints matches over newer text, or over rows a newer model renumbered | `SideBySideDiffView.RequestFind`, `RunFind`, `CompleteFind`, `ApplyModel` |
| `SearchMatchRenderer` is added to the text view **before** `DiffSelectionRenderer`; both draw on `KnownLayer.Selection`, in list order | The selection vanishes under the match highlight, or matches fall under the row fills | `DiffPanePresenter` constructor; test `FindTests.Match_highlights_sit_above_the_row_fill_and_below_the_selection` |
| A fresh `FindResult` leaves `CurrentFindMatchIndex` at -1; only `FindNext`, `FindPrevious` and the setter reveal a match, and revealing is the only thing that selects, focuses and scrolls | Typing in the query box pulls focus into a pane after every keystroke | `SideBySideDiffView.ApplyFindResult`, `SetCurrentFindMatch`, `RevealMatch`; test `FindTests.In_both_scope_F3_walks_the_matches_in_row_then_side_order_and_the_pane_holding_one_has_it_selected` |
| A query that cannot run is `FindResult.Error` shown inline in the bar; `State` does not change and no match is highlighted | A typo in a regular expression puts the control in `Failed` | `SideBySideDiffView.CompleteFind`, `UpdateFindBar`; test `FindTests.An_invalid_regular_expression_shows_the_error_inline_leaves_no_highlights_and_the_state_stays_ready` |
| A grammar that will not install turns the pane back to plain text, reports one fault naming the language, and is retried only when `SyntaxFileName` or `UseSyntaxHighlighting` changes — never by a rebuild, which would repeat the same failure | The control flickers between `Ready` and `Degraded` on every option change, or a broken grammar is never retried after the file changes | `DiffPanePresenter.DisableSyntax`, `UpdateSyntax`; test `SyntaxTests.A_grammar_that_will_not_install_degrades_the_control_names_it_and_leaves_the_diff_highlighting` |
| `DiffPanePresenter.SyntaxFault` outlives `ResetFaults`, and `SideBySideDiffView.ApplyResult` reads `PaneFault()` **before** applying the model, so a fault raised while a build ran lands as `Degraded` when the build does | A grammar failure during `Building` is swallowed by the `Ready` that follows | `SideBySideDiffView.ApplyResult`, `PaneFault`; the test above |
| The find query is never logged — only its length — because Ctrl+F pre-fills it from the pane's selection, so it may be document text | A secret under comparison reaches a log file through the find bar | `DiffViewLog.FindStarted`, `DiffViewLog.FindFailed`; the sentinel test above |

## 7. Contributing back

Anything that belongs in ClaudeForge goes there first, as a branch and pull request in the same
working session; the plan's *Contributing back to ClaudeForge* section and the *Upstreamed to
ClaudeForge* table in `PROGRESS.md` are the record.
