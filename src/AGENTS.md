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
| **The two libraries share their internals with their own tests and nothing else.** Each sets `SolutionFriendGrants` false and declares its one grant; the demo and AgentBaseline take the solution's generated grants | A host reaching a library internal fails to compile, so the demo and AgentBaseline are held to the public surface by the compiler and not by discipline | each library's `.csproj`; the root `Directory.Build.targets` |
| Every packable project carries a licence, an author, both URLs and tags | Each defaults to something wrong rather than failing | `PackagingTests.Every_packable_project_carries_the_metadata_a_consumer_needs` |
| No namespace authored here carries a segment matching the root namespace of an assembly it references | C# then resolves a framework type to ours, and fails with `CS0234` | `AssemblyQualityTests.No_shipped_namespace_shadows_a_referenced_root` |
| ⚠ Markup is globbed: the Avalonia SDK compiles every `**/*.axaml` under a project, a stray `*.received.axaml` among them | One failed test run could ship a foreign resource dictionary inside the package | `PROGRESS.md`, open item 20 |
