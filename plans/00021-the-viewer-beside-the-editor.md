# 00021 — The viewer beside the editor

> Status: **approved 2026-09-17**. Supersedes nothing.
> Fourth draft, **2026-09-17**. First argued; second written from a measurement spike, which
> proposed a public base class; third abandoned that for siblings after the drift measurement; this
> one replaces the sharing mechanism after a second adversarial review found the extension block
> mis-costed by roughly 2×. Every count names the tool that produced it.

`SideBySideDiffView` already ships read-only: `LeftReadOnly` and `RightReadOnly` both default to
`true`, and that hides the copy arrows in the line-number margins. It is not a viewer. The context
menus still open on six surfaces with the copy, save and revert entries **greyed rather than
absent**; Ctrl+F still opens the find bar; F7, F6 and the whole `DiffKeyMap` are still bound.

The greying is deliberate and correct where it stands — *"whether the pointer is in a change is
state, and 00010's shape rule is that a host's 'insert after this item' means the same thing on
every open"* (`SideBySideDiffView.cs:2125`). Hiding is reserved for a verb **the view does not
have**, which is why `InlineDiffView` shows no copy items rather than four disabled ones. So the way
to a viewer is not a flag that greys more things out. It is a control that genuinely does not have
the verbs.

**What already works today, and is not a reason to skip this plan.** Every one of the six context
menus can be suppressed per instance, now, with no new code: `DiffPaneContextMenuEventArgs.Cancel`
is *"set to suppress the menu entirely for this click"*, an emptied `Items` opens nothing, and
`PaneContextMenuOpening` plus `HeaderContextMenuOpening` between them cover all six surfaces. A host
that only wants the menus gone needs two handlers and this plan buys them nothing. What they cannot
turn off is the find bar, the key map, the copy arrows' plumbing, or the verbs on the type.

## What is being asked for

A control you scroll and look at. No find bar, no context menus, no copy arrows, no save or revert,
no key bindings beyond what a text pane does with an arrow key. The chrome stays, **each piece
independently switchable**, and the overview map stays **interactive** — clicking it jumps.

**Plan 00022 already delivered the switches.** `ShowMinimap`, `ShowHeaders`, `ShowStatusStrip` and
`ShowBanner` all exist on both views, each on by default. This plan adds no toggle.

## Two shapes rejected before this one

**Derivation cannot narrow.** A `ReadOnlyDiffView : SideBySideDiffView` would inherit `Save`,
`Revert`, `CopyBlock`, `LeftReadOnly`, `OpenFind`, `KeyMap` and the rest; each would be inert or
throwing, and a base-typed reference reaches all of them.

**So the second draft made the viewer the base, with the editor deriving from it.** That followed
only if two standalone siblings were unaffordable, and the reason given was that *"two copies of the
build orchestration drift"*. Measured 2026-09-17: **false here.** `InlineDiffView` has been a
standalone copy since `cec26cb` (2026-09-07); of the 27 commits touching either file since, **13
touched both, every one in a single commit, and not one touched `InlineDiffView` alone.** The 14
that touched only `SideBySideDiffView` are editing and two-pane features the unified view lacks;
`cbd2875`, a genuine shared-orchestration bug fix, landed in both at once. Meanwhile the base class
would have made **194 members permanent v1 API** on a type no consumer had used, and required an
irreversible first phase.

So: **two unrelated sibling controls, sharing their implementation internally.**

## How they share it

The third draft shared through an **internal interface plus a C# 14 extension block**. A second
review measured that and it does not hold:

| Claim | Measured |
|---|---|
| "roughly 50 state members on the interface" | **~90.** An extension block declares no state, so all 34 unwrapped shared private fields must be named on it, plus ~44 CLR properties the shared half reads |
| the block can run the shared orchestration | **Not all of it.** An extension method over an interface cannot reach `protected` members of the implementing class. `UpdatePseudoClasses` (`:2879-2895`) is nine `PseudoClasses.Set` calls and nothing else; `SetCurrentChange` calls `SetAndRaise(…, ref _currentChangeIndex, …)`, and **no interface property can be passed by `ref`** |
| the block can raise the events | **No.** CS0079 — an event is invocable only inside its declaring type. `BuildCompleted` (`:2698`), `BuildFailed` (`:2710`) and `RenderFault` (`:4245`) are all raised from shared code |
| "the extension block leaves the orchestration bodies almost untouched" | **Backwards.** Method calls on `this` survive; field access does not, and 46 distinct private fields are touched by just 772 lines of the shared half |

