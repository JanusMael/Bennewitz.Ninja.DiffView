using Avalonia;
using Avalonia.Headless;
using Avalonia.Headless.XUnit;
using Avalonia.Input;
using Avalonia.Markup.Xaml.Styling;
using Avalonia.Media;
using Avalonia.Media.Imaging;
using Bennewitz.Ninja.DiffView.Avalonia.Tests.Presenter;
using Bennewitz.Ninja.DiffView.Core;

namespace Bennewitz.Ninja.DiffView.Avalonia.Tests.Composite;

/// <summary>
/// Plan 00006 phase 2, copying a selection: which pane offers the arrow and on which row, what it
/// does to the cell a block arrow already wanted, and where the copied lines land — over the other
/// side's lines in the same rows, or, where that side has none there, inserted between them.
/// </summary>
public sealed class CopySelectionTests
{
    private static readonly Uri BaseUri = new("avares://DiffView.Avalonia.Tests/");

    [AvaloniaFact]
    public async Task A_selection_offers_an_arrow_on_its_first_row()
    {
        using CompositeHost host = new(width: 900, height: 400);
        host.Show();
        await host.LoadAsync("one\nTWO\nTHREE\nfour\n", "one\ntwo\nthree\nfour\n");
        host.View.RightReadOnly = false;
        CompositeHost.Layout();

        // Lines 2 and 3 whole: offset 4 is the start of line 2 and offset 13 the end of line 3.
        host.Left.Select(4, 9);
        host.Capture().Dispose();

        DiffLineNumberMargin margin = host.Left.LineNumberMargin;
        Assert.NotNull(margin.LastSelectionArrow);
        (Rect bounds, int overLine) = margin.LastSelectionArrow.Value;
        Assert.Equal(2, overLine);
        Assert.True(host.View.CanCopySelection(DiffSide.Left));

        // It sits where a number sits, flush with the column the numbers are aligned to, and the
        // number it displaced is the only one the frame is missing.
        Assert.Equal(margin.LastColumnRight, bounds.Right, 3);
        Assert.DoesNotContain(2, margin.LastSourceNumbers.Select(n => n.Left).OfType<int>());
        Assert.Contains(3, margin.LastSourceNumbers.Select(n => n.Left).OfType<int>());
    }

    [AvaloniaFact]
    public async Task Clearing_the_selection_takes_the_arrow_with_it()
    {
        using CompositeHost host = new(width: 900, height: 400);
        host.Show();
        await host.LoadAsync("one\nTWO\nTHREE\nfour\n", "one\ntwo\nthree\nfour\n");
        host.View.RightReadOnly = false;
        CompositeHost.Layout();

        host.Left.Select(4, 9);
        host.Capture().Dispose();
        Assert.NotNull(host.Left.LineNumberMargin.LastSelectionArrow);

        // Nothing else is touched — no scroll, no model, no option — and the margin hears about
        // it from `TextArea.SelectionChanged` rather than from the next redraw that happens along.
        // The notice is what is asserted, because the frame cannot tell the two apart: a headless
        // capture re-renders every visual whether or not it was invalidated.
        int notices = host.Left.LineNumberMargin.SelectionNotices;
        host.Left.TextArea.ClearSelection();
        Assert.True(host.Left.LineNumberMargin.SelectionNotices > notices, "clearing the selection never reached the margin");
        host.Capture().Dispose();
        Assert.Null(host.Left.LineNumberMargin.LastSelectionArrow);
        Assert.False(host.View.CanCopySelection(DiffSide.Left));

        // Line 2 is the block's anchor row as well, so the cell reverts to the block's arrow
        // rather than to its number: the contested cell is contested in both directions.
        Assert.Single(host.Left.LineNumberMargin.LastCopyArrows, a => a.OverLine == 2);
    }

    [AvaloniaFact]
    public async Task A_read_only_neighbour_offers_nothing()
    {
        using CompositeHost host = new(width: 900, height: 400);
        host.Show();
        await host.LoadAsync("one\nTWO\nTHREE\nfour\n", "one\ntwo\nthree\nfour\n");

        // Neither side editable, so there is nowhere for the selection to go — the same gate the
        // block arrow already passes through.
        host.Left.Select(4, 9);
        host.Capture().Dispose();
        Assert.Null(host.Left.LineNumberMargin.LastSelectionArrow);
        Assert.False(host.View.CanCopySelection(DiffSide.Left));
        Assert.False(host.View.CopySelection(DiffSide.Left));
        Assert.Equal("one\ntwo\nthree\nfour\n", host.Right.Document.Text);
    }

