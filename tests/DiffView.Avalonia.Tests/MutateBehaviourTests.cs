using System.Diagnostics;

namespace Bennewitz.Ninja.DiffView.Tests;

/// <summary>
/// <c>scripts/mutate-behaviour</c> proves a behaviour test able to fail: it breaks what the test
/// guards and checks that it goes red, and makes a change that should not matter and checks that it
/// stays green. Running a mutation needs several minutes and a rebuild, so what is tested here is the
/// half that decides the verdict — how it reads a run, and how it reads a set before running one —
/// which is where a harness is wrong in silence.
/// </summary>
/// <remarks>
/// <para>
/// Driven through its real command line, as <see cref="CatchCrashTests"/> and
/// <see cref="DriveDemoTests"/> drive theirs: what runs here is what a developer runs.
/// </para>
/// <para>
/// ⛔ The reason this is worth a test at all is <c>aborted.log</c>. A test host that dies mid-run
/// prints <c>Failed!</c> with <c>failed: 0</c> and a total short of the suite, so every reading built
/// on counts calls it healthy — the defect <c>scripts/catch-crash</c> exists for (<c>AGENTS.md</c>
/// §5). A harness that called that green would report a mutation as surviving, and one that called
/// it a kill would report a test as guarding what no test ever failed on.
/// </para>
/// </remarks>
public sealed class MutateBehaviourTests
{
    /// <summary>The one test <c>killed.log</c> records failing.</summary>
    private const string KilledTest = "A_revert_after_a_same_line_edit_leaves_each_fold_over_the_lines_it_covered";

    private static string Fixture(string name) =>
        RepoPaths.Source(Path.Combine("fixtures", "mutations", name));

    /// <summary>A fixture as a set names it: relative to the repository, with <c>/</c>.</summary>
    private static string SetFixture(string name) => $"fixtures/mutations/{name}";

    [Fact]
    public void A_run_that_went_red_is_read_as_red_and_says_where_its_trace_stopped()
    {
        (int exitCode, string output) = Run(["--judge", Fixture("killed.log"), "red"]);

        Assert.True(exitCode == 0, output);
        Assert.Contains("red", output, StringComparison.Ordinal);

        // Both frames, in order: the assertion helper and the test line it was reached from. Which
        // stage of a staged test failed is the whole discrimination a mutation is judged on.
        Assert.Contains("98", output, StringComparison.Ordinal);
        Assert.Contains("346", output, StringComparison.Ordinal);
    }

    [Fact]
    public void A_run_that_stayed_green_is_read_as_green()
    {
        (int exitCode, string output) = Run(["--judge", Fixture("survived.log"), "green"]);

        Assert.True(exitCode == 0, output);
        Assert.Contains("green", output, StringComparison.Ordinal);
    }

    [Fact]
    public void A_reading_that_disagrees_with_the_declaration_fails()
    {
        // The whole value of the harness: `Expect` is compared, never printed. A mutation declared
        // green that goes red is as much a failure as one declared red that survives.
        (int exitCode, string output) = Run(["--judge", Fixture("survived.log"), "red"]);

        Assert.Equal(1, exitCode);
        Assert.Contains("green", output, StringComparison.Ordinal);
    }

    [Fact]
    public void An_aborted_host_is_not_read_as_green()
    {
        // `Failed!` with `failed: 0` and a short total. No arithmetic over the counts can see this —
        // 4 + 0 + 0 closes perfectly — so the outcome line is what the reading rests on.
        (int exitCode, string output) = Run(["--judge", Fixture("aborted.log"), "green"]);

        Assert.True(exitCode == 1, "an aborted host was read as green: " + output);
        Assert.Contains("red", output, StringComparison.Ordinal);
    }

    [Fact]
    public void An_aborted_host_is_not_read_as_a_kill_either()
    {
        // ⛔ The mirror of the test above, and the case a reading of "not Passed!" alone gets wrong: a
        // host that died failed no test, so a mutation declared red is not shown to be guarded by one.
        (int exitCode, string output) = Run(["--judge", Fixture("aborted.log"), "red"]);

        Assert.True(exitCode == 1, "an aborted host was read as a kill: " + output);
        Assert.Contains("aborted", output, StringComparison.Ordinal);
    }

