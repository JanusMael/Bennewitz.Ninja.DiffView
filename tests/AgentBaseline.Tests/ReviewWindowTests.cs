using System.Text;
using Avalonia.Automation.Peers;
using Avalonia.Automation.Provider;
using Avalonia.Controls;
using Avalonia.Headless.XUnit;
using Avalonia.Input;
using Avalonia.Logging;
using Bennewitz.Ninja.DiffView;
using Bennewitz.Ninja.DiffView.Core;
using Bennewitz.Ninja.DiffView.Tests;

namespace Bennewitz.Ninja.AgentBaseline.Tests;

/// <summary>
/// Plan 00034 step 7: the loop end to end, as the operator drives it — the window's commands, and
/// cycling by the next command.
/// </summary>
/// <remarks>
/// ⭐ <b>Every step goes through the window.</b> A button is clicked through its automation peer, as a
/// harness or a screen reader acts on it; the next command is a key pressed in a pane; a caret is put
/// where a click in the text would put it. A test calling the session instead passes over a window whose
/// buttons are wired to nothing, and that window is what this step exists to check.
/// </remarks>
public sealed class ReviewWindowTests
{
    /// <summary>
    /// The seed: One at line 3, Three at 7, Four at 11, under a title at line 1.
    /// </summary>
    private const string Seed = "# Rules\n\n## One\n\nAlpha.\n\n## Three\n\nGamma.\n\n## Four\n\nDelta.\n";

    /// <summary>One reworded, its body at line 5.</summary>
    private const string OneReworded = "# Rules\n\n## One\n\nAlpha, reworded.\n\n## Three\n\nGamma.\n\n## Four\n\nDelta.\n";

    /// <summary>
    /// Two, new, at lines 7 to 10 between One and Three, and Four reworded — two changes.
    /// </summary>
    private const string TwoAdded =
        "# Rules\n\n## One\n\nAlpha.\n\n## Two\n\nBeta, new here.\n\n## Three\n\nGamma.\n\n## Four\n\nDelta, reworded.\n";

    /// <summary>
    /// <paramref name="seed"/> and <paramref name="candidates"/>, reviewed in the order given, so a
    /// position names a known candidate — <c>machine 1</c>, <c>machine 2</c> and so on.
    /// </summary>
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

    /// <summary>
    /// Clicks <paramref name="button"/> through its automation peer — the click a pointer makes, without a
    /// coordinate to get wrong, and refused as a pointer's would be while the button is disabled.
    /// </summary>
    private static void Click(Button button)
    {
        ((IInvokeProvider)ControlAutomationPeer.CreatePeerForElement(button)).Invoke();
        ReviewHost.Layout();
    }

    private static void PressNext(ReviewHost host, DiffPanePresenter pane) =>
        host.Press(pane, Key.F8, RawInputModifiers.None, PhysicalKey.F8);

    private static void PressPrevious(ReviewHost host, DiffPanePresenter pane) =>
        host.Press(pane, Key.F8, RawInputModifiers.Shift, PhysicalKey.F8);

