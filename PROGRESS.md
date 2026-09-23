# Progress

## Resume

**This repository has a remote, and CI has run.** `main` is at
`https://github.com/JanusMael/Bennewitz.Ninja.DiffView`, public, and the first execution of a
workflow written dormant in plan 00018 found **three defects that had been green here for
twenty-three plans**. Everything below the fold is still true; what changed is that "green" now
means something it did not mean before, because it is no longer a statement about one machine.

**`main` carries plan 00024 phase 1.** `dotnet build DiffView.slnx -warnaserror` is clean and the
suite is 679 locally. **CI is not green**, and the remaining failures are known rather than
mysterious:

| Defect | Where | State |
|---|---|---|
| **A** — `docs/theme-audit.md` stale on every CI machine | all four test jobs | **Diagnosed and fixed upstream**; blocked on a release, see *Open* |
| **B** — `PackagingTests` packed a configuration the suite never built | all four test jobs | **Fixed**, plan 00024 phase 1 |
| **C** — 32 rendering tests fail | Windows and macOS only | Plan 00024 phase 3. A decision, not a bug |

**Defect A is the one worth reading about**, because the shape of it is the lesson. The theme
audit's content digest hashed each file's path *relative to the configuration's own directory*
alongside its bytes. ClaudeForge resolves to a **sibling checkout** on a developer machine
(`../cl/ClaudeForge/src`) and to a **fetched copy** on CI (`reference/ClaudeForge/src`) — a
both-spellings form the audit's `paths` explicitly supports. Same 43 files, same pinned commit,
clean worktree, every count in the row identical; only the digest moved. It could never have passed
in both places. The method's own summary is the indictment: the report changes *"whenever a pin bump
changes what was audited, and only then."*

It took **three days and three measured, dead hypotheses** to find, and then one artifact upload to
see. That upload is the other half of phase 1: CI wrote `theme-audit.received.md` and threw the
runner away, so the failure named a file nobody outside the runner could read. With it kept, the
diff was **one line**.

**The fix is upstream and merged**, in `Bennewitz.Ninja.XamlQuality` — the audit moved there on
2026-09-20 and DiffView now consumes it. Both digest call sites root at the deepest directory the
scanned files share, which is a property of the audited set rather than of the machine. Predicted
result for this repository, computed from the same 43 files: **`b7ea0ec438c5` in both layouts**,
where it was `0ef898b7243a` here and `f95218bc267e` on CI.

Plans 00001, 00003–00020 and 00022 are complete and closed; plan 00002 was rejected on its own
review before any code was written. **Plan 00021 phase 1 is done** — `DiffBuildController` and
`IDiffSurface`, on `feat/the-viewer-beside-the-editor`, pushed, 681 green. **Plan 00023 phase 1 is
done.** **Plan 00024 is approved and phase 1 has landed on `main`.**

### What ships

`SideBySideDiffView` compares two `PaneSource`s over two `DiffPanePresenter`s, with rendered padding
holding the rows level. It carries row and word-level highlights, line-number and change-marker
gutters, headers, a banner for what failed or was skipped, a status strip, a connector gutter
between the panes and an overview map beside them; vertical scrolling is coupled 1:1, F7 and
Shift+F7 walk the changes and F6 switches panes. Above the panes sits the find bar — the query box,
the Match case / Whole word / Regex / Changed rows only toggles, the L / R / Both scope, the count,
previous / next / close, and an inline line for a bad pattern or a truncated result — with matches
highlighted in both panes, ticked down the map, and Ctrl+F / F3 / Enter / Escape driving it. Each
pane colours its text from a TextMate grammar chosen by the side's file extension, under everything
the diff draws.

Either side can be made editable. An edit rests for `ReDiffDelay` and rebuilds through the same
latest-wins worker without replacing the document, so the caret, the selection, the scroll offset
and the undo stack all survive; `IsDirty`, `Save` and `Revert` round-trip the encoding, the
byte-order mark and the line-terminator convention the file arrived with; and a copy arrow in each
number margin sends a block or a whole-line selection to the other side, as Alt+Left and Alt+Right
also do.

Unchanged rows fold behind a placeholder under `UnchangedContextRows` — `null`, `0` and `n` being
Beyond Compare's *Show All*, *Show Differences* and *Show Context* in one property, off by default.
A fold is a **row** range projected onto each side's lines through `RowProjection`, never read off a
document, which is what keeps the panes aligned; a click on a placeholder gives its run back, and so
does walking a find match into one.

A right-click anywhere the control draws opens a menu about what is under the pointer — either
gutter, the connector column, the overview map, a header or the text — through a public context
object (`DiffPaneContext`, or `DiffHeaderContext` for a header) that a host amends through an
opening event or replaces outright. No left-click behaviour changed to make room for it.

`InlineDiffView` is the same model, builder, renderers, margins, find engine and state machine on
**one** read-only pane, over a document it composes from both sides in `diff -u` order: context rows
once, then every removal of a change block before every addition. The gutter carries a number column
per side, the find bar loses its scope group, and there is no minimap, no connector gutter and no
F6. The demo hosts both, switched by View ▸ Unified (inline) view or the `--unified` flag.

Builds and searches run latest-wins on a worker over text captured on the UI thread; the control is
always in one `DiffViewState`; every user-visible string goes through `DiffViewStrings` and every
log line through `DiffViewLog`, which never carries document text. `DiffCommand` names the verbs and
`DiffKeyMap` says which key each is on, so a rebind moves the context menu's accelerators with it.

**Text resolves through `DiffViewStrings.Localization`** — one record carrying an optional host
resolver and an optional culture, because two statics that must agree are a bug waiting on someone's
ordering. The chain is the host's resolver, then the bundled translation for the resolved culture,
then the compiled English table: a host that wires nothing gets its own language out of the box, and
a host that wires something outranks the library by construction. The resolver is *handed* the
culture rather than reading it, so returning `null` for a key is a decision made knowing which
language will answer instead. Eight locales ship — `de-DE`, `es-ES`, `fr-FR`, `ja-JP`, `ko-KR`,
`pt-BR`, `ru-RU`, `zh-CN` — **machine-generated and not yet read by a native speaker**, which every
file header says and *Decisions* records; the parity gate proves keys and placeholders, never
wording.

### Open

