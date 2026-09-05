using Avalonia;
using Avalonia.Media.TextFormatting;
using AvaloniaEdit;
using AvaloniaEdit.Document;
using AvaloniaEdit.Editing;
using AvaloniaEdit.Rendering;

namespace Bennewitz.Ninja.DiffView.Avalonia;

/// <summary>
/// Text-band rectangles in text-view coordinates with the scroll offset applied. AvaloniaEdit's
/// own selection and caret layers span the full visual-line extent (<c>LineTop</c> to
/// <c>LineBottom</c>), which on a padded line includes the padding; these use <c>TextTop</c> and
/// <c>TextBottom</c>, the natural text height inside the row, so nothing paints padding space.
/// </summary>
internal static class TextBands
{
    private const double EmptyLineWidth = 1;

    /// <summary>One rectangle per visual line the segment touches, over the text band only.</summary>
    public static IEnumerable<Rect> ForSegment(TextView textView, SelectionSegment segment)
    {
        TextDocument document = textView.Document;
        TextViewPosition start = new(document.GetLocation(segment.StartOffset), segment.StartVisualColumn);
        TextViewPosition end = new(document.GetLocation(segment.EndOffset), segment.EndVisualColumn);
        Vector scroll = textView.ScrollOffset;

        foreach (VisualLine line in textView.VisualLines)
        {
            int lineStart = line.FirstDocumentLine.Offset;
            int lineEnd = line.LastDocumentLine.EndOffset;
            if (lineStart > segment.EndOffset)
            {
                break;
            }

            if (lineEnd < segment.StartOffset)
            {
                continue;
            }

            int startColumn = segment.StartOffset < lineStart ? 0 : line.ValidateVisualColumn(start, allowVirtualSpace: false);
            int endColumn = segment.EndOffset > lineEnd ? line.VisualLength : line.ValidateVisualColumn(end, allowVirtualSpace: false);

            // Word wrap is forced off: one TextLine per visual line.
            TextLine textLine = line.TextLines[0];
            double top = line.GetTextLineVisualYPosition(textLine, VisualYPosition.TextTop) - scroll.Y;
            double bottom = line.GetTextLineVisualYPosition(textLine, VisualYPosition.TextBottom) - scroll.Y;
            double left = line.GetTextLineVisualXPosition(textLine, startColumn) - scroll.X;
            double right = startColumn == endColumn
                ? left + EmptyLineWidth
                : line.GetTextLineVisualXPosition(textLine, endColumn) - scroll.X;

            yield return new Rect(left, top, right - left, bottom - top);
        }
    }

    /// <summary>The caret rectangle over its line's text band, or <c>null</c> when the caret line is not visible.</summary>
    public static Rect? ForCaret(TextView textView, Caret caret, double width)
    {
        VisualLine? line = textView.GetVisualLine(caret.Line);
        if (line is null)
        {
            return null;
        }

        TextLine textLine = line.TextLines[0];
        Vector scroll = textView.ScrollOffset;
        double x = line.GetTextLineVisualXPosition(textLine, caret.Position.VisualColumn) - scroll.X;
        double top = line.GetTextLineVisualYPosition(textLine, VisualYPosition.TextTop) - scroll.Y;
        double bottom = line.GetTextLineVisualYPosition(textLine, VisualYPosition.TextBottom) - scroll.Y;
        return new Rect(x, top, width, bottom - top);
    }
}