    [Fact]
    public void A_kill_by_the_declared_test_in_the_declared_words_is_as_declared()
    {
        (int exitCode, string output) = Run(
            ["--judge", Fixture("killed.log"), "red", "--killer", KilledTest, "--says", "17.554062499999997"]);

        Assert.True(exitCode == 0, output);
        Assert.Contains(KilledTest, output, StringComparison.Ordinal);
    }

    [Fact]
    public void A_kill_by_some_other_test_is_a_wrong_killer()
    {
        // A red run proves the mutation broke something; only the declared test failing proves it
        // broke what that test guards.
        (int exitCode, string output) = Run(
            ["--judge", Fixture("killed.log"), "red", "--killer", "Some_test_that_never_ran"]);

        Assert.Equal(1, exitCode);
        Assert.Contains("wrong-killer", output, StringComparison.Ordinal);
    }

    [Fact]
    public void The_declared_test_failing_in_other_words_is_a_wrong_assertion()
    {
        (int exitCode, string output) = Run(
            ["--judge", Fixture("killed.log"), "red", "--killer", KilledTest, "--says", "words no assertion said"]);

        Assert.Equal(1, exitCode);
        Assert.Contains("wrong-assertion", output, StringComparison.Ordinal);
    }

    [Fact]
    public void An_edit_that_changed_only_the_file_it_declared_has_strayed_nowhere()
    {
        (int exitCode, string output) = Run(
        [
            "--strayed",
            Fixture("status-before.txt"),
            Fixture("status-after-clean.txt"),
            "src/DiffView.Avalonia/DiffBuildController.cs",
        ]);

        // The untracked draft is in both readings, so it is not a stray — only what APPEARED is.
        Assert.True(exitCode == 0, output);
        Assert.Contains("nothing strayed", output, StringComparison.Ordinal);
    }

    [Fact]
    public void An_edit_that_wrote_outside_the_file_it_declared_is_caught_and_named()
    {
        (int exitCode, string output) = Run(
        [
            "--strayed",
            Fixture("status-before.txt"),
            Fixture("status-after-stray.txt"),
            "src/DiffView.Avalonia/DiffBuildController.cs",
        ]);

        // This is the guard the harness exists to add over the one-off it came from. Without it a
        // write outside the declared file reads as a no-op, survives the revert that cannot reach
        // it, and contaminates every mutation after it.
        Assert.Equal(1, exitCode);
        Assert.Contains("SideBySideDiffView.cs", output, StringComparison.Ordinal);
        Assert.Contains("scratch-output.txt", output, StringComparison.Ordinal);
        Assert.DoesNotContain("DiffBuildController.cs", output, StringComparison.Ordinal);
    }

    [Fact]
    public void A_narrowed_list_is_narrowed()
    {
        // `--list` used to print from inside the argument loop and return, so `--only` had not been
        // read yet and a narrowed list silently showed everything. A filter that does nothing is
        // worse than one that errors, because its output looks like an answer.
        (int exitCode, string output) = Run(["--only", "refold", "--list"]);

        Assert.True(exitCode == 0, output);
        Assert.Equal(1, output.Split('\n').Count(l => l.Contains("expects:", StringComparison.Ordinal)));
        Assert.Contains("refold", output, StringComparison.Ordinal);
    }

    [Fact]
    public void Every_mutation_declares_the_file_it_may_touch_and_why_it_expects_what_it_does()
    {
        (int exitCode, string output) = Run(["--list"]);

        Assert.True(exitCode == 0, output);

        // A mutation with no stated reason is one nobody can judge a year from now, and the file it
        // names is what stops a stray edit being counted as a result.
        string[] lines = output.Split('\n', StringSplitOptions.RemoveEmptyEntries);
        int named = lines.Count(l => l.Contains("expects:", StringComparison.Ordinal));
        Assert.True(named >= 3, "the harness names fewer mutations than it carries: " + output);
        Assert.All(
            lines.Where(l => l.Contains("expects:", StringComparison.Ordinal)),
            line => Assert.Contains(" — ", line, StringComparison.Ordinal));
    }

