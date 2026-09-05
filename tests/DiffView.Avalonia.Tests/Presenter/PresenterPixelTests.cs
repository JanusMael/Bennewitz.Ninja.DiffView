using Avalonia;
using Avalonia.Headless;
using Avalonia.Headless.XUnit;
using Avalonia.Input;
using Avalonia.Media;
using Avalonia.Media.Imaging;
using AvaloniaEdit.Document;
using AvaloniaEdit.Editing;
using AvaloniaEdit.Rendering;
using Bennewitz.Ninja.DiffView.Core;

namespace Bennewitz.Ninja.DiffView.Avalonia.Tests.Presenter;

/// <summary>
/// Plan 00001 §Phase 4, the pixel assertions: inserted and deleted rows and padding space carry
/// their theme brushes; a selection across a padded line paints only text bands; the caret on a
/// padded line is one text line tall; and the caret-column wart the Phase 1 spike found is fixed.
/// </summary>
public sealed class PresenterPixelTests
{
    private const double Tolerance = PresenterHost.Tolerance;

    [AvaloniaFact]
    public void Inserted_and_deleted_rows_and_padding_space_carry_their_theme_brushes()
    {
        // Wide enough that the band sampled at the right end of each row lies past the longest line.
        using PresenterHost host = PresenterHost.Small(width: 1400, height: 640);
        host.Show();
        using WriteableBitmap frame = host.Capture();
        SideBySideDocument document = host.Document;
        double lineHeight = host.Left.TextArea.TextView.DefaultLineHeight;
        Assert.Equal(0, host.Left.VerticalOffset, Tolerance);
        Assert.True(document.Rows.Count * lineHeight <= host.Left.TextArea.TextView.Bounds.Height, "the whole fixture should be visible");

        Color paneBackground = PresenterHost.Token("DiffView.PaneBackgroundBrush");
        Color inserted = PresenterHost.Composite(PresenterHost.Token("DiffView.InsertedBrush"), paneBackground);
        Color deleted = PresenterHost.Composite(PresenterHost.Token("DiffView.DeletedBrush"), paneBackground);
        Color modified = PresenterHost.Composite(PresenterHost.Token("DiffView.ModifiedBrush"), paneBackground);
        Color paddingBrush = PresenterHost.Token("DiffView.PaddingBrush");
        Color padding = PresenterHost.Composite(paddingBrush, paneBackground);
        Color hatch = PresenterHost.Composite(paddingBrush, padding);

        int insertedRow = document.Rows.ToList().FindIndex(r => r.Kind == DiffLineKind.Inserted);
        int deletedRow = document.Rows.ToList().FindIndex(r => r.Kind == DiffLineKind.Deleted);
        int modifiedRow = document.Rows.ToList().FindIndex(r => r.Kind == DiffLineKind.Modified);
        int unchangedRow = document.Rows.ToList().FindIndex(r => r.Kind == DiffLineKind.Unchanged);
        Assert.True(insertedRow >= 0 && deletedRow >= 0 && modifiedRow >= 0 && unchangedRow >= 0);

        // Rows are aligned from the top with no scrolling, so row r spans [r · lineHeight, (r + 1) · lineHeight) in both panes.
        AssertBand(host, host.Right, frame, insertedRow, lineHeight, c => PresenterHost.Near(c, inserted), "inserted row on the right");
        AssertPadding(host, host.Left, frame, insertedRow, lineHeight, padding, hatch, "padding opposite the inserted row");
        AssertBand(host, host.Left, frame, deletedRow, lineHeight, c => PresenterHost.Near(c, deleted), "deleted row on the left");
        AssertPadding(host, host.Right, frame, deletedRow, lineHeight, padding, hatch, "padding opposite the deleted row");
        AssertBand(host, host.Left, frame, modifiedRow, lineHeight, c => PresenterHost.Near(c, modified), "modified row on the left");
        AssertBand(host, host.Right, frame, modifiedRow, lineHeight, c => PresenterHost.Near(c, modified), "modified row on the right");
        AssertBand(host, host.Left, frame, unchangedRow, lineHeight, c => PresenterHost.Near(c, paneBackground), "unchanged row on the left");
    }

