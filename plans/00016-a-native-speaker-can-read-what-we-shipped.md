# 00016 — A native speaker can read what we shipped

The last thing standing between DiffView and a first release, named in `DECISIONS.md` §*"The eight
locales are machine-generated and unreviewed"* and carried in `PROGRESS.md` §*Open* since plan
00014: **1,144 strings in eight languages that no speaker of any of them has read.** Every file
header says so, `LocaleParityTests` proves every key is present and every placeholder preserved, and
`CHANGELOG.md` holds only `[Unreleased]` — so nothing shipped on them yet, and the gate is the
release rather than any commit.

**The blocker is not that nobody has reviewed them. It is that there is nothing to review from.** A
locale is 143 `<data>` elements in a `.resx`, and everything a translator needs in order to judge a
string — what it is, where it appears, what each `{0}` will be replaced with — lives in the
`<summary>` on a `const string` in `DiffViewStrings.cs`. Asking someone to review German by reading
XML against C# doc comments is asking them to do the build's job.

Plan 00015 is the precedent for the shape of this: the audit it performed was blocked for one plan
because nobody could tell a real failure from a correct one, and the fix was to make the thing
legible rather than to work harder at reading it.

## Goal

**A reviewer who speaks the language and does not read C# can review one locale end to end from one
document**, and a correction they return names its key unambiguously, so applying it is mechanical.

## Non-goals

| Excluded | Note |
|---|---|
| **Doing the review** | This plan produces the thing a speaker reviews. It does not make anyone German. The locales stay marked machine-generated until a person says otherwise |
| **A review the reviewer edits in place** | Tempting and wrong — see *One source of truth, and it is the resx* |
| **More locales** | Eight is the set `SatelliteResourceLanguages` names and the set ClaudeForge ships. A ninth is a separate change with a parity-gate row |
| **Changing any translation** | Nothing in this plan edits a `.resx`. A generator that rewrites its own input cannot be a drift gate |
| **Translating the demo's own chrome, or RTL** | Non-goals of plan 00014 and still non-goals; nothing here makes either harder |
| **A web tool, a spreadsheet, or a TMS** | A markdown file in the repository is reviewable in a browser, a diff and an editor, and costs nothing to keep. If a reviewer wants a spreadsheet, the table converts |
| **Screenshots per string** | The by-hand locale passes are `AGENTS.md` §9 work and do not scale to 143 keys × 8. The document names the surface in words; a reviewer who needs to see it runs the demo with `--culture` |

## Architecture

### Three sources, none of them re-authored

`scripts/gen-locale-review.cs`, a .NET 10 file-based app beside `gen-strings.cs` with the same
`.sh`/`.ps1` wrappers and the same `--check` mode, reads:

| Source | For |
|---|---|
| `DiffViewStrings.EnglishDefaults` | the English text — the compiled table, which is the single authored English surface |
| `Strings.<culture>.resx` | the translation under review |
| `DiffView.Avalonia.xml` | the `<summary>` per key: what the string is and what each placeholder holds. `GenerateDocumentationFile` is already `true`, so this is a build output, not a new artifact |

and writes `docs/locale-review/<culture>.md`, one per culture, each a table of **key · what it is ·
placeholders · English · the translation**.

The summaries are already written for exactly this purpose — *"A decorator failed on a line and is
disabled: `{0}` decorator, `{1}` line, `{2}` exception message"* — which is why this plan is mostly
a generator rather than a writing project. Mostly, and not entirely: see below.

### A summary that says *"the same"* is prose for a C# reader, not a table row

Measured before drafting rather than discovered during phase 2. **Nine of the 143 summaries do not
account for the placeholders their string takes**, and the nine are two different problems:

| | Count | Keys | What it is |
|---|---|---|---|
| **Back-references** | 4 | `LineTooltip.Aligned.Right`, `LineTooltip.Alone.Right`, `LineTooltip.Unified.Alone.Right`, `Minimap.LaneTooltip.Right` | *"The same, counterpart on the right."* Correct and readable **in the file**, where the left sibling two lines above spells out `{0}` and `{1}`. In a table row read on its own it resolves to nothing |
| **Genuine gaps** | 5 | `Status.Dirty`, `Save.Succeeded`, `Save.NoPath`, `Save.ChangedOnDisk`, `Save.Failed` | The placeholder is documented **nowhere**. *"Reported when the write itself failed"* never says that `{0}` is the side and `{1}` the reason. A documentation gap in public API, which `DECISIONS.md` §*Documentation is mandatory on public API* already forbids |

The two want different answers. The **five are repaired** — they are a real defect and this plan is
the moment it was noticed. The **four are a rendering problem**, not a writing one: their prose is
good, and a generator that flattens a file into rows is what breaks it. Resolving *"the same"*
textually is guesswork; the honest fix is that the generator **refuses to emit a context cell that
does not account for the string's placeholders**, and the four are repaired to stand alone like the
five.

