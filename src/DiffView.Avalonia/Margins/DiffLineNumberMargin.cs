using System.Globalization;
using Avalonia;
using Avalonia.Input;
using Avalonia.Media;
using AvaloniaEdit.Document;
using AvaloniaEdit.Rendering;
using Bennewitz.Ninja.DiffView.Core;

namespace Bennewitz.Ninja.DiffView.Avalonia;

/// <summary>
/// Draws the numbers of the lines the pane shows, at each line's text band, so nothing is drawn
/// over padding space. A click puts the caret on that line and focuses the pane. Width follows
/// the digit count of the line count, two digits at least.
/// </summary>
/// <remarks>
/// A side's pane draws the document's own line numbers — no lookup through the model, because
/// the document <em>is</em> that side. A unified pane draws two columns, the left side's number
/// and the right side's: a context line is in both files and carries both, a removed or added
/// line is in one and leaves the other column empty. A unified document's own numbering belongs
/// to neither side and would name no line of either file.
/// </remarks>
internal sealed class DiffLineNumberMargin : DiffMargin
{
    private const int MinimumDigits = 2;
    private const double HorizontalPadding = 6;
    private const double ColumnGap = 6;

    private readonly List<(int LineNumber, double Y)> _lastRendered = [];
    private readonly List<(int? Left, int? Right)> _lastSourceNumbers = [];
    private readonly List<(Rect Bounds, int BlockIndex, int? OverLine)> _lastCopyArrows = [];
    private int _digits = MinimumDigits;
    private int _rightDigits;

    public DiffLineNumberMargin(DiffPanePresenter owner)
        : base(owner, nameof(DiffLineNumberMargin), DiffViewStrings.LineNumbersMarginName)
    {
    }

    /// <summary>The numbers of the last frame and the y each was drawn at, in order.</summary>
    public IReadOnlyList<(int LineNumber, double Y)> LastRendered => _lastRendered;

    /// <summary>
    /// The source numbers of the last frame, one pair per line in order: the left column and the
    /// right, each <c>null</c> where the line is not that side's — or where a copy arrow took the
    /// cell, which is the one case a number the pane has is not drawn. A side's pane draws its own
    /// numbers in the left column and leaves the right one empty.
    /// </summary>
    public IReadOnlyList<(int? Left, int? Right)> LastSourceNumbers => _lastSourceNumbers;

    /// <summary>
    /// The copy arrows of the last frame: the hit-zone, the block it would copy, and the line
    /// whose number cell it took — <c>null</c> where the block has no lines on this side and the
    /// arrow was drawn in padding, costing no number at all.
    /// </summary>
    public IReadOnlyList<(Rect Bounds, int BlockIndex, int? OverLine)> LastCopyArrows => _lastCopyArrows;

    /// <summary>The tooltip for <paramref name="lineNumber"/>: the line on the other side that shares its row, or that there is none.</summary>
    public override string? TooltipFor(int lineNumber)
    {
        PaneMetadata metadata = Owner.Metadata;
        if (!metadata.Knows(lineNumber))
        {
            return null;
        }

        DiffSide side = metadata.SideOf(lineNumber);
        string mine = DiffViewStrings.SideName(side);
        string other = DiffViewStrings.SideName(side == DiffSide.Left ? DiffSide.Right : DiffSide.Left);
        int number = metadata.UnifiedLineAt(lineNumber) is { } unified ? unified.SourceLine + 1 : lineNumber;
        string line = number.ToString("N0", CultureInfo.CurrentCulture);
        string? otherLine = metadata.OtherLine(lineNumber)?.ToString("N0", CultureInfo.CurrentCulture);

        // A unified line names its own side too: its neighbours may be the other one.
        if (metadata.IsUnified)
        {
            return otherLine is null
                ? DiffViewStrings.Format(DiffViewStrings.LineTooltipUnifiedAlone, mine, line, other)
                : DiffViewStrings.Format(DiffViewStrings.LineTooltipUnifiedAligned, mine, line, other, otherLine);
        }

        return otherLine is null
            ? DiffViewStrings.Format(DiffViewStrings.LineTooltipAlone, line, other)
            : DiffViewStrings.Format(DiffViewStrings.LineTooltipAligned, line, other, otherLine);
    }

    /// <summary>The metadata was swapped: the columns may have changed width, and the numbers have changed.</summary>
    public void OnMetadataChanged()
    {
        UpdateDigits();
        InvalidateVisual();
    }

