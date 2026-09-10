using Avalonia.Controls;
using Avalonia.Headless.XUnit;
using Avalonia.Input;
using Bennewitz.Ninja.DiffView.Core;

namespace Bennewitz.Ninja.DiffView.Avalonia.Tests.Inline;

/// <summary>
/// Plan 00010 phase 1 in the unified view: the same seam, and a context that says what a pane
/// with no side can honestly say — <c>Side</c> is null, and the line names the file it came from.
/// </summary>
public sealed class InlinePaneContextMenuTests
{
    [AvaloniaFact]
    public async Task The_unified_context_has_no_side_and_names_the_line_own_file()
    {
        (string left, string right) = InlineHost.SmallFixture();
        using InlineHost host = new(width: 900, height: 400);
        host.Show();
        await host.LoadAsync(left, right);

        // A line well past the first block, where the composed numbering and the side's have
        // parted — the whole reason SourceLine exists as well as LineNumber.
        InlineDocument table = host.View.Inline!;
        int index = table.Lines.ToList().FindLastIndex(l => l.Kind == DiffLineKind.Unchanged);
        Assert.True(index > 0);

        DiffPaneContext context = host.Pane.ContextAt(index + 1);

        // The pane belongs to neither file, so a side would be a lie half the time.
        Assert.Null(context.Side);
        Assert.True(context.IsUnified);
        Assert.True(context.IsReadOnly);

        // The line belongs to one of them, and says which.
        Assert.Equal(table.Lines[index].Side, context.SourceSide);
        Assert.Equal(table.Lines[index].SourceLine + 1, context.SourceLine);
        Assert.NotEqual(context.LineNumber, context.SourceLine);
    }

    [AvaloniaFact]
    public async Task The_seam_is_the_same_one_the_side_by_side_view_has()
    {
        (string left, string right) = InlineHost.SmallFixture();
        using InlineHost host = new(width: 900, height: 400);
        host.Show();
        await host.LoadAsync(left, right);
        host.Pane.TextArea.Caret.Line = 3;
        InlineHost.Layout();

        DiffPaneContext? seen = null;
        host.View.PaneContextMenuOpening += (_, e) =>
        {
            seen = e.Context;
            Assert.Empty(e.Items);
            e.Items.Add(new DiffMenuItem { Header = "Mine" });
        };

        host.Pane.RaiseEvent(new ContextRequestedEventArgs { RoutedEvent = Control.ContextRequestedEvent });

        Assert.NotNull(seen);
        Assert.Equal(3, seen.LineNumber);
    }

    [AvaloniaFact]
    public async Task The_replacement_menu_suppresses_the_opening_event_here_too()
    {
        (string left, string right) = InlineHost.SmallFixture();
        using InlineHost host = new(width: 900, height: 400);
        host.Show();
        await host.LoadAsync(left, right);

        bool raised = false;
        host.View.PaneContextMenuOpening += (_, _) => raised = true;
        ContextMenu mine = new();
        host.View.PaneContextMenu = mine;

        host.Pane.RaiseEvent(new ContextRequestedEventArgs { RoutedEvent = Control.ContextRequestedEvent });

        Assert.False(raised);
        Assert.IsType<DiffPaneContext>(mine.DataContext);
    }
}
