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
| The unified view's pane document is the exception to the rule above, and the only one: `InlineDiffView` composes it from both sides and rewrites it when the model changes, which is why that pane is read-only and why its sides' documents stay off screen | An edit lands in a document that is half one file and half the other, or the composed text stops matching the unified table | `InlineDiffView.ComposeUnifiedText`; §7 |
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
- `InlineDiffView`'s control theme is `Themes/InlineDiffView.axaml`, compiled as
  `InlineDiffViewTheme` and merged by the control itself; the find bar, the headers and the strip
  it hosts each merge `SideBySideDiffViewTheme` in their own constructors, so the file holds one
  theme and nothing else.
- `DiffLineNumberMargin` draws one column of the document's own numbers for a side's pane and two
  columns of the *sides'* numbers for a unified one, a context line filling both; each column is
  measured against its own side's line count (§7).
- Nothing under `src/DiffView.Avalonia/Themes` references a host theme key: `theme-audit report`
  scans that directory as the "DiffView" consumer and `ReferenceAuditTests` fails on drift.
- A new colour token goes into `DiffView.Tokens.axaml`, `DiffView.Tokens.ColorBlind.axaml` and
  `contrast-pairs.json` together; `ThemeResolutionTests.Every_DiffView_token_resolves_in_both_palettes`
  asserts the two palettes define the same keys.
- **A decorator that draws a ground *under* a glyph needs a `contrast-pairs.json` pair of its
  own.** The contract scored a marker against the plain gutter and the host page and nothing
  else, so a chip drawn behind one was outside what it could see, and a chip that failed the 3.0
  floor was designed, rendered and reviewed before anything measured it. The pairs holding
  `DiffView.Marker*Brush` against `DiffView.MarkerChip*Brush` are what closes that; anything new
  drawn behind a glyph adds its own.
- The `DiffView.MarkerChip*Brush` tokens are **opaque**, and each is its marker composited over
  that variant's `DiffView.PaneBackgroundBrush` — not over the gutter it is painted on. Over the
  pane because that is the lighter of the two, which is what keeps a marker above its floor; a
  blend into the gutter darkens the ground towards the glyph and cannot reach 3.0 for
  `DiffView.MarkerModifiedBrush` at any alpha. Opaque because a colour computed over one surface
  and painted on another is not expressible as a translucent brush with an `over` key, and
  because `theme-audit` scores an opaque token directly. `DiffBrushes.MarkerChipFor` reads them.
- The **view options** — `ShowWhitespace` (which covers spaces *and* tabs), `ShowLineEndings` and
  `TabWidth` (`IndentationSize`, coerced to at least 1) — are written onto the editor's
  `TextEditorOptions` by `DiffPanePresenter.ApplyDisplayOptions`, which also runs when a host
  replaces `Options` wholesale. None of them changes a row's height, so none re-primes; anything
  added here that *would* change a row's height must (test
  `ViewOptionsTests.A_tab_width_change_moves_text_sideways_and_leaves_the_rows_and_the_extents_alone`).
- The **pane font** is `SideBySideDiffView.PaneFontSize` / `PaneFontFamily`, and `double.NaN` /
  `null` mean "leave the pane theme's own": `ApplyPaneFont` clears the local value rather than
  writing a default over it. Inheriting `FontSize` into the panes does not work — the presenter's
  `ControlTheme` setter beats an inherited value. A size change re-primes through
  `PaddingHeightPrimer.LineHeightChanged`; test
  `ViewOptionsTests.A_font_size_change_re_primes_both_panes_and_leaves_their_extents_equal`.
- The **focus accent** is a collapsed overlay in the header's template (`PART_FocusAccent`, the
  `:pane-focused` pseudo-class, `DiffView.FocusAccentBrush`), never a border thickness: an
  unfocused header must lay out exactly as it did before it existed, so focus moves no row.
  `SideBySideDiffView.UpdateCaret` and `UpdateHeader` are the two places that set it.
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
- `InlineHost` under `tests/DiffView.Avalonia.Tests/Inline` is `CompositeHost`'s unified twin —
  the same hand-advanced clock, the same zero-time builder, the same syntax rule. A test that
  reads what a margin or a background renderer *drew* captures a frame (`Capture()`); a layout
  pass alone does not redraw a margin.
- Rendered text must be machine-independent (`SmokeSnapshotTests`, `PresenterSnapshotTests`).
  Static seams: `DebugFlags.ResetForTesting`, `DiffViewStrings.ResetForTesting`.
