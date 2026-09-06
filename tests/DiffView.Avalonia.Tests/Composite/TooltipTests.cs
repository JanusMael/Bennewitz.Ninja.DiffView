using Avalonia;
using Avalonia.Controls;
using Avalonia.Headless;
using Avalonia.Headless.XUnit;
using Avalonia.Media.Imaging;
using Bennewitz.Ninja.DiffView.Core;

namespace Bennewitz.Ninja.DiffView.Avalonia.Tests.Composite;

/// <summary>Plan 00001 §Phase 7, the tooltips: the aligned line on a line number, the block summary on a marker, and the pointer bringing them up.</summary>
public sealed class TooltipTests
{
    [AvaloniaFact]
    public async Task Line_numbers_name_the_counterpart_and_markers_name_the_block()
    {
        (string left, string right) = CompositeHost.SmallFixture();
        using CompositeHost host = new(width: 900, height: 640);
        host.Show();
        await host.LoadAsync(left, right);
        SideBySideDocument document = host.View.Document!;
        DiffLineNumberMargin numbers = host.Left.LineNumberMargin;
        ChangeMarkerMargin markers = host.Left.ChangeMarkerMargin;

        // Left line 1 pairs with right line 1; left line 2 sits opposite right line 3 after the
        // inserted using; left line 17 is deleted and has no counterpart.
        Assert.Equal("Line 1 · right line 1", numbers.TooltipFor(1));
        Assert.Equal("Line 2 · right line 3", numbers.TooltipFor(2));
        Assert.Equal(DiffLineKind.Deleted, host.Left.Metadata.KindOf(17));
        Assert.Equal("Line 17 · no right line", numbers.TooltipFor(17));
        Assert.Null(numbers.TooltipFor(999));

        // Right line 2 is the inserted using with no counterpart; right line 3 sits opposite left line 2.
        Assert.Equal("Line 2 · no left line", host.Right.LineNumberMargin.TooltipFor(2));
        Assert.Equal("Line 3 · left line 2", host.Right.LineNumberMargin.TooltipFor(3));

        Assert.Null(markers.TooltipFor(1));
        // Lines 17–21 are deleted; line 16 above them is modified, and the block holds both.
        ChangeBlock deletion = document.Blocks.Single(b => b.DeletedCount == 5);
        Assert.Equal(1, deletion.ModifiedCount);
        Assert.Equal($"Change {deletion.Index + 1} of {document.Blocks.Count} · +0 −5 ~1", markers.TooltipFor(17));
        Assert.Equal(markers.TooltipFor(17), markers.TooltipFor(16));
        ChangeBlock insertion = document.Blocks[0];
        Assert.Equal($"Change 1 of {document.Blocks.Count} · +1 −0 ~0", host.Right.ChangeMarkerMargin.TooltipFor(2));

        // The pointer over line 2's row on the line-number margin brings its tooltip up, and leaving clears it.
        using (WriteableBitmap _ = host.Capture())
        {
        }

        double lineHeight = host.Left.TextArea.TextView.DefaultLineHeight;
        double rowTop = host.Left.TextArea.TextView.GetVisualTopByDocumentLine(2) + Padding.Before(document, DiffSide.Left, 1) * lineHeight - host.Left.VerticalOffset;
        Point over = numbers.TranslatePoint(new Point(numbers.Bounds.Width / 2, rowTop + lineHeight / 2), host.Window) ?? throw new InvalidOperationException("margin not in tree");
        host.Window.MouseMove(over);
        CompositeHost.Layout();
        Assert.Equal("Line 2 · right line 3", ToolTip.GetTip(numbers));
        host.Window.MouseMove(new Point(host.Window.Width - 3, host.Window.Height - 3));
        CompositeHost.Layout();
        Assert.Null(ToolTip.GetTip(numbers));
    }
}