    [Fact]
    public void A_committed_set_is_checked_without_building_or_running_anything()
    {
        (int exitCode, string output) = Run(["--set", SetFixture("set-clean.json"), "--check"]);

        // Five declared tests, not six: the reader takes a test under each attribute the fixture
        // carries — the theory whose data holds a bracket of its own among them, which a reader that
        // matched brackets lost — and leaves the public method that is not a test.
        Assert.True(exitCode == 0, output);
        Assert.Contains("2 mutation(s) over 5 declared test(s), 0 fault(s)", output, StringComparison.Ordinal);
        Assert.DoesNotContain("baseline", output, StringComparison.Ordinal);
    }

    [Fact]
    public void A_set_lists_the_killer_each_mutation_names_and_why_a_green_one_should_stay_green()
    {
        (int exitCode, string output) = Run(["--set", SetFixture("set-clean.json"), "--list"]);

        Assert.True(exitCode == 0, output);
        Assert.Contains("red, killed by A_plain_fact", output, StringComparison.Ordinal);
        Assert.Contains("green — the subject is read by no test", output, StringComparison.Ordinal);
    }

    [Fact]
    public void A_listed_mutation_says_what_its_run_filters_to_and_whether_its_build_restores()
    {
        (int exitCode, string output) = Run(["--set", SetFixture("set-filters.json"), "--list"]);

        // Narrowing a run to its set's test classes is a trim, taken only where it cannot pass a mutation:
        // a red one naming its killer, which those classes declare. A green one and a red one naming no
        // killer keep everything, or narrowing could hide the failure that should have reddened them. And
        // only an edit to what restore reads costs its build a restore.
        Assert.True(exitCode == 0, output);
        Dictionary<string, string> runs = ListedRuns(output);
        Assert.Equal("SampleTests, built without a restore", runs["a red mutation naming its killer"]);
        Assert.Equal("the whole project, built without a restore", runs["a green mutation"]);
        Assert.Equal("the whole project, built without a restore", runs["a red mutation naming no killer"]);
        Assert.Equal(
            "SampleTests, restored before it builds, the edit being to what restore reads",
            runs["an edit to what restore reads"]);
    }

    [Fact]
    public void A_set_whose_edits_do_not_apply_once_or_name_a_stranger_is_refused_before_anything_runs()
    {
        (int exitCode, string output) = Run(["--set", SetFixture("set-faulty.json"), "--check"]);

        // Each fault named, not merely counted: a run that refuses has to say what to fix. Not_a_test
        // is a public method of the declared file and no test, so naming it as a killer is a fault.
        Assert.Equal(1, exitCode);
        Assert.Contains("\"an edit that applies twice\" matches 2 times", output, StringComparison.Ordinal);
        Assert.Contains("\"an edit that applies nowhere\" matches 0 times", output, StringComparison.Ordinal);
        Assert.Contains("do not declare: Not_a_test", output, StringComparison.Ordinal);
    }

    [Fact]
    public void A_set_with_red_mutations_and_no_green_one_is_refused_before_anything_runs()
    {
        (int exitCode, string output) = Run(["--set", SetFixture("set-no-green.json"), "--check"]);

        // A green run is the only one in which a test that fails under every mutation shows: under each
        // red one it is merely among the failures, and the mutation naming it reads as its kill.
        Assert.Equal(1, exitCode);
        Assert.Contains("holds red mutations and no green mutation", output, StringComparison.Ordinal);
    }

    [Fact]
    public void A_shard_dealt_no_green_mutation_is_not_refused_for_it()
    {
        (int exitCode, string output) = Run(["--set", SetFixture("set-clean.json"), "--shard", "1/2", "--check"]);

        // set-clean's red mutation is its first and its green one its second, so shard 1 of 2 holds the
        // red alone. The rule is the set's, and the set carries its control.
        Assert.True(exitCode == 0, output);
        Assert.DoesNotContain("no green mutation", output, StringComparison.Ordinal);
    }

