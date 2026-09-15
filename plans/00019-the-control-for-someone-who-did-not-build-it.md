# 00019 — The control, for someone who did not build it

> Status: **approved 2026-09-15**. Supersedes nothing.

Asked for directly: a guide for consumers of the control itself, with the demo kept as it is.

Nothing in the repository addresses that reader. `README.md` is a contributor's document — *Layout*,
*Building*, *Releasing*, *The theme audit* — and every command in it assumes the checkout. The demo
is a harness by design, holds its own chrome in English on purpose, and exists to drive the control
by hand under `AGENTS.md` §9. `DECISIONS.md` and `AGENTS.md` are addressed to whoever changes the
library next.

**The gap is not reference material.** The public types carry XML documentation on every member,
because `GenerateDocumentationFile` is on and CS1591 is an error under `-warnaserror`. What is
missing is a *path* through them — `SideBySideDiffView` alone declares 175 public members — and the
handful of facts that are not discoverable from any of them, because they are things you must do
rather than things you may call.

## Goal

**Someone who has never seen this repository can put a working side-by-side diff into their own
Avalonia application from one document**, and knows before they start what it will cost them, what
they must not skip, and where the control stops.

## What this plan was rebased onto

This plan was drafted as 00017 on 2026-09-14 and parked. Three changes landed before it was
renumbered, and each one falsified something it said. They are recorded here rather than silently
corrected, because the same facts are what the guide must now state.

| Then | Now | Where |
|---|---|---|
| Namespace `…DiffView.Avalonia` | **`Bennewitz.Ninja.DiffView`** — the `Avalonia` segment shadowed the framework's own root (CS0234) | plan 00017 |
| "Neither project sets `PackageId`; the id is unsettled and the release plan decides" | **Settled**: `Bennewitz.Ninja.DiffView.Avalonia` and `Bennewitz.Ninja.DiffView.Core`. Assembly names stay unprefixed | `3ae46d8` |
| "NuGet publication is the release plan's question" | The release plan ran and **handed `PackageReadmeFile` to this one**, deliberately unset. The release waits on this plan | plan 00018 |

## Decisions

| Decision | Why, and what was dismissed |
|---|---|
| **The gates land before the guide** | A guide's citations are public API names and its snippets are code a stranger pastes; both fail silently and are paid for by someone who cannot fix them. Dismissed: write the guide and gate it after — which is how `PROGRESS.md` came to record a passing verification for a test plan 00004 had deleted |
| **The guide is the package readme** | `PackageReadmeFile` points at `docs/hosting-diffview.md`. Dismissed: a separate, shorter readme — a second consumer-facing English surface to keep in lockstep, which is the objection plan 00014 recorded and plan 00018 repeated when it left the property unset |
| **Citations resolve against the shipped assemblies, not the source** | Reflection over `DiffView.Avalonia` and `DiffView.Core` — the assembly names did **not** move in the namespace sweep, and the shipped surface is what a consumer actually has. Dismissed: a regex over `src/`, which cannot tell public from internal |
| **The quickstart under test is read from the document** | A snippet retyped into a test is a copy that drifts. Dismissed: a fixture beside the guide |
| **The demo is untouched but for one line** | Asked for directly. It gains a sentence saying it is a harness, not a sample |

## Non-goals

| Excluded | Note |
|---|---|
| **An API reference** | The XML documentation is complete and enforced. A second prose copy is a second surface to keep in lockstep. The guide links to members; it does not restate them |
| **A walkthrough of the demo** | The demo stays exactly as it is and gains one line saying what it is for |
| **A tutorial on diffing** | The reader knows what a diff is. `DiffOptions` and the similarity gate are documented where they live |
| **Running a publish** | The guide *names* the two package ids, which are now settled, and sets `PackageReadmeFile`. It does not create the remote, register a Trusted Publishing policy, or push a tag — those are the release's three preconditions and the push is Brian's |
| **Any change to public API** | If a passage is hard to write because the surface is awkward, that is a **finding for `DECISIONS.md`**, not a licence to refactor while writing prose |
| **Documenting what is not built** | Folder compare, 3-way merge, rule-based unimportant differences and binary diffing are named non-goals of plan 00001. The guide says where the control stops, in one short section |

## Architecture

### The gate is written before the guide

