using Avalonia;
using Avalonia.Automation;
using Avalonia.Controls;
using Avalonia.Headless;
using Avalonia.Headless.XUnit;
using Avalonia.Input;
using Avalonia.Media.Imaging;
using Avalonia.VisualTree;
using Bennewitz.Ninja.DiffView.Avalonia.Tests.Inline;
using Bennewitz.Ninja.DiffView.Core;

namespace Bennewitz.Ninja.DiffView.Avalonia.Tests.Composite;

/// <summary>
/// Plan 00012 phase 2: the two surfaces between and beside the panes. Each already hit-tests what
/// it is about for its own left-click, so the menu is that hit-test asked a second question — and
/// the first thing every one of these asserts is that asking it changes nothing.
/// </summary>
public sealed class ConnectorAndMapMenuTests
{
    [AvaloniaFact]
    public async Task A_right_click_on_the_connector_names_the_polygons_own_block_and_jumps_nowhere()
    {
        using CompositeHost host = new(width: 900, height: 640);
        await LoadAsync(host);
        ChangeConnectorGutter gutter = host.View.Gutter!;
        SideBySideDocument document = host.View.Document!;

        // The tallest block, because the point of naming the polygon's own block rather than the
        // block nearest the pointer's row is that the two differ only where a polygon is tall.
        ChangeBlock tallest = document.Blocks.MaxBy(b => b.RowCount)!;
        Point inside = Inside(gutter, tallest);

        host.View.CurrentChangeIndex = -1;
        CompositeHost.Layout();

        DiffPaneContext? seen = null;
        host.View.PaneContextMenuOpening += (_, e) =>
        {
            seen = e.Context;
            e.Cancel = true;
        };

        RightClick(host, gutter, inside);

        Assert.NotNull(seen);
        Assert.Equal(DiffPaneRegion.ConnectorGutter, seen.Region);
        Assert.Equal(tallest, seen.Block);

        // The column belongs to neither pane, so it names no side — and the row it reports is one
        // of the block's own, because the polygon never reaches past the rows the block occupies.
        Assert.Null(seen.Side);
        Assert.NotNull(seen.Row);
        Assert.InRange(seen.Row.Value, tallest.FirstRow, tallest.LastRow);

        // Offering "go to this change" is not performing it. A left-click at the same point is.
        Assert.Equal(-1, host.View.CurrentChangeIndex);
        LeftClick(host, gutter, inside);
        Assert.Equal(tallest.Index, host.View.CurrentChangeIndex);
    }

    [AvaloniaFact]
    public async Task A_right_click_off_every_polygon_is_the_splitter_and_opens_nothing()
    {
        using CompositeHost host = new(width: 900, height: 640);
        await LoadAsync(host);
        ChangeConnectorGutter gutter = host.View.Gutter!;
        SideBySideDocument document = host.View.Document!;

        Assert.Equal(DiffLineKind.Unchanged, document.Rows[0].Kind);
        Point empty = new(gutter.Bounds.Width / 2, gutter.TopOfRow(0) + (gutter.RowHeight / 2));
        Assert.Null(gutter.PolygonAt(empty));

        bool raised = false;
        host.View.PaneContextMenuOpening += (_, _) => raised = true;

        // Off a polygon there is no block, and every entry this menu has is about one. The empty
        // column is the splitter, whose verb is a drag; a menu of four greyed rows would be worse
        // than none, because a greyed row promises a state in which it would work.
        RightClick(host, gutter, empty);

        Assert.False(raised);
        Assert.Null(host.View.LastPaneMenu);
    }