    protected override void OnDocumentChanged(TextDocument? oldDocument, TextDocument? newDocument)
    {
        if (oldDocument is not null)
        {
            oldDocument.LineCountChanged -= OnLineCountChanged;
        }

        base.OnDocumentChanged(oldDocument, newDocument);
        if (newDocument is not null)
        {
            newDocument.LineCountChanged += OnLineCountChanged;
        }

        UpdateDigits();
    }

    protected override Size MeasureOverride(Size availableSize)
    {
        IBrush foreground = Owner.Palette[DiffBrush.LineNumberForeground];
        double width = (2 * HorizontalPadding) + Format(new string('9', _digits), foreground).Width;
        if (_rightDigits > 0)
        {
            width += ColumnGap + Format(new string('9', _rightDigits), foreground).Width;
        }

        return new Size(width, 0);
    }

    protected override void RenderCore(DrawingContext context, TextView textView)
    {
        _lastRendered.Clear();
        _lastSourceNumbers.Clear();
        _lastCopyArrows.Clear();
        PaneMetadata metadata = Owner.Metadata;
        IBrush foreground = Owner.Palette[DiffBrush.LineNumberForeground];
        double right = Bounds.Width - HorizontalPadding;
        double leftColumnRight = _rightDigits > 0
            ? right - ColumnGap - Format(new string('9', _rightDigits), foreground).Width
            : right;

        // A copy arrow takes a number's cell, so it is offered only where the other side can
        // receive the copy: a read-only pair shows every number it has ever shown.
        bool offersCopy = Owner.CanCopyOut && !metadata.IsUnified && metadata.Document is not null;
        IBrush arrowBrush = Owner.Palette[DiffBrush.GutterArrow];
        double rowHeight = textView.DefaultLineHeight;

        foreach (VisualLine line in textView.VisualLines)
        {
            int number = line.FirstDocumentLine.LineNumber;
            double y = TextTopOf(line, textView);
            _lastRendered.Add((number, y));

            if (offersCopy)
            {
                DrawPaddingArrow(context, textView, metadata, line, number, leftColumnRight, rowHeight, arrowBrush);
                if (AnchorBlockOf(metadata, number) is { } anchored
                    && DrawArrow(context, arrowBrush, leftColumnRight, y, rowHeight, anchored.Index, number))
                {
                    // The arrow has this row's cell. The number it stands in for is one hover away.
                    _lastSourceNumbers.Add((null, null));
                    continue;
                }
            }

            if (metadata.UnifiedLineAt(number) is not { } unified)
            {
                Draw(context, number, leftColumnRight, y, foreground);
                _lastSourceNumbers.Add((number, null));
                continue;
            }

            int source = unified.SourceLine + 1;
            // A context line is in both files, so it carries both numbers; a removed or added
            // line is in one, and the other column stays empty.
            int? other = unified.Kind == DiffLineKind.Unchanged ? metadata.OtherLine(number) : null;
            if (unified.Side == DiffSide.Left)
            {
                Draw(context, source, leftColumnRight, y, foreground);
                if (other is { } counterpart)
                {
                    Draw(context, counterpart, right, y, foreground);
                }

                _lastSourceNumbers.Add((source, other));
            }
            else
            {
                Draw(context, source, right, y, foreground);
                if (other is { } counterpart)
                {
                    Draw(context, counterpart, leftColumnRight, y, foreground);
                }

                _lastSourceNumbers.Add((other, source));
            }
        }

        if (offersCopy)
        {
            DrawTrailingArrow(context, textView, metadata, leftColumnRight, rowHeight, arrowBrush);
        }
    }

    /// <summary>
    /// The block whose anchor row is <paramref name="lineNumber"/> — its first line on this side —
    /// or <c>null</c> where the line is not a block's first. A side's lines in a block start at
    /// the block's first row, so the anchor is the block's first row wherever this side has one.
    /// </summary>
    private static ChangeBlock? AnchorBlockOf(PaneMetadata metadata, int lineNumber)
    {
        if (metadata.BlockAt(lineNumber) is not { } block)
        {
            return null;
        }

        LineRange mine = block.LinesFor(metadata.Side);
        return !mine.IsEmpty && mine.Start + 1 == lineNumber ? block : null;
    }