    [Fact]
    public void Every_build_and_test_run_the_harness_starts_has_Avalonias_telemetry_off()
    {
        (int exitCode, string output) = Run(["--set", SetFixture("set-clean.json"), "--check"]);

        // The collector Avalonia's build task starts before each compile kept a worktree for three quarters
        // of an hour after its build. Read off the dry run, which names what every build would be given.
        Assert.True(exitCode == 0, output);
        Assert.Contains("every build and test run is given AVALONIA_TELEMETRY_OPTOUT=1", output, StringComparison.Ordinal);
    }

    [Fact]
    public void A_mutation_that_names_neither_its_killer_nor_why_is_refused()
    {
        (int exitCode, string output) = Run(["--set", SetFixture("set-unreasoned.json"), "--list"]);

        Assert.Equal(2, exitCode);
        Assert.Contains("names neither its killer nor why", output, StringComparison.Ordinal);
    }

    [Fact]
    public void A_set_with_a_key_the_harness_does_not_know_is_refused_rather_than_ignored()
    {
        // ⛔ The fixture carries a reason, so nothing else refuses it: ignored, "kiler" would leave the
        // killer unchecked, and the mutation would be judged against a declaration it never made.
        (int exitCode, string output) = Run(["--set", SetFixture("set-misspelt.json"), "--list"]);

        Assert.Equal(2, exitCode);
        Assert.Contains("kiler", output, StringComparison.Ordinal);
    }

    [Fact]
    public void A_test_marked_inert_at_a_pin_its_set_still_holds_is_read_as_marked()
    {
        (int exitCode, string output) = Run(["--set", SetFixture("set-marked.json"), "--check"]);

        // The set names a pins file of its own, so the fixture leans on no package this repository
        // pins — judged against Directory.Packages.props instead, which pins no Fixture.Package, the
        // marker would read as expired.
        Assert.True(exitCode == 0, output);
        Assert.Contains("2 mutation(s) over 2 declared test(s) (1 marked inert), 0 fault(s)", output, StringComparison.Ordinal);
    }

    [Fact]
    public void A_marker_whose_pin_moved_or_that_no_test_holds_is_refused_before_anything_runs()
    {
        (int exitCode, string output) = Run(["--set", SetFixture("set-stale-markers.json"), "--check"]);

        // ⛔ Both are faults of the set, not of a run: a moved pin is exactly the change that lands
        // green while the test it excused stops being excused, and a marker in no test excuses nothing
        // — most likely because it sits in a test the reader did not see.
        Assert.Equal(1, exitCode);
        Assert.Contains(
            "A_test_whose_pin_moved is marked inert at Fixture.Package 0.9.0, and the pins hold it at 1.0.0",
            output,
            StringComparison.Ordinal);
        Assert.Contains("fixtures/mutations/stale-markers.txt:15 holds a marker no declared test does", output, StringComparison.Ordinal);
    }

    [Fact]
    public void A_declared_test_no_mutation_killed_fails_the_judgement()
    {
        (int exitCode, string output) = Run(
            ["--set", SetFixture("set-marked.json"), "--kills", SetFixture("kills-none.json")]);

        Assert.Equal(1, exitCode);
        Assert.Contains(
            "0 killed by a mutation, 1 inert at the current pin, 1 killed by none, 0 with a bad marker",
            output,
            StringComparison.Ordinal);
    }

    [Fact]
    public void A_test_marked_inert_and_killed_by_none_is_excused()
    {
        (int exitCode, string output) = Run(
            ["--set", SetFixture("set-marked.json"), "--kills", SetFixture("kills-all-but-the-inert.json")]);

        Assert.True(exitCode == 0, output);
        Assert.Contains(
            "1 killed by a mutation, 1 inert at the current pin, 0 killed by none, 0 with a bad marker",
            output,
            StringComparison.Ordinal);
    }

    [Fact]
    public void A_test_marked_inert_that_a_mutation_killed_anyway_fails_the_judgement_and_names_the_mutation()
    {
        (int exitCode, string output) = Run(
            ["--set", SetFixture("set-marked.json"), "--kills", SetFixture("kills-the-inert-too.json")]);

        // The marker claimed no mutation could reach the test, and one did: the claim is the defect,
        // and the run says which mutation disproved it so the marker can come off with a reason.
        Assert.Equal(1, exitCode);
        Assert.Contains(
            "A_test_inert_at_its_pin, marked inert at Fixture.Package 1.0.0, killed by \"the one place changes\"",
            output,
            StringComparison.Ordinal);
    }