    [AvaloniaFact]
    public async Task The_connectors_menu_is_both_copy_directions_and_the_two_block_verbs()
    {
        using CompositeHost host = new(width: 900, height: 640);
        await LoadAsync(host);
        host.View.LeftReadOnly = false;
        host.View.RightReadOnly = false;
        CompositeHost.Layout();

        ChangeBlock block = host.View.Document!.Blocks.MaxBy(b => b.RowCount)!;
        List<DiffMenuItem> items = ItemsOn(host, host.View.Gutter!, Inside(host.View.Gutter!, block));

        Assert.Equal(
            [
                DiffViewStrings.MenuCopyChange(DiffSide.Left),
                DiffViewStrings.MenuCopyChange(DiffSide.Right),
                DiffViewStrings.Get(DiffViewStrings.MenuGoToChange),
                DiffViewStrings.Get(DiffViewStrings.MenuSelectChange),
            ],
            items.Select(i => i.Header));

        // No find and no navigate: the connector is not a position in a file, and no save or
        // revert, which are the file's verbs and belong to the header.
        Assert.DoesNotContain(DiffViewStrings.Get(DiffViewStrings.MenuFind), items.Select(i => i.Header));
        Assert.DoesNotContain(DiffViewStrings.Get(DiffViewStrings.MenuNextChange), items.Select(i => i.Header));
        Assert.All(items, i => Assert.False(i.IsSeparator));
    }

    [AvaloniaFact]
    public async Task The_connectors_entries_act_on_the_block_under_the_pointer()
    {
        using CompositeHost host = new(width: 900, height: 640);
        await LoadAsync(host);
        ChangeConnectorGutter gutter = host.View.Gutter!;

        // A block with lines on both sides, so "select" has something to select on each.
        ChangeBlock block = host.View.Document!.Blocks.First(b => !b.LeftLines.IsEmpty && !b.RightLines.IsEmpty);
        host.View.CurrentChangeIndex = -1;
        CompositeHost.Layout();

        List<DiffMenuItem> items = ItemsOn(host, gutter, Inside(gutter, block));
        Find(items, DiffViewStrings.MenuGoToChange).Command!.Execute(null);
        Assert.Equal(block.Index, host.View.CurrentChangeIndex);

        // The block spans both files, so selecting from a column that belongs to neither selects
        // in both panes rather than picking a side on the strength of which one had focus.
        Find(items, DiffViewStrings.MenuSelectChange).Command!.Execute(null);
        CompositeHost.Layout();
        Assert.Equal(block.LeftLines, host.Left.SelectedLines);
        Assert.Equal(block.RightLines, host.Right.SelectedLines);
    }

    [AvaloniaFact]
    public async Task Selecting_a_block_one_side_has_no_lines_in_clears_that_sides_selection()
    {
        using CompositeHost host = new(width: 900, height: 640);
        await LoadAsync(host);

        ChangeBlock? oneSided = host.View.Document!.Blocks.FirstOrDefault(b => b.LeftLines.IsEmpty ^ b.RightLines.IsEmpty);
        Assert.NotNull(oneSided);
        DiffSide absent = oneSided.LeftLines.IsEmpty ? DiffSide.Left : DiffSide.Right;
        DiffPanePresenter pane = absent == DiffSide.Left ? host.Left : host.Right;

        pane.Select(0, 4);
        CompositeHost.Layout();
        Assert.NotNull(pane.SelectedLines);

        // Left standing, a selection somewhere else would be describing a different change — and
        // the copy arrows read the selection, so it would also change what a copy copies.
        host.View.SelectChange(oneSided.Index, side: null);
        CompositeHost.Layout();

        Assert.Null(pane.SelectedLines);
        Assert.NotNull((absent == DiffSide.Left ? host.Right : host.Left).SelectedLines);
    }

    [AvaloniaFact]
    public async Task A_right_click_on_the_map_names_the_row_it_reports_and_scrolls_nothing()
    {
        using CompositeHost host = new(width: 900, height: 300);
        await LoadAsync(host);
        DiffMinimap map = host.View.Minimap!;

        double y = map.Bounds.Height * 0.8;

        // Against what the map *reports*, not against pixels: a lane is thin lines rather than a
        // band wherever rows are sparser than pixels, so a bucket is the only honest unit here.
        int expected = map.RowForClick(y);
        double offsetBefore = host.Left.VerticalOffset;

        DiffPaneContext? seen = null;
        host.View.PaneContextMenuOpening += (_, e) =>
        {
            seen = e.Context;
            e.Cancel = true;
        };

        Point point = new(LaneX(map, DiffSide.Left), y);
        RightClick(host, map, point);

        Assert.NotNull(seen);
        Assert.Equal(DiffPaneRegion.OverviewMap, seen.Region);
        Assert.Equal(expected, seen.Row);
        Assert.Equal(offsetBefore, host.Left.VerticalOffset);

        // And a left-click at the same point still does the map's own verb.
        LeftClick(host, map, point);
        Assert.True(host.Left.VerticalOffset > offsetBefore, "a left-click on the map should still scroll");
    }

