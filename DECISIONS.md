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

## ClaudeForge pin at the merge of #37 and #38; diagnostics stays 1.0.1

Both pull requests merged into ClaudeForge's `main` (`99c2963` for #37, `93065ba` for #38), so
the pin in `reference/sources.json` moved from the branch head `f7980f2` to `93065ba`, the tip
that carries both. `git diff f7980f2 93065ba -- src/LayeredEditors.Avalonia.Diagnostics` is
empty: the merged diagnostics source is byte-for-byte what 1.0.1 was packed from, so the package
in the local feed and the version in `Directory.Packages.props` stay at 1.0.1 and the pack script
keeps its constant — a repack would produce the same content under a new timestamp. The audit
report was regenerated because the sibling checkout now carries ClaudeForge's own copies of the
compat dictionaries, which the consumer scan counts as keys it defines itself.

## The presenter's control themes travel with the control

`DiffPanePresenter` merges `Themes/DiffPanePresenter.axaml`, compiled as the
`DiffPanePresenterTheme` dictionary class — its own control theme and the `TextArea` theme it
applies by key — into its own `Resources` in its constructor. The first draft merged a runtime
`ResourceInclude` by URI, and the trim-check refused it with IL2026: the include's loader
resolves the resource by reflection. An `x:Class` dictionary is resolved by the XAML compiler at
build time and trims clean, so the fix is the class, not a suppression. The host's
`DiffView.axaml` include supplies the `DiffView.*` tokens those themes bind to, and
`DiffBrushes` carries a hard fallback per token. A host that forgets the include therefore gets
a structurally complete pane in the fallback palette rather than the invisible control the
theme audit exists to prevent; AvaloniaEdit's `Base.xaml` is not needed under Semi, Fluent or
Simple, because neither template references a host theme key. The cost: a host restyles the
presenter by setting `Theme` on the instance or deriving from the shipped theme
(`DiffViewResources.PresenterThemeUri`), not by an application-level `{x:Type}` theme, since
the control's own resources are consulted first. The two templates are AvaloniaEdit's
`TextEditor.xaml` and `TextArea.xaml` with the watermark and the theme-keyed selection brush
removed, attributed in `THIRD-PARTY-NOTICES.md`.

## The panes' font is a token, and the tests override it

The plan's monospace stack is the value of `DiffView.MonospaceFontFamily`, defined in
`DiffView.axaml` beside the merged palette rather than inside the two token dictionaries, so
the test that holds both palettes to the same key set still holds. `HeadlessTestApp` overrides
the key with the bundled DejaVu Sans Mono at application level, where a resource beats the
include's, and that is what keeps the presenter and demo snapshots machine-independent. A host
picks its editor font the same way.

## Priming runs on Loaded first, then on LayoutUpdated

The presenter primes at once when it is loaded and its text view is measured, and otherwise
leaves the prime pending. The first draft waited for `LayoutUpdated` only, and its tests
passed — because every padded line of the small fixture was inside a 320 px viewport, where the
measure pass builds the line with its padding anyway. `Loaded` is dispatched after the first
layout pass and after that pass's `LayoutUpdated`, so a prime requested before the control was
in the tree never ran; a test with a 200 px viewport, which keeps one padded line below it,
failed one row short and is now the regression guard. `OnLoaded` runs the pending prime;
`OnLayoutUpdated` runs a pending prime that arrived while the tree was already loaded but
unmeasured, and re-primes when the default line height moved (a font change rebases only the
lines still at the old default height, as the spike found). A re-prime covers the union of the
old and new padded sets, because a line that lost its padding keeps its stale height until it
is rebuilt.

## Two AvaloniaEdit facts the presenter's constructor had to learn

`TextEditor.OnIsReadOnlyChanged` applies the flag to the text area only when the property
changes, so overriding the default value would leave the text area writable; the presenter sets
`IsReadOnly = true` as a local value in its constructor instead. `TextEditor.OnApplyTemplate`
installs the search panel, so it is null in the constructor — the first draft dereferenced it
there — and `SearchPanel.Uninstall()` lives in the presenter's own `OnApplyTemplate`, after
the base call, where it runs on every template application.

## Metadata for a document that is not the model's

The plan asks that "with metadata for a shorter document, every line renders as `Unchanged` and
nothing throws". `PaneMetadata` implements the per-line reading: a line the model knows keeps
its kind and padding, a line beyond the model's count is `Unchanged` with no padding, and the
trailing padding belongs to the document's last line only while the document and the model
agree on the line count — otherwise it would sit on the wrong line. Between a keystroke and
the next build this is what the editing plan needs: known lines stay tinted, nothing flashes,
nothing throws. The test covers both a longer and a shorter document than the model's.

## Where SourceGit's approach did not carry over

The plan characterises SourceGit as padding with real lines in a padded document built from a
git hunk; nothing of that pipeline ports, because the presenter renders padding over the source
document. What carried over is the layering: a background renderer per row kind on
`KnownLayer.Background`, own selection and caret renderers, custom margins on
`TextArea.LeftMargins`. Avalonia 12 specifics met on the way: `ImmutablePen` takes an
`IImmutableBrush`, so the hatch pen is a `Pen` over the token brush; `GetVisualRoot` is not on
this API, and `IsLoaded` plus the text view's `IsMeasureValid` is the right question anyway;
margin text goes through `FormattedText` directly, since AvaloniaEdit's `TextFormatterFactory`
is internal.

## The caret blinks on the presenter's timer; tests turn it off

`DiffCaretRenderer` blinks at AvaloniaEdit's 500 ms cadence on a `DispatcherTimer` the
presenter owns, restarted on every caret move, stopped on focus loss and on detach.
`IsCaretBlinkEnabled` turns blinking off; `PresenterHost` sets it off so a pixel assertion
never races the timer. AvaloniaEdit's own caret layer keeps running with a transparent brush.

## Gutter tooltips wait for Phase 7

The Phase 4 type table mentions a tooltip on the line-number margin and on the change markers;
Phase 7's own list owns "Tooltips on line numbers and markers", and their text belongs behind
`DiffViewStrings`, which Phase 5 wires through every user-visible string. The margins ship
without tooltips now, with automation names (`DiffViewStrings.LineNumbersMarginName`,
`ChangeMarkersMarginName`) so a screen reader can name the gutters; the accessibility guard
counts `DiffPanePresenter` and `TextEditor` as interactive from this phase on.

## The Phase 1 spike stays in the test project

The presenter's padding types are the spike's, lifted into `src/DiffView.Avalonia/Padding`.
The spike itself — `tests/DiffView.Avalonia.Tests/Spike` — stays: it proves the mechanism
against two plain `TextEditor`s with no presenter in the way, which is the regression canary
wanted when AvaloniaEdit or Avalonia is bumped, and it is the record Phase 1's verification
table cites.

## The composite takes a logger factory, not a logger

The plan's public surface names `Logger (ILogger?)`. The library logs under four categories —
`DiffViewLogCategories.Build`, `Render`, `Find`, `Theme` — and one `ILogger` carries one
category, so `SideBySideDiffView.LoggerFactory` (`ILoggerFactory?`) creates the category loggers
and hands the render one to the panes, whose own `Logger` stays as it was in Phase 4. Every log
line is formatted in `DiffViewLog`, the one place the never-log-document-text rule is enforced:
lines carry counts, codes, line numbers, lengths, paths and timings. The sentinel test runs the
build, the render and a forced fault; the find leg of the plan's test waits for Phase 8, where
find arrives.

## The status controller runs on `TimeProvider`, and only its typed helpers emit

