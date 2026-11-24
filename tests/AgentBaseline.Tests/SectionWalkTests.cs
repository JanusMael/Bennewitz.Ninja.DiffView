using Avalonia;
using Avalonia.Automation;
using Avalonia.Automation.Peers;
using Avalonia.Automation.Provider;
using Avalonia.Controls;
using Avalonia.Headless;
using Avalonia.Headless.XUnit;
using Avalonia.Input;
using Avalonia.Logging;
using AvaloniaEdit.Editing;
using Bennewitz.Ninja.DiffView;
using Bennewitz.Ninja.DiffView.Core;
using Bennewitz.Ninja.DiffView.Tests;

namespace Bennewitz.Ninja.AgentBaseline.Tests;

/// <summary>
/// Plan 00038: <i>Previous section</i> and <i>Next section</i>, and Ctrl+P and Ctrl+N, walk the sections that differ,
/// each in the pane that has it, the right when both do — Brian's of 2026-11-19, for finding each section's mark without
/// scrolling and clicking.
/// </summary>
/// <remarks>
/// The walk itself is read through <see cref="ReviewSession.SectionAfter"/> and <see cref="ReviewSession.SectionBefore"/>
/// over a bare view; the window is driven as <c>ReviewWindowTests</c> drives it, a button through its peer or the pointer,
/// a chord pressed in a pane, a caret put where a click would put it.
/// </remarks>
public sealed class SectionWalkTests
{
    /// <summary>
    /// The seed: the title at line 1, One at 3, Two at 7, Three at 11, and Flags at 15, whose paragraph breaks before a
    /// dash that its indent keeps in the paragraph.
    /// </summary>
    private const string Seed =
        "# Rules\n\n## One\n\nAlpha.\n\n## Two\n\nBeta.\n\n## Three\n\nGamma.\n\n## Flags\n\nPass the flags\n    - v and q when the tool should run quietly.\n";

    /// <summary>
    /// Machine 1: One reworded at 3, Two gone, Three as the seed has it at 7, Flags at 11 on one line — the seed's version
    /// folded, which the right shows as written because the seed's break would open a list there — and Four new at 15.
    /// </summary>
    private const string Candidate =
        "# Rules\n\n## One\n\nAlpha, reworded.\n\n## Three\n\nGamma.\n\n## Flags\n\nPass the flags - v and q when the tool should run quietly.\n\n## Four\n\nDelta.\n";

    /// <summary>The seed with Flags on one line: every section the seed's version, once folded.</summary>
    private const string SeedFolded =
        "# Rules\n\n## One\n\nAlpha.\n\n## Two\n\nBeta.\n\n## Three\n\nGamma.\n\n## Flags\n\nPass the flags - v and q when the tool should run quietly.\n";

    /// <summary>One's mark on the right in <see cref="Candidate"/>: its heading at line 3 to its rule at 5.</summary>
    private static readonly LineRange OneOnTheRight = new(2, 3);

    /// <summary>Two's mark on the left in <see cref="Seed"/>: its heading at line 7 to its rule at 9.</summary>
    private static readonly LineRange TwoOnTheLeft = new(6, 3);

    /// <summary>Four's mark on the right in <see cref="Candidate"/>: its heading at line 15 to its rule at 17.</summary>
    private static readonly LineRange FourOnTheRight = new(14, 3);

    /// <summary>The seed and <paramref name="candidates"/>, reviewed in the order given.</summary>
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

    /// <summary>A paragraph of <paramref name="lines"/> lines, each naming its place.</summary>
    private static string Paragraph(string word, int lines) =>
        string.Join("\n", Enumerable.Range(1, lines).Select(i => $"{word} line {i}."));

    private static MarkdownSection SectionOf(MarkdownOutline outline, string headingPath) =>
        outline.Sections.Single(s => s.HeadingPath == headingPath);

    /// <summary>A press of the walk in words, so a failure reads as the walk it found.</summary>
    private static string Describe(WalkStep step) =>
        step.Section is { } section
            ? $"{step.Outcome} {step.Side} {MarkdownOutline.Spoken(section.HeadingPath)} at {section.FirstLine}"
            : step.Outcome.ToString();