    [AvaloniaFact]
    public async Task The_maps_row_is_the_one_a_click_goes_to_not_the_one_the_pixel_starts_at()
    {
        // More rows than the map has pixels, which is the only arrangement where those two
        // differ: a bucket then holds several rows, and a click goes to the first *changed* one
        // rather than the first. On the small fixture every bucket holds at most one row — a lane
        // is thin lines rather than a band there — so the same assertion is vacuous, which is
        // exactly how this one was written the first time and why it caught no mutation.
        string[] left = [.. Enumerable.Range(0, 4_000).Select(i => $"line {i}")];
        string[] right = [.. left];
        for (int i = 37; i < right.Length; i += 211)
        {
            right[i] = $"CHANGED {i}";
        }

        using CompositeHost host = new(width: 900, height: 300);
        host.Show();
        await host.LoadAsync(string.Join('\n', left) + "\n", string.Join('\n', right) + "\n");
        using (WriteableBitmap _ = host.Capture())
        {
        }

        DiffMinimap map = host.View.Minimap!;
        Assert.True(map.RowCount > map.BucketCount, $"{map.RowCount} rows over {map.BucketCount} buckets proves nothing");

        double y = Enumerable.Range(0, (int)map.Bounds.Height)
            .Select(i => i + 0.5)
            .First(candidate => map.RowForClick(candidate) != map.RowAtPixel(candidate));

        DiffPaneContext? seen = null;
        host.View.PaneContextMenuOpening += (_, e) =>
        {
            seen = e.Context;
            e.Cancel = true;
        };

        RightClick(host, map, new Point(LaneX(map, DiffSide.Left), y));

        Assert.NotNull(seen);
        Assert.Equal(map.RowForClick(y), seen.Row);
        Assert.NotEqual(map.RowAtPixel(y), seen.Row);
    }

    [AvaloniaFact]
    public async Task The_maps_context_names_the_lane_under_the_pointer_and_the_line_that_row_has()
    {
        using CompositeHost host = new(width: 900, height: 300);
        await LoadAsync(host);
        DiffMinimap map = host.View.Minimap!;
        SideBySideDocument document = host.View.Document!;

        List<DiffPaneContext> seen = [];
        host.View.PaneContextMenuOpening += (_, e) =>
        {
            seen.Add(e.Context);
            e.Cancel = true;
        };

        double y = map.Bounds.Height * 0.5;
        AlignedRow row = document.Rows[map.RowForClick(y)];

        RightClick(host, map, new Point(LaneX(map, DiffSide.Left), y));
        RightClick(host, map, new Point(LaneX(map, DiffSide.Right), y));
        RightClick(host, map, new Point(MarkerColumnX(map), y));

        Assert.Equal([DiffSide.Left, DiffSide.Right, null], seen.Select(c => c.Side));

        // The lane says which side the menu is about; the line is that side's own where it has
        // one, and the other's where it pads — SourceSide says which, and it is never a
        // stand-in, because a row always has a line on at least one side.
        foreach (DiffPaneContext context in seen)
        {
            int? own = context.Side is { } side ? row.LineOf(side) : null;
            DiffSide expected = own is not null ? context.Side!.Value
                : row.LeftLine is not null ? DiffSide.Left
                : DiffSide.Right;
            Assert.Equal(expected, context.SourceSide);
            Assert.Equal(row.LineOf(expected) + 1, context.LineNumber);
            Assert.Equal(context.LineNumber, context.SourceLine);
        }
    }

