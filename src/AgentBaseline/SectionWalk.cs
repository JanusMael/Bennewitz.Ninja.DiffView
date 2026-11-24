using Bennewitz.Ninja.DiffView.Core;
using Key = (int Row, int Pane, int Line);

namespace Bennewitz.Ninja.AgentBaseline;

/// <summary>What a press of the section walk found — plan 00038.</summary>
internal enum WalkOutcome
{
    /// <summary>The next or the previous section that differs, short of an end.</summary>
    Found,

    /// <summary>Nothing further that way, so the walk came round to the section at its other end.</summary>
    CameRound,

    /// <summary>Nothing further that way, and the walk does not wrap.</summary>
    AtEnd,

    /// <summary>No section differs: the candidate holds the accumulator's every version and nothing besides.</summary>
    NoneDiffers,

    /// <summary>The view has no model yet, so there are no rows to order the walk by.</summary>
    NotBuilt,
}

/// <summary>
/// A press of the section walk: what it found and, where it found a section, that section and the pane it goes to.
/// </summary>
/// <param name="Outcome">What the press found.</param>
/// <param name="Side">The pane the section goes to; meaningful only with a section.</param>
/// <param name="Section">The section, from that pane's outline, or <see langword="null"/> when the press found none.</param>
internal readonly record struct WalkStep(WalkOutcome Outcome, DiffSide Side, MarkdownSection? Section);

/// <summary>
/// Which sections the review window's <i>Previous section</i> and <i>Next section</i> walk, and in what order — plan
/// 00038.
/// </summary>
/// <remarks>
/// <para>
/// ⭐ <b>The walk holds the sections that differ</b> (decision 1): each of the candidate's sections that the accumulator
/// holds no identical version of, compared by <see cref="SectionIdentity"/> as a decision is, and each of the
/// accumulator's at a heading path the candidate has no section at. A section is walked in the pane that has it, the
/// right when both do (decision 2): the candidate's version is the one take, leave and defer read, and the
/// accumulator's alone is the one drop reads.
/// </para>
/// <para>
/// ⭐ <b>The order is the rows the panes show</b> (decision 3): each section at its heading's row in the pane it goes to,
/// and on one row the left's first. A row keeps each side's lines in order, so the candidate's sections keep the
/// candidate's order and a section only the accumulator has comes where it stands between them. The rows are the view's
/// public model; with none there is nothing to order by, and a line past the end of a model that lags an edit of the left
/// counts after its last row (decision 9).
/// </para>
/// <para>
/// A press counts from a caret (decision 4): past the section it is in, or past its own row when it is in no section. The
/// accumulator's version of a section that goes to the right pane counts as that section, so a press from either
/// version moves on from it rather than across to the other.
/// </para>
/// </remarks>
internal static class SectionWalk
{
    /// <summary>
    /// The next or the previous section that differs, counted from 1-based <paramref name="line"/> of
    /// <paramref name="from"/>'s pane, or from above every row when <paramref name="from"/> is <see langword="null"/>.
    /// </summary>
    /// <param name="accumulator">The left pane's outline, read from its live text.</param>
    /// <param name="candidate">The right pane's outline: the candidate as it is shown.</param>
    /// <param name="model">The view's model, or <see langword="null"/> before a build has landed.</param>
    /// <param name="from">The pane whose caret the press counts from, or <see langword="null"/> for the top.</param>
    /// <param name="line">That caret's line; read only with a pane.</param>
    /// <param name="forward">Whether the press goes to the next section or to the previous one.</param>
    /// <param name="wrap">Whether a press with nothing further that way comes round to the other end.</param>
    internal static WalkStep Step(
        MarkdownOutline accumulator,
        MarkdownOutline candidate,
        SideBySideDocument? model,
        DiffSide? from,
        int line,
        bool forward,
        bool wrap)
    {
        ArgumentNullException.ThrowIfNull(accumulator);
        ArgumentNullException.ThrowIfNull(candidate);

        List<Stop> stops = StopsOf(accumulator, candidate);
        if (stops.Count == 0)
        {
            return new WalkStep(WalkOutcome.NoneDiffers, default, null);
        }

        if (model is null)
        {
            return new WalkStep(WalkOutcome.NotBuilt, default, null);
        }

        List<(Key Key, Stop Stop)> walk =
            [.. stops.Select(s => (Key: KeyOf(model, s.Side, s.Section.FirstLine), Stop: s)).OrderBy(p => p.Key)];
        Key anchor = AnchorOf(accumulator, candidate, model, walk, from, line);
        int at = forward
            ? walk.FindIndex(p => p.Key.CompareTo(anchor) > 0)
            : walk.FindLastIndex(p => p.Key.CompareTo(anchor) < 0);
        if (at >= 0)
        {
            return new WalkStep(WalkOutcome.Found, walk[at].Stop.Side, walk[at].Stop.Section);
        }

        if (!wrap)
        {
            return new WalkStep(WalkOutcome.AtEnd, default, null);
        }

        Stop round = forward ? walk[0].Stop : walk[^1].Stop;
        return new WalkStep(WalkOutcome.CameRound, round.Side, round.Section);
    }

