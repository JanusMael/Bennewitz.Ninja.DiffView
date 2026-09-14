using Avalonia;
using Avalonia.Media;
using Avalonia.Media.TextFormatting;
using AvaloniaEdit;
using AvaloniaEdit.Document;
using AvaloniaEdit.Editing;
using AvaloniaEdit.Rendering;

namespace Bennewitz.Ninja.DiffView.Tests.Spike;

// Phase 1 spike, throwaway. AvaloniaEdit's own selection and caret layers use the full text-line
// extent (LineTop / LineBottom), which on a padded line includes the padding. These renderers
// draw from TextTop / TextBottom instead, over transparent editor brushes.

/// <summary>Text-band rectangles for a segment, in text-view coordinates with the scroll offset applied.</summary>
internal static class TextBands
{
    private const double EmptyLineWidth = 1;

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

            // No wrapping in the spike: one TextLine per visual line.
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

internal sealed class TextBandSelectionRenderer(TextArea textArea, IBrush brush) : IBackgroundRenderer
{
    public KnownLayer Layer => KnownLayer.Selection;

    public void Draw(TextView textView, DrawingContext drawingContext)
    {
        if (!textView.VisualLinesValid)
        {
            return;
        }

        foreach (SelectionSegment segment in textArea.Selection.Segments)
        {
            foreach (Rect rect in TextBands.ForSegment(textView, segment))
            {
                drawingContext.FillRectangle(brush, rect);
            }
        }
    }
}

internal sealed class TextBandCaretRenderer(TextArea textArea, IBrush brush) : IBackgroundRenderer
{
    public const double Width = 2;

    public KnownLayer Layer => KnownLayer.Caret;

    public void Draw(TextView textView, DrawingContext drawingContext)
    {
        if (!textView.VisualLinesValid)
        {
            return;
        }

        if (TextBands.ForCaret(textView, textArea.Caret, Width) is { } rect)
        {
            drawingContext.FillRectangle(brush, rect);
        }
    }
}
