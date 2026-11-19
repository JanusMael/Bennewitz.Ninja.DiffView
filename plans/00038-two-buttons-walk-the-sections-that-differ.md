# 00038 — Two buttons walk the sections that differ

> Status: **approved 2026-11-19**. Supersedes nothing.

Brian, 2026-11-19, reviewing JanusHalo's `CLAUDE.md` against the JANUSSER6 seed in AgentBaseline's review window:
finding each section's mark meant scrolling and clicking, and a press-and-drag in the right pane near a tall padded
line ended the window in a stack overflow, AvaloniaEdit's caret bringing itself into view without end, which a fix to
the library stops. He asked for a pair of buttons, *Previous section* and *Next section*, each going to the previous
or next section and putting the keyboard in the pane that has it, so that section's mark shows: in the only pane that
has the section, or in the right pane when both do. His words for which sections were *"the relevant section"*.

The mark is plan 00035's: `ReviewWindow.Mark` sets the view's `LeftEmphasis` or `RightEmphasis` from the two caret
handlers, and the line under the panes names the section at the caret of whichever pane moved last. The buttons change
neither: they move a caret, and the mark and the line follow it as they follow a click.

DiffView does not change. What the buttons need is public: the view's model for the rows, each pane's caret and
keyboard focus, and AvaloniaEdit's own scrolling.

## 1. Decisions

| # | Decision | Why |
|---|---|---|
| 1 | **The walk holds the sections that differ.** Each of the candidate's sections that the accumulator holds no identical version of, compared by `SectionIdentity` as a decision is, its heading path and its folded content; and each of the accumulator's sections at a heading path the candidate has no section at. A section the two hold alike is passed over, and so is the text above either's first heading, which is in no section | Brian's, 2026-11-19. A verb has work only where the two differ: take, leave and defer on the candidate's version, drop on the accumulator's own. A take of a section held alike changes nothing, and a leave of one rejects the baseline's own text. Compared as a decision compares, the walk and the line under the panes agree on what is the same version, a re-wrapped paragraph included (plan 00036) |
| 2 | **A section goes to the right pane when the candidate has a section at its heading path, and to the left when only the accumulator has one, and the keyboard goes with it** | Brian's, 2026-11-19: the only pane that has the section, or the right when both do. The right pane's caret is the one take, leave and defer read, and the left's the one drop reads |
| 3 | **The walk runs in row order**: top to bottom through the aligned rows the two panes show, each section at the row of its heading in the pane it goes to, and on one row the left's before the right's. The rows are the view's public model, `SideBySideDiffView.Document`, a line's row being its `DiffLine.Row` | Brian's, 2026-11-19. The panes scroll as one, so their rows are the order the eye follows. A row keeps each side's lines in order, so the candidate's sections keep the candidate's order, the accumulator's keep theirs, and a section only the accumulator has comes where it stands between the candidate's |
| 4 | **A press counts from the caret of the pane that has the keyboard, or, while something else has it, of the pane that last had it.** Next goes to the first section of the walk below the heading of that caret's section, or below the caret's own row when it is in no section; Previous to the last above it. Until the walk lands or a click or a key moves a caret on the candidate shown, a press counts from the top | Brian's, 2026-11-19, F6 included: the keyboard is where the operator is working, a click putting it there, F6 moving it and the walk leaving it where it lands. From inside a section a press goes to its neighbour rather than back to its own heading. Not from the caret that moved last, which the mark and the line follow: a take or a drop moves the left's caret as it edits (`ReviewWindow.OnLeftCaretMoved`), so a press after a take would go on from wherever the edit left that caret |
| 5 | **At either end a press wraps round**, Next from the last section to the first and Previous from the first to the last, and the line under the panes says it came round. Whether it wraps is `ReviewWindow.WrapSectionWalk`, a styled property, `true` by default and bound to nothing yet; set `false`, a press at either end says so and moves nothing, as F7 stops at the last change and F8 at the last candidate. A candidate with no section that differs says that either way | Brian's, 2026-11-19: wrapping by default, and as an option a later setting or a style can bind without the commands changing. Saying that it came round keeps the one thing a stop would have told, that every section has been passed |
| 6 | **Ctrl+N goes to the next section and Ctrl+P to the previous**, bound on the window as F8 is, so either pane may have the keyboard, and named once beside F8's two gestures; each button's tooltip names its chord, as the candidate buttons name F8 | Brian's, 2026-11-19: Beyond Compare's next and previous difference, as his has them. Neither is bound anywhere a press in the window could reach: AvaloniaEdit binds no N or P, and neither do Avalonia's platform hotkeys or the view's key map. Every chord is to have sane defaults and one common way to configure it, which does not exist yet, so these are fixed until then, as F8 is |
| 7 | **The caret lands at the start of the section's heading with nothing selected, and the panes scroll so that the heading's row is the top one in view**, as far as the panes scroll. AvaloniaEdit's public `TextEditor.ScrollTo` does it with `VisualYPosition.TextTop`, which places a padded line's own row rather than the padding above it, measured in step 2 before it is relied on; if it does not, the step stops for Brian | Brian's, 2026-11-19. The heading names the section and the mark runs down from it, so as much of the section as the panes hold is in view below it. A selection left elsewhere would read as the copy arrows' subject |
| 8 | **A press that lands where that pane's caret already is marks and names the section itself**; otherwise the caret handlers do, as for a click | A caret already in place raises no move, and the mark would stay in the other pane |
| 9 | **While the view has no model, a candidate just shown and its diff not yet built, a press says so and moves nothing.** While the model lags an edit of the left, until the re-diff lands, a line past its end counts after its last row | Without rows there is no order. A lagging model is still what the panes are drawn aligned by until the re-diff lands |
| 10 | **The two buttons stand between the candidate buttons and the verbs**, `PreviousSection` and `NextSection` by `AutomationId`, each with an automation name and a tooltip, and enabled while a review is open, as the others are | They move within the candidate the pair to their left moves between, to the section the verbs to their right act on. `BNXQ1011` holds a button to its tooltip, and the accessibility gate to its name |