`StatusController` is ClaudeForge's lifecycle — success clears after six seconds, warning after
ten, failure sticks until dismissed, active and state stick until replaced, a new message
cancels the pending clear — rewritten as a plain class with a `Changed` event rather than an
MVVM-toolkit observable, and moved from `Task.Delay` with a test override onto
`TimeProvider.CreateTimer`, so a hand-advanced clock in the tests fires the clear
deterministically. `Set` is private; `SetActive`, `SetSuccess`, `SetWarning`, `SetFailure` and
`SetState` are the only way to emit, which is what keeps a failure from rendering as quiet
text. Timer callbacks are marshalled to the UI thread. The composite creates its controller on
first use so a `TimeProvider` set right after construction is the one it runs on.

## Dark status foregrounds brightened one step for 7:1 on the host page

The plan holds the status pills to 4.5:1 on their fill and 7:1 on the page. ClaudeForge's Dark
foregrounds cleared the fills but sat between 5.7:1 and 6.9:1 on the two lightest dark pages
the audit knows — Semi Dusk's `#2D3236` and Simple's `#282828` — so Dark now carries
`#6ADB88`, `#F7B85A`, `#FFAEAE` and `#8FC8F7` for success, warning, failure and active, each
still above 4.5:1 on its fill and above 7:1 on every page under all ten targets; the Light
values are ClaudeForge's verbatim. The four page pairs live in `contrast-pairs.json` at floor
7.0, so the audit keeps holding them.

## A rebuild keeps the model on an option change and drops it on a source change

Changing an option rebuilds over the same documents, so the previous model stays on screen,
marked stale in the strip, until the new one lands — the plan's "previous result stays on
screen". Assigning a source replaces that side's `TextDocument`, and the previous model
described the previous text, so it is cleared at once: the panes show plain text until the
build lands rather than the old kinds over the new lines. A rebuild never replaces a
`TextDocument`; the test holds caret, selection, scroll offset and the undo stack across one.

## Latest wins by generation; cancellation is honoured between stages

Each build increments a generation and cancels the previous token; the builder runs on the
thread pool over text captured on the UI thread, and the outcome is applied on the UI thread
only when its generation is still the latest — an older build that lands late is discarded
with a `Debug` line. Cancellation and supersession are `Debug`; the Myers run itself is not
interruptible, which is why the similarity gate runs first. Progress shows after
`SlowBuildThreshold` (100 ms) on a `TimeProvider` timer, so a fast build never flashes it.

## A render fault is raised after the render pass that caught it

A decorator's throw is caught inside `TextView.Render`, and a listener that changes a
pseudo-class or a property there trips Avalonia's "visual was invalidated during the render
pass". `DiffPanePresenter.ReportFault` records and logs the fault at once and posts the
`RenderFault` event to the dispatcher, so the composite's state change and the strip's failure
pill land after the pass. A test reading the event runs the dispatcher once after the frame.

## Horizontal scrollbars are equalised by need; the left vertical bar is hidden

`Auto` would show a horizontal bar in one pane only, and a bar takes height from its viewport,
so the composite sets both panes to `Visible` when either extent overflows its viewport and to
`Hidden` otherwise, on every extent or viewport change. The left pane's vertical bar is hidden
and the right one reflects both, since the primed extents are equal. `Hidden` still scrolls
through the keyboard and the wheel; `Disabled` would switch AvaloniaEdit to word wrap.

## The demo loads on `Opened`, and the smoke snapshot zeroes the build time

A rendered frame carries no machine-specific text, and the strip shows the build time. The test
host wraps the real builder to zero `DiffDiagnostics.BuildTime`, and the demo's window loads
its sides on `Opened` rather than in its constructor so the smoke test can install the same
wrapper between construction and the first build; the demo grants the test project
`InternalsVisibleTo` for that one seam. The demo's colour-blind toggle merges the compiled
`DiffViewColorBlindPalette` class, for the same trim reason as the presenter theme.

## Pointer scrolling in the test: the thumb when the theme exposes one, otherwise the wheel

The plan's "dragging the gutter" test drives the right pane's vertical scrollbar with headless
pointer input. Semi's scrollbar may not expose a hit-testable thumb until hovered, so the test
drags the thumb when it finds one with a size and otherwise scrolls with the wheel; either way
it asserts that both offsets moved together and every shared row sits at the same
document-relative top. The first visual line's top is not the comparison: a padding block
straddling the viewport edge starts above it on one side only.

## Word-level pieces are looked up through the live documents, one lookup per build

`WordDiffCache` computes a row's pieces from two line texts and the model holds no text, so
`WordDiffLookup` sits between: given a row it finds the line on each side, reads it from that
side's `TextDocument` and asks the cache, which computes on the first request and keeps the
answer keyed by the model's version. The composite creates one lookup per build result, bound
to the options that build ran under, and hands it to both presenters; a host of two bare
presenters can build one over its own documents. The lookup is bounds-checked like
`PaneMetadata`: a row that is not modified, or whose line lies beyond its document between a
keystroke and the next build, gets no pieces and nothing throws. It runs on the UI thread,
like the documents it reads; the plan's "computed on the UI thread on demand from the visible
rows" is what the renderer's first frame of a row does.

## Word rectangles are the full row, through the visual line's column mapping

The rectangle for a piece runs from the piece's start column to its end column as the visual
line maps them — `VisualLine.GetVisualColumn` accounts for the padding element and for tabs —
and spans the full row height, the same band as the row tint it sits on, so the two read as
one highlight. Ranges are clamped to the line's current length. The renderer records every
rectangle it draws, which is how the test checks the geometry against the pieces and the visual
line rather than against pixels alone.

## The long-line tooltip lives on the marker margin, following the pointer

A modified row whose line exceeds `MaxWordDiffLineLength` gets no pieces; the change-marker
margin's tooltip, resolved per line under the pointer, says so with the limit. The margin sets
its tooltip as the pointer moves and clears it when the pointer leaves; the block-summary
tooltips of Phase 7 will use the same hook. The one-megabyte single-line fixture is generated
in the test rather than committed, and the test records how long its build, priming and first
frame took.

## Row geometry is uniform once primed, and navigation and the overview rely on it

After priming every row is exactly one line height tall on both sides, so a row's document
top is its index times the line height. The current-block border, the centring scroll of
`CurrentChangeIndex`, the connector polygons and the minimap's viewport all compute from that
product rather than asking the height tree, which keeps them cheap and identical on both
sides; the presenter's priming invariant (union of padded sets, re-prime on a font change) is
what makes the product true. A connector's left extent is the block's first
`ModifiedCount + DeletedCount` rows and its right extent the first `ModifiedCount + InsertedCount`,
because the row builder pairs modified rows first and then lays a side's own rows — a band
where both sides have lines, a wedge where one has none.

## The current change is state on the composite, cleared by every new model

`CurrentChangeIndex` clamps to the blocks, scrolls both panes so the block is centred, and
pushes the block to the presenters (border), the gutter (outline) and the minimap (edge mark);
a new model resets it to none, because its blocks are new. Next at the last block and
previous at the first stay where they are and say so through the status lane as a warning,
which clears itself; with no changes at all every command says that instead. The default key
bindings live in the composite's `KeyBindings` — F7, Shift+F7, F6 — where a host clears or
replaces them; Avalonia tries an ancestor's bindings before raising the key event, so they
fire while a pane has focus.

## The minimap buckets rows into pixel rows and jumps to the first changed row of a bucket

One bucket per pixel row of the control's height; a bucket's kind is the strongest of its
rows, deleted over inserted over modified, computed once per document and height. A click
jumps to the bucket's first changed row, or its first row when nothing in it changed, and the
composite centres that row; the viewport rectangle follows the panes' offset through the
composite's overview update on every scroll and layout. The 200k-line test builds its fixture
from a generator rather than a committed file, as the Core tests do.

