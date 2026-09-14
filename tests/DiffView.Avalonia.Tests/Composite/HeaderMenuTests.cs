using Avalonia;
using Avalonia.Automation;
using Avalonia.Controls;
using Avalonia.Headless;
using Avalonia.Headless.XUnit;
using Avalonia.Input;
using Avalonia.VisualTree;
using Bennewitz.Ninja.DiffView.Tests.Inline;
using Bennewitz.Ninja.DiffView.Core;

namespace Bennewitz.Ninja.DiffView.Tests.Composite;

/// <summary>
/// Plan 00012 phase 3: the header, and the second context type it needed. Five line-, row- and
/// block-shaped surfaces share <see cref="DiffPaneContext"/>; a header's subject is a side and its
/// file, with no line at all, so it carries its own record — and the tests that matter most here
/// are the ones asserting the two types did not take the *menu* apart with them.
/// </summary>
public sealed class HeaderMenuTests
{
    [AvaloniaFact]
    public async Task A_headers_context_names_the_side_and_its_file()
    {
        using CompositeHost host = new(width: 900, height: 400);
        host.Show();

        // Every member asserted below is given a *different* value on the two sides: different
        // titles, one side editable and one not, one side dirty and one not. A fixture where the
        // sides agree cannot tell a context that reads the wrong one from a context that reads
        // the right one — it passes either way, which is the same vacuum the map's row assertion
        // fell into in phase 2.
        await host.LoadAsync(
            CompositeHost.Named("one\nTWO\nthree\n", "Left.cs"),
            CompositeHost.Named("one\ntwo\nthree\n", "Right.cs"));
        host.View.LeftReadOnly = false;
        CompositeHost.Layout();

        host.Left.Document.Insert(0, "edited ");
        await host.WaitForReDiffAsync();

        DiffHeaderContext left = host.View.HeaderContextAt(DiffSide.Left);
        DiffHeaderContext right = host.View.HeaderContextAt(DiffSide.Right);

        // There is no LineNumber on the type to be wrong — which is the whole reason the type
        // exists, rather than a sixth DiffPaneRegion over a synthetic line.
        Assert.Equal(DiffSide.Left, left.Side);
        Assert.Equal("Left.cs", left.Title);
        Assert.True(left.IsDirty);
        Assert.False(left.IsReadOnly);

        Assert.Equal(DiffSide.Right, right.Side);
        Assert.Equal("Right.cs", right.Title);
        Assert.False(right.IsDirty);
        Assert.True(right.IsReadOnly);

        // And what it reports is what is on screen, not a second reading of the same sources.
        // The two details are not asserted to differ: on this pair they do not, because the
        // detail line counts lines, encoding, endings and characters and both files have the
        // same four readings. That is a fact about the fixture, not about the control.
        Assert.Equal(host.View.LeftHeader!.Title, left.Title);
        Assert.Equal(host.View.LeftHeader.Detail, left.Detail);
        Assert.Equal(host.View.RightHeader!.Detail, right.Detail);
    }

    [AvaloniaFact]
    public async Task Each_header_answers_for_its_own_side()
    {
        using CompositeHost host = new(width: 900, height: 400);
        host.Show();
        await host.LoadAsync("one\nTWO\nthree\n", "one\ntwo\nthree\n");
        CompositeHost.Layout();

        List<DiffHeaderContext> seen = [];
        host.View.HeaderContextMenuOpening += (_, e) =>
        {
            seen.Add(e.Context);
            e.Cancel = true;
        };

        // Matched by identity against this view's own two headers, as the margins are — "our left
        // header", not "some header of that class".
        RightClick(host, host.View.LeftHeader!);
        RightClick(host, host.View.RightHeader!);

        Assert.Equal([DiffSide.Left, DiffSide.Right], seen.Select(c => c.Side));
    }

    [AvaloniaFact]
    public async Task A_click_on_the_title_inside_the_header_is_a_click_on_the_header()
    {
        using CompositeHost host = new(width: 900, height: 400);
        host.Show();
        await host.LoadAsync(
            CompositeHost.Named("one\nTWO\nthree\n", "Greeter.cs"),
            CompositeHost.Named("one\ntwo\nthree\n", "Other.cs"));
        CompositeHost.Layout();

        DiffHeaderContext? seen = null;
        host.View.HeaderContextMenuOpening += (_, e) =>
        {
            seen = e.Context;
            e.Cancel = true;
        };

        // The handler is attached to the header rather than to a parent, and ContextRequested
        // bubbles — so the whole strip answers rather than the gaps between its text. There is no
        // foreign-header case to guard the way phase 1 guards a margin the library did not draw:
        // the pane's handler has several possible sources and a header's has exactly one.
        TextBlock title = host.View.LeftHeader!.GetVisualDescendants()
            .OfType<TextBlock>()
            .First(t => t.Text == "Greeter.cs");
        title.RaiseEvent(new ContextRequestedEventArgs { RoutedEvent = Control.ContextRequestedEvent });
        CompositeHost.Layout();

        Assert.NotNull(seen);
        Assert.Equal(DiffSide.Left, seen.Side);
    }

