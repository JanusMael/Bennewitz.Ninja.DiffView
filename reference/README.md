# Reference sources

This directory holds **read-only checkouts** of the upstream sources DiffView is built
against — Semi.Avalonia, Avalonia's Fluent and Simple themes, AvaloniaEdit, DiffPlex — and
a pointer to the ClaudeForge checkout. They exist for theme and control discovery, for reading
a control's behaviour before a feature is added here or in ClaudeForge, and for the tests that
audit theme keys (`theme-audit`).

The checkouts are not committed. `.gitignore` excludes every directory under `reference/`;
only this file and `sources.json` are versioned.

## Fetching

From the repository root, any of:

```bash
scripts/fetch-reference.sh
```

```powershell
scripts/fetch-reference.ps1
```

```bash
dotnet run scripts/fetch-reference.cs
```

The script reads `sources.json` and clones whatever is missing — shallow, at the pinned ref,
sparse where the entry asks for it — and reports every source with its state:

| State | Meaning |
|---|---|
| `present` | checked out at the pinned ref |
| `local` | satisfied by the entry's `local` path (a sibling checkout you already have) |
| `fetched` | just cloned |
| `missing` | not present; the plain command fetches it |
| `off-pin` | present at a different ref than the manifest pins; `--update` re-fetches it |

`--status` reports without fetching and exits 1 if anything is missing or off-pin; `--update`
re-fetches off-pin sources; naming sources limits the run to them.

`dotnet test` runs the fetch for you: the test projects' `EnsureReferenceSources` target invokes
the script before reference-dependent tests, so a fresh clone self-heals. A test that still cannot
find a source fails naming the command — it never silently skips.

## The manifest

`sources.json` has one entry per source:

| Field | Meaning |
|---|---|
| `name` | how the source is named on the command line and in tests |
| `dir` | where it is checked out, relative to the repository root |
| `url` | git URL to clone |
| `ref` | a tag, a branch, or a commit hash — the pin |
| `sparse` | optional list of paths to check out (`git sparse-checkout`) |
| `local` | optional sibling path used instead of cloning when it exists |

Pins track consumed package versions: bumping a package bumps its pin in the same commit, and
the theme audit's drift test forces `docs/theme-audit.md` to be regenerated with it.

## Rules

- Checkouts are read-only. A change an upstream needs is a pull request to that upstream, or a
  recorded local workaround — never an edit under `reference/`.
- "Latest" means bumping a pin here deliberately, in its own commit, with the audit diff in the
  commit body.
