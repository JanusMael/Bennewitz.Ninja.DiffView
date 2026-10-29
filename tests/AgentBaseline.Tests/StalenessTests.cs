using Avalonia.Headless.XUnit;
using Avalonia.Logging;
using Bennewitz.Ninja.DiffView.Tests;

namespace Bennewitz.Ninja.AgentBaseline.Tests;

/// <summary>
/// Plan 00034 step 9: narrow staleness — decision 11. A candidate is stale only where it carries its own version
/// of a section the accumulator has gained or changed since the candidate was last on the right.
/// </summary>
/// <remarks>
/// <para>
/// ⛔ <b>Positive and negative</b>, as the step asks: a take of a section a candidate has no version of must
/// leave it unstale, and a take of one it carries a differing version of must mark it. The positive half alone
/// passes on literal staleness, every take staling every candidate reviewed before it, which decision 11
/// rejects as a wall.
/// </para>
/// <para>
/// A candidate is reviewed here by being on the right and then left there, through
/// <see cref="ReviewSession.Next"/> or <see cref="ReviewSession.Previous"/>, as the window's F8 and Shift+F8
/// do. The edits typed are typed, for <see cref="SectionDecisionTests"/>' reason.
/// </para>
/// </remarks>
public sealed class StalenessTests
{
    /// <summary>The accumulator's starting text: the title, One and Two.</summary>
    private static readonly string Seed = Doc("# Rules", "", "## One", "", "Alpha.", "", "## Two", "", "Beta.", "");

    private static string Doc(params string[] lines) => string.Join("\n", lines);

    /// <summary>The seed, with One's body reading <paramref name="one"/>.</summary>
    private static string WithOne(string one) => Doc("# Rules", "", "## One", "", one, "", "## Two", "", "Beta.", "");

    private static ReviewQueue QueueOf(FixtureTree tree, string seed, params string[] candidates)
    {
        List<CorpusEntry> entries = [Entry(tree.Write("Seed/AGENTS.md", seed), "seed")];
        for (int i = 0; i < candidates.Length; i++)
        {
            entries.Add(Entry(tree.Write($"Machine{i + 1}/AGENTS.md", candidates[i]), $"machine {i + 1}"));
        }

        Corpus corpus = Corpus.Gather(CorpusRequest.Create(entries, seed: "seed", order: CorpusOrder.Listed));
        return corpus.Queue(cancellationToken: TestContext.Current.CancellationToken);
    }

    private static CorpusEntry Entry(string path, string label) =>
        new(path, label, new CandidateTarget(Path.GetFileName(path), null));

    private static MarkdownSection SectionOf(ReviewSession session, string path) =>
        session.Current.Candidate.Document.Outline.Sections.Single(s => s.HeadingPath == path);

    /// <summary>The content of <paramref name="item"/>'s section at <paramref name="path"/>.</summary>
    private static string ContentAt(QueuedCandidate item, string path) =>
        item.Candidate.Document.Outline.Sections.Single(s => s.HeadingPath == path).Content;

    /// <summary>The content of the accumulator's section at <paramref name="path"/>, as the left now reads.</summary>
    private static string LeftContentAt(ReviewHost host, string path) =>
        MarkdownOutline.Read(host.View.LeftDocument.Text).Sections.Single(s => s.HeadingPath == path).Content;

    /// <summary>Asserts that <paramref name="session"/> says exactly <paramref name="expected"/> is stale, in order.</summary>
    private static void AssertStale(ReviewSession session, string when, params StaleSection[] expected)
    {
        IReadOnlyList<StaleSection> stale = session.Staleness();
        Assert.True(stale.SequenceEqual(expected), $"{when}, the session should say {Spoken(expected)}, and it says {Spoken(stale)}.");
    }

    private static string Spoken(IEnumerable<StaleSection> stale) =>
        stale.Any()
            ? string.Join(" and ", stale.Select(s => $"\"{s.Source}\" is stale at \"{s.HeadingPath}\""))
            : "nothing is stale";