**The shape is an internal controller plus a narrow internal interface.**

| Piece | What it holds |
|---|---|
| `DiffBuildController` — **internal, not a control** | The shared **state**: the 34 fields — `_leftPane`, `_gutter`, `_minimap`, `_headersGrid`, `_buildCts`, `_slowTimer`, `_generation`, `_projection`, `_expandedFolds` — and the orchestration that uses them. Both controls own one |
| `IDiffSurface` — **internal, ~15 members** | Only what the controller must call back for: the **ten variation points**, the **protected re-exposures** (`PseudoClasses`, `SetAndRaise`, `InvalidateVisual`) and **three raise-methods** for the events |
| `DiffViewer`, `SideBySideDiffView` — **public, unrelated** | Their own templates, their own public API, each implementing `IDiffSurface` explicitly so none of it shows |

**Why this over the extension block**, having got it wrong once in the other direction:

- **The interface drops from ~90 members to ~15**, which is ~150 forwarders never written.
- **Private state stays private.** Those 34 fields are the orchestration's own; in the controller
  they remain private fields of the thing that uses them. On an interface they would become members
  of a type surface — **worse encapsulation than the code has today.**
- **It rewrites less, not more.** A field that *moves into* the controller keeps every reference
  verbatim; only members that stay on the control need qualifying. Under the extension block
  everything qualifies. (The third draft claimed the reverse. It was wrong.)
- **The orchestration becomes unit-testable without a visual tree** — a controller is directly
  constructible, where today the build path is reachable only through `CompositeHost`.

Neither shape escapes the protected-member problem, so that cost is common and discriminates
nothing; both need a back-reference, which under the extension block is spelled `surface`.

## What the measurements say

Five re-runnable tools, all under `~/c/cl/scratch/DiffView/`: `spike/` (the inventory),
`DocSeamProbe/` (the documents), `SurfaceDump/` (the surface gate and the split), `SiblingProbe/`
(the mechanism, 15 checks + the limits pass), `drift-check.sh` (the drift measurement).

### Scale

`SideBySideDiffView` is 4,297 lines. The spike measured the **acting half** — find, editing, menus,
the key map — at **1,512 lines**, which is what **stays**. The **base remainder that moves into the
controller is 2,785 lines.** (The third draft quoted 1,512 as what moves. Wrong half.)

### The ten variation points

| # | Point | The editor | The viewer |
|---|---|---|---|
| 1 | parts attached | wires 5 find-bar and 4 context handlers | nothing |
| 2 | parts detaching | unwires the same 9 | nothing |
| 3 | pane attached | context, copy-out and copy-selection handlers; both `ReadOnly` flags | nothing |
| 4 | model applied | `ApplyFindResult(null)`, `RequestFind()` | nothing |
| 5 | source replaced | the four document handlers | nothing |
| 6 | change set moved | `RaiseNavigationCanExecuteChanged` | same — navigation is on both |
| 7 | dirty decoration | `IsDirty(side)` for header and strip | `null` |
| 8 | strip find text | `FindStripText()` | `null` |
| 9 | pending edit | `IsEdited(side)` | `false` |
| 10 | documents | public `LeftDocument` / `RightDocument` | internal only |

Points 7 and 8 were one point in the second draft and cannot be: one site sets a **`bool`**
(`header.IsDirty`, `:2920`, which also drives `header.DirtyMarker`, `:2921`), and it cannot borrow
point 9 because `IsDirty` (`:1680`) and `IsEdited` (`:1502`) read **different fields** — `_leftDirty`
(`:397`) and `_leftEdited` (`:393`) — which diverge, since `:1732` clears one and not the other.

### Property re-ownership, and what `AddOwner` actually does

53 registrations: 30 `StyledProperty`, 23 `RegisterDirect`. Both controls declare their own CLR
wrappers; the registrations are shared with `AddOwner` **where that means anything**:

