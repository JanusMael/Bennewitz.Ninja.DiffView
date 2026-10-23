using System.Text;
using Bennewitz.Ninja.DiffView.Core;

namespace Bennewitz.Ninja.AgentBaseline;

/// <summary>
/// A save refused before a byte was written: the path is a file the corpus read, which decision 9 says
/// this tool never writes over — or the accumulator does not let the save tell what a take became.
/// </summary>
internal sealed class SaveRefusedException(string message) : Exception(message);

/// <summary>Where a save wrote the baseline, its provenance report and its decision file, as full paths.</summary>
internal sealed record SavedBaseline(string BaselinePath, string ReportPath, string DecisionsPath);

/// <summary>
/// Writes the accumulator out as the new baseline, with a provenance report and a decision file beside
/// it — plan 00034 decision 9: as LF, to a new path the operator chooses, by this tool rather than by the
/// view.
/// </summary>
/// <remarks>
/// <para>
/// ⛔ <b>Never over a file the corpus read.</b> The view's own save was ruled out because it writes a side
/// back to its source's path, which for the accumulator would be the seed machine's live file — and a
/// path chosen in a dialog can name that file just as well, so the refusal lives here and not only in
/// what the panes carry. The seed and every queued candidate are refused, for the baseline, its report
/// and its decision file alike, before anything is written. Paths are compared without case: Windows'
/// file systems and macOS's default one ignore it, and refusing a name that differs only in case costs
/// nothing where they do not.
/// </para>
/// <para>
/// The baseline is the accumulator's text as it stands — takes, drops and typing alike — written LF and
/// as UTF-8 without a byte-order mark: one convention for a file distributed to four platforms, whatever
/// each candidate used. ⚠ A first LF baseline lands on a Windows machine as a whole-file change once,
/// which decision 9 accepts.
/// </para>
/// <para>
/// The report is <see cref="ReportPathFor"/>: the seed, the queue in review order, and every durable
/// decision this run made, in the order made — the decision log §1 calls the product. Decisions an earlier
/// run made it counts and does not list: the decision file keeps them. ⚠ It is the log and nothing more
/// yet: whether the saved text still carries what each take took — an undo, or the take edited by hand —
/// is decision 9c's to mark at step 10, beside step 9's staleness.
/// </para>
/// <para>
/// ⭐ The decision file is <see cref="DecisionsPathFor"/>, step 8's durable store on disk: every run's
/// decisions, each take this run made read against the text being saved
/// (<see cref="DurableStore.Reconcile"/>), and nothing of the session store. A take the save cannot read
/// refuses it before anything is written, as a path the corpus read does. A later run begins from that
/// file, so an operator who keeps it beside the baseline keeps every decision behind it.
/// </para>
/// </remarks>
internal static class BaselineOutput
{
    /// <summary>
    /// The report's path for a baseline at <paramref name="baselinePath"/>: beside it, with
    /// <c>.provenance.md</c> for its extension — never a name a walk for the baseline's own target finds.
    /// </summary>
    internal static string ReportPathFor(string baselinePath) => Path.ChangeExtension(baselinePath, ".provenance.md");

    /// <summary>
    /// The decision file's path for a baseline at <paramref name="baselinePath"/>: beside it, with
    /// <c>.decisions.json</c> for its extension.
    /// </summary>
    internal static string DecisionsPathFor(string baselinePath) => Path.ChangeExtension(baselinePath, ".decisions.json");