    [AvaloniaFact]
    public async Task The_selection_arrow_wins_the_contested_cell()
    {
        using CompositeHost host = new(width: 900, height: 400);
        host.Show();
        await host.LoadAsync("one\nTWO\nthree\n", "one\ntwo\nthree\n");
        host.View.RightReadOnly = false;
        CompositeHost.Layout();
        host.Capture().Dispose();

        // The one block is anchored on line 2, and that is where the selection begins too.
        DiffLineNumberMargin margin = host.Left.LineNumberMargin;
        (Rect blockZone, _, int? anchored) = Assert.Single(margin.LastCopyArrows);
        Assert.Equal(2, anchored);

        host.Left.Select(4, 3);
        host.Capture().Dispose();

        // One arrow in that cell, not two: the selection's is the more specific and the more
        // recent intent, so the block's is not drawn there at all.
        Assert.NotNull(margin.LastSelectionArrow);
        (Rect selectionZone, int overLine) = margin.LastSelectionArrow.Value;
        Assert.Equal(2, overLine);
        Assert.Equal(blockZone, selectionZone);
        Assert.Empty(margin.LastCopyArrows);

        // And the block's own copy is still there to be had, from the keyboard.
        Assert.True(host.View.CanCopyBlock(0, DiffSide.Right));
    }

    [AvaloniaFact]
    public async Task The_copy_takes_whole_lines()
    {
        using CompositeHost host = new(width: 900, height: 400);
        host.Show();
        await host.LoadAsync("one\nTWO\nTHREE\nfour\n", "one\ntwo\nthree\nfour\n");
        host.View.RightReadOnly = false;
        CompositeHost.Layout();

        // Inside line 2 to inside line 3: the model is line-based, so both lines go whole.
        host.Left.Select(5, 5);
        Assert.True(host.View.CopySelection(DiffSide.Left));
        await host.WaitForReDiffAsync();

        Assert.Equal("one\nTWO\nTHREE\nfour\n", host.Right.Document.Text);
        Assert.Equal(0, host.View.ChangeCount);
    }

    [AvaloniaFact]
    public async Task The_copy_lands_in_the_aligned_rows()
    {
        using CompositeHost host = new(width: 900, height: 400);
        host.Show();
        await host.LoadAsync("one\nTWO\nTHREE\nfour\n", "one\ntwo\nthree\nfour\n");
        host.View.RightReadOnly = false;
        CompositeHost.Layout();

        host.Left.Select(4, 9);
        Assert.True(host.View.CopySelection(DiffSide.Left));
        await host.WaitForReDiffAsync();

        // The right side's lines in those rows are replaced — line 4 is untouched, and the
        // re-diff collapses what the copy made identical.
        Assert.Equal("one\nTWO\nTHREE\nfour\n", host.Right.Document.Text);
        Assert.Equal(0, host.View.ChangeCount);
    }

    [AvaloniaFact]
    public async Task A_selection_over_padding_inserts()
    {
        using CompositeHost host = new(width: 900, height: 400);
        host.Show();
        await host.LoadAsync("a\nb\nc\nd\n", "a\nd\n");
        host.View.RightReadOnly = false;
        CompositeHost.Layout();

        // Lines 2 and 3 of the left sit in rows the right has no lines in at all, so there is
        // nothing to replace and the copy inserts between the right's own lines.
        host.Left.Select(2, 3);
        Assert.True(host.View.CopySelection(DiffSide.Left));
        await host.WaitForReDiffAsync();

        Assert.Equal("a\nb\nc\nd\n", host.Right.Document.Text);
        Assert.Equal(0, host.View.ChangeCount);
    }