This repository has already been taught this lesson once. `DocumentationCitationTests` exists
because `PROGRESS.md` recorded a *passing* verification for a test plan 00004 had deleted and
nothing noticed for six plans — and its own regex then admitted no `.`, so **155 qualified citations
were invisible** until it was widened.

A hosting guide is a worse case than a progress note, in two ways a reader cannot check for
themselves:

1. **Its citations are public API names.** `LeftSource`, `UnchangedContextRows`,
   `DiffViewResources.ThemeUri` — a renamed member leaves prose that reads perfectly and is wrong.
   Plan 00017 is the proof this is not hypothetical: every namespace in the parked draft was stale
   within a day of writing it.
2. **Its snippets are code a stranger will paste.** A quickstart that does not compile, or XAML that
   does not parse, fails on someone else's machine with no way back to us.

So two gates land first, against a fixture wrong in both ways:

| Gate | Asserts |
|---|---|
| **Every member the guide names exists** | Each `` `Type.Member` `` in the document resolves by reflection against the public surface of `DiffView.Avalonia` and `DiffView.Core`. Not a regex over source — the shipped surface |
| **The quickstart runs** | The guide's XAML is loaded with `AvaloniaRuntimeXamlLoader` in a headless test and the control it produces reaches `DiffViewState.Ready` over two sources. The snippet in the document **is** the snippet under test, read from the document |

The second is the one worth the effort. A quickstart is the only part of a guide that every reader
executes, and the cost of it being subtly wrong is paid entirely by people who cannot fix it.

### What the guide must say, because none of it is discoverable

These are the passages that justify the document existing at all; the rest is navigation.

| | Why a reader cannot work it out |
|---|---|
| **The XAML namespace is `using:Bennewitz.Ninja.DiffView`** | There is **no `XmlnsDefinition`** in the library, so there is no friendly `https://` URI to import — a consumer writes the CLR form or gets nothing. The namespace also does not match either assembly or package name, which is exactly where a reader would guess. New since plan 00017 and the single most likely first-five-minutes failure |
| **The `StyleInclude` is not optional** | `DiffViewResources.ThemeUri` must be merged into `Application.Styles` or the control templates never resolve. A missing include is not an exception — it is a blank control |
| **Semi is the intended base theme, and Fluent or Simple needs the compat dictionary** | `DiffViewResources.FluentCompatUri` / `SimpleCompatUri` exist because AvaloniaEdit's search panel is templated for Fluent and resolves nothing under Semi. A host on the wrong footing gets an unstyled panel and no error |
| **TextMateSharp is unconditional** | `DiffView.Avalonia` references `AvaloniaEdit.TextMate` with no syntax-free variant, so a consumer inherits **6.7 MB of grammars and a native `libonigwrap.so` per RID** whether or not they colour anything. A consumer deserves to read it before they take the dependency |
| **Localization happens whether you ask or not** | Wire nothing and a German machine renders German, because eight locales ship. A host resolver outranks them by construction. This surprises people in both directions |
| **The eight locales are machine-generated and unreviewed** | Stated plainly, because a consumer shipping to those markets is inheriting that and cannot tell from the package |
| **Publishing trimmed keeps only the declared satellites** | `SatelliteResourceLanguages` names the eight; a consumer trimming without knowing this silently gets English |
| **Editing is a flip, not a mode** | `LeftReadOnly` / `RightReadOnly`, and an edit rebuilds through the same worker without replacing the document, so caret, selection, scroll and undo survive |
| **`UnchangedContextRows` is three behaviours in one property** | `null`, `0` and `n` are Beyond Compare's *Show All*, *Show Differences* and *Show Context*; off by default |
| **It is two packages, and neither is named after its assembly** | `Bennewitz.Ninja.DiffView.Avalonia` carries the control and depends on `Bennewitz.Ninja.DiffView.Core`, where `PaneSource` and the model live. The **assemblies** are `DiffView.Avalonia` and `DiffView.Core` — unprefixed on purpose — so the id a consumer installs, the assembly they reference and the namespace they import are three different strings |

### Where it lives

`docs/hosting-diffview.md`, beside `docs/theme-audit.md` and `docs/locale-review/`. `README.md`
gains a link near the top and otherwise stays what it is: the document for someone working *on*
DiffView rather than *with* it. Both packable projects set `PackageReadmeFile` to it, which means it
is also the page nuget.org renders.

## Phases

