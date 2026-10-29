using Avalonia.Automation.Peers;
using Avalonia.Automation.Provider;
using Avalonia.Controls;
using Avalonia.Headless.XUnit;
using Avalonia.Input;

namespace Bennewitz.Ninja.AgentBaseline.Tests;

/// <summary>
/// The line under the panes says what the durable store knows about the section at the right pane's caret
/// — Brian's choice of 2026-10-29, beside the start-up that brings a decision file to the window.
/// </summary>
/// <remarks>
/// Every caret is put where a click in the text would put it and every verb is clicked through its peer,
/// as <c>ReviewWindowTests</c> drives the window: the line is the window's, so the window is what is driven.
/// </remarks>
public sealed class StandingLineTests
{
    /// <summary>The seed: the title at line 1, One at 3 with its body at 5, Three at 7 with its body at 9.</summary>
    private const string Seed = "# Rules\n\n## One\n\nAlpha.\n\n## Three\n\nGamma.\n";

    /// <summary>One reworded.</summary>
    private const string OneReworded = "# Rules\n\n## One\n\nAlpha, reworded.\n\n## Three\n\nGamma.\n";

    /// <summary>Three reworded.</summary>
    private const string ThreeReworded = "# Rules\n\n## One\n\nAlpha.\n\n## Three\n\nGamma, reworded.\n";

    /// <summary>The seed under a line that is in no section: One's body is at line 7, Three's at 11.</summary>
    private const string Preamble = "Read this first.\n\n# Rules\n\n## One\n\nAlpha.\n\n## Three\n\nGamma.\n";

    /// <summary>The seed and <paramref name="candidates"/>, reviewed in the order given.</summary>
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

    private static void Click(Button button)
    {
        ((IInvokeProvider)ControlAutomationPeer.CreatePeerForElement(button)).Invoke();
        ReviewHost.Layout();
    }

    [AvaloniaFact]
    public async Task The_line_says_what_the_store_knows_of_the_section_the_right_pane_caret_enters()
    {
        using FixtureTree tree = new();
        ReviewQueue queue = QueueOf(tree, OneReworded);
        MarkdownSection one = queue.Items[0].Candidate.Document.Outline.Sections.Single(s => s.HeadingPath == "One");
        ReviewWindow window = new();
        using ReviewHost host = new(window);
        await host.AwaitBuildAsync(() => window.Review(queue, [new SectionDecision(DecisionKind.Rejected, SectionIdentity.Of(one), "machine 1", null)]));

        // An earlier run left this very version out: the store says so as the caret enters it.
        ReviewHost.PutCaret(host.Right, 5);
        Assert.Equal("\"One\" — already left, from \"machine 1\".", window.OutcomeText.Text);

        ReviewHost.PutCaret(host.Right, 9);
        Assert.Equal("\"Three\" — not decided yet.", window.OutcomeText.Text);

        // ⭐ The left pane's caret is not the reading, so the line stays as it was.
        ReviewHost.PutCaret(host.Left, 5);
        Assert.Equal("\"Three\" — not decided yet.", window.OutcomeText.Text);
    }

    [AvaloniaFact]
    public async Task A_verbs_outcome_stays_while_the_caret_moves_within_its_section_and_goes_when_it_leaves()
    {
        using FixtureTree tree = new();
        ReviewQueue queue = QueueOf(tree, OneReworded);
        ReviewWindow window = new();
        using ReviewHost host = new(window);
        await host.AwaitBuildAsync(() => window.Review(queue));

        ReviewHost.PutCaret(host.Right, 5);
        Click(window.TakeButton);
        ReviewHost.PutCaret(host.Right, 4);
        Assert.Equal("Took \"One\" from \"machine 1\" in place of the baseline's version.", window.OutcomeText.Text);

        ReviewHost.PutCaret(host.Right, 9);
        Assert.Equal("\"Three\" — not decided yet.", window.OutcomeText.Text);

        // Back in One, the store knows what this run did with it.
        ReviewHost.PutCaret(host.Right, 5);
        Assert.Equal("\"One\" — already taken, from \"machine 1\".", window.OutcomeText.Text);
    }

    [AvaloniaFact]
    public async Task A_caret_in_no_section_says_nothing_and_the_section_it_returns_to_is_said_again()
    {
        using FixtureTree tree = new();
        ReviewQueue queue = QueueOf(tree, Preamble);
        ReviewWindow window = new();
        using ReviewHost host = new(window);
        await host.AwaitBuildAsync(() => window.Review(queue));

        ReviewHost.PutCaret(host.Right, 7);
        Click(window.DeferButton);
        Assert.Equal("Put \"One\" from \"machine 1\" off for this session.", window.OutcomeText.Text);

        // Above the first heading the caret is in no section: nothing to say, so the outcome stays.
        ReviewHost.PutCaret(host.Right, 1);
        Assert.Equal("Put \"One\" from \"machine 1\" off for this session.", window.OutcomeText.Text);

        // Put off is the session's, never the store's: One is still not decided.
        ReviewHost.PutCaret(host.Right, 7);
        Assert.Equal("\"One\" — not decided yet.", window.OutcomeText.Text);
    }

    [AvaloniaFact]
    public async Task Coming_back_to_a_candidate_says_the_section_the_caret_enters_there_again()
    {
        using FixtureTree tree = new();
        ReviewQueue queue = QueueOf(tree, OneReworded, ThreeReworded);
        ReviewWindow window = new();
        using ReviewHost host = new(window);
        await host.AwaitBuildAsync(() => window.Review(queue));

        ReviewHost.PutCaret(host.Right, 5);
        ReviewHost.PutCaret(host.Right, 1);
        Assert.Equal("the title — not decided yet.", window.OutcomeText.Text);

        // Each move clears the line, the next candidate and the way back alike.
        await host.AwaitBuildAsync(() => host.Press(host.Right, Key.F8, RawInputModifiers.None, PhysicalKey.F8));
        Assert.Equal(string.Empty, window.OutcomeText.Text);
        await host.AwaitBuildAsync(() => host.Press(host.Right, Key.F8, RawInputModifiers.Shift, PhysicalKey.F8));
        Assert.Equal(string.Empty, window.OutcomeText.Text);

        // ⭐ The title is the section the line last said before the moves, and it is said again.
        ReviewHost.PutCaret(host.Right, 2);
        Assert.Equal("the title — not decided yet.", window.OutcomeText.Text);
    }
}
