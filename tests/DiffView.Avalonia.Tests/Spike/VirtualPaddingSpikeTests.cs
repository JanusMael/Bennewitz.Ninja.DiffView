using System.Diagnostics;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Headless;
using Avalonia.Headless.XUnit;
using Avalonia.Input;
using Avalonia.Media;
using Avalonia.Media.Imaging;
using Avalonia.Media.TextFormatting;
using AvaloniaEdit;
using AvaloniaEdit.Document;
using AvaloniaEdit.Editing;
using AvaloniaEdit.Rendering;

namespace Bennewitz.Ninja.DiffView.Tests.Spike;

/// <summary>
/// Plan 00001, Phase 1: the virtual-padding spike, against two plain <see cref="TextEditor"/>s.
/// Each test is one numbered item of the phase. The mechanism under test lives in
/// <c>Padding.cs</c> and <c>TextBandRenderers.cs</c> beside this file; all of it is throwaway,
/// and the go / no-go it produces is recorded in <c>DECISIONS.md</c>.
/// </summary>
public sealed class VirtualPaddingSpikeTests
{
    private const double Tolerance = 1e-6;

    /// <summary>
    /// Rows 1–30 are shared, so every padding gap lies below the first viewport of a 300 px
    /// window. Left: 94 lines, 3 rows of padding above line 31 and 2 above line 76. Right: 90
    /// lines, 5 rows above line 54 and 4 trailing rows after line 90. Both sides reach 99 rows.
    /// </summary>
    private static AlignmentFixture SmallFixture()
    {
        return AlignmentFixture.Build(
            new Same(30), new RightOnly(3), new Same(20), new LeftOnly(5),
            new Same(20), new RightOnly(2), new Same(15), new LeftOnly(4));
    }

