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

- Phase 10, scale, visibility and accessibility: `ShowWhitespace`, `ShowLineEndings` and
  `TabWidth` on the presenter and the composite, written onto the editor's `TextEditorOptions`
  and re-applied when a host replaces them; `PaneFontSize` and `PaneFontFamily`, which leave the
  pane theme's own when unset and re-prime when they change; the focused pane accented under its
  header on a new `DiffView.FocusAccentBrush` (in both palettes, in `contrast-pairs.json` and in
  the regenerated `docs/theme-audit.md`); the demo's View menu gaining whitespace, line endings,
  tab width and pane font size.
- Headless tests for the view options, the tab width, the pane font and family, the
  mixed-line-ending notice, per-pane copy with read-only holding against paste and typing, the
  focus accent and F6, and a runtime sweep of every decorator's automation name; a snapshot of
  the options and the accent in both variants; `ScalePerfTests` measuring the 200,000-line pair
  and the one-megabyte line from build to scroll, with the numbers in `PROGRESS.md`.

- Phase 11, the unified (inline) view: `InlineDocument`, the unified line table over a
  `SideBySideDocument` — a context row once, a change block's removals before its additions, a
  modified pair keeping its kind on both halves so the word diff survives, and a map back from a
  side's line to the unified one; `InlineDiffView`, one `DiffPanePresenter` over a document
  composed from both sides, read-only, with the renderers, margins, banner, find bar, status strip
  and state machine unchanged, a line-number column per side (a context line filling both), block
  navigation over unified lines, and no minimap, connector gutter or F6; `DiffPanePresenter.IsUnified`
  and `InlineDocument`, `PaneMetadata.Unified` / `SideOf` / `DisplayRowsOf`, and
  `DiffFindBar.ShowScope`; the demo hosts both views, switched by View → Unified (inline) view or
  the `--unified` flag.
- Headless and unit tests for the composed text, the change counts against the side-by-side
  view's, the kinds and the absence of padding, the two-column gutter, word pieces on both halves
  of a modified pair, block navigation and the current-block border, a binary side, a throwing
  decorator, an option change, the caret lane, the find scope collapsing, the matches the unified
  view drops and the ones it keeps, and a snapshot of the unified view in both variants.

- Plan 00003, in-pane editing: clearing `LeftReadOnly` / `RightReadOnly` lets a pane take typing,
  paste, undo and redo — no layer turned out to rely on the document being immutable, and the
  bounds-checked `PaneMetadata` absorbs a document that has run ahead of the model.
  `LiveReDiff` / `ReDiffDelay` / `ReDiffNow()` rebuild on a debounce from the pane's live text
  while the source keeps the encoding, path and title a save writes back with; the previous model
  stays on screen until the new one lands, the `TextDocument`, caret, selection, scroll offset and
  undo stack all survive the rebuild, and the matches an edit invalidated are dropped.
  `IsEdited(side)`, `IsDirty(side)`, `CanSave(side)`, `Save(side)`, `Revert(side)` and the
  `SaveOutcome` enum (`Saved`, `NotDirty`, `NoPath`, `ChangedOnDisk`, `Failed`); `PaneWriter` in
  Core, which round-trips the encoding, its byte-order mark and the file's line endings, and a
  `(LastWriteTimeUtc, Length)` stamp that catches someone else's write and follows our own.
  `CanCopyBlock` / `CopyBlock` / `CopyCurrentBlock` replace the target's lines over the ranges
  `ChangeBlock` already carries, through the editor's own document so one undo takes a copy back;
  `CopyToLeftCommand` / `CopyToRightCommand` on Alt+Left and Alt+Right, and the per-block copy
  arrows on a new `DiffView.GutterArrowBrush` — drawn in the connector column then, see *Changed*.
  `ModifiedLines(side)`, maintained from `TextDocument.Changed` so a mark shifts with the text an
  edit above it moved rather than with the line number, drawn as a bar down the marker margin on a
  new `DiffView.ModifiedSinceLoadBrush`; `DiffPaneHeader.IsDirty` with its `:dirty` pseudo-class
  and `PART_Dirty` marker; the strip's lane naming the sides holding unsaved edits; the
  `Header.Dirty`, `Marker.ModifiedSinceLoad`, `CopyArrow.Tooltip`, `Status.Dirty` and `Save.*`
  strings.
