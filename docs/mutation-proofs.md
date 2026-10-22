# Proving tests able to fail

A passing test is evidence only if breaking what it guards turns it red. This repository proves that for
its behaviour tests by **declared mutations**: each is one literal edit to the code, committed beside
the tests it proves, and each says what must happen to the run — which test must fail, in which words,
or that the run must stay green and why. `scripts/mutate-behaviour.cs` applies them one at a time and
compares every run against its declaration. `scripts/mutate-gates.cs` is its sibling for the
static-analysis gates, judged by which assertion fails first; `AGENTS.md` §5 holds the rules for both.

## A set

A set is a JSON file beside the tests it proves, `tests/<project>/Mutations/<subject>.json`:

```json
{
  "project": "tests/AgentBaseline.Tests/AgentBaseline.Tests.csproj",
  "tests": [
    "tests/AgentBaseline.Tests/AccumulatorEditTests.cs",
    "tests/AgentBaseline.Tests/SectionIdentityTests.cs",
    "tests/AgentBaseline.Tests/SectionDecisionTests.cs"
  ],
  "mutations": [
    {
      "name": "a take is placed by the diff's alignment, as draft 1's CopyToLeft was",
      "file": "src/AgentBaseline/ReviewSession.cs",
      "old": "left.Replace(edit.Offset, edit.Length, edit.Text);",
      "new": "view.CopyBlock(view.Document!.Blocks.Select((b, i) => (b, i)).First(p => p.b.LinesFor(DiffSide.Right).Start <= section.FirstLine - 1 && section.FirstLine - 1 < p.b.LinesFor(DiffSide.Right).Start + p.b.LinesFor(DiffSide.Right).Count).i, DiffSide.Left);",
      "killer": "Taking_a_section_from_a_reordered_candidate_leaves_every_other_section_of_the_accumulator_byte_identical",
      "says": "is not byte-identical after the take",
      "because": "decision 5: a copy over the aligned range writes the candidate's Two over the accumulator's Three, which the misaligned fixture is built to show"
    }
  ]
}
```

That is the first of the fifty-one in `tests/AgentBaseline.Tests/Mutations/section-decisions.json`, the
proof of plan 00034 step 6's negative test.

| Key | Meaning |
|---|---|
| `project` | The test project every run builds and tests |
| `class` | A test class every run is filtered to; omitted, a run takes the whole project, or the classes of `tests` for a red mutation naming its killer |
| `tests` | Test files whose every test some mutation must kill |
| `pins` | The props file `inert-at-pin` markers are judged against; omitted, `Directory.Packages.props` |
| `name` | What is broken, in the words of the contract it breaks |
| `file`, `old`, `new` | The one file the edit may touch, a literal that occurs there exactly once, and its replacement |
| `expect` | `red`, the default, or `green` |
| `killer` | For a red mutation, the test that must be among the failures |
| `says` | Text the run must contain — the declared assertion's own words |
| `because` | Why the expectation is right; required when no killer is named |

A key the format does not know is refused, not skipped: `"kiler"` read as nothing would drop the very
check the mutation was written to make.

## What a run judges

Every mutation's run gets one outcome, and every outcome but `ok` fails the run:

| Outcome | What happened |
|---|---|
| `ok` | The run went the way the mutation declared |
| `survived` | Declared red, and every test passed |
| `wrong-killer` | Red, but not the test the mutation names |
| `wrong-assertion` | The named test failed, without the declared words |
| `aborted` | Red with no test failed by name — a test host that died, which a reading of "not `Passed!`" alone calls a kill |
| `unexpected-red` | Declared green, and something failed |
| `no-op` | The edit matched nothing: the code moved, and the mutation needs re-pointing |
| `build` | The mutation does not compile |

A set that names test files is also held to every test in them: a run fails while any declared test was
killed by no mutation, because a test nothing can break proves nothing. A test that asks about a
dependency rather than about our code carries `// inert-at-pin: <package> <version>` in its body and is
excused while the set's pins hold that version. A moved pin, a marker in no test the reader found and a
test file in which it finds no test are setup faults; a marked test some mutation killed fails the run,
naming that mutation.

## What keeps a run honest

- **The clean tree must pass first**, so a test that was already red cannot be read as killed.
- **Each edit is reverted from a snapshot** of the file's bytes, written back and compared, so a set
  runs over uncommitted work — the point being to prove a test before it is committed.
- **What a mutation may touch is derived, not trusted**: the repository's whole status, with a hash of
  every file it lists, is compared before and after each edit, and a write anywhere else stops the run.
- **The project is rebuilt on every exit**, because reverting a mutation does not unbuild it.
- **One harness to a checkout.** Each one's stray guard reads the whole tree, so two in one checkout
  stop each other; `--parallel` is the way to run more than one at once.
- **The harness's own set never runs from the script it mutates**: its tests rebuild that script's
  binary while it runs. A dry run notes it, a run refuses it, and `--parallel` needs no copy, its shards
  running from the binary that started them.
