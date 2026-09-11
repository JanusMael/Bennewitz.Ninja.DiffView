# 00012 — Context menus beyond the pane

Plan 00010 built a seam and used it once. Right-click in a pane and a menu opens; right-click on
the gutter beside it, on the connector between the panes, on the map, or on a header, and nothing
happens. Those four were named as non-goals then, deliberately, and `DiffPaneRegion` was shaped as
an enum with room so that adding them later would be a case rather than a redesign.

For four of the five new surfaces that holds exactly, and this is a small plan. **The header is the
one it does not hold for**, and that is the part worth arguing about before any code.

## Goal

A right-click anywhere the control draws opens a menu about what is under the pointer, through the
same two extensibility shapes plan 00010 built — an opening event carrying the context and a
mutable item list, and a property that replaces the menu outright. And the icon column, reserved
and empty since 00010, gets an icon set.

## Non-goals

| Excluded | Note |
|---|---|
| The status strip and the find bar | Both are their own controls with their own verbs, and nobody has asked. The seam does not stop a later plan adding them |
| Host-defined verbs in `DiffCommand` | Plan 00009's rule, unchanged: `DiffCommand` names *this control's* verbs. A host's item carries the host's own `ICommand`, through the opening event |
| A second menu implementation | `DiffPaneMenu.Request` stays the only place that turns a context and a list into an open menu. `AGENTS.md` §7 exists because of what happens otherwise |
| Changing what the pane menu offers | 00010's list is v1 and stays v1 here. This plan adds menus beside it, not items to it |
| A menu on a pane the control did not build | Out of reach, as before |

## Architecture

### What each menu is *about*, which is the whole design

| Surface | Its subject | What already answers it |
|---|---|---|
| Text | a line | `ContextAt(line)` today |
| Line-number margin | a line, and the copy arrow drawn on it | the same line resolution; the arrow's block |
| Change-marker margin | a line, and the chip's run it belongs to | the same |
| Connector gutter | a **change block** | `ConnectorPolygon.Contains(Point)` and `BlockClicked` already hit-test one |
| Overview map | a **row**, and the block at it if any | `JumpRequested` already maps a y to a row |
| Header | a **side and its file** — no line at all | `Title`, `Detail`, `Badge`, `IsDirty` |

The first five are line-, row- or block-shaped. `DiffPaneContext` already carries `LineNumber`,
`Row` and `Block`, so each of them is a new `Region` value and a constructor call — precisely the
shape plan 00010 bought.

### The locked decision this plan amends, and why

Plan 00010 decided: *"adding them later must not change `DiffPaneContext`, which is why `Region` is
an enum with room rather than a `bool IsText`."* That decision was made with the margins in view,
and for the margins, the connector and the map it is exactly right — this plan honours it.

**It does not survive the header.** `DiffPaneContext.LineNumber` is a non-nullable `int`, and a
header has no line to put in it. Three ways out, and only one of them is honest:

| Option | Why not |
|---|---|
| A synthetic line number — `0`, or line 1 | A lie a consumer reads as truth. The whole point of the context object is that a host can act on it without guessing |
| Make `LineNumber` nullable | Changes a non-null field every existing consumer already reads, to buy one region that is not a line. The cost lands on the five regions that *do* have a line |
| **A sibling record for the header** | The header's subject genuinely differs in kind. Its verbs are file verbs — save, revert — not line verbs |

So: **`DiffHeaderContext`**, a public record, raised through its own event and its own
replace-property, both built on the same `DiffPaneMenu.Request`:

```csharp
public sealed record DiffHeaderContext(
    DiffSide Side,
    string Title,
    string? Detail,
    bool IsDirty,
    bool IsReadOnly);
```

Five members and no more. `Side` is non-nullable here, unlike on `DiffPaneContext`, because the
unified view has no header menu at all — there is no case where a header exists without a side.
`DiffPaneContext` is untouched, so 00010's decision holds where it was aimed and is amended only
where it was not.

**Decided by Brian, 2026-09-11**, against the two alternatives — a synthetic line number, and
relaxing `LineNumber` to `int?` — on the grounds that the header's subject differs in kind and the
five line-shaped regions should not pay for the one that is not. Recorded in `DECISIONS.md` as an
amendment when this plan is implemented; plan 00010 is not edited.

### Two new enum members, not three

`DiffPaneRegion` gains `ConnectorGutter` and `OverviewMap`. It gains **no** `Header` member — a
header is not a region of a pane, and giving the enum a value that its own context type cannot
describe is how the enum starts lying.

### Where each menu is raised

| Surface | How |
|---|---|
| The two margins | The presenter already handles `ContextRequested`; which margin is a pointer-x test against the margins' own bounds. `ContextAt(line, region)` finally gets a caller that passes the second argument — it has taken a defaulted `region` since 00010 and nothing has ever supplied one |
| Connector, map | Each gets its own `ContextRequested` handler and builds the context from the hit-test it already performs for `BlockClicked` / `JumpRequested`. A right-click must **not** also jump or select — that is the first test |
| Header | Its own handler, building `DiffHeaderContext` |

### The items

Every label through `DiffViewStrings`, every accelerator from `GestureFor`, never typed. Absent
where the view has no such verb, disabled where it has it and cannot run it — 00010's rule.