- **The snapshot comparer tolerates anti-aliasing, not glyphs, and this is the trap that recurs.**
  `VerifySetup.ChannelTolerance` and `VerifySetup.MaxDifferingFraction` exist so a platform's
  anti-aliasing does not fail a frame; the cost is that a change smaller than that fraction passes
  every snapshot *while the frames still depict the old drawing*. A redrawn arrow, a swapped
  marker glyph and a new chip behind one have each done it in this repository — every baseline
  green, every baseline stale. **A snapshot is never the guard for anything smaller than a row.**
  Put a pixel assertion beside the capture (`PixelProbe`, `PresenterHost.Near`,
  `PresenterHost.Token`) and let the PNG be what a reviewer looks at.
- To find what a small change actually moved: set `VerifySetup.MaxDifferingFraction` to zero,
  **leaving `ChannelTolerance` at 8**, run once, regenerate exactly the baselines that fail,
  restore the constant. Promoting every `.received.png` blindly is not the same thing — it
  rewrites frames a change never touched.
- **Do not zero `ChannelTolerance` as well.** It absorbs sub-perceptual anti-aliasing, and some
  frames carry a few pixels of it that reproduce run to run: zeroing it reported two
  `SyntaxSnapshotTests` frames as moved by a change to the copy arrow, which those frames do not
  even draw. The differences were 3 and 5 of 255 in a single-pixel column; the genuinely changed
  frames differed by 121 to 143. The fraction is what hides a real change, and the fraction alone
  is what the sweep should remove.
- A reported rectangle cannot show that a drawing is where it should be, because a drawing that
  strays reports where it strayed to. Where position matters, assert it against something the
  decorator does not choose: `DiffLineNumberMargin.LastColumnRight` names the edge the numbers
  were aligned to, which is why a copy arrow shifted four pixels survived every other assertion.
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
| `DiffPanePresenter.CanCopyOut` carries the **other** side's editable flag, not its own: a pane offers to copy a block out when the side that would receive it is editable | Arrows appear on the pane that cannot be copied from, or vanish from the one that can | `SideBySideDiffView.AttachPane` and the `LeftReadOnlyProperty` / `RightReadOnlyProperty` branches of `OnPropertyChanged` — both paths, because a host that sets the flag in XAML is wired by the first and never reaches the second; test `CopyArrowMarginTests.A_side_editable_before_the_template_applies_is_wired_too` |
| A copy arrow is drawn over the line number of its block's **anchor row** — the block's first row — and a block a side has no lines in draws its arrow in that side's padding, where no number is given up | An arrow on the wrong row, or a block that offers no copy on the side that has nothing to send | `DiffLineNumberMargin.LastCopyArrows` (its `OverLine` is null for a padding arrow), `PaneMetadata.BlockAtRow`, `ChangeBlock.LinesFor`; tests `CopyArrowMarginTests.One_arrow_per_block_on_the_block_s_first_line`, `A_one_sided_block_at_the_very_end_is_reached_in_the_trailing_padding` |
| The number margin shows `StandardCursorType.Hand` over an arrow and clears its `Cursor` off one, so the pointer says which cells are clickable; the margin claims no cursor elsewhere and keeps the pane's | A clickable arrow under a text I-beam | `DiffLineNumberMargin.OnPointerMoved`, `OnPointerExited`; test `CopyArrowMarginTests.The_pointer_says_the_arrow_is_clickable` |
| The number margin hit-tests its arrow before the row it sits in; the composite performs the copy, because a pane knows nothing about the other side | A click on an arrow moves the caret instead of copying, or a pane tries to reach across | `DiffLineNumberMargin.OnPointerPressed`, `DiffPanePresenter.CopyOutRequested`, `SideBySideDiffView.OnPaneCopyOutRequested`; test `CopyArrowMarginTests.A_click_on_a_number_still_only_moves_the_caret` |
| A marker chip covers a **run** of same-kind rows, not a row, and needs no padding test: adjacent lines of one changed kind are always adjacent rows, because a side's lines inside a block are contiguous and blocks are separated by at least one unchanged row | A band claims rows the side does not own, or a block's extent stops being visible | `ChangeMarkerMargin.RenderChips`, `ChangeMarkerMargin.LastChips`; test `MarkerChipTests.The_chips_are_exactly_the_runs_the_model_describes` |
| A run continuing past the viewport extends a row beyond the edge, so its rounding falls off screen | A scrolled block looks as though it ends where the window does | `ChangeMarkerMargin.RenderChips`; test `MarkerChipSnapshotTests.A_run_scrolled_off_the_top_is_not_rounded_there` |
| The marker margin's chip stops exactly where the modified-since-load bar begins, and both are centred on a line's own **row** rather than its text band or its whole visual box | Chip and bar overlap, or a mark spans the padding rows above a line — which belong to the other side's lines, and which nobody edited | `ChangeMarkerMargin.RenderChips`, `ChangeMarkerMargin.RenderModifiedBar`; tests `MarkerChipTests.The_chip_stops_where_the_modified_since_load_bar_begins`, `EditFeedbackTests.The_bar_covers_the_line_s_own_row_and_not_the_padding_above_it` |
| Find is latest-wins by generation and debounced on `TimeProvider`; a query, an option or a new model cancels the search in flight and re-runs it | A stale result paints matches over newer text, or over rows a newer model renumbered | `SideBySideDiffView.RequestFind`, `RunFind`, `CompleteFind`, `ApplyModel` |
| `SearchMatchRenderer` is added to the text view **before** `DiffSelectionRenderer`; both draw on `KnownLayer.Selection`, in list order | The selection vanishes under the match highlight, or matches fall under the row fills | `DiffPanePresenter` constructor; test `FindTests.Match_highlights_sit_above_the_row_fill_and_below_the_selection` |
| A fresh `FindResult` leaves `CurrentFindMatchIndex` at -1; only `FindNext`, `FindPrevious` and the setter reveal a match, and revealing is the only thing that selects, focuses and scrolls | Typing in the query box pulls focus into a pane after every keystroke | `SideBySideDiffView.ApplyFindResult`, `SetCurrentFindMatch`, `RevealMatch`; test `FindTests.In_both_scope_F3_walks_the_matches_in_row_then_side_order_and_the_pane_holding_one_has_it_selected` |
| A query that cannot run is `FindResult.Error` shown inline in the bar; `State` does not change and no match is highlighted | A typo in a regular expression puts the control in `Failed` | `SideBySideDiffView.CompleteFind`, `UpdateFindBar`; test `FindTests.An_invalid_regular_expression_shows_the_error_inline_leaves_no_highlights_and_the_state_stays_ready` |
| A grammar that will not install turns the pane back to plain text, reports one fault naming the language, and is retried only when `SyntaxFileName` or `UseSyntaxHighlighting` changes — never by a rebuild, which would repeat the same failure | The control flickers between `Ready` and `Degraded` on every option change, or a broken grammar is never retried after the file changes | `DiffPanePresenter.DisableSyntax`, `UpdateSyntax`; test `SyntaxTests.A_grammar_that_will_not_install_degrades_the_control_names_it_and_leaves_the_diff_highlighting` |
| `DiffPanePresenter.SyntaxFault` outlives `ResetFaults`, and `SideBySideDiffView.ApplyResult` reads `PaneFault()` **before** applying the model, so a fault raised while a build ran lands as `Degraded` when the build does | A grammar failure during `Building` is swallowed by the `Ready` that follows | `SideBySideDiffView.ApplyResult`, `PaneFault`; the test above |
| The find query is never logged — only its length — because Ctrl+F pre-fills it from the pane's selection, so it may be document text | A secret under comparison reaches a log file through the find bar | `DiffViewLog.FindStarted`, `DiffViewLog.FindFailed`; the sentinel test above |

