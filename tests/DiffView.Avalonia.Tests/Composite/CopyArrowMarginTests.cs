using Avalonia;
using Avalonia.Headless;
using Avalonia.Headless.XUnit;
using Avalonia.Input;
using Avalonia.Media;
using Avalonia.Media.Imaging;
using Bennewitz.Ninja.DiffView.Avalonia.Tests.Presenter;
using Bennewitz.Ninja.DiffView.Core;

namespace Bennewitz.Ninja.DiffView.Avalonia.Tests.Composite;

/// <summary>
/// Plan 00004, the copy arrow in the number cell: which pane carries it, which row it takes, the
/// blocks whose rows are padding on a side — where it costs no number at all — and, once §Phase 2
/// moved the copy here, what a click on it does and what the connector column has left.
/// </summary>
public sealed class CopyArrowMarginTests
{
    [AvaloniaFact]
    public async Task The_arrow_is_offered_by_the_side_it_would_copy_from()
    {
        (string left, string right) = CompositeHost.SmallFixture();
        using CompositeHost host = new(width: 900, height: 600);
        host.Show();
        await host.LoadAsync(left, right);

        // Neither side editable: nothing to receive a copy, so no arrow and every number stands.
        host.Capture().Dispose();
        Assert.Empty(host.Left.LineNumberMargin.LastCopyArrows);
        Assert.Empty(host.Right.LineNumberMargin.LastCopyArrows);

        // The right side becomes editable, so the *left* pane offers to send its blocks over.
        host.View.RightReadOnly = false;
        CompositeHost.Layout();
        host.Capture().Dispose();
        Assert.NotEmpty(host.Left.LineNumberMargin.LastCopyArrows);
        Assert.Empty(host.Right.LineNumberMargin.LastCopyArrows);

        // And the other way round.
        host.View.RightReadOnly = true;
        host.View.LeftReadOnly = false;
        CompositeHost.Layout();
        host.Capture().Dispose();
        Assert.Empty(host.Left.LineNumberMargin.LastCopyArrows);
        Assert.NotEmpty(host.Right.LineNumberMargin.LastCopyArrows);
    }

    [AvaloniaFact]
    public async Task A_side_editable_before_the_template_applies_is_wired_too()
    {
        (string left, string right) = CompositeHost.SmallFixture();
        using CompositeHost host = new(width: 900, height: 600);

        // Set before the template exists, so the property-change handler has no pane to reach and
        // the wiring falls to `AttachPane` — the path a host that sets the flag in XAML takes.
        host.View.RightReadOnly = false;
        host.Show();
        await host.LoadAsync(left, right);
        host.Capture().Dispose();

        Assert.True(host.Left.CanCopyOut);
        Assert.False(host.Right.CanCopyOut);
        Assert.NotEmpty(host.Left.LineNumberMargin.LastCopyArrows);
        Assert.Empty(host.Right.LineNumberMargin.LastCopyArrows);
    }

    [AvaloniaFact]
    public async Task One_arrow_per_block_on_the_block_s_first_line()
    {
        (string left, string right) = CompositeHost.SmallFixture();
        using CompositeHost host = new(width: 900, height: 600);
        host.Show();
        await host.LoadAsync(left, right);
        host.View.RightReadOnly = false;
        CompositeHost.Layout();
        host.Capture().Dispose();

        SideBySideDocument document = host.View.Document!;
        Assert.True(document.Blocks.Count > 1);

        // Every block the left side has lines in is anchored on the first of them.
        foreach (ChangeBlock block in document.Blocks)
        {
            LineRange mine = block.LinesFor(DiffSide.Left);
            (_, int index, int? overLine) = Assert.Single(host.Left.LineNumberMargin.LastCopyArrows, a => a.BlockIndex == block.Index);
            Assert.Equal(block.Index, index);
            Assert.Equal(mine.IsEmpty ? null : mine.Start + 1, overLine);
        }

        Assert.Equal(document.Blocks.Count, host.Left.LineNumberMargin.LastCopyArrows.Count);
    }

    [AvaloniaFact]
    public async Task The_only_numbers_missing_are_the_anchored_ones()
    {
        (string left, string right) = CompositeHost.SmallFixture();
        using CompositeHost host = new(width: 900, height: 600);
        host.Show();
        await host.LoadAsync(left, right);
        host.Capture().Dispose();

        // Every visible line's number, before any arrow exists.
        List<int> before = host.Left.LineNumberMargin.LastSourceNumbers
            .Select(n => n.Left)
            .OfType<int>()
            .ToList();
        Assert.NotEmpty(before);

        host.View.RightReadOnly = false;
        CompositeHost.Layout();
        host.Capture().Dispose();

        List<int> after = host.Left.LineNumberMargin.LastSourceNumbers
            .Select(n => n.Left)
            .OfType<int>()
            .ToList();
        int[] anchored = host.Left.LineNumberMargin.LastCopyArrows
            .Select(a => a.OverLine)
            .OfType<int>()
            .ToArray();

        Assert.NotEmpty(anchored);
        Assert.Equal(before.Except(anchored), after);
    }

