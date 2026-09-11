namespace Bennewitz.Ninja.DiffView.Avalonia;

/// <summary>
/// A run of model rows hidden behind one placeholder. The run's first row stays visible and
/// carries the placeholder; <see cref="RowCount"/> − 1 rows behind it do not, which is what
/// <c>TextView.CollapseLines</c> can express — a line's height is all or nothing.
/// </summary>
/// <param name="FirstRow">The first model row of the run, which stays visible.</param>
/// <param name="RowCount">How many model rows the run spans, at least two.</param>
internal readonly record struct FoldedRun(int FirstRow, int RowCount)
{
    /// <summary>One past the last model row of the run.</summary>
    public int EndRow => FirstRow + RowCount;

    /// <summary>How many rows the run takes off the document's height.</summary>
    public int HiddenCount => RowCount - 1;
}

/// <summary>
/// The one place a model row becomes a visible row and back. Every row is one line height once
/// the padding is primed, so a visible row multiplied by that height is a pixel — but only a
/// <i>visible</i> row is, and before this type the two were the same number and the multiplication
/// was done wherever it was wanted.
/// </summary>
/// <remarks>
/// The unified view's rows are its own document's lines, which is the same arithmetic over a
/// different domain; nothing here knows the difference.
/// </remarks>
internal sealed class RowProjection
{
    private readonly int[] _foldFirst;
    private readonly int[] _foldCount;

    /// <summary>Rows hidden by every fold before this one.</summary>
    private readonly int[] _hiddenBefore;

    /// <summary>The visible row each fold's placeholder sits at, which inverts the mapping.</summary>
    private readonly int[] _visibleFirst;

    private RowProjection(int rowCount, int[] foldFirst, int[] foldCount, int[] hiddenBefore, int[] visibleFirst, int visibleRowCount)
    {
        ModelRowCount = rowCount;
        VisibleRowCount = visibleRowCount;
        _foldFirst = foldFirst;
        _foldCount = foldCount;
        _hiddenBefore = hiddenBefore;
        _visibleFirst = visibleFirst;
    }

    /// <summary>How many rows the model has.</summary>
    public int ModelRowCount { get; }

    /// <summary>How many rows are on screen: <see cref="ModelRowCount"/> less what the folds hide.</summary>
    public int VisibleRowCount { get; }

    /// <summary>Whether nothing is folded, so every row maps to itself.</summary>
    public bool IsIdentity => _foldFirst.Length == 0;

    /// <summary>A projection over <paramref name="rowCount"/> rows with nothing folded.</summary>
    public static RowProjection Identity(int rowCount)
    {
        int rows = Math.Max(0, rowCount);
        return new RowProjection(rows, [], [], [], [], rows);
    }

    /// <summary>
    /// A projection over <paramref name="rowCount"/> rows with <paramref name="folds"/> hidden.
    /// The folds are sorted and any that is degenerate, out of range or overlapping one already
    /// taken is dropped — a projection that cannot be inverted is worse than one that folds less.
    /// </summary>
    public static RowProjection Of(int rowCount, IEnumerable<FoldedRun> folds)
    {
        ArgumentNullException.ThrowIfNull(folds);
        int rows = Math.Max(0, rowCount);

        List<FoldedRun> taken = [];
        foreach (FoldedRun fold in folds.OrderBy(fold => fold.FirstRow))
        {
            bool usable = fold.RowCount >= 2
                          && fold.FirstRow >= 0
                          && fold.EndRow <= rows
                          && (taken.Count == 0 || fold.FirstRow >= taken[^1].EndRow);
            if (usable)
            {
                taken.Add(fold);
            }
        }

        if (taken.Count == 0)
        {
            return Identity(rows);
        }

        int[] foldFirst = new int[taken.Count];
        int[] foldCount = new int[taken.Count];
        int[] hiddenBefore = new int[taken.Count];
        int[] visibleFirst = new int[taken.Count];
        int hidden = 0;
        for (int i = 0; i < taken.Count; i++)
        {
            foldFirst[i] = taken[i].FirstRow;
            foldCount[i] = taken[i].RowCount;
            hiddenBefore[i] = hidden;
            visibleFirst[i] = taken[i].FirstRow - hidden;
            hidden += taken[i].HiddenCount;
        }

        return new RowProjection(rows, foldFirst, foldCount, hiddenBefore, visibleFirst, rows - hidden);
    }

    /// <summary>
    /// The visible row <paramref name="modelRow"/> is drawn at. A row inside a fold reports the
    /// fold's placeholder, because that is where a reader sees it — not a row of its own.
    /// </summary>
    public int VisibleRowOf(int modelRow)
    {
        if (IsIdentity)
        {
            return modelRow;
        }

        int fold = FoldAtOrBefore(_foldFirst, modelRow);
        if (fold < 0)
        {
            return modelRow;
        }

        return modelRow < _foldFirst[fold] + _foldCount[fold]
            ? _visibleFirst[fold]
            : modelRow - (_hiddenBefore[fold] + _foldCount[fold] - 1);
    }

    /// <summary>
    /// The model row at <paramref name="visibleRow"/> — <see cref="VisibleRowOf"/> read backwards.
    /// A placeholder answers the first row of the run it stands for.
    /// </summary>
    public int ModelRowOf(int visibleRow)
    {
        if (IsIdentity)
        {
            return visibleRow;
        }

        int fold = FoldAtOrBefore(_visibleFirst, visibleRow);
        if (fold < 0)
        {
            return visibleRow;
        }

        return visibleRow == _visibleFirst[fold]
            ? _foldFirst[fold]
            : visibleRow + _hiddenBefore[fold] + _foldCount[fold] - 1;
    }

    /// <summary>Whether <paramref name="modelRow"/> is behind a placeholder rather than on screen.</summary>
    public bool IsHidden(int modelRow)
    {
        if (IsIdentity)
        {
            return false;
        }

        int fold = FoldAtOrBefore(_foldFirst, modelRow);
        return fold >= 0 && modelRow > _foldFirst[fold] && modelRow < _foldFirst[fold] + _foldCount[fold];
    }

    /// <summary>
    /// The fold <paramref name="modelRow"/> belongs to, or <c>-1</c>. A fold's own first row
    /// counts: it is the placeholder's row, and revealing a run from it is what a reader clicking
    /// one asks for.
    /// </summary>
    public int FoldContaining(int modelRow)
    {
        if (IsIdentity)
        {
            return -1;
        }

        int fold = FoldAtOrBefore(_foldFirst, modelRow);
        return fold >= 0 && modelRow < _foldFirst[fold] + _foldCount[fold] ? fold : -1;
    }

    /// <summary>Whether a placeholder is drawn at <paramref name="modelRow"/>, which is a fold's first row.</summary>
    public bool IsPlaceholder(int modelRow)
    {
        return !IsIdentity && Array.BinarySearch(_foldFirst, modelRow) >= 0;
    }

    /// <summary>The fold at <paramref name="index"/>, in row order.</summary>
    public FoldedRun FoldAt(int index)
    {
        return new FoldedRun(_foldFirst[index], _foldCount[index]);
    }

    /// <summary>How many folds the projection holds.</summary>
    public int FoldCount => _foldFirst.Length;

    /// <summary>
    /// The last fold whose start is at or before <paramref name="row"/>, or −1. The starts are
    /// sorted and disjoint by construction, so a binary search answers it.
    /// </summary>
    private static int FoldAtOrBefore(int[] starts, int row)
    {
        int found = Array.BinarySearch(starts, row);
        return found >= 0 ? found : ~found - 1;
    }
}