- **`StyledProperty.AddOwner<T>()` returns the same instance**, so one `Setter` reaches both. Proven
  by reference equality.
- **`DirectProperty.AddOwner<T>(getter, …)` returns a NEW instance** bound to the new owner's
  delegates. Proven. `==` and `Equals` still hold across the two (shared `Id`), so
  `change.Property == StateProperty` branching in shared code survives — **but only if `AddOwner` is
  used.** A sibling that calls `RegisterDirect` afresh gets a property that compares unequal, and the
  branch silently never runs. `InlineDiffView.cs:148-228` registers 21 direct properties with its own
  `RegisterDirect`, and `grep -rn "AddOwner" src/` returns **zero** — the habit in this repository is
  the one that breaks.
- A direct property **cannot be set from a style at all**: attempting it throws
  *"Cannot set direct property … because the style has an activator"*. So "one `Setter` reaches both"
  was never about those 23, and nothing regresses — they are equally unstyleable today.

### Styling reach

Avalonia has **no interface selector** — `Selectors.Is<T>` is constrained `T : StyledElement`, so
`x.Is<IDiffSurface>()` is CS0311. The third draft concluded a shared style class was therefore the
only way to reach both controls with one selector, and made both constructors add `diff-surface`.

**That was unnecessary.** A comma-union type selector reaches both **with no cooperation from either
control** — measured, neither added a class:

```
Selector="local|DiffViewer, local|SideBySideDiffView"
```

So the style class is dropped: it was an opt-in convention, with a test to maintain, substituting for
something the selector syntax already does as a guarantee. What a base class would have given —
*one name that matches both* — is genuinely absent, and that is the whole residual price.

### The surface gate

`SideBySideDiffView`'s public surface must not change. The measure is the **flattened** dump filtered
to members this library declares (`Public | Instance | Static | FlattenHierarchy`, then
`DeclaringType.Assembly == the library`) — **307 members, before and after.**

Two corrections to the third draft. It called this dump a byte-for-byte regeneration of the spike's
baseline; it is **content-identical but not byte-identical** — the spike's file carries a UTF-8 BOM,
13422 bytes against 13419 — so the gate **normalises the BOM and compares content**, and says so. And
it is a weaker gate than claimed: explicit interface implementations are **private in IL**, so a
`BindingFlags.Public` dump cannot see them. The gate proves nothing leaked to the public surface; it
does not police the refactor. **The 676 tests are what protect phase 1**, and the plan says so
plainly rather than dressing a near-tautology as a safety net.

## Decisions

