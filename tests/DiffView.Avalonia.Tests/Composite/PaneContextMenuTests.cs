using Avalonia;
using Avalonia.Controls;
using Avalonia.Headless;
using Avalonia.Headless.XUnit;
using Avalonia.Input;
using Bennewitz.Ninja.DiffView.Core;

namespace Bennewitz.Ninja.DiffView.Avalonia.Tests.Composite;

/// <summary>
/// Plan 00010 phase 1: the context object a host reads, and the two shapes it can change the menu
/// through. There are no default items yet — the seam is built and tested before anything is in
/// it — so every menu here is one a host put there.
/// </summary>
public sealed class PaneContextMenuTests
{
    [AvaloniaFact]
    public async Task A_right_click_reports_the_line_the_side_and_the_block()
    {
        using CompositeHost host = new(width: 900, height: 400);
        host.Show();
        await host.LoadAsync("one\nTWO\nthree\n", "one\ntwo\nthree\n");
        CompositeHost.Layout();

        DiffPaneContext context = host.Left.ContextAt(2);

        Assert.Equal(DiffPaneRegion.Text, context.Region);
        Assert.Equal(DiffSide.Left, context.Side);
        Assert.Equal(DiffSide.Left, context.SourceSide);
        Assert.Equal(2, context.LineNumber);
        Assert.Equal(2, context.SourceLine);
        Assert.NotNull(context.Row);
        Assert.NotNull(context.Block);
        Assert.True(context.IsInChange);
        Assert.False(context.IsUnified);
        Assert.False(context.HasSelection);
    }

    [AvaloniaFact]
    public async Task An_unchanged_line_has_no_block_and_a_line_past_the_end_throws_nothing()
    {
        using CompositeHost host = new(width: 900, height: 400);
        host.Show();
        await host.LoadAsync("one\nTWO\nthree\n", "one\ntwo\nthree\n");
        CompositeHost.Layout();

        DiffPaneContext unchanged = host.Left.ContextAt(1);
        Assert.Null(unchanged.Block);
        Assert.False(unchanged.IsInChange);
        Assert.Equal(DiffLineKind.Unchanged, unchanged.Kind);

        // A line the model does not know — the trailing padding, or a document longer than the
        // model after an edit. Null row, null block, no throw; the menu still has to open.
        DiffPaneContext beyond = host.Left.ContextAt(9_999);
        Assert.Null(beyond.Row);
        Assert.Null(beyond.Block);
        Assert.Null(beyond.SourceLine);
    }

    [AvaloniaFact]
    public async Task The_context_carries_the_selection_the_gutter_reads()
    {
        using CompositeHost host = new(width: 900, height: 400);
        host.Show();
        await host.LoadAsync("one\nTWO\nTHREE\nfour\n", "one\ntwo\nthree\nfour\n");
        host.Left.TextArea.Focus();
        CompositeHost.Layout();

        host.Left.Select(4, 3);
        DiffPaneContext context = host.Left.ContextAt(2);

        Assert.True(context.HasSelection);
        Assert.Equal(host.Left.SelectedLines, context.SelectedLines);
    }

    [AvaloniaFact]
    public async Task A_right_click_leaves_the_caret_and_the_selection_where_they_were()
    {
        using CompositeHost host = new(width: 900, height: 400);
        host.Show();
        await host.LoadAsync("one\nTWO\nTHREE\nfour\n", "one\ntwo\nthree\nfour\n");
        host.Left.TextArea.Focus();
        CompositeHost.Layout();

        host.Left.Select(4, 8);
        LineRange? before = host.Left.SelectedLines;
        int caret = host.Left.TextArea.Caret.Offset;
        Assert.NotNull(before);

        DiffPaneContext? seen = null;
        host.View.PaneContextMenuOpening += (_, e) =>
        {
            seen = e.Context;
            e.Cancel = true;
        };

        // Well below the selection: a right-click that moved the caret would collapse it, and the
        // menu is about to offer to copy exactly those lines.
        RightClick(host, host.Left, new Point(60, 300));

        Assert.NotNull(seen);
        Assert.Equal(before, host.Left.SelectedLines);
        Assert.Equal(caret, host.Left.TextArea.Caret.Offset);
        Assert.Equal(before, seen.SelectedLines);
    }

    [AvaloniaFact]
    public async Task The_keyboard_asks_at_the_caret()
    {
        using CompositeHost host = new(width: 900, height: 400);
        host.Show();
        await host.LoadAsync("one\nTWO\nthree\nfour\nfive\n", "one\ntwo\nthree\nfour\nfive\n");
        host.Left.TextArea.Focus();
        CompositeHost.Layout();

        host.Left.TextArea.Caret.Line = 4;
        CompositeHost.Layout();

        DiffPaneContext? seen = null;
        host.View.PaneContextMenuOpening += (_, e) =>
        {
            seen = e.Context;
            e.Cancel = true;
        };

        // Shift+F10 and the Menu key arrive with no position at all, which is why the menu is not
        // a ContextMenu assigned in a template: that would answer them somewhere else entirely.
        host.Left.RaiseEvent(new ContextRequestedEventArgs { RoutedEvent = Control.ContextRequestedEvent });

        Assert.NotNull(seen);
        Assert.Equal(4, seen.LineNumber);
    }

