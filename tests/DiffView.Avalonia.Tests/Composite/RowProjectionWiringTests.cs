using System.Text;
using Avalonia.Controls;
using Avalonia.Headless;
using Avalonia.Headless.XUnit;
using Bennewitz.Ninja.DiffView.Core;

namespace Bennewitz.Ninja.DiffView.Avalonia.Tests.Composite;

/// <summary>
/// Plan 00013 phase 1. The two surfaces outside a pane converted rows to pixels by multiplying,
/// which is only accidentally true — it holds because every row is one line height, and a fold is
/// the first thing that makes a row not be one. These assert that each reads the projection rather
/// than the row index, and that with nothing folded each answers exactly what it answered before.
/// </summary>
public sealed class RowProjectionWiringTests
{
    private const double Tolerance = 1e-6;
    private const double RowHeight = 10;

    /// <summary>
    /// Two folds, so an assertion cannot pass by reading one fold's own count instead of the
    /// running total: rows 3–6 behind a placeholder at 3, rows 12–16 behind one at 12.
    /// </summary>
    private static RowProjection TwoFolds(int rowCount)
    {
        return RowProjection.Of(rowCount, [new FoldedRun(3, 4), new FoldedRun(12, 5)]);
    }

    [AvaloniaFact]
    public void The_connector_places_a_block_at_its_visible_row_not_its_model_row()
    {
        ChangeConnectorGutter gutter = new() { RowHeight = RowHeight, VerticalOffset = 0, ContentOffset = 0 };

        // With nothing folded the gutter answers what it always answered.
        Assert.Equal(170, gutter.TopOfRow(17), Tolerance);
        gutter.Projection = RowProjection.Identity(20);
        Assert.Equal(170, gutter.TopOfRow(17), Tolerance);
        Assert.Equal(17, gutter.RowAt(170));

        gutter.Projection = TwoFolds(20);

        // Row 17 is the eleventh row on screen once seven above it are hidden.
        Assert.Equal(100, gutter.TopOfRow(17), Tolerance);
        Assert.Equal(40, gutter.TopOfRow(7), Tolerance);
        Assert.Equal(20, gutter.TopOfRow(2), Tolerance);

        // Every row of a fold is drawn where its placeholder is.
        Assert.Equal(gutter.TopOfRow(3), gutter.TopOfRow(6), Tolerance);
        Assert.Equal(30, gutter.TopOfRow(5), Tolerance);

        // And read backwards, a pixel names the model row under it.
        Assert.Equal(17, gutter.RowAt(100));
        Assert.Equal(7, gutter.RowAt(40));
        Assert.Equal(3, gutter.RowAt(30));
        Assert.Null(gutter.RowAt(-1));
    }

    [AvaloniaFact]
    public void The_connector_scrolled_past_a_fold_still_agrees_with_itself()
    {
        ChangeConnectorGutter gutter = new()
        {
            RowHeight = RowHeight,
            VerticalOffset = 35,
            ContentOffset = 5,
            Projection = TwoFolds(20),
        };

        // TopOfRow and RowAt are one equation read each way, offsets included.
        foreach (int row in (int[])[0, 2, 3, 7, 11, 12, 17, 19])
        {
            double top = gutter.TopOfRow(row);
            int back = gutter.RowAt(top) ?? -1;
            Assert.Equal(gutter.TopOfRow(back), top, Tolerance);
        }

        Assert.Equal(17, gutter.RowAt(gutter.TopOfRow(17)));
    }

    /// <summary>
    /// The gutter culls by pixels and draws by model rows, so the two bounds it culls against are
    /// visible rows that have to be projected back. Getting that wrong does not misplace a
    /// polygon — it <i>breaks out of the loop early</i>, and every block below the fold silently
    /// stops being drawn.
    /// </summary>
    [AvaloniaFact]
    public void The_connector_still_draws_the_blocks_a_fold_brought_into_view()
    {
        SideBySideDocument document = Pair(400);
        ChangeBlock far = document.Blocks.First(block => block.FirstRow > 300);
        ChangeConnectorGutter gutter = new()
        {
            Document = document,
            RowHeight = 1,
            VerticalOffset = 0,
            ContentOffset = 0,
        };

        Window window = new() { Width = 30, Height = 200, Content = gutter };
        window.Show();
        Render(window);
        try
        {
            // Unfolded, that block is far below a 200 px gutter and is correctly culled.
            Assert.DoesNotContain(gutter.LastPolygons, polygon => polygon.BlockIndex == far.Index);

            // Folding everything above it brings it into view, and it has to be drawn there.
            gutter.Projection = RowProjection.Of(document.Rows.Count, [new FoldedRun(0, 300)]);
            Render(window);

            Assert.Contains(gutter.LastPolygons, polygon => polygon.BlockIndex == far.Index);
            ConnectorPolygon drawn = gutter.LastPolygons.First(polygon => polygon.BlockIndex == far.Index);
            Assert.Equal(gutter.TopOfRow(far.FirstRow), drawn.LeftTop.Y, Tolerance);
            Assert.InRange(drawn.LeftTop.Y, 0, 200);
        }
        finally
        {
            window.Close();
        }
    }

