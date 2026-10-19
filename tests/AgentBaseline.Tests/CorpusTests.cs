namespace Bennewitz.Ninja.AgentBaseline.Tests;

/// <summary>
/// Plan 00034 step 4: the corpus — labels, targets, per-candidate change counts and divergence order;
/// a missing candidate reported rather than skipped, and a mixed-target set refused.
/// </summary>
/// <remarks>
/// The three exclusions, which the step's verification asks four assertions of, are
/// <see cref="CorpusExclusionTests"/>.
/// </remarks>
public sealed class CorpusTests
{
    private const string Seed =
        "# Rules\n\n## One\n\nAlpha.\n\n## Two\n\nBeta.\n\n## Three\n\nGamma.\n";

    private const string OneChange =
        "# Rules\n\n## One\n\nAlpha.\n\n## Two\n\nBeta, revised.\n\n## Three\n\nGamma.\n";

    private const string AnotherOneChange =
        "# Rules\n\n## One\n\nAlpha.\n\n## Two\n\nBeta.\n\n## Three\n\nGamma, revised.\n";

    private const string ThreeChanges =
        "# Rules\n\n## One\n\nAlpha, revised.\n\n## Two\n\nBeta, revised.\n\n## Three\n\nGamma, revised.\n";

    private static CorpusEntry Entry(string path, string? label = null, string? scope = null) =>
        new(path, label, new CandidateTarget(Path.GetFileName(path), scope));

    private static ReviewQueue QueueOf(Corpus corpus, string? seed = null) =>
        corpus.Queue(seed, TestContext.Current.CancellationToken);

    /// <summary>JanusHalo's shape: a marker on the first line and two spaces on every other.</summary>
    private static string WrappedInAListItem(string plain)
    {
        string[] lines = plain.Split('\n');
        for (int i = 0; i < lines.Length; i++)
        {
            bool terminatorOnly = i == lines.Length - 1 && lines[i].Length == 0;
            lines[i] = i == 0 ? "- " + lines[i] : terminatorOnly ? lines[i] : "  " + lines[i];
        }

        return string.Join('\n', lines);
    }

    [Fact]
    public void Each_candidate_is_labelled_by_the_manifest_or_by_the_shortest_trailing_path_no_other_shares()
    {
        using FixtureTree tree = new();
        string alpha = tree.Write("Alpha/AGENTS.md", Seed);
        string oldAlpha = tree.Write("old/Alpha/AGENTS.md", OneChange);
        string beta = tree.Write("Beta/AGENTS.md", OneChange);
        string gamma = tree.Write("Gamma/AGENTS.md", OneChange);

        Corpus corpus = Corpus.Gather(CorpusRequest.Create([Entry(alpha), Entry(oldAlpha), Entry(beta), Entry(gamma, "Beta")]));

        // Alpha and old/Alpha share their last directory, so each reaches one further; Beta's own name
        // is the label the manifest gave Gamma, so Beta reaches one further too.
        Assert.Equal(
            [$"{tree.RootName}/Alpha", "old/Alpha", $"{tree.RootName}/Beta", "Beta"],
            corpus.Candidates.Select(c => c.Label));
    }

    [Fact]
    public void Two_candidates_given_one_label_are_refused()
    {
        using FixtureTree tree = new();
        CorpusRequest request = CorpusRequest.Create(
            [Entry(tree.Write("A/AGENTS.md", Seed), "same"), Entry(tree.Write("B/AGENTS.md", OneChange), "same")]);

        CorpusRefusedException refused = Assert.Throws<CorpusRefusedException>(() => Corpus.Gather(request));

        Assert.Contains("labelled \"same\"", refused.Message, StringComparison.Ordinal);
    }

    [Fact]
    public void A_path_list_takes_paths_and_file_uris_and_skips_blank_lines_and_comments()
    {
        using FixtureTree tree = new();
        string alpha = tree.Write("Alpha/AGENTS.md", Seed);
        string beta = tree.Write("Beta/AGENTS.md", OneChange);

        CorpusRequest request = CorpusRequest.FromPathList(
            ["# the machines to merge", "", "Alpha/AGENTS.md", "  " + new Uri(beta).AbsoluteUri + "  "],
            tree.Root);
        Corpus corpus = Corpus.Gather(request);

        Assert.Equal([alpha, beta], corpus.Candidates.Select(c => c.Path));
        Assert.Equal(["Alpha", "Beta"], corpus.Candidates.Select(c => c.Label));
        Assert.Equal(new CandidateTarget("AGENTS.md", null), corpus.Target);
    }

