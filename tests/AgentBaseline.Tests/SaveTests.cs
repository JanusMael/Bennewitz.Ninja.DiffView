using Avalonia.Automation.Peers;
using Avalonia.Automation.Provider;
using Avalonia.Controls;
using Avalonia.Headless.XUnit;
using Avalonia.Logging;
using Bennewitz.Ninja.DiffView.Tests;

namespace Bennewitz.Ninja.AgentBaseline.Tests;

/// <summary>
/// Plan 00034 step 10, the save: decision 11's warning over stale candidates and the acknowledgement it goes
/// ahead on, and the report's last lines — each take as the save reads it, decision 9c, and the staleness
/// accepted.
/// </summary>
/// <remarks>
/// <para>
/// ⭐ <b>A save over stale candidates is made twice</b>, Brian's choice of 2026-10-29: the first writes nothing
/// and says where the candidates are stale, and the second, over the same staleness and the same text, saves.
/// Each window test counts what the save asked for, because a first save that asked for a file would already
/// have gone ahead.
/// </para>
/// <para>
/// The new path, the LF and the seed's untouched bytes are <see cref="BaselineOutputTests"/>' own; this
/// class holds what step 10 adds.
/// </para>
/// </remarks>
public sealed class SaveTests
{
    /// <summary>The accumulator's starting text: One's body at line 5, Two's at line 9.</summary>
    private static readonly string Seed = Doc("# Rules", "", "## One", "", "Alpha.", "", "## Two", "", "Beta.", "");

    /// <summary>Machine 1: One and Two in versions of its own.</summary>
    private static readonly string Machine1 = Doc("# Rules", "", "## One", "", "Alpha, as machine 1 has it.", "", "## Two", "", "Beta, as machine 1 has it.", "");

    /// <summary>Machine 2: One in a version of its own, and no Two at all.</summary>
    private static readonly string Machine2 = Doc("# Rules", "", "## One", "", "Alpha, as machine 2 has it.", "");

    /// <summary>Machine 3: One and Two in versions of its own, their bodies at lines 5 and 9.</summary>
    private static readonly string Machine3 = Doc("# Rules", "", "## One", "", "Alpha, as machine 3 has it.", "", "## Two", "", "Beta, as machine 3 has it.", "");

    /// <summary>What the first save says once machine 3's One and Two are taken over the two machines before it.</summary>
    private const string Warning =
        "Not saved yet: the baseline changed after these were reviewed, where each carries its own version — "
        + "\"machine 1\" at \"One\" and \"Two\"; \"machine 2\" at \"One\". Save again to accept that, or review them first.";

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

    /// <summary>Clicks <paramref name="button"/> through its automation peer, as <see cref="ReviewWindowTests"/> does.</summary>
    private static void Click(Button button)
    {
        ((IInvokeProvider)ControlAutomationPeer.CreatePeerForElement(button)).Invoke();
        ReviewHost.Layout();
    }

    private static MarkdownSection SectionOf(ReviewSession session, string path) =>
        session.Current.Candidate.Document.Outline.Sections.Single(s => s.HeadingPath == path);

    private static string Said(params StaleSection[] stale) => ((IReadOnlyList<StaleSection>)stale).Spoken();

    /// <summary>
    /// A window reviewing machines 1 to 3, with both earlier machines reviewed and machine 3's One and Two taken —
    /// so machine 1 is stale at both and machine 2, which has no Two, at One alone.
    /// </summary>
    private static async Task<(ReviewHost Host, ReviewWindow Window)> StaleWindowAsync(ReviewQueue queue, Func<Task<string?>> choose)
    {
        ReviewWindow window = new() { ChooseSavePath = choose };
        ReviewHost host = new(window);
        await host.AwaitBuildAsync(() => window.Review(queue));
        await host.AwaitBuildAsync(() => Click(window.NextButton));
        await host.AwaitBuildAsync(() => Click(window.NextButton));
        ReviewHost.PutCaret(host.Right, 5);
        Click(window.TakeButton);
        ReviewHost.PutCaret(host.Right, 9);
        Click(window.TakeButton);
        return (host, window);
    }