## 7. The unified view

`InlineDiffView` is the same model, builder, renderers, margins, find engine and state machine as
`SideBySideDiffView`, on one pane. Only what differs is listed here; everything in §1–§6 that is
not contradicted below holds unchanged.

| Invariant | Failure signature if broken | Canonical source |
|---|---|---|
| The pane's document is *composed*, not a source: `InlineDiffView.ComposeUnifiedText` rewrites `PaneDocument` from `InlineDocument.Lines` on every model change, keeping the one `TextDocument` instance and clearing its undo stack | The caret and the scroll jump to the top on every option change, or the editor's line count stops matching the unified table | `InlineDiffView.ComposeUnifiedText`, `ApplyModel`; test `InlineDiffViewTests.The_pane_holds_the_two_sides_unified_and_the_change_counts_match_the_side_by_side_view` |
| The unified pane is read-only, and there is no per-side read-only switch | An edit lands in a document that is half one file and half the other | `InlineDiffView.AttachPane` |
| `LeftDocument` and `RightDocument` are still built and never displayed: the word diff reads a row's two lines from them, and the search runs over `DocumentPaneText` snapshots of them | Word pieces vanish, or the search reads the composed document and finds a context line once where the model has it twice | `InlineDiffView.OnSourceChanged`, `ApplyModel`, `RunFind` |
| A unified pane takes its metadata from `InlineDocument`, and `DiffPanePresenter.IsUnified` is what says so — `PaneMetadata.Unified` rather than `PaneMetadata.For` | The pane renders a side's kinds over unified lines | `DiffPanePresenter.ApplyMetadata`, `PaneMetadata.Unified` |
| There is no padding in a unified reading: `PaneMetadata.PaddedLineNumbers` yields nothing and the primer does no work | The height tree is primed for lines that hold no padding, and rows stop being uniform | `PaneMetadata.PaddingFor`, `PaddedLineNumbers`; test `InlineDiffViewTests.Every_visible_row_is_filled_by_its_own_kind_and_none_of_them_is_padded` |
| Both halves of a modified row carry `DiffLineKind.Modified`, and the renderer picks the side's pieces with `PaneMetadata.SideOf(lineNumber)` — not the pane's `Side`, which is meaningless when unified | A modified pair loses its word-level highlights, or both halves are highlighted from the same side's pieces | `InlineDocument.Build`, `DiffLineBackgroundRenderer.DrawWordPieces`; test `InlineDiffViewTests.A_modified_row_shows_both_of_its_lines_with_the_word_pieces_of_the_side_each_belongs_to` |
| A block's on-screen extent is `PaneMetadata.DisplayRowsOf`: the model's rows for a side, the block's own unified lines when unified, where a modified pair takes two | The current-block border is too short and in the wrong place, and navigation scrolls to the wrong line | `PaneMetadata.DisplayRowsOf`, `InlineDiffView.SetCurrentChange`; test `InlineDiffViewTests.F7_walks_the_blocks_and_the_border_covers_the_block_own_unified_lines` |
| Find maps every match through `InlineDocument.LineOf` and drops the ones the view does not show — the right line of a context row — then re-sorts by unified line and column | A highlight lands on the wrong line, or `SearchMatchRenderer`'s binary search misses matches because they are not in line order | `InlineDiffView.ToUnified`; tests `InlineFindTests.The_matches_are_the_side_by_side_view_own_minus_the_context_lines_it_shows_twice`, `Over_changed_rows_only_the_two_views_find_exactly_the_same_matches` |
| The find scope is always `FindScope.Both`: the setter coerces it and `DiffFindBar.ShowScope` hides the group | A scope of one side hides matches that are on screen | `InlineDiffView.FindOptions`, `OnApplyTemplate`; test `InlineFindTests.The_scope_control_is_gone_and_the_scope_stays_both` |
| The line numbers are the *sides'*, in two columns, and so is the strip's caret lane; the unified document's own numbering is never shown | The gutter names lines of neither file | `DiffLineNumberMargin.RenderCore`, `InlineDiffView.UpdateCaret`; test `InlineDiffViewTests.The_gutter_numbers_each_line_on_its_own_side_and_leaves_the_other_column_empty` |
| A pane that is unified logs as `unified`, never as a side: the three pane-naming log methods take `DiffSide?` and the presenter passes `LogSide` | A log line blames the left pane for a fault in a view that has no sides | `DiffPanePresenter.LogSide`, `DiffViewLog.Pane`; test `InlineDiffViewTests.A_throwing_decorator_degrades_the_control_and_the_text_still_renders` |
| No minimap and no connector gutter — both are two-sided — and no F6: six key bindings, not seven | A template part that cannot be fed | `Themes/InlineDiffView.axaml`, the `InlineDiffView` constructor |

