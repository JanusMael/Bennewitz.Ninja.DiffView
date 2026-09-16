# 00022 — Three pieces of chrome you can turn off

> Status: **approved 2026-09-16**. Supersedes nothing.

`ShowMinimap` already lets a host switch one piece of chrome off. The pane headers, the status strip
and the banner cannot be switched at all. This adds `ShowHeaders`, `ShowStatusStrip` and
`ShowBanner`, on both views.

This is its own plan rather than a phase of the viewer plan because these three were chosen to land
whatever the viewer's spike finds. They land on the **editable** controls, which is where the code
is — that was a deliberate call, not a request from a host, and it is worth reading as such: three
properties × two controls is six permanent public members added before the first publish.

## Written after the probe, not before it

The first draft of this plan said all three toggles could *"follow `ShowMinimap` exactly"*. A review
and then a probe showed one of them cannot, in a way no amount of reading the source would reveal.
The measurements are below because they are the plan.

**Only the banner's visibility is style-driven.** `PART_Headers` and `PART_StatusStrip` have no
`IsVisible` setter in any style in either theme; `PART_Banner` has one per banner kind, driven from
`UpdatePseudoClasses`.

**So `IsVisible` from code is safe for two of the three and permanently wrong for the banner.** A
code write lands at `LocalValue`, which outranks `StyleTrigger` for the life of the control — so a
banner hidden and then shown again never returns to style control, and the next build with nothing to
say leaves an empty strip on screen forever. That is not a glitch to work around; it is the mechanism
refusing the instrument.

**The pseudo-class route works and is smaller.** Measured on a real banner, at 1000×700:

| Step | Banner | Panes height |
|---|---|---|
| Identical sources, banner showing | visible | 525 |
| `:banner-none` set | hidden | **554** |
| `:banner-none` cleared | visible again | **525** |

One added disjunct in `UpdatePseudoClasses`, no new template part, no `axaml` edit, and **no
two-path problem** — a pseudo-class lives on the control, not on a part, so a host setting it before
the template applies is already correct.

**Headers and the strip give their space back and take it again.** Identical on both views:

| Step | Part | Panes height |
|---|---|---|
| baseline | visible | 555 |
| headers off | hidden | **577** |
| strip off as well | hidden | **600** |
| both back on | visible | **555** |

**A hidden part's own bounds go stale.** Measured: headers report height 22, the strip 23 and the
banner 29 *while hidden*, because Avalonia does not re-arrange an invisible control. **A test that
asserts a hidden part's bounds passes for a correct implementation and a broken one alike.** The
assertion has to be `IsVisible` on the part plus arithmetic on the survivors — which is what
`MinimapLaneTests` already does for the map.

**`InlineDiffView` names `PART_Headers` in its theme and never looks it up.** Its
`OnApplyTemplate` finds the two headers and the strip but no headers grid. Hiding the grid is also
what sidesteps the unnamed 1-px divider inside it, which is `IsVisible` true and unreachable from
code: with the grid hidden the row collapses regardless, and with only the two headers hidden it
would not.

## Decisions

| Decision | Why |
|---|---|
| **Three `StyledProperty<bool>`, each defaulting `true`** | The default is what the control does today, so nothing moves and no committed snapshot changes |
| **`ShowHeaders` and `ShowStatusStrip` drive `IsVisible` on the part, on both paths** | The `ShowMinimap` pattern, and safe for these two because no style competes for their `IsVisible`. Both paths — `OnApplyTemplate` and the `OnPropertyChanged` branch — per `AGENTS.md` §6, which exists because a property set before the template applies is otherwise lost and a part found afterwards is otherwise never told |
| **`ShowBanner` adds a disjunct to `:banner-none` instead** | `PseudoClasses.Set(":banner-none", BannerKind == DiffBannerKind.None \|\| !ShowBanner)`. Measured above. The alternative writes `LocalValue` over a style and never gives it back |
| **`:banner-none` comes to mean "not shown", not "nothing to say"** | The honest cost of the route above. The alternative is a second `:banner-off` style in both themes, which is a restyle this plan excludes |
| **`InlineDiffView` gains a `HeadersPart` constant and looks the grid up** | The XAML already names it, so this costs one constant and one `NameScope.Find`. Without it `ShowHeaders` would have to hide two headers separately — a second implementation of one toggle, and the one that leaves the divider behind |
| **An `internal` accessor per new part, as the other parts have** | `StatusStrip` and `Minimap` are already exposed this way for tests. `HeadersGrid` and `Banner` join them rather than tests reaching through the visual tree by name |
| **Both views** | Not because `AGENTS.md` §7 demands parallelism — **it does not**. §7 is a ledger of divergences and records "No minimap and no connector gutter" as legitimate; eleven `StyledProperty` registrations already exist on one view and not the other. The reason is the ask: this chrome exists to serve read-only viewing, and `InlineDiffView` already *is* the read-only view — §7 records that too |
| **The demo gets all three as View-menu entries** | `ShowMinimap` and `UnchangedContextRows` both have one. `AGENTS.md` §9 is the argument: the by-hand pass finds what headless cannot, and the Windows and macOS runs are still open. Each entry carries an `AutomationProperties.Name`, which `AccessibilityCoverageTests` scans for |
| **Registered on each view today** | If the viewer plan lands they are re-owned to its base — three more of the re-ownings its registry gate exists to catch. Named here so that is expected rather than discovered |