    /// <summary>
    /// Writes <paramref name="session"/>'s accumulator to <paramref name="path"/> as the baseline, and its
    /// provenance and its decisions beside it.
    /// </summary>
    /// <exception cref="SaveRefusedException">
    /// A file would be one the corpus read, or a take cannot be read against the accumulator; nothing was written.
    /// </exception>
    /// <exception cref="IOException">A file could not be written.</exception>
    /// <exception cref="UnauthorizedAccessException">A file may not be written.</exception>
    internal static SavedBaseline Write(ReviewSession session, string path)
    {
        ArgumentNullException.ThrowIfNull(session);
        ArgumentException.ThrowIfNullOrWhiteSpace(path);

        string baseline = Path.GetFullPath(path);
        string report = ReportPathFor(baseline);
        string decisions = DecisionsPathFor(baseline);
        RefuseAnyFileRead(session.Queue, baseline, "the baseline");
        RefuseAnyFileRead(session.Queue, report, "its provenance report");
        RefuseAnyFileRead(session.Queue, decisions, "its decision file");
        IReadOnlyList<SectionDecision> kept = session.Durable.Reconcile(session.Accumulator);

        PaneWriter.Write(baseline, session.Accumulator, encoding: null, LineEnding.Lf);
        PaneWriter.Write(report, Report(session, Path.GetFileName(baseline)), encoding: null, LineEnding.Lf);
        DecisionFile.Write(decisions, kept);
        return new SavedBaseline(baseline, report, decisions);
    }

    /// <summary>
    /// The provenance report for <paramref name="session"/>, for a baseline saved as
    /// <paramref name="baselineName"/>: markdown, one line ending per line.
    /// </summary>
    internal static string Report(ReviewSession session, string baselineName)
    {
        ArgumentNullException.ThrowIfNull(session);

        Candidate seed = session.Queue.Seed;
        StringBuilder report = new();
        report.Append("# Provenance of ").Append(baselineName).Append("\n\n");
        report.Append("Seeded from \"").Append(seed.Label).Append("\", `").Append(seed.Path).Append("`.\n\n");

        report.Append("## The queue, in review order\n\n");
        report.Append("| Candidate | File |\n|---|---|\n");
        foreach (QueuedCandidate item in session.Queue.Items)
        {
            report.Append("| ").Append(Cell(item.Candidate.Label)).Append(" | `").Append(Cell(item.Candidate.Path)).Append("` |\n");
        }

        report.Append("\n## Decisions, in the order made\n\n");
        int earlier = session.Durable.Inherited;
        if (earlier > 0)
        {
            report.Append(earlier == 1
                ? "One decision made before this run came with it and is not listed here: the decision file beside this report keeps it.\n\n"
                : $"{earlier} decisions made before this run came with it and are not listed here: the decision file beside this report keeps them.\n\n");
        }

        IReadOnlyList<SectionDecision> decisions = session.Durable.Made;
        if (decisions.Count == 0)
        {
            report.Append("None: no section was taken, left or dropped.\n");
            return report.ToString();
        }

        report.Append("| # | Decision | Section | From |\n|---|---|---|---|\n");
        for (int i = 0; i < decisions.Count; i++)
        {
            SectionDecision decision = decisions[i];
            report.Append("| ").Append(i + 1)
                .Append(" | ").Append(decision.Kind.Spoken())
                .Append(" | ").Append(SectionCell(decision.Section.HeadingPath))
                .Append(" | ").Append(decision.Source is { } source ? Cell(source) : "—")
                .Append(" |\n");
        }

        return report.ToString();
    }

    /// <summary>Refuses <paramref name="written"/> when it is the seed's file or a queued candidate's.</summary>
    private static void RefuseAnyFileRead(ReviewQueue queue, string written, string what)
    {
        foreach (Candidate read in (Candidate[])[queue.Seed, .. queue.Items.Select(i => i.Candidate)])
        {
            if (string.Equals(Path.GetFullPath(read.Path), written, StringComparison.OrdinalIgnoreCase))
            {
                string whose = ReferenceEquals(read, queue.Seed) ? "the seed" : "the candidate";
                throw new SaveRefusedException(
                    $"{what} would be written over {written}, which is {whose} \"{read.Label}\" — a save never writes "
                    + "over a file the corpus read, so choose a new path");
            }
        }
    }

    /// <summary>A section in the report's table: its heading path, or the title, whose path is empty.</summary>
    private static string SectionCell(string headingPath) => headingPath.Length == 0 ? "*the title*" : Cell(headingPath);

    /// <summary><paramref name="text"/> as a table cell holds it: a pipe would end the cell.</summary>
    private static string Cell(string text) => text.Replace("|", "\\|", StringComparison.Ordinal);
}
