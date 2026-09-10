# 00008 — Docking the overview map on either side

Plan 00007 put the map in the panes grid's last column. It is pinned there: `PART_Minimap` carries
`Grid.Column="3"` and nothing can move it but replacing the control template. Brian wants it on
either edge — both are real arrangements, and which one is right depends on the window, the
screen and the habit of the reader rather than on anything the control knows.

## Goal

`MinimapPlacement` on `SideBySideDiffView`, `Left` or `Right`, defaulting to `Right`. Docked left
the map sits outside the left pane; docked right it sits where it is today, pixel for pixel.

## Non-goals

| Excluded | Note |
|---|---|
| Flipping the lanes | Settled by Brian: **the left lane is the left file, always.** Tying a lane to the edge the map happens to be on would make it mean two things, and the whole point of two lanes is that one of them means one file |
| Top or bottom placement | The map is a vertical instrument over a vertical scroll; laid horizontally it would have to compress rows into columns and the viewport box would stop matching the panes |
| Moving the connector gutter, the find bar or the status strip | The gutter belongs between the panes by definition, and the other two span the control |
| A map per pane | One document, one row table, one overview. Two maps would show the same rows twice |
| Docking by drag | A property and a menu item. A drag target is a different feature with its own affordances |

## Architecture

### An Auto slot at each end

`PART_Panes` becomes `ColumnDefinitions="Auto,*,16,*,Auto"`: a map slot at either end, the panes
and gutter shifted one column right. The map is placed with `Grid.SetColumn` into column 0 or
column 4, and **an `Auto` column with no child in it takes no width**, so the unused slot costs
nothing.

That is also what keeps this change invisible when nothing asked for it: docked right — the
default — every pixel is where plan 00007 left it. A test asserts that rather than assuming it,
because "no snapshot moved" is exactly the kind of claim this repository has been wrong about
before.

`MinimapPlacement` is applied on **both** the template-application path and the property-change
path, for the reason §6 now records twice: a host that sets it in XAML is wired by the first and
never reaches the second.

### One rule mirrors, and it is not "the map"

Three things share the map's 22 px, and they do not answer the same question:

| | docked right | docked left | why |
|---|---|---|---|
| The two lanes | left lane at the left | **unchanged** | a lane names a file, not an edge |
| The current-block marker | the edge **nearest the panes** | mirrors | it is a pointer into the panes, so it belongs against them |
| The find ticks | the edge **away from the panes** | mirrors | they are a second reading laid beside the first, and they stay out of the lanes' way |

So the rule is one sentence — **the marker hugs the panes and the ticks hug the outside** — and
the mirroring falls out of it rather than being two special cases. `DiffMinimap` gains a
`MirrorEdges` property that the composite drives from the placement; the control never learns
which side of the window it is on, only which of its own edges faces the panes.

`LaneAt`, `LaneLeft` and the tick column all read from one geometry helper so the rule lives in a
single place. `ViewportBounds` spans the full width and does not care.

## Phases

| Phase | Size | Content |
|---|---|---|
| **1 — Placement** | M | The template's two `Auto` slots; `MinimapPlacement` on the composite wired on both paths; `MirrorEdges` on the map and the geometry helper the marker, the ticks and `LaneAt` all read; the demo's View menu |
| **2 — Evidence** | S | A snapshot docked left in both variants, the lanes proven not to have flipped and the marker and ticks proven to have; the assertion that docking right moves no frame at all; the mutations; `DECISIONS.md`, `AGENTS.md`, `PROGRESS.md`, the changelog |

## Testing

Per the house rule, every new test is proven able to fail before it is committed. Per `AGENTS.md`
§5 a bucket is a pixel row, so the pixel assertions are the guard and the PNG is what a reviewer
looks at.

| Test | Asserts |
|---|---|
| Docked right is unchanged | Every composite snapshot passes untouched, and the map's bounds equal plan 00007's |
| Docked left, the map is outside the left pane | Its right edge is at or before the left pane's left edge |
| The lanes do not flip | A left-only block inks the lane nearer the left in **both** placements |
| The marker hugs the panes | The current block's column is on the map's right edge docked left, and its left edge docked right |
| The ticks hug the outside | A find match ticks the opposite edge from the marker, in both placements |
| `LaneAt` follows the geometry | The x it reports as a lane is the x the lane is drawn at, in both placements |
| The drag and the jump still divide at the box | Unchanged by placement — the box spans the width either way |
| A host setting the placement in XAML is wired | The template-application path, not just the property-change path |
| Off is still off | `ShowMinimap = false` collapses whichever slot the map is in |

## Risks

| Risk | Assessment |
|---|---|
| **The shifted column indices touch every child of `PART_Panes`** | Three `Grid.Column` values move by one. If one is missed the panes overlap, which no test would have to be clever to catch — but the empty-`Auto` claim is the subtler half, so the "docked right moves no frame" test is the one that matters |
| The mirroring gets duplicated into three call sites and drifts | Which is why it is one helper and one `MirrorEdges` flag rather than three `if`s. A mutation that flips only one of the three should fail a test |
| Docked left, the map's tooltip or cursor may feel wrong at the window edge | Nothing about either depends on the edge; if it does read wrong, the frames in phase 2 are where it shows |
| Snapshot churn | Expected to be **zero** when docked right and confined to the new frames otherwise. §5's sweep confirms rather than assumes it |

## Conventions

Conventional Commits, dense bodies, no AI attribution trailer. Every user-visible string through
`DiffViewStrings`, every log line through `DiffViewLog`. The theme audit regenerates after any
change under `src/DiffView.Avalonia/Themes`. New tests are proven able to fail before they are
committed, and a snapshot is never the only guard for anything smaller than a row.