## The gutter resizes the panes through a split ratio applied to two grids

The headers and the panes are two grids with the same star columns, and `SplitRatio` sets
both, so the headers stay over their panes when the gutter is dragged. A drag on empty gutter
space reports horizontal deltas; a press on a polygon selects its block instead. The gutter
tests its polygons by point-in-convex-quad, which the wedge shapes are.

## Tooltips are resolved per line under the pointer, in the margin base class

Both gutters resolve a tooltip for the line under the pointer as it moves and clear it as it
leaves; the base margin owns that mechanism and each margin supplies its text. Line numbers
name the counterpart line on the other side or say there is none; markers name the change
block with its counts and, on a long line, why it has no word highlights; the connector
gutter and the minimap carry the same block and row texts. The tooltips are on the model's
metadata (`PaneMetadata.OtherLine`, `PaneMetadata.BlockAt`), bounds-checked like everything
else the presenter reads from it.

## The find bar is chrome; the search state lives on the composite

`DiffFindBar` holds a query, four toggles, a scope and three pieces of text, and it runs
nothing. It raises `QueryChanged` and `OptionsChanged` from `OnPropertyChanged`, not from its
click handlers, so a host — or a test — can drive it by setting properties and get the same
behaviour a click gives. The composite pushes its own state back into the bar inside a
`_syncingFindBar` guard and ignores the events that come back, which is the same shape the
status strip uses for its dismiss control.

The three scope buttons are one segmented control made of `ToggleButton`s whose checked state
the bar owns: clicking the one already checked would otherwise uncheck it and leave no scope
selected, so the handler sets `Scope` from the sender and re-applies the checked states.

## A fresh result has no current match

`ApplyFindResult` leaves `CurrentFindMatchIndex` at -1 and highlights every match. Only
`FindNext`, `FindPrevious` and the property setter make a match current, and only that reveals
it — selects it in its pane, focuses that pane and centres its row in both. The reason is
focus: the search re-runs on every keystroke, and a current match that revealed itself would
pull focus out of the query box before the next character arrived. It also means the first F3
or Enter lands on match 1 rather than skipping it.

The walk wraps at either end, unlike change navigation, which stops and says so. A find bar
that stopped at the last match would cost a second key to start over, and the matches are in
row order, so the wrap is the only backwards jump on screen.

## The search worker reads a snapshot and a copy of the line table

`DocumentPaneText.Capture` runs on the UI thread: it takes `TextDocument.CreateSnapshot()` —
immutable and free to read from any thread — and copies each line's offset and length into two
arrays, because `TextDocument.Lines` is owner-thread only. The worker then answers
`IPaneText.GetLine` out of the snapshot. A test holds a document open in `RunUpdate()` while a
gated search completes; capturing on the worker instead makes it throw from
`TextDocument.VerifyAccess`, which is how that test was proven able to fail.

Searches run inline at or below `FindWorkerRowThreshold` (2,000 rows) and on `Task.Run` above
it, debounced by `FindDebounce` (150 ms) on the composite's `TimeProvider`, latest-wins by
generation exactly as builds are.

## Escape and F3 are the composite's key bindings, gated on the bar being open

`CloseFindCommand`, `FindNextCommand` and `FindPreviousCommand` all report `CanExecute` false
while the bar is closed. Avalonia marks a key handled only when a binding actually executes, so
Escape with the bar closed still reaches whatever else the host wants it for. Enter and
Shift+Enter are not bindings at all: they are handled in `DiffFindBar.OnKeyDown`, so they walk
the matches only while the bar has focus and an editable pane keeps its own Enter.

## The find bar's row is `Auto` and the bar collapses

The composite's template grew a third `Auto` row between the banner and the panes. A closed bar
is `IsVisible="False"`, so the row measures zero and the layout is byte-identical to Phase 7's —
which is why every snapshot from earlier phases still matches without being re-approved.

## Focus waits for the layout pass

A control that has only just become visible has not been measured, and an unmeasured control
cannot take focus: `DiffFindBar.FocusQuery` returns whether the box took it, and `OpenFind`
retries once through the dispatcher at `DispatcherPriority.Input`, below the layout pass. The
first headless run of the Ctrl+F test caught this — and, through it, that a composite whose key
bindings never see a key press is a composite with nothing focused inside it.

## `TextBox.Watermark` is obsolete in Avalonia 12

It is `PlaceholderText` now, and the obsoletion is an error under `-warnaserror` from the XAML
compiler (`AVLN5001`), not a warning at the C# layer. The find bar's property is
`QueryPlaceholder` to match.

## The query is never logged, only its length

`DiffViewLog.FindStarted` records the query's length and the options; `FindCompleted` records
counts, timings and truncation; `FindFailed` records that the query would not compile or timed
out, and never the engine's own message, which quotes the pattern. Ctrl+F pre-fills the query
from the pane's selection, so the query is document text as often as not, and the sentinel test
now searches for the sentinel to prove it does not reach the log.

## Syntax highlighting is an installation per pane, and a grammar registry per pane with it

`SyntaxHighlighting` wraps `AvaloniaEdit.TextMate` for one `DiffPanePresenter`: a
`TextMateSharp.Grammars.RegistryOptions`, the `TextMate.Installation` over the editor, and the
theme. Both are per pane rather than shared. The installation has to be, because it owns a
`TextMateColoringTransformer` on that text view; the registry does not have to be, and sharing one
would save reading the grammar index twice — but TextMateSharp tokenizes on its own thread and
reaches back into the registry for embedded grammars, and two panes tokenizing at once would be
two threads in one registry for no measurable gain. The registry is built lazily, on the first
file name that has an extension, so a pane comparing two strings never pays for it at all.

The installation colours **foregrounds**: it adds a line transformer, and it never touches the
editor's background. That is what lets the diff layers keep working unchanged — the row fills and
word pieces below the text, the match highlights and selection above it — and it is asserted as
pixels in `SyntaxSnapshotTests.Syntax_colour_and_the_inserted_fill_compose_on_the_same_row`, which
requires both the inserted row's fill and more than one token colour inside the same row.

## The grammar comes from the file name; an extension no grammar claims is a result, not a failure

`DiffPanePresenter.SyntaxFileName` takes a name or a path — the composite assigns
`PaneSource.Path`, falling back to `PaneSource.Title` — and `SyntaxHighlighting.GrammarFor` maps
its extension through `RegistryOptions.GetLanguageByExtension`. No name, no extension, or an
extension no grammar claims leaves the pane plain text, logs one `Debug` line, and installs
nothing at all: no transformer, no tokenizer thread, no cost. The bundled `.txt` fixture is
exactly that case, which is why every snapshot from Phases 4 through 8 still matches unchanged.

`Language.Id` (`csharp`, `json`) is what a message names; the scope name (`source.cs`) is what the
registry loads by. Both are carried on `SyntaxGrammar`.

## A grammar that will not install degrades the control and stays off until its inputs change

The install is a fault boundary like every other decorator: `DiffPanePresenter.DisableSyntax`
removes the installation, leaves plain text, and reports one `RenderFaultEventArgs` whose
`Subject` is the language, so the message names the grammar
(`RenderFault.OnSubject` — "{0} failed for {1} and was disabled: {2}"). What is *not* like the
other decorators is the retry: `ResetFaults` re-enables a renderer on every new model, but a
rebuild is not what would fix a grammar, so a failed install is retried only when
`SyntaxFileName` or `UseSyntaxHighlighting` changes — the install's own inputs. Repeating it per
build would flip the control between `Ready` and `Degraded` on every option change.