### Dismissed

| Alternative | Why not |
|---|---|
| One `Chrome` flags enum | Reads well and binds badly: a XAML host sets a boolean per piece, and a flags enum makes every setter a converter |
| `IsVisible` from code for the banner too | Measured wrong — see above. It is the shape the first draft mandated and its own test would have caught |
| A `:banner-off` pseudo-class of its own | Keeps `:banner-none` honest and costs a style in both themes plus a second mechanism for one question. Reconsider if `:banner-none`'s new meaning bites |
| Waiting and putting these on the viewer's base | Mechanically cleaner and delivers nothing until a refactor that may not happen |

## Scope

**In.** The three properties and their wiring on both views; `HeadersPart` and two internal
accessors; the three demo menu entries; the tests; `AGENTS.md`, `DECISIONS.md`, `PROGRESS.md`,
`CHANGELOG.md`, and the hosting guide's list of what a host can set.

**Out.**

| Excluded | Note |
|---|---|
| Any restyle | No `axaml` changes beyond none at all: the banner route needs no style, and `PART_Headers` is already named in both themes |
| A user verb for the new toggles | `ShowMinimap` carries one — *"Hide the overview map"* in the pane menu. Matching it would need three new `DiffViewStrings` keys, eight locale files and a `docs/locale-review` regeneration. Deliberately not done here; a host-facing property first, a menu verb only if asked for |
| **Folding's menu entries** | `UnchangedContextRows` defaults to off, but `DiffPaneMenu.AddFolding` is called unconditionally, so a right-click still offers *Show all rows / Show differences only / Show context*. Folding is therefore "optional" in its display and not in its menu. A host suppresses those entries today through `PaneContextMenuOpening`, which is the same seam that turns the menus off wholesale. Recorded rather than fixed, because it is a menu question and not a chrome toggle |
| Changing any default | All three are `true`. A host that sets nothing sees what it sees today |

## Phases

| Phase | Size | Content |
|---|---|---|
| **1 — The three toggles** | M | The properties and both mechanisms on both views; `HeadersPart` and the internal accessors; the three demo entries. Tests below, seen red first |
| **2 — The record** | S | `AGENTS.md` gains a row per property — including that the banner's is a pseudo-class and why — plus the corrected reading of §7. `DECISIONS.md` takes the `LocalValue`-over-`StyleTrigger` measurement, which is the reusable part. `PROGRESS.md`, `CHANGELOG.md`, the guide |

## Testing

Every new test is proven able to fail before it is committed.

| Test | Asserts |
|---|---|
| **Each part hides, and the survivors take the room** | Per part per view: `IsVisible` false on the part, and the panes' height grown by what the part occupied. **Never the hidden part's own bounds** — measured stale at 22, 23 and 29 while hidden, so that assertion passes for a broken implementation too |
| **Each part comes back** | The same three, toggled off and on, with the panes' height returning to its baseline. This is the case the banner's rejected mechanism fails |
| **The banner's cases drive a banner** | At rest `BannerKind` is `None` and the banner is already hidden, so a toggle test in the default state asserts nothing. Every banner case loads identical sources first, so there is something to suppress |
| **`ShowBanner = true` does not force an empty banner** | With nothing to say, a permitted banner stays hidden. The half that distinguishes the pseudo-class route from the one that was rejected |
| **Each property set before the template applies still takes** | Three tests, not one: `AGENTS.md` §6 makes the two-path rule an invariant per property, and a property wired on one path only stays green if only its sibling is tested. `ShowBanner`'s version is the intersection case — set before the template, on a build that will have something to say |
| **The defaults move nothing** | Committed snapshots unchanged, full suite green with no test edits |
| **The demo entries carry automation names** | `AccessibilityCoverageTests` scans the `.axaml`; three new menu items without names would pass only because the element set is a hardcoded literal that does not include them |

## Risks

| Risk | Assessment |
|---|---|
| **`:banner-none` changes meaning** | It stops meaning "nothing to say" and starts meaning "not shown". `UpdatePseudoClasses` is the only writer and `AGENTS.md` gets the row, but a reader who knows the old meaning will be wrong once. The named alternative is a `:banner-off` style in both themes |
| **Each switch takes more than its name suggests** | The strip carries save outcomes and the caret lane, not only transient messages. The banner is the only in-control affordance for `Retry()` and `ForceAlign()` — both public, so a host can re-surface them. The headers carry the header context-menu surface and the `:pane-focused` accent, so with F6 still switching panes nothing shows which pane has focus. **Three sentences in the guide, not one** |
| Three more properties to re-own if the viewer plan lands | Named in *Decisions*; its registry gate is designed for exactly this |
| The unnamed divider in Inline's headers grid | Harmless while the grid is hidden as a whole, which is what `HeadersPart` buys. It would matter if someone later hid the two headers individually; the row that records `HeadersPart`'s reason is what stops that |

## Conventions

Conventional Commits, dense bodies, no AI attribution trailer. New tests are proven able to fail
before they are committed. An approved plan is committed before implementation and never edited;
drift goes to `DECISIONS.md`. Work on `feat/chrome-you-can-turn-off`, branched from `main` at
`71c1508`; a completed branch merges without asking once it is proven stable.
