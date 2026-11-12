using Avalonia;
using Avalonia.Controls;
using Avalonia.Headless.XUnit;
using Avalonia.Media;
using Avalonia.Media.Imaging;
using AvaloniaEdit.Rendering;
using Bennewitz.Ninja.DiffView.Core;

namespace Bennewitz.Ninja.DiffView.Tests.Presenter;

/// <summary>
/// Plan 00035 step 2, the presenter's emphasis: the range of a side's lines a host marks, which
/// <see cref="EmphasisRenderer"/> shades row by row. Where the shade lands is read against the height tree and the
/// model's padding, which the renderer does not choose, and against a frame drawn before the range was set.
/// </summary>
public sealed class EmphasisTests
{
    private const double Tolerance = PresenterHost.Tolerance;

    [AvaloniaFact]
    public void The_shade_covers_exactly_the_marked_lines_rows_and_no_padding_row()
    {
        TestLogSink.Instance.Clear();

        // Wide enough that the pixel each row is sampled at lies clear of its text.
        using PresenterHost host = PresenterHost.Small(width: 1200);
        host.Show();
        DiffPanePresenter pane = host.Left;
        TextView view = pane.TextArea.TextView;

        // Lines 7 to 9 of the left: line 8 carries a padding row above it, for the line the right inserts there, and
        // line 9 is modified.
        Assert.Equal(0, Padding.Before(host.Document, DiffSide.Left, 6));
        Assert.Equal(1, Padding.Before(host.Document, DiffSide.Left, 7));
        Assert.Equal(0, Padding.Before(host.Document, DiffSide.Left, 8));
        Assert.Equal(DiffLineKind.Modified, pane.Metadata.KindOf(9));

        // Drawn on the background layer, right after the row fills: over the tints and the current block's border,
        // under the find matches, the selection and the text. Under a tint or over it, a marked row differs by a few
        // levels a channel, which no pixel here can tell from rounding, so the order is read where it is kept.
        Assert.Equal(KnownLayer.Background, pane.EmphasisRenderer.Layer);
        int fills = view.BackgroundRenderers.IndexOf(pane.BackgroundRenderer);
        int emphasis = view.BackgroundRenderers.IndexOf(pane.EmphasisRenderer);
        Assert.True(emphasis == fills + 1, $"the emphasis is background renderer {emphasis}, and the row fills are {fills}");

        using WriteableBitmap before = host.Capture();
        pane.Emphasis = new LineRange(6, 3);
        using WriteableBitmap after = host.Capture();

        AssertShaded(pane, [7, 8, 9], "lines 7 to 9");
        Rect[] rows = AssertRows(host, pane, [7, 8, 9]);

        // Each marked row carries the shade over what was there: the pane on lines 7 and 8, the modified tint on 9.
        double lineHeight = view.DefaultLineHeight;
        double width = view.Bounds.Width;
        Point origin = host.ToWindow(pane, new Point(0, 0));
        Color shade = PresenterHost.Token("DiffView.EmphasisBrush");
        Color paneBackground = PresenterHost.Token("DiffView.PaneBackgroundBrush");
        Color[] grounds = [paneBackground, paneBackground, PresenterHost.Composite(PresenterHost.Token("DiffView.ModifiedBrush"), paneBackground)];
        int x = (int)(origin.X + width - 8);
        foreach ((Rect row, Color ground) in rows.Zip(grounds))
        {
            int y = (int)(origin.Y + row.Top + lineHeight / 2);
            Color unmarked = PixelProbe.At(before, x, y);
            Assert.True(PresenterHost.Near(unmarked, ground), $"the row at {row.Top} is sampled on {unmarked}, not on its bare ground {ground}");
            Color expected = PresenterHost.Composite(shade, unmarked);
            Color marked = PixelProbe.At(after, x, y);
            Assert.True(PresenterHost.Near(marked, expected), $"the row at {row.Top}: expected {expected}, the shade over {unmarked}, got {marked}");
        }

        // Nothing else moved: the text above the range, the padding row between lines 7 and 8, the text below the
        // range, and the gutter beside it.
        Point paneOrigin = host.ToWindow((Control)pane, new Point(0, 0));
        PixelRect[] untouched =
        [
            PixelProbe.Inside(origin.X, origin.Y, origin.X + width, origin.Y + rows[0].Top),
            PixelProbe.Inside(origin.X, origin.Y + rows[0].Bottom, origin.X + width, origin.Y + rows[1].Top),
            PixelProbe.Inside(origin.X, origin.Y + rows[2].Bottom, origin.X + width, origin.Y + view.Bounds.Height),
            PixelProbe.Inside(paneOrigin.X, origin.Y + rows[0].Top, origin.X, origin.Y + rows[2].Bottom),
        ];
        foreach (PixelRect area in untouched)
        {
            Assert.True(area.Width > 0 && area.Height > 0, $"{area} is empty, so it would show nothing");
            (int count, PixelPoint? first) = PixelProbe.Differing(before, after, area);
            Assert.True(count == 0, $"{count} pixels moved in {area}, the first at {first}");
        }

        // The rows move with the scroll.
        PresenterHost.ScrollTo(pane, 2.5 * lineHeight);
        using (WriteableBitmap _ = host.Capture())
        {
        }

        Assert.True(view.ScrollOffset.Y > lineHeight, $"the pane scrolled to {view.ScrollOffset.Y}, less than a row");
        AssertRows(host, pane, [7, 8, 9]);

        Assert.Empty(pane.Faults);
        host.Dispose();
        TestLogSink.AssertNoWarnings();
    }

