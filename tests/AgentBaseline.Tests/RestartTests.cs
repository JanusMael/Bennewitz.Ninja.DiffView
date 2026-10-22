using System.Security.Cryptography;
using System.Text;
using Avalonia.Automation.Peers;
using Avalonia.Automation.Provider;
using Avalonia.Controls;
using Avalonia.Headless.XUnit;
using Avalonia.Logging;
using Bennewitz.Ninja.DiffView.Tests;

namespace Bennewitz.Ninja.AgentBaseline.Tests;

/// <summary>
/// Plan 00034 step 8: the two stores across a restart. A run decides, saves and is disposed; a later run
/// reads the decision file the save wrote, and is asked what it knows.
/// </summary>
/// <remarks>
/// <para>
/// ⭐ <b>Every restart is a real one.</b> The first run's host is disposed before the second is built, the
/// second reads the corpus from disk again, and what passes between them is the decision file and nothing
/// else — so a decision held in memory, or a session store that leaked, cannot make a test pass.
/// </para>
/// <para>
/// ⛔ <b>Decision 9c is asserted point by point</b>: the result and the source's hash as the file holds
/// them, the hash restated here rather than asked of <see cref="SectionIdentity"/>. A test that reloads
/// only the edited flag passes while the file holds neither.
/// </para>
/// </remarks>
public sealed class RestartTests
{
    /// <summary>The seed: One, Two and Four under a title.</summary>
    private static readonly string Seed = Doc(
        "# Rules", "", "## One", "", "Alpha.", "", "## Two", "", "Beta.", "", "## Four", "", "Delta.", "");

    /// <summary>One and Two reworded, Three new, Four as the seed has it.</summary>
    private static readonly string Machine1 = Doc(
        "# Rules", "", "## One", "", "Alpha, reworded.", "", "## Two", "", "Beta, reworded.", "", "## Three", "", "Gamma.", "",
        "## Four", "", "Delta.", "");

    /// <summary>One and Two reworded otherwise than machine 1 has them.</summary>
    private static readonly string Machine2 = Doc(
        "# Rules", "", "## One", "", "Alpha, as machine 2 has it.", "", "## Two", "", "Beta, as machine 2 has it.", "", "## Four", "",
        "Delta.", "");

    /// <summary>JanusHalo's rules as text, before ingest has anything to undo: Two is its own.</summary>
    private static readonly string JanusHalo = Doc(
        "# Rules", "", "## One", "", "Alpha.", "", "## Two", "", "Beta, as janushalo has it.", "", "## Four", "", "Delta.", "");

    private const string TakenTwo = "## Two\n\nBeta, as janushalo has it.";

    private const string EditedTwo = "## Two\n\nBeta, as janushalo has it. Edited by hand.";

    private static string Doc(params string[] lines) => string.Join("\n", lines);

    /// <summary>
    /// <paramref name="plain"/> shaped as JanusHalo's file is — nested whole in one list item, IngestTests'
    /// wrapping — and in CRLF besides, so both of plan 00034 §2's differences stand between the bytes and the
    /// text a review sees.
    /// </summary>
    private static string NestedInCrlf(string plain)
    {
        string[] lines = plain.Split('\n');
        for (int i = 0; i < lines.Length; i++)
        {
            bool terminatorOnly = i == lines.Length - 1 && lines[i].Length == 0;
            lines[i] = i == 0 ? "- " + lines[i] : terminatorOnly ? lines[i] : "  " + lines[i];
        }

        return string.Join("\r\n", lines);
    }