    private static void AssertStep(string expected, WalkStep step) => Assert.Equal(expected, Describe(step));

    /// <summary>Next pressed <paramref name="presses"/> times from the top, each counting from where the one before landed.</summary>
    private static List<string> WalkForward(ReviewSession session, int presses)
    {
        List<string> steps = [];
        WalkStep step = session.SectionAfter(null, 0, wrap: true);
        steps.Add(Describe(step));
        while (steps.Count < presses && step.Section is { } section)
        {
            step = session.SectionAfter(step.Side, section.FirstLine, wrap: true);
            steps.Add(Describe(step));
        }

        return steps;
    }

    /// <summary>Clicks <paramref name="button"/> through its automation peer, refused as a pointer's would be while disabled.</summary>
    private static void Click(Button button)
    {
        ((IInvokeProvider)ControlAutomationPeer.CreatePeerForElement(button)).Invoke();
        ReviewHost.Layout();
    }

    /// <summary>
    /// Clicks <paramref name="button"/> with the pointer, whose press puts the keyboard on the button before its command
    /// runs, as a peer's click does not. Hit-testing reads the frame drawn last, so one is drawn first.
    /// </summary>
    private static void PointerClick(Window window, Button button)
    {
        window.CaptureRenderedFrame()?.Dispose();
        Point at = button.TranslatePoint(new Point(button.Bounds.Width / 2, button.Bounds.Height / 2), window)!.Value;
        window.MouseDown(at, MouseButton.Left);
        window.MouseUp(at, MouseButton.Left);
        ReviewHost.Layout();
    }

    /// <summary>Ctrl+N or Ctrl+P pressed in <paramref name="pane"/>.</summary>
    private static void Chord(ReviewHost host, DiffPanePresenter pane, Key key) =>
        host.Press(pane, key, RawInputModifiers.Control, key == Key.N ? PhysicalKey.N : PhysicalKey.P);

    /// <summary>
    /// The walk landed on the heading at <paramref name="line"/> of <paramref name="side"/>'s pane: the keyboard there
    /// and not in the other pane, the caret at the heading's first column, that section marked and no other, and the
    /// line under the panes saying <paramref name="said"/>.
    /// </summary>
    private static void AssertLanded(ReviewHost host, ReviewWindow window, DiffSide side, int line, LineRange mark, string said, string after)
    {
        DiffPanePresenter pane = side == DiffSide.Left ? host.Left : host.Right;
        DiffPanePresenter other = side == DiffSide.Left ? host.Right : host.Left;
        Assert.True(pane.TextArea.IsFocused, $"after {after}, the {side} pane does not have the keyboard");
        Assert.False(other.TextArea.IsFocused, $"after {after}, the pane the walk did not go to has the keyboard");
        Assert.Equal((line, 1), (pane.TextArea.Caret.Line, pane.TextArea.Caret.Column));
        Assert.Equal(side == DiffSide.Left ? mark : null, host.View.LeftEmphasis);
        Assert.Equal(side == DiffSide.Right ? mark : null, host.View.RightEmphasis);
        Assert.Equal(said, window.OutcomeText.Text);
    }

    /// <summary>
    /// Both panes, which scroll as one, show <paramref name="row"/> as the top row in view: their offset is past that
    /// row's top by less than half a row — the slack above a row's text, which the landing scrolls past.
    /// </summary>
    private static void AssertRowAtTop(ReviewHost host, int row, string what)
    {
        double lineHeight = host.Right.TextArea.TextView.DefaultLineHeight;
        double top = row * lineHeight;
        bool Shows(DiffPanePresenter pane) => pane.VerticalOffset > top - 0.01 && pane.VerticalOffset < top + (lineHeight / 2);
        Assert.True(
            Shows(host.Right) && Shows(host.Left),
            $"{what} should be the top row in view, row {row} at {top}; the right pane is at {host.Right.VerticalOffset} and the left at {host.Left.VerticalOffset}");
    }

