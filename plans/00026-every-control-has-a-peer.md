# 00026 — Every control has a peer a harness can find

> Status: **approved 2026-09-30**. Supersedes nothing.

A harness cannot find this library's controls. Measured on 2026-09-29, headless, over the code on
`main` at `c187541`, which is what `main` still holds under `src`: every control DiffView ships gets
Avalonia's `NoneAutomationPeer`, which is not a control element, so a search of the control view
finds none of them — not by name, not at all. None of them honours a pattern, and none carries an
`AutomationId`. The two framework controls measured beside them answer as a harness expects.

| Measured | Peer | Control element | Control type | Patterns |
|---|---|---|---|---|
| `SideBySideDiffView`, `DiffViewer`, `InlineDiffView` | `NoneAutomationPeer` | no | `None` | none |
| `DiffPanePresenter`, `DiffPaneHeader`, `DiffFindBar`, `DiffStatusStrip` | `NoneAutomationPeer` | no | `None` | none |
| `DiffMinimap`, `ChangeConnectorGutter` | `NoneAutomationPeer` | no | `None` | none |
| the pane's two margins, `DiffLineNumberMargin` and `ChangeMarkerMargin` | `NoneAutomationPeer` | no | `None` | none |
| `Button`, for contrast | `ButtonAutomationPeer` | yes | `Button` | `Invoke` |
| `TextBox`, for contrast | `TextBoxAutomationPeer` | yes | `Edit` | `Value` |

The find bar and the two margins already carry names — *Find*, *Line numbers*, *Change markers* — that
no control-view search reaches. `BNXQ1006`, XamlQuality's check that a themed control gets a peer,
reports the same thing at the pinned `2026.3.928` when its scan is given the library's assembly: 7
inspected, 7 findings, one for every control the library themes.

## Who it is for

**Plan 00023's Windows back end, and what that back end does decides what this plan builds.** It finds
elements through `System.Windows.Automation`, acts on them with `SendInput` and captures them with
`PrintWindow` (plan 00023, *The seam* and phase 4). What it needs from this library is that every part
be a **control element**, found by a **stable handle that no culture changes**, with **true bounds**,
and **absent when hidden**. That is this plan. The back end reads no pattern, so this plan advertises
none (Brian, 2026-09-29); *Deferred* has what a harness that drives through patterns would still need,
and what each needs to be right.

**The handle is an `AutomationId`, not a name.** Plan 00023 finds by automation name, on the premise
that the demo sets the names. It sets none of the library's: `DiffBuildController.RefreshStrings` fills
them from `DiffViewStrings`, which is translated into eight locales, so a back end that finds *Left
pane* in `en-US` finds nothing in `de-DE`. Plan 00023 is approved and is not edited. `DECISIONS.md`
records that its back end finds DiffView's parts by `AutomationId`, scoped first by the process and
then by the part that owns them.

**Every id is explicit** (Brian, 2026-09-29). Once a control has a peer, Avalonia derives an id from
its `Name` when none is set (`ControlAutomationPeer.GetAutomationIdCore`, 12.1.2), so the panes would
answer to `PART_LeftPane` after the first two phases alone. Rule 1 is why the plan does not rest on
that: renaming a template part would rename what every harness searches for. The margins and the menu
entries have no `Name` to derive from in any case.

Screen-reader quality is why the criteria exist; it is not the gate, and nothing here tests a screen
reader.

## The rules it applies

From XamlQuality's `docs/ai-drivable-ui.md`, the living copy (`AGENTS.md` §8):