    [AvaloniaFact]
    public void A_range_past_the_end_shades_the_lines_there_are_and_throws_nothing()
    {
        TestLogSink.Instance.Clear();

        // Tall enough that every line of the fixture is a visual line.
        using PresenterHost host = PresenterHost.Small(height: 1000);
        host.Show();
        DiffPanePresenter pane = host.Left;
        int lines = pane.Document.LineCount;

        pane.Emphasis = new LineRange(lines - 2, 10);
        using (WriteableBitmap _ = host.Capture())
        {
        }

        Assert.Equal(lines, pane.TextArea.TextView.VisualLines[^1].LastDocumentLine.LineNumber);
        AssertShaded(pane, [lines - 1, lines], "a range running past the end");

        pane.Emphasis = new LineRange(lines - 2, int.MaxValue);
        using (WriteableBitmap _ = host.Capture())
        {
        }

        AssertShaded(pane, [lines - 1, lines], "a count carrying the end past int.MaxValue");

        pane.Emphasis = new LineRange(lines + 5, 3);
        using (WriteableBitmap _ = host.Capture())
        {
        }

        AssertShaded(pane, [], "a range wholly past the end");
        Assert.Empty(pane.Faults);
        host.Dispose();
        TestLogSink.AssertNoWarnings();
    }

    [AvaloniaFact]
    public void A_throwing_renderer_reports_once_disables_itself_and_a_new_model_turns_it_back_on()
    {
        TestLogSink.Instance.Clear();
        using PresenterHost host = PresenterHost.Small();
        host.Show();
        DiffPanePresenter pane = host.Left;
        List<RenderFaultEventArgs> faults = [];
        pane.RenderFault += (_, e) => faults.Add(e);
        pane.Emphasis = new LineRange(0, 3);
        int asked = 0;
        pane.EmphasisSourceForTesting = () =>
        {
            asked++;
            throw new InvalidOperationException("deliberate emphasis fault");
        };

        using (WriteableBitmap _ = host.Capture())
        {
        }

        pane.TextArea.TextView.Redraw();
        using WriteableBitmap second = host.Capture();

        Assert.Equal(1, asked);
        Assert.True(pane.EmphasisRenderer.IsDisabled);
        RenderFaultEventArgs fault = Assert.Single(faults);
        Assert.True(fault.Source == nameof(EmphasisRenderer), $"the fault names {fault.Source}");
        Assert.IsType<InvalidOperationException>(fault.Exception);
        Assert.True(pane.IsDegraded);
        Assert.Empty(pane.EmphasisRenderer.LastDrawn);

        // The rest of the pane still draws: the row tints, and line 1's glyphs.
        Assert.NotEmpty(pane.BackgroundRenderer.LastDrawn);
        Point origin = host.ToWindow(pane, new Point(0, 0));
        double lineHeight = pane.TextArea.TextView.DefaultLineHeight;
        PixelRect firstRow = PixelProbe.Inside(origin.X, origin.Y, origin.X + 150, origin.Y + lineHeight);
        Assert.True(PixelProbe.Count(second, firstRow, PixelProbe.IsDark) > 0, "line 1's glyphs should still render");

        // A new model gives the renderer another chance, and the range, which is the host's, is still there to shade.
        pane.EmphasisSourceForTesting = null;
        pane.DiffDocument = DiffDocumentBuilder.Build("one", "two", CancellationToken.None).Document;
        pane.DiffDocument = host.Document;
        using (WriteableBitmap _ = host.Capture())
        {
        }

        Assert.False(pane.EmphasisRenderer.IsDisabled, "a new model left the emphasis renderer disabled");
        Assert.Empty(pane.Faults);
        AssertShaded(pane, [1, 2, 3], "lines 1 to 3, after the new model");
        host.Dispose();
        TestLogSink.AssertNoWarnings();
    }