    /// <summary>Both panes scrolled to <paramref name="offset"/>.</summary>
    private static void AssertScrolledTo(ReviewHost host, double offset, string what)
    {
        Assert.True(
            Math.Abs(host.Right.VerticalOffset - offset) < 0.5 && Math.Abs(host.Left.VerticalOffset - offset) < 0.5,
            $"{what} should be at the top of the view, offset {offset}; the right pane is at {host.Right.VerticalOffset} and the left at {host.Left.VerticalOffset}");
    }

    [AvaloniaFact]
    public async Task The_walk_holds_the_sections_that_differ_each_in_the_pane_that_has_it()
    {
        using FixtureTree tree = new();
        using ReviewHost host = new();
        ReviewSession session = await host.AwaitBuildAsync(() => new ReviewSession(QueueOf(tree, Seed, Candidate), host.View));

        // ⛔ Flags folds to the seed's version and is shown as written, the seed's break there opening a list: only a
        // reading of what a section says, as a decision makes it, passes over Flags.
        Assert.Contains("Pass the flags - v and q", session.Shown.Text, StringComparison.Ordinal);

        // The title and Three the two hold alike; Two only the seed has, so it is walked on the left.
        Assert.Equal(
            ["Found Right \"One\" at 3", "Found Left \"Two\" at 7", "Found Right \"Four\" at 15", "CameRound Right \"One\" at 3"],
            WalkForward(session, presses: 4));

        // The candidate's sections are the right pane's own, the ones its caret and every verb read.
        Assert.Same(SectionOf(session.Shown.Outline, "One"), session.SectionAfter(null, 0, wrap: true).Section);
        Assert.Same(SectionOf(session.Shown.Outline, "Four"), session.SectionBefore(null, 0, wrap: true).Section);
    }

    [AvaloniaFact]
    public async Task The_walk_runs_in_the_rows_the_panes_show_and_so_in_the_candidates_own_order()
    {
        using FixtureTree tree = new();
        using ReviewHost host = new();

        // Machine 1 adds a long Added, swaps Alpha and Beta and rewords both, keeps Kept word for word, which anchors
        // the two sides' rows, and has no Gone: Gone's line, 15, is above every line of the candidate's walked sections
        // but Added's, and its row, past Kept's, below all of them.
        string seed = "# Rules\n\n## Alpha\n\nA.\n\n## Beta\n\nB.\n\n## Kept\n\nKappa is kept as it is.\n\n## Gone\n\nGamma.\n";
        string candidate = "# Rules\n\n## Added\n\n" + Paragraph("Added", 20)
            + "\n\n## Beta\n\nB, changed.\n\n## Alpha\n\nA, changed.\n\n## Kept\n\nKappa is kept as it is.\n";
        ReviewSession session = await host.AwaitBuildAsync(() => new ReviewSession(QueueOf(tree, seed, candidate), host.View));

        Assert.Equal(
            [
                "Found Right \"Added\" at 3", "Found Right \"Beta\" at 26", "Found Right \"Alpha\" at 30",
                "Found Left \"Gone\" at 15", "CameRound Right \"Added\" at 3",
            ],
            WalkForward(session, presses: 5));
    }

    [AvaloniaFact]
    public async Task On_one_row_the_baselines_section_comes_before_the_candidates()
    {
        using FixtureTree tree = new();
        using ReviewHost host = new();

        // Old and New are one modified row: Old only the seed has, New only the candidate.
        ReviewSession session = await host.AwaitBuildAsync(() => new ReviewSession(
            QueueOf(tree, "# Rules\n\n## Old\n\nSame body.\n", "# Rules\n\n## New\n\nSame body.\n"),
            host.View));

        Assert.Equal(
            ["Found Left \"Old\" at 3", "Found Right \"New\" at 3", "CameRound Left \"Old\" at 3"],
            WalkForward(session, presses: 3));
    }