    [AvaloniaFact]
    public async Task A_host_item_alone_makes_the_menu_and_an_empty_list_makes_none()
    {
        using CompositeHost host = new(width: 900, height: 400);
        host.Show();
        await host.LoadAsync("one\nTWO\nthree\n", "one\ntwo\nthree\n");
        CompositeHost.Layout();

        int openings = 0;
        host.View.PaneContextMenuOpening += (_, e) =>
        {
            openings++;
            Assert.Empty(e.Items);
        };

        // Nothing added: phase 1 builds no items of its own, so the event fires and no menu opens.
        RightClick(host, host.Left, new Point(60, 40));
        Assert.Equal(1, openings);
        Assert.Null(host.View.LastPaneMenu);

        host.View.PaneContextMenuOpening += (_, e) => e.Items.Add(new DiffMenuItem { Header = "Mine" });
        RightClick(host, host.Left, new Point(60, 40));

        Assert.Equal(2, openings);
        Assert.NotNull(host.View.LastPaneMenu);
        Assert.True(host.View.LastPaneMenu.IsOpen);
        Assert.Single(host.View.LastPaneMenu.Items);
    }

    [AvaloniaFact]
    public async Task Cancelling_the_opening_suppresses_a_menu_that_would_otherwise_open()
    {
        using CompositeHost host = new(width: 900, height: 400);
        host.Show();
        await host.LoadAsync("one\nTWO\nthree\n", "one\ntwo\nthree\n");
        CompositeHost.Layout();

        bool cancel = false;
        host.View.PaneContextMenuOpening += (_, e) =>
        {
            e.Items.Add(new DiffMenuItem { Header = "Mine" });
            e.Cancel = cancel;
        };

        // The same item list either way, so the only thing under test is the flag.
        RightClick(host, host.Left, new Point(60, 40));
        Assert.NotNull(host.View.LastPaneMenu);

        cancel = true;
        RightClick(host, host.Left, new Point(60, 40));
        Assert.Null(host.View.LastPaneMenu);
    }

    [AvaloniaFact]
    public async Task The_replacement_menu_suppresses_the_opening_event()
    {
        using CompositeHost host = new(width: 900, height: 400);
        host.Show();
        await host.LoadAsync("one\nTWO\nthree\n", "one\ntwo\nthree\n");
        CompositeHost.Layout();

        bool raised = false;
        host.View.PaneContextMenuOpening += (_, _) => raised = true;
        ContextMenu mine = new();
        host.View.PaneContextMenu = mine;

        RightClick(host, host.Left, new Point(60, 40));

        // There is nothing of ours to amend, so the event does not fire; the context reaches a
        // host's own menu through its DataContext instead.
        Assert.False(raised);
        Assert.IsType<DiffPaneContext>(mine.DataContext);
    }

    [AvaloniaFact]
    public async Task A_request_the_pane_answers_with_nothing_still_reaches_an_ancestor_menu()
    {
        using CompositeHost host = new(width: 900, height: 400);
        host.Show();
        await host.LoadAsync("one\nTWO\nthree\n", "one\ntwo\nthree\n");
        CompositeHost.Layout();

        // A plain Avalonia menu on the view itself — not PaneContextMenu, the ordinary property
        // any control has. Eating the request while opening nothing would suppress it in silence.
        ContextMenu ancestor = new();
        ancestor.Items.Add(new MenuItem { Header = "Host" });
        host.View.ContextMenu = ancestor;

        RightClick(host, host.Left, new Point(60, 40));
        Assert.Null(host.View.LastPaneMenu);
        Assert.True(ancestor.IsOpen);

        ancestor.Close();
        CompositeHost.Layout();

        // With something of ours to show, the pane claims the request and the ancestor stays shut.
        host.View.PaneContextMenuOpening += (_, e) => e.Items.Add(new DiffMenuItem { Header = "Mine" });
        RightClick(host, host.Left, new Point(60, 40));

        Assert.NotNull(host.View.LastPaneMenu);
        Assert.False(ancestor.IsOpen);
    }

    [AvaloniaFact]
    public async Task A_right_click_on_a_gutter_is_left_alone()
    {
        using CompositeHost host = new(width: 900, height: 400);
        host.Show();
        await host.LoadAsync("one\nTWO\nthree\n", "one\ntwo\nthree\n");
        CompositeHost.Layout();

        bool raised = false;
        host.View.PaneContextMenuOpening += (_, _) => raised = true;

        // v1 is the panes. A margin's verbs are its own and are a later plan's, so the text's menu
        // is not offered in their place.
        host.Left.LineNumberMargin.RaiseEvent(new ContextRequestedEventArgs { RoutedEvent = Control.ContextRequestedEvent });

        Assert.False(raised);
    }

    private static void RightClick(CompositeHost host, DiffPanePresenter pane, Point inPane)
    {
        Point inWindow = pane.TranslatePoint(inPane, host.Window) ?? inPane;
        host.Window.MouseDown(inWindow, MouseButton.Right);
        host.Window.MouseUp(inWindow, MouseButton.Right);
        CompositeHost.Layout();
    }
}
