using Avalonia.Headless.XUnit;
using AvaloniaEdit;
using AvaloniaEdit.Rendering;
using Bennewitz.Ninja.DiffView.Core;

namespace Bennewitz.Ninja.DiffView.Tests.Spike;

/// <summary>
/// The folding feasibility spike: Beyond Compare's <i>Show Differences / Show Same / Show
/// Context</i>, asked for on 2026-09-11 and to be answered before any plan 00013 is written. It
/// runs against the same two plain <see cref="TextEditor"/>s and the same padding mechanism as
/// <see cref="VirtualPaddingSpikeTests"/>, because the question is not whether AvaloniaEdit can
/// fold — it ships a whole folding stack — but whether a fold can keep two padded panes
/// row-aligned, which is the constraint that forced word wrap off.
///
/// Each test is one item. All of it is throwaway; the go / no-go it produces is recorded in
/// <c>DECISIONS.md</c>, as the virtual-padding spike's was.
/// </summary>
public sealed class FoldingSpikeTests
{
    private const double Tolerance = 1e-6;

    /// <summary>
    /// The same 99-row fixture the padding spike uses. Left: 94 lines, 3 rows of padding above
    /// line 31 and 2 above line 76. Right: 90 lines, 5 rows above line 54 and 4 trailing rows
    /// after line 90.
    /// </summary>
    private static AlignmentFixture SmallFixture()
    {
        return AlignmentFixture.Build(
            new Same(30), new RightOnly(3), new Same(20), new LeftOnly(5),
            new Same(20), new RightOnly(2), new Same(15), new LeftOnly(4));
    }

    /// <summary>
    /// Item 1. A fold over rows that exist on both sides and carry no padding takes the same
    /// number of rows out of each pane: the extents stay equal, every surviving aligned pair
    /// still shares a row top, and the pairs below the fold have moved up by exactly the fold.
    /// This is <i>Show Differences</i> — hiding what is the same — and it is the case that works.
    /// </summary>
    [AvaloniaFact]
    public void Item1_a_symmetric_fold_of_an_unchanged_run_keeps_both_panes_aligned()
    {
        using SpikeHost host = new(SmallFixture());
        host.Show();
        host.Prime(host.Left);
        host.Prime(host.Right);

        TextView leftView = host.Left.TextArea.TextView;
        TextView rightView = host.Right.TextArea.TextView;
        double lineHeight = leftView.DefaultLineHeight;
        int rows = host.Fixture.RowCount;
        Assert.Equal(rows * lineHeight, leftView.DocumentHeight, Tolerance);
        Assert.Equal(rows * lineHeight, rightView.DocumentHeight, Tolerance);
        Assert.Equal(rows * lineHeight, host.Left.ExtentHeight, Tolerance);
        Assert.Equal(rows * lineHeight, host.Right.ExtentHeight, Tolerance);
        AssertRowsAligned(host);

        IReadOnlyList<(int Left, int Right)> run = MidDocumentUnpaddedRun(host.Fixture);
        (int left, int right) below = FirstUnpaddedPairAfter(host.Fixture, run[^1]);
        (int left, int right) above = LastUnpaddedPairBefore(host.Fixture, run[0]);
        double leftBelowBefore = RowTopOfLine(host, host.Left, below.left);
        double rightBelowBefore = RowTopOfLine(host, host.Right, below.right);
        double leftAboveBefore = RowTopOfLine(host, host.Left, above.left);

        Collapse(host.Left, run[0].Left, run[^1].Left);
        Collapse(host.Right, run[0].Right, run[^1].Right);
        Settle(host);

        // Both panes lost exactly the folded rows, so the extents are still equal.
        Assert.Equal((rows - run.Count) * lineHeight, leftView.DocumentHeight, Tolerance);
        Assert.Equal((rows - run.Count) * lineHeight, rightView.DocumentHeight, Tolerance);
        Assert.Equal((rows - run.Count) * lineHeight, host.Left.ExtentHeight, Tolerance);
        Assert.Equal(host.Left.ExtentHeight, host.Right.ExtentHeight, Tolerance);

        // The rows that survive still line up. A row above the fold has not moved; a row below
        // it has moved up by the fold, on both sides — which is what makes the two panes agree
        // rather than merely being the same height.
        AssertRowsAligned(host, skip: run);
        Assert.Equal(leftAboveBefore, RowTopOfLine(host, host.Left, above.left), Tolerance);
        Assert.Equal(leftBelowBefore - run.Count * lineHeight, RowTopOfLine(host, host.Left, below.left), Tolerance);
        Assert.Equal(rightBelowBefore - run.Count * lineHeight, RowTopOfLine(host, host.Right, below.right), Tolerance);
    }