    [AvaloniaFact]
    public void Item1_padding_run_pads_above_a_line_and_after_the_last_line_by_whole_rows()
    {
        using SpikeHost host = new(SmallFixture());
        host.Show();
        host.Prime(host.Left);
        host.Prime(host.Right);

        TextView leftView = host.Left.TextArea.TextView;
        TextView rightView = host.Right.TextArea.TextView;
        double lineHeight = leftView.DefaultLineHeight;
        PaddingMetrics metrics = MetricsOf(host.Left);
        Assert.Equal(lineHeight, rightView.DefaultLineHeight, Tolerance);
        Assert.True(metrics.HalfSlack > 0, "LineHeightFactor above 1 is assumed: text is centred in its row.");

        // A plain line is one row tall.
        VisualLine plain = leftView.GetOrConstructVisualLine(host.Left.Document.GetLineByNumber(1));
        Assert.Equal(lineHeight, plain.Height, Tolerance);
        Assert.Equal(metrics.HalfSlack, Y(plain, VisualYPosition.TextTop) - Y(plain, VisualYPosition.LineTop), Tolerance);

        // Three rows of padding above line 31: four rows tall, text in the last row, centred as a plain line's.
        VisualLine above = leftView.GetOrConstructVisualLine(host.Left.Document.GetLineByNumber(31));
        Assert.Single(above.TextLines);
        Assert.Equal(4 * lineHeight, above.Height, Tolerance);
        Assert.Equal(3 * lineHeight + metrics.HalfSlack, Y(above, VisualYPosition.TextTop) - Y(above, VisualYPosition.LineTop), Tolerance);
        Assert.Equal(metrics.TextHeight, Y(above, VisualYPosition.TextBottom) - Y(above, VisualYPosition.TextTop), Tolerance);
        Assert.Equal(metrics.HalfSlack, Y(above, VisualYPosition.LineBottom) - Y(above, VisualYPosition.TextBottom), Tolerance);

        // Four trailing rows after the right's last line: five rows tall, text in the first row.
        VisualLine trailing = rightView.GetOrConstructVisualLine(host.Right.Document.GetLineByNumber(90));
        Assert.Equal(5 * lineHeight, trailing.Height, Tolerance);
        Assert.Equal(metrics.HalfSlack, Y(trailing, VisualYPosition.TextTop) - Y(trailing, VisualYPosition.LineTop), Tolerance);
        Assert.Equal(4 * lineHeight + metrics.HalfSlack, Y(trailing, VisualYPosition.LineBottom) - Y(trailing, VisualYPosition.TextBottom), Tolerance);

        // Pixels agree: glyphs sit in the text row, the padding rows are blank.
        SpikeHost.ScrollTo(host.Left, SpikeHost.TopOfLine(host.Left, 31));
        host.Right.ScrollToEnd();
        using WriteableBitmap frame = host.Capture();

        Point leftOrigin = host.ToWindow(host.Left, new Point(0, 0));
        double top31 = SpikeHost.TopOfLine(host.Left, 31) - host.Left.VerticalOffset;
        Assert.Equal(0, top31, Tolerance);
        PixelRect leftPaddingRows = PixelProbe.Inside(leftOrigin.X, leftOrigin.Y + top31, leftOrigin.X + 150, leftOrigin.Y + top31 + 3 * lineHeight);
        PixelRect leftTextRow = PixelProbe.Inside(leftOrigin.X, leftOrigin.Y + top31 + 3 * lineHeight, leftOrigin.X + 150, leftOrigin.Y + top31 + 4 * lineHeight);
        Assert.Equal(0, PixelProbe.Count(frame, leftPaddingRows, PixelProbe.IsDark));
        Assert.True(PixelProbe.Count(frame, leftTextRow, PixelProbe.IsDark) > 0, "line 31's glyphs should render in the row after its padding");

        Point rightOrigin = host.ToWindow(host.Right, new Point(0, 0));
        double top90 = SpikeHost.TopOfLine(host.Right, 90) - host.Right.VerticalOffset;
        Assert.True(top90 >= 0 && top90 + 5 * lineHeight <= host.Window.Height + Tolerance, $"line 90's five rows should fit the viewport; top is {top90}");
        PixelRect rightTextRow = PixelProbe.Inside(rightOrigin.X, rightOrigin.Y + top90, rightOrigin.X + 150, rightOrigin.Y + top90 + lineHeight);
        PixelRect rightPaddingRows = PixelProbe.Inside(rightOrigin.X, rightOrigin.Y + top90 + lineHeight, rightOrigin.X + 150, rightOrigin.Y + top90 + 5 * lineHeight);
        Assert.True(PixelProbe.Count(frame, rightTextRow, PixelProbe.IsDark) > 0, "line 90's glyphs should render in its first row");
        Assert.Equal(0, PixelProbe.Count(frame, rightPaddingRows, PixelProbe.IsDark));
    }