    [AvaloniaFact]
    public async Task A_selection_reaching_the_next_line_s_first_column_stops_above_it()
    {
        using CompositeHost host = new(width: 900, height: 400);
        host.Show();
        await host.LoadAsync("one\nTWO\nTHREE\nfour\n", "one\ntwo\nthree\nfour\n");
        host.View.RightReadOnly = false;
        CompositeHost.Layout();

        // Offset 8 is line 3's first column. A drag that lands there means line 2 and nothing
        // more, which is what it means in every editor.
        host.Left.Select(4, 4);
        Assert.True(host.View.CopySelection(DiffSide.Left));
        await host.WaitForReDiffAsync();

        Assert.Equal("one\nTWO\nthree\nfour\n", host.Right.Document.Text);
        Assert.Equal(1, host.View.ChangeCount);
    }

    [AvaloniaFact]
    public async Task A_click_on_the_selection_arrow_copies_the_selection_out()
    {
        using CompositeHost host = new(width: 900, height: 400);
        host.Show();
        await host.LoadAsync("one\nTWO\nTHREE\nfour\n", "one\ntwo\nthree\nfour\n");
        host.View.RightReadOnly = false;
        CompositeHost.Layout();

        host.Left.Select(4, 9);
        host.Capture().Dispose();

        DiffLineNumberMargin margin = host.Left.LineNumberMargin;
        Assert.NotNull(margin.LastSelectionArrow);
        Point at = margin.TranslatePoint(margin.LastSelectionArrow.Value.Bounds.Center, host.Window)!.Value;

        // The pointer says it is clickable before it is clicked.
        host.Window.MouseMove(at);
        CompositeHost.Layout();
        Assert.Equal(StandardCursorType.Hand.ToString(), margin.Cursor?.ToString());

        host.Window.MouseDown(at, MouseButton.Left);
        host.Window.MouseUp(at, MouseButton.Left);
        await host.WaitForReDiffAsync();

        Assert.Equal("one\nTWO\nTHREE\nfour\n", host.Right.Document.Text);
        Assert.Equal(0, host.View.ChangeCount);
    }

    [AvaloniaFact]
    public async Task The_selection_arrow_s_tooltip_names_what_it_copies()
    {
        using CompositeHost host = new(width: 900, height: 400);
        host.Show();
        await host.LoadAsync("one\nTWO\nTHREE\nfour\n", "one\ntwo\nthree\nfour\n");
        host.View.RightReadOnly = false;
        CompositeHost.Layout();

        host.Left.Select(4, 9);
        host.Capture().Dispose();

        // The number the arrow displaced is one hover away, and the tooltip says what would
        // travel — the selected lines, not the block the row happens to sit in.
        string? tooltip = host.Left.LineNumberMargin.TooltipFor(2);
        Assert.NotNull(tooltip);
        Assert.Contains("2", tooltip, StringComparison.Ordinal);
        Assert.Contains("Copy the selected lines", tooltip, StringComparison.Ordinal);
        Assert.DoesNotContain("Copy the selected lines", host.Left.LineNumberMargin.TooltipFor(3)!, StringComparison.Ordinal);
    }

    [AvaloniaTheory]
    [InlineData("Default", false)]
    [InlineData("ColorBlind", true)]
    public async Task The_selection_arrow_carries_the_palette_s_share_of_shape(string palette, bool barExpected)
    {
        Application app = Application.Current!;
        ResourceInclude? colourBlind = palette == "ColorBlind"
            ? new ResourceInclude(BaseUri) { Source = DiffViewResources.ColorBlindTokensUri }
            : null;
        if (colourBlind is not null)
        {
            app.Resources.MergedDictionaries.Add(colourBlind);
        }

        try
        {
            using CompositeHost host = new(width: 900, height: 400);
            host.Show();

            // Two blocks, so a selection can take one arrow's cell and leave the other's alone:
            // the two glyphs are then in one frame, in one pane, pointing one way.
            await host.LoadAsync("one\nTWO\nthree\nFOUR\nfive\n", "one\ntwo\nthree\nfour\nfive\n");
            host.View.RightReadOnly = false;
            CompositeHost.Layout();
            host.Left.Select(4, 3);

            using WriteableBitmap frame = host.Capture();
            DiffLineNumberMargin margin = host.Left.LineNumberMargin;
            Point origin = margin.TranslatePoint(new Point(0, 0), host.Window)!.Value;
            Color gutter = PresenterHost.Token("DiffView.GutterBackgroundBrush");

            Assert.NotNull(margin.LastSelectionArrow);
            Rect selection = margin.LastSelectionArrow.Value.Bounds;
            (Rect block, _, _) = Assert.Single(margin.LastCopyArrows);

            // Colour discarded: how far the ink at the shaft's trailing end reaches. A plain
            // arrow leaves the shaft's own height there; a tail bar reaches the head's. Which one
            // the selection arrow gets is the palette's decision, carried by a single
            // transparent-or-not token, so one code path produces both readings — and the default
            // palette's reading is that the two glyphs are the same shape, told apart by colour.
            int blockRows = TailInkRows(frame, origin, block, gutter);
            int selectionRows = TailInkRows(frame, origin, selection, gutter);
            Assert.True(blockRows <= 6, $"the block arrow's tail inked {blockRows} rows");
            if (barExpected)
            {
                Assert.True(
                    selectionRows >= 8,
                    $"the {palette} selection arrow's tail inked {selectionRows} rows, against the block arrow's {blockRows}");
            }
            else
            {
                Assert.Equal(blockRows, selectionRows);
            }
        }
        finally
        {
            if (colourBlind is not null)
            {
                app.Resources.MergedDictionaries.Remove(colourBlind);
            }
        }
    }