    [AvaloniaFact]
    public async Task A_block_the_side_has_no_lines_in_puts_its_arrow_in_the_padding()
    {
        using CompositeHost host = new(width: 900, height: 400);
        host.Show();
        await host.LoadAsync("a\nb\nc\n", "a\nc\n");
        host.View.LeftReadOnly = false;
        host.View.RightReadOnly = false;
        CompositeHost.Layout();
        host.Capture().Dispose();

        // The left has the deleted line, so its arrow takes that line's number cell.
        (_, _, int? overLine) = Assert.Single(host.Left.LineNumberMargin.LastCopyArrows);
        Assert.Equal(2, overLine);

        // The right has no line there at all — the block's row is padding — so the arrow costs
        // it no number, and every number it has is still drawn.
        (_, int index, int? none) = Assert.Single(host.Right.LineNumberMargin.LastCopyArrows);
        Assert.Equal(0, index);
        Assert.Null(none);

        // Three numbers, not two: the trailing newline gives the document a final empty line.
        Assert.Equal([1, 2, 3], host.Right.LineNumberMargin.LastSourceNumbers.Select(n => n.Left).OfType<int>());
    }

    [AvaloniaFact]
    public async Task A_one_sided_block_at_the_very_end_is_reached_in_the_trailing_padding()
    {
        // No trailing newline on either side, so the right document's last line really is its
        // last: the block's row falls past it, in trailing padding.
        using CompositeHost host = new(width: 900, height: 400);
        host.Show();
        await host.LoadAsync("a\nb", "a");
        host.View.LeftReadOnly = false;
        host.View.RightReadOnly = false;
        CompositeHost.Layout();
        host.Capture().Dispose();

        // The block is past the right side's last line, where a walk over the visual lines never
        // reaches it.
        (_, int index, int? none) = Assert.Single(host.Right.LineNumberMargin.LastCopyArrows);
        Assert.Equal(0, index);
        Assert.Null(none);
        Assert.Equal([1], host.Right.LineNumberMargin.LastSourceNumbers.Select(n => n.Left).OfType<int>());
    }

    [AvaloniaFact]
    public async Task The_arrow_costs_the_margin_no_width()
    {
        (string left, string right) = CompositeHost.SmallFixture();
        using CompositeHost host = new(width: 900, height: 600);
        host.Show();
        await host.LoadAsync(left, right);
        host.Capture().Dispose();
        double before = host.Left.LineNumberMargin.Bounds.Width;

        host.View.RightReadOnly = false;
        CompositeHost.Layout();
        host.Capture().Dispose();

        // The premise of drawing over the number rather than beside it: the cell is already
        // wider than the glyph, so nothing grows and no horizontal space is spent.
        Assert.NotEmpty(host.Left.LineNumberMargin.LastCopyArrows);
        Assert.Equal(before, host.Left.LineNumberMargin.Bounds.Width);
        Assert.True(before >= CopyArrowGlyph.Size, $"the number cell is {before} px against a {CopyArrowGlyph.Size} px glyph");
    }

    [AvaloniaFact]
    public async Task A_click_on_the_arrow_copies_the_block_out()
    {
        using CompositeHost host = new(width: 900, height: 400);
        host.Show();
        await host.LoadAsync("one\nTWO\nthree\n", "one\ntwo\nthree\n");
        host.View.RightReadOnly = false;
        CompositeHost.Layout();
        host.Capture().Dispose();

        (Rect zone, _, _) = Assert.Single(host.Left.LineNumberMargin.LastCopyArrows);
        Point at = host.Left.LineNumberMargin.TranslatePoint(zone.Center, host.Window)!.Value;
        host.Window.MouseDown(at, MouseButton.Left);
        host.Window.MouseUp(at, MouseButton.Left);
        await host.WaitForReDiffAsync();

        // The arrow is in the left pane, so the copy lands on the right.
        Assert.Equal("one\nTWO\nthree\n", host.Right.Document.Text);
        Assert.Equal(0, host.View.ChangeCount);
    }