- **A green mutation is a set's positive control.** It is the one run that must pass, and so the only
  one in which a test that fails under *every* mutation shows: under a red one that test is merely among
  the failures, and a mutation naming it as the killer reads as proving it. One of the harness's own
  tests checked the harness's own set, whose edits stop applying while one of them is in place, so it
  failed under every mutation of that set and its own proof proved nothing; the set's one green mutation
  going red is what said so. The results a `--parallel` run keeps record every kill, which is where the
  same symptom can be read in a set that has no green mutation. Every set in this repository carries
  one, an edit equivalent by a stated law — addition or `Math.Max` commuting, an interpolation as the
  concatenation it is — in the file most of its mutations edit. By a law, because an edit equivalent
  only for the inputs the tests happen to use is a surviving mutation that its declaration excuses. The
  harness refuses a set holding red mutations and no green one, judged on the whole set rather than on
  the part a shard or `--only` chose.

## Running fewer, and running them at once

| Command | What it does |
|---|---|
| `--set <file> --list` | Every mutation, what it expects, and what its run filters to and builds |
| `--set <file> --check` | Every edit applies once and every killer is declared; runs nothing |
| `--set <a> --set <b>` | Several sets, one after another, each judged on its own |
| `--owed <ref>` | Only the sets a change owes: one whose own file, pins, test files or a file one of its mutations edits changed since the ref, committed or not — among those named, or else every set under `tests/*/Mutations/` |
| `--parallel <n>` | Every mutation of each set in `n` worktrees at once, judged as one run per set |
| `--shard k/n --emit <file>` | One shard's share — every `n`th mutation from the `k`th — written down for a merge |
| `--merge <file> …` | Shards' results judged as one run of the set |

⚠ `--owed` reads the files a set names, not what its tests depend on: a change to code no mutation of a
set edits can still change what its tests see, so it narrows the runs between full ones and replaces
none.

`--parallel` writes this checkout's state, uncommitted work included, as a git tree through a temporary
index — no commit is made and no ref moves — cuts `n` detached worktrees from `HEAD`, loads that tree
into each, and checks each holds it before any build. Each shard reports the tree it ran over, and the
merge refuses anything but every declared mutation exactly once from shards of one `n` over that one
tree. It never edits the checkout it was started in. Every build and test run the harness starts is
given `AVALONIA_TELEMETRY_OPTOUT=1`, because the collector Avalonia's build task starts before each
compile once kept a worktree for three quarters of an hour after its build; anything else a build starts
that holds a worktree, the harness names, with how to remove it.

Two trims make each mutation cheaper, each measured before it was made: a mutation's build skips the
restore the baseline already did, and a mutation that names its killer runs only the classes of the
set's test files. A green mutation, and a red one with no killer, keep the set's whole filter, so
narrowing can relabel a failure but never turn one into a pass. `--list` prints what each run filters to
and whether its build restores.

Measured on a Windows box with sixteen logical CPUs, over the five sets that prove the AgentBaseline
library — the same 176 mutations each time:

| Run | Minutes |
|---|---|
| One at a time, in the checkout, before either trim | 40.2 |
| One worktree, with both trims | 32.8 |
| Four worktrees, with both trims | 16.5 |

Every run's verdicts were the first's, mutation for mutation. The trims are worth about a fifth, and
four worktrees run about twice as fast as one rather than four times, each shard's builds sharing the
CPU with the others'. An earlier four-worktree run was through the same five sets in about 14 minutes,
inside a run of all six, so the box's own load moves these figures by minutes. The harness's own set,
whose tests mostly start processes rather than build, shares it better: at 39 mutations, 23.6 minutes in
one worktree and 8.8 in four.

## Taking it to another repository

The method is not specific to this repository; five things in the script are:

- `Repo.Root` finds the repository by `DiffView.slnx`;
- `Source` carries untracked files from `src`, `tests`, `scripts`, `fixtures` and this repository's root
  build files;
- `Judge` reads the output of Microsoft.Testing.Platform, and the class filter is xUnit v3's
  `--filter-class`;
- `Dotnet.BuildEnvironment` turns Avalonia's build telemetry off, which a repository with no Avalonia
  build task need not;
- the built-in set at the foot of the file is the library's own scenario.

`Bennewitz.Ninja.XamlQuality` has a runner of its own for the same idea, `tools/mutate.cs`: a catalog of
defects, each held to the exact set of tests that must fail, run in copies of the working tree that
rebuild only what a defect touches and swap the rebuilt assembly into the test output, and a refusal to
start a run estimated past thirty minutes. Each has what the other lacks — that one the exact failing
set, the assembly swap and the estimate; this one the per-set completeness, the `inert-at-pin` markers,
green controls with their reasons, the outcomes that look like a kill and are not, and owed-only runs.