    [Fact]
    public void A_uri_that_is_not_a_file_is_refused_because_there_is_no_transport()
    {
        CorpusRefusedException refused = Assert.Throws<CorpusRefusedException>(
            () => CorpusRequest.FromPathList(["ssh://janushalo/home/janus/.claude/CLAUDE.md"], Path.GetTempPath()));

        Assert.Contains("ssh://janushalo", refused.Message, StringComparison.Ordinal);
    }

    [Fact]
    public void A_directory_named_as_a_candidate_is_refused_and_told_how_a_directory_is_read()
    {
        using FixtureTree tree = new();
        tree.Write("Alpha/AGENTS.md", Seed);

        CorpusRefusedException refused = Assert.Throws<CorpusRefusedException>(
            () => Corpus.Gather(CorpusRequest.FromPathList(["Alpha"], tree.Root)));

        Assert.Contains("\"walk\"", refused.Message, StringComparison.Ordinal);
    }

    [Fact]
    public void A_path_named_twice_is_refused()
    {
        using FixtureTree tree = new();
        string alpha = tree.Write("Alpha/AGENTS.md", Seed);

        CorpusRefusedException refused = Assert.Throws<CorpusRefusedException>(
            () => Corpus.Gather(CorpusRequest.Create([Entry(alpha, "first"), Entry(alpha, "second")])));

        Assert.Contains("named twice", refused.Message, StringComparison.Ordinal);
    }

    [Fact]
    public void Candidates_for_two_file_names_are_refused_before_anything_is_read()
    {
        using FixtureTree tree = new();
        string claude = tree.Write("A/CLAUDE.md", Seed);
        string agents = tree.Write("B/AGENTS.md", Seed);
        RecordingFileSystem disk = new();

        CorpusRefusedException refused = Assert.Throws<CorpusRefusedException>(
            () => Corpus.Gather(CorpusRequest.FromPathList([claude, agents], tree.Root), disk));

        // Each target named with the candidates declaring it, so the operator can see which to drop.
        Assert.Contains("2 targets", refused.Message, StringComparison.Ordinal);
        Assert.Contains("CLAUDE.md: " + claude, refused.Message, StringComparison.Ordinal);
        Assert.Contains("AGENTS.md: " + agents, refused.Message, StringComparison.Ordinal);
        Assert.Empty(disk.Touched);
    }

    [Fact]
    public void A_file_name_differing_only_in_case_is_one_target()
    {
        using FixtureTree tree = new();
        string upper = tree.Write("A/CLAUDE.md", Seed);
        string lower = tree.Write("B/claude.md", OneChange);

        // One file to Windows' and macOS's file systems, and plainly meant as the same document on Linux.
        Corpus corpus = Corpus.Gather(CorpusRequest.FromPathList([upper, lower], tree.Root));

        Assert.Equal([upper, lower], corpus.Candidates.Select(c => c.Path));
    }

    [Fact]
    public void A_root_file_and_a_project_file_are_two_targets_though_they_share_a_file_name()
    {
        using FixtureTree tree = new();
        tree.Write("Alpha/AGENTS.md", Seed);
        tree.Write("Alpha/src/AGENTS.md", OneChange);
        const string manifest = """
            {
              "target": "AGENTS.md",
              "scope": "project",
              "candidates": [
                "Alpha/src/AGENTS.md",
                { "path": "Alpha/AGENTS.md", "scope": "root" }
              ]
            }
            """;

        CorpusRefusedException refused = Assert.Throws<CorpusRefusedException>(
            () => Corpus.Gather(CorpusRequest.FromManifest(manifest, tree.Root)));

        Assert.Contains("AGENTS.md (project)", refused.Message, StringComparison.Ordinal);
        Assert.Contains("AGENTS.md (root)", refused.Message, StringComparison.Ordinal);
    }

