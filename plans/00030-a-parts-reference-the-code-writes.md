# 00030 — A parts reference the code writes

> Status: **approved 2026-11-01**. Supersedes nothing.

What a developer needs to know about a control of this library — which template parts it has, what
each one is, which of them code looks up, which `AutomationId` a harness finds it by, and what UI
Automation calls the control — is spread across seven theme files, the lookups in the C#, AvaloniaEdit's
own code, `fixtures/automation-ids.txt` and the peer table in a test. Asked for on 2026-10-01, five
tables of it were assembled by hand from a scratch script, and Brian asked whether they could be
generated live from the code instead. They can, and the repository already has the shape:
`docs/locale-review/*.md` and `docs/theme-audit.md` are generated, committed, and failed by a test the
moment the code moves away from them.

This plan writes `docs/template-parts.md` that way: generated from the code, committed so it reads on
GitHub and in a pull request, never edited by hand, and gated so it cannot go stale. It was parked on
2026-10-03 until XamlQuality made `BNXQ1003`'s reading of template parts public, so that the column
saying what code looks a part up comes from the rule rather than from a second reading beside it.
`2026.4.1014` did, as `TemplatePartRule.ReadParts`, and item 14 moved the pin to `2026.4.1026`.

## What it shows

The five hand tables of 2026-10-01, generated:

| Table | Rows | Columns |
|---|---|---|
| The controls | Every concrete control type the library defines, internal ones included: the three views in the views table's order, then the other themed controls, then the drawn ones, each of the last two in ordinal order | Visibility, base class, the theme file and its part count — or *drawn, no template*, and for the two margins *drawn, built by the pane* — the automation peer's control type, and the `AutomationId` the control's own code gives it, where it gives one |
| The views | Every part any of the three views declares | Element type; one column each for `SideBySideDiffView`, `DiffViewer` and `InlineDiffView`, saying whether its template has the part; the `AutomationId`; *Looked up in code* |
| `DiffFindBar` | Every part its theme declares | Element type, `AutomationId`, *Looked up in code* |
| `DiffStatusStrip` and `DiffPaneHeader` | Every part either theme declares, the strip's first | The control, element type, `AutomationId`, *Looked up in code* |
| `DiffPanePresenter` | Its own theme's parts, then those of the text area theme it applies, which targets AvaloniaEdit's `TextArea` | The theme, element type, `AutomationId`, *Looked up in code* |

*Looked up in code* reads **✓** where this library's code looks the part up — the control's own, or
`DiffBuildController`'s, which the views hand their templates to — ***AvaloniaEdit*** where only code
AvaloniaEdit defines does, a base class's such as `TextEditor`'s or `TextArea`'s own, and **—** where
nothing does. That is the form Brian chose on 2026-10-03. A part nothing looks up serves only its
theme's selectors and bindings and the tests, so a host's own template may rename it or leave it out. A
part something looks up is found by its name, and a template without it loses whatever that code does
with it — which is what someone re-templating a control needs to know.

Rows follow the order each template declares its parts, as the hand tables did, one row per part where
those merged a few. The views table takes the side-by-side template's order, and a part only another
view declares follows the part that precedes it in that view's template. A part the views declare
differently is shown, not failed: its element or id cell names each view's value, and where the views
that declare it differ in whether code looks it up, its *Looked up* cell names the views whose code
does. None of the sixteen differs today. What the reading could not read, if anything, is listed last,
as the theme audit's own parts report lists it.

## Where each column comes from

| Column | Read from |
|---|---|
| The controls, their visibility and base class | `ConcreteControls`' set in `AccessibilityCoverageTests`, resolved to its types — the set the peer walk requires every one of on screen |
| The peer's control type | `ExpectedControlTypes` in the same class, which the peer walk asserts every live peer against |
| A control's own `AutomationId` | The two margins a constructed `DiffPanePresenter` builds; the fixture files both under the pane, so it cannot say which margin carries which |
| The parts, their element types and `AutomationId`s, and what looks each up | `TemplatePartRule.ReadParts(context)`, `BNXQ1003`'s own reading — each control's `PART_` constants, each part looked up on its template with the code's origin, type, method and assembly, and each of its themes' declared parts with the element and the id — over the scan `TemplatePartTests` hands the rule |
| The strip's and the header's parts | Their themes, read from the same scan's parsed markup. The reading has no entry for either: their code declares no part, and nothing looks one up on their templates |

Measured over this repository with `theme-audit parts`, which writes the same reading as the tool's own
report: **11 controls and 59 lookups, nothing unread** — the three views, the find bar, the presenter,
AvaloniaEdit's `TextArea`, whose text area theme is this library's markup, and five AvaloniaEdit controls
whose themes ship with AvaloniaEdit and not here. The strip and the header are not among them, though
their themes declare 14 parts and 6.