    [AvaloniaFact]
    public void Item2_caret_skips_the_padding_element_and_a_click_in_padding_lands_on_the_adjacent_line()
    {
        using SpikeHost host = new(SmallFixture());
        host.Show();
        host.Prime(host.Left);
        host.Prime(host.Right);

        TextEditor editor = host.Left;
        TextArea area = editor.TextArea;
        Caret caret = area.Caret;
        DocumentLine line30 = editor.Document.GetLineByNumber(30);
        DocumentLine line31 = editor.Document.GetLineByNumber(31);
        double lineHeight = area.TextView.DefaultLineHeight;

        SpikeHost.ScrollTo(editor, SpikeHost.TopOfLine(editor, 30));
        area.Focus();
        SpikeHost.Layout();
        Assert.True(area.IsFocused, "the text area should take keyboard focus");

        // Right from the end of line 30 enters line 31 at its first text column, and every press moves one character.
        caret.Offset = line30.EndOffset;
        Press(host, Key.Right, PhysicalKey.ArrowRight);
        Assert.Equal(new TextLocation(31, 1), caret.Location);
        Assert.Equal(1, caret.Position.VisualColumn);
        Press(host, Key.Right, PhysicalKey.ArrowRight);
        Assert.Equal(new TextLocation(31, 2), caret.Location);
        Assert.Equal(2, caret.Position.VisualColumn);
        Press(host, Key.Left, PhysicalKey.ArrowLeft);
        Assert.Equal(new TextLocation(31, 1), caret.Location);
        Press(host, Key.Left, PhysicalKey.ArrowLeft);
        Assert.Equal(new TextLocation(30, line30.Length + 1), caret.Location);

        // Down and Up cross the padding as one row of caret movement.
        caret.Offset = line30.Offset + 3;
        Press(host, Key.Down, PhysicalKey.ArrowDown);
        Assert.Equal(31, caret.Line);
        Press(host, Key.Up, PhysicalKey.ArrowUp);
        Assert.Equal(30, caret.Line);

        // End and Home on the padded line land on real text columns.
        caret.Offset = line31.Offset + 3;
        Press(host, Key.End, PhysicalKey.End);
        Assert.Equal(new TextLocation(31, line31.Length + 1), caret.Location);
        Assert.Equal(line31.Length + 1, caret.Position.VisualColumn);
        Press(host, Key.Home, PhysicalKey.Home);
        Assert.Equal(new TextLocation(31, 1), caret.Location);
        Assert.Equal(1, caret.Position.VisualColumn);

        // A click in the padding rows above line 31 puts the caret on line 31.
        double top31 = SpikeHost.TopOfLine(editor, 31) - editor.VerticalOffset;
        Click(host, editor, new Point(10, top31 + 1.5 * lineHeight));
        Assert.Equal(31, caret.Line);
        Assert.True(caret.Position.VisualColumn >= 1, "the caret never sits on the padding column");

        // A click at the left edge of the text row lands on the first text column, not the padding column.
        Click(host, editor, new Point(0, top31 + 3.5 * lineHeight));
        Assert.Equal(new TextLocation(31, 1), caret.Location);
        Assert.Equal(1, caret.Position.VisualColumn);

        // A click in the trailing padding after the right's last line puts the caret on that line.
        host.Right.ScrollToEnd();
        host.Right.TextArea.Focus();
        SpikeHost.Layout();
        double top90 = SpikeHost.TopOfLine(host.Right, 90) - host.Right.VerticalOffset;
        Click(host, host.Right, new Point(10, top90 + 2.5 * lineHeight));
        Assert.Equal(90, host.Right.TextArea.Caret.Line);
    }

