# DiffView

A side-by-side text diff control for Avalonia 12 — row-aligned panes on AvaloniaEdit, line and
word-level highlighting, change navigation, minimap, connectors, find across either or both
panes, syntax highlighting — built read-only first and designed so in-pane editing is a flip,
not a rewrite. A second, standalone deliverable is `theme-audit`, a dotnet tool that finds the
resource keys an Avalonia theme leaves undefined (the invisible-control cases) and the tokens
below a contrast floor, for any Avalonia project.

Status: **Phase 1 — virtual-padding spike: go.** Next is Phase 2, the theme-key audit. The plan is [plans/00001-side-by-side-diff-control.md](plans/00001-side-by-side-diff-control.md);
progress is tracked in [PROGRESS.md](PROGRESS.md) and decisions in [DECISIONS.md](DECISIONS.md).

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

Run the demo under a different theme or variant:

```bash
dotnet run --project src/DiffView.Demo -- --theme fluent --variant dark
```

## The theme audit

```bash
dotnet run --project src/ThemeAudit -- inventory reference/Semi.Avalonia/src/Semi.Avalonia/Themes/Light
```

Later phases add the consumer scan, the findings report, the contrast check and the compat
dictionary generator; the tool packs with `dotnet pack src/ThemeAudit -o ../nuget-local`.
