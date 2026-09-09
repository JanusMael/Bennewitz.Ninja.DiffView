# 00004 — Copy arrows in the panes

Plan 00003 put the copy arrows where the connectors already were: the 24 px column between the
panes, two arrows per block sharing it back to back. That column is the one place in the control
where two things point in opposite directions at the same coordinates, and it shows — at 12 px a
side they meet in the middle, and `789105d` had to give each a shaft before a pair read as two
things to click rather than one bowtie.

This plan moves each arrow into the pane whose lines it would copy, and draws it **over the line
number** of the block's anchor row, so it costs no horizontal space at all.

## Goal

A block that can be copied to the other side shows one arrow, in the gutter of the side it would
be copied *from*, pointing the way the text would travel. The arrow occupies the line-number cell
of the block's anchor row and takes no column of its own. Clicking it copies the block, exactly as
the connector arrows do today. The connector column goes back to one job.

## Non-goals

| Excluded | Note |
|---|---|
| Changing what a copy does | `CanCopyBlock`, `CopyBlock`, `CopyCurrentBlock` and the `CopyToLeftCommand` / `CopyToRightCommand` pair keep their signatures and their semantics. This plan moves the *affordance*, not the operation |
| The keyboard path | Alt+Left and Alt+Right on the current block are untouched, and remain the accessible route to a copy |
| Line-level arrows | Plan 00003 sketched arrows "per block and — at the row level — per line"; only block arrows shipped. This plan does not add the line-level ones, and moving to the number cell does not depend on them |
| An editable unified view | `InlineDiffView` is read-only, so it offers no arrow. Its number margin already draws two columns and this plan leaves it alone |
| Widening the arrow | Freed from sharing a column the glyph could grow. It does not, in this plan: the cell it now occupies is a line number's, and an arrow taller than a text row would break the column's rhythm |

## What plans 00001 and 00003 already paid for

| Choice | Where it is now |
|---|---|
| Every pane hosts its own margins, added once in the constructor | `DiffPanePresenter.cs:149` — `TextArea.LeftMargins.Add`. A margin is inside the text view's coordinate space, so it scrolls with the text without the composite feeding it an offset |
| `DiffMargin` carries the shared margin behaviour | Gutter background, the render fault boundary that disables one decorator instead of throwing every frame, the automation name, and the pointer-tracking tooltip. A new drawing job inherits all of it |
| The number margin already hit-tests a row | `DiffLineNumberMargin.OnPointerPressed` maps a y to a `DocumentLine` and puts the caret on it. An arrow hit-test is the same lookup, one step earlier |
| The number margin is wider than an arrow | `MinimumDigits` is 2 and `HorizontalPadding` is 6 a side, so the narrowest cell the margin ever measures is two digits of the pane font plus 12 px — expected to clear the 12 px glyph without growing, which Phase 1 asserts rather than assumes |
| `ChangeBlock` carries both sides' line ranges, and the model knows a line's block | `ChangeBlock.LeftLines` / `RightLines`; `PaneMetadata.BlockAt(lineNumber)`, which `ChangeMarkerMargin.TooltipFor` already uses |
| Padding keeps the rows level, so every block occupies vertical space on both sides | `Padding.Before`. A block with no lines on a side still has somewhere for that side's arrow to go |
| The copy is gated on the target being editable | `SideBySideDiffView.cs:1092`. The gate does not change; only which control reads it does |

## Architecture

### Where the arrow goes

The arrow is drawn by `DiffLineNumberMargin`, in the cell that would otherwise hold the anchor
row's number, right-aligned to the same edge the numbers use so the column's rhythm is unbroken.

```
read-only, or nothing to copy      [    12 │ ~ ] using System;
the block's anchor row             [    ➡ │ ~ ] public Greeter(string name)
the rest of the block              [    14 │ ~ ]     _name = name;
a block with no lines on this side [       │   ]              ← padding: arrow here, no number lost
```

**The arrow sits with the source and points at the target.** In the left pane it points right and
means *send this block over there*; it is offered when the **right** side is editable. This is the
inverse of plan 00003's placement, where the arrow sat at the target's edge of the shared column
and was offered when that target was editable. Beyond Compare anchors to the source, and it is the
arrangement that makes a per-pane arrow possible at all: anchored to the target, the arrow in a
pane would point at the pane's own text.

### The anchor row

The block's **first row on this side**. It is predictable, it lines up with the top of the
current-block outline the connector column and the presenter already draw, and it is one lookup
from what the margin has in hand while walking `textView.VisualLines`.

The cost is real and is accepted: that row's line number is not shown while the arrow is there.
Three things hold it down.

1. **Only in edit mode.** The arrow appears only where the other side is editable, which is the
   only time it is worth more than the number. A read-only pair — the common case — shows every
   number it shows today.
2. **One row per block**, not per line.
3. **The tooltip says the number.** `TooltipFor` on that row names the hidden line and the copy
   the arrow would perform, so the information is one hover away rather than gone.

### A block with no lines on this side

A deletion has no right-side lines; an insertion has no left-side ones. Both directions are still
meaningful — `CopyBlock` already inserts the missing lines one way and removes them the other, and
`Copying_an_insertion_puts_the_missing_lines_in_and_copying_back_takes_them_out` pins it — so the
side without lines must still offer its arrow.

There is no number to replace there, because there is no line: the block's rows on that side are
padding, which is height inside the *following* line's visual box rather than lines of its own.
The arrow is drawn in that padding, and nothing is hidden. This is the one piece of geometry the
plan cannot take from the margin's ordinary line walk, and it uses the arithmetic the tests
already state as `RowTopOfLine`: the visual top of the following line, plus `Padding.Before` rows
of line height.