- **A custom control gets a peer** (rule 3), and custom-drawn is not decorative (rule 6's note): the
  overview map, the connector gutter and the number margin are the surfaces a person acts on most.
  `BNXQ1006` checks it for the controls the markup themes.
- **Every control a test or a user needs to reach has an explicit `AutomationId`** (rule 1), not one a
  framework derives from `x:Name`, and no harness finds a control by its visible text. `BNXQ1007`
  checks it for the controls it knows and the ones it is given.
- **Never advertise a pattern the peer does not honour** (rule 4). These peers advertise none.
- **What is not on screen is not in the tree** (rule 6). The toggles and the find bar hide through
  `IsVisible`, and Avalonia 12.1.2 leaves an invisible control out of its parent's peer children and
  reads them again when `IsVisible` changes (`ControlAutomationPeer.GetChildrenCore`). `BNXQ1008`
  checks that a control in a slot of no size is hidden that way too.
- **Scope every search to the process, then to the container that owns the part** (§5, steps 3 and 4).
  That is the scope each id is unique within.

## The peers

| Control | Control type | Name |
|---|---|---|
| `SideBySideDiffView`, `DiffViewer`, `InlineDiffView` | `Group` | the host's `AutomationProperties.Name` |
| `DiffPanePresenter` | `Edit` | the pane's own: `LeftPaneName`, `RightPaneName`, `PaneName` |
| `DiffPaneHeader` | `Header` | `LeftHeaderName`, `RightHeaderName` |
| `DiffFindBar` | `ToolBar` | *Find* |
| `DiffStatusStrip` | `StatusBar` | `StatusStripName` |
| `DiffMinimap` | `ScrollBar` | `MinimapName` |
| `ChangeConnectorGutter` | `Custom` | `GutterName` |
| `DiffLineNumberMargin`, `ChangeMarkerMargin` | `Custom` | *Line numbers*, *Change markers* |

UIA's control-pattern mapping lists no required pattern for any of these types, so no peer fails the
specification. No peer advertises a pattern. The pane's peer answers `HasKeyboardFocus`,
`IsKeyboardFocusable` and `SetFocus` for its text area, where the keyboard focus lives: Avalonia's
`ControlAutomationPeer` reads its owner's own `IsFocused`, which is false while the pane is being typed
into. Every peer is `internal`, in a file beside its control, and reached through an
`OnCreateAutomationPeer` override — the one place `BNXQ1006` reads.

The pane's template holds its margins in an `ItemsControl`, whose peer is an unnamed `List`. The
library owns that template, so the host is marked `AutomationProperties.AccessibilityView="Raw"`, and a
margin's parent in the control view is its pane.

## Identities

| Part | `AutomationId` | Unique within |
|---|---|---|
| a view | the host's — the demo's three are `SideBySide`, `Unified` and `Viewer` | the window |
| the panes | `LeftPane`, `RightPane`; `Pane` in the unified view | the view |
| the headers | `LeftHeader`, `RightHeader` | the view |
| the connector gutter, the overview map, the status strip, the find bar | `Gutter`, `Minimap`, `StatusStrip`, `FindBar` | the view |
| the banner's action, the strip's dismiss button | `BannerAction`, `StatusDismiss` | the view |
| the find bar's eleven controls | `FindQuery`, `FindMatchCase`, `FindWholeWord`, `FindRegex`, `FindChangedRowsOnly`, `FindScopeLeft`, `FindScopeRight`, `FindScopeBoth`, `FindPrevious`, `FindNext`, `FindClose` | the view |
| a pane's two margins | `LineNumbers`, `ChangeMarkers` | the pane |
| an entry the library puts in a context menu | the name of its `DiffCommand`; the four entries with none are `Save`, `Revert`, `GoToRow` and `HideMinimap` | the menu |

The templates declare every id they place. Code declares the rest: the margins, which the presenter
builds in its constructor, and the menu entries. `DiffMenuItem` gains a public `AutomationId`, which
defaults to the name of the entry's `Verb`; the builders give the four entries that have no verb —
save and revert, which are the whole of a header's menu, and the map's go-to-row and hide entries —
their ids; `DiffPaneMenu.Build` puts it on the `MenuItem`; and a host that adds an entry of its own sets
it there too. A context menu is its own top-level window, found from the desktop root together with
the process id (§5, step 3).

## Decisions

| Decision | Why |
|---|---|
| A peer for all eleven controls, the two custom-drawn surfaces and the two margins included, not only the seven `BNXQ1006` inspects | Rule 3, and rule 6's note: the controls it cannot see — they have no theme — are among the ones a person acts on most |
| The control types in the table, each one UIA's mapping requires no pattern of (Brian, 2026-09-29) | The consumer reads no control type, and a type whose required pattern the peer lacks is a defect any checker reports. `Document` and `Thumb` say more about the pane and the gutter and fail the specification: a `Document` must support `Text` and a `Thumb` must support `Transform`, and Avalonia 12.1.2 provides neither — its providers are `Invoke`, `Toggle`, `Value`, `RangeValue`, `Scroll`, `Selection`, `SelectionItem` and `ExpandCollapse`, measured |
| No patterns (Brian, 2026-09-29) | The one consumer reads none, and rule 4 is kept by advertising nothing. *Deferred* has each pattern and what it needs |
| The pane's peer answers focus for its text area | `ControlAutomationPeer.HasKeyboardFocusCore` is `Owner.IsFocused` in 12.1.2; AvaloniaEdit 12.0.0's `TextEditor` is not focusable, and its text area is what takes the focus |
| An explicit `AutomationId` on every part, unique within the part that owns it (Brian, 2026-09-29) | Rule 1; the names are translated, and a derived id moves when a part is renamed. The owner's scope is the guide's §5 step 4, and it is what lets both panes' margins carry the same two ids. `BNXQ1007` checks that an id is present and not that it is unique, by its own account, so the walk asserts uniqueness |
| `DiffMenuItem.AutomationId`, public, defaulting to the verb's name | Menu entries are built in code, named by translated text and invisible to `BNXQ1007`. The verb is already on most entries and is public API, so the id is exactly as stable as the enum; four entries have no verb, and a host's own entries need a way to carry one. A new public member regenerates the API fixtures it reaches |
| `BNXQ1006` adopted, gated as `BNXQ1003` is | Plan 00025's standard: a floor, the findings, the precise `Skipped` guard, and a mutation that blinds each. Its scan is given the library's assembly, as `TemplatePartTests` gives `BNXQ1003`'s: without it every themed control goes to `Skipped` and the rule reads clean — measured, 0 inspected, 0 findings, 8 skipped |
| `BNXQ1007` adopted over the library's markup, with a derivation of its own | The derivation `AccessibilityCoverageTests` gives `BNXQ1002` covers the library and the demo together, and over the library alone four of its names would cover nothing, the three views and `Menu` being placed only by the demo. So this gate is given every concrete control type the library defines: each one the library's markup instantiates must cover an element there, and the rest — the views, which only a host places, and the margins, which code builds — must cover none, so that one starting to appear moves across. It carries no `Expander` forward cover and no `Skipped` guard, because the rule adds `Expander` itself and has no skip path: neither guard could fail. Measured with the stock names and the derived controls: 35 inspected, 35 findings. Not the demo's markup, whose 57 are its menu bar, 53 menu items and three views |
| `BNXQ1008` adopted over the library's markup, beside `BNXQ1004` in `GridSlotTests` (Brian, 2026-09-29) | Rule 6's static half, and by XamlQuality's account the other half of `BNXQ1004`. Measured: 5 inspected — the controls whose slot it can size — and 0 findings; a control put in a slot of no size without being hidden becomes one. It has a skip path, so it carries the `Skipped` guard |
| `BNXQ1009` not adopted | Nothing here for it to read: 0 inspected over the library, whose one `ItemsSource` is the text area's margin host, which it skips; 0 over the demo |
| The walk's requirements are derived, not listed | Plan 00025's standard for its walk, which takes its reach from the markup. The controls it must reach are every concrete control type the library defines, internal ones included — eleven, measured by reflection, and `GetExportedTypes` would miss the two margins — with the themed ones equal in number to what `BNXQ1006` inspects. The ids it must find are the ones the markup declares and the ones code declares; the menu entries it checks are whatever each surface's builder produced |
| One walk: plan 00025's runtime walk in `AccessibilityCoverageTests`, extended | Its reach requirement stands; a second walk would be a second thing to keep reaching |
| The demo's three views get ids, asserted in `DemoViewTests` | The demo is the host plan 00023's back end drives, and `DemoViewTests` already builds its window; the rest of its chrome stays as it is |

## Deferred

A harness that drives through patterns — the guide prefers it to input (§4) — already has the framework
controls inside the find bar, the banner and the strip, which carry peers of their own: the query box's
`Value`, each toggle's `Toggle`, each button's `Invoke`. Of the library's own parts it has nothing.
Each pane's own `ScrollViewer` honours `Scroll`, but it is a template part, which Avalonia keeps out of
the control view (`ScrollViewerAutomationPeer.IsControlElementCore`, 12.1.2). Avalonia's menu item
honours `Toggle` and not `Invoke` (`MenuItemAutomationPeer`, 12.1.2), so a menu entry is acted on by
input at the bounds its id finds. What follows is a later plan's, when a harness drives through
patterns; each entry says what it needs.

| Deferred | What it needs |
|---|---|
| The pane's `Value`: its text, and on an editable pane `SetValue` as one undoable edit | A harness that reads or sets a pane's text. An `Edit` carries `Value` where it has one |
| The pane's `Scroll` | To delegate to the pane's own `ScrollViewer`, which a control-view search cannot reach |
| The map's `RangeValue` | Rows as its unit, because a row is what a drag moves: `DiffMinimap.ScrollTo` raises `JumpRequested` with a model row. An offset in pixels would be rounded away on every set |
| The gutter's `RangeValue`, over `SplitRatio` | The range a drag allows, and a value that moves with a drag in both directions |
| The view's `ItemStatus`, carrying its `State` | To be set as the attached property, the one change `ControlAutomationPeer` raises an event for, and to carry the untranslated `DiffViewState` name |
| `ShowContextMenu` | An override on every surface that opens a menu — the pane, the header, the gutter, the map and both margins — since Avalonia's default opens the nearest ancestor's `ContextMenu`, which is a host's. And a COM client: `System.Windows.Automation` cannot call it; only `IUIAutomationElement3` can |
| `LiveSetting` on the strip | To sit on the element whose name is the message. Avalonia's Windows layer announces a live region only when a peer's name changes, and the strip's name never does. Screen-reader work, besides |

## Dismissed

- **Peers for the seven `BNXQ1006` names and no more.** It would leave the map, the gutter and the
  margins — the drawn surfaces a person drives — as unreachable as they are today.
- **Per-element children for what is drawn** — a copy arrow, a connector polygon, a map lane as an
  element of its own. A later plan's. This one makes their parents findable, and a harness acts on
  them by position within those bounds, or through the key or menu entry each already has.
- **`Document` for the pane and `Thumb` for the gutter.** Each says what its control is more exactly —
  UIA's own guidance calls a separator that can be moved a `Thumb` — and each fails the specification
  on a pattern Avalonia 12 cannot provide. The consumer reads neither.
- **A text pattern for the pane.** Avalonia 12 has no text provider.
- **`OverlayPopups` set in the demo** (rule 10). Plan 00023's X11 driver finds a popup as its own
  top-level window, a UI Automation harness finds one from the root together with the process id
  (§5, step 3), and the library cannot choose for a host. The hosting guide says what a host driving
  with UI Automation gains from setting it.
- **A Windows job in CI that drives the peers through UI Automation.** It would read the projection
  before plan 00023's owed run, but it is that harness run in CI, which plan 00023 keeps out of CI; the
  projection is read from Avalonia's source instead (*Where the walk stops*).
- **Driving the peers over AT-SPI on Linux.** Avalonia 12 publishes them there — XamlQuality's guide
  reads it from the source and has not verified it — and plan 00023 leaves AT-SPI out.

## Where the walk stops

The walk reads Avalonia's managed peers, headless. A harness on Windows reads Avalonia's projection of
them into UI Automation, which cannot run here. Read at 12.1.2
(`src/Windows/Avalonia.Win32.Automation/AutomationNode.cs`), the projection carries through everything
this plan relies on: whether an element is a control element, its control type, its `AutomationId`,
its name, its bounds and its keyboard focus. So for these the walk is a faithful proxy. "No pattern" is
a statement about the managed peers: on Windows every element also answers `ScrollItem`, which the
projection honours itself. The first reading at the UI Automation layer is plan 00023's owed Windows
run.

## Scope

**In.** The peers and their `OnCreateAutomationPeer` overrides; the pane's focus answers; the margin
host marked raw; `AutomationId`s on every part, in the templates and in the code that builds the
margins and the menu entries, and on the demo's three views; `DiffMenuItem.AutomationId`, with the API
fixtures it reaches; `BNXQ1006`, `BNXQ1007` and `BNXQ1008` adopted in the XAML gates, with their
mutations in `scripts/mutate-gates.cs`; plan 00025's walk, extended and derived; the hosting guide's
section on automation; `AGENTS.md`'s contracts; `PROGRESS.md` (open item 0b closes), `DECISIONS.md`
(the change to plan 00023's back end among it), `CHANGELOG.md`.

**Out.** Every pattern (*Deferred*). Screen-reader quality. AT-SPI. The harness that uses these — plan
00023's phase 4. Elements for what is drawn inside a control. `OverlayPopups` in the demo. The demo's
chrome beyond its three views' ids.

## Phases

| Phase | Size | What | Verified by |
|---|---|---|---|
| 1 The themed controls, and `BNXQ1006` | S | Peers for the three views, the pane, the header, the find bar and the status strip, with their control types and names; the pane's focus answers. `BNXQ1006` gated, its scan given the library's assembly | `BNXQ1006`: 7 inspected, 0 findings, nothing skipped. The walk, its requirement derived, finds each themed control as a control element of its type with no pattern — as many as `BNXQ1006` inspects — and a hit-test at the centre of each one's bounding rectangle lands into that control. `SetFocus` on a pane puts the keyboard in its text area, the pane reports having it, and a key reaches it. Every new guard tripped first by its own mutation |
| 2 The drawn surfaces | S | Peers for the map, the gutter and the two margins; the margin host marked raw | The walk over every concrete control type the library defines: each a control element of its type with no pattern, the centre of its bounding rectangle hit-testing into it, and each margin's parent in the control view its pane |
| 3 Identities, `BNXQ1007` and `BNXQ1008` | M | An `AutomationId` on every part in *Identities*, in markup and in code; `DiffMenuItem.AutomationId`; the demo's three views. `BNXQ1007` and `BNXQ1008` gated. The `DECISIONS.md` entry moving plan 00023's back end to ids | `BNXQ1007`: 0 findings, each control the library's markup instantiates covering an element and the rest none, the stock names' reading over a floor under today's 15. `BNXQ1008`: 0 findings over a floor under today's 5, nothing skipped. The walk finds every id the markup and code declare exactly once within its owner, in `en-US` and `de-DE`; every entry of every surface's menu carries an id, unique within its menu and found by it in `de-DE`; a hidden part — the map switched off, the headers off, the strip off, the find bar closed — is not found at all. `DemoViewTests` finds the demo's three views by their ids. The API fixtures carry `DiffMenuItem.AutomationId` |
| 4 The record | S | `AGENTS.md`, the hosting guide, `PROGRESS.md`, `DECISIONS.md`, `CHANGELOG.md` | A full `scripts/mutate-gates.sh`; the suite in `en-US` and `de-DE` under `catch-crash --expect auto`; the trimmed publish without an `IL` warning; CI green on all five jobs |

## Risks

- **The pane is AvaloniaEdit's `TextEditor`**, whose `TextArea` owns the focus and the keyboard. A peer
  must not change which element takes the focus or what a key does; the key-map and focus tests are the
  check. UI Automation's focused element stays the text area, and AvaloniaEdit 12.0.0 declares no
  `OnCreateAutomationPeer` on its `TextArea` or its `TextEditor`, so that element is not a control
  element: a harness asks a pane whether it has the focus, not which element does.
- **A scan that is not given the assembly reads clean.** `BNXQ1006` skips every themed control without
  it. Its `Skipped` guard is the assertion that fails, and a mutation that drops the assembly proves it.
- **A guard that cannot fail is not a guard.** `BNXQ1007`'s gate leaves out the two guards its rule
  makes vacuous, and each guard it does carry gets a mutation that trips it first; a full
  `scripts/mutate-gates.sh` fails any that none reaches.
- **Hidden means absent only while no peer of ours overrides `GetChildrenCore`**, which is where
  Avalonia leaves an invisible child out. None does, and the walk asserts the absence rather than
  trusting it.

## Conventions

Conventional Commits, dense bodies, no AI attribution trailer. Every new test proven able to fail
before it is committed, and every new gate guard given its blinding mutation in the same change
(`AGENTS.md` §5). An approved plan is committed alone before implementation and never edited; drift
goes to `DECISIONS.md`. Work on its own branch from `main`; pushed only when Brian says.
