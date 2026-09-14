using AvaloniaEdit.Document;
using AvaloniaEdit.Rendering;

namespace Bennewitz.Ninja.DiffView;

/// <summary>
/// Builds every padded line once so the height tree learns its height before the line scrolls
/// into view; the scroll extents of two panes are then equal before any scrolling.
/// <c>GetOrConstructVisualLine</c> keeps each built line in the text view's list and re-walks
/// that list per call, so long runs are cut into batches separated by a <c>Redraw</c>, which
/// drops the built lines but keeps their heights. A line that was padded by the previous
/// document and is not padded now keeps its stale height until it is rebuilt, so a re-prime
/// covers the union of the old and the new padded sets.
/// </summary>
internal sealed class PaddingHeightPrimer
{
    /// <summary>
    /// Lines built between two <c>Redraw</c>s. Measured in the Phase 1 spike on 10,000 gaps:
    /// one pass 10.3–10.5 s, batches of 256 with a redraw between them 200–240 ms
    /// (<c>DECISIONS.md</c>, "Virtual padding: go").
    /// </summary>
    public const int BatchSize = 256;

    private const double LineHeightTolerance = 1e-6;

    private TextDocument? _document;
    private HashSet<int> _primed = [];
    private double _lineHeight = double.NaN;

    /// <summary>Whether a prime has run for the current document.</summary>
    public bool HasPrimed => _document is not null;

    /// <summary>The lines the last prime built or rebuilt.</summary>
    public IReadOnlyCollection<int> PrimedLines => _primed;

    /// <summary>
    /// Whether the text view's default line height has moved since the last prime of the same
    /// document — a font change rebases only lines still at the old default height, so padded
    /// lines keep stale heights until they are primed again.
    /// </summary>
    public bool LineHeightChanged(TextView textView)
    {
        return HasPrimed
               && ReferenceEquals(textView.Document, _document)
               && Math.Abs(textView.DefaultLineHeight - _lineHeight) > LineHeightTolerance;
    }

    /// <summary>
    /// Builds every line in <paramref name="paddedLineNumbers"/> (1-based), and every line the
    /// previous prime of the same document built, in batches; then asks for the measure pass
    /// that republishes the extent. Returns the number of lines built.
    /// </summary>
    public int Prime(TextView textView, IEnumerable<int> paddedLineNumbers)
    {
        TextDocument document = textView.Document ?? throw new InvalidOperationException("The text view has no document to prime.");
        HashSet<int> current = [.. paddedLineNumbers];
        IEnumerable<int> lines = ReferenceEquals(document, _document) ? current.Union(_primed) : current;

        int built = 0;
        int inBatch = 0;
        foreach (int lineNumber in lines.Order())
        {
            if (lineNumber < 1 || lineNumber > document.LineCount)
            {
                continue;
            }

            textView.GetOrConstructVisualLine(document.GetLineByNumber(lineNumber));
            built++;
            if (++inBatch >= BatchSize)
            {
                textView.Redraw();
                inBatch = 0;
            }
        }

        // Heights are in the tree, but the scroll extent is published only by a measure pass.
        textView.InvalidateMeasure();

        _document = document;
        _primed = current;
        _lineHeight = textView.DefaultLineHeight;
        return built;
    }

    /// <summary>Drops what was primed: the document was swapped, so its height tree is new.</summary>
    public void Forget()
    {
        _document = null;
        _primed = [];
        _lineHeight = double.NaN;
    }
}
