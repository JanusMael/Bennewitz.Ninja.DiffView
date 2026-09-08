# 00002 — Diffing the Save dialog's long values

DiffView's first real consumer. ClaudeForge's Save Changes dialog gains an expandable unified
diff for the property values that are too large to read in an 80-character cell.

## Goal

Replace the truncate-plus-tooltip pattern in `SaveChangesDialog`'s Old and New columns, for the
entries where it fails, with an expandable `InlineDiffView` over the pretty-printed old and new
JSON. A settings value that is a JSON object or array currently renders as its first 80
characters and an ellipsis; the full text is reachable only through a hover tooltip that is
itself capped, or a context-menu copy into some other program. Neither answers the question the
dialog exists to answer: *what changed inside this value?*

The secondary goal is the one that motivated doing this before in-pane editing: put a real
consumer on DiffView's public API while that API is still cheap to change.

## Non-goals

| Excluded | Note |
|---|---|
| Whole-file preview of the settings document | The dialog is built from a property diff, not file text; there is no "new file" without serialising `SettingsDocument.Root` against `BaselineRoot`, and the save path writes a provenance header containing `DateTime.Now`, so every save would show a spurious header diff |
| A two-file diff anywhere in ClaudeForge | Backup/Restore is the better home — restoring an archive genuinely is old-file versus new-file and a restore preview surface already exists. A later plan |
| Side-by-side diff in the dialog | The window is 800 wide across four columns; see *Why the unified view* |
| Editing the values in place | The panes are read-only, and the unified view is read-only by construction |
| Publishing DiffView to nuget.org | The local feed is the delivery mechanism, as it is for `LayeredEditors.Avalonia.Diagnostics` and the `theme-audit` tool |
| Restructuring the dialog | The property table, its four columns, the section headers and the button bar are untouched |

## Decisions taken before drafting

| Decision | Choice | Why |
|---|---|---|
| Which of the two readings of plan 00001's promise | Diff the long values, not the whole file | The "old/new rows" are a JSON property table, so a whole-file diff is a different product. `SaveChangeEntryViewModel` already carries `FullOldValue` / `FullNewValue` untruncated, so the data needs no new plumbing |
| Which trunk | `main` | Accepted knowingly; see *Risks* for the rebase exposure |
| The Avalonia and Semi version skew | Its own ClaudeForge pull request, landed and green before any DiffView code | The bump is solution-wide, dependabot manages an "avalonia" group, and the app csproj already carries a `Tmds.DBus.Protocol` pin to satisfy NU1605 downgrade-as-error. A version regression must not share a diff with a feature |

## What exists today

`SaveDialogBuilder` (`src/ClaudeForge/Services/SaveDialogBuilder.cs`) builds each
`SaveChangeEntryViewModel` with four value fields:

| Field | Content |
|---|---|
| `OldValue` / `NewValue` | `TruncateJson(v, 80)` — the first 80 characters, then `…`; `"(null)"` for null or empty |
| `FullOldValue` / `FullNewValue` | The untruncated JSON, `null` when the value was absent on that side |

The AXAML renders `OldValue` / `NewValue` in a `SelectableTextBlock` per cell, with
`ToolTip.Tip` bound to the full value through `LongValueTooltipConverter` and a single
context-menu Copy that copies the untruncated text.

`LongValueTooltipConverter` delegates its JSON work to `ClaudeForge.Sdk.Diagnostics.JsonFormatting`,
which is the piece this plan reuses rather than reimplements:

| Member | Use here |
|---|---|
| `LooksLikeJson(string?)` | Decides whether an entry qualifies for a diff |
| `TryPrettyPrint(string?)` | Produces the multi-line text each side of the diff is built from |
| `Cap(…)` | Bounds pathological values before they reach the control |

The converter caps its tooltip deliberately: an over-tall tooltip makes Avalonia's positioner
oscillate between above-cursor and below-cursor placement, which the user sees as flicker. That
constraint belongs to the tooltip, not to the diff, and the expander replaces it rather than
inheriting it.

## The AvaloniaEdit-under-Semi question, settled

`src/ClaudeForge/Views/AgentsSkillsEditorView.axaml.cs:23` records that a syntax-highlighting
library was rejected because "AvaloniaEdit is incompatible with Semi.Avalonia; see CLAUDE.md
Common gotchas". No such `CLAUDE.md` exists in that repository. The comment must be answered
before review quotes it, and the answer is that **it was correct when written and is no longer
correct**.

DiffView's own theme audit measures it, because DiffView audits AvaloniaEdit as a consumer:

