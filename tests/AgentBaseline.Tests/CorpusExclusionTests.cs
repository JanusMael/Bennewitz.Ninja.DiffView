using System.Text;

namespace Bennewitz.Ninja.AgentBaseline.Tests;

/// <summary>
/// Plan 00034 step 4's three exclusions, which decisions 15 and 15b need <b>four</b> assertions of:
/// tier 2's copies named and re-admittable, tier 1 never descended, pointers counted and never
/// collapsed, and a path named under a pruned directory still a candidate.
/// </summary>
/// <remarks>
/// ⛔ Each assertion is shaped by the way its first attempt was wrong. A walk that visited everything
/// and then filtered produces the same candidates as one that pruned, so tier 1 is asserted on the
/// <b>traversal</b>, through <see cref="RecordingFileSystem"/>. A copy that is byte-identical to the
/// document it copies cannot tell a hash rule from an identity rule, so the copies here are
/// <b>older versions</b>. And pointers that differ cannot show a collapse, so the pointers here are
/// <b>identical</b>.
/// </remarks>
public sealed class CorpusExclusionTests
{
    private static CorpusRequest Walking(FixtureTree tree, string fileName) =>
        CorpusRequest.Create([], new CandidateTarget(fileName, null), [tree.Root]);

    private static string Version(string name, int version) =>
        $"# {name}\n\n## Rules\n\nVersion {version} of {name}'s rules.\n";

    [Fact]
    public void Build_output_is_counted_and_never_descended_while_everything_else_is_walked()
    {
        using FixtureTree tree = new();
        string alpha = tree.Write("Alpha/AGENTS.md", Version("Alpha", 1));
        string nested = tree.Write("Beta/src/AGENTS.md", Version("Beta", 1));

        // Each of these would be a candidate were its directory descended, and the inner node_modules
        // would be counted too were the outer one.
        tree.Write("Alpha/node_modules/pkg/AGENTS.md", Version("a dependency", 1));
        tree.Write("Alpha/node_modules/pkg/node_modules/inner/AGENTS.md", Version("a dependency's dependency", 1));
        tree.Write("Alpha/bin/Debug/AGENTS.md", Version("build output", 1));
        tree.Write("Beta/bin/AGENTS.md", Version("build output", 3));
        tree.Write("Alpha/obj/AGENTS.md", Version("build output", 2));
        tree.Write("artifacts/mutate/repo/AGENTS.md", Version("a mutation harness's working copy", 1));
        tree.Write("Alpha/.git/AGENTS.md", Version("inside git", 1));

        // Git marks .git hidden on Windows. A listing that skips hidden entries — the default of
        // new EnumerationOptions(), which on Linux skips every dot-directory — would leave it uncounted.
        string git = tree.PathOf("Alpha/.git");
        File.SetAttributes(git, File.GetAttributes(git) | FileAttributes.Hidden);

        string[] output = ["Alpha/.git", "Alpha/bin", "Alpha/node_modules", "Alpha/obj", "Beta/bin", "artifacts"];
        RecordingFileSystem disk = new();

        Corpus corpus = Corpus.Gather(Walking(tree, "AGENTS.md"), disk);

        // ⛔ The assertion the step exists for, and first so that it is the one a descent trips: nothing
        // at or under build output was ever handed to the disk — not listed, not probed, not read. "No
        // candidate came out" would pass for a walk that visited everything first.
        Assert.All(output, d => Assert.False(disk.TouchedAtOrUnder(tree.PathOf(d)), $"the walk reached into {d}"));

        Assert.Equal([alpha, nested], corpus.Candidates.Select(c => c.Path));
        Assert.Equal([.. output.Select(tree.PathOf)], corpus.Report.Pruned.Select(p => p.Path));

        // The count an operator reads: by name, and the inner node_modules absent because it was never seen.
        Assert.Equal(
            [(".git", 1), ("artifacts", 1), ("bin", 2), ("node_modules", 1), ("obj", 1)],
            corpus.Report.PrunedByName.Select(p => (p.Key, p.Value)).OrderBy(p => p.Key, StringComparer.Ordinal));

        // The positive control: the walk did walk, and this is the whole of it.
        Assert.Equal(
            [tree.Root, tree.PathOf("Alpha"), tree.PathOf("Beta"), tree.PathOf("Beta/src")],
            disk.Listed);
    }

