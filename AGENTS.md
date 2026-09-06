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
| Workers never touch a `TextDocument`; AvaloniaEdit enforces owner-thread access | `InvalidOperationException` from `TextDocument.VerifyAccess` | `DiffDocumentBuilder.Build` takes `PaneSource` text; `DiffSearch.Find` reads `IPaneText` |
| `SearchPanel.Uninstall()` runs in `DiffPanePresenter.OnApplyTemplate`, after the base call, because `TextEditor.OnApplyTemplate` installs one on every template application (the panel is null in the constructor) | Ctrl+F opens AvaloniaEdit's panel over the composite's find bar; a null reference in the constructor | `DiffPanePresenter.OnApplyTemplate`; test `DiffPanePresenterTests.No_search_panel_is_installed` |
| Every decorator is a fault boundary: it catches, reports once through `DiffPanePresenter.ReportFault`, and disables itself; the text layer always renders | A throwing renderer takes down every frame | `GuardedBackgroundRenderer.Draw`, `DiffMargin.Render`, `PaddingElementGenerator`, `DiffPanePresenter.PrimeNow`, `DiffPanePresenter.OnCaretPositionChanged`; tests `A_throwing_renderer_raises_RenderFault_once_disables_itself_and_the_text_still_renders`, `A_throwing_generator_raises_RenderFault_once_and_the_lines_render_without_padding` |
| A new model re-enables every decorator | A decorator stays dark after the input that broke it is gone | `DiffPanePresenter.ResetFaults`, called from `ApplyMetadata` |
| No renderer or margin reads `TextView.VisualLines` while `TextView.VisualLinesValid` is false | `VisualLinesInvalidException` during layout | `GuardedBackgroundRenderer.Draw`, `DiffMargin.Render` |
| Line splitting agrees three ways: `LineSplitter`, DiffPlex `LineChunker`, AvaloniaEdit `NewLineFinder` | Kinds and padding land on the wrong lines for CR or mixed input | `DiffPane.Lines` count equals `TextDocument.LineCount`; test `DiffPanePresenterTests.The_model_and_the_editor_count_the_same_lines_on_mixed_line_endings` |
| Document text is never logged | A secret under comparison lands in a log file | `DiffPanePresenter.ReportFault` logs decorator, side and line number only |

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

## 7. Contributing back

Anything that belongs in ClaudeForge goes there first, as a branch and pull request in the same
working session; the plan's *Contributing back to ClaudeForge* section and the *Upstreamed to
ClaudeForge* table in `PROGRESS.md` are the record.
