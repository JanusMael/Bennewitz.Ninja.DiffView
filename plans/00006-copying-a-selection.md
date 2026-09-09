# 00006 — Copying a selection, and an arrow that reads as a shape

Plan 00004 put a copy arrow over each block's anchor row: click it and the block goes to the other
side. Two things are missing. A user who has selected some lines has no way to send *those* — only
whole blocks. And the arrow is a flat silhouette in one colour, which reads as a mark rather than
as a control.

This plan adds the selection copy, and gives the arrow an outline and a fill so it reads as a
shape. The two belong together: the second arrow needs to be told apart from the first, and
deciding how is the same question as deciding what an arrow looks like.

## Goal

A selection in an editable-neighbour pane offers its own arrow, in its own colour **and its own
shape**, which copies the selected lines to the other side. Both arrows are drawn with an outline
carrying the silhouette and a fill inside it. Every arrow colour is scored against the ground it
sits on — which none of them is today.

## Non-goals

| Excluded | Note |
|---|---|
| A keyboard path for the selection copy | Alt+Left / Alt+Right keep copying the **current block**, unchanged, including while a selection exists. A selection copy is pointer-only here; giving it a chord is a later decision, and guessing at one now would make Alt+Left ambiguous |
| A shape cue in the default palette | Settled: colour alone there, colour and a tail bar in the colour-blind palette. The token that carries it is `Transparent` by default, so a host that disagrees changes one key rather than forking the glyph |
| Copying a partial line | The model is line-based and every existing copy is whole lines. A selection that starts mid-line copies the whole of that line, as `CopyBlock` does |
| Copying a selection out of the unified view | `InlineDiffView` is read-only and its document is half one file and half the other; §7's rule is untouched |
| Renaming `DiffView.GutterArrowBrush` | The arrows moved out of the connector column in plan 00004, so the name is a shade stale. Renaming a theming key churns every host that overrides it, and the key still names a gutter — the line-number margin is one |
| A second selection arrow on the receiving side | The arrow lives with the source, as plan 00004 settled. A selection has one owner |

## Architecture

### The arrow reads as a shape

`CopyArrowGlyph.Draw` takes a fill **and** a pen. The outline carries the silhouette, so it is the
colour with the strongest contrast against the ground; the fill sits between the outline and the
ground, which is what makes the arrow read as an object rather than a stencil.

| | outline | vs gutter | fill | vs gutter | outline vs fill |
|---|---|---|---|---|---|
| Block arrow, Light | `#37474F` | 8.77 | `#607D8B` | 3.97 | 2.21 |
| Block arrow, Dark | `#CFD8DC` | 10.58 | `#78909C` | 4.57 | 2.31 |
| Selection arrow, Light | `#0D47A1` | 7.84 | `#1976D2` | 4.18 | 1.88 |
| Selection arrow, Dark | `#90CAF9` | 8.75 | `#42A5F5` | 5.78 | 1.51 |

`DiffView.GutterArrowBrush` keeps its value and becomes the outline; `DiffView.GutterArrowFillBrush`
is new. `DiffView.SelectionArrowBrush` and `DiffView.SelectionArrowFillBrush` are new for the
second arrow.

### The colour-blind palette cannot use blue

Beyond Compare tells its two arrows apart by hue — yellow for the diff copy, bluish for the
selection copy. In the **default** palette a blue is free and that is what this plan uses. In the
**colour-blind** palette it is not: `#0072B2` already means *inserted*. Okabe–Ito's bluish green
`#009E73` is unused and is what the selection arrow takes there — 3.11 as a fill on the gutter,
6.45 as an outline.

| | outline | fill |
|---|---|---|
| Colour-blind Light | `#00654A` | `#009E73` |
| Colour-blind Dark | `#7FD9BE` | `#009E73` |

`DiffView.SelectionArrowBarBrush` takes the outline's colour in this palette and `Transparent` in
the default one.

### The shape cue is the palette's decision, not the code's

The two arrows are told apart **by colour in the default palette, and by colour and shape in the
colour-blind one**. The selection arrow can carry a **bar across its tail** — the same head and
shaft with a stroke at the trailing edge — which reads as "this bounded thing goes that way"
against the block arrow's plain "that way", and survives a rendering with no colour at all.

Rendered side by side at 12 px, the bar costs a little crispness and the colour difference alone is
strong in the default palette; with colour discarded, the bar is the only thing that separates the
two glyphs at all. So it is drawn where it earns its cost and not where it does not.

**The palette decides, through a token.** `DiffView.SelectionArrowBarBrush` is `Transparent` in
`DiffView.Tokens.axaml` and the arrow's outline colour in `DiffView.Tokens.ColorBlind.axaml`; the
margin always lays the bar out and paints it in that brush, so a transparent one simply is not
seen. No new resolver, no new property, and nothing in `ChangeMarkerMargin` or the composite has
to know which palette is merged — which it cannot, since the palette is a resource dictionary the
*host* merges over the tokens (`App.UseColourBlindPalette` in the demo).

A property would have been the other way to do it, and is worse: the host would merge the
dictionary *and* set the property, and the two could disagree. A token cannot disagree with itself.
It also means a host that wants the bar in the default palette sets one key, and one that ships a
palette of its own decides for itself how much shape it needs.

### Copying a selection

`SideBySideDiffView.CanCopySelection(DiffSide fromSide)` and `CopySelection(DiffSide fromSide)`,
mirroring `CanCopyBlock` / `CopyBlock`.

The operation is the block copy's rule with a different range. The selection's **whole lines** on
`fromSide` occupy a run of rows; the other side's lines in those same rows are the target; the
target is replaced by the source's text. Where the other side has no lines in those rows — every
row is padding — the replacement is an insertion at that point, which is what `CopyBlock` already
does for a one-sided block. `SideBySideDocument.Rows` and `SideBySideDocument.LineOf` give the
mapping; nothing new is needed in Core.