That outliving is why `DiffPanePresenter.SyntaxFault` is separate from `Faults`, and why
`SideBySideDiffView.ApplyResult` reads `PaneFault()` *before* it applies the model: a grammar
installs at source-assignment time, which is inside `Building`, where `OnPaneRenderFault` will not
change the state — and the `Ready` that follows the build would otherwise bury it. The first
version of the test caught exactly that, twice: once because the fault landed during `Building`,
and again because the second source's `RequestBuild` cleared the first side's faults.

TextMateSharp also raises on its own thread, after the install: the `exceptionHandler` given to
`InstallTextMate` posts to the UI thread and lands in the same `DisableSyntax`, which reports the
first fault and ignores the stream that follows it.

## The syntax theme follows the variant, and only the variant

`ThemeName.DarkPlus` under `ThemeVariant.Dark`, `ThemeName.LightPlus` otherwise, re-applied from
`ActualThemeVariantChanged` beside the palette refresh. The colour-blind palette is a `DiffView.*`
matter — the row fills — and does not reach the grammar's colours; a reader who needs different
token colours wants a different TextMate theme, which is a `RegistryOptions.LoadTheme` away if it
is ever asked for. The test asserts the *first token's* colour changes with the variant, not the
line's whole colour set: the set also holds the pane's own foreground, which the palette moves,
and an earlier version of the test passed for that reason under a mutation that pinned the syntax
theme to Light+.

## TextMateSharp trims clean, and the demo publishes at 57 MB

The Phase 9 trim-check needed no wiring at all: `dotnet publish -c Release -r linux-x64
--self-contained true` with `TrimMode=link` produced **0 IL warnings** with TextMateSharp,
TextMateSharp.Grammars and Onigwrap on board, and no `_ILLinkSuppressions` or
`TrimmerRootAssembly` entry. The grammars are embedded resources and the parser is hand-written —
no reflection, no `System.Text.Json` — which is why. The published output grew from 51 MB to
57 MB, most of it the grammar and theme resources; `libonigwrap.so` travels with it.

The trimmed binary was then run against two real `.cs` files, and its log carries
`Syntax highlighting on the "Left" pane: csharp` for both sides with no fault and a plain
`Ready` — the grammars resolve from the trimmed assembly at runtime, not only in the test host.

## The view options are the editor's own, pushed down from the composite

`ShowWhitespace`, `ShowLineEndings` and `TabWidth` exist on both `DiffPanePresenter` and
`SideBySideDiffView`; the composite's are pushed onto both panes, and each pane writes them onto
its `TextEditorOptions` — `ShowWhitespace` covering `ShowSpaces` **and** `ShowTabs`, because a
reader who wants to see one wants to see the other, and `TabWidth` mapping to `IndentationSize`,
coerced to at least 1 rather than throwing at a caller who computed a zero.

`DiffPanePresenter.ApplyDisplayOptions` also runs when `Options` itself changes, so a host that
replaces the whole options object does not silently lose them.

None of the three re-primes. A glyph is drawn inside the row it belongs to and a tab moves text
sideways: the row heights, and with them the padded heights the two extents are built from, do
not move. The tab-width test asserts exactly that — the first text column moves right, the
default line height does not move, and the extents stay equal.

## The pane font is named by the composite, never inherited into it

`PaneFontSize` (`double.NaN` by default) and `PaneFontFamily` (`null` by default) mean "leave the
panes' own theme in charge", and `ApplyPaneFont` *clears* the local value rather than writing a
default over it. The obvious alternative — let `FontSize` inherit from the composite into the
panes — does not work: `DiffPanePresenter`'s control theme sets `FontSize` and `FontFamily`, and a
`ControlTheme` setter beats an inherited value, so the inheritance would be silently ignored until
some ancestor changed its font, at which point every pane would jump to it. An explicit property
that a host sets on purpose is the honest version of the same feature.

A size change re-primes through the Phase 4 path — `PaddingHeightPrimer.LineHeightChanged` sees
the moved default line height on the next `LayoutUpdated` — which is what the plan's done-when
test asserts: after `PaneFontSize = 22` the rows are taller, the document is taller, and the two
extents are still equal.

## The focus accent is an overlay, so taking focus moves nothing

Which pane has focus was visible only as a caret, and a caret can be scrolled out of sight. The
focused pane's header now carries a 2 px accent along its bottom edge, in a new
`DiffView.FocusAccentBrush` (the same blues as the current-block border, 5.22:1 light and 6.45:1
dark against the header background, against a 3.0 floor for non-text UI).

It is a `Grid` overlay with `IsVisible="False"`, not a border thickness: a collapsed overlay
measures nothing, so an unfocused header lays out exactly as it did before this phase and no row,
gutter or connector moves when focus arrives. That is also why no snapshot from Phases 4 through 9
had to be re-approved for it.

`SideBySideDiffView.UpdateCaret` is where it is set, because that is the one place that already
runs on every focus change, and `UpdateHeader` sets it again when the headers are rebuilt.

## Avalonia 12 renamed the clipboard's read

`IClipboard.GetTextAsync` is `TryGetTextAsync` in Avalonia 12; `SetTextAsync` is unchanged. The
headless platform implements both, so the per-pane copy test reads back what the pane put there
rather than asserting on `CanCopy` alone.

## No DiffPlex vendoring: the Myers run is inside the budget

The plan left open whether to vendor DiffPlex's `Differ` with a cancellation check if a realistic
large pair ran too long. It does not: the 200,000-line pair (204,001 rows, 4,000 blocks) builds in
**297 ms** on this machine in Debug, on a worker, with the control showing its previous result
marked stale meanwhile. The build is already cancellable between stages, and a cancelled build's
result is discarded by generation. Vendoring would buy cancellation *within* one Myers run for a
cost that nothing in the measurements justifies, so it is not done; the numbers are in
`PROGRESS.md` and the decision is revisited only if a real pair misses the budget.

## The unified view composes its own document, and that one *is* replaced by a build

Every other document in this library is a source: the pane's editor holds the side's own text,
padding is rendered rather than inserted, and a rebuild swaps the model without touching the
`TextDocument` (`AGENTS.md` §1). The unified view cannot work that way — half its lines belong to
one file and half to the other — so `InlineDiffView` composes `PaneDocument` from
`InlineDocument.Lines`, taking each line's text from the side it names, and rewrites it whenever
the model changes.

Three consequences, all deliberate:

- The pane is **read-only, full stop**. There is no `LeftReadOnly`/`RightReadOnly` pair, because
  there is no edit that could mean the same thing for both files.
- The text is rewritten through `TextDocument.Text` on the one instance rather than by handing
  the pane a new document, so a rebuild that leaves the text where it was keeps the caret and the
  scroll offset; the undo stack is dropped straight after (`UndoStack.ClearAll`), a composed
  document having no edit history worth keeping.
- The two source documents are still built and kept — `LeftDocument`, `RightDocument` — because
  the word diff reads a row's two lines from them and the search runs over snapshots of them.
  They are simply never displayed.

There is no padding anywhere in the unified reading: every line it shows is a real line of its own
document, so `PaneMetadata.PaddedLineNumbers` yields nothing, the primer does no work, and the row
geometry is the editor's own uniform line height.

## A modified pair keeps its kind on both halves, so the word diff survives