    [AvaloniaFact]
    public void Item3_height_priming_equalises_extents_before_scrolling_and_survives_redraw_document_swap_and_font_change()
    {
        using SpikeHost host = new(SmallFixture());
        host.Show();
        TextView leftView = host.Left.TextArea.TextView;
        TextView rightView = host.Right.TextArea.TextView;
        double lineHeight = leftView.DefaultLineHeight;
        int rows = host.Fixture.RowCount;
        Assert.Equal(99, rows);

        // Before priming the height tree only knows the lines that have been rendered: the extents differ.
        Assert.Equal(0, host.Left.VerticalOffset, Tolerance);
        Assert.Equal(0, host.Right.VerticalOffset, Tolerance);
        Assert.Equal(94 * lineHeight, leftView.DocumentHeight, Tolerance);
        Assert.Equal(90 * lineHeight, rightView.DocumentHeight, Tolerance);
        Assert.NotEqual(host.Left.ExtentHeight, host.Right.ExtentHeight);

        // Priming every padded line makes both extents the row count, with no scrolling having happened.
        Assert.Equal(2, host.Prime(host.Left));
        Assert.Equal(2, host.Prime(host.Right));
        Assert.Equal(0, host.Left.VerticalOffset, Tolerance);
        Assert.Equal(0, host.Right.VerticalOffset, Tolerance);
        Assert.Equal(rows * lineHeight, leftView.DocumentHeight, Tolerance);
        Assert.Equal(rows * lineHeight, rightView.DocumentHeight, Tolerance);
        Assert.Equal(rows * lineHeight, host.Left.ExtentHeight, Tolerance);
        Assert.Equal(rows * lineHeight, host.Right.ExtentHeight, Tolerance);
        AssertRowsAligned(host);

        // Redraw drops the visual lines but keeps the heights.
        leftView.Redraw();
        rightView.Redraw();
        SpikeHost.Layout();
        Assert.True(leftView.VisualLinesValid);
        Assert.Equal(rows * lineHeight, leftView.DocumentHeight, Tolerance);
        Assert.Equal(rows * lineHeight, rightView.DocumentHeight, Tolerance);
        AssertRowsAligned(host);

        // A document swap recreates the height tree; re-priming restores equality.
        host.Left.Document = new TextDocument(host.Fixture.LeftText);
        SpikeHost.Layout();
        leftView = host.Left.TextArea.TextView;
        Assert.Equal(94 * lineHeight, leftView.DocumentHeight, Tolerance);
        host.Prime(host.Left);
        Assert.Equal(rows * lineHeight, leftView.DocumentHeight, Tolerance);
        Assert.Equal(host.Right.ExtentHeight, host.Left.ExtentHeight, Tolerance);
        AssertRowsAligned(host);

        // A font change rebases every default-height line but leaves padded lines stale; re-priming restores equality.
        host.Left.FontSize = 18;
        host.Right.FontSize = 18;
        SpikeHost.Layout();
        double newLineHeight = leftView.DefaultLineHeight;
        Assert.NotEqual(lineHeight, newLineHeight);
        Assert.Equal(newLineHeight, rightView.DefaultLineHeight, Tolerance);
        Assert.True(Math.Abs(rows * newLineHeight - leftView.DocumentHeight) > 1, "padded lines should still carry their old heights after a font change");
        host.Prime(host.Left);
        host.Prime(host.Right);
        Assert.Equal(rows * newLineHeight, leftView.DocumentHeight, Tolerance);
        Assert.Equal(rows * newLineHeight, rightView.DocumentHeight, Tolerance);
        Assert.Equal(host.Left.ExtentHeight, host.Right.ExtentHeight, Tolerance);
        AssertRowsAligned(host);
    }

