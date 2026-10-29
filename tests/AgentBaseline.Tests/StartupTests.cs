using Avalonia.Headless.XUnit;

namespace Bennewitz.Ninja.AgentBaseline.Tests;

/// <summary>
/// The start-up plan 00034 names in no step, built ahead of step 9 on Brian's word of 2026-10-29: told a
/// corpus, a seed and the decision file an earlier save wrote, it says what the corpus left out and opens
/// the review window on them.
/// </summary>
/// <remarks>
/// ⛔ The platform lifetime <see cref="App.Run"/> starts is the one part no headless test reaches, so
/// <see cref="Program.Run"/> takes the review it runs as a parameter, and everything up to that call is
/// asserted here.
/// </remarks>
public sealed class StartupTests
{
    /// <summary>The seed: One at line 3, Three at 7, under a title at line 1.</summary>
    private const string Seed = "# Rules\n\n## One\n\nAlpha.\n\n## Three\n\nGamma.\n";

    /// <summary>One reworded: one change against the seed.</summary>
    private const string OneReworded = "# Rules\n\n## One\n\nAlpha, reworded.\n\n## Three\n\nGamma.\n";

    /// <summary>Two new and Three reworded: two changes against the seed.</summary>
    private const string TwoAdded = "# Rules\n\n## One\n\nAlpha.\n\n## Two\n\nBeta.\n\n## Three\n\nGamma, reworded.\n";

    private static CancellationToken Cancellation => TestContext.Current.CancellationToken;

    /// <summary>
    /// The seed and two machines under <paramref name="tree"/>, and a manifest at its root naming them —
    /// with <paramref name="seed"/> as its seed, or none — and any <paramref name="more"/> candidates.
    /// </summary>
    private static string ManifestOf(FixtureTree tree, string? seed = "seed", string order = "divergence", string more = "")
    {
        tree.Write("Seed/AGENTS.md", Seed);
        tree.Write("Machine1/AGENTS.md", OneReworded);
        tree.Write("Machine2/AGENTS.md", TwoAdded);
        string seedKey = seed is null ? "" : $"\"seed\": \"{seed}\",";
        return tree.Write("corpus.json", $$"""
            {
              "target": "AGENTS.md",
              {{seedKey}}
              "order": "{{order}}",
              "candidates": [
                { "path": "Seed/AGENTS.md", "label": "seed" },
                { "path": "Machine1/AGENTS.md", "label": "machine 1" },
                {{more}}
                { "path": "Machine2/AGENTS.md", "label": "machine 2" }
              ]
            }
            """);
    }

    /// <summary>
    /// A decision file beside a baseline under <paramref name="tree"/>, as a save writes one: machine 1's
    /// One left out, and — when <paramref name="both"/> — machine 2's Three too.
    /// </summary>
    private static string DecisionsOf(FixtureTree tree, bool both = false)
    {
        List<SectionDecision> decisions = [Left(OneReworded, "One", "machine 1")];
        if (both)
        {
            decisions.Add(Left(TwoAdded, "Three", "machine 2"));
        }

        string path = tree.PathOf("Baseline/AGENTS.decisions.json");
        Directory.CreateDirectory(Path.GetDirectoryName(path)!);
        DecisionFile.Write(path, decisions);
        return path;
    }

    private static SectionDecision Left(string text, string headingPath, string source) =>
        new(
            DecisionKind.Rejected,
            SectionIdentity.Of(MarkdownOutline.Read(text).Sections.Single(s => s.HeadingPath == headingPath)),
            source,
            null);

    [Fact]
    public void The_command_line_names_a_corpus_a_seed_and_a_decision_file_each_path_made_full()
    {
        string work = Path.Combine(Path.GetTempPath(), "work");

        Assert.Equal(
            new StartupRequest(
                Path.Combine(work, "corpus.json"),
                true,
                "janushalo",
                Path.Combine(Path.GetTempPath(), "AGENTS.decisions.json")),
            Startup.Parse(["--manifest", "corpus.json", "--seed", "janushalo", "--decisions", "../AGENTS.decisions.json"], work));

        // The order of the options is the operator's; an option not given is null, not a default.
        Assert.Equal(
            new StartupRequest(Path.Combine(work, "paths.txt"), false, null, null),
            Startup.Parse(["--list", "paths.txt"], work));
        Assert.Equal(
            new StartupRequest(Path.Combine(work, "paths.txt"), false, "seed", null),
            Startup.Parse(["--seed", "seed", "--list", "paths.txt"], work));
    }