    [AvaloniaFact]
    public async Task The_loop_end_to_end_takes_a_section_cycles_by_the_next_command_takes_another_drops_one_and_saves()
    {
        TestLogSink.Instance.Clear();
        using FixtureTree tree = new();
        ReviewQueue queue = QueueOf(tree, Seed, OneReworded, TwoAdded);
        byte[] seedBytes = File.ReadAllBytes(queue.Seed.Path);
        string output = Path.Combine(tree.MakeDirectory("Baseline"), "AGENTS.md");
        ReviewWindow window = new() { ChooseSavePath = () => Task.FromResult<string?>(output) };
        using ReviewHost host = new(window);
        ReviewSession session = await host.AwaitBuildAsync(() => window.Review(queue));

        // Take: machine 1's One, read at the right pane's caret.
        ReviewHost.PutCaret(host.Right, 5);
        Click(window.TakeButton);
        Assert.Equal("Took \"One\" from \"machine 1\" in place of the baseline's version.", window.OutcomeText.Text);

        // ⭐ Cycle with the next command: F8, pressed in the right pane, where the reading is.
        await host.AwaitBuildAsync(() => PressNext(host, host.Right));
        Assert.Equal(1, session.Position);
        Assert.Equal(TwoAdded, host.View.RightDocument.Text);

        // Take another: machine 2's Two, new to the baseline.
        ReviewHost.PutCaret(host.Right, 9);
        Click(window.TakeButton);

        // Drop one: the accumulator's own Four, read at the left pane's caret — its body is line 17 once
        // Two is in.
        ReviewHost.PutCaret(host.Left, 17);
        Click(window.DropButton);

        Click(window.SaveButton);

        Assert.Equal(
            Encoding.UTF8.GetBytes("# Rules\n\n## One\n\nAlpha, reworded.\n\n## Two\n\nBeta, new here.\n\n## Three\n\nGamma.\n"),
            File.ReadAllBytes(output));
        Assert.Equal(
            $"# Provenance of AGENTS.md\n\nSeeded from \"seed\", `{queue.Seed.Path}`.\n\n"
            + "## The queue, in review order\n\n| Candidate | File |\n|---|---|\n"
            + $"| machine 1 | `{queue.Items[0].Candidate.Path}` |\n"
            + $"| machine 2 | `{queue.Items[1].Candidate.Path}` |\n\n"
            + "## Decisions, in the order made\n\n| # | Decision | Section | From |\n|---|---|---|---|\n"
            + "| 1 | taken | One | machine 1 |\n"
            + "| 2 | taken | Two | machine 2 |\n"
            + "| 3 | dropped | Four | — |\n",
            File.ReadAllText(Path.Combine(tree.PathOf("Baseline"), "AGENTS.provenance.md")));

        // ⛔ Decision 9: the seed machine's own file is read and never written.
        Assert.Equal(seedBytes, File.ReadAllBytes(queue.Seed.Path));
        Assert.Equal($"Saved the baseline to {output}, and its provenance to {Path.ChangeExtension(output, ".provenance.md")}.", window.OutcomeText.Text);

        TestLogSink.AssertNoWarnings(LogArea.Binding);
    }

    [AvaloniaFact]
    public async Task The_next_command_moves_the_right_from_either_pane_and_at_either_end_says_so_and_moves_nothing()
    {
        TestLogSink.Instance.Clear();
        using FixtureTree tree = new();
        ReviewQueue queue = QueueOf(tree, Seed, OneReworded, TwoAdded);
        ReviewWindow window = new();
        using ReviewHost host = new(window);
        ReviewSession session = await host.AwaitBuildAsync(() => window.Review(queue));
        Assert.Equal("Candidate 1 of 2: \"machine 1\", 1 change against the seed \"seed\"", window.PositionText.Text);

        // From the left: the bindings are the window's, so which pane has the keyboard does not matter.
        await host.AwaitBuildAsync(() => PressNext(host, host.Left));
        Assert.Equal(1, session.Position);
        Assert.Equal("Candidate 2 of 2: \"machine 2\", 2 changes against the seed \"seed\"", window.PositionText.Text);

        PaneSource? shown = host.View.RightSource;
        PressNext(host, host.Right);
        Assert.Equal(1, session.Position);
        Assert.Same(shown, host.View.RightSource);
        Assert.Equal("\"machine 2\" is the last candidate, so there is no next one.", window.OutcomeText.Text);

        await host.AwaitBuildAsync(() => PressPrevious(host, host.Right));
        Assert.Equal(0, session.Position);
        Assert.Equal(OneReworded, host.View.RightDocument.Text);
        Assert.Equal("Candidate 1 of 2: \"machine 1\", 1 change against the seed \"seed\"", window.PositionText.Text);
        Assert.True(window.OutcomeText.Text?.Length is null or 0, "A move left the last outcome standing beside a candidate it was not about.");

        shown = host.View.RightSource;
        PressPrevious(host, host.Left);
        Assert.Equal(0, session.Position);
        Assert.Same(shown, host.View.RightSource);
        Assert.Equal("\"machine 1\" is the first candidate, so there is no previous one.", window.OutcomeText.Text);

        // The buttons are the same two commands.
        await host.AwaitBuildAsync(() => Click(window.NextButton));
        Assert.Equal(1, session.Position);
        await host.AwaitBuildAsync(() => Click(window.PreviousButton));
        Assert.Equal(0, session.Position);

        TestLogSink.AssertNoWarnings(LogArea.Binding);
    }