    [AvaloniaFact]
    public async Task The_maps_menu_goes_to_a_row_to_a_change_and_takes_itself_off_screen()
    {
        using CompositeHost host = new(width: 900, height: 300);
        await LoadAsync(host);
        DiffMinimap map = host.View.Minimap!;

        double y = map.Bounds.Height * 0.8;
        int row = map.RowForClick(y);
        List<DiffMenuItem> items = ItemsOn(host, map, new Point(LaneX(map, DiffSide.Left), y));

        Assert.Equal(
            [
                DiffViewStrings.Get(DiffViewStrings.MenuGoToRow),
                DiffViewStrings.Get(DiffViewStrings.MenuGoToChange),
                null,
                DiffViewStrings.Get(DiffViewStrings.MenuHideOverviewMap),
            ],
            items.Select(i => i.Header));

        // No "select this change": the map's subject is a row, and the lines a block covers are
        // in the panes rather than anywhere the map can show them.
        Assert.DoesNotContain(DiffViewStrings.Get(DiffViewStrings.MenuSelectChange), items.Select(i => i.Header));

        double before = host.Left.VerticalOffset;
        Find(items, DiffViewStrings.MenuGoToRow).Command!.Execute(null);
        CompositeHost.Layout();
        Assert.True(host.Left.VerticalOffset > before, $"row {row} should have scrolled the panes from {before}");

        Assert.True(host.View.ShowMinimap);
        Find(items, DiffViewStrings.MenuHideOverviewMap).Command!.Execute(null);
        CompositeHost.Layout();
        Assert.False(host.View.ShowMinimap);
        Assert.False(map.IsVisible);
    }

    [AvaloniaFact]
    public async Task The_block_verbs_are_disabled_rather_than_absent_off_a_change()
    {
        using CompositeHost host = new(width: 900, height: 300);
        await LoadAsync(host);
        DiffMinimap map = host.View.Minimap!;

        // A bucket whose rows are all unchanged: the map still answers, with the same four rows.
        double y = Enumerable.Range(0, (int)map.Bounds.Height)
            .Select(i => i + 0.5)
            .First(candidate => host.View.Document!.Rows[map.RowForClick(candidate)].Kind == DiffLineKind.Unchanged);

        List<DiffMenuItem> items = ItemsOn(host, map, new Point(LaneX(map, DiffSide.Left), y));

        // 00010's shape rule: whether the pointer is in a change is state, and a host's "insert
        // after this item" has to mean the same thing on every open.
        Assert.Equal(4, items.Count);
        Assert.False(Find(items, DiffViewStrings.MenuGoToChange).IsEnabled);
        Assert.True(Find(items, DiffViewStrings.MenuGoToRow).IsEnabled);
    }

    [AvaloniaFact]
    public async Task The_replacement_menu_suppresses_the_opening_event_on_both_new_surfaces()
    {
        using CompositeHost host = new(width: 900, height: 640);
        await LoadAsync(host);
        ChangeConnectorGutter gutter = host.View.Gutter!;
        DiffMinimap map = host.View.Minimap!;
        ChangeBlock block = host.View.Document!.Blocks.MaxBy(b => b.RowCount)!;

        bool raised = false;
        host.View.PaneContextMenuOpening += (_, _) => raised = true;
        ContextMenu mine = new();
        host.View.PaneContextMenu = mine;

        // Both surfaces go through the one DiffPaneMenu.Request, so this rule cannot hold for one
        // and not the other. If it ever does, the seam has split in fact and not just in type.
        foreach ((Control control, Point point) in ((Control, Point)[])
                 [
                     (gutter, Inside(gutter, block)),
                     (map, new Point(LaneX(map, DiffSide.Left), map.Bounds.Height * 0.5)),
                 ])
        {
            mine.DataContext = null;
            RightClick(host, control, point);

            Assert.False(raised);
            Assert.IsType<DiffPaneContext>(mine.DataContext);
            Assert.Same(control, mine.PlacementTarget);
            mine.Close();
            CompositeHost.Layout();
        }
    }

    [AvaloniaFact]
    public async Task Every_entry_of_the_two_new_menus_announces_itself()
    {
        using CompositeHost host = new(width: 900, height: 640);
        await LoadAsync(host);
        ChangeConnectorGutter gutter = host.View.Gutter!;
        ChangeBlock block = host.View.Document!.Blocks.MaxBy(b => b.RowCount)!;

        // Built in code, so the XAML accessibility sweep cannot see them.
        foreach ((Control control, Point point) in ((Control, Point)[])
                 [
                     (gutter, Inside(gutter, block)),
                     (host.View.Minimap!, new Point(LaneX(host.View.Minimap!, DiffSide.Left), host.View.Minimap!.Bounds.Height * 0.5)),
                 ])
        {
            RightClick(host, control, point);
            Assert.NotNull(host.View.LastPaneMenu);

            List<MenuItem> rows = host.View.LastPaneMenu.Items.OfType<MenuItem>().ToList();
            Assert.NotEmpty(rows);
            Assert.All(rows, row => Assert.False(string.IsNullOrEmpty(AutomationProperties.GetName(row))));

            host.View.LastPaneMenu.Close();
            CompositeHost.Layout();
        }
    }