    /// <summary>A command line, and the refusal it earns.</summary>
    public static TheoryData<string[], string> Unreadable => new()
    {
        { [], "no corpus is named: --manifest names a manifest, and --list a path list" },
        { ["--seed", "seed"], "no corpus is named: --manifest names a manifest, and --list a path list" },
        { ["--manifest", "a.json", "--list", "b.txt"], "--manifest and --list are both given, and a corpus is read from one of them" },
        { ["corpus.json"], "\"corpus.json\" is not an option here; the options are --manifest, --list, --seed, --decisions" },
        { ["--Manifest", "a.json"], "\"--Manifest\" is not an option here; the options are --manifest, --list, --seed, --decisions" },
        { ["--manifest"], "--manifest needs a value" },
        { ["--manifest", " "], "--manifest needs a value" },
        { ["--seed", "--manifest", "a.json"], "--seed needs a value" },
        { ["--manifest", "a.json", "--manifest", "b.json"], "--manifest is given twice" },
    };

    [Theory]
    [MemberData(nameof(Unreadable))]
    public void A_command_line_the_start_up_cannot_read_is_refused_and_says_why(string[] args, string why) =>
        Assert.Equal(why, Assert.Throws<StartupRefusedException>(() => Startup.Parse(args, Path.GetTempPath())).Message);

    [Fact]
    public void A_manifest_names_the_seed_and_the_decision_file_brings_every_earlier_decision()
    {
        using FixtureTree tree = new();
        string decisions = DecisionsOf(tree, both: true);

        StartupPlan plan = Startup.Prepare(new StartupRequest(ManifestOf(tree), true, null, decisions), Cancellation);

        Assert.Equal("seed", plan.Queue.Seed.Label);
        Assert.Equal(["machine 1", "machine 2"], plan.Queue.Items.Select(i => i.Candidate.Label).Order(StringComparer.Ordinal));
        Assert.Equal(DecisionFile.Read(decisions), plan.Earlier);
        Assert.Equal(decisions, plan.DecisionsPath);
    }

    [Fact]
    public void A_path_list_is_read_against_its_own_directory_with_the_seed_the_command_line_names()
    {
        using FixtureTree tree = new();
        tree.Write("Seed/AGENTS.md", Seed);
        tree.Write("Machine1/AGENTS.md", OneReworded);
        string list = tree.Write("corpus/paths.txt", "# the seed first\n../Seed/AGENTS.md\r\n../Machine1/AGENTS.md\n");

        StartupPlan plan = Startup.Prepare(new StartupRequest(list, false, "Seed", null), Cancellation);

        Assert.Equal(tree.PathOf("Seed/AGENTS.md"), plan.Queue.Seed.Path);
        Assert.Equal("Machine1", Assert.Single(plan.Queue.Items).Candidate.Label);
        Assert.Empty(plan.Earlier);
        Assert.Null(plan.DecisionsPath);
    }

    [Fact]
    public void No_seed_named_anywhere_is_refused_with_the_labels_to_choose_from()
    {
        using FixtureTree tree = new();

        Assert.Equal(
            "no seed is named: --seed names one of \"seed\", \"machine 1\", \"machine 2\" — it is never implied by the order",
            Assert.Throws<StartupRefusedException>(
                () => Startup.Prepare(new StartupRequest(ManifestOf(tree, seed: null), true, null, null), Cancellation)).Message);

        // A corpus whose every candidate is missing has no label to offer.
        string list = tree.Write("paths.txt", "Gone/AGENTS.md\n");
        Assert.Equal(
            "no seed is named, and the corpus holds no candidate to name",
            Assert.Throws<StartupRefusedException>(
                () => Startup.Prepare(new StartupRequest(list, false, null, null), Cancellation)).Message);
    }

