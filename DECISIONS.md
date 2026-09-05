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

## Theme audit: Avalonia's variant lookup, modelled exactly

Building the inventories over the real checkouts exposed five gaps between a naive model and
what Avalonia resolves; the walker models each, and the runtime tests
(`ThemeResolutionTests`) confirm the static view against `TryGetResource`.

- **Default is a fallback, not a variant.** Avalonia looks a key up in the requested variant,
  then along its `InheritVariant` chain, then in `Default`; Fluent and Simple declare only
  `Default` and `Dark`, so their `Light` *is* `Default`, and a key present in `Default` but
  absent from `Dark` still resolves under `Dark`. `ThemeInventory.ForVariant` applies the chain
  and `Default`'s keys sit under every variant. Lookup order, lowest first: shared base, `Default`,
  inherit chain, own — an inheriting variant sees its parent over `Default`, never the reverse
  (the first cut had that backwards and painted NightSky's page white).
- **A dictionary with no `ThemeDictionaries` has one variant, `Default`;** one that declares
  variants but no `Default` still serves its base keys under any variant, through the last
  fallback.
- **`StyleInclude` is an include.** Fluent and Simple reach their control themes through
  `StyleInclude → Styles.Resources → MergeResourceInclude`; following it took Fluent from 831 to
  1153 keys with nothing unresolved.
- **Linked files and code providers are declared, not guessed.** Simple's
  `/Strings/InvariantResources.xaml` is an MSBuild link into Fluent's folder (`ThemeSource.Links`);
  Fluent's `SystemAccentColors` and `ColorPaletteResourcesCollection` are `ResourceProvider`s in
  C# (`ThemeSource.Providers`, with the accent and its six HSL shades as literals). Both live in
  `theme-audit.json`, so the report names what was assumed.
- **Brush opacity is part of the colour.** Semi's `SemiColorText1..3` are Grey9 at `Opacity`
  0.8/0.62/0.35; the opacity multiplies into the alpha along the alias chain so contrast is
  scored as seen.

## Compat dictionaries: mapped colours, verbatim aliases, no templates, and stand-in variant keys

`theme-audit compat` writes, per target variant, every key the source theme defines that the
target lacks. The reviewed table (`src/ThemeAudit/Mappings/FluentToSemi.json`,
`SimpleToSemi.json`) maps only what has a meaning in the target: the 14 opaque `System*`
colours and the seven accent shades onto Semi's palette tokens, the Simple `Theme*` colours onto
Semi's greys by their Light value. Everything else is Fluent's or Simple's own definition copied
verbatim — the `SystemControl*` brushes and control resources are aliases and follow the mapped
colours; the white-or-black-at-alpha ramps read the same over Semi's surfaces. Templates, styles
and includes are never copied: Fluent's 36 named sub-templates would restyle controls Semi
already themes. Every generated element must close over the target or the dictionary itself;
a colour that would dangle is written out and flagged for review (none was), anything else is
dropped with the reason (two Simple keys reference colours Simple never defines).

**Semi's high-contrast variants define `HighlightColor` themselves,** a name Simple also uses.
An application's `Resources` are consulted before its `Styles`, so a compat entry in the
dictionary's `Dark` would shadow NightSky's own value; the generator restores such a key in a
per-variant dictionary. Avalonia's `ThemeVariant` type converter accepts only `Default`,
`Light` and `Dark` as strings — a custom variant needs `{x:Static}` — and `DiffView.Avalonia`
does not reference Semi. `ThemeVariant` equality is by key, and Semi declares each variant as
`new ThemeVariant("Aquatic", ThemeVariant.Dark)`, so `SemiThemeVariants` in `DiffView.Avalonia`
declares the same four keys and the generated dictionaries address them through it
(`variantKeys` on the compat entry); the runtime test proves `SemiTheme.NightSky` finds them.
A project that references Semi (ClaudeForge) omits `variantKeys` and gets Semi's own keys.

The dictionaries are opt-in: `DiffViewResources.FluentCompatUri` / `SimpleCompatUri`, merged
after the theme by a host that runs Semi and also hosts Fluent- or Simple-templated controls.
`Themes/DiffView.axaml` does not include them; DiffView's own controls need no host key.

## DiffView tokens: ClaudeForge's palette, one step darker where a floor demanded it

`Themes/DiffView.Tokens.axaml` defines every `DiffView.*` brush for `Default` (Light) and `Dark`;
no Semi-variant override was needed — the pairs in `contrast-pairs.json` pass under all ten
targets, including the high-contrast pages (`SemiColorWindow`: Aquatic `#202020`, Desert
`#FFFAEF`, Dusk `#2D3236`, NightSky `#000000`). Two deviations from the plan's "ClaudeForge's
values": the modified marker is `#D96A00` rather than the pill's Orange 700 `#F57C00`, which is
2.8:1 on white, and Orange 800 (`#EF6C00`) still fell short on the gutter (2.80) and on Desert's
warm page (2.96) against the 3.0 floor for a change marker; and Dark uses lighter siblings of the
same hues (`#66BB6A`, `#EF5350`, `#FFA726`) because `#C62828` is 2.3:1 on the dark pane. The
status family carries ClaudeForge's measured status-bar values verbatim in both variants. Row
tints are translucent and scored composited over the pane background. The colour-blind sibling
uses Okabe–Ito blue / vermillion / reddish purple, the purple one step darker (`#B5588F`) than
`#CC79A7` for the same gutter floor. Pane background is `#FFFFFF` / `#1E1E1E`, independent of the
host page.

## Plan drift: the AvaloniaEdit gaps are six plus nine, and the hosts have gaps of their own

The plan's resolution test expected "exactly the six missing keys" under Semi without the
compat dictionary. The audit counts six for AvaloniaEdit's Fluent theme file
(`ContentControlThemeFontFamily`, `ControlContentThemeFontSize`, `SystemAccentColor`,
`SystemBaseLowColor`, `SystemChromeMediumColor`, `ToolTipBorderThemeThickness`) and nine for its
Simple theme file, three of the six and six of the nine static — they throw when the style
loads, which is why AvaloniaEdit's own theme cannot simply be added under Semi. The tests assert
those sets; with the compat dictionaries both are empty under all six variants.

Findings the report carries about others, left where they are: Fluent 12.1.2's control templates
reference `ScrollBarButtonBackgroundDisabled` and `ToggleSwitchFillOffDisabled`, which Fluent
never defines (the compat dictionary cannot invent them); ClaudeForge's views still reference
seven `SystemControl*` keys undefined under Semi, six of which the compat dictionary supplies
and one of which — `SystemAccentColorBrush` — no theme defines, reported to the ClaudeForge
session per the working rule rather than edited here.

## The Reference trait runs by default, and drift leaves a `.received` file

`ReferenceAuditTests` and `ThemeResolutionTests` carry `Category=Reference` for filtering but are
not excluded: the checkouts self-heal through each test project's `EnsureReferenceSources`
target, and CI fetches them first. When the committed report or a compat dictionary differs from
a fresh run, the test writes the fresh output beside it as `*.received.md` / `*.received.axaml`
(gitignored by the existing `*.received.*` rule) so the difference can be read, and names the two
commands that regenerate.

## theme-audit packs as 1.1.0, stamped by its own script

`scripts/pack-theme-audit.{cs,sh,ps1}` mirrors the diagnostics packer: the project sets no
package version (the AutoVersioning generator stamps assemblies, not packages), so the script
stamps `PackageVersion` — 1.1.0 now that the tool has `report` and `compat`, superseding the
1.0.0 that carried only `inventory`. The reviewed mappings ship inside the package
(`Mappings/*.json` beside the executable), so a configuration names them bare
(`"mapping": "FluentToSemi"`) or points at its own file.

## Core model: an empty side is one empty line, as the editor sees it

DiffPlex treats the empty string as zero lines; an editor's empty document has one line, and
invariant 1 (every line of each side in exactly one row) needs the model to agree with the
editor, not the engine. The builder therefore never hands DiffPlex an empty side: two empty
sides are one unchanged row; an empty side against <em>n</em> lines is one block of one deleted
line against <em>n</em> inserted lines, so the empty line pairs with the first line of the other
side as a modified row and the rest are inserted (or deleted) rows with padding opposite. The
plan's "empty left → all Inserted" holds for every right line but the first, and the tests say
so. `LineSplitter` is the model's line splitter — the same three terminators as DiffPlex's
`LineChunker` and AvaloniaEdit's `NewLineFinder`, which a test checks against `LineChunker` on
mixed text — and `TextProbe.LineCount` is terminators plus one. `LineEnding.None` was added for
a text with no terminator at all; the plan's four values had no name for a single line.

## Similarity gate: Dice over line multisets, gated by size, forceable

`SimilarityGate.Measure` is the Dice coefficient of the two sides' line multisets after the
options' normalisation (trim for `IgnoreWhitespace`, ordinal-ignore-case for `IgnoreCase`), one
pass and one dictionary — linear where Myers on unrelated inputs is not. Defaults:
`AlignmentSimilarityFloor` 0.1, `AlignmentSizeThreshold` 10,000 combined lines (below it the
gate never fires; Myers on small inputs is fast whatever the inputs), and `ForceAlignment` for
the banner's "Force". An unaligned document has one block covering every row with both full
line ranges, so invariant 4 holds there too.

## Large fixtures are generated from a seed, not committed

The plan lists 10k-line, 200k-line, unrelated and 1 MB single-line fixtures under `fixtures/`.
A 200k-line pair is megabytes of text that would sit in every clone; `Fixtures` in
`tests/DiffView.Core.Tests` generates each from a fixed seed with `System.Random`, whose
sequence is stable for a given seed within a .NET major version, so a measurement names its
seed and line count and reproduces. Only the small pair (`fixtures/small`, a class with a using
and a field added, a signature changed, a method removed) is committed; the demo can open it.
The mixed-line-ending and binary inputs are built in code where the terminators and NUL bytes
are explicit. Phase 10 reuses the generator.

## Core tests suppress xUnit1051

xunit's analyzer xUnit1051 asks that `TestContext.Current.CancellationToken` be passed to
every call that accepts a token. Core's build and search take an optional token by design and
the tests exercise the default deliberately, with two tests passing a cancelled token of their
own; the rule is disabled for `tests/DiffView.Core.Tests` in its project file with the reason.
The other analyzer rules stayed on and caught two real slips (`Assert.Single`, `Assert.Contains`).

## Search: non-backtracking first, timeout second, errors as results

`DiffSearch` compiles a regular expression with `RegexOptions.NonBacktracking` and falls back to
the backtracking engine with `FindOptions.MatchTimeout` (default one second) only when the
pattern uses a construct the linear engine refuses (backreferences, lookarounds). An invalid
pattern and a timeout come back as `FindResult.Error`; cancellation throws; an empty query is
`FindResult.Empty`. Whole-word literal search checks the neighbouring characters; whole-word
regex wraps the pattern in `\b(?:…)\b`, which both engines support. A pane text shorter than
the document (an editor snapshot a keystroke behind the rebuild) is tolerated, never thrown on.
`DiffOptions.Default` is declared after `DefaultWordSeparators` because static initialisers run
in textual order — the first cut had them reversed and the default separators were null.