    [AvaloniaFact]
    public async Task Taking_a_section_a_candidate_has_no_version_of_leaves_it_unstale_and_one_it_carries_differently_marks_it()
    {
        TestLogSink.Instance.Clear();
        using FixtureTree tree = new();
        ReviewQueue queue = QueueOf(
            tree,
            Seed,
            Doc("# Rules", "", "## One", "", "Alpha, as machine 1 has it.", "", "## Two", "", "Beta, as machine 1 has it.", ""),
            Doc("# Rules", "", "## One", "", "Alpha, as machine 2 has it.", "", "## Two", "", "Beta.", "", "## Three", "", "Gamma.", ""));
        using ReviewHost host = new();
        ReviewSession session = await host.AwaitBuildAsync(() => new ReviewSession(queue, host.View));

        // Machine 1 is reviewed, and machine 2 is on the right.
        Assert.True(await host.AwaitBuildAsync(session.Next));

        // ⛔ The premise, asserted rather than assumed: machine 1 carries versions of One and Two that are not the
        // accumulator's, so a staleness that did not ask where the accumulator moved would mark both.
        Assert.NotEqual(LeftContentAt(host, "One"), ContentAt(queue.Items[0], "One"));
        Assert.NotEqual(LeftContentAt(host, "Two"), ContentAt(queue.Items[0], "Two"));

        Assert.Equal(TakeResult.Inserted, session.Take(SectionOf(session, "Three")));
        AssertStale(session, "After a take of Three, which machine 1 has no version of");

        Assert.Equal(TakeResult.Replaced, session.Take(SectionOf(session, "One")));
        AssertStale(session, "After a take of One, which machine 1 carries in a version of its own", new StaleSection("One", "machine 1"));

        // Staleness is read, never marked: undone, the take puts back the One machine 1 was reviewed against.
        host.Left.Undo();
        ReviewHost.Layout();
        Assert.Equal(MarkdownOutline.Read(Seed).Sections.Single(s => s.HeadingPath == "One").Content, LeftContentAt(host, "One"));
        AssertStale(session, "After the take of One was undone");

        TestLogSink.AssertNoWarnings(LogArea.Binding);
    }

    [AvaloniaFact]
    public async Task A_reviewed_candidate_carrying_the_very_version_taken_is_not_stale_there()
    {
        TestLogSink.Instance.Clear();
        using FixtureTree tree = new();
        ReviewQueue queue = QueueOf(tree, Seed, WithOne("Alpha, reworded."), WithOne("Alpha, reworded."));
        using ReviewHost host = new();
        ReviewSession session = await host.AwaitBuildAsync(() => new ReviewSession(queue, host.View));
        string reviewedAgainst = LeftContentAt(host, "One");
        Assert.True(await host.AwaitBuildAsync(session.Next));

        Assert.Equal(TakeResult.Replaced, session.Take(SectionOf(session, "One")));

        // Machine 1 was reviewed against another One, and carries exactly the One the accumulator holds now.
        Assert.NotEqual(reviewedAgainst, ContentAt(queue.Items[0], "One"));
        Assert.Equal(LeftContentAt(host, "One"), ContentAt(queue.Items[0], "One"));
        AssertStale(session, "After a take of One in the very version machine 1 carries");

        TestLogSink.AssertNoWarnings(LogArea.Binding);
    }

    [AvaloniaFact]
    public async Task Typing_stales_a_candidate_reviewed_before_it_but_never_the_one_on_the_right()
    {
        TestLogSink.Instance.Clear();
        using FixtureTree tree = new();
        ReviewQueue queue = QueueOf(tree, Seed, WithOne("Alpha, as machine 1 has it."), WithOne("Alpha, as machine 2 has it."));
        using ReviewHost host = new();
        ReviewSession session = await host.AwaitBuildAsync(() => new ReviewSession(queue, host.View));

        // Both are reviewed, and machine 1 is on the right again.
        Assert.True(await host.AwaitBuildAsync(session.Next));
        Assert.True(await host.AwaitBuildAsync(session.Previous));

        host.Type(host.Left, Seed.IndexOf("Alpha.", StringComparison.Ordinal) + "Alpha.".Length, " Typed.");
        Assert.True(LeftContentAt(host, "One") == "## One\n\nAlpha. Typed.", "The left refused the keystroke.");

        // ⭐ Machine 1 is under review against the accumulator as it stands, so only machine 2 is behind it.
        AssertStale(session, "After typing in One with machine 1 on the right", new StaleSection("One", "machine 2"));

        // Leaving machine 1 records what it was reviewed against, the typing included.
        Assert.True(await host.AwaitBuildAsync(session.Next));
        AssertStale(session, "After machine 1 left the right pane again");

        TestLogSink.AssertNoWarnings(LogArea.Binding);
    }

    [AvaloniaFact]
    public async Task Coming_back_to_a_stale_candidate_reviews_it_again()
    {
        TestLogSink.Instance.Clear();
        using FixtureTree tree = new();
        ReviewQueue queue = QueueOf(tree, Seed, WithOne("Alpha, as machine 1 has it."), WithOne("Alpha, as machine 2 has it."));
        using ReviewHost host = new();
        ReviewSession session = await host.AwaitBuildAsync(() => new ReviewSession(queue, host.View));
        Assert.True(await host.AwaitBuildAsync(session.Next));

        Assert.Equal(TakeResult.Replaced, session.Take(SectionOf(session, "One")));
        AssertStale(session, "After a take of One from machine 2", new StaleSection("One", "machine 1"));

        Assert.True(await host.AwaitBuildAsync(session.Previous));
        AssertStale(session, "With machine 1 on the right again");

        // ⭐ Its second departure replaces its first: reviewed again, it was reviewed against machine 2's One.
        Assert.True(await host.AwaitBuildAsync(session.Next));
        AssertStale(session, "After machine 1 left the right pane a second time");

        TestLogSink.AssertNoWarnings(LogArea.Binding);
    }