    /// <summary>Decision 6's hash restated rather than asked of <see cref="SectionIdentity"/>.</summary>
    private static string Sha256(string text) => Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(text)));

    private static ReviewQueue QueueOf(FixtureTree tree, params (string Label, string Text)[] candidates) =>
        QueueOf(tree, "AGENTS.md", candidates);

    /// <summary>
    /// The corpus a run reads: the seed and <paramref name="candidates"/>, each in a directory named for its
    /// label and read from disk, so a later run given the same labels reads the same candidates — changed or
    /// not — as a later run would.
    /// </summary>
    private static ReviewQueue QueueOf(FixtureTree tree, string fileName, params (string Label, string Text)[] candidates)
    {
        List<CorpusEntry> entries = [Entry(tree.Write($"seed/{fileName}", Seed), "seed")];
        foreach ((string label, string text) in candidates)
        {
            entries.Add(Entry(tree.Write($"{label.Replace(' ', '-')}/{fileName}", text), label));
        }

        Corpus corpus = Corpus.Gather(CorpusRequest.Create(entries, seed: "seed", order: CorpusOrder.Listed));
        return corpus.Queue(cancellationToken: TestContext.Current.CancellationToken);
    }

    private static CorpusEntry Entry(string path, string label) =>
        new(path, label, new CandidateTarget(Path.GetFileName(path), null));

    private static MarkdownSection SectionOf(ReviewSession session, string path) =>
        session.Current.Candidate.Document.Outline.Sections.Single(s => s.HeadingPath == path);

    private static async Task<(ReviewHost Host, ReviewSession Session)> OpenAsync(
        ReviewQueue queue, IReadOnlyList<SectionDecision>? earlier = null)
    {
        ReviewHost host = new();
        ReviewSession session = await host.AwaitBuildAsync(() => new ReviewSession(queue, host.View, earlier));
        return (host, session);
    }

    private static void Click(Button button)
    {
        ((IInvokeProvider)ControlAutomationPeer.CreatePeerForElement(button)).Invoke();
        ReviewHost.Layout();
    }

    /// <summary>
    /// A first run: JanusHalo's Two taken, nested and in CRLF as the machine has it, then edited by hand in
    /// the accumulator, and saved. Answers the decisions the save's file holds.
    /// </summary>
    private static async Task<(IReadOnlyList<SectionDecision> Saved, string Baseline)> SaveAnEditedTakeAsync(FixtureTree tree)
    {
        ReviewQueue queue = QueueOf(tree, ("janushalo", NestedInCrlf(JanusHalo)));
        string output = Path.Combine(tree.MakeDirectory("Baseline"), "AGENTS.md");
        (ReviewHost host, ReviewSession session) = await OpenAsync(queue);
        using (host)
        {
            Assert.Equal(TakeResult.Replaced, session.Take(SectionOf(session, "Two")));
            int end = host.View.LeftDocument.Text.IndexOf(TakenTwo, StringComparison.Ordinal) + TakenTwo.Length;
            host.Type(host.Left, end, " Edited by hand.");
            Assert.True(host.View.LeftDocument.Text.Contains(EditedTwo, StringComparison.Ordinal), "The left refused the edit.");

            SavedBaseline saved = BaselineOutput.Write(session, output);
            Assert.Equal(BaselineOutput.DecisionsPathFor(output), saved.DecisionsPath);
        }

        return (DecisionFile.Read(BaselineOutput.DecisionsPathFor(output)), output);
    }

    [AvaloniaFact]
    public async Task Decisions_saved_with_a_baseline_are_found_by_the_next_run_and_nothing_put_off_is_among_them()
    {
        TestLogSink.Instance.Clear();
        using FixtureTree tree = new();
        MarkdownOutline machine1 = MarkdownOutline.Read(Machine1);
        MarkdownSection Of(string path) => machine1.Sections.Single(s => s.HeadingPath == path);
        string output = Path.Combine(tree.MakeDirectory("Baseline"), "AGENTS.md");

        (ReviewHost first, ReviewSession run) = await OpenAsync(QueueOf(tree, ("machine 1", Machine1)));
        using (first)
        {
            run.Leave(SectionOf(run, "One"));
            run.Defer(SectionOf(run, "Two"));
            Assert.Equal(TakeResult.Inserted, run.Take(SectionOf(run, "Three")));
            run.Drop("Four");
            BaselineOutput.Write(run, output);
        }

        IReadOnlyList<SectionDecision> earlier = DecisionFile.Read(BaselineOutput.DecisionsPathFor(output));
        Assert.Equal(
            [
                new SectionDecision(DecisionKind.Rejected, SectionIdentity.Of(Of("One")), "machine 1", null),
                new SectionDecision(DecisionKind.Taken, SectionIdentity.Of(Of("Three")), "machine 1", "## Three\n\nGamma."),
                new SectionDecision(DecisionKind.Dropped, SectionIdentity.Of(Of("Four")), null, null),
            ],
            earlier);

        // ⛔ Decision 10's poisoning, ruled out on disk: the version put off is nowhere in the file — not its
        // hash, not its words.
        string file = File.ReadAllText(BaselineOutput.DecisionsPathFor(output));
        Assert.DoesNotContain(SectionIdentity.Of(Of("Two")).Hash, file, StringComparison.Ordinal);
        Assert.DoesNotContain("Beta, reworded.", file, StringComparison.Ordinal);

        (ReviewHost second, ReviewSession next) = await OpenAsync(QueueOf(tree, ("machine 1", Machine1)), earlier);
        using (second)
        {
            Assert.Equal(3, next.Durable.Inherited);
            Assert.Empty(next.Durable.Made);
            Assert.Empty(next.Session.Deferred);
            Assert.Equal(0, next.Position);

            Assert.Equal(new SectionStanding(StandingKind.Rejected, earlier[0]), next.StandingOf(SectionOf(next, "One")));
            Assert.Equal(new SectionStanding(StandingKind.Taken, earlier[1]), next.StandingOf(SectionOf(next, "Three")));
            Assert.Equal(new SectionStanding(StandingKind.Dropped, earlier[2]), next.StandingOf(SectionOf(next, "Four")));

            // ⭐ And what was put off is offered again: it was never decided.
            SectionStanding putOff = next.StandingOf(SectionOf(next, "Two"));
            Assert.Equal(new SectionStanding(StandingKind.Undecided, null), putOff);
            Assert.True(putOff.IsPresented, "A section put off in one run is not offered in the next.");
        }

        TestLogSink.AssertNoWarnings(LogArea.Binding);
    }

    [AvaloniaFact]
    public async Task A_reworded_version_of_a_rejected_section_is_presented_again_and_an_identical_one_is_not()
    {
        TestLogSink.Instance.Clear();
        using FixtureTree tree = new();
        string output = Path.Combine(tree.MakeDirectory("Baseline"), "AGENTS.md");

        (ReviewHost first, ReviewSession run) = await OpenAsync(QueueOf(tree, ("machine 1", Machine1)));
        using (first)
        {
            run.Leave(SectionOf(run, "One"));
            BaselineOutput.Write(run, output);
        }

        IReadOnlyList<SectionDecision> earlier = DecisionFile.Read(BaselineOutput.DecisionsPathFor(output));
        string rewordedAgain = Machine1.Replace("Alpha, reworded.", "Alpha, reworded once more.", StringComparison.Ordinal);
        ReviewQueue queue = QueueOf(tree, ("machine 1", Machine1), ("machine 2", Machine1), ("machine 3", rewordedAgain));
        string later = Path.Combine(tree.MakeDirectory("Later"), "AGENTS.md");
        (ReviewHost second, ReviewSession next) = await OpenAsync(queue, earlier);
        using (second)
        {
            // The same words where they were left, and the same words on another machine: one version, left.
            SectionStanding same = next.StandingOf(SectionOf(next, "One"));
            Assert.Equal(StandingKind.Rejected, same.Kind);
            Assert.False(same.IsPresented, "An identical version of a rejected section is offered again.");
            Assert.Equal("already left, from \"machine 1\"", same.Spoken);

            MarkdownSection shownFirst = SectionOf(next, "One");
            Assert.True(await second.AwaitBuildAsync(next.Next));
            Assert.Equal("section", Assert.Throws<ArgumentException>(() => next.StandingOf(shownFirst)).ParamName);
            SectionStanding elsewhere = next.StandingOf(SectionOf(next, "One"));
            Assert.Equal(StandingKind.Rejected, elsewhere.Kind);
            Assert.False(elsewhere.IsPresented, "An identical version seen on another machine is offered again.");

            // ⭐ Reworded, it is a version nobody decided.
            Assert.True(await second.AwaitBuildAsync(next.Next));
            SectionStanding reworded = next.StandingOf(SectionOf(next, "One"));
            Assert.Equal(new SectionStanding(StandingKind.Undecided, null), reworded);
            Assert.True(reworded.IsPresented, "A reworded version of a rejected section is not offered again.");

            // The report beside a later save counts what came from before and lists what this run made.
            BaselineOutput.Write(next, later);
            Assert.EndsWith(
                "## Decisions, in the order made\n\n"
                + "One decision made before this run came with it and is not listed here: the decision file beside this "
                + "report keeps it.\n\nNone: no section was taken, left or dropped.\n",
                File.ReadAllText(BaselineOutput.ReportPathFor(later)),
                StringComparison.Ordinal);
        }

        TestLogSink.AssertNoWarnings(LogArea.Binding);
    }

    [AvaloniaFact]
    public async Task An_edited_take_is_saved_with_its_result_and_the_hash_its_source_had_over_normalised_text()
    {
        TestLogSink.Instance.Clear();
        using FixtureTree tree = new();

        (IReadOnlyList<SectionDecision> saved, string baseline) = await SaveAnEditedTakeAsync(tree);

        SectionDecision take = Assert.Single(saved);
        Assert.Equal(DecisionKind.Taken, take.Kind);
        Assert.Equal("janushalo", take.Source);
        Assert.Equal("Two", take.Section.HeadingPath);
        Assert.Equal(TakenTwo, take.Text);

        // ⭐ The source's version when it was taken, hashed as decision 6 hashes it: over the normalised text.
        // ⛔ Not over the bytes JanusHalo holds, which carry two spaces of list indent and a CR on every line.
        Assert.Equal(Sha256(TakenTwo), take.Section.Hash);
        Assert.NotEqual(Sha256("  ## Two\r\n  \r\n  Beta, as janushalo has it."), take.Section.Hash);

        // ⭐ The result, which nothing else holds once the session has gone.
        Assert.True(take.Edited, "The take was edited by hand, and the file says it was not.");
        Assert.Equal(EditedTwo, take.Result);
        Assert.Contains(EditedTwo, File.ReadAllText(baseline), StringComparison.Ordinal);

        TestLogSink.AssertNoWarnings(LogArea.Binding);
    }

    [AvaloniaFact]
    public async Task An_edit_whose_source_is_unchanged_reads_your_edit_stands_and_one_whose_source_moved_reads_re_review()
    {
        TestLogSink.Instance.Clear();
        using FixtureTree tree = new();
        (IReadOnlyList<SectionDecision> earlier, _) = await SaveAnEditedTakeAsync(tree);

        // ⭐ JanusHalo unchanged — though no longer nested and no longer CRLF, which ingest takes away — and a
        // second machine carrying the version JanusHalo had when it was taken.
        (ReviewHost unchanged, ReviewSession run) = await OpenAsync(
            QueueOf(tree, ("janushalo", JanusHalo), ("machine 2", JanusHalo)), earlier);
        using (unchanged)
        {
            SectionStanding stands = run.StandingOf(SectionOf(run, "Two"));
            Assert.Equal(new SectionStanding(StandingKind.EditStands, earlier[0]), stands);
            Assert.False(stands.IsPresented, "An edit whose source is unchanged is offered for review again.");
            Assert.Equal("taken from \"janushalo\" and then edited: \"janushalo\" is unchanged, your edit stands", stands.Spoken);

            // The same version on another machine is no question of JanusHalo's: it was taken, then edited.
            Assert.True(await unchanged.AwaitBuildAsync(run.Next));
            SectionStanding seenElsewhere = run.StandingOf(SectionOf(run, "Two"));
            Assert.Equal(new SectionStanding(StandingKind.Taken, earlier[0]), seenElsewhere);
            Assert.Equal("taken from \"janushalo\" and then edited", seenElsewhere.Spoken);
        }

        // ⭐ JanusHalo has rewritten what was taken from it: the edit was made against a version that is gone.
        string rewritten = JanusHalo.Replace("Beta, as janushalo has it.", "Beta, rewritten by janushalo since.", StringComparison.Ordinal);
        (ReviewHost moved, ReviewSession later) = await OpenAsync(QueueOf(tree, ("janushalo", rewritten)), earlier);
        using (moved)
        {
            MarkdownSection two = SectionOf(later, "Two");
            SectionStanding review = later.StandingOf(two);
            Assert.Equal(new SectionStanding(StandingKind.ReReview, earlier[0]), review);
            Assert.True(review.IsPresented, "An edit whose source has moved is not offered for review.");
            Assert.Equal("taken from \"janushalo\" and then edited: \"janushalo\" has rewritten this, re-review it", review.Spoken);
            Assert.Equal(EditedTwo, review.Decision!.Result);

            // Reviewed again and left, the rewrite is answered by that decision, not asked again.
            later.Leave(two);
            Assert.Equal(StandingKind.Rejected, later.StandingOf(two).Kind);
        }

        TestLogSink.AssertNoWarnings(LogArea.Binding);
    }

    [AvaloniaFact]
    public async Task A_save_reads_each_take_against_the_section_it_left_and_only_while_that_section_follows_from_it()
    {
        TestLogSink.Instance.Clear();
        using FixtureTree tree = new();
        MarkdownOutline machine1 = MarkdownOutline.Read(Machine1);
        MarkdownOutline machine2 = MarkdownOutline.Read(Machine2);
        string output = Path.Combine(tree.MakeDirectory("Baseline"), "AGENTS.md");

        (ReviewHost host, ReviewSession session) = await OpenAsync(QueueOf(tree, ("machine 1", Machine1), ("machine 2", Machine2)));
        using (host)
        {
            session.Take(SectionOf(session, "One"));
            session.Take(SectionOf(session, "Two"));
            int end = host.View.LeftDocument.Text.IndexOf("Beta, reworded.", StringComparison.Ordinal) + "Beta, reworded.".Length;
            host.Type(host.Left, end, " Kept.");
            Assert.Equal(TakeResult.Inserted, session.Take(SectionOf(session, "Three")));
            host.Left.Undo();
            ReviewHost.Layout();
            Assert.DoesNotContain("## Three", host.View.LeftDocument.Text, StringComparison.Ordinal);

            Assert.True(await host.AwaitBuildAsync(session.Next));
            session.Leave(SectionOf(session, "Two"));
            session.Take(SectionOf(session, "One"));

            BaselineOutput.Write(session, output);
        }

        SectionIdentity Of(MarkdownOutline outline, string path) => SectionIdentity.Of(outline.Sections.Single(s => s.HeadingPath == path));
        Assert.Equal(
            [
                // Replaced by machine 2's One afterwards: history, kept as taken, not read as an edit of it.
                new SectionDecision(DecisionKind.Taken, Of(machine1, "One"), "machine 1", "## One\n\nAlpha, reworded."),

                // Edited by hand, and still what the left's Two follows from: a later leave writes nothing there.
                new SectionDecision(DecisionKind.Taken, Of(machine1, "Two"), "machine 1", "## Two\n\nBeta, reworded.")
                {
                    Edited = true,
                    Result = "## Two\n\nBeta, reworded. Kept.",
                },

                // Undone, so the left holds no Three: edited away, with no result.
                new SectionDecision(DecisionKind.Taken, Of(machine1, "Three"), "machine 1", "## Three\n\nGamma.") { Edited = true },

                new SectionDecision(DecisionKind.Rejected, Of(machine2, "Two"), "machine 2", null),
                new SectionDecision(DecisionKind.Taken, Of(machine2, "One"), "machine 2", "## One\n\nAlpha, as machine 2 has it."),
            ],
            DecisionFile.Read(BaselineOutput.DecisionsPathFor(output)));

        TestLogSink.AssertNoWarnings(LogArea.Binding);
    }

    [AvaloniaFact]
    public async Task A_save_that_cannot_tell_what_a_take_became_is_refused_and_writes_nothing()
    {
        TestLogSink.Instance.Clear();
        using FixtureTree tree = new();
        string directory = tree.MakeDirectory("Baseline");
        string output = Path.Combine(directory, "AGENTS.md");

        (ReviewHost host, ReviewSession session) = await OpenAsync(QueueOf(tree, ("machine 1", Machine1)));
        using (host)
        {
            session.Take(SectionOf(session, "One"));

            // A second One, as a paste would land it: the take could have become either.
            host.View.LeftDocument.Insert(host.View.LeftDocument.TextLength, "\n## One\n\nAnother One, pasted.\n");

            SaveRefusedException refused = Assert.Throws<SaveRefusedException>(() => BaselineOutput.Write(session, output));
            Assert.Equal(
                "the accumulator holds 2 sections at \"One\", which was taken from \"machine 1\", so a save cannot tell what that "
                + "take became — make their headings differ by hand first",
                refused.Message);
            Assert.Empty(Directory.GetFileSystemEntries(directory));
        }

        TestLogSink.AssertNoWarnings(LogArea.Binding);
    }

    [AvaloniaFact]
    public async Task The_report_lists_the_decisions_this_run_made_and_the_file_keeps_every_runs_as_it_read_them()
    {
        TestLogSink.Instance.Clear();
        using FixtureTree tree = new();
        string first = Path.Combine(tree.MakeDirectory("First"), "AGENTS.md");
        string second = Path.Combine(tree.MakeDirectory("Second"), "AGENTS.md");

        (ReviewHost host, ReviewSession run) = await OpenAsync(QueueOf(tree, ("machine 1", Machine1)));
        using (host)
        {
            run.Leave(SectionOf(run, "One"));
            run.Take(SectionOf(run, "Three"));
            BaselineOutput.Write(run, first);
        }

        IReadOnlyList<SectionDecision> earlier = DecisionFile.Read(BaselineOutput.DecisionsPathFor(first));
        (ReviewHost again, ReviewSession next) = await OpenAsync(QueueOf(tree, ("machine 1", Machine1)), earlier);
        using (again)
        {
            next.Take(SectionOf(next, "Two"));
            SectionDecision taken = Assert.Single(next.Durable.Made);
            BaselineOutput.Write(next, second);

            // ⭐ This run's seed carries no Three, and the earlier take of it is kept as it was read: a later
            // save reads only the takes it made against the text it writes.
            Assert.Equal([.. earlier, taken], DecisionFile.Read(BaselineOutput.DecisionsPathFor(second)));
        }

        Assert.EndsWith(
            "## Decisions, in the order made\n\n"
            + "2 decisions made before this run came with it and are not listed here: the decision file beside this report "
            + "keeps them.\n\n| # | Decision | Section | From |\n|---|---|---|---|\n| 1 | taken | Two | machine 1 |\n",
            File.ReadAllText(BaselineOutput.ReportPathFor(second)),
            StringComparison.Ordinal);

        TestLogSink.AssertNoWarnings(LogArea.Binding);
    }

    [AvaloniaFact]
    public async Task A_review_the_window_saved_is_taken_up_by_the_next_window_with_every_decision_it_made()
    {
        TestLogSink.Instance.Clear();
        using FixtureTree tree = new();
        string output = Path.Combine(tree.MakeDirectory("Baseline"), "AGENTS.md");

        ReviewWindow window = new() { ChooseSavePath = () => Task.FromResult<string?>(output) };
        using (ReviewHost host = new(window))
        {
            await host.AwaitBuildAsync(() => window.Review(QueueOf(tree, ("machine 1", Machine1))));
            ReviewHost.PutCaret(host.Right, 5);
            Click(window.LeaveButton);
            Click(window.SaveButton);

            Assert.Equal(
                $"Saved the baseline to {output}, its provenance to {BaselineOutput.ReportPathFor(output)}, and its decisions "
                + $"to {Path.ChangeExtension(output, ".decisions.json")}.",
                window.OutcomeText.Text);
        }

        ReviewWindow next = new();
        using (ReviewHost host = new(next))
        {
            ReviewSession session = await host.AwaitBuildAsync(() =>
                next.Review(QueueOf(tree, ("machine 1", Machine1)), DecisionFile.Read(BaselineOutput.DecisionsPathFor(output))));

            Assert.Equal(1, session.Durable.Inherited);
            Assert.Equal(StandingKind.Rejected, session.StandingOf(SectionOf(session, "One")).Kind);
        }

        TestLogSink.AssertNoWarnings(LogArea.Binding);
    }

    [AvaloniaFact]
    public async Task A_save_never_puts_its_decision_file_over_a_file_the_corpus_read()
    {
        TestLogSink.Instance.Clear();
        using FixtureTree tree = new();

        // The candidate is named as a decision file is, so a baseline beside it has its decisions land on it.
        ReviewQueue queue = QueueOf(tree, "AGENTS.decisions.json", ("machine 1", Machine1));
        string candidate = queue.Items[0].Candidate.Path;
        byte[] candidateBytes = File.ReadAllBytes(candidate);
        string chosen = Path.Combine(Path.GetDirectoryName(candidate)!, "AGENTS.md");
        (ReviewHost host, ReviewSession session) = await OpenAsync(queue);
        using (host)
        {
            session.Leave(SectionOf(session, "One"));
            SaveRefusedException refused = Assert.Throws<SaveRefusedException>(() => BaselineOutput.Write(session, chosen));

            Assert.Equal(
                $"its decision file would be written over {candidate}, which is the candidate \"machine 1\" — a save never "
                + "writes over a file the corpus read, so choose a new path",
                refused.Message);
            Assert.Equal(candidateBytes, File.ReadAllBytes(candidate));
            Assert.Equal(["AGENTS.decisions.json"], Directory.GetFiles(Path.GetDirectoryName(candidate)!).Select(Path.GetFileName));
        }

        TestLogSink.AssertNoWarnings(LogArea.Binding);
    }
}
