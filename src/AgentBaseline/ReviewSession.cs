using AvaloniaEdit.Document;
using Bennewitz.Ninja.DiffView;
using Bennewitz.Ninja.DiffView.Core;

namespace Bennewitz.Ninja.AgentBaseline;

/// <summary>
/// The review: the seed's text on the left as an accumulator, edited by hand and never replaced, and
/// the queue's candidates shown on the right one at a time — plan 00034 step 5, <i>cycling with the
/// accumulator intact</i> — and the four decisions made about a candidate's sections, step 6. Each
/// pane shows its document's normalised text, which is the text the queue counted every candidate's
/// changes over.
/// </summary>
/// <remarks>
/// <para>
/// ⛔ <b>The left is assigned once, here, and never again.</b> Assigning a side a source that differs
/// from the one it holds replaces that side's <c>TextDocument</c> — AGENTS.md §6 — and with it every
/// edit and the undo stack, however faithfully the new source copies the text across. Cycling assigns
/// the right alone, which <c>DiffBuildController.OnSourceChanged</c> keeps strictly per side, and each
/// build compares the candidate against the left as it now stands: once the left has been edited, the
/// build reads its live text rather than the seed's.
/// </para>
/// <para>
/// ⚠ <b>A source equal to the one a side holds is not assigned at all</b>, measured: a
/// <see cref="PaneSource"/> is a record, and Avalonia raises a property change only for a value that
/// differs. So the same seed assigned twice is harmless, and the first assignment that differs in any
/// member — the text, the title, the path — is the defect. That is what makes it easy to write: a left
/// whose title counted the candidates would lose every edit on the first cycle.
/// </para>
/// <para>
/// ⛔ <b>Neither source carries a path.</b> <see cref="SideBySideDiffView.Save"/> writes a side back to
/// its <see cref="PaneSource.Path"/>, and the header's menu offers that save the moment the side is
/// dirty, so a left built from the seed's file would offer to overwrite that machine's live file —
/// which decision 9 exists to rule out. The baseline is written by this tool, to a new path, at step 10.
/// </para>
/// <para>
/// ⭐ <b>A take and a drop are edits of the left's own document</b>, positioned by a parse of the left's
/// live text and checked before they are made — <see cref="AccumulatorEdits"/>, decision 5 — never a
/// copy over the diff's alignment, which over a reordered candidate writes one rule over another. Each
/// is one edit, so one undo takes it back. A <c>leave</c> and a <c>defer</c> write nothing to the left:
/// the first goes to the <see cref="DurableStore"/>, the second to the <see cref="SessionStore"/>, and
/// that difference is the one decision 10 turns on.
/// </para>
/// <para>
/// ⚠ The stores record what was decided, and the left is the operator's to go on editing — an undo of
/// a take, or the take edited by hand. Reconciling the two is decision 9c's work at save, which reads the
/// left's section against the text the take recorded.
/// </para>
/// <para>
/// Every title ends with its candidate's own file name, because the control chooses a pane's grammar
/// from the title's extension when there is no path. A title of the label alone would highlight one
/// candidate and not the next, as a label happened to end in <c>.md</c> or not.
/// </para>
/// <para>Like the view it drives, a session is used on the UI thread only.</para>
/// </remarks>
internal sealed class ReviewSession
{
    private readonly SideBySideDiffView view;

    /// <summary>
    /// Seeds <paramref name="view"/>'s left with <paramref name="queue"/>'s seed and shows the first
    /// candidate on its right.
    /// </summary>
    /// <exception cref="CorpusRefusedException">
    /// The queue holds nothing to review — refused before the view is touched.
    /// </exception>
    internal ReviewSession(ReviewQueue queue, SideBySideDiffView view)
    {
        ArgumentNullException.ThrowIfNull(queue);
        ArgumentNullException.ThrowIfNull(view);
        if (queue.Items.Count == 0)
        {
            throw new CorpusRefusedException(
                $"nothing to review: the corpus holds only the seed, \"{queue.Seed.Label}\", and a file "
                + "has nothing to offer against itself");
        }

        Queue = queue;
        this.view = view;

        // Merging two phrasings is typing in the left (decision 9c), so the accumulator is editable. A
        // candidate is read and never changed: what a take records is the candidate's own version.
        view.LeftReadOnly = false;
        view.RightReadOnly = true;
        view.LeftSource = new PaneSource(queue.Seed.Document.Text)
        {
            Title = TitleOf($"baseline from {queue.Seed.Label}", queue.Seed),
        };
        Show(0);
    }

    /// <summary>The seed and the candidates under review, in the order they are reviewed.</summary>
    internal ReviewQueue Queue { get; }

    /// <summary>Taken, rejected and dropped: what the next run must know was decided.</summary>
    internal DurableStore Durable { get; } = new();

    /// <summary>Where the review is, and what it has put off for now.</summary>
    internal SessionStore Session { get; } = new();

    /// <summary>Where in <see cref="ReviewQueue.Items"/> the right pane is.</summary>
    internal int Position => Session.Position;

    /// <summary>The candidate the right pane shows.</summary>
    internal QueuedCandidate Current => Queue.Items[Position];

    /// <summary>
    /// Shows the next candidate. At the last there is none, so nothing moves and the answer is
    /// <see langword="false"/>.
    /// </summary>
    internal bool Next()
    {
        if (Position == Queue.Items.Count - 1)
        {
            return false;
        }

        Show(Position + 1);
        return true;
    }

