using Avalonia;
using Avalonia.Headless;
using Avalonia.Headless.XUnit;
using Avalonia.Input;
using Bennewitz.Ninja.DiffView.Core;

namespace Bennewitz.Ninja.DiffView.Avalonia.Tests.Composite;

/// <summary>
/// Plan 00007, the overview map: a lane per side so a one-sided block inks one and notches the
/// other, the viewport box that drags where the rest of the map jumps, the wheel, and the toggle
/// that gives the column's width back to the panes.
/// </summary>
public sealed class MinimapLaneTests
{
    [AvaloniaFact]
    public async Task A_deletion_inks_the_left_lane_and_notches_the_right()
    {
        using CompositeHost host = new(width: 900, height: 600);
        host.Show();

        // Ten lines the right side does not have, surrounded by lines it does: a block big enough
        // to cover several buckets on its own.
        await host.LoadAsync(Lines("a", 5) + Lines("gone", 10) + Lines("b", 5), Lines("a", 5) + Lines("b", 5));
        CompositeHost.Layout();

        DiffMinimap map = host.View.Minimap!;
        ChangeBlock block = Assert.Single(host.View.Document!.Blocks);
        Assert.Equal(DiffLineKind.Deleted, block.Kind);

        int bucket = map.BucketOfRow(block.FirstRow + (block.RowCount / 2));
        Assert.Equal(DiffLineKind.Deleted, map.KindOfBucket(bucket, DiffSide.Left));
        Assert.Equal(DiffLineKind.Unchanged, map.KindOfBucket(bucket, DiffSide.Right));

        // The combined reading is what the single-lane map always gave, so nothing that asked the
        // old question gets a new answer.
        Assert.Equal(DiffLineKind.Deleted, map.KindOfBucket(bucket));
    }

    [AvaloniaFact]
    public async Task An_insertion_is_the_mirror()
    {
        using CompositeHost host = new(width: 900, height: 600);
        host.Show();
        await host.LoadAsync(Lines("a", 5) + Lines("b", 5), Lines("a", 5) + Lines("new", 10) + Lines("b", 5));
        CompositeHost.Layout();

        DiffMinimap map = host.View.Minimap!;
        ChangeBlock block = Assert.Single(host.View.Document!.Blocks);
        int bucket = map.BucketOfRow(block.FirstRow + (block.RowCount / 2));

        Assert.Equal(DiffLineKind.Inserted, map.KindOfBucket(bucket, DiffSide.Right));
        Assert.Equal(DiffLineKind.Unchanged, map.KindOfBucket(bucket, DiffSide.Left));
    }

    [AvaloniaFact]
    public async Task A_modification_inks_both_lanes()
    {
        using CompositeHost host = new(width: 900, height: 600);
        host.Show();
        await host.LoadAsync(Lines("a", 5) + Lines("OLD", 10) + Lines("b", 5), Lines("a", 5) + Lines("new", 10) + Lines("b", 5));
        CompositeHost.Layout();

        DiffMinimap map = host.View.Minimap!;
        ChangeBlock block = host.View.Document!.Blocks[0];
        int bucket = map.BucketOfRow(block.FirstRow + (block.RowCount / 2));

        // Both sides have a line in those rows, so neither lane is notched.
        Assert.NotEqual(DiffLineKind.Unchanged, map.KindOfBucket(bucket, DiffSide.Left));
        Assert.NotEqual(DiffLineKind.Unchanged, map.KindOfBucket(bucket, DiffSide.Right));
    }

