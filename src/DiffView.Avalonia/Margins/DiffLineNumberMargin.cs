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
    /// right, each <c>null</c> where the line is not that side's. A side's pane draws its own
    /// numbers in the left column and leaves the right one empty.
    /// </summary>
    public IReadOnlyList<(int? Left, int? Right)> LastSourceNumbers => _lastSourceNumbers;

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
        PaneMetadata metadata = Owner.Metadata;
        IBrush foreground = Owner.Palette[DiffBrush.LineNumberForeground];
        double right = Bounds.Width - HorizontalPadding;
        double leftColumnRight = _rightDigits > 0
            ? right - ColumnGap - Format(new string('9', _rightDigits), foreground).Width
            : right;

        foreach (VisualLine line in textView.VisualLines)
        {
            int number = line.FirstDocumentLine.LineNumber;
            double y = TextTopOf(line, textView);
            _lastRendered.Add((number, y));

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
