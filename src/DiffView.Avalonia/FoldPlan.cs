using Bennewitz.Ninja.DiffView.Core;

namespace Bennewitz.Ninja.DiffView;

/// <summary>
/// Which rows a fold takes, computed from the model alone. A fold is a <b>row</b> range: the two
/// line ranges it collapses are projections of it, and neither can be read off a document.
/// </summary>
internal static class FoldPlan
{
    /// <summary>
    /// Below this many rows a run is left alone. A placeholder costs a row, so hiding two to save
    /// one is a trade the reader loses.
    /// </summary>
    public const int DefaultMinimumFoldedRows = 4;

    /// <summary>
    /// The runs to fold in <paramref name="document"/>, in row order and disjoint.
    /// </summary>
    /// <param name="document">The model whose unchanged runs are the candidates.</param>
    /// <param name="contextRows">Rows kept either side of every change; zero hides every match.</param>
    /// <param name="minimumFoldedRows">The floor below which a run is not worth a placeholder.</param>
    public static IReadOnlyList<FoldedRun> For(SideBySideDocument document, int contextRows, int minimumFoldedRows = DefaultMinimumFoldedRows)
    {
        ArgumentNullException.ThrowIfNull(document);
        ArgumentOutOfRangeException.ThrowIfNegative(contextRows);

        IReadOnlyList<AlignedRow> rows = document.Rows;
        if (rows.Count == 0)
        {
            return [];
        }

        int lastWithLeft = LastRowWithLine(rows, DiffSide.Left);
        int lastWithRight = LastRowWithLine(rows, DiffSide.Right);

        List<FoldedRun> folds = [];
        int runStart = -1;
        for (int row = 0; row <= rows.Count; row++)
        {
            bool unchanged = row < rows.Count && IsFoldable(rows[row]);
            if (unchanged)
            {
                if (runStart < 0)
                {
                    runStart = row;
                }

                continue;
            }

            if (runStart >= 0)
            {
                Take(folds, rows, runStart, row - 1, contextRows, minimumFoldedRows, lastWithLeft, lastWithRight);
                runStart = -1;
            }
        }

        return folds;
    }

    /// <summary>
    /// The runs to fold in a unified document, in line order and disjoint. One pane, so there is
    /// no second side to keep in step and no padding to orphan: the boundary rule has nothing to
    /// say here, and a run is cut by context and by the floor alone.
    /// </summary>
    public static IReadOnlyList<FoldedRun> For(InlineDocument document, int contextRows, int minimumFoldedRows = DefaultMinimumFoldedRows)
    {
        ArgumentNullException.ThrowIfNull(document);
        ArgumentOutOfRangeException.ThrowIfNegative(contextRows);

        IReadOnlyList<InlineLine> lines = document.Lines;
        List<FoldedRun> folds = [];
        int runStart = -1;
        for (int line = 0; line <= lines.Count; line++)
        {
            bool unchanged = line < lines.Count && lines[line].Kind == DiffLineKind.Unchanged;
            if (unchanged)
            {
                if (runStart < 0)
                {
                    runStart = line;
                }

                continue;
            }

            if (runStart >= 0)
            {
                int first = runStart + contextRows;
                int last = line - 1 - contextRows;
                int count = last - first + 1;
                if (count >= minimumFoldedRows && count >= 2)
                {
                    folds.Add(new FoldedRun(first, count));
                }

                runStart = -1;
            }
        }

        return folds;
    }

    /// <summary>
    /// The inclusive line range <paramref name="fold"/> collapses in a unified document: the
    /// run's own lines less its first, which stays visible to carry the placeholder. In
    /// AvaloniaEdit's 1-based numbering, which the unified document's own indices are not.
    /// </summary>
    public static (int First, int Last)? LinesOf(InlineDocument document, FoldedRun fold)
    {
        ArgumentNullException.ThrowIfNull(document);
        int first = fold.FirstRow + 1;
        int last = fold.EndRow - 1;
        return first > last || first < 0 || last >= document.Lines.Count ? null : (first + 1, last + 1);
    }

    /// <summary>
    /// The inclusive line range <paramref name="fold"/> collapses on <paramref name="side"/>, or
    /// <c>null</c> where the run holds no row to hide. The run's first row stays visible and
    /// carries the placeholder, so what collapses begins one row in.
    /// </summary>
    /// <remarks>
    /// The range is in <b>AvaloniaEdit's</b> 1-based line numbers, which the model's own line
    /// indices are not: an <see cref="AlignedRow"/> holds a 0-based index into that side's lines,
    /// the same offset <c>PaneMetadata</c> undoes when it reads a document line's kind.
    /// </remarks>
    public static (int First, int Last)? LinesOf(SideBySideDocument document, FoldedRun fold, DiffSide side)
    {
        ArgumentNullException.ThrowIfNull(document);
        IReadOnlyList<AlignedRow> rows = document.Rows;
        int first = fold.FirstRow + 1;
        int last = fold.EndRow - 1;
        if (first > last || first < 0 || last >= rows.Count)
        {
            return null;
        }

        int? firstLine = rows[first].LineOf(side);
        int? lastLine = rows[last].LineOf(side);
        return firstLine is { } from && lastLine is { } to && to >= from ? (from + 1, to + 1) : null;
    }

    /// <summary>A row is a fold candidate when it matches on both sides, which means it has both lines.</summary>
    private static bool IsFoldable(AlignedRow row)
    {
        return row.Kind == DiffLineKind.Unchanged && row.LeftLine is not null && row.RightLine is not null;
    }

    private static int LastRowWithLine(IReadOnlyList<AlignedRow> rows, DiffSide side)
    {
        for (int row = rows.Count - 1; row >= 0; row--)
        {
            if (rows[row].LineOf(side) is not null)
            {
                return row;
            }
        }

        return -1;
    }

    /// <summary>
    /// Cuts one run down to what may fold and takes it, or takes nothing. Context first, then the
    /// boundary rule, then the floor.
    /// </summary>
    private static void Take(
        List<FoldedRun> folds,
        IReadOnlyList<AlignedRow> rows,
        int runStart,
        int runEnd,
        int contextRows,
        int minimumFoldedRows,
        int lastWithLeft,
        int lastWithRight)
    {
        int first = runStart + contextRows;
        int last = runEnd - contextRows;

        // The boundary rule: a row is foldable only if collapsing its lines removes that row's
        // height and nothing else. Padding is height on a line, so a line at a run's edge can be
        // carrying rows the fold does not cover — rows the other pane is still drawing.
        if (first > 0 && first <= last && !HasBothLines(rows, first - 1))
        {
            first++;
        }

        if (last < rows.Count - 1 && first <= last && (last >= lastWithLeft || last >= lastWithRight))
        {
            last--;
        }

        int count = last - first + 1;
        if (count >= minimumFoldedRows && count >= 2)
        {
            folds.Add(new FoldedRun(first, count));
        }
    }

    private static bool HasBothLines(IReadOnlyList<AlignedRow> rows, int row)
    {
        return rows[row].LeftLine is not null && rows[row].RightLine is not null;
    }
}
