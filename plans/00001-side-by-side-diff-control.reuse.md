# Reuse from ClaudeForge for plan 00001

Examined: `/home/janus/c/cl/ClaudeForge` at `befedb0` — the root docs (`AGENTS.md`,
`AGENT-ONBOARDING.md`, `CONTRIBUTING.md`, `TRIMMING.md`, `PLATFORM.md`), `docs/UI-STYLE-GUIDE.md`,
`docs/AVALONIA-GOTCHAS.md`, the build props and CI, the `LayeredEditors.*` libraries, the status
controller, the headless and accessibility test harnesses, and the app bootstrap. AvaloniaEdit's
own theme files were read alongside to check one of the style guide's warnings against this plan.

## Summary

| What | Form of reuse | Lands in |
|---|---|---|
| `StatusController` + `StatusKind` — transient status lane with lifecycle | Copy (MIT), attribute | Phase 4 status strip |
| `AppStatus*` pill tokens with measured contrast; kind-pill glyphs and colours | Copy values | Phases 3–4 theme dictionaries |
| `AxamlAccessibilityCoverageTests` — ratchet-baseline guard | Copy, retarget | Phase 0 test infra |
| `LayeredEditors.Avalonia.Diagnostics` — Serilog bridge, binding-error logger, crash dialogs, F12 log | Consume as a package | Phase 0 demo app |
| `WrapperStrings.Resolver` — library strings with host override | Pattern | Phase 4 first user-visible strings |
| `FocusOnRequest`, `BrushHelper` | Pattern | Phases 3, 7 |
| Token taxonomy, "own your tokens under Semi", contrast budgets | Pattern, plus tests | Phases 3–4 |
| Semi.Avalonia as a first-class host theme | New requirement | Phases 0, 3, 4 |
| Avalonia 12 gotchas, error-handling conventions, test seams | Conventions | All phases |
| Build props, CI matrix + trim-check, changelog, `AGENTS.md` discipline | Conventions | Phase 0 |
| `SaveChangesDialog` old/new rows | A future consumer | After this plan |

## 1. Code to lift

### `StatusController` and `StatusKind`