    /// <summary>
    /// The arrow for a block this side has no lines in. Its rows are padding above the line that
    /// follows the block, so there is no number to stand in for and nothing is hidden. Blocks are
    /// separated by at least one unchanged row — a line on both sides — so the padding above one
    /// line belongs to exactly one block.
    /// </summary>
    private void DrawPaddingArrow(
        DrawingContext context,
        TextView textView,
        PaneMetadata metadata,
        VisualLine line,
        int lineNumber,
        double cellRight,
        double rowHeight,
        IBrush brush)
    {
        int padding = metadata.PaddingBefore(lineNumber);
        if (padding <= 0
            || metadata.RowOf(lineNumber) is not { } row
            || metadata.BlockAtRow(row - 1) is not { } block)
        {
            return;
        }

        // A side's lines in a block start at the block's first row, so a block this side *does*
        // have lines in cannot have that row in padding: the offset lands before the padding
        // begins and is rejected here. Testing the block's line range as well would be a second
        // guard on the same invariant, and nothing could make the two disagree.
        int offset = block.FirstRow - (row - padding);
        if (offset < 0 || offset >= padding)
        {
            return;
        }

        DrawArrow(context, brush, cellRight, line.VisualTop - textView.VerticalOffset + (offset * rowHeight), rowHeight, block.Index, overLine: null);
    }

    /// <summary>
    /// A one-sided block at the very end sits in trailing padding, below the last line, where a
    /// walk over the visual lines never reaches it.
    /// </summary>
    private void DrawTrailingArrow(DrawingContext context, TextView textView, PaneMetadata metadata, double cellRight, double rowHeight, IBrush brush)
    {
        if (Document is not { } document || textView.VisualLines.Count == 0)
        {
            return;
        }

        VisualLine last = textView.VisualLines[^1];
        int lineNumber = last.FirstDocumentLine.LineNumber;
        if (lineNumber != document.LineCount
            || metadata.PaddingFor(lineNumber, document.LineCount).Below <= 0
            || metadata.RowOf(lineNumber) is not { } row
            || metadata.BlockAtRow(row + 1) is not { } block
            || !block.LinesFor(metadata.Side).IsEmpty)
        {
            return;
        }

        double top = TextTopOf(last, textView) + rowHeight + ((block.FirstRow - (row + 1)) * rowHeight);
        DrawArrow(context, brush, cellRight, top, rowHeight, block.Index, overLine: null);
    }

    /// <summary>
    /// One arrow in the cell ending at <paramref name="cellRight"/>, centred on the row that
    /// starts at <paramref name="top"/>, pointing the way a copy out of this pane would travel.
    /// </summary>
    /// <returns>Whether the arrow was drawn; <c>false</c> leaves the cell to its number.</returns>
    private bool DrawArrow(DrawingContext context, IBrush brush, double cellRight, double top, double rowHeight, int blockIndex, int? overLine)
    {
        Rect zone = new(
            cellRight - CopyArrowGlyph.Size,
            top + ((rowHeight - CopyArrowGlyph.Size) / 2),
            CopyArrowGlyph.Size,
            CopyArrowGlyph.Size);
        if (zone.Left < 0)
        {
            // Narrower than the glyph: no arrow rather than a clipped one, and the number stays.
            return false;
        }

        CopyArrowGlyph.Draw(context, brush, zone, pointsLeft: Owner.Side == DiffSide.Right);
        _lastCopyArrows.Add((zone, blockIndex, overLine));
        return true;
    }

    protected override void OnPointerPressed(PointerPressedEventArgs e)
    {
        base.OnPointerPressed(e);
        if (e.Handled || TextView is null || TextArea is null || Document is null)
        {
            return;
        }

        double y = e.GetPosition(TextView).Y + TextView.VerticalOffset;
        DocumentLine? line = TextView.GetDocumentLineByVisualTop(y);
        if (line is null)
        {
            return;
        }

        TextArea.Caret.Offset = line.Offset;
        TextArea.Focus();
        e.Handled = true;
    }

    private void Draw(DrawingContext context, int number, double columnRight, double y, IBrush foreground)
    {
        FormattedText text = Format(number.ToString(CultureInfo.CurrentCulture), foreground);
        context.DrawText(text, new Point(columnRight - text.Width, y));
    }

    private void OnLineCountChanged(object? sender, EventArgs e)
    {
        UpdateDigits();
    }

    private void UpdateDigits()
    {
        PaneMetadata metadata = Owner.Metadata;
        int digits;
        int rightDigits;
        if (metadata.IsUnified && metadata.Document is { } model)
        {
            // Each column is as wide as its own side needs; a unified document's own line count
            // is the sum of both and would over-measure them.
            digits = DigitsOf(model.Left.Lines.Count);
            rightDigits = DigitsOf(model.Right.Lines.Count);
        }
        else
        {
            digits = DigitsOf(Document?.LineCount ?? 1);
            rightDigits = 0;
        }

        if (digits != _digits || rightDigits != _rightDigits)
        {
            _digits = digits;
            _rightDigits = rightDigits;
            InvalidateMeasure();
        }
    }

    private static int DigitsOf(int count)
    {
        return Math.Max(MinimumDigits, count.ToString(CultureInfo.InvariantCulture).Length);
    }
}