- The demo's File menu saves and reverts either side (Ctrl+S for the left), its View menu makes
  either pane editable, and its status line names the editable sides and the unsaved ones.
- Headless and unit tests for typing beside a read-only neighbour, typing past what the model
  knows without a fault, the rebuild's survivors, the debounce and its coalescing, live re-diff
  turned off, a replaced document that stops arming re-diffs, dirty against edited, every
  `SaveOutcome`, a copy at either end of a document and under undo, a read-only target refusing,
  the marks moving with their text and clearing on a revert, and the tooltip on a line the diff
  has nothing to say about; `PaneWriterTests` round-tripping marked and unmarked UTF-8, UTF-16 LE
  and BE, UTF-32 and a Latin-1 fallback byte for byte; `EditScalePerfTests` measuring the 200k
  pair's re-diff, the twenty keystrokes that coalesce into one build and what the escape hatch
  saves, with the numbers in `PROGRESS.md`; and `EditingSnapshotTests`, the frame in both variants
  carrying an edit's bar, the header's marker and the strip's lane at once.

- Plan 00004, copy arrows in the panes: `PaneMetadata.BlockAtRow(row)`, which answers for a padded
  row that has no line to look a block up by, and `ChangeBlock.LinesFor(side)`; the two paths that
  reach a block the side has no lines in — the padding above the line that follows it, and, past
  the last line, the trailing padding a walk over the visual lines never gets to; the anchored
  row's tooltip, carrying the number the arrow stands in for and what it would do; `CopyArrowGlyph`,
  extracted so the column and the margin drew one arrow rather than two; and
  `DiffLineNumberMargin.LastColumnRight`, exposed because an arrow's own bounds cannot show that it
  is where the numbers are — a glyph that strayed would report the place it strayed to. Where the
  arrows moved to is under *Changed*, and what the connector column gave up under *Removed*.
- Headless, pixel and snapshot tests for the side that offers an arrow and the row it sits on, the
  numbers it hides and the ones it leaves alone, a side editable before the template applies, both
  one-sided-block paths, the width the arrow costs (none, which is the premise of drawing over the
  number), the click that copies against the click that only moves the caret, the tooltip, and the
  connector column with both sides editable carrying no arrow pixels at all; `CopyArrowSnapshotTests`,
  six frames over an editable pair, the same pair read-only and a one-sided block in both variants,
  with the direction asserted by which half of the zone the glyph's head fills.

- Plan 00005, change-marker chips: `DiffBrushes.MarkerChipFor(kind)` and its `DiffBrush` entries,
  the accessor a margin reaches for the chip's colour; and the chip's shape rules — one code path
  for a run and for a lone row, symmetric about the margin's centre, stopping exactly where the
  modified-since-load bar begins so an edited line inside a changed block paints both, and no
  rounded end where the run carries on past the viewport, the rectangle running a full row beyond
  the edge instead. The chips, their tokens and the glyphs drawn on them are under *Changed*.
- Headless and snapshot tests for the chips being exactly the runs the margin reported, a lone
  row's badge against a five-row band, a run scrolled past both edges, the glyph's centring weighed
  as ink either side of the chip's centre line — which the reported rectangles cannot show — the
  chip stopping at the bar, and the margin not growing; `MarkerGlyphTests` pinning the vocabulary
  and weighing each marker's ink against a floor, which is the property the new glyphs exist for;
  and `MarkerChipSnapshotTests`, four frames over two cases in both variants.

- Plan 00006, copying a selection: `SideBySideDiffView.CanCopySelection(DiffSide)` and
  `CopySelection(DiffSide)` send a pane's selected **whole lines** to the other side, over the rows
  those lines occupy — the other side's lines in the same rows are replaced, and where it has none
  there at all the copy inserts between them, as a one-sided block's copy already did; a selection
  that ends on the next line's first column stops above it. `DiffPanePresenter.SelectedLines` and
  `CopySelectionRequested`; a selection arrow drawn in the number cell of the selection's first
  line on new `DiffView.SelectionArrowBrush` / `SelectionArrowFillBrush` /
  `SelectionArrowBarBrush`, taking that cell from a block arrow that wants it, with its own
  tooltip and the hand cursor. Alt+Left and Alt+Right go on copying the current block.