| Decision | Why |
|---|---|
| **`DiffViewer` and `SideBySideDiffView` are unrelated sibling controls** | Derivation cannot narrow, and a base class would publish 194 permanent v1 members to buy a duplication cost this repo has measurably never paid |
| **They share through an internal controller plus a ~15-member internal interface** | Decided 2026-09-17, replacing the extension block. Keeps 34 private fields private, rewrites less, and makes the orchestration unit-testable without a visual tree |
| **The variation points are interface members** | Extension members are not virtual and a controller cannot guess; either way variation goes through the interface, which keeps it internal |
| **Shared registrations use `AddOwner`, and a gate proves it** | For `StyledProperty` it is the same instance. For `DirectProperty` it is not, but `==` survives — and a fresh `RegisterDirect` breaks equality **silently**, which is the established habit in this file, so it is gated rather than trusted |
| **No shared style class; a comma-union type selector is the documented way to reach both** | Measured to need no cooperation from either control, where the class was opt-in. One less convention and one less test |
| **Navigation is on the viewer: the index, the four methods and the four commands** | `CurrentChangeIndex`'s setter *is* the scroll, and the map and gutter already move it. None touches content. The ask was *no key bindings*, not *no navigation* |
| **All nine pseudo-classes stay, and the hosting guide names them** | A pseudo-class is the **only** way a host styles on control state through a selector — `State` is a property and cannot be selected on. Five of the nine are styled by nothing today and are kept as documented hooks |
| **The demo hosts the viewer** | Decided 2026-09-17, reversing the third draft's exclusion. A View-menu entry and a `--viewer` flag, exactly as `--unified` already does for `InlineDiffView`. A by-hand look is not what makes it correct — but excluding it means **no human ever sees a new public control**, and the owed Windows and macOS runs could never include it |
| **This does NOT gate the first publish** | Decided 2026-09-17. The base-class draft required it, because re-rooting a published type changes the identity consumers' XAML and `ControlTheme`s bind against. With siblings there is no re-rooting: a new control in a minor is purely additive. The release's real gates are the eight locale readers, a remote, and a Trusted Publishing policy — none of them this |
| **`SideBySideDiffView`'s public surface does not change** | Gated at 307, normalised and content-compared |
| **The four chrome toggles are on both; `ShowBanner` keeps its pseudo-class mechanism** | A code write lands at `LocalValue` and outranks for the control's life the style that owns the banner's visibility, so it is a disjunct inside `:banner-none` (`:2886`) |
| **The map is interactive; its context menu is not. The gutter's block click is on both** | `OnGutterBlockClicked` calls `SetCurrentChange(blockIndex, scroll: true)`; there is no copy verb there |
| **The viewer has no find and no `DiffKeyMap`** | The ask says both. AvaloniaEdit's own Ctrl+F is already uninstalled (`AGENTS.md` §1) |
| **`InlineDiffView` is out of scope, but the interface is designed not to exclude it** | Its property set is disjoint from the two-pane controls' — no `ShowMinimap`, `SplitRatio`, `SyncHorizontalScroll`, `FocusedSide`; `PaneName` where these have two; `AttachPane` with no side. A base class could never have fitted it; an interface it implements later **can**. Not done here, and not promised |

### Dismissed

| Alternative | Why not |
|---|---|
| `ReadOnlyDiffView : SideBySideDiffView` | Derivation cannot narrow |
| `DiffViewer` as a public base class | 194 permanent v1 members, an irreversible first phase, 42 re-ownings of which 26 fail silently — to avoid a duplication measured never to have happened. Buys one thing: a style target by type. A comma-union selector recovers most of that |
| An internal interface plus a C# 14 extension block | The third draft's answer. ~90 interface members, cannot reach `protected` members, cannot raise the three events, and rewrites *more* than the controller because every field access qualifies |
| Sharing through extension members with no interface | An extension block declares no state and its members are not virtual. Nothing could vary per control |
| Duplicating the orchestration outright, as `InlineDiffView` does | The honest fallback, supported by the no-drift evidence. Rejected because that evidence measures **two** copies and this would make three, and the controller costs a rewrite rather than a copy |
| A shared `diff-surface` style class | Superseded: a comma-union type selector reaches both with no cooperation |

## Scope

**In.** `DiffBuildController` and `IDiffSurface`; `SideBySideDiffView` refactored onto them with its
public surface unchanged; `DiffViewer`, its theme and its tests; `AddOwner` for the shared
registrations; the demo's View-menu entry and `--viewer` flag; the gates; the hosting guide's viewer
section and its pseudo-class documentation; `DECISIONS.md`, `PROGRESS.md`, `CHANGELOG.md`;
`docs/theme-audit.md` regenerated.

**Out.**

| Excluded | Note |
|---|---|
| `InlineDiffView` | Untouched. The interface is designed not to exclude it later; nothing here makes it implement one |
| `SideBySideDiffView`'s public surface | Nothing removed, renamed or resignatured. Gated at 307 |
| New chrome toggles | Plan 00022 shipped all four |
| New rendering or diff behaviour | The viewer draws what the editor draws. A visual difference for the same sources is a defect |
| A read-only mode on the editable control | The greyed entries stay greyed there |
| The Windows and macOS by-hand runs | Owed since plan 00001 phase 10 and needing those machines. The demo entry this plan adds is what makes the viewer *includable* in them |

## Phases

**Phase 1 is fully reversible** — it changes one control's internals and adds no type.