    [AvaloniaFact]
    public void A_fold_reaching_into_the_range_shades_its_placeholders_row()
    {
        TestLogSink.Instance.Clear();
        using PresenterHost host = PresenterHost.Small();
        host.Show();
        DiffPanePresenter pane = host.Left;
        TextView view = pane.TextArea.TextView;

        // Lines 4 to 6 of the left fold behind the placeholder at the end of line 3, and the range is line 5 alone,
        // inside the fold.
        Assert.Equal(1, pane.SetCollapsedLines([(4, 6)]));
        pane.Emphasis = new LineRange(4, 1);
        using (WriteableBitmap _ = host.Capture())
        {
        }

        IReadOnlyList<EmphasisRow> drawn = pane.EmphasisRenderer.LastDrawn;
        Assert.True(
            drawn is [{ FirstLineNumber: 3, LastLineNumber: 6 }],
            $"a range inside the fold shaded {string.Join("; ", drawn.Select(r => $"lines {r.FirstLineNumber} to {r.LastLineNumber}"))}, not the fold's row, lines 3 to 6");
        Assert.Equal(host.RowTopOfLine(pane, 3) - view.ScrollOffset.Y, drawn[0].Rect.Top, Tolerance);
        Assert.Equal(view.DefaultLineHeight, drawn[0].Rect.Height, Tolerance);

        // The line after the fold is a row of its own, and a range of it alone leaves the fold's row unshaded.
        pane.Emphasis = new LineRange(6, 1);
        using (WriteableBitmap _ = host.Capture())
        {
        }

        AssertShaded(pane, [7], "line 7, after the fold");
        Assert.Empty(pane.Faults);
        host.Dispose();
        TestLogSink.AssertNoWarnings();
    }

    /// <summary>The first lines of the visual lines the last frame shaded are exactly <paramref name="expected"/>.</summary>
    private static void AssertShaded(DiffPanePresenter pane, int[] expected, string what)
    {
        int[] shaded = [.. pane.EmphasisRenderer.LastDrawn.Select(r => r.FirstLineNumber)];
        Assert.True(
            shaded.SequenceEqual(expected),
            $"the range of {what} shaded lines [{string.Join(", ", shaded)}] where it should shade [{string.Join(", ", expected)}]");
    }

    /// <summary>
    /// Each line's shade is its own row, read from the height tree and the model's padding: one line height tall below
    /// any padding above the line, across the text view, at the scroll the view is at. Returns the rows.
    /// </summary>
    private static Rect[] AssertRows(PresenterHost host, DiffPanePresenter pane, int[] lineNumbers)
    {
        TextView view = pane.TextArea.TextView;
        double scroll = view.ScrollOffset.Y;
        Rect[] rows = [.. lineNumbers.Select(n => new Rect(0, host.RowTopOfLine(pane, n) - scroll, view.Bounds.Width, view.DefaultLineHeight))];
        foreach ((EmphasisRow drawn, Rect expected) in pane.EmphasisRenderer.LastDrawn.Zip(rows))
        {
            Rect shaded = drawn.Rect;
            Assert.True(
                Math.Abs(shaded.Left - expected.Left) < Tolerance && Math.Abs(shaded.Top - expected.Top) < Tolerance
                    && Math.Abs(shaded.Width - expected.Width) < Tolerance && Math.Abs(shaded.Height - expected.Height) < Tolerance,
                $"line {drawn.FirstLineNumber} is shaded at {shaded}, and its row is {expected}");
        }

        return rows;
    }
}