    [AvaloniaFact]
    public void Item4_selection_and_caret_drawn_from_text_extents_paint_only_text_bands()
    {
        using SpikeHost host = new(SmallFixture());
        host.Show();
        host.Prime(host.Left);

        TextEditor editor = host.Left;
        TextArea area = editor.TextArea;
        TextView view = area.TextView;
        area.SelectionBrush = Brushes.Transparent;
        area.SelectionBorder = null;
        area.Caret.CaretBrush = Brushes.Transparent;
        view.BackgroundRenderers.Add(new TextBandSelectionRenderer(area, Brushes.Red));
        view.BackgroundRenderers.Add(new TextBandCaretRenderer(area, Brushes.Blue));

        DocumentLine line30 = editor.Document.GetLineByNumber(30);
        DocumentLine line31 = editor.Document.GetLineByNumber(31);
        DocumentLine line32 = editor.Document.GetLineByNumber(32);
        SpikeHost.ScrollTo(editor, SpikeHost.TopOfLine(editor, 30));
        area.Selection = Selection.Create(area, line30.Offset, line32.EndOffset);
        area.Caret.Offset = line31.Offset;
        area.Focus();
        using WriteableBitmap frame = host.Capture();

        double lineHeight = view.DefaultLineHeight;
        PaddingMetrics metrics = MetricsOf(editor);
        Point origin = host.ToWindow(editor, new Point(0, 0));
        double top30 = SpikeHost.TopOfLine(editor, 30) - editor.VerticalOffset;
        double top31 = SpikeHost.TopOfLine(editor, 31) - editor.VerticalOffset;
        Assert.Equal(lineHeight, top31 - top30, Tolerance);

        // Selection: red in the text bands of lines 30 and 31, nothing in the three padding rows between them.
        PixelRect paddingRows = PixelProbe.Inside(origin.X, origin.Y + top31, origin.X + 150, origin.Y + top31 + 3 * lineHeight);
        PixelRect textBand30 = PixelProbe.Inside(origin.X, origin.Y + top30 + metrics.HalfSlack, origin.X + 150, origin.Y + top30 + lineHeight - metrics.HalfSlack);
        PixelRect textBand31 = PixelProbe.Inside(origin.X, origin.Y + top31 + 3 * lineHeight + metrics.HalfSlack, origin.X + 150, origin.Y + top31 + 4 * lineHeight - metrics.HalfSlack);
        Assert.Equal(0, PixelProbe.Count(frame, paddingRows, PixelProbe.IsRed));
        Assert.True(PixelProbe.Count(frame, textBand30, PixelProbe.IsRed) > 0, "line 30's text band should carry the selection brush");
        Assert.True(PixelProbe.Count(frame, textBand31, PixelProbe.IsRed) > 0, "line 31's text band should carry the selection brush");

        // Caret at the start of line 31: blue in its text band only, one text line tall.
        int caretWidth = (int)TextBandCaretRenderer.Width;
        PixelRect caretPaddingRows = new((int)origin.X, (int)(origin.Y + top31) + 1, caretWidth, (int)(3 * lineHeight) - 2);
        PixelRect caretTextBand = new((int)origin.X, (int)Math.Ceiling(origin.Y + top31 + 3 * lineHeight + metrics.HalfSlack) + 1, caretWidth, (int)metrics.TextHeight - 2);
        Assert.Equal(0, PixelProbe.Count(frame, caretPaddingRows, PixelProbe.IsBlue));
        Assert.True(PixelProbe.Count(frame, caretTextBand, PixelProbe.IsBlue) > 0, "the caret should be drawn in line 31's text band");
        Assert.Equal(0, PixelProbe.Count(frame, caretPaddingRows, PixelProbe.IsDark));
    }

    [AvaloniaFact]
    public void Item5_offset_sync_is_one_to_one_at_top_middle_and_bottom()
    {
        using SpikeHost host = new(SmallFixture());
        host.Show();
        host.Prime(host.Left);
        host.Prime(host.Right);

        ScrollViewer leftScroll = SpikeHost.ScrollViewerOf(host.Left);
        ScrollViewer rightScroll = SpikeHost.ScrollViewerOf(host.Right);
        leftScroll.ScrollChanged += (_, _) =>
        {
            if (Math.Abs(rightScroll.Offset.Y - leftScroll.Offset.Y) > Tolerance)
            {
                rightScroll.Offset = rightScroll.Offset.WithY(leftScroll.Offset.Y);
            }
        };

        double maxOffset = host.Left.ExtentHeight - host.Left.ViewportHeight;
        Assert.True(maxOffset > 0, "the fixture should be taller than the viewport");
        Assert.Equal(maxOffset, host.Right.ExtentHeight - host.Right.ViewportHeight, Tolerance);

        foreach (double offset in new[] { 0, maxOffset / 2, maxOffset })
        {
            SpikeHost.ScrollTo(host.Left, offset);
            Assert.Equal(offset, host.Left.VerticalOffset, Tolerance);
            Assert.Equal(offset, host.Right.VerticalOffset, Tolerance);
            Assert.Equal(host.Left.TextArea.TextView.VisualLines[0].VisualTop, host.Right.TextArea.TextView.VisualLines[0].VisualTop, Tolerance);
            AssertRowsAligned(host);
        }

        // At the bottom both panes end on their last row.
        double lineHeight = host.Left.TextArea.TextView.DefaultLineHeight;
        Assert.Equal(host.Left.ExtentHeight, SpikeHost.TopOfLine(host.Left, 94) + lineHeight, Tolerance);
        Assert.Equal(host.Right.ExtentHeight, SpikeHost.TopOfLine(host.Right, 90) + 5 * lineHeight, Tolerance);
    }