- A copy arrow is drawn as an outlined shape rather than a flat silhouette, on a new
  `DiffView.GutterArrowFillBrush`, and the selection arrow may carry a bar across its tail — drawn
  on every frame and seen only where `DiffView.SelectionArrowBarBrush` has a colour, which the
  colour-blind palette gives it and the default palette leaves transparent, so a host changes one
  key rather than forking the glyph.
- Four `contrast-pairs.json` pairs scoring each arrow's outline and fill against the gutter
  background. Arrows had never been scored at all: the block arrow had been drawn there since
  plan 00003 with nothing holding it to a floor.
- Headless, pixel and snapshot tests for the selection copy: the arrow's row, its colours, the
  contested cell, the whole-line rule at both ends, the aligned-row copy, the insertion over
  padding, the click and the tooltip; and `SelectionArrowSnapshotTests`, twelve frames over three
  cases in both variants and both palettes — the colour-blind ones being the only committed
  evidence of the tail bar.

- Plan 00007, the overview map: `DiffMinimap` draws **two lanes**, one per side, a bucket inking a
  lane only where that side has a line in the rows it covers — so a deletion inks the left lane and
  notches the right, an insertion does the reverse, and a modification inks both.
  `KindOfBucket(bucket, DiffSide)`, `LaneAt`, `ViewportBounds` and `DiffMinimap.MapWidth` are new;
  `KindOfBucket(bucket)` keeps its meaning as the stronger of the two lanes. A press inside the
  viewport box drags it and scrolls continuously where a press outside jumps, and the wheel over
  the map scrolls the panes. `SideBySideDiffView.ShowMinimap` turns the column off, defaulting on,
  and the demo's View menu carries it as Show overview map. The column is 22 px — a marker column
  the current block moved into, two lanes, the gap and the margin — and `Auto`, so hiding it gives
  the width back to the panes.
- Plan 00008, docking the overview map: `SideBySideDiffView.MinimapPlacement` puts the map
  outside either pane, over an `Auto` slot at each end of the panes grid — the empty one takes no
  width. The map's **lanes do not follow the dock**, because a lane names a file and not an edge;
  the current-block marker and the find ticks do, through one `DiffMinimap.MirrorEdges` flag that
  says which of the map's own edges faces the panes. The demo's View menu carries it as Overview
  map on the left.
- Plan 00009, key bindings a host can change: `DiffCommand` names the controls' verbs and
  `DiffKeyMap` says which key each is on — assign a gesture to rebind, `null` to unbind, and a
  command the default leaves unbound takes one the same way. `SideBySideDiffView.KeyMap` and
  `InlineDiffView.KeyMap` each hold one, `GestureFor` is the read side, and `CommandFor` hands out
  the command behind a verb. **Only the bindings a control created are replaced** when the map
  changes, so a `KeyBinding` a host added to the public collection survives; `DiffKeyBindings` is
  the one implementation of that rule, and of the warning when two commands land on one gesture,
  held by both views. The unified view defaults to `DiffKeyMap.UnifiedDefault()` — the same six
  gestures with `SwitchPane` and the four copies unbound — and a gesture given to one of those
  five is skipped and logged through `DiffViewLog.KeyCommandUnsupported` rather than left dead
  without a word.
- The demo's View menu carries **Key bindings**, a submenu whose accelerators are written from
  `GestureFor` rather than typed into the XAML, with a toggle that moves navigation to
  Ctrl+Down / Ctrl+Up on both views — so the labels move along with the keys.
- Headless tests for the pinned defaults, rebinding and the old key going quiet, unbinding with the
  command still callable, binding a command with no default, a host's own binding surviving a
  rebuild, a cleared collection staying cleared, two commands on one gesture, the unified view's
  smaller default, a two-sided verb skipped and logged there, and the copy chord against both the
  selection and the block.
- Plan 00010, the pane context menu: a right-click — or Shift+F10, or the Menu key — opens a menu
  of what the control can do to the line under the pointer, on by default in both views.
  **`DiffPaneContext` is the part that matters**: a public description of what was clicked, carrying
  the region, the side (**null** in the unified view, which has none), the line and the line's own
  number on its own file, the row, the `ChangeBlock`, and the selection. Without it nothing outside
  the library could describe a click at all, because `PaneMetadata` is internal.
  `SideBySideDiffView.PaneContextMenuOpening` and `InlineDiffView.PaneContextMenuOpening` hand over
  that context and a mutable item list to amend; `PaneContextMenu` replaces the menu outright and
  suppresses the event, and either way the context arrives as the menu's `DataContext`. Accelerators
  are read from `GestureFor`, so a rebind moves them. `DiffPanePresenter.SelectedLines` is now
  public.
