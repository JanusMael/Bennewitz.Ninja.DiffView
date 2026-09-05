# Decisions

Decisions made during implementation, and drift from
[plan 00001](plans/00001-side-by-side-diff-control.md). The plan itself is not edited after
approval; this file is where the record lives. Newest at the bottom.

## AvaloniaEdit: upstream package, no fork

`Avalonia.AvaloniaEdit` 12.0.0 from NuGet. SourceGit's fork (`love-linger/AvaloniaEdit`,
branch `patch-on-12.x`) is one commit on upstream master, 9 files, +45/−46: a
`VisualLinesValid` guard in `BackgroundGeometryBuilder` (replicated in our own renderers), lone
`\r` no longer a line terminator (upstream behaviour kept; DiffPlex's `LineChunker` agrees with
upstream), scroll slack, package bumps and cosmetics. Fallback if a post-12.0.0 master fix is
needed: a submodule of upstream at a pinned commit.

## Local feed for packages from the author's other repositories

`NuGet.config` lists `../nuget-local` — `/home/janus/c/nuget-local` on this machine — with
package-source mapping so `LayeredEditors.*` and `Bennewitz.Ninja.ThemeAudit` resolve only
from it. `LayeredEditors.Avalonia.Diagnostics` was packed from the ClaudeForge checkout at
`befedb0` as version **1.0.0** (its project sets no package version; the AutoVersioning
generator stamps assemblies, not packages). The project also sets `PackageReadmeFile` without
shipping a README, so `scripts/pack-diagnostics.cs` overrides that property to empty — recorded
as an upstream fix to contribute. `Bennewitz.Ninja.ThemeAudit` packs as 1.0.0 for the same
reason; package versioning for both is a later decision.

## Reference fetching is a .NET file-based app, wrapped

The plan names `scripts/fetch-reference.sh` (bash 3.2) and `.ps1` (pwsh 7). Both exist, as thin
wrappers around `scripts/fetch-reference.cs`, a .NET 10 file-based app that parses the JSON
manifest with `System.Text.Json` and drives git through `Process`. Reason: the manifest is JSON,
`jq` is not on every machine, and the working conventions forbid hand-rolled JSON parsing in a
shell. The SDK is the one tool every machine here already has. The `EnsureReferenceSources`
target hooks `Build` of the Avalonia test project rather than a test target: under
Microsoft.Testing.Platform there is no `VSTest` target to hook, and the script is a fast no-op
when every checkout is present.

## Reference pins

| Source | Pin | Why |
|---|---|---|
| Semi.Avalonia | `v12.1.0.1` | The consumed package is 12.1.0.1 |
| Avalonia | `12.1.2` | The consumed package is 12.1.2; sparse to the Fluent and Simple theme folders |
| AvaloniaEdit | `12.0.0` | The consumed package is 12.0.0 |
| DiffPlex | `f500e73f28e28813f942f1f16dec5a34b40e48e3` | DiffPlex tags no 1.9.0 release; this master commit carries `<Version>1.9</Version>` and the 1.9.0 `Differ` |
| ClaudeForge | `befedb047b6425494de16772b261217a426ecd4d`; local sibling `../cl/ClaudeForge` preferred | The checkout the reuse analysis and the diagnostics package were taken from |

## Documentation is mandatory on public API

Library projects generate XML documentation and CS1591 stays an error under warnings-as-errors,
so every public type and member is documented when it is written. Test projects and the demo
are exempt by not generating documentation files.

## The accessibility guard also covers menu items

ClaudeForge's guard lists buttons, inputs, pickers and lists. Menus are the demo's main
interactive surface, so `MenuItem` is in this repository's interactive set, and the baseline
starts at zero for every file.

## Test stack: xunit v3 3.2.2 on Microsoft.Testing.Platform 1.9.1, `dotnet test` in MTP mode

`Avalonia.Headless.XUnit` 12.1.2 is compiled against `xunit.v3.extensibility.core` 3.2.2. On
xunit v3 4.0.0 its test-framework discoverer never hooks in, every `[AvaloniaFact]` runs as a
plain test and `Application.Current` is null. `xunit.v3` is therefore pinned to 3.2.2, whose
core rides Microsoft.Testing.Platform 1.9.1; `Microsoft.Testing.Extensions.TrxReport` is pinned
to 1.9.1 for the same reason (2.4.0 pulled the platform to 2.4.0 and xunit's bundled MSBuild
extension failed to load `IDataConsumer`). The .NET 10 SDK no longer runs xunit v3 through the
VSTest bridge, so `global.json` carries `"test": { "runner": "Microsoft.Testing.Platform" }`,
`Microsoft.NET.Test.Sdk` and `xunit.runner.visualstudio` are absent, and the command is
`dotnet test --solution DiffView.slnx` (`--report-trx` for CI). Verify's xunit v3 package is
`Verify.XunitV3`. Serial execution of the Avalonia tests comes from `xunit.runner.json`
(`parallelizeTestCollections: false`) — the assembly attribute for it is obsolete in newer xunit.

## Rendered snapshots carry no machine-specific text

The first smoke snapshot printed the log directory in the demo's status bar, which differs per
user and OS. Rendered text in anything the snapshot tests capture must be machine-independent;
the demo shows the logs path as a tooltip, in the Debug menu and in the log itself instead.

## Virtual padding: go

The Phase 1 spike (`tests/DiffView.Avalonia.Tests/Spike`, `VirtualPaddingSpikeTests`) passed all
six items of the plan against two plain `TextEditor`s — AvaloniaEdit 12.0.0 on Avalonia 12.1.2,
headless with Skia — so the padding design stands: the source document is never touched, padding
is a zero-width `DrawableTextRun`, and heights are primed. No projection layer, no plan 00002.
What the spike established, each a fact the Phase 4 presenter is built on:

**Line metrics.** For a `DrawableTextRun`, Avalonia's `TextLineImpl.CreateLineMetrics` takes
`ascent = min(ascent, −run.Baseline)` and `descent = max(descent, run.Size.Height − run.Baseline)`,
then `height = descent − ascent + lineGap`. AvaloniaEdit's `LineHeightFactor` (default 1.16) makes
a plain row taller than its text, and the text is centred with
`halfSlack = (DefaultLineHeight − naturalTextHeight) / 2` above and below. A spacer with
`Baseline = ascent + halfSlack + above · lineHeight` and
`Size.Height = Baseline + descent + halfSlack + below · lineHeight` therefore makes the visual line
exactly `(1 + above + below) · lineHeight` tall with its text centred in its own row — measured
exact to 10⁻⁶ px for padding above a line and trailing padding after the last line. Ascent,
descent and line gap come from `new TextMetrics(typeface.GlyphTypeface, fontSize)`, the numbers
the formatter itself starts from. The run's `Properties` must be non-null: Avalonia switches on
its `BaselineAlignment` and throws on a null run properties object.

**One element per padded line, at the line start.** Visual length 1, document length 0, emitted at
the line's offset whichever direction the padding goes; direction lives in the run's metrics. The
element returns no caret stop of its own and reports `HandlesLineBorders = true`, so the visual
line adds no implicit stop at column 0 and the text element supplies column 1 for the line's first
offset; on an empty line the element is alone and supplies that single stop itself. Every arrow
key, `End`, `Home` and click then moves one caret position per press, and a click at x = 0 lands on
column 1 because Avalonia's hit test gives a zero-width run a trailing length of 1.

**Known wart, owned by Phase 4.** `Home` pressed twice — AvalonEdit's toggle to "column 0, before
the indentation" — puts the caret on the padding column: same offset, same x, and the next `Right`
is a no-op. The presenter normalises the caret's visual column on `Caret.PositionChanged` to
`VisualLine.GetVisualColumn(offset)`.

**Priming.** `TextView.GetOrConstructVisualLine` writes the line's height into the height tree,
but the scroll extent is republished only by a measure pass, so priming ends with
`InvalidateMeasure`. A padded line's height-tree position is the top of its padding block; the row
it shares with the other side is `position + above · lineHeight`. Heights survive `Redraw`; a
`Document` swap recreates the tree, and a `FontSize` change rebases only lines still at the old
default height (padded lines keep stale heights) — both re-prime, as the plan says.

**Priming cost.** 10,000 gaps: 10.3–10.5 s in one pass, 200–240 ms in batches of 256 with
`Redraw()` between batches (two runs each, Debug, this machine). `GetOrConstructVisualLine` keeps every built line in the text view's list, and both the
`GetVisualLine` lookup and the `VisualTop` refresh walk that list — quadratic in the batch.
`PaddingHeightPrimer` batches; the batch size is a named constant carrying this measurement.

**Selection and caret.** `TextArea.SelectionBrush` transparent, `SelectionBorder` null,
`Caret.CaretBrush` transparent; renderers in `KnownLayer.Selection` and `KnownLayer.Caret` draw
from `VisualYPosition.TextTop` / `TextBottom`, which span the natural text height inside the row.
Pixel-asserted: neither paints a padding row.

**Editor settings the presenter forces.** `AllowScrollBelowDocument` defaults to true in
AvaloniaEdit 12 (the WPF original defaults to false); the presenter sets it false on both panes so
`ExtentHeight` equals the height tree's total. Scrollbar visibility `Hidden` keeps scrolling
enabled without a bar; `Disabled` switches the text view to word wrap.

**Theme include.** Only `avares://AvaloniaEdit/Themes/Base.xaml`, `Themes/Fluent/AvaloniaEdit.xaml`
and `Themes/Simple/AvaloniaEdit.xaml` are addressable; the per-control files are merged at compile
time. `Base.xaml` carries the `TextEditor` and `TextArea` control themes without the Fluent or
Simple static-resource keys Semi lacks, so the spike merges it into its window. Phase 4's presenter
style replaces it, bound to `DiffView.*` tokens only.

## Stopwatch tests are excluded by an MSBuild property

Tests that measure carry `[Trait("Category", "Perf")]`. `tests/Directory.Build.props` sets
`TestingPlatformCommandLineArguments` to `--filter-not-trait Category=Perf` unless
`IncludePerfTests` is `true`, and `dotnet test` in Microsoft.Testing.Platform mode honours the
property, so the default run and CI skip them and
`dotnet test --solution DiffView.slnx -p:IncludePerfTests=true` runs them. Their numbers are read
from the test output (the `.trx` report carries it) and recorded in `PROGRESS.md`.

## Diagnostics package 1.0.1: the pack script stamps the version, the pin follows the fix

Phase 0's manual check found that F12 inside the live-log window did not close it: the
toggle lived on the host's main-window key handler, which never sees a key pressed while the
log window has focus. The fix belongs to the package, so it went to ClaudeForge first per the
plan's *Contributing back* protocol — branch `fix/diagnostics-live-log-f12-and-readme`,
pull request https://github.com/JanusMael/ClaudeForge/pull/37 — together with the README the project's `PackageReadmeFile` had been
naming without shipping. `scripts/pack-diagnostics.cs` no longer clears that property; it now
stamps `PackageVersion` (**1.0.1**) because the upstream project sets none, and the ClaudeForge
pin in `reference/sources.json` moved to the branch head `f7980f2` so CI packs the same
source the local sibling checkout is on. The three move together: the pin, the constant in the
pack script, and the version in `Directory.Packages.props`. When the pull request merges, the
pin moves to the merge commit in a routine bump.