`src/ClaudeForge/ViewModels/Status/`. A five-kind lane — `Active` sticks, `Success` auto-clears
after 6 s, `Warning` after 10 s, `Failure` sticks until dismissed, `State` is quiet identity text —
with dispatcher-safe auto-clear, a `DelayOverride` seam and `ResetForTesting()`. The plan's status
strip has a build-state enum but no lane for transient messages ("Files are identical", "Word-level
skipped on 3 lines", "Grammar `csharp` failed to load"). The controller *is* that lane. Its
invariant — callers go through typed `SetStatusXxx` helpers so severity is classified at the call
site — is worth keeping verbatim. Replace the `DelayOverride` `Func` with a `TimeProvider`, which
is the .NET 8+ idiom and also serves the find debounce and the re-diff debounce later.

### Status and kind tokens

The eight `AppStatus{Success,Warning,Failure,Active}{Foreground,Background}Brush` values in
`App.axaml` were retinted against two contrast budgets at once (fill vs bar ≥ 1.3:1, glyph vs fill
≥ 4.5:1, and ≥ 7:1 on the plain page). Take the values as `DiffView.Status*`. The kind pills —
`+` `#2E7D32`, `-` `#C62828`, `~` `#F57C00` (Orange 700, chosen because Orange 900 reads as red
next to the removed pill) — are exactly the `ChangeMarkerMargin` glyphs; using the same colours
means DiffView looks native inside ClaudeForge's Save Changes dialog if it is ever hosted there.

Turn the guide's "measure, don't eyeball" into a test: a unit test computes the WCAG ratio for
every foreground/background token pair in both theme variants and fails below the stated floor.

### `AxamlAccessibilityCoverageTests`

`tests/ClaudeForge.Tests/Accessibility/`. Scans AXAML for interactive controls without
`AutomationProperties.Name`, with a per-file baseline that can only ratchet down. Retarget the scan
at `src/DiffView.Avalonia/Themes/*.axaml` (the control templates) and the demo's views; start the
baseline at zero. It is the cheapest permanent enforcement of the plan's "focus visuals and
automation names" item, and it belongs in Phase 0, not Phase 9.

### `LayeredEditors.Avalonia.Diagnostics`

Everything the plan's *Error handling* section wants at the app level already exists here:
`ConfigureLogging` (bucketed rolling file sink, Trace, F12 live window, Avalonia-logger bridge with
muted `Layout`/`Property`/`Visual` areas), `InstallAvaloniaHooks` (the `BindingValidationErrorLogger`
that catches coercion errors Avalonia's own logger never emits), `FatalErrorDialog` with a native-OS
fallback, `NonFatalNoticeDialog`. The demo app should consume it whole and adopt `App.axaml.cs`'s
handler triple (`Dispatcher.UIThread.UnhandledException`, `TaskScheduler.UnobservedTaskException`,
`AppDomain.UnhandledException`) including the benign-Linux-DBus classification and the
"cancellation is control flow" rule.

The library keeps `ILogger`; Serilog attaches through `Serilog.Extensions.Logging`. The library
is a project inside the ClaudeForge repo with NuGet metadata marked "filled in before first
publish" — how DiffView consumes it (local feed, git submodule, or a copy) is a decision.

Two concepts from it belong in DiffView's *tests*, not only the demo: bridge Avalonia's logger into
a test sink and fail any headless test that produces a `Binding` or resource-resolution warning
while rendering the composite control. That is the plan's "no silent failure" rule applied to the
XAML layer.

### Smaller pieces

- `FocusOnRequest` (`int` bumper, not `bool`; posted to the dispatcher) — the find bar's Ctrl+F
  focus if the bar is view-model driven; the same "why int" reasoning applies to any "do it again"
  request from a VM.
- `BrushHelper.Resolve(key, fallbackHex)` — the shape for `DiffBrushes`: background renderers need
  `IBrush` in code, resolved from `DiffView.*` tokens with hard fallbacks, and re-resolved on
  `ActualThemeVariantChanged` as `JsonHighlightBlock` does.
- `HeadlessTestApp` with `[assembly: AvaloniaTestApplication]` — same shape as the plan's, but note
  §3 on the harness difference.

## 2. Patterns and requirements to adopt

### Semi.Avalonia is a first-class host, and it changes Phase 3

The user's apps run Semi. The style guide records that AvaloniaEdit-coupled packages broke under
Semi (`ThemeDetector.IsFluentUsed` gates theme loading; missing Fluent typography keys cascade into
`KeyNotFoundException`), and ClaudeForge *dropped* the AvaloniaEdit-dependent Markdown package to
escape it. Checked against AvaloniaEdit master: `Themes/Fluent/AvaloniaEdit.xaml` depends on
`SystemAccentColor`, `SystemBaseLowColor`, `SystemChromeMediumColor`; `Themes/Simple` on
`ThemeBorderLowColor`, `ThemeBackgroundColor`, `HighlightColor`. None are guaranteed under Semi.

Consequences for the plan:

- `DiffPanePresenter` ships its own style that binds every brush the editor template needs to
  `DiffView.*` tokens and never references a host theme key. AvaloniaEdit's theme include is not
  required by consumers.
- The demo runs under **Semi** as its primary theme with a Fluent switch; Phase 0's *done when*
  covers both.
- Phase 4 adds a headless test that renders the composite under Semi and under Fluent and asserts
  no resource or binding warnings via the logger bridge.
- Token rule from the guide, verbatim: define an `App`-style token for every user-visible
  foreground, background and border, per theme variant; never fall back to `SystemControl*`.

### Accessibility: never colour or glyph alone

Every pill, marker and badge carries both `ToolTip.Tip` and `AutomationProperties.Name`, bound to
the same string; tooltips go on the parent *and* the hovered child (Avalonia does not walk up);
`AccessText` is for mnemonics, not a substitute for a name; `ContentControl` auto-derivation is
unreliable (emoji, underscores). The plan's marker glyphs are ASCII on purpose — the guide records
that `⭐` renders as a box on some Linux fonts while `★` does not.

### Avalonia 12 gotchas that apply directly

- A property a Style sets must not also be set as an attribute: LocalValue outranks every Style
  setter. Our templated controls put defaults in styles so theme and state classes can win.
- A control's `Styles` apply to its descendants, not itself.
- `DataTemplate`s match in declaration order; derived before base.
- Mixed fonts on one line: `Run` inlines share a baseline; two `TextBlock`s do not. The status
  strip's monospace `line:col` next to proportional text uses `Run`s.
- `DockPanel LastChildFill` where text must wrap; a horizontal `StackPanel` measures with infinite
  width.
- `IsVisible="False"` still realizes the subtree; collapsed content should have an empty source.
- `WithInterFont()` for the UI chrome; `Window.Icon` is a no-op on Wayland; log `XDG_SESSION_TYPE`
  in diagnostics on Linux.
- Monospace stack: the guide names `Consolas,Menlo,monospace` and warns that Courier New "breaks
  alignment in side-by-side diff columns" — they have hit this before. Adopt
  `Cascadia Mono, Consolas, Menlo, DejaVu Sans Mono, monospace`.

### Error-handling conventions

No bare `catch { }`; filter with `when (ex is …)` and log; `OperationCanceledException` is
control flow at Verbose; no `static readonly` capturing host state (property with `=>`); benign
platform exceptions classified once, in the global handler; `Starting`/`Exiting` log brackets so a
post-mortem can tell a crash from a clean quit.

### Test seams and harness

- `internal static … ResetForTesting()` on every type with a static seam, called from test
  cleanup; `InternalsVisibleTo` declared as an `AssemblyAttribute` item in the csproj.
- Timing through an injectable `TimeProvider`.
- ClaudeForge uses MSTest with `HeadlessUnitTestSession.Dispatch`; its `AGENTS.md` records that
  `Session.Dispatch(async () => …)` compiles to `Task<Task>` and makes a test pass
  unconditionally — nineteen of its headless tests are inert for this reason. The plan's
  `Avalonia.Headless.XUnit` 12.1.2 (xunit v3) runs `[AvaloniaFact]` bodies on the dispatcher with
  real awaiting, so the trap does not exist there. Keep their rule anyway: a new headless test is
  proven able to fail with a temporary `Assert.Fail` before it is committed. Avalonia tests run in a
  serial collection, as their `[assembly: DoNotParallelize]` does.

### Trim safety from day one

ClaudeForge publishes `PublishTrimmed=true`, `TrimMode=link`. If DiffView is ever hosted there it
must be trim-clean: compiled bindings with `x:DataType` everywhere, no reflection JSON, no
string-typed type lookup. AvaloniaEdit and TextMateSharp under ILLink are unknowns. Add a
`dotnet publish -c Release -r linux-x64` of the demo as a Phase 0 check and a CI job mirroring
their `trim-check`; when an IL2026 appears, use the one wiring `TRIMMING.md` proves works
(`<_ILLinkSuppressions>` plus `Scope="module"` attributes) or `<TrimmerRootAssembly>` for packages
that resolve their own types by name.

### Library strings

`WrapperStrings` — a static class whose members read through a swappable `Resolver`, English by
default, with `ResetForTesting()`. `DiffViewStrings` follows it for the find bar, status strip,
tooltips and banners; hosts map keys to their own resx before the first template is parsed.

### Repository conventions

- `Directory.Build.props`: `net10.0`, `Nullable`, `ImplicitUsings`, `LangVersion preview`,
  `TreatWarningsAsErrors`, portable PDB in Debug and embedded in Release, the
  `Bennewitz.Ninja.AutoVersioning` generator hoisted once; `tests/Directory.Build.props` imports the
  root file with `GetPathOfFileAbove` and pins the shared test packages; `global.json` with
  `rollForward: latestFeature`.
- Namespaces `Bennewitz.Ninja.<Product>` with unprefixed assembly names; `AssemblyCompany`,
  `AssemblyProduct`, `CopyrightHolder` set once.
- CI: build and test on ubuntu, windows and macos with `fail-fast: false`, results uploaded as
  `.trx`; a separate trim-check job; dependabot.
- Conventional Commits with dense bodies; Keep a Changelog; one concern per commit.
- `AGENTS.md` discipline: fact-shaped invariants (file, type, member, test name), no line numbers,
  no dates, no counts, no renumbered row ids; per-folder sidecars only where contract density
  justifies them. DiffView will have such contracts by Phase 3 (metadata version stamps, workers
  never touching a `TextDocument`, `SearchPanel.Uninstall`, height priming triggers); an
  `AGENTS.md` starts then, alongside `DECISIONS.md`.
- `docs/AVALONIA-GOTCHAS.md` and `docs/UI-STYLE-GUIDE.md` are the two documents the guide itself
  says to copy when forking the visual layer.

## 3. A consumer on the other side

`SaveChangesDialog` renders `JsonDiff`/`PropertyDiff` rows as `+ - ~` pills with truncated Old and
New cells and full-value tooltips. A Modified row's old/new JSON pair is a side-by-side text diff
waiting to happen. That integration is the concrete reason DiffView should be Semi-native,
trim-clean, `ILogger`-based and consistent with ClaudeForge's kind colours from the first commit.

## 4. Not taken

The `LayeredEditors` abstractions and property editors (config-scope domain), `PlatformInfo`
emulation and `PlatformPaths` (app-level), `ScopeTheme.axaml`, the MAUI share service,
`Markdown.Avalonia`, the MSTest `Dispatch` harness, and Semi's `Locale="en-US"` pin (a host
decision, not a library one).

## 5. Proposed changes to the plan

1. Stack: Semi.Avalonia 12.1.x for the demo; Semi primary, Fluent secondary. Phase 0 boots both.
2. Phase 3: presenter style bound only to `DiffView.*` tokens; no host theme keys; Avalonia's
   theme include not required by consumers.
3. Phase 4: headless render under Semi and Fluent with the logger bridge asserting zero binding
   and resource warnings; `StatusController` lane in the status strip with `TimeProvider`;
   `DiffView.Status*` tokens with contrast-ratio tests.
4. Phase 3: marker and kind colours from ClaudeForge's kind pills.
5. Phase 0: accessibility coverage guard retargeted at the templates; demo consumes
   `LayeredEditors.Avalonia.Diagnostics`; trim-check publish; `Directory.Build.props` and
   `tests/Directory.Build.props` in ClaudeForge's shape; `global.json`; CI matrix and trim job
   when the repo has a remote.
6. Phase 4: `DiffViewStrings` resolver for every user-visible string.
7. Conventions: the gotchas above; `ResetForTesting` and `TimeProvider` seams; the "prove the test
   can fail" rule; `AGENTS.md` from Phase 3.
8. Phase 9: the monospace stack.