    /// <summary>
    /// Item 2. Padding is height on a line, not lines of its own, so a fold that starts at a
    /// padded line swallows padding standing in for rows <i>above</i> the fold — rows the other
    /// side keeps. The panes then differ by exactly that padding. A fold's boundaries are
    /// therefore a property of the aligned model, not of either document.
    /// </summary>
    [AvaloniaFact]
    public void Item2_a_fold_that_starts_at_a_padded_line_swallows_padding_belonging_to_the_rows_above_it()
    {
        using SpikeHost host = new(SmallFixture());
        host.Show();
        host.Prime(host.Left);
        host.Prime(host.Right);

        TextView leftView = host.Left.TextArea.TextView;
        TextView rightView = host.Right.TextArea.TextView;
        double lineHeight = leftView.DefaultLineHeight;

        // The pair whose left line carries the padding for the RightOnly rows above it, and the
        // twenty aligned rows that start there.
        (int Left, int Right) start = PaddedPair(host.Fixture, DiffSide.Left);
        int orphaned = host.Fixture.LeftPadding[start.Left].Above;
        Assert.Equal(3, orphaned);
        IReadOnlyList<(int Left, int Right)> run = RunFrom(host.Fixture, start, length: 20);

        Collapse(host.Left, run[0].Left, run[^1].Left);
        Collapse(host.Right, run[0].Right, run[^1].Right);
        Settle(host);

        // The right lost the run; the left lost the run and the orphaned padding with it.
        Assert.Equal(-orphaned * lineHeight, leftView.DocumentHeight - rightView.DocumentHeight, Tolerance);
        Assert.Equal(-orphaned * lineHeight, host.Left.ExtentHeight - host.Right.ExtentHeight, Tolerance);
    }

    /// <summary>
    /// Item 3. <i>Show Same</i> — hiding what differs — is the harder direction, and not for a
    /// reason folding can answer on its own: a change block that is lines on one side is
    /// <i>padding</i> on the other, and padding has no line to collapse. Collapsing the side that
    /// has lines moves that pane alone.
    /// </summary>
    [AvaloniaFact]
    public void Item3_a_change_block_has_no_line_to_collapse_on_the_padded_side()
    {
        using SpikeHost host = new(SmallFixture());
        host.Show();
        host.Prime(host.Left);
        host.Prime(host.Right);

        TextView leftView = host.Left.TextArea.TextView;
        TextView rightView = host.Right.TextArea.TextView;
        double lineHeight = leftView.DefaultLineHeight;
        double before = leftView.DocumentHeight;
        Assert.Equal(before, rightView.DocumentHeight, Tolerance);

        // The LeftOnly(5) run: left lines that no right line shares a row with. The right's five
        // rows are the padding above the pair that follows, so there is nothing there to collapse.
        (int Left, int Right) after = PaddedPair(host.Fixture, DiffSide.Right);
        int leftOnly = host.Fixture.RightPadding[after.Right].Above;
        Assert.Equal(5, leftOnly);

        Collapse(host.Left, after.Left - leftOnly, after.Left - 1);
        Settle(host);

        Assert.Equal(before - leftOnly * lineHeight, leftView.DocumentHeight, Tolerance);
        Assert.Equal(before, rightView.DocumentHeight, Tolerance);
        Assert.Equal(-leftOnly * lineHeight, host.Left.ExtentHeight - host.Right.ExtentHeight, Tolerance);

        // Equal heights are not alignment, and this is the case that tells them apart: every
        // pair below the block is now out by the block, which is what Item 1's row-top check
        // would have caught had a symmetric fold not been symmetric.
        (int left, int right) pair = FirstUnpaddedPairAfter(host.Fixture, after);
        Assert.Equal(
            RowTopOfLine(host, host.Right, pair.right) - leftOnly * lineHeight,
            RowTopOfLine(host, host.Left, pair.left),
            Tolerance);
    }