    [Fact]
    public void A_missing_candidate_is_reported_by_its_label_and_kept_out_of_the_queue()
    {
        using FixtureTree tree = new();
        tree.Write("janushalo/CLAUDE.md", Seed);
        tree.Write("janusser6/CLAUDE.md", OneChange);
        tree.MakeDirectory("januswax2/.claude");
        const string manifest = """
            {
              "target": "CLAUDE.md",
              "scope": "global",
              "seed": "janushalo",
              "candidates": [
                { "path": "janushalo/CLAUDE.md", "label": "janushalo" },
                { "path": "januswax2/.claude/CLAUDE.md", "label": "januswax2" },
                { "path": "janusser6/CLAUDE.md", "label": "janusser6" }
              ]
            }
            """;

        Corpus corpus = Corpus.Gather(CorpusRequest.FromManifest(manifest, tree.Root));

        // Measured: januswax2 has a .claude directory and no CLAUDE.md, so a missing candidate is
        // ordinary — reported by the label it was given, never quietly dropped.
        MissingCandidate missing = Assert.Single(corpus.Report.Missing);
        Assert.Equal("januswax2", missing.Label);
        Assert.Equal(tree.PathOf("januswax2/.claude/CLAUDE.md"), missing.Path);
        Assert.Equal(["janushalo", "janusser6"], corpus.Candidates.Select(c => c.Label));
        Assert.Equal(["janusser6"], QueueOf(corpus).Items.Select(i => i.Candidate.Label));
    }

    [Fact]
    public void A_seed_that_is_missing_is_refused_and_says_it_is_missing()
    {
        using FixtureTree tree = new();
        string present = tree.Write("present/CLAUDE.md", Seed);

        Corpus corpus = Corpus.Gather(CorpusRequest.Create(
            [Entry(present, "present"), Entry(tree.PathOf("absent/CLAUDE.md"), "absent")]));

        Assert.Contains("is missing", Assert.Throws<CorpusRefusedException>(() => QueueOf(corpus, "absent")).Message, StringComparison.Ordinal);
        Assert.Contains("no candidate is labelled", Assert.Throws<CorpusRefusedException>(() => QueueOf(corpus, "nobody")).Message, StringComparison.Ordinal);
    }

    [Fact]
    public void Each_candidate_is_counted_against_the_seeds_normalised_text_and_the_seed_is_not_queued()
    {
        using FixtureTree tree = new();
        Corpus corpus = Corpus.Gather(CorpusRequest.Create(
        [
            Entry(tree.Write("Seed/AGENTS.md", Seed), "seed"),
            Entry(tree.Write("One/AGENTS.md", OneChange), "one"),
            Entry(tree.Write("Three/AGENTS.md", ThreeChanges), "three"),
            Entry(tree.Write("Crlf/AGENTS.md", Seed.Replace("\n", "\r\n", StringComparison.Ordinal)), "crlf"),
            Entry(tree.Write("Nested/AGENTS.md", WrappedInAListItem(Seed)), "nested"),
        ]));

        ReviewQueue queue = QueueOf(corpus, "seed");

        // ⭐ The CRLF copy and the list-wrapped copy are the seed after ingest, and count no changes: the
        // diff compares like with like (decision 8), where the raw bytes differ on every line.
        Assert.Equal("seed", queue.Seed.Label);
        Assert.DoesNotContain(queue.Items, i => i.Candidate.Label == "seed");
        Assert.Equal(
            [("crlf", 0), ("nested", 0), ("one", 1), ("three", 3)],
            queue.Items.Select(i => (i.Candidate.Label, i.Changes)).OrderBy(p => p.Label, StringComparer.Ordinal));
    }

    [Fact]
    public void The_queue_puts_the_most_changes_first_and_a_tie_keeps_the_order_it_was_given()
    {
        using FixtureTree tree = new();
        Corpus corpus = Corpus.Gather(CorpusRequest.Create(
        [
            Entry(tree.Write("Seed/AGENTS.md", Seed), "seed"),
            Entry(tree.Write("A/AGENTS.md", OneChange), "one-a"),
            Entry(tree.Write("C/AGENTS.md", Seed), "none"),
            Entry(tree.Write("B/AGENTS.md", ThreeChanges), "three"),
            Entry(tree.Write("D/AGENTS.md", AnotherOneChange), "one-d"),
        ]));

        // Given as one-a, none, three, one-d: neither that order nor its reverse.
        Assert.Equal(["three", "one-a", "one-d", "none"], QueueOf(corpus, "seed").Items.Select(i => i.Candidate.Label));
    }