    [Fact]
    public void A_seed_named_twice_differently_is_refused_and_named_alike_is_one_seed()
    {
        using FixtureTree tree = new();
        string manifest = ManifestOf(tree);

        Assert.Equal(
            "the manifest names the seed \"seed\" and --seed names \"machine 1\", and the seed is one candidate",
            Assert.Throws<StartupRefusedException>(
                () => Startup.Prepare(new StartupRequest(manifest, true, "machine 1", null), Cancellation)).Message);
        Assert.Equal("seed", Startup.Prepare(new StartupRequest(manifest, true, "seed", null), Cancellation).Queue.Seed.Label);
    }

    [Fact]
    public void A_seed_that_is_no_candidate_or_all_there_is_is_refused_before_any_window_opens()
    {
        using FixtureTree tree = new();
        tree.Write("Seed/AGENTS.md", Seed);
        string list = tree.Write("paths.txt", "Seed/AGENTS.md\n");

        Assert.Equal(
            "no candidate is labelled \"januswax2\"",
            Assert.Throws<CorpusRefusedException>(
                () => Startup.Prepare(new StartupRequest(list, false, "januswax2", null), Cancellation)).Message);
        Assert.Equal(
            "nothing to review: the corpus holds only the seed, \"Seed\", and a file has nothing to offer against itself",
            Assert.Throws<CorpusRefusedException>(
                () => Startup.Prepare(new StartupRequest(list, false, "Seed", null), Cancellation)).Message);
    }

    [Fact]
    public void A_decision_file_named_and_not_there_is_refused_before_any_corpus_is_read()
    {
        using FixtureTree tree = new();
        string gone = tree.PathOf("Baseline/AGENTS.decisions.json");

        // ⭐ The corpus is not there either, so a refusal naming the decision file is one read first.
        Assert.Equal(
            $"the decision file {gone} is not there — a first run names none",
            Assert.Throws<StartupRefusedException>(
                () => Startup.Prepare(new StartupRequest(tree.PathOf("corpus.json"), true, null, gone), Cancellation)).Message);
    }

    [Fact]
    public void A_file_that_is_no_decision_file_or_cannot_be_read_is_refused_naming_where_it_is()
    {
        using FixtureTree tree = new();
        string manifest = ManifestOf(tree);
        string unversioned = tree.Write("Baseline/AGENTS.decisions.json", "{ \"decisions\": [] }\n");
        string directory = tree.MakeDirectory("Elsewhere/AGENTS.decisions.json");

        Assert.Equal(
            $"the decision file says no \"version\", in {unversioned}",
            Assert.Throws<StartupRefusedException>(
                () => Startup.Prepare(new StartupRequest(manifest, true, null, unversioned), Cancellation)).Message);
        Assert.StartsWith(
            $"the decision file {directory} cannot be read: ",
            Assert.Throws<StartupRefusedException>(
                () => Startup.Prepare(new StartupRequest(manifest, true, null, directory), Cancellation)).Message,
            StringComparison.Ordinal);
    }

    [Fact]
    public void A_corpus_file_that_is_not_there_or_cannot_be_read_is_refused_as_what_it_was_named()
    {
        using FixtureTree tree = new();
        string manifest = tree.PathOf("corpus.json");
        string list = tree.PathOf("paths.txt");
        string elsewhere = tree.PathOf("gone/paths.txt");
        string directory = tree.MakeDirectory("corpus");

        Assert.Equal(
            $"the manifest {manifest} is not there",
            Assert.Throws<StartupRefusedException>(() => Startup.Prepare(new StartupRequest(manifest, true, null, null), Cancellation)).Message);
        Assert.Equal(
            $"the path list {list} is not there",
            Assert.Throws<StartupRefusedException>(() => Startup.Prepare(new StartupRequest(list, false, null, null), Cancellation)).Message);

        // A directory that is not there either is the same answer, and not a failure to read.
        Assert.Equal(
            $"the path list {elsewhere} is not there",
            Assert.Throws<StartupRefusedException>(() => Startup.Prepare(new StartupRequest(elsewhere, false, null, null), Cancellation)).Message);
        Assert.StartsWith(
            $"the path list {directory} cannot be read: ",
            Assert.Throws<StartupRefusedException>(() => Startup.Prepare(new StartupRequest(directory, false, null, null), Cancellation)).Message,
            StringComparison.Ordinal);
    }

