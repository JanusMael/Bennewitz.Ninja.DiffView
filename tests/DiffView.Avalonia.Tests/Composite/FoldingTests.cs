using System.Text;
using Avalonia.Headless.XUnit;
using AvaloniaEdit.Rendering;
using Bennewitz.Ninja.DiffView.Core;

namespace Bennewitz.Ninja.DiffView.Tests.Composite;

/// <summary>
/// Plan 00013 phase 2: the runs, the three cuts, and the two line ranges each fold collapses.
/// No placeholder and no option yet — what is asserted here is that a fold takes the same height
/// out of both panes and leaves every surviving pair sharing a row top.
/// </summary>
public sealed class FoldingTests
{
    private const double Tolerance = 1e-6;

    private static (string Left, string Right) Fixture()
    {
        return FoldingFixture.Pair();
    }

    private static SideBySideDocument Document()
    {
        (string left, string right) = Fixture();
        return DiffDocumentBuilder.Build(left, right).Document;
    }

    [AvaloniaFact]
    public async Task Folding_the_unchanged_runs_takes_the_same_height_out_of_both_panes()
    {
        using CompositeHost host = new();
        host.Show();
        (string left, string right) = Fixture();
        await host.LoadAsync(new PaneSource(left), new PaneSource(right));

        TextView leftView = host.Left.TextArea.TextView;
        TextView rightView = host.Right.TextArea.TextView;
        double lineHeight = leftView.DefaultLineHeight;
        double before = host.Left.ExtentHeight;
        Assert.Equal(before, host.Right.ExtentHeight, Tolerance);

        RowProjection projection = host.View.ApplyFolds(contextRows: 0);
        CompositeHost.Layout();

        Assert.True(projection.FoldCount > 1, $"the fixture should fold more than one run, folded {projection.FoldCount}");
        Assert.True(projection.VisibleRowCount < projection.ModelRowCount);

        int hidden = projection.ModelRowCount - projection.VisibleRowCount;
        Assert.Equal(before - (hidden * lineHeight), host.Left.ExtentHeight, Tolerance);
        Assert.Equal(host.Left.ExtentHeight, host.Right.ExtentHeight, Tolerance);
        Assert.Equal(host.Left.CollapsedSectionCount, host.Right.CollapsedSectionCount);
        Assert.Equal(projection.FoldCount, host.Left.CollapsedSectionCount);
    }

    [AvaloniaFact]
    public async Task Every_surviving_pair_still_shares_a_row_top()
    {
        using CompositeHost host = new();
        host.Show();
        (string left, string right) = Fixture();
        await host.LoadAsync(new PaneSource(left), new PaneSource(right));

        SideBySideDocument document = host.View.Document ?? throw new InvalidOperationException("No model.");
        AssertRowsAligned(host, document, RowProjection.Identity(document.Rows.Count));

        RowProjection projection = host.View.ApplyFolds(contextRows: 0);
        CompositeHost.Layout();
        AssertRowsAligned(host, document, projection);

        // And unfolding puts every row back where it started.
        double folded = host.Left.ExtentHeight;
        RowProjection none = host.View.ApplyFolds(contextRows: null);
        CompositeHost.Layout();
        Assert.True(none.IsIdentity);
        Assert.NotEqual(folded, host.Left.ExtentHeight);
        Assert.Equal(host.Left.ExtentHeight, host.Right.ExtentHeight, Tolerance);
        AssertRowsAligned(host, document, none);
    }

    /// <summary>
    /// A fold shifted by one line collapses the same <i>number</i> of lines on both sides, so the
    /// extents still match and every surviving pair still shares a row top — two panes wrong in
    /// the same direction agree with each other. What catches it is which lines are left standing:
    /// the run's own first line carries the placeholder and keeps its height, and the line after
    /// it is the first with none.
    /// </summary>
    [AvaloniaFact]
    public async Task A_fold_leaves_its_first_line_standing_and_takes_every_line_after_it()
    {
        using CompositeHost host = new();
        host.Show();
        (string left, string right) = Fixture();
        await host.LoadAsync(new PaneSource(left), new PaneSource(right));

        SideBySideDocument document = host.View.Document ?? throw new InvalidOperationException("No model.");
        RowProjection projection = host.View.ApplyFolds(contextRows: 0);
        CompositeHost.Layout();
        Assert.True(projection.FoldCount > 0);

        double lineHeight = host.Left.TextArea.TextView.DefaultLineHeight;
        for (int fold = 0; fold < projection.FoldCount; fold++)
        {
            FoldedRun run = projection.FoldAt(fold);
            foreach ((DiffPanePresenter pane, DiffSide side) in (( DiffPanePresenter, DiffSide)[])[(host.Left, DiffSide.Left), (host.Right, DiffSide.Right)])
            {
                (int First, int Last) lines = FoldPlan.LinesOf(document, run, side)
                                              ?? throw new InvalidOperationException("A taken fold hides nothing.");
                double placeholder = Top(pane, lines.First - 1);
                double firstHidden = Top(pane, lines.First);
                double afterFold = Top(pane, lines.Last + 1);

                // The placeholder's line is on screen, so it is one row tall.
                Assert.Equal(lineHeight, firstHidden - placeholder, Tolerance);

                // And everything from there to the end of the run has no height at all.
                Assert.Equal(firstHidden, afterFold, Tolerance);
            }
        }
    }

