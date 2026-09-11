using Avalonia.Headless.XUnit;
using Bennewitz.Ninja.DiffView.Core;

namespace Bennewitz.Ninja.DiffView.Avalonia.Tests.Composite;

/// <summary>
/// What a fold can and cannot put out of reach. Navigation and find both scroll by row through the
/// projection, but they aim at different kinds of row, and only one of those kinds can be folded.
/// </summary>
public sealed class FoldingReachTests
{
    /// <summary>
    /// A fold only ever covers <see cref="DiffLineKind.Unchanged"/> rows, and a change block has
    /// none — so navigation cannot aim into one. This is asserted rather than assumed, because
    /// "F7 into a folded run" is a sentence that sounds like it describes something.
    /// </summary>
    [AvaloniaFact]
    public async Task No_fold_can_hide_a_change_so_navigation_is_never_aimed_into_one()
    {
        using CompositeHost host = new();
        host.Show();
        (string left, string right) = FoldingFixture.Pair();
        await host.LoadAsync(new PaneSource(left), new PaneSource(right));

        RowProjection projection = host.View.ApplyFolds(contextRows: 0);
        CompositeHost.Layout();
        SideBySideDocument document = host.View.Document ?? throw new InvalidOperationException("No model.");
        Assert.True(projection.FoldCount > 0);

        foreach (ChangeBlock block in document.Blocks)
        {
            for (int row = block.FirstRow; row <= block.LastRow; row++)
            {
                Assert.False(projection.IsHidden(row), $"block {block.Index} row {row} is behind a placeholder");
            }
        }

        // And walking the changes lands on each block with its first row on screen.
        for (int change = 0; change < document.Blocks.Count; change++)
        {
            host.View.CurrentChangeIndex = change;
            CompositeHost.Layout();

            ChangeBlock block = document.Blocks[change];
            double lineHeight = host.Left.TextArea.TextView.DefaultLineHeight;
            double top = projection.VisibleRowOf(block.FirstRow) * lineHeight;
            double offset = host.Left.VerticalOffset;
            Assert.InRange(top, offset, offset + host.Left.ViewportHeight);
        }
    }

    /// <summary>
    /// A find match is a different matter: it can be on an unchanged row, which is exactly what a
    /// fold covers. Walking to one opens the run hiding it, so the count stays the document's and
    /// every match it reports is a match the reader can see — settled 2026-09-11 over excluding
    /// folded matches, because a count that changes when you fold describes the view rather than
    /// the file.
    /// </summary>
    [AvaloniaFact]
    public async Task Walking_to_a_match_inside_a_folded_run_opens_it()
    {
        using CompositeHost host = new();
        host.Show();
        (string left, string right) = FoldingFixture.Pair();
        await host.LoadAsync(new PaneSource(left), new PaneSource(right));

        RowProjection folded = host.View.ApplyFolds(contextRows: 0);
        CompositeHost.Layout();
        int foldsBefore = folded.FoldCount;
        Assert.True(foldsBefore > 1);

        // "same" is on every unchanged row, so most matches start behind a placeholder.
        host.View.OpenFind();
        await host.FindAsync("same");
        SideBySideDocument document = host.View.Document ?? throw new InvalidOperationException("No model.");
        FindResult result = host.View.FindResult ?? throw new InvalidOperationException("No find result.");
        Assert.NotEmpty(result.Matches);

        int RowOf(FindMatch match) => document.Pane(match.Side).Lines[match.Line].Row;
        Assert.Contains(result.Matches, match => folded.IsHidden(RowOf(match)));

        // The count is the model's, and stays so.
        Assert.Equal(result.Matches.Count, result.LeftCount + result.RightCount);

        // Walk to the first match that was hidden: its run opens, and it is on screen.
        int index = result.Matches.ToList().FindIndex(match => folded.IsHidden(RowOf(match)));
        host.View.CurrentFindMatchIndex = index;
        CompositeHost.Layout();

        RowProjection after = host.View.ApplyFolds(contextRows: 0);
        int row = RowOf(result.Matches[index]);
        Assert.False(after.IsHidden(row), "the match's row should be on screen once walked to");
        Assert.Equal(foldsBefore - 1, after.FoldCount);

        // Only the run holding it opened; the rest are untouched.
        Assert.Equal(after.FoldCount, host.Left.CollapsedSectionCount);
        Assert.Equal(host.Left.CollapsedSectionCount, host.Right.CollapsedSectionCount);
    }
}