    [Fact]
    public void A_test_file_in_which_the_reader_finds_no_test_is_refused()
    {
        (int exitCode, string output) = Run(["--set", SetFixture("set-barren.json"), "--check"]);

        // ⛔ The positive control on the reading: with nothing read, no test is killed by none, and
        // every judgement made of the file's tests would hold.
        Assert.Equal(1, exitCode);
        Assert.Contains("fixtures/mutations/subject.txt declares no test the reader can see", output, StringComparison.Ordinal);
    }

    [Fact]
    public void Recorded_kills_with_no_set_to_judge_them_against_are_refused()
    {
        (int exitCode, string output) = Run(["--kills", SetFixture("kills-none.json")]);

        // The built-in set names no test file, so a judgement of its tests would judge none and hold.
        Assert.Equal(2, exitCode);
        Assert.Contains("this set names no test file", output, StringComparison.Ordinal);
    }

    [Fact]
    public void A_shard_is_every_nth_mutation_from_its_own_place()
    {
        (int exitCode, string output) = Run(["--set", SetFixture("set-shards.json"), "--shard", "2/2", "--list"]);

        // Dealt round, not cut into runs: the second and the fourth of five, so a set's slow mutations,
        // which sit together, land in different shards.
        Assert.True(exitCode == 0, output);
        Assert.Equal(["two", "four"], ListedNames(output));
    }

    [Fact]
    public void Flags_that_contradict_each_other_are_refused_rather_than_resolved()
    {
        (int exitCode, string output) = Run(["--parallel", "2", "--merge", SetFixture("shard-1-of-2.tsv")]);

        // A precedence between the two would be a guess at what was meant, and its output would look
        // like an answer to the question nobody asked.
        Assert.Equal(2, exitCode);
        Assert.Contains("--parallel runs every mutation of each set", output, StringComparison.Ordinal);
    }

    [Fact]
    public void A_merge_of_every_shard_that_ran_over_other_code_is_refused()
    {
        (int exitCode, string output) = Run(
        [
            "--set", SetFixture("set-shards.json"),
            "--merge", SetFixture("shard-1-of-2.tsv"),
            "--merge", SetFixture("shard-2-of-2.tsv"),
        ]);

        // ⛔ Every mutation once, from shards 1 and 2 of 2 — and over a tree no checkout has, so the
        // verdicts are about code that is not this code.
        Assert.Equal(2, exitCode);
        Assert.Contains("the shards ran over tree 0000000000000000000000000000000000000000, and this checkout is tree", output, StringComparison.Ordinal);
    }

    [Fact]
    public void A_merge_short_of_a_mutation_is_refused_and_names_it()
    {
        (int exitCode, string output) = Run(
        [
            "--set", SetFixture("set-shards.json"),
            "--merge", SetFixture("shard-1-of-2.tsv"),
            "--merge", SetFixture("shard-2-of-2-short.tsv"),
        ]);

        // A merge of a partial set would judge only what it reached and present that as the whole.
        Assert.Equal(2, exitCode);
        Assert.Contains("missing: four", output, StringComparison.Ordinal);
    }

    [Fact]
    public void A_shard_given_twice_is_refused()
    {
        (int exitCode, string output) = Run(
        [
            "--set", SetFixture("set-shards.json"),
            "--merge", SetFixture("shard-1-of-2.tsv"),
            "--merge", SetFixture("shard-1-of-2.tsv"),
        ]);

        Assert.Equal(2, exitCode);
        Assert.Contains("shard 1/2 is given twice", output, StringComparison.Ordinal);
    }

    [Fact]
    public void Only_the_sets_a_change_touches_are_owed_a_run()
    {
        (int exitCode, string output) = Run(
        [
            "--set", SetFixture("set-clean.json"),
            "--set", SetFixture("set-marked.json"),
            "--owed-by", SetFixture("changed-marked-tests.txt"),
            "--list",
        ]);

        // The change names set-marked's test file and nothing set-clean reads.
        Assert.True(exitCode == 0, output);
        Assert.Contains("owed      fixtures/mutations/set-marked.json — fixtures/mutations/marked-tests.txt changed", output, StringComparison.Ordinal);
        Assert.Contains("not owed  fixtures/mutations/set-clean.json", output, StringComparison.Ordinal);
    }