    [AvaloniaFact]
    public async Task A_press_counts_from_past_the_section_its_caret_is_in_from_a_caret_in_no_section_and_from_the_top()
    {
        using FixtureTree tree = new();
        using ReviewHost host = new();
        ReviewSession session = await host.AwaitBuildAsync(() => new ReviewSession(QueueOf(tree, Seed, Candidate), host.View));

        // From One's rule on the right: on past One, and back past it rather than to its own heading.
        AssertStep("Found Left \"Two\" at 7", session.SectionAfter(DiffSide.Right, 5, wrap: true));
        AssertStep("CameRound Right \"Four\" at 15", session.SectionBefore(DiffSide.Right, 5, wrap: true));

        // ⭐ From the seed's version of One, which the walk visits on the right: on past One, not across to it.
        AssertStep("Found Left \"Two\" at 7", session.SectionAfter(DiffSide.Left, 5, wrap: true));

        // From Three on the left, held alike and so not walked: from its heading's row, either way.
        AssertStep("Found Right \"Four\" at 15", session.SectionAfter(DiffSide.Left, 13, wrap: true));
        AssertStep("Found Left \"Two\" at 7", session.SectionBefore(DiffSide.Left, 13, wrap: true));

        // ⛔ A caret in no section counts from its own row: machine 1's first line, in no section, shares row 0 with
        // the seed's Early, which a press from the top finds first and a press from that caret finds behind it.
        using FixtureTree preambleTree = new();
        using ReviewHost preambleHost = new();
        ReviewSession preamble = await preambleHost.AwaitBuildAsync(() => new ReviewSession(
            QueueOf(
                preambleTree,
                "## Early\n\nEpsilon.\n\n## One\n\nAlpha.\n\n## Two\n\nBeta.\n",
                "Read this first.\n\n## One\n\nAlpha, reworded.\n\n## Two\n\nBeta.\n"),
            preambleHost.View));

        AssertStep("Found Left \"Early\" at 1", preamble.SectionAfter(null, 0, wrap: true));
        AssertStep("Found Right \"One\" at 3", preamble.SectionAfter(DiffSide.Right, 1, wrap: true));
        AssertStep("Found Left \"Early\" at 1", preamble.SectionBefore(DiffSide.Right, 1, wrap: true));
    }

    [AvaloniaFact]
    public async Task At_either_end_the_walk_comes_round_or_with_wrapping_off_stops()
    {
        using FixtureTree tree = new();
        using ReviewHost host = new();
        ReviewSession session = await host.AwaitBuildAsync(() => new ReviewSession(QueueOf(tree, Seed, Candidate), host.View));

        AssertStep("CameRound Right \"One\" at 3", session.SectionAfter(DiffSide.Right, 15, wrap: true));
        AssertStep("AtEnd", session.SectionAfter(DiffSide.Right, 15, wrap: false));
        AssertStep("CameRound Right \"Four\" at 15", session.SectionBefore(DiffSide.Right, 3, wrap: true));
        AssertStep("AtEnd", session.SectionBefore(DiffSide.Right, 3, wrap: false));

        // From the top, Next finds the first section whether or not the walk wraps, and Previous comes round or stops.
        AssertStep("Found Right \"One\" at 3", session.SectionAfter(null, 0, wrap: false));
        AssertStep("CameRound Right \"Four\" at 15", session.SectionBefore(null, 0, wrap: true));
        AssertStep("AtEnd", session.SectionBefore(null, 0, wrap: false));
    }

    [AvaloniaFact]
    public async Task With_nothing_that_differs_or_no_rows_yet_a_press_finds_nothing_and_says_which()
    {
        using FixtureTree tree = new();
        using ReviewHost host = new();
        ReviewSession session = await host.AwaitBuildAsync(() => new ReviewSession(QueueOf(tree, Seed, SeedFolded), host.View));

        AssertStep("NoneDiffers", session.SectionAfter(null, 0, wrap: true));
        AssertStep("NoneDiffers", session.SectionBefore(DiffSide.Right, 3, wrap: true));

        // With no model there are no rows to order the walk by, and nothing that differs is said before that.
        MarkdownOutline seed = MarkdownOutline.Read(Seed);
        AssertStep("NotBuilt", SectionWalk.Step(seed, MarkdownOutline.Read(Candidate), null, null, 0, forward: true, wrap: true));
        AssertStep("NoneDiffers", SectionWalk.Step(seed, MarkdownOutline.Read(SeedFolded), null, null, 0, forward: true, wrap: true));
    }