- The menu's entries are copy-the-selection, copy-the-change, next and previous change, find, save
  and revert — **all present on every open**, enabled or not, so that a host's "insert after this
  item" means the same thing every time. A verb the *view* does not have is **absent** rather than
  greyed: the unified view has no copy, no save and no revert at all, because a greyed entry would
  promise a state that does not exist. The block entry copies the block **under the pointer**.
- The demo's pane menu carries an entry of its own, inserted through the opening event, which reports
  what was clicked in the status strip.
- Headless tests for the context over changed, unchanged and unmodelled lines; the caret and
  selection surviving a right-click; the keyboard resolving to the caret; the shape holding still
  across a selection change; absent-versus-disabled; the replacement menu; accelerators following the
  key map; and the icon column staying aligned with a solid square in one slot.
- `StringCatalogueTests`, adapted from ClaudeForge's `LocalizationParityTests`: every declared key
  has English text, every key reaches the host's resolver, and no two constants name one key —
  without which a key added with no default renders as its own name.
- `scripts/run-demo.sh` and `scripts/run-demo.ps1`, which run the demo on a pair with plenty of
  changes and print what is worth trying by hand.
- Headless, pixel and snapshot tests for the lanes against the model, the combined reading against
  the single-lane one, the toggle on both wiring paths, the drag against the jump, the wheel, the
  lane-naming tooltip, and `MinimapSnapshotTests` — a left-only block in both variants and both
  palettes, the drawing asserted against the buckets the map reports.
- Plan 00013, folding unchanged rows: `UnchangedContextRows` on both views collapses the runs of
  matching rows behind a placeholder, keeping a configurable number of rows around every change.
  `null` — the default — folds nothing, `0` hides every matching row, and `n` keeps `n`; those are
  Beyond Compare's *Show All*, *Show Differences* and *Show Context* as one property rather than a
  flag and a count, which together could express a state with no meaning. A click on a placeholder
  gives its run back. The commands `ShowAllRows`, `ShowDifferencesOnly`, `ShowContext` and
  `ExpandFold` arrive in the key map **unbound**, and the three modes appear on the pane's text
  menu, both margins and the connector — last on each, and absent from the overview map's, which
  plan 00012 kept short on purpose.
- The panes stay row-aligned under a fold because a fold is a **row** range projected onto each
  side's lines rather than a line range read off a document, and because a row is foldable only if
  collapsing its lines removes that row's height and nothing else — padding is height *on* a line,
  so a line at a run's edge can be carrying rows the fold does not cover. Hiding what *differs* —
  Beyond Compare's *Show Same* — is deliberately not here: a change block is lines on one side and
  padding on the other, and padding has no line to collapse.
- Walking a find match into a folded run opens that run, in both views: a match the reader is being
  taken to has to be a match they can see. Chosen over excluding folded matches from the count,
  because a count that changes when you fold describes the view rather than the file. The menu's
  *"Show the rows hidden here"* means the run under the **pointer**, as every other menu entry in
  the library does, while the unbound `ExpandFold` gesture means the run at the caret — the only
  run a keyboard can name. Navigation needed nothing: a fold covers only unchanged rows, and a
  change block has none.
- The demo takes `--edit left|right|both`, so a side starts editable without a drive through the
  View menu, and its View menu carries the three folding modes as a radio group.
- `RowProjection`, one place where a model row becomes a visible row and a pixel. The connector
  gutter and the overview map converted rows to pixels by multiplying by the line height, an
  equation only accidentally true, and the map now buckets the document that is on screen so its
  viewport box cannot point where the viewport is not.
- Plan 00012, the context menus beyond the pane: a right-click anywhere the control draws now opens
  a menu about what is under the pointer. The two gutters, the connector column, the overview map
  and the headers, through the same two extensibility shapes plan 00010 built — an opening event
  carrying the context and a mutable item list, and a property that replaces the menu outright.
  `DiffPaneRegion` gains `ConnectorGutter` and `OverviewMap`; each control builds its context from
  the hit-test it already performs for its own left-click, and **a right-click navigates nothing**:
  the connector does not jump and the map does not scroll. Off every polygon the connector opens no
  menu at all, because the empty column is the splitter and every entry that menu has is about a
  block.
