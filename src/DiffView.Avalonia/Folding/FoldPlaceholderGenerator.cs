using Avalonia;
using Avalonia.Input;
using Avalonia.Media;
using Avalonia.Media.TextFormatting;
using AvaloniaEdit.Document;
using AvaloniaEdit.Rendering;

namespace Bennewitz.Ninja.DiffView;

/// <summary>
/// A collapsed fold: the section AvaloniaEdit keeps current as the text changes, and the identity the
/// fold was collapsed under — its first line as the model had it, which is how the composite names a
/// fold, and which an edit before the re-diff does not move.
/// </summary>
internal readonly record struct CollapsedFold(CollapsedLineSection Section, int Identity);

/// <summary>
/// Emits one element per folded run, spanning the run's collapsed lines. Collapsing alone does not
/// hide them: <c>TextView</c> walks from a visual line to <c>LastDocumentLine.NextLine</c> without
/// skipping what is collapsed, and throws when the next line turns out to be. The element is what
/// makes one visual line span the run — which is what <c>TextView.CollapseLines</c> means by "do
/// not call it without providing a corresponding VisualLineElementGenerator".
/// </summary>
/// <remarks>
/// <para>
/// It is interested in the <em>end</em> offset of the line before a collapsed range, where
/// <see cref="PaddingElementGenerator"/> is interested in a line's start, so the two do not
/// compete for an offset except on an empty line — and a run's first line carries no padding, by
/// the boundary rule that chose it. A layout boundary like the padding generator: any exception is
/// reported once, the generator disables itself, and from then on it emits nothing.
/// </para>
/// <para>
/// ⛔ <b>It reads the sections, never line numbers of its own.</b> AvaloniaEdit moves a section with
/// its lines, shrinks one whose first or last line is deleted and uncollapses one whose lines are all
/// deleted, and the height tree is what the text view checks this generator against. Line numbers kept
/// from the moment of collapsing go stale with the first edit that moves a line, and the next layout
/// pass throws — plan 00031, open item 12.
/// </para>
/// </remarks>
internal sealed class FoldPlaceholderGenerator : VisualLineElementGenerator
{
    private readonly Action<int?, Exception> _onFault;
    private readonly Action<int> _onExpand;
    private IReadOnlyList<CollapsedFold> _folds = [];

    /// <param name="onFault">Receives the faulting line, when known, and the exception.</param>
    /// <param name="onExpand">Receives a fold's identity when its placeholder is clicked.</param>
    public FoldPlaceholderGenerator(Action<int?, Exception> onFault, Action<int> onExpand)
    {
        _onFault = onFault;
        _onExpand = onExpand;
    }

    /// <summary>Whether a fault has disabled the generator.</summary>
    public bool IsDisabled { get; private set; }

    /// <summary>
    /// The folds as they are now, in line order: each one's first and last line, 1-based and inclusive,
    /// read from its section, and the identity it was collapsed under. A fold whose lines were all
    /// deleted is no longer collapsed, and is left out.
    /// </summary>
    public IReadOnlyList<(int First, int Last, int Identity)> Folds => [.. LiveFolds()];

    /// <summary>Re-enables the generator after a fault.</summary>
    public void Reset()
    {
        IsDisabled = false;
    }

    /// <summary>Takes the folds the pane collapsed, which must be ordered and disjoint.</summary>
    public void SetFolds(IReadOnlyList<CollapsedFold> folds)
    {
        _folds = folds;
    }

    public override int GetFirstInterestedOffset(int startOffset)
    {
        if (IsDisabled || _folds.Count == 0)
        {
            return -1;
        }

        try
        {
            TextDocument document = CurrentContext.Document;
            foreach ((int first, int _, int _) in LiveFolds())
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
        if (IsDisabled || _folds.Count == 0)
        {
            return null;
        }

        int? lineNumber = null;
        try
        {
            TextDocument document = CurrentContext.Document;
            foreach ((int first, int last, int identity) in LiveFolds())
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
                return new FoldPlaceholderElement(text, end - offset, identity, _onExpand);
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
    /// The folds whose sections are still collapsed, each at the lines its section holds now. The
    /// height tree sets a section's ends to null when it uncollapses one, so a fold is read only
    /// while both ends are there.
    /// </summary>
    private IEnumerable<(int First, int Last, int Identity)> LiveFolds()
    {
        foreach (CollapsedFold fold in _folds)
        {
            if (fold.Section is { IsCollapsed: true, Start: { } start, End: { } end })
            {
                yield return (start.LineNumber, end.LineNumber, fold.Identity);
            }
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
internal sealed class FoldPlaceholderElement(string text, int documentLength, int identity, Action<int> onExpand)
    : FormattedTextElement(text, documentLength)
{
    /// <summary>The text drawn in place of the run, kept because the base class discards it once formatted.</summary>
    public string PlaceholderText { get; } = text;

    /// <summary>
    /// The identity of the fold this stands in for: its first line as the model had it when the pane
    /// collapsed it, which is how the composite names the fold — not where the fold is now, which an
    /// edit before the re-diff can move.
    /// </summary>
    public int Identity { get; } = identity;

    /// <summary>Asks for the run back. What a click does, and what a test can do without pixels.</summary>
    public void Expand()
    {
        onExpand(Identity);
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
