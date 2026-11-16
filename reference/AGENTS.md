# AGENTS.md — `reference/`

Read-only checkouts of the upstream sources DiffView is built against — Avalonia, AvaloniaEdit, DiffPlex and
Semi.Avalonia — and of ClaudeForge, fetched by `fetch-reference.cs` beside this file at the pins its packages'
nuspecs record, or `sources.json` names. [`README.md`](README.md) is the whole account. Git tracks
`fetch-reference.cs`, `sources.json`, the three shields and the three documents; `.gitignore` excludes everything else
here, re-admitting each tracked file by name, so a file added here is ignored until it is named there too.

## Rules

| Rule | Why | Guarded by |
|---|---|---|
| ⛔ **Prune `reference/` from every search of this repository**, unless the search is about upstream code | Each checkout is a whole repository, and a match in one belongs to upstream while reading exactly like a match in DiffView's own code | — |
| A checkout is read, never edited: a new pin is a raised package or a change to `sources.json`, and `--update` fetches again a source off its pin | The tests and the theme audit read upstream as it is at the pin | `dotnet run --file reference/fetch-reference.cs -- --status` |
| **`fetch-reference.cs` is never edited here** | It is a copy of AppKit's `reference/fetch-reference.cs`, the one script every repository that keeps reference checkouts runs: a change is made there and copied back | review |
| Its build settings stay in its `#:property` lines | It builds under the shields, never the repository's props, so it builds alike wherever it is kept | the shields |
| The shields pin nothing: `Directory.Build.props` and `Directory.Build.targets` are empty, and `Directory.Packages.props` turns central package management off | Without them a checkout shipping none of its own, and the script, inherit the root's: its analyzers, warnings as errors, and central package management, under which every `PackageReference` with a version fails with `NU1008` | their own comments |
| Every CI job fetches the sources before it restores | ClaudeForge's diagnostics package is packed from its checkout, and the demo restores from that pack | `.github/workflows/ci.yml` |