## 8. Contributing back

Anything that belongs in ClaudeForge goes there first, as a branch and pull request in the same
working session; the plan's *Contributing back to ClaudeForge* section and the *Upstreamed to
ClaudeForge* table in `PROGRESS.md` are the record.

### Mechanics, learned the hard way

**Never `git checkout` in the ClaudeForge checkout.** Another session may be working in it. Use a
worktree instead, which leaves that checkout's `HEAD` where it was:

```bash
git -C /home/janus/c/cl/ClaudeForge worktree add <scratch-path> -b <branch> main
```

Announce the intent to the ClaudeForge session first if one is running (`ListAgents`); if none is,
there is nothing to collide with, but the worktree rule still stands.

**An agent session cannot use SSH.** The sandbox's network is host-and-port allowlisted: probed
2026-09-08, `github.com:443` and `api.github.com:443` connect, while `github.com:22`,
`ssh.github.com:443` and `1.1.1.1:22` all time out. So an SSH remote hangs until it is killed, and
GitHub's port-443 SSH endpoint is not a way around it. This is independent of the developer's own
machine, where SSH works.

Push over HTTPS with `gh` as the credential helper, which is how ClaudeForge PR #44 landed:

```bash
git -c credential.helper='!gh auth git-credential' push https://github.com/JanusMael/ClaudeForge.git HEAD
```

`gh` itself is authenticated over HTTPS, so `gh pr create` and `gh api` work normally.

**A pull request description ends at its last line** — no AI attribution trailer, the same rule
commits follow. Say so once at the point of use when a session instruction asks for one.

**`cmd | head -n; echo $?` reports `head`'s exit code, not `cmd`'s.** This produced a confidently
wrong conclusion during the work above: an `ssh` probe that had actually failed was read as having
succeeded, because `head` exits 0 regardless. Capture the status without a pipe, or read
`${PIPESTATUS[0]}`. The same trap applies to any pipeline whose last stage always succeeds.
