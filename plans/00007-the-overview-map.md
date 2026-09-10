# 00007 — The overview map: two lanes, a draggable viewport, and a way to turn it off

`DiffMinimap` has been beside the right pane since plan 00001 Phase 7. It compresses the whole
row table to one pixel row per bucket, colours each bucket by the aligned row's kind, draws the
viewport box and the current block, ticks the find matches down its right edge, answers a tooltip,
and jumps the panes when clicked.

Three things separate it from Beyond Compare's overview map, which is the control Brian asked for
after using both side by side.

| | today | Beyond Compare |
|---|---|---|
| Lanes | **one**, coloured by the *row's* kind, so a change is visible but the side it belongs to is not | **two**, one per file, so the shape of each file's changes is visible at a glance |
| Pointer | click jumps | click jumps, **and the viewport box drags**, and the wheel scrolls over it |
| Presence | always in the template, 14 px, no way to turn it off | a view option |

## Goal

The map shows **both sides** in one column: a lane per side, a bucket inked only where that side
has a changed line in those rows. The viewport box can be dragged and the wheel scrolls over the
map. `ShowMinimap` turns the whole column off, defaulting to on, reaching the demo's View menu
like every other view option.

## Non-goals

| Excluded | Note |
|---|---|
| Renaming `DiffMinimap` | It is a public type and a template part (`PART_Minimap`), and a host that overrides the theme names it. "Minimap" is a shade less apt than "overview map" and the churn is not worth the accuracy — the same call plan 00006 made for `DiffView.GutterArrowBrush` |
| Per-side find ticks | `MatchRows` is a list of rows, not of (row, side) pairs, so splitting the ticks by lane means changing what the composite's find plumbing hands the map. The ticks stay on the outer edge, over both lanes, and the lanes stay about the diff |
| A map on the unified view | `InlineDiffView` has no minimap and §7 says why: one pane, one document, no second side to overview. Untouched |
| Rendering text or a thumbnail | Beyond Compare's map is bands of colour, not miniature text, and so is this. A text thumbnail is a different control with a different cost |
| A horizontal overview | The panes do not scroll horizontally in sync and the map is a vertical instrument |

## Architecture

### A lane per side

`BucketKinds()` becomes `BucketKinds(DiffSide)`: a bucket takes a side's kind only where that side
**has a line** in one of the rows the bucket covers — `SideBySideDocument.LineOf(row, side)` is
already the question, and `PaneMetadata` is not involved because the map reads the model directly.

The consequence is the point of the change: a deleted block inks the **left** lane and leaves the
right blank, an inserted block does the reverse, and a modified block inks both. Padding is
absence, drawn as nothing, so a one-sided block reads as a notch in one lane — which is exactly
what a reader wants from an overview and what one lane cannot say.

The column grows from 14 px to 22: two 9 px lanes with a 2 px gutter between them and the find
ticks over the outer edge, which is the narrowest that leaves each lane readable at a glance. The
width is a constant on the control, and the template's column becomes `Auto` so the toggle can
collapse it.

### The viewport drags, and the wheel scrolls

`OnPointerPressed` keeps its jump. What is new is that a press **inside the viewport box** starts
a drag instead: the control captures the pointer, `OnPointerMoved` maps each position to a row and
raises `JumpRequested`, and the release ends it. A press outside the box jumps as it does today,
which means the two gestures never compete for the same pixel — the box is where the difference
is, and a test pins it.

`OnPointerWheelChanged` raises the same event, offset by a few rows per notch, so the wheel over
the map scrolls the panes rather than the page.

Both go through `JumpRequested`, which the composite already turns into a scroll, so nothing new
crosses the boundary between the map and the panes.

### The toggle

`ShowMinimap` on `SideBySideDiffView`, defaulting `true`, following `ShowWhitespace`'s shape
exactly: a styled property, pushed to the part on change and on `AttachPane`-equivalent template
application, so a host that sets it in XAML is wired by the same path as one that sets it later —
the bug plan 00004 had to fix for `CanCopyOut` and which a test pins here from the start.

Off, the control's `IsVisible` is false and its `Auto` column takes no width, so the panes get the
22 px back rather than looking at a gap.

## Phases

| Phase | Size | Content |
|---|---|---|
| **1 — Two lanes** | M | `BucketKinds(DiffSide)`, the lane geometry and the 22 px width; the tooltip naming which side a bucket belongs to; the template column to `Auto` |
| **2 — Drag, wheel, toggle** | M | The viewport drag with pointer capture, the wheel, `ShowMinimap` on the composite and the demo's View menu |
| **3 — Evidence** | S | Snapshots in both variants and both palettes of a pair with a one-sided block, showing one lane inked and the other notched; the mutations; `DECISIONS.md`, `AGENTS.md`, `PROGRESS.md`, the changelog |

## Testing

Per the house rule, every new test is proven able to fail before it is committed. Per `AGENTS.md`
§5, a bucket is a pixel row, so **snapshots are not the guard** — pixel assertions beside the
captures are.

| Test | Asserts |
|---|---|
| A deletion inks one lane only | A left-only block inks the left lane in those buckets and leaves the right lane the gutter colour |
| An insertion is the mirror | The same, the other way round |
| A modification inks both | A modified block inks both lanes in the same buckets |
| The lanes are the model's | Every inked bucket in a lane maps back to a row that side has a changed line in — derived from the model, not from the drawing |
| The map costs 22 px, and 0 when off | Measured width with `ShowMinimap` on and off, and the panes gain what it gives up |
| A drag inside the box scrolls | A press inside the viewport, three moves and a release produce three jumps, and the panes follow the last |
| A press outside the box jumps once | The gesture that exists today, unchanged, and it does not start a drag |
| The wheel scrolls the panes | A notch over the map moves the viewport and does not scroll the page |
| A host setting the flag in XAML is wired | The template-application path, not just the property-change path |
| The tooltip names the side | A bucket in the left lane says so |

## Risks

| Risk | Assessment |
|---|---|
| **The drag and the jump share a control and could share a pixel** | They cannot: the drag starts only inside the viewport box and the jump only outside it. The rule is one `if`, so a test pins it rather than leaving it to reading order — the lesson plan 00004 wrote down |
| Two lanes in 22 px may read as noise rather than as two files | This is the one that needs eyes, not arithmetic. Phase 3's frames in both palettes are how it gets judged, and **`AGENTS.md` §9 now means it can also be looked at in a running window** — the first plan in this repository for which that is true. If it does not read, the fallback is one lane with a per-side tick on its edge |
| The colour-blind palette has to carry it too | The lanes reuse `MarkerFor(kind)`, which both palettes already define and `contrast-pairs.json` already scores against the gutter. No new token, so no new pair |
| Bucket aggregation doubles | Two passes over the row table instead of one, both cached and invalidated exactly where `_bucketKinds` is today. The 200k pair's row table is already in memory; if it proves hot, `ScalePerfTests` is where that gets measured rather than guessed |
| Baselines will not fail on their own | Certain, at bucket scale. §5's zeroed-comparer sweep applies — `MaxDifferingFraction` alone |

## Conventions

Conventional Commits, dense bodies, no AI attribution trailer. Every user-visible string through
`DiffViewStrings`, every log line through `DiffViewLog`. The theme audit regenerates after any
change under `src/DiffView.Avalonia/Themes`. New tests are proven able to fail before they are
committed, and a snapshot is never the only guard for anything smaller than a row.