### What the connector gutter keeps, and what it loses

| Keeps | Loses |
|---|---|
| The connector polygons joining left rows to right rows | `CanCopyToLeft`, `CanCopyToRight`, `LastArrows`, `ArrowAt`, `CopyRequested` |
| `BlockClicked` — a click selects the block | `DrawArrows`, `DrawArrow`, `ArrowSize`, `TipInset`, `HeadLength`, `HeadHalfHeight`, `ShaftLength`, `ShaftHalfHeight`, `InnerGap` |
| `ResizeDragged` — a drag on empty space resizes the panes | The arrow-before-polygon hit order in `OnPointerPressed` |
| The current-block outline | 8 px of width: the column narrows from 24 px to 16 px, which is what the polygons alone need |

Every member in that right-hand column is **public** on `ChangeConnectorGutter`. Nothing has been
released — the repository's only tag, `plan-00003-complete`, is a checkpoint and not a version —
so they are removed outright rather than deprecated, and the `Removed` section of the changelog
records them against the first version that ships.

The `DECISIONS.md` entry *"The arrows are hit before the polygon they sit inside"* is **retired,
not amended**: an arrow in a pane's number margin is not inside a polygon, and the ordering rule
it describes stops existing. The entry is replaced by one that records why, so a reader who
remembers the rule finds out where it went.

### Hit-testing

One margin, two jobs, and an order between them: `DiffLineNumberMargin.OnPointerPressed` tests the
arrow cell first and raises the copy, and only if there is no arrow under the pointer does it fall
through to putting the caret on the line. Same shape as the rule being retired, but inside one
control over cells that do not overlap, rather than between two controls over regions that do.

The presenter forwards the request; the composite performs it, because `CopyBlock` lives there and
a pane knows nothing about the other side.

## Phases

| Phase | Size | Content |
|---|---|---|
| **1 — The arrow in the number cell** | M | The anchor row, the padding case for a one-sided block, and the drawing, gated on the other side being editable. The number margin measures and renders it; the connector arrows are still there, so the two can be compared in one frame |
| **2 — The copy, and the column** | M | Arrow hit-testing before the caret click, the request forwarded to the composite, the tooltip that names the hidden number and the copy. Then the connector column's arrow surface is removed and the column narrows to 16 px |
| **3 — Evidence** | S | Snapshots in both variants for a read-only pair, an editable pair and a one-sided block; the mutations; the retired and added `DECISIONS.md` entries; `PROGRESS.md`; the changelog's `Removed` section |

## Testing

Per the house rule, every new test is proven able to fail before it is committed.

| Test | Asserts |
|---|---|
| The arrow appears with the source | With only the right side editable, the **left** pane carries the arrows and the right carries none; and the reverse |
| Read-only shows every number | With neither side editable, no arrow is drawn and the anchor row's number is the number it would be with no editing feature at all |
| One arrow per block, on the first row | The drawn arrows are exactly one per block, each on that block's first row on that side |
| The hidden number is the only one hidden | The rendered numbers equal the full sequence minus exactly the anchor rows |
| A one-sided block still offers its arrow | On a deletion, the right pane draws an arrow in the padding, at the block's rows, and no number is missing from that pane |
| The arrow copies, the cell below does not | A click on the arrow copies the block and leaves the caret where it was; a click on a number puts the caret there and copies nothing |
| The tooltip names both | The anchor row's tooltip carries the hidden line number and the copy it offers |
| Pixels | The arrow is painted in `DiffView.GutterArrowBrush` inside the anchor row's cell and nowhere else in the margin — the guard the snapshot comparer's 0.5 % tolerance cannot be |
| The connector column has no arrows left | `ChangeConnectorGutter` paints nothing in the arrow brush, at any block, with both sides editable |
| The unified view is unchanged | `InlineDiffView` draws its two number columns and no arrow, whatever the composite's read-only flags say |

## Risks

| Risk | Assessment |
|---|---|
| **A hidden line number is information the user wanted.** One row per block shows an arrow instead of its number | The sharpest risk, and the reason the arrow is confined to edit mode. If it still reads badly, the fallback is to show the arrow only while the pointer is over the pane, which restores every number at rest and costs discoverability. That fallback is a change to one condition |
| An arrow at the gutter's inner edge points at its own pane's text | It is Beyond Compare's arrangement and reads as direction of travel rather than destination. The snapshots in Phase 3 are how this gets judged rather than argued |
| The margin's width changes when arrows appear | It should not: two digits and the padding already exceed the glyph. Phase 1 asserts the measured width is identical with arrows on and off, so a regression here is caught rather than discovered |
| Padding arithmetic for a one-sided block is wrong by a row | The same family as `RowTopOfLine`, which the tests already exercise. A dedicated test per block kind, and the snapshot of a deletion, are the guard |
| Removing public members from `ChangeConnectorGutter` | Free today and only today. The checkpoint tag marks where that surface last existed, and the changelog records the removal |
| Two hit-tests in one margin | Simpler than the two-control rule it replaces — the cells do not overlap — but it is still an order, so a test pins it rather than leaving it to the reading order of two `if`s |

## Conventions

Conventional Commits, dense bodies, no AI attribution trailer. Every user-visible string through
`DiffViewStrings`, every log line through `DiffViewLog`, which never carries document text. The
theme audit regenerates after any change under `src/DiffView.Avalonia/Themes`. New tests are
proven able to fail before they are committed, and a snapshot is never the only guard for a glyph
smaller than the comparer's tolerance.