## Decisions

| Decision | Why |
|---|---|
| Generated by a test in the test project, in a class of its own, not by a file-based app | The control set, the peer table, the gate's scan and the headless runtime the margins need are all there. A script beside `gen-locale-review.cs` would copy the first three or bring up its own Avalonia platform |
| The parts and what looks each up come from `TemplatePartRule.ReadParts`, never from a reading of our own | Brian's call of 2026-10-03 (`DECISIONS.md`): the rule derives its findings from this reading, so the reference cannot disagree with the gate. The IL reading this draft proposed before — an `ldstr "PART_…"` before a call to `Find` — read less than the rule, which follows `Find*` and `Get*` calls, lambdas, helpers, call chains, constructors and base classes, and its one cross-check compared two different counts |
| The scan is the gate's: `TemplatePartTests` builds its context — the library's markup, with the model, the UI assembly and AvaloniaEdit's — in an internal method, which the gate and the generator both call | Two scans could disagree about what the rule was handed. AvaloniaEdit's assembly is what makes `TextArea`'s `PART_CP` and `TextEditor`'s `PART_ScrollViewer` readable at all |
| The *Looked up* cell is a projection of each lookup's assembly: ✓ when any lookup of the part is in this library's assembly, *AvaloniaEdit* when every one is in AvaloniaEdit's, — when there is none | Brian's form. The tool's own report names the helper's type, `DiffBuildController`, where this table writes ✓: someone re-templating a control needs to know that code finds the part, not which class does |
| The strip's and the header's parts are read from their themes in the scan's parsed markup, and that reading is held to the rule's: for every control the reading covers, the generator's reading of its themes must equal the reading's declared parts — name, element and id, in order | The reading has no entry for a control whose code declares no part and on whose template nothing looks one up. A second reading is tolerated only where it is anchored to the first: this one extends the rule's to two controls, and is proven equal to it wherever both read |
| `ConcreteControls` and `ExpectedControlTypes` go from `private` to `internal` in `AccessibilityCoverageTests`, and the scan's context moves into an internal method in `TemplatePartTests` | Two copies of the peer table, or of the scan, are what the gates exist to stop drifting apart. Both files are gate files of `scripts/mutate-gates.sh`, so the edits owe a full harness run, which phase 1 makes; it is also what shows that the mutations editing the context still apply, since one that stops matching fails the run as a `no-op` and is re-pointed in the same change. The generating test stays out of the gate files: its checks are proven by scratch mutations, as `ReferenceAuditTests`' are |
| A part the views declare differently is shown, not failed | Brian, 2026-10-03. A theme rule is a gate's to enforce; the reference describes the templates as they are |
| Two checks are asserted before the comparison, and a failed check writes nothing: every control of the set, and every entry of the reading whose theme is in this library's markup, is placed in a table; and the declarations agree, as above | A control nobody placed would be missing from the reference without a word, and a reading that fails a check is a wrong reference, whose received copy would invite someone to accept it |
| Compared by `OutputDrift.Check`, XamlQuality's drift comparison: on drift it writes `docs/template-parts.received.md` and the test fails, and a check that finds the reference up to date removes a received copy an earlier one left | One comparison for the tool and a consumer's test, which is what XamlQuality made it for: CRLF reads as LF, and the received copy is where a contributor already looks. CI uploads `docs/*.received.md` beside the theme audit's |
| No write mode: `scripts/gen-template-parts.{sh,ps1}` deletes any received copy, runs the test, and, when it fails having written one, moves it over the committed file and runs the test again | A switch that makes the test rewrite the file and pass is a switch that turns the gate off. A failure that wrote no received copy — a check's — moves nothing, and the deletion first is what makes that true |
| Template order, and nothing culture- or machine-dependent in the text: a theme by its file name, never its path | The order the templates declare their parts is the layout of the hand tables, and as fixed across cultures and platforms as ordinal order, which is kept for what no template orders. The reference must regenerate identically in the `de-DE` leg and on every platform |
| The hosting guide links to the reference by an absolute URL | The guide is both packages' readme and `docs/` is not packed, so a relative link would be dead on nuget.org. Its id table stays: it is grouped by what a host scopes a search to, which the reference does not show, and `HostingGuideTests` holds it to the fixture |

### Dismissed