    [Fact]
    public void A_dry_run_of_a_set_that_edits_the_script_running_it_says_a_run_would_need_a_copy()
    {
        (int exitCode, string output) = Run(["--set", SetFixture("set-edits-its-runner.json"), "--check"]);

        // A set editing scripts/mutate-behaviour.cs, checked from that script: its tests would rebuild
        // the binary running the check. Noted here, refused by a run, and nothing to do with --parallel,
        // whose shards run from the binary that started them. Not the harness's own set: while one of its
        // mutations is in place another of its edits no longer applies, so the check fails and this test
        // would be killed by every mutation of that set.
        Assert.True(exitCode == 0, output);
        Assert.Contains("the script this harness is running from", output, StringComparison.Ordinal);
    }

    [Fact]
    public void A_run_of_a_set_that_edits_the_script_running_it_is_refused_before_anything_builds()
    {
        (int exitCode, string output) = Run(["--set", SetFixture("set-edits-its-runner.json")]);

        // The fixture's project does not exist, so a run that got past the refusal would fail its first
        // build with 1, having edited nothing.
        Assert.True(exitCode == 2, output);
        Assert.Contains("the script this harness is running from", output, StringComparison.Ordinal);
    }

    [Fact]
    public void Several_sets_run_one_after_another_each_judged_on_its_own()
    {
        (int exitCode, string output) = Run(
            ["--set", SetFixture("set-clean.json"), "--set", SetFixture("set-faulty.json"), "--check"]);

        Assert.Equal(1, exitCode);
        Assert.Contains("2 set(s), 1 not as declared", output, StringComparison.Ordinal);
    }

    /// <summary>The names <c>--list</c> printed, each on a line of its own, indented by two spaces and no more.</summary>
    private static string[] ListedNames(string output) =>
        [.. output.Split('\n')
            .Select(line => line.TrimEnd('\r'))
            .Where(line => line.StartsWith("  ", StringComparison.Ordinal) && !line.StartsWith("   ", StringComparison.Ordinal))
            .Select(line => line.Trim())];

    /// <summary>Each listed mutation's <c>runs:</c> line, by the name it is listed under.</summary>
    private static Dictionary<string, string> ListedRuns(string output)
    {
        Dictionary<string, string> runs = new(StringComparer.Ordinal);
        string? name = null;
        foreach (string line in output.Split('\n').Select(line => line.TrimEnd('\r')))
        {
            if (line.StartsWith("  ", StringComparison.Ordinal) && !line.StartsWith("   ", StringComparison.Ordinal))
            {
                name = line.Trim();
            }
            else if (name is not null && line.StartsWith("      runs: ", StringComparison.Ordinal))
            {
                runs[name] = line["      runs: ".Length..];
            }
        }

        return runs;
    }

    private static (int ExitCode, string Output) Run(string[] arguments)
    {
        ProcessStartInfo start = new()
        {
            FileName = "dotnet",
            WorkingDirectory = RepoPaths.Root,
            RedirectStandardOutput = true,
            RedirectStandardError = true,
        };

        start.ArgumentList.Add("run");
        start.ArgumentList.Add(Path.Combine("scripts", "mutate-behaviour.cs"));
        start.ArgumentList.Add("--");
        foreach (string argument in arguments)
        {
            start.ArgumentList.Add(argument);
        }

        using Process process = Process.Start(start)
            ?? throw new InvalidOperationException("Could not start dotnet run.");

        // Both pipes drained concurrently: stdout to EOF first deadlocks once the child fills the
        // stderr pipe buffer, and the test then hangs rather than failing.
        Task<string> stdout = process.StandardOutput.ReadToEndAsync();
        Task<string> stderr = process.StandardError.ReadToEndAsync();
        process.WaitForExit();
        return (process.ExitCode, stdout.GetAwaiter().GetResult() + stderr.GetAwaiter().GetResult());
    }
}