| Phase | Size | Content |
|---|---|---|
| **1 — The two gates** | S | The API-citation gate and the quickstart-runs gate, both against a fixture document that names a member which does not exist and carries XAML that does not parse. Seen red before there is a guide to make green. Ships alone |
| **2 — The guide** | M | `docs/hosting-diffview.md`: quickstart, theming, the properties that carry the control, the extension points (`DiffKeyMap`, the context-menu events, `DiffViewStrings.Localization`), the costs, where it stops. Written against the live gates, so every citation is checked as it is typed |
| **3 — The package readme** | S | `PackageReadmeFile` on both packable projects, with the `None Include … Pack="true" PackagePath="\"` item that a file outside the project directory needs. Gated: a `dotnet pack` that produces a nupkg whose nuspec names the readme and whose payload contains it |
| **4 — The record** | S | `README.md` links it, the demo gains its one line, `DECISIONS.md` takes anything the writing exposed, `PROGRESS.md` — whose *Open* item 2 closes and item 3 loses one of its three preconditions — `CHANGELOG.md` |

## Testing

Every new test is proven able to fail before it is committed.

| Test | Asserts |
|---|---|
| **Every member the guide names exists** | Reflection over the two public surfaces. Against a fixture naming `SideBySideDiffView.NoSuchProperty`, it must fail and name it |
| A member that exists but is not public is still caught | The reader cannot call it. Internal and private members resolve to *absent* for this purpose |
| **The quickstart's XAML parses** | Loaded from the document, not retyped beside it. A fixture with a malformed element must fail |
| **The quickstart's control reaches `Ready`** | Two sources in, a built document out, headless. Parsing is not working |
| **The quickstart's `xmlns` is the one the library actually has** | The namespace the document tells a reader to import is compared against the `RootNamespace` the control's type reports. This is the citation that plan 00017 would have broken and no reflection over member names would catch |
| **The packed nuspec names a readme that is in the package** | `dotnet pack` for both ids; read the nuspec's `<readme>` and assert the payload carries that path. The failure being gated is `LayeredEditors.Avalonia.Diagnostics`'s, recorded in `DECISIONS.md`: a `PackageReadmeFile` naming a file that does not ship fails the pack |
| The guide cites no test name that does not exist | `DocumentationCitationTests` already does this for five documents; the guide joins the list |
| The document exists and is linked from `README.md` | A guide nobody can find is the failure mode that costs least to prevent |

## Risks

| Risk | Assessment |
|---|---|
| **A guide that drifts is worse than no guide** | The whole reason the gates come first. A consumer cannot tell a stale instruction from a current one, and unlike a contributor they cannot check. Plan 00017 invalidating this plan's own first draft is the evidence |
| **`PackageReadmeFile` outside the project directory** | `docs/hosting-diffview.md` is at the repository root, not under either project. It needs an explicit `None … Pack="true" PackagePath="\"` item beside the property; the property alone packs nothing and `dotnet pack` fails on the missing file. Gated in phase 3 rather than trusted |
| **The guide is the nuget.org landing page as well as a repository document** | Relative links that work in the checkout resolve against nowhere on nuget.org. Either keep links absolute to the project URL, or accept and state that the rendered page has dead links. To be decided while writing, and recorded |
| Reflection misses overloads, generics or explicit interface implementations | Real. The gate matches on member *name* rather than signature, which is the right strictness for prose: the guide names `Save`, not `Save(DiffSide)` |
| The XAML gate needs the headless Avalonia app | Already how the whole Avalonia suite runs; `HeadlessTestApp` is the fixture. It does mean the quickstart is verified against the **test** host's style setup, so the test must merge exactly what the guide tells a reader to merge, and nothing more |
| **Over-documenting** | 175 public members on one control, and a guide that tries to cover them becomes a worse copy of the XML docs. The measure is the Goal: what does a reader need to get to a working diff and not be ambushed afterwards |
| Writing the guide exposes an awkward API | Likely, and explicitly **not** licence to change it mid-plan. It goes to `DECISIONS.md` and, if it deserves one, a later plan |

## Conventions

Conventional Commits, dense bodies, no AI attribution trailer. New tests are proven able to fail
before they are committed. An approved plan is committed before implementation and never edited;
drift goes to `DECISIONS.md`. Work on `docs/the-hosting-guide`, branched from `main` at `2bb5258`;
a completed branch merges without asking once it is proven stable.