A unified diff prints a modified row as a removal followed by an addition, which invites giving
the two halves the `Deleted` and `Inserted` kinds. `InlineDocument.Build` does not: both halves
carry **`Modified`**, and only a row that is a pure deletion or insertion gets `Deleted` or
`Inserted`. The reason is the word diff — `DiffLineBackgroundRenderer` computes pieces only for a
modified row — so kinds-by-appearance would have silently dropped word-level highlighting from
exactly the rows that have it. The two halves share the row, which is what
`WordDiffLookup.PiecesFor` is keyed by; the renderer picks the side's pieces with
`PaneMetadata.SideOf(lineNumber)`, which is the pane's own `Side` everywhere else and the line's
own side here. The change-marker gutter then reads `~` on both halves of a pair, which is the same
vocabulary the side-by-side view uses.

## The unified gutter is two columns, and a context line fills both

`DiffLineNumberMargin` draws the document's own line numbers for a side's pane — the document
*is* that side — and two columns for a unified pane: the left file's number and the right file's.
A removed line fills the left column only, an added line the right, and a **context line fills
both**, which is what makes the gutter readable as a diff and what `diff -u` consumers expect. The
unified document's own numbering names no line of either file and is never drawn; the same rule
puts the *source* line in the status strip's caret lane (`InlineDiffView.UpdateCaret`). Each
column is measured against its own side's line count, not the unified document's, which is the
sum of both.

## Find searches the two sides and drops what the unified view does not show

The unified view runs the same `DiffSearch` over the same two `IPaneText` snapshots as the
side-by-side view — one engine, one set of options, one truncation cap — and then maps each match
through `InlineDocument.LineOf`. A match whose line is not displayed is dropped, which happens for
exactly one case: the **right line of a context row**, whose text the left line already carries on
screen. So the two views find the same matches on the lines a user can see, and over
`ChangedRowsOnly` the counts are identical, no context row being searched at all.

The mapped matches are re-sorted by unified line and column: the engine walks the model's rows,
left before right *within* a row, while the unified view prints a block's removals before its
additions. `SearchMatchRenderer` binary-searches the pane's matches by line, so the order is a
requirement, not a nicety.

The find scope collapses with the panes: `InlineDiffView.FindOptions` coerces `Scope` to
`FindScope.Both` however it is assigned, and `DiffFindBar.ShowScope` hides the L / R / Both group
(separator included). A scope of one side would hide matches that are on screen.

## The unified view drops the minimap, the connector gutter and F6

Both overviews are two-sided by construction: `ChangeConnectorGutter` draws the wedge between two
panes' rows, and `DiffMinimap` addresses the model's rows, which are not the unified view's rows.
Neither is in the plan's list of what Phase 11 reuses, and neither is in the template. F6 goes
with them — there is no other pane to switch to — so `InlineDiffView` binds six keys where the
side-by-side view binds seven. What the block navigation does keep is the current-block border,
whose extent comes from `PaneMetadata.DisplayRowsOf`: the model's rows for a side, the block's own
unified lines for the unified view, where a modified pair takes two of them.

## A log line names the unified pane, which is neither side

`DiffPanePresenter.Side` is meaningless while `IsUnified`, so the three log calls that name a pane
take a `DiffSide?` and the presenter passes `LogSide` — `null` when unified, which
`DiffViewLog.Pane` renders as `unified`. Naming it "Left" would have been a lie in the one place a
reader goes to find out what failed. `Side` itself is left alone: it is a public property with a
default, and the unified pane simply does not use it.

## The ClaudeForge integration is deferred; in-pane editing comes first

Plan 00001 promised the control back to ClaudeForge "once it is ready to host the Save Changes
dialog's old/new rows". Plan 00002 was drafted against that promise and **rejected on its own
review** before any code was written. Both the draft and the review are kept under `plans/`.

The premise did not survive contact with the dialog. Those old/new columns are a property table,
not file text, and `ClaudeForge.Sdk.Diagnostics.JsonDiff` already recurses into nested objects and
computes a multi-set delta over arrays — deliberately, so that "a single hook removal" does not
emit a whole container. Its own summary is "surfaces just the leaf changes". By the time a
`PropertyDiff` reaches the dialog the blob has already been decomposed, so a `Modified` entry
holds two *leaves*: realistically two long single-line strings. A line-based diff of two
single-line strings renders one removed line and one added line, which is what the dialog already
prints as `~ key: old → new`. The unit of the control is the line; the unit of the data is the
character.

The cost was also larger than it looked. `DiffView.Avalonia` references `AvaloniaEdit.TextMate`
unconditionally — there is no syntax-free variant — so a consumer inherits 6.7 MB of
`TextMateSharp.Grammars` and a native `libonigwrap.so` per RID whether or not it colours anything.
Beyond that, every integration pays the same fixed toll: a solution-wide Avalonia bump across
three pin sets (12.1.0 application, 12.1.1 headless), a dependabot-managed group and a
pre-existing NU1605 pin; `nuget.config` wiring ClaudeForge has never had; new strings in nine
`.resx` files with real translations; four CI gates; and a rebase against the 83-commit
`feat/agentforge-opencodeforge`, which moves the target files into a product-neutral assembly.

Backup/Restore was checked as a retarget, since a genuine two-file diff would justify all of that.
It does not exist: `BackupRestoreView` is a configuration page, and the "restore preview" is the
same property table under `SaveDialogMode.Restore`. Retargeting means designing a new ClaudeForge
feature, not consuming an existing surface.

So integration waits, and gets cheaper by waiting — once the in-flight branch lands, the rebase
risk is gone. Editing is brought forward instead: it pays none of that toll, it is what plan
00001's nine editing-readiness choices were bought for, and it stresses the API in the way a
read-only consumer never would. The one piece salvaged and sent back on its own merits is the
correction of ClaudeForge's stale "AvaloniaEdit is incompatible with Semi.Avalonia" note, which
the compat dictionaries of PR #38 had already made false.

## A re-diff builds from the document; a source assignment builds from the source

`RequestBuild` composes its two sides through `EffectiveSource`, which returns the assigned
`PaneSource` until the user has edited that side and the document's own text afterwards. The
encoding, the path and the title always come from the source, because they are what a save writes
back with and typing does not change them. The text is read on the UI thread, like every other
text the worker sees.

That split is what lets one pipeline serve both. A source assignment replaces the `TextDocument`
and rebuilds with `keepModel: false`; an edit keeps the document and rebuilds with
`keepModel: true`, so the previous model stays on screen — misaligned by whatever the edit changed
— until the new one lands. Nothing else in the build path needed to know which case it was in.

Two defects surfaced while the phase's last test was being written, and both are worth stating
because neither was obvious:

**A source assignment must cancel a pending re-diff.** An edit arms a debounce timer against the
document it edited. If a new source arrives inside that window, the timer survives its document
and fires a rebuild that the assignment's own build has already superseded. `OnSourceChanged`
disposes the timer before it replaces anything.

**Re-assigning an equal source does nothing, so reverting needs its own verb.** `PaneSource` is a
record, so a source equal to the one already assigned raises no property change, `OnSourceChanged`
never runs, and an edited pane keeps both its edits and its edited flag. Restoring a pane to its
source is therefore a real operation rather than a re-assignment, and it belongs with the dirty
state and save work rather than here.

## Live re-diff is a property, not an assumption

`LiveReDiff` defaults to true and `ReDiffDelay` to 300 ms, but the control does not assume either
is affordable. Clearing `LiveReDiff` leaves the model exactly as it was until `ReDiffNow()` is
called. Priming the 200,000-line pair took 1,243 ms to be up, and a rebuild re-primes, so a pair
exists for which rebuilding on a debounce costs more than it is worth. Phase 6 measures where that
line falls; until it does, the escape hatch is deliberately part of the public surface rather than
something to be retrofitted once the measurement is in.

## The encoding carries the byte-order mark, so nothing else has to