    [AvaloniaFact]
    public async Task Each_verb_reads_the_section_at_its_own_panes_caret()
    {
        TestLogSink.Instance.Clear();
        using FixtureTree tree = new();
        ReviewQueue queue = QueueOf(tree, Seed, TwoAdded);
        ReviewWindow window = new();
        using ReviewHost host = new(window);
        ReviewSession session = await host.AwaitBuildAsync(() => window.Review(queue));

        // ⛔ The two carets sit on different sections, and line 7 is a different section in each pane — the
        // accumulator's Three, the candidate's Two — so a verb reading the wrong pane, or the right pane's
        // line in the wrong document, acts on a section it was not pointed at.
        ReviewHost.PutCaret(host.Left, 7);
        ReviewHost.PutCaret(host.Right, 3);
        Click(window.DropButton);
        Assert.Equal("# Rules\n\n## One\n\nAlpha.\n\n## Four\n\nDelta.\n", host.View.LeftDocument.Text);
        Assert.Equal("Dropped \"Three\" from the baseline.", window.OutcomeText.Text);

        // Line 10 is the last of Two's lines, the blank one before Three: a section runs to there.
        ReviewHost.PutCaret(host.Left, 3);
        ReviewHost.PutCaret(host.Right, 10);
        Click(window.TakeButton);
        Assert.Equal("# Rules\n\n## One\n\nAlpha.\n\n## Two\n\nBeta, new here.\n\n## Four\n\nDelta.\n", host.View.LeftDocument.Text);
        Assert.Equal("Took \"Two\" from \"machine 1\", new to the baseline.", window.OutcomeText.Text);

        // The candidate's One reads as the accumulator's does, so a take of it writes nothing — and says so.
        string before = host.View.LeftDocument.Text;
        ReviewHost.PutCaret(host.Right, 5);
        Click(window.TakeButton);
        Assert.Equal(before, host.View.LeftDocument.Text);
        Assert.Equal("Nothing changed: \"One\" from \"machine 1\" is already the baseline's version.", window.OutcomeText.Text);

        Assert.Equal(
            [DecisionKind.Dropped, DecisionKind.Taken, DecisionKind.Taken],
            session.Durable.Decisions.Select(d => d.Kind));

        TestLogSink.AssertNoWarnings(LogArea.Binding);
    }

    [AvaloniaFact]
    public async Task Leave_and_defer_from_the_window_reach_the_store_each_belongs_to_and_touch_no_text()
    {
        TestLogSink.Instance.Clear();
        using FixtureTree tree = new();
        ReviewQueue queue = QueueOf(tree, Seed, OneReworded);
        ReviewWindow window = new();
        using ReviewHost host = new(window);
        ReviewSession session = await host.AwaitBuildAsync(() => window.Review(queue));
        MarkdownOutline candidate = queue.Items[0].Candidate.Document.Outline;

        ReviewHost.PutCaret(host.Right, 5);
        Click(window.LeaveButton);
        Assert.Equal("Left \"One\" from \"machine 1\" out of the baseline.", window.OutcomeText.Text);

        // The title's own heading line: a section begins at its heading.
        ReviewHost.PutCaret(host.Right, 1);
        Click(window.DeferButton);
        Assert.Equal("Put the title from \"machine 1\" off for this session.", window.OutcomeText.Text);

        Assert.Equal(
            [new SectionDecision(DecisionKind.Rejected, SectionIdentity.Of(candidate.Sections[1]), "machine 1", null)],
            session.Durable.Decisions);
        Assert.Equal([new DeferredSection(SectionIdentity.Of(candidate.Sections[0]), "machine 1")], session.Session.Deferred);
        Assert.Equal(Seed, host.View.LeftDocument.Text);

        TestLogSink.AssertNoWarnings(LogArea.Binding);
    }