    [AvaloniaFact]
    public void A_fold_never_starts_on_a_line_carrying_padding_for_the_rows_above_it()
    {
        SideBySideDocument document = Document();
        IReadOnlyList<FoldedRun> folds = FoldPlan.For(document, contextRows: 0);

        Assert.NotEmpty(folds);
        bool anyRuleFired = false;
        foreach (FoldedRun fold in folds)
        {
            if (fold.FirstRow == 0)
            {
                continue;
            }

            AlignedRow above = document.Rows[fold.FirstRow - 1];
            Assert.NotNull(above.LeftLine);
            Assert.NotNull(above.RightLine);

            // The rule fires where the row above the run's own start is one-sided, which is every
            // run in this fixture that follows an insertion or a deletion.
            anyRuleFired |= document.Rows[fold.FirstRow - 1].Kind == DiffLineKind.Unchanged
                            && document.Rows[fold.FirstRow - 2].LeftLine is null;
        }

        Assert.True(anyRuleFired, "the fixture should make the rule fire at least once");
    }

    [AvaloniaFact]
    public void A_fold_never_ends_on_a_sides_last_line_while_that_side_has_a_trailing_gap()
    {
        SideBySideDocument document = Document();
        IReadOnlyList<FoldedRun> folds = FoldPlan.For(document, contextRows: 0);
        int lastRow = document.Rows.Count - 1;
        int lastWithRight = LastRowWithLine(document, DiffSide.Right);

        Assert.True(lastWithRight < lastRow, "the fixture should end with rows the right side pads");

        FoldedRun last = folds[^1];
        Assert.True(
            last.EndRow - 1 < lastWithRight,
            $"the last fold ends at row {last.EndRow - 1}, on or past the right's last line at {lastWithRight}");
    }

    [AvaloniaFact]
    public void A_run_shorter_than_the_floor_is_left_alone()
    {
        SideBySideDocument document = Document();

        // Every run in the fixture is twenty rows, so a floor above that folds nothing at all.
        Assert.Empty(FoldPlan.For(document, contextRows: 0, minimumFoldedRows: 40));
        Assert.NotEmpty(FoldPlan.For(document, contextRows: 0, minimumFoldedRows: 4));

        // And context eats into the same budget: nine rows either side of a twenty-row run leaves
        // two, which is under the floor.
        Assert.Empty(FoldPlan.For(document, contextRows: 9));
    }

    [AvaloniaFact]
    public void Context_shrinks_every_run_from_both_ends()
    {
        SideBySideDocument document = Document();
        IReadOnlyList<FoldedRun> none = FoldPlan.For(document, contextRows: 0);
        IReadOnlyList<FoldedRun> three = FoldPlan.For(document, contextRows: 3);

        Assert.Equal(none.Count, three.Count);
        for (int i = 0; i < none.Count; i++)
        {
            // Six rows fewer, less whatever the boundary rule had already taken off the start:
            // with context the rule cannot fire, so a run that lost a row to it loses one less.
            Assert.True(three[i].RowCount <= none[i].RowCount - 5, $"fold {i} went from {none[i].RowCount} to {three[i].RowCount}");
            Assert.True(three[i].FirstRow >= none[i].FirstRow);
            Assert.True(three[i].EndRow <= none[i].EndRow);
        }
    }

    /// <param name="line">An AvaloniaEdit 1-based document line.</param>
    private static double Top(DiffPanePresenter pane, int line)
    {
        return pane.TextArea.TextView.GetVisualTopByDocumentLine(line);
    }

    private static int LastRowWithLine(SideBySideDocument document, DiffSide side)
    {
        for (int row = document.Rows.Count - 1; row >= 0; row--)
        {
            if (document.Rows[row].LineOf(side) is not null)
            {
                return row;
            }
        }

        return -1;
    }

    /// <summary>
    /// Every pair of lines that share a row sits at the same document-relative top. A padded
    /// line's height-tree position is the top of its padding block, so its row top is that
    /// position plus the rows of padding above it. Rows behind a placeholder have no row top.
    /// </summary>
    private static void AssertRowsAligned(CompositeHost host, SideBySideDocument document, RowProjection projection)
    {
        double lineHeight = host.Left.TextArea.TextView.DefaultLineHeight;
        for (int row = 0; row < document.Rows.Count; row++)
        {
            AlignedRow aligned = document.Rows[row];
            if (projection.IsHidden(row) || aligned.LeftLine is not { } left || aligned.RightLine is not { } right)
            {
                continue;
            }

            double leftTop = RowTop(host.Left, document, DiffSide.Left, left, lineHeight);
            double rightTop = RowTop(host.Right, document, DiffSide.Right, right, lineHeight);
            Assert.Equal(leftTop, rightTop, Tolerance);
        }
    }

    /// <param name="line">The model's own 0-based line index; AvaloniaEdit numbers it one higher.</param>
    private static double RowTop(DiffPanePresenter pane, SideBySideDocument document, DiffSide side, int line, double lineHeight)
    {
        int padding = Padding.Before(document, side, line);
        return pane.TextArea.TextView.GetVisualTopByDocumentLine(line + 1) + (padding * lineHeight);
    }
}
