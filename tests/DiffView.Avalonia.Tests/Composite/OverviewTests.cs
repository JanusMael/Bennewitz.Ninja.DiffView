using System.Text;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Headless;
using Avalonia.Headless.XUnit;
using Avalonia.Input;
using Avalonia.Media.Imaging;
using Bennewitz.Ninja.DiffView.Core;

namespace Bennewitz.Ninja.DiffView.Avalonia.Tests.Composite;

/// <summary>
/// Plan 00001 §Phase 7, the overview: the minimap's pixel → bucket → row mapping on the
/// 200k-line fixture and its click in the composite; the connector gutter's polygons, its
/// click selecting a block and its drag resizing the panes.
/// </summary>
public sealed class OverviewTests
{
    private const double Tolerance = 1e-6;

    /// <summary>A pair of <paramref name="lines"/> lines with one line modified and one inserted every fifty.</summary>
    private static (string Left, string Right) LargePair(int lines)
    {
        StringBuilder left = new(lines * 12);
        StringBuilder right = new(lines * 12);
        for (int i = 0; i < lines; i++)
        {
            left.Append("line ").Append(i).Append('\n');
            if (i % 50 == 10)
            {
                right.Append("LINE ").Append(i).Append('\n');
                right.Append("inserted after ").Append(i).Append('\n');
            }
            else
            {
                right.Append("line ").Append(i).Append('\n');
            }
        }

        return (left.ToString(), right.ToString());
    }

    [AvaloniaFact]
    public void The_minimap_maps_pixels_to_buckets_to_rows_at_top_middle_and_bottom_on_the_200k_line_fixture()
    {
        (string left, string right) = LargePair(200_000);
        SideBySideDocument document = DiffDocumentBuilder.Build(left, right).Document;
        Assert.True(document.Rows.Count > 200_000);
        DiffMinimap minimap = new() { Document = document };
        Window window = new() { Width = 14, Height = 400, Content = minimap };
        window.Show();
        CompositeHost.Layout();
        try
        {
            int rows = document.Rows.Count;
            int buckets = minimap.BucketCount;
            Assert.True(buckets >= 300, $"the minimap should be a few hundred pixels tall, got {buckets}");

            foreach (int bucket in new[] { 0, buckets / 2, buckets - 1 })
            {
                int expectedRow = (int)((long)bucket * rows / buckets);
                Assert.Equal(expectedRow, minimap.RowAtPixel(bucket));
                Assert.Equal(expectedRow, minimap.FirstRowOfBucket(bucket));
                Assert.Equal(bucket, minimap.BucketOfRow(expectedRow));
                Assert.Equal((int)((long)(bucket + 1) * rows / buckets), minimap.EndRowOfBucket(bucket));

                // The bucket's kind is the strongest of its rows, and a click lands on its first changed row.
                DiffLineKind strongest = DiffLineKind.Unchanged;
                int firstChanged = -1;
                for (int row = minimap.FirstRowOfBucket(bucket); row < minimap.EndRowOfBucket(bucket); row++)
                {
                    DiffLineKind kind = document.Rows[row].Kind;
                    if (Strength(kind) > Strength(strongest))
                    {
                        strongest = kind;
                    }

                    if (firstChanged < 0 && kind != DiffLineKind.Unchanged)
                    {
                        firstChanged = row;
                    }
                }

                Assert.Equal(strongest, minimap.KindOfBucket(bucket));
                Assert.Equal(firstChanged < 0 ? expectedRow : firstChanged, minimap.RowForClick(bucket));
            }

            Assert.Equal(0, minimap.BucketOfRow(0));
            Assert.Equal(buckets - 1, minimap.BucketOfRow(rows - 1));
            Assert.Equal(rows - 1, Math.Max(minimap.RowAtPixel(buckets - 1), Math.Min(rows - 1, minimap.EndRowOfBucket(buckets - 1) - 1)));
            // The fixture modifies and inserts, never deletes: every bucket's strongest kind is inserted.
            Assert.StartsWith("Row 1 of ", minimap.TooltipFor(0), StringComparison.Ordinal);
            Assert.EndsWith("inserted", minimap.TooltipFor(buckets / 2), StringComparison.Ordinal);
        }
        finally
        {
            window.Close();
        }
    }

    [AvaloniaFact]
    public async Task A_minimap_click_in_the_composite_jumps_both_panes_and_the_viewport_tracks_the_scroll()
    {
        (string left, string right) = CompositeHost.SmallFixture();
        using CompositeHost host = new(width: 900, height: 300);
        host.Show();
        await host.LoadAsync(left, right);
        DiffMinimap minimap = host.View.Minimap!;
        double lineHeight = host.Left.TextArea.TextView.DefaultLineHeight;
        Assert.Same(host.View.Document, minimap.Document);
        Assert.Equal(0, minimap.ViewportStartRow, Tolerance);
        Assert.Equal(host.Left.ViewportHeight / lineHeight, minimap.ViewportRowCount, Tolerance);

        double y = minimap.Bounds.Height * 0.8;
        int expectedRow = minimap.RowForClick(y);
        Point point = minimap.TranslatePoint(new Point(minimap.Bounds.Width / 2, y), host.Window) ?? throw new InvalidOperationException("minimap not in tree");
        host.Window.MouseDown(point, MouseButton.Left);
        host.Window.MouseUp(point, MouseButton.Left);
        CompositeHost.Layout();

        Assert.True(host.Left.VerticalOffset > 0, "the click should have scrolled the panes");
        Assert.Equal(host.Left.VerticalOffset, host.Right.VerticalOffset, Tolerance);
        double centre = host.Left.VerticalOffset + host.Left.ViewportHeight / 2;
        double rowTop = expectedRow * lineHeight;
        double maxOffset = host.Left.ExtentHeight - host.Left.ViewportHeight;
        Assert.True(host.Left.VerticalOffset >= maxOffset - Tolerance || Math.Abs(centre - (rowTop + lineHeight / 2)) <= lineHeight, $"row {expectedRow} should be centred or the panes at the bottom");
        Assert.Equal(host.Left.VerticalOffset / lineHeight, minimap.ViewportStartRow, Tolerance);
    }