    [AvaloniaFact]
    public async Task A_caret_above_a_panes_first_heading_decides_nothing_and_says_why()
    {
        TestLogSink.Instance.Clear();
        using FixtureTree tree = new();
        string seed = "<!-- kept above every heading -->\n\n# Rules\n\n## One\n\nAlpha.\n";
        ReviewQueue queue = QueueOf(tree, seed, "<!-- machine 1's -->\n\n# Rules\n\n## One\n\nAlpha, reworded.\n");
        ReviewWindow window = new();
        using ReviewHost host = new(window);
        ReviewSession session = await host.AwaitBuildAsync(() => window.Review(queue));
        ReviewHost.PutCaret(host.Left, 1);
        ReviewHost.PutCaret(host.Right, 1);

        Click(window.TakeButton);
        Assert.Equal("Nothing to take: the caret in the right pane is above its first heading, in no section.", window.OutcomeText.Text);
        Click(window.LeaveButton);
        Assert.Equal("Nothing to leave: the caret in the right pane is above its first heading, in no section.", window.OutcomeText.Text);
        Click(window.DeferButton);
        Assert.Equal("Nothing to defer: the caret in the right pane is above its first heading, in no section.", window.OutcomeText.Text);
        Click(window.DropButton);
        Assert.Equal("Nothing to drop: the caret in the left pane is above its first heading, in no section.", window.OutcomeText.Text);

        Assert.Equal(seed, host.View.LeftDocument.Text);
        Assert.Empty(session.Durable.Decisions);
        Assert.Empty(session.Session.Deferred);

        TestLogSink.AssertNoWarnings(LogArea.Binding);
    }

    [AvaloniaFact]
    public async Task A_refused_decision_is_said_in_the_window_and_writes_nothing()
    {
        TestLogSink.Instance.Clear();
        using FixtureTree tree = new();

        // Machine 1's Detail sits under a Five the accumulator does not hold: its body is line 9.
        ReviewQueue queue = QueueOf(tree, Seed, "# Rules\n\n## Five\n\nEpsilon.\n\n### Detail\n\nFine print.\n");
        ReviewWindow window = new();
        using ReviewHost host = new(window);
        ReviewSession session = await host.AwaitBuildAsync(() => window.Review(queue));

        ReviewHost.PutCaret(host.Right, 9);
        Click(window.TakeButton);
        Assert.StartsWith("Not done: \"Detail\" goes under \"Five\", which the accumulator does not hold", window.OutcomeText.Text, StringComparison.Ordinal);
        Assert.Equal(Seed, host.View.LeftDocument.Text);
        Assert.False(host.Left.CanUndo, "A refused take wrote to the accumulator and took it back.");
        Assert.Empty(session.Durable.Decisions);

        // A deferral of a version already decided is refused the same way.
        ReviewHost.PutCaret(host.Right, 5);
        Click(window.LeaveButton);
        Click(window.DeferButton);
        Assert.StartsWith("Not done: this version of \"Five\" is already left", window.OutcomeText.Text, StringComparison.Ordinal);
        Assert.Empty(session.Session.Deferred);

        TestLogSink.AssertNoWarnings(LogArea.Binding);
    }

    [AvaloniaFact]
    public async Task Before_a_review_every_command_is_disabled_and_a_window_holds_one_review()
    {
        TestLogSink.Instance.Clear();
        using FixtureTree tree = new();
        ReviewWindow window = new();
        using ReviewHost host = new(window);
        Button[] buttons =
        [
            window.PreviousButton, window.NextButton, window.TakeButton, window.LeaveButton,
            window.DeferButton, window.DropButton, window.SaveButton,
        ];
        Assert.All(buttons, b => Assert.False(b.IsEffectivelyEnabled, $"{b.Name} is enabled with no review to act on."));

        // The next command with nothing to move through moves nothing and throws nothing.
        PressNext(host, host.Right);
        Assert.Null(window.Session);

        // A queue holding only the seed is refused, and the window is left as it was.
        Assert.Throws<CorpusRefusedException>(() => window.Review(QueueOf(tree, Seed)));
        Assert.Null(window.Session);
        Assert.All(buttons, b => Assert.False(b.IsEffectivelyEnabled, $"{b.Name} is enabled after a refused review."));

        ReviewQueue queue = QueueOf(tree, Seed, OneReworded);
        ReviewSession session = await host.AwaitBuildAsync(() => window.Review(queue));
        Assert.All(buttons, b => Assert.True(b.IsEffectivelyEnabled, $"{b.Name} stayed disabled once the review began."));

        Assert.Throws<InvalidOperationException>(() => window.Review(queue));
        Assert.Same(session, window.Session);

        TestLogSink.AssertNoWarnings(LogArea.Binding);
    }
}
