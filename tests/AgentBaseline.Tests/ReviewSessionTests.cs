using Avalonia.Headless.XUnit;
using Avalonia.Logging;
using Bennewitz.Ninja.DiffView;
using Bennewitz.Ninja.DiffView.Core;
using Bennewitz.Ninja.DiffView.Tests;

namespace Bennewitz.Ninja.AgentBaseline.Tests;

/// <summary>
/// Plan 00034 step 5: cycling with the accumulator intact — edit the left, cycle the right twice, and
/// the left's text stands and an undo still reverses the edit.
/// </summary>
/// <remarks>
/// ⛔ <b>Every edit here is typed.</b> An edit made straight on <c>TextDocument</c> is never refused, so a
/// session that left the accumulator read-only would pass a test that edited the document directly —
/// and the person at the keyboard could not type into it.
/// </remarks>
public sealed class ReviewSessionTests
{
    private const string Seed = "# Rules\n\n## One\n\nAlpha.\n\n## Two\n\nBeta.\n";

    private const string First = "# Rules\n\n## One\n\nAlpha, from the first.\n\n## Two\n\nBeta.\n";

    private const string Second = "# Rules\n\n## One\n\nAlpha.\n\n## Two\n\nBeta, from the second.\n";

    private const string Third = "# Rules\n\n## One\n\nAlpha.\n\n## Two\n\nBeta.\n\n## Three\n\nGamma, from the third.\n";

    /// <summary><see cref="First"/> as JanusHalo's file is shaped: one list item, every line indented two spaces.</summary>
    private const string FirstWrapped =
        "- # Rules\n  \n  ## One\n  \n  Alpha, from the first.\n  \n  ## Two\n  \n  Beta.\n";

    private const string Edit = "Kept. ";

    /// <summary>
    /// The seed and <paramref name="candidates"/>, reviewed in the order given, so a position names a
    /// known candidate — <c>machine 1</c>, <c>machine 2</c> and so on.
    /// </summary>
    private static (ReviewQueue Queue, string SeedPath) QueueOf(FixtureTree tree, params string[] candidates)
    {
        string seedPath = tree.Write("Seed/AGENTS.md", Seed);
        List<CorpusEntry> entries = [Entry(seedPath, "seed")];
        for (int i = 0; i < candidates.Length; i++)
        {
            entries.Add(Entry(tree.Write($"Machine{i + 1}/AGENTS.md", candidates[i]), $"machine {i + 1}"));
        }

        Corpus corpus = Corpus.Gather(CorpusRequest.Create(entries, seed: "seed", order: CorpusOrder.Listed));
        return (corpus.Queue(cancellationToken: TestContext.Current.CancellationToken), seedPath);
    }

    private static CorpusEntry Entry(string path, string label) =>
        new(path, label, new CandidateTarget(Path.GetFileName(path), null));

    [AvaloniaFact]
    public async Task Editing_the_left_and_cycling_the_right_twice_keeps_the_edit_and_its_undo()
    {
        TestLogSink.Instance.Clear();
        using FixtureTree tree = new();
        (ReviewQueue queue, _) = QueueOf(tree, First, Second, Third);
        using ReviewHost host = new();
        ReviewSession session = await host.AwaitBuildAsync(() => new ReviewSession(queue, host.View));

        host.Type(host.Left, 0, Edit);
        Assert.True(
            host.View.LeftDocument.Text == Edit + Seed,
            "The left refused the keystroke, so nobody can edit the accumulator by hand.");

        Assert.True(await host.AwaitBuildAsync(session.Next));
        Assert.True(await host.AwaitBuildAsync(session.Next));

        Assert.Equal(2, session.Position);
        Assert.Equal(Third, host.View.RightDocument.Text);
        Assert.True(
            host.View.LeftDocument.Text == Edit + Seed,
            "Cycling the right changed the left's text, so an edit made to the accumulator did not survive.");

        // ⛔ The text alone is not the proof. A left assigned again with its own edited text reads the same,
        // and has lost its undo stack, because assigning a source replaces the document (AGENTS.md §6).
        Assert.True(
            host.Left.CanUndo,
            "Cycling the right kept the left's text but emptied its undo stack: the left's document was replaced.");
        host.Left.Undo();
        ReviewHost.Layout();
        Assert.True(host.View.LeftDocument.Text == Seed, "An undo after cycling did not reverse the edit.");

        TestLogSink.AssertNoWarnings(LogArea.Binding);
    }