    [AvaloniaFact]
    public async Task Connector_polygons_have_the_expected_extents_a_click_selects_the_block_and_a_drag_resizes_the_panes()
    {
        (string left, string right) = CompositeHost.SmallFixture();
        using CompositeHost host = new(width: 900, height: 640);
        host.Show();
        await host.LoadAsync(left, right);
        using (WriteableBitmap _ = host.Capture())
        {
        }

        ChangeConnectorGutter gutter = host.View.Gutter!;
        SideBySideDocument document = host.View.Document!;
        double lineHeight = host.Left.TextArea.TextView.DefaultLineHeight;
        Assert.Equal(lineHeight, gutter.RowHeight, Tolerance);
        Assert.Equal(document.Blocks.Count, gutter.LastPolygons.Count);

        // The gutter's row tops agree with the left pane's, translated into gutter coordinates.
        Point paneRow3 = host.Left.TextArea.TextView.TranslatePoint(new Point(0, 3 * lineHeight - host.Left.VerticalOffset), gutter) ?? throw new InvalidOperationException("text view not in tree");
        Assert.Equal(paneRow3.Y, gutter.TopOfRow(3), 1e-3);

        foreach (ChangeBlock block in document.Blocks)
        {
            ConnectorPolygon polygon = gutter.LastPolygons[block.Index];
            Assert.Equal(block.Index, polygon.BlockIndex);
            Assert.Equal(gutter.TopOfRow(block.FirstRow), polygon.LeftTop.Y, Tolerance);
            Assert.Equal(gutter.TopOfRow(block.FirstRow), polygon.RightTop.Y, Tolerance);
            Assert.Equal(gutter.TopOfRow(block.FirstRow + block.ModifiedCount + block.DeletedCount), polygon.LeftBottom.Y, Tolerance);
            Assert.Equal(gutter.TopOfRow(block.FirstRow + block.ModifiedCount + block.InsertedCount), polygon.RightBottom.Y, Tolerance);
            Assert.Equal(0, polygon.LeftTop.X, Tolerance);
            Assert.Equal(gutter.Bounds.Width, polygon.RightTop.X, Tolerance);
        }

        // A click inside the tallest polygon makes its block the current change.
        ChangeBlock tallest = document.Blocks.MaxBy(b => b.RowCount)!;
        ConnectorPolygon target = gutter.LastPolygons[tallest.Index];
        double midY = (target.LeftTop.Y + Math.Max(target.LeftBottom.Y, target.RightBottom.Y)) / 2;
        Point inside = new(gutter.Bounds.Width / 2, midY);
        Assert.True(target.Contains(inside));
        Assert.Equal(tallest.Index, gutter.PolygonAt(inside)!.BlockIndex);
        Assert.StartsWith($"Change {tallest.Index + 1} of {document.Blocks.Count}", gutter.TooltipFor(inside), StringComparison.Ordinal);
        Point clickAt = gutter.TranslatePoint(inside, host.Window) ?? throw new InvalidOperationException("gutter not in tree");
        host.Window.MouseDown(clickAt, MouseButton.Left);
        host.Window.MouseUp(clickAt, MouseButton.Left);
        CompositeHost.Layout();
        Assert.Equal(tallest.Index, host.View.CurrentChangeIndex);
        Assert.Equal(tallest.Index, gutter.CurrentChangeIndex);

        // A drag on empty space — row 0 is unchanged on both sides — widens the left pane.
        Assert.Equal(DiffLineKind.Unchanged, document.Rows[0].Kind);
        Point empty = new(gutter.Bounds.Width / 2, gutter.TopOfRow(0) + lineHeight / 2);
        Assert.Null(gutter.PolygonAt(empty));
        double leftBefore = host.Left.Bounds.Width;
        double ratioBefore = host.View.SplitRatio;
        Point start = gutter.TranslatePoint(empty, host.Window) ?? throw new InvalidOperationException("gutter not in tree");
        host.Window.MouseDown(start, MouseButton.Left);
        host.Window.MouseMove(start + new Point(40, 0));
        host.Window.MouseMove(start + new Point(80, 0));
        host.Window.MouseUp(start + new Point(80, 0), MouseButton.Left);
        CompositeHost.Layout();
        Assert.True(host.View.SplitRatio > ratioBefore, $"the split ratio should have grown from {ratioBefore}, got {host.View.SplitRatio}");
        Assert.True(host.Left.Bounds.Width > leftBefore + 40, $"the left pane should be wider than {leftBefore} + 40, got {host.Left.Bounds.Width}");
        Assert.Equal(tallest.Index, host.View.CurrentChangeIndex);
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
}