    [AvaloniaFact]
    public async Task The_selection_arrow_is_painted_in_its_own_colours()
    {
        using CompositeHost host = new(width: 900, height: 400);
        host.Show();

        // Two blocks again, so both glyphs are in one frame and each can be read for the other's
        // colours as well as its own.
        await host.LoadAsync("one\nTWO\nthree\nFOUR\nfive\n", "one\ntwo\nthree\nfour\nfive\n");
        host.View.RightReadOnly = false;
        CompositeHost.Layout();
        host.Left.Select(4, 3);

        using WriteableBitmap frame = host.Capture();
        DiffLineNumberMargin margin = host.Left.LineNumberMargin;
        Point origin = margin.TranslatePoint(new Point(0, 0), host.Window)!.Value;
        Assert.NotNull(margin.LastSelectionArrow);
        Rect selection = margin.LastSelectionArrow.Value.Bounds;
        (Rect block, _, _) = Assert.Single(margin.LastCopyArrows);

        // Each arrow carries its own fill and neither carries the other's: the pair is told apart
        // by colour in the default palette, which is only true if the two colours are both there.
        Color selectionFill = PresenterHost.Token("DiffView.SelectionArrowFillBrush");
        Color blockFill = PresenterHost.Token("DiffView.GutterArrowFillBrush");
        Assert.True(Painted(frame, origin, selection, selectionFill) > 5, "the selection arrow painted too little of its own fill");
        Assert.True(Painted(frame, origin, block, blockFill) > 5, "the block arrow painted too little of its own fill");
        Assert.Equal(0, Painted(frame, origin, selection, blockFill));
        Assert.Equal(0, Painted(frame, origin, block, selectionFill));
    }

    private static int Painted(WriteableBitmap frame, Point origin, Rect area, Color colour)
    {
        PixelRect probe = PixelProbe.Inside(origin.X + area.Left, origin.Y + area.Top, origin.X + area.Right, origin.Y + area.Bottom, inset: 0);
        return PixelProbe.Count(frame, probe, c => PresenterHost.Near(c, colour));
    }

    /// <summary>
    /// How many rows of <paramref name="zone"/> carry ink in the strip at the shaft's trailing
    /// end, read as luminance against <paramref name="gutter"/> so no colour survives the reading.
    /// The pane is the left one, so its arrows point right and their tails are at the zone's left.
    /// </summary>
    private static int TailInkRows(WriteableBitmap frame, Point origin, Rect zone, Color gutter)
    {
        int tail = (int)Math.Round(origin.X + zone.Left + CopyArrowGlyph.InnerGap);
        int rows = 0;
        for (int y = (int)Math.Ceiling(origin.Y + zone.Top); y < (int)Math.Floor(origin.Y + zone.Bottom); y++)
        {
            PixelRect strip = new(tail - 1, y, 3, 1);
            if (PixelProbe.Count(frame, strip, c => Math.Abs(Luminance(c) - Luminance(gutter)) > 24) > 0)
            {
                rows++;
            }
        }

        return rows;
    }

    private static double Luminance(Color c) => (0.2126 * c.R) + (0.7152 * c.G) + (0.0722 * c.B);
}