    [AvaloniaFact]
    public async Task Every_lane_bucket_is_one_the_model_puts_there()
    {
        (string left, string right) = CompositeHost.SmallFixture();
        using CompositeHost host = new(width: 900, height: 600);
        host.Show();
        await host.LoadAsync(left, right);
        CompositeHost.Layout();

        DiffMinimap map = host.View.Minimap!;
        SideBySideDocument model = host.View.Document!;

        // The lanes derived from the model, bucket by bucket, against the lanes the map reports.
        // A drawing cannot vouch for itself, so the expectation is computed rather than read back.
        int buckets = map.BucketCount;
        int rows = model.Rows.Count;
        Assert.True(rows > 0 && buckets > 0);

        foreach (DiffSide side in (DiffSide[])[DiffSide.Left, DiffSide.Right])
        {
            DiffLineKind[] expected = new DiffLineKind[buckets];
            for (int row = 0; row < rows; row++)
            {
                AlignedRow aligned = model.Rows[row];
                if (aligned.Kind == DiffLineKind.Unchanged || SideBySideDocument.LineOf(aligned, side) is null)
                {
                    continue;
                }

                int bucket = (int)((long)row * buckets / rows);
                if (Strength(aligned.Kind) > Strength(expected[bucket]))
                {
                    expected[bucket] = aligned.Kind;
                }
            }

            for (int bucket = 0; bucket < buckets; bucket++)
            {
                Assert.Equal(expected[bucket], map.KindOfBucket(bucket, side));
            }
        }

        // The independent part: the two lanes together say exactly what the single lane said, so
        // nothing that asked the old question gets a new answer.
        for (int bucket = 0; bucket < buckets; bucket++)
        {
            DiffLineKind inLeftLane = map.KindOfBucket(bucket, DiffSide.Left);
            DiffLineKind inRightLane = map.KindOfBucket(bucket, DiffSide.Right);
            Assert.Equal(
                Strength(inLeftLane) >= Strength(inRightLane) ? inLeftLane : inRightLane,
                map.KindOfBucket(bucket));
        }
    }

    [AvaloniaFact]
    public async Task The_tooltip_names_the_lane()
    {
        using CompositeHost host = new(width: 900, height: 600);
        host.Show();
        await host.LoadAsync(Lines("a", 5) + Lines("gone", 10) + Lines("b", 5), Lines("a", 5) + Lines("b", 5));
        CompositeHost.Layout();

        DiffMinimap map = host.View.Minimap!;
        ChangeBlock block = Assert.Single(host.View.Document!.Blocks);
        double y = map.BucketOfRow(block.FirstRow + 1) + 0.5;

        // Inside a lane the tooltip says which side it is; between the lanes it does not claim one.
        Assert.Equal(DiffSide.Left, map.LaneAt(3));
        Assert.Equal(DiffSide.Right, map.LaneAt(13));
        Assert.Null(map.LaneAt(0));

        string? left = map.TooltipFor(3, y);
        Assert.NotNull(left);
        Assert.Contains("left", left, StringComparison.Ordinal);
        Assert.DoesNotContain("left", map.TooltipFor(0, y)!, StringComparison.Ordinal);
    }

    [AvaloniaFact]
    public async Task The_map_costs_its_width_and_nothing_when_it_is_off()
    {
        (string left, string right) = CompositeHost.SmallFixture();
        using CompositeHost host = new(width: 900, height: 600);
        host.Show();
        await host.LoadAsync(left, right);
        CompositeHost.Layout();

        DiffMinimap map = host.View.Minimap!;
        Assert.True(map.IsVisible);
        Assert.Equal(DiffMinimap.MapWidth, map.Bounds.Width);
        double panesWith = host.Left.Bounds.Width + host.Right.Bounds.Width;

        host.View.ShowMinimap = false;
        CompositeHost.Layout();

        // Off, the column takes nothing and the panes get it back rather than looking at a gap.
        Assert.False(map.IsVisible);
        Assert.Equal(panesWith + DiffMinimap.MapWidth, host.Left.Bounds.Width + host.Right.Bounds.Width, 1);
    }

    [AvaloniaFact]
    public async Task A_host_setting_the_toggle_before_the_template_applies_is_wired_too()
    {
        (string left, string right) = CompositeHost.SmallFixture();
        using CompositeHost host = new(width: 900, height: 600);

        // Set before the template exists, so the property-change handler has no part to reach and
        // the wiring falls to template application — the path a host setting it in XAML takes.
        host.View.ShowMinimap = false;
        host.Show();
        await host.LoadAsync(left, right);
        CompositeHost.Layout();

        Assert.False(host.View.Minimap!.IsVisible);
    }