    /// <summary>
    /// Item 4. The equation the composite's out-of-pane surfaces are built on — a row is one line
    /// height, so a row index is a pixel offset divided by one — holds before a fold and does not
    /// survive one. In-pane drawing is unaffected: it walks <see cref="TextView.VisualLines"/> and
    /// reads <see cref="VisualLine.VisualTop"/>, both of which the height tree already answers
    /// under a fold.
    /// </summary>
    [AvaloniaFact]
    public void Item4_rows_and_pixels_stop_sharing_one_scale_once_anything_is_folded()
    {
        using SpikeHost host = new(SmallFixture());
        host.Show();
        host.Prime(host.Left);
        host.Prime(host.Right);

        TextView leftView = host.Left.TextArea.TextView;
        double lineHeight = leftView.DefaultLineHeight;
        int rows = host.Fixture.RowCount;

        IReadOnlyList<(int Left, int Right)> run = MidDocumentUnpaddedRun(host.Fixture);
        (int left, int right) below = FirstUnpaddedPairAfter(host.Fixture, run[^1]);

        // Before the fold the two scales are one: a row index is a top divided by the line
        // height, which is what SideBySideDiffView computes for the connector and the map.
        Assert.Equal(rows * lineHeight, leftView.DocumentHeight, Tolerance);
        int modelRow = (int)Math.Round(RowTopOfLine(host, host.Left, below.left) / lineHeight);

        Collapse(host.Left, run[0].Left, run[^1].Left);
        Collapse(host.Right, run[0].Right, run[^1].Right);
        Settle(host);

        // After it, that division answers the visible row, and the model row is no longer
        // reachable from a pixel without asking what is folded.
        Assert.NotEqual(rows * lineHeight, leftView.DocumentHeight);
        int visibleRow = (int)Math.Round(RowTopOfLine(host, host.Left, below.left) / lineHeight);
        Assert.NotEqual(modelRow, visibleRow);
        Assert.Equal(modelRow - run.Count, visibleRow);
    }

    /// <summary>
    /// Item 5. <c>FoldingManager.Install</c> adds a <c>FoldingMargin</c> to the text area's left
    /// margins and inserts its element generator at index 0 — "folding only works correctly when
    /// it has highest priority", which is a claim on the slot the padding generator occupies. The
    /// collapsing itself is a text-view primitive that needs none of that, so the panes keep their
    /// own margins and their own generator order, and it is reversible.
    /// </summary>
    [AvaloniaFact]
    public void Item5_collapsing_is_a_text_view_primitive_that_needs_no_folding_manager()
    {
        using SpikeHost host = new(SmallFixture());
        host.Show();
        host.Prime(host.Left);

        TextView leftView = host.Left.TextArea.TextView;
        Type folding = typeof(global::AvaloniaEdit.Folding.FoldingManager);
        int generators = leftView.ElementGenerators.Count;
        int margins = host.Left.TextArea.LeftMargins.Count;
        Assert.Null(leftView.GetService(folding));

        IReadOnlyList<(int Left, int Right)> run = MidDocumentUnpaddedRun(host.Fixture);
        double height = leftView.DocumentHeight;
        CollapsedLineSection section = Collapse(host.Left, run[0].Left, run[^1].Left);
        Settle(host);

        // The folded lines occupy no height at all: the line after the fold starts where the
        // fold's own first line does.
        Assert.Equal(SpikeHost.TopOfLine(host.Left, run[0].Left), SpikeHost.TopOfLine(host.Left, run[^1].Left + 1), Tolerance);
        Assert.Equal(height - run.Count * leftView.DefaultLineHeight, leftView.DocumentHeight, Tolerance);

        // And none of it went through the folding stack.
        Assert.Equal(generators, leftView.ElementGenerators.Count);
        Assert.Equal(margins, host.Left.TextArea.LeftMargins.Count);
        Assert.Null(leftView.GetService(folding));

        section.Uncollapse();
        Settle(host);
        Assert.Equal(height, leftView.DocumentHeight, Tolerance);
    }

    private static CollapsedLineSection Collapse(TextEditor editor, int firstLine, int lastLine)
    {
        TextView view = editor.TextArea.TextView;
        return view.CollapseLines(editor.Document.GetLineByNumber(firstLine), editor.Document.GetLineByNumber(lastLine));
    }

    /// <summary>
    /// Collapsing writes the height tree, which answers <c>DocumentHeight</c> at once; the
    /// visual lines and the published scroll extent are not the height tree, and a fold leaves
    /// both stale until a redraw and a measure pass — the same two steps
    /// <see cref="PaddingHeightPrimer"/> takes for the same reason.
    /// </summary>
    private static void Settle(SpikeHost host)
    {
        foreach (TextEditor editor in (TextEditor[])[host.Left, host.Right])
        {
            editor.TextArea.TextView.Redraw();
            editor.TextArea.TextView.InvalidateMeasure();
        }

        SpikeHost.Layout();
    }

