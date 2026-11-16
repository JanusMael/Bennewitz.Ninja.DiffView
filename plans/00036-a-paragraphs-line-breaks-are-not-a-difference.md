# 00036 — A paragraph's line breaks are not a difference

> Status: **approved 2026-11-16**. Supersedes nothing.

Brian, 2026-11-16, reviewing the 2026-11-10 corpus in AgentBaseline's review window: in the first pair one side has
short lines and the other very long ones, which makes it hard to diff by eye. Measured: JanusHalo writes each
paragraph on one line, its longest 462 characters and 51 lines over 120, where the seed, JANUSSER6, wraps near 100,
its longest 123. Compared with every run of whitespace read as one space, 74 of JanusHalo's 85 blank-line-separated
blocks, 85% of its text, are the seed's word for word, yet only 60 of the 129 non-blank lines the right pane shows
equal a line of the seed. The diff compares lines, so the pair reads as changed nearly throughout, and the eleven
blocks that really differ are lost among the rest. brianb-2025, wrapped like the seed, reads well: 210 of its 251
lines equal one of the seed's.

In CommonMark a line break inside a paragraph is a soft break, rendered as whitespace: where a paragraph breaks its
lines is layout, not content. This plan makes AgentBaseline treat it so, in what the review shows, in what a take
writes and in what a decision is about. It extends plan 00034's decision 8, which normalised line endings and a
document's list nesting *"so the diff compares like with like"*, and its decision 6, whose identity is taken after
that normalisation because without it *"neither matches across platforms"*.

DiffView does not change. Its panes cannot wrap, `DiffPanePresenter` forcing `WordWrap` off because wrapped lines
cannot stay row-aligned, and wrapping would not help here: the diff would still set one line beside three.

## 1. Decisions

| # | Decision | Why |
|---|---|---|
| 1 | **A soft line break inside a paragraph is one space.** `ParagraphBreaks.Fold` turns each soft break in a paragraph into a single space, the spaces ending the line and the indent beginning the next going with it as CommonMark strips them, and leaves everything else as it is: a hard break (two trailing spaces or a backslash), a heading, a fenced or indented code block, an HTML block, a table, a blank line, and the structure of a list or a quote. Its parse enables Markdig's pipe tables | CommonMark renders a soft break as whitespace, so two paragraphs that fold to one text render alike; everything else that breaks a line means something by it. The outline's own pipeline has no pipe tables, and under it a table is a paragraph whose rows a fold would join |
| 2 | **A section's identity hashes its folded content.** `SectionIdentity.Of` hashes `Fold(Content)`, so one rule wrapped one way on one machine and another way on the next is one version, and a decision about it holds for both. Text with no soft break folds to itself, so its hash does not move | Decision 6's own reason: an identity that differs across machines where the rule does not is the failure normalisation exists to prevent. Staleness, a take's reading as edited or not, and the save's report all read identities, so each stops counting a re-wrap as a change |
| 3 | **The decision file goes to version 2.** A take's hash is checked against its folded text, and a version-1 file is refused, the refusal naming the version this tool reads | A version-1 hash was taken over unfolded text: read as version 2, a take of a wrapped rule fails its check, and a rejection of one silently stops matching it. No version-1 file exists: Brian has never saved a baseline |
| 4 | **The right pane shows the candidate re-broken against the accumulator.** Each candidate paragraph whose folded text equals an accumulator paragraph's, at the same heading path first and then anywhere, is broken where the accumulator's is, each continuation line indented as the candidate's own container needs. The left pane is the accumulator, byte for byte | Where the words are the accumulator's, the lines are then the accumulator's, and the diff shows no change: the operator reads the candidate's words laid out as the baseline lays them out. Only the right is shown other than as it is held, because the left is the baseline and is what a save writes |
| 5 | **A paragraph that differs in words is re-broken by aligning its words with its counterpart's.** The counterpart is the accumulator paragraph that a line diff of the two sections' folded paragraphs pairs it with; the words are aligned by the public `DiffDocumentBuilder.Build` over one word a line; each of the counterpart's breaks moves to the matching place in the candidate | The eleven blocks that differ are the review's work. Broken where the accumulator breaks, each unchanged line pairs with an equal line and a changed word sits in a short line with its word pieces, where a 462-character line set beside three shows most of itself as inserted. The builder is the public diff `Corpus.Queue` already runs, so AgentBaseline takes no new dependency |
| 6 | **Every shown text is checked before it is shown.** Parsed, it must give the candidate's outline, the same heading paths in the same order, and each of its sections must fold to the candidate's own section. A paragraph whose re-break fails the check is shown as the candidate wrote it | `AccumulatorEdits`' pattern, a precondition rather than a hope: a line beginning with `-`, `1.`, `#`, `>`, `===` or a fence opens a block or turns the paragraph above it into a heading, so a break before such a word changes what the text is |
| 7 | **A take writes what the right pane shows, and a take whose folded content equals the accumulator section's is `Unchanged`** and writes nothing | What the operator takes is what they read. Where the words are the accumulator's, a take leaves its layout alone rather than writing the same words wrapped otherwise, and a paragraph the accumulator has no version of comes in as the candidate wrote it |
| 8 | **The re-break is made when a candidate is shown**, against the accumulator as it reads then; typing in the left re-breaks nothing until the next move | Replacing the right's document under every keystroke in the left would move the right pane's caret and scroll each time |
| 9 | **The queue's *most changes first* counts against the re-broken text** | A candidate that differs from the seed only in where it breaks its lines is not the one likeliest to give the most |