    [AvaloniaFact]
    public async Task Before_the_re_diff_after_a_take_a_press_still_finds_the_baselines_own_section_where_the_take_moved_it()
    {
        using FixtureTree tree = new();
        using ReviewHost host = new();
        ReviewSession session = await host.AwaitBuildAsync(() => new ReviewSession(
            QueueOf(tree, "# Rules\n\n## One\n\nAlpha.\n\n## Two\n\nBeta.\n", "# Rules\n\n## One\n\nAlpha.\n\n## Four\n\nDelta.\n"),
            host.View));
        AssertStep("Found Left \"Two\" at 7", session.SectionAfter(null, 0, wrap: true));

        // Taking Four puts it above Two, whose heading moves to line 11, past the last line of the model's left: the
        // clock is never advanced here, so the model still describes the left as it was.
        session.Take(SectionOf(session.Shown.Outline, "Four"));
        Assert.Equal(10, host.View.Document!.Pane(DiffSide.Left).Lines.Count);

        AssertStep("Found Left \"Two\" at 11", session.SectionAfter(null, 0, wrap: true));
        AssertStep("Found Left \"Two\" at 11", session.SectionAfter(DiffSide.Right, 7, wrap: true));
    }

    [AvaloniaFact]
    public async Task Each_button_and_each_chord_lands_on_a_section_with_the_keyboard_in_its_pane()
    {
        TestLogSink.Instance.Clear();
        using FixtureTree tree = new();
        ReviewWindow window = new();
        using ReviewHost host = new(window);
        await host.AwaitBuildAsync(() => window.Review(QueueOf(tree, Seed, Candidate)));

        Click(window.NextSectionButton);
        AssertLanded(host, window, DiffSide.Right, 3, OneOnTheRight, "\"One\" — not decided yet.", "the next section button, from the top");

        Chord(host, host.Right, Key.N);
        AssertLanded(host, window, DiffSide.Left, 7, TwoOnTheLeft, "\"Two\" — in the baseline; Drop removes it.", "Ctrl+N in the right pane");

        Chord(host, host.Left, Key.P);
        AssertLanded(host, window, DiffSide.Right, 3, OneOnTheRight, "\"One\" — not decided yet.", "Ctrl+P in the left pane");

        Click(window.PreviousSectionButton);
        AssertLanded(
            host, window, DiffSide.Right, 15, FourOnTheRight,
            "\"Four\" — not decided yet. Came round to the last section that differs.",
            "the previous section button, from the first section");

        TestLogSink.AssertNoWarnings(LogArea.Binding);
    }

    [AvaloniaFact]
    public async Task A_pointers_click_on_a_section_button_leaves_the_keyboard_in_the_pane_and_not_on_the_button()
    {
        TestLogSink.Instance.Clear();
        using FixtureTree tree = new();
        ReviewWindow window = new();
        using ReviewHost host = new(window);
        await host.AwaitBuildAsync(() => window.Review(QueueOf(tree, Seed, Candidate)));

        PointerClick(window, window.NextSectionButton);
        AssertLanded(host, window, DiffSide.Right, 3, OneOnTheRight, "\"One\" — not decided yet.", "a pointer's click on the next section button");
        Assert.False(window.NextSectionButton.IsFocused, "The next section button kept the keyboard its press took.");

        PointerClick(window, window.NextSectionButton);
        AssertLanded(host, window, DiffSide.Left, 7, TwoOnTheLeft, "\"Two\" — in the baseline; Drop removes it.", "a second pointer's click");

        TestLogSink.AssertNoWarnings(LogArea.Binding);
    }