    [AvaloniaFact]
    public async Task A_candidate_never_on_the_right_is_never_stale()
    {
        TestLogSink.Instance.Clear();
        using FixtureTree tree = new();
        ReviewQueue queue = QueueOf(
            tree,
            Seed,
            WithOne("Alpha, as machine 1 has it."),
            WithOne("Alpha, as machine 2 has it."),
            WithOne("Alpha, as machine 3 has it."));
        using ReviewHost host = new();
        ReviewSession session = await host.AwaitBuildAsync(() => new ReviewSession(queue, host.View));
        Assert.True(await host.AwaitBuildAsync(session.Next));

        Assert.Equal(TakeResult.Replaced, session.Take(SectionOf(session, "One")));

        // Machine 3 carries a One of its own too, and will be reviewed against the accumulator as it then stands.
        AssertStale(session, "After a take of One from machine 2, machine 3 not yet shown", new StaleSection("One", "machine 1"));

        TestLogSink.AssertNoWarnings(LogArea.Binding);
    }

    [AvaloniaFact]
    public async Task A_section_the_accumulator_has_dropped_stales_nothing()
    {
        TestLogSink.Instance.Clear();
        using FixtureTree tree = new();
        ReviewQueue queue = QueueOf(
            tree,
            Seed,
            WithOne("Alpha, as machine 1 has it."),
            Doc("# Rules", "", "## One", "", "Alpha.", "", "## Two", "", "Beta, as machine 2 has it.", ""));
        using ReviewHost host = new();
        ReviewSession session = await host.AwaitBuildAsync(() => new ReviewSession(queue, host.View));
        Assert.True(await host.AwaitBuildAsync(session.Next));

        session.Drop("One");

        // Machine 1 carries a One of its own, and the accumulator holds no One at all now: a drop decides the rule
        // whole, and staling every candidate carrying it would be decision 11's wall again.
        Assert.DoesNotContain("One", MarkdownOutline.Read(host.View.LeftDocument.Text).Sections.Select(s => s.HeadingPath));
        AssertStale(session, "After One was dropped from the accumulator");

        TestLogSink.AssertNoWarnings(LogArea.Binding);
    }

    [AvaloniaFact]
    public async Task A_path_the_accumulator_holds_twice_is_read_as_both_its_sections()
    {
        TestLogSink.Instance.Clear();
        using FixtureTree tree = new();
        string twice = Doc("# Rules", "", "## Notes", "", "First.", "", "## Notes", "", "Second.", "");
        ReviewQueue queue = QueueOf(
            tree,
            twice,
            Doc("# Rules", "", "## Notes", "", "A note of machine 1's.", ""),
            Doc("# Rules", "", "## Notes", "", "First.", "", "## Notes", "", "Second, as machine 2 has it.", ""));
        using ReviewHost host = new();
        ReviewSession session = await host.AwaitBuildAsync(() => new ReviewSession(queue, host.View));
        Assert.True(await host.AwaitBuildAsync(session.Next));

        host.Type(host.Left, twice.IndexOf("Second.", StringComparison.Ordinal) + "Second.".Length, " Typed.");
        Assert.True(
            host.View.LeftDocument.Text == twice.Replace("Second.", "Second. Typed.", StringComparison.Ordinal),
            "The left refused the keystroke.");

        // Only the second of the two moved, and a path is compared as everything held at it: a take refuses a path
        // held twice, and staleness, which only warns, reads it instead.
        AssertStale(session, "After typing in the second of two sections at Notes", new StaleSection("Notes", "machine 1"));

        TestLogSink.AssertNoWarnings(LogArea.Binding);
    }

    [Fact]
    public void The_session_store_refuses_a_missing_accumulator_or_queue_by_name()
    {
        SessionStore store = new();
        MarkdownOutline accumulator = MarkdownOutline.Read(Seed);

        Assert.Equal("accumulator", Assert.Throws<ArgumentNullException>(() => store.MoveTo(1, null!)).ParamName);
        Assert.Equal(0, store.Position);
        Assert.Equal("queue", Assert.Throws<ArgumentNullException>(() => store.StaleIn(null!, accumulator)).ParamName);
        Assert.Equal("accumulator", Assert.Throws<ArgumentNullException>(() => store.StaleIn([], null!)).ParamName);
    }
}