Dismissed, one line each:

- **Every section, those held alike included**: a candidate holding most of the baseline's text would be mostly presses
  past sections with nothing to decide.
- **Only the sections not yet decided**: the line under the panes already says what the store knows of each, and a
  version left out would drop out of the walk as it was left, out of Previous's reach.
- **The candidate's order, then the accumulator's own sections**: a section only the accumulator has would be met far
  from where it stands on screen.
- **Going on to the next candidate at either end**: that is F8's, and a press meant for a section would replace the
  right pane.
- **Alt+Down and Alt+Up, Ctrl+Down and Ctrl+Up, or F9 and Shift+F9**: each free in the panes, and none the chord
  Beyond Compare walks its differences with.
- **Centring the heading, as F7 centres a change**: gives the top half of the panes to the section before it.
- **Scrolling only as far as brings the heading into view**: a heading reached from above stands at the bottom edge with
  its section out of view.
- **The section's first changed line**: the verbs act on the whole section, and in a long one the heading goes out of
  view.
- **The walk as a command of DiffView's**: a section is AgentBaseline's notion; the library knows lines and rows.
- **An order read from a diff of the window's own**: a second alignment, which can disagree with the one on screen.

## 2. Scope

**In:** AgentBaseline: the walk in `ReviewSession`; the buttons, their chords, the keyboard, the caret and the scroll in
`ReviewWindow`; their tests and proof set; the remark on `AccessibilityCoverageTests.BaselineInspectedFloor`, which
counts the window's buttons; a log line for each press, written as the window's other acts are, should plan 00037's
log be in by then; `AGENTS.md` §10, `DECISIONS.md` and `PROGRESS.md`.

**Out:** DiffView, which does not change; the unified view, which AgentBaseline does not show; configuring the chords,
which waits for the one way every chord is to be configured; hiding or folding the sections held alike; any new verb.

It follows the library's fix to how a pane brings its caret into view on a padded line, which a landing on a padded
heading leans on, and plan 00036, from whose shown candidate the right pane's sections are read.

## 3. Steps

| Step | What | Verified by |
|---|---|---|
| 1 | The walk: `ReviewSession.SectionAfter` and `SectionBefore`, from a side and a line, and whether to wrap, to the next or previous section of the walk, the pane it goes to and whether it came round, or none; read from the accumulator's live outline, the shown candidate's and the view's rows (decisions 1 to 5 and 9) | New `SectionWalkTests`, each proven able to fail: a changed section and one only the candidate has go to the right, one only the accumulator has to the left, and one held alike, or alike once re-wrapped, is passed over; the order over a candidate that reorders the accumulator's sections, with one only the accumulator has standing between two of the candidate's, and two on one row; a count from inside a section, from the preamble and from the top; both ends, wrapping round and, with `WrapSectionWalk` off, stopping; no model yet, and a model lagging a take. A new set, `tests/AgentBaseline.Tests/Mutations/section-walk.json`, with its green control |
| 2 | The window: the two buttons, their commands and chords, `WrapSectionWalk`, the keyboard, the caret and the scroll (decisions 5 to 8 and 10), the scroll measured first on a heading carrying padding | Tests in the same class, driven through the window as `ReviewWindowTests` drives it: each button through its peer and by a pointer's click, whose press puts the keyboard on the button before its command runs, and each chord from either pane; the keyboard in the pane the section went to, the mark and the line on that section, nothing selected; a press landing where the caret already is moving the mark; a press after a take whose edit moved the left's caret still counting from the right, and one after F6 counting from the pane F6 moved the keyboard to; the heading's row at the top, read against its row times the line height rather than from the scroll itself, and as far as the panes go near the end; each end wrapping round and saying so, and with the property set `false` in the test, stopping and saying so. Their mutations in `section-walk.json`, and `mutate-behaviour --owed main --parallel 4` over every set whose mutations edit the window or the session |
| 3 | The record: `AGENTS.md` §10's rows for the walk and the buttons; `DECISIONS.md`; `PROGRESS.md`; `BaselineInspectedFloor`'s remark brought to the buttons the window then carries; the suite under `catch-crash --expect auto`; a full `mutate-gates --parallel 4`, the window's markup being a gated subject | The suite's verdict, the gates as declared, and Brian's look at the walk over the first pair in the review window, which no test gives |
