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
    private IReadOnlyList<(int First, int Last)> _ranges = [];

    /// <param name="onFault">Receives the faulting line, when known, and the exception.</param>
    public FoldPlaceholderGenerator(Action<int?, Exception> onFault)
    {
        _onFault = onFault;
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
                return new FormattedTextElement(text, end - offset);
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