    [AvaloniaFact]
    public async Task The_unified_view_has_no_connector_and_no_map_to_ask()
    {
        using InlineHost host = new(width: 900, height: 400);
        host.Show();
        (string left, string right) = CompositeHost.SmallFixture();
        await host.LoadAsync(left, right);
        InlineHost.Layout();

        // Absent, not empty: there is no handler to wire because there is no control to wire it
        // to, and the two verbs they carry answer null there for the same reason.
        Assert.Empty(host.View.GetVisualDescendants().OfType<ChangeConnectorGutter>());
        Assert.Empty(host.View.GetVisualDescendants().OfType<DiffMinimap>());
        Assert.Throws<NotSupportedException>(() => host.View.CommandFor(DiffCommand.GoToChange));
        Assert.Throws<NotSupportedException>(() => host.View.CommandFor(DiffCommand.SelectBlock));
    }

    /// <summary>Loads the committed small pair and renders once, because the polygons are a frame's product.</summary>
    private static async Task LoadAsync(CompositeHost host)
    {
        (string left, string right) = CompositeHost.SmallFixture();
        host.Show();
        await host.LoadAsync(left, right);
        using (WriteableBitmap _ = host.Capture())
        {
        }
    }

    /// <summary>A point inside <paramref name="block"/>'s polygon, halfway down its taller edge.</summary>
    private static Point Inside(ChangeConnectorGutter gutter, ChangeBlock block)
    {
        ConnectorPolygon polygon = gutter.LastPolygons[block.Index];
        double bottom = Math.Max(polygon.LeftBottom.Y, polygon.RightBottom.Y);
        Point inside = new(gutter.Bounds.Width / 2, (polygon.LeftTop.Y + bottom) / 2);
        Assert.True(polygon.Contains(inside), $"block {block.Index}'s polygon should contain {inside}");
        return inside;
    }

    /// <summary>An x the map itself says is in <paramref name="side"/>'s lane.</summary>
    private static double LaneX(DiffMinimap map, DiffSide side)
    {
        return Enumerable.Range(0, (int)map.Bounds.Width)
            .Select(i => i + 0.5)
            .First(x => map.LaneAt(x) == side);
    }

    /// <summary>An x in neither lane — the marker column, the gap or the margin.</summary>
    private static double MarkerColumnX(DiffMinimap map)
    {
        return Enumerable.Range(0, (int)map.Bounds.Width)
            .Select(i => i + 0.5)
            .First(x => map.LaneAt(x) is null);
    }

    /// <summary>The entry headed <paramref name="key"/>'s text; the caller's assertion if there is none.</summary>
    private static DiffMenuItem Find(List<DiffMenuItem> items, string key)
    {
        string header = DiffViewStrings.Get(key);
        return items.Single(i => i.Header == header);
    }

    /// <summary>The items a right-click at <paramref name="point"/> would show, without opening a menu.</summary>
    private static List<DiffMenuItem> ItemsOn(CompositeHost host, Control control, Point point)
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
            RightClick(host, control, point);
        }
        finally
        {
            host.View.PaneContextMenuOpening -= Capture;
        }

        Assert.NotEmpty(captured);
        return captured;
    }

    private static void RightClick(CompositeHost host, Control control, Point inControl)
    {
        Click(host, control, inControl, MouseButton.Right);
    }

    private static void LeftClick(CompositeHost host, Control control, Point inControl)
    {
        Click(host, control, inControl, MouseButton.Left);
    }

    private static void Click(CompositeHost host, Control control, Point inControl, MouseButton button)
    {
        Point inWindow = control.TranslatePoint(inControl, host.Window) ?? inControl;
        host.Window.MouseDown(inWindow, button);
        host.Window.MouseUp(inWindow, button);
        CompositeHost.Layout();
    }
}