| Phase | Size | Content |
|---|---|---|
| **1 — The controller** | L | `DiffBuildController` and `IDiffSurface`; the 2,785-line shared half moves into the controller; `SideBySideDiffView` implements the interface explicitly and delegates. **No new control.** The 676 tests are the oracle and must stay green throughout — they test the one control that exists, and it must behave identically. The surface gate is written here (normalised, content-compared, 307) and proves nothing leaked to the public API. Reversible throughout |
| **2 — The viewer** | M | `DiffViewer` implementing `IDiffSurface` with the viewer's answers to the ten points; `Themes/DiffViewer.axaml`; `AutomationProperties.Name` on presenter, gutter and minimap; **`"DiffViewer"` added to `AccessibilityCoverageTests.InteractiveControlElements`**, without which the new control is exempt from the a11y gate; a **second host fixture**, because `CompositeHost.View` is hard-typed `SideBySideDiffView` (`:47`) and no existing coverage transfers. Its own behaviour tests. `docs/theme-audit.md` regenerated |
| **3 — Sharing the registrations, and the demo** | M | `AddOwner` for every property both controls carry, with the equality gate; the theme split that keeps `DiffPaneHeader`, `DiffStatusStrip` **and `DiffFindBar`** from being keyed twice — `DiffFindBar` most of all, since the viewer has no find and must not merge its theme; the demo's View-menu entry and `--viewer` flag |
| **4 — The record** | S | The hosting guide's viewer section under its citation gate: which of the three controls a host reaches for, how far the read-only guarantee goes, the comma-union selector as the documented way to style both, and the nine pseudo-classes as the supported hooks. `DECISIONS.md`, `PROGRESS.md`, `CHANGELOG.md` |

## Testing

Every new test is proven able to fail **by mutation** before it is committed.

| Test | Asserts |
|---|---|
| **`SideBySideDiffView`'s reachable surface is unchanged** | The flattened library-filtered dump equals the committed 307-member baseline, BOM-normalised. Proves nothing leaked to the public API — **not** that the refactor is correct, which the 676 tests carry |
| **`DiffViewer`'s public surface is a subset of its allow-list** | Appendix A, committed as a file. Permanent from v1 |
| **Nothing shared is public** | No member of `IDiffSurface` and no member of `DiffBuildController` appears on either control's public surface; both types are `internal` |
| **Each variation point dispatches per control** | For all ten: the editor's answer and the viewer's answer, against the same controller |
| **The controller runs without a visual tree** | Constructed directly and driven through a build, with no control, no template and no headless app. The thing the extension block could not offer |
| **`AddOwner` equality holds for every shared registration** | For each, the two controls' static fields compare equal. **Catches the silent failure**: a sibling that calls `RegisterDirect` afresh gets a property that compares unequal and whose `OnPropertyChanged` branch never runs, with no error anywhere |
| **One `Setter` on a shared `StyledProperty` reaches both controls** | The styling claim for the 30 that can be styled at all |
| **A comma-union type selector reaches both with no class added** | The documented mechanism, asserted rather than assumed |
| **Every `AGENTS.md` §6 dual-path contract holds on the viewer** | §6 records six the refactor touches — `CanCopyOut` (`:258`), `ShowMinimap` (`:273`), `ShowHeaders`/`ShowStatusStrip` (`:274`), `MinimapPlacement`, `SplitRatio`, `:banner-none` — and **every test holding them binds to `SideBySideDiffView` through `CompositeHost.View`**, so the viewer inherits none of that coverage. Each gets the set-before-the-template-applies case |
| **All three controls set the same nine pseudo-classes** | Shared through the controller, and gated so it stays that way |
| **Every pseudo-class *any* theme styles is styled by *every* theme** | The theme drift gate, worded so it passes today: five of the nine are styled by nothing, and the second draft's version demanded all nine and was red before it was written |
| **Every `PART_` each control looks up exists in its own theme** | Two templates; the viewer's must not inherit the editor's assumptions |
| **The viewer's type does not hand out a `TextDocument`** | No public member of `DiffViewer` returns one |
| **Each toggle hides its part and the layout closes up** | Four on each. **Assert `IsVisible` plus the survivors' arithmetic, never the hidden part's bounds** — Avalonia does not re-arrange an invisible control |
| **The map still jumps, and folding still folds, in the viewer** | Click-to-jump scrolls to the row named; `UnchangedContextRows` collapses runs; a placeholder click gives one back |
| **The viewer opens no menu on any of six surfaces** | Pane text, both margins, connector gutter, map, both headers |
| **No gesture does anything but scroll** | F7, Shift+F7, F6, Ctrl+F, Alt+Left, Alt+Right move no state; an arrow key and PgDn scroll |
| **The viewer and the editor draw the same diff** | By **drawn state, not a snapshot** — `PixelProbe`, `DiffLineNumberMargin.LastColumnRight`, connector extents. `AGENTS.md` §5: at `ChannelTolerance = 8` and `MaxDifferingFraction = 0.005` a frame comparison passes while half a percent of pixels differ, **which is why the demo entry in phase 3 matters** |
| **The 676 existing tests** | Green throughout, and in phase 1 they are the whole oracle |

