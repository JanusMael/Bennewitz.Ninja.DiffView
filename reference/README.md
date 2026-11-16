# Reference sources

This folder holds read-only checkouts of the upstream sources DiffView is built against, each at the version the
build pins, and of ClaudeForge, whose diagnostics package the demo restores:

| Source | Pinned by | Checked out | Read by |
|---|---|---|---|
| Avalonia | `Avalonia.Themes.Fluent`, `Avalonia.Themes.Simple` | the two themes' folders under `src` | the theme audit |
| AvaloniaEdit | `Avalonia.AvaloniaEdit`, `AvaloniaEdit.TextMate` | everything | the theme audit and `ThemeResolutionTests` |
| Semi.Avalonia | `Semi.Avalonia` | everything | the theme audit |
| DiffPlex | `DiffPlex` | everything | a reader of the diff engine |
| ClaudeForge | the commit `sources.json` names | everything | the theme audit and `scripts/pack-diagnostics.cs` |

They are for reading what the build compiles against, and two things read them unattended. The theme audit,
`ReferenceAuditTests`, compares a fresh `theme-audit report` over them with `docs/theme-audit.md`, and
`scripts/pack-diagnostics.cs` packs ClaudeForge's `LayeredEditors.Avalonia.Diagnostics` from its checkout into the
local feed the demo restores from. So every CI job fetches them before it restores, and the test project's
`EnsureReferenceSources` target fetches whatever is missing before it builds, which `-p:SkipReferenceSources=true`
skips.

Each checkout is a folder of its own, and git ignores every one: `.gitignore` ignores `reference/*` and re-admits each
tracked file by name, a file pattern rather than a folder one because a checkout linked in from elsewhere is a
symlink, which to git is a file. The tracked files:

| File | What it is |
|---|---|
| `fetch-reference.cs` | The script that fetches the checkouts, a .NET 10 file-based app, AppKit's |
| `sources.json` | Its manifest, one entry for each source |
| `README.md` | This file |
| `AGENTS.md`, `CLAUDE.md` | The rules for agents |
| `Directory.Build.props`, `Directory.Build.targets`, `Directory.Packages.props` | The shields, described below |

## Fetching

From anywhere inside the repository:

```bash
dotnet run --file reference/fetch-reference.cs
```

The script reads `sources.json`, works out each source's pin, and fetches what is missing: shallow, at the pin, and
sparse where the entry lists folders. Git converts no line endings in a checkout, so it holds the same bytes on every
platform, which the theme audit's hashes rely on. The script prints a line when a fetch starts, and one for each
source as it finishes, with its state:

| State | Meaning |
|---|---|
| `present` | Checked out at its pin |
| `fetched` | Fetched by this run |
| `missing` | Not there, or a fetch never finished; a plain run fetches it |
| `off-pin` | Checked out at another pin, or its HEAD has moved since; `--update` fetches it again |
| `failed` | The fetch failed, for the reason the line gives, and what it had fetched is removed |

| Option | Effect |
|---|---|
| `--status` | Reports only, fetching nothing |
| `--update` | Also fetches again each source off its pin |
| `--quiet` | Prints only what changed and what is wrong |
| `<name> ...` | Takes only the sources named, in any case |

It exits 0 when every source asked for is at its pin; 1 when one is missing, off its pin, or failed to fetch; and 2
on a usage error, or a problem with the manifest, a pin or `.gitignore`.

## Where a pin comes from

An entry that names `packages` is fetched at the commit their nuspecs record, the `<repository commit="…">` every
package carries, for the version `Directory.Packages.props` pins. That is the source the package was built from,
which a tag need not be: Semi.Avalonia's tag `v12.1.0.1` sits one commit past the commit its 12.1.0.1 package
records. A raised package therefore leaves its checkout `off-pin` until `--update` fetches it again at the new
version's commit.

A source no package pins names its `ref`: a tag, a branch, or a commit's full hash. ClaudeForge is one, its
diagnostics package being packed here from the checkout rather than restored from a feed that records its source.

## The manifest

`sources.json` is JSON that may hold comments. Each entry has these fields:

| Field | Meaning |
|---|---|
| `name` | The source's name on the command line and in the report |
| `dir` | Where it is checked out, relative to the repository root: a folder under `reference/` |
| `packages` | The packages whose pinned version, and the commit their nuspecs record, make the pin |
| `url` | The repository to fetch from. Beside `packages` it is optional, and must be theirs |
| `tag` | Beside `packages`, a tag holding `{version}`, fetched only when the nuspecs record no commit |
| `ref` | For a source no package pins: a tag, a branch, or a commit's full hash. It needs a `url` |
| `sparse` | The folders to check out; without it, everything |
| `local` | A sibling checkout to fetch from when it exists, relative to the repository root. The pin still holds, and the `url` serves when the sibling lacks it |

An entry has `packages` or `ref`, never both, and a field the script does not know is refused, so a misspelt one
cannot pass unnoticed. ⚠ `scripts/pack-diagnostics.cs` reads this file too, with a parser that refuses comments, so
it holds none.

## The shields

MSBuild imports the first `Directory.Build.props`, `Directory.Build.targets` and `Directory.Packages.props` it finds
walking up from a project. A checkout that ships none of its own would inherit this repository's: its packages, its
analyzers, warnings as errors, and central package management, which fails every `PackageReference` that has a
`Version` with NU1008, as ClaudeForge's did in DiffView's CI. The shields here end the walk: the first two hold
nothing but a comment, and the third turns central package management off and pins nothing.

The script builds under them too, so it builds alike in every repository that keeps it, with the settings its
`#:property` lines state, warnings as errors among them. It writes any shield that is missing.

## Rules

- **A checkout is read-only.** A change an upstream needs is a pull request to that upstream, never an edit here.
  `git -C reference/<name> status` shows whether anything in a checkout was changed.
- **`fetch-reference.cs` is AppKit's**, copied from `reference/` in `JanusMael/Bennewitz.Ninja.AppKit`: a change to
  it is made there and copied back, so every repository that keeps it runs the same script.
- **A search reaches a checkout by its path**, as in `rg -n StyledElement reference/Avalonia/src`. Ripgrep, and the
  tools built on it, skip what `.gitignore` excludes, so a search of the repository leaves the checkouts out, as it
  should unless the task is about one.
- **A raised package stops the test project's build** while its checkout is `off-pin`, until
  `dotnet run --file reference/fetch-reference.cs -- --update` fetches the new version; the theme audit's drift test
  then makes `docs/theme-audit.md` follow it.