- `DiffHeaderContext`, a public sibling record — `Side`, `Title`, `Detail`, `IsDirty`, `IsReadOnly`
  — with `SideBySideDiffView.HeaderContextMenuOpening` and `HeaderContextMenu` beside the pane's
  pair, and `HeaderContextAt(side)` for a host writing its own. A header's subject is a side and its
  file with no line at all, so it is a second context type rather than a sixth region over a
  synthetic line; `DiffPaneContext` is untouched. Both types go through the one menu implementation,
  so the contexts differ and the menu's behaviour cannot. The header's menu is save and revert and
  nothing else — no copy and no navigate, a header not being a position. The unified view raises no
  header menu: it has two headers but, being read-only, neither of the verbs one would offer.
- `DiffCommand.GoToChange` and `DiffCommand.SelectBlock`, both **present in the key map and unbound**
  rather than absent from it. A menu entry carries the block that was clicked, as the copy entries
  already did; a gesture — if a host binds one — means the current block, which is the only reading
  a keyboard has. Selecting from the connector selects in **both** panes, because the block spans
  both files, and a side the block has no lines in has its selection cleared rather than left
  standing. The unified view answers `null` for both, so they are absent from its menus.
- The icon column plan 00010 reserved is filled, from the vocabulary already on screen: the copy
  entries carry the gutter's own arrow — the same geometry, now shared rather than redrawn —
  pointing the way the text would travel, and the entries about a change carry the change-marker
  margin's own `+`, `−` and `≠` for that change's kind, as strokes. Both follow the inherited
  `TextElement.Foreground`, so a theme or variant swap carries them and a disabled row greys its
  icon with its label. Navigate, find, save, revert, *go to this row* and *hide the overview map*
  carry none, having no mark in that vocabulary. 00010's alignment assertion is unchanged; its
  stand-in square moved to an entry the control leaves null, so a host's own icon is still proved to
  land in the same column.
- Headless tests for each new surface: the region each gutter reports, the block a polygon names
  against the block nearest the pointer's row, the row the map reports against the pixel the pointer
  is on, the lane under the pointer and the line that row has, a right-click that navigates nothing,
  the splitter that opens nothing, the replacement property suppressing the opening event on every
  surface and on both context types, the two replacement properties governing their own surface
  only, every new entry carrying an automation name, and the icons against the gutter's own geometry
  and the theme's foreground.
- `MenuSnapshotTests`, the first snapshots in the repository to capture a popup: one frame per
  surface the plan gave a menu, and the change-marker margin's in both variants because that is the
  menu carrying both kinds of icon at once. Opening a menu moves about 8% of a frame's pixels, well
  past the comparer's half-a-percent tolerance, so unlike a chip or an arrow these frames can fail
  on their own.

- Plan 00014 — the library ships its own translations. `DiffViewLocalization` carries an optional
  host resolver and an optional culture as **one record**, because two settable statics that must
  agree are a bug waiting on someone's ordering; `DiffViewStrings.Localization` replaces the former
  `Resolver` property and `Override` returns an `IDisposable` that restores the previous value.
  `Get` reads that record once, resolves one culture, and **hands it to the resolver** — so a host
  returning `null` for a key does so knowing which language will answer instead. The chain is host
  resolver, then bundled translation for that culture, then the compiled English table, which cannot
  fail to load.
- Eight locales: `de-DE`, `es-ES`, `fr-FR`, `ja-JP`, `ko-KR`, `pt-BR`, `ru-RU`, `zh-CN`, 143 keys
  each, the same set ClaudeForge ships. **They are machine-generated and have not been read by a
  native speaker of any of the eight** — every file header says so, and `DECISIONS.md` records it as
  a decision rather than an omission. The parity gate proves keys and placeholders; it proves nothing
  about wording, and a release should not go out on them unreviewed.
- `DiffViewStrings.EnglishDefaults`, and `scripts/gen-strings.cs` (with `.sh` / `.ps1` wrappers)
  which generates the neutral `Localization/Strings.resx` from it under a drift gate, so the embedded
  resource and the compiled table cannot disagree. The locale files beside it are authored.
