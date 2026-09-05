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