1. **Plan 00024 phase 2 is unblocked as of 2026-09-23 and is the next work.** The digest fix is
   merged in `Bennewitz.Ninja.XamlQuality` (`4f7bd62`, PR #3) and still present on `main`; the
   calendar block has expired, because the caldate `2026.3.923` is now today's rather than
   tomorrow's.

   ⚠ **Upstream has moved since that merge — re-check before assuming.** As of 2026-09-23
   `main` is at `02e945b` with six further commits from another session: `XQ1004` (a Grid slot a
   control does not fit in), `XQ1005`, and an Avalonia gotchas reference. A release now carries all
   of that, not only the digest fix, so it deserves its own verification pass rather than the one
   run on 2026-09-22.

   **Then, here**: bump the pin, regenerate `docs/theme-audit.md`, and confirm the ClaudeForge row
   reads **`b7ea0ec438c5`** — predicted from the same 43 files under the new rooting and identical
   in both layouts, so a different value means the diagnosis was incomplete rather than that the
   report simply moved. CI should agree.

   ⚠ The fix **rebases every digest once**, not only rows resolved outside the tree, because the
   root moves from the configuration directory to each scanned set's own common ancestor. Nothing
   breaks silently — the drift test fails loudly and a regenerate fixes it.

2. **Plan 00024 phase 3 — defect C — is the only one that is a decision rather than a bug.** 32
   rendering tests fail on Windows and macOS at 10–13% of pixels differing: glyph rasterization, not
   antialiasing. The bundled `DejaVuSansMono` fixes *which* glyphs are drawn and nothing about *how*
   they are rasterized; macOS is arm64 besides. Linux is green only because every verified PNG was
   generated on Linux. **Two of the 32 are not baselines at all** —
   `MarkerChipTests.The_glyph_is_centred_in_its_chip` and
   `MarkerGlyphTests.Every_marker_is_heavy_enough_to_scan` measure the frame, so no baseline strategy
   touches them: two pixel measurements are simply tighter than the platform spread. The plan's
   decision is Linux-only baselines plus the owed by-hand runs; the artifacts from run `35780424399`
   hold the evidence.

3. **The first publish — on hold, decided 2026-09-23.** DiffView does not publish yet. The packages
   are MIT, carry their metadata and readme, and pack at a caldate the release tag supplies. The
   remote exists. **Two preconditions remain and both are Brian's**: a nuget.org Trusted Publishing
   policy naming owner, repository and workflow filename, and `NUGET_USER`.

   ⛔ **`NUGET_USER` is a repository VARIABLE, here and everywhere. The workflow was wrong and has
   been corrected; the repository settings have not.** `release.yml` was written in plan 00018 from
   a template that said `secrets.`, and read `secrets.NUGET_USER` until 2026-09-23, when it was
   changed to `vars.NUGET_USER`. A nuget.org profile name is not a secret, and the convention is
   the same in every repository here — a `secrets.` reading is a defect to fix, not a local shape to
   accommodate.

   **Two steps, and only one is done:**

   | Step | State |
   |---|---|
   | `release.yml` reads `vars.NUGET_USER` | ✅ done 2026-09-23 |
   | The repository has `NUGET_USER` as a **variable** | ❌ **not done — Brian's** |

   ⚠ `gh secret list` shows `NUGET_USER` set as a **secret** on 2026-09-20 and `gh variable list` is
   empty, so **a release dispatched right now would fail**. Set the variable, then delete the
   secret, which otherwise only preserves the ambiguity. DiffView's publish being on hold is what
   makes this safe to leave half-done for the moment. The failure mode when these disagree is
   "NUGET_USER is not set" while `gh secret list` shows it present — which sends you looking
   anywhere but at the `vars.`/`secrets.` prefix, and cost a round on
   `Bennewitz.Ninja.AssemblyQuality` on 2026-09-22.

   ⚠ **`Bennewitz.Ninja.XamlQuality`'s own tag is not this repository's to cut** — another session
   manages that release. DiffView's publish being on hold does not hold XamlQuality's, and defect A
   waits on the latter rather than the former.

4. **Plan 00021 phases 2–4 — the viewer.** Phase 1 is done and pushed:
   `DiffBuildController` (1,872 lines) and `IDiffSurface` (17 members, not the ~15 the plan
   estimated), 681 green. Phase 2 is `DiffViewer` itself, and it **no longer gates the release**.
   The viewer still has no find — re-confirmed 2026-09-22 rather than superseded — and **phase 4's
   hosting guide must name that gap outright**, so a host wanting read-only side-by-side with search
   meets documentation rather than silence.

5. **Plan 00023 phases 2–5 — the window harness.** Phase 1 (`catch-crash`) is merged. The rest
   follows 00021.

6. **The eight locales ship unread, with the caveat owed to the consumer.** Decided 2026-09-18:
   this **no longer gates the release**. Blocking a publish on eight volunteers has no end date, and
   `DiffViewStrings.Localization` lets a host outrank the library with its own resolver. What is
   owed is the caveat where a consumer reads it — the README and the package description, not the
   `.resx` headers alone. **Unstarted, and it has no plan**; whether it needs a number was asked and
   never answered.

7. **Adopt `XQ1002` and delete `AccessibilityCoverageTests`.** Unblocked —
   `Bennewitz.Ninja.XamlQuality 2026.3.922` is live and carries it. Closes two holes measured in
   this repository's own gate on 2026-09-22: an empty `AutomationProperties.Name=""` passes it, and
   an element set that matches nothing passes it, so a renamed control would silently stop being
   checked. Needs its own plan.

8. **`AQ1001` reports two findings here**, from this author's own new
   `Bennewitz.Ninja.AssemblyQuality 2026.3.922`: `DiffDocumentBuilder.Build` and `DiffSearch.Find`
   take defaulted `CancellationToken`s on public API about to publish. A policy call, not a defect —
   the BCL defaults its own tokens everywhere, and the rule says so in its own documentation.

9. **Windows and macOS by-hand demo runs**, owed since plan 00001 phase 10. Reframed by 00024: the
   *suite* does not pass on those platforms, so a by-hand run was never the first obstacle. Plan
   00023's harness is what would make them repeatable.

10. **`LayeredEditors.Avalonia.Diagnostics` → `Bennewitz.Ninja.AppServices.Avalonia`: deferred
    2026-09-23, deliberately.** The demo keeps its hand-packed `1.0.1` from `../nuget-local`, and
    nothing breaks: the published package ships `2026.3.924` and later, trusted-published and
    trimmable, but **DiffView is not moving to it yet**. What a move would involve, recorded now so
    it need not be re-derived: the namespace becomes `Bennewitz.Ninja.AppServices.AvaloniaUI`
    (dialogs in `.Dialogs`), the package id keeping `.Avalonia` while the assembly and namespaces
    take `.AvaloniaUI`. Six of the demo's seven call sites are unaffected — `ConfigureLogging` with
    `AppName` / `LogsDirectory` / `MinimumLevel` / `FileNamePrefix`, `InstallAvaloniaHooks`,
    `ShowFatalErrorDialog`, `ShowNativeFatalError`.

    ⛔ **`AvaloniaDiagnostics.ToggleLiveLogWindow()` is gone** in the published package — the live
    log and tail windows stayed in ClaudeForge. It has **three** call sites here, not the two the
    hand-off note names: `MainWindow.axaml.cs:76` (F12), `:573` (`OnToggleLiveLog`) and
    `MainWindow.axaml:113`, the View-menu entry. Replacing it means a window of DiffView's own, fed
    by `AvaloniaDiagnosticsOptions.ConfigureLogger` adding a Serilog sink, with `EventListener` for
    diagnostic events. ⚠ That menu entry carries an `AutomationProperties.Name`, so it is one of the
    71 elements plan 00025's accessibility gate counts — **whichever of the two changes lands
    second sets that floor.** The rename log is at
    `https://github.com/JanusMael/Bennewitz.Ninja.Templates/blob/main/docs/layered-editors-renames.md`.

    The cost of deferring is that the local feed is a hand-packed artifact no other machine can
    restore, so CI and any fresh checkout stay dependent on a folder here, and the gap widens with
    each AppServices release.

Items 3, 6, 9 and 10 are Brian's own. Nothing else is in flight; new work needs a new plan under
`plans/`.

### Running it

**CI is the other judge now, and it disagrees with this machine.** Three defects were green here
for twenty-three plans; two of the three are invisible from a developer checkout by construction.
A local green is evidence about one machine — `catch-crash` judges a run, and CI judges the claim.

**`scripts/run-demo.sh` (and `run-demo.ps1`) is the by-hand path.** `--edit left|right|both` starts
a side editable so a run no longer opens with a menu drive, `--unified` opens the inline view, and
`AGENTS.md` §9 is how to capture and drive the running window from a session here.

**`scripts/catch-crash.sh` judges a test run.** `--check <log>` reads a captured one; with no
argument it re-runs the suite until it catches an abort and keeps that log. It exists because a
host that dies mid-run prints `Failed!` with `failed: 0` and a short total, which any grep reads
as success. `--expect 679` makes a short total a failure too.

The theme audit regenerates after a ClaudeForge pin bump or a change under
`src/DiffView.Avalonia/Themes`, in this order:

```bash
dotnet run --project src/ThemeAudit -- compat
```

```bash
dotnet run --project src/ThemeAudit -- report
```

## History — plan by plan

Written as each plan closed, newest first, and left as written except where a later plan falsified a
sentence outright. What is true *now* is in *Resume* above; this is the record of how it got there.

**[Plan 00013](plans/00013-folding-unchanged-rows.md) is complete** — all five phases — on `main`.
Unchanged rows now fold behind a placeholder, with a configurable number of rows kept around every
change — `UnchangedContextRows`, where `null` folds nothing, `0` hides every matching row and `n`
keeps `n`, which is Beyond Compare's *Show All*, *Show Differences* and *Show Context* in one
property. Both views, off by default. The panes stay row-aligned because a fold is a **row** range
projected onto each side's lines, never read off a document, and because a row is foldable only if
collapsing its lines removes that row's height and nothing else. A click on a placeholder gives its
run back, and so does walking a find match into one — chosen over excluding folded matches,
because a count that changes when you fold describes the view rather than the file. *Navigation*
needed nothing and never did: a fold only covers unchanged rows and a change block has none.

**The load-bearing change is not the folding.** The connector gutter and the overview map were fed
`row × lineHeight`, an equation only accidentally true, and a fold is the first thing in this
library to break it; `RowProjection` is now the one place a row becomes a pixel. Phase 1 shipped it
as the identity, changing no behaviour, which is why the other 549 tests staying green *is* that
phase's evidence.

**Driven by hand on 2026-09-11**, which found three things no headless frame could: `--edit` was
missing from the demo's flag summary; the pane's context menu **fits** at real size, so the
overflow its snapshot shows is the 600 px capture window rather than the app and the question of
reorganising it answers itself as *no*; and the menu's *"Show the rows hidden here"* acted on the
caret while saying "here", now the run under the pointer as plan 00010's rule has it. The capture
recipe in `AGENTS.md` §9 was wrong and is rewritten: `import` does not work on this box at all and
`ffmpeg`'s `x11grab` does, against the client's own window rather than the root, which under
XWayland holds nothing.

**[Plan 00012](plans/00012-context-menus-beyond-the-pane.md) is complete** — all five phases — and
`main` carries it: `feat/menus-beyond-the-pane` fast-forwarded in on 2026-09-11 as
`feat/in-pane-editing` did before it, and the repository still holds no merge commit. A right-click
anywhere the control draws now opens a
menu about what is under the pointer: the two gutters, the connector column, the overview map and
the headers, through the two extensibility shapes plan 00010 built. **No left-click changed** —
neither the connector's jump nor the map's scroll — and off every polygon the connector opens
nothing, because the empty column is the splitter. The header carries its own context type, a side
and its file with no line at all; both types go through the one menu implementation, so the contexts
differ and the menu's behaviour cannot. `GoToChange` and `SelectBlock` arrive in the key map
unbound. The icon column 00010 reserved is filled from the vocabulary already on screen — the
gutter's arrow and the marker margin's `+` `−` `≠`, on the theme's foreground. *Plan 00012 phases*
and *verification* below carry the detail, and *Decisions* the three arguments worth keeping.

**The folding spike has run, and its verdict is split.** Beyond Compare's *Show Differences* and
*Show Context* — hiding rows that match — are feasible: a fold taken symmetrically over unchanged
aligned rows keeps both panes aligned, and `TextView.CollapseLines` needs none of AvaloniaEdit's
folding stack, whose margin and index-0 generator would each collide with what the panes already
install. *Show Same* is not that in mirror image: a change block that is lines on one side is
padding on the other, and padding has no line to collapse, so hiding what differs would need
`PaddingSpec` to become a function of what is folded. The cost is elsewhere anyway — the connector
gutter and the overview map are fed `row × lineHeight`, and a fold is the first thing in this
library to break that equation, so a folding plan is mostly a plan about one row-to-pixel
projection. *Folding spike* below and *Decisions* carry it. **Plan 00013 answered that question**:
all three modes, as the one `UnchangedContextRows` property.

**Every phase of [plan 00001](plans/00001-side-by-side-diff-control.md) is complete**, Phase 11 —
the optional inline view — included. The library ships two controls over one model.

`SideBySideDiffView` compares two `PaneSource`s: two `DiffPanePresenter`s over the sources' own
documents, with rendered padding holding the rows level, row and word-level highlights,
line-number and change-marker gutters, headers, a banner for what failed or was skipped, a status
strip, a connector gutter between the panes and a minimap beside them, vertical scrolling coupled
1:1, change navigation on F7 / Shift+F7 with F6 switching panes, and a tooltip on every gutter.
Above the panes sits the find bar: the query box, the Match case / Whole word / Regex / Changed
rows only toggles, the L / R / Both scope, the match count, previous / next / close, and an inline
line for a bad pattern or a truncated result. Matches are highlighted in both panes above the row
fills and below the selection, the current one in its own brush, with ticks down the minimap and
the count and scope in the strip; Ctrl+F opens the bar with the query pre-filled from the
selection, F3 and Enter walk the matches, Escape closes it and hands focus back. Each pane also
colours its text from a TextMate grammar chosen by the side's file extension, with the theme
following the variant, under everything the diff draws; an extension no grammar claims is plain
text and not a failure, and a grammar that will not install turns itself off and leaves the
control `Degraded` with the language named. Whitespace glyphs, line-ending glyphs, the tab width
and the pane font are the view options, each pushed to both panes and none of them touching a
row's height; the focused pane is accented under its header; each pane copies its own selection
while read-only holds against typing and pasting; and every decorator announces itself. A
right-click in either pane — or Shift+F10, which resolves to the caret — opens a context menu of
copy, navigate, find, save and revert, whose entries a host amends or replaces and whose
accelerators are read from the key map rather than typed in. **So does a right-click on either
gutter, on the connector column, on the map and on a header**, each menu about what that surface
is about: a line, the block a polygon draws, the row under the pointer, the side and its file. No
left-click changed. The entries that have a mark in the gutter's vocabulary carry it — the copy
arrow, and `+` `−` `≠` for the change's kind — on the theme's foreground.

`InlineDiffView` is the same model, builder, renderers, margins, find engine and state machine on
**one** pane, over a document it composes from both sides in `diff -u` order: context rows once,
then every removal of a change block before every addition. It is read-only, because half its
lines belong to one file and half to the other, and it is the only document in the library that a
build replaces. A modified pair keeps its kind on both halves, so the word-level highlights
survive the unified reading; the gutter carries a number column per side, a context line filling
both; the find bar loses its scope group and searches the two sides, dropping only the matches the
view does not show; and there is no minimap, no connector gutter and no F6. The demo hosts both,
switched by View → Unified (inline) view or the `--unified` flag, and only the one on screen holds
the sources.

Builds run latest-wins on a worker over text captured on the UI thread, and so do searches, over
`TextDocument` snapshots with the line table copied on the UI thread; the control is always in one
`DiffViewState`; every user-visible string goes through `DiffViewStrings` and every log line
through `DiffViewLog`, which never carries document text — not the query either, only its length,
and it names the unified pane `unified`, which is neither side.

What is left is not code: **Windows and macOS demo runs are still owed** from Phase 10. The
Linux run is no longer owed — the demo is an XWayland client and its window **can** be captured
from a session here, by window id rather than from the root; `AGENTS.md` §9 carries the recipe and
the mistake it corrects. The app can also be **driven** here: `xdotool` and `wmctrl` are installed,
clicks reach menu items and keys reach a focused pane, which is how plan 00009's rebind was judged
by hand. The demo had no flag for making a side editable when this was written, so in-pane editing
and the copy arrows were switched on through the View menu; `--edit left|right|both` landed with
plan 00013's follow-ups. All three ClaudeForge
contributions (PR #37, #38 and #44) are merged and the ClaudeForge pin follows (see *Upstreamed to
ClaudeForge*).

**[Plan 00003](plans/00003-in-pane-editing.md) — in-pane editing.** Phase 1,
typing, took no source change at all: `LeftReadOnly` and `RightReadOnly` already reached the
panes, and no renderer, margin or sync turned out to depend on the document holding still.
Phase 2, live re-diff, is done too: an edit rests for `ReDiffDelay` and then rebuilds through
the same latest-wins worker, from the pane's live text, without replacing the document — so
the caret, the selection, the scroll offset and the undo stack all come through. `LiveReDiff`
can be cleared and `ReDiffNow()` called instead, which is the escape hatch for a pair too
large to rebuild on a debounce. What remains is dirty state and a save that round-trips the
encoding and the line endings, copy-to-side through the connector gutter, the feedback marks,
and the scale measurement that sizes the escape hatch. Phase 3 is done too: `IsDirty`,
`Save` and `Revert`, with `PaneWriter` writing back the encoding, the byte-order mark and the
line-terminator convention the file arrived with, and a stamp that refuses to overwrite
someone else's write. Phase 4 is done too: a block's lines replace the other side's over the ranges
`ChangeBlock` already carried, undoably, with arrows in the connector gutter and Alt+Left /
Alt+Right on the current block. Phase 5 is done too: the lines this session edited are
tracked across edits that move them, marked down the marker margin and named in the strip.
Phase 6 closed it: on the 200,000-line pair a keystroke costs 71 ms, the
re-diff behind it 403 ms, and a rebuild re-primes only the 4,000 lines whose padding moved
rather than the whole document — so live re-diff is affordable at that size, no threshold is
imposed, and DiffPlex stays unvendored. **Every phase of plan 00003 is complete.** A ClaudeForge
integration was drafted as plan 00002 and rejected on its own review before any code; it is
deferred until `feat/agentforge-opencodeforge` lands, and *Decisions* records why.

The demo reaches all of it: View → Edit left/right pane clears the corresponding `ReadOnly`,
Copy block to left/right sits on the Alt+Left and Alt+Right the composite binds, and File →
Save and Revert per side report each `SaveOutcome` in the status line. The unified view stays
read-only whatever the menu says.

**Plan 00003 landed on `feat/in-pane-editing`**, which fast-forwarded into `main` on 2026-09-10,
and **[plan 00004](plans/00004-copy-arrows-in-the-panes.md) is complete on the same branch.** Plan
00003's rendered evidence landed first: `EditingSnapshotTests` captured the marks an edit leaves,
in both variants, and the modified-since-load bar proved to run down the marker margin's inner
edge beside the diff's `~` rather than over it, which reads as intended. The copy arrows did not
survive their first frame: as bare triangles sharing a 24 px column they met in the middle and
read as one bowtie. Giving each a head **and a shaft** fixed the glyph; plan 00004 then fixed the
arrangement that had made a 12 px glyph the constraint at all.

**The arrows now live in the panes.** Each sits in the number margin of the side it would copy
*from*, drawn over the line number of its block's anchor row and pointing the way the text would
travel — Beyond Compare's arrangement, on a cell that already existed, so it costs no horizontal
space. A block a side has no lines in still offers its arrow, in that side's padding, where no
number is given up at all. The connector column is back to one job and 16 px wide, five public
members lighter, and the decision *"The arrows are hit before the polygon they sit inside"* is
retired rather than amended: an arrow in a number margin is not inside a polygon.

Looking hard at the gutter turned up two more things. Settling the arrow's vertical placement
turned up the same mistake in the modified-since-load bar, which spanned each line's whole visual
box — padding rows included, though those belong to the other side's lines and nobody edited them;
it now covers the line's own row, like the arrow. And the change markers were
measured for the first time: the deleted marker laid down **5 pixels of ink against a digit's 29**,
making the mark meant to carry kind without colour the faintest thing in the gutter. They are now
`+` `−` `≠` — one vocabulary of operators — drawn semibold, at 41, 25 and 56. Each now sits on a
chip of its kind's colour, shared across a run of same-kind rows, so the gutter shows a block's
extent as well as each row's kind — and the chip is composited over the *pane* background rather
than blended into the gutter, which is what let every palette colour stay exactly as it was.

**[Plan 00006](plans/00006-copying-a-selection.md) is complete, all three phases.** A copy arrow
is now drawn with an outline carrying its silhouette and a fill inside it, so it reads as a
control rather than a mark, and the pointer shows a hand over one. Phase 1 also
closed a gap it found rather than created: **`contrast-pairs.json` had never scored an arrow at
all** — the block arrow had been drawn on the gutter since plan 00003 with nothing holding it to a
floor. Four pairs went in, and the twelve tokens the selection arrow needed were declared and
scored ahead of the code that draws them.

**A selection now copies.** `CanCopySelection` / `CopySelection` send a pane's selected whole lines
to the other side, over the block copy's own rule and a different range: the selection's lines
occupy a run of rows, the other side's lines in those rows are the target, and where that side has
no lines there at all the copy inserts rather than replaces. `CopyBlock` and `CopySelection` share
one writer, so they differ in the range they name and in nothing else. The selection's arrow sits
in the number cell of the line the selection starts on, in its own colours, and where that row is
also a block's anchor it takes the cell and the block's arrow is not drawn — the block's copy is
still on Alt+Left and Alt+Right. Its tail bar is drawn on every frame and seen only where the
palette gives the bar token a colour, which the colour-blind palette does and the default one does
not.

Phase 3 closed it with twelve frames — both arrows at once, a selection whose rows are padding on
the other side, and the cell both arrows want, in both variants and both palettes. **Brian then
asked for the block arrow to stand out from the selection's blue the way Beyond Compare's does**,
and the default palette's is `#8A6D00` over `#F0E442` in Light, `#FFD54F` over `#DAA520` in Dark.
Getting there **corrected a contract**: the plan scored each arrow's fill against the gutter at
3.0, which rules out every yellow on a near-white ground, so the first attempt landed on a
goldenrod `#AB8000` that Brian rejected on the rendered frames as reading brown. A fill is not text
on a ground — the outline carries the silhouette — so `contrast-pairs.json` now scores each fill
against **its own outline** at 1.5, the number the plan named for that relationship and did not
contract. *Decisions* carries the arithmetic. The colour-blind palette keeps its slate arrow, where
gold would collide with two Okabe–Ito hues already in use.

**[Plan 00007](plans/00007-the-overview-map.md) and [plan 00008](plans/00008-docking-the-overview-map.md)
are complete.** `DiffMinimap` is now the overview map Beyond Compare has: **two lanes**, one per
side, inking a lane only where that side has a line in the bucket's rows — so a one-sided block
inks one lane and *notches* the other, which is the thing one lane cannot say. The viewport box
drags where the rest of the map jumps, the wheel scrolls it, `ShowMinimap` turns the column off,
and `MinimapPlacement` docks it outside either pane. The lanes do not follow the dock — a lane
names a file, not an edge — while the current-block marker and the find ticks do, through one
`MirrorEdges` flag. Plan 00008 also **fixed a defect four plans old**: the headers grid and the
panes grid never shared a column layout, so a header was the right size in the wrong place;
*Decisions* has the arithmetic.

**[Plan 00009](plans/00009-configurable-key-bindings.md) is complete.** `DiffCommand` names the
control's verbs and `DiffKeyMap` says which key each is on; both views hold one, and both rebuild
through the single `DiffKeyBindings`, which replaces the bindings **the control owns** and leaves a
host's own alone. `GestureFor` is the seam plan 00010's context menu will read its accelerators
from — the demo already reads it, in View ▸ Key bindings, so a rebind moves the menu's labels along
with the keys. The unified view takes `DiffKeyMap.UnifiedDefault()`: the same six gestures, with
`SwitchPane` and the four copies unbound, and a gesture given to one of those five is skipped and
logged rather than left dead without a word. Plan 00009 also settles what plan 00006 deferred:
`CopyToLeft` / `CopyToRight` now copy the selection when there is one and the block otherwise, so
the chord agrees with the gutter, and `CopyBlockToLeft` / `CopyBlockToRight` keep the old behaviour
under a name, unbound. That supersedes a non-goal of an approved plan, so *Decisions* carries it.

**[Plan 00010](plans/00010-the-pane-context-menu.md) — the pane context menu — is complete.** The
deliverable is `DiffPaneContext`, not the menu: everything a host would ask about a click lived in
the internal `PaneMetadata`, so nobody could write a pane menu of their own. The menu hangs off
`ContextRequested` so the keyboard's Shift+F10 resolves to the caret rather than to nowhere, and a
right-click **never moves the caret**, which would discard the selection the menu offers to copy. A
host amends the item list through `PaneContextMenuOpening` or replaces the menu with
`PaneContextMenu`; either way the context arrives as the menu's `DataContext`. Every entry is present
on every open, enabled or not — Beyond Compare's own rule, captured for the plan — while a verb the
*view* lacks is absent rather than greyed, decided by the same `CommandOrNull` the key map reads.

**Every string that names a side is now a whole sentence per direction.** Seven of them built the
side in by substitution, which no translator can inflect; fourteen keys and six selectors replaced
them, the rendered English unchanged. `StringCatalogueTests` is the contract this repository lacked —
every key has English text, reaches the host's resolver, and is named once — adapted from
ClaudeForge's `LocalizationParityTests`, with a sentinel test that catches a pasted side word coming
back. Whether the library should ship `.resx` and satellite assemblies of its own, as ClaudeForge
does, rather than leaving translation to the host's resolver, is **open**; the key structure suits
either.

## Phases

| Phase | Status | Notes |
|---|---|---|
| 0 Bootstrap | done | 7 tests across three tiers; trim-check clean; manual dialog/F12 check passed |
| 1 Virtual-padding spike | done — go | 6 headless tests, 1 of them `Perf`; priming batched at 256 |
| 2 Theme-key audit and exhaustive dictionaries | done (ClaudeForge PR #38 merged) | `theme-audit` `report` and `compat` over a JSON configuration; inventories model Default fallback, `StyleInclude`, linked files, code providers and brush opacity; contrast scoring against each variant's own surface; reviewed Fluent→Semi and Simple→Semi mappings; `DiffView.Tokens.axaml` + colour-blind sibling; `docs/theme-audit.md` committed with drift tests; runtime resolution and rendering tests under all ten targets; tool packed as 1.1.0 |
| 3 Core model, probing, search engine | done | `PaneSource`, `TextProbe`, `LineSplitter`, `DiffOptions`, `SimilarityGate`, `DiffDocumentBuilder`, `SideBySideDocument` + `Padding`, `WordDiffCache`, `DiffSearch`; 87 unit tests (seven invariants, every failure path, cache, search) and 4 `Perf` measurements |
| 4 Pane presenter, padding, gutters | done | `DiffPanePresenter` over the source document; `PaddingRun` / `PaddingElement` / `PaddingElementGenerator` / `PaddingHeightPrimer` lifted from the spike; `PaneMetadata` bounds-checked and version-stamped; `DiffLineBackgroundRenderer`, `DiffSelectionRenderer`, `DiffCaretRenderer`, `DiffLineNumberMargin`, `ChangeMarkerMargin`, `DiffBrushes`; the fault boundary with `RenderFault`; caret column normalised after `Home` twice; control themes in `Themes/DiffPanePresenter.axaml`; `AGENTS.md`; 26 headless, pixel and snapshot test cases |
| 5 Composite control, scroll sync, headers, status strip, theming | done | `SideBySideDiffView`, `DiffPaneHeader`, `DiffStatusStrip`, the state machine and banners, the latest-wins worker, `ScrollSync`, `StatusController` on `TimeProvider`, `DiffViewLog`, `DiffViewStrings` over the new surface, compiled themes; the demo on the composite; 34 headless and snapshot test cases plus 5 status-controller unit tests |
| 6 Word-level highlights and options | done | `WordDiffLookup` over the live documents, one per build bound to its options; piece rectangles in `DiffLineBackgroundRenderer` through the visual line's columns; the marker margin's long-line tooltip; 6 headless and snapshot test cases |
| 7 Navigation, minimap, connectors, tooltips | done | `CurrentChangeIndex` + commands + F7 / Shift+F7 / F6, current-block border, "change i of n"; `DiffMinimap`; `ChangeConnectorGutter` with `SplitRatio`; tooltips on line numbers, markers, connectors and the minimap; 9 headless and snapshot test cases |
| 8 Find | done | `DiffFindBar` (query, Match case / Whole word / Regex / Changed rows only, L / R / Both scope, count, prev / next / close, inline error and truncation notice); `SearchMatchRenderer` per pane over `KnownLayer.Selection`; debounced, cancellable searches over `DocumentPaneText` snapshots, off the UI thread above 2,000 rows; minimap match ticks; the strip's find lane; Ctrl+F / F3 / Shift+F3 / Enter / Shift+Enter / Escape; 9 headless and snapshot test cases plus the sentinel log test's find leg |
| 9 Syntax highlighting | done | `SyntaxHighlighting` over `AvaloniaEdit.TextMate` per pane, the grammar from the file's extension and the theme from the variant; `UseSyntaxHighlighting` on presenter and composite; an unclaimed extension is plain text, a failed install is `Degraded` with the language named and the diff untouched; trimmed publish clean with TextMateSharp on board; 12 headless, snapshot and pixel test cases |
| 10 Scale, visibility, accessibility | done | `ScalePerfTests` on the 200k pair and the 1 MB line (numbers in *Measurements*; DiffPlex not vendored); `ShowWhitespace` / `ShowLineEndings` / `TabWidth` on presenter and composite, none of them re-priming; `PaneFontSize` / `PaneFontFamily`, which do; the mixed-line-ending notice asserted end to end; copy per pane with read-only holding against paste and typing; the focus accent under the focused pane's header on a new `DiffView.FocusAccentBrush`; a runtime sweep of every decorator's automation name; 10 headless, pixel and snapshot test cases plus 2 `Perf` measurements |
| 11 Inline (unified) view | done | `InlineDocument`, the unified line table over the model — context rows once, a block's removals before its additions, a modified pair keeping its kind on both halves; `InlineDiffView` over a document it composes from both sides, read-only, with the renderers, margins, find bar, status strip and state machine unchanged, a number column per side, the find scope collapsed and the block extents in unified lines; the demo hosts both views; 37 unit, headless and snapshot test cases |

## Plan 00024 phases

| Phase | Size | Status | Notes |
|---|---|---|---|
| 1 Two fixes and one question | S | done | `PackagingTests` names the configuration it packs; both test jobs keep what the gates rejected. Ended with defect A **diagnosed**, which is what it was for |
| 2 Defect A | S | **blocked** | Fixed upstream and merged in `Bennewitz.Ninja.XamlQuality`; waiting on a release. The caldate scheme means the next tag is tomorrow's — `2026.3.922` is spent, and tagging `.923` today would make the package version disagree with the assembly attributes AutoVersioning stamps from build time |
| 3 The rendering split | M | not started | Independent of A and B. The `received-*` artifacts from run `35780424399` carry the raw material: 3.1 MB of macOS PNGs, 1.9 MB of Windows |
| 4 The record | S | not started | |

## Plan 00024 phase 1 verification

| Done-when item | Result |
|---|---|
| Defect B fixed | `dotnet pack` defaults to Release; the suite builds Debug; `--no-build` then read a `bin/Release` that on this machine was populated by plans 00018 and 00019 and on a clean checkout does not exist. The configuration is now read from `AssemblyConfigurationAttribute` rather than hardcoded, so it stays right if the suite is ever run in Release |
| The packaging check packs what was built | **Proven in both directions before committing.** Deleting `bin/Release` locally reproduced CI's `NU5026` naming the same missing `DiffView.Core.dll`; after the fix it passes with still no packable `.dll` under `bin/Release`, so the flag and not stale output is what made it green |
| A red CI run stops being a dead end | Both test jobs upload `docs/*.received.md` and `tests/**/*.received.*`. Gated on `failure()` rather than `always()`: on a green run those paths do not exist, and a step that loudly uploads nothing every time is one that gets deleted by whoever tidies next |
| Defect A diagnosed | **One-line diff.** Committed `0ef898b7243a` against CI's `f95218bc267e`, every count identical — 43 files, 1852 keys, 210, 1109. Then both values reproduced from those same files by changing nothing but the path prefix, `../cl/ClaudeForge/src` against `reference/ClaudeForge/src`. Three earlier hypotheses were measured and dead: the pins resolve exactly, the committed report was not merely unregenerated, and `XamlFiles` already sorts ordinally |
| The suite | ubuntu and the culture leg went from 2 failures to **1**; Windows and macOS keep defect C's 32 |
| What it cost to find | Three days. The artifact upload is worth more than the fix beside it: the diff was available on the first run after it existed |

## Plan 00023 phases

| Phase | Size | Status | Notes |
|---|---|---|---|
| 1 `catch-crash` | S | done | `scripts/catch-crash.cs` plus `.sh` and `.ps1` wrappers, following the `gen-locale-review` convention. Re-runs the suite until it catches an abort and keeps that log; `--check <log>` judges a captured one and runs nothing, which is what makes it testable through its real command line. Two fixtures under `fixtures/test-runs/`. 3 tests, 2 of 2 mutations killed |
| 2 The driver, X11 only | M | not started | The seven verbs and the X11 back end; the three capture scripts reduced to calls into it |
| 3 `run-demo --detach` and the locale tool | S | not started | |
| 4 The Windows back end | M | not started | Cannot be verified here |
| 5 The record | S | not started | |

## Plan 00023 phase 1 verification

| Done-when item | Result |
|---|---|
| The judgement is not a grep | Six clauses: a non-zero exit, a missing summary, an outcome that is not `Passed`, any failure, counts that do not close, and a total short of `--expect`. **The outcome clause is the load-bearing one** — an aborted host says `Failed!` while reporting `failed: 0`, and no arithmetic over the counts alone can see that |
| An aborted run is refused | Green, and **red when the outcome clause is removed** — with it gone the reconstructed abort reads as healthy, because 412 + 0 + 0 = 412 closes perfectly |
| A complete pass is accepted | Green against a **real captured run** of all 676, absolute paths normalised so the fixture is not machine-specific |
| A short run is refused | Green via `--expect`, and red when that clause is removed |
| The fixtures are honest about what they are | `healthy.log` is captured. **`aborted.log` is reconstructed and its header says so** — the abort is intermittent and no log of one was kept when it was first seen, so every element comes from the recorded signature: exit 134, the host exiting unexpectedly, `Failed!`, `failed: 0`, 412 of 676. A real one replaces it if ever caught |
| The tool is tested through its real interface | The tests shell out to `dotnet run scripts/catch-crash.cs -- --check`, not to a shared type — the `LocaleReviewTests` principle that a gate re-deriving its subject from the code that produced it tests very little |
| New tests proven able to fail | 2 of 2 mutations killed. **The harness itself had to be fixed first**: it judged a mutation by `failed: 0` being absent from the output, which a failing run contains for other reasons, so it reported a killed mutation as survived. The exit code is the oracle now — the spike's lesson that a tool filtering its own input can under-report, relearned |
| The suite | 679 = 676 + 3, no existing test edited; build clean under `-warnaserror` |

## Plan 00022 phases

| Phase | Size | Status | Notes |
|---|---|---|---|
| 1 The three toggles | M | done | `ShowHeaders`, `ShowStatusStrip`, `ShowBanner` on both views, defaulting on. Headers and strip through one `ApplyChromeVisibility` per view, called from both paths; the banner through a disjunct in `:banner-none`, because a code write would outrank its style permanently. `InlineDiffView` gained the `HeadersPart` it had always named in its theme, plus `BannerPart` and two internal accessors on both views. Three demo View-menu entries. 15 tests, 12 red first; 7 of 7 mutations killed |
| 2 The record | S | done | `AGENTS.md` §6 ×3 and a sentence making §7's purpose explicit; `DECISIONS.md` ×2 — the value-priority finding and the stale-bounds one, both general; the hosting guide's display table; this file; `CHANGELOG.md` |

## Plan 00022 verification

| Done-when item | Result |
|---|---|
| Each part hides and the survivors take the room | Green on both views, and red before the wiring. Measured: panes 555 at baseline, 577 with the headers off, 600 with the strip off as well |
| Each part comes back | Green. The half the rejected banner mechanism cannot do at all |
| The banner's cases drive a banner | Every banner case loads identical sources first. At rest `BannerKind` is `None` and the banner is already hidden, so a toggle test in the default state would assert nothing |
| A permitted banner with nothing to say stays hidden | Green — and **this is the only test of fifteen that fails when the banner is put back on `IsVisible` from code.** The other fourteen pass against the design that strands an empty banner on screen |
| Each property set before the template applies still takes | Green, three tests per view rather than one: `AGENTS.md` §6 makes the two-path rule an invariant per property |
| The defaults move nothing | 676 = 661 + 15, no existing test edited, no committed snapshot moved |
| The demo entries carry automation names | Green, by a scan of the `.axaml` — `AccessibilityCoverageTests`' element set is a hardcoded literal that does not include `MenuItem`, so it would not have caught this |
| New tests proven able to fail | 7 of 7 mutations killed: each path dropped, each property unwired, the unified view left alone, the banner's disjunct dropped, the banner's mechanism reverted, a demo entry stripped of its name |
| The suite | 676 passed / 0 failed / 0 skipped, `en-US` and `de-DE` alike; build clean under `-warnaserror` |

## Plan 00020 phases

| Phase | Size | Status | Notes |
|---|---|---|---|
| 1 The release | M | done | `OnDetachedFromVisualTree` on both views disposes the controller and nulls the field, the order commented because it is load-bearing; `UpdateStrip` reads the field rather than the lazy getter, which is what the review measured undoing the release; `Status` gains a sentence about its lifetime; `StatusOrNull` is internal so a test can ask without building. Ten tests, six of them red first. 7 of 7 mutations killed |
| 2 The record | S | done | `DECISIONS.md` ×5 — the round-three measurement, the residue and why zero is unreachable, `ScrollSync` closed, plan 00019's template-touch lesson relaxed, and the two consequences a consumer would otherwise find. This file, `CHANGELOG.md` |

## Plan 00020 verification

| Done-when item | Result |
|---|---|
| A detached view leaves no armed timer | Green on both views, and red before the fix |
| A strip refresh on a detached view does not rebuild the controller | Green on both views. **This is the case the plan's second draft would have shipped broken** — measured at two armed timers with its fix applied and all four of its proposed tests passing |
| A re-attached view still takes a message | Green on both views. Passes today too: it guards the naive one-line fix, and mutation M1 (the null dropped) is what proves it bites |
| Detaching with a message on screen does not throw | Green on both views, and red before the fix — the teardown path did not run at all |
| The residue is one timer, and it drains | Green on both views. Characterisation, not a repair: `Set` arms *after* it raises `Changed`, so no detach-time hook can cancel what the call is about to create. Bounded at ten seconds, which is the bound the defect always had |
| `ScrollSync` | **Not a finding.** Already released at `SideBySideDiffView.cs:3130`; the draft's open question was answerable by one grep |
| New tests proven able to fail | 7 of 7 mutations killed: the null dropped, the dispose dropped, the two statements swapped, `UpdateStrip` reading the getter again, `InlineDiffView` left unfixed, and two aimed at the residue tests alone — a success outlasting the advance, and two timers armed per message |
| The suite | 661 passed / 0 failed / 0 skipped, `en-US` and `de-DE` alike; build clean under `-warnaserror` |

## Plan 00019 phases

| Phase | Size | Status | Notes |
|---|---|---|---|
| 1 The two gates | S | done | `HostingGuideGate` + `HostingGuideTests`, shipped before the document existed and against fixtures broken in exactly one way each. Citations resolve by reflection against the shipped surface (exported types, `Public` binding flags — a non-public member is *absent*); the quickstart is read from the document, parsed and driven to `Ready`; and the `xmlns` is compared against the namespace the control's type reports. `Avalonia.Markup.Xaml.Loader` added test-only. 5 of 5 mutations killed |
| 2 The guide | M | done | [`docs/hosting-diffview.md`](docs/hosting-diffview.md). Writing it against the live gate exposed the gate's own gap: the guide must print `Bennewitz.Ninja.DiffView`, which resolves to no type because plan 00017 left none — `Kind.Namespace` added, excused but verified against the namespaces the surface declares. The TextMate cost is stated at the measured 6 MB, not the 6.7 MB the parked draft asserted. Joined `DocumentationCitationTests` |
| 3 The package readme | S | done | `PackageReadmeFile` on both packable projects plus the `None … Pack="true" PackagePath="\"` item the guide needs for living outside them; gated by packing for real and reading the readme back out of the `.nupkg`. 2 of 2 mutations killed. **Uncovered an intermittent process abort it did not cause** — see *Plan 00019 verification* |
| 4 The record | S | done | `README.md` leads with the guide, the demo says in one line that it is a harness rather than a sample, `DECISIONS.md` takes both findings, this file, `CHANGELOG.md` |

## Plan 00019 verification

| Done-when item | Result |
|---|---|
| Every API name the guide prints resolves | Green, and shown biting: a deliberate `SideBySideDiffView.NoSuchThing` appended to the real document fails `The_guide_itself_passes_every_check` and names it |
| The quickstart parses, declares the right namespace and reaches `Ready` | Green, over the block read from the document. It carries no `x:Class`, so it is loadable exactly as printed |
| A non-public member is caught | Green — `SideBySideDiffView.Builder` is internal and visible to the test assembly, and still reports as absent |
| The readme is inside both packages | Green, by `dotnet pack` and reading the nuspec's `<readme>` and the payload back |
| The guide is linked from `README.md` | Green |
| The suite | 651 passed / 0 failed / 0 skipped, `en-US` and `de-DE` alike; build clean under `-warnaserror` |
| **The abort phase 3 uncovered** | **Fixed in the test; the finding under it is open.** The pack gate parks the Avalonia thread for ~2s, which let a real-clock status auto-clear come due. `StatusController.DispatchToUiThread` then invoked inline — `Dispatcher.UIThread.CheckAccess()` answers true for a pooled thread the headless dispatcher has handed back — outside its own `try`/`catch`, so `VerifyAccess` escaped unhandled and aborted the process at exit 134. **The runner reports this as `Failed!` with `failed: 0`**, so a grep for failing tests finds nothing. Two crashes in 18 runs, then 0 in 10 with the gate removed (which nearly bought the wrong culprit), then 1 more in 13. The quickstart gate now installs a `FakeTimeProvider` **before `Show()`** — after it, `StatusController` has already been built lazily on the template's touch of `Status`, so a late assignment compiles and changes nothing — and asserts `ActiveTimers` is non-zero to prove the clock took. **18 consecutive clean full runs** afterwards |

## Plan 00018 phases

| Phase | Size | Status | Notes |
|---|---|---|---|
| 1 The licence and the metadata | S | done | MIT `LICENSE` — there was none — plus `Authors`, both URLs and tags on every packable project, and the gate that each is present. **Landed with phase 2**: `PackagingTests` holds both sets of checks and splitting one file across two commits to honour a planning device would be the worse commit |
| 2 The workflow | M | done | `.github/workflows/release.yml`, mirroring the upstream one and adapted: the reference fetch and diagnostics pack, both test legs, `/p:Version=` on build **and** pack, Trusted Publishing, an explicit two-package push, the GitHub release. **Inert** — no remote |
| 3 The record | S | done | `DECISIONS.md` (five sections), `AGENTS.md` §1, this file, the changelog, and the README's release procedure |

## Plan 00018 verification

| Done-when item | Result |
|---|---|
| **The packages carry a licence** | pass: MIT, and `PackagingTests.The_licence_the_packages_declare_is_the_one_in_the_repository` compares the expression against the file, which nothing in the toolchain does — a repository can declare MIT and ship Apache without a murmur |
| Every packable project carries the metadata a consumer needs | pass: `PackagingTests.Every_packable_project_carries_the_metadata_a_consumer_needs`, read as XML rather than searched as text, since the comment above those properties names every one while defining none |
| **The tag becomes the version** | pass, and measured: `dotnet pack -c Release -p:Version=2026.3.914` produces `Bennewitz.Ninja.DiffView.Avalonia.2026.3.914.nupkg` with the Core dependency pinned to the same version, all eight satellites and the XML documentation |
| The version reaches build and pack both | pass: `PackagingTests.The_release_workflow_carries_the_tag_version_into_build_and_pack`. `--no-build` means one without the other packs what the build made, silently |
| **The push never globs** | pass: `PackagingTests.The_release_workflow_names_the_packages_it_pushes`. The guard against publishing `Bennewitz.Ninja.ThemeAudit` — packable here, local-feed-only by design — which could not then be withdrawn |
| Every new test proven able to fail | pass: six mutations across the two phases, six killed — the licence expression removed, `Authors` removed, one project's tags removed, the `LICENSE` file disagreeing with the expression, the upstream `*.nupkg` glob restored, and the version dropped from the pack |
| The suite, both cultures | pass: 631 passed / 0 failed / 0 skipped under `en-US` and under `de-DE` |
| **Anything published** | **no, and deliberately.** No remote, so the workflow has never run. The first push is Brian's: a package id and version are permanent, unlisting is possible and deleting is not |
| `PackageReadmeFile` | **not set, and owned by the next plan.** A package readme and the hosting guide are the same document; writing both means keeping two consumer-facing surfaces in step. The release cannot go out before the guide, which costs nothing because it cannot go out before the remote either |

## Plan 00017 phases

| Phase | Size | Status | Notes |
|---|---|---|---|
| 1 The sweep | M | done | 201 occurrences across 158 files, longest name first because `…DiffView.Avalonia` is a prefix of `…DiffView.Avalonia.Tests`. Six forms: namespaces and usings, two `RootNamespace` overrides, four `x:Class` and four `xmlns:dv`, the `F:` documentation prefix in two places, `theme-audit.json` plus its regenerated report, and the `ResourceManager` base name. No behaviour changed |
| 2 The guard, and the proof | S | done | `NamespaceConventionTests`, and `DiffCommand`'s `global::` **deleted** so the compiler holds the other half. Three mutations, three as specified — including one whose correct outcome is **NO BUILD** |
| 3 The record | S | done | `DECISIONS.md` (four sections, one superseding line), `AGENTS.md` §1, this file, the changelog as a breaking change |

## Plan 00017 verification

| Done-when item | Result |
|---|---|
| **The shadow is gone** | pass, by construction: `DiffCommand`'s cref reads `Avalonia.Input.KeyBinding` with no `global::` and the build is clean. Re-creating `Bennewitz.Ninja.DiffView.Avalonia` anywhere in the library fails at that line with `CS1574` — measured, as mutation M3 |
| No namespace shadows a referenced root | pass: `NamespaceConventionTests.No_namespace_carries_a_segment_that_shadows_a_referenced_root`, against the real reference graph rather than the word *Avalonia* |
| The guard looks at all the code it claims to | pass: `NamespaceConventionTests.Everything_outside_the_repository_prefix_is_generated`. **The naive rule failed on its first run** and correctly — `CompiledAvaloniaXaml` is emitted by the XAML compiler into this assembly and shipped by Avalonia too, and is not a collision because nothing we write resolves through it |
| Every new test proven able to fail | pass: three mutations, three as specified — a segment named `Avalonia` somewhere that still compiles, a namespace outside the prefix, and the old namespace back in the library, whose correct outcome is NO BUILD |
| **Locales still resolve** | pass: `LocalizationTests.The_bundled_translation_answers_when_the_library_ships_the_culture`. The `ResourceManager` base name is a string literal and the only form of the name that fails silently; this test existing is what made a mechanical sweep safe |
| The locale review packet still reads | pass: `LocaleReviewTests.Every_committed_review_document_describes_the_strings_it_claims_to`, which fails wholesale if the `F:` prefix was missed |
| The theme audit still matches | pass: `theme-audit report` regenerated `docs/theme-audit.md`; **three lines move and all three are content hashes**, every count identical, which is what a rename should do to a report about themes |
| The suite, both cultures | pass: 627 passed / 0 failed / 0 skipped under `en-US` and under `de-DE` |
| Nothing outside code changed name | pass: `InternalsVisibleTo` and every `avares://` URI name assemblies, checked rather than assumed. `DECISIONS.md` keeps the old name where it records what was verified at the time, with a superseding line beside it |

## Plan 00016 phases

| Phase | Size | Status | Notes |
|---|---|---|---|
| 1 The nine summaries | S | done | Five genuine gaps written — `Status.Dirty` and the four `Save.*`, whose `{0}` is a **header title, a file name, not a side word** — and four back-references given their sibling's full form. The gate reads the XML documentation build output, because that is what the generator reads. Four mutations, four killed |
| 2 The generator, and one locale | S | done | `scripts/gen-locale-review.cs` and its `.sh`/`.ps1` wrappers with a `--check` mode, over `de-DE` alone. The phase's question — do the summaries read well enough to review from — answered by reading the output |
| 3 Eight locales and the drift gate | M | done | The other seven, `LocaleReview` + `LocaleReviewTests`, and `Summaries` lifted out so the gate and the generator read one artifact. **The gate caught a real defect on its first run** (below). Four mutations, four killed |
| 4 The record | S | done | `DECISIONS.md` (three sections), `AGENTS.md` §6 (three rows), this file, the changelog, the README |

## Plan 00016 verification

| Done-when item | Result |
|---|---|
| Every summary accounts for its string's placeholders | pass: `StringCatalogueTests.Every_summary_accounts_for_the_placeholders_its_string_takes`. **Failed on 9 of 143 when written** — measured before the plan was drafted, which is why phase 1 exists at all |
| Every summary reads on its own | pass: `StringCatalogueTests.No_summary_leans_on_the_key_above_it`. **Not in the plan**, and the plan is the reason it is needed: four summaries pass the placeholder gate and say nothing — *"The same, rightwards."* accounts for all of its zero placeholders. *Decisions* carries why a checkable criterion is a proxy rather than the goal |
| Every key appears in every packet | pass: `LocaleReviewTests.Every_committed_review_document_describes_the_strings_it_claims_to` reports a key with no row, and `Every_shipped_locale_has_a_review_document` a locale with no document |
| Every row shows the strings it claims to | pass, same test, across all eight — English against `EnglishDefaults`, the translation against the `.resx`, the context against the key's summary, the `Holds` column against the string itself |
| A pipe in a translation is escaped and read back | pass: `LocaleReviewTests.An_escaped_pipe_survives_the_round_trip`, against a fixture, since no shipped string contains one |
| A newline in a translation is refused, by key | pass, by inspection of the generator rather than by test: it exits naming the culture and the key. No shipped string contains one and the generator is not a library, so there is nothing to call |
| **A padded string survives the round trip** | pass, **and this is what the gate was worth.** `Fold.Placeholder` is `" ⋯ {0} matching rows hidden "` and a markdown cell trims; the first eight documents showed a string that was not the string. Now `␣`, with `LocaleReviewTests.A_string_padded_with_spaces_survives_the_round_trip` and `A_padded_string_shown_without_its_padding_is_caught` holding both halves |
| The committed packet matches the strings | pass, and `dotnet run scripts/gen-locale-review.cs -- --check` is the same question asked from outside the suite |
| The suite is green under both cultures | pass: 625 passed / 0 failed / 0 skipped under `en-US` and under `de-DE` |
| A native speaker has read a locale | **no, and that is the point.** This plan built the thing to read; it did not make anyone a German speaker. The eight locales stay marked machine-generated until a person says otherwise, and that still gates the first release |

## Plan 00015 phases

| Phase | Size | Status | Notes |
|---|---|---|---|
| 1 The mechanism, proven to fire | S | done | `EnglishChromeAttribute` over `DiffViewStrings.Override`, five tests, and one class pinned. The phase's job was the unknown: these are `[AvaloniaFact]` tests dispatched by `AvaloniaTestRunner`, and an xunit before/after attribute that silently did not run there would read as a pin while pinning nothing. It runs — measured, not inferred |
| 2 The 34 | M | done | 33 methods across 14 files, 34 failures because one is a theory with two rows. Pinned rather than softened. The leg 92 → 58, and the 58 left were all snapshot comparisons |
| 3 The frames | M | done | Twelve classes pinned whole, `SyntaxSnapshotTests` pinned on one method, `PresenterSnapshotTests` pinned nowhere. The leg 58 → 0. **No baseline regenerated** |
| 4 The leg and the record | S | done | The `culture-leg` CI job on `ubuntu-latest`, `DECISIONS.md` (four sections, one superseding its own arithmetic), `AGENTS.md` §5 (three rows), this file, the changelog |

## Plan 00015 verification

| Done-when item | Result |
|---|---|
| The pin reaches the body of a headless test | pass: `EnglishChromeTests.The_pin_reaches_the_body_of_a_headless_test`. **The load-bearing one** — the only evidence that Avalonia's dispatched test path honours an xunit before/after attribute at all. Culture-independent by construction, so it means the same under either leg; removing the attribute turns it red |
| The pin reaches the rendered frame | pass: `NavigationSnapshotTests.Minimap_connectors_and_the_current_block_render` goes 2 failed → 0 under `de-DE` with the class pinned, and back to 2 without it |
| Text resolves English while the thread asks for German | pass: `EnglishChromeTests.Text_resolves_English_while_the_thread_asks_for_German`, against the compiled table rather than a literal, so the two sides cannot drift |
| The pin survives a test that reset the seam | pass: `EnglishChromeTests.The_pin_is_re_established_after_a_test_reset_the_seam`. **The scope decision, as a test.** An ambient pin recovers 16 of the 94 and ends at 78 failures distributed by run order; mutating the attribute to pin once ambiently is what fails this |
| The pin does not leak past its test | pass: `EnglishChromeTests.The_pin_is_removed_when_the_test_ends`, and `EnglishChromeTests.A_second_pin_opened_over_the_first_is_refused` holds the serial assumption the static scope rests on |
| Every new test proven able to fail | pass: five mutations, five killed — the attribute off the headless test, the attribute off the resolution test, the teardown removed, the nesting guard removed, and the pin made ambient. Harness at `~/c/cl/scratch/DiffView/mutate-phase1.py` |
| The suite is green under `de-DE` | pass: 611 passed / 0 failed / 0 skipped with `DIFFVIEW_TEST_UI_CULTURE=de-DE` |
| The English run is unchanged | pass: 611 passed / 0 failed / 0 skipped, the 606 that existed plus phase 1's five |
| The leg is a CI gate | pass: `culture-leg` on `ubuntu-latest`, beside the three-OS matrix rather than inside it. **Not yet observed on GitHub — there is no remote**, so what is verified is the workflow's shape and the command it runs, both of which were run here |
| No snapshot baseline regenerated | pass: none. A frame needing regeneration would have been a rendering difference hiding behind a culture failure, which is a finding rather than a step |
| The 62/32 split | **corrected to 60/34.** The recorded split was by class name; by failure mode `EditingSnapshotTests` moves from the snapshot half to the behavioural half. *Decisions* supersedes the arithmetic and keeps the conclusion |
| The hidden snapshot failures phase 2 was ordered around | **none surfaced, and the ordering was still right.** A pin repairs the assertion and the frame in one stroke, so the frame that had never been compared under the leg compares and passes. Softening the 34 instead would have moved the failures one phase later rather than removing them |

## Plan 00014 phases

| Phase | Size | Status | Notes |
|---|---|---|---|
| 1 The seam | M | done | `DiffViewLocalization` (resolver + culture as one record), `Override`, the culture-carrying resolver, the generated neutral resx and its drift gate, the `ResourceManager` step, `NeutralLanguage`, the six test sites migrated to `using`, and `HeadlessTestApp` pinning `DefaultThreadCurrentUICulture`. **Shipped as a no-op** — the 581 existing tests staying green is the evidence |
| 2 The parity gate | S | done | `LocaleParity`: missing keys, undeclared keys, placeholder-set equality, share identical to English. Written **before** any translation existed, against `fixtures/locales` — a miniature neutral catalogue and a `zz-ZZ` file wrong in all four ways at once |
| 3 The eight locales | M | done | `de-DE`, `es-ES`, `fr-FR`, `ja-JP`, `ko-KR`, `pt-BR`, `ru-RU`, `zh-CN`, 143 keys each, machine-generated and unreviewed. `SatelliteResourceLanguages` names them; the trim canary keeps them |
| 4 Evidence | M | done | The width measured before anything was rendered, the by-hand pass in `pt-BR` and `ja-JP`, the demo's `--culture`, `DECISIONS.md` (three sections), `AGENTS.md` §6 (five rows) and §9 (three), this file, the changelog |

## Plan 00014 verification

| Done-when item | Result |
|---|---|
| A host resolver outranks a satellite | pass: `LocalizationTests.A_host_resolver_outranks_the_bundled_translation`, with the culture set to one the library ships, so the satellite is present and still loses |
| A partial resolver never mixes languages | pass: `LocalizationTests.A_partial_resolver_falls_through_to_the_resolved_culture_not_the_machines` — a resolver answering for Japanese and covering one key sees the other 142 come back Japanese, not the machine's en-US. **The defect that put the culture in the signature**, and unwritable before a locale existed |
| A satellite is used when no resolver is set | pass: `LocalizationTests.The_bundled_translation_answers_when_the_library_ships_the_culture`. **This is the test that kills phase 1's surviving mutation**, exactly where *Decisions* said it would have to |
| English is used when the culture has no satellite | pass: `LocalizationTests.A_culture_the_library_ships_nothing_for_falls_back_to_English`, against `fi-FI` |
| The localisation state cannot be half-reset | pass: `LocalizationTests.Resetting_restores_both_the_resolver_and_the_culture`; mutated to restore only the resolver, it fails — which is the shape a two-property seam would have shipped |
| `Override` restores on an exception | pass: `LocalizationTests.Override_restores_the_previous_state_when_the_body_throws` |
| The existing resolver-swap contract still holds | pass: `SideBySideDiffViewTests.Swapping_the_string_resolver_before_load_changes_the_rendered_strings` unmoved |
| The committed neutral resx equals the English table | pass: `LocalizationTests.The_committed_neutral_resx_carries_every_English_default_and_no_others`, and `scripts/gen-strings.cs --check` as the other half |
| Every locale has every key and no others | pass: `LocaleParityTests.Every_shipped_locale_passes_every_check`; the fixture proving it can fail is `The_broken_fixture_locale_is_caught_reading_real_files` |
| Placeholders match English per key | pass, and the fixture had to be made **asymmetric** first: a reader blind to `{{` finds `{0}` in both halves of a matched pair and calls them equal. `LocaleParityTests.An_escaped_brace_is_not_a_placeholder`; *Decisions* carries it |
| No locale string pastes a side word | pass **by placeholder parity rather than a check of its own** — a translator reintroduces the defect by adding a hole to a whole sentence, which the set comparison already fails. `LocaleParityTests.A_locale_that_adds_a_placeholder_is_caught` |
| A trimmed publish carries the satellites | pass: `dotnet publish -c Release -r linux-x64 --self-contained true` emits no `IL2xxx` and all eight culture directories are present in the output |
| The pane menu fits in the widest locale at real size | pass, **and not in the locale the plan named**. Measured first: `de-DE` is 100% of English's widest entry and `ja-JP` 76%, while `pt-BR` is 117%. The by-hand pass ran `pt-BR` — menu 403×434, no truncation, no wrap, accelerators aligned — and `ja-JP` at 345×456. *Decisions* carries why German was the wrong thing to fear |
| The header detail line and status strip fit | pass, in both `pt-BR` and `ja-JP`, at the app's real 1100×720 |
| The gutter tooltips fit | **not done, and not reachable this way.** `xdotool mousemove` places the pointer but raises no tooltip — synthetic motion does not produce the dwell Avalonia waits on, the same class as accelerators not working through `xdotool` while clicks do. Tooltip *text* stays covered by the headless tests; tooltip *layout in a locale* is covered by nothing, which `AGENTS.md` §9 now says rather than implying the pass swept it |
| The suite passes pinned to `de-DE` | **deferred here, closed by [plan 00015](plans/00015-a-test-that-asserts-english-says-so.md).** 94 of 606 failed. The split recorded at the time — 62 snapshot, 32 behavioural — was made by class name; by failure mode it is **60 and 34**, and *Decisions* supersedes the arithmetic while keeping the conclusion that both halves had to land together. The leg is green and a CI job |

## Plan 00013 phases

| Phase | Size | Status | Notes |
|---|---|---|---|
| 1 The row projection | M | done | `RowProjection` and `FoldedRun`, with the three sites that multiplied a row by the line height routed through it — the connector's `TopOfRow` / `RowAt` and the two bounds it culls against, the map's buckets, and both views' scroll-to. Ships as the **identity**, so the phase's evidence is that nothing else moves. Binary search over the fold starts, so the map's per-row work stays logarithmic on the 200k fixture |
| 2 Folding the runs | M | done | `FoldPlan`: maximal unchanged runs, cut by context, then by the boundary rule, then by a floor of four. `SideBySideDiffView.ApplyFolds` projects each run onto the two line ranges it collapses and each pane collapses them. **`FoldPlaceholderGenerator` landed here rather than in phase 3** — a collapse without a spanning element throws; see *Decisions* |
| 3 The placeholder row | M | done | The click, the run the reader opened being remembered and forgotten with the model, and the test that the padding generator and the placeholder generator share a document rather than displace one another |
| 4 The option and the verbs | S | done | `UnchangedContextRows` on both views; `ShowAllRows`, `ShowDifferencesOnly`, `ShowContext` and `ExpandFold` in the key map unbound; the folding group last on the pane's text menu, both margins and the connector, and absent from the map's; the demo's View menu |
| 5 Evidence | S | done | Four mutation runs, four rendered frames, `DECISIONS.md` (five sections), `AGENTS.md` §6 (five rows) and §7 (one), this file, the changelog |

## Plan 00013 verification

| Done-when item | Result |
|---|---|
| The projection is the identity when nothing is folded | pass: `RowProjectionTests.Nothing_folded_maps_every_row_to_itself` for the arithmetic, and the whole suite staying green for the wiring — 549 tests that would have moved if any of the three sites had changed answer |
| A folded pair of panes has equal extents | pass: `FoldingTests.Folding_the_unchanged_runs_takes_the_same_height_out_of_both_panes` and `Every_surviving_pair_still_shares_a_row_top`. **Mutated one row asymmetric, the spike's version of this failed**, which is why it is asserted against row tops and not against heights alone |
| A fold never starts on a line carrying padding for the rows above it | pass: `FoldingTests.A_fold_never_starts_on_a_line_carrying_padding_for_the_rows_above_it`, at `contextRows: 0` because that is the only value at which the rule fires |
| A fold never ends on a side's last line while that side has a trailing gap | pass: `FoldingTests.A_fold_never_ends_on_a_sides_last_line_while_that_side_has_a_trailing_gap` — the half the spike did not measure, now measured |
| A run shorter than the floor does not fold | pass: `FoldingTests.A_run_shorter_than_the_floor_is_left_alone`, against a floor above the run and against context eating the same budget |
| The placeholder row is one row on each side | pass: `FoldingTests.A_fold_leaves_its_first_line_standing_and_takes_every_line_after_it`. This is the test that catches a fold shifted by one line, which nothing else can: both panes collapse the same *number* of lines, so the extents match and every pair still shares a row top |
| Expanding a fold restores every row top | pass: `FoldingTests.Every_surviving_pair_still_shares_a_row_top` unfolds and re-asserts; `FoldPlaceholderTests.A_click_on_a_placeholder_gives_that_run_back_and_leaves_the_others_folded` does it through a real pointer press at the placeholder's own visual column |
| The map's viewport box follows the visible document | pass: `RowProjectionWiringTests.The_maps_viewport_box_is_measured_against_the_visible_document` |
| The connector still draws what a fold brings into view | pass: `RowProjectionWiringTests.The_connector_still_draws_the_blocks_a_fold_brought_into_view` — the cull's upper bound, which does not misplace a polygon but breaks the loop early and drops every block below the fold |
| Navigation crosses a fold | pass, and **there was nothing to do**: a fold only ever covers `Unchanged` rows and a change block has none, so F7 cannot aim into one. `FoldingReachTests.No_fold_can_hide_a_change_so_navigation_is_never_aimed_into_one` asserts it over every block rather than leaving it to be assumed — *"F7 into a folded run"* is a sentence that sounds like it describes something. **An earlier version of this row said "not done" and was wrong**, on that sentence alone |
| Find crosses a fold | **done in `429b4e0`**, after phase 5 recorded it as the one real gap: a match can be on an unchanged row, which is exactly what a fold covers. Walking to such a match now **reveals** its run, chosen over excluding the match, because a count that changes when you fold describes the view rather than the file. `FoldingReachTests.Walking_to_a_match_inside_a_folded_run_opens_it` |
| The unified view folds the same runs | pass: `FoldingOptionTests.The_unified_view_folds_its_own_runs` |
| The option's three values | pass: `FoldingOptionTests.The_option_folds_and_the_default_folds_nothing`, `The_three_modes_are_the_one_option_written_three_ways`, `A_negative_context_is_coerced_rather_than_kept` |
| Every folding verb is in the key map and unbound | pass: `FoldingOptionTests.All_four_folding_verbs_arrive_in_the_key_map_unbound` |
| The folding group's shape, on every surface that has it | pass: `FoldingOptionTests.Every_surface_that_names_a_run_offers_the_folding_group_and_the_map_does_not` — the separator, the three modes in order, then `ExpandFold`, asserted for four surfaces and denied for the map |
| A rebuild keeps the option and forgets what was opened | pass: `FoldingOptionTests.A_rebuild_keeps_the_option_and_forgets_the_runs_the_reader_opened`. Its first version rebuilt nothing at all: `PaneSource` is a record, so reloading the same text is not a property change |
| Rendered frames, folded and unfolded | pass: `FoldingSnapshotTests`, four frames — folded in both variants, unfolded, and folded with context. Four of the six `MenuSnapshotTests` frames also moved, and **the two that did not are the map's and the header's**, which is exactly the set that should not have |
| Mutations | pass: 7/7 on `RowProjection`, 7/7 on the phase 1 wiring, 7/8 on `FoldPlan`, 6/6 on the placeholder and the expand path. The two survivors are equivalent mutants and are named in the phase commits: an unreachable defensive guard, and a cull bound that only ever under-culls |
| Build and tests | pass: `dotnet build DiffView.slnx -warnaserror` clean, zero warnings; `dotnet test --solution DiffView.slnx` **578 passed** (556 before the plan's first phase) |
| Exercised by hand | pass: driven under §9 on 2026-09-11, against `PaneSource.cs` vs `TextProbe.cs` with `--edit both`, in Dark. Folding reads right at real size — boxed placeholders, both panes level, the line numbers jumping — and the View menu's three modes are a radio group with the one in force selected. **Three findings, all of them things a headless frame could not have given.** `--edit` was missing from the `[DebugFlags] active:` line, so the flag could not be confirmed from the log. The pane's context menu **fits**, twelve entries and no scroll chevron: the overflow its snapshot shows is the 600 px capture window, not the 720 px app, and the reorganising question answers itself as *no*. And *"Show the rows hidden here"* was enabled from the **caret** while the pointer was elsewhere — the entry says "here", and plan 00010's rule is that a menu entry carries what was clicked |
| The menu's expand entry means the pointer, not the caret | pass: `FoldingReachTests.The_menus_expand_entry_is_the_run_under_the_pointer_not_the_one_at_the_caret`, which is the by-hand finding above turned into an assertion. The gesture still means the caret's run, which is the only run a keyboard can name |

## Folding spike

Run on 2026-09-11, before any plan 00013 is written, on branch `spike/folding-feasibility`. Five
headless items in `tests/DiffView.Avalonia.Tests/Spike/FoldingSpikeTests.cs`, against the same two
plain `TextEditor`s and the same padding mechanism as the Phase 1 padding spike — the question was
never whether AvaloniaEdit can fold. *Decisions*, "Folding: go for hiding what matches, and the
price is a row projection", carries the verdict and its reasoning.

| Question | Answer |
|---|---|
| Can a fold keep both panes row-aligned? | **Yes, taken symmetrically over unchanged rows**: `Item1_a_symmetric_fold_of_an_unchanged_run_keeps_both_panes_aligned` — equal extents, every surviving pair still sharing a row top, rows above the fold unmoved and rows below it moved up by the fold on *both* sides. Made one row asymmetric, it fails, so the alignment it asserts is not equal heights by coincidence |
| Can a fold's range be read off a document? | **No**: `Item2_a_fold_that_starts_at_a_padded_line_swallows_padding_belonging_to_the_rows_above_it` — padding is height on a line, so a fold beginning at a padded line takes padding that stands in for rows above it and the panes diverge by exactly that. The range is one row range projected twice |
| Is *Show Same* the same problem mirrored? | **No**: `Item3_a_change_block_has_no_line_to_collapse_on_the_padded_side` — a change block is lines on one side and padding on the other, and padding has no line to collapse. It also shows equal heights are not alignment: the pairs below the block are out by the block |
| What does a fold cost the rest of the control? | **The row-to-pixel equation**: `Item4_rows_and_pixels_stop_sharing_one_scale_once_anything_is_folded` — in-pane drawing walks `TextView.VisualLines` and reads `VisualTop`, so it is unaffected; the connector gutter and the overview map are fed `row × DefaultLineHeight` and are not |
| Is AvaloniaEdit's folding stack wanted? | **No — only its primitive**: `Item5_collapsing_is_a_text_view_primitive_that_needs_no_folding_manager` — `TextView.CollapseLines` writes the height tree and hands back something that uncollapses, while `FoldingManager.Install` would claim a slot in `TextArea.LeftMargins` and generator index 0, which is where the padding generator lives |
| Build and tests | pass: `dotnet build DiffView.slnx -warnaserror` clean, zero warnings; `dotnet test --solution DiffView.slnx` **544 passed** (539 before the spike) |

Two of the five passed for the wrong reason first. The extent assertions were reading a stale
`ExtentHeight`, because collapsing writes the height tree and leaves both the visual lines and the
published extent behind until a redraw and a measure pass — the two steps `PaddingHeightPrimer`
already takes for that reason. And the first fold landed at the top of the document, where a row
that moved cannot be told from a row that never moved; the run is now taken from the second
unpadded block, so the fold has aligned rows on both sides of it.

## Plan 00012 phases

| Phase | Status | Notes |
|---|---|---|
| 1 The margins | done | The region resolved from the event's **source** rather than from pointer-x against the margins' widths, and by identity against the pane's own two rather than by type — which a same-named property shadows into a CS0150. `ContextAt`'s region argument, defaulted since 00010, finally gets a caller. A gutter's menu is the pane's copy and navigate items without save and revert. A margin the library did not draw is left alone; 3 cases |
| 2 The connector and the map | done | `DiffPaneRegion.ConnectorGutter` and `OverviewMap`; a `ContextRequested` handler on each control built from its existing hit-test; **a right-click navigates nothing**, and off every polygon nothing opens. `GoToChange` and `SelectBlock`, both in the key map and unbound, and with them the item lists the plan wrote for the two margins; 13 cases, eleven mutations, eleven kills |
| 3 The header | done | `DiffHeaderContext` and `DiffHeaderContextMenuEventArgs`, `HeaderContextMenuOpening`, `HeaderContextMenu`, `HeaderContextAt`; `DiffPaneMenu.Request` generalised to carry either context type without the menu's behaviour forking. Save and revert from the same `AddFileVerbs` the text menu uses. `LastPaneMenu` renamed `LastMenu`, true since phase 2; 11 cases, ten mutations, ten kills |
| 4 The icon set | done | `CopyArrowGlyph.Geometry` split out so the menu's arrow is the gutter's by construction; `DiffMenuIcons` with the marker margin's `+` `−` `≠` as strokes; both on the inherited `TextElement.Foreground`. 00010's alignment assertion unchanged, its stand-in square moved to an entry the control leaves null; 5 cases, nine mutations, nine kills |
| 5 Evidence | done | `MenuSnapshotTests`, the first snapshots here to capture a popup — six frames; `DECISIONS.md` (three sections, covering both pieces of drift the earlier phases owed), `AGENTS.md` §6 (six rows) and §7 (one), this file, the changelog; the by-hand drive that found the demo's stale label |

## Plan 00012 verification

| Done-when item | Result |
|---|---|
| Each margin reports its own region | pass: `PaneContextMenuTests.Each_gutter_reports_its_own_region_and_the_text_still_reports_text`. **The plan's "the boundary pixel belongs to exactly one of them" is not tested, because the implementation made it meaningless**: the region comes from the event's source, so there is no boundary to fall the wrong side of. The pointer-x version the plan imagined is the one that would have needed it, and is the one plan 00008's misaligned header argues against |
| A right-click on the connector does not jump | pass: `ConnectorAndMapMenuTests.A_right_click_on_the_connector_names_the_polygons_own_block_and_jumps_nowhere`, which also asserts a left-click at the same point still does. The map's half is `A_right_click_on_the_map_names_the_row_it_reports_and_scrolls_nothing` |
| The connector's context names the polygon's own block | pass: same test — the block the polygon draws, not the block nearest the pointer's row, which differs wherever a polygon is tall. The mutation that swaps them kills it |
| The map's context names the row under the pointer | pass: `The_maps_row_is_the_one_a_click_goes_to_not_the_one_the_pixel_starts_at` — against what the map *reports*, and the row a click goes to rather than the row the pixel starts at. **This assertion was vacuous when first written**: on a short fixture every bucket holds at most one row, so `RowForClick` and `RowAtPixel` never disagree and the mutation between them survived. It now runs over 4,000 rows on a 300-pixel map |
| A header's context has no line | pass: `HeaderMenuTests.A_headers_context_names_the_side_and_its_file` — there is no `LineNumber` on the type to be wrong. **This one was vacuous too**: with both sides read-only and neither dirty, a context reading the *other* side passed unchanged. The fixture now differs on every member it asserts |
| The unified view has no connector, map or header menu | pass: `ConnectorAndMapMenuTests.The_unified_view_has_no_connector_and_no_map_to_ask` for the two controls and the two verbs, and `HeaderMenuTests.The_unified_views_headers_raise_no_menu` for the headers — which it *has*, two of them, and answers with nothing because being read-only it has neither verb |
| Every new menu goes through the one implementation | pass: `The_replacement_menu_suppresses_the_opening_event_on_both_new_surfaces` and `The_replacement_menu_suppresses_the_opening_event_on_a_header_too`, the standing test the plan named for the seam; beside it `The_two_replacement_properties_govern_their_own_surface_only`, because one property governing both would be the seam quietly becoming one surface again |
| The icon column still does not move labels | pass: `The_icon_column_is_reserved_whether_or_not_anything_fills_it`, 00010's assertion unchanged, now with real icons in it. It also asserts both kinds of row are present, since alignment means nothing in a menu where every row carries one |
| Every new item carries an automation name | pass: `Every_entry_of_the_two_new_menus_announces_itself` and `Every_entry_of_a_header_menu_announces_itself`. They are built in code, so the XAML sweep cannot see them |
| One rendered frame per new surface | pass: `MenuSnapshotTests`, six frames — each margin, the connector, the map, the header, and the change-marker margin again in Dark. **The first popup captures in this repository**: plan 00010 shipped the pane menu with no frame at all. Opening a menu moves about 8% of a frame's pixels against a comparer that tolerates half a percent, so unlike a chip or an arrow these can fail on their own |
| Exercised by hand, not only headless | pass: driven under §9. The connector, the map and the header each open their menu on a right-click; the connector's is four rows with both arrows pointing opposite ways; the map's showed **`+` Go to this change** over an inserted row; the header's is save, revert and the demo's own entry. **Two findings.** A right-click on the empty connector column opens nothing, which is the rule working and looked like a failure until the polygon's edge was measured. And the demo's status line reported *"unified"* on the connector — its own label, written when a null `Side` could only mean the unified view — now `ConnectorGutter · neither side · line 18 (source line 18) · change 5 · no selection` |
| Build and tests | pass: `dotnet build DiffView.slnx -warnaserror` clean, zero warnings; `dotnet test --solution DiffView.slnx` **539 passed** (504 before the plan). No theme change, so no audit regeneration beyond the ClaudeForge pin bump this branch already carried |

## Plan 00010 phases

| Phase | Status | Notes |
|---|---|---|
| 1 The context and the seam | done | `DiffPaneContext`, `DiffPaneRegion`, `DiffMenuItem` with its `Icon` slot, `DiffPaneContextMenuEventArgs`; `ContextRequested` on the presenter resolving position-or-caret without moving it; `PaneContextMenuOpening` and `PaneContextMenu` on both views over `DiffPaneMenu`, the one binder; `SelectedLines` made public; 13 cases, ten mutations, ten kills |
| 2 The items | done | Copy / navigate / find / save / revert on the side-by-side view, navigate and find on the unified one; labels through `DiffViewStrings`, accelerators through `GestureFor`, the absent-vs-disabled rule through `CommandOrNull`; the demo inserts an entry of its own; shorter per-direction labels and the header gap followed; 12 + 4 cases, fifteen mutations, fifteen kills |
| 3 Evidence | done | The icon-column alignment test; `DECISIONS.md` (including three deviations from the plan), `AGENTS.md` §6 and §7, this file, the changelog |

## Plan 00010 verification

| Done-when item | Result |
|---|---|
| The context names what was clicked | pass: `PaneContextMenuTests.A_right_click_reports_the_line_the_side_and_the_block`, and `An_unchanged_line_has_no_block_and_a_line_past_the_end_throws_nothing` for the trailing padding, where row and block are null and nothing throws |
| The unified view's context has no side | pass: `InlinePaneContextMenuTests.The_unified_context_has_no_side_and_names_the_line_own_file` — `Side` null, `SourceSide` / `SourceLine` naming the line's own file, and `SourceLine` deliberately different from `LineNumber` |
| **A right-click does not move the caret or drop the selection** | pass: `A_right_click_leaves_the_caret_and_the_selection_where_they_were`. The test was written before the handler; the mutation that moves the caret kills it |
| The keyboard raises it at the caret | pass: `The_keyboard_asks_at_the_caret` — no position resolves to the caret's line, not line 1 |
| A host's item survives in the place it was put | pass: the demo inserts *What did I click?* after the copy entries and it is there in both views; `An_emptied_list_opens_no_menu_and_a_host_item_alone_opens_one` covers the amend shape to its limit |
| The menu's shape does not move with the selection | pass: `The_menu_shape_does_not_move_with_the_selection` — same entries, same order, only `IsEnabled` differing. BC's behaviour, captured with and without a selection while drafting |
| A verb the view lacks is absent, not disabled | pass: `InlinePaneContextMenuTests.A_verb_this_view_lacks_is_absent_from_the_menu_not_greyed_in_it` for the list, and `A_verb_the_resolver_has_no_command_for_makes_no_item_at_all` for the rule itself. **The rule's own test came later**: the mutation that greys an absent verb survived at first, because the list test would have passed just as well if nothing built the entries — which is in fact why |
| `PaneContextMenu` replaces, and suppresses the event | pass: `The_replacement_menu_suppresses_the_opening_event`, in both views, which also pins the context arriving as the menu's `DataContext` |
| Accelerators come from the map | pass: `The_accelerator_follows_the_key_map` — rebinding moves it, unbinding removes it. The dependency on plan 00009 this menu exists downstream of |
| Labels match the gutter's | **superseded.** Seen at size the copy entry ran to 41 characters with its accelerator against it; the menu has its own shorter wording and the gutter's tooltips are unchanged. *Decisions* carries the reasoning |
| The icon column is reserved | pass: `The_icon_column_is_reserved_whether_or_not_anything_fills_it` — a solid square in one slot, every row's label still starting at the same x, with a guard against the vacuous version, since an unrealised popup would report every row at zero and agree with itself |
| Every item carries an automation name | pass: `DiffPaneMenu` sets one from the header where a host gives none; the a11y sweep covers the XAML surface as before |
| Exercised by hand, not only headless | pass: driven under §9 in both views. The menu opens on a right-click; the demo's own entry reports *left · line 9 (source line 9) · change 3 · no selection*; accelerators read F7 / Shift+F7 / Ctrl+F; the unified pane's menu has no copies and no save at all |
| Build and tests | pass: `dotnet build DiffView.slnx -warnaserror` clean, zero warnings; `dotnet test --solution DiffView.slnx` **501 passed** (473 before the plan). No theme change, so no audit regeneration |

## Plan 00009 phases

| Phase | Status | Notes |
|---|---|---|
| 1 The table | done | `DiffCommand`, `DiffKeyMap` + `Default()` / `UnifiedDefault()`; `SideBySideDiffView.KeyMap`, `GestureFor`, `CommandFor`; the owned-binding rebuild; `DiffViewLog.KeyGestureConflict`; `CopyToward` / `CanCopyToward` and the `CopyBlockTo*` pair; 10 cases, six mutations, six kills |
| 2 The unified view, and the demo | done | `InlineDiffView.KeyMap` over `DiffKeyMap.UnifiedDefault()`, with `CommandOrNull` behind both the skip and `CommandFor`'s throw; `DiffViewLog.KeyCommandUnsupported`; **`DiffKeyBindings`, the one binder both views call** — phase 1's copy collapsed onto it rather than duplicated; the demo's View ▸ Key bindings submenu, its accelerators read from `GestureFor`, and the copy items moved onto `CopyToward`; 7 cases, seven mutations, seven kills |
| 3 Evidence | done | The mutations above; `DECISIONS.md` (the key table, the owned-binding rule, the one binder, and **plan 00006's non-goal being superseded** — drift, so it lives there and not in that plan), `AGENTS.md` §6, §7 and §9, this file, the changelog |

## Plan 00009 verification

Test names are `KeyMapTests.*` (side-by-side) and `InlineKeyMapTests.*` (unified) unless said
otherwise.

| Done-when item | Result |
|---|---|
| The default map is the bindings that were hard-coded | pass: `KeyMapTests.The_default_map_is_the_bindings_that_were_hard_coded` pins all nine gesture for gesture, plus the two unbound block-always copies |
| Rebinding moves the behaviour, **and the old key stops working** | pass: `Rebinding_moves_the_behaviour_off_the_old_key`, in both views. The second half is the one a rebind test usually forgets |
| Unbinding clears the key and leaves the command callable | pass: `Unbinding_leaves_the_key_doing_nothing_and_the_command_still_callable`, in both views |
| A command with no default can be bound | pass: `KeyMapTests.A_command_with_no_default_can_be_bound` binds `CopyBlockToRight` to F9 and copies the block **while a selection is active** — the behaviour the default no longer has |
| A host's own binding survives a rebuild | pass: `A_binding_the_host_added_survives_a_rebuild`, in both views. Written before the rebuild code, so it failed first |
| A cleared collection stays cleared | pass: `A_cleared_collection_stays_cleared`, in both views — §6's existing promise, still true |
| Two commands on one gesture are both kept and logged once | pass: `Two_commands_on_one_gesture_are_both_kept_and_the_clash_is_logged`, in both views, asserting the warning and that neither binding was dropped |
| `GestureFor` answers the map, `null` included | pass: asserted through the two default tests and both rebind tests — it is the read side plan 00010 depends on |
| The unified view's default is smaller | pass: `InlineKeyMapTests.The_unified_default_is_the_side_by_side_one_less_its_two_sided_verbs` — six bindings, `SwitchPane` and the four copies `null`, the rest identical |
| **A two-sided verb bound on the unified view is skipped and logged, not bound to nothing** | pass: `InlineKeyMapTests.A_two_sided_verb_bound_here_is_skipped_and_logged_not_bound_to_nothing` — assigning `DiffKeyMap.Default()` there leaves six bindings, warns three times at `Warning`, keeps the map reading back what it was given, and F6 does nothing. Beyond the plan's table, and the thing a host will actually hit |
| Alt+Left copies the selection when there is one, and the block when there is not | pass: `KeyMapTests.The_copy_chord_takes_the_selection_when_there_is_one` and `With_no_selection_the_copy_chord_is_the_block_as_before` |
| **The gutter and the chord agree on the contested cell** | pass: `KeyMapTests.The_gutter_and_the_chord_agree_on_the_contested_cell` — a selection beginning on a block's anchor row draws the selection's arrow and fires the selection's copy. This is the assertion the behaviour change exists for |
| Every new test proven able to fail | pass: phase 1 six mutations / six kills, phase 2 seven mutations / seven kills. `A_cleared_collection_stays_cleared` is the exception in both views — it re-asserts §6's standing promise rather than new behaviour, and carries no mutation of its own |
| Exercised by hand, not only headless | pass: driven under §9 with `xdotool` in both views. F7 walks the blocks; View ▸ Key bindings ▸ Rebind moves navigation to Ctrl+Down / Ctrl+Up; F7 then does nothing, Ctrl+Down / Ctrl+Up walk the blocks, and the submenu's labels follow to `Ctrl+Down Arrow` / `Ctrl+Up Arrow`. The same sequence holds under `--unified`, which is what phase 2 is about |
| Build and tests | pass: `dotnet build DiffView.slnx -warnaserror` clean, zero warnings; `dotnet test --solution DiffView.slnx` **473 passed** (466 before phase 2). No theme change, so no audit regeneration |

## Plan 00008 phases

| Phase | Status | Notes |
|---|---|---|
| 1 Placement | done | `MinimapPlacement` over an `Auto` slot at each end of both grids; `DiffMinimap.MirrorEdges` and the one geometry helper the lanes, the marker and the ticks read; the headers grid re-laid to match the panes; `ApplySplit` off hardcoded indices; the demo's View menu |
| 2 Evidence | done | `MinimapSnapshotTests.Docked_left_the_lanes_stay_and_the_edges_mirror` in both variants; the header-alignment test in both placements; six mutations, six kills |

## Plan 00008 verification

| Done-when item | Result |
|---|---|
| The map docks outside either pane | pass: `MinimapLaneTests.The_map_docks_outside_the_panes_either_way` — its far edge is at or before the left pane docked left, at or after the right pane docked right, with `MirrorEdges` following |
| The lanes do not flip | pass: same test asserts the left lane stays left of the right in both placements, and `MinimapSnapshotTests` reads the lanes through `LaneAt` in both — a flipped lane would be read where it moved to and the notch assertion would fail |
| The marker hugs the panes | pass: `Docked_left_the_lanes_stay_and_the_edges_mirror` — the current block's colour is on the map's right edge docked left and its left edge docked right, and absent from the other |
| A host setting the placement in XAML is wired | pass: `A_host_setting_the_placement_before_the_template_applies_is_wired_too` |
| **Docking right moves no frame** | **failed, and the plan was wrong to expect it.** 22 frames moved — not from the placement mechanism but because the same change corrects a header/pane misalignment that had been in the control since plan 00004. *Decisions* has the arithmetic |
| Each header is as wide as its pane, and starts where it starts | pass: `Each_header_is_exactly_as_wide_as_its_pane`, both placements, asserting **width and x**. The width alone had been true for four plans while the x was 8 px out |
| `dotnet build DiffView.slnx -warnaserror` | clean |
| `dotnet test --solution DiffView.slnx` | 456 passed (449 before the plan) |
| `theme-audit compat` then `report` | regenerated after both grids changed: 0 low-contrast findings |
| New tests proven able to fail | six mutations, six kills: the lanes made to follow the dock, the marker pinned to one edge, the map never moved out of its template column, the header spacers frozen, the split ratio written into the old indices, and the placement missing from the template path |
| Snapshot baselines moved | **22** for the header correction, and 6 regenerated in the minimap set — 4 because the evidence now navigates to a block, 2 new for the left dock |

## Plan 00007 phases

| Phase | Status | Notes |
|---|---|---|
| 1 Two lanes | done | `DiffMinimap.BucketKinds(DiffSide)` over `SideBySideDocument.LineOf`; the 22 px column — a marker column, two 8 px lanes, the gap, the margin — with the template's column now `Auto`; `KindOfBucket(bucket, side)`, `LaneAt`, the lane-naming tooltip on a new `Minimap.LaneTooltip` string |
| 2 Drag, wheel, toggle | done | `ViewportBounds` public as the boundary between the gestures; drag with pointer capture, `OnPointerWheelChanged`, and `SideBySideDiffView.ShowMinimap` wired on both paths, with the demo's View → Show overview map |
| 3 Evidence | done | `MinimapSnapshotTests` — a left-only block in both variants and both palettes, the drawing asserted against the buckets the map reports; `MinimapLaneTests`, 9 cases; six mutations, six kills |

## Plan 00007 verification

| Done-when item | Result |
|---|---|
| A one-sided block inks one lane and notches the other | pass: `MinimapLaneTests.A_deletion_inks_the_left_lane_and_notches_the_right` and `An_insertion_is_the_mirror`; `A_modification_inks_both_lanes` is the third case |
| The lanes are the model's | pass: `Every_lane_bucket_is_one_the_model_puts_there` — every bucket of both lanes against the model, plus the independent invariant that the two lanes together equal the single-lane reading the map has always given |
| The map costs its width, and nothing when off | pass: `The_map_costs_its_width_and_nothing_when_it_is_off` — 22 px on, and the panes gain exactly that when off. `A_host_setting_the_toggle_before_the_template_applies_is_wired_too` covers the XAML path |
| Drag and jump do not contest a pixel | pass: `A_press_outside_the_viewport_jumps_and_a_press_inside_it_drags` — one jump outside, three from a press-and-two-moves inside, and nothing after the release. The first version of this test read `ViewportBounds` **before** the outside click, which scrolls: the box had moved by the time it pressed "inside", so it pressed outside and saw one jump |
| The wheel scrolls the panes | pass: `The_wheel_over_the_map_scrolls_the_panes` |
| The tooltip names the lane | pass: `The_tooltip_names_the_lane` — and `LaneAt` says the marker column belongs to neither side |
| The rendered evidence | pass: `MinimapSnapshotTests`, 4 frames. **A lane is thin lines, not a band**, whenever rows are sparser than pixels: the assertion is the drawing against the buckets the map reports, after a first version expecting a solid band failed at 58 of 228 |
| `dotnet build DiffView.slnx -warnaserror` | clean |
| `dotnet test --solution DiffView.slnx` | 449 passed (436 before the plan) |
| `theme-audit compat` then `report` | regenerated after the template's column changed: 0 low-contrast findings. **No new token**: the lanes reuse `MarkerFor(kind)`, which both palettes define and `contrast-pairs.json` already scores |
| New tests proven able to fail | six mutations, six kills: the side filter dropped, the drag branch disabled, the drag's moves dropped, the wheel silenced, and the toggle missing from each of its two paths |
| Snapshot baselines moved | **44**, every composite frame, because the column grew from 14 px to 22 and the panes are narrower. Genuine failures, not a silent drift — the geometry moved far more than the comparer's tolerance |
| Seen in a running window | **yes** — the first plan here for which that is true. `AGENTS.md` §9 |

## Plan 00006 phases

| Phase | Status | Notes |
|---|---|---|
| 1 The arrow reads as a shape | done | `CopyArrowGlyph.Draw` takes a fill and a pen; twelve tokens across both palettes and both variants — the block arrow's fill and the selection arrow's outline, fill and tail bar; four contrast pairs, **two of them for a block arrow that had never been scored**; the hand cursor over an arrow landed just before, in `c88d706` |
| 2 Copying a selection | done | `CanCopySelection` / `CopySelection` over the row mapping, sharing `CopyLines` with `CopyBlock`; `DiffPanePresenter.SelectedLines` and `CopySelectionRequested`; the selection arrow with its own colours and its tail bar; first-row placement, the contested-cell rule, the tooltip, the hand cursor; `TextArea.SelectionChanged` reaching the margin as a counted notice |
| 3 Evidence | done | `SelectionArrowSnapshotTests` — twelve frames across three cases, both variants, both palettes; four mutations, three kills and **one deliberate survivor**; the goldenrod block arrow Brian asked for, with the audit regenerated; `DECISIONS.md`, `AGENTS.md`, this file and the changelog |

## Plan 00006, Phase 3 verification

| Done-when item | Result |
|---|---|
| Both arrows in one frame | pass: `SelectionArrowSnapshotTests.Both_arrows_are_painted_in_one_frame` — a selection starting on an unchanged row, so its arrow and both block arrows are on three different rows; each fill present in its own zones and absent from the others' |
| A selection spanning padding | pass: `A_selection_spanning_padding_is_painted` — the selection starts an unchanged line above the block and runs over two rows the right side has no lines for; the right's arrow is in padding and costs no number |
| The contested cell | pass: `The_selection_arrow_takes_the_block_s_cell` — the right pane is the control, with two block arrows; the left has one, and its selection arrow is level with the right pane's on the row it took |
| Nothing strayed | pass: in all twelve, the selection fill counted over the **whole margin** equals the count inside the one reported zone, and the zone's right edge is `LastColumnRight` |
| Both palettes, both variants | pass: 12 frames — 3 cases × Light/Dark × Default/ColorBlind. The colour-blind frames are the only committed evidence of the tail bar, which exists in no other frame |
| `dotnet build DiffView.slnx -warnaserror` | clean |
| `dotnet test --solution DiffView.slnx` | 436 passed (424 before the phase) |
| `theme-audit compat` then `report` | regenerated after the palette change: **0 low-contrast findings**, drift test passes |
| New tests proven able to fail | four mutations, three kills — a strayed arrow and an arrow on every row each kill all twelve; the swapped arrow order kills exactly the four collision frames. **The fourth survives on purpose**: removing the tail bar leaves all twelve green, which is §5's trap measured rather than asserted, and is why `CopySelectionTests.The_selection_arrow_carries_the_palette_s_share_of_shape` is the bar's real guard |
| Snapshot baselines moved | **12 by the slate-to-goldenrod change and 6 more by goldenrod-to-yellow**, at 0.03 %–0.11 % against the comparer's 0.5 % — not one would have failed on its own, and both sets were found with the §5 sweep. Every colour-blind frame is byte-identical throughout, which is the evidence that only the default palette moved; the second pass moved Light frames only, which is the evidence that only the Light fill did |

## Plan 00006, Phase 2 verification

| Done-when item | Result |
|---|---|
| A selection offers an arrow, on its first row | pass: `CopySelectionTests.A_selection_offers_an_arrow_on_its_first_row` — one arrow over the selection's first line, flush with `LastColumnRight`, and that line's number is the only one the frame is missing |
| No selection, no arrow | pass: `Clearing_the_selection_takes_the_arrow_with_it`. The first version asserted the frame and **passed with the `SelectionChanged` wiring removed**: a headless capture re-renders every visual whether or not it was invalidated, so no frame can show an invalidation. `DiffLineNumberMargin.SelectionNotices` counts the notice instead, and the mutation dies |
| A read-only neighbour offers nothing | pass: `A_read_only_neighbour_offers_nothing` — no arrow, `CanCopySelection` false, and `CopySelection` writes nothing |
| The copy takes whole lines | pass: `The_copy_takes_whole_lines` — a selection from inside line 2 to inside line 3 copies both lines whole. `A_selection_reaching_the_next_line_s_first_column_stops_above_it` pins the other end of that rule: a drag onto the next line's first column stops above it |
| The copy lands in the aligned rows | pass: `The_copy_lands_in_the_aligned_rows` — the other side's lines in those rows are replaced, the line below is untouched, and the re-diff collapses the block |
| A selection over padding inserts | pass: `A_selection_over_padding_inserts` — two left lines whose rows the right side has no lines in at all land between the right's own lines |
| The selection arrow wins the cell | pass: `The_selection_arrow_wins_the_contested_cell` — one zone in that cell, the selection's, at exactly the rectangle the block's arrow had, and `LastCopyArrows` is empty. Clearing the selection hands the cell back to the block rather than to the number, which the clearing test asserts |
| The two arrows are told apart | pass: `The_selection_arrow_is_painted_in_its_own_colours` — each fill is present in its own zone and absent from the other's — and `The_selection_arrow_carries_the_palette_s_share_of_shape`, which reads the tail as luminance so no colour survives: **9 inked rows against the block arrow's 5** in the colour-blind palette, and exactly the block arrow's 5 in the default one, where the bar token is transparent |
| A click copies, and the pointer says so | pass: `A_click_on_the_selection_arrow_copies_the_selection_out` — a hand over the zone, and a click that lands the selection on the other side |
| The tooltip names what it copies | pass: `The_selection_arrow_s_tooltip_names_what_it_copies` — the displaced number and *the selected lines*, not the block the row sits in |
| `dotnet build DiffView.slnx -warnaserror` | clean |
| `dotnet test --solution DiffView.slnx` | 424 passed (411 before the phase); 434 with `-p:IncludePerfTests=true` |
| New tests proven able to fail | nine mutations, nine kills, after one survivor — the frame-only selection test above, which is why the notice is counted |
| Snapshot baselines moved | **none.** The block arrow is drawn exactly as it was — the tail bar is absent unless a caller passes one — and no committed frame holds a selection |

## Plan 00006, Phase 1 verification

| Done-when item | Result |
|---|---|
| The arrow is outlined and filled | pass: `CopyArrowMarginTests.The_arrow_is_outlined_and_filled` — both colours inside one zone. The outline is a one-pixel pen, so most of it is a blend rather than the token exactly and it is counted at tolerance 24, where it is a reliable 7–8 per zone against the fill's 15 |
| Every arrow colour clears its floor | pass: `theme-audit report` with the four new pairs — 0 low-contrast findings. Outlines are 7.84–10.58 against their gutters and fills 3.97–5.78 |
| The direction still reads | pass: `CopyArrowSnapshotTests.The_copy_arrows_are_painted_in_the_panes`, rewritten. The old heavier-half rule **reversed** when the outline arrived: the shaft's long edges put ink on the tail side and the head's interior is eaten by its own border. The silhouette is symmetric anyway — a 1 px tip inset plus a 5 px head puts the widest column 6 from either end of a 12 px zone — so the *fill*, which the pen offsets towards the head, is what now says which way the arrow points |
| No arrow ink where there are no arrows | pass: `A_read_only_pair_shows_every_number_instead` — measured on the **outline** only. The dark fill `#78909C` matches 16 pixels of the line numbers' own anti-aliasing, so the fill cannot answer "is this an arrow"; the outline is 0 in a read-only margin and 13–15 with arrows |
| `dotnet build DiffView.slnx -warnaserror` | clean |
| `dotnet test --solution DiffView.slnx` | 411 passed (410 before the phase); 421 with `-p:IncludePerfTests=true` |
| New tests proven able to fail | three mutations, three kills: a zero-width outline, an outline in the fill's colour, and a fill in the outline's. A fourth — removing the pen — **does not compile**, `IPen` being non-nullable, so an arrow with no outline is not expressible rather than merely untested |
| Snapshot baselines moved | 6, none of which failed on its own: the two copy-arrow frames, the one-sided-block frames and the plan 00003 marks frames |

## Plan 00005 phases

| Phase | Status | Notes |
|---|---|---|
| 1 The tokens and the contract | done | Twelve opaque `MarkerChip*` brushes across both palettes and both variants, each its marker over that variant's pane background; `DiffBrush` entries and `MarkerChipFor(kind)`; three new pairs in `contrast-pairs.json`; audit regenerated. Nothing drawn, so no frame moved — the phase's output is that a floor nothing had scored is now scored |
| 2 The chip | done | One rounded chip per run of same-kind rows, symmetric about the margin's centre, stopping where the modified-since-load bar begins; the glyph centred in it; the viewport-edge rule; 6 headless test cases |
| 3 Evidence | done | `MarkerChipSnapshotTests` — a run sharing one chip beside a lone badge, and a run scrolled past both edges, in both variants; six mutations; this section and `DECISIONS.md` |

## Plan 00005 verification

| Done-when item | Result |
|---|---|
| One chip per run | pass: `The_chips_are_exactly_the_runs_the_model_describes` — the runs are derived from the kinds the margin reported, on both panes of the small fixture, and compared to the chips it drew, kind for kind and height for height |
| A lone changed row is a badge | pass: `A_lone_changed_row_gets_a_badge_of_the_same_shape` — one row's height less the inset, same corner radius as a five-row band |
| A run leaving the viewport is not rounded there | pass: `A_run_scrolled_off_the_top_is_not_rounded_there` — scrolled into the middle of a 40-line deletion, the chip starts a **full row** above the edge and ends below it. The first version of this assertion only required a negative top, which holds either way when the first visible row starts mid-line; the mutation that stopped extending the run survived it |
| The glyph is centred in its chip | pass: `The_glyph_is_centred_in_its_chip` — glyph ink weighed either side of each chip's centre line, within a third of the heavier side. Reported rectangles cannot show this: the off-centre draft reported the place it drew |
| The chip stops where the bar begins | pass: `The_chip_stops_where_the_modified_since_load_bar_begins` — symmetric bounds, right edge at `Bounds.Width - ModifiedBarWidth`, and an edited line inside a changed block paints chip and bar both |
| The margin does not grow | pass: `The_chip_costs_the_margin_no_width` — 16 px with chips and without |
| Every marker clears its floor with the chip behind it | pass: `theme-audit report`, 0 low-contrast findings with the three new pairs present. Proven to bite: the Default Light chip set back to the blended value yields 6 findings and prints **2.60** against the 3.0 floor |
| No palette colour changed | `#D96A00` and Okabe–Ito's `#D55E00` are as they were. An earlier draft darkened both; compositing over the pane rather than the gutter made that unnecessary |
| `dotnet build DiffView.slnx -warnaserror` | clean |
| `dotnet test --solution DiffView.slnx` | 409 passed (399 before the plan) |
| New tests proven able to fail | six mutations, six kills, after a first pass with three survivors: one weak assertion (the viewport top), one unreachable guard (the run's padding test, now removed with the invariant written down), and one provably equivalent expression (the glyph's centring, kept and documented rather than tested) |
| Snapshot baselines moved | **30**, across 13 test methods, **none of which failed on its own** — a chip is glyph-scale against a 0.5 % tolerance. Found with the zeroed-comparer sweep, now the documented procedure and used for the fourth time in this branch |

## The change markers, weighed and replaced

| Done-when item | Result |
|---|---|
| The vocabulary is pinned | pass: `MarkerGlyphTests.The_vocabulary_is_one_family_of_operators` — `+`, `−` (U+2212), `≠` (U+2260), and nothing for an unchanged line |
| Every marker is heavy enough to scan | pass: `Every_marker_is_heavy_enough_to_scan` — a pair carrying one block of each kind, each marker's cell measured against the gutter background, all three over a 20-pixel floor. This is the property the change exists for, so it is the assertion rather than a detail beside one |
| Weight alone was not the fix | recorded: `+` `-` `~` semibold weighs 41 / **14** / 21 — a hyphen is a short bar at any weight, so the character had to change. The mutation that drops the weight and the two that restore the old characters each fail the ink test |
| `dotnet build DiffView.slnx -warnaserror` | clean |
| `dotnet test --solution DiffView.slnx` | 399 passed (397 before) |
| New tests proven able to fail | three mutations, three kills: the hyphen restored, the tilde restored, and the semibold weight dropped |
| Snapshot baselines moved | **30**, across 13 test methods — every frame that draws a changed row, `PresenterSnapshotTests` and `InlineSnapshotTests` included. **Not one of them failed on its own**: a glyph swap moves fewer pixels than the comparer's 0.5 % tolerance, so they were stale and green. They were found by zeroing `ChannelTolerance` and `MaxDifferingFraction` for one run, regenerating exactly what that flagged, and restoring the comparer — the third time in this branch that a change too small for the comparer had to be caught deliberately |

## Plan 00004 phases

| Phase | Status | Notes |
|---|---|---|
| 1 The arrow in the number cell | done | `DiffLineNumberMargin` draws a copy arrow on each block's anchor row, over the number, right-aligned to the numbers' edge; `DiffPanePresenter.CanCopyOut` carries the *other* side's flag; `PaneMetadata.BlockAtRow` and `ChangeBlock.LinesFor(side)` added; the padding and trailing-padding paths for a block a side has no lines in; `CopyArrowGlyph` extracted so the column and the margin drew the same arrow; 7 headless test cases, seven mutations killed |
| 2 The copy, and the column | done | The margin hit-tests the arrow before the row and raises `CopyOutRequested`, which the composite turns into a `CopyBlock` onto the other side; the anchored row's tooltip names the hidden number and the copy; `ChangeConnectorGutter` loses five public members and narrows to 16 px; two connector-arrow tests retired with the decision they pinned; 4 headless test cases, six mutations killed |
| 3 Evidence | done | `CopyArrowSnapshotTests` — the editable pair, the same pair read-only, and a one-sided block, in both variants; the direction asserted by which half of the zone the glyph's head fills; `LastColumnRight` exposed so a strayed arrow cannot report its own new home as correct; `DECISIONS.md` gains *The copy arrow takes the line number's cell* and marks the hit-order rule retired; 6 snapshot test cases, four mutations killed |

## Plan 00004 verification

| Done-when item | Result |
|---|---|
| The arrow is offered by the side it would copy from | pass: `The_arrow_is_offered_by_the_side_it_would_copy_from` — read-only shows none; making the right side editable puts arrows in the **left** pane and none in the right; and the reverse. `A_side_editable_before_the_template_applies_is_wired_too` covers the path a host setting the flag in XAML takes, which the property-change handler never sees |
| One arrow per block, on its first row | pass: `One_arrow_per_block_on_the_block_s_first_line` — exactly one arrow per block, each over the block's first line on that side, or over no line where the side has none |
| The hidden number is the only one hidden | pass: `The_only_numbers_missing_are_the_anchored_ones` — the numbers drawn with arrows on equal the numbers drawn with them off, minus exactly the anchor rows |
| A block a side has no lines in still offers its arrow | pass: `A_block_the_side_has_no_lines_in_puts_its_arrow_in_the_padding` and `A_one_sided_block_at_the_very_end_is_reached_in_the_trailing_padding` — the arrow lands in padding, costs no number, and the trailing case is reached even though a walk over the visual lines never gets there |
| The arrow costs no horizontal space | pass: `The_arrow_costs_the_margin_no_width` — the measured width is identical with arrows off and on, and exceeds the glyph. This is the premise of drawing over the number rather than beside it, so it is asserted rather than assumed |
| A click on the arrow copies; a click on a number does not | pass: `A_click_on_the_arrow_copies_the_block_out` and `A_click_on_a_number_still_only_moves_the_caret` — the copy lands on the *other* side and collapses the block; the cell below the arrow puts the caret on line 3 and copies nothing |
| The tooltip carries what the arrow displaced | pass: `The_anchored_row_s_tooltip_carries_the_number_it_stands_in_for` — the anchored row's tooltip names the line and the copy; a row without an arrow says nothing about copying |
| The connector column has no arrows left | pass: `The_connector_column_has_no_arrows_left` — with **both** sides editable, the state that used to paint two arrows per block, the column carries zero pixels of the arrow brush and measures 16 px |
| The arrows are painted, and point the right way | pass: `The_copy_arrows_are_painted_in_the_panes` — the head carries about twice the shaft's area, so the heavier half of the zone is the half the arrow points at, which needs no threshold and dies when the glyph is flipped. Every arrow pixel in each margin belongs to a zone the margin reported, and each zone is flush with `LastColumnRight` |
| A read-only pair shows every number | pass: `A_read_only_pair_shows_every_number_instead` — not one pixel of the arrow brush in either margin, and every line the pane shows carries its number |
| Both arrows on a row sit level | pass: `A_one_sided_block_puts_its_arrow_in_the_padding` — the arrow over a number and the arrow in padding are at the same window y. They were 1.2 px apart until the anchored one was centred on its row rather than on its text band |
| `dotnet build DiffView.slnx -warnaserror` | clean |
| `dotnet test --solution DiffView.slnx` | 396 passed (390 before phase 3, 383 before the plan) |
| New tests proven able to fail | seventeen mutations across the three phases, seventeen kills. Three survived a first pass: one test gap (the template-apply wiring), one code gap (a guard on an invariant its neighbour already enforced, now removed), and one badly aimed mutation. A fourth survived phase 3 until `LastColumnRight` existed — an arrow shifted four pixels reported its own new position, so every assertion followed it |
| Theme audit regenerated | `theme-audit compat` then `report` after the column width changed under `Themes/`: 0 low-contrast findings, drift test passes |
| Snapshot baselines moved | 22 regenerated for the narrower column — composite, find, navigation, syntax, view options, word diff, demo and the plan 00003 marks frame. `PresenterSnapshotTests` and `InlineSnapshotTests` are untouched, which is the evidence that only the composite's geometry moved |

## The modified-since-load bar, corrected

| Done-when item | Result |
|---|---|
| The bar covers what this session edited, and no more | pass: `The_bar_covers_the_line_s_own_row_and_not_the_padding_above_it` — on a line carrying two padding rows above it, the padding band holds zero pixels of the bar brush and the line's own row holds them. Before the fix that band held 68, which is what the test was written to see fail |
| Nothing else moves | the plan 00003 marks snapshot is byte-identical: its edited line 1 carries no padding above it, so the bar already covered exactly one row there. The defect only ever showed on a padded line, which no committed frame had |
| `dotnet test --solution DiffView.slnx` | 397 passed (396 before) |

## Plan 00003 phases

| Phase | Status | Notes |
|---|---|---|
| 1 Typing | done | No source change was needed. `LeftReadOnly` / `RightReadOnly` already reached the panes, and no layer turned out to rely on the document being immutable; 5 headless test cases, each proven able to fail |
| 2 Live re-diff | done | `LiveReDiff` / `ReDiffDelay` / `ReDiffNow()` / `IsEdited(side)`; `EffectiveSource` builds from the live document while keeping the source's encoding, path and title; the document, caret, selection, scroll and undo stack survive a rebuild; find results invalidated on edit; 6 headless test cases, each proven able to fail |
| 3 Dirty and save | done | `IsDirty` / `CanSave` / `Save` / `Revert` and `SaveOutcome`; `PaneWriter` in Core round-trips the encoding, the byte-order mark and the line endings; a `(LastWriteTimeUtc, Length)` stamp catches someone else's write and follows our own; the header carries a dirty marker; 20 unit and headless test cases, each proven able to fail |
| 4 Copy to side | done | `CanCopyBlock` / `CopyBlock` / `CopyCurrentBlock`, `CopyToLeftCommand` / `CopyToRightCommand` on Alt+Left and Alt+Right; per-block arrows in the connector gutter on a new `DiffView.GutterArrowBrush`, hit-tested before the polygon they sit inside; 10 headless test cases, five mutations killed and two survivors that removed a dead branch and a wrong one |
| 5 Feedback and polish | done | `ModifiedLines(side)` tracked across edits that move lines, drawn as a bar down the marker margin on a new `DiffView.ModifiedSinceLoadBrush` and explained in its tooltip; the strip names the sides holding unsaved edits; a revert clears both; 6 headless test cases, each proven able to fail |
| 6 Scale and hardening | done | `EditScalePerfTests` on the 200k pair — a re-diff costs 403 ms because a rebuild re-primes only what moved (4,000 lines), not the 1,243 ms a load from cold takes; the debounce collapses 20 keystrokes into 1 build; live re-diff is affordable at 200k and DiffPlex stays unvendored; 3 `Perf` measurements |

## Plan 00003, rendered evidence

Plan 00001 gave every phase that drew something a snapshot; plan 00003 did not, and its phases
assert only what the gutter and the margin *recorded*. `EditingSnapshotTests` closed that.

**Two of its four frames, and the three rows below them, were retired by plan 00004**, which moved
the copy arrows out of the connector column and into the panes' number margins:
`The_copy_arrows_are_painted_on_both_edges_of_the_gutter` no longer exists, and
`CopyArrowSnapshotTests` is where an arrow is now photographed. The rows are struck through rather
than deleted, so a reader who remembers them finds out where they went — the same treatment
`DECISIONS.md` gives the hit-order rule.

| Done-when item | Result |
|---|---|
| ~~The copy arrows are painted, and only where the gutter says~~ — retired by plan 00004 | ~~pass: `The_copy_arrows_are_painted_on_both_edges_of_the_gutter`~~ — with both sides editable every block carries two arrows; each zone holds more than 20 pixels of `DiffView.GutterArrowBrush`, and the count over the whole column equals the sum over the zones, so an arrow drawn anywhere else would fail even though it moves far too few pixels for the snapshot comparer to notice |
| ~~The arrows' geometry~~ — retired by plan 00004 | ~~pass: same test~~ — the leftward arrow starts at the column's left edge, the rightward one ends at its right, the two are level and do not overlap. The assertions pin that relationship rather than `ArrowSize`, so the glyph inside the zone can be redrawn without touching a test |
| ~~The arrows read as arrows~~ — retired by plan 00004, and worth reading anyway | the first frames showed the pair as one bowtie: two bare triangles meeting in the middle of the 24 px column. Each is now a head and a shaft with `InnerGap` unpainted at the centre, on Brian's comparison with Beyond Compare, whose arrows carry a shaft for the same reason. **The four snapshots passed unchanged across that redraw** — a 12 px glyph is 0.03% of a 900×600 frame and the comparer tolerates 0.5%, so the baselines were regenerated deliberately rather than caught. The pixel assertions, not the PNGs, are what guard a glyph this small |
| The modified-since-load bar is painted where it belongs | pass: `The_marks_an_edit_leaves_are_painted_and_named` — after one keystroke on line 1 the bar is in `DiffView.ModifiedSinceLoadBrush` at the marker margin's inner edge, absent from the margin's outer half where the diff's glyph sits, and absent from an unedited line |
| The dirty markers are on screen | pass: same test — the left header's `PART_Dirty` is visible, reads "Unsaved" and paints in the warning brush; the right header is not dirty; the strip's lane names the left side |
| What the pixels add over Phase 5 | with `RenderModifiedBar` mutated to draw nothing, all six `EditFeedbackTests` still pass and only the new test fails. The bookkeeping and the painting are separate claims, and until now only the first had evidence |
| `dotnet build DiffView.slnx -warnaserror` | clean |
| `dotnet test --solution DiffView.slnx` | 383 passed (379 before) |
| New tests proven able to fail | six mutations, six distinct failures: recording the leftward arrow's zone without drawing it; moving the rightward arrow off the column's edge; painting the arrows in the connector brush; drawing the bar at the margin's outer edge; recording a modified line without drawing its bar; and clearing the header's dirty marker |

## Plan 00003, Phase 6 verification

| Done-when item | Result |
|---|---|
| The re-diff loop measured on the 200k pair | pass: `EditScalePerfTests.An_edit_to_the_200k_pair_re_diffs_re_primes_and_repaints` — one keystroke costs 71 ms, the frame drawn while the model is stale costs 1 ms, the re-diff through prime and layout costs 403 ms of which the engine is 278 ms, and the frame after costs 13 ms. The control ends `Ready`, undegraded, with the two extents still equal |
| The priming cost per keystroke | pass: same test — a rebuild re-primes **4,000 lines, not 204,001**. Priming is proportional to the padding that moved, not to the document, which is why a rebuild is a third of the 1,243 ms a load from cold takes |
| The debounce holds at scale | pass: `Typing_a_run_of_keys_coalesces_into_one_rebuild_at_scale` — twenty keystrokes inside the window cost 378 ms in total (18.9 ms per key) and produce exactly **one** build, not twenty |
| The escape hatch is worth having | pass: `With_live_re_diff_off_a_keystroke_costs_nothing_beyond_the_keystroke` — with `LiveReDiff` false the same twenty keystrokes cost 298 ms (14.9 ms per key), the model does not move however far the clock is advanced, and `ReDiffNow()` rebuilds in 366 ms on demand. Live re-diff therefore costs about 4 ms per keystroke in bookkeeping |
| The measurement decides the open question | **Live re-diff is affordable at 200,000 lines**, so the threshold Phase 2 hedged against does not exist at this size and none is imposed. The DiffPlex vendoring decision closed in plan 00001 stays closed: the engine is 278 ms of the 403, well inside a debounce the user does not wait on, because the previous model stays on screen throughout |
| `dotnet build DiffView.slnx -warnaserror` | clean |
| `dotnet test --solution DiffView.slnx` | 379 passed; with `-p:IncludePerfTests=true`, **389** — the 10 `Perf` tests, 3 of them new |

## Plan 00003, Phase 5 verification

| Done-when item | Result |
|---|---|
| Modified-since-load marks in the marker margin | pass: `EditFeedbackTests.The_margin_draws_a_bar_on_the_lines_this_session_changed` — the margin's `LastModified` is empty before any edit, carries exactly the edited line after one, and stays empty on the pane that was not touched |
| The marks follow the text, not the line number | pass: `An_edited_line_is_marked_and_the_mark_moves_with_the_line` — a line is edited, then a whole line is inserted above it, and the mark moves down with the text it belongs to; `A_multi_line_insert_marks_every_line_it_added` covers the other half, where three inserted lines are all this session's work rather than only the one the caret sat on |
| The tooltip explains the mark | pass: `The_margin_tooltip_says_a_line_was_edited_even_where_the_diff_is_silent` — with identical sides the marker tooltip is normally null, and an edited line still gets one; on a line inside a change block the note is appended to the block summary, so a line that is both says both |
| Unsaved-changes state | pass: `The_strip_names_the_sides_holding_unsaved_edits` — the strip's lane is null while nothing is dirty, names the left side once it is edited, and names both once both are |
| Reverting clears the feedback | pass: `Reverting_clears_the_marks_and_the_lane` — after a revert the tracked set is empty, the margin draws no bars, and the strip's lane is null again |
| Automation names on the new decorators | pass: the strip's lane and the header's dirty marker each carry their text as `AutomationProperties.Name` and as a tooltip, so neither is colour alone; the margin's bar is described through `TooltipFor`, which the existing decorator sweep already covers |
| `dotnet build DiffView.slnx -warnaserror` | clean |
| `dotnet test --solution DiffView.slnx` | 379 passed (373 before the phase) |
| New tests proven able to fail | six mutations, six distinct failures: not shifting the marks when lines move; marking only the first line of a multi-line insert; leaving the marks behind on a revert; never drawing the bar; never naming an unsaved side in the strip; and returning null from the tooltip where the diff has nothing to say. The multi-line case survived its mutation on the first pass, which is why `A_multi_line_insert_marks_every_line_it_added` exists |
| Theme audit regenerated | `theme-audit compat` then `report` after the new `DiffView.ModifiedSinceLoadBrush`: 0 low-contrast findings, drift test passes |

## Plan 00003, Phase 4 verification

| Done-when item | Result |
|---|---|
| A block's lines replace the other side's, and the block collapses | pass: `CopyToSideTests.Copying_a_modified_block_collapses_it_and_leaves_the_sides_equal_there` — one modified block copied rightwards leaves the right document equal to the left and the change count at 0 once the re-diff lands |
| Insertions and deletions both work | pass: `Copying_an_insertion_puts_the_missing_lines_in_and_copying_back_takes_them_out` — copying the block rightwards inserts the line the right side lacked; copying the same block leftwards, in a fresh host, removes it instead |
| The document's ends are handled | pass: `Copying_a_block_at_the_very_end_does_not_strand_a_terminator` — appending past a last line that carries no terminator puts one in front, rather than joining the runs; `Copying_onto_an_unterminated_last_line_gives_it_the_source_terminator` is the mirror, where keeping the source's terminator is what makes the sides identical |
| Every block copied leaves the sides identical | pass: `Copying_every_block_makes_the_sides_identical` — the small fixture's blocks copied back to front, so an earlier copy cannot move a later block's lines out from under it, ending with equal documents and no changes |
| Undo takes a copy back | pass: `A_copy_is_one_undo_away_from_never_having_happened` — the copy goes through the editor's own document, so one undo restores both the text and the change count |
| A read-only target refuses | pass: `A_read_only_target_refuses_the_copy` — `CanCopyBlock` and `CopyBlock` are both false while the target is read-only, an out-of-range index is refused whatever the flags say, and the target document does not move |
| Commands follow the current block and the flags | pass: `The_commands_follow_the_current_block_and_the_read_only_flags` — unavailable while there is no current block (`CurrentChangeIndex` is -1), available once navigation picks one and the target is editable, and executing copies the block navigation is sitting on. Alt+Left and Alt+Right are the gestures, so the composite now binds 9 keys rather than 7 |
| ~~Arrows in the connector gutter~~ — retired by plan 00004 | ~~pass: `The_gutter_draws_an_arrow_only_towards_an_editable_side` — no arrows while both sides are read-only, rightward arrows only once the right is editable, both once both are; `An_arrow_is_hit_before_the_polygon_it_sits_inside` pins the hit-test order~~. Both tests went with the arrows when they moved into the line-number margins; `CopyArrowMarginTests` is where the same questions are asked now. This row read as a live pass for two deleted tests until plan 00010 checked the prose against the test tree |
| `dotnet build DiffView.slnx -warnaserror` | clean |
| `dotnet test --solution DiffView.slnx` | 373 passed (363 before the phase) |
| New tests proven able to fail | five mutations killed: dropping the terminator that an append past an unterminated last line needs; ignoring the read-only flag in `CanCopyBlock`; reading the block's ranges from the wrong side; and drawing arrows towards read-only sides. **Two mutations survived, and both were the point**: they showed that a "copied run needs a terminator" branch was unreachable — a block is a maximal run of changed rows, so a run reaching one side's last line reaches the other's — and that a "trim the stranded terminator" branch was not merely untested but wrong, since keeping the source's terminator is exactly what makes the sides identical. Both were removed, and the second is now pinned by a test |
| Theme audit regenerated | `theme-audit compat` then `report` after the new `DiffView.GutterArrowBrush`: 0 low-contrast findings, drift test passes |

## Plan 00003, Phase 3 verification

| Done-when item | Result |
|---|---|
| `IsDirty(side)` tracks unsaved edits | pass: `SaveTests.An_edited_pane_is_dirty_saves_and_comes_back_clean` — clean on load, dirty after a keystroke, clean again after the save, with the header's `IsDirty` following it |
| `Save` writes with the file's encoding, mark and line endings | pass: same test — a UTF-8-with-mark, CRLF file is edited and written back with its mark intact as the first three bytes and its CRLF convention preserved, though the editor holds LF internally. The byte-level cases are `PaneWriterTests`: a marked UTF-8 file, an unmarked one, UTF-16 LE and BE, UTF-32, a Latin-1 fallback, and a source built from a string, each round-tripping byte for byte, plus `An_edit_to_one_line_changes_only_that_line_bytes` |
| A file changed on disk is reported and nothing is written | pass: `A_file_changed_on_disk_is_reported_and_nothing_is_written` — someone else's write lands while the pane holds edits; `Save` answers `ChangedOnDisk`, their bytes are untouched, and the pane keeps both its edits and its dirty flag |
| A side with no file reports rather than throwing | pass: `A_side_with_no_file_reports_rather_than_throwing` — `CanSave` is false and `Save` answers `NoPath` |
| A clean side writes nothing | pass: `Saving_a_clean_side_writes_nothing` — `NotDirty`, and the file's last-write time does not move |
| Our own write is not mistaken for someone else's | pass: `A_save_lets_the_next_one_through_rather_than_seeing_its_own_write_as_a_conflict` — the stamp follows each successful save, so a second save succeeds where a stale stamp would have reported a conflict |
| Reverting restores the source text | pass: `Revert_puts_the_source_text_back_and_rebuilds` — the document returns to the assigned text, the side is clean and unedited, the header's marker clears, and the rebuilt model's block count returns to what it was before the edit |
| The dirty state is visible | pass: `DiffPaneHeader.IsDirty` drives a `:dirty` pseudo-class and a `PART_Dirty` marker carrying its own tooltip and automation name; the strip reports the save outcome through the existing warning lane |
| `dotnet build DiffView.slnx -warnaserror` | clean |
| `dotnet test --solution DiffView.slnx` | 363 passed (343 before the phase) — 14 `PaneWriterTests` and 6 `SaveTests` |
| New tests proven able to fail | seven mutations, seven distinct failures: never writing the byte-order mark; counting a CRLF pair as two terminators; disabling the disk-change check; not re-stamping after our own write; clearing dirty but not edited on revert; saving a clean side anyway; and reporting the wrong outcome for a pathless side |
| Theme audit regenerated | `theme-audit compat` then `report` after the header's dirty marker: 0 low-contrast findings, drift test passes |

## Plan 00003, Phase 2 verification

| Done-when item | Result |
|---|---|
| A keystroke rebuilds the model once the debounce elapses | pass: `LiveReDiffTests.An_edit_rebuilds_the_model_from_the_live_text_after_the_debounce` — the model's `Version` and block count are unchanged while the edit rests, and both move once `ReDiffDelay` passes, with the metadata's line count catching up to the document's |
| The rebuild reads the live text, not the assigned source | pass: same test — the new block count can only come from text the `PaneSource` never carried. `EffectiveSource` builds a source from the document while keeping the original's encoding, path and title, which is what a save writes back with |
| The document, caret, selection, scroll and undo stack survive | pass: `The_rebuild_keeps_the_document_the_caret_the_selection_and_the_undo_stack` — `Assert.Same` on the `TextDocument` instance across the rebuild, with the caret offset, selected text and scroll offset unchanged, and the edit still undoable afterwards back to the original text |
| Keystrokes coalesce | pass: `Keystrokes_inside_the_window_coalesce_into_one_build` — four keystrokes, each advancing the clock to just short of the delay, leave the version untouched; one build lands after the last, moving it by exactly one |
| Find results are invalidated | pass: `The_matches_an_edit_invalidated_are_dropped_rather_than_left_pointing_at_moved_text` — matches found before an edit are dropped when it lands, because their offsets index text that has moved |
| Live re-diff can be turned off | pass: `With_live_re_diff_off_the_model_waits_for_an_explicit_rebuild` — with `LiveReDiff` false the debounce elapses four times over with no build, the side still reports as edited, and `ReDiffNow()` rebuilds on demand. This is the escape hatch Phase 6 will size against the 200k pair |
| A replaced document stops arming re-diffs | pass: `Assigning_a_source_again_clears_the_edited_flag_and_stops_the_old_document_talking` — after a new source lands, editing the abandoned document changes nothing and arms nothing. This test found two real defects while it was being written; see *Decisions* |
| `dotnet build DiffView.slnx -warnaserror` | clean |
| `dotnet test --solution DiffView.slnx` | 343 passed (337 before the phase) |
| New tests proven able to fail | six mutations, six distinct failures: returning the assigned source from `EffectiveSource` when the side is edited; dropping the `LiveReDiff` gate; never unsubscribing the replaced document; skipping the find invalidation; replacing the document on a re-diff the way a source assignment does; and collapsing the debounce to zero |

## Plan 00003, Phase 1 verification

| Done-when item | Result |
|---|---|
| Flipping `IsReadOnly` lets a pane accept typing | pass: `EditingTests.An_editable_pane_accepts_typing_while_its_neighbour_stays_read_only` — with `LeftReadOnly` cleared, a keystroke lands in the left document and its length grows by one, while the right pane, still read-only, does not move under the same keystroke |
| No layer relies on the document being immutable | pass: `Typing_past_the_model_leaves_every_visible_row_rendered_and_raises_no_fault` — three lines are appended past the end of what the model knows, with nothing rebuilding. `PaneMetadata.LineCount` stays behind `Document.LineCount`, `Knows` is false for the new lines, `KindOf` answers `Unchanged`, `BlockAt` and `RowOf` answer `null`, the frame still paints, no `RenderFault` is raised and the state does not fail. The bounds-check plan 00001 paid for is what absorbs the disagreement |
| Undo and redo are the editor's own | pass: `Undo_restores_the_document_and_the_editor_owns_the_stack` — `CanUndo`, undo restores the text exactly, `CanRedo`, redo reapplies it |
| Paste follows the property in both directions | pass: `Pasting_follows_the_property_in_both_directions` — `CanPaste` is false while read-only and true once cleared, the paste lands, and setting the property back on makes `CanPaste` false again and leaves the next keystroke and paste with no effect |
| Scroll coupling survives an edit | pass: `An_edit_does_not_disturb_the_other_pane_scroll_coupling` — the appended line is asserted to have landed first, then a 40px scroll on the edited pane still moves the other to the same offset |
| `dotnet build DiffView.slnx -warnaserror` | clean |
| `dotnet test --solution DiffView.slnx` | 337 passed (332 before the phase) |
| New tests proven able to fail | two mutations, five failures. Unbounding `PaneMetadata.Knows` (dropping the `<= LineCount` half) failed the stale-model test; pinning `_leftPane.IsReadOnly = true` in the composite's property handler failed the other four. The scroll-coupling test survived both on its first draft — it asserted the coupling without asserting the edit had landed — and was strengthened until the mutation killed it too |

## Phase 11 verification

| Done-when item | Result |
|---|---|
| The same fixtures render in unified form | pass: `InlineDiffViewTests.The_pane_holds_the_two_sides_unified_and_the_change_counts_match_the_side_by_side_view` composes the small pair's text independently, line by line from the side each unified line names, and finds it equal to `PaneDocument.Text`, with the editor's line count equal to the table's; `Every_visible_row_is_filled_by_its_own_kind_and_none_of_them_is_padded` walks the rendered frame and finds every row filled by its own kind, the markers agreeing glyph for glyph, and not one padded line — a unified document holds every line it shows, so nothing primes |
| Matching change counts | pass: the same test compares the two controls side by side — `ChangeCount`, `RowCount` and the strip's `+3 −5 ~2` and `5 changes` are the same numbers, one builder producing one model |
| The same find results | pass: `InlineFindTests.The_matches_are_the_side_by_side_view_own_minus_the_context_lines_it_shows_twice` — the side-by-side view's four matches for `Greeter` map to the unified view's three, the one dropped being the right line of a context row, whose text the left line already carries on screen; every surviving match is on the line it names with the query under it. `Over_changed_rows_only_the_two_views_find_exactly_the_same_matches` closes the gap the other way: with no context row searched, the counts and sides are identical. `F3_walks_the_matches_down_the_pane_selecting_each_in_turn` walks them in line order and wraps; `An_invalid_regular_expression_shows_the_error_inline_and_the_state_stays_ready` and `Escape_closes_the_bar_drops_the_highlights_and_returns_focus_to_the_pane` are the Phase 8 behaviours over one pane |
| The same failure behaviour | pass: `A_binary_side_fails_the_build_and_the_banner_offers_a_retry` — `Empty → Building → Failed`, the error banner with Retry, no model, no composed text, the strip failing; `A_throwing_decorator_degrades_the_control_and_the_text_still_renders` — one fault, `Degraded`, the generator disabled, every line still rendered, and the log line naming the pane `unified` rather than a side |
| The find scope control collapses to the single pane | pass: `The_scope_control_is_gone_and_the_scope_stays_both` — `DiffFindBar.ShowScope` is false, and a host that assigns `FindScope.Left` gets `Both` back while its other options are kept; the strip's find lane carries the count with no scope to name |
| Line numbers, navigation and the word diff over unified rows | pass: `The_gutter_numbers_each_line_on_its_own_side_and_leaves_the_other_column_empty` (a context line fills both columns, a removed or added line one, and the tooltip names the side); `A_modified_row_shows_both_of_its_lines_with_the_word_pieces_of_the_side_each_belongs_to` (each half highlighted over its own changed word); `F7_walks_the_blocks_and_the_border_covers_the_block_own_unified_lines` (the border spans the block's unified lines, which outnumber its rows); `The_caret_lane_names_the_line_on_its_own_side_not_the_unified_one` |
| Rendered under every theme target | pass: `Renders_under_every_theme_target_with_no_binding_or_resource_warnings` over all ten targets, with the header's own token sampled from the frame and the log sink asserting no warnings |
| Automation names | pass: `Every_decorator_the_unified_view_builds_carries_an_automation_name` sweeps the pane, both its margins, the strip and the find bar at runtime; the XAML guard counts `InlineDiffView` as interactive |
| Snapshot | `InlineSnapshotTests.The_unified_view_renders` Light and Dark — a block's removals above its additions, a number column per side, `+` / `−` / `~` markers, word-level pieces on the modified pair and the current block outlined; reviewed and approved |
| Core model | pass: `InlineDocumentTests`, 9 cases — one line per row when the sides are identical, every displayed line exactly once with the counts adding up, removals before additions inside a block with each side keeping its order, a modified row's kind on both halves, the right line of a context row mapping to nothing, out-of-range lines and blocks answering rather than throwing, every block a contiguous range holding exactly its own rows over a 600-line pair, and an unaligned pair printing every left line before every right |
| `dotnet build DiffView.slnx -warnaserror` | clean |
| `dotnet test --solution DiffView.slnx` | 332 passed (295 before the phase); with `-p:IncludePerfTests=true`, 339 |
| New tests proven able to fail | six mutations, each caught: emitting a block's additions before its removals; reading the word pieces from the pane's `Side` instead of the line's; drawing the current-block border over the model's rows instead of the block's unified lines; drawing the unified document's own numbers in the gutter; logging the unified pane as the left side; and keeping the find matches the unified view cannot show |
| Trimmed publish (`linux-x64`, self-contained) | succeeds, 0 IL warnings, 57 MB |
| Demo | pass: the published binary run as `--unified --left src/DiffView.Core/PaneSource.cs --right src/DiffView.Core/TextProbe.cs` logs `Syntax highlighting on the unified pane: csharp`, then `Build 1 completed in 6.4 ms: 151 rows, 25 blocks (+23 -31 ~61)` and `State "Building" → "Ready"` with no fault — the same model the side-by-side run reports for the same pair. The window itself could not be screenshotted from this session (XWayland refuses the grab — **this was wrong**, and `AGENTS.md` §9 has the working recipe); the headless snapshots are the pixels' evidence |
| Theme audit regenerated | `theme-audit compat` then `report` after `Themes/InlineDiffView.axaml`: the DiffView consumer moves from 5 files to 6 and 56 references to 65, with no new key and 0 low-contrast findings; the drift test passes |

## Phase 10 verification

| Done-when item | Result |
|---|---|
| 200k-line fixture opens and scrolls without visible stalls; the 1 MB line renders; numbers in `PROGRESS.md` | pass: `ScalePerfTests` (`Perf` trait) — the 200,000-line pair (204,001 rows, 4,000 blocks) builds in 297 ms, is loaded, primed and laid out 1,243 ms after the sources are assigned, paints its first frame in 14 ms, and scrolls to the middle, to the end and back in 20 / 17 / 17 ms; the one-megabyte single line builds in 16 ms, is up in 612 ms, paints in 55 ms and scrolls sideways to the middle of the line in 54 ms. Both are in *Measurements*. The build is well inside the budget, so DiffPlex is **not** vendored (`DECISIONS.md`) |
| Headless test: a `FontSize` change leaves both extents equal | pass: `ViewOptionsTests.A_font_size_change_re_primes_both_panes_and_leaves_their_extents_equal` — `PaneFontSize = 22` gives taller rows and a taller document with the two extents still equal, and clearing it back to `NaN` returns the panes to the size their own theme sets, extents equal again; `A_pane_font_family_change_reaches_both_panes_and_clears_back_to_the_theme` is the family's half |
| Demo runs cleanly on this Linux box; Windows and macOS runs are recorded when available | pass: the demo boots on this Wayland session against two `.cs` files, colourises both panes, builds and reaches `Ready` with no fault; its View menu now carries whitespace, line endings, tab width and pane font size. Windows and macOS runs are still owed; the window itself cannot be screenshotted from this session (XWayland refuses the grab — **this was wrong**, see `AGENTS.md` §9) |
| `ShowWhitespace`, `ShowLineEndings`, `TabWidth` | pass: `Whitespace_and_line_ending_glyphs_reach_both_panes_and_put_more_ink_on_the_page` (the options reach both panes' `TextEditorOptions` and the frame gains ink, which returns exactly to its old count when they go off) and `A_tab_width_change_moves_text_sideways_and_leaves_the_rows_and_the_extents_alone` (the first text column moves right, the line height does not move, the extents stay equal, and a width of 0 is floored to 1) |
| Mixed-line-ending notice | pass: `Mixed_line_endings_are_noticed_in_the_state_and_the_strip` — a CRLF/LF pair leaves the control `Degraded` with the warning in `StateMessage`, the strip's transient lane carrying it as a warning, and the header naming the convention per side |
| Font-family fallback list for Linux/macOS/Windows | already in `Themes/DiffView.axaml`: `Cascadia Mono, Consolas, Menlo, DejaVu Sans Mono, monospace` — the first installed family of the stack, with the tests overriding the key with the bundled font so frames stay machine-independent |
| Copy selection works per pane; read-only is enforced against paste and typing | pass: `InteractionTests.Each_pane_copies_its_own_selection_and_read_only_holds_against_typing_and_pasting` — each pane copies its own selection to the clipboard (read back through `TryGetTextAsync`), and with `INJECTED` on the clipboard neither `Paste()` nor a keystroke changes a character in either document; `CanPaste` is false while read-only. `DiffPanePresenterTests.IsReadOnly_is_honoured_and_defaults_to_true` covers typing at the presenter |
| Focus visuals; automation names on every decorator | pass: `The_focused_pane_is_accented_in_its_header_and_F6_moves_the_accent` — no accent pixels while nothing has focus, the accent under the focused pane's header only, and F6 moves it to the other side; `Every_decorator_the_composite_builds_carries_an_automation_name` sweeps the panes, both margins of each, the connector gutter, the minimap, the status strip and the find bar at runtime, where the XAML guard cannot see the margins because they are built in code |
| Snapshot | `ViewOptionsSnapshotTests.The_view_options_and_the_focus_accent_render` Light and Dark — tab arrows, space dots and `\n` glyphs at a tab width of 8 and a pane font of 16, with the focus accent under the right header; reviewed and approved |
| `dotnet build DiffView.slnx -warnaserror` | clean |
| `dotnet test --solution DiffView.slnx` | 295 passed (285 before the phase); with `-p:IncludePerfTests=true`, 302 — the 7 `Perf` tests, 2 of them new |
| New headless tests proven able to fail | six mutations: dropping `ShowSpaces`/`ShowTabs`/`IndentationSize` failed the whitespace and tab-width tests; clearing the pane font instead of setting it failed the font test; not pushing focus into the headers failed the focus test; dropping the margins' automation name failed the decorator sweep; and reporting the mixed-line-ending warning as a success failed the notice test |
| Theme audit regenerated | `theme-audit compat` then `report` after the new `DiffView.FocusAccentBrush`: `docs/theme-audit.md` scores it 5.22:1 (light) and 6.45:1 (dark) against the header background, 0 low-contrast findings, and the drift test passes |

## Phase 9 verification

| Done-when item | Result |
|---|---|
| The trimmed publish of the demo succeeds and colourizes a C# fixture at runtime | pass: `dotnet publish -c Release -r linux-x64 --self-contained true` with `TrimMode=link` gives **0 IL warnings** and needs no `TRIMMING.md` wiring — TextMateSharp keeps its grammars as embedded resources and parses them with its own parser, so nothing is reflected over; `TextMateSharp.dll`, `TextMateSharp.Grammars.dll`, `Onigwrap.dll` and `libonigwrap.so` are in the output, which grew from 51 MB to 57 MB. The published binary run against `src/DiffView.Core/PaneSource.cs` and `TextProbe.cs` logs `Syntax highlighting on the "Left" pane: csharp` and the same for the right, then `State "Building" → "Ready"` with no fault. The window itself could not be screenshotted from this session (XWayland refuses the grab — **this was wrong**, see `AGENTS.md` §9), so the pixels are the headless snapshots' evidence; a look at the running window is left for the user |
| Headless: unknown extension does not throw and falls back to plain text with state `Ready` | pass: `SyntaxTests.An_extension_no_grammar_claims_leaves_plain_text_and_the_state_stays_ready` — the small pair under `left.txt` / `right.txt` leaves `SyntaxLanguageId` null on both panes, one foreground on the first line, `Ready`, no fault, and the row kinds still drawn; `A_source_with_no_name_at_all_stays_plain_text` covers the source with neither path nor title, and `The_grammar_follows_the_path_when_the_source_has_one` the path route (the title route is what every other test uses) |
| Headless: a grammar install that throws puts the control in `Degraded`, names the grammar, and diff highlighting is unaffected | pass: `A_grammar_that_will_not_install_degrades_the_control_names_it_and_leaves_the_diff_highlighting` — the left pane's install throws through the `SyntaxInstallerForTesting` seam: exactly one fault, `Source` `SyntaxHighlighting` and `Subject` `csharp`, the message and `StateMessage` both naming it, that pane back to one foreground, `Degraded`, and the modified and inserted row fills still drawn on both sides while the right pane stays colourised |
| Snapshot: C# and JSON fixtures colorized under the diff backgrounds in both theme variants | pass: `SyntaxSnapshotTests.A_colourised_pair_renders_under_the_diff_backgrounds` × {Csharp, Json} × {Light, Dark} — keywords, types, strings and numbers coloured by Dark+ / Light+ over the inserted, deleted and modified fills, with the word-level pieces and the change border still on top; reviewed and approved. `fixtures/json` is the new pair |
| Pixel assertion: syntax colour is present under an inserted row's background — the two layers compose | pass: `Syntax_colour_and_the_inserted_fill_compose_on_the_same_row` — on the first inserted row of the right pane, the inserted fill composited over the pane background is on screen *and* more than one of the token colours that row's runs carry is painted inside the same band |
| Headless: the toggle | pass: `Turning_the_toggle_off_returns_the_panes_to_plain_text_and_turning_it_on_colours_them_again` — `UseSyntaxHighlighting = false` removes both installations and returns the first line to one foreground; back on, the grammar returns and the state stays `Ready` |
| Headless: the theme follows the variant | pass: `The_syntax_theme_follows_the_variant` — the first token's own colour on `using System;` changes when the application variant goes Light → Dark, and the pane is still colourised by the same grammar |
| `dotnet build DiffView.slnx -warnaserror` | clean |
| `dotnet test --solution DiffView.slnx` | 285 passed (273 before the phase) |
| New headless tests proven able to fail | six mutations: dropping `SetGrammar` failed seven of the twelve (both colourisation tests, the theme and toggle tests, all four snapshots and the pixel assertion); pinning the syntax theme to Light+ failed the variant test and the two Dark snapshots — and, before the test was sharpened to read the first token's colour rather than the line's whole colour set, it passed under that mutation because the pane's own foreground moves with the palette; dropping the fault report failed the degraded test; making `Remove` a no-op failed the toggle test; resolving every file to the C# grammar failed the unclaimed-extension test and the JSON snapshots; and treating a nameless source as `x.cs` failed the no-name test |

## Phase 8 verification

| Done-when item | Result |
|---|---|
| Headless: Ctrl+F opens the bar with focus in the query box; Esc closes it and focus returns to the pane that had it | pass: `FindTests.Ctrl_F_opens_the_bar_with_focus_in_the_query_box_and_Escape_closes_it_and_returns_focus` — with the right pane focused and one line of it selected, Ctrl+F opens the bar, pre-fills "Greeter" from the selection and puts the caret in the query box; the strip reads "find · both" before the search and "find 4 matches · both" after it, the bar "4 matches (L 2 · R 2)", both panes carry their two matches and the minimap its rows; Escape closes the bar, drops every highlight and the result, and the right pane has focus again |
| Headless: in `Both` scope with hits on both sides, F3 walks the matches in row-then-side order, and the presenter holding the current match has focus and the match selected | pass: `In_both_scope_F3_walks_the_matches_in_row_then_side_order_and_the_pane_holding_one_has_it_selected` — the four matches equal an independent walk of the alignment table (left line before right line within a row), a fresh result has no current match, and each F3 lands on the next one with its pane focused, the match selected, `CurrentSearchMatch` set on that pane and null on the other, the bar reading "match i of 4 (L 2 · R 2)", the strip "find i of 4 · both", and both panes at the same offset; the walk wraps at either end |
| Headless: switching scope L → R → Both re-runs the search and the counts and highlights change accordingly | pass: `Switching_the_scope_re_runs_the_search_and_the_counts_and_highlights_follow` — through the bar's own properties, as a click drives them: Left leaves `RightCount` 0, the right pane with no matches and no drawn rectangles, and the strip "find 2 matches · left"; Right mirrors it; Both restores four; Match case with a lower-case query gives "no matches" |
| Headless: an invalid regex shows the inline error, leaves no highlights, and the control state stays `Ready` | pass: `An_invalid_regular_expression_shows_the_error_inline_leaves_no_highlights_and_the_state_stays_ready` — `Greet(er` under Regex puts "Invalid regular expression…" in the bar, clears both panes' matches and the count, leaves `State` at `Ready` and the current match at -1, logs one `Warning` under `DiffView.Find`, and the pattern itself never reaches the log; `Greet(er)?` clears the error and the matches come back |
| Headless: a query with more than `MaxMatches` hits on the 10k-line fixture shows the truncation notice and the UI stays responsive | pass: `More_hits_than_the_cap_truncate_with_a_notice_and_the_search_runs_off_the_UI_thread` — a generated 10,000-line pair (every line holding the needle, so 20,000 hits against the default 10,000 cap): the search runs off the UI thread, `Truncated` is set, exactly `MaxMatches` matches are kept, the bar and the transient lane both read "Showing the first 10,000 matches", the frame still renders with match rectangles, and the walk works over the capped matches |
| Headless: the search worker never touches the live document — a search runs to completion while the UI thread holds the document in an update | pass: `The_search_completes_while_the_UI_thread_holds_a_document_in_an_update` — the searcher is held on a gate until the UI thread has opened `TextDocument.RunUpdate()`, and the worker then reads its `DocumentPaneText` snapshot and completes with the four matches; capturing on the worker instead makes it throw from `TextDocument.VerifyAccess` |
| Snapshot: match highlights sit above the diff backgrounds and below the selection, current match distinct, in both theme variants | pass: `FindSnapshotTests.The_find_bar_and_the_match_highlights_render` Light and Dark — the bar over the panes with the scope segmented control and "match 2 of 7 (L 4 · R 3)", every `_name` highlighted (including over word-diff pieces on modified rows), the current match on the right pane in the current-match brush under its selection, ticks in the minimap, "find 2 of 7 · both" in the strip; reviewed and approved. `FindTests.Match_highlights_sit_above_the_row_fill_and_below_the_selection` asserts the composited pixels: the match brush over the pane background on an unchanged row, and the selection over the current-match brush once it is current |
| The sentinel log test's find leg, owed since Phase 5 | pass: `SideBySideDiffViewTests.The_log_never_carries_document_text_and_each_state_transition_appears_exactly_once` now searches for the sentinel — which is document text, because Ctrl+F pre-fills the query from the selection — walks to a match, and asserts the `DiffView.Find` line reports "4 match(es)" while no record anywhere holds the sentinel |
| `dotnet build DiffView.slnx -warnaserror` | clean |
| `dotnet test --solution DiffView.slnx` | 273 passed |
| New headless tests proven able to fail | the Ctrl+F and F3 tests failed for real before the focus fix — a query box that has only just become visible has not been measured and cannot take focus, and with nothing inside the composite focused the ancestor key bindings never ran; disabling `SearchMatchRenderer.DrawCore` then failed the pixel, scope, truncation and both snapshot tests; dropping the truncation notice, the bar's error text, and the UI-thread capture failed the cap, regex and update tests respectively |
| Trimmed publish (`linux-x64`, self-contained) | succeeds, 0 IL warnings, 51 MB |
| Demo launched on this machine | boots, builds the bundled pair and logs its transitions with no fault or error; the find bar itself is exercised headlessly, and the demo's View menu now carries Find (Ctrl+F) |

## Phase 7 verification

| Done-when item | Result |
|---|---|
| Headless: `NextChange` from the top lands on `Blocks[0].FirstRow`; at the last block it stops and the status strip says so | pass: `NavigationTests.NextChange_from_the_top_lands_on_the_first_block_and_stops_at_the_last_with_the_strip_saying_so` — the first block is the current one in both panes with its border drawn in the token colour over its rows, the strip reads "change 1 of 5", every subsequent block lands in view with both panes at the same offset, the sixth press stays at 5 with "No next change" in the lane; first, previous at the first, last, and the clamping setter are covered too |
| Headless: minimap pixel → bucket → row mapping is correct at top, middle, bottom on the 200k-line fixture | pass: `OverviewTests.The_minimap_maps_pixels_to_buckets_to_rows_at_top_middle_and_bottom_on_the_200k_line_fixture` — a generated 200,000-line pair (204,001 rows) in a 400 px minimap; at buckets 0, 200 and 399 the first row, the end row, the round trip through `BucketOfRow`, the strongest kind and the click's first changed row all match an independent computation; `A_minimap_click_in_the_composite_jumps_both_panes_and_the_viewport_tracks_the_scroll` covers the click and the viewport rectangle in the composite |
| Headless: connector polygons for visible blocks have the expected left and right extents; a headless click on a polygon makes that block the current change; a headless drag on empty gutter space resizes the panes | pass: `Connector_polygons_have_the_expected_extents_a_click_selects_the_block_and_a_drag_resizes_the_panes` — one polygon per block, tops at the block's first row, the left bottom after its modified plus deleted rows and the right bottom after its modified plus inserted rows, the gutter's row tops agreeing with the pane's; a click inside the tallest polygon selects its block; a drag on the unchanged first row widens the left pane and raises `SplitRatio` |
| Headless: F6 moves focus between the panes | pass: `F7_and_Shift_F7_navigate_and_F6_switches_panes` — F7 and Shift+F7 walk the changes from a focused pane, F6 alternates the focused pane both ways, and clearing `KeyBindings` disarms them |
| Tooltips on line numbers and markers | pass: `TooltipTests.Line_numbers_name_the_counterpart_and_markers_name_the_block` — "Line 2 · right line 3", "Line 17 · no right line", "Line 2 · no left line" for the inserted using, "Change 5 of 5 · +0 −5 ~1" for the block holding the deleted and modified lines, the pointer over the line-number margin bringing the text up and leaving clearing it; the connector and minimap tooltips are asserted in `OverviewTests` |
| Snapshot: minimap, connectors and the current-block border on the small fixture in both theme variants | pass: `NavigationSnapshotTests.Minimap_connectors_and_the_current_block_render` Light and Dark with change 3 current, reviewed and approved; the ten composite and demo baselines re-approved with the gutter and minimap columns |
| Navigation with no changes, and a new model clearing the current change | pass: `Navigation_without_changes_says_so_and_a_new_model_clears_the_current_change` |
| `dotnet build DiffView.slnx -warnaserror` | clean |
| `dotnet test --solution DiffView.slnx` | 264 passed |
| New headless tests proven able to fail | the minimap's round trip failed at the middle bucket until `BucketOfRow` became the exact inverse of `FirstRowOfBucket`; the marker tooltip test expected `~0` for a block that holds a modified line above its deletions and the line-number test expected a counterpart for the inserted using — both expectations were wrong and were corrected against the model |
| Trimmed publish (`linux-x64`, self-contained) | succeeds, 0 IL warnings, 51 MB |
| Demo launched on this machine | boots with the connector gutter and the minimap beside the panes, builds the bundled pair, logs its transitions; no fault or error in the log |

## Phase 6 verification

| Done-when item | Result |
|---|---|
| Headless: the renderer's word rectangles for a `Modified` row cover exactly the `PieceRange` columns, in both panes, and the cache is populated only for rows that were rendered | pass: `WordDiffTests.Word_rectangles_cover_exactly_the_piece_columns_in_both_panes_and_the_cache_holds_only_rendered_rows` — with the modified row below a 180 px viewport the cache is empty and nothing is drawn; scrolled into view, the cache holds that one row, each drawn rectangle's left and right equal the visual line's x at the piece's start and end columns over the full row height, the pieces name exactly the changed words on each side and concatenate to the line, and the pixels inside the first rectangle carry the composited word brush while those just outside carry the plain row tint |
| Headless: the 1 MB single-line fixture renders without word-level pieces and the tooltip says so | pass: `The_one_megabyte_single_line_renders_without_pieces_and_the_marker_tooltip_says_so` — a generated 1,000,000-character line against a copy with its tail changed: `Degraded` with `LongLinesSkipped`, no pieces and no rectangles on either side, the row still drawn as modified; the marker margin's tooltip under the pointer names the limit and clears when the pointer leaves. Timing in *Measurements* |
| Snapshot: a `Modified` row shows only the changed words highlighted, in both theme variants | pass: `WordDiffSnapshotTests.A_modified_row_shows_only_the_changed_words` Light and Dark — two modified rows with two and three highlighted pieces each, reviewed and approved; the composite and demo snapshots re-approved with the highlights on their modified rows |
| Headless: toggling `IgnoreWhitespace` removes whitespace-only diffs and the status strip reflects the option | pass: `Toggling_IgnoreWhitespace_removes_whitespace_only_diffs_and_the_strip_reflects_the_option` — one modified row becomes none, the identical banner appears, the strip reads "ignore whitespace", and the option reaches the cache; off again restores the change |
| `WordDiff` and `MaxWordDiffLineLength` wired through with a rebuild on change | pass: `Word_diff_off_draws_nothing_and_character_mode_reaches_the_cache`; the limit rides in the same options record and the long-line test exercises it |
| `dotnet build DiffView.slnx -warnaserror` | clean |
| `dotnet test --solution DiffView.slnx` | 255 passed |
| New headless tests proven able to fail | the geometry test's first run under the earlier nullable-flow draft did not compile until the lookup carried `NotNullWhen`; the snapshot and composite baselines mismatched on the highlights before review, as they must |
| Trimmed publish (`linux-x64`, self-contained) | succeeds, 0 IL warnings, 51 MB |
| Demo launched on this machine | boots, builds the bundled pair, shows the highlights on its modified rows; no fault or error in the log |

## Phase 5 verification

| Done-when item | Result |
|---|---|
| Headless: with a sentinel string in both sources, the captured log after build, render and a forced `RenderFault` never contains it, and each state transition appears exactly once at `Information` | pass: `SideBySideDiffViewTests.The_log_never_carries_document_text_and_each_state_transition_appears_exactly_once` — no record's message or exception text carries the sentinel or a fixture word; `Empty → Building`, `Building → Ready`, `Ready → Degraded` each once under `DiffView.Build`; the fault once at `Error` under `DiffView.Render` with its exception. The find leg waits for Phase 8 |
| Headless: the composite renders under all ten theme targets with zero binding and resource warnings | pass: `Renders_under_every_theme_target_with_no_binding_or_resource_warnings` — Ready under each target, the header painting its own token, the bridge silent in every area |
| Unit: every `DiffView.*` pair meets its floor under all ten targets — status pills ≥ 4.5:1 on their fill and ≥ 7:1 on the page | pass: four page pairs added at floor 7.0 to `contrast-pairs.json`; the Dark status foregrounds brightened one step to clear Semi Dusk and Simple Dark (see `DECISIONS.md`); `theme-audit report` shows 0 low-contrast findings over 34 pairs × 10 targets × 2 palettes, held by `ReferenceAuditTests` |
| Headless: a `Success` message clears after its delay under a test `TimeProvider`; a `Failure` sticks until dismissed; a new message cancels the pending clear | pass: `StatusControllerTests` on `FakeTimeProvider` (five cases, including active and state sticking and disposal), and `The_status_strip_shows_the_focused_panes_caret_and_a_failure_can_be_dismissed` through the strip |
| Headless: swapping `DiffViewStrings.Resolver` before load changes the rendered strings | pass: `Swapping_the_string_resolver_before_load_changes_the_rendered_strings` — state pill, header title and change count in German |
| Headless: setting the left offset moves the right offset to the same value and back, with no feedback loop, at top, middle and bottom | pass: `Scroll_sync_is_one_to_one_at_top_middle_and_bottom_with_no_feedback_loop` — both directions at three offsets, at most four scroll events per change and none once settled, every shared row at the same top |
| Headless: `LeftSource` / `RightSource` changes rebuild the document; a change during a build supersedes it and the final state reflects the last input | pass: `Sources_build_the_document_and_the_state_moves_from_Empty_through_Building_to_Ready`, `A_change_during_a_build_supersedes_it_and_the_final_state_reflects_the_last_input` — the gated first build lands late and is discarded with a `Debug` line, one `BuildCompleted` |
| Headless: changing an option property re-runs the diff and preserves caret, selection, scroll offset and the undo stack in both panes; the `TextDocument` instances are the same objects | pass: `Changing_an_option_rebuilds_and_preserves_caret_selection_scroll_and_undo_in_both_panes` |
| Headless: a throwing builder puts the control in `Failed` with the message shown and `Retry` rebuilds | pass: `A_throwing_builder_puts_the_control_in_Failed_with_the_message_and_Retry_rebuilds` — banner, strip, status lane and `BuildFailed` all carry the message; the command re-enables and disables with the state |
| Headless: binary input → `Failed` with `BinaryInput`; the unrelated pair → `Degraded` with the banner, and Force aligns it; identical input → `Ready` with the identical banner | pass: `Binary_input_fails_the_unrelated_pair_degrades_until_forced_and_identical_input_is_Ready_with_the_banner` — the binary header badge, the too-different banner over a 12,000-line unrelated pair, Force aligning it, the identical banner and badges |
| Headless: state transitions are logged when a logger is set | pass: the sentinel test above; `LoggerFactory` replaces the plan's `Logger` (see `DECISIONS.md`) |
| Headless: dragging the gutter with headless pointer input leaves both vertical offsets equal and both panes at the same first visible row | pass: `Scrolling_the_right_pane_with_headless_pointer_input_keeps_both_panes_on_the_same_first_row` — the right scrollbar's thumb when the theme exposes one, else the wheel; offsets equal and every shared row aligned |
| Snapshot: headers, status strip, error banner and identical banner in both theme variants and both palettes | pass: `CompositeSnapshotTests` — headers and strip in Light and Dark under both palettes (four frames), the identical banner and the error banner (two), all reviewed and approved; the demo smoke snapshot re-approved with the composite in it |
| Latest-wins worker, progress after 100 ms, stale marking | pass: `A_slow_build_shows_progress_after_the_threshold_and_the_previous_result_is_marked_stale` — the previous model stays on both panes marked stale; progress appears once the fake clock passes the threshold |
| Equal horizontal scrollbar visibility; `SyncHorizontalScroll`; `LeftReadOnly` / `RightReadOnly` | pass: `Horizontal_sync_is_optional_and_both_panes_show_the_same_horizontal_bar`; the read-only flags reach the panes in the options test |
| Demo on the composite with file-open and error reporting | done: `MainWindow` hosts `SideBySideDiffView`, opens files into either side through the picker with failures in the status lane and the log, toggles the options and the palette |
| `dotnet build DiffView.slnx -warnaserror` | clean |
| `dotnet test --solution DiffView.slnx` | 249 passed |
| New headless tests proven able to fail | the caret test pointed at a blank line and reported line 4 for 3; the scroll test counted three events where it expected two and now asserts quiescence; the sentinel test tripped "visual invalidated during the render pass" before the fault event was deferred; each failed for a real reason before its fixture or the code was corrected |
| Trimmed publish (`linux-x64`, self-contained) | succeeds, 0 IL warnings, 51 MB; the palette toggle and the three control themes are compiled dictionary classes |
| Demo launched on this machine | boots, logs `Empty → Building`, the build's counts and `Building → Ready`; no fault or error in the log |

## Phase 4 verification

| Done-when item | Result |
|---|---|
| Headless: the presenter renders under all ten theme targets with zero binding and resource warnings from the logger bridge | pass: `DiffPanePresenterTests.Renders_under_every_theme_target_with_no_binding_or_resource_warnings` over the ten targets; each pane paints its own `DiffView.PaneBackgroundBrush` under every one, and the bridge saw no warning in any area |
| Headless: each presenter's document text equals its source text exactly — no padding in the document | pass: `The_document_text_equals_the_source_text_and_the_line_counts_agree`, both sides, character for character |
| Headless: on the mixed-line-ending fixture, `DiffPane.Lines` count equals the presenter's `TextDocument.LineCount` | pass: `The_model_and_the_editor_count_the_same_lines_on_mixed_line_endings` — CRLF, CR and LF in one text, five lines both ways, the `MixedLineEndings` warning raised |
| Headless: after a load both presenters report equal scroll extents before any scrolling, and the renderer receives the expected `Kind` per visual line | pass: `After_a_load_both_extents_are_equal_before_scrolling_and_the_renderer_sees_each_lines_kind` — a 200 px viewport keeps one padded line below it; both extents equal the row count times the line height, every shared row sits at the same top, the background renderer and the marker margin report each visible line's own kind and padding, and a font change to 18 px re-primes with the extents still equal. `Assigning_a_new_model_reprimes_so_lines_that_lost_their_padding_return_to_one_row` covers the union rule on a model swap |
| Headless: the line-number margin shows the document's own numbers and nothing over padding space | pass: `The_line_number_margin_shows_the_documents_own_numbers_and_nothing_over_padding_space` — numbers in visual-line order at each line's text top; the margin's pixels over the padding rows are the gutter background only, the text row carries the number |
| Headless: no `SearchPanel` is installed on the presenter | pass: `No_search_panel_is_installed` — no search input handler nested in the text area, and Ctrl+F leaves the panel closed |
| Headless: the presenter honours `IsReadOnly` — typing is rejected when true and accepted when false | pass: `IsReadOnly_is_honoured_and_defaults_to_true` — the default is true; headless text input changes nothing until the flag is cleared |
| Headless: with metadata for a shorter document, every line renders as `Unchanged` and nothing throws | pass: `Metadata_for_a_different_document_renders_unknown_lines_as_unchanged_and_never_throws` — a longer document keeps the known lines' kinds and renders the rest unchanged and unpadded; a shorter one renders every line it has; no fault either way (the reading is in `DECISIONS.md`) |
| Headless: a renderer that throws raises `RenderFault` once, disables itself, and the text is still rendered; a generator that throws does the same and the line renders without padding | pass: `A_throwing_renderer_raises_RenderFault_once_disables_itself_and_the_text_still_renders`, `A_throwing_generator_raises_RenderFault_once_and_the_lines_render_without_padding` — one fault each over two frames, glyphs still drawn, every line one row tall after the generator fault; a new model re-enables the generator |
| Pixel assertions: inserted and deleted text bands and padding space carry their theme brushes; a selection across a padded line paints only text bands; the caret on a padded line is one text line tall | pass: `PresenterPixelTests.Inserted_and_deleted_rows_and_padding_space_carry_their_theme_brushes` (row bands match the composited token colours; the padding rows carry the fill and the hatch), `A_selection_across_a_padded_line_paints_only_text_bands_and_the_caret_is_one_text_line_tall` (selection in both text bands, none in the padding rows; caret in the padded line's text band only, gone when the other pane takes focus) |
| Snapshot: the small fixture in light and dark | pass: `PresenterSnapshotTests.Small_fixture_renders` under Semi Light and Dark at 900×600, reviewed and approved; the demo smoke snapshot re-approved with the two panes in it |
| The caret column after `Home` twice, owed by Phase 1 | pass: `Home_pressed_twice_on_a_padded_line_keeps_the_caret_on_the_first_text_column` — the second `Home` lands on column 1 at visual column 1, and `Right` moves to column 2 |
| Automation names on the new surface | the two margins carry names through `DiffViewStrings` (`Margins_carry_automation_names_through_the_string_resolver`); the demo's panes are named; the accessibility guard counts `DiffPanePresenter` and `TextEditor` |
| `AGENTS.md` started with the first cross-file contracts | done: the metadata stamp, the no-worker rule, `SearchPanel.Uninstall()`, the priming triggers, the fault boundary, the padding geometry, the theming rules and the test seams |
| `dotnet build DiffView.slnx -warnaserror` | clean |
| `dotnet test --solution DiffView.slnx` | 215 passed |
| New headless tests proven able to fail | the extents test failed one padding row short before `OnLoaded` became a priming trigger — the defect it was written to catch, and one the 320 px hosts of the other tests hid; the selection test failed while it sampled an empty padded line and the bands test while its sample band still held glyphs, before their fixtures were corrected |
| Trimmed publish (`linux-x64`, self-contained) | succeeds, 0 IL warnings, 50 MB; the first draft's runtime `ResourceInclude` failed it with IL2026 and became the compiled `DiffPanePresenterTheme` (see `DECISIONS.md`) |
| Demo launched on this machine | boots, builds the bundled pair (33 rows, 5 blocks, no warnings) and shows both panes; no fault or error in the log |

## Phase 3 verification

| Done-when item | Result |
|---|---|
| Invariant 1 — every line of each side appears exactly once in `Rows`, in order | pass on seven pairs (small, generated 1000-line, identical, empty left, empty right, both empty, no trailing terminator); the pane's row pointers agree with the table and the line count is the editor's |
| Invariant 2 — no row has both sides `null` | pass, and each kind has exactly the sides it implies |
| Invariant 3 — a modified row's pieces concatenate to its lines | pass on every modified row of every pair, in word and character mode, and on edge lines (empty sides, separators only, trailing space) |
| Invariant 4 — blocks disjoint, ordered, covering every changed row, with exact per-side ranges | pass; an empty range sits where the side's next line would go |
| Invariant 5 — `Cr`, `CrLf`, `Lf` variants produce identical rows | pass on a 300-line pair with changes |
| Invariant 6 — `Padding.Before` summed plus `Padding.Trailing` equals the side's `null` rows | pass on every pair; padding before a line equals the run of `null` rows above it |
| Invariant 7 — below the floor on large inputs: unaligned concatenation, `Aligned` false, `TooDifferentToAlign` | pass on a 6,000-line unrelated pair over a 10,000-line threshold; `ForceAlignment` and a higher threshold align it; a small unrelated pair aligns regardless |
| Identical inputs → no blocks; empty left / empty right; binary → `BinaryInput`; mixed and CR-only endings → `MixedLineEndings`; Latin-1 fallback warned; long line → `LongLinesSkipped` and no pieces; cancellation between stages | pass — an empty side is one empty line that pairs with the other side's first line as modified (see `DECISIONS.md`) |
| `WordDiffCache` returns the same instance twice, evicts by LRU, keys by document version | pass |
| Search: scope filtering; `Both` ordering (row, left, column); whole word at line boundaries; `ChangedRowsOnly`; invalid regex → `Error`; catastrophic backtracking → timeout `Error`; `Truncated` above `MaxMatches`; cancellation | pass; a backreference pattern runs on the fallback engine; a pane text shorter than the document is tolerated |
| `Perf`: `Build` on the 10k and 200k pairs; the gate on the unrelated pair | measured, see *Measurements* |
| `dotnet build DiffView.slnx -warnaserror` | clean |
| `dotnet test --solution DiffView.slnx` | 189 passed |
| Tests proven able to fail | the search fixture, the small-pair block expectation and the default-options static initialiser each failed a run during the phase before the code or the expectation was corrected |
| Trimmed publish (`linux-x64`, self-contained) | succeeds, 0 IL warnings, 48 MB |

## Phase 2 verification

| Done-when item | Result |
|---|---|
| `theme-audit report` from a clean directory produces the report against the reference checkouts | pass: `dotnet run --project src/ThemeAudit -- report` writes `docs/theme-audit.md` (4 themes, 6 consumers); `--check` confirms it |
| `dotnet pack` puts `Bennewitz.Ninja.ThemeAudit` in the local feed | pass: `scripts/pack-theme-audit.*` packs 1.1.0 into `../nuget-local` |
| `tests/ThemeAudit.Tests` passes on fixture themes, including one that omits a key and one whose token fails its floor | pass: `Fixtures/audit` (Host omits `HostAccent` under Dark; `AppOwn` fails 4.5 in Dark) and `Fixtures/compat` |
| `docs/theme-audit.md` committed; regenerating changes nothing | pass: `ReferenceAuditTests.The_committed_report_equals_a_fresh_run` and the compat sibling; a mismatch writes a `.received` file beside the committed one |
| Resolution: with the compat dictionary every AvaloniaEdit theme key resolves under all six Semi variants; without it the test names the missing keys | pass, static and at runtime: with compat 0 missing; without, the Fluent theme file lacks 6 keys (`ContentControlThemeFontFamily`, `ControlContentThemeFontSize`, `SystemAccentColor`, `SystemBaseLowColor`, `SystemChromeMediumColor`, `ToolTipBorderThemeThickness`) and the Simple theme file 9 — the plan's "six" counted only the Fluent file |
| Contrast: every `DiffView.*` pair meets its floor under all ten targets | pass: 30 pairs × 10 targets, both palettes, 0 below the floor, 0 unmeasurable |
| Headless: a plain `TextEditor` renders under each Semi variant with the compat dictionary and no resource warning | pass: `ThemeResolutionTests.A_TextEditor_with_its_Fluent_search_panel_renders_under_Semi_with_the_compat_dictionary`, six variants, search panel open and painted, no binding warning |
| ClaudeForge pull request open and referenced here | pass: [JanusMael/ClaudeForge#38](https://github.com/JanusMael/ClaudeForge/pull/38), draft — see *Upstreamed to ClaudeForge* |
| `dotnet build DiffView.slnx -warnaserror` | clean |
| `dotnet test --solution DiffView.slnx` | 102 passed |
| New headless tests proven able to fail | removing the compat include from the `TextEditor` test failed it (the Fluent theme's static references throw at load) before the include was restored |
| Trimmed publish (`linux-x64`, self-contained) | succeeds, 0 IL warnings; the compat dictionaries and tokens compile into `DiffView.Avalonia` |

## Phase 1 verification

| Item | Test | Result |
|---|---|---|
| 1 Padding above a line and after the last line, height exactly `(k + 1) · lineHeight` | `Item1_padding_run_pads_above_a_line_and_after_the_last_line_by_whole_rows` | pass: 4 rows for 3 above, 5 rows for 4 trailing, text row centred as a plain line's; glyph pixels only in the text row |
| 2 Caret skips the zero-length element; click in padding lands on the adjacent line | `Item2_caret_skips_the_padding_element_and_a_click_in_padding_lands_on_the_adjacent_line` | pass: Right, Left, Up, Down, End, Home move one position per press; clicks in padding above, at x = 0, and in trailing padding land on the right line |
| 3 Priming equalises extents before any scrolling; survives `Redraw`; re-primes after `Document` and `FontSize` change | `Item3_height_priming_equalises_extents_before_scrolling_and_survives_redraw_document_swap_and_font_change` | pass: extents differ before priming (94 vs 90 rows), equal 99 rows after, every shared row at the same top |
| 4 Own selection and caret over transparent editor brushes paint only text bands | `Item4_selection_and_caret_drawn_from_text_extents_paint_only_text_bands` | pass: no selection or caret pixels in three padding rows; both present in the text rows |
| 5 1:1 offset sync at top, middle and bottom | `Item5_offset_sync_is_one_to_one_at_top_middle_and_bottom` | pass: equal maximum offsets, equal first visible row, every shared row aligned at all three offsets |
| 6 Priming cost for 10k gaps | `Item6_priming_cost_for_ten_thousand_gaps` (`Category=Perf`) | measured: see below |
| `dotnet build DiffView.slnx -warnaserror` | | clean |
| `dotnet test --solution DiffView.slnx` | | 12 passed (7 from Phase 0, 5 spike); the `Perf` test is excluded by default and passes with `-p:IncludePerfTests=true` |
| New tests proven able to fail | | a temporary `Assert.Fail` at the top of item 1 failed the run before removal |
| Trimmed publish (`linux-x64`, self-contained) | | succeeds, 0 IL warnings, 48 MB; no shipped code changed in this phase |

## Phase 0 verification

| Check | Result |
|---|---|
| `dotnet build DiffView.slnx -warnaserror` | clean |
| `dotnet test --solution DiffView.slnx` | 7 passed: 1 Core unit, 3 ThemeAudit unit, 1 accessibility guard, 2 rendered smoke snapshots (Semi light and dark) |
| Headless host renders | `CaptureRenderedFrame()` returns an 800×500 frame; both snapshots verified |
| Demo boots under Semi and under Fluent | launched on this Wayland session with `--theme semi` and `--theme fluent`; the log shows the flags summary, `Starting`, the log directory and `XDG_SESSION_TYPE` |
| Trimmed publish (`linux-x64`, self-contained, `TrimMode=link`) | succeeds, 0 IL warnings, 48 MB output, boots and logs |
| Reference self-heal | removing `reference/DiffPlex` and building the Avalonia test project re-fetched it at its pin; `git status` shows nothing under `reference/` but the manifest and README |
| `theme-audit inventory` on Semi Light | 624 keys in 44 files; the tool packs into the local feed |
| Deliberate throw → dialog, F12 live log | **pass** at the window, Debug build and trimmed publish (user, 2026-09-04): the dialog shows with a working copy button and F12 opens the live log. Found: F12 pressed inside the live-log window did not close it, because the toggle lived on the main window's key handler only; fixed upstream in the diagnostics package (see *Upstreamed*) and consumed as 1.0.1 |

## Upstreamed to ClaudeForge

| Change | Reference | State |
|---|---|---|
| Live-log window ignored F12 (toggle lived on the host's main window only); and `LayeredEditors.Avalonia.Diagnostics` named a `PackageReadmeFile` it did not ship, so `dotnet pack` failed | [JanusMael/ClaudeForge#37](https://github.com/JanusMael/ClaudeForge/pull/37) | merged as `99c2963`; consumed here as diagnostics 1.0.1 — the merged source is identical to the packed branch head `f7980f2`, so the package did not change; the pin in `reference/sources.json` moved to `93065ba`, main's tip after both merges |
| Theme audit: the report for ClaudeForge's views (seven `SystemControl*` keys still referenced and undefined under Semi, one of them — `SystemAccentColorBrush` — defined by no theme at all), the generated `FluentKeys.Semi.axaml` / `SimpleKeys.Semi.axaml` merged in its `App.axaml`, the tool as a local dotnet tool, and the `docs/UI-STYLE-GUIDE.md` §2 update | [JanusMael/ClaudeForge#38](https://github.com/JanusMael/ClaudeForge/pull/38) | merged as `93065ba` after the user's click-through; `docs/theme-audit.md` regenerated against the merged checkout — ClaudeForge's own copies of the compat dictionaries now count as consumer-defined keys, leaving `SystemAccentColorBrush` as its one undefined key under Semi |
| A blanket "AvaloniaEdit is incompatible with Semi.Avalonia" note in `AgentsSkillsEditorView`, citing a `CLAUDE.md` that has never existed in that repository. The narrow claim was true when written — under bare Semi, AvaloniaEdit's Fluent theme leaves 6 keys undefined and its Simple theme 9, three and six of them `StaticResource`, which throws at template load — and stopped being true with PR #38's compat dictionaries, which take both to 0. Corrected in the comment, with the numbers and the corollary (AvaloniaEdit breaks loudly, not subtly, if those dictionaries are ever dropped) recorded as a new `docs/AVALONIA-GOTCHAS.md` entry | [JanusMael/ClaudeForge#44](https://github.com/JanusMael/ClaudeForge/pull/44) | merged as `168bf26`, 2026-09-08. Found while scoping the rejected plan 00002: the comment would have been quoted at the first attempt to host this control there, and it is wrong whether or not that ever happens |
| `StatusController`'s auto-clear ran on `Task.Delay` behind three mutable statics — `DelayOverride`, the two delay properties — and a hand-maintained `ResetForTesting` whose own comment warned that a missed seam leaks state across tests. Ported to an injected `TimeProvider` with per-instance delays, `Set` made private behind the five typed emitters so severity cannot be passed wrongly, and the token guard from *A pending clear belongs to the message that scheduled it* carried across. The binding surface is untouched, so no AXAML moved | [JanusMael/ClaudeForge#48](https://github.com/JanusMael/ClaudeForge/pull/48) | **merged as `07819f4`**, 2026-09-11; CI was green on ubuntu / macOS / windows and CodeQL. Net −108 lines: the tests lose five `Task.Delay(50)` sleeps, a 5 s watchdog helper, the pumped headless session and the seam-reset test, and run in 52 ms. One behaviour became assertable that was not — that a warning outlasts a success, which the old seam made indistinguishable |
| The accessibility guard scanned `src/ClaudeForge/Views/*.axaml` flat, leaving **16 of 43** AXAML files never examined, and counted neither `MenuItem` nor `RepeatButton`. It reported zero unnamed controls; there were 59. Widened to walk the three view-bearing assemblies recursively, with the baseline keyed by repo-relative path since a bare filename stopped being unique. Five controls named, each reusing the key its own header or tooltip already carried | [JanusMael/ClaudeForge#49](https://github.com/JanusMael/ClaudeForge/pull/49) | **merged as `6b6795a`**, 2026-09-11; CI was green on all three platforms. 54 controls in the two `PropertyEditorWrapper.axaml` files are baselined rather than guessed at, and tracked upstream. Proven able to fail by removing the name just added to `ModelPicker.axaml` — a file the old scan could not reach |
| `App.axaml` stated the status palette's contrast contract in prose — 4.5:1 on the pill, 7.3:1 on the page — and asked whoever retints one half to recheck both numbers by hand. Made a test that reads those brush values out of the AXAML, so a retint is measured rather than assumed | [JanusMael/ClaudeForge#50](https://github.com/JanusMael/ClaudeForge/pull/50) | **merged as `d9281c1`**, 2026-09-11, after a keep-both resolution of a `CHANGELOG.md` conflict with #49 — both added a bullet at the top of the same four-line section, with no semantic overlap. The margin is thinner than the prose implies: the worst pill pair clears by 0.07, the worst page pair by 0.05. Semi's two window grounds are the only written-down values, with a note that a Semi bump is what invalidates them |

**Filed rather than fixed**, because each needs a decision that is ClaudeForge's to make:
[#45](https://github.com/JanusMael/ClaudeForge/issues/45), the 54-control accessibility backfill —
most sit inside `DataTemplate`s over a property model, so the right announcement names the property
being edited, which is a design decision needing new keys mirrored into the Chinese resx;
[#46](https://github.com/JanusMael/ClaudeForge/issues/46), `PropertyEditorWrapper.axaml` existing in
two assemblies at 975 and 323 lines with different content; and
[#47](https://github.com/JanusMael/ClaudeForge/issues/47), four wall-clock dependencies in
`MainWindowViewModel` — including a two-second watcher-suppression window no test can assert either
half of, and a 150 ms debounce with an already-documented race — which the `TimeProvider` shape of
#48 would make testable.

**Two came back answered, 2026-09-11**, from the session working in that checkout. **#47 is
implemented** as its PR #51, on #48's shape — an optional `TimeProvider` defaulting to
`TimeProvider.System`, no production callsite changed, all four sites converted, six tests and no
sleeps. **#46's answer corrects the issue as filed**: *both* copies are canonical. Every
instantiation in that tree resolves to the app's `ClaudeForge.Controls` copy, so the library copy is
never rendered there at all — it is the packable library's default surface for an external consumer,
which makes its six unnamed controls **a defect in shipped API rather than internal debt**. The
guess in the filed issue that deleting one would retire its debt for free was wrong, and the two
files cannot share keys: the app's 48 go through `Strings.resx` plus eight locales plus the
designer, the library's 6 through `WrapperStrings` and its host `Resolver` seam. **#45, the
backfill, is the one still open** — what a screen reader should announce for 23 templated text boxes
over a property model is a design call, which is why it was filed rather than guessed at. As of the
evening of **2026-09-11** that session has #47 closed by its merged PR #51, and four further pull
requests open, of which **#53 is #45's backfill**: *"name all 54 interactive controls in both"*. All
three issues are being worked there; none of them is ours to carry.

**Two more filed, 2026-09-12**, from plan 00014 phase 1 — both about the *packable* library's
string seam rather than the app, both left for the session working in that checkout:
[#56](https://github.com/JanusMael/ClaudeForge/issues/56), `WrapperStrings` binding through
`{x:Static}`, which dereferences at parse time and caches — so the resolver has to be assigned
before any wrapper XAML is parsed, a constraint `Program.cs` honours and an external consumer with a
language picker cannot, and which is why that library has no equivalent of
`SideBySideDiffViewTests.Swapping_the_string_resolver_before_load_changes_the_rendered_strings`;
and [#57](https://github.com/JanusMael/ClaudeForge/issues/57), `Resolver` being
`Func<string, string>` with a private default, so a host cannot answer *"use your own"* for one key
and ClaudeForge's own `var _ => key` fallback renders the key on screen for anything its switch does
not cover. The `string?` fall-through is source-compatible; nothing gates the host's coverage
against the library's key set, where
`StringCatalogueTests.Every_declared_key_goes_through_the_resolver` is the shape that would.

## Measurements

| What | Value | Where |
|---|---|---|
| Test run, all three projects | ~20 s for 295 tests (~8 s at Phase 5's 200) | this machine, Debug, `Perf` excluded; the Reference-trait tests inventory 452 theme files; the presenter and composite tests render under all ten theme targets; the syntax tests wait on TextMateSharp's tokenizer thread |
| The 200,000-line pair in the composite (204,001 rows, 4,000 blocks) | build 297 ms; sources assigned through prime and layout 1,243 ms; first frame 14 ms; scroll to middle 20 ms, to end 17 ms, back to top 17 ms | `ScalePerfTests.The_200k_line_pair_builds_primes_paints_and_scrolls`, this machine, Debug. The left pane primes 4,000 padded lines and the right none: this fixture only inserts and modifies, so every gap falls on the left |
| The 1 MB single line in the composite | build 16 ms; assigned through prime and layout 612 ms; first frame 55 ms; scroll to the middle of the line 54 ms | `ScalePerfTests.The_one_megabyte_single_line_renders_and_scrolls_sideways`, same machine and configuration |
| An edit to the 200,000-line pair | keystroke 71 ms; frame while the model is stale 1 ms; re-diff through prime and layout 403 ms (engine 278 ms); frame after 13 ms; **4,000 lines re-primed, not 204,001** | `EditScalePerfTests.An_edit_to_the_200k_pair_re_diffs_re_primes_and_repaints`, this machine, Debug. A rebuild re-primes only the padding that moved, which is why it costs a third of the 1,243 ms a load from cold takes |
| Typing on the 200,000-line pair | 20 keystrokes inside the debounce: 378 ms (18.9 ms per key), settling in 380 ms, **1 build not 20**. With `LiveReDiff` off: 298 ms (14.9 ms per key), then `ReDiffNow()` in 366 ms | `EditScalePerfTests`, same machine and configuration. Live re-diff costs about 4 ms per keystroke in bookkeeping |
| Trimmed self-contained publish of the demo, linux-x64 | 57 MB after Phase 9 — TextMateSharp's grammars and themes (51 MB after Phase 5, 50 MB after Phase 4, 48 MB through Phase 3) | `dotnet publish -c Release -r linux-x64 --self-contained true` |
| Priming 10,000 padding gaps in one pass | 10.3–10.5 s (two runs) | `Item6_priming_cost_for_ten_thousand_gaps`, this machine, Debug; quadratic in the text view's built-line list |
| Priming 10,000 padding gaps in batches of 256 with `Redraw()` between batches | 200–240 ms (two runs) | same test, including the layout pass that republishes the extent |
| Fluent 12.1.2 inventory | 1153 keys, 85 files per variant | `docs/theme-audit.md` |
| Semi 12.1.0.1 inventory | 2225–2242 keys, 285 files per variant | `docs/theme-audit.md` |
| `FluentKeys.Semi.axaml` | 42 mapped, 1912 copied, 72 skipped (36 named sub-templates × 2 variants) | `docs/theme-audit.md` §Compat dictionaries |
| `SimpleKeys.Semi.axaml` | 38 mapped, 336 copied, 4 restored, 56 skipped | same |
| `DiffDocumentBuilder.Build`, generated similar pair, 10,000 lines (400 blocks) | 8 ms | `PerfTests.Build_on_a_similar_pair`, this machine, Debug, seed 11 |
| `DiffDocumentBuilder.Build`, generated similar pair, 200,000 lines (8,000 blocks, 204,001 rows) | 459 ms | same |
| `SimilarityGate.Measure`, 2 × 200,000 unrelated lines | 16 ms (similarity 0.000) | `PerfTests.The_similarity_gate_on_the_unrelated_pair`; the gated, unaligned build of the same pair takes 109 ms |
| `DiffSearch.Find` over the 10,000-line pair | literal 3 ms (1,995 matches); regex `\b(alpha\|beta)\b` 54 ms (3,940 matches) | `PerfTests.Search_on_the_10k_pair` |
| A 1,000,000-character single line against a copy with its tail changed: build, priming and the first frame in the composite | about 2 s for the whole test on this machine, headless, Debug (the test writes the exact figure to its output) | `WordDiffTests.The_one_megabyte_single_line_renders_without_pieces_and_the_marker_tooltip_says_so`; Phase 10 measures scrolling |
