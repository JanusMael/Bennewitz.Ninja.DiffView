# Hosting DiffView

Side-by-side and unified text diff controls for Avalonia 12, built on AvaloniaEdit: row-aligned
panes, line and word-level highlighting, change navigation, a minimap, find across either or both
panes, syntax highlighting, and optional in-pane editing.

This document is for someone putting the control into their own application. It is not an API
reference — every public member carries XML documentation, and your IDE will show it. It is the path
through that surface, plus the handful of things you must do rather than may call.

## Three names, and they are all different

The single most common way to lose an afternoon here. The package you install, the assembly you
reference and the namespace you import are three different strings:

| | Side-by-side | Model |
|---|---|---|
| **Package** | `Bennewitz.Ninja.DiffView.Avalonia` | `Bennewitz.Ninja.DiffView.Core` |
| **Assembly** | `DiffView.Avalonia` | `DiffView.Core` |
| **Namespace** | `Bennewitz.Ninja.DiffView` | `Bennewitz.Ninja.DiffView.Core` |

The control package depends on the model package, so installing the first is enough. There is no
type named `DiffView` — the controls are `SideBySideDiffView` and `InlineDiffView` — and the
namespace carries no `Avalonia` segment, because a segment of that name shadows the framework's own
root from inside the assembly.

There is also **no XAML namespace definition**, so there is no `https://` URI to import. You write
the CLR form, exactly as the quickstart does.

## Install

```bash
dotnet add package Bennewitz.Ninja.DiffView.Avalonia
```

## Quickstart

Two files. This is the whole thing.

```xml
<Window xmlns="https://github.com/avaloniaui"
        xmlns:dv="using:Bennewitz.Ninja.DiffView"
        Width="900" Height="600">
  <dv:SideBySideDiffView />
</Window>
```

Add your own `x:Class` and the usual `xmlns:x` when you wire it to code-behind; the markup above is
kept free of both so that it can be loaded and driven as it stands, which is how it is tested.

Give it two sources and it builds:

```csharp
view.LeftSource = new PaneSource(File.ReadAllText(leftPath)) { Title = "before.cs" };
view.RightSource = new PaneSource(File.ReadAllText(rightPath)) { Title = "after.cs" };
```

`PaneSource` converts implicitly from `string`, so a bare string works too. Prefer
`PaneSource.FromBytes` when you are reading files: it decodes by byte-order mark, then strict UTF-8,
then Latin-1, and reports what it did through `PaneSource.Latin1Fallback` — and it detects binary
content on the bytes before decoding, reporting it through `PaneSource.IsBinary`. `PaneSource.Title`
is what the header shows; `PaneSource.Path` is what the syntax grammar is chosen from.

Assigning either source starts a build on a background thread. The control moves through
`DiffViewState.Building` to `DiffViewState.Ready`, and you can watch `SideBySideDiffView.State` or
handle `SideBySideDiffView.BuildCompleted`.

## Theming: the one step you cannot skip

**Merge the theme, or you get a blank control.** The templates resolve nothing without it, and a
missing include raises no exception:

```xml
<Application.Styles>
  <StyleInclude Source="avares://DiffView.Avalonia/Themes/DiffView.axaml" />
</Application.Styles>
```

That URI is also available as `DiffViewResources.ThemeUri` if you would rather merge it in code. It
defines every token the controls use, for every theme variant, and never references a host theme's
own keys.

Three more, all optional:

| | |
|---|---|
| `DiffViewResources.TokensUri` | The colour palette alone, without the control styles — for a host that wants DiffView's colours under its own templates |
| `DiffViewResources.ColorBlindTokensUri` | Blue / vermillion / purple for inserted / deleted / modified. Merge **after** the theme to replace the default palette |
| `DiffViewResources.MonospaceFontFamilyKey` | Define this key in your own resources to override the panes' font |

### Semi is the intended base theme

The controls are developed and tested against Semi.Avalonia. They work under Fluent and Simple, but
AvaloniaEdit's search panel is templated for Fluent and resolves nothing under Semi — so if you run
Semi **and** host other Fluent-templated controls, merge `DiffViewResources.FluentCompatUri` after
the theme. `DiffViewResources.SimpleCompatUri` is its sibling. Neither is needed for DiffView's own
controls; the symptom they cure is a control that renders invisible rather than throwing.

## The properties that carry the control

You will not need most of the surface. These are the ones that change what a reader sees:

| Property | What it does |
|---|---|
| `SideBySideDiffView.UnchangedContextRows` | **Three behaviours in one property.** `null` shows every row, `0` shows only differences, and `n` shows differences with `n` rows of context. Off by default |
| `SideBySideDiffView.IgnoreWhitespace`, `SideBySideDiffView.IgnoreCase` | What counts as a difference |
| `SideBySideDiffView.WordDiff` | Word-level highlighting within a changed row |
| `SideBySideDiffView.UseSyntaxHighlighting` | Grammar colouring, chosen from the source's path |
| `SideBySideDiffView.ShowMinimap`, `SideBySideDiffView.MinimapPlacement` | The overview map and which side it docks to |
| `SideBySideDiffView.ShowHeaders`, `SideBySideDiffView.ShowStatusStrip`, `SideBySideDiffView.ShowBanner` | The pane headers, the status strip and the banner, each on by default. Switching one off takes what it carries with it: the headers carry the header context menu and the accent that says which pane has focus; the strip carries the transient message lane, the save outcomes and the caret position; the banner carries the only in-control way to retry a failed build or force an alignment, though `SideBySideDiffView.Retry` and `SideBySideDiffView.ForceAlignment` stay available either way |
| `SideBySideDiffView.SyncHorizontalScroll` | Vertical scrolling is always coupled; horizontal is opt-in |
| `SideBySideDiffView.ShowWhitespace`, `SideBySideDiffView.ShowLineEndings`, `SideBySideDiffView.TabWidth` | The editor's own view options |
| `SideBySideDiffView.PaneFontSize`, `SideBySideDiffView.PaneFontFamily` | Leave unset to let the pane theme decide |

