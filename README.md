# DiffView

Text diff controls for Avalonia 12 — row-aligned panes on AvaloniaEdit, line and word-level
highlighting, change navigation, minimap, connectors, find across either or both panes, syntax
highlighting, and a unified (inline) view over the same model — built read-only first and
designed so in-pane editing is a flip, not a rewrite. A second, standalone deliverable is `theme-audit`, a dotnet tool that finds the
resource keys an Avalonia theme leaves undefined (the invisible-control cases) and the tokens
below a contrast floor, for any Avalonia project.

Status: **every phase of the plan is complete, the optional inline view included.** The
theme audit report is [docs/theme-audit.md](docs/theme-audit.md). The plan is
[plans/00001-side-by-side-diff-control.md](plans/00001-side-by-side-diff-control.md);
progress is tracked in [PROGRESS.md](PROGRESS.md), decisions in [DECISIONS.md](DECISIONS.md),
and the cross-file contracts an agent must not break in [AGENTS.md](AGENTS.md).

## Layout

| Path | What |
|---|---|
| `src/DiffView.Core` | Diff model: alignment, probing, search, diagnostics. No UI dependency |
| `src/DiffView.Avalonia` | The controls and their themes |
| `src/DiffView.Demo` | Desktop demo — Semi.Avalonia by default, Fluent or Simple by flag |
| `src/ThemeAudit` | The `theme-audit` dotnet tool |
| `tests/` | Unit, headless UI and rendered-snapshot tiers |
| `reference/` | Read-only upstream checkouts, fetched on demand — see [reference/README.md](reference/README.md) |
| `fixtures/` | Test inputs, including the bundled monospace font |

## Building

Requires the .NET SDK named in `global.json` and, for the demo and tests, the sibling package
feed at `../nuget-local`:

```bash
dotnet run scripts/fetch-reference.cs
```

```bash
dotnet run scripts/pack-diagnostics.cs
```

```bash
dotnet build DiffView.slnx -warnaserror
```

```bash
dotnet test --solution DiffView.slnx
```

The first command fetches the upstream sources (or finds a sibling ClaudeForge checkout); the
second builds ClaudeForge's diagnostics library into the feed. `dotnet test` runs the fetch
itself when a reference checkout is missing.

Stopwatch tests (`Category=Perf`) stay out of the default run; their numbers are recorded in
`PROGRESS.md`. To run them too:

```bash
dotnet test --solution DiffView.slnx -p:IncludePerfTests=true
```

CI also runs the whole suite pinned to a culture the library ships. That leg is how a test that
asserts English text without saying it wanted English gets caught, because such a test is invisible
on an English machine and red on a German desk:

```bash
DIFFVIEW_TEST_UI_CULTURE=de-DE dotnet test --solution DiffView.slnx
```

A test that fails only there either wants `[EnglishChrome]` — the claim that it is about the English
words — or wants its assertion repaired. `AGENTS.md` §5 says which, and why the attribute is not an
excuse to reach for first.

The eight shipped locales are machine-generated and have **not** been read by a native speaker, which
gates the first release. `docs/locale-review/` holds one document per locale for exactly that
review — every string beside what it is, what each placeholder holds, and the English it came from.
Those documents are generated and must not be edited; a correction belongs in the matching
`src/DiffView.Avalonia/Localization/Strings.<culture>.resx`, after which:

```bash
./scripts/gen-locale-review.sh
```

regenerates them. `--check` reports staleness instead, and `LocaleReviewTests` fails if a committed
document stops describing the strings it was generated from.

Run the demo under a different theme or variant:

```bash
dotnet run --project src/DiffView.Demo -- --theme fluent --variant dark
```

Open two files in the unified view, which View → Unified (inline) view also switches to:

```bash
dotnet run --project src/DiffView.Demo -- --unified --left one.cs --right two.cs
```

## The theme audit

`theme-audit.json` at the root names the themes (from the reference checkouts), the consumers
and the compat dictionaries. Regenerate the dictionaries, then the report:

```bash
dotnet run --project src/ThemeAudit -- compat
```

```bash
dotnet run --project src/ThemeAudit -- report
```

`--check` on either writes nothing and exits 1 when the committed output differs from a fresh
run; the Reference-trait tests do the same. The report is [docs/theme-audit.md](docs/theme-audit.md):
what each theme defines per variant, what each consumer references, the undefined keys per
(consumer, theme, variant), the contrast matrix for the `DiffView.*` tokens, and the ledger of
the generated `Themes/Compat/*.Semi.axaml` dictionaries. The quick per-directory key count is
still there:

```bash
dotnet run --project src/ThemeAudit -- inventory reference/Semi.Avalonia/src/Semi.Avalonia/Themes/Light
```

Pack the tool into the local feed for another repository to adopt (`dotnet tool install Bennewitz.Ninja.ThemeAudit`):

```bash
scripts/pack-theme-audit.sh
```
