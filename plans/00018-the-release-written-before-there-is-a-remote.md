# 00018 — The release, written before there is a remote

Package ids were settled in `3ae46d8` — `Bennewitz.Ninja.DiffView.Avalonia` and
`Bennewitz.Ninja.DiffView.Core`, both unclaimed. `dotnet pack -c Release` already produces sound
packages: the project reference becomes a dependency on the Core id, and all eight satellite
assemblies and the XML documentation land in `lib/net10.0`. What is missing is everything that makes
a package **takeable**: a licence, an author, a version that means something, and a path from a tag
to nuget.org.

**The version is not a csproj setting.** `Bennewitz.Ninja.AutoVersioning` stamps assembly attributes
with a computed caldate and accepts an optional `PublicVersion` it records as metadata; it does not
set `PackageVersion`. In this author's other repositories the package version comes from the **git
tag**, through the release workflow: a push of `v2026.3.914` matches `v*.*.*`, the job strips the
`v`, and `/p:Version=` carries `2026.3.914` into both `build` and `pack`.

The caldate is `YYYY.Q.MDD` — year, **quarter**, then month and day run together. AutoVersioning's
own documentation gives a build on 2026-04-29 at 14:35 producing `2026.2.429.1435`, and the package
form drops the time. `Bennewitz.Ninja.AutoVersioning` 2026.3.914 and `Bennewitz.Ninja.Chisel`
2026.3.819 both decode. `Bennewitz.Ninja.FileServer` **2026.9.2 does not** — there is no quarter 9 —
so the scheme is not uniform across the author's repositories, and this plan follows the two that
agree.

**There is no remote.** Decided: write all of it now against the intended
`https://github.com/JanusMael/DiffView`, and let it sit dormant. Nothing runs and nothing publishes
until that repository exists, which makes this the cheapest possible moment to get it wrong and
find out.

## Goal

**A stranger can take this package**: it has a licence, an author, a project it came from, and a
version that says when it was cut. Tagging `vYYYY.Q.MDD` is the whole release procedure, and the
first person to try it is not also debugging it.

## Non-goals

| Excluded | Note |
|---|---|
| **Publishing anything** | No push, no tag, no nuget.org. The workflow is written and inert. A package id and version are permanent — unlisting is possible, deleting and reusing are not — so the first push is Brian's to run, deliberately |
| **Creating the remote** | Brian's, and the workflow does not care when it happens |
| **`PackageReadmeFile`** | A package readme is a consumer-facing document, and so is the hosting guide that plan 00019 writes. Writing both means writing the same thing twice and keeping them in lockstep — the objection plan 00014 already recorded about a second English surface. The guide owns it, and the release cannot go out before the guide any more than it can before the remote |
| **Publishing `Bennewitz.Ninja.ThemeAudit`** | It is local-feed-only by design: `NuGet.config` maps that exact id to `../nuget-local` and the README installs it from there. It nevertheless **packs** from this solution, which is a trap this plan gates rather than tolerates |
| **Changing the version scheme in the author's other repositories** | `FileServer`'s `2026.9.2` is noted, not fixed. Not this repository's business |
| **Signing, SBOM, symbol packages** | Each is its own argument. `snupkg` in particular is a separate decision about whether sources are published |

## Architecture

### The tag is the version, and nothing else is

Mirrors the existing `Bennewitz.Ninja.AutoVersioning` release workflow rather than inventing a
variant: trigger on `v*.*.*` and on `workflow_dispatch` with a version input, resolve
`VERSION=${GITHUB_REF_NAME#v}`, and pass `/p:Version=$VERSION` to **both** `build` and `pack` —
`--no-build` on the pack means a mismatch between the two would silently pack the wrong thing.

`PublicVersion` is wired to `$(Version)` so the assembly metadata records what was released, which
is what AutoVersioning's `PublicVersion` hook is for and what the repository currently leaves empty.

**A consequence worth stating rather than fixing:** the package version comes from a tag a human
writes, and the assembly caldate is computed from build time. Tag on a different day than you build
and the two disagree. That is inherent to the scheme, it is how the author's other packages already
behave, and the release notes say which one is authoritative.

### Trusted Publishing, so there is no key

`NuGet/login@v1` exchanges the job's GitHub OIDC token for a short-lived nuget.org key, with only
`NUGET_USER` stored as a repository secret. No long-lived `NUGET_API_KEY` to hold, leak or rotate.

The cost is a coupling that fails in an unhelpful direction: the nuget.org Trusted Publishing policy
names **owner, repository and workflow filename**, so renaming `release.yml` later breaks publishing
with an authentication error rather than a missing-file one. That belongs in a comment at the top of
the file, where somebody renaming it will read it.

### The push names its packages

`dotnet pack` over this solution produces **three** packages, because `src/ThemeAudit` is packable
too. The upstream workflow pushes `./packages/Release/*.nupkg`, and copying that glob here would
publish `Bennewitz.Ninja.ThemeAudit` to nuget.org on the first release — irreversibly, since a
published id cannot be withdrawn.

