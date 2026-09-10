# 00009 — Key bindings a host can change

Nine `KeyBinding`s are built in `SideBySideDiffView`'s constructor, each pairing a `KeyGesture`
with a private `DelegateCommand`. `InlineDiffView` builds six more the same way. A host can clear
the collection — `AGENTS.md` §6 says so — but it cannot *rebind* anything without rebuilding all
of them, because nothing names a command and nothing hands out its gesture.

This is also upstream of the context menu. A menu that prints `Alt+Left` beside "copy this change"
has to read that text from wherever the binding lives, or the label becomes a lie the first time
someone rebinds. So the table comes first and the menu is built on it.

## Goal

A public, named command table with a gesture per command. A host can rebind one, unbind one, or
bind one that has no default — in code, on the control. `GestureFor` answers what a command is
bound to, which is what plan 00010's menu will print.

## Non-goals

| Excluded | Note |
|---|---|
| A binding file format | Settled: rebind and clear, in code. A schema brings versioning, unknown-command policy and conflict rules, and the host is better placed to decide how its own settings persist |
| A keybinding editor control | A whole surface with its own conflict UI and accessibility work. The table is what an editor would need; building one is a later decision |
| Host-defined commands | The table names *this control's* commands. A host with its own command adds its own `KeyBinding` alongside — which this plan must not trample, and §Risks says how |
| A new chord for the selection copy | Settled below by making the existing chord follow the selection rather than by adding a gesture. No new default binding |
| Mouse gestures | The map is keys. The pointer's meanings are decided by what is under it |

## Architecture

### A command is named, and a gesture is a property of the map

`DiffCommand` — an enum naming what the control does: `NextChange`, `PreviousChange`,
`SwitchPane`, `OpenFind`, `FindNext`, `FindPrevious`, `CloseFind`, `CopyToLeft`, `CopyToRight`,
`CopyBlockToLeft`, `CopyBlockToRight`. Closed on purpose: it names this control's own verbs, and a
host's verbs are the host's.

`DiffKeyMap` — a mutable map from `DiffCommand` to `KeyGesture?`, with `DiffKeyMap.Default` as a
static factory so a host can compare against it or restore it. A `null` gesture is *unbound*,
which is how a binding is cleared without removing the command.

`SideBySideDiffView.KeyMap` and `InlineDiffView.KeyMap` each hold one. Assigning a map, or
changing one in place, rebuilds the bindings. `GestureFor(DiffCommand)` is the read side, and the
seam plan 00010 needs.

### Alt+Left now copies what the gutter says it will

Plan 00006 made a selection arrow **take the block arrow's cell** when a selection begins on a
block's anchor row — "a selection is the more specific and the more recent intent" — and in the
same breath kept Alt+Left copying the block regardless. So with a selection up, the gutter showed
one thing and the chord did another. That is the ambiguity, and it was never about which key.

`CopyToLeft` and `CopyToRight` now copy **the selection when there is one, and the current block
otherwise** — the rule the gutter already applies, and the rule Cut, Copy and Delete follow in
every editor. `CopyBlockToLeft` and `CopyBlockToRight` name the block-always behaviour and are
**unbound by default**, so a host that wants it back binds a gesture rather than losing the verb.

This supersedes plan 00006's *"A keyboard path for the selection copy"* non-goal. That plan is
approved and is not edited; `DECISIONS.md` records the change and why, as drift does.

### The bindings the control owns, and the ones it does not

**This is the sharp part.** `KeyBindings` is a public collection on any `InputElement`, so a host
may already have added its own — and rebuilding the collection wholesale would silently delete it.
The control therefore tracks the `KeyBinding` instances it created and replaces **only those**,
leaving anything else in place. A host that clears the whole collection still gets what §6
promises: no bindings until it puts some back.

### Two commands, one gesture

Allowed, and logged once through `DiffViewLog` at warning level naming both commands. Avalonia
decides which fires, and pretending otherwise would mean either refusing a binding a host asked
for or silently dropping one. Neither is better than saying so. `DiffKeyMap` is keyed by command,
so the reverse — one command with two gestures — is not expressible.

