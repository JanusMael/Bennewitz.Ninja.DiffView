using Avalonia;
using Avalonia.Media;
using AvaloniaEdit.Rendering;
using Bennewitz.Ninja.DiffView.Core;

namespace Bennewitz.Ninja.DiffView;

/// <summary>
/// One row the emphasis renderer filled, in text-view coordinates, with the document lines its visual line spans; read
/// by tests.
/// </summary>
internal readonly record struct EmphasisRow(int FirstLineNumber, int LastLineNumber, Rect Rect);

/// <summary>
/// Shades the row of every visual line that carries a line of the presenter's emphasis, the range of this side's lines
/// a host marks: one line height tall below any padding above the line, across the text area, so consecutive rows
/// touch and a range reads as one block wherever padding does not interrupt it. Padding belongs to the other file's
/// lines and is never shaded. A fold is one visual line spanning its run, so a fold reaching into the range shades its
/// row; lines past the document's end are not there to shade. Draws on <see cref="KnownLayer.Background"/> after the
/// <see cref="DiffLineBackgroundRenderer"/>: over the row tints and the current block's border, under the find
/// matches, the selection and the text. The <see cref="ChangeMarkerMargin"/> runs a bar beside the same rows.
/// </summary>
internal sealed class EmphasisRenderer : GuardedBackgroundRenderer
{
    private readonly List<EmphasisRow> _lastDrawn = [];

    public EmphasisRenderer(DiffPanePresenter owner)
        : base(owner, KnownLayer.Background, nameof(EmphasisRenderer))
    {
    }

    /// <summary>The rows of the last frame, in order.</summary>
    public IReadOnlyList<EmphasisRow> LastDrawn => _lastDrawn;

    /// <summary>
    /// Whether <paramref name="line"/> carries a line of <paramref name="range"/>: a visual line spans its first
    /// document line to its last, which differ only for a fold, so a fold reaching into the range is marked whole. The
    /// one place the shade and the bar beside it decide which rows are marked.
    /// </summary>
    internal static bool Marks(LineRange range, VisualLine line)
    {
        // The range's lines, 1-based and inclusive, as the visual lines number theirs. In longs, because a host may
        // hand over any two ints, and a start near the top of the range plus a count would wrap round to nothing.
        long first = (long)range.Start + 1;
        long last = (long)range.Start + range.Count;
        return line.LastDocumentLine.LineNumber >= first && line.FirstDocumentLine.LineNumber <= last;
    }

    protected override void DrawCore(TextView textView, DrawingContext drawingContext)
    {
        _lastDrawn.Clear();
        double width = textView.Bounds.Width;
        if (width <= 0 || Owner.EmphasisToDraw is not { Count: > 0 } range)
        {
            return;
        }

        double lineHeight = textView.DefaultLineHeight;
        double scrollY = textView.ScrollOffset.Y;
        IBrush brush = Owner.Palette[DiffBrush.Emphasis];

        foreach (VisualLine line in textView.VisualLines)
        {
            if (!Marks(range, line))
            {
                continue;
            }

            PaddingSpec padding = DiffLineBackgroundRenderer.PaddingOf(line);
            Rect row = new(0, line.VisualTop - scrollY + padding.Above * lineHeight, width, lineHeight);
            drawingContext.FillRectangle(brush, row);
            _lastDrawn.Add(new EmphasisRow(line.FirstDocumentLine.LineNumber, line.LastDocumentLine.LineNumber, row));
        }
    }
}
