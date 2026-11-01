# AGENTS.md — `tests/`

| Project | What it tests |
|---|---|
| `DiffView.Core.Tests` | The diff model |
| `DiffView.Avalonia.Tests` | The controls headless, the demo, the gates over both shipped assemblies, the documents and the scripts |
| `AgentBaseline.Tests` | The consolidation tool, its window headless |

`Directory.Build.props` here filters the Linux-only frame baselines out away from Linux
(`ExcludeLinuxBaselines`). The rules the suite keeps are the root `AGENTS.md`'s §5.

## Rules

| Rule | Why | Guarded by |
|---|---|---|
| A run is judged by its summary's outcome, never by its counts: `scripts/catch-crash --check <log> --expect auto` | A host that dies mid-run prints `Failed!` with `failed: 0` and a short total, which reads as a pass | `CatchCrashTests.An_aborted_run_reporting_zero_failures_is_not_healthy` |
| A test that compares a frame carries `[LinuxBaseline]`, and a test asserting English text carries `[EnglishChrome]` on the method | A frame is Linux's rendering, and CI runs the whole suite in German too | root `AGENTS.md` §5 |
| A behaviour test is proven able to fail from a committed set, `<project>/Mutations/<subject>.json`, and every set carries a green control | A green run is the only one in which a test failing under every mutation shows | `MutateBehaviourTests.A_set_with_red_mutations_and_no_green_one_is_refused_before_anything_runs` |
| ⛔ `DiffView.Avalonia.Tests/Mutations/mutate-behaviour.json` never runs from `scripts/mutate-behaviour.cs` itself | Its tests run that file, and Windows locks a running binary | root `AGENTS.md` §5 |
| A build is awaited, never pumped for: `CompositeHost.WaitForBuildAsync` | One layout pass lands a build only when the thread pool finished first: green on a fast desk, red on a slow runner | root `AGENTS.md` §5 |
| A test that opens a window of its own puts every view in it on the hand-advanced clock before the window shows | A build landing after the close arms an auto-clear on the real clock that nothing disposes, and the host aborts mid-suite when it comes due on the pool thread running the UI loop | root `AGENTS.md` §5 |