    [Fact]
    public void Each_copy_is_named_and_judged_by_the_document_it_copies_not_by_its_bytes()
    {
        using FixtureTree tree = new();
        string alpha = tree.Write("Alpha/AGENTS.md", Version("Alpha", 3));
        string beta = tree.Write("Beta/AGENTS.md", Version("Beta", 1));
        string worktree = tree.Write("Alpha/.claude/worktrees/wt-one/AGENTS.md", Version("Alpha", 2));
        string reference = tree.Write("Beta/reference/Alpha/AGENTS.md", Version("Alpha", 1));
        string orphan = tree.Write("Gamma/reference/Delta/AGENTS.md", Version("Delta", 1));
        string bundle = tree.Write("bundles/Alpha/AGENTS.md", Version("Alpha", 1) + "\nPinned.\n");

        // Measured on this estate: a worktree carrying its own reference checkout. The innermost copy
        // directory says what the file is a copy of.
        string nested = tree.Write("Alpha/.claude/worktrees/wt-two/reference/Delta/AGENTS.md", Version("Delta", 0));

        // The premise, asserted so the test cannot pass on equal bytes: every copy is an OLDER VERSION
        // of the document it copies — the case a hash rule got wrong, and the common one.
        byte[] current = File.ReadAllBytes(alpha);
        Assert.All([worktree, reference, bundle], copy => Assert.NotEqual(current, File.ReadAllBytes(copy)));

        Corpus corpus = Corpus.Gather(Walking(tree, "AGENTS.md"));

        Assert.Equal([alpha, beta], corpus.Candidates.Select(c => c.Path));

        // ⭐ Named one by one, each with what it is a copy of and who holds that document now. The
        // reference copy sits under Beta and is Alpha's; the orphan is Delta's, and nobody holds Delta,
        // which is the line an operator reads to decide whether to include it.
        (string Path, string Holder, string? Identity, string? HeldAs)[] expected =
        [
            (worktree, "worktrees", "Alpha/AGENTS.md", "Alpha"),
            (nested, "reference", "Delta/AGENTS.md", null),
            (reference, "reference", "Alpha/AGENTS.md", "Alpha"),
            (orphan, "reference", "Delta/AGENTS.md", null),
            (bundle, "bundles", "Alpha/AGENTS.md", "Alpha"),
        ];
        Assert.Equal(expected, corpus.Report.Copies.Select(c => (c.Path, c.Holder, c.Identity, c.HeldAs)));
        Assert.DoesNotContain(corpus.Report.Copies, c => c.IsPointer);

        // "Queues none of them", read literally: seeded from Alpha, the queue holds Beta and nothing else.
        Assert.Equal([beta], corpus.Queue("Alpha", TestContext.Current.CancellationToken).Items.Select(i => i.Candidate.Path));
    }

    [Fact]
    public void A_copy_sitting_directly_in_a_copy_directory_is_named_with_no_identity()
    {
        using FixtureTree tree = new();
        tree.Write("Alpha/AGENTS.md", Version("Alpha", 1));
        string loose = tree.Write("Alpha/reference/AGENTS.md", Version("something", 1));

        Corpus corpus = Corpus.Gather(Walking(tree, "AGENTS.md"));

        // No directory names what it is a copy of, so nothing can say it is held: the operator is told.
        CopySkip copy = Assert.Single(corpus.Report.Copies);
        Assert.Equal((loose, "reference", (string?)null, (string?)null), (copy.Path, copy.Holder, copy.Identity, copy.HeldAs));
    }

    [Fact]
    public void An_include_re_admits_a_copy_and_an_include_that_re_admits_nothing_is_reported()
    {
        using FixtureTree tree = new();
        string alpha = tree.Write("Alpha/AGENTS.md", Version("Alpha", 3));
        string worktree = tree.Write("Alpha/.claude/worktrees/wt-one/AGENTS.md", Version("Alpha", 2));
        string delta = tree.Write("Gamma/reference/Delta/AGENTS.md", Version("Delta", 1));
        string epsilon = tree.Write("bundles/Epsilon/AGENTS.md", Version("Epsilon", 1));
        string nowhere = tree.PathOf("Gamma/reference/Nowhere");
        const string manifest = """
            {
              "target": "AGENTS.md",
              "walk": ["."],
              "include": ["Gamma/reference", "bundles/Epsilon/AGENTS.md", "Gamma/reference/Nowhere"]
            }
            """;

        // A manifest's include, as the step says: one names a directory and re-admits what is under it,
        // one names a file exactly, and one names nothing that is there.
        Corpus corpus = Corpus.Gather(CorpusRequest.FromManifest(manifest, tree.Root));

        Assert.Equal(
            [(alpha, Admission.Walked), (delta, Admission.Included), (epsilon, Admission.Included)],
            corpus.Candidates.Select(c => (c.Path, c.Admission)));
        Assert.Equal([worktree], corpus.Report.Copies.Select(c => c.Path));
        Assert.Equal([nowhere], corpus.Report.UnmatchedIncludes);
    }

