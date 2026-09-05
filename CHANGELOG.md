# Changelog

All notable changes to DiffView are recorded here. The format follows
[Keep a Changelog](https://keepachangelog.com/en/1.1.0/); versions follow
[Semantic Versioning](https://semver.org/).

## [Unreleased]

### Added

- Plan 00001 — the side-by-side diff control on Avalonia 12, AvaloniaEdit and DiffPlex — with
  its adversarial review and the ClaudeForge reuse analysis.
- Repository scaffolding: solution, central package management, the local feed, reference
  sources with on-demand fetching, CI workflow, and the `theme-audit` tool's first command
  (`inventory`).
- Phase 1, the virtual-padding spike: six headless tests under `tests/DiffView.Avalonia.Tests/Spike`
  proving zero-width `DrawableTextRun` padding, caret movement across it, height priming,
  text-band selection and caret drawing, 1:1 scroll sync, and the priming cost; the go and the
  facts the presenter is built on are in `DECISIONS.md`.
- `PixelProbe`, for pixel assertions on captured frames.
- Stopwatch tests carry `Category=Perf` and stay out of the default `dotnet test` run;
  `-p:IncludePerfTests=true` includes them.
- Phase 2, the theme-key audit: `theme-audit report` and `theme-audit compat` over a JSON
  configuration (`theme-audit.json`), inventorying Fluent, Simple and Semi as Avalonia resolves
  them — Default fallback, inheritance, `StyleInclude`, linked files, code providers, brush
  opacity — scanning consumers for undefined keys, scoring contrast pairs against each variant's
  own surfaces, and writing `docs/theme-audit.md`; `--check` is the drift test.
- The reviewed Fluent→Semi and Simple→Semi mappings and the generated
  `Themes/Compat/FluentKeys.Semi.axaml` / `SimpleKeys.Semi.axaml`, which let a control templated
  for Fluent or Simple (AvaloniaEdit's search panel) resolve every key under all six Semi variants.
- `Themes/DiffView.Tokens.axaml`, the `DiffView.*` palette for Light and Dark, its colour-blind
  sibling, the contrast contract in `contrast-pairs.json`, `SemiThemeVariants`, and the resource
  URIs on `DiffViewResources`.
- Reference-trait tests: the committed report and dictionaries equal a fresh run; every token
  resolves under all ten targets; the AvaloniaEdit gaps are exactly the known ones; a
  `TextEditor` with its search panel renders under each Semi variant with the compat dictionary.
- `scripts/pack-theme-audit.*`, packing the tool as `Bennewitz.Ninja.ThemeAudit` 1.1.0.
- Phase 3, the core model: `PaneSource` (bytes decoded by BOM, strict UTF-8, then Latin-1;
  binary detected on the bytes), `TextProbe`, `LineSplitter`, `DiffOptions`, the similarity
  gate, `DiffDocumentBuilder` producing a source-indexed `SideBySideDocument` (per-side lines,
  aligned rows, change blocks with per-side line ranges, derived padding) with diagnostics and
  warnings, `WordDiffCache` for lazily computed LRU-cached word-level pieces, and `DiffSearch`
  over `IPaneText` with scope, whole-word, regex (non-backtracking first), a match cap and a
  timeout. Tests for the seven invariants, every failure path, the cache and the search;
  `Perf` measurements for the 10k and 200k pairs and the gate.
- `fixtures/small`, the committed small pair; larger pairs are generated from a seed in the tests.