`PaneWriter` writes `encoding.GetPreamble()` and then `encoding.GetBytes(text)`, and that is the
whole of the mark's handling. No flag records whether the file had one, because the encoding
already does: `PaneSource.FromBytes` hands back `Encoding.UTF8` — whose preamble is three bytes —
for a file whose preamble said so, and a `UTF8Encoding` constructed with
`encoderShouldEmitUTF8Identifier: false` for one that had none, whose preamble is empty. The same
holds for every UTF-16 and UTF-32 encoding a preamble selects. A separate `HadBom` would have been
a second source of truth for a question already answered.

A source built from a string has no encoding at all, and writes UTF-8 without a mark.

## Normalising line endings, except when there is nothing to normalise to

A save rewrites the text's terminators as `TextInfo.LineEnding`, because the editor works in its
own convention and a CRLF file must come back CRLF. `LineEnding.None` and `LineEnding.Mixed`
return the text untouched: there is no single convention to impose, and imposing one would rewrite
every line in a file the user changed one line of. A CRLF pair counts as one terminator, not two.

## Dirty and edited are different questions

`_leftDirty` is "there are changes not on disk" and clears on a successful save. `_leftEdited` is
"the document no longer matches the assigned `PaneSource`" and does not, because after a save the
document still differs from the source the control was handed, and `EffectiveSource` must keep
reading the document rather than reverting the diff to the original text. Only a source assignment
or a revert clears `_leftEdited`. Collapsing the two would either make the diff go stale after a
save or make every save look unnecessary.

## Reverting is its own verb, and the file's identity is a stamp

`Revert(side)` puts the source's text back through the same `TextDocument` — so the caret and the
scroll offset survive, and the revert is itself undoable — then clears both flags and rebuilds. It
exists because re-assigning the source cannot do the job: `PaneSource` is a record, so an equal
source raises no property change at all.

The disk-change check compares a `(LastWriteTimeUtc, Length)` stamp taken when the source was
assigned against the file at save time, and **the stamp follows every successful save**, or the
second save of a session would report a conflict with its own first. A side whose stamp was never
taken — the file did not exist when it was read — is not treated as changed, or a first save could
never happen. A save reports through `SaveOutcome` and the status strip rather than the banner:
the banner says what the *build* did, and a save is not a build.

## A copy makes the target's lines the source's lines, terminator included

`CopyBlock` replaces the target's `LineRange` with the source's, over the ranges `ChangeBlock`
already carries — which is what plan 00001 meant when it wrote "copy-to-side is a replace over
these ranges". The edit goes through the editor's own `TextDocument`, so undo takes a copy back
like any other edit, and the re-diff that follows collapses the block.

Only one fix-up survives at the document's ends, and finding out which was the useful part of the
phase. Two candidate branches were written and both turned out to be wrong:

**"A copied run that lacks a terminator needs one" is unreachable.** A block is a *maximal* run of
changed rows, so a run that reaches one side's last line reaches the other's too — there can be no
unchanged row after it on one side only. A copied run can therefore only lack a terminator when it
is going to the end of the target as well, where none is wanted.

**"Trim the terminator the target never had" is actively wrong.** The point of a copy is that the
block collapses, which means the target's bytes become the source's bytes. Trimming the source's
own trailing terminator leaves exactly the difference the copy was meant to remove.
`Copying_onto_an_unterminated_last_line_gives_it_the_source_terminator` now pins that.

What remains is the opposite case, which is real: appending past a last line that carries no
terminator has to put one in *front*, or the copied run joins onto it.

Both branches were found by mutating them and watching nothing fail. A mutation that survives is
not a gap in the tests by default — sometimes it is a gap in the code, and here it was twice.

## ~~The arrows are hit before the polygon they sit inside~~ — retired by plan 00004

**Retired.** The rule below described a hit-order between two controls over regions that overlap.
Plan 00004 moved the arrows into the panes' number margins, where an arrow is not inside a
polygon and the question stops existing. `ChangeConnectorGutter` no longer has
`CanCopyToLeft`, `CanCopyToRight`, `LastArrows`, `ArrowAt` or `CopyRequested`, and
`An_arrow_is_hit_before_the_polygon_it_sits_inside` is deleted rather than adjusted. The
replacement is *The copy arrow takes the line number's cell* below. Kept here because a reader
who remembers the rule should find out where it went, not just fail to find it.

The rule as it stood: `ChangeConnectorGutter` gains `CanCopyToLeft` / `CanCopyToRight`, an arrow
per block per editable direction, and a `CopyRequested` event. An arrow's hit-zone lies inside its
own block's polygon, so `OnPointerPressed` tests `ArrowAt` before `PolygonAt`; the order is what
decides whether a click copies the block or merely selects it, and a test pins it rather than
leaving it to the reading order of two `if`s.

Both flags are in `AffectsRender`. Without that the arrows appear only at the next unrelated
invalidation, which is the kind of defect that looks like a race and is not one.

The arrows draw in a token of their own, `DiffView.GutterArrowBrush`, dark on the light variants
and light on the dark ones, so the glyph reads against the block tint it sits on. A token rather
than a borrowed one, because the audit scores contrast per variant and a borrowed token would be
scored for a job it is not doing.

The glyph is a head **and a shaft**, not a bare triangle. The two arrows share one 24 px column
back to back, and two triangles meeting there read as a single bowtie rather than as two things
to click; Beyond Compare draws a shaft for the same reason, and at this size it is what makes the
direction legible. `TipInset`, `HeadLength`, `ShaftLength` and their half-heights lay each arrow
out from its tip inwards, which keeps the head on the column's outer edge whatever the rest is
set to, and leaves `InnerGap` unpainted at the centre so the two shafts never touch. Growing
`ArrowSize` past 12 means growing the column with it.

## The marker chip is computed over the pane, and shaped to the run

Plan 00005. Each marker glyph sits on a chip of its kind's colour, and consecutive rows of one kind
share it, so a lone changed line reads as a badge and a block reads as one band — the block's
extent, which neither the glyph nor the row tint shows in the gutter.

**The chip is composited over the pane background, not blended into the gutter.** The first design
blended: the marker's colour at 18 % over `DiffView.GutterBackgroundBrush`. It darkens the ground
*towards* the glyph, and it failed the 3.0 floor in two palettes — `#D96A00` at 2.60 and
Okabe–Ito's `#D55E00` at 2.84. No alpha rescued it: at 6 %, far too faint to be worth drawing,
`#D96A00` is still 2.98, and a neutral grey chip is worse. The reason is that `#D96A00` scores 3.49
against pure white and 3.17 against the gutter, so its entire headroom is 0.49 and blending
downward spends more than it has.

Compositing over the pane — `#FFFFFF` against the gutter's `#F3F4F6` — moves the ground the other
way, so the glyph gains contrast instead. Worst case across four palettes is 3.04, and **no palette
colour changed**. Two darkenings that an earlier draft proposed, `#D96A00` for a third time and the
canonical vermillion, are both unnecessary. The chip also lands on the colour the row tint already
is, which is what makes gutter and row read as one field rather than two.

The tokens are **opaque**, one per kind per palette per variant. Opaque because the colour is
deliberately not a blend with the surface it is painted on — computed over the pane, drawn on the
gutter — which a translucent brush cannot express, and because `theme-audit` can score an opaque
token directly with no `over` key to get wrong. Three of the four palettes use their own row tint's
alpha; Default Light steps 15 % to 12 %, its modified marker having the least headroom of any
marker in any palette.

**The contract is the durable part.** `contrast-pairs.json` scored markers against the plain gutter
and the host page and nothing else, so a decorator drawn *behind* a marker was outside what it
could see — which is how a chip that failed the floor was designed, rendered and reviewed before
anyone measured it. Three pairs now hold each marker against its own chip. Setting the Default
Light chip back to the blended value turns 0 low-contrast findings into 6 and prints 2.60 in every
theme target's column.

