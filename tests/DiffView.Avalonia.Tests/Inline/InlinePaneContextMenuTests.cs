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
            e.Items.Add(new DiffMenuItem { Header = "Mine" });
        };

        host.Pane.RaiseEvent(new ContextRequestedEventArgs { RoutedEvent = Control.ContextRequestedEvent });

        Assert.NotNull(seen);
        Assert.Equal(3, seen.LineNumber);
    }

    [AvaloniaFact]
    public async Task A_verb_this_view_lacks_is_absent_from_the_menu_not_greyed_in_it()
    {
        (string left, string right) = InlineHost.SmallFixture();
        using InlineHost host = new(width: 900, height: 400);
        host.Show();
        await host.LoadAsync(left, right);

        List<DiffMenuItem> items = [];
        host.View.PaneContextMenuOpening += (_, e) =>
        {
            items.AddRange(e.Items);
            e.Cancel = true;
        };
        host.Pane.RaiseEvent(new ContextRequestedEventArgs { RoutedEvent = Control.ContextRequestedEvent });

        // Disabled says "not now"; this view has no other side to copy to and no half-and-half
        // document to save, ever. A greyed entry would promise a state that does not exist.
        Assert.DoesNotContain(items, i => i.Verb is DiffCommand.CopyToLeft or DiffCommand.CopyToRight);
        Assert.DoesNotContain(items, i => i.Verb is DiffCommand.CopyBlockToLeft or DiffCommand.CopyBlockToRight);
        Assert.DoesNotContain(items, i => i.Header?.StartsWith("Save", StringComparison.Ordinal) == true);
        Assert.DoesNotContain(items, i => i.Header?.StartsWith("Revert", StringComparison.Ordinal) == true);

        // What it does have, it has: the navigation and the find the side-by-side view also shows.
        Assert.Contains(items, i => i.Verb == DiffCommand.NextChange);
        Assert.Contains(items, i => i.Verb == DiffCommand.PreviousChange);
        Assert.Contains(items, i => i.Verb == DiffCommand.OpenFind);
    }

    [AvaloniaFact]
    public void A_verb_the_resolver_has_no_command_for_makes_no_item_at_all()
    {
        // The test above proves the unified list has no copies; it would prove that just as well
        // if nothing built them, which is in fact why. This one is the rule itself: asked for an
        // item whose command does not exist, `Verb` answers with nothing rather than something
        // greyed — so a copy item added to that list by mistake vanishes instead of throwing out
        // of a menu, and the menu's idea of what exists stays the key map's.
        Assert.Null(DiffPaneMenu.Verb(
            "Copy to the left side",
            DiffCommand.CopyToLeft,
            _ => null,
            _ => null,
            enabled: true));

        Assert.NotNull(DiffPaneMenu.Verb(
            "Next change",
            DiffCommand.NextChange,
            _ => new DelegateCommand(() => { }),
            _ => null,
            enabled: true));
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
