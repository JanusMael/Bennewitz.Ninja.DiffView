# Review of plan 00001 (draft)

Every claim below was checked against source: AvaloniaEdit master (`12.0.0-rc1-72`, the base of
SourceGit's 12.x patch) and DiffPlex 1.9. Line references are to those trees. Each finding gives
the evidence, the consequence, and the change to make to the plan.

## Verdict

The architecture is right — AvaloniaEdit over a custom row control, the upstream package over the
fork, a source-indexed model, rendered padding, three test tiers. But the plan's central
guarantee ("scroll extents equal by construction") is false as written, the padding spike is
under-scoped by two known problems, three concrete integration conflicts are missing, and two
pieces of machinery are unnecessary. None of it changes the shape. All of it changes what Phase 2
costs and what the spike must prove.

## Findings, severity first

### F1 — "Equal by construction" is false: heights are learned by rendering  [blocker]

**Evidence.** `HeightTree` is `internal` (HeightTree.cs:31). Its only writer is
`TextView.BuildVisualLine` → `_heightTree.SetHeight(...)` (TextView.cs:1130). A line that has never
been built is `DefaultLineHeight` tall in the tree.

**Consequence.** A pane whose padding lies below the first viewport has a *shorter* scroll extent
than the other pane until the user scrolls past every padded line. 1:1 offset sync drifts, "jump to
last change" lands off target, the minimap viewport is wrong, and the extents change while
scrolling — the classic AvalonEdit inline-image jump.

**Change.** Add a `PaddingHeightPrimer`: after every rebuild, for each line whose padding changed,
call the public `TextView.GetOrConstructVisualLine(line)` (TextView.cs:751), which builds the line
and writes its height. Cost is bounded by the number of padding gaps on that side (≈ its
insert/delete block count), not the line count. Heights persist across `ClearVisualLines` and
`Redraw` (no `_heightTree` access there), but the tree is recreated when `Document` changes
(TextView.cs:159) and re-based when `DefaultLineHeight` changes on a font change (TextView.cs:1528)
— both must re-prime. Spike checklist item; headless test: extents are equal *before any
scrolling* on a fixture whose padding is entirely below the first viewport.

### F2 — Selection and caret stretch across the padding  [major]

**Evidence.** `BackgroundGeometryBuilder` (:198) and `Caret.CalculateCaretRectangle` (Caret.cs:396–397)
use `VisualYPosition.LineTop` / `LineBottom` — the full TextLine extent, spacer included.
`TextTop` / `TextBottom` exist precisely for "inline UI elements larger than the text"
(VisualYPosition.cs:32–34, 47–49).

**Consequence.** On a line with twenty rows of padding above it, the caret is twenty-one rows tall
and selecting the line paints the padding. Not acceptable for a control whose pitch is feedback.

**Change.** Our renderers use `TextTop` / `TextBottom`. For AvaloniaEdit's own layers, set
`TextArea.SelectionBrush` / `SelectionBorder` (TextArea.cs:520, 550) and `Caret.CaretBrush`
(Caret.cs:551) transparent and draw selection and caret ourselves from `TextArea.Selection.Segments`
and `Caret.Position` with text extents — one renderer each. Spike checklist item; this is the
honest reason Phase 2 is L.

### F3 — Spike mechanism misdescribed: a custom `DrawableTextRun`, not a `Control`  [major, cheap]

**Evidence.** `InlineObjectRun.Baseline` is `TextBlock.GetBaselineOffset(Element)`, else
`DesiredSize.Height` (InlineObjectRun.cs:100–108): bottom-aligned by default, so a spacer pushes
the text *down* and the padding appears *above* the line. Trailing padding (other side longer at
the end) therefore needs a top-aligned spacer on the last line. With `InlineObjectElement` that is
a live `Control` per padded line, measured and arranged, for a zero-width rectangle.

**Change.** A `VisualLineElement` subclass whose `CreateTextRun` returns our own `DrawableTextRun`
with `Size = (0, k · lineHeight)` and a `Baseline` chosen per direction. No controls, no arrange
pass. Zero-length elements are supported (`askInterestOffset`, VisualLine.cs:170–218). The spike
proves both directions and that Avalonia's line metrics honour `DrawableTextRun.Baseline`.

### F4 — Ctrl+F, F3, Escape collide with AvaloniaEdit's built-in SearchPanel  [major, cheap]

**Evidence.** `TextEditor` calls `SearchPanel.Install(this)` (TextEditor.cs:308), which binds
`ApplicationCommands.Find`, F3, Shift+F3 and Escape (SearchCommands.cs:35–45, 72–80).

**Consequence.** Two find UIs; our Escape and F3 race the editor's.

**Change.** `DiffPanePresenter` calls `SearchPanel.Uninstall()` (SearchPanel.cs:211). One line, but
it belongs in the plan, with a headless test that Ctrl+F opens *our* bar and no `SearchPanel` exists.

### F5 — Off-thread find over a `TextDocument` throws  [major]

**Evidence.** `TextDocument.VerifyAccess()` / `ownerThread` (TextDocument.cs:128–162).

**Consequence.** `IPaneText` over the live document, read from the search worker, throws.

**Change.** `IPaneText` wraps `document.CreateSnapshot()` (an immutable `ITextSource`) plus a
line-offset array captured on the UI thread; the diff builder likewise receives text captured on
the UI thread. Rule for *Responsiveness*: workers never touch a `TextDocument`; they get snapshots.

### F6 — "Cancellable build" overstates DiffPlex  [major]

**Evidence.** No `CancellationToken` anywhere in DiffPlex 1.9.

**Consequence.** Cancellation works only *between* stages (probe → line diff → alignment →
pieces). A Myers run on two large unrelated files cannot be stopped; rapid option toggles queue
full runs.

**Change.** Say so. Single "latest wins" worker with at most one pending request. A cheap O(N)
similarity gate (line-hash overlap) before the line diff: below a threshold on large inputs,
present the sides unaligned with a `TooDifferentToAlign` warning instead of running Myers. If
Phase 8 measurements demand it, vendor DiffPlex's `Differ` (MIT) with a cancellation check in the
loop. Gate in Phase 8; risk in Risks.

### F7 — Eager word-level diff is the wrong shape for a virtualized viewer  [design]

**Evidence.** `SideBySideDiffBuilder.Diff` runs the word diff for every modified line pair eagerly
(SideBySideDiffBuilder.cs:97–100). The viewer shows about sixty rows.

**Consequence.** The 200k-line build pays for word diffs nobody sees — hence the plan's
`WordLevelRowLimit`, `WordLevelSkipped`, a degraded state and Phase 8 threshold work, all of it
managing a cost we need not incur.

**Change.** Core calls `Differ.CreateDiffs(…, LineChunker)` and builds `Rows` from
`DiffResult.DiffBlocks` (`DeleteStartA/CountA`, `InsertStartB/CountB`; DiffBlock.cs) — about
fifty lines, and we own how deletes and inserts pair within a block. `PieceRange`s are computed
lazily per modified row pair on first render (microseconds each) behind an LRU cache. Delete
`WordLevelRowLimit` and `WordLevelSkipped`. Keep the guard that matters: a *line-length* cap for
minified files, skipped word-level noted in the line's tooltip.

### F8 — The "numbering-preserving normalization" is unnecessary  [simplification]

**Evidence.** `LineChunker` splits on `"\r\n"`, `"\r"`, `"\n"` in that order
(LineChunker.cs:8–17); upstream `NewLineFinder` recognises the same three. Numbering agrees with
no normalization at all.

**Change.** Delete the normalization from Core and from the fork-table rationale. Keep the
equality test (`DiffPane.Lines.Length == TextDocument.LineCount` on the mixed-EOL fixture) as
the guard. `TextInfo.LineEnding` stays for headers, warnings and save.

### F9 — Invariant 3 is stated for the wrong object  [test correctness]

**Evidence.** `DelimiterChunker` is lossless — delimiter runs are emitted as chunks
(DelimiterChunker.cs:20–60) — so DiffPlex's raw pieces concatenate to the line. But the aligned
`SubPieces` of a side-by-side model include `Imaginary` entries with empty text.

**Change.** Under F7 we own the pieces; restate: "for a modified row pair, the left pieces
concatenate to the left line and the right pieces to the right line". Document `WordChunker`'s
separator set (space, tab, `.(){},!?;` — not `= + - " ' [ ] < > :`) as a default to extend,
since `foo=bar` → `foo=baz` currently highlights the whole token.

### F10 — Stale metadata after an edit indexes out of range  [editing-readiness gap, cheap now]

Between a keystroke and the re-diff, `DiffLine[]` is indexed by line numbers that no longer match
the document. Nothing in the plan says lookups are bounds-checked.

**Change.** Add to the *baked in* table: metadata lookups are bounds-checked and version-stamped
against the document; unknown lines render as `Unchanged`, never throw. A rule now, a test in
Phase 2; it also protects the read-only control against renderer/document races.

### F11 — Encoding is promised for save but never captured  [editing-readiness inconsistency]

`TextInfo` is computed from a `string`; it can see a BOM character and line endings, not the
encoding. The readiness table promises "same encoding" on save.

**Change.** Accept a `PaneSource { Text, Encoding?, Path?, Title? }` per side with an implicit
conversion from `string`; `TextInfo.Encoding` is the loader-supplied value. Binary detection
belongs on bytes in the loader; `TextProbe` on a string only sees NULs.

### F12 — Connector gutter and `GridSplitter` fight for one column  [UI gotcha]

The gutter needs clicks (block select, later copy arrows); the splitter needs drags; same column.

**Change.** The gutter owns the column and implements both: drag on empty space resizes, click on
a polygon selects. Headless test for both gestures.

### F13 — "One stripe per row" does not survive 200k rows in 800 px  [spec]

**Change.** One stripe per pixel bucket; bucket kind is the strongest change in the bucket; click
maps pixel → bucket → first changed row in it. Rewrite the table row and the Phase 5 test.

### F14 — `Verify.ImageMagick` adds a native dependency for what SkiaSharp already does  [simplification]

Avalonia.Skia is already in the headless host. A tolerance comparer over two `SKBitmap`s is
thirty lines and registers as a Verify comparer for `.png`; DiffEngine still opens Beyond Compare
on a mismatch.

**Change.** Drop `Verify.ImageMagick`. Add: snapshots are few and coarse (one per phase on the
small fixture); pixel assertions gate, snapshots catch what assertions do not.

### F15 — Timing budgets as *done when* items will flap  [process]

**Change.** Perf numbers are measured by Stopwatch tests in a `Perf` trait excluded from the
default run and recorded in `PROGRESS.md`. Not gates.

### F16 — Row versus line is used loosely  [clarity]

`ChangedRowsOnly`, "caret row:col", `FindMatch.Line`, `DiffLine.Row`.

**Change.** Two-line glossary: **row** — an aligned index across both panes; **line** — a pane's
own line number. "Caret row:col" becomes "line:col of the focused pane".

### F17 — The biggest unknown is scheduled second  [process]

The spike is Phase 2, after Core. Core is fallback-proof (source-indexed works under the
projection fallback too), so nothing breaks — but nothing is gained by waiting either.

**Change.** The spike becomes Phase 1 with a stated time box; Core becomes Phase 2. If the spike
fails *and* the fallback changes the design materially, that is plan 00002 superseding this one,
not an edit.

### F18 — Smaller items

- Horizontal scrollbar visibility can differ per pane, so viewport heights differ by one scrollbar
  and the maximum vertical offsets differ at the bottom. Force the same visibility on both.
- Two vertical scrollbars for one scroll position; hide the left one.
- `Options` as a record binds awkwardly in XAML; expose `IgnoreWhitespace`, `IgnoreCase`,
  `Chunker` as styled properties that compose into `DiffOptions`.
- Red/green-only highlights fail colour-blind users; the `+ - ~` markers already help. Add a
  colour-blind-safe brush set as a second resource dictionary; the palette must be
  distinguishable without hue.
- Regex: prefer `RegexOptions.NonBacktracking` and fall back to backtracking plus timeout only
  when the pattern uses constructs it does not support.
- A runtime `FontFamily` / `FontSize` change alters `DefaultLineHeight`; padding heights and
  priming must re-run. Add to the rebuild triggers.
- Keyboard pane switching (Tab / Ctrl+Tab) is missing from the bindings.
- Very long lines degrade AvaloniaEdit itself (upstream carries a
  `do-not-run-generators-for-long-lines` branch); measure a 1 MB single-line fixture in Phase 8.
- `PaddingElementGenerator` runs inside `MeasureOverride`; it needs its own fault boundary
  (return no element on exception — misaligned but alive) in the boundary table.

## What survives unchanged

AvaloniaEdit over a custom row control (editing later would otherwise mean writing a text editor).
The upstream package over the fork (F1, F2, F4 and F5 are all solvable on public API). The
source-indexed Core model. The three test tiers. The feedback surface. The find design. The
editing-readiness list, with F10 and F11 added to it.

## Disposition

Fold F1–F17 into the draft; F18 where cheap. Net effect: the padding phase is honestly L and
front-loaded; Core gets simpler (F7, F8); `WordLevelRowLimit` and the normalization go away; the
plan's guarantees become true.