    [Fact]
    public void A_listed_order_reviews_the_candidates_as_the_manifest_lists_them()
    {
        using FixtureTree tree = new();
        tree.Write("Seed/AGENTS.md", Seed);
        tree.Write("A/AGENTS.md", OneChange);
        tree.Write("C/AGENTS.md", Seed);
        tree.Write("B/AGENTS.md", ThreeChanges);
        const string manifest = """
            {
              "target": "AGENTS.md",
              "seed": "seed",
              "order": "listed",
              "candidates": [
                { "path": "Seed/AGENTS.md", "label": "seed" },
                { "path": "A/AGENTS.md", "label": "one" },
                { "path": "C/AGENTS.md", "label": "none" },
                { "path": "B/AGENTS.md", "label": "three" }
              ]
            }
            """;

        Corpus corpus = Corpus.Gather(CorpusRequest.FromManifest(manifest, tree.Root));

        Assert.Equal(["one", "none", "three"], QueueOf(corpus).Items.Select(i => i.Candidate.Label));
    }

    [Fact]
    public void A_corpus_never_chooses_its_own_seed()
    {
        using FixtureTree tree = new();
        Corpus corpus = Corpus.Gather(CorpusRequest.Create(
            [Entry(tree.Write("A/AGENTS.md", Seed)), Entry(tree.Write("B/AGENTS.md", OneChange))]));

        // Decision 9b: stated in the manifest or chosen at start-up, never implied by ordering.
        Assert.Null(corpus.RequestedSeed);
        Assert.Contains(
            "never implied by the order",
            Assert.Throws<CorpusRefusedException>(() => QueueOf(corpus)).Message,
            StringComparison.Ordinal);
    }

    [Fact]
    public void A_candidate_that_is_not_text_is_reported_and_cannot_be_the_seed()
    {
        using FixtureTree tree = new();
        string blob = tree.WriteBytes("Blob/AGENTS.md", [0x23, 0x20, 0x00, 0x01]);
        string alpha = tree.Write("Alpha/AGENTS.md", Seed);

        Corpus corpus = Corpus.Gather(CorpusRequest.Create([Entry(blob, "blob"), Entry(alpha, "alpha")]));

        ExcludedFile excluded = Assert.Single(corpus.Report.Binary);
        Assert.Equal(blob, excluded.Path);
        Assert.True(excluded.Named);
        Assert.Equal(["alpha"], corpus.Candidates.Select(c => c.Label));
        Assert.Contains("not text", Assert.Throws<CorpusRefusedException>(() => QueueOf(corpus, "blob")).Message, StringComparison.Ordinal);
    }

    [Fact]
    public void A_pointer_cannot_be_the_seed_and_the_refusal_says_why()
    {
        using FixtureTree tree = new();
        string pointer = tree.Write("halo/CLAUDE.md", "@AGENTS.md\n");
        string real = tree.Write("wax/CLAUDE.md", Seed);

        Corpus corpus = Corpus.Gather(CorpusRequest.Create([Entry(pointer, "halo"), Entry(real, "wax")]));

        Assert.Contains("is a pointer", Assert.Throws<CorpusRefusedException>(() => QueueOf(corpus, "halo")).Message, StringComparison.Ordinal);
    }

    [Fact]
    public void A_candidate_that_cannot_be_read_is_reported_as_missing_with_the_reason()
    {
        using FixtureTree tree = new();
        string locked = tree.Write("Locked/AGENTS.md", Seed);
        string alpha = tree.Write("Alpha/AGENTS.md", OneChange);
        RecordingFileSystem disk = new();
        disk.UnreadableFiles.Add(locked);

        Corpus corpus = Corpus.Gather(CorpusRequest.Create([Entry(locked, "locked"), Entry(alpha, "alpha")]), disk);

        MissingCandidate missing = Assert.Single(corpus.Report.Missing);
        Assert.Equal(("locked", locked), (missing.Label, missing.Path));
        Assert.StartsWith("could not be read: ", missing.Reason, StringComparison.Ordinal);
        Assert.Equal(["alpha"], corpus.Candidates.Select(c => c.Label));
    }