    /// <summary>The sections that differ (decision 1), each with the pane it goes to (decision 2).</summary>
    private static List<Stop> StopsOf(MarkdownOutline accumulator, MarkdownOutline candidate)
    {
        HashSet<SectionIdentity> held = [.. accumulator.Sections.Select(VersionOf)];
        HashSet<string> paths = new(candidate.Sections.Select(s => s.HeadingPath), StringComparer.Ordinal);
        List<Stop> stops = [.. candidate.Sections.Where(s => !held.Contains(VersionOf(s))).Select(s => new Stop(DiffSide.Right, s))];
        stops.AddRange(accumulator.Sections.Where(s => !paths.Contains(s.HeadingPath)).Select(s => new Stop(DiffSide.Left, s)));
        return stops;
    }

    /// <summary>
    /// The version a section is, as a decision reads it: its heading path and its folded content, so a paragraph
    /// broken otherwise is the same version, and the walk and the line under the panes agree.
    /// </summary>
    private static SectionIdentity VersionOf(MarkdownSection section) => SectionIdentity.Of(section);

    /// <summary>
    /// Where a press counts from: the top; the section the caret is in, which for the accumulator's version of a section
    /// that goes to the right pane is that section's place in the walk; or, in no section, the caret's own row.
    /// </summary>
    private static Key AnchorOf(
        MarkdownOutline accumulator,
        MarkdownOutline candidate,
        SideBySideDocument model,
        List<(Key Key, Stop Stop)> walk,
        DiffSide? from,
        int line)
    {
        if (from is not { } side)
        {
            return (-1, -1, -1);
        }

        if (ReviewSession.SectionAt(side == DiffSide.Left ? accumulator : candidate, line) is not { } section)
        {
            return KeyOf(model, side, line);
        }

        if (side == DiffSide.Left)
        {
            foreach ((Key key, Stop stop) in walk)
            {
                if (stop.Side == DiffSide.Right && string.Equals(stop.Section.HeadingPath, section.HeadingPath, StringComparison.Ordinal))
                {
                    return key;
                }
            }
        }

        return KeyOf(model, side, section.FirstLine);
    }

    /// <summary>
    /// Where 1-based <paramref name="line"/> of <paramref name="side"/>'s pane stands in the walk's order: its row, then
    /// its pane, the left's first on one row, then the line itself.
    /// </summary>
    private static Key KeyOf(SideBySideDocument model, DiffSide side, int line) =>
        (RowOf(model, side, line), side == DiffSide.Left ? 0 : 1, line);

    /// <summary>
    /// The model's row holding 1-based <paramref name="line"/> of <paramref name="side"/>: -1 above the first line, and
    /// the row after the last for a line past the end of a model that lags an edit of the left.
    /// </summary>
    private static int RowOf(SideBySideDocument model, DiffSide side, int line)
    {
        IReadOnlyList<DiffLine> lines = model.Pane(side).Lines;
        int index = line - 1;
        return index < 0 ? -1 : index < lines.Count ? lines[index].Row : model.Rows.Count;
    }

    /// <summary>A section of the walk and the pane it goes to.</summary>
    private readonly record struct Stop(DiffSide Side, MarkdownSection Section);
}
