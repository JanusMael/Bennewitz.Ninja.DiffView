# 00017 — One namespace, and no segment that shadows the framework

Asked for as a move to `Bennewitz.Ninja.Avalonia.DiffView`, and settled on
**`Bennewitz.Ninja.DiffView`** after the collision was measured rather than assumed. Two facts
decided it.

**There is no type named `DiffView`.** The controls are `SideBySideDiffView` and `InlineDiffView`,
and the seven `DiffView*` types are all suffixed. The type-versus-namespace collision the rename was
worried about does not exist today and is not being created: the suffixed names stay.

**The collision that does exist is the segment `Avalonia`, and it is live.** A probe compiled against
the library on 2026-09-14:

```
error CS0234: The type or namespace name 'Media' does not exist in
the namespace 'Bennewitz.Ninja.DiffView.Avalonia'
```

Inside `namespace Bennewitz.Ninja.DiffView.Avalonia`, the identifier `Avalonia` binds to **our own**
namespace before the framework's, so `Avalonia.Media.Color` resolves to
`Bennewitz.Ninja.DiffView.Avalonia.Media.Color` and fails. The tree already carries the scar:
`DiffCommand` writes `<see cref="global::Avalonia.Input.KeyBinding"/>`, and the `global::` is load-
bearing.

Moving `Avalonia` to the second segment would have promoted that shadow from *this library's tree*
to **every `Bennewitz.Ninja.*` sibling**, and would have forced `DiffView.Core` — which is UI-free by
design — either to sit under `…Avalonia.…` and lie about it, or to split into a second tree.

**Nothing has been released**: `CHANGELOG.md` holds only `[Unreleased]` and the only tag is a plan
checkpoint. A namespace change is free now and permanently expensive after the first package.

## Goal

**Every namespace in this repository is free of a segment that shadows a referenced framework root**,
the library's public namespace is `Bennewitz.Ninja.DiffView` with the model beneath it at
`Bennewitz.Ninja.DiffView.Core`, and the `global::` in `DiffCommand` is gone because nothing needs it.

## Non-goals

| Excluded | Note |
|---|---|
| **Renaming assemblies, projects or packages** | `DiffView.Avalonia` stays the assembly and the package. The platform belongs in the package name, where a consumer looks for it; `Directory.Build.props` already says assembly names stay unprefixed, so assembly ≠ namespace is the existing convention, not a new inconsistency |
| **A bare `DiffView` control type** | Decided against. `SideBySideDiffView` and `InlineDiffView` keep their names, which is also what keeps `Bennewitz.Ninja.DiffView` safe as a namespace |
| **Renaming `DiffView.Core`'s namespace** | `Bennewitz.Ninja.DiffView.Core` is already correct under this scheme and becomes a natural child of the library's namespace |
| **Touching `ThemeAudit`** | `Bennewitz.Ninja.ThemeAudit` carries no shadowing segment. It has its own `Avalonia` *class*, deliberately, which is a different thing and stays |
| **Any behaviour change** | Not one line of logic. A namespace sweep that fixes a bug on the way is a sweep nobody can review |
| **Reorganising types into new sub-namespaces** | `.Controls`, `.Rendering`, `.Model` and the rest are a separate argument. This plan changes the prefix and nothing else |

## Architecture

### What moves, and what does not

| From | To |
|---|---|
| `Bennewitz.Ninja.DiffView.Avalonia` | **`Bennewitz.Ninja.DiffView`** |
| `Bennewitz.Ninja.DiffView.Avalonia.Tests[.Composite│.Inline│.Presenter│.Spike]` | **`Bennewitz.Ninja.DiffView.Tests[.…]`** |
| `Bennewitz.Ninja.DiffView.Core`, `.Core.Tests`, `.Demo`, `Bennewitz.Ninja.ThemeAudit*` | unchanged |

The tests move for the same reason the library does, and it is easy to miss: leaving them at
`…DiffView.Avalonia.Tests` would keep `Bennewitz.Ninja.DiffView.Avalonia` in existence as a
*test-only* namespace, and every test file would still shadow `Avalonia`. The point is that the
segment stops existing.

### Six forms the name takes, and only one of them is a C# namespace declaration

A sweep that changes `namespace` lines and stops is the failure mode here. The name also appears as:

| Form | Where | If missed |
|---|---|---|
| `RootNamespace` | `Directory.Build.props` computes `Bennewitz.Ninja.$(MSBuildProjectName)`; the two affected projects need an explicit override | The convention silently reasserts the old value for generated code and embedded resource names |
| **The `ResourceManager` base name** | `DiffViewStrings` constructs it from a **string literal**, and the embedded `.resx` logical name derives from `RootNamespace` | **The dangerous one.** Every locale falls back to English with nothing throwing. Covered by `LocalizationTests.The_bundled_translation_answers_when_the_library_ships_the_culture` |
| `x:Class` and `xmlns:dv="using:…"` | Four themes in the library, plus the demo's window | A XAML compile error, which is loud — the benign kind |
| The XML-documentation member prefix `F:…` | `Summaries` and `scripts/gen-locale-review.cs` | Every summary reads as absent; the locale-review gate reports 143 keys undocumented |
| `theme-audit.json` | Two `"namespace": "using:…"` entries | The compat generator emits a dictionary naming a namespace that does not exist; `docs/theme-audit.md` must be regenerated under its own `--check` |
| Prose in `DECISIONS.md` | One line names the trimmed satellite's logical resource name | History stays as written; see below |

