# 00010 — The pane context menu

A right-click in a pane does nothing. Beyond Compare answers it with the menu that carries most of
what it can do to the text under the pointer, and that is the visible gap. The invisible one is
larger: a host that wants its own menu cannot write one, because everything it would need to ask —
which side is this, which line, which block, is there a selection — lives in `PaneMetadata`, which
is `internal`.

So the menu is the smaller half of this plan. **The context object is the deliverable**, and the
menu is the first thing built on it.

Plan 00009 is the other half of the foundation. A menu that printed `Alt+Left` beside "copy this
change" would start lying the first time a host rebound it; `GestureFor` is where the accelerator
comes from, and it exists now.

## Goal

Right-click in either view's pane opens a menu of what this control can do to what is under the
pointer, on by default. A host can insert an item into it, remove one, or replace the menu
outright, and in every case can read a **public** description of what was clicked.

## Non-goals

| Excluded | Note |
|---|---|
| Menus on the margins, the connector, the map and the headers | v1 is the panes. Each of those has its own verbs — a marker chip's block, a header's file — and adding them later must not change `DiffPaneContext`, which is why `Region` is an enum with room rather than a `bool IsText` |
| An icon set | The column is reserved and `DiffMenuItem.Icon` exists, null everywhere. BC's own pane menu is mostly unicoded items with a handful of icons, and they align; that alignment is the thing to get right now, because retrofitting it moves every label |
| Host-defined commands in the table | Plan 00009's rule, unchanged: `DiffCommand` names *this control's* verbs. A host's item carries the host's own `ICommand`, through the opening event |
| Clipboard verbs of our own | Cut, Copy, Paste and Delete are AvaloniaEdit's, already bound in the text area, and re-implementing them would give the menu a second opinion about the editor's own state |
| A menu on a pane the control did not build | Out of reach and out of scope |

## Architecture

### The context object, which is the part that has to be right

`DiffPaneContext` — a public record built at the moment the menu opens, carrying:

| Member | Note |
|---|---|
| `Region` | `DiffPaneRegion.Text`, `.LineNumberMargin`, `.ChangeMarkerMargin`. v1 raises only `Text`; the others are named now so a later plan adds a case rather than a type |
| `Side` | `DiffSide?` — **null in the unified view**, which has no side. Not `DiffSide.Left` as a stand-in: a consumer that reads `Left` and acts on it would be wrong half the time |
| `LineNumber` | The pane's own line. In the unified view this is the composed document's, and `SourceLine` below is the side's |
| `SourceLine`, `SourceSide` | What the line *is* on its own file, which is the only numbering the unified view's gutter shows |
| `Row` | The model's row, or null for a line the model does not know |
| `Block` | `ChangeBlock?` — the block the line is in, null outside one |
| `HasSelection`, `SelectedLines` | `LineRange?`, whole lines, from `DiffPanePresenter.SelectedLines` — the same reading the selection arrow uses, so the menu and the gutter cannot disagree. **That property is `internal` today and becomes public**: the context hands out the value anyway, and a presenter that will not answer the question its own context answers is an arbitrary line |
| `IsUnified`, `IsReadOnly` | What the pane is, so a host does not have to infer it |

It is a snapshot, not a live view. The menu is open for as long as it takes to read, and a model
rebuilt under it would otherwise renumber the thing the reader is pointing at.

### Two extensibility shapes, and why one is not enough

**`PaneContextMenuOpening`** carries the context and a **mutable** `IList<DiffMenuItem>`, already
filled with the default items. This is the shape for *"our menu, plus mine"* — insert after Copy,
remove Save, retitle one — and it is the common case.

**`PaneContextMenu`**, a property, replaces the menu outright. This is the shape for *"my menu,
not yours"*, and it exists because a host that wants a different menu should not have to empty a
list to get one. Set to `null`, the control is back to its own.

Both, because either alone forces the wrong shape on half the hosts. The event does not fire when
the property is set: there is nothing of ours to amend.

### Disable, do not hide — and the line between disabled and absent

Beyond Compare's pane menu was captured for this plan, with and without a selection. **The two are
the same menu**: same items, same order, same positions. Only the enabled state moves — Isolate,
the indents, Cut, Copy, Delete and *Compare Selection to Clipboard* grey out with no selection,
and *Save File* stays grey until something is edited.