    [AvaloniaFact]
    public async Task A_landing_clears_the_selection_and_marks_its_section_where_the_caret_already_was()
    {
        TestLogSink.Instance.Clear();
        using FixtureTree tree = new();
        ReviewWindow window = new();
        using ReviewHost host = new(window);
        await host.AwaitBuildAsync(() => window.Review(QueueOf(tree, Seed, Candidate)));

        // A selection in the right pane, its caret at One's heading, where the press will land, and then the left's
        // caret moved last, so the mark is the left's.
        TextArea right = host.Right.TextArea;
        ReviewHost.PutCaret(host.Right, 3);
        right.Selection = Selection.Create(right, host.Right.Document.GetLineByNumber(9).Offset, host.Right.Document.GetLineByNumber(13).EndOffset);
        ReviewHost.PutCaret(host.Left, 13);
        Assert.True(host.View.LeftEmphasis is not null && host.View.RightEmphasis is null, "premise: the left's caret moved last");
        Assert.False(right.Selection.IsEmpty, "premise: a selection stands in the right pane");

        Click(window.NextSectionButton);
        AssertLanded(host, window, DiffSide.Right, 3, OneOnTheRight, "\"One\" — not decided yet.", "a press landing where the right's caret already was");
        Assert.True(right.Selection.IsEmpty, "The landing left a selection standing in the pane it went to.");

        TestLogSink.AssertNoWarnings(LogArea.Binding);
    }

    [AvaloniaFact]
    public async Task A_press_after_a_take_counts_from_the_pane_with_the_keyboard_and_not_from_the_caret_the_take_moved()
    {
        TestLogSink.Instance.Clear();
        using FixtureTree tree = new();
        ReviewWindow window = new();
        using ReviewHost host = new(window);

        // One gains a second paragraph, so a take of it writes two lines more into the left.
        string candidate =
            "# Rules\n\n## One\n\nAlpha, reworded.\n\nAnd a second paragraph.\n\n## Three\n\nGamma.\n\n## Flags\n\nPass the flags - v and q when the tool should run quietly.\n\n## Four\n\nDelta.\n";
        await host.AwaitBuildAsync(() => window.Review(QueueOf(tree, Seed, candidate)));

        Click(window.NextSectionButton);
        AssertLanded(host, window, DiffSide.Right, 3, new LineRange(2, 5), "\"One\" — not decided yet.", "the next section button, from the top");
        ReviewHost.PutCaret(host.Left, 13);
        Click(window.TakeButton);
        Assert.Equal("Took \"One\" from \"machine 1\" in place of the baseline's version.", window.OutcomeText.Text);
        Assert.Equal(15, host.Left.TextArea.Caret.Line);

        // The keyboard is still the right's, at One, which the take made the seed's: on to Two, two lines down now.
        Click(window.NextSectionButton);
        AssertLanded(host, window, DiffSide.Left, 9, new LineRange(8, 3), "\"Two\" — in the baseline; Drop removes it.", "a press after the take");

        TestLogSink.AssertNoWarnings(LogArea.Binding);
    }

    [AvaloniaFact]
    public async Task After_F6_a_press_counts_from_the_pane_F6_gave_the_keyboard()
    {
        TestLogSink.Instance.Clear();
        using FixtureTree tree = new();
        ReviewWindow window = new();
        using ReviewHost host = new(window);
        await host.AwaitBuildAsync(() => window.Review(QueueOf(tree, Seed, Candidate)));

        Click(window.NextSectionButton);
        ReviewHost.PutCaret(host.Left, 13);
        host.Press(host.Right, Key.F6, RawInputModifiers.None, PhysicalKey.F6);
        Assert.True(host.Left.TextArea.IsFocused, "premise: F6 gave the left pane the keyboard");

        // From the left's caret, in Three, which is not walked: past Three's row, to Four.
        Click(window.NextSectionButton);
        AssertLanded(host, window, DiffSide.Right, 15, FourOnTheRight, "\"Four\" — not decided yet.", "a press after F6");

        TestLogSink.AssertNoWarnings(LogArea.Binding);
    }

