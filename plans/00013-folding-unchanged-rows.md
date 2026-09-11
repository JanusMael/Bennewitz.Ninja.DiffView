# 00013 — Folding unchanged rows

The largest functional delta from Beyond Compare, flagged since plan 00010 and never proposed. The
feasibility spike ran on 2026-09-11 (`dc93c20`, `FoldingSpikeTests`, `DECISIONS.md` §"Folding: go
for hiding what matches, and the price is a row projection") and came back split: hiding what
matches is feasible and cheap, hiding what differs is not, and the expensive part of either is
neither the folding nor the panes.

**The panes are the easy half.** A fold taken symmetrically over unchanged aligned rows removes
equal height from each, every surviving pair keeps its row top, and in-pane drawing already walks
`TextView.VisualLines` and reads `VisualTop`, which the height tree answers under a fold without
being asked. **The hard half is everything outside a pane**: the connector gutter and the overview
map are fed `row × DefaultLineHeight`, and the unified view scrolls by `firstLine × lineHeight`.
A fold is the first thing in this library to break that equation. So this is mostly a plan about
introducing one projection between rows and pixels, and the folding rides on it.

## Goal

Unchanged rows collapse to a single placeholder row per run, with a configurable number of context
rows kept around every change — Beyond Compare's *Show Context*, of which its *Show Differences* is
the case where that number is zero. Both views. On by default: **off**.

## Non-goals

| Excluded | Note |
|---|---|
| ***Show Same*** — hiding what differs | The spike's finding, and the reason it is named here rather than met in a last phase: a change block is lines on one side and **padding** on the other, and padding is height on the following line rather than lines of its own, so there is nothing there to collapse. It needs `PaddingSpec` to become a function of what is folded — a change to the mechanism plan 00001 built, not an addition beside it |
| Syntax folding — braces, regions, `XmlFoldingStrategy` | This is diff folding. A run is foldable because the *model* says its rows match, never because the text has a shape |
| `FoldingManager`, `FoldingSection` and `FoldingMargin` | The spike took the primitive and left the stack: `Install` adds a margin to `TextArea.LeftMargins` and inserts its generator at **index 0**, which is the slot `PaddingElementGenerator` holds. `TextView.CollapseLines` writes the height tree and hands back something that uncollapses, which is all a view option needs |
| A fold a host defines | Rows fold because the model says they are unchanged. A host-defined fold is a different feature and nobody has asked |
| Word wrap | Still off, `AGENTS.md` §1. A fold changes nothing about why |
| Fold state surviving a rebuild | A rebuild makes a new model, so the runs are recomputed from it. Nothing is remembered across one, and there is no per-run expanded/collapsed memory to get stale |

## Architecture

### What the spike settled, and what it left

| Settled | Consequence for this plan |
|---|---|
| A symmetric fold over unchanged rows keeps both panes aligned | The panes need no new alignment machinery. Phase 2 is a translation problem, not a rendering one |
| A fold's range cannot be read off a document | The range is a **row** range, computed once from the model and projected onto each side's lines. Two line ranges, one row range |
| Padding is height on a line, so a fold starting at a padded line swallows padding belonging to rows above it | The boundary rule below. The spike measured this at a run's *start*; its mirror at the end, where a side's trailing gap rides on that side's last line, is reasoned from the same mechanism and is phase 2's to prove |
| In-pane drawing is already fold-safe; the out-of-pane surfaces are not | Phase 1, and the reason it comes first |
| Collapsing leaves the visual lines and the published extent stale | Every fold and unfold ends in a `Redraw()` **and** an `InvalidateMeasure()`, the two steps `PaddingHeightPrimer` already takes |

### The row projection, which is the load-bearing part

Three sites convert between rows and pixels by multiplying, and each is wrong the moment anything
is folded:

| Site | What it computes today |
|---|---|
| `SideBySideDiffView` ~3080 | `gutter.RowHeight = lineHeight`; `minimap.ViewportStartRow = VerticalOffset / lineHeight`; `ViewportRowCount = ViewportHeight / lineHeight` |
| `SideBySideDiffView` ~3388 | `top = firstRow * lineHeight`, `height = rowCount * lineHeight` for the scroll-to |
| `InlineDiffView` ~1757 | `top = firstLine * lineHeight` for the same |

A `RowProjection` owns both directions — model row to visible row and back, and visible row to
pixel — and is the only place either conversion happens. **When nothing is folded it is the
identity**, which is what makes phase 1 shippable on its own: the behaviour that exists today is
the projection's empty case, and a test says so.

`DiffMinimap` maps the document to its own height and therefore maps the **visible** document once
this lands. A map of rows the panes are not showing would put its viewport box somewhere the
viewport is not.

### What a fold is, and where it ends

A **run** is a maximal stretch of `DiffLineKind.Unchanged` rows between two change blocks, or
before the first and after the last. A run becomes a fold after three cuts, in this order:

1. **Context.** Drop `n` rows from each end, `n` being the option's value.
2. **The boundary rule**, below.
3. **The floor.** If fewer than `MinimumFoldedRows` remain, fold nothing: a placeholder that hides
   two rows costs a row to save two, and the reader loses more than they gain.

### The boundary rule

`TextView.CollapseLines` hides whole document lines, and a line's height is all or nothing. Padding
is height *on* a line, so a line can be carrying height that belongs to rows the fold does not
cover — and collapsing it takes that height with it, on one side only. The rule is therefore not
about padding at all:

> **A row is foldable only if collapsing its lines removes that row's height and nothing else.**

Which is a property of the fold's **neighbours**, needing no lookup in the padding table:

- the **first** row is foldable iff the row above it has a line on *both* sides — otherwise this
  side's line is carrying padding for rows above the fold, rows the other side is still showing;
- the **last** row is foldable iff nothing after it is riding on a side's final line — the trailing
  gap that `CloseTrailingPadding` puts in that line's `Below`.

At most one row at each end, because padding only ever accumulates at a run's boundary: inside an
unchanged run every row has a line on both sides, so no interior line can carry any. A row the rule
drops stays visible as an ordinary row and keeps its padding, which is the whole point of dropping
it — that padding is the visual counterpart of rows the *other* pane is still drawing.

**The rule fires only at `n = 0`.** With any context at all, cut 1 has already moved both ends past
the boundary rows. So it costs at most two rows per fold, in *Show Differences* alone — and that is
the measure the alternatives were weighed against, settled 2026-09-11 before any code:

| Option | Why not |
|---|---|
| Trim one row at each end **unconditionally**, with no neighbour test | Costs two rows per fold always, including after a modification, where nothing is padded and nothing needs trimming. It buys a uniform fold shape, which is the one real argument against the rule as written: at `n = 0` a placeholder is preceded by a context row after a one-sided block and by none after a modification. Not worth paying for on every fold |
| Make `PaddingSpec` a function of **fold state**, re-homing the orphan | Buys back the one or two rows, and is the machinery *Show Same* would need — but it stops the padding spec being a pure function of the model and puts a re-prime on every fold and unfold. It is a change to the mechanism plan 00001 built, which is exactly what this plan's first non-goal declines. Nothing here makes it harder later: it needs the row projection first regardless |
| Give the placeholder line a **synthetic** padding spec matching the orphan | Ends up with geometry identical to the rule as written, except that one line of text is also hidden. One row per fold, for a synthetic-padding path through the placeholder |

### The placeholder row

A fold leaves its first line **visible** on each side and collapses the rest, which is how
`FoldingElementGenerator` works and what `TextView.CollapseLines` documents itself for. That line
renders as a placeholder naming how many rows are hidden, and a click on it expands the run.

A fold of `k` rows therefore shows one row and hides `k − 1`, on both sides, and stays symmetric.
The placeholder is a `VisualLineElementGenerator` of its own, and it must **compose** with
`PaddingElementGenerator` rather than displace it: the padding element is zero document characters
and one visual column at the line's start, so it sets the line's height and the placeholder then
covers the line's text. The boundary rule has already guaranteed that the placeholder's own line
carries no padding, so on that line the two generators do not compete — but they compete on every
other padded line in the document either way, and an order that is right by accident is what phase
3 has to rule out.

### The option

One property, `UnchangedContextRows`, an `int?` defaulting to `null`:

| Value | Behaviour | Beyond Compare's name |
|---|---|---|
| `null` | Nothing folds | *Show All* |
| `0` | Every unchanged run folds | *Show Differences* |
| `n` | `n` rows kept either side of every change | *Show Context* |

One property rather than a `bool` and an `int`, because a pair can express "folding off with three
context rows", which is a state with no meaning and therefore a state to test, document and get
wrong.

### The unified view

`InlineDiffView` has one pane and so has no alignment constraint at all — its fold is the same run
computation against `InlineDocument`, with no second side to keep in step. It needs the projection
for exactly one reason: `ScrollToLines` multiplies.

## Phases

| Phase | Size | Content |
|---|---|---|
| **1 — The row projection** | M | `RowProjection`, the three multiplying sites routed through it, and `DiffMinimap` mapping visible rows. **No folding yet**: the projection is the identity, and the phase's evidence is that nothing moves. Both views |
| **2 — Folding the runs** | M | Runs from the model, the three cuts, the two line ranges per fold, `CollapseLines` on each side, and the redraw-and-measure that publishes it. Still no UI and no placeholder — a method and a test that the extents stay equal and every surviving pair keeps its row top |
| **3 — The placeholder row** | M | The element generator, its composition with the padding generator, the string, the automation name, and click-to-expand |
| **4 — The option and the verbs** | S | `UnchangedContextRows` on both views, the key map entries, the View menu, and the fold verbs on the menus plan 00012 built — the margins and the connector each know a run |
| **5 — Evidence** | S | Mutations, rendered frames for folded and unfolded, `DECISIONS.md`, `AGENTS.md` §6 and §7, `PROGRESS.md`, the changelog |

Phase 1 is worth shipping even if the rest is abandoned: it replaces an equation that is already
only accidentally true — it assumes every row is one line height, which padding makes true only
because a padded line stands for several rows.

## Testing

Every new test is proven able to fail before it is committed.

| Test | Asserts |
|---|---|
| The projection is the identity when nothing is folded | Every row maps to itself and every pixel to the row it maps to today. The phase-1 guarantee, and the one that lets the rest land incrementally |
| A folded pair of panes has equal extents | And every surviving aligned pair still shares a row top. **Mutated by one row on one side, it must fail** — the spike's version did, and equal heights are not alignment |
| A fold never starts on a line carrying padding for rows above it | The boundary rule's first half, against a fixture whose every run begins after a one-sided block, which is what produces the orphan. `n = 0`, because that is the only value at which the rule fires |
| A fold never ends on a side's last line while that side has a trailing gap | The rule's second half, and **the half the spike did not measure**: `Item2` exercised `PaddingSpec.Above` only, and `Below` is the same mechanism through the spec's other field. A fixture ending in a one-sided block, and the assertion is the panes' extents |
| A run shorter than the floor does not fold | And the rows stay exactly where they were |
| The placeholder row is one row on each side | A fold of `k` rows shows one and hides `k − 1` on both sides, and the first pair below it still shares a row top |
| Expanding a fold restores every row top | Collapse, expand, and every pair is where it started — against a document taller than the map, because a short fixture cannot exercise the projection's bucketing at all |
| The map's viewport box follows the visible document | A folded document scrolled to the bottom puts the box at the bottom. The assertion is against what the map *reports*, not against pixels |
| Navigation crosses a fold | F7 to a change inside a folded run expands it, or lands on the row after it — decided in phase 4, asserted either way |
| Find crosses a fold | A match inside a folded run is either revealed or excluded, never counted and unreachable |
| The unified view folds the same runs | One pane, the same model, and `ScrollToLines` landing on the right row |

## Risks

| Risk | Assessment |
|---|---|
| **The projection touches everything that reads a row** | The reason it is phase 1 and ships as the identity. Anything that reads rows for *model* purposes — `WordDiffLookup`, `PaneMetadata` — keeps reading model rows and is not in the projection's path at all. Only rows-to-pixels moves |
| The placeholder generator displaces the padding generator | The spike found `FoldingManager` inserting its generator at index 0 for exactly this reason, and that is why the stack is a non-goal. Ours composes instead: zero document characters for the padding, the line's text for the placeholder. Asserted in phase 3, which is why the placeholder is not phase 2 |
| Scroll position jumps when a run folds under the viewport | Fold and unfold both anchor on the model row at the top of the viewport, through the projection. Named here because it is the difference between a feature and an annoyance |
| Snapshot churn hides a real defect | Plan 00008's lesson. Folding moves nearly every frame, so the phase-5 evidence asserts the *invariant* — equal extents, equal row tops — and adds frames rather than regenerating the existing ones wholesale |
| Scope creep into *Show Same* | Named as a non-goal with its reason. The row projection is what it would need first anyway, so this plan does not make it harder |
| The floor and the context count need tuning by eye | Both are options with defaults, and the by-hand pass under `AGENTS.md` §9 is where the defaults get chosen. Beyond Compare is installed at `/usr/bin/bcompare` and is the reference |

## Conventions

Conventional Commits, dense bodies, no AI attribution trailer. Every user-visible string through
`DiffViewStrings`, every log line through `DiffViewLog`. New tests are proven able to fail before
they are committed. An approved plan is committed before implementation and never edited; drift
goes to `DECISIONS.md`.
