# AGENTS.md — `reference/`

Read-only checkouts of the upstream sources DiffView is built against — Avalonia, AvaloniaEdit,
DiffPlex and Semi.Avalonia — and of ClaudeForge, fetched by `scripts/fetch-reference.cs` at the refs
`sources.json` pins. `README.md` says how. Git tracks `README.md`, `sources.json`,
`Directory.Packages.props` and the two documents beside them; `.gitignore` excludes everything else here,
re-admitting each tracked file by name, so a file added here is ignored until it is named there too.

## Rules

| Rule | Why | Guarded by |
|---|---|---|
| ⛔ **Prune `reference/` from every search of this repository**, unless the search is about upstream code | Each checkout is a whole repository, and a match in one belongs to upstream while reading exactly like a match in DiffView's own code | — |
| A checkout is read, never edited: a new pin is a change to `sources.json`, and `fetch-reference --update` re-fetches a source that is off its pin | The tests and the theme audit read upstream as it is at the pin | `scripts/fetch-reference.cs --status` |
| `Directory.Packages.props` here pins nothing: it turns central package management off for the checkouts | Without it, a checkout shipping no `Directory.Packages.props` of its own inherits the root's, and every `PackageReference` with a version fails with `NU1008` | the file's own comment |
| Every CI job fetches the sources before it restores | ClaudeForge's diagnostics package is packed from its checkout, and the demo restores from that pack | `.github/workflows/ci.yml` |