    [AvaloniaFact]
    public async Task The_headings_row_comes_to_the_top_of_the_view_below_any_padding_and_as_far_as_the_view_goes_near_the_end()
    {
        TestLogSink.Instance.Clear();
        using FixtureTree tree = new();
        ReviewWindow window = new();
        using ReviewHost host = new(window);

        // Long, which only the seed has, puts more rows of padding above the candidate's Target than the pane holds.
        string seed = "# Rules\n\n## Long\n\n" + Paragraph("Long", 60) + "\n\n## Target\n\nOld target text.\n\n## Filler\n\n"
            + Paragraph("Filler", 60) + "\n\n## Last\n\nOld last text.\n";
        string candidate = "# Rules\n\n## Target\n\nNew target text.\n\n## Filler\n\n" + Paragraph("Filler", 60)
            + "\n\n## Last\n\nNew last text.\n";
        ReviewSession session = await host.AwaitBuildAsync(() => window.Review(QueueOf(tree, seed, candidate)));
        SideBySideDocument model = host.View.Document!;
        double lineHeight = host.Right.TextArea.TextView.DefaultLineHeight;
        int target = SectionOf(session.Shown.Outline, "Target").FirstLine;
        int last = SectionOf(session.Shown.Outline, "Last").FirstLine;

        Click(window.NextSectionButton);
        AssertLanded(host, window, DiffSide.Left, 3, new LineRange(2, 62), "\"Long\" — in the baseline; Drop removes it.", "the first press");
        AssertRowAtTop(host, model.Pane(DiffSide.Left).Lines[2].Row, "Long's heading");

        Click(window.NextSectionButton);
        int targetRow = model.Pane(DiffSide.Right).Lines[target - 1].Row;
        int padding = targetRow - model.Pane(DiffSide.Right).Lines[target - 2].Row - 1;
        Assert.True(padding * lineHeight > host.Right.ViewportHeight, $"premise: {padding} rows of padding over Target's heading are taller than the pane");
        AssertRowAtTop(host, targetRow, "Target's own row, below its padding,");

        Click(window.NextSectionButton);
        double bottom = host.Right.ExtentHeight - host.Right.ViewportHeight;
        Assert.True(model.Pane(DiffSide.Right).Lines[last - 1].Row * lineHeight > bottom, "premise: Last's heading cannot reach the top");
        AssertScrolledTo(host, bottom, "The view's end");

        TestLogSink.AssertNoWarnings(LogArea.Binding);
    }

    [AvaloniaFact]
    public async Task Each_end_comes_round_and_says_so_and_with_wrapping_off_stops_and_says_so()
    {
        TestLogSink.Instance.Clear();
        using FixtureTree tree = new();
        ReviewWindow window = new();
        using ReviewHost host = new(window);
        await host.AwaitBuildAsync(() => window.Review(QueueOf(tree, Seed, Candidate)));
        Assert.True(window.WrapSectionWalk, "The section walk does not wrap by default.");

        Click(window.NextSectionButton);
        Click(window.NextSectionButton);
        Click(window.NextSectionButton);
        Click(window.NextSectionButton);
        AssertLanded(
            host, window, DiffSide.Right, 3, OneOnTheRight,
            "\"One\" — not decided yet. Came round to the first section that differs.",
            "a press past the last section");

        window.WrapSectionWalk = false;
        Click(window.PreviousSectionButton);
        AssertLanded(
            host, window, DiffSide.Right, 3, OneOnTheRight,
            "No section of \"machine 1\" before this point differs from the baseline.",
            "a press before the first section, the walk not wrapping");

        Click(window.NextSectionButton);
        Click(window.NextSectionButton);
        Click(window.NextSectionButton);
        AssertLanded(
            host, window, DiffSide.Right, 15, FourOnTheRight,
            "No section of \"machine 1\" after this point differs from the baseline.",
            "a press past the last section, the walk not wrapping");

        TestLogSink.AssertNoWarnings(LogArea.Binding);
    }

