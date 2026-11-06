# AGENTS.md — `.github/`

The workflows and the repository's GitHub settings. What governs `main` is the root `AGENTS.md`'s §8.

| File | What it is |
|---|---|
| `workflows/ci.yml` | On every push and pull request: `Build & Test` on Linux, Windows and macOS, `Culture Leg (de-DE)`, `Trim Check (Release linux-x64)` and `conventions`. Each job fetches the reference sources and packs ClaudeForge's diagnostics into the local feed before it restores, because the demo restores from that feed |
| `workflows/release.yml` | Packs and publishes both packages on a `v*.*.*` tag, at the version the tag names. Dispatched with the preflight box ticked and no version, it reaches the NuGet login and stops |
| `workflows/spike-injected-input.yml` | Open item 16's spike, dispatch-only and shelved: it has never run |
| `repository.json` | What this repository's GitHub settings vary by: the description, the topics, the checks `main` requires, and exemptions. `scripts/repo-conventions.cs` checks it, and its `apply` writes it to GitHub |
| `copilot-instructions.md` | A pointer to the root `AGENTS.md`, for the tools that look here |

## Rules

| Rule | Why | Guarded by |
|---|---|---|
| **A job's name is a required check.** Renaming or removing a job `repository.json` lists under `requiredChecks` changes that list and runs `repo-conventions apply` in the same change | A required check no job reports blocks every pull request, and GitHub never says why | `repo-conventions check`, which fails on a required check no job reports |
| `conventions` joins `requiredChecks` only once it is green, and `apply` runs again then | Required while red, it would block every pull request | `docs/repository-conventions.md` in Bennewitz.Ninja.Templates, *Bringing an existing repository in* |
| The release pushes the packages it names, never a `*.nupkg` glob | A glob publishes whatever the build produced, permanently | `PackagingTests.The_release_workflow_names_the_packages_it_pushes`; `PackagesPushTests.The_file_names_exactly_what_the_solution_packs_and_the_release_publishes` holds what it names to `packages.push` |
| The version is the tag's, carried by `/p:Version=` into the build **and** the pack | `dotnet pack --no-build` reuses the build's output, so a version given to one alone packs the wrong thing silently | `PackagingTests.The_release_workflow_carries_the_tag_version_into_build_and_pack` |
| A preflight publishes nothing: the two steps that reach outside the job carry `if: ${{ !inputs.preflight }}` | An empty `/p:Version=` is MSBuild's route to `1.0.0`, and a published version is permanent | `PackagingTests.A_preflight_dispatch_reaches_the_credentials_and_no_further` |
| Nothing is released from a repository the family's conventions reject: the release runs `repo-conventions check --release` once the local feed is packed and before it builds | A package page links back here, so the repository's own documents are part of what ships | the step itself |
| `release.yml` keeps its file name | nuget.org's trusted-publishing policy names the workflow file, and the OIDC token is bound to it | the file's own header |
| CI runs `scripts/mutate-gates.cs --guards` on Linux | An `inert-at-pin` marker expires when its pin moves, and a pin bump otherwise lands green | the step itself; root `AGENTS.md` §5 |