## Risks

| Risk | Assessment |
|---|---|
| **Phase 1 moves 2,785 lines of working orchestration** | The largest refactor in the repository, on the control everything depends on. Mitigated by its being the only thing phase 1 does, by the 676 tests as a same-behaviour oracle, and by being reversible |
| **The controller needs a back-reference for template parts and protected members** | Real, and paid by any shape — an extension block's is its receiver parameter. Bounded by keeping `IDiffSurface` to the ten points plus the protected re-exposures and three event raises |
| **`AddOwner` has never been used here** | Zero hits in `src/`, and 21 direct properties on `InlineDiffView` show the habit that breaks equality silently. Gated in phase 3, not trusted |
| **Two themes** | All four controls merge the **same** `SideBySideDiffViewTheme` dictionary (`DiffStatusStrip.cs:93`, `DiffFindBar.cs:199`, `DiffPaneHeader.cs:44`, `SideBySideDiffView.cs:420`), and `InlineDiffView.cs:312` already merges a second — so the duplicate-key condition exists today and is reproducible with no new code |
| **`DiffViewer`'s 194 members are permanent from v1** | Its own API, constraining nothing else, and trimmed on 2026-09-17 from a 208-member inventory. Appendix A is the contract |
| **Three controls, two read-only, and a host picks wrong** | `DiffViewer` (two panes, **no find**), `InlineDiffView` (one pane, **with find**), `SideBySideDiffView` (editable). "Read-only, side-by-side, with search" is unreachable — the gap that makes *no find on the viewer* the decision most likely to be revisited |
| **`avares://` and `InternalsVisibleTo` name assemblies** | They do not move and must not be touched — plan 00017's lesson |

## Conventions

Conventional Commits, dense bodies, no AI attribution trailer. New tests are proven able to fail by
mutation before they are committed. An approved plan is committed before implementation and never
edited; drift goes to `DECISIONS.md`. Work on `feat/the-viewer-beside-the-editor`, branched from
`main` at **`bf4b14f`**. **Plan 00023 phase 1 lands first** — `catch-crash` guards the intermittent
test-host abort that reports `Failed!` with `failed: 0`, which during a 2,785-line move is a false
green at the worst moment.

**Cut by member name, not by region.** The spike found the region comments do not map to the split:
cutting the `// ── Find ──` region removed `UpdateHorizontalScrollBars`, `OnPaneRenderFault`,
`OnPaneGotFocus`, `OnPaneLostFocus` and `OnCaretPositionChanged`, which are pane helpers filed under
Find.

## Appendix A — `DiffViewer`'s public surface

**194 reflection members**, trimmed from a 208-member inventory by the decisions of 2026-09-17
below — that is 112 distinct named members: 73 (47 properties, 9 methods, 3 events, 14
constants) plus 39 `…Property` statics. The type's own constructor is **additional** and is not
among the 194, which is why `194 = 208 − 14` holds. Derived by
`~/c/cl/scratch/DiffView/SurfaceDump/` as `SideBySideDiffView`'s 307-member flattened surface minus
the 98 that are find, editing, context menus or the key map, minus the five trimmed here.
**Descriptions are the control's own `<summary>` text, not paraphrase.**

Permanent once published. `InternalsVisibleTo` already names `DiffView.Avalonia.Tests`, so
everything trimmed to internal stays testable.

### Keep — the viewer's API