    [Fact]
    public void Pointers_are_counted_one_per_file_and_never_collapsed_into_one()
    {
        using FixtureTree tree = new();
        const string pointer = "@AGENTS.md\n";
        string one = tree.Write("One/CLAUDE.md", pointer);
        string two = tree.Write("Two/CLAUDE.md", pointer);
        string three = tree.Write("Three/CLAUDE.md", pointer);
        string real = tree.Write("Four/CLAUDE.md", "# Working preferences\n\nA rule.\n");
        string copied = tree.Write("One/.claude/worktrees/wt/CLAUDE.md", pointer);

        // The premise: three byte-identical files of the measured 11 bytes, which is exactly what a
        // content-hash rule collapses into one entry.
        Assert.Equal(11, File.ReadAllBytes(one).Length);
        Assert.Equal(File.ReadAllBytes(one), File.ReadAllBytes(two));
        Assert.Equal(File.ReadAllBytes(one), File.ReadAllBytes(three));

        Corpus corpus = Corpus.Gather(Walking(tree, "CLAUDE.md"));

        // Three repositories present, three lines: the count is the evidence they are there at all.
        Assert.Equal([real], corpus.Candidates.Select(c => c.Path));
        Assert.Equal([one, three, two], corpus.Report.Pointers.Select(p => p.Path));
        Assert.All(corpus.Report.Pointers, p => Assert.False(p.Named));

        // A pointer inside a copy is the copy's line, not a fourth repository, and loses nothing.
        CopySkip copy = Assert.Single(corpus.Report.Copies);
        Assert.Equal(copied, copy.Path);
        Assert.True(copy.IsPointer);
    }

    [Theory]
    [InlineData("@AGENTS.md", true)]
    [InlineData("@AGENTS.md\n", true)]
    [InlineData("\n  @AGENTS.md  \n\n", true)]
    [InlineData("@AGENTS.md\n@CLAUDE.local.md\n", true)]
    [InlineData("", false)]
    [InlineData("\n\n", false)]
    [InlineData("@\n", false)]
    [InlineData("See @AGENTS.md\n", false)]
    [InlineData("@AGENTS.md and more\n", false)]
    [InlineData("# Title\n\n@AGENTS.md\n", false)]
    public void A_pointer_is_a_file_of_imports_and_nothing_else(string text, bool isPointer) =>
        Assert.Equal(isPointer, Corpus.IsPointer(IngestedDocument.FromBytes(Encoding.UTF8.GetBytes(text))));

    [Fact]
    public void A_path_named_under_a_pruned_directory_is_still_a_candidate_and_the_walk_still_prunes()
    {
        using FixtureTree tree = new();
        tree.Write("Alpha/AGENTS.md", Version("Alpha", 1));
        string inBin = tree.Write("Alpha/bin/AGENTS.md", Version("built", 1));
        string inReference = tree.Write("Alpha/reference/Other/AGENTS.md", Version("Other", 1));
        RecordingFileSystem disk = new();
        CorpusRequest request = CorpusRequest.Create(
            [
                new CorpusEntry(inBin, "named-in-bin", new CandidateTarget("AGENTS.md", null)),
                new CorpusEntry(inReference, "named-in-reference", new CandidateTarget("AGENTS.md", null)),
            ],
            new CandidateTarget("AGENTS.md", null),
            [tree.Root]);

        Corpus corpus = Corpus.Gather(request, disk);

        // ⭐ Pruning governs the traversal, never a path the operator wrote down.
        Assert.Equal(
            [("named-in-bin", Admission.Named), ("named-in-reference", Admission.Named), ("Alpha", Admission.Walked)],
            corpus.Candidates.Select(c => (c.Label, c.Admission)));

        // And the walk pruned exactly as it would have: the named file was read, its directory never
        // listed; the named copy was met again and is not a copy for having been named.
        Assert.Equal([tree.PathOf("Alpha/bin")], corpus.Report.Pruned.Select(p => p.Path));
        Assert.DoesNotContain(tree.PathOf("Alpha/bin"), disk.Listed);
        Assert.Empty(corpus.Report.Copies);
    }

