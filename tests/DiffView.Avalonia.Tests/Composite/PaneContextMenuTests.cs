using Avalonia;
using Avalonia.Controls;
using Avalonia.Headless;
using Avalonia.Headless.XUnit;
using Avalonia.Input;
using Avalonia.Media;
using Avalonia.VisualTree;
using Bennewitz.Ninja.DiffView.Core;

namespace Bennewitz.Ninja.DiffView.Avalonia.Tests.Composite;

/// <summary>
/// Plan 00010: the context object a host reads, the two shapes it can change the menu through,
/// and the items the side-by-side view puts there — whose shape does not move with the state,
/// because a host's "insert after this item" has to mean the same thing on every open.
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
    public async Task An_emptied_list_opens_no_menu_and_a_host_item_alone_opens_one()
    {
        using CompositeHost host = new(width: 900, height: 400);
        host.Show();
        await host.LoadAsync("one\nTWO\nthree\n", "one\ntwo\nthree\n");
        CompositeHost.Layout();

        bool add = false;
        int openings = 0;
        host.View.PaneContextMenuOpening += (_, e) =>
        {
            openings++;
            Assert.NotEmpty(e.Items);
            e.Items.Clear();
            if (add)
            {
                e.Items.Add(new DiffMenuItem { Header = "Mine" });
            }
        };

        // The amend shape reaches as far as removing everything, and nothing left is no menu.
        RightClick(host, host.Left, new Point(60, 40));
        Assert.Equal(1, openings);
        Assert.Null(host.View.LastPaneMenu);

        add = true;
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

        bool empty = true;
        host.View.PaneContextMenuOpening += (_, e) =>
        {
            if (empty)
            {
                e.Items.Clear();
            }
        };

        RightClick(host, host.Left, new Point(60, 40));
        Assert.Null(host.View.LastPaneMenu);
        Assert.True(ancestor.IsOpen);

        ancestor.Close();
        CompositeHost.Layout();

        // With something of ours to show, the pane claims the request and the ancestor stays shut.
        empty = false;
        RightClick(host, host.Left, new Point(60, 40));

        Assert.NotNull(host.View.LastPaneMenu);
        Assert.False(ancestor.IsOpen);
    }

    // Plan 00010's `A_right_click_on_a_gutter_is_left_alone` asserted the opposite of the three
    // tests below: that a margin was NOT answered, because v1 was the panes and a margin's verbs
    // were "a later plan's". Plan 00012 is that plan, so the assertion is replaced rather than
    // deleted quietly — what it protected, that the text's menu is never offered in a margin's
    // place, is now carried by `A_margin_the_library_did_not_draw_is_left_alone`.

    [AvaloniaFact]
    public async Task Each_gutter_reports_its_own_region_and_the_text_still_reports_text()
    {
        using CompositeHost host = new(width: 900, height: 400);
        host.Show();
        await host.LoadAsync("one\nTWO\nthree\n", "one\ntwo\nthree\n");
        CompositeHost.Layout();

        List<DiffPaneRegion> seen = [];
        host.View.PaneContextMenuOpening += (_, e) =>
        {
            seen.Add(e.Context.Region);
            e.Cancel = true;
        };

        // The region is resolved from the event's source, so each gutter answers for itself. It
        // is not pointer arithmetic against the margins' widths — the margins own those, and a
        // constant here would be plan 00008's misaligned header a second time.
        host.Left.LineNumberMargin.RaiseEvent(new ContextRequestedEventArgs { RoutedEvent = Control.ContextRequestedEvent });
        host.Left.ChangeMarkerMargin.RaiseEvent(new ContextRequestedEventArgs { RoutedEvent = Control.ContextRequestedEvent });
        host.Left.RaiseEvent(new ContextRequestedEventArgs { RoutedEvent = Control.ContextRequestedEvent });

        Assert.Equal(
            [DiffPaneRegion.LineNumberMargin, DiffPaneRegion.ChangeMarkerMargin, DiffPaneRegion.Text],
            seen);
    }

    [AvaloniaFact]
    public async Task A_gutter_menu_leaves_the_file_verbs_to_the_file()
    {
        using CompositeHost host = new(width: 900, height: 400);
        host.Show();
        await host.LoadAsync("one\nTWO\nthree\n", "one\ntwo\nthree\n");
        CompositeHost.Layout();

        List<string?> headers = [];
        host.View.PaneContextMenuOpening += (_, e) =>
        {
            headers = e.Items.Select(i => i.Header).ToList();
            e.Cancel = true;
        };

        // Save and revert are the file's verbs and a gutter is a position, so they are absent
        // from its menu rather than greyed in it — the rule plan 00010 set for a verb a view does
        // not have, applied to a region that does not have one.
        host.Left.LineNumberMargin.RaiseEvent(new ContextRequestedEventArgs { RoutedEvent = Control.ContextRequestedEvent });
        List<string?> gutter = headers;

        host.Left.RaiseEvent(new ContextRequestedEventArgs { RoutedEvent = Control.ContextRequestedEvent });
        List<string?> text = headers;

        string save = DiffViewStrings.MenuSave(DiffSide.Left);
        string revert = DiffViewStrings.MenuRevert(DiffSide.Left);

        Assert.DoesNotContain(save, gutter);
        Assert.DoesNotContain(revert, gutter);
        Assert.Contains(save, text);
        Assert.Contains(revert, text);

        // The copy and navigate verbs the gutter IS about are still there, so this is a shorter
        // menu rather than an emptier one.
        Assert.NotEmpty(gutter);
        Assert.Contains(DiffViewStrings.MenuCopyChange(DiffSide.Right), gutter);
    }

    [AvaloniaFact]
    public async Task A_margin_the_library_did_not_draw_is_left_alone()
    {
        using CompositeHost host = new(width: 900, height: 400);
        host.Show();
        await host.LoadAsync("one\nTWO\nthree\n", "one\ntwo\nthree\n");
        CompositeHost.Layout();

        bool raised = false;
        host.View.PaneContextMenuOpening += (_, _) => raised = true;

        // AvaloniaEdit's own margins, and any a host adds, are not ours to answer for: their
        // verbs are not in DiffCommand and the text's menu would be the wrong menu. Answering
        // "some margin I do not recognise" with the pane's items is the failure this prevents.
        //
        // The margin is added to the text area rather than constructed loose, and the event is
        // raised ON it, because that is how the real one arrives: RaiseEvent overwrites a Source
        // set by hand with the control it was raised on, so a detached margin passed as Source
        // would test nothing — the guard would never see it, and the test would pass for the
        // wrong reason.
        AvaloniaEdit.Editing.LineNumberMargin foreign = new();
        host.Left.TextArea.LeftMargins.Add(foreign);
        CompositeHost.Layout();

        foreign.RaiseEvent(new ContextRequestedEventArgs { RoutedEvent = Control.ContextRequestedEvent });

        Assert.False(raised);
    }

    [AvaloniaFact]
    public async Task The_menu_shape_does_not_move_with_the_selection()
    {
        using CompositeHost host = new(width: 900, height: 400);
        host.Show();
        await host.LoadAsync("one\nTWO\nTHREE\nfour\n", "one\ntwo\nthree\nfour\n");
        host.View.RightReadOnly = false;
        host.Left.TextArea.Focus();
        CompositeHost.Layout();
        host.View.NextChange();

        List<DiffMenuItem> without = ItemsAt(host, 2);
        host.Left.Select(4, 3);
        CompositeHost.Layout();
        List<DiffMenuItem> with = ItemsAt(host, 2);

        // Beyond Compare's rule, and the reason "insert after this item" means anything: the same
        // entries in the same order either way. Only whether they can be invoked moves.
        Assert.Equal(without.Count, with.Count);
        Assert.Equal(without.Select(i => i.Header), with.Select(i => i.Header));
        Assert.Equal(without.Select(i => i.Verb), with.Select(i => i.Verb));

        DiffMenuItem selectionWithout = without.Single(i => i.Verb == DiffCommand.CopyToRight);
        DiffMenuItem selectionWith = with.Single(i => i.Verb == DiffCommand.CopyToRight);
        Assert.False(selectionWithout.IsEnabled);
        Assert.True(selectionWith.IsEnabled);
    }

    [AvaloniaFact]
    public async Task The_copy_entries_name_their_scope_and_their_direction()
    {
        using CompositeHost host = new(width: 900, height: 400);
        host.Show();
        await host.LoadAsync("one\nTWO\nthree\n", "one\ntwo\nthree\n");
        host.View.RightReadOnly = false;
        CompositeHost.Layout();

        List<DiffMenuItem> items = ItemsAt(host, 2);

        // Whole sentences per direction, not one sentence with a side word pushed into it: the
        // word is part of the sentence and a translator needs the whole of it to inflect.
        Assert.Equal(DiffViewStrings.Get(DiffViewStrings.MenuCopySelectionRight), items.Single(i => i.Verb == DiffCommand.CopyToRight).Header);
        Assert.Equal(DiffViewStrings.Get(DiffViewStrings.MenuCopyChangeRight), items.Single(i => i.Verb == DiffCommand.CopyBlockToRight).Header);

        // And the right pane's entries point the other way, from their own keys.
        List<DiffMenuItem> fromRight = ItemsOfRightPaneAt(host, 2);
        Assert.Equal(DiffViewStrings.Get(DiffViewStrings.MenuCopySelectionLeft), fromRight.Single(i => i.Verb == DiffCommand.CopyToLeft).Header);
        Assert.Equal(DiffViewStrings.Get(DiffViewStrings.MenuCopyChangeLeft), fromRight.Single(i => i.Verb == DiffCommand.CopyBlockToLeft).Header);
    }

    [AvaloniaFact]
    public async Task Every_row_keeps_a_gap_between_its_label_and_its_accelerator()
    {
        using CompositeHost host = new(width: 900, height: 400);
        host.Show();
        await host.LoadAsync("one\nTWO\nthree\n", "one\ntwo\nthree\n");
        CompositeHost.Layout();

        RightClick(host, host.Left, new Point(60, 40));
        Assert.NotNull(host.View.LastPaneMenu);

        // The widest row sets the popup's width, so its own accelerator has nowhere to sit and the
        // two touch. How tight that looks is otherwise the host theme's
        // `MenuItemInputGestureTextMargin` — 4 in Semi, 24 in Fluent — so the control holds a small
        // gap of its own and a host's value is added to it rather than replaced.
        List<MenuItem> rows = host.View.LastPaneMenu.Items.OfType<MenuItem>().ToList();
        Assert.NotEmpty(rows);
        Assert.All(rows, row =>
        {
            TextBlock header = Assert.IsType<TextBlock>(row.Header);
            Assert.True(header.Margin.Right > 0, $"'{header.Text}' has no gap before its accelerator");
        });
    }

    [AvaloniaFact]
    public async Task The_icon_column_is_reserved_whether_or_not_anything_fills_it()
    {
        using CompositeHost host = new(width: 900, height: 400);
        host.Show();
        await host.LoadAsync("one\nTWO\nthree\n", "one\ntwo\nthree\n");
        CompositeHost.Layout();

        // No icon set ships, and none is planned yet; the column is laid out now so that adding
        // one later moves nothing. A solid square is enough to prove it — the future without an
        // icon set — and every other entry keeps a null slot beside it.
        host.View.PaneContextMenuOpening += (_, e) =>
            e.Items.First(i => !i.IsSeparator).Icon = new Border
            {
                Width = 12,
                Height = 12,
                Background = Brushes.Black,
            };

        RightClick(host, host.Left, new Point(60, 40));
        CompositeHost.Layout();
        Assert.NotNull(host.View.LastPaneMenu);

        List<MenuItem> rows = host.View.LastPaneMenu.Items.OfType<MenuItem>().ToList();
        Assert.True(rows.Count > 2, "the menu needs several rows for alignment to mean anything");

        List<double> lefts = rows.Select(HeaderLeft).ToList();

        // Guard against the frame that never laid out: all-zero would satisfy "they agree".
        Assert.True(lefts[0] > 0, "the menu did not lay out, so the alignment assertion would be vacuous");
        Assert.All(lefts, left => Assert.Equal(lefts[0], left, 1));

        // And the one carrying the square is among them, not off on its own.
        Assert.Single(rows, r => r.Icon is not null);
    }

    /// <summary>Where a row's label starts, in the row's own coordinates.</summary>
    private static double HeaderLeft(MenuItem row)
    {
        TextBlock header = row.GetVisualDescendants().OfType<TextBlock>().First(t => !string.IsNullOrEmpty(t.Text));
        return header.TranslatePoint(default, row)?.X ?? -1;
    }

    [AvaloniaFact]
    public async Task A_host_translation_reaches_every_entry_in_the_menu()
    {
        using CompositeHost host = new(width: 900, height: 400);
        host.Show();
        await host.LoadAsync("one\nTWO\nthree\n", "one\ntwo\nthree\n");
        host.View.RightReadOnly = false;
        CompositeHost.Layout();

        try
        {
            // What a localized host does: one resolver over the whole catalogue. Every entry has
            // to come through it, or a translated application shows English in its context menu.
            DiffViewStrings.Resolver = key => "»" + key;
            List<DiffMenuItem> items = ItemsAt(host, 2);

            Assert.NotEmpty(items);
            Assert.All(
                items.Where(i => !i.IsSeparator),
                i => Assert.StartsWith("»", i.Header, StringComparison.Ordinal));
        }
        finally
        {
            DiffViewStrings.Resolver = null;
        }
    }

    [AvaloniaFact]
    public async Task The_accelerator_follows_the_key_map()
    {
        using CompositeHost host = new(width: 900, height: 400);
        host.Show();
        await host.LoadAsync("one\nTWO\nthree\n", "one\ntwo\nthree\n");
        CompositeHost.Layout();

        Assert.Equal(new KeyGesture(Key.F7), ItemsAt(host, 2).Single(i => i.Verb == DiffCommand.NextChange).Gesture);

        // The reason plan 00009 came first: a literal here would start lying now.
        host.View.KeyMap[DiffCommand.NextChange] = new KeyGesture(Key.F8);
        Assert.Equal(new KeyGesture(Key.F8), ItemsAt(host, 2).Single(i => i.Verb == DiffCommand.NextChange).Gesture);

        host.View.KeyMap[DiffCommand.NextChange] = null;
        Assert.Null(ItemsAt(host, 2).Single(i => i.Verb == DiffCommand.NextChange).Gesture);
    }

    [AvaloniaFact]
    public async Task The_block_entry_copies_the_block_that_was_clicked_not_the_current_one()
    {
        using CompositeHost host = new(width: 900, height: 400);
        host.Show();
        await host.LoadAsync("one\nTWO\nthree\nFOUR\nfive\n", "one\ntwo\nthree\nfour\nfive\n");
        host.View.RightReadOnly = false;
        CompositeHost.Layout();

        // The current change is the first block; the click is on the second. A context menu that
        // acted on the current one would not be a context menu.
        host.View.NextChange();
        Assert.Equal(0, host.View.CurrentChangeIndex);

        DiffMenuItem copy = ItemsAt(host, 4).Single(i => i.Verb == DiffCommand.CopyBlockToRight);
        Assert.True(copy.IsEnabled);
        copy.Command!.Execute(null);
        await host.WaitForReDiffAsync();

        Assert.Equal("one\ntwo\nthree\nFOUR\nfive\n", host.Right.Document.Text);
    }

    [AvaloniaFact]
    public async Task Save_and_revert_are_present_and_disabled_until_there_is_an_edit()
    {
        using CompositeHost host = new(width: 900, height: 400);
        host.Show();
        await host.LoadAsync("one\nTWO\nthree\n", "one\ntwo\nthree\n");
        host.View.LeftReadOnly = false;
        CompositeHost.Layout();

        string revert = DiffViewStrings.MenuRevert(DiffSide.Left);
        Assert.False(ItemsAt(host, 1).Single(i => i.Header == revert).IsEnabled);

        host.Left.Document.Insert(0, "edited ");
        await host.WaitForReDiffAsync();

        // Present the whole time, which is what makes the entry's position stable.
        Assert.True(ItemsAt(host, 1).Single(i => i.Header == revert).IsEnabled);
    }

    /// <summary>The same, for the right pane, whose entries point the other way.</summary>
    private static List<DiffMenuItem> ItemsOfRightPaneAt(CompositeHost host, int line)
    {
        return ItemsAt(host, line, host.Right);
    }

    /// <summary>
    /// The items the menu would show for <paramref name="line"/> of <paramref name="pane"/>, the
    /// left one by default. Asked with the keyboard, which resolves to the caret, so the caret is
    /// moved there first.
    /// </summary>
    private static List<DiffMenuItem> ItemsAt(CompositeHost host, int line, DiffPanePresenter? pane = null)
    {
        pane ??= host.Left;
        pane.TextArea.Caret.Line = line;
        CompositeHost.Layout();

        List<DiffMenuItem> captured = [];
        void Capture(object? sender, DiffPaneContextMenuEventArgs e)
        {
            captured.AddRange(e.Items);
            e.Cancel = true;
        }

        host.View.PaneContextMenuOpening += Capture;
        try
        {
            pane.RaiseEvent(new ContextRequestedEventArgs { RoutedEvent = Control.ContextRequestedEvent });
        }
        finally
        {
            host.View.PaneContextMenuOpening -= Capture;
        }

        return captured;
    }

    private static void RightClick(CompositeHost host, DiffPanePresenter pane, Point inPane)
    {
        Point inWindow = pane.TranslatePoint(inPane, host.Window) ?? inPane;
        host.Window.MouseDown(inWindow, MouseButton.Right);
        host.Window.MouseUp(inWindow, MouseButton.Right);
        CompositeHost.Layout();
    }
}