    [AvaloniaFact]
    public async Task A_candidate_on_the_right_refuses_a_keystroke()
    {
        TestLogSink.Instance.Clear();
        using FixtureTree tree = new();
        (ReviewQueue queue, _) = QueueOf(tree, First, Second);
        using ReviewHost host = new();
        await host.AwaitBuildAsync(() => new ReviewSession(queue, host.View));

        host.Type(host.Right, 0, Edit);

        // What a take records is the candidate's own version (decision 9c), so the right is never edited.
        Assert.True(
            host.View.RightDocument.Text == First,
            "The right took a keystroke, so a candidate can be changed before anything is taken from it.");

        TestLogSink.AssertNoWarnings(LogArea.Binding);
    }

    [AvaloniaFact]
    public async Task The_view_cannot_save_the_accumulator_over_the_seeds_own_file()
    {
        TestLogSink.Instance.Clear();
        using FixtureTree tree = new();
        (ReviewQueue queue, string seedPath) = QueueOf(tree, First, Second);
        byte[] before = File.ReadAllBytes(seedPath);
        using ReviewHost host = new();
        await host.AwaitBuildAsync(() => new ReviewSession(queue, host.View));

        host.Type(host.Left, 0, Edit);
        Assert.True(
            host.View.IsDirty(DiffSide.Left),
            "The edit did not mark the left dirty, so the assertions below would hold of any left at all.");

        // ⛔ Decision 9. The header's menu offers Save exactly when CanSave says so, and a save writes the
        // side's source path — for a left built from the seed's file, that machine's live file.
        Assert.False(
            host.View.CanSave(DiffSide.Left),
            "The view offers to save the edited accumulator, and a save would write it over the seed's own file.");
        Assert.Equal(SaveOutcome.NoPath, host.View.Save(DiffSide.Left));
        Assert.Equal(before, File.ReadAllBytes(seedPath));

        // The right cannot become dirty while it refuses a keystroke, so CanSave says nothing about it:
        // the source itself is what must carry no path.
        Assert.True(
            host.View.RightSource?.Path is null,
            "The right's source carries the candidate's path, which a save of the right would write to.");

        TestLogSink.AssertNoWarnings(LogArea.Binding);
    }

    [AvaloniaFact]
    public async Task Next_and_previous_stop_at_the_ends_and_a_jump_shows_the_candidate_it_names()
    {
        TestLogSink.Instance.Clear();
        using FixtureTree tree = new();
        (ReviewQueue queue, _) = QueueOf(tree, First, Second, Third);
        using ReviewHost host = new();
        ReviewSession session = await host.AwaitBuildAsync(() => new ReviewSession(queue, host.View));

        Assert.Equal(0, session.Position);
        Assert.Equal(First, host.View.RightDocument.Text);

        PaneSource? shown = host.View.RightSource;
        Assert.False(session.Previous());
        Assert.Equal(0, session.Position);
        Assert.Same(shown, host.View.RightSource);

        await host.AwaitBuildAsync(() => session.JumpTo(2));
        Assert.Equal(2, session.Position);
        Assert.Same(queue.Items[2], session.Current);
        Assert.Equal(Third, host.View.RightDocument.Text);

        shown = host.View.RightSource;
        Assert.False(session.Next());
        Assert.Equal(2, session.Position);
        Assert.Same(shown, host.View.RightSource);

        Assert.True(await host.AwaitBuildAsync(session.Previous));
        Assert.Equal(1, session.Position);
        Assert.Equal(Second, host.View.RightDocument.Text);

        TestLogSink.AssertNoWarnings(LogArea.Binding);
    }