    [Fact]
    public void The_start_up_says_what_it_reviews_against_which_seed_and_which_earlier_decisions_came_with_it()
    {
        using FixtureTree tree = new();
        string decisions = DecisionsOf(tree);

        StartupPlan plan = Startup.Prepare(new StartupRequest(ManifestOf(tree), true, null, decisions), Cancellation);
        Assert.Equal(
            [
                "Reviewing AGENTS.md: 2 candidates against the seed \"seed\", most changes first.",
                $"The seed is {tree.PathOf("Seed/AGENTS.md")}.",
                $"1 earlier decision, from {decisions}.",
            ],
            Startup.Describe(plan));

        // One candidate, listed, from two earlier decisions.
        string two = DecisionsOf(tree, both: true);
        string list = tree.Write("paths.txt", "Seed/AGENTS.md\nMachine1/AGENTS.md\n");
        StartupPlan listed = Startup.Prepare(new StartupRequest(ManifestOf(tree, order: "listed"), true, null, two), Cancellation);
        Assert.Equal("Reviewing AGENTS.md: 2 candidates against the seed \"seed\", in the order listed.", Startup.Describe(listed)[0]);
        Assert.Equal($"2 earlier decisions, from {two}.", Startup.Describe(listed)[2]);

        StartupPlan bare = Startup.Prepare(new StartupRequest(list, false, "Seed", null), Cancellation);
        Assert.Equal(
            [
                "Reviewing AGENTS.md: 1 candidate against the seed \"Seed\", most changes first.",
                $"The seed is {tree.PathOf("Seed/AGENTS.md")}.",
                "No decision file is named, so no earlier decision is known.",
            ],
            Startup.Describe(bare));
    }

    [Fact]
    public void Every_line_of_the_corpus_report_is_said_and_an_empty_report_says_nothing()
    {
        CorpusReport report = new(
            Missing:
            [
                new MissingCandidate("/c/januswax2/AGENTS.md", "januswax2", "does not exist"),
                new MissingCandidate("/c/walked", null, "the directory to walk does not exist"),
            ],
            Pruned:
            [
                new PrunedDirectory("/c/a/node_modules", "node_modules"),
                new PrunedDirectory("/c/b/bin", "bin"),
                new PrunedDirectory("/c/c/node_modules", "node_modules"),
            ],
            Unentered: [new UnenteredDirectory("/c/locked", "Access to the path '/c/locked' is denied.")],
            Copies:
            [
                new CopySkip("/c/DiffView/worktrees/x/AGENTS.md", "worktrees", "DiffView/AGENTS.md", "DiffView", IsPointer: false),
                new CopySkip("/c/X/reference/ClaudeForge/AGENTS.md", "reference", "ClaudeForge/AGENTS.md", null, IsPointer: false),
                new CopySkip("/c/Y/bundles/z/AGENTS.md", "bundles", null, null, IsPointer: false),
                new CopySkip("/c/Z/worktrees/p/AGENTS.md", "worktrees", "Z/AGENTS.md", null, IsPointer: true),
            ],
            Pointers: [new ExcludedFile("/c/p1/AGENTS.md", null, false), new ExcludedFile("/c/p2/AGENTS.md", null, false)],
            Binary: [new ExcludedFile("/c/blob/AGENTS.md", "blob", true)],
            UnmatchedIncludes: ["/c/X/reference/Nothing"]);

        Assert.Equal(
            [
                "Missing: /c/januswax2/AGENTS.md (\"januswax2\") — does not exist.",
                "Missing: /c/walked — the directory to walk does not exist.",
                "Not text, so no candidate: /c/blob/AGENTS.md.",
                "Pointers, each nothing but an import, so no candidate: 2.",
                "Build and tool output, never entered: 3 directories, bin 1, node_modules 2.",
                "Not entered: /c/locked — Access to the path '/c/locked' is denied.",
                "A copy of DiffView/AGENTS.md in worktrees: /c/DiffView/worktrees/x/AGENTS.md, held as \"DiffView\".",
                "A copy of ClaudeForge/AGENTS.md in reference: /c/X/reference/ClaudeForge/AGENTS.md, ⚠ held by no candidate.",
                "A copy in bundles: /c/Y/bundles/z/AGENTS.md, ⚠ held by no candidate.",
                "A pointer in worktrees: /c/Z/worktrees/p/AGENTS.md.",
                "An include that re-admitted nothing: /c/X/reference/Nothing.",
            ],
            Startup.LeftOut(report));

        Assert.Equal(
            ["Build and tool output, never entered: 1 directory, obj 1."],
            Startup.LeftOut(new CorpusReport([], [new PrunedDirectory("/c/obj", "obj")], [], [], [], [], [])));
        Assert.Empty(Startup.LeftOut(new CorpusReport([], [], [], [], [], [], [])));
    }