    [AvaloniaFact]
    public async Task A_header_menu_is_the_files_two_verbs_and_nothing_else()
    {
        using CompositeHost host = new(width: 900, height: 400);
        host.Show();
        await host.LoadAsync("one\nTWO\nthree\n", "one\ntwo\nthree\n");
        CompositeHost.Layout();

        List<DiffMenuItem> items = ItemsOn(host, host.View.LeftHeader!);

        Assert.Equal(
            [
                DiffViewStrings.MenuSave(DiffSide.Left),
                DiffViewStrings.MenuRevert(DiffSide.Left),
            ],
            items.Select(i => i.Header));

        // No copy and no navigate: a header is not a position. The entries are already named per
        // side, which is the only thing distinguishing the two headers' menus from each other.
        Assert.DoesNotContain(DiffViewStrings.MenuCopyChange(DiffSide.Right), items.Select(i => i.Header));
        Assert.DoesNotContain(DiffViewStrings.Get(DiffViewStrings.MenuNextChange), items.Select(i => i.Header));
        Assert.DoesNotContain(DiffViewStrings.Get(DiffViewStrings.MenuFind), items.Select(i => i.Header));
        Assert.DoesNotContain(DiffViewStrings.Get(DiffViewStrings.MenuGoToChange), items.Select(i => i.Header));
    }

    [AvaloniaFact]
    public async Task The_header_offers_the_same_two_file_verbs_the_text_does()
    {
        using CompositeHost host = new(width: 900, height: 400);
        host.Show();
        await host.LoadAsync("one\nTWO\nthree\n", "one\ntwo\nthree\n");
        host.View.LeftReadOnly = false;
        CompositeHost.Layout();

        host.Left.Document.Insert(0, "edited ");
        await host.WaitForReDiffAsync();

        List<DiffMenuItem> header = ItemsOn(host, host.View.LeftHeader!);
        List<DiffMenuItem> text = PaneItems(host);

        // Both lists come from one AddFileVerbs, so the two places that offer to write a file
        // cannot come to different answers about whether it can be written.
        foreach (string key in (string[])[DiffViewStrings.MenuSaveLeft, DiffViewStrings.MenuRevertLeft])
        {
            string label = DiffViewStrings.Get(key);
            DiffMenuItem fromHeader = header.Single(i => i.Header == label);
            DiffMenuItem fromText = text.Single(i => i.Header == label);
            Assert.Equal(fromText.IsEnabled, fromHeader.IsEnabled);
        }

        Assert.True(header.Single(i => i.Header == DiffViewStrings.MenuRevert(DiffSide.Left)).IsEnabled);
    }

    [AvaloniaFact]
    public async Task Save_and_revert_are_present_on_a_header_and_disabled_until_there_is_an_edit()
    {
        using CompositeHost host = new(width: 900, height: 400);
        host.Show();
        await host.LoadAsync("one\nTWO\nthree\n", "one\ntwo\nthree\n");
        host.View.LeftReadOnly = false;
        CompositeHost.Layout();

        string revert = DiffViewStrings.MenuRevert(DiffSide.Left);
        List<DiffMenuItem> before = ItemsOn(host, host.View.LeftHeader!);
        Assert.False(before.Single(i => i.Header == revert).IsEnabled);

        host.Left.Document.Insert(0, "edited ");
        await host.WaitForReDiffAsync();

        // Present the whole time and only its state moving, which is what makes a host's "insert
        // after this item" mean the same thing on every open.
        List<DiffMenuItem> after = ItemsOn(host, host.View.LeftHeader!);
        Assert.Equal(before.Select(i => i.Header), after.Select(i => i.Header));
        Assert.True(after.Single(i => i.Header == revert).IsEnabled);
    }

    [AvaloniaFact]
    public async Task A_headers_revert_entry_reverts_that_side()
    {
        using CompositeHost host = new(width: 900, height: 400);
        host.Show();
        await host.LoadAsync("one\nTWO\nthree\n", "one\ntwo\nthree\n");
        host.View.RightReadOnly = false;
        CompositeHost.Layout();

        host.Right.Document.Insert(0, "edited ");
        await host.WaitForReDiffAsync();
        Assert.True(host.View.IsEdited(DiffSide.Right));
        Assert.False(host.View.IsEdited(DiffSide.Left));

        ItemsOn(host, host.View.RightHeader!)
            .Single(i => i.Header == DiffViewStrings.MenuRevert(DiffSide.Right))
            .Command!.Execute(null);
        await host.WaitForReDiffAsync();

        Assert.StartsWith("one", host.Right.Document.Text, StringComparison.Ordinal);
        Assert.False(host.View.IsEdited(DiffSide.Right));
    }