    /// <summary>
    /// Shows the previous candidate. At the first there is none, so nothing moves and the answer is
    /// <see langword="false"/>.
    /// </summary>
    internal bool Previous()
    {
        if (Position == 0)
        {
            return false;
        }

        Show(Position - 1);
        return true;
    }

    /// <summary>Shows the candidate at <paramref name="index"/> in <see cref="ReviewQueue.Items"/>.</summary>
    /// <exception cref="ArgumentOutOfRangeException">There is no candidate there; nothing moves.</exception>
    internal void JumpTo(int index)
    {
        ArgumentOutOfRangeException.ThrowIfNegative(index);
        ArgumentOutOfRangeException.ThrowIfGreaterThanOrEqual(index, Queue.Items.Count);
        Show(index);
    }

    /// <summary>
    /// Takes <paramref name="section"/> — one of the right's sections — into the accumulator: its
    /// version replaces the left's section at the same heading path, or is inserted where the candidate
    /// has it when the left holds none. Recorded as taken, with the content taken.
    /// </summary>
    /// <exception cref="ArgumentException"><paramref name="section"/> is not one of the current candidate's.</exception>
    /// <exception cref="DecisionRefusedException">
    /// The take cannot be placed, or would change another section; nothing is written or recorded.
    /// </exception>
    internal TakeResult Take(MarkdownSection section)
    {
        int index = IndexInCurrent(section);
        TextDocument left = view.LeftDocument;
        (TextEdit edit, TakeResult result) = AccumulatorEdits.Take(left.Text, Current.Candidate.Document.Outline, index);
        if (result != TakeResult.Unchanged)
        {
            left.Replace(edit.Offset, edit.Length, edit.Text);
        }

        Decide(new SectionDecision(DecisionKind.Taken, SectionIdentity.Of(section), Current.Candidate.Label, section.Content));
        return result;
    }

    /// <summary>
    /// Leaves <paramref name="section"/> — one of the right's sections — out of the baseline, durably:
    /// the same version is not offered again. The left is not touched.
    /// </summary>
    /// <exception cref="ArgumentException"><paramref name="section"/> is not one of the current candidate's.</exception>
    internal void Leave(MarkdownSection section)
    {
        IndexInCurrent(section);
        Decide(new SectionDecision(DecisionKind.Rejected, SectionIdentity.Of(section), Current.Candidate.Label, null));
    }

    /// <summary>
    /// Puts <paramref name="section"/> — one of the right's sections — off for this session, deciding
    /// nothing: it is held in the session store and never in the durable one. The left is not touched.
    /// </summary>
    /// <exception cref="ArgumentException"><paramref name="section"/> is not one of the current candidate's.</exception>
    /// <exception cref="DecisionRefusedException">
    /// That version is already decided, and putting it off would leave the decision standing unseen.
    /// </exception>
    internal void Defer(MarkdownSection section)
    {
        IndexInCurrent(section);
        SectionIdentity identity = SectionIdentity.Of(section);
        if (Durable.LatestFor(identity) is { } decided)
        {
            throw new DecisionRefusedException(
                $"this version of {MarkdownOutline.Spoken(section.HeadingPath)} is already {Spoken(decided.Kind)}, and putting it off "
                + "would leave that decision standing where nobody can see it — decide it again instead");
        }

        Session.Defer(new DeferredSection(identity, Current.Candidate.Label));
    }

    /// <summary>
    /// Drops the accumulator's own section at <paramref name="headingPath"/> from the baseline, durably:
    /// the only decision that makes the baseline lose something the machines had (decision 10).
    /// </summary>
    /// <exception cref="DecisionRefusedException">
    /// The left holds no section at that path, or two, or dropping it would move another; nothing is
    /// written or recorded.
    /// </exception>
    internal void Drop(string headingPath)
    {
        TextDocument left = view.LeftDocument;
        (TextEdit removal, MarkdownSection dropped) = AccumulatorEdits.Drop(left.Text, headingPath);
        left.Replace(removal.Offset, removal.Length, removal.Text);
        Decide(new SectionDecision(DecisionKind.Dropped, SectionIdentity.Of(dropped), null, null));
    }

    private void Show(int index)
    {
        Session.Position = index;
        Candidate candidate = Queue.Items[index].Candidate;
        view.RightSource = new PaneSource(candidate.Document.Text) { Title = TitleOf(candidate.Label, candidate) };
    }

    /// <summary>Records a durable decision, which settles any deferral of the same version.</summary>
    private void Decide(SectionDecision decision)
    {
        Durable.Record(decision);
        Session.Settle(decision.Section);
    }

    /// <summary>
    /// Where <paramref name="section"/> is among the current candidate's sections. By reference: a
    /// section is a handle on the right's own outline, and an equal one from elsewhere is not it.
    /// </summary>
    private int IndexInCurrent(MarkdownSection section)
    {
        ArgumentNullException.ThrowIfNull(section);
        IReadOnlyList<MarkdownSection> sections = Current.Candidate.Document.Outline.Sections;
        for (int i = 0; i < sections.Count; i++)
        {
            if (ReferenceEquals(sections[i], section))
            {
                return i;
            }
        }

        throw new ArgumentException(
            $"{MarkdownOutline.Spoken(section.HeadingPath)} is not one of the sections of \"{Current.Candidate.Label}\", the candidate on the right",
            nameof(section));
    }

    private static string Spoken(DecisionKind kind) => kind switch
    {
        DecisionKind.Taken => "taken",
        DecisionKind.Rejected => "left",
        _ => "dropped",
    };

    private static string TitleOf(string name, Candidate candidate) =>
        $"{name} — {System.IO.Path.GetFileName(candidate.Path)}";
}