    /// <summary>
    /// The second maximal run of consecutive aligned pairs whose lines carry no padding on either
    /// side — the second, so the fold it describes has aligned rows both above and below it. A
    /// fold at the top of the document cannot tell a row that moved from a row that never moved.
    /// </summary>
    private static IReadOnlyList<(int Left, int Right)> MidDocumentUnpaddedRun(AlignmentFixture fixture)
    {
        List<List<(int Left, int Right)>> blocks = [];
        List<(int Left, int Right)> current = [];

        foreach ((int left, int right) in fixture.AlignedPairs)
        {
            bool consecutive = current.Count > 0 && left == current[^1].Left + 1 && right == current[^1].Right + 1;
            bool padded = fixture.LeftPadding.ContainsKey(left) || fixture.RightPadding.ContainsKey(right);
            if (!consecutive || padded)
            {
                if (current.Count > 0)
                {
                    blocks.Add(current);
                    current = [];
                }
            }

            if (!padded)
            {
                current.Add((left, right));
            }
        }

        if (current.Count > 0)
        {
            blocks.Add(current);
        }

        Assert.True(blocks.Count > 1, "The fixture has more than one unpadded run of aligned rows.");
        return blocks[1];
    }

    /// <summary>The <paramref name="length"/> consecutive aligned pairs starting at <paramref name="start"/>.</summary>
    private static IReadOnlyList<(int Left, int Right)> RunFrom(AlignmentFixture fixture, (int Left, int Right) start, int length)
    {
        int index = fixture.AlignedPairs.ToList().IndexOf(start);
        Assert.InRange(index, 0, fixture.AlignedPairs.Count - length);
        return [.. fixture.AlignedPairs.Skip(index).Take(length)];
    }

    /// <summary>The first aligned pair whose line on <paramref name="side"/> carries padding above it.</summary>
    private static (int Left, int Right) PaddedPair(AlignmentFixture fixture, DiffSide side)
    {
        foreach ((int left, int right) in fixture.AlignedPairs)
        {
            PaddingSpec spec = side == DiffSide.Left
                ? fixture.LeftPadding.GetValueOrDefault(left)
                : fixture.RightPadding.GetValueOrDefault(right);
            if (spec.Above > 0)
            {
                return (left, right);
            }
        }

        throw new InvalidOperationException($"The fixture has no {side} line padded above an aligned row.");
    }

    private static (int left, int right) FirstUnpaddedPairAfter(AlignmentFixture fixture, (int Left, int Right) pair)
    {
        return UnpaddedPairs(fixture).First(candidate => candidate.Left > pair.Left);
    }

    private static (int left, int right) LastUnpaddedPairBefore(AlignmentFixture fixture, (int Left, int Right) pair)
    {
        return UnpaddedPairs(fixture).Last(candidate => candidate.Left < pair.Left);
    }

    private static IEnumerable<(int Left, int Right)> UnpaddedPairs(AlignmentFixture fixture)
    {
        return fixture.AlignedPairs.Where(pair =>
            !fixture.LeftPadding.ContainsKey(pair.Left) && !fixture.RightPadding.ContainsKey(pair.Right));
    }

    /// <summary>
    /// Every pair of lines that share a row sits at the same document-relative top. Pairs inside
    /// a fold are skipped: a collapsed row has no row top to compare.
    /// </summary>
    private static void AssertRowsAligned(SpikeHost host, IReadOnlyList<(int Left, int Right)>? skip = null)
    {
        foreach ((int left, int right) in host.Fixture.AlignedPairs)
        {
            if (skip is not null && skip.Any(pair => pair.Left == left))
            {
                continue;
            }

            Assert.Equal(RowTopOfLine(host, host.Left, left), RowTopOfLine(host, host.Right, right), Tolerance);
        }
    }

    /// <summary>
    /// A padded line's height-tree position is the top of its padding block, so its row top is
    /// that position plus the rows of padding above it.
    /// </summary>
    private static double RowTopOfLine(SpikeHost host, TextEditor editor, int lineNumber)
    {
        double lineHeight = editor.TextArea.TextView.DefaultLineHeight;
        return SpikeHost.TopOfLine(editor, lineNumber) + host.PaddingOf(editor).GetValueOrDefault(lineNumber).Above * lineHeight;
    }
}