| Surface | Items |
|---|---|
| Line-number margin | The pane's copy items for the line's block, *go to this change*, then the pane menu's navigate and find group. The arrow drawn here is a copy control, so the menu names the same operation |
| Change-marker margin | *Go to this change*, *select this block*, and the copy items. The chip names a run, so the verbs are the run's |
| Connector gutter | `CopyBlockToLeft`, `CopyBlockToRight`, *go to this change*, *select this block*. The block is unambiguous here — it is what the polygon **is** |
| Overview map | *Go to this row*, *go to this change*, a separator, and *hide the overview map*. Short by nature — the map is a navigation surface and a left-click already does its main verb — and **kept anyway, decided 2026-09-11**, because the hit-test already exists so the context is a constructor call, and because a seam that answers every surface but one is a seam a host has to special-case |
| Header | Save, revert, and the side's identity items. **No copy and no navigate** — a header is not a position |
| Unified view | The map and the connector do not exist there, and the header has no side. Only the margins gain a menu |

### The icon column, filled

`DiffMenuItem.Icon` has been null everywhere since 00010, in a column laid out whether or not
anything fills it, with a test asserting a solid square does not move any label. The icons come
from the vocabulary the gutter already uses rather than a new set: the `+` `−` `≠` operators for the
kind verbs and the copy arrows' own silhouette for the copy verbs, drawn as geometry on the theme's
foreground so a palette swap carries them. **00010's alignment test keeps passing unchanged** — that
is the assertion that the column was reserved correctly, and filling it is what finally exercises it.

## Phases

| Phase | Size | Content |
|---|---|---|
| **1 — The margins** | S | Pointer-x resolution to `LineNumberMargin` / `ChangeMarkerMargin`, their item lists, and the first caller that passes `ContextAt`'s region argument. Both views |
| **2 — The connector and the map** | M | Two new `DiffPaneRegion` members, a `ContextRequested` handler on each control built from its existing hit-test, and the rule that a right-click navigates nothing. Side-by-side only |
| **3 — The header** | M | `DiffHeaderContext`, its event and its replace-property, on `DiffPaneMenu.Request`. The phase the amendment above has to be approved for |
| **4 — The icon set** | S | Geometry for the kind and copy verbs, on the reserved column, with 00010's alignment test unchanged |
| **5 — Evidence** | S | Mutations, one rendered frame per new surface, `DECISIONS.md`, `AGENTS.md` §6 and §7, `PROGRESS.md`, the changelog |

## Testing

Every new test is proven able to fail before it is committed.

| Test | Asserts |
|---|---|
| Each margin reports its own region | A right-click at a pointer-x inside the line-number margin gives `LineNumberMargin`, inside the marker margin gives `ChangeMarkerMargin`, and in the text still gives `Text`. The boundary pixel belongs to exactly one of them |
| A right-click on the connector does not jump | The block under the pointer is named in the context and `CurrentChangeIndex` is **unchanged** — a left-click still jumps. Same for the map, whose left-click scrolls |
| The connector's context names the polygon's own block | Right-click inside a polygon, and `Block` is the block that polygon draws — not the block nearest the pointer's row, which is a different answer where a polygon is tall |
| The map's context names the row under the pointer | Against what the map *reports*, not against pixels — plan 00007's rule, because a lane is thin lines rather than a band when rows are sparser than pixels |
| A header's context has no line | `DiffHeaderContext` names the side and its file; there is no `LineNumber` on the type to be wrong |
| The unified view has no connector, map or header menu | They do not exist there, so no handler is wired and nothing is raised — absent, not empty |
| Every new menu goes through the one implementation | The replacement property suppresses the opening event on each new surface exactly as it does on the pane — one test per surface, because that rule living in `DiffPaneMenu` is the only thing keeping them in step |
| The icon column still does not move labels | 00010's test, unchanged, now with real icons in it rather than a stand-in square |
| Every new item carries an automation name | The a11y sweep extended to the new menus; they are built in code, so the XAML guard cannot see them |

## Risks

| Risk | Assessment |
|---|---|
| **The header's second context type splits the seam in two** | Settled before any code, and the reason the header is phase 3 rather than phase 1. Mitigated by both types going through one `DiffPaneMenu.Request`, so the *menu* behaviour cannot diverge even though the *contexts* differ. The standing test for this is that the replacement property suppresses the opening event on **both** types — if that ever holds for one and not the other, the seam has split in fact and not just in type |
| A right-click that also navigates | The connector and the map both act on click today. The test is written before the handler, as in 00010 |
| Pointer-x margin resolution drifts from what is drawn | The margins own their widths; the resolution reads the margin's own bounds rather than a constant. A constant here is the 8-px header bug of plan 00008 waiting to happen again |
| The icon set becomes a design project | Scoped to the vocabulary already on screen. No new visual language, no icon font, no asset pipeline |
| Scope creep into the strip and the find bar | Named as non-goals. The seam does not stop them later |

## Conventions

Conventional Commits, dense bodies, no AI attribution trailer. Every user-visible string through
`DiffViewStrings`, every log line through `DiffViewLog`. New tests are proven able to fail before
they are committed. An approved plan is committed before implementation and never edited; drift —
including the amendment above, if approved — goes to `DECISIONS.md`.