| Member | What it is |
|---|---|
| `LeftSource` | The left side's input; assigning it replaces the left document and builds |
| `RightSource` | The right side's input; assigning it replaces the right document and builds |
| `State` | The one state the control is in |
| `StateMessage` | What the state means to the user: the empty prompt, the warning, or the failure |
| `ChangeCount` | Change blocks in the model |
| `BuildCompleted` | A build produced a document and the panes show it. **Carries `DiffBuildResult`, which is how a host reaches the document, the diagnostics and the warnings** |
| `BuildFailed` | A build failed; the control is `Failed` |
| `RenderFault` | A pane's decorator threw and disabled itself |
| `IsStale` | Whether the result on screen is about to be replaced by a running build. **Not a duplicate of `State`**: `IsStale = Document is not null` is assigned immediately before `SetState(Building)`, so it distinguishes a first build from a rebuild with a stale result still visible |
| `IsBuildingSlowly` | Whether the running build has passed `SlowBuildThreshold`. **There is no `BuildStarted` event**, so a host cannot time a build itself; this and the above are the only signals one is running long |
| `IgnoreCase` | Letter case does not count as a difference. Changing it rebuilds |
| `IgnoreWhitespace` | Leading and trailing whitespace does not count as a difference. Changing it rebuilds |
| `WordDiff` | The word-level mode. Changing it rebuilds |
| `MaxWordDiffLineLength` | A line longer than this gets no word-level pieces. Changing it rebuilds |
| `ForceAlignment` | Align regardless of similarity — the banner's Force. Changing it rebuilds |
| `ShowWhitespace` | Whether both panes draw spaces and tabs as glyphs. Off by default |
| `ShowLineEndings` | Whether both panes draw a line terminator at the end of each line. Off by default |
| `TabWidth` | Columns a tab advances to in both panes, 4 by default and never below 1 |
| `UseSyntaxHighlighting` | Whether both panes colour their text from a TextMate grammar chosen per side |
| `PaneFontFamily` | The panes' font family, or `null` to leave the family their theme sets |
| `PaneFontSize` | The panes' font size, or the default to leave the size their own theme sets |
| `IsCaretBlinkEnabled` | Whether the focused pane's caret blinks |
| `CaretLine` | The focused pane's caret line, 1-based; 0 without focus |
| `CaretColumn` | The focused pane's caret column, 1-based; 0 without focus |
| `FocusedSide` | The pane with keyboard focus, or `null`. The three above are the **only** access to caret state — the panes are not public — and the panes stay focusable because that is how keyboard scrolling and select-to-copy work |
| `SplitRatio` | The left pane's share of the panes' width, 0.1 to 0.9; a drag on the gutter changes it |
| `SyncHorizontalScroll` | Whether the horizontal offsets are coupled as the vertical ones always are |
| `MinimapPlacement` | Which edge of the panes the overview map is docked against |
| `UnchangedContextRows` | Rows kept either side of every change, the runs between folded behind a placeholder |
| `ShowHeaders` | Whether the pane headers are shown. On by default |
| `ShowStatusStrip` | Whether the status strip is shown. On by default |
| `ShowBanner` | Whether the banner may be shown when there is something to say. On by default |
| `ShowMinimap` | Whether the overview map is shown beside the panes. On by default |
| `BannerKind` | Which banner is shown above the panes |
| `BannerMessage` | The banner's text |
| `BannerActionText` | The banner's action label, or `null` when the banner offers none |
| `RetryCommand` | Rebuilds after a failure |
| `ForceAlignmentCommand` | Sets `ForceAlignment` from the too-different banner |
| `Retry()` | Runs the build again with the current sources and options; the panes' faults are cleared |
| `ForceAlign()` | Aligns the sides regardless of similarity, which rebuilds |
| `CurrentChangeIndex` | The current change block, -1 for none. Setting it clamps, scrolls both panes and outlines |
| `NextChangeCommand` `PreviousChangeCommand` `FirstChangeCommand` `LastChangeCommand` | The four change-walking commands |
| `NextChange()` `PreviousChange()` `FirstChange()` `LastChange()` | The same four as methods; at either end the position holds and the strip says so |
| `GoToChange(int)` | Makes a block the current change and scrolls to it |
| `ScrollToRow(int)` | Scrolls both panes so a row sits at the centre of the viewport |
| `SelectChange(int, DiffSide?)` | Selects a block's lines on one side, or on both where it is `null` |
| `Status` | The transient message lane; its typed helpers are the only way to emit one |
| `LoggerFactory` | Creates the four category loggers when set |
| `LeftPaneName` `RightPaneName` `GutterName` `MinimapName` `StatusStripName` | Automation names for the five interactive parts |
| `SlowBuildThreshold` | How long a build runs before the strip shows progress — what `IsBuildingSlowly` means |
| The 13 `…Part` constants | `LeftPanePart` `RightPanePart` `HeadersPart` `PanesPart` `LeftHeaderPart` `RightHeaderPart` `HeaderLeftSpacerPart` `HeaderRightSpacerPart` `GutterPart` `MinimapPart` `StatusStripPart` `BannerPart` `BannerActionPart`. **All thirteen carry over**: the viewer has two panes, two headers, the spacers that keep headers aligned when the map is docked, the panes grid, the connector gutter, the minimap, the status strip, and the banner with its action button, since `Retry` and `ForceAlign` are both viewer verbs. `FindBarPart` left with find |

