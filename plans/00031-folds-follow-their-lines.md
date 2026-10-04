# 00031 — Folds follow their lines

> Status: **approved 2026-10-03**. Supersedes nothing.

An edit under the editor's folds breaks its layout before the edit's re-diff lands — open item 12, found
on 2026-09-28 and kept out of that day's fold fixes. Measured again on 2026-10-03 on `main` at
`44a7cd1`, over the folding pair with the right side editable and no context rows, each of three edits
throws from the first layout pass after it: a line typed at the top (*Line 2 was skipped by a
VisualLineElementGenerator, but it is not collapsed*), block 0 copied to the right (*Line 23 …*), and a
revert after a same-line edit (*Line 2 …*).

The cause is two records of one fact. `DiffPanePresenter.SetCollapsedLines` collapses line ranges and
hands the same ranges, as line numbers, to `FoldPlaceholderGenerator`, which keeps them. AvaloniaEdit
keeps its own record, and keeps it current: a `CollapsedLineSection` holds the `DocumentLine`s it starts
and ends on, which move with the text, and AvaloniaEdit 12.0.0's `HeightTree` shrinks a section whose
first or last line is deleted and uncollapses one whose lines are all deleted. After any edit that moves
lines, the generator spans lines the height tree no longer collapses, and the text view throws. Only the
editor edits, so the viewer and the unified view never reach this state; the unified view rewrites its
text for each model and refolds after it, which PR #11 fixed as open item 11.

This plan makes the generator read AvaloniaEdit's record instead of keeping its own.

## Decisions

| Decision | Why |
|---|---|
| `FoldPlaceholderGenerator` reads the pane's live `CollapsedLineSection`s — each one's first and last line as they are now, and only while it is collapsed — and keeps no line numbers | AvaloniaEdit already moves, shrinks and uncollapses the sections as the text changes, and the height tree is what the text view checks the generator against, so a generator that reads the sections cannot disagree with it. Measured with a throwaway spike on 2026-10-03: all three edits stop throwing, every fold stays collapsed over the lines it covered, the right pane's extent grows by exactly the rows inserted — one for the typed line, three for the copied block — and the 40 existing folding tests pass unchanged |
| A fold keeps the identity it was collapsed under — its first line in the model that folded it — and the pane translates between that identity and where the fold is now | The controller names a fold by the model's lines (`FoldPlan.LinesOf`), and between an edit and its re-diff the model still has the old ones. Measured under the spike: a placeholder that moved from line 2 to line 3 reports 3 when clicked, `OnFoldExpandRequested` finds no fold with that first line, and nothing opens until the re-diff lands. So the placeholder reports the fold's identity rather than its live line, and `FoldAtCaret`, behind the expand command, asks the pane which fold's header line the caret is on instead of comparing the caret with the model's lines, the same comparison the click made |
| No refold on an edit; the re-diff refolds as it does today | Folding is a row range of a model, and between an edit and its re-diff there is no model to fold from. The stale model already governs every decoration in that window — fills, padding, the border, the connector — and none of them throws; folds were the one that did |
| Folds stay folded through an edit | Opening every fold under a keystroke and folding again when the re-diff lands would make every edit flicker in both panes and lose the reader's place |

### Dismissed

- **Refolding on every edit.** It needs a model, and a model is what the re-diff builds.
- **Opening the folds under an edit.** Safe, and it flickers on every keystroke; see above.
- **Shifting the generator's line numbers on `TextDocument.Changed`.** A second implementation of what the height tree already does, including its rules for deleted edges, which is how the two records came apart in the first place.

## Scope

**In.** `FoldPlaceholderGenerator` reading the sections; `DiffPanePresenter` keeping each section's
identity and answering which fold is on a line; `DiffBuildController`'s two fold lookups, the click and
the caret; the tests; `AGENTS.md` §6's row *Collapsing a line is not hiding it*, which names the
generator's ranges; `DECISIONS.md`; `PROGRESS.md`, where open item 12 closes.

**Out.** The other decorations between an edit and its re-diff, which draw from the model as they always
have and are corrected by it. The unified view and the viewer, which do not edit — though both share the
generator and so get its change.

## Phases

| Phase | Size | What | Verified by |
|---|---|---|---|
| 1 The tests, red on `main` | S | Over the folding pair with a side editable and no context rows: a typed line above the folds, a block copied above them and a revert after a same-line edit each leave the layout whole, every fold collapsed over the lines it covered and every placeholder on the line before its fold; a deleted edge line shrinks its fold, and a fold whose lines are all deleted leaves no placeholder; a moved placeholder clicked, and the expand command with the caret on a moved placeholder's line, each open that fold; and once the re-diff lands the folds are the new model's. The layout failures are collected from `Dispatcher.UIThread.UnhandledException`, as `FoldingOptionTests` collects them | Red on `main`'s sources: every test that edits under a fold fails at its collector, the layout throwing first. With only phase 2's generator change applied — the spike's state — the edits pass and the click and the caret stay red at the section count, which is what makes a fold's identity part of the repair rather than a refinement of it |
| 2 Folds follow their lines | S | The generator over the live sections; the pane keeping each section's identity and translating it; the controller's click and caret lookups through the pane | Phase 1's tests green. Scratch mutations, each killed by a named test: the generator back to the numbers it was handed; the placeholder reporting its live line; the caret lookup comparing with the model's lines again; a section read without its collapsed check. The suite passes in `en-US` and `de-DE` under `catch-crash --expect auto`, the build clean under `-warnaserror`, `scripts/mutate-gates.sh --guards` clean. By hand on this box per `AGENTS.md` §9, the demo with `--edit right` and *Show differences only*: a line typed above a fold, a copy arrow pressed above one, and a revert, with no fault in the log |
| 3 The record | XS | `AGENTS.md` §6, `DECISIONS.md`, `PROGRESS.md` | All five CI jobs green |

## Risks

| Risk | Assessment |
|---|---|
| A live line number costs a walk of the line tree | Each placeholder asks for two per layout, and a pane has as many folds as runs between change blocks. The spike's tests ran in their usual time; the 200k-line pair under folding is measured before and after in phase 2 if it moves a `Perf` number |
| AvaloniaEdit changes how a section treats a deleted edge | The deletion tests in phase 1 pin today's rules, so a bump that changes them fails there rather than in a layout pass |

## Conventions

Conventional Commits, dense bodies, no AI attribution trailer. Every new test proven able to fail
before it is committed. An approved plan is committed alone before implementation and never edited;
drift goes to `DECISIONS.md`. Work on its own branch from `main`; pushed only when Brian says.
