# AGENTS.md — `fixtures/`

The inputs the tests and the demo read, compared byte for byte. `.gitattributes` marks the whole tree
`-text`, so git never normalises a line ending here: a file stays exactly as its test expects it, which
for text is LF.

| Path | What it is | Read by |
|---|---|---|
| `api/sidebyside.txt`, `api/viewer.txt` | The editor's and the viewer's reachable public surface | `SurfaceGateTests.SideBySideDiffView_reachable_surface_matches_the_committed_baseline`, `SurfaceGateTests.DiffViewer_reachable_surface_matches_its_allow_list` |
| `automation-ids.txt` | Every `AutomationId` a harness searches for | `AccessibilityCoverageTests.Every_id_a_harness_searches_for_is_the_one_the_fixture_pins`, `HostingGuideTests.The_guides_id_table_names_exactly_the_ids_the_fixture_pins` |
| `fonts/` | DejaVu Sans Mono and its licence: the font every rendered frame is drawn in, linked into both Avalonia test projects | `HeadlessTestApp.Initialize` |
| `small/` | The committed small pair: the input of the Core tests and the presenter tests, and what the demo shows when no file is named | `Fixtures` in `DiffView.Core.Tests`, `PresenterHost`, `DiffView.Demo.csproj` |
| `json/` | A JSON pair for the composite's tests | `CompositeHost` |
| `locales/` | A neutral and a made-up locale's `.resx`, for the parity checks | `LocaleParityTests` |
| `test-runs/` | A healthy test run's log and an aborted one's | `CatchCrashTests` |
| `windows/`, `x11/` | What UI Automation and the X11 tools printed, captured from the real tools | `DriveDemoTests` |
| `mutations/` | The proof harness's own fixtures: sets, pins and test files shaped like a real set's | `MutateBehaviourTests` |

## Rules

| Rule | Why | Guarded by |
|---|---|---|
| A change to a control's public surface made on purpose regenerates its `api/` file in the same change | After the first publish, a member that vanishes with the gate green is the break | `SurfaceGateTests` |
| An `AutomationId` renamed, added or removed edits `automation-ids.txt` in the same change | A renamed id passes every other gate while every harness searching for the old one finds nothing | root `AGENTS.md` §1 |
| ⛔ `mutations/` holds fixtures and never a committed set: `MutateBehaviourTests` reads this directory and the script's built-in set, and no set under `tests/` | A test reading the harness's own set is killed by every mutation of it, and its declared kill proves nothing | root `AGENTS.md` §5 |