### Trimmed to internal — decided 2026-09-17

| Member | What it is | Why |
|---|---|---|
| `Document` | The model the panes render, or `null` before the first build | **A convenience over the event, not an exposure.** `BuildCompleted` carries `DiffBuildResult(SideBySideDocument, DiffDiagnostics, IReadOnlyList<DiffWarning>)`, and those types are already public in the **`DiffView.Core`** package — so trimming this removes a second, pollable path to data the control already raises an event for, not the data itself |
| `Diagnostics` | What the last successful build measured | Same: reachable as `Result.Diagnostics` |
| `Warnings` | The last successful build's warnings | Same: reachable as `Result.Warnings`. A host wanting them on demand keeps the last result in one field |
| `WordDiffLookup` | The word-level lookup of the current model, bound to the options its build ran under | Genuine internals — keyed to a single build's options, and not carried by the result |
| `TimeProvider` | The clock behind the transient messages' auto-clear and the slow-build threshold | A test seam with a "set it before the first build" ordering constraint, public and settable. A host has no reason to swap the clock, and this is the only one of the five unreachable any other way |

**An earlier draft of this appendix justified the first two as internals leaks. That was wrong** —
`SideBySideDocument` and `DiffDiagnostics` are published by `DiffView.Core` and reachable through
`BuildCompleted` regardless. The trims stand on the narrower ground above.

### What is the editor's alone

98 members: **find** (16) `CloseFind` `CloseFindCommand` `CurrentFindMatchIndex` `FindBarPart`
`FindCompleted` `FindDebounce` `FindNext` `FindNextCommand` `FindOptions` `FindPrevious`
`FindPreviousCommand` `FindQuery` `FindResult` `IsFindBarOpen` `OpenFind` `OpenFindCommand`;
**editing** (23) `CanCopyBlock` `CanCopySelection` `CanCopyToward` `CanSave` `CopyBlock`
`CopyCurrentBlock` `CopySelection` `CopyToLeftCommand` `CopyToRightCommand` `CopyToward` `IsDirty`
`IsEdited` `LeftDocument` `LeftReadOnly` `LiveReDiff` `ModifiedLines` `ReDiffDebounce` `ReDiffDelay`
`ReDiffNow` `Revert` `RightDocument` `RightReadOnly` `Save`; **context menus** (5) `HeaderContextAt`
`HeaderContextMenu` `HeaderContextMenuOpening` `PaneContextMenu` `PaneContextMenuOpening`;
**the key map** (5) `CommandFor` `GestureFor` `KeyMap` `SwitchPane` `SwitchPaneCommand`.

**The arithmetic.** A named member costs more than one reflection line — a property brings its
accessors and, where it is an `AvaloniaProperty`, its static field. The five trims remove **14**
lines: 208 → **194**. (`WordDiffLookup` and `TimeProvider` carry no `…Property` field, being plain
CLR properties; `TimeProvider` is the only one of the five with a public setter.)