Dismissed, one line each:

- **Word wrap in DiffView's panes**: wrapped lines cannot stay row-aligned, and the diff would still set one long line
  beside three short ones.
- **Folding both panes' paragraphs to a line each**: the long lines everywhere, and the left is the baseline a save
  writes.
- **Reflowing both sides to one width**: every paragraph of the baseline rewritten in a layout no machine wrote.
- **Folding at ingest, into `IngestedDocument.Text`**: both panes folded, and every take writing one-line paragraphs
  into the baseline.
- **Re-breaking the display and leaving identity as it is**: a decision about a rule wrapped one way would not hold
  for the same rule wrapped another, and staleness would fire on a re-wrap, decision 6's failure.
- **An option in DiffView to ignore breaks within a paragraph**: the library's diff is about lines and knows no
  markdown; a paragraph is AgentBaseline's notion.
- **Migrating version-1 decision files**: none exists, and a rejection stores no text to hash again.

## 2. Scope

**In:** AgentBaseline: the fold; the identity and the decision file's version; the shown candidate and everything that
reads it, which is the right pane, the caret's section, the mark, a take and the queue's order; their tests and proof
sets; `AGENTS.md` §10, `DECISIONS.md` and `PROGRESS.md`.

**Out:** DiffView, which does not change; how a candidate is ingested, and what provenance records of it; the
accumulator's own layout; re-wrapping a paragraph the accumulator has no counterpart for; every other markdown
normalisation, list markers, emphasis markers, trailing spaces and a heading's form among them.

It follows plan 00035, whose mark in the right pane is placed from the sections this plan re-numbers.

## 3. Steps

| Step | What | Verified by |
|---|---|---|
| 1 | The fold: `ParagraphBreaks.Fold`, over a parse with pipe tables | New tests, each proven able to fail: one paragraph wrapped three ways folds to one text; a hard break of each kind, a fenced and an indented code block, a table, an HTML block and a list's structure keep their breaks; a list item's and a quote's wrapped paragraphs fold; text with no soft break comes out byte for byte. A new set, `tests/AgentBaseline.Tests/Mutations/paragraph-breaks.json`, with its green control |
| 2 | Identity and the file: `SectionIdentity.Of` over the fold; `DecisionFile` version 2, a take's hash checked against its folded text and a version-1 file refused; a take whose folded content equals the accumulator section's is `Unchanged` | `SectionIdentityTests` gains a rule re-wrapped keeping its identity; `DecisionFileTests` the refusal of version 1; staleness and the save's report each tested over a re-wrap; the sets holding those tests gain the mutations that kill them, and `mutate-behaviour --owed main --parallel 4` runs every owed set as declared |
| 3 | The shown candidate, for exact matches: decisions 4, 6 and 8; `ReviewSession` reads the shown text's outline for the caret's section, the mark and a take; the queue counts against it, decision 9 | Tests over a synthetic pair shaped like the first one, a candidate nested in a list item with a paragraph a line against a seed wrapped near 100: every block equal in words shows as equal lines; a paragraph whose re-break would open a list is shown as written; a take of a matching section writes nothing; the queue puts a re-wrapped candidate after one that differs in words. Their mutations in the sets holding their classes, one class a set |
| 4 | Near matches: decision 5's word alignment | Tests: a paragraph differing in one word shows every other line equal to the accumulator's, the changed word in a line pair of its own; a word added at a line's start or end, and a paragraph twice its counterpart's length, pass decision 6's check. Their mutations in the set holding their class |
| 5 | The record: `AGENTS.md` §10's identity row and a row for the shown candidate; `DECISIONS.md`; `PROGRESS.md`'s open item; the suite under `catch-crash --expect auto`; the measure above taken again | Blocks equal in words showing as equal lines in the first pair, against 60 of 129 lines before; Brian's look at the first pair in the review window, which no test gives |