    [AvaloniaFact]
    public void The_map_buckets_the_visible_document_rather_than_the_model()
    {
        SideBySideDocument document = Pair(400);
        DiffMinimap minimap = new() { Document = document };
        Window window = new() { Width = 14, Height = 100, Content = minimap };
        window.Show();
        CompositeHost.Layout();
        try
        {
            int rows = document.Rows.Count;
            int buckets = minimap.BucketCount;
            Assert.True(buckets >= 50, $"the map should be tens of pixels tall, got {buckets}");
            Assert.Equal(rows, minimap.VisibleRowCount);

            int bucket = buckets / 2;
            int unfolded = minimap.FirstRowOfBucket(bucket);
            Assert.Equal((int)((long)bucket * rows / buckets), unfolded);

            // Fold half of the document away and the same bucket names a different row.
            RowProjection projection = RowProjection.Of(rows, [new FoldedRun(0, rows / 2)]);
            minimap.Projection = projection;
            Assert.Equal(rows - (rows / 2) + 1, minimap.VisibleRowCount);
            Assert.Equal(rows, minimap.RowCount);

            int folded = minimap.FirstRowOfBucket(bucket);
            Assert.Equal(projection.ModelRowOf((int)((long)bucket * minimap.VisibleRowCount / buckets)), folded);
            Assert.NotEqual(unfolded, folded);

            // The two directions still invert, and the last bucket still ends at the model's end.
            Assert.Equal(bucket, minimap.BucketOfRow(folded));
            Assert.Equal(rows, minimap.EndRowOfBucket(buckets - 1));
        }
        finally
        {
            window.Close();
        }
    }

    [AvaloniaFact]
    public void The_maps_viewport_box_is_measured_against_the_visible_document()
    {
        SideBySideDocument document = Pair(400);
        DiffMinimap minimap = new() { Document = document };
        Window window = new() { Width = 14, Height = 100, Content = minimap };
        window.Show();
        CompositeHost.Layout();
        try
        {
            int rows = document.Rows.Count;

            // Scrolled to the very bottom: the box sits at the bottom whether or not rows are
            // folded, because the viewport is measured in the rows that are on screen.
            RowProjection projection = RowProjection.Of(rows, [new FoldedRun(0, rows / 2)]);
            minimap.Projection = projection;
            minimap.ViewportRowCount = 20;
            minimap.ViewportStartRow = minimap.VisibleRowCount - 20;
            CompositeHost.Layout();

            Assert.NotNull(minimap.ViewportBounds);
            double bottom = minimap.ViewportBounds!.Value.Bottom;
            Assert.True(
                bottom >= minimap.Bounds.Height - 1,
                $"the box should reach the bottom of a {minimap.Bounds.Height} px map, got {bottom}");
        }
        finally
        {
            window.Close();
        }
    }

    /// <summary>
    /// Lays out and draws. <c>InvalidateVisual</c> schedules a frame that the headless dispatcher
    /// does not pump on its own, so a property read back after a render pass needs the frame
    /// asked for.
    /// </summary>
    private static void Render(Window window)
    {
        CompositeHost.Layout();
        window.CaptureRenderedFrame();
    }

    /// <summary>A pair of <paramref name="lines"/> lines with one modified every fifty.</summary>
    private static SideBySideDocument Pair(int lines)
    {
        StringBuilder left = new(lines * 12);
        StringBuilder right = new(lines * 12);
        for (int i = 0; i < lines; i++)
        {
            left.Append("line ").Append(i).Append('\n');
            right.Append(i % 50 == 10 ? "LINE " : "line ").Append(i).Append('\n');
        }

        return DiffDocumentBuilder.Build(left.ToString(), right.ToString()).Document;
    }
}