| Consumer | Theme | Undefined keys | Static | Dynamic |
|---|---|---:|---:|---:|
| AvaloniaEdit Fluent theme | Semi | 6 | 3 | 3 |
| AvaloniaEdit Fluent theme | Semi + compat | 0 | 0 | 0 |
| AvaloniaEdit Simple theme | Semi | 9 | 6 | 3 |
| AvaloniaEdit Simple theme | Semi + compat | 0 | 0 | 0 |
| DiffView | Semi | 0 | 0 | 0 |
| DiffView | Semi + compat | 0 | 0 | 0 |

Under bare Semi, AvaloniaEdit references six keys the theme does not define, three of them
through `StaticResource` — and a static reference to a missing key throws when the template
loads. That is a hard failure and a fair reason to have rejected it.

The compat dictionaries merged into ClaudeForge as
[#38](https://github.com/JanusMael/ClaudeForge/pull/38) (`93065ba`) take both themes to zero
undefined keys. Two further facts close the question:

- Every one of DiffView's 332 tests runs under **Semi as the base theme** —
  `tests/DiffView.Avalonia.Tests/HeadlessTestApp.cs` states it as "the intended host's" — across
  six Semi variants, with a log sink that fails a test on a binding or resource warning.
- The control the ClaudeForge documentation names as the problem case is AvaloniaEdit's **search
  panel**, and `DiffPanePresenter` uninstalls it at `DiffPanePresenter.cs:440`, replacing it with
  DiffView's own find bar. The named offender is not present.

The plan therefore treats the rejection as resolved, and Phase 3 records the resolution in the
comment itself so the next reader is not misled.

## Design

### Which entries qualify

An entry gets an expander when **all** of:

- `Kind == ChangeKind.Modified` — added and removed entries have content on one side only, so
  there is nothing to diff. They keep today's cell and tooltip.
- `FullOldValue` and `FullNewValue` are both non-null.
- The value is worth it: `JsonFormatting.LooksLikeJson(v)` on either side, **or** either side
  exceeds the 80-character truncation threshold, **or** either side is multi-line.

Everything else renders exactly as it does today. The dialog's common case — a short scalar
changing — gains nothing and loses nothing.

### Why the unified view

The dialog is 800 logical pixels wide, and the value columns are `3*` of a `28,2*,3*,3*` grid —
roughly 260px each. A side-by-side diff needs two panes plus a connector gutter in that space and
would be unreadable. `InlineDiffView` is one pane, spans the two value columns when expanded, and
is read-only by construction, which is exactly the semantics a confirmation dialog wants: the
user is inspecting a pending change, not editing it.

It also drops the minimap, the connector gutter and F6, all of which would be noise at this size.

### Where it goes

The expander is a row beneath the existing four-column `Grid` inside the same data-row template,
spanning the Old and New columns, collapsed by default. Expanding it does not change the columns
above it, so the table's alignment — which depends on every row's `Grid` sharing star widths from
a common `StackPanel` parent — is untouched.

### Sizing, and the infinite-height trap

`SaveChangesDialog` is `SizeToContent="Height"`. Inside that, a `ScrollViewer` passes **infinite**
height downward regardless of its own `MaxHeight="640"`. This is documented in the AXAML as the
reason `DataGrid` was rejected: `DataGridRowsPresenter` resolves `CellsHeight` to 0 under infinity
and never materialises rows on first paint, so they appear only after an external resize forces a
second arrange with a finite height.

AvaloniaEdit's `TextView` virtualises on viewport height and sees the same infinity, and
DiffView's height priming runs off real layout passes. The mitigation is therefore a rule, not a
tweak:

**The diff control is given an explicit `MaxHeight` so it never inherits the infinity.** 240px,
which is roughly ten rows at the dialog's font size and leaves budget for the rest of the list
inside the window's `MaxHeight="700"`. A value taller than that scrolls within the pane.

The failure mode is specifically that a resize hides it, so **every test asserts on the first
captured frame**, never after a resize.

### Strings

New user-visible strings go through `Strings.resx` and all eight locale files with real
translations, plus the hand-maintained `Designer.cs`, and are referenced literally as
`Strings.Name` — a build-time guard fails on an unreferenced key and on reflective lookup.

| Key | English |
|---|---|
| `SaveDialogShowValueDiff` | Show differences |
| `SaveDialogHideValueDiff` | Hide differences |
| `SaveDialogValueDiffAccessibleName` | Differences for {0} |

### Packaging

ClaudeForge has no `nuget.config` and consumes its own diagnostics library by `ProjectReference`,
so the local feed is new wiring on that side:

1. `DiffView.Core` and `DiffView.Avalonia` are packed to `/home/janus/c/nuget-local`, the feed
   that already carries `LayeredEditors.Avalonia.Diagnostics` and `Bennewitz.Ninja.ThemeAudit`.
2. ClaudeForge gains a `nuget.config` declaring that feed alongside nuget.org.
3. `src/ClaudeForge/ClaudeForge.csproj` takes a `PackageReference` to `DiffView.Avalonia`, which
   brings `DiffView.Core` transitively.

## Phases

### Phase 1 — The version bump  (S)

Its own ClaudeForge pull request, landed and green before anything else.

Avalonia, `Avalonia.Controls.DataGrid`, `Semi.Avalonia` and `Semi.Avalonia.DataGrid` move from
12.1.0 to the versions DiffView is built against (Avalonia 12.1.2, Semi 12.1.0.1) across every
`csproj`. The existing `Tmds.DBus.Protocol` pin is re-checked against NU1605, and the dependabot
"avalonia" group is left consistent.

**Done when:** ClaudeForge's full CI is green on the bump alone, with no DiffView code present.

### Phase 2 — Packaging and consumption  (S)

DiffView packs to the local feed; ClaudeForge restores it and builds with the reference in place
but nothing yet using it.

**Done when:** `dotnet restore` and a Release build of ClaudeForge succeed with the
`PackageReference` added; the trimmed publish still succeeds; the app runs unchanged.

### Phase 3 — The value diff  (M)

The expander, the qualification rule, `JsonFormatting`-fed pane sources, the explicit `MaxHeight`,
the strings, the automation names. The stale AvaloniaEdit comment in
`AgentsSkillsEditorView.axaml.cs` is corrected to record what the audit measured.

**Done when:** a modified JSON-valued entry expands to a unified diff of the pretty-printed old
and new values; a modified scalar does not offer one; added and removed entries are unchanged.

### Phase 4 — Gates  (S)

The four CI gates ClaudeForge enforces, each verified rather than assumed.

| Gate | What it needs |
|---|---|
| Trim | Release publish with `PublishTrimmed`. DiffView's own trimmed publish is already clean at 57 MB with AvaloniaEdit and TextMateSharp aboard, so this is expected to pass; if a `TrimmerRootAssembly` entry is needed, `TRIMMING.md`'s `Markdown.Avalonia` case is the pattern |
| Localization | All eight locales populated, `Designer.cs` updated, keys referenced literally |
| Accessibility | `AxamlAccessibilityCoverageTests` sees an `AutomationProperties.Name` on the expander; templates carry `x:DataType` for compiled bindings |
| Theme audit | Regenerated in DiffView (`compat` then `report`) if anything under `src/DiffView.Avalonia/Themes` moves; the ClaudeForge consumer row is expected to change |

## Testing

Following DiffView's own discipline: new tests are proven able to fail before they are committed.

| Test | Asserts |
|---|---|
| Qualification | A modified JSON entry qualifies; a modified short scalar does not; added and removed entries never do; a null side never does |
| First paint | The expanded diff renders its rows in the **first captured frame**, with no resize — the specific failure the `SizeToContent` trap produces |
| Height | The pane never exceeds its `MaxHeight`, and a value far taller than the cap scrolls inside the pane rather than growing the dialog past `MaxHeight="700"` |
| Content | The two sides are the pretty-printed forms of `FullOldValue` and `FullNewValue`, and the change count matches the JSON's actual differences |
| Unchanged path | A dialog with no qualifying entry renders byte-identically to today's |
| Localization | Every new key resolves in all eight locales |
| Accessibility | The expander carries an automation name; the existing coverage test passes |

## Risks

| Risk | Mitigation |
|---|---|
| **The rebase.** `origin/feat/agentforge-opencodeforge` is 83 commits ahead and moves `SaveChangesDialogViewModel.cs` and `SaveDialogBuilder.cs` into `src/AgentForge.Avalonia.Shell/Save/`, a product-neutral assembly. This plan targets `main` by decision, so a rebase is owed when that branch lands | The view stays at `src/ClaudeForge/Views/SaveChangesDialog.axaml` on both trunks, and this plan's changes are concentrated there. The view-model change is the qualification predicate — small, and portable to the new location. If the shell assembly forbids referencing DiffView, the predicate moves to the host's `ClaudeSaveDialogText` layer |
| The infinite-height trap defeats the control in ways the explicit `MaxHeight` does not cover | Phase 3 stops and reports rather than working around it; the first-paint test is the detector |
| The version bump destabilises ClaudeForge CI | Phase 1 is standalone and reversible; nothing depends on it until it is green |
| DiffView's API turns out to be awkward to host | That is the point of doing this first. Any friction is recorded and fixed in DiffView before the editing plan hardens the surface |

## Conventions

Conventional Commits, dense bodies, no AI attribution trailer. ClaudeForge work lands as a branch
plus a pull request, announced to the session working in that checkout before anything is
touched; `git checkout` is never run there. `PROGRESS.md` keeps the "Upstreamed to ClaudeForge"
list current, and a change ClaudeForge declines is recorded in `DECISIONS.md` with the reason.