Undo is the editor's own, as every copy is, and the re-diff that follows collapses whatever the
copy made identical.

### Where the arrow goes, and what happens when both want the cell

The selection arrow takes the number cell of the selection's **first row**, exactly as a block
arrow takes its block's anchor row.

When a selection begins on a row that already carries a block arrow, **the selection arrow wins
that cell and the block arrow is not drawn there.** A selection is the more specific and more
recent intent, and two arrows cannot share one cell. The block's own copy remains available from
the keyboard, which is one of the reasons Alt+Left and Alt+Right stay bound to the block.

`TextArea.SelectionChanged` invalidates the number margin, so the arrow appears and vanishes with
the selection rather than at the next unrelated redraw — the same failure `AffectsRender` was
added to `ChangeConnectorGutter` to avoid, and the kind that looks like a race and is not.

### The contrast pairs the arrows never had

`contrast-pairs.json` scores the caret, the current-block border, the markers, their chips, the
connector and the minimap ticks. **It has never scored an arrow.** Plan 00005 made the rule
explicit — a decorator that draws a ground under a glyph needs a pair of its own — and this is the
same gap one control over: the block arrow has been drawn on the gutter since plan 00003 with
nothing holding it to a floor.

Four pairs go in: each arrow's outline against the gutter background, and each arrow's fill against
it, both at 3.0. The outline-versus-fill ratios are recorded above but not contracted — an outline
is a shape's edge, not text on a ground, and 1.5 is enough to see it.

## Phases

| Phase | Size | Content |
|---|---|---|
| **1 — The arrow reads as a shape** | M | `CopyArrowGlyph.Draw` takes a fill and a pen; the fill tokens for both palettes and both variants; the four contrast pairs, including the two the block arrow never had; `theme-audit compat` then `report`. The block arrow gains its fill here and the selection arrow does not exist yet |
| **2 — Copying a selection** | L | `CanCopySelection` / `CopySelection` over the row mapping; the selection arrow with its bar and its own tokens; the first-row placement and the rule when it meets a block arrow; `TextArea.SelectionChanged` invalidation; the pointer's hand over it |
| **3 — Evidence** | S | Snapshots of both arrows in one frame, a selection spanning padding, and the collision case, in both variants and both palettes; the mutations; `DECISIONS.md`, `AGENTS.md`, `PROGRESS.md` and the changelog |

## Testing

Per the house rule, every new test is proven able to fail before it is committed.

| Test | Asserts |
|---|---|
| The arrow is outlined and filled | Both colours are painted inside one arrow's zone, the outline on its edge and the fill inside it |
| The selection arrow is a different shape | Its painted pixel signature differs from the block arrow's in a monochrome reading — the bar is present with colour discarded |
| A selection offers an arrow | A multi-line selection in a pane whose neighbour is editable draws one arrow, on the selection's first row |
| No selection, no arrow | Clearing the selection removes it on the next frame, driven by `SelectionChanged` and not by an unrelated redraw |
| A read-only neighbour offers nothing | A selection in a pane whose neighbour is read-only draws no selection arrow, as `CanCopyOut` already gates the block arrow |
| The copy takes whole lines | A selection starting and ending mid-line copies both lines whole |
| The copy lands in the aligned rows | The other side's lines in the selection's rows are replaced, and the re-diff collapses what became identical |
| A selection over padding inserts | Where the other side has no lines in those rows, the copy inserts rather than replacing |
| The selection arrow wins the cell | A selection beginning on a block's anchor row leaves exactly one arrow there, the selection's |
| Both arrows clear their floors | `theme-audit report` finds 0 low-contrast findings with the four new pairs present |

## Risks

| Risk | Assessment |
|---|---|
| **The row mapping for a selection is the sharpest code risk.** A selection's rows are not a block's rows, so none of `CopyBlock`'s ranges apply | It is the same table, read differently: `SideBySideDocument.Rows` maps a row to each side's line or to nothing. A test per shape — inside one block, spanning two, over padding, past the last line — and the arithmetic is pinned rather than reasoned about |
| Two arrows in one cell | Decided rather than left to render order: the selection's wins. A test pins it, because a rule that lives only in the order of two `if`s is the rule plan 00004 had to retire |
| The selection arrow's bar is small | It is one segment on a 12 px glyph. Rendered at size it reads, but the phase 3 frames in the colour-blind palette are how that gets confirmed. If it does not, the fallback is a different silhouette — a double head, or a squared tail |
| **The colour-blind palette is opt-in, so the shape cue is too** | A reader who cannot separate blue from slate gets colour-only arrows until they find the toggle — which is exactly the argument that made the change markers `+` `−` `≠` in *both* palettes. Accepted here on a real distinction: a marker is read at a glance and never confirmed, while an arrow is hovered, carries a tooltip naming what it would do, and shows a hand cursor before it is clicked. The tooltip is the safety net the markers never had, and phase 2 makes sure the selection arrow's names what it copies |
| Baselines will not fail on their own | Certain, as always at glyph scale. §5's zeroed-comparer sweep applies |
| `SelectionChanged` fires often | It is a margin invalidation, not a rebuild; the margin already redraws on every scroll. If it proves hot, the guard is comparing the selection's first row against the last drawn one |

## Conventions

Conventional Commits, dense bodies, no AI attribution trailer. Every user-visible string through
`DiffViewStrings`, every log line through `DiffViewLog`. The theme audit regenerates after any
change under `src/DiffView.Avalonia/Themes`. New tests are proven able to fail before they are
committed, and a snapshot is never the only guard for anything smaller than a row.
