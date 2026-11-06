# AGENTS.md — `src/`

| Project | What it is | Packs as |
|---|---|---|
| `DiffView.Core` | The UI-free diff model | `Bennewitz.Ninja.DiffView.Core` |
| `DiffView.Avalonia` | The controls — the editor, the viewer and the unified view — over AvaloniaEdit | `Bennewitz.Ninja.DiffView.Avalonia` |
| `DiffView.Demo` | The demo, a host like any other | nothing |
| `AgentBaseline` | Plan 00034's consolidation tool, a host of `SideBySideDiffView` | nothing |

The libraries' contracts are the root `AGENTS.md`'s §1 to §7, and AgentBaseline's its §10.

## Rules

| Rule | Why | Guarded by |
|---|---|---|
| **The two libraries share their internals with their own tests and nothing else.** Each sets `SolutionFriendGrants` false and declares its one grant; the demo and AgentBaseline take the solution's generated grants | A host reaching a library internal fails to compile, so the demo and AgentBaseline are held to the public surface by the compiler and not by discipline | `AssemblyQualityTests.No_library_grants_its_internals_beyond_its_own_tests` (`BNAQ1005`) |
| Every packable project carries a licence, an author, both URLs and tags | Each defaults to something wrong rather than failing | `PackagingTests.Every_packable_project_carries_the_metadata_a_consumer_needs` |
| `VSTHRD101` and `VSTHRD110` fail the build, here, in the tests and in the scripts; the analyzer package's other rules are off | An async lambda given where a void delegate is expected ends the process on a throw, and a task nobody awaits loses its failure | the root `Directory.Build.props`, as the family's template has it, and warnings as errors |
| No namespace authored here carries a segment matching the root namespace of an assembly it references | C# then resolves a framework type to ours, and fails with `CS0234` | `AssemblyQualityTests.No_shipped_namespace_shadows_a_referenced_root` |
| **A received file is never markup.** The Avalonia package compiles and embeds every `**/*.axaml` under a project, and the root `Directory.Build.props` adds `**/*.received.*` to `DefaultItemExcludes`, which that glob honours as the SDK's own globs do | A gate's rejected copy left beside a theme, as `ReferenceAuditTests` leaves one beside a stale compat dictionary, would be compiled and embedded without a word, and the library would pack it | `ReceivedFileTests.A_received_file_beside_the_markup_is_not_handed_to_the_markup_compiler` |