That makes the rule a reviewer can rely on: **every context cell in the packet accounts for every
placeholder in its string**, enforced rather than hoped for.

### One source of truth, and it is the resx

The obvious design is a **Correction** column the reviewer fills in, and it is wrong: a generated
document that is also an input cannot be drift-gated, and the moment it is edited the repository has
two spellings of German and no rule about which wins.

So the packet is **generated, committed and gated, and never edited**. A correction goes into the
`.resx` and the packet regenerates — the same contract `gen-strings.cs` already has with the neutral
resx, and the same one `docs/theme-audit.md` has with its report. A reviewer works however suits
them — a copy, a comment, a message — and the repository keeps one spelling of German.

### A string that breaks the table must fail the build, not the table

No string in any of the eight currently contains a `|` or a newline — checked, 1,144 of them. That
makes this latent rather than present, and latent is exactly when to decide: the generator
**escapes** pipes and refuses newlines with a named key, rather than emitting a table that renders
as garbage and is noticed by nobody because the document is 143 rows long.

## Phases

| Phase | Size | Content |
|---|---|---|
| **1 — The nine summaries** | S | The five gaps written, the four back-references made to stand alone, and the test that every summary accounts for its string's placeholders. **Touches no generator and no locale**: it is a documentation repair to public API that happens to be the precondition for the rest, and it is worth landing whether or not the packet ever does. The test is red before it is green, which is unusual here only in that the red was measured before the plan was written |
| **2 — The generator, and one locale** | S | `gen-locale-review.cs` and its wrappers, reading the XML doc file, over `de-DE` alone. The phase's question is whether the summaries survive the round trip legibly enough to review from — answered by reading the output, not by its existing. Ships alone |
| **3 — Eight locales and the drift gate** | M | The other seven, and the test that the committed packet equals a fresh generation. Written against a deliberately stale fixture so it is seen red first |
| **4 — The record** | S | `DECISIONS.md`, `PROGRESS.md` §*Open* narrowed from *"unread"* to *"reviewable and unread"*, `AGENTS.md`, `CHANGELOG.md`, `README.md` |

## Testing

Every new test is proven able to fail before it is committed.

| Test | Asserts |
|---|---|
| The committed packet equals a fresh generation | The drift gate, per culture. Mutated by one row, it must fail — the `gen-strings.cs --check` contract, applied to eight files |
| Every key appears in every packet | 143 rows per culture, against `EnglishDefaults` rather than against the resx, so a key missing from **both** sides is still caught |
| Every key carries its summary | A key whose `<summary>` did not survive the XML round trip renders an empty context cell, which is a document that looks complete and is not |
| A pipe in a translation is escaped | Against a fixture string containing `|`, since no real one does yet |
| A newline in a translation is refused, by key | Same fixture, and the failure names the key rather than the file |
| **Every summary accounts for its string's placeholders** | Phase 1, and **it fails today on 9 of 143** — measured, not predicted. The set `{n}` named in the summary equals the set `{n}` in the English string, per key, with the failure naming the key. This is the test that makes the packet's context column trustworthy; without it the document looks complete and is not |
| The placeholder column matches the string | The `{n}` actually present in the string, independent of the summary. The two columns come from different sources on purpose, so the phase 1 test has something to compare |

## Risks

| Risk | Assessment |
|---|---|
| **The packet goes stale and is trusted anyway** | The failure mode that makes this worse than nothing, and the reason phase 2 is the gate rather than the seven files. `docs/theme-audit.md` is the precedent that this works here |
| **`DiffView.Avalonia.xml` is a build output the script depends on** | It exists because `GenerateDocumentationFile` is `true`, but a script that silently emits empty context when the build has not run is the *"looks complete, is not"* failure. Phase 1 fails loudly when the file is absent |
| **The summary and the string disagree about placeholders** | **No longer a risk — a measurement.** Nine of 143, split four back-references and five genuine gaps, and phase 1 is the repair. The risk it *was* is the one worth naming: had this been found during phase 3 it would have arrived as eight packets quietly carrying nine useless context cells, in a document nobody re-reads once it has been reviewed |
| **A reviewer cannot map a correction back to a key** | Why the key column is first and verbatim. A correction that says *"the third one under Header"* is the outcome this plan exists to avoid |
| **CJK and Cyrillic column widths** | Cosmetic in rendered markdown and irrelevant in a browser; named so it is not mistaken for a defect. Plan 00014 already measured the display-width question where it mattered, which was the menu |
| **Eight more generated documents to regenerate on every string change** | Real and accepted: one wrapper run, and the gate says when. The alternative is a review that cannot be trusted six weeks after it is done |

## Conventions

Conventional Commits, dense bodies, no AI attribution trailer. New tests are proven able to fail
before they are committed. An approved plan is committed before implementation and never edited;
drift goes to `DECISIONS.md`. A completed branch merges without asking once it is proven stable.