- **A hand-written table in a document.** It rots, which is the whole question this plan answers.
- **The scratch script, kept for running on demand.** Nobody finds it, and nothing notices when it is wrong.
- **Generating at build time.** Current on one machine, invisible in review, and build work every time.
- **A documentation site.** A generator, a host and a theme for one table set.
- **The theme audit's own parts report as the reference**, `theme-audit parts --check` from a `parts`
  section of `theme-audit.json`. The cheapest path — no generator, one section and a check — and it is
  not the five tables: one section per control, six of its eleven AvaloniaEdit's, no controls table, so
  no visibility, base class or peer, no views side by side, and no entry for the strip or the header.
- **An IL reading of our own**, which this draft proposed before the pin moved. It read less than the
  rule, and a second reading of one fact is what the gates exist to keep from drifting.
- **A column of the public part constants, read as what the code looks up.** The reading keeps a
  control's constants apart from its lookups, because a constant only a selector needs is no lookup.
- **Lookups read from the C# source**, as the hand tables were. It cannot see AvaloniaEdit's, which this
  repository consumes as a binary.
- **A table of the menu entries' ids.** It restates `fixtures/automation-ids.txt` (Brian, 2026-10-03).

## Scope

**In.** The generating test, in a class of its own; `ConcreteControls` and `ExpectedControlTypes` made
`internal`; `TemplatePartTests`' scan moved into an internal method; `docs/template-parts.md`;
`scripts/gen-template-parts.{sh,ps1}`; the hosting guide's absolute link to the reference; a line in
`AGENTS.md` §4 that a template edit regenerates the reference, and so may an AvaloniaEdit or XamlQuality
bump, beside the theme audit's; a short section in the README beside *The theme audit*; `PROGRESS.md`.

**Out.** The rest of the hosting guide. Anything about the controls beyond their templates and
identities — properties, events, commands, which `fixtures/api` and the XML docs carry.
Moving `ReferenceAuditTests`, which writes its received copies itself, onto `OutputDrift`. Asking XamlQuality to give a
themed control whose code declares no part an entry in the reading, which would retire the strip's and
the header's local reading — worth offering, and nothing here waits on it.

## Phases

| Phase | Size | What | Verified by |
|---|---|---|---|
| 1 The reference and its gate | M | The test that reads the control set, the peer table, the margins and `ReadParts`, builds the markdown, asserts its checks and compares with `OutputDrift.Check`; the two members made `internal` and the scan's method; `docs/template-parts.md` generated by it | The reference has the five hand tables' rows and cells, in template order, one row per part; its *Looked up* cells agree with the hand tables' ✓ and — everywhere, and read *AvaloniaEdit* for the text area theme's `PART_CP`; both checks hold. Scratch mutations each make the gate fail with the fresh reference written beside it: a part renamed in a view's theme, an `AutomationId` changed, a header part's element changed, a library lookup removed with its constant kept, a peer type changed in the peer table, a margin's id changed in its constructor, and the committed file edited by hand. Two trip a check instead and write nothing: the generator's own reading of declarations made to skip a part, the anchor, and a control dropped from the tables' placement, the placement check. A full `scripts/mutate-gates.sh` run, owed to the gate-file edits, behaves as declared. The suite passes in `en-US` and `de-DE`, the reference byte-identical in both |
| 2 The wrappers and the record | S | `scripts/gen-template-parts.{sh,ps1}`; the hosting guide's absolute link; `AGENTS.md` §4; the README section; `PROGRESS.md` | Each wrapper, run after a template edit, rewrites the reference and leaves the gate green, and moves nothing when the test fails without writing a received copy, a stale one left beside the reference included; `pwsh -NoProfile -File scripts/gen-template-parts.ps1` as well as the shell one. CI green on all five jobs |

## Risks

| Risk | Assessment |
|---|---|
| Every template edit now regenerates a document | That is the point, and the theme audit already asks the same of the same edits. The failure names the fresh file, and the wrapper writes it |
| A test that writes into the repository | Only on drift, beside the committed file and under a `.received` name the repository already ignores; `ReferenceAuditTests` and the snapshot tests do the same |
| A pin bump moves the reference | An AvaloniaEdit bump that starts or stops looking a part up moves that cell; a XamlQuality bump that reads more, or differently, moves whatever it changes, and a XamlQuality bump re-reads every rule anyway, by item 14's rule. The drift is the notice either way |
| The reading gains the strip and the header | A release that gives a themed control with no code parts an entry moves those two from the local reading to the rule's, and the anchor holds the two readings equal on the way |
| A lookup the rule cannot see | A part name built at run time. The rule cannot hold it to a theme either, and the library has none today; its cell would read — |

## Conventions

Conventional Commits, dense bodies, no AI attribution trailer. Every new test proven able to fail
before it is committed. An approved plan is committed alone before implementation and never edited;
drift goes to `DECISIONS.md`. Work on a branch of its own, cut from the local stack's tip while the
stack is unmerged and from `main` once it is; pushed only when Brian says.