    [AvaloniaFact]
    [Trait("Category", "Perf")]
    public void Item6_priming_cost_for_ten_thousand_gaps()
    {
        AlignmentOp[] ops = Enumerable.Range(0, 10_000).SelectMany(_ => new AlignmentOp[] { new Same(1), new RightOnly(1) }).ToArray();
        AlignmentFixture fixture = AlignmentFixture.Build(ops);
        Assert.Equal(20_000, fixture.RowCount);
        Assert.Equal(10_000, fixture.LeftPadding.Values.Sum(p => (p.Above > 0 ? 1 : 0) + (p.Below > 0 ? 1 : 0)));

        using SpikeHost host = new(fixture);
        host.Show();
        TextView view = host.Left.TextArea.TextView;
        double lineHeight = view.DefaultLineHeight;

        Stopwatch stopwatch = Stopwatch.StartNew();
        host.Prime(host.Left);
        TimeSpan singleBatch = stopwatch.Elapsed;
        Assert.Equal(20_000 * lineHeight, view.DocumentHeight, 1e-3);

        host.Left.Document = new TextDocument(fixture.LeftText);
        SpikeHost.Layout();
        view = host.Left.TextArea.TextView;
        stopwatch.Restart();
        host.Prime(host.Left, batchSize: 256);
        TimeSpan batched = stopwatch.Elapsed;
        Assert.Equal(20_000 * lineHeight, view.DocumentHeight, 1e-3);

        TestContext.Current.TestOutputHelper?.WriteLine(
            $"Priming 10,000 padding gaps: one batch {singleBatch.TotalMilliseconds:F0} ms; batches of 256 {batched.TotalMilliseconds:F0} ms.");
    }

    /// <summary>
    /// Every pair of lines that share a row sits at the same document-relative top. A padded
    /// line's height-tree position is the top of its padding block, so its text row is that
    /// position plus the rows of padding above it.
    /// </summary>
    private static void AssertRowsAligned(SpikeHost host)
    {
        foreach ((int left, int right) in host.Fixture.AlignedPairs)
        {
            Assert.Equal(RowTopOfLine(host, host.Left, left), RowTopOfLine(host, host.Right, right), Tolerance);
        }
    }

    private static double RowTopOfLine(SpikeHost host, TextEditor editor, int lineNumber)
    {
        double lineHeight = editor.TextArea.TextView.DefaultLineHeight;
        return SpikeHost.TopOfLine(editor, lineNumber) + host.PaddingOf(editor).GetValueOrDefault(lineNumber).Above * lineHeight;
    }

    private static double Y(VisualLine line, VisualYPosition position)
    {
        return line.GetTextLineVisualYPosition(line.TextLines[0], position);
    }

    private static PaddingMetrics MetricsOf(TextEditor editor)
    {
        Typeface typeface = new(editor.FontFamily, editor.FontStyle, editor.FontWeight);
        TextMetrics metrics = new(typeface.GlyphTypeface, editor.FontSize);
        return new PaddingMetrics(-metrics.Ascent, metrics.Descent, metrics.LineGap, editor.TextArea.TextView.DefaultLineHeight);
    }

    private static void Press(SpikeHost host, Key key, PhysicalKey physicalKey)
    {
        host.Window.KeyPress(key, RawInputModifiers.None, physicalKey, null);
        host.Window.KeyRelease(key, RawInputModifiers.None, physicalKey, null);
        SpikeHost.Layout();
    }

    private static void Click(SpikeHost host, TextEditor editor, Point textViewPoint)
    {
        Point point = host.ToWindow(editor, textViewPoint);
        host.Window.MouseDown(point, MouseButton.Left);
        host.Window.MouseUp(point, MouseButton.Left);
        SpikeHost.Layout();
    }
}