- `LocaleParity` and `LocaleParityTests`: missing keys, undeclared keys, placeholder-set equality per
  key, and the share of values byte-identical to English — the last borrowed from ClaudeForge's
  `LocalizationParityTests`, which catches a resx copied and never translated. Written **before any
  translation existed**, against `fixtures/locales`, a miniature catalogue and a `zz-ZZ` file wrong
  in all four ways at once. `DeclaredLocales` is asserted against what is on disk, so shipping a
  locale stays a decision rather than a side effect of adding a file.
- `SatelliteResourceLanguages` names the eight, so a consumer publishing trimmed keeps exactly them;
  the trim canary confirms all eight survive with no `IL2xxx`.
- The demo's `--culture <name>`, which drives the library's text without touching the operating
  system and names the culture in the status strip. `HeadlessTestApp` pins
  `DefaultThreadCurrentUICulture`, overridable with `DIFFVIEW_TEST_UI_CULTURE`, so an assertion of
  English text is an assertion about the library rather than about the machine.
- Plan 00015 — the culture audit. `[EnglishChrome]` marks a test as being about the English words,
  holding the library's text at `en-US` for that test's duration through `DiffViewStrings.Override`
  rather than by an assignment that a test resetting the seam would destroy. The 94 tests that
  failed under `DIFFVIEW_TEST_UI_CULTURE=de-DE` now pass — 34 assertions of English text pinned
  rather than softened, 60 frames declared to be pictures of English chrome — and a `culture-leg`
  CI job on `ubuntu-latest` keeps the whole suite green in German, so a test that fails only there
  asserted English without saying it wanted English.
- Plan 00016 — the locale review packet. `scripts/gen-locale-review.cs` writes
  `docs/locale-review/<culture>.md` for each of the eight shipped locales, pairing every string with
  what it is, what each of its placeholders holds, and the English it was translated from, so a
  native speaker can review a language without reading `.resx` files or C# doc comments. Generated
  and gated, never edited: a correction goes into the `.resx` and the document regenerates, and
  `LocaleReviewTests` fails if the two drift apart. Thirteen `DiffViewStrings` summaries were
  repaired to make it possible — five that never documented their placeholder and eight that only
  made sense beside the key above them — and two gates keep them that way.
- Plan 00018 — the release, written and dormant. `LICENSE` (MIT — there was none), `Authors`,
  `PackageLicenseExpression`, `PackageProjectUrl`, `RepositoryUrl` and `PackageTags` on every
  packable project, and `.github/workflows/release.yml`: tagging `vYYYY.Q.MDD` strips the `v` and
  carries the version into build and pack, publishes through NuGet Trusted Publishing with no
  long-lived API key, and creates the GitHub release. The push **names its two packages** rather
  than globbing `*.nupkg`, because this solution also packs `Bennewitz.Ninja.ThemeAudit`, which is
  local-feed-only by design and could not be withdrawn once published. `PackagingTests` gates all of
  it. Nothing has run: there is no remote, and the first push is a person's to make.

### Removed

- `ChangeConnectorGutter.CanCopyToLeft`, `CanCopyToRight`, `LastArrows`, `ArrowAt` and
  `CopyRequested`. The copy arrows moved into the panes' line-number margins under plan 00004, so
  the connector column draws polygons and resizes the split and nothing else. Removed outright
  rather than deprecated: nothing has shipped, and the repository's only tag is a checkpoint.

### Fixed

- **A status message could be cleared by the previous message's timer.**
  `StatusController` identified a pending auto-clear by "is one scheduled" rather than by *which*
  one, so a timer that came due and posted its clear immediately before the next message arrived
  ran against that new message — clearing it and disposing its timer. A token created with the
  timer and compared in the callback fixes it. The existing tests could not see the race: all five
  ran the clear inline on the advancing thread, closing the window it lives in. Found while scoping
  plan 00011, and the guard went upstream with the port.

- **A header was the right size in the wrong place, and had been since plan 00004.**
  `PART_Headers` reserved 24 px for the connector gutter and 14 px for the overview map while
  `PART_Panes` used 16 and 22 — two stale numbers, from two different plans, that summed to the
  same 38. The star columns therefore matched and each header was exactly as wide as its pane, so
  the outer edges lined up; the boundary between them sat 8 px out. Both grids now share one
  column layout, and a test asserts each header's **x** as well as its width.