    [AvaloniaFact]
    public async Task A_save_over_stale_candidates_writes_nothing_and_says_where_and_a_second_save_accepts_it()
    {
        TestLogSink.Instance.Clear();
        using FixtureTree tree = new();
        ReviewQueue queue = QueueOf(tree, Machine1, Machine2, Machine3);
        string output = Path.Combine(tree.MakeDirectory("Baseline"), "AGENTS.md");
        int asked = 0;
        (ReviewHost host, ReviewWindow window) = await StaleWindowAsync(queue, () =>
        {
            asked++;
            return Task.FromResult<string?>(output);
        });
        using (host)
        {
            Click(window.SaveButton);
            Assert.True(asked == 0 && !File.Exists(output), "A first save over stale candidates asked for a file, or wrote one.");
            Assert.Equal(Warning, window.OutcomeText.Text);

            // ⭐ The second save, over the same staleness and the same text, is the acknowledgement.
            Click(window.SaveButton);
            Assert.True(asked == 1 && File.Exists(output), "A second save over the same staleness and the same text did not save.");
            Assert.EndsWith(" Its provenance lists the staleness accepted.", window.OutcomeText.Text, StringComparison.Ordinal);
            Assert.EndsWith(
                "\n## Staleness accepted\n\nThe baseline gained or changed each section below after the candidate beside it was "
                + "reviewed, and that candidate carries a version of its own; the save went ahead once that was accepted.\n\n"
                + "| Candidate | Section |\n|---|---|\n| machine 1 | One |\n| machine 1 | Two |\n| machine 2 | One |\n",
                File.ReadAllText(BaselineOutput.ReportPathFor(output)),
                StringComparison.Ordinal);

            // Each save is accepted on its own: the next one over the same staleness warns again.
            Click(window.SaveButton);
            Assert.True(asked == 1, "A save went ahead on the acknowledgement of the save before it.");
            Assert.Equal(Warning, window.OutcomeText.Text);
        }

        TestLogSink.AssertNoWarnings(LogArea.Binding);
    }

    [AvaloniaFact]
    public async Task A_save_after_the_accumulator_or_the_staleness_changed_asks_again()
    {
        TestLogSink.Instance.Clear();
        using FixtureTree tree = new();
        ReviewQueue queue = QueueOf(tree, Machine1, Machine2, Machine3);
        string output = Path.Combine(tree.MakeDirectory("Baseline"), "AGENTS.md");
        int asked = 0;
        (ReviewHost host, ReviewWindow window) = await StaleWindowAsync(queue, () =>
        {
            asked++;
            return Task.FromResult<string?>(output);
        });
        using (host)
        {
            Click(window.SaveButton);

            // Typed between the two saves: the text the warning was about is not the text a save would write.
            host.Type(host.Left, host.View.LeftDocument.Text.Length, "\nTyped after the warning.");
            Click(window.SaveButton);
            Assert.True(asked == 0 && !File.Exists(output), "A save after the accumulator changed went ahead on the earlier warning.");
            Assert.Equal(Warning, window.OutcomeText.Text);

            Click(window.SaveButton);
            Assert.True(asked == 1, "A second save over the text as it now reads did not save.");

            // ⭐ Moved between the two saves, with the text as it was: machine 2 is on the right, so it is under
            // review and no longer stale, and what is stale is not what the warning said.
            Click(window.SaveButton);
            Assert.Equal(Warning, window.OutcomeText.Text);
            await host.AwaitBuildAsync(() => Click(window.PreviousButton));
            Click(window.SaveButton);
            Assert.True(asked == 1, "A save after the staleness changed went ahead on the earlier warning.");
            Assert.Equal(
                "Not saved yet: the baseline changed after these were reviewed, where each carries its own version — "
                + "\"machine 1\" at \"One\" and \"Two\". Save again to accept that, or review them first.",
                window.OutcomeText.Text);

            Click(window.SaveButton);
            Assert.True(asked == 2, "A second save over the staleness as it now stands did not save.");
        }

        TestLogSink.AssertNoWarnings(LogArea.Binding);
    }

    [AvaloniaFact]
    public async Task A_save_with_nothing_stale_is_made_once_and_its_report_says_none_was_accepted()
    {
        TestLogSink.Instance.Clear();
        using FixtureTree tree = new();
        ReviewQueue queue = QueueOf(tree, Machine1);
        string output = Path.Combine(tree.MakeDirectory("Baseline"), "AGENTS.md");
        ReviewWindow window = new() { ChooseSavePath = () => Task.FromResult<string?>(output) };
        using ReviewHost host = new(window);
        await host.AwaitBuildAsync(() => window.Review(queue));

        // The candidate on the right is under review, so a take from it stales nothing.
        ReviewHost.PutCaret(host.Right, 5);
        Click(window.TakeButton);
        Click(window.SaveButton);

        Assert.True(File.Exists(output), "A save with nothing stale wrote nothing.");
        Assert.Equal(
            $"Saved the baseline to {output}, its provenance to {BaselineOutput.ReportPathFor(output)}, and its decisions to "
            + $"{BaselineOutput.DecisionsPathFor(output)}.",
            window.OutcomeText.Text);
        Assert.EndsWith(
            "\n## Staleness accepted\n\nNone: no candidate was stale when this baseline was saved.\n",
            File.ReadAllText(BaselineOutput.ReportPathFor(output)),
            StringComparison.Ordinal);

        TestLogSink.AssertNoWarnings(LogArea.Binding);
    }