So the push step names the two packages explicitly, and **a test reads the workflow and fails if it
ever globs**. That is an unusual thing to assert about YAML, and it is justified by the asymmetry:
the failure is silent, instant and permanent, and the test costs twenty lines.

### What a package needs before anyone can take it

| Property | Value | Why it is not optional |
|---|---|---|
| `PackageLicenseExpression` | `MIT` | A package with no licence is one nobody's compliance review passes. There is no `LICENSE` file in the repository at all today |
| `Authors` | `Brian Bennewitz` | Currently defaults to `AssemblyName`, so it packs as *DiffView.Avalonia*. Matches `AutoVersioning` and `FileServer`; `Chisel` says *Bennewitz.Ninja*, and that inconsistency is noted rather than propagated |
| `PackageProjectUrl`, `RepositoryUrl` | `https://github.com/JanusMael/DiffView` | Only `<repository commit="…">` is emitted today — a commit hash pointing at no repository |
| `PackageTags` | avalonia, diff, control, side-by-side, avaloniaedit | Discovery. Cheap and wrong to skip |
| `CopyrightHolder` | `Brian Bennewitz` | AutoVersioning generates the copyright attribute from it; `AssemblyCompany` alone yields *Bennewitz.Ninja* |

## Phases

| Phase | Size | Content |
|---|---|---|
| **1 — The licence and the metadata** | S | `LICENSE` (MIT, 2026, Brian Bennewitz), the properties above in `Directory.Build.props` where they are shared and in the two csproj files where they are not, and the gate: **every packable project carries a licence, an author and a project URL**, proven against a project stripped of each |
| **2 — The workflow** | M | `.github/workflows/release.yml`, mirroring the upstream one and adapted — the reference fetch and diagnostics pack `ci.yml` already needs, `/p:Version=` on build and pack, Trusted Publishing, an explicit two-package push, and the GitHub release. The gate that the push never globs. **Inert**: no remote, so nothing runs |
| **3 — The record** | S | `DECISIONS.md`, `AGENTS.md`, `PROGRESS.md` — whose *Open* item becomes *"tag and push, once the remote and the guide exist"* — `CHANGELOG.md`, and `README.md` gaining the release procedure |

## Testing

Every new test is proven able to fail before it is committed.

| Test | Asserts |
|---|---|
| **Every packable project carries a licence, an author and a project URL** | Over the `.csproj` files and `Directory.Build.props` together, since the properties may live in either. Stripped of each in turn, it must fail and name the project |
| **The push step names its packages** | Reads `release.yml` and fails on a `*.nupkg` glob in the push. The guard on publishing `ThemeAudit` by accident, which cannot be undone |
| The workflow resolves the version from the tag | `VERSION=${GITHUB_REF_NAME#v}` reaches both `build` and `pack`. A pack without it publishes `1.0.0` |
| The packed nuspec carries what it should | Over a real `dotnet pack` into a temporary directory: id, licence, authors, project URL, the dependency on the Core id, eight satellites, the XML documentation |
| `theme-audit`, `gen-strings` and the locale packet still gate | Unchanged. This plan touches metadata and CI, and a regression anywhere else means it touched more than it meant to |

## Risks

| Risk | Assessment |
|---|---|
| **An accidental `ThemeAudit` publish is permanent** | The sharpest risk here, and the reason a YAML assertion earns its place. Explicit package names, plus a test that the glob never comes back |
| **The Trusted Publishing policy is filename-coupled** | Renaming `release.yml` breaks publishing with an authentication error that does not mention filenames. A comment at the top of the file is the whole mitigation, and it is worth more than it looks |
| **Nothing can be tested end to end** | No remote means the workflow is verified by reading and by unit-level assertions, not by running. Named plainly: the first real run will be the first real run, and `workflow_dispatch` exists so it need not also be a tag push |
| `--skip-duplicate` hides a re-push | Kept, because it makes a re-run safe, and stated so nobody reads a green job as proof that a version was published |
| **The tag and the assembly caldate can disagree** | Inherent to the scheme rather than introduced here. Documented at the point of use |
| The version scheme is not uniform upstream | `FileServer` is `2026.9.2` and has no quarter. DiffView follows `AutoVersioning` and `Chisel`; if the intent was `YYYY.M.D` all along, this is the plan that finds out |
| MIT over a repository with adapted third-party code | `THIRD-PARTY-NOTICES.md` already records what was read or adapted and from where. MIT over the whole is the same position ClaudeForge takes |

## Conventions

Conventional Commits, dense bodies, no AI attribution trailer. New tests are proven able to fail
before they are committed. An approved plan is committed before implementation and never edited;
drift goes to `DECISIONS.md`. Work on `chore/the-release`, branched from `main` at `3ae46d8`; a
completed branch merges without asking once it is proven stable. **Nothing in this plan publishes.**
