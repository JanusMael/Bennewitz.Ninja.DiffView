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
