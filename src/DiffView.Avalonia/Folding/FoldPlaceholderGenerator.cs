using Avalonia;
using Avalonia.Input;
using Avalonia.Media;
using Avalonia.Media.TextFormatting;
using AvaloniaEdit.Document;
using AvaloniaEdit.Rendering;

namespace Bennewitz.Ninja.DiffView.Avalonia;

/// <summary>
/// Emits one element per folded run, spanning the run's collapsed lines. Collapsing alone does not
/// hide them: <c>TextView</c> walks from a visual line to <c>LastDocumentLine.NextLine</c> without
/// skipping what is collapsed, and throws when the next line turns out to be. The element is what
/// makes one visual line span the run — which is what <c>TextView.CollapseLines</c> means by "do
/// not call it without providing a corresponding VisualLineElementGenerator".
/// </summary>
/// <remarks>
/// It is interested in the <em>end</em> offset of the line before a collapsed range, where
/// <see cref="PaddingElementGenerator"/> is interested in a line's start, so the two do not
/// compete for an offset except on an empty line — and a run's first line carries no padding, by
/// the boundary rule that chose it. A layout boundary like the padding generator: any exception is
/// reported once, the generator disables itself, and from then on it emits nothing.
/// </remarks>
internal sealed class FoldPlaceholderGenerator : VisualLineElementGenerator
{
    private readonly Action<int?, Exception> _onFault;
    private readonly Action<int> _onExpand;
    private IReadOnlyList<(int First, int Last)> _ranges = [];

    /// <param name="onFault">Receives the faulting line, when known, and the exception.</param>
    /// <param name="onExpand">Receives a run's first collapsed line when its placeholder is clicked.</param>
    public FoldPlaceholderGenerator(Action<int?, Exception> onFault, Action<int> onExpand)
    {
        _onFault = onFault;
        _onExpand = onExpand;
    }

    /// <summary>Whether a fault has disabled the generator.</summary>
    public bool IsDisabled { get; private set; }

    /// <summary>The collapsed line ranges, 1-based and inclusive, in line order.</summary>
    public IReadOnlyList<(int First, int Last)> Ranges => _ranges;

    /// <summary>Re-enables the generator after a fault.</summary>
    public void Reset()
    {
        IsDisabled = false;
    }

    /// <summary>Takes the ranges the panes collapsed, which must be ordered and disjoint.</summary>
    public void SetRanges(IReadOnlyList<(int First, int Last)> ranges)
    {
        _ranges = ranges;
    }

    public override int GetFirstInterestedOffset(int startOffset)
    {
        if (IsDisabled || _ranges.Count == 0)
        {
            return -1;
        }

        try
        {
            TextDocument document = CurrentContext.Document;
            foreach ((int first, int _) in _ranges)
            {
                if (HeaderEndOffset(document, first) is not { } offset)
                {
                    continue;
                }

                if (offset >= startOffset)
                {
                    return offset;
                }
            }

            return -1;
        }
        catch (Exception ex)
        {
            Fault(null, ex);
            return -1;
        }
    }

    public override VisualLineElement? ConstructElement(int offset)
    {
        if (IsDisabled || _ranges.Count == 0)
        {
            return null;
        }

        int? lineNumber = null;
        try
        {
            TextDocument document = CurrentContext.Document;
            foreach ((int first, int last) in _ranges)
            {
                if (HeaderEndOffset(document, first) != offset || last > document.LineCount)
                {
                    continue;
                }

                lineNumber = first;
                int end = document.GetLineByNumber(last).EndOffset;
                if (end <= offset)
                {
                    return null;
                }

                int hidden = last - first + 1;
                string text = hidden == 1
                    ? DiffViewStrings.Get(DiffViewStrings.FoldPlaceholderOne)
                    : DiffViewStrings.Format(DiffViewStrings.FoldPlaceholder, hidden.ToString("N0", System.Globalization.CultureInfo.CurrentCulture));
                return new FoldPlaceholderElement(text, end - offset, first, _onExpand);
            }

            return null;
        }
        catch (Exception ex)
        {
            Fault(lineNumber, ex);
            return null;
        }
    }

    /// <summary>
    /// Where the placeholder starts: the end of the line before the collapsed range, so that
    /// line's own text still renders and the element covers only what is hidden.
    /// </summary>
    private static int? HeaderEndOffset(TextDocument document, int firstCollapsedLine)
    {
        int header = firstCollapsedLine - 1;
        return header >= 1 && firstCollapsedLine <= document.LineCount
            ? document.GetLineByNumber(header).EndOffset
            : null;
    }

    private void Fault(int? lineNumber, Exception exception)
    {
        IsDisabled = true;
        _onFault(lineNumber, exception);
    }

}

/// <summary>
/// The run's stand-in: the text a reader sees, and the only affordance for getting the rows back.
/// The pane's document is untouched — a folded run is still in its text, so what folding takes
/// away is the drawing and not the content.
/// </summary>
internal sealed class FoldPlaceholderElement(string text, int documentLength, int firstCollapsedLine, Action<int> onExpand)
    : FormattedTextElement(text, documentLength)
{
    /// <summary>The text drawn in place of the run, kept because the base class discards it once formatted.</summary>
    public string PlaceholderText { get; } = text;

    /// <summary>The run's first collapsed line, which names the fold to the composite.</summary>
    public int FirstCollapsedLine { get; } = firstCollapsedLine;

    /// <summary>Asks for the run back. What a click does, and what a test can do without pixels.</summary>
    public void Expand()
    {
        onExpand(FirstCollapsedLine);
    }

    /// <inheritdoc/>
    public override TextRun CreateTextRun(int startVisualColumn, ITextRunConstructionContext context)
    {
        base.CreateTextRun(startVisualColumn, context);
        return new FoldPlaceholderRun(this, TextRunProperties);
    }

    // Declared protected, not protected internal: the base member's internal half belongs to
    // AvaloniaEdit's assembly, so from here only the protected half is inherited.
    protected override void OnPointerPressed(PointerPressedEventArgs e)
    {
        base.OnPointerPressed(e);
        Expand();
        e.Handled = true;
    }
}

/// <summary>
/// Draws the placeholder inside a thin outline. Without it the stand-in runs straight on from the
/// text of the line it shares — "same 1⋯ 19 matching rows hidden" reads as one sentence, where
/// the first two words are the file's and the rest is the control's. The box is also the only
/// thing that says the text can be clicked.
/// </summary>
/// <remarks>
/// Drawn from the run's own foreground, which is the pane's, so it follows a theme swap for the
/// same reason the menu's icons do rather than carrying a colour of its own.
/// </remarks>
internal sealed class FoldPlaceholderRun(FormattedTextElement element, TextRunProperties properties)
    : FormattedTextRun(element, properties)
{
    /// <summary>Inset from the run's box, so the outline does not sit on the text's own edge.</summary>
    private const double Inset = 0.5;

    public override void Draw(DrawingContext drawingContext, Point origin)
    {
        base.Draw(drawingContext, origin);
        if (Properties.ForegroundBrush is not { } brush)
        {
            return;
        }

        (double width, double height) = Size;
        if (width <= 0 || height <= 0)
        {
            return;
        }

        Rect box = new(origin.X + Inset, origin.Y + Inset, Math.Max(0, width - (2 * Inset)), Math.Max(0, height - (2 * Inset)));
        drawingContext.DrawRectangle(brush: null, new Pen(brush, 1), box, radiusX: 2, radiusY: 2);
    }
}