That is what makes "insert after Copy" a stable instruction. If items came and went, an index
would mean a different thing on every open, and a host's insertion would wander.

But there is a second case BC does not have, and this control does. **A verb the *view* does not
have is absent, not disabled.** The unified view has no `CopyToLeft` at all — plan 00009's
`CommandOrNull` already answers null for it, and `CommandFor` throws. A greyed *Copy to the left
side* would promise a state in which it works, and there is none. So:

- the command exists and cannot run **now** → present and disabled;
- the view has no such command **ever** → absent.

`CommandOrNull` is the one thing that decides which, so the menu cannot disagree with the key map.

### Accelerators and labels both come from somewhere else

The accelerator is `GestureFor(command)`, never a literal — the whole reason plan 00009 came
first. An unbound command shows no accelerator rather than a blank column.

The label is `DiffViewStrings`, and where an item is a verb the gutter already offers it **reuses
the gutter's own wording**: `CopyArrowTooltip` is *"Copy this change to the {0} side"* and
`SelectionArrowTooltip` is *"Copy the selected lines to the {0} side"*. The arrow and the menu
entry perform the same operation, and two wordings for one operation is how a user learns they are
two.

### Where the menu is raised, and the keyboard

Avalonia's `ContextRequested` on the presenter, not a `ContextMenu` assigned in a template. Two
reasons: it is the event that carries the pointer position, and it is **also** what the keyboard
raises — Shift+F10 and the Menu key — which a template-assigned menu would answer with a menu
built for the wrong place.

So the position is optional, and that is a real branch rather than a detail:

- **With a position** — resolve the line the way the margins' tooltips already do, through
  `TextView.GetDocumentLineByVisualTop`, and do **not** move the caret. A right-click that moved
  the caret would discard a selection the menu is about to offer to copy.
- **Without one** — resolve to the caret's line, and open at the caret. A keyboard user asking
  about "here" means the caret.

A right-click **inside an existing selection** keeps it; outside one, the selection stands too and
the context reports both it and the clicked line. Which of the two an item acts on is the item's
business, and it is the rule plan 00009 already set: the selection when there is one.

### The item list, v1

Both views, in this order, with separators as grouped:

| Item | Command | Enabled when | Unified |
|---|---|---|---|
| Copy the selected lines to the {other} side | `CopyToLeft` / `CopyToRight` | `CanCopyToward` | absent |
| Copy this change to the {other} side | `CopyBlockToLeft` / `CopyBlockToRight` | `CanCopyBlock` | absent |
| Next change | `NextChange` | `ChangeCount > 0` | present |
| Previous change | `PreviousChange` | `ChangeCount > 0` | present |
| Find… | `OpenFind` | always | present |
| Save this side | `Save(side)` | `CanSave(side)` | absent |
| Revert this side | `Revert(side)` | `IsEdited(side)` | absent |

The first item's label changes with the context — *the selected lines* or, with no selection,
*this change* — because that is what the chord does and what the gutter draws. The second names
the block always, which is the verb plan 00009 kept a name for.

Cut, Copy and Paste are deliberately **not** in the list: they are AvaloniaEdit's, they are already
on their gestures, and an item of ours would need a second opinion about the editor's state. If
they turn out to be wanted, they are a host's three lines through the opening event, which is a
fair test of whether that seam is good enough.

### The icon column is reserved, empty, and tested

`DiffMenuItem.Icon` is an `object?`, null everywhere in v1. The column is laid out regardless, so
that adding an icon later moves nothing. The test that proves it fills one item's slot with a
**solid square** and asserts the column's width is unchanged and every unicoded item's label still
starts at the same x. BC's menu is the existence proof: a handful of icons among a majority of
unicoded items, all aligned.

## Phases