    [Fact]
    public void Two_candidates_in_one_directory_are_told_apart_by_their_file_names()
    {
        using FixtureTree tree = new();
        CandidateTarget target = new("CLAUDE.md", null);
        string current = tree.Write("X/CLAUDE.md", Seed);
        string old = tree.Write("X/CLAUDE.old.md", OneChange);

        Corpus corpus = Corpus.Gather(CorpusRequest.Create([new CorpusEntry(current, null, target), new CorpusEntry(old, null, target)]));

        // Every run of trailing directories is shared, so only the file name can tell them apart.
        string[] labels = [.. corpus.Candidates.Select(c => c.Label)];
        Assert.EndsWith("/X/CLAUDE.md", labels[0], StringComparison.Ordinal);
        Assert.EndsWith("/X/CLAUDE.old.md", labels[1], StringComparison.Ordinal);
    }

    [Fact]
    public void An_empty_label_is_refused()
    {
        using FixtureTree tree = new();
        CorpusRequest request = CorpusRequest.Create([Entry(tree.Write("A/AGENTS.md", Seed), "  ")]);

        Assert.Contains("an empty label", Assert.Throws<CorpusRefusedException>(() => Corpus.Gather(request)).Message, StringComparison.Ordinal);
    }

    [Fact]
    public void An_input_naming_nothing_is_refused()
    {
        Assert.Contains(
            "names no candidate",
            Assert.Throws<CorpusRefusedException>(() => Corpus.Gather(CorpusRequest.Create([]))).Message,
            StringComparison.Ordinal);
    }

    [Theory]
    [InlineData("not json", "is not JSON")]
    [InlineData("[]", "is not a JSON object")]
    [InlineData("""{ "target": 1 }""", "\"target\" is not a string")]
    [InlineData("""{ "order": "random" }""", "\"order\" is \"random\"")]
    [InlineData("""{ "walk": "." }""", "\"walk\" is not a list")]
    [InlineData("""{ "candidates": [ 3 ] }""", "a candidate is a path, or an object")]
    [InlineData("""{ "candidates": [ { "label": "x" } ] }""", "a candidate has no \"path\"")]
    [InlineData("""{ "include": [ 3 ] }""", "an entry of \"include\" is not a path")]
    public void A_manifest_that_cannot_be_read_as_one_is_refused_and_says_why(string json, string because) =>
        Assert.Contains(
            because,
            Assert.Throws<CorpusRefusedException>(() => CorpusRequest.FromManifest(json, Path.GetTempPath())).Message,
            StringComparison.Ordinal);

    [Fact]
    public void A_manifest_key_it_does_not_know_is_refused_rather_than_ignored()
    {
        string temp = Path.GetTempPath();

        CorpusRefusedException topLevel = Assert.Throws<CorpusRefusedException>(
            () => CorpusRequest.FromManifest("""{ "target": "AGENTS.md", "inlcude": ["x"] }""", temp));
        CorpusRefusedException inCandidate = Assert.Throws<CorpusRefusedException>(
            () => CorpusRequest.FromManifest("""{ "candidates": [ { "path": "x/AGENTS.md", "lable": "y" } ] }""", temp));

        Assert.Contains("\"inlcude\"", topLevel.Message, StringComparison.Ordinal);
        Assert.Contains("\"lable\"", inCandidate.Message, StringComparison.Ordinal);
    }

    [Fact]
    public void A_walk_with_no_target_is_refused_because_it_would_not_know_what_to_look_for()
    {
        CorpusRefusedException refused = Assert.Throws<CorpusRefusedException>(
            () => CorpusRequest.FromManifest("""{ "walk": ["."] }""", Path.GetTempPath()));

        Assert.Contains("a walk needs a target", refused.Message, StringComparison.Ordinal);
    }

    [Fact]
    public void A_manifest_reads_its_relative_paths_against_its_own_directory()
    {
        using FixtureTree tree = new();
        string manifests = tree.MakeDirectory("manifests");
        string alpha = tree.Write("Alpha/AGENTS.md", Seed);

        CorpusRequest request = CorpusRequest.FromManifest(
            """{ "target": "AGENTS.md", "candidates": ["../Alpha/AGENTS.md"], "walk": [".."], "include": ["../Alpha/reference"] }""",
            manifests);

        Assert.Equal([alpha], request.Candidates.Select(c => c.Path));
        Assert.Equal([tree.Root], request.Walks);
        Assert.Equal([tree.PathOf("Alpha/reference")], request.Includes);
    }
}