    [Fact]
    public void A_command_line_naming_a_review_says_what_it_reviews_and_what_was_left_out_and_runs_it()
    {
        using FixtureTree tree = new();
        ManifestOf(tree, more: "{ \"path\": \"januswax2/AGENTS.md\", \"label\": \"januswax2\" },");
        using StringWriter output = new();
        using StringWriter errors = new();
        StartupPlan? reviewed = null;

        int exit = Program.Run(["--manifest", "corpus.json"], tree.Root, output, errors, plan =>
        {
            reviewed = plan;
            return 7;
        });

        // The review's own exit code, and every line said before it ran — the missing machine's among them.
        Assert.Equal(7, exit);
        Assert.NotNull(reviewed);
        Assert.Equal(
            [.. Startup.Describe(reviewed), ""],
            output.ToString().Split(Environment.NewLine));
        Assert.Contains(
            $"Missing: {tree.PathOf("januswax2/AGENTS.md")} (\"januswax2\") — does not exist.",
            Startup.Describe(reviewed));
        Assert.Empty(errors.ToString());
    }

    [Fact]
    public void A_command_line_the_start_up_cannot_read_says_why_and_how_it_is_written_and_runs_nothing()
    {
        using StringWriter output = new();
        using StringWriter errors = new();
        bool ran = false;

        int exit = Program.Run(["--manifest"], Path.GetTempPath(), output, errors, _ =>
        {
            ran = true;
            return 0;
        });

        Assert.Equal(2, exit);
        Assert.False(ran);
        Assert.Empty(output.ToString());
        Assert.Equal(
            ["agent-baseline: --manifest needs a value", Startup.Usage, ""],
            errors.ToString().Split(Environment.NewLine));
    }

    [Fact]
    public void A_review_the_start_up_refuses_says_why_without_the_usage_and_runs_nothing()
    {
        using FixtureTree tree = new();
        tree.Write("Seed/AGENTS.md", Seed);
        tree.Write("paths.txt", "Seed/AGENTS.md\n");
        bool ran = false;
        int Review(StartupPlan plan)
        {
            ran = true;
            return 0;
        }

        using StringWriter missing = new();
        Assert.Equal(2, Program.Run(["--list", "gone.txt"], tree.Root, TextWriter.Null, missing, Review));
        Assert.Equal([$"agent-baseline: the path list {tree.PathOf("gone.txt")} is not there", ""], missing.ToString().Split(Environment.NewLine));

        using StringWriter alone = new();
        Assert.Equal(2, Program.Run(["--list", "paths.txt", "--seed", "Seed"], tree.Root, TextWriter.Null, alone, Review));
        Assert.Equal(
            ["agent-baseline: nothing to review: the corpus holds only the seed, \"Seed\", and a file has nothing to offer against itself", ""],
            alone.ToString().Split(Environment.NewLine));
        Assert.False(ran);
    }

    [AvaloniaFact]
    public async Task The_window_opens_on_the_plan_with_every_earlier_decision_in_its_store()
    {
        using FixtureTree tree = new();
        StartupPlan plan = Startup.Prepare(new StartupRequest(ManifestOf(tree), true, null, DecisionsOf(tree, both: true)), Cancellation);
        ReviewWindow window = new();
        using ReviewHost host = new(window);

        Assert.Same(window, await host.AwaitBuildAsync(() => Startup.Open(plan, window)));

        ReviewSession session = Assert.IsType<ReviewSession>(window.Session);
        Assert.Same(plan.Queue, session.Queue);
        Assert.Equal(plan.Earlier, session.Durable.Decisions);
        Assert.Equal(2, session.Durable.Inherited);
    }
}