    [AvaloniaFact]
    public async Task A_jump_past_either_end_is_refused_and_nothing_moves()
    {
        TestLogSink.Instance.Clear();
        using FixtureTree tree = new();
        (ReviewQueue queue, _) = QueueOf(tree, First, Second);
        using ReviewHost host = new();
        ReviewSession session = await host.AwaitBuildAsync(() => new ReviewSession(queue, host.View));
        PaneSource? shown = host.View.RightSource;

        Assert.Throws<ArgumentOutOfRangeException>(() => session.JumpTo(2));
        Assert.Throws<ArgumentOutOfRangeException>(() => session.JumpTo(-1));

        Assert.Equal(0, session.Position);
        Assert.Same(shown, host.View.RightSource);

        TestLogSink.AssertNoWarnings(LogArea.Binding);
    }

    [AvaloniaFact]
    public async Task Each_pane_is_titled_for_what_it_holds_and_both_end_in_the_targets_file_name()
    {
        TestLogSink.Instance.Clear();
        using FixtureTree tree = new();
        (ReviewQueue queue, _) = QueueOf(tree, First, Second);
        using ReviewHost host = new();
        ReviewSession session = await host.AwaitBuildAsync(() => new ReviewSession(queue, host.View));

        // The left is the baseline being made, not the seed's file, so it is not titled as the seed. Both
        // end in the file name because the control chooses a pane's grammar from the title's extension.
        Assert.Equal("baseline from seed — AGENTS.md", host.View.LeftSource?.Title);
        Assert.Equal("machine 1 — AGENTS.md", host.View.RightSource?.Title);

        Assert.True(await host.AwaitBuildAsync(session.Next));
        Assert.Equal("machine 2 — AGENTS.md", host.View.RightSource?.Title);

        TestLogSink.AssertNoWarnings(LogArea.Binding);
    }

    [AvaloniaFact]
    public async Task Each_pane_shows_its_documents_normalised_text_and_not_its_bytes()
    {
        TestLogSink.Instance.Clear();
        using FixtureTree tree = new();

        // ⭐ The two shapes plan 00034 §2 measured: one machine's file in CRLF, another's wholly nested in a
        // list item. The queue counted each candidate's changes over normalised text, so the panes show that
        // text too — or the review is a different diff from the one the queue was ordered by.
        string seedPath = tree.Write("Seed/AGENTS.md", Seed.Replace("\n", "\r\n", StringComparison.Ordinal));
        string wrappedPath = tree.Write("Machine1/AGENTS.md", FirstWrapped);
        Corpus corpus = Corpus.Gather(CorpusRequest.Create(
            [Entry(seedPath, "seed"), Entry(wrappedPath, "machine 1")], seed: "seed", order: CorpusOrder.Listed));
        ReviewQueue queue = corpus.Queue(cancellationToken: TestContext.Current.CancellationToken);
        using ReviewHost host = new();
        await host.AwaitBuildAsync(() => new ReviewSession(queue, host.View));

        Assert.Equal(Seed, host.View.LeftDocument.Text);
        Assert.Equal(First, host.View.RightDocument.Text);

        TestLogSink.AssertNoWarnings(LogArea.Binding);
    }

    [AvaloniaFact]
    public void A_session_without_a_queue_or_a_view_is_refused_by_name()
    {
        TestLogSink.Instance.Clear();
        using FixtureTree tree = new();
        (ReviewQueue queue, _) = QueueOf(tree, First);
        using ReviewHost host = new();

        Assert.Equal("queue", Assert.Throws<ArgumentNullException>(() => new ReviewSession(null!, host.View)).ParamName);
        Assert.Equal("view", Assert.Throws<ArgumentNullException>(() => new ReviewSession(queue, null!)).ParamName);

        TestLogSink.AssertNoWarnings(LogArea.Binding);
    }

    [AvaloniaFact]
    public void A_queue_holding_only_the_seed_is_refused_before_the_view_is_touched()
    {
        TestLogSink.Instance.Clear();
        using FixtureTree tree = new();
        (ReviewQueue queue, _) = QueueOf(tree);
        using ReviewHost host = new();

        CorpusRefusedException refused = Assert.Throws<CorpusRefusedException>(() => new ReviewSession(queue, host.View));

        Assert.Contains("nothing to review", refused.Message, StringComparison.Ordinal);
        Assert.Null(host.View.LeftSource);
        Assert.True(host.View.LeftReadOnly, "The refused session opened the left for editing before it refused.");

        TestLogSink.AssertNoWarnings(LogArea.Binding);
    }
}