### The unified view gets the same mechanism, not a copy of it

`InlineDiffView` has no second side and no pane to switch to, so its default map has no
`SwitchPane`, `CopyToLeft` or `CopyToRight`. That is a different *default*, not a different type:
§7 keeps the two views parallel, and the drift this repository keeps finding is exactly what a
second implementation would produce.

## Phases

| Phase | Size | Content |
|---|---|---|
| **1 — The table** | M | `DiffCommand`, `DiffKeyMap` and `DiffKeyMap.Default`; `SideBySideDiffView.KeyMap` and `GestureFor`; the owned-bindings rebuild; the conflict log line |
| **2 — The unified view, and the demo** | S | `InlineDiffView.KeyMap` with its smaller default; a demo menu item that rebinds something visibly, so the mechanism is exercised by hand and not only by tests |
| **3 — Evidence** | S | The mutations; `DECISIONS.md`, `AGENTS.md`, `PROGRESS.md`, the changelog. No frames: nothing here draws |

## Testing

Per the house rule, every new test is proven able to fail before it is committed. Per `AGENTS.md`
§6, a key test focuses the pane first, because an ancestor's `KeyBindings` fire before the routed
key event and only mark the key handled when the command executes.

| Test | Asserts |
|---|---|
| The default map is the bindings that were hardcoded | All nine, gesture for gesture — a pin, so changing a default is a deliberate act with a failing test attached |
| Rebinding moves the behaviour | The new gesture invokes the command and **the old one no longer does**, which is the half a rebind test usually forgets |
| Unbinding clears it | A `null` gesture leaves the key doing nothing, and the command still invokable in code |
| Binding an unbound command works | A command the default map leaves `null` can be given a gesture |
| A host's own binding survives a rebuild | The control replaces only what it created; a `KeyBinding` the host added is still there afterwards |
| A cleared collection stays cleared | §6's existing promise, still true |
| Two commands on one gesture logs once | Named in the log line, at warning, and neither binding silently dropped |
| `GestureFor` answers the map | Including `null` for unbound — the seam plan 00010 reads |
| The unified view's default is smaller | No `SwitchPane`, no copies, and the rest identical |
| Alt+Left copies the selection when there is one | With lines selected the chord writes those lines, not the block's; with none it writes the block's, exactly as before |
| The gutter and the chord agree | On a selection that begins on a block's anchor row — the contested cell of plan 00006 — the arrow drawn and the chord fired do the same thing. This is the assertion the change exists for |
| `CopyBlockToLeft` is the block always | Bound to a gesture in the test, it copies the block **while a selection is active**, which is the behaviour the default no longer has |

## Risks

| Risk | Assessment |
|---|---|
| **Rebuilding `KeyBindings` deletes a host's own** | The one that would be found by a consumer rather than by us. Tracking the instances the control created is the whole fix, and a test asserts a host's binding survives — written before the rebuild code, so it fails first |
| **Alt+Left changes meaning for anyone relying on it** | It is a behaviour change, not an addition, and the only one in this plan. It is also the one that makes the keyboard agree with what the gutter has been showing since plan 00006, and the old behaviour keeps a name — `CopyBlockToLeft` — rather than being removed. Existing tests that copy a block with no selection are unaffected by construction |
| A rebind that lands on a gesture AvaloniaEdit already uses inside the pane | The pane's own editing bindings are AvaloniaEdit's and outside this table. A host that binds `Ctrl+C` to `NextChange` gets what it asked for; the log line is the warning, not a veto |
| The map is mutable, so a change in place must be noticed | `DiffKeyMap` raises when an entry changes and the control rebuilds on it. A plain dictionary would need the host to reassign, which is a trap |
| Scope creep into a settings surface | The non-goals are explicit. If persistence is wanted later it reads and writes `DiffKeyMap`, which is why the type is public and separable |

## Conventions

Conventional Commits, dense bodies, no AI attribution trailer. Every user-visible string through
`DiffViewStrings`, every log line through `DiffViewLog`. New tests are proven able to fail before
they are committed.
