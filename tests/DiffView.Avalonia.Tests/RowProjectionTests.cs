namespace Bennewitz.Ninja.DiffView.Tests;

/// <summary>
/// Plan 00013 phase 1. The projection is pure arithmetic over row indices, so it is tested
/// without a window.
/// </summary>
/// <remarks>
/// Every fixture here holds <b>two</b> folds. One fold cannot tell a running total of hidden rows
/// from the fold's own count, because the first fold's running total is zero — an assertion that
/// passes against a single fold is an assertion about nothing.
/// </remarks>
public sealed class RowProjectionTests
{
    private const int Rows = 20;

    /// <summary>
    /// Rows 3–6 behind a placeholder at 3, and rows 12–16 behind one at 12: seven rows hidden of
    /// twenty, so the visible sequence is 0 1 2 [3] 7 8 9 10 11 [12] 17 18 19.
    /// </summary>
    private static readonly int[] VisibleSequence = [0, 1, 2, 3, 7, 8, 9, 10, 11, 12, 17, 18, 19];

    private static RowProjection TwoFolds()
    {
        return RowProjection.Of(Rows, [new FoldedRun(3, 4), new FoldedRun(12, 5)]);
    }

    [Fact]
    public void Nothing_folded_maps_every_row_to_itself()
    {
        RowProjection projection = RowProjection.Identity(Rows);

        Assert.True(projection.IsIdentity);
        Assert.Equal(Rows, projection.ModelRowCount);
        Assert.Equal(Rows, projection.VisibleRowCount);
        for (int row = 0; row < Rows; row++)
        {
            Assert.Equal(row, projection.VisibleRowOf(row));
            Assert.Equal(row, projection.ModelRowOf(row));
            Assert.False(projection.IsHidden(row));
            Assert.False(projection.IsPlaceholder(row));
        }
    }

    [Fact]
    public void A_fold_takes_all_but_one_of_its_rows_off_the_height()
    {
        RowProjection projection = TwoFolds();

        Assert.False(projection.IsIdentity);
        Assert.Equal(Rows, projection.ModelRowCount);
        Assert.Equal(VisibleSequence.Length, projection.VisibleRowCount);
        Assert.Equal(Rows - 3 - 4, projection.VisibleRowCount);
    }

    [Fact]
    public void A_row_below_two_folds_moves_up_by_what_both_of_them_hide()
    {
        RowProjection projection = TwoFolds();

        // Above the first fold, nothing has moved.
        Assert.Equal(2, projection.VisibleRowOf(2));

        // Between them, only the first fold's three hidden rows have.
        Assert.Equal(4, projection.VisibleRowOf(7));
        Assert.Equal(8, projection.VisibleRowOf(11));

        // Below both, both folds have. Reading the second fold's count without the running total
        // of the first gives 13 here, and reading the first fold's count gives 11.
        Assert.Equal(10, projection.VisibleRowOf(17));
        Assert.Equal(12, projection.VisibleRowOf(19));
    }

    [Fact]
    public void A_row_inside_a_fold_reports_the_placeholder_it_is_behind()
    {
        RowProjection projection = TwoFolds();

        foreach (int row in (int[])[3, 4, 5, 6])
        {
            Assert.Equal(3, projection.VisibleRowOf(row));
        }

        foreach (int row in (int[])[12, 13, 14, 15, 16])
        {
            Assert.Equal(9, projection.VisibleRowOf(row));
        }

        // The run's first row is the placeholder and is on screen; the rest are not.
        Assert.True(projection.IsPlaceholder(3));
        Assert.True(projection.IsPlaceholder(12));
        Assert.False(projection.IsHidden(3));
        Assert.False(projection.IsHidden(12));
        foreach (int row in (int[])[4, 5, 6, 13, 14, 15, 16])
        {
            Assert.True(projection.IsHidden(row));
            Assert.False(projection.IsPlaceholder(row));
        }

        foreach (int row in (int[])[0, 2, 7, 11, 17, 19])
        {
            Assert.False(projection.IsHidden(row));
            Assert.False(projection.IsPlaceholder(row));
        }
    }

    [Fact]
    public void The_two_directions_invert_each_other_on_every_visible_row()
    {
        RowProjection projection = TwoFolds();

        Assert.Equal(VisibleSequence.Length, projection.VisibleRowCount);
        for (int visible = 0; visible < projection.VisibleRowCount; visible++)
        {
            int model = projection.ModelRowOf(visible);
            Assert.Equal(VisibleSequence[visible], model);
            Assert.Equal(visible, projection.VisibleRowOf(model));
        }
    }

    [Fact]
    public void A_fold_that_would_not_invert_is_dropped_rather_than_taken()
    {
        // A run of one hides nothing and would be a placeholder standing for the row it replaces.
        Assert.True(RowProjection.Of(Rows, [new FoldedRun(4, 1)]).IsIdentity);

        // A run reaching past the last row cannot be walked back from a visible row.
        Assert.True(RowProjection.Of(Rows, [new FoldedRun(18, 5)]).IsIdentity);
        Assert.True(RowProjection.Of(Rows, [new FoldedRun(-2, 4)]).IsIdentity);

        // Two runs over the same rows would hide a row twice. The first in row order is taken and
        // the overlap is dropped, so what survives still inverts.
        RowProjection overlapping = RowProjection.Of(Rows, [new FoldedRun(3, 4), new FoldedRun(5, 4)]);
        Assert.Equal(1, overlapping.FoldCount);
        Assert.Equal(new FoldedRun(3, 4), overlapping.FoldAt(0));
        Assert.Equal(Rows - 3, overlapping.VisibleRowCount);
    }

    [Fact]
    public void The_folds_are_ordered_by_row_however_they_arrive()
    {
        RowProjection projection = RowProjection.Of(Rows, [new FoldedRun(12, 5), new FoldedRun(3, 4)]);

        Assert.Equal(2, projection.FoldCount);
        Assert.Equal(new FoldedRun(3, 4), projection.FoldAt(0));
        Assert.Equal(new FoldedRun(12, 5), projection.FoldAt(1));
        Assert.Equal(10, projection.VisibleRowOf(17));
    }
}