### Changed

- **Breaking — the library's namespace is `Bennewitz.Ninja.DiffView`**, where it was
  `Bennewitz.Ninja.DiffView.Avalonia`. A namespace segment named `Avalonia` shadows the framework's
  own root: C# walks enclosing namespaces before it reaches global, so `Avalonia.Media.Color`
  resolved to `Bennewitz.Ninja.DiffView.Avalonia.Media.Color` and failed to compile with CS0234,
  and `DiffCommand`'s documentation needed `global::` to name a `KeyBinding`. The platform is
  carried by the assembly and package name, `DiffView.Avalonia`, which does **not** change;
  `Bennewitz.Ninja.DiffView.Core` does not change either and is now a natural child of the
  library's namespace. Nothing had been published, so no consumer is affected — which is why it was
  done now rather than later.
- **Every string that names a side is now a whole sentence per direction**, rather than one sentence
  with the side substituted into it — *"Copy this change to the left side"* and *"…to the right
  side"* as separate keys, not *"…to the {0} side"*. A translator cannot inflect a word dropped into
  someone else's sentence. Fourteen keys replace seven across the copy and selection arrows, the four
  line-number tooltips and the overview map's lane, with selectors that choose between them; the
  rendered English is unchanged. Kind words stay placeholders, being labels between separators rather
  than parts of a phrase. **A host overriding these keys through `DiffViewStrings.Resolver` must
  supply the new names.**
- **Alt+Left and Alt+Right copy the selection when there is one**, and the current change block
  otherwise — the rule the gutter already applied when a selection arrow took a block arrow's
  cell, and the rule cut, copy and delete follow everywhere. Before this the gutter drew one
  operation and the chord fired another whenever a selection was up. The block-always behaviour
  keeps a name, `CopyBlockToLeft` / `CopyBlockToRight`, unbound by default, so a host that wants
  it back binds a gesture rather than losing the verb. This supersedes a non-goal of plan 00006;
  `DECISIONS.md` carries the reasoning.
- The change-block arrow is goldenrod-and-yellow in the default palette — `#8A6D00` over
  `#F0E442` in Light, `#FFD54F` over `#DAA520` in Dark — in place of slate, so it reads apart from
  the selection arrow's blue at a glance, as Beyond Compare's does. The colour-blind palette keeps
  its slate arrow, where gold would collide with two Okabe–Ito hues already in use.
- **An arrow's fill is scored against its own outline at 1.5, not against the gutter at 3.0.** An
  outline carries a closed glyph's silhouette and keeps its 3.0 against the ground; a fill is
  interior to the shape, and scoring it as though it were text on a ground ruled every yellow out
  of the near-white light gutter — which is the hue that separates the two copy arrows.
  `contrast-pairs.json` re-aims the two `*ArrowFillBrush` pairs accordingly; `DECISIONS.md`
  carries the arithmetic and the numbers that were being read wrongly.

- Change markers are drawn on a chip of their kind's colour, one per run of consecutive same-kind
  rows, so a lone changed line reads as a badge and a block reads as one band. Twelve opaque
  `DiffView.MarkerChip*Brush` tokens carry the colours, each its marker composited over that
  variant's pane background, and three new `contrast-pairs.json` pairs hold every marker to its 3.0
  floor against the chip behind it — a floor nothing had scored until now. No palette colour
  changed.
- The change markers are `+`, `−` (U+2212) and `≠` (U+2260), drawn semibold, in place of `+`, `-`
  and `~`. The glyph carries a row's kind where colour cannot, and the ASCII set was too light to
  do it: a hyphen laid down 5 pixels of ink against a line number's 29. `DECISIONS.md` carries the
  measurements and why weight alone was not enough.
- The copy arrows are drawn by `DiffLineNumberMargin`, over the line number of each block's anchor
  row in the pane the block would be copied *from*, and clicking one raises
  `DiffPanePresenter.CopyOutRequested`. `CanCopyOut` on a pane carries the other side's editable
  flag. The connector column narrows from 24 px to 16.
- The three log methods that name a pane take a `DiffSide?`; the unified view's pane logs as
  `unified`, which is neither side.
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