    [AvaloniaFact]
    public void A_selection_across_a_padded_line_paints_only_text_bands_and_the_caret_is_one_text_line_tall()
    {
        using PresenterHost host = PresenterHost.Small(width: 900, height: 640);
        host.Show();
        DiffPanePresenter pane = host.Left;
        TextArea area = pane.TextArea;
        TextView view = area.TextView;
        double lineHeight = view.DefaultLineHeight;
        // A padded line with indented text: the selection has width there, and the caret column is glyph-free.
        int paddedLine = Enumerable.Range(1, pane.Document.LineCount)
            .First(n => Padding.Before(host.Document, DiffSide.Left, n - 1) > 0 && pane.Document.GetText(pane.Document.GetLineByNumber(n)).StartsWith(' '));
        int above = Padding.Before(host.Document, DiffSide.Left, paddedLine - 1);
        DocumentLine before = pane.Document.GetLineByNumber(paddedLine - 1);
        DocumentLine padded = pane.Document.GetLineByNumber(paddedLine);

        area.Selection = Selection.Create(area, before.Offset, padded.EndOffset);
        area.Caret.Offset = padded.Offset;
        area.Focus();
        PresenterHost.Layout();
        Assert.True(area.IsFocused, "the text area should take keyboard focus");
        Assert.True(pane.CaretRenderer.IsCaretShown);
        using WriteableBitmap frame = host.Capture();

        // The selection layer sits above the row tint, so the expected colour depends on the line's kind.
        Color paneBackground = PresenterHost.Token("DiffView.PaneBackgroundBrush");
        Color selectionBrush = PresenterHost.Token("DiffView.SelectionBrush");
        Color SelectionOn(int lineNumber)
        {
            string? tint = host.Document.Left.Lines[lineNumber - 1].Kind switch
            {
                DiffLineKind.Inserted => "DiffView.InsertedBrush",
                DiffLineKind.Deleted => "DiffView.DeletedBrush",
                DiffLineKind.Modified => "DiffView.ModifiedBrush",
                _ => null,
            };
            Color row = tint is null ? paneBackground : PresenterHost.Composite(PresenterHost.Token(tint), paneBackground);
            return PresenterHost.Composite(selectionBrush, row);
        }

        Color selectionBefore = SelectionOn(paddedLine - 1);
        Color selectionPadded = SelectionOn(paddedLine);
        Point origin = host.ToWindow(pane, new Point(0, 0));
        double topBefore = PresenterHost.TopOfLine(pane, paddedLine - 1) - pane.VerticalOffset;
        double topPadded = PresenterHost.TopOfLine(pane, paddedLine) - pane.VerticalOffset;
        Assert.Equal(lineHeight, topPadded - topBefore, Tolerance);
        VisualLine visual = view.GetVisualLine(paddedLine) ?? throw new InvalidOperationException("the padded line is not visible");
        double textTop = visual.GetTextLineVisualYPosition(visual.TextLines[0], VisualYPosition.TextTop) - view.VerticalOffset;
        double textBottom = visual.GetTextLineVisualYPosition(visual.TextLines[0], VisualYPosition.TextBottom) - view.VerticalOffset;
        Assert.True(textTop > topPadded + above * lineHeight - Tolerance, "the text band starts after the padding rows");

        // Selection: in the text bands of both lines, and nowhere in the padding rows between them.
        PixelRect paddingRows = PixelProbe.Inside(origin.X, origin.Y + topPadded, origin.X + 120, origin.Y + topPadded + above * lineHeight);
        PixelRect bandBefore = PixelProbe.Inside(origin.X, origin.Y + topBefore, origin.X + 120, origin.Y + topBefore + lineHeight);
        PixelRect bandPadded = PixelProbe.Inside(origin.X, origin.Y + textTop, origin.X + 120, origin.Y + textBottom);
        Assert.Equal(0, PixelProbe.Count(frame, paddingRows, c => PresenterHost.Near(c, selectionBefore) || PresenterHost.Near(c, selectionPadded)));
        Assert.True(PixelProbe.Count(frame, bandBefore, c => PresenterHost.Near(c, selectionBefore)) > 0, "the line before the padding should carry the selection brush");
        Assert.True(PixelProbe.Count(frame, bandPadded, c => PresenterHost.Near(c, selectionPadded)) > 0, "the padded line's text band should carry the selection brush");

        // Caret at the start of the padded line: dark pixels in its text band only, none in the padding rows above.
        int caretWidth = (int)DiffCaretRenderer.Width;
        PixelRect caretPadding = new((int)origin.X, (int)(origin.Y + topPadded) + 1, caretWidth, (int)(above * lineHeight) - 2);
        PixelRect caretBand = new((int)origin.X, (int)Math.Ceiling(origin.Y + textTop) + 1, caretWidth, (int)(textBottom - textTop) - 2);
        Assert.Equal(0, PixelProbe.Count(frame, caretPadding, PixelProbe.IsDark));
        Assert.True(PixelProbe.Count(frame, caretBand, PixelProbe.IsDark) > 0, "the caret should be drawn in the padded line's text band");

        // Focus moves to the other pane: this caret is gone, the selection stays.
        host.Right.TextArea.Focus();
        PresenterHost.Layout();
        Assert.False(area.IsFocused);
        Assert.True(host.Right.TextArea.IsFocused);
        using WriteableBitmap unfocused = host.Capture();
        Assert.Equal(0, PixelProbe.Count(unfocused, caretBand, PixelProbe.IsDark));
        Assert.True(PixelProbe.Count(unfocused, bandBefore, c => PresenterHost.Near(c, selectionBefore)) > 0);
    }

