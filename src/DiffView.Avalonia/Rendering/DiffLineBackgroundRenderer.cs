using Avalonia;
using Avalonia.Media;
using Avalonia.Media.TextFormatting;
using AvaloniaEdit.Rendering;
using Bennewitz.Ninja.DiffView.Core;

namespace Bennewitz.Ninja.DiffView.Avalonia;

/// <summary>What the background renderer drew for one visual line; read by tests.</summary>
internal readonly record struct DrawnLine(int LineNumber, DiffLineKind Kind, PaddingSpec Padding);

/// <summary>One word-level rectangle the background renderer drew, in text-view coordinates; read by tests.</summary>
internal readonly record struct WordRectangle(int LineNumber, PieceRange Piece, Rect Rect);

/// <summary>
/// Fills every visible row by kind — the full row, <c>lineHeight</c> tall, so consecutive rows
/// touch — hatches the padding space above or below a padded line, and over a modified row
/// draws a rectangle per changed word-level piece from the presenter's
/// <see cref="WordDiffLookup"/>, which computes a row's pieces on its first frame. The row
/// geometry comes from the <see cref="PaddingElement"/> the visual line actually carries, not
/// from the metadata, so a disabled generator leaves no phantom padding fills. Draws on
/// <see cref="KnownLayer.Background"/>, under the selection and the text.
/// </summary>
internal sealed class DiffLineBackgroundRenderer : GuardedBackgroundRenderer
{
    private const double HatchSpacing = 6;

    private readonly List<DrawnLine> _lastDrawn = [];
    private readonly List<WordRectangle> _lastWordRectangles = [];

    public DiffLineBackgroundRenderer(DiffPanePresenter owner)
        : base(owner, KnownLayer.Background, nameof(DiffLineBackgroundRenderer))
    {
    }

    /// <summary>The lines of the last frame, in order, with the kind and padding drawn for each.</summary>
    public IReadOnlyList<DrawnLine> LastDrawn => _lastDrawn;

    /// <summary>The word-level rectangles of the last frame, in line and piece order.</summary>
    public IReadOnlyList<WordRectangle> LastWordRectangles => _lastWordRectangles;

    /// <summary>The padding the visual line carries, from its element; none when the generator emitted nothing.</summary>
    public static PaddingSpec PaddingOf(VisualLine line)
    {
        foreach (VisualLineElement element in line.Elements)
        {
            if (element is PaddingElement padding)
            {
                return padding.Spec;
            }
        }

        return PaddingSpec.None;
    }

    protected override void DrawCore(TextView textView, DrawingContext drawingContext)
    {
        _lastDrawn.Clear();
        _lastWordRectangles.Clear();
        double width = textView.Bounds.Width;
        if (width <= 0)
        {
            return;
        }

        double lineHeight = textView.DefaultLineHeight;
        Vector scroll = textView.ScrollOffset;
        PaneMetadata metadata = Owner.Metadata;
        DiffBrushes palette = Owner.Palette;
        IBrush paddingBrush = palette[DiffBrush.Padding];
        Pen hatchPen = new(paddingBrush, 1);

        foreach (VisualLine line in textView.VisualLines)
        {
            int lineNumber = line.FirstDocumentLine.LineNumber;
            DiffLineKind kind = metadata.KindOf(lineNumber);
            PaddingSpec padding = PaddingOf(line);
            double top = line.VisualTop - scroll.Y;
            double rowTop = top + padding.Above * lineHeight;

            if (padding.Above > 0)
            {
                DrawPadding(drawingContext, new Rect(0, top, width, padding.Above * lineHeight), paddingBrush, hatchPen);
            }

            if (kind != DiffLineKind.Unchanged)
            {
                drawingContext.FillRectangle(palette.ForKind(kind), new Rect(0, rowTop, width, lineHeight));
            }

            if (kind == DiffLineKind.Modified && Owner.WordDiffLookup is { } words && metadata.RowOf(lineNumber) is { } row)
            {
                DrawWordPieces(drawingContext, line, row, rowTop, lineHeight, scroll, words, palette);
            }

            if (padding.Below > 0)
            {
                DrawPadding(drawingContext, new Rect(0, rowTop + lineHeight, width, padding.Below * lineHeight), paddingBrush, hatchPen);
            }

            _lastDrawn.Add(new DrawnLine(lineNumber, kind, padding));
        }
    }

    /// <summary>
    /// One rectangle per changed piece of this side's line, over the row, from the piece's
    /// character range through the visual line's own column mapping (so tabs and the padding
    /// element are accounted for). Ranges are clamped to the line, which may have changed since
    /// the model was built.
    /// </summary>
    private void DrawWordPieces(DrawingContext drawingContext, VisualLine line, int row, double rowTop, double lineHeight, Vector scroll, WordDiffLookup words, DiffBrushes palette)
    {
        WordDiffPieces pieces = words.PiecesFor(row);
        IReadOnlyList<PieceRange> mine = Owner.Side == DiffSide.Left ? pieces.Left : pieces.Right;
        if (mine.Count == 0)
        {
            return;
        }

        TextLine textLine = line.TextLines[0];
        int lineLength = line.FirstDocumentLine.Length;
        foreach (PieceRange piece in mine)
        {
            if (piece.Kind == PieceKind.Unchanged)
            {
                continue;
            }

            int start = Math.Min(piece.Start, lineLength);
            int end = Math.Min(piece.End, lineLength);
            if (end <= start)
            {
                continue;
            }

            double left = line.GetTextLineVisualXPosition(textLine, line.GetVisualColumn(start)) - scroll.X;
            double right = line.GetTextLineVisualXPosition(textLine, line.GetVisualColumn(end)) - scroll.X;
            Rect rect = new(left, rowTop, Math.Max(0, right - left), lineHeight);
            drawingContext.FillRectangle(palette.ForPiece(piece.Kind), rect);
            _lastWordRectangles.Add(new WordRectangle(line.FirstDocumentLine.LineNumber, piece, rect));
        }
    }

    private static void DrawPadding(DrawingContext drawingContext, Rect area, IBrush brush, IPen pen)
    {
        drawingContext.FillRectangle(brush, area);
        using DrawingContext.PushedState clip = drawingContext.PushClip(area);
        for (double x = area.Left - area.Height; x < area.Right; x += HatchSpacing)
        {
            drawingContext.DrawLine(pen, new Point(x, area.Bottom), new Point(x + area.Height, area.Top));
        }
    }
}