**The chip is shaped to the run, not the row**, with no second code path: a one-row run is a short
rectangle with the same corner radius. Two rules make it right. A run that carries on past the
viewport gets no rounded end there — the rectangle runs a row beyond the edge so the rounding falls
outside the visible area, rather than making a scrolled block look as though it ends where the
window does. And a run needs **no padding test**, though the first implementation had one: adjacent
lines of one changed kind are always adjacent rows, because a side's lines inside a block are
contiguous and blocks are separated by at least one unchanged row. A mutation that ran the loop
through padding changed no frame, which is how the guard was found to be unreachable rather than
merely untested.

The chip is symmetric about the margin's centre and stops exactly where the modified-since-load bar
begins, so an edited line inside a changed block paints both without overlap. The glyph is centred
in the chip rather than placed at `HorizontalPadding`. Those two are arithmetically identical while
the pane font gives all three glyphs one advance width — `MeasureOverride` returns the widest glyph
plus twice the padding — so no test distinguishes them; the explicit form is kept because it stays
correct under a host font whose fallback for one character does not, which is the same case the
measurement already covers.


## The change markers are operators, not ASCII punctuation

`+` for an inserted line, `−` (U+2212) for a deleted one, `≠` (U+2260) for a modified one, drawn
semibold. The glyph exists to carry a row's kind where colour cannot — the palette has a
colour-blind sibling, and row tints are translucent by design so text stays readable under them,
which makes hue a weak channel for kind even for a reader who sees it perfectly.

**The ASCII set failed at its own job, and only measuring showed it.** Ink laid down per glyph at
the pane's 14 px, against a single digit's 29 in the next column:

| | inserted | deleted | modified |
|---|---|---|---|
| `+` `-` `~` (was) | 20 | **5** | 14 |
| `+` `-` `~` semibold | 41 | **14** | 21 |
| `+` `−` `≠` semibold | 41 | 25 | 56 |
| `▲` `▼` `◆` | 53 | 44 | 48 |

The mark meant to survive a colour-blind reader was the faintest thing in the gutter, an order of
magnitude lighter than the number beside it. **Weight alone does not fix a hyphen** — it is a short
bar at any weight — so the character had to change, not just its rendering. The colours had been
scored against a 3.0:1 floor; nothing had ever scored the geometry, and contrast on a five-pixel
mark buys little.

Operators rather than the heavier geometric shapes, which are the most visible option on the
sheet: `▲`/`▼` read as sort direction rather than added/removed, and the set throws away the
plus/minus mnemonic every diff tool has taught. `≠` also says what a modified row *is* more
precisely than `~` did — the two sides are not equal. Over the heavier `✚` and `━`: U+2212 and
U+2260 are in every monospace font worth the name, while dingbats and box-drawing are not, and
the pane font is the host's choice rather than ours.

`MeasureOverride` measures all three glyphs rather than assuming one is widest, for that same
reason — a fallback for a character the host font lacks need not share the family's advance
width. No test can distinguish that from measuring one glyph while the bundled font covers all
three; it is defensive, and deliberately so.

## A line's number is not its identity, so the marks shift with the text

`ModifiedLines(side)` is the set of lines the user has edited since the source was assigned, and
it is maintained from `TextDocument.Changed` rather than recomputed. Only that event carries what
actually moved: the offset, the text removed and the text inserted. On each change the lines the
edit touched are added, and **every tracked line below the edit shifts by the number of lines the
edit gained or lost** — because inserting a line above a marked one does not un-edit it, and
comparing line numbers against a snapshot would mark every line below an insertion as changed.

Marks are per line, not per row, so they belong to the pane rather than the model and survive a
rebuild untouched. A revert clears them; a save does not, because the question they answer is
"what did this session change", not "what is unsaved" — the strip's lane and the header's marker
answer that one, and the two are deliberately different.

The bar is drawn down the margin's inner edge beside the diff's own glyph rather than replacing
it. The two answer different questions — what differs between the sides, and what this session
touched — and a line is very often both, so the tooltip says both.

**The bar covers the line's own row, not its whole visual box** (corrected under plan 00004; it
spanned the box until then). A visual box begins above the padding rows the *other* side's lines
put there, and this session did not edit those — nobody did, they are not lines. Measured before
the fix, on `"a\nb\n"` against `"a\nX\nY\nb\n"` with line 2 edited: 68 of the bar's pixels fell in
padding. A row rather than the text band, so that consecutive edited lines make one unbroken bar,
which is also why the copy arrow is centred on its row.

## Live re-diff is affordable at 200,000 lines, because a rebuild re-primes only what moved

Phase 2 shipped `LiveReDiff` and `ReDiffNow()` against a fear: priming the 200,000-line pair took
1,243 ms to be up, a rebuild re-primes, and a rebuild on every debounce might therefore be
unaffordable. Phase 6 measured it, and the fear was misplaced.

A re-diff of that pair costs **403 ms** end to end — 278 ms of it the diff engine — because
**priming is proportional to the padding that moved, not to the document**. The rebuild re-primes
4,000 lines, not 204,001; the 1,243 ms figure was a load from cold, where everything is primed for
the first time. The two numbers were never measuring the same work.

Nothing waits on that 403 ms either. The previous model stays on screen throughout, and the frame
drawn while it is stale costs 1 ms. What the user actually feels is the keystroke: 71 ms for the
first one on a document that size, and 18.9 ms per key sustained.

So **no size threshold is imposed**, and the DiffPlex vendoring decision closed in plan 00001
stays closed — 278 ms is well inside a debounce nobody waits on.

`LiveReDiff` stays in the public surface even so. Turning it off saves about 4 ms per keystroke of
bookkeeping and defers the rebuild entirely, which is a reasonable thing for a host to want on a
pair larger than anything measured here; and a property that exists is easier to reach for than
one that has to be retrofitted. It is now an option offered on evidence rather than a hedge
against an unmeasured worry.

## The copy arrow takes the line number's cell

Plan 00004. Each copy arrow lives in the number margin of the pane whose lines it would copy,
drawn over the line number of its block's anchor row, right-aligned to the edge the numbers use.
No new column, and nothing wider: `MinimumDigits` is 2 and `HorizontalPadding` is 6 a side, so the
narrowest cell the margin ever measures already clears the 12 px glyph. A test asserts the
measured width is identical with arrows on and off rather than trusting that.

**The arrow sits with the source and points at the target.** The left pane's arrow points right and
means *send this block over there*; it is offered when the **right** side is editable.
`DiffPanePresenter.CanCopyOut` carries the other side's flag, which reads backwards until you hold
the rule in mind, and is why it is named for what it does rather than for the flag it mirrors.
This is the inverse of the connector column's arrangement, and the overlay forces it: anchored to
the target, a pane's arrow would point at the pane's own text.

**The anchor is the block's first row.** A side's lines in a block start at the block's first row,
so on a side that has lines there the anchor is a real line and its number gives way. The cost is
one number per block, and it is paid only in edit mode — the arrow appears only where the other
side can receive a copy, so a read-only pair shows every number it has ever shown. The tooltip
carries the number that is not on screen, and says what the arrow in its place would do.

**A block a side has no lines in still offers its arrow**, because copying nothing over is as
meaningful as copying something — the re-diff collapses the block either way. Those rows are that
side's padding, so the arrow is drawn there and no number is given up at all. Two paths reach it:
padding above the line that follows the block, and, for a block past the last line, trailing
padding, which a walk over the visual lines never reaches. `PaneMetadata.BlockAtRow` exists
because a padded row has no line to look a block up by.

