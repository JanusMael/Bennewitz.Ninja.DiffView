using Avalonia.Headless.XUnit;
using Avalonia.Logging;
using Bennewitz.Ninja.DiffView.Core;
using Bennewitz.Ninja.DiffView.Tests;

namespace Bennewitz.Ninja.AgentBaseline.Tests;

/// <summary>
/// Plan 00034 step 6: take, leave, drop and defer, over a real view, and the stores they write to.
/// </summary>
/// <remarks>
/// <para>
/// ⭐ <b>The negative is the point.</b> A take is judged by what it did <i>not</i> change: every other
/// section of the accumulator byte-identical afterwards, over a candidate whose sections are in another
/// order, so the line diff sets the taken section beside a different one. A test that the left gained
/// the taken lines passes while the left silently loses a rule, which is what draft 1's take — a copy
/// over the diff's alignment — would have done.
/// </para>
/// <para>
/// ⛔ The one edit typed here is typed, for <see cref="ReviewSessionTests"/>' reason: an edit made
/// straight on the document is never refused, so it could not show a read-only accumulator.
/// </para>
/// </remarks>
public sealed class SectionDecisionTests
{
    /// <summary>
    /// The accumulator's starting text, its sections in an order no candidate keeps: Two is last.
    /// </summary>
    private static readonly string Seed = Doc(
        "# Rules", "", "## One", "", "Alpha.", "", "## Three", "", "Gamma.", "", "## Four", "", "Delta.", "", "## Two", "", "Beta.", "");

    /// <summary>
    /// Three gone, Two moved up into its place and reworded. The line diff pairs this Two with the
    /// accumulator's Three, which is what makes a copy over the alignment destroy a rule.
    /// </summary>
    private static readonly string Reordered = Doc(
        "# Rules", "", "## One", "", "Alpha.", "", "## Two", "", "Beta, reworded and moved up.", "", "## Four", "", "Delta.", "");

    /// <summary>One and Two reworded, in the accumulator's order.</summary>
    private static readonly string Reworded = Doc(
        "# Rules", "", "## One", "", "Alpha, reworded.", "", "## Two", "", "Beta, reworded.", "");

    /// <summary>Machine 3: a section under a heading the accumulator does not have.</summary>
    private static readonly string Orphaned = Doc(
        "# Rules", "", "## Five", "", "Epsilon.", "", "### Detail", "", "Fine print.", "");

    private static string Doc(params string[] lines) => string.Join("\n", lines);

