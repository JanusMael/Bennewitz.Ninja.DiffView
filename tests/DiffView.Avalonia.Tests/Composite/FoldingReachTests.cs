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
    /// fold covers. Today such a match is counted, ticked on the map and walked to — and the walk
    /// lands on the placeholder standing in for it, because that is where the row is drawn.
    /// </summary>
    /// <remarks>
    /// This records the gap rather than the intent. Plan 00013's testing table asked for a match
    /// inside a folded run to be "either revealed or excluded, never counted and unreachable", and
    /// it is currently the third thing. The fix is a decision, not an oversight.
    /// </remarks>
    [AvaloniaFact]
    public async Task A_find_match_inside_a_folded_run_is_still_counted_and_still_hidden()
    {
        using CompositeHost host = new();
        host.Show();
        (string left, string right) = FoldingFixture.Pair();
        await host.LoadAsync(new PaneSource(left), new PaneSource(right));

        RowProjection projection = host.View.ApplyFolds(contextRows: 0);
        CompositeHost.Layout();

        // "same" is on every unchanged row, so most matches are behind a placeholder.
        host.View.OpenFind();
        await host.FindAsync("same");
        SideBySideDocument document = host.View.Document ?? throw new InvalidOperationException("No model.");
        FindResult result = host.View.FindResult ?? throw new InvalidOperationException("No find result.");
        Assert.NotEmpty(result.Matches);

        int[] rows = [.. result.Matches
            .Select(match => document.Pane(match.Side).Lines[match.Line].Row)
            .Distinct()];

        int hidden = rows.Count(projection.IsHidden);
        Assert.True(hidden > 0, $"the fixture should put matches inside a fold; {rows.Length} rows, none hidden");

        // Counted all the same — the count is the model's, and the map ticks every one.
        Assert.Equal(result.Matches.Count, result.LeftCount + result.RightCount);
    }
}