    [AvaloniaFact]
    public async Task The_first_press_on_a_candidate_counts_from_the_top_with_the_keyboard_in_a_pane()
    {
        TestLogSink.Instance.Clear();
        using FixtureTree tree = new();
        ReviewWindow window = new();
        using ReviewHost host = new(window);

        // Each machine words the title its own way, so the title, at the caret's line 1, is walked.
        string first = Seed.Replace("# Rules\n", "# Rules, as machine 1 keeps them\n", StringComparison.Ordinal)
            .Replace("Alpha.", "Alpha, reworded.", StringComparison.Ordinal);
        string second = Seed.Replace("# Rules\n", "# Rules, as machine 2 keeps them\n", StringComparison.Ordinal)
            .Replace("Alpha.", "Alpha, again.", StringComparison.Ordinal);
        await host.AwaitBuildAsync(() => window.Review(QueueOf(tree, Seed, first, second)));

        Chord(host, host.Right, Key.N);
        AssertLanded(host, window, DiffSide.Right, 1, new LineRange(0, 1), "the title — not decided yet.", "the review's first press");
        Chord(host, host.Right, Key.N);
        AssertLanded(host, window, DiffSide.Right, 3, OneOnTheRight, "\"One\" — not decided yet.", "a second press");

        // Showing the next candidate puts the right's caret back at its start, the keyboard still in the right.
        await host.AwaitBuildAsync(() => host.Press(host.Right, Key.F8, RawInputModifiers.None, PhysicalKey.F8));
        Chord(host, host.Right, Key.N);
        AssertLanded(host, window, DiffSide.Right, 1, new LineRange(0, 1), "the title — not decided yet.", "the first press on the next candidate");

        TestLogSink.AssertNoWarnings(LogArea.Binding);
    }

    [AvaloniaFact]
    public async Task A_press_on_a_candidate_with_nothing_that_differs_says_so_and_moves_nothing()
    {
        TestLogSink.Instance.Clear();
        using FixtureTree tree = new();
        ReviewWindow window = new();
        using ReviewHost host = new(window);
        await host.AwaitBuildAsync(() => window.Review(QueueOf(tree, Seed, SeedFolded)));

        Click(window.NextSectionButton);
        Assert.Equal("No section of \"machine 1\" differs from the baseline.", window.OutcomeText.Text);
        Assert.Null(host.View.LeftEmphasis);
        Assert.Null(host.View.RightEmphasis);
        Assert.False(host.Left.TextArea.IsFocused || host.Right.TextArea.IsFocused, "A press with nowhere to go put the keyboard in a pane.");

        TestLogSink.AssertNoWarnings(LogArea.Binding);
    }

    [AvaloniaFact]
    public async Task The_section_buttons_carry_their_ids_name_their_chords_and_wait_for_a_review()
    {
        TestLogSink.Instance.Clear();
        using FixtureTree tree = new();
        ReviewWindow window = new();
        using ReviewHost host = new(window);

        Assert.Equal("PreviousSection", AutomationProperties.GetAutomationId(window.PreviousSectionButton));
        Assert.Equal("NextSection", AutomationProperties.GetAutomationId(window.NextSectionButton));
        Assert.EndsWith("(Ctrl+P).", (string?)ToolTip.GetTip(window.PreviousSectionButton), StringComparison.Ordinal);
        Assert.EndsWith("(Ctrl+N).", (string?)ToolTip.GetTip(window.NextSectionButton), StringComparison.Ordinal);
        Assert.False(window.PreviousSectionButton.IsEffectivelyEnabled, "The previous section button is enabled with no review to walk.");
        Assert.False(window.NextSectionButton.IsEffectivelyEnabled, "The next section button is enabled with no review to walk.");

        // A chord with nothing to walk moves nothing and throws nothing.
        Chord(host, host.Right, Key.N);
        Assert.Null(window.Session);

        await host.AwaitBuildAsync(() => window.Review(QueueOf(tree, Seed, Candidate)));
        Assert.True(window.PreviousSectionButton.IsEffectivelyEnabled, "The previous section button stayed disabled once the review began.");
        Assert.True(window.NextSectionButton.IsEffectivelyEnabled, "The next section button stayed disabled once the review began.");

        TestLogSink.AssertNoWarnings(LogArea.Binding);
    }
}
