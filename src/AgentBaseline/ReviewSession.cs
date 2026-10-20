using Bennewitz.Ninja.DiffView;
using Bennewitz.Ninja.DiffView.Core;

namespace Bennewitz.Ninja.AgentBaseline;

/// <summary>
/// The review: the seed's text on the left as an accumulator, edited by hand and never replaced, and
/// the queue's candidates shown on the right one at a time — plan 00034 step 5, <i>cycling with the
/// accumulator intact</i>. Each pane shows its document's normalised text, which is the text the queue
/// counted every candidate's changes over.
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

    /// <summary>Where in <see cref="ReviewQueue.Items"/> the right pane is.</summary>
    internal int Position { get; private set; }

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

    private void Show(int index)
    {
        Position = index;
        Candidate candidate = Queue.Items[index].Candidate;
        view.RightSource = new PaneSource(candidate.Document.Text) { Title = TitleOf(candidate.Label, candidate) };
    }

    private static string TitleOf(string name, Candidate candidate) =>
        $"{name} — {System.IO.Path.GetFileName(candidate.Path)}";
}