`InternalsVisibleTo` and every `avares://` URI name **assemblies**, not namespaces, and are
untouched — checked, not assumed.

### The guard is what makes it stick

A rename with no guard is a rename that comes back. `StringCatalogueTests` and `LocaleReviewTests`
are the precedent: the repository's habit is that a property worth fixing is worth asserting.

The guard is a test that **no namespace in the shipped assemblies carries a segment matching the
root namespace of a referenced framework** — `Avalonia` being the one that bites, and the only one
today. It is proven able to fail by mutation, by putting a single type back under the old namespace.

And the proof by construction: **`DiffCommand`'s `global::` comes off.** If the shadow is gone the
file compiles without it, and if it is not, the build says so. That is a better test than any
assertion, and it costs a deletion.

### History stays as written

`DECISIONS.md` records that `Bennewitz.Ninja.DiffView.Avalonia.Localization.Strings.resources` was
present in a trimmed publish. That was true when it was written and the file's rule is that history
does not converge. It gains a superseding line where the new name is stated, rather than being
edited to pretend the old verification named something it did not.

## Phases

| Phase | Size | Content |
|---|---|---|
| **1 — The sweep** | M | All six forms, in one change: namespace declarations and `using` directives, the two `RootNamespace` overrides, the XAML, the four hardcoded strings, `theme-audit.json` and its regenerated report. Behaviour untouched. **The suite is the gate** — 625 tests under two cultures, and the one silent failure has a test already |
| **2 — The guard, and the proof** | S | The no-shadowing-segment test, proven able to fail by mutation; `DiffCommand`'s `global::` removed so the compiler carries the other half |
| **3 — The record** | S | `DECISIONS.md` (the argument and the superseding line), `AGENTS.md`, `PROGRESS.md`, `CHANGELOG.md` as a **breaking change**, `README.md` if it names a namespace |

Phase 1 is not separable and is not meant to be: a half-swept tree does not compile.

## Testing

Every new test is proven able to fail before it is committed.

| Test | Asserts |
|---|---|
| **No namespace carries a shadowing segment** | Over the public surface of both shipped assemblies. Mutated by moving one type back under `Bennewitz.Ninja.DiffView.Avalonia`, it must fail and name it |
| **`DiffCommand` compiles without `global::`** | By construction, not by assertion. The shadow's absence is a build fact |
| The bundled translations still resolve | `LocalizationTests.The_bundled_translation_answers_when_the_library_ships_the_culture` — already exists, and is the only guard on a failure that is otherwise silent. Unchanged, and must stay green |
| The committed neutral resx still matches | `LocalizationTests.The_committed_neutral_resx_carries_every_English_default_and_no_others`; the resx path is unchanged but its logical name is not |
| The locale review packet still reads | `LocaleReviewTests.Every_committed_review_document_describes_the_strings_it_claims_to` — it fails wholesale if the `F:` prefix was missed, which is the cheap detector for that mistake |
| The theme audit still matches its report | `theme-audit --check` over the regenerated `docs/theme-audit.md` |
| The whole suite, both cultures | 625 / 0 / 0 under `en-US` and `de-DE`. A sweep that changes a test's meaning is a sweep that went wrong |

## Risks

| Risk | Assessment |
|---|---|
| **A missed form fails silently** | Only one form can: the `ResourceManager` base name, where every locale quietly becomes English. It has a test, and that test existing is why this plan is comfortable being mechanical |
| **A mechanical sweep hits a string it should not** | `Bennewitz.Ninja.DiffView.Core` and `.Demo` are prefixes of nothing that moves, but `Bennewitz.Ninja.DiffView.Avalonia` **is** a prefix of `Bennewitz.Ninja.DiffView.Avalonia.Tests`. Order matters: the longer name is rewritten first, or the tests land in `Bennewitz.Ninja.DiffView.Tests` by accident and by luck rather than by intent |
| **XAML and C# disagree** | `x:Class` must match the code-behind exactly. This failure is a build error, not a runtime one |
| Generated code follows `RootNamespace` | The `AutoGenerated` namespace from the versioning generator moves on its own once the override is set; it is not swept by hand |
| **It is a breaking change** | Every consumer's `using` changes. There are no consumers: no package, no version tag, `[Unreleased]` only. This is the last moment it is free, which is the argument for doing it before the hosting guide rather than after |
| The hosting guide is drafted against the old names | Drafted, not committed — plan 00016's successor was parked for exactly this reason and is renumbered when it lands |

## Conventions

Conventional Commits, dense bodies, no AI attribution trailer. New tests are proven able to fail
before they are committed. An approved plan is committed before implementation and never edited;
drift goes to `DECISIONS.md`. Work on `refactor/the-namespace-sweep`, branched from `main` at
`dcda07d`; a completed branch merges without asking once it is proven stable.