| Phase | Size | Content |
|---|---|---|
| **1 — The context and the seam** | M | `DiffPaneContext`, `DiffPaneRegion`, `DiffMenuItem` with its `Icon` slot; `ContextRequested` on the presenter resolving position-or-caret; `PaneContextMenuOpening` and `PaneContextMenu` on both views; an empty default list, so the seam is testable before there is anything in it |
| **2 — The items** | M | The table above on both views, labels through `DiffViewStrings` reusing the arrows' wording, accelerators from `GestureFor`, the enabled rules and the absent-vs-disabled rule through `CommandOrNull`; the demo inserts an item of its own, so the seam is exercised by hand |
| **3 — Evidence** | S | The mutations; the icon-column alignment frame — the only rendered evidence here; `DECISIONS.md`, `AGENTS.md` §6 and §7, `PROGRESS.md`, the changelog |

## Testing

Every new test is proven able to fail before it is committed. Per `AGENTS.md` §6 a key test focuses
the pane first; per §9 the by-hand pass is a drive, not a look.

| Test | Asserts |
|---|---|
| The context names what was clicked | Side, line, row, block and kind for a click in a changed block, an unchanged line, and the trailing padding — where `Row` is null and `Block` is null and neither throws |
| The unified view's context has no side | `Side` is **null**, and `SourceLine` / `SourceSide` name the line's own file — the numbering its gutter shows |
| A right-click does not move the caret or drop the selection | Select three lines, right-click **outside** them, and the selection is still there with `HasSelection` true and `SelectedLines` unchanged. This is the one that makes copy-from-the-menu work at all |
| The keyboard raises it at the caret | `ContextRequested` with no position resolves to the caret's line, not to line 1 and not to the last pointer position |
| A host's item survives, in the place it was put | Insert after a known item through the opening event; it is still at that index after a state change that greys two of its neighbours — the assertion "disable, do not hide" exists for |
| The menu's shape does not move with the selection | Same items, same order, with and without a selection; only `IsEnabled` differs. BC's behaviour, and the reason an index means something |
| A verb the view lacks is absent, not disabled | The unified view's menu has no copy items at all, while the side-by-side view's are present and disabled when the other side is read-only |
| `PaneContextMenu` replaces, and suppresses the event | With the property set, the default items are not built and `PaneContextMenuOpening` does not fire |
| Accelerators come from the map | Rebind `NextChange` and the menu's accelerator moves with it; unbind it and the item shows none — the assertion this plan's dependency on 00009 exists for |
| Labels match the gutter's | The copy items' text is `DiffViewStrings`' `CopyArrowTooltip` / `SelectionArrowTooltip` formatted for the same side, character for character |
| The icon column is reserved | A solid square in one item's `Icon` leaves the column's width and every other label's x unchanged — rendered evidence, one frame per variant |
| Every item carries an automation name | The a11y sweep, extended to the menu; `AccessibilityCoverageTests` already fails an unnamed `MenuItem` in XAML, and these are built in code |

## Risks

| Risk | Assessment |
|---|---|
| **A right-click that moves the caret destroys the selection the menu offers to copy** | The one that would make the headline feature not work, and it is the default behaviour of a text editor. The test is written before the handler |
| ~~`TextEditor` or `TextArea` brings a context menu of its own~~ | **Answered while writing this: it does not.** `ContextMenu` appears nowhere in AvaloniaEdit's source or themes, so there is nothing to compete with. Recorded because the question was worth asking before phase 1, not after |
| The context is a snapshot and the model rebuilds under it | Deliberate, and stated. A live view would renumber under the reader's pointer. A host acting on a stale context gets what a stale context describes, which is why the *commands* re-read the selection rather than taking it from the event — plan 00006's rule, unchanged |
| The item list is the thing everyone will want to argue about | The list is v1 and the seam is forever. Both extensibility shapes exist precisely so that a disagreement about the list is a host's three lines rather than a plan |
| Scope creep into the margins | The non-goal is explicit and `Region` is the enum that makes the later addition a case rather than a redesign |
| The unified view drifting again | `AGENTS.md` §7's standing complaint. The absent-vs-disabled rule reads `CommandOrNull`, which is already the single source for both views — the same consolidation plan 00009 phase 2 made |

## Conventions

Conventional Commits, dense bodies, no AI attribution trailer. Every user-visible string through
`DiffViewStrings`, every log line through `DiffViewLog`. New tests are proven able to fail before
they are committed. An approved plan is committed before implementation and never edited; drift
goes to `DECISIONS.md`.