    private static ReviewQueue QueueOf(FixtureTree tree, params string[] candidates)
    {
        List<CorpusEntry> entries = [Entry(tree.Write("Seed/AGENTS.md", Seed), "seed")];
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

    [AvaloniaFact]
    public async Task Taking_a_section_from_a_reordered_candidate_leaves_every_other_section_of_the_accumulator_byte_identical()
    {
        TestLogSink.Instance.Clear();
        using FixtureTree tree = new();
        ReviewQueue queue = QueueOf(tree, Reordered);
        using ReviewHost host = new();
        ReviewSession session = await host.AwaitBuildAsync(() => new ReviewSession(queue, host.View));

        // ⛔ The premise, asserted rather than assumed: the diff really does set the candidate's Two beside
        // the accumulator's Three. Were it to stop, a take positioned by the alignment would pass too.
        MarkdownSection two = SectionOf(session, "Two");
        ChangeBlock beside = host.View.Document!.Blocks.Single(b =>
            b.LinesFor(DiffSide.Right).Start <= two.FirstLine - 1
            && two.FirstLine - 1 < b.LinesFor(DiffSide.Right).Start + b.LinesFor(DiffSide.Right).Count);
        int seedThree = MarkdownOutline.Read(Seed).Sections.Single(s => s.HeadingPath == "Three").FirstLine - 1;
        LineRange left = beside.LinesFor(DiffSide.Left);
        Assert.True(
            left.Start <= seedThree && seedThree < left.Start + left.Count,
            "The fixture no longer misaligns: the diff no longer sets the candidate's Two beside the accumulator's "
            + "Three, so this test would pass for a take placed by the alignment as well.");

        Assert.Equal(TakeResult.Replaced, session.Take(two));

        MarkdownOutline before = MarkdownOutline.Read(Seed);
        MarkdownOutline after = MarkdownOutline.Read(host.View.LeftDocument.Text);
        Assert.Equal(before.Preamble, after.Preamble);
        Assert.Equal(before.Sections.Count, after.Sections.Count);
        for (int i = 0; i < before.Sections.Count; i++)
        {
            MarkdownSection was = before.Sections[i];
            MarkdownSection now = after.Sections[i];
            if (was.HeadingPath == "Two")
            {
                Assert.Equal(two.Content, now.Content);
                continue;
            }

            Assert.True(
                was.HeadingPath == now.HeadingPath && was.Text == now.Text,
                $"\"{was.HeadingPath}\" is not byte-identical after the take: it reads \"{now.HeadingPath}\", \"{now.Text}\".");
        }

        Assert.Equal(
            Doc("# Rules", "", "## One", "", "Alpha.", "", "## Three", "", "Gamma.", "", "## Four", "", "Delta.", "", "## Two", "", "Beta, reworded and moved up.", ""),
            host.View.LeftDocument.Text);

        // What the take records: decision 9c's source, identity, and the text taken.
        SectionDecision taken = Assert.Single(session.Durable.Decisions);
        Assert.Equal(new SectionDecision(DecisionKind.Taken, SectionIdentity.Of(two), "machine 1", two.Content), taken);

        TestLogSink.AssertNoWarnings(LogArea.Binding);
    }

    [AvaloniaFact]
    public async Task A_take_is_one_undo_step_and_the_edits_typed_before_it_stand()
    {
        TestLogSink.Instance.Clear();
        using FixtureTree tree = new();
        ReviewQueue queue = QueueOf(tree, Reordered);
        using ReviewHost host = new();
        ReviewSession session = await host.AwaitBuildAsync(() => new ReviewSession(queue, host.View));

        host.Type(host.Left, Seed.IndexOf("Alpha.", StringComparison.Ordinal) + "Alpha.".Length, " Kept.");
        string typed = host.View.LeftDocument.Text;
        Assert.True(typed == Seed.Replace("Alpha.", "Alpha. Kept.", StringComparison.Ordinal), "The left refused the keystroke.");

        session.Take(SectionOf(session, "Two"));
        Assert.NotEqual(typed, host.View.LeftDocument.Text);

        // ⛔ Through the document, not around it: a left replaced by a new source has no undo at all.
        host.Left.Undo();
        ReviewHost.Layout();
        Assert.True(host.View.LeftDocument.Text == typed, "One undo did not take the take back, and only the take.");
        host.Left.Undo();
        ReviewHost.Layout();
        Assert.True(host.View.LeftDocument.Text == Seed, "The edit typed before the take did not survive it.");

        TestLogSink.AssertNoWarnings(LogArea.Binding);
    }

    [AvaloniaFact]
    public async Task Dropping_a_section_removes_it_from_the_accumulator_and_records_the_version_removed()
    {
        TestLogSink.Instance.Clear();
        using FixtureTree tree = new();
        ReviewQueue queue = QueueOf(tree, Reordered);
        using ReviewHost host = new();
        ReviewSession session = await host.AwaitBuildAsync(() => new ReviewSession(queue, host.View));
        MarkdownSection ours = MarkdownOutline.Read(Seed).Sections.Single(s => s.HeadingPath == "Two");

        session.Drop("Two");

        Assert.Equal(
            Doc("# Rules", "", "## One", "", "Alpha.", "", "## Three", "", "Gamma.", "", "## Four", "", "Delta.", ""),
            host.View.LeftDocument.Text);

        // A drop is of the accumulator's own section — its version, not the one machine 1 shows beside
        // it — so it comes from no candidate, and carries no text.
        SectionDecision dropped = Assert.Single(session.Durable.Decisions);
        Assert.Equal(new SectionDecision(DecisionKind.Dropped, SectionIdentity.Of(ours), null, null), dropped);

        TestLogSink.AssertNoWarnings(LogArea.Binding);
    }

    [AvaloniaFact]
    public async Task Leave_is_durable_and_defer_is_the_sessions_and_neither_touches_the_accumulator()
    {
        TestLogSink.Instance.Clear();
        using FixtureTree tree = new();
        ReviewQueue queue = QueueOf(tree, Reworded);
        using ReviewHost host = new();
        ReviewSession session = await host.AwaitBuildAsync(() => new ReviewSession(queue, host.View));
        SectionIdentity one = SectionIdentity.Of(SectionOf(session, "One"));
        SectionIdentity two = SectionIdentity.Of(SectionOf(session, "Two"));

        session.Leave(SectionOf(session, "One"));
        session.Defer(SectionOf(session, "Two"));

        // ⛔ Decision 10's poisoning, ruled out: what is put off is in the session store and nowhere else.
        Assert.Equal([new SectionDecision(DecisionKind.Rejected, one, "machine 1", null)], session.Durable.Decisions);
        Assert.Null(session.Durable.LatestFor(two));
        Assert.Equal([new DeferredSection(two, "machine 1")], session.Session.Deferred);
        Assert.False(session.Session.IsDeferred(one));

        Assert.Equal(Seed, host.View.LeftDocument.Text);

        TestLogSink.AssertNoWarnings(LogArea.Binding);
    }

    [AvaloniaFact]
    public async Task A_decision_settles_a_deferral_and_a_deferral_will_not_hide_a_decision()
    {
        TestLogSink.Instance.Clear();
        using FixtureTree tree = new();
        ReviewQueue queue = QueueOf(tree, Reworded);
        using ReviewHost host = new();
        ReviewSession session = await host.AwaitBuildAsync(() => new ReviewSession(queue, host.View));
        MarkdownSection one = SectionOf(session, "One");
        MarkdownSection two = SectionOf(session, "Two");

        session.Defer(two);
        session.Take(two);
        Assert.Empty(session.Session.Deferred);
        Assert.Equal(DecisionKind.Taken, session.Durable.LatestFor(SectionIdentity.Of(two))?.Kind);

        session.Leave(one);
        DecisionRefusedException refused = Assert.Throws<DecisionRefusedException>(() => session.Defer(one));
        Assert.Contains("already left", refused.Message, StringComparison.Ordinal);
        Assert.False(session.Session.IsDeferred(SectionIdentity.Of(one)));

        // The refusal names what it refuses, and the title's path is empty: quoted, it would name nothing.
        MarkdownSection title = SectionOf(session, "");
        session.Leave(title);
        Assert.Contains(
            "this version of the title is already left",
            Assert.Throws<DecisionRefusedException>(() => session.Defer(title)).Message,
            StringComparison.Ordinal);

        TestLogSink.AssertNoWarnings(LogArea.Binding);
    }

    [AvaloniaFact]
    public async Task A_verb_given_a_section_the_right_does_not_show_is_refused_by_name()
    {
        TestLogSink.Instance.Clear();
        using FixtureTree tree = new();
        ReviewQueue queue = QueueOf(tree, Reordered, Reworded);
        using ReviewHost host = new();
        ReviewSession session = await host.AwaitBuildAsync(() => new ReviewSession(queue, host.View));
        MarkdownSection fromMachine1 = SectionOf(session, "Two");
        MarkdownSection titleFromMachine1 = SectionOf(session, "");
        Assert.True(await host.AwaitBuildAsync(session.Next));

        // A section is a handle on the right's own outline: one from the candidate shown before is not one —
        // not even a title that reads the same, line for line, in the candidate shown now.
        Assert.Equal(titleFromMachine1, SectionOf(session, ""));
        Assert.Equal("section", Assert.Throws<ArgumentException>(() => session.Take(fromMachine1)).ParamName);
        Assert.Equal("section", Assert.Throws<ArgumentException>(() => session.Leave(titleFromMachine1)).ParamName);
        Assert.Equal("section", Assert.Throws<ArgumentException>(() => session.Defer(fromMachine1)).ParamName);
        Assert.Equal("section", Assert.Throws<ArgumentNullException>(() => session.Take(null!)).ParamName);

        Assert.Empty(session.Durable.Decisions);
        Assert.Empty(session.Session.Deferred);
        Assert.Equal(Seed, host.View.LeftDocument.Text);

        TestLogSink.AssertNoWarnings(LogArea.Binding);
    }

    [AvaloniaFact]
    public async Task A_refused_take_writes_nothing_and_records_nothing()
    {
        TestLogSink.Instance.Clear();
        using FixtureTree tree = new();
        ReviewQueue queue = QueueOf(tree, Orphaned);
        using ReviewHost host = new();
        ReviewSession session = await host.AwaitBuildAsync(() => new ReviewSession(queue, host.View));

        Assert.Throws<DecisionRefusedException>(() => session.Take(SectionOf(session, "Five / Detail")));

        Assert.Equal(Seed, host.View.LeftDocument.Text);
        Assert.False(host.Left.CanUndo, "A refused take wrote to the accumulator and took it back.");
        Assert.Empty(session.Durable.Decisions);

        TestLogSink.AssertNoWarnings(LogArea.Binding);
    }
}
