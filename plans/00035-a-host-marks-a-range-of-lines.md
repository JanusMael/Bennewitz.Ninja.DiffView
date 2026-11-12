# 00035 — A host marks a range of lines

> Status: **approved 2026-11-12**. Supersedes nothing.

Open item 36. Brian, 2026-11-10: the review window should mark the section its verbs act on, and the mark is built
in the library, as a small public way for a host to emphasise a range of lines in a pane. Only the library can draw
it right, because only the library can tell a line's own row from the padding the other file's lines put above it.

## 1. Decisions

| # | Decision | Why |
|---|---|---|
| 1 | Two styled properties on `SideBySideDiffView`, `LeftEmphasis` and `RightEmphasis`, each a `LineRange?` over that side's document lines, 0-based as `LineRange` already is; `null`, the default, marks nothing | One range a side is what a section needs. The library takes every other host choice as a property, which markup and styles can set, and `LineRange` is the public type a range of a side's lines already has |
| 2 | `DiffViewer` takes both through `AddOwner`, the same registrations | A viewer's host has the same reason to point at lines, and `SharedRegistrationTests` holds the viewer to the editor's registrations |
| 3 | The range is the host's: the library never writes it, never moves it with an edit and keeps it across a new source; lines past the document's end are ignored, and none throws | A value the control writes outranks the host's for the life of the control, `ShowBanner`'s lesson in `AGENTS.md` §6. Only the host knows what its range means after an edit, and AgentBaseline reads its sections again on every caret move |
| 4 | Drawn as a translucent fill over each marked line's **row**, one line height tall below any padding above the line, across the text area, on `KnownLayer.Background` after `DiffLineBackgroundRenderer`: over its row tints and the current block's border, under the find matches, the selection and the text | A row never covers padding, which belongs to the other file's lines, and consecutive rows touch, so a section reads as one block wherever padding does not interrupt it. The text band item 36 named does not touch its neighbour: two lines' bands leave `2 × HalfSlack` between them, so a fill by text bands would be striped. Over the tints, so a changed line still shows the mark |
| 5 | A renderer of its own, `EmphasisRenderer : GuardedBackgroundRenderer`, reset with the others in `ResetFaults` | Every decorator is a fault boundary, `AGENTS.md` §1: a mark the renderer cannot draw disables the mark and nothing else |
| 6 | One new token, `DiffView.EmphasisBrush`, translucent, in both palettes and both variants, with a `contrast-pairs.json` pair for text over it | `AGENTS.md` §4: a new colour token goes into the two token files and the pairs together, and text is drawn over this fill |
| 7 | A fold inside the range marks its placeholder's row | A fold is one visual line spanning its run, and marking it says the range reaches into what is folded |
| 8 | The unified view gets no emphasis in this plan, recorded as a divergence in `AGENTS.md` §7 | Its one pane interleaves both files: a side's contiguous range is split there by the other side's lines and loses its context lines, so what to mark is a question of its own |
| 9 | AgentBaseline marks the section the line under the panes names, in the pane whose caret moved last, from the section's heading to its last line of text; it clears the other pane's mark, and marks nothing in the preamble | The mark and the line then say the same thing, and the verbs' subject is one block on screen. A section's trailing blank lines carry no rule, so the mark stops where its text does |

Dismissed, one line each:

- **A method, `Emphasise(DiffSide, LineRange?)`**: it cannot be set from markup or a style.
- **Several ranges a side**: one host needs one range each, and a collection added later does not break these two.
- **A mark in the gutter as well**: the gutter's columns belong to the numbers, the copy arrows, the change markers
  and the modified-since-load bar.
- **A range of the alignment's rows rather than a side's lines**: a host knows its document's lines, never the
  library's rows.
- **Both panes' sections marked at once in AgentBaseline**: two shaded blocks would leave it to the eye which one the
  line and the verbs mean.

## 2. Scope

**In:** the two properties on both two-pane views; the presenter's state and its renderer; the token; the hosting
guide's rows; AgentBaseline's use.

**Out:** the unified view (decision 8); several ranges a side; animation; any key that moves the mark, which follows
the caret.

## 3. Steps

| Step | What | Verified by |
|---|---|---|
| 1 | The token: `DiffView.EmphasisBrush` in `DiffView.Tokens.axaml` and `DiffView.Tokens.ColorBlind.axaml`, both variants; `DiffBrush.Emphasis` with its `DiffBrushes.Tokens` entry at the same position, the list being read by the enum's position; a pair in `contrast-pairs.json`; then `theme-audit compat` and `theme-audit report` | `ThemeResolutionTests.Every_DiffView_token_resolves_in_both_palettes` and `ReferenceAuditTests`; the pair passing its floor. A pair scores the fill over one surface, so its composite over each row tint is measured and recorded in `DECISIONS.md` |
| 2 | The presenter: an internal `Emphasis` that invalidates `KnownLayer.Background` when it changes, as `CurrentBlock` does; `EmphasisRenderer` added after `DiffLineBackgroundRenderer`, reset in `ResetFaults`, keeping the rectangles it last drew | New `EmphasisTests`, each proven able to fail: the fill covers exactly the marked lines' rows and no padding row; a range past the end draws what exists and throws nothing; a throwing renderer reports once and disables itself; a fold inside the range marks its placeholder |
| 3 | The views: `LeftEmphasis` and `RightEmphasis` on `SideBySideDiffView` and, through `AddOwner`, on `DiffViewer`, pushed to the panes on the template path and on a change, `AGENTS.md` §6's two paths | Tests that set each property before the template applies and after load, on both views; `SharedRegistrationTests`; `fixtures/api/sidebyside.txt` and `fixtures/api/viewer.txt` regenerated, four lines a property; a frame in both variants, `[LinuxBaseline]`, beside a pixel assertion, rendered in WSL and reviewed by Brian before it is committed |
| 4 | The library's record: the hosting guide's rows; `AGENTS.md` §4 on the renderer and its layer, §6 on the two paths and §7 on the unified view; `CHANGELOG.md`; a proof set under `tests/DiffView.Avalonia.Tests/Mutations` | `HostingGuideTests`; the set as declared; a full `mutate-gates --parallel 4` run, the themes and the library being gated subjects |
| 5 | AgentBaseline: `ReviewSession` returns the accumulator's section rather than only its path; the window sets the mark from both caret handlers, clears the other pane's and the preamble's, clears it on a move to another candidate, and reads it again when typing moves a section's end under an unchanged heading path | `StandingLineTests`, one test a behaviour; `tests/AgentBaseline.Tests/Mutations/review-window.json` gaining the mutations that kill them; `AGENTS.md` §10's row |
| 6 | The record: `PROGRESS.md` item 36 and `DECISIONS.md`; Brian looking at the review window with the mark in it | His word on the window, which no test gives |