    [AvaloniaFact]
    public async Task A_click_on_a_number_still_only_moves_the_caret()
    {
        using CompositeHost host = new(width: 900, height: 400);
        host.Show();
        await host.LoadAsync("one\nTWO\nthree\n", "one\ntwo\nthree\n");
        host.View.RightReadOnly = false;
        CompositeHost.Layout();
        host.Capture().Dispose();

        // Line 3's cell: a number, not an arrow. The two never overlap, but the order in which
        // they are tested is what decides this, so it is pinned rather than left to reading order.
        (Rect zone, _, _) = Assert.Single(host.Left.LineNumberMargin.LastCopyArrows);
        Point at = host.Left.LineNumberMargin.TranslatePoint(zone.Center, host.Window)!.Value;
        Point below = at + new Point(0, host.Left.TextArea.TextView.DefaultLineHeight);
        Assert.Null(host.Left.LineNumberMargin.ArrowAt(zone.Center + new Point(0, host.Left.TextArea.TextView.DefaultLineHeight)));

        host.Window.MouseDown(below, MouseButton.Left);
        host.Window.MouseUp(below, MouseButton.Left);
        await host.WaitForReDiffAsync();

        Assert.Equal("one\ntwo\nthree\n", host.Right.Document.Text);
        Assert.Equal(1, host.View.ChangeCount);
        Assert.Equal(3, host.Left.TextArea.Caret.Line);
    }

    [AvaloniaFact]
    public async Task The_pointer_says_the_arrow_is_clickable()
    {
        using CompositeHost host = new(width: 900, height: 400);
        host.Show();
        await host.LoadAsync("one\nTWO\nthree\n", "one\ntwo\nthree\n");
        host.View.RightReadOnly = false;
        CompositeHost.Layout();
        host.Capture().Dispose();

        DiffLineNumberMargin margin = host.Left.LineNumberMargin;
        (Rect zone, _, _) = Assert.Single(margin.LastCopyArrows);
        double rowHeight = host.Left.TextArea.TextView.DefaultLineHeight;

        // Over the arrow: a hand. A line number is not clickable in the same way, and the margin
        // inherits the pane's own cursor there rather than claiming one.
        host.Window.MouseMove(margin.TranslatePoint(zone.Center, host.Window)!.Value);
        CompositeHost.Layout();
        Assert.Equal(StandardCursorType.Hand.ToString(), margin.Cursor?.ToString());

        host.Window.MouseMove(margin.TranslatePoint(zone.Center + new Point(0, rowHeight), host.Window)!.Value);
        CompositeHost.Layout();
        Assert.Null(margin.Cursor);
    }

    [AvaloniaFact]
    public async Task The_connector_column_has_no_arrows_left()
    {
        (string left, string right) = CompositeHost.SmallFixture();
        using CompositeHost host = new(width: 900, height: 600);
        host.Show();
        await host.LoadAsync(left, right);
        host.View.LeftReadOnly = false;
        host.View.RightReadOnly = false;
        CompositeHost.Layout();

        using WriteableBitmap frame = host.Capture();
        ChangeConnectorGutter gutter = host.View.Gutter!;
        Point origin = gutter.TranslatePoint(new Point(0, 0), host.Window)!.Value;
        Color arrowColour = PresenterHost.Token("DiffView.GutterArrowBrush");
        PixelRect column = PixelProbe.Inside(origin.X, origin.Y, origin.X + gutter.Bounds.Width, origin.Y + gutter.Bounds.Height, inset: 0);

        // Both sides editable, so this is the state that used to paint two arrows per block.
        Assert.NotEmpty(host.Left.LineNumberMargin.LastCopyArrows);
        Assert.NotEmpty(host.Right.LineNumberMargin.LastCopyArrows);
        Assert.Equal(0, PixelProbe.Count(frame, column, c => PresenterHost.Near(c, arrowColour)));
        Assert.Equal(16, gutter.Bounds.Width);
    }

    [AvaloniaFact]
    public async Task The_anchored_row_s_tooltip_carries_the_number_it_stands_in_for()
    {
        using CompositeHost host = new(width: 900, height: 400);
        host.Show();
        await host.LoadAsync("one\nTWO\nthree\n", "one\ntwo\nthree\n");
        host.View.RightReadOnly = false;
        CompositeHost.Layout();
        host.Capture().Dispose();

        (_, _, int? overLine) = Assert.Single(host.Left.LineNumberMargin.LastCopyArrows);
        Assert.Equal(2, overLine);

        // The one number not on screen is still one hover away, and the tooltip says what the
        // arrow in its place would do.
        string? tooltip = host.Left.LineNumberMargin.TooltipFor(2);
        Assert.NotNull(tooltip);
        Assert.Contains("2", tooltip, StringComparison.Ordinal);
        Assert.Contains("Copy this change", tooltip, StringComparison.Ordinal);

        // A row with no arrow says nothing about copying.
        Assert.DoesNotContain("Copy this change", host.Left.LineNumberMargin.TooltipFor(3)!, StringComparison.Ordinal);
    }
}