    [AvaloniaFact]
    public async Task The_writer_refuses_staleness_unless_exactly_it_was_accepted_and_writes_nothing()
    {
        TestLogSink.Instance.Clear();
        using FixtureTree tree = new();
        ReviewQueue queue = QueueOf(tree, Machine1, Machine2);
        string directory = tree.MakeDirectory("Baseline");
        string output = Path.Combine(directory, "AGENTS.md");
        using ReviewHost host = new();
        ReviewSession session = await host.AwaitBuildAsync(() => new ReviewSession(queue, host.View));
        Assert.True(await host.AwaitBuildAsync(session.Next));
        session.Take(SectionOf(session, "One"));

        Assert.Equal(
            "a save over stale candidates goes ahead only once exactly what is stale is accepted, and stale is "
            + "\"machine 1\" at \"One\" where accepted is nothing",
            Assert.Throws<SaveRefusedException>(() => BaselineOutput.Write(session, output)).Message);
        Assert.Equal(
            "a save over stale candidates goes ahead only once exactly what is stale is accepted, and stale is "
            + "\"machine 1\" at \"One\" where accepted is \"machine 1\" at \"Two\"",
            Assert.Throws<SaveRefusedException>(() => BaselineOutput.Write(session, output, [new StaleSection("Two", "machine 1")])).Message);
        Assert.Empty(Directory.GetFileSystemEntries(directory));

        BaselineOutput.Write(session, output, session.Staleness());
        Assert.True(File.Exists(output), "A save over exactly the staleness accepted wrote nothing.");

        TestLogSink.AssertNoWarnings(LogArea.Binding);
    }

    [AvaloniaFact]
    public async Task The_report_says_an_edited_take_was_edited_and_a_removed_one_removed_and_the_file_names_its_target()
    {
        TestLogSink.Instance.Clear();
        using FixtureTree tree = new();
        ReviewQueue queue = QueueOf(
            tree,
            Doc("# Rules", "", "## One", "", "Alpha, reworded.", "", "## Two", "", "Beta.", "", "## Three", "", "Gamma.", ""));
        string output = Path.Combine(tree.MakeDirectory("Baseline"), "AGENTS.md");
        using ReviewHost host = new();
        ReviewSession session = await host.AwaitBuildAsync(() => new ReviewSession(queue, host.View));

        // Taken and then edited by hand; taken as the accumulator already held it; and taken, then undone.
        session.Take(SectionOf(session, "One"));
        string left = host.View.LeftDocument.Text;
        host.Type(host.Left, left.IndexOf("Alpha, reworded.", StringComparison.Ordinal) + "Alpha, reworded.".Length, " Edited.");
        Assert.Equal(TakeResult.Unchanged, session.Take(SectionOf(session, "Two")));
        Assert.Equal(TakeResult.Inserted, session.Take(SectionOf(session, "Three")));
        host.Left.Undo();
        ReviewHost.Layout();
        Assert.DoesNotContain("Three", MarkdownOutline.Read(host.View.LeftDocument.Text).Sections.Select(s => s.HeadingPath));

        SavedBaseline saved = BaselineOutput.Write(session, output);

        // ⭐ Decision 9c: what the baseline holds of each take, never that each was taken.
        Assert.Contains(
            "| # | Decision | Section | From |\n|---|---|---|---|\n"
            + "| 1 | taken, then edited | One | machine 1 |\n"
            + "| 2 | taken | Two | machine 1 |\n"
            + "| 3 | taken, then removed | Three | machine 1 |\n",
            File.ReadAllText(saved.ReportPath),
            StringComparison.Ordinal);
        Assert.Equal(queue.Target, DecisionFile.Read(saved.DecisionsPath).Target);

        TestLogSink.AssertNoWarnings(LogArea.Binding);
    }

    [Fact]
    public void Staleness_is_said_candidate_by_candidate_with_each_ones_sections_listed()
    {
        Assert.Equal("nothing", Said());
        Assert.Equal("\"m1\" at \"One\"", Said(new StaleSection("One", "m1")));
        Assert.Equal(
            "\"m1\" at the title, \"One\" and \"One / Detail\"; \"m2\" at \"Two\"",
            Said(new StaleSection("", "m1"), new StaleSection("One", "m1"), new StaleSection("Two", "m2"), new StaleSection("One / Detail", "m1")));
    }
}