The arrow is centred on its **row**, not on the text band, so that an arrow standing in for a
number and an arrow in padding sit at the same height on the same row. Centring on the text band
put them 1.2 px apart — invisible, and wrong.

`DrawPaddingArrow` tests only that the arrow's row falls inside the padding. Testing the block's
line range as well would be a second guard on one invariant that nothing can make disagree, and a
mutation proved it unreachable rather than merely untested.

The margin hit-tests the arrow cell before the row it sits in. The two do not overlap — the arrow
has the number's cell and nothing else — but the order still decides what a click on an arrow
does, so a test pins it.

`DiffLineNumberMargin.LastColumnRight` is exposed for one reason: an arrow's own reported bounds
cannot show that it is where the numbers are, because a drawing that strayed would report the
place it strayed to. A mutation that shifted the glyph four pixels survived every assertion until
the numbers' edge was something a test could name.

## A selection copies, and its arrow takes the cell

Plan 00006. A block arrow sends a whole change block; a selection arrow sends the lines the reader
picked. `SideBySideDiffView.CanCopySelection(DiffSide)` / `CopySelection(DiffSide)` mirror
`CanCopyBlock` / `CopyBlock`, and both now write through one `CopyLines`, so a block copy and a
selection copy differ in the **range** they name and in nothing else — the terminator rules, the
append-past-an-unterminated-last-line rule and the undoable single `Replace` are shared, not
reimplemented.

**The rows are the mapping, not the line numbers.** A selection's rows are not a block's rows, so
none of `CopyBlock`'s ranges apply — but it is the same table read differently. The selection's
whole lines on the source side occupy a run of rows; the other side's lines *in those same rows*
are the target; `SideBySideDocument.Rows` and `LineOf` answer both questions and Core needed
nothing new. Where every one of those rows is padding on the other side, there is nothing to
replace and the copy inserts, which is what a one-sided block's copy already did. The insertion
point is the line after the last one that side has **above** the run — the same backward scan
`DiffDocumentBuilder.NextLine` uses to position a block's empty range, so the two agree by
construction rather than by coincidence.

**Whole lines, both ends.** A selection that starts or ends mid-line copies both lines whole,
because every copy in the library is line-based and a partial line has no counterpart on the other
side. The other end of that rule is the one worth writing down: a selection whose end sits on the
**very start** of a line stops at the line above. That is what a drag onto the next line's first
column means in every editor, and without it a three-line drag copies four.

**A selection beginning on a block's anchor row takes that cell, and the block arrow is not drawn
there.** Two arrows cannot share one cell, and the choice is decided rather than left to render
order: a selection is the more specific and the more recent intent. The block's own copy remains on
Alt+Left and Alt+Right, which is one of the reasons those stay bound to the block and were not
given to the selection. Clearing the selection hands the cell back to the block, not to the number.

**Alt+Left and Alt+Right keep copying the block, even while a selection exists.** A chord chosen
now would make Alt+Left mean two things depending on state the reader cannot see. The selection
copy is pointer-only, and the pointer is where it is unambiguous: a hand cursor over the arrow, and
a tooltip naming what would travel.

**The shape cue is the palette's decision, not the code's.** The two arrows are told apart by
colour in the default palette and by colour *and* a bar across the tail in the colour-blind one,
where blue is unavailable — `#0072B2` already means *inserted* there, so the selection arrow takes
Okabe–Ito's unused bluish green. `DiffView.SelectionArrowBarBrush` is `Transparent` by default and
the arrow's outline colour in the colour-blind palette; the margin lays the bar out and paints it
on **every** frame, and a transparent one simply is not seen. Nothing branches on the palette
because nothing can: the palette is a resource dictionary the *host* merges over the tokens, and
the control never learns it happened. A property would have been the other way to do it, and is
worse — the host would merge the dictionary *and* set the property, and the two could disagree. A
token cannot disagree with itself.

**`CopySelectionRequested` carries no payload.** `CopyOutRequested` carries a block index because a
block index is stable; a selection is not, so the composite reads it back through
`DiffPanePresenter.SelectedLines` at the moment it acts. Passing a range through the event would
create a second copy of the truth that could be stale by the time it arrived.

**A headless capture re-renders every visual, so no frame can show an invalidation.** The margin is
invalidated from `TextArea.SelectionChanged` for the reason `AffectsRender` was added to
`ChangeConnectorGutter`: an arrow that appears at the next unrelated redraw looks like a race. The
first version of the test asserted the frame — and passed with the wiring removed, because
`Window.CaptureRenderedFrame` draws the whole tree whether or not anything was invalidated.
`DiffLineNumberMargin.SelectionNotices` counts the notice instead, and the mutation dies. The rule
generalises: **where a decorator's contract is *when* it repaints rather than what it paints, a
captured frame is not evidence.**

**The frames cannot see the tail bar, and that was measured rather than assumed.** Removing the bar
entirely leaves all twelve phase 3 snapshots green — six pixels against a 0.5 % tolerance — which
is §5's trap in its purest form. `CopySelectionTests.The_selection_arrow_carries_the_palette_s_share_of_shape`
is the guard: read as luminance against the gutter, with colour discarded, the bar inks **9 rows of
the 12 px zone against a plain arrow's 5**, and exactly 5 in the default palette, where the token is
transparent. The PNGs are for a reviewer's eye; that pair of numbers is the contract.

## The change-block arrow is goldenrod, and a pale yellow fill will not fit a light gutter

Brian, looking at the plan 00006 frames: the block arrow should stand out from the selection's blue
the way Beyond Compare's does — a goldenrod outline over a yellowish fill — and the blue should
stay. The **default** palette's block arrow moved from slate accordingly. This is drift from an
approved plan, so it is recorded here rather than edited into `plans/00006-copying-a-selection.md`.

| | outline | vs gutter | fill | vs gutter | outline vs fill |
|---|---|---|---|---|---|
| Light | `#7A5C00` | 5.68 | `#AB8000` | 3.28 | 1.73 |
| Dark | `#FFD54F` | 10.85 | `#DAA520` | 6.84 | 1.59 |

**Beyond Compare's actual fill cannot be used on the light gutter, and the numbers are why.** The
gutter is `#F3F4F6`, a near-white, and the arrow fill is contracted at 3.0 against it. Khaki is
1.16 there; Okabe–Ito's yellow `#F0E442` is 1.20; goldenrod `#DAA520` is 2.03; even CSS
darkgoldenrod `#B8860B` is **2.96 — under the floor by four hundredths**. Nothing that reads as
*yellow* clears 3.0 on a near-white ground, because that is what "yellow" means. `#AB8000` at 3.28
is the palest golden fill with headroom, and it reads as goldenrod rather than as mustard only
because the outline above it is darker still.

The dark variant has no such problem — its gutter is `#252526` — and gets what Beyond Compare
actually shows: a bright amber outline over a goldenrod fill.

**The colour-blind palette keeps its slate block arrow.** Gold is not free there: `#E69F00` and
`#D55E00` are already spoken for in the Okabe–Ito set the palette draws from, and a fourth warm
hue in one gutter is exactly the collision that palette exists to avoid. The two arrows are told
apart there by the tail bar as well as by hue, which is the whole point of §*A selection copies*'s
shape rule, so the neutral loses nothing.

**Every one of the twelve frames this moved was under the comparer's tolerance** — 0.05 % to 0.11 %
against 0.5 % — so not one would have failed on its own. They were found with the §5 sweep. A
palette change is now the fifth time in this branch that a real change to what the frames depict
was invisible to the comparer.