    [Fact]
    public void A_directory_the_operator_names_to_walk_is_walked_whatever_it_is_called()
    {
        using FixtureTree tree = new();
        string inBin = tree.Write("bin/AGENTS.md", Version("bin", 1));
        string inReference = tree.Write("reference/Other/AGENTS.md", Version("Other", 1));

        Corpus corpus = Corpus.Gather(CorpusRequest.Create(
            [], new CandidateTarget("AGENTS.md", null), [tree.PathOf("bin"), tree.PathOf("reference")]));

        // Only what a walk meets BELOW its root is judged, so a root named like a tier is no tier.
        Assert.Equal([inBin, inReference], corpus.Candidates.Select(c => c.Path));
        Assert.Empty(corpus.Report.Pruned);
        Assert.Empty(corpus.Report.Copies);
    }

    [Fact]
    public void A_directory_link_is_reported_and_never_followed()
    {
        using FixtureTree tree = new();
        string alpha = tree.Write("walked/Alpha/AGENTS.md", Version("Alpha", 1));
        tree.Write("outside/AGENTS.md", Version("outside", 1));
        string link = tree.PathOf("walked/Alpha/linked");
        Directory.CreateSymbolicLink(link, tree.PathOf("outside"));
        RecordingFileSystem disk = new();

        Corpus corpus = Corpus.Gather(
            CorpusRequest.Create([], new CandidateTarget("AGENTS.md", null), [tree.PathOf("walked")]), disk);

        // A tree without links is acyclic and one with them need not be; following this one would
        // admit the outside file under a second path.
        Assert.Equal([alpha], corpus.Candidates.Select(c => c.Path));
        UnenteredDirectory unentered = Assert.Single(corpus.Report.Unentered);
        Assert.Equal(link, unentered.Path);
        Assert.Contains("link", unentered.Reason, StringComparison.Ordinal);
        Assert.False(disk.TouchedAtOrUnder(link));
    }

    [Fact]
    public void A_directory_that_cannot_be_read_is_reported_and_the_walk_goes_on()
    {
        using FixtureTree tree = new();
        string alpha = tree.Write("Alpha/AGENTS.md", Version("Alpha", 1));
        tree.Write("Locked/AGENTS.md", Version("Locked", 1));
        string zeta = tree.Write("Zeta/AGENTS.md", Version("Zeta", 1));
        RecordingFileSystem disk = new();
        disk.Unreadable.Add(tree.PathOf("Locked"));

        Corpus corpus = Corpus.Gather(Walking(tree, "AGENTS.md"), disk);

        Assert.Equal([alpha, zeta], corpus.Candidates.Select(c => c.Path));
        UnenteredDirectory unentered = Assert.Single(corpus.Report.Unentered);
        Assert.Equal(tree.PathOf("Locked"), unentered.Path);
        Assert.Contains("denied", unentered.Reason, StringComparison.Ordinal);
    }

    [Fact]
    public void A_directory_to_walk_that_is_not_there_is_reported_as_missing()
    {
        using FixtureTree tree = new();
        string absent = tree.PathOf("not-here");

        Corpus corpus = Corpus.Gather(CorpusRequest.Create([], new CandidateTarget("AGENTS.md", null), [absent]));

        MissingCandidate missing = Assert.Single(corpus.Report.Missing);
        Assert.Equal(absent, missing.Path);
        Assert.Empty(corpus.Candidates);
    }

    [Fact]
    public void A_file_given_as_a_directory_to_walk_is_refused()
    {
        using FixtureTree tree = new();
        string file = tree.Write("Alpha/AGENTS.md", Version("Alpha", 1));

        CorpusRefusedException refused = Assert.Throws<CorpusRefusedException>(
            () => Corpus.Gather(CorpusRequest.Create([], new CandidateTarget("AGENTS.md", null), [file])));

        Assert.Contains("is a file", refused.Message, StringComparison.Ordinal);
    }
}
