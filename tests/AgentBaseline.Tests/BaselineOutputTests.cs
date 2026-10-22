using System.Text;
using Avalonia.Automation.Peers;
using Avalonia.Automation.Provider;
using Avalonia.Controls;
using Avalonia.Headless.XUnit;
using Avalonia.Logging;
using Bennewitz.Ninja.DiffView.Tests;

namespace Bennewitz.Ninja.AgentBaseline.Tests;

/// <summary>
/// Plan 00034 step 7's save: the baseline written by the tool, as LF, to a path the operator chooses and
/// never over a file the corpus read — decision 9 — with its provenance beside it.
/// </summary>
/// <remarks>
/// The report is the decision log: the seed, the queue and every durable decision in the order made.
/// Marking a take the accumulator no longer carries as it was taken — decision 9c — and the staleness a
/// save warns of are steps 10 and 9, and nothing here asserts them.
/// </remarks>
public sealed class BaselineOutputTests
{
    private const string Seed = "# Rules\n\n## One\n\nAlpha.\n\n## Three\n\nGamma.\n\n## Four\n\nDelta.\n";

    private const string OneReworded = "# Rules\n\n## One\n\nAlpha, reworded.\n\n## Three\n\nGamma.\n\n## Four\n\nDelta.\n";

    private static ReviewQueue QueueOf(FixtureTree tree, string fileName, string seed, params (string Label, string Text)[] candidates)
    {
        List<CorpusEntry> entries = [Entry(tree.Write($"Seed/{fileName}", seed), "seed")];
        for (int i = 0; i < candidates.Length; i++)
        {
            entries.Add(Entry(tree.Write($"Machine{i + 1}/{fileName}", candidates[i].Text), candidates[i].Label));
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

    /// <summary>A window reviewing <paramref name="queue"/>, every save going to what <paramref name="choose"/> answers.</summary>
    private static async Task<(ReviewHost Host, ReviewWindow Window, ReviewSession Session)> OpenAsync(
        ReviewQueue queue, Func<Task<string?>> choose)
    {
        ReviewWindow window = new() { ChooseSavePath = choose };
        ReviewHost host = new(window);
        ReviewSession session = await host.AwaitBuildAsync(() => window.Review(queue));
        return (host, window, session);
    }

    [AvaloniaFact]
    public async Task A_save_never_writes_the_baseline_over_a_file_the_corpus_read()
    {
        TestLogSink.Instance.Clear();
        using FixtureTree tree = new();
        ReviewQueue queue = QueueOf(tree, "AGENTS.md", Seed, ("machine 1", OneReworded));
        byte[] seedBytes = File.ReadAllBytes(queue.Seed.Path);
        string chosen = queue.Seed.Path;
        (ReviewHost host, ReviewWindow window, _) = await OpenAsync(queue, () => Task.FromResult<string?>(chosen));
        using (host)
        {
            // ⛔ Typed first, so the accumulator differs from the file: a save of the seed's own text over its
            // own file would leave its bytes as they were, and the assertion on them would prove nothing.
            host.Type(host.Left, 0, "Edited. ");
            Click(window.SaveButton);

            Assert.Equal(
                $"Not saved: the baseline would be written over {queue.Seed.Path}, which is the seed \"seed\" — a save "
                + "never writes over a file the corpus read, so choose a new path",
                window.OutcomeText.Text);
            Assert.Equal(seedBytes, File.ReadAllBytes(queue.Seed.Path));
            Assert.False(
                File.Exists(Path.ChangeExtension(queue.Seed.Path, ".provenance.md")),
                "A refused save still wrote its report beside the seed's file.");

            // ⚠ A path that differs only in case names the same file on Windows and on macOS's default file
            // system, so it is refused on every platform rather than on the ones that ignore case.
            string candidate = queue.Items[0].Candidate.Path;
            byte[] candidateBytes = File.ReadAllBytes(candidate);
            chosen = Path.Combine(Path.GetDirectoryName(candidate)!, "agents.MD");
            Click(window.SaveButton);

            Assert.StartsWith("Not saved: the baseline would be written over ", window.OutcomeText.Text, StringComparison.Ordinal);
            Assert.EndsWith(", which is the candidate \"machine 1\" — a save never writes over a file the corpus read, so choose a new path", window.OutcomeText.Text, StringComparison.Ordinal);
            Assert.Equal(candidateBytes, File.ReadAllBytes(candidate));
            Assert.Equal(["AGENTS.md"], Directory.GetFiles(Path.GetDirectoryName(candidate)!).Select(Path.GetFileName));
        }

        TestLogSink.AssertNoWarnings(LogArea.Binding);
    }

    [AvaloniaFact]
    public async Task A_save_never_puts_its_report_over_a_file_the_corpus_read()
    {
        TestLogSink.Instance.Clear();
        using FixtureTree tree = new();

        // The candidates are named as a report would be, so a baseline beside one has its report land on it.
        ReviewQueue queue = QueueOf(tree, "AGENTS.provenance.md", Seed, ("machine 1", OneReworded));
        string candidate = queue.Items[0].Candidate.Path;
        byte[] candidateBytes = File.ReadAllBytes(candidate);
        string chosen = Path.Combine(Path.GetDirectoryName(candidate)!, "AGENTS.md");
        (ReviewHost host, _, ReviewSession session) = await OpenAsync(queue, () => Task.FromResult<string?>(chosen));
        using (host)
        {
            SaveRefusedException refused = Assert.Throws<SaveRefusedException>(() => BaselineOutput.Write(session, chosen));

            Assert.Equal(
                $"its provenance report would be written over {candidate}, which is the candidate \"machine 1\" — a "
                + "save never writes over a file the corpus read, so choose a new path",
                refused.Message);
            Assert.Equal(candidateBytes, File.ReadAllBytes(candidate));
            Assert.False(File.Exists(chosen), "A refused save still wrote the baseline before refusing its report.");
        }

        TestLogSink.AssertNoWarnings(LogArea.Binding);
    }

    [AvaloniaFact]
    public async Task The_baseline_is_written_LF_and_without_a_byte_order_mark_whatever_the_left_holds()
    {
        TestLogSink.Instance.Clear();
        using FixtureTree tree = new();

        // The seed arrives in CRLF behind a byte-order mark, and the review shows it normalised.
        List<CorpusEntry> entries =
        [
            Entry(tree.WriteBytes("Seed/AGENTS.md", [0xEF, 0xBB, 0xBF, .. Encoding.UTF8.GetBytes(Seed.Replace("\n", "\r\n", StringComparison.Ordinal))]), "seed"),
            Entry(tree.Write("Machine1/AGENTS.md", OneReworded), "machine 1"),
        ];
        ReviewQueue queue = Corpus.Gather(CorpusRequest.Create(entries, seed: "seed", order: CorpusOrder.Listed))
            .Queue(cancellationToken: TestContext.Current.CancellationToken);
        string output = Path.Combine(tree.MakeDirectory("Baseline"), "AGENTS.md");
        (ReviewHost host, ReviewWindow window, _) = await OpenAsync(queue, () => Task.FromResult<string?>(output));
        using (host)
        {
            // ⚠ Straight onto the document, as a paste would land it: a typed line break is the document's own
            // LF, so typing cannot put a carriage return in the accumulator.
            host.View.LeftDocument.Insert(0, "Pasted.\r\nAnd a lone return.\r");
            Click(window.SaveButton);

            Assert.Equal(Encoding.UTF8.GetBytes("Pasted.\nAnd a lone return.\n" + Seed), File.ReadAllBytes(output));
        }

        TestLogSink.AssertNoWarnings(LogArea.Binding);
    }

    [AvaloniaFact]
    public async Task The_report_logs_every_durable_decision_in_the_order_made_the_title_and_a_pipe_included()
    {
        TestLogSink.Instance.Clear();
        using FixtureTree tree = new();

        // A label holding a pipe, which would end a table cell, and a title of its own, which the report names
        // as the title rather than as an empty path.
        ReviewQueue queue = QueueOf(
            tree,
            "AGENTS.md",
            Seed,
            ("rack 3 | slot 2", OneReworded.Replace("# Rules", "# Rules, from the rack", StringComparison.Ordinal)));
        string output = Path.Combine(tree.MakeDirectory("Baseline"), "AGENTS.md");
        (ReviewHost host, _, ReviewSession session) = await OpenAsync(queue, () => Task.FromResult<string?>(output));
        using (host)
        {
            IReadOnlyList<MarkdownSection> sections = session.Current.Candidate.Document.Outline.Sections;
            session.Leave(sections[0]);
            session.Take(sections[1]);
            session.Defer(sections[2]);
            session.Drop("Four");

            BaselineOutput.Write(session, output);

            // ⛔ The deferral is not here: it is the session's, and the report is what the durable store holds.
            Assert.Equal(
                $"# Provenance of AGENTS.md\n\nSeeded from \"seed\", `{queue.Seed.Path}`.\n\n"
                + "## The queue, in review order\n\n| Candidate | File |\n|---|---|\n"
                + $"| rack 3 \\| slot 2 | `{queue.Items[0].Candidate.Path}` |\n\n"
                + "## Decisions, in the order made\n\n| # | Decision | Section | From |\n|---|---|---|---|\n"
                + "| 1 | left | *the title* | rack 3 \\| slot 2 |\n"
                + "| 2 | taken | One | rack 3 \\| slot 2 |\n"
                + "| 3 | dropped | Four | — |\n",
                File.ReadAllText(BaselineOutput.ReportPathFor(output)));
        }

        TestLogSink.AssertNoWarnings(LogArea.Binding);
    }

    [AvaloniaFact]
    public async Task A_save_before_any_decision_writes_the_seeds_text_and_says_nothing_was_decided()
    {
        TestLogSink.Instance.Clear();
        using FixtureTree tree = new();
        ReviewQueue queue = QueueOf(tree, "AGENTS.md", Seed, ("machine 1", OneReworded));
        string output = Path.Combine(tree.MakeDirectory("Baseline"), "AGENTS.md");
        (ReviewHost host, _, ReviewSession session) = await OpenAsync(queue, () => Task.FromResult<string?>(output));
        using (host)
        {
            SavedBaseline saved = BaselineOutput.Write(session, output);

            Assert.Equal(
                new SavedBaseline(
                    output,
                    Path.Combine(tree.PathOf("Baseline"), "AGENTS.provenance.md"),
                    Path.Combine(tree.PathOf("Baseline"), "AGENTS.decisions.json")),
                saved);
            Assert.Equal(Encoding.UTF8.GetBytes(Seed), File.ReadAllBytes(output));
            Assert.EndsWith(
                "\n\n## Decisions, in the order made\n\nNone: no section was taken, left or dropped.\n",
                File.ReadAllText(saved.ReportPath),
                StringComparison.Ordinal);
        }

        TestLogSink.AssertNoWarnings(LogArea.Binding);
    }

    [AvaloniaFact]
    public async Task A_save_that_chooses_nothing_or_cannot_write_says_so_and_writes_nothing()
    {
        TestLogSink.Instance.Clear();
        using FixtureTree tree = new();
        ReviewQueue queue = QueueOf(tree, "AGENTS.md", Seed, ("machine 1", OneReworded));
        string baselineDirectory = tree.MakeDirectory("Baseline");
        Func<Task<string?>> choose = () => Task.FromResult<string?>(null);
        (ReviewHost host, ReviewWindow window, _) = await OpenAsync(queue, () => choose());
        using (host)
        {
            Click(window.SaveButton);
            Assert.Equal("Not saved: no file was chosen.", window.OutcomeText.Text);

            choose = () => throw new PlatformNotSupportedException("no dialog");
            Click(window.SaveButton);
            Assert.Equal("Not saved: there is no file dialog here.", window.OutcomeText.Text);

            // A directory that is not there: the write fails, and the window says how rather than throwing.
            string missing = Path.Combine(baselineDirectory, "not-there", "AGENTS.md");
            choose = () => Task.FromResult<string?>(missing);
            Click(window.SaveButton);
            Assert.StartsWith("Not saved: ", window.OutcomeText.Text, StringComparison.Ordinal);
            Assert.Contains("not-there", window.OutcomeText.Text, StringComparison.Ordinal);

            Assert.Empty(Directory.GetFileSystemEntries(baselineDirectory));
        }

        TestLogSink.AssertNoWarnings(LogArea.Binding);
    }
}
