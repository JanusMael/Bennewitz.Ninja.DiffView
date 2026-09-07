# Changelog

All notable changes to DiffView are recorded here. The format follows
[Keep a Changelog](https://keepachangelog.com/en/1.1.0/); versions follow
[Semantic Versioning](https://semver.org/).

## [Unreleased]

### Added

- Plan 00001 — the side-by-side diff control on Avalonia 12, AvaloniaEdit and DiffPlex — with
  its adversarial review and the ClaudeForge reuse analysis.
- Repository scaffolding: solution, central package management, the local feed, reference
  sources with on-demand fetching, CI workflow, and the `theme-audit` tool's first command
  (`inventory`).
- Phase 1, the virtual-padding spike: six headless tests under `tests/DiffView.Avalonia.Tests/Spike`
  proving zero-width `DrawableTextRun` padding, caret movement across it, height priming,
  text-band selection and caret drawing, 1:1 scroll sync, and the priming cost; the go and the
  facts the presenter is built on are in `DECISIONS.md`.
- `PixelProbe`, for pixel assertions on captured frames.
- Stopwatch tests carry `Category=Perf` and stay out of the default `dotnet test` run;
  `-p:IncludePerfTests=true` includes them.
- Phase 2, the theme-key audit: `theme-audit report` and `theme-audit compat` over a JSON
  configuration (`theme-audit.json`), inventorying Fluent, Simple and Semi as Avalonia resolves
  them — Default fallback, inheritance, `StyleInclude`, linked files, code providers, brush
  opacity — scanning consumers for undefined keys, scoring contrast pairs against each variant's
  own surfaces, and writing `docs/theme-audit.md`; `--check` is the drift test.
- The reviewed Fluent→Semi and Simple→Semi mappings and the generated
  `Themes/Compat/FluentKeys.Semi.axaml` / `SimpleKeys.Semi.axaml`, which let a control templated
  for Fluent or Simple (AvaloniaEdit's search panel) resolve every key under all six Semi variants.
- `Themes/DiffView.Tokens.axaml`, the `DiffView.*` palette for Light and Dark, its colour-blind
  sibling, the contrast contract in `contrast-pairs.json`, `SemiThemeVariants`, and the resource
  URIs on `DiffViewResources`.
- Reference-trait tests: the committed report and dictionaries equal a fresh run; every token
  resolves under all ten targets; the AvaloniaEdit gaps are exactly the known ones; a
  `TextEditor` with its search panel renders under each Semi variant with the compat dictionary.
- `scripts/pack-theme-audit.*`, packing the tool as `Bennewitz.Ninja.ThemeAudit` 1.1.0.
- Phase 3, the core model: `PaneSource` (bytes decoded by BOM, strict UTF-8, then Latin-1;
  binary detected on the bytes), `TextProbe`, `LineSplitter`, `DiffOptions`, the similarity
  gate, `DiffDocumentBuilder` producing a source-indexed `SideBySideDocument` (per-side lines,
  aligned rows, change blocks with per-side line ranges, derived padding) with diagnostics and
  warnings, `WordDiffCache` for lazily computed LRU-cached word-level pieces, and `DiffSearch`
  over `IPaneText` with scope, whole-word, regex (non-backtracking first), a match cap and a
  timeout. Tests for the seven invariants, every failure path, the cache and the search;
  `Perf` measurements for the 10k and 200k pairs and the gate.
- `fixtures/small`, the committed small pair; larger pairs are generated from a seed in the tests.
- Phase 4, the pane presenter: `DiffPanePresenter` over the pane's source document, with the
  padding mechanism lifted from the spike (`PaddingRun`, `PaddingElement`,
  `PaddingElementGenerator`, `PaddingHeightPrimer`), `PaneMetadata` (bounds-checked,
  version-stamped, 1-based), `DiffLineBackgroundRenderer` (row tints, hatched padding),
  `DiffSelectionRenderer` and `DiffCaretRenderer` over text bands, `DiffLineNumberMargin` and
  `ChangeMarkerMargin`, `DiffBrushes` with fallbacks, the fault boundary (`RenderFault`,
  `RenderFaultEventArgs`, `IsDegraded`, `Faults`), the caret-column normalisation after `Home`
  twice, `DiffViewStrings`, `DiffViewLogCategories`; the presenter's control themes in
  `Themes/DiffPanePresenter.axaml`, merged into the control's own resources; the
  `DiffView.MonospaceFontFamily` token.
- `AGENTS.md`, the fact-shaped cross-file contracts, starting with the presenter's.
- The demo shows two presenters over the bundled small fixture, or over the files named by
  `--left` and `--right`, fed from one Core build; the status bar carries the diff counts.
- Headless, pixel and snapshot tests for the presenter under all ten theme targets;
  `PresenterHost`, `ThemeSwap` and `ThemeTargets` as shared fixtures.

- Phase 5, the composite control: `SideBySideDiffView` with headers, banner, panes and status
  strip; `DiffPaneHeader` (title, detail line, binary / empty / identical badge);
  `DiffStatusStrip` (state pill, stale marker, progress after 100 ms, counts, changes, build
  time, options, caret position, transient lane with dismiss); the state machine `Empty` /
  `Building` / `Ready` / `Degraded` / `Failed` with the error, too-different and identical
  banners and their Retry and Force actions; the latest-wins build worker with cancellation;
  `ScrollSync` with equalised horizontal bars; `StatusController` and `StatusKind` lifted from
  ClaudeForge onto `TimeProvider`; `DiffViewLog` as the one log formatter under the four
  `DiffViewLogCategories`; `DiffViewStrings` behind every string of the new surface;
  `DiffPanePresenter.PaneScrollViewer`; the compiled `SideBySideDiffViewTheme` and
  `DiffViewColorBlindPalette`; the four 7:1 page pairs in `contrast-pairs.json` and the
  brightened Dark status foregrounds.
- The demo hosts the composite, opens files into either side through the file picker, toggles
  ignore-whitespace, ignore-case, horizontal sync and the colour-blind palette, and loads its
  sides on `Opened`.
- Headless tests for every Phase 5 done-when item, `StatusControllerTests` on a fake clock,
  snapshots of the headers and strip in both variants and both palettes plus the identical and
  error banners; `CompositeHost`, `FakeTimeProvider` and `CapturingLoggerFactory` as fixtures.

- Phase 6, word-level highlights: `WordDiffLookup` reads a modified row's two lines from the
  live documents and asks `WordDiffCache` on the row's first frame; `DiffLineBackgroundRenderer`
  draws a rectangle per changed piece over the row through the visual line's column mapping,
  in `DiffView.WordInsertedBrush` / `WordDeletedBrush`; the composite creates one lookup per
  build result, bound to that build's options, and `DiffPanePresenter.WordDiffLookup` takes it;
  the change-marker margin's tooltip names a line too long for word-level pieces; the
  `WordDiff.Skipped` string.
- Headless tests for the rectangle geometry and the render-only cache, the one-megabyte single
  line without pieces and its tooltip, the ignore-whitespace toggle and the word-diff modes;
  a snapshot of modified rows with only their changed words highlighted, in both variants.

- Phase 7, navigation and overview: `CurrentChangeIndex` with `NextChange` / `PreviousChange` /
  `FirstChange` / `LastChange` and `SwitchPane` commands, F7 / Shift+F7 / F6 default key
  bindings, the current-block border in both panes, "change i of n" in the strip and the
  end-of-changes notices; `DiffMinimap` (pixel buckets by strongest kind, viewport rectangle,
  current-change mark, click-to-jump, row tooltip); `ChangeConnectorGutter` (a polygon per
  visible block joining the sides' extents, the current block outlined, click selects, drag
  resizes through `SplitRatio`, block tooltip); tooltips on line numbers (the counterpart line)
  and markers (the block summary); `PaneMetadata.OtherLine` / `BlockAt`;
  `DiffPanePresenter.CurrentBlock`; `ScrollToRow`; the navigation, side, kind and tooltip strings.
- Headless tests for navigation and the keys, the minimap's mapping on a 200k-line fixture and
  its click, the connector polygons with click and drag, and the tooltips; a snapshot of the
  minimap, connectors and the current-block border in both variants.
- Phase 8, find: `DiffFindBar` (query box, Match case / Whole word / Regex / Changed rows only
  toggles, L / R / Both scope, "match i of n (L a · R b)", previous / next / close, and an
  inline line for a bad pattern or a truncated result); `SearchMatchRenderer` per pane on
  `KnownLayer.Selection`, above the row fills and below the selection, with the current match in
  its own brush; `IsFindBarOpen`, `FindQuery`, `FindOptions`, `FindResult`,
  `CurrentFindMatchIndex`, the `OpenFind` / `CloseFind` / `FindNext` / `FindPrevious` commands
  and the `FindCompleted` event on the composite; Ctrl+F (pre-filled from the selection), F3 /
  Shift+F3, Enter / Shift+Enter and Escape; searches debounced on `TimeProvider`, latest-wins,
  cancelled by every change and run off the UI thread above 2,000 rows over `DocumentPaneText`
  snapshots; match ticks in the minimap; the find count and scope in the status strip;
  `DiffView.Find` logging that carries counts and the query's length but never the query.
- Headless tests for the find keys and focus, the row-then-side walk, the scope, a bad pattern,
  the `MaxMatches` cap on a 10k-line fixture, a search completing while the UI thread holds a
  document in an update, and the highlight layering; a snapshot of the bar and the highlights in
  both variants; the sentinel log test gained the find leg Phase 5 left owing.
- The demo's View menu opens the find bar (Ctrl+F).

- Phase 9, syntax highlighting: `SyntaxHighlighting` installs `AvaloniaEdit.TextMate` on a pane,
  choosing the grammar from `DiffPanePresenter.SyntaxFileName`'s extension — the composite gives
  it each side's `PaneSource.Path`, or its `Title` — with the TextMate theme following
  `ActualThemeVariant` (Dark+ / Light+); `UseSyntaxHighlighting` on the presenter and on the
  composite, on by default; `DiffPanePresenter.SyntaxLanguageId`; an extension no grammar claims
  is plain text and not a failure, and a grammar that will not install turns itself off, leaves
  the diff highlighting untouched and puts the control in `Degraded` with the language named
  (`RenderFaultEventArgs.Subject`, the `RenderFault.OnSubject` string); `DiffView.Syntax` logging
  that carries the language and the extension, never text; the demo's View menu toggles it.
- Headless tests for the unclaimed extension, a source with no name at all, the path and title
  routes, the theme following the variant, the toggle, and a grammar that will not install;
  snapshots of a C# and a JSON pair colourised under the diff backgrounds in both variants, and
  the pixel assertion that an inserted row keeps its fill with the token colours over it;
  `fixtures/json`, `SyntaxProbe` and `CompositeHost.PumpUntilAsync` as fixtures.

### Changed

- The demo smoke snapshot shows the panes; the accessibility guard counts `TextEditor` and
  `DiffPanePresenter` as interactive controls.
- The composite's gutter is the connector gutter and a minimap column sits beside the right
  pane; every composite and demo snapshot shows both; the accessibility guard counts
  `ChangeConnectorGutter` and `DiffMinimap` as interactive.
- The composite and demo snapshots show word-level highlights on their modified rows.
- `RenderFault` on the presenter is raised after the render pass that caught the fault.
- The demo smoke snapshot shows the composite; the accessibility guard counts
  `SideBySideDiffView` as interactive.
- The snapshot comparer decodes from copies of the streams, so a mismatch can still write the
  `.received` file.
- A pane's faults reach the composite's state through `SideBySideDiffView.ApplyResult` as well as
  through `RenderFault`, so one raised while a build was running is not buried by the `Ready` that
  follows it; `DiffPanePresenter.ReportFault` now formats through `DiffViewLog.RenderFault`, which
  was written in Phase 5 and never called.