    [AvaloniaFact]
    public async Task A_press_outside_the_viewport_jumps_and_a_press_inside_it_drags()
    {
        using CompositeHost host = new(width: 900, height: 400);
        host.Show();
        await host.LoadAsync(Lines("a", 200) + Lines("gone", 40) + Lines("b", 200), Lines("a", 200) + Lines("b", 200));
        CompositeHost.Layout();
        host.Capture().Dispose();

        DiffMinimap map = host.View.Minimap!;
        Assert.NotNull(map.ViewportBounds);
        Rect viewport = map.ViewportBounds.Value;

        List<int> jumps = [];
        map.JumpRequested += (_, row) => jumps.Add(row);

        // Outside the box: one jump, and no drag — a move afterwards must add nothing.
        double outside = Math.Min(map.Bounds.Height - 1, viewport.Bottom + 40);
        host.Window.MouseDown(ToWindow(map, host, new Point(6, outside)), MouseButton.Left);
        host.Window.MouseUp(ToWindow(map, host, new Point(6, outside)), MouseButton.Left);
        host.Window.MouseMove(ToWindow(map, host, new Point(6, outside - 10)));
        CompositeHost.Layout();
        Assert.Single(jumps);

        // Inside the box: the press starts a drag, and each move scrolls again. The rectangle is
        // read again first — the click above scrolled the panes, so the box has moved since.
        jumps.Clear();
        CompositeHost.Layout();
        Assert.NotNull(map.ViewportBounds);
        Point inside = new(6, map.ViewportBounds.Value.Center.Y);
        host.Window.MouseDown(ToWindow(map, host, inside), MouseButton.Left);
        host.Window.MouseMove(ToWindow(map, host, inside + new Point(0, 20)));
        host.Window.MouseMove(ToWindow(map, host, inside + new Point(0, 40)));
        host.Window.MouseUp(ToWindow(map, host, inside + new Point(0, 40)), MouseButton.Left);
        CompositeHost.Layout();
        Assert.Equal(3, jumps.Count);
        Assert.True(jumps[2] > jumps[0], $"dragging down should scroll down; got {jumps[0]} then {jumps[2]}");

        // And the drag ended: a move after the release scrolls nothing.
        jumps.Clear();
        host.Window.MouseMove(ToWindow(map, host, inside + new Point(0, 60)));
        CompositeHost.Layout();
        Assert.Empty(jumps);
    }

    [AvaloniaFact]
    public async Task The_wheel_over_the_map_scrolls_the_panes()
    {
        using CompositeHost host = new(width: 900, height: 400);
        host.Show();
        await host.LoadAsync(Lines("a", 200) + Lines("gone", 40) + Lines("b", 200), Lines("a", 200) + Lines("b", 200));
        CompositeHost.Layout();
        host.Capture().Dispose();

        DiffMinimap map = host.View.Minimap!;
        double before = host.Left.VerticalOffset;
        host.Window.MouseWheel(ToWindow(map, host, new Point(6, map.Bounds.Height / 2)), new Vector(0, -3));
        CompositeHost.Layout();

        Assert.True(host.Left.VerticalOffset > before, $"the wheel should scroll the panes; {before} → {host.Left.VerticalOffset}");
    }

    private static int Strength(DiffLineKind kind)
    {
        return kind switch
        {
            DiffLineKind.Deleted => 3,
            DiffLineKind.Inserted => 2,
            DiffLineKind.Modified => 1,
            _ => 0,
        };
    }

    private static Point ToWindow(Visual control, CompositeHost host, Point point)
    {
        return control.TranslatePoint(point, host.Window)
               ?? throw new InvalidOperationException("The control is not in the window's visual tree.");
    }

    private static string Lines(string text, int count)
    {
        return string.Concat(Enumerable.Range(0, count).Select(i => $"{text}{i}\n"));
    }
}