Navigation is `SideBySideDiffView.NextChange` and `SideBySideDiffView.PreviousChange`, with
`SideBySideDiffView.CurrentChangeIndex` and `SideBySideDiffView.ChangeCount` for a position
indicator. Find is `SideBySideDiffView.FindQuery`, `SideBySideDiffView.IsFindBarOpen` and
`SideBySideDiffView.FindResult`.

## Editing is a flip, not a mode

Set `SideBySideDiffView.LeftReadOnly` or `SideBySideDiffView.RightReadOnly` to `false` and that side
becomes editable. There is no separate editing control and no rebuild of the view: an edit re-runs
the diff through the same worker without replacing the document, so caret, selection, scroll
position and undo history all survive it. `SideBySideDiffView.LiveReDiff` controls whether that
happens as you type, and `SideBySideDiffView.ReDiffDelay` how long it waits.

The rest is what you would expect: `SideBySideDiffView.IsDirty` and `SideBySideDiffView.CanSave` ask,
`SideBySideDiffView.Save` and `SideBySideDiffView.Revert` act, both taking a `DiffSide`. Copying a
change across is `SideBySideDiffView.CopyBlock` or `SideBySideDiffView.CopyToward`.

## Extension points

**Key bindings.** `SideBySideDiffView.KeyMap` is a mutable map from command to gesture. Start from
`DiffKeyMap.Default` (or `DiffKeyMap.UnifiedDefault` for the inline view), change what you want, and
assign it back.

**Context menus.** Handle `SideBySideDiffView.PaneContextMenuOpening` to add, remove or reorder
entries before a pane's menu opens; `SideBySideDiffView.HeaderContextMenuOpening` does the same for
the headers. To replace the menu wholesale instead, set `SideBySideDiffView.PaneContextMenu`.

**Text.** See below — it is not opt-in.

**Logging.** Set `SideBySideDiffView.LoggerFactory` and the control logs builds, faults and grammar
resolution through it.

## Localization happens whether you ask or not

The library ships translations for eight locales — German, Spanish, French, Japanese, Korean,
Brazilian Portuguese, Russian and Simplified Chinese. **A machine in one of those locales renders
the control's text in that language with no configuration from you.** This surprises people in both
directions, so it is worth knowing before a screenshot arrives in a bug report.

**Those eight translations are machine-generated and have not been reviewed by native speakers.**
Every locale file says so at its head. The structure is gated — keys, placeholder sets, and a suite
that stays green in German — but the wording is not. If you ship to those markets, review them or
override them.

To override, or to add a language the library does not ship, set `DiffViewStrings.Localization`. It
is one record: `DiffViewLocalization.Resolver` is consulted first and outranks the bundled
translations by construction, and `DiffViewLocalization.Culture` pins which culture is resolved.
Returning `null` from the resolver falls through to the bundled translation, then to English.

Formatting — numbers, dates — follows the current culture independently, as it should.

## What it costs you

**Syntax highlighting is unconditional.** The control references AvaloniaEdit.TextMate with no
syntax-free variant, so you inherit TextMateSharp's grammars and a native `libonigwrap.so` per
runtime identifier whether or not you ever colour anything. Measured on a self-contained linux-x64
publish, that is about **6 MB** — the output went from 51 MB to 57 MB, most of it grammar and theme
resources. Setting `SideBySideDiffView.UseSyntaxHighlighting` to `false` turns the feature off; it
does not remove the dependency.

It does trim cleanly: `TrimMode=link` produces zero IL warnings, because the grammars are embedded
resources and the parser is hand-written.

**Publishing trimmed keeps only the declared satellites.** The library names its eight locales in
`SatelliteResourceLanguages`. If you trim or otherwise prune satellite assemblies without carrying
that set forward, your users silently get English — no error, no warning.

**A menu cap you may inherit.** Semi caps a menu popup's height at 400px through its own
`MenuFlyoutMaxHeight` resource. If you add enough entries to a pane context menu to pass that, it
scrolls. Override the key in your application resources if you would rather it sized to its content;
the library cannot decide this for you.

## Diagnosing a build that did not go as expected

`SideBySideDiffView.State` tells you where you are. `DiffViewState.Degraded` means the build
succeeded but something is worth saying — read `SideBySideDiffView.Warnings`.
`DiffViewState.Failed` means it did not, and `SideBySideDiffView.BuildFailed` carries why; the
control shows a banner with a retry. `SideBySideDiffView.Diagnostics` carries build timings and
counts.

Binary input is **detected and reported, not diffed**: the banner says so and the panes stay empty.

## The unified view

`InlineDiffView` is the same model rendered into one pane instead of two. It takes the same sources
— `InlineDiffView.LeftSource` and `InlineDiffView.RightSource` — and most of the same display
properties. Use it where horizontal space is short.

## Where the control stops

These are deliberate non-goals, not gaps awaiting a patch:

- **Folder and directory compare.** One pair of texts at a time.
- **Three-way merge.** Two sides, no ancestor and no merge result pane.
- **Binary, hex and image compare.** Binary content is detected and reported only.
- **Rule-based "unimportant differences".** Whitespace and case are the two axes; there is no
  rule engine for ignoring generated regions or reordered members.
- **Right-to-left text.** A named non-goal; the eight shipped locales are all left-to-right.

## Working on DiffView itself

`README.md` in the repository is the contributor's document — layout, building, the theme audit and
the release procedure. This one is for consuming the control.