    [AvaloniaFact]
    public void Home_pressed_twice_on_a_padded_line_keeps_the_caret_on_the_first_text_column()
    {
        using PresenterHost host = PresenterHost.Small(width: 900, height: 640);
        host.Show();
        DiffPanePresenter pane = host.Left;
        Caret caret = pane.TextArea.Caret;
        int lineNumber = Enumerable.Range(1, pane.Document.LineCount)
            .First(n => Padding.Before(host.Document, DiffSide.Left, n - 1) > 0 && pane.Document.GetText(pane.Document.GetLineByNumber(n)).StartsWith(' '));
        DocumentLine line = pane.Document.GetLineByNumber(lineNumber);
        int firstNonBlank = pane.Document.GetText(line).TakeWhile(char.IsWhiteSpace).Count() + 1;

        pane.TextArea.Focus();
        caret.Offset = line.EndOffset;
        PresenterHost.Layout();

        Press(host, Key.Home, PhysicalKey.Home);
        Assert.Equal(new TextLocation(lineNumber, firstNonBlank), caret.Location);

        // The second Home is AvalonEdit's "before the indentation": column 1, and never the padding column.
        Press(host, Key.Home, PhysicalKey.Home);
        Assert.Equal(new TextLocation(lineNumber, 1), caret.Location);
        Assert.Equal(1, caret.Position.VisualColumn);

        Press(host, Key.Right, PhysicalKey.ArrowRight);
        Assert.Equal(new TextLocation(lineNumber, 2), caret.Location);
        Assert.Empty(pane.Faults);
    }

    private static void AssertBand(PresenterHost host, DiffPanePresenter pane, WriteableBitmap frame, int row, double lineHeight, Func<Color, bool> predicate, string what)
    {
        PixelRect area = RowBand(host, pane, row, lineHeight);
        int count = PixelProbe.Count(frame, area, predicate);
        int total = area.Width * area.Height;
        Assert.True(count >= 0.9 * total, $"{what}: {count} of {total} pixels carry the expected brush");
    }

    private static void AssertPadding(PresenterHost host, DiffPanePresenter pane, WriteableBitmap frame, int row, double lineHeight, Color fill, Color hatch, string what)
    {
        PixelRect area = RowBand(host, pane, row, lineHeight);
        int total = area.Width * area.Height;
        int filled = PixelProbe.Count(frame, area, c => PresenterHost.Near(c, fill));
        int hatched = PixelProbe.Count(frame, area, c => PresenterHost.Near(c, hatch, 12));
        Assert.True(filled > 0, $"{what}: no pixel carries the padding fill");
        Assert.True(hatched > 0, $"{what}: no pixel carries the hatch");
        Assert.True(filled + hatched >= 0.9 * total, $"{what}: {filled} filled + {hatched} hatched of {total}");
    }

    /// <summary>The right-hand end of a row, past any text, in window pixels.</summary>
    private static PixelRect RowBand(PresenterHost host, DiffPanePresenter pane, int row, double lineHeight)
    {
        Point origin = host.ToWindow(pane, new Point(0, 0));
        double width = pane.TextArea.TextView.Bounds.Width;
        return PixelProbe.Inside(origin.X + width - 40, origin.Y + row * lineHeight, origin.X + width - 4, origin.Y + (row + 1) * lineHeight);
    }

    private static void Press(PresenterHost host, Key key, PhysicalKey physicalKey)
    {
        host.Window.KeyPress(key, RawInputModifiers.None, physicalKey, null);
        host.Window.KeyRelease(key, RawInputModifiers.None, physicalKey, null);
        PresenterHost.Layout();
    }
}
