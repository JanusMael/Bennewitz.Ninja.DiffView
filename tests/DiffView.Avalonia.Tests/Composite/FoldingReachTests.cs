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
    /// The menu's expand entry says "here", so it means the run under the pointer — plan 00010's
    /// rule, the one the copy entries and plan 00012's block verbs already follow. A gesture means
    /// the caret's run, which is the only run a keyboard can name. Found by driving the demo: the
    /// entry was enabled from the caret while the pointer was somewhere else entirely.
    /// </summary>
    [AvaloniaFact]
    public async Task The_menus_expand_entry_is_the_run_under_the_pointer_not_the_one_at_the_caret()
    {
        using CompositeHost host = new();
        host.Show();
        (string left, string right) = FoldingFixture.Pair();
        await host.LoadAsync(new PaneSource(left), new PaneSource(right));

        SideBySideDocument document = host.View.Document ?? throw new InvalidOperationException("No model.");
        RowProjection projection = host.View.ApplyFolds(contextRows: 0);
        CompositeHost.Layout();
        Assert.True(projection.FoldCount > 1);

        FoldedRun run = projection.FoldAt(1);
        ChangeBlock block = document.Blocks[0];

        // The caret goes inside a change block, which no fold can cover, so the view's own verb
        // is unavailable. Line 1 would not do: it is the first run's placeholder.
        host.Left.TextArea.Caret.Line = block.LeftLines.Start + 1;
        CompositeHost.Layout();
        Assert.False(host.View.CommandFor(DiffCommand.ExpandFold).CanExecute(null));

        // The menu built over a row inside the second fold offers it all the same, and opens
        // that run rather than the caret's.
        DiffMenuItem entry = ExpandEntry(host, run.FirstRow + 1);
        Assert.True(entry.IsEnabled);
        entry.Command!.Execute(null);
        CompositeHost.Layout();

        RowProjection after = host.View.ApplyFolds(contextRows: 0);
        Assert.Equal(projection.FoldCount - 1, after.FoldCount);
        Assert.False(after.IsHidden(run.FirstRow + 1));

        // And over a row in no fold at all it is present and disabled, not absent: the menu's
        // shape does not change with the state.
        DiffMenuItem outside = ExpandEntry(host, block.FirstRow);
        Assert.False(outside.IsEnabled);
    }

    private static DiffMenuItem ExpandEntry(CompositeHost host, int row)
    {
        DiffPaneContext context = new(
            DiffPaneRegion.Text,
            DiffSide.Left,
            LineNumber: 1,
            SourceSide: DiffSide.Left,
            SourceLine: 1,
            Row: row,
            Block: null,
            Kind: DiffLineKind.Unchanged,
            SelectedLines: null,
            IsUnified: false,
            IsReadOnly: false);

        string header = DiffViewStrings.Get(DiffViewStrings.MenuExpandFold);
        return host.View.MenuItemsFor(context).Single(item => item.Header == header);
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