    [AvaloniaFact]
    public async Task The_replacement_menu_suppresses_the_opening_event_on_a_header_too()
    {
        using CompositeHost host = new(width: 900, height: 400);
        host.Show();
        await host.LoadAsync("one\nTWO\nthree\n", "one\ntwo\nthree\n");
        CompositeHost.Layout();

        bool raised = false;
        host.View.HeaderContextMenuOpening += (_, _) => raised = true;
        ContextMenu mine = new();
        host.View.HeaderContextMenu = mine;

        RightClick(host, host.View.LeftHeader!);

        // The standing test for the seam: if this ever holds for one context type and not the
        // other, the two have split in fact and not just in type.
        Assert.False(raised);
        Assert.IsType<DiffHeaderContext>(mine.DataContext);
        Assert.Same(host.View.LeftHeader, mine.PlacementTarget);
    }

    [AvaloniaFact]
    public async Task The_two_replacement_properties_govern_their_own_surface_only()
    {
        using CompositeHost host = new(width: 900, height: 400);
        host.Show();
        await host.LoadAsync("one\nTWO\nthree\n", "one\ntwo\nthree\n");
        CompositeHost.Layout();

        ContextMenu forPanes = new();
        forPanes.Items.Add(new MenuItem { Header = "Pane" });
        host.View.PaneContextMenu = forPanes;

        bool headerRaised = false;
        host.View.HeaderContextMenuOpening += (_, e) =>
        {
            headerRaised = true;
            e.Cancel = true;
        };

        // A host that replaced the pane menu has said nothing about the header's, so the header
        // still builds ours and still raises its own event. One property governing both would be
        // the seam quietly becoming one surface again.
        RightClick(host, host.View.LeftHeader!);

        Assert.True(headerRaised);
        Assert.Null(forPanes.DataContext);
    }

    [AvaloniaFact]
    public async Task Every_entry_of_a_header_menu_announces_itself()
    {
        using CompositeHost host = new(width: 900, height: 400);
        host.Show();
        await host.LoadAsync("one\nTWO\nthree\n", "one\ntwo\nthree\n");
        CompositeHost.Layout();

        // Built in code, so the XAML accessibility sweep cannot see them.
        RightClick(host, host.View.LeftHeader!);

        Assert.NotNull(host.View.LastMenu);
        List<MenuItem> rows = host.View.LastMenu.Items.OfType<MenuItem>().ToList();
        Assert.Equal(2, rows.Count);
        Assert.All(rows, row => Assert.False(string.IsNullOrEmpty(AutomationProperties.GetName(row))));
    }

    [AvaloniaFact]
    public async Task The_unified_views_headers_raise_no_menu()
    {
        using InlineHost host = new(width: 900, height: 400);
        host.Show();
        await host.LoadAsync("one\nTWO\nthree\n", "one\ntwo\nthree\n");
        InlineHost.Layout();

        // The unified view *has* two headers — it shows both files above its one pane — so this
        // is not "there is nothing to click". It is that the view is read-only, so it has neither
        // of the two verbs a header menu is, and an empty list opens nothing. Absent, not empty.
        Assert.NotNull(host.View.LeftHeader);
        Assert.NotNull(host.View.RightHeader);

        host.View.LeftHeader.RaiseEvent(new ContextRequestedEventArgs { RoutedEvent = Control.ContextRequestedEvent });
        host.View.RightHeader.RaiseEvent(new ContextRequestedEventArgs { RoutedEvent = Control.ContextRequestedEvent });
        InlineHost.Layout();

        Assert.Null(host.View.LastMenu);
    }

    /// <summary>The items a right-click on <paramref name="header"/> would show, without opening a menu.</summary>
    private static List<DiffMenuItem> ItemsOn(CompositeHost host, DiffPaneHeader header)
    {
        List<DiffMenuItem> captured = [];
        void Capture(object? sender, DiffHeaderContextMenuEventArgs e)
        {
            captured.AddRange(e.Items);
            e.Cancel = true;
        }

        host.View.HeaderContextMenuOpening += Capture;
        try
        {
            RightClick(host, header);
        }
        finally
        {
            host.View.HeaderContextMenuOpening -= Capture;
        }

        Assert.NotEmpty(captured);
        return captured;
    }

    /// <summary>The left pane's own menu items, for comparing the file verbs against.</summary>
    private static List<DiffMenuItem> PaneItems(CompositeHost host)
    {
        List<DiffMenuItem> captured = [];
        void Capture(object? sender, DiffPaneContextMenuEventArgs e)
        {
            captured.AddRange(e.Items);
            e.Cancel = true;
        }

        host.View.PaneContextMenuOpening += Capture;
        try
        {
            host.Left.RaiseEvent(new ContextRequestedEventArgs { RoutedEvent = Control.ContextRequestedEvent });
        }
        finally
        {
            host.View.PaneContextMenuOpening -= Capture;
        }

        return captured;
    }

    private static void RightClick(CompositeHost host, DiffPaneHeader header)
    {
        Point inWindow = header.TranslatePoint(new Point(header.Bounds.Width / 2, header.Bounds.Height / 2), host.Window)
            ?? throw new InvalidOperationException("the header is not in the tree");
        host.Window.MouseDown(inWindow, MouseButton.Right);
        host.Window.MouseUp(inWindow, MouseButton.Right);
        CompositeHost.Layout();
    }
}
